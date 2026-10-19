using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SpendWise.Application.AI;
using SpendWise.Application.Identity;
using SpendWise.Application.Features.AdminAi;
using SpendWise.Domain.Admin;
using SpendWise.Domain.AI;
using SpendWise.Domain.Finance;
using SpendWise.Domain.Identity;
using SpendWise.Infrastructure.Persistence;
using Xunit;
using S = DocumentFormat.OpenXml.Spreadsheet;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace SpendWise.Tests;

public sealed class AdminAiBackendTests
{
    [Fact]
    public async Task SystemAdmin_crud_is_authorized_owner_only_and_excludes_user_conversations()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var first = await RegisterAsync(client, "First admin");
        var second = await RegisterAsync(client, "Second admin");
        var normalUser = await RegisterAsync(client, "Normal user");

        UseToken(client, normalUser.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.PostAsJsonAsync("/api/admin/ai/conversations", new CreateAiConversationRequest("Denied"))).StatusCode);

        await PromoteToAdminAsync(factory.Services, first.User.Id);
        await PromoteToAdminAsync(factory.Services, second.User.Id);
        first = await LoginAsync(client, first.User.Email);
        second = await LoginAsync(client, second.User.Email);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            db.AiConversations.Add(new AiConversation
            {
                UserId = first.User.Id,
                Title = "Normal user context",
                ContextType = AiConversationContextType.General
            });
            await db.SaveChangesAsync();
        }

        UseToken(client, first.AccessToken);
        var createResponse = await client.PostAsJsonAsync(
            "/api/admin/ai/conversations", new CreateAiConversationRequest("Operations"));
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        var created = (await createResponse.Content.ReadFromJsonAsync<AiConversationDto>())!;
        Assert.Equal("Admin", created.ContextType);

        var list = await client.GetFromJsonAsync<IReadOnlyList<AiConversationDto>>("/api/admin/ai/conversations");
        Assert.Equal(created.Id, Assert.Single(list!).Id);

        UseToken(client, second.AccessToken);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/admin/ai/conversations/{created.Id}")).StatusCode);

        UseToken(client, first.AccessToken);
        var update = await client.PutAsJsonAsync($"/api/admin/ai/conversations/{created.Id}",
            new UpdateAiConversationRequest("Pinned operations", true));
        update.EnsureSuccessStatusCode();
        var updated = (await update.Content.ReadFromJsonAsync<AiConversationDto>())!;
        Assert.Equal("Pinned operations", updated.Title);
        Assert.True(updated.IsPinned);
        Assert.Equal(created.Id,
            (await client.GetFromJsonAsync<AiConversationDto>($"/api/admin/ai/conversations/{created.Id}"))!.Id);

        Assert.Equal(HttpStatusCode.NoContent,
            (await client.DeleteAsync($"/api/admin/ai/conversations/{created.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/admin/ai/conversations/{created.Id}")).StatusCode);
    }

    [Fact]
    public async Task Admin_chat_uses_provider_supplies_safe_context_persists_messages_and_is_read_only()
    {
        var provider = new RecordingAiProvider("Current system state is healthy.", "Gemini", false);
        using var baseFactory = new SpendWiseApiFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAiCompletionProvider>();
            services.AddSingleton<IAiCompletionProvider>(provider);
        }));
        using var client = CreateHttpsClient(factory);
        var admin = await RegisterAsync(client, "Context admin");
        await PromoteToAdminAsync(factory.Services, admin.User.Id);
        admin = await LoginAsync(client, admin.User.Email);

        await SeedAiFlagAndSupportCaseAsync(factory.Services, admin.User.Id);
        var before = await BusinessCountsAsync(factory.Services);
        UseToken(client, admin.AccessToken);
        var response = await client.PostAsJsonAsync("/api/admin/ai/chat",
            new AiChatRequest(null, "Tóm tắt tình trạng hệ thống hôm nay."));

        response.EnsureSuccessStatusCode();
        var chat = (await response.Content.ReadFromJsonAsync<AiChatResponse>())!;
        Assert.Equal("Gemini", chat.Provider);
        Assert.False(chat.IsFallback);
        Assert.Null(chat.ProposedAction);
        var captured = Assert.Single(provider.Requests);
        Assert.Equal("SystemAdmin", captured.SystemRole);
        Assert.Contains("SpendWise SystemAdmin assistant", captured.SystemInstruction);
        Assert.Empty(captured.Tools);
        Assert.Empty(captured.Attachments);
        Assert.Contains("\"awaiting\":1", captured.AuthorizedContextJson);
        Assert.DoesNotContain(admin.User.Email, captured.AuthorizedContextJson);

        var conversation = await client.GetFromJsonAsync<AiConversationDto>(
            $"/api/admin/ai/conversations/{chat.ConversationId}");
        Assert.Equal(2, conversation!.Messages.Count);
        Assert.Equal("User", conversation.Messages[0].Role);
        Assert.Equal("Assistant", conversation.Messages[1].Role);
        Assert.Equal(before, await BusinessCountsAsync(factory.Services));
    }

    [Fact]
    public async Task Clearly_unrelated_admin_question_is_refused_without_calling_provider()
    {
        var provider = new RecordingAiProvider("must not be used", "Gemini", false);
        using var baseFactory = new SpendWiseApiFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAiCompletionProvider>();
            services.AddSingleton<IAiCompletionProvider>(provider);
        }));
        using var client = CreateHttpsClient(factory);
        var admin = await RegisterAsync(client, "Scope admin");
        await PromoteToAdminAsync(factory.Services, admin.User.Id);
        admin = await LoginAsync(client, admin.User.Email);
        await SeedAiFlagAsync(factory.Services);
        UseToken(client, admin.AccessToken);

        var response = await client.PostAsJsonAsync("/api/admin/ai/chat",
            new AiChatRequest(null, "Phùng Thanh Độ là ai?"));

        response.EnsureSuccessStatusCode();
        var chat = (await response.Content.ReadFromJsonAsync<AiChatResponse>())!;
        Assert.Equal("SpendWisePolicy", chat.Provider);
        Assert.Contains("chỉ hỗ trợ", chat.Message);
        Assert.Empty(provider.Requests);
        var conversation = await client.GetFromJsonAsync<AiConversationDto>(
            $"/api/admin/ai/conversations/{chat.ConversationId}");
        Assert.Equal(2, conversation!.Messages.Count);
    }

    [Fact]
    public async Task User_ai_cannot_read_admin_conversation_after_same_account_role_change()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var actor = await RegisterAsync(client, "Role change actor");
        await PromoteToAdminAsync(factory.Services, actor.User.Id);
        var admin = await LoginAsync(client, actor.User.Email);
        UseToken(client, admin.AccessToken);
        var created = (await (await client.PostAsJsonAsync("/api/admin/ai/conversations",
            new CreateAiConversationRequest("Private admin history"))).Content.ReadFromJsonAsync<AiConversationDto>())!;

        await SetRoleAsync(factory.Services, actor.User.Id, SystemRole.User);
        var user = await LoginAsync(client, actor.User.Email);
        UseToken(client, user.AccessToken);

        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/ai/conversations/{created.Id}")).StatusCode);
        var list = await client.GetFromJsonAsync<IReadOnlyList<AiConversationDto>>("/api/ai/conversations");
        Assert.Empty(list!);
    }

    [Fact]
    public async Task Normal_user_cannot_export_admin_ai_report()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var user = await RegisterAsync(client, "Report normal user");
        UseToken(client, user.AccessToken);

        var response = await client.PostAsJsonAsync("/api/admin/ai/reports/export",
            new AdminAiReportRequest("Word"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Word_report_is_valid_contains_seeded_aggregates_and_excludes_private_data()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var admin = await RegisterAsync(client, "Word report admin");
        await PromoteToAdminAsync(factory.Services, admin.User.Id);
        admin = await LoginAsync(client, admin.User.Email);
        await SeedReportStateAsync(factory.Services, admin.User.Id);
        var before = await BusinessCountsAsync(factory.Services);
        UseToken(client, admin.AccessToken);

        var response = await client.PostAsJsonAsync("/api/admin/ai/reports/export",
            new { format = "Word", reportType = "SystemOverview" });

        response.EnsureSuccessStatusCode();
        Assert.Equal("application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            response.Content.Headers.ContentType!.MediaType);
        Assert.EndsWith(".docx", DownloadFileName(response), StringComparison.OrdinalIgnoreCase);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 1000);
        using var document = WordprocessingDocument.Open(new MemoryStream(bytes), false);
        var validationErrors = new OpenXmlValidator().Validate(document).ToList();
        Assert.True(validationErrors.Count == 0,
            string.Join(Environment.NewLine, validationErrors.Select(x => $"{x.Description} Node={x.Node?.OuterXml}")));
        var text = string.Join(" ", document.MainDocumentPart!.Document!.Descendants<W.Text>().Select(x => x.Text));
        Assert.Contains("Support cases awaiting 1", text);
        Assert.Contains("Ledger completed 1", text);
        Assert.Contains("Audit events in window 1", text);
        Assert.DoesNotContain(admin.User.Email, text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PRIVATE-REPORT-MARKER", text);
        Assert.DoesNotContain("PasswordHash", text, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, await BusinessCountsAsync(factory.Services));
    }

    [Fact]
    public async Task Excel_report_is_valid_contains_seeded_aggregates_and_excludes_private_data()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var admin = await RegisterAsync(client, "Excel report admin");
        await PromoteToAdminAsync(factory.Services, admin.User.Id);
        admin = await LoginAsync(client, admin.User.Email);
        await SeedReportStateAsync(factory.Services, admin.User.Id);
        var before = await BusinessCountsAsync(factory.Services);
        UseToken(client, admin.AccessToken);

        var response = await client.PostAsJsonAsync("/api/admin/ai/reports/export",
            new AdminAiReportRequest("Excel", "SystemOverview", 30));

        response.EnsureSuccessStatusCode();
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            response.Content.Headers.ContentType!.MediaType);
        Assert.EndsWith(".xlsx", DownloadFileName(response), StringComparison.OrdinalIgnoreCase);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 1000);
        using var document = SpreadsheetDocument.Open(new MemoryStream(bytes), false);
        Assert.Empty(new OpenXmlValidator().Validate(document));
        var text = string.Join(" ", document.WorkbookPart!.WorksheetParts
            .SelectMany(x => x.Worksheet!.Descendants<S.Text>()).Select(x => x.Text));
        Assert.Contains("Support cases awaiting 1", text);
        Assert.Contains("Ledger completed 1", text);
        Assert.Contains("Audit events in window 1", text);
        Assert.DoesNotContain(admin.User.Email, text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PRIVATE-REPORT-MARKER", text);
        Assert.DoesNotContain("PasswordHash", text, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, await BusinessCountsAsync(factory.Services));
    }

    [Fact]
    public async Task Report_export_rejects_invalid_format_type_and_period()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var admin = await RegisterAsync(client, "Invalid report admin");
        await PromoteToAdminAsync(factory.Services, admin.User.Id);
        admin = await LoginAsync(client, admin.User.Email);
        UseToken(client, admin.AccessToken);

        await AssertReportErrorAsync(client,
            new AdminAiReportRequest("Pdf", "SystemOverview", 30), "invalid_admin_ai_report_format");
        await AssertReportErrorAsync(client,
            new AdminAiReportRequest("Word", "PrivateRecords", 30), "invalid_admin_ai_report_type");
        await AssertReportErrorAsync(client,
            new AdminAiReportRequest("Excel", "SystemOverview", 0), "invalid_admin_ai_report_period");
        await AssertReportErrorAsync(client,
            new AdminAiReportRequest("Excel", "SystemOverview", 366), "invalid_admin_ai_report_period");
    }

    [Fact]
    public async Task Severe_admin_profanity_is_rejected_before_provider_call()
    {
        var provider = new RecordingAiProvider("must not be used", "Gemini", false);
        using var baseFactory = new SpendWiseApiFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAiCompletionProvider>();
            services.AddSingleton<IAiCompletionProvider>(provider);
        }));
        using var client = CreateHttpsClient(factory);
        var admin = await RegisterAsync(client, "Profanity admin");
        await PromoteToAdminAsync(factory.Services, admin.User.Id);
        admin = await LoginAsync(client, admin.User.Email);
        await SeedAiFlagAsync(factory.Services);
        UseToken(client, admin.AccessToken);

        foreach (var message in new[] { "fuck", "đụ", "địt" })
        {
            var response = await client.PostAsJsonAsync("/api/admin/ai/chat", new AiChatRequest(null, message));
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            using var error = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.EndsWith("/content_rejected", error.RootElement.GetProperty("type").GetString());
        }
        Assert.Empty(provider.Requests);
    }

    private static HttpClient CreateHttpsClient(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>().Database.EnsureCreated();
        return client;
    }

    private static async Task<AuthResponse> RegisterAsync(HttpClient client, string name)
    {
        client.DefaultRequestHeaders.Authorization = null;
        return await IdentityTestData.RegisterAndSetupPinAsync(client, name);
    }

    private static async Task<AuthResponse> LoginAsync(HttpClient client, string email)
    {
        client.DefaultRequestHeaders.Authorization = null;
        return await IdentityTestData.LoginAndVerifyPinAsync(client, email);
    }

    private static void UseToken(HttpClient client, string accessToken) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

    private static Task PromoteToAdminAsync(IServiceProvider services, Guid userId) =>
        SetRoleAsync(services, userId, SystemRole.SystemAdmin);

    private static async Task SetRoleAsync(IServiceProvider services, Guid userId, SystemRole role)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        var user = await db.Users.FindAsync(userId);
        user!.SystemRole = role;
        await db.SaveChangesAsync();
    }

    private static async Task SeedAiFlagAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        if (!await db.FeatureFlags.AnyAsync(x => x.Name == "AIAssistant"))
        {
            db.FeatureFlags.Add(new FeatureFlag { Name = "AIAssistant", IsEnabled = true, Description = "AI" });
            await db.SaveChangesAsync();
        }
    }

    private static async Task SeedAiFlagAndSupportCaseAsync(IServiceProvider services, Guid userId)
    {
        await SeedAiFlagAsync(services);
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        db.SupportCases.Add(new SupportCase
        {
            TicketNumber = $"TEST-{Guid.NewGuid():N}",
            LinkedUserId = userId,
            Category = "operations",
            Subject = "Pending support",
            Description = "Test aggregate only",
            Status = CaseStatus.Open
        });
        await db.SaveChangesAsync();
    }

    private static async Task SeedReportStateAsync(IServiceProvider services, Guid userId)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        db.SupportCases.Add(new SupportCase
        {
            TicketNumber = $"REPORT-{Guid.NewGuid():N}",
            LinkedUserId = userId,
            Category = "private-category",
            Subject = "PRIVATE-REPORT-MARKER",
            Description = "PRIVATE-REPORT-MARKER",
            Status = CaseStatus.Open
        });
        db.LedgerTransactions.Add(new LedgerTransaction
        {
            Reference = $"REPORT-{Guid.NewGuid():N}",
            BusinessType = "ReportTest",
            BusinessId = Guid.NewGuid(),
            Description = "PRIVATE-REPORT-MARKER",
            Status = LedgerTransactionStatus.Completed,
            CompletedAt = DateTime.UtcNow
        });
        db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = userId,
            Action = "PRIVATE-REPORT-MARKER",
            TargetType = "ReportTest",
            Reason = "PRIVATE-REPORT-MARKER",
            MetadataJson = "{\"secret\":\"PRIVATE-REPORT-MARKER\"}"
        });
        await db.SaveChangesAsync();
    }

    private static async Task AssertReportErrorAsync(
        HttpClient client,
        AdminAiReportRequest request,
        string expectedCode)
    {
        var response = await client.PostAsJsonAsync("/api/admin/ai/reports/export", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var error = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.EndsWith($"/{expectedCode}", error.RootElement.GetProperty("type").GetString());
    }

    private static string DownloadFileName(HttpResponseMessage response) =>
        (response.Content.Headers.ContentDisposition?.FileNameStar
            ?? response.Content.Headers.ContentDisposition?.FileName
            ?? string.Empty).Trim('"');

    private static async Task<(int Users, int Transactions, int Ledger, int Support, int Payments, int Flags, int Audit)> BusinessCountsAsync(
        IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        return (await db.Users.CountAsync(), await db.FinancialTransactions.CountAsync(), await db.LedgerTransactions.CountAsync(),
            await db.SupportCases.CountAsync(), await db.BankDirectPayments.CountAsync(), await db.FeatureFlags.CountAsync(),
            await db.AuditLogs.CountAsync());
    }

    private sealed class RecordingAiProvider(string answer, string provider, bool fallback) : IAiCompletionProvider
    {
        public List<AiProviderRequest> Requests { get; } = [];

        public Task<AiCompletionResult> CompleteAsync(AiProviderRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(new AiCompletionResult(answer, provider, fallback));
        }
    }
}
