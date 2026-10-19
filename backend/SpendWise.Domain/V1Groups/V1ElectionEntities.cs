using SpendWise.Domain.Common;

namespace SpendWise.Domain.V1Groups;

public enum V1ElectionPhase { Registration, Nomination, Runoff, Confirmation, AwaitingAcceptance, Completed, Failed }
public sealed class V1Election : BaseEntity
{
    public Guid FundId { get; set; }
    public Guid IncumbentUserId { get; set; }
    public Guid OpenedByUserId { get; set; }
    public long OwnershipRevision { get; set; }
    public V1ElectionPhase Phase { get; set; }
    public DateTime PhaseStartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public Guid? WinnerUserId { get; set; }
    public string PolicyJson { get; set; } = string.Empty;
    public string InactivityEvidenceId { get; set; } = string.Empty;
    public string? FailureReason { get; set; }
}
public sealed class V1ElectionVoter : BaseEntity
{
    public Guid ElectionId { get; set; }
    public Guid UserId { get; set; }
    public Guid MembershipId { get; set; }
}
public sealed class V1ElectionCandidate : BaseEntity
{
    public Guid ElectionId { get; set; }
    public Guid UserId { get; set; }
    public bool InRunoff { get; set; }
}
public sealed class V1ElectionBallot : BaseEntity
{
    public Guid ElectionId { get; set; }
    public Guid VoterUserId { get; set; }
    public int Round { get; set; }
    public Guid? CandidateUserId { get; set; }
    public bool? Support { get; set; }
}
