using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Domain.Finance;
using SpendWise.Domain.V1Groups;

namespace SpendWise.Application.Features.V1Groups;

public sealed record V1LeaveFundRequest(string SecurityPin, string IdempotencyKey, Guid? DestinationWalletId = null);
public sealed record V1KickMemberRequest(string SecurityPin, string Reason, string IdempotencyKey, Guid? DestinationWalletId = null);
public sealed record V1DissolveFundRequest(string SecurityPin, string IdempotencyKey);

public sealed record V1RefundResultDto(
    Guid FundId,
    Guid UserId,
    long RefundAmountVnd,
    Guid? WalletId,
    Guid? LedgerTransactionId,
    string Status,
    string CaseType,
    DateTime ProcessedAt);

public sealed record V1RefundPreviewDto(
    Guid FundId,
    Guid UserId,
    decimal CurrentFundBalanceVnd,
    long EligibleRefundAmountVnd,
    string CaseType,
    string Status);

public sealed record V1DissolveResultDto(
    Guid FundId,
    long TotalRefundedVnd,
    decimal FinalBalanceVnd,
    IReadOnlyList<V1RefundResultDto> Refunds,
    DateTime DissolvedAt);

public sealed partial class V1FundService
{
    public async Task<V1RefundPreviewDto> PreviewRefundAsync(
        Guid actorUserId,
        Guid fundId,
        Guid? targetMemberUserId,
        CancellationToken ct)
    {
        await RequireActorAsync(actorUserId, ct);
        var fund = await FundAsync(fundId, ct);
        await RequireMemberAsync(actorUserId, fundId, ct);

        var targetUserId = targetMemberUserId ?? actorUserId;
        if (targetUserId != actorUserId && fund.OwnerUserId != actorUserId)
            throw new ForbiddenException("refund_preview_forbidden", "Only the member or fund owner may preview refund.");

        var treasury = await db.V1FundTreasuries.AsNoTracking().SingleOrDefaultAsync(t => t.FundId == fundId, ct)
            ?? throw new NotFoundException("fund_treasury_not_found", "Fund treasury not initialized.");

        await RequireReconciledAccountingAsync(treasury, ct);
        var calc = await CalculateUnifiedRefundsAsync(fund, treasury, [targetUserId], ct);
        var targetRefund = calc.MemberRefunds.GetValueOrDefault(targetUserId, (0, "ZeroBalance"));
        var account = await db.LedgerAccounts.AsNoTracking().SingleAsync(a => a.Id == treasury.LedgerAccountId, ct);

        return new V1RefundPreviewDto(fundId, targetUserId, account.Balance, targetRefund.Amount, targetRefund.CaseType, "Eligible");
    }

