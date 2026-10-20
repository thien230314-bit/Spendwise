using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Finance;
using SpendWise.Application.Features;
using SpendWise.Domain.Finance;
using SpendWise.Domain.Identity;

namespace SpendWise.Application.Features.Transfers.Commands.P2PTransfer;

public sealed class P2PTransferCommandHandler(
    ISpendWiseDbContext db,
    ILedgerService ledger,
    IClock clock,
    IOperationPolicy policy,
    FinancialSecurityGuard security) : IRequestHandler<P2PTransferCommand, TransferDto>
{
    public async Task<TransferDto> Handle(
        P2PTransferCommand command,
        CancellationToken cancellationToken)
    {
        await policy.EnsureFeatureEnabledAsync("P2PTransfer", cancellationToken);
        await security.VerifyAsync(command.SenderUserId, command.Request.SecurityPin, cancellationToken);

        return await db.ExecuteInTransactionAsync(
            token => ExecuteP2PAsync(command.SenderUserId, command.Request, token),
            cancellationToken);
    }

    private async Task<TransferDto> ExecuteP2PAsync(
        Guid senderUserId,
        P2PTransferRequest request,
        CancellationToken cancellationToken)
    {
        TransferFeatureSupport.ValidateRequest(request.Amount, request.IdempotencyKey, request.Description);

        var senderUser = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == senderUserId, cancellationToken);
        if (senderUser?.Status == UserStatus.Restricted)
            throw new AppException("account_restricted", "Tài khoản đang bị hạn chế giao dịch tài chính.", 403);

        if (senderUserId == request.RecipientUserId)
        {
            throw new AppException(
                "self_transfer",
                "Không thể chuyển tiền P2P cho chính mình.");
        }

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

        const string operationScope = "p2p";
        var replay = await TransferFeatureSupport.FindReplayAsync(
            db,
            senderUserId,
            operationScope,
            normalizedRequest.IdempotencyKey,
            cancellationToken);

        if (replay is not null)
        {
            TransferFeatureSupport.EnsureReplayMatches(replay, normalizedRequest);
            return TransferFeatureSupport.ToDto(replay, true);
        }

        var sender = await db.Users.FindAsync([senderUserId], cancellationToken)
            ?? throw new NotFoundException("user_not_found", "Sender not found.");
        var recipient = await db.Users.FindAsync([normalizedRequest.RecipientUserId], cancellationToken)
            ?? throw new NotFoundException("recipient_not_found", "Recipient not found.");

        if (sender.Status != UserStatus.Active)
        {
            throw new ConflictException("sender_unavailable", "Sender account is not active.");
        }
        if (recipient.Status != UserStatus.Active)
        {
            throw new ConflictException("recipient_unavailable", "Recipient account is not active.");
        }

        var sourceWallet = await db.Wallets
            .Include(w => w.LedgerAccount)
            .SingleOrDefaultAsync(w => w.Id == normalizedRequest.SourceWalletId && w.OwnerUserId == senderUserId, cancellationToken)
            ?? throw new NotFoundException("wallet_not_found", "Source wallet not found.");

        TransferFeatureSupport.ValidateWallet(sourceWallet);

        // Recipient Main Wallet
        var destinationWallet = await db.Wallets
            .Include(w => w.LedgerAccount)
            .SingleOrDefaultAsync(w => w.OwnerUserId == recipient.Id && w.Type == WalletType.Main, cancellationToken);

        if (destinationWallet is null)
        {
            destinationWallet = await db.Wallets
                .Include(w => w.LedgerAccount)
                .Where(w => w.OwnerUserId == recipient.Id && w.Status == WalletStatus.Active)
                .OrderBy(w => w.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new ConflictException("recipient_wallet_unavailable", "Recipient does not have an active wallet.");
        }

        TransferFeatureSupport.ValidateWallet(destinationWallet);

        var transfer = new Transfer
        {
            SenderUserId = senderUserId,
            RecipientUserId = recipient.Id,
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
            db.ClearTrackedEntities();
            var concurrentReplay = await TransferFeatureSupport.FindReplayAsync(
                db,
                senderUserId,
                operationScope,
                normalizedRequest.IdempotencyKey,
                cancellationToken);

            if (concurrentReplay is null)
            {
                throw;
            }

            TransferFeatureSupport.EnsureReplayMatches(concurrentReplay, normalizedRequest);
            return TransferFeatureSupport.ToDto(concurrentReplay, true);
        }

        // Ledger posting: Debit source, Credit destination
        var posting = await ledger.PostAsync(
            sourceWallet.LedgerAccount.Id,
            destinationWallet.LedgerAccount.Id,
            normalizedRequest.Amount,
            "VND",
            "P2PTransfer",
            transfer.Id,
            $"user:{senderUserId:N}:{operationScope}",
            normalizedRequest.IdempotencyKey,
            transfer.Description,
            cancellationToken);

        transfer.Status = TransferStatus.Completed;
        transfer.LedgerTransactionId = posting.LedgerTransactionId;
        transfer.CompletedAt = clock.UtcNow;

        // History for sender (TransferOut)
        db.FinancialTransactions.Add(TransferFeatureSupport.CreateHistory(
            senderUserId,
            sourceWallet.Id,
            posting.LedgerTransactionId,
            transfer.Id,
            FinancialTransactionType.TransferOut,
            normalizedRequest.Amount,
            transfer.Description,
            recipient.FullName,
            clock.UtcNow));

        // History for recipient (TransferIn)
        db.FinancialTransactions.Add(TransferFeatureSupport.CreateHistory(
            recipient.Id,
            destinationWallet.Id,
            posting.LedgerTransactionId,
            transfer.Id,
            FinancialTransactionType.TransferIn,
            normalizedRequest.Amount,
            transfer.Description,
            sender.FullName,
            clock.UtcNow));

        await db.SaveChangesAsync(cancellationToken);

        transfer.SenderUser = sender;
        transfer.RecipientUser = recipient;
        return TransferFeatureSupport.ToDto(transfer, false);
    }
}
