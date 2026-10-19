using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Authentication;
using SpendWise.Application.Features.Authentication.Commands.CompleteSecurityPinLogin;
using SpendWise.Application.Features.Authentication.Commands.RefreshToken;
using SpendWise.Application.Features.Authentication.Commands.SetupSecurityPin;
using SpendWise.Application.Features.Users.Commands.UpdatePreferences;
using SpendWise.Application.Identity;
using SpendWise.Domain.Identity;
using SpendWise.Domain.Notifications;
using SpendWise.Infrastructure.Identity;
using SpendWise.Infrastructure.Notifications;
using SpendWise.Infrastructure.Persistence;
using Xunit;

namespace SpendWise.Tests;

// Khóa hành vi E07/E08 tại đúng bước hoàn tất đăng nhập bằng PIN.
public sealed class LoginEmailEventsTests
{
    [Fact]
    public async Task Initial_pin_setup_establishes_hashed_trust_without_E07_or_E08()
    {
        var clock = new MutableClock(new DateTime(2026, 9, 25, 2, 0, 0, DateTimeKind.Utc));
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync(clock);
        var user = await fixture.AddUserAsync("Onboarding user");
        user.SecurityPinHash = null;
        user.LastLoginAt = null;
        await fixture.Db.SaveChangesAsync();
        var challenge = await Challenges(fixture.Db, clock).CreateAsync(
            user, "First browser", "127.0.0.1", "Browser/1", default);
        var handler = new SetupSecurityPinCommandHandler(
            fixture.Db,
            new SecurityPinService(),
            Challenges(fixture.Db, clock),
            new AuthenticationSupport(fixture.Db, new TargetedTokenService(), clock),
            InteractiveLoginTestFactory.Create(fixture.Db, clock),
            clock);

        var result = await handler.Handle(new SetupSecurityPinCommand(
            new SetupSecurityPinRequest(
                challenge.ChallengeToken,
                TargetedCorrectnessFixture.SecurityPin,
                TargetedCorrectnessFixture.SecurityPin)), default);

        Assert.NotNull(result.DeviceTrustToken);
        Assert.Equal(64, result.DeviceTrustToken!.Length);
        var session = await fixture.Db.UserSessions.SingleAsync();
        Assert.NotEqual(result.DeviceTrustToken, session.DeviceIdentifier);
        Assert.Equal(new DeviceTrustTokenService().HashToken(result.DeviceTrustToken), session.DeviceIdentifier);
        Assert.Empty(await fixture.Db.EmailOutboxMessages.ToListAsync());
    }

    [Theory]
    [InlineData(13.999, true, false)]
    [InlineData(14.0, true, true)]
    [InlineData(15.0, true, true)]
    [InlineData(20.0, false, false)]
    public async Task E07_uses_elapsed_UTC_and_privacy_safe_preference(
        double elapsedDays,
        bool optedIn,
        bool expected)
    {
        var clock = new MutableClock(new DateTime(2026, 9, 25, 4, 0, 0, DateTimeKind.Utc));
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync(clock);
        var user = await fixture.AddUserAsync("Welcome user");
        user.EngagementEmailOptIn = optedIn;
        user.LastLoginAt = clock.UtcNow.Subtract(TimeSpan.FromDays(elapsedDays));
        await fixture.Db.SaveChangesAsync();
        var trustedToken = await AddTrustedDeviceAsync(fixture.Db, user.Id, clock.UtcNow.AddDays(-1));

        var result = await CompleteAsync(fixture.Db, user, clock, trustedToken);

        Assert.NotEqual(trustedToken, result.DeviceTrustToken);
        Assert.Equal(expected ? 1 : 0, await fixture.Db.EmailOutboxMessages.CountAsync(
            item => item.DeduplicationKey.StartsWith("login-welcome-back:")));
        Assert.Empty(await fixture.Db.EmailOutboxMessages.Where(
            item => item.DeduplicationKey.StartsWith("login-new-device:")).ToListAsync());
    }

