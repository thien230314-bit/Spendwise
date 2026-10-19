using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Features.Friends;
using SpendWise.Application.Notifications;
using SpendWise.Application.Social;
using SpendWise.Domain.Identity;
using SpendWise.Domain.Social;
using Xunit;

namespace SpendWise.Tests;

public sealed class V1FriendLimitTests
{
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public async Task Both_acceptance_paths_enforce_cap_for_both_participants(bool fullRequester, bool reciprocal)
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var sender = await f.AddUserAsync("Sender");
        var receiver = await f.AddUserAsync("Receiver");
        await SeedFriends(f, fullRequester ? sender : receiver, 100);
        var request = Request(sender.Id, receiver.Id);
        f.Db.FriendRequests.Add(request);
        await f.Db.SaveChangesAsync();
        var notifications = new CapturingNotifications();
        var exception = await Assert.ThrowsAsync<ConflictException>(async () =>
        {
            if (reciprocal)
                await new SendFriendRequestCommandHandler(f.Db, f.Clock, notifications, new(f.Db))
                    .Handle(new(receiver.Id, new SendFriendRequest(sender.Email)), default);
            else
                await new AcceptFriendRequestCommandHandler(f.Db, f.Clock, notifications)
                    .Handle(new(receiver.Id, request.Id), default);
        });
        Assert.Equal("friend_limit_reached", exception.Code);
        Assert.Equal(FriendRequestStatus.Pending, await f.Db.FriendRequests.Where(r => r.Id == request.Id).Select(r => r.Status).SingleAsync());
        Assert.Equal(100, await f.Db.Friendships.CountAsync(r => r.EndedAt == null));
        Assert.Equal(0, notifications.Count);
    }

    [Fact]
    public async Task Hundredth_friend_is_allowed_and_ended_friendships_do_not_consume_capacity()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var sender = await f.AddUserAsync("Sender");
        var receiver = await f.AddUserAsync("Receiver");
        await SeedFriends(f, receiver, 99);
        await SeedFriends(f, receiver, 2, ended: true);
        var request = Request(sender.Id, receiver.Id);
        f.Db.FriendRequests.Add(request);
        await f.Db.SaveChangesAsync();
        await new AcceptFriendRequestCommandHandler(f.Db, f.Clock, new CapturingNotifications())
            .Handle(new(receiver.Id, request.Id), default);
        Assert.Equal(100, await f.Db.Friendships.CountAsync(r => r.EndedAt == null));
    }

    [Fact]
    public async Task An_inactive_requester_cannot_become_a_new_friend()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var sender = await f.AddUserAsync("Sender", status: UserStatus.Locked);
        var receiver = await f.AddUserAsync("Receiver");
        var request = Request(sender.Id, receiver.Id);
        f.Db.FriendRequests.Add(request);
        await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<ForbiddenException>(() => new AcceptFriendRequestCommandHandler(f.Db, f.Clock, new CapturingNotifications())
            .Handle(new(receiver.Id, request.Id), default));
        Assert.Empty(await f.Db.Friendships.ToListAsync());
    }

    private static FriendRequest Request(Guid sender, Guid receiver) => new()
    {
        RequesterUserId = sender, AddresseeUserId = receiver,
        LowerUserId = sender.CompareTo(receiver) < 0 ? sender : receiver,
        HigherUserId = sender.CompareTo(receiver) < 0 ? receiver : sender
    };

    private static async Task SeedFriends(TargetedCorrectnessFixture f, User user, int count, bool ended = false)
    {
        for (var i = 0; i < count; i++)
        {
            var unique = Guid.NewGuid().ToString("N");
            var other = new User { FullName = "Synthetic", Email = unique + "@test.local", NormalizedEmail = unique.ToUpperInvariant() + "@TEST.LOCAL",
                SpendId = unique, NormalizedSpendId = unique.ToUpperInvariant(), PasswordHash = "synthetic" };
            var request = Request(user.Id, other.Id);
            request.Status = FriendRequestStatus.Accepted;
            f.Db.Users.Add(other);
            f.Db.Friendships.Add(new Friendship { LowerUserId = request.LowerUserId, HigherUserId = request.HigherUserId,
                AcceptedRequest = request, AcceptedAt = f.Clock.UtcNow, EndedAt = ended ? f.Clock.UtcNow : null });
        }
        await f.Db.SaveChangesAsync();
    }

    internal sealed class CapturingNotifications : INotificationService
    {
        public int Count { get; private set; }
        public Task<NotificationDto> CreateAsync(Guid userId, string type, string title, string message, object? data, CancellationToken ct)
        {
            Count++;
            return Task.FromResult(new NotificationDto(Guid.NewGuid(), type, title, message, null, false, DateTime.UtcNow, null));
        }
        public async Task<IReadOnlyList<NotificationDto>> CreateManyAsync(IReadOnlyCollection<CreateNotificationRequest> requests, CancellationToken ct)
        {
            var result = new List<NotificationDto>();
            foreach (var r in requests) result.Add(await CreateAsync(r.UserId, r.Type, r.Title, r.Message, r.Data, ct));
            return result;
        }
    }
}
