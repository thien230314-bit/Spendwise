using MediatR;
using SpendWise.Application.Planning;

namespace SpendWise.Application.Features.Budgets.Queries.GetBudgetById;

// Lấy một ngân sách theo mã và phạm vi sở hữu của người dùng.
public sealed record GetBudgetByIdQuery(Guid UserId, Guid BudgetId) : IRequest<BudgetDto>;
