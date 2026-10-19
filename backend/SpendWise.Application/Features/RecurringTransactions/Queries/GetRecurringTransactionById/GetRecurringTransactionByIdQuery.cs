using MediatR;
using SpendWise.Application.Planning;

namespace SpendWise.Application.Features.RecurringTransactions.Queries.GetRecurringTransactionById;

// Lấy một định nghĩa giao dịch định kỳ theo Id và chủ sở hữu.
public sealed record GetRecurringTransactionByIdQuery(
    Guid UserId,
    Guid RecurringTransactionId) : IRequest<RecurringDto>;
