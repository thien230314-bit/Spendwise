using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Domain.Admin;
using SpendWise.Domain.Finance;
using SpendWise.Domain.Identity;

namespace SpendWise.Application.Features.Administration;

public sealed partial class AdminA1Service
{
    public Task<A1Page<A1FeedItem>> SourceRecordsAsync(ClaimsPrincipal principal, string metric, DateOnly? date, A1ListQuery query, CancellationToken ct) =>
        WithOperatorAsync(principal, "SOURCE_RECORDS", async (actor, token) =>
        {
            var (_, start, end) = Window(date); var cases = ScopedCases(principal, true);
            IQueryable<A1FeedItem> rows;
            switch (metric)
            {
                case "usersCreated":
                    rows = db.Users.AsNoTracking().Where(u => u.SystemRole == SystemRole.User && u.CreatedAt >= start && u.CreatedAt < end && cases.Any(c => c.LinkedUserId == u.Id))
                        .Select(u => new A1FeedItem { Id = u.Id, Source = "Users", Code = "SourceRecord", Status = "Recorded", At = u.CreatedAt, ActorId = null,
                            CaseId = cases.Where(c => c.LinkedUserId == u.Id).OrderBy(c => c.Id).Select(c => (Guid?)c.Id).FirstOrDefault(), CanOpenCase = true });
                    break;
                case "postingStatuses":
                case "completedDebitVolumes":
                    rows = db.LedgerTransactions.AsNoTracking().Where(t => t.CreatedAt >= start && t.CreatedAt < end && (metric != "completedDebitVolumes" || t.Status == LedgerTransactionStatus.Completed)
                            && cases.Any(c => db.FinancialTransactions.Any(h => h.Id == c.LinkedTransactionId && h.OwnerUserId == c.LinkedUserId && h.LedgerTransactionId == t.Id)))
                        .Select(t => new A1FeedItem { Id = t.Id, Source = "LedgerTransactions", Code = "SourceRecord",
                            Status = t.Status == LedgerTransactionStatus.Completed ? "Completed" : t.Status == LedgerTransactionStatus.Failed ? "Failed" : t.Status == LedgerTransactionStatus.Reversed ? "Reversed" : "Pending",
                            At = t.CreatedAt, ActorId = null, CaseId = cases.Where(c => db.FinancialTransactions.Any(h => h.Id == c.LinkedTransactionId && h.OwnerUserId == c.LinkedUserId && h.LedgerTransactionId == t.Id)).OrderBy(c => c.Id).Select(c => (Guid?)c.Id).FirstOrDefault(), CanOpenCase = true });
                    break;
                case "supportCasesCreated":
                    rows = cases.Where(c => c.CreatedAt >= start && c.CreatedAt < end)
                        .Select(c => new A1FeedItem { Id = c.Id, Source = "SupportCases", Code = "SourceRecord", Status = "Recorded", At = c.CreatedAt, ActorId = null, CaseId = c.Id, CanOpenCase = true });
                    break;
                case "disputesCreated":
                    rows = db.Disputes.AsNoTracking().Where(d => d.CreatedAt >= start && d.CreatedAt < end && cases.Any(c => c.LinkedUserId == d.UserId && c.LinkedTransactionId == d.LinkedTransactionId))
                        .Select(d => new A1FeedItem { Id = d.Id, Source = "Disputes", Code = "SourceRecord", Status = "Recorded", At = d.CreatedAt, ActorId = null,
                            CaseId = cases.Where(c => c.LinkedUserId == d.UserId && c.LinkedTransactionId == d.LinkedTransactionId).OrderBy(c => c.Id).Select(c => (Guid?)c.Id).FirstOrDefault(), CanOpenCase = true });
                    break;
                case "groupsCreated":
                    var v1 = db.V1Funds.AsNoTracking().Where(f => f.CreatedAt >= start && f.CreatedAt < end
                            && cases.Any(c => db.V1FundMemberships.Any(m => m.FundId == f.Id && m.UserId == c.LinkedUserId)))
                        .Select(f => new A1FeedItem { Id = f.Id, Source = "V1Funds", Code = "SourceRecord", Status = "Recorded", At = f.CreatedAt, ActorId = null,
                            CaseId = cases.Where(c => db.V1FundMemberships.Any(m => m.FundId == f.Id && m.UserId == c.LinkedUserId)).OrderBy(c => c.Id).Select(c => (Guid?)c.Id).FirstOrDefault(), CanOpenCase = true });
                    var legacy = db.Groups.AsNoTracking().Where(g => g.CreatedAt >= start && g.CreatedAt < end && !db.V1Funds.Any(f => f.Id == g.Id)
                            && cases.Any(c => db.GroupMembers.Any(m => m.GroupId == g.Id && m.UserId == c.LinkedUserId)))
                        .Select(g => new A1FeedItem { Id = g.Id, Source = "Groups", Code = "SourceRecord", Status = "Recorded", At = g.CreatedAt, ActorId = null,
                            CaseId = cases.Where(c => db.GroupMembers.Any(m => m.GroupId == g.Id && m.UserId == c.LinkedUserId)).OrderBy(c => c.Id).Select(c => (Guid?)c.Id).FirstOrDefault(), CanOpenCase = true });
                    rows = v1.Concat(legacy); break;
                default: throw new AppException("invalid_overview_metric", "Unsupported overview source metric.");
            }
            return await FeedAsync(principal, actor, rows, query, ["Users", "LedgerTransactions", "SupportCases", "Disputes", "V1Funds", "Groups"], "SOURCE_RECORDS", token);
        }, ct);
}
