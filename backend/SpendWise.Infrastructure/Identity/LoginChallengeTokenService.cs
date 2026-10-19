using System.Security.Cryptography;
using System.Text;
using SpendWise.Application.Identity;

namespace SpendWise.Infrastructure.Identity;

// Tạo challenge token ngẫu nhiên và chỉ lưu SHA-256 hash ở database.
public sealed class LoginChallengeTokenService : ILoginChallengeTokenService
{
    public string CreateToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));

    public string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
