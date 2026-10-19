using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Features.Admin;
using SpendWise.Application.Features.Administration;
using SpendWise.Application.Features.Authentication;
using SpendWise.Domain.Identity;
using SpendWise.Infrastructure.Identity;
using Xunit;

namespace SpendWise.Tests;

public sealed class V1AdminLifecycleTests
{
    [Fact]
    public async Task Operator_directory_excludes_peers_and_is_denied_after_session_revocation()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var supervisor = await f.AddUserAsync("Supervisor", role: SystemRole.SuperAdmin);
        var admin = await f.AddUserAsync("Operator", role: SystemRole.Admin);
        await f.AddUserAsync("Legacy", role: SystemRole.SystemAdmin); await f.AddUserAsync("Ordinary");
        var session = await f.AddSessionAsync(supervisor.Id); var s = Service(f);
        var principal = V1PermissionTests.Principal(supervisor.Id, session.Id, "SuperAdmin");
        Assert.Equal(admin.Id, Assert.Single(await s.ListAsync(principal, "Review assigned operators", 1, 20, default)).Id);
        session.RevokedAt = f.Clock.UtcNow; await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<ForbiddenException>(() => s.ListAsync(principal, "Review assigned operators", 1, 20, default));
        Assert.Single(await f.Db.AuditLogs.Where(a => a.Action == "V1_ADMIN_DIRECTORY_READ").ToListAsync());
    }

    [Fact]
    public async Task Grant_and_revoke_are_audited_revoke_sessions_and_preserve_credentials()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var supervisor = await f.AddUserAsync("Supervisor", role: SystemRole.SuperAdmin);
        var target = await f.AddUserAsync("Operator");
        var actorSession = await f.AddSessionAsync(supervisor.Id);
        var session = await f.AddSessionAsync(target.Id);
        await f.AddRefreshTokenAsync(target.Id, "synthetic-refresh-hash", userSessionId: session.Id);
        var password = target.PasswordHash;
        var pin = target.SecurityPinHash;
        var service = Service(f);
        var principal = V1PermissionTests.Principal(supervisor.Id, actorSession.Id, "SuperAdmin");

        await service.SetAsync(principal, target.Id, true, TargetedCorrectnessFixture.SecurityPin, "Approved operator", default);
        Assert.Equal(SystemRole.Admin, await f.Db.Users.Where(u => u.Id == target.Id).Select(u => u.SystemRole).SingleAsync());
        Assert.False(await f.Db.UserSessions.AnyAsync(s => s.UserId == target.Id && s.RevokedAt == null));
        Assert.False(await f.Db.RefreshTokens.AnyAsync(s => s.UserId == target.Id && s.RevokedAt == null));
        Assert.True(await f.Db.UserSessions.AnyAsync(s => s.Id == actorSession.Id && s.RevokedAt == null));

        await service.SetAsync(principal, target.Id, false, TargetedCorrectnessFixture.SecurityPin, "Operator revoked", default);
        var stored = await f.Db.Users.AsNoTracking().SingleAsync(u => u.Id == target.Id);
        Assert.Equal(SystemRole.User, stored.SystemRole);
        Assert.Equal(password, stored.PasswordHash);
        Assert.Equal(pin, stored.SecurityPinHash);
        Assert.Equal(2, await f.Db.AuditLogs.CountAsync(a => a.Action == "V1_ADMIN_ROLE_CHANGED"));
        Assert.Empty(await f.Db.LedgerTransactions.ToListAsync());
    }

    [Theory]
    [InlineData(SystemRole.User)]
    [InlineData(SystemRole.Admin)]
    [InlineData(SystemRole.SystemAdmin)]
    public async Task Non_supervisor_cannot_grant_Admin_even_with_valid_PIN(SystemRole role)
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var actor = await f.AddUserAsync("Actor", role: role);
        var target = await f.AddUserAsync("Target");
        var session = await f.AddSessionAsync(actor.Id);
        await Assert.ThrowsAsync<ForbiddenException>(() => Service(f).SetAsync(
            V1PermissionTests.Principal(actor.Id, session.Id, role.ToString()), target.Id, true,
            TargetedCorrectnessFixture.SecurityPin, "Attempted grant", default));
        Assert.Equal(SystemRole.User, await f.Db.Users.Where(u => u.Id == target.Id).Select(u => u.SystemRole).SingleAsync());
    }

    [Theory]
    [InlineData(SystemRole.SystemAdmin)]
    [InlineData(SystemRole.SuperAdmin)]
    public async Task Legacy_admin_and_SuperAdmin_targets_are_never_relabelled(SystemRole role)
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var actor = await f.AddUserAsync("Supervisor", role: SystemRole.SuperAdmin);
        var target = await f.AddUserAsync("Protected target", role: role);
        var session = await f.AddSessionAsync(actor.Id);
        await Assert.ThrowsAsync<ForbiddenException>(() => Service(f).SetAsync(
            V1PermissionTests.Principal(actor.Id, session.Id, "SuperAdmin"), target.Id, false,
            TargetedCorrectnessFixture.SecurityPin, "Attempted revoke", default));
        Assert.Equal(role, await f.Db.Users.Where(u => u.Id == target.Id).Select(u => u.SystemRole).SingleAsync());
    }

    [Fact]
    public async Task Invalid_PIN_and_self_target_are_denied_without_role_change()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var actor = await f.AddUserAsync("Supervisor", role: SystemRole.SuperAdmin);
        var target = await f.AddUserAsync("Target");
        var session = await f.AddSessionAsync(actor.Id);
        var principal = V1PermissionTests.Principal(actor.Id, session.Id, "SuperAdmin");
        await Assert.ThrowsAsync<ForbiddenException>(() => Service(f).SetAsync(principal,
            target.Id, true, "654321", "Invalid verification", default));
        await Assert.ThrowsAsync<ForbiddenException>(() => Service(f).SetAsync(principal,
            actor.Id, false, TargetedCorrectnessFixture.SecurityPin, "Self revoke denied", default));
        Assert.Equal(SystemRole.User, await f.Db.Users.Where(u => u.Id == target.Id).Select(u => u.SystemRole).SingleAsync());
    }

    [Fact]
    public async Task Audit_failure_rolls_back_role_and_session_revocation_together()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var actor = await f.AddUserAsync("Supervisor", role: SystemRole.SuperAdmin);
        var target = await f.AddUserAsync("Target");
        var actorSession = await f.AddSessionAsync(actor.Id);
        var targetSession = await f.AddSessionAsync(target.Id);
        await f.AddRefreshTokenAsync(target.Id, "synthetic-refresh", userSessionId: targetSession.Id);
        await f.Db.Database.ExecuteSqlRawAsync("CREATE TRIGGER FailRoleAudit BEFORE INSERT ON AuditLogs BEGIN SELECT RAISE(ABORT, 'synthetic audit failure'); END;");
        await Assert.ThrowsAsync<DbUpdateException>(() => Service(f).SetAsync(
            V1PermissionTests.Principal(actor.Id, actorSession.Id, "SuperAdmin"), target.Id, true,
            TargetedCorrectnessFixture.SecurityPin, "Audited grant required", default));
        f.Db.ClearTrackedEntities();
        Assert.Equal(SystemRole.User, await f.Db.Users.Where(u => u.Id == target.Id).Select(u => u.SystemRole).SingleAsync());
        Assert.True(await f.Db.UserSessions.AnyAsync(s => s.Id == targetSession.Id && s.RevokedAt == null));
        Assert.True(await f.Db.RefreshTokens.AnyAsync(t => t.UserId == target.Id && t.RevokedAt == null));
    }

    private static OrdinaryAdminLifecycle Service(TargetedCorrectnessFixture f) => new(
        f.Db, new PlatformPermissionService(f.Db),
        new AdminSecurityGuard(f.Db, new SecurityPinService(), new AccountCredentialLockout(f.Db, f.Clock)),
        new AuthenticationSupport(f.Db, new TargetedTokenService(), f.Clock), new AdminAuditWriter(f.Db));
}
