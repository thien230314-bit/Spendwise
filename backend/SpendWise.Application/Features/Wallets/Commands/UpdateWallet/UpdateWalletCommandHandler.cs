using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Finance;
using SpendWise.Domain.Finance;

namespace SpendWise.Application.Features.Wallets.Commands.UpdateWallet;

// Sửa tên, loại và cờ mặc định của ví nhưng không cho ghi trực tiếp số dư.
public sealed class UpdateWalletCommandHandler(
    ISpendWiseDbContext db) : IRequestHandler<UpdateWalletCommand, WalletDto>
{
    public Task<WalletDto> Handle(UpdateWalletCommand command, CancellationToken cancellationToken)
    {
        // Kiểm tra tên và loại ví; request không có trường số dư hoặc trạng thái.
        var name = InputBoundary.RequiredText(command.Request.Name, 100, "Wallet name");
        if (!WalletMappings.TryParseApiType(command.Request.Type, out var walletType))
        {
            throw new AppException("invalid_wallet", "Wallet name or type is invalid.");
        }

        // Đổi default và metadata trong cùng transaction để lỗi cuối không làm mất default cũ.
        return db.ExecuteInTransactionAsync(async token =>
        {
            // Nạp mọi ví để có thể chọn fallback theo thứ tự ổn định.
            var relevantWallets = await db.Wallets
                .Where(candidate => candidate.OwnerUserId == command.UserId)
                .ToListAsync(token);
            var wallet = relevantWallets.SingleOrDefault(candidate => candidate.Id == command.WalletId)
                ?? throw new NotFoundException("wallet_not_found", "Wallet not found.");
            if (wallet.Status == WalletStatus.Closed)
            {
                throw new ConflictException("wallet_closed", "A closed wallet cannot be updated.");
            }

            // Loại Main thuộc lifecycle hệ thống: không được bỏ khỏi ví Main hoặc gán cho ví khác.
            if ((wallet.Type == WalletType.Main && walletType != WalletType.Main)
                || (wallet.Type != WalletType.Main && walletType == WalletType.Main))
            {
                throw new ConflictException(
                    "main_wallet_type_protected",
                    "The Main wallet type cannot be changed or assigned to another wallet.");
            }

            if (command.Request.IsDefault && wallet.Status != WalletStatus.Active)
            {
                throw new ConflictException(
                    "wallet_unavailable",
                    "Only an active wallet can be the default wallet.");
            }

            Wallet? selectedDefault;
            if (command.Request.IsDefault)
            {
                // Ví đích trở thành default và thay thế mọi cờ default cũ.
                selectedDefault = wallet;
            }
            else
            {
                // Ưu tiên default Active khác; nếu dữ liệu cũ thiếu default thì chọn fallback ổn định.
                var otherActiveWallets = relevantWallets
                    .Where(candidate => candidate.Id != wallet.Id
                        && candidate.Status == WalletStatus.Active)
                    .OrderBy(candidate => candidate.CreatedAt)
                    .ThenBy(candidate => candidate.Id)
                    .ToList();
                selectedDefault = otherActiveWallets.FirstOrDefault(candidate => candidate.IsDefault)
                    ?? otherActiveWallets.FirstOrDefault();

                // Không được bỏ default của ví Active duy nhất vì request sẽ phá invariant.
                if (wallet.Status == WalletStatus.Active && selectedDefault is null)
                {
                    throw new ConflictException(
                        "default_wallet_required",
                        "At least one active wallet must remain the default wallet.");
                }
            }

            // Bỏ default cũ trước rồi mới bật selectedDefault để unique index không xung đột.
            await WalletDefaultCoordinator.PreparePromotionAsync(
                db,
                relevantWallets,
                selectedDefault,
                token);

            // Chỉ sửa metadata; AvailableBalance, Status và LedgerAccount giữ nguyên.
            wallet.Name = name;
            wallet.Type = walletType;

            // Transaction ngoài bảo đảm chuyển default và metadata cùng thành công hoặc rollback.
            await db.SaveChangesAsync(token);
            return WalletMappings.ToDto(wallet);
        }, cancellationToken);
    }
}
