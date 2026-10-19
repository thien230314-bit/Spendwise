using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SpendWise.Application.AI;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Identity;
using SpendWise.Domain.Admin;
using SpendWise.Domain.Identity;
using SpendWise.Infrastructure.Persistence;
using Xunit;

namespace SpendWise.Tests;

// Real JWT/session/controllers/handlers/persistence; only the external provider is replaced.
public sealed class AiConcurrencyHttpTests
{
    [Fact]
    public async Task Normal_chat_persists_existing_response_and_releases_capacity()
    {
        using var host = new TestHost();
        var user = await host.SeedAsync();
        using var response = await host.ChatAsync(user.Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var chat = await response.Content.ReadFromJsonAsync<AiChatResponse>();
        Assert.Equal("TestProvider", chat!.Provider);
        Assert.Equal("SpendWise test answer.", chat.Message);
        Assert.False(chat.IsFallback);
        Assert.Equal((1, 2, 0, 0), await host.StateAsync());
        Assert.Equal(HttpStatusCode.OK, (await host.ChatAsync(user.Token)).StatusCode);
    }

    [Fact]
    public async Task Two_active_chats_are_admitted_third_is_429_without_provider_or_state_change()
    {
        using var host = new TestHost();
        var user = await host.SeedAsync();
        var first = await host.StartBlockedAsync(user.Token, "first");
        var second = await host.StartBlockedAsync(user.Token, "second");
        try
        {
            Assert.Equal(2, host.Provider.Calls);
            var state = await host.StateAsync();
            var snapshot = await host.SnapshotAsync();
            using var rejected = await host.ChatAsync(user.Token).WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
            Assert.Equal(2, host.Provider.Calls);
            Assert.Equal(state, await host.StateAsync());
            Assert.Equal(snapshot, await host.SnapshotAsync());
        }
        finally
        {
            host.Provider.ReleaseAll();
            await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(10));
        }
        Assert.Equal(HttpStatusCode.OK, (await host.ChatAsync(user.Token)).StatusCode);
    }

    [Fact]
    public async Task Different_users_can_start_provider_work_while_another_user_is_full()
    {
        using var host = new TestHost();
        var firstUser = await host.SeedAsync();
        var secondUser = await host.SeedAsync();
        var first = await host.StartBlockedAsync(firstUser.Token, "first");
        var second = await host.StartBlockedAsync(firstUser.Token, "second");
        var independent = await host.StartBlockedAsync(secondUser.Token, "independent");
        try
        {
            Assert.Equal(3, host.Provider.Calls);
            Assert.Equal(HttpStatusCode.TooManyRequests, (await host.ChatAsync(firstUser.Token)).StatusCode);
        }
        finally
        {
            host.Provider.ReleaseAll();
            var responses = await Task.WhenAll(first, second, independent);
            Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        }
    }

    [Fact]
    public async Task Three_sessions_of_one_user_share_one_concurrency_partition()
    {
        using var host = new TestHost();
        var firstSession = await host.SeedAsync();
        var secondSession = await host.AddSessionAsync(firstSession.User);
        var thirdSession = await host.AddSessionAsync(firstSession.User);
        var first = await host.StartBlockedAsync(firstSession.Token, "first");
        var second = await host.StartBlockedAsync(secondSession.Token, "second");
        try
        {
            Assert.Equal(HttpStatusCode.TooManyRequests, (await host.ChatAsync(thirdSession.Token)).StatusCode);
            Assert.Equal(2, host.Provider.Calls);
        }
        finally { host.Provider.ReleaseAll(); await Task.WhenAll(first, second); }
    }

    [Fact]
    public async Task Changing_body_headers_and_forwarded_IP_does_not_reset_validated_user_partition()
    {
        using var host = new TestHost();
        var user = await host.SeedAsync();
        var first = await host.StartBlockedAsync(user.Token, "first");
        var second = await host.StartBlockedAsync(user.Token, "second");
        try
        {
            for (var i = 0; i < 3; i++)
                Assert.Equal(HttpStatusCode.TooManyRequests,
                    (await host.ChatAsync(user.Token, claimedUser: Guid.NewGuid())).StatusCode);
            Assert.Equal(2, host.Provider.Calls);
        }
        finally { host.Provider.ReleaseAll(); await Task.WhenAll(first, second); }
    }

