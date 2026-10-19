using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SpendWise.Application.Identity;
using SpendWise.Domain.Identity;

namespace SpendWise.Infrastructure.Identity;

// Phát hành access token từ danh tính đã được Application xác thực.

public sealed class JwtTokenService(IConfiguration configuration, TimeProvider timeProvider) : ITokenService
{
    public AccessTokenResult CreateAccessToken(User user, Guid sessionId)
    {
        // Tạo JWT chứa UserId, role và SessionId với thời hạn cấu hình.
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var expiry = now.AddMinutes(configuration.GetValue("Jwt:AccessTokenMinutes", 15));
        var key = configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is required.");
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()), new Claim(ClaimTypes.Name, user.SpendId),
            new Claim(ClaimTypes.Email, user.Email), new Claim(ClaimTypes.Role, user.SystemRole.ToString()),
            new Claim("sid", sessionId.ToString()), new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken(configuration["Jwt:Issuer"], configuration["Jwt:Audience"], claims, now, expiry, credentials);
        return new AccessTokenResult(new JwtSecurityTokenHandler().WriteToken(jwt), expiry);
    }

    public string CreateRefreshToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    public string HashRefreshToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
