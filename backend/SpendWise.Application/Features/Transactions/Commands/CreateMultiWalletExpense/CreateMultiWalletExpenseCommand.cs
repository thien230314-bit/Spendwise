using MediatR;
using SpendWise.Application.Finance;

namespace SpendWise.Application.Features.Transactions.Commands.CreateMultiWalletExpense;

public sealed record CreateMultiWalletExpenseCommand(
    Guid UserId,
    CreateMultiWalletExpenseRequest Request) : IRequest<TransactionDto>;