    [Fact]
    public async Task History_attachments_and_action_routes_do_not_use_provider_concurrency_capacity()
    {
        using var host = new TestHost();
        var user = await host.SeedAsync();
        var first = await host.StartBlockedAsync(user.Token, "first");
        var second = await host.StartBlockedAsync(user.Token, "second");
        try
        {
            using var listRequest = new HttpRequestMessage(HttpMethod.Get, "/api/ai/conversations");
            listRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", user.Token);
            using var list = await host.Client.SendAsync(listRequest);
            Assert.Equal(HttpStatusCode.OK, list.StatusCode);
            var conversations = await list.Content.ReadFromJsonAsync<List<AiConversationDto>>();
            Assert.Equal(2, conversations!.Count);
            using var detailRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/ai/conversations/{conversations[0].Id}");
            detailRequest.Headers.Authorization = listRequest.Headers.Authorization;
            Assert.Equal(HttpStatusCode.OK, (await host.Client.SendAsync(detailRequest)).StatusCode);

            using var upload = new HttpRequestMessage(HttpMethod.Post, "/api/ai/attachments");
            upload.Headers.Authorization = listRequest.Headers.Authorization;
            var form = new MultipartFormDataContent();
            var file = new ByteArrayContent("SpendWise attachment test"u8.ToArray());
            file.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
            form.Add(file, "file", "test.txt");
            upload.Content = form;
            Assert.Equal(HttpStatusCode.Created, (await host.Client.SendAsync(upload)).StatusCode);

            var missingAction = Guid.NewGuid();
            using var action = new HttpRequestMessage(HttpMethod.Get, $"/api/ai/actions/{missingAction}");
            action.Headers.Authorization = listRequest.Headers.Authorization;
            Assert.Equal(HttpStatusCode.NotFound, (await host.Client.SendAsync(action)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound,
                (await host.ChatAsync(user.Token, path: $"/api/ai/actions/{missingAction}/execute")).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict,
                (await host.ChatAsync(user.Token, path: $"/api/ai/actions/{missingAction}/cancel")).StatusCode);
            Assert.Equal(2, host.Provider.Calls);
        }
        finally { host.Provider.ReleaseAll(); await Task.WhenAll(first, second); }
    }

    [Fact]
    public async Task Existing_fifteen_per_minute_budget_is_shared_with_history_and_rejection_never_calls_provider()
    {
        using var host = new TestHost();
        var user = await host.SeedAsync();
        for (var i = 0; i < 14; i++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/ai/conversations");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", user.Token);
            Assert.Equal(HttpStatusCode.OK, (await host.Client.SendAsync(request)).StatusCode);
        }
        Assert.Equal(HttpStatusCode.OK, (await host.ChatAsync(user.Token)).StatusCode);
        var state = await host.StateAsync();
        Assert.Equal(HttpStatusCode.TooManyRequests, (await host.ChatAsync(user.Token)).StatusCode);
        Assert.Equal(1, host.Provider.Calls);
        Assert.Equal(state, await host.StateAsync());
        var other = await host.SeedAsync();
        Assert.Equal(HttpStatusCode.OK, (await host.ChatAsync(other.Token)).StatusCode);
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData(SystemRole.SystemAdmin, HttpStatusCode.Forbidden)]
    public async Task Anonymous_and_admin_are_not_admitted_to_user_chat(SystemRole? role, HttpStatusCode expected)
    {
        using var host = new TestHost();
        var token = role is null ? null : (await host.SeedAsync(role.Value)).Token;
        Assert.Equal(expected, (await host.ChatAsync(token)).StatusCode);
        Assert.Equal(0, host.Provider.Calls);
        Assert.Equal((0, 0, 0, 0), await host.StateAsync());
    }

