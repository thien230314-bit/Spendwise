using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Features.V1Groups;
using SpendWise.Domain.V1Groups;
using SpendWise.Domain.Social;
using Xunit;

namespace SpendWise.Tests;

public sealed class V1FundMembershipTests
{
    [Fact]
    public async Task New_funds_are_versioned_have_global_codes_and_do_not_modify_legacy_or_money()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Founder");
        var service = Service(f);
        var fixedFund = await service.CreateAsync(owner.Id, new("Fixed", "Fixed", 101, TargetedCorrectnessFixture.SecurityPin), default);
        var free = await service.CreateAsync(owner.Id, new("Free", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);
        Assert.Equal("SPW000001", fixedFund.Code);
        Assert.Equal("SPW000002", free.Code);
        Assert.Equal(owner.Id, fixedFund.FounderUserId);
        Assert.Equal(owner.Id, fixedFund.OwnerUserId);
        Assert.Equal("CONTRIBUTIONS_ONLY", fixedFund.FinanceAvailability);
        Assert.Empty(await f.Db.Groups.ToListAsync());
        Assert.Empty(await f.Db.LedgerTransactions.ToListAsync());
        Assert.Empty(await f.Db.Wallets.ToListAsync());
    }

    [Theory]
    [InlineData("Fixed", 0L)]
    [InlineData("Fixed", -1L)]
    [InlineData("Free", 100L)]
    [InlineData("Family", 100L)]
    [InlineData("999", 100L)]
    public async Task Invalid_kind_or_target_does_not_allocate_a_code(string kind, long target)
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Founder");
        await Assert.ThrowsAsync<AppException>(() => Service(f).CreateAsync(owner.Id,
            new("Invalid", kind, target, TargetedCorrectnessFixture.SecurityPin), default));
        Assert.Empty(await f.Db.V1Funds.ToListAsync());
        Assert.Equal(1, await f.Db.V1FundCodeCounters.Select(c => c.NextValue).SingleAsync());
    }

    [Fact]
    public async Task Invite_requires_friend_owner_and_explicit_target_acceptance_then_permission_defaults_off()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Founder");
        var member = await f.AddUserAsync("Member");
        var outsider = await f.AddUserAsync("Outsider");
        await MakeFriends(f, owner.Id, member.Id);
        var service = Service(f);
        var fund = await service.CreateAsync(owner.Id, new("Fixed", "Fixed", 101, TargetedCorrectnessFixture.SecurityPin), default);
        var invite = await service.InviteAsync(owner.Id, fund.Id, member.Id, default);
        Assert.Equal(1, await f.Db.V1FundMemberships.CountAsync());
        await Assert.ThrowsAsync<NotFoundException>(() => service.AcceptAsync(outsider.Id, invite.Id, default));
        await service.AcceptAsync(member.Id, invite.Id, default);
        await service.AcceptAsync(member.Id, invite.Id, default);
        var membership = await f.Db.V1FundMemberships.SingleAsync(m => m.UserId == member.Id);
        Assert.False(membership.CanSpend);
        Assert.Equal(2, membership.Seat);
        await Assert.ThrowsAsync<ForbiddenException>(() => service.SetSpendingPermissionAsync(member.Id, fund.Id, owner.Id, true,
            TargetedCorrectnessFixture.SecurityPin, default));
        var conf = await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(100, TargetedCorrectnessFixture.SecurityPin), default);
        var mWallet = await f.AddWalletAsync(member.Id, "MWallet", true, balance: 1000);
        var memObligation = await f.Db.V1FundObligations.SingleAsync(o => o.FundId == fund.Id && o.Revision == conf.Revision && o.UserId == member.Id);
        await service.ContributeAsync(member.Id, fund.Id, new(mWallet.Id, memObligation.RequiredVnd, "c-mem-seed", TargetedCorrectnessFixture.SecurityPin, conf.Revision), default);
        await service.SetSpendingPermissionAsync(owner.Id, fund.Id, member.Id, true, TargetedCorrectnessFixture.SecurityPin, default);
        Assert.True(await f.Db.V1FundMemberships.Where(m => m.UserId == member.Id).Select(m => m.CanSpend).SingleAsync());
        await service.SetSpendingPermissionAsync(owner.Id, fund.Id, member.Id, false, TargetedCorrectnessFixture.SecurityPin, default);
        Assert.False(await f.Db.V1FundMemberships.Where(m => m.UserId == member.Id).Select(m => m.CanSpend).SingleAsync());
        Assert.Single(await f.Db.LedgerTransactions.ToListAsync());
        Assert.Equal(2, await f.Db.V1FundActivities.CountAsync(a => a.Event == "SpendingPermissionChanged"));
    }

    [Fact]
    public async Task Free_fund_does_not_inherit_fixed_spending_permission()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Founder");
        var service = Service(f);
        var fund = await service.CreateAsync(owner.Id, new("Free", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);
        await Assert.ThrowsAsync<ConflictException>(() => service.SetSpendingPermissionAsync(owner.Id, fund.Id, owner.Id, true,
            TargetedCorrectnessFixture.SecurityPin, default));
    }

    [Fact]
    public async Task Ownership_changes_only_after_recipient_acceptance_and_founder_is_preserved()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var founder = await f.AddUserAsync("Founder");
        var recipient = await f.AddUserAsync("Next owner");
        await MakeFriends(f, founder.Id, recipient.Id);
        var service = Service(f);
        var fund = await service.CreateAsync(founder.Id, new("Free", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);
        var invitation = await service.InviteAsync(founder.Id, fund.Id, recipient.Id, default);
        await service.AcceptAsync(recipient.Id, invitation.Id, default);
        var offer = await service.OfferOwnershipAsync(founder.Id, fund.Id, recipient.Id, TargetedCorrectnessFixture.SecurityPin, default);
        Assert.Equal(offer.Id, Assert.Single(await service.OwnershipOffersAsync(recipient.Id, default)).Id);
        Assert.Empty(await service.OwnershipOffersAsync(founder.Id, default));
        Assert.Equal(founder.Id, (await service.GetAsync(founder.Id, fund.Id, default)).OwnerUserId);
        await Assert.ThrowsAsync<NotFoundException>(() => service.AcceptOwnershipAsync(founder.Id, offer.Id, TargetedCorrectnessFixture.SecurityPin, default));
        await service.AcceptOwnershipAsync(recipient.Id, offer.Id, TargetedCorrectnessFixture.SecurityPin, default);
        Assert.Empty(await service.OwnershipOffersAsync(recipient.Id, default));
        await service.AcceptOwnershipAsync(recipient.Id, offer.Id, TargetedCorrectnessFixture.SecurityPin, default);
        var current = await service.GetAsync(recipient.Id, fund.Id, default);
        Assert.Equal(recipient.Id, current.OwnerUserId);
        Assert.Equal(founder.Id, current.FounderUserId);
        Assert.Equal(2, await f.Db.V1FundMemberships.CountAsync(m => m.LeftAt == null));
        Assert.Empty(await f.Db.LedgerTransactions.ToListAsync());
    }

    [Theory]
    [InlineData("Fixed", 30)]
    [InlineData("Free", 6)]
    public async Task Group_capacity_is_checked_again_at_acceptance(string kind, int capacity)
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Founder");
        var target = await f.AddUserAsync("Target");
        await MakeFriends(f, owner.Id, target.Id);
        var service = Service(f);
        var fund = await service.CreateAsync(owner.Id, new("Fund", kind, kind == "Fixed" ? 101 : null, TargetedCorrectnessFixture.SecurityPin), default);
        var invite = await service.InviteAsync(owner.Id, fund.Id, target.Id, default);
        for (var i = 1; i < capacity; i++)
        {
            var u = await f.AddUserAsync("Synthetic member");
            f.Db.V1FundMemberships.Add(new V1FundMembership { FundId = fund.Id, UserId = u.Id, Seat = i + 1, JoinedAt = f.Clock.UtcNow });
        }
        await f.Db.SaveChangesAsync();
        var error = await Assert.ThrowsAsync<ConflictException>(() => service.AcceptAsync(target.Id, invite.Id, default));
        Assert.Equal("fund_member_limit", error.Code);
        Assert.Equal(V1InvitationStatus.Pending, await f.Db.V1FundInvitations.Where(i => i.Id == invite.Id).Select(i => i.Status).SingleAsync());
    }

    [Fact]
    public async Task Ownership_offer_from_an_old_tenure_cannot_reactivate_when_owner_returns()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var a = await f.AddUserAsync("A");
        var b = await f.AddUserAsync("B");
        var c = await f.AddUserAsync("C");
        await MakeFriends(f, a.Id, b.Id);
        await MakeFriends(f, a.Id, c.Id);
        var s = Service(f);
        var fund = await s.CreateAsync(a.Id, new("Free", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);
        foreach (var user in new[] { b, c })
        {
            var invite = await s.InviteAsync(a.Id, fund.Id, user.Id, default);
            await s.AcceptAsync(user.Id, invite.Id, default);
        }
        var stale = await s.OfferOwnershipAsync(a.Id, fund.Id, b.Id, TargetedCorrectnessFixture.SecurityPin, default);
        var toC = await s.OfferOwnershipAsync(a.Id, fund.Id, c.Id, TargetedCorrectnessFixture.SecurityPin, default);
        await s.AcceptOwnershipAsync(c.Id, toC.Id, TargetedCorrectnessFixture.SecurityPin, default);
        var back = await s.OfferOwnershipAsync(c.Id, fund.Id, a.Id, TargetedCorrectnessFixture.SecurityPin, default);
        await s.AcceptOwnershipAsync(a.Id, back.Id, TargetedCorrectnessFixture.SecurityPin, default);
        await Assert.ThrowsAsync<ConflictException>(() => s.AcceptOwnershipAsync(b.Id, stale.Id, TargetedCorrectnessFixture.SecurityPin, default));
        Assert.Equal(a.Id, (await s.GetAsync(a.Id, fund.Id, default)).OwnerUserId);
    }

    internal static V1FundService Service(TargetedCorrectnessFixture f) => new(f.Db, f.Clock, f.Security, new SpendWise.Infrastructure.Notifications.NotificationService(f.Db), new SpendWise.Infrastructure.Finance.LedgerService(f.Db, f.Clock), new SpendWise.Application.Features.Banking.ActiveBankGuard(f.Db));

    [Fact]
    public async Task V1_admission_counts_existing_legacy_groups_toward_ten_without_changing_them()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Owner");
        for (var i = 0; i < 9; i++) f.Db.Groups.Add(new SpendWise.Domain.Groups.Group
        {
            Name = "Legacy", Members = [new SpendWise.Domain.Groups.GroupMember
                { UserId = owner.Id, Role = SpendWise.Domain.Groups.GroupRole.Owner, JoinedAt = f.Clock.UtcNow }]
        });
        await f.Db.SaveChangesAsync();
        var s = Service(f);
        await s.CreateAsync(owner.Id, new("Tenth", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);
        var error = await Assert.ThrowsAsync<ConflictException>(() => s.CreateAsync(owner.Id,
            new("Eleventh", "Free", null, TargetedCorrectnessFixture.SecurityPin), default));
        Assert.Equal("active_group_limit", error.Code);
        Assert.Equal(9, await f.Db.Groups.CountAsync());
        Assert.Equal(1, await f.Db.V1Funds.CountAsync());
        Assert.Equal(2, await f.Db.V1FundCodeCounters.Select(c => c.NextValue).SingleAsync());
    }

    [Fact]
    public async Task Historical_member_cannot_read_activity_after_their_membership_interval_or_current_fund()
    {
        var clock = new MutableClock();
        await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);
        var owner = await f.AddUserAsync("Owner");
        var member = await f.AddUserAsync("Member");
        var outsider = await f.AddUserAsync("Outsider");
        await MakeFriends(f, owner.Id, member.Id);
        var s = Service(f);
        var fund = await s.CreateAsync(owner.Id, new("Free", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);
        clock.UtcNow = clock.UtcNow.AddHours(1);
        var invitation = await s.InviteAsync(owner.Id, fund.Id, member.Id, default);
        await s.AcceptAsync(member.Id, invitation.Id, default);
        clock.UtcNow = clock.UtcNow.AddHours(1);
        await f.Db.V1FundMemberships.Where(m => m.UserId == member.Id).ExecuteUpdateAsync(u => u.SetProperty(m => m.LeftAt, clock.UtcNow));
        clock.UtcNow = clock.UtcNow.AddHours(1);
        f.Db.V1FundActivities.Add(new V1FundActivity { FundId = fund.Id, ActorUserId = owner.Id, Event = "AfterExit" });
        await f.Db.SaveChangesAsync();
        var history = await s.ActivitiesAsync(member.Id, fund.Id, 1, 20, default);
        Assert.Contains(history, a => a.Event == "Joined");
        Assert.DoesNotContain(history, a => a.Event is "AfterExit" or "Created");
        await Assert.ThrowsAsync<NotFoundException>(() => s.GetAsync(member.Id, fund.Id, default));
        await Assert.ThrowsAsync<NotFoundException>(() => s.ActivitiesAsync(outsider.Id, fund.Id, 1, 20, default));
    }

    private sealed class MutableClock : SpendWise.Application.Common.Interfaces.IClock
    {
        public DateTime UtcNow { get; set; } = new(2026, 10, 18, 0, 0, 0, DateTimeKind.Utc);
    }

    [Fact]
    public async Task New_member_reuses_smallest_vacant_seat_without_shifting_others()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Owner");
        var leaving = await f.AddUserAsync("Leaving");
        var staying = await f.AddUserAsync("Staying");
        var joining = await f.AddUserAsync("Joining");
        var s = Service(f);
        var fund = await s.CreateAsync(owner.Id, new("Fixed", "Fixed", 100, TargetedCorrectnessFixture.SecurityPin), default);
        foreach (var user in new[] { leaving, staying, joining }) await MakeFriends(f, owner.Id, user.Id);
        foreach (var user in new[] { leaving, staying })
        {
            var invite = await s.InviteAsync(owner.Id, fund.Id, user.Id, default);
            await s.AcceptAsync(user.Id, invite.Id, default);
        }
        await f.Db.V1FundMemberships.Where(m => m.UserId == leaving.Id).ExecuteUpdateAsync(u => u.SetProperty(m => m.LeftAt, f.Clock.UtcNow));
        var newInvite = await s.InviteAsync(owner.Id, fund.Id, joining.Id, default);
        await s.AcceptAsync(joining.Id, newInvite.Id, default);
        Assert.Equal(2, await f.Db.V1FundMemberships.Where(m => m.UserId == joining.Id).Select(m => m.Seat).SingleAsync());
        Assert.Equal(3, await f.Db.V1FundMemberships.Where(m => m.UserId == staying.Id).Select(m => m.Seat).SingleAsync());
        Assert.Equal(4, await f.Db.V1FundMemberships.CountAsync());
    }

    [Fact]
    public async Task Free_member_may_invite_an_accepted_friend_without_ownership()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Owner");
        var member = await f.AddUserAsync("Member");
        var target = await f.AddUserAsync("Target");
        await MakeFriends(f, owner.Id, member.Id);
        await MakeFriends(f, member.Id, target.Id);
        var s = Service(f);
        var fund = await s.CreateAsync(owner.Id, new("Free", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);
        var invitation = await s.InviteAsync(owner.Id, fund.Id, member.Id, default);
        await s.AcceptAsync(member.Id, invitation.Id, default);
        var memberInvite = await s.InviteAsync(member.Id, fund.Id, target.Id, default);
        await s.AcceptAsync(target.Id, memberInvite.Id, default);
        Assert.Equal(3, (await s.MembersAsync(owner.Id, fund.Id, default)).Count);
    }

    internal static async Task MakeFriends(TargetedCorrectnessFixture f, Guid first, Guid second)
    {
        var lower = first.CompareTo(second) < 0 ? first : second;
        var higher = lower == first ? second : first;
        var request = new FriendRequest { RequesterUserId = first, AddresseeUserId = second,
            LowerUserId = lower, HigherUserId = higher, Status = FriendRequestStatus.Accepted };
        f.Db.Friendships.Add(new Friendship { LowerUserId = lower, HigherUserId = higher,
            AcceptedRequest = request, AcceptedAt = f.Clock.UtcNow });
        await f.Db.SaveChangesAsync();
    }
}
