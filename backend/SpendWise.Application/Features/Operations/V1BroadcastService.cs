using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Admin;
using SpendWise.Application.Features.Administration;
using SpendWise.Application.Notifications;
using SpendWise.Domain.Admin;
using SpendWise.Domain.Identity;

namespace SpendWise.Application.Features.Operations;

public sealed class V1BroadcastPolicy
{
    public string? Version { get; set; }
    public int? MaxRecipients { get; set; }
    public int? MinSpacingSeconds { get; set; }
}
public sealed record BroadcastDraftRequest(string Type, string TargetRole, string Title, string Message, string IdempotencyKey, string SecurityPin, string Reason);
public sealed record BroadcastPublishRequest(DateTime? ScheduledFor, long ExpectedRevision, string SecurityPin, string Reason);
public sealed record V1BroadcastDto(Guid Id, string State, long Revision, string Type, string TargetRole, string Title, string Message, DateTime? ScheduledFor);
public sealed record V1BroadcastStatus(Guid Id, string State, int Persisted, int Read, int PushSubmitted, int PushCompleted, int PushFailed, int ReadUnknown = 0);

public sealed partial class V1BroadcastService(ISpendWiseDbContext db, IClock clock, PlatformPermissionService permissions,
    AdminSecurityGuard security, AdminAuditWriter audit, INotificationService notifications, IOptions<V1BroadcastPolicy> policy)
{
    public async Task<V1BroadcastDto> DraftAsync(ClaimsPrincipal principal, BroadcastDraftRequest request, CancellationToken ct)
    {
        var title = InputBoundary.RequiredText(request.Title, 200, "Title"); var message = InputBoundary.RequiredText(request.Message, 1000, "Message");
        var key = InputBoundary.RequiredText(request.IdempotencyKey, 100, "Idempotency key"); var reason = Reason(request.Reason);
        if (request.Type != "Operational" || request.TargetRole is not ("User" or "Admin"))
            throw new ConflictException("broadcast_contract_required", "Only operational notices with explicit supported role targeting are available. Promotions require an approved program; emergency notices require a verified incident source.");
        var actor = await OperatorAsync(principal, request.SecurityPin, ct);
        return await db.ExecuteInTransactionAsync(async token =>
        {
            await AdmitAsync(principal, actor, token); var eventKey = $"manual:{actor:N}:{key}";
            var existing = await db.V1Broadcasts.AsNoTracking().SingleOrDefaultAsync(b => b.EventKey == eventKey, token);
            if (existing is not null)
            {
                if (existing.Title != title || existing.Message != message || existing.Type != request.Type || existing.TargetRole != request.TargetRole)
                    throw new ConflictException("idempotency_conflict", "Broadcast intent differs.");
                return Map(existing);
            }
            var draft = new V1Broadcast { CreatedByUserId = actor, EventKey = eventKey, Type = request.Type, TargetRole = request.TargetRole, Title = title, Message = message };
            db.V1Broadcasts.Add(draft); audit.Add(actor, "V1_BROADCAST_DRAFTED", "V1Broadcast", draft.Id.ToString(), reason, new { draft.Type, draft.TargetRole });
            await db.SaveChangesAsync(token); return Map(draft);
        }, ct);
    }

    public async Task PublishAsync(ClaimsPrincipal principal, Guid id, BroadcastPublishRequest request, CancellationToken ct)
    {
        var reason = Reason(request.Reason); var actor = await OperatorAsync(principal, request.SecurityPin, ct); var p = policy.Value;
        if (string.IsNullOrWhiteSpace(p.Version) || p.MaxRecipients is null or <= 0 || p.MinSpacingSeconds is null or <= 0)
            throw new ConflictException("BROADCAST_POLICY_BLOCKED", "Approved recipient and anti-spam policy is required.");
        if (request.ScheduledFor is { } scheduled && (scheduled.Kind != DateTimeKind.Utc || scheduled < clock.UtcNow)) throw new AppException("invalid_broadcast_time", "Use a future UTC schedule, or omit it to send now.");
        var isImmediate = request.ScheduledFor is null || request.ScheduledFor <= clock.UtcNow;
        await db.ExecuteInTransactionAsync(async token =>
        {
            await AdmitAsync(principal, actor, token); var b = await LockAsync(id, token);
            if (b.State != "Draft" || b.Revision != request.ExpectedRevision || b.MaintenancePlanId is not null) throw new ConflictException("broadcast_immutable", "Only the current manual draft may be published.");
            var observed = clock.UtcNow; var when = request.ScheduledFor ?? observed;
            if (when < observed) throw new ConflictException("broadcast_time_elapsed", "Schedule elapsed during admission.");
            var lo = when.AddSeconds(-p.MinSpacingSeconds.Value); var hi = when.AddSeconds(p.MinSpacingSeconds.Value);
            if (await db.V1Broadcasts.AnyAsync(x => x.Id != id && x.CreatedByUserId == actor && x.MaintenancePlanId == null
                && x.State != "Cancelled" && x.ScheduledFor > lo && x.ScheduledFor < hi, token)) throw new ConflictException("broadcast_cooldown", "Notices are too closely scheduled.");
            b.AudienceCutoffAt = clock.UtcNow;
            if (await Recipients(b).CountAsync(token) > p.MaxRecipients.Value) throw new ConflictException("broadcast_recipient_limit", "Audience exceeds approved policy.");
            b.State = "Scheduled"; b.ScheduledFor = when; b.RecipientLimit = p.MaxRecipients; b.Revision++;
            audit.Add(actor, "V1_BROADCAST_SCHEDULED", "V1Broadcast", id.ToString(), reason, new { b.ScheduledFor, b.TargetRole, Policy = p.Version });
            await db.SaveChangesAsync(token); return true;
        }, ct);
        if (isImmediate)
        {
            await ProcessDueAsync(ct);
        }
    }

    public async Task CancelAsync(ClaimsPrincipal principal, Guid id, MaintenanceActionRequest request, CancellationToken ct)
    {
        var actor = await OperatorAsync(principal, request.SecurityPin, ct); var reason = Reason(request.Reason);
        await db.ExecuteInTransactionAsync(async token =>
        {
            await AdmitAsync(principal, actor, token); var b = await LockAsync(id, token);
            if (b.State == "Cancelled") return true;
            if (b.MaintenancePlanId is not null || b.State is not ("Draft" or "Scheduled")) throw new ConflictException("broadcast_immutable", "Published or started notices cannot be rewritten or cancelled.");
            b.State = "Cancelled"; b.Revision++; audit.Add(actor, "V1_BROADCAST_CANCELLED", "V1Broadcast", id.ToString(), reason, null);
            await db.SaveChangesAsync(token); return true;
        }, ct);
    }

    private IQueryable<User> Recipients(V1Broadcast b)
    {
        var role = Enum.Parse<SystemRole>(b.TargetRole);
        return db.Users.AsNoTracking().Where(u => u.SystemRole == role && u.Status == UserStatus.Active && u.EmailVerifiedAt != null && u.CreatedAt <= b.AudienceCutoffAt);
    }
    private async Task<Guid> OperatorAsync(ClaimsPrincipal principal, string? pin, CancellationToken ct)
    {
        if (!await permissions.HasAsync(principal, PlatformPermission.ManageMaintenance, ct)) throw new ForbiddenException("permission_denied", "Administrator maintenance permission required.");
        var actor = Guid.Parse(principal.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        if (pin is not null) await security.VerifyAsync(actor, pin, ct, SystemRole.Admin); return actor;
    }
    private async Task AdmitAsync(ClaimsPrincipal principal, Guid actor, CancellationToken ct)
    {
        if (!await db.AcquireGroupAdmissionLockAsync(actor, ct)) throw new ForbiddenException();
        var session = Guid.Parse(principal.FindFirst("sid")!.Value);
        if (await db.UserSessions.Where(s => s.Id == session && s.UserId == actor && s.RevokedAt == null).ExecuteUpdateAsync(s => s.SetProperty(s => s.RevokedAt, s => s.RevokedAt), ct) != 1
            || !await permissions.HasAsync(principal, PlatformPermission.ManageMaintenance, ct)) throw new ForbiddenException();
    }
    private async Task<V1Broadcast> LockAsync(Guid id, CancellationToken ct)
    {
        if (await db.V1Broadcasts.Where(b => b.Id == id).ExecuteUpdateAsync(s => s.SetProperty(b => b.Revision, b => b.Revision), ct) != 1) throw new NotFoundException("broadcast_not_found", "Notice not found.");
        var b = await db.V1Broadcasts.SingleAsync(b => b.Id == id, ct); if (db is DbContext context) await context.Entry(b).ReloadAsync(ct); return b;
    }
    private static string Reason(string reason) { var value = InputBoundary.RequiredText(reason, 1000, "Reason"); AdminAuditWriter.RequireReason(value); return value; }
    private static V1BroadcastDto Map(V1Broadcast b) => new(b.Id, b.State, b.Revision, b.Type, b.TargetRole, b.Title, b.Message, b.ScheduledFor);
}
