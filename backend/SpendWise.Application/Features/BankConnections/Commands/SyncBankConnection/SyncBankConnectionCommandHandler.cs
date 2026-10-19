using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Banking;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Banking;
using SpendWise.Domain.Banking;

namespace SpendWise.Application.Features.BankConnections.Commands.SyncBankConnection;

public sealed class SyncBankConnectionCommandHandler(
    ISpendWiseDbContext db,
    IBankHubGateway gateway,
    IBankHubConfiguration configuration,
    IBankTransactionIngestionService ingestion,
    IClock clock) : IRequestHandler<SyncBankConnectionCommand, BankSyncDto>
{
    public async Task<BankSyncDto> Handle(
        SyncBankConnectionCommand command,
        CancellationToken cancellationToken)
    {
        var connection = await db.BankConnections.SingleOrDefaultAsync(
            item => item.Id == command.BankConnectionId && item.UserId == command.UserId,
            cancellationToken) ?? throw BankingErrors.ConnectionNotFound();
        if (connection.Status != BankConnectionStatus.Active)
        {
            throw new ConflictException("bank_connection_not_active", "Only an active bank connection can be synchronized.");
        }
        if (connection.AccountEnvironment == SePayAccountEnvironment.TestMode)
            throw new ConflictException("bank_sync_not_supported", "Test Mode accounts are reconciled through the Test Mode flow.");

        configuration.EnsureAvailable();
        // Chồng lấn một ngày giúp lấy lại giao dịch đến trễ; idempotency loại bỏ bản đã có.
        var to = clock.UtcNow;
        var from = connection.LastSyncedAt?.AddDays(-1) ?? to.AddDays(-90);
        var imported = 0;
        var duplicates = 0;
        var page = 1;
        const int pageSize = 100;
        const int maximumPages = 20;

        do
        {
            var providerPage = await gateway.GetTransactionsAsync(
                connection.ExternalAccountId,
                from,
                to,
                page,
                pageSize,
                cancellationToken);
            if (providerPage.TotalPages > maximumPages)
            {
                // Không đánh dấu LastSyncedAt khi response vượt giới hạn an toàn của một lần sync.
                throw new AppException(
                    "bank_sync_failed",
                    "The requested sync window contains too many pages.",
                    503);
            }
            foreach (var transaction in providerPage.Items)
            {
                var result = await ingestion.IngestAsync(
                    connection.Id,
                    transaction,
                    BankTransactionSource.ManualSync,
                    null,
                    null,
                    cancellationToken);
                if (result.Created) imported++; else duplicates++;
            }

            if (page >= providerPage.TotalPages || page >= maximumPages)
            {
                break;
            }
            page++;
        } while (true);

        // Chỉ đánh dấu thành công sau khi mọi provider page và import đều hoàn tất.
        connection.LastSyncedAt = to;
        await db.SaveChangesAsync(cancellationToken);
        return new BankSyncDto(imported, duplicates, to);
    }
}
