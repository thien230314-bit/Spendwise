using MediatR;

namespace SpendWise.Application.Features.Budgets.Commands.DeleteBudget;

// Command xóa cấu hình ngân sách mà không ảnh hưởng giao dịch trong tháng.

public sealed record DeleteBudgetCommand(
    Guid UserId,
    Guid BudgetId,
    string SecurityPin) : IRequest;
