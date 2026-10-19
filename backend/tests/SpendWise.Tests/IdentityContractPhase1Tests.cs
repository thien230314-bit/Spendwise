using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SpendWise.Application.Features.Authentication.Commands.ForgotPassword;
using SpendWise.Application.Identity;
using SpendWise.Domain.Identity;
using SpendWise.Infrastructure.Identity;
using SpendWise.Infrastructure.Persistence;
using Xunit;

namespace SpendWise.Tests;

// Các test này khóa contract Identity Phase 1 mà frontend v0.1 sử dụng.
public sealed class IdentityContractPhase1Tests
{
    [Fact]
    public async Task Registration_accepts_locked_contract_and_generates_internal_spendid()
    {
        // Arrange: request chỉ chứa năm field frontend, không gửi SpendID.
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var email = $"phase1-{Guid.NewGuid():N}@test.local";
        var phone = IdentityTestData.NewPhone();

        // Act: đăng ký bằng FullName, Email, Phone, Password và ConfirmPassword.
        var response = await RegisterAsync(client, email, phone);

        // Assert: backend tạo SpendID/hash nhưng yêu cầu xác minh email trước PIN.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var pending = (await response.Content.ReadFromJsonAsync<EmailVerificationPendingResponse>())!;
        Assert.Equal("EMAIL_VERIFICATION_REQUIRED", pending.Status);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        var user = await db.Users.SingleAsync(item => item.NormalizedEmail == email.ToUpperInvariant());
        Assert.False(string.IsNullOrWhiteSpace(user.SpendId));
        Assert.Equal(phone, user.Phone);
        Assert.NotEqual("StrongPass1", user.PasswordHash);
        Assert.True(new PasswordService().Verify("StrongPass1", user.PasswordHash));
        Assert.Null(user.EmailVerifiedAt);
        Assert.Empty(await db.UserSessions.ToListAsync());
        Assert.Empty(await db.RefreshTokens.ToListAsync());
    }

    [Fact]
    public async Task Registration_rejects_invalid_phone_and_password_mismatch()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var unique = Guid.NewGuid().ToString("N");

        // Phone nước ngoài/không bắt đầu bằng 0 không được tự sửa thành số Việt Nam hợp lệ.
        var invalidPhone = await RegisterAsync(
            client,
            $"invalid-phone-{unique}@test.local",
            "8491234567");
        Assert.Equal(HttpStatusCode.BadRequest, invalidPhone.StatusCode);
        Assert.Equal("invalid_phone", await ProblemCodeAsync(invalidPhone));

