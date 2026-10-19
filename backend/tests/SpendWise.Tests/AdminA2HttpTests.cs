using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using SpendWise.Application.Identity;
using SpendWise.Domain.Identity;
using SpendWise.Domain.Admin;
using SpendWise.Infrastructure.Persistence;
using Xunit;

namespace SpendWise.Tests;

public sealed class AdminA2HttpTests
{
    [Theory]
    [InlineData(SystemRole.Admin)] [InlineData(SystemRole.SuperAdmin)] [InlineData(SystemRole.SystemAdmin)] [InlineData(SystemRole.User)]
    public async Task Directory_and_legacy_mutations_cannot_bypass_V1_user_management(SystemRole role)
    {
        var denied = role == SystemRole.SuperAdmin ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden;
        using var factory = new SpendWiseApiFactory(); using var client = factory.CreateHttpsClient();
        var caseId = Guid.NewGuid(); var targetId = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>(); var key = Guid.NewGuid().ToString("N");
            var user = new User { FullName = "Synthetic", Email = key + "@test.local", NormalizedEmail = key + "@TEST.LOCAL", SpendId = key, NormalizedSpendId = key, PasswordHash = "synthetic", SystemRole = role };
            var session = new UserSession { UserId = user.Id, LastSeenAt = DateTime.UtcNow }; db.AddRange(user, session); await db.SaveChangesAsync();
            if (role is SystemRole.Admin or SystemRole.SuperAdmin)
            {
                var subject = new User { Id = targetId, FullName = "Synthetic subject", Email = targetId + "@test.local", NormalizedEmail = targetId + "@TEST.LOCAL", SpendId = targetId.ToString(), NormalizedSpendId = targetId.ToString(), PasswordHash = "synthetic" };
                db.AddRange(subject, new SupportCase { Id = caseId, LinkedUserId = subject.Id, AssignedAdminId = role == SystemRole.Admin ? user.Id : null,
                    EscalatedAt = role == SystemRole.SuperAdmin ? DateTime.UtcNow : null, EscalationReviewerId = role == SystemRole.SuperAdmin ? user.Id : null });
                await db.SaveChangesAsync();
            }
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", scope.ServiceProvider.GetRequiredService<ITokenService>().CreateAccessToken(user, session.Id).Token);
        }
        using var search = await client.PostAsJsonAsync("/api/v1/admin/users/search", new { reason = "Find account" });
        Assert.Equal(role == SystemRole.Admin ? HttpStatusCode.OK : denied, search.StatusCode);
        foreach (var view in new[] { "profile", "sessions", "groups", "friends", "history", "restrictions" })
        {
            using var response = await client.PostAsJsonAsync($"/api/v1/admin/support/{caseId}/users/{targetId}/{view}", new { reason = "Verify scoped API" });
            Assert.Equal(role == SystemRole.Admin ? HttpStatusCode.OK : denied, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync(); Assert.DoesNotContain("passwordHash", body, StringComparison.OrdinalIgnoreCase); Assert.DoesNotContain("tokenHash", body, StringComparison.OrdinalIgnoreCase);
        }
        var id = Guid.NewGuid();
        foreach (var (method, path) in new[] { ("GET", "/api/admin/users"), ("PUT", $"/api/admin/users/{id}/status"), ("PUT", $"/api/admin/wallets/{id}/freeze"),
                     ("POST", $"/api/admin/users/{id}/revoke-sessions"), ("POST", $"/api/admin/users/{id}/reset-password"), ("POST", $"/api/admin/users/{id}/reset-pin"),
                     ("GET", "/api/admin/account-recovery"), ("GET", $"/api/admin/account-recovery/{id}"), ("POST", $"/api/admin/account-recovery/{id}/more-info"),
                     ("POST", $"/api/admin/account-recovery/{id}/approve"), ("POST", $"/api/admin/account-recovery/{id}/reject") })
        {
            using var request = new HttpRequestMessage(new HttpMethod(method), path) { Content = JsonContent.Create(new { reason = "Synthetic scope check", status = "Locked", adminPin = "123456" }) };
            using var response = await client.SendAsync(request); Assert.Equal(denied, response.StatusCode);
        }
    }
}
