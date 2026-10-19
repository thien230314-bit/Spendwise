using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Domain.Admin;
using SpendWise.Domain.Identity;

namespace SpendWise.Application.Features.Administration;

public sealed class PlatformPermissionService(ISpendWiseDbContext db)
{
    public async Task<bool> HasAsync(ClaimsPrincipal principal, PlatformPermission permission, CancellationToken ct)
    {
        if (!TryIdentity(principal, permission, out var actor, out var session, out var role))
            return false;

        // Never authorize a revoked or downgraded operator from a cached role claim.
        return await db.UserSessions.AsNoTracking().AnyAsync(s => s.Id == session && s.UserId == actor
            && s.RevokedAt == null && s.User.Status == UserStatus.Active && s.User.EmailVerifiedAt != null
            && !s.User.MustChangePassword && s.User.SystemRole == role, ct);
    }

    public async Task<bool> HasSupportScopeAsync(
        ClaimsPrincipal principal, Guid caseId, Guid targetUserId, CancellationToken ct)
    {
        if (!await HasAsync(principal, PlatformPermission.SensitiveRead, ct)) return false;
        var actor = Guid.Parse(principal.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        return await db.SupportCases.AsNoTracking().AnyAsync(c => c.Id == caseId
            && c.LinkedUserId == targetUserId && (c.AssignedAdminId == actor || principal.IsInRole("SuperAdmin") && c.EscalatedAt != null && c.EscalationReviewerId == actor)
            && c.Status != CaseStatus.Resolved && c.Status != CaseStatus.Rejected && c.Status != CaseStatus.Closed, ct);
    }

    public IQueryable<User> SupportSubjects(ClaimsPrincipal principal, Guid caseId, Guid targetId)
    {
        if (!TryIdentity(principal, PlatformPermission.SensitiveRead, out var actor, out var session, out var role))
            return db.Users.AsNoTracking().Where(u => false);
        return db.Users.AsNoTracking().Where(u => u.Id == targetId && u.SystemRole == SystemRole.User
            && db.SupportCases.Any(c => c.Id == caseId && c.LinkedUserId == u.Id && (c.AssignedAdminId == actor || role == SystemRole.SuperAdmin && c.EscalatedAt != null && c.EscalationReviewerId == actor)
                && c.Status != CaseStatus.Resolved && c.Status != CaseStatus.Rejected && c.Status != CaseStatus.Closed)
            && db.UserSessions.Any(s => s.Id == session && s.UserId == actor && s.RevokedAt == null
                && s.User.SystemRole == role && s.User.Status == UserStatus.Active
                && s.User.EmailVerifiedAt != null && !s.User.MustChangePassword));
    }

    private static bool TryIdentity(ClaimsPrincipal principal, PlatformPermission permission, out Guid actor, out Guid session, out SystemRole role)
    {
        actor = session = default; role = default;
        return principal.Identity?.IsAuthenticated == true
            && Guid.TryParse(principal.FindFirst(ClaimTypes.NameIdentifier)?.Value, out actor)
            && Guid.TryParse(principal.FindFirst("sid")?.Value, out session)
            && principal.FindAll(ClaimTypes.Role).Count() == 1
            && Enum.TryParse(principal.FindFirst(ClaimTypes.Role)?.Value, out role)
            && principal.FindFirst(ClaimTypes.Role)?.Value == role.ToString()
            && PlatformPermissions.Allows(role, permission);
    }
}
