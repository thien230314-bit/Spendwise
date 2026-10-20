using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Domain.Admin;
using SpendWise.Domain.Groups;

namespace SpendWise.Application.Features.Administration;

public sealed record A2Group
{
    public Guid Id { get; init; }
    public string Source { get; init; } = "";
    public string Name { get; init; } = "";
    public string? Code { get; init; }
    public string State { get; init; } = "";
    public DateTime? JoinedAt { get; init; }
    public DateTime? LeftAt { get; init; }
    public bool Dissolved { get; init; }
}
public sealed record A2Friend(Guid UserId, string SpendId, DateTime AcceptedAt);
public sealed record A2HistoryItem
{
    public Guid Id { get; init; }
    public string Source { get; init; } = "";
    public string Action { get; init; } = "";
    public Guid? ActorId { get; init; }
    public DateTime At { get; init; }
    public string Reason { get; init; } = "";
    public string Outcome { get; init; } = "";
    public Guid ReferenceId { get; init; }
}

public sealed partial class AdminA2Service
{
    public Task<A2Page<A2Group>> GroupsAsync(ClaimsPrincipal principal, Guid caseId, Guid userId, A2Query query, CancellationToken ct) =>
        WithSubject(principal, caseId, userId, query.Reason, "GROUPS", async token =>
        {
            Validate(query); RejectSearch(query);
            var memberships = db.V1FundMemberships.AsNoTracking().Where(m => m.UserId == userId);
            var v1 = from m in memberships join fund in db.V1Funds.AsNoTracking() on m.FundId equals fund.Id
                select new A2Group { Id = fund.Id, Source = "V1", Name = fund.Name, Code = fund.Code,
                    State = m.LeftAt == null ? "Current" : "Historical", JoinedAt = m.JoinedAt, LeftAt = m.LeftAt, Dissolved = fund.DissolvedAt != null };
            var legacy = db.Groups.AsNoTracking().Where(g => !memberships.Any(m => m.FundId == g.Id)
                && (db.GroupMembers.Any(m => m.GroupId == g.Id && m.UserId == userId)
                    || db.GroupInvites.Any(i => i.GroupId == g.Id && i.InvitedUserId == userId && i.Status == InviteStatus.Accepted)
                    || db.GroupActivities.Any(a => a.GroupId == g.Id && a.ActorUserId == userId && a.Type == GroupActivityType.MemberJoined)))
                .Select(g => new A2Group { Id = g.Id, Source = "Legacy", Name = g.Name, Code = null,
                    State = db.GroupMembers.Any(m => m.GroupId == g.Id && m.UserId == userId) ? "Current" : "Historical",
                    JoinedAt = db.GroupMembers.Where(m => m.GroupId == g.Id && m.UserId == userId).Select(m => (DateTime?)m.JoinedAt).FirstOrDefault(),
                    LeftAt = null, Dissolved = g.Status == GroupStatus.Closed });
            var rows = v1.Concat(legacy);
            if (query.Status is {} status) { if (status is not ("Current" or "Historical")) throw new AppException("invalid_membership_status", "Use Current or Historical."); rows = rows.Where(r => r.State == status); }
            var total = await rows.CountAsync(token); var page = await rows.OrderBy(r => r.Source).ThenBy(r => r.Id).Skip(Skip(query)).Take(query.PageSize).ToListAsync(token);
            return new A2Page<A2Group>(clock.UtcNow, query.Page, query.PageSize, total, page.Select(r => r with { JoinedAt = r.JoinedAt is {} joined ? Utc(joined) : null, LeftAt = r.LeftAt is {} left ? Utc(left) : null }).ToList());
        }, ct);

    public Task<A2Page<A2Friend>> FriendsAsync(ClaimsPrincipal principal, Guid caseId, Guid userId, A2Query query, CancellationToken ct) =>
        WithSubject(principal, caseId, userId, query.Reason, "FRIENDS", async token =>
        {
            Validate(query); RejectSearch(query); if (query.Status is not null) throw new AppException("invalid_relation_filter", "Friends view contains active relations only.");
            var links = db.Friendships.AsNoTracking().Where(f => (f.LowerUserId == userId || f.HigherUserId == userId) && f.EndedAt == null);
            var rows = from link in links join user in db.Users.AsNoTracking() on (link.LowerUserId == userId ? link.HigherUserId : link.LowerUserId) equals user.Id
                where user.SystemRole == SpendWise.Domain.Identity.SystemRole.User
                orderby link.AcceptedAt descending, link.Id
                select new A2Friend(user.Id, user.SpendId, link.AcceptedAt);
            return new A2Page<A2Friend>(clock.UtcNow, query.Page, query.PageSize, await rows.CountAsync(token),
                (await rows.Skip(Skip(query)).Take(query.PageSize).ToListAsync(token)).Select(r => r with { AcceptedAt = Utc(r.AcceptedAt) }).ToList());
        }, ct);

