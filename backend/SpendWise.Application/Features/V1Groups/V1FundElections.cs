using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Domain.V1Groups;
using SpendWise.Domain.Identity;

namespace SpendWise.Application.Features.V1Groups;

public sealed partial class V1FundService
{
    public async Task<V1ElectionView> ElectionAsync(Guid actor, Guid id, CancellationToken ct)
    {
        await RequireActorAsync(actor, ct);
        var e = await db.V1Elections.AsNoTracking().SingleOrDefaultAsync(e => e.Id == id, ct) ?? throw new NotFoundException("election_not_found", "Election not found.");
        await RequireMemberAsync(actor, e.FundId, ct);
        var round = e.Phase switch { V1ElectionPhase.Nomination => 1, V1ElectionPhase.Runoff => 2, V1ElectionPhase.Confirmation => 3, _ => 0 };
        var own = await db.V1ElectionBallots.AsNoTracking().SingleOrDefaultAsync(b => b.ElectionId == id && b.VoterUserId == actor && b.Round == round, ct);
        var candidates = await db.V1ElectionCandidates.AsNoTracking().Where(c => c.ElectionId == id && (e.Phase != V1ElectionPhase.Runoff || c.InRunoff)).Select(c => c.UserId).ToListAsync(ct);
        return new(ElectionDto(e), candidates, await db.V1ElectionVoters.CountAsync(v => v.ElectionId == id, ct),
            await db.V1ElectionVoters.AnyAsync(v => v.ElectionId == id && v.UserId == actor, ct), own is null ? null : new(own.Id, own.Round, true));
    }

    public async Task<V1ElectionDto> OpenElectionAsync(Guid actor, Guid fundId, V1OpenElectionRequest request, CancellationToken ct)
    {
        await RequireActorAsync(actor, ct);
        var policy = (electionOptions?.Value ?? new V1ElectionPolicy()).Snapshot();
        return await db.ExecuteInTransactionAsync(async token =>
        {
            var admittedUsers = await db.V1FundMemberships.AsNoTracking().Where(m => m.FundId == fundId && m.LeftAt == null).Select(m => m.UserId).ToListAsync(token);
            await SpendWise.Application.Features.Groups.GroupAdmission.LockUsersAsync(db, admittedUsers.Append(actor), token);
            await RequireActorAsync(actor, token);
            await AdmitActorSessionAsync(actor, token);
            var fund = await LockFundAsync(fundId, token); await RequireMemberAsync(actor, fundId, token);
            var currentUsers = await db.V1FundMemberships.AsNoTracking().Where(m => m.FundId == fundId && m.LeftAt == null).Select(m => m.UserId).ToListAsync(token);
            if (!currentUsers.ToHashSet().SetEquals(admittedUsers)) throw new ConflictException("election_membership_changed", "Membership changed during admission; retry with the current electorate.");
            if (fund.OwnershipRevision != request.ExpectedOwnershipRevision) throw new ConflictException("stale_ownership_epoch", "Ownership changed.");
            if (fund.OwnerUserId == actor) throw new ForbiddenException("incumbent_not_elector", "Incumbent is excluded from replacement electorate.");
            var existing = await db.V1Elections.AsNoTracking().SingleOrDefaultAsync(e => e.FundId == fundId && e.FinishedAt == null, token);
            if (existing is not null) return ElectionDto(existing);
            var proof = await (inactivityEvidence ?? new IncompleteInactivityEvidence()).GetAsync(fund.OwnerUserId, token);
            if (await OwnerResponseOverdueAsync(fund, token)
                && await db.V1FundContributions.AnyAsync(c => c.FundId == fundId && c.UserId == actor && c.AmountVnd > 0, token))
                proof = new(true, true, "owner-response:" + fund.ActivityClosedAt!.Value.Ticks);
            if (!proof.CoverageComplete || !proof.Eligible || string.IsNullOrWhiteSpace(proof.EvidenceId) || proof.EvidenceId.Length > 200)
                throw new ConflictException("ACTIVITY_EVIDENCE_BLOCKED", "Complete authoritative inactivity evidence is required.");
            var voters = await db.V1FundMemberships.AsNoTracking().Where(m => m.FundId == fundId && m.LeftAt == null && m.UserId != fund.OwnerUserId
                && db.Users.Any(u => u.Id == m.UserId && u.SystemRole == SystemRole.User && u.Status == UserStatus.Active && u.EmailVerifiedAt != null && !u.MustChangePassword)).ToListAsync(token);
            if (voters.All(v => v.UserId != actor)) throw new ForbiddenException("not_elector", "Eligible electorate required.");
            var election = new V1Election { FundId = fundId, IncumbentUserId = fund.OwnerUserId, OpenedByUserId = actor,
                OwnershipRevision = fund.OwnershipRevision, PhaseStartedAt = clock.UtcNow, PolicyJson = JsonSerializer.Serialize(policy), InactivityEvidenceId = proof.EvidenceId };
            db.V1Elections.Add(election); db.V1ElectionVoters.AddRange(voters.Select(v => new V1ElectionVoter { ElectionId = election.Id, UserId = v.UserId, MembershipId = v.Id }));
            await ActivityAsync(fundId, actor, "ElectionOpened", token, referenceId: election.Id);
            return ElectionDto(election);
        }, ct);
    }
    private static V1ElectionDto ElectionDto(V1Election e) => new(e.Id, e.FundId, e.Phase.ToString(), e.OwnershipRevision, e.WinnerUserId, e.PhaseStartedAt, e.FinishedAt, e.FailureReason);
}
