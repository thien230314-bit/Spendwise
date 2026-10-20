using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Features.Groups;
using SpendWise.Application.Features.Operations;
using SpendWise.Domain.Identity;
using SpendWise.Domain.V1Groups;

namespace SpendWise.Application.Features.V1Groups;

public sealed partial class V1FundService
{
    public async Task<V1ElectionDto> RegisterCandidateAsync(Guid actor, Guid id, string pin, CancellationToken ct)
    {
        await RequireActorAsync(actor, ct); await security.VerifyAsync(actor, pin, ct);
        return await WithElectionAsync(actor, id, async (e, fund, token) =>
        {
            await RequireElectorAsync(actor, e.Id, token);
            if (await db.V1ElectionCandidates.AnyAsync(c => c.ElectionId == id && c.UserId == actor, token)) return ElectionDto(e);
            if (e.Phase != V1ElectionPhase.Registration || await PhaseExpiredAsync(e, token)) throw new ConflictException("candidate_registration_closed", "Candidate registration is closed.");
            var candidate = new V1ElectionCandidate { ElectionId = id, UserId = actor }; db.V1ElectionCandidates.Add(candidate);
            await ActivityAsync(e.FundId, actor, "ElectionCandidateRegistered", token, referenceId: candidate.Id); return ElectionDto(e);
        }, ct);
    }

