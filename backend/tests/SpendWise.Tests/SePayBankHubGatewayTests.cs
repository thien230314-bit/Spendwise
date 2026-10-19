using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SpendWise.Application.Banking;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Domain.Banking;
using SpendWise.Infrastructure.Banking;
using Xunit;

namespace SpendWise.Tests;

// Các test này xác nhận wire authentication và token cache mà không gọi mạng thật.
public sealed class SePayBankHubGatewayTests
{
    [Theory]
    [InlineData(BankLinkClientPlatform.Web, 0)]
    [InlineData(BankLinkClientPlatform.Mobile, 1)]
    public async Task Link_request_maps_client_platform_to_official_provider_indicator(
        BankLinkClientPlatform clientPlatform,
        int expectedIndicator)
    {
        JsonElement? providerPayload = null;
        var handler = new CallbackHttpHandler(async (request, cancellationToken) =>
        {
            if (request.RequestUri!.AbsolutePath == "/v1/token")
                return Json(HttpStatusCode.Created, """{"access_token":"token","ttl":600}""");

            using var document = JsonDocument.Parse(
                await request.Content!.ReadAsStringAsync(cancellationToken));
            providerPayload = document.RootElement.Clone();
            return LinkResponse();
        });
        using var gateway = Gateway(Options(), handler);

        await gateway.CreateLinkAsync(LinkRequest(clientPlatform), default);

        Assert.Equal(expectedIndicator, providerPayload!.Value.GetProperty("is_mobile_app").GetInt32());
    }

    [Theory]
    [InlineData("http://mobile.spendwise.test/callback", "https://mobile.spendwise.test")]
    [InlineData("https://untrusted.example/callback", "https://mobile.spendwise.test")]
    public void Mobile_completion_must_be_https_and_allowlisted(
        string configuredUri,
        string allowedOrigin)
    {
        var options = Options();
        options.MobileCompletionRedirectUri = configuredUri;
        options.AllowedCompletionRedirectOrigins = [allowedOrigin];
        var configuration = new SePayBankHubConfiguration(
            new StaticOptionsMonitor<SePayBankHubOptions>(options),
            new TestHostEnvironment());

        var error = Assert.Throws<AppException>(() =>
            configuration.ResolveMobileCompletionRedirectUri(null));

        Assert.Equal("bank_mobile_completion_unavailable", error.Code);
        Assert.Equal(503, error.StatusCode);
    }

