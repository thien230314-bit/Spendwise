using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Admin;
using SpendWise.Domain.Admin;
using SpendWise.Domain.Finance;
using SpendWise.Domain.Identity;

namespace SpendWise.Application.Features.Administration;

public sealed record A1ListQuery(int Page = 1, int PageSize = 20, string? Status = null, string? Source = null, Guid? CaseId = null, string? Due = null);
public sealed record A1Page<T>(DateTime ObservedAtUtc, int Page, int PageSize, int Total, IReadOnlyList<T> Items);
public sealed record A1StatusCount(string Status, int Count);
public sealed record A1Volume(string Currency, decimal Amount);
public sealed record A1MetricSource(string Code, string Source, string TimestampField, string Aggregation, DateTime FromUtc, DateTime ToUtc)
{
    public string Endpoint => $"/api/v1/admin/overview/sources/{Code}?date={DateOnly.FromDateTime(VietnamBusinessTime.ToLocal(FromUtc)).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)}";
    public string DetailScope => "Assigned case subset only; aggregate counts do not grant global detail access";
}
public sealed record A1Summary(DateOnly Date, string TimeZone, DateTime FromUtc, DateTime ToUtc, DateTime ObservedAtUtc,
    int TotalOrdinaryUsers, int ActiveOrdinaryUsers, int UsersCreated, int GroupsCreated, int LegacyGroupRowsCreated, int V1FundRowsCreated,
    int SupportCasesCreated, int DisputesCreated, IReadOnlyList<A1StatusCount> PostingStatuses, IReadOnlyList<A1Volume> CompletedDebitVolumes,
    IReadOnlyList<A1MetricSource> Sources);
public sealed record A1Task(Guid Id, string TicketNumber, string Status, DateTime CreatedAt, DateTime? DueAt, string DeadlineState, string CaseEndpoint, string Method);

public sealed partial class AdminA1Service(ISpendWiseDbContext db, PlatformPermissionService permissions, IClock clock, AdminAuditWriter audit)
{
    public Task<A1Summary> SummaryAsync(ClaimsPrincipal principal, DateOnly? date, CancellationToken ct) => WithOperatorAsync(principal, "SUMMARY", async (actor, token) =>
    {
        var (day, start, end) = Window(date);
        var postings = await db.LedgerTransactions.AsNoTracking().Where(t => t.CreatedAt >= start && t.CreatedAt < end)
            .GroupBy(t => t.Status).Select(g => new { Status = g.Key, Count = g.Count() }).ToListAsync(token);
        // Keep decimal aggregation in the database; never count mirrored display histories as movements.
        var amounts = await db.LedgerEntries.AsNoTracking().Where(e => e.LedgerTransaction.CreatedAt >= start && e.LedgerTransaction.CreatedAt < end
                && e.LedgerTransaction.Status == LedgerTransactionStatus.Completed && e.Direction == LedgerEntryDirection.Debit)
            .GroupBy(e => e.Currency).Select(g => new { Currency = g.Key, Amount = g.Sum(e => e.Amount) }).OrderBy(g => g.Currency).ToListAsync(token);
        var legacy = db.Groups.AsNoTracking().Where(g => g.CreatedAt >= start && g.CreatedAt < end).Select(g => g.Id);
        var v1 = db.V1Funds.AsNoTracking().Where(g => g.CreatedAt >= start && g.CreatedAt < end).Select(g => g.Id);
        var sources = new[] {
            new A1MetricSource("usersCreated", "Users[SystemRole=User]", "CreatedAt", "COUNT DISTINCT Id", start, end),
            new A1MetricSource("groupsCreated", "Groups UNION V1Funds", "CreatedAt", "COUNT DISTINCT Id across both sources; row counts are separate", start, end),
            new A1MetricSource("postingStatuses", "LedgerTransactions", "CreatedAt", "COUNT DISTINCT posting Id by current status; not display-history rows", start, end),
            new A1MetricSource("completedDebitVolumes", "LedgerEntries joined to Completed LedgerTransactions", "LedgerTransaction.CreatedAt", "SUM Debit amount by currency; includes completed reversal postings, not revenue", start, end),
            new A1MetricSource("supportCasesCreated", "SupportCases", "CreatedAt", "COUNT DISTINCT Id", start, end),
            new A1MetricSource("disputesCreated", "Disputes", "CreatedAt", "COUNT DISTINCT Id; separate case kind, not financial volume", start, end) };
        var result = new A1Summary(day, "Asia/Ho_Chi_Minh", start, end, clock.UtcNow,
            await db.Users.CountAsync(u => u.SystemRole == SystemRole.User, token),
            await db.Users.CountAsync(u => u.SystemRole == SystemRole.User && u.Status == UserStatus.Active, token),
            await db.Users.CountAsync(u => u.SystemRole == SystemRole.User && u.CreatedAt >= start && u.CreatedAt < end, token),
            await legacy.Union(v1).CountAsync(token), await legacy.CountAsync(token), await v1.CountAsync(token),
            await db.SupportCases.CountAsync(c => c.CreatedAt >= start && c.CreatedAt < end, token),
            await db.Disputes.CountAsync(c => c.CreatedAt >= start && c.CreatedAt < end, token),
            postings.OrderBy(p => p.Status).Select(p => new A1StatusCount(p.Status.ToString(), p.Count)).ToList(),
            amounts.Select(a => new A1Volume(a.Currency, a.Amount)).ToList(), sources);
        return result with { ObservedAtUtc = clock.UtcNow };
    }, ct);

