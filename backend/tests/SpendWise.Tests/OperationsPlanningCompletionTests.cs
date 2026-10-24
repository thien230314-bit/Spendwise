using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using SpendWise.Application.Admin;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Features;
using SpendWise.Application.Features.Authentication;
using SpendWise.Application.Features.Admin;
using SpendWise.Application.Features.Admin.Cases;
using SpendWise.Application.Features.Admin.Cases.Commands.CreateSupportCase;
using SpendWise.Application.Features.Admin.Cases.Commands.CreateDispute;
using SpendWise.Application.Features.Budgets;
using SpendWise.Application.Features.Budgets.Commands.CreateBudget;
using SpendWise.Application.Features.Dashboard;
using SpendWise.Application.Features.Goals.Commands;
using SpendWise.Application.Features.Goals.Commands.CreateGoal;
using SpendWise.Application.Features.Groups;
using SpendWise.Application.Features.Groups.Commands.CreateGroup;
using SpendWise.Application.Features.Notifications;
using SpendWise.Application.Features.Operations;
using SpendWise.Application.Groups;
using SpendWise.Application.Notifications;
using SpendWise.Application.Planning;
using SpendWise.Domain.Admin;
using SpendWise.Domain.Finance;
using SpendWise.Domain.Identity;
using SpendWise.Domain.Planning;
using SpendWise.Infrastructure.Finance;
using SpendWise.Infrastructure.Identity;
using SpendWise.Infrastructure.Notifications;
using SpendWise.API.Infrastructure;
using System.Security.Claims;
using Xunit;

namespace SpendWise.Tests;

// Các test này khóa các hợp đồng mới của giai đoạn hoàn thiện backend.
public sealed class OperationsPlanningCompletionTests
{
    [Fact]
    public async Task Group_creation_verifies_the_pin_on_the_mutation()
    {
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Group PIN owner");
        var handler = new CreateGroupCommandHandler(fixture.Db, fixture.Clock,
            new GroupAccess(fixture.Db), fixture.Security);

        await Assert.ThrowsAsync<AppException>(() => handler.Handle(new CreateGroupCommand(user.Id,
            new CreateGroupRequest("Secured", null, "Friends", SecurityPin: "654321")), default));
        Assert.Empty(fixture.Db.Groups);

        var created = await handler.Handle(new CreateGroupCommand(user.Id,
            new CreateGroupRequest("Secured", null, "Friends", SecurityPin: TargetedCorrectnessFixture.SecurityPin)), default);
        Assert.NotEqual(Guid.Empty, created.Id);
    }

    [Fact]
    public async Task Budget_requires_pin_enforces_minimum_and_round_trips_rollover()
    {
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Budget owner");
        var category = new Category { OwnerUserId = user.Id, Key = $"test:{Guid.NewGuid():N}", Name = "Food", Kind = CategoryKind.Expense };
        fixture.Db.Categories.Add(category);
        await fixture.Db.SaveChangesAsync();
        var handler = new CreateBudgetCommandHandler(fixture.Db, new BudgetReader(fixture.Db), fixture.Security);

        await Assert.ThrowsAsync<AppException>(() => handler.Handle(new CreateBudgetCommand(user.Id,
            new CreateBudgetRequest(category.Id, 2026, 9, 200_000, SecurityPin: "654321")), default));
        await Assert.ThrowsAsync<AppException>(() => handler.Handle(new CreateBudgetCommand(user.Id,
            new CreateBudgetRequest(category.Id, 2026, 9, 99_999, SecurityPin: TargetedCorrectnessFixture.SecurityPin)), default));

        var result = await handler.Handle(new CreateBudgetCommand(user.Id,
            new CreateBudgetRequest(category.Id, 2026, 9, 200_000, Rollover: true,
                SecurityPin: TargetedCorrectnessFixture.SecurityPin)), default);
        Assert.True(result.Rollover);
        Assert.True((await fixture.Db.Budgets.SingleAsync()).Rollover);
    }

