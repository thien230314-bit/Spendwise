using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Domain.Finance;

namespace SpendWise.Application.Features.Wallets.Commands.CloseWallet;

// Handler đóng ví khi thỏa điều kiện và giữ nguyên lịch sử tài chính.

public sealed class CloseWalletCommandHandler(
    ISpendWiseDbContext db) : IRequestHandler<CloseWalletCommand>
{
    public Task Handle(
        CloseWalletCommand command,
        CancellationToken cancellationToken)
    {
        // Đóng ví và chuyển default trong cùng transaction để không tạo trạng thái trung gian.
        return db.ExecuteInTransactionAsync(async token =>
        {
            var wallets = await db.Wallets
                .Where(candidate => candidate.OwnerUserId == command.UserId)
                .ToListAsync(token);
            var wallet = wallets.SingleOrDefault(candidate => candidate.Id == command.WalletId)
                ?? throw new NotFoundException("wallet_not_found", "Wallet not found.");

            // Ví Main phải luôn tồn tại để giữ điểm vào mặc định của tài khoản người dùng.
            if (wallet.Type == WalletType.Main)
            {
                throw new ConflictException(
                    "main_wallet_protected",
                    "The Main wallet cannot be closed.");
            }

            if (wallet.AvailableBalance != 0)
            {
                throw new ConflictException(
                    "wallet_not_empty",
                    "A wallet must have zero balance before closing.");
            }

            wallet.Status = WalletStatus.Closed;

            // Giữ default Active hiện có hoặc chọn ví Active đầu tiên làm fallback.
            var selectedDefault = wallets
                .Where(candidate => candidate.Status == WalletStatus.Active)
                .OrderByDescending(candidate => candidate.IsDefault)
                .ThenBy(candidate => candidate.CreatedAt)
                .ThenBy(candidate => candidate.Id)
                .FirstOrDefault();
            await WalletDefaultCoordinator.PreparePromotionAsync(
                db,
                wallets,
                selectedDefault,
                token);

            await db.SaveChangesAsync(token);
            return true;
        }, cancellationToken);
    }
}
