using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Planning;

namespace SpendWise.Application.Features.Budgets.Commands.UpdateBudget;

// Chỉ sửa hạn mức và cảnh báo của ngân sách thuộc người dùng, không sửa giao dịch.
public sealed class UpdateBudgetCommandHandler(
    ISpendWiseDbContext db,
    BudgetReader budgetReader,
    FinancialSecurityGuard security) : IRequestHandler<UpdateBudgetCommand, BudgetDto>
{
    public async Task<BudgetDto> Handle(UpdateBudgetCommand command, CancellationToken cancellationToken)
    {
        await security.VerifyAsync(command.UserId, command.Request.SecurityPin, cancellationToken);
        // Hạn mức ngân sách tối thiểu phản ánh đơn vị VND của sản phẩm.
        InputBoundary.PositiveMoney(command.Request.LimitAmount, "Budget limit");
        if (command.Request.LimitAmount < 100_000)
        {
            throw new AppException("budget_minimum", "Budget limit must be at least 100000 VND.");
        }

        // Lọc theo OwnerUserId để người dùng không thể sửa ngân sách của người khác.
        var budget = await db.Budgets
            .Include(candidate => candidate.Category)
            .SingleOrDefaultAsync(
                candidate => candidate.Id == command.BudgetId
                    && candidate.OwnerUserId == command.UserId,
                cancellationToken)
            ?? throw new NotFoundException("budget_not_found", "Budget not found.");

        // Chỉ sửa cấu hình; các giao dịch dùng để tính chi tiêu vẫn được giữ nguyên.
        budget.LimitAmount = command.Request.LimitAmount;
        budget.AlertsEnabled = command.Request.AlertsEnabled;
        budget.Rollover = command.Request.Rollover;
        await db.SaveChangesAsync(cancellationToken);

        return await budgetReader.ToDtoAsync(budget, cancellationToken);
    }
}
