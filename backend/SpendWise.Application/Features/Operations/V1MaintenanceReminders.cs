using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;

namespace SpendWise.Application.Features.Operations;

public sealed class V1ReminderPolicy
{
    public string? Version { get; set; }
    public int? MaximumCount { get; set; }
    public int? MinimumSpacingMinutes { get; set; }
    public int? LateToleranceSeconds { get; set; }
    public int[]? DefaultMinutesBefore { get; set; }
}
public sealed record MaintenanceReminderRequest(int[]? MinutesBefore, long ExpectedRevision, string SecurityPin, string Reason);

public sealed class V1MaintenanceReminders(ISpendWiseDbContext db, IClock clock, V1MaintenanceService maintenance, IOptions<V1ReminderPolicy> policy)
{
    public Task ConfigureAsync(ClaimsPrincipal principal, Guid id, MaintenanceReminderRequest request, CancellationToken ct)
    {
        var p = policy.Value;
        if (string.IsNullOrWhiteSpace(p.Version) || p.Version.Length > 100 || p.MaximumCount is null or <= 0 || p.MinimumSpacingMinutes is null or <= 0 || p.LateToleranceSeconds is null or <= 0)
            throw new ConflictException("REMINDER_POLICY_BLOCKED", "Approved reminder count, spacing and late-delivery policy are required.");
        var offsets = (request.MinutesBefore ?? p.DefaultMinutesBefore ?? throw new ConflictException("REMINDER_PRESET_BLOCKED", "No approved default preset exists.")).Order().ToArray();
        if (offsets.Length > p.MaximumCount || offsets.Any(n => n <= 0) || offsets.Zip(offsets.Skip(1), (a, b) => b - a).Any(gap => gap < p.MinimumSpacingMinutes))
            throw new AppException("invalid_reminder_schedule", "Reminder count, uniqueness or spacing violates policy.");
        var json = JsonSerializer.Serialize(offsets);
        if (json.Length > 4000) throw new AppException("invalid_reminder_schedule", "Reminder schedule is too large.");
        return maintenance.MutateAsync(principal, id, new(request.SecurityPin, request.Reason), (plan, token) =>
        {
            if (plan.Revision != request.ExpectedRevision) throw new ConflictException("stale_maintenance_revision", "Maintenance changed.");
            if (plan.StartsAt <= clock.UtcNow || plan.ClosedAt is not null || plan.CancelledAt is not null) throw new ConflictException("maintenance_already_started", "Configure reminders before activation.");
            if (offsets.Any(n => (long)n * TimeSpan.TicksPerMinute > plan.StartsAt.Ticks)) throw new AppException("invalid_reminder_schedule", "Reminder offset is outside the supported date range.");
            if (plan.ReminderOffsetsJson == json && plan.ReminderPolicyVersion == p.Version && plan.ReminderLateToleranceSeconds == p.LateToleranceSeconds) return Task.CompletedTask;
            plan.ReminderOffsetsJson = json; plan.RemindersConfiguredAt = clock.UtcNow;
            plan.ReminderPolicyVersion = p.Version; plan.ReminderLateToleranceSeconds = p.LateToleranceSeconds; return Task.CompletedTask;
        }, "V1_MAINTENANCE_REMINDERS_CONFIGURED", ct);
    }

    public Task ProcessAsync(CancellationToken ct)
    {
        return db.ExecuteInTransactionAsync(async token =>
        {
            if (await db.V1MaintenanceControls.Where(c => c.Id == 1).ExecuteUpdateAsync(s => s.SetProperty(c => c.Revision, c => c.Revision + 1), token) != 1)
                throw new ConflictException("maintenance_coordinator_missing", "Maintenance coordinator is unavailable.");
            var now = clock.UtcNow;
            var plans = await db.V1MaintenancePlans.AsNoTracking().Where(x => x.CancelledAt == null && x.ClosedAt == null && x.StartsAt > now && x.RemindersConfiguredAt != null).ToListAsync(token);
            foreach (var plan in plans)
            {
                if (string.IsNullOrWhiteSpace(plan.ReminderPolicyVersion) || plan.ReminderLateToleranceSeconds is null or <= 0) continue;
                var offsets = JsonSerializer.Deserialize<int[]>(plan.ReminderOffsetsJson) ?? [];
                var due = offsets.Select(offset => new { Offset = offset, At = plan.StartsAt.AddMinutes(-offset) })
                    .Where(x => x.At > plan.RemindersConfiguredAt && x.At <= now && now - x.At <= TimeSpan.FromSeconds(plan.ReminderLateToleranceSeconds.Value)).OrderByDescending(x => x.At).FirstOrDefault();
                if (due is not null) await V1MaintenanceBroadcast.QueueAsync(db, clock, plan, "V1_MAINTENANCE_REMINDER:" + due.Offset, plan.CreatedByUserId, token);
            }
            await db.SaveChangesAsync(token); return true;
        }, ct);
    }
}
