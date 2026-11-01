using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SpendWise.Application.Features.Groups;
using SpendWise.Application.Features.Operations;
using SpendWise.Domain.Finance;
using SpendWise.Domain.Identity;
using SpendWise.Domain.V1Groups;

namespace SpendWise.Application.Features.V1Groups;

public sealed partial class V1FundService
{
    public async Task<int> ProgressActivitiesAsync(CancellationToken ct)
    {
        var ids = await db.V1ActivityProposals.AsNoTracking()
            .Where(p => p.State == "Voting" || p.State == "AwaitingReplacement")
            .OrderBy(p => p.Id)
            .Select(p => p.Id)
            .ToListAsync(ct);

        var processed = 0;
        foreach (var id in ids)
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                if (scopes is not null)
                {
                    using var fundScope = scopes.CreateScope();
                    var scopedService = fundScope.ServiceProvider.GetRequiredService<V1FundService>();
                    var changed = await scopedService.ProcessActivityProposalDeadlineAsync(id, ct);
                    if (changed) processed++;
                }
                else
                {
                    var changed = await ProcessActivityProposalDeadlineAsync(id, ct);
                    if (changed) processed++;
                }
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed processing activity proposal {ProposalId}: {Message}", id, ex.Message);
            }
        }
        return processed;
    }

    public Task<bool> ProcessActivityProposalDeadlineAsync(Guid id, CancellationToken ct) =>
        db.ExecuteInTransactionAsync(async token =>
        {
            var snapshot = await db.V1ActivityProposals.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id, token);
            if (snapshot is null || snapshot.State is not ("Voting" or "AwaitingReplacement"))
                return false;

            var proposal = await db.V1ActivityProposals.SingleAsync(p => p.Id == id, token);
            var fund = await LockFundAsync(snapshot.FundId, token);
            var treasury = await TreasuryAsync(fund, token);

            if (proposal.State == "Voting")
            {
                var elapsed = clock.UtcNow - proposal.OpenedAt;
                if (elapsed >= TimeSpan.FromHours(24))
                {
                    // Silent members (Support == null): timeout = opt-out (T16)
                    var voters = await db.V1ActivityVoters.Where(v => v.ProposalId == id).ToListAsync(token);
                    foreach (var voter in voters.Where(v => v.Support == null))
                    {
                        var membership = await db.V1FundMemberships.SingleOrDefaultAsync(m => m.Id == voter.MembershipId, token);
                        if (membership is not null && membership.LeftAt == null)
                        {
                            var amount = voter.CarryOverVnd ?? 0L;
                            if (amount > 0)
                            {
                                var wallet = await ResolveDestinationWalletAsync(voter.UserId, null, token);
                                var destAcc = await db.LedgerAccounts.SingleAsync(a => a.WalletId == wallet.Id, token);
                                await FreshAccountAsync(destAcc.Id, token);
                                var srcAcc = await FreshAccountAsync(treasury.LedgerAccountId, token);

                                var posting = await ledger.PostAsync(
                                    srcAcc.Id,
                                    destAcc.Id,
                                    amount,
                                    "VND",
                                    "V1FundRefund",
                                    fund.Id,
                                    $"v1-fund-activity-refund:{id:N}:{voter.UserId:N}",
                                    "timeout-opt-out",
                                    $"Hoàn tiền hết hạn phản hồi kỳ quỹ {fund.Name}",
                                    token);

                                db.FinancialTransactions.Add(new FinancialTransaction
                                {
                                    OwnerUserId = voter.UserId,
                                    WalletId = wallet.Id,
                                    LedgerTransactionId = posting.LedgerTransactionId,
                                    RelatedBusinessId = fund.Id,
                                    Type = FinancialTransactionType.Refund,
                                    Status = FinancialTransactionStatus.Completed,
                                    Amount = amount,
                                    Currency = "VND",
                                    Description = $"Hoàn tiền hết hạn phản hồi kỳ quỹ {fund.Name}",
                                    OccurredAt = clock.UtcNow
                                });
                            }
                            membership.RefundedVnd = checked((membership.RefundedVnd ?? 0) + amount);
                            membership.LeftAt = clock.UtcNow;
                            await ActivityAsync(fund.Id, null, "ActivityNonParticipantSettled", token, voter.UserId, detail: amount.ToString(), referenceId: id);
                        }
                        voter.Support = false;
                    }

                    var continuing = voters.Where(v => v.Support == true).ToList();
                    if (continuing.Count >= 2)
                    {
                        proposal.State = "ApprovedAwaitingSettlement";
                        proposal.FinishedAt = clock.UtcNow;
                        await ActivityAsync(fund.Id, null, "ActivityVoteClosed", token, detail: proposal.State, referenceId: id);
                    }
                    else if (continuing.Count == 1 && continuing[0].UserId == fund.OwnerUserId)
                    {
                        // Only Owner -> Open 72h window (T18)
                        treasury.ReplacementWindowExpiresAt = clock.UtcNow.AddHours(72);
                        proposal.State = "AwaitingReplacement";
                        proposal.FinishedAt = clock.UtcNow;
                        await ActivityAsync(fund.Id, null, "NextPeriodReplacementWindowOpened", token, referenceId: id);
                    }
                    else
                    {
                        proposal.State = "Rejected";
                        proposal.FinishedAt = clock.UtcNow;
                        await ActivityAsync(fund.Id, null, "ActivityVoteClosed", token, detail: proposal.State, referenceId: id);
                    }
                    await db.SaveChangesAsync(token);
                    return true;
                }
                else if (elapsed >= TimeSpan.FromHours(18) && proposal.ReminderSentAt is null)
                {
                    proposal.ReminderSentAt = clock.UtcNow;
                    var voters = await db.V1ActivityVoters.Where(v => v.ProposalId == id).ToListAsync(token);
                    await ActivityAsync(fund.Id, null, "ActivityVoteReminder", token, detail: "Silence means not participating; any refund must finish before removal.", referenceId: id,
                        audience: voters.Where(v => v.Support == null).Select(v => v.UserId));
                    await db.SaveChangesAsync(token);
                    return true;
                }
            }
            else if (proposal.State == "AwaitingReplacement")
            {
                // T18: If 72h window expired and still < 2 active members: dissolve and refund Owner!
                if (treasury.ReplacementWindowExpiresAt.HasValue && clock.UtcNow >= treasury.ReplacementWindowExpiresAt.Value)
                {
                    var activeMembers = await ActiveMembersAsync(fund.Id, token);
                    if (activeMembers.Count < 2)
                    {
                        await AutoDissolveFixedFundAsync(fund, treasury, activeMembers, "NextPeriodReplacementExpired", token);
                        proposal.State = "Failed";
                        proposal.FinishedAt = clock.UtcNow;
                        await ActivityAsync(fund.Id, null, "ReplacementWindowExpired", token,
                            detail: "Hết 72h tìm thành viên mới cho kỳ tiếp theo nhưng không đủ người. Quỹ tự giải tán và hoàn tiền.", referenceId: id);
                        await db.SaveChangesAsync(token);
                        return true;
                    }
                }
            }
            return false;
        }, ct);
}
