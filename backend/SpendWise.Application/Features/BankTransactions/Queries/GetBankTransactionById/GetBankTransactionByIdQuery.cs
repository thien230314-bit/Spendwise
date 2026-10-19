using MediatR;
using SpendWise.Application.Banking;

namespace SpendWise.Application.Features.BankTransactions.Queries.GetBankTransactionById;

public sealed record GetBankTransactionByIdQuery(Guid UserId, Guid BankTransactionId) : IRequest<BankTransactionDto>;
