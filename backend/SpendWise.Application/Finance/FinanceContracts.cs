using SpendWise.Domain.Finance;

namespace SpendWise.Application.Finance;

public sealed record CreateWalletRequest(string Name, string Type, bool IsDefault = false);
// Không nhận số dư hoặc trạng thái để tránh sửa lịch sử tài chính bằng CRUD.
public sealed record UpdateWalletRequest(string Name, string Type, bool IsDefault = false);
public sealed record WalletDto(Guid Id, string Name, string Type, string Currency, string Status, decimal AvailableBalance, bool IsDefault, DateTime CreatedAt);
public sealed record LedgerPostingResult(Guid LedgerTransactionId, string Reference, decimal Amount, string Currency, bool Replayed);
public sealed record LedgerPostingEntry(Guid AccountId, LedgerEntryDirection Direction, decimal Amount);

public interface ILedgerService
{
    Task<LedgerPostingResult> PostAsync(Guid sourceAccountId, Guid destinationAccountId, decimal amount, string currency,
        string businessType, Guid businessId, string? idempotencyScope, string? idempotencyKey, string description,
        CancellationToken cancellationToken);
    Task<LedgerPostingResult> PostEntriesAsync(IReadOnlyList<LedgerPostingEntry> entries, string currency,
        string businessType, Guid businessId, string? idempotencyScope, string? idempotencyKey, string description,
        CancellationToken cancellationToken);
    Task<LedgerPostingResult> ReverseAsync(Guid originalLedgerTransactionId, Guid reversalBusinessId, string idempotencyKey,
        string reason, CancellationToken cancellationToken);
}
