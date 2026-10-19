using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpendWise.Application.Identity;
using SpendWise.Domain.Identity;
using SpendWise.Infrastructure.Persistence;
using Xunit;

namespace SpendWise.Tests;

public sealed class SessionManagementHttpTests
{
    [Fact]
    public async Task User_lists_only_own_active_sessions_with_safe_metadata_and_exact_current_session()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var ownerFirst = await SeedUserAsync(factory, "Session owner");
        var ownerSecond = await AddSessionAsync(factory, ownerFirst.User, "Owner phone");
        _ = await SeedUserAsync(factory, "Other session owner");
        UseToken(client, ownerSecond.AccessToken);

        var response = await client.GetAsync("/api/auth/sessions");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        var sessions = json.RootElement.EnumerateArray().ToArray();
        Assert.Equal(2, sessions.Length);
        Assert.Single(sessions, item => item.GetProperty("isCurrent").GetBoolean());
        foreach (var session in sessions)
        {
            Assert.Equal(
                ["createdAt", "deviceName", "expiresAt", "isCurrent", "lastSeenAt", "sessionId"],
                session.EnumerateObject().Select(property => property.Name).Order().ToArray());
        }
        Assert.DoesNotContain("refreshToken", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("accessToken", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("tokenHash", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("deviceIdentifier", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("deviceTrust", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ipAddress", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("userAgent", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SystemAdmin_cannot_list_or_revoke_user_sessions()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var admin = await SeedUserAsync(factory, "Session admin", SystemRole.SystemAdmin);
        UseToken(client, admin.AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/auth/sessions")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await client.DeleteAsync($"/api/auth/sessions/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task User_revokes_one_owned_session_without_mutating_other_sessions_push_or_finance()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var ownerFirst = await SeedUserAsync(factory, "Revoke owner");
        var ownerSecond = await AddSessionAsync(factory, ownerFirst.User, "Owner tablet");
        UseToken(client, ownerFirst.AccessToken);
        var installationId = Guid.NewGuid();
        (await client.PutAsJsonAsync(
            $"/api/mobile/push/installations/{installationId}",
            new { platform = "Android", pushToken = "ExpoPushToken[sessionmanagementtest]", deviceName = "Phone" }))
            .EnsureSuccessStatusCode();

        Guid secondSessionId;
        int financialCount;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            secondSessionId = await SessionIdForRefreshTokenAsync(
                db,
                scope.ServiceProvider.GetRequiredService<ITokenService>(),
                ownerSecond.RefreshToken);
            financialCount = await db.FinancialTransactions.CountAsync();
        }

        var response = await client.DeleteAsync($"/api/auth/sessions/{secondSessionId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            Assert.NotNull((await db.UserSessions.AsNoTracking().SingleAsync(x => x.Id == secondSessionId)).RevokedAt);
            Assert.All(
                await db.RefreshTokens.AsNoTracking().Where(x => x.UserSessionId == secondSessionId).ToListAsync(),
                token => Assert.NotNull(token.RevokedAt));
            Assert.Equal(1, await db.UserSessions.CountAsync(x => x.UserId == ownerFirst.User.Id && x.RevokedAt == null));
            var installation = await db.MobilePushInstallations.AsNoTracking().SingleAsync(x => x.Id == installationId);
            Assert.True(installation.IsActive);
            Assert.Equal("ExpoPushToken[sessionmanagementtest]", installation.PushToken);
            Assert.Equal(financialCount, await db.FinancialTransactions.CountAsync());
        }

        var rejectedRefresh = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = ownerSecond.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, rejectedRefresh.StatusCode);
        UseToken(client, ownerFirst.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/sessions")).StatusCode);
    }

    [Fact]
    public async Task Other_user_and_missing_session_revoke_are_indistinguishable_and_do_not_mutate_target()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var owner = await SeedUserAsync(factory, "Protected session owner");
        var attacker = await SeedUserAsync(factory, "Session attacker");
        Guid ownerSessionId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            ownerSessionId = await SessionIdForRefreshTokenAsync(
                db,
                scope.ServiceProvider.GetRequiredService<ITokenService>(),
                owner.RefreshToken);
        }
        UseToken(client, attacker.AccessToken);

        var otherUser = await client.DeleteAsync($"/api/auth/sessions/{ownerSessionId}");
        var missing = await client.DeleteAsync($"/api/auth/sessions/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, otherUser.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(
            await ErrorCodeAsync(otherUser),
            await ErrorCodeAsync(missing));
        using var verificationScope = factory.Services.CreateScope();
        Assert.Null((await verificationScope.ServiceProvider.GetRequiredService<SpendWiseDbContext>()
            .UserSessions.AsNoTracking().SingleAsync(x => x.Id == ownerSessionId)).RevokedAt);
    }

    [Fact]
    public async Task Revoking_current_session_invalidates_its_access_and_refresh_after_successful_delete()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var current = await SeedUserAsync(factory, "Current session owner");
        UseToken(client, current.AccessToken);
        var list = await client.GetFromJsonAsync<JsonElement>("/api/auth/sessions");
        var currentSessionId = list.EnumerateArray().Single(item => item.GetProperty("isCurrent").GetBoolean())
            .GetProperty("sessionId").GetGuid();

        Assert.Equal(
            HttpStatusCode.NoContent,
            (await client.DeleteAsync($"/api/auth/sessions/{currentSessionId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/sessions")).StatusCode);
        var rejectedRefresh = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = current.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, rejectedRefresh.StatusCode);
    }

    private static Task<Guid> SessionIdForRefreshTokenAsync(
        SpendWiseDbContext db,
        ITokenService tokens,
        string rawToken)
    {
        var tokenHash = tokens.HashRefreshToken(rawToken);
        return db.RefreshTokens.AsNoTracking()
            .Where(token => token.TokenHash == tokenHash)
            .Select(token => token.UserSessionId)
            .SingleAsync();
    }

    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("code").GetString();
    }

    private static void UseToken(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private static async Task<SeededAuth> SeedUserAsync(
        SpendWiseApiFactory factory,
        string name,
        SystemRole role = SystemRole.User)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        var unique = Guid.NewGuid().ToString("N");
        var user = new User
        {
            FullName = name,
            Email = $"{unique}@test.local",
            NormalizedEmail = $"{unique}@test.local".ToUpperInvariant(),
            SpendId = unique,
            NormalizedSpendId = unique.ToUpperInvariant(),
            PasswordHash = "test-hash",
            SecurityPinHash = "test-pin-hash",
            EmailVerifiedAt = DateTime.UtcNow,
            Status = UserStatus.Active,
            SystemRole = role
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return await AddSessionAsync(factory, user, "Test browser");
    }

    private static async Task<SeededAuth> AddSessionAsync(
        SpendWiseApiFactory factory,
        User user,
        string deviceName)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();
        var session = new UserSession
        {
            UserId = user.Id,
            DeviceName = deviceName,
            LastSeenAt = DateTime.UtcNow
        };
        var rawRefreshToken = tokens.CreateRefreshToken();
        db.UserSessions.Add(session);
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            UserSessionId = session.Id,
            TokenHash = tokens.HashRefreshToken(rawRefreshToken),
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            DeviceName = deviceName
        });
        await db.SaveChangesAsync();
        var accessToken = tokens.CreateAccessToken(user, session.Id).Token;
        return new SeededAuth(user, accessToken, rawRefreshToken);
    }

    private sealed record SeededAuth(User User, string AccessToken, string RefreshToken);
}
