using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Domain.Admin;
using SpendWise.Domain.Banking;
using SpendWise.Domain.Finance;
using SpendWise.Domain.Notifications;

namespace SpendWise.Application.Features.Administration;

public sealed record A1FeedItem
{
    public Guid Id { get; init; }
    public string Source { get; init; } = "";
    public string Code { get; init; } = "";
    public string Status { get; init; } = "";
    public DateTime At { get; init; }
    public Guid? ActorId { get; init; }
    public Guid? CaseId { get; init; }
    public bool CanOpenCase { get; init; }
    public bool IsFraudConclusion => false;
    public string? CaseEndpoint => CaseId is { } id && CanOpenCase ? $"/api/v1/admin/support/{id}/detail" : null;
}

public sealed partial class AdminA1Service
{
    public Task<A1Page<A1FeedItem>> AlertsAsync(ClaimsPrincipal principal, A1ListQuery query, CancellationToken ct) => WithOperatorAsync(principal, "ALERTS", async (actor, token) =>
    {
        var now = clock.UtcNow; var cases = ScopedCases(principal, true);
        var risks = db.RiskCases.AsNoTracking().Where(r => r.ReviewStatus == ReviewStatus.Pending && cases.Any(c => c.LinkedUserId == r.UserId))
            .Select(r => new A1FeedItem { Id = r.Id, Source = "RiskCase", Code = "RiskSignal", Status = "Pending", At = r.CreatedAt,
                ActorId = null, CaseId = cases.Where(c => c.LinkedUserId == r.UserId).OrderBy(c => c.Id).Select(c => (Guid?)c.Id).FirstOrDefault(), CanOpenCase = true });
        var failures = db.FinancialTransactions.AsNoTracking().Where(t => t.Status == FinancialTransactionStatus.Failed
                && cases.Any(c => c.LinkedUserId == t.OwnerUserId && c.LinkedTransactionId == t.Id))
            .Select(t => new A1FeedItem { Id = t.Id, Source = "FinancialTransaction", Code = "HistoryFailure", Status = "Failed", At = t.CreatedAt,
                ActorId = null, CaseId = cases.Where(c => c.LinkedUserId == t.OwnerUserId && c.LinkedTransactionId == t.Id).OrderBy(c => c.Id).Select(c => (Guid?)c.Id).FirstOrDefault(), CanOpenCase = true });
        var funds = cases.Where(c => c.Category == "V1FundGovernance" || c.Category == "group_fund" || c.Category == "group_expense")
            .Select(c => new A1FeedItem { Id = c.Id, Source = "SupportCase", Code = "FundGovernanceCase", Status = c.Status == CaseStatus.New ? "New" : c.Status == CaseStatus.WaitingForUser ? "WaitingForUser" : c.Status == CaseStatus.Investigating ? "Investigating" : "Open", At = c.CreatedAt,
                ActorId = null, CaseId = c.Id, CanOpenCase = true });
        var notices = db.Notifications.AsNoTracking().Where(n => n.UserId == actor && (n.Type == NotificationType.SecurityAlert || n.Type == NotificationType.BankIntegrationProblem
                || n.Type == NotificationType.MaintenanceChanged || n.Type == NotificationType.EmergencyAnnouncement))
            .Select(n => new A1FeedItem { Id = n.Id, Source = "Notification", Code = n.Type == NotificationType.SecurityAlert ? "SecurityNotice" : n.Type == NotificationType.BankIntegrationProblem ? "BankIntegrationNotice" : n.Type == NotificationType.EmergencyAnnouncement ? "EmergencyNotice" : "MaintenanceNotice", Status = n.ReadAt == null ? "Unread" : "Read", At = n.CreatedAt,
                ActorId = null, CaseId = null, CanOpenCase = false });
        var maintenance = db.V1MaintenancePlans.AsNoTracking().Where(p => p.CancelledAt == null && p.ClosedAt == null)
            .Select(p => new A1FeedItem { Id = p.Id, Source = "Maintenance", Code = "Maintenance", Status = p.StartsAt > now ? "Scheduled" : "Active", At = p.StartsAt,
                ActorId = null, CaseId = null, CanOpenCase = false });
        var bank = db.BankIntegrationEvents.AsNoTracking().Where(e => e.ExternalBankAccountId != null && (e.ProcessingStatus == BankIntegrationEventStatus.Failed || e.ProcessingStatus == BankIntegrationEventStatus.Unmatched)
                && db.BankConnections.Where(b => b.Provider == e.Provider && b.ExternalAccountId == e.ExternalBankAccountId).Select(b => b.UserId).Distinct().Count() == 1
                && cases.Any(c => db.BankConnections.Any(b => b.UserId == c.LinkedUserId && b.Provider == e.Provider && b.ExternalAccountId == e.ExternalBankAccountId)))
            .Select(e => new A1FeedItem { Id = e.Id, Source = "BankIntegrationEvent", Code = "BankProcessingSignal", Status = e.ProcessingStatus == BankIntegrationEventStatus.Failed ? "Failed" : "Unmatched", At = e.ReceivedAt,
                ActorId = null, CaseId = cases.Where(c => db.BankConnections.Any(b => b.UserId == c.LinkedUserId && b.Provider == e.Provider && b.ExternalAccountId == e.ExternalBankAccountId)).OrderBy(c => c.Id).Select(c => (Guid?)c.Id).FirstOrDefault(), CanOpenCase = true });
        return await FeedAsync(principal, actor, risks.Concat(failures).Concat(funds).Concat(notices).Concat(maintenance).Concat(bank), query,
            ["RiskCase", "FinancialTransaction", "SupportCase", "Notification", "Maintenance", "BankIntegrationEvent"], "ALERTS", token);
    }, ct);

