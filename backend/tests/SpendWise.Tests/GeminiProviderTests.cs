using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using SpendWise.Application.AI;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Infrastructure.AI;
using Xunit;

namespace SpendWise.Tests;

public sealed class GeminiProviderTests
{
    [Fact]
    public async Task Successful_response_maps_to_existing_completion_contract()
    {
        HttpRequestMessage? captured = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            captured = request;
            return JsonResponse("""{"candidates":[{"content":{"role":"model","parts":[{"text":"Gemini answer"}]}}]}""");
        });
        var provider = CreateProvider(handler, GeminiConfiguration());

        var result = await provider.CompleteAsync(EmptyRequest(), default);

        Assert.Equal("Gemini answer", result.Message);
        Assert.Equal("Gemini", result.Provider);
        Assert.False(result.IsFallback);
        Assert.Equal("test-key", captured!.Headers.GetValues("x-goog-api-key").Single());
        Assert.Equal("https://generativelanguage.googleapis.com/v1beta/models/gemini-3.5-flash-lite:generateContent",
            captured.RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task Function_declaration_uses_full_json_schema_field_and_preserves_additional_properties()
    {
        string? requestBody = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            requestBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return JsonResponse("""{"candidates":[{"content":{"parts":[{"text":"Schema accepted"}]}}]}""");
        });
        var schema = """{"type":"object","properties":{"amount":{"type":"number"}},"additionalProperties":false}""";
        var request = EmptyRequest() with
        {
            Tools = [new AiToolDefinition("test_tool", "A harmless test tool.", schema)]
        };

        await CreateProvider(handler, GeminiConfiguration()).CompleteAsync(request, default);

        using var json = JsonDocument.Parse(requestBody!);
        var declaration = json.RootElement.GetProperty("tools")[0]
            .GetProperty("functionDeclarations")[0];
        Assert.False(declaration.TryGetProperty("parameters", out _));
        var submittedSchema = declaration.GetProperty("parametersJsonSchema");
        Assert.False(submittedSchema.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal("number", submittedSchema.GetProperty("properties").GetProperty("amount").GetProperty("type").GetString());
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"candidates\":[{\"content\":{\"parts\":[]}}]}")]
    public async Task Malformed_or_empty_response_is_handled_as_provider_unavailable(string responseBody)
    {
        var provider = CreateProvider(new StubHttpMessageHandler(_ => JsonResponse(responseBody)), GeminiConfiguration());

        var error = await Assert.ThrowsAsync<AppException>(() => provider.CompleteAsync(EmptyRequest(), default));

        Assert.Equal("AI_PROVIDER_UNAVAILABLE", error.Code);
        Assert.Equal(503, error.StatusCode);
    }

    [Fact]
    public async Task Missing_key_returns_provider_unavailable_without_network_call()
    {
        var called = false;
        var provider = CreateProvider(new StubHttpMessageHandler(_ =>
        {
            called = true;
            return JsonResponse("{}");
        }), new ConfigurationBuilder().Build());

        var error = await Assert.ThrowsAsync<AppException>(() => provider.CompleteAsync(EmptyRequest(), default));

        Assert.Equal("AI_PROVIDER_UNAVAILABLE", error.Code);
        Assert.False(called);
    }

    [Fact]
    public async Task OpenAi_selection_keeps_existing_provider_behavior()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AI:Provider"] = "OpenAI",
            ["OpenAI:AllowDevelopmentFallback"] = "true"
        }).Build();
        var selector = new ConfiguredAiCompletionProvider(
            configuration,
            new OpenAiCompletionProvider(configuration, new TestHostEnvironment()),
            CreateProvider(new StubHttpMessageHandler(_ => JsonResponse("{}")), configuration));

        var result = await selector.CompleteAsync(EmptyRequest(), default);

        Assert.Equal("DevelopmentFallback", result.Provider);
        Assert.True(result.IsFallback);
    }

    [Fact]
    public async Task Gemini_selection_uses_Gemini_provider()
    {
        var configuration = GeminiConfiguration(new Dictionary<string, string?> { ["AI:Provider"] = "Gemini" });
        var gemini = CreateProvider(new StubHttpMessageHandler(_ =>
            JsonResponse("""{"candidates":[{"content":{"parts":[{"text":"Selected Gemini"}]}}]}""")), configuration);
        var selector = new ConfiguredAiCompletionProvider(
            configuration,
            new OpenAiCompletionProvider(configuration, new TestHostEnvironment()),
            gemini);

        var result = await selector.CompleteAsync(EmptyRequest(), default);

        Assert.Equal("Gemini", result.Provider);
        Assert.Equal("Selected Gemini", result.Message);
    }

    [Fact]
    public async Task Admin_request_uses_dedicated_system_instruction_and_authorized_context()
    {
        string? requestBody = null;
        var provider = CreateProvider(new StubHttpMessageHandler(request =>
        {
            requestBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return JsonResponse("""{"candidates":[{"content":{"parts":[{"text":"Admin summary"}]}}]}""");
        }), GeminiConfiguration());
        var request = EmptyRequest() with
        {
            SystemRole = "SystemAdmin",
            SystemInstruction = "You are the SpendWise SystemAdmin assistant.",
            AuthorizedContextJson = "{\"support\":{\"open\":3}}"
        };

        await provider.CompleteAsync(request, default);

        using var json = JsonDocument.Parse(requestBody!);
        var instruction = json.RootElement.GetProperty("systemInstruction").GetProperty("parts")[0]
            .GetProperty("text").GetString();
        Assert.Contains("SpendWise SystemAdmin assistant", instruction);
        Assert.Contains("\"open\":3", instruction);
        Assert.DoesNotContain("in-app financial and product assistant", instruction);
    }

    private static GeminiCompletionProvider CreateProvider(HttpMessageHandler handler, IConfiguration configuration) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://generativelanguage.googleapis.com/") }, configuration);

    private static IConfiguration GeminiConfiguration(Dictionary<string, string?>? additional = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["Gemini:ApiKey"] = "test-key",
            ["Gemini:Model"] = "gemini-3.5-flash-lite"
        };
        if (additional is not null)
            foreach (var item in additional) values[item.Key] = item.Value;
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private static AiProviderRequest EmptyRequest() => new(
        "User",
        "vi-VN",
        new AiFinancialContext(0, 0, new Dictionary<string, decimal>(), [], [], null),
        [],
        "Hello",
        [],
        [],
        (_, _) => Task.FromResult("{}"));

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(responseFactory(request));
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "SpendWise.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
