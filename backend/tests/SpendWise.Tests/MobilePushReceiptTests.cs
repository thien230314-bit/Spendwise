using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.MobilePush;
using SpendWise.Domain.MobilePush;
using SpendWise.Domain.Notifications;
using SpendWise.Infrastructure.MobilePush;
using SpendWise.Infrastructure.Notifications;
using Xunit;

namespace SpendWise.Tests;

public sealed class MobilePushReceiptTests
{
    private const string PushToken = "ExpoPushToken[aaaaaaaaaaaaaaaaaaaaaa]";

    [Fact]
    public async Task Expo_client_batches_receipts_and_maps_only_returned_ids()
    {
        Uri? endpoint = null;
        JsonElement? payload = null;
        var handler = new CallbackHttpHandler(async (request, cancellationToken) =>
        {
            endpoint = request.RequestUri;
            using var document = JsonDocument.Parse(
                await request.Content!.ReadAsStringAsync(cancellationToken));
            payload = document.RootElement.Clone();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"data":{"receipt-ok":{"status":"ok"},"receipt-dead":{"status":"error","details":{"error":"DeviceNotRegistered"}},"receipt-other":{"status":"error","message":"ExpoPushToken[secret]","details":{"error":"MessageRateExceeded"}},"receipt-malformed":{"unexpected":true}}}""",
                    Encoding.UTF8,
                    "application/json")
            };
        });
        var sender = new ExpoPushSender(new HttpClient(handler));

        var results = await sender.GetReceiptsAsync(
            ["receipt-ok", "receipt-dead", "receipt-other", "receipt-malformed", "receipt-missing"],
            default);

        Assert.Equal("https://exp.host/--/api/v2/push/getReceipts", endpoint!.ToString());
        Assert.Equal(5, payload!.Value.GetProperty("ids").GetArrayLength());
        Assert.Equal(ExpoPushReceiptStatus.Succeeded, results["receipt-ok"].Status);
        Assert.Equal(ExpoPushReceiptStatus.DeviceNotRegistered, results["receipt-dead"].Status);
        Assert.Equal(ExpoPushReceiptStatus.PermanentFailure, results["receipt-other"].Status);
        Assert.Equal(ExpoPushReceiptStatus.PermanentFailure, results["receipt-malformed"].Status);
        Assert.False(results.ContainsKey("receipt-missing"));
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task Expo_receipt_client_treats_429_and_5xx_as_transient(
        HttpStatusCode statusCode)
    {
        var handler = new CallbackHttpHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(statusCode)));
        var sender = new ExpoPushSender(new HttpClient(handler));

        await Assert.ThrowsAsync<ExpoPushTransientException>(() =>
            sender.GetReceiptsAsync(["receipt-1"], default));
    }

    [Fact]
    public async Task Expo_receipt_client_treats_network_failure_as_transient()
    {
        var handler = new CallbackHttpHandler((_, _) =>
            throw new HttpRequestException("network unavailable"));
        var sender = new ExpoPushSender(new HttpClient(handler));

        await Assert.ThrowsAsync<ExpoPushTransientException>(() =>
            sender.GetReceiptsAsync(["receipt-1"], default));
    }

    [Fact]
    public async Task Accepted_ticket_waits_15_minutes_then_receipt_ok_completes_only_push()
    {
        var clock = new MutableClock(new DateTime(2026, 9, 30, 4, 0, 0, DateTimeKind.Utc));
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync(clock);
        var user = await fixture.AddUserAsync("Receipt recipient");
        fixture.Db.MobilePushInstallations.Add(new MobilePushInstallation
        {
            UserId = user.Id,
            Platform = MobilePlatform.Android,
            PushToken = PushToken
        });
        await fixture.Db.SaveChangesAsync();
        await new NotificationService(fixture.Db).CreateAsync(
            user.Id, "FriendAccepted", "Title", "Private message", null, default);
        var provider = new FakeExpoProvider();

        Assert.Equal(1, await Dispatcher(fixture, provider).DispatchPendingAsync(default));
        var submitted = await fixture.Db.MobilePushDeliveries.SingleAsync();
        Assert.Equal(MobilePushDeliveryStatus.Submitted, submitted.Status);
        Assert.Equal(clock.UtcNow.AddMinutes(15), submitted.NextAttemptAt);
        Assert.Equal(0, await Reconciler(fixture, provider).ReconcileAsync(default));
        Assert.Equal(0, provider.ReceiptCallCount);

        clock.Advance(TimeSpan.FromMinutes(15));
        Assert.Equal(1, await Reconciler(fixture, provider).ReconcileAsync(default));

        Assert.Equal(MobilePushDeliveryStatus.Completed, submitted.Status);
        Assert.Equal(1, provider.SendCallCount);
        Assert.Equal(1, provider.ReceiptCallCount);
        Assert.Null((await fixture.Db.OutboxMessages.SingleAsync()).ProcessedAt);

        Assert.Equal(0, await Reconciler(fixture, provider).ReconcileAsync(default));
        Assert.Equal(1, provider.ReceiptCallCount);
    }

    [Fact]
    public async Task Receipt_device_not_registered_affects_only_matching_installation()
    {
        var clock = new MutableClock(new DateTime(2026, 9, 30, 5, 0, 0, DateTimeKind.Utc));
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync(clock);
        var first = await AddSubmittedDeliveryAsync(
            fixture, "Dead device", "receipt-dead", PushToken);
        var second = await AddSubmittedDeliveryAsync(
            fixture, "Live device", "receipt-ok", "ExpoPushToken[bbbbbbbbbbbbbbbbbbbbbb]");
        var provider = new FakeExpoProvider(new Dictionary<string, ExpoPushReceiptResult>
        {
            ["receipt-dead"] = new(ExpoPushReceiptStatus.DeviceNotRegistered),
            ["receipt-ok"] = new(ExpoPushReceiptStatus.Succeeded)
        });

        Assert.Equal(1, await Reconciler(fixture, provider).ReconcileAsync(default));

        Assert.Equal(MobilePushDeliveryStatus.Failed, first.Delivery.Status);
        Assert.Equal("DeviceNotRegistered", first.Delivery.LastError);
        Assert.False(first.Installation.IsActive);
        Assert.Null(first.Installation.PushToken);
        Assert.Equal(MobilePushDeliveryStatus.Completed, second.Delivery.Status);
        Assert.True(second.Installation.IsActive);
        Assert.NotNull(second.Installation.PushToken);
    }

    [Fact]
    public async Task Missing_receipt_stays_submitted_and_schedules_later_lookup()
    {
        var clock = new MutableClock(new DateTime(2026, 9, 30, 6, 0, 0, DateTimeKind.Utc));
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync(clock);
        var item = await AddSubmittedDeliveryAsync(fixture, "Missing receipt", "receipt-missing", PushToken);
        var provider = new FakeExpoProvider(new Dictionary<string, ExpoPushReceiptResult>());

        Assert.Equal(0, await Reconciler(fixture, provider).ReconcileAsync(default));

        Assert.Equal(MobilePushDeliveryStatus.Submitted, item.Delivery.Status);
        Assert.Equal("ReceiptPending", item.Delivery.LastError);
        Assert.Equal(clock.UtcNow.AddMinutes(15), item.Delivery.NextAttemptAt);
        Assert.True(item.Installation.IsActive);
        Assert.Equal(0, provider.SendCallCount);
    }

    [Fact]
    public async Task Transient_receipt_failure_backs_off_without_resending_push()
    {
        var clock = new MutableClock(new DateTime(2026, 9, 30, 7, 0, 0, DateTimeKind.Utc));
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync(clock);
        var item = await AddSubmittedDeliveryAsync(fixture, "Transient receipt", "receipt-transient", PushToken);
        var provider = new FakeExpoProvider(receiptTransient: true);

        Assert.Equal(0, await Reconciler(fixture, provider).ReconcileAsync(default));

        Assert.Equal(MobilePushDeliveryStatus.Submitted, item.Delivery.Status);
        Assert.Equal("ReceiptLookupUnavailable", item.Delivery.LastError);
        Assert.True(item.Delivery.NextAttemptAt > clock.UtcNow);
        Assert.Equal(0, provider.SendCallCount);
        Assert.Equal(0, await Reconciler(fixture, provider).ReconcileAsync(default));
        Assert.Equal(1, provider.ReceiptCallCount);
        Assert.Null((await fixture.Db.OutboxMessages.FirstAsync()).ProcessedAt);
    }

    [Fact]
    public async Task Permanent_receipt_error_is_sanitized_and_terminal()
    {
        var clock = new MutableClock(new DateTime(2026, 9, 30, 8, 0, 0, DateTimeKind.Utc));
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync(clock);
        var item = await AddSubmittedDeliveryAsync(fixture, "Rejected receipt", "receipt-rejected", PushToken);
        var provider = new FakeExpoProvider(new Dictionary<string, ExpoPushReceiptResult>
        {
            ["receipt-rejected"] = new(ExpoPushReceiptStatus.PermanentFailure)
        });

        Assert.Equal(0, await Reconciler(fixture, provider).ReconcileAsync(default));

        Assert.Equal(MobilePushDeliveryStatus.Failed, item.Delivery.Status);
        Assert.Equal("ReceiptRejected", item.Delivery.LastError);
        Assert.DoesNotContain(PushToken, item.Delivery.LastError, StringComparison.Ordinal);
        Assert.True(item.Installation.IsActive);
        Assert.Equal(0, provider.SendCallCount);
    }

    [Fact]
    public async Task Receipt_expiry_fails_without_lookup_resend_or_installation_revocation()
    {
        var clock = new MutableClock(new DateTime(2026, 9, 30, 9, 0, 0, DateTimeKind.Utc));
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync(clock);
        var item = await AddSubmittedDeliveryAsync(
            fixture, "Expired receipt", "receipt-expired", PushToken, clock.UtcNow.AddHours(-24));
        var provider = new FakeExpoProvider();

        Assert.Equal(0, await Reconciler(fixture, provider).ReconcileAsync(default));

        Assert.Equal(MobilePushDeliveryStatus.Failed, item.Delivery.Status);
        Assert.Equal("ReceiptUnavailable", item.Delivery.LastError);
        Assert.True(item.Installation.IsActive);
        Assert.Equal(0, provider.ReceiptCallCount);
        Assert.Equal(0, provider.SendCallCount);
    }

    [Fact]
    public async Task Disabled_receipts_make_no_provider_call()
    {
        var clock = new MutableClock(new DateTime(2026, 9, 30, 10, 0, 0, DateTimeKind.Utc));
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync(clock);
        var item = await AddSubmittedDeliveryAsync(fixture, "Disabled receipt", "receipt-disabled", PushToken);
        var provider = new FakeExpoProvider();

        Assert.Equal(0, await Reconciler(fixture, provider, enabled: false).ReconcileAsync(default));

        Assert.Equal(MobilePushDeliveryStatus.Submitted, item.Delivery.Status);
        Assert.Equal(0, provider.ReceiptCallCount);
        Assert.Equal(0, provider.SendCallCount);
    }

    [Fact]
    public async Task Receipt_worker_limits_each_provider_batch_to_1000_ids()
    {
        var clock = new MutableClock(new DateTime(2026, 9, 30, 11, 0, 0, DateTimeKind.Utc));
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync(clock);
        var user = await fixture.AddUserAsync("Batch receipt recipient");
        var installation = new MobilePushInstallation
        {
            UserId = user.Id,
            Platform = MobilePlatform.Android,
            PushToken = PushToken
        };
        fixture.Db.MobilePushInstallations.Add(installation);
        var notifications = Enumerable.Range(0, 1001).Select(index => new Notification
        {
            UserId = user.Id,
            Type = NotificationType.SystemAnnouncement,
            Title = "Title",
            Message = $"Message {index}"
        }).ToList();
        fixture.Db.Notifications.AddRange(notifications);
        fixture.Db.MobilePushDeliveries.AddRange(notifications.Select((notification, index) =>
            new MobilePushDelivery
            {
                NotificationId = notification.Id,
                InstallationId = installation.Id,
                Status = MobilePushDeliveryStatus.Submitted,
                SubmittedAt = clock.UtcNow.AddMinutes(-15),
                ExpoReceiptId = $"receipt-{index}",
                NextAttemptAt = clock.UtcNow
            }));
        await fixture.Db.SaveChangesAsync();
        var provider = new FakeExpoProvider(new Dictionary<string, ExpoPushReceiptResult>());

        await Reconciler(fixture, provider).ReconcileAsync(default);
        await Reconciler(fixture, provider).ReconcileAsync(default);

        Assert.Equal(2, provider.ReceiptCallCount);
        Assert.Equal(1000, provider.ReceiptBatches[0].Count);
        Assert.Single(provider.ReceiptBatches[1]);
        Assert.All(provider.ReceiptBatches, batch => Assert.True(batch.Count <= 1000));
        Assert.Equal(0, provider.SendCallCount);
    }

    private static MobilePushDispatcher Dispatcher(
        TargetedCorrectnessFixture fixture,
        IExpoPushSender provider) => new(
            fixture.Db,
            provider,
            Options.Create(new MobilePushOptions { Enabled = true }),
            fixture.Clock,
            NullLogger<MobilePushDispatcher>.Instance);

    private static MobilePushReceiptReconciler Reconciler(
        TargetedCorrectnessFixture fixture,
        IExpoPushSender provider,
        bool enabled = true) => new(
            fixture.Db,
            provider,
            Options.Create(new MobilePushOptions { Enabled = enabled }),
            fixture.Clock,
            NullLogger<MobilePushReceiptReconciler>.Instance);

    private static async Task<(MobilePushDelivery Delivery, MobilePushInstallation Installation)>
        AddSubmittedDeliveryAsync(
            TargetedCorrectnessFixture fixture,
            string userName,
            string receiptId,
            string pushToken,
            DateTime? submittedAt = null)
    {
        var user = await fixture.AddUserAsync(userName);
        var installation = new MobilePushInstallation
        {
            UserId = user.Id,
            Platform = MobilePlatform.Android,
            PushToken = pushToken
        };
        fixture.Db.MobilePushInstallations.Add(installation);
        await fixture.Db.SaveChangesAsync();
        await new NotificationService(fixture.Db).CreateAsync(
            user.Id, "SystemAnnouncement", "Title", "Private message", null, default);
        var delivery = await fixture.Db.MobilePushDeliveries
            .SingleAsync(item => item.InstallationId == installation.Id);
        delivery.Status = MobilePushDeliveryStatus.Submitted;
        delivery.SubmittedAt = submittedAt ?? fixture.Clock.UtcNow.AddMinutes(-15);
        delivery.ExpoReceiptId = receiptId;
        delivery.NextAttemptAt = fixture.Clock.UtcNow;
        await fixture.Db.SaveChangesAsync();
        return (delivery, installation);
    }

    private sealed class FakeExpoProvider(
        IReadOnlyDictionary<string, ExpoPushReceiptResult>? receipts = null,
        bool receiptTransient = false) : IExpoPushSender
    {
        public int SendCallCount { get; private set; }
        public int ReceiptCallCount { get; private set; }
        public List<IReadOnlyCollection<string>> ReceiptBatches { get; } = [];

        public Task<ExpoPushSendResult> SendAsync(
            ExpoPushMessage message,
            CancellationToken cancellationToken)
        {
            SendCallCount++;
            return Task.FromResult(
                new ExpoPushSendResult(ExpoPushSendStatus.Submitted, "receipt-1"));
        }

        public Task<IReadOnlyDictionary<string, ExpoPushReceiptResult>> GetReceiptsAsync(
            IReadOnlyCollection<string> receiptIds,
            CancellationToken cancellationToken)
        {
            ReceiptCallCount++;
            ReceiptBatches.Add(receiptIds.ToList());
            if (receiptTransient)
                throw new ExpoPushTransientException();
            return Task.FromResult<IReadOnlyDictionary<string, ExpoPushReceiptResult>>(
                receipts ?? receiptIds.ToDictionary(
                    id => id,
                    _ => new ExpoPushReceiptResult(ExpoPushReceiptStatus.Succeeded)));
        }
    }

    private sealed class MutableClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; private set; } = utcNow;
        public void Advance(TimeSpan duration) => UtcNow = UtcNow.Add(duration);
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
