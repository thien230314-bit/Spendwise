using SpendWise.Domain.Common;

namespace SpendWise.Domain.Admin;

public sealed class V1Broadcast : BaseEntity
{
    public Guid CreatedByUserId { get; set; }
    public string EventKey { get; set; } = string.Empty;
    public string Type { get; set; } = "Operational";
    public string TargetRole { get; set; } = "User";
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string State { get; set; } = "Draft";
    public long Revision { get; set; } = 1;
    public DateTime? ScheduledFor { get; set; }
    public DateTime? AudienceCutoffAt { get; set; }
    public DateTime? PublishedAt { get; set; }
    public Guid? MaintenancePlanId { get; set; }
    public long? MaintenanceRevision { get; set; }
    public int? RecipientLimit { get; set; }
}
