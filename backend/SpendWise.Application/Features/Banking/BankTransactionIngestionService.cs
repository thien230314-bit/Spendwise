using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Banking;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Notifications;
using SpendWise.Domain.Banking;

namespace SpendWise.Application.Features.Banking;

// Dùng chung một luồng import cho IPN và manual sync để giữ cùng quy tắc idempotency.
public sealed class BankTransactionIngestionService(
    ISpendWiseDbContext db,
    INotificationService notifications,
    IBankDirectPaymentReconciler paymentReconciler,
    IClock clock) : IBankTransactionIngestionService
{
    // Overload giữ các unit test cũ độc lập với feature reconciliation mới.
    public BankTransactionIngestionService(
        ISpendWiseDbContext db,
        INotificationService notifications,
        IClock clock)
        : this(db, notifications, NoOpBankDirectPaymentReconciler.Instance, clock)
    {
    }

    public async Task<BankTransactionIngestionResult> IngestAsync(
        Guid bankConnectionId,
        ExternalBankTransactionData input,
        BankTransactionSource source,
        string? externalEventId,
        string? payloadHash,
        CancellationToken cancellationToken)
    {
        Validate(input);

        // Kiểm tra nhanh trước transaction; unique index vẫn là lớp bảo vệ cho race đồng thời.
        var replay = await db.BankTransactions
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Provider == BankProvider.SePayBankHub
                    && item.ExternalTransactionId == input.ExternalTransactionId,
                cancellationToken);
        if (replay is not null)
        {
            await RecordDuplicateDeliveryAsync(externalEventId, cancellationToken);
            return new BankTransactionIngestionResult(false, replay);
        }

        try
        {
            return await db.ExecuteInTransactionAsync(async ct =>
            {
                var connection = await db.BankConnections.SingleOrDefaultAsync(
                    item => item.Id == bankConnectionId,
                    ct) ?? throw BankingErrors.ConnectionNotFound();

                var transaction = new BankTransaction
                {
                    UserId = connection.UserId,
                    BankConnectionId = connection.Id,
                    Provider = BankProvider.SePayBankHub,
                    ExternalTransactionId = input.ExternalTransactionId.Trim(),
                    Direction = input.Direction,
                    Amount = input.Amount,
                    Currency = SpendWise.Domain.Common.Money.DefaultCurrency,
                    TransactionDate = input.TransactionDate.ToUniversalTime(),
                    Content = input.Content.Trim(),
                    ReferenceCode = input.ReferenceCode?.Trim(),
                    PaymentCode = input.PaymentCode?.Trim(),
                    Gateway = input.Gateway?.Trim(),
                    Source = source,
                    ImportedAt = clock.UtcNow
                };
                db.BankTransactions.Add(transaction);

                BankIntegrationEvent? integrationEvent = null;
                if (!string.IsNullOrWhiteSpace(externalEventId))
                {
                    // IPN audit chỉ giữ hash và metadata an toàn, không giữ Authorization header.
                    integrationEvent = new BankIntegrationEvent
                    {
                        Provider = BankProvider.SePayBankHub,
                        ExternalEventId = externalEventId,
                        ExternalTransactionId = input.ExternalTransactionId,
                        EventType = "BANK_TRANSACTION_IPN",
                        ExternalBankAccountId = connection.ExternalAccountId,
                        PayloadHash = payloadHash,
                        ProcessingStatus = BankIntegrationEventStatus.Received,
                        ReceivedAt = clock.UtcNow,
                        ProcessedAt = clock.UtcNow
                    };
                    db.BankIntegrationEvents.Add(integrationEvent);
                }

                await db.SaveChangesAsync(ct);
                var matched = await paymentReconciler.ReconcileAsync(transaction, ct);
                if (integrationEvent is not null)
                {
                    integrationEvent.ProcessingStatus = matched
                        ? BankIntegrationEventStatus.Processed
                        : BankIntegrationEventStatus.Unmatched;
                    await db.SaveChangesAsync(ct);
                }

                // Notification và Outbox được lưu trong cùng database transaction; SignalR chạy sau commit.
                await notifications.CreateAsync(
                    connection.UserId,
                    "BankTransactionImported",
                    "Giao dịch ngân hàng mới",
                    $"Đã nhập giao dịch {input.Direction.ToString().ToLowerInvariant()} {input.Amount:N0} VND.",
                    new { bankConnectionId = connection.Id, bankTransactionId = transaction.Id },
                    ct);

                return new BankTransactionIngestionResult(true, transaction);
            }, cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Hai request đồng thời có thể cùng vượt qua bước kiểm tra; unique index chỉ cho một row thắng.
            db.ClearTrackedEntities();
            var stored = await db.BankTransactions
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    item => item.Provider == BankProvider.SePayBankHub
                        && item.ExternalTransactionId == input.ExternalTransactionId,
                    cancellationToken);
            if (stored is null)
            {
                throw;
            }

            await RecordDuplicateDeliveryAsync(externalEventId, cancellationToken);
            return new BankTransactionIngestionResult(false, stored);
        }
    }

    private static void Validate(ExternalBankTransactionData input)
    {
        InputBoundary.RequiredText(input.ExternalTransactionId, 150, "Transaction id");
        InputBoundary.PositiveMoney(input.Amount);
        InputBoundary.OptionalText(input.Content, 1000, "Content");
        InputBoundary.OptionalText(input.ReferenceCode, 200, "Reference code");
        InputBoundary.OptionalText(input.PaymentCode, 200, "Payment code");
        InputBoundary.OptionalText(input.Gateway, 100, "Gateway");
        if (input.TransactionDate == default)
        {
            throw new AppException("bank_transaction_invalid", "Transaction date is invalid.");
        }
    }

    private async Task RecordDuplicateDeliveryAsync(
        string? externalEventId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(externalEventId))
        {
            return;
        }

        // Atomic UPDATE tránh hai duplicate đồng thời cùng đọc một count cũ rồi ghi đè lẫn nhau.
        _ = await BankIntegrationEventDuplicates.IncrementAsync(
            db,
            externalEventId,
            clock.UtcNow,
            cancellationToken);
    }

    private sealed class NoOpBankDirectPaymentReconciler : IBankDirectPaymentReconciler
    {
        public static readonly NoOpBankDirectPaymentReconciler Instance = new();
        public Task<bool> ReconcileAsync(BankTransaction transaction, CancellationToken cancellationToken) =>
            Task.FromResult(false);
    }
}
