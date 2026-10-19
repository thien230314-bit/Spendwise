using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Identity;
using SpendWise.Domain.Identity;
using SpendWise.Domain.Notifications;

namespace SpendWise.Application.Features.Authentication;

// Quản lý token reset PIN ngắn hạn; database chỉ lưu hash của token.
public sealed class SecurityPinResetSupport(
    ISpendWiseDbContext db,
    ILoginChallengeTokenService tokens,
    IClock clock)
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);

    public async Task<(string RawToken, SecurityPinResetToken Token)> CreateAsync(
        User user,
        SecurityPinResetPurpose purpose,
        Guid? createdByAdminId,
        CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var oldHashes = await db.SecurityPinResetTokens.AsNoTracking()
            .Where(x => x.UserId == user.Id && x.UsedAt == null && x.RevokedAt == null)
            .Select(x => x.TokenHash).ToListAsync(cancellationToken);
        var oldKeys = oldHashes.Select(hash => $"email:security-pin-reset:{hash}").ToArray();
        if (oldKeys.Length > 0)
            await db.EmailOutboxMessages.Where(x => oldKeys.Contains(x.DeduplicationKey) && x.Status == EmailDeliveryStatus.Pending)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, EmailDeliveryStatus.Failed)
                    .SetProperty(x => x.LastError, "content_superseded").SetProperty(x => x.ContentExpiresAt, now)
                    .SetProperty(x => x.HtmlBody, string.Empty).SetProperty(x => x.TextBody, string.Empty), cancellationToken);
        // Token mới thu hồi mọi token PIN đang hoạt động của cùng tài khoản.
        await db.SecurityPinResetTokens
            .Where(item => item.UserId == user.Id
                && item.UsedAt == null
                && item.RevokedAt == null
                && item.ExpiresAt > now)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(item => item.RevokedAt, now),
                cancellationToken);

        var rawToken = tokens.CreateToken();
        var resetToken = new SecurityPinResetToken
        {
            UserId = user.Id,
            TokenHash = tokens.HashToken(rawToken),
            Purpose = purpose,
            ExpiresAt = now.Add(Lifetime),
            CreatedByAdminId = createdByAdminId
        };
        db.SecurityPinResetTokens.Add(resetToken);
        await db.SaveChangesAsync(cancellationToken);
        return (rawToken, resetToken);
    }

    public async Task<SecurityPinResetToken> GetValidAsync(
        string rawToken,
        CancellationToken cancellationToken)
    {
        var token = InputBoundary.RequiredText(rawToken, 200, "Security PIN reset token");
        var tokenHash = tokens.HashToken(token);
        var now = clock.UtcNow;
        return await db.SecurityPinResetTokens
            .AsNoTracking()
            .Include(item => item.User)
            .SingleOrDefaultAsync(item => item.TokenHash == tokenHash
                && item.UsedAt == null
                && item.RevokedAt == null
                && item.ExpiresAt > now
                && item.User.Status == UserStatus.Active
                && (item.Purpose != SecurityPinResetPurpose.SelfService || item.User.EmailVerifiedAt != null),
                cancellationToken)
            ?? throw Failure();
    }

    public Task<int> ConsumeAsync(Guid tokenId, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        return db.SecurityPinResetTokens
            .Where(item => item.Id == tokenId
                && item.UsedAt == null
                && item.RevokedAt == null
                && item.ExpiresAt > now)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(item => item.UsedAt, now),
                cancellationToken);
    }

    public static AppException Failure() => new(
        "pin_reset_token_invalid",
        "Security PIN reset token is invalid or expired.",
        401);
}
