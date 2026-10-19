using MediatR;
using SpendWise.Application.Planning;

namespace SpendWise.Application.Features.RecurringTransactions.Commands.CreateRecurringTransaction;

// Command tạo cấu hình giao dịch định kỳ mà không sửa lịch sử đã phát sinh.

public sealed record CreateRecurringTransactionCommand(
    Guid UserId,
    CreateRecurringRequest Request) : IRequest<RecurringDto>;
