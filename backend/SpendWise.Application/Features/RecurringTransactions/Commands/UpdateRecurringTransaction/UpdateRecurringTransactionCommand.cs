using MediatR;
using SpendWise.Application.Planning;

namespace SpendWise.Application.Features.RecurringTransactions.Commands.UpdateRecurringTransaction;

// Yêu cầu sửa cấu hình chạy trong tương lai của một giao dịch định kỳ.
public sealed record UpdateRecurringTransactionCommand(
    Guid UserId,
    Guid RecurringTransactionId,
    UpdateRecurringRequest Request) : IRequest<RecurringDto>;
