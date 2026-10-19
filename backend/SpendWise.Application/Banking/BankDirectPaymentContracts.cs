namespace SpendWise.Application.Banking;

public sealed record BankTransferRecipientDto(
    Guid UserId,
    string FullName,
    string BankBrand,
    string AccountHolder,
    string MaskedAccount);

public sealed record CreateBankDirectTransferRequest(
    Guid RecipientUserId,
    decimal Amount,
    string? Note,
    string IdempotencyKey);

public sealed record ConfirmBankDirectPaymentRequest(string SecurityPin);

public sealed record BankDirectPaymentDto(
    Guid Id,
    Guid SenderUserId,
    Guid? RecipientUserId,
    string RecipientDisplayName,
    string? RecipientPhone,
    Guid RecipientBankConnectionId,
    string BankBrand,
    string AccountHolder,
    string MaskedAccount,
    decimal Amount,
    string Currency,
    string? Note,
    string PaymentReference,
    string ContextType,
    Guid? GroupId,
    string Status,
    DateTime CreatedAt,
    DateTime? SenderConfirmedAt,
    DateTime? CompletedAt,
    Guid? MatchedBankTransactionId);

public sealed record BankDirectPaymentHistoryFilter(int Page = 1, int PageSize = 20);
