using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Identity;
using SpendWise.Domain.Notifications;

namespace SpendWise.Application.Features.Authentication.Commands.ResetSecurityPin;

public sealed class ResetSecurityPinCommandHandler(
    ISpendWiseDbContext db,
    SecurityPinResetSupport resets,
    ISecurityPinService pins,
    AuthenticationSupport authentication,
    IClock? clock = null)
    : IRequestHandler<ResetSecurityPinCommand>
{
    public async Task Handle(
        ResetSecurityPinCommand command,
        CancellationToken cancellationToken)
    {
        var newPin = InputBoundary.SecurityPin(command.Request.NewPin, "New PIN");
        var confirmPin = InputBoundary.SecurityPin(command.Request.ConfirmPin, "Confirm PIN");
        if (!string.Equals(newPin, confirmPin, StringComparison.Ordinal))
        {
            throw new AppException("pin_mismatch", "New PIN and confirmation PIN do not match.");
        }

        var resetToken = await resets.GetValidAsync(command.Request.Token, cancellationToken);
        await db.ExecuteInTransactionAsync(async token =>
        {
            if (!await db.TryAcquireEmailVerificationLockAsync(resetToken.UserId, null, token))
                throw SecurityPinResetSupport.Failure();
            // Revalidate after admission: a replacement/disabled account must not use stale proof.
            await resets.GetValidAsync(command.Request.Token, token);
            // Conditional consume ngăn một token reset được dùng hai lần đồng thời.
            if (await resets.ConsumeAsync(resetToken.Id, token) != 1)
            {
                throw SecurityPinResetSupport.Failure();
            }

            var emailKey = $"email:security-pin-reset:{resetToken.TokenHash}";
            await db.EmailOutboxMessages.Where(x => x.DeduplicationKey == emailKey && x.Status == EmailDeliveryStatus.Pending)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, EmailDeliveryStatus.Failed)
                    .SetProperty(x => x.LastError, "content_consumed").SetProperty(x => x.ContentExpiresAt, clock == null ? DateTime.UtcNow : clock.UtcNow)
                    .SetProperty(x => x.HtmlBody, string.Empty).SetProperty(x => x.TextBody, string.Empty), token);

            var user = await db.Users.FindAsync([resetToken.UserId], token)
                ?? throw new NotFoundException("user_not_found", "User not found.");
            user.SecurityPinHash = pins.Hash(newPin);
            user.SecurityPinFailedAttempts = 0;
            user.SecurityPinLockoutUntil = null;
            await authentication.RevokeAllSessionsAsync(user.Id, token);
            await db.SaveChangesAsync(token);
            return true;
        }, cancellationToken);
    }
}
