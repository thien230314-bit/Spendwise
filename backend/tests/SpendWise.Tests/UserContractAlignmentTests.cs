using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Features.Friends;
using SpendWise.Application.Features.V1Groups;
using SpendWise.Application.Social;
using SpendWise.Domain.Identity;
using SpendWise.Domain.Social;
using SpendWise.Domain.V1Groups;
using Xunit;

namespace SpendWise.Tests;

public sealed class UserContractAlignmentTests
{
    private static (Guid Lower, Guid Higher) Pair(Guid a, Guid b) => a.CompareTo(b) < 0 ? (a, b) : (b, a);

    private static async Task MakeFriendsAsync(TargetedCorrectnessFixture f, Guid user1, Guid user2)
    {
        await V1FundMembershipTests.MakeFriends(f, user1, user2);
        var pair = Pair(user1, user2);
        var friendship = await f.Db.Friendships.SingleAsync(x => x.LowerUserId == pair.Lower && x.HigherUserId == pair.Higher);
        f.Db.FriendPreferences.AddRange(
            new FriendPreference { FriendshipId = friendship.Id, UserId = user1 },
            new FriendPreference { FriendshipId = friendship.Id, UserId = user2 }
        );
        await f.Db.SaveChangesAsync();
    }

    [Fact]
    public async Task TestA_Friend_Avatar_Freshness_On_Refetch()
    {
        // 1. Tạo User A và B
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var userA = await f.AddUserAsync("User A");
        var userB = await f.AddUserAsync("User B");

        // 2. A và B là bạn bè
        await MakeFriendsAsync(f, userA.Id, userB.Id);

        // 3. A chưa có avatar -> B đọc thấy null
        var friendsHandler = new GetFriendsQueryHandler(f.Db);
        var initialList = await friendsHandler.Handle(new GetFriendsQuery(userB.Id), default);
        var friendItem = Assert.Single(initialList);
        Assert.Equal(userA.Id, friendItem.UserId);
        Assert.Null(friendItem.AvatarUrl);

        // 4. A upload Avatar mới
        userA.AvatarUrl = "/uploads/avatars/userA-v1.jpg";
        await f.Db.SaveChangesAsync();

        // 5. B refetch danh sách bạn bè -> Thấy AvatarUrl mới ngay lập tức
        var refetchList = await friendsHandler.Handle(new GetFriendsQuery(userB.Id), default);
        var refetchedFriend = Assert.Single(refetchList);
        Assert.Equal(userA.Id, refetchedFriend.UserId);
        Assert.Equal("/uploads/avatars/userA-v1.jpg", refetchedFriend.AvatarUrl);

        // 6. A đổi Avatar lần hai
        userA.AvatarUrl = "/uploads/avatars/userA-v2.jpg";
        await f.Db.SaveChangesAsync();

        // 7. B refetch lần nữa -> Thấy URL mới nhất
        var secondRefetch = await friendsHandler.Handle(new GetFriendsQuery(userB.Id), default);
        Assert.Equal("/uploads/avatars/userA-v2.jpg", Assert.Single(secondRefetch).AvatarUrl);
    }

    [Fact]
    public async Task TestB_Fund_Members_Projection_Includes_FullName_And_AvatarUrl()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Owner Member");
        owner.AvatarUrl = "/avatars/owner.png";
        var member = await f.AddUserAsync("Colleague Member");
        member.AvatarUrl = "/avatars/colleague.png";
        await f.Db.SaveChangesAsync();

