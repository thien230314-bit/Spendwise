using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Authentication;
using SpendWise.Application.Identity;

namespace SpendWise.Application.Features;

// Xác minh PIN ngay trong từng thao tác tài chính, không tin trạng thái frontend.
public sealed class FinancialSecurityGuard(
    ISpendWiseDbContext db,
    ISecurityPinService pins,
    AccountCredentialLockout lockout)
{
    public async Task VerifyAsync(Guid userId, string? suppliedPin, CancellationToken cancellationToken)
    {
        var pin = InputBoundary.SecurityPin(suppliedPin, "Security PIN");
        var hash = await db.Users.AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => user.SecurityPinHash)
            .SingleOrDefaultAsync(cancellationToken);
        var verified = !await lockout.IsSecurityPinLockedAsync(userId, cancellationToken)
            && !string.IsNullOrWhiteSpace(hash)
            && pins.Verify(pin, hash);
        if (!verified)
        {
            await lockout.RecordSecurityPinFailureAsync(userId, cancellationToken);
            throw new AppException("pin_verification_failed", "PIN verification failed.", 401);
        }
        if (await lockout.ResetSecurityPinAfterSuccessAsync(userId, cancellationToken) != 1)
        {
            throw new AppException("pin_verification_failed", "PIN verification failed.", 401);
        }
    }
}
