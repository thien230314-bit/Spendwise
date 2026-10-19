using MediatR;
using SpendWise.Application.Finance;
using SpendWise.Domain.Finance;

namespace SpendWise.Application.Features.Transactions.Commands.CreateExpense;

// Handler ghi khoản chi sau khi kiểm tra ví, số dư và quyền sở hữu.

public sealed class CreateExpenseCommandHandler(
    ManualTransactionCreator creator) : IRequestHandler<CreateExpenseCommand, TransactionDto>
{
    public Task<TransactionDto> Handle(
        CreateExpenseCommand command,
        CancellationToken cancellationToken)
    {
        // Ghi khoản chi qua ledger rồi lưu lịch sử Expense trong cùng transaction.
        return creator.CreateAsync(
            command.UserId,
            command.Request,
            CategoryKind.Expense,
            cancellationToken);
    }
}
