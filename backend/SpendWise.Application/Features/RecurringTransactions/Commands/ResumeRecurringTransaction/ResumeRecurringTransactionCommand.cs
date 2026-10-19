using MediatR;
using SpendWise.Application.Planning;

namespace SpendWise.Application.Features.RecurringTransactions.Commands.ResumeRecurringTransaction;

public sealed record ResumeRecurringTransactionCommand(Guid UserId, Guid RecurringTransactionId) : IRequest<RecurringDto>;
