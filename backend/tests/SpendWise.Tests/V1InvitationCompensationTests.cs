using Microsoft.EntityFrameworkCore;
using SpendWise.Domain.Admin;
using SpendWise.Domain.V1Groups;
using Xunit;

namespace SpendWise.Tests;

public sealed class V1InvitationCompensationTests
{
    [Fact]
    public async Task Paused_invitation_remains_listed_and_acceptable_after_original_expiry()
    {
        var clock = new V1GovernanceFinishTests.Clock(); await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);
        var owner = await f.AddUserAsync("Owner"); var target = await f.AddUserAsync("Target"); var service = V1ElectionTests.Configured(f);
        var fund = await service.CreateAsync(owner.Id, new("Free", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);
        var invitation = new V1FundInvitation { FundId = fund.Id, InvitedByUserId = owner.Id, InvitedUserId = target.Id, ExpiresAt = clock.UtcNow.AddDays(-1) };
        f.Db.AddRange(invitation, new V1MaintenancePlan { CreatedByUserId = owner.Id, Scope = "Groups", StartsAt = clock.UtcNow.AddDays(-7), ClosedAt = clock.UtcNow.AddDays(-5) });
        await f.Db.SaveChangesAsync();
        await f.Db.V1FundInvitations.Where(i => i.Id == invitation.Id).ExecuteUpdateAsync(s => s.SetProperty(i => i.CreatedAt, clock.UtcNow.AddDays(-8)));
        var row = Assert.Single(await service.InvitationsAsync(target.Id, default)); Assert.Equal(clock.UtcNow.AddDays(1), row.ExpiresAt);
        await service.AcceptAsync(target.Id, invitation.Id, default);
        Assert.True(await f.Db.V1FundMemberships.AnyAsync(m => m.UserId == target.Id && m.FundId == fund.Id));
    }
}
