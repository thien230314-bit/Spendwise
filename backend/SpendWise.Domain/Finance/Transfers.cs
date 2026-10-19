using SpendWise.Domain.Common;
using SpendWise.Domain.Identity;

namespace SpendWise.Domain.Finance;

// Các entity này lưu Transfer và MoneyRequest để giữ đầy đủ vòng đời chuyển tiền.

public enum TransferStatus { Pending, Processing, Completed, Failed, Cancelled, Reversed }
public enum MoneyRequestStatus { Pending, Paid, Rejected, Cancelled, Expired }

public sealed class Transfer : BaseEntity
{
    public Guid SenderUserId { get; set; }
    public User SenderUser { get; set; } = null!;
    public Guid RecipientUserId { get; set; }
    public User RecipientUser { get; set; } = null!;
    public Guid SourceWalletId { get; set; }
    public Guid DestinationWalletId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = Money.DefaultCurrency;
    public string Description { get; set; } = string.Empty;
    // Scope và key ngăn retry tạo lần chuyển tiền thứ hai.
    public string IdempotencyScope { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public TransferStatus Status { get; set; } = TransferStatus.Pending;
    // LedgerTransactionId nối Transfer với posting Nợ/Có đã hoàn tất.
    public Guid? LedgerTransactionId { get; set; }
    public Guid? MoneyRequestId { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public sealed class MoneyRequest : BaseEntity
{
    public Guid RequesterUserId { get; set; }
    public User RequesterUser { get; set; } = null!;
    public Guid PayerUserId { get; set; }
    public User PayerUser { get; set; } = null!;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = Money.DefaultCurrency;
    public string Description { get; set; } = string.Empty;
    // Chỉ yêu cầu Pending mới được Paid, Rejected hoặc Cancelled.
    // Cancelled/Rejected được giữ lại để yêu cầu tiền vẫn có thể kiểm toán.
    public MoneyRequestStatus Status { get; set; } = MoneyRequestStatus.Pending;
    // PaymentTransferId chỉ có khi yêu cầu đã được thanh toán bằng một Transfer thật.
    public Guid? PaymentTransferId { get; set; }
    public DateTime? ExpiresAt { get; set; }
}
