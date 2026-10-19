using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;

namespace SpendWise.Application.Features.Budgets.Commands.DeleteBudget;

// Handler xóa cấu hình ngân sách mà không ảnh hưởng giao dịch trong tháng.

public sealed class DeleteBudgetCommandHandler(
    ISpendWiseDbContext db,
    FinancialSecurityGuard security) : IRequestHandler<DeleteBudgetCommand>
{
    public async Task Handle(
        DeleteBudgetCommand command,
        CancellationToken cancellationToken)
    {
        await security.VerifyAsync(command.UserId, command.SecurityPin, cancellationToken);
        // Chỉ xóa cấu hình ngân sách thuộc người dùng, không đụng giao dịch lịch sử.
        var budget = await db.Budgets.SingleOrDefaultAsync(
            candidate => candidate.Id == command.BudgetId
                && candidate.OwnerUserId == command.UserId,
            cancellationToken)
            ?? throw new NotFoundException("budget_not_found", "Budget not found.");

        db.Budgets.Remove(budget);
        await db.SaveChangesAsync(cancellationToken);
    }
}
