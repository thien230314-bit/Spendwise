namespace SpendWise.Application.Features.Finance.TransactionAuthorizations;

public sealed record CreateTransactionAuthorizationOptionsRequest(
    string TransactionType,
    decimal Amount,
    string? Currency,
    string SourceId,
    string DestinationId,
    string IdempotencyKey,
    string? PayloadJson);

public sealed record TransactionAuthorizationOptionsResponse(
    Guid AuthorizationId,
    string Challenge,
    string TransactionType,
    decimal Amount,
    string Currency,
    string SourceId,
    string DestinationId,
    DateTime ExpiresAt,
    IReadOnlyList<string> AllowedCredentials);

public sealed record VerifyTransactionAuthorizationRequest(
    Guid AuthorizationId,
    string CredentialId,
    string Signature,
    string? ClientDataJSON,
    string? AuthenticatorData);

public sealed record TransactionAuthorizationExecutionResult(
    bool Success,
    Guid AuthorizationId,
    string Status,
    string TransactionType,
    decimal Amount,
    string Currency,
    DateTime? ExecutedAt,
    object? Result);
