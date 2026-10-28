using SpendWise.Domain.Common;
using SpendWise.Domain.Identity;

namespace SpendWise.Domain.Finance;

public enum TransactionAuthorizationStatus
{
    Pending,
    Authorized,
    Executed,
    Cancelled,
    Expired
}

public sealed class TransactionAuthorization : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public string TransactionType { get; set; } = string.Empty; // "InternalTransfer", "P2PTransfer", "FundExpense", "TopUp", "Withdrawal"
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "VND";
    public string SourceId { get; set; } = string.Empty;
    public string DestinationId { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;

    public string Challenge { get; set; } = string.Empty; // Base64Url random challenge
    public TransactionAuthorizationStatus Status { get; set; } = TransactionAuthorizationStatus.Pending;
    public string? CredentialId { get; set; }
    public string? ExecutedResultJson { get; set; }

    public DateTime ExpiresAt { get; set; }
    public DateTime? AuthorizedAt { get; set; }
    public DateTime? ExecutedAt { get; set; }
}
