using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SpendWise.API.Controllers;
using SpendWise.Application.Admin;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Admin;
using SpendWise.Application.Features.Admin.Cases;
using SpendWise.Application.Features.Admin.Cases.Commands.CreateSupportCase;
using SpendWise.Application.Features.Admin.Cases.Commands.UpdateAdminCase;
using SpendWise.Application.Features.Authentication;
using SpendWise.Application.Identity;
using SpendWise.Domain.Admin;
using SpendWise.Domain.Identity;
using SpendWise.Domain.Notifications;
using SpendWise.Infrastructure.Identity;
using SpendWise.Infrastructure.Notifications;
using SpendWise.Infrastructure.Persistence;
using Xunit;

namespace SpendWise.Tests;

// Các test này khóa hành vi email E03/E04 mà không gửi email thật ra ngoài.
public sealed class SupportEmailEventsTests
{
    [Fact]
    public async Task Support_list_maps_multiple_cases_sequentially_in_existing_order()
    {
        var clock = new AdvancingClock(new DateTime(2026, 9, 26, 1, 0, 0, DateTimeKind.Utc));
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync(clock);
        var owner = await fixture.AddUserAsync("Support list owner");
        var older = new SupportCase
        {
            TicketNumber = "SW-OLDER",
            LinkedUserId = owner.Id,
            Category = "general",
            Subject = "Older support case",
            Description = "Older description",
            Priority = "Normal"
        };
        fixture.Db.SupportCases.Add(older);
        fixture.Db.CaseHistories.Add(History(CaseKind.Support, older.Id, owner.Id, "Older reply"));
        await fixture.Db.SaveChangesAsync();

        clock.Advance(TimeSpan.FromMinutes(1));
        var newer = new SupportCase
        {
            TicketNumber = "SW-NEWER",
            LinkedUserId = owner.Id,
            Category = "account",
            Subject = "Newer support case",
            Description = "Newer description",
            Priority = "High"
        };
        fixture.Db.SupportCases.Add(newer);
        fixture.Db.CaseHistories.Add(History(CaseKind.Support, newer.Id, owner.Id, "Newer reply"));
        await fixture.Db.SaveChangesAsync();

        var result = await Workflow(fixture).Handle(new GetSupportCasesQuery(owner.Id), default);

        Assert.Equal(2, result.Count);
        Assert.Equal(newer.Id, result[0].Id);
        Assert.Equal("Newer support case", result[0].Subject);
        Assert.Equal("Newer reply", Assert.Single(result[0].History).Message);
        Assert.Equal(older.Id, result[1].Id);
        Assert.Equal("Older reply", Assert.Single(result[1].History).Message);
    }

    [Fact]
    public async Task Dispute_list_maps_multiple_cases_sequentially_in_existing_order()
    {
        var clock = new AdvancingClock(new DateTime(2026, 9, 26, 2, 0, 0, DateTimeKind.Utc));
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync(clock);
        var owner = await fixture.AddUserAsync("Dispute list owner");
        var older = new Dispute
        {
            UserId = owner.Id,
            LinkedTransactionId = Guid.NewGuid(),
            Reason = "older_reason",
            Description = "Older dispute",
            TransactionReferenceSnapshot = "TX-OLDER",
            TransactionTitleSnapshot = "Older transaction",
            TransactionAmountSnapshot = 100,
            TransactionOccurredAtSnapshot = clock.UtcNow
        };
        fixture.Db.Disputes.Add(older);
        fixture.Db.CaseHistories.Add(History(CaseKind.Dispute, older.Id, owner.Id, "Older dispute reply"));
        await fixture.Db.SaveChangesAsync();

        clock.Advance(TimeSpan.FromMinutes(1));
        var newer = new Dispute
        {
            UserId = owner.Id,
            LinkedTransactionId = Guid.NewGuid(),
            Reason = "newer_reason",
            Description = "Newer dispute",
            TransactionReferenceSnapshot = "TX-NEWER",
            TransactionTitleSnapshot = "Newer transaction",
            TransactionAmountSnapshot = 200,
            TransactionOccurredAtSnapshot = clock.UtcNow
        };
        fixture.Db.Disputes.Add(newer);
        fixture.Db.CaseHistories.Add(History(CaseKind.Dispute, newer.Id, owner.Id, "Newer dispute reply"));
        await fixture.Db.SaveChangesAsync();

        var result = await Workflow(fixture).Handle(new GetDisputesQuery(owner.Id), default);

        Assert.Equal(2, result.Count);
        Assert.Equal(newer.Id, result[0].Id);
        Assert.Equal("TX-NEWER", result[0].Reference);
        Assert.Equal("Newer dispute reply", Assert.Single(result[0].History).Message);
        Assert.Equal(older.Id, result[1].Id);
        Assert.Equal("Older dispute reply", Assert.Single(result[1].History).Message);
    }