    [Fact]
    public async Task Recognized_token_remains_recognized_when_IP_and_user_agent_change()
    {
        var clock = new MutableClock(new DateTime(2026, 9, 25, 5, 0, 0, DateTimeKind.Utc));
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync(clock);
        var user = await fixture.AddUserAsync("Recognized user");
        user.LastLoginAt = clock.UtcNow.AddDays(-1);
        await fixture.Db.SaveChangesAsync();
        var trustedToken = await AddTrustedDeviceAsync(fixture.Db, user.Id, clock.UtcNow.AddDays(-10));

        await CompleteAsync(
            fixture.Db,
            user,
            clock,
            trustedToken,
            "Renamed browser",
            "203.0.113.99",
            "Completely-Different-Browser/9");

        Assert.Empty(await fixture.Db.EmailOutboxMessages.ToListAsync());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-valid-device-token")]
    public async Task Missing_or_invalid_token_sends_E08_after_successful_PIN(string? suppliedToken)
    {
        var clock = new MutableClock(new DateTime(2026, 9, 25, 6, 0, 0, DateTimeKind.Utc));
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync(clock);
        var user = await fixture.AddUserAsync("New device user");
        user.EngagementEmailOptIn = false;
        user.LastLoginAt = clock.UtcNow.AddDays(-30);
        await fixture.Db.SaveChangesAsync();

        var result = await CompleteAsync(fixture.Db, user, clock, suppliedToken);

        Assert.NotNull(result.DeviceTrustToken);
        var email = await fixture.Db.EmailOutboxMessages.SingleAsync(
            item => item.DeduplicationKey.StartsWith("login-new-device:"));
        Assert.Equal(user.Email, email.RecipientEmail);
        Assert.Empty(await fixture.Db.EmailOutboxMessages.Where(
            item => item.DeduplicationKey.StartsWith("login-welcome-back:")).ToListAsync());
        var bodies = new TestEmailContentProtector().Unprotect(email.HtmlBody)
            + new TestEmailContentProtector().Unprotect(email.TextBody);
        Assert.DoesNotContain(result.DeviceTrustToken!, bodies);
        Assert.DoesNotContain("wallet", bodies, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Token_for_another_user_is_not_recognized()
    {
        var clock = new MutableClock(new DateTime(2026, 9, 25, 7, 0, 0, DateTimeKind.Utc));
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync(clock);
        var owner = await fixture.AddUserAsync("Token owner");
        var other = await fixture.AddUserAsync("Other user");
        var ownerToken = await AddTrustedDeviceAsync(fixture.Db, owner.Id, clock.UtcNow.AddDays(-1));

        await CompleteAsync(fixture.Db, other, clock, ownerToken);

        Assert.Single(await fixture.Db.EmailOutboxMessages.Where(
            item => item.DeduplicationKey.StartsWith("login-new-device:")).ToListAsync());
    }

    [Fact]
    public async Task Expired_trust_sends_E08_clears_old_hash_and_issues_fresh_token()
    {
        var clock = new MutableClock(new DateTime(2026, 9, 25, 8, 0, 0, DateTimeKind.Utc));
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync(clock);
        var user = await fixture.AddUserAsync("Expired trust user");
        var expiredToken = await AddTrustedDeviceAsync(fixture.Db, user.Id, clock.UtcNow.AddDays(-31));

        var result = await CompleteAsync(
            fixture.Db,
            user,
            clock,
            expiredToken,
            lifetime: TimeSpan.FromDays(30));

        Assert.NotEqual(expiredToken, result.DeviceTrustToken);
        Assert.Single(await fixture.Db.EmailOutboxMessages.Where(
            item => item.DeduplicationKey.StartsWith("login-new-device:")).ToListAsync());
        Assert.DoesNotContain(await fixture.Db.UserSessions.AsNoTracking().ToListAsync(),
            session => session.DeviceIdentifier == new DeviceTrustTokenService().HashToken(expiredToken));
    }

    [Fact]
    public async Task E08_suppresses_E07_even_when_welcome_back_is_eligible()
    {
        var clock = new MutableClock(new DateTime(2026, 9, 25, 9, 0, 0, DateTimeKind.Utc));
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync(clock);
        var user = await fixture.AddUserAsync("Priority user");
        user.EngagementEmailOptIn = true;
        user.LastLoginAt = clock.UtcNow.AddDays(-20);
        await fixture.Db.SaveChangesAsync();

        await CompleteAsync(fixture.Db, user, clock, null);

        Assert.Single(await fixture.Db.EmailOutboxMessages.Where(
            item => item.DeduplicationKey.StartsWith("login-new-device:")).ToListAsync());
        Assert.Empty(await fixture.Db.EmailOutboxMessages.Where(
            item => item.DeduplicationKey.StartsWith("login-welcome-back:")).ToListAsync());
    }

    [Fact]
    public async Task Reused_challenge_cannot_create_second_session_or_email()
    {
        var clock = new MutableClock(new DateTime(2026, 9, 25, 10, 0, 0, DateTimeKind.Utc));
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync(clock);
        var user = await fixture.AddUserAsync("Retry user");
        var challenges = Challenges(fixture.Db, clock);
        var challenge = await challenges.CreateAsync(user, "Retry browser", null, null, default);
        var handler = CompleteHandler(fixture.Db, clock);
        var command = new CompleteSecurityPinLoginCommand(
            new CompleteSecurityPinLoginRequest(
                challenge.ChallengeToken,
                TargetedCorrectnessFixture.SecurityPin));

        await handler.Handle(command, default);
        await Assert.ThrowsAsync<AppException>(() => handler.Handle(command, default));

        Assert.Single(await fixture.Db.UserSessions.ToListAsync());
        Assert.Single(await fixture.Db.RefreshTokens.ToListAsync());
        Assert.Single(await fixture.Db.EmailOutboxMessages.ToListAsync());
    }

    [Fact]
    public async Task Device_token_cannot_bypass_challenge_or_PIN()
    {
        var clock = new MutableClock(new DateTime(2026, 9, 25, 11, 0, 0, DateTimeKind.Utc));
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync(clock);
        var user = await fixture.AddUserAsync("No bypass user");
        var trustedToken = await AddTrustedDeviceAsync(fixture.Db, user.Id, clock.UtcNow.AddDays(-1));
        var handler = CompleteHandler(fixture.Db, clock);

        await Assert.ThrowsAsync<AppException>(() => handler.Handle(
            new CompleteSecurityPinLoginCommand(new CompleteSecurityPinLoginRequest(
                "not-a-valid-challenge",
                TargetedCorrectnessFixture.SecurityPin,
                trustedToken)), default));
        var challenge = await Challenges(fixture.Db, clock).CreateAsync(user, "Browser", null, null, default);
        await Assert.ThrowsAsync<AppException>(() => handler.Handle(
            new CompleteSecurityPinLoginCommand(new CompleteSecurityPinLoginRequest(
                challenge.ChallengeToken,
                "654321",
                trustedToken)), default));

        Assert.Single(await fixture.Db.UserSessions.ToListAsync());
        Assert.Empty(await fixture.Db.RefreshTokens.ToListAsync());
        Assert.Empty(await fixture.Db.EmailOutboxMessages.ToListAsync());
    }

    [Fact]
    public async Task Refresh_rotation_does_not_trigger_E07_or_E08()
    {
        var clock = new MutableClock(new DateTime(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc));
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync(clock);
        var user = await fixture.AddUserAsync("Refresh user");
        user.LastLoginAt = clock.UtcNow.AddDays(-20);
        user.EngagementEmailOptIn = true;
        await fixture.Db.SaveChangesAsync();
        var trustedToken = await AddTrustedDeviceAsync(fixture.Db, user.Id, clock.UtcNow.AddDays(-1));
        var tokens = new TargetedTokenService();
        var login = await CompleteAsync(fixture.Db, user, clock, trustedToken, tokens: tokens);
        fixture.Db.EmailOutboxMessages.RemoveRange(fixture.Db.EmailOutboxMessages);
        await fixture.Db.SaveChangesAsync();

        await new RefreshTokenCommandHandler(fixture.Db, tokens, clock).Handle(
            new RefreshTokenCommand(new RefreshRequest(login.RefreshToken, "Refresh browser"), null, null),
            default);

        Assert.Empty(await fixture.Db.EmailOutboxMessages.ToListAsync());
        Assert.Equal(2, await fixture.Db.RefreshTokens.CountAsync());
        Assert.Equal(2, await fixture.Db.UserSessions.CountAsync(x => x.UserId == user.Id && x.RevokedAt == null));
    }

    [Fact]
    public async Task E08_outbox_failure_rolls_back_session_LastLogin_and_device_rotation()
    {
        var clock = new MutableClock(new DateTime(2026, 9, 25, 13, 0, 0, DateTimeKind.Utc));
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync(clock);
        var user = await fixture.AddUserAsync("Rollback login user");
        var previousLogin = clock.UtcNow.AddDays(-1);
        user.LastLoginAt = previousLogin;
        await fixture.Db.SaveChangesAsync();
        var challenge = await Challenges(fixture.Db, clock).CreateAsync(user, "Rollback browser", null, null, default);
        await fixture.Db.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER fail_login_email_outbox
            BEFORE INSERT ON EmailOutboxMessages
            WHEN NEW.DeduplicationKey LIKE 'login-new-device:%'
            BEGIN
                SELECT RAISE(ABORT, 'forced login email outbox failure');
            END;
            """);

        await Assert.ThrowsAnyAsync<Exception>(() => CompleteHandler(fixture.Db, clock).Handle(
            new CompleteSecurityPinLoginCommand(new CompleteSecurityPinLoginRequest(
                challenge.ChallengeToken,
                TargetedCorrectnessFixture.SecurityPin)), default));

        Assert.Empty(await fixture.Db.UserSessions.ToListAsync());
        Assert.Empty(await fixture.Db.RefreshTokens.ToListAsync());
        Assert.Empty(await fixture.Db.EmailOutboxMessages.ToListAsync());
        Assert.Equal(previousLogin, (await fixture.Db.Users.AsNoTracking().SingleAsync(x => x.Id == user.Id)).LastLoginAt);
        var challengeId = await ChallengeIdAsync(challenge.ChallengeToken, fixture.Db);
        Assert.Null((await fixture.Db.LoginChallenges.AsNoTracking().SingleAsync(x => x.Id == challengeId)).UsedAt);
    }

    [Fact]
    public async Task Delivery_failure_does_not_revoke_successful_login()
    {
        var clock = new MutableClock(new DateTime(2026, 9, 25, 14, 0, 0, DateTimeKind.Utc));
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync(clock);
        var user = await fixture.AddUserAsync("Delivery login user");
        var result = await CompleteAsync(fixture.Db, user, clock, null);
        var dispatcher = new EmailOutboxDispatcher(
            fixture.Db,
            new FailingSender(),
            new TestEmailContentProtector(),
            clock,
            NullLogger<EmailOutboxDispatcher>.Instance);

        await dispatcher.DispatchPendingAsync(default);

        Assert.False(string.IsNullOrWhiteSpace(result.AccessToken));
        Assert.Single(await fixture.Db.UserSessions.Where(x => x.RevokedAt == null).ToListAsync());
        Assert.Single(await fixture.Db.RefreshTokens.Where(x => x.RevokedAt == null).ToListAsync());
        var email = await fixture.Db.EmailOutboxMessages.SingleAsync();
        Assert.Equal(EmailDeliveryStatus.Pending, email.Status);
        Assert.Equal("delivery_failed", email.LastError);
    }

    [Fact]
    public async Task Engagement_email_preference_is_explicit_and_old_clients_preserve_it()
    {
        var clock = new MutableClock(new DateTime(2026, 9, 25, 15, 0, 0, DateTimeKind.Utc));
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync(clock);
        var user = await fixture.AddUserAsync("Preference user");
        var handler = new UpdatePreferencesCommandHandler(fixture.Db);

        var enabled = await handler.Handle(new UpdatePreferencesCommand(
            user.Id,
            new UpdatePreferencesRequest("vi-VN", "system", "VND", true)), default);
        var preserved = await handler.Handle(new UpdatePreferencesCommand(
            user.Id,
            new UpdatePreferencesRequest("en-US", "dark", "VND")), default);

        Assert.True(enabled.EngagementEmailOptIn);
        Assert.True(preserved.EngagementEmailOptIn);
        Assert.True((await fixture.Db.Users.AsNoTracking().SingleAsync(x => x.Id == user.Id))
            .EngagementEmailOptIn);
    }

    [Fact]
    public async Task Concurrent_completion_creates_only_one_session_refresh_token_and_email()
    {
        var clock = new MutableClock(new DateTime(2026, 9, 25, 16, 0, 0, DateTimeKind.Utc));
        var databasePath = Path.Combine(
            Path.GetTempPath(),
            $"spendwise-login-{Guid.NewGuid():N}.db");
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            DefaultTimeout = 30,
            Pooling = false
        }.ToString();
        var options = new DbContextOptionsBuilder<SpendWiseDbContext>()
            .UseSqlite(connectionString)
            .Options;

        try
        {
            Guid userId;
            LoginChallengeResponse challenge;
            await using (var setup = new SpendWiseDbContext(options, clock))
            {
                await setup.Database.EnsureCreatedAsync();
                var unique = Guid.NewGuid().ToString("N");
                var user = new User
                {
                    FullName = "Concurrent login user",
                    Email = $"{unique}@test.local",
                    NormalizedEmail = $"{unique}@test.local".ToUpperInvariant(),
                    SpendId = unique,
                    NormalizedSpendId = unique.ToUpperInvariant(),
                    PasswordHash = "test-hash",
                    SecurityPinHash = new SecurityPinService().Hash(TargetedCorrectnessFixture.SecurityPin),
                    Status = UserStatus.Active
                };
                setup.Users.Add(user);
                await setup.SaveChangesAsync();
                userId = user.Id;
                challenge = await Challenges(setup, clock).CreateAsync(
                    user,
                    "Concurrent browser",
                    "127.0.0.1",
                    "SpendWise-Tests/1",
                    default);
            }

            async Task<bool> TryCompleteAsync()
            {
                await using var db = new SpendWiseDbContext(options, clock);
                try
                {
                    await CompleteHandler(db, clock).Handle(
                        new CompleteSecurityPinLoginCommand(new CompleteSecurityPinLoginRequest(
                            challenge.ChallengeToken,
                            TargetedCorrectnessFixture.SecurityPin)), default);
                    return true;
                }
                catch (Exception exception) when (exception is AppException
                    or DbUpdateException
                    or SqliteException)
                {
                    return false;
                }
            }

            var results = await Task.WhenAll(TryCompleteAsync(), TryCompleteAsync());

            Assert.Equal(1, results.Count(success => success));
            await using var verify = new SpendWiseDbContext(options, clock);
            Assert.Single(await verify.UserSessions.AsNoTracking()
                .Where(session => session.UserId == userId)
                .ToListAsync());
            Assert.Single(await verify.RefreshTokens.AsNoTracking()
                .Where(token => token.UserId == userId)
                .ToListAsync());
            Assert.Single(await verify.EmailOutboxMessages.AsNoTracking()
                .Where(email => email.DeduplicationKey.StartsWith("login-new-device:"))
                .ToListAsync());
        }
        finally
        {
            if (File.Exists(databasePath)) File.Delete(databasePath);
        }
    }

    private static LoginChallengeSupport Challenges(ISpendWiseDbContext db, IClock clock) =>
        new(db, new LoginChallengeTokenService(), clock);

    private static CompleteSecurityPinLoginCommandHandler CompleteHandler(
        SpendWiseDbContext db,
        IClock clock,
        TimeSpan? lifetime = null,
        ITokenService? tokens = null) =>
        new(
            db,
            new SecurityPinService(),
            Challenges(db, clock),
            new AuthenticationSupport(db, tokens ?? new TargetedTokenService(), clock),
            InteractiveLoginTestFactory.Create(db, clock, lifetime),
            clock,
            new AccountCredentialLockout(db, clock));

    private static async Task<AuthResponse> CompleteAsync(
        SpendWiseDbContext db,
        User user,
        IClock clock,
        string? deviceTrustToken,
        string deviceName = "Test browser",
        string? ipAddress = "127.0.0.1",
        string? userAgent = "SpendWise-Tests/1",
        TimeSpan? lifetime = null,
        ITokenService? tokens = null)
    {
        var challenge = await Challenges(db, clock).CreateAsync(
            user,
            deviceName,
            ipAddress,
            userAgent,
            default);
        return await CompleteHandler(db, clock, lifetime, tokens).Handle(
            new CompleteSecurityPinLoginCommand(new CompleteSecurityPinLoginRequest(
                challenge.ChallengeToken,
                TargetedCorrectnessFixture.SecurityPin,
                deviceTrustToken)), default);
    }

    private static async Task<string> AddTrustedDeviceAsync(
        SpendWiseDbContext db,
        Guid userId,
        DateTime createdAt)
    {
        var tokens = new DeviceTrustTokenService();
        var raw = tokens.CreateToken();
        var session = new UserSession
        {
            UserId = userId,
            DeviceName = "Trusted browser",
            DeviceIdentifier = tokens.HashToken(raw),
            LastSeenAt = createdAt
        };
        db.UserSessions.Add(session);
        await db.SaveChangesAsync();
        session.CreatedAt = createdAt;
        await db.SaveChangesAsync();
        return raw;
    }

    private static async Task<Guid> ChallengeIdAsync(string rawToken, SpendWiseDbContext db)
    {
        var hash = new LoginChallengeTokenService().HashToken(rawToken);
        return await db.LoginChallenges.AsNoTracking()
            .Where(x => x.TokenHash == hash)
            .Select(x => x.Id)
            .SingleAsync();
    }

    private sealed class MutableClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; set; } = utcNow;
    }

    private sealed class FailingSender : ITransactionalEmailSender
    {
        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("provider unavailable");
    }
}
