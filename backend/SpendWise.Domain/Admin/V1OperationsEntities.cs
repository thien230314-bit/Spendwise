using SpendWise.Domain.Common;

namespace SpendWise.Domain.Admin;

public sealed class V1MaintenanceControl { public int Id { get; set; } public long Revision { get; set; } }

public sealed class V1UserActivity : BaseEntity
{
    public Guid UserId { get; set; }
    public DateTime LastActionAt { get; set; }
}

public sealed class V1MaintenancePlan : BaseEntity
{
    public string Scope { get; set; } = "Global";
    public bool IsEmergency { get; set; }
    public string? OperationKey { get; set; }
    public string? CloseSafetyEvidenceId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public string? ReviewNotes { get; set; }
    public string ReminderOffsetsJson { get; set; } = "[]";
    public DateTime? RemindersConfiguredAt { get; set; }
    public string? ReminderPolicyVersion { get; set; }
    public int? ReminderLateToleranceSeconds { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime ExpectedEndsAt { get; set; }
    public DateTime? ActivatedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string Reason { get; set; } = string.Empty;
    public long Revision { get; set; }
}
