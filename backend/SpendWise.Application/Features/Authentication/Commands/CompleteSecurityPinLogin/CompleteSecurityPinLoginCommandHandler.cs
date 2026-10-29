using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Identity;
using SpendWise.Domain.Identity;

namespace SpendWise.Application.Features.Authentication.Commands.CompleteSecurityPinLogin;

public sealed class CompleteSecurityPinLoginCommandHandler(
    ISpendWiseDbContext db,
    ISecurityPinService pins,
    LoginChallengeSupport challenges,
    AuthenticationSupport authentication,
    InteractiveLoginSupport interactiveLogin,
    IClock clock,
    AccountCredentialLockout lockout) : IRequestHandler<CompleteSecurityPinLoginCommand, AuthResponse>
{
    public async Task<AuthResponse> Handle(
        CompleteSecurityPinLoginCommand command,
        CancellationToken cancellationToken)
    {
        var pin = InputBoundary.SecurityPin(command.Request.Pin);
        var challenge = await challenges.GetValidAsync(
            command.Request.ChallengeToken,
            LoginChallengePurpose.PinVerification,
            cancellationToken);
        var pinVerified = !await lockout.IsSecurityPinLockedAsync(challenge.UserId, cancellationToken)
            && !string.IsNullOrWhiteSpace(challenge.User.SecurityPinHash)
            && pins.Verify(pin, challenge.User.SecurityPinHash);
        if (!pinVerified)
        {
            await lockout.RecordSecurityPinFailureAsync(challenge.UserId, cancellationToken);
            // Chỉ tăng số lần sai; không log hoặc trả chi tiết về giá trị PIN.
            await challenges.RecordFailedAttemptAsync(challenge.Id, cancellationToken);
            throw LoginChallengeSupport.Failure();
        }
        if (await lockout.ResetSecurityPinAfterSuccessAsync(challenge.UserId, cancellationToken) != 1)
        {
            await challenges.RecordFailedAttemptAsync(challenge.Id, cancellationToken);
            throw LoginChallengeSupport.Failure();
        }

        return await db.ExecuteInTransactionAsync(async token =>
        {
            // Chỉ request thắng conditional consume mới được tạo session/token.
            if (await challenges.ConsumeAsync(challenge.Id, token) != 1)
            {
                throw LoginChallengeSupport.Failure();
            }

            var user = await db.Users.SingleAsync(item => item.Id == challenge.UserId, token);
            if (user.Status != UserStatus.Active && user.Status != UserStatus.Restricted)
            {
                throw new AppException(
                    "account_unavailable",
                    "The account is not active.",
                    403);
            }
            if (user.EmailVerifiedAt is null)
            {
                throw new AppException(
                    "email_verification_required",
                    "Email verification is required before login.",
                    403);
            }
            // Giữ mốc login trước để E07 dùng đúng elapsed UTC, không dùng calendar day.
            var previousLoginAt = user.LastLoginAt;
            var loginAt = clock.UtcNow;
            var device = await interactiveLogin.PrepareDeviceAsync(
                user.Id,
                command.Request.DeviceTrustToken,
                token);
            var sessionId = Guid.NewGuid();
            user.LastLoginAt = loginAt;
            var response = await authentication.IssueAsync(
                user,
                challenge.DeviceName,
                challenge.IpAddress,
                challenge.UserAgent,
                token,
                device.TokenHash,
                device.RawToken,
                sessionId);
            await interactiveLogin.QueueLoginEmailAsync(
                user,
                sessionId,
                previousLoginAt,
                loginAt,
                device,
                challenge.DeviceName,
                challenge.UserAgent,
                challenge.IpAddress,
                token);
            return response;
        }, cancellationToken);
    }
}