    [Fact]
    public async Task Goal_initial_allocation_uses_goal_posting_and_returns_history()
    {
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Goal owner");
        var wallet = await fixture.AddWalletAsync(user.Id, "Main", true, balance: 1_000);
        var movement = new GoalMovementExecutor(fixture.Db, new LedgerService(fixture.Db, fixture.Clock),
            fixture.Clock, fixture.Security, new SpendWise.Application.Features.Banking.ActiveBankGuard(fixture.Db));
        var handler = new CreateGoalCommandHandler(fixture.Db, fixture.Security, movement, fixture.Clock);

        var goal = await handler.Handle(new CreateGoalCommand(user.Id,
            new CreateGoalRequest("Laptop", 2_000, IconName: "laptop", ColorHex: "#112233",
                InitialAmount: 300, WalletId: wallet.Id, IdempotencyKey: "goal-initial-1",
                SecurityPin: TargetedCorrectnessFixture.SecurityPin)), default);

        Assert.Equal(300, goal.CurrentAllocatedAmount);
        Assert.Equal("deposit", Assert.Single(goal.Movements).Type);
        Assert.Equal("Main", Assert.Single(goal.Movements).WalletName);
        Assert.Equal(700, (await fixture.Db.Wallets.FindAsync(wallet.Id))!.AvailableBalance);
        Assert.DoesNotContain(fixture.Db.FinancialTransactions, item => item.Type == FinancialTransactionType.Expense);
        Assert.Contains(fixture.Db.FinancialTransactions, item => item.Type == FinancialTransactionType.GoalContribution);
    }

    [Fact]
    public async Task Budget_alerts_are_persisted_once_per_threshold()
    {
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Alert owner");
        var wallet = await fixture.AddWalletAsync(user.Id, "Main", true, balance: 1_000_000);
        var category = new Category { OwnerUserId = user.Id, Key = $"test:{Guid.NewGuid():N}", Name = "Food", Kind = CategoryKind.Expense };
        fixture.Db.Categories.Add(category);
        fixture.Db.Budgets.Add(new Budget { OwnerUserId = user.Id, Category = category, CategoryId = category.Id,
            Year = fixture.Clock.UtcNow.Year, Month = fixture.Clock.UtcNow.Month, LimitAmount = 100_000, AlertsEnabled = true });
        await AddExpenseAsync(fixture, user.Id, wallet, category, 90_000, "warning");
        var evaluator = new BudgetAlertEvaluator(fixture.Db, new NotificationService(fixture.Db),
            fixture.Clock, new BudgetReader(fixture.Db));

        await evaluator.EvaluateAsync(user.Id, default);
        await evaluator.EvaluateAsync(user.Id, default);
        Assert.Single(await fixture.Db.Notifications.Where(item => item.Type == SpendWise.Domain.Notifications.NotificationType.BudgetWarning).ToListAsync());

        await AddExpenseAsync(fixture, user.Id, wallet, category, 10_000, "exceeded");
        await evaluator.EvaluateAsync(user.Id, default);
        Assert.Single(await fixture.Db.Notifications.Where(item => item.Type == SpendWise.Domain.Notifications.NotificationType.BudgetExceeded).ToListAsync());
    }

