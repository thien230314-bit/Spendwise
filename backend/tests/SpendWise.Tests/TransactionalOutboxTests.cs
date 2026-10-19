using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SpendWise.Application.Notifications;
using SpendWise.Infrastructure.Notifications;
using Xunit;

namespace SpendWise.Tests;

// Các test này chứng minh realtime chỉ được gửi từ outbox sau khi dữ liệu đã commit.
public sealed class TransactionalOutboxTests
{
    [Fact]
    public async Task CreateAsync_WritesNotificationAndOutboxWithoutCallingRealtime()
    {
        // Arrange: tạo người nhận và bộ gửi realtime có thể đếm số lần gọi.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Outbox recipient");
        var realtime = new RecordingRealtimeNotifier();
        var service = new NotificationService(fixture.Db);

        // Act: lưu thông báo như luồng nghiệp vụ bình thường.
        await service.CreateAsync(
            user.Id,
            "MoneyReceived",
            "Nhận tiền",
            "Bạn vừa nhận tiền.",
            new { Amount = 10_000m },
            CancellationToken.None);

        // Assert: transaction chỉ ghi Notification và Outbox, không gọi SignalR trực tiếp.
        Assert.Equal(0, realtime.SendCount);
        Assert.Equal(1, await fixture.Db.Notifications.CountAsync());
        Assert.Equal(1, await fixture.Db.OutboxMessages.CountAsync());
    }

    [Fact]
    public async Task BusinessRollback_RemovesNotificationAndOutboxTogether()
    {
        // Arrange: dùng transaction ngoài giống các luồng chuyển tiền.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Rollback recipient");
        var service = new NotificationService(fixture.Db);

        // Act: phát sinh lỗi sau khi tạo thông báo để buộc transaction rollback.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Db.ExecuteInTransactionAsync<int>(async token =>
            {
                await service.CreateAsync(
                    user.Id,
                    "MoneySent",
                    "Gửi tiền",
                    "Nội dung test.",
                    null,
                    token);
                throw new InvalidOperationException("forced rollback");
            }));

        // Assert: không còn bản ghi mồ côi sau rollback.
        fixture.Db.ChangeTracker.Clear();
        Assert.Equal(0, await fixture.Db.Notifications.CountAsync());
        Assert.Equal(0, await fixture.Db.OutboxMessages.CountAsync());
    }

    [Fact]
    public async Task DispatchFailure_LeavesMessagePendingForRetry()
    {
        // Arrange: lưu một outbox và giả lập SignalR tạm thời lỗi.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Retry recipient");
        await new NotificationService(fixture.Db).CreateAsync(
            user.Id,
            "SystemAnnouncement",
            "Thông báo",
            "Nội dung test.",
            null,
            CancellationToken.None);
        var realtime = new RecordingRealtimeNotifier { ShouldFail = true };
        var dispatcher = CreateDispatcher(fixture, realtime);

        // Act: worker thử gửi một lần.
        var delivered = await dispatcher.DispatchPendingAsync(CancellationToken.None);

        // Assert: lỗi được ghi nhận nhưng message vẫn chưa processed để thử lại.
        var message = await fixture.Db.OutboxMessages.SingleAsync();
        Assert.Equal(0, delivered);
        Assert.Null(message.ProcessedAt);
        Assert.Equal(1, message.AttemptCount);
        Assert.NotNull(message.NextAttemptAt);
        Assert.NotNull(message.LastError);
    }

    [Fact]
    public async Task SuccessfulDispatch_MarksProcessedAndDoesNotSendTwice()
    {
        // Arrange: tạo outbox đã commit và bộ gửi realtime thành công.
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Delivery recipient");
        await new NotificationService(fixture.Db).CreateAsync(
            user.Id,
            "GroupInvite",
            "Lời mời",
            "Nội dung test.",
            new { GroupId = Guid.NewGuid() },
            CancellationToken.None);
        var realtime = new RecordingRealtimeNotifier();
        var dispatcher = CreateDispatcher(fixture, realtime);

        // Act: quét outbox hai lần liên tiếp.
        var first = await dispatcher.DispatchPendingAsync(CancellationToken.None);
        var second = await dispatcher.DispatchPendingAsync(CancellationToken.None);

        // Assert: message processed không bị gửi lại và payload vẫn hợp lệ.
        var message = await fixture.Db.OutboxMessages.SingleAsync();
        Assert.Equal(1, first);
        Assert.Equal(0, second);
        Assert.Equal(1, realtime.SendCount);
        Assert.NotNull(message.ProcessedAt);
        Assert.Equal("notification", realtime.LastEventName);
        Assert.Equal(user.Id, realtime.LastUserId);
    }

    private static OutboxDispatcher CreateDispatcher(
        TargetedCorrectnessFixture fixture,
        RecordingRealtimeNotifier realtime) =>
        new(fixture.Db, realtime, fixture.Clock, NullLogger<OutboxDispatcher>.Instance);

    private sealed class RecordingRealtimeNotifier : IRealtimeNotifier
    {
        public int SendCount { get; private set; }
        public Guid LastUserId { get; private set; }
        public string? LastEventName { get; private set; }
        public bool ShouldFail { get; init; }

        public Task SendToUserAsync(
            Guid userId,
            string eventName,
            object payload,
            CancellationToken cancellationToken)
        {
            SendCount++;
            LastUserId = userId;
            LastEventName = eventName;
            if (ShouldFail)
            {
                throw new InvalidOperationException("SignalR unavailable");
            }

            return Task.CompletedTask;
        }
    }
}
