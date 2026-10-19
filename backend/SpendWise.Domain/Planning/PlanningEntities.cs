using SpendWise.Domain.Common;
using SpendWise.Domain.Finance;

namespace SpendWise.Domain.Planning;

// Các entity này lưu Budget, Goal, lịch định kỳ và quy tắc giao dịch của người dùng.

public enum GoalStatus { Active, Completed, Paused, Cancelled }
public enum GoalMovementType { Contribution, Withdrawal }
public enum RecurringFrequency { Daily, Weekly, Monthly, Yearly }
public enum RecurringStatus { Active, Paused, Completed, Cancelled }
public enum RecurringOccurrenceStatus { Due, Recorded }

public sealed class Budget : BaseEntity
{
    public Guid OwnerUserId { get; set; }
    public Guid CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    public int Year { get; set; }
    public int Month { get; set; }
    // LimitAmount là cấu hình cảnh báo, không phải tiền bị giữ hoặc số dư ví.
    public decimal LimitAmount { get; set; }
    public bool AlertsEnabled { get; set; } = true;
    public bool Rollover { get; set; }
}

public sealed class Goal : BaseEntity
{
    public Guid OwnerUserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal TargetAmount { get; set; }
    // CurrentAllocatedAmount chỉ thay đổi qua ContributeGoal và WithdrawGoal.
    public decimal CurrentAllocatedAmount { get; set; }
    public DateTime? TargetDate { get; set; }
    public Guid? CategoryId { get; set; }
    public string? CategorySnapshot { get; set; }
    public string? IconName { get; set; }
    public string? ColorHex { get; set; }
    public GoalStatus Status { get; set; } = GoalStatus.Active;
    // LedgerAccount giữ riêng số tiền đang phân bổ cho mục tiêu.
    public Guid LedgerAccountId { get; set; }
    public LedgerAccount LedgerAccount { get; set; } = null!;
    public List<GoalMovement> Movements { get; set; } = [];
}

public sealed class GoalMovement : BaseEntity
{
    public Guid GoalId { get; set; }
    public Goal Goal { get; set; } = null!;
    public Guid WalletId { get; set; }
    public string? WalletNameSnapshot { get; set; }
    public GoalMovementType Type { get; set; }
    public decimal Amount { get; set; }
    public Guid LedgerTransactionId { get; set; }
    // IdempotencyKey ngăn đóng góp hoặc rút tiền bị ghi hai lần khi client retry.
    public string IdempotencyKey { get; set; } = string.Empty;
}

public sealed class RecurringTransaction : BaseEntity
{
    public Guid OwnerUserId { get; set; }
    public Guid WalletId { get; set; }
    public Guid CategoryId { get; set; }
    public CategoryKind Kind { get; set; }
    public decimal Amount { get; set; }
    // Tiêu đề ngắn hiển thị trong danh sách lịch định kỳ.
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public RecurringFrequency Frequency { get; set; }
    // NextRunAt xác định lần chạy tương lai; LastRunAt ghi lần đã hoàn tất gần nhất.
    public DateTime NextRunAt { get; set; }
    public RecurringStatus Status { get; set; } = RecurringStatus.Active;
    public bool IsSubscription { get; set; }
    public DateTime? LastRunAt { get; set; }
    // Anchor giữ ngày/tháng người dùng chọn để lịch 31 hoặc 29/02 không bị trôi.
    public int AnchorDay { get; set; }
    public int AnchorMonth { get; set; }
    public List<RecurringOccurrence> Occurrences { get; set; } = [];
}

public sealed class RecurringOccurrence : BaseEntity
{
    public Guid RecurringTransactionId { get; set; }
    public RecurringTransaction RecurringTransaction { get; set; } = null!;
    public DateTime ScheduledFor { get; set; }
    public RecurringOccurrenceStatus Status { get; set; } = RecurringOccurrenceStatus.Due;
    public Guid? RecordedTransactionId { get; set; }
    public FinancialTransaction? RecordedTransaction { get; set; }
    public DateTime? RecordedAt { get; set; }
}

public sealed class TransactionRule : BaseEntity
{
    public Guid OwnerUserId { get; set; }
    public string DescriptionPattern { get; set; } = string.Empty;
    public Guid TargetCategoryId { get; set; }
    public Category TargetCategory { get; set; } = null!;
    // Priority lớn hơn được xét trước khi tự động chọn danh mục.
    public int Priority { get; set; }
    public bool IsActive { get; set; } = true;
}
