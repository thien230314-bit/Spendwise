using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Finance;
using SpendWise.Application.Features;
using SpendWise.Domain.Finance;

namespace SpendWise.Application.Features.Transfers.Commands.TransferBetweenWallets;

// Chuyển tiền giữa hai ví cùng chủ sở hữu mà không ghi nhận Thu hoặc Chi.
public sealed class TransferBetweenWalletsCommandHandler(
    ISpendWiseDbContext db,
    ILedgerService ledger,
    IClock clock,
    IOperationPolicy policy,
    FinancialSecurityGuard security) : IRequestHandler<TransferBetweenWalletsCommand, TransferDto>
{
    public async Task<TransferDto> Handle(
        TransferBetweenWalletsCommand command,
        CancellationToken cancellationToken)
    {
        await security.VerifyAsync(command.UserId, command.Request.SecurityPin, cancellationToken);
        // Chuyển giữa hai ví cá nhân không phải thu nhập hoặc chi tiêu.
        return await db.ExecuteInTransactionAsync(
            token => TransferAsync(command.UserId, command.Request, token),
            cancellationToken);
    }

    private async Task<TransferDto> TransferAsync(
        Guid userId,
        InternalTransferRequest request,
        CancellationToken cancellationToken)
    {
        // Kiểm tra hai ví cùng chủ sở hữu rồi posting InternalTransfer, không ghi Thu/Chi.
        TransferFeatureSupport.ValidateRequest(request.Amount, request.IdempotencyKey, request.Description);
        // Chuẩn hóa key để retry không chuyển tiền lần thứ hai.
        var normalizedRequest = request with
        {
            IdempotencyKey = request.IdempotencyKey.Trim(),
            Description = request.Description?.Trim() ?? string.Empty
        };
        var minimum = await policy.GetDecimalAsync(
            "TransferMinimum",
            1_000m,
            cancellationToken);
        if (normalizedRequest.Amount < minimum)
        {
            throw new AppException(
                "transfer_below_minimum",
                $"Transfer amount must be at least {minimum:N0} VND.");
        }

        const string operationScope = "internal";
        var replay = await TransferFeatureSupport.FindReplayAsync(
            db,
            userId,
            operationScope,
            normalizedRequest.IdempotencyKey,
            cancellationToken);
        if (replay is not null)
        {
            // Cùng key và payload trả kết quả cũ; payload khác bị Conflict trong helper.
            TransferFeatureSupport.EnsureReplayMatches(replay, normalizedRequest);
            return TransferFeatureSupport.ToDto(replay, true);
        }

        if (normalizedRequest.SourceWalletId == normalizedRequest.DestinationWalletId)
        {
            // Ví nguồn và ví đích phải khác nhau dù cùng một chủ sở hữu.
            throw new AppException(
                "same_wallet",
                "Source and destination wallets must differ.");
        }

        // Một truy vấn lấy cả hai ví và bảo đảm chúng đều thuộc người dùng hiện tại.
        var wallets = await db.Wallets
            .Include(wallet => wallet.LedgerAccount)
            .Where(wallet => wallet.OwnerUserId == userId
                && (wallet.Id == normalizedRequest.SourceWalletId
                    || wallet.Id == normalizedRequest.DestinationWalletId))
            .ToListAsync(cancellationToken);
        var sourceWallet = wallets.SingleOrDefault(
            wallet => wallet.Id == normalizedRequest.SourceWalletId)
            ?? throw new NotFoundException("wallet_not_found", "Source wallet not found.");
        var destinationWallet = wallets.SingleOrDefault(
            wallet => wallet.Id == normalizedRequest.DestinationWalletId)
            ?? throw new NotFoundException("wallet_not_found", "Destination wallet not found.");
        TransferFeatureSupport.ValidateWallet(sourceWallet);
        TransferFeatureSupport.ValidateWallet(destinationWallet);

        var transfer = new Transfer
        {
            SenderUserId = userId,
            RecipientUserId = userId,
            SourceWalletId = sourceWallet.Id,
            DestinationWalletId = destinationWallet.Id,
            Amount = normalizedRequest.Amount,
            Description = normalizedRequest.Description.Trim(),
            IdempotencyScope = operationScope,
            IdempotencyKey = normalizedRequest.IdempotencyKey,
            Status = TransferStatus.Processing
        };
        db.Transfers.Add(transfer);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Một request cùng key có thể đã thắng unique index sau bước kiểm tra nhanh.
            db.ClearTrackedEntities();
            var concurrentReplay = await TransferFeatureSupport.FindReplayAsync(
                db,
                userId,
                operationScope,
                normalizedRequest.IdempotencyKey,
                cancellationToken);
            if (concurrentReplay is null)
            {
                // Lỗi database không liên quan idempotency phải giữ nguyên để được điều tra đúng.
                throw;
            }

            TransferFeatureSupport.EnsureReplayMatches(concurrentReplay, normalizedRequest);
            return TransferFeatureSupport.ToDto(concurrentReplay, true);
        }

        // Posting giảm ví nguồn, tăng ví đích và vẫn cân bằng Nợ/Có.
        var posting = await ledger.PostAsync(
            sourceWallet.LedgerAccount.Id,
            destinationWallet.LedgerAccount.Id,
            normalizedRequest.Amount,
            "VND",
            "InternalTransfer",
            transfer.Id,
            $"user:{userId:N}:{operationScope}",
            normalizedRequest.IdempotencyKey,
            transfer.Description,
            cancellationToken);

        transfer.Status = TransferStatus.Completed;
        transfer.LedgerTransactionId = posting.LedgerTransactionId;
        transfer.CompletedAt = clock.UtcNow;
        // Lịch sử mang loại InternalTransfer nên không làm sai báo cáo Thu/Chi.
        db.FinancialTransactions.Add(TransferFeatureSupport.CreateHistory(
            userId,
            sourceWallet.Id,
            posting.LedgerTransactionId,
            transfer.Id,
            FinancialTransactionType.InternalTransfer,
            normalizedRequest.Amount,
            transfer.Description,
            destinationWallet.Name,
            clock.UtcNow));
        await db.SaveChangesAsync(cancellationToken);

        var user = await db.Users.FindAsync([userId], cancellationToken)
            ?? throw new NotFoundException("user_not_found", "User not found.");
        transfer.SenderUser = user;
        transfer.RecipientUser = user;
        return TransferFeatureSupport.ToDto(transfer, false);
    }
}
