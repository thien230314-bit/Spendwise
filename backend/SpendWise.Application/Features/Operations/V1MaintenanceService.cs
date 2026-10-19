using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Admin;
using SpendWise.Application.Features.Administration;
using SpendWise.Domain.Admin;
using SpendWise.Domain.Identity;

namespace SpendWise.Application.Features.Operations;

public sealed record ScheduleMaintenanceRequest(DateTime StartsAt, DateTime ExpectedEndsAt, string Scope, string SecurityPin, string Reason);
public sealed record MaintenanceActionRequest(string SecurityPin, string Reason);
public sealed record MaintenancePlanDto(Guid Id, DateTime StartsAt, DateTime ExpectedEndsAt, string State, long Revision, string Scope = "Global", bool IsEmergency = false);
public sealed record EmergencyMaintenanceRequest(DateTime ExpectedEndsAt, string Scope, string SecurityPin, string Reason, string ImpactConfirmation, string IdempotencyKey);
public sealed record ExtendMaintenanceRequest(DateTime ExpectedEndsAt, long ExpectedRevision, string SecurityPin, string Reason);
public sealed record RescheduleMaintenanceRequest(DateTime StartsAt, DateTime ExpectedEndsAt, string Scope, long ExpectedRevision, string SecurityPin, string Reason);
public sealed record MaintenanceSafetyResult(bool Ready, string? EvidenceId);
public interface IV1MaintenanceSafety { Task<MaintenanceSafetyResult> CheckAsync(Guid planId, CancellationToken ct); }
public sealed class UnconfiguredMaintenanceSafety : IV1MaintenanceSafety
{
    public Task<MaintenanceSafetyResult> CheckAsync(Guid id, CancellationToken ct) => Task.FromResult(new MaintenanceSafetyResult(false, null));
}

