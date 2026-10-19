using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using SpendWise.Application.Banking;
using SpendWise.Application.Common.Exceptions;

namespace SpendWise.Infrastructure.Banking;

// SePay API v2 Test Mode dùng token và hostname riêng, không dùng Bank Hub credentials.
public sealed class SePayTestModeAccountReader(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    IHostEnvironment environment) : ISePayTestModeAccountReader
{
    private const string Endpoint = "https://userapi-sandbox.sepay.vn/v2/bank-accounts";
    private const string TransactionsEndpoint = "https://userapi-sandbox.sepay.vn/v2/transactions";
    private const int MaximumPages = 20;

    public async Task<IReadOnlyList<SePayTestModeAccountDto>> GetAccountsAsync(CancellationToken cancellationToken)
    {
        var token = GetToken();

        var accounts = new List<SePayTestModeAccountDto>();
        for (var page = 1; page <= MaximumPages; page++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{Endpoint}?page={page}&per_page=100");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            HttpResponseMessage received;
            try
            {
                received = await httpClientFactory.CreateClient("SePayTestModeAccounts")
                    .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            }
            catch (HttpRequestException)
            {
                throw new AppException("sepay_test_mode_provider_unavailable", "SePay Test Mode could not be reached.", 503);
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new AppException("sepay_test_mode_provider_unavailable", "SePay Test Mode timed out.", 503);
            }
            using var response = received;

            if (!response.IsSuccessStatusCode)
                throw new AppException(response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden
                    ? "sepay_test_mode_auth_failed" : "sepay_test_mode_provider_failed",
                    "SePay Test Mode account lookup failed.", 503);

            try
            {
                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("status", out var status)
                    || status.ValueKind != JsonValueKind.String || status.GetString() != "success"
                    || !root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array
                    || !root.TryGetProperty("meta", out var meta) || meta.ValueKind != JsonValueKind.Object
                    || !meta.TryGetProperty("pagination", out var pagination) || pagination.ValueKind != JsonValueKind.Object
                    || !pagination.TryGetProperty("has_more", out var hasMore)
                    || hasMore.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                    throw InvalidResponse();

                foreach (var item in data.EnumerateArray())
                    accounts.Add(MapAccount(item));
                if (hasMore.ValueKind == JsonValueKind.False)
                    return accounts;
            }
            catch (JsonException)
            {
                throw InvalidResponse();
            }
        }

        throw new AppException("sepay_test_mode_page_limit", "SePay Test Mode returned too many account pages.", 502);
    }

