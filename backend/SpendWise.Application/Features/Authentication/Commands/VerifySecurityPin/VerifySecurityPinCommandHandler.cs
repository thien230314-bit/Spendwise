using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Identity;

namespace SpendWise.Application.Features.Authentication.Commands.VerifySecurityPin;

public sealed class VerifySecurityPinCommandHandler(
    ISpendWiseDbContext db,
    ISecurityPinService pins,
    AccountCredentialLockout lockout) : IRequestHandler<VerifySecurityPinCommand>
{
    public async Task Handle(
        VerifySecurityPinCommand command,
        CancellationToken cancellationToken)
    {
        var pin = InputBoundary.SecurityPin(command.Request.Pin);
        var pinHash = await db.Users
            .Where(item => item.Id == command.UserId)
            .Select(item => item.SecurityPinHash)
            .SingleOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(pinHash))
        {
            throw new ConflictException("pin_not_configured", "Security PIN is not configured.");
        }

        var verified = !await lockout.IsSecurityPinLockedAsync(command.UserId, cancellationToken)
            && pins.Verify(pin, pinHash);
        if (!verified)
        {
            await lockout.RecordSecurityPinFailureAsync(command.UserId, cancellationToken);
            throw new AppException("pin_verification_failed", "PIN verification failed.", 401);
        }
        if (await lockout.ResetSecurityPinAfterSuccessAsync(command.UserId, cancellationToken) != 1)
        {
            throw new AppException("pin_verification_failed", "PIN verification failed.", 401);
        }
    }
}
