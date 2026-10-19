using MediatR;
using SpendWise.Application.Banking;

namespace SpendWise.Application.Features.BankIntegration.Queries.GetBankIntegrationEvents;

public sealed record GetBankIntegrationEventsQuery(
    string? Status,
    string? EventType,
    string? ExternalTransactionId,
    string? ExternalBankAccountId,
    DateTime? From,
    DateTime? To) : IRequest<IReadOnlyList<AdminBankIntegrationEventDto>>;
