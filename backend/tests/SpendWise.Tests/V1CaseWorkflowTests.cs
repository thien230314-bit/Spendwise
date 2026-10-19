using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Features.Administration;
using SpendWise.Domain.Admin;
using SpendWise.Domain.Identity;
using Xunit;

namespace SpendWise.Tests;

public sealed class V1CaseWorkflowTests
{
    [Fact]
    public async Task Account_investigation_is_bound_to_live_case_subject_and_audited_without_credential_fields()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync(); var admin = await f.AddUserAsync("Operator", role: SystemRole.Admin);
        var subject = await f.AddUserAsync("Subject"); var outsider = await f.AddUserAsync("Other");
        var session = await f.AddSessionAsync(admin.Id); await f.AddSessionAsync(subject.Id); await f.AddSessionAsync(outsider.Id);
        var wallet = await f.AddWalletAsync(subject.Id, "Main", true, balance: 25); await f.AddWalletAsync(outsider.Id, "Other", true, balance: 90);
        var ticket = new SupportCase { AssignedAdminId = admin.Id, LinkedUserId = subject.Id, TicketNumber = "CASE-SYNTHETIC" };
        f.Db.Add(ticket); await f.Db.SaveChangesAsync(); var service = new V1CaseWorkflow(f.Db, new(f.Db), new(f.Db), new SpendWise.Infrastructure.Notifications.NotificationService(f.Db), f.Clock);
        var principal = V1PermissionTests.Principal(admin.Id, session.Id, "Admin");
        var accounts = await service.AccountsAsync(principal, ticket.Id, "Investigate assigned case", default);
        Assert.Equal(wallet.Id, Assert.Single(accounts.Wallets).Id); Assert.Single(accounts.Sessions);
        Assert.Single(await f.Db.AuditLogs.Where(a => a.Action == "V1_CASE_ACCOUNTS_READ").ToListAsync());
        await f.Db.UserSessions.Where(s => s.Id == session.Id).ExecuteUpdateAsync(s => s.SetProperty(s => s.RevokedAt, f.Clock.UtcNow));
        await Assert.ThrowsAsync<ForbiddenException>(() => service.AccountsAsync(principal, ticket.Id, "Investigate assigned case", default));
        Assert.Empty(await f.Db.LedgerTransactions.ToListAsync());
    }
    [Fact]
    public async Task Assigned_operator_escalates_supervisor_claims_and_guides_without_financial_mutation()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var admin = await f.AddUserAsync("Operator", role: SystemRole.Admin);
        var sup = await f.AddUserAsync("Supervisor", role: SystemRole.SuperAdmin);
        var other = await f.AddUserAsync("Unassigned", role: SystemRole.Admin); var user = await f.AddUserAsync("Subject");
        var a = V1PermissionTests.Principal(admin.Id, (await f.AddSessionAsync(admin.Id)).Id, "Admin");
        var s = V1PermissionTests.Principal(sup.Id, (await f.AddSessionAsync(sup.Id)).Id, "SuperAdmin");
        var o = V1PermissionTests.Principal(other.Id, (await f.AddSessionAsync(other.Id)).Id, "Admin");
        var ticket = new SupportCase { AssignedAdminId = admin.Id, LinkedUserId = user.Id, Subject = "Synthetic", Description = "Synthetic" };
        f.Db.Add(ticket); await f.Db.SaveChangesAsync(); var service = new V1CaseWorkflow(f.Db, new(f.Db), new(f.Db), new SpendWise.Infrastructure.Notifications.NotificationService(f.Db), f.Clock);
        await Assert.ThrowsAsync<ForbiddenException>(() => service.ActAsync(o, ticket.Id, new("escalate", "Need policy guidance", "New"), default));
        await service.ActAsync(a, ticket.Id, new("escalate", "Need policy guidance", "New"), default);
        Assert.DoesNotContain(await f.Db.CaseHistories.ToListAsync(), h => h.Message.Contains("policy guidance"));
        Assert.False(await new PlatformPermissionService(f.Db).HasSupportScopeAsync(s, ticket.Id, user.Id, default));
        await service.ActAsync(s, ticket.Id, new("claim", "Review assigned escalation", "New"), default);
        Assert.True(await new PlatformPermissionService(f.Db).HasSupportScopeAsync(s, ticket.Id, user.Id, default));
        await Assert.ThrowsAsync<ConflictException>(() => service.ActAsync(a, ticket.Id, new("status", "Resolve before review", "New", "resolved"), default));
        await service.ActAsync(s, ticket.Id, new("guidance", "Verified non-financial guidance", "New"), default);
        await service.ActAsync(a, ticket.Id, new("status", "Resolved with documented guidance", "New", "resolved"), default);
        Assert.False(await new PlatformPermissionService(f.Db).HasSupportScopeAsync(s, ticket.Id, user.Id, default));
        Assert.Equal(4, await f.Db.CaseHistories.CountAsync()); Assert.Equal(4, await f.Db.AuditLogs.CountAsync(a => a.Action != "V1_CASE_ACCESS_DENIED"));
        Assert.Empty(await f.Db.LedgerTransactions.ToListAsync());
        await Assert.ThrowsAsync<ForbiddenException>(() => service.ActAsync(a, ticket.Id, new("reply", "Late reply", "Resolved"), default));
    }
}