    [Fact]
    public async Task E03_queues_receipt_only_for_verified_ticket_owner_with_safe_content()
    {
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync(new FixedClock(new DateTime(2026, 9, 25, 3, 15, 0, DateTimeKind.Utc)));
        var owner = await fixture.AddUserAsync("Ticket owner");
        _ = await fixture.AddUserAsync("Another user");

        var created = await CreateHandler(fixture).Handle(
            new CreateSupportCaseCommand(owner.Id, new CreateSupportCaseRequest(
                "account", "Normal", null, "Need <account> help", "Description with bank evidence omitted.")),
            default);

        var email = await fixture.Db.EmailOutboxMessages.SingleAsync();
        var protector = new TestEmailContentProtector();
        var html = protector.Unprotect(email.HtmlBody);
        var text = protector.Unprotect(email.TextBody);
        Assert.Equal(owner.Email, email.RecipientEmail);
        Assert.Equal($"support-received:{created.Id:N}", email.DeduplicationKey);
        var storedCase = await fixture.Db.SupportCases.SingleAsync(x => x.Id == created.Id);
        Assert.Contains(storedCase.TicketNumber, html);
        Assert.Contains("Need &lt;account&gt; help", html);
        Assert.DoesNotContain("Description with bank evidence omitted.", html);
        Assert.Contains("25/09/2026 03:15 UTC", text);
    }

    [Fact]
    public async Task E03_unverified_owner_keeps_support_case_and_safely_skips_email()
    {
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await fixture.AddUserAsync("Unverified owner");
        owner.EmailVerifiedAt = null;
        await fixture.Db.SaveChangesAsync();

        var created = await CreateHandler(fixture).Handle(
            new CreateSupportCaseCommand(owner.Id, new CreateSupportCaseRequest(
                "general", "Normal", null, "Need help", "Please review this request.")),
            default);

        Assert.NotNull(await fixture.Db.SupportCases.FindAsync(created.Id));
        Assert.Single(await fixture.Db.CaseHistories.Where(x => x.CaseId == created.Id).ToListAsync());
        Assert.Empty(await fixture.Db.EmailOutboxMessages.ToListAsync());
    }

    [Fact]
    public async Task E04a_resolved_transition_persists_actual_message_and_sends_once()
    {
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await fixture.AddUserAsync("Resolved owner");
        var admin = await fixture.AddUserAsync("Support admin", role: SystemRole.SystemAdmin);
        var created = await CreateCaseAsync(fixture, owner.Id);
        var workflow = Workflow(fixture);

        // Cập nhật trung gian không được tạo email E04.
        await workflow.Handle(new UpdateSupportStatusCommand(admin.Id, created.Id,
            new CaseStatusRequest("open", "Admin started reviewing the request.")), default);
        var resolution = "Đã sửa cấu hình hiển thị <an toàn>; không có khoản tiền nào được hoàn.";
        var result = await workflow.Handle(new UpdateSupportStatusCommand(admin.Id, created.Id,
            new CaseStatusRequest("resolved", resolution)), default);

        Assert.Equal("resolved", result.Status);
        Assert.Contains(result.History, x => x.NewStatus == "resolved" && x.Message == resolution);
        Assert.Single(await fixture.Db.EmailOutboxMessages.Where(x => x.DeduplicationKey == $"support-resolved:{created.Id:N}").ToListAsync());
        Assert.Equal(2, await fixture.Db.AuditLogs.CountAsync(x => x.TargetId == created.Id.ToString()));
        Assert.Equal(2, await fixture.Db.Notifications.CountAsync(x => x.UserId == owner.Id));
        var email = await fixture.Db.EmailOutboxMessages.SingleAsync(x => x.DeduplicationKey == $"support-resolved:{created.Id:N}");
        Assert.Equal(owner.Email, email.RecipientEmail);
        var html = new TestEmailContentProtector().Unprotect(email.HtmlBody);
        Assert.Contains("&lt;", html);
        Assert.Contains("&gt;", html);
        Assert.DoesNotContain("<an toàn>", html);

        await Assert.ThrowsAsync<ConflictException>(() => workflow.Handle(
            new UpdateSupportStatusCommand(admin.Id, created.Id,
                new CaseStatusRequest("resolved", resolution)), default));
        Assert.Single(await fixture.Db.EmailOutboxMessages.Where(x => x.DeduplicationKey == $"support-resolved:{created.Id:N}").ToListAsync());
    }

