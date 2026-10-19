using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Features.Authentication;
using SpendWise.Application.Features.Authentication.Commands.ChangePassword;
using SpendWise.Application.Features.Authentication.Commands.Logout;
using SpendWise.Application.Features.Users.Commands.RevokeSessions;
using SpendWise.Application.Identity;
using Xunit;

namespace SpendWise.Tests;

// Các test này bảo vệ logout theo thiết bị và đổi mật khẩu có transaction.
public sealed class AuthSessionLifecycleTests
{
    [Fact]
    public async Task Logout_revokes_current_session_and_its_refresh_tokens()
    {
        // Arrange: một phiên đăng nhập có refresh token đang hoạt động.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Logout user");
        var session = await fixture.AddSessionAsync(user.Id);
        var refresh = await fixture.AddRefreshTokenAsync(
            user.Id,
            "logout-refresh",
            userSessionId: session.Id);
        fixture.Db.ChangeTracker.Clear();
        var handler = new LogoutCommandHandler(Authentication(fixture));

        // Act: logout bằng sid của access token hiện tại.
        await handler.Handle(
            new LogoutCommand(user.Id, session.Id, "logout-refresh"),
            default);

        // Assert: cả session và token cùng phiên đều bị thu hồi.
        fixture.Db.ChangeTracker.Clear();
        Assert.NotNull((await fixture.Db.UserSessions.FindAsync(session.Id))!.RevokedAt);
        Assert.NotNull((await fixture.Db.RefreshTokens.FindAsync(refresh.Id))!.RevokedAt);
    }

    [Fact]
    public async Task Logout_does_not_revoke_another_device()
    {
        // Arrange: user đăng nhập trên hai session/thiết bị độc lập.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Multi-device user");
        var firstSession = await fixture.AddSessionAsync(user.Id);
        var secondSession = await fixture.AddSessionAsync(user.Id);
        var firstToken = await fixture.AddRefreshTokenAsync(
            user.Id,
            "first-device-token",
            userSessionId: firstSession.Id);
        var secondToken = await fixture.AddRefreshTokenAsync(
            user.Id,
            "second-device-token",
            userSessionId: secondSession.Id);
        fixture.Db.ChangeTracker.Clear();
        var handler = new LogoutCommandHandler(Authentication(fixture));

        // Act: chỉ logout session thứ nhất.
        await handler.Handle(
            new LogoutCommand(user.Id, firstSession.Id, null),
            default);

        // Assert: session/token thiết bị thứ hai vẫn hoạt động.
        fixture.Db.ChangeTracker.Clear();
        Assert.NotNull((await fixture.Db.UserSessions.FindAsync(firstSession.Id))!.RevokedAt);
        Assert.NotNull((await fixture.Db.RefreshTokens.FindAsync(firstToken.Id))!.RevokedAt);
        Assert.Null((await fixture.Db.UserSessions.FindAsync(secondSession.Id))!.RevokedAt);
        Assert.Null((await fixture.Db.RefreshTokens.FindAsync(secondToken.Id))!.RevokedAt);
    }

    [Fact]
    public async Task Logout_all_revokes_every_session_and_refresh_token()
    {
        // Arrange: hai session còn hiệu lực của cùng người dùng.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Logout all user");
        var first = await fixture.AddSessionAsync(user.Id);
        var second = await fixture.AddSessionAsync(user.Id);
        await fixture.AddRefreshTokenAsync(user.Id, "all-token-1", userSessionId: first.Id);
        await fixture.AddRefreshTokenAsync(user.Id, "all-token-2", userSessionId: second.Id);
        fixture.Db.ChangeTracker.Clear();
        var handler = new RevokeSessionsCommandHandler(Authentication(fixture));

        // Act: use case Logout All thu hồi toàn bộ credential.
        await handler.Handle(new RevokeSessionsCommand(user.Id), default);

        // Assert: không còn session hoặc refresh token hoạt động.
        fixture.Db.ChangeTracker.Clear();
        Assert.DoesNotContain(
            await fixture.Db.UserSessions.Where(x => x.UserId == user.Id).ToListAsync(),
            session => session.RevokedAt is null);
        Assert.DoesNotContain(
            await fixture.Db.RefreshTokens.Where(x => x.UserId == user.Id).ToListAsync(),
            token => token.RevokedAt is null);
    }

