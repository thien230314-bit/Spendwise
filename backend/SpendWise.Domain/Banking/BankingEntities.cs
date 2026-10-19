using SpendWise.Domain.Common;
using SpendWise.Domain.Identity;
using SpendWise.Domain.Finance;

namespace SpendWise.Domain.Banking;

// Các enum mô tả nhà cung cấp và vòng đời dữ liệu ngân hàng bên ngoài SpendWise.
public enum BankProvider { SePayBankHub }
// Phân biệt không gian định danh tài khoản giữa Bank Hub và API v2 Test Mode của cùng SePay.
public enum SePayAccountEnvironment { BankHub, TestMode }
public enum BankConnectionStatus { Pending, Active, Inactive, Unlinked, Error }
public enum BankLinkPurpose { LinkBankAccount, UnlinkBankAccount, ReactivateBankAccount }
public enum BankLinkSessionStatus { Pending, Completed, Failed, Expired }
public enum BankTransactionDirection { Credit, Debit }
public enum BankTransactionSource { Webhook, ManualSync }
public enum BankIntegrationEventStatus { Received, Processed, Duplicate, Unmatched, Failed, Ignored }
public enum WalletTopUpStatus { Pending, Processing, Completed, Expired, Failed }

// BankConnection chỉ lưu liên kết tới tài khoản bên ngoài; đây không phải ví do SpendWise giữ tiền.
public sealed class BankConnection : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public BankProvider Provider { get; set; }
    public SePayAccountEnvironment AccountEnvironment { get; set; } = SePayAccountEnvironment.BankHub;
    // ExternalAccountId giữ ID thuộc đúng môi trường provider: bank_account_xid của Bank Hub hoặc account ID của Test Mode.
    public string ExternalAccountId { get; set; } = string.Empty;
    public string BankBrandName { get; set; } = string.Empty;
    public string? AccountType { get; set; }
    public string? AccountHolderName { get; set; }
    // Chỉ giữ số tài khoản đã che để không lưu dữ liệu nhạy cảm không cần thiết.
    public string MaskedAccountNumber { get; set; } = string.Empty;
    public BankConnectionStatus Status { get; set; } = BankConnectionStatus.Pending;
    public DateTime? LinkedAt { get; set; }
    public DateTime? UnlinkedAt { get; set; }
    public DateTime? LastSyncedAt { get; set; }
    public List<BankTransaction> Transactions { get; set; } = [];
}

// BankLinkSession liên kết Hosted Link với đúng người dùng đã đăng nhập.
public sealed class BankLinkSession : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public BankProvider Provider { get; set; }
    public SePayAccountEnvironment AccountEnvironment { get; set; } = SePayAccountEnvironment.BankHub;
    public BankLinkPurpose Purpose { get; set; }
    // Xid của link token là khóa tương quan webhook; raw link_token không được lưu.
    public string ExternalLinkTokenXid { get; set; } = string.Empty;
    public Guid? BankConnectionId { get; set; }
    public BankConnection? BankConnection { get; set; }
    public string? ExternalBankAccountId { get; set; }
    public BankLinkSessionStatus Status { get; set; } = BankLinkSessionStatus.Pending;
    public DateTime ExpiresAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? FailedAt { get; set; }
}

// WalletTopUp lưu yêu cầu nạp tiền đã được đối soát bằng giao dịch SePay Test Mode.
public sealed class WalletTopUp : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid BankConnectionId { get; set; }
    public BankConnection BankConnection { get; set; } = null!;
    public Guid WalletId { get; set; }
    public Wallet Wallet { get; set; } = null!;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = Money.DefaultCurrency;
    public string Reference { get; set; } = string.Empty;
    public WalletTopUpStatus Status { get; set; } = WalletTopUpStatus.Pending;
    public DateTime ExpiresAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    // ID giao dịch provider chỉ được gắn sau khi mọi trường đối soát khớp chính xác.
    public string? ProviderTransactionId { get; set; }
    public Guid? LedgerTransactionId { get; set; }
    public LedgerTransaction? LedgerTransaction { get; set; }
    public Guid? FinancialTransactionId { get; set; }
    public FinancialTransaction? FinancialTransaction { get; set; }
}

// BankTransaction là dữ liệu quan sát từ ngân hàng, tách biệt FinancialTransaction và Ledger.
public sealed class BankTransaction : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid BankConnectionId { get; set; }
    public BankConnection BankConnection { get; set; } = null!;
    public BankProvider Provider { get; set; }
    public string ExternalTransactionId { get; set; } = string.Empty;
    public BankTransactionDirection Direction { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = Money.DefaultCurrency;
    public DateTime TransactionDate { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? ReferenceCode { get; set; }
    public string? PaymentCode { get; set; }
    public string? Gateway { get; set; }
    public BankTransactionSource Source { get; set; }
    public DateTime ImportedAt { get; set; }
    // Phân loại chỉ bổ sung category; Direction gốc từ ngân hàng không được thay đổi.
    public Guid? CategoryId { get; set; }
    public Category? Category { get; set; }
}

// BankIntegrationEvent lưu trạng thái xử lý webhook/IPN mà không giữ secret hoặc payload thô.
public sealed class BankIntegrationEvent : BaseEntity
{
    public BankProvider Provider { get; set; }
    public string? ExternalEventId { get; set; }
    public string? ExternalTransactionId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string? ExternalBankAccountId { get; set; }
    public string? PayloadHash { get; set; }
    public BankIntegrationEventStatus ProcessingStatus { get; set; } = BankIntegrationEventStatus.Received;
    public string? FailureCode { get; set; }
    // Đếm các lần provider gửi lại cùng logical event mà không làm sai trạng thái xử lý ban đầu.
    public int DuplicateDeliveryCount { get; set; }
    // Lưu lần nhận duplicate gần nhất để Admin kiểm tra lịch sử retry của provider.
    public DateTime? LastDuplicateReceivedAt { get; set; }
    public DateTime ReceivedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
}
