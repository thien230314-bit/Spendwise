using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features;
using SpendWise.Application.Features.Authentication;
using SpendWise.Application.Features.Groups;
using SpendWise.Application.Features.Wallets;
using SpendWise.Application.Groups;
using SpendWise.Application.Notifications;
using SpendWise.Domain.Finance;
using SpendWise.Domain.Groups;
using SpendWise.Domain.Identity;
using SpendWise.Domain.Notifications;
using SpendWise.Infrastructure.Finance;
using SpendWise.Infrastructure.Identity;
using SpendWise.Infrastructure.Notifications;
using SpendWise.Infrastructure.Persistence;
using Xunit;
using Group = SpendWise.Domain.Groups.Group;

namespace SpendWise.Tests;

public sealed class GroupDeletionSqlServerConcurrencyTests(Xunit.Abstractions.ITestOutputHelper output) : SqlTestFixtureScope
{
    private const string Pin = "123456";

    [Fact]
    public async Task Dedicated_changed_owner_marker_is_refused_before_catalog_configuration()
    {
        var fixture = await OwnAsync(OwnedDatabase.CreateAsync());
        await using var db = fixture.Context();
        await db.Database.OpenConnectionAsync();
        await using var marker = db.Database.GetDbConnection().CreateCommand();
        marker.CommandText = "EXEC sys.sp_updateextendedproperty @name=N'SpendWise.TestFixture', @value=@owner";
        var owner = marker.CreateParameter(); owner.ParameterName = "@owner";
        owner.Value = "another-fixture"; marker.Parameters.Add(owner);
        try
        {
            await marker.ExecuteNonQueryAsync();
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                SqlTestFixtureLifecycle.ConfigureOwnedCatalogAsync(db.Database.GetConnectionString()!));
        }
        finally
        {
            owner.Value = new SqlConnectionStringBuilder(db.Database.GetConnectionString()).InitialCatalog;
            await marker.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task Dedicated_existing_catalog_is_rejected_without_adopting_or_changing_it()
    {
        var fixture = await OwnAsync(OwnedDatabase.CreateAsync());
        await using var db = fixture.Context();
        var before = await db.Users.CountAsync();
        var error = await Assert.ThrowsAsync<SqlException>(() =>
            SqlTestFixtureLifecycle.CreateOwnedCatalogAsync(db.Database.GetConnectionString()!));
        Assert.Equal(51000, error.Number);
        Assert.Equal(before, await db.Users.CountAsync());
        await SqlTestFixtureLifecycle.VerifyCatalogOwnerAsync(db.Database.GetConnectionString()!);
    }

    [Fact]
    public async Task Owned_connection_pool_reuses_physical_session_and_can_be_cleared()
    {
        var fixture = await OwnAsync(OwnedDatabase.CreateAsync());
        await fixture.CheckConnectionPoolingAsync(output);
    }

    [Fact]
    public async Task Owned_catalog_stays_open_between_independent_fixture_connections()
    {
        var fixture = await OwnAsync(OwnedDatabase.CreateAsync());
        for (var connection = 0; connection < 2; connection++)
        {
            await using var db = fixture.Context();
            await db.Database.OpenConnectionAsync();
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = "SELECT is_auto_close_on FROM sys.databases WHERE database_id=DB_ID()";
            Assert.False(Convert.ToBoolean(await command.ExecuteScalarAsync()));
            command.CommandText = "SELECT is_read_committed_snapshot_on FROM sys.databases WHERE database_id=DB_ID()";
            Assert.True(Convert.ToBoolean(await command.ExecuteScalarAsync()));
        }
    }

    [Fact]
    public async Task Standalone_stale_refund_snapshot_replays_same_key_without_changing_money_or_history()
    {
        var fixture = await OwnAsync(OwnedDatabase.CreateAsync(true)); await fixture.MakeReadyAsync();
        var refundId = await fixture.RefundIdAsync(fixture.First);
        await using var stale = fixture.Context();
        // Deliberately no parent request/navigation preload: child identity resolution
        // must not resurrect Pending when another context has completed this refund.
        var pending = await stale.GroupRefunds.SingleAsync(x => x.Id == refundId);
        Assert.Equal(GroupRefundStatus.Pending, pending.Status);
        await using (var winner = fixture.Context())
            await fixture.Refunder(winner).Handle(fixture.Refund(fixture.First, refundId, "child-replay"), default);
        Assert.Equal(GroupRefundStatus.Pending, pending.Status);
        var replay = await fixture.Refunder(stale).Handle(fixture.Refund(fixture.First, refundId, "child-replay"), default);
        Assert.Equal(GroupRefundStatus.Completed.ToString(), replay.Status);
        await using var verify = fixture.Context();
        Assert.Single(await verify.LedgerTransactions.Where(x => x.BusinessType == "GroupRefund").ToListAsync());
        Assert.Equal(200m, (await verify.SharedFunds.SingleAsync()).Balance);
        Assert.Equal(200m, (await verify.LedgerAccounts.SingleAsync(x => x.Type == LedgerAccountType.SharedFund)).Balance);
        Assert.Equal(500m, (await verify.Wallets.SingleAsync(x => x.OwnerUserId == fixture.First)).AvailableBalance);
        Assert.Equal(0m, (await verify.GroupFundShares.SingleAsync(x => x.UserId == fixture.First)).RemainingAmount);
        Assert.Single(await verify.Notifications.Where(x => x.Type == NotificationType.GroupRefundCompleted).ToListAsync());
    }

    [Fact]
    public async Task Standalone_stale_pending_vote_cannot_be_recorded_again_after_another_context_commits()
    {
        var fixture = await OwnAsync(OwnedDatabase.CreateAsync());
        await using var stale = fixture.Context();
        var pending = await stale.GroupDeletionVotes.SingleAsync(x => x.UserId == fixture.First);
        Assert.Equal(GroupDeletionVoteStatus.Pending, pending.Status);
        await using (var winner = fixture.Context())
            await fixture.Voter(winner).Handle(fixture.Vote(fixture.First), default);
        Assert.Equal(GroupDeletionVoteStatus.Pending, pending.Status);
        var error = await Record.ExceptionAsync(() => fixture.Voter(stale).Handle(fixture.Vote(fixture.First), default));
        Assert.Equal("deletion_vote_recorded", Assert.IsType<ConflictException>(error).Code);
        await using var verify = fixture.Context();
        Assert.Equal(GroupDeletionRequestStatus.Pending, (await verify.GroupDeletionRequests.SingleAsync()).Status);
        Assert.Equal(GroupDeletionVoteStatus.Approved,
            (await verify.GroupDeletionVotes.SingleAsync(x => x.UserId == fixture.First)).Status);
        Assert.Equal(GroupDeletionVoteStatus.Pending,
            (await verify.GroupDeletionVotes.SingleAsync(x => x.UserId == fixture.Second)).Status);
        Assert.Empty(await verify.GroupRefunds.ToListAsync());
        Assert.Equal(2, await verify.Notifications.CountAsync());
        Assert.Equal(2, await verify.OutboxMessages.CountAsync());
    }

    [Fact]
    public async Task Concurrent_finalization_and_cancel_cannot_reactivate_a_closed_group_or_duplicate_refunds()
    {
        var fixture = await OwnAsync(OwnedDatabase.CreateAsync(true));
        await fixture.MakeReadyAsync(); await fixture.RefundAllAsync();
        var gate = new FinalizeSaveGate();
        await using var finalizing = fixture.Context(gate);
        await using var contender = fixture.Context();
        await contender.Database.OpenConnectionAsync();
        var session = ((SqlConnection)contender.Database.GetDbConnection()).ServerProcessId;
        var first = fixture.Finalizer(finalizing).Handle(new(fixture.Owner, fixture.Group, fixture.Request, new(Pin)), default);
        Task<GroupDeletionRequestDto>? cancel = null;
        Exception? cancelError;
        try
        {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            cancel = new CancelGroupDeletionCommandHandler(contender, new GroupAccess(contender))
                .Handle(new(fixture.Owner, fixture.Group, fixture.Request), default);
            var observed = await Task.WhenAny(cancel, fixture.ObserveWaitAsync(session, cancel)).WaitAsync(TimeSpan.FromSeconds(10));
            if (observed != cancel) Assert.True(await (Task<bool>)observed);
            gate.Release.TrySetResult(); await first.WaitAsync(TimeSpan.FromSeconds(10));
            cancelError = await Record.ExceptionAsync(() => cancel.WaitAsync(TimeSpan.FromSeconds(10)));
        }
        finally
        {
            gate.Release.TrySetResult(); await Record.ExceptionAsync(() => first);
            if (cancel is not null) await Record.ExceptionAsync(() => cancel);
        }
        Assert.IsAssignableFrom<AppException>(cancelError);
        await fixture.AssertClosedAsync();
    }

    [Fact]
    public async Task Two_fresh_finalization_requests_leave_one_completed_lifecycle_and_preserve_financial_history()
    {
        var fixture = await OwnAsync(OwnedDatabase.CreateAsync(true));
        await fixture.MakeReadyAsync(); await fixture.RefundAllAsync();
        var firstGate = new FinalizeSaveGate(); var secondGate = new FinalizeSaveGate();
        await using var firstDb = fixture.Context(firstGate); await using var secondDb = fixture.Context(secondGate);
        await secondDb.Database.OpenConnectionAsync();
        var session = ((SqlConnection)secondDb.Database.GetDbConnection()).ServerProcessId;
        var first = fixture.Finalizer(firstDb).Handle(new(fixture.Owner, fixture.Group, fixture.Request, new(Pin)), default);
        Task<GroupDeletionRequestDto>? second = null;
        try
        {
            await firstGate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            second = fixture.Finalizer(secondDb).Handle(new(fixture.Owner, fixture.Group, fixture.Request, new(Pin)), default);
            var observed = await Task.WhenAny(secondGate.Entered.Task, second, fixture.ObserveWaitAsync(session, second))
                .WaitAsync(TimeSpan.FromSeconds(10));
            if (observed != secondGate.Entered.Task && observed != second) Assert.True(await (Task<bool>)observed);
            firstGate.Release.TrySetResult(); await first.WaitAsync(TimeSpan.FromSeconds(10));
            secondGate.Release.TrySetResult();
            var error = await Record.ExceptionAsync(() => second.WaitAsync(TimeSpan.FromSeconds(10)));
            // Existing contract may reject the second request once membership closes.
            if (error is not null) Assert.IsAssignableFrom<AppException>(error);
        }
        finally
        {
            firstGate.Release.TrySetResult(); secondGate.Release.TrySetResult();
            await Record.ExceptionAsync(() => first);
            if (second is not null) await Record.ExceptionAsync(() => second);
        }
        await fixture.AssertClosedAsync();
    }

    [Fact]
    public async Task Final_refund_and_finalization_overlap_commit_consistent_completed_state()
    {
        var fixture = await OwnAsync(OwnedDatabase.CreateAsync(true)); await fixture.MakeReadyAsync();
        foreach (var user in new[] { fixture.Owner, fixture.First })
        {
            var id = await fixture.RefundIdAsync(user);
            await using var db = fixture.Context();
            await fixture.Refunder(db).Handle(fixture.Refund(user, id, "before-final-" + user.ToString("N")), default);
        }
        var lastId = await fixture.RefundIdAsync(fixture.Second);
        var gate = new RefundNotificationGate();
        await using var refundDb = fixture.Context(); await using var finalizeDb = fixture.Context();
        await finalizeDb.Database.OpenConnectionAsync();
        var session = ((SqlConnection)finalizeDb.Database.GetDbConnection()).ServerProcessId;
        var refund = fixture.Refunder(refundDb, new GatedRefundNotifications(new NotificationService(refundDb), gate))
            .Handle(fixture.Refund(fixture.Second, lastId, "last-refund"), default);
        Task<GroupDeletionRequestDto>? finalize = null;
        Exception? finalizeError;
        try
        {
            // Actual ledger, refund/share/fund/wallet writes exist inside an uncommitted transaction.
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            finalize = fixture.Finalizer(finalizeDb).Handle(new(fixture.Owner, fixture.Group, fixture.Request, new(Pin)), default);
            var observed = await Task.WhenAny(finalize, fixture.ObserveWaitAsync(session, finalize)).WaitAsync(TimeSpan.FromSeconds(10));
            if (observed != finalize) Assert.True(await (Task<bool>)observed);
            gate.Release.TrySetResult(); await refund.WaitAsync(TimeSpan.FromSeconds(10));
            finalizeError = await Record.ExceptionAsync(() => finalize.WaitAsync(TimeSpan.FromSeconds(10)));
        }
        finally
        {
            gate.Release.TrySetResult(); await Record.ExceptionAsync(() => refund);
            if (finalize is not null) await Record.ExceptionAsync(() => finalize);
        }
        if (finalizeError is not null)
        {
            Assert.IsAssignableFrom<AppException>(finalizeError);
            await using var retry = fixture.Context();
            await fixture.Finalizer(retry).Handle(new(fixture.Owner, fixture.Group, fixture.Request, new(Pin)), default);
        }
        await fixture.AssertClosedAsync();
    }

    [Fact]
    public async Task Stale_tracked_ready_refund_request_cannot_refund_after_cancellation_commits()
    {
        var fixture = await OwnAsync(OwnedDatabase.CreateAsync(true)); await fixture.MakeReadyAsync();
        var refundId = await fixture.RefundIdAsync(fixture.First);
        await using var stale = fixture.Context();
        var snapshot = await stale.GroupDeletionRequests.SingleAsync();
        Assert.Equal(GroupDeletionRequestStatus.Ready, snapshot.Status);
        await using (var cancelling = fixture.Context())
            await new CancelGroupDeletionCommandHandler(cancelling, new GroupAccess(cancelling))
                .Handle(new(fixture.Owner, fixture.Group, fixture.Request), default);
        var error = await Record.ExceptionAsync(() => fixture.Refunder(stale)
            .Handle(fixture.Refund(fixture.First, refundId, "stale-refund"), default));
        Assert.Equal("refund_plan_not_ready", Assert.IsType<ConflictException>(error).Code);
        await using var verify = fixture.Context();
        Assert.Equal(0, await verify.LedgerTransactions.CountAsync(x => x.BusinessType == "GroupRefund"));
        Assert.Equal(300m, (await verify.SharedFunds.SingleAsync()).Balance);
        Assert.All(await verify.GroupRefunds.ToListAsync(), x => Assert.Equal(GroupRefundStatus.Pending, x.Status));
    }

    [Fact]
    public async Task Funded_cancel_admitted_before_refund_prevents_financial_posting()
    {
        var fixture = await OwnAsync(OwnedDatabase.CreateAsync(true));
        await fixture.MakeReadyAsync();
        var refundId = await fixture.RefundIdAsync(fixture.First);
        var cancelGate = new CancelSaveGate();
        var refundGate = new RefundWalletGate();
        await using var cancelling = fixture.Context(cancelGate);
        await using var refunding = fixture.Context(refundGate);
        await refunding.Database.OpenConnectionAsync();
        var session = ((SqlConnection)refunding.Database.GetDbConnection()).ServerProcessId;
        var cancel = new CancelGroupDeletionCommandHandler(cancelling, new GroupAccess(cancelling))
            .Handle(new(fixture.Owner, fixture.Group, fixture.Request), default);
        Task<GroupRefundDto>? refund = null;
        Exception? refundError = null;
        try
        {
            await cancelGate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            refund = fixture.Refunder(refunding).Handle(fixture.Refund(fixture.First, refundId, "cancel-race"), default);
            var observed = await Task.WhenAny(refundGate.Entered.Task, fixture.ObserveWaitAsync(session, refund))
                .WaitAsync(TimeSpan.FromSeconds(10));
            if (observed == refundGate.Entered.Task)
            {
                // Pre-repair refund reads Ready while cancellation holds only its stale snapshot.
                refundGate.Release.TrySetResult();
                refundError = await Record.ExceptionAsync(() => refund.WaitAsync(TimeSpan.FromSeconds(10)));
            }
            else Assert.True(await (Task<bool>)observed);
            cancelGate.Release.TrySetResult();
            await cancel.WaitAsync(TimeSpan.FromSeconds(10));
            refundGate.Release.TrySetResult();
            refundError ??= await Record.ExceptionAsync(() => refund.WaitAsync(TimeSpan.FromSeconds(10)));
        }
        finally
        {
            cancelGate.Release.TrySetResult(); refundGate.Release.TrySetResult();
            await Record.ExceptionAsync(() => cancel);
            if (refund is not null) await Record.ExceptionAsync(() => refund);
        }
        Assert.Equal("refund_plan_not_ready", Assert.IsType<ConflictException>(refundError).Code);
        await using var verify = fixture.Context();
        Assert.Equal(GroupDeletionRequestStatus.Cancelled, (await verify.GroupDeletionRequests.SingleAsync()).Status);
        Assert.Equal(GroupStatus.Active, (await verify.Groups.SingleAsync()).Status);
        Assert.Equal(300m, (await verify.SharedFunds.SingleAsync()).Balance);
        Assert.Equal(300m, (await verify.LedgerAccounts.SingleAsync(x => x.Type == LedgerAccountType.SharedFund)).Balance);
        Assert.All(await verify.GroupFundShares.ToListAsync(), x => Assert.Equal(100m, x.RemainingAmount));
        Assert.All(await verify.GroupRefunds.ToListAsync(), x => Assert.Equal(GroupRefundStatus.Pending, x.Status));
        Assert.Equal(0, await verify.LedgerTransactions.CountAsync(x => x.BusinessType == "GroupRefund"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Funded_overlapping_same_refund_posts_once_and_retry_replays(bool sameKey)
    {
        var fixture = await OwnAsync(OwnedDatabase.CreateAsync(true));
        await fixture.MakeReadyAsync();
        var refundId = await fixture.RefundIdAsync(fixture.First);
        var firstGate = new RefundWalletGate(); var secondGate = new RefundWalletGate();
        await using var firstDb = fixture.Context(firstGate);
        await using var secondDb = fixture.Context(secondGate);
        await secondDb.Database.OpenConnectionAsync();
        var session = ((SqlConnection)secondDb.Database.GetDbConnection()).ServerProcessId;
        using var cancellation = new CancellationTokenSource();
        var first = fixture.Refunder(firstDb).Handle(fixture.Refund(fixture.First, refundId, "one-refund"), cancellation.Token);
        Task<GroupRefundDto>? second = null;
        Task<bool>? observation = null;
        Exception? secondError = null;
        Exception? scenarioError = null;
        try
        {
            await SqlTestFixtureLifecycle.WaitForGateAsync(firstGate.Entered.Task, first, TimeSpan.FromSeconds(10));
            Assert.True(firstGate.WalletReadInTransaction);
            Assert.False(first.IsCompleted);
            output.WriteLine($"Refund gate matched: shape=SELECT/FROM Wallets;transaction={firstGate.WalletReadInTransaction};readerCommands={firstGate.ReaderCommands};firstPending={!first.IsCompleted}");
            // Owner and member can select the same member refund; distinct actors avoid incidental PIN row serialization.
            second = fixture.Refunder(secondDb).Handle(fixture.Refund(fixture.Owner, refundId,
                sameKey ? "one-refund" : "another-key"), cancellation.Token);
            observation = fixture.ObserveWaitAsync(session, second, cancellation.Token);
            var observed = await Task.WhenAny(secondGate.Entered.Task, second, observation)
                .WaitAsync(TimeSpan.FromSeconds(10));
            if (observed == second)
            {
                await second;
                throw new InvalidOperationException("Second refund completed before overlap was observed.");
            }
            if (observed == observation) Assert.True(await observation);
            firstGate.Release.TrySetResult(); await first.WaitAsync(TimeSpan.FromSeconds(10));
            secondGate.Release.TrySetResult();
            secondError = await Record.ExceptionAsync(() => second.WaitAsync(TimeSpan.FromSeconds(10)));
        }
        catch (Exception error)
        {
            scenarioError = error;
            // Metadata only: no SQL text, parameters, identifiers, or exception values.
            output.WriteLine($"Refund stage: first={first.Status};second={second?.Status};firstGate={firstGate.Entered.Task.Status};secondGate={secondGate.Entered.Task.Status};firstWalletInTransaction={firstGate.WalletReadInTransaction};firstReads={firstGate.ReaderCommands};secondReads={secondGate.ReaderCommands}");
            throw;
        }
        finally
        {
            firstGate.Release.TrySetResult(); secondGate.Release.TrySetResult();
            cancellation.Cancel();
            var cleanupErrors = new List<Exception>();
            foreach (var operation in new Task[] { first, second ?? Task.CompletedTask, observation ?? Task.CompletedTask })
            {
                var error = await Record.ExceptionAsync(() => operation.WaitAsync(TimeSpan.FromSeconds(10)));
                if (error is not null && !ReferenceEquals(error, scenarioError) && !ReferenceEquals(error, secondError)
                    && !(cancellation.IsCancellationRequested && error is OperationCanceledException))
                    cleanupErrors.Add(error);
            }
            if (cleanupErrors.Count > 0)
            {
                if (scenarioError is not null) cleanupErrors.Insert(0, scenarioError);
                throw new AggregateException("Refund scenario and task-drain failures.", cleanupErrors);
            }
        }
        if (secondError is not null) Assert.IsAssignableFrom<AppException>(secondError);
        await using (var retry = fixture.Context())
            await fixture.Refunder(retry).Handle(fixture.Refund(fixture.First, refundId, "one-refund"), default);
        await using var verify = fixture.Context();
        Assert.Single(await verify.LedgerTransactions.Where(x => x.BusinessType == "GroupRefund").ToListAsync());
        Assert.Equal(200m, (await verify.SharedFunds.SingleAsync()).Balance);
        Assert.Equal(200m, (await verify.LedgerAccounts.SingleAsync(x => x.Type == LedgerAccountType.SharedFund)).Balance);
        Assert.Equal(500m, (await verify.Wallets.SingleAsync(x => x.OwnerUserId == fixture.First)).AvailableBalance);
        Assert.Equal(0m, (await verify.GroupFundShares.SingleAsync(x => x.UserId == fixture.First)).RemainingAmount);
        Assert.Single(await verify.Notifications.Where(x => x.Type == NotificationType.GroupRefundCompleted).ToListAsync());
        var completedRefund = await verify.GroupRefunds.SingleAsync(x => x.Id == refundId);
        Assert.Equal(GroupRefundStatus.Completed, completedRefund.Status);
        Assert.Equal("one-refund", completedRefund.IdempotencyKey);
        Assert.Equal((await verify.LedgerTransactions.SingleAsync(x => x.BusinessType == "GroupRefund")).Id,
            completedRefund.LedgerTransactionId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Funded_refund_notification_failure_or_cancellation_rolls_back_ledger_and_can_retry(bool cancel)
    {
        var fixture = await OwnAsync(OwnedDatabase.CreateAsync(true));
        await fixture.MakeReadyAsync(); var refundId = await fixture.RefundIdAsync(fixture.First);
        using var cancellation = new CancellationTokenSource();
        await using (var failing = fixture.Context())
        {
            var error = await Record.ExceptionAsync(() => fixture.Refunder(failing,
                new FailingRefundNotifications(new NotificationService(failing), cancellation, cancel))
                .Handle(fixture.Refund(fixture.First, refundId, "rollback-refund"), cancellation.Token));
            if (cancel) Assert.IsAssignableFrom<OperationCanceledException>(error);
            else Assert.IsType<InvalidOperationException>(error);
        }
        await using (var verify = fixture.Context())
        {
            Assert.Equal(300m, (await verify.SharedFunds.SingleAsync()).Balance);
            Assert.Equal(300m, (await verify.LedgerAccounts.SingleAsync(x => x.Type == LedgerAccountType.SharedFund)).Balance);
            Assert.Equal(400m, (await verify.Wallets.SingleAsync(x => x.OwnerUserId == fixture.First)).AvailableBalance);
            Assert.Equal(100m, (await verify.GroupFundShares.SingleAsync(x => x.UserId == fixture.First)).RemainingAmount);
            Assert.Equal(GroupRefundStatus.Pending, (await verify.GroupRefunds.SingleAsync(x => x.Id == refundId)).Status);
            Assert.Equal(0, await verify.LedgerTransactions.CountAsync(x => x.BusinessType == "GroupRefund"));
            Assert.Equal(0, await verify.Notifications.CountAsync(x => x.Type == NotificationType.GroupRefundCompleted));
            Assert.Equal(5, await verify.OutboxMessages.CountAsync());
        }
        await using var retry = fixture.Context();
        await fixture.Refunder(retry).Handle(fixture.Refund(fixture.First, refundId, "rollback-refund"), default);
    }

    [Fact]
    public async Task Funded_all_refunds_required_before_explicit_finalization_and_ledger_remains_balanced()
    {
        var fixture = await OwnAsync(OwnedDatabase.CreateAsync(true)); await fixture.MakeReadyAsync();
        await using (var early = fixture.Context())
            Assert.Equal("refunds_incomplete", (await Assert.ThrowsAsync<ConflictException>(() =>
                fixture.Finalizer(early).Handle(new(fixture.Owner, fixture.Group, fixture.Request, new(Pin)), default))).Code);
        foreach (var user in new[] { fixture.Owner, fixture.First, fixture.Second })
        {
            var refundId = await fixture.RefundIdAsync(user);
            await using var db = fixture.Context();
            await fixture.Refunder(db).Handle(fixture.Refund(user, refundId, "refund-" + user.ToString("N")), default);
        }
        await using (var before = fixture.Context())
            Assert.Equal(GroupStatus.DeletionPending, (await before.Groups.SingleAsync()).Status);
        await using (var db = fixture.Context())
            await fixture.Finalizer(db).Handle(new(fixture.Owner, fixture.Group, fixture.Request, new(Pin)), default);
        await using var verify = fixture.Context();
        Assert.Equal(GroupStatus.Closed, (await verify.Groups.SingleAsync()).Status);
        Assert.Equal(GroupDeletionRequestStatus.Completed, (await verify.GroupDeletionRequests.SingleAsync()).Status);
        Assert.Equal(0m, (await verify.SharedFunds.SingleAsync()).Balance);
        Assert.Equal(0m, (await verify.LedgerAccounts.SingleAsync(x => x.Type == LedgerAccountType.SharedFund)).Balance);
        Assert.All(await verify.GroupFundShares.ToListAsync(), x => Assert.Equal(0m, x.RemainingAmount));
        Assert.All(await verify.Wallets.ToListAsync(), x => Assert.Equal(500m, x.AvailableBalance));
        Assert.Equal(3, await verify.LedgerTransactions.CountAsync(x => x.BusinessType == "GroupRefund"));
        Assert.Equal(3, await verify.GroupContributions.CountAsync());
        var ledger = await verify.LedgerTransactions.Include(x => x.Entries).ToListAsync();
        Assert.Equal(6, ledger.Count);
        Assert.All(ledger, x => Assert.Equal(x.Entries.Where(e => e.Direction == LedgerEntryDirection.Debit).Sum(e => e.Amount),
            x.Entries.Where(e => e.Direction == LedgerEntryDirection.Credit).Sum(e => e.Amount)));
    }

    [Fact]
    public async Task Last_approval_and_owner_cancel_overlap_cannot_leave_ready_request_with_active_group()
    {
        var fixture = await OwnAsync(OwnedDatabase.CreateAsync());
        await using (var seed = fixture.Context())
            await fixture.Voter(seed).Handle(fixture.Vote(fixture.First), default);
        var gate = new VoteSaveGate();
        await using var voting = fixture.Context(gate);
        await using var cancelling = fixture.Context();
        await cancelling.Database.OpenConnectionAsync();
        var session = ((SqlConnection)cancelling.Database.GetDbConnection()).ServerProcessId;
        var approval = fixture.Voter(voting).Handle(fixture.Vote(fixture.Second), default);
        Task<GroupDeletionRequestDto>? cancel = null;
        Exception? cancelError = null;
        try
        {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            cancel = new CancelGroupDeletionCommandHandler(cancelling, new GroupAccess(cancelling))
                .Handle(new(fixture.Owner, fixture.Group, fixture.Request), default);
            var observed = await Task.WhenAny(cancel, fixture.ObserveWaitAsync(session, cancel))
                .WaitAsync(TimeSpan.FromSeconds(10));
            if (observed != cancel) Assert.True(await (Task<bool>)observed);
            gate.Release.TrySetResult();
            await approval.WaitAsync(TimeSpan.FromSeconds(10));
            cancelError = await Record.ExceptionAsync(() => cancel.WaitAsync(TimeSpan.FromSeconds(10)));
        }
        finally
        {
            gate.Release.TrySetResult();
            await Record.ExceptionAsync(() => approval);
            if (cancel is not null) await Record.ExceptionAsync(() => cancel);
        }
        await using var verify = fixture.Context();
        // Approval owned admission first. Once Ready, zero-amount completed refunds
        // make cancellation unavailable under the existing product rule.
        Assert.Equal("deletion_cancel_unavailable", Assert.IsType<ConflictException>(cancelError).Code);
        Assert.Equal(GroupDeletionRequestStatus.Ready, (await verify.GroupDeletionRequests.SingleAsync()).Status);
        Assert.Equal(GroupStatus.DeletionPending, (await verify.Groups.SingleAsync()).Status);
        Assert.Equal(3, await verify.GroupRefunds.CountAsync());
        Assert.Empty(await verify.LedgerTransactions.ToListAsync());
    }

    [Fact]
    public async Task Two_last_approvals_from_fresh_overlapping_requests_make_deletion_ready_once()
    {
        var fixture = await OwnAsync(OwnedDatabase.CreateAsync());
        var firstGate = new VoteSaveGate();
        var secondGate = new VoteSaveGate();
        await using var firstDb = fixture.Context(firstGate);
        await using var secondDb = fixture.Context(secondGate);
        await secondDb.Database.OpenConnectionAsync();
        var session = ((SqlConnection)secondDb.Database.GetDbConnection()).ServerProcessId;
        var first = fixture.Voter(firstDb).Handle(fixture.Vote(fixture.First), default);
        Task<GroupDeletionRequestDto>? second = null;
        try
        {
            await firstGate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            second = fixture.Voter(secondDb).Handle(fixture.Vote(fixture.Second), default);
            var observed = await Task.WhenAny(secondGate.Entered.Task,
                fixture.ObserveWaitAsync(session, second)).WaitAsync(TimeSpan.FromSeconds(10));
            if (observed != secondGate.Entered.Task)
                Assert.True(await (Task<bool>)observed, "Second approval neither reached save nor waited on a SQL lock.");
            firstGate.Release.TrySetResult();
            await first.WaitAsync(TimeSpan.FromSeconds(10));
            secondGate.Release.TrySetResult();
            await second.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            firstGate.Release.TrySetResult();
            secondGate.Release.TrySetResult();
            await Record.ExceptionAsync(() => first);
            if (second is not null) await Record.ExceptionAsync(() => second);
        }
        await using var verify = fixture.Context();
        var request = await verify.GroupDeletionRequests.Include(x => x.Votes).Include(x => x.Refunds).SingleAsync();
        Assert.All(request.Votes, x => Assert.Equal(GroupDeletionVoteStatus.Approved, x.Status));
        Assert.Equal(GroupDeletionRequestStatus.Ready, request.Status);
        Assert.NotNull(request.ReadyAt);
        Assert.Equal(3, request.Refunds.Count);
        Assert.All(request.Refunds, x => Assert.Equal(GroupRefundStatus.Completed, x.Status));
        Assert.Equal(3, await verify.Notifications.CountAsync(x => x.Type == NotificationType.GroupDeletionReady));
        Assert.Equal(GroupStatus.DeletionPending, (await verify.Groups.SingleAsync()).Status);
        Assert.Empty(await verify.LedgerTransactions.ToListAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Last_approval_notification_failure_or_cancellation_rolls_back_vote_ready_and_refund_plan(bool cancel)
    {
        var fixture = await OwnAsync(OwnedDatabase.CreateAsync());
        await using (var first = fixture.Context())
            await fixture.Voter(first).Handle(fixture.Vote(fixture.First), default);
        using var cancellation = new CancellationTokenSource();
        await using (var failing = fixture.Context())
        {
            var notifications = new FailingNotifications(new NotificationService(failing), cancellation, cancel);
            var error = await Record.ExceptionAsync(() => fixture.Voter(failing, notifications)
                .Handle(fixture.Vote(fixture.Second), cancellation.Token));
            if (cancel) Assert.IsAssignableFrom<OperationCanceledException>(error);
            else Assert.IsType<InvalidOperationException>(error);
        }
        await using (var verify = fixture.Context())
        {
            var request = await verify.GroupDeletionRequests.SingleAsync();
            Assert.Equal(GroupDeletionRequestStatus.Pending, request.Status);
            Assert.Null(request.ReadyAt);
            Assert.Equal(GroupDeletionVoteStatus.Pending,
                (await verify.GroupDeletionVotes.SingleAsync(x => x.UserId == fixture.Second)).Status);
            Assert.Empty(await verify.GroupRefunds.ToListAsync());
            Assert.Equal(0, await verify.Notifications.CountAsync(x => x.Type == NotificationType.GroupDeletionReady));
            Assert.Equal(2, await verify.OutboxMessages.CountAsync());
            Assert.Empty(await verify.LedgerTransactions.ToListAsync());
        }
        await using (var retry = fixture.Context())
            await fixture.Voter(retry).Handle(fixture.Vote(fixture.Second), default);
        await using var final = fixture.Context();
        Assert.Equal(GroupDeletionRequestStatus.Ready, (await final.GroupDeletionRequests.SingleAsync()).Status);
        Assert.Equal(3, await final.GroupRefunds.CountAsync());
    }

    [Fact]
    public async Task Outsider_cannot_vote_or_cancel_and_leaves_deletion_state_unchanged()
    {
        var fixture = await OwnAsync(OwnedDatabase.CreateAsync());
        Guid outsider;
        await using (var seed = fixture.Context())
        {
            var user = NewUser("Outsider");
            outsider = user.Id;
            seed.Users.Add(user);
            await seed.SaveChangesAsync();
        }
        await using (var db = fixture.Context())
            Assert.Equal("deletion_vote_forbidden", (await Assert.ThrowsAsync<ForbiddenException>(() =>
                fixture.Voter(db).Handle(fixture.Vote(outsider), default))).Code);
        await using (var db = fixture.Context())
            Assert.Equal("group_membership_required", (await Assert.ThrowsAsync<ForbiddenException>(() =>
                new CancelGroupDeletionCommandHandler(db, new GroupAccess(db)).Handle(
                    new(outsider, fixture.Group, fixture.Request), default))).Code);
        await using var verify = fixture.Context();
        Assert.Equal(GroupDeletionRequestStatus.Pending, (await verify.GroupDeletionRequests.SingleAsync()).Status);
        Assert.Equal(2, await verify.GroupDeletionVotes.CountAsync(x => x.Status == GroupDeletionVoteStatus.Pending));
        Assert.Empty(await verify.GroupRefunds.ToListAsync());
        Assert.Empty(await verify.LedgerTransactions.ToListAsync());
    }

    private sealed class VoteSaveGate : SaveChangesInterceptor
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int calls;
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<GroupDeletionVote>().Any(x => x.State == EntityState.Modified)
                && Interlocked.Increment(ref calls) == 1)
            {
                // All vote snapshots have materialized and SQL readers are disposed.
                Entered.TrySetResult();
                await Release.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
            }
            return result;
        }
    }

    private sealed class CancelSaveGate : SaveChangesInterceptor
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<GroupDeletionRequest>()
                .Any(x => x.State == EntityState.Modified && x.Entity.Status == GroupDeletionRequestStatus.Cancelled))
            {
                Entered.TrySetResult(); await Release.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
            }
            return result;
        }
    }
    private sealed class FinalizeSaveGate : SaveChangesInterceptor
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<GroupDeletionRequest>()
                .Any(x => x.State == EntityState.Modified && x.Entity.Status == GroupDeletionRequestStatus.Completed))
            {
                Entered.TrySetResult(); await Release.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
            }
            return result;
        }
    }
    private sealed class RefundNotificationGate
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
    private sealed class GatedRefundNotifications(NotificationService real, RefundNotificationGate gate) : INotificationService
    {
        public async Task<NotificationDto> CreateAsync(Guid userId, string type, string title, string message,
            object? data, CancellationToken token)
        {
            gate.Entered.TrySetResult(); await gate.Release.Task.WaitAsync(TimeSpan.FromSeconds(15), token);
            return await real.CreateAsync(userId, type, title, message, data, token);
        }
        public Task<IReadOnlyList<NotificationDto>> CreateManyAsync(
            IReadOnlyCollection<CreateNotificationRequest> requests, CancellationToken token) => real.CreateManyAsync(requests, token);
    }
    private sealed class RefundWalletGate : DbCommandInterceptor
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int calls;
        public int ReaderCommands { get; private set; }
        public bool WalletReadInTransaction { get; private set; }
        public override async ValueTask<InterceptionResult<System.Data.Common.DbDataReader>> ReaderExecutingAsync(
            System.Data.Common.DbCommand command, CommandEventData eventData,
            InterceptionResult<System.Data.Common.DbDataReader> result, CancellationToken cancellationToken = default)
        {
            ReaderCommands++;
            if (command.CommandText.Contains("FROM [Wallets]", StringComparison.Ordinal)
                && Interlocked.Increment(ref calls) == 1)
            {
                WalletReadInTransaction = command.Transaction is not null;
                Entered.TrySetResult(); await Release.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
            }
            return result;
        }
    }
    private sealed class FailingRefundNotifications(NotificationService real, CancellationTokenSource cancellation, bool cancel)
        : INotificationService
    {
        public async Task<NotificationDto> CreateAsync(Guid userId, string type, string title, string message,
            object? data, CancellationToken token)
        {
            var result = await real.CreateAsync(userId, type, title, message, data, token);
            if (cancel) { cancellation.Cancel(); throw new OperationCanceledException(cancellation.Token); }
            throw new InvalidOperationException("Controlled refund notification failure.");
        }
        public Task<IReadOnlyList<NotificationDto>> CreateManyAsync(
            IReadOnlyCollection<CreateNotificationRequest> requests, CancellationToken token) => real.CreateManyAsync(requests, token);
    }

    private sealed class FailingNotifications(NotificationService real, CancellationTokenSource cancellation, bool cancel)
        : INotificationService
    {
        public Task<NotificationDto> CreateAsync(Guid userId, string type, string title, string message,
            object? data, CancellationToken token) => real.CreateAsync(userId, type, title, message, data, token);
        public async Task<IReadOnlyList<NotificationDto>> CreateManyAsync(
            IReadOnlyCollection<CreateNotificationRequest> requests, CancellationToken token)
        {
            var result = await real.CreateManyAsync(requests, token);
            if (cancel) { cancellation.Cancel(); throw new OperationCanceledException(cancellation.Token); }
            throw new InvalidOperationException("Controlled deletion ready notification failure.");
        }
    }

    private static User NewUser(string name)
    {
        var unique = Guid.NewGuid().ToString("N");
        return new() { FullName = name, Email = unique + "@test.local",
            NormalizedEmail = (unique + "@test.local").ToUpperInvariant(), SpendId = unique,
            NormalizedSpendId = unique.ToUpperInvariant(), PasswordHash = "test-only",
            SecurityPinHash = new SecurityPinService().Hash(Pin), SystemRole = SystemRole.User, Status = UserStatus.Active };
    }
    private sealed class Clock : IClock
    {
        public DateTime UtcNow => new(2026, 10, 12, 4, 0, 0, DateTimeKind.Utc);
    }
    private sealed class OwnedDatabase : IAsyncDisposable
    {
        private readonly string name = "SpendWise_GroupDeletionS052_Test_" + Guid.NewGuid().ToString("N");
        private readonly string connection;
        private bool ownsCatalog;
        private readonly IClock clock = new Clock();
        public Guid Owner { get; private set; }
        public Guid First { get; private set; }
        public Guid Second { get; private set; }
        public Guid Group { get; private set; }
        public Guid Request { get; private set; }
        private OwnedDatabase() => connection = SqlTestFixtureLifecycle.CreateOwnedConnectionString(name,
            Environment.GetEnvironmentVariable(SqlTestFixtureLifecycle.InstanceEnvironmentVariable));
        public SpendWiseDbContext Context(params IInterceptor[] interceptors) => new(
            new DbContextOptionsBuilder<SpendWiseDbContext>().UseSqlServer(connection).AddInterceptors(interceptors).Options, clock);
        public VoteGroupDeletionCommand Vote(Guid user) => new(user, Group, Request, new(true, Pin));
        public VoteGroupDeletionCommandHandler Voter(SpendWiseDbContext db, INotificationService? supplied = null)
        {
            var notifications = supplied ?? new NotificationService(db);
            var access = new GroupAccess(db);
            var security = new FinancialSecurityGuard(db, new SecurityPinService(), new AccountCredentialLockout(db, clock));
            var accounting = new WalletFundAccounting(db, access, new LedgerService(db, clock), security, clock);
            return new(db, clock, notifications, new GroupDeletionSupport(db, clock, notifications, accounting), security);
        }
        public CreateGroupRefundPaymentCommand Refund(Guid actor, Guid refund, string key) =>
            new(actor, Group, Request, refund, new(key, Pin));
        public async Task<Guid> RefundIdAsync(Guid user)
        {
            await using var db = Context();
            return (await db.GroupRefunds.SingleAsync(x => x.UserId == user)).Id;
        }
        public async Task MakeReadyAsync()
        {
            await using (var db = Context()) await Voter(db).Handle(Vote(First), default);
            await using (var db = Context()) await Voter(db).Handle(Vote(Second), default);
        }
        public async Task RefundAllAsync()
        {
            foreach (var user in new[] { Owner, First, Second })
            {
                var id = await RefundIdAsync(user); await using var db = Context();
                await Refunder(db).Handle(Refund(user, id, "all-" + user.ToString("N")), default);
            }
        }
        public async Task AssertClosedAsync()
        {
            await using var db = Context();
            Assert.Equal(GroupStatus.Closed, (await db.Groups.SingleAsync()).Status);
            var request = await db.GroupDeletionRequests.SingleAsync();
            Assert.Equal(GroupDeletionRequestStatus.Completed, request.Status); Assert.NotNull(request.CompletedAt);
            Assert.Equal(0m, (await db.SharedFunds.SingleAsync()).Balance);
            Assert.Equal(0m, (await db.LedgerAccounts.SingleAsync(x => x.Type == LedgerAccountType.SharedFund)).Balance);
            Assert.All(await db.GroupFundShares.ToListAsync(), x => Assert.Equal(0m, x.RemainingAmount));
            Assert.All(await db.Wallets.ToListAsync(), x => Assert.Equal(500m, x.AvailableBalance));
            Assert.All(await db.GroupRefunds.ToListAsync(), x => Assert.Equal(GroupRefundStatus.Completed, x.Status));
            Assert.Equal(3, await db.LedgerTransactions.CountAsync(x => x.BusinessType == "GroupRefund"));
            Assert.Equal(3, await db.GroupContributions.CountAsync());
            Assert.Equal(6, await db.LedgerTransactions.CountAsync());
            Assert.Equal(12, await db.LedgerEntries.CountAsync());
            Assert.Equal(3, await db.Notifications.CountAsync(x => x.Type == NotificationType.GroupRefundCompleted));
        }
        public CreateGroupRefundPaymentCommandHandler Refunder(SpendWiseDbContext db, INotificationService? supplied = null)
        {
            var security = new FinancialSecurityGuard(db, new SecurityPinService(), new AccountCredentialLockout(db, clock));
            var access = new GroupAccess(db); var ledger = new LedgerService(db, clock);
            return new(db, access, supplied ?? new NotificationService(db), security,
                new WalletFundAccounting(db, access, ledger, security, clock), ledger, clock);
        }
        public FinalizeGroupDeletionCommandHandler Finalizer(SpendWiseDbContext db) => new(db, new GroupAccess(db),
            new FinancialSecurityGuard(db, new SecurityPinService(), new AccountCredentialLockout(db, clock)), clock);
        public static async Task<OwnedDatabase> CreateAsync(bool funded = false)
        {
            var owned = new OwnedDatabase();
            owned.Validate();
            await SqlTestFixtureLifecycle.CreateOwnedCatalogAsync(owned.connection);
            owned.ownsCatalog = true;
            return await SqlTestFixtureLifecycle.CreateAsync(async () =>
            {
                await using var db = owned.Context();
                await SqlTestFixtureLifecycle.MeasureAsync(owned.name, "create", () => db.Database.EnsureCreatedAsync());
                await SqlTestFixtureLifecycle.ConfigureOwnedCatalogAsync(owned.connection);
                var owner = NewUser("Owner"); var first = NewUser("First"); var second = NewUser("Second");
                var account = new LedgerAccount { Code = "GROUP:" + Guid.NewGuid().ToString("N"),
                    Name = "Owned empty fund", Type = LedgerAccountType.SharedFund };
                var group = new Group { Name = "Owned deletion group", Type = GroupType.Friends,
                    Members = [new() { UserId = owner.Id, Role = GroupRole.Owner, JoinedAt = owned.clock.UtcNow },
                        new() { UserId = first.Id, Role = GroupRole.Member, JoinedAt = owned.clock.UtcNow },
                        new() { UserId = second.Id, Role = GroupRole.Member, JoinedAt = owned.clock.UtcNow }],
                    SharedFund = new SharedFund { LedgerAccount = account, LedgerAccountId = account.Id } };
                group.SharedFund.GroupId = group.Id; account.GroupId = group.Id;
                db.AddRange(owner, first, second, group);
                await SqlTestFixtureLifecycle.MeasureAsync(owned.name, "seed-save", () => db.SaveChangesAsync());
                owned.Owner = owner.Id; owned.First = first.Id; owned.Second = second.Id; owned.Group = group.Id;
                var notifications = new NotificationService(db); var access = new GroupAccess(db);
                var security = new FinancialSecurityGuard(db, new SecurityPinService(), new AccountCredentialLockout(db, owned.clock));
                var accounting = new WalletFundAccounting(db, access, new LedgerService(db, owned.clock), security, owned.clock);
                if (funded)
                {
                    foreach (var user in new[] { owner, first, second })
                    {
                        var wallet = WalletFactory.Create(user.Id, "Owned personal wallet", WalletType.Main, true);
                        wallet.AvailableBalance = 500; wallet.LedgerAccount.Balance = 500;
                        db.Wallets.Add(wallet);
                        await SqlTestFixtureLifecycle.MeasureAsync(owned.name, "seed-save", () => db.SaveChangesAsync());
                        await SqlTestFixtureLifecycle.MeasureAsync(owned.name, "seed-contribution", () =>
                            accounting.ContributeAsync(user.Id, group.Id, wallet.Id, 100m,
                                "Owned test contribution", "seed-" + user.Id.ToString("N"), Pin, null, default));
                    }
                }
                var created = await new CreateGroupDeletionRequestCommandHandler(db, access, owned.clock, notifications,
                    new GroupDeletionSupport(db, owned.clock, notifications, accounting)).Handle(new(owner.Id, group.Id), default);
                owned.Request = created.Id;
                return owned;
            }, owned.DisposeAsync);
        }
        public async Task<bool> ObserveWaitAsync(int session, Task operation, CancellationToken token = default)
        {
            Validate();
            await using var observer = new SqlConnection(connection); await observer.OpenAsync(token);
            await using var command = observer.CreateCommand(); command.CommandTimeout = 2;
            command.CommandText = "SELECT COUNT(*) FROM sys.dm_tran_locks WHERE request_session_id=@session AND resource_database_id=DB_ID() AND request_status IN ('WAIT','LOW_PRIORITY_WAIT')";
            command.Parameters.AddWithValue("@session", session);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            while (!operation.IsCompleted && watch.Elapsed < TimeSpan.FromSeconds(5))
            {
                if (Convert.ToInt32(await command.ExecuteScalarAsync(token)) > 0) return true;
                await Task.Delay(10, token); // Observe real SQL state, never infer race from elapsed sleep.
            }
            return false;
        }
        public async Task CheckConnectionPoolingAsync(Xunit.Abstractions.ITestOutputHelper output)
        {
            Validate();
            foreach (var pooling in new[] { false, true })
            {
                var target = new SqlConnectionStringBuilder(connection) { Pooling = pooling }.ConnectionString;
                using var pool = new SqlConnection(target);
                SqlConnection.ClearPool(pool);
                var ids = new List<Guid>();
                var watch = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    for (var cycle = 0; cycle < 3; cycle++)
                    {
                        await using var session = new SqlConnection(target);
                        await session.OpenAsync();
                        ids.Add(session.ClientConnectionId);
                        await using var command = session.CreateCommand(); command.CommandText = "SELECT 1";
                        Assert.Equal(1, Convert.ToInt32(await command.ExecuteScalarAsync()));
                    }
                    output.WriteLine($"Owned connection cycles: pooling={pooling};cycles=3;elapsedMs={watch.Elapsed.TotalMilliseconds:F2}");
                    Assert.Equal(pooling ? 1 : 3, ids.Distinct().Count());
                    SqlConnection.ClearPool(pool);
                    await using var fresh = new SqlConnection(target); await fresh.OpenAsync();
                    Assert.DoesNotContain(fresh.ClientConnectionId, ids);
                }
                finally { SqlConnection.ClearPool(pool); }
            }
        }
        private void Validate()
        {
            SqlTestFixtureLifecycle.ValidateOwnedConnection(connection);
            var parsed = new SqlConnectionStringBuilder(connection);
            if (!Regex.IsMatch(name, @"\ASpendWise_GroupDeletionS052_Test_[a-f0-9]{32}\z")
                || parsed.InitialCatalog != name
                || !parsed.IntegratedSecurity || parsed.UserID.Length != 0 || parsed.Password.Length != 0)
                throw new InvalidOperationException("Refusing non-owned group deletion SQL database.");
        }
        public async ValueTask DisposeAsync()
        {
            Validate(); await using var db = Context();
            if (!ownsCatalog) throw new InvalidOperationException("Catalog was not created by this fixture.");
            await SqlTestFixtureLifecycle.VerifyCatalogOwnerAsync(connection);
            await SqlTestFixtureLifecycle.MeasureAsync(name, "cleanup", () => db.Database.EnsureDeletedAsync());
            ownsCatalog = false;
        }
    }
}
