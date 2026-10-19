using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Admin;
using SpendWise.Application.Features.Admin;
using SpendWise.Application.Features.Admin.Users.Commands.ChangeUserStatus;
using SpendWise.Application.Features.Authentication;
using SpendWise.Domain.Identity;
using SpendWise.Infrastructure.Identity;
using Xunit;

namespace SpendWise.Tests;

// Các test này bảo đảm kích hoạt lại tài khoản luôn yêu cầu đăng nhập mới.
public sealed class ReactivationCredentialTests
{
    [Fact]
    public async Task Restricted_to_active_revokes_old_unrevoked_session()
    {
        // Arrange: mô phỏng dữ liệu cũ có user Restricted nhưng session chưa revoke.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var admin = await fixture.AddUserAsync("Admin", role: SystemRole.SystemAdmin);
        var target = await fixture.AddUserAsync("Restricted target", UserStatus.Restricted);
        var session = await fixture.AddSessionAsync(target.Id);
        fixture.Db.ChangeTracker.Clear();

        // Act: admin kích hoạt lại tài khoản.
        await ChangeStatusAsync(fixture, admin.Id, target.Id, "Active");

        // Assert: session cũ bị thu hồi trước khi tài khoản được dùng lại.
        fixture.Db.ChangeTracker.Clear();
        Assert.NotNull((await fixture.Db.UserSessions.FindAsync(session.Id))!.RevokedAt);
    }

    [Fact]
    public async Task Restricted_to_active_revokes_old_unrevoked_refresh_token()
    {
        // Arrange: refresh token cũ còn hạn của user Restricted chưa có RevokedAt.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var admin = await fixture.AddUserAsync("Admin", role: SystemRole.SystemAdmin);
        var target = await fixture.AddUserAsync("Restricted target", UserStatus.Restricted);
        var token = await fixture.AddRefreshTokenAsync(target.Id, "restricted-old-token");
        fixture.Db.ChangeTracker.Clear();

        // Act: chuyển tài khoản về Active.
        await ChangeStatusAsync(fixture, admin.Id, target.Id, "Active");

        // Assert: refresh token phát hành trước reactivation không dùng lại được.
        fixture.Db.ChangeTracker.Clear();
        Assert.NotNull((await fixture.Db.RefreshTokens.FindAsync(token.Id))!.RevokedAt);
    }

    [Fact]
    public async Task Locked_to_active_revokes_all_old_credentials()
    {
        // Arrange: user Locked có cả session và refresh token cũ chưa revoke.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var admin = await fixture.AddUserAsync("Admin", role: SystemRole.SystemAdmin);
        var target = await fixture.AddUserAsync("Locked target", UserStatus.Locked);
        var session = await fixture.AddSessionAsync(target.Id);
        var token = await fixture.AddRefreshTokenAsync(target.Id, "locked-old-token");
        fixture.Db.ChangeTracker.Clear();

        // Act: kích hoạt lại tài khoản Locked.
        await ChangeStatusAsync(fixture, admin.Id, target.Id, "Active");

        // Assert: cả hai credential cũ đều bị thu hồi.
        fixture.Db.ChangeTracker.Clear();
        Assert.NotNull((await fixture.Db.UserSessions.FindAsync(session.Id))!.RevokedAt);
        Assert.NotNull((await fixture.Db.RefreshTokens.FindAsync(token.Id))!.RevokedAt);
    }

    [Fact]
    public async Task Suspended_to_active_revokes_all_old_credentials()
    {
        // Arrange: mô phỏng credential tồn tại từ trước bản sửa session enforcement.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var admin = await fixture.AddUserAsync("Admin", role: SystemRole.SystemAdmin);
        var target = await fixture.AddUserAsync("Suspended target", UserStatus.Suspended);
        var session = await fixture.AddSessionAsync(target.Id);
        var token = await fixture.AddRefreshTokenAsync(target.Id, "suspended-old-token");
        fixture.Db.ChangeTracker.Clear();

        // Act: admin đưa Suspended về Active.
        await ChangeStatusAsync(fixture, admin.Id, target.Id, "Active");

        // Assert: không credential cũ nào sống lại theo trạng thái User.
        fixture.Db.ChangeTracker.Clear();
        Assert.NotNull((await fixture.Db.UserSessions.FindAsync(session.Id))!.RevokedAt);
        Assert.NotNull((await fixture.Db.RefreshTokens.FindAsync(token.Id))!.RevokedAt);
    }

