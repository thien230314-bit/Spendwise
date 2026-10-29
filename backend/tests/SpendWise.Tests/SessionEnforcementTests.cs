using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Admin;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Features.Admin;
using SpendWise.Application.Features.Admin.Users.Commands.ChangeUserStatus;
using SpendWise.Application.Features.Authentication;
using SpendWise.Application.Features.Authentication.Commands.RefreshToken;
using SpendWise.Application.Identity;
using SpendWise.Domain.Identity;
using SpendWise.Infrastructure.Identity;
using Xunit;

namespace SpendWise.Tests;

// Các test này bảo đảm access session và refresh token bị vô hiệu hóa theo trạng thái tài khoản.
public sealed class SessionEnforcementTests
{
    [Fact]
    public async Task Active_account_with_matching_session_is_valid()
    {
        // Arrange: tài khoản Active có session chưa bị thu hồi.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Active user");
        var session = await fixture.AddSessionAsync(user.Id);
        var validator = new AccessSessionValidator(fixture.Db);

        // Act: kiểm tra cặp user id và session id chính xác.
        var valid = await validator.IsValidAsync(user.Id, session.Id, default);

        // Assert: access token gắn với phiên này được phép đi tiếp.
        Assert.True(valid);
    }

    [Fact]
    public async Task Revoked_session_is_invalid()
    {
        // Arrange: tài khoản vẫn Active nhưng session đã bị thu hồi.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Revoked session user");
        var session = await fixture.AddSessionAsync(user.Id, fixture.Clock.UtcNow);
        var validator = new AccessSessionValidator(fixture.Db);

        // Act: validator đọc trạng thái trực tiếp từ database.
        var valid = await validator.IsValidAsync(user.Id, session.Id, default);

        // Assert: access token cũ không còn được chấp nhận.
        Assert.False(valid);
    }

    [Fact]
    public async Task Locked_account_session_is_invalid()
    {
        // Arrange: session chưa revoke nhưng tài khoản đã Locked.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Locked user", UserStatus.Locked);
        var session = await fixture.AddSessionAsync(user.Id);
        var validator = new AccessSessionValidator(fixture.Db);

        // Act: xác thực session của tài khoản Locked.
        var valid = await validator.IsValidAsync(user.Id, session.Id, default);

        // Assert: trạng thái tài khoản đủ để từ chối access token.
        Assert.False(valid);
    }

    [Fact]
    public async Task Restricted_account_session_is_valid_for_reading()
    {
        // Arrange: theo Quyết định 2, tài khoản Restricted được phép duy trì phiên đọc.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Restricted user", UserStatus.Restricted);
        var session = await fixture.AddSessionAsync(user.Id);
        var validator = new AccessSessionValidator(fixture.Db);

        // Act: kiểm tra phiên của tài khoản Restricted.
        var valid = await validator.IsValidAsync(user.Id, session.Id, default);

        // Assert: Quyết định 2 cho phép phiên Restricted để xem số dư, lịch sử, profile.
        Assert.True(valid);
    }

    [Fact]
    public async Task Suspended_account_session_is_invalid()
    {
        // Arrange: tài khoản Suspended có session chưa đánh dấu revoke.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Suspended user", UserStatus.Suspended);
        var session = await fixture.AddSessionAsync(user.Id);
        var validator = new AccessSessionValidator(fixture.Db);

        // Act: xác thực session hiện có.
        var valid = await validator.IsValidAsync(user.Id, session.Id, default);

        // Assert: Suspended luôn bị từ chối ở access-token validation.
        Assert.False(valid);
    }

    [Fact]
    public async Task Session_owned_by_another_user_is_invalid()
    {
        // Arrange: session thuộc user B nhưng token khai báo user A.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var claimedUser = await fixture.AddUserAsync("Claimed user");
        var actualOwner = await fixture.AddUserAsync("Session owner");
        var session = await fixture.AddSessionAsync(actualOwner.Id);
        var validator = new AccessSessionValidator(fixture.Db);

        // Act: ghép user id và session id không cùng chủ sở hữu.
        var valid = await validator.IsValidAsync(claimedUser.Id, session.Id, default);

        // Assert: không thể mượn session của người dùng khác.
        Assert.False(valid);
    }

    [Fact]
    public async Task Refresh_token_for_nonactive_account_returns_forbidden()
    {
        // Arrange: refresh token còn hạn nhưng tài khoản đã Locked.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Blocked refresh user", UserStatus.Locked);
        await fixture.AddRefreshTokenAsync(user.Id, "blocked-refresh");
        var tokens = new TargetedTokenService();
        var handler = new RefreshTokenCommandHandler(
            fixture.Db,
            tokens,
            fixture.Clock);

        // Act: thử xoay refresh token của tài khoản không Active.
        var error = await Assert.ThrowsAsync<AppException>(() => handler.Handle(
            new RefreshTokenCommand(new RefreshRequest("blocked-refresh"), null, null),
            default));

        // Assert: trả đúng account_unavailable 403 và không phát hành token mới.
        Assert.Equal("account_unavailable", error.Code);
        Assert.Equal(403, error.StatusCode);
        Assert.Equal(1, await fixture.Db.RefreshTokens.CountAsync());
    }

