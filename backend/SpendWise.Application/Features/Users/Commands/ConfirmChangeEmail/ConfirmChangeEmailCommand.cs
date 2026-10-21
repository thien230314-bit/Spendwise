using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Authentication;
using SpendWise.Application.Identity;
using SpendWise.Domain.Identity;

namespace SpendWise.Application.Features.Users.Commands.ConfirmChangeEmail;

public sealed record ConfirmChangeEmailRequest(string Otp);

public sealed record ConfirmChangeEmailCommand(
    Guid UserId,
    ConfirmChangeEmailRequest Request,
    string? IpAddress) : IRequest<UserProfile>;

public sealed class ConfirmChangeEmailCommandHandler(
    ISpendWiseDbContext db,
    IEmailVerificationOtpService otpService,
    IEmailVerificationPolicy policy,
    EmailVerificationSupport emailVerification,
    IClock clock) : IRequestHandler<ConfirmChangeEmailCommand, UserProfile>
{
    public async Task<UserProfile> Handle(
        ConfirmChangeEmailCommand command,
        CancellationToken ct)
    {
        var otp = command.Request.Otp?.Trim() ?? string.Empty;
        if (otp.Length != 8 || otp.Any(c => !char.IsAsciiDigit(c)))
            throw EmailVerificationSupport.VerificationFailure();

        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == command.UserId, ct)
            ?? throw new NotFoundException("user_not_found", "User not found.");

        if (string.IsNullOrWhiteSpace(user.PendingNewEmail))
            throw new AppException("no_pending_email", "No pending email change request found.", 400);

        var pendingEmail = user.PendingNewEmail.Trim();
        var normalizedPendingEmail = InputBoundary.NormalizeEmail(pendingEmail);

        var result = await db.ExecuteInTransactionAsync<UserProfile?>(async transactionToken =>
        {
            var ipHash = string.IsNullOrWhiteSpace(command.IpAddress) ? null : otpService.HashRateLimitKey(command.IpAddress);
            if (!await db.TryAcquireEmailVerificationLockAsync(user.Id, ipHash, transactionToken))
                throw EmailVerificationSupport.VerificationFailure();

            // Check duplicate email again inside transaction to prevent race conditions
            var isDuplicate = await db.Users.AsNoTracking().AnyAsync(
                u => u.NormalizedEmail == normalizedPendingEmail && u.Id != user.Id,
                transactionToken);

            if (isDuplicate)
                throw new AppException("duplicate_email", "Email address is already in use by another account.", 409);

            var now = clock.UtcNow;
            var token = await db.EmailVerificationTokens.AsNoTracking()
                .Where(item => item.UserId == user.Id
                    && item.Purpose == EmailVerificationPurpose.ChangeEmail)
                .OrderByDescending(item => item.CreatedAt)
                .FirstOrDefaultAsync(transactionToken);

            if (token is null
                || token.UsedAt is not null
                || token.RevokedAt is not null
                || token.ExpiresAt <= now
                || token.FailedAttempts >= policy.MaximumFailedAttempts)
            {
                throw EmailVerificationSupport.VerificationFailure();
            }

            if (!otpService.VerifyOtp(user.Id, EmailVerificationSupport.ChangeEmailPurpose, otp, token.CodeHash))
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

                throw EmailVerificationSupport.VerificationFailure();
            }

            // Mark token as used
            await db.EmailVerificationTokens
                .Where(item => item.Id == token.Id)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(item => item.UsedAt, now),
                    transactionToken);

            var oldEmail = user.Email;

            // Commit new email to User entity
            user.Email = pendingEmail;
            user.NormalizedEmail = normalizedPendingEmail;
            user.PendingNewEmail = null;
            user.EmailVerifiedAt = now;
            await db.SaveChangesAsync(transactionToken);

            // Queue security notice to the old email
            await emailVerification.QueueEmailChangeNoticeAsync(oldEmail, pendingEmail, transactionToken);

            return AuthenticationSupport.MapProfile(user);
        }, ct);

        return result ?? throw EmailVerificationSupport.VerificationFailure();
    }
}
