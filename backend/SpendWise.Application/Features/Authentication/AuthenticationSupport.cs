using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Identity;
using SpendWise.Domain.Identity;

namespace SpendWise.Application.Features.Authentication;

// Lớp này tạo token, phiên đăng nhập và hồ sơ sau khi thông tin xác thực đã hợp lệ.

public sealed class AuthenticationSupport(
    ISpendWiseDbContext db,
    ITokenService tokens,
    IClock clock)
{
    public async Task<AuthResponse> IssueAsync(
        User user,
        string device,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken,
        string? deviceIdentifierHash = null,
        string? rawDeviceTrustToken = null,
        Guid? sessionId = null)
    {
        if (user.SystemRole == SystemRole.SuperAdmin)
            throw new AppException("account_unavailable", "This account is unavailable in V1.0.", 403);
        if (user.EmailVerifiedAt is null)
        {
            throw new AppException(
                "email_verification_required",
                "Email verification is required before creating a session.",
                403);
        }

        // Tạo session để access token gắn với một lần đăng nhập cụ thể.
        var safeDevice = device.Length <= 200 ? device : device[..200];
        var safeIpAddress = ipAddress is null || ipAddress.Length <= 64 ? ipAddress : ipAddress[..64];
        var safeUserAgent = userAgent is null || userAgent.Length <= 512 ? userAgent : userAgent[..512];
        var session = new UserSession
        {
            Id = sessionId ?? Guid.NewGuid(),
            UserId = user.Id,
            DeviceName = safeDevice,
            DeviceIdentifier = deviceIdentifierHash,
            IpAddress = safeIpAddress,
            UserAgent = safeUserAgent,
            LastSeenAt = clock.UtcNow
        };
        // Chỉ lưu hash của refresh token; giá trị thô chỉ trả một lần cho client.
        var rawRefreshToken = tokens.CreateRefreshToken();
        var refreshToken = new RefreshToken
        {
            UserId = user.Id,
            UserSessionId = session.Id,
            UserSession = session,
            TokenHash = tokens.HashRefreshToken(rawRefreshToken),
            ExpiresAt = clock.UtcNow.AddDays(30),
            DeviceName = safeDevice,
            IpAddress = safeIpAddress
        };

        db.UserSessions.Add(session);
        db.RefreshTokens.Add(refreshToken);
        await db.SaveChangesAsync(cancellationToken);

        // Tạo access token sau khi session và refresh token đã được lưu thành công.
        var accessToken = tokens.CreateAccessToken(user, session.Id);
        return new AuthResponse(
            accessToken.Token,
            rawRefreshToken,
            accessToken.ExpiresAt,
            MapProfile(user),
            rawDeviceTrustToken);
    }

    public async Task RevokeAllSessionsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        // Token và session phải được thu hồi cùng nhau, kể cả khi gọi ngoài transaction khác.
        await db.ExecuteInTransactionAsync(async token =>
        {
            var revokedAt = clock.UtcNow;
            await db.RefreshTokens
                .Where(x => x.UserId == userId && x.RevokedAt == null)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(x => x.RevokedAt, revokedAt),
                    token);
            await db.UserSessions
                .Where(x => x.UserId == userId && x.RevokedAt == null)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(x => x.RevokedAt, revokedAt),
                    token);
            return true;
        }, cancellationToken);
    }

    public async Task RevokeSessionAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        // Logout chỉ thu hồi token thuộc session hiện tại, không ảnh hưởng thiết bị khác.
        await db.ExecuteInTransactionAsync(async token =>
        {
            var revokedAt = clock.UtcNow;
            await db.RefreshTokens
                .Where(refreshToken => refreshToken.UserId == userId
                    && refreshToken.UserSessionId == sessionId
                    && refreshToken.RevokedAt == null)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        refreshToken => refreshToken.RevokedAt,
                        revokedAt),
                    token);
            await db.UserSessions
                .Where(session => session.Id == sessionId
                    && session.UserId == userId
                    && session.RevokedAt == null)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(session => session.RevokedAt, revokedAt),
                    token);
            return true;
        }, cancellationToken);
    }

    public static void ValidatePassword(string password)
    {
        // Mật khẩu phải đủ dài và có chữ hoa, chữ thường, chữ số.
        var isStrong = !string.IsNullOrEmpty(password)
            && password.Length is >= 8 and <= 128
            && password.Any(char.IsUpper)
            && password.Any(char.IsLower)
            && password.Any(char.IsDigit);
        if (!isStrong)
        {
            throw new AppException(
                "weak_password",
                "Password must contain 8-128 characters, upper/lower case letters, and a number.");
        }
    }

    public static UserProfile MapProfile(User user)
    {
        // Chỉ đưa các trường hồ sơ được phép trả cho client vào UserProfile.
        return new UserProfile(
            user.Id,
            user.FullName,
            user.Email,
            user.SpendId,
            user.Phone,
            user.SystemRole.ToString(),
            user.Status.ToString(),
            user.MustChangePassword,
            user.PreferredLanguage,
            user.PreferredTheme,
            user.DefaultCurrency,
            user.CreatedAt,
            user.LastLoginAt,
            user.EngagementEmailOptIn);
    }
}
