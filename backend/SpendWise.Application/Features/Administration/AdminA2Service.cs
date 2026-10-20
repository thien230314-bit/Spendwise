using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Admin;
using SpendWise.Application.Features.Authentication;
using SpendWise.Domain.Admin;
using SpendWise.Domain.Identity;
using SpendWise.Domain.Finance;
using SpendWise.Application.Notifications;

namespace SpendWise.Application.Features.Administration;

public sealed record A2Query(string Reason, string? Search = null, string? Status = null, int Page = 1, int PageSize = 20);
public sealed record A2Page<T>(DateTime ObservedAtUtc, int Page, int PageSize, int Total, IReadOnlyList<T> Items);
public sealed record A2User(Guid Id, string SpendId, string DisplayName, string MaskedEmail, string Status, DateTime CreatedAt);
public sealed record A2Profile(A2User User, Guid CaseId, IReadOnlyList<V1InvestigatedWallet> Wallets);
public sealed record A2Session(Guid Id, string DeviceName, DateTime CreatedAt, DateTime LastSeenAt, DateTime? RevokedAt, string State);
public sealed record A2RevokeRequest(string Reason, string SecurityPin, Guid? SessionId = null);
public sealed record A2StatusRequest(string Status, string Reason, string SecurityPin);
public sealed record A2Restrictions(string LoginStatus, DateTime? LastLoginAt, int RevokedSessions, bool EmergencyOutgoingControlAvailable,
    string OutgoingControlStatus, IReadOnlyList<A2WalletState> Wallets, string Provenance);
public sealed record A2WalletState(Guid Id, string Type, string Status);

