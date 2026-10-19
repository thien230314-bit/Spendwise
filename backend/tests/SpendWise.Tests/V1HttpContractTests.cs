using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Identity;
using SpendWise.Domain.Admin;
using SpendWise.Domain.Identity;
using SpendWise.Infrastructure.Identity;
using SpendWise.Infrastructure.Persistence;
using Xunit;

namespace SpendWise.Tests;

public sealed class V1HttpContractTests
{
    [Theory]
    [InlineData(SystemRole.User, false)]
    [InlineData(SystemRole.SystemAdmin, false)]
    [InlineData(SystemRole.Admin, true)]
    [InlineData(SystemRole.SuperAdmin, true)]
    public async Task Actual_JWT_pipeline_enforces_v1_policy_case_scope_and_user_boundary(SystemRole role, bool allowed)
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        Guid caseId, targetId, sessionId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            var actor = User(role); var target = User(SystemRole.User);
            var session = new UserSession { UserId = actor.Id, LastSeenAt = DateTime.UtcNow };
            var ticket = new SupportCase { LinkedUserId = target.Id, AssignedAdminId = actor.Id, Subject = "Synthetic case", Description = "Synthetic evidence" };
            db.AddRange(actor, target, session, ticket);
            await db.SaveChangesAsync();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
                scope.ServiceProvider.GetRequiredService<ITokenService>().CreateAccessToken(actor, session.Id).Token);
            caseId = ticket.Id; targetId = target.Id; sessionId = session.Id;
        }
        using var response = await client.PostAsJsonAsync($"/api/v1/admin/support/{caseId}/user-profile", new { UserId = targetId, Reason = "Investigate assigned case" });
        Assert.Equal(allowed ? HttpStatusCode.OK : HttpStatusCode.Forbidden, response.StatusCode);
        if (!allowed) return;
        using var cross = await client.PostAsJsonAsync($"/api/v1/admin/support/{caseId}/user-profile", new { UserId = Guid.NewGuid(), Reason = "Wrong subject denied" });
        Assert.Equal(HttpStatusCode.Forbidden, cross.StatusCode);
        using var wallets = await client.GetAsync("/api/wallets");
        Assert.Equal(HttpStatusCode.Forbidden, wallets.StatusCode);
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>().UserSessions.Where(s => s.Id == sessionId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, DateTime.UtcNow));
        using var revoked = await client.PostAsJsonAsync($"/api/v1/admin/support/{caseId}/user-profile", new { UserId = targetId, Reason = "Revoked session denied" });
        Assert.Equal(HttpStatusCode.Unauthorized, revoked.StatusCode);
    }

    [Fact]
    public async Task V1_fixed_target_rejects_fractional_VND_at_HTTP_boundary()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            var actor = User(SystemRole.User);
            var session = new UserSession { UserId = actor.Id, LastSeenAt = DateTime.UtcNow };
            db.AddRange(actor, session); await db.SaveChangesAsync();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
                scope.ServiceProvider.GetRequiredService<ITokenService>().CreateAccessToken(actor, session.Id).Token);
        }
        using var response = await client.PostAsJsonAsync("/api/v1/funds", new { Name = "Fixed", Kind = "Fixed", TargetVnd = 1.5m, SecurityPin = IdentityTestData.DefaultPin });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var verify = factory.Services.CreateScope();
        Assert.Empty(await verify.ServiceProvider.GetRequiredService<SpendWiseDbContext>().V1Funds.ToListAsync());
    }

    private static User User(SystemRole role)
    {
        var unique = Guid.NewGuid().ToString("N");
        return new User { FullName = "Synthetic", Email = unique + "@test.local", NormalizedEmail = unique.ToUpperInvariant() + "@TEST.LOCAL",
            SpendId = unique, NormalizedSpendId = unique.ToUpperInvariant(), PasswordHash = "synthetic-only",
            SecurityPinHash = new SecurityPinService().Hash(IdentityTestData.DefaultPin), SystemRole = role };
    }

    [Fact]
    public async Task Case_transaction_endpoint_denies_unassigned_operator_with_live_session()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            var actor = User(SystemRole.Admin);
            var session = new UserSession { UserId = actor.Id, LastSeenAt = DateTime.UtcNow };
            db.AddRange(actor, session); await db.SaveChangesAsync();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
                scope.ServiceProvider.GetRequiredService<ITokenService>().CreateAccessToken(actor, session.Id).Token);
        }
        using var response = await client.PostAsJsonAsync($"/api/v1/admin/support/{Guid.NewGuid()}/transaction",
            new { UserId = Guid.NewGuid(), Reason = "Investigate assigned transaction" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Authenticated_User_denied_sensitive_read_is_audited_by_actual_pipeline()
    {
        using var factory = new SpendWiseApiFactory(); using var client = factory.CreateHttpsClient();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            var actor = User(SystemRole.User); var session = new UserSession { UserId = actor.Id, LastSeenAt = DateTime.UtcNow };
            db.AddRange(actor, session); await db.SaveChangesAsync();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
                scope.ServiceProvider.GetRequiredService<ITokenService>().CreateAccessToken(actor, session.Id).Token);
        }
        using var response = await client.PostAsJsonAsync($"/api/v1/admin/support/{Guid.NewGuid()}/transaction",
            new { UserId = Guid.NewGuid(), Reason = "Attempt outside assigned authority" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var verify = factory.Services.CreateScope();
        Assert.True(await verify.ServiceProvider.GetRequiredService<SpendWiseDbContext>().AuditLogs
            .AnyAsync(a => a.Action == "V1_CASE_TRANSACTION_READ_DENIED"));
    }
}
