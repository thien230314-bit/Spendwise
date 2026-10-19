using SpendWise.Domain.Common;

namespace SpendWise.Domain.V1Groups;

public sealed class V1ActivityProposal : BaseEntity
{
    public Guid FundId { get; set; }
    public Guid ProposedByUserId { get; set; }
    public long OwnershipRevision { get; set; }
    public long TargetVnd { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public DateTime OpenedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public DateTime? ReminderSentAt { get; set; }
    public string State { get; set; } = "Voting";
}
public sealed class V1ActivityVoter : BaseEntity
{
    public Guid ProposalId { get; set; }
    public Guid UserId { get; set; }
    public Guid MembershipId { get; set; }
    public bool? Support { get; set; }
}