    public async Task<V1RefundResultDto> LeaveFundAsync(
        Guid actorUserId,
        Guid fundId,
        V1LeaveFundRequest request,
        CancellationToken ct)
    {
        var key = InputBoundary.RequiredText(request.IdempotencyKey, 100, "Idempotency key");
        await RequireActorAsync(actorUserId, ct);
        await security.VerifyAsync(actorUserId, request.SecurityPin, ct);

        return await db.ExecuteInTransactionAsync(async token =>
        {
            await LockUsersAsync([actorUserId], token);
            var fund = await LockFundAsync(fundId, token);
            if (fund.DissolvedAt is not null)
                throw new ConflictException("fund_already_dissolved", "Fund is already dissolved.");

            // Idempotency check: if replayed with the same key, return previous result
            var existingTx = await db.LedgerTransactions.AsNoTracking()
                .FirstOrDefaultAsync(t => t.IdempotencyKey == key && t.BusinessType == "V1FundRefund" && t.BusinessId == fundId
                    && t.IdempotencyScope == $"v1-fund-refund:{fundId:N}:{actorUserId:N}", token);
            if (existingTx is not null)
            {
                var ft = await db.FinancialTransactions.AsNoTracking()
                    .FirstOrDefaultAsync(f => f.LedgerTransactionId == existingTx.Id, token);
                return new V1RefundResultDto(
                    fund.Id,
                    actorUserId,
                    (long)(ft?.Amount ?? 0),
                    ft?.WalletId,
                    existingTx.Id,
                    "Completed",
                    "Replayed",
                    existingTx.CreatedAt);
            }

            var membership = await db.V1FundMemberships.SingleOrDefaultAsync(
                m => m.FundId == fundId && m.UserId == actorUserId && m.LeftAt == null, token);
            if (membership is null)
            {
                var pastLeft = await db.V1FundActivities.AsNoTracking()
                    .AnyAsync(a => a.FundId == fundId && a.SubjectUserId == actorUserId && a.Event == "MemberLeft", token);
                if (pastLeft)
                    return new V1RefundResultDto(fund.Id, actorUserId, 0, null, null, "Completed", "Replayed", clock.UtcNow);

                throw new ConflictException("already_left", "Member is not active in this fund.");
            }

            var activeMembers = await ActiveMembersAsync(fundId, token);
            if (fund.OwnerUserId == actorUserId)
            {
                if (activeMembers.Count > 1)
                    throw new AppException("owner_transfer_required", "Chủ nhóm phải chuyển quyền trước khi rời nhóm.");
                else
                    throw new AppException("single_member_must_dissolve", "Nhóm chỉ còn một người không thể tự rời; hãy thực hiện giải tán nhóm.");
            }

            var election = await db.V1Elections.AsNoTracking().SingleOrDefaultAsync(e => e.FundId == fundId && e.FinishedAt == null, token);
            if (election is not null)
                throw new ConflictException("election_leave_paused", "Voluntary leave is paused during an active election.");

            var treasury = await TreasuryAsync(fund, token);
            await RequireReconciledAccountingAsync(treasury, token);

            var calc = await CalculateUnifiedRefundsAsync(fund, treasury, [actorUserId], token);
            var (refundAmount, caseType) = calc.MemberRefunds.GetValueOrDefault(actorUserId, (0, "ZeroBalance"));

            Guid? ledgerTxId = null;
            Guid? destinationWalletId = null;

            if (refundAmount > 0)
            {
                var destinationWallet = await ResolveDestinationWalletAsync(actorUserId, request.DestinationWalletId, token);
                destinationWalletId = destinationWallet.Id;
                var destAccount = await db.LedgerAccounts.SingleAsync(a => a.WalletId == destinationWallet.Id, token);
                await FreshAccountAsync(destAccount.Id, token);
                var sourceAccount = await FreshAccountAsync(treasury.LedgerAccountId, token);

                if (sourceAccount.Balance < refundAmount)
                    throw new ConflictException("insufficient_fund_balance", "Fund treasury balance is insufficient to complete the refund.");

                var posting = await ledger.PostAsync(
                    sourceAccount.Id,
                    destAccount.Id,
                    refundAmount,
                    "VND",
                    "V1FundRefund",
                    fund.Id,
                    $"v1-fund-refund:{fund.Id:N}:{actorUserId:N}",
                    key,
                    $"Hoàn tiền rời quỹ SpendWise: {fund.Name}",
                    token);

                ledgerTxId = posting.LedgerTransactionId;

                db.FinancialTransactions.Add(new FinancialTransaction
                {
                    OwnerUserId = actorUserId,
                    WalletId = destinationWallet.Id,
                    LedgerTransactionId = posting.LedgerTransactionId,
                    RelatedBusinessId = fund.Id,
                    Type = FinancialTransactionType.Refund,
                    Status = FinancialTransactionStatus.Completed,
                    Amount = refundAmount,
                    Currency = "VND",
                    Description = $"Hoàn tiền rời quỹ {fund.Name}",
                    OccurredAt = clock.UtcNow
                });
            }

            membership.RefundedVnd = checked((membership.RefundedVnd ?? 0) + refundAmount);
            membership.LeftAt = clock.UtcNow;

            await ActivityAsync(fund.Id, actorUserId, "MemberLeft", token,
                subject: actorUserId,
                detail: $"Thành viên tự rời nhóm. Hoàn tiền: {refundAmount:N0} đ. Cơ chế: {caseType}.",
                referenceId: ledgerTxId);

            await db.SaveChangesAsync(token);

            return new V1RefundResultDto(
                fund.Id,
                actorUserId,
                refundAmount,
                destinationWalletId,
                ledgerTxId,
                "Completed",
                caseType,
                clock.UtcNow);
        }, ct);
    }

