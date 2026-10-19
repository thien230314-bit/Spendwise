using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Identity;
using SpendWise.Domain.Identity;
using SpendWise.Domain.Notifications;
using SpendWise.Infrastructure.Identity;
using SpendWise.Infrastructure.Notifications;
using SpendWise.Infrastructure.Persistence;
using Xunit;

namespace SpendWise.Tests;

public sealed class RegistrationEmailVerificationTests
{
    [Fact]
    public async Task Registration_queues_encrypted_e01_without_session_or_pin_challenge()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var email = $"e01-{Guid.NewGuid():N}@test.local";

        var response = await RegisterAsync(client, email);

        response.EnsureSuccessStatusCode();
        var pending = (await response.Content.ReadFromJsonAsync<EmailVerificationPendingResponse>())!;
        Assert.Equal("EMAIL_VERIFICATION_REQUIRED", pending.Status);
        Assert.Equal(60, pending.ResendAfterSeconds);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        var user = await db.Users.SingleAsync();
        Assert.Null(user.EmailVerifiedAt);
        Assert.Empty(await db.LoginChallenges.ToListAsync());
        Assert.Empty(await db.UserSessions.ToListAsync());
        var token = await db.EmailVerificationTokens.SingleAsync();
        Assert.NotEqual(IdentityTestData.DefaultEmailOtp, token.CodeHash);
        var emailRow = await db.EmailOutboxMessages.SingleAsync();
        Assert.DoesNotContain(IdentityTestData.DefaultEmailOtp, emailRow.HtmlBody);
        Assert.Contains(
            IdentityTestData.DefaultEmailOtp,
            scope.ServiceProvider.GetRequiredService<IEmailContentProtector>().Unprotect(emailRow.TextBody));
    }

    [Fact]
    public async Task Verification_is_single_use_and_welcome_is_queued_once()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var email = $"verify-{Guid.NewGuid():N}@test.local";
        (await RegisterAsync(client, email)).EnsureSuccessStatusCode();

        var first = await VerifyAsync(client, email, IdentityTestData.DefaultEmailOtp);
        var replay = await VerifyAsync(client, email, IdentityTestData.DefaultEmailOtp);

        first.EnsureSuccessStatusCode();
        var challenge = (await first.Content.ReadFromJsonAsync<LoginChallengeResponse>())!;
        Assert.Equal("PIN_SETUP_REQUIRED", challenge.Status);
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
        Assert.Equal("email_verification_failed", await ProblemCodeAsync(replay));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        Assert.NotNull((await db.Users.SingleAsync()).EmailVerifiedAt);
        Assert.NotNull((await db.EmailVerificationTokens.SingleAsync()).UsedAt);
        Assert.Equal(2, await db.EmailOutboxMessages.CountAsync());
        Assert.Single(await db.EmailOutboxMessages.Where(x =>
            x.DeduplicationKey.StartsWith("email:registration-welcome:")).ToListAsync());
    }

    [Fact]
    public async Task Expired_or_incorrect_otp_is_rejected_and_attempts_are_limited()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var expiredEmail = $"expired-{Guid.NewGuid():N}@test.local";
        (await RegisterAsync(client, expiredEmail)).EnsureSuccessStatusCode();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            var token = await db.EmailVerificationTokens.SingleAsync();
            token.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }
        var expired = await VerifyAsync(client, expiredEmail, IdentityTestData.DefaultEmailOtp);
        Assert.Equal(HttpStatusCode.BadRequest, expired.StatusCode);

        using var secondFactory = new SpendWiseApiFactory();
        using var secondClient = secondFactory.CreateHttpsClient();
        var email = $"wrong-{Guid.NewGuid():N}@test.local";
        (await RegisterAsync(secondClient, email)).EnsureSuccessStatusCode();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var wrong = await VerifyAsync(secondClient, email, "87654321");
            Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        }
        var correctAfterLimit = await VerifyAsync(secondClient, email, IdentityTestData.DefaultEmailOtp);
        Assert.Equal(HttpStatusCode.BadRequest, correctAfterLimit.StatusCode);
        using var secondScope = secondFactory.Services.CreateScope();
        var stored = await secondScope.ServiceProvider.GetRequiredService<SpendWiseDbContext>()
            .EmailVerificationTokens.SingleAsync();
        Assert.Equal(5, stored.FailedAttempts);
        Assert.NotNull(stored.RevokedAt);
    }

    [Fact]
    public async Task Resend_enforces_cooldown_and_hourly_account_limit()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var email = $"resend-{Guid.NewGuid():N}@test.local";
        (await RegisterAsync(client, email)).EnsureSuccessStatusCode();

        var cooldown = await ResendAsync(client, email);
        Assert.Equal(HttpStatusCode.TooManyRequests, cooldown.StatusCode);
        Assert.Equal("email_verification_cooldown", await ProblemCodeAsync(cooldown));

        for (var resend = 0; resend < 4; resend++)
        {
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
                var latest = await db.EmailVerificationTokens.OrderByDescending(x => x.CreatedAt).FirstAsync();
                latest.CreatedAt = DateTime.UtcNow.AddMinutes(-2);
                await db.SaveChangesAsync();
            }
            (await ResendAsync(client, email)).EnsureSuccessStatusCode();
        }

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            var latest = await db.EmailVerificationTokens.OrderByDescending(x => x.CreatedAt).FirstAsync();
            latest.CreatedAt = DateTime.UtcNow.AddMinutes(-2);
            await db.SaveChangesAsync();
        }
        var limited = await ResendAsync(client, email);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal("email_verification_rate_limited", await ProblemCodeAsync(limited));
    }

    [Fact]
    public async Task Unverified_user_cannot_login_but_existing_verified_user_is_compatible()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var pendingEmail = $"pending-{Guid.NewGuid():N}@test.local";
        (await RegisterAsync(client, pendingEmail)).EnsureSuccessStatusCode();
        var blocked = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(pendingEmail, "StrongPass1", "Tests"));
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
        Assert.Equal("email_verification_required", await ProblemCodeAsync(blocked));

        var verifiedEmail = $"verified-{Guid.NewGuid():N}@test.local";
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            var unique = Guid.NewGuid().ToString("N");
            db.Users.Add(new User
            {
                FullName = "Existing verified",
                Email = verifiedEmail,
                NormalizedEmail = verifiedEmail.ToUpperInvariant(),
                SpendId = unique,
                NormalizedSpendId = unique.ToUpperInvariant(),
                Phone = IdentityTestData.NewPhone(),
                PasswordHash = new PasswordService().Hash("StrongPass1"),
                SecurityPinHash = new SecurityPinService().Hash(IdentityTestData.DefaultPin),
                EmailVerifiedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }
        var compatible = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(verifiedEmail, "StrongPass1", "Tests"));
        compatible.EnsureSuccessStatusCode();
        Assert.Equal(
            "PIN_REQUIRED",
            (await compatible.Content.ReadFromJsonAsync<LoginChallengeResponse>())!.Status);
    }

    [Fact]
    public async Task Concurrent_dispatchers_claim_registration_email_only_once()
    {
        using var factory = new SpendWiseApiFactory(useIndependentDatabaseConnections: true);
        using var client = factory.CreateHttpsClient();
        var email = $"claim-{Guid.NewGuid():N}@test.local";
        (await RegisterAsync(client, email)).EnsureSuccessStatusCode();
        var sender = new CountingSender();

        using var firstScope = factory.Services.CreateScope();
        using var secondScope = factory.Services.CreateScope();
        var first = Dispatcher(firstScope.ServiceProvider, sender);
        var second = Dispatcher(secondScope.ServiceProvider, sender);
        await Task.WhenAll(
            first.DispatchPendingAsync(CancellationToken.None),
            second.DispatchPendingAsync(CancellationToken.None));

        Assert.Equal(1, sender.CallCount);
        using var verifyScope = factory.Services.CreateScope();
        var row = await verifyScope.ServiceProvider.GetRequiredService<SpendWiseDbContext>()
            .EmailOutboxMessages.SingleAsync();
        Assert.Equal(EmailDeliveryStatus.Sent, row.Status);
        Assert.Equal(string.Empty, row.HtmlBody);
        Assert.Equal(string.Empty, row.TextBody);
    }

    [Fact]
    public async Task Delivery_failure_keeps_registration_committed_and_records_safe_error_only()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var email = $"delivery-failure-{Guid.NewGuid():N}@test.local";
        (await RegisterAsync(client, email)).EnsureSuccessStatusCode();
        using var scope = factory.Services.CreateScope();
        var dispatcher = Dispatcher(scope.ServiceProvider, new FailingSender());

        Assert.Equal(0, await dispatcher.DispatchPendingAsync(CancellationToken.None));

        var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        Assert.Single(await db.Users.Where(x => x.NormalizedEmail == email.ToUpperInvariant()).ToListAsync());
        var row = await db.EmailOutboxMessages.SingleAsync();
        Assert.Equal(EmailDeliveryStatus.Pending, row.Status);
        Assert.Equal("delivery_failed", row.LastError);
        Assert.DoesNotContain("provider", row.LastError, StringComparison.OrdinalIgnoreCase);
    }

    private static EmailOutboxDispatcher Dispatcher(IServiceProvider services, ITransactionalEmailSender sender) =>
        new(
            services.GetRequiredService<SpendWiseDbContext>(),
            sender,
            services.GetRequiredService<IEmailContentProtector>(),
            services.GetRequiredService<IClock>(),
            NullLogger<EmailOutboxDispatcher>.Instance);

    private static Task<HttpResponseMessage> RegisterAsync(HttpClient client, string email) =>
        client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(
                "Email verification test",
                email,
                IdentityTestData.NewPhone(),
                "StrongPass1",
                "StrongPass1"));

    private static Task<HttpResponseMessage> VerifyAsync(HttpClient client, string email, string otp) =>
        client.PostAsJsonAsync(
            "/api/auth/email-verification/verify",
            new VerifyRegistrationEmailRequest(email, otp, "Tests"));

    private static Task<HttpResponseMessage> ResendAsync(HttpClient client, string email) =>
        client.PostAsJsonAsync(
            "/api/auth/email-verification/resend",
            new ResendRegistrationEmailRequest(email));

    private static async Task<string?> ProblemCodeAsync(HttpResponseMessage response)
    {
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        return problem.GetProperty("code").GetString();
    }

    private sealed class CountingSender : ITransactionalEmailSender
    {
        private int calls;
        public int CallCount => calls;

        public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref calls);
            await Task.Delay(100, cancellationToken);
        }
    }

    private sealed class FailingSender : ITransactionalEmailSender
    {
        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("provider secret payload must not be stored");
    }
}