    public Task<A1Page<A1FeedItem>> ActivityAsync(ClaimsPrincipal principal, A1ListQuery query, CancellationToken ct) => WithOperatorAsync(principal, "ACTIVITY", async (actor, token) =>
    {
        var cases = ScopedCases(principal, false);
        string[] historyActions = ["created", "reply", "provide_info", "request_info", "status_change", "V1Assigned", "V1_reply", "V1_status", "V1_escalate", "V1_claim", "V1_guidance"];
        var history = db.CaseHistories.AsNoTracking().Where(h => h.CaseKind == CaseKind.Support && historyActions.Contains(h.Action) && cases.Any(c => c.Id == h.CaseId))
            .Select(h => new A1FeedItem { Id = h.Id, Source = "CaseHistory", Code = h.Action, Status = "Recorded", At = h.CreatedAt, ActorId = h.ActorId, CaseId = h.CaseId,
                CanOpenCase = cases.Any(c => c.Id == h.CaseId && c.Status != CaseStatus.Closed && c.Status != CaseStatus.Resolved && c.Status != CaseStatus.Rejected) });
        string[] protectionActions = ["REVOKE_SESSIONS", "LOCK_USER", "UNLOCK_USER", "RESET_USER_PASSWORD", "RESET_USER_SECURITY_PIN"];
        var protection = db.AuditLogs.AsNoTracking().Where(a => a.ActorUserId == actor && a.TargetType == "User" && protectionActions.Contains(a.Action)
                && cases.Any(c => c.LinkedUserId.ToString().ToLower() == a.TargetId!.ToLower()))
            .Select(a => new A1FeedItem { Id = a.Id, Source = "ProtectionAudit", Code = a.Action, Status = "Recorded", At = a.CreatedAt, ActorId = a.ActorUserId,
                CaseId = cases.Where(c => c.LinkedUserId.ToString().ToLower() == a.TargetId!.ToLower()).OrderBy(c => c.Id).Select(c => (Guid?)c.Id).FirstOrDefault(), CanOpenCase = false });
        string[] operationsActions = ["V1_MAINTENANCE_SCHEDULED", "V1_MAINTENANCE_ACTIVATED", "V1_MAINTENANCE_EMERGENCY", "V1_MAINTENANCE_EXTENDED", "V1_MAINTENANCE_RESCHEDULED", "V1_MAINTENANCE_CLOSED", "V1_MAINTENANCE_CANCELLED"];
        var operations = db.AuditLogs.AsNoTracking().Where(a => a.TargetType == "V1MaintenancePlan" && operationsActions.Contains(a.Action)
                && db.V1MaintenancePlans.Any(p => p.Id.ToString().ToLower() == a.TargetId!.ToLower()))
            .Select(a => new A1FeedItem { Id = a.Id, Source = "MaintenanceAudit", Code = a.Action, Status = "Recorded", At = a.CreatedAt,
                ActorId = a.Action == "V1_MAINTENANCE_ACTIVATED" ? null : a.ActorUserId, CaseId = null, CanOpenCase = false });
        // CaseHistory is the canonical case event; its accompanying audit row is not a second event.
        return await FeedAsync(principal, actor, history.Concat(protection).Concat(operations), query,
            ["CaseHistory", "ProtectionAudit", "MaintenanceAudit"], "ACTIVITY", token);
    }, ct);

    private async Task<A1Page<A1FeedItem>> FeedAsync(ClaimsPrincipal principal, Guid actor, IQueryable<A1FeedItem> rows, A1ListQuery query,
        string[] sources, string dataset, CancellationToken ct)
    {
        ValidatePage(query);
        if (query.Due is not null) throw new AppException("invalid_overview_deadline", "Deadline filtering is available only for assigned tasks.");
        if (query.Source is { } source)
        {
            if (!sources.Contains(source, StringComparer.Ordinal)) throw new AppException("invalid_overview_source", "Unsupported source.");
            rows = rows.Where(r => r.Source == source);
        }
        if (query.Status is { } status)
        {
            if (!new[] { "Pending", "Completed", "Reversed", "Failed", "New", "Open", "Investigating", "WaitingForUser", "Unread", "Read", "Scheduled", "Active", "Unmatched", "Recorded" }.Contains(status, StringComparer.Ordinal))
                throw new AppException("invalid_overview_status", "Unsupported status.");
            rows = rows.Where(r => r.Status == status);
        }
        if (query.CaseId is { } caseId) rows = rows.Where(r => r.CaseId == caseId);
        var total = await rows.CountAsync(ct);
        var items = await rows.OrderByDescending(r => r.At).ThenBy(r => r.Source).ThenBy(r => r.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(ct);
        await AuditCasesAsync(actor, principal, items.Where(r => r.CaseId != null).Select(r => r.CaseId!.Value), dataset, ct);
        return new(clock.UtcNow, query.Page, query.PageSize, total, items.Select(i => i with { At = Utc(i.At) }).ToList());
    }
}
