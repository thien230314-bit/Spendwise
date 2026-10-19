using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Identity;

namespace SpendWise.Application.Features.Authentication.Commands.ChangeSecurityPin;

public sealed class ChangeSecurityPinCommandHandler(
    ISpendWiseDbContext db,
    ISecurityPinService pins,
    AccountCredentialLockout lockout) : IRequestHandler<ChangeSecurityPinCommand>
{
    public async Task Handle(
        ChangeSecurityPinCommand command,
        CancellationToken cancellationToken)
    {
        var currentPin = InputBoundary.SecurityPin(command.Request.CurrentPin, "Current PIN");
        var newPin = InputBoundary.SecurityPin(command.Request.NewPin, "New PIN");
        var confirmation = InputBoundary.SecurityPin(command.Request.ConfirmPin, "Confirm PIN");
        if (!string.Equals(newPin, confirmation, StringComparison.Ordinal))
        {
            throw new AppException("pin_mismatch", "New PIN and confirmation PIN do not match.");
        }

        var user = await db.Users.SingleOrDefaultAsync(
            item => item.Id == command.UserId,
            cancellationToken)
            ?? throw new NotFoundException("user_not_found", "User not found.");
        if (string.IsNullOrWhiteSpace(user.SecurityPinHash))
        {
            throw new ConflictException("pin_not_configured", "Security PIN is not configured.");
        }
        var verified = !await lockout.IsSecurityPinLockedAsync(command.UserId, cancellationToken)
            && pins.Verify(currentPin, user.SecurityPinHash);
        if (!verified)
        {
            await lockout.RecordSecurityPinFailureAsync(command.UserId, cancellationToken);
            throw new AppException("pin_verification_failed", "PIN verification failed.", 401);
        }
        if (await lockout.ResetSecurityPinAfterSuccessAsync(command.UserId, cancellationToken) != 1)
        {
            throw new AppException("pin_verification_failed", "PIN verification failed.", 401);
        }
        if (pins.Verify(newPin, user.SecurityPinHash))
        {
            throw new ConflictException(
                "pin_unchanged",
                "New PIN must be different from the current PIN.");
        }

        // Salt mới được tạo cho mỗi lần đổi PIN; plaintext không được lưu hoặc ghi log.
        user.SecurityPinHash = pins.Hash(newPin);
        user.SecurityPinFailedAttempts = 0;
        user.SecurityPinLockoutUntil = null;
        await db.SaveChangesAsync(cancellationToken);
    }
}
