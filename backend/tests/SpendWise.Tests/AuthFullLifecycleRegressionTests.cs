using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Authentication;
using SpendWise.Application.Identity;
using SpendWise.Domain.Identity;
using SpendWise.Domain.Notifications;
using SpendWise.Infrastructure.Notifications;
using SpendWise.Infrastructure.Persistence;
using Xunit;

namespace SpendWise.Tests;

public sealed class AuthFullLifecycleRegressionTests
{
    [Fact]
    public async Task New_user_must_setup_pin_and_cannot_get_session_without_pin()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var unique = Guid.NewGuid().ToString("N");
        var email = $"{unique}@test.local";

        // 1. Đăng ký tài khoản
        var registerRes = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("New User", email, IdentityTestData.NewPhone(), "StrongPass1", "StrongPass1"));
        registerRes.EnsureSuccessStatusCode();

        // 2. Xác thực email OTP
        var verifyRes = await client.PostAsJsonAsync("/api/auth/email-verification/verify",
            new VerifyRegistrationEmailRequest(email, IdentityTestData.DefaultEmailOtp, "Test Device"));
        verifyRes.EnsureSuccessStatusCode();
        var challenge = (await verifyRes.Content.ReadFromJsonAsync<LoginChallengeResponse>())!;

        Assert.Equal("PIN_SETUP_REQUIRED", challenge.Status);
        Assert.NotNull(challenge.ChallengeToken);

        // Chưa thiết lập PIN -> Chưa có session trong database
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            var user = await db.Users.SingleAsync(u => u.Email == email);
            Assert.Empty(await db.UserSessions.Where(s => s.UserId == user.Id).ToListAsync());
            Assert.Null(user.SecurityPinHash);
        }

        // Thiết lập PIN hợp lệ
        var setupRes = await client.PostAsJsonAsync("/api/auth/pin/setup",
            new SetupSecurityPinRequest(challenge.ChallengeToken, "123456", "123456"));
        setupRes.EnsureSuccessStatusCode();
        var auth = (await setupRes.Content.ReadFromJsonAsync<AuthResponse>())!;

        Assert.NotNull(auth.AccessToken);
        Assert.NotNull(auth.RefreshToken);

        // Sau khi setup PIN -> Session đã tồn tại
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            var user = await db.Users.SingleAsync(u => u.Email == email);
            Assert.Single(await db.UserSessions.Where(s => s.UserId == user.Id).ToListAsync());
            Assert.NotNull(user.SecurityPinHash);
        }
    }

    [Fact]
    public async Task Existing_user_with_pin_does_not_setup_again_and_logs_in_with_pin()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();

        var auth = await IdentityTestData.RegisterAndSetupPinAsync(client, "Existing User");
        var email = auth.User.Email;

        // Login với mật khẩu
        var loginRes = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(email, "StrongPass1", "Device 2"));
        loginRes.EnsureSuccessStatusCode();
        var challenge = (await loginRes.Content.ReadFromJsonAsync<LoginChallengeResponse>())!;

        // User đã có PIN -> Trạng thái trả về phải là PIN_REQUIRED, KHÔNG PHẢI PIN_SETUP_REQUIRED
        Assert.Equal("PIN_REQUIRED", challenge.Status);

        // Nếu cố gọi pin/setup sẽ bị từ chối
        var setupRes = await client.PostAsJsonAsync("/api/auth/pin/setup",
            new SetupSecurityPinRequest(challenge.ChallengeToken, "654321", "654321"));
        Assert.Equal(HttpStatusCode.Unauthorized, setupRes.StatusCode);

        // Verify PIN hoàn thành đăng nhập
        var verifyRes = await client.PostAsJsonAsync("/api/auth/pin/verify",
            new CompleteSecurityPinLoginRequest(challenge.ChallengeToken, IdentityTestData.DefaultPin));
        verifyRes.EnsureSuccessStatusCode();
        var secondAuth = (await verifyRes.Content.ReadFromJsonAsync<AuthResponse>())!;
        Assert.NotNull(secondAuth.AccessToken);
    }

    [Fact]
    public async Task Logout_then_login_again_uses_current_pin()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();

        var auth = await IdentityTestData.RegisterAndSetupPinAsync(client, "Relogin User");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        // Logout
        var logoutRes = await client.PostAsJsonAsync("/api/auth/logout", new RefreshRequest(auth.RefreshToken, "Logout"));
        Assert.Equal(HttpStatusCode.NoContent, logoutRes.StatusCode);

        // Token cũ không còn dùng được
        var walletBlocked = await client.GetAsync("/api/wallets");
        Assert.Equal(HttpStatusCode.Unauthorized, walletBlocked.StatusCode);

        // Login lại bằng PIN hiện tại
        client.DefaultRequestHeaders.Authorization = null;
        var reAuth = await IdentityTestData.LoginAndVerifyPinAsync(client, auth.User.Email);
        Assert.NotNull(reAuth.AccessToken);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", reAuth.AccessToken);
        var wallets = await client.GetAsync("/api/wallets");
        Assert.Equal(HttpStatusCode.OK, wallets.StatusCode);
    }

    [Fact]
    public async Task Change_pin_multiple_times_consecutively_and_old_pins_rejected()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();

        var auth = await IdentityTestData.RegisterAndSetupPinAsync(client, "Multi Pin Changer");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        // Đổi PIN lần 1: 123456 -> 234567
        var change1 = await client.PutAsJsonAsync("/api/security-pin",
            new ChangeSecurityPinRequest("123456", "234567", "234567"));
        Assert.Equal(HttpStatusCode.NoContent, change1.StatusCode);

        // Đổi PIN lần 2: 234567 -> 345678
        var change2 = await client.PutAsJsonAsync("/api/security-pin",
            new ChangeSecurityPinRequest("234567", "345678", "345678"));
        Assert.Equal(HttpStatusCode.NoContent, change2.StatusCode);

        // Đổi PIN lần 3: 345678 -> 456789
        var change3 = await client.PutAsJsonAsync("/api/security-pin",
            new ChangeSecurityPinRequest("345678", "456789", "456789"));
        Assert.Equal(HttpStatusCode.NoContent, change3.StatusCode);

        // Các PIN cũ (123456, 234567, 345678) đều bị từ chối
        var verifyOld1 = await client.PostAsJsonAsync("/api/security-pin/verify", new VerifySecurityPinRequest("123456"));
        Assert.Equal(HttpStatusCode.Unauthorized, verifyOld1.StatusCode);

        var verifyOld2 = await client.PostAsJsonAsync("/api/security-pin/verify", new VerifySecurityPinRequest("234567"));
        Assert.Equal(HttpStatusCode.Unauthorized, verifyOld2.StatusCode);

        var verifyOld3 = await client.PostAsJsonAsync("/api/security-pin/verify", new VerifySecurityPinRequest("345678"));
        Assert.Equal(HttpStatusCode.Unauthorized, verifyOld3.StatusCode);

        // PIN mới nhất (456789) được chấp nhận
        var verifyCurrent = await client.PostAsJsonAsync("/api/security-pin/verify", new VerifySecurityPinRequest("456789"));
        Assert.Equal(HttpStatusCode.NoContent, verifyCurrent.StatusCode);

        // Đổi sang chính PIN hiện tại (456789) bị lỗi Conflict pin_unchanged
        var changeSame = await client.PutAsJsonAsync("/api/security-pin",
            new ChangeSecurityPinRequest("456789", "456789", "456789"));
        Assert.Equal(HttpStatusCode.Conflict, changeSame.StatusCode);
    }

    [Fact]
    public async Task Change_password_multiple_times_and_old_passwords_rejected()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();

        var auth = await IdentityTestData.RegisterAndSetupPinAsync(client, "Multi Pass Changer");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        // Đổi mật khẩu lần 1: StrongPass1 -> StrongPass2
        var change1 = await client.PostAsJsonAsync("/api/auth/change-password",
            new ChangePasswordRequest("StrongPass1", "StrongPass2"));
        Assert.Equal(HttpStatusCode.NoContent, change1.StatusCode);

        // Login bằng mật khẩu cũ StrongPass1 bị từ chối
        client.DefaultRequestHeaders.Authorization = null;
        var failOld1 = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(auth.User.Email, "StrongPass1", "Device"));
        Assert.Equal(HttpStatusCode.Unauthorized, failOld1.StatusCode);

        // Login bằng mật khẩu mới StrongPass2 thành công
        var login2 = await IdentityTestData.LoginAndVerifyPinAsync(client, auth.User.Email, "StrongPass2");
        Assert.NotNull(login2.AccessToken);

        // Đổi mật khẩu lần 2: StrongPass2 -> StrongPass3
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login2.AccessToken);
        var change2 = await client.PostAsJsonAsync("/api/auth/change-password",
            new ChangePasswordRequest("StrongPass2", "StrongPass3"));
        Assert.Equal(HttpStatusCode.NoContent, change2.StatusCode);

        // Login bằng mật khẩu cũ StrongPass2 bị từ chối
        client.DefaultRequestHeaders.Authorization = null;
        var failOld2 = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(auth.User.Email, "StrongPass2", "Device"));
        Assert.Equal(HttpStatusCode.Unauthorized, failOld2.StatusCode);

        // Login bằng mật khẩu mới nhất StrongPass3 thành công
        var login3 = await IdentityTestData.LoginAndVerifyPinAsync(client, auth.User.Email, "StrongPass3");
        Assert.NotNull(login3.AccessToken);
    }

    [Fact]
    public async Task Forgot_pin_and_reset_pin_flow_with_token_lifecycle()
    {
        using var factory = new SpendWiseApiFactory(new Dictionary<string, string?> { ["Email:WebsiteUrl"] = "http://localhost:3000" });
        using var client = factory.CreateHttpsClient();

        var auth = await IdentityTestData.RegisterAndSetupPinAsync(client, "Forgot Pin User");
        var email = auth.User.Email;

        // Yêu cầu quên PIN
        var forgotRes = await client.PostAsJsonAsync("/api/auth/pin/forgot", new ForgotSecurityPinRequest(email));
        Assert.Equal(HttpStatusCode.Accepted, forgotRes.StatusCode);

        // Lấy token reset từ database (trong môi trường test)
        string rawToken;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            var emailMsg = await db.EmailOutboxMessages.FirstAsync(m => m.RecipientEmail == email && m.Subject.Contains("PIN"));
            var plain = scope.ServiceProvider.GetRequiredService<IEmailContentProtector>().Unprotect(emailMsg.TextBody);
            var match = System.Text.RegularExpressions.Regex.Match(plain, @"http://localhost:3000/reset-pin\?token=([^\s]+)");
            Assert.True(match.Success);
            rawToken = Uri.UnescapeDataString(match.Groups[1].Value);
        }

        Assert.False(string.IsNullOrWhiteSpace(rawToken));

        // Validate token
        var validateRes = await client.PostAsJsonAsync("/api/auth/pin/reset/validate",
            new ValidateSecurityPinResetTokenRequest(rawToken));
        validateRes.EnsureSuccessStatusCode();
        var status = (await validateRes.Content.ReadFromJsonAsync<SecurityPinResetTokenStatusDto>())!;
        Assert.True(status.Valid);

        // Reset PIN sang 999999
        var resetRes = await client.PostAsJsonAsync("/api/auth/pin/reset",
            new ResetSecurityPinRequest(rawToken, "999999", "999999"));
        Assert.Equal(HttpStatusCode.NoContent, resetRes.StatusCode);

        // Reusing the same reset token is rejected
        var reuseRes = await client.PostAsJsonAsync("/api/auth/pin/reset",
            new ResetSecurityPinRequest(rawToken, "888888", "888888"));
        Assert.Equal(HttpStatusCode.Unauthorized, reuseRes.StatusCode);

        // Login lại dùng PIN mới 999999 thành công
        var loginChallenge = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(email, "StrongPass1", "Device"));
        var challenge = (await loginChallenge.Content.ReadFromJsonAsync<LoginChallengeResponse>())!;
        var verifyNew = await client.PostAsJsonAsync("/api/auth/pin/verify",
            new CompleteSecurityPinLoginRequest(challenge.ChallengeToken, "999999"));
        verifyNew.EnsureSuccessStatusCode();

        // Old PIN 123456 rejected
        var loginChallengeOld = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(email, "StrongPass1", "Device"));
        var challengeOld = (await loginChallengeOld.Content.ReadFromJsonAsync<LoginChallengeResponse>())!;
        var verifyOld = await client.PostAsJsonAsync("/api/auth/pin/verify",
            new CompleteSecurityPinLoginRequest(challengeOld.ChallengeToken, "123456"));
        Assert.Equal(HttpStatusCode.Unauthorized, verifyOld.StatusCode);
    }

    [Fact]
    public async Task User_A_cannot_change_credentials_of_User_B()
    {
        using var factory = new SpendWiseApiFactory();
        using var clientA = factory.CreateHttpsClient();
        using var clientB = factory.CreateHttpsClient();

        var authA = await IdentityTestData.RegisterAndSetupPinAsync(clientA, "User A");
        var authB = await IdentityTestData.RegisterAndSetupPinAsync(clientB, "User B");

        clientA.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authA.AccessToken);
        clientB.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authB.AccessToken);

        // User A đổi PIN của chính mình
        var changePinA = await clientA.PutAsJsonAsync("/api/security-pin",
            new ChangeSecurityPinRequest("123456", "777777", "777777"));
        Assert.Equal(HttpStatusCode.NoContent, changePinA.StatusCode);

        // PIN của User B vẫn là 123456, hoàn toàn không bị ảnh hưởng bởi User A
        var verifyB = await clientB.PostAsJsonAsync("/api/security-pin/verify",
            new VerifySecurityPinRequest("123456"));
        Assert.Equal(HttpStatusCode.NoContent, verifyB.StatusCode);

        // User B không thể verify bằng PIN 777777 của User A
        var verifyBFail = await clientB.PostAsJsonAsync("/api/security-pin/verify",
            new VerifySecurityPinRequest("777777"));
        Assert.Equal(HttpStatusCode.Unauthorized, verifyBFail.StatusCode);
    }
}
