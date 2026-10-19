using MediatR;
using SpendWise.Application.Finance;

namespace SpendWise.Application.Features.Transactions.Commands.CreateExpense;

// Command ghi khoản chi sau khi kiểm tra ví, số dư và quyền sở hữu.

public sealed record CreateExpenseCommand(
    Guid UserId,
    CreateManualTransactionRequest Request) : IRequest<TransactionDto>;
