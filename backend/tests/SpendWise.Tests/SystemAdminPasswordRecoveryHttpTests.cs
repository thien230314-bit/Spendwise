using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpendWise.Application.Features.Authentication;
using SpendWise.Application.Identity;
using SpendWise.Domain.Identity;
using SpendWise.Infrastructure.Identity;
using SpendWise.Infrastructure.Persistence;
using Xunit;

namespace SpendWise.Tests;

// Real HTTP pipeline and password/PIN services, synthetic SQLite accounts only.
public sealed class SystemAdminPasswordRecoveryHttpTests
{
    [Fact]
    public async Task Admin_reset_returns_204_preserves_PIN_revokes_old_credentials_and_requires_PIN_for_new_login()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var admin = await SeedAsync(factory);
        AuthResponse oldAuth;
        string pinHash;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            var user = await db.Users.SingleAsync(x => x.Id == admin.Id);
            pinHash = user.SecurityPinHash!;
            oldAuth = await scope.ServiceProvider.GetRequiredService<AuthenticationSupport>()
                .IssueAsync(user, "synthetic-old-session", null, null, default);
        }
        Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsJsonAsync("/api/auth/forgot-password",
            new ForgotPasswordRequest(admin.Email))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest(admin.Email, IdentityTestData.DefaultEmailOtp, NewPassword))).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            var user = await db.Users.SingleAsync(x => x.Id == admin.Id);
            Assert.Equal(SystemRole.SystemAdmin, user.SystemRole);
            Assert.Equal(pinHash, user.SecurityPinHash);
            Assert.True(new SecurityPinService().Verify(IdentityTestData.DefaultPin, user.SecurityPinHash!));
            Assert.True(new PasswordService().Verify(NewPassword, user.PasswordHash));
            Assert.False(new PasswordService().Verify(OldPassword, user.PasswordHash));
            Assert.All(await db.UserSessions.Where(x => x.UserId == admin.Id).ToListAsync(), x => Assert.NotNull(x.RevokedAt));
            Assert.All(await db.RefreshTokens.Where(x => x.UserId == admin.Id).ToListAsync(), x => Assert.NotNull(x.RevokedAt));
            Assert.NotNull((await db.EmailVerificationTokens.SingleAsync()).UsedAt);
        }
        using var oldRequest = new HttpRequestMessage(HttpMethod.Get, "/api/admin/dashboard");
        oldRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", oldAuth.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(oldRequest)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshRequest(oldAuth.RefreshToken))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(admin.Email, OldPassword))).StatusCode);
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(admin.Email, NewPassword));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var challenge = (await login.Content.ReadFromJsonAsync<LoginChallengeResponse>())!;
        Assert.False(string.IsNullOrWhiteSpace(challenge.ChallengeToken));
        var completed = await client.PostAsJsonAsync("/api/auth/pin/verify",
            new CompleteSecurityPinLoginRequest(challenge.ChallengeToken, IdentityTestData.DefaultPin));
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        Assert.Equal("SystemAdmin", (await completed.Content.ReadFromJsonAsync<AuthResponse>())!.User.SystemRole);
        var replay = await client.PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest(admin.Email, IdentityTestData.DefaultEmailOtp, NewPassword));
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
        Assert.Equal("invalid_password_reset_token", await ErrorCode(replay));
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("inactive")]
    [InlineData("unverified")]
    public async Task Ineligible_Admin_matches_neutral_unknown_response_without_OTP_or_email(string condition)
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var admin = await SeedAsync(factory);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            var user = await db.Users.SingleAsync();
            if (condition == "inactive") user.Status = UserStatus.Locked;
            if (condition == "unverified") user.EmailVerifiedAt = null;
            await db.SaveChangesAsync();
        }
        var response = await client.PostAsJsonAsync("/api/auth/forgot-password",
            new ForgotPasswordRequest(condition == "unknown" ? "absent@fixture.invalid" : admin.Email));
        var unknown = await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest("other-absent@fixture.invalid"));
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(await unknown.Content.ReadAsStringAsync(), await response.Content.ReadAsStringAsync());
        using var check = factory.Services.CreateScope();
        var verify = check.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        Assert.Empty(await verify.EmailVerificationTokens.ToListAsync());
        Assert.Empty(await verify.EmailOutboxMessages.ToListAsync());
    }

    [Theory]
    [InlineData("short")]
    [InlineData("alllowercase123")]
    [InlineData("ALLUPPERCASE123")]
    [InlineData("NoDigitsInPassword")]
    public async Task Admin_weak_password_returns_safe_code_without_consuming_OTP(string password)
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var admin = await SeedAsync(factory);
        await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest(admin.Email));
        var reset = await client.PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest(admin.Email, IdentityTestData.DefaultEmailOtp, password));
        Assert.Equal(HttpStatusCode.BadRequest, reset.StatusCode);
        Assert.Equal("weak_password", await ErrorCode(reset));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        Assert.Null((await db.EmailVerificationTokens.SingleAsync()).UsedAt);
        Assert.True(new PasswordService().Verify(OldPassword, (await db.Users.SingleAsync()).PasswordHash));
    }

    [Fact]
    public async Task Recovery_CORS_accepts_explicit_Admin_origin_and_denies_unknown_origin()
    {
        using var factory = new SpendWiseApiFactory();
        using var corsFactory = factory.WithWebHostBuilder(builder => builder.UseSetting("AllowedOrigins:0", "http://localhost:3000"));
        using var client = corsFactory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        foreach (var origin in new[] { "http://localhost:3000", "https://untrusted.invalid" })
        {
            using var request = new HttpRequestMessage(HttpMethod.Options, "/api/auth/reset-password");
            request.Headers.Add("Origin", origin);
            request.Headers.Add("Access-Control-Request-Method", "POST");
            request.Headers.Add("Access-Control-Request-Headers", "content-type");
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.Equal(origin == "http://localhost:3000", response.Headers.Contains("Access-Control-Allow-Origin"));
        }
    }

    [Theory]
    [InlineData(SystemRole.SystemAdmin, 7, false)]
    [InlineData(SystemRole.SystemAdmin, 8, true)]
    [InlineData(SystemRole.SystemAdmin, 9, true)]
    [InlineData(SystemRole.SystemAdmin, 10, true)]
    [InlineData(SystemRole.SystemAdmin, 128, true)]
    [InlineData(SystemRole.SystemAdmin, 129, false)]
    [InlineData(SystemRole.User, 7, false)]
    [InlineData(SystemRole.User, 8, true)]
    [InlineData(SystemRole.User, 9, true)]
    [InlineData(SystemRole.User, 10, true)]
    [InlineData(SystemRole.User, 128, true)]
    [InlineData(SystemRole.User, 129, false)]
    public async Task Reset_password_boundaries_are_unified_for_all_roles(SystemRole role, int length, bool accepted)
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var account = await SeedAsync(factory, role);
        var password = "Aa1" + new string('x', length - 3); // No special character required.
        Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsJsonAsync("/api/auth/forgot-password",
            new ForgotPasswordRequest(account.Email))).StatusCode);
        var response = await client.PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest(account.Email, IdentityTestData.DefaultEmailOtp, password));
        Assert.Equal(accepted ? HttpStatusCode.NoContent : HttpStatusCode.BadRequest, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        var saved = await db.Users.SingleAsync();
        Assert.Equal(accepted, (await db.EmailVerificationTokens.SingleAsync()).UsedAt != null);
        Assert.True(new PasswordService().Verify(accepted ? password : OldPassword, saved.PasswordHash));
        Assert.True(new SecurityPinService().Verify(IdentityTestData.DefaultPin, saved.SecurityPinHash!));
        Assert.Equal(role, saved.SystemRole);
        if (accepted)
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/login",
                new LoginRequest(account.Email, password))).StatusCode);
        else Assert.Equal("weak_password", await ErrorCode(response));
    }

    [Theory]
    [InlineData("abcdefg1")]
    [InlineData("ABCDEFG1")]
    [InlineData("Abcdefgh")]
    public async Task Admin_eight_character_policy_still_requires_all_components(string password)
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var account = await SeedAsync(factory);
        await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest(account.Email));
        var response = await client.PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest(account.Email, IdentityTestData.DefaultEmailOtp, password));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("weak_password", await ErrorCode(response));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        Assert.Null((await db.EmailVerificationTokens.SingleAsync()).UsedAt);
        Assert.True(new PasswordService().Verify(OldPassword, (await db.Users.SingleAsync()).PasswordHash));
    }

    [Theory]
    [InlineData(SystemRole.SystemAdmin, 7, false)]
    [InlineData(SystemRole.SystemAdmin, 8, true)]
    [InlineData(SystemRole.SystemAdmin, 9, true)]
    [InlineData(SystemRole.SystemAdmin, 10, true)]
    [InlineData(SystemRole.SystemAdmin, 128, true)]
    [InlineData(SystemRole.SystemAdmin, 129, false)]
    [InlineData(SystemRole.User, 7, false)]
    [InlineData(SystemRole.User, 8, true)]
    [InlineData(SystemRole.User, 9, true)]
    [InlineData(SystemRole.User, 10, true)]
    [InlineData(SystemRole.User, 128, true)]
    [InlineData(SystemRole.User, 129, false)]
    public async Task Authenticated_change_password_uses_unified_policy(SystemRole role, int length, bool accepted)
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var account = await SeedAsync(factory, role);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            var auth = await scope.ServiceProvider.GetRequiredService<AuthenticationSupport>()
                .IssueAsync(await db.Users.SingleAsync(), "synthetic-policy-session", null, null, default);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        }
        var password = "Aa1" + new string('x', length - 3);
        var response = await client.PostAsJsonAsync("/api/auth/change-password", new ChangePasswordRequest(OldPassword, password));
        Assert.Equal(accepted ? HttpStatusCode.NoContent : HttpStatusCode.BadRequest, response.StatusCode);
        using var verify = factory.Services.CreateScope();
        var saved = await verify.ServiceProvider.GetRequiredService<SpendWiseDbContext>().Users.SingleAsync();
        Assert.True(new PasswordService().Verify(accepted ? password : OldPassword, saved.PasswordHash));
        Assert.True(new SecurityPinService().Verify(IdentityTestData.DefaultPin, saved.SecurityPinHash!));
        if (!accepted) Assert.Equal("weak_password", await ErrorCode(response));
    }

    [Theory]
    [InlineData(SystemRole.SystemAdmin)]
    [InlineData(SystemRole.User)]
    public async Task Eight_character_password_does_not_reveal_role_before_valid_OTP(SystemRole role)
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var account = await SeedAsync(factory, role);
        await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest(account.Email));
        var known = await client.PostAsJsonAsync("/api/auth/reset-password", new ResetPasswordRequest(account.Email, "00000000", "Abcdefg1"));
        var unknown = await client.PostAsJsonAsync("/api/auth/reset-password", new ResetPasswordRequest("absent@fixture.invalid", "00000000", "Abcdefg1"));
        Assert.Equal(HttpStatusCode.BadRequest, known.StatusCode);
        Assert.Equal(unknown.StatusCode, known.StatusCode);
        Assert.Equal("invalid_password_reset_token", await ErrorCode(known));
        Assert.Equal("invalid_password_reset_token", await ErrorCode(unknown));
    }

    [Theory]
    [InlineData(7, false)]
    [InlineData(8, true)]
    [InlineData(9, true)]
    [InlineData(10, true)]
    [InlineData(128, true)]
    [InlineData(129, false)]
    public void Shared_validator_uses_unified_minimum(int length, bool accepted)
    {
        var password = "Aa1" + new string('x', length - 3);
        if (accepted) AuthenticationSupport.ValidatePassword(password);
        else Assert.Equal("weak_password", Assert.Throws<SpendWise.Application.Common.Exceptions.AppException>(
            () => AuthenticationSupport.ValidatePassword(password)).Code);
    }

    [Theory]
    [InlineData(7, false)]
    [InlineData(8, true)]
    [InlineData(9, true)]
    [InlineData(10, true)]
    [InlineData(128, true)]
    [InlineData(129, false)]
    public async Task Mobile_registration_accepts_unified_password_boundaries(int length, bool accepted)
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var password = "Aa1" + new string('x', length - 3);
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            "Synthetic Mobile User", $"mobile-{Guid.NewGuid():N}@fixture.invalid", IdentityTestData.NewPhone(), password, password));
        Assert.Equal(accepted ? HttpStatusCode.OK : HttpStatusCode.BadRequest, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        if (accepted)
        {
            var user = await db.Users.SingleAsync();
            Assert.Equal(SystemRole.User, user.SystemRole);
            Assert.True(new PasswordService().Verify(password, user.PasswordHash));
            Assert.Null(user.EmailVerifiedAt);
            Assert.Single(await db.EmailVerificationTokens.ToListAsync());
        }
        else
        {
            Assert.Equal("weak_password", await ErrorCode(response));
            Assert.Empty(await db.Users.ToListAsync());
        }
    }

    [Theory]
    [InlineData("abcdefg1")]
    [InlineData("ABCDEFG1")]
    [InlineData("Abcdefgh")]
    public void Unified_validator_preserves_composition_requirements(string password)
    {
        Assert.Equal("weak_password", Assert.Throws<SpendWise.Application.Common.Exceptions.AppException>(
            () => AuthenticationSupport.ValidatePassword(password)).Code);
    }

    private const string OldPassword = "SyntheticOldPassword2026";
    private const string NewPassword = "DifferentSyntheticPassword2026";
    private static async Task<string> ErrorCode(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("code").GetString()!;
    }
    private static async Task<(Guid Id, string Email)> SeedAsync(SpendWiseApiFactory factory, SystemRole role = SystemRole.SystemAdmin)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        var unique = Guid.NewGuid().ToString("N");
        var user = new User { FullName = "Synthetic SystemAdmin", Email = $"admin-{unique}@fixture.invalid",
            NormalizedEmail = $"ADMIN-{unique.ToUpperInvariant()}@FIXTURE.INVALID", SpendId = unique,
            NormalizedSpendId = unique.ToUpperInvariant(), Phone = IdentityTestData.NewPhone(),
            SystemRole = role, Status = UserStatus.Active, EmailVerifiedAt = DateTime.UtcNow,
            PasswordHash = new PasswordService().Hash(OldPassword),
            SecurityPinHash = new SecurityPinService().Hash(IdentityTestData.DefaultPin) };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return (user.Id, user.Email);
    }
}
