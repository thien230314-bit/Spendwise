using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpendWise.Application.Features.Authentication;
using SpendWise.Application.Identity;
using SpendWise.Domain.Admin;
using SpendWise.Domain.Identity;
using SpendWise.Infrastructure.Identity;
using SpendWise.Infrastructure.Persistence;
using Xunit;

namespace SpendWise.Tests;

public sealed partial class CoreAcceptanceHttpTests
{
    private const string Pin = IdentityTestData.DefaultPin;

    [Fact]
    public async Task Admin_restrict_lock_unlock_HTTP_enforces_sessions_and_preserves_money()
    {
        using var factory = new SpendWiseApiFactory(new Dictionary<string, string?> { ["RateLimiting:AuthPermitLimit"] = "100" });
        using var userClient = factory.CreateHttpsClient();
        var auth = await IdentityTestData.RegisterAndSetupPinAsync(userClient, "Synthetic restriction subject");
        using var adminClient = factory.CreateHttpsClient();
        Guid userId, caseId;
        string email;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            var subject = await db.Users.SingleAsync(u => u.SystemRole == SystemRole.User);
            userId = subject.Id; email = subject.Email;
            var admin = SyntheticUser(SystemRole.Admin);
            var session = new UserSession { UserId = admin.Id, LastSeenAt = DateTime.UtcNow };
            var ticket = new SupportCase { LinkedUserId = userId, AssignedAdminId = admin.Id, Status = CaseStatus.Investigating };
            db.AddRange(admin, session, ticket); await db.SaveChangesAsync(); caseId = ticket.Id;
            Authorize(adminClient, scope.ServiceProvider.GetRequiredService<ITokenService>().CreateAccessToken(admin, session.Id).Token);
        }
        var path = $"/api/v1/admin/support/{caseId}/users/{userId}/status";
        Authorize(userClient, auth.AccessToken);
        using var denied = await userClient.PutAsJsonAsync(path, new { Status = "Restricted", Reason = "Synthetic verified report", SecurityPin = Pin });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using var restricted = await adminClient.PutAsJsonAsync(path, new { Status = "Restricted", Reason = "Synthetic verified report", SecurityPin = Pin });
        Assert.Equal(HttpStatusCode.NoContent, restricted.StatusCode);
        using var existingRead = await userClient.GetAsync("/api/wallets"); Assert.Equal(HttpStatusCode.OK, existingRead.StatusCode);
        var login = await IdentityTestData.LoginAndVerifyPinAsync(userClient, email);
        using var restrictedRefresh = await userClient.PostAsJsonAsync("/api/auth/refresh", new { RefreshToken = login.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, restrictedRefresh.StatusCode);
        login = (await restrictedRefresh.Content.ReadFromJsonAsync<AuthResponse>())!;
        Authorize(userClient, login.AccessToken);
        using var read = await userClient.GetAsync("/api/wallets"); Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        using var debit = await userClient.PostAsJsonAsync("/api/transfers/internal", new { SourceWalletId = Guid.NewGuid(), DestinationWalletId = Guid.NewGuid(), Amount = 1, Description = "Synthetic debit", IdempotencyKey = "restricted", SecurityPin = Pin });
        Assert.Equal(HttpStatusCode.Forbidden, debit.StatusCode);
        Assert.Contains("account_restricted", await debit.Content.ReadAsStringAsync());
        using var locked = await adminClient.PutAsJsonAsync(path, new { Status = "Locked", Reason = "Synthetic security incident", SecurityPin = Pin });
        Assert.Equal(HttpStatusCode.NoContent, locked.StatusCode);
        using var old = await userClient.GetAsync("/api/wallets"); Assert.Equal(HttpStatusCode.Unauthorized, old.StatusCode);
        using var refresh = await userClient.PostAsJsonAsync("/api/auth/refresh", new { RefreshToken = login.RefreshToken });
        Assert.Equal(HttpStatusCode.Forbidden, refresh.StatusCode);
        using var blockedLogin = await userClient.PostAsJsonAsync("/api/auth/login", new { Identifier = email, Password = "StrongPass1", DeviceName = "Synthetic" });
        Assert.False(blockedLogin.IsSuccessStatusCode);
        using var unlocked = await adminClient.PutAsJsonAsync(path, new { Status = "Active", Reason = "Synthetic incident resolved", SecurityPin = Pin });
        Assert.Equal(HttpStatusCode.NoContent, unlocked.StatusCode);
        await IdentityTestData.LoginAndVerifyPinAsync(userClient, email);
        using var verifyScope = factory.Services.CreateScope();
        var verify = verifyScope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        Assert.Equal(UserStatus.Active, await verify.Users.Where(u => u.Id == userId).Select(u => u.Status).SingleAsync());
        Assert.Empty(await verify.LedgerTransactions.ToListAsync());
        Assert.Equal(3, await verify.AuditLogs.CountAsync(a => a.Action == "A2_ACCOUNT_STATUS_CHANGED"));
        Assert.Equal(3, await verify.Notifications.CountAsync(n => n.UserId == userId && n.Title == "Account status changed"));
    }

