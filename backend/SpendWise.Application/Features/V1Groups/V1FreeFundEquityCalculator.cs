using Microsoft.EntityFrameworkCore;
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
    /// Replays chronological epochs (contributions, personal withdrawals, settled shared expenses)
    /// to determine every active member's current real equity in VND according to User.md 1.3.1.
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
        var activeUserIds = activeMembers.Select(m => m.UserId).ToHashSet();
        var activeMembershipIds = activeMembers.Select(m => m.Id).ToHashSet();

        // If no active members or 0 balance
        if (activeMembers.Count == 0)
        {
            return new FreeFundEquityCalculationResult(
                [],
                0,
                currentBalance,
                new Dictionary<Guid, long>());
        }

        // Gather all completed financial events in chronological order
        // 1. Contributions
        var contributions = await db.V1FundContributions.AsNoTracking()
            .Where(c => c.FundId == fund.Id)
            .Select(c => new
            {
                Type = "Contribution",
                c.UserId,
                c.MembershipId,
                c.AmountVnd,
                c.CreatedAt,
                c.Id
            })
            .ToListAsync(ct);

        // 2. Personal withdrawals (Completed)
        var withdrawals = await db.V1FundRequests.AsNoTracking()
            .Where(r => r.FundId == fund.Id && r.Kind == "Withdrawal" && r.State == "Completed")
            .Select(w => new
            {
                Type = "Withdrawal",
                UserId = w.RecipientUserId,
                MembershipId = w.RequestedByMembershipId ?? Guid.Empty,
                AmountVnd = w.AmountVnd,
                CreatedAt = w.ReviewedAt ?? w.CreatedAt,
                w.Id
            })
            .ToListAsync(ct);

        // 3. Shared expenses (Completed)
        var expenses = await db.V1FundRequests.AsNoTracking()
            .Where(r => r.FundId == fund.Id && r.Kind == "Expense" && r.State == "Completed")
            .Select(e => new
            {
                Type = "Expense",
                UserId = e.RequestedByUserId,
                MembershipId = e.RequestedByMembershipId ?? Guid.Empty,
                AmountVnd = e.AmountVnd,
                CreatedAt = e.ReviewedAt ?? e.CreatedAt,
                e.Id
            })
            .ToListAsync(ct);

        // We also check any direct ledger transactions with BusinessType == "V1FundExpense" not tied to V1FundRequest
        var expenseRequestIds = expenses.Select(e => e.Id).ToHashSet();
        var directExpensePostings = await db.LedgerTransactions.AsNoTracking()
            .Where(lt => lt.BusinessId == fund.Id && lt.BusinessType == "V1FundExpense" && lt.Status == LedgerTransactionStatus.Completed)
            .ToListAsync(ct);

        foreach (var dep in directExpensePostings)
        {
            if (!expenseRequestIds.Contains(dep.Id))
            {
                var entry = await db.LedgerEntries.AsNoTracking()
                    .FirstOrDefaultAsync(e => e.LedgerTransactionId == dep.Id && e.LedgerAccountId == treasury.LedgerAccountId && e.Direction == LedgerEntryDirection.Credit, ct);
                if (entry != null && entry.Amount > 0)
                {
                    expenses.Add(new
                    {
                        Type = "Expense",
                        UserId = fund.OwnerUserId,
                        MembershipId = Guid.Empty,
                        AmountVnd = (long)entry.Amount,
                        dep.CreatedAt,
                        dep.Id
                    });
                }
            }
        }

        // Combine all events and sort chronologically
        var timeline = contributions.Select(c => (c.Type, c.UserId, c.MembershipId, c.AmountVnd, c.CreatedAt, c.Id))
            .Concat(withdrawals.Select(w => (w.Type, w.UserId, w.MembershipId, w.AmountVnd, w.CreatedAt, w.Id)))
            .Concat(expenses.Select(e => (e.Type, e.UserId, e.MembershipId, e.AmountVnd, e.CreatedAt, e.Id)))
            .OrderBy(evt => evt.CreatedAt)
            .ThenBy(evt => evt.Id)
            .ToList();

        // Load all historical memberships to know when members joined / left
        var allMemberships = await db.V1FundMemberships.AsNoTracking()
            .Where(m => m.FundId == fund.Id)
            .OrderBy(m => m.Seat)
            .ToListAsync(ct);

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

        foreach (var evt in timeline)
        {
            if (evt.Type == "Contribution")
            {
                // Find target membership: either explicit membershipId or active at evt.CreatedAt
                var targetMem = allMemberships.FirstOrDefault(m => m.Id == evt.MembershipId)
                    ?? allMemberships.FirstOrDefault(m => m.UserId == evt.UserId && m.JoinedAt <= evt.CreatedAt && (m.LeftAt == null || m.LeftAt >= evt.CreatedAt));

                if (targetMem != null)
                {
                    currentEquities[targetMem.Id] += evt.AmountVnd;
                    totalContributed[targetMem.Id] += evt.AmountVnd;
                }
            }
            else if (evt.Type == "Withdrawal")
            {
                var targetMem = allMemberships.FirstOrDefault(m => m.Id == evt.MembershipId)
                    ?? allMemberships.FirstOrDefault(m => m.UserId == evt.UserId && m.JoinedAt <= evt.CreatedAt && (m.LeftAt == null || m.LeftAt >= evt.CreatedAt));

                if (targetMem != null)
                {
                    currentEquities[targetMem.Id] = Math.Max(0, currentEquities[targetMem.Id] - evt.AmountVnd);
                    totalWithdrawn[targetMem.Id] += evt.AmountVnd;
                }
            }
            else if (evt.Type == "Expense")
            {
                // General shared expense: distributed proportionally across members who were active at evt.CreatedAt
                var activeAtExpense = allMemberships
                    .Where(m => m.JoinedAt <= evt.CreatedAt && (m.LeftAt == null || m.LeftAt >= evt.CreatedAt))
                    .OrderBy(m => m.Seat)
                    .ToList();

                var memberEpochs = activeAtExpense
                    .Select(m => new FreeFundEquityEngine.MemberEpochState(m.UserId, m.Id, m.Seat, currentEquities[m.Id]))
                    .ToList();

                long positiveSum = memberEpochs.Where(e => e.EquityVnd > 0).Sum(e => e.EquityVnd);
                if (positiveSum > 0)
                {
                    var updated = FreeFundEquityEngine.ApplySharedExpense(memberEpochs, Math.Min(evt.AmountVnd, positiveSum));
                    foreach (var mem in activeAtExpense)
                    {
                        var before = currentEquities[mem.Id];
                        var after = updated.GetValueOrDefault(mem.UserId, before);
                        var charged = before - after;
                        currentEquities[mem.Id] = after;
                        totalExpenseCharged[mem.Id] += charged;
                    }
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