    [Fact]
    public async Task Maintenance_and_announcements_require_admin_pin_and_broadcast()
    {
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var admin = await fixture.AddUserAsync("Admin", role: SystemRole.SystemAdmin);
        _ = await fixture.AddUserAsync("Normal user");
        var realtime = new RecordingRealtime();
        var handler = new OperationsHandler(fixture.Db, fixture.Clock,
            new AdminSecurityGuard(fixture.Db, new SecurityPinService(),
                new AccountCredentialLockout(fixture.Db, fixture.Clock)), new AdminAuditWriter(fixture.Db),
            new NotificationService(fixture.Db), realtime);

        await Assert.ThrowsAsync<ForbiddenException>(() => handler.Handle(new ChangeMaintenanceStateCommand(admin.Id,
            new ChangeMaintenanceRequest(true, "654321", "planned maintenance")), default));
        var state = await handler.Handle(new ChangeMaintenanceStateCommand(admin.Id,
            new ChangeMaintenanceRequest(true, TargetedCorrectnessFixture.SecurityPin, "planned maintenance")), default);
        Assert.True(state.IsEnabled);

        var first = await handler.Handle(new PublishAnnouncementCommand(admin.Id,
            new PublishAnnouncementRequest("First", "First notice", TargetedCorrectnessFixture.SecurityPin, "urgent notice")), default);
        var second = await handler.Handle(new PublishAnnouncementCommand(admin.Id,
            new PublishAnnouncementRequest("Second", "Second notice", TargetedCorrectnessFixture.SecurityPin, "updated notice")), default);
        Assert.Equal(EmergencyAnnouncementStatus.Inactive, (await fixture.Db.EmergencyAnnouncements.FindAsync(first.Id))!.Status);
        Assert.Equal(EmergencyAnnouncementStatus.Active, (await fixture.Db.EmergencyAnnouncements.FindAsync(second.Id))!.Status);
        Assert.Contains("maintenanceChanged", realtime.Events);
        Assert.Contains("emergencyAnnouncement", realtime.Events);
    }

    [Fact]
    public async Task Dashboard_uses_authoritative_wallet_goal_and_monthly_transactions()
    {
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Dashboard user");
        var wallet = await fixture.AddWalletAsync(user.Id, "Main", true, balance: 1_000);
        var incomeCategory = new Category { OwnerUserId = user.Id, Key = $"test:{Guid.NewGuid():N}", Name = "Salary", Kind = CategoryKind.Income };
        var expenseCategory = new Category { OwnerUserId = user.Id, Key = $"test:{Guid.NewGuid():N}", Name = "Food", Kind = CategoryKind.Expense };
        fixture.Db.Categories.AddRange(incomeCategory, expenseCategory);
        await AddTransactionAsync(fixture, user.Id, wallet, incomeCategory, 500, FinancialTransactionType.Income, "income");
        await AddTransactionAsync(fixture, user.Id, wallet, expenseCategory, 200, FinancialTransactionType.Expense, "expense");
        var goalAccount = new LedgerAccount { OwnerUserId = user.Id, Code = "GOAL:TEST", Name = "Goal", Type = LedgerAccountType.GoalReserve };
        fixture.Db.Goals.Add(new Goal { OwnerUserId = user.Id, Name = "Goal", TargetAmount = 1_000,
            CurrentAllocatedAmount = 300, LedgerAccount = goalAccount, LedgerAccountId = goalAccount.Id });
        await fixture.Db.SaveChangesAsync();

        var result = await new GetDashboardQueryHandler(fixture.Db, fixture.Clock, new BudgetReader(fixture.Db))
            .Handle(new GetDashboardQuery(user.Id), default);
        Assert.Equal(1_000, result.TotalWalletsBalance);
        Assert.Equal(300, result.TotalGoalsReserved);
        Assert.Equal(500, result.IncomeThisMonth);
        Assert.Equal(200, result.ExpenseThisMonth);
        Assert.Equal(300, result.CashFlowDifference);
        Assert.Equal(6, result.SixMonthCashFlow.Count);
        Assert.Equal(0, result.OthersOweUser);
        Assert.Equal(0, result.UserOwesOthers);
    }

