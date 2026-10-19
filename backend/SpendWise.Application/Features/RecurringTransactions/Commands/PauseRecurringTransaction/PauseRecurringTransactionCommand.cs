using MediatR;
using SpendWise.Application.Planning;

namespace SpendWise.Application.Features.RecurringTransactions.Commands.PauseRecurringTransaction;

public sealed record PauseRecurringTransactionCommand(Guid UserId, Guid RecurringTransactionId) : IRequest<RecurringDto>;
