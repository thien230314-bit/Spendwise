namespace SpendWise.Application.Finance;

public sealed record CreateCategoryRequest(string Name, string Kind, string? Icon = null, string? Color = null);
// Cho phép đổi loại khi không xung đột với lịch định kỳ đang hoạt động.
public sealed record UpdateCategoryRequest(string Name, string Kind, string? Icon = null, string? Color = null);
public sealed record CategoryDto(Guid Id, string Key, string Name, string Kind, string? Icon, string? Color, bool IsSystem, bool IsActive);
public sealed record CategoryDeleteResult(Guid Id, bool Archived);
public sealed record CreateManualTransactionRequest(Guid WalletId, Guid CategoryId, decimal Amount, string Description,
    DateTime? OccurredAt = null, string? IdempotencyKey = null, string? SecurityPin = null,
    IReadOnlyList<Guid>? EvidenceIds = null, string? Counterparty = null);
public sealed record WalletAllocationRequest(Guid WalletId, decimal Amount);
public sealed record CreateMultiWalletExpenseRequest(decimal Amount, IReadOnlyList<WalletAllocationRequest> WalletAllocations,
    Guid CategoryId, string Description, DateTime? OccurredAt = null, string? IdempotencyKey = null,
    string? SecurityPin = null, IReadOnlyList<Guid>? EvidenceIds = null, string? Counterparty = null);
public sealed record UpdateTransactionRequest(Guid? CategoryId, string Description, string? SecurityPin = null,
    IReadOnlyList<Guid>? EvidenceIds = null, string? Reason = null, DateTime? OccurredAt = null,
    Guid? WalletId = null, decimal? Amount = null, string? Counterparty = null,
    IReadOnlyList<WalletAllocationRequest>? WalletAllocations = null);
public sealed record TransactionQuery(int Page = 1, int PageSize = 20, Guid? WalletId = null, Guid? CategoryId = null,
    string? Type = null, DateTime? From = null, DateTime? To = null, decimal? MinimumAmount = null, decimal? MaximumAmount = null, string? Search = null);
public sealed record WalletAllocationDto(Guid WalletId, string WalletName, decimal Amount);
public sealed record TransactionDto(Guid Id, Guid? WalletId, string? WalletName, Guid? CategoryId, string? CategoryName, string Type, string Status,
    decimal Amount, decimal SignedAmount, string Currency, string Description, string? CounterpartyName, DateTime OccurredAt,
    Guid LedgerTransactionId, Guid? RelatedBusinessId, int CorrectionAllowance = 0, int CorrectionCount = 0,
    bool CanCorrect = false, IReadOnlyList<TransactionCorrectionDto>? Corrections = null,
    IReadOnlyList<EvidenceDto>? Evidence = null,
    IReadOnlyList<TransactionCorrectionGrantDto>? CorrectionGrants = null,
    IReadOnlyList<WalletAllocationDto>? WalletAllocations = null);
public sealed record CorrectionSnapshotDto(decimal Amount, string? Category, string Description, Guid? WalletId, string? WalletName, DateTime OccurredAt, string? Counterparty,
    IReadOnlyList<WalletAllocationDto>? WalletAllocations = null);
public sealed record TransactionCorrectionDto(Guid Id, int CorrectionNumber, string Reason, Guid ActorUserId,
    string OriginalValuesJson, string CorrectedValuesJson, Guid OriginalLedgerTransactionId,
    Guid ReversalLedgerTransactionId, Guid CorrectedLedgerTransactionId, DateTime CreatedAt,
    IReadOnlyList<EvidenceDto> Evidence,
    string ActorRole = "User", string ActorName = "", CorrectionSnapshotDto? Before = null, CorrectionSnapshotDto? After = null);
public sealed record TransactionCorrectionGrantDto(Guid Id, Guid AdminId, string AdminName, Guid SupportCaseId, string Reason, DateTime GrantedAt);
public sealed record EvidenceDto(Guid Id, string OriginalFileName, string ContentType, long Length, string ContentHash, DateTime CreatedAt);
public sealed record EvidenceContentDto(string OriginalFileName, string ContentType, byte[] Content);
public sealed record UploadEvidenceData(string OriginalFileName, string ContentType, byte[] Content);
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
