using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Features.Groups;
using SpendWise.Application.Features.Operations;
using SpendWise.Domain.Finance;
using SpendWise.Domain.V1Groups;
using SpendWise.Domain.Identity;

namespace SpendWise.Application.Features.V1Groups;

public sealed record V1ProposeActivityRequest(long TargetVnd, string IdempotencyKey, string SecurityPin, int? ContributionDurationHours = null, string? NewName = null);
public sealed record V1ActivityProposalDto(Guid Id, Guid FundId, long TargetVnd, string State, int ElectorateSize, int Yes, int No, DateTime OpenedAt, DateTime Deadline, bool? OwnVote, long? OwnCarryOverVnd = null, int? ContributionDurationHours = null, long? ExpectedPerMemberVnd = null);

public sealed partial class V1FundService
{
    public async Task CloseActivityAsync(Guid actor, Guid fundId, string pin, CancellationToken ct)
    {
        await RequireActorAsync(actor, ct); await security.VerifyAsync(actor, pin, ct);
        await db.ExecuteInTransactionAsync(async token =>
        {
            await LockUsersAsync([actor], token); var fund = await LockFundAsync(fundId, token); RequireOwner(fund, actor);
            if (fund.Kind != V1FundKind.Fixed) throw new ConflictException("fixed_fund_required", "Only Fixed funds have activity cycles.");
            if (fund.ActivityClosedAt is not null) return true;
            await RequireNoActiveElectionAsync(fundId, token);
            if (await db.V1FundRequests.AnyAsync(x => x.FundId == fundId && (x.State == "AwaitingOwnerReview" || x.State == "ApprovedAwaitingFinanceContract" || x.State == "Pending24h" || x.State == "Suspended"), token))
                throw new ConflictException("fund_requests_pending", "Resolve outstanding requests before closing the activity.");
            var treasury = await db.V1FundTreasuries.SingleOrDefaultAsync(t => t.FundId == fundId, token);
            if (treasury is not null) { await FreshAccountAsync(treasury.LedgerAccountId, token); await RequireReconciledAccountingAsync(treasury, token); }
            await db.V1Funds.Where(f => f.Id == fundId).ExecuteUpdateAsync(s => s.SetProperty(f => f.ActivityClosedAt, (DateTime?)clock.UtcNow), token);
            await ActivityAsync(fundId, actor, "ActivityClosed", token); return true;
        }, ct);
    }

    public async Task<V1ActivityProposalDto> ProposeActivityAsync(Guid actor, Guid fundId, V1ProposeActivityRequest request, CancellationToken ct)
    {
        var key = InputBoundary.RequiredText(request.IdempotencyKey, 100, "Idempotency key");
        if (request.TargetVnd is <= 0 or > MaximumVnd) throw new AppException("invalid_amount", "Use positive integer VND.");
        await RequireActorAsync(actor, ct); await security.VerifyAsync(actor, request.SecurityPin, ct);
        return await db.ExecuteInTransactionAsync(async token =>
        {
            var members = await ActiveMembersAsync(fundId, token);
            await LockUsersAsync(members.Select(m => m.UserId).Append(actor), token);
            var fund = await LockFundAsync(fundId, token); RequireOwner(fund, actor);
            if (fund.Kind != V1FundKind.Fixed || fund.ActivityClosedAt is null) throw new ConflictException("closed_fixed_activity_required", "Close the current Fixed activity first.");

            // Rule 12 & Rule 6: Max 3 periods cap (T21)
            var settledCount = await db.V1ActivityProposals.CountAsync(p => p.FundId == fundId && p.SettledAt != null, token);
            if (settledCount >= 2)
            {
                throw new ConflictException("maximum_periods_exceeded", "Fixed Fund chỉ được tối đa 3 kỳ hoạt động. Kỳ hiện tại là kỳ cuối cùng, chỉ được quyết toán và giải tán.");
            }

            var hasActiveProposal = await db.V1ActivityProposals
                .AnyAsync(p => p.FundId == fundId && (p.State == "Voting" || p.State == "AwaitingReplacement" || p.State == "ApprovedAwaitingSettlement"), token);
            if (hasActiveProposal)
            {
                throw new ConflictException("active_proposal_exists", "An active activity proposal already exists for this fund.");
            }

            if (request.ContributionDurationHours is not null && (request.ContributionDurationHours < 1 || request.ContributionDurationHours > 720))
            {
                throw new AppException("invalid_duration", "Contribution duration must be between 1 and 720 hours.");
            }

            var replay = await db.V1ActivityProposals.AsNoTracking().SingleOrDefaultAsync(p => p.FundId == fundId && p.IdempotencyKey == key, token);
            if (replay is not null)
            {
                if (replay.ProposedByUserId != actor || replay.TargetVnd != request.TargetVnd) throw new ConflictException("idempotency_conflict", "Activity intent differs.");
                return await ActivityDtoAsync(replay, actor, token);
            }
            await RequireNoActiveElectionAsync(fundId, token);
            var current = await ActiveMembersAsync(fundId, token);
            if (!current.Select(m => m.Id).ToHashSet().SetEquals(members.Select(m => m.Id))) throw new ConflictException("fund_configuration_changed", "Membership changed during admission.");

            var treasury = await TreasuryAsync(fund, token);
            // Immutable snapshot of each member's entitlement at proposal time (Rule 6, T15)
            var allocation = await CalculateUnifiedRefundsAsync(fund, treasury, null, token);

            var proposal = new V1ActivityProposal
            {
                FundId = fundId,
                ProposedByUserId = actor,
                OwnershipRevision = fund.OwnershipRevision,
                TargetVnd = request.TargetVnd,
                IdempotencyKey = key,
                OpenedAt = clock.UtcNow,
                State = "Voting"
            };
            db.V1ActivityProposals.Add(proposal);

            db.V1ActivityVoters.AddRange(current.Select(m => new V1ActivityVoter
            {
                ProposalId = proposal.Id,
                UserId = m.UserId,
                MembershipId = m.Id,
                Support = m.UserId == actor ? true : (bool?)null,
                CarryOverVnd = allocation.MemberRefunds.GetValueOrDefault(m.UserId).Amount // Immutable Snapshot!
            }));

            await db.V1Funds.Where(f => f.Id == fundId).ExecuteUpdateAsync(s => s.SetProperty(f => f.OwnerRespondedAt, (DateTime?)clock.UtcNow), token);
            await ActivityAsync(fundId, actor, "ActivityVoteOpened", token, detail: "24h; silence means no participation; refund must complete before any removal.", referenceId: proposal.Id);
            return await ActivityDtoAsync(proposal, actor, token);
        }, ct);
    }