    internal static User SyntheticUser(SystemRole role = SystemRole.User)
    {
        var key = Guid.NewGuid().ToString("N");
        return new User { FullName = "Synthetic acceptance", Email = key + "@spendwise.test", NormalizedEmail = key.ToUpperInvariant() + "@SPENDWISE.TEST",
            SpendId = key, NormalizedSpendId = key.ToUpperInvariant(), PasswordHash = "synthetic-only", SystemRole = role, EmailVerifiedAt = DateTime.UtcNow,
            SecurityPinHash = new SecurityPinService().Hash(Pin) };
    }

    internal static void Authorize(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    [Theory]
    [InlineData("wrong-pin")] [InlineData("foreign-case")] [InlineData("closed-case")] [InlineData("invalid-status")] [InlineData("peer")]
    public async Task Account_status_HTTP_requires_pin_case_subject_and_approved_status(string scenario)
    {
        using var factory = new SpendWiseApiFactory(); using var client = factory.CreateHttpsClient();
        var admin = SyntheticUser(SystemRole.Admin); var subject = SyntheticUser(scenario == "peer" ? SystemRole.Admin : SystemRole.User);
        await ActorAsync(factory, client, admin, addUser: true); Guid caseId;
        using (var setupScope = factory.Services.CreateScope())
        {
            var db = setupScope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            var ticket = new SupportCase { LinkedUserId = subject.Id, AssignedAdminId = scenario == "foreign-case" ? null : admin.Id,
                Status = scenario == "closed-case" ? CaseStatus.Closed : CaseStatus.Investigating };
            db.AddRange(subject, ticket); await db.SaveChangesAsync(); caseId = ticket.Id;
        }
        using var response = await client.PutAsJsonAsync($"/api/v1/admin/support/{caseId}/users/{subject.Id}/status",
            new { Status = scenario == "invalid-status" ? "Suspended" : "Locked", Reason = "Synthetic authorization check", SecurityPin = scenario == "wrong-pin" ? "654321" : Pin });
        Assert.Equal(scenario == "invalid-status" ? HttpStatusCode.BadRequest : HttpStatusCode.Forbidden, response.StatusCode);
        using var scope = factory.Services.CreateScope(); var verify = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        Assert.Equal(UserStatus.Active, await verify.Users.Where(u => u.Id == subject.Id).Select(u => u.Status).SingleAsync());
        Assert.Empty(await verify.AuditLogs.Where(a => a.Action == "A2_ACCOUNT_STATUS_CHANGED").ToListAsync());
        Assert.Empty(await verify.LedgerTransactions.ToListAsync());
    }

    [Fact]
    public async Task Legacy_SuperAdmin_HTTP_password_login_is_denied_without_issuing_credentials()
    {
        using var factory = new SpendWiseApiFactory(); using var client = factory.CreateHttpsClient(); var user = SyntheticUser(SystemRole.SuperAdmin);
        using (var scope = factory.Services.CreateScope())
        {
            user.PasswordHash = scope.ServiceProvider.GetRequiredService<IPasswordService>().Hash("StrongPass1");
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>(); db.Users.Add(user); await db.SaveChangesAsync();
        }
        using var response = await client.PostAsJsonAsync("/api/auth/login", new { Identifier = user.Email, Password = "StrongPass1", DeviceName = "Synthetic legacy login" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var verifyScope = factory.Services.CreateScope(); var verify = verifyScope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        Assert.Empty(await verify.UserSessions.ToListAsync()); Assert.Empty(await verify.RefreshTokens.ToListAsync());
    }
}
