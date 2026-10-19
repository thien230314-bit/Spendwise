using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Features.Operations;
using SpendWise.Domain.Admin;

namespace SpendWise.Application.Features.V1Groups;

public sealed record V1GovernanceDto(Guid FundId, long OwnershipRevision, bool InactivityEligible, string ActivityEvidence, string ElectionAvailability, bool ObservedInactivity);

public sealed partial class V1FundService
{
    private async Task RecordActionAsync(Guid userId, CancellationToken ct)
    {
        // Called only by committed User-initiated fund commands after ordered admission, never refresh/push/read/worker.
        var now = clock.UtcNow;
        if (await db.V1UserActivities.Where(a => a.UserId == userId && a.LastActionAt < now)
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.LastActionAt, now), ct) == 0
            && !await db.V1UserActivities.AnyAsync(a => a.UserId == userId, ct))
            db.V1UserActivities.Add(new V1UserActivity { UserId = userId, LastActionAt = now });
    }

    public async Task<V1GovernanceDto> GovernanceAsync(Guid userId, Guid fundId, CancellationToken ct)
    {
        await RequireActorAsync(userId, ct); await RequireMemberAsync(userId, fundId, ct);
        var fund = await FundAsync(fundId, ct);
        var last = await db.V1UserActivities.AsNoTracking().Where(a => a.UserId == fund.OwnerUserId).Select(a => (DateTime?)a.LastActionAt).SingleOrDefaultAsync(ct);
        var eligible = last is not null && await V1MaintenanceGate.ActiveElapsedAsync(db, last.Value, clock.UtcNow, ct) >= TimeSpan.FromDays(15);
        var proof = await (inactivityEvidence ?? new IncompleteInactivityEvidence()).GetAsync(fund.OwnerUserId, ct);
        var available = "POLICY_BLOCKED";
        try { _ = (electionOptions?.Value ?? new V1ElectionPolicy()).Snapshot(); available = proof.CoverageComplete ? "CONFIGURED" : "ACTIVITY_EVIDENCE_BLOCKED"; }
        catch (SpendWise.Application.Common.Exceptions.ConflictException) { }
        return new(fundId, fund.OwnershipRevision, proof.CoverageComplete && proof.Eligible,
            proof.CoverageComplete ? "VERIFIED_FOREGROUND_COVERAGE" : last is null ? "UNKNOWN_NO_GENUINE_EVIDENCE" : "PARTIAL_ACTIVITY_COVERAGE", available, eligible);
    }
}
