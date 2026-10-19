using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Domain.Admin;
using SpendWise.Domain.Groups;

namespace SpendWise.Application.Features.Administration;

public sealed record A3FeedQuery(string Reason, int Page = 1, int PageSize = 20, string? Status = null, Guid? ParentId = null);
public sealed record A3Member
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public string Source { get; init; } = "";
    public string Role { get; init; } = "";
    public int? Seat { get; init; }
    public bool? CanSpend { get; init; }
    public DateTime JoinedAt { get; init; }
    public DateTime? LeftAt { get; init; }
}
public sealed record A3Event
{
    public Guid Id { get; init; }
    public Guid? ActorId { get; init; }
    public Guid? SubjectId { get; init; }
    public string Source { get; init; } = "";
    public string Action { get; init; } = "";
    public DateTime At { get; init; }
    public Guid? ReferenceId { get; init; }
    public string Outcome { get; init; } = "Recorded";
    public string? Detail { get; init; }
}
public sealed record A3Case(Guid Id, string TicketNumber, string Status, string LinkSource, string DetailEndpoint, string ActionEndpoint,
    Guid? LinkedTransactionId, int EvidenceCount);

public sealed partial class AdminA3Service
{
    public Task<A2Page<A3Member>> MembersAsync(ClaimsPrincipal principal, Guid caseId, Guid fundId, A3FeedQuery q, CancellationToken ct) =>
        WithFund(principal, caseId, fundId, q.Reason, "MEMBERS", async (_, fund, token) =>
        {
            ValidateFeed(q); if (q.ParentId != null || q.Status is not (null or "Current" or "Historical")) throw new AppException("invalid_member_filter", "Use Current or Historical.");
            IQueryable<A3Member> rows;
            if (fund.Code != null)
            {
                var owner = await db.V1Funds.Where(f => f.Id == fundId).Select(f => f.OwnerUserId).SingleAsync(token);
                rows = db.V1FundMemberships.AsNoTracking().Where(m => m.FundId == fundId).Select(m => new A3Member { Id = m.Id, UserId = m.UserId, Source = "V1", Role = m.LeftAt == null && m.UserId == owner ? "Owner" : "Member", Seat = m.Seat, CanSpend = fund.Kind == "Fixed" ? m.CanSpend : null, JoinedAt = m.JoinedAt, LeftAt = m.LeftAt });
            }
            else rows = db.GroupMembers.AsNoTracking().Where(m => m.GroupId == fundId).Select(m => new A3Member { Id = m.Id, UserId = m.UserId, Source = "LegacyCurrent", Role = m.Role.ToString(), JoinedAt = m.JoinedAt });
            if (q.Status == "Current") rows = rows.Where(m => m.LeftAt == null);
            if (q.Status == "Historical") rows = rows.Where(m => m.LeftAt != null);
            var total = await rows.CountAsync(token); var items = await rows.OrderBy(m => m.JoinedAt).ThenBy(m => m.Id).Skip(Skip(q)).Take(q.PageSize).ToListAsync(token);
            return new A2Page<A3Member>(clock.UtcNow, q.Page, q.PageSize, total, items.Select(m => m with { JoinedAt = Utc(m.JoinedAt), LeftAt = m.LeftAt is {} left ? Utc(left) : null }).ToList());
        }, ct);

