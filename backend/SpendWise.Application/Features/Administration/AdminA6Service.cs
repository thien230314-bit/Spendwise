using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Admin;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Admin;
using SpendWise.Domain.Admin;
using SpendWise.Domain.Identity;
using SpendWise.Domain.MobilePush;
using SpendWise.Domain.Notifications;

namespace SpendWise.Application.Features.Administration;

public sealed class AdminA6Service(
    ISpendWiseDbContext db,
    PlatformPermissionService permissions,
    AdminAuditWriter audit,
    IClock clock)
{
    // A6.1 - Thông báo nghiệp vụ
    public async Task<A2Page<A6NotificationItem>> NotificationsAsync(ClaimsPrincipal principal, A6NotificationQuery query, CancellationToken ct)
    {
        var purpose = InputBoundary.RequiredText(query.Reason, 1000, "Reason");
        AdminAuditWriter.RequireReason(purpose);
        Validate(query.Page, query.PageSize, query.FromUtc, query.ToUtc);

        if (!await permissions.HasAsync(principal, PlatformPermission.OperationsRead, ct))
            throw new ForbiddenException("permission_denied", "Operator required.");

        var actor = Guid.Parse(principal.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        Guid? scopedUserId = null;

        if (query.CaseId is { } caseId)
        {
            var ticket = await db.SupportCases.AsNoTracking().SingleOrDefaultAsync(c => c.Id == caseId, ct)
                ?? throw new NotFoundException("case_not_found", "Case not found.");
            scopedUserId = ticket.LinkedUserId;
        }
        else if (query.UserId is { } uid)
        {
            scopedUserId = uid;
        }

        var q = db.Notifications.AsNoTracking();

        if (scopedUserId is { } targetUid)
        {
            q = q.Where(n => n.UserId == targetUid);
        }

        if (!string.IsNullOrWhiteSpace(query.Type) && Enum.TryParse<NotificationType>(query.Type, true, out var parsedType))
        {
            q = q.Where(n => n.Type == parsedType);
        }

        if (query.FromUtc is { } f)
            q = q.Where(n => n.CreatedAt >= f);
        if (query.ToUtc is { } t)
            q = q.Where(n => n.CreatedAt < t);

        var total = await q.CountAsync(ct);

        var rawItems = await q.OrderByDescending(n => n.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        var userIds = rawItems.Select(n => n.UserId).Distinct().ToList();
        var userMap = await db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => new { u.FullName, u.SpendId }, ct);

        var items = rawItems.Select(n =>
        {
            userMap.TryGetValue(n.UserId, out var uInfo);
            return new A6NotificationItem(
                n.Id,
                n.UserId,
                uInfo?.FullName,
                uInfo?.SpendId,
                n.Type.ToString(),
                n.Title,
                SanitizeContent(n.Message) ?? string.Empty,
                SanitizeContent(n.DataJson),
                n.ReadAt != null,
                n.ReadAt != null ? DateTime.SpecifyKind(n.ReadAt.Value, DateTimeKind.Utc) : null,
                DateTime.SpecifyKind(n.CreatedAt, DateTimeKind.Utc)
            );
        }).ToList();

        audit.Add(actor, "A6_NOTIFICATIONS_SEARCH", "Notification", scopedUserId?.ToString(), purpose,
            new { Count = items.Count, Page = query.Page, Total = total, CaseId = query.CaseId, UserId = scopedUserId });
        await db.SaveChangesAsync(ct);

        return new A2Page<A6NotificationItem>(clock.UtcNow, query.Page, query.PageSize, total, items);
    }

    // A6.2 - Lịch sử gửi (Delivery History)
    public async Task<A2Page<A6DeliveryHistoryItem>> DeliveryHistoryAsync(ClaimsPrincipal principal, A6DeliveryHistoryQuery query, CancellationToken ct)
    {
        var purpose = InputBoundary.RequiredText(query.Reason, 1000, "Reason");
        AdminAuditWriter.RequireReason(purpose);
        Validate(query.Page, query.PageSize, query.FromUtc, query.ToUtc);

        if (!await permissions.HasAsync(principal, PlatformPermission.OperationsRead, ct))
            throw new ForbiddenException("permission_denied", "Operator required.");

        var actor = Guid.Parse(principal.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        Guid? scopedUserId = null;

        if (query.CaseId is { } caseId)
        {
            var ticket = await db.SupportCases.AsNoTracking().SingleOrDefaultAsync(c => c.Id == caseId, ct)
                ?? throw new NotFoundException("case_not_found", "Case not found.");
            scopedUserId = ticket.LinkedUserId;
        }
        else if (query.UserId is { } uid)
        {
            scopedUserId = uid;
        }

        var unifiedDeliveries = new List<A6DeliveryHistoryItem>();

        // 1. In-App Notifications
        if (string.IsNullOrWhiteSpace(query.Channel) || query.Channel.Equals("InApp", StringComparison.OrdinalIgnoreCase))
        {
            var notifQ = db.Notifications.AsNoTracking();
            if (scopedUserId is { } targetUid)
                notifQ = notifQ.Where(n => n.UserId == targetUid);
            if (query.FromUtc is { } f)
                notifQ = notifQ.Where(n => n.CreatedAt >= f);
            if (query.ToUtc is { } t)
                notifQ = notifQ.Where(n => n.CreatedAt < t);

            var limit = Math.Max(200, query.Page * query.PageSize);
            var notifs = await notifQ.OrderByDescending(n => n.CreatedAt).Take(limit).ToListAsync(ct);
            var notifUserIds = notifs.Select(n => n.UserId).Distinct().ToList();
            var notifUsers = await db.Users.AsNoTracking().Where(u => notifUserIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.SpendId, ct);

            foreach (var n in notifs)
            {
                var status = n.ReadAt != null ? "UserRead" : "Sent";
                if (!string.IsNullOrWhiteSpace(query.Status) && !status.Equals(query.Status, StringComparison.OrdinalIgnoreCase))
                    continue;

                notifUsers.TryGetValue(n.UserId, out var spendId);
                unifiedDeliveries.Add(new A6DeliveryHistoryItem(
                    n.Id,
                    n.UserId,
                    "InApp",
                    n.Type.ToString(),
                    MaskSpendId(spendId ?? n.UserId.ToString()),
                    status,
                    1,
                    DateTime.SpecifyKind(n.CreatedAt, DateTimeKind.Utc),
                    DateTime.SpecifyKind(n.CreatedAt, DateTimeKind.Utc),
                    n.ReadAt != null ? DateTime.SpecifyKind(n.ReadAt.Value, DateTimeKind.Utc) : null,
                    null
                ));
            }
        }

        // 2. Email Outbox Deliveries
        if (string.IsNullOrWhiteSpace(query.Channel) || query.Channel.Equals("Email", StringComparison.OrdinalIgnoreCase))
        {
            var emailQ = db.EmailOutboxMessages.AsNoTracking();
            if (query.FromUtc is { } f)
                emailQ = emailQ.Where(e => e.CreatedAt >= f);
            if (query.ToUtc is { } t)
                emailQ = emailQ.Where(e => e.CreatedAt < t);

            string? targetEmail = null;
            if (scopedUserId is { } targetUid)
            {
                var targetUser = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == targetUid, ct);
                targetEmail = targetUser?.Email;
                if (!string.IsNullOrEmpty(targetEmail))
                {
                    emailQ = emailQ.Where(e => e.RecipientEmail == targetEmail);
                }
            }

            // Only query if not scoped or scoped user had an email
            if (scopedUserId == null || !string.IsNullOrEmpty(targetEmail))
            {
                var emailLimit = Math.Max(200, query.Page * query.PageSize);
                var emails = await emailQ.OrderByDescending(e => e.CreatedAt).Take(emailLimit).ToListAsync(ct);
                foreach (var e in emails)
                {
                    var status = e.Status switch
                    {
                        EmailDeliveryStatus.Sent => "Sent",
                        EmailDeliveryStatus.Failed => "Failed",
                        EmailDeliveryStatus.Processing => "Processing",
                        _ => "Queued"
                    };
                    if (!string.IsNullOrWhiteSpace(query.Status) && !status.Equals(query.Status, StringComparison.OrdinalIgnoreCase))
                        continue;

                    unifiedDeliveries.Add(new A6DeliveryHistoryItem(
                        e.Id,
                        scopedUserId ?? Guid.Empty,
                        "Email",
                        SanitizeSubject(e.Subject),
                        MaskEmail(e.RecipientEmail),
                        status,
                        e.AttemptCount,
                        DateTime.SpecifyKind(e.CreatedAt, DateTimeKind.Utc),
                        e.SentAt != null ? DateTime.SpecifyKind(e.SentAt.Value, DateTimeKind.Utc) : null,
                        null,
                        SanitizeContent(e.LastError)
                    ));
                }
            }
        }

        // 3. Mobile Push Deliveries
        if (string.IsNullOrWhiteSpace(query.Channel) || query.Channel.Equals("Push", StringComparison.OrdinalIgnoreCase))
        {
            var pushQ = db.MobilePushDeliveries.AsNoTracking().Include(p => p.Notification).Include(p => p.Installation);
            IQueryable<MobilePushDelivery> filteredPush = pushQ;

            if (scopedUserId is { } targetUid)
                filteredPush = filteredPush.Where(p => p.Notification.UserId == targetUid);
            if (query.FromUtc is { } f)
                filteredPush = filteredPush.Where(p => p.CreatedAt >= f);
            if (query.ToUtc is { } t)
                filteredPush = filteredPush.Where(p => p.CreatedAt < t);

            var pushLimit = Math.Max(200, query.Page * query.PageSize);
            var pushes = await filteredPush.OrderByDescending(p => p.CreatedAt).Take(pushLimit).ToListAsync(ct);
            foreach (var p in pushes)
            {
                var status = p.Status switch
                {
                    MobilePushDeliveryStatus.Completed => "ProviderAccepted",
                    MobilePushDeliveryStatus.Submitted => "Submitted",
                    MobilePushDeliveryStatus.Failed => "Failed",
                    _ => "Pending"
                };
                if (!string.IsNullOrWhiteSpace(query.Status) && !status.Equals(query.Status, StringComparison.OrdinalIgnoreCase))
                    continue;

                unifiedDeliveries.Add(new A6DeliveryHistoryItem(
                    p.Id,
                    p.Notification?.UserId ?? Guid.Empty,
                    "Push",
                    p.Notification?.Type.ToString() ?? "MobilePush",
                    MaskPushToken(p.Installation?.PushToken),
                    status,
                    p.AttemptCount,
                    DateTime.SpecifyKind(p.CreatedAt, DateTimeKind.Utc),
                    p.SubmittedAt != null ? DateTime.SpecifyKind(p.SubmittedAt.Value, DateTimeKind.Utc) : null,
                    null,
                    SanitizeContent(p.LastError)
                ));
            }
        }

        var sorted = unifiedDeliveries.OrderByDescending(d => d.CreatedAt).ToList();
        var total = sorted.Count;
        var paged = sorted.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToList();

        audit.Add(actor, "A6_DELIVERY_HISTORY_READ", "DeliveryHistory", scopedUserId?.ToString(), purpose,
            new { Count = paged.Count, Total = total, Page = query.Page, ScopedUser = scopedUserId });
        await db.SaveChangesAsync(ct);

        return new A2Page<A6DeliveryHistoryItem>(clock.UtcNow, query.Page, query.PageSize, total, paged);
    }

    // A6.3 - Lỗi gửi thông báo (Failed Deliveries Feed)
    public async Task<A2Page<A6DeliveryFailureItem>> DeliveryFailuresAsync(ClaimsPrincipal principal, A6DeliveryFailureQuery query, CancellationToken ct)
    {
        var purpose = InputBoundary.RequiredText(query.Reason, 1000, "Reason");
        AdminAuditWriter.RequireReason(purpose);
        Validate(query.Page, query.PageSize, query.FromUtc, query.ToUtc);

        if (!await permissions.HasAsync(principal, PlatformPermission.OperationsRead, ct))
            throw new ForbiddenException("permission_denied", "Operator required.");

        var actor = Guid.Parse(principal.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var failures = new List<A6DeliveryFailureItem>();

        // 1. Failed Emails
        if (string.IsNullOrWhiteSpace(query.Channel) || query.Channel.Equals("Email", StringComparison.OrdinalIgnoreCase))
        {
            var emailQ = db.EmailOutboxMessages.AsNoTracking().Where(e => e.Status == EmailDeliveryStatus.Failed);
            if (query.FromUtc is { } f) emailQ = emailQ.Where(e => e.CreatedAt >= f);
            if (query.ToUtc is { } t) emailQ = emailQ.Where(e => e.CreatedAt < t);

            var emailLimit = Math.Max(200, query.Page * query.PageSize);
            var failedEmails = await emailQ.OrderByDescending(e => e.CreatedAt).Take(emailLimit).ToListAsync(ct);
            foreach (var e in failedEmails)
            {
                failures.Add(new A6DeliveryFailureItem(
                    e.Id,
                    "Email",
                    MaskEmail(e.RecipientEmail),
                    SanitizeSubject(e.Subject),
                    e.AttemptCount,
                    e.NextAttemptAt != null ? DateTime.SpecifyKind(e.NextAttemptAt.Value, DateTimeKind.Utc) : null,
                    DateTime.SpecifyKind(e.CreatedAt, DateTimeKind.Utc),
                    SanitizeContent(e.LastError)
                ));
            }
        }

        // 2. Failed Pushes
        if (string.IsNullOrWhiteSpace(query.Channel) || query.Channel.Equals("Push", StringComparison.OrdinalIgnoreCase))
        {
            var pushQ = db.MobilePushDeliveries.AsNoTracking()
                .Include(p => p.Notification)
                .Include(p => p.Installation)
                .Where(p => p.Status == MobilePushDeliveryStatus.Failed);
            if (query.FromUtc is { } f) pushQ = pushQ.Where(p => p.CreatedAt >= f);
            if (query.ToUtc is { } t) pushQ = pushQ.Where(p => p.CreatedAt < t);

            var pushLimit = Math.Max(200, query.Page * query.PageSize);
            var failedPushes = await pushQ.OrderByDescending(p => p.CreatedAt).Take(pushLimit).ToListAsync(ct);
            foreach (var p in failedPushes)
            {
                failures.Add(new A6DeliveryFailureItem(
                    p.Id,
                    "Push",
                    MaskPushToken(p.Installation?.PushToken),
                    p.Notification?.Type.ToString() ?? "MobilePush",
                    p.AttemptCount,
                    p.NextAttemptAt != null ? DateTime.SpecifyKind(p.NextAttemptAt.Value, DateTimeKind.Utc) : null,
                    DateTime.SpecifyKind(p.CreatedAt, DateTimeKind.Utc),
                    SanitizeContent(p.LastError)
                ));
            }
        }

        var sorted = failures.OrderByDescending(f => f.CreatedAt).ToList();
        var total = sorted.Count;
        var paged = sorted.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToList();

        audit.Add(actor, "A6_DELIVERY_FAILURES_READ", "DeliveryFailure", null, purpose,
            new { Count = paged.Count, Total = total, Page = query.Page });
        await db.SaveChangesAsync(ct);

        return new A2Page<A6DeliveryFailureItem>(clock.UtcNow, query.Page, query.PageSize, total, paged);
    }

    // A6.4 - Danh mục mẫu thông báo (Read-only catalog)
    public async Task<IReadOnlyList<A6NotificationTemplateItem>> GetTemplatesAsync(ClaimsPrincipal principal, string reason, CancellationToken ct)
    {
        var purpose = InputBoundary.RequiredText(reason, 1000, "Reason");
        AdminAuditWriter.RequireReason(purpose);

        if (!await permissions.HasAsync(principal, PlatformPermission.OperationsRead, ct))
            throw new ForbiddenException("permission_denied", "Operator required.");

        var actor = Guid.Parse(principal.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        IReadOnlyList<A6NotificationTemplateItem> catalog =
        [
            new("support-info-request", "Yêu cầu bổ sung sao kê/chứng từ", "Support",
                "Yêu cầu người dùng cung cấp thêm hóa đơn hoặc sao kê ngân hàng đối chiếu.",
                ["TicketNumber", "Subject", "RequiredDocuments", "Note"]),

            new("support-resolved", "Thông báo đã giải quyết khiếu nại", "Support",
                "Thông báo kết quả giải quyết hoàn tất cho người dùng.",
                ["TicketNumber", "Message", "ActionUrl"]),

            new("support-rejected", "Thông báo từ chối yêu cầu hỗ trợ", "Support",
                "Thông báo lý do từ chối hỗ trợ theo chính sách hệ thống.",
                ["TicketNumber", "Message", "ActionUrl"]),

            new("support-fund-investigation", "Thông báo tiến độ điều tra quỹ", "FundInvestigation",
                "Cập nhật người dùng về việc hồ sơ quỹ đang trong quá trình rà soát đối chiếu sổ cái.",
                ["TicketNumber", "FundName", "CurrentPhase", "Note"]),

            new("transaction-received", "Thông báo nhận tiền vào ví", "Transaction",
                "Thông báo giao dịch ghi nhận số dư cộng vào ví của người dùng.",
                ["Amount", "Currency", "SenderName", "WalletName"]),

            new("transaction-sent", "Thông báo chuyển tiền thành công", "Transaction",
                "Thông báo hoàn tất chuyển tiền từ ví người dùng.",
                ["Amount", "Currency", "RecipientName", "WalletName"]),

            new("maintenance-announcement", "Thông báo lịch bảo trì hệ thống", "System",
                "Thông báo lịch bảo trì hệ thống tới người dùng.",
                ["StartsAt", "ExpectedEndsAt", "Scope", "Reason"]),

            new("security-alert", "Cảnh báo bảo mật tài khoản", "Security",
                "Thông báo khi phát hiện đăng nhập từ thiết bị lạ hoặc hoạt động bất thường.",
                ["DeviceName", "LoginTime", "IpAddressMasked", "ActionUrl"])
        ];

        audit.Add(actor, "A6_TEMPLATES_CATALOG_READ", "NotificationTemplate", null, purpose, new { Count = catalog.Count });
        await db.SaveChangesAsync(ct);

        return catalog;
    }

    // A6.4 - Tổng quan trạng thái bảo trì (Read-only maintenance view)
    public async Task<A6MaintenanceOverviewResult> GetMaintenanceOverviewAsync(ClaimsPrincipal principal, string reason, CancellationToken ct)
    {
        var purpose = InputBoundary.RequiredText(reason, 1000, "Reason");
        AdminAuditWriter.RequireReason(purpose);

        if (!await permissions.HasAsync(principal, PlatformPermission.OperationsRead, ct))
            throw new ForbiddenException("permission_denied", "Operator required.");

        var actor = Guid.Parse(principal.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var now = clock.UtcNow;

        // 1. SystemMaintenanceState flag
        var sysMaintenance = await db.SystemMaintenanceStates.AsNoTracking().FirstOrDefaultAsync(ct);
        var isSystemMaintenanceActive = sysMaintenance?.IsEnabled ?? false;

        // 2. Active Plans: Plans that have started, not cancelled, and not closed
        var activePlansRaw = await db.V1MaintenancePlans.AsNoTracking()
            .Where(p => p.CancelledAt == null && p.ClosedAt == null && p.StartsAt <= now)
            .OrderBy(p => p.StartsAt)
            .ToListAsync(ct);

        var activePlans = activePlansRaw.Select(p => new A6ActiveMaintenanceItem(
            p.Id,
            p.Scope,
            p.IsEmergency,
            DateTime.SpecifyKind(p.StartsAt, DateTimeKind.Utc),
            DateTime.SpecifyKind(p.ExpectedEndsAt, DateTimeKind.Utc),
            p.Reason
        )).ToList();

        // 3. Scheduled Plans: Future plans, not cancelled, not closed
        var scheduledPlansRaw = await db.V1MaintenancePlans.AsNoTracking()
            .Where(p => p.CancelledAt == null && p.ClosedAt == null && p.StartsAt > now)
            .OrderBy(p => p.StartsAt)
            .ToListAsync(ct);

        var scheduledPlans = scheduledPlansRaw.Select(p => new A6ScheduledMaintenanceItem(
            p.Id,
            p.Scope,
            p.IsEmergency,
            DateTime.SpecifyKind(p.StartsAt, DateTimeKind.Utc),
            DateTime.SpecifyKind(p.ExpectedEndsAt, DateTimeKind.Utc),
            p.Reason
        )).ToList();

        // 4. Active Emergency Announcements
        var announcementsRaw = await db.EmergencyAnnouncements.AsNoTracking()
            .Where(a => a.Status == EmergencyAnnouncementStatus.Active && a.DeactivatedAt == null)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(ct);

        var announcements = announcementsRaw.Select(a => new A6EmergencyAnnouncementItem(
            a.Id,
            a.Title,
            a.Description,
            DateTime.SpecifyKind(a.CreatedAt, DateTimeKind.Utc)
        )).ToList();

        audit.Add(actor, "A6_MAINTENANCE_OVERVIEW_READ", "SystemMaintenance", null, purpose,
            new { SystemEnabled = isSystemMaintenanceActive, ActiveCount = activePlans.Count, ScheduledCount = scheduledPlans.Count });
        await db.SaveChangesAsync(ct);

        return new A6MaintenanceOverviewResult(
            now,
            isSystemMaintenanceActive,
            activePlans,
            scheduledPlans,
            announcements
        );
    }

    private static void Validate(int page, int pageSize, DateTime? fromUtc, DateTime? toUtc)
    {
        InputBoundary.Page(page, pageSize);
        if (page - 1 > int.MaxValue / pageSize)
            throw new AppException("invalid_pagination", "Page too large.");
        if (fromUtc is { } from && from.Kind != DateTimeKind.Utc || toUtc is { } to && to.Kind != DateTimeKind.Utc || fromUtc >= toUtc)
            throw new AppException("invalid_date_range", "Use increasing UTC half-open boundaries.");
    }

    private static string? SanitizeContent(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;
        // Redact OTPs and numeric verification codes: 4 to 8 digits
        var sanitized = Regex.Replace(text, @"\b\d{4,8}\b", "[REDACTED_CODE]");
        // Redact authorization tokens / bearer tokens
        sanitized = Regex.Replace(sanitized, @"(?i)(bearer\s+)[A-Za-z0-9\-._~+/]+=*", "$1[REDACTED_TOKEN]");
        sanitized = Regex.Replace(sanitized, @"(?i)(token[""']?\s*[:=]\s*[""']?)[A-Za-z0-9\-._~+/]+", "$1[REDACTED_TOKEN]");
        sanitized = Regex.Replace(sanitized, @"(?i)(password[""']?\s*[:=]\s*[""']?)[^""'\s,}]+", "$1[REDACTED_PASSWORD]");
        sanitized = Regex.Replace(sanitized, @"(?i)(secret[""']?\s*[:=]\s*[""']?)[^""'\s,}]+", "$1[REDACTED_SECRET]");
        sanitized = Regex.Replace(sanitized, @"(?i)(apiKey[""']?\s*[:=]\s*[""']?)[^""'\s,}]+", "$1[REDACTED_SECRET]");
        return sanitized;
    }

    private static string SanitizeSubject(string subject)
    {
        if (string.IsNullOrWhiteSpace(subject)) return subject;
        var sanitized = Regex.Replace(subject, @"\b\d{4,8}\b", "[CODE]");
        sanitized = Regex.Replace(sanitized, @"(?i)(bearer\s+)[A-Za-z0-9\-._~+/]+=*", "$1[REDACTED_TOKEN]");
        sanitized = Regex.Replace(sanitized, @"(?i)(token[""']?\s*[:=]\s*[""']?)[A-Za-z0-9\-._~+/]+", "$1[REDACTED_TOKEN]");
        sanitized = Regex.Replace(sanitized, @"(?i)(password[""']?\s*[:=]\s*[""']?)[^""'\s,}]+", "$1[REDACTED_PASSWORD]");
        sanitized = Regex.Replace(sanitized, @"(?i)(secret[""']?\s*[:=]\s*[""']?)[^""'\s,}]+", "$1[REDACTED_SECRET]");
        return sanitized;
    }

    private static string MaskEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return "****";
        var atIndex = email.IndexOf('@');
        if (atIndex <= 1) return "****" + (atIndex >= 0 ? email[atIndex..] : "");
        var local = email[..atIndex];
        var domain = email[atIndex..];
        var maskedLocal = local.Length switch
        {
            <= 2 => local[0] + "*",
            _ => string.Concat(local.AsSpan(0, 2), "****", local.AsSpan(local.Length - 1))
        };
        return maskedLocal + domain;
    }

    private static string MaskPushToken(string? pushToken)
    {
        if (string.IsNullOrWhiteSpace(pushToken)) return "Token[none]";
        if (pushToken.Length <= 10) return "Token[****]";
        return string.Concat(pushToken.AsSpan(0, 7), "****", pushToken.AsSpan(pushToken.Length - 4));
    }

    private static string MaskSpendId(string spendId)
    {
        if (string.IsNullOrWhiteSpace(spendId)) return "****";
        if (spendId.Length <= 4) return "****";
        return string.Concat(spendId.AsSpan(0, 2), "****", spendId.AsSpan(spendId.Length - 2));
    }
}