    public async Task<V1RefundResultDto> KickMemberAsync(
        Guid actorUserId,
        Guid fundId,
        Guid targetUserId,
        V1KickMemberRequest request,
        CancellationToken ct)
    {
        var key = InputBoundary.RequiredText(request.IdempotencyKey, 100, "Idempotency key");
        var reason = InputBoundary.RequiredText(request.Reason, 500, "Reason");
        await RequireActorAsync(actorUserId, ct);
        await security.VerifyAsync(actorUserId, request.SecurityPin, ct);

        return await db.ExecuteInTransactionAsync(async token =>
        {
            await LockUsersAsync([actorUserId, targetUserId], token);
            var fund = await LockFundAsync(fundId, token);
            RequireOwner(fund, actorUserId);

            if (fund.DissolvedAt is not null)
                throw new ConflictException("fund_already_dissolved", "Fund is already dissolved.");

            if (targetUserId == actorUserId)
                throw new AppException("cannot_kick_owner", "Chủ nhóm không thể kick chính mình.");

            // Idempotency check: if replayed with same key, return previous result
            var existingTx = await db.LedgerTransactions.AsNoTracking()
                .FirstOrDefaultAsync(t => t.IdempotencyKey == key && t.BusinessType == "V1FundRefund" && t.BusinessId == fundId
                    && t.IdempotencyScope == $"v1-fund-refund:{fundId:N}:{targetUserId:N}", token);
            if (existingTx is not null)
            {
                var ft = await db.FinancialTransactions.AsNoTracking()
                    .FirstOrDefaultAsync(f => f.LedgerTransactionId == existingTx.Id, token);
                return new V1RefundResultDto(
                    fund.Id,
                    targetUserId,
                    (long)(ft?.Amount ?? 0),
                    ft?.WalletId,
                    existingTx.Id,
                    "Completed",
                    "Replayed",
                    existingTx.CreatedAt);
            }

            var membership = await db.V1FundMemberships.SingleOrDefaultAsync(
                m => m.FundId == fundId && m.UserId == targetUserId && m.LeftAt == null, token);
            if (membership is null)
            {
                var pastKicked = await db.V1FundActivities.AsNoTracking()
                    .AnyAsync(a => a.FundId == fundId && a.SubjectUserId == targetUserId && a.Event == "MemberKicked", token);
                if (pastKicked)
                    return new V1RefundResultDto(fund.Id, targetUserId, 0, null, null, "Completed", "Replayed", clock.UtcNow);

                throw new NotFoundException("member_not_found", "Target member not found in fund.");
            }

            var treasury = await TreasuryAsync(fund, token);
            await RequireReconciledAccountingAsync(treasury, token);

            var calc = await CalculateUnifiedRefundsAsync(fund, treasury, [targetUserId], token);
            var (refundAmount, caseType) = calc.MemberRefunds.GetValueOrDefault(targetUserId, (0, "ZeroBalance"));

            Guid? ledgerTxId = null;
            Guid? destinationWalletId = null;

            if (refundAmount > 0)
            {
                var destinationWallet = await ResolveDestinationWalletAsync(targetUserId, request.DestinationWalletId, token);
                destinationWalletId = destinationWallet.Id;
                var destAccount = await db.LedgerAccounts.SingleAsync(a => a.WalletId == destinationWallet.Id, token);
                await FreshAccountAsync(destAccount.Id, token);
                var sourceAccount = await FreshAccountAsync(treasury.LedgerAccountId, token);

                if (sourceAccount.Balance < refundAmount)
                    throw new ConflictException("insufficient_fund_balance", "Fund treasury balance is insufficient to complete the refund.");

                var posting = await ledger.PostAsync(
                    sourceAccount.Id,
                    destAccount.Id,
                    refundAmount,
                    "VND",
                    "V1FundRefund",
                    fund.Id,
                    $"v1-fund-refund:{fund.Id:N}:{targetUserId:N}",
                    key,
                    $"Hoàn tiền kick khỏi quỹ SpendWise: {fund.Name}",
                    token);

                ledgerTxId = posting.LedgerTransactionId;

                db.FinancialTransactions.Add(new FinancialTransaction
                {
                    OwnerUserId = targetUserId,
                    WalletId = destinationWallet.Id,
                    LedgerTransactionId = posting.LedgerTransactionId,
                    RelatedBusinessId = fund.Id,
                    Type = FinancialTransactionType.Refund,
                    Status = FinancialTransactionStatus.Completed,
                    Amount = refundAmount,
                    Currency = "VND",
                    Description = $"Hoàn tiền kick khỏi quỹ {fund.Name}",
                    OccurredAt = clock.UtcNow
                });
            }

            membership.RefundedVnd = checked((membership.RefundedVnd ?? 0) + refundAmount);
            membership.LeftAt = clock.UtcNow;

            await ActivityAsync(fund.Id, actorUserId, "MemberKicked", token,
                subject: targetUserId,
                detail: $"Thành viên bị kick bởi chủ nhóm. Lý do: {reason}. Hoàn tiền: {refundAmount:N0} đ. Cơ chế: {caseType}.",
                referenceId: ledgerTxId);

            await db.SaveChangesAsync(token);

            return new V1RefundResultDto(
                fund.Id,
                targetUserId,
                refundAmount,
                destinationWalletId,
                ledgerTxId,
                "Completed",
                caseType,
                clock.UtcNow);
        }, ct);
    }