    public async Task<IReadOnlyList<SePayTestModeTransactionDto>> GetTransactionsAsync(
        string reference,
        CancellationToken cancellationToken)
    {
        var token = GetToken();
        var normalizedReference = reference?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedReference) || normalizedReference.Length > 64
            || normalizedReference.Any(character => !char.IsAsciiLetterOrDigit(character)))
            throw new AppException("invalid_input", "A valid SePay reference is required.");

        var transactions = new List<SePayTestModeTransactionDto>();
        for (var page = 1; page <= MaximumPages; page++)
        {
            var query = Uri.EscapeDataString(normalizedReference);
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"{TransactionsEndpoint}?q={query}&timestamp_format=iso8601&page={page}&per_page=100");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await SendAsync(request, cancellationToken);
            try
            {
                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                var root = document.RootElement;
                if (!TryReadEnvelope(root, out var data, out var hasMore))
                    throw InvalidTransactionResponse();
                foreach (var item in data.EnumerateArray())
                    transactions.Add(MapTransaction(item));
                if (!hasMore)
                    return transactions;
            }
            catch (JsonException)
            {
                throw InvalidTransactionResponse();
            }
        }

        throw new AppException("sepay_test_mode_page_limit", "SePay Test Mode returned too many transaction pages.", 502);
    }

    private string GetToken()
    {
        if (!environment.IsDevelopment())
            throw new NotFoundException("sepay_test_mode_unavailable", "Test Mode access is unavailable.");
        var token = configuration["SePayTestMode:ApiToken"];
        if (string.IsNullOrWhiteSpace(token))
            throw new AppException("sepay_test_mode_not_configured", "SePay Test Mode API token is not configured.", 503);
        token = token.Trim();
        if (token.Any(char.IsControl))
            throw new AppException("sepay_test_mode_not_configured", "SePay Test Mode API token is invalid.", 503);
        return token;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await httpClientFactory.CreateClient("SePayTestModeAccounts")
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (HttpRequestException)
        {
            throw new AppException("sepay_test_mode_provider_unavailable", "SePay Test Mode could not be reached.", 503);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AppException("sepay_test_mode_provider_unavailable", "SePay Test Mode timed out.", 503);
        }
        if (!response.IsSuccessStatusCode)
        {
            response.Dispose();
            throw new AppException(response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden
                ? "sepay_test_mode_auth_failed" : "sepay_test_mode_provider_failed",
                "SePay Test Mode lookup failed.", 503);
        }
        return response;
    }

    private static bool TryReadEnvelope(JsonElement root, out JsonElement data, out bool hasMore)
    {
        data = default;
        hasMore = false;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("status", out var status)
            || status.ValueKind != JsonValueKind.String || status.GetString() != "success"
            || !root.TryGetProperty("data", out data) || data.ValueKind != JsonValueKind.Array
            || !root.TryGetProperty("meta", out var meta) || meta.ValueKind != JsonValueKind.Object
            || !meta.TryGetProperty("pagination", out var pagination) || pagination.ValueKind != JsonValueKind.Object
            || !pagination.TryGetProperty("has_more", out var more)
            || more.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            return false;
        hasMore = more.ValueKind == JsonValueKind.True;
        return true;
    }

    private static SePayTestModeTransactionDto MapTransaction(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object
            || !Guid.TryParse(RequiredString(item, "id", 60), out var transactionId)
            || transactionId == Guid.Empty
            || !Guid.TryParse(RequiredString(item, "bank_account_id", 60), out var accountId)
            || accountId == Guid.Empty)
            throw InvalidTransactionResponse();
        var direction = RequiredString(item, "transfer_type", 10).ToLowerInvariant();
        if (direction is not ("in" or "out"))
            throw InvalidTransactionResponse();
        var amountIn = RequiredIntegerMoney(item, "amount_in");
        var amountOut = RequiredIntegerMoney(item, "amount_out");
        var accumulated = RequiredIntegerMoney(item, "accumulated");
        if ((direction == "in" && (amountIn <= 0 || amountOut != 0))
            || (direction == "out" && (amountOut <= 0 || amountIn != 0)))
            throw InvalidTransactionResponse();
        if (!DateTimeOffset.TryParse(
                RequiredString(item, "transaction_date", 50),
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind,
                out var occurredAt))
            throw InvalidTransactionResponse();
        return new SePayTestModeTransactionDto(
            transactionId,
            accountId,
            direction,
            amountIn,
            amountOut,
            accumulated,
            RequiredString(item, "transaction_content", 1000),
            OptionalString(item, "reference_number", 200),
            OptionalString(item, "code", 200),
            occurredAt.UtcDateTime);
    }

    private static decimal RequiredIntegerMoney(JsonElement item, string name)
    {
        if (!item.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Number
            || !value.TryGetDecimal(out var amount) || amount < 0 || decimal.Truncate(amount) != amount)
            throw InvalidTransactionResponse();
        return amount;
    }

    private static string? OptionalString(JsonElement item, string name, int maxLength)
    {
        if (!item.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
            return null;
        if (value.ValueKind != JsonValueKind.String)
            throw InvalidTransactionResponse();
        var text = value.GetString()?.Trim();
        return text?.Length > maxLength ? throw InvalidTransactionResponse() : text;
    }

    private static SePayTestModeAccountDto MapAccount(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object)
            throw InvalidResponse();
        var idText = RequiredString(item, "id", 60);
        if (!Guid.TryParse(idText, out var id) || id == Guid.Empty)
            throw InvalidResponse();
        var bankName = RequiredString(item, "bank_short_name", 160);
        var accountNumber = RequiredString(item, "account_number", 34);
        if (accountNumber.Length < 4 || !accountNumber.All(char.IsAsciiDigit))
            throw InvalidResponse();
        var accountType = RequiredString(item, "account_type", 80);
        if (!item.TryGetProperty("accumulated", out var balance)
            || balance.ValueKind != JsonValueKind.Number
            || !balance.TryGetDecimal(out var amount) || amount < 0 || decimal.Truncate(amount) != amount)
            throw InvalidResponse();
        if (!item.TryGetProperty("active", out var active))
            throw InvalidResponse();
        var isActive = active.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when active.TryGetInt32(out var value) && value is 0 or 1 => value == 1,
            JsonValueKind.String when active.GetString() is "true" or "1" => true,
            JsonValueKind.String when active.GetString() is "false" or "0" => false,
            _ => throw InvalidResponse()
        };
        return new SePayTestModeAccountDto(id, bankName, "****" + accountNumber[^4..],
            accountType, isActive ? "Active" : "Inactive", amount);
    }

    private static string RequiredString(JsonElement item, string name, int maxLength)
    {
        if (!item.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
            throw InvalidResponse();
        var text = value.GetString()?.Trim();
        return string.IsNullOrWhiteSpace(text) || text.Length > maxLength ? throw InvalidResponse() : text;
    }

    private static AppException InvalidResponse() =>
        new("sepay_test_mode_invalid_response", "SePay Test Mode returned invalid account data.", 502);

    private static AppException InvalidTransactionResponse() =>
        new("sepay_test_mode_invalid_response", "SePay Test Mode returned invalid transaction data.", 502);
}
