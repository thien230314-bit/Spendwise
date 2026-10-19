using SpendWise.Domain.Identity;

namespace SpendWise.Application.Identity;

// Các interface này tách việc phát hành token và băm mật khẩu khỏi use case xác thực.

public sealed record AccessTokenResult(string Token, DateTime ExpiresAt);

public interface ITokenService
{
    AccessTokenResult CreateAccessToken(User user, Guid sessionId);
    string CreateRefreshToken();
    string HashRefreshToken(string token);
}

public interface IPasswordService
{
    string Hash(string password);
    bool Verify(string password, string encodedHash);
}
