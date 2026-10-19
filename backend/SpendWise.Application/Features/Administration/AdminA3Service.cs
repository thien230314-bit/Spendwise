using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Admin;
using SpendWise.Domain.Admin;
using SpendWise.Domain.Groups;
using SpendWise.Domain.Identity;

namespace SpendWise.Application.Features.Administration;

public sealed record A3Query(string Reason, string? Code = null, string? Kind = null, string? Status = null,
    Guid? RelatedUserId = null, DateTime? CreatedFromUtc = null, DateTime? CreatedToUtc = null, int Page = 1, int PageSize = 20);
public sealed record A3Fund
{
    public Guid Id { get; init; }
    public string Name { get; init; } = "";
    public string? Code { get; init; }
    public string Kind { get; init; } = "";
    public string Status { get; init; } = "";
    public DateTime CreatedAt { get; init; }
    public int? MaximumMembers { get; init; }
}
public sealed record A3Detail(A3Fund Fund, Guid? FounderUserId, Guid? OwnerUserId, DateTime? DissolvedAt, long? OwnershipRevision,
    string FinancialExecution, string DissolutionEvidence, bool CountsAsActiveGroup, string LegacyLimit);

public sealed partial class AdminA3Service(ISpendWiseDbContext db, V1CaseWorkflow workflow, AdminAuditWriter audit, IClock clock)
{
    public Task<A2Page<A3Fund>> SearchAsync(ClaimsPrincipal principal, Guid caseId, A3Query query, CancellationToken ct) =>
        WithCase(principal, caseId, query.Reason, "DIRECTORY", async (ticket, token) =>
        {
            Validate(query); var rows = Funds(ticket.LinkedUserId);
            if (query.Code is {} code) { if (code.Length > 32 || !Regex.IsMatch(code, "^SPW[0-9]{6,}$", RegexOptions.CultureInvariant)) throw new AppException("invalid_fund_code", "Use an exact SPW code."); rows = rows.Where(f => f.Code == code); }
            if (query.Kind is {} kind) { if (kind is not ("Fixed" or "Free" or "Legacy")) throw new AppException("invalid_fund_kind", "Use Fixed, Free or Legacy."); rows = kind == "Legacy" ? rows.Where(f => f.Code == null) : rows.Where(f => f.Kind == kind && f.Code != null); }
            if (query.Status is {} status) { if (status is not ("Active" or "Dissolved" or "Suspended" or "DeletionPending")) throw new AppException("invalid_fund_status", "Unsupported state."); rows = rows.Where(f => f.Status == status); }
            if (query.RelatedUserId is {} user && user != ticket.LinkedUserId) throw new ForbiddenException("case_subject_mismatch", "Use the assigned case subject.");
            if (query.CreatedFromUtc is {} start) rows = rows.Where(f => f.CreatedAt >= start);
            if (query.CreatedToUtc is {} end) rows = rows.Where(f => f.CreatedAt < end);
            var total = await rows.CountAsync(token); var items = await rows.OrderByDescending(f => f.CreatedAt).ThenBy(f => f.Id).Skip(Skip(query)).Take(query.PageSize).ToListAsync(token);
            return new A2Page<A3Fund>(clock.UtcNow, query.Page, query.PageSize, total, items.Select(f => f with { CreatedAt = Utc(f.CreatedAt) }).ToList());
        }, ct);

    public Task<A3Detail> DetailAsync(ClaimsPrincipal principal, Guid caseId, Guid fundId, string reason, CancellationToken ct) =>
        WithFund(principal, caseId, fundId, reason, "DETAIL", async (_, fund, token) =>
        {
            if (fund.Code != null)
            {
                var actual = await db.V1Funds.AsNoTracking().SingleAsync(f => f.Id == fundId, token);
                return new A3Detail(fund, actual.FounderUserId, actual.OwnerUserId, actual.DissolvedAt is {} d ? Utc(d) : null, actual.OwnershipRevision,
                    "FINANCE_BLOCKED except existing wallet contributions", "Dissolution marker is not proof of zero treasury or completed refunds", actual.DissolvedAt == null, "Not Legacy");
            }
            var owner = await db.GroupMembers.Where(m => m.GroupId == fundId && m.Role == GroupRole.Owner).Select(m => (Guid?)m.UserId).SingleOrDefaultAsync(token);
            return new A3Detail(fund, null, owner, null, null, "Legacy execution unchanged",
                "Closed status only; no inferred zero balance/refund completion", fund.Status != "Dissolved", "Founder and exact departure intervals were not persisted by Legacy; null means unknown, never reconstructed from current owner");
        }, ct);

