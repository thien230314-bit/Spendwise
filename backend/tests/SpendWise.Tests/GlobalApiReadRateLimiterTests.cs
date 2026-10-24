using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using SpendWise.Domain.Identity;
using SpendWise.Infrastructure.Persistence;
using SpendWise.Application.Identity;
using Xunit;

namespace SpendWise.Tests;

public sealed class GlobalApiReadRateLimiterTests
{
    [Fact]
    public async Task GetRequest_UnderLimit_Succeeds()
    {
        // Arrange
        using var factory = new SpendWiseApiFactory(new Dictionary<string, string?>
        {
            ["RateLimiting:ApiReadPermitLimit"] = "10"
        });
        using var client = factory.CreateHttpsClient();
        var session = await SeedAsync(factory);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.Token);

        // Act & Assert
        for (var i = 0; i < 5; i++)
        {
            using var response = await client.GetAsync("/api/users/me");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task GetRequest_ExceedingLimit_Returns429_WithRetryAfter()
    {
        // Arrange
        using var factory = new SpendWiseApiFactory(new Dictionary<string, string?>
        {
            ["RateLimiting:ApiReadPermitLimit"] = "3"
        });
        using var client = factory.CreateHttpsClient();
        var session = await SeedAsync(factory);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.Token);

        // Act & Assert: 3 requests pass, 4th gets 429
        for (var i = 0; i < 3; i++)
        {
            using var okResp = await client.GetAsync("/api/users/me");
            Assert.Equal(HttpStatusCode.OK, okResp.StatusCode);
        }

        using var limitedResp = await client.GetAsync("/api/users/me");
        Assert.Equal(HttpStatusCode.TooManyRequests, limitedResp.StatusCode);
        Assert.True(limitedResp.Headers.Contains("Retry-After"), "Response must contain Retry-After header.");
        var retryAfter = limitedResp.Headers.GetValues("Retry-After").FirstOrDefault();
        Assert.NotNull(retryAfter);
        Assert.True(int.TryParse(retryAfter, out var seconds) && seconds > 0, "Retry-After must be a positive integer.");
    }

    [Fact]
    public async Task SpammingUser_DoesNotBlock_DifferentUser()
    {
        // Arrange
        using var factory = new SpendWiseApiFactory(new Dictionary<string, string?>
        {
            ["RateLimiting:ApiReadPermitLimit"] = "3"
        });
        using var client = factory.CreateHttpsClient();
        var spammer = await SeedAsync(factory);
        var victim = await SeedAsync(factory);

        // Spammer exhausts quota
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", spammer.Token);
        for (var i = 0; i < 3; i++)
        {
            using var r = await client.GetAsync("/api/users/me");
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        }
        using var spammerBlocked = await client.GetAsync("/api/users/me");
        Assert.Equal(HttpStatusCode.TooManyRequests, spammerBlocked.StatusCode);

        // Victim is not affected
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", victim.Token);
        using var victimAllowed = await client.GetAsync("/api/users/me");
        Assert.Equal(HttpStatusCode.OK, victimAllowed.StatusCode);
    }

    [Fact]
    public async Task ExcludedRoutes_AreNotThrottled_ByReadLimiter()
    {
        // Arrange
        using var factory = new SpendWiseApiFactory(new Dictionary<string, string?>
        {
            ["RateLimiting:ApiReadPermitLimit"] = "2"
        });
        using var client = factory.CreateHttpsClient();

        // /api/ping
        for (var i = 0; i < 5; i++)
        {
            using var pingResp = await client.GetAsync("/api/ping");
            Assert.Equal(HttpStatusCode.OK, pingResp.StatusCode);
        }

        // /health
        for (var i = 0; i < 5; i++)
        {
            using var healthResp = await client.GetAsync("/health");
            Assert.Equal(HttpStatusCode.OK, healthResp.StatusCode);
        }
    }

    [Fact]
    public async Task AnonymousIp_IsThrottled_Independently()
    {
        // Arrange
        using var factory = new SpendWiseApiFactory(new Dictionary<string, string?>
        {
            ["RateLimiting:ApiReadPermitLimit"] = "2"
        });
        using var client = factory.CreateHttpsClient();

        // Anonymous request to protected read endpoint gets 401 Unauthorized twice, then 429 Too Many Requests
        using var r1 = await client.GetAsync("/api/users/me");
        Assert.Equal(HttpStatusCode.Unauthorized, r1.StatusCode);

        using var r2 = await client.GetAsync("/api/users/me");
        Assert.Equal(HttpStatusCode.Unauthorized, r2.StatusCode);

        using var r3 = await client.GetAsync("/api/users/me");
        Assert.Equal(HttpStatusCode.TooManyRequests, r3.StatusCode);
    }

    [Fact]
    public async Task PostRequests_DoNotConsume_ReadLimit()
    {
        // Arrange
        using var factory = new SpendWiseApiFactory(new Dictionary<string, string?>
        {
            ["RateLimiting:ApiReadPermitLimit"] = "2",
            ["RateLimiting:FinancialPermitLimit"] = "100"
        });
        using var client = factory.CreateHttpsClient();
        var session = await SeedAsync(factory);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.Token);

        // Exceed read limit on GET /api/wallets
        for (var i = 0; i < 2; i++)
        {
            using var r = await client.GetAsync("/api/wallets");
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        }
        using var rBlocked = await client.GetAsync("/api/wallets");
        Assert.Equal(HttpStatusCode.TooManyRequests, rBlocked.StatusCode);

        // POST /api/wallets still evaluated under financial policy, not blocked by read limit
        using var postResp = await client.PostAsJsonAsync("/api/wallets", new
        {
            name = "Test Wallet Post",
            currency = "VND",
            initialBalance = 0
        });
        // Returns 201 Created or 409 if limit reached, but definitely NOT 429
        Assert.NotEqual(HttpStatusCode.TooManyRequests, postResp.StatusCode);
    }

    private static async Task<SeededSession> SeedAsync(SpendWiseApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();
        var unique = Guid.NewGuid().ToString("N");
        var user = new User
        {
            FullName = "Rate limit user",
            Email = $"{unique}@test.local",
            NormalizedEmail = $"{unique}@test.local".ToUpperInvariant(),
            SpendId = unique,
            NormalizedSpendId = unique.ToUpperInvariant(),
            PasswordHash = "test-hash",
            SecurityPinHash = "test-pin-hash",
            EmailVerifiedAt = DateTime.UtcNow,
            Status = UserStatus.Active,
            SystemRole = SystemRole.User
        };
        db.Users.Add(user);
        var session = new UserSession
        {
            UserId = user.Id,
            DeviceName = "TestDevice",
            LastSeenAt = DateTime.UtcNow
        };
        db.UserSessions.Add(session);
        await db.SaveChangesAsync();
        return new SeededSession(user, session.Id, tokens.CreateAccessToken(user, session.Id).Token);
    }

    private sealed record SeededSession(User User, Guid SessionId, string Token);
}
