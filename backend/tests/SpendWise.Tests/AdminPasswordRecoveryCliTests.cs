using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using SpendWise.AdminPasswordRecovery;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Identity;
using SpendWise.Domain.Identity;
using SpendWise.Domain.Notifications;
using SpendWise.Infrastructure.Identity;
using SpendWise.Infrastructure.Notifications;
using SpendWise.Infrastructure.Persistence;
using SpendWise.Infrastructure.Services;
using Xunit;

namespace SpendWise.Tests;

public sealed class AdminPasswordRecoveryCliTests
{
    [Fact]
    public async Task Real_handlers_issue_deliver_reset_and_revoke_only_target_without_hosted_services()
    {
        await using var fixture = await Fixture.CreateAsync();
        Assert.Empty(fixture.Provider.GetServices<IHostedService>());
        RecoveryTicket ticket;
        await using (var scope = fixture.Provider.CreateAsyncScope())
        {
            var session = scope.ServiceProvider.GetRequiredService<RecoverySession>();
            Assert.Equal(fixture.UserId, await session.IdentifyAsync(default));
            ticket = await session.IssueAsync(fixture.UserId, default);
        }
        await using (var scope = fixture.Provider.CreateAsyncScope())
            Assert.True(await scope.ServiceProvider.GetRequiredService<RecoverySession>().DeliverAsync(ticket, default));
        Assert.Single(fixture.Sender.Messages);
        var otp = Regex.Match(fixture.Sender.Messages[0].TextBody, @"\b\d{8}\b").Value;
        Assert.Equal(8, otp.Length);
        const string syntheticPassword = "SyntheticNewPassword2026";
        await using (var scope = fixture.Provider.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<RecoverySession>().ResetAsync(ticket, otp, syntheticPassword, default);
        await using var db = fixture.Context();
        var user = await db.Users.SingleAsync(x => x.Id == fixture.UserId);
        Assert.True(new PasswordService().Verify(syntheticPassword, user.PasswordHash));
        Assert.Equal("synthetic-pin-unchanged", user.SecurityPinHash);
        Assert.Equal(SystemRole.SystemAdmin, user.SystemRole);
        Assert.Equal(RecoverySession.Email, user.Email);
        Assert.False(user.MustChangePassword);
        Assert.All(await db.UserSessions.Where(x => x.UserId == fixture.UserId).ToListAsync(), x => Assert.NotNull(x.RevokedAt));
        Assert.All(await db.RefreshTokens.Where(x => x.UserId == fixture.UserId).ToListAsync(), x => Assert.NotNull(x.RevokedAt));
        Assert.NotNull((await db.EmailVerificationTokens.SingleAsync(x => x.Id == ticket.TokenId)).UsedAt);
        Assert.Equal(EmailDeliveryStatus.Pending, (await db.EmailOutboxMessages.SingleAsync(x => x.DeduplicationKey == "unrelated-backlog")).Status);
        Assert.Equal(EmailDeliveryStatus.Pending, (await db.EmailOutboxMessages.SingleAsync(x => x.DeduplicationKey.StartsWith("email:password-reset-success:"))).Status);
        await using var replay = fixture.Provider.CreateAsyncScope();
        await Assert.ThrowsAsync<InvalidOperationException>(() => replay.ServiceProvider.GetRequiredService<RecoverySession>()
            .ResetAsync(ticket, otp, syntheticPassword, default));
    }

    [Fact]
    public async Task Wrong_otp_preserves_handler_failed_attempt_accounting_without_password_or_pin_change()
    {
        await using var fixture = await Fixture.CreateAsync();
        RecoveryTicket ticket;
        await using (var scope = fixture.Provider.CreateAsyncScope())
            ticket = await scope.ServiceProvider.GetRequiredService<RecoverySession>().IssueAsync(fixture.UserId, default);
        // Wrong length is rejected before any attempt; valid-shaped wrong code exercises the handler's accounting.
        await using (var scope = fixture.Provider.CreateAsyncScope())
            Assert.True(await scope.ServiceProvider.GetRequiredService<RecoverySession>().DeliverAsync(ticket, default));
        var correct = Regex.Match(fixture.Sender.Messages.Single().TextBody, @"\b\d{8}\b").Value;
        var wrong = correct == "00000000" ? "11111111" : "00000000";
        await using (var scope = fixture.Provider.CreateAsyncScope())
            await Assert.ThrowsAsync<AppException>(() => scope.ServiceProvider.GetRequiredService<RecoverySession>()
                .ResetAsync(ticket, wrong, "SyntheticNewPassword2026", default));
        await using var db = fixture.Context();
        var token = await db.EmailVerificationTokens.SingleAsync(x => x.Id == ticket.TokenId);
        Assert.Equal(1, token.FailedAttempts);
        Assert.Null(token.UsedAt);
        var user = await db.Users.SingleAsync(x => x.Id == fixture.UserId);
        Assert.Equal("synthetic-old-hash", user.PasswordHash);
        Assert.Equal("synthetic-pin-unchanged", user.SecurityPinHash);
        Assert.Null((await db.UserSessions.SingleAsync()).RevokedAt);
    }

    [Theory]
    [InlineData("wrong-id")]
    [InlineData("non-admin")]
    [InlineData("inactive")]
    [InlineData("unverified")]
    public async Task Account_guards_reject_before_creating_otp(string condition)
    {
        await using var fixture = await Fixture.CreateAsync();
        await using (var db = fixture.Context())
        {
            var user = await db.Users.SingleAsync();
            if (condition == "non-admin") user.SystemRole = SystemRole.User;
            if (condition == "inactive") user.Status = UserStatus.Locked;
            if (condition == "unverified") user.EmailVerifiedAt = null;
            await db.SaveChangesAsync();
        }
        await using var scope = fixture.Provider.CreateAsyncScope();
        await Assert.ThrowsAsync<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<RecoverySession>()
            .IssueAsync(condition == "wrong-id" ? Guid.NewGuid() : fixture.UserId, default));
        await using var check = fixture.Context();
        Assert.Empty(await check.EmailVerificationTokens.ToListAsync());
        Assert.Single(await check.EmailOutboxMessages.ToListAsync());
        Assert.Empty(fixture.Sender.Messages);
    }

