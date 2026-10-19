using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Features.Friends;
using SpendWise.Domain.Social;
using SpendWise.Infrastructure.Persistence;
using Xunit;

namespace SpendWise.Tests;

public sealed class V1FriendResolutionTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_stale_pending_request_cannot_cancel_or_reject_a_completed_acceptance(bool cancel)
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var a = await f.AddUserAsync("A");
        var b = await f.AddUserAsync("B");
        var request = new FriendRequest { RequesterUserId = a.Id, AddresseeUserId = b.Id,
            LowerUserId = a.Id.CompareTo(b.Id) < 0 ? a.Id : b.Id, HigherUserId = a.Id.CompareTo(b.Id) < 0 ? b.Id : a.Id };
        f.Db.FriendRequests.Add(request);
        await f.Db.SaveChangesAsync();
        await using (var fresh = new SpendWiseDbContext(new DbContextOptionsBuilder<SpendWiseDbContext>()
            .UseSqlite(f.Db.Database.GetDbConnection()).Options, f.Clock))
            await new AcceptFriendRequestCommandHandler(fresh, f.Clock, new V1FriendLimitTests.CapturingNotifications())
                .Handle(new(b.Id, request.Id), default);
        var error = await Assert.ThrowsAsync<ConflictException>(async () =>
        {
            if (cancel) await new CancelFriendRequestCommandHandler(f.Db, f.Clock).Handle(new(a.Id, request.Id), default);
            else await new RejectFriendRequestCommandHandler(f.Db, f.Clock, new V1FriendLimitTests.CapturingNotifications()).Handle(new(b.Id, request.Id), default);
        });
        Assert.Equal("friend_request_unavailable", error.Code);
        Assert.Equal(FriendRequestStatus.Accepted, await f.Db.FriendRequests.Where(r => r.Id == request.Id).Select(r => r.Status).SingleAsync());
        Assert.Single(await f.Db.Friendships.ToListAsync());
    }
}
