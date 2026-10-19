using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Notifications;
using SpendWise.Domain.Admin;
using SpendWise.Domain.MobilePush;
using SpendWise.Domain.Notifications;

namespace SpendWise.Application.Features.Operations;

public sealed partial class V1BroadcastService
{
    public async Task<int> ProcessDueAsync(CancellationToken ct)
    {
        var ids = await db.V1Broadcasts.AsNoTracking().Where(b => (b.State == "Scheduled" || b.State == "Dispatching") && b.ScheduledFor <= clock.UtcNow)
            .OrderBy(b => b.ScheduledFor).Take(50).Select(b => b.Id).ToListAsync(ct);
        var total = 0;
        foreach (var id in ids)
        {
            total += await db.ExecuteInTransactionAsync(async token =>
            {
                var b = await LockAsync(id, token);
                if (b.State is not ("Scheduled" or "Dispatching") || b.ScheduledFor > clock.UtcNow) return 0;
                b.AudienceCutoffAt ??= clock.UtcNow;
                var key = "broadcast:" + id.ToString("N");
                var recipients = await Recipients(b).Where(u => !db.NotificationDeduplications.Any(d => d.UserId == u.Id && d.Key == key))
                    .OrderBy(u => u.Id).Take(500).Select(u => u.Id).ToListAsync(token);
                var persisted = await db.NotificationDeduplications.CountAsync(d => d.Key == key, token);
                if (b.RecipientLimit is { } limit && persisted + recipients.Count > limit)
                { b.State = "RecipientLimitExceeded"; await db.SaveChangesAsync(token); return 0; }
                var notices = await notifications.CreateManyAsync(recipients.Select(user => new CreateNotificationRequest(user,
                    b.MaintenancePlanId is not null ? b.Type == "Emergency" ? "EmergencyAnnouncement" : "MaintenanceChanged" : "SystemAnnouncement",
                    b.Title, b.Message, new { BroadcastId = id, b.MaintenancePlanId, b.MaintenanceRevision })).ToList(), token);
                var notificationIds = notices.Select(n => n.Id).ToArray();
                var links = await db.Notifications.AsNoTracking().Where(n => notificationIds.Contains(n.Id)).Select(n => new { n.Id, n.UserId }).ToListAsync(token);
                if (links.Count != recipients.Count || !links.Select(n => n.UserId).ToHashSet().SetEquals(recipients)) throw new InvalidOperationException("Notification batch result is inconsistent.");
                db.NotificationDeduplications.AddRange(links.Select(n => new NotificationDeduplication { UserId = n.UserId, Key = key, NotificationId = n.Id }));
                b.State = "Dispatching"; await db.SaveChangesAsync(token);
                if (!await Recipients(b).AnyAsync(u => !db.NotificationDeduplications.Any(d => d.UserId == u.Id && d.Key == key), token))
                {
                    b.State = "Published"; b.PublishedAt = clock.UtcNow;
                    db.AuditLogs.Add(new AuditLog { Action = "V1_BROADCAST_PERSISTED", TargetType = "V1Broadcast", TargetId = id.ToString(),
                        Reason = "Authorized scheduled notice persisted; provider delivery and user read are separate evidence.", MetadataJson = System.Text.Json.JsonSerializer.Serialize(new { ScheduledBy = b.CreatedByUserId, Persisted = persisted + recipients.Count }) });
                }
                await db.SaveChangesAsync(token); return recipients.Count;
            }, ct);
        }
        return total;
    }

    public async Task<V1BroadcastStatus> StatusAsync(System.Security.Claims.ClaimsPrincipal principal, Guid id, string reason, CancellationToken ct)
    {
        var actor = await OperatorAsync(principal, null, ct); var purpose = Reason(reason);
        return await db.ExecuteInTransactionAsync(async token =>
        {
            await AdmitAsync(principal, actor, token); var b = await LockAsync(id, token); var key = "broadcast:" + id.ToString("N");
            var links = db.NotificationDeduplications.AsNoTracking().Where(d => d.Key == key);
            var notificationIds = links.Select(d => d.NotificationId);
            var persisted = await links.CountAsync(token);
            var read = await db.Notifications.CountAsync(n => notificationIds.Contains(n.Id) && n.ReadAt != null, token);
            var retained = await db.Notifications.CountAsync(n => notificationIds.Contains(n.Id), token);
            var push = db.MobilePushDeliveries.AsNoTracking().Where(d => notificationIds.Contains(d.NotificationId));
            var result = new V1BroadcastStatus(id, b.State, persisted, read, await push.CountAsync(d => d.Status == MobilePushDeliveryStatus.Submitted, token),
                await push.CountAsync(d => d.Status == MobilePushDeliveryStatus.Completed, token), await push.CountAsync(d => d.Status == MobilePushDeliveryStatus.Failed, token), persisted - retained);
            audit.Add(actor, "V1_BROADCAST_STATUS_READ", "V1Broadcast", id.ToString(), purpose, new { b.State, Persisted = persisted }); await db.SaveChangesAsync(token); return result;
        }, ct);
    }
}
