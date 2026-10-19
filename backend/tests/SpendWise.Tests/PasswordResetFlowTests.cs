using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Net.Http.Json;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Authentication;
using SpendWise.Application.Features.Authentication.Commands.ForgotPassword;
using SpendWise.Application.Features.Authentication.Commands.ResetPassword;
using SpendWise.Application.Identity;
using SpendWise.Domain.Identity;
using SpendWise.Domain.Notifications;
using SpendWise.Infrastructure.Identity;
using SpendWise.Infrastructure.Notifications;
using SpendWise.Infrastructure.Services;
using Xunit;

namespace SpendWise.Tests;

// Các test này bảo vệ OTP E05, transaction reset E06 và việc thu hồi phiên cũ.
public sealed class PasswordResetFlowTests
{
    [Fact]
    public async Task Unknown_account_returns_normally_without_creating_otp_or_email()
    {
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();

        await ForgotHandler(fixture).Handle(
            new ForgotPasswordCommand(
                new ForgotPasswordRequest("unknown@test.local"),
                "203.0.113.10"),
            default);

        Assert.Empty(await fixture.Db.EmailVerificationTokens.ToListAsync());
        Assert.Empty(await fixture.Db.EmailOutboxMessages.ToListAsync());
    }

    [Fact]
    public async Task Http_contract_accepts_identifier_otp_and_new_password()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var email = $"http-reset-{Guid.NewGuid():N}@test.local";
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWise.Infrastructure.Persistence.SpendWiseDbContext>();
            var unique = Guid.NewGuid().ToString("N");
            db.Users.Add(new User
            {
                FullName = "HTTP reset",
                Email = email,
                NormalizedEmail = email.ToUpperInvariant(),
                SpendId = unique,
                NormalizedSpendId = unique.ToUpperInvariant(),
                Phone = IdentityTestData.NewPhone(),
                PasswordHash = new PasswordService().Hash("OldPassword1"),
                SecurityPinHash = new SecurityPinService().Hash(IdentityTestData.DefaultPin),
                EmailVerifiedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var forgot = await client.PostAsJsonAsync(
            "/api/auth/forgot-password",
            new ForgotPasswordRequest(email));
        var reset = await client.PostAsJsonAsync(
            "/api/auth/reset-password",
            new ResetPasswordRequest(email, IdentityTestData.DefaultEmailOtp, "NewPassword1"));

        Assert.Equal(HttpStatusCode.Accepted, forgot.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        var oldLogin = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, "OldPassword1"));
        var newLogin = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, "NewPassword1"));
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Forgot_password_queues_encrypted_e05_to_verified_email_only(bool useEmail)
    {
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var passwords = new TestPasswordService();
        var user = await AddPasswordUserAsync(fixture, passwords, "otp-storage");
        user.Phone = IdentityTestData.NewPhone();
        await fixture.Db.SaveChangesAsync();

        await ForgotHandler(fixture).Handle(
            new ForgotPasswordCommand(
                new ForgotPasswordRequest(useEmail ? user.Email : user.Phone),
                "203.0.113.11"),
            default);

        var token = await fixture.Db.EmailVerificationTokens.SingleAsync();
        Assert.Equal(EmailVerificationPurpose.ResetPassword, token.Purpose);
        Assert.NotEqual(IdentityTestData.DefaultEmailOtp, token.CodeHash);
        Assert.Equal(64, token.CodeHash.Length);
        var email = await fixture.Db.EmailOutboxMessages.SingleAsync();
        Assert.Equal(user.Email, email.RecipientEmail);
        Assert.Equal(token.ExpiresAt, email.ContentExpiresAt);
        Assert.StartsWith("protected:", email.TextBody, StringComparison.Ordinal);
        Assert.Contains(
            IdentityTestData.DefaultEmailOtp,
            new TestEmailContentProtector().Unprotect(email.TextBody));
    }

    [Fact]
    public async Task Unverified_account_receives_no_reset_email()
    {
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var user = await fixture.AddUserAsync("unverified-reset");
        user.EmailVerifiedAt = null;
        await fixture.Db.SaveChangesAsync();

        await ForgotHandler(fixture).Handle(
            new ForgotPasswordCommand(new ForgotPasswordRequest(user.Email), "203.0.113.12"),
            default);

        Assert.Empty(await fixture.Db.EmailVerificationTokens.ToListAsync());
        Assert.Empty(await fixture.Db.EmailOutboxMessages.ToListAsync());
    }

    [Fact]
    public async Task Valid_reset_otp_changes_password_once_and_queues_one_e06()
    {
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var passwords = new TestPasswordService();
        var user = await AddPasswordUserAsync(fixture, passwords, "valid-reset");
        user.PasswordFailedAttempts = 5;
        user.PasswordLockoutUntil = fixture.Clock.UtcNow.AddMinutes(5);
        await fixture.Db.SaveChangesAsync();
        await IssueResetOtpAsync(fixture, user);
        fixture.Db.ChangeTracker.Clear();
        var handler = ResetHandler(fixture, passwords);

        await handler.Handle(Command(user.Email), default);
        var replay = await Assert.ThrowsAsync<AppException>(() =>
            handler.Handle(Command(user.Email), default));

        fixture.Db.ChangeTracker.Clear();
        var resetUser = (await fixture.Db.Users.FindAsync(user.Id))!;
        Assert.True(passwords.Verify("NewPassword1", resetUser.PasswordHash));
        Assert.Equal(0, resetUser.PasswordFailedAttempts);
        Assert.Null(resetUser.PasswordLockoutUntil);
        Assert.NotNull((await fixture.Db.EmailVerificationTokens.SingleAsync()).UsedAt);
        Assert.Equal("invalid_password_reset_token", replay.Code);
        var successEmail = await fixture.Db.EmailOutboxMessages.SingleAsync(item =>
            item.DeduplicationKey.StartsWith("email:password-reset-success:"));
        var successText = new TestEmailContentProtector().Unprotect(successEmail.TextBody);
        Assert.Contains("Mật khẩu", successText);
        Assert.DoesNotContain("NewPassword1", successText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Incorrect_expired_and_reused_otp_are_rejected()
    {
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var passwords = new TestPasswordService();
        var user = await AddPasswordUserAsync(fixture, passwords, "invalid-reset");
        await IssueResetOtpAsync(fixture, user);
        var handler = ResetHandler(fixture, passwords);

        var incorrect = await Assert.ThrowsAsync<AppException>(() =>
            handler.Handle(Command(user.Email, "87654321"), default));
        Assert.Equal("invalid_password_reset_token", incorrect.Code);
        fixture.Db.ChangeTracker.Clear();
        var token = await fixture.Db.EmailVerificationTokens.SingleAsync();
        Assert.Equal(1, token.FailedAttempts);

        token.ExpiresAt = fixture.Clock.UtcNow.AddSeconds(-1);
        await fixture.Db.SaveChangesAsync();
        fixture.Db.ChangeTracker.Clear();
        var expired = await Assert.ThrowsAsync<AppException>(() =>
            handler.Handle(Command(user.Email), default));
        Assert.Equal("invalid_password_reset_token", expired.Code);
    }

    [Fact]
    public async Task Failed_attempt_limit_revokes_reset_otp()
    {
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var passwords = new TestPasswordService();
        var user = await AddPasswordUserAsync(fixture, passwords, "attempt-limit");
        await IssueResetOtpAsync(fixture, user);
        var handler = ResetHandler(fixture, passwords);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            await Assert.ThrowsAsync<AppException>(() =>
                handler.Handle(Command(user.Email, "87654321"), default));
        }

        fixture.Db.ChangeTracker.Clear();
        var token = await fixture.Db.EmailVerificationTokens.SingleAsync();
        Assert.Equal(5, token.FailedAttempts);
        Assert.NotNull(token.RevokedAt);
        await Assert.ThrowsAsync<AppException>(() => handler.Handle(Command(user.Email), default));
    }

    [Fact]
    public async Task Registration_otp_cannot_be_used_for_password_reset()
    {
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var passwords = new TestPasswordService();
        var user = await AddPasswordUserAsync(fixture, passwords, "purpose-separation");
        fixture.Db.EmailVerificationTokens.Add(new EmailVerificationToken
        {
            UserId = user.Id,
            Purpose = EmailVerificationPurpose.Registration,
            CodeHash = new TestEmailVerificationOtpService().HashOtp(
                user.Id,
                EmailVerificationSupport.RegistrationPurpose,
                IdentityTestData.DefaultEmailOtp),
            ExpiresAt = fixture.Clock.UtcNow.AddMinutes(10)
        });
        await fixture.Db.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<AppException>(() =>
            ResetHandler(fixture, passwords).Handle(Command(user.Email), default));

        Assert.Equal("invalid_password_reset_token", error.Code);
        Assert.True(passwords.Verify("OldPassword1", (await fixture.Db.Users.FindAsync(user.Id))!.PasswordHash));
    }

    [Fact]
    public async Task Resend_cooldown_rate_limit_and_previous_otp_invalidation_are_enforced()
    {
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var passwords = new TestPasswordService();
        var user = await AddPasswordUserAsync(fixture, passwords, "resend-reset");
        var policy = new TestEmailVerificationPolicy
        {
            ResendCooldown = TimeSpan.FromMinutes(1),
            MaximumSendsPerHourPerAccount = 2
        };
        var handler = ForgotHandler(fixture, policy);
        var command = new ForgotPasswordCommand(
            new ForgotPasswordRequest(user.Email),
            "203.0.113.13");
        await handler.Handle(command, default);

        var cooldown = await Assert.ThrowsAsync<AppException>(() => handler.Handle(command, default));
        Assert.Equal("password_reset_cooldown", cooldown.Code);
        var firstToken = await fixture.Db.EmailVerificationTokens.SingleAsync();
        firstToken.CreatedAt = fixture.Clock.UtcNow.AddMinutes(-2);
        await fixture.Db.SaveChangesAsync();
        await handler.Handle(command, default);

        fixture.Db.ChangeTracker.Clear();
        var tokens = await fixture.Db.EmailVerificationTokens.OrderBy(item => item.CreatedAt).ToListAsync();
        Assert.Equal(2, tokens.Count);
        Assert.NotNull(tokens[0].RevokedAt);
        var firstEmail = await fixture.Db.EmailOutboxMessages.SingleAsync(item =>
            item.DeduplicationKey == $"email:password-reset-otp:{tokens[0].Id:N}");
        Assert.Equal(EmailDeliveryStatus.Failed, firstEmail.Status);
        Assert.Equal(string.Empty, firstEmail.TextBody);

        var latest = tokens[1];
        latest.CreatedAt = fixture.Clock.UtcNow.AddMinutes(-2);
        await fixture.Db.SaveChangesAsync();
        var limited = await Assert.ThrowsAsync<AppException>(() => handler.Handle(command, default));
        Assert.Equal("password_reset_rate_limited", limited.Code);
    }

    [Fact]
    public async Task Password_reset_send_rate_is_limited_per_ip_across_accounts()
    {
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var passwords = new TestPasswordService();
        var first = await AddPasswordUserAsync(fixture, passwords, "ip-limit-first");
        var second = await AddPasswordUserAsync(fixture, passwords, "ip-limit-second");
        var policy = new TestEmailVerificationPolicy
        {
            MaximumSendsPerHourPerAccount = 5,
            MaximumSendsPerHourPerIp = 1
        };
        var handler = ForgotHandler(fixture, policy);
        const string sharedIp = "203.0.113.14";
        await handler.Handle(
            new ForgotPasswordCommand(new ForgotPasswordRequest(first.Email), sharedIp),
            default);

        var limited = await Assert.ThrowsAsync<AppException>(() => handler.Handle(
            new ForgotPasswordCommand(new ForgotPasswordRequest(second.Email), sharedIp),
            default));

        Assert.Equal("password_reset_rate_limited", limited.Code);
        Assert.Single(await fixture.Db.EmailVerificationTokens.ToListAsync());
    }

    [Fact]
    public async Task Password_reset_revokes_sessions_and_refresh_tokens_atomically()
    {
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var passwords = new TestPasswordService();
        var user = await AddPasswordUserAsync(fixture, passwords, "session-reset");
        var session = await fixture.AddSessionAsync(user.Id);
        var refresh = await fixture.AddRefreshTokenAsync(
            user.Id,
            "refresh-before-reset",
            userSessionId: session.Id);
        await IssueResetOtpAsync(fixture, user);
        fixture.Db.ChangeTracker.Clear();

        await ResetHandler(fixture, passwords).Handle(Command(user.Email), default);

        fixture.Db.ChangeTracker.Clear();
        Assert.NotNull((await fixture.Db.UserSessions.FindAsync(session.Id))!.RevokedAt);
        Assert.NotNull((await fixture.Db.RefreshTokens.FindAsync(refresh.Id))!.RevokedAt);
    }

    [Fact]
    public async Task Database_failure_rolls_back_otp_password_sessions_and_e06()
    {
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var passwords = new TestPasswordService();
        var user = await AddPasswordUserAsync(fixture, passwords, "rollback-reset");
        var oldHash = user.PasswordHash;
        var session = await fixture.AddSessionAsync(user.Id);
        var refresh = await fixture.AddRefreshTokenAsync(
            user.Id,
            "rollback-refresh",
            userSessionId: session.Id);
        await IssueResetOtpAsync(fixture, user);
        await fixture.Db.Database.ExecuteSqlRawAsync(
            """
            CREATE TRIGGER fail_reset_password_update
            BEFORE UPDATE ON Users
            WHEN NEW.PasswordHash = 'hash:NewPassword1'
            BEGIN
                SELECT RAISE(ABORT, 'forced reset failure');
            END;
            """);
        fixture.Db.ChangeTracker.Clear();

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            ResetHandler(fixture, passwords).Handle(Command(user.Email), default));

        fixture.Db.ChangeTracker.Clear();
        Assert.Equal(oldHash, (await fixture.Db.Users.FindAsync(user.Id))!.PasswordHash);
        Assert.Null((await fixture.Db.EmailVerificationTokens.SingleAsync()).UsedAt);
        Assert.Null((await fixture.Db.UserSessions.FindAsync(session.Id))!.RevokedAt);
        Assert.Null((await fixture.Db.RefreshTokens.FindAsync(refresh.Id))!.RevokedAt);
        Assert.Empty(await fixture.Db.EmailOutboxMessages.Where(item =>
            item.DeduplicationKey.StartsWith("email:password-reset-success:")).ToListAsync());
    }

    [Fact]
    public async Task E06_delivery_failure_does_not_undo_committed_password_reset()
    {
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var passwords = new TestPasswordService();
        var user = await AddPasswordUserAsync(fixture, passwords, "delivery-failure");
        await IssueResetOtpAsync(fixture, user);
        await ResetHandler(fixture, passwords).Handle(Command(user.Email), default);
        fixture.Db.ChangeTracker.Clear();
        var e05 = await fixture.Db.EmailOutboxMessages.SingleAsync(item =>
            item.DeduplicationKey.StartsWith("email:password-reset-otp:"));
        e05.Status = EmailDeliveryStatus.Sent;
        e05.HtmlBody = string.Empty;
        e05.TextBody = string.Empty;
        await fixture.Db.SaveChangesAsync();
        var dispatcher = new EmailOutboxDispatcher(
            fixture.Db,
            new FailingSender(),
            new TestEmailContentProtector(),
            fixture.Clock,
            NullLogger<EmailOutboxDispatcher>.Instance);

        Assert.Equal(0, await dispatcher.DispatchPendingAsync(default));

        fixture.Db.ChangeTracker.Clear();
        Assert.True(passwords.Verify(
            "NewPassword1",
            (await fixture.Db.Users.FindAsync(user.Id))!.PasswordHash));
        var e06 = await fixture.Db.EmailOutboxMessages.SingleAsync(item =>
            item.DeduplicationKey.StartsWith("email:password-reset-success:"));
        Assert.Equal(EmailDeliveryStatus.Pending, e06.Status);
        Assert.Equal("delivery_failed", e06.LastError);
    }

    private static ForgotPasswordCommandHandler ForgotHandler(
        TargetedCorrectnessFixture fixture,
        IEmailVerificationPolicy? policy = null) =>
        new(fixture.Db, EmailVerificationTestFactory.Create(fixture.Db, fixture.Clock, policy));

    private static ResetPasswordCommandHandler ResetHandler(
        TargetedCorrectnessFixture fixture,
        TestPasswordService passwords,
        IEmailVerificationPolicy? policy = null)
    {
        var effectivePolicy = policy ?? new TestEmailVerificationPolicy();
        return new ResetPasswordCommandHandler(
            fixture.Db,
            new TestEmailVerificationOtpService(),
            effectivePolicy,
            passwords,
            new AuthenticationSupport(fixture.Db, new TargetedTokenService(), fixture.Clock),
            EmailVerificationTestFactory.Create(fixture.Db, fixture.Clock, effectivePolicy),
            fixture.Clock);
    }

    private static async Task IssueResetOtpAsync(
        TargetedCorrectnessFixture fixture,
        User user,
        IEmailVerificationPolicy? policy = null)
    {
        await ForgotHandler(fixture, policy).Handle(
            new ForgotPasswordCommand(
                new ForgotPasswordRequest(user.Email),
                "203.0.113.20"),
            default);
    }

    private static ResetPasswordCommand Command(
        string identifier,
        string otp = IdentityTestData.DefaultEmailOtp) =>
        new(new ResetPasswordRequest(identifier, otp, "NewPassword1"));

    private static async Task<User> AddPasswordUserAsync(
        TargetedCorrectnessFixture fixture,
        TestPasswordService passwords,
        string name)
    {
        var user = await fixture.AddUserAsync(name);
        user.PasswordHash = passwords.Hash("OldPassword1");
        await fixture.Db.SaveChangesAsync();
        return user;
    }

    private sealed class TestPasswordService : IPasswordService
    {
        public string Hash(string password) => $"hash:{password}";
        public bool Verify(string password, string encodedHash) => encodedHash == Hash(password);
    }

    private sealed class FailingSender : ITransactionalEmailSender
    {
        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("provider detail must remain private");
    }
}
