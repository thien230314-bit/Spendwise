using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Identity;
using SpendWise.Domain.Identity;

namespace SpendWise.Application.Features.Authentication;

public sealed record DeviceTrustIssue(
    bool Recognized,
    string RawToken,
    string TokenHash,
    DateTime ExpiresAt);

// Điều phối nhận diện thiết bị và email chỉ sau khi password + PIN đã hợp lệ.
public sealed class InteractiveLoginSupport(
    ISpendWiseDbContext db,
    IDeviceTrustTokenService deviceTokens,
    IDeviceTrustPolicy devicePolicy,
    IEmailTemplateRenderer renderer,
    IEmailDeliveryQueue emailQueue,
    IEmailVerificationPolicy emailPolicy,
    IClock clock)
{
    public static readonly TimeSpan WelcomeBackThreshold = TimeSpan.FromDays(14);

    public async Task<DeviceTrustIssue> PrepareDeviceAsync(
        Guid userId,
        string? presentedToken,
        CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var cutoff = now.Subtract(devicePolicy.Lifetime);

        // Hash hết hạn được xóa định kỳ ngay trong lần đăng nhập tương tác tiếp theo.
        await db.UserSessions
            .Where(session => session.UserId == userId
                && session.DeviceIdentifier != null
                && session.CreatedAt <= cutoff)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(session => session.DeviceIdentifier, (string?)null),
                cancellationToken);

        var recognized = false;
        if (deviceTokens.TryHashToken(presentedToken, out var presentedHash))
        {
            var recognizedSessionId = await db.UserSessions.AsNoTracking()
                .Where(session => session.UserId == userId
                    && session.DeviceIdentifier == presentedHash
                    && session.CreatedAt > cutoff)
                .OrderByDescending(session => session.CreatedAt)
                .Select(session => (Guid?)session.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (recognizedSessionId is Guid sessionId)
            {
                // Token cũ được xoay trong cùng transaction với session mới.
                recognized = await db.UserSessions
                    .Where(session => session.Id == sessionId
                        && session.UserId == userId
                        && session.DeviceIdentifier == presentedHash)
                    .ExecuteUpdateAsync(
                        setters => setters.SetProperty(session => session.DeviceIdentifier, (string?)null),
                        cancellationToken) == 1;
            }
        }

        var rawToken = deviceTokens.CreateToken();
        return new DeviceTrustIssue(
            recognized,
            rawToken,
            deviceTokens.HashToken(rawToken),
            now.Add(devicePolicy.Lifetime));
    }

    public async Task QueueLoginEmailAsync(
        User user,
        Guid sessionId,
        DateTime? previousLoginAt,
        DateTime loginAt,
        DeviceTrustIssue device,
        string deviceName,
        string? userAgent,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        // Tài khoản đăng nhập bình thường luôn đã verify; kiểm tra này tránh làm mất session dữ liệu legacy.
        if (user.EmailVerifiedAt is null || string.IsNullOrWhiteSpace(user.Email)) return;

        if (!device.Recognized)
        {
            var rendered = renderer.Render("new-device-login", new Dictionary<string, string?>
            {
                ["UserName"] = user.FullName,
                ["LoginTime"] = loginAt.ToString("dd/MM/yyyy HH:mm:ss 'UTC'"),
                ["DeviceName"] = deviceName,
                ["BrowserInfo"] = userAgent,
                ["IpAddress"] = ipAddress,
                ["ActionUrl"] = emailPolicy.WebsiteUrl
            });
            await emailQueue.EnqueueAsync(
                new EmailMessage(
                    user.Email,
                    "[SpendWise] Cảnh báo đăng nhập từ thiết bị mới",
                    rendered.HtmlBody,
                    rendered.TextBody),
                $"login-new-device:{sessionId:N}",
                cancellationToken);
            return;
        }

        TimeSpan? elapsed = previousLoginAt is null ? null : loginAt - previousLoginAt.Value;
        if (!user.EngagementEmailOptIn
            || elapsed is null
            || elapsed.Value < WelcomeBackThreshold)
        {
            return;
        }

        var welcome = renderer.Render("welcome-back", new Dictionary<string, string?>
        {
            ["UserName"] = user.FullName,
            ["ActionUrl"] = emailPolicy.WebsiteUrl
        });
        await emailQueue.EnqueueAsync(
            new EmailMessage(
                user.Email,
                "[SpendWise] Chào mừng bạn quay lại",
                welcome.HtmlBody,
                welcome.TextBody),
            $"login-welcome-back:{sessionId:N}",
            cancellationToken);
    }
}
