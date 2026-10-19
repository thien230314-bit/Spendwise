using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Planning;
using SpendWise.Domain.Planning;

namespace SpendWise.Application.Features.RecurringTransactions.Commands.PauseRecurringTransaction;

public sealed class PauseRecurringTransactionCommandHandler(ISpendWiseDbContext db, IClock clock)
    : IRequestHandler<PauseRecurringTransactionCommand, RecurringDto>
{
    public async Task<RecurringDto> Handle(PauseRecurringTransactionCommand command, CancellationToken cancellationToken)
    {
        var recurring = await db.RecurringTransactions.Include(item => item.Occurrences).SingleOrDefaultAsync(
            item => item.Id == command.RecurringTransactionId && item.OwnerUserId == command.UserId,
            cancellationToken)
            ?? throw new NotFoundException("recurring_not_found", "Recurring transaction not found.");
        if (recurring.Status == RecurringStatus.Cancelled)
        {
            throw new ConflictException("recurring_cancelled", "Cancelled recurring transaction cannot be paused.");
        }
        recurring.Status = RecurringStatus.Paused;
        await db.SaveChangesAsync(cancellationToken);
        return RecurringTransactionMappings.ToDto(recurring, clock.UtcNow);
    }
}
