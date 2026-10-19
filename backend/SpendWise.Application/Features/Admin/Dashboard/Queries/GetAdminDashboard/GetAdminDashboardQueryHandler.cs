using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Admin;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Administration;
using SpendWise.Domain.Admin;
using SpendWise.Domain.AI;
using SpendWise.Domain.Banking;

namespace SpendWise.Application.Features.Admin.Dashboard.Queries.GetAdminDashboard;

// Keep legacy aggregate fields, but never retain a legacy authorization or counting bypass.
public sealed class GetAdminDashboardQueryHandler(ISpendWiseDbContext db, IDatabaseHealthChecker healthChecker, AdminA1Service overview)
    : IRequestHandler<GetAdminDashboardQuery, AdminDashboardDto>
{
    public Task<AdminDashboardDto> Handle(GetAdminDashboardQuery query, CancellationToken ct)
    {
        var principal = query.Principal ?? throw new ForbiddenException();
        return overview.WithOperatorAsync(principal, "LEGACY_SUMMARY", async (_, token) =>
        {
            var s = await overview.SummaryAsync(principal, null, token);
            if (query.From is { } from && from != s.FromUtc || query.To is { } to && to != s.ToUtc)
                throw new AppException("invalid_dashboard_range", "Legacy dashboard supports today's Vietnam half-open UTC range only; use the V1 overview date filter.");
            var count = s.PostingStatuses.Sum(x => x.Count);
            var completed = s.PostingStatuses.Where(x => x.Status == "Completed").Sum(x => x.Count);
            return new AdminDashboardDto(s.TotalOrdinaryUsers, s.ActiveOrdinaryUsers, s.UsersCreated, count,
                s.CompletedDebitVolumes.Where(v => v.Currency == "VND").Sum(v => v.Amount),
                await db.BankDirectPayments.CountAsync(p => p.ContextType == BankDirectPaymentContextType.Transfer && p.Status == BankDirectPaymentStatus.Completed && p.CompletedAt >= s.FromUtc && p.CompletedAt < s.ToUtc, token),
                count == 0 ? 0 : Math.Round((decimal)completed / count * 100, 2),
                s.PostingStatuses.Where(x => x.Status == "Failed").Sum(x => x.Count), s.PostingStatuses.Where(x => x.Status == "Pending").Sum(x => x.Count),
                await db.RiskCases.CountAsync(r => r.ReviewStatus == ReviewStatus.Pending, token),
                await db.Disputes.CountAsync(c => c.Status != CaseStatus.Closed && c.Status != CaseStatus.Resolved && c.Status != CaseStatus.Rejected, token),
                await db.SupportCases.CountAsync(c => c.Status != CaseStatus.Closed && c.Status != CaseStatus.Resolved && c.Status != CaseStatus.Rejected, token),
                await db.AiMessages.CountAsync(m => m.Role == AiMessageRole.User && m.CreatedAt >= s.FromUtc && m.CreatedAt < s.ToUtc, token),
                await healthChecker.CanConnectAsync(token) ? "Healthy" : "Unhealthy", s);
        }, ct);
    }
}
