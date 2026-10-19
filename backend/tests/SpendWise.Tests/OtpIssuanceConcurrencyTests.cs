using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Authentication;
using SpendWise.Application.Features.Authentication.Commands.ForgotPassword;
using SpendWise.Application.Features.Authentication.Commands.ResendRegistrationEmail;
using SpendWise.Application.Identity;
using SpendWise.Domain.Identity;
using SpendWise.Infrastructure.Identity;
using SpendWise.Infrastructure.Notifications;
using SpendWise.Infrastructure.Persistence;
using Xunit;

namespace SpendWise.Tests;

public sealed class OtpIssuanceConcurrencyTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Failed_or_cancelled_replacement_preserves_committed_otp_and_outbox(
        bool passwordReset, bool cancel)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var clock = new MutableClock();
        var options = new DbContextOptionsBuilder<SpendWiseDbContext>().UseSqlite(connection).Options;
        var policy = new TestEmailVerificationPolicy();
        Guid oldTokenId;
        Guid oldMessageId;
        string oldHtml;
        string oldText;
        string email;
        await using (var seed = new SpendWiseDbContext(options, clock))
        {
            await seed.Database.EnsureCreatedAsync();
            var unique = Guid.NewGuid().ToString("N");
            var user = new User
            {
                FullName = "OTP atomicity test", Email = $"otp-{unique}@test.local",
                NormalizedEmail = $"OTP-{unique.ToUpperInvariant()}@TEST.LOCAL",
                SpendId = unique, NormalizedSpendId = unique.ToUpperInvariant(),
                Phone = IdentityTestData.NewPhone(), PasswordHash = "test-only-hash",
                EmailVerifiedAt = passwordReset ? clock.UtcNow : null
            };
            email = user.Email;
            seed.Users.Add(user);
            await seed.SaveChangesAsync();
            var support = EmailVerificationTestFactory.Create(seed, clock, policy);
            if (passwordReset)
                await support.IssuePasswordResetOtpAsync(user, "192.0.2.10", CancellationToken.None);
            else
                await support.IssueRegistrationOtpAsync(user, "192.0.2.10", CancellationToken.None);
            oldTokenId = (await seed.EmailVerificationTokens.SingleAsync()).Id;
            var message = await seed.EmailOutboxMessages.SingleAsync();
            oldMessageId = message.Id;
            oldHtml = message.HtmlBody;
            oldText = message.TextBody;
        }

        clock.UtcNow = clock.UtcNow.AddMinutes(2);
        using var cancellation = new CancellationTokenSource();
        await using (var request = new SpendWiseDbContext(options, clock))
        {
            var support = new EmailVerificationSupport(request,
                new TestEmailVerificationOtpService(), policy, new EmailTemplateRenderer(),
                new EmailDeliveryQueue(request, new FailingProtector(cancellation, cancel)), clock);
            var error = await Record.ExceptionAsync(async () =>
            {
                if (passwordReset)
                    await new ForgotPasswordCommandHandler(request, support).Handle(
                        new ForgotPasswordCommand(new ForgotPasswordRequest(email), "192.0.2.10"),
                        cancellation.Token);
                else
                    await new ResendRegistrationEmailCommandHandler(request, support, policy, clock).Handle(
                        new ResendRegistrationEmailCommand(new ResendRegistrationEmailRequest(email), "192.0.2.10"),
                        cancellation.Token);
            });
            if (cancel)
                Assert.IsAssignableFrom<OperationCanceledException>(error);
            else
                Assert.IsType<InvalidOperationException>(error);
        }

        // Verify persisted state in a new context, not the failed request's tracked entities.
        await using var verify = new SpendWiseDbContext(options, clock);
        var token = await verify.EmailVerificationTokens.SingleAsync();
        Assert.Equal(oldTokenId, token.Id);
        Assert.Null(token.RevokedAt);
        Assert.Null(token.UsedAt);
        Assert.Equal(0, token.FailedAttempts);
        var preserved = await verify.EmailOutboxMessages.SingleAsync();
        Assert.Equal(oldMessageId, preserved.Id);
        Assert.Equal(SpendWise.Domain.Notifications.EmailDeliveryStatus.Pending, preserved.Status);
        Assert.Null(preserved.LastError);
        Assert.Equal(oldHtml, preserved.HtmlBody);
        Assert.Equal(oldText, preserved.TextBody);
        Assert.Empty(await verify.UserSessions.ToListAsync());
        Assert.Empty(await verify.LoginChallenges.ToListAsync());
    }

    private sealed class MutableClock : IClock
    {
        public DateTime UtcNow { get; set; } = new(2026, 10, 11, 12, 0, 0, DateTimeKind.Utc);
    }

    private sealed class FailingProtector(CancellationTokenSource cancellation, bool cancel)
        : IEmailContentProtector
    {
        public string Protect(string value)
        {
            if (!cancel)
                throw new InvalidOperationException("Controlled test-only protection failure.");
            cancellation.Cancel();
            throw new OperationCanceledException(cancellation.Token);
        }

        public string Unprotect(string value) => throw new NotSupportedException();
    }
}
