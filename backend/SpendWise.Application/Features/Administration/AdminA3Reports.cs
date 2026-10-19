using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Domain.Admin;

namespace SpendWise.Application.Features.Administration;

public sealed record A3ReportHistory(Guid Id, Guid ActorId, string ActorRole, string Action, string? OldStatus, string? NewStatus, DateTime At, int EvidenceCount);

public sealed partial class AdminA3Service
{
    private sealed record ReportRow
    {
        public Guid Id { get; init; }
        public string Source { get; init; } = "";
        public string Number { get; init; } = "";
        public string Status { get; init; } = "";
        public string Link { get; init; } = "";
        public Guid? TransactionId { get; init; }
        public DateTime At { get; init; }
        public int Evidence { get; init; }
    }

    private IQueryable<ReportRow> ReportRows(ClaimsPrincipal principal, SupportCase current, Guid fundId, bool v1)
    {
        var actor = Guid.Parse(principal.FindFirst(ClaimTypes.NameIdentifier)!.Value); var super = principal.IsInRole("SuperAdmin"); var subject = current.LinkedUserId;
        var postingIds = v1 ? db.V1FundContributions.Where(c => c.FundId == fundId && c.UserId == subject).Select(c => c.LedgerTransactionId)
            : db.FundTransactions.Where(t => t.GroupId == fundId && (t.UserId == subject || t.Type == SpendWise.Domain.Groups.FundTransactionType.Expense) && t.LedgerTransactionId != null).Select(t => t.LedgerTransactionId!.Value)
                .Concat(db.GroupContributions.Where(c => c.GroupId == fundId && c.UserId == subject).Select(c => c.LedgerTransactionId))
                .Concat(db.LedgerTransactions.Where(p => p.BusinessType == "GroupFundExpense" && db.GroupExpenses.Any(e => e.Id == p.BusinessId && e.GroupId == fundId && e.PaidByType == SpendWise.Domain.Groups.GroupExpensePaidByType.Fund)).Select(p => p.Id))
                .Concat(db.GroupRefunds.Where(r => r.UserId == subject && r.LedgerTransactionId != null && db.GroupDeletionRequests.Any(d => d.Id == r.GroupDeletionRequestId && d.GroupId == fundId)).Select(r => r.LedgerTransactionId!.Value));
        var transactions = db.FinancialTransactions.Where(t => t.OwnerUserId == subject && postingIds.Contains(t.LedgerTransactionId));
        var cases = db.SupportCases.AsNoTracking().Where(c => c.LinkedUserId == subject && (c.AssignedAdminId == actor || super && c.EscalatedAt != null && c.EscalationReviewerId == actor)
            && c.Status != CaseStatus.Closed && c.Status != CaseStatus.Resolved && c.Status != CaseStatus.Rejected
            && (v1 && (db.V1Funds.Any(f => f.Id == fundId && f.OwnerResponseCaseId == c.Id)
                || db.V1FundActivities.Any(a => a.FundId == fundId && a.Event == "OwnerResponseEscalated" && a.ReferenceId == c.Id))
                || transactions.Any(t => t.Id == c.LinkedTransactionId)));
        var support = cases.Select(c => new ReportRow { Id = c.Id, Source = "Support", Number = c.TicketNumber, Status = c.Status.ToString(), At = c.CreatedAt,
            Link = transactions.Any(t => t.Id == c.LinkedTransactionId) ? "SubjectFinancialTransactionAndFundPosting" : "PersistedFundCaseReference", TransactionId = transactions.Any(t => t.Id == c.LinkedTransactionId) ? c.LinkedTransactionId : null,
            Evidence = db.CaseAttachments.Count(a => a.CaseOwnerUserId == subject && a.History.CaseKind == CaseKind.Support && a.History.CaseId == c.Id) });
        // Disputes have no independent assignee field. Only the exact transaction of an authorized linked support task grants derivative read scope.
        var disputes = db.Disputes.AsNoTracking().Where(d => d.UserId == subject && transactions.Any(t => t.Id == d.LinkedTransactionId)
            && cases.Any(c => c.LinkedTransactionId == d.LinkedTransactionId || c.Id == current.Id && c.LinkedTransactionId == null)).Select(d => new ReportRow { Id = d.Id, Source = "Dispute", Number = "", Status = d.Status.ToString(), At = d.CreatedAt,
                TransactionId = d.LinkedTransactionId, Link = "AuthorizedFundOrTransactionTaskExactSubjectPosting",
                Evidence = db.CaseAttachments.Count(a => a.CaseOwnerUserId == subject && a.History.CaseKind == CaseKind.Dispute && a.History.CaseId == d.Id) });
        return support.Concat(disputes);
    }

    public Task<A2Page<A3ReportHistory>> ReportHistoryAsync(ClaimsPrincipal principal, Guid caseId, Guid fundId, string source, Guid reportId, A3FeedQuery q, CancellationToken ct) =>
        WithFund(principal, caseId, fundId, q.Reason, "REPORT_HISTORY", async (ticket, fund, token) =>
        {
            ValidateFeed(q); if (source is not ("Support" or "Dispute") || q.ParentId != null || q.Status != null) throw new AppException("invalid_report_filter", "Use Support or Dispute and paging.");
            if (!await ReportRows(principal, ticket, fundId, fund.Code != null).AnyAsync(r => r.Id == reportId && r.Source == source, token))
                throw new ForbiddenException("report_scope_denied", "An assigned support task and structured fund/transaction linkage are required.");
            var kind = source == "Support" ? CaseKind.Support : CaseKind.Dispute;
            var rows = db.CaseHistories.AsNoTracking().Where(h => h.CaseId == reportId && h.CaseKind == kind);
            var total = await rows.CountAsync(token); var items = await rows.OrderByDescending(h => h.CreatedAt).ThenBy(h => h.Id).Skip(Skip(q)).Take(q.PageSize)
                .Select(h => new A3ReportHistory(h.Id, h.ActorId, h.ActorRole, h.Action, h.OldStatus, h.NewStatus, h.CreatedAt,
                    h.Attachments.Count(a => a.CaseOwnerUserId == ticket.LinkedUserId))).ToListAsync(token);
            // Other support tasks can be reassigned independently of the root task. Revalidate before releasing their history.
            if (!await ReportRows(principal, ticket, fundId, fund.Code != null).AnyAsync(r => r.Id == reportId && r.Source == source, token))
                throw new ForbiddenException("report_scope_denied", "Report scope changed.");
            return new A2Page<A3ReportHistory>(clock.UtcNow, q.Page, q.PageSize, total, items.Select(h => h with { At = Utc(h.At) }).ToList());
        }, ct);
}
