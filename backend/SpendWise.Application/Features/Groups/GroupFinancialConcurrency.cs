using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Domain.Finance;
using SpendWise.Domain.Groups;

namespace SpendWise.Application.Features.Groups;

internal static class GroupFinancialConcurrency
{
    public static async Task SerializeAsync(ISpendWiseDbContext db, Guid groupId, CancellationToken token)
    {
        // Acquire the group root before reading lifecycle/financial state and before ledger writes.
        // The caller's transaction retains the write lock until all business writes commit.
        if (db is DbContext context && context.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory") return;
        await db.Groups.Where(item => item.Id == groupId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.UpdatedAt, item => item.UpdatedAt), token);

        // A scoped context can already contain an earlier snapshot. Refresh only unchanged
        // group-owned state; never clear the tracker or discard pending caller writes.
        if (db is not DbContext tracked) return;
        var entries = tracked.ChangeTracker.Entries().Where(entry => entry.State == EntityState.Unchanged).ToList();
        foreach (var entry in entries)
        {
            var belongsToGroup = entry.Entity switch
            {
                Group item => item.Id == groupId,
                GroupMember item => item.GroupId == groupId,
                SharedFund item => item.GroupId == groupId,
                LedgerAccount item => item.GroupId == groupId,
                GroupFundShare item => item.GroupId == groupId,
                FundRequest item => item.GroupId == groupId,
                FundRequestRecipient item => item.FundRequest?.GroupId == groupId
                    || (item.FundRequest is null && await db.FundRequests.AsNoTracking()
                        .AnyAsync(parent => parent.Id == item.FundRequestId && parent.GroupId == groupId, token)),
                GroupFundRound item => item.GroupId == groupId,
                GroupFundRoundMember item => item.Round?.GroupId == groupId
                    || (item.Round is null && await db.GroupFundRounds.AsNoTracking()
                        .AnyAsync(parent => parent.Id == item.GroupFundRoundId && parent.GroupId == groupId, token)),
                GroupDeletionRequest item => item.GroupId == groupId,
                GroupDeletionVote item => item.Request?.GroupId == groupId
                    || (item.Request is null && await db.GroupDeletionRequests.AsNoTracking()
                        .AnyAsync(parent => parent.Id == item.GroupDeletionRequestId && parent.GroupId == groupId, token)),
                GroupRefund item => item.Request?.GroupId == groupId
                    || (item.Request is null && await db.GroupDeletionRequests.AsNoTracking()
                        .AnyAsync(parent => parent.Id == item.GroupDeletionRequestId && parent.GroupId == groupId, token)),
                _ => false
            };
            if (belongsToGroup) await entry.ReloadAsync(token);
        }
    }
}
