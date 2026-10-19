using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using SpendWise.Application.Identity;
using SpendWise.Domain.Identity;
using SpendWise.Infrastructure.Persistence;
using Xunit;

namespace SpendWise.Tests;

// Real pipeline, real policies/budgets, isolated SQLite; no live providers or database.
public sealed class RateLimiterHttpTests
{
    [Theory]
    [InlineData("/api/ai/conversations", 15, HttpStatusCode.OK)]
    [InlineData("/api/transactions/income", 30, HttpStatusCode.BadRequest)]
    public async Task Different_authenticated_users_have_independent_budgets_on_same_connection(
        string path, int limit, HttpStatusCode allowed)
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var first = await SeedAsync(factory);
        var second = await SeedAsync(factory);

        for (var i = 0; i < limit; i++)
            Assert.Equal(allowed, await SendAsync(client, path, first.Token));
        Assert.Equal(HttpStatusCode.TooManyRequests, await SendAsync(client, path, first.Token));
        Assert.Equal(allowed, await SendAsync(client, path, second.Token));
    }

    [Theory]
    [InlineData("/api/ai/conversations", 15, HttpStatusCode.OK)]
    [InlineData("/api/transactions/income", 30, HttpStatusCode.BadRequest)]
    public async Task Same_user_sessions_share_budget_and_client_identity_cannot_reset_it(
        string path, int limit, HttpStatusCode allowed)
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var first = await SeedAsync(factory);
        var second = await AddSessionAsync(factory, first.User);

        for (var i = 0; i < limit; i++)
            Assert.Equal(allowed, await SendAsync(client, path, i % 2 == 0 ? first.Token : second.Token));
        Assert.Equal(HttpStatusCode.TooManyRequests, await SendAsync(client, path, second.Token));
        // A read-only route without the financial policy remains usable after exhaustion.
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/transactions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", first.Token);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/ai/conversations", 15, HttpStatusCode.Unauthorized)]
    [InlineData("/api/transactions/income", 30, HttpStatusCode.Unauthorized)]
    [InlineData("/api/auth/login", 10, HttpStatusCode.BadRequest)]
    [InlineData("/api/auth/refresh", 10, HttpStatusCode.BadRequest)]
    [InlineData("/api/auth/email-verification/verify", 10, HttpStatusCode.BadRequest)]
    public async Task Anonymous_requests_keep_existing_fallback_budget_despite_forged_identity(
        string path, int limit, HttpStatusCode allowed)
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        for (var i = 0; i < limit; i++)
            Assert.Equal(allowed, await SendAsync(client, path));
        Assert.Equal(HttpStatusCode.TooManyRequests, await SendAsync(client, path));
    }

    [Theory]
    [InlineData("/api/ai/conversations", 15, HttpStatusCode.OK)]
    [InlineData("/api/transactions/income", 30, HttpStatusCode.BadRequest)]
    public async Task Anonymous_exhaustion_does_not_consume_authenticated_user_budget(
        string path, int limit, HttpStatusCode allowed)
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var user = await SeedAsync(factory);
        for (var i = 0; i < limit; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, await SendAsync(client, path));
        Assert.Equal(HttpStatusCode.TooManyRequests, await SendAsync(client, path));
        Assert.Equal(allowed, await SendAsync(client, path, user.Token));
    }

    [Theory]
    [InlineData("/api/ai/conversations")]
    [InlineData("/api/transactions/income")]
    public async Task Invalid_session_and_SystemAdmin_cannot_access_normal_user_endpoints(string path)
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var revoked = await SeedAsync(factory);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            var session = await db.UserSessions.FindAsync(revoked.SessionId);
            session!.RevokedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Unauthorized, await SendAsync(client, path, revoked.Token));
        var admin = await SeedAsync(factory, SystemRole.SystemAdmin);
        Assert.Equal(HttpStatusCode.Forbidden, await SendAsync(client, path, admin.Token));
        Assert.Equal(HttpStatusCode.Unauthorized, await SendAsync(client, path, "invalid-token"));
    }

    [Fact]
    public async Task SystemAdmin_AI_has_own_identity_budget_and_rejects_normal_users()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var first = await SeedAsync(factory, SystemRole.SystemAdmin);
        var second = await SeedAsync(factory, SystemRole.SystemAdmin);
        var user = await SeedAsync(factory);
        const string path = "/api/admin/ai/conversations";
        for (var i = 0; i < 15; i++)
            Assert.Equal(HttpStatusCode.OK, await SendAsync(client, path, first.Token));
        Assert.Equal(HttpStatusCode.TooManyRequests, await SendAsync(client, path, first.Token));
        Assert.Equal(HttpStatusCode.OK, await SendAsync(client, path, second.Token));
        Assert.Equal(HttpStatusCode.Forbidden, await SendAsync(client, path, user.Token));
        Assert.Equal(HttpStatusCode.OK, await SendAsync(client, "/api/ai/conversations", user.Token));
    }

    [Fact]
    public async Task Login_and_refresh_share_auth_budget_but_OTP_uses_separate_policy()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        for (var i = 0; i < 10; i++)
            Assert.Equal(HttpStatusCode.BadRequest, await SendAsync(client, "/api/auth/login"));
        Assert.Equal(HttpStatusCode.TooManyRequests, await SendAsync(client, "/api/auth/refresh"));
        Assert.Equal(HttpStatusCode.BadRequest, await SendAsync(client, "/api/auth/email-verification/verify"));
    }

    [Theory]
    [InlineData("/api/auth/login", 10, HttpStatusCode.BadRequest)]
    [InlineData("/api/auth/email-verification/verify", 10, HttpStatusCode.BadRequest)]
    [InlineData("/api/transactions/income", 30, HttpStatusCode.Unauthorized)]
    public async Task Anonymous_IP_policies_separate_actual_peers(
        string path, int limit, HttpStatusCode allowed)
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        for (var i = 0; i < limit; i++)
            Assert.Equal(allowed, await SendPeerAsync(factory, path, "192.0.2.1"));
        Assert.Equal(HttpStatusCode.TooManyRequests, await SendPeerAsync(factory, path, "192.0.2.1"));
        Assert.Equal(allowed, await SendPeerAsync(factory, path, "192.0.2.2"));
    }

    [Fact]
    public async Task Anonymous_AI_keeps_shared_anonymous_partition_across_actual_peers()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        for (var i = 0; i < 15; i++)
            Assert.Equal(HttpStatusCode.Unauthorized,
                await SendPeerAsync(factory, "/api/ai/conversations", "192.0.2.1"));
        Assert.Equal(HttpStatusCode.TooManyRequests,
            await SendPeerAsync(factory, "/api/ai/conversations", "192.0.2.2"));
    }

    private static async Task<HttpStatusCode> SendPeerAsync(SpendWiseApiFactory factory, string path, string peer)
    {
        // TestServer sets the transport peer directly; no production header trust is added.
        using var body = new MemoryStream(Encoding.UTF8.GetBytes("{}"));
        var response = await factory.Server.SendAsync(context =>
        {
            context.Connection.RemoteIpAddress = IPAddress.Parse(peer);
            context.Request.Scheme = "https";
            context.Request.Method = path.EndsWith("/conversations", StringComparison.Ordinal)
                ? HttpMethods.Get : HttpMethods.Post;
            context.Request.Path = path;
            context.Request.ContentType = "application/json";
            context.Request.ContentLength = body.Length;
            context.Request.Body = body;
        });
        return (HttpStatusCode)response.Response.StatusCode;
    }

    private static async Task<HttpStatusCode> SendAsync(HttpClient client, string path, string? token = null)
    {
        using var request = new HttpRequestMessage(
            path.EndsWith("/conversations", StringComparison.Ordinal) ? HttpMethod.Get : HttpMethod.Post, path);
        if (token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        // These change on every request, but neither claims nor peer IP may come from them.
        var claimedId = Guid.NewGuid().ToString();
        request.Headers.Add("X-User-Id", claimedId);
        request.Headers.Add("UserId", claimedId);
        request.Headers.Add("X-Forwarded-For", $"192.0.2.{Random.Shared.Next(1, 255)}");
        request.Content = JsonContent.Create(new { userId = claimedId });
        using var response = await client.SendAsync(request);
        return response.StatusCode;
    }

    private static async Task<SeededSession> SeedAsync(
        SpendWiseApiFactory factory, SystemRole role = SystemRole.User)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        var unique = Guid.NewGuid().ToString("N");
        var user = new User
        {
            FullName = "Rate limiter test",
            Email = $"{unique}@test.local", NormalizedEmail = $"{unique}@test.local".ToUpperInvariant(),
            SpendId = unique, NormalizedSpendId = unique.ToUpperInvariant(),
            PasswordHash = "test-hash", SecurityPinHash = "test-pin-hash",
            EmailVerifiedAt = DateTime.UtcNow, Status = UserStatus.Active, SystemRole = role
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return await AddSessionAsync(factory, user);
    }

    private static async Task<SeededSession> AddSessionAsync(SpendWiseApiFactory factory, User user)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();
        var session = new UserSession { UserId = user.Id, DeviceName = "Limiter test", LastSeenAt = DateTime.UtcNow };
        db.UserSessions.Add(session);
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id, UserSessionId = session.Id,
            TokenHash = tokens.HashRefreshToken(tokens.CreateRefreshToken()),
            ExpiresAt = DateTime.UtcNow.AddDays(30), DeviceName = session.DeviceName
        });
        await db.SaveChangesAsync();
        return new SeededSession(user, session.Id, tokens.CreateAccessToken(user, session.Id).Token);
    }

    private sealed record SeededSession(User User, Guid SessionId, string Token);
}
