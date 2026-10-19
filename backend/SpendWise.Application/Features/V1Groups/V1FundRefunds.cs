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

        var (amount, caseType) = await CalculateMemberRefundAmountAsync(fund, treasury, targetUserId, ct);
        var account = await db.LedgerAccounts.AsNoTracking().SingleAsync(a => a.Id == treasury.LedgerAccountId, ct);

        return new V1RefundPreviewDto(fundId, targetUserId, account.Balance, amount, caseType, "Eligible");
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

            var membership = await db.V1FundMemberships.SingleOrDefaultAsync(
                m => m.FundId == fundId && m.UserId == actorUserId && m.LeftAt == null, token)
                ?? throw new ConflictException("already_left", "Member is not active in this fund.");

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

            var (refundAmount, caseType) = await CalculateMemberRefundAmountAsync(fund, treasury, actorUserId, token);

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

            // Membership is closed ONLY AFTER the refund is posted successfully
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

            var membership = await db.V1FundMemberships.SingleOrDefaultAsync(
                m => m.FundId == fundId && m.UserId == targetUserId && m.LeftAt == null, token)
                ?? throw new NotFoundException("member_not_found", "Target member not found in fund.");

            var treasury = await TreasuryAsync(fund, token);
            await RequireReconciledAccountingAsync(treasury, token);

            // Owner CANNOT modify or override refund amount: calculated authoritatively
            var (refundAmount, caseType) = await CalculateMemberRefundAmountAsync(fund, treasury, targetUserId, token);

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

            // Membership is closed ONLY AFTER the refund is posted successfully
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
                var contributions = await db.V1FundContributions.AsNoTracking()
                    .Where(c => c.FundId == fundId).ToListAsync(token);

                if (fund.Kind == V1FundKind.Free)
                {
                    var hasWithdrawals = await db.V1FundRequests.AsNoTracking()
                        .AnyAsync(r => r.FundId == fundId && r.Kind == "Withdrawal" && r.State == "Completed", token);
                    if (hasWithdrawals)
                        throw new ConflictException("REFUND_BLOCKED_COMPLEX_CONTRACT",
                            "Refund blocked by unresolved complex contract: multiple previous personal withdrawals require formal net benefit formula.");
                }

                // Compute shares based on member contribution weights
                var weights = activeMembers.ToDictionary(
                    m => m.UserId,
                    m => contributions.Where(c => c.UserId == m.UserId).Sum(c => c.AmountVnd));
                var sumWeights = weights.Values.Sum();

                var payouts = new Dictionary<Guid, long>();
                if (sumWeights == 0)
                {
                    var equalShare = (long)Math.Floor(currentBalance / activeMembers.Count);
                    foreach (var m in activeMembers) payouts[m.UserId] = equalShare;
                }
                else
                {
                    foreach (var m in activeMembers)
                    {
                        payouts[m.UserId] = (long)Math.Floor(currentBalance * weights[m.UserId] / sumWeights);
                    }
                }

                // Allocate remainder (đồng dư) starting from lowest seat
                var allocatedSum = payouts.Values.Sum();
                var remainder = (long)currentBalance - allocatedSum;
                for (var i = 0; i < remainder && i < activeMembers.Count; i++)
                {
                    payouts[activeMembers[i].UserId] += 1;
                }

                var caseType = fund.Kind == V1FundKind.Fixed ? "CaseB_DissolveFixed" : "CaseC_DissolveFree";

                foreach (var m in activeMembers)
                {
                    var amount = payouts[m.UserId];
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

                    // Update membership record
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
                // Zero balance: just mark active members as left
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

    private async Task<(long Amount, string CaseType)> CalculateMemberRefundAmountAsync(
        V1Fund fund,
        V1FundTreasury treasury,
        Guid memberUserId,
        CancellationToken ct)
    {
        var account = await db.LedgerAccounts.AsNoTracking().SingleAsync(a => a.Id == treasury.LedgerAccountId, ct);
        var currentBalance = account.Balance;
        if (currentBalance <= 0)
            return (0, "ZeroBalance");

        var contributions = await db.V1FundContributions.AsNoTracking()
            .Where(c => c.FundId == fund.Id).ToListAsync(ct);
        var totalContributed = contributions.Sum(c => c.AmountVnd);
        var userContributed = contributions.Where(c => c.UserId == memberUserId).Sum(c => c.AmountVnd);
        if (userContributed <= 0)
            return (0, "NoContribution");

        var previousRefunds = await db.FinancialTransactions.AsNoTracking()
            .Where(ft => ft.RelatedBusinessId == fund.Id && ft.Type == FinancialTransactionType.Refund && ft.Status == FinancialTransactionStatus.Completed)
            .ToListAsync(ct);
        var totalPriorRefunds = previousRefunds.Sum(r => (long)r.Amount);
        var userPriorRefunds = previousRefunds.Where(r => r.OwnerUserId == memberUserId).Sum(r => (long)r.Amount);

        var totalSpent = totalContributed - totalPriorRefunds - (long)currentBalance;
        if (totalSpent < 0) totalSpent = 0;

        // Case A: Chưa phát sinh chi
        if (totalSpent == 0)
        {
            var unrefunded = userContributed - userPriorRefunds;
            var refund = Math.Max(0, Math.Min(unrefunded, (long)currentBalance));
            return (refund, "CaseA_Unspent");
        }

        // Case B: Fixed fund đã chi, quyền lợi rõ ràng
        if (fund.Kind == V1FundKind.Fixed)
        {
            var activeMembers = await ActiveMembersAsync(fund.Id, ct);
            var activeUserIds = activeMembers.Select(m => m.UserId).ToHashSet();
            var activeContributions = contributions.Where(c => activeUserIds.Contains(c.UserId)).ToList();
            var sumActiveWeights = activeContributions.Sum(c => c.AmountVnd);
            if (sumActiveWeights <= 0) return (0, "ZeroWeight");

            var memberWeight = activeContributions.Where(c => c.UserId == memberUserId).Sum(c => c.AmountVnd);
            var memberShare = (long)Math.Floor((decimal)currentBalance * memberWeight / sumActiveWeights);
            var refund = Math.Max(0, Math.Min(memberShare, (long)currentBalance));
            return (refund, "CaseB_FixedSpentClearShare");
        }

        // Case C: Free fund, chưa có rút cá nhân
        if (fund.Kind == V1FundKind.Free)
        {
            var hasWithdrawals = await db.V1FundRequests.AsNoTracking()
                .AnyAsync(r => r.FundId == fund.Id && r.Kind == "Withdrawal" && r.State == "Completed", ct);
            if (hasWithdrawals)
                throw new ConflictException("REFUND_BLOCKED_COMPLEX_CONTRACT",
                    "Refund blocked by unresolved complex contract: multiple personal withdrawals require formal net benefit formula.");

            var activeMembers = await ActiveMembersAsync(fund.Id, ct);
            var activeUserIds = activeMembers.Select(m => m.UserId).ToHashSet();
            var activeContributions = contributions.Where(c => activeUserIds.Contains(c.UserId)).ToList();
            var sumActiveWeights = activeContributions.Sum(c => c.AmountVnd);
            if (sumActiveWeights <= 0) return (0, "ZeroWeight");

            var memberWeight = activeContributions.Where(c => c.UserId == memberUserId).Sum(c => c.AmountVnd);
            var memberShare = (long)Math.Floor((decimal)currentBalance * memberWeight / sumActiveWeights);
            var refund = Math.Max(0, Math.Min(memberShare, (long)currentBalance));
            return (refund, "CaseC_FreeSharedExpenseClearShare");
        }

        return (0, "UnrecognizedFundKind");
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
