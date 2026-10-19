using SpendWise.Domain.Common;

namespace SpendWise.Domain.V1Groups;

public enum V1FundKind { Fixed, Free }
public enum V1InvitationStatus { Pending, Accepted, Rejected, Cancelled, Proposed }

// Separate tables keep Legacy money, taxonomy and histories untouched.
public sealed class V1Fund : BaseEntity
{
    private V1Fund() { }
    public V1Fund(string name, string code, V1FundKind kind, Guid founderUserId, long? targetVnd)
    {
        Name = name; Code = code; Kind = kind;
        FounderUserId = OwnerUserId = founderUserId; TargetVnd = targetVnd;
    }
    public string Name { get; private set; } = string.Empty;
    public string Code { get; private set; } = string.Empty;
    public V1FundKind Kind { get; private set; }
    public Guid FounderUserId { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public long OwnershipRevision { get; private set; }
    public long? TargetVnd { get; private set; }
    public DateTime? DissolvedAt { get; set; }
    public DateTime? ActivityClosedAt { get; private set; }
    public DateTime? OwnerRespondedAt { get; private set; }
    public Guid? OwnerResponseCaseId { get; private set; }
}

public sealed class V1FundMembership : BaseEntity
{
    public Guid FundId { get; set; }
    public Guid UserId { get; set; }
    public int Seat { get; set; }
    public bool CanSpend { get; set; }
    public DateTime JoinedAt { get; set; }
    public DateTime? LeftAt { get; set; }
}

public sealed class V1FundInvitation : BaseEntity
{
    public Guid FundId { get; set; }
    public Guid InvitedUserId { get; set; }
    public Guid InvitedByUserId { get; set; }
    public V1InvitationStatus Status { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
}

public sealed class V1OwnershipOffer : BaseEntity
{
    public Guid FundId { get; set; }
    public Guid FromUserId { get; set; }
    public Guid ToUserId { get; set; }
    public long ExpectedOwnershipRevision { get; set; }
    public DateTime? AcceptedAt { get; set; }
}

public sealed class V1FundActivity : BaseEntity
{
    public Guid FundId { get; set; }
    public Guid? ActorUserId { get; set; }
    public Guid? SubjectUserId { get; set; }
    public string Event { get; set; } = string.Empty;
    public string? Detail { get; set; }
    public Guid? ReferenceId { get; set; }
}

public sealed class V1FundCodeCounter
{
    public int Id { get; set; }
    public long NextValue { get; set; }
}