    private IQueryable<A3Fund> Funds(Guid subject)
    {
        var v1 = db.V1Funds.AsNoTracking().Where(f => db.V1FundMemberships.Any(m => m.FundId == f.Id && m.UserId == subject))
            .Select(f => new A3Fund { Id = f.Id, Name = f.Name, Code = f.Code, Kind = f.Kind.ToString(), Status = f.DissolvedAt == null ? "Active" : "Dissolved", CreatedAt = f.CreatedAt, MaximumMembers = f.Kind == SpendWise.Domain.V1Groups.V1FundKind.Fixed ? 30 : 6 });
        var legacy = db.Groups.AsNoTracking().Where(g => !db.V1Funds.Any(f => f.Id == g.Id)
            && (db.GroupMembers.Any(m => m.GroupId == g.Id && m.UserId == subject)
                || db.GroupInvites.Any(i => i.GroupId == g.Id && i.InvitedUserId == subject && i.Status == InviteStatus.Accepted)
                || db.GroupActivities.Any(a => a.GroupId == g.Id && a.ActorUserId == subject && a.Type == GroupActivityType.MemberJoined)))
            .Select(g => new A3Fund { Id = g.Id, Name = g.Name, Code = null, Kind = "Legacy:" + g.Type.ToString(), Status = g.Status == GroupStatus.Closed ? "Dissolved" : g.Status.ToString(), CreatedAt = g.CreatedAt, MaximumMembers = null });
        return v1.Concat(legacy);
    }

    private Task<T> WithFund<T>(ClaimsPrincipal principal, Guid caseId, Guid fundId, string reason, string dataset,
        Func<SupportCase, A3Fund, CancellationToken, Task<T>> read, CancellationToken ct) => WithCase(principal, caseId, reason, dataset, async (ticket, token) =>
    {
        var fund = await Funds(ticket.LinkedUserId).SingleOrDefaultAsync(f => f.Id == fundId, token)
            ?? throw new ForbiddenException("fund_scope_denied", "A retained subject membership and assigned case are required.");
        // Existing writers take this root row; hold it while rechecking membership and reading the investigation.
        if (fund.Code != null) await db.V1Funds.Where(f => f.Id == fundId).ExecuteUpdateAsync(s => s.SetProperty(f => f.OwnershipRevision, f => f.OwnershipRevision), token);
        else await db.Groups.Where(g => g.Id == fundId).ExecuteUpdateAsync(s => s.SetProperty(g => g.Status, g => g.Status), token);
        fund = await Funds(ticket.LinkedUserId).SingleOrDefaultAsync(f => f.Id == fundId, token) ?? throw new ForbiddenException("fund_scope_denied", "Membership evidence changed.");
        var result = await read(ticket, fund with { CreatedAt = Utc(fund.CreatedAt) }, token);
        audit.Add(Guid.Parse(principal.FindFirst(ClaimTypes.NameIdentifier)!.Value), "A3_FUND_READ", "Fund", fundId.ToString(), reason,
            new { CaseId = caseId, Dataset = dataset, SubjectUserId = ticket.LinkedUserId });
        return result;
    }, ct);

    private async Task<T> WithCase<T>(ClaimsPrincipal principal, Guid caseId, string reason, string dataset, Func<SupportCase, CancellationToken, Task<T>> read, CancellationToken ct)
    {
        try { AdminAuditWriter.RequireReason(InputBoundary.RequiredText(reason, 1000, "Reason")); }
        catch (AppException) { await Denied(principal, caseId, ct); throw; }
        if (!await new PlatformPermissionService(db).HasAsync(principal, PlatformPermission.SensitiveRead, ct))
        { await Denied(principal, caseId, ct); throw new ForbiddenException("fund_scope_denied", "A current operator session is required."); }
        return await workflow.WithCaseAsync(principal, caseId, reason, false, async (ticket, actor, token) =>
        {
            var result = await read(ticket, token);
            audit.Add(actor, "A3_" + dataset + "_READ", "SupportCase", caseId.ToString(), reason,
                new { SubjectUserId = ticket.LinkedUserId, Role = principal.FindFirst(ClaimTypes.Role)!.Value, Dataset = dataset, Outcome = "Allowed" });
            await db.SaveChangesAsync(token); return result;
        }, ct);
    }
    private async Task Denied(ClaimsPrincipal principal, Guid caseId, CancellationToken ct)
    {
        db.AuditLogs.Add(new AuditLog { ActorUserId = Guid.TryParse(principal.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var actor) ? actor : null,
            Action = "A3_ACCESS_DENIED", TargetType = "SupportCase", TargetId = caseId.ToString(), Reason = "Investigation authorization or reason rejected" }); await db.SaveChangesAsync(ct);
    }
    private static void Validate(A3Query q)
    {
        InputBoundary.Page(q.Page, q.PageSize); if (q.Page - 1 > int.MaxValue / q.PageSize) throw new AppException("invalid_pagination", "Page too large.");
        if (q.CreatedFromUtc is {} from && from.Kind != DateTimeKind.Utc || q.CreatedToUtc is {} to && to.Kind != DateTimeKind.Utc
            || q.CreatedFromUtc >= q.CreatedToUtc) throw new AppException("invalid_date_range", "Use increasing UTC half-open boundaries.");
    }
    private static int Skip(A3Query q) => (q.Page - 1) * q.PageSize;
    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
