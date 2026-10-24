using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Banking;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Banking;
using SpendWise.Application.Notifications;
using SpendWise.Domain.Banking;

namespace SpendWise.Application.Features.BankIntegration.Commands.ProcessLifecycleEvent;

public sealed class ProcessLifecycleEventCommandHandler(
    ISpendWiseDbContext db,
    INotificationService notifications,
    IClock clock,
    IBankHubConfiguration configuration)
    : IRequestHandler<ProcessLifecycleEventCommand, IntegrationAcknowledgementDto>
{
    public async Task<IntegrationAcknowledgementDto> Handle(
        ProcessLifecycleEventCommand command,
        CancellationToken cancellationToken)
    {
        var request = command.Request;
        var eventId = InputBoundary.RequiredText(request.Xid, 150, "Event xid");
        var eventType = InputBoundary.RequiredText(request.Event, 100, "Event type").ToUpperInvariant();
        configuration.MarkWebhookReceived(clock.UtcNow);

        // Một atomic UPDATE vừa phát hiện replay vừa tăng bộ đếm mà vẫn giữ nguyên kết quả xử lý cũ.
        if (await BankIntegrationEventDuplicates.IncrementAsync(
                db,
                eventId,
                clock.UtcNow,
                cancellationToken))
        {
            return new IntegrationAcknowledgementDto(true);
        }

        var externalAccountId = MetadataString(request.Metadata, "bank_account_xid");
        var integrationEvent = new BankIntegrationEvent
        {
            Provider = BankProvider.SePayBankHub,
            ExternalEventId = eventId,
            EventType = eventType,
            ExternalBankAccountId = externalAccountId,
            PayloadHash = Hash(request),
            ProcessingStatus = BankIntegrationEventStatus.Received,
            ReceivedAt = clock.UtcNow
        };

        try
        {
            await db.ExecuteInTransactionAsync(async ct =>
            {
                db.BankIntegrationEvents.Add(integrationEvent);
                switch (eventType)
                {
                    case "BANK_ACCOUNT_LINKED":
                        await LinkAccountAsync(request.Metadata, integrationEvent, ct);
                        break;
                    case "BANK_ACCOUNT_UNLINKED":
                        await ChangeConnectionStatusAsync(
                            externalAccountId,
                            BankConnectionStatus.Unlinked,
                            "BankAccountUnlinked",
                            "Tài khoản ngân hàng đã hủy liên kết",
                            integrationEvent,
                            ct);
                        break;
                    case "BANK_ACCOUNT_INACTIVATED":
                    case "BANK_ACCOUNT_INACTIVED":
                        await ChangeConnectionStatusAsync(
                            externalAccountId,
                            BankConnectionStatus.Inactive,
                            "BankAccountInactive",
                            "Tài khoản ngân hàng cần được kích hoạt lại",
                            integrationEvent,
                            ct);
                        break;
                    case "BANK_ACCOUNT_REACTIVATED":
                        await ChangeConnectionStatusAsync(
                            externalAccountId,
                            BankConnectionStatus.Active,
                            "BankAccountLinked",
                            "Tài khoản ngân hàng đã hoạt động trở lại",
                            integrationEvent,
                            ct);
                        break;
                    case "LINK_TOKEN_EXPIRED":
                    case "LINK_SESSION_EXPIRED":
                        await UpdateSessionAsync(request.Metadata, BankLinkSessionStatus.Expired, integrationEvent, ct);
                        break;
                    case "LINK_SESSION_FAILED":
                    case "LINK_SESSION_ABANDONED":
                        await UpdateSessionAsync(request.Metadata, BankLinkSessionStatus.Failed, integrationEvent, ct);
                        break;
                    case "LINK_SESSION_COMPLETED":
                        await UpdateSessionAsync(request.Metadata, BankLinkSessionStatus.Completed, integrationEvent, ct);
                        break;
                    case "LINK_TOKEN_CREATED":
                    case "LINK_TOKEN_REVOKED":
                    case "LINK_SESSION_INITIALIZED":
                    case "LINK_SESSION_REINITIALIZED":
                    case "LINK_SESSION_STATE_CHANGED":
                        integrationEvent.ProcessingStatus = BankIntegrationEventStatus.Processed;
                        integrationEvent.ProcessedAt = clock.UtcNow;
                        break;
                    default:
                        // Provider có thể thêm event mới; xác thực xong thì ghi audit Ignored thay vì trả 500.
                        integrationEvent.ProcessingStatus = BankIntegrationEventStatus.Ignored;
                        integrationEvent.FailureCode = "unsupported_event";
                        integrationEvent.ProcessedAt = clock.UtcNow;
                        break;
                }

                await db.SaveChangesAsync(ct);
                return true;
            }, cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Request thua unique race chỉ tăng audit duplicate; toàn bộ side effect của nó đã rollback.
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

        return new IntegrationAcknowledgementDto(true);
    }

    private async Task LinkAccountAsync(
        JsonElement metadata,
        BankIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        var linkTokenXid = MetadataString(metadata, "link_token_xid");
        var bankAccountXid = MetadataString(metadata, "bank_account_xid");
        if (string.IsNullOrWhiteSpace(linkTokenXid) || string.IsNullOrWhiteSpace(bankAccountXid))
        {
            integrationEvent.ProcessingStatus = BankIntegrationEventStatus.Failed;
            integrationEvent.FailureCode = "bank_webhook_invalid";
            integrationEvent.ProcessedAt = clock.UtcNow;
            return;
        }

        // Chuỗi sở hữu duy nhất: link_token_xid -> BankLinkSession -> UserId.
        var session = await db.BankLinkSessions.SingleOrDefaultAsync(
            item => item.Provider == BankProvider.SePayBankHub
                && item.ExternalLinkTokenXid == linkTokenXid,
            cancellationToken);
        if (session is not null)
        {
            var userHasOtherActiveBank = await db.BankConnections.AnyAsync(
                item => item.UserId == session.UserId && item.Status == BankConnectionStatus.Active && item.ExternalAccountId != bankAccountXid,
                cancellationToken);
            if (userHasOtherActiveBank)
            {
                integrationEvent.ProcessingStatus = BankIntegrationEventStatus.Failed;
                integrationEvent.FailureCode = "user_already_has_active_bank";
                integrationEvent.ProcessedAt = clock.UtcNow;
                return;
            }
        }
        if (session is null)
        {
            // Không đoán người dùng dựa trên tên/số tài khoản khi thiếu session tương quan.
            integrationEvent.ProcessingStatus = BankIntegrationEventStatus.Unmatched;
            integrationEvent.FailureCode = "unknown_link_token_xid";
            integrationEvent.ProcessedAt = clock.UtcNow;
            return;
        }

        var activeWithOther = await db.BankConnections.AnyAsync(
            item => item.Provider == BankProvider.SePayBankHub
                && item.ExternalAccountId == bankAccountXid
                && item.UserId != session.UserId
                && item.Status == BankConnectionStatus.Active,
            cancellationToken);
        if (activeWithOther)
        {
            integrationEvent.ProcessingStatus = BankIntegrationEventStatus.Failed;
            integrationEvent.FailureCode = "bank_connection_owner_conflict";
            integrationEvent.ProcessedAt = clock.UtcNow;
            return;
        }

        var connection = await db.BankConnections.SingleOrDefaultAsync(
            item => item.UserId == session.UserId
                && item.Provider == BankProvider.SePayBankHub
                && item.ExternalAccountId == bankAccountXid,
            cancellationToken);

        var accountNumber = MetadataString(metadata, "account_number");
        if (connection is null)
        {
            connection = new BankConnection
            {
                UserId = session.UserId,
                Provider = BankProvider.SePayBankHub,
                ExternalAccountId = bankAccountXid
            };
            db.BankConnections.Add(connection);
        }
        connection.BankBrandName = MetadataString(metadata, "brand_name", "bank_brand_name") ?? "Ngân hàng liên kết";
        connection.AccountType = MetadataString(metadata, "account_type");
        connection.AccountHolderName = MetadataString(metadata, "account_holder_name", "account_name");
        connection.MaskedAccountNumber = Mask(accountNumber);
        connection.Status = BankConnectionStatus.Active;
        connection.LinkedAt = clock.UtcNow;
        connection.UnlinkedAt = null;

        var restrictedWallets = await db.Wallets
            .Where(w => w.OwnerUserId == session.UserId
                && w.Status == SpendWise.Domain.Finance.WalletStatus.Restricted
                && w.RestrictionReason == SpendWise.Domain.Finance.WalletRestrictionReason.NoActiveBank)
            .ToListAsync(cancellationToken);
        foreach (var rw in restrictedWallets)
        {
            rw.Status = SpendWise.Domain.Finance.WalletStatus.Active;
            rw.RestrictionReason = SpendWise.Domain.Finance.WalletRestrictionReason.None;
        }

        session.BankConnectionId = connection.Id;
        session.ExternalBankAccountId = bankAccountXid;
        session.Status = BankLinkSessionStatus.Completed;
        session.CompletedAt = clock.UtcNow;
        integrationEvent.ExternalBankAccountId = bankAccountXid;
        integrationEvent.ProcessingStatus = BankIntegrationEventStatus.Processed;
        integrationEvent.ProcessedAt = clock.UtcNow;

        await notifications.CreateAsync(
            session.UserId,
            "BankAccountLinked",
            "Liên kết ngân hàng thành công",
            $"Đã liên kết {connection.BankBrandName} {connection.MaskedAccountNumber}.",
            new { bankConnectionId = connection.Id },
            cancellationToken);
    }

    private async Task ChangeConnectionStatusAsync(
        string? externalAccountId,
        BankConnectionStatus status,
        string notificationType,
        string title,
        BankIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(externalAccountId))
        {
            integrationEvent.ProcessingStatus = BankIntegrationEventStatus.Failed;
            integrationEvent.FailureCode = "bank_webhook_invalid";
            integrationEvent.ProcessedAt = clock.UtcNow;
            return;
        }

        var matchingConnections = await db.BankConnections
            .Where(item => item.Provider == BankProvider.SePayBankHub
                && item.ExternalAccountId == externalAccountId)
            .ToListAsync(cancellationToken);

        if (matchingConnections.Count == 0)
        {
            integrationEvent.ProcessingStatus = BankIntegrationEventStatus.Unmatched;
            integrationEvent.FailureCode = "unknown_bank_account_xid";
            integrationEvent.ProcessedAt = clock.UtcNow;
            return;
        }

        var activeConnection = matchingConnections.SingleOrDefault(item => item.Status == BankConnectionStatus.Active);

        if (status is BankConnectionStatus.Unlinked or BankConnectionStatus.Inactive)
        {
            if (activeConnection is null)
            {
                // Historical / delayed webhook for an account that is already unlinked or inactive.
                // Do not crash or mutate other users' connections.
                integrationEvent.ProcessingStatus = BankIntegrationEventStatus.Processed;
                integrationEvent.FailureCode = "already_unlinked";
                integrationEvent.ProcessedAt = clock.UtcNow;
                return;
            }

            activeConnection.Status = status;
            if (status == BankConnectionStatus.Unlinked)
            {
                activeConnection.UnlinkedAt = clock.UtcNow;
            }

            var activeWallets = await db.Wallets
                .Where(w => w.OwnerUserId == activeConnection.UserId && w.Status == SpendWise.Domain.Finance.WalletStatus.Active)
                .ToListAsync(cancellationToken);
            foreach (var aw in activeWallets)
            {
                aw.Status = SpendWise.Domain.Finance.WalletStatus.Restricted;
                aw.RestrictionReason = SpendWise.Domain.Finance.WalletRestrictionReason.NoActiveBank;
            }

            integrationEvent.ProcessingStatus = BankIntegrationEventStatus.Processed;
            integrationEvent.ProcessedAt = clock.UtcNow;

            await notifications.CreateAsync(
                activeConnection.UserId,
                notificationType,
                title,
                $"Trạng thái {activeConnection.BankBrandName} {activeConnection.MaskedAccountNumber}: {status}.",
                new { bankConnectionId = activeConnection.Id, status = status.ToString() },
                cancellationToken);
            return;
        }

        if (status == BankConnectionStatus.Active)
        {
            if (activeConnection is not null)
            {
                integrationEvent.ProcessingStatus = BankIntegrationEventStatus.Processed;
                integrationEvent.ProcessedAt = clock.UtcNow;
                return;
            }

            var targetConnection = matchingConnections
                .OrderByDescending(c => c.LinkedAt ?? c.CreatedAt)
                .FirstOrDefault();

            if (targetConnection is null)
            {
                integrationEvent.ProcessingStatus = BankIntegrationEventStatus.Unmatched;
                integrationEvent.FailureCode = "unknown_bank_account_xid";
                integrationEvent.ProcessedAt = clock.UtcNow;
                return;
            }

            var userHasOtherActive = await db.BankConnections.AnyAsync(
                c => c.UserId == targetConnection.UserId && c.Status == BankConnectionStatus.Active && c.Id != targetConnection.Id,
                cancellationToken);
            if (userHasOtherActive)
            {
                integrationEvent.ProcessingStatus = BankIntegrationEventStatus.Failed;
                integrationEvent.FailureCode = "user_already_has_active_bank";
                integrationEvent.ProcessedAt = clock.UtcNow;
                return;
            }

            targetConnection.Status = BankConnectionStatus.Active;
            targetConnection.UnlinkedAt = null;

            var restrictedWallets = await db.Wallets
                .Where(w => w.OwnerUserId == targetConnection.UserId
                    && w.Status == SpendWise.Domain.Finance.WalletStatus.Restricted
                    && w.RestrictionReason == SpendWise.Domain.Finance.WalletRestrictionReason.NoActiveBank)
                .ToListAsync(cancellationToken);
            foreach (var rw in restrictedWallets)
            {
                rw.Status = SpendWise.Domain.Finance.WalletStatus.Active;
                rw.RestrictionReason = SpendWise.Domain.Finance.WalletRestrictionReason.None;
            }

            integrationEvent.ProcessingStatus = BankIntegrationEventStatus.Processed;
            integrationEvent.ProcessedAt = clock.UtcNow;

            await notifications.CreateAsync(
                targetConnection.UserId,
                notificationType,
                title,
                $"Trạng thái {targetConnection.BankBrandName} {targetConnection.MaskedAccountNumber}: {status}.",
                new { bankConnectionId = targetConnection.Id, status = status.ToString() },
                cancellationToken);
        }
    }

    private async Task UpdateSessionAsync(
        JsonElement metadata,
        BankLinkSessionStatus status,
        BankIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        var linkTokenXid = MetadataString(metadata, "link_token_xid");
        if (string.IsNullOrWhiteSpace(linkTokenXid))
        {
            integrationEvent.ProcessingStatus = BankIntegrationEventStatus.Unmatched;
            integrationEvent.FailureCode = "unknown_link_token_xid";
            integrationEvent.ProcessedAt = clock.UtcNow;
            return;
        }

        var session = await db.BankLinkSessions.SingleOrDefaultAsync(
            item => item.Provider == BankProvider.SePayBankHub
                && item.ExternalLinkTokenXid == linkTokenXid,
            cancellationToken);
        if (session is null)
        {
            integrationEvent.ProcessingStatus = BankIntegrationEventStatus.Unmatched;
            integrationEvent.FailureCode = "unknown_link_token_xid";
            integrationEvent.ProcessedAt = clock.UtcNow;
            return;
        }

        session.Status = status;
        if (status == BankLinkSessionStatus.Completed) session.CompletedAt = clock.UtcNow;
        if (status == BankLinkSessionStatus.Failed) session.FailedAt = clock.UtcNow;
        integrationEvent.ProcessingStatus = BankIntegrationEventStatus.Processed;
        integrationEvent.ProcessedAt = clock.UtcNow;
    }

    private static string? MetadataString(JsonElement element, params string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (names.Any(name => string.Equals(name, property.Name, StringComparison.OrdinalIgnoreCase))
                && property.Value.ValueKind == JsonValueKind.String)
            {
                return property.Value.GetString();
            }

            // Một số event bọc thông tin tài khoản trong object con metadata.data/bank_account.
            if (property.Value.ValueKind == JsonValueKind.Object)
            {
                var nested = MetadataString(property.Value, names);
                if (!string.IsNullOrWhiteSpace(nested)) return nested;
            }
        }
        return null;
    }

    private static string Mask(string? accountNumber)
    {
        if (string.IsNullOrWhiteSpace(accountNumber)) return "****";
        var normalized = accountNumber.Trim();
        return normalized.Length <= 4 ? $"****{normalized}" : $"****{normalized[^4..]}";
    }

    private static string Hash(SePayLifecycleEventRequest request)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(request);
        return Convert.ToHexString(SHA256.HashData(bytes));
    }
}
