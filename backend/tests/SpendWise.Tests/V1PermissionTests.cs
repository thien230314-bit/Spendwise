using System.Security.Claims;
using SpendWise.Application.Features.Administration;
using SpendWise.Domain.Identity;
using SpendWise.Domain.Admin;
using Xunit;

namespace SpendWise.Tests;

public sealed class V1PermissionTests
{
    [Theory]
    [InlineData(SystemRole.User, false, false)]
    [InlineData(SystemRole.SystemAdmin, false, false)]
    [InlineData(SystemRole.Admin, true, false)]
    [InlineData(SystemRole.SuperAdmin, false, true)]
    [InlineData((SystemRole)99, false, false)]
    public void Capabilities_do_not_promote_legacy_roles_or_bootstrap_an_owner(
        SystemRole role, bool operations, bool manageAdmins)
    {
        Assert.Equal(operations, PlatformPermissions.Allows(role, PlatformPermission.OperationsRead));
        Assert.Equal(manageAdmins, PlatformPermissions.Allows(role, PlatformPermission.ManageOrdinaryAdmins));
        Assert.False(PlatformPermissions.Allows(role, PlatformPermission.ProvisionSuperAdmin));
        Assert.False(PlatformPermissions.Allows(role, (PlatformPermission)99));
    }

    [Fact]
    public async Task Current_database_role_and_live_session_are_required_not_cached_claims()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var actor = await f.AddUserAsync("Operator", role: SystemRole.Admin);
        var session = new UserSession { UserId = actor.Id, LastSeenAt = f.Clock.UtcNow };
        f.Db.UserSessions.Add(session);
        await f.Db.SaveChangesAsync();
        var service = new PlatformPermissionService(f.Db);
        var principal = Principal(actor.Id, session.Id, "Admin");
        Assert.True(await service.HasAsync(principal, PlatformPermission.OperationsRead, default));
        Assert.False(await service.HasAsync(principal, PlatformPermission.ManageOrdinaryAdmins, default));
        Assert.False(await service.HasAsync(Principal(actor.Id, session.Id, "SuperAdmin"), PlatformPermission.OperationsRead, default));
        actor.SystemRole = SystemRole.User;
        await f.Db.SaveChangesAsync();
        Assert.False(await service.HasAsync(principal, PlatformPermission.OperationsRead, default));
        actor.SystemRole = SystemRole.Admin;
        session.RevokedAt = f.Clock.UtcNow;
        await f.Db.SaveChangesAsync();
        Assert.False(await service.HasAsync(principal, PlatformPermission.OperationsRead, default));
    }

    [Fact]
    public async Task Even_SuperAdmin_needs_assigned_case_for_the_specific_user()
    {
        var role = SystemRole.Admin;
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var actor = await f.AddUserAsync("Case operator", role: role);
        var target = await f.AddUserAsync("Case subject");
        var other = await f.AddUserAsync("Other subject");
        var session = new UserSession { UserId = actor.Id, LastSeenAt = f.Clock.UtcNow };
        var ticket = new SupportCase { LinkedUserId = target.Id, Subject = "Assigned case", Description = "Test only", AssignedAdminId = actor.Id };
        f.Db.AddRange(session, ticket);
        await f.Db.SaveChangesAsync();
        var service = new PlatformPermissionService(f.Db);
        var principal = Principal(actor.Id, session.Id, role.ToString());
        Assert.True(await service.HasSupportScopeAsync(principal, ticket.Id, target.Id, default));
        Assert.False(await service.HasSupportScopeAsync(principal, ticket.Id, other.Id, default));
        ticket.AssignedAdminId = null;
        await f.Db.SaveChangesAsync();
        Assert.False(await service.HasSupportScopeAsync(principal, ticket.Id, target.Id, default));
    }

    [Fact]
    public async Task Anonymous_and_ambiguous_role_claims_fail_closed()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var service = new PlatformPermissionService(f.Db);
        Assert.False(await service.HasAsync(new ClaimsPrincipal(), PlatformPermission.OperationsRead, default));
        var principal = Principal(Guid.NewGuid(), Guid.NewGuid(), "Admin");
        ((ClaimsIdentity)principal.Identity!).AddClaim(new Claim(ClaimTypes.Role, "SuperAdmin"));
        Assert.False(await service.HasAsync(principal, PlatformPermission.OperationsRead, default));
    }

    [Theory]
    [InlineData("2")]
    [InlineData("Admin, SuperAdmin")]
    [InlineData("admin")]
    public async Task Role_claim_must_be_one_canonical_role_name(string claim)
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var actor = await f.AddUserAsync("Operator", role: SystemRole.Admin);
        var session = await f.AddSessionAsync(actor.Id);
        Assert.False(await new PlatformPermissionService(f.Db).HasAsync(
            Principal(actor.Id, session.Id, claim), PlatformPermission.OperationsRead, default));
    }

    internal static ClaimsPrincipal Principal(Guid user, Guid session, string role) => new(
        new ClaimsIdentity([new(ClaimTypes.NameIdentifier, user.ToString()), new("sid", session.ToString()), new(ClaimTypes.Role, role)], "test"));
}
