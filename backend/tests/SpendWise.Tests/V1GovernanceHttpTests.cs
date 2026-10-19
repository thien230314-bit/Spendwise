using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpendWise.Application.Identity;
using SpendWise.Domain.Identity;
using SpendWise.Infrastructure.Persistence;
using Xunit;

namespace SpendWise.Tests;

public sealed class V1GovernanceHttpTests
{
    [Theory]
    [InlineData(SystemRole.User, HttpStatusCode.NoContent)]
    [InlineData(SystemRole.Admin, HttpStatusCode.Forbidden)]
    [InlineData(SystemRole.SuperAdmin, HttpStatusCode.Unauthorized)]
    public async Task Foreground_activity_HTTP_uses_authenticated_actor_and_rejects_privileged_roles(SystemRole role, HttpStatusCode expected)
    {
        var denied = role == SystemRole.SuperAdmin ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden;
        using var factory = new SpendWiseApiFactory(); using var client = factory.CreateHttpsClient(); Guid userId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>(); var key = Guid.NewGuid().ToString("N");
            var user = new User { FullName = "Synthetic", Email = key + "@test.local", NormalizedEmail = key.ToUpperInvariant() + "@TEST.LOCAL", SpendId = key, NormalizedSpendId = key.ToUpperInvariant(), PasswordHash = "synthetic", SystemRole = role };
            var session = new UserSession { UserId = user.Id, LastSeenAt = DateTime.UtcNow }; db.AddRange(user, session); await db.SaveChangesAsync(); userId = user.Id;
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", scope.ServiceProvider.GetRequiredService<ITokenService>().CreateAccessToken(user, session.Id).Token);
        }
        using var response = await client.PostAsJsonAsync("/api/v1/activity", new { UserId = Guid.NewGuid(), Timestamp = DateTime.UtcNow.AddYears(5) }); Assert.Equal(expected, response.StatusCode);
        using var verify = factory.Services.CreateScope(); var rows = await verify.ServiceProvider.GetRequiredService<SpendWiseDbContext>().V1UserActivities.ToListAsync();
        if (role == SystemRole.User) { var row = Assert.Single(rows); Assert.Equal(userId, row.UserId); Assert.True(row.LastActionAt < DateTime.UtcNow.AddMinutes(1)); }
        else Assert.Empty(rows);
        using var vote = await client.PostAsJsonAsync($"/api/v1/funds/activity-proposals/{Guid.NewGuid()}/vote", new { });
        Assert.Equal(role == SystemRole.User ? HttpStatusCode.BadRequest : denied, vote.StatusCode);
        using var staff = await client.PostAsJsonAsync($"/api/v1/admin/support/{Guid.NewGuid()}/actions", new { Action = "reply", Message = "Synthetic investigation reply", ExpectedStatus = "New" });
        Assert.Equal(role == SystemRole.Admin ? HttpStatusCode.NotFound : denied, staff.StatusCode);
        using var review = await client.PostAsJsonAsync($"/api/v1/funds/requests/{Guid.NewGuid()}/review", new { Reason = "Synthetic review" });
        Assert.Equal(role == SystemRole.User ? HttpStatusCode.BadRequest : denied, review.StatusCode);
    }
}