    public async Task<V1DissolveResultDto> DissolveFundAsync(
        Guid actorUserId,
        Guid fundId,
        V1DissolveFundRequest request,
        CancellationToken ct)
    {
        var key = InputBoundary.RequiredText(request.IdempotencyKey, 100, "Idempotency key");
        await RequireActorAsync(actorUserId, ct);
        await security.VerifyAsync(actorUserId, request.SecurityPin, ct);

        return await db.ExecuteInTransactionAsync(async token =>
        {
            await LockUsersAsync([actorUserId], token);
            var fund = await LockFundAsync(fundId, token);
            RequireOwner(fund, actorUserId);

            if (fund.DissolvedAt is not null)
                throw new ConflictException("fund_already_dissolved", "Fund is already dissolved.");

            var activeMembers = await ActiveMembersAsync(fundId, token);
            var treasury = await TreasuryAsync(fund, token);
            await RequireReconciledAccountingAsync(treasury, token);

            var treasuryAccount = await FreshAccountAsync(treasury.LedgerAccountId, token);
            var currentBalance = treasuryAccount.Balance;

            var refunds = new List<V1RefundResultDto>();
            long totalRefunded = 0;

            if (currentBalance > 0 && activeMembers.Count > 0)
            {
                var calc = await CalculateUnifiedRefundsAsync(fund, treasury, null, token);
                var caseType = fund.Kind == V1FundKind.Fixed ? "CaseB_DissolveFixed" : "CaseC_DissolveFree";

                foreach (var m in activeMembers)
                {
                    var amount = calc.MemberRefunds.GetValueOrDefault(m.UserId, (0, caseType)).Amount;
                    Guid? ledgerTxId = null;
                    Guid? walletId = null;

                    if (amount > 0)
                    {
                        var destWallet = await ResolveDestinationWalletAsync(m.UserId, null, token);
                        walletId = destWallet.Id;
                        var destAcc = await db.LedgerAccounts.SingleAsync(a => a.WalletId == destWallet.Id, token);
                        await FreshAccountAsync(destAcc.Id, token);
                        var srcAcc = await FreshAccountAsync(treasury.LedgerAccountId, token);

                        var posting = await ledger.PostAsync(
                            srcAcc.Id,
                            destAcc.Id,
                            amount,
                            "VND",
                            "V1FundRefund",
                            fund.Id,
                            $"v1-fund-refund:{fund.Id:N}:{m.UserId:N}",
                            $"{key}:{m.UserId:N}",
                            $"Hoàn tiền giải tán quỹ: {fund.Name}",
                            token);

                        ledgerTxId = posting.LedgerTransactionId;

                        db.FinancialTransactions.Add(new FinancialTransaction
                        {
                            OwnerUserId = m.UserId,
                            WalletId = destWallet.Id,
                            LedgerTransactionId = posting.LedgerTransactionId,
                            RelatedBusinessId = fund.Id,
                            Type = FinancialTransactionType.Refund,
                            Status = FinancialTransactionStatus.Completed,
                            Amount = amount,
                            Currency = "VND",
                            Description = $"Hoàn tiền giải tán quỹ {fund.Name}",
                            OccurredAt = clock.UtcNow
                        });

                        totalRefunded += amount;
                    }

                    var mem = await db.V1FundMemberships.SingleAsync(x => x.Id == m.Id, token);
                    mem.LeftAt = clock.UtcNow;

                    refunds.Add(new V1RefundResultDto(
                        fund.Id,
                        m.UserId,
                        amount,
                        walletId,
                        ledgerTxId,
                        "Completed",
                        caseType,
                        clock.UtcNow));
                }

                // Verify treasury account balance is now EXACTLY 0 VND!
                var freshTreasuryAccount = await FreshAccountAsync(treasury.LedgerAccountId, token);
                if (freshTreasuryAccount.Balance != 0)
                    throw new ConflictException("dissolve_invariant_failed",
                        $"Fund balance after dissolution is {freshTreasuryAccount.Balance}, expected 0 VND.");
            }
            else
            {
                foreach (var m in activeMembers)
                {
                    var mem = await db.V1FundMemberships.SingleAsync(x => x.Id == m.Id, token);
                    mem.LeftAt = clock.UtcNow;
                }
            }

            fund.DissolvedAt = clock.UtcNow;
            await db.V1Funds.Where(f => f.Id == fund.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(f => f.DissolvedAt, clock.UtcNow), token);

            await ActivityAsync(fund.Id, actorUserId, "FundDissolved", token,
                detail: $"Quỹ đã được giải tán. Tổng hoàn tiền: {totalRefunded:N0} đ. Số dư quỹ về 0 đ.");

            await db.SaveChangesAsync(token);

            return new V1DissolveResultDto(
                fund.Id,
                totalRefunded,
                0,
                refunds,
                clock.UtcNow);
        }, ct);
    }