        var fundService = V1FundMembershipTests.Service(f);
        var fund = await fundService.CreateAsync(owner.Id, new CreateV1FundRequest("Team Project", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);

        // Thêm member vào quỹ
        f.Db.V1FundMemberships.Add(new V1FundMembership
        {
            FundId = fund.Id,
            UserId = member.Id,
            Seat = 2,
            JoinedAt = f.Clock.UtcNow,
            CanSpend = false
        });
        await f.Db.SaveChangesAsync();

        // Member gọi xem danh sách thành viên
        var members = await fundService.MembersAsync(member.Id, fund.Id, default);
        Assert.Equal(2, members.Count);

        var ownerDto = members.Single(m => m.UserId == owner.Id);
        Assert.Equal("Owner Member", ownerDto.FullName);
        Assert.Equal("Owner Member", ownerDto.DisplayName);
        Assert.Equal("/avatars/owner.png", ownerDto.AvatarUrl);
        Assert.True(ownerDto.IsOwner);
        Assert.Equal(1, ownerDto.Seat);

        var colleagueDto = members.Single(m => m.UserId == member.Id);
        Assert.Equal("Colleague Member", colleagueDto.FullName);
        Assert.Equal("/avatars/colleague.png", colleagueDto.AvatarUrl);
        Assert.False(colleagueDto.IsOwner);
        Assert.Equal(2, colleagueDto.Seat);

        // Non-member bị từ chối
        var outsider = await f.AddUserAsync("Outsider");
        await Assert.ThrowsAsync<NotFoundException>(() => fundService.MembersAsync(outsider.Id, fund.Id, default));
    }

    [Fact]
    public async Task TestC_Nickname_Remains_Private_To_Owner_And_Does_Not_Leak_To_Global_Search()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var userA = await f.AddUserAsync("Alice Nguyen");
        var userB = await f.AddUserAsync("Bob Tran");
        var userC = await f.AddUserAsync("Charlie Le");

        await MakeFriendsAsync(f, userA.Id, userB.Id);

        // Alice đặt biệt danh cho Bob là "Bobby Secret"
        var prefHandler = new UpdateFriendPreferenceCommandHandler(f.Db);
        await prefHandler.Handle(new UpdateFriendPreferenceCommand(userA.Id, userB.Id, new UpdateFriendPreferenceRequest("Bobby Secret", true)), default);

        // 1. Alice đọc thấy biệt danh "Bobby Secret"
        var friendsHandler = new GetFriendsQueryHandler(f.Db);
        var aliceFriends = await friendsHandler.Handle(new GetFriendsQuery(userA.Id), default);
        Assert.Equal("Bobby Secret", Assert.Single(aliceFriends).Nickname);

        // 2. Bob đọc danh sách bạn bè của mình không thấy nickname Alice đặt
        var bobFriends = await friendsHandler.Handle(new GetFriendsQuery(userB.Id), default);
        Assert.Null(Assert.Single(bobFriends).Nickname);

        // 3. FullName gốc của Bob vẫn nguyên vẹn
        var bobUser = await f.Db.Users.FindAsync(userB.Id);
        Assert.Equal("Bob Tran", bobUser!.FullName);

        // 4. Tìm kiếm toàn hệ thống bằng biệt danh riêng "Bobby Secret" không ra kết quả
        var discoverHandler = new DiscoverUsersQueryHandler(f.Db);
        var searchByNickname = await discoverHandler.Handle(new DiscoverUsersQuery(userC.Id, "Bobby Secret"), default);
        Assert.Empty(searchByNickname.Items);