    [Fact]
    public async Task E04b_legacy_admin_workflow_rejects_with_history_audit_notification_and_email()
    {
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await fixture.AddUserAsync("Rejected owner");
        var admin = await fixture.AddUserAsync("Legacy admin", role: SystemRole.SystemAdmin);
        var created = await CreateCaseAsync(fixture, owner.Id);
        var reason = "Không đủ thông tin để xác minh yêu cầu.";
        var handler = new UpdateAdminCaseCommandHandler(
            fixture.Db,
            new AdminAuditWriter(fixture.Db),
            fixture.Clock,
            SupportEmailTestFactory.Create(fixture.Db),
            new NotificationService(fixture.Db));

        var result = await handler.Handle(new UpdateAdminCaseCommand(
            admin.Id,
            "support",
            created.Id,
            new UpdateCaseRequest("Rejected", "Internal note must not be emailed", reason)), default);

        Assert.Equal("Rejected", result.Status);
        var history = await fixture.Db.CaseHistories.SingleAsync(x => x.CaseId == created.Id && x.NewStatus == "rejected");
        Assert.Equal(reason, history.Message);
        Assert.Equal(admin.Id, history.ActorId);
        Assert.Single(await fixture.Db.AuditLogs.Where(x => x.TargetId == created.Id.ToString()).ToListAsync());
        Assert.Single(await fixture.Db.Notifications.Where(x => x.UserId == owner.Id).ToListAsync());
        var email = await fixture.Db.EmailOutboxMessages.SingleAsync(x => x.DeduplicationKey == $"support-rejected:{created.Id:N}");
        Assert.Equal(owner.Email, email.RecipientEmail);
        var protector = new TestEmailContentProtector();
        var html = protector.Unprotect(email.HtmlBody);
        var text = protector.Unprotect(email.TextBody);
        Assert.Contains(reason, text);
        Assert.DoesNotContain("Internal note must not be emailed", html);
        Assert.DoesNotContain("Internal note must not be emailed", text);
    }

    [Fact]
    public async Task Terminal_support_transitions_require_concrete_messages_before_any_change()
    {
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await fixture.AddUserAsync("Message owner");
        var admin = await fixture.AddUserAsync("Message admin", role: SystemRole.SystemAdmin);
        var first = await CreateCaseAsync(fixture, owner.Id);
        var second = await CreateCaseAsync(fixture, owner.Id);
        var workflow = Workflow(fixture);

        var resolution = await Assert.ThrowsAsync<AppException>(() => workflow.Handle(
            new UpdateSupportStatusCommand(admin.Id, first.Id, new CaseStatusRequest("resolved", "x")), default));
        var rejection = await Assert.ThrowsAsync<AppException>(() => workflow.Handle(
            new UpdateSupportStatusCommand(admin.Id, second.Id, new CaseStatusRequest("rejected", " ")), default));

        Assert.Equal("resolution_message_required", resolution.Code);
        Assert.Equal("invalid_input", rejection.Code);
        Assert.Equal(CaseStatus.New, (await fixture.Db.SupportCases.FindAsync(first.Id))!.Status);
        Assert.Equal(CaseStatus.New, (await fixture.Db.SupportCases.FindAsync(second.Id))!.Status);
        Assert.Empty(await fixture.Db.EmailOutboxMessages.Where(x => x.DeduplicationKey.StartsWith("support-resolved:") || x.DeduplicationKey.StartsWith("support-rejected:")).ToListAsync());
    }

    [Fact]
    public async Task E04_unverified_owner_keeps_successful_transition_and_skips_email()
    {
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await fixture.AddUserAsync("Unverified decision owner");
        owner.EmailVerifiedAt = null;
        await fixture.Db.SaveChangesAsync();
        var admin = await fixture.AddUserAsync("Decision admin", role: SystemRole.SystemAdmin);
        var created = await CreateCaseAsync(fixture, owner.Id);

        var result = await Workflow(fixture).Handle(
            new UpdateSupportStatusCommand(admin.Id, created.Id,
                new CaseStatusRequest("resolved", "Đã xử lý yêu cầu thành công.")), default);

        Assert.Equal("resolved", result.Status);
        Assert.Contains(result.History, x => x.NewStatus == "resolved");
        Assert.Empty(await fixture.Db.EmailOutboxMessages.ToListAsync());
        Assert.Single(await fixture.Db.AuditLogs.Where(x => x.TargetId == created.Id.ToString()).ToListAsync());
        Assert.Single(await fixture.Db.Notifications.Where(x => x.UserId == owner.Id).ToListAsync());
    }

