using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Domain.V1Groups;
using Xunit;

namespace SpendWise.Tests;

public sealed class V1ActivityVotingTests
{
    [Fact]
    public async Task Overdue_owner_response_creates_one_linked_support_case_without_approving_money()
    {
        var clock = new V1GovernanceFinishTests.Clock(); await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);
        var owner = await f.AddUserAsync("Owner"); var member = await f.AddUserAsync("Member"); var service = V1ElectionTests.Configured(f);
        var fund = await service.CreateAsync(owner.Id, new("Fixed", "Fixed", 100, TargetedCorrectnessFixture.SecurityPin), default);
        f.Db.Add(new V1FundMembership { FundId = fund.Id, UserId = member.Id, Seat = 2 }); await f.Db.SaveChangesAsync();
        var wallet = await f.AddWalletAsync(member.Id, "Main", true, balance: 100);
        var revision = await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(100, TargetedCorrectnessFixture.SecurityPin), default);
        await service.ContributeAsync(member.Id, fund.Id, new(wallet.Id, 50, "paid", TargetedCorrectnessFixture.SecurityPin, revision.Revision), default);
        await service.CloseActivityAsync(owner.Id, fund.Id, TargetedCorrectnessFixture.SecurityPin, default);
        await Assert.ThrowsAsync<ConflictException>(() => service.EscalateOwnerResponseAsync(member.Id, fund.Id, default));
        clock.UtcNow = clock.UtcNow.AddDays(7);
        var first = await service.EscalateOwnerResponseAsync(member.Id, fund.Id, default);
        Assert.Equal(first, await service.EscalateOwnerResponseAsync(member.Id, fund.Id, default));
        Assert.Single(await f.Db.SupportCases.ToListAsync()); Assert.Single(await f.Db.CaseHistories.ToListAsync());
        Assert.True((await service.OwnerResponseAsync(member.Id, fund.Id, default)).Overdue);
        Assert.Equal(50, await f.Db.Wallets.Select(w => w.AvailableBalance).SingleAsync());
        Assert.Single(await f.Db.LedgerTransactions.ToListAsync());
    }
    [Theory]
    [InlineData(true, "ApprovedAwaitingSettlement")]
    [InlineData(false, "Rejected")]
    public async Task Fixed_activity_vote_closes_after_24_active_hours_without_spending_nonconsenting_money(bool majority, string expected)
    {
        var clock = new V1GovernanceFinishTests.Clock(); await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);
        var owner = await f.AddUserAsync("Owner"); var a = await f.AddUserAsync("A"); var b = await f.AddUserAsync("B"); var service = V1ElectionTests.Configured(f);
        var fund = await service.CreateAsync(owner.Id, new("Fixed", "Fixed", 100, TargetedCorrectnessFixture.SecurityPin), default);
        f.Db.AddRange(new V1FundMembership { FundId = fund.Id, UserId = a.Id, Seat = 2 }, new V1FundMembership { FundId = fund.Id, UserId = b.Id, Seat = 3 }); await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<ConflictException>(() => service.ProposeActivityAsync(owner.Id, fund.Id, new(200, "one", TargetedCorrectnessFixture.SecurityPin), default));
        await service.CloseActivityAsync(owner.Id, fund.Id, TargetedCorrectnessFixture.SecurityPin, default);
        var proposal = await service.ProposeActivityAsync(owner.Id, fund.Id, new(200, "one", TargetedCorrectnessFixture.SecurityPin), default);
        await service.VoteActivityAsync(owner.Id, proposal.Id, true, default);
        await service.VoteActivityAsync(owner.Id, proposal.Id, true, default);
        await Assert.ThrowsAsync<ConflictException>(() => service.VoteActivityAsync(owner.Id, proposal.Id, false, default));
        if (majority) await service.VoteActivityAsync(a.Id, proposal.Id, true, default);
        clock.UtcNow = clock.UtcNow.AddHours(18); await service.ProgressActivitiesAsync(default); await service.ProgressActivitiesAsync(default);
        Assert.Single(await f.Db.V1FundActivities.Where(x => x.Event == "ActivityVoteReminder").ToListAsync());
        clock.UtcNow = clock.UtcNow.AddHours(6); await service.ProgressActivitiesAsync(default); await service.ProgressActivitiesAsync(default);
        var state = await service.ActivityProposalAsync(a.Id, proposal.Id, default); Assert.Equal(expected, state.State);
        Assert.Equal(100, (await service.GetAsync(a.Id, fund.Id, default)).TargetVnd);
        Assert.Equal(3, await f.Db.V1FundMemberships.CountAsync()); Assert.Empty(await f.Db.LedgerTransactions.ToListAsync());
        Assert.Single(await f.Db.V1FundActivities.Where(x => x.Event == "ActivityVoteClosed").ToListAsync());
        await Assert.ThrowsAsync<ConflictException>(() => service.ContributeAsync(a.Id, fund.Id, new(Guid.NewGuid(), 1, "closed", TargetedCorrectnessFixture.SecurityPin), default));
    }
}
