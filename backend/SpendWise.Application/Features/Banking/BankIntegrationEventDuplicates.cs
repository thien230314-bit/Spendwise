using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Domain.Banking;

namespace SpendWise.Application.Features.Banking;

// Cập nhật audit duplicate bằng một câu lệnh SQL để các request đồng thời không làm mất lượt đếm.
internal static class BankIntegrationEventDuplicates
{
    public static async Task<bool> IncrementAsync(
        ISpendWiseDbContext db,
        string externalEventId,
        DateTime receivedAt,
        CancellationToken cancellationToken)
    {
        var updatedRows = await db.BankIntegrationEvents
            .Where(item => item.Provider == BankProvider.SePayBankHub
                && item.ExternalEventId == externalEventId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(
                        item => item.DuplicateDeliveryCount,
                        item => item.DuplicateDeliveryCount + 1)
                    .SetProperty(item => item.LastDuplicateReceivedAt, receivedAt)
                    .SetProperty(item => item.UpdatedAt, receivedAt),
                cancellationToken);

        return updatedRows == 1;
    }
}
