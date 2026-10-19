using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;

namespace SpendWise.Application.Features.Administration;

public sealed record A3Governance
{
    public Guid Id { get; init; }
    public Guid? ParentId { get; init; }
    public Guid? ActorId { get; init; }
    public Guid? SubjectId { get; init; }
    public string State { get; init; } = "";
    public DateTime At { get; init; }
    public int? Round { get; init; }
    public bool? Support { get; init; }
    public long? Revision { get; init; }
    public string ExecutionAvailability => "ReadOnly; stored results only; no production policy approval inferred";
}

public sealed partial class AdminA3Service
{
    public Task<A2Page<A3Governance>> GovernanceAsync(ClaimsPrincipal principal, Guid caseId, Guid fundId, string source, A3FeedQuery q, CancellationToken ct) =>
        WithFund(principal, caseId, fundId, q.Reason, "GOVERNANCE", async (_, fund, token) =>
        {
            ValidateFeed(q);
            if (fund.Code == null) throw new AppException("legacy_governance_unavailable", "Legacy ownership events remain in the activity source; no V1 ballots are inferred.");
            if (source is "ActivityProposals" or "ActivityVoters" && fund.Kind != "Fixed") throw new AppException("fixed_fund_required", "Activity proposals apply only to Fixed funds.");
            IQueryable<A3Governance> rows = source switch
            {
                "Elections" => db.V1Elections.AsNoTracking().Where(e => e.FundId == fundId).Select(e => new A3Governance { Id = e.Id, ActorId = e.OpenedByUserId, SubjectId = e.WinnerUserId, State = e.Phase.ToString(), At = e.PhaseStartedAt, Revision = e.OwnershipRevision }),
                "Ballots" => from b in db.V1ElectionBallots.AsNoTracking() join e in db.V1Elections on b.ElectionId equals e.Id where e.FundId == fundId
                    select new A3Governance { Id = b.Id, ParentId = e.Id, ActorId = b.VoterUserId, SubjectId = b.CandidateUserId, State = "Recorded", At = b.CreatedAt, Round = b.Round, Support = b.Support },
                "Candidates" => from c in db.V1ElectionCandidates.AsNoTracking() join e in db.V1Elections on c.ElectionId equals e.Id where e.FundId == fundId
                    select new A3Governance { Id = c.Id, ParentId = e.Id, SubjectId = c.UserId, State = c.InRunoff ? "RunoffCandidate" : "Candidate", At = c.CreatedAt },
                "Electorate" => from v in db.V1ElectionVoters.AsNoTracking() join e in db.V1Elections on v.ElectionId equals e.Id where e.FundId == fundId
                    select new A3Governance { Id = v.Id, ParentId = e.Id, SubjectId = v.UserId, State = "FrozenElectorate", At = v.CreatedAt },
                "OwnershipOffers" => db.V1OwnershipOffers.AsNoTracking().Where(o => o.FundId == fundId).Select(o => new A3Governance { Id = o.Id, ActorId = o.FromUserId, SubjectId = o.ToUserId, State = o.AcceptedAt == null ? "Offered" : "Accepted", At = o.AcceptedAt ?? o.CreatedAt, Revision = o.ExpectedOwnershipRevision }),
                "ActivityProposals" => db.V1ActivityProposals.AsNoTracking().Where(p => p.FundId == fundId).Select(p => new A3Governance { Id = p.Id, ActorId = p.ProposedByUserId, State = p.State, At = p.OpenedAt, Revision = p.OwnershipRevision }),
                "ActivityVoters" => from v in db.V1ActivityVoters.AsNoTracking() join p in db.V1ActivityProposals on v.ProposalId equals p.Id where p.FundId == fundId
                    select new A3Governance { Id = v.Id, ParentId = p.Id, ActorId = v.UserId, State = v.Support == null ? "NoResponse" : "Recorded", Support = v.Support, At = v.CreatedAt },
                _ => throw new AppException("invalid_governance_source", "Unsupported governance source.")
            };
            if (q.ParentId is {} parent) rows = rows.Where(r => r.ParentId == parent);
            if (q.Status is {} state) rows = rows.Where(r => r.State == state);
            var total = await rows.CountAsync(token); var items = await rows.OrderByDescending(r => r.At).ThenBy(r => r.Id).Skip(Skip(q)).Take(q.PageSize).ToListAsync(token);
            return new A2Page<A3Governance>(clock.UtcNow, q.Page, q.PageSize, total, items.Select(r => r with { At = Utc(r.At) }).ToList());
        }, ct);
}