    [Fact]
    public async Task Reactivating_does_not_create_a_new_session()
    {
        // Arrange: user Restricted có đúng một session cũ.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var admin = await fixture.AddUserAsync("Admin", role: SystemRole.SystemAdmin);
        var target = await fixture.AddUserAsync("Session count target", UserStatus.Restricted);
        await fixture.AddSessionAsync(target.Id);
        var countBefore = await fixture.Db.UserSessions.CountAsync(x => x.UserId == target.Id);
        fixture.Db.ChangeTracker.Clear();

        // Act: kích hoạt lại không đi qua Login/IssueAsync.
        await ChangeStatusAsync(fixture, admin.Id, target.Id, "Active");

        // Assert: chỉ thu hồi bản ghi cũ, không tự tạo phiên đăng nhập mới.
        fixture.Db.ChangeTracker.Clear();
        Assert.Equal(
            countBefore,
            await fixture.Db.UserSessions.CountAsync(x => x.UserId == target.Id));
    }

    [Fact]
    public async Task Reactivating_does_not_create_a_new_refresh_token()
    {
        // Arrange: user Restricted có đúng một refresh token cũ.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var admin = await fixture.AddUserAsync("Admin", role: SystemRole.SystemAdmin);
        var target = await fixture.AddUserAsync("Token count target", UserStatus.Restricted);
        await fixture.AddRefreshTokenAsync(target.Id, "count-old-token");
        var countBefore = await fixture.Db.RefreshTokens.CountAsync(x => x.UserId == target.Id);
        fixture.Db.ChangeTracker.Clear();

        // Act: reactivation chỉ là thao tác quản trị trạng thái.
        await ChangeStatusAsync(fixture, admin.Id, target.Id, "Active");

        // Assert: không phát hành refresh token thay thế.
        fixture.Db.ChangeTracker.Clear();
        Assert.Equal(
            countBefore,
            await fixture.Db.RefreshTokens.CountAsync(x => x.UserId == target.Id));
    }

    [Fact]
    public async Task Active_to_active_does_not_revoke_current_session()
    {
        // Arrange: tài khoản đã Active và session hiện tại còn hợp lệ.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var admin = await fixture.AddUserAsync("Admin", role: SystemRole.SystemAdmin);
        var target = await fixture.AddUserAsync("Same status target");
        var session = await fixture.AddSessionAsync(target.Id);
        fixture.Db.ChangeTracker.Clear();

        // Act: admin gửi lại cùng trạng thái Active.
        await ChangeStatusAsync(fixture, admin.Id, target.Id, "Active");

        // Assert: không thu hồi credential vì không có state transition.
        fixture.Db.ChangeTracker.Clear();
        Assert.Null((await fixture.Db.UserSessions.FindAsync(session.Id))!.RevokedAt);
    }

    [Fact]
    public async Task Active_to_suspended_still_revokes_all_credentials()
    {
        // Arrange: bảo vệ chiều Active sang non-active sau thay đổi điều kiện transition.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var admin = await fixture.AddUserAsync("Admin", role: SystemRole.SystemAdmin);
        var target = await fixture.AddUserAsync("Suspend target");
        var session = await fixture.AddSessionAsync(target.Id);
        var token = await fixture.AddRefreshTokenAsync(target.Id, "active-suspend-token");
        fixture.Db.ChangeTracker.Clear();

        // Act: chuyển Active sang Suspended.
        await ChangeStatusAsync(fixture, admin.Id, target.Id, "Suspended");

        // Assert: chiều khóa tài khoản vẫn thu hồi session và refresh token.
        fixture.Db.ChangeTracker.Clear();
        Assert.NotNull((await fixture.Db.UserSessions.FindAsync(session.Id))!.RevokedAt);
        Assert.NotNull((await fixture.Db.RefreshTokens.FindAsync(token.Id))!.RevokedAt);
    }

    private static async Task ChangeStatusAsync(
        TargetedCorrectnessFixture fixture,
        Guid adminId,
        Guid userId,
        string status)
    {
        // Dùng handler và AuthenticationSupport thật để kiểm tra ExecuteUpdate trong transaction.
        var tokens = new TargetedTokenService();
        var pinService = new SecurityPinService();
        var admin = await fixture.Db.Users.FindAsync(adminId)
            ?? throw new InvalidOperationException("Admin test user not found.");
        admin.SecurityPinHash = pinService.Hash("123456");
        await fixture.Db.SaveChangesAsync();
        var handler = new ChangeUserStatusCommandHandler(
            fixture.Db,
            new AuthenticationSupport(fixture.Db, tokens, fixture.Clock),
            new AdminAuditWriter(fixture.Db),
            new AdminSecurityGuard(fixture.Db, pinService,
                new AccountCredentialLockout(fixture.Db, fixture.Clock)));
        await handler.Handle(
            new ChangeUserStatusCommand(
                adminId,
                userId,
                new ChangeUserStatusRequest(status, "Targeted status change", "123456")),
            default);
    }
}
