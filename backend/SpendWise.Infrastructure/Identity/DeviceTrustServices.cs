using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using SpendWise.Application.Identity;

namespace SpendWise.Infrastructure.Identity;

// Token ngẫu nhiên có entropy cao; database chỉ giữ SHA-256 của token thô.
public sealed class DeviceTrustTokenService : IDeviceTrustTokenService
{
    public string CreateToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    public string HashToken(string rawToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

    public bool TryHashToken(string? rawToken, out string tokenHash)
    {
        tokenHash = string.Empty;
        if (string.IsNullOrWhiteSpace(rawToken)
            || rawToken.Length != 64
            || rawToken.Any(character => !Uri.IsHexDigit(character)))
        {
            return false;
        }

        tokenHash = HashToken(rawToken);
        return true;
    }
}

// Thời hạn tin cậy mặc định 180 ngày và có thể ghi đè bằng configuration.
public sealed class DeviceTrustPolicy(IConfiguration configuration) : IDeviceTrustPolicy
{
    public TimeSpan Lifetime
    {
        get
        {
            var days = configuration.GetValue<int?>("DeviceTrust:LifetimeDays") ?? 180;
            if (days is < 1 or > 3650)
                throw new InvalidOperationException("DeviceTrust:LifetimeDays must be between 1 and 3650.");
            return TimeSpan.FromDays(days);
        }
    }
}