    public Task<A2Page<A2HistoryItem>> HistoryAsync(ClaimsPrincipal principal, Guid caseId, Guid userId, A2Query query, CancellationToken ct) =>
        WithSubject(principal, caseId, userId, query.Reason, "HISTORY", async token =>
        {
            Validate(query); RejectSearch(query); if (query.Status is not null) throw new AppException("invalid_history_filter", "History has typed source outcomes, not a shared status enum.");
            var actor = Guid.Parse(principal.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var cases = db.SupportCases.Where(c => c.LinkedUserId == userId && c.AssignedAdminId == actor);
            string[] actions = ["LOCK_USER", "UNLOCK_USER", "RESTRICT_USER", "A2_ACCOUNT_STATUS_CHANGED", "REVOKE_SESSIONS", "A2_SESSION_REVOKED", "RESET_USER_PASSWORD", "RESET_USER_SECURITY_PIN"];
            var history = db.CaseHistories.AsNoTracking().Where(h => h.CaseKind == CaseKind.Support && cases.Any(c => c.Id == h.CaseId))
                .Select(h => new A2HistoryItem { Id = h.Id, Source = "CaseHistory", Action = h.Action, ActorId = h.ActorId, At = h.CreatedAt,
                    Reason = h.Action == "V1_escalate" || h.Action == "V1_claim" || h.Action == "V1_guidance" ? "Internal escalation event; use authorized case detail" : h.Message,
                    Outcome = h.NewStatus ?? "Recorded", ReferenceId = h.CaseId });
            var decisions = db.AuditLogs.AsNoTracking().Where(a => a.TargetType == "User" && a.TargetId != null && a.TargetId.ToLower() == userId.ToString().ToLower() && actions.Contains(a.Action))
                .Select(a => new A2HistoryItem { Id = a.Id, Source = "ProtectionAudit", Action = a.Action, ActorId = a.ActorUserId, At = a.CreatedAt, Reason = a.Reason, Outcome = "Recorded", ReferenceId = userId });
            var walletDecisions = from entry in db.AuditLogs.AsNoTracking() join wallet in db.Wallets.AsNoTracking()
                on entry.TargetId!.ToLower() equals wallet.Id.ToString().ToLower()
                where wallet.OwnerUserId == userId && entry.TargetType == "Wallet" && (entry.Action == "FREEZE_WALLET" || entry.Action == "UNFREEZE_WALLET")
                select new A2HistoryItem { Id = entry.Id, Source = "LegacyWalletAudit", Action = entry.Action, ActorId = entry.ActorUserId, At = entry.CreatedAt,
                    Reason = entry.Reason, Outcome = "Recorded", ReferenceId = wallet.Id };
            var recovery = db.AccountRecoveryRequests.AsNoTracking().Where(r => r.ResolvedUserId == userId && r.ReviewedByAdminId == actor
                    && !db.AccountRecoveryReviews.Any(v => v.AccountRecoveryRequestId == r.Id))
                .Select(r => new A2HistoryItem { Id = r.Id, Source = "AccountRecovery", Action = "RecoveryStatus", ActorId = r.ReviewedByAdminId, At = r.ReviewedAt ?? r.CreatedAt,
                    Reason = "Verified account linkage; private evidence withheld", Outcome = r.Status.ToString(), ReferenceId = r.Id });
            var reviews = from review in db.AccountRecoveryReviews.AsNoTracking() join request in db.AccountRecoveryRequests.AsNoTracking() on review.AccountRecoveryRequestId equals request.Id
                where request.ResolvedUserId == userId && review.AdminId == actor
                select new A2HistoryItem { Id = review.Id, Source = "AccountRecoveryReview", Action = review.Action, ActorId = review.AdminId, At = review.CreatedAt,
                    Reason = "Account recovery review; private evidence withheld", Outcome = "Recorded", ReferenceId = request.Id };
            var all = history.Concat(decisions).Concat(walletDecisions).Concat(recovery).Concat(reviews);
            var total = await all.CountAsync(token); var rows = await all.OrderByDescending(h => h.At).ThenBy(h => h.Source).ThenBy(h => h.Id).Skip(Skip(query)).Take(query.PageSize).ToListAsync(token);
            return new A2Page<A2HistoryItem>(clock.UtcNow, query.Page, query.PageSize, total, rows.Select(r => r with { At = Utc(r.At) }).ToList());
        }, ct);

    private static void RejectSearch(A2Query query)
    { if (query.Search is not null) throw new AppException("invalid_a2_filter", "Search is supported only by the basic directory."); }
}
