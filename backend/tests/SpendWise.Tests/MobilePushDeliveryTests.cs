using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SpendWise.Application.MobilePush;
using SpendWise.Domain.MobilePush;
using SpendWise.Infrastructure.MobilePush;
using SpendWise.Infrastructure.Notifications;
using Xunit;

namespace SpendWise.Tests;

public sealed class MobilePushDeliveryTests
{
    private const string PushToken = "ExpoPushToken[aaaaaaaaaaaaaaaaaaaaaa]";

    [Fact]
    public async Task Notification_without_active_installation_preserves_web_outbox_only()
    {
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Web-only recipient");

        await new NotificationService(fixture.Db).CreateAsync(
            user.Id,
            "MoneyReceived",
            "Sensitive title",
            "Sensitive financial message",
            new { AccountNumber = "123456789" },
            default);

        Assert.Single(await fixture.Db.Notifications.ToListAsync());
        Assert.Single(await fixture.Db.OutboxMessages.ToListAsync());
        Assert.Empty(await fixture.Db.MobilePushDeliveries.ToListAsync());
    }

    [Fact]
    public async Task Notification_with_active_installation_creates_one_unique_push_delivery()
    {
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Mobile recipient");
        var installation = await AddInstallationAsync(fixture, user.Id);

        var notification = await new NotificationService(fixture.Db).CreateAsync(
            user.Id,
            "GroupInvite",
            "Invitation",
            "Private invitation content",
            new { GroupId = Guid.NewGuid() },
            default);

        var delivery = await fixture.Db.MobilePushDeliveries.SingleAsync();
        Assert.Equal(notification.Id, delivery.NotificationId);
        Assert.Equal(installation.Id, delivery.InstallationId);
        Assert.Equal(MobilePushDeliveryStatus.Pending, delivery.Status);

        fixture.Db.MobilePushDeliveries.Add(new MobilePushDelivery
        {
            NotificationId = notification.Id,
            InstallationId = installation.Id
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => fixture.Db.SaveChangesAsync());
    }

    [Fact]
    public async Task Successful_push_submission_does_not_process_signalr_outbox()
    {
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Push success recipient");
        await AddInstallationAsync(fixture, user.Id);
        await new NotificationService(fixture.Db).CreateAsync(
            user.Id,
            "BankTransactionImported",
            "Bank transaction title",
            "Bank transaction amount and account",
            new { AccountNumber = "123456789", Amount = 500_000 },
            default);
        var sender = new FakeExpoPushSender(
            new ExpoPushSendResult(ExpoPushSendStatus.Submitted, "receipt-1"));
        var dispatcher = Dispatcher(fixture, sender);

        var submitted = await dispatcher.DispatchPendingAsync(default);

        Assert.Equal(1, submitted);
        var delivery = await fixture.Db.MobilePushDeliveries.SingleAsync();
        Assert.Equal(MobilePushDeliveryStatus.Submitted, delivery.Status);
        Assert.Equal("receipt-1", delivery.ExpoReceiptId);
        Assert.NotNull(delivery.SubmittedAt);
        var realtime = await fixture.Db.OutboxMessages.SingleAsync();
        Assert.Null(realtime.ProcessedAt);
        Assert.Equal(0, realtime.AttemptCount);
        Assert.Equal("BankTransactionImported", sender.LastMessage!.NotificationType);
    }

    [Fact]
    public async Task Transient_push_failure_retries_only_push_delivery()
    {
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Push retry recipient");
        await AddInstallationAsync(fixture, user.Id);
        await new NotificationService(fixture.Db).CreateAsync(
            user.Id, "SystemAnnouncement", "Title", "Message", null, default);
        var dispatcher = Dispatcher(fixture, new FakeExpoPushSender(transientFailure: true));

        Assert.Equal(0, await dispatcher.DispatchPendingAsync(default));

        var delivery = await fixture.Db.MobilePushDeliveries.SingleAsync();
        Assert.Equal(MobilePushDeliveryStatus.Pending, delivery.Status);
        Assert.Equal(1, delivery.AttemptCount);
        Assert.NotNull(delivery.NextAttemptAt);
        Assert.Equal("ProviderUnavailable", delivery.LastError);
        var realtime = await fixture.Db.OutboxMessages.SingleAsync();
        Assert.Null(realtime.ProcessedAt);
        Assert.Equal(0, realtime.AttemptCount);
    }

    [Fact]
    public async Task Immediate_device_not_registered_deactivates_installation_only()
    {
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Invalid device recipient");
        await AddInstallationAsync(fixture, user.Id);
        await new NotificationService(fixture.Db).CreateAsync(
            user.Id, "SecurityAlert", "Title", "Message", null, default);
        var sender = new FakeExpoPushSender(
            new ExpoPushSendResult(ExpoPushSendStatus.DeviceNotRegistered));

        Assert.Equal(0, await Dispatcher(fixture, sender).DispatchPendingAsync(default));

        var delivery = await fixture.Db.MobilePushDeliveries.SingleAsync();
        Assert.Equal(MobilePushDeliveryStatus.Failed, delivery.Status);
        Assert.Equal("DeviceNotRegistered", delivery.LastError);
        var installation = await fixture.Db.MobilePushInstallations.SingleAsync();
        Assert.False(installation.IsActive);
        Assert.Null(installation.PushToken);
        Assert.Null((await fixture.Db.OutboxMessages.SingleAsync()).ProcessedAt);
    }

    [Fact]
    public async Task Disabled_push_leaves_delivery_pending_without_calling_provider()
    {
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var user = await fixture.AddUserAsync("Disabled push recipient");
        await AddInstallationAsync(fixture, user.Id);
        await new NotificationService(fixture.Db).CreateAsync(
            user.Id, "FriendRequest", "Title", "Message", null, default);
        var sender = new FakeExpoPushSender(
            new ExpoPushSendResult(ExpoPushSendStatus.Submitted, "receipt"));

        var submitted = await Dispatcher(fixture, sender, enabled: false)
            .DispatchPendingAsync(default);

        Assert.Equal(0, submitted);
        Assert.Equal(0, sender.SendCount);
        Assert.Equal(MobilePushDeliveryStatus.Pending,
            (await fixture.Db.MobilePushDeliveries.SingleAsync()).Status);
    }

    [Fact]
    public async Task Expo_sender_uses_privacy_safe_payload_and_maps_immediate_invalid_device()
    {
        JsonElement? sentPayload = null;
        var handler = new CallbackHttpHandler(async (request, cancellationToken) =>
        {
            using var document = JsonDocument.Parse(
                await request.Content!.ReadAsStringAsync(cancellationToken));
            sentPayload = document.RootElement.Clone();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"data":{"status":"error","message":"token rejected","details":{"error":"DeviceNotRegistered"}}}""",
                    Encoding.UTF8,
                    "application/json")
            };
        });
        var sender = new ExpoPushSender(new HttpClient(handler));
        var notificationId = Guid.NewGuid();

        var result = await sender.SendAsync(
            new ExpoPushMessage(PushToken, notificationId, "MoneyReceived"),
            default);

        Assert.Equal(ExpoPushSendStatus.DeviceNotRegistered, result.Status);
        Assert.Equal("SpendWise", sentPayload!.Value.GetProperty("title").GetString());
        Assert.Equal("Bạn có thông báo mới trong SpendWise.",
            sentPayload.Value.GetProperty("body").GetString());
        var data = sentPayload.Value.GetProperty("data");
        Assert.Equal(notificationId.ToString(), data.GetProperty("notificationId").GetString());
        Assert.Equal("MoneyReceived", data.GetProperty("notificationType").GetString());
        Assert.False(sentPayload.Value.TryGetProperty("message", out _));
        Assert.DoesNotContain("AccountNumber", sentPayload.Value.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Expo_sender_maps_success_ticket_to_receipt_id()
    {
        var handler = new CallbackHttpHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"data":[{"status":"ok","id":"receipt-success"}]}""",
                    Encoding.UTF8,
                    "application/json")
            }));
        var sender = new ExpoPushSender(new HttpClient(handler));

        var result = await sender.SendAsync(
            new ExpoPushMessage(PushToken, Guid.NewGuid(), "FriendAccepted"),
            default);

        Assert.Equal(ExpoPushSendStatus.Submitted, result.Status);
        Assert.Equal("receipt-success", result.ReceiptId);
    }

    [Fact]
    public async Task Expo_sender_treats_http_429_as_transient()
    {
        var handler = new CallbackHttpHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.TooManyRequests)));
        var sender = new ExpoPushSender(new HttpClient(handler));

        await Assert.ThrowsAsync<ExpoPushTransientException>(() => sender.SendAsync(
            new ExpoPushMessage(PushToken, Guid.NewGuid(), "FriendAccepted"),
            default));
    }

    private static async Task<MobilePushInstallation> AddInstallationAsync(
        TargetedCorrectnessFixture fixture,
        Guid userId)
    {
        var installation = new MobilePushInstallation
        {
            UserId = userId,
            Platform = MobilePlatform.Android,
            PushToken = PushToken,
            DeviceName = "Test phone",
            IsActive = true
        };
        fixture.Db.MobilePushInstallations.Add(installation);
        await fixture.Db.SaveChangesAsync();
        return installation;
    }

    private static MobilePushDispatcher Dispatcher(
        TargetedCorrectnessFixture fixture,
        IExpoPushSender sender,
        bool enabled = true) => new(
            fixture.Db,
            sender,
            Options.Create(new MobilePushOptions { Enabled = enabled }),
            fixture.Clock,
            NullLogger<MobilePushDispatcher>.Instance);

    private sealed class FakeExpoPushSender(
        ExpoPushSendResult? result = null,
        bool transientFailure = false) : IExpoPushSender
    {
        public int SendCount { get; private set; }
        public ExpoPushMessage? LastMessage { get; private set; }

        public Task<ExpoPushSendResult> SendAsync(
            ExpoPushMessage message,
            CancellationToken cancellationToken)
        {
            SendCount++;
            LastMessage = message;
            if (transientFailure)
                throw new ExpoPushTransientException();
            return Task.FromResult(result!);
        }

        public Task<IReadOnlyDictionary<string, ExpoPushReceiptResult>> GetReceiptsAsync(
            IReadOnlyCollection<string> receiptIds,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class CallbackHttpHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> callback)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => callback(request, cancellationToken);
    }
}
