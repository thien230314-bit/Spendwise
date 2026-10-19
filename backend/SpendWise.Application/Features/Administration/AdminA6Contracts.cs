using System.Security.Claims;

namespace SpendWise.Application.Features.Administration;

// A6.1 - Thông báo nghiệp vụ
public sealed record A6NotificationQuery(
    string Reason,
    Guid? CaseId = null,
    Guid? UserId = null,
    string? Type = null,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null,
    int Page = 1,
    int PageSize = 20);

public sealed record A6NotificationItem(
    Guid Id,
    Guid UserId,
    string? UserFullName,
    string? UserSpendId,
    string Type,
    string Title,
    string Message,
    string? DataJson,
    bool IsRead,
    DateTime? ReadAt,
    DateTime CreatedAt);

// A6.2 - Lịch sử gửi (Delivery History)
public sealed record A6DeliveryHistoryQuery(
    string Reason,
    Guid? CaseId = null,
    Guid? UserId = null,
    string? Channel = null, // InApp, Email, Push
    string? Status = null,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null,
    int Page = 1,
    int PageSize = 20);

public sealed record A6DeliveryHistoryItem(
    Guid Id,
    Guid UserId,
    string Channel, // InApp, Email, Push
    string TypeOrSubject,
    string MaskedRecipient, // Masked email, masked push token, or User SpendId
    string Status, // Created, Queued, Processing, Sent, ProviderAccepted, Failed, UserRead
    int AttemptCount,
    DateTime CreatedAt,
    DateTime? SentAt,
    DateTime? ReadAt,
    string? LastError);

// A6.3 - Lỗi gửi thông báo (Failed Deliveries Feed)
public sealed record A6DeliveryFailureQuery(
    string Reason,
    string? Channel = null,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null,
    int Page = 1,
    int PageSize = 20);

public sealed record A6DeliveryFailureItem(
    Guid Id,
    string Channel,
    string RecipientMasked,
    string SubjectOrEvent,
    int AttemptCount,
    DateTime? NextAttemptAt,
    DateTime CreatedAt,
    string? SanitizedError);

// A6.4 - Danh mục mẫu & Tổng quan bảo trì
public sealed record A6NotificationTemplateItem(
    string TemplateKey,
    string Title,
    string Category,
    string Description,
    IReadOnlyList<string> AllowedPlaceholders);

public sealed record A6MaintenanceOverviewResult(
    DateTime Timestamp,
    bool SystemMaintenanceEnabled,
    IReadOnlyList<A6ActiveMaintenanceItem> ActivePlans,
    IReadOnlyList<A6ScheduledMaintenanceItem> ScheduledPlans,
    IReadOnlyList<A6EmergencyAnnouncementItem> ActiveAnnouncements);

public sealed record A6ActiveMaintenanceItem(
    Guid PlanId,
    string Scope,
    bool IsEmergency,
    DateTime StartsAt,
    DateTime ExpectedEndsAt,
    string Reason);

public sealed record A6ScheduledMaintenanceItem(
    Guid PlanId,
    string Scope,
    bool IsEmergency,
    DateTime StartsAt,
    DateTime ExpectedEndsAt,
    string Reason);

public sealed record A6EmergencyAnnouncementItem(
    Guid Id,
    string Title,
    string Description,
    DateTime CreatedAt);
