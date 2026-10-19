using SpendWise.Domain.Banking;

namespace SpendWise.Application.Banking;

// Application chỉ biết gateway provider-neutral, còn HTTP/token SePay nằm trong Infrastructure.
public interface IBankHubGateway
{
    Task<BankProviderLinkResult> CreateLinkAsync(
        BankProviderLinkRequest request,
        CancellationToken cancellationToken);
    Task<ExternalBankTransactionPage> GetTransactionsAsync(
        string externalBankAccountId,
        DateTime from,
        DateTime to,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
    Task CreateSandboxTransactionAsync(
        string externalBankAccountId,
        BankTransactionDirection direction,
        decimal amount,
        string content,
        CancellationToken cancellationToken);
}

// Service cấu hình kiểm tra availability, redirect allowlist và xác thực webhook/IPN.
public interface IBankHubConfiguration
{
    bool IsSandbox { get; }
    void EnsureAvailable();
    string ResolveCompletionRedirectUri(string? requestedUri);
    string ResolveMobileCompletionRedirectUri(string? requestedUri);
    bool VerifyLifecycleSecret(string suppliedSecret);
    bool VerifyIpnAuthorization(string authorizationHeader);
    SePayBankHubStatusDto GetSafeStatus();
    void MarkWebhookReceived(DateTime receivedAt);
}

public interface IBankTransactionIngestionService
{
    Task<BankTransactionIngestionResult> IngestAsync(
        Guid bankConnectionId,
        ExternalBankTransactionData transaction,
        BankTransactionSource source,
        string? externalEventId,
        string? payloadHash,
        CancellationToken cancellationToken);
}

public interface IBankDirectPaymentReconciler
{
    Task<bool> ReconcileAsync(
        BankTransaction transaction,
        CancellationToken cancellationToken);
}
