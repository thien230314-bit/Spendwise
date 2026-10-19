using System.Text.Json;
using System.Text.Json.Serialization;
using SpendWise.Application.Finance;
using SpendWise.Domain.Banking;

namespace SpendWise.Application.Banking;

// DTO public chỉ chứa dữ liệu an toàn cần cho frontend; không chứa token hoặc secret của SePay.
[JsonConverter(typeof(JsonStringEnumConverter<BankLinkClientPlatform>))]
public enum BankLinkClientPlatform { Web, Mobile }

public sealed record CreateBankLinkRequest(
    string? CompletionRedirectUri = null,
    string Language = "vi",
    BankLinkClientPlatform ClientPlatform = BankLinkClientPlatform.Web);
public sealed record BankLinkSessionDto(Guid SessionId, string HostedLinkUrl, DateTime ExpiresAt, string Purpose);
public sealed record TestModeBankLinkChallengeDto(
    Guid ChallengeId,
    string Reference,
    DateTime ExpiresAt,
    string Instruction);
public sealed record CreateWalletTopUpRequest(Guid BankConnectionId, Guid WalletId, decimal Amount);
public sealed record WalletTopUpDto(
    Guid Id,
    Guid BankConnectionId,
    Guid WalletId,
    decimal Amount,
    string Currency,
    string MaskedAccountNumber,
    string Reference,
    string Status,
    DateTime ExpiresAt,
    DateTime? CompletedAt,
    Guid? FinancialTransactionId,
    string Instruction);
public sealed record TestModeLinkedAccountDto(
    Guid BankConnectionId,
    string BankName,
    string MaskedAccountNumber,
    string? AccountType,
    string Status,
    decimal AccumulatedBalance);
public sealed record BankConnectionDto(
    Guid Id,
    string Provider,
    string AccountEnvironment,
    string BankBrandName,
    string? AccountType,
    string? AccountHolderName,
    string MaskedAccountNumber,
    string Status,
    DateTime? LinkedAt,
    DateTime? LastSyncedAt);
public sealed record BankTransactionDto(
    Guid Id,
    Guid BankConnectionId,
    string BankBrandName,
    string MaskedAccountNumber,
    string ExternalTransactionId,
    string Direction,
    decimal Amount,
    string Currency,
    DateTime TransactionDate,
    string Content,
    string? ReferenceCode,
    string? PaymentCode,
    string? Gateway,
    string Source,
    DateTime ImportedAt,
    Guid? CategoryId = null,
    string? CategoryName = null);
public sealed record ReclassifyBankTransactionRequest(Guid CategoryId);
public sealed record BankTransactionFilter(
    int Page = 1,
    int PageSize = 20,
    Guid? BankConnectionId = null,
    string? Direction = null,
    DateTime? From = null,
    DateTime? To = null,
    string? Search = null);
public sealed record AdminBankTransactionFilter(
    int Page = 1,
    int PageSize = 20,
    Guid? UserId = null,
    Guid? BankConnectionId = null,
    string? Provider = null,
    string? Direction = null,
    DateTime? From = null,
    DateTime? To = null,
    string? Search = null);
public sealed record BankSyncDto(int ImportedCount, int DuplicateCount, DateTime LastSyncedAt);
public sealed record CreateBankSandboxTransactionRequest(
    Guid BankConnectionId,
    string TransferType,
    decimal Amount,
    string Content);
public sealed record BankSandboxTransactionAcceptedDto(bool Accepted, string Provider, string Environment);

// Các DTO quản trị chỉ hỗ trợ quan sát và đối soát, không cho gán tài khoản vào người dùng tùy ý.
public sealed record AdminBankConnectionDto(
    Guid Id,
    Guid UserId,
    string Provider,
    string AccountEnvironment,
    string ExternalAccountId,
    string BankBrandName,
    string MaskedAccountNumber,
    string Status,
    DateTime? LinkedAt,
    DateTime? LastSyncedAt,
    DateTime CreatedAt);
