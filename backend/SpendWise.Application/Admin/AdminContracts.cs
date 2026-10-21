namespace SpendWise.Application.Admin;

// Các DTO và interface này phục vụ dashboard, giám sát rủi ro và thao tác quản trị.

public sealed record AdminDashboardDto(int TotalUsers, int ActiveUsers, int NewUsers, int TransactionCount, decimal TransactionVolume,
    int P2PCount, decimal SuccessRate, int FailedTransactions, int PendingTransactions,
    int RiskCases, int Disputes, int SupportCases, int AiRequests, string SystemHealth,
    SpendWise.Application.Features.Administration.A1Summary? Overview = null);
public sealed record AdminUserDto(Guid Id, string FullName, string Email, string SpendId, string Status, string SystemRole,
    bool MustChangePassword, bool HasSecurityPin, DateTime CreatedAt, DateTime? LastLoginAt, string? AvatarUrl = null, bool EmailVerified = true);
public sealed record ChangeUserStatusRequest(string Status, string Reason, string AdminPin);
public sealed record AdminCredentialResetRequest(string Reason, string AdminPin);
public sealed record AdminTemporaryPasswordResponse(Guid UserId, string TemporaryPassword, bool MustChangePassword);
public sealed record AdminSecurityPinResetResponse(Guid UserId, bool ResetRequired);
public sealed record FreezeWalletRequest(bool Frozen, string Reason);
public sealed record SensitiveAccessRequest(string Reason);
public sealed record LedgerTransactionDto(Guid Id, string Reference, string BusinessType, Guid BusinessId, string Status, decimal DebitTotal, decimal CreditTotal, bool Balanced, DateTime CreatedAt);
public sealed record RiskAssessment(int Score, string Level, string Decision, IReadOnlyList<string> Signals, Guid? RiskCaseId);
public sealed record ReviewRiskRequest(string Decision, string Reason);
public sealed record CreateDisputeRequest(Guid LinkedTransactionId, string Reason, string Description = "");
public sealed record CreateSupportCaseRequest(string Category, string Priority, Guid? LinkedTransactionId = null, string Subject = "Support request", string Description = "");
public sealed record CaseDto(Guid Id, string Type, string Status, string Summary, Guid UserId, Guid? LinkedTransactionId, DateTime CreatedAt);
public sealed record UpdateCaseRequest(string Status, string? InternalNotes, string Reason);
public sealed record ConfigurationDto(string Key, string Value, string ValueType, string Description);
public sealed record UpdateConfigurationRequest(string Value, string Reason);
public sealed record FeatureFlagDto(string Name, bool IsEnabled, string Description);
public sealed record UpdateFeatureFlagRequest(bool IsEnabled, string Reason);

public sealed record CaseAttachmentUpload(string FileName, string ContentType, byte[] Content);
public sealed record CaseAttachmentDto(Guid Id, string FileName, string ContentType, long Length, string Hash, DateTime CreatedAt);
public sealed record CaseHistoryDto(Guid Id, Guid ActorId, string ActorName, string ActorRole, string Action,
    string Message, string? OldStatus, string? NewStatus, DateTime CreatedAt, IReadOnlyList<CaseAttachmentDto> Attachments);
public sealed record SupportCaseDetailDto(Guid Id, string TicketNumber, Guid UserId, string Subject, string Description,
    string Category, string Priority, string Status, Guid? LinkedTransactionId, Guid? AssignedAdminId,
    DateTime CreatedAt, DateTime? UpdatedAt, DateTime? LatestReplyAt, bool CorrectionGranted,
    IReadOnlyList<CaseHistoryDto> History);
public sealed record DisputeDetailDto(Guid Id, Guid UserId, Guid LinkedTransactionId, string Reference, string Title,
    decimal Amount, string? Counterparty, DateTime OccurredAt, string Reason, string Description, string Status,
    string? Resolution, DateTime CreatedAt, DateTime? UpdatedAt, IReadOnlyList<CaseHistoryDto> History);
public sealed record CaseMessageRequest(string Message, IReadOnlyList<CaseAttachmentUpload>? Attachments = null);
public sealed record CaseStatusRequest(string Status, string Message, IReadOnlyList<CaseAttachmentUpload>? Attachments = null);
public sealed record CorrectionGrantRequest(string AdminPin, string Reason);
public sealed record AttachmentContentDto(string FileName, string ContentType, byte[] Content);

public interface IDatabaseHealthChecker
{
    Task<bool> CanConnectAsync(CancellationToken cancellationToken);
}

public interface IRiskService { Task<RiskAssessment> AssessTransferAsync(Guid userId, decimal amount, string? sessionId, CancellationToken cancellationToken); }
