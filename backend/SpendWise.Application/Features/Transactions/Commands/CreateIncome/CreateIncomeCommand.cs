using MediatR;
using SpendWise.Application.Finance;

namespace SpendWise.Application.Features.Transactions.Commands.CreateIncome;

// Command ghi khoản thu vào ví và tạo posting ledger cân bằng.

public sealed record CreateIncomeCommand(
    Guid UserId,
    CreateManualTransactionRequest Request) : IRequest<TransactionDto>;
