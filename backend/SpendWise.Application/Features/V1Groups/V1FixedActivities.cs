using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Features.Groups;
using SpendWise.Application.Features.Operations;
using SpendWise.Domain.V1Groups;
using SpendWise.Domain.Identity;

namespace SpendWise.Application.Features.V1Groups;

public sealed record V1ProposeActivityRequest(long TargetVnd, string IdempotencyKey, string SecurityPin);
public sealed record V1ActivityProposalDto(Guid Id, Guid FundId, long TargetVnd, string State, int ElectorateSize, int Yes, int No, DateTime OpenedAt, DateTime Deadline, bool? OwnVote);

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
            if (await db.V1FundRequests.AnyAsync(x => x.FundId == fundId && (x.State == "AwaitingOwnerReview" || x.State == "ApprovedAwaitingFinanceContract"), token))
                throw new ConflictException("fund_requests_pending", "Resolve outstanding requests before closing the activity.");
            var treasury = await db.V1FundTreasuries.SingleOrDefaultAsync(t => t.FundId == fundId, token);
            if (treasury is not null) { await FreshAccountAsync(treasury.LedgerAccountId, token); await RequireUnspentAccountingAsync(treasury, token); }
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
            var replay = await db.V1ActivityProposals.AsNoTracking().SingleOrDefaultAsync(p => p.FundId == fundId && p.IdempotencyKey == key, token);
            if (replay is not null)
            {
                if (replay.ProposedByUserId != actor || replay.TargetVnd != request.TargetVnd) throw new ConflictException("idempotency_conflict", "Activity intent differs.");
                return await ActivityDtoAsync(replay, actor, token);
            }
            await RequireNoActiveElectionAsync(fundId, token);
            var current = await ActiveMembersAsync(fundId, token);
            if (!current.Select(m => m.Id).ToHashSet().SetEquals(members.Select(m => m.Id))) throw new ConflictException("fund_configuration_changed", "Membership changed during admission.");
            var proposal = new V1ActivityProposal { FundId = fundId, ProposedByUserId = actor, OwnershipRevision = fund.OwnershipRevision,
                TargetVnd = request.TargetVnd, IdempotencyKey = key, OpenedAt = clock.UtcNow };
            db.V1ActivityProposals.Add(proposal); db.V1ActivityVoters.AddRange(current.Select(m => new V1ActivityVoter { ProposalId = proposal.Id, UserId = m.UserId, MembershipId = m.Id }));
            await db.V1Funds.Where(f => f.Id == fundId).ExecuteUpdateAsync(s => s.SetProperty(f => f.OwnerRespondedAt, (DateTime?)clock.UtcNow), token);
            await ActivityAsync(fundId, actor, "ActivityVoteOpened", token, detail: "24h; silence means no participation; refund must complete before any removal.", referenceId: proposal.Id);
            return await ActivityDtoAsync(proposal, actor, token);
        }, ct);
    }

    public Task<V1ActivityProposalDto> VoteActivityAsync(Guid actor, Guid id, bool support, CancellationToken ct) => db.ExecuteInTransactionAsync(async token =>
    {
        await LockUsersAsync([actor], token);
        var p = await db.V1ActivityProposals.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id, token) ?? throw new NotFoundException("activity_proposal_not_found", "Proposal not found.");
        var fund = await LockFundAsync(p.FundId, token); await RequireMemberAsync(actor, fund.Id, token);
        p = await db.V1ActivityProposals.AsNoTracking().SingleAsync(p => p.Id == id, token);
        var vote = await db.V1ActivityVoters.AsNoTracking().SingleOrDefaultAsync(v => v.ProposalId == id && v.UserId == actor, token) ?? throw new ForbiddenException("not_elector", "Frozen electorate required.");
        if (vote.Support is { } previous)
        {
            if (previous != support) throw new ConflictException("vote_already_cast", "Vote cannot change.");
            return await ActivityDtoAsync(p, actor, token);
        }
        if (p.State != "Voting" || fund.OwnershipRevision != p.OwnershipRevision || await V1MaintenanceGate.ActiveElapsedAsync(db, p.OpenedAt, clock.UtcNow, token, "Groups") >= TimeSpan.FromHours(24))
            throw new ConflictException("activity_vote_closed", "Activity vote is closed.");
        if (await db.V1ActivityVoters.Where(v => v.Id == vote.Id && v.Support == null).ExecuteUpdateAsync(s => s.SetProperty(v => v.Support, (bool?)support), token) != 1)
            throw new ConflictException("vote_already_cast", "Vote cannot change.");
        await ActivityAsync(fund.Id, actor, "ActivityVoteCast", token, referenceId: id); return await ActivityDtoAsync(p, actor, token);
    }, ct);

    public async Task<V1ActivityProposalDto> ActivityProposalAsync(Guid actor, Guid id, CancellationToken ct)
    {
        await RequireActorAsync(actor, ct);
        var p = await db.V1ActivityProposals.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id, ct) ?? throw new NotFoundException("activity_proposal_not_found", "Proposal not found.");
        await RequireMemberAsync(actor, p.FundId, ct); return await ActivityDtoAsync(p, actor, ct);
    }
    private async Task<V1ActivityProposalDto> ActivityDtoAsync(V1ActivityProposal p, Guid actor, CancellationToken ct)
    {
        var voters = await db.V1ActivityVoters.AsNoTracking().Where(v => v.ProposalId == p.Id).ToListAsync(ct);
        return new(p.Id, p.FundId, p.TargetVnd, p.State, voters.Count, voters.Count(v => v.Support == true), voters.Count(v => v.Support == false), p.OpenedAt,
            await V1MaintenanceGate.DeadlineAsync(db, p.OpenedAt, TimeSpan.FromHours(24), clock.UtcNow, ct, "Groups"), voters.SingleOrDefault(v => v.UserId == actor)?.Support);
    }
}
