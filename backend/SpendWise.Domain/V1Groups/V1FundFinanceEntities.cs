using SpendWise.Domain.Common;

namespace SpendWise.Domain.V1Groups;

public sealed class V1FundTreasury : BaseEntity
{
    public Guid FundId { get; set; }
    public Guid LedgerAccountId { get; set; }
    public long Revision { get; set; }
    public long? TargetVnd { get; set; }
    public string MembershipSignature { get; set; } = string.Empty;
}

public sealed class V1FundObligation : BaseEntity
{
    public Guid FundId { get; set; }
    public long Revision { get; set; }
    public Guid MembershipId { get; set; }
    public Guid UserId { get; set; }
    public int Seat { get; set; }
    public long RequiredVnd { get; set; }
}

public sealed class V1FundContribution : BaseEntity
{
    public Guid FundId { get; set; }
    public Guid UserId { get; set; }
    public Guid MembershipId { get; set; }
    public Guid WalletId { get; set; }
    public long AmountVnd { get; set; }
    public long? ObligationRevision { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public Guid LedgerTransactionId { get; set; }
}