    public Task<A1Page<A1Task>> TasksAsync(ClaimsPrincipal principal, A1ListQuery query, CancellationToken ct) => WithOperatorAsync(principal, "TASKS", async (actor, token) =>
    {
        ValidatePage(query); var now = clock.UtcNow; var cases = ScopedCases(principal, true);
        if (query.Status is { } status)
        {
            if (!Enum.TryParse<CaseStatus>(status, out var parsed) || !Enum.IsDefined(parsed) || parsed.ToString() != status) throw new AppException("invalid_overview_status", "Unsupported case status.");
            cases = cases.Where(c => c.Status == parsed);
        }
        if (query.Source is not null && query.Source != "SupportCase") throw new AppException("invalid_overview_source", "Tasks currently use assigned SupportCase records.");
        if (query.CaseId is { } id) cases = cases.Where(c => c.Id == id);
        cases = query.Due switch { null => cases, "Overdue" => cases.Where(c => c.DueAt != null && c.DueAt < now),
            "Scheduled" => cases.Where(c => c.DueAt != null && c.DueAt >= now), "NotSet" => cases.Where(c => c.DueAt == null),
            _ => throw new AppException("invalid_overview_deadline", "Use Overdue, Scheduled or NotSet.") };
        var total = await cases.CountAsync(token);
        var rows = await cases.OrderBy(c => c.CreatedAt).ThenBy(c => c.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(c => new { c.Id, c.TicketNumber, c.Status, c.CreatedAt, c.DueAt }).ToListAsync(token);
        await AuditCasesAsync(actor, principal, rows.Select(c => c.Id), "TASKS", token);
        return new A1Page<A1Task>(now, query.Page, query.PageSize, total, rows.Select(c => new A1Task(c.Id, c.TicketNumber, c.Status.ToString(), Utc(c.CreatedAt), c.DueAt is { } due ? Utc(due) : null,
            c.DueAt is null ? "NotSet" : c.DueAt < now ? "Overdue" : "Scheduled", $"/api/v1/admin/support/{c.Id}/detail", "POST")).ToList());
    }, ct);

    private IQueryable<SupportCase> ScopedCases(ClaimsPrincipal principal, bool active)
    {
        var actor = Guid.Parse(principal.FindFirst(ClaimTypes.NameIdentifier)!.Value); var supervisor = principal.IsInRole("SuperAdmin");
        return db.SupportCases.AsNoTracking().Where(c => (c.AssignedAdminId == actor || supervisor && c.EscalatedAt != null && c.EscalationReviewerId == actor)
            && db.Users.Any(u => u.Id == c.LinkedUserId && u.SystemRole == SystemRole.User)
            && (!active || c.Status != CaseStatus.Closed && c.Status != CaseStatus.Resolved && c.Status != CaseStatus.Rejected));
    }
    private static void ValidatePage(A1ListQuery query)
    {
        InputBoundary.Page(query.Page, query.PageSize);
        if (query.Page - 1 > int.MaxValue / query.PageSize) throw new AppException("invalid_pagination", "Page is too large.");
    }
    private static DateTime Utc(DateTime timestamp) => DateTime.SpecifyKind(timestamp, DateTimeKind.Utc);
    private (DateOnly Day, DateTime Start, DateTime End) Window(DateOnly? date)
    {
        var today = DateOnly.FromDateTime(VietnamBusinessTime.ToLocal(clock.UtcNow)); var day = date ?? today;
        if (day.Year < 2 || day.Year == 9999 || day > today) throw new AppException("invalid_overview_date", "Use a supported Vietnam calendar date no later than today.");
        return (day, VietnamBusinessTime.ToUtc(day.ToDateTime(TimeOnly.MinValue)), VietnamBusinessTime.ToUtc(day.AddDays(1).ToDateTime(TimeOnly.MinValue)));
    }
    private async Task AuditCasesAsync(Guid actor, ClaimsPrincipal principal, IEnumerable<Guid> ids, string dataset, CancellationToken ct)
    {
        foreach (var id in ids.Distinct()) audit.Add(actor, "A1_CASE_READ", "SupportCase", id.ToString(), "View assigned operational overview",
            new { Dataset = dataset, Role = principal.FindFirst(ClaimTypes.Role)!.Value, Outcome = "Allowed" });
        await db.SaveChangesAsync(ct);
    }
    internal async Task<T> WithOperatorAsync<T>(ClaimsPrincipal principal, string dataset, Func<Guid, CancellationToken, Task<T>> read, CancellationToken ct)
    {
        if (!await permissions.HasAsync(principal, PlatformPermission.OperationsRead, ct)) throw new ForbiddenException();
        var actor = Guid.Parse(principal.FindFirst(ClaimTypes.NameIdentifier)!.Value); var session = Guid.Parse(principal.FindFirst("sid")!.Value);
        return await db.ExecuteInTransactionAsync(async token =>
        {
            if (!await db.AcquireGroupAdmissionLockAsync(actor, token)
                || await db.UserSessions.Where(s => s.Id == session && s.UserId == actor && s.RevokedAt == null).ExecuteUpdateAsync(s => s.SetProperty(s => s.RevokedAt, s => s.RevokedAt), token) != 1
                || !await permissions.HasAsync(principal, PlatformPermission.OperationsRead, token)) throw new ForbiddenException();
            var result = await read(actor, token);
            audit.Add(actor, "A1_" + dataset + "_READ", "AdminOverview", null, "View operational overview", new { Role = principal.FindFirst(ClaimTypes.Role)!.Value, Outcome = "Allowed" });
            await db.SaveChangesAsync(token); return result;
        }, ct);
    }
}
