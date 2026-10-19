using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Planning;
using SpendWise.Domain.Finance;
using SpendWise.Domain.Planning;

namespace SpendWise.Application.Features.Budgets.Commands.CreateBudget;

// Tạo một cấu hình ngân sách duy nhất cho danh mục và tháng của người dùng.
public sealed class CreateBudgetCommandHandler(
    ISpendWiseDbContext db,
    BudgetReader budgetReader,
    FinancialSecurityGuard security) : IRequestHandler<CreateBudgetCommand, BudgetDto>
{
    public async Task<BudgetDto> Handle(
        CreateBudgetCommand command,
        CancellationToken cancellationToken)
    {
        // Mỗi mutation Budget tự xác minh PIN từ request hiện tại.
        await security.VerifyAsync(command.UserId, command.Request.SecurityPin, cancellationToken);
        // Kiểm tra tháng, năm và hạn mức trước khi truy cập cơ sở dữ liệu.
        if (command.Request.Month is < 1 or > 12
            || command.Request.Year is < 2000 or > 2200)
        {
            throw new AppException("invalid_budget", "Budget month, year, and limit are invalid.");
        }
        ValidateLimit(command.Request.LimitAmount);

        // Chỉ cho phép dùng danh mục chi tiêu hệ thống hoặc danh mục của chính người dùng.
        var category = await db.Categories.SingleOrDefaultAsync(
            candidate => candidate.Id == command.Request.CategoryId
                && candidate.Kind == CategoryKind.Expense
                && candidate.IsActive
                && (candidate.IsSystem || candidate.OwnerUserId == command.UserId),
            cancellationToken)
            ?? throw new AppException("invalid_category", "Expense category is invalid.");

        // Mỗi người dùng chỉ có một ngân sách cho cùng danh mục và tháng.
        var alreadyExists = await db.Budgets.AnyAsync(
            budget => budget.OwnerUserId == command.UserId
                && budget.CategoryId == command.Request.CategoryId
                && budget.Year == command.Request.Year
                && budget.Month == command.Request.Month,
            cancellationToken);
        if (alreadyExists)
        {
            throw new ConflictException("budget_exists", "Budget already exists for this category and month.");
        }

        // Tạo cấu hình ngân sách; thao tác này không thay đổi giao dịch đã có.
        var budget = new Budget
        {
            OwnerUserId = command.UserId,
            CategoryId = category.Id,
            Category = category,
            Year = command.Request.Year,
            Month = command.Request.Month,
            LimitAmount = command.Request.LimitAmount,
            AlertsEnabled = command.Request.AlertsEnabled,
            Rollover = command.Request.Rollover
        };
        db.Budgets.Add(budget);
        await db.SaveChangesAsync(cancellationToken);

        // Tính số tiền đã chi để trả DTO đầy đủ cho API.
        return await budgetReader.ToDtoAsync(budget, cancellationToken);
    }

    private static void ValidateLimit(decimal amount)
    {
        InputBoundary.PositiveMoney(amount, "Budget limit");
        if (amount < 100_000)
        {
            throw new AppException("budget_minimum", "Budget limit must be at least 100000 VND.");
        }
    }
}
