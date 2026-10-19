using SpendWise.Domain.Finance;

namespace SpendWise.Application.Features.Wallets;

// Factory này tạo Wallet cùng LedgerAccount ban đầu theo một quy tắc thống nhất.

public static class WalletFactory
{
    public static Wallet Create(
        Guid userId,
        string name,
        WalletType type,
        bool isDefault)
    {
        // Tạo Wallet cùng LedgerAccount liên kết nhưng chưa làm thay đổi số dư.
        var wallet = new Wallet
        {
            OwnerUserId = userId,
            Name = name,
            Type = type,
            IsDefault = isDefault
        };
        wallet.LedgerAccount = new LedgerAccount
        {
            Code = $"WALLET:{wallet.Id:N}",
            Name = name,
            Type = LedgerAccountType.Wallet,
            OwnerUserId = userId,
            WalletId = wallet.Id
        };

        return wallet;
    }
}
