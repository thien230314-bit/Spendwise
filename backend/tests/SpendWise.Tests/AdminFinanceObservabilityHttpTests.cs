using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpendWise.Application.Identity;
using SpendWise.Domain.Identity;
using SpendWise.Infrastructure.Persistence;
using Xunit;

namespace SpendWise.Tests;

public sealed class AdminFinanceObservabilityHttpTests
{
    [Theory]
    [InlineData(SystemRole.Admin, HttpStatusCode.OK)]
    [InlineData(SystemRole.User, HttpStatusCode.Forbidden)]
    [InlineData(SystemRole.SystemAdmin, HttpStatusCode.Forbidden)]
    [InlineData(SystemRole.SuperAdmin, HttpStatusCode.Unauthorized)]
    public async Task Finance_overview_and_SePay_health_enforce_current_V1_admin_role(
        SystemRole role, HttpStatusCode expected)
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        Guid sessionId;

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            var key = Guid.NewGuid().ToString("N");
            var user = new User
            {
                FullName = "Synthetic Admin Test",
                Email = key + "@test.local",
                NormalizedEmail = key.ToUpperInvariant() + "@TEST.LOCAL",
                SpendId = key,
                NormalizedSpendId = key.ToUpperInvariant(),
                PasswordHash = "synthetic",
                SystemRole = role
            };
            var session = new UserSession { UserId = user.Id, LastSeenAt = DateTime.UtcNow };
            db.AddRange(user, session);
            await db.SaveChangesAsync();
            sessionId = session.Id;
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                scope.ServiceProvider.GetRequiredService<ITokenService>()
                    .CreateAccessToken(user, session.Id).Token);
        }

        foreach (var url in new[]
        {
            "/api/v1/admin/finance/overview?days=7",
            "/api/v1/admin/finance/sepay/health"
        })
        {
            using var response = await client.GetAsync(url);
            Assert.Equal(expected, response.StatusCode);
            if (expected != HttpStatusCode.OK) continue;
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var body = json.RootElement.GetRawText();
            Assert.DoesNotContain("clientSecret", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("ipnApiKey", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("lifecycleWebhookSecret", body, StringComparison.OrdinalIgnoreCase);
        }

        if (expected != HttpStatusCode.OK) return;
        using (var invalidWindow = await client.GetAsync("/api/v1/admin/finance/overview?days=91"))
            Assert.Equal(HttpStatusCode.BadRequest, invalidWindow.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>()
                .UserSessions.Where(s => s.Id == sessionId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, (DateTime?)DateTime.UtcNow));
        }

        using var revoked = await client.GetAsync("/api/v1/admin/finance/sepay/health");
        Assert.Equal(HttpStatusCode.Unauthorized, revoked.StatusCode);
    }
}
