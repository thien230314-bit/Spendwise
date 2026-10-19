namespace SpendWise.Application.Finance;

// Các DTO này mang dữ liệu chuyển tiền giữa hai ví của cùng một người dùng.
public sealed record InternalTransferRequest(Guid SourceWalletId, Guid DestinationWalletId, decimal Amount, string Description,
    string IdempotencyKey, string? SecurityPin = null);
public sealed record TransferDto(Guid Id, Guid SenderUserId, string SenderName, Guid RecipientUserId, string RecipientName, Guid SourceWalletId,
    Guid DestinationWalletId, decimal Amount, string Currency, string Description, string Status, Guid? LedgerTransactionId, DateTime? CompletedAt, bool Replayed);
