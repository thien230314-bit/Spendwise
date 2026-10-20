using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Admin;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Features.Admin;
using SpendWise.Application.Features.Admin.Cases;
using SpendWise.Application.Features.Administration;
using SpendWise.Application.Features.Authentication;
using SpendWise.Application.Features.Operations;
using SpendWise.Application.Identity;
using SpendWise.Domain.Admin;
using SpendWise.Domain.Identity;
using SpendWise.Infrastructure.Identity;
using SpendWise.Infrastructure.Notifications;
using SpendWise.Infrastructure.Persistence;
using Xunit;

namespace SpendWise.Tests;

public sealed class Task16_4_AdminCleanupTests
{
    private static AdminA5Service CreateA5Service(TargetedCorrectnessFixture f) =>
        new(
            f.Db,
            new(f.Db),
            new(f.Db, new(f.Db), new(f.Db), new NotificationService(f.Db), f.Clock),
            new(f.Db),
            new NotificationService(f.Db),
            new(new EmailTemplateRenderer(), new NullEmailDeliveryQueue(), new TestEmailVerificationPolicy()),
            new EmailTemplateRenderer(),
            new CaseAttachmentValidator(),
            f.Clock
        );

    private sealed class NullEmailDeliveryQueue : IEmailDeliveryQueue
    {
        public Task<bool> EnqueueAsync(EmailMessage message, string deduplicationKey, CancellationToken cancellationToken) =>
            Task.FromResult(true);
    }

    private sealed class TestEmailVerificationPolicy : IEmailVerificationPolicy
    {
        public TimeSpan OtpLifetime => TimeSpan.FromMinutes(15);
        public TimeSpan ResendCooldown => TimeSpan.FromMinutes(1);
        public int MaximumFailedAttempts => 5;
        public int MaximumSendsPerHourPerAccount => 10;
        public int MaximumSendsPerHourPerIp => 30;
        public string WebsiteUrl => "https://spendwise.local";
    }

    [Fact]
    public async Task Admin_Can_View_Unassigned_Tickets_In_Support_Queue()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var admin = await f.AddUserAsync("AdminOp", role: SystemRole.Admin);
        var session = await f.AddSessionAsync(admin.Id);
        var subject = await f.AddUserAsync("SubjectUser");

        var ticketUnassigned = new SupportCase { TicketNumber = "TK-164-UNASSIGNED", LinkedUserId = subject.Id, AssignedAdminId = null, Subject = "Unassigned Case", Description = "Waiting for assignment", Status = CaseStatus.Open };
        var ticketAssignedToSelf = new SupportCase { TicketNumber = "TK-164-SELF", LinkedUserId = subject.Id, AssignedAdminId = admin.Id, Subject = "Assigned To Self", Description = "In progress", Status = CaseStatus.Investigating };
        var otherAdmin = await f.AddUserAsync("OtherAdmin", role: SystemRole.Admin);
        var ticketAssignedToOther = new SupportCase { TicketNumber = "TK-164-OTHER", LinkedUserId = subject.Id, AssignedAdminId = otherAdmin.Id, Subject = "Assigned To Other", Description = "In progress", Status = CaseStatus.Investigating };

        f.Db.AddRange(ticketUnassigned, ticketAssignedToSelf, ticketAssignedToOther);
        await f.Db.SaveChangesAsync();

        var service = new V1CaseAssignment(
            f.Db,
            new PlatformPermissionService(f.Db),
            new AdminSecurityGuard(f.Db, new SecurityPinService(), new AccountCredentialLockout(f.Db, f.Clock)),
            new AdminAuditWriter(f.Db),
            new NotificationService(f.Db));

        var principal = V1PermissionTests.Principal(admin.Id, session.Id, "Admin");
        var queue = await service.QueueAsync(principal, "Reviewing open support tickets", 1, 20, default);