public sealed class V1MaintenanceService(ISpendWiseDbContext db, IClock clock, PlatformPermissionService permissions,
    AdminSecurityGuard security, AdminAuditWriter audit, IV1MaintenanceSafety safety)
{
    public async Task<MaintenancePlanDto> ScheduleAsync(ClaimsPrincipal principal, ScheduleMaintenanceRequest request, CancellationToken ct, bool emergency = false, string? operationKey = null)
    {
        var reason = InputBoundary.RequiredText(request.Reason, 1000, "Reason"); AdminAuditWriter.RequireReason(reason);
        if (request.Scope is not ("Global" or "Groups" or "AI" or "Money")) throw new ConflictException("maintenance_scope_not_proven", "Unsupported module scope.");
        if (request.StartsAt.Kind != DateTimeKind.Utc || request.ExpectedEndsAt.Kind != DateTimeKind.Utc
            || !emergency && request.StartsAt < clock.UtcNow || request.ExpectedEndsAt <= request.StartsAt)
            throw new AppException("invalid_maintenance_window", "Use a future UTC start and later estimated end.");
        var actor = await OperatorAsync(principal, request.SecurityPin, ct);
        return await db.ExecuteInTransactionAsync(async token =>
        {
            await AdmitOperatorAsync(principal, actor, token); await AdmitAsync(token); await RequirePermissionAsync(principal, token);
            if (operationKey is not null)
            {
                var replay = await db.V1MaintenancePlans.AsNoTracking().SingleOrDefaultAsync(p => p.CreatedByUserId == actor && p.OperationKey == operationKey, token);
                if (replay is not null)
                {
                    if (!replay.IsEmergency || replay.Scope != request.Scope || replay.Reason != reason || replay.ExpectedEndsAt != request.ExpectedEndsAt)
                        throw new ConflictException("idempotency_conflict", "Maintenance intent differs.");
                    return Map(replay);
                }
            }
            if (!emergency && request.StartsAt < clock.UtcNow || request.ExpectedEndsAt <= clock.UtcNow) throw new ConflictException("maintenance_start_elapsed", "Maintenance window elapsed while waiting for admission.");
            if (emergency) await db.AcquireMaintenanceAdmissionAsync(true, token);
            if (await db.V1MaintenancePlans.AnyAsync(p => p.CancelledAt == null && p.ClosedAt == null
                    && p.StartsAt < request.ExpectedEndsAt && p.ExpectedEndsAt > request.StartsAt, token))
                throw new ConflictException("maintenance_overlap", "Maintenance windows overlap.");
            var plan = new V1MaintenancePlan { CreatedByUserId = actor, StartsAt = emergency ? clock.UtcNow : request.StartsAt,
                ExpectedEndsAt = request.ExpectedEndsAt, Reason = reason, Revision = 1, Scope = request.Scope, IsEmergency = emergency, OperationKey = operationKey };
            db.V1MaintenancePlans.Add(plan);
            audit.Add(actor, emergency ? "V1_MAINTENANCE_EMERGENCY" : "V1_MAINTENANCE_SCHEDULED", "V1MaintenancePlan", plan.Id.ToString(), reason, new { plan.StartsAt, plan.ExpectedEndsAt, plan.Scope, ImpactConfirmed = emergency });
            await V1MaintenanceBroadcast.QueueAsync(db, clock, plan, emergency ? "V1_MAINTENANCE_EMERGENCY" : "V1_MAINTENANCE_SCHEDULED", actor, token);
            await db.SaveChangesAsync(token); return Map(plan);
        }, ct);
    }

    public Task<MaintenancePlanDto> EmergencyAsync(ClaimsPrincipal principal, EmergencyMaintenanceRequest request, CancellationToken ct)
    {
        if (request.ImpactConfirmation != "CONFIRM_MAINTENANCE_IMPACT") throw new AppException("impact_confirmation_required", "Confirm the maintenance impact explicitly.");
        var key = InputBoundary.RequiredText(request.IdempotencyKey, 100, "Idempotency key");
        return ScheduleAsync(principal, new(clock.UtcNow, request.ExpectedEndsAt, request.Scope, request.SecurityPin, request.Reason), ct, true, key);
    }

    public Task ExtendAsync(ClaimsPrincipal principal, Guid id, ExtendMaintenanceRequest request, CancellationToken ct) =>
        MutateAsync(principal, id, new(request.SecurityPin, request.Reason), (p, token) =>
        {
            if (p.Revision != request.ExpectedRevision) throw new ConflictException("stale_maintenance_revision", "Maintenance changed.");
            if (p.CancelledAt is not null || p.ClosedAt is not null || request.ExpectedEndsAt.Kind != DateTimeKind.Utc || request.ExpectedEndsAt <= p.ExpectedEndsAt || request.ExpectedEndsAt <= clock.UtcNow)
                throw new ConflictException("invalid_maintenance_extension", "Extend an open plan to a later UTC estimate.");
            p.ExpectedEndsAt = request.ExpectedEndsAt; return Task.CompletedTask;
        }, "V1_MAINTENANCE_EXTENDED", ct);

    public Task CancelAsync(ClaimsPrincipal principal, Guid id, MaintenanceActionRequest request, CancellationToken ct) =>
        MutateAsync(principal, id, request, async (plan, token) =>
        {
            if (plan.CancelledAt is not null) return;
            if (plan.StartsAt <= clock.UtcNow || plan.ClosedAt is not null) throw new ConflictException("maintenance_already_started", "Active maintenance cannot be cancelled.");
            plan.CancelledAt = clock.UtcNow; await Task.CompletedTask;
        }, "V1_MAINTENANCE_CANCELLED", ct);

    public Task RescheduleAsync(ClaimsPrincipal principal, Guid id, RescheduleMaintenanceRequest request, CancellationToken ct) =>
        MutateAsync(principal, id, new(request.SecurityPin, request.Reason), async (p, token) =>
        {
            if (p.Revision != request.ExpectedRevision) throw new ConflictException("stale_maintenance_revision", "Maintenance changed.");
            if (p.CancelledAt is not null || p.ClosedAt is not null || p.StartsAt <= clock.UtcNow || request.StartsAt < clock.UtcNow
                || request.StartsAt.Kind != DateTimeKind.Utc || request.ExpectedEndsAt.Kind != DateTimeKind.Utc || request.ExpectedEndsAt <= request.StartsAt
                || request.Scope is not ("Global" or "Groups" or "AI" or "Money")) throw new ConflictException("invalid_maintenance_edit", "Only future schedules can be edited.");
            if (await db.V1MaintenancePlans.AnyAsync(other => other.Id != id && other.CancelledAt == null && other.ClosedAt == null
                && other.StartsAt < request.ExpectedEndsAt && other.ExpectedEndsAt > request.StartsAt, token)) throw new ConflictException("maintenance_overlap", "Maintenance windows overlap.");
            p.StartsAt = request.StartsAt; p.ExpectedEndsAt = request.ExpectedEndsAt; p.Scope = request.Scope;
        }, "V1_MAINTENANCE_RESCHEDULED", ct);

    public Task ReviewEmergencyAsync(ClaimsPrincipal principal, Guid id, MaintenanceActionRequest request, CancellationToken ct) =>
        MutateAsync(principal, id, request, (p, token) =>
        {
            if (!p.IsEmergency) throw new ConflictException("emergency_plan_required", "Only emergency incidents use this review.");
            var notes = InputBoundary.RequiredText(request.Reason, 1000, "Review notes");
            if (p.ReviewedAt is not null)
            {
                if (p.ReviewNotes != notes) throw new ConflictException("review_immutable", "Append a new audit process rather than replace an incident review.");
                return Task.CompletedTask;
            }
            p.ReviewedAt = clock.UtcNow; p.ReviewedByUserId = Guid.Parse(principal.FindFirst(ClaimTypes.NameIdentifier)!.Value); p.ReviewNotes = notes;
            return Task.CompletedTask;
        }, "V1_MAINTENANCE_REVIEWED", ct);

    public Task CloseAsync(ClaimsPrincipal principal, Guid id, MaintenanceActionRequest request, CancellationToken ct) =>
        MutateAsync(principal, id, request, async (plan, token) =>
        {
            if (plan.ClosedAt is not null) return;
            if (plan.CancelledAt is not null || plan.StartsAt > clock.UtcNow) throw new ConflictException("maintenance_not_active", "Maintenance is not active.");
            await db.AcquireMaintenanceAdmissionAsync(true, token);
            var result = await safety.CheckAsync(id, token);
            if (!result.Ready || string.IsNullOrWhiteSpace(result.EvidenceId) || result.EvidenceId.Length > 200)
                throw new ConflictException("maintenance_safety_not_configured", "Approved reopening safety evidence is required.");
            plan.ClosedAt = clock.UtcNow;
            plan.CloseSafetyEvidenceId = result.EvidenceId;
        }, "V1_MAINTENANCE_CLOSED", ct);

    internal async Task MutateAsync(ClaimsPrincipal principal, Guid id, MaintenanceActionRequest request,
        Func<V1MaintenancePlan, CancellationToken, Task> mutation, string action, CancellationToken ct)
    {
        var reason = InputBoundary.RequiredText(request.Reason, 1000, "Reason"); AdminAuditWriter.RequireReason(reason);
        var actor = await OperatorAsync(principal, request.SecurityPin, ct);
        try
        {
        await db.ExecuteInTransactionAsync(async token =>
        {
            await AdmitOperatorAsync(principal, actor, token); await AdmitAsync(token); await RequirePermissionAsync(principal, token);
            var plan = await db.V1MaintenancePlans.SingleOrDefaultAsync(p => p.Id == id, token)
                ?? throw new NotFoundException("maintenance_not_found", "Maintenance plan not found.");
            if (db is DbContext context) await context.Entry(plan).ReloadAsync(token);
            var before = (plan.CancelledAt, plan.ClosedAt, plan.ExpectedEndsAt, plan.StartsAt, plan.Scope, plan.ReviewedAt, plan.ReminderOffsetsJson, plan.ReminderPolicyVersion, plan.ReminderLateToleranceSeconds);
            await mutation(plan, token);
            if (before == (plan.CancelledAt, plan.ClosedAt, plan.ExpectedEndsAt, plan.StartsAt, plan.Scope, plan.ReviewedAt, plan.ReminderOffsetsJson, plan.ReminderPolicyVersion, plan.ReminderLateToleranceSeconds)) return true;
            plan.Revision++;
            audit.Add(actor, action, "V1MaintenancePlan", id.ToString(), reason, new { plan.Revision, plan.Scope, plan.CloseSafetyEvidenceId });
            if (action != "V1_MAINTENANCE_REVIEWED") await V1MaintenanceBroadcast.QueueAsync(db, clock, plan, action, actor, token);
            await db.SaveChangesAsync(token); return true;
        }, ct);
        }
        catch (AppException ex)
        {
            db.ClearTrackedEntities();
            audit.Add(actor, action + "_DENIED", "V1MaintenancePlan", id.ToString(), reason, new { Outcome = "Denied", ex.Code });
            await db.SaveChangesAsync(ct); throw;
        }
    }

    public Task ActivateDueAsync(CancellationToken ct) => db.ExecuteInTransactionAsync(async token =>
    {
        await AdmitAsync(token);
        var plans = await db.V1MaintenancePlans.AsNoTracking().Where(p => p.StartsAt <= clock.UtcNow && p.CancelledAt == null
            && p.ClosedAt == null && p.ActivatedAt == null).ToListAsync(token);
        foreach (var plan in plans)
        {
            if (await db.V1MaintenancePlans.Where(p => p.Id == plan.Id && p.ActivatedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.ActivatedAt, (DateTime?)clock.UtcNow), token) != 1) continue;
            audit.Add(plan.CreatedByUserId, "V1_MAINTENANCE_ACTIVATED", "V1MaintenancePlan", plan.Id.ToString(), plan.Reason,
                new { EffectiveAt = plan.StartsAt, ObservedAt = clock.UtcNow });
            await V1MaintenanceBroadcast.QueueAsync(db, clock, plan, "V1_MAINTENANCE_ACTIVATED", plan.CreatedByUserId, token);
        }
        await db.SaveChangesAsync(token); return true;
    }, ct);

    public async Task<IReadOnlyList<MaintenancePlanDto>> StatusAsync(CancellationToken ct)
    {
        var plans = await db.V1MaintenancePlans.AsNoTracking().Where(p => p.CancelledAt == null && p.ClosedAt == null)
            .OrderBy(p => p.StartsAt).Take(100).ToListAsync(ct);
        return plans.Select(Map).ToList();
    }
    private MaintenancePlanDto Map(V1MaintenancePlan p) => new(p.Id, p.StartsAt, p.ExpectedEndsAt,
        p.CancelledAt is not null ? "Cancelled" : p.ClosedAt is not null ? "Closed" : p.StartsAt <= clock.UtcNow ? "Active" : "Scheduled", p.Revision, p.Scope, p.IsEmergency);
    private async Task<Guid> OperatorAsync(ClaimsPrincipal principal, string pin, CancellationToken ct)
    {
        await RequirePermissionAsync(principal, ct); var actor = Guid.Parse(principal.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        await security.VerifyAsync(actor, pin, ct, SystemRole.SuperAdmin); return actor;
    }
    private async Task RequirePermissionAsync(ClaimsPrincipal principal, CancellationToken ct)
    {
        if (!await permissions.HasAsync(principal, PlatformPermission.ManageMaintenance, ct)) throw new ForbiddenException("permission_denied", "SuperAdmin permission required.");
    }
    private async Task AdmitAsync(CancellationToken ct)
    {
        if (await db.V1MaintenanceControls.Where(c => c.Id == 1).ExecuteUpdateAsync(s => s.SetProperty(c => c.Revision, c => c.Revision + 1), ct) != 1)
            throw new ConflictException("maintenance_coordinator_missing", "Maintenance coordinator is unavailable.");
    }
    private async Task AdmitOperatorAsync(ClaimsPrincipal principal, Guid actor, CancellationToken ct)
    {
        if (!await db.AcquireGroupAdmissionLockAsync(actor, ct)) throw new ForbiddenException("permission_denied", "Operator unavailable.");
        var session = Guid.Parse(principal.FindFirst("sid")!.Value);
        if (await db.UserSessions.Where(s => s.Id == session && s.UserId == actor && s.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(s => s.RevokedAt, s => s.RevokedAt), ct) != 1)
            throw new ForbiddenException("permission_denied", "Operator session revoked.");
    }
}