    [Fact]
    public async Task Support_routes_keep_user_ownership_and_system_admin_authorization_boundaries()
    {
        var userAuthorize = Assert.Single(typeof(CasesController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>());
        var adminAuthorize = Assert.Single(typeof(AdminCasesV2Controller).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>());
        Assert.Equal("User", userAuthorize.Roles);
        Assert.Equal("SystemAdmin", adminAuthorize.Policy);

        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await fixture.AddUserAsync("Owner");
        var stranger = await fixture.AddUserAsync("Stranger");
        var created = await CreateCaseAsync(fixture, owner.Id);
        await Assert.ThrowsAsync<NotFoundException>(() => Workflow(fixture).Handle(
            new GetSupportCaseQuery(stranger.Id, created.Id), default));
    }

    [Fact]
    public async Task E03_outbox_failure_rolls_back_case_and_history()
    {
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await fixture.AddUserAsync("Rollback owner");
        await fixture.Db.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER fail_support_email_outbox
            BEFORE INSERT ON EmailOutboxMessages
            WHEN NEW.DeduplicationKey LIKE 'support-received:%'
            BEGIN
                SELECT RAISE(ABORT, 'forced support email outbox failure');
            END;
            """);

        await Assert.ThrowsAnyAsync<Exception>(() => CreateHandler(fixture).Handle(
            new CreateSupportCaseCommand(owner.Id, new CreateSupportCaseRequest(
                "general", "Normal", null, "Rollback request", "This transaction must rollback.")), default));

        Assert.Empty(await fixture.Db.SupportCases.ToListAsync());
        Assert.Empty(await fixture.Db.CaseHistories.ToListAsync());
        Assert.Empty(await fixture.Db.EmailOutboxMessages.ToListAsync());
    }

    [Fact]
    public async Task E04_outbox_failure_rolls_back_transition_history_audit_and_notification()
    {
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await fixture.AddUserAsync("Transition rollback owner");
        var admin = await fixture.AddUserAsync("Transition rollback admin", role: SystemRole.SystemAdmin);
        var created = await CreateCaseAsync(fixture, owner.Id);
        await fixture.Db.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER fail_support_resolution_outbox
            BEFORE INSERT ON EmailOutboxMessages
            WHEN NEW.DeduplicationKey LIKE 'support-resolved:%'
            BEGIN
                SELECT RAISE(ABORT, 'forced support resolution outbox failure');
            END;
            """);

        await Assert.ThrowsAnyAsync<Exception>(() => Workflow(fixture).Handle(
            new UpdateSupportStatusCommand(admin.Id, created.Id,
                new CaseStatusRequest("resolved", "Resolution must rollback together.")), default));

        var stored = await fixture.Db.SupportCases.AsNoTracking().SingleAsync(x => x.Id == created.Id);
        Assert.Equal(CaseStatus.New, stored.Status);
        Assert.DoesNotContain(await fixture.Db.CaseHistories.Where(x => x.CaseId == created.Id).ToListAsync(), x => x.NewStatus == "resolved");
        Assert.Empty(await fixture.Db.AuditLogs.Where(x => x.TargetId == created.Id.ToString()).ToListAsync());
        Assert.Empty(await fixture.Db.Notifications.Where(x => x.UserId == owner.Id).ToListAsync());
        Assert.Empty(await fixture.Db.EmailOutboxMessages.Where(x => x.DeduplicationKey.StartsWith("support-resolved:")).ToListAsync());
    }

    [Fact]
    public async Task Email_delivery_failure_does_not_reverse_committed_support_case()
    {
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await fixture.AddUserAsync("Delivery owner");
        var created = await CreateCaseAsync(fixture, owner.Id);
        var dispatcher = new EmailOutboxDispatcher(
            fixture.Db,
            new FailingSender(),
            new TestEmailContentProtector(),
            fixture.Clock,
            NullLogger<EmailOutboxDispatcher>.Instance);

        await dispatcher.DispatchPendingAsync(default);

        Assert.NotNull(await fixture.Db.SupportCases.FindAsync(created.Id));
        var email = await fixture.Db.EmailOutboxMessages.SingleAsync();
        Assert.Equal(EmailDeliveryStatus.Pending, email.Status);
        Assert.Equal(1, email.AttemptCount);
        Assert.Equal("delivery_failed", email.LastError);
    }

