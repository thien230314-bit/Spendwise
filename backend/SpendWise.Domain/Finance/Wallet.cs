using SpendWise.Domain.Common;
using SpendWise.Domain.Identity;

namespace SpendWise.Domain.Finance;

// Entity Wallet giữ metadata, số dư khả dụng và tài khoản Ledger liên kết của một người dùng.

public enum WalletType { Main, Savings, Emergency }
public enum WalletStatus { Active, Frozen, Restricted, Closed }

public sealed class Wallet : BaseEntity
{
    public Guid OwnerUserId { get; set; }
    public User OwnerUser { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public WalletType Type { get; set; }
    public string Currency { get; set; } = Money.DefaultCurrency;
    // Status chuyển sang Closed thay vì xóa để giữ lịch sử tài chính.
    public WalletStatus Status { get; set; } = WalletStatus.Active;
    // AvailableBalance chỉ thay đổi qua posting tài chính, không qua UpdateWallet.
    public decimal AvailableBalance { get; set; }
    public bool IsDefault { get; set; }
    // RowVersion phát hiện hai thao tác tài chính cập nhật số dư cùng lúc.
    public byte[] RowVersion { get; set; } = [];
    public LedgerAccount LedgerAccount { get; set; } = null!;
}
