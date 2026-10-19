namespace SpendWise.Application.AccountRecovery;

public sealed record SubmitAccountRecoveryRequest(
    string FullName,
    string Phone,
    string Description,
    string? EmailHint);

public sealed record AccountRecoverySubmissionResponse(string Message);

public sealed record AccountRecoveryDto(
    Guid Id,
    string FullName,
    string Phone,
    string Description,
    string? EmailHint,
    string Status,
    Guid? ResolvedUserId,
    Guid? ReviewedByAdminId,
    DateTime? ReviewedAt,
    string? AdminNote,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    IReadOnlyList<AccountRecoveryReviewDto> Reviews);

public sealed record AccountRecoveryReviewDto(
    Guid Id,
    Guid AdminId,
    string Action,
    string Note,
    DateTime CreatedAt);

public sealed record AdminAccountRecoveryFilter(string? Status = null, string? Search = null);
public sealed record AccountRecoveryAdminActionRequest(string AdminPin, string Reason);
public sealed record ApproveAccountRecoveryRequest(string AdminPin, string Reason, Guid UserId);
public sealed record AccountRecoveryApprovalResponse(
    AccountRecoveryDto Recovery,
    string TemporaryPassword);
