using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Identity;
using SpendWise.Domain.Identity;
using SpendWise.Domain.Notifications;

namespace SpendWise.Application.Features.Authentication.Commands.ResetPassword;

// Handler xác minh OTP RESET_PASSWORD, đổi mật khẩu và thu hồi phiên trong một transaction.
public sealed class ResetPasswordCommandHandler(
    ISpendWiseDbContext db,
    IEmailVerificationOtpService otpService,
    IEmailVerificationPolicy policy,
    IPasswordService passwords,
    AuthenticationSupport authentication,
    EmailVerificationSupport emailVerification,
    IClock clock) : IRequestHandler<ResetPasswordCommand>
{
    public async Task Handle(
        ResetPasswordCommand command,
        CancellationToken cancellationToken)
    {
        AuthenticationSupport.ValidatePassword(command.Request.NewPassword);
        var identifier = command.Request.Identifier?.Trim();
        var otp = command.Request.Otp?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(identifier)
            || identifier.Length > 256
            || otp.Length != 8
            || otp.Any(character => !char.IsAsciiDigit(character)))
        {
            throw ResetFailure();
        }

        var user = await FindUserAsync(identifier, cancellationToken);
        if (user is null)
            throw ResetFailure();

        var reset = await db.ExecuteInTransactionAsync(async transactionToken =>
        {
            if (!await db.TryAcquireEmailVerificationLockAsync(user.Id, null, transactionToken))
                return false;
            var currentUser = await db.Users.SingleOrDefaultAsync(
                item => item.Id == user.Id && item.Status == UserStatus.Active && item.EmailVerifiedAt != null,
                transactionToken);
            if (currentUser is null)
                return false;
            var now = clock.UtcNow;
            var resetToken = await db.EmailVerificationTokens.AsNoTracking()
                .Where(item => item.UserId == user.Id
                    && item.Purpose == EmailVerificationPurpose.ResetPassword)
                .OrderByDescending(item => item.CreatedAt)
                .FirstOrDefaultAsync(transactionToken);
            if (resetToken is null
                || resetToken.UsedAt is not null
                || resetToken.RevokedAt is not null
                || resetToken.ExpiresAt <= now
                || resetToken.FailedAttempts >= policy.MaximumFailedAttempts)
            {
                return false;
            }

            if (!otpService.VerifyOtp(
                    user.Id,
                    EmailVerificationSupport.PasswordResetPurpose,
                    otp,
                    resetToken.CodeHash))
            {
                // Mỗi OTP chỉ cho phép số lần thử giới hạn; lần cuối sẽ thu hồi mã.
                await db.EmailVerificationTokens
                    .Where(item => item.Id == resetToken.Id
                        && item.UsedAt == null
                        && item.RevokedAt == null
                        && item.ExpiresAt > now
                        && item.FailedAttempts < policy.MaximumFailedAttempts)
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(item => item.FailedAttempts, item => item.FailedAttempts + 1)
                            .SetProperty(
                                item => item.RevokedAt,
                                item => item.FailedAttempts >= policy.MaximumFailedAttempts - 1
                                    ? now
                                    : item.RevokedAt),
                        transactionToken);
                // Commit failed-attempt accounting before returning the neutral failure.
                return false;
            }

            // Conditional UPDATE bảo đảm một OTP chỉ được consume thành công một lần.
            var consumedRows = await db.EmailVerificationTokens
                .Where(item => item.Id == resetToken.Id
                    && item.UserId == user.Id
                    && item.Purpose == EmailVerificationPurpose.ResetPassword
                    && item.UsedAt == null
                    && item.RevokedAt == null
                    && item.ExpiresAt > now
                    && item.FailedAttempts < policy.MaximumFailedAttempts)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(item => item.UsedAt, now),
                    transactionToken);
            if (consumedRows != 1)
                throw ResetFailure();

            // OTP đã consume thì email E05 chưa gửi không còn giá trị; xóa ciphertext ngay.
            var otpEmailKey = $"email:password-reset-otp:{resetToken.Id:N}";
            await db.EmailOutboxMessages
                .Where(item => item.DeduplicationKey == otpEmailKey
                    && item.Status == EmailDeliveryStatus.Pending)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(item => item.Status, EmailDeliveryStatus.Failed)
                        .SetProperty(item => item.LastError, "content_consumed")
                        .SetProperty(item => item.ContentExpiresAt, now)
                        .SetProperty(item => item.HtmlBody, string.Empty)
                        .SetProperty(item => item.TextBody, string.Empty),
                    transactionToken);

            // Password hash, lockout và toàn bộ session cũ phải commit hoặc rollback cùng nhau.
            currentUser.PasswordHash = passwords.Hash(command.Request.NewPassword);
            currentUser.MustChangePassword = false;
            currentUser.PasswordFailedAttempts = 0;
            currentUser.PasswordLockoutUntil = null;
            await authentication.RevokeAllSessionsAsync(currentUser.Id, transactionToken);

            // E06 dùng ID OTP làm khóa dedupe nên request retry không xếp email thứ hai.
            await emailVerification.QueuePasswordResetSuccessAsync(
                currentUser,
                resetToken.Id,
                now,
                transactionToken);
            await db.SaveChangesAsync(transactionToken);
            return true;
        }, cancellationToken);
        if (!reset)
            throw ResetFailure();
    }

    private async Task<User?> FindUserAsync(string identifier, CancellationToken cancellationToken)
    {
        if (InputBoundary.IsValidEmail(identifier))
        {
            var normalizedEmail = identifier.ToUpperInvariant();
            return await db.Users.AsNoTracking().SingleOrDefaultAsync(
                item => item.NormalizedEmail == normalizedEmail
                    && item.Status == UserStatus.Active
                    && item.EmailVerifiedAt != null,
                cancellationToken);
        }

        if (InputBoundary.TryNormalizePhone(identifier, out var normalizedPhone))
        {
            return await db.Users.AsNoTracking().SingleOrDefaultAsync(
                item => item.Phone == normalizedPhone
                    && item.Status == UserStatus.Active
                    && item.EmailVerifiedAt != null,
                cancellationToken);
        }

        return null;
    }

    private static AppException ResetFailure() => new(
        "invalid_password_reset_token",
        "Password reset code is invalid, expired, or already used.",
        400);
}
