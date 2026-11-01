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
    public async Task T01_OwnerCreatesFixedFund_ExplicitCommitmentRequired_ShowsZeroOfTarget_NoClockRunning()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("OwnerT01");
        var service = V1ElectionTests.Configured(f);

        // F1: Without explicit trust commitment, Fixed creation is rejected
        var exNoTrust = await Assert.ThrowsAsync<ConflictException>(() =>
            service.CreateAsync(owner.Id, new("TripFundT01", "Fixed", 9_000_000, Pin, 24, null, null), default));
        Assert.Equal("trust_commitment_required", exNoTrust.Code);

        var exFalseTrust = await Assert.ThrowsAsync<ConflictException>(() =>
            service.CreateAsync(owner.Id, new("TripFundT01", "Fixed", 9_000_000, Pin, 24, false, "v2.0"), default));
        Assert.Equal("trust_commitment_required", exFalseTrust.Code);

        // Invalid duration rejected with explicit validation
        var exDuration = await Assert.ThrowsAsync<AppException>(() =>
            service.CreateAsync(owner.Id, new("TripFundT01", "Fixed", 9_000_000, Pin, 0, true, "v2.0"), default));
        Assert.Equal("invalid_contribution_duration", exDuration.Code);

        // Valid creation with explicit commitment
        var fund = await service.CreateAsync(owner.Id, new("TripFundT01", "Fixed", 9_000_000, Pin, 24, true, "v2.0"), default);

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
        // Owner has trust commitment recorded explicitly
        Assert.NotNull(ownerMember.TrustCommitmentAcceptedAt);
        Assert.Equal("v2.0", ownerMember.AcceptedCommitmentVersion);
    }

    [Fact]
    public async Task T02_ConfirmObligations_WithOnlyOwner_ThrowsInsufficientMembers()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("OwnerT02");
        var service = V1ElectionTests.Configured(f);
        var fund = await service.CreateAsync(owner.Id, new("TripFundT02", "Fixed", 9_000_000, Pin, 24, true, "v2.0"), default);

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
        var fund = await service.CreateAsync(owner.Id, new("TripFundT03", "Fixed", 10_000_000, Pin, 24, true, "v2.0"), default);

        var inv1 = await service.InviteAsync(owner.Id, fund.Id, m1.Id, default);
        var inv2 = await service.InviteAsync(owner.Id, fund.Id, m2.Id, default);
        await service.AcceptAsync(m1.Id, inv1.Id, true, "v2.0", default);
        await service.AcceptAsync(m2.Id, inv2.Id, true, "v2.0", default);

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
        var fund = await service.CreateAsync(owner.Id, new("TripFundT04", "Fixed", 9_000_000, Pin, 24, true, "v2.0"), default);

        var inv1 = await service.InviteAsync(owner.Id, fund.Id, m1.Id, default);
        var inv2 = await service.InviteAsync(owner.Id, fund.Id, m2.Id, default);
        await service.AcceptAsync(m1.Id, inv1.Id, true, "v2.0", default);
        await service.AcceptAsync(m2.Id, inv2.Id, true, "v2.0", default);

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
    public async Task F2_ObligationSnapshot_IsImmutable_CannotChangeTargetOrResetDeadline()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("OwnerF2");
        var m1 = await f.AddUserAsync("MemberF2");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, m1.Id);

        var service = V1ElectionTests.Configured(f);
        var fund = await service.CreateAsync(owner.Id, new("FundF2", "Fixed", 4_000_000, Pin, 24, true, "v2.0"), default);

        var inv = await service.InviteAsync(owner.Id, fund.Id, m1.Id, default);
        await service.AcceptAsync(m1.Id, inv.Id, true, "v2.0", default);

        // Initial confirmation: 4,000,000 / 2 = 2,000,000 each
        var config1 = await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(4_000_000, Pin), default);
        Assert.Equal(1, config1.Revision);

        // Idempotent call with identical target and members succeeds without changing anything
        var configSame = await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(4_000_000, Pin), default);
        Assert.Equal(1, configSame.Revision);

        // F2: Owner attempts to change target to 6,000,000 after locked -> REJECTED
        var exChange = await Assert.ThrowsAsync<ConflictException>(() =>
            service.ConfirmObligationsAsync(owner.Id, fund.Id, new(6_000_000, Pin), default));
        Assert.Equal("obligations_already_confirmed", exChange.Code);

        // Verify target and revision remained strictly intact in database
        var treasury = await f.Db.V1FundTreasuries.SingleAsync(t => t.FundId == fund.Id);
        Assert.Equal(1, treasury.Revision);
        Assert.Equal(4_000_000, treasury.TargetVnd);
    }

    [Fact]
    public async Task F1_TrustCommitment_ExplicitConfirmationRequired_RejectsMissingOrFalseOrWrongVersion()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("OwnerTrustF1");
        var member = await f.AddUserAsync("MemberTrustF1");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);

        var service = V1ElectionTests.Configured(f);
        var fund = await service.CreateAsync(owner.Id, new("FundTrustF1", "Fixed", 4_000_000, Pin, 24, true, "v2.0"), default);
        var inv = await service.InviteAsync(owner.Id, fund.Id, member.Id, default);

        // Call Accept without confirmation (null) -> REJECTED
        var exNull = await Assert.ThrowsAsync<ConflictException>(() =>
            service.AcceptAsync(member.Id, inv.Id, null, null, default));
        Assert.Equal("trust_commitment_required", exNull.Code);

        // Call Accept with false -> REJECTED
        var exFalse = await Assert.ThrowsAsync<ConflictException>(() =>
            service.AcceptAsync(member.Id, inv.Id, false, "v2.0", default));
        Assert.Equal("trust_commitment_required", exFalse.Code);

        // Call Accept with wrong version -> REJECTED
        var exWrongVer = await Assert.ThrowsAsync<ConflictException>(() =>
            service.AcceptAsync(member.Id, inv.Id, true, "v1.0", default));
        Assert.Equal("trust_commitment_required", exWrongVer.Code);

        // Not joined yet
        Assert.False(await f.Db.V1FundMemberships.AnyAsync(m => m.FundId == fund.Id && m.UserId == member.Id));

        // Call Accept with true and valid version v2.0 -> SUCCESS
        await service.AcceptAsync(member.Id, inv.Id, true, "v2.0", default);
        var m = await f.Db.V1FundMemberships.SingleAsync(m => m.FundId == fund.Id && m.UserId == member.Id);
        Assert.NotNull(m.TrustCommitmentAcceptedAt);
        Assert.Equal("v2.0", m.AcceptedCommitmentVersion);
    }

    [Fact]
    public async Task F4_FreeFund_PreservesZeroContamination_NoTrustFieldsOrDuration()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("OwnerFreeF4");
        var member = await f.AddUserAsync("MemberFreeF4");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);

        var service = V1ElectionTests.Configured(f);

        // Free fund rejects contribution duration
        var exDuration = await Assert.ThrowsAsync<AppException>(() =>
            service.CreateAsync(owner.Id, new("FreeFundF4", "Free", null, Pin, 24), default));
        Assert.Equal("invalid_fund_configuration", exDuration.Code);

        // Free fund created without trust commitment
        var fund = await service.CreateAsync(owner.Id, new("FreeFundF4", "Free", null, Pin), default);
        Assert.Equal("Free", fund.Kind);
        Assert.Null(fund.TargetVnd);

        var ownerM = await f.Db.V1FundMemberships.SingleAsync(m => m.FundId == fund.Id && m.UserId == owner.Id);
        // F4: Trust fields MUST be null for Free Fund
        Assert.Null(ownerM.TrustCommitmentAcceptedAt);
        Assert.Null(ownerM.AcceptedCommitmentVersion);

        // Member joins Free Fund without needing trust commitment
        var inv = await service.InviteAsync(owner.Id, fund.Id, member.Id, default);
        await service.AcceptAsync(member.Id, inv.Id, null, null, default);

        var memberM = await f.Db.V1FundMemberships.SingleAsync(m => m.FundId == fund.Id && m.UserId == member.Id);
        Assert.Null(memberM.TrustCommitmentAcceptedAt);
        Assert.Null(memberM.AcceptedCommitmentVersion);
    }

    [Fact]
    public async Task F5_MembershipLock_AfterObligationsConfirmed_RejectsNewInviteAndPreExistingAcceptance()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("OwnerF5");
        var member1 = await f.AddUserAsync("Member1F5");
        var member2 = await f.AddUserAsync("Member2F5");
        var member3 = await f.AddUserAsync("Member3F5");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member1.Id);
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member2.Id);
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member3.Id);

        var service = V1ElectionTests.Configured(f);
        var fund = await service.CreateAsync(owner.Id, new("FundF5", "Fixed", 4_000_000, Pin, 24, true, "v2.0"), default);

        // Invite member1 and member2 BEFORE confirmation
        var inv1 = await service.InviteAsync(owner.Id, fund.Id, member1.Id, default);
        var inv2 = await service.InviteAsync(owner.Id, fund.Id, member2.Id, default);

        // Member 1 accepts
        await service.AcceptAsync(member1.Id, inv1.Id, true, "v2.0", default);

        // Owner confirms obligations for 2 members (Owner + Member 1) with 4,000,000 VND
        var config = await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(4_000_000, Pin), default);
        Assert.Equal(1, config.Revision);

        // Scenario 1: Owner attempts to invite member3 AFTER confirmation -> REJECTED
        var exInvite = await Assert.ThrowsAsync<ConflictException>(() =>
            service.InviteAsync(owner.Id, fund.Id, member3.Id, default));
        Assert.Equal("fund_obligations_locked", exInvite.Code);

        // Scenario 2: Member 2 attempts to accept pre-existing invitation AFTER confirmation -> REJECTED
        var exAccept = await Assert.ThrowsAsync<ConflictException>(() =>
            service.AcceptAsync(member2.Id, inv2.Id, true, "v2.0", default));
        Assert.Equal("fund_obligations_locked", exAccept.Code);

        // Scenario 3: Existing confirmed members can still contribute cleanly with their deadlines preserved
        var oWallet = await f.AddWalletAsync(owner.Id, "OWalletF5", true, balance: 5_000_000);
        var m1Wallet = await f.AddWalletAsync(member1.Id, "MWalletF5", true, balance: 5_000_000);

        var oContrib = await service.ContributeAsync(owner.Id, fund.Id, new(oWallet.Id, 2_000_000, "c-o-f5", Pin, config.Revision), default);
        Assert.Equal(2_000_000, oContrib.AmountVnd);

        var m1Contrib = await service.ContributeAsync(member1.Id, fund.Id, new(m1Wallet.Id, 2_000_000, "c-m1-f5", Pin, config.Revision), default);
        Assert.Equal(2_000_000, m1Contrib.AmountVnd);

        // Deadlines remain unchanged
        var members = await service.MembersAsync(owner.Id, fund.Id, default);
        var m1 = Assert.Single(members, m => m.UserId == member1.Id);
        Assert.NotNull(m1.ContributionDeadline);
    }
}
