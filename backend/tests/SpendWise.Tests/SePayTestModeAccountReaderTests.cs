using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Infrastructure.Banking;
using Xunit;

namespace SpendWise.Tests;

public sealed class SePayTestModeAccountReaderTests
{
    private const string AccountId = "f9e8d7c6-b5a4-3210-fedc-ba0987654321";
    private const string Route = "https://userapi-sandbox.sepay.vn/v2/bank-accounts?page=1&per_page=100";

    [Fact]
    public async Task Development_maps_documented_account_without_exposing_full_number()
    {
        var handler = new ProviderHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(Response("\"accumulated\":15000000,\"active\":1"))
        });
        var reader = CreateReader(handler, "Development", "test-token");

        var account = Assert.Single(await reader.GetAccountsAsync(default));
        Assert.Equal(Guid.Parse(AccountId), account.ProviderAccountId);
        Assert.Equal("TPBank", account.BankName);
        Assert.Equal("****6789", account.MaskedAccountNumber);
        Assert.Equal("personal", account.AccountType);
        Assert.Equal("Active", account.Status);
        Assert.Equal(15_000_000m, account.AccumulatedBalance);
        Assert.Equal(Route, handler.LastUri);
        Assert.Equal("Bearer", handler.LastAuthScheme);
        Assert.Equal("test-token", handler.LastAuthValue);
    }

    [Theory]
    [InlineData("true", "Active")]
    [InlineData("false", "Inactive")]
    [InlineData("1", "Active")]
    [InlineData("0", "Inactive")]
    [InlineData("\"true\"", "Active")]
    [InlineData("\"false\"", "Inactive")]
    [InlineData("\"1\"", "Active")]
    [InlineData("\"0\"", "Inactive")]
    public async Task Maps_only_documented_active_formats(string active, string expectedStatus)
    {
        var handler = new ProviderHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(Response($"\"accumulated\":15000000,\"active\":{active}"))
        });

        var account = Assert.Single(await CreateReader(
            handler,
            "Development",
            "test-token").GetAccountsAsync(default));

        Assert.Equal(expectedStatus, account.Status);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("Testing")]
    public async Task Outside_development_never_calls_provider(string environment)
    {
        var handler = new ProviderHandler(_ => throw new InvalidOperationException("Provider must not be called."));
        var reader = CreateReader(handler, environment, "test-token");
        var error = await Assert.ThrowsAsync<NotFoundException>(() => reader.GetAccountsAsync(default));
        Assert.Equal("sepay_test_mode_unavailable", error.Code);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task Missing_token_fails_before_provider_call()
    {
        var handler = new ProviderHandler(_ => throw new InvalidOperationException("Provider must not be called."));
        var error = await Assert.ThrowsAsync<AppException>(() => CreateReader(handler, "Development", null).GetAccountsAsync(default));
        Assert.Equal("sepay_test_mode_not_configured", error.Code);
        Assert.Equal(0, handler.Calls);
    }

    [Theory]
    [InlineData("\"accumulated\":null,\"active\":1")]
    [InlineData("\"accumulated\":\"15000000\",\"active\":1")]
    [InlineData("\"accumulated\":1.5,\"active\":1")]
    [InlineData("\"accumulated\":-1,\"active\":1")]
    [InlineData("\"accumulated\":100,\"active\":2")]
    [InlineData("\"accumulated\":100,\"active\":null")]
    [InlineData("\"accumulated\":100,\"active\":\"yes\"")]
    [InlineData("\"accumulated\":100,\"active\":\"active\"")]
    [InlineData("\"accumulated\":100,\"active\":\"TRUE\"")]
    public async Task Invalid_balance_or_status_fails_instead_of_reporting_zero(string fields)
    {
        var handler = new ProviderHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(Response(fields))
        });
        var error = await Assert.ThrowsAsync<AppException>(() => CreateReader(handler, "Development", "test-token").GetAccountsAsync(default));
        Assert.Equal("sepay_test_mode_invalid_response", error.Code);
    }

    [Fact]
    public async Task Provider_auth_failure_returns_safe_explicit_error()
    {
        var handler = new ProviderHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("sensitive provider response")
        });
        var error = await Assert.ThrowsAsync<AppException>(() => CreateReader(handler, "Development", "test-token").GetAccountsAsync(default));
        Assert.Equal("sepay_test_mode_auth_failed", error.Code);
        Assert.DoesNotContain("sensitive provider response", error.Message);
    }

    [Fact]
    public async Task Transaction_reader_maps_documented_v2_fields_and_utc_time()
    {
        var transactionId = Guid.NewGuid();
        var handler = new ProviderHandler(request => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
                {"status":"success","data":[{
                  "id":"{{TRANSACTION}}","transaction_date":"2026-09-26T09:30:00+07:00",
                  "transfer_type":"out","amount_in":0,"amount_out":125000,"accumulated":900000,
                  "transaction_content":"Nap SWTPABC","reference_number":"REF-1","code":null,
                  "bank_account_id":"{{ACCOUNT}}"}],"meta":{"pagination":{"has_more":false}}}
                """.Replace("{{TRANSACTION}}", transactionId.ToString()).Replace("{{ACCOUNT}}", AccountId))
        });

        var item = Assert.Single(await CreateReader(handler, "Development", "test-token")
            .GetTransactionsAsync("SWTPABC", default));

        Assert.Equal(transactionId, item.ProviderTransactionId);
        Assert.Equal(Guid.Parse(AccountId), item.ProviderAccountId);
        Assert.Equal("out", item.Direction);
        Assert.Equal(125000m, item.AmountOut);
        Assert.Equal(0m, item.AmountIn);
        Assert.Equal(DateTimeKind.Utc, item.TransactionAt.Kind);
        Assert.Contains("/v2/transactions?q=SWTPABC", handler.LastUri);
    }

    [Theory]
    [InlineData("\"transfer_type\":\"sideways\",\"amount_in\":0,\"amount_out\":100")]
    [InlineData("\"transfer_type\":\"out\",\"amount_in\":1,\"amount_out\":100")]
    [InlineData("\"transfer_type\":\"out\",\"amount_in\":0,\"amount_out\":1.5")]
    public async Task Transaction_reader_rejects_malformed_financial_fields(string fields)
    {
        var handler = new ProviderHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"status\":\"success\",\"data\":[{\"id\":\"" + Guid.NewGuid() +
                "\",\"bank_account_id\":\"" + AccountId + "\",\"transaction_date\":\"2026-09-26T09:30:00+07:00\"," +
                fields + ",\"accumulated\":100,\"transaction_content\":\"SWTPABC\",\"reference_number\":null,\"code\":null}]," +
                "\"meta\":{\"pagination\":{\"has_more\":false}}}")
        });

        var error = await Assert.ThrowsAsync<AppException>(() => CreateReader(handler, "Development", "test-token")
            .GetTransactionsAsync("SWTPABC", default));
        Assert.Equal("sepay_test_mode_invalid_response", error.Code);
    }

    private static string Response(string fields) =>
        "{\"status\":\"success\",\"data\":[{\"id\":\"" + AccountId +
        "\",\"bank_short_name\":\"TPBank\",\"account_number\":\"0123456789\",\"account_type\":\"personal\"," +
        fields + "}],\"meta\":{\"pagination\":{\"has_more\":false}}}";

    private static SePayTestModeAccountReader CreateReader(ProviderHandler handler, string environment, string? token)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["SePayTestMode:ApiToken"] = token
        }).Build();
        return new SePayTestModeAccountReader(new ClientFactory(handler), configuration, new TestEnvironment(environment));
    }

    private sealed class ProviderHandler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public string? LastUri { get; private set; }
        public string? LastAuthScheme { get; private set; }
        public string? LastAuthValue { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            LastUri = request.RequestUri?.ToString();
            LastAuthScheme = request.Headers.Authorization?.Scheme;
            LastAuthValue = request.Headers.Authorization?.Parameter;
            return Task.FromResult(response(request));
        }
    }

    private sealed class ClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class TestEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "SpendWise.Tests";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
