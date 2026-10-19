using SpendWise.Application.Common.Interfaces;
using SpendWise.Domain.Finance;

namespace SpendWise.Application.Features.Wallets;

// Chuyển cờ default theo hai bước để không vi phạm unique index giữa một batch SQL.
public static class WalletDefaultCoordinator
{
    public static async Task PreparePromotionAsync(
        ISpendWiseDbContext db,
        IReadOnlyCollection<Wallet> wallets,
        Wallet? selectedDefault,
        CancellationToken cancellationToken)
    {
        var mustDemoteBeforePromotion = false;
        foreach (var wallet in wallets)
        {
            var shouldRemainDefault = selectedDefault is not null
                && wallet.Id == selectedDefault.Id;
            if (wallet.IsDefault && !shouldRemainDefault)
            {
                // Bỏ default cũ trước khi bật default mới để filtered index luôn hợp lệ.
                wallet.IsDefault = false;
                mustDemoteBeforePromotion = true;
            }
        }

        if (mustDemoteBeforePromotion)
        {
            // Save trung gian vẫn nằm trong transaction ngoài nên lỗi sau sẽ rollback toàn bộ.
            await db.SaveChangesAsync(cancellationToken);
        }

        foreach (var wallet in wallets)
        {
            wallet.IsDefault = selectedDefault is not null
                && wallet.Id == selectedDefault.Id;
        }
    }
}
