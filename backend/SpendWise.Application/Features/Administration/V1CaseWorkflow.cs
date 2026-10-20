using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Admin;
using SpendWise.Application.Features.Admin.Cases;
using SpendWise.Application.Notifications;
using SpendWise.Domain.Admin;
using SpendWise.Domain.Identity;

namespace SpendWise.Application.Features.Administration;

public sealed record V1CaseAction(string Action, string Message, string ExpectedStatus, string? Status = null);
public sealed record V1CaseHistoryDto(Guid Id, Guid ActorId, string Role, string Action, string Message, DateTime CreatedAt);
public sealed record V1CaseDetail(Guid Id, string Subject, string Description, string Status, Guid? AssignedAdminId,
    Guid? EscalationReviewerId, DateTime? EscalatedAt, DateTime? ReviewedAt, string? EscalationReason, string? EscalationGuidance, IReadOnlyList<V1CaseHistoryDto> History);
public sealed record V1InvestigatedWallet(Guid Id, string Type, string Status, string Currency, decimal AvailableBalance);
public sealed record V1InvestigatedSession(Guid Id, string DeviceName, DateTime CreatedAt, DateTime LastSeenAt, DateTime? RevokedAt);
public sealed record V1CaseAccounts(IReadOnlyList<V1InvestigatedWallet> Wallets, IReadOnlyList<V1InvestigatedSession> Sessions);

