using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SpendWise.Application.Banking;
using SpendWise.Application.Common.Exceptions;

namespace SpendWise.Infrastructure.Banking;

// Đọc options động, xác thực secret và chỉ trả trạng thái cấu hình an toàn cho Admin.
public sealed class SePayBankHubConfiguration(
    IOptionsMonitor<SePayBankHubOptions> options,
    IHostEnvironment hostEnvironment) : IBankHubConfiguration
{
    private long lastSuccessfulProviderCallTicks;
    private long lastWebhookReceivedTicks;

    public bool IsSandbox => options.CurrentValue.Environment == SePayBankHubEnvironment.Sandbox;

    public void EnsureAvailable()
    {
        var current = options.CurrentValue;
        if (!current.Enabled
            || string.IsNullOrWhiteSpace(current.ClientId)
            || string.IsNullOrWhiteSpace(current.ClientSecret)
            || string.IsNullOrWhiteSpace(current.CompanyXid))
        {
            throw new AppException(
                "bank_integration_unavailable",
                "Bank integration is disabled or not configured.",
                503);
        }
    }

    public string ResolveCompletionRedirectUri(string? requestedUri)
    {
        var current = options.CurrentValue;
        var selected = string.IsNullOrWhiteSpace(requestedUri)
            ? current.CompletionRedirectUri
            : requestedUri.Trim();
        if (!Uri.TryCreate(selected, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || (!hostEnvironment.IsDevelopment() && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new AppException("invalid_redirect_uri", "Completion redirect URI is invalid.");
        }

        var origin = uri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
        var allowed = current.AllowedCompletionRedirectOrigins.Any(candidate =>
            string.Equals(candidate.TrimEnd('/'), origin, StringComparison.OrdinalIgnoreCase));
        if (!allowed)
        {
            throw new AppException("invalid_redirect_uri", "Completion redirect origin is not allowed.");
        }
        return uri.AbsoluteUri;
    }

    public string ResolveMobileCompletionRedirectUri(string? requestedUri)
    {
        if (!string.IsNullOrWhiteSpace(requestedUri))
        {
            throw new AppException(
                "invalid_redirect_uri",
                "Mobile completion redirect URI is controlled by the server.");
        }

        var selected = options.CurrentValue.MobileCompletionRedirectUri;
        if (string.IsNullOrWhiteSpace(selected))
        {
            throw new AppException(
                "bank_mobile_completion_unavailable",
                "Mobile bank linking is not configured.",
                503);
        }

        if (!Uri.TryCreate(selected, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new AppException(
                "bank_mobile_completion_unavailable",
                "Mobile bank linking is not configured.",
                503);
        }

        var origin = uri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
        var allowed = options.CurrentValue.AllowedCompletionRedirectOrigins.Any(candidate =>
            string.Equals(candidate.TrimEnd('/'), origin, StringComparison.OrdinalIgnoreCase));
        if (!allowed)
        {
            throw new AppException(
                "bank_mobile_completion_unavailable",
                "Mobile bank linking is not configured.",
                503);
        }

        return uri.AbsoluteUri;
    }

    public bool VerifyLifecycleSecret(string suppliedSecret)
    {
        var expected = options.CurrentValue.LifecycleWebhookSecret;
        return !string.IsNullOrWhiteSpace(expected)
            && FixedEquals(expected, suppliedSecret);
    }

    public bool VerifyIpnAuthorization(string authorizationHeader)
    {
        const string prefix = "Apikey ";
        var expected = options.CurrentValue.IpnApiKey;
        return !string.IsNullOrWhiteSpace(expected)
            && authorizationHeader.StartsWith(prefix, StringComparison.Ordinal)
            && FixedEquals(expected, authorizationHeader[prefix.Length..]);
    }

    public SePayBankHubStatusDto GetSafeStatus()
    {
        var current = options.CurrentValue;
        return new SePayBankHubStatusDto(
            current.Enabled,
            current.Environment.ToString(),
            current.Enabled
                && !string.IsNullOrWhiteSpace(current.ClientId)
                && !string.IsNullOrWhiteSpace(current.ClientSecret)
                && !string.IsNullOrWhiteSpace(current.CompanyXid),
            !string.IsNullOrWhiteSpace(current.CompanyXid),
            !string.IsNullOrWhiteSpace(current.LifecycleWebhookSecret),
            !string.IsNullOrWhiteSpace(current.IpnApiKey),
            ReadTimestamp(lastSuccessfulProviderCallTicks),
            ReadTimestamp(lastWebhookReceivedTicks));
    }

    public void MarkWebhookReceived(DateTime receivedAt) =>
        Interlocked.Exchange(ref lastWebhookReceivedTicks, receivedAt.ToUniversalTime().Ticks);

    internal void MarkProviderCallSucceeded(DateTime receivedAt) =>
        Interlocked.Exchange(ref lastSuccessfulProviderCallTicks, receivedAt.ToUniversalTime().Ticks);

    internal SePayBankHubOptions Current => options.CurrentValue;

    private static DateTime? ReadTimestamp(long ticks)
    {
        var value = Interlocked.Read(ref ticks);
        return value == 0 ? null : new DateTime(value, DateTimeKind.Utc);
    }

    private static bool FixedEquals(string expected, string supplied)
    {
        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        var suppliedHash = SHA256.HashData(Encoding.UTF8.GetBytes(supplied ?? string.Empty));
        return CryptographicOperations.FixedTimeEquals(expectedHash, suppliedHash);
    }
}
