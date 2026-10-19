using MediatR;
using SpendWise.Application.Finance;

namespace SpendWise.Application.Features.Transactions.Commands.UpdateTransaction;

// Route PATCH cũ được giữ lại nhưng bên trong luôn thực hiện correction có reversal.

public sealed record UpdateTransactionCommand(
    Guid UserId,
    Guid TransactionId,
    UpdateTransactionRequest Request) : IRequest<TransactionDto>;
