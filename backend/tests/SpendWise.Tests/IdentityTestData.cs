using System.Threading;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using SpendWise.Application.Finance;
using SpendWise.Application.Identity;

namespace SpendWise.Tests;

// Tạo phone hợp lệ và khác nhau để các test không đụng unique index của User.
internal static class IdentityTestData
{
    public const string DefaultPin = "123456";
    public const string DefaultEmailOtp = "12345678";
    private static long phoneSequence = 100_000_000;

    public static string NewPhone()
    {
        var suffix = Interlocked.Increment(ref phoneSequence) % 1_000_000_000;
        return $"0{suffix:D9}";
    }

    public static async Task<AuthResponse> RegisterAndSetupPinAsync(
        HttpClient client,
        string name)
    {
        var unique = Guid.NewGuid().ToString("N");
        var email = $"{unique}@test.local";
        var registration = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(
                name,
                email,
                NewPhone(),
                "StrongPass1",
                "StrongPass1"));
        registration.EnsureSuccessStatusCode();
        var pending = (await registration.Content.ReadFromJsonAsync<EmailVerificationPendingResponse>())!;
        if (pending.Status != "EMAIL_VERIFICATION_REQUIRED")
            throw new InvalidOperationException("Registration did not require email verification.");
        var verification = await client.PostAsJsonAsync(
            "/api/auth/email-verification/verify",
            new VerifyRegistrationEmailRequest(email, DefaultEmailOtp, "Test browser"));
        verification.EnsureSuccessStatusCode();
        var challenge = (await verification.Content.ReadFromJsonAsync<LoginChallengeResponse>())!;
        var setup = await client.PostAsJsonAsync(
            "/api/auth/pin/setup",
            new SetupSecurityPinRequest(
                challenge.ChallengeToken,
                DefaultPin,
                DefaultPin));
        setup.EnsureSuccessStatusCode();
        return (await setup.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    public static async Task<AuthResponse> LoginAndVerifyPinAsync(
        HttpClient client,
        string identifier,
        string password = "StrongPass1")
    {
        var login = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(identifier, password, "Test browser"));
        login.EnsureSuccessStatusCode();
        var challenge = (await login.Content.ReadFromJsonAsync<LoginChallengeResponse>())!;
        var verification = await client.PostAsJsonAsync(
            "/api/auth/pin/verify",
            new CompleteSecurityPinLoginRequest(challenge.ChallengeToken, DefaultPin));
        verification.EnsureSuccessStatusCode();
        return (await verification.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    public static async Task<EvidenceDto> UploadEvidenceAsync(HttpClient client, string fileName = "receipt.png")
    {
        // Gửi một PNG tối thiểu hợp lệ qua đúng multipart contract của API.
        using var form = new MultipartFormDataContent();
        using var file = new ByteArrayContent([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "file", fileName);
        var response = await client.PostAsync("/api/evidence", form);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<EvidenceDto>())!;
    }
}