    public Task<V1ActivityProposalDto> VoteActivityAsync(Guid actor, Guid id, bool support, CancellationToken ct) => db.ExecuteInTransactionAsync(async token =>
    {
        await LockUsersAsync([actor], token);
        var p = await db.V1ActivityProposals.SingleOrDefaultAsync(p => p.Id == id, token) ?? throw new NotFoundException("activity_proposal_not_found", "Proposal not found.");
        var fund = await LockFundAsync(p.FundId, token); await RequireMemberAsync(actor, fund.Id, token);
        var vote = await db.V1ActivityVoters.SingleOrDefaultAsync(v => v.ProposalId == id && v.UserId == actor, token) ?? throw new ForbiddenException("not_elector", "Frozen electorate required.");
        if (vote.Support is { } previous)
        {
            if (previous != support) throw new ConflictException("vote_already_cast", "Vote cannot change.");
            return await ActivityDtoAsync(p, actor, token);
        }
        if (p.State != "Voting" || fund.OwnershipRevision != p.OwnershipRevision || await V1MaintenanceGate.ActiveElapsedAsync(db, p.OpenedAt, clock.UtcNow, token, "Groups") >= TimeSpan.FromHours(24))
            throw new ConflictException("activity_vote_closed", "Activity vote is closed.");

        if (actor == fund.OwnerUserId && !support)
        {
            throw new ConflictException("owner_cannot_opt_out", "Owner cannot opt out of their own proposal. Cancel the proposal instead to dissolve.");
        }

        var treasury = await TreasuryAsync(fund, token);

        if (!support)
        {
            // Member clicks "Không — rời và hoàn tiền ngay" (Rule 6, T15)
            // Use their immutable snapshot entitlement CarryOverVnd!
            var mem = await db.V1FundMemberships.SingleAsync(m => m.Id == vote.MembershipId, token);
            var amount = vote.CarryOverVnd ?? 0L;
            if (amount > 0)
            {
                var wallet = await ResolveDestinationWalletAsync(actor, null, token);
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
                    $"v1-fund-activity-refund:{id:N}:{actor:N}",
                    "opt-out",
                    $"Hoàn tiền rời kỳ quỹ {fund.Name}",
                    token);

                db.FinancialTransactions.Add(new FinancialTransaction
                {
                    OwnerUserId = actor,
                    WalletId = wallet.Id,
                    LedgerTransactionId = posting.LedgerTransactionId,
                    RelatedBusinessId = fund.Id,
                    Type = FinancialTransactionType.Refund,
                    Status = FinancialTransactionStatus.Completed,
                    Amount = amount,
                    Currency = "VND",
                    Description = $"Hoàn tiền rời kỳ quỹ {fund.Name}",
                    OccurredAt = clock.UtcNow
                });
            }
            mem.RefundedVnd = checked((mem.RefundedVnd ?? 0) + amount);
            mem.LeftAt = clock.UtcNow;
            vote.Support = false;
            await ActivityAsync(fund.Id, actor, "ActivityNonParticipantSettled", token, actor, detail: amount.ToString(), referenceId: id);
        }
        else
        {
            vote.Support = true;
            await ActivityAsync(fund.Id, actor, "ActivityVoteCast", token, referenceId: id);
        }

        await db.SaveChangesAsync(token);

        // Check if all voters have decided
        var allVoters = await db.V1ActivityVoters.Where(v => v.ProposalId == id).ToListAsync(token);
        if (allVoters.All(v => v.Support != null))
        {
            var continuing = allVoters.Where(v => v.Support == true).ToList();
            if (continuing.Count >= 2)
            {
                p.State = "ApprovedAwaitingSettlement";
                p.FinishedAt = clock.UtcNow;
            }
            else if (continuing.Count == 1 && continuing[0].UserId == fund.OwnerUserId)
            {
                // Only Owner -> Open 72h window (T18)
                treasury.ReplacementWindowExpiresAt = clock.UtcNow.AddHours(72);
                p.State = "AwaitingReplacement";
                p.FinishedAt = clock.UtcNow;
            }
            else
            {
                p.State = "Rejected";
                p.FinishedAt = clock.UtcNow;
            }
            await db.SaveChangesAsync(token);
        }

        return await ActivityDtoAsync(p, actor, token);
    }, ct);

    public async Task<V1ActivityProposalDto> CancelActivityProposalAsync(Guid actor, Guid id, string pin, CancellationToken ct)
    {
        await RequireActorAsync(actor, ct);
        await security.VerifyAsync(actor, pin, ct);
        return await db.ExecuteInTransactionAsync(async token =>
        {
            var p = await db.V1ActivityProposals.SingleOrDefaultAsync(p => p.Id == id, token)
                ?? throw new NotFoundException("activity_proposal_not_found", "Proposal not found.");
            var fund = await LockFundAsync(p.FundId, token);
            RequireOwner(fund, actor);

            if (p.State != "Voting" || clock.UtcNow - p.OpenedAt >= TimeSpan.FromHours(24))
                throw new ConflictException("proposal_voting_closed", "Proposal can only be cancelled while voting window is open.");

            p.State = "Cancelled";
            p.FinishedAt = clock.UtcNow;

            var treasury = await db.V1FundTreasuries.SingleOrDefaultAsync(t => t.FundId == fund.Id, token);
            if (treasury?.ReplacementWindowExpiresAt is not null)
            {
                treasury.ReplacementWindowExpiresAt = null;
            }

            await ActivityAsync(fund.Id, actor, "ActivityProposalCancelled", token, referenceId: id);
            await db.SaveChangesAsync(token);
            return await ActivityDtoAsync(p, actor, token);
        }, ct);
    }

    public async Task<V1ActivityProposalDto> ActivityProposalAsync(Guid actor, Guid id, CancellationToken ct)
    {
        await RequireActorAsync(actor, ct);
        var p = await db.V1ActivityProposals.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id, ct) ?? throw new NotFoundException("activity_proposal_not_found", "Proposal not found.");
        await RequireMemberAsync(actor, p.FundId, ct); return await ActivityDtoAsync(p, actor, ct);
    }

    private async Task<V1ActivityProposalDto> ActivityDtoAsync(V1ActivityProposal p, Guid actor, CancellationToken ct)
    {
        var voters = await db.V1ActivityVoters.AsNoTracking().Where(v => v.ProposalId == p.Id).ToListAsync(ct);
        var own = voters.SingleOrDefault(v => v.UserId == actor);
        var fund = await db.V1Funds.AsNoTracking().SingleOrDefaultAsync(f => f.Id == p.FundId, ct);
        var duration = fund?.ContributionDurationHours > 0 ? fund.ContributionDurationHours : 24;
        var expectedPerMember = voters.Count > 0 ? p.TargetVnd / voters.Count : p.TargetVnd;
        return new(p.Id, p.FundId, p.TargetVnd, p.State, voters.Count, voters.Count(v => v.Support == true), voters.Count(v => v.Support == false), p.OpenedAt,
            await V1MaintenanceGate.DeadlineAsync(db, p.OpenedAt, TimeSpan.FromHours(24), clock.UtcNow, ct, "Groups"), own?.Support, own?.CarryOverVnd, duration, expectedPerMember);
    }
}