    public Task<A2Page<A3Event>> ActivityAsync(ClaimsPrincipal principal, Guid caseId, Guid fundId, A3FeedQuery q, CancellationToken ct) =>
        WithFund(principal, caseId, fundId, q.Reason, "ACTIVITY", async (_, fund, token) =>
        {
            ValidateFeed(q); if (q.ParentId != null || q.Status != null) throw new AppException("invalid_activity_filter", "Activity is a typed event feed.");
            // Financial events belong to their own ledger/reference feed, never this operational timeline.
            string[] money = ["Contributed", "ContributionPosted", "FundContributed", "ContributionRecorded", "FundRequestCreated", "FundRequestReviewedFinanceBlocked", "FundRequestRejected", "FundRequestCancelled"];
            IQueryable<A3Event> rows = fund.Code != null
                ? db.V1FundActivities.AsNoTracking().Where(a => a.FundId == fundId && !money.Contains(a.Event))
                    .Select(a => new A3Event { Id = a.Id, ActorId = a.ActorUserId, SubjectId = a.SubjectUserId, Source = "V1Activity", Action = a.Event, At = a.CreatedAt, ReferenceId = a.ReferenceId,
                        Detail = a.Event == "SpendingPermissionChanged" && (a.Detail == "ON" || a.Detail == "OFF") ? a.Detail : null })
                : db.GroupActivities.AsNoTracking().Where(a => a.GroupId == fundId && a.Type != GroupActivityType.Expense && a.Type != GroupActivityType.FundContribution && a.Type != GroupActivityType.FundExpense && a.Type != GroupActivityType.FundRoundPaid)
                    .Select(a => new A3Event { Id = a.Id, ActorId = a.ActorUserId, Source = "LegacyActivity", Action = a.Type.ToString(), At = a.CreatedAt, ReferenceId = a.GroupId });
            var total = await rows.CountAsync(token); var items = await rows.OrderByDescending(a => a.At).ThenBy(a => a.Id).Skip(Skip(q)).Take(q.PageSize).ToListAsync(token);
            return new A2Page<A3Event>(clock.UtcNow, q.Page, q.PageSize, total, items.Select(a => a with { At = Utc(a.At) }).ToList());
        }, ct);

    public Task<A2Page<A3Case>> CasesAsync(ClaimsPrincipal principal, Guid caseId, Guid fundId, A3FeedQuery q, CancellationToken ct) =>
        WithFund(principal, caseId, fundId, q.Reason, "CASES", async (ticket, fund, token) =>
        {
            ValidateFeed(q); if (q.ParentId != null || q.Status != null) throw new AppException("invalid_case_filter", "This view returns the authorized linked case only.");
            var linked = fund.Code != null && (await db.V1Funds.AnyAsync(f => f.Id == fundId && f.OwnerResponseCaseId == caseId, token)
                || await db.V1FundActivities.AnyAsync(a => a.FundId == fundId && a.ReferenceId == caseId && a.Event == "OwnerResponseEscalated", token));
            if (!linked && ticket.LinkedTransactionId is {} transactionId)
            {
                var ledger = await db.FinancialTransactions.AsNoTracking().Where(t => t.Id == transactionId && t.OwnerUserId == ticket.LinkedUserId)
                    .Select(t => (Guid?)t.LedgerTransactionId).SingleOrDefaultAsync(token);
                linked = ledger != null && (fund.Code != null
                    ? await db.V1FundContributions.AnyAsync(c => c.FundId == fundId && c.UserId == ticket.LinkedUserId && c.LedgerTransactionId == ledger, token)
                    : await db.FundTransactions.AnyAsync(t => t.GroupId == fundId && t.UserId == ticket.LinkedUserId && t.LedgerTransactionId == ledger, token));
            }
            var evidence = linked ? await db.CaseAttachments.CountAsync(a => a.CaseOwnerUserId == ticket.LinkedUserId
                && a.History.CaseKind == CaseKind.Support && a.History.CaseId == caseId, token) : 0;
            // No description/code substring matching: membership alone does not make a case a fund report.
            var rows = linked ? new[] { new A3Case(ticket.Id, ticket.TicketNumber, ticket.Status.ToString(), "PersistedFundCaseReferenceOrSubjectPosting", $"/api/v1/admin/support/{caseId}/detail", $"/api/v1/admin/support/{caseId}/actions", ticket.LinkedTransactionId, evidence) } : [];
            return new A2Page<A3Case>(clock.UtcNow, q.Page, q.PageSize, rows.Length, rows.Skip(Skip(q)).Take(q.PageSize).ToList());
        }, ct);

    private static void ValidateFeed(A3FeedQuery q)
    {
        InputBoundary.Page(q.Page, q.PageSize); if (q.Page - 1 > int.MaxValue / q.PageSize) throw new AppException("invalid_pagination", "Page too large.");
    }
    private static int Skip(A3FeedQuery q) => (q.Page - 1) * q.PageSize;
}