public sealed record AdminBankTransactionDto(
    Guid Id,
    Guid UserId,
    string UserFullName,
    string UserSpendId,
    Guid BankConnectionId,
    string Provider,
    string BankBrandName,
    string MaskedAccountNumber,
    string ExternalTransactionId,
    string Direction,
    decimal Amount,
    string Currency,
    DateTime TransactionDate,
    string Content,
    string? ReferenceCode,
    string? PaymentCode,
    string? Gateway,
    string Source,
    DateTime ImportedAt);
public sealed record AdminBankIntegrationEventDto(
    Guid Id,
    string Provider,
    string? ExternalEventId,
    string? ExternalTransactionId,
    string EventType,
    string? ExternalBankAccountId,
    string Status,
    string? FailureCode,
    int DuplicateDeliveryCount,
    DateTime? LastDuplicateReceivedAt,
    DateTime ReceivedAt,
    DateTime? ProcessedAt);
public sealed record BankReconciliationDto(
    int ActiveConnections,
    int InactiveConnections,
    int UnlinkedConnections,
    int ProcessedEvents,
    int DuplicateDeliveries,
    int UnmatchedEvents,
    int FailedEvents,
    int ImportedTransactions);
public sealed record SePayBankHubStatusDto(
    bool Enabled,
    string Environment,
    bool Configured,
    bool CompanyConfigured,
    bool LifecycleWebhookConfigured,
    bool IpnConfigured,
    DateTime? LastSuccessfulProviderCallAt,
    DateTime? LastWebhookReceivedAt);

// Dữ liệu từ provider được chuẩn hóa tại boundary trước khi đi vào nghiệp vụ.
public sealed record BankProviderLinkRequest(
    BankLinkPurpose Purpose,
    string CompletionRedirectUri,
    string Language,
    string? ExternalBankAccountId = null,
    BankLinkClientPlatform ClientPlatform = BankLinkClientPlatform.Web);
public sealed record BankProviderLinkResult(
    string ExternalLinkTokenXid,
    string HostedLinkUrl,
    DateTime ExpiresAt);
public sealed record ExternalBankTransactionData(
    string ExternalTransactionId,
    BankTransactionDirection Direction,
    decimal Amount,
    DateTime TransactionDate,
    string Content,
    string? ReferenceCode,
    string? PaymentCode,
    string? Gateway);
public sealed record ExternalBankTransactionPage(
    IReadOnlyList<ExternalBankTransactionData> Items,
    int Page,
    int TotalPages);
public sealed record BankTransactionIngestionResult(bool Created, BankTransaction? Transaction);

// Request webhook lifecycle giữ metadata dạng JSON vì từng loại event có trường khác nhau.
public sealed record SePayLifecycleEventRequest(
    long? Timestamp,
    string Xid,
    string? Version,
    string Event,
    JsonElement Metadata);
public sealed record SePayBankTransactionIpnRequest(
    string? Gateway,
    [property: JsonPropertyName("transaction_date")]
    string? TransactionDate,
    [property: JsonPropertyName("account_number")]
    string? AccountNumber,
    [property: JsonPropertyName("bank_account_xid")]
    string? BankAccountXid,
    string? Va,
    [property: JsonPropertyName("payment_code")]
    string? PaymentCode,
    string? Content,
    [property: JsonPropertyName("transfer_type")]
    string? TransferType,
    decimal Amount,
    [property: JsonPropertyName("reference_code")]
    string? ReferenceCode,
    decimal? Accumulated,
    [property: JsonPropertyName("transaction_id")]
    string? TransactionId);
public sealed record IntegrationAcknowledgementDto(bool Success);

// Alias này giữ response phân trang nhất quán với API giao dịch nội bộ.
public static class BankPaging
{
    public static PagedResult<BankTransactionDto> Result(
        IReadOnlyList<BankTransactionDto> items,
        int page,
        int pageSize,
        int totalCount) => new(items, page, pageSize, totalCount);
}
