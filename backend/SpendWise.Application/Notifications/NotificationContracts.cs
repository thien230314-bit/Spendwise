namespace SpendWise.Application.Notifications;

// Các hợp đồng này lưu thông báo và gửi sự kiện realtime tới đúng người dùng.

public sealed record NotificationDto(Guid Id, string Type, string Title, string Message, string? DataJson, bool IsRead, DateTime CreatedAt, DateTime? ReadAt);
public sealed record NotificationPageDto(IReadOnlyList<NotificationDto> Items, int Page, int PageSize, int Total);
public sealed record NotificationSearchFilter(bool? UnreadOnly = null, string? Type = null, string? Search = null, int Page = 1, int PageSize = 20);
public sealed record NotificationPreferenceRequest(string AccountKey, string AccountName, Guid? WalletId,
    bool NotifyBalanceChanges, bool NotifyBudgetAlerts, bool NotifyLargeTransactions, decimal LargeTransactionThreshold,
    bool NotifyMinBalanceAlert, decimal MinBalanceThreshold);
public sealed record NotificationPreferenceDto(Guid Id, string AccountKey, string AccountName, Guid? WalletId,
    bool NotifyBalanceChanges, bool NotifyBudgetAlerts, bool NotifyLargeTransactions, decimal LargeTransactionThreshold,
    bool NotifyMinBalanceAlert, decimal MinBalanceThreshold);
// Request nội bộ dùng để batch nhiều Notification và Outbox trong một transaction.
public sealed record CreateNotificationRequest(
    Guid UserId,
    string Type,
    string Title,
    string Message,
    object? Data);

public interface IRealtimeNotifier
{
    Task SendToUserAsync(Guid userId, string eventName, object payload, CancellationToken cancellationToken);

    // Sự kiện vận hành toàn hệ thống được gửi tới mọi kết nối đang online.
    Task SendAllAsync(string eventName, object payload, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}

public interface INotificationService
{
    Task<NotificationDto> CreateAsync(Guid userId, string type, string title, string message, object? data, CancellationToken cancellationToken);
    Task<IReadOnlyList<NotificationDto>> CreateManyAsync(
        IReadOnlyCollection<CreateNotificationRequest> requests,
        CancellationToken cancellationToken);
}
