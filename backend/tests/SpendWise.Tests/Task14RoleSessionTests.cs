using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Features.Authentication;
using SpendWise.Application.Features.Authentication.Commands.RefreshToken;
using SpendWise.Application.Identity;
using SpendWise.Domain.Identity;
using SpendWise.Infrastructure.Identity;
using Xunit;

namespace SpendWise.Tests;

public sealed class Task14RoleSessionTests
{
    [Fact]
    public async Task Existing_session_is_denied_after_user_becomes_blocked_superadmin()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var user = await f.AddUserAsync("Previously ordinary user");
        user.EmailVerifiedAt = f.Clock.UtcNow;
        await f.Db.SaveChangesAsync();
        var session = await f.AddSessionAsync(user.Id);
        var validator = new AccessSessionValidator(f.Db);
        Assert.True(await validator.IsValidAsync(user.Id, session.Id, default));
        user.SystemRole = SystemRole.SuperAdmin;
        await f.Db.SaveChangesAsync();

        Assert.False(await validator.IsValidAsync(user.Id, session.Id, default));
        Assert.Single(await f.Db.UserSessions.AsNoTracking().ToListAsync());
        Assert.Null(await f.Db.UserSessions.AsNoTracking().Where(x => x.Id == session.Id).Select(x => x.RevokedAt).SingleAsync());
    }

    [Fact]
    public async Task Session_issuance_for_blocked_superadmin_fails_before_creating_credentials()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var user = await f.AddUserAsync("Blocked SuperAdmin", role: SystemRole.SuperAdmin);
        user.EmailVerifiedAt = f.Clock.UtcNow;
        await f.Db.SaveChangesAsync();
        var support = new AuthenticationSupport(f.Db, new TargetedTokenService(), f.Clock);

        var error = await Record.ExceptionAsync(() => support.IssueAsync(user, "Test device", null, null, default));

        Assert.Empty(await f.Db.UserSessions.AsNoTracking().ToListAsync());
        Assert.Empty(await f.Db.RefreshTokens.AsNoTracking().ToListAsync());
        var unavailable = Assert.IsAssignableFrom<AppException>(error);
        Assert.Equal("account_unavailable", unavailable.Code);
        Assert.Equal(403, unavailable.StatusCode);
    }

    [Fact]
    public async Task Refresh_for_blocked_superadmin_fails_without_consuming_or_rotating_credentials()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var user = await f.AddUserAsync("Previously ordinary user");
        user.EmailVerifiedAt = f.Clock.UtcNow;
        await f.Db.SaveChangesAsync();
        var original = await f.AddRefreshTokenAsync(user.Id, "task14-original-refresh");
        user.SystemRole = SystemRole.SuperAdmin;
        await f.Db.SaveChangesAsync();
        var handler = new RefreshTokenCommandHandler(f.Db, new TargetedTokenService(), f.Clock);

        var error = await Record.ExceptionAsync(() => handler.Handle(
            new RefreshTokenCommand(new RefreshRequest("task14-original-refresh"), null, null), default));

        var persisted = Assert.Single(await f.Db.RefreshTokens.AsNoTracking().ToListAsync());
        Assert.Equal(original.Id, persisted.Id);
        Assert.Equal(original.TokenHash, persisted.TokenHash);
        Assert.Equal(original.ExpiresAt, persisted.ExpiresAt);
        Assert.Null(persisted.RevokedAt);
        Assert.Null(persisted.ReplacedByTokenId);
        var session = Assert.Single(await f.Db.UserSessions.AsNoTracking().ToListAsync());
        Assert.Equal(original.UserSessionId, session.Id);
        Assert.Null(session.RevokedAt);
        var unavailable = Assert.IsAssignableFrom<AppException>(error);
        Assert.Equal("account_unavailable", unavailable.Code);
        Assert.Equal(403, unavailable.StatusCode);
    }

    [Fact]
    public void Blocked_superadmin_has_no_platform_permissions()
    {
        foreach (var permission in Enum.GetValues<PlatformPermission>())
            Assert.False(PlatformPermissions.Allows(SystemRole.SuperAdmin, permission));
    }
}
