using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Domain.V1Groups;
using Xunit;

namespace SpendWise.Tests;

public sealed class V1InvitationLifecycleTests
{
    [Fact]
    public async Task Fixed_member_proposes_without_notifying_target_or_adding_membership()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Owner");
        var member = await f.AddUserAsync("Member");
        var target = await f.AddUserAsync("Target");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);
        await V1FundMembershipTests.MakeFriends(f, member.Id, target.Id);
        var s = V1FundMembershipTests.Service(f);
        var fund = await s.CreateAsync(owner.Id, new("Fixed", "Fixed", 100, TargetedCorrectnessFixture.SecurityPin), default);
        var first = await s.InviteAsync(owner.Id, fund.Id, member.Id, default);
        await s.AcceptAsync(member.Id, first.Id, default);
        var proposal = await s.InviteAsync(member.Id, fund.Id, target.Id, default);
        Assert.Equal("Proposed", proposal.Status);
        Assert.Empty(await s.InvitationsAsync(target.Id, default));
        Assert.False(await f.Db.V1FundMemberships.AnyAsync(m => m.UserId == target.Id));
        Assert.False(await f.Db.Notifications.AnyAsync(n => n.UserId == target.Id));
        Assert.True(await f.Db.Notifications.AnyAsync(n => n.UserId == owner.Id));
        Assert.True(await f.Db.V1FundActivities.AnyAsync(a => a.Event == "InvitationProposed" && a.ActorUserId == member.Id));
    }

    [Fact]
    public async Task Replayed_invite_and_accept_do_not_duplicate_persisted_notifications_or_outbox()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Owner");
        var target = await f.AddUserAsync("Target");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, target.Id);
        var s = V1FundMembershipTests.Service(f);
        var fund = await s.CreateAsync(owner.Id, new("Free", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);
        var invite = await s.InviteAsync(owner.Id, fund.Id, target.Id, default);
        Assert.Single(await f.Db.Notifications.Where(n => n.UserId == target.Id).ToListAsync());
        Assert.Equal(invite.Id, (await s.InviteAsync(owner.Id, fund.Id, target.Id, default)).Id);
        await s.AcceptAsync(target.Id, invite.Id, default);
        var count = await f.Db.Notifications.CountAsync();
        Assert.True(count >= 2);
        await s.AcceptAsync(target.Id, invite.Id, default);
        Assert.Equal(count, await f.Db.Notifications.CountAsync());
        Assert.Equal(count, await f.Db.OutboxMessages.CountAsync());
        Assert.Empty(await f.Db.LedgerTransactions.ToListAsync());
    }

    [Fact]
    public async Task Only_owner_can_approve_proposal_then_only_recipient_can_join()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Owner"); var member = await f.AddUserAsync("Member"); var target = await f.AddUserAsync("Target");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);
        await V1FundMembershipTests.MakeFriends(f, member.Id, target.Id);
        var s = V1FundMembershipTests.Service(f);
        var fund = await s.CreateAsync(owner.Id, new("Fixed", "Fixed", 100, TargetedCorrectnessFixture.SecurityPin), default);
        await s.AcceptAsync(member.Id, (await s.InviteAsync(owner.Id, fund.Id, member.Id, default)).Id, default);
        var proposal = await s.InviteAsync(member.Id, fund.Id, target.Id, default);
        await Assert.ThrowsAsync<ConflictException>(() => s.AcceptAsync(target.Id, proposal.Id, default));
        await Assert.ThrowsAsync<ForbiddenException>(() => s.ReviewInvitationAsync(member.Id, proposal.Id, true, default));
        Assert.Equal(proposal.Id, Assert.Single(await s.InvitationProposalsAsync(owner.Id, fund.Id, 1, 20, default)).Id);
        Assert.Equal("Pending", (await s.ReviewInvitationAsync(owner.Id, proposal.Id, true, default)).Status);
        Assert.Single(await s.InvitationsAsync(target.Id, default));
        var count = await f.Db.Notifications.CountAsync();
        await s.ReviewInvitationAsync(owner.Id, proposal.Id, true, default);
        Assert.Equal(count, await f.Db.Notifications.CountAsync());
        Assert.False(await f.Db.V1FundMemberships.AnyAsync(m => m.UserId == target.Id));
        await s.AcceptAsync(target.Id, proposal.Id, default);
        Assert.True(await f.Db.V1FundMemberships.AnyAsync(m => m.UserId == target.Id && !m.CanSpend));
        Assert.True(await f.Db.V1FundActivities.AnyAsync(a => a.Event == "InvitationApproved" && a.ActorUserId == owner.Id));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Decline_or_cancel_is_authorized_terminal_and_replay_does_not_notify(bool cancel)
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Owner"); var target = await f.AddUserAsync("Target"); var stranger = await f.AddUserAsync("Stranger");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, target.Id);
        var s = V1FundMembershipTests.Service(f);
        var fund = await s.CreateAsync(owner.Id, new("Free", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);
        var invite = await s.InviteAsync(owner.Id, fund.Id, target.Id, default);
        await Assert.ThrowsAsync<NotFoundException>(() => s.ResolveInvitationAsync(stranger.Id, invite.Id, cancel, default));
        var actor = cancel ? owner.Id : target.Id;
        Assert.Equal(cancel ? "Cancelled" : "Rejected", (await s.ResolveInvitationAsync(actor, invite.Id, cancel, default)).Status);
        var count = await f.Db.Notifications.CountAsync();
        await s.ResolveInvitationAsync(actor, invite.Id, cancel, default);
        Assert.Equal(count, await f.Db.Notifications.CountAsync());
        await Assert.ThrowsAsync<ConflictException>(() => s.AcceptAsync(target.Id, invite.Id, default));
        Assert.Single(await f.Db.V1FundMemberships.ToListAsync());
        var replacement = await s.InviteAsync(owner.Id, fund.Id, target.Id, default);
        Assert.NotEqual(invite.Id, replacement.Id);
    }

    [Fact]
    public async Task Approval_rechecks_capacity_and_rejecting_proposal_never_invites_target()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Owner"); var member = await f.AddUserAsync("Member"); var target = await f.AddUserAsync("Target");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);
        await V1FundMembershipTests.MakeFriends(f, member.Id, target.Id);
        var s = V1FundMembershipTests.Service(f);
        var fund = await s.CreateAsync(owner.Id, new("Fixed", "Fixed", 100, TargetedCorrectnessFixture.SecurityPin), default);
        await s.AcceptAsync(member.Id, (await s.InviteAsync(owner.Id, fund.Id, member.Id, default)).Id, default);
        var proposal = await s.InviteAsync(member.Id, fund.Id, target.Id, default);
        await V1LegacyAdmissionTests.SeedV1Async(f, target.Id, 10);
        var error = await Assert.ThrowsAsync<ConflictException>(() => s.ReviewInvitationAsync(owner.Id, proposal.Id, true, default));
        Assert.Equal("active_group_limit", error.Code);
        Assert.Equal("Rejected", (await s.ReviewInvitationAsync(owner.Id, proposal.Id, false, default)).Status);
        Assert.False(await f.Db.Notifications.AnyAsync(n => n.UserId == target.Id));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Ineligible_participant_blocks_approval_but_cannot_prevent_owner_rejection(bool suspendProposer)
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Owner"); var member = await f.AddUserAsync("Member"); var target = await f.AddUserAsync("Target");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);
        await V1FundMembershipTests.MakeFriends(f, member.Id, target.Id);
        var s = V1FundMembershipTests.Service(f);
        var fund = await s.CreateAsync(owner.Id, new("Fixed", "Fixed", 100, TargetedCorrectnessFixture.SecurityPin), default);
        await s.AcceptAsync(member.Id, (await s.InviteAsync(owner.Id, fund.Id, member.Id, default)).Id, default);
        var proposal = await s.InviteAsync(member.Id, fund.Id, target.Id, default);
        var suspendedId = suspendProposer ? member.Id : target.Id;
        await f.Db.Users.Where(u => u.Id == suspendedId).ExecuteUpdateAsync(s => s.SetProperty(u => u.Status, SpendWise.Domain.Identity.UserStatus.Suspended));
        await Assert.ThrowsAsync<ForbiddenException>(() => s.ReviewInvitationAsync(owner.Id, proposal.Id, true, default));
        Assert.Equal("Rejected", (await s.ReviewInvitationAsync(owner.Id, proposal.Id, false, default)).Status);
        Assert.False(await f.Db.V1FundMemberships.AnyAsync(m => m.UserId == target.Id));
    }
}
