using System.Security.Claims;
using SpendWise.Application.Admin;
using SpendWise.Domain.Admin;

namespace SpendWise.Application.Features.Administration;

// A5.1 - Ticket mới & tra cứu
public sealed record A5TicketQuery(
    string Reason,
    string? Status = null,
    string? Category = null,
    string? Priority = null,
    Guid? LinkedUserId = null,
    Guid? LinkedTransactionId = null,
    string? SearchText = null,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null,
    int Page = 1,
    int PageSize = 20);

public sealed record A5TicketListItem(
    Guid Id,
    string TicketNumber,
    Guid LinkedUserId,
    string? UserSpendId,
    string? UserFullName,
    string Category,
    string Priority,
    string Status,
    string Subject,
    string Description,
    Guid? LinkedTransactionId,
    Guid? AssignedAdminId,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? LatestReplyAt);

// A5.2 - Phân công
public sealed record A5AssignTicketRequest(
    string Reason,
    Guid OperatorId);

public sealed record A5AssignedCaseItem(
    Guid Id,
    string TicketNumber,
    string Status,
    string Priority,
    string Category,
    string Subject,
    Guid LinkedUserId,
    Guid? AssignedAdminId,
    DateTime CreatedAt,
    DateTime? LatestReplyAt);

// A5.3 - Chờ User phản hồi (Request info)
public sealed record A5RequestInfoRequest(
    string Reason,
    string Message,
    IReadOnlyList<CaseAttachmentUpload>? Attachments = null);

// A5.4 - Hồ sơ điều tra quỹ (Dossier)
public sealed record A5FundDossierResult(
    Guid CaseId,
    string TicketNumber,
    Guid LinkedUserId,
    string UserFullName,
    string UserEmail,
    string CaseStatus,
    string Category,
    string Priority,
    Guid? LinkedTransactionId,
    A5LinkedFundOverview? LinkedFund,
    IReadOnlyList<A5DossierDisputeItem> LinkedDisputes,
    IReadOnlyList<A5DossierTimelineEvent> Timeline,
    IReadOnlyList<A5DossierAttachmentItem> Attachments,
    string FinancialAuditNotice);

public sealed record A5LinkedFundOverview(
    Guid FundId,
    string FundName,
    bool IsV1Fund,
    string Currency,
    decimal Balance,
    string UserRoleInFund,
    int MemberCount,
    decimal TotalContributions,
    decimal TotalExpenses);

public sealed record A5DossierDisputeItem(
    Guid DisputeId,
    Guid TransactionId,
    string Status,
    string Reason,
    decimal Amount,
    DateTime CreatedAt);

public sealed record A5DossierTimelineEvent(
    DateTime TimestampUtc,
    string ActorRole,
    string ActorName,
    string Action,
    string Message);

public sealed record A5DossierAttachmentItem(
    Guid Id,
    string FileName,
    string ContentType,
    long Length,
    DateTime UploadedAt);

// A5.5 - Đề xuất chuyển Super Admin (Escalate)
public sealed record A5EscalateRequest(
    string Reason,
    string EscalationDetails);

public sealed record A5EscalationStatusResult(
    Guid CaseId,
    string TicketNumber,
    DateTime EscalatedAt,
    Guid EscalatedById,
    string EscalationReason,
    Guid? EscalationReviewerId,
    DateTime? EscalationReviewedAt,
    string? EscalationGuidance);

// A5.6 - Đã giải quyết (Resolve)
public sealed record A5ResolveRequest(
    string Reason,
    string ResolutionMessage,
    bool IsRejected = false);

public sealed record A5ResolveResult(
    Guid CaseId,
    string TicketNumber,
    string OldStatus,
    string NewStatus,
    string ResolutionMessage,
    DateTime ResolvedAt);

// A5.7 - Mẫu phản hồi (Templates)
public sealed record A5SupportTemplateItem(
    string TemplateKey,
    string DisplayTitle,
    string Category,
    string Description,
    IReadOnlyList<string> AvailablePlaceholders);

public sealed record A5ApplyTemplateRequest(
    string Reason,
    string TemplateKey,
    Dictionary<string, string?> Parameters,
    bool TransitionToWaitingForUser = false);

public sealed record A5AppliedTemplateResult(
    Guid CaseId,
    string TemplateKey,
    string RenderedSubject,
    string RenderedTextBody,
    bool SentAsNotification,
    string UpdatedCaseStatus);
