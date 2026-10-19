using SpendWise.Application.Planning;
using SpendWise.Domain.Planning;

namespace SpendWise.Application.Features.RecurringTransactions;

// Lớp này chuyển cấu hình giao dịch định kỳ sang DTO trả cho người sở hữu.

internal static class RecurringTransactionMappings
{
    public static RecurringDto ToDto(RecurringTransaction transaction, DateTime utcNow)
    {
        // Chuyển lịch chạy, số tiền và trạng thái RecurringTransaction sang DTO.
        return new RecurringDto(
            transaction.Id,
            transaction.WalletId,
            transaction.CategoryId,
            transaction.Kind.ToString(),
            transaction.Amount,
            transaction.Title,
            transaction.Description,
            transaction.Frequency.ToString(),
            transaction.NextRunAt,
            transaction.Status.ToString(),
            transaction.IsSubscription,
            transaction.LastRunAt,
            transaction.AnchorDay,
            transaction.AnchorMonth,
            transaction.Occurrences
                .OrderByDescending(item => item.ScheduledFor)
                .Select(item => new RecurringOccurrenceDto(
                    item.Id,
                    item.ScheduledFor,
                    item.Status == RecurringOccurrenceStatus.Recorded
                        ? "Recorded"
                        : item.ScheduledFor < utcNow ? "Overdue" : "Due",
                    item.RecordedTransactionId,
                    item.RecordedAt))
                .ToList());
    }
}
