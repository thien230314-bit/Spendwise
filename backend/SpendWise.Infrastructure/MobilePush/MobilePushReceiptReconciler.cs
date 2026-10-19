using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.MobilePush;
using SpendWise.Domain.MobilePush;
using SpendWise.Infrastructure.Persistence;

namespace SpendWise.Infrastructure.MobilePush;

public sealed class MobilePushReceiptReconciler(
    SpendWiseDbContext db,
    IExpoPushSender sender,
    IOptions<MobilePushOptions> options,
    IClock clock,
    ILogger<MobilePushReceiptReconciler> logger)
{
    // Expo recommends the first receipt lookup after 15 minutes, clears receipts after 24 hours,
    // and accepts at most 1,000 receipt IDs per request.
    // Source: https://docs.expo.dev/push-notifications/sending-notifications/
    private static readonly TimeSpan FirstLookupDelay = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan ReceiptRetention = TimeSpan.FromHours(24);
    private const int MaximumBatchSize = 1000;

    public async Task<int> ReconcileAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled)
            return 0;

        var now = clock.UtcNow;
        var deliveries = await db.MobilePushDeliveries
            .Include(item => item.Installation)
            .Where(item => item.Status == MobilePushDeliveryStatus.Submitted
                && item.ExpoReceiptId != null
                && item.SubmittedAt != null
                && item.SubmittedAt <= now - FirstLookupDelay
                && (item.NextAttemptAt == null || item.NextAttemptAt <= now))
            .OrderBy(item => item.SubmittedAt)
            .Take(MaximumBatchSize)
            .ToListAsync(cancellationToken);
        if (deliveries.Count == 0)
            return 0;

        var expired = deliveries
            .Where(item => item.SubmittedAt <= now - ReceiptRetention)
            .ToList();
        foreach (var delivery in expired)
            Fail(delivery, "ReceiptUnavailable");

        var pending = deliveries.Except(expired).ToList();
        if (pending.Count == 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            return 0;
        }

        IReadOnlyDictionary<string, ExpoPushReceiptResult> receipts;
        try
        {
            receipts = await sender.GetReceiptsAsync(
                pending.Select(item => item.ExpoReceiptId!).Distinct().ToList(),
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (ExpoPushTransientException)
        {
            foreach (var delivery in pending)
                ScheduleTransientRetry(delivery, now);
            logger.LogWarning(
                "Expo receipt lookup for {ReceiptCount} deliveries is unavailable and will be retried.",
                pending.Count);
            await db.SaveChangesAsync(cancellationToken);
            return 0;
        }
        catch (ExpoPushReceiptPermanentException)
        {
            foreach (var delivery in pending)
                Fail(delivery, "ReceiptLookupRejected");
            await db.SaveChangesAsync(cancellationToken);
            return 0;
        }

        var completed = 0;
        foreach (var delivery in pending)
        {
            if (!receipts.TryGetValue(delivery.ExpoReceiptId!, out var receipt))
            {
                delivery.AttemptCount++;
                delivery.LastError = "ReceiptPending";
                delivery.NextAttemptAt = now.Add(FirstLookupDelay);
                continue;
            }

            delivery.AttemptCount++;
            switch (receipt.Status)
            {
                case ExpoPushReceiptStatus.Succeeded:
                    delivery.Status = MobilePushDeliveryStatus.Completed;
                    delivery.NextAttemptAt = null;
                    delivery.LastError = null;
                    completed++;
                    break;
                case ExpoPushReceiptStatus.DeviceNotRegistered:
                    Fail(delivery, "DeviceNotRegistered");
                    delivery.Installation.IsActive = false;
                    delivery.Installation.PushToken = null;
                    break;
                default:
                    Fail(delivery, "ReceiptRejected");
                    break;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return completed;
    }

    private static void ScheduleTransientRetry(MobilePushDelivery delivery, DateTime now)
    {
        delivery.AttemptCount++;
        delivery.LastError = "ReceiptLookupUnavailable";
        delivery.NextAttemptAt = now.AddSeconds(
            Math.Min(900, 30 * Math.Pow(2, Math.Min(delivery.AttemptCount, 5))));
    }

    private static void Fail(MobilePushDelivery delivery, string error)
    {
        delivery.Status = MobilePushDeliveryStatus.Failed;
        delivery.NextAttemptAt = null;
        delivery.LastError = error;
    }
}
