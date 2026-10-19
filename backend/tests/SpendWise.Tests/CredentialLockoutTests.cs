using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Authentication;
using SpendWise.Application.Features.Authentication.Commands.CompleteSecurityPinLogin;
using SpendWise.Application.Features.Authentication.Commands.Login;
using SpendWise.Application.Identity;
using SpendWise.Domain.Identity;
using SpendWise.Infrastructure.Identity;
using SpendWise.Infrastructure.Persistence;
using Xunit;

namespace SpendWise.Tests;

// Các test này bảo vệ khóa tài khoản dùng chung giữa nhiều request và nhiều challenge.
public sealed class CredentialLockoutTests
{
    private const string CorrectPassword = "CorrectPassword1";
    private const string CorrectPin = TargetedCorrectnessFixture.SecurityPin;

    [Fact]
    public async Task Unknown_identifier_and_locked_account_have_the_same_http_error_contract()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        string email;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            var user = NewUser("HTTP lockout contract");
            user.PasswordHash = new PasswordService().Hash(CorrectPassword);
            user.PasswordFailedAttempts = 5;
            user.PasswordLockoutUntil = DateTime.UtcNow.AddMinutes(5);
            email = user.Email;
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        var unknown = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("unknown@test.local", CorrectPassword));
        var locked = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, CorrectPassword));

        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        Assert.Equal(unknown.StatusCode, locked.StatusCode);
        var unknownCode = await ProblemCodeAsync(unknown);
        var lockedCode = await ProblemCodeAsync(locked);
        Assert.Equal(unknownCode, lockedCode);
        Assert.Equal("invalid_credentials", lockedCode);
        using var verificationScope = factory.Services.CreateScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        Assert.Empty(await verificationDb.LoginChallenges.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Password_failures_lock_account_without_revealing_identifier_and_expiry_resets_it()
    {
        var clock = new MutableClock(new DateTime(2026, 9, 19, 1, 0, 0, DateTimeKind.Utc));
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync(clock);
        var user = await fixture.AddUserAsync("Password lockout");
        user.PasswordHash = TestPasswordService.HashValue(CorrectPassword);
        user.SecurityPinFailedAttempts = 3;
        await fixture.Db.SaveChangesAsync();
        fixture.Db.ChangeTracker.Clear();
        var handler = LoginHandler(fixture, clock);

        // Năm mật khẩu sai trên cùng tài khoản phải tạo khóa năm phút.
        AppException? knownFailure = null;
        for (var attempt = 0; attempt < AccountCredentialLockout.MaximumFailedAttempts; attempt++)
        {
            knownFailure = await Assert.ThrowsAsync<AppException>(() => handler.Handle(
                Login(user.Email, "WrongPassword1"),
                default));
            Assert.Equal("invalid_credentials", knownFailure.Code);
            Assert.Equal(401, knownFailure.StatusCode);
        }

        fixture.Db.ChangeTracker.Clear();
        var lockedUser = await fixture.Db.Users.AsNoTracking().SingleAsync(item => item.Id == user.Id);
        Assert.Equal(5, lockedUser.PasswordFailedAttempts);
        Assert.Equal(clock.UtcNow.AddMinutes(5), lockedUser.PasswordLockoutUntil);
        Assert.Equal(3, lockedUser.SecurityPinFailedAttempts);
        Assert.Empty(await fixture.Db.LoginChallenges.AsNoTracking().ToListAsync());

        // Tài khoản không tồn tại và tài khoản bị khóa đều giữ nguyên lỗi chung.
        var unknownFailure = await Assert.ThrowsAsync<AppException>(() => handler.Handle(
            Login("unknown@test.local", "WrongPassword1"),
            default));
        var lockedFailure = await Assert.ThrowsAsync<AppException>(() => handler.Handle(
            Login(user.Email, CorrectPassword),
            default));
        Assert.Equal((knownFailure!.Code, knownFailure.StatusCode),
            (unknownFailure.Code, unknownFailure.StatusCode));
        Assert.Equal((knownFailure.Code, knownFailure.StatusCode),
            (lockedFailure.Code, lockedFailure.StatusCode));
        Assert.Empty(await fixture.Db.LoginChallenges.AsNoTracking().ToListAsync());

        // Hết thời gian khóa, mật khẩu đúng tạo challenge và xóa bộ đếm password.
        clock.Advance(TimeSpan.FromMinutes(6));
        var challenge = await handler.Handle(Login(user.Email, CorrectPassword), default);
        Assert.Equal("PIN_REQUIRED", challenge.Status);
        fixture.Db.ChangeTracker.Clear();
        var recoveredUser = await fixture.Db.Users.AsNoTracking().SingleAsync(item => item.Id == user.Id);
        Assert.Equal(0, recoveredUser.PasswordFailedAttempts);
        Assert.Null(recoveredUser.PasswordLockoutUntil);
        Assert.Equal(3, recoveredUser.SecurityPinFailedAttempts);
    }

    [Fact]
    public async Task Pin_failures_across_fresh_challenges_lock_account_then_expiry_allows_login()
    {
        var clock = new MutableClock(new DateTime(2026, 9, 19, 2, 0, 0, DateTimeKind.Utc));
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync(clock);
        var user = await fixture.AddUserAsync("PIN lockout");
        user.PasswordHash = TestPasswordService.HashValue(CorrectPassword);
        await fixture.Db.SaveChangesAsync();
        fixture.Db.ChangeTracker.Clear();
        var login = LoginHandler(fixture, clock);
        var complete = CompleteLoginHandler(fixture, clock);
        LoginChallengeResponse? latestChallenge = null;

        // Mỗi lần dùng challenge mới chỉ sai một lần, nhưng bộ đếm PIN cấp tài khoản vẫn cộng dồn.
        for (var attempt = 0; attempt < AccountCredentialLockout.MaximumFailedAttempts; attempt++)
        {
            latestChallenge = await login.Handle(Login(user.Email, CorrectPassword), default);
            var failure = await Assert.ThrowsAsync<AppException>(() => complete.Handle(
                new CompleteSecurityPinLoginCommand(
                    new CompleteSecurityPinLoginRequest(latestChallenge.ChallengeToken, "654321")),
                default));
            Assert.Equal("login_challenge_failed", failure.Code);
        }

        fixture.Db.ChangeTracker.Clear();
        var lockedUser = await fixture.Db.Users.AsNoTracking().SingleAsync(item => item.Id == user.Id);
        Assert.Equal(5, lockedUser.SecurityPinFailedAttempts);
        Assert.Equal(clock.UtcNow.AddMinutes(5), lockedUser.SecurityPinLockoutUntil);
        Assert.Equal(0, lockedUser.PasswordFailedAttempts);
        Assert.All(
            await fixture.Db.LoginChallenges.AsNoTracking().ToListAsync(),
            challenge => Assert.Equal(1, challenge.FailedAttempts));

        // PIN đúng vẫn bị chặn khi khóa đang hoạt động và không tạo session.
        var lockedFailure = await Assert.ThrowsAsync<AppException>(() => complete.Handle(
            new CompleteSecurityPinLoginCommand(
                new CompleteSecurityPinLoginRequest(latestChallenge!.ChallengeToken, CorrectPin)),
            default));
        Assert.Equal("login_challenge_failed", lockedFailure.Code);
        Assert.Empty(await fixture.Db.UserSessions.AsNoTracking().ToListAsync());

        // Challenge mới sau khi khóa hết hạn có thể hoàn tất login và reset bộ đếm PIN.
        clock.Advance(TimeSpan.FromMinutes(6));
        var recoveredChallenge = await login.Handle(Login(user.Email, CorrectPassword), default);
        var authentication = await complete.Handle(
            new CompleteSecurityPinLoginCommand(
                new CompleteSecurityPinLoginRequest(recoveredChallenge.ChallengeToken, CorrectPin)),
            default);
        Assert.False(string.IsNullOrWhiteSpace(authentication.AccessToken));
        fixture.Db.ChangeTracker.Clear();
        var recoveredUser = await fixture.Db.Users.AsNoTracking().SingleAsync(item => item.Id == user.Id);
        Assert.Equal(0, recoveredUser.SecurityPinFailedAttempts);
        Assert.Null(recoveredUser.SecurityPinLockoutUntil);
        Assert.Single(await fixture.Db.UserSessions.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Concurrent_password_failures_are_counted_without_lost_updates()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"spendwise-lockout-{Guid.NewGuid():N}.db");
        try
        {
            var connectionString = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                DefaultTimeout = 30,
                Pooling = false
            }.ToString();
            var options = new DbContextOptionsBuilder<SpendWiseDbContext>()
                .UseSqlite(connectionString)
                .Options;
            var clock = new MutableClock(new DateTime(2026, 9, 19, 3, 0, 0, DateTimeKind.Utc));
            Guid userId;
            await using (var setup = new SpendWiseDbContext(options, clock))
            {
                await setup.Database.EnsureCreatedAsync();
                var user = NewUser("Concurrent failures");
                setup.Users.Add(user);
                await setup.SaveChangesAsync();
                userId = user.Id;
            }

            // Năm DbContext độc lập cùng tăng bộ đếm; UPDATE phía database phải giữ đủ năm lượt.
            var attempts = Enumerable.Range(0, AccountCredentialLockout.MaximumFailedAttempts)
                .Select(async _ =>
                {
                    await using var db = new SpendWiseDbContext(options, clock);
                    await new AccountCredentialLockout(db, clock)
                        .RecordPasswordFailureAsync(userId, default);
                });
            await Task.WhenAll(attempts);

            await using var verification = new SpendWiseDbContext(options, clock);
            var userAfterFailures = await verification.Users.AsNoTracking()
                .SingleAsync(item => item.Id == userId);
            Assert.Equal(5, userAfterFailures.PasswordFailedAttempts);
            Assert.Equal(clock.UtcNow.AddMinutes(5), userAfterFailures.PasswordLockoutUntil);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath)) File.Delete(databasePath);
        }
    }

    private static LoginCommandHandler LoginHandler(
        TargetedCorrectnessFixture fixture,
        IClock clock)
    {
        var challenges = new LoginChallengeSupport(
            fixture.Db,
            new LoginChallengeTokenService(),
            clock);
        return new LoginCommandHandler(
            fixture.Db,
            new TestPasswordService(),
            challenges,
            new AccountCredentialLockout(fixture.Db, clock));
    }

    private static CompleteSecurityPinLoginCommandHandler CompleteLoginHandler(
        TargetedCorrectnessFixture fixture,
        IClock clock)
    {
        var challenges = new LoginChallengeSupport(
            fixture.Db,
            new LoginChallengeTokenService(),
            clock);
        return new CompleteSecurityPinLoginCommandHandler(
            fixture.Db,
            new SecurityPinService(),
            challenges,
            new AuthenticationSupport(fixture.Db, new TargetedTokenService(), clock),
            InteractiveLoginTestFactory.Create(fixture.Db, clock),
            clock,
            new AccountCredentialLockout(fixture.Db, clock));
    }

    private static LoginCommand Login(string identifier, string password) => new(
        new LoginRequest(identifier, password, "Lockout test"),
        "127.0.0.1",
        "SpendWise tests");

    private static User NewUser(string name)
    {
        var unique = Guid.NewGuid().ToString("N");
        return new User
        {
            FullName = name,
            Email = $"{unique}@test.local",
            NormalizedEmail = $"{unique}@TEST.LOCAL",
            SpendId = unique,
            NormalizedSpendId = unique.ToUpperInvariant(),
            PasswordHash = TestPasswordService.HashValue(CorrectPassword),
            SecurityPinHash = new SecurityPinService().Hash(CorrectPin)
        };
    }

    private static async Task<string?> ProblemCodeAsync(HttpResponseMessage response)
    {
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        return problem.GetProperty("code").GetString();
    }

    private sealed class TestPasswordService : IPasswordService
    {
        public static string HashValue(string password) => $"test-hash:{password}";
        public string Hash(string password) => HashValue(password);
        public bool Verify(string password, string encodedHash) =>
            string.Equals(HashValue(password), encodedHash, StringComparison.Ordinal);
    }

    private sealed class MutableClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; private set; } = utcNow;
        public void Advance(TimeSpan duration) => UtcNow = UtcNow.Add(duration);
    }
}
