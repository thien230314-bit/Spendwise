using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Features.Administration;
using SpendWise.Application.Features.Admin;
using SpendWise.Application.Features.Authentication;
using SpendWise.Domain.Admin;
using SpendWise.Domain.Identity;
using SpendWise.Infrastructure.Identity;
using Xunit;

namespace SpendWise.Tests;

public sealed class V1CaseAssignmentTests
{
    [Fact]
    public async Task Supervisor_assignment_moves_live_scope_and_replay_does_not_duplicate_history_or_notification()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var supervisor = await f.AddUserAsync("Supervisor", role: SystemRole.SuperAdmin); var a = await f.AddUserAsync("A", role: SystemRole.Admin);
        var b = await f.AddUserAsync("B", role: SystemRole.Admin); var subject = await f.AddUserAsync("Subject");
        var session = await f.AddSessionAsync(supervisor.Id); var sa = await f.AddSessionAsync(a.Id); var sb = await f.AddSessionAsync(b.Id);
        var ticket = new SupportCase { LinkedUserId = subject.Id, AssignedAdminId = a.Id, Subject = "Synthetic", Description = "Synthetic" }; f.Db.Add(ticket); await f.Db.SaveChangesAsync();
        var service = new V1CaseAssignment(f.Db, new(f.Db), new AdminSecurityGuard(f.Db, new SecurityPinService(), new AccountCredentialLockout(f.Db, f.Clock)), new(f.Db), new SpendWise.Infrastructure.Notifications.NotificationService(f.Db));
        var principal = V1PermissionTests.Principal(supervisor.Id, session.Id, "SuperAdmin");
        await service.AssignAsync(principal, ticket.Id, b.Id, TargetedCorrectnessFixture.SecurityPin, "Assign investigation", default);
        await service.AssignAsync(principal, ticket.Id, b.Id, TargetedCorrectnessFixture.SecurityPin, "Repeat assignment", default);
        var permissions = new PlatformPermissionService(f.Db);
        Assert.False(await permissions.HasSupportScopeAsync(V1PermissionTests.Principal(a.Id, sa.Id, "Admin"), ticket.Id, subject.Id, default));
        Assert.True(await permissions.HasSupportScopeAsync(V1PermissionTests.Principal(b.Id, sb.Id, "Admin"), ticket.Id, subject.Id, default));
        Assert.Single(await f.Db.CaseHistories.ToListAsync()); Assert.Single(await f.Db.Notifications.ToListAsync());
        Assert.Empty(await f.Db.LedgerTransactions.ToListAsync());
    }
}