        // 5. Tìm theo FullName gốc "Bob" ra Bob Tran
        var searchByFullName = await discoverHandler.Handle(new DiscoverUsersQuery(userC.Id, "Bob"), default);
        Assert.Single(searchByFullName.Items);
        Assert.Equal(userB.Id, searchByFullName.Items[0].UserId);
    }

    [Fact]
    public async Task TestD_User_Discovery_Search_ByName_Pagination_And_Limits()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var caller = await f.AddUserAsync("Caller Account");

        // Tạo 25 user có tên chứa "Developer"
        for (var i = 1; i <= 25; i++)
        {
            var u = await f.AddUserAsync($"Developer {i:D2}");
            u.AvatarUrl = $"/avatars/dev-{i}.png";
        }
        await f.Db.SaveChangesAsync();

        var handler = new DiscoverUsersQueryHandler(f.Db);

        // Page 1: 10 items
        var page1 = await handler.Handle(new DiscoverUsersQuery(caller.Id, "Developer", Page: 1, PageSize: 10), default);
        Assert.Equal(25, page1.TotalCount);
        Assert.Equal(10, page1.Items.Count);
        Assert.Equal(1, page1.Page);
        Assert.Equal(10, page1.PageSize);
        Assert.Equal("Developer 01", page1.Items[0].FullName);
        Assert.Equal("/avatars/dev-1.png", page1.Items[0].AvatarUrl);

        // Page 2: 10 items
        var page2 = await handler.Handle(new DiscoverUsersQuery(caller.Id, "Developer", Page: 2, PageSize: 10), default);
        Assert.Equal(10, page2.Items.Count);
        Assert.Equal(2, page2.Page);
        Assert.Equal("Developer 11", page2.Items[0].FullName);

        // Page 3: 5 items
        var page3 = await handler.Handle(new DiscoverUsersQuery(caller.Id, "Developer", Page: 3, PageSize: 10), default);
        Assert.Equal(5, page3.Items.Count);
        Assert.Equal("Developer 25", page3.Items[4].FullName);

        // Đảm bảo không trùng nhau giữa page 1 và page 2
        var page1Ids = page1.Items.Select(x => x.UserId).ToHashSet();
        Assert.All(page2.Items, item => Assert.DoesNotContain(item.UserId, page1Ids));

        // Kiểm tra validation độ dài: < 2 ký tự -> lỗi
        await Assert.ThrowsAsync<AppException>(() => handler.Handle(new DiscoverUsersQuery(caller.Id, "a"), default));

        // Rỗng -> lỗi
        await Assert.ThrowsAsync<AppException>(() => handler.Handle(new DiscoverUsersQuery(caller.Id, "   "), default));

        // PageSize > 50 -> lỗi
        await Assert.ThrowsAsync<AppException>(() => handler.Handle(new DiscoverUsersQuery(caller.Id, "Developer", Page: 1, PageSize: 51), default));

        // Không trả tài khoản caller (người đang tìm)
        var callerSearchResult = await handler.Handle(new DiscoverUsersQuery(caller.Id, "Caller"), default);
        Assert.Empty(callerSearchResult.Items);

        // Không trả platform admin/operator
        var admin = await f.AddUserAsync("Developer Admin", role: SystemRole.Admin);
        var superAdmin = await f.AddUserAsync("Developer Super", role: SystemRole.SuperAdmin);
        var sysAdmin = await f.AddUserAsync("Developer Sys", role: SystemRole.SystemAdmin);
        var adminSearch = await handler.Handle(new DiscoverUsersQuery(caller.Id, "Developer"), default);
        Assert.DoesNotContain(adminSearch.Items, item => item.UserId == admin.Id || item.UserId == superAdmin.Id || item.UserId == sysAdmin.Id);

        // Không trả tài khoản không Active
        var lockedUser = await f.AddUserAsync("Developer Locked", status: UserStatus.Locked);
        var suspendedUser = await f.AddUserAsync("Developer Suspended", status: UserStatus.Suspended);
        var restrictedUser = await f.AddUserAsync("Developer Restricted", status: UserStatus.Restricted);
        var inactiveSearch = await handler.Handle(new DiscoverUsersQuery(caller.Id, "Developer"), default);
        Assert.DoesNotContain(inactiveSearch.Items, item => item.UserId == lockedUser.Id || item.UserId == suspendedUser.Id || item.UserId == restrictedUser.Id);
    }

    [Fact]
    public async Task TestE_User_Discovery_Accurately_Reflects_All_Four_Relationship_States()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var caller = await f.AddUserAsync("Main Caller");

        var stranger = await f.AddUserAsync("TestState Stranger");
        var friend = await f.AddUserAsync("TestState Friend");
        var sentTarget = await f.AddUserAsync("TestState SentRequest");
        var receivedRequester = await f.AddUserAsync("TestState ReceivedRequest");

        // Thiết lập trạng thái FRIENDS
        await MakeFriendsAsync(f, caller.Id, friend.Id);

        // Thiết lập trạng thái REQUEST_SENT (caller gửi cho sentTarget)
        var pairSent = Pair(caller.Id, sentTarget.Id);
        f.Db.FriendRequests.Add(new FriendRequest
        {
            RequesterUserId = caller.Id,
            AddresseeUserId = sentTarget.Id,
            LowerUserId = pairSent.Lower,
            HigherUserId = pairSent.Higher,
            Status = FriendRequestStatus.Pending,
            Message = "Hello"
        });

        // Thiết lập trạng thái REQUEST_RECEIVED (receivedRequester gửi cho caller)
        var pairReceived = Pair(receivedRequester.Id, caller.Id);
        f.Db.FriendRequests.Add(new FriendRequest
        {
            RequesterUserId = receivedRequester.Id,
            AddresseeUserId = caller.Id,
            LowerUserId = pairReceived.Lower,
            HigherUserId = pairReceived.Higher,
            Status = FriendRequestStatus.Pending,
            Message = "Let's connect"
        });
        await f.Db.SaveChangesAsync();

        var handler = new DiscoverUsersQueryHandler(f.Db);
        var result = await handler.Handle(new DiscoverUsersQuery(caller.Id, "TestState"), default);

        Assert.Equal(4, result.TotalCount);

        var strangerItem = result.Items.Single(x => x.UserId == stranger.Id);
        Assert.Equal("NONE", strangerItem.RelationshipState);

        var friendItem = result.Items.Single(x => x.UserId == friend.Id);
        Assert.Equal("FRIENDS", friendItem.RelationshipState);

        var sentItem = result.Items.Single(x => x.UserId == sentTarget.Id);
        Assert.Equal("REQUEST_SENT", sentItem.RelationshipState);

        var receivedItem = result.Items.Single(x => x.UserId == receivedRequester.Id);
        Assert.Equal("REQUEST_RECEIVED", receivedItem.RelationshipState);
    }

    [Fact]
    public async Task TestF_Backward_Compatibility_Existing_Search_And_Requests_Intact()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var caller = await f.AddUserAsync("Current Caller");
        var target = await f.AddUserAsync("Target Contact");
        target.Phone = "0908765432";
        target.AvatarUrl = "/avatars/contact.png";
        await f.Db.SaveChangesAsync();

        // Exact lookup cũ qua SearchFriendContactQuery
        var exactHandler = new SearchFriendContactQueryHandler(f.Db);
        var contactDto = await exactHandler.Handle(new SearchFriendContactQuery(caller.Id, target.SpendId), default);

        Assert.Equal(target.Id, contactDto.UserId);
        Assert.Equal(target.FullName, contactDto.FullName);
        Assert.Equal(target.SpendId, contactDto.SpendId);
        Assert.Equal("NONE", contactDto.State);
        Assert.Equal("/avatars/contact.png", contactDto.AvatarUrl); // Đã có thêm AvatarUrl

        // Gửi lời mời và kiểm tra FriendRequestDto có AvatarUrl
        var sendHandler = new SendFriendRequestCommandHandler(f.Db, f.Clock, new SpendWise.Infrastructure.Notifications.NotificationService(f.Db), exactHandler);
        var requestDto = await sendHandler.Handle(new SendFriendRequestCommand(caller.Id, new SendFriendRequest(target.SpendId, "Hi")), default);

        Assert.Equal(target.Id, requestDto.UserId);
        Assert.Equal("/avatars/contact.png", requestDto.AvatarUrl);

        // Incoming handler kiểm tra caller avatar
        caller.AvatarUrl = "/avatars/caller.png";
        await f.Db.SaveChangesAsync();

        var incomingHandler = new GetIncomingFriendRequestsQueryHandler(f.Db);
        var incoming = await incomingHandler.Handle(new GetIncomingFriendRequestsQuery(target.Id), default);
        var inc = Assert.Single(incoming);
        Assert.Equal(caller.Id, inc.UserId);
        Assert.Equal("/avatars/caller.png", inc.AvatarUrl);
    }
}
