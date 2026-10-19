using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Banking;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Domain.Banking;

namespace SpendWise.Application.Features.BankIntegration.Queries.GetBankIntegrationEvents;

public sealed class GetBankIntegrationEventsQueryHandler(
    ISpendWiseDbContext db) : IRequestHandler<GetBankIntegrationEventsQuery, IReadOnlyList<AdminBankIntegrationEventDto>>
{
    public async Task<IReadOnlyList<AdminBankIntegrationEventDto>> Handle(
        GetBankIntegrationEventsQuery query,
        CancellationToken cancellationToken)
    {
        var rows = db.BankIntegrationEvents.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (!Enum.TryParse<BankIntegrationEventStatus>(query.Status, true, out var status) || !Enum.IsDefined(status))
                throw new AppException("invalid_input", "Integration event status is invalid.");
            rows = rows.Where(item => item.ProcessingStatus == status);
        }
        if (!string.IsNullOrWhiteSpace(query.EventType))
        {
            var eventType = InputBoundary.OptionalText(query.EventType, 100, "Event type")!;
            rows = rows.Where(item => item.EventType == eventType);
        }
        if (!string.IsNullOrWhiteSpace(query.ExternalTransactionId))
        {
            var id = InputBoundary.OptionalText(query.ExternalTransactionId, 150, "External transaction id")!;
            rows = rows.Where(item => item.ExternalTransactionId == id);
        }
        if (!string.IsNullOrWhiteSpace(query.ExternalBankAccountId))
        {
            var id = InputBoundary.OptionalText(query.ExternalBankAccountId, 100, "External bank account id")!;
            rows = rows.Where(item => item.ExternalBankAccountId == id);
        }
        if (query.From is not null) rows = rows.Where(item => item.ReceivedAt >= query.From.Value.ToUniversalTime());
        if (query.To is not null) rows = rows.Where(item => item.ReceivedAt <= query.To.Value.ToUniversalTime());

        return await rows
            .OrderByDescending(item => item.ReceivedAt)
            .Take(500)
            .Select(item => new AdminBankIntegrationEventDto(
                item.Id,
                item.Provider.ToString(),
                item.ExternalEventId,
                item.ExternalTransactionId,
                item.EventType,
                item.ExternalBankAccountId,
                item.ProcessingStatus.ToString(),
                item.FailureCode,
                item.DuplicateDeliveryCount,
                item.LastDuplicateReceivedAt,
                item.ReceivedAt,
                item.ProcessedAt))
            .ToListAsync(cancellationToken);
    }
}
