using SpendWise.Application.Common.Interfaces;
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

    private sealed class MutableClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; set; } = utcNow;
    }

    [Fact]
    public async Task F6_ConfirmObligations_Idempotency_AndReconfirmationRejection()
    {
        var clock = new MutableClock(DateTime.UtcNow);
        await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);
        var owner = await f.AddUserAsync("OwnerF6");
        var member = await f.AddUserAsync("MemberF6");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);

        var service = V1ElectionTests.Configured(f);
        var fund = await service.CreateAsync(owner.Id, new("FundF6", "Fixed", 4_000_000, Pin, 24, true, "v2.0"), default);
        var inv = await service.InviteAsync(owner.Id, fund.Id, member.Id, default);
        await service.AcceptAsync(member.Id, inv.Id, true, "v2.0", default);

        var conf1 = await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(4_000_000, Pin), default);
        Assert.Equal(1, conf1.Revision);

        var initialMembers = await service.MembersAsync(owner.Id, fund.Id, default);
        var initialDeadlines = initialMembers.ToDictionary(m => m.UserId, m => m.ContributionDeadline);

        // Advance clock by 2 hours
        clock.UtcNow = clock.UtcNow.AddHours(2);

        // Idempotent re-confirmation: returns current state, revision does NOT increment, deadlines do NOT change
        var confIdempotent = await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(4_000_000, Pin), default);
        Assert.Equal(1, confIdempotent.Revision);

        var membersAfter = await service.MembersAsync(owner.Id, fund.Id, default);
        foreach (var m in membersAfter)
        {
            Assert.Equal(initialDeadlines[m.UserId], m.ContributionDeadline);
        }

        // Attempting to change target mid-period -> rejected
        var exTargetChange = await Assert.ThrowsAsync<ConflictException>(() =>
            service.ConfirmObligationsAsync(owner.Id, fund.Id, new(2_000_000, Pin), default));
        Assert.Equal("obligations_already_confirmed", exTargetChange.Code);
    }

    [Fact]
    public async Task F7_ReplacementSeat_ValidationAndRejection()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("OwnerF7");
        var member = await f.AddUserAsync("MemberF7");
        var replacementUser = await f.AddUserAsync("ReplacementUserF7");
        var replacementUser2 = await f.AddUserAsync("ReplacementUser2F7");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);
        await V1FundMembershipTests.MakeFriends(f, owner.Id, replacementUser.Id);
        await V1FundMembershipTests.MakeFriends(f, owner.Id, replacementUser2.Id);

        var service = V1ElectionTests.Configured(f);
        var fund = await service.CreateAsync(owner.Id, new("FundF7", "Fixed", 4_000_000, Pin, 24, true, "v2.0"), default);
        var inv = await service.InviteAsync(owner.Id, fund.Id, member.Id, default);
        await service.AcceptAsync(member.Id, inv.Id, true, "v2.0", default);
        await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(4_000_000, Pin), default);

        // Member is on seat 2 (occupied). If an invitation points to seat 2, it must be rejected!
        var memberSeat = (await service.MembersAsync(owner.Id, fund.Id, default)).Single(m => m.UserId == member.Id).Seat;
        var invOccupied = new V1FundInvitation
        {
            FundId = fund.Id,
            InvitedUserId = replacementUser.Id,
            InvitedByUserId = owner.Id,
            Status = V1InvitationStatus.Pending,
            ExpiresAt = f.Clock.UtcNow.AddDays(7),
            IsReplacement = true,
            TargetSeat = memberSeat
        };
        f.Db.V1FundInvitations.Add(invOccupied);
        await f.Db.SaveChangesAsync();

        var exOccupied = await Assert.ThrowsAsync<ConflictException>(() =>
            service.AcceptAsync(replacementUser.Id, invOccupied.Id, true, "v2.0", default));
        Assert.Equal("replacement_seat_unavailable", exOccupied.Code);

        // If replacement invitation has null TargetSeat -> rejected
        var invNullSeat = new V1FundInvitation
        {
            FundId = fund.Id,
            InvitedUserId = replacementUser2.Id,
            InvitedByUserId = owner.Id,
            Status = V1InvitationStatus.Pending,
            ExpiresAt = f.Clock.UtcNow.AddDays(7),
            IsReplacement = true,
            TargetSeat = null
        };
        f.Db.V1FundInvitations.Add(invNullSeat);
        await f.Db.SaveChangesAsync();

        var exNullSeat = await Assert.ThrowsAsync<ConflictException>(() =>
            service.AcceptAsync(replacementUser2.Id, invNullSeat.Id, true, "v2.0", default));
        Assert.Equal("replacement_seat_unavailable", exNullSeat.Code);
    }

    [Fact]
    public async Task F8_ReplacementMember_PreservesHistoricalObligation_AndCreatesNewEpisodeObligation()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("OwnerF8");
        var departedMember = await f.AddUserAsync("DepartedF8");
        var replacementUser = await f.AddUserAsync("ReplacementF8");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, departedMember.Id);
        await V1FundMembershipTests.MakeFriends(f, owner.Id, replacementUser.Id);

        var service = V1ElectionTests.Configured(f);
        var fund = await service.CreateAsync(owner.Id, new("FundF8", "Fixed", 4_000_000, Pin, 24, true, "v2.0"), default);
        var inv1 = await service.InviteAsync(owner.Id, fund.Id, departedMember.Id, default);
        await service.AcceptAsync(departedMember.Id, inv1.Id, true, "v2.0", default);
        await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(4_000_000, Pin), default);

        var departedMembership = await f.Db.V1FundMemberships.SingleAsync(m => m.FundId == fund.Id && m.UserId == departedMember.Id);
        var departedObligation = await f.Db.V1FundObligations.SingleAsync(o => o.FundId == fund.Id && o.UserId == departedMember.Id);
        Assert.Equal(departedMembership.Id, departedObligation.MembershipId);
        Assert.Equal(2, departedObligation.Seat);
        Assert.Equal(2_000_000, departedObligation.RequiredVnd);

        // Departed member leaves -> seat 2 becomes vacant
        departedMembership.LeftAt = f.Clock.UtcNow;
        await f.Db.SaveChangesAsync();

        // Create replacement invite for seat 2
        var replacementInvite = new V1FundInvitation
        {
            FundId = fund.Id,
            InvitedUserId = replacementUser.Id,
            InvitedByUserId = owner.Id,
            Status = V1InvitationStatus.Pending,
            ExpiresAt = f.Clock.UtcNow.AddDays(7),
            IsReplacement = true,
            TargetSeat = 2
        };
        f.Db.V1FundInvitations.Add(replacementInvite);
        await f.Db.SaveChangesAsync();

        // Replacement member accepts
        await service.AcceptAsync(replacementUser.Id, replacementInvite.Id, true, "v2.0", default);

        var replacementMembership = await f.Db.V1FundMemberships.SingleAsync(m => m.FundId == fund.Id && m.UserId == replacementUser.Id && m.LeftAt == null);
        Assert.Equal(2, replacementMembership.Seat);
        Assert.NotNull(replacementMembership.ContributionDeadline);

        // Verification of invariant:
        // 1. Departed member historical obligation remains 100% intact (MembershipId & UserId untouched)
        var departedObligationAfter = await f.Db.V1FundObligations.SingleAsync(o => o.Id == departedObligation.Id);
        Assert.Equal(departedMembership.Id, departedObligationAfter.MembershipId);
        Assert.Equal(departedMember.Id, departedObligationAfter.UserId);
        Assert.Equal(2, departedObligationAfter.Seat);

        // 2. A separate obligation record is created for replacement member episode
        var replacementObligation = await f.Db.V1FundObligations.SingleAsync(o => o.FundId == fund.Id && o.UserId == replacementUser.Id);
        Assert.Equal(replacementMembership.Id, replacementObligation.MembershipId);
        Assert.Equal(2, replacementObligation.Seat);
        Assert.Equal(2_000_000, replacementObligation.RequiredVnd);
        Assert.Equal(replacementMembership.ContributionDeadline, replacementObligation.Deadline);
        Assert.NotEqual(departedObligationAfter.Id, replacementObligation.Id);

        // Total obligations for seat 2 across revisions/episodes is 2 (one historical, one active replacement)
        var seat2Obligations = await f.Db.V1FundObligations.Where(o => o.FundId == fund.Id && o.Seat == 2).ToListAsync();
        Assert.Equal(2, seat2Obligations.Count);
    }

    private sealed class TestInactivity : IV1InactivityEvidence
    {
        public Task<V1InactivityProof> GetAsync(Guid userId, CancellationToken ct) => Task.FromResult(new V1InactivityProof(true, true, "Synthetic-proof"));
    }

    private static V1FundService CreateService(SpendWise.Infrastructure.Persistence.SpendWiseDbContext db, TargetedCorrectnessFixture f) => new(
        db, f.Clock, f.Security,
        new SpendWise.Infrastructure.Notifications.NotificationService(db),
        new SpendWise.Infrastructure.Finance.LedgerService(db, f.Clock),
        new SpendWise.Application.Features.Banking.ActiveBankGuard(db),
        Microsoft.Extensions.Options.Options.Create(new V1ElectionPolicy
        {
            Version = "SYNTHETIC-TEST-POLICY",
            RegistrationSeconds = 60,
            NominationSeconds = 60,
            RunoffSeconds = 60,
            ConfirmationSeconds = 60,
            AcceptanceSeconds = 60,
            FreezeMembership = true,
            AllowContributions = false,
            NoCandidatesOutcome = "Fail",
            NoVotesOutcome = "Fail",
            AcceptanceExpiryOutcome = "Fail",
            EligibilityChangeOutcome = "Fail"
        }),
        new TestInactivity());

    [Fact]
    public async Task F9_ReplacementMember_FullFlow_Reconfirm_Contribute_CompleteFund_OpenSpending()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("OwnerF9");
        var departedMember = await f.AddUserAsync("DepartedF9");
        var replacementUser = await f.AddUserAsync("ReplacementF9");
        var merchant = await f.AddUserAsync("MerchantF9");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, departedMember.Id);
        await V1FundMembershipTests.MakeFriends(f, owner.Id, replacementUser.Id);

        var service = V1ElectionTests.Configured(f);
        var fund = await service.CreateAsync(owner.Id, new("FundF9", "Fixed", 4_000_000, Pin, 24, true, "v2.0"), default);
        var inv1 = await service.InviteAsync(owner.Id, fund.Id, departedMember.Id, default);
        await service.AcceptAsync(departedMember.Id, inv1.Id, true, "v2.0", default);
        await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(4_000_000, Pin), default);

        // Record initial signature from DB with fresh context
        await using (var dbInit = f.CreateNewDbContext())
        {
            var initialTreasury = await dbInit.V1FundTreasuries.AsNoTracking().SingleAsync(t => t.FundId == fund.Id);
            Assert.NotNull(initialTreasury.MembershipSignature);
        }

        // Departed member leaves -> seat 2 becomes vacant
        var departedMembership = await f.Db.V1FundMemberships.SingleAsync(m => m.FundId == fund.Id && m.UserId == departedMember.Id);
        departedMembership.LeftAt = f.Clock.UtcNow;
        await f.Db.SaveChangesAsync();

        // Create replacement invite for seat 2
        var replacementInvite = new V1FundInvitation
        {
            FundId = fund.Id,
            InvitedUserId = replacementUser.Id,
            InvitedByUserId = owner.Id,
            Status = V1InvitationStatus.Pending,
            ExpiresAt = f.Clock.UtcNow.AddDays(7),
            IsReplacement = true,
            TargetSeat = 2
        };
        f.Db.V1FundInvitations.Add(replacementInvite);
        await f.Db.SaveChangesAsync();

        // Replacement member accepts -> gets their own episode obligation
        await service.AcceptAsync(replacementUser.Id, replacementInvite.Id, true, "v2.0", default);

        // Reconfirm obligations: updates membership signature and persists it to database
        var reconfirmed = await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(4_000_000, Pin), default);
        Assert.True(reconfirmed.ConfigurationCurrent);

        // Verify with FRESH DbContext: signature is persisted in database and matches SHA256 of active members
        await using (var dbCheck = f.CreateNewDbContext())
        {
            var dbTreasury = await dbCheck.V1FundTreasuries.AsNoTracking().SingleAsync(t => t.FundId == fund.Id);
            Assert.NotNull(dbTreasury.MembershipSignature);
            Assert.Equal(64, dbTreasury.MembershipSignature.Length);

            var activeMembersDb = await dbCheck.V1FundMemberships.AsNoTracking()
                .Where(m => m.FundId == fund.Id && m.LeftAt == null)
                .OrderBy(m => m.Seat)
                .ToListAsync();
            var expectedSig = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(string.Join("|", activeMembersDb.Select(m => $"{m.Id:N}:{m.UserId:N}:{m.Seat}")))));
            Assert.Equal(expectedSig, dbTreasury.MembershipSignature);
        }

        // Setup wallets and fund them
        var oWallet = await f.AddWalletAsync(owner.Id, "OWalletF9", true, balance: 5_000_000);
        var rWallet = await f.AddWalletAsync(replacementUser.Id, "RWalletF9", true, balance: 5_000_000);
        await f.AddWalletAsync(merchant.Id, "MerchWalletF9", true, balance: 0);

        // MULTI-DBCONTEXT TEST: Perform contributions using a BRAND NEW DbContext
        await using (var dbContribute = f.CreateNewDbContext())
        {
            var serviceContribute = CreateService(dbContribute, f);
            await serviceContribute.ContributeAsync(owner.Id, fund.Id, new(oWallet.Id, 2_000_000, "c-owner-f9", Pin, reconfirmed.Revision), default);
            await serviceContribute.ContributeAsync(replacementUser.Id, fund.Id, new(rWallet.Id, 2_000_000, "c-rep-f9", Pin, reconfirmed.Revision), default);
        }

        // MULTI-DBCONTEXT TEST: Verify finance state and execute expense using another BRAND NEW DbContext
        await using (var dbSpend = f.CreateNewDbContext())
        {
            var serviceSpend = CreateService(dbSpend, f);
            var finance = await serviceSpend.FinanceAsync(owner.Id, fund.Id, default);
            Assert.Equal(2, finance.Obligations.Count);
            Assert.All(finance.Obligations, o => Assert.Equal(0, o.RemainingVnd));
            Assert.Equal(4_000_000, finance.BalanceVnd);
            Assert.Equal("OPEN", finance.SpendingState);

            var exp = new V1FundRequestInput("Expense", 1_000_000, merchant.Id, "Team expense after replacement", "exp-f9-01", Pin);
            var req = await serviceSpend.CreateFundRequestAsync(owner.Id, fund.Id, exp, default);
            Assert.NotNull(req);
            Assert.Equal("Executed", req.ExecutionAvailability);
        }
    }

    [Fact]
    public async Task F10_MultipleReplacementsOnSameSeat_AndPartialPaidDeparture()
    {
        var clock = new MutableClock(new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc));
        await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);
        var owner = await f.AddUserAsync("OwnerF10");
        var departed1 = await f.AddUserAsync("Departed1F10");
        var departed2 = await f.AddUserAsync("Departed2F10");
        var finalUser = await f.AddUserAsync("FinalUserF10");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, departed1.Id);
        await V1FundMembershipTests.MakeFriends(f, owner.Id, departed2.Id);
        await V1FundMembershipTests.MakeFriends(f, owner.Id, finalUser.Id);

        var service = V1ElectionTests.Configured(f);
        var fund = await service.CreateAsync(owner.Id, new("FundF10", "Fixed", 4_000_000, Pin, 24, true, "v2.0"), default);
        var inv1 = await service.InviteAsync(owner.Id, fund.Id, departed1.Id, default);
        await service.AcceptAsync(departed1.Id, inv1.Id, true, "v2.0", default);
        var config = await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(4_000_000, Pin), default);

        // Departed1 contributes partial (500,000 VND)
        var d1Wallet = await f.AddWalletAsync(departed1.Id, "D1Wallet", true, balance: 1_000_000);
        await service.ContributeAsync(departed1.Id, fund.Id, new(d1Wallet.Id, 500_000, "c-part-d1", Pin, config.Revision), default);

        // Advance clock past personal 24h deadline so kick is permitted
        clock.UtcNow += TimeSpan.FromHours(25);

        // Departed1 leaves with REAL REFUND through KickMemberAsync (voluntary leave disallowed in Fixed Fund during normal operation)
        var refund1 = await service.KickMemberAsync(owner.Id, fund.Id, departed1.Id, new(Pin, "kick-d1-f10", "Seat 2 departed 1"), default);
        Assert.Equal("Completed", refund1.Status);
        Assert.Equal(500_000, refund1.RefundAmountVnd);

        // Verify ledger balances on fresh DbContext
        await using (var dbVerifyD1 = f.CreateNewDbContext())
        {
            var d1Acc = await dbVerifyD1.LedgerAccounts.AsNoTracking().SingleAsync(a => a.WalletId == d1Wallet.Id);
            Assert.Equal(1_000_000, d1Acc.Balance); // Full initial wallet balance restored!

            var treasury = await dbVerifyD1.V1FundTreasuries.AsNoTracking().SingleAsync(t => t.FundId == fund.Id);
            var treasuryAcc = await dbVerifyD1.LedgerAccounts.AsNoTracking().SingleAsync(a => a.Id == treasury.LedgerAccountId);
            Assert.Equal(0, treasuryAcc.Balance); // Treasury balance refunded to 0!
        }

        // Departed2 replaces seat 2
        var inv2 = new V1FundInvitation
        {
            FundId = fund.Id,
            InvitedUserId = departed2.Id,
            InvitedByUserId = owner.Id,
            Status = V1InvitationStatus.Pending,
            ExpiresAt = f.Clock.UtcNow.AddDays(7),
            IsReplacement = true,
            TargetSeat = 2
        };
        f.Db.V1FundInvitations.Add(inv2);
        await f.Db.SaveChangesAsync();
        await service.AcceptAsync(departed2.Id, inv2.Id, true, "v2.0", default);

        // Departed2 also leaves without contributing (refund 0)
        var d2Wallet = await f.AddWalletAsync(departed2.Id, "D2Wallet", true, balance: 100_000);
        clock.UtcNow += TimeSpan.FromHours(25);
        var refund2 = await service.KickMemberAsync(owner.Id, fund.Id, departed2.Id, new(Pin, "kick-d2-f10", "Seat 2 departed 2"), default);
        Assert.Equal("Completed", refund2.Status);
        Assert.Equal(0, refund2.RefundAmountVnd);

        // FinalUser replaces seat 2
        var inv3 = new V1FundInvitation
        {
            FundId = fund.Id,
            InvitedUserId = finalUser.Id,
            InvitedByUserId = owner.Id,
            Status = V1InvitationStatus.Pending,
            ExpiresAt = f.Clock.UtcNow.AddDays(7),
            IsReplacement = true,
            TargetSeat = 2
        };
        f.Db.V1FundInvitations.Add(inv3);
        await f.Db.SaveChangesAsync();
        await service.AcceptAsync(finalUser.Id, inv3.Id, true, "v2.0", default);

        // Verification of database records on FRESH DbContext:
        await using (var dbFinal = f.CreateNewDbContext())
        {
            // Exactly 3 historical/active obligations for Seat 2: Departed1, Departed2, FinalUser
            var seat2Obligations = await dbFinal.V1FundObligations.AsNoTracking()
                .Where(o => o.FundId == fund.Id && o.Seat == 2)
                .ToListAsync();
            Assert.Equal(3, seat2Obligations.Count);
            Assert.Contains(seat2Obligations, o => o.UserId == departed1.Id);
            Assert.Contains(seat2Obligations, o => o.UserId == departed2.Id);
            Assert.Contains(seat2Obligations, o => o.UserId == finalUser.Id);

            // Verification of active Finance state on fresh DbContext:
            var finalService = CreateService(dbFinal, f);
            var finance = await finalService.FinanceAsync(owner.Id, fund.Id, default);
            Assert.Equal(2, finance.Obligations.Count);
            var activeSeat2 = finance.Obligations.Single(o => o.Seat == 2);
            Assert.Equal(finalUser.Id, activeSeat2.UserId);
            Assert.Equal(2_000_000, activeSeat2.RequiredVnd);
        }
    }
}
