using MediatR;

namespace SpendWise.Application.Features.RecurringTransactions.Commands.DeleteRecurringTransaction;

// Xóa định nghĩa định kỳ để dừng các lần chạy trong tương lai.
public sealed record DeleteRecurringTransactionCommand(
    Guid UserId,
    Guid RecurringTransactionId) : IRequest;
