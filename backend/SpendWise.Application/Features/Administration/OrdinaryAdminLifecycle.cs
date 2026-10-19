using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Admin;
using SpendWise.Application.Features.Authentication;
using SpendWise.Domain.Identity;

namespace SpendWise.Application.Features.Administration;

public sealed record OrdinaryAdminDto(Guid Id, string DisplayName, string Status, DateTime CreatedAt);

public sealed class OrdinaryAdminLifecycle(
    ISpendWiseDbContext db, PlatformPermissionService permissions, AdminSecurityGuard security,
    AuthenticationSupport authentication, AdminAuditWriter audit)
{
    public async Task<IReadOnlyList<OrdinaryAdminDto>> ListAsync(ClaimsPrincipal principal, string reason, int page, int pageSize, CancellationToken ct)
    {
        var purpose = InputBoundary.RequiredText(reason, 1000, "Reason"); AdminAuditWriter.RequireReason(purpose);
        InputBoundary.Page(page, pageSize);
        if (page - 1 > int.MaxValue / pageSize) throw new AppException("invalid_pagination", "Page is too large.");
        if (!await permissions.HasAsync(principal, PlatformPermission.ManageOrdinaryAdmins, ct))
            throw new ForbiddenException("permission_denied", "SuperAdmin permission is required.");
        var rows = await db.Users.AsNoTracking().Where(u => u.SystemRole == SystemRole.Admin)
            .OrderBy(u => u.FullName).ThenBy(u => u.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(u => new OrdinaryAdminDto(u.Id, u.FullName, u.Status.ToString(), u.CreatedAt)).ToListAsync(ct);
        if (!await permissions.HasAsync(principal, PlatformPermission.ManageOrdinaryAdmins, ct))
            throw new ForbiddenException("permission_denied", "Operator permission changed.");
        var actor = Guid.Parse(principal.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        audit.Add(actor, "V1_ADMIN_DIRECTORY_READ", "AdminDirectory", "OrdinaryAdmins", purpose, new { Page = page, PageSize = pageSize, ReturnedCount = rows.Count });
        await db.SaveChangesAsync(ct);
        return rows;
    }

    public async Task SetAsync(ClaimsPrincipal principal, Guid targetId, bool enabled,
        string securityPin, string reason, CancellationToken ct)
    {
        var purpose = InputBoundary.RequiredText(reason, 1000, "Reason");
        AdminAuditWriter.RequireReason(purpose);
        if (!await permissions.HasAsync(principal, PlatformPermission.ManageOrdinaryAdmins, ct))
            throw new ForbiddenException("permission_denied", "SuperAdmin permission is required.");
        var actorId = Guid.Parse(principal.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        if (actorId == targetId)
            throw new ForbiddenException("self_role_change_forbidden", "An operator cannot change their own role.");
        await security.VerifyAsync(actorId, securityPin, ct, SystemRole.SuperAdmin);

        await db.ExecuteInTransactionAsync(async token =>
        {
            // Role writers use the same ordered user locks; permission is rechecked after admission.
            foreach (var id in new[] { actorId, targetId }.Order())
                await db.Users.Where(u => u.Id == id)
                    .ExecuteUpdateAsync(s => s.SetProperty(u => u.SystemRole, u => u.SystemRole), token);
            if (!await permissions.HasAsync(principal, PlatformPermission.ManageOrdinaryAdmins, token))
                throw new ForbiddenException("permission_denied", "Operator permission changed.");
            var target = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == targetId, token)
                ?? throw new NotFoundException("user_not_found", "User not found.");
            if (target.SystemRole is not (SystemRole.User or SystemRole.Admin))
                throw new ForbiddenException("protected_role", "This role cannot be changed through ordinary Admin management.");
            if (enabled && (target.Status != UserStatus.Active || target.EmailVerifiedAt is null
                || target.MustChangePassword || string.IsNullOrEmpty(target.SecurityPinHash)))
                throw new ConflictException("operator_not_ready", "An active verified account with PIN is required.");
            if (enabled)
            {
                var existingAdminCount = await db.Users.CountAsync(u => u.SystemRole == SystemRole.Admin && u.Id != targetId, token);
                if (existingAdminCount >= 1)
                    throw new ConflictException("admin_limit_exceeded", "SpendWise allows at most one active Admin across the entire system.");
            }
            var desired = enabled ? SystemRole.Admin : SystemRole.User;
            if (target.SystemRole == desired) return true;
            var changed = await db.Users.Where(u => u.Id == targetId && u.SystemRole == target.SystemRole)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.SystemRole, desired), token);
            if (changed != 1) throw new ConflictException("role_changed", "User role changed; re-evaluate the operation.");
            await authentication.RevokeAllSessionsAsync(targetId, token);
            audit.Add(actorId, "V1_ADMIN_ROLE_CHANGED", "User", targetId.ToString(), purpose,
                new { PreviousRole = target.SystemRole.ToString(), Role = desired.ToString() });
            await db.SaveChangesAsync(token);
            return true;
        }, ct);
    }
}
