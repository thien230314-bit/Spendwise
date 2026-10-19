using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Finance;
using SpendWise.Application.Features.RecurringTransactions;
using SpendWise.Application.Notifications;
using SpendWise.Application.Planning;
using SpendWise.Domain.Planning;
using SpendWise.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SpendWise.Infrastructure.Planning;

// Xử lý lịch chạy của giao dịch định kỳ và tạo lần chạy kế tiếp.

public sealed class RecurringProcessor(
    SpendWiseDbContext db,
    INotificationService notifications,
    IClock clock) : IRecurringProcessor
{
    public async Task<int> ProcessDueAsync(CancellationToken ct)
    {
        if (await SpendWise.Application.Features.Operations.V1MaintenanceGate.IsActiveAsync(db, clock.UtcNow, ct, "Money")) return 0;
        // Chỉ materialize occurrence; worker tuyệt đối không posting tiền hoặc sửa số dư.
        var due = await db.RecurringTransactions
            .Where(x => x.Status == RecurringStatus.Active && x.NextRunAt <= clock.UtcNow)
            .OrderBy(x => x.NextRunAt)
            .Take(50)
            .ToListAsync(ct);
        var count = 0;
        foreach (var item in due)
        {
            var scheduledFor = item.NextRunAt;
            // Dữ liệu cũ chưa có anchor được khởi tạo một lần từ ngày đến hạn gốc.
            if (item.AnchorDay is < 1 or > 31) item.AnchorDay = scheduledFor.Day;
            if (item.AnchorMonth is < 1 or > 12) item.AnchorMonth = scheduledFor.Month;
            var exists = await db.RecurringOccurrences.AnyAsync(
                occurrence => occurrence.RecurringTransactionId == item.Id
                    && occurrence.ScheduledFor == scheduledFor,
                ct);
            if (!exists)
            {
                var occurrence = new RecurringOccurrence
                {
                    RecurringTransactionId = item.Id,
                    ScheduledFor = scheduledFor
                };
                db.RecurringOccurrences.Add(occurrence);
                // Advance bằng anchor và bỏ qua các kỳ quá cũ, không tự ghi nhận tiền đã lỡ.
                item.NextRunAt = RecurringSchedule.AdvanceUntilFuture(
                    scheduledFor,
                    item.Frequency,
                    item.AnchorDay,
                    item.AnchorMonth,
                    clock.UtcNow);
                await db.SaveChangesAsync(ct);

                var overdue = scheduledFor < clock.UtcNow;
                await notifications.CreateAsync(
                    item.OwnerUserId,
                    overdue ? "RecurringOverdue" : "RecurringDue",
                    overdue ? "Recurring payment overdue" : "Recurring payment due",
                    $"{item.Description} is ready to record.",
                    new { recurringTransactionId = item.Id, occurrenceId = occurrence.Id },
                    ct);
                count++;
            }
            else
            {
                item.NextRunAt = RecurringSchedule.AdvanceUntilFuture(
                    scheduledFor,
                    item.Frequency,
                    item.AnchorDay,
                    item.AnchorMonth,
                    clock.UtcNow);
            }
        }

        await db.SaveChangesAsync(ct);
        return count;
    }

}

public sealed class RecurringBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<RecurringBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Chạy tác vụ nền theo chu kỳ và dừng an toàn khi ứng dụng tắt.
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                await scope.ServiceProvider.GetRequiredService<IRecurringProcessor>().ProcessDueAsync(stoppingToken);
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                // Ghi loại worker và lỗi để vận hành điều tra được nhưng không lộ payload tài chính.
                logger.LogError(
                    exception,
                    "Recurring transaction cycle failed; retrying after a short delay.");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
    }
}
