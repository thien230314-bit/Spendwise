using SpendWise.Domain.Common;

namespace SpendWise.Domain.Finance;

// Các entity này lưu tài khoản, giao dịch ghi sổ và từng bút toán bên Nợ hoặc bên Có.

public enum LedgerAccountType { Wallet, GoalReserve, SharedFund, ExternalSource, ExpenseClearing }
public enum LedgerEntryDirection { Debit, Credit }
public enum LedgerTransactionStatus { Pending, Completed, Failed, Reversed }

public sealed class LedgerAccount : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public LedgerAccountType Type { get; set; }
    public string Currency { get; set; } = Money.DefaultCurrency;
    // Balance là số dư chuẩn của tài khoản ledger; số dư Wallet được đồng bộ từ đây.
    public decimal Balance { get; set; }
    public Guid? WalletId { get; set; }
    public Wallet? Wallet { get; set; }
    public Guid? OwnerUserId { get; set; }
    public Guid? GroupId { get; set; }
    public bool IsSystem { get; set; }
    // RowVersion ngăn hai posting đồng thời ghi đè số dư mới của nhau.
    public byte[] RowVersion { get; set; } = [];
}

public sealed class LedgerTransaction : BaseEntity
{
    public string Reference { get; set; } = string.Empty;
    public string BusinessType { get; set; } = string.Empty;
    public Guid BusinessId { get; set; }
    // Scope và key bảo đảm một yêu cầu tài chính gửi lặp chỉ được posting một lần.
    public string? IdempotencyScope { get; set; }
    public string? IdempotencyKey { get; set; }
    public string Description { get; set; } = string.Empty;
    public LedgerTransactionStatus Status { get; set; } = LedgerTransactionStatus.Pending;
    public DateTime? CompletedAt { get; set; }
    // ReversesLedgerTransactionId liên kết posting đảo với posting gốc, không xóa lịch sử.
    public Guid? ReversesLedgerTransactionId { get; set; }
    // Các bút toán Nợ/Có phải có tổng tiền cân bằng.
    public List<LedgerEntry> Entries { get; set; } = [];
}

public sealed class LedgerEntry : BaseEntity
{
    public Guid LedgerTransactionId { get; set; }
    public LedgerTransaction LedgerTransaction { get; set; } = null!;
    public Guid LedgerAccountId { get; set; }
    public LedgerAccount LedgerAccount { get; set; } = null!;
    // Direction cho biết bút toán nằm bên Nợ hay bên Có.
    public LedgerEntryDirection Direction { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = Money.DefaultCurrency;
    public string Memo { get; set; } = string.Empty;
}
