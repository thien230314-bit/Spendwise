using MediatR;
using SpendWise.Application.Planning;

namespace SpendWise.Application.Features.Budgets.Commands.UpdateBudget;

// Yêu cầu sửa hạn mức của một ngân sách thuộc người dùng hiện tại.
public sealed record UpdateBudgetCommand(
    Guid UserId,
    Guid BudgetId,
    UpdateBudgetRequest Request) : IRequest<BudgetDto>;
