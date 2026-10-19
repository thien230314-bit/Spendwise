using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpendWise.Application.Features.Authentication;
using SpendWise.Application.Identity;
using SpendWise.Domain.Identity;
using SpendWise.Infrastructure.Identity;
using SpendWise.Infrastructure.Persistence;
using Xunit;

namespace SpendWise.Tests;

// Các test này bảo vệ bước PIN bắt buộc giữa password và session cuối cùng.
public sealed class SecurityPinAuthenticationTests
{
    [Fact]
    public async Task Pin_requires_exactly_six_numeric_digits()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var registration = await RegisterAsync(client, "PIN format");

        // PIN thiếu chữ số hoặc chứa ký tự đều bị từ chối trước khi lưu.
        var shortPin = await SetupAsync(client, registration.ChallengeToken, "12345", "12345");
        var alphaPin = await SetupAsync(client, registration.ChallengeToken, "12A456", "12A456");
        Assert.Equal(HttpStatusCode.BadRequest, shortPin.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, alphaPin.StatusCode);
        Assert.Equal("invalid_pin_format", await ProblemCodeAsync(shortPin));
        Assert.Equal("invalid_pin_format", await ProblemCodeAsync(alphaPin));
    }

    [Fact]
    public async Task Initial_pin_setup_hashes_pin_and_only_then_creates_session()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var registration = await RegisterAsync(client, "PIN setup");

        // Dữ liệu khóa cũ không được tồn tại sau khi người dùng enrollment PIN thành công.
        using (var staleScope = factory.Services.CreateScope())
        {
            var staleDb = staleScope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            var staleUser = await staleDb.Users.SingleAsync();
            staleUser.SecurityPinFailedAttempts = 5;
            staleUser.SecurityPinLockoutUntil = DateTime.UtcNow.AddMinutes(5);
            await staleDb.SaveChangesAsync();
        }

        // Password registration chỉ tạo challenge, chưa tạo credential đăng nhập.
        using (var beforeScope = factory.Services.CreateScope())
        {
            var before = beforeScope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            Assert.Empty(await before.UserSessions.ToListAsync());
            Assert.Empty(await before.RefreshTokens.ToListAsync());
        }

        var setup = await SetupAsync(
            client,
            registration.ChallengeToken,
            IdentityTestData.DefaultPin,
            IdentityTestData.DefaultPin);
        setup.EnsureSuccessStatusCode();
        var auth = (await setup.Content.ReadFromJsonAsync<AuthResponse>())!;
        Assert.False(string.IsNullOrWhiteSpace(auth.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(auth.RefreshToken));

        // Database chỉ chứa salted hash, không chứa PIN sáu chữ số.
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        var user = await db.Users.SingleAsync(item => item.Id == auth.User.Id);
        Assert.NotEqual(IdentityTestData.DefaultPin, user.SecurityPinHash);
        Assert.True(new SecurityPinService().Verify(
            IdentityTestData.DefaultPin,
            user.SecurityPinHash!));
        Assert.Equal(0, user.SecurityPinFailedAttempts);
        Assert.Null(user.SecurityPinLockoutUntil);
        Assert.Single(await db.UserSessions.ToListAsync());
        Assert.Single(await db.RefreshTokens.ToListAsync());
    }

    [Fact]
    public async Task Password_login_only_creates_challenge_and_pin_verification_completes_login()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var enrolled = await RegisterAndSetupAsync(client, "Existing PIN");
        int sessionsBefore;
        using (var beforeScope = factory.Services.CreateScope())
        {
            sessionsBefore = await beforeScope.ServiceProvider
                .GetRequiredService<SpendWiseDbContext>()
                .UserSessions.CountAsync();
        }

        var login = await PasswordLoginAsync(client, enrolled.Auth.User.Email);
        Assert.Equal("PIN_REQUIRED", login.Status);
        using (var challengeScope = factory.Services.CreateScope())
        {
            var db = challengeScope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            Assert.Equal(sessionsBefore, await db.UserSessions.CountAsync());
        }

        var completed = await VerifyLoginAsync(
            client,
            login.ChallengeToken,
            IdentityTestData.DefaultPin);
        completed.EnsureSuccessStatusCode();
        using var finalScope = factory.Services.CreateScope();
        Assert.Equal(
            sessionsBefore + 1,
            await finalScope.ServiceProvider.GetRequiredService<SpendWiseDbContext>()
                .UserSessions.CountAsync());
    }

    [Fact]
    public async Task Wrong_pin_cannot_complete_login_and_five_failures_consume_challenge()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var enrolled = await RegisterAndSetupAsync(client, "Wrong PIN");
        var login = await PasswordLoginAsync(client, enrolled.Auth.User.Email);

        // Năm lần sai đều trả lỗi chung và lần thứ năm khóa challenge.
        for (var attempt = 0; attempt < LoginChallengeSupport.MaximumFailedAttempts; attempt++)
        {
            var wrong = await VerifyLoginAsync(client, login.ChallengeToken, "654321");
            Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
            Assert.Equal("login_challenge_failed", await ProblemCodeAsync(wrong));
        }
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await VerifyLoginAsync(
                client,
                login.ChallengeToken,
                IdentityTestData.DefaultPin)).StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        var stored = await db.LoginChallenges.SingleAsync(
            item => item.TokenHash == new LoginChallengeTokenService()
                .HashToken(login.ChallengeToken));
        Assert.Equal(LoginChallengeSupport.MaximumFailedAttempts, stored.FailedAttempts);
        Assert.NotNull(stored.UsedAt);
    }

    [Fact]
    public async Task Expired_and_reused_login_challenges_are_rejected()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var enrolled = await RegisterAndSetupAsync(client, "Challenge lifecycle");
        var expired = await PasswordLoginAsync(client, enrolled.Auth.User.Email);

        // Làm challenge hết hạn trong database để kiểm tra server-authoritative expiry.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            var hash = new LoginChallengeTokenService().HashToken(expired.ChallengeToken);
            var stored = await db.LoginChallenges.SingleAsync(item => item.TokenHash == hash);
            stored.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await VerifyLoginAsync(client, expired.ChallengeToken, IdentityTestData.DefaultPin)).StatusCode);

        var singleUse = await PasswordLoginAsync(client, enrolled.Auth.User.Email);
        (await VerifyLoginAsync(client, singleUse.ChallengeToken, IdentityTestData.DefaultPin))
            .EnsureSuccessStatusCode();
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await VerifyLoginAsync(client, singleUse.ChallengeToken, IdentityTestData.DefaultPin)).StatusCode);
    }

    [Fact]
    public async Task User_and_system_admin_both_receive_pin_required_challenge()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var user = await RegisterAndSetupAsync(client, "PIN user");
        var admin = await RegisterAndSetupAsync(client, "PIN admin");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            var adminEntity = await db.Users.SingleAsync(item => item.Id == admin.Auth.User.Id);
            adminEntity.SystemRole = SystemRole.SystemAdmin;
            await db.SaveChangesAsync();
        }

        Assert.Equal(
            "PIN_REQUIRED",
            (await PasswordLoginAsync(client, user.Auth.User.Email)).Status);
        Assert.Equal(
            "PIN_REQUIRED",
            (await PasswordLoginAsync(client, admin.Auth.User.Email)).Status);
    }

    [Fact]
    public async Task Authenticated_pin_status_verify_and_change_require_current_pin()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var enrolled = await RegisterAndSetupAsync(client, "PIN management");
        UseToken(client, enrolled.Auth.AccessToken);

        var status = await client.GetFromJsonAsync<SecurityPinStatusDto>("/api/security-pin/status");
        Assert.True(status!.IsSet);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await client.PostAsJsonAsync(
                "/api/security-pin/verify",
                new VerifySecurityPinRequest(IdentityTestData.DefaultPin))).StatusCode);

        var wrongCurrent = await client.PutAsJsonAsync(
            "/api/security-pin",
            new ChangeSecurityPinRequest("654321", "222222", "222222"));
        Assert.Equal(HttpStatusCode.Unauthorized, wrongCurrent.StatusCode);
        var unchanged = await client.PutAsJsonAsync(
            "/api/security-pin",
            new ChangeSecurityPinRequest(
                IdentityTestData.DefaultPin,
                IdentityTestData.DefaultPin,
                IdentityTestData.DefaultPin));
        Assert.Equal(HttpStatusCode.Conflict, unchanged.StatusCode);
        Assert.Equal("pin_unchanged", await ProblemCodeAsync(unchanged));
        var changed = await client.PutAsJsonAsync(
            "/api/security-pin",
            new ChangeSecurityPinRequest(IdentityTestData.DefaultPin, "222222", "222222"));
        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);

        client.DefaultRequestHeaders.Authorization = null;
        var login = await PasswordLoginAsync(client, enrolled.Auth.User.Email);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await VerifyLoginAsync(client, login.ChallengeToken, IdentityTestData.DefaultPin)).StatusCode);
        (await VerifyLoginAsync(client, login.ChallengeToken, "222222")).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Must_change_password_blocks_business_api_until_password_is_changed()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var enrolled = await RegisterAndSetupAsync(client, "Forced password change");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            var user = await db.Users.SingleAsync(item => item.Id == enrolled.Auth.User.Id);
            user.MustChangePassword = true;
            await db.SaveChangesAsync();
        }

        var challenge = await PasswordLoginAsync(client, enrolled.Auth.User.Email);
        var completed = await VerifyLoginAsync(client, challenge.ChallengeToken, IdentityTestData.DefaultPin);
        completed.EnsureSuccessStatusCode();
        var forcedAuth = (await completed.Content.ReadFromJsonAsync<AuthResponse>())!;
        Assert.True(forcedAuth.User.MustChangePassword);
        UseToken(client, forcedAuth.AccessToken);
        var blocked = await client.GetAsync("/api/wallets");
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
        Assert.Equal("PASSWORD_CHANGE_REQUIRED", await ProblemCodeAsync(blocked));

        // Hồ sơ bảo mật và endpoint đổi password vẫn dùng được trong trạng thái bắt buộc.
        var profile = await client.GetFromJsonAsync<UserProfile>("/api/users/me");
        Assert.True(profile!.MustChangePassword);
        using (var lockoutScope = factory.Services.CreateScope())
        {
            var lockoutDb = lockoutScope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            var lockedUser = await lockoutDb.Users.SingleAsync(item => item.Id == enrolled.Auth.User.Id);
            lockedUser.PasswordFailedAttempts = 5;
            lockedUser.PasswordLockoutUntil = DateTime.UtcNow.AddMinutes(5);
            await lockoutDb.SaveChangesAsync();
        }
        var changed = await client.PostAsJsonAsync(
            "/api/auth/change-password",
            new ChangePasswordRequest("StrongPass1", "ChangedPass2"));
        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);
        using var finalScope = factory.Services.CreateScope();
        var finalDb = finalScope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        var changedUser = await finalDb.Users.SingleAsync(item => item.Id == enrolled.Auth.User.Id);
        Assert.False(changedUser.MustChangePassword);
        Assert.Equal(0, changedUser.PasswordFailedAttempts);
        Assert.Null(changedUser.PasswordLockoutUntil);
        Assert.All(
            await finalDb.UserSessions.Where(item => item.UserId == enrolled.Auth.User.Id).ToListAsync(),
            item => Assert.NotNull(item.RevokedAt));
    }

    [Fact]
    public async Task Refresh_rotation_still_works_after_pin_completed_login()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var enrolled = await RegisterAndSetupAsync(client, "PIN refresh");
        var login = await PasswordLoginAsync(client, enrolled.Auth.User.Email);
        var completed = await VerifyLoginAsync(
            client,
            login.ChallengeToken,
            IdentityTestData.DefaultPin);
        completed.EnsureSuccessStatusCode();
        var auth = (await completed.Content.ReadFromJsonAsync<AuthResponse>())!;

        var refresh = await client.PostAsJsonAsync(
            "/api/auth/refresh",
            new RefreshRequest(auth.RefreshToken, "PIN refresh rotated"));
        refresh.EnsureSuccessStatusCode();
        var rotated = (await refresh.Content.ReadFromJsonAsync<AuthResponse>())!;
        Assert.NotEqual(auth.RefreshToken, rotated.RefreshToken);
        Assert.False(string.IsNullOrWhiteSpace(rotated.AccessToken));
    }

    private static async Task<LoginChallengeResponse> RegisterAsync(
        HttpClient client,
        string name)
    {
        var unique = Guid.NewGuid().ToString("N");
        var email = $"{unique}@test.local";
        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(
                name,
                email,
                IdentityTestData.NewPhone(),
                "StrongPass1",
                "StrongPass1"));
        response.EnsureSuccessStatusCode();
        var pending = (await response.Content.ReadFromJsonAsync<EmailVerificationPendingResponse>())!;
        Assert.Equal("EMAIL_VERIFICATION_REQUIRED", pending.Status);
        var verification = await client.PostAsJsonAsync(
            "/api/auth/email-verification/verify",
            new VerifyRegistrationEmailRequest(email, IdentityTestData.DefaultEmailOtp, "PIN tests"));
        verification.EnsureSuccessStatusCode();
        return (await verification.Content.ReadFromJsonAsync<LoginChallengeResponse>())!;
    }

    private static async Task<(AuthResponse Auth, string Pin)> RegisterAndSetupAsync(
        HttpClient client,
        string name)
    {
        var registration = await RegisterAsync(client, name);
        Assert.Equal("PIN_SETUP_REQUIRED", registration.Status);
        var response = await SetupAsync(
            client,
            registration.ChallengeToken,
            IdentityTestData.DefaultPin,
            IdentityTestData.DefaultPin);
        response.EnsureSuccessStatusCode();
        return ((await response.Content.ReadFromJsonAsync<AuthResponse>())!, IdentityTestData.DefaultPin);
    }

    private static Task<HttpResponseMessage> SetupAsync(
        HttpClient client,
        string challengeToken,
        string pin,
        string confirmPin) =>
        client.PostAsJsonAsync(
            "/api/auth/pin/setup",
            new SetupSecurityPinRequest(challengeToken, pin, confirmPin));

    private static async Task<LoginChallengeResponse> PasswordLoginAsync(
        HttpClient client,
        string identifier)
    {
        client.DefaultRequestHeaders.Authorization = null;
        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(identifier, "StrongPass1", "PIN tests"));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LoginChallengeResponse>())!;
    }

    private static Task<HttpResponseMessage> VerifyLoginAsync(
        HttpClient client,
        string challengeToken,
        string pin) =>
        client.PostAsJsonAsync(
            "/api/auth/pin/verify",
            new CompleteSecurityPinLoginRequest(challengeToken, pin));

    private static async Task<string?> ProblemCodeAsync(HttpResponseMessage response)
    {
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        return problem.GetProperty("code").GetString();
    }

    private static void UseToken(HttpClient client, string accessToken) =>
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);
}
