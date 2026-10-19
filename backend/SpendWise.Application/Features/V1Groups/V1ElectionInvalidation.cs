using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Features.Groups;
using SpendWise.Application.Notifications;
using SpendWise.Domain.Admin;
using SpendWise.Domain.Identity;

namespace SpendWise.Application.Features.V1Groups;

public sealed partial class V1FundService
{
    // System observation never stamps a human action or elects a replacement owner.
    public Task InvalidateIneligibleElectionsAsync(CancellationToken ct) => ObserveElectionsAsync(false, ct);
    public Task ProgressElectionsAsync(CancellationToken ct) => ObserveElectionsAsync(true, ct);
    private async Task ObserveElectionsAsync(bool progress, CancellationToken ct)
    {
        var ids = await db.V1Elections.AsNoTracking().Where(e => e.FinishedAt == null
            && (progress || !db.V1ElectionVoters.Any(v => v.ElectionId == e.Id)
                || db.V1ElectionVoters.Any(v => v.ElectionId == e.Id
                    && (!db.V1FundMemberships.Any(m => m.Id == v.MembershipId && m.UserId == v.UserId && m.FundId == e.FundId && m.LeftAt == null)
                        || !db.Users.Any(u => u.Id == v.UserId && u.SystemRole == SystemRole.User && u.Status == UserStatus.Active && u.EmailVerifiedAt != null && !u.MustChangePassword)))))
            .OrderBy(e => e.Id).Select(e => e.Id).ToListAsync(ct);
        foreach (var id in ids)
        {
            await db.ExecuteInTransactionAsync(async token =>
            {
                var snapshot = await db.V1Elections.AsNoTracking().SingleAsync(e => e.Id == id, token);
                var voters = await db.V1ElectionVoters.AsNoTracking().Where(v => v.ElectionId == id).ToListAsync(token);
                await GroupAdmission.LockUsersAsync(db, voters.Select(v => v.UserId).Append(snapshot.IncumbentUserId), token);
                var fund = await LockFundAsync(snapshot.FundId, token);
                var election = await db.V1Elections.SingleAsync(e => e.Id == id, token);
                if (db is DbContext context) await context.Entry(election).ReloadAsync(token);
                if (election.FinishedAt is not null) return false;
                _ = ElectionPolicy(election);
                var eligible = await db.V1ElectionVoters.CountAsync(v => v.ElectionId == id
                    && db.V1FundMemberships.Any(m => m.Id == v.MembershipId && m.UserId == v.UserId && m.FundId == fund.Id && m.LeftAt == null)
                    && db.Users.Any(u => u.Id == v.UserId && u.SystemRole == SystemRole.User && u.Status == UserStatus.Active && u.EmailVerifiedAt != null && !u.MustChangePassword), token);
                if (voters.Count != 0 && eligible == voters.Count)
                {
                    if (!progress || !await PhaseExpiredAsync(election, token)) return false;
                    if (fund.OwnerUserId != election.IncumbentUserId || fund.OwnershipRevision != election.OwnershipRevision)
                        throw new SpendWise.Application.Common.Exceptions.ConflictException("stale_ownership_epoch", "Election ownership changed.");
                    await TransitionElectionAsync(election, null, token);
                    db.AuditLogs.Add(new AuditLog { Action = "V1_ELECTION_PHASE_ADVANCED", TargetType = "V1Election", TargetId = id.ToString(), Reason = election.Phase.ToString() });
                    await db.SaveChangesAsync(token); return true;
                }
                FailElection(election, "EligibilityChanged");
                db.AuditLogs.Add(new AuditLog { Action = "V1_ELECTION_INVALIDATED", TargetType = "V1Election", TargetId = id.ToString(),
                    Reason = "Frozen electorate eligibility changed; system termination without ownership or financial mutation." });
                await db.SaveChangesAsync(token);
                await ActivityAsync(fund.Id, null, "ElectionInvalidated", token, detail: "EligibilityChanged", referenceId: id);
                return true;
            }, ct);
        }
    }
}
