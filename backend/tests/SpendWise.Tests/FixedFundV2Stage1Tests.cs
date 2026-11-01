using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Features.V1Groups;
using SpendWise.Domain.V1Groups;
using Xunit;

namespace SpendWise.Tests;

public sealed class FixedFundV2Stage1Tests
{
    private const string Pin = TargetedCorrectnessFixture.SecurityPin;

    [Fact]
    public async Task T01_OwnerCreatesFixedFund_ShowsZeroOfTarget_NoContributionClockRunning()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("OwnerT01");
        var service = V1ElectionTests.Configured(f);

        // Owner creates fund with 9,000,000 VND target, 24h individual duration
        var fund = await service.CreateAsync(owner.Id, new("TripFundT01", "Fixed", 9_000_000, Pin, 24), default);

        Assert.Equal(9_000_000, fund.TargetVnd);
        Assert.Equal(24, fund.ContributionDurationHours);
        Assert.Equal("CONTRIBUTIONS_ONLY", fund.FinanceAvailability);

        var members = await service.MembersAsync(owner.Id, fund.Id, default);
        var ownerMember = Assert.Single(members);
        Assert.True(ownerMember.IsOwner);
        Assert.False(ownerMember.IndividualContributionCompleted);
        Assert.False(ownerMember.GroupFundingCompleted);
        // T01: No deadline / clock running yet before obligations confirmed
        Assert.Null(ownerMember.ContributionDeadline);
        // Owner has trust commitment recorded
        Assert.NotNull(ownerMember.TrustCommitmentAcceptedAt);
        Assert.Equal("v2.0", ownerMember.AcceptedCommitmentVersion);
    }

    [Fact]
    public async Task T02_ConfirmObligations_WithOnlyOwner_ThrowsInsufficientMembers()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("OwnerT02");
        var service = V1ElectionTests.Configured(f);
        var fund = await service.CreateAsync(owner.Id, new("TripFundT02", "Fixed", 9_000_000, Pin, 24), default);

        // T02: Single owner tries to confirm obligations -> Refused (requires at least 2 members including owner)
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            service.ConfirmObligationsAsync(owner.Id, fund.Id, new(9_000_000, Pin), default));
        Assert.Equal("insufficient_members", ex.Code);
    }

    [Fact]
    public async Task T03_IndivisibleTarget_RefusesObligationConfirmationAndPreview()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("OwnerT03");
        var m1 = await f.AddUserAsync("Member1T03");
        var m2 = await f.AddUserAsync("Member2T03");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, m1.Id);
        await V1FundMembershipTests.MakeFriends(f, owner.Id, m2.Id);

        var service = V1ElectionTests.Configured(f);
        var fund = await service.CreateAsync(owner.Id, new("TripFundT03", "Fixed", 10_000_000, Pin, 24), default);

        var inv1 = await service.InviteAsync(owner.Id, fund.Id, m1.Id, default);
        var inv2 = await service.InviteAsync(owner.Id, fund.Id, m2.Id, default);
        await service.AcceptAsync(m1.Id, inv1.Id, default);
        await service.AcceptAsync(m2.Id, inv2.Id, default);

        // Preview should refuse because 10,000,000 is not divisible by 3 seats
        var exPreview = await Assert.ThrowsAsync<ConflictException>(() =>
            service.ContributionAllocationAsync(owner.Id, fund.Id, default));
        Assert.Equal("target_not_divisible_by_seats", exPreview.Code);

        // T03: Confirming 10,000,000 / 3 seats must be refused; no 1 VND remainder distributed
        var exConfirm = await Assert.ThrowsAsync<ConflictException>(() =>
            service.ConfirmObligationsAsync(owner.Id, fund.Id, new(10_000_000, Pin), default));
        Assert.Equal("target_not_divisible_by_seats", exConfirm.Code);
    }

    [Fact]
    public async Task T04_DivisibleTarget_LocksSnapshotAndStartsIndividualCountdown()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("OwnerT04");
        var m1 = await f.AddUserAsync("Member1T04");
        var m2 = await f.AddUserAsync("Member2T04");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, m1.Id);
        await V1FundMembershipTests.MakeFriends(f, owner.Id, m2.Id);

        var service = V1ElectionTests.Configured(f);
        var fund = await service.CreateAsync(owner.Id, new("TripFundT04", "Fixed", 9_000_000, Pin, 24), default);

        var inv1 = await service.InviteAsync(owner.Id, fund.Id, m1.Id, default);
        var inv2 = await service.InviteAsync(owner.Id, fund.Id, m2.Id, default);
        await service.AcceptAsync(m1.Id, inv1.Id, default);
        await service.AcceptAsync(m2.Id, inv2.Id, default);

        // Preview is exactly 3,000,000 each
        var preview = await service.ContributionAllocationAsync(owner.Id, fund.Id, default);
        Assert.Equal(9_000_000, preview.TargetVnd);
        Assert.Equal(3, preview.Members.Count);
        Assert.All(preview.Members, m => Assert.Equal(3_000_000, m.RequiredVnd));

        // T04: Confirm obligations succeeds
        var config = await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(9_000_000, Pin), default);
        Assert.Equal(1, config.Revision);
        Assert.Equal(9_000_000, config.TargetVnd);

        // Snapshot is locked: exactly 3,000,000 per seat with individual deadline = UtcNow + 24h
        var obligations = await f.Db.V1FundObligations.Where(o => o.FundId == fund.Id && o.Revision == config.Revision).ToListAsync();
        Assert.Equal(3, obligations.Count);
        Assert.All(obligations, o =>
        {
            Assert.Equal(3_000_000, o.RequiredVnd);
            Assert.NotNull(o.Deadline);
            Assert.True(o.Deadline > f.Clock.UtcNow);
        });

        // Members list reflects the new deadline countdown
        var members = await service.MembersAsync(owner.Id, fund.Id, default);
        Assert.Equal(3, members.Count);
        Assert.All(members, m =>
        {
            Assert.NotNull(m.ContributionDeadline);
            Assert.NotNull(m.TrustCommitmentAcceptedAt);
        });
    }

    [Fact]
    public async Task TrustCommitment_RefusalPreventsJoiningAndObligations()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("OwnerTrust");
        var member = await f.AddUserAsync("MemberTrust");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);

        var service = V1ElectionTests.Configured(f);
        var fund = await service.CreateAsync(owner.Id, new("FundTrust", "Fixed", 4_000_000, Pin, 24), default);
        var inv = await service.InviteAsync(owner.Id, fund.Id, member.Id, default);

        // Member refuses trust commitment
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            service.AcceptAsync(member.Id, inv.Id, trustCommitmentAccepted: false, commitmentVersion: "v2.0", default));
        Assert.Equal("trust_commitment_required", ex.Code);

        // Verify member has not joined and no obligation exists
        Assert.False(await f.Db.V1FundMemberships.AnyAsync(m => m.FundId == fund.Id && m.UserId == member.Id));

        // Now member accepts trust commitment
        await service.AcceptAsync(member.Id, inv.Id, trustCommitmentAccepted: true, commitmentVersion: "v2.0", default);
        var m = await f.Db.V1FundMemberships.SingleAsync(m => m.FundId == fund.Id && m.UserId == member.Id);
        Assert.NotNull(m.TrustCommitmentAcceptedAt);
        Assert.Equal("v2.0", m.AcceptedCommitmentVersion);
    }
}