public sealed class V1CaseWorkflow(ISpendWiseDbContext db, PlatformPermissionService permissions, AdminAuditWriter audit,
    INotificationService notifications, IClock clock)
{
    public Task<V1CaseDetail> GetAsync(ClaimsPrincipal principal, Guid id, string reason, CancellationToken ct) =>
        WithCaseAsync(principal, id, reason, false, async (ticket, actor, token) =>
        {
            audit.Add(actor, "V1_CASE_READ", "SupportCase", id.ToString(), reason, null);
            await db.SaveChangesAsync(token);
            var history = await db.CaseHistories.AsNoTracking().Where(h => h.CaseId == id && h.CaseKind == CaseKind.Support)
                .OrderBy(h => h.CreatedAt).ThenBy(h => h.Id).Select(h => new V1CaseHistoryDto(h.Id, h.ActorId, h.ActorRole, h.Action, h.Message, h.CreatedAt)).ToListAsync(token);
            return new V1CaseDetail(id, ticket.Subject, ticket.Description, ticket.Status.ToString(), ticket.AssignedAdminId,
                ticket.EscalationReviewerId, ticket.EscalatedAt, ticket.EscalationReviewedAt, ticket.EscalationReason, ticket.EscalationGuidance, history);
        }, ct);

    public Task<V1CaseAccounts> AccountsAsync(ClaimsPrincipal principal, Guid id, string reason, CancellationToken ct) =>
        WithCaseAsync(principal, id, reason, false, async (ticket, actor, token) =>
        {
            var wallets = await db.Wallets.AsNoTracking().Where(w => w.OwnerUserId == ticket.LinkedUserId).OrderBy(w => w.CreatedAt)
                .Select(w => new V1InvestigatedWallet(w.Id, w.Type.ToString(), w.Status.ToString(), w.Currency, w.AvailableBalance)).ToListAsync(token);
            var sessions = await db.UserSessions.AsNoTracking().Where(s => s.UserId == ticket.LinkedUserId).OrderByDescending(s => s.CreatedAt).Take(100)
                .Select(s => new V1InvestigatedSession(s.Id, s.DeviceName, s.CreatedAt, s.LastSeenAt, s.RevokedAt)).ToListAsync(token);
            audit.Add(actor, "V1_CASE_ACCOUNTS_READ", "SupportCase", id.ToString(), reason, new { Wallets = wallets.Count, Sessions = sessions.Count });
            await db.SaveChangesAsync(token); return new V1CaseAccounts(wallets, sessions);
        }, ct);

    public Task<bool> ActAsync(ClaimsPrincipal principal, Guid id, V1CaseAction request, CancellationToken ct)
    {
        var message = InputBoundary.RequiredText(request.Message, request.Action == "guidance" ? 2000 : 1000, "Message");
        AdminAuditWriter.RequireReason(message);
        if (request.Action is not ("reply" or "status" or "escalate" or "claim" or "guidance")) throw new AppException("invalid_case_action", "Unsupported case action.");
        return WithCaseAsync(principal, id, message[..Math.Min(1000, message.Length)], request.Action == "claim", async (ticket, actor, token) =>
        {
            if (ticket.Status.ToString() != request.ExpectedStatus) throw new ConflictException("concurrent_case_update", "Case status changed.");
            var oldStatus = ticket.Status;
            if (request.Action is "escalate" or "claim" or "guidance")
                throw new ForbiddenException("escalation_disabled", "SuperAdmin escalation is unavailable in V1.0.");
            switch (request.Action)
            {
                case "status":
                    if (ticket.AssignedAdminId != actor) throw new ForbiddenException("case_access_denied", "Assigned Admin must execute the case decision.");
                    var next = CaseWorkflowMappings.ParseSupport(request.Status ?? "");
                    CaseWorkflowMappings.EnsureSupportTransition(ticket.Status, next);
                    CaseWorkflowMappings.SupportStatusMessage(next, message);
                    ticket.Status = next; break;
            }
            ticket.LatestReplyAt = clock.UtcNow;
            var name = await db.Users.AsNoTracking().Where(u => u.Id == actor).Select(u => u.FullName).SingleAsync(token);
            db.CaseHistories.Add(new CaseHistory { CaseId = id, CaseKind = CaseKind.Support, ActorId = actor,
                ActorDisplayNameSnapshot = name, ActorRole = "Admin", Action = "V1_" + request.Action,
                Message = request.Action is "escalate" or "claim" or "guidance" ? "Case escalation updated." : message,
                OldStatus = oldStatus.ToString(), NewStatus = ticket.Status.ToString() });
            audit.Add(actor, "V1_CASE_" + request.Action.ToUpperInvariant(), "SupportCase", id.ToString(), message[..Math.Min(1000, message.Length)], null);
            await db.SaveChangesAsync(token);
            // Internal escalation notes stay internal; customer notifications contain no investigation text.
            var recipient = request.Action is "claim" or "guidance" ? ticket.AssignedAdminId : request.Action is "reply" or "status" ? ticket.LinkedUserId : (Guid?)null;
            if (recipient is { } user) await notifications.CreateAsync(user, request.Action is "claim" or "guidance" ? "AdminTaskAssigned" : "SupportUpdated", "Case updated", "Your case has an update.", new { CaseId = id }, token);
            return true;
        }, ct);
    }

    internal async Task<T> WithCaseAsync<T>(ClaimsPrincipal principal, Guid id, string reason, bool claim,
        Func<SupportCase, Guid, CancellationToken, Task<T>> action, CancellationToken ct)
    {
        AdminAuditWriter.RequireReason(InputBoundary.RequiredText(reason, 1000, "Reason"));
        if (!await permissions.HasAsync(principal, PlatformPermission.SensitiveRead, ct)) throw new ForbiddenException("permission_denied", "Operator required.");
        var actor = Guid.Parse(principal.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var session = Guid.Parse(principal.FindFirst("sid")!.Value);
        try { return await db.ExecuteInTransactionAsync(async token =>
        {
            var subject = await db.SupportCases.AsNoTracking().Where(c => c.Id == id).Select(c => (Guid?)c.LinkedUserId).SingleOrDefaultAsync(token)
                ?? throw new NotFoundException("case_not_found", "Case not found.");
            foreach (var user in new[] { actor, subject }.Distinct().Order())
                if (!await db.AcquireGroupAdmissionLockAsync(user, token)) throw new NotFoundException("user_not_found", "User not found.");
            if (await db.UserSessions.Where(s => s.Id == session && s.UserId == actor && s.RevokedAt == null).ExecuteUpdateAsync(s => s.SetProperty(s => s.RevokedAt, s => s.RevokedAt), token) != 1
                || !await permissions.HasAsync(principal, PlatformPermission.SensitiveRead, token)) throw new ForbiddenException("permission_denied", "Operator session changed.");
            if (await db.SupportCases.Where(c => c.Id == id).ExecuteUpdateAsync(s => s.SetProperty(c => c.Status, c => c.Status), token) != 1)
                throw new NotFoundException("case_not_found", "Case not found.");
            var ticket = await db.SupportCases.SingleAsync(c => c.Id == id, token);
            if (db is DbContext context) await context.Entry(ticket).ReloadAsync(token);
            if (ticket.LinkedUserId != subject || !await db.Users.AsNoTracking().AnyAsync(u => u.Id == subject && u.SystemRole == SystemRole.User, token))
                throw new ForbiddenException("case_access_denied", "Ordinary User subject required.");
            if (ticket.Status is CaseStatus.Resolved or CaseStatus.Rejected or CaseStatus.Closed)
                throw new ForbiddenException("case_access_denied", "An assigned active case is required.");
            if (ticket.Category == "V1FundDispute" && ticket.AssignedAdminId == null)
            {
                ticket.AssignedAdminId = actor;
                if (ticket.Status is CaseStatus.New or CaseStatus.Open)
                    ticket.Status = CaseStatus.Investigating;
            }
            var hasScope = ticket.AssignedAdminId == actor || await permissions.HasSupportScopeAsync(principal, id, subject, token);
            if (!hasScope)
                throw new ForbiddenException("case_access_denied", "An assigned active case is required.");
            return await action(ticket, actor, token);
        }, ct); }
        catch (AppException ex)
        {
            if (db is DbContext context) context.ChangeTracker.Clear();
            audit.Add(actor, "V1_CASE_ACCESS_DENIED", "SupportCase", id.ToString(), reason, new { ex.Code });
            await db.SaveChangesAsync(ct); throw;
        }
    }
}