    [Fact]
    public async Task Normal_user_cannot_access_admin_chat()
    {
        using var host = new TestHost();
        var user = await host.SeedAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await host.ChatAsync(user.Token, path: "/api/admin/ai/chat")).StatusCode);
        Assert.Equal(0, host.Provider.Calls);
    }

    [Fact]
    public async Task Completion_releases_exactly_one_permit_while_another_chat_is_still_running()
    {
        using var host = new TestHost();
        var user = await host.SeedAsync();
        var first = await host.StartBlockedAsync(user.Token, "first");
        var other = await host.StartBlockedAsync(user.Token, "other");
        Task<HttpResponseMessage>? later = null;
        try
        {
            host.Provider.Block("first").Release.TrySetResult();
            Assert.Equal(HttpStatusCode.OK, (await first).StatusCode);
            later = await host.StartBlockedAsync(user.Token, "later");
            Assert.Equal(HttpStatusCode.TooManyRequests, (await host.ChatAsync(user.Token)).StatusCode);
            Assert.Equal(3, host.Provider.Calls);
        }
        finally
        {
            host.Provider.ReleaseAll();
            await other;
            if (later is not null) await later;
        }
    }

    [Theory]
    [InlineData(false, HttpStatusCode.ServiceUnavailable)]
    [InlineData(true, HttpStatusCode.InternalServerError)]
    public async Task Provider_failure_releases_only_its_permit_and_later_chat_succeeds(bool unexpected, HttpStatusCode expected)
    {
        using var host = new TestHost();
        var user = await host.SeedAsync();
        var plan = host.Provider.Block("failure");
        plan.Failure = unexpected ? new InvalidOperationException("Test provider failure")
            : new AppException("AI_PROVIDER_UNAVAILABLE", "AI provider is unavailable.", 503);
        var failure = await host.StartBlockedAsync(user.Token, "failure");
        var other = await host.StartBlockedAsync(user.Token, "other");
        Task<HttpResponseMessage>? later = null;
        try
        {
            plan.Release.TrySetResult();
            using var response = await failure;
            Assert.Equal(expected, response.StatusCode);
            Assert.DoesNotContain("Test provider failure", await response.Content.ReadAsStringAsync());
            later = await host.StartBlockedAsync(user.Token, "later");
            Assert.Equal(HttpStatusCode.TooManyRequests, (await host.ChatAsync(user.Token)).StatusCode);
            Assert.Equal(3, host.Provider.Calls);
        }
        finally
        {
            host.Provider.ReleaseAll();
            await other;
            if (later is not null) await later;
        }
    }

    [Fact]
    public async Task Cancellation_holds_permit_until_underlying_provider_cleanup_and_pipeline_terminate()
    {
        using var host = new TestHost();
        using var cancellation = new CancellationTokenSource();
        var user = await host.SeedAsync();
        var plan = host.Provider.Block("cancelled");
        plan.WaitForCancellationCleanup = true;
        var cancelled = host.ChatAsync(user.Token, "cancelled", cancellationToken: cancellation.Token);
        await plan.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var other = await host.StartBlockedAsync(user.Token, "other");
        Task<HttpResponseMessage>? later = null;
        try
        {
            cancellation.Cancel();
            await plan.CancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(plan.Terminated.Task.IsCompleted);
            Assert.Equal(HttpStatusCode.TooManyRequests, (await host.ChatAsync(user.Token)).StatusCode);
            Assert.Equal(2, host.Provider.Calls);
            plan.Cleanup.TrySetResult();
            await plan.PipelineCompleted.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(plan.Terminated.Task.IsCompleted);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await cancelled);
            later = await host.StartBlockedAsync(user.Token, "later");
            Assert.Equal(HttpStatusCode.TooManyRequests, (await host.ChatAsync(user.Token)).StatusCode);
            Assert.Equal(3, host.Provider.Calls);
        }
        finally
        {
            host.Provider.ReleaseAll();
            await other;
            if (later is not null) await later;
        }
    }

    [Fact]
    public async Task Admin_chat_remains_independent_read_only_and_has_no_normal_user_concurrency_cap()
    {
        using var host = new TestHost();
        var user = await host.SeedAsync();
        var admin = await host.SeedAsync(SystemRole.SystemAdmin);
        var tasks = new List<Task<HttpResponseMessage>>();
        try
        {
            tasks.Add(await host.StartBlockedAsync(user.Token, "user first"));
            tasks.Add(await host.StartBlockedAsync(user.Token, "user second"));
            foreach (var name in new[] { "SpendWise system first", "SpendWise system second", "SpendWise system third" })
            {
                var plan = host.Provider.Block(name);
                var response = host.ChatAsync(admin.Token, name, "/api/admin/ai/chat");
                tasks.Add(response);
                await plan.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            }
            Assert.Equal(5, host.Provider.Calls);
            var adminRequests = host.Provider.Requests.Where(request => request.SystemRole == "SystemAdmin").ToList();
            Assert.Equal(3, adminRequests.Count);
            foreach (var request in adminRequests)
            {
                Assert.Empty(request.Tools);
                Assert.Empty(request.Attachments);
                Assert.Contains("read-only", request.SystemInstruction);
                Assert.Contains("admin_ai_read_only",
                    await request.ExecuteToolAsync(new AiToolCall("anything", "{}"), CancellationToken.None));
            }
        }
        finally
        {
            host.Provider.ReleaseAll();
            var responses = await Task.WhenAll(tasks);
            Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        }
    }

    private sealed class TestHost : IDisposable
    {
        private readonly SpendWiseApiFactory baseFactory = new(useIndependentDatabaseConnections: true);
        public WebApplicationFactory<Program> Factory { get; }
        public ControlledProvider Provider { get; } = new();
        public HttpClient Client { get; }

        public TestHost()
        {
            Factory = baseFactory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IAiCompletionProvider>();
                services.AddSingleton<IAiCompletionProvider>(Provider);
                services.AddSingleton<IStartupFilter>(new RequestCompletionFilter(Provider));
            }));
            Client = Factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false
            });
            using var scope = Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            db.Database.EnsureCreated();
            db.FeatureFlags.Add(new FeatureFlag { Name = "AIAssistant", IsEnabled = true, Description = "Test" });
            db.SaveChanges();
        }

        public async Task<Session> SeedAsync(SystemRole role = SystemRole.User)
        {
            using var scope = Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            var unique = Guid.NewGuid().ToString("N");
            var user = new User
            {
                FullName = "AI concurrency test", Email = $"{unique}@test.local",
                NormalizedEmail = $"{unique}@test.local".ToUpperInvariant(),
                SpendId = unique, NormalizedSpendId = unique.ToUpperInvariant(),
                PasswordHash = "test-hash", SecurityPinHash = "test-pin-hash",
                EmailVerifiedAt = DateTime.UtcNow, Status = UserStatus.Active, SystemRole = role
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();
            return await AddSessionAsync(user);
        }

        public async Task<Session> AddSessionAsync(User user)
        {
            using var scope = Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();
            var session = new UserSession { UserId = user.Id, DeviceName = "AI test", LastSeenAt = DateTime.UtcNow };
            db.UserSessions.Add(session);
            db.RefreshTokens.Add(new RefreshToken
            {
                UserId = user.Id, UserSessionId = session.Id,
                TokenHash = tokens.HashRefreshToken(tokens.CreateRefreshToken()),
                ExpiresAt = DateTime.UtcNow.AddDays(30), DeviceName = session.DeviceName
            });
            await db.SaveChangesAsync();
            return new Session(user, session.Id, tokens.CreateAccessToken(user, session.Id).Token);
        }

        public async Task<HttpResponseMessage> ChatAsync(string? token, string message = "Help with SpendWise.",
            string path = "/api/ai/chat", Guid? claimedUser = null, CancellationToken cancellationToken = default)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, path);
            if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Add("X-Test-Completion", message);
            if (claimedUser is not null)
            {
                request.Headers.Add("X-User-Id", claimedUser.ToString());
                request.Headers.Add("UserId", claimedUser.ToString());
                request.Headers.Add("X-Forwarded-For", "192.0.2.250");
            }
            request.Content = JsonContent.Create(new { conversationId = (Guid?)null, message, userId = claimedUser });
            return await Client.SendAsync(request, cancellationToken);
        }

        public async Task<Task<HttpResponseMessage>> StartBlockedAsync(string token, string name)
        {
            var plan = Provider.Block(name);
            var response = ChatAsync(token, name);
            await plan.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            return response;
        }

        public async Task<(int Conversations, int Messages, int Attachments, int Actions)> StateAsync()
        {
            using var scope = Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            return (await db.AiConversations.CountAsync(), await db.AiMessages.CountAsync(),
                await db.AiAttachments.CountAsync(), await db.AiActionProposals.CountAsync());
        }

        public async Task<string> SnapshotAsync()
        {
            using var scope = Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            return System.Text.Json.JsonSerializer.Serialize(new
            {
                Conversations = await db.AiConversations.AsNoTracking().OrderBy(x => x.Id)
                    .Select(x => new { x.Id, x.UserId, x.Title, x.UpdatedAt, x.ContextType }).ToListAsync(),
                Messages = await db.AiMessages.AsNoTracking().OrderBy(x => x.Id)
                    .Select(x => new { x.Id, x.AiConversationId, x.Role, x.Content, x.Provider }).ToListAsync(),
                Attachments = await db.AiAttachments.AsNoTracking().OrderBy(x => x.Id)
                    .Select(x => new { x.Id, x.OwnerUserId, x.AiConversationId, x.AiMessageId }).ToListAsync(),
                Actions = await db.AiActionProposals.AsNoTracking().OrderBy(x => x.Id)
                    .Select(x => new { x.Id, x.OwnerUserId, x.Status, x.ExecutedAt, x.FailureCode }).ToListAsync()
            });
        }

        public void Dispose()
        {
            Provider.ReleaseAll();
            Client.Dispose();
            Factory.Dispose();
            baseFactory.Dispose();
        }
    }

    private sealed record Session(User User, Guid Id, string Token);

    private sealed class ProviderPlan
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource CancellationObserved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Cleanup { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Terminated { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource PipelineCompleted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Exception? Failure { get; set; }
        public bool WaitForCancellationCleanup { get; set; }
    }

    private sealed class ControlledProvider : IAiCompletionProvider
    {
        private readonly ConcurrentDictionary<string, ProviderPlan> plans = new();
        private int calls;
        public int Calls => Volatile.Read(ref calls);
        public ConcurrentQueue<AiProviderRequest> Requests { get; } = new();
        public ProviderPlan Block(string name) => plans.GetOrAdd(name, _ => new ProviderPlan());
        public void ReleaseAll()
        {
            foreach (var plan in plans.Values) { plan.Release.TrySetResult(); plan.Cleanup.TrySetResult(); }
        }
        public void CompletePipeline(string name)
        {
            if (plans.TryGetValue(name, out var plan)) plan.PipelineCompleted.TrySetResult();
        }

        public async Task<AiCompletionResult> CompleteAsync(AiProviderRequest request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref calls);
            Requests.Enqueue(request);
            if (plans.TryGetValue(request.CurrentMessage, out var plan))
            {
                plan.Entered.TrySetResult();
                try
                {
                    try { await plan.Release.Task.WaitAsync(cancellationToken); }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        plan.CancellationObserved.TrySetResult();
                        if (plan.WaitForCancellationCleanup) await plan.Cleanup.Task;
                        throw;
                    }
                    if (plan.Failure is not null) throw plan.Failure;
                }
                finally { plan.Terminated.TrySetResult(); }
            }
            return new AiCompletionResult("SpendWise test answer.", "TestProvider", false);
        }
    }

    // Test-only notification after the real middleware stack unwinds: avoids polling/sleeps
    // or assuming client cancellation means the server/provider has finished.
    private sealed class RequestCompletionFilter(ControlledProvider provider) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, continuation) =>
            {
                try { await continuation(); }
                finally { provider.CompletePipeline(context.Request.Headers["X-Test-Completion"].ToString()); }
            });
            next(app);
        };
    }
}
