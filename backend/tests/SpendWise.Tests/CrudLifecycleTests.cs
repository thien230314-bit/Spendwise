using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Features.AiAssistant.Commands.UpdateAiConversation;
using SpendWise.Application.Features.AiAssistant.Queries.GetAiConversationById;
using SpendWise.Application.Features.AiAssistant.Queries.GetAiConversations;
using SpendWise.Application.Features.Budgets;
using SpendWise.Application.Features.Budgets.Commands.CreateBudget;
using SpendWise.Application.Features.Budgets.Commands.DeleteBudget;
using SpendWise.Application.Features.Budgets.Commands.UpdateBudget;
using SpendWise.Application.Features.Budgets.Queries.GetBudgetById;
using SpendWise.Application.Features.Categories.Commands.ArchiveCategory;
using SpendWise.Application.Features.Categories.Commands.UpdateCategory;
using SpendWise.Application.Features.Goals.Commands.CreateGoal;
using SpendWise.Application.Features.Goals.Commands;
using SpendWise.Application.Features;
using SpendWise.Application.Features.Authentication;
using SpendWise.Application.Features.Goals.Commands.DeleteGoal;
using SpendWise.Application.Features.Goals.Commands.UpdateGoal;
using SpendWise.Application.Features.Goals.Queries.GetGoalById;
using SpendWise.Application.Features.RecurringTransactions.Commands.DeleteRecurringTransaction;
using SpendWise.Application.Features.RecurringTransactions.Commands.UpdateRecurringTransaction;
using SpendWise.Application.Features.RecurringTransactions.Queries.GetRecurringTransactionById;
using SpendWise.Application.Features.TransactionRules.Commands.DeleteTransactionRule;
using SpendWise.Application.Features.TransactionRules.Commands.UpdateTransactionRule;
using SpendWise.Application.Features.TransactionRules.Queries.GetTransactionRuleById;
using SpendWise.Application.Features.Wallets;
using SpendWise.Application.Features.Wallets.Commands.CloseWallet;
using SpendWise.Application.Features.Wallets.Commands.CreateWallet;
using SpendWise.Application.Features.Wallets.Commands.UpdateWallet;
using SpendWise.Application.AI;
using SpendWise.Application.Finance;
using SpendWise.Application.Planning;
using SpendWise.Domain.AI;
using SpendWise.Domain.Finance;
using SpendWise.Domain.Identity;
using SpendWise.Domain.Planning;
using SpendWise.Domain.Groups;
using SpendWise.Infrastructure.Persistence;
using SpendWise.Infrastructure.Services;
using SpendWise.Infrastructure.Finance;
using SpendWise.Infrastructure.Identity;
using Xunit;

namespace SpendWise.Tests;

// Các test này bảo vệ CRUD cấu hình và các quy tắc giữ lịch sử tài chính.
public sealed class CrudLifecycleTests
{
    [Fact]
    public async Task Budget_create_adds_one_owned_configuration()
    {
        // Arrange: tạo người dùng và danh mục chi tiêu hợp lệ.
        await using var fixture = await CrudFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Budget owner");
        var category = await fixture.AddCategoryAsync(user.Id, CategoryKind.Expense);
        var handler = new CreateBudgetCommandHandler(fixture.Db, new BudgetReader(fixture.Db), fixture.Security);

        // Act: tạo ngân sách tháng bằng Command chuẩn Create.
        var result = await handler.Handle(
            new CreateBudgetCommand(user.Id, new CreateBudgetRequest(category.Id, 2026, 9, 2_000_000, SecurityPin: CrudFixture.Pin)),
            default);

        // Assert: ngân sách được lưu đúng chủ sở hữu và hạn mức.
        var stored = await fixture.Db.Budgets.SingleAsync();
        Assert.Equal(result.Id, stored.Id);
        Assert.Equal(user.Id, stored.OwnerUserId);
        Assert.Equal(2_000_000, stored.LimitAmount);
    }

    [Fact]
    public async Task Budget_update_changes_configuration_only()
    {
        // Arrange: tạo ngân sách hiện có và giữ khóa danh mục/tháng ban đầu.
        await using var fixture = await CrudFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Budget owner");
        var category = await fixture.AddCategoryAsync(user.Id, CategoryKind.Expense);
        var budget = await fixture.AddBudgetAsync(user.Id, category, 1_000_000);
        var handler = new UpdateBudgetCommandHandler(fixture.Db, new BudgetReader(fixture.Db), fixture.Security);

        // Act: sửa hạn mức và tắt cảnh báo.
        await handler.Handle(
            new UpdateBudgetCommand(user.Id, budget.Id, new UpdateBudgetRequest(1_500_000, false, SecurityPin: CrudFixture.Pin)),
            default);

        // Assert: chỉ cấu hình thay đổi, khóa danh mục/tháng vẫn giữ nguyên.
        Assert.Equal(1_500_000, budget.LimitAmount);
        Assert.False(budget.AlertsEnabled);
        Assert.Equal(category.Id, budget.CategoryId);
        Assert.Equal(9, budget.Month);
    }

    [Fact]
    public async Task Budget_delete_removes_configuration_only()
    {
        // Arrange: tạo ngân sách không chứa dữ liệu tài chính bên trong.
        await using var fixture = await CrudFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Budget owner");
        var category = await fixture.AddCategoryAsync(user.Id, CategoryKind.Expense);
        var budget = await fixture.AddBudgetAsync(user.Id, category, 1_000_000);

        // Act: xóa cấu hình ngân sách.
        await new DeleteBudgetCommandHandler(fixture.Db, fixture.Security)
            .Handle(new DeleteBudgetCommand(user.Id, budget.Id, CrudFixture.Pin), default);

        // Assert: ngân sách bị xóa nhưng danh mục vẫn còn.
        Assert.Empty(await fixture.Db.Budgets.ToListAsync());
        Assert.NotNull(await fixture.Db.Categories.FindAsync(category.Id));
    }

    [Fact]
    public async Task Budget_update_rejects_another_user()
    {
        // Arrange: ngân sách thuộc người thứ nhất, Command dùng người thứ hai.
        await using var fixture = await CrudFixture.CreateAsync();
        var owner = await fixture.AddUserAsync("Owner");
        var stranger = await fixture.AddUserAsync("Stranger");
        var category = await fixture.AddCategoryAsync(owner.Id, CategoryKind.Expense);
        var budget = await fixture.AddBudgetAsync(owner.Id, category, 1_000_000);

        // Act: thử sửa ngân sách không thuộc quyền sở hữu.
        var action = () => new UpdateBudgetCommandHandler(fixture.Db, new BudgetReader(fixture.Db), fixture.Security)
            .Handle(new UpdateBudgetCommand(stranger.Id, budget.Id,
                new UpdateBudgetRequest(100_000, SecurityPin: CrudFixture.Pin)), default);

        // Assert: trả NotFound để không làm lộ tài nguyên của người khác.
        await Assert.ThrowsAsync<NotFoundException>(action);
        Assert.Equal(1_000_000, budget.LimitAmount);
    }

