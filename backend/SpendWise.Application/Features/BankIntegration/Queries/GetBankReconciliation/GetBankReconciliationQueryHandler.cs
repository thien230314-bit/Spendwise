using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Banking;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Domain.Banking;

namespace SpendWise.Application.Features.BankIntegration.Queries.GetBankReconciliation;

public sealed class GetBankReconciliationQueryHandler(
    ISpendWiseDbContext db) : IRequestHandler<GetBankReconciliationQuery, BankReconciliationDto>
{
    public async Task<BankReconciliationDto> Handle(
        GetBankReconciliationQuery query,
        CancellationToken cancellationToken)
    {
        // Mỗi Count chạy ở SQL Server và chỉ đọc các chỉ số cần cho trang đối soát.
        return new BankReconciliationDto(
            await db.BankConnections.CountAsync(x => x.Status == BankConnectionStatus.Active, cancellationToken),
            await db.BankConnections.CountAsync(x => x.Status == BankConnectionStatus.Inactive, cancellationToken),
            await db.BankConnections.CountAsync(x => x.Status == BankConnectionStatus.Unlinked, cancellationToken),
            await db.BankIntegrationEvents.CountAsync(x => x.ProcessingStatus == BankIntegrationEventStatus.Processed, cancellationToken),
            await db.BankIntegrationEvents.SumAsync(x => x.DuplicateDeliveryCount, cancellationToken),
            await db.BankIntegrationEvents.CountAsync(x => x.ProcessingStatus == BankIntegrationEventStatus.Unmatched, cancellationToken),
            await db.BankIntegrationEvents.CountAsync(x => x.ProcessingStatus == BankIntegrationEventStatus.Failed, cancellationToken),
            await db.BankTransactions.CountAsync(cancellationToken));
    }
}