public sealed partial class AdminA2Service(ISpendWiseDbContext db, PlatformPermissionService permissions, V1CaseWorkflow workflow,
    AdminAuditWriter audit, AdminSecurityGuard security, AuthenticationSupport authentication, IClock clock, INotificationService notifications)
{
    public async Task<A2Page<A2User>> SearchAsync(ClaimsPrincipal principal, A2Query query, CancellationToken ct)
    {
        Validate(query);
        if (!await permissions.HasAsync(principal, PlatformPermission.OperationsRead, ct))
        { await DeniedAsync(principal, null, null, "OperatorDenied", ct); throw new ForbiddenException(); }
        var actor = Guid.Parse(principal.FindFirst(ClaimTypes.NameIdentifier)!.Value); var session = Guid.Parse(principal.FindFirst("sid")!.Value);
        try { return await db.ExecuteInTransactionAsync(async token =>
        {
            if (!await db.AcquireGroupAdmissionLockAsync(actor, token)
                || await db.UserSessions.Where(s => s.Id == session && s.UserId == actor && s.RevokedAt == null).ExecuteUpdateAsync(s => s.SetProperty(s => s.RevokedAt, s => s.RevokedAt), token) != 1
                || !await permissions.HasAsync(principal, PlatformPermission.OperationsRead, token)) throw new ForbiddenException();
            var users = db.Users.AsNoTracking().Where(u => u.SystemRole == SystemRole.User);
            if (query.Search is { } search && !string.IsNullOrWhiteSpace(search))
            {
                var key = InputBoundary.RequiredText(search, 254, "Search").ToUpperInvariant(); var isId = Guid.TryParse(search, out var id);
                users = users.Where(u => isId && u.Id == id || u.NormalizedEmail == key || u.NormalizedSpendId == key);
            }
            if (query.Status is { } state)
            {
                if (!Enum.TryParse<UserStatus>(state, out var status) || status.ToString() != state || !Enum.IsDefined(status)) throw new AppException("invalid_user_status", "Unsupported status.");
                users = users.Where(u => u.Status == status);
            }
            var total = await users.CountAsync(token);
            var rows = await users.OrderByDescending(u => u.CreatedAt).ThenBy(u => u.Id).Skip(Skip(query)).Take(query.PageSize)
                .Select(u => new { u.Id, u.SpendId, u.FullName, u.Email, u.Status, u.CreatedAt }).ToListAsync(token);
            audit.Add(actor, "A2_DIRECTORY_READ", "UserDirectory", null, query.Reason, new { Count = rows.Count, query.Page, Role = principal.FindFirst(ClaimTypes.Role)!.Value, Outcome = "Allowed" });
            await db.SaveChangesAsync(token);
            return new A2Page<A2User>(clock.UtcNow, query.Page, query.PageSize, total, rows.Select(u => new A2User(u.Id, u.SpendId, u.FullName, Mask(u.Email), u.Status.ToString(), Utc(u.CreatedAt))).ToList());
        }, ct); }
        catch (AppException ex)
        {
            if (db is DbContext context) context.ChangeTracker.Clear();
            await DeniedAsync(principal, null, null, ex.Code, ct); throw;
        }
    }

    public Task<A2Profile> ProfileAsync(ClaimsPrincipal principal, Guid caseId, Guid userId, string reason, CancellationToken ct) =>
        WithSubject(principal, caseId, userId, reason, "PROFILE", async token =>
        {
            var user = await db.Users.AsNoTracking().SingleAsync(u => u.Id == userId, token);
            var wallets = await db.Wallets.AsNoTracking().Where(w => w.OwnerUserId == userId && w.Status != WalletStatus.Closed)
                .OrderBy(w => w.Type).ThenBy(w => w.Id).Select(w => new V1InvestigatedWallet(w.Id, w.Type.ToString(), w.Status.ToString(), w.Currency, w.AvailableBalance)).Take(4).ToListAsync(token);
            if (wallets.Count > 3 || wallets.Count(w => w.Type == "Main") > 1 || wallets.Count(w => w.Type != "Main") > 2)
                throw new ConflictException("wallet_structure_requires_review", "Stored wallet structure requires review; no money was changed.");
            return new A2Profile(new(user.Id, user.SpendId, user.FullName, Mask(user.Email), user.Status.ToString(), Utc(user.CreatedAt)), caseId, wallets);
        }, ct);

    public Task<A2Page<A2Session>> SessionsAsync(ClaimsPrincipal principal, Guid caseId, Guid userId, A2Query query, CancellationToken ct) =>
        WithSubject(principal, caseId, userId, query.Reason, "SESSIONS", async token =>
        {
            Validate(query); RejectSearch(query); var now = clock.UtcNow; var sessions = db.UserSessions.AsNoTracking().Where(s => s.UserId == userId);
            if (query.Status is {} status)
            {
                sessions = status switch {
                    "Revoked" => sessions.Where(s => s.RevokedAt != null),
                    "RefreshEligible" => sessions.Where(s => s.RevokedAt == null && db.RefreshTokens.Any(r => r.UserSessionId == s.Id && r.RevokedAt == null && r.ExpiresAt > now)),
                    "NoLiveRefreshEvidence" => sessions.Where(s => s.RevokedAt == null && !db.RefreshTokens.Any(r => r.UserSessionId == s.Id && r.RevokedAt == null && r.ExpiresAt > now)),
                    _ => throw new AppException("invalid_session_status", "Unsupported session status.") };
            }
            var total = await sessions.CountAsync(token);
            var rows = await sessions.OrderByDescending(s => s.CreatedAt).ThenBy(s => s.Id).Skip(Skip(query)).Take(query.PageSize)
                .Select(s => new { s.Id, s.DeviceName, s.CreatedAt, s.LastSeenAt, s.RevokedAt,
                    HasRefresh = db.RefreshTokens.Any(r => r.UserSessionId == s.Id && r.RevokedAt == null && r.ExpiresAt > now) }).ToListAsync(token);
            return new A2Page<A2Session>(now, query.Page, query.PageSize, total, rows.Select(s => new A2Session(s.Id, s.DeviceName, Utc(s.CreatedAt), Utc(s.LastSeenAt), s.RevokedAt is {} at ? Utc(at) : null,
                s.RevokedAt != null ? "Revoked" : s.HasRefresh ? "RefreshEligible" : "NoLiveRefreshEvidence")).ToList());
        }, ct);

    public Task<A2Restrictions> RestrictionsAsync(ClaimsPrincipal principal, Guid caseId, Guid userId, string reason, CancellationToken ct) =>
        WithSubject(principal, caseId, userId, reason, "RESTRICTIONS", async token =>
        {
            var user = await db.Users.AsNoTracking().SingleAsync(u => u.Id == userId, token);
            var wallets = await db.Wallets.AsNoTracking().Where(w => w.OwnerUserId == userId).OrderBy(w => w.Id)
                .Select(w => new A2WalletState(w.Id, w.Type.ToString(), w.Status.ToString())).ToListAsync(token);
            var restrictionLevel = user.Status switch
            {
                UserStatus.Restricted => "Level 1: Financial Restriction (Outbound debits blocked, read/login preserved)",
                UserStatus.Locked => "Level 2: Account Lock (Login blocked, sessions revoked)",
                UserStatus.Suspended => "Suspended (Pending security review)",
                _ => "None (Active)"
            };
            return new A2Restrictions(user.Status.ToString(), user.LastLoginAt is {} at ? Utc(at) : null,
                await db.UserSessions.CountAsync(s => s.UserId == userId && s.RevokedAt != null, token),
                user.Status == UserStatus.Restricted || user.Status == UserStatus.Locked,
                restrictionLevel, wallets,
                "Decision 2 approved: Level 1 Financial Restriction allows login, balances, incoming/refunds while blocking outbound debits; Level 2 Account Lock disables login/sessions.");
        }, ct);

    public async Task RevokeAsync(ClaimsPrincipal principal, Guid caseId, Guid userId, A2RevokeRequest request, CancellationToken ct)
    {
        await RequireScopeAsync(principal, caseId, userId, request.Reason, ct);
        var actor = Guid.Parse(principal.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var role = Enum.Parse<SystemRole>(principal.FindFirst(ClaimTypes.Role)!.Value);
        try { await security.VerifyAsync(actor, request.SecurityPin, ct, role); }
        catch (AppException ex) { await DeniedAsync(principal, caseId, userId, ex.Code, ct); throw; }
        await WithSubject(principal, caseId, userId, request.Reason, "REVOKE", async token =>
        {
            if (request.SessionId is {} id && !await db.UserSessions.AnyAsync(s => s.Id == id && s.UserId == userId, token)) throw new NotFoundException("session_not_found", "Session not found.");
            var active = db.UserSessions.Where(s => s.UserId == userId && s.RevokedAt == null && (request.SessionId == null || s.Id == request.SessionId));
            var hasSessions = await active.AnyAsync(token);
            var hasTokens = await db.RefreshTokens.AnyAsync(r => r.UserId == userId && r.RevokedAt == null && (request.SessionId == null || r.UserSessionId == request.SessionId), token);
            if (!hasSessions && !hasTokens) return false;
            if (request.SessionId is {} selected) await authentication.RevokeSessionAsync(userId, selected, token);
            else await authentication.RevokeAllSessionsAsync(userId, token);
            audit.Add(actor, "A2_SESSION_REVOKED", "User", userId.ToString(), request.Reason, new { CaseId = caseId, request.SessionId, Outcome = "Revoked" });
            return true;
        }, ct);
    }

    public async Task ChangeStatusAsync(ClaimsPrincipal principal, Guid caseId, Guid userId, A2StatusRequest request, CancellationToken ct)
    {
        await RequireScopeAsync(principal, caseId, userId, request.Reason, ct);
        if (request.Status is not ("Active" or "Restricted" or "Locked"))
            throw new AppException("invalid_user_status", "Use Active, Restricted or Locked.");
        var actor = Guid.Parse(principal.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        await security.VerifyAsync(actor, request.SecurityPin, ct, SystemRole.Admin);
        await WithSubject(principal, caseId, userId, request.Reason, "STATUS", async token =>
        {
            var user = await AdminSecurityGuard.RequireOrdinaryTargetAsync(db, userId, token);
            if (user.Status is not (UserStatus.Active or UserStatus.Restricted or UserStatus.Locked))
                throw new ConflictException("account_status_requires_review", "This account is outside the approved restriction workflow.");
            var next = Enum.Parse<UserStatus>(request.Status);
            if (user.Status == next) return false;
            var previous = user.Status;
            user.Status = next;
            if (next == UserStatus.Locked) await authentication.RevokeAllSessionsAsync(userId, token);
            audit.Add(actor, "A2_ACCOUNT_STATUS_CHANGED", "User", userId.ToString(), request.Reason,
                new { CaseId = caseId, PreviousStatus = previous.ToString(), NewStatus = request.Status });
            await notifications.CreateAsync(userId, "SecurityAlert", "Account status changed",
                $"Your account status is now {request.Status}. Reason: {request.Reason}", new { CaseId = caseId, Status = request.Status }, token);
            return true;
        }, ct);
    }

    private async Task<T> WithSubject<T>(ClaimsPrincipal principal, Guid caseId, Guid userId, string reason, string dataset, Func<CancellationToken, Task<T>> read, CancellationToken ct)
    {
        await RequireScopeAsync(principal, caseId, userId, reason, ct);
        reason = InputBoundary.RequiredText(reason, 1000, "Reason");
        return await workflow.WithCaseAsync(principal, caseId, reason, false, async (ticket, actor, token) =>
        {
            if (ticket.LinkedUserId != userId) throw new ForbiddenException("case_access_denied", "Case subject mismatch.");
            var result = await read(token);
            audit.Add(actor, "A2_" + dataset + "_ACCESS", "SupportCase", caseId.ToString(), reason, new { SubjectUserId = userId, Dataset = dataset, Role = principal.FindFirst(ClaimTypes.Role)!.Value, Outcome = "Allowed" });
            await db.SaveChangesAsync(token); return result;
        }, ct);
    }

    private async Task RequireScopeAsync(ClaimsPrincipal principal, Guid caseId, Guid userId, string reason, CancellationToken ct)
    {
        try { AdminAuditWriter.RequireReason(InputBoundary.RequiredText(reason, 1000, "Reason")); }
        catch (AppException) { await DeniedAsync(principal, caseId, userId, "InvalidReason", ct); throw; }
        if (!await permissions.HasSupportScopeAsync(principal, caseId, userId, ct)
            || !await permissions.SupportSubjects(principal, caseId, userId).AnyAsync(ct))
        { await DeniedAsync(principal, caseId, userId, "ScopeDenied", ct); throw new ForbiddenException("case_access_denied", "An assigned active ordinary User case is required."); }
    }

    private async Task DeniedAsync(ClaimsPrincipal principal, Guid? caseId, Guid? userId, string code, CancellationToken ct)
    {
        Guid? actor = Guid.TryParse(principal.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;
        db.AuditLogs.Add(new AuditLog { ActorUserId = actor, Action = "A2_ACCESS_DENIED", TargetType = "SupportCase", TargetId = caseId?.ToString(),
            Reason = "User management access refused", MetadataJson = System.Text.Json.JsonSerializer.Serialize(new { SubjectUserId = userId, Outcome = code }) });
        await db.SaveChangesAsync(ct);
    }

    private static void Validate(A2Query query)
    {
        AdminAuditWriter.RequireReason(InputBoundary.RequiredText(query.Reason, 1000, "Reason")); InputBoundary.Page(query.Page, query.PageSize);
        if (query.Page - 1 > int.MaxValue / query.PageSize) throw new AppException("invalid_pagination", "Page is too large.");
    }
    private static int Skip(A2Query query) => (query.Page - 1) * query.PageSize;
    private static DateTime Utc(DateTime date) => DateTime.SpecifyKind(date, DateTimeKind.Utc);
    private static string Mask(string email) { var at = email.LastIndexOf('@'); return at > 0 ? email[..1] + "***" + email[at..] : "***"; }
}
