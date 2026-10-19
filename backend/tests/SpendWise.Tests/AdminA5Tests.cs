using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpendWise.Application.Admin;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Features.Admin.Cases;
using SpendWise.Application.Features.Administration;
using SpendWise.Application.Identity;
using SpendWise.Domain.Admin;
using SpendWise.Domain.Finance;
using SpendWise.Domain.Groups;
using SpendWise.Domain.Identity;
using SpendWise.Domain.V1Groups;
using SpendWise.Infrastructure.Identity;
using SpendWise.Infrastructure.Persistence;
using Xunit;

namespace SpendWise.Tests;

public sealed class AdminA5Tests
{
    private static AdminA5Service CreateService(TargetedCorrectnessFixture f) =>
        new(
            f.Db,
            new(f.Db),
            new(f.Db, new(f.Db), new(f.Db), new SpendWise.Infrastructure.Notifications.NotificationService(f.Db), f.Clock),
            new(f.Db),
            new SpendWise.Infrastructure.Notifications.NotificationService(f.Db),
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

    [Theory]
    [InlineData(SystemRole.Admin)]
    [InlineData(SystemRole.SuperAdmin)]
    [InlineData(SystemRole.User)]
    public async Task A5_HTTP_routes_enforce_canonical_roles_and_scope(SystemRole role)
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var caseId = Guid.NewGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            User Synthetic(SystemRole r)
            {
                var key = Guid.NewGuid().ToString("N");
                return new()
                {
                    FullName = "Synthetic " + r,
                    Email = key + "@test.local",
                    NormalizedEmail = key + "@TEST.LOCAL",
                    SpendId = key,
                    NormalizedSpendId = key,
                    PasswordHash = "synthetic",
                    SystemRole = r
                };
            }

            var actor = Synthetic(role);
            var subject = Synthetic(SystemRole.User);
            var session = new UserSession { UserId = actor.Id, LastSeenAt = DateTime.UtcNow };

            var ticket = new SupportCase
            {
                Id = caseId,
                TicketNumber = "SPW-TEST-001",
                LinkedUserId = subject.Id,
                AssignedAdminId = role == SystemRole.Admin ? actor.Id : null,
                EscalatedAt = role == SystemRole.SuperAdmin ? DateTime.UtcNow : null,
                EscalationReviewerId = role == SystemRole.SuperAdmin ? actor.Id : null,
                Status = CaseStatus.Open,
                Subject = "General help",
                Description = "Need support on my account"
            };

            db.AddRange(actor, subject, session, ticket);
            await db.SaveChangesAsync();

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
                scope.ServiceProvider.GetRequiredService<ITokenService>().CreateAccessToken(actor, session.Id).Token);
        }

        var root = "/api/v1/admin/support";

        // A5.1 Tickets Search
        using (var res = await client.PostAsJsonAsync(root + "/tickets/search", new { reason = "Review new tickets" }))
        {
            Assert.Equal(role == SystemRole.Admin ? HttpStatusCode.OK : HttpStatusCode.Forbidden, res.StatusCode);
        }

        // A5.2 Assigned cases
        using (var res = await client.PostAsJsonAsync(root + "/assigned", new { reason = "Check my assigned queue" }))
        {
            Assert.Equal(role == SystemRole.Admin ? HttpStatusCode.OK : HttpStatusCode.Forbidden, res.StatusCode);
        }

        // A5.4 Dossier
        using (var res = await client.PostAsJsonAsync(root + $"/{caseId}/dossier", new { reason = "Review case dossier" }))
        {
            Assert.Equal(role == SystemRole.Admin ? HttpStatusCode.OK : HttpStatusCode.Forbidden, res.StatusCode);
        }

        // A5.7 Templates list
        using (var res = await client.PostAsJsonAsync(root + "/templates", new { reason = "Read template catalog" }))
        {
            Assert.Equal(role == SystemRole.Admin ? HttpStatusCode.OK : HttpStatusCode.Forbidden, res.StatusCode);
        }
    }

    [Fact]
    public async Task A5_1_Tickets_Search_Filters_Correctly_And_Respects_Scope()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var admin = await f.AddUserAsync("AdminUser", role: SystemRole.Admin);
        var subject1 = await f.AddUserAsync("User1");
        var subject2 = await f.AddUserAsync("User2");
        var session = await f.AddSessionAsync(admin.Id);

        var c1 = new SupportCase { TicketNumber = "TK-1", LinkedUserId = subject1.Id, AssignedAdminId = admin.Id, Status = CaseStatus.Open, Category = "general", Subject = "Issue 1" };
        var c2 = new SupportCase { TicketNumber = "TK-2", LinkedUserId = subject2.Id, AssignedAdminId = null, Status = CaseStatus.New, Category = "transaction", Subject = "Issue 2" };
        var otherAdmin = await f.AddUserAsync("OtherAdmin", role: SystemRole.Admin);
        var c3 = new SupportCase { TicketNumber = "TK-3", LinkedUserId = subject2.Id, AssignedAdminId = otherAdmin.Id, Status = CaseStatus.Investigating, Category = "fund", Subject = "Issue 3" };

        f.Db.AddRange(c1, c2, c3);
        await f.Db.SaveChangesAsync();

        var service = CreateService(f);
        var principal = V1PermissionTests.Principal(admin.Id, session.Id, "Admin");

        // Ordinary admin should see c1 (assigned to self) and c2 (unassigned), but not c3 (assigned to other admin)
        var result = await service.TicketsAsync(principal, new("Search tickets"), default);
        Assert.Equal(2, result.Total);
        Assert.DoesNotContain(result.Items, x => x.TicketNumber == "TK-3");

        // Filter by Category = "general"
        var filtered = await service.TicketsAsync(principal, new("Filter category", Category: "general"), default);
        Assert.Single(filtered.Items);
        Assert.Equal("TK-1", filtered.Items[0].TicketNumber);
    }

    [Fact]
    public async Task A5_2_Assign_And_Claim_Workflow_Prevents_Cross_Admin_Hijack()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var admin1 = await f.AddUserAsync("AdminOne", role: SystemRole.Admin);
        var admin2 = await f.AddUserAsync("AdminTwo", role: SystemRole.Admin);
        var subject = await f.AddUserAsync("Customer");
        var session1 = await f.AddSessionAsync(admin1.Id);

        var ticket = new SupportCase { TicketNumber = "TK-ASSIGN", LinkedUserId = subject.Id, Status = CaseStatus.New };
        f.Db.Add(ticket);
        await f.Db.SaveChangesAsync();

        var service = CreateService(f);
        var principalAdmin1 = V1PermissionTests.Principal(admin1.Id, session1.Id, "Admin");

        // Ordinary Admin cannot assign to another admin (only self)
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.AssignTicketAsync(principalAdmin1, ticket.Id, new("Assign to other", admin2.Id), default));

        // Ordinary Admin assigns to self -> success
        var assigned = await service.AssignTicketAsync(principalAdmin1, ticket.Id, new("Assign to self", admin1.Id), default);
        Assert.True(assigned);

        var reloaded = await f.Db.SupportCases.AsNoTracking().SingleAsync(c => c.Id == ticket.Id);
        Assert.Equal(admin1.Id, reloaded.AssignedAdminId);
        Assert.Equal(CaseStatus.Investigating, reloaded.Status);
    }

    [Fact]
    public async Task A5_3_RequestInfo_Transitions_Case_To_WaitingForUser_And_Records_History()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var admin = await f.AddUserAsync("AdminUser", role: SystemRole.Admin);
        var subject = await f.AddUserAsync("Customer");
        var session = await f.AddSessionAsync(admin.Id);

        var ticket = new SupportCase { TicketNumber = "TK-REQINFO", LinkedUserId = subject.Id, AssignedAdminId = admin.Id, Status = CaseStatus.Open };
        f.Db.Add(ticket);
        await f.Db.SaveChangesAsync();

        var service = CreateService(f);
        var principal = V1PermissionTests.Principal(admin.Id, session.Id, "Admin");

        var success = await service.RequestInfoAsync(principal, ticket.Id, new("Need docs", "Vui long cung cap sao ke"), default);
        Assert.True(success);

        var updated = await f.Db.SupportCases.AsNoTracking().SingleAsync(c => c.Id == ticket.Id);
        Assert.Equal(CaseStatus.WaitingForUser, updated.Status);

        var history = await f.Db.CaseHistories.AsNoTracking().Where(h => h.CaseId == ticket.Id).ToListAsync();
        Assert.Contains(history, h => h.Action == "request_info" && h.NewStatus == "WaitingForUser");
    }

    [Fact]
    public async Task A5_4_FundDossier_Aggregates_Snapshot_And_Linked_Fund_Accurately()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var admin = await f.AddUserAsync("AdminUser", role: SystemRole.Admin);
        var subject = await f.AddUserAsync("Customer");
        var session = await f.AddSessionAsync(admin.Id);

        var group = new Group { Name = "Weekend Travel Group" };
        var ledgerAccount = new LedgerAccount { Code = "GRP:LED", Name = "Group Ledger", Type = LedgerAccountType.SharedFund };
        var sharedFund = new SharedFund { GroupId = group.Id, Balance = 2500000, Currency = "VND", LedgerAccountId = ledgerAccount.Id };
        var userWallet = new Wallet { OwnerUserId = subject.Id, Name = "Main", AvailableBalance = 5000000 };
        var ledgerTx = new LedgerTransaction { Reference = "TX-DOSSIER-LED", BusinessType = "GroupContribution", Status = LedgerTransactionStatus.Completed };
        var contrib = new GroupContribution { GroupId = group.Id, UserId = subject.Id, Amount = 1000000, WalletId = userWallet.Id, LedgerTransactionId = ledgerTx.Id };
        ledgerTx.BusinessId = contrib.Id;
        var tx = new FinancialTransaction { OwnerUserId = subject.Id, Type = FinancialTransactionType.Expense, Amount = 1000000, RelatedBusinessId = contrib.Id, LedgerTransaction = ledgerTx, OccurredAt = f.Clock.UtcNow };
        var ticket = new SupportCase { TicketNumber = "TK-DOSSIER", LinkedUserId = subject.Id, AssignedAdminId = admin.Id, LinkedTransactionId = tx.Id, Status = CaseStatus.Open };
        var dispute = new Dispute { UserId = subject.Id, LinkedTransactionId = tx.Id, Reason = "Charged twice", TransactionAmountSnapshot = 1000000, Status = CaseStatus.Open };

        f.Db.AddRange(group, ledgerAccount, sharedFund, userWallet, ledgerTx, contrib, tx, ticket, dispute);
        await f.Db.SaveChangesAsync();

        var service = CreateService(f);
        var principal = V1PermissionTests.Principal(admin.Id, session.Id, "Admin");

        var dossier = await service.FundDossierAsync(principal, ticket.Id, "Investigate fund dispute", default);
        Assert.Equal(ticket.Id, dossier.CaseId);
        Assert.Equal("Customer", dossier.UserFullName);
        Assert.NotNull(dossier.LinkedFund);
        Assert.Equal("Weekend Travel Group", dossier.LinkedFund!.FundName);
        Assert.Equal(2500000, dossier.LinkedFund.Balance);
        Assert.Single(dossier.LinkedDisputes);
        Assert.Equal(1000000, dossier.LinkedDisputes[0].Amount);
        Assert.Contains("READ_ONLY_DOSSIER", dossier.FinancialAuditNotice);
    }

    [Fact]
    public async Task A5_5_Escalate_Requires_Assigned_Admin_And_Sets_Escalated_Properties()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var admin = await f.AddUserAsync("AssignedAdmin", role: SystemRole.Admin);
        var otherAdmin = await f.AddUserAsync("OtherAdmin", role: SystemRole.Admin);
        var subject = await f.AddUserAsync("Customer");
        var session = await f.AddSessionAsync(admin.Id);
        var sessionOther = await f.AddSessionAsync(otherAdmin.Id);

        var ticket = new SupportCase { TicketNumber = "TK-ESC", LinkedUserId = subject.Id, AssignedAdminId = admin.Id, Status = CaseStatus.Investigating };
        f.Db.Add(ticket);
        await f.Db.SaveChangesAsync();

        var service = CreateService(f);
        var principalOther = V1PermissionTests.Principal(otherAdmin.Id, sessionOther.Id, "Admin");
        var principalAssigned = V1PermissionTests.Principal(admin.Id, session.Id, "Admin");

        // Non-assigned admin cannot escalate
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.EscalateAsync(principalOther, ticket.Id, new("Try escalate", "Complex case"), default));

        // Assigned admin escalates successfully
        var res = await service.EscalateAsync(principalAssigned, ticket.Id, new("Request guidance", "Need SuperAdmin guidance on complex fund"), default);
        Assert.Equal(ticket.Id, res.CaseId);
        Assert.Equal(admin.Id, res.EscalatedById);
        Assert.Equal("Need SuperAdmin guidance on complex fund", res.EscalationReason);

        // Cannot escalate twice
        await Assert.ThrowsAsync<ConflictException>(() =>
            service.EscalateAsync(principalAssigned, ticket.Id, new("Escalate again", "Repeated"), default));
    }

    [Fact]
    public async Task A5_6_Resolve_And_Reject_Require_Concrete_Reason_And_Valid_Status()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var admin = await f.AddUserAsync("AssignedAdmin", role: SystemRole.Admin);
        var subject = await f.AddUserAsync("Customer");
        var session = await f.AddSessionAsync(admin.Id);

        var ticket = new SupportCase { TicketNumber = "TK-RESOLVE", LinkedUserId = subject.Id, AssignedAdminId = admin.Id, Status = CaseStatus.Investigating };
        f.Db.Add(ticket);
        await f.Db.SaveChangesAsync();

        var service = CreateService(f);
        var principal = V1PermissionTests.Principal(admin.Id, session.Id, "Admin");

        // Message too short (< 5 chars) -> AppException
        await Assert.ThrowsAsync<AppException>(() =>
            service.ResolveAsync(principal, ticket.Id, new("Closing", "ok"), default));

        // Valid resolve
        var resolved = await service.ResolveAsync(principal, ticket.Id, new("Close case", "Van de da duoc giai quyet thoa dang"), default);
        Assert.Equal("Resolved", resolved.NewStatus);

        // Resolved case cannot be transitioned again; WithCaseAsync forbids access to inactive/resolved cases
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.ResolveAsync(principal, ticket.Id, new("Close again", "Reclosing already closed ticket"), default));
    }

    [Fact]
    public async Task A5_7_Templates_List_And_Apply_Prevents_Credential_Injection()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var admin = await f.AddUserAsync("AssignedAdmin", role: SystemRole.Admin);
        var subject = await f.AddUserAsync("Customer");
        var session = await f.AddSessionAsync(admin.Id);

        var ticket = new SupportCase { TicketNumber = "TK-TPL", LinkedUserId = subject.Id, AssignedAdminId = admin.Id, Status = CaseStatus.Open };
        f.Db.Add(ticket);
        await f.Db.SaveChangesAsync();

        var service = CreateService(f);
        var principal = V1PermissionTests.Principal(admin.Id, session.Id, "Admin");

        // 1. Check template catalog
        var templates = await service.GetTemplatesAsync(principal, "Get templates", default);
        Assert.NotEmpty(templates);
        Assert.Contains(templates, t => t.TemplateKey == "support-info-request");

        // 2. Reject injection of sensitive terms in parameters
        var badParams = new Dictionary<string, string?> { ["Note"] = "Here is your token: abc123xyz" };
        await Assert.ThrowsAsync<AppException>(() =>
            service.ApplyTemplateAsync(principal, ticket.Id, new("Apply", "support-info-request", badParams), default));

        // 3. Valid template application
        var safeParams = new Dictionary<string, string?> { ["RequiredDocuments"] = "Hóa đơn VAT và sao kê ngân hàng", ["Note"] = "Vui lòng phản hồi sớm" };
        var applied = await service.ApplyTemplateAsync(principal, ticket.Id, new("Apply template", "support-info-request", safeParams, TransitionToWaitingForUser: true), default);
        Assert.Equal("WaitingForUser", applied.UpdatedCaseStatus);
        Assert.True(applied.SentAsNotification);
    }

    [Fact]
    public async Task A5_2_FIX2_OrdinaryAdmin_Cannot_Steal_Ticket_Assigned_To_Another_Admin()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var adminA = await f.AddUserAsync("AdminAlpha", role: SystemRole.Admin);
        var adminB = await f.AddUserAsync("AdminBeta", role: SystemRole.Admin);
        var subject = await f.AddUserAsync("Customer");
        var sessionB = await f.AddSessionAsync(adminB.Id);

        // Case assigned to AdminAlpha
        var ticket = new SupportCase { TicketNumber = "TK-STOLEN", LinkedUserId = subject.Id, AssignedAdminId = adminA.Id, Status = CaseStatus.Investigating };
        f.Db.Add(ticket);
        await f.Db.SaveChangesAsync();

        var service = CreateService(f);
        var principalB = V1PermissionTests.Principal(adminB.Id, sessionB.Id, "Admin");

        // AdminBeta tries to claim ticket assigned to AdminAlpha -> ForbiddenException
        var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.AssignTicketAsync(principalB, ticket.Id, new("Attempt to claim assigned ticket", adminB.Id), default));
        Assert.Equal("assignment_denied", ex.Code);
    }

    [Fact]
    public async Task A5_4_FIX3_FundDossier_Only_Includes_Disputes_Linked_To_Case_Transaction()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var admin = await f.AddUserAsync("Investigator", role: SystemRole.Admin);
        var subject = await f.AddUserAsync("Customer");
        var session = await f.AddSessionAsync(admin.Id);

        var caseTxId = Guid.NewGuid();
        var otherTxId = Guid.NewGuid();

        var ticket = new SupportCase { TicketNumber = "TK-DOSSIER", LinkedUserId = subject.Id, AssignedAdminId = admin.Id, LinkedTransactionId = caseTxId, Status = CaseStatus.Investigating };

        var linkedDispute = new SpendWise.Domain.Admin.Dispute
        {
            Id = Guid.NewGuid(),
            UserId = subject.Id,
            LinkedTransactionId = caseTxId,
            Reason = "Linked dispute regarding case transaction",
            Status = CaseStatus.Open,
            CreatedAt = f.Clock.UtcNow
        };

        var unrelatedDispute = new SpendWise.Domain.Admin.Dispute
        {
            Id = Guid.NewGuid(),
            UserId = subject.Id,
            LinkedTransactionId = otherTxId,
            Reason = "Unrelated dispute for a completely different transaction",
            Status = CaseStatus.Resolved,
            CreatedAt = f.Clock.UtcNow
        };

        f.Db.AddRange(ticket, linkedDispute, unrelatedDispute);
        await f.Db.SaveChangesAsync();

        var service = CreateService(f);
        var principal = V1PermissionTests.Principal(admin.Id, session.Id, "Admin");

        var dossier = await service.FundDossierAsync(principal, ticket.Id, "Investigate dossier", default);

        Assert.NotEmpty(dossier.LinkedDisputes);
        Assert.Contains(dossier.LinkedDisputes, d => d.DisputeId == linkedDispute.Id);
        Assert.DoesNotContain(dossier.LinkedDisputes, d => d.DisputeId == unrelatedDispute.Id);
    }
}
