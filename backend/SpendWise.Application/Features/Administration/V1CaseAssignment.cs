using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Admin;
using SpendWise.Application.Notifications;
using SpendWise.Domain.Admin;
using SpendWise.Domain.Identity;

namespace SpendWise.Application.Features.Administration;

public sealed record V1CaseTaskDto(Guid Id, string TicketNumber, string Status, string Priority, Guid? AssignedAdminId);

public sealed class V1CaseAssignment(ISpendWiseDbContext db, PlatformPermissionService permissions, AdminSecurityGuard security,
    AdminAuditWriter audit, INotificationService notifications)
{
    public async Task AssignAsync(ClaimsPrincipal principal, Guid caseId, Guid operatorId, string pin, string reason, CancellationToken ct)
    {
        var purpose = InputBoundary.RequiredText(reason, 1000, "Reason"); AdminAuditWriter.RequireReason(purpose);
        if (!await permissions.HasAsync(principal, PlatformPermission.OperationsRead, ct)) throw new ForbiddenException("permission_denied", "Admin permission required.");
        var actor = Guid.Parse(principal.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        await security.VerifyAsync(actor, pin, ct, SystemRole.Admin);
        await db.ExecuteInTransactionAsync(async token =>
        {
            var subject = await db.SupportCases.AsNoTracking().Where(c => c.Id == caseId).Select(c => (Guid?)c.LinkedUserId).SingleOrDefaultAsync(token)
                ?? throw new NotFoundException("case_not_found", "Case not found.");
            foreach (var id in new[] { actor, operatorId, subject }.Distinct().Order())
                if (!await db.AcquireGroupAdmissionLockAsync(id, token)) throw new NotFoundException("user_not_found", "User not found.");
            var session = Guid.Parse(principal.FindFirst("sid")!.Value);
            if (await db.UserSessions.Where(s => s.Id == session && s.UserId == actor && s.RevokedAt == null)
                    .ExecuteUpdateAsync(s => s.SetProperty(s => s.RevokedAt, s => s.RevokedAt), token) != 1)
                throw new ForbiddenException("permission_denied", "Operator session revoked.");
            if (!await permissions.HasAsync(principal, PlatformPermission.OperationsRead, token)) throw new ForbiddenException("permission_denied", "Admin permission changed.");
            if (!await db.Users.AsNoTracking().AnyAsync(u => u.Id == operatorId && u.SystemRole == SystemRole.Admin
                    && u.Status == UserStatus.Active && u.EmailVerifiedAt != null && !u.MustChangePassword, token))
                throw new ConflictException("operator_not_ready", "An active ordinary Admin is required.");
            if (await db.SupportCases.Where(c => c.Id == caseId).ExecuteUpdateAsync(s => s.SetProperty(c => c.Status, c => c.Status), token) != 1)
                throw new NotFoundException("case_not_found", "Case not found.");
            var ticket = await db.SupportCases.AsNoTracking().SingleAsync(c => c.Id == caseId, token);
            if (ticket.LinkedUserId != subject) throw new ConflictException("case_subject_changed", "Case subject changed; re-evaluate assignment.");
            if (!await db.Users.AsNoTracking().AnyAsync(u => u.Id == ticket.LinkedUserId && u.SystemRole == SystemRole.User, token))
                throw new ForbiddenException("protected_case_subject", "Ordinary User case required.");
            if (ticket.Status is CaseStatus.Closed or CaseStatus.Rejected or CaseStatus.Resolved) throw new ConflictException("case_closed", "Closed case cannot be assigned.");
            if (ticket.AssignedAdminId == operatorId) return true;
            await db.SupportCases.Where(c => c.Id == caseId).ExecuteUpdateAsync(s => s.SetProperty(c => c.AssignedAdminId, (Guid?)operatorId), token);
            var actorName = await db.Users.AsNoTracking().Where(u => u.Id == actor).Select(u => u.FullName).SingleAsync(token);
            db.CaseHistories.Add(new CaseHistory { CaseId = caseId, CaseKind = CaseKind.Support, ActorId = actor,
                ActorRole = "Admin", ActorDisplayNameSnapshot = actorName, Action = "V1Assigned", Message = purpose });
            audit.Add(actor, "V1_CASE_ASSIGNED", "SupportCase", caseId.ToString(), purpose, new { Previous = ticket.AssignedAdminId, AssignedAdminId = operatorId });
            await db.SaveChangesAsync(token);
            await notifications.CreateAsync(operatorId, "AdminTaskAssigned", "Case assigned", "A support case was assigned to you.", new { CaseId = caseId }, token);
            return true;
        }, ct);
    }

    public async Task<IReadOnlyList<V1CaseTaskDto>> QueueAsync(ClaimsPrincipal principal, string reason, int page, int pageSize, CancellationToken ct)
    {
        var purpose = InputBoundary.RequiredText(reason, 1000, "Reason"); AdminAuditWriter.RequireReason(purpose); InputBoundary.Page(page, pageSize);
        if (page - 1 > int.MaxValue / pageSize) throw new AppException("invalid_pagination", "Page too large.");
        if (!await permissions.HasAsync(principal, PlatformPermission.OperationsRead, ct)) throw new ForbiddenException("permission_denied", "Operator required.");
        var actor = Guid.Parse(principal.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var rows = await db.SupportCases.AsNoTracking().Where(c => (c.AssignedAdminId == actor || c.AssignedAdminId == null)
                && c.Status != CaseStatus.Closed && c.Status != CaseStatus.Rejected && c.Status != CaseStatus.Resolved)
            .OrderBy(c => c.CreatedAt).ThenBy(c => c.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(c => new V1CaseTaskDto(c.Id, c.TicketNumber, c.Status.ToString(), c.Priority, c.AssignedAdminId)).ToListAsync(ct);
        if (!await permissions.HasAsync(principal, PlatformPermission.OperationsRead, ct)) throw new ForbiddenException("permission_denied", "Operator permission changed.");
        audit.Add(actor, "V1_CASE_QUEUE_READ", "SupportQueue", null, purpose, new { Count = rows.Count, Page = page }); await db.SaveChangesAsync(ct); return rows;
    }
}
