using System.Text.RegularExpressions;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Authentication;
using SpendWise.Application.Features.Authentication.Commands.VerifyRegistrationEmail;
using SpendWise.Application.Features.Authentication.Commands.ResetPassword;
using SpendWise.Application.Identity;
using SpendWise.Domain.Identity;
using SpendWise.Infrastructure.Identity;
using SpendWise.Infrastructure.Notifications;
using SpendWise.Infrastructure.Persistence;
using Xunit;

namespace SpendWise.Tests;

// Real SQL Server integration proof. This never reads application configuration or User Secrets.
public sealed class OtpSqlServerConcurrencyTests : SqlTestFixtureScope
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(7)]
    public void SQL_lock_success_results_are_admitted(int result)
        => Assert.True(InterpretLockResult(result, CancellationToken.None));

    [Fact]
    public void SQL_lock_timeout_is_the_only_expected_negative_admission_result()
        => Assert.False(InterpretLockResult(-1, CancellationToken.None));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SQL_lock_cancellation_propagates_with_the_supplied_cancellation_token(bool cancelled)
    {
        using var source = new CancellationTokenSource();
        if (cancelled)
            source.Cancel();
        var token = cancelled ? source.Token : CancellationToken.None;
        var error = Assert.ThrowsAny<OperationCanceledException>(() => InterpretLockResult(-2, token));
        Assert.Equal(token, error.CancellationToken);
    }

    [Theory]
    [InlineData(-3)]
    [InlineData(-999)]
    [InlineData(-42)]
    [InlineData(int.MinValue)]
    public void SQL_lock_faults_and_unknown_negative_results_cannot_be_disguised_as_quota_denials(int result)
        => Assert.Throws<InvalidOperationException>(() => InterpretLockResult(result, CancellationToken.None));

    public static IEnumerable<object?[]> InvalidScalarResults =>
    [
        [null], [DBNull.Value], ["0"], ["-1"], [(short)0], [0L], [true]
    ];

    [Theory]
    [MemberData(nameof(InvalidScalarResults))]
    public void SQL_lock_missing_or_unexpected_scalar_types_fail_closed(object? result)
        => Assert.Throws<InvalidOperationException>(() => InterpretLockResult(result, CancellationToken.None));

    [Theory]
    [InlineData(-2, false)]
    [InlineData(-2, true)]
    [InlineData(-3, false)]
    [InlineData(-999, false)]
    [InlineData(-42, false)]
    public async Task SQL_lock_fault_classification_rolls_back_real_transaction_and_releases_both_locks(
        int result, bool cancelled)
    {
        var database = await OwnAsync(OwnedDatabase.CreateAsync());
        var userId = await database.SeedUserAsync(false);
        Guid tokenId;
        await using (var seed = database.Context())
        {
            var token = new EmailVerificationToken
            {
                UserId = userId, Purpose = EmailVerificationPurpose.Registration,
                CodeHash = "test-only-hash", ExpiresAt = database.Clock.UtcNow.AddMinutes(10)
            };
            seed.EmailVerificationTokens.Add(token);
            await seed.SaveChangesAsync();
            tokenId = token.Id;
        }
        using var cancellation = new CancellationTokenSource();
        if (cancelled)
            cancellation.Cancel();
        var classificationToken = cancelled ? cancellation.Token : CancellationToken.None;
        const string ipHash = "s04-1-owned-test-ip";
        await using var failing = database.Context();
        await failing.Database.OpenConnectionAsync();
        var error = await Record.ExceptionAsync(() => failing.ExecuteInTransactionAsync(async ct =>
        {
            Assert.True(await failing.TryAcquireEmailVerificationLockAsync(userId, ipHash, ct));
            var user = await failing.Users.SingleAsync(x => x.Id == userId, ct);
            user.FullName = "must roll back";
            await failing.EmailVerificationTokens.Where(x => x.Id == tokenId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.FailedAttempts, 1), ct);
            await Support(failing, database.Clock, new TestEmailContentProtector()).QueueWelcomeAsync(user, ct);
            // Inject only the server result classification, not a simulated transaction/lock.
            return InterpretLockResult(result, classificationToken);
        }));
        if (result == -2)
            Assert.Equal(classificationToken, Assert.IsAssignableFrom<OperationCanceledException>(error).CancellationToken);
        else
            Assert.IsType<InvalidOperationException>(error);

        // The failing context/connection remains open: lock release must come from rollback.
        Assert.Equal(System.Data.ConnectionState.Open, failing.Database.GetDbConnection().State);
        await using var independent = database.Context();
        Assert.True(await independent.ExecuteInTransactionAsync(ct =>
            independent.TryAcquireEmailVerificationLockAsync(userId, ipHash, ct)));
        var preserved = await independent.EmailVerificationTokens.SingleAsync();
        Assert.Equal(tokenId, preserved.Id);
        Assert.Equal(0, preserved.FailedAttempts);
        Assert.Null(preserved.UsedAt);
        Assert.Null(preserved.RevokedAt);
        Assert.Equal("Isolated SQL OTP test", (await independent.Users.SingleAsync()).FullName);
        Assert.Empty(await independent.EmailOutboxMessages.ToListAsync());
        Assert.Empty(await independent.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task SQL_lock_timeout_false_keeps_state_empty_and_releases_owned_transaction_locks()
    {
        var database = await OwnAsync(OwnedDatabase.CreateAsync());
        var userId = await database.SeedUserAsync(false);
        const string ipHash = "s04-1-owned-timeout-ip";
        await using var timedOut = database.Context();
        await timedOut.Database.OpenConnectionAsync();
        var admitted = await timedOut.ExecuteInTransactionAsync(async ct =>
        {
            Assert.True(await timedOut.TryAcquireEmailVerificationLockAsync(userId, ipHash, ct));
            return InterpretLockResult(-1, ct);
        });
        Assert.False(admitted);
        Assert.Equal(System.Data.ConnectionState.Open, timedOut.Database.GetDbConnection().State);
        await using var independent = database.Context();
        Assert.True(await independent.ExecuteInTransactionAsync(ct =>
            independent.TryAcquireEmailVerificationLockAsync(userId, ipHash, ct)));
        Assert.Empty(await independent.EmailVerificationTokens.ToListAsync());
        Assert.Empty(await independent.EmailOutboxMessages.ToListAsync());
        Assert.Empty(await independent.AuditLogs.ToListAsync());
        Assert.Equal("Isolated SQL OTP test", (await independent.Users.SingleAsync()).FullName);
    }

    private static bool InterpretLockResult(object? result, CancellationToken token)
    {
        var classifier = typeof(SpendWiseDbContext).GetMethod("InterpretEmailVerificationLockResult",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("OTP lock result classifier is missing.");
        try
        {
            return (bool)classifier.Invoke(null, [result, token])!;
        }
        catch (TargetInvocationException error) when (error.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(error.InnerException).Throw();
            throw;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Simultaneous_issuance_for_same_account_rejects_without_duplicate_token_or_email(
        bool passwordReset)
        => await AssertConcurrentIssuanceAsync(passwordReset, passwordReset, false, false, true);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Different_accounts_and_IPs_can_issue_while_another_request_is_in_flight(bool reset)
        => await AssertConcurrentIssuanceAsync(reset, reset, true, false, false);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Shared_IP_preserves_under_quota_requests_for_different_accounts(bool reset)
        => await AssertConcurrentIssuanceAsync(reset, !reset, true, true, false);

    [Fact]
    public async Task Same_account_cannot_bypass_admission_by_changing_OTP_purpose()
        => await AssertConcurrentIssuanceAsync(false, true, false, false, true);

    private async Task AssertConcurrentIssuanceAsync(
        bool passwordReset, bool secondReset, bool differentAccount, bool sharedIp, bool reject)
    {
        var database = await OwnAsync(OwnedDatabase.CreateAsync());
        var userId = await database.SeedUserAsync(passwordReset);
        var secondId = differentAccount ? await database.SeedUserAsync(secondReset) : userId;
        var gate = new GatedProtector();
        await using var firstContext = database.Context();
        await using var secondContext = database.Context();
        var firstUser = await firstContext.Users.SingleAsync(x => x.Id == userId);
        var secondUser = await secondContext.Users.SingleAsync(x => x.Id == secondId);
        var firstSupport = Support(firstContext, database.Clock, gate);
        var secondSupport = Support(secondContext, database.Clock, new TestEmailContentProtector());
        // Pause after quota reads and revocation, before any first-request token/outbox commit.
        var first = Task.Run(() => Issue(firstSupport, firstUser, passwordReset, "192.0.2.40"));
        Exception? secondError;
        try
        {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var second = Issue(secondSupport, secondUser, secondReset,
                sharedIp ? "192.0.2.40" : "192.0.2.41");
            // Same-IP admission may serialize quota reads briefly, but must not reject
            // a different account merely because another under-quota request is in flight.
            if (sharedIp)
                gate.Release.TrySetResult();
            secondError = await Record.ExceptionAsync(() => DrainOnTimeout(second, gate));
        }
        finally
        {
            gate.Release.TrySetResult();
            await first.WaitAsync(TimeSpan.FromSeconds(10));
        }

        if (reject)
            Assert.Equal(429, Assert.IsType<AppException>(secondError).StatusCode);
        else
            Assert.Null(secondError);
        await using var verify = database.Context();
        var tokens = await verify.EmailVerificationTokens.ToListAsync();
        Assert.Equal(reject ? 1 : 2, tokens.Count);
        Assert.Contains(tokens, token => token.UserId == userId);
        Assert.All(tokens, token =>
        {
            Assert.Null(token.UsedAt);
            Assert.Null(token.RevokedAt);
            Assert.Equal(0, token.FailedAttempts);
        });
        Assert.Equal(reject ? 1 : 2, await verify.EmailOutboxMessages.CountAsync());
        Assert.Empty(await verify.UserSessions.ToListAsync());
        Assert.Empty(await verify.LoginChallenges.ToListAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IP_hour_quota_is_shared_across_accounts_and_OTP_purposes(bool firstReset)
    {
        var database = await OwnAsync(OwnedDatabase.CreateAsync());
        var firstId = await database.SeedUserAsync(firstReset);
        var secondId = await database.SeedUserAsync(!firstReset);
        var policy = new TestEmailVerificationPolicy { MaximumSendsPerHourPerIp = 1 };
        await using (var first = database.Context())
            await Issue(Support(first, database.Clock, new TestEmailContentProtector(), policy),
                await first.Users.SingleAsync(x => x.Id == firstId), firstReset, "192.0.2.50");
        await using (var second = database.Context())
        {
            var error = await Record.ExceptionAsync(() => Issue(
                Support(second, database.Clock, new TestEmailContentProtector(), policy),
                second.Users.Single(x => x.Id == secondId), !firstReset, "192.0.2.50"));
            Assert.Equal(429, Assert.IsType<AppException>(error).StatusCode);
        }
        await using var verify = database.Context();
        Assert.Equal(firstId, (await verify.EmailVerificationTokens.SingleAsync()).UserId);
        Assert.Single(await verify.EmailOutboxMessages.ToListAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Concurrent_shared_IP_waits_for_committed_cross_purpose_quota_before_rejecting(
        bool firstReset)
    {
        var database = await OwnAsync(OwnedDatabase.CreateAsync());
        var firstId = await database.SeedUserAsync(firstReset);
        var secondId = await database.SeedUserAsync(!firstReset);
        var policy = new TestEmailVerificationPolicy { MaximumSendsPerHourPerIp = 1 };
        var gate = new GatedProtector();
        await using var firstContext = database.Context();
        await using var secondContext = database.Context();
        var firstUser = await firstContext.Users.SingleAsync(x => x.Id == firstId);
        var secondUser = await secondContext.Users.SingleAsync(x => x.Id == secondId);
        // Keep this exact SQL session open so the DMV observation cannot follow a pooled replacement.
        await secondContext.Database.OpenConnectionAsync();
        await using var sessionCommand = secondContext.Database.GetDbConnection().CreateCommand();
        sessionCommand.CommandText = "SELECT @@SPID";
        var secondSession = Convert.ToInt32(await sessionCommand.ExecuteScalarAsync());
        var first = Task.Run(() => Issue(
            Support(firstContext, database.Clock, gate, policy), firstUser, firstReset, "192.0.2.55"));
        Task? second = null;
        Exception? error = null;
        var observedWait = false;
        try
        {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            second = Issue(Support(secondContext, database.Clock, new TestEmailContentProtector(), policy),
                secondUser, !firstReset, "192.0.2.55");
            observedWait = await database.ObserveApplicationLockWaitAsync(secondSession, second);
            gate.Release.TrySetResult();
            error = await Record.ExceptionAsync(() => DrainOnTimeout(second!, gate));
        }
        finally
        {
            gate.Release.TrySetResult();
            if (second is not null)
                await Record.ExceptionAsync(() => second!);
            await first.WaitAsync(TimeSpan.FromSeconds(10));
        }
        Assert.True(observedWait, "The second SQL session must actually wait for the shared-IP admission lock.");
        Assert.Equal(429, Assert.IsType<AppException>(error).StatusCode);
        await using var verify = database.Context();
        var token = Assert.Single(await verify.EmailVerificationTokens.ToListAsync());
        Assert.Equal(firstId, token.UserId);
        Assert.Null(token.UsedAt);
        Assert.Null(token.RevokedAt);
        Assert.Equal(0, token.FailedAttempts);
        Assert.Single(await verify.EmailOutboxMessages.ToListAsync());
        Assert.Empty(await verify.UserSessions.ToListAsync());
        Assert.Empty(await verify.LoginChallenges.ToListAsync());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Failed_or_cancelled_SQL_transaction_releases_account_and_IP_admission(
        bool reset, bool cancel)
    {
        var database = await OwnAsync(OwnedDatabase.CreateAsync());
        var userId = await database.SeedUserAsync(reset);
        using var cancellation = new CancellationTokenSource();
        await using (var first = database.Context())
        {
            var support = Support(first, database.Clock, new FailingProtector(cancellation, cancel));
            var user = await first.Users.SingleAsync(x => x.Id == userId);
            var error = await Record.ExceptionAsync(() => reset
                ? support.IssuePasswordResetOtpAsync(user, "192.0.2.60", cancellation.Token)
                : support.IssueRegistrationOtpAsync(user, "192.0.2.60", cancellation.Token));
            if (cancel)
                Assert.IsAssignableFrom<OperationCanceledException>(error);
            else
                Assert.IsType<InvalidOperationException>(error);
        }
        await using (var second = database.Context())
            await Issue(Support(second, database.Clock, new TestEmailContentProtector()),
                await second.Users.SingleAsync(x => x.Id == userId), reset, "192.0.2.60")
                .WaitAsync(TimeSpan.FromSeconds(10));
        await using var verify = database.Context();
        Assert.Single(await verify.EmailVerificationTokens.ToListAsync());
        Assert.Single(await verify.EmailOutboxMessages.ToListAsync());
    }

    [Theory]
    [InlineData("12345678")]
    [InlineData("87654321")]
    public async Task Verification_during_replacement_fails_generically_without_attempt_or_challenge_changes(
        string submittedOtp)
    {
        var database = await OwnAsync(OwnedDatabase.CreateAsync());
        var userId = await database.SeedUserAsync(false);
        var otpService = new TestEmailVerificationOtpService();
        Guid oldId;
        await using (var seed = database.Context())
        {
            var oldToken = new EmailVerificationToken
            {
                UserId = userId, Purpose = EmailVerificationPurpose.Registration,
                CodeHash = otpService.HashOtp(userId, EmailVerificationSupport.RegistrationPurpose,
                    IdentityTestData.DefaultEmailOtp),
                CreatedAt = database.Clock.UtcNow.AddMinutes(-2),
                ExpiresAt = database.Clock.UtcNow.AddMinutes(8)
            };
            seed.EmailVerificationTokens.Add(oldToken);
            await seed.SaveChangesAsync();
            // Explicit test timestamp avoids depending on SaveChanges timestamp conventions.
            await seed.EmailVerificationTokens.Where(x => x.Id == oldToken.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.CreatedAt,
                    database.Clock.UtcNow.AddMinutes(-2)));
            oldId = oldToken.Id;
        }

        var gate = new GatedProtector();
        await using var issuing = database.Context();
        var user = await issuing.Users.SingleAsync(x => x.Id == userId);
        var first = Task.Run(() => Issue(Support(issuing, database.Clock, gate), user, false, "192.0.2.70"));
        await using var verifying = database.Context();
        Exception? busyError;
        try
        {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var handler = RegistrationVerifier(verifying, database.Clock);
            var verification = handler.Handle(
                new VerifyRegistrationEmailCommand(
                    new VerifyRegistrationEmailRequest(user.Email, submittedOtp, "SQL test"),
                    "192.0.2.71", "test-only"), CancellationToken.None);
            busyError = await Record.ExceptionAsync(() => DrainOnTimeout(verification, gate));
        }
        finally
        {
            gate.Release.TrySetResult();
            await first.WaitAsync(TimeSpan.FromSeconds(10));
        }
        var rejected = Assert.IsType<AppException>(busyError);
        Assert.Equal(400, rejected.StatusCode);
        Assert.Equal("email_verification_failed", rejected.Code);
        await using (var verify = database.Context())
        {
            var tokens = await verify.EmailVerificationTokens.ToListAsync();
            Assert.Equal(2, tokens.Count);
            Assert.NotNull(tokens.Single(x => x.Id == oldId).RevokedAt);
            Assert.All(tokens, token => Assert.Equal(0, token.FailedAttempts));
            Assert.All(tokens, token => Assert.Null(token.UsedAt));
            Assert.Null((await verify.Users.SingleAsync()).EmailVerifiedAt);
            Assert.Empty(await verify.LoginChallenges.ToListAsync());
            Assert.Empty(await verify.UserSessions.ToListAsync());
            Assert.Single(await verify.EmailOutboxMessages.ToListAsync());
        }

        // The newly committed OTP still verifies; replay cannot create a second welcome/challenge.
        var command = new VerifyRegistrationEmailCommand(
            new VerifyRegistrationEmailRequest(user.Email, IdentityTestData.DefaultEmailOtp, "SQL test"),
            "192.0.2.71", "test-only");
        await using (var successful = database.Context())
            await RegistrationVerifier(successful, database.Clock).Handle(command, CancellationToken.None);
        await using (var replay = database.Context())
        {
            var error = await Record.ExceptionAsync(() =>
                RegistrationVerifier(replay, database.Clock).Handle(command, CancellationToken.None));
            Assert.Equal("email_verification_failed", Assert.IsType<AppException>(error).Code);
        }
        await using var final = database.Context();
        Assert.NotNull((await final.Users.SingleAsync()).EmailVerifiedAt);
        Assert.Single(await final.EmailVerificationTokens.Where(x => x.UsedAt != null).ToListAsync());
        Assert.Single(await final.LoginChallenges.ToListAsync());
        Assert.Equal(2, await final.EmailOutboxMessages.CountAsync());
        Assert.Empty(await final.UserSessions.ToListAsync());
    }

    private static VerifyRegistrationEmailCommandHandler RegistrationVerifier(SpendWiseDbContext db, IClock clock) =>
        new(db, new TestEmailVerificationOtpService(), new TestEmailVerificationPolicy(),
            Support(db, clock, new TestEmailContentProtector()),
            new LoginChallengeSupport(db, new LoginChallengeTokenService(), clock), clock);

    // WaitAsync times out only the observer, not the SQL command. Release the barrier
    // and drain the original operation before its context is disposed, preserving timeout evidence.
    private static async Task DrainOnTimeout(Task operation, GatedProtector gate)
    {
        try
        {
            await operation.WaitAsync(TimeSpan.FromSeconds(10));
        }
        catch (TimeoutException)
        {
            gate.Release.TrySetResult();
            await Record.ExceptionAsync(() => operation);
            throw;
        }
    }

    [Theory]
    [InlineData("12345678")]
    [InlineData("87654321")]
    public async Task Reset_during_replacement_does_not_change_password_attempts_or_sessions(
        string submittedOtp)
    {
        var database = await OwnAsync(OwnedDatabase.CreateAsync());
        var userId = await database.SeedUserAsync(true);
        var passwords = new PasswordService();
        var oldHash = passwords.Hash("OldPassword1");
        Guid oldId;
        await using (var seed = database.Context())
        {
            var user = await seed.Users.SingleAsync(x => x.Id == userId);
            user.PasswordHash = oldHash;
            var oldToken = new EmailVerificationToken
            {
                UserId = userId, Purpose = EmailVerificationPurpose.ResetPassword,
                CodeHash = new TestEmailVerificationOtpService().HashOtp(userId,
                    EmailVerificationSupport.PasswordResetPurpose, IdentityTestData.DefaultEmailOtp),
                ExpiresAt = database.Clock.UtcNow.AddMinutes(8)
            };
            seed.EmailVerificationTokens.Add(oldToken);
            // A real persisted pre-existing session verifies reset side effects, without login/SMTP.
            await new AuthenticationSupport(seed, new TargetedTokenService(), database.Clock)
                .IssueAsync(user, "SQL test", "192.0.2.80", "test-only", CancellationToken.None);
            await seed.EmailVerificationTokens.Where(x => x.Id == oldToken.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.CreatedAt,
                    database.Clock.UtcNow.AddMinutes(-2)));
            oldId = oldToken.Id;
        }
        var gate = new GatedProtector();
        await using var issuing = database.Context();
        var account = await issuing.Users.SingleAsync(x => x.Id == userId);
        var first = Task.Run(() => Issue(Support(issuing, database.Clock, gate), account, true, "192.0.2.81"));
        await using var resetting = database.Context();
        Exception? busyError;
        try
        {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var reset = ResetHandler(resetting, database.Clock).Handle(
                new ResetPasswordCommand(new ResetPasswordRequest(account.Email, submittedOtp, "NewPassword1")),
                CancellationToken.None);
            busyError = await Record.ExceptionAsync(() => DrainOnTimeout(reset, gate));
        }
        finally
        {
            gate.Release.TrySetResult();
            await first.WaitAsync(TimeSpan.FromSeconds(10));
        }
        var rejected = Assert.IsType<AppException>(busyError);
        Assert.Equal(400, rejected.StatusCode);
        Assert.Equal("invalid_password_reset_token", rejected.Code);
        await using (var verify = database.Context())
        {
            Assert.Equal(oldHash, (await verify.Users.SingleAsync()).PasswordHash);
            var tokens = await verify.EmailVerificationTokens.ToListAsync();
            Assert.Equal(2, tokens.Count);
            Assert.NotNull(tokens.Single(x => x.Id == oldId).RevokedAt);
            Assert.All(tokens, token => Assert.Equal(0, token.FailedAttempts));
            Assert.All(tokens, token => Assert.Null(token.UsedAt));
            Assert.Null((await verify.UserSessions.SingleAsync()).RevokedAt);
            Assert.Null((await verify.RefreshTokens.SingleAsync()).RevokedAt);
            Assert.Empty(await verify.LoginChallenges.ToListAsync());
            Assert.Single(await verify.EmailOutboxMessages.ToListAsync());
        }

        var command = new ResetPasswordCommand(new ResetPasswordRequest(
            account.Email, IdentityTestData.DefaultEmailOtp, "NewPassword1"));
        await using (var success = database.Context())
            await ResetHandler(success, database.Clock).Handle(command, CancellationToken.None);
        await using (var replay = database.Context())
        {
            var error = await Record.ExceptionAsync(() =>
                ResetHandler(replay, database.Clock).Handle(command, CancellationToken.None));
            Assert.Equal("invalid_password_reset_token", Assert.IsType<AppException>(error).Code);
        }
        await using var final = database.Context();
        Assert.True(passwords.Verify("NewPassword1", (await final.Users.SingleAsync()).PasswordHash));
        Assert.NotNull((await final.UserSessions.SingleAsync()).RevokedAt);
        Assert.NotNull((await final.RefreshTokens.SingleAsync()).RevokedAt);
        Assert.Single(await final.EmailVerificationTokens.Where(x => x.UsedAt != null).ToListAsync());
        Assert.Single(await final.EmailOutboxMessages.Where(x =>
            x.DeduplicationKey.StartsWith("email:password-reset-success:")).ToListAsync());
        Assert.Equal(2, await final.EmailOutboxMessages.CountAsync());
    }

    [Fact]
    public async Task Concurrent_SystemAdmin_password_reset_consumes_once_and_preserves_PIN_on_SQL02()
    {
        var database = await OwnAsync(OwnedDatabase.CreateAsync());
        var userId = await database.SeedUserAsync(true);
        var passwords = new PasswordService();
        var pinHash = new SecurityPinService().Hash(IdentityTestData.DefaultPin);
        string email;
        await using (var seed = database.Context())
        {
            var user = await seed.Users.SingleAsync(x => x.Id == userId);
            user.SystemRole = SystemRole.SystemAdmin;
            user.PasswordHash = passwords.Hash("SyntheticOldPassword2026");
            user.SecurityPinHash = pinHash;
            await seed.SaveChangesAsync();
            email = user.Email;
            await Support(seed, database.Clock, new TestEmailContentProtector())
                .IssuePasswordResetOtpAsync(user, "192.0.2.90", default);
            await new AuthenticationSupport(seed, new TargetedTokenService(), database.Clock)
                .IssueAsync(user, "synthetic SQL session", null, null, default);
        }
        await using var first = database.Context();
        await using var second = database.Context();
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var command = new ResetPasswordCommand(new ResetPasswordRequest(
            email, IdentityTestData.DefaultEmailOtp, "DifferentSyntheticPassword2026"));
        async Task<Exception?> Attempt(SpendWiseDbContext db)
        {
            await start.Task;
            return await Record.ExceptionAsync(() => ResetHandler(db, database.Clock).Handle(command, default));
        }
        var attempts = new[] { Attempt(first), Attempt(second) };
        start.SetResult();
        var results = await Task.WhenAll(attempts);
        Assert.Single(results, x => x is null);
        Assert.Equal("invalid_password_reset_token", Assert.IsType<AppException>(Assert.Single(results, x => x is not null)).Code);
        await using var verify = database.Context();
        var saved = await verify.Users.SingleAsync(x => x.Id == userId);
        Assert.True(passwords.Verify("DifferentSyntheticPassword2026", saved.PasswordHash));
        Assert.Equal(pinHash, saved.SecurityPinHash);
        Assert.Equal(SystemRole.SystemAdmin, saved.SystemRole);
        Assert.NotNull((await verify.EmailVerificationTokens.SingleAsync()).UsedAt);
        Assert.All(await verify.UserSessions.ToListAsync(), x => Assert.NotNull(x.RevokedAt));
        Assert.All(await verify.RefreshTokens.ToListAsync(), x => Assert.NotNull(x.RevokedAt));
        Assert.Single(await verify.EmailOutboxMessages.Where(x => x.DeduplicationKey.StartsWith("email:password-reset-success:")).ToListAsync());
    }

    [Fact]
    public async Task Concurrent_Admin_PIN_reset_consumes_once_preserves_password_and_revokes_credentials_on_SQL02()
    {
        var database=await OwnAsync(OwnedDatabase.CreateAsync());var userId=await database.SeedUserAsync(true);
        string raw;string passwordHash;
        await using(var seed=database.Context())
        {
            var user=await seed.Users.SingleAsync(x=>x.Id==userId);user.SystemRole=SystemRole.SystemAdmin;
            passwordHash=new PasswordService().Hash("SyntheticUnchangedPassword2026");user.PasswordHash=passwordHash;
            user.SecurityPinHash=new SecurityPinService().Hash("123456");await seed.SaveChangesAsync();
            raw=(await new SecurityPinResetSupport(seed,new LoginChallengeTokenService(),database.Clock)
                .CreateAsync(user,SecurityPinResetPurpose.SelfService,null,default)).RawToken;
            await new AuthenticationSupport(seed,new TargetedTokenService(),database.Clock).IssueAsync(user,"synthetic PIN session",null,null,default);
        }
        await using var first=database.Context();await using var second=database.Context();
        var start=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<Exception?> Attempt(SpendWiseDbContext db)
        {
            await start.Task;
            var handler=new SpendWise.Application.Features.Authentication.Commands.ResetSecurityPin.ResetSecurityPinCommandHandler(
                db,new SecurityPinResetSupport(db,new LoginChallengeTokenService(),database.Clock),new SecurityPinService(),
                new AuthenticationSupport(db,new TargetedTokenService(),database.Clock),database.Clock);
            return await Record.ExceptionAsync(()=>handler.Handle(new SpendWise.Application.Features.Authentication.Commands.ResetSecurityPin.ResetSecurityPinCommand(
                new ResetSecurityPinRequest(raw,"654321","654321")),default));
        }
        var attempts=new[]{Attempt(first),Attempt(second)};start.SetResult();var results=await Task.WhenAll(attempts);
        Assert.Single(results,x=>x is null);Assert.Equal("pin_reset_token_invalid",Assert.IsType<AppException>(Assert.Single(results,x=>x is not null)).Code);
        await using var verify=database.Context();var saved=await verify.Users.SingleAsync();
        Assert.Equal(passwordHash,saved.PasswordHash);Assert.Equal(SystemRole.SystemAdmin,saved.SystemRole);
        Assert.True(new SecurityPinService().Verify("654321",saved.SecurityPinHash!));
        Assert.NotNull((await verify.SecurityPinResetTokens.SingleAsync()).UsedAt);
        Assert.All(await verify.UserSessions.ToListAsync(),x=>Assert.NotNull(x.RevokedAt));
        Assert.All(await verify.RefreshTokens.ToListAsync(),x=>Assert.NotNull(x.RevokedAt));
    }

    private static ResetPasswordCommandHandler ResetHandler(SpendWiseDbContext db, IClock clock) =>
        new(db, new TestEmailVerificationOtpService(), new TestEmailVerificationPolicy(),
            new PasswordService(), new AuthenticationSupport(db, new TargetedTokenService(), clock),
            Support(db, clock, new TestEmailContentProtector()), clock);

    [Fact]
    public async Task Stale_pending_user_snapshot_cannot_reissue_registration_OTP_after_verification_commits()
    {
        var database = await OwnAsync(OwnedDatabase.CreateAsync());
        var userId = await database.SeedUserAsync(false);
        await using var staleContext = database.Context();
        var staleUser = await staleContext.Users.SingleAsync(x => x.Id == userId);
        await using (var issuing = database.Context())
            await Issue(Support(issuing, database.Clock, new TestEmailContentProtector()),
                await issuing.Users.SingleAsync(x => x.Id == userId), false, "192.0.2.90");
        await using (var verifying = database.Context())
            await RegistrationVerifier(verifying, database.Clock).Handle(
                new VerifyRegistrationEmailCommand(new VerifyRegistrationEmailRequest(
                    staleUser.Email, IdentityTestData.DefaultEmailOtp, "SQL test"),
                    "192.0.2.91", "test-only"), CancellationToken.None);

        Assert.Null(staleUser.EmailVerifiedAt); // The request really holds a stale pre-verification snapshot.
        var accepted = await Support(staleContext, database.Clock, new TestEmailContentProtector())
            .IssueRegistrationOtpAsync(staleUser, "192.0.2.92", CancellationToken.None);
        Assert.Equal("EMAIL_VERIFICATION_REQUEST_ACCEPTED", accepted.Status);
        await using var final = database.Context();
        var token = Assert.Single(await final.EmailVerificationTokens.ToListAsync());
        Assert.NotNull(token.UsedAt);
        Assert.Null(token.RevokedAt);
        Assert.Equal(0, token.FailedAttempts);
        Assert.NotNull((await final.Users.SingleAsync()).EmailVerifiedAt);
        Assert.Equal(2, await final.EmailOutboxMessages.CountAsync());
        Assert.Single(await final.LoginChallenges.ToListAsync());
        Assert.Empty(await final.UserSessions.ToListAsync());
    }

    private sealed class FailingProtector(CancellationTokenSource cancellation, bool cancel)
        : IEmailContentProtector
    {
        public string Protect(string value)
        {
            if (!cancel)
                throw new InvalidOperationException("Controlled SQL test-only queue failure.");
            cancellation.Cancel();
            throw new OperationCanceledException(cancellation.Token);
        }

        public string Unprotect(string value) => throw new NotSupportedException();
    }

    private static EmailVerificationSupport Support(
        SpendWiseDbContext db, IClock clock, IEmailContentProtector protector,
        IEmailVerificationPolicy? policy = null) => new(
            db, new TestEmailVerificationOtpService(), policy ?? new TestEmailVerificationPolicy(),
            new EmailTemplateRenderer(), new EmailDeliveryQueue(db, protector), clock);

    private static Task Issue(EmailVerificationSupport support, User user, bool reset, string ip) =>
        reset ? support.IssuePasswordResetOtpAsync(user, ip, CancellationToken.None)
            : support.IssueRegistrationOtpAsync(user, ip, CancellationToken.None);

    private sealed class GatedProtector : IEmailContentProtector
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int calls;

        public string Protect(string value)
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
                Entered.TrySetResult();
                Release.Task.WaitAsync(TimeSpan.FromSeconds(15)).GetAwaiter().GetResult();
            }
            return $"protected:{value}";
        }

        public string Unprotect(string value) => throw new NotSupportedException();
    }

    private sealed class FixedClock : IClock
    {
        public DateTime UtcNow => new(2026, 10, 11, 12, 0, 0, DateTimeKind.Utc);
    }

    private sealed class OwnedDatabase : IAsyncDisposable
    {
        private readonly string name = "SpendWise_OtpS04_Test_" + Guid.NewGuid().ToString("N");
        private readonly string connection;
        private bool ownsCatalog;
        public IClock Clock { get; } = new FixedClock();

        private OwnedDatabase()
        {
            connection = SqlTestFixtureLifecycle.CreateOwnedConnectionString(name,
                Environment.GetEnvironmentVariable(SqlTestFixtureLifecycle.InstanceEnvironmentVariable));
        }

        public static async Task<OwnedDatabase> CreateAsync()
        {
            var owned = new OwnedDatabase();
            owned.ValidateOwnership();
            await SqlTestFixtureLifecycle.CreateOwnedCatalogAsync(owned.connection);
            owned.ownsCatalog = true;
            return await SqlTestFixtureLifecycle.CreateAsync(async () =>
            {
                await using var db = owned.Context();
                await SqlTestFixtureLifecycle.MeasureAsync(owned.name, "create", () => db.Database.EnsureCreatedAsync());
                await SqlTestFixtureLifecycle.ConfigureOwnedCatalogAsync(owned.connection);
                return owned;
            }, owned.DisposeAsync);
        }

        public SpendWiseDbContext Context() => new(
            new DbContextOptionsBuilder<SpendWiseDbContext>().UseSqlServer(connection).Options, Clock);

        public async Task<bool> ObserveApplicationLockWaitAsync(int sessionId, Task pending)
        {
            ValidateOwnership();
            // Read-only DMV query scoped to this owned test database; no production/master mutation.
            await using var observer = new SqlConnection(connection);
            await observer.OpenAsync();
            await using var command = observer.CreateCommand();
            command.CommandTimeout = 2;
            command.CommandText = """
                SELECT COUNT(*) FROM sys.dm_tran_locks
                WHERE request_session_id = @sessionId
                  AND resource_database_id = DB_ID()
                  AND resource_type = 'APPLICATION'
                  AND request_status IN ('WAIT', 'LOW_PRIORITY_WAIT')
                """;
            command.Parameters.AddWithValue("@sessionId", sessionId);
            var deadline = System.Diagnostics.Stopwatch.StartNew();
            while (!pending.IsCompleted && deadline.Elapsed < TimeSpan.FromSeconds(4))
            {
                if (Convert.ToInt32(await command.ExecuteScalarAsync()) > 0)
                    return true;
                // Poll only for observable SQL lock state; no assertion depends on sleep duration.
                await Task.Delay(10);
            }
            return false;
        }

        public async Task<Guid> SeedUserAsync(bool verified)
        {
            await using var db = Context();
            var id = Guid.NewGuid().ToString("N");
            var user = new User
            {
                FullName = "Isolated SQL OTP test", Email = $"otp-{id}@test.local",
                NormalizedEmail = $"OTP-{id.ToUpperInvariant()}@TEST.LOCAL",
                SpendId = id, NormalizedSpendId = id.ToUpperInvariant(),
                Phone = IdentityTestData.NewPhone(), PasswordHash = "test-only-hash",
                EmailVerifiedAt = verified ? Clock.UtcNow : null
            };
            db.Users.Add(user);
            await SqlTestFixtureLifecycle.MeasureAsync(name, "seed-save", () => db.SaveChangesAsync());
            return user.Id;
        }

        private void ValidateOwnership()
        {
            SqlTestFixtureLifecycle.ValidateOwnedConnection(connection);
            var parsed = new SqlConnectionStringBuilder(connection);
            if (!Regex.IsMatch(name, @"\ASpendWise_OtpS04_Test_[a-f0-9]{32}\z")
                || parsed.InitialCatalog != name
                || !parsed.IntegratedSecurity || parsed.UserID.Length != 0 || parsed.Password.Length != 0)
                throw new InvalidOperationException("Refusing non-owned SQL test database access.");
        }

        public async ValueTask DisposeAsync()
        {
            ValidateOwnership();
            if (!ownsCatalog) throw new InvalidOperationException("Catalog was not created by this fixture.");
            await SqlTestFixtureLifecycle.VerifyCatalogOwnerAsync(connection);
            await using var db = Context();
            await SqlTestFixtureLifecycle.MeasureAsync(name, "cleanup", () => db.Database.EnsureDeletedAsync());
            ownsCatalog = false;
        }
    }
}
