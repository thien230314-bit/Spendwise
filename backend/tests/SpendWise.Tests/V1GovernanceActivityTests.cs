using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Features.V1Groups;
using SpendWise.Application.Common.Interfaces;
using Xunit;

namespace SpendWise.Tests;

public sealed class V1GovernanceActivityTests
{
    [Fact]
    public async Task Genuine_action_stamps_server_time_but_reads_do_not_extend_owner_activity()
    {
        var clock = new TestClock(); await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);
        var owner = await f.AddUserAsync("Owner"); var service = V1FundMembershipTests.Service(f);
        var fund = await service.CreateAsync(owner.Id, new("Fixed", "Fixed", 100, TargetedCorrectnessFixture.SecurityPin), default);
        Assert.Equal(clock.UtcNow, await f.Db.V1UserActivities.Select(a => a.LastActionAt).SingleAsync());
        clock.UtcNow = clock.UtcNow.AddDays(15);
        await service.GetAsync(owner.Id, fund.Id, default);
        var state = await service.GovernanceAsync(owner.Id, fund.Id, default);
        Assert.True(state.ObservedInactivity); Assert.False(state.InactivityEligible); Assert.Equal("PARTIAL_ACTIVITY_COVERAGE", state.ActivityEvidence); Assert.Equal(0, state.OwnershipRevision);
        Assert.Equal("ACTIVITY_EVIDENCE_BLOCKED", state.ElectionAvailability);
        Assert.Equal(owner.Id, (await service.GetAsync(owner.Id, fund.Id, default)).OwnerUserId);
        await service.SetSpendingPermissionAsync(owner.Id, fund.Id, owner.Id, false, TargetedCorrectnessFixture.SecurityPin, default);
        Assert.False((await service.GovernanceAsync(owner.Id, fund.Id, default)).InactivityEligible);
        Assert.False((await service.GovernanceAsync(owner.Id, fund.Id, default)).ObservedInactivity);
        Assert.Equal(clock.UtcNow, await f.Db.V1UserActivities.Select(a => a.LastActionAt).SingleAsync());
    }

    private sealed class TestClock : IClock { public DateTime UtcNow { get; set; } = new(2026, 10, 18, 12, 0, 0, DateTimeKind.Utc); }
}
