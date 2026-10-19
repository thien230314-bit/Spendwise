using SpendWise.Domain.Common;

namespace SpendWise.Domain.Admin;

public enum AccountRecoveryStatus { Pending, MoreInfoNeeded, Approved, Rejected }

// Các entity này lưu audit, RiskCase, tranh chấp, hỗ trợ và cấu hình vận hành.

public enum CaseStatus { New, Open, Investigating, WaitingForUser, Resolved, Rejected, Closed }
public enum RiskLevel { Low, Medium, High }
public enum RiskDecision { Allow, Block, Review }
public enum ReviewStatus { Pending, Approved, Rejected }

public sealed class AuditLog : BaseEntity
{
    // AuditLog chỉ được thêm để truy vết thao tác quản trị, không CRUD-sửa lịch sử.
    public Guid? ActorUserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string TargetType { get; set; } = string.Empty;
    public string? TargetId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? MetadataJson { get; set; }
    public string? IpAddress { get; set; }
}

public sealed class AccountRecoveryRequest : BaseEntity
{
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? EmailHint { get; set; }
    public AccountRecoveryStatus Status { get; set; } = AccountRecoveryStatus.Pending;
    public Guid? ResolvedUserId { get; set; }
    public Guid? ReviewedByAdminId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? AdminNote { get; set; }
    public List<AccountRecoveryReview> Reviews { get; set; } = [];
}

public sealed class AccountRecoveryReview : BaseEntity
{
    public Guid AccountRecoveryRequestId { get; set; }
    public AccountRecoveryRequest Request { get; set; } = null!;
    public Guid AdminId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
}
public sealed class RiskCase : BaseEntity
{
    // RiskCase lưu kết quả đánh giá và quy trình review, không phải dữ liệu master CRUD.
    public Guid UserId { get; set; }
    public Guid? TransactionId { get; set; }
    public int RiskScore { get; set; }
    public RiskLevel RiskLevel { get; set; }
    public RiskDecision Decision { get; set; }
    public ReviewStatus ReviewStatus { get; set; } = ReviewStatus.Pending;
    public string Signals { get; set; } = string.Empty;
    public Guid? ReviewedByAdminId { get; set; }
}
public sealed class Dispute : BaseEntity
{
    // Tranh chấp giữ liên kết giao dịch để kiểm toán và giải quyết theo workflow.
    public Guid UserId { get; set; }
    public Guid LinkedTransactionId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string TransactionReferenceSnapshot { get; set; } = string.Empty;
    public string TransactionTitleSnapshot { get; set; } = string.Empty;
    public decimal TransactionAmountSnapshot { get; set; }
    public string? CounterpartySnapshot { get; set; }
    public DateTime TransactionOccurredAtSnapshot { get; set; }
    public CaseStatus Status { get; set; } = CaseStatus.Open;
    public string? InternalNotes { get; set; }
    public string? Resolution { get; set; }
    public DateTime? LatestReplyAt { get; set; }
}
public sealed class SupportCase : BaseEntity
{
    public DateTime? DueAt { get; set; }
    public DateTime? EscalatedAt { get; set; }
    public Guid? EscalatedById { get; set; }
    public Guid? EscalationReviewerId { get; set; }
    public DateTime? EscalationReviewedAt { get; set; }
    public string? EscalationReason { get; set; }
    public string? EscalationGuidance { get; set; }
    public string TicketNumber { get; set; } = string.Empty;
    public Guid LinkedUserId { get; set; }
    public Guid? LinkedTransactionId { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Priority { get; set; } = "Normal";
    public CaseStatus Status { get; set; } = CaseStatus.New;
    public Guid? AssignedAdminId { get; set; }
    public string? InternalNotes { get; set; }
    public DateTime? LatestReplyAt { get; set; }
    public bool CorrectionGranted { get; set; }
    public DateTime? CorrectionGrantedAt { get; set; }
    public Guid? CorrectionGrantedByAdminId { get; set; }
    public string? CorrectionGrantReason { get; set; }
}

public enum CaseKind { Support, Dispute }

public sealed class CaseHistory : BaseEntity
{
    public CaseKind CaseKind { get; set; }
    public Guid CaseId { get; set; }
    public Guid ActorId { get; set; }
    public string ActorDisplayNameSnapshot { get; set; } = string.Empty;
    public string ActorRole { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? OldStatus { get; set; }
    public string? NewStatus { get; set; }
    public List<CaseAttachment> Attachments { get; set; } = [];
}

public sealed class CaseAttachment : BaseEntity
{
    public Guid CaseHistoryId { get; set; }
    public CaseHistory History { get; set; } = null!;
    public Guid CaseOwnerUserId { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long Length { get; set; }
    public string ContentHash { get; set; } = string.Empty;
    public byte[] Content { get; set; } = [];
}

public sealed class SystemMaintenanceState : BaseEntity
{
    public bool IsEnabled { get; set; }
    public DateTime ChangedAt { get; set; }
    public Guid ChangedByAdminId { get; set; }
}

public enum EmergencyAnnouncementStatus { Active, Inactive }

public sealed class EmergencyAnnouncement : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public EmergencyAnnouncementStatus Status { get; set; } = EmergencyAnnouncementStatus.Active;
    public Guid CreatedByAdminId { get; set; }
    public DateTime? DeactivatedAt { get; set; }
}
public sealed class SystemConfiguration : BaseEntity
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string ValueType { get; set; } = "decimal";
    public string Description { get; set; } = string.Empty;
}
public sealed class FeatureFlag : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public string Description { get; set; } = string.Empty;
}
