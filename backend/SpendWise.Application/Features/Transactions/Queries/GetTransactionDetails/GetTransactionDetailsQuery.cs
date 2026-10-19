using MediatR;
using SpendWise.Application.Finance;

namespace SpendWise.Application.Features.Transactions.Queries.GetTransactionDetails;

// Query lấy một giao dịch mà người dùng có quyền xem.

public sealed record GetTransactionDetailsQuery(
    Guid UserId,
    Guid TransactionId) : IRequest<TransactionDto>;
