using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Domain.Groups;

namespace SpendWise.Application.Features.Groups;

internal static class GroupAdmission
{
    public static async Task LockUsersAsync(ISpendWiseDbContext db, IEnumerable<Guid> users, CancellationToken ct)
    {
        if (db is DbContext context)
        {
            if (context.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory") return;
            if (context.Database.CurrentTransaction is null)
                throw new InvalidOperationException("Group admission requires an enclosing transaction.");
        }
        // Update locks serialize admissions but allow FK shared locks for activity/notification inserts.
        foreach (var id in users.Distinct().Order())
        {
            if (!await db.AcquireGroupAdmissionLockAsync(id, ct))
                throw new NotFoundException("user_not_found", "User not found.");
        }
    }

    public static async Task RequireCapacityAsync(ISpendWiseDbContext db, Guid userId, CancellationToken ct)
    {
        var current = await db.V1FundMemberships.CountAsync(m => m.UserId == userId && m.LeftAt == null
            && db.V1Funds.Any(f => f.Id == m.FundId && f.DissolvedAt == null), ct);
        var legacy = await db.GroupMembers.CountAsync(m => m.UserId == userId && m.Group.Status != GroupStatus.Closed, ct);
        if (current + legacy >= 10)
            throw new ConflictException("active_group_limit", "A User may participate in at most 10 active groups.");
    }

    public static async Task<bool> ResolveInviteAsync(ISpendWiseDbContext db, Guid inviteId, InviteStatus outcome, CancellationToken ct)
    {
        if (db is DbContext context && context.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
        {
            var invite = await db.GroupInvites.SingleAsync(i => i.Id == inviteId, ct);
            if (invite.Status != InviteStatus.Pending) return false;
            invite.Status = outcome;
            await db.SaveChangesAsync(ct);
            return true;
        }
        return await db.GroupInvites.Where(i => i.Id == inviteId && i.Status == InviteStatus.Pending)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.Status, outcome), ct) == 1;
    }
}
