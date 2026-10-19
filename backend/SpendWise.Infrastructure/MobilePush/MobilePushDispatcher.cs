using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.MobilePush;
using SpendWise.Domain.MobilePush;
using SpendWise.Infrastructure.Persistence;

namespace SpendWise.Infrastructure.MobilePush;

public sealed class MobilePushDispatcher(
    SpendWiseDbContext db,
    IExpoPushSender sender,
    IOptions<MobilePushOptions> options,
    IClock clock,
    ILogger<MobilePushDispatcher> logger)
{
    private const int MaximumAttempts = 8;

    public async Task<int> DispatchPendingAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled)
            return 0;

        var now = clock.UtcNow;
        var pending = await db.MobilePushDeliveries
            .Include(item => item.Installation)
            .Include(item => item.Notification)
            .Where(item => item.Status == MobilePushDeliveryStatus.Pending
                && (item.NextAttemptAt == null || item.NextAttemptAt <= now))
            .OrderBy(item => item.CreatedAt)
            .Take(50)
            .ToListAsync(cancellationToken);

        var submitted = 0;
        foreach (var delivery in pending)
        {
            if (!delivery.Installation.IsActive
                || string.IsNullOrWhiteSpace(delivery.Installation.PushToken))
            {
                Fail(delivery, "InstallationInactive");
                continue;
            }

            try
            {
                var result = await sender.SendAsync(
                    new ExpoPushMessage(
                        delivery.Installation.PushToken,
                        delivery.NotificationId,
                        delivery.Notification.Type.ToString()),
                    cancellationToken);
                delivery.AttemptCount++;
                switch (result.Status)
                {
                    case ExpoPushSendStatus.Submitted:
                        delivery.Status = MobilePushDeliveryStatus.Submitted;
                        delivery.SubmittedAt = clock.UtcNow;
                        delivery.ExpoReceiptId = result.ReceiptId;
                        delivery.NextAttemptAt = clock.UtcNow.AddMinutes(15);
                        delivery.LastError = null;
                        submitted++;
                        break;
                    case ExpoPushSendStatus.DeviceNotRegistered:
                        Fail(delivery, "DeviceNotRegistered");
                        delivery.Installation.IsActive = false;
                        delivery.Installation.PushToken = null;
                        break;
                    default:
                        Fail(delivery, "ProviderRejected");
                        break;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (ExpoPushTransientException)
            {
                Retry(delivery);
                logger.LogWarning(
                    "Expo push delivery {MobilePushDeliveryId} is unavailable and will be retried.",
                    delivery.Id);
            }
            catch (Exception exception)
            {
                Retry(delivery);
                logger.LogWarning(
                    "Expo push delivery {MobilePushDeliveryId} failed with {ExceptionType} and will be retried.",
                    delivery.Id,
                    exception.GetType().Name);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return submitted;
    }

    private void Retry(MobilePushDelivery delivery)
    {
        delivery.AttemptCount++;
        delivery.LastError = "ProviderUnavailable";
        if (delivery.AttemptCount >= MaximumAttempts)
        {
            delivery.Status = MobilePushDeliveryStatus.Failed;
            delivery.NextAttemptAt = null;
            return;
        }

        delivery.NextAttemptAt = clock.UtcNow.AddSeconds(
            Math.Min(300, Math.Pow(2, Math.Min(delivery.AttemptCount, 8))));
    }

    private static void Fail(MobilePushDelivery delivery, string error)
    {
        delivery.Status = MobilePushDeliveryStatus.Failed;
        delivery.NextAttemptAt = null;
        delivery.LastError = error;
    }
}

public sealed class MobilePushBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<MobilePushBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                await scope.ServiceProvider.GetRequiredService<MobilePushDispatcher>()
                    .DispatchPendingAsync(stoppingToken);
                await scope.ServiceProvider.GetRequiredService<MobilePushReceiptReconciler>()
                    .ReconcileAsync(stoppingToken);
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Mobile push dispatch scan failed.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
