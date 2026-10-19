using System.Collections.Concurrent;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Features.Authentication.Commands.RefreshToken;
using SpendWise.Application.Identity;
using SpendWise.Domain.Identity;
using SpendWise.Infrastructure.Persistence;
using SpendWise.Infrastructure.Services;
using Xunit;

namespace SpendWise.Tests;

// Các test relational này bảo vệ refresh-token rotation và liên kết session thật.
public sealed class RefreshRotationHardeningTests
{
    [Fact]
    public async Task Valid_refresh_consumes_old_token_and_creates_replacement()
    {
        // Arrange: token còn hạn thuộc một session Active.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Refresh user");
        var session = await fixture.AddSessionAsync(user.Id);
        var oldToken = await fixture.AddRefreshTokenAsync(
            user.Id,
            "valid-refresh",
            userSessionId: session.Id);
        fixture.Db.ChangeTracker.Clear();
        var tokens = new RecordingTokenService();
        var handler = new RefreshTokenCommandHandler(fixture.Db, tokens, fixture.Clock);

        // Act: rotate token một lần.
        var response = await handler.Handle(
            Command("valid-refresh"),
            default);

        // Assert: token cũ đã consume và chỉ có một replacement mới.
        fixture.Db.ChangeTracker.Clear();
        var storedOld = await fixture.Db.RefreshTokens.FindAsync(oldToken.Id);
        var replacement = await fixture.Db.RefreshTokens.SingleAsync(
            token => token.Id != oldToken.Id);
        Assert.NotNull(storedOld!.RevokedAt);
        Assert.Equal(replacement.Id, storedOld.ReplacedByTokenId);
        Assert.Equal(response.RefreshToken, replacement.TokenHash);
        Assert.Equal(2, await fixture.Db.RefreshTokens.CountAsync());
    }

    [Fact]
    public async Task Revoked_refresh_is_rejected()
    {
        // Arrange: token đã có RevokedAt nên không thể consume lần nữa.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Revoked refresh user");
        await fixture.AddRefreshTokenAsync(
            user.Id,
            "revoked-refresh",
            revokedAt: fixture.Clock.UtcNow);
        fixture.Db.ChangeTracker.Clear();
        var handler = Handler(fixture);

        // Act + Assert: trả Unauthorized có kiểm soát và không tạo replacement.
        var error = await Assert.ThrowsAsync<AppException>(() => handler.Handle(
            Command("revoked-refresh"),
            default));
        Assert.Equal("invalid_refresh_token", error.Code);
        Assert.Equal(401, error.StatusCode);
        Assert.Equal(1, await fixture.Db.RefreshTokens.CountAsync());
    }

    [Fact]
    public async Task Expired_refresh_is_rejected()
    {
        // Arrange: token chưa revoke nhưng đã hết hạn.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Expired refresh user");
        await fixture.AddRefreshTokenAsync(
            user.Id,
            "expired-refresh",
            expiresAt: fixture.Clock.UtcNow.AddMinutes(-1));
        fixture.Db.ChangeTracker.Clear();
        var handler = Handler(fixture);

        // Act + Assert: token hết hạn không được phát hành credential mới.
        var error = await Assert.ThrowsAsync<AppException>(() => handler.Handle(
            Command("expired-refresh"),
            default));
        Assert.Equal("invalid_refresh_token", error.Code);
        Assert.Equal(401, error.StatusCode);
        Assert.Equal(1, await fixture.Db.RefreshTokens.CountAsync());
    }

    [Fact]
    public async Task Revoked_session_cannot_refresh()
    {
        // Arrange: refresh token còn hạn nhưng session cha đã bị thu hồi.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Revoked session refresh user");
        var session = await fixture.AddSessionAsync(user.Id, fixture.Clock.UtcNow);
        await fixture.AddRefreshTokenAsync(
            user.Id,
            "revoked-session-refresh",
            userSessionId: session.Id);
        fixture.Db.ChangeTracker.Clear();
        var handler = Handler(fixture);

        // Act + Assert: session lifecycle chặn cả refresh rotation.
        var error = await Assert.ThrowsAsync<AppException>(() => handler.Handle(
            Command("revoked-session-refresh"),
            default));
        Assert.Equal("invalid_refresh_token", error.Code);
        Assert.Equal(401, error.StatusCode);
    }

    [Fact]
    public async Task Replacement_keeps_same_logical_session()
    {
        // Arrange: ghi lại session id mà token service nhận khi tạo access token mới.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Same session user");
        var session = await fixture.AddSessionAsync(user.Id);
        await fixture.AddRefreshTokenAsync(
            user.Id,
            "same-session-refresh",
            userSessionId: session.Id);
        fixture.Db.ChangeTracker.Clear();
        var tokens = new RecordingTokenService();
        var handler = new RefreshTokenCommandHandler(fixture.Db, tokens, fixture.Clock);

        // Act: rotate token.
        await handler.Handle(Command("same-session-refresh"), default);

        // Assert: refresh token mới và access token đều dùng sid ban đầu.
        fixture.Db.ChangeTracker.Clear();
        var replacement = await fixture.Db.RefreshTokens.SingleAsync(
            token => token.TokenHash != "same-session-refresh");
        Assert.Equal(session.Id, replacement.UserSessionId);
        Assert.Equal(session.Id, Assert.Single(tokens.AccessSessionIds));
        Assert.Single(await fixture.Db.UserSessions.ToListAsync());
    }