    [Theory]
    [InlineData("ConnectionStrings:DefaultConnection", "Server=DESKTOP-B5KA3LT\\SQLEXPRESS02;Database=SpendWiseDb;Integrated Security=true")]
    [InlineData("ConnectionStrings:DefaultConnection", "Server=DESKTOP-B5KA3LT\\SQLEXPRESS01;Database=OtherDb;Integrated Security=true")]
    [InlineData("Email:DeliveryMode", "Pickup")]
    [InlineData("Email:Smtp:EnableSsl", "false")]
    public void Configuration_rejects_other_targets_and_pickup_before_connecting(string key, string value)
    {
        var config = Configuration();
        config[key] = value;
        Assert.Throws<InvalidOperationException>(() => RecoveryServices.ValidateConfiguration(config));
    }

    private static IConfigurationRoot Configuration() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["ConnectionStrings:DefaultConnection"] = @"Server=DESKTOP-B5KA3LT\SQLEXPRESS01;Database=SpendWiseDb;Integrated Security=true",
        ["Email:DeliveryMode"] = "Smtp", ["Email:Smtp:Host"] = "smtp.gmail.com", ["Email:Smtp:Port"] = "587",
        ["Email:Smtp:EnableSsl"] = "true", ["Email:Smtp:From"] = "sender@fixture.invalid",
        ["Email:Smtp:Username"] = "sender@fixture.invalid", ["Email:Smtp:Password"] = "synthetic-not-a-secret",
        ["EmailVerification:OtpPepper"] = new string('t', 64), ["Email:WebsiteUrl"] = "https://fixture.invalid"
    }).Build();

    private sealed class Sender : ITransactionalEmailSender
    {
        public List<EmailMessage> Messages { get; } = [];
        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
        { Messages.Add(message); return Task.CompletedTask; }
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection = new("Data Source=:memory:");
        public ServiceProvider Provider { get; private set; } = null!;
        public Sender Sender { get; } = new();
        public Guid UserId { get; private set; }
        public SpendWiseDbContext Context() => new(new DbContextOptionsBuilder<SpendWiseDbContext>()
            .UseSqlite(connection).Options, new SystemClock());
        public static async Task<Fixture> CreateAsync()
        {
            var fixture = new Fixture();
            await fixture.connection.OpenAsync();
            var config = Configuration();
            RecoveryServices.ValidateConfiguration(config);
            var services = RecoveryServices.Create(config);
            services.RemoveAll<SpendWiseDbContext>();
            services.AddScoped(_ => fixture.Context());
            services.RemoveAll<ITransactionalEmailSender>();
            services.AddSingleton<ITransactionalEmailSender>(fixture.Sender);
            services.RemoveAll<IEmailContentProtector>();
            services.AddSingleton<IEmailContentProtector, TestEmailContentProtector>();
            fixture.Provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            await using var db = fixture.Context();
            await db.Database.EnsureCreatedAsync();
            var user = new User { Email = RecoverySession.Email, NormalizedEmail = RecoverySession.Email.ToUpperInvariant(),
                FullName = "Synthetic Admin", SpendId = "fixture-admin", NormalizedSpendId = "FIXTURE-ADMIN",
                Phone = IdentityTestData.NewPhone(), PasswordHash = "synthetic-old-hash", SecurityPinHash = "synthetic-pin-unchanged",
                SystemRole = SystemRole.SystemAdmin, Status = UserStatus.Active, EmailVerifiedAt = DateTime.UtcNow, MustChangePassword = true };
            db.Users.Add(user);
            var session = new UserSession { UserId = user.Id, DeviceName = "synthetic", LastSeenAt = DateTime.UtcNow };
            db.UserSessions.Add(session);
            db.RefreshTokens.Add(new RefreshToken { UserId = user.Id, UserSessionId = session.Id, TokenHash = "synthetic-hash", ExpiresAt = DateTime.UtcNow.AddDays(1) });
            db.EmailOutboxMessages.Add(new EmailOutboxMessage { RecipientEmail = "backlog@fixture.invalid", Subject = "unrelated",
                TextBody = "unrelated", HtmlBody = "unrelated", DeduplicationKey = "unrelated-backlog" });
            await db.SaveChangesAsync();
            fixture.UserId = user.Id;
            return fixture;
        }
        public async ValueTask DisposeAsync() { await Provider.DisposeAsync(); await connection.DisposeAsync(); }
    }
}
