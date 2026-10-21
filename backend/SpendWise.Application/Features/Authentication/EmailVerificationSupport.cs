using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Identity;
using SpendWise.Domain.Identity;
using SpendWise.Domain.Notifications;

namespace SpendWise.Application.Features.Authentication;

// Tạo OTP đăng ký, áp dụng cooldown/rate-limit và xếp email vào outbox bền vững.
public sealed class EmailVerificationSupport(
    ISpendWiseDbContext db,
    IEmailVerificationOtpService otpService,
    IEmailVerificationPolicy policy,
    IEmailTemplateRenderer renderer,
    IEmailDeliveryQueue emailQueue,
    IClock clock)
{
    public const string RegistrationPurpose = "REGISTER";
    public const string PasswordResetPurpose = "RESET_PASSWORD";
    public const string ChangeEmailPurpose = "CHANGE_EMAIL";

    public Task<EmailVerificationPendingResponse> IssueRegistrationOtpAsync(
        User user,
        string? ipAddress,
        CancellationToken cancellationToken) =>
        db.ExecuteInTransactionAsync(
            token => IssueRegistrationOtpCoreAsync(user, ipAddress, token),
            cancellationToken);

    private async Task<EmailVerificationPendingResponse> IssueRegistrationOtpCoreAsync(
        User user,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var ipHash = string.IsNullOrWhiteSpace(ipAddress) ? null : otpService.HashRateLimitKey(ipAddress);
        if (!await db.TryAcquireEmailVerificationLockAsync(user.Id, ipHash, cancellationToken))
            throw RateLimited();
        // Resend đã đọc User trước lock; verify có thể vừa commit trong khoảng đó.
        var currentUser = await db.Users.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == user.Id, cancellationToken);
        if (currentUser?.EmailVerifiedAt is not null)
            return new EmailVerificationPendingResponse("EMAIL_VERIFICATION_REQUEST_ACCEPTED",
                clock.UtcNow.Add(policy.OtpLifetime), (int)policy.ResendCooldown.TotalSeconds);

        var now = clock.UtcNow;
        var hourStart = now.AddHours(-1);
        var recentForAccount = await db.EmailVerificationTokens
            .Where(item => item.UserId == user.Id
                && item.Purpose == EmailVerificationPurpose.Registration
                && item.CreatedAt >= hourStart)
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => new { item.CreatedAt })
            .ToListAsync(cancellationToken);

        if (recentForAccount.Count >= policy.MaximumSendsPerHourPerAccount)
            throw RateLimited();
        if (recentForAccount.FirstOrDefault() is { } latest
            && latest.CreatedAt.Add(policy.ResendCooldown) > now)
        {
            throw new AppException(
                "email_verification_cooldown",
                "Please wait before requesting another verification email.",
                429);
        }

        if (ipHash is not null)
        {
            var recentForIp = await db.EmailVerificationTokens.CountAsync(
                item => item.RequestIpHash == ipHash && item.CreatedAt >= hourStart,
                cancellationToken);
            if (recentForIp >= policy.MaximumSendsPerHourPerIp)
                throw RateLimited();
        }

        // OTP mới thu hồi mọi OTP đăng ký còn hiệu lực của cùng tài khoản.
        await db.EmailVerificationTokens
            .Where(item => item.UserId == user.Id
                && item.Purpose == EmailVerificationPurpose.Registration
                && item.UsedAt == null
                && item.RevokedAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(item => item.RevokedAt, now),
                cancellationToken);

        var otp = otpService.CreateOtp();
        var token = new EmailVerificationToken
        {
            UserId = user.Id,
            Purpose = EmailVerificationPurpose.Registration,
            CodeHash = otpService.HashOtp(user.Id, RegistrationPurpose, otp),
            ExpiresAt = now.Add(policy.OtpLifetime),
            RequestIpHash = ipHash
        };
        db.EmailVerificationTokens.Add(token);

        var rendered = renderer.Render("registration-verification", new Dictionary<string, string?>
        {
            ["Email"] = user.Email,
            ["Otp"] = otp,
            ["Minutes"] = ((int)policy.OtpLifetime.TotalMinutes).ToString()
        });
        await emailQueue.EnqueueAsync(
            new EmailMessage(
                user.Email,
                "[SpendWise] Mã xác thực đăng ký",
                rendered.HtmlBody,
                rendered.TextBody,
                token.ExpiresAt),
            $"email:registration-verification:{token.Id:N}",
            cancellationToken);

        return new EmailVerificationPendingResponse(
            "EMAIL_VERIFICATION_REQUIRED",
            token.ExpiresAt,
            (int)policy.ResendCooldown.TotalSeconds);
    }

    public async Task QueueWelcomeAsync(User user, CancellationToken cancellationToken)
    {
        var rendered = renderer.Render("registration-welcome", new Dictionary<string, string?>
        {
            ["UserName"] = user.FullName,
            ["ActionUrl"] = policy.WebsiteUrl,
            ["ActionText"] = "Truy cập SpendWise"
        });
        await emailQueue.EnqueueAsync(
            new EmailMessage(
                user.Email,
                "[SpendWise] Chào mừng bạn đến với SpendWise",
                rendered.HtmlBody,
                rendered.TextBody),
            $"email:registration-welcome:{user.Id:N}",
            cancellationToken);
    }

    public Task IssuePasswordResetOtpAsync(
        User user,
        string? ipAddress,
        CancellationToken cancellationToken) =>
        db.ExecuteInTransactionAsync(async token =>
        {
            await IssuePasswordResetOtpCoreAsync(user, ipAddress, token);
            return true;
        }, cancellationToken);

    private async Task IssuePasswordResetOtpCoreAsync(
        User user,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var ipHash = string.IsNullOrWhiteSpace(ipAddress) ? null : otpService.HashRateLimitKey(ipAddress);
        if (!await db.TryAcquireEmailVerificationLockAsync(user.Id, ipHash, cancellationToken))
            throw PasswordResetRateLimited();
        var eligible = await db.Users.AsNoTracking().AnyAsync(item => item.Id == user.Id
            && item.Status == UserStatus.Active && item.EmailVerifiedAt != null, cancellationToken);
        if (!eligible)
            return;

        var now = clock.UtcNow;
        var hourStart = now.AddHours(-1);
        var recentForAccount = await db.EmailVerificationTokens
            .Where(item => item.UserId == user.Id
                && item.Purpose == EmailVerificationPurpose.ResetPassword
                && item.CreatedAt >= hourStart)
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => new { item.CreatedAt })
            .ToListAsync(cancellationToken);

        if (recentForAccount.Count >= policy.MaximumSendsPerHourPerAccount)
            throw PasswordResetRateLimited();
        if (recentForAccount.FirstOrDefault() is { } latest
            && latest.CreatedAt.Add(policy.ResendCooldown) > now)
        {
            throw new AppException(
                "password_reset_cooldown",
                "Please wait before requesting another password reset email.",
                429);
        }

        if (ipHash is not null)
        {
            var recentForIp = await db.EmailVerificationTokens.CountAsync(
                item => item.RequestIpHash == ipHash && item.CreatedAt >= hourStart,
                cancellationToken);
            if (recentForIp >= policy.MaximumSendsPerHourPerIp)
                throw PasswordResetRateLimited();
        }

        // OTP reset mới thu hồi mọi OTP reset cũ còn hiệu lực của cùng tài khoản.
        var supersededTokenIds = await db.EmailVerificationTokens
            .Where(item => item.UserId == user.Id
                && item.Purpose == EmailVerificationPurpose.ResetPassword
                && item.UsedAt == null
                && item.RevokedAt == null)
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);
        await db.EmailVerificationTokens
            .Where(item => supersededTokenIds.Contains(item.Id))
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(item => item.RevokedAt, now),
                cancellationToken);

        // Xóa ciphertext của email OTP cũ chưa gửi để mã hết hiệu lực không tồn tại trong queue.
        if (supersededTokenIds.Count > 0)
        {
            var supersededKeys = supersededTokenIds
                .Select(id => $"email:password-reset-otp:{id:N}")
                .ToArray();
            await db.EmailOutboxMessages
                .Where(item => supersededKeys.Contains(item.DeduplicationKey)
                    && item.Status == EmailDeliveryStatus.Pending)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(item => item.Status, EmailDeliveryStatus.Failed)
                        .SetProperty(item => item.LastError, "content_superseded")
                        .SetProperty(item => item.ContentExpiresAt, now)
                        .SetProperty(item => item.HtmlBody, string.Empty)
                        .SetProperty(item => item.TextBody, string.Empty),
                    cancellationToken);
        }

        var otp = otpService.CreateOtp();
        var token = new EmailVerificationToken
        {
            UserId = user.Id,
            Purpose = EmailVerificationPurpose.ResetPassword,
            CodeHash = otpService.HashOtp(user.Id, PasswordResetPurpose, otp),
            ExpiresAt = now.Add(policy.OtpLifetime),
            RequestIpHash = ipHash
        };
        db.EmailVerificationTokens.Add(token);

        var rendered = renderer.Render("password-reset-verification", new Dictionary<string, string?>
        {
            ["Email"] = user.Email,
            ["Otp"] = otp,
            ["Minutes"] = ((int)policy.OtpLifetime.TotalMinutes).ToString()
        });
        await emailQueue.EnqueueAsync(
            new EmailMessage(
                user.Email,
                "[SpendWise] Mã OTP đặt lại mật khẩu",
                rendered.HtmlBody,
                rendered.TextBody,
                token.ExpiresAt),
            $"email:password-reset-otp:{token.Id:N}",
            cancellationToken);
    }

    public async Task QueuePasswordResetSuccessAsync(
        User user,
        Guid resetOtpTokenId,
        DateTime changedAt,
        CancellationToken cancellationToken)
    {
        var rendered = renderer.Render("password-reset-success", new Dictionary<string, string?>
        {
            ["UserName"] = user.FullName,
            ["ChangedAt"] = changedAt.ToString("O"),
            ["ActionUrl"] = policy.WebsiteUrl,
            ["ActionText"] = "Truy cập SpendWise"
        });
        await emailQueue.EnqueueAsync(
            new EmailMessage(
                user.Email,
                "[SpendWise] Mật khẩu đã được đặt lại",
                rendered.HtmlBody,
                rendered.TextBody),
            $"email:password-reset-success:{resetOtpTokenId:N}",
            cancellationToken);
    }

    public async Task<EmailVerificationPendingResponse> IssueChangeEmailOtpAsync(
        User user,
        string newEmail,
        string? ipAddress,
        CancellationToken cancellationToken) =>
        await db.ExecuteInTransactionAsync(
            token => IssueChangeEmailOtpCoreAsync(user, newEmail, ipAddress, token),
            cancellationToken);

    private async Task<EmailVerificationPendingResponse> IssueChangeEmailOtpCoreAsync(
        User user,
        string newEmail,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var ipHash = string.IsNullOrWhiteSpace(ipAddress) ? null : otpService.HashRateLimitKey(ipAddress);
        if (!await db.TryAcquireEmailVerificationLockAsync(user.Id, ipHash, cancellationToken))
            throw RateLimited();

        var now = clock.UtcNow;
        var hourStart = now.AddHours(-1);
        var recentForAccount = await db.EmailVerificationTokens
            .Where(item => item.UserId == user.Id
                && item.Purpose == EmailVerificationPurpose.ChangeEmail
                && item.CreatedAt >= hourStart)
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => new { item.CreatedAt })
            .ToListAsync(cancellationToken);

        if (recentForAccount.Count >= policy.MaximumSendsPerHourPerAccount)
            throw RateLimited();
        if (recentForAccount.FirstOrDefault() is { } latest
            && latest.CreatedAt.Add(policy.ResendCooldown) > now)
        {
            throw new AppException(
                "email_verification_cooldown",
                "Please wait before requesting another verification email.",
                429);
        }

        if (ipHash is not null)
        {
            var recentForIp = await db.EmailVerificationTokens.CountAsync(
                item => item.RequestIpHash == ipHash && item.CreatedAt >= hourStart,
                cancellationToken);
            if (recentForIp >= policy.MaximumSendsPerHourPerIp)
                throw RateLimited();
        }

        // Revoke active change email tokens for this user
        await db.EmailVerificationTokens
            .Where(item => item.UserId == user.Id
                && item.Purpose == EmailVerificationPurpose.ChangeEmail
                && item.UsedAt == null
                && item.RevokedAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(item => item.RevokedAt, now),
                cancellationToken);

        // Atomically update user's PendingNewEmail in the same transaction
        await db.Users
            .Where(u => u.Id == user.Id)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(u => u.PendingNewEmail, newEmail),
                cancellationToken);
        user.PendingNewEmail = newEmail;

        var otp = otpService.CreateOtp();
        var token = new EmailVerificationToken
        {
            UserId = user.Id,
            CodeHash = otpService.HashOtp(user.Id, ChangeEmailPurpose, otp),
            Purpose = EmailVerificationPurpose.ChangeEmail,
            ExpiresAt = now.Add(policy.OtpLifetime),
            RequestIpHash = ipHash
        };
        db.EmailVerificationTokens.Add(token);
        await db.SaveChangesAsync(cancellationToken);

        var rendered = renderer.Render("email-change-verification", new Dictionary<string, string?>
        {
            ["Email"] = newEmail,
            ["Otp"] = otp,
            ["Minutes"] = ((int)policy.OtpLifetime.TotalMinutes).ToString()
        });
        await emailQueue.EnqueueAsync(
            new EmailMessage(
                newEmail,
                "[SpendWise] Mã xác thực thay đổi email",
                rendered.HtmlBody,
                rendered.TextBody,
                token.ExpiresAt),
            $"email:change-email-otp:{token.Id:N}",
            cancellationToken);

        return new EmailVerificationPendingResponse(
            "EMAIL_CHANGE_OTP_SENT",
            token.ExpiresAt,
            (int)policy.ResendCooldown.TotalSeconds);
    }

    public async Task QueueEmailChangeNoticeAsync(
        string oldEmail,
        string newEmail,
        CancellationToken cancellationToken)
    {
        var rendered = renderer.Render("email-change-notice", new Dictionary<string, string?>
        {
            ["OldEmail"] = oldEmail,
            ["NewEmail"] = newEmail
        });
        await emailQueue.EnqueueAsync(
            new EmailMessage(
                oldEmail,
                "[SpendWise] Cảnh báo bảo mật: Email tài khoản đã thay đổi",
                rendered.HtmlBody,
                rendered.TextBody),
            $"email:change-notice:{Guid.NewGuid():N}",
            cancellationToken);
    }

    public static AppException VerificationFailure() => new(
        "email_verification_failed",
        "The email verification code is invalid or expired.",
        400);

    private static AppException RateLimited() => new(
        "email_verification_rate_limited",
        "Too many verification email requests. Please try again later.",
        429);

    private static AppException PasswordResetRateLimited() => new(
        "password_reset_rate_limited",
        "Too many password reset email requests. Please try again later.",
        429);
}
