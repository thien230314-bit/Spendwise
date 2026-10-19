using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Domain.Identity;
using SpendWise.Domain.Notifications;
using SpendWise.Infrastructure.Persistence;

namespace SpendWise.Infrastructure.Notifications;

// Explicit durable activation boundary: old backlog is never adopted on process restart.
public sealed class AuthRecoveryDeliveryScope
{
    public DateTime NotBeforeUtc { get; }
    public string[] Recipients { get; }
    public AuthRecoveryDeliveryScope(DateTime notBeforeUtc, string[] recipients)
    {
        if (notBeforeUtc.Kind != DateTimeKind.Utc || recipients.Length == 0
            || recipients.Any(x => !System.Net.Mail.MailAddress.TryCreate(x, out var mail) || mail.Address != x))
            throw new InvalidOperationException("Auth recovery requires an explicit UTC activation boundary and recipient allowlist.");
        NotBeforeUtc = notBeforeUtc;
        Recipients = recipients.Select(x => x.ToUpperInvariant()).Distinct().ToArray();
    }
    public static AuthRecoveryDeliveryScope? Read(IConfiguration configuration)
    {
        if (!configuration.GetValue<bool>("AuthRecoveryDelivery:Enabled")) return null;
        var boundary = configuration.GetValue<DateTimeOffset?>("AuthRecoveryDelivery:NotBeforeUtc")
            ?? throw new InvalidOperationException("Auth recovery activation boundary is required.");
        return new(boundary.UtcDateTime, configuration.GetSection("AuthRecoveryDelivery:AllowedRecipients").Get<string[]>() ?? []);
    }
}

public sealed class AuthRecoveryEmailDispatcher(SpendWiseDbContext db, EmailOutboxDispatcher dispatcher, IClock clock)
{
    public async Task<int> DispatchEligibleAsync(AuthRecoveryDeliveryScope scope, CancellationToken ct)
    {
        var now = clock.UtcNow;
        // Initial attempts only: uncertain SMTP outcomes are not automatically retried/reclaimed.
        var rows = await db.EmailOutboxMessages.AsNoTracking()
            .Where(x => x.Status == EmailDeliveryStatus.Pending && x.AttemptCount == 0
                && x.CreatedAt >= scope.NotBeforeUtc && x.ContentExpiresAt > now
                && scope.Recipients.Contains(x.RecipientEmail.ToUpper())
                && (x.DeduplicationKey.StartsWith("email:password-reset-otp:")
                    || x.DeduplicationKey.StartsWith("email:security-pin-reset:")))
            .OrderBy(x => x.CreatedAt).Take(20)
            .Select(x => new { x.Id, x.RecipientEmail, x.DeduplicationKey }).ToListAsync(ct);
        var count = 0;
        foreach (var row in rows)
        {
            var users = db.Users.AsNoTracking().Where(u => u.Email == row.RecipientEmail
                && u.SystemRole == SystemRole.SystemAdmin && u.Status == UserStatus.Active && u.EmailVerifiedAt != null);
            bool valid;
            const string passwordPrefix = "email:password-reset-otp:";
            const string pinPrefix = "email:security-pin-reset:";
            if (row.DeduplicationKey.StartsWith(passwordPrefix, StringComparison.Ordinal))
            {
                if (!Guid.TryParseExact(row.DeduplicationKey[passwordPrefix.Length..], "N", out var id)) continue;
                valid = await db.EmailVerificationTokens.AsNoTracking().AnyAsync(t => t.Id == id
                    && t.Purpose == EmailVerificationPurpose.ResetPassword && t.UsedAt == null && t.RevokedAt == null
                    && t.ExpiresAt > now && users.Any(u => u.Id == t.UserId), ct);
            }
            else
            {
                var hash = row.DeduplicationKey[pinPrefix.Length..];
                if (hash.Length != 64 || !hash.All(Uri.IsHexDigit)) continue;
                valid = await db.SecurityPinResetTokens.AsNoTracking().AnyAsync(t => t.TokenHash == hash
                    && t.Purpose == SecurityPinResetPurpose.SelfService && t.UsedAt == null && t.RevokedAt == null
                    && t.ExpiresAt > now && users.Any(u => u.Id == t.UserId), ct);
            }
            if (valid && await dispatcher.DispatchByIdAsync(row.Id, row.RecipientEmail, row.DeduplicationKey, ct)) count++;
        }
        return count;
    }
}

public sealed class AuthRecoveryEmailBackgroundService(
    IServiceScopeFactory scopes, AuthRecoveryDeliveryScope deliveryScope,
    ILogger<AuthRecoveryEmailBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                deadline.CancelAfter(TimeSpan.FromSeconds(30));
                await scope.ServiceProvider.GetRequiredService<AuthRecoveryEmailDispatcher>()
                    .DispatchEligibleAsync(deliveryScope, deadline.Token);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception) { logger.LogWarning("Scoped auth recovery delivery incomplete; inspect outbox metadata before retrying."); }
            try { await timer.WaitForNextTickAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
