using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Interfaces;

namespace SpendWise.Application.Features.Authentication;

// Quản lý hai khóa tạm thời độc lập bằng UPDATE nguyên tử để không mất lượt sai đồng thời.
public sealed class AccountCredentialLockout(
    ISpendWiseDbContext db,
    IClock clock)
{
    public const int MaximumFailedAttempts = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(5);

    public Task<bool> IsPasswordLockedAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        return db.Users.AsNoTracking().AnyAsync(
            user => user.Id == userId
                && user.PasswordLockoutUntil != null
                && user.PasswordLockoutUntil > now,
            cancellationToken);
    }

    public async Task RecordPasswordFailureAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        if (UsesInMemoryProvider())
        {
            await RecordInMemoryAsync(userId, password: true, now, cancellationToken);
            return;
        }

        var lockoutUntil = now.Add(LockoutDuration);
        await db.Users
            .Where(user => user.Id == userId
                && (user.PasswordLockoutUntil == null || user.PasswordLockoutUntil <= now))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(
                    user => user.PasswordFailedAttempts,
                    user => user.PasswordLockoutUntil != null && user.PasswordLockoutUntil <= now
                        ? 1
                        : user.PasswordFailedAttempts + 1)
                .SetProperty(
                    user => user.PasswordLockoutUntil,
                    user => (user.PasswordLockoutUntil != null && user.PasswordLockoutUntil <= now
                            ? 1
                            : user.PasswordFailedAttempts + 1) >= MaximumFailedAttempts
                        ? lockoutUntil
                        : null),
                cancellationToken);
    }

    public async Task<int> ResetPasswordAfterSuccessAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        if (UsesInMemoryProvider())
        {
            return await ResetInMemoryAsync(userId, password: true, now, cancellationToken);
        }

        return await db.Users
            .Where(user => user.Id == userId
                && (user.PasswordLockoutUntil == null || user.PasswordLockoutUntil <= now))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(user => user.PasswordFailedAttempts, 0)
                .SetProperty(user => user.PasswordLockoutUntil, (DateTime?)null),
                cancellationToken);
    }

    public Task<bool> IsSecurityPinLockedAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        return db.Users.AsNoTracking().AnyAsync(
            user => user.Id == userId
                && user.SecurityPinLockoutUntil != null
                && user.SecurityPinLockoutUntil > now,
            cancellationToken);
    }

    public async Task RecordSecurityPinFailureAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        if (UsesInMemoryProvider())
        {
            await RecordInMemoryAsync(userId, password: false, now, cancellationToken);
            return;
        }

        var lockoutUntil = now.Add(LockoutDuration);
        await db.Users
            .Where(user => user.Id == userId
                && (user.SecurityPinLockoutUntil == null || user.SecurityPinLockoutUntil <= now))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(
                    user => user.SecurityPinFailedAttempts,
                    user => user.SecurityPinLockoutUntil != null && user.SecurityPinLockoutUntil <= now
                        ? 1
                        : user.SecurityPinFailedAttempts + 1)
                .SetProperty(
                    user => user.SecurityPinLockoutUntil,
                    user => (user.SecurityPinLockoutUntil != null && user.SecurityPinLockoutUntil <= now
                            ? 1
                            : user.SecurityPinFailedAttempts + 1) >= MaximumFailedAttempts
                        ? lockoutUntil
                        : null),
                cancellationToken);
    }

    public async Task<int> ResetSecurityPinAfterSuccessAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        if (UsesInMemoryProvider())
        {
            return await ResetInMemoryAsync(userId, password: false, now, cancellationToken);
        }

        return await db.Users
            .Where(user => user.Id == userId
                && (user.SecurityPinLockoutUntil == null || user.SecurityPinLockoutUntil <= now))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(user => user.SecurityPinFailedAttempts, 0)
                .SetProperty(user => user.SecurityPinLockoutUntil, (DateTime?)null),
                cancellationToken);
    }

    // EF InMemory chỉ dùng trong một số test cũ và không hỗ trợ ExecuteUpdate.
    // Mọi provider quan hệ (SQL Server/SQLite) vẫn luôn đi qua UPDATE nguyên tử phía database.
    private bool UsesInMemoryProvider() =>
        db is DbContext context
        && string.Equals(
            context.Database.ProviderName,
            "Microsoft.EntityFrameworkCore.InMemory",
            StringComparison.Ordinal);

    private async Task RecordInMemoryAsync(
        Guid userId,
        bool password,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var user = await db.Users.SingleOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user is null) return;

        if (password)
        {
            if (user.PasswordLockoutUntil > now) return;
            user.PasswordFailedAttempts = user.PasswordLockoutUntil is not null
                ? 1
                : user.PasswordFailedAttempts + 1;
            user.PasswordLockoutUntil = user.PasswordFailedAttempts >= MaximumFailedAttempts
                ? now.Add(LockoutDuration)
                : null;
        }
        else
        {
            if (user.SecurityPinLockoutUntil > now) return;
            user.SecurityPinFailedAttempts = user.SecurityPinLockoutUntil is not null
                ? 1
                : user.SecurityPinFailedAttempts + 1;
            user.SecurityPinLockoutUntil = user.SecurityPinFailedAttempts >= MaximumFailedAttempts
                ? now.Add(LockoutDuration)
                : null;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<int> ResetInMemoryAsync(
        Guid userId,
        bool password,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var user = await db.Users.SingleOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user is null) return 0;
        if (password)
        {
            if (user.PasswordLockoutUntil > now) return 0;
            user.PasswordFailedAttempts = 0;
            user.PasswordLockoutUntil = null;
        }
        else
        {
            if (user.SecurityPinLockoutUntil > now) return 0;
            user.SecurityPinFailedAttempts = 0;
            user.SecurityPinLockoutUntil = null;
        }

        await db.SaveChangesAsync(cancellationToken);
        return 1;
    }
}
