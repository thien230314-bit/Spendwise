using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Domain.V1Groups;

namespace SpendWise.Application.Features.V1Groups;

public sealed partial class V1FundService
{
    public Task<V1ElectionDto> AdvanceElectionAsync(Guid actor, Guid id, CancellationToken ct) => WithElectionAsync(actor, id, async (e, fund, token) =>
    {
        await RequireElectorAsync(actor, id, token);
        return await TransitionElectionAsync(e, actor, token);
    }, ct, terminateInvalidated: true);

    private async Task<V1ElectionDto> TransitionElectionAsync(V1Election e, Guid? actor, CancellationToken token)
    {
        var id = e.Id;
        if (e.FinishedAt is not null) return ElectionDto(e);
        if (!await PhaseExpiredAsync(e, token)) throw new ConflictException("election_round_open", "Election phase is still open.");
        var nextStart = await SpendWise.Application.Features.Operations.V1MaintenanceGate.DeadlineAsync(db, e.PhaseStartedAt, TimeSpan.FromSeconds(PhaseSeconds(e)), clock.UtcNow, token, "Groups");
        if (e.Phase == V1ElectionPhase.Registration)
        {
            if (!await db.V1ElectionCandidates.AnyAsync(c => c.ElectionId == id, token)) FailElection(e, "NoCandidates");
            else e.Phase = V1ElectionPhase.Nomination;
        }
        else if (e.Phase is V1ElectionPhase.Nomination or V1ElectionPhase.Runoff)
        {
            var round = e.Phase == V1ElectionPhase.Nomination ? 1 : 2;
            var ballots = await db.V1ElectionBallots.AsNoTracking().Where(b => b.ElectionId == id && b.Round == round).ToListAsync(token);
            if (ballots.Count == 0) FailElection(e, "NoVotes");
            else
            {
                var counts = ballots.GroupBy(b => b.CandidateUserId!.Value).ToDictionary(g => g.Key, g => g.Count()); var max = counts.Values.Max();
                var top = counts.Where(c => c.Value == max).Select(c => c.Key).ToArray();
                if (top.Length > 1 && round == 2) FailElection(e, "RunoffTie");
                else if (top.Length > 1)
                {
                    await db.V1ElectionCandidates.Where(c => c.ElectionId == id && top.Contains(c.UserId)).ExecuteUpdateAsync(s => s.SetProperty(c => c.InRunoff, true), token);
                    e.Phase = V1ElectionPhase.Runoff;
                }
                else { e.WinnerUserId = top[0]; e.Phase = V1ElectionPhase.Confirmation; }
            }
        }
        else if (e.Phase == V1ElectionPhase.Confirmation)
        {
            var voters = await db.V1ElectionVoters.CountAsync(v => v.ElectionId == id, token);
            var yes = await db.V1ElectionBallots.CountAsync(b => b.ElectionId == id && b.Round == 3 && b.Support == true, token);
            if (yes * 2 > voters) e.Phase = V1ElectionPhase.AwaitingAcceptance; else FailElection(e, "NoAbsoluteMajority");
        }
        else if (e.Phase == V1ElectionPhase.AwaitingAcceptance) FailElection(e, "AcceptanceExpired");
        e.PhaseStartedAt = nextStart; await ActivityAsync(e.FundId, actor, "ElectionPhaseAdvanced", token, detail: e.Phase.ToString(), referenceId: id);
        return ElectionDto(e);
    }

    public async Task<V1ElectionDto> AcceptElectedOwnershipAsync(Guid actor, Guid id, string pin, CancellationToken ct)
    {
        await RequireActorAsync(actor, ct); await security.VerifyAsync(actor, pin, ct);
        return await WithElectionAsync(actor, id, async (e, fund, token) =>
        {
            await RequireElectorAsync(actor, id, token);
            if (e.WinnerUserId != actor) throw new ForbiddenException("not_elected_candidate", "Only the elected candidate may accept.");
            if (e.Phase == V1ElectionPhase.Completed) return ElectionDto(e);
            if (e.Phase != V1ElectionPhase.AwaitingAcceptance || await PhaseExpiredAsync(e, token)) throw new ConflictException("election_acceptance_closed", "Acceptance is unavailable.");
            var proof = await (inactivityEvidence ?? new IncompleteInactivityEvidence()).GetAsync(e.IncumbentUserId, token);
            if (e.InactivityEvidenceId == "owner-response:" + fund.ActivityClosedAt?.Ticks && await OwnerResponseOverdueAsync(fund, token))
                proof = new(true, true, e.InactivityEvidenceId);
            if (!proof.CoverageComplete || !proof.Eligible || string.IsNullOrWhiteSpace(proof.EvidenceId))
                throw new ConflictException("ACTIVITY_EVIDENCE_BLOCKED", "Complete authoritative inactivity evidence is required.");
            var changed = await db.V1Funds.Where(f => f.Id == fund.Id && f.OwnerUserId == e.IncumbentUserId && f.OwnershipRevision == e.OwnershipRevision)
                .ExecuteUpdateAsync(s => s.SetProperty(f => f.OwnerUserId, actor).SetProperty(f => f.OwnershipRevision, f => f.OwnershipRevision + 1), token);
            if (changed != 1) throw new ConflictException("stale_ownership_epoch", "Election ownership epoch is stale.");
            e.Phase = V1ElectionPhase.Completed; e.FinishedAt = clock.UtcNow;
            await ActivityAsync(fund.Id, actor, "ElectionOwnershipAccepted", token, e.IncumbentUserId, referenceId: id);
            return ElectionDto(e);
        }, ct);
    }
    private void FailElection(V1Election e, string reason) { e.Phase = V1ElectionPhase.Failed; e.FailureReason = reason; e.FinishedAt = clock.UtcNow; }
}
