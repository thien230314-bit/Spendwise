using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Planning;

namespace SpendWise.Application.Features.Budgets.Queries.GetBudgets;

// Handler lấy cấu hình ngân sách của người dùng theo kỳ.

public sealed class GetBudgetsQueryHandler(
    ISpendWiseDbContext db,
    BudgetReader budgetReader) : IRequestHandler<GetBudgetsQuery, IReadOnlyList<BudgetDto>>
{
    public async Task<IReadOnlyList<BudgetDto>> Handle(
        GetBudgetsQuery query,
        CancellationToken cancellationToken)
    {
        // Lấy ngân sách đúng người dùng, năm và tháng rồi tính số tiền đã chi.
        if (query.Year is < 1 or > 9999 || query.Month is < 1 or > 12)
        {
            return [];
        }

        // BudgetReader dùng ranh giới lịch Việt Nam và cùng một công thức rollover cho mọi API.
        return await budgetReader.ReadAsync(
            db.Budgets.Where(budget => budget.OwnerUserId == query.UserId
                && budget.Year == query.Year
                && budget.Month == query.Month),
            cancellationToken);
    }
}