        // Password xác nhận khác password phải bị chặn trước khi tạo tài khoản.
        var mismatch = await RegisterAsync(
            client,
            $"mismatch-{unique}@test.local",
            IdentityTestData.NewPhone(),
            confirmPassword: "DifferentPass1");
        Assert.Equal(HttpStatusCode.BadRequest, mismatch.StatusCode);
        Assert.Equal("password_mismatch", await ProblemCodeAsync(mismatch));
    }

    [Fact]
    public async Task Registration_rejects_duplicate_email_case_insensitively()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var email = $"case-{Guid.NewGuid():N}@test.local";
        (await RegisterAsync(client, email, IdentityTestData.NewPhone())).EnsureSuccessStatusCode();

        // Cùng email khác chữ hoa/thường vẫn là cùng identity.
        var duplicate = await RegisterAsync(
            client,
            email.ToUpperInvariant(),
            IdentityTestData.NewPhone());
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("email_exists", await ProblemCodeAsync(duplicate));
    }

    [Fact]
    public async Task Registration_rejects_duplicate_normalized_phone()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var phone = IdentityTestData.NewPhone();
        (await RegisterAsync(
            client,
            $"phone-a-{Guid.NewGuid():N}@test.local",
            phone)).EnsureSuccessStatusCode();

        var duplicate = await RegisterAsync(
            client,
            $"phone-b-{Guid.NewGuid():N}@test.local",
            $"  {phone}  ");
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("phone_exists", await ProblemCodeAsync(duplicate));
    }

    [Fact]
    public async Task Login_supports_email_phone_and_legacy_spendid_but_rejects_wrong_password()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var email = $"login-{Guid.NewGuid():N}@test.local";
        var phone = IdentityTestData.NewPhone();
        var registration = await RegisterAsync(client, email, phone);
        registration.EnsureSuccessStatusCode();
        var setupChallenge = await VerifyRegistrationAsync(client, email);
        var auth = await CompleteSetupAsync(client, setupChallenge.ChallengeToken);

        // Email không phân biệt hoa/thường và phone chuẩn hóa đều đăng nhập được.
        var emailLogin = await LoginAsync(client, email.ToUpperInvariant(), "StrongPass1");
        Assert.Equal(HttpStatusCode.OK, emailLogin.StatusCode);
        var emailChallenge = (await emailLogin.Content.ReadFromJsonAsync<LoginChallengeResponse>())!;
        Assert.Equal(
            "PIN_REQUIRED",
            emailChallenge.Status);
        await CompleteLoginAsync(client, emailChallenge.ChallengeToken);
        var phoneLogin = await LoginAsync(client, phone, "StrongPass1");
        Assert.Equal(HttpStatusCode.OK, phoneLogin.StatusCode);
        var phoneChallenge = (await phoneLogin.Content.ReadFromJsonAsync<LoginChallengeResponse>())!;
        await CompleteLoginAsync(client, phoneChallenge.ChallengeToken);

        // Chuyển role trong database để chứng minh SystemAdmin cũ vẫn đăng nhập an toàn bằng SpendID.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            var user = await db.Users.SingleAsync(item => item.Id == auth.User.Id);
            user.SystemRole = SystemRole.SystemAdmin;
            await db.SaveChangesAsync();
        }
        var adminLogin = await LoginAsync(client, auth.User.SpendId, "StrongPass1");
        Assert.Equal(HttpStatusCode.OK, adminLogin.StatusCode);
        var adminChallenge = (await adminLogin.Content.ReadFromJsonAsync<LoginChallengeResponse>())!;
        var adminAuth = await CompleteLoginAsync(client, adminChallenge.ChallengeToken);
        Assert.Equal(
            nameof(SystemRole.SystemAdmin),
            adminAuth.User.SystemRole);
        var wrongPassword = await LoginAsync(client, email, "WrongPassword1");
        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal("invalid_credentials", await ProblemCodeAsync(wrongPassword));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Forgot_password_accepts_email_or_phone_and_always_sends_to_registered_email(
        bool useEmail)
    {
        // Arrange: user có cả email và phone; outbox test giữ nội dung ở dạng đã bảo vệ.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var user = await fixture.AddUserAsync($"forgot-{Guid.NewGuid():N}");
        user.Phone = IdentityTestData.NewPhone();
        await fixture.Db.SaveChangesAsync();
        var handler = new ForgotPasswordCommandHandler(
            fixture.Db,
            EmailVerificationTestFactory.Create(fixture.Db, fixture.Clock));

        // Act: identifier có thể là email hoặc phone.
        await handler.Handle(
            new ForgotPasswordCommand(
                new ForgotPasswordRequest(
                    useEmail ? user.Email.ToUpperInvariant() : user.Phone),
                "203.0.113.30"),
            default);

        // Assert: OTP chỉ xếp tới email đã đăng ký, không gửi SMS hay lưu plaintext.
        var queued = await fixture.Db.EmailOutboxMessages.SingleAsync();
        Assert.Equal(user.Email, queued.RecipientEmail);
        var stored = await fixture.Db.EmailVerificationTokens.SingleAsync();
        Assert.Equal(EmailVerificationPurpose.ResetPassword, stored.Purpose);
        Assert.NotEqual(IdentityTestData.DefaultEmailOtp, stored.CodeHash);
    }

    [Fact]
    public async Task Forgot_password_public_response_does_not_disclose_account_existence()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();

        // Hai identifier không tồn tại phải nhận cùng status và cùng thông điệp trung lập.
        var unknownEmail = await client.PostAsJsonAsync(
            "/api/auth/forgot-password",
            new ForgotPasswordRequest($"missing-{Guid.NewGuid():N}@test.local"));
        var unknownPhone = await client.PostAsJsonAsync(
            "/api/auth/forgot-password",
            new ForgotPasswordRequest(IdentityTestData.NewPhone()));
        Assert.Equal(HttpStatusCode.Accepted, unknownEmail.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, unknownPhone.StatusCode);
        Assert.Equal(
            await unknownEmail.Content.ReadAsStringAsync(),
            await unknownPhone.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Database_unique_index_rejects_duplicate_phone_even_without_handler_precheck()
    {
        // Arrange: hai User được chèn trực tiếp để chứng minh database cũng bảo vệ uniqueness.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var phone = IdentityTestData.NewPhone();
        fixture.Db.Users.AddRange(
            DirectUser("phone-index-a", phone),
            DirectUser("phone-index-b", phone));

        // Act + Assert: unique index chặn duplicate dù bỏ qua API validation.
        await Assert.ThrowsAsync<DbUpdateException>(() => fixture.Db.SaveChangesAsync());
    }

    private static Task<HttpResponseMessage> RegisterAsync(
        HttpClient client,
        string email,
        string phone,
        string confirmPassword = "StrongPass1") =>
        client.PostAsJsonAsync(
            "/api/auth/register",
            new
            {
                fullName = "Identity Phase 1",
                email,
                phone,
                password = "StrongPass1",
                confirmPassword
            });

    private static Task<HttpResponseMessage> LoginAsync(
        HttpClient client,
        string identifier,
        string password) =>
        client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(identifier, password, "Identity Phase 1 tests"));

    private static async Task<AuthResponse> CompleteSetupAsync(
        HttpClient client,
        string challengeToken)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/pin/setup",
            new SetupSecurityPinRequest(
                challengeToken,
                IdentityTestData.DefaultPin,
                IdentityTestData.DefaultPin));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    private static async Task<LoginChallengeResponse> VerifyRegistrationAsync(
        HttpClient client,
        string email)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/email-verification/verify",
            new VerifyRegistrationEmailRequest(
                email,
                IdentityTestData.DefaultEmailOtp,
                "Identity Phase 1 tests"));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LoginChallengeResponse>())!;
    }

    private static async Task<AuthResponse> CompleteLoginAsync(
        HttpClient client,
        string challengeToken)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/pin/verify",
            new CompleteSecurityPinLoginRequest(
                challengeToken,
                IdentityTestData.DefaultPin));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    private static async Task<string?> ProblemCodeAsync(HttpResponseMessage response)
    {
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        return problem.GetProperty("code").GetString();
    }

    private static User DirectUser(string key, string phone) => new()
    {
        FullName = key,
        Email = $"{key}@test.local",
        NormalizedEmail = $"{key}@TEST.LOCAL",
        SpendId = key,
        NormalizedSpendId = key.ToUpperInvariant(),
        Phone = phone,
        PasswordHash = "hash-only-for-database-index-test"
    };

}