    [Fact]
    public async Task Concurrent_terminal_requests_commit_one_decision_and_one_email()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"spendwise-support-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<SpendWiseDbContext>()
            .UseSqlite($"Data Source={databasePath};Default Timeout=30")
            .Options;
        var clock = new FixedClock(new DateTime(2026, 9, 25, 6, 0, 0, DateTimeKind.Utc));
        Guid caseId;
        Guid ownerId;
        Guid adminId;
        try
        {
            await using (var seed = new SpendWiseDbContext(options, clock))
            {
                await seed.Database.EnsureCreatedAsync();
                var owner = User("Concurrent owner", SystemRole.User);
                var admin = User("Concurrent admin", SystemRole.SystemAdmin);
                var item = new SupportCase { TicketNumber = "SW-CONCURRENT", LinkedUserId = owner.Id, Category = "general", Priority = "Normal", Subject = "Concurrent", Description = "Test" };
                seed.AddRange(owner, admin, item);
                await seed.SaveChangesAsync();
                caseId = item.Id;
                ownerId = owner.Id;
                adminId = admin.Id;
            }

            await using var firstDb = new SpendWiseDbContext(options, clock);
            await using var secondDb = new SpendWiseDbContext(options, clock);
            var first = Workflow(firstDb, clock);
            var second = Workflow(secondDb, clock);
            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            async Task<Exception?> Run(CaseWorkflowHandler handler, string status)
            {
                await start.Task;
                try
                {
                    await handler.Handle(new UpdateSupportStatusCommand(adminId, caseId,
                        new CaseStatusRequest(status, $"Concrete {status} decision.")), default);
                    return null;
                }
                catch (Exception exception)
                {
                    return exception;
                }
            }

            var attempts = new[] { Run(first, "resolved"), Run(second, "rejected") };
            start.SetResult();
            var results = await Task.WhenAll(attempts);
            Assert.Single(results, x => x is null);
            Assert.Single(results.OfType<ConflictException>());

            await using var verify = new SpendWiseDbContext(options, clock);
            Assert.Single(await verify.CaseHistories.Where(x => x.CaseId == caseId).ToListAsync());
            Assert.Single(await verify.EmailOutboxMessages.Where(x =>
                x.DeduplicationKey == $"support-resolved:{caseId:N}" ||
                x.DeduplicationKey == $"support-rejected:{caseId:N}").ToListAsync());
            Assert.Single(await verify.AuditLogs.Where(x => x.TargetId == caseId.ToString()).ToListAsync());
            Assert.Single(await verify.Notifications.Where(x => x.UserId == ownerId).ToListAsync());
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath)) File.Delete(databasePath);
        }
    }

    private static CreateSupportCaseCommandHandler CreateHandler(TargetedCorrectnessFixture fixture) =>
        new(fixture.Db, fixture.Clock, SupportEmailTestFactory.Create(fixture.Db));

    private static async Task<CaseDto> CreateCaseAsync(TargetedCorrectnessFixture fixture, Guid ownerId) =>
        await CreateHandler(fixture).Handle(
            new CreateSupportCaseCommand(ownerId, new CreateSupportCaseRequest(
                "general", "Normal", null, "Support request", "Please review this support request.")),
            default);

    private static CaseHistory History(CaseKind kind, Guid caseId, Guid actorId, string message) =>
        new()
        {
            CaseKind = kind,
            CaseId = caseId,
            ActorId = actorId,
            ActorDisplayNameSnapshot = "Case owner",
            ActorRole = "User",
            Action = "reply",
            Message = message
        };

    private static CaseWorkflowHandler Workflow(TargetedCorrectnessFixture fixture) =>
        Workflow(fixture.Db, fixture.Clock);

    private static CaseWorkflowHandler Workflow(SpendWiseDbContext db, IClock clock) =>
        new(
            db,
            new CaseAttachmentValidator(),
            clock,
            new AdminSecurityGuard(db, new SecurityPinService(), new AccountCredentialLockout(db, clock)),
            new AdminAuditWriter(db),
            new NotificationService(db),
            SupportEmailTestFactory.Create(db));

    private static User User(string name, SystemRole role)
    {
        var unique = Guid.NewGuid().ToString("N");
        return new User
        {
            FullName = name,
            Email = $"{unique}@test.local",
            NormalizedEmail = $"{unique}@test.local".ToUpperInvariant(),
            SpendId = unique,
            NormalizedSpendId = unique.ToUpperInvariant(),
            PasswordHash = "test-hash",
            EmailVerifiedAt = DateTime.UnixEpoch,
            SystemRole = role
        };
    }

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow => utcNow;
    }

    private sealed class AdvancingClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; private set; } = utcNow;
        public void Advance(TimeSpan duration) => UtcNow = UtcNow.Add(duration);
    }

    private sealed class FailingSender : ITransactionalEmailSender
    {
        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("provider unavailable");
    }
}
