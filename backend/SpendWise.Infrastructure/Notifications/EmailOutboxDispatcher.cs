using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Identity;
using SpendWise.Domain.Notifications;
using SpendWise.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace SpendWise.Infrastructure.Notifications;

// Worker gửi email đã commit; lỗi được lưu lại và retry tối đa năm lần với backoff giới hạn.
public sealed class EmailOutboxDispatcher(
    SpendWiseDbContext db,
    ITransactionalEmailSender sender,
    IEmailContentProtector contentProtector,
    IClock clock,
    ILogger<EmailOutboxDispatcher> logger)
{
    public async Task<int> DispatchPendingAsync(CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var staleBefore = now.AddMinutes(-10);
        var pendingIds = await db.EmailOutboxMessages.AsNoTracking()
            .Where(x => (x.Status == EmailDeliveryStatus.Pending
                         || (x.Status == EmailDeliveryStatus.Processing
                             && x.ProcessingStartedAt < staleBefore)) &&
                        x.AttemptCount < 5 &&
                        (x.NextAttemptAt == null || x.NextAttemptAt <= now))
            .OrderBy(x => x.CreatedAt)
            .Take(50)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var delivered = 0;
        foreach (var id in pendingIds)
        {
            if (await DispatchOneAsync(id, now, staleBefore, cancellationToken))
                delivered++;
        }
        return delivered;
    }

    // Recovery must never scan backlog or reclaim a possibly in-flight Processing row.
    public async Task<bool> DispatchByIdAsync(
        Guid id, string expectedRecipient, string expectedDeduplicationKey, CancellationToken cancellationToken)
    {
        if (!db.Database.IsRelational())
            throw new InvalidOperationException("Targeted dispatch requires a relational atomic claim.");
        if (id == Guid.Empty || string.IsNullOrWhiteSpace(expectedRecipient)
            || string.IsNullOrWhiteSpace(expectedDeduplicationKey))
            return false;
        var target = await db.EmailOutboxMessages.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new { x.RecipientEmail, x.DeduplicationKey })
            .SingleOrDefaultAsync(cancellationToken);
        if (target is null || !string.Equals(target.RecipientEmail, expectedRecipient, StringComparison.Ordinal)
            || !string.Equals(target.DeduplicationKey, expectedDeduplicationKey, StringComparison.Ordinal))
            return false;
        var now = clock.UtcNow;
        return await DispatchOneAsync(id, now, now.AddMinutes(-10), cancellationToken,
            expectedRecipient, expectedDeduplicationKey);
    }

    private async Task<bool> DispatchOneAsync(
        Guid id, DateTime now, DateTime staleBefore, CancellationToken cancellationToken,
        string? expectedRecipient = null, string? expectedDeduplicationKey = null)
    {
        var claimed = await ClaimAsync(id, now, staleBefore, cancellationToken,
            expectedRecipient, expectedDeduplicationKey);
        if (!claimed) return false;
        db.ChangeTracker.Clear();
        var item = await db.EmailOutboxMessages.SingleAsync(x => x.Id == id, cancellationToken);
        if (expectedRecipient is not null
            && (!string.Equals(item.RecipientEmail, expectedRecipient, StringComparison.Ordinal)
                || !string.Equals(item.DeduplicationKey, expectedDeduplicationKey, StringComparison.Ordinal)))
            throw new InvalidOperationException("Claimed email identity changed; delivery stopped.");
        if (item.ContentExpiresAt is not null && item.ContentExpiresAt <= clock.UtcNow)
        {
            item.Status = EmailDeliveryStatus.Failed;
            item.LastError = "content_expired";
            item.ProcessingStartedAt = null;
            item.HtmlBody = string.Empty;
            item.TextBody = string.Empty;
            await db.SaveChangesAsync(cancellationToken);
            return false;
        }
        var delivered = false;
        try
        {
            await sender.SendAsync(
                new EmailMessage(
                    item.RecipientEmail,
                    item.Subject,
                    contentProtector.Unprotect(item.HtmlBody),
                    contentProtector.Unprotect(item.TextBody)),
                cancellationToken);
            item.Status = EmailDeliveryStatus.Sent;
            item.SentAt = clock.UtcNow;
            item.LastError = null;
            item.NextAttemptAt = null;
            item.ProcessingStartedAt = null;
            // Xóa ciphertext sau khi gửi để OTP không tồn tại lâu hơn cần thiết.
            item.HtmlBody = string.Empty;
            item.TextBody = string.Empty;
            delivered = true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // Không lưu/log exception message vì provider có thể đưa secret hoặc payload vào đó.
            item.LastError = "delivery_failed";
            item.NextAttemptAt = clock.UtcNow.AddSeconds(Math.Min(300, Math.Pow(2, item.AttemptCount)));
            item.ProcessingStartedAt = null;
            if (item.AttemptCount >= 5)
            {
                item.Status = EmailDeliveryStatus.Failed;
                item.HtmlBody = string.Empty;
                item.TextBody = string.Empty;
            }
            else
                item.Status = EmailDeliveryStatus.Pending;
            logger.LogWarning("Email outbox delivery failed for message {EmailOutboxId}.", item.Id);
        }

        await db.SaveChangesAsync(cancellationToken);
        return delivered;
    }

    private async Task<bool> ClaimAsync(
        Guid id,
        DateTime now,
        DateTime staleBefore,
        CancellationToken cancellationToken,
        string? expectedRecipient = null, string? expectedDeduplicationKey = null)
    {
        if (!db.Database.IsRelational())
        {
            var item = await db.EmailOutboxMessages.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (item is null
                || (expectedRecipient is not null && (item.RecipientEmail != expectedRecipient
                    || item.DeduplicationKey != expectedDeduplicationKey
                    || item.Status != EmailDeliveryStatus.Pending))
                || item.AttemptCount >= 5
                || (item.Status != EmailDeliveryStatus.Pending
                    && !(item.Status == EmailDeliveryStatus.Processing
                         && item.ProcessingStartedAt < staleBefore)))
            {
                return false;
            }
            item.Status = EmailDeliveryStatus.Processing;
            item.ProcessingStartedAt = now;
            item.AttemptCount++;
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }

        var affected = await db.EmailOutboxMessages
            .Where(x => x.Id == id
                && (expectedRecipient == null || (x.RecipientEmail == expectedRecipient
                    && x.DeduplicationKey == expectedDeduplicationKey
                    && x.Status == EmailDeliveryStatus.Pending))
                && x.AttemptCount < 5
                && (x.Status == EmailDeliveryStatus.Pending
                    || (x.Status == EmailDeliveryStatus.Processing
                        && x.ProcessingStartedAt < staleBefore))
                && (x.NextAttemptAt == null || x.NextAttemptAt <= now))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.Status, EmailDeliveryStatus.Processing)
                    .SetProperty(x => x.ProcessingStartedAt, now)
                    .SetProperty(x => x.AttemptCount, x => x.AttemptCount + 1),
                cancellationToken);
        return affected == 1;
    }
}

// Worker tạo scope mới, nên email được gửi bằng DbContext riêng ở mỗi chu kỳ.
public sealed class EmailOutboxBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<EmailOutboxBackgroundService> logger) : BackgroundService
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
                    .GetRequiredService<EmailOutboxDispatcher>()
                    .DispatchPendingAsync(stoppingToken);
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Email outbox scan failed; the next cycle will retry.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
