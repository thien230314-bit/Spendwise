using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Domain.Finance;
using SpendWise.Domain.V1Groups;

namespace SpendWise.Application.Features.V1Groups;

public sealed record V1SettleActivityRequest(
    string SecurityPin,
    long? TargetVnd = null,
    int? ContributionDurationHours = null,
    string? NewName = null);

public sealed partial class V1FundService
{
    private Task<V1ActivityProposal?> CurrentActivityAsync(Guid fundId, CancellationToken ct) =>
        db.V1ActivityProposals.AsNoTracking().Where(p => p.FundId == fundId && p.SettledAt != null)
            .OrderByDescending(p => p.SettledAt).FirstOrDefaultAsync(ct);

    private async Task<Dictionary<Guid, long>> ActivityPaidAsync(Guid fundId, CancellationToken ct)
    {
        var period = await CurrentActivityAsync(fundId, ct);
        var paid = (await db.V1FundContributions.AsNoTracking().Where(c => c.FundId == fundId && (period == null || c.ObligationRevision >= period.StartedRevision)).ToListAsync(ct))
            .GroupBy(c => c.MembershipId).ToDictionary(g => g.Key, g => g.Sum(c => c.AmountVnd));
        if (period is not null)
            foreach (var voter in await db.V1ActivityVoters.AsNoTracking().Where(v => v.ProposalId == period.Id && v.Support == true).ToListAsync(ct))
                paid[voter.MembershipId] = checked(paid.GetValueOrDefault(voter.MembershipId) + (voter.CarryOverVnd ?? 0L));
        return paid;
    }

    public Task<V1ActivityProposalDto> SettleActivityAsync(Guid actor, Guid id, string pin, CancellationToken ct) =>
        SettleActivityAsync(actor, id, new V1SettleActivityRequest(pin), ct);

