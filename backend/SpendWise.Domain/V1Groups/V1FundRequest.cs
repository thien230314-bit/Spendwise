using SpendWise.Domain.Common;

namespace SpendWise.Domain.V1Groups;

public sealed class V1FundRequest : BaseEntity
{
    public Guid FundId { get; set; }
    public Guid RequestedByUserId { get; set; }
    public Guid? RequestedByMembershipId { get; set; }
    public Guid RecipientUserId { get; set; }
    public long OwnershipRevision { get; set; }
    public string Kind { get; set; } = string.Empty;
    public long AmountVnd { get; set; }
    public string Purpose { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string State { get; set; } = "AwaitingOwnerReview";
    public Guid? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewReason { get; set; }
    public DateTime? CancelledAt { get; set; }
}
