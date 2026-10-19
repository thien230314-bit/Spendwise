using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Wallets;
using SpendWise.Domain.Finance;

namespace SpendWise.Application.Features.Admin.Wallets.Commands.FreezeWallet;

// Handler đóng băng ví theo quyền quản trị mà không xóa lịch sử tài chính.

public sealed class FreezeWalletCommandHandler(
    ISpendWiseDbContext db,
    AdminAuditWriter audit) : IRequestHandler<FreezeWalletCommand>
{
    public Task Handle(
        FreezeWalletCommand command,
        CancellationToken cancellationToken)
    {
        // Xác nhận quyền quản trị và lý do trước khi khóa hoặc mở lại ví.
        if (string.IsNullOrWhiteSpace(command.Request.Reason))
        {
            throw new AppException("reason_required", "Reason is required.");
        }

        // Đổi trạng thái, chuyển default và ghi audit trong cùng database transaction.
        return db.ExecuteInTransactionAsync(async token =>
        {
            var wallet = await db.Wallets.SingleOrDefaultAsync(
                candidate => candidate.Id == command.WalletId,
                token)
                ?? throw new NotFoundException("wallet_not_found", "Wallet not found.");
            if (wallet.Status == WalletStatus.Closed)
            {
                throw new ConflictException(
                    "wallet_closed",
                    "Closed wallet cannot be changed.");
            }

            var wallets = await db.Wallets
                .Where(candidate => candidate.OwnerUserId == wallet.OwnerUserId)
                .ToListAsync(token);
            wallet.Status = command.Request.Frozen
                ? WalletStatus.Frozen
                : WalletStatus.Active;

            // Khi mở khóa, giữ default Active khác; nếu chưa có thì dùng chính ví vừa mở.
            var selectedDefault = wallets
                .Where(candidate => candidate.Id != wallet.Id
                    && candidate.Status == WalletStatus.Active
                    && candidate.IsDefault)
                .OrderBy(candidate => candidate.CreatedAt)
                .ThenBy(candidate => candidate.Id)
                .FirstOrDefault();
            selectedDefault ??= command.Request.Frozen
                ? wallets
                    .Where(candidate => candidate.Status == WalletStatus.Active)
                    .OrderBy(candidate => candidate.CreatedAt)
                    .ThenBy(candidate => candidate.Id)
                    .FirstOrDefault()
                : wallet;
            await WalletDefaultCoordinator.PreparePromotionAsync(
                db,
                wallets,
                selectedDefault,
                token);

            audit.Add(
                command.AdminId,
                command.Request.Frozen ? "FREEZE_WALLET" : "UNFREEZE_WALLET",
                "Wallet",
                command.WalletId.ToString(),
                command.Request.Reason,
                null);
            await db.SaveChangesAsync(token);
            return true;
        }, cancellationToken);
    }
}