    public async Task<V1ActivityProposalDto> SettleActivityAsync(Guid actor, Guid id, V1SettleActivityRequest request, CancellationToken ct)
    {
        await RequireActorAsync(actor, ct); await security.VerifyAsync(actor, request.SecurityPin, ct);
        return await db.ExecuteInTransactionAsync(async token =>
        {
            var snapshot = await db.V1ActivityProposals.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id, token)
                ?? throw new NotFoundException("activity_proposal_not_found", "Proposal not found.");
            var voters = await db.V1ActivityVoters.Where(v => v.ProposalId == id).OrderBy(v => v.UserId).ToListAsync(token);
            await LockUsersAsync(voters.Select(v => v.UserId).Append(actor), token);
            var fund = await LockFundAsync(snapshot.FundId, token); RequireOwner(fund, actor);
            var proposal = await db.V1ActivityProposals.SingleAsync(p => p.Id == id, token);
            if (db is DbContext context) await context.Entry(proposal).ReloadAsync(token);
            if (proposal.State == "Settled") return await ActivityDtoAsync(proposal, actor, token);
            if ((proposal.State != "ApprovedAwaitingSettlement" && proposal.State != "AwaitingReplacement") || fund.ActivityClosedAt is null || proposal.OpenedAt < fund.ActivityClosedAt || fund.OwnershipRevision != proposal.OwnershipRevision)
                throw new ConflictException("activity_not_approved", "Approved current activity proposal required.");
            if (await db.V1Elections.AnyAsync(e => e.FundId == fund.Id && e.FinishedAt == null, token))
                throw new ConflictException("election_finance_paused", "Settlement is paused during an active election.");

            var activeMembers = await ActiveMembersAsync(fund.Id, token);
            if (activeMembers.Count < 2)
                throw new ConflictException("insufficient_members", "Kỳ mới phải có ít nhất hai người.");

            if (request.TargetVnd.HasValue && request.TargetVnd.Value != proposal.TargetVnd)
                throw new ConflictException("configuration_changed_requires_consent", "Changing target requires member consent before settlement.");

            if (request.ContributionDurationHours.HasValue && request.ContributionDurationHours.Value != proposal.ContributionDurationHours)
                throw new ConflictException("configuration_changed_requires_consent", "Changing duration requires member consent before settlement.");

            var finalTarget = proposal.TargetVnd;
            var finalDuration = proposal.ContributionDurationHours > 0 ? proposal.ContributionDurationHours : 24;

            if (finalTarget % activeMembers.Count != 0)
                throw new ConflictException("target_not_divisible", "Mục tiêu kỳ mới bắt buộc phải chia hết cho số ghế.");

            var treasury = await TreasuryAsync(fund, token);
            await RequireReconciledAccountingAsync(treasury, token);

            // Any voters with Support == false were already refunded during VoteActivityAsync or ProgressActivitiesAsync.
            // If any voter still has Support == null (unresponded), settle/refund them now before starting the new period.
            foreach (var voter in voters.Where(v => v.Support == null))
            {
                var membership = await db.V1FundMemberships.SingleOrDefaultAsync(m => m.Id == voter.MembershipId, token);
                if (membership is not null && membership.JoinedAt < proposal.OpenedAt && membership.LeftAt == null)
                {
                    var amount = voter.CarryOverVnd ?? 0L;
                    if (amount > 0)
                    {
                        var wallet = await ResolveDestinationWalletAsync(voter.UserId, null, token);
                        var destination = await db.LedgerAccounts.Where(a => a.WalletId == wallet.Id).Select(a => a.Id).SingleAsync(token);
                        await FreshAccountAsync(destination, token);
                        await FreshAccountAsync(treasury.LedgerAccountId, token);
                        var posting = await ledger.PostAsync(treasury.LedgerAccountId, destination, amount, "VND", "V1FundRefund", fund.Id,
                            $"v1-fund-activity-refund:{id:N}:{voter.UserId:N}", "settlement", "Activity non-participant refund", token);
                        db.FinancialTransactions.Add(new() {
                            OwnerUserId = voter.UserId, WalletId = wallet.Id, LedgerTransactionId = posting.LedgerTransactionId,
                            RelatedBusinessId = fund.Id, Type = FinancialTransactionType.Refund, Status = FinancialTransactionStatus.Completed,
                            Amount = amount, Currency = "VND", Description = "Activity non-participant refund", OccurredAt = clock.UtcNow
                        });
                    }
                    membership.RefundedVnd = checked((membership.RefundedVnd ?? 0) + amount);
                    membership.LeftAt = clock.UtcNow;
                    await ActivityAsync(fund.Id, actor, "ActivityNonParticipantSettled", token, voter.UserId, detail: amount.ToString(), referenceId: id);
                }
                voter.Support = false;
            }

            // Refresh active members after non-participants left
            activeMembers = await ActiveMembersAsync(fund.Id, token);
            if (activeMembers.Count < 2)
                throw new ConflictException("insufficient_members", "Kỳ mới phải có ít nhất hai người.");

            if (finalTarget % activeMembers.Count != 0)
                throw new ConflictException("target_not_divisible", "Mục tiêu kỳ mới bắt buộc phải chia hết cho số ghế.");

            // Guard: Check member obligation consent for ALL active members (Blocker P1)
            var required = FixedFundAllocation.Allocate(finalTarget, activeMembers.Select(m => m.Seat).ToArray());
            var allVoters = await db.V1ActivityVoters.Where(v => v.ProposalId == id).ToListAsync(token);

            foreach (var m in activeMembers)
            {
                var voter = allVoters.SingleOrDefault(v => v.UserId == m.UserId);
                if (voter is null || voter.Support != true || voter.ConfirmedObligationVnd == null)
                {
                    throw new ConflictException("obligation_changed_requires_consent",
                        $"Thành viên {m.UserId} chưa xác nhận nghĩa vụ tài chính cuối cùng {required[m.Seat]:N0} đ cho kỳ mới.");
                }

                if (voter.ConfirmedObligationVnd.Value != required[m.Seat] || voter.ConfirmedDurationHours != finalDuration)
                {
                    throw new ConflictException("obligation_changed_requires_consent",
                        $"Nghĩa vụ thành viên thay đổi từ {voter.ConfirmedObligationVnd.Value:N0} đ lên {required[m.Seat]:N0} đ do số thành viên thay đổi. Phải được thành viên đồng ý trước khi chốt kỳ.");
                }
            }

            if (!string.IsNullOrWhiteSpace(request.NewName) && request.NewName.Trim() != (proposal.NewName ?? fund.Name))
            {
                throw new ConflictException("configuration_changed_requires_consent", "Changing fund name requires member consent before settlement.");
            }

            proposal.TargetVnd = finalTarget;
            proposal.SettledAt = clock.UtcNow;
            proposal.StartedRevision = checked(treasury.Revision + 1);
            proposal.State = "Settled";
            treasury.Revision = checked(treasury.Revision + 1);
            treasury.TargetVnd = finalTarget;
            treasury.ReplacementWindowExpiresAt = null;

            treasury.MembershipSignature = MembershipSignature(activeMembers);
            var deadline = clock.UtcNow.AddHours(finalDuration);

            foreach (var m in activeMembers)
            {
                var trackedMem = await db.V1FundMemberships.SingleAsync(x => x.Id == m.Id, token);
                trackedMem.ContributionDeadline = deadline;
                trackedMem.RefundedVnd = 0;
            }

            db.V1FundObligations.AddRange(activeMembers.Select(m => new V1FundObligation {
                FundId = fund.Id, Revision = treasury.Revision,
                MembershipId = m.Id, UserId = m.UserId, Seat = m.Seat,
                RequiredVnd = required[m.Seat], Deadline = deadline
            }));

            var newName = !string.IsNullOrWhiteSpace(request.NewName) ? request.NewName.Trim() : (proposal.NewName ?? fund.Name);

            await db.V1Funds.Where(f => f.Id == fund.Id).ExecuteUpdateAsync(s => s
                .SetProperty(f => f.Name, newName)
                .SetProperty(f => f.TargetVnd, (long?)finalTarget)
                .SetProperty(f => f.ContributionDurationHours, finalDuration)
                .SetProperty(f => f.ActivityClosedAt, (DateTime?)null)
                .SetProperty(f => f.OwnerRespondedAt, (DateTime?)null)
                .SetProperty(f => f.OwnerResponseCaseId, (Guid?)null), token);

            await ActivityAsync(fund.Id, actor, "ActivitySettlementCompleted", token, referenceId: id);
            await db.SaveChangesAsync(token);
            return await ActivityDtoAsync(proposal, actor, token);
        }, ct);
    }
}
