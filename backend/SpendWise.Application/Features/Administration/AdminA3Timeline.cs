using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Domain.Groups;

namespace SpendWise.Application.Features.Administration;

public sealed record A3Invitation
{
    public Guid Id { get; init; }
    public Guid InvitedByUserId { get; init; }
    public Guid InvitedUserId { get; init; }
    public string Source { get; init; } = "";
    public string State { get; init; } = "";
    public DateTime CreatedAt { get; init; }
    public DateTime ExpiresAt { get; init; }
    public DateTime? AcceptedAt { get; init; }
    public string DecisionTimeProvenance => AcceptedAt == null ? "NOT_RECORDED" : "AcceptedAt";
}

public sealed partial class AdminA3Service
{
    public Task<A2Page<A3Invitation>> InvitationsAsync(ClaimsPrincipal principal, Guid caseId, Guid fundId, A3FeedQuery q, CancellationToken ct) =>
        WithFund(principal, caseId, fundId, q.Reason, "INVITATIONS", async (_, fund, token) =>
        {
            ValidateFeed(q); if (q.ParentId != null) throw new AppException("invalid_invitation_filter", "Use status and paging.");
            IQueryable<A3Invitation> rows = fund.Code != null
                ? db.V1FundInvitations.AsNoTracking().Where(i => i.FundId == fundId).Select(i => new A3Invitation { Id = i.Id, InvitedByUserId = i.InvitedByUserId, InvitedUserId = i.InvitedUserId,
                    Source = "V1Invitation", State = i.Status.ToString(), CreatedAt = i.CreatedAt, ExpiresAt = i.ExpiresAt, AcceptedAt = i.AcceptedAt })
                : db.GroupInvites.AsNoTracking().Where(i => i.GroupId == fundId).Select(i => new A3Invitation { Id = i.Id, InvitedByUserId = i.InvitedByUserId, InvitedUserId = i.InvitedUserId,
                    Source = "LegacyInvitation", State = i.Status.ToString(), CreatedAt = i.CreatedAt, ExpiresAt = i.ExpiresAt });
            if (q.Status is {} state) rows = rows.Where(i => i.State == state);
            var total = await rows.CountAsync(token); var items = await rows.OrderByDescending(i => i.CreatedAt).ThenBy(i => i.Id).Skip(Skip(q)).Take(q.PageSize).ToListAsync(token);
            return new A2Page<A3Invitation>(clock.UtcNow, q.Page, q.PageSize, total, items.Select(i => i with { CreatedAt = Utc(i.CreatedAt), ExpiresAt = Utc(i.ExpiresAt), AcceptedAt = i.AcceptedAt is {} at ? Utc(at) : null }).ToList());
        }, ct);