    public Task<V1BallotReceipt> CastBallotAsync(Guid actor, Guid id, V1BallotRequest request, CancellationToken ct) =>
        WithElectionAsync(actor, id, async (e, fund, token) =>
        {
            await RequireElectorAsync(actor, id, token);
            var voter = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == actor, token);
            if (voter?.Status != SpendWise.Domain.Identity.UserStatus.Active) throw new ForbiddenException("account_not_active", "Voter account is not active.");
            var replay = await db.V1ElectionBallots.AsNoTracking().SingleOrDefaultAsync(b => b.ElectionId == id && b.VoterUserId == actor && b.Round == request.Round, token);
            if (replay is not null)
            {
                if (replay.CandidateUserId != request.CandidateUserId || replay.Support != request.Support) throw new ConflictException("ballot_already_cast", "A ballot cannot be changed.");
                return new V1BallotReceipt(replay.Id, replay.Round, true);
            }
            var round = e.Phase switch { V1ElectionPhase.Nomination => 1, V1ElectionPhase.Runoff => 2, V1ElectionPhase.Confirmation => 3, _ => 0 };
            if (round == 0 || request.Round != round || await PhaseExpiredAsync(e, token)) throw new ConflictException("election_round_closed", "Election round is closed or changed.");
            if (round == 3)
            {
                if (request.CandidateUserId is not null || request.Support is null) throw new AppException("invalid_ballot", "Confirmation requires an explicit Yes or No.");
            }
            else if (request.Support is not null || request.CandidateUserId is null || !await db.V1ElectionCandidates.AnyAsync(c => c.ElectionId == id
                && c.UserId == request.CandidateUserId && (round != 2 || c.InRunoff), token)) throw new AppException("invalid_ballot", "Select an eligible consented candidate.");
            var ballot = new V1ElectionBallot { ElectionId = id, VoterUserId = actor, Round = round, CandidateUserId = request.CandidateUserId, Support = request.Support };
            db.V1ElectionBallots.Add(ballot); await ActivityAsync(e.FundId, actor, "ElectionBallotCast", token, referenceId: ballot.Id);
            return new V1BallotReceipt(ballot.Id, ballot.Round, false);
        }, ct);

    private Task<T> WithElectionAsync<T>(Guid actor, Guid id, Func<V1Election, V1Fund, CancellationToken, Task<T>> action, CancellationToken ct, bool terminateInvalidated = false) =>
        db.ExecuteInTransactionAsync(async token =>
        {
            var snapshot = await db.V1Elections.AsNoTracking().SingleOrDefaultAsync(e => e.Id == id, token) ?? throw new NotFoundException("election_not_found", "Election not found.");
            var users = await db.V1ElectionVoters.AsNoTracking().Where(v => v.ElectionId == id).Select(v => v.UserId).ToListAsync(token);
            await GroupAdmission.LockUsersAsync(db, users.Append(actor).Append(snapshot.IncumbentUserId), token); await RequireActorAsync(actor, token);
            await AdmitActorSessionAsync(actor, token);
            var fund = await LockFundAsync(snapshot.FundId, token); await RequireMemberAsync(actor, fund.Id, token);
            var election = await db.V1Elections.SingleAsync(e => e.Id == id, token);
            if (db is DbContext context) await context.Entry(election).ReloadAsync(token);
            var validEpoch = election.Phase == V1ElectionPhase.Completed
                ? fund.OwnerUserId == election.WinnerUserId && fund.OwnershipRevision == election.OwnershipRevision + 1
                : fund.OwnerUserId == election.IncumbentUserId && fund.OwnershipRevision == election.OwnershipRevision;
            if (!validEpoch) throw new ConflictException("stale_ownership_epoch", "Election ownership epoch is stale.");
            if (election.FinishedAt is not null) return await action(election, fund, token);
            var expected = await db.V1ElectionVoters.CountAsync(v => v.ElectionId == id, token);
            var eligible = await db.V1ElectionVoters.CountAsync(v => v.ElectionId == id
                && db.V1FundMemberships.Any(m => m.Id == v.MembershipId && m.FundId == fund.Id && m.UserId == v.UserId && m.LeftAt == null)
                && db.Users.Any(u => u.Id == v.UserId && u.SystemRole == SystemRole.User && u.Status == UserStatus.Active && u.EmailVerifiedAt != null && !u.MustChangePassword), token);
            if (expected == 0 || eligible != expected)
            {
                if (!terminateInvalidated) throw new ConflictException("election_eligibility_changed", "Electorate eligibility changed; election must be terminated without replacing the owner.");
                await RequireElectorAsync(actor, id, token);
                FailElection(election, "EligibilityChanged");
                await ActivityAsync(fund.Id, actor, "ElectionInvalidated", token, referenceId: id);
            }
            return await action(election, fund, token);
        }, ct);

    private async Task RequireElectorAsync(Guid actor, Guid id, CancellationToken ct)
    {
        if (!await db.V1ElectionVoters.AnyAsync(v => v.ElectionId == id && v.UserId == actor, ct)) throw new ForbiddenException("not_elector", "Frozen electorate membership required.");
    }
    private static V1ElectionPolicySnapshot ElectionPolicy(V1Election e)
    {
        try
        {
            var p = JsonSerializer.Deserialize<V1ElectionPolicySnapshot>(e.PolicyJson) ?? throw new JsonException();
            if (string.IsNullOrWhiteSpace(p.Version) || p.Version.Length > 100 || p.RegistrationSeconds <= 0 || p.NominationSeconds <= 0
                || p.RunoffSeconds <= 0 || p.ConfirmationSeconds <= 0 || p.AcceptanceSeconds <= 0) throw new JsonException();
            return p;
        }
        catch (JsonException) { throw new ConflictException("election_policy_invalid", "Election policy requires review."); }
    }

    private async Task RequireNoActiveElectionAsync(Guid fundId, CancellationToken ct)
    {
        if (await db.V1ActivityProposals.AnyAsync(p => p.FundId == fundId && (p.State == "Voting" || p.State == "ApprovedAwaitingSettlement"), ct))
            throw new ConflictException("activity_transition_in_progress", "Membership is frozen until activity voting and settlement finish.");
        if (await db.V1Elections.AnyAsync(e => e.FundId == fundId && e.FinishedAt == null, ct))
            throw new ConflictException("election_in_progress", "Membership and voluntary ownership changes are frozen during an election.");
    }
    private async Task<bool> PhaseExpiredAsync(V1Election e, CancellationToken ct)
    {
        var seconds = PhaseSeconds(e);
        return seconds <= 0 || await V1MaintenanceGate.ActiveElapsedAsync(db, e.PhaseStartedAt, clock.UtcNow, ct, "Groups") >= TimeSpan.FromSeconds(seconds);
    }
    private static int PhaseSeconds(V1Election e)
    {
        var p = ElectionPolicy(e);
        return e.Phase switch { V1ElectionPhase.Registration => p.RegistrationSeconds, V1ElectionPhase.Nomination => p.NominationSeconds,
            V1ElectionPhase.Runoff => p.RunoffSeconds, V1ElectionPhase.Confirmation => p.ConfirmationSeconds, V1ElectionPhase.AwaitingAcceptance => p.AcceptanceSeconds, _ => 0 };
    }
}