    [Fact]
    public async Task Token_uses_basic_auth_sandbox_url_and_is_cached()
    {
        // Arrange: fake provider trả một token rồi chấp nhận hai link request.
        var tokenCalls = 0;
        var linkCalls = 0;
        string? basicHeader = null;
        var handler = new CallbackHttpHandler(async (request, cancellationToken) =>
        {
            if (request.RequestUri!.AbsolutePath == "/v1/token")
            {
                Interlocked.Increment(ref tokenCalls);
                basicHeader = request.Headers.Authorization?.ToString();
                return Json(HttpStatusCode.Created, """{"access_token":"cached-token","ttl":600}""");
            }
            Interlocked.Increment(ref linkCalls);
            Assert.Equal("Bearer cached-token", request.Headers.Authorization?.ToString());
            Assert.Equal("bankhub-api-sandbox.sepay.vn", request.RequestUri.Host);
            _ = await request.Content!.ReadAsStringAsync(cancellationToken);
            return LinkResponse();
        });
        using var gateway = Gateway(Options(), handler);

        // Act: hai thao tác dùng chung một access token còn hạn.
        await gateway.CreateLinkAsync(LinkRequest(), default);
        await gateway.CreateLinkAsync(LinkRequest(), default);

        // Assert: Basic chứa đúng client id/secret, nhưng token chỉ được lấy một lần.
        Assert.Equal(1, tokenCalls);
        Assert.Equal(2, linkCalls);
        Assert.StartsWith("Basic ", basicHeader, StringComparison.Ordinal);
        var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(basicHeader![6..]));
        Assert.Equal("client-id:client-secret", decoded);
    }

    [Fact]
    public async Task Concurrent_requests_single_flight_and_ttl_refreshes_early()
    {
        // Arrange: token endpoint chậm một chút để 10 request tranh cache cùng lúc.
        var tokenCalls = 0;
        var clock = new MutableTimeProvider(DateTimeOffset.Parse("2026-09-04T00:00:00Z"));
        var handler = new CallbackHttpHandler(async (request, cancellationToken) =>
        {
            if (request.RequestUri!.AbsolutePath == "/v1/token")
            {
                Interlocked.Increment(ref tokenCalls);
                await Task.Delay(30, cancellationToken);
                return Json(HttpStatusCode.Created, """{"access_token":"ttl-token","ttl":100}""");
            }
            return LinkResponse();
        });
        using var gateway = Gateway(Options(), handler, clock);

        // Act: 10 request đồng thời, sau đó tiến thời gian qua mốc refresh sớm 10 giây.
        await Task.WhenAll(Enumerable.Range(0, 10).Select(_ =>
            gateway.CreateLinkAsync(LinkRequest(), default)));
        clock.Advance(TimeSpan.FromSeconds(91));
        await gateway.CreateLinkAsync(LinkRequest(), default);

        // Assert: batch đầu chỉ lấy một token; request sau expiry sớm lấy token thứ hai.
        Assert.Equal(2, tokenCalls);
    }

    [Fact]
    public async Task Provider_401_refreshes_token_and_retries_exactly_once()
    {
        var tokenCalls = 0;
        var providerCalls = 0;
        var handler = new CallbackHttpHandler((request, _) =>
        {
            if (request.RequestUri!.AbsolutePath == "/v1/token")
            {
                var number = Interlocked.Increment(ref tokenCalls);
                return Task.FromResult(Json(
                    HttpStatusCode.Created,
                    $"{{\"access_token\":\"token-{number}\",\"ttl\":600}}"));
            }
            Interlocked.Increment(ref providerCalls);
            return Task.FromResult(request.Headers.Authorization?.Parameter == "token-1"
                ? new HttpResponseMessage(HttpStatusCode.Unauthorized)
                : LinkResponse());
        });
        using var gateway = Gateway(Options(), handler);

        await gateway.CreateLinkAsync(LinkRequest(), default);

        Assert.Equal(2, tokenCalls);
        Assert.Equal(2, providerCalls);
    }

    [Fact]
    public async Task Second_provider_401_is_not_retried_forever()
    {
        var tokenCalls = 0;
        var providerCalls = 0;
        var handler = new CallbackHttpHandler((request, _) =>
        {
            if (request.RequestUri!.AbsolutePath == "/v1/token")
            {
                var number = Interlocked.Increment(ref tokenCalls);
                return Task.FromResult(Json(
                    HttpStatusCode.Created,
                    $"{{\"access_token\":\"token-{number}\",\"ttl\":600}}"));
            }
            Interlocked.Increment(ref providerCalls);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
        });
        using var gateway = Gateway(Options(), handler);

        var error = await Assert.ThrowsAsync<AppException>(() =>
            gateway.CreateLinkAsync(LinkRequest(), default));

        Assert.Equal("sepay_auth_failed", error.Code);
        Assert.Equal(2, tokenCalls);
        Assert.Equal(2, providerCalls);
    }

    [Fact]
    public async Task Production_environment_selects_production_base_url()
    {
        var hosts = new ConcurrentBag<string>();
        var options = Options();
        options.Environment = SePayBankHubEnvironment.Production;
        var handler = new CallbackHttpHandler((request, _) =>
        {
            hosts.Add(request.RequestUri!.Host);
            return Task.FromResult(request.RequestUri.AbsolutePath == "/v1/token"
                ? Json(HttpStatusCode.Created, """{"access_token":"token","ttl":600}""")
                : LinkResponse());
        });
        using var gateway = Gateway(options, handler);

        await gateway.CreateLinkAsync(LinkRequest(), default);

        Assert.All(hosts, host => Assert.Equal("bankhub-api.sepay.vn", host));
    }

    [Fact]
    public void Public_status_dto_has_no_credentials_or_tokens()
    {
        var names = typeof(SePayBankHubStatusDto).GetProperties().Select(property => property.Name).ToList();
        Assert.DoesNotContain(names, name => name.Contains("Secret", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, name => name.Contains("Token", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, name => name.Contains("ApiKey", StringComparison.OrdinalIgnoreCase));
    }

    private static SePayBankHubGateway Gateway(
        SePayBankHubOptions options,
        HttpMessageHandler handler,
        TimeProvider? clock = null)
    {
        var configuration = new SePayBankHubConfiguration(
            new StaticOptionsMonitor<SePayBankHubOptions>(options),
            new TestHostEnvironment());
        return new SePayBankHubGateway(
            new SingleClientFactory(new HttpClient(handler, false)),
            configuration,
            clock ?? TimeProvider.System);
    }

    private static SePayBankHubOptions Options() => new()
    {
        Enabled = true,
        Environment = SePayBankHubEnvironment.Sandbox,
        ClientId = "client-id",
        ClientSecret = "client-secret",
        CompanyXid = "company-xid",
        CompletionRedirectUri = "https://localhost/callback",
        AllowedCompletionRedirectOrigins = ["https://localhost"]
    };

    private static BankProviderLinkRequest LinkRequest(
        BankLinkClientPlatform clientPlatform = BankLinkClientPlatform.Web) => new(
        BankLinkPurpose.LinkBankAccount,
        "https://localhost/callback",
        "vi",
        ClientPlatform: clientPlatform);

    private static HttpResponseMessage LinkResponse() => Json(
        HttpStatusCode.Created,
        """{"xid":"link-xid","hosted_link_url":"https://provider/link","link_token":"raw-not-persisted","expires_at":"2026-09-04T01:00:00Z"}""");

    private static HttpResponseMessage Json(HttpStatusCode status, string json) => new(status)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private sealed class CallbackHttpHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> callback) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => callback(request, cancellationToken);
    }

    private sealed class SingleClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class StaticOptionsMonitor<T>(T current) : IOptionsMonitor<T>
    {
        public T CurrentValue => current;
        public T Get(string? name) => current;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "SpendWise.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset current = now;
        public override DateTimeOffset GetUtcNow() => current;
        public void Advance(TimeSpan value) => current += value;
    }
}
