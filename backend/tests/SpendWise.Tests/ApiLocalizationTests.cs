using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using SpendWise.Application.Finance;
using SpendWise.Application.Identity;
using Xunit;

namespace SpendWise.Tests;

// Kiểm tra ngôn ngữ ở đúng HTTP boundary mà frontend sử dụng.
public sealed class ApiLocalizationTests
{
    [Theory]
    [InlineData("vi-VN", "Yêu cầu đăng nhập.")]
    [InlineData("en-US", "Authentication is required.")]
    public async Task Authentication_challenge_uses_requested_language(
        string language,
        string expectedMessage)
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        SetLanguage(client, language);

        var response = await client.GetAsync("/api/wallets");
        var error = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("unauthorized", error.GetProperty("code").GetString());
        Assert.Equal(expectedMessage, error.GetProperty("title").GetString());
    }

    [Theory]
    [InlineData("vi-VN", "Dữ liệu đầu vào không hợp lệ.", "Trường này là bắt buộc.")]
    [InlineData("en-US", "One or more validation errors occurred.", "This field is required.")]
    public async Task Model_validation_uses_requested_language_without_changing_contract(
        string language,
        string expectedTitle,
        string expectedFieldMessage)
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        SetLanguage(client, language);

        using var body = new StringContent("{}", Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/auth/login", body);
        var error = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(expectedTitle, error.GetProperty("title").GetString());
        Assert.Equal(400, error.GetProperty("status").GetInt32());
        Assert.Contains(
            error.GetProperty("errors").EnumerateObject(),
            field => field.Value.EnumerateArray()
                .Any(message => message.GetString() == expectedFieldMessage));
    }

    [Theory]
    [InlineData("vi-VN", "Thông tin đăng nhập không hợp lệ.")]
    [InlineData("en-US", "Invalid credentials.")]
    public async Task Authentication_error_uses_requested_language_and_keeps_code(
        string language,
        string expectedMessage)
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var user = await IdentityTestData.RegisterAndSetupPinAsync(client, "Localized auth");
        SetLanguage(client, language);

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(user.User.Email, "WrongPass1", "Test browser"));
        var error = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("invalid_credentials", error.GetProperty("code").GetString());
        Assert.Equal(expectedMessage, error.GetProperty("title").GetString());
    }

    [Theory]
    [InlineData("vi-VN", "Yêu cầu xác minh đăng nhập hoặc mã PIN không hợp lệ.")]
    [InlineData("en-US", "Login challenge or PIN verification failed.")]
    public async Task Pin_error_uses_requested_language_and_keeps_code(
        string language,
        string expectedMessage)
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var user = await IdentityTestData.RegisterAndSetupPinAsync(client, "Localized PIN");
        var login = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(user.User.Email, "StrongPass1", "Test browser"));
        var challenge = (await login.Content.ReadFromJsonAsync<LoginChallengeResponse>())!;
        SetLanguage(client, language);

        var response = await client.PostAsJsonAsync(
            "/api/auth/pin/verify",
            new CompleteSecurityPinLoginRequest(challenge.ChallengeToken, "654321"));
        var error = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("login_challenge_failed", error.GetProperty("code").GetString());
        Assert.Equal(expectedMessage, error.GetProperty("title").GetString());
    }

    [Theory]
    [InlineData("vi-VN", "Bạn không có quyền thực hiện thao tác này.")]
    [InlineData("en-US", "You do not have permission to perform this action.")]
    public async Task Authorization_error_uses_requested_language(
        string language,
        string expectedMessage)
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var user = await IdentityTestData.RegisterAndSetupPinAsync(client, "Localized authorization");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            user.AccessToken);
        SetLanguage(client, language);

        var response = await client.GetAsync("/api/admin/dashboard");
        var error = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("forbidden", error.GetProperty("code").GetString());
        Assert.Equal(expectedMessage, error.GetProperty("title").GetString());
    }

    [Theory]
    [InlineData("vi-VN", "Ví nguồn và ví đích phải khác nhau.")]
    [InlineData("en-US", "Source and destination wallets must differ.")]
    public async Task Financial_error_uses_requested_language_and_does_not_change_code(
        string language,
        string expectedMessage)
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        var user = await IdentityTestData.RegisterAndSetupPinAsync(client, "Localized finance");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            user.AccessToken);
        var wallets = await client.GetFromJsonAsync<IReadOnlyList<WalletDto>>("/api/wallets");
        var walletId = Assert.Single(wallets!).Id;
        SetLanguage(client, language);

        var response = await client.PostAsJsonAsync(
            "/api/transfers/internal",
            new InternalTransferRequest(
                walletId,
                walletId,
                1_000m,
                "Same wallet",
                $"localization-{Guid.NewGuid():N}",
                IdentityTestData.DefaultPin));
        var error = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("same_wallet", error.GetProperty("code").GetString());
        Assert.Equal(expectedMessage, error.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Default_language_is_vietnamese_and_login_challenge_has_no_success_message()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();

        var unauthorized = await client.GetAsync("/api/wallets");
        var error = await ReadJsonAsync(unauthorized);
        Assert.Equal("Yêu cầu đăng nhập.", error.GetProperty("title").GetString());

        var unique = Guid.NewGuid().ToString("N");
        var registration = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(
                "Challenge only",
                $"{unique}@test.local",
                IdentityTestData.NewPhone(),
                "StrongPass1",
                "StrongPass1"));
        var challenge = await ReadJsonAsync(registration);

        Assert.Equal(HttpStatusCode.OK, registration.StatusCode);
        Assert.Equal("EMAIL_VERIFICATION_REQUIRED", challenge.GetProperty("status").GetString());
        Assert.False(challenge.TryGetProperty("message", out _));
        Assert.False(challenge.TryGetProperty("accessToken", out _));
    }

    [Theory]
    [InlineData("vi-VN", "Nếu thông tin khớp với một tài khoản, hướng dẫn đặt lại mật khẩu sẽ được gửi đến email đã đăng ký.")]
    [InlineData("en-US", "If an account matches the information provided, password reset instructions will be sent to the registered email.")]
    public async Task Existing_password_reset_instruction_uses_requested_language(
        string language,
        string expectedMessage)
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        SetLanguage(client, language);

        var response = await client.PostAsJsonAsync(
            "/api/auth/forgot-password",
            new ForgotPasswordRequest($"missing-{Guid.NewGuid():N}@test.local"));
        var payload = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(expectedMessage, payload.GetProperty("message").GetString());
    }

    private static void SetLanguage(HttpClient client, string language)
    {
        client.DefaultRequestHeaders.AcceptLanguage.Clear();
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(language);
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }
}
