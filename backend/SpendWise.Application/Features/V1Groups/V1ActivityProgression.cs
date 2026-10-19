using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Features.Groups;
using SpendWise.Application.Features.Operations;
using SpendWise.Domain.Identity;

namespace SpendWise.Application.Features.V1Groups;

public sealed partial class V1FundService
{
    public async Task ProgressActivitiesAsync(CancellationToken ct)
    {
        var ids = await db.V1ActivityProposals.AsNoTracking().Where(p => p.State == "Voting").OrderBy(p => p.Id).Select(p => p.Id).ToListAsync(ct);
        foreach (var id in ids)
        {
            await db.ExecuteInTransactionAsync(async token =>
            {
                var snapshot = await db.V1ActivityProposals.AsNoTracking().SingleAsync(p => p.Id == id, token);
                var voters = await db.V1ActivityVoters.AsNoTracking().Where(v => v.ProposalId == id).ToListAsync(token);
                await GroupAdmission.LockUsersAsync(db, voters.Select(v => v.UserId), token);
                var fund = await LockFundAsync(snapshot.FundId, token);
                var proposal = await db.V1ActivityProposals.SingleAsync(p => p.Id == id, token);
                if (db is DbContext context) await context.Entry(proposal).ReloadAsync(token);
                if (proposal.State != "Voting") return false;
                voters = await db.V1ActivityVoters.AsNoTracking().Where(v => v.ProposalId == id).ToListAsync(token);
                var eligible = await db.V1ActivityVoters.CountAsync(v => v.ProposalId == id
                    && db.V1FundMemberships.Any(m => m.Id == v.MembershipId && m.UserId == v.UserId && m.FundId == fund.Id && m.LeftAt == null)
                    && db.Users.Any(u => u.Id == v.UserId && u.SystemRole == SystemRole.User && u.Status == UserStatus.Active && u.EmailVerifiedAt != null && !u.MustChangePassword), token);
                var elapsed = await V1MaintenanceGate.ActiveElapsedAsync(db, proposal.OpenedAt, clock.UtcNow, token, "Groups");
                if (eligible != voters.Count || voters.Count == 0 || fund.OwnershipRevision != proposal.OwnershipRevision) proposal.State = "Invalidated";
                else if (elapsed >= TimeSpan.FromHours(24)) proposal.State = voters.Count(v => v.Support == true) * 2 > voters.Count ? "ApprovedAwaitingSettlement" : "Rejected";
                if (proposal.State != "Voting")
                {
                    proposal.FinishedAt = clock.UtcNow;
                    await ActivityAsync(fund.Id, null, "ActivityVoteClosed", token, detail: proposal.State, referenceId: id);
                }
                else if (elapsed >= TimeSpan.FromHours(18) && proposal.ReminderSentAt is null)
                {
                    proposal.ReminderSentAt = clock.UtcNow;
                    await ActivityAsync(fund.Id, null, "ActivityVoteReminder", token, detail: "Silence means not participating; any refund must finish before removal.", referenceId: id,
                        audience: voters.Where(v => v.Support == null).Select(v => v.UserId));
                }
                return true;
            }, ct);
        }
    }
}
