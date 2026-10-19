using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Finance;
using SpendWise.Domain.Finance;

namespace SpendWise.Application.Features.Wallets.Commands.CreateWallet;

// Handler tạo ví mới và chọn ví mặc định trong cùng database transaction.

public sealed class CreateWalletCommandHandler(
    ISpendWiseDbContext db) : IRequestHandler<CreateWalletCommand, WalletDto>
{
    private const int MaximumPersonalWallets = 3;

    public Task<WalletDto> Handle(
        CreateWalletCommand command,
        CancellationToken cancellationToken)
    {
        // Chuyển loại ví từ request sang enum của Domain.
        if (!WalletMappings.TryParseApiType(command.Request.Type, out var walletType))
        {
            throw new AppException("invalid_wallet_type", "Wallet type is invalid.");
        }

        if (string.IsNullOrWhiteSpace(command.Request.Name) || command.Request.Name.Length > 100)
        {
            throw new AppException(
                "invalid_wallet_name",
                "Wallet name is required and limited to 100 characters.");
        }

        // Đổi ví mặc định và tạo ví mới trong cùng transaction để lỗi ghi cuối rollback toàn bộ.
        return db.ExecuteInTransactionAsync(async token =>
        {
            // Khóa hàng User trong transaction để hai request đồng thời không cùng vượt giới hạn.
            if (await LockOwnerAsync(command.UserId, token) != 1)
            {
                throw new NotFoundException("user_not_found", "User not found.");
            }

            // Main là ví hệ thống tạo khi đăng ký, client không được tự tạo loại ví này.
            if (walletType == WalletType.Main)
            {
                throw new ConflictException(
                    "main_wallet_exists",
                    "The Main wallet is created by the system during registration.");
            }

            // Nạp mọi ví của người dùng để sửa cờ default trong cùng một lần lưu.
            var existingWallets = await db.Wallets
                .Where(wallet => wallet.OwnerUserId == command.UserId)
                .ToListAsync(token);

            // Ví đã đóng vẫn được tính; SharedFund không thuộc DbSet Wallets nên không ảnh hưởng giới hạn.
            if (existingWallets.Count >= MaximumPersonalWallets)
            {
                throw new ConflictException(
                    "wallet_limit_reached",
                    "A user can have at most three personal wallets.");
            }

            // Ví mới phải làm default khi request yêu cầu hoặc chưa có default đang Active.
            var retainedDefault = existingWallets
                .Where(wallet => wallet.Status == WalletStatus.Active && wallet.IsDefault)
                .OrderBy(wallet => wallet.CreatedAt)
                .ThenBy(wallet => wallet.Id)
                .FirstOrDefault();
            var makeDefault = command.Request.IsDefault || retainedDefault is null;

            // Bỏ default cũ trong một Save riêng trước khi tạo default mới.
            await WalletDefaultCoordinator.PreparePromotionAsync(
                db,
                existingWallets,
                makeDefault ? null : retainedDefault,
                token);

            // WalletFactory tạo ví cùng LedgerAccount nhưng không đụng đến số dư ví khác.
            var wallet = WalletFactory.Create(
                command.UserId,
                command.Request.Name.Trim(),
                walletType,
                makeDefault);
            db.Wallets.Add(wallet);

            // Transaction ngoài giữ cả bước bỏ default và tạo ví mới có tính nguyên tử.
            await db.SaveChangesAsync(token);

            return WalletMappings.ToDto(wallet);
        }, cancellationToken);
    }

    private async Task<int> LockOwnerAsync(Guid userId, CancellationToken cancellationToken)
    {
        // EF InMemory chỉ dùng cho test đơn luồng và không hỗ trợ ExecuteUpdate.
        if (db is DbContext context
            && string.Equals(
                context.Database.ProviderName,
                "Microsoft.EntityFrameworkCore.InMemory",
                StringComparison.Ordinal))
        {
            return await db.Users.AnyAsync(user => user.Id == userId, cancellationToken) ? 1 : 0;
        }

        // UPDATE không đổi dữ liệu nhưng giữ khóa hàng tới khi transaction commit.
        return await db.Users
            .Where(user => user.Id == userId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(user => user.UpdatedAt, user => user.UpdatedAt),
                cancellationToken);
    }
}
