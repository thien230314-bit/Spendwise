using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Domain.Planning;

namespace SpendWise.Application.Features.RecurringTransactions.Commands.DeleteRecurringTransaction;

// Xóa lịch chạy tương lai nhưng giữ nguyên mọi giao dịch tài chính đã phát sinh.
public sealed class DeleteRecurringTransactionCommandHandler(
    ISpendWiseDbContext db) : IRequestHandler<DeleteRecurringTransactionCommand>
{
    public async Task Handle(
        DeleteRecurringTransactionCommand command,
        CancellationToken cancellationToken)
    {
        // Lọc theo chủ sở hữu để không thể xóa lịch định kỳ của người khác.
        var recurring = await db.RecurringTransactions.SingleOrDefaultAsync(
            candidate => candidate.Id == command.RecurringTransactionId
                && candidate.OwnerUserId == command.UserId,
            cancellationToken)
            ?? throw new NotFoundException("recurring_not_found", "Recurring transaction not found.");

        // Soft-disable giữ occurrence và FinancialTransaction đã ghi nhận làm lịch sử.
        recurring.Status = RecurringStatus.Cancelled;
        await db.SaveChangesAsync(cancellationToken);
    }
}
