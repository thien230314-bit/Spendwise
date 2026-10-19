using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Identity;
using SpendWise.Domain.Identity;

namespace SpendWise.Application.Features.Authentication.Commands.RefreshToken;

// Xoay refresh token theo cơ chế dùng một lần và giữ nguyên phiên đăng nhập.
public sealed class RefreshTokenCommandHandler(
    ISpendWiseDbContext db,
    ITokenService tokens,
    IClock clock) : IRequestHandler<RefreshTokenCommand, AuthResponse>
{
    public async Task<AuthResponse> Handle(
        RefreshTokenCommand command,
        CancellationToken cancellationToken)
    {
        // Refresh token chỉ được tra cứu bằng giá trị hash.
        InputBoundary.RequiredText(command.Request.RefreshToken, 500, "Refresh token");
        InputBoundary.OptionalText(command.Request.DeviceName, 200, "Device name");
        var tokenHash = tokens.HashRefreshToken(command.Request.RefreshToken);
        var consumedAt = clock.UtcNow;

        // Consume và tạo token thay thế nằm trong cùng transaction; chỉ một request thắng.
        return await db.ExecuteInTransactionAsync(async token =>
        {
            // UPDATE có điều kiện là điểm nguyên tử ngăn hai request cùng dùng một token.
            var consumedRows = await db.RefreshTokens
                .Where(refreshToken => refreshToken.TokenHash == tokenHash
                    && refreshToken.RevokedAt == null
                    && refreshToken.ExpiresAt > consumedAt)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        refreshToken => refreshToken.RevokedAt,
                        consumedAt),
                    token);
            if (consumedRows != 1)
            {
                // Chỉ phân biệt tài khoản bị khóa; mọi token/session lỗi khác đều trả Unauthorized.
                var unavailableAccount = await db.RefreshTokens
                    .AsNoTracking()
                    .AnyAsync(
                        refreshToken => refreshToken.TokenHash == tokenHash
                            && (refreshToken.User.Status != UserStatus.Active
                                || refreshToken.User.EmailVerifiedAt == null),
                        token);
                if (unavailableAccount)
                {
                    throw new AppException(
                        "account_unavailable",
                        "The account is not active.",
                        403);
                }

                throw new AppException(
                    "invalid_refresh_token",
                    "Refresh token is invalid, expired, revoked, or its session is unavailable.",
                    401);
            }

            var oldToken = await db.RefreshTokens
                .AsNoTracking()
                .Include(refreshToken => refreshToken.User)
                .Include(refreshToken => refreshToken.UserSession)
                .SingleAsync(refreshToken => refreshToken.TokenHash == tokenHash, token);
            if (oldToken.User.Status != UserStatus.Active)
            {
                // Throw làm transaction rollback thao tác consume vừa thực hiện.
                throw new AppException(
                    "account_unavailable",
                    "The account is not active.",
                    403);
            }
            if (oldToken.User.EmailVerifiedAt is null)
            {
                throw new AppException(
                    "email_verification_required",
                    "Email verification is required before refreshing a session.",
                    403);
            }
            if (oldToken.UserSession.RevokedAt is not null
                || oldToken.UserSession.UserId != oldToken.UserId)
            {
                // Session bị thu hồi hoặc sai chủ sở hữu không được phép rotate token.
                throw new AppException(
                    "invalid_refresh_token",
                    "Refresh token session is unavailable.",
                    401);
            }

            var rawReplacement = tokens.CreateRefreshToken();
            var replacement = new SpendWise.Domain.Identity.RefreshToken
            {
                UserId = oldToken.UserId,
                UserSessionId = oldToken.UserSessionId,
                TokenHash = tokens.HashRefreshToken(rawReplacement),
                ExpiresAt = clock.UtcNow.AddDays(30),
                DeviceName = command.Request.DeviceName
                    ?? oldToken.DeviceName
                    ?? oldToken.UserSession.DeviceName,
                IpAddress = command.IpAddress ?? oldToken.IpAddress
            };
            db.RefreshTokens.Add(replacement);
            await db.SaveChangesAsync(token);

            // Lưu liên kết rotation sau khi token thay thế đã tồn tại để thỏa khóa ngoại.
            await db.RefreshTokens
                .Where(refreshToken => refreshToken.Id == oldToken.Id)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        refreshToken => refreshToken.ReplacedByTokenId,
                        replacement.Id),
                    token);

            // Access token mới giữ nguyên sid của phiên đăng nhập ban đầu.
            var accessToken = tokens.CreateAccessToken(
                oldToken.User,
                oldToken.UserSessionId);
            return new AuthResponse(
                accessToken.Token,
                rawReplacement,
                accessToken.ExpiresAt,
                AuthenticationSupport.MapProfile(oldToken.User));
        }, cancellationToken);
    }
}
