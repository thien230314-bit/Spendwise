using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Operations;
using SpendWise.Domain.Admin;
using SpendWise.Domain.Identity;

namespace SpendWise.Application.Features.V1Groups;

public sealed class V1ActivityPolicy
{
    public bool CoverageVerified { get; set; }
    public DateTime? CoverageStartsAtUtc { get; set; }
}

public sealed class V1ActivityService(ISpendWiseDbContext db, IClock clock, IOptions<V1ActivityPolicy> policy) : IV1InactivityEvidence
{
    public Task RecordAsync(Guid userId, Guid sessionId, CancellationToken ct) => db.ExecuteInTransactionAsync(async token =>
    {
        if (!await db.AcquireGroupAdmissionLockAsync(userId, token)
            || !await db.Users.AnyAsync(u => u.Id == userId && u.SystemRole == SystemRole.User && u.Status == UserStatus.Active && u.EmailVerifiedAt != null && !u.MustChangePassword, token)
            || await db.UserSessions.Where(s => s.Id == sessionId && s.UserId == userId && s.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(s => s.RevokedAt, s => s.RevokedAt), token) != 1)
            throw new ForbiddenException("active_user_session_required", "An active User session is required.");
        var now = clock.UtcNow;
        if (await db.V1UserActivities.Where(a => a.UserId == userId && a.LastActionAt < now).ExecuteUpdateAsync(s => s.SetProperty(a => a.LastActionAt, now), token) == 0
            && !await db.V1UserActivities.AnyAsync(a => a.UserId == userId, token))
            db.V1UserActivities.Add(new V1UserActivity { UserId = userId, LastActionAt = now });
        await db.SaveChangesAsync(token); return true;
    }, ct);

    public async Task<V1InactivityProof> GetAsync(Guid userId, CancellationToken ct)
    {
        var p = policy.Value; var now = clock.UtcNow;
        if (!p.CoverageVerified || p.CoverageStartsAtUtc is not { Kind: DateTimeKind.Utc } start || start > now)
            return new(false, false, null);
        var user = await db.Users.AsNoTracking().Where(u => u.Id == userId && u.SystemRole == SystemRole.User).Select(u => new { u.CreatedAt }).SingleOrDefaultAsync(ct);
        if (user is null) return new(false, false, null);
        var last = await db.V1UserActivities.AsNoTracking().Where(a => a.UserId == userId).Select(a => (DateTime?)a.LastActionAt).SingleOrDefaultAsync(ct);
        var since = new[] { start, user.CreatedAt, last ?? start }.Max();
        var eligible = await V1MaintenanceGate.ActiveElapsedAsync(db, since, now, ct) >= TimeSpan.FromDays(15);
        return new(true, eligible, $"activity-v1:{start.Ticks}:{since.Ticks}");
    }
}
