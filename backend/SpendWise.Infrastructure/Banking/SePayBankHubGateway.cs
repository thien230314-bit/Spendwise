using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using SpendWise.Application.Banking;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Domain.Banking;

namespace SpendWise.Infrastructure.Banking;

// Client duy nhất quản lý HTTP, Basic/Bearer authentication và token cache của SePay.
public sealed class SePayBankHubGateway(
    IHttpClientFactory httpClientFactory,
    SePayBankHubConfiguration configuration,
    TimeProvider timeProvider) : IBankHubGateway, IDisposable
{
    private readonly SemaphoreSlim tokenGate = new(1, 1);
    private readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web);
    private string? cachedAccessToken;
    private DateTimeOffset tokenRefreshAt;

    public async Task<BankProviderLinkResult> CreateLinkAsync(
        BankProviderLinkRequest request,
        CancellationToken cancellationToken)
    {
        configuration.EnsureAvailable();
        var payload = new CreateLinkTokenWireRequest(
            configuration.Current.CompanyXid!,
            Purpose(request.Purpose),
            request.CompletionRedirectUri,
            request.ClientPlatform switch
            {
                BankLinkClientPlatform.Web => 0,
                BankLinkClientPlatform.Mobile => 1,
                _ => throw new AppException("invalid_input", "Client platform is invalid.")
            },
            request.Language,
            request.ExternalBankAccountId);
        using var response = await SendAuthorizedAsync(
            () => NewJsonRequest(HttpMethod.Post, "/v1/link-token/create", payload),
            cancellationToken);
        await EnsureSuccessAsync(response, "bank_link_failed", cancellationToken);
        var body = await ReadJsonAsync<CreateLinkTokenWireResponse>(response, cancellationToken);
        if (string.IsNullOrWhiteSpace(body.Xid)
            || string.IsNullOrWhiteSpace(body.HostedLinkUrl)
            || !TryProviderDate(body.ExpiresAt, out var expiresAt))
        {
            throw InvalidResponse();
        }
        return new BankProviderLinkResult(body.Xid, body.HostedLinkUrl, expiresAt);
    }

    public async Task<ExternalBankTransactionPage> GetTransactionsAsync(
        string externalBankAccountId,
        DateTime from,
        DateTime to,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        configuration.EnsureAvailable();
        var path = "/v1/transaction"
            + $"?page={page}&per_page={pageSize}"
            + $"&company_xid={Uri.EscapeDataString(configuration.Current.CompanyXid!)}"
            + $"&bank_account_xid={Uri.EscapeDataString(externalBankAccountId)}"
            + $"&start_transaction_date={from:yyyy-MM-dd}"
            + $"&end_transaction_date={to:yyyy-MM-dd}";
        using var response = await SendAuthorizedAsync(
            () => new HttpRequestMessage(HttpMethod.Get, BuildUri(path)),
            cancellationToken);
        await EnsureSuccessAsync(response, "bank_sync_failed", cancellationToken);
        var body = await ReadJsonAsync<TransactionListWireResponse>(response, cancellationToken);
        var items = (body.Data ?? []).Select(MapTransaction).ToList();
        var totalPages = Math.Max(body.Meta?.TotalPages ?? page, page);
        return new ExternalBankTransactionPage(items, page, totalPages);
    }

    public async Task<SandboxTransactionResult> CreateSandboxTransactionAsync(
        string externalBankAccountId,
        BankTransactionDirection direction,
        decimal amount,
        string content,
        CancellationToken cancellationToken)
    {
        if (!configuration.IsSandbox)
        {
            throw new ForbiddenException("sepay_sandbox_forbidden", "SePay Sandbox simulator is disabled in Production mode.");
        }

        try
        {
            using var response = await SendAuthorizedAsync(
                () => NewJsonRequest(
                    HttpMethod.Post,
                    "/v1/transaction/create",
                    new CreateSandboxTransactionWireRequest(
                        externalBankAccountId,
                        direction == BankTransactionDirection.Credit ? "credit" : "debit",
                        amount,
                        content)),
                cancellationToken);

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var isTimeout = response.StatusCode is System.Net.HttpStatusCode.RequestTimeout
                    or System.Net.HttpStatusCode.GatewayTimeout
                    or System.Net.HttpStatusCode.ServiceUnavailable;
                var isAmbiguous5xx = (int)response.StatusCode >= 500;
                return new SandboxTransactionResult(
                    Success: false,
                    ProviderTransactionId: null,
                    ReferenceNumber: null,
                    TransactionDate: null,
                    Amount: amount,
                    Direction: direction.ToString(),
                    ExternalBankAccountId: externalBankAccountId,
                    ProviderStatus: response.StatusCode.ToString(),
                    ErrorCode: (isTimeout || isAmbiguous5xx) ? "provider_timeout" : "provider_rejected",
                    ErrorMessage: string.IsNullOrWhiteSpace(responseBody) ? response.ReasonPhrase : responseBody,
                    IsTimeoutOrUnknown: isTimeout || isAmbiguous5xx,
                    RawResponseData: responseBody);
            }

            // HTTP 2xx: Validate response envelope and mandatory business evidence
            try
            {
                using var doc = JsonDocument.Parse(responseBody);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    return new SandboxTransactionResult(
                        Success: false,
                        Amount: amount,
                        Direction: direction.ToString(),
                        ExternalBankAccountId: externalBankAccountId,
                        ProviderStatus: "InvalidEnvelope",
                        ErrorCode: "invalid_response_envelope",
                        ErrorMessage: "Provider response root is not a JSON object.",
                        IsTimeoutOrUnknown: false,
                        RawResponseData: responseBody);
                }

                // Check for business error inside envelope if code property exists
                if (root.TryGetProperty("code", out var codeProp) && codeProp.ValueKind == JsonValueKind.Number)
                {
                    var businessCode = codeProp.GetInt32();
                    if (businessCode is not (200 or 201))
                    {
                        var msg = root.TryGetProperty("message", out var msgProp) ? msgProp.GetString() : "Provider business error.";
                        return new SandboxTransactionResult(
                            Success: false,
                            Amount: amount,
                            Direction: direction.ToString(),
                            ExternalBankAccountId: externalBankAccountId,
                            ProviderStatus: "BusinessError",
                            ErrorCode: "provider_business_error",
                            ErrorMessage: msg,
                            IsTimeoutOrUnknown: false,
                            RawResponseData: responseBody);
                    }
                }

                if (!root.TryGetProperty("data", out var dataEl) || dataEl.ValueKind != JsonValueKind.Object)
                {
                    return new SandboxTransactionResult(
                        Success: false,
                        Amount: amount,
                        Direction: direction.ToString(),
                        ExternalBankAccountId: externalBankAccountId,
                        ProviderStatus: "MissingData",
                        ErrorCode: "missing_provider_data",
                        ErrorMessage: "Provider response missing mandatory 'data' object.",
                        IsTimeoutOrUnknown: false,
                        RawResponseData: responseBody);
                }

                // Mandatory: Provider Transaction ID
                string? providerTxId = null;
                if (dataEl.TryGetProperty("transaction_id", out var txIdProp) && txIdProp.ValueKind == JsonValueKind.String)
                {
                    providerTxId = txIdProp.GetString()?.Trim();
                }
                else if (dataEl.TryGetProperty("id", out var idProp) && idProp.ValueKind is JsonValueKind.String or JsonValueKind.Number)
                {
                    providerTxId = idProp.ToString().Trim();
                }
                else if (dataEl.TryGetProperty("xid", out var xidProp) && xidProp.ValueKind == JsonValueKind.String)
                {
                    providerTxId = xidProp.GetString()?.Trim();
                }

                if (string.IsNullOrWhiteSpace(providerTxId))
                {
                    return new SandboxTransactionResult(
                        Success: false,
                        Amount: amount,
                        Direction: direction.ToString(),
                        ExternalBankAccountId: externalBankAccountId,
                        ProviderStatus: "MissingTransactionId",
                        ErrorCode: "missing_provider_transaction_id",
                        ErrorMessage: "Provider response does not contain a valid non-empty transaction ID.",
                        IsTimeoutOrUnknown: false,
                        RawResponseData: responseBody);
                }

                // Validate amount if present in provider response
                if (dataEl.TryGetProperty("amount", out var amountProp) && amountProp.ValueKind == JsonValueKind.Number)
                {
                    if (amountProp.TryGetDecimal(out var returnedAmount) && returnedAmount != amount)
                    {
                        return new SandboxTransactionResult(
                            Success: false,
                            ProviderTransactionId: providerTxId,
                            Amount: returnedAmount,
                            Direction: direction.ToString(),
                            ExternalBankAccountId: externalBankAccountId,
                            ProviderStatus: "AmountMismatch",
                            ErrorCode: "provider_amount_mismatch",
                            ErrorMessage: $"Provider transaction amount {returnedAmount} does not match requested amount {amount}.",
                            IsTimeoutOrUnknown: false,
                            RawResponseData: responseBody);
                    }
                }

                // Validate transfer_type / direction if present in provider response
                var expectedType = direction == BankTransactionDirection.Credit ? "credit" : "debit";
                if (dataEl.TryGetProperty("transfer_type", out var typeProp) && typeProp.ValueKind == JsonValueKind.String)
                {
                    var returnedType = typeProp.GetString()?.Trim().ToLowerInvariant();
                    if (!string.IsNullOrEmpty(returnedType) && returnedType != expectedType)
                    {
                        return new SandboxTransactionResult(
                            Success: false,
                            ProviderTransactionId: providerTxId,
                            Amount: amount,
                            Direction: returnedType,
                            ExternalBankAccountId: externalBankAccountId,
                            ProviderStatus: "DirectionMismatch",
                            ErrorCode: "provider_direction_mismatch",
                            ErrorMessage: $"Provider transfer_type '{returnedType}' does not match expected '{expectedType}'.",
                            IsTimeoutOrUnknown: false,
                            RawResponseData: responseBody);
                    }
                }

                string? refNum = null;
                if (dataEl.TryGetProperty("reference_number", out var refProp) && refProp.ValueKind == JsonValueKind.String)
                {
                    refNum = refProp.GetString()?.Trim();
                }

                DateTime? txDate = null;
                if (dataEl.TryGetProperty("transaction_date", out var dateProp))
                {
                    if (dateProp.ValueKind == JsonValueKind.String && DateTime.TryParse(dateProp.GetString(), out var parsedDate))
                    {
                        txDate = parsedDate;
                    }
                    else if (dateProp.TryGetDateTime(out var directDate))
                    {
                        txDate = directDate;
                    }
                }

                return new SandboxTransactionResult(
                    Success: true,
                    ProviderTransactionId: providerTxId,
                    ReferenceNumber: refNum,
                    TransactionDate: txDate,
                    Amount: amount,
                    Direction: direction.ToString(),
                    ExternalBankAccountId: externalBankAccountId,
                    ProviderStatus: "Success",
                    ErrorCode: null,
                    ErrorMessage: null,
                    IsTimeoutOrUnknown: false,
                    RawResponseData: responseBody);
            }
            catch (JsonException ex)
            {
                return new SandboxTransactionResult(
                    Success: false,
                    Amount: amount,
                    Direction: direction.ToString(),
                    ExternalBankAccountId: externalBankAccountId,
                    ProviderStatus: "InvalidJson",
                    ErrorCode: "invalid_provider_json",
                    ErrorMessage: $"Failed to parse provider response as valid JSON: {ex.Message}",
                    IsTimeoutOrUnknown: false,
                    RawResponseData: responseBody);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new SandboxTransactionResult(
                Success: false,
                ProviderTransactionId: null,
                ReferenceNumber: null,
                TransactionDate: null,
                Amount: amount,
                Direction: direction.ToString(),
                ExternalBankAccountId: externalBankAccountId,
                ProviderStatus: "Timeout",
                ErrorCode: "provider_timeout",
                ErrorMessage: "Provider call timed out.",
                IsTimeoutOrUnknown: true);
        }
        catch (HttpRequestException ex)
        {
            return new SandboxTransactionResult(
                Success: false,
                ProviderTransactionId: null,
                ReferenceNumber: null,
                TransactionDate: null,
                Amount: amount,
                Direction: direction.ToString(),
                ExternalBankAccountId: externalBankAccountId,
                ProviderStatus: "NetworkError",
                ErrorCode: "provider_network_error",
                ErrorMessage: ex.Message,
                IsTimeoutOrUnknown: true);
        }
    }

    private async Task<HttpResponseMessage> SendAuthorizedAsync(
        Func<HttpRequestMessage> requestFactory,
        CancellationToken cancellationToken)
    {
        configuration.EnsureAvailable();
        var token = await GetAccessTokenAsync(cancellationToken);
        using var firstRequest = requestFactory();
        firstRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var client = httpClientFactory.CreateClient("SePayBankHub");
        var response = await client.SendAsync(firstRequest, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            if (response.IsSuccessStatusCode)
                configuration.MarkProviderCallSucceeded(timeProvider.GetUtcNow().UtcDateTime);
            return response;
        }

        response.Dispose();
        // Provider 401 làm mất hiệu lực token cũ rồi retry đúng một lần.
        InvalidateToken(token);
        var refreshed = await GetAccessTokenAsync(cancellationToken);
        using var retryRequest = requestFactory();
        retryRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refreshed);
        var retryResponse = await client.SendAsync(retryRequest, cancellationToken);
        if (retryResponse.IsSuccessStatusCode)
            configuration.MarkProviderCallSucceeded(timeProvider.GetUtcNow().UtcDateTime);
        return retryResponse;
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        if (!string.IsNullOrWhiteSpace(cachedAccessToken) && now < tokenRefreshAt)
            return cachedAccessToken;

        // SemaphoreSlim tạo single-flight: nhiều request hết token chỉ gọi /v1/token một lần.
        await tokenGate.WaitAsync(cancellationToken);
        try
        {
            now = timeProvider.GetUtcNow();
            if (!string.IsNullOrWhiteSpace(cachedAccessToken) && now < tokenRefreshAt)
                return cachedAccessToken;

            var current = configuration.Current;
            configuration.EnsureAvailable();
            using var request = new HttpRequestMessage(HttpMethod.Post, BuildUri("/v1/token"));
            var basicValue = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{current.ClientId}:{current.ClientSecret}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basicValue);
            request.Content = new ByteArrayContent([]);
            using var response = await httpClientFactory.CreateClient("SePayBankHub")
                .SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
                throw new AppException("sepay_auth_failed", "SePay authentication failed.", 503);
            await EnsureSuccessAsync(response, "sepay_unavailable", cancellationToken);
            var body = await ReadJsonAsync<TokenWireResponse>(response, cancellationToken);
            if (string.IsNullOrWhiteSpace(body.AccessToken) || body.Ttl <= 0)
                throw InvalidResponse();

            cachedAccessToken = body.AccessToken;
            var lifetime = TimeSpan.FromSeconds(body.Ttl);
            var skew = TimeSpan.FromSeconds(Math.Min(30, Math.Max(1, body.Ttl / 10d)));
            tokenRefreshAt = now + lifetime - skew;
            configuration.MarkProviderCallSucceeded(now.UtcDateTime);
            return cachedAccessToken;
        }
        finally
        {
            tokenGate.Release();
        }
    }

    private void InvalidateToken(string rejectedToken)
    {
        // Chỉ xóa nếu cache vẫn chứa đúng token bị provider từ chối.
        if (string.Equals(cachedAccessToken, rejectedToken, StringComparison.Ordinal))
        {
            cachedAccessToken = null;
            tokenRefreshAt = default;
        }
    }

    private HttpRequestMessage NewJsonRequest<T>(HttpMethod method, string path, T payload)
    {
        var request = new HttpRequestMessage(method, BuildUri(path));
        request.Content = JsonContent.Create(payload, options: jsonOptions);
        return request;
    }

    private Uri BuildUri(string path)
    {
        var current = configuration.Current;
        var baseUrl = current.Environment == SePayBankHubEnvironment.Sandbox
            ? current.SandboxBaseUrl
            : current.ProductionBaseUrl;
        if (!Uri.TryCreate(baseUrl.TrimEnd('/') + path, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new AppException("sepay_invalid_response", "SePay base URL is invalid.", 503);
        }
        return uri;
    }

    private async Task<T> ReadJsonAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<T>(jsonOptions, cancellationToken)
                ?? throw InvalidResponse();
        }
        catch (JsonException)
        {
            throw InvalidResponse();
        }
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        string errorCode,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;
        // Đọc và bỏ body để hoàn tất response nhưng không đưa payload/secret provider vào log hoặc exception.
        _ = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        var code = response.StatusCode == HttpStatusCode.Unauthorized ? "sepay_auth_failed" : errorCode;
        throw new AppException(code, "SePay Bank Hub request failed.", 503);
    }

    private static ExternalBankTransactionData MapTransaction(TransactionWireItem item)
    {
        if (!TryProviderDate(item.TransactionDate, out var date)
            || string.IsNullOrWhiteSpace(item.TransactionId))
            throw InvalidResponse();
        var direction = item.TransferType?.Trim().ToLowerInvariant() switch
        {
            "in" or "credit" => BankTransactionDirection.Credit,
            "out" or "debit" => BankTransactionDirection.Debit,
            _ => throw InvalidResponse()
        };
        return new ExternalBankTransactionData(
            item.TransactionId,
            direction,
            item.Amount,
            date,
            item.TransactionContent ?? string.Empty,
            item.ReferenceNumber,
            item.PaymentCode,
            item.BrandName);
    }

    private static bool TryProviderDate(string? value, out DateTime result)
    {
        var valid = DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsed);
        result = parsed.UtcDateTime;
        return valid;
    }

    private static string Purpose(BankLinkPurpose purpose) => purpose switch
    {
        BankLinkPurpose.LinkBankAccount => "LINK_BANK_ACCOUNT",
        BankLinkPurpose.UnlinkBankAccount => "UNLINK_BANK_ACCOUNT",
        BankLinkPurpose.ReactivateBankAccount => "REACTIVATE_BANK_ACCOUNT",
        _ => throw new ArgumentOutOfRangeException(nameof(purpose))
    };

    private static AppException InvalidResponse() => new(
        "sepay_invalid_response",
        "SePay returned an invalid response.",
        502);

    public void Dispose() => tokenGate.Dispose();

    private sealed record TokenWireResponse(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("ttl")] int Ttl);
    private sealed record CreateLinkTokenWireRequest(
        [property: JsonPropertyName("company_xid")] string CompanyXid,
        [property: JsonPropertyName("purpose")] string Purpose,
        [property: JsonPropertyName("completion_redirect_uri")] string CompletionRedirectUri,
        [property: JsonPropertyName("is_mobile_app")] int IsMobileApp,
        [property: JsonPropertyName("language")] string Language,
        [property: JsonPropertyName("bank_account_xid")]
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? BankAccountXid);
    private sealed record CreateLinkTokenWireResponse(
        [property: JsonPropertyName("xid")] string? Xid,
        [property: JsonPropertyName("hosted_link_url")] string? HostedLinkUrl,
        [property: JsonPropertyName("link_token")] string? LinkToken,
        [property: JsonPropertyName("expires_at")] string? ExpiresAt);
    private sealed record CreateSandboxTransactionWireRequest(
        [property: JsonPropertyName("bank_account_xid")] string BankAccountXid,
        [property: JsonPropertyName("transfer_type")] string TransferType,
        [property: JsonPropertyName("amount")] decimal Amount,
        [property: JsonPropertyName("transaction_content")] string TransactionContent);
    private sealed record TransactionListWireResponse(
        [property: JsonPropertyName("data")] List<TransactionWireItem>? Data,
        [property: JsonPropertyName("meta")] TransactionMetaWire? Meta);
    private sealed record TransactionMetaWire(
        [property: JsonPropertyName("page")] int Page,
        [property: JsonPropertyName("per_page")] int PerPage,
        [property: JsonPropertyName("total")] int Total,
        [property: JsonPropertyName("total_pages")] int TotalPages);
    private sealed record TransactionWireItem(
        [property: JsonPropertyName("transaction_id")] string? TransactionId,
        [property: JsonPropertyName("transaction_date")] string? TransactionDate,
        [property: JsonPropertyName("bank_account_xid")] string? BankAccountXid,
        [property: JsonPropertyName("brand_name")] string? BrandName,
        [property: JsonPropertyName("reference_number")] string? ReferenceNumber,
        [property: JsonPropertyName("transfer_type")] string? TransferType,
        [property: JsonPropertyName("amount")] decimal Amount,
        [property: JsonPropertyName("transaction_content")] string? TransactionContent,
        [property: JsonPropertyName("payment_code")] string? PaymentCode);
}
