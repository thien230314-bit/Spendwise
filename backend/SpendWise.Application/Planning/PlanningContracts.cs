namespace SpendWise.Application.Planning;

// Dữ liệu tạo ngân sách mới cho một danh mục trong một tháng.
public sealed record CreateBudgetRequest(Guid CategoryId, int Year, int Month, decimal LimitAmount,
    bool AlertsEnabled = true, bool Rollover = false, string? SecurityPin = null);
// Chỉ sửa hạn mức và trạng thái cảnh báo, không đổi khóa danh mục/tháng.
public sealed record UpdateBudgetRequest(decimal LimitAmount, bool AlertsEnabled = true,
    bool Rollover = false, string? SecurityPin = null);
public sealed record DeleteBudgetRequest(string SecurityPin);
public sealed record BudgetDto(Guid Id, Guid CategoryId, string CategoryName, int Year, int Month, decimal LimitAmount,
    decimal SpentAmount, decimal RemainingAmount, decimal Percentage, string AlertLevel, bool AlertsEnabled, bool Rollover);
public sealed record CreateGoalRequest(string Name, decimal TargetAmount, DateTime? TargetDate = null,
    Guid? CategoryId = null, string? IconName = null, string? ColorHex = null,
    decimal InitialAmount = 0, Guid? WalletId = null, string? IdempotencyKey = null, string? SecurityPin = null);
// CurrentAllocatedAmount không nằm trong request vì tiền chỉ đổi qua đóng góp hoặc rút tiền.
public sealed record UpdateGoalRequest(string Name, decimal TargetAmount, DateTime? TargetDate = null,
    Guid? CategoryId = null, string? IconName = null, string? ColorHex = null, string? SecurityPin = null);
public sealed record DeleteGoalRequest(string SecurityPin);
public sealed record GoalMovementRequest(Guid WalletId, decimal Amount, string IdempotencyKey, string? SecurityPin = null);
public sealed record GoalMovementDto(Guid Id, decimal Amount, DateTime Date, Guid WalletId, string? WalletName, string Type);
public sealed record GoalDto(Guid Id, string Name, decimal TargetAmount, decimal CurrentAllocatedAmount, DateTime? TargetDate,
    string Status, decimal Percentage, Guid? CategoryId, string? CategoryName, string? IconName, string? ColorHex,
    IReadOnlyList<GoalMovementDto> Movements);
public sealed record CreateRecurringRequest(Guid WalletId, Guid CategoryId, string Kind, decimal Amount, string Description, string Frequency, DateTime NextRunAt, bool IsSubscription = false, string? Title = null);
// Cập nhật cấu hình chạy trong tương lai, không sửa giao dịch đã phát sinh.
public sealed record UpdateRecurringRequest(Guid WalletId, Guid CategoryId, string Kind, decimal Amount, string Description, string Frequency, DateTime NextRunAt, bool IsSubscription = false, string? Title = null);
public sealed record RecurringOccurrenceDto(Guid Id, DateTime ScheduledFor, string Status, Guid? RecordedTransactionId, DateTime? RecordedAt);
public sealed record RecurringDto(Guid Id, Guid WalletId, Guid CategoryId, string Kind, decimal Amount, string Title, string Description, string Frequency,
    DateTime NextRunAt, string Status, bool IsSubscription, DateTime? LastRunAt, int AnchorDay = 0, int AnchorMonth = 0,
    IReadOnlyList<RecurringOccurrenceDto>? Occurrences = null);
public sealed record RecordRecurringOccurrenceRequest(string SecurityPin, IReadOnlyList<Guid> EvidenceIds, string IdempotencyKey);
public sealed record CreateTransactionRuleRequest(string DescriptionPattern, Guid TargetCategoryId, int Priority = 0);
public sealed record UpdateTransactionRuleRequest(string DescriptionPattern, Guid TargetCategoryId, int Priority = 0, bool IsActive = true);
public sealed record TransactionRuleDto(Guid Id, string DescriptionPattern, Guid TargetCategoryId, string TargetCategoryName, int Priority, bool IsActive);

public interface IRecurringProcessor
{
    Task<int> ProcessDueAsync(CancellationToken cancellationToken);
}