    private sealed record UnifiedCalculationResult(
        Dictionary<Guid, (long Amount, string CaseType)> MemberRefunds,
        long TotalAllocatedVnd,
        decimal TreasuryBalanceVnd);

    /// <summary>
    /// Unified refund calculator ensuring identical calculation across Preview, Leave, Kick, and Dissolve.
    /// If singleMemberUserIds is provided, calculates specifically for those members leaving individually.
    /// If singleMemberUserIds is null, calculates full dissolution distributing 100% of current balance across all active members.
    /// </summary>
    private async Task<UnifiedCalculationResult> CalculateUnifiedRefundsAsync(
        V1Fund fund,
        V1FundTreasury treasury,
        IReadOnlyList<Guid>? singleMemberUserIds,
        CancellationToken ct)
    {
        if (await db.V1FundRequests.AsNoTracking().AnyAsync(r => r.FundId == fund.Id
            && (r.State == "Pending24h" || r.State == "Suspended"), ct))
            throw new ConflictException("refund_pending_hold", "Cancel or settle held expenses before calculating refunds.");
        var account = await db.LedgerAccounts.AsNoTracking().SingleAsync(a => a.Id == treasury.LedgerAccountId, ct);
        var currentBalance = account.Balance;
        var results = new Dictionary<Guid, (long Amount, string CaseType)>();
        if (currentBalance <= 0)
        {
            if (singleMemberUserIds is not null)
                foreach (var id in singleMemberUserIds) results[id] = (0, "ZeroBalance");
            return new UnifiedCalculationResult(results, 0, currentBalance);
        }

        var contributions = await db.V1FundContributions.AsNoTracking().Where(c => c.FundId == fund.Id).ToListAsync(ct);
        var activeMembers = await ActiveMembersAsync(fund.Id, ct);
        var activeUserIds = activeMembers.Select(m => m.UserId).ToHashSet();

        var previousRefunds = await db.FinancialTransactions.AsNoTracking()
            .Where(ft => ft.RelatedBusinessId == fund.Id && ft.Type == FinancialTransactionType.Refund && ft.Status == FinancialTransactionStatus.Completed)
            .ToListAsync(ct);

        var period = fund.Kind == V1FundKind.Fixed ? await CurrentActivityAsync(fund.Id, ct) : null;
        var activityPaid = period is not null ? await ActivityPaidAsync(fund.Id, ct) : null;
        if (period is not null)
        {
            var settlementPrefix = $"v1-fund-activity-refund:{period.Id:N}:";
            var settlementPostings = await db.LedgerTransactions.AsNoTracking().Where(t => t.IdempotencyScope != null && t.IdempotencyScope.StartsWith(settlementPrefix)).Select(t => t.Id).ToListAsync(ct);
            previousRefunds = previousRefunds.Where(r => r.CreatedAt >= period.SettledAt && !settlementPostings.Contains(r.LedgerTransactionId)).ToList();
        }
        var totalContributed = activityPaid is null ? contributions.Sum(c => c.AmountVnd) : activityPaid.Values.Sum();
        var totalPriorRefunds = previousRefunds.Sum(r => (long)r.Amount);
        var totalSpent = totalContributed - totalPriorRefunds - (long)currentBalance;
        if (totalSpent < 0) totalSpent = 0;

        bool isDissolution = singleMemberUserIds is null;

        if (fund.Kind == V1FundKind.Fixed)
        {
            var obligations = await db.V1FundObligations.AsNoTracking()
                .Where(o => o.FundId == fund.Id && o.Revision == treasury.Revision)
                .OrderBy(o => o.Seat).ToListAsync(ct);

            // Compute paid and required per active member
            var memberStats = new Dictionary<Guid, (long Paid, long Required, long LockedExcess, long PriorRefunds, long ProtectedExcess)>();
            long totalProtectedExcess = 0;

            foreach (var m in activeMembers)
            {
                var paid = activityPaid?.GetValueOrDefault(m.Id) ?? contributions.Where(c => c.MembershipId == m.Id).Sum(c => c.AmountVnd);
                var required = obligations.SingleOrDefault(o => o.MembershipId == m.Id)?.RequiredVnd ?? 0;
                var lockedExcess = Math.Max(0, paid - required);
                var priorRef = m.RefundedVnd ?? previousRefunds.Where(r => r.OwnerUserId == m.UserId && r.CreatedAt >= m.JoinedAt).Sum(r => (long)r.Amount);
                var protectedExcess = Math.Max(0, lockedExcess - priorRef);
                memberStats[m.UserId] = (paid, required, lockedExcess, priorRef, protectedExcess);
                totalProtectedExcess += protectedExcess;
            }

            var availableForSharing = Math.Max(0, (long)currentBalance - totalProtectedExcess);

            // Case A: Chưa phát sinh chi
            if (totalSpent == 0)
            {
                if (isDissolution)
                {
                    // Full refund of all unspent contributions
                    long allocated = 0;
                    foreach (var m in activeMembers)
                    {
                        var stats = memberStats[m.UserId];
                        var unspent = Math.Max(0, stats.Paid - stats.PriorRefunds);
                        var amount = Math.Min(unspent, (long)currentBalance - allocated);
                        results[m.UserId] = (amount, "CaseA_Unspent");
                        allocated += amount;
                    }
                    if (allocated < (long)currentBalance)
                    {
                        throw new ConflictException("zero_entitlement_dissolution_blocked",
                            "Cannot dissolve fund: active members unspent contributions are less than the current fund balance.");
                    }
                    return new UnifiedCalculationResult(results, allocated, currentBalance);
                }
                else
                {
                    foreach (var targetUserId in singleMemberUserIds!)
                    {
                        if (!memberStats.TryGetValue(targetUserId, out var stats))
                        {
                            results[targetUserId] = (0, "NoContribution");
                            continue;
                        }
                        var unspent = Math.Max(0, stats.Paid - stats.PriorRefunds);
                        var refund = Math.Max(0, Math.Min(unspent, (long)currentBalance));
                        results[targetUserId] = (refund, "CaseA_Unspent");
                    }
                    return new UnifiedCalculationResult(results, results.Values.Sum(v => v.Amount), currentBalance);
                }
            }

            // Case B: Fixed fund đã chi
            // Calculate weights on available balance: obligation portion w_i = min(paid, required)
            var weights = activeMembers.ToDictionary(
                m => m.UserId,
                m => memberStats.TryGetValue(m.UserId, out var st) ? Math.Min(st.Paid, st.Required) : 0L);
            var sumWeights = weights.Values.Sum();

            var payouts = activeMembers.ToDictionary(m => m.UserId, m => memberStats[m.UserId].ProtectedExcess);
            var remainingBalance = (long)currentBalance - payouts.Values.Sum();
            if (remainingBalance > 0 && sumWeights > 0)
            {
                foreach (var m in activeMembers)
                    payouts[m.UserId] += (long)Math.Floor((decimal)remainingBalance * weights[m.UserId] / sumWeights);

                // Preview and execution use the same stable-seat allocation of whole đồng.
                var eligibleMembers = activeMembers.Where(m => weights[m.UserId] > 0 || payouts[m.UserId] > 0).ToList();
                var remainder = (long)currentBalance - payouts.Values.Sum();
                for (var i = 0; i < remainder && i < eligibleMembers.Count; i++)
                    payouts[eligibleMembers[i].UserId]++;
            }
            else if (remainingBalance > 0 && sumWeights == 0 && isDissolution)
            {
                throw new ConflictException("zero_entitlement_dissolution_blocked",
                    "Cannot dissolve fund: active members have zero valid obligation entitlement to receive the remaining balance.");
            }

            foreach (var userId in isDissolution ? activeMembers.Select(m => m.UserId) : singleMemberUserIds!)
                results[userId] = payouts.TryGetValue(userId, out var payout)
                    ? (Math.Max(0, Math.Min((long)currentBalance, payout)), isDissolution ? "CaseB_DissolveFixed" : "CaseB_FixedSpentClearShare")
                    : (0, "NoContribution");
            return new UnifiedCalculationResult(results, results.Values.Sum(v => v.Amount), currentBalance);
        }
        else // Free Fund
        {
            // Gather completed personal withdrawals
            var withdrawals = await db.V1FundRequests.AsNoTracking()
                .Where(r => r.FundId == fund.Id && r.Kind == "Withdrawal" && r.State == "Completed")
                .ToListAsync(ct);

            // Compute net equity per member: NetEquity_i = max(0, Contributed - Withdrawn - Refunded)
            var memberNetEquity = new Dictionary<Guid, long>();
            foreach (var m in activeMembers)
            {
                var contrib = contributions.Where(c => c.MembershipId == m.Id).Sum(c => c.AmountVnd);
                var wd = withdrawals.Where(w => w.RecipientUserId == m.UserId && (w.RequestedByMembershipId == m.Id || (w.RequestedByMembershipId == null && w.CreatedAt >= m.JoinedAt))).Sum(w => w.AmountVnd);
                var priorRef = m.RefundedVnd ?? previousRefunds.Where(r => r.OwnerUserId == m.UserId && r.CreatedAt >= m.JoinedAt).Sum(r => (long)r.Amount);
                var net = Math.Max(0, contrib - wd - priorRef);
                memberNetEquity[m.UserId] = net;
            }

            var sumNetEquity = memberNetEquity.Values.Sum();

            if (isDissolution)
            {
                var payouts = new Dictionary<Guid, long>();
                if (sumNetEquity > 0)
                {
                    foreach (var m in activeMembers)
                    {
                        var share = (long)Math.Floor((decimal)currentBalance * memberNetEquity[m.UserId] / sumNetEquity);
                        payouts[m.UserId] = share;
                    }
                }
                else if ((long)currentBalance > 0 && sumNetEquity == 0)
                {
                    throw new ConflictException("zero_entitlement_dissolution_blocked",
                        "Cannot dissolve fund: no active members have valid contribution entitlement to receive the remaining balance.");
                }
                else
                {
                    foreach (var m in activeMembers) payouts[m.UserId] = 0;
                }

                // Distribute remainder (đồng dư)
                var eligibleMembers = activeMembers.Where(m => memberNetEquity.GetValueOrDefault(m.UserId) > 0).ToList();
                var allocatedSum = payouts.Values.Sum();
                var remainder = (long)currentBalance - allocatedSum;
                for (var i = 0; i < remainder && i < eligibleMembers.Count; i++)
                {
                    payouts[eligibleMembers[i].UserId] += 1;
                }
                foreach (var m in activeMembers)
                {
                    results[m.UserId] = (payouts[m.UserId], "CaseC_DissolveFree");
                }
                return new UnifiedCalculationResult(results, (long)currentBalance, currentBalance);
            }
            else
            {
                foreach (var targetUserId in singleMemberUserIds!)
                {
                    if (!memberNetEquity.TryGetValue(targetUserId, out var net) || net <= 0)
                    {
                        results[targetUserId] = (0, "NoContribution");
                        continue;
                    }

                    if (totalSpent == 0)
                    {
                        var refund = Math.Max(0, Math.Min(net, (long)currentBalance));
                        results[targetUserId] = (refund, "CaseA_Unspent");
                    }
                    else
                    {
                        long share = 0;
                        if (sumNetEquity > 0)
                            share = (long)Math.Floor((decimal)currentBalance * net / sumNetEquity);
                        else
                            share = 0;

                        var refund = Math.Max(0, Math.Min((long)currentBalance, share));
                        results[targetUserId] = (refund, "CaseC_FreeSharedExpenseClearShare");
                    }
                }
                return new UnifiedCalculationResult(results, results.Values.Sum(v => v.Amount), currentBalance);
            }
        }
    }

    private async Task<Wallet> ResolveDestinationWalletAsync(Guid userId, Guid? preferredWalletId, CancellationToken ct)
    {
        if (preferredWalletId is { } walletId && walletId != Guid.Empty)
        {
            var preferred = await db.Wallets.SingleOrDefaultAsync(
                w => w.Id == walletId && w.OwnerUserId == userId && w.Status == WalletStatus.Active, ct);
            if (preferred is not null) return preferred;
        }

        var mainWallet = await db.Wallets.SingleOrDefaultAsync(
            w => w.OwnerUserId == userId && w.Type == WalletType.Main && w.Status == WalletStatus.Active, ct);
        if (mainWallet is not null) return mainWallet;

        var anyWallet = await db.Wallets.FirstOrDefaultAsync(
            w => w.OwnerUserId == userId && w.Status == WalletStatus.Active, ct);
        return anyWallet ?? throw new ConflictException("refund_wallet_unavailable", "No eligible active destination wallet is available for user.");
    }
}
