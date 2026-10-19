using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Finance;
using SpendWise.Domain.Finance;

namespace SpendWise.Application.Features.Transfers;

// Lớp này gom kiểm tra request, replay idempotency và ánh xạ cho các luồng Transfer.

internal static class TransferFeatureSupport
{
    public static Task<Transfer?> FindReplayAsync(
        ISpendWiseDbContext db,
        Guid senderId,
        string operationScope,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        // Tìm transfer cũ theo người gửi, scope và key để retry không chuyển tiền lần hai.
        return db.Transfers
            .AsNoTracking()
            .Include(transfer => transfer.SenderUser)
            .Include(transfer => transfer.RecipientUser)
            .SingleOrDefaultAsync(
                transfer => transfer.SenderUserId == senderId
                    && transfer.IdempotencyScope == operationScope
                    && transfer.IdempotencyKey == idempotencyKey,
                cancellationToken);
    }

    public static void ValidateRequest(decimal amount, string idempotencyKey, string description)
    {
        // Lệnh chuyển tiền phải có số tiền dương và IdempotencyKey không vượt giới hạn.
        InputBoundary.PositiveMoney(amount);

        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 100)
        {
            throw new AppException(
                "invalid_idempotency_key",
                "Idempotency key is required and limited to 100 characters.");
        }


        InputBoundary.OptionalText(description, 500, "Description");
    }

    public static void ValidateWallet(Wallet wallet)
    {
        // Chỉ Wallet đang Active mới được dùng cho giao dịch chuyển tiền.
        if (wallet.Status != WalletStatus.Active)
        {
            throw new ConflictException(
                "wallet_unavailable",
                $"Wallet is {wallet.Status}.");
        }
    }

    public static void EnsureReplayMatches(
        Transfer existing,
        InternalTransferRequest request)
    {
        // Replay nội bộ phải khớp cả hai ví, số tiền và mô tả của request trước.
        var matches = existing.SourceWalletId == request.SourceWalletId
            && existing.DestinationWalletId == request.DestinationWalletId
            && existing.Amount == request.Amount
            && existing.Description == request.Description.Trim();
        if (!matches)
        {
            throw IdempotencyConflict();
        }
    }

    public static void EnsureReplayMatches(
        Transfer existing,
        P2PTransferRequest request)
    {
        // Replay P2P phải khớp ví nguồn, người nhận, số tiền và mô tả.
        var matches = existing.SourceWalletId == request.SourceWalletId
            && existing.RecipientUserId == request.RecipientUserId
            && existing.Amount == request.Amount
            && existing.Description == request.Description.Trim();
        if (!matches)
        {
            throw IdempotencyConflict();
        }
    }

    public static FinancialTransaction CreateHistory(
        Guid ownerUserId,
        Guid walletId,
        Guid ledgerTransactionId,
        Guid transferId,
        FinancialTransactionType type,
        decimal amount,
        string description,
        string counterparty,
        DateTime occurredAt)
    {
        // Tạo bản ghi lịch sử transfer cho một phía mà không posting thêm tiền.
        return new FinancialTransaction
        {
            OwnerUserId = ownerUserId,
            WalletId = walletId,
            LedgerTransactionId = ledgerTransactionId,
            RelatedBusinessId = transferId,
            Type = type,
            Amount = amount,
            Description = description,
            CounterpartyName = counterparty,
            OccurredAt = occurredAt
        };
    }

    public static TransferDto ToDto(Transfer transfer, bool replayed)
    {
        // Chuyển Transfer cùng tên hai bên và trạng thái replay sang DTO.
        return new TransferDto(
            transfer.Id,
            transfer.SenderUserId,
            transfer.SenderUser.FullName,
            transfer.RecipientUserId,
            transfer.RecipientUser.FullName,
            transfer.SourceWalletId,
            transfer.DestinationWalletId,
            transfer.Amount,
            transfer.Currency,
            transfer.Description,
            transfer.Status.ToString(),
            transfer.LedgerTransactionId,
            transfer.CompletedAt,
            replayed);
    }

    private static ConflictException IdempotencyConflict()
    {
        // Tạo lỗi Conflict thống nhất khi cùng key được dùng với payload khác.
        return new ConflictException(
            "idempotency_conflict",
            "The idempotency key was already used with different transfer details.");
    }
}
