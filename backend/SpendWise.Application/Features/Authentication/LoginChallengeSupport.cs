using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Identity;
using SpendWise.Domain.Identity;

namespace SpendWise.Application.Features.Authentication;

// Quản lý challenge ngắn hạn; challenge không phải JWT và không cấp quyền truy cập API.
public sealed class LoginChallengeSupport(
    ISpendWiseDbContext db,
    ILoginChallengeTokenService tokens,
    IClock clock)
{
    public const int MaximumFailedAttempts = 5;
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    public async Task<LoginChallengeResponse> CreateAsync(
        User user,
        string deviceName,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        // Challenge mới làm các challenge chưa dùng trước đó mất hiệu lực.
        await db.LoginChallenges
            .Where(item => item.UserId == user.Id
                && item.UsedAt == null
                && item.ExpiresAt > now)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(item => item.UsedAt, now),
                cancellationToken);

        var rawToken = tokens.CreateToken();
        var challenge = new LoginChallenge
        {
            UserId = user.Id,
            TokenHash = tokens.HashToken(rawToken),
            Purpose = string.IsNullOrWhiteSpace(user.SecurityPinHash)
                ? LoginChallengePurpose.PinSetup
                : LoginChallengePurpose.PinVerification,
            ExpiresAt = now.Add(Lifetime),
            DeviceName = Safe(deviceName, 200) ?? "Browser",
            IpAddress = Safe(ipAddress, 64),
            UserAgent = Safe(userAgent, 512)
        };
        db.LoginChallenges.Add(challenge);
        await db.SaveChangesAsync(cancellationToken);

        return new LoginChallengeResponse(
            rawToken,
            challenge.Purpose == LoginChallengePurpose.PinSetup
                ? "PIN_SETUP_REQUIRED"
                : "PIN_REQUIRED",
            challenge.ExpiresAt);
    }

    public async Task<LoginChallenge> GetValidAsync(
        string rawToken,
        LoginChallengePurpose purpose,
        CancellationToken cancellationToken)
    {
        var token = InputBoundary.RequiredText(rawToken, 200, "Login challenge");
        var tokenHash = tokens.HashToken(token);
        var now = clock.UtcNow;
        return await db.LoginChallenges
            .AsNoTracking()
            .Include(item => item.User)
            .SingleOrDefaultAsync(
                item => item.TokenHash == tokenHash
                    && item.Purpose == purpose
                    && item.UsedAt == null
                    && item.ExpiresAt > now
                    && item.FailedAttempts < MaximumFailedAttempts,
                cancellationToken)
            ?? throw Failure();
    }

    public Task<int> ConsumeAsync(
        Guid challengeId,
        CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        return db.LoginChallenges
            .Where(item => item.Id == challengeId
                && item.UsedAt == null
                && item.ExpiresAt > now
                && item.FailedAttempts < MaximumFailedAttempts)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(item => item.UsedAt, now),
                cancellationToken);
    }

    public async Task RecordFailedAttemptAsync(
        Guid challengeId,
        CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        await db.LoginChallenges
            .Where(item => item.Id == challengeId
                && item.UsedAt == null
                && item.ExpiresAt > now
                && item.FailedAttempts < MaximumFailedAttempts)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(item => item.FailedAttempts, item => item.FailedAttempts + 1)
                    .SetProperty(
                        item => item.UsedAt,
                        item => item.FailedAttempts >= MaximumFailedAttempts - 1
                            ? now
                            : item.UsedAt),
                cancellationToken);
    }

    public static AppException Failure() => new(
        "login_challenge_failed",
        "Login challenge or PIN verification failed.",
        401);

    private static string? Safe(string? value, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim();
        return normalized.Length <= maximumLength
            ? normalized
            : normalized[..maximumLength];
    }
}
