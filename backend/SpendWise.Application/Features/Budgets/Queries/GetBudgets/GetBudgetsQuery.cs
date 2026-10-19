using MediatR;
using SpendWise.Application.Planning;

namespace SpendWise.Application.Features.Budgets.Queries.GetBudgets;

// Query lấy cấu hình ngân sách của người dùng theo kỳ.

public sealed record GetBudgetsQuery(
    Guid UserId,
    int Year,
    int Month) : IRequest<IReadOnlyList<BudgetDto>>;
