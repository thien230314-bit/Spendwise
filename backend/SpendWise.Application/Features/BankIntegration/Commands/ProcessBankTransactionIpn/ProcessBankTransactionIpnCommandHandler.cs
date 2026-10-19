using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Banking;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Banking;
using SpendWise.Domain.Banking;

namespace SpendWise.Application.Features.BankIntegration.Commands.ProcessBankTransactionIpn;

public sealed class ProcessBankTransactionIpnCommandHandler(
    ISpendWiseDbContext db,
    IBankTransactionIngestionService ingestion,
    IClock clock,
    IBankHubConfiguration configuration)
    : IRequestHandler<ProcessBankTransactionIpnCommand, IntegrationAcknowledgementDto>
{
    public async Task<IntegrationAcknowledgementDto> Handle(
        ProcessBankTransactionIpnCommand command,
        CancellationToken cancellationToken)
    {
        configuration.MarkWebhookReceived(clock.UtcNow);
        var request = command.Request;
        var transactionId = InputBoundary.RequiredText(request.TransactionId, 140, "Transaction id");
        var bankAccountXid = InputBoundary.RequiredText(request.BankAccountXid, 100, "Bank account xid");
        InputBoundary.PositiveMoney(request.Amount);
        if (!TryDirection(request.TransferType, out var direction))
        {
            throw new AppException("bank_transaction_invalid", "Transfer type must be credit or debit.");
        }
        if (!DateTimeOffset.TryParse(
                request.TransactionDate,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var transactionDate))
        {
            throw new AppException("bank_transaction_invalid", "Transaction date is invalid.");
        }

        var eventId = $"IPN:{transactionId}";
        // Replay chỉ tăng metadata audit bằng atomic UPDATE và không thay đổi kết quả Processed/Unmatched cũ.
        if (await BankIntegrationEventDuplicates.IncrementAsync(
                db,
                eventId,
                clock.UtcNow,
                cancellationToken))
        {
            return new IntegrationAcknowledgementDto(true);
        }

        // bank_account_xid là khóa duy nhất để tìm connection và User; không dùng account_number.
        var connection = await db.BankConnections.SingleOrDefaultAsync(
            item => item.Provider == BankProvider.SePayBankHub
                && item.ExternalAccountId == bankAccountXid,
            cancellationToken);
        if (connection is null || connection.Status != BankConnectionStatus.Active)
        {
            await RecordUnmatchedAsync(
                eventId,
                transactionId,
                bankAccountXid,
                connection is null ? "unknown_bank_account_xid" : "bank_connection_not_active",
                Hash(request),
                cancellationToken);
            return new IntegrationAcknowledgementDto(true);
        }

        // Ingestion chỉ tạo BankTransaction ngoài; không truy cập Wallet, Ledger hoặc FinancialTransaction.
        await ingestion.IngestAsync(
            connection.Id,
            new ExternalBankTransactionData(
                transactionId,
                direction,
                request.Amount,
                transactionDate.UtcDateTime,
                InputBoundary.OptionalText(request.Content, 1000, "Content") ?? string.Empty,
                InputBoundary.OptionalText(request.ReferenceCode, 200, "Reference code"),
                InputBoundary.OptionalText(request.PaymentCode, 200, "Payment code"),
                InputBoundary.OptionalText(request.Gateway, 100, "Gateway")),
            BankTransactionSource.Webhook,
            eventId,
            Hash(request),
            cancellationToken);
        return new IntegrationAcknowledgementDto(true);
    }

    private async Task RecordUnmatchedAsync(
        string eventId,
        string transactionId,
        string bankAccountXid,
        string failureCode,
        string payloadHash,
        CancellationToken cancellationToken)
    {
        db.BankIntegrationEvents.Add(new BankIntegrationEvent
        {
            Provider = BankProvider.SePayBankHub,
            ExternalEventId = eventId,
            ExternalTransactionId = transactionId,
            EventType = "BANK_TRANSACTION_IPN",
            ExternalBankAccountId = bankAccountXid,
            PayloadHash = payloadHash,
            ProcessingStatus = BankIntegrationEventStatus.Unmatched,
            FailureCode = failureCode,
            ReceivedAt = clock.UtcNow,
            ProcessedAt = clock.UtcNow
        });
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Một unmatched IPN thua unique race chỉ tăng bộ đếm, không đổi trạng thái Unmatched ban đầu.
            db.ClearTrackedEntities();
            if (!await BankIntegrationEventDuplicates.IncrementAsync(
                    db,
                    eventId,
                    clock.UtcNow,
                    cancellationToken))
            {
                throw;
            }
        }
    }

    private static bool TryDirection(string? value, out BankTransactionDirection direction)
    {
        direction = value?.Trim().ToLowerInvariant() switch
        {
            "credit" => BankTransactionDirection.Credit,
            "debit" => BankTransactionDirection.Debit,
            _ => default
        };
        return value?.Trim().ToLowerInvariant() is "credit" or "debit";
    }

    private static string Hash(SePayBankTransactionIpnRequest request) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(request)));
}
