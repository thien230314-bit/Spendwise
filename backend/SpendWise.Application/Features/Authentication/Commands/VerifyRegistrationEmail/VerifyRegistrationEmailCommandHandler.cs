using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Identity;
using SpendWise.Domain.Identity;

namespace SpendWise.Application.Features.Authentication.Commands.VerifyRegistrationEmail;

// Xác minh OTP một lần, commit trạng thái email rồi tạo challenge PIN hiện có.
public sealed class VerifyRegistrationEmailCommandHandler(
    ISpendWiseDbContext db,
    IEmailVerificationOtpService otpService,
    IEmailVerificationPolicy policy,
    EmailVerificationSupport emailVerification,
    LoginChallengeSupport challenges,
    IClock clock) : IRequestHandler<VerifyRegistrationEmailCommand, LoginChallengeResponse>
{
    public async Task<LoginChallengeResponse> Handle(
        VerifyRegistrationEmailCommand command,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = InputBoundary.NormalizeEmail(command.Request.Email);
        var otp = command.Request.Otp?.Trim() ?? string.Empty;
        if (otp.Length != 8 || otp.Any(character => !char.IsAsciiDigit(character)))
            throw EmailVerificationSupport.VerificationFailure();

        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(
            item => item.NormalizedEmail == normalizedEmail,
            cancellationToken);
        if (user is null || user.EmailVerifiedAt is not null)
            throw EmailVerificationSupport.VerificationFailure();

        var response = await db.ExecuteInTransactionAsync<LoginChallengeResponse?>(async transactionToken =>
        {
            if (!await db.TryAcquireEmailVerificationLockAsync(user.Id, null, transactionToken))
                return null;
            var currentUser = await db.Users.SingleOrDefaultAsync(item => item.Id == user.Id, transactionToken);
            if (currentUser is null || currentUser.EmailVerifiedAt is not null)
                return null;
            var now = clock.UtcNow;
            var token = await db.EmailVerificationTokens.AsNoTracking()
                .Where(item => item.UserId == user.Id
                    && item.Purpose == EmailVerificationPurpose.Registration)
                .OrderByDescending(item => item.CreatedAt)
                .FirstOrDefaultAsync(transactionToken);
            if (token is null
                || token.UsedAt is not null
                || token.RevokedAt is not null
                || token.ExpiresAt <= now
                || token.FailedAttempts >= policy.MaximumFailedAttempts)
            {
                return null;
            }

            if (!otpService.VerifyOtp(user.Id, EmailVerificationSupport.RegistrationPurpose, otp, token.CodeHash))
            {
                await db.EmailVerificationTokens
                    .Where(item => item.Id == token.Id
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
                return null;
            }

            var consumed = await db.EmailVerificationTokens
                .Where(item => item.Id == token.Id
                    && item.UsedAt == null
                    && item.RevokedAt == null
                    && item.ExpiresAt > now
                    && item.FailedAttempts < policy.MaximumFailedAttempts)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(item => item.UsedAt, now),
                    transactionToken);
            if (consumed != 1)
                throw EmailVerificationSupport.VerificationFailure();

            currentUser.EmailVerifiedAt = now;
            await db.SaveChangesAsync(transactionToken);

            // Welcome dùng khóa cố định theo User nên retry không thể xếp email thứ hai.
            await emailVerification.QueueWelcomeAsync(currentUser, transactionToken);
            return await challenges.CreateAsync(
                currentUser,
                command.Request.DeviceName?.Trim() ?? "Browser",
                command.IpAddress,
                command.UserAgent,
                transactionToken);
        }, cancellationToken);
        return response ?? throw EmailVerificationSupport.VerificationFailure();
    }
}