    private sealed record ActivityReference(Guid Id, string Family, Guid CorrelationId, Guid? Subject, string State);
    private async Task<IReadOnlyList<A3Event>> EnrichActivityAsync(Guid fundId, bool v1, IReadOnlyList<A3Event> page, CancellationToken ct)
    {
        var ids = page.Where(a => a.ReferenceId != null).Select(a => a.ReferenceId!.Value).Distinct().ToArray();
        var references = new List<ActivityReference>();
        if (v1)
        {
            references.AddRange(await db.V1FundInvitations.AsNoTracking().Where(i => i.FundId == fundId && ids.Contains(i.Id))
                .Select(i => new ActivityReference(i.Id, "Invitation", i.Id, i.InvitedUserId, i.Status.ToString())).ToListAsync(ct));
            references.AddRange(await db.V1OwnershipOffers.AsNoTracking().Where(o => o.FundId == fundId && ids.Contains(o.Id))
                .Select(o => new ActivityReference(o.Id, "Ownership", o.Id, o.ToUserId, o.AcceptedAt == null ? "Offered" : "Accepted")).ToListAsync(ct));
            references.AddRange(await db.V1Elections.AsNoTracking().Where(e => e.FundId == fundId && ids.Contains(e.Id))
                .Select(e => new ActivityReference(e.Id, "Election", e.Id, e.WinnerUserId, e.Phase.ToString())).ToListAsync(ct));
            references.AddRange(await (from b in db.V1ElectionBallots.AsNoTracking() join e in db.V1Elections on b.ElectionId equals e.Id where e.FundId == fundId && ids.Contains(b.Id)
                select new ActivityReference(b.Id, "Election", e.Id, b.CandidateUserId, "StoredBallot")).ToListAsync(ct));
            references.AddRange(await (from c in db.V1ElectionCandidates.AsNoTracking() join e in db.V1Elections on c.ElectionId equals e.Id where e.FundId == fundId && ids.Contains(c.Id)
                select new ActivityReference(c.Id, "Election", e.Id, c.UserId, "Candidate")).ToListAsync(ct));
            references.AddRange(await db.V1ActivityProposals.AsNoTracking().Where(p => p.FundId == fundId && ids.Contains(p.Id))
                .Select(p => new ActivityReference(p.Id, "Activity", p.Id, null, p.State)).ToListAsync(ct));
        }
        var legacyPayloads = v1 ? new Dictionary<Guid, string?>() : await db.GroupActivities.AsNoTracking().Where(a => a.GroupId == fundId && page.Select(e => e.Id).Contains(a.Id))
            .Select(a => new { a.Id, a.DataJson }).ToDictionaryAsync(a => a.Id, a => a.DataJson, ct);
        return page.Select(a =>
        {
            var family = a.Action is "Joined" or "Invited" || a.Action.StartsWith("Invitation", StringComparison.Ordinal) || a.Action == "ProposalCancelled" ? "Invitation"
                : a.Action.StartsWith("Ownership", StringComparison.Ordinal) ? "Ownership"
                : a.Action.StartsWith("Election", StringComparison.Ordinal) ? "Election" : a.Action.StartsWith("ActivityVote", StringComparison.Ordinal) ? "Activity" : "Unknown";
            var link = references.SingleOrDefault(r => r.Id == a.ReferenceId && r.Family == family);
            var actorMismatch = link != null && a.Action is "Joined" or "OwnershipAccepted" && a.ActorId != link.Subject;
            var outcome = a.Action switch { "Joined" => "Accepted", "InvitationDeclined" => "Declined", "InvitationRejected" => "Rejected", "InvitationCancelled" or "ProposalCancelled" => "Cancelled",
                "OwnershipAccepted" or "ElectionOwnershipAccepted" => "OwnershipTransferred", "OwnershipOffered" => "Offered", "InvitationApproved" => "Approved", "InvitationProposed" => "Proposed",
                "ElectionPhaseAdvanced" or "ActivityVoteClosed" or "SpendingPermissionChanged" => a.Detail ?? "NOT_RECORDED", "Created" => "Created", _ => "Recorded" };
            var subject = a.SubjectId;
            if (!v1 && legacyPayloads.TryGetValue(a.Id, out var payload) && payload != null && payload.Length <= 16384)
            {
                try { using var doc = JsonDocument.Parse(payload); var key = a.Action == "MemberRemoved" ? "MemberUserId" : a.Action == "MemberInvited" ? "InvitedUserId" : a.Action == "MemberJoined" ? "UserId" : "";
                    if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String && value.TryGetGuid(out var user)) subject = user; }
                catch (JsonException) { /* Historical malformed payload is unavailable, never an authority source. */ }
            }
            return a with { At = Utc(a.At), SubjectId = subject, Outcome = actorMismatch ? "REQUIRES_REVIEW" : outcome, CorrelationId = link?.CorrelationId,
                RelatedRecordState = link?.State ?? "NOT_RECORDED", ReferenceVerification = actorMismatch ? "REQUIRES_REVIEW" : link != null ? "MATCHED" : v1 && a.ReferenceId != null && family != "Unknown" ? "NOT_FOUND_IN_FUND" : "NOT_RECORDED",
                Cause = a.Detail != null ? "AllowlistedStoredEventDetail" : "NOT_RECORDED" };
        }).ToList();
    }
}
