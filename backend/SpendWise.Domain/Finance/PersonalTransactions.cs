using SpendWise.Domain.Common;

namespace SpendWise.Domain.Finance;

// Các entity này lưu Category và lịch sử Thu nhập, Chi tiêu hoặc Transfer của người dùng.

public enum CategoryKind { Income, Expense }
// Deposit/Withdrawal được giữ để đọc lịch sử tài chính bất biến đã tạo trước migration thay thế; Bank Hub mới không tạo hai loại này.
public enum FinancialTransactionType { Income, Expense, TransferOut, TransferIn, InternalTransfer, GoalContribution, GoalWithdrawal, GroupFundContribution, DebtSettlement, Deposit, Withdrawal, Refund, Reversal, BankTopUp }
public enum FinancialTransactionStatus { Pending, Processing, Completed, Failed, Cancelled, Reversed }

public sealed class Category : BaseEntity
{
    // OwnerUserId null biểu thị danh mục hệ thống dùng chung.
    public Guid? OwnerUserId { get; set; }
    public string Name { get; set; } = string.Empty;
    // Key ổn định để frontend nhận diện danh mục hệ thống qua nhiều môi trường.
    public string Key { get; set; } = string.Empty;
    public CategoryKind Kind { get; set; }
    public string? Icon { get; set; }
    public string? Color { get; set; }
    public bool IsSystem { get; set; }
    // Archive chỉ đặt IsActive=false để giao dịch lịch sử vẫn tham chiếu được.
    public bool IsActive { get; set; } = true;
}

public sealed class FinancialTransaction : BaseEntity
{
    public Guid OwnerUserId { get; set; }
    // WalletId chỉ có giá trị khi đúng một ví tài trợ giao dịch; giao dịch nhiều ví dùng Allocations.
    public Guid? WalletId { get; set; }
    public Wallet? Wallet { get; set; }
    public Guid? CategoryId { get; set; }
    public Category? Category { get; set; }
    // LedgerTransactionId nối lịch sử hiển thị với posting Nợ/Có bất biến.
    public Guid LedgerTransactionId { get; set; }
    public LedgerTransaction LedgerTransaction { get; set; } = null!;
    // RelatedBusinessId liên kết transfer, goal, debt hoặc nghiệp vụ nguồn nếu có.
    public Guid? RelatedBusinessId { get; set; }
    public FinancialTransactionType Type { get; set; }
    // Completed/Reversed là vòng đời tài chính; bản ghi hoàn tất không bị CRUD-delete.
    public FinancialTransactionStatus Status { get; set; } = FinancialTransactionStatus.Completed;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = Money.DefaultCurrency;
    public string Description { get; set; } = string.Empty;
    public string? CounterpartyName { get; set; }
    public DateTime OccurredAt { get; set; }
    // Chỉ giao dịch Thu/Chi thủ công mới có quyền tự sửa và tổng quyền có thể được Admin tăng sau này.
    public bool IsManual { get; set; }
    public int CorrectionAllowance { get; set; }
    public int CorrectionCount { get; set; }
    public List<EvidenceAttachment> Evidence { get; set; } = [];
    public List<TransactionCorrection> Corrections { get; set; } = [];
    public List<TransactionWalletAllocation> WalletAllocations { get; set; } = [];
}

public sealed class TransactionWalletAllocation : BaseEntity
{
    public Guid FinancialTransactionId { get; set; }
    public FinancialTransaction FinancialTransaction { get; set; } = null!;
    public Guid WalletId { get; set; }
    public Wallet Wallet { get; set; } = null!;
    public decimal Amount { get; set; }
}

public sealed class EvidenceAttachment : BaseEntity
{
    public Guid OwnerUserId { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long Length { get; set; }
    public string ContentHash { get; set; } = string.Empty;
    // Nội dung được lưu trong DB để tránh phụ thuộc đường dẫn local do client cung cấp.
    public byte[] Content { get; set; } = [];
    public Guid? FinancialTransactionId { get; set; }
    public FinancialTransaction? FinancialTransaction { get; set; }
    public Guid? TransactionCorrectionId { get; set; }
    public TransactionCorrection? TransactionCorrection { get; set; }
}

public sealed class TransactionCorrection : BaseEntity
{
    public Guid TransactionId { get; set; }
    public FinancialTransaction Transaction { get; set; } = null!;
    public Guid ActorUserId { get; set; }
    public int CorrectionNumber { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string OriginalValuesJson { get; set; } = string.Empty;
    public string CorrectedValuesJson { get; set; } = string.Empty;
    public FinancialTransactionType TransactionType { get; set; }
    public Guid? OriginalCategoryId { get; set; }
    public Guid? CorrectedCategoryId { get; set; }
    public Category? CorrectedCategory { get; set; }
    public string OriginalDescription { get; set; } = string.Empty;
    public string CorrectedDescription { get; set; } = string.Empty;
    public Guid? OriginalWalletId { get; set; }
    public Guid? CorrectedWalletId { get; set; }
    public decimal OriginalAmount { get; set; }
    public decimal CorrectedAmount { get; set; }
    public string? OriginalCounterpartyName { get; set; }
    public string? CorrectedCounterpartyName { get; set; }
    public DateTime OriginalOccurredAt { get; set; }
    public DateTime CorrectedOccurredAt { get; set; }
    public Guid OriginalLedgerTransactionId { get; set; }
    public Guid ReversalLedgerTransactionId { get; set; }
    public Guid CorrectedLedgerTransactionId { get; set; }
    public int AllowanceConsumed { get; set; } = 1;
    public List<EvidenceAttachment> Evidence { get; set; } = [];
    public List<TransactionCorrectionWalletAllocation> WalletAllocations { get; set; } = [];
}

public sealed class TransactionCorrectionWalletAllocation : BaseEntity
{
    public Guid TransactionCorrectionId { get; set; }
    public TransactionCorrection TransactionCorrection { get; set; } = null!;
    // IsOriginal phân biệt phân bổ trước và sau correction mà không sửa lịch sử cũ.
    public bool IsOriginal { get; set; }
    public Guid WalletId { get; set; }
    public Wallet Wallet { get; set; } = null!;
    public decimal Amount { get; set; }
}
