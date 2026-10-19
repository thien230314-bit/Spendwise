using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Admin;
using SpendWise.Domain.Admin;

namespace SpendWise.Application.Features.Administration;

public sealed record ScopedFundMembership(Guid FundId, string Code, string Name, string Kind, int Seat,
    DateTime JoinedAt, DateTime? LeftAt, bool Dissolved);

public sealed class ScopedCaseFundReader(ISpendWiseDbContext db, PlatformPermissionService permissions)
{
    public async Task<IReadOnlyList<ScopedFundMembership>> GetAsync(ClaimsPrincipal principal, Guid caseId, Guid subjectId,
        string reason, string? code, int page, int pageSize, CancellationToken ct)
    {
        Guid? actor = Guid.TryParse(principal.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;
        string purpose;
        try
        {
            purpose = InputBoundary.RequiredText(reason, 1000, "Reason"); AdminAuditWriter.RequireReason(purpose);
            InputBoundary.Page(page, pageSize);
            if (page - 1 > int.MaxValue / pageSize) throw new AppException("invalid_pagination", "Page is too large.");
            code = InputBoundary.OptionalText(code, 32, "Fund code");
        }
        catch (AppException)
        {
            await AuditAsync(actor, caseId, subjectId, "Invalid access request", "InvalidRequest", 0, ct); throw;
        }
        var subjects = permissions.SupportSubjects(principal, caseId, subjectId);
        if (!await subjects.AnyAsync(ct))
        {
            await AuditAsync(actor, caseId, subjectId, purpose, "ScopeDenied", 0, ct);
            throw new ForbiddenException("case_access_denied", "An assigned active case is required.");
        }
        // Only this subject's membership intervals, never other members, balances or private fund activity.
        var rows = await (from membership in db.V1FundMemberships.AsNoTracking()
                          join fund in db.V1Funds.AsNoTracking() on membership.FundId equals fund.Id
                          where subjects.Any(u => u.Id == membership.UserId) && (code == null || fund.Code == code)
                          orderby membership.JoinedAt descending, membership.Id
                          select new ScopedFundMembership(fund.Id, fund.Code, fund.Name, fund.Kind.ToString(), membership.Seat,
                              membership.JoinedAt, membership.LeftAt, fund.DissolvedAt != null))
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        if (!await subjects.AnyAsync(ct))
        {
            await AuditAsync(actor, caseId, subjectId, purpose, "ScopeChanged", 0, ct);
            throw new ForbiddenException("case_access_denied", "Case access changed.");
        }
        await AuditAsync(actor, caseId, subjectId, purpose, "Allowed", rows.Count, ct);
        return rows;
    }

    private async Task AuditAsync(Guid? actor, Guid caseId, Guid subject, string reason, string outcome, int count, CancellationToken ct)
    {
        db.AuditLogs.Add(new AuditLog { ActorUserId = actor, Action = outcome == "Allowed" ? "V1_CASE_FUNDS_READ" : "V1_CASE_FUNDS_READ_DENIED",
            TargetType = "SupportCase", TargetId = caseId.ToString(), Reason = reason,
            MetadataJson = JsonSerializer.Serialize(new { SubjectUserId = subject, Outcome = outcome, ReturnedCount = count }) });
        await db.SaveChangesAsync(ct);
    }
}
