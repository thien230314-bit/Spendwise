using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Features.Groups;
using SpendWise.Application.Features.Groups.Commands.CreateGroup;
using SpendWise.Application.Groups;
using SpendWise.Domain.Groups;
using SpendWise.Infrastructure.Admin;
using SpendWise.Infrastructure.Notifications;
using Xunit;

namespace SpendWise.Tests;

// Kiểm tra tối ưu batch không làm thay đổi tập dữ liệu được tạo.
public sealed class BatchLoopOptimizationTests
{
 #if RETIRED_LEGACY_TESTS
    [Fact]
    public async Task Contribution_request_creates_one_notification_and_outbox_per_recipient()
    {
        // Arrange: một owner và ba thành viên hợp lệ trong cùng Group.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await fixture.AddUserAsync("Batch owner");
        var members = new[]
        {
            await fixture.AddUserAsync("Batch member one"),
            await fixture.AddUserAsync("Batch member two"),
            await fixture.AddUserAsync("Batch member three")
        };
        var access = new GroupAccess(fixture.Db);
        var group = await new CreateGroupCommandHandler(fixture.Db, fixture.Clock, access, fixture.Security)
            .Handle(
                new CreateGroupCommand(
                    owner.Id,
                    new CreateGroupRequest("Batch group", null, "Friends", SecurityPin: TargetedCorrectnessFixture.SecurityPin)),
                default);
        fixture.Db.GroupMembers.AddRange(members.Select(member => new GroupMember
        {
            GroupId = group.Id,
            UserId = member.Id,
            Role = GroupRole.Member,
            JoinedAt = fixture.Clock.UtcNow
        }));
        await fixture.Db.SaveChangesAsync();
        var handler = new CreateContributionRequestCommandHandler(
            fixture.Db,
            new NotificationService(fixture.Db),
            new OperationPolicy(fixture.Db),
            access);

        // Act: tạo một yêu cầu gửi tới cả ba thành viên.
        var result = await handler.Handle(
            new CreateContributionRequestCommand(
                owner.Id,
                group.Id,
                new CreateContributionRequest("Batch purpose", 25_000, null)),
            default);

        // Assert: mỗi recipient có đúng một Notification và một Outbox, owner không nhận.
        Assert.Equal(3, result.Recipients.Count);
        var notifications = await fixture.Db.Notifications.AsNoTracking().ToListAsync();
        var outbox = await fixture.Db.OutboxMessages.AsNoTracking().ToListAsync();
        Assert.Equal(3, notifications.Count);
        Assert.Equal(3, outbox.Count);
        Assert.Equal(
            members.Select(member => member.Id).OrderBy(id => id),
            notifications.Select(item => item.UserId).OrderBy(id => id));
        Assert.All(outbox, message => Assert.Equal("notification", message.EventName));
    }

 #endif
}
