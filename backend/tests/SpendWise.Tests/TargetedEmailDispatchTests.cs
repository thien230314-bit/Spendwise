using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Identity;
using SpendWise.Domain.Notifications;
using SpendWise.Infrastructure.Notifications;
using SpendWise.Infrastructure.Persistence;
using Xunit;

namespace SpendWise.Tests;

public sealed class TargetedEmailDispatchTests
{
    [Fact]
    public async Task Targeted_dispatch_refuses_nonrelational_store_without_atomic_claim()
    {
        await using var db = new SpendWiseDbContext(new DbContextOptionsBuilder<SpendWiseDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, new TestClock());
        var sender = new Sender();
        var dispatcher = new EmailOutboxDispatcher(db, sender, new TestEmailContentProtector(),
            new TestClock(), NullLogger<EmailOutboxDispatcher>.Instance);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Dispatch(dispatcher, Guid.NewGuid()));
        Assert.Equal(0, sender.Count);
    }

    [Fact]
    public async Task Targeted_dispatch_sends_one_row_leaves_backlog_and_rejects_replay()
    {
        await using var fixture = await Fixture.CreateAsync();
        var sender = new Sender();
        await using var db = fixture.Context();
        var dispatcher = Dispatcher(db, sender, fixture);
        Assert.True(await Dispatch(dispatcher, fixture.TargetId));
        Assert.False(await Dispatch(dispatcher, fixture.TargetId));
        Assert.Equal(1, sender.Count);
        db.ChangeTracker.Clear();
        var target = await db.EmailOutboxMessages.SingleAsync(x => x.Id == fixture.TargetId);
        Assert.Equal(EmailDeliveryStatus.Sent, target.Status);
        Assert.Equal(1, target.AttemptCount);
        Assert.Empty(target.TextBody);
        var backlog = await db.EmailOutboxMessages.SingleAsync(x => x.Id != fixture.TargetId);
        Assert.Equal(EmailDeliveryStatus.Pending, backlog.Status);
        Assert.Equal(0, backlog.AttemptCount);
        Assert.Equal("backlog", backlog.TextBody);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("recipient")]
    [InlineData("key")]
    public async Task Missing_or_mismatched_target_is_not_claimed(string mismatch)
    {
        await using var fixture = await Fixture.CreateAsync();
        var sender = new Sender();
        await using var db = fixture.Context();
        Assert.False(await Dispatch(Dispatcher(db, sender, fixture),
            mismatch == "missing" ? Guid.NewGuid() : fixture.TargetId,
            mismatch == "recipient" ? "other@fixture.invalid" : Recipient,
            mismatch == "key" ? "wrong-key" : Key));
        Assert.Equal(0, sender.Count);
        Assert.All(await db.EmailOutboxMessages.AsNoTracking().ToListAsync(),
            item => Assert.Equal(0, item.AttemptCount));
    }

    [Fact]
    public async Task Competing_targeted_and_batch_dispatchers_do_not_send_same_row_twice()
    {
        await using var fixture = await Fixture.CreateAsync();
        var sender = new Sender { Hold = true };
        await using var firstDb = fixture.Context();
        await using var secondDb = fixture.Context();
        var first = Dispatch(Dispatcher(firstDb, sender, fixture), fixture.TargetId);
        await sender.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            Assert.False(await Dispatch(Dispatcher(secondDb, sender, fixture), fixture.TargetId));
            // Hide backlog from this existing batch scan: only the claimed row is eligible.
            await secondDb.EmailOutboxMessages.Where(x => x.Id != fixture.TargetId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.NextAttemptAt, fixture.Clock.UtcNow.AddHours(1)));
            Assert.Equal(0, await Dispatcher(secondDb, sender, fixture).DispatchPendingAsync(default));
            Assert.Equal(1, sender.Count);
        }
        finally { sender.Release.TrySetResult(); }
        Assert.True(await first);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Expiry_and_delivery_failure_follow_existing_lifecycle(bool fail)
    {
        await using var fixture = await Fixture.CreateAsync();
        await using var db = fixture.Context();
        if (!fail)
            await db.EmailOutboxMessages.Where(x => x.Id == fixture.TargetId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.ContentExpiresAt, fixture.Clock.UtcNow.AddMinutes(-1)));
        var sender = new Sender { Fail = fail };
        Assert.False(await Dispatch(Dispatcher(db, sender, fixture), fixture.TargetId));
        var item = await db.EmailOutboxMessages.AsNoTracking().SingleAsync(x => x.Id == fixture.TargetId);
        Assert.Equal(fail ? EmailDeliveryStatus.Pending : EmailDeliveryStatus.Failed, item.Status);
        Assert.Equal(fail ? "delivery_failed" : "content_expired", item.LastError);
        Assert.Equal(1, item.AttemptCount);
        Assert.Equal(fail ? 1 : 0, sender.Count);
        if (fail) Assert.NotNull(item.NextAttemptAt);
    }

    [Theory]
    [InlineData("processing")]
    [InlineData("backoff")]
    [InlineData("exhausted")]
    [InlineData("failed")]
    public async Task Targeted_dispatch_respects_inflight_retry_and_terminal_guards(string state)
    {
        await using var fixture = await Fixture.CreateAsync();
        await using var db = fixture.Context();
        var item = await db.EmailOutboxMessages.SingleAsync(x => x.Id == fixture.TargetId);
        if (state == "processing")
        {
            item.Status = EmailDeliveryStatus.Processing;
            item.ProcessingStartedAt = fixture.Clock.UtcNow.AddHours(-1);
        }
        if (state == "backoff") item.NextAttemptAt = fixture.Clock.UtcNow.AddHours(1);
        if (state == "exhausted") item.AttemptCount = 5;
        if (state == "failed") item.Status = EmailDeliveryStatus.Failed;
        await db.SaveChangesAsync();
        var sender = new Sender();
        Assert.False(await Dispatch(Dispatcher(db, sender, fixture), fixture.TargetId));
        Assert.Equal(0, sender.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Auth_recovery_worker_delivers_only_new_configured_valid_Admin_recovery_without_manual_dispatch(bool pin)
    {
        await using var fixture = await Fixture.CreateAsync();
        await using var db = fixture.Context();
        await SeedRecoveryAsync(fixture, db, pin);
        var sender = new Sender();
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        services.AddSingleton<IClock>(fixture.Clock);
        services.AddScoped(_ => fixture.Context());
        services.AddSingleton<ITransactionalEmailSender>(sender);
        services.AddSingleton<IEmailContentProtector, TestEmailContentProtector>();
        services.AddLogging();
        services.AddScoped<EmailOutboxDispatcher>();
        services.AddScoped<AuthRecoveryEmailDispatcher>();
        services.AddSingleton(new AuthRecoveryDeliveryScope(fixture.Clock.UtcNow.AddSeconds(-1), [Recipient]));
        await using var provider = services.BuildServiceProvider();
        using var worker = new AuthRecoveryEmailBackgroundService(provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<AuthRecoveryDeliveryScope>(), NullLogger<AuthRecoveryEmailBackgroundService>.Instance);
        await worker.StartAsync(default);
        await sender.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        using var deliveryDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (true)
        {
            await using var observed = fixture.Context();
            if (await observed.EmailOutboxMessages.AsNoTracking().AnyAsync(x => x.Id == fixture.TargetId
                && x.Status == EmailDeliveryStatus.Sent, deliveryDeadline.Token)) break;
            await Task.Delay(10, deliveryDeadline.Token);
        }
        await worker.StopAsync(default);
        await using var verify = fixture.Context();
        Assert.Equal(EmailDeliveryStatus.Sent, (await verify.EmailOutboxMessages.SingleAsync(x=>x.Id==fixture.TargetId)).Status);
        Assert.Equal(EmailDeliveryStatus.Pending, (await verify.EmailOutboxMessages.SingleAsync(x=>x.Id!=fixture.TargetId)).Status);
        Assert.Equal(1, sender.Count);
    }

    private static async Task SeedRecoveryAsync(Fixture fixture, SpendWiseDbContext db, bool pin)
    {
        var user = new SpendWise.Domain.Identity.User { FullName = "Synthetic Admin", Email = Recipient,
            NormalizedEmail = Recipient.ToUpperInvariant(), SpendId = Guid.NewGuid().ToString("N"),
            NormalizedSpendId = Guid.NewGuid().ToString("N"), Phone = IdentityTestData.NewPhone(),
            SystemRole = SpendWise.Domain.Identity.SystemRole.SystemAdmin, EmailVerifiedAt = fixture.Clock.UtcNow };
        db.Users.Add(user);
        var otp = new SpendWise.Domain.Identity.EmailVerificationToken { UserId = user.Id,
            Purpose = SpendWise.Domain.Identity.EmailVerificationPurpose.ResetPassword,
            CodeHash = "synthetic", ExpiresAt = fixture.Clock.UtcNow.AddMinutes(10) };
        db.EmailVerificationTokens.Add(otp);
        var row = await db.EmailOutboxMessages.SingleAsync(x => x.Id == fixture.TargetId);
        row.DeduplicationKey = $"email:password-reset-otp:{otp.Id:N}";
        row.ContentExpiresAt = otp.ExpiresAt;
        if (pin)
        {
            var token = new SpendWise.Domain.Identity.SecurityPinResetToken { UserId=user.Id,
                TokenHash=new string('A',64), Purpose=SpendWise.Domain.Identity.SecurityPinResetPurpose.SelfService,
                ExpiresAt=fixture.Clock.UtcNow.AddMinutes(15) };
            db.SecurityPinResetTokens.Add(token);
            row.DeduplicationKey=$"email:security-pin-reset:{token.TokenHash}";
            row.ContentExpiresAt=token.ExpiresAt;
        }
        await db.SaveChangesAsync();
    }

    [Theory]
    [InlineData("old")][InlineData("recipient")][InlineData("expired")][InlineData("revoked")]
    [InlineData("used")][InlineData("User")][InlineData("inactive")][InlineData("unverified")]
    [InlineData("wrong-key")][InlineData("content-expired")][InlineData("attempted")][InlineData("sent")]
    public async Task Scoped_recovery_refuses_out_of_scope_stale_or_invalid_rows(string condition)
    {
        await using var fixture=await Fixture.CreateAsync(); await using var db=fixture.Context();
        await SeedRecoveryAsync(fixture,db,false);
        var row=await db.EmailOutboxMessages.SingleAsync(x=>x.Id==fixture.TargetId);
        var token=await db.EmailVerificationTokens.SingleAsync(); var user=await db.Users.SingleAsync();
        if(condition=="expired")token.ExpiresAt=fixture.Clock.UtcNow.AddSeconds(-1);
        if(condition=="revoked")token.RevokedAt=fixture.Clock.UtcNow;
        if(condition=="used")token.UsedAt=fixture.Clock.UtcNow;
        if(condition=="User")user.SystemRole=SpendWise.Domain.Identity.SystemRole.User;
        if(condition=="inactive")user.Status=SpendWise.Domain.Identity.UserStatus.Locked;
        if(condition=="unverified")user.EmailVerifiedAt=null;
        if(condition=="wrong-key")row.DeduplicationKey="email:password-reset-otp:invalid";
        if(condition=="content-expired")row.ContentExpiresAt=fixture.Clock.UtcNow.AddSeconds(-1);
        if(condition=="attempted")row.AttemptCount=1;
        if(condition=="sent")row.Status=EmailDeliveryStatus.Sent;
        await db.SaveChangesAsync(); var sender=new Sender();
        var scope=new AuthRecoveryDeliveryScope(fixture.Clock.UtcNow.AddSeconds(condition=="old"?1:-1),
            [condition=="recipient"?"other@fixture.invalid":Recipient]);
        Assert.Equal(0,await new AuthRecoveryEmailDispatcher(db,Dispatcher(db,sender,fixture),fixture.Clock).DispatchEligibleAsync(scope,default));
        Assert.Equal(0,sender.Count);
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task Scoped_SMTP_failure_or_cancellation_never_fakes_Sent_or_automatically_retries(bool cancellation)
    {
        await using var fixture=await Fixture.CreateAsync(); await using var db=fixture.Context();
        await SeedRecoveryAsync(fixture,db,false);
        var sender=new Sender{Fail=!cancellation,Hold=cancellation};
        var scope=new AuthRecoveryDeliveryScope(fixture.Clock.UtcNow.AddSeconds(-1),[Recipient]);
        var dispatcher=new AuthRecoveryEmailDispatcher(db,Dispatcher(db,sender,fixture),fixture.Clock);
        using var deadline=new CancellationTokenSource();
        var sending=dispatcher.DispatchEligibleAsync(scope,deadline.Token);
        if(cancellation){await sender.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));deadline.Cancel();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>sending);}
        else Assert.Equal(0,await sending);
        await using var verify=fixture.Context();
        var row=await verify.EmailOutboxMessages.AsNoTracking().SingleAsync(x=>x.Id==fixture.TargetId);
        Assert.NotEqual(EmailDeliveryStatus.Sent,row.Status); Assert.NotEmpty(row.HtmlBody);
        Assert.Equal(1,row.AttemptCount); Assert.Null(row.SentAt);
        Assert.Equal(0,await new AuthRecoveryEmailDispatcher(verify,Dispatcher(verify,sender,fixture),fixture.Clock).DispatchEligibleAsync(scope,default));
        Assert.Equal(1,sender.Count);
        Assert.Null((await verify.EmailVerificationTokens.SingleAsync()).UsedAt);
    }

    private const string Recipient = "admin@fixture.invalid";
    [Fact]
    public async Task Concurrent_scoped_recovery_dispatchers_share_existing_atomic_claim()
    {
        await using var fixture=await Fixture.CreateAsync();await using var first=fixture.Context();
        await SeedRecoveryAsync(fixture,first,false);var sender=new Sender{Hold=true};
        var scope=new AuthRecoveryDeliveryScope(fixture.Clock.UtcNow.AddSeconds(-1),[Recipient]);
        var sending=new AuthRecoveryEmailDispatcher(first,Dispatcher(first,sender,fixture),fixture.Clock).DispatchEligibleAsync(scope,default);
        await sender.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            await using var second=fixture.Context();
            Assert.Equal(0,await new AuthRecoveryEmailDispatcher(second,Dispatcher(second,sender,fixture),fixture.Clock).DispatchEligibleAsync(scope,default));
        }
        finally{sender.Release.TrySetResult();}
        Assert.Equal(1,await sending);Assert.Equal(1,sender.Count);
    }

    private const string Key = "email:password-reset-otp:fixture";
    private static EmailOutboxDispatcher Dispatcher(SpendWiseDbContext db, Sender sender, Fixture fixture) =>
        new(db, sender, new TestEmailContentProtector(), fixture.Clock, NullLogger<EmailOutboxDispatcher>.Instance);

    private static Task<bool> Dispatch(EmailOutboxDispatcher dispatcher, Guid id,
        string recipient = Recipient, string key = Key)
    {
        return dispatcher.DispatchByIdAsync(id, recipient, key, CancellationToken.None);
    }

    private sealed class Sender : ITransactionalEmailSender
    {
        public int Count;
        public bool Hold, Fail;
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Count);
            Entered.TrySetResult();
            if (Hold) await Release.Task.WaitAsync(cancellationToken);
            if (Fail) throw new InvalidOperationException("synthetic provider failure");
        }
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly string path = Path.Combine(Path.GetTempPath(), $"SpendWise-targeted-email-{Guid.NewGuid():N}.db");
        public TestClock Clock { get; } = new();
        public Guid TargetId { get; private set; }
        public SpendWiseDbContext Context() => new(new DbContextOptionsBuilder<SpendWiseDbContext>()
            .UseSqlite($"Data Source={path};Pooling=False").Options, Clock);
        public static async Task<Fixture> CreateAsync()
        {
            var fixture = new Fixture();
            await using var db = fixture.Context();
            await db.Database.EnsureCreatedAsync();
            var target = new EmailOutboxMessage { RecipientEmail = Recipient, Subject = "synthetic",
                HtmlBody = "html", TextBody = "text", DeduplicationKey = Key };
            db.EmailOutboxMessages.AddRange(target, new EmailOutboxMessage { RecipientEmail = "backlog@fixture.invalid",
                Subject = "backlog", HtmlBody = "backlog", TextBody = "backlog", DeduplicationKey = "backlog" });
            await db.SaveChangesAsync();
            fixture.TargetId = target.Id;
            return fixture;
        }
        public ValueTask DisposeAsync() { File.Delete(path); return ValueTask.CompletedTask; }
    }
    private sealed class TestClock : IClock { public DateTime UtcNow { get; } = DateTime.UtcNow; }
}