    [Fact]
    public async Task Changing_active_account_to_locked_revokes_all_credentials()
    {
        // Arrange: tài khoản Active có hai session và hai refresh token.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var admin = await fixture.AddUserAsync("Admin", role: SystemRole.Admin);
        var target = await fixture.AddUserAsync("Lock target");
        await AddTwoCredentialsAsync(fixture, target.Id, "lock");
        fixture.Db.ChangeTracker.Clear();

        // Act: admin chuyển Active sang Locked.
        await ChangeStatusAsync(fixture, admin.Id, target.Id, "Locked", "Security lock");

        // Assert: trạng thái, toàn bộ session và refresh token cùng bị vô hiệu hóa.
        fixture.Db.ChangeTracker.Clear();
        Assert.Equal(UserStatus.Locked, (await fixture.Db.Users.FindAsync(target.Id))!.Status);
        Assert.All(
            await fixture.Db.UserSessions.Where(x => x.UserId == target.Id).ToListAsync(),
            session => Assert.NotNull(session.RevokedAt));
        Assert.All(
            await fixture.Db.RefreshTokens.Where(x => x.UserId == target.Id).ToListAsync(),
            token => Assert.NotNull(token.RevokedAt));
    }

    [Fact]
    public async Task Changing_active_account_to_restricted_revokes_all_credentials()
    {
        // Arrange: tài khoản Active có thông tin đăng nhập còn hiệu lực.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var admin = await fixture.AddUserAsync("Admin", role: SystemRole.Admin);
        var target = await fixture.AddUserAsync("Restrict target");
        await AddTwoCredentialsAsync(fixture, target.Id, "restrict");
        fixture.Db.ChangeTracker.Clear();

        // Act: admin chuyển Active sang Restricted.
        await ChangeStatusAsync(fixture, admin.Id, target.Id, "Restricted", "Policy restriction");

        // Assert: Restricted cũng thu hồi cả hai loại credential.
        fixture.Db.ChangeTracker.Clear();
        Assert.Equal(UserStatus.Restricted, (await fixture.Db.Users.FindAsync(target.Id))!.Status);
        Assert.DoesNotContain(
            await fixture.Db.UserSessions.Where(x => x.UserId == target.Id).ToListAsync(),
            session => session.RevokedAt is null);
        Assert.DoesNotContain(
            await fixture.Db.RefreshTokens.Where(x => x.UserId == target.Id).ToListAsync(),
            token => token.RevokedAt is null);
    }

    [Fact]
    public async Task Reactivating_account_does_not_restore_revoked_credentials()
    {
        // Arrange: khóa tài khoản một lần để ghi thời điểm revoke vào session và token.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var admin = await fixture.AddUserAsync("Admin", role: SystemRole.Admin);
        var target = await fixture.AddUserAsync("Reactivate target");
        var session = await fixture.AddSessionAsync(target.Id);
        var refresh = await fixture.AddRefreshTokenAsync(target.Id, "reactivate-token");
        fixture.Db.ChangeTracker.Clear();
        await ChangeStatusAsync(fixture, admin.Id, target.Id, "Locked", "Temporary lock");
        fixture.Db.ChangeTracker.Clear();
        var sessionRevokedAt = (await fixture.Db.UserSessions.FindAsync(session.Id))!.RevokedAt;
        var tokenRevokedAt = (await fixture.Db.RefreshTokens.FindAsync(refresh.Id))!.RevokedAt;
        fixture.Db.ChangeTracker.Clear();

        // Act: admin đưa tài khoản về Active.
        await ChangeStatusAsync(fixture, admin.Id, target.Id, "Active", "Review completed");

        // Assert: tài khoản Active lại nhưng credential cũ vẫn giữ nguyên thời điểm revoke.
        fixture.Db.ChangeTracker.Clear();
        Assert.Equal(UserStatus.Active, (await fixture.Db.Users.FindAsync(target.Id))!.Status);
        Assert.Equal(
            sessionRevokedAt,
            (await fixture.Db.UserSessions.FindAsync(session.Id))!.RevokedAt);
        Assert.Equal(
            tokenRevokedAt,
            (await fixture.Db.RefreshTokens.FindAsync(refresh.Id))!.RevokedAt);
    }

    private static async Task AddTwoCredentialsAsync(
        TargetedCorrectnessFixture fixture,
        Guid userId,
        string prefix)
    {
        // Hai bản ghi mỗi loại chứng minh thao tác admin thu hồi toàn bộ, không chỉ một phiên.
        await fixture.AddSessionAsync(userId);
        await fixture.AddSessionAsync(userId);
        await fixture.AddRefreshTokenAsync(userId, $"{prefix}-token-1");
        await fixture.AddRefreshTokenAsync(userId, $"{prefix}-token-2");
    }

    private static async Task ChangeStatusAsync(
        TargetedCorrectnessFixture fixture,
        Guid adminId,
        Guid userId,
        string status,
        string reason)
    {
        // Tạo đúng dependency graph của handler mà API dùng qua DI.
        var tokens = new TargetedTokenService();
        var authentication = new AuthenticationSupport(fixture.Db, tokens, fixture.Clock);
        var pinService = new SecurityPinService();
        var admin = await fixture.Db.Users.FindAsync(adminId)
            ?? throw new InvalidOperationException("Admin test user not found.");
        admin.SecurityPinHash = pinService.Hash("123456");
        await fixture.Db.SaveChangesAsync();
        var handler = new ChangeUserStatusCommandHandler(
            fixture.Db,
            authentication,
            new AdminAuditWriter(fixture.Db),
            new AdminSecurityGuard(fixture.Db, pinService,
                new AccountCredentialLockout(fixture.Db, fixture.Clock)));
        await handler.Handle(
            new ChangeUserStatusCommand(
                adminId,
                userId,
                new ChangeUserStatusRequest(status, reason, "123456")),
            default);
    }
}
