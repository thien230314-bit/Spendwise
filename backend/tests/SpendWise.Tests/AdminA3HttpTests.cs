using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using SpendWise.Application.Identity;
using SpendWise.Domain.Admin;
using SpendWise.Domain.Identity;
using SpendWise.Domain.V1Groups;
using SpendWise.Infrastructure.Persistence;
using Xunit;

namespace SpendWise.Tests;

public sealed class AdminA3HttpTests
{
    [Theory]
    [InlineData(SystemRole.Admin)] [InlineData(SystemRole.SuperAdmin)] [InlineData(SystemRole.User)] [InlineData(SystemRole.SystemAdmin)]
    public async Task Real_HTTP_routes_enforce_canonical_roles_and_scope(SystemRole role)
    {
        using var factory = new SpendWiseApiFactory(); using var client = factory.CreateHttpsClient();
        var caseId = Guid.NewGuid(); var fundId = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            User Synthetic(SystemRole r) { var key = Guid.NewGuid().ToString("N"); return new() { FullName = "Synthetic", Email = key + "@test.local", NormalizedEmail = key + "@TEST.LOCAL", SpendId = key, NormalizedSpendId = key, PasswordHash = "synthetic", SystemRole = r }; }
            var actor = Synthetic(role); var subject = Synthetic(SystemRole.User); var session = new UserSession { UserId = actor.Id, LastSeenAt = DateTime.UtcNow };
            var fund = new V1Fund("Synthetic", "SPW000001", V1FundKind.Free, subject.Id, null); fundId = fund.Id;
            db.AddRange(actor, subject, session, fund, new V1FundMembership { FundId = fund.Id, UserId = subject.Id, Seat = 1 },
                new SupportCase { Id = caseId, LinkedUserId = subject.Id, AssignedAdminId = role == SystemRole.Admin ? actor.Id : null,
                    EscalatedAt = role == SystemRole.SuperAdmin ? DateTime.UtcNow : null, EscalationReviewerId = role == SystemRole.SuperAdmin ? actor.Id : null });
            await db.SaveChangesAsync();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", scope.ServiceProvider.GetRequiredService<ITokenService>().CreateAccessToken(actor, session.Id).Token);
        }
        var root = $"/api/v1/admin/support/{caseId}/funds";
        foreach (var path in new[] { "search", $"{fundId}/detail", $"{fundId}/members", $"{fundId}/activity", $"{fundId}/finance/Requests", $"{fundId}/treasury", $"{fundId}/governance/Elections", $"{fundId}/cases", $"{fundId}/invitations", $"{fundId}/dissolution" })
        {
            using var response = await client.PostAsJsonAsync(root + "/" + path, new { reason = "Verify scoped group API" });
            Assert.Equal(role is SystemRole.Admin or SystemRole.SuperAdmin ? HttpStatusCode.OK : HttpStatusCode.Forbidden, response.StatusCode);
            var text = await response.Content.ReadAsStringAsync(); Assert.DoesNotContain("passwordHash", text, StringComparison.OrdinalIgnoreCase); Assert.DoesNotContain("policyJson", text, StringComparison.OrdinalIgnoreCase);
        }
        using var foreign = await client.PostAsJsonAsync(root + $"/{Guid.NewGuid()}/detail", new { reason = "Verify foreign group denial" }); Assert.Equal(HttpStatusCode.Forbidden, foreign.StatusCode);
        using var posting = await client.PostAsJsonAsync(root + $"/{fundId}/postings/{Guid.NewGuid()}", new { reason = "Verify foreign posting denial" }); Assert.Equal(HttpStatusCode.Forbidden, posting.StatusCode);
        using var report = await client.PostAsJsonAsync(root + $"/{fundId}/reports/Support/{Guid.NewGuid()}/history", new { reason = "Verify foreign report denial" }); Assert.Equal(HttpStatusCode.Forbidden, report.StatusCode);
        using var expense = await client.PostAsJsonAsync(root + $"/{fundId}/expenses/{Guid.NewGuid()}/postings", new { reason = "Verify foreign expense denial" }); Assert.Equal(HttpStatusCode.Forbidden, expense.StatusCode);
        using var legacy = await client.GetAsync("/api/groups"); Assert.Equal(role == SystemRole.User ? HttpStatusCode.OK : HttpStatusCode.Forbidden, legacy.StatusCode);
        using var userV1 = await client.GetAsync("/api/v1/funds"); Assert.Equal(role == SystemRole.User ? HttpStatusCode.OK : HttpStatusCode.Forbidden, userV1.StatusCode);
    }
}
