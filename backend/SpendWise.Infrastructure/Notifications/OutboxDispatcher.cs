using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Notifications;
using SpendWise.Domain.Notifications;
using SpendWise.Infrastructure.Persistence;

namespace SpendWise.Infrastructure.Notifications;

// Dispatcher gửi các sự kiện đã commit và giữ lại bản ghi khi hệ thống realtime tạm lỗi.
public sealed class OutboxDispatcher(
    SpendWiseDbContext db,
    IRealtimeNotifier realtime,
    IClock clock,
    ILogger<OutboxDispatcher> logger)
{
    public async Task<int> DispatchPendingAsync(CancellationToken ct)
    {
        // Chỉ lấy các sự kiện chưa gửi và đã đến thời điểm thử lại.
        var now = clock.UtcNow;
        var pending = await db.OutboxMessages
            .Where(x => x.ProcessedAt == null &&
                (x.NextAttemptAt == null || x.NextAttemptAt <= now))
            .OrderBy(x => x.CreatedAt)
            .Take(50)
            .ToListAsync(ct);

        var delivered = 0;
        foreach (var message in pending)
        {
            try
            {
                // Chuyển payload JSON về dữ liệu thuần để SignalR gửi đúng nội dung đã lưu.
                using var document = JsonDocument.Parse(message.PayloadJson);
                await realtime.SendToUserAsync(
                    message.UserId,
                    message.EventName,
                    document.RootElement.Clone(),
                    ct);

                message.ProcessedAt = clock.UtcNow;
                message.LastError = null;
                message.NextAttemptAt = null;
                message.AttemptCount++;
                delivered++;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                // Lỗi gửi không xóa outbox; worker sẽ thử lại với thời gian chờ tăng dần.
                message.AttemptCount++;
                message.NextAttemptAt = clock.UtcNow.AddSeconds(Math.Min(300, Math.Pow(2, Math.Min(message.AttemptCount, 8))));
                message.LastError = $"{exception.GetType().Name}: {exception.Message}"[..Math.Min(1000, $"{exception.GetType().Name}: {exception.Message}".Length)];
                logger.LogWarning(exception, "Không thể gửi outbox {OutboxMessageId}; hệ thống sẽ thử lại.", message.Id);
            }
        }

        await db.SaveChangesAsync(ct);
        return delivered;
    }
}

// Background worker tạo scope mới cho mỗi lần quét outbox.
public sealed class OutboxBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<OutboxBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                await scope.ServiceProvider
                    .GetRequiredService<OutboxDispatcher>()
                    .DispatchPendingAsync(stoppingToken);
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                // Worker không được chết vì một lần quét lỗi; log rồi tiếp tục ở chu kỳ sau.
                logger.LogError(exception, "Vòng quét transactional outbox thất bại.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