        // Admin should see unassigned ticket and ticket assigned to self, but NOT ticket assigned to other
        Assert.Contains(queue, t => t.Id == ticketUnassigned.Id);
        Assert.Contains(queue, t => t.Id == ticketAssignedToSelf.Id);
        Assert.DoesNotContain(queue, t => t.Id == ticketAssignedToOther.Id);
    }

    [Fact]
    public async Task Admin_Can_Self_Assign_Unassigned_Ticket_Through_AdminA5Service()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var admin = await f.AddUserAsync("SelfAssignAdmin", role: SystemRole.Admin);
        var session = await f.AddSessionAsync(admin.Id);
        var subject = await f.AddUserAsync("SubjectUser2");

        var ticket = new SupportCase { TicketNumber = "TK-164-ASSIGN", LinkedUserId = subject.Id, AssignedAdminId = null, Subject = "Dispute Case", Description = "Needs resolution", Status = CaseStatus.Open };
        f.Db.Add(ticket);
        await f.Db.SaveChangesAsync();

        var a5 = CreateA5Service(f);
        var principal = V1PermissionTests.Principal(admin.Id, session.Id, "Admin");
        var result = await a5.AssignTicketAsync(principal, ticket.Id, new A5AssignTicketRequest("Taking ownership of ticket", admin.Id), default);

        Assert.True(result);
        var updated = await f.Db.SupportCases.SingleAsync(c => c.Id == ticket.Id);
        Assert.Equal(admin.Id, updated.AssignedAdminId);

        var history = await f.Db.CaseHistories.SingleOrDefaultAsync(h => h.CaseId == ticket.Id && h.Action == "A5_ASSIGNED");
        Assert.NotNull(history);
        Assert.Equal("Admin", history.ActorRole);
    }

    [Fact]
    public async Task Admin_Stealing_Another_Admins_Ticket_Is_Denied()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var adminA = await f.AddUserAsync("AdminA", role: SystemRole.Admin);
        var adminB = await f.AddUserAsync("AdminB", role: SystemRole.Admin);
        var sessionB = await f.AddSessionAsync(adminB.Id);
        var subject = await f.AddUserAsync("SubjectUser3");

        var ticket = new SupportCase { TicketNumber = "TK-164-STOLEN", LinkedUserId = subject.Id, AssignedAdminId = adminA.Id, Subject = "Locked Case", Description = "Under AdminA", Status = CaseStatus.Investigating };
        f.Db.Add(ticket);
        await f.Db.SaveChangesAsync();

        var a5 = CreateA5Service(f);
        var principalB = V1PermissionTests.Principal(adminB.Id, sessionB.Id, "Admin");
        var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
            a5.AssignTicketAsync(principalB, ticket.Id, new A5AssignTicketRequest("Attempting steal", adminB.Id), default));

        Assert.Equal("assignment_denied", ex.Code);
    }

    [Fact]
    public async Task V1CaseWorkflow_Escalate_Or_Claim_Is_Disabled_In_V1()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var admin = await f.AddUserAsync("WorkflowAdmin", role: SystemRole.Admin);
        var session = await f.AddSessionAsync(admin.Id);
        var subject = await f.AddUserAsync("SubjectUser4");

        var ticket = new SupportCase { TicketNumber = "TK-164-WORKFLOW", LinkedUserId = subject.Id, AssignedAdminId = admin.Id, Subject = "Escalation Test", Description = "Testing V1 workflow", Status = CaseStatus.Open };
        f.Db.Add(ticket);
        await f.Db.SaveChangesAsync();

        var workflow = new V1CaseWorkflow(
            f.Db,
            new PlatformPermissionService(f.Db),
            new AdminAuditWriter(f.Db),
            new NotificationService(f.Db),
            f.Clock);

        var principal = V1PermissionTests.Principal(admin.Id, session.Id, "Admin");
        var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
            workflow.ActAsync(principal, ticket.Id, new V1CaseAction("escalate", "Valid escalation reason for audit", "Open"), default));

        Assert.Equal("escalation_disabled", ex.Code);
    }

    [Fact]
    public async Task V1Broadcast_Rejects_SuperAdmin_TargetRole()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var admin = await f.AddUserAsync("BroadcastAdmin", role: SystemRole.Admin);
        var session = await f.AddSessionAsync(admin.Id);

        var service = V1BroadcastTests.Service(f, true);
        var principal = V1PermissionTests.Principal(admin.Id, session.Id, "Admin");
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            service.DraftAsync(principal, new BroadcastDraftRequest(
                "Operational",
                "SuperAdmin",
                "Maintenance Notice",
                "System maintenance window",
                "idem-key-1",
                TargetedCorrectnessFixture.SecurityPin,
                "Testing role target"), default));

        Assert.Equal("broadcast_contract_required", ex.Code);
    }
}