    [Fact]
    public async Task Budget_get_by_id_enforces_ownership()
    {
        // Arrange: tạo ngân sách và một người dùng không sở hữu.
        await using var fixture = await CrudFixture.CreateAsync();
        var owner = await fixture.AddUserAsync("Owner");
        var stranger = await fixture.AddUserAsync("Stranger");
        var category = await fixture.AddCategoryAsync(owner.Id, CategoryKind.Expense);
        var budget = await fixture.AddBudgetAsync(owner.Id, category, 1_000_000);
        var handler = new GetBudgetByIdQueryHandler(fixture.Db, new BudgetReader(fixture.Db));

        // Act và Assert: chủ sở hữu đọc được, người khác không đọc được.
        Assert.Equal(budget.Id, (await handler.Handle(new GetBudgetByIdQuery(owner.Id, budget.Id), default)).Id);
        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new GetBudgetByIdQuery(stranger.Id, budget.Id), default));
    }

    [Fact]
    public async Task Goal_update_never_overwrites_allocated_money()
    {
        // Arrange: tạo mục tiêu và mô phỏng số tiền đã được phân bổ.
        await using var fixture = await CrudFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Goal owner");
        var goal = await fixture.CreateGoalHandler().Handle(
            new CreateGoalCommand(user.Id, new CreateGoalRequest("Laptop", 10_000_000, SecurityPin: CrudFixture.Pin)),
            default);
        var entity = await fixture.Db.Goals.FindAsync(goal.Id);
        entity!.CurrentAllocatedAmount = 2_000_000;
        await fixture.Db.SaveChangesAsync();

        // Act: sửa tên, số tiền mục tiêu và thời hạn.
        var result = await new UpdateGoalCommandHandler(fixture.Db, fixture.Security, fixture.Clock).Handle(
            new UpdateGoalCommand(
                user.Id,
                goal.Id,
                new UpdateGoalRequest("Laptop mới", 12_000_000, DateTime.UtcNow.AddMonths(6), SecurityPin: CrudFixture.Pin)),
            default);

        // Assert: metadata đổi nhưng tiền đã phân bổ giữ nguyên.
        Assert.Equal("Laptop mới", result.Name);
        Assert.Equal(12_000_000, result.TargetAmount);
        Assert.Equal(2_000_000, result.CurrentAllocatedAmount);
    }

    [Fact]
    public async Task Goal_update_preserves_paused_status()
    {
        // Arrange: tạo Goal đã đạt mục tiêu nhưng đang được người dùng tạm dừng.
        await using var fixture = await CrudFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Paused goal owner");
        var created = await fixture.CreateGoalHandler().Handle(
            new CreateGoalCommand(user.Id, new CreateGoalRequest("Paused", 1_000, SecurityPin: CrudFixture.Pin)),
            default);
        var goal = await fixture.Db.Goals.FindAsync(created.Id);
        goal!.CurrentAllocatedAmount = 1_000;
        goal.Status = GoalStatus.Paused;
        await fixture.Db.SaveChangesAsync();

        // Act: chỉ cập nhật metadata của Goal.
        var result = await new UpdateGoalCommandHandler(fixture.Db, fixture.Security, fixture.Clock).Handle(
            new UpdateGoalCommand(user.Id, goal.Id, new UpdateGoalRequest("Paused updated", 1_000, SecurityPin: CrudFixture.Pin)),
            default);

        // Assert: metadata update không tự chuyển Paused thành Completed.
        Assert.Equal(GoalStatus.Paused.ToString(), result.Status);
    }

    [Fact]
    public async Task Goal_update_preserves_cancelled_status()
    {
        // Arrange: tạo Goal đã đạt mức tiền nhưng có trạng thái Cancelled.
        await using var fixture = await CrudFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Cancelled goal owner");
        var created = await fixture.CreateGoalHandler().Handle(
            new CreateGoalCommand(user.Id, new CreateGoalRequest("Cancelled", 1_000, SecurityPin: CrudFixture.Pin)),
            default);
        var goal = await fixture.Db.Goals.FindAsync(created.Id);
        goal!.CurrentAllocatedAmount = 1_000;
        goal.Status = GoalStatus.Cancelled;
        await fixture.Db.SaveChangesAsync();

        // Act: sửa tên nhưng không thực hiện nghiệp vụ thay đổi vòng đời.
        var result = await new UpdateGoalCommandHandler(fixture.Db, fixture.Security, fixture.Clock).Handle(
            new UpdateGoalCommand(user.Id, goal.Id, new UpdateGoalRequest("Cancelled updated", 1_000, SecurityPin: CrudFixture.Pin)),
            default);

        // Assert: Cancelled được giữ nguyên dù số tiền đã đạt mục tiêu.
        Assert.Equal(GoalStatus.Cancelled.ToString(), result.Status);
    }

    [Fact]
    public async Task Goal_update_marks_active_goal_completed_when_target_is_reached()
    {
        // Arrange: Goal Active đã phân bổ 1.000 nhưng mục tiêu cũ còn cao hơn.
        await using var fixture = await CrudFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Active goal owner");
        var created = await fixture.CreateGoalHandler().Handle(
            new CreateGoalCommand(user.Id, new CreateGoalRequest("Active", 2_000, SecurityPin: CrudFixture.Pin)),
            default);
        var goal = await fixture.Db.Goals.FindAsync(created.Id);
        goal!.CurrentAllocatedAmount = 1_000;
        goal.Status = GoalStatus.Active;
        await fixture.Db.SaveChangesAsync();

        // Act: giảm target đến đúng số tiền hiện đã phân bổ.
        var result = await new UpdateGoalCommandHandler(fixture.Db, fixture.Security, fixture.Clock).Handle(
            new UpdateGoalCommand(user.Id, goal.Id, new UpdateGoalRequest("Reached", 1_000, SecurityPin: CrudFixture.Pin)),
            default);

        // Assert: Active chuyển sang Completed khi tiến độ đạt 100%.
        Assert.Equal(GoalStatus.Completed.ToString(), result.Status);
    }

    [Fact]
    public async Task Goal_update_reactivates_completed_goal_when_target_increases()
    {
        // Arrange: Goal Completed có tiền phân bổ bằng mục tiêu hiện tại.
        await using var fixture = await CrudFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Completed goal owner");
        var created = await fixture.CreateGoalHandler().Handle(
            new CreateGoalCommand(user.Id, new CreateGoalRequest("Completed", 1_000, SecurityPin: CrudFixture.Pin)),
            default);
        var goal = await fixture.Db.Goals.FindAsync(created.Id);
        goal!.CurrentAllocatedAmount = 1_000;
        goal.Status = GoalStatus.Completed;
        await fixture.Db.SaveChangesAsync();

        // Act: tăng target cao hơn số tiền đã phân bổ.
        var result = await new UpdateGoalCommandHandler(fixture.Db, fixture.Security, fixture.Clock).Handle(
            new UpdateGoalCommand(user.Id, goal.Id, new UpdateGoalRequest("Stretch", 2_000, SecurityPin: CrudFixture.Pin)),
            default);

        // Assert: Completed trở lại Active để tiếp tục theo dõi tiến độ.
        Assert.Equal(GoalStatus.Active.ToString(), result.Status);
    }

    [Fact]
    public async Task Goal_delete_allows_empty_goal_without_history()
    {
        // Arrange: tạo Goal chưa có đóng góp và ghi nhớ LedgerAccount đi kèm.
        await using var fixture = await CrudFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Goal owner");
        var goal = await fixture.CreateGoalHandler().Handle(
            new CreateGoalCommand(user.Id, new CreateGoalRequest("Empty", 1_000, SecurityPin: CrudFixture.Pin)),
            default);
        var ledgerAccountId = (await fixture.Db.Goals.FindAsync(goal.Id))!.LedgerAccountId;

        // Act: xóa mục tiêu rỗng.
        await new DeleteGoalCommandHandler(fixture.Db, fixture.Security)
            .Handle(new DeleteGoalCommand(user.Id, goal.Id, CrudFixture.Pin), default);

        // Assert: Goal và tài khoản dự trữ chưa dùng đều được dọn sạch.
        Assert.Null(await fixture.Db.Goals.FindAsync(goal.Id));
        Assert.Null(await fixture.Db.LedgerAccounts.FindAsync(ledgerAccountId));
    }

    [Fact]
    public async Task Goal_delete_rejects_allocated_money()
    {
        // Arrange: tạo Goal đang giữ tiền.
        await using var fixture = await CrudFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Goal owner");
        var goal = await fixture.CreateGoalHandler().Handle(
            new CreateGoalCommand(user.Id, new CreateGoalRequest("Emergency", 5_000, SecurityPin: CrudFixture.Pin)),
            default);
        var entity = await fixture.Db.Goals.FindAsync(goal.Id);
        entity!.CurrentAllocatedAmount = 500;
        await fixture.Db.SaveChangesAsync();

        // Act: thử xóa khi chưa rút hết tiền.
        var error = await Assert.ThrowsAsync<ConflictException>(() =>
            new DeleteGoalCommandHandler(fixture.Db, fixture.Security)
                .Handle(new DeleteGoalCommand(user.Id, goal.Id, CrudFixture.Pin), default));

        // Assert: nghiệp vụ bị từ chối và Goal vẫn còn.
        Assert.Equal("goal_has_allocated_money", error.Code);
        Assert.NotNull(await fixture.Db.Goals.FindAsync(goal.Id));
    }

    [Fact]
    public async Task Goal_delete_rejects_existing_financial_history_even_after_money_is_withdrawn()
    {
        // Arrange: tạo Goal có số dư bằng không nhưng đã có một movement trong lịch sử.
        await using var fixture = await CrudFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Goal owner");
        var goal = await fixture.CreateGoalHandler().Handle(
            new CreateGoalCommand(user.Id, new CreateGoalRequest("Used goal", 5_000, SecurityPin: CrudFixture.Pin)),
            default);
        fixture.Db.GoalMovements.Add(new GoalMovement
        {
            GoalId = goal.Id,
            WalletId = Guid.NewGuid(),
            Type = GoalMovementType.Withdrawal,
            Amount = 500,
            LedgerTransactionId = Guid.NewGuid(),
            IdempotencyKey = "old-movement"
        });
        await fixture.Db.SaveChangesAsync();

        // Act: thử xóa Goal đã từng luân chuyển tiền.
        var error = await Assert.ThrowsAsync<ConflictException>(() =>
            new DeleteGoalCommandHandler(fixture.Db, fixture.Security)
                .Handle(new DeleteGoalCommand(user.Id, goal.Id, CrudFixture.Pin), default));

        // Assert: lịch sử tài chính buộc Goal phải được giữ lại.
        Assert.Equal("goal_has_financial_history", error.Code);
        Assert.NotNull(await fixture.Db.Goals.FindAsync(goal.Id));
    }

    [Fact]
    public async Task Goal_get_by_id_enforces_ownership()
    {
        // Arrange: tạo Goal thuộc một người dùng.
        await using var fixture = await CrudFixture.CreateAsync();
        var owner = await fixture.AddUserAsync("Owner");
        var stranger = await fixture.AddUserAsync("Stranger");
        var goal = await fixture.CreateGoalHandler().Handle(
            new CreateGoalCommand(owner.Id, new CreateGoalRequest("Private", 1_000, SecurityPin: CrudFixture.Pin)),
            default);
        var handler = new GetGoalByIdQueryHandler(fixture.Db);

        // Act và Assert: chỉ chủ sở hữu xem được chi tiết.
        Assert.Equal(goal.Id, (await handler.Handle(new GetGoalByIdQuery(owner.Id, goal.Id), default)).Id);
        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new GetGoalByIdQuery(stranger.Id, goal.Id), default));
    }

    [Fact]
    public async Task Recurring_update_changes_future_schedule_only()
    {
        // Arrange: tạo ví, hai danh mục và một định nghĩa định kỳ đã từng chạy.
        await using var fixture = await CrudFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Recurring owner");
        var wallet = await fixture.AddWalletAsync(user.Id, 500);
        var oldCategory = await fixture.AddCategoryAsync(user.Id, CategoryKind.Expense, "Old");
        var newCategory = await fixture.AddCategoryAsync(user.Id, CategoryKind.Expense, "New");
        var lastRun = DateTime.UtcNow.AddDays(-1);
        var recurring = await fixture.AddRecurringAsync(user.Id, wallet.Id, oldCategory.Id, lastRun);

        // Act: sửa cấu hình cho lần chạy kế tiếp.
        var result = await new UpdateRecurringTransactionCommandHandler(fixture.Db, fixture.Clock).Handle(
            new UpdateRecurringTransactionCommand(
                user.Id,
                recurring.Id,
                new UpdateRecurringRequest(wallet.Id, newCategory.Id, "Expense", 250, "Updated", "Weekly", DateTime.UtcNow.AddDays(7))),
            default);

        // Assert: cấu hình đổi nhưng LastRunAt vẫn là lịch sử cũ.
        Assert.Equal(newCategory.Id, result.CategoryId);
        Assert.Equal("Weekly", result.Frequency);
        Assert.Equal(lastRun, result.LastRunAt);
    }

    [Fact]
    public async Task Recurring_delete_removes_future_definition()
    {
        // Arrange: tạo một định nghĩa định kỳ chưa phát sinh lịch sử.
        await using var fixture = await CrudFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Recurring owner");
        var wallet = await fixture.AddWalletAsync(user.Id, 0);
        var category = await fixture.AddCategoryAsync(user.Id, CategoryKind.Expense);
        var recurring = await fixture.AddRecurringAsync(user.Id, wallet.Id, category.Id);

        // Act: xóa định nghĩa để dừng các lần chạy tương lai.
        await new DeleteRecurringTransactionCommandHandler(fixture.Db)
            .Handle(new DeleteRecurringTransactionCommand(user.Id, recurring.Id), default);

        // Assert: định nghĩa được hủy mềm để worker không tạo lần đến hạn mới.
        Assert.Equal(
            RecurringStatus.Cancelled,
            (await fixture.Db.RecurringTransactions.FindAsync(recurring.Id))!.Status);
    }

    [Fact]
    public async Task Recurring_delete_preserves_generated_transaction_history()
    {
        // Arrange: tạo định nghĩa và một giao dịch lịch sử tham chiếu business Id của nó.
        await using var fixture = await CrudFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Recurring owner");
        var wallet = await fixture.AddWalletAsync(user.Id, 0);
        var category = await fixture.AddCategoryAsync(user.Id, CategoryKind.Expense);
        var recurring = await fixture.AddRecurringAsync(user.Id, wallet.Id, category.Id);
        var history = await fixture.AddFinancialHistoryAsync(user.Id, wallet, category, recurring.Id);

        // Act: xóa lịch định kỳ.
        await new DeleteRecurringTransactionCommandHandler(fixture.Db)
            .Handle(new DeleteRecurringTransactionCommand(user.Id, recurring.Id), default);

        // Assert: lịch tương lai bị hủy mềm nhưng giao dịch đã phát sinh vẫn còn.
        Assert.Equal(
            RecurringStatus.Cancelled,
            (await fixture.Db.RecurringTransactions.FindAsync(recurring.Id))!.Status);
        Assert.NotNull(await fixture.Db.FinancialTransactions.FindAsync(history.Id));
    }

    [Fact]
    public async Task Recurring_get_by_id_enforces_ownership()
    {
        // Arrange: tạo định nghĩa của owner và một người dùng khác.
        await using var fixture = await CrudFixture.CreateAsync();
        var owner = await fixture.AddUserAsync("Owner");
        var stranger = await fixture.AddUserAsync("Stranger");
        var wallet = await fixture.AddWalletAsync(owner.Id, 0);
        var category = await fixture.AddCategoryAsync(owner.Id, CategoryKind.Expense);
        var recurring = await fixture.AddRecurringAsync(owner.Id, wallet.Id, category.Id);
        var handler = new GetRecurringTransactionByIdQueryHandler(fixture.Db, fixture.Clock);

        // Act và Assert: owner đọc được, người khác nhận NotFound.
        Assert.Equal(recurring.Id, (await handler.Handle(new GetRecurringTransactionByIdQuery(owner.Id, recurring.Id), default)).Id);
        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new GetRecurringTransactionByIdQuery(stranger.Id, recurring.Id), default));
    }

    [Fact]
    public async Task Transaction_rule_update_changes_configuration()
    {
        // Arrange: tạo quy tắc và danh mục đích mới.
        await using var fixture = await CrudFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Rule owner");
        var oldCategory = await fixture.AddCategoryAsync(user.Id, CategoryKind.Expense, "Old");
        var newCategory = await fixture.AddCategoryAsync(user.Id, CategoryKind.Expense, "New");
        var rule = await fixture.AddRuleAsync(user.Id, oldCategory);

        // Act: sửa mẫu, độ ưu tiên, danh mục và trạng thái.
        var result = await new UpdateTransactionRuleCommandHandler(fixture.Db).Handle(
            new UpdateTransactionRuleCommand(
                user.Id,
                rule.Id,
                new UpdateTransactionRuleRequest("coffee", newCategory.Id, 10, false)),
            default);

        // Assert: quy tắc mới được chuẩn hóa và lưu đầy đủ.
        Assert.Equal("COFFEE", result.DescriptionPattern);
        Assert.Equal(newCategory.Id, result.TargetCategoryId);
        Assert.Equal(10, result.Priority);
        Assert.False(result.IsActive);
    }

    [Fact]
    public async Task Transaction_rule_delete_removes_configuration()
    {
        // Arrange: tạo một quy tắc cấu hình.
        await using var fixture = await CrudFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Rule owner");
        var category = await fixture.AddCategoryAsync(user.Id, CategoryKind.Expense);
        var rule = await fixture.AddRuleAsync(user.Id, category);

        // Act: xóa quy tắc.
        await new DeleteTransactionRuleCommandHandler(fixture.Db)
            .Handle(new DeleteTransactionRuleCommand(user.Id, rule.Id), default);

        // Assert: quy tắc mất nhưng danh mục đích vẫn còn.
        Assert.Null(await fixture.Db.TransactionRules.FindAsync(rule.Id));
        Assert.NotNull(await fixture.Db.Categories.FindAsync(category.Id));
    }

    [Fact]
    public async Task Transaction_rule_get_by_id_enforces_ownership()
    {
        // Arrange: tạo quy tắc của owner và một người dùng khác.
        await using var fixture = await CrudFixture.CreateAsync();
        var owner = await fixture.AddUserAsync("Owner");
        var stranger = await fixture.AddUserAsync("Stranger");
        var category = await fixture.AddCategoryAsync(owner.Id, CategoryKind.Expense);
        var rule = await fixture.AddRuleAsync(owner.Id, category);
        var handler = new GetTransactionRuleByIdQueryHandler(fixture.Db);

        // Act và Assert: quy tắc chỉ hiển thị cho owner.
        Assert.Equal(rule.Id, (await handler.Handle(new GetTransactionRuleByIdQuery(owner.Id, rule.Id), default)).Id);
        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new GetTransactionRuleByIdQuery(stranger.Id, rule.Id), default));
    }

    [Fact]
    public async Task Category_update_changes_metadata_and_preserves_kind_when_requested()
    {
        // Arrange: tạo danh mục chi tiêu cá nhân.
        await using var fixture = await CrudFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Category owner");
        var category = await fixture.AddCategoryAsync(user.Id, CategoryKind.Expense, "Food");

        // Act: sửa tên và thông tin hiển thị.
        var result = await new UpdateCategoryCommandHandler(fixture.Db).Handle(
            new UpdateCategoryCommand(
                user.Id,
                category.Id,
                new UpdateCategoryRequest("Dining", "Expense", "fork", "#123456")),
            default);

        // Assert: metadata đổi nhưng Kind vẫn là Expense.
        Assert.Equal("Dining", result.Name);
        Assert.Equal("fork", result.Icon);
        Assert.Equal("Expense", result.Kind);
    }

    [Fact]
    public async Task Category_kind_change_is_blocked_by_incompatible_active_recurring_definition()
    {
        // Arrange: danh mục Chi đang được một lịch định kỳ Chi hoạt động tham chiếu.
        await using var fixture = await CrudFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Recurring category owner");
        var wallet = await fixture.AddWalletAsync(user.Id, 1_000_000);
        var category = await fixture.AddCategoryAsync(user.Id, CategoryKind.Expense, "Recurring food");
        fixture.Db.RecurringTransactions.Add(new RecurringTransaction
        {
            OwnerUserId = user.Id,
            WalletId = wallet.Id,
            CategoryId = category.Id,
            Kind = CategoryKind.Expense,
            Amount = 100_000,
            Title = "Lunch",
            Description = "Lunch plan",
            Frequency = RecurringFrequency.Monthly,
            NextRunAt = DateTime.UtcNow.AddDays(5),
            Status = RecurringStatus.Active
        });
        await fixture.Db.SaveChangesAsync();

        // Act và Assert: không âm thầm đổi ý nghĩa lịch đang chạy; lịch sử/danh mục giữ nguyên.
        await Assert.ThrowsAsync<ConflictException>(() => new UpdateCategoryCommandHandler(fixture.Db).Handle(
            new UpdateCategoryCommand(user.Id, category.Id,
                new UpdateCategoryRequest("Recurring income", "Income")), default));
        Assert.Equal(CategoryKind.Expense, category.Kind);
    }

    [Fact]
    public async Task Category_archive_preserves_historical_usage()
    {
        // Arrange: tạo danh mục đã được một giao dịch tài chính sử dụng.
        await using var fixture = await CrudFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Category owner");
        var wallet = await fixture.AddWalletAsync(user.Id, 0);
        var category = await fixture.AddCategoryAsync(user.Id, CategoryKind.Expense);
        var history = await fixture.AddFinancialHistoryAsync(user.Id, wallet, category, Guid.NewGuid());

        // Act: archive danh mục thay vì hard-delete.
        await new ArchiveCategoryCommandHandler(fixture.Db)
            .Handle(new ArchiveCategoryCommand(user.Id, category.Id), default);

        // Assert: danh mục ngừng hoạt động nhưng giao dịch lịch sử vẫn tham chiếu được.
        Assert.False(category.IsActive);
        Assert.Equal(category.Id, (await fixture.Db.FinancialTransactions.FindAsync(history.Id))!.CategoryId);
    }

    [Fact]
    public async Task Wallet_update_changes_metadata_without_balance_or_status()
    {
        // Arrange: tạo hai ví để kiểm tra chuyển cờ mặc định.
        await using var fixture = await CrudFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Wallet owner");
        var first = await fixture.AddWalletAsync(user.Id, 900, true, "Main");
        var second = await fixture.AddWalletAsync(
            user.Id,
            250,
            false,
            "Savings",
            WalletType.Savings);
        var originalLedgerId = second.LedgerAccount.Id;

        // Act: đổi metadata của ví thứ hai và đặt làm mặc định.
        var result = await new UpdateWalletCommandHandler(fixture.Db).Handle(
            new UpdateWalletCommand(user.Id, second.Id, new UpdateWalletRequest("Emergency", "Emergency", true)),
            default);

        // Assert: metadata đổi; số dư, trạng thái và tài khoản ledger giữ nguyên.
        fixture.Db.ChangeTracker.Clear();
        var storedFirst = await fixture.Db.Wallets.FindAsync(first.Id);
        var storedSecond = await fixture.Db.Wallets.FindAsync(second.Id);
        Assert.False(storedFirst!.IsDefault);
        Assert.True(storedSecond!.IsDefault);
        Assert.Equal(250, storedSecond.AvailableBalance);
        Assert.Equal(WalletStatus.Active, storedSecond.Status);
        Assert.Equal(second.Id, result.Id);
        Assert.NotNull(await fixture.Db.LedgerAccounts.FindAsync(originalLedgerId));
    }

    [Fact]
    public async Task Wallet_create_default_rolls_back_previous_default_when_insert_fails()
    {
        // Arrange: ví A đang là default và trigger buộc insert ví B thất bại.
        await using var fixture = await CrudFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Create rollback owner");
        var walletA = await fixture.AddWalletAsync(user.Id, 500, true, "Wallet A");
        await fixture.Db.Database.ExecuteSqlRawAsync(
            """
            CREATE TRIGGER fail_default_wallet_insert
            BEFORE INSERT ON Wallets
            WHEN NEW.Name = 'Fail Create'
            BEGIN
                SELECT RAISE(ABORT, 'forced wallet insert failure');
            END;
            """);

        // Act: thử tạo ví B làm default; lần SaveChanges cuối bị trigger từ chối.
        await Assert.ThrowsAsync<DbUpdateException>(() =>
            new CreateWalletCommandHandler(fixture.Db, new SpendWise.Application.Features.Banking.ActiveBankGuard(fixture.Db)).Handle(
                new CreateWalletCommand(
                    user.Id,
                    new CreateWalletRequest("Fail Create", "Savings", true)),
                default));

        // Assert: transaction rollback nên ví A vẫn default và ví B không được tạo.
        fixture.Db.ChangeTracker.Clear();
        var storedA = await fixture.Db.Wallets.AsNoTracking().SingleAsync(wallet => wallet.Id == walletA.Id);
        Assert.True(storedA.IsDefault);
        Assert.Equal(1, await fixture.Db.Wallets.CountAsync(wallet => wallet.OwnerUserId == user.Id));
    }

    [Fact]
    public async Task Wallet_update_default_rolls_back_both_wallets_when_final_write_fails()
    {
        // Arrange: ví A là default, ví B giữ metadata và số dư ban đầu.
        await using var fixture = await CrudFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Update rollback owner");
        var walletA = await fixture.AddWalletAsync(user.Id, 500, true, "Wallet A");
        var walletB = await fixture.AddWalletAsync(
            user.Id,
            250,
            false,
            "Wallet B",
            WalletType.Savings);
        var walletBLedgerId = walletB.LedgerAccount.Id;
        await fixture.Db.Database.ExecuteSqlRawAsync(
            """
            CREATE TRIGGER fail_default_wallet_update
            BEFORE UPDATE ON Wallets
            WHEN NEW.Name = 'Fail Update'
            BEGIN
                SELECT RAISE(ABORT, 'forced wallet update failure');
            END;
            """);

        // Act: thử đổi B thành default và đổi metadata; update B bị trigger từ chối.
        await Assert.ThrowsAsync<DbUpdateException>(() =>
            new UpdateWalletCommandHandler(fixture.Db).Handle(
                new UpdateWalletCommand(
                    user.Id,
                    walletB.Id,
                    new UpdateWalletRequest("Fail Update", "Emergency", true)),
                default));

        // Assert: A vẫn default; B giữ nguyên metadata, số dư, trạng thái và ledger.
        fixture.Db.ChangeTracker.Clear();
        var storedA = await fixture.Db.Wallets.AsNoTracking().SingleAsync(wallet => wallet.Id == walletA.Id);
        var storedB = await fixture.Db.Wallets.AsNoTracking().SingleAsync(wallet => wallet.Id == walletB.Id);
        Assert.True(storedA.IsDefault);
        Assert.False(storedB.IsDefault);
        Assert.Equal("Wallet B", storedB.Name);
        Assert.Equal(WalletType.Savings, storedB.Type);
        Assert.Equal(250, storedB.AvailableBalance);
        Assert.Equal(WalletStatus.Active, storedB.Status);
        Assert.NotNull(await fixture.Db.LedgerAccounts.FindAsync(walletBLedgerId));
    }

    [Fact]
    public async Task Wallet_close_enforces_zero_balance_and_preserves_history()
    {
        // Arrange: tạo ví có số dư và một giao dịch lịch sử.
        await using var fixture = await CrudFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Wallet owner");
        var wallet = await fixture.AddWalletAsync(
            user.Id,
            100,
            type: WalletType.Savings);
        var category = await fixture.AddCategoryAsync(user.Id, CategoryKind.Expense);
        var history = await fixture.AddFinancialHistoryAsync(user.Id, wallet, category, Guid.NewGuid());
        var handler = new CloseWalletCommandHandler(fixture.Db);

        // Act và Assert: ví còn tiền không được đóng.
        await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(new CloseWalletCommand(user.Id, wallet.Id), default));

        // Act: rút hết số dư qua giả lập chuẩn bị rồi đóng ví.
        wallet.AvailableBalance = 0;
        wallet.LedgerAccount.Balance = 0;
        await fixture.Db.SaveChangesAsync();
        await handler.Handle(new CloseWalletCommand(user.Id, wallet.Id), default);

        // Assert: ví chuyển Closed nhưng giao dịch và ledger vẫn tồn tại.
        Assert.Equal(WalletStatus.Closed, wallet.Status);
        Assert.NotNull(await fixture.Db.FinancialTransactions.FindAsync(history.Id));
        Assert.NotNull(await fixture.Db.LedgerAccounts.FindAsync(wallet.LedgerAccount.Id));
    }

    [Fact]
    public async Task Ai_conversation_get_and_update_enforce_ownership_and_preserve_messages()
    {
        // Arrange: tạo hội thoại có tin nhắn và một người dùng khác.
        await using var fixture = await CrudFixture.CreateAsync();
        var owner = await fixture.AddUserAsync("AI owner");
        var stranger = await fixture.AddUserAsync("Stranger");
        var conversation = new AiConversation { UserId = owner.Id, Title = "Old" };
        conversation.Messages.Add(new AiMessage
        {
            AiConversationId = conversation.Id,
            Role = AiMessageRole.User,
            Content = "Xin chào"
        });
        fixture.Db.AiConversations.Add(conversation);
        await fixture.Db.SaveChangesAsync();

        // Act: đổi tiêu đề, pin mà không gửi lại title, rồi tìm theo title/message.
        var update = new UpdateAiConversationCommandHandler(fixture.Db);
        await update.Handle(
            new UpdateAiConversationCommand(owner.Id, conversation.Id, new UpdateAiConversationRequest("Kế hoạch tháng")),
            default);
        var pinned = await update.Handle(
            new UpdateAiConversationCommand(owner.Id, conversation.Id, new UpdateAiConversationRequest(IsPinned: true)),
            default);
        var query = new GetAiConversationByIdQueryHandler(fixture.Db);
        var result = await query.Handle(new GetAiConversationByIdQuery(owner.Id, conversation.Id), default);
        var list = new GetAiConversationsQueryHandler(fixture.Db);
        var byTitle = await list.Handle(new GetAiConversationsQuery(owner.Id, "Kế hoạch"), default);
        var byMessage = await list.Handle(new GetAiConversationsQuery(owner.Id, "Xin chào"), default);
        var unpinned = await update.Handle(
            new UpdateAiConversationCommand(owner.Id, conversation.Id, new UpdateAiConversationRequest(IsPinned: false)),
            default);

        // Assert: pin/unpin không cần title, search hoạt động và người khác không xem được.
        Assert.Equal("Kế hoạch tháng", result.Title);
        Assert.Equal("Kế hoạch tháng", pinned.Title);
        Assert.True(pinned.IsPinned);
        Assert.False(unpinned.IsPinned);
        Assert.Single(byTitle);
        Assert.Single(byMessage);
        Assert.Single(result.Messages);
        await Assert.ThrowsAsync<NotFoundException>(() =>
            query.Handle(new GetAiConversationByIdQuery(stranger.Id, conversation.Id), default));
    }

    [Fact]
    public async Task Ai_group_context_is_revalidated_after_membership_is_removed()
    {
        // Arrange: tạo group context hợp lệ cho thành viên đang hoạt động.
        await using var fixture = await CrudFixture.CreateAsync();
        var user = await fixture.AddUserAsync("AI group member");
        var group = new Group { Name = "Private group", Type = GroupType.Friends, Status = GroupStatus.Active };
        var member = new GroupMember
        {
            Group = group,
            GroupId = group.Id,
            User = user,
            UserId = user.Id,
            Role = GroupRole.Member,
            JoinedAt = DateTime.UtcNow
        };
        var conversation = new AiConversation
        {
            UserId = user.Id,
            Title = "Group context",
            ContextType = AiConversationContextType.Group,
            ContextGroupId = group.Id
        };
        fixture.Db.AddRange(group, member, conversation);
        await fixture.Db.SaveChangesAsync();
        var handler = new GetAiConversationByIdQueryHandler(fixture.Db);
        Assert.Equal(group.Id, (await handler.Handle(new(user.Id, conversation.Id), default)).ContextGroupId);

        // Act: xóa quan hệ thành viên sau khi conversation đã tạo.
        fixture.Db.GroupMembers.Remove(member);
        await fixture.Db.SaveChangesAsync();

        // Assert: lịch sử vẫn còn nhưng không cho đọc dữ liệu Group hiện tại.
        await Assert.ThrowsAsync<ForbiddenException>(() => handler.Handle(new(user.Id, conversation.Id), default));
        Assert.NotNull(await fixture.Db.AiConversations.FindAsync(conversation.Id));
    }

    [Theory]
    [InlineData(true, GroupStatus.Active)]
    [InlineData(false, GroupStatus.Suspended)]
    [InlineData(false, GroupStatus.Closed)]
    [InlineData(false, GroupStatus.DeletionPending)]
    public async Task Ai_M02_inaccessible_group_history_is_excluded_from_list_and_search(bool removed, GroupStatus status)
    {
        await using var fixture = await CrudFixture.CreateAsync();
        var user = await fixture.AddUserAsync("History owner");
        var group = new Group { Name = "Private", Type = GroupType.Friends, Status = status };
        var member = new GroupMember { Group = group, UserId = user.Id, Role = GroupRole.Member };
        var hidden = new AiConversation { UserId = user.Id, Title = "secret metadata", ContextType = AiConversationContextType.Group, ContextGroupId = group.Id };
        hidden.Messages.Add(new AiMessage { Role = AiMessageRole.Assistant, Content = "secret message" });
        var general = new AiConversation { UserId = user.Id, Title = "Allowed", ContextType = AiConversationContextType.General };
        fixture.Db.AddRange(group, member, hidden, general);
        await fixture.Db.SaveChangesAsync();
        if (removed) { fixture.Db.Remove(member); await fixture.Db.SaveChangesAsync(); }
        await Assert.ThrowsAsync<ForbiddenException>(() => new GetAiConversationByIdQueryHandler(fixture.Db).Handle(new(user.Id, hidden.Id), default));
        var handler = new GetAiConversationsQueryHandler(fixture.Db);
        Assert.Equal(general.Id, Assert.Single(await handler.Handle(new(user.Id), default)).Id);
        Assert.Empty(await handler.Handle(new(user.Id, "secret"), default));
        Assert.Equal(AiConversationContextType.Group, hidden.ContextType);
        Assert.Single(hidden.Messages);
    }

    [Theory]
    [InlineData(true, GroupStatus.Active, "General")]
    [InlineData(true, GroupStatus.Active, "Group")]
    [InlineData(false, GroupStatus.Suspended, "General")]
    [InlineData(false, GroupStatus.Closed, "Group")]
    [InlineData(false, GroupStatus.DeletionPending, "General")]
    public async Task Ai_M02_update_checks_source_group_before_context_transition(bool removed, GroupStatus status, string targetContext)
    {
        await using var fixture = await CrudFixture.CreateAsync();
        var user = await fixture.AddUserAsync("History owner");
        var source = new Group { Name = "Source", Type = GroupType.Friends, Status = status };
        var target = new Group { Name = "Target", Type = GroupType.Friends, Status = GroupStatus.Active };
        var member = new GroupMember { Group = source, UserId = user.Id, Role = GroupRole.Member };
        var conversation = new AiConversation { UserId = user.Id, Title = "Original", ContextType = AiConversationContextType.Group, ContextGroupId = source.Id };
        conversation.Messages.Add(new AiMessage { Role = AiMessageRole.Assistant, Content = "Private history" });
        fixture.Db.AddRange(source, target, member, new GroupMember { Group = target, UserId = user.Id, Role = GroupRole.Member }, conversation);
        await fixture.Db.SaveChangesAsync();
        if (removed) { fixture.Db.Remove(member); await fixture.Db.SaveChangesAsync(); }
        var handler = new UpdateAiConversationCommandHandler(fixture.Db);
        await Assert.ThrowsAsync<ForbiddenException>(() => handler.Handle(new(user.Id, conversation.Id, new("Changed", true, targetContext, target.Id)), default));
        fixture.Db.ChangeTracker.Clear();
        var stored = await fixture.Db.AiConversations.Include(x => x.Messages).SingleAsync();
        Assert.Equal("Original", stored.Title);
        Assert.False(stored.IsPinned);
        Assert.Equal(source.Id, stored.ContextGroupId);
        Assert.Equal(AiConversationContextType.Group, stored.ContextType);
        Assert.Equal("Private history", Assert.Single(stored.Messages).Content);
    }

    [Fact]
    public async Task Ai_M02_active_source_and_target_membership_preserve_legitimate_history_and_isolation()
    {
        await using var fixture = await CrudFixture.CreateAsync();
        var owner = await fixture.AddUserAsync("Owner");
        var stranger = await fixture.AddUserAsync("Stranger");
        var a = new Group { Name = "A", Type = GroupType.Friends, Status = GroupStatus.Active };
        var b = new Group { Name = "B", Type = GroupType.Friends, Status = GroupStatus.Active };
        var c = new AiConversation { UserId = owner.Id, Title = "Allowed", ContextType = AiConversationContextType.Group, ContextGroupId = a.Id };
        c.Messages.Add(new AiMessage { Role = AiMessageRole.Assistant, Content = "History" });
        var admin = new AiConversation { UserId = owner.Id, Title = "Admin", ContextType = AiConversationContextType.Admin };
        fixture.Db.AddRange(a, b, new GroupMember { Group = a, UserId = owner.Id, Role = GroupRole.Member }, c, admin);
        await fixture.Db.SaveChangesAsync();
        var list = new GetAiConversationsQueryHandler(fixture.Db);
        Assert.Equal(c.Id, Assert.Single(await list.Handle(new(owner.Id, "History"), default)).Id);
        Assert.Empty(await list.Handle(new(stranger.Id), default));
        var update = new UpdateAiConversationCommandHandler(fixture.Db);
        await Assert.ThrowsAsync<NotFoundException>(() => update.Handle(new(stranger.Id, c.Id, new("Foreign")), default));
        await Assert.ThrowsAsync<NotFoundException>(() => update.Handle(new(owner.Id, admin.Id, new("Admin")), default));
        await Assert.ThrowsAsync<ForbiddenException>(() => update.Handle(new(owner.Id, c.Id, new(ContextType: "Group", ContextGroupId: b.Id)), default));
        Assert.Equal(a.Id, c.ContextGroupId);
        fixture.Db.Add(new GroupMember { Group = b, UserId = owner.Id, Role = GroupRole.Member });
        await fixture.Db.SaveChangesAsync();
        var moved = await update.Handle(new(owner.Id, c.Id, new("Changed", true, "Group", b.Id)), default);
        Assert.Equal(b.Id, moved.ContextGroupId);
        Assert.Equal("History", Assert.Single(moved.Messages).Content);
        var general = await update.Handle(new(owner.Id, c.Id, new(ContextType: "General")), default);
        Assert.Null(general.ContextGroupId);
        Assert.Equal("General", general.ContextType);
        Assert.Equal(c.Id, Assert.Single(await list.Handle(new(owner.Id), default)).Id);
        Assert.Equal("Renamed", (await update.Handle(new(owner.Id, c.Id, new("Renamed")), default)).Title);
    }

    private sealed class CrudFixture : IAsyncDisposable
    {
        public const string Pin = "123456";
        private readonly SqliteConnection connection;

        private CrudFixture(SqliteConnection connection, SpendWiseDbContext db, SystemClock clock)
        {
            // Giữ DbContext và kết nối SQLite dùng cho toàn bộ vòng đời của mỗi test CRUD.
            this.connection = connection;
            Db = db;
            Clock = clock;
            Security = new FinancialSecurityGuard(db, new SecurityPinService(),
                new AccountCredentialLockout(db, clock));
        }

        public SpendWiseDbContext Db { get; }
        public SystemClock Clock { get; }
        public FinancialSecurityGuard Security { get; }

        public CreateGoalCommandHandler CreateGoalHandler()
        {
            var movement = new GoalMovementExecutor(Db, new LedgerService(Db, Clock), Clock, Security, new SpendWise.Application.Features.Banking.ActiveBankGuard(Db));
            return new CreateGoalCommandHandler(Db, Security, movement, Clock);
        }

        public static async Task<CrudFixture> CreateAsync()
        {
            // Dùng SQLite in-memory để kiểm tra khóa ngoại và hành vi relational thật.
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<SpendWiseDbContext>()
                .UseSqlite(connection)
                .Options;
            var clock = new SystemClock();
            var db = new SpendWiseDbContext(options, clock);
            await db.Database.EnsureCreatedAsync();
            return new CrudFixture(connection, db, clock);
        }

        public async Task<User> AddUserAsync(string name)
        {
            // Tạo người dùng tối thiểu hợp lệ cho các kiểm tra quyền sở hữu.
            var unique = Guid.NewGuid().ToString("N");
            var user = new User
            {
                FullName = name,
                Email = $"{unique}@test.local",
                NormalizedEmail = $"{unique}@TEST.LOCAL",
                SpendId = unique,
                NormalizedSpendId = unique.ToUpperInvariant(),
                PasswordHash = "test",
                SecurityPinHash = new SecurityPinService().Hash(Pin)
            };
            var bank = new SpendWise.Domain.Banking.BankConnection
            {
                UserId = user.Id,
                Provider = SpendWise.Domain.Banking.BankProvider.SePayBankHub,
                AccountEnvironment = SpendWise.Domain.Banking.SePayAccountEnvironment.TestMode,
                ExternalAccountId = $"ACC_{user.Id:N}",
                BankBrandName = "MBBank",
                AccountHolderName = user.FullName,
                MaskedAccountNumber = "****1234",
                Status = SpendWise.Domain.Banking.BankConnectionStatus.Active,
                LinkedAt = DateTime.UtcNow
            };
            Db.Users.Add(user);
            Db.BankConnections.Add(bank);
            await Db.SaveChangesAsync();
            return user;
        }

        public async Task<Category> AddCategoryAsync(
            Guid ownerUserId,
            CategoryKind kind,
            string name = "Category")
        {
            // Tạo danh mục cá nhân đang hoạt động.
            var category = new Category
            {
                OwnerUserId = ownerUserId,
                Key = $"test:{Guid.NewGuid():N}",
                Name = $"{name}-{Guid.NewGuid():N}",
                Kind = kind
            };
            Db.Categories.Add(category);
            await Db.SaveChangesAsync();
            return category;
        }

        public async Task<Wallet> AddWalletAsync(
            Guid ownerUserId,
            decimal balance,
            bool isDefault = true,
            string name = "Wallet",
            WalletType type = WalletType.Main)
        {
            // WalletFactory luôn tạo kèm LedgerAccount tương ứng.
            var wallet = WalletFactory.Create(ownerUserId, name, type, isDefault);
            wallet.AvailableBalance = balance;
            wallet.LedgerAccount.Balance = balance;
            Db.Wallets.Add(wallet);
            await Db.SaveChangesAsync();
            return wallet;
        }

        public async Task<Budget> AddBudgetAsync(Guid ownerUserId, Category category, decimal limit)
        {
            // Tạo ngân sách tháng 9/2026 để các test Update/Delete dùng chung.
            var budget = new Budget
            {
                OwnerUserId = ownerUserId,
                CategoryId = category.Id,
                Category = category,
                Year = 2026,
                Month = 9,
                LimitAmount = limit
            };
            Db.Budgets.Add(budget);
            await Db.SaveChangesAsync();
            return budget;
        }

        public async Task<RecurringTransaction> AddRecurringAsync(
            Guid ownerUserId,
            Guid walletId,
            Guid categoryId,
            DateTime? lastRunAt = null)
        {
            // Tạo định nghĩa định kỳ đại diện cho một lịch chi tiêu tương lai.
            var recurring = new RecurringTransaction
            {
                OwnerUserId = ownerUserId,
                WalletId = walletId,
                CategoryId = categoryId,
                Kind = CategoryKind.Expense,
                Amount = 100,
                Description = "Recurring",
                Frequency = RecurringFrequency.Monthly,
                NextRunAt = DateTime.UtcNow.AddMonths(1),
                LastRunAt = lastRunAt
            };
            Db.RecurringTransactions.Add(recurring);
            await Db.SaveChangesAsync();
            return recurring;
        }

        public async Task<TransactionRule> AddRuleAsync(Guid ownerUserId, Category category)
        {
            // Tạo quy tắc phân loại thuộc người dùng.
            var rule = new TransactionRule
            {
                OwnerUserId = ownerUserId,
                DescriptionPattern = "OLD",
                TargetCategoryId = category.Id,
                TargetCategory = category
            };
            Db.TransactionRules.Add(rule);
            await Db.SaveChangesAsync();
            return rule;
        }

        public async Task<FinancialTransaction> AddFinancialHistoryAsync(
            Guid ownerUserId,
            Wallet wallet,
            Category category,
            Guid relatedBusinessId)
        {
            // Tạo posting hoàn tất để mô phỏng một giao dịch lịch sử đã ghi ledger.
            var ledgerTransaction = new LedgerTransaction
            {
                Reference = Guid.NewGuid().ToString("N"),
                BusinessType = "TestHistory",
                BusinessId = relatedBusinessId,
                Status = LedgerTransactionStatus.Completed,
                CompletedAt = DateTime.UtcNow
            };
            var history = new FinancialTransaction
            {
                OwnerUserId = ownerUserId,
                WalletId = wallet.Id,
                Wallet = wallet,
                CategoryId = category.Id,
                Category = category,
                LedgerTransactionId = ledgerTransaction.Id,
                LedgerTransaction = ledgerTransaction,
                RelatedBusinessId = relatedBusinessId,
                Type = FinancialTransactionType.Expense,
                Amount = 10,
                Description = "Historical record",
                OccurredAt = DateTime.UtcNow
            };
            Db.FinancialTransactions.Add(history);
            await Db.SaveChangesAsync();
            return history;
        }

        public async ValueTask DisposeAsync()
        {
            // Giải phóng DbContext và kết nối SQLite sau mỗi test.
            await Db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
