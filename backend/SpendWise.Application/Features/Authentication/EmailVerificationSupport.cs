using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common;
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
    IClock clock,
    ILoginChallengeTokenService? challengeTokens = null)
{
    public const string RegistrationPurpose = "REGISTER";
    public const string PasswordResetPurpose = "RESET_PASSWORD";
    public const string ChangeEmailPurpose = "CHANGE_EMAIL";
    public const string ChangeEmailCurrentPurpose = "CHANGE_EMAIL_CURRENT";
    public const string ChangeEmailNewPurpose = "CHANGE_EMAIL_NEW";

    private readonly ILoginChallengeTokenService tokens = challengeTokens ?? new DefaultChallengeTokenService();

    private sealed class DefaultChallengeTokenService : ILoginChallengeTokenService
    {
        public string CreateToken() => Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        public string HashToken(string token) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));
    }

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

    public Task<EmailVerificationPendingResponse> IssueCurrentEmailChangeOtpAsync(
        User user,
        string? ipAddress,
        CancellationToken cancellationToken) =>
        db.ExecuteInTransactionAsync(
            token => IssueCurrentEmailChangeOtpCoreAsync(user, ipAddress, token),
            cancellationToken);

    private async Task<EmailVerificationPendingResponse> IssueCurrentEmailChangeOtpCoreAsync(
        User user,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var ipHash = string.IsNullOrWhiteSpace(ipAddress) ? null : otpService.HashRateLimitKey(ipAddress);
        if (!await db.TryAcquireEmailVerificationLockAsync(user.Id, ipHash, cancellationToken))
            throw RateLimited();

        if (user.Status != UserStatus.Active)
            throw new AppException("account_restricted", "Tài khoản hiện không ở trạng thái hoạt động.", 403);

        var now = clock.UtcNow;
        var hourStart = now.AddHours(-1);
        var recentForAccount = await db.EmailVerificationTokens
            .Where(item => item.UserId == user.Id
                && item.Purpose == EmailVerificationPurpose.ChangeEmailCurrent
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

        // Revoke active current email OTP tokens for this user
        await db.EmailVerificationTokens
            .Where(item => item.UserId == user.Id
                && item.Purpose == EmailVerificationPurpose.ChangeEmailCurrent
                && item.UsedAt == null
                && item.RevokedAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(item => item.RevokedAt, now),
                cancellationToken);

        var otp = otpService.CreateOtp();
        var token = new EmailVerificationToken
        {
            UserId = user.Id,
            CodeHash = otpService.HashOtp(user.Id, ChangeEmailCurrentPurpose, otp),
            Purpose = EmailVerificationPurpose.ChangeEmailCurrent,
            ExpiresAt = now.Add(policy.OtpLifetime),
            RequestIpHash = ipHash
        };
        db.EmailVerificationTokens.Add(token);
        await db.SaveChangesAsync(cancellationToken);

        var rendered = renderer.Render("email-change-current-verification", new Dictionary<string, string?>
        {
            ["Email"] = user.Email,
            ["Otp"] = otp,
            ["Minutes"] = ((int)policy.OtpLifetime.TotalMinutes).ToString()
        });
        await emailQueue.EnqueueAsync(
            new EmailMessage(
                user.Email,
                "[SpendWise] Mã xác thực đổi email (Xác nhận email hiện tại)",
                rendered.HtmlBody,
                rendered.TextBody,
                token.ExpiresAt),
            $"email:change-email-current-otp:{token.Id:N}",
            cancellationToken);

        return new EmailVerificationPendingResponse(
            "CURRENT_EMAIL_OTP_SENT",
            token.ExpiresAt,
            (int)policy.ResendCooldown.TotalSeconds);
    }

    public Task<(string Ticket, DateTime ExpiresAt)> VerifyCurrentEmailChangeOtpAsync(
        User user,
        Guid? sessionId,
        string rawOtp,
        string? ipAddress,
        CancellationToken cancellationToken) =>
        db.ExecuteInTransactionAsync(
            token => VerifyCurrentEmailChangeOtpCoreAsync(user, sessionId, rawOtp, ipAddress, token),
            cancellationToken);

    private async Task<(string Ticket, DateTime ExpiresAt)> VerifyCurrentEmailChangeOtpCoreAsync(
        User user,
        Guid? sessionId,
        string rawOtp,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var otp = rawOtp?.Trim() ?? string.Empty;
        if (otp.Length != 8 || otp.Any(c => !char.IsAsciiDigit(c)))
            throw VerificationFailure();

        var ipHash = string.IsNullOrWhiteSpace(ipAddress) ? null : otpService.HashRateLimitKey(ipAddress);
        if (!await db.TryAcquireEmailVerificationLockAsync(user.Id, ipHash, cancellationToken))
            throw VerificationFailure();

        if (user.Status != UserStatus.Active)
            throw new AppException("account_restricted", "Tài khoản hiện không ở trạng thái hoạt động.", 403);

        var now = clock.UtcNow;
        var token = await db.EmailVerificationTokens
            .Where(item => item.UserId == user.Id
                && item.Purpose == EmailVerificationPurpose.ChangeEmailCurrent
                && item.UsedAt == null
                && item.RevokedAt == null
                && item.ExpiresAt > now
                && item.FailedAttempts < policy.MaximumFailedAttempts)
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (token is null)
            throw VerificationFailure();

        if (!otpService.VerifyOtp(user.Id, ChangeEmailCurrentPurpose, otp, token.CodeHash))
        {
            await db.EmailVerificationTokens
                .Where(item => item.Id == token.Id)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(item => item.FailedAttempts, item => item.FailedAttempts + 1)
                        .SetProperty(
                            item => item.RevokedAt,
                            item => item.FailedAttempts >= policy.MaximumFailedAttempts - 1
                                ? now
                                : item.RevokedAt),
                    cancellationToken);

            await db.CommitTransactionAsync(cancellationToken);

            throw VerificationFailure();
        }

        // Mark current email token as used
        await db.EmailVerificationTokens
            .Where(item => item.Id == token.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.UsedAt, now), cancellationToken);

        // Revoke previous unconsumed change email tickets for this user
        await db.EmailVerificationTokens
            .Where(item => item.UserId == user.Id
                && item.Purpose == EmailVerificationPurpose.ChangeEmailTicket
                && item.UsedAt == null
                && item.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.RevokedAt, now), cancellationToken);

        // Issue new ticket bound to this user, session, and 15-minute lifetime
        var rawTicket = tokens.CreateToken();
        var sessionPart = sessionId?.ToString("N") ?? string.Empty;
        var ticketHash = tokens.HashToken($"{rawTicket}:{sessionPart}:{user.Id:N}");
        var ticketExpiresAt = now.Add(TimeSpan.FromMinutes(15));

        var ticketRecord = new EmailVerificationToken
        {
            UserId = user.Id,
            Purpose = EmailVerificationPurpose.ChangeEmailTicket,
            CodeHash = ticketHash,
            ExpiresAt = ticketExpiresAt,
            RequestIpHash = ipHash
        };
        db.EmailVerificationTokens.Add(ticketRecord);
        await db.SaveChangesAsync(cancellationToken);

        return (rawTicket, ticketExpiresAt);
    }

    public Task<EmailVerificationPendingResponse> IssueNewEmailChangeOtpAsync(
        User user,
        Guid? sessionId,
        string rawTicket,
        string newEmail,
        string? ipAddress,
        CancellationToken cancellationToken) =>
        db.ExecuteInTransactionAsync(
            token => IssueNewEmailChangeOtpCoreAsync(user, sessionId, rawTicket, newEmail, ipAddress, token),
            cancellationToken);

    private async Task<EmailVerificationPendingResponse> IssueNewEmailChangeOtpCoreAsync(
        User user,
        Guid? sessionId,
        string rawTicket,
        string newEmail,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawTicket))
            throw new AppException("email_change_ticket_invalid", "Email change verification session is invalid or expired.", 400);

        var normalizedNewEmail = InputBoundary.NormalizeEmail(newEmail);
        if (string.IsNullOrWhiteSpace(normalizedNewEmail))
            throw new AppException("invalid_email", "Email is required.", 400);

        if (string.Equals(user.NormalizedEmail, normalizedNewEmail, StringComparison.OrdinalIgnoreCase))
            throw new AppException("email_unchanged", "New email cannot be the same as the current email.", 400);

        var ipHash = string.IsNullOrWhiteSpace(ipAddress) ? null : otpService.HashRateLimitKey(ipAddress);
        if (!await db.TryAcquireEmailVerificationLockAsync(user.Id, ipHash, cancellationToken))
            throw RateLimited();

        if (user.Status != UserStatus.Active)
            throw new AppException("account_restricted", "Tài khoản hiện không ở trạng thái hoạt động.", 403);

        var now = clock.UtcNow;
        var sessionPart = sessionId?.ToString("N") ?? string.Empty;
        var ticketHash = tokens.HashToken($"{rawTicket}:{sessionPart}:{user.Id:N}");

        var ticketRecord = await db.EmailVerificationTokens.AsNoTracking()
            .Where(item => item.UserId == user.Id
                && item.Purpose == EmailVerificationPurpose.ChangeEmailTicket
                && item.CodeHash == ticketHash
                && item.UsedAt == null
                && item.RevokedAt == null
                && item.ExpiresAt > now)
            .FirstOrDefaultAsync(cancellationToken);

        if (ticketRecord is null)
            throw new AppException("email_change_ticket_invalid", "Email change verification session is invalid or expired.", 400);

        // Check duplicate email
        var isDuplicate = await db.Users.AsNoTracking().AnyAsync(
            u => u.NormalizedEmail == normalizedNewEmail && u.Id != user.Id,
            cancellationToken);

        if (isDuplicate)
            throw new AppException("duplicate_email", "Email address is already in use by another account.", 409);

        // Check cooldown on new email OTP sends
        var hourStart = now.AddHours(-1);
        var recentForAccount = await db.EmailVerificationTokens
            .Where(item => item.UserId == user.Id
                && item.Purpose == EmailVerificationPurpose.ChangeEmailNew
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

        // Revoke previous active ChangeEmailNew tokens for this user
        await db.EmailVerificationTokens
            .Where(item => item.UserId == user.Id
                && item.Purpose == EmailVerificationPurpose.ChangeEmailNew
                && item.UsedAt == null
                && item.RevokedAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(item => item.RevokedAt, now),
                cancellationToken);

        // Atomically update user's PendingNewEmail
        await db.Users
            .Where(u => u.Id == user.Id)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(u => u.PendingNewEmail, newEmail.Trim()),
                cancellationToken);
        user.PendingNewEmail = newEmail.Trim();

        var otp = otpService.CreateOtp();
        var token = new EmailVerificationToken
        {
            UserId = user.Id,
            CodeHash = otpService.HashOtp(user.Id, ChangeEmailNewPurpose, otp),
            Purpose = EmailVerificationPurpose.ChangeEmailNew,
            ExpiresAt = now.Add(policy.OtpLifetime),
            RequestIpHash = ipHash
        };
        db.EmailVerificationTokens.Add(token);
        await db.SaveChangesAsync(cancellationToken);

        var rendered = renderer.Render("email-change-verification", new Dictionary<string, string?>
        {
            ["Email"] = newEmail.Trim(),
            ["Otp"] = otp,
            ["Minutes"] = ((int)policy.OtpLifetime.TotalMinutes).ToString()
        });
        await emailQueue.EnqueueAsync(
            new EmailMessage(
                newEmail.Trim(),
                "[SpendWise] Mã xác thực địa chỉ email mới",
                rendered.HtmlBody,
                rendered.TextBody,
                token.ExpiresAt),
            $"email:change-email-new-otp:{token.Id:N}",
            cancellationToken);

        return new EmailVerificationPendingResponse(
            "NEW_EMAIL_OTP_SENT",
            token.ExpiresAt,
            (int)policy.ResendCooldown.TotalSeconds);
    }

    public Task<UserProfile> ConfirmNewEmailChangeOtpAsync(
        User user,
        Guid? sessionId,
        string rawTicket,
        string rawOtp,
        string? ipAddress,
        CancellationToken cancellationToken) =>
        db.ExecuteInTransactionAsync(
            token => ConfirmNewEmailChangeOtpCoreAsync(user, sessionId, rawTicket, rawOtp, ipAddress, token),
            cancellationToken);

    private async Task<UserProfile> ConfirmNewEmailChangeOtpCoreAsync(
        User user,
        Guid? sessionId,
        string rawTicket,
        string rawOtp,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawTicket))
            throw new AppException("email_change_ticket_invalid", "Email change verification session is invalid or expired.", 400);

        var otp = rawOtp?.Trim() ?? string.Empty;
        if (otp.Length != 8 || otp.Any(c => !char.IsAsciiDigit(c)))
            throw VerificationFailure();

        var ipHash = string.IsNullOrWhiteSpace(ipAddress) ? null : otpService.HashRateLimitKey(ipAddress);
        if (!await db.TryAcquireEmailVerificationLockAsync(user.Id, ipHash, cancellationToken))
            throw VerificationFailure();

        if (user.Status != UserStatus.Active)
            throw new AppException("account_restricted", "Tài khoản hiện không ở trạng thái hoạt động.", 403);

        // Check session validity
        if (sessionId.HasValue)
        {
            var isSessionActive = await db.UserSessions.AsNoTracking().AnyAsync(
                s => s.Id == sessionId.Value && s.UserId == user.Id && s.RevokedAt == null,
                cancellationToken);
            if (!isSessionActive)
                throw new AppException("session_revoked", "Current session has been revoked.", 401);
        }

        var now = clock.UtcNow;
        var sessionPart = sessionId?.ToString("N") ?? string.Empty;
        var ticketHash = tokens.HashToken($"{rawTicket}:{sessionPart}:{user.Id:N}");

        var ticketRecord = await db.EmailVerificationTokens
            .Where(item => item.UserId == user.Id
                && item.Purpose == EmailVerificationPurpose.ChangeEmailTicket
                && item.CodeHash == ticketHash
                && item.UsedAt == null
                && item.RevokedAt == null
                && item.ExpiresAt > now)
            .FirstOrDefaultAsync(cancellationToken);

        if (ticketRecord is null)
            throw new AppException("email_change_ticket_invalid", "Email change verification session is invalid or expired.", 400);

        if (string.IsNullOrWhiteSpace(user.PendingNewEmail))
            throw new AppException("no_pending_email", "No pending email change request found.", 400);

        var pendingEmail = user.PendingNewEmail.Trim();
        var normalizedPendingEmail = InputBoundary.NormalizeEmail(pendingEmail);

        // Check duplicate email again to prevent race conditions
        var isDuplicate = await db.Users.AsNoTracking().AnyAsync(
            u => u.NormalizedEmail == normalizedPendingEmail && u.Id != user.Id,
            cancellationToken);

        if (isDuplicate)
            throw new AppException("duplicate_email", "Email address is already in use by another account.", 409);

        var token = await db.EmailVerificationTokens
            .Where(item => item.UserId == user.Id
                && item.Purpose == EmailVerificationPurpose.ChangeEmailNew
                && item.UsedAt == null
                && item.RevokedAt == null
                && item.ExpiresAt > now
                && item.FailedAttempts < policy.MaximumFailedAttempts)
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (token is null)
            throw VerificationFailure();

        if (!otpService.VerifyOtp(user.Id, ChangeEmailNewPurpose, otp, token.CodeHash))
        {
            await db.EmailVerificationTokens
                .Where(item => item.Id == token.Id)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(item => item.FailedAttempts, item => item.FailedAttempts + 1)
                        .SetProperty(
                            item => item.RevokedAt,
                            item => item.FailedAttempts >= policy.MaximumFailedAttempts - 1
                                ? now
                                : item.RevokedAt),
                    cancellationToken);

            if (token.FailedAttempts >= policy.MaximumFailedAttempts - 1)
            {
                // Revoke ticket as well on max failed attempts
                await db.EmailVerificationTokens
                    .Where(item => item.Id == ticketRecord.Id)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.RevokedAt, now), cancellationToken);
            }

            await db.CommitTransactionAsync(cancellationToken);

            throw VerificationFailure();
        }

        // Mark both tokens as used
        await db.EmailVerificationTokens
            .Where(item => item.Id == token.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.UsedAt, now), cancellationToken);

        await db.EmailVerificationTokens
            .Where(item => item.Id == ticketRecord.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.UsedAt, now), cancellationToken);

        // Revoke all remaining pending change email tokens for this user
        await db.EmailVerificationTokens
            .Where(item => item.UserId == user.Id
                && (item.Purpose == EmailVerificationPurpose.ChangeEmailCurrent
                    || item.Purpose == EmailVerificationPurpose.ChangeEmailTicket
                    || item.Purpose == EmailVerificationPurpose.ChangeEmailNew)
                && item.UsedAt == null
                && item.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.RevokedAt, now), cancellationToken);

        var oldEmail = user.Email;

        // Commit new email to User entity
        user.Email = pendingEmail;
        user.NormalizedEmail = normalizedPendingEmail;
        user.PendingNewEmail = null;
        user.EmailVerifiedAt = now;
        await db.SaveChangesAsync(cancellationToken);

        // Queue security notice to the old email
        await QueueEmailChangeNoticeAsync(oldEmail, pendingEmail, cancellationToken);

        // Revoke all other active sessions of this user
        if (sessionId.HasValue)
        {
            await db.RefreshTokens
                .Where(x => x.UserId == user.Id && x.UserSessionId != sessionId.Value && x.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, now), cancellationToken);
            await db.UserSessions
                .Where(x => x.UserId == user.Id && x.Id != sessionId.Value && x.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, now), cancellationToken);
        }

        return AuthenticationSupport.MapProfile(user);
    }

    public Task<bool> CancelEmailChangeAsync(
        User user,
        Guid? sessionId,
        string? rawTicket,
        CancellationToken cancellationToken) =>
        db.ExecuteInTransactionAsync(
            token => CancelEmailChangeCoreAsync(user, sessionId, rawTicket, token),
            cancellationToken);

    private async Task<bool> CancelEmailChangeCoreAsync(
        User user,
        Guid? sessionId,
        string? rawTicket,
        CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        await db.EmailVerificationTokens
            .Where(item => item.UserId == user.Id
                && (item.Purpose == EmailVerificationPurpose.ChangeEmailCurrent
                    || item.Purpose == EmailVerificationPurpose.ChangeEmailTicket
                    || item.Purpose == EmailVerificationPurpose.ChangeEmailNew)
                && item.UsedAt == null
                && item.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.RevokedAt, now), cancellationToken);

        user.PendingNewEmail = null;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public Task<EmailVerificationPendingResponse> IssueChangeEmailOtpAsync(
        User user,
        string newEmail,
        string? ipAddress,
        CancellationToken cancellationToken) =>
        IssueCurrentEmailChangeOtpAsync(user, ipAddress, cancellationToken);

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
