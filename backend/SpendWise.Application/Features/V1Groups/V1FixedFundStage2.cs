using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Domain.Finance;
using SpendWise.Domain.V1Groups;

namespace SpendWise.Application.Features.V1Groups;

public sealed record V1DeadlineProcessingResultDto(Guid FundId, string Status, IReadOnlyList<Guid> AutoKickedUserIds, bool Dissolved);

public sealed partial class V1FundService
{
    public async Task<int> ProgressFixedFundDeadlinesAsync(CancellationToken ct)
    {
        var activeFixedFundIds = await db.V1Funds.AsNoTracking()
            .Where(f => f.Kind == V1FundKind.Fixed && f.DissolvedAt == null)
            .Select(f => f.Id)
            .ToListAsync(ct);

        var processed = 0;
        foreach (var fundId in activeFixedFundIds)
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                if (scopes is not null)
                {
                    using var fundScope = scopes.CreateScope();
                    var scopedService = fundScope.ServiceProvider.GetRequiredService<V1FundService>();
                    var res = await scopedService.ProcessFixedFundDeadlinesAsync(fundId, ct);
                    if (res.Status != "NoAction")
                    {
                        processed++;
                    }
                }
                else
                {
                    var res = await ProcessFixedFundDeadlinesAsync(fundId, ct);
                    if (res.Status != "NoAction")
                    {
                        processed++;
                    }
                }
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed processing fixed fund deadlines for FundId {FundId} ({ErrorType}): {Message}",
                    fundId, ex.GetType().Name, ex.Message);
            }
        }
        return processed;
    }

    public Task<V1DeadlineProcessingResultDto> ProcessFixedFundDeadlinesAsync(Guid fundId, CancellationToken ct) =>
        db.ExecuteInTransactionAsync(async token =>
        {
            var fund = await LockFundAsync(fundId, token);
            if (fund.DissolvedAt is not null || fund.Kind != V1FundKind.Fixed)
                return new V1DeadlineProcessingResultDto(fundId, "NoAction", Array.Empty<Guid>(), false);

            var treasury = await TreasuryAsync(fund, token);
            if (treasury.Revision == 0)
                return new V1DeadlineProcessingResultDto(fundId, "NoAction", Array.Empty<Guid>(), false);

            var activeMembers = await ActiveMembersAsync(fundId, token);
            var confirmedSeats = await db.V1FundObligations
                .Where(o => o.FundId == fundId && o.Revision == treasury.Revision)
                .Select(o => o.Seat)
                .Distinct()
                .ToListAsync(token);
            var paidDict = await ActivityPaidAsync(fundId, token);
            var obligations = await db.V1FundObligations.AsNoTracking()
                .Where(o => o.FundId == fundId && o.Revision == treasury.Revision)
                .ToListAsync(token);

            // 1. OWNER FUNDING FAILURE (Rule 46 & T09)
            // If Owner's personal contribution deadline has expired and owner has contributed less than required:
            // Owner is NOT auto-kicked. Instead, the funding period fails, auto-reconciling and dissolving the fund.
            var ownerMem = activeMembers.SingleOrDefault(m => m.UserId == fund.OwnerUserId);
            if (ownerMem is not null && ownerMem.ContributionDeadline.HasValue && clock.UtcNow >= ownerMem.ContributionDeadline.Value)
            {
                var ownerReq = obligations.SingleOrDefault(o => o.MembershipId == ownerMem.Id)?.RequiredVnd ?? 0;
                var ownerPaid = paidDict.GetValueOrDefault(ownerMem.Id, 0L);
                if (ownerPaid < ownerReq)
                {
                    var allMembersOnFailure = activeMembers.Select(x => x.UserId).ToList();
                    await AutoDissolveFixedFundAsync(fund, treasury, activeMembers, "OwnerFundingFailure", token);
                    await ActivityAsync(fundId, null, "OwnerFundingFailed", token,
                        subject: fund.OwnerUserId,
                        detail: $"Chủ nhóm không góp đủ nghĩa vụ khi hết hạn ({ownerPaid:N0}/{ownerReq:N0} đ). Kỳ góp thất bại, hệ thống tự giải tán và hoàn tiền.",
                        audience: allMembersOnFailure);
                    return new V1DeadlineProcessingResultDto(fundId, "OwnerFundingFailed", Array.Empty<Guid>(), true);
                }
            }

            // 2. MEMBER AUTO-KICK (Rule 45 & T06)
            // If a Member's personal deadline has expired and member has contributed less than required:
            // Auto-kick the member, refund their valid contribution, release the seat, and open 72h replacement window.
            var autoKicked = new List<Guid>();
            foreach (var m in activeMembers.Where(m => m.UserId != fund.OwnerUserId))
            {
                if (m.ContributionDeadline.HasValue && clock.UtcNow >= m.ContributionDeadline.Value)
                {
                    var req = obligations.SingleOrDefault(o => o.MembershipId == m.Id)?.RequiredVnd ?? 0;
                    var paid = paidDict.GetValueOrDefault(m.Id, 0L);
                    if (paid < req)
                    {
                        // Execute refund for any partial contribution
                        if (paid > 0)
                        {
                            var destWallet = await ResolveDestinationWalletAsync(m.UserId, null, token);
                            var destAcc = await db.LedgerAccounts.SingleAsync(a => a.WalletId == destWallet.Id, token);
                            var srcAcc = await FreshAccountAsync(treasury.LedgerAccountId, token);

                            var posting = await ledger.PostAsync(
                                srcAcc.Id,
                                destAcc.Id,
                                paid,
                                "VND",
                                "V1FundRefund",
                                fund.Id,
                                $"v1-fund-refund:{fund.Id:N}:{m.Id:N}",
                                $"auto-kick:{fund.Id:N}:{m.Id:N}",
                                $"Hoàn tiền Auto-Kick quỹ {fund.Name}",
                                token);

                            db.FinancialTransactions.Add(new FinancialTransaction
                            {
                                OwnerUserId = m.UserId,
                                WalletId = destWallet.Id,
                                LedgerTransactionId = posting.LedgerTransactionId,
                                RelatedBusinessId = fund.Id,
                                Type = FinancialTransactionType.Refund,
                                Status = FinancialTransactionStatus.Completed,
                                Amount = paid,
                                Currency = "VND",
                                Description = $"Hoàn tiền Auto-Kick quỹ {fund.Name}",
                                OccurredAt = clock.UtcNow
                            });
                        }

                        var memToUpdate = await db.V1FundMemberships.SingleAsync(x => x.Id == m.Id, token);
                        memToUpdate.RefundedVnd = checked((memToUpdate.RefundedVnd ?? 0) + paid);
                        memToUpdate.LeftAt = clock.UtcNow;
                        autoKicked.Add(m.UserId);

                        // Open 72-hour replacement window from the first Auto-Kick
                        if (treasury.ReplacementWindowExpiresAt is null)
                        {
                            treasury.ReplacementWindowExpiresAt = clock.UtcNow.AddHours(72);
                        }

                        var allMembersBeforeKick = activeMembers.Select(x => x.UserId).ToList();
                        await ActivityAsync(fundId, null, "MemberAutoKicked", token,
                            subject: m.UserId,
                            detail: $"Thành viên hết hạn đóng góp ({paid:N0}/{req:N0} đ). Hệ thống tự loại (Auto-Kick), hoàn {paid:N0} đ, mở cửa sổ thay thế 72 giờ.",
                            audience: allMembersBeforeKick);
                    }
                }
            }

            if (autoKicked.Count > 0)
            {
                var remaining = activeMembers.Where(m => !autoKicked.Contains(m.UserId)).ToList();
                treasury.MembershipSignature = MembershipSignature(remaining);
                await db.SaveChangesAsync(token);
            }

            // 3. 72-HOUR REPLACEMENT WINDOW EXPIRATION (Rule 50 & T08)
            // If the 72h window has expired and there are still unfilled vacant seats:
            // Period fails automatically, refunding all remaining members and dissolving the fund.
            var remainingActive = activeMembers.Where(m => !autoKicked.Contains(m.UserId)).ToList();
            if (treasury.ReplacementWindowExpiresAt.HasValue && clock.UtcNow >= treasury.ReplacementWindowExpiresAt.Value)
            {
                if (remainingActive.Count < confirmedSeats.Count)
                {
                    var allRemainingMembers = activeMembers.Select(x => x.UserId).ToList();
                    await AutoDissolveFixedFundAsync(fund, treasury, remainingActive, "ReplacementWindowExpired", token);
                    await ActivityAsync(fundId, null, "ReplacementWindowExpired", token,
                        subject: null,
                        detail: "Hết cửa sổ tìm người thay thế 72 giờ nhưng vẫn thiếu người. Kỳ góp thất bại, hệ thống tự giải tán và hoàn tiền.",
                        audience: allRemainingMembers);
                    return new V1DeadlineProcessingResultDto(fundId, "ReplacementWindowExpired", autoKicked, true);
                }
            }

            return new V1DeadlineProcessingResultDto(
                fundId,
                autoKicked.Count > 0 ? "MembersAutoKicked" : "NoAction",
                autoKicked,
                false);
        }, ct);

    public Task<V1DissolveResultDto> FailFixedPeriodEarlyAsync(Guid actor, Guid fundId, string pin, CancellationToken ct) =>
        db.ExecuteInTransactionAsync(async token =>
        {
            await RequireActorAsync(actor, token);
            await security.VerifyAsync(actor, pin, token);

            var fund = await LockFundAsync(fundId, token);
            RequireOwner(fund, actor);

            if (fund.DissolvedAt is not null)
                throw new ConflictException("fund_already_dissolved", "Fund is already dissolved.");
            if (fund.Kind != V1FundKind.Fixed)
                throw new ConflictException("fixed_fund_required", "Only fixed funds support period failure.");

            var treasury = await TreasuryAsync(fund, token);
            if (treasury.Revision == 0)
                throw new ConflictException("obligations_not_confirmed", "Fund has no confirmed period.");

            var activeMembers = await ActiveMembersAsync(fundId, token);
            var confirmedSeats = await db.V1FundObligations
                .Where(o => o.FundId == fundId && o.Revision == treasury.Revision)
                .Select(o => o.Seat)
                .Distinct()
                .ToListAsync(token);

            if (activeMembers.Count >= confirmedSeats.Count)
                throw new ConflictException("no_vacant_seats", "Kỳ không có ghế trống; không thể kết thúc thất bại sớm.");

            var (totalRefunded, refunds) = await AutoDissolveFixedFundAsync(fund, treasury, activeMembers, "EarlyFailureByOwner", token);
            await ActivityAsync(fundId, actor, "FixedPeriodFailedEarly", token,
                detail: "Chủ nhóm chủ động kết thúc kỳ thất bại khi còn ghế trống. Hệ thống giải tán và hoàn tiền cho mọi người.");

            return new V1DissolveResultDto(fundId, totalRefunded, 0, refunds, clock.UtcNow);
        }, ct);

    private async Task<(long TotalRefunded, List<V1RefundResultDto> Refunds)> AutoDissolveFixedFundAsync(
        V1Fund fund,
        V1FundTreasury treasury,
        List<V1FundMembership> activeMembers,
        string reason,
        CancellationToken token)
    {
        await RequireReconciledAccountingAsync(treasury, token);
        var treasuryAccount = await FreshAccountAsync(treasury.LedgerAccountId, token);
        var refunds = new List<V1RefundResultDto>();
        long totalRefunded = 0;

        if (treasuryAccount.Balance > 0 && activeMembers.Count > 0)
        {
            var calc = await CalculateUnifiedRefundsAsync(fund, treasury, null, token);
            const string caseType = "CaseB_DissolveFixed";

            foreach (var m in activeMembers)
            {
                var amount = calc.MemberRefunds.TryGetValue(m.UserId, out var tuple) ? tuple.Amount : 0L;
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
                        $"v1-fund-refund:{fund.Id:N}:{m.Id:N}",
                        $"auto-dissolve:{reason}:{fund.Id:N}:{m.UserId:N}",
                        $"Hoàn tiền giải tán tự động quỹ {fund.Name}: {reason}",
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
                        Description = $"Hoàn tiền giải tán tự động ({reason}) quỹ {fund.Name}",
                        OccurredAt = clock.UtcNow
                    });

                    totalRefunded += amount;
                }

                var mem = await db.V1FundMemberships.SingleAsync(x => x.Id == m.Id, token);
                mem.RefundedVnd = checked((mem.RefundedVnd ?? 0) + amount);
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

        await db.SaveChangesAsync(token);
        return (totalRefunded, refunds);
    }
}
