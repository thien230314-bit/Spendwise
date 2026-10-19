using SpendWise.Domain.Common;

namespace SpendWise.Domain.Notifications;

// Entity này lưu nội dung thông báo và thời điểm người dùng đã đọc.

public enum NotificationType
{
    MoneyReceived, MoneySent, MoneyRequest, BudgetWarning, BudgetExceeded, DebtReminder,
    GoalProgress, GroupInvite, ContributionRequest, SecurityAlert, SystemAnnouncement,
    BankAccountLinked, BankAccountInactive, BankAccountUnlinked, BankTransactionImported,
    BankIntegrationProblem, RecurringDue, RecurringOverdue,
    FriendRequest, FriendAccepted, FriendRejected, GroupFundRequest, GroupFundRound,
    BankDirectPaymentCompleted, BankDirectPaymentFailed, GroupDeletionVote,
    GroupDeletionRejected, GroupDeletionReady, GroupRefundAction, GroupRefundCompleted,
    SupportUpdated, DisputeUpdated, BalanceChanged, LargeTransaction, MinimumBalance,
    MaintenanceChanged, EmergencyAnnouncement, V1FundActivity, AdminTaskAssigned
}

public sealed class NotificationPreference : BaseEntity
{
    public Guid UserId { get; set; }
    public string AccountKey { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public Guid? WalletId { get; set; }
    public bool NotifyBalanceChanges { get; set; } = true;
    public bool NotifyBudgetAlerts { get; set; } = true;
    public bool NotifyLargeTransactions { get; set; } = true;
    public decimal LargeTransactionThreshold { get; set; }
    public bool NotifyMinBalanceAlert { get; set; } = true;
    public decimal MinBalanceThreshold { get; set; }
}

public sealed class NotificationDeduplication : BaseEntity
{
    public Guid UserId { get; set; }
    public string Key { get; set; } = string.Empty;
    public Guid NotificationId { get; set; }
}

public sealed class Notification : BaseEntity
{
    // UserId giới hạn thông báo cho đúng người nhận.
    public Guid UserId { get; set; }
    public NotificationType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? DataJson { get; set; }
    // ReadAt null nghĩa là thông báo chưa được đọc.
    public DateTime? ReadAt { get; set; }
}

// Outbox lưu sự kiện cần gửi sau khi transaction nghiệp vụ đã commit.
public sealed class OutboxMessage : BaseEntity
{
    public Guid UserId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    // ProcessedAt có giá trị nghĩa là sự kiện đã được gửi thành công.
    public DateTime? ProcessedAt { get; set; }
    public int AttemptCount { get; set; }
    public DateTime? NextAttemptAt { get; set; }
    public string? LastError { get; set; }
}