    [Fact]
    public async Task Same_refresh_token_cannot_be_consumed_twice_sequentially()
    {
        // Arrange: một raw token hợp lệ và một handler duy nhất.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Sequential refresh user");
        await fixture.AddRefreshTokenAsync(user.Id, "single-use-refresh");
        fixture.Db.ChangeTracker.Clear();
        var handler = Handler(fixture);

        // Act: request đầu thành công, request thứ hai dùng lại raw token cũ.
        await handler.Handle(Command("single-use-refresh"), default);
        var error = await Assert.ThrowsAsync<AppException>(() => handler.Handle(
            Command("single-use-refresh"),
            default));

        // Assert: lần hai bị từ chối và không sinh thêm replacement.
        Assert.Equal("invalid_refresh_token", error.Code);
        fixture.Db.ChangeTracker.Clear();
        Assert.Equal(2, await fixture.Db.RefreshTokens.CountAsync());
    }

    [Fact]
    public async Task Two_concurrent_refresh_attempts_have_exactly_one_success()
    {
        // Arrange: file SQLite cho phép hai DbContext/connection tranh chấp cùng một row.
        var databasePath = Path.Combine(
            Path.GetTempPath(),
            $"spendwise-refresh-{Guid.NewGuid():N}.db");
        try
        {
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                DefaultTimeout = 30,
                Pooling = false
            }.ToString();
            var clock = new SystemClock();
            var options = new DbContextOptionsBuilder<SpendWiseDbContext>()
                .UseSqlite(connectionString)
                .Options;
            Guid userId;
            await using (var setup = new SpendWiseDbContext(options, clock))
            {
                await setup.Database.EnsureCreatedAsync();
                var unique = Guid.NewGuid().ToString("N");
                var user = new User
                {
                    FullName = "Concurrent refresh user",
                    Email = $"{unique}@test.local",
                    NormalizedEmail = $"{unique}@TEST.LOCAL",
                    SpendId = unique,
                    NormalizedSpendId = unique.ToUpperInvariant(),
                    PasswordHash = "test"
                };
                var session = new UserSession
                {
                    UserId = user.Id,
                    DeviceName = "Concurrent test",
                    LastSeenAt = clock.UtcNow
                };
                setup.AddRange(
                    user,
                    session,
                    new SpendWise.Domain.Identity.RefreshToken
                    {
                        UserId = user.Id,
                        UserSessionId = session.Id,
                        TokenHash = "concurrent-refresh",
                        ExpiresAt = clock.UtcNow.AddHours(1)
                    });
                await setup.SaveChangesAsync();
                userId = user.Id;
            }

            await using var firstDb = new SpendWiseDbContext(options, clock);
            await using var secondDb = new SpendWiseDbContext(options, clock);
            var first = AttemptAsync(new RefreshTokenCommandHandler(
                firstDb,
                new RecordingTokenService(),
                clock));
            var second = AttemptAsync(new RefreshTokenCommandHandler(
                secondDb,
                new RecordingTokenService(),
                clock));

            // Act: hai request bắt đầu trên hai DbContext độc lập.
            var outcomes = await Task.WhenAll(first, second);

            // Assert: đúng một request thắng; database chỉ có một replacement.
            Assert.Single(outcomes, outcome => outcome.Success);
            Assert.Single(
                outcomes,
                outcome => !outcome.Success && outcome.ErrorCode == "invalid_refresh_token");
            await using var verify = new SpendWiseDbContext(options, clock);
            Assert.Equal(
                2,
                await verify.RefreshTokens.CountAsync(token => token.UserId == userId));
            Assert.Single(await verify.RefreshTokens.Where(
                token => token.UserId == userId && token.RevokedAt == null).ToListAsync());
        }
        finally
        {
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }

    [Fact]
    public async Task Old_token_remains_unusable_after_replacement_is_used()
    {
        // Arrange: rotate token gốc và lấy raw replacement trả về cho client.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Rotation chain user");
        await fixture.AddRefreshTokenAsync(user.Id, "rotation-root");
        fixture.Db.ChangeTracker.Clear();
        var handler = Handler(fixture);
        var firstResponse = await handler.Handle(Command("rotation-root"), default);

        // Act: replacement tiếp tục rotate hợp lệ, sau đó thử lại token gốc.
        await handler.Handle(Command(firstResponse.RefreshToken), default);
        var error = await Assert.ThrowsAsync<AppException>(() => handler.Handle(
            Command("rotation-root"),
            default));

        // Assert: token gốc vĩnh viễn không hoạt động lại trong rotation chain.
        Assert.Equal("invalid_refresh_token", error.Code);
        fixture.Db.ChangeTracker.Clear();
        Assert.Equal(3, await fixture.Db.RefreshTokens.CountAsync());
    }

    private static RefreshTokenCommandHandler Handler(
        TargetedCorrectnessFixture fixture) =>
        new(fixture.Db, new RecordingTokenService(), fixture.Clock);

    private static RefreshTokenCommand Command(string rawToken) =>
        new(new RefreshRequest(rawToken, "Test browser"), "127.0.0.1", "Tests");

    private static async Task<(bool Success, string? ErrorCode)> AttemptAsync(
        RefreshTokenCommandHandler handler)
    {
        try
        {
            await handler.Handle(Command("concurrent-refresh"), default);
            return (true, null);
        }
        catch (AppException exception)
        {
            return (false, exception.Code);
        }
    }

    private sealed class RecordingTokenService : ITokenService
    {
        public ConcurrentBag<Guid> AccessSessionIds { get; } = [];

        public AccessTokenResult CreateAccessToken(User user, Guid sessionId)
        {
            AccessSessionIds.Add(sessionId);
            return new AccessTokenResult(
                $"access-{Guid.NewGuid():N}",
                DateTime.UtcNow.AddMinutes(15));
        }

        public string CreateRefreshToken() => $"refresh-{Guid.NewGuid():N}";

        public string HashRefreshToken(string token) => token;
    }
}
