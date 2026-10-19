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
    public async Task Assigned_operator_handles_status_and_replies_without_financial_mutation()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var admin = await f.AddUserAsync("Operator", role: SystemRole.Admin);
        var other = await f.AddUserAsync("Unassigned", role: SystemRole.Admin); var user = await f.AddUserAsync("Subject");
        var a = V1PermissionTests.Principal(admin.Id, (await f.AddSessionAsync(admin.Id)).Id, "Admin");
        var o = V1PermissionTests.Principal(other.Id, (await f.AddSessionAsync(other.Id)).Id, "Admin");
        var ticket = new SupportCase { AssignedAdminId = admin.Id, LinkedUserId = user.Id, Subject = "Synthetic", Description = "Synthetic", Status = CaseStatus.Open };
        f.Db.Add(ticket); await f.Db.SaveChangesAsync(); var service = new V1CaseWorkflow(f.Db, new(f.Db), new(f.Db), new SpendWise.Infrastructure.Notifications.NotificationService(f.Db), f.Clock);
        await Assert.ThrowsAsync<ForbiddenException>(() => service.ActAsync(o, ticket.Id, new("reply", "Unauthorized reply", "Open"), default));
        await service.ActAsync(a, ticket.Id, new("reply", "Customer reply sent", "Open"), default);
        await service.ActAsync(a, ticket.Id, new("status", "Issue resolved successfully", "Open", "resolved"), default);
        Assert.Equal(2, await f.Db.CaseHistories.CountAsync());
        Assert.Equal(2, await f.Db.AuditLogs.CountAsync(a => a.Action != "V1_CASE_ACCESS_DENIED"));
        Assert.Empty(await f.Db.LedgerTransactions.ToListAsync());
        await Assert.ThrowsAsync<ForbiddenException>(() => service.ActAsync(a, ticket.Id, new("reply", "Late reply", "Resolved"), default));
    }
}