    [Fact]
    public void Case_attachment_validator_accepts_only_safe_supported_content()
    {
        var validator = new CaseAttachmentValidator();
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x01 };
        var files = validator.Create(Guid.NewGuid(), Guid.NewGuid(), [new CaseAttachmentUpload("proof.png", "image/png", png)]);
        Assert.Single(files);
        Assert.Throws<AppException>(() => validator.Create(Guid.NewGuid(), Guid.NewGuid(),
            [new CaseAttachmentUpload("run.exe", "application/octet-stream", [0x4D, 0x5A])]));
    }

    [Fact]
    public async Task Correction_grant_is_pin_protected_exactly_once_and_keeps_correction_count()
    {
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Support owner");
        var admin = await fixture.AddUserAsync("Support admin", role: SystemRole.SystemAdmin);
        var wallet = await fixture.AddWalletAsync(user.Id, "Main", true, balance: 1_000);
        var category = new Category { OwnerUserId = user.Id, Key = $"test:{Guid.NewGuid():N}", Name = "Food", Kind = CategoryKind.Expense };
        fixture.Db.Categories.Add(category);
        await AddTransactionAsync(fixture, user.Id, wallet, category, 100, FinancialTransactionType.Expense, "manual");
        var transaction = await fixture.Db.FinancialTransactions.SingleAsync();
        transaction.IsManual = true;
        transaction.CorrectionAllowance = 1;
        transaction.CorrectionCount = 1;
        await fixture.Db.SaveChangesAsync();
        var created = await new CreateSupportCaseCommandHandler(
            fixture.Db,
            fixture.Clock,
            SupportEmailTestFactory.Create(fixture.Db)).Handle(
            new CreateSupportCaseCommand(user.Id, new CreateSupportCaseRequest("transaction_edit", "Normal",
                transaction.Id, "Fix transaction", "I need one more correction.")), default);
        var workflow = new CaseWorkflowHandler(fixture.Db, new CaseAttachmentValidator(), fixture.Clock,
            new AdminSecurityGuard(fixture.Db, new SecurityPinService(),
                new AccountCredentialLockout(fixture.Db, fixture.Clock)), new AdminAuditWriter(fixture.Db),
            new NotificationService(fixture.Db), SupportEmailTestFactory.Create(fixture.Db));

        await Assert.ThrowsAsync<ForbiddenException>(() => workflow.Handle(new GrantSupportCorrectionCommand(admin.Id,
            created.Id, new CorrectionGrantRequest("654321", "verified request")), default));
        await workflow.Handle(new GrantSupportCorrectionCommand(admin.Id, created.Id,
            new CorrectionGrantRequest(TargetedCorrectnessFixture.SecurityPin, "verified request")), default);
        await Assert.ThrowsAsync<ConflictException>(() => workflow.Handle(new GrantSupportCorrectionCommand(admin.Id,
            created.Id, new CorrectionGrantRequest(TargetedCorrectnessFixture.SecurityPin, "duplicate request")), default));

        var stored = await fixture.Db.FinancialTransactions.FindAsync(transaction.Id);
        Assert.Equal(2, stored!.CorrectionAllowance);
        Assert.Equal(1, stored.CorrectionCount);
        Assert.True((await fixture.Db.SupportCases.FindAsync(created.Id))!.CorrectionGranted);
    }

    [Fact]
    public async Task Dispute_history_and_resolution_do_not_change_wallet_balance()
    {
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Dispute owner");
        var admin = await fixture.AddUserAsync("Dispute admin", role: SystemRole.SystemAdmin);
        var wallet = await fixture.AddWalletAsync(user.Id, "Main", true, balance: 750);
        var category = new Category { OwnerUserId = user.Id, Key = $"test:{Guid.NewGuid():N}", Name = "Shop", Kind = CategoryKind.Expense };
        fixture.Db.Categories.Add(category);
        await AddTransactionAsync(fixture, user.Id, wallet, category, 250, FinancialTransactionType.Expense, "disputed");
        var transaction = await fixture.Db.FinancialTransactions.SingleAsync();
        var created = await new CreateDisputeCommandHandler(fixture.Db).Handle(new CreateDisputeCommand(user.Id,
            new CreateDisputeRequest(transaction.Id, "incorrect_amount", "The amount is incorrect.")), default);
        var workflow = new CaseWorkflowHandler(fixture.Db, new CaseAttachmentValidator(), fixture.Clock,
            new AdminSecurityGuard(fixture.Db, new SecurityPinService(),
                new AccountCredentialLockout(fixture.Db, fixture.Clock)), new AdminAuditWriter(fixture.Db),
            new NotificationService(fixture.Db), SupportEmailTestFactory.Create(fixture.Db));

        var resolved = await workflow.Handle(new UpdateDisputeStatusCommand(admin.Id, created.Id,
            new CaseStatusRequest("resolved", "Reviewed and resolved.")), default);
        Assert.Equal("resolved", resolved.Status);
        Assert.True(resolved.History.Count >= 2);
        Assert.Equal(750, (await fixture.Db.Wallets.FindAsync(wallet.Id))!.AvailableBalance);
        Assert.Equal(250, (await fixture.Db.FinancialTransactions.FindAsync(transaction.Id))!.Amount);
    }

    [Fact]
    public async Task Business_access_blocks_users_during_maintenance_but_exempts_admin_and_sepay()
    {
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        fixture.Db.SystemMaintenanceStates.Add(new SystemMaintenanceState
        {
            IsEnabled = true,
            ChangedAt = fixture.Clock.UtcNow,
            ChangedByAdminId = Guid.NewGuid()
        });
        await fixture.Db.SaveChangesAsync();
        var reachedNext = false;
        var middleware = new BusinessAccessMiddleware(_ => { reachedNext = true; return Task.CompletedTask; });

        var userContext = Context("User", "/api/wallets");
        var blocked = await Assert.ThrowsAsync<AppException>(() => middleware.InvokeAsync(userContext, fixture.Db));
        Assert.Equal("MAINTENANCE_MODE", blocked.Code);
        Assert.Equal(503, blocked.StatusCode);

        await middleware.InvokeAsync(Context("SystemAdmin", "/api/admin/operations/maintenance"), fixture.Db);
        await middleware.InvokeAsync(Context("User", "/api/integrations/sepay/bankhub/ipn"), fixture.Db);
        Assert.True(reachedNext);
    }

    [Fact]
    public async Task System_admin_cannot_enter_ordinary_user_financial_api()
    {
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var middleware = new BusinessAccessMiddleware(_ => Task.CompletedTask);
        var error = await Assert.ThrowsAsync<ForbiddenException>(() =>
            middleware.InvokeAsync(Context("SystemAdmin", "/api/wallets"), fixture.Db));
        Assert.Equal("user_role_required", error.Code);
    }

    private static Task AddExpenseAsync(TargetedCorrectnessFixture fixture, Guid userId, Wallet wallet,
        Category category, decimal amount, string reference) =>
        AddTransactionAsync(fixture, userId, wallet, category, amount, FinancialTransactionType.Expense, reference);

    private static async Task AddTransactionAsync(TargetedCorrectnessFixture fixture, Guid userId, Wallet wallet,
        Category category, decimal amount, FinancialTransactionType type, string reference)
    {
        var ledger = new LedgerTransaction { Reference = reference, BusinessType = "Test", BusinessId = Guid.NewGuid(),
            Status = LedgerTransactionStatus.Completed, CompletedAt = fixture.Clock.UtcNow };
        fixture.Db.FinancialTransactions.Add(new FinancialTransaction { OwnerUserId = userId, Wallet = wallet,
            WalletId = wallet.Id, Category = category, CategoryId = category.Id, LedgerTransaction = ledger,
            LedgerTransactionId = ledger.Id, Type = type, Amount = amount, Description = reference,
            OccurredAt = fixture.Clock.UtcNow });
        await fixture.Db.SaveChangesAsync();
    }

    private sealed class RecordingRealtime : IRealtimeNotifier
    {
        public List<string> Events { get; } = [];
        public Task SendToUserAsync(Guid userId, string eventName, object payload, CancellationToken cancellationToken)
        {
            Events.Add(eventName);
            return Task.CompletedTask;
        }
        public Task SendAllAsync(string eventName, object payload, CancellationToken cancellationToken)
        {
            Events.Add(eventName);
            return Task.CompletedTask;
        }
    }

    private static DefaultHttpContext Context(string role, string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()), new Claim(ClaimTypes.Role, role)],
            "Test"));
        return context;
    }
}
