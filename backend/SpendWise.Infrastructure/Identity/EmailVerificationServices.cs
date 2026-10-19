using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using SpendWise.Application.Identity;

namespace SpendWise.Infrastructure.Identity;

// Đọc các giới hạn OTP; deployment có thể ghi đè bằng environment hoặc user-secrets.
public sealed class EmailVerificationPolicy(
    IConfiguration configuration,
    IHostEnvironment environment) : IEmailVerificationPolicy
{
    public TimeSpan OtpLifetime => TimeSpan.FromMinutes(Positive("EmailVerification:OtpTtlMinutes", 10));
    public TimeSpan ResendCooldown => TimeSpan.FromSeconds(Positive("EmailVerification:ResendCooldownSeconds", 60));
    public int MaximumFailedAttempts => Positive("EmailVerification:MaximumFailedAttempts", 5);
    public int MaximumSendsPerHourPerAccount => Positive("EmailVerification:MaximumSendsPerHourPerAccount", 5);
    public int MaximumSendsPerHourPerIp => Positive("EmailVerification:MaximumSendsPerHourPerIp", 20);
    public string WebsiteUrl
    {
        get
        {
            var configured = configuration["Email:WebsiteUrl"];
            if (!string.IsNullOrWhiteSpace(configured)) return configured;
            if (environment.IsDevelopment() || environment.IsEnvironment("Testing"))
                return "https://localhost:5173";
            throw new InvalidOperationException("Email:WebsiteUrl must be configured outside Development.");
        }
    }

    private int Positive(string key, int fallback)
    {
        var value = configuration.GetValue<int?>(key) ?? fallback;
        return value > 0 ? value : fallback;
    }
}

// Tạo OTP tám chữ số bằng CSPRNG và bảo vệ hash bằng HMAC secret của deployment.
public sealed class EmailVerificationOtpService(IConfiguration configuration) : IEmailVerificationOtpService
{
    private readonly byte[] pepper = ReadPepper(configuration);

    public string CreateOtp() => RandomNumberGenerator.GetInt32(100_000_000).ToString("D8");

    public string HashOtp(Guid userId, string purpose, string otp) =>
        Hmac($"OTP|{purpose}|{userId:N}|{otp}");

    public bool VerifyOtp(Guid userId, string purpose, string otp, string expectedHash)
    {
        var actual = Convert.FromHexString(HashOtp(userId, purpose, otp));
        var expected = Convert.FromHexString(expectedHash);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    public string HashRateLimitKey(string value) => Hmac($"RATE|{value.Trim()}");

    private string Hmac(string value)
    {
        using var hmac = new HMACSHA256(pepper);
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(value)));
    }

    private static byte[] ReadPepper(IConfiguration configuration)
    {
        var value = configuration["EmailVerification:OtpPepper"];
        if (string.IsNullOrWhiteSpace(value) || Encoding.UTF8.GetByteCount(value) < 32)
        {
            throw new InvalidOperationException(
                "EmailVerification:OtpPepper must be supplied securely and contain at least 32 bytes.");
        }

        return SHA256.HashData(Encoding.UTF8.GetBytes(value));
    }
}
