using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Identity;
using SpendWise.Domain.Notifications;

namespace SpendWise.Infrastructure.Notifications;

// Ghi email vào database cùng transaction nghiệp vụ và chống trùng bằng khóa duy nhất.
public sealed class EmailDeliveryQueue(
    ISpendWiseDbContext db,
    IEmailContentProtector contentProtector) : IEmailDeliveryQueue
{
    public async Task<bool> EnqueueAsync(
        EmailMessage message,
        string deduplicationKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(message.RecipientEmail))
            throw new ArgumentException("Recipient email is required.", nameof(message));
        if (string.IsNullOrWhiteSpace(deduplicationKey))
            throw new ArgumentException("Email deduplication key is required.", nameof(deduplicationKey));

        var key = deduplicationKey.Trim();
        if (await db.EmailOutboxMessages.AnyAsync(x => x.DeduplicationKey == key, cancellationToken))
            return false;

        db.EmailOutboxMessages.Add(new EmailOutboxMessage
        {
            RecipientEmail = message.RecipientEmail.Trim(),
            Subject = message.Subject,
            // OTP và nội dung động được mã hóa khi nằm trong database.
            HtmlBody = contentProtector.Protect(message.HtmlBody),
            TextBody = contentProtector.Protect(message.TextBody),
            ContentExpiresAt = message.ContentExpiresAt,
            DeduplicationKey = key,
            Status = EmailDeliveryStatus.Pending
        });
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            // Unique index là lớp bảo vệ cuối khi hai request cùng queue một event.
            db.ClearTrackedEntities();
            if (await db.EmailOutboxMessages.AsNoTracking()
                .AnyAsync(x => x.DeduplicationKey == key, cancellationToken))
            {
                return false;
            }
            throw;
        }
    }
}
