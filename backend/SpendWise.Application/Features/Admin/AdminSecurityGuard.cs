using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Authentication;
using SpendWise.Application.Identity;
using SpendWise.Domain.Identity;

namespace SpendWise.Application.Features.Admin;

// Reauthenticate operators; legacy callers retain the SystemAdmin-only default.
public sealed class AdminSecurityGuard(
    ISpendWiseDbContext db,
    ISecurityPinService pins,
    AccountCredentialLockout lockout)
{
    public static async Task<User> RequireOrdinaryTargetAsync(ISpendWiseDbContext db, Guid userId, CancellationToken ct)
    {
        // Retain the same User-row lock used by V1 role changes until the caller commits.
        if (db is DbContext context && context.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory")
        {
            if (context.Database.CurrentTransaction is null)
                throw new InvalidOperationException("Administrative target admission requires a transaction.");
            if (await db.Users.Where(u => u.Id == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.SystemRole, u => u.SystemRole), ct) != 1)
                throw new NotFoundException("user_not_found", "User not found.");
        }
        var user = await db.Users.FindAsync([userId], ct)
            ?? throw new NotFoundException("user_not_found", "User not found.");
        if (db is DbContext tracked)
        {
            if (tracked.Entry(user).State != EntityState.Unchanged)
                throw new ConflictException("target_state_changed", "Reload the target before administrative mutation.");
            await tracked.Entry(user).ReloadAsync(ct);
        }
        if (user.SystemRole != SystemRole.User)
            throw new ForbiddenException("admin_peer_protected", "Administrative accounts cannot be changed through User management.");
        return user;
    }

    public async Task VerifyAsync(
        Guid adminId,
        string suppliedPin,
        CancellationToken cancellationToken,
        SystemRole requiredRole = SystemRole.SystemAdmin)
    {
        var pin = InputBoundary.SecurityPin(suppliedPin, "Admin PIN");
        var admin = await db.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == adminId
                && item.SystemRole == requiredRole
                && item.Status == UserStatus.Active,
                cancellationToken);
        if (admin is null)
        {
            throw new ForbiddenException(
                "admin_pin_invalid",
                "Administrator PIN verification failed.");
        }
        var verified = !await lockout.IsSecurityPinLockedAsync(admin.Id, cancellationToken)
            && !string.IsNullOrWhiteSpace(admin.SecurityPinHash)
            && pins.Verify(pin, admin.SecurityPinHash);
        if (!verified)
        {
            await lockout.RecordSecurityPinFailureAsync(admin.Id, cancellationToken);
            throw new ForbiddenException(
                "admin_pin_invalid",
                "Administrator PIN verification failed.");
        }
        if (await lockout.ResetSecurityPinAfterSuccessAsync(admin.Id, cancellationToken) != 1)
        {
            throw new ForbiddenException(
                "admin_pin_invalid",
                "Administrator PIN verification failed.");
        }
    }
}
