using MediatR;
using SpendWise.Application.Banking;

namespace SpendWise.Application.Features.BankTransactions.Commands.ReclassifyBankTransaction;

public sealed record ReclassifyBankTransactionCommand(
    Guid UserId,
    Guid BankTransactionId,
    ReclassifyBankTransactionRequest Request) : IRequest<BankTransactionDto>;
