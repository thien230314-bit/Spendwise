using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Admin;
using SpendWise.Domain.Admin;
using SpendWise.Domain.Identity;

namespace SpendWise.Application.Features.Administration;

public sealed record ScopedUserProfile(Guid Id, string DisplayName, string MaskedEmail, string Status);

public sealed class ScopedCaseProfileReader(ISpendWiseDbContext db, PlatformPermissionService permissions)
{
    public async Task<ScopedUserProfile> GetAsync(
        ClaimsPrincipal principal, Guid caseId, Guid targetUserId, string reason, CancellationToken ct)
    {
        var actor = Guid.TryParse(principal.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? (Guid?)id : null;
        string purpose;
        try
        {
            purpose = InputBoundary.RequiredText(reason, 1000, "Reason");
            AdminAuditWriter.RequireReason(purpose);
        }
        catch (AppException)
        {
            AddAudit(actor, caseId, targetUserId, false, "Invalid access reason", "InvalidReason");
            await db.SaveChangesAsync(ct);
            throw;
        }
        var allowed = await permissions.HasSupportScopeAsync(principal, caseId, targetUserId, ct);
        var user = allowed ? await permissions.SupportSubjects(principal, caseId, targetUserId)
            .Select(u => new { u.Id, u.FullName, u.Email, u.Status }).SingleOrDefaultAsync(ct) : null;
        AddAudit(actor, caseId, targetUserId, user is not null, purpose, user is null ? "ScopeDenied" : "Allowed");
        await db.SaveChangesAsync(ct);
        if (user is null) throw new ForbiddenException("case_access_denied", "An assigned active case is required.");
        var at = user.Email.LastIndexOf('@');
        var masked = at > 0 ? user.Email[..1] + "***" + user.Email[at..] : "***";
        return new(user.Id, user.FullName, masked, user.Status.ToString());
    }

    private void AddAudit(Guid? actor, Guid caseId, Guid subject, bool allowed, string reason, string outcome) =>
        db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actor, Action = allowed ? "V1_PROFILE_READ" : "V1_PROFILE_READ_DENIED",
            TargetType = "SupportCase", TargetId = caseId.ToString(), Reason = reason,
            MetadataJson = JsonSerializer.Serialize(new { SubjectUserId = subject, Outcome = outcome })
        });
}
