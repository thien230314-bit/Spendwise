using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Identity;
using SpendWise.Domain.Identity;

namespace SpendWise.Application.Features.Authentication.Commands.SetupSecurityPin;

public sealed class SetupSecurityPinCommandHandler(
    ISpendWiseDbContext db,
    ISecurityPinService pins,
    LoginChallengeSupport challenges,
    AuthenticationSupport authentication,
    InteractiveLoginSupport interactiveLogin,
    IClock clock) : IRequestHandler<SetupSecurityPinCommand, AuthResponse>
{
    public async Task<AuthResponse> Handle(
        SetupSecurityPinCommand command,
        CancellationToken cancellationToken)
    {
        var pin = InputBoundary.SecurityPin(command.Request.Pin);
        var confirmation = InputBoundary.SecurityPin(command.Request.ConfirmPin, "Confirm PIN");
        if (!string.Equals(pin, confirmation, StringComparison.Ordinal))
        {
            throw new AppException("pin_mismatch", "PIN and confirmation PIN do not match.");
        }

        var challenge = await challenges.GetValidAsync(
            command.Request.ChallengeToken,
            LoginChallengePurpose.PinSetup,
            cancellationToken);
        if (!string.IsNullOrWhiteSpace(challenge.User.SecurityPinHash))
        {
            throw LoginChallengeSupport.Failure();
        }

        return await db.ExecuteInTransactionAsync(async token =>
        {
            // Conditional consume bảo đảm challenge chỉ thiết lập PIN đúng một lần.
            if (await challenges.ConsumeAsync(challenge.Id, token) != 1)
            {
                throw LoginChallengeSupport.Failure();
            }

            var user = await db.Users.SingleAsync(item => item.Id == challenge.UserId, token);
            if (!string.IsNullOrWhiteSpace(user.SecurityPinHash))
            {
                throw LoginChallengeSupport.Failure();
            }
            if (user.Status != UserStatus.Active)
            {
                throw new AppException("account_unavailable", "The account is not active.", 403);
            }
            if (user.EmailVerifiedAt is null)
            {
                throw new AppException(
                    "email_verification_required",
                    "Email verification is required before PIN setup.",
                    403);
            }

            user.SecurityPinHash = pins.Hash(pin);
            user.SecurityPinFailedAttempts = 0;
            user.SecurityPinLockoutUntil = null;
            user.LastLoginAt = clock.UtcNow;
            // Onboarding tạo trust token đầu tiên nhưng không gửi cảnh báo E07/E08.
            var device = await interactiveLogin.PrepareDeviceAsync(user.Id, null, token);
            return await authentication.IssueAsync(
                user,
                challenge.DeviceName,
                challenge.IpAddress,
                challenge.UserAgent,
                token,
                device.TokenHash,
                device.RawToken,
                Guid.NewGuid());
        }, cancellationToken);
    }
}
