using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Features.Friends;
using SpendWise.Domain.Groups;
using SpendWise.Domain.Identity;
using SpendWise.Domain.V1Groups;
using Xunit;

namespace SpendWise.Tests;

public sealed class V1FriendDiscoveryTests
{
    [Fact]
    public async Task Exact_SpendId_search_is_case_insensitive_and_does_not_disclose_stranger_phone()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var actor = await f.AddUserAsync("Actor"); var target = await f.AddUserAsync("Target");
        target.Phone = "0901234567"; await f.Db.SaveChangesAsync();
        var result = await new SearchFriendContactQueryHandler(f.Db).Handle(new(actor.Id, target.SpendId.ToLowerInvariant()), default);
        Assert.Equal(target.Id, result.UserId);
        Assert.Null(result.Phone);
        Assert.Equal("NONE", result.State);
    }

    [Theory]
    [InlineData(SystemRole.Admin)]
    [InlineData(SystemRole.SuperAdmin)]
    [InlineData(SystemRole.SystemAdmin)]
    public async Task SpendId_search_does_not_expose_platform_operators(SystemRole role)
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var actor = await f.AddUserAsync("Actor"); var target = await f.AddUserAsync("Operator", role: role);
        await Assert.ThrowsAsync<NotFoundException>(() => new SearchFriendContactQueryHandler(f.Db).Handle(new(actor.Id, target.SpendId), default));
    }

    [Fact]
    public async Task Friend_list_returns_only_current_common_groups_without_private_or_past_memberships()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var actor = await f.AddUserAsync("Actor"); var friend = await f.AddUserAsync("Friend"); var other = await f.AddUserAsync("Other");
        await V1FundMembershipTests.MakeFriends(f, actor.Id, friend.Id);
        var friendship = await f.Db.Friendships.SingleAsync();
        f.Db.FriendPreferences.AddRange(new SpendWise.Domain.Social.FriendPreference { FriendshipId = friendship.Id, UserId = actor.Id },
            new SpendWise.Domain.Social.FriendPreference { FriendshipId = friendship.Id, UserId = friend.Id });
        var shared = new Group { Name = "Common Legacy", Members = [new GroupMember { UserId = actor.Id, Role = GroupRole.Owner }, new GroupMember { UserId = friend.Id }] };
        var privateGroup = new Group { Name = "Private other", Members = [new GroupMember { UserId = friend.Id, Role = GroupRole.Owner }, new GroupMember { UserId = other.Id }] };
        f.Db.Groups.AddRange(shared, privateGroup);
        var fund = new V1Fund("Common V1", "SPW-SYNTHETIC", V1FundKind.Free, actor.Id, null);
        f.Db.V1Funds.Add(fund);
        f.Db.V1FundMemberships.AddRange(new V1FundMembership { FundId = fund.Id, UserId = actor.Id, Seat = 1 },
            new V1FundMembership { FundId = fund.Id, UserId = friend.Id, Seat = 2 });
        await f.Db.SaveChangesAsync();
        var handler = new GetFriendsQueryHandler(f.Db);
        var result = Assert.Single(await handler.Handle(new(actor.Id), default));
        var json = JsonSerializer.SerializeToElement(result);
        Assert.True(json.TryGetProperty("CommonGroups", out var groups));
        Assert.Equal(2, groups.GetArrayLength());
        Assert.Contains(groups.EnumerateArray(), g => g.GetProperty("Name").GetString() == shared.Name);
        Assert.DoesNotContain(groups.EnumerateArray(), g => g.GetProperty("Name").GetString() == privateGroup.Name);
        await f.Db.V1FundMemberships.Where(m => m.UserId == friend.Id).ExecuteUpdateAsync(s => s.SetProperty(m => m.LeftAt, f.Clock.UtcNow));
        await f.Db.Groups.Where(g => g.Id == shared.Id).ExecuteUpdateAsync(s => s.SetProperty(g => g.Status, GroupStatus.Closed));
        var after = JsonSerializer.SerializeToElement(Assert.Single(await handler.Handle(new(actor.Id), default)));
        Assert.Equal(0, after.GetProperty("CommonGroups").GetArrayLength());
    }
}
