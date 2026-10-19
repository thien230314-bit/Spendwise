using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Planning;
using SpendWise.Domain.Planning;

namespace SpendWise.Application.Features.RecurringTransactions.Commands.ResumeRecurringTransaction;

public sealed class ResumeRecurringTransactionCommandHandler(
    ISpendWiseDbContext db,
    IClock clock) : IRequestHandler<ResumeRecurringTransactionCommand, RecurringDto>
{
    public async Task<RecurringDto> Handle(ResumeRecurringTransactionCommand command, CancellationToken cancellationToken)
    {
        var recurring = await db.RecurringTransactions.Include(item => item.Occurrences).SingleOrDefaultAsync(
            item => item.Id == command.RecurringTransactionId && item.OwnerUserId == command.UserId,
            cancellationToken)
            ?? throw new NotFoundException("recurring_not_found", "Recurring transaction not found.");
        if (recurring.Status == RecurringStatus.Cancelled)
        {
            throw new ConflictException("recurring_cancelled", "Cancelled recurring transaction cannot be resumed.");
        }
        if (recurring.NextRunAt <= clock.UtcNow)
        {
            // Lịch cũ được neo theo NextRunAt gốc trước khi bỏ qua các kỳ đã lỡ.
            if (recurring.AnchorDay is < 1 or > 31) recurring.AnchorDay = recurring.NextRunAt.Day;
            if (recurring.AnchorMonth is < 1 or > 12) recurring.AnchorMonth = recurring.NextRunAt.Month;
            recurring.NextRunAt = RecurringSchedule.AdvanceUntilFuture(
                recurring.NextRunAt,
                recurring.Frequency,
                recurring.AnchorDay,
                recurring.AnchorMonth,
                clock.UtcNow);
        }
        recurring.Status = RecurringStatus.Active;
        await db.SaveChangesAsync(cancellationToken);
        return RecurringTransactionMappings.ToDto(recurring, clock.UtcNow);
    }
}
