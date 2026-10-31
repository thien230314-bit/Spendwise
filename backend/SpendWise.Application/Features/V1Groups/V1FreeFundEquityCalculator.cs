using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Domain.Finance;
using SpendWise.Domain.V1Groups;

namespace SpendWise.Application.Features.V1Groups;

public sealed record FreeFundMemberEquity(
    Guid UserId,
    Guid MembershipId,
    int Seat,
    long EquityVnd,
    long TotalContributedVnd,
    long TotalWithdrawnVnd,
    long TotalSharedExpenseChargedVnd);

public sealed record FreeFundEquityCalculationResult(
    IReadOnlyList<FreeFundMemberEquity> MemberEquities,
    long TotalFundEquityVnd,
    long CurrentTreasuryBalanceVnd,
    IReadOnlyDictionary<Guid, long> RefundEntitlements);

public sealed partial class V1FundService
{
    /// <summary>
    /// Computes the exact time-weighted equity timeline for all active members in a Free Fund.
    /// Replays chronological epochs based on authoritative ledger transactions and fund events.
    /// Invariants:
    /// - Each expense, withdrawal, or refund is counted strictly once using LedgerTransactionId / RelatedBusinessId.
    /// - If any event exceeds valid equity or available funds, fails with ConflictException.
    /// - Newcomers or new membership episodes start at 0 VND and never absorb past expenses.
    /// </summary>
    public async Task<FreeFundEquityCalculationResult> CalculateFreeFundEquityAsync(
        V1Fund fund,
        V1FundTreasury treasury,
        IReadOnlyList<Guid>? singleMemberRefundTargetUserIds,
        CancellationToken ct)
    {
        if (fund.Kind != V1FundKind.Free)
            throw new InvalidOperationException("CalculateFreeFundEquityAsync only applies to Free Funds.");

        var account = await db.LedgerAccounts.AsNoTracking().SingleAsync(a => a.Id == treasury.LedgerAccountId, ct);
        var currentBalance = (long)account.Balance;

        var activeMembers = await ActiveMembersAsync(fund.Id, ct);

        // If no active members
        if (activeMembers.Count == 0)
        {
            return new FreeFundEquityCalculationResult(
                [],
                0,
                currentBalance,
                new Dictionary<Guid, long>());
        }

        // Load all historical memberships to establish member episode boundaries
        var allMemberships = await db.V1FundMemberships.AsNoTracking()
            .Where(m => m.FundId == fund.Id)
            .OrderBy(m => m.Seat)
            .ToListAsync(ct);

        // Event internal model
        // Types: "Contribution", "Withdrawal", "Expense"
        var events = new List<(string Type, Guid UserId, Guid MembershipId, long AmountVnd, DateTime Timestamp, Guid EventId)>();

        // 1. Authoritative Contributions
        var contributions = await db.V1FundContributions.AsNoTracking()
            .Where(c => c.FundId == fund.Id)
            .ToListAsync(ct);

        foreach (var c in contributions)
        {
            events.Add(("Contribution", c.UserId, c.MembershipId, c.AmountVnd, c.CreatedAt, c.Id));
        }

        // 2. Authoritative Outflows and Ledger Reconciliation
        // In a Free Fund, every completed request must be backed by an authoritative ledger debit on the treasury.
        var completedRequests = await db.V1FundRequests.AsNoTracking()
            .Where(r => r.FundId == fund.Id && r.State == "Completed")
            .ToListAsync(ct);

        // Fetch all completed ledger transactions that credit the fund treasury (outflows)
        var fundLedgerOutflows = await db.LedgerTransactions.AsNoTracking()
            .Include(lt => lt.Entries)
            .Where(lt => lt.Status == LedgerTransactionStatus.Completed &&
                         (lt.BusinessType == "V1FundExpense" || lt.BusinessType == "V1FundWithdrawal") &&
                         lt.Entries.Any(e => e.LedgerAccountId == treasury.LedgerAccountId && e.Direction == LedgerEntryDirection.Credit && e.Amount > 0))
            .ToListAsync(ct);

        var outflowLedgerIds = fundLedgerOutflows.Select(lt => lt.Id).ToList();
        var linkedFinancialTxs = await db.FinancialTransactions.AsNoTracking()
            .Where(ft => outflowLedgerIds.Contains(ft.LedgerTransactionId))
            .ToListAsync(ct);

        var ledgerToFinancialTx = linkedFinancialTxs
            .ToDictionary(ft => ft.LedgerTransactionId, ft => ft);

        // Strict 1-to-1 matching: map each completed request to its unique ledger debit transaction
        var accountedLedgerTxIds = new HashSet<Guid>();

        foreach (var r in completedRequests)
        {
            // Find the matching ledger transaction by exact reference/id link
            var matchedTx = fundLedgerOutflows.FirstOrDefault(tx =>
                !accountedLedgerTxIds.Contains(tx.Id) &&
                ((ledgerToFinancialTx.TryGetValue(tx.Id, out var finTx) && finTx.RelatedBusinessId == r.Id) ||
                 tx.BusinessId == r.Id ||
                 (tx.IdempotencyScope != null && tx.IdempotencyScope == $"v1-fund-payout:{fund.Id:N}:{r.Id:N}") ||
                 tx.IdempotencyKey == $"review-{r.Id:N}"));

            if (matchedTx == null)
            {
                throw new ConflictException("fund_request_ledger_mismatch",
                    $"Completed fund request {r.Id} ({r.Kind}, {r.AmountVnd:N0} VND) has no verified ledger debit transaction in treasury. Audit halted.");
            }

            var creditEntry = matchedTx.Entries.FirstOrDefault(e => e.LedgerAccountId == treasury.LedgerAccountId && e.Direction == LedgerEntryDirection.Credit);
            if (creditEntry == null || (long)creditEntry.Amount != r.AmountVnd)
            {
                throw new ConflictException("fund_request_ledger_mismatch",
                    $"Ledger debit amount ({creditEntry?.Amount ?? 0} VND) does not match request amount ({r.AmountVnd} VND) for request {r.Id}.");
            }

            var expectedBusinessType = r.Kind == "Withdrawal" ? "V1FundWithdrawal" : "V1FundExpense";
            if (matchedTx.BusinessType != expectedBusinessType)
            {
                throw new ConflictException("fund_request_ledger_mismatch",
                    $"Ledger business type {matchedTx.BusinessType} does not match request kind {r.Kind} for request {r.Id}.");
            }

            accountedLedgerTxIds.Add(matchedTx.Id);

            // Add verified event with the true ledger execution timestamp
            if (r.Kind == "Withdrawal")
            {
                events.Add(("Withdrawal", r.RecipientUserId, r.RequestedByMembershipId ?? Guid.Empty, r.AmountVnd, matchedTx.CreatedAt, r.Id));
            }
            else if (r.Kind == "Expense")
            {
                events.Add(("Expense", r.RequestedByUserId, r.RequestedByMembershipId ?? Guid.Empty, r.AmountVnd, matchedTx.CreatedAt, r.Id));
            }
        }

        // Process any direct ledger transactions that are NOT backed by a request (e.g. direct owner expenses or unmanaged debits)
        foreach (var tx in fundLedgerOutflows)
        {
            if (accountedLedgerTxIds.Contains(tx.Id))
                continue;

            var creditEntry = tx.Entries.FirstOrDefault(e => e.LedgerAccountId == treasury.LedgerAccountId && e.Direction == LedgerEntryDirection.Credit);
            if (creditEntry == null || creditEntry.Amount <= 0)
                continue;

            var amount = (long)creditEntry.Amount;
            if (tx.BusinessType == "V1FundExpense")
            {
                events.Add(("Expense", fund.OwnerUserId, Guid.Empty, amount, tx.CreatedAt, tx.Id));
            }
            else if (tx.BusinessType == "V1FundWithdrawal")
            {
                Guid userId = ledgerToFinancialTx.TryGetValue(tx.Id, out var fTx) ? fTx.OwnerUserId : fund.OwnerUserId;
                events.Add(("Withdrawal", userId, Guid.Empty, amount, tx.CreatedAt, tx.Id));
            }
        }

        // Sort events chronologically. For identical timestamps, contributions process before withdrawals/expenses.
        var orderedEvents = events
            .OrderBy(e => e.Timestamp)
            .ThenBy(e => e.Type == "Contribution" ? 0 : (e.Type == "Withdrawal" ? 1 : 2))
            .ThenBy(e => e.EventId)
            .ToList();

        // Tracking equity per membershipId across time
        var currentEquities = new Dictionary<Guid, long>();
        var totalContributed = new Dictionary<Guid, long>();
        var totalWithdrawn = new Dictionary<Guid, long>();
        var totalExpenseCharged = new Dictionary<Guid, long>();

        foreach (var m in allMemberships)
        {
            currentEquities[m.Id] = 0;
            totalContributed[m.Id] = 0;
            totalWithdrawn[m.Id] = 0;
            totalExpenseCharged[m.Id] = 0;
        }

        foreach (var evt in orderedEvents)
        {
            if (evt.Type == "Contribution")
            {
                var targetMem = allMemberships.FirstOrDefault(m => m.Id == evt.MembershipId)
                    ?? allMemberships.FirstOrDefault(m => m.UserId == evt.UserId && m.JoinedAt <= evt.Timestamp && (m.LeftAt == null || m.LeftAt >= evt.Timestamp));

                if (targetMem == null)
                    throw new InvalidOperationException($"Cannot identify active membership episode for contribution {evt.EventId}.");

                currentEquities[targetMem.Id] += evt.AmountVnd;
                totalContributed[targetMem.Id] += evt.AmountVnd;
            }
            else if (evt.Type == "Withdrawal")
            {
                var targetMem = allMemberships.FirstOrDefault(m => m.Id == evt.MembershipId)
                    ?? allMemberships.FirstOrDefault(m => m.UserId == evt.UserId && m.JoinedAt <= evt.Timestamp && (m.LeftAt == null || m.LeftAt >= evt.Timestamp));

                if (targetMem == null)
                    throw new InvalidOperationException($"Cannot identify active membership episode for withdrawal {evt.EventId}.");

                var available = currentEquities[targetMem.Id];
                if (evt.AmountVnd > available)
                    throw new InvalidOperationException(
                        $"Audit error: Withdrawal {evt.AmountVnd} VND exceeds member equity {available} VND at {evt.Timestamp:O}.");

                currentEquities[targetMem.Id] -= evt.AmountVnd;
                totalWithdrawn[targetMem.Id] += evt.AmountVnd;
            }
            else if (evt.Type == "Expense")
            {
                // General shared expense: distributed proportionally across members who were active at evt.Timestamp
                var activeAtExpense = allMemberships
                    .Where(m => m.JoinedAt <= evt.Timestamp && (m.LeftAt == null || m.LeftAt >= evt.Timestamp))
                    .OrderBy(m => m.Seat)
                    .ToList();

                var memberEpochs = activeAtExpense
                    .Select(m => new FreeFundEquityEngine.MemberEpochState(m.UserId, m.Id, m.Seat, currentEquities[m.Id]))
                    .ToList();

                long positiveSum = memberEpochs.Where(e => e.EquityVnd > 0).Sum(e => e.EquityVnd);
                if (evt.AmountVnd > positiveSum)
                    throw new InvalidOperationException(
                        $"Audit error: Shared expense {evt.AmountVnd} VND exceeds total positive equity {positiveSum} VND at {evt.Timestamp:O}.");

                var updated = FreeFundEquityEngine.ApplySharedExpense(memberEpochs, evt.AmountVnd);
                foreach (var mem in activeAtExpense)
                {
                    var before = currentEquities[mem.Id];
                    var after = updated[mem.UserId];
                    var charged = before - after;
                    currentEquities[mem.Id] = after;
                    totalExpenseCharged[mem.Id] += charged;
                }
            }
        }

        // Active member records
        var activeResults = new List<FreeFundMemberEquity>();
        foreach (var m in activeMembers)
        {
            var eq = currentEquities.GetValueOrDefault(m.Id, 0L);
            activeResults.Add(new FreeFundMemberEquity(
                m.UserId,
                m.Id,
                m.Seat,
                eq,
                totalContributed.GetValueOrDefault(m.Id, 0L),
                totalWithdrawn.GetValueOrDefault(m.Id, 0L),
                totalExpenseCharged.GetValueOrDefault(m.Id, 0L)));
        }

        long totalActiveEquity = activeResults.Sum(r => r.EquityVnd);

        if (currentBalance > 0 && totalActiveEquity == 0)
        {
            throw new ConflictException("zero_entitlement_dissolution_blocked",
                "Cannot refund or dissolve fund: no active members have valid contribution entitlement to receive the balance.");
        }

        if (totalActiveEquity != currentBalance)
        {
            throw new ConflictException("free_fund_reconciliation_mismatch",
                $"Free Fund equity reconciliation mismatch: Total active member equity ({totalActiveEquity} VND) does not match treasury balance ({currentBalance} VND). Operation halted for audit.");
        }

        // Compute refunds using FreeFundEquityEngine
        var memberStates = activeResults.Select(r => new FreeFundEquityEngine.MemberEpochState(r.UserId, r.MembershipId, r.Seat, r.EquityVnd)).ToList();
        var refundMap = FreeFundEquityEngine.CalculateRefunds(memberStates, currentBalance, singleMemberRefundTargetUserIds);

        return new FreeFundEquityCalculationResult(
            activeResults,
            totalActiveEquity,
            currentBalance,
            refundMap);
    }
}
