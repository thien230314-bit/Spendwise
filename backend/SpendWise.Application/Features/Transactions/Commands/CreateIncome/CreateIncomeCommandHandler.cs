using MediatR;
using SpendWise.Application.Finance;
using SpendWise.Domain.Finance;

namespace SpendWise.Application.Features.Transactions.Commands.CreateIncome;

// Handler ghi khoản thu vào ví và tạo posting ledger cân bằng.

public sealed class CreateIncomeCommandHandler(
    ManualTransactionCreator creator) : IRequestHandler<CreateIncomeCommand, TransactionDto>
{
    public Task<TransactionDto> Handle(
        CreateIncomeCommand command,
        CancellationToken cancellationToken)
    {
        // Ghi khoản thu qua ledger rồi lưu lịch sử Income trong cùng transaction.
        return creator.CreateAsync(
            command.UserId,
            command.Request,
            CategoryKind.Income,
            cancellationToken);
    }
}
