using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Features.Groups;
using SpendWise.Application.Features.Groups.Commands.AcceptGroupInvite;
using SpendWise.Application.Features.Groups.Commands.CreateGroup;
using SpendWise.Domain.Groups;
using SpendWise.Domain.V1Groups;
using SpendWise.Domain.Identity;
using SpendWise.Domain.Social;
using SpendWise.Infrastructure.Persistence;
using SpendWise.Infrastructure.Services;
using Xunit;

namespace SpendWise.Tests;

public sealed class V1LegacyAdmissionTests
{
    [Fact]
    public async Task Legacy_create_cannot_bypass_ten_V1_memberships_or_create_ledger_account()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var user = await f.AddUserAsync("Synthetic User");
        await SeedV1Async(f, user.Id, 10);
        var handler = new CreateGroupCommandHandler(f.Db, f.Clock, new GroupAccess(f.Db), f.Security);
        var error = await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(new(user.Id,
            new("Eleventh", null, "Family", SecurityPin: TargetedCorrectnessFixture.SecurityPin)), default));
        Assert.Equal("active_group_limit", error.Code);
        Assert.Empty(await f.Db.Groups.ToListAsync());
        Assert.Empty(await f.Db.LedgerAccounts.ToListAsync());
    }

    [Fact]
    public async Task Legacy_accept_counts_V1_and_preserves_pending_invite_when_full()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Owner");
        var user = await f.AddUserAsync("Invitee");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, user.Id);
        await SeedV1Async(f, user.Id, 10);
        var group = new Group { Name = "Legacy", Members = [new GroupMember { UserId = owner.Id, Role = GroupRole.Owner }] };
        f.Db.Groups.Add(group);
        var invite = new GroupInvite { GroupId = group.Id, InvitedByUserId = owner.Id, InvitedUserId = user.Id,
            Token = "synthetic-invite", ExpiresAt = f.Clock.UtcNow.AddDays(1) };
        f.Db.GroupInvites.Add(invite);
        await f.Db.SaveChangesAsync();
        var handler = new AcceptGroupInviteCommandHandler(f.Db, f.Clock, new GroupAccess(f.Db));
        var error = await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(new(user.Id, invite.Id, invite.Token), default));
        Assert.Equal("active_group_limit", error.Code);
        Assert.False(await f.Db.GroupMembers.AnyAsync(m => m.UserId == user.Id));
        Assert.Equal(InviteStatus.Pending, await f.Db.GroupInvites.AsNoTracking().Where(i => i.Id == invite.Id).Select(i => i.Status).SingleAsync());
        Assert.Empty(await f.Db.GroupActivities.ToListAsync());
    }

    [Fact]
    public async Task Mixed_nine_then_Legacy_create_allows_tenth_but_blocks_eleventh()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var user = await f.AddUserAsync("Owner");
        await SeedV1Async(f, user.Id, 9);
        var handler = new CreateGroupCommandHandler(f.Db, f.Clock, new GroupAccess(f.Db), f.Security);
        var created = await handler.Handle(new(user.Id, new("Tenth", null, "Trip", SecurityPin: TargetedCorrectnessFixture.SecurityPin)), default);
        Assert.Equal("Owner", created.CurrentUserRole);
        var error = await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(new(user.Id,
            new("Eleventh", null, "Trip", SecurityPin: TargetedCorrectnessFixture.SecurityPin)), default));
        Assert.Equal("active_group_limit", error.Code);
        Assert.Single(await f.Db.Groups.ToListAsync());
    }

    [Fact]
    public async Task Closed_Legacy_and_dissolved_V1_do_not_consume_capacity()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var user = await f.AddUserAsync("Owner");
        await SeedV1Async(f, user.Id, 9);
        await f.Db.V1Funds.ExecuteUpdateAsync(s => s.SetProperty(g => g.DissolvedAt, f.Clock.UtcNow));
        for (var i = 0; i < 10; i++) f.Db.Groups.Add(new Group { Name = "Closed", Status = GroupStatus.Closed,
            Members = [new GroupMember { UserId = user.Id, Role = GroupRole.Owner }] });
        await f.Db.SaveChangesAsync();
        var handler = new CreateGroupCommandHandler(f.Db, f.Clock, new GroupAccess(f.Db), f.Security);
        await handler.Handle(new(user.Id, new("Active", null, "Trip", SecurityPin: TargetedCorrectnessFixture.SecurityPin)), default);
        Assert.Equal(11, await f.Db.Groups.CountAsync());
    }

    internal static async Task SeedV1Async(TargetedCorrectnessFixture f, Guid userId, int count)
    {
        for (var i = 0; i < count; i++)
        {
            var fund = new V1Fund("Synthetic", "SPW" + Guid.NewGuid().ToString("N"), V1FundKind.Free, userId, null);
            f.Db.V1Funds.Add(fund);
            f.Db.V1FundMemberships.Add(new V1FundMembership { FundId = fund.Id, UserId = userId, Seat = 1, JoinedAt = f.Clock.UtcNow });
        }
        await f.Db.SaveChangesAsync();
    }

    [Fact]
    public async Task Stale_Legacy_rejection_cannot_overwrite_accepted_invitation()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Owner"); var user = await f.AddUserAsync("Invitee");
        var group = new Group { Name = "Legacy", Members = [new GroupMember { UserId = owner.Id, Role = GroupRole.Owner }] };
        f.Db.Groups.Add(group);
        var invite = new GroupInvite { GroupId = group.Id, InvitedByUserId = owner.Id, InvitedUserId = user.Id,
            Token = "synthetic-invite", ExpiresAt = f.Clock.UtcNow.AddDays(1) };
        f.Db.GroupInvites.Add(invite); await f.Db.SaveChangesAsync();
        await f.Db.GroupInvites.Where(i => i.Id == invite.Id).ExecuteUpdateAsync(s => s.SetProperty(i => i.Status, InviteStatus.Accepted));
        var handler = new RejectGroupInviteCommandHandler(f.Db);
        await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(new(user.Id, invite.Id), default));
        Assert.Equal(InviteStatus.Accepted, await f.Db.GroupInvites.AsNoTracking().Where(i => i.Id == invite.Id).Select(i => i.Status).SingleAsync());
    }

    [Fact]
    public async Task Legacy_accept_still_works_with_InMemory_test_provider()
    {
        await using var db = new SpendWiseDbContext(new DbContextOptionsBuilder<SpendWiseDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, new SystemClock());
        var owner = new User { FullName = "Synthetic owner" }; var user = new User { FullName = "Synthetic invitee" };
        var group = new Group { Name = "Legacy", Members = [new GroupMember { UserId = owner.Id, Role = GroupRole.Owner }] };
        var lower = owner.Id.CompareTo(user.Id) < 0 ? owner.Id : user.Id;
        var higher = lower == owner.Id ? user.Id : owner.Id;
        db.AddRange(owner, user, group, new Friendship { LowerUserId = lower, HigherUserId = higher });
        var invite = new GroupInvite { GroupId = group.Id, InvitedByUserId = owner.Id, InvitedUserId = user.Id,
            Token = "synthetic-invite", ExpiresAt = DateTime.UtcNow.AddDays(1) };
        db.Add(invite); await db.SaveChangesAsync();
        var result = await new AcceptGroupInviteCommandHandler(db, new SystemClock(), new GroupAccess(db))
            .Handle(new(user.Id, invite.Id, invite.Token), default);
        Assert.Equal(group.Id, result.Id);
        Assert.Equal(InviteStatus.Accepted, (await db.GroupInvites.SingleAsync()).Status);
        Assert.Equal(2, await db.GroupMembers.CountAsync());
    }
}
