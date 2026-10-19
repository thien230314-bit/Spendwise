using MediatR;
using SpendWise.Application.Planning;

namespace SpendWise.Application.Features.Budgets.Commands.CreateBudget;

// Yêu cầu tạo một cấu hình ngân sách mới cho người dùng hiện tại.
public sealed record CreateBudgetCommand(
    Guid UserId,
    CreateBudgetRequest Request) : IRequest<BudgetDto>;
