using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Identity;
using SpendWise.Domain.Identity;
using SpendWise.Infrastructure.Persistence;
using Xunit;

namespace SpendWise.Tests;

public sealed class AdminA1HttpTests
{
    [Theory]
    [InlineData(SystemRole.Admin, HttpStatusCode.OK)]
    [InlineData(SystemRole.SuperAdmin, HttpStatusCode.OK)]
    [InlineData(SystemRole.User, HttpStatusCode.Forbidden)]
    [InlineData(SystemRole.SystemAdmin, HttpStatusCode.Forbidden)]
    public async Task All_A1_and_legacy_aggregate_routes_enforce_live_V1_roles(SystemRole role, HttpStatusCode expected)
    {
        using var factory = new SpendWiseApiFactory(); using var client = factory.CreateHttpsClient();
        Guid sessionId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>(); var key = Guid.NewGuid().ToString("N");
            var user = new User { FullName = "Synthetic", Email = key + "@test.local", NormalizedEmail = key.ToUpperInvariant() + "@TEST.LOCAL", SpendId = key, NormalizedSpendId = key.ToUpperInvariant(), PasswordHash = "synthetic", SystemRole = role };
            var session = new UserSession { UserId = user.Id, LastSeenAt = DateTime.UtcNow }; db.AddRange(user, session); await db.SaveChangesAsync();
            sessionId = session.Id;
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", scope.ServiceProvider.GetRequiredService<ITokenService>().CreateAccessToken(user, session.Id).Token);
        }
        foreach (var path in new[] { "/api/v1/admin/overview", "/api/v1/admin/overview/tasks", "/api/v1/admin/overview/alerts", "/api/v1/admin/overview/activity", "/api/v1/admin/overview/sources/usersCreated", "/api/admin/dashboard", "/api/admin/reports" })
        {
            using var response = await client.GetAsync(path); Assert.Equal(expected, response.StatusCode);
            if (expected == HttpStatusCode.OK)
            {
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                Assert.DoesNotContain("passwordHash", json.RootElement.GetRawText(), StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("token", json.RootElement.GetRawText(), StringComparison.OrdinalIgnoreCase);
                if (json.RootElement.TryGetProperty("items", out var items)) Assert.Equal(0, items.GetArrayLength());
            }
        }
        if (expected == HttpStatusCode.OK)
        {
            using var invalid = await client.GetAsync("/api/v1/admin/overview/tasks?page=2147483647&pageSize=100"); Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
            using var badFilter = await client.GetAsync("/api/v1/admin/overview/alerts?source=RawSecrets"); Assert.Equal(HttpStatusCode.BadRequest, badFilter.StatusCode);
            using var invalidLegacy = await client.GetAsync("/api/admin/dashboard?from=2000-01-01T00:00:00Z"); Assert.Equal(HttpStatusCode.BadRequest, invalidLegacy.StatusCode);
            using (var scope = factory.Services.CreateScope()) await scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>().UserSessions.Where(s => s.Id == sessionId).ExecuteUpdateAsync(s => s.SetProperty(s => s.RevokedAt, (DateTime?)DateTime.UtcNow));
            using var revoked = await client.GetAsync("/api/admin/dashboard"); Assert.Equal(HttpStatusCode.Unauthorized, revoked.StatusCode);
        }
    }
}
