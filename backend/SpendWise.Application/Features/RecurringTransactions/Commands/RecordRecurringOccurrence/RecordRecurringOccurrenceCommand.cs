using MediatR;
using SpendWise.Application.Finance;
using SpendWise.Application.Planning;

namespace SpendWise.Application.Features.RecurringTransactions.Commands.RecordRecurringOccurrence;

public sealed record RecordRecurringOccurrenceCommand(
    Guid UserId,
    Guid RecurringTransactionId,
    Guid OccurrenceId,
    RecordRecurringOccurrenceRequest Request) : IRequest<TransactionDto>;
