using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Planning;

namespace SpendWise.Application.Features.Budgets.Queries.GetBudgetById;

// Trả một ngân sách thuộc đúng người dùng cùng số tiền đã chi trong kỳ.
public sealed class GetBudgetByIdQueryHandler(
    ISpendWiseDbContext db,
    BudgetReader budgetReader) : IRequestHandler<GetBudgetByIdQuery, BudgetDto>
{
    public async Task<BudgetDto> Handle(GetBudgetByIdQuery query, CancellationToken cancellationToken)
    {
        // Truy vấn đồng thời theo Id và chủ sở hữu để không làm lộ dữ liệu người khác.
        var budget = await db.Budgets
            .AsNoTracking()
            .Include(candidate => candidate.Category)
            .Where(candidate => candidate.Id == query.BudgetId
                && candidate.OwnerUserId == query.UserId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("budget_not_found", "Budget not found.");
        return await budgetReader.ToDtoAsync(budget, cancellationToken);
    }
}
