using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Domain.Admin;

namespace SpendWise.Application.Features.Operations;

public static class V1MaintenanceBroadcast
{
    public static async Task QueueAsync(ISpendWiseDbContext db, IClock clock, V1MaintenancePlan plan, string action, Guid actor, CancellationToken ct)
    {
        var key = $"maintenance:{plan.Id:N}:{plan.Revision}:{action}";
        if (await db.V1Broadcasts.AnyAsync(b => b.EventKey == key, ct)) return;
        // Already emitted notices stay immutable; newer revisions supersede only unsent work.
        await db.V1Broadcasts.Where(b => b.MaintenancePlanId == plan.Id && (b.State == "Scheduled" || b.State == "Dispatching"))
            .ExecuteUpdateAsync(s => s.SetProperty(b => b.State, "Superseded"), ct);
        db.V1Broadcasts.Add(new V1Broadcast { CreatedByUserId = actor, EventKey = key, MaintenancePlanId = plan.Id, MaintenanceRevision = plan.Revision,
            Type = plan.IsEmergency && action == "V1_MAINTENANCE_EMERGENCY" ? "Emergency" : "Operational", TargetRole = "User", State = "Scheduled", ScheduledFor = clock.UtcNow,
            Title = "SpendWise maintenance update", Message = $"{action}; scope {plan.Scope}; start {plan.StartsAt:O}; estimated end {plan.ExpectedEndsAt:O}. Reopening requires manual safety confirmation. Check current server status; this notice is not a transaction result." });
    }
}