    [Fact]
    public async Task Change_password_revokes_old_sessions_and_tokens()
    {
        // Arrange: user có password cũ cùng session/token đang dùng.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var passwords = new TestPasswordService();
        var user = await fixture.AddUserAsync("Password user");
        user.PasswordHash = passwords.Hash("OldPassword1");
        await fixture.Db.SaveChangesAsync();
        var session = await fixture.AddSessionAsync(user.Id);
        var refresh = await fixture.AddRefreshTokenAsync(
            user.Id,
            "password-refresh",
            userSessionId: session.Id);
        fixture.Db.ChangeTracker.Clear();
        var handler = new ChangePasswordCommandHandler(
            fixture.Db,
            passwords,
            Authentication(fixture));

        // Act: đổi sang password hợp lệ mới.
        await handler.Handle(
            new ChangePasswordCommand(
                user.Id,
                new ChangePasswordRequest("OldPassword1", "NewPassword1")),
            default);

        // Assert: password đổi và mọi credential cũ bị thu hồi.
        fixture.Db.ChangeTracker.Clear();
        Assert.True(passwords.Verify(
            "NewPassword1",
            (await fixture.Db.Users.FindAsync(user.Id))!.PasswordHash));
        Assert.NotNull((await fixture.Db.UserSessions.FindAsync(session.Id))!.RevokedAt);
        Assert.NotNull((await fixture.Db.RefreshTokens.FindAsync(refresh.Id))!.RevokedAt);
    }

    [Fact]
    public async Task Change_password_database_failure_rolls_back_password_and_revocations()
    {
        // Arrange: trigger làm lỗi lúc UPDATE User sau khi handler đã revoke credential.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var passwords = new TestPasswordService();
        var user = await fixture.AddUserAsync("Password rollback user");
        var oldHash = passwords.Hash("OldPassword1");
        user.PasswordHash = oldHash;
        await fixture.Db.SaveChangesAsync();
        var session = await fixture.AddSessionAsync(user.Id);
        var refresh = await fixture.AddRefreshTokenAsync(
            user.Id,
            "rollback-password-refresh",
            userSessionId: session.Id);
        await fixture.Db.Database.ExecuteSqlRawAsync(
            """
            CREATE TRIGGER fail_password_update
            BEFORE UPDATE ON Users
            WHEN NEW.PasswordHash = 'hash:NewPassword1'
            BEGIN
                SELECT RAISE(ABORT, 'forced password failure');
            END;
            """);
        fixture.Db.ChangeTracker.Clear();
        var handler = new ChangePasswordCommandHandler(
            fixture.Db,
            passwords,
            Authentication(fixture));

        // Act: lỗi SaveChanges phải rollback cả ExecuteUpdate trước đó.
        await Assert.ThrowsAsync<DbUpdateException>(() => handler.Handle(
            new ChangePasswordCommand(
                user.Id,
                new ChangePasswordRequest("OldPassword1", "NewPassword1")),
            default));

        // Assert: password và credential đều giữ nguyên như trước request.
        fixture.Db.ChangeTracker.Clear();
        Assert.Equal(oldHash, (await fixture.Db.Users.FindAsync(user.Id))!.PasswordHash);
        Assert.Null((await fixture.Db.UserSessions.FindAsync(session.Id))!.RevokedAt);
        Assert.Null((await fixture.Db.RefreshTokens.FindAsync(refresh.Id))!.RevokedAt);
    }

    private static AuthenticationSupport Authentication(
        TargetedCorrectnessFixture fixture) =>
        new(fixture.Db, new TargetedTokenService(), fixture.Clock);

    private sealed class TestPasswordService : IPasswordService
    {
        public string Hash(string password) => $"hash:{password}";

        public bool Verify(string password, string encodedHash) =>
            encodedHash == Hash(password);
    }
}
