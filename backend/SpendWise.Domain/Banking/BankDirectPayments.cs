using SpendWise.Domain.Common;
using SpendWise.Domain.Identity;

namespace SpendWise.Domain.Banking;

public enum BankDirectPaymentContextType
{
    Transfer,
    GroupContribution,
    GroupFundRequest,
    GroupFundRound,
    GroupRefund
}

public enum BankDirectPaymentStatus
{
    Pending,
    AwaitingReconciliation,
    Completed,
    Failed,
    Cancelled
}

// Lệnh thanh toán ngân hàng lưu snapshot để lịch sử không đổi khi tài khoản liên kết thay đổi.
public sealed class BankDirectPayment : BaseEntity
{
    public Guid SenderUserId { get; set; }
    public User SenderUser { get; set; } = null!;
    public Guid? RecipientUserId { get; set; }
    public User? RecipientUser { get; set; }
    public string RecipientDisplayNameSnapshot { get; set; } = string.Empty;
    public string? RecipientPhoneSnapshot { get; set; }
    public Guid RecipientBankConnectionId { get; set; }
    public BankConnection RecipientBankConnection { get; set; } = null!;
    public string BankBrandSnapshot { get; set; } = string.Empty;
    public string AccountHolderSnapshot { get; set; } = string.Empty;
    public string MaskedAccountSnapshot { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = Money.DefaultCurrency;
    public string? Note { get; set; }
    public string PaymentReference { get; set; } = string.Empty;
    public string ClientRequestKey { get; set; } = string.Empty;
    public BankDirectPaymentContextType ContextType { get; set; }
    public Guid? GroupId { get; set; }
    public Guid? FundRequestId { get; set; }
    public Guid? FundRequestRecipientId { get; set; }
    public Guid? FundRoundId { get; set; }
    public Guid? FundRoundMemberId { get; set; }
    public Guid? DeletionRequestId { get; set; }
    public Guid? GroupRefundId { get; set; }
    public DateTime? SenderConfirmedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? FailedAt { get; set; }
    public Guid? MatchedBankTransactionId { get; set; }
    public BankTransaction? MatchedBankTransaction { get; set; }
    public BankDirectPaymentStatus Status { get; set; } = BankDirectPaymentStatus.Pending;
}
