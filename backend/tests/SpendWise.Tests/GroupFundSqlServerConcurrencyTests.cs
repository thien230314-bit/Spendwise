using System.Data.Common;
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
using SpendWise.Infrastructure.Finance;
using SpendWise.Infrastructure.Identity;
using SpendWise.Infrastructure.Persistence;
using SpendWise.Infrastructure.Notifications;
using Xunit;
using Group = SpendWise.Domain.Groups.Group;

namespace SpendWise.Tests;

public sealed class GroupFundSqlServerConcurrencyTests : SqlTestFixtureScope
{
    private const string Pin = "123456";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Fresh_overlapping_payments_cannot_pay_one_obligation_twice(bool round)
    {
        var database = await OwnAsync(OwnedDatabase.CreateAsync());
        var ids = await database.SeedAsync();
        var firstGate = new BusinessSnapshotGate(round ? "GroupFundRoundMembers" : "FundRequestRecipients");
        var secondGate = new BusinessSnapshotGate(round ? "GroupFundRoundMembers" : "FundRequestRecipients");
        await using var firstDb = database.Context(firstGate);
        await using var secondDb = database.Context(secondGate);
        await secondDb.Database.OpenConnectionAsync();
        var session = ((SqlConnection)secondDb.Database.GetDbConnection()).ServerProcessId;
        var first = Pay(firstDb, database.Clock, ids, round, 100, "first");
        Task<GroupContributionDto>? second = null;
        try
        {
            await firstGate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            second = Pay(secondDb, database.Clock, ids, round, 100, "second");
            var observed = await Task.WhenAny(secondGate.Entered.Task,
                database.ObserveLockWaitAsync(session, second)).WaitAsync(TimeSpan.FromSeconds(10));
            if (observed != secondGate.Entered.Task)
                Assert.True(await (Task<bool>)observed, "Contender neither reached its stale snapshot nor waited on SQL admission.");
            firstGate.Release.TrySetResult();
            await first.WaitAsync(TimeSpan.FromSeconds(10));
            secondGate.Release.TrySetResult();
            var error = await Record.ExceptionAsync(() => second.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.IsAssignableFrom<AppException>(error);
            await AssertAccounting(database, ids, 100, 1);
            await using var verify = database.Context();
            if (round)
            {
                var stored = await verify.GroupFundRounds.SingleAsync();
                Assert.Equal(100, stored.CollectedAmount);
                Assert.Equal(100, (await verify.GroupFundRoundMembers.SingleAsync()).PaidAmount);
                Assert.Equal(GroupFundRoundStatus.Completed, stored.Status);
            }
            else
                Assert.Equal(FundRequestRecipientStatus.Paid, (await verify.FundRequestRecipients.SingleAsync()).Status);
        }
        finally
        {
            firstGate.Release.TrySetResult(); secondGate.Release.TrySetResult();
            await Record.ExceptionAsync(() => first);
            if (second is not null) await Record.ExceptionAsync(() => second);
        }
    }

    [Fact]
    public async Task Stale_round_aggregate_cannot_lose_a_different_members_valid_payment()
    {
        var database = await OwnAsync(OwnedDatabase.CreateAsync());
        var ids = await database.SeedAsync(twoMembers: true);
        await using var stale = database.Context();
        await stale.GroupFundRoundMembers.Include(x => x.Round).SingleAsync(x => x.UserId == ids.SecondUser);
        await using (var first = database.Context())
            await Pay(first, database.Clock, ids, true, 100, "owner-payment");
        var otherIds = ids with { User = ids.SecondUser, Wallet = ids.SecondWallet };
        await Pay(stale, database.Clock, otherIds, true, 100, "member-payment");
        await using var verify = database.Context();
        var round = await verify.GroupFundRounds.SingleAsync();
        Assert.Equal(200, round.CollectedAmount);
        Assert.Equal(GroupFundRoundStatus.Completed, round.Status);
        Assert.All(await verify.GroupFundRoundMembers.ToListAsync(), x => Assert.Equal(100, x.PaidAmount));
        Assert.Equal(200, (await verify.SharedFunds.SingleAsync()).Balance);
        Assert.Equal(200, (await verify.LedgerAccounts.SingleAsync(x => x.GroupId == ids.Group)).Balance);
        Assert.Equal(200, await verify.GroupFundShares.SumAsync(x => x.RemainingAmount));
        Assert.Equal(2, await verify.GroupContributions.CountAsync());
        Assert.Equal(2, await verify.LedgerTransactions.CountAsync());
        await AssertBalanced(verify);
    }

    [Fact]
    public async Task Same_key_round_retry_replays_but_distinct_partial_payments_are_not_deduplicated()
    {
        var database = await OwnAsync(OwnedDatabase.CreateAsync());
        var ids = await database.SeedAsync();
        Guid first;
        await using (var db = database.Context()) first = (await Pay(db, database.Clock, ids, true, 50, "partial-one")).Id;
        await using (var db = database.Context())
            Assert.Equal(first, (await Pay(db, database.Clock, ids, true, 50, "partial-one")).Id);
        await using (var db = database.Context())
            Assert.Equal("idempotency_conflict", (await Assert.ThrowsAsync<ConflictException>(() =>
                Pay(db, database.Clock, ids, true, 40, "partial-one"))).Code);
        await using (var db = database.Context())
            Assert.NotEqual(first, (await Pay(db, database.Clock, ids, true, 50, "partial-two")).Id);
        await AssertAccounting(database, ids, 100, 2);
        await using var verify = database.Context();
        Assert.Equal(100, (await verify.GroupFundRounds.SingleAsync()).CollectedAmount);
        Assert.Equal(GroupFundRoundStatus.Completed, (await verify.GroupFundRounds.SingleAsync()).Status);
    }

    [Fact]
    public async Task Request_retry_same_key_is_once_and_different_key_is_rejected()
    {
        var database = await OwnAsync(OwnedDatabase.CreateAsync());
        var ids = await database.SeedAsync();
        Guid first;
        await using (var db = database.Context()) first = (await Pay(db, database.Clock, ids, false, 100, "request")).Id;
        await using (var db = database.Context())
            Assert.Equal(first, (await Pay(db, database.Clock, ids, false, 100, "request")).Id);
        await using (var db = database.Context())
            Assert.IsAssignableFrom<AppException>(await Record.ExceptionAsync(() => Pay(db, database.Clock, ids, false, 100, "changed")));
        await AssertAccounting(database, ids, 100, 1);
    }

    [Fact]
    public async Task Contribution_distinct_keys_remain_distinct_and_same_key_replays()
    {
        var database = await OwnAsync(OwnedDatabase.CreateAsync());
        var ids = await database.SeedAsync();
        Guid first;
        await using (var db = database.Context()) first = (await Contribution(db, database.Clock, ids, 50, "one")).Id;
        await using (var db = database.Context()) Assert.Equal(first, (await Contribution(db, database.Clock, ids, 50, "one")).Id);
        await using (var db = database.Context()) Assert.NotEqual(first, (await Contribution(db, database.Clock, ids, 50, "two")).Id);
        await AssertAccounting(database, ids, 100, 2);
    }

    [Fact]
    public async Task Contribution_rejects_wrong_pin_foreign_wallet_and_insufficient_balance_without_posting()
    {
        var database = await OwnAsync(OwnedDatabase.CreateAsync());
        var ids = await database.SeedAsync(twoMembers: true);
        await using (var db = database.Context())
            Assert.Equal("pin_verification_failed", (await Assert.ThrowsAsync<AppException>(() =>
                Accounting(db, database.Clock).ContributeAsync(ids.User, ids.Group, ids.Wallet, 50, "test", "bad-pin", "999999", null, default))).Code);
        await using (var db = database.Context())
            Assert.IsType<NotFoundException>(await Record.ExceptionAsync(() => Contribution(db, database.Clock,
                ids with { Wallet = ids.SecondWallet }, 50, "foreign-wallet")));
        await using (var db = database.Context())
            Assert.Equal("insufficient_balance", (await Assert.ThrowsAsync<ConflictException>(() =>
                Contribution(db, database.Clock, ids, 600, "overspend"))).Code);
        await using var verify = database.Context();
        Assert.Empty(await verify.GroupContributions.ToListAsync());
        Assert.Empty(await verify.LedgerTransactions.ToListAsync());
        Assert.Empty(await verify.FundTransactions.ToListAsync());
        Assert.Equal(0, (await verify.SharedFunds.SingleAsync()).Balance);
        Assert.All(await verify.Wallets.ToListAsync(), x => Assert.Equal(500, x.AvailableBalance));
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public async Task Overlapping_contributions_preserve_keys_and_never_overspend(
        bool differentUser, bool sameKey, bool insufficient)
    {
        var database = await OwnAsync(OwnedDatabase.CreateAsync());
        var ids = await database.SeedAsync(twoMembers: true);
        var firstGate = new WalletReadGate(); var secondGate = new WalletReadGate();
        await using var firstDb = database.Context(firstGate);
        await using var secondDb = database.Context(secondGate);
        await secondDb.Database.OpenConnectionAsync();
        var session = ((SqlConnection)secondDb.Database.GetDbConnection()).ServerProcessId;
        var other = differentUser ? ids with { User = ids.SecondUser, Wallet = ids.SecondWallet } : ids;
        decimal amount = insufficient ? 400 : 50;
        var first = Contribution(firstDb, database.Clock, ids, amount, "one");
        Task<GroupContributionDto>? second = null;
        try
        {
            await firstGate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            second = Contribution(secondDb, database.Clock, other, amount, sameKey ? "one" : "two");
            var observed = await Task.WhenAny(secondGate.Entered.Task, database.ObserveLockWaitAsync(session, second))
                .WaitAsync(TimeSpan.FromSeconds(10));
            if (observed != secondGate.Entered.Task) Assert.True(await (Task<bool>)observed);
            firstGate.Release.TrySetResult(); await first.WaitAsync(TimeSpan.FromSeconds(10));
            secondGate.Release.TrySetResult();
            var error = await Record.ExceptionAsync(() => second.WaitAsync(TimeSpan.FromSeconds(10)));
            if (insufficient)
                Assert.Equal("insufficient_balance", Assert.IsType<ConflictException>(error).Code);
            else if (error is not null)
            {
                // Existing optimistic protection may legitimately require retry with the same key.
                Assert.Equal("concurrent_financial_operation", Assert.IsType<ConflictException>(error).Code);
                await using var retry = database.Context();
                await Contribution(retry, database.Clock, other, amount, sameKey ? "one" : "two");
            }
            await using var verify = database.Context();
            var expectedPostings = sameKey || insufficient ? 1 : 2;
            Assert.Equal(expectedPostings, await verify.GroupContributions.CountAsync());
            Assert.Equal(expectedPostings, await verify.LedgerTransactions.CountAsync());
            Assert.Equal(amount * expectedPostings, (await verify.SharedFunds.SingleAsync()).Balance);
            Assert.Equal(amount * expectedPostings, await verify.GroupFundShares.SumAsync(x => x.RemainingAmount));
            Assert.Equal(amount * expectedPostings, (await verify.LedgerAccounts.SingleAsync(x => x.GroupId == ids.Group)).Balance);
            Assert.All(await verify.Wallets.ToListAsync(), x => Assert.True(x.AvailableBalance >= 0));
            if (differentUser)
                Assert.All(await verify.Wallets.ToListAsync(), x => Assert.Equal(450, x.AvailableBalance));
            else Assert.Equal(500 - amount * expectedPostings, (await verify.Wallets.SingleAsync(x => x.Id == ids.Wallet)).AvailableBalance);
            await AssertBalanced(verify);
        }
        finally
        {
            firstGate.Release.TrySetResult(); secondGate.Release.TrySetResult();
            await Record.ExceptionAsync(() => first);
            if (second is not null) await Record.ExceptionAsync(() => second);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Contribution_final_failure_or_cancellation_rolls_back_and_retry_posts_once(bool cancel)
    {
        var database = await OwnAsync(OwnedDatabase.CreateAsync());
        var ids = await database.SeedAsync();
        using var cancellation = new CancellationTokenSource();
        await using (var failing = database.Context(new FailContributionSave(cancellation, cancel)))
        {
            var error = await Record.ExceptionAsync(() => Accounting(failing, database.Clock).ContributeAsync(
                ids.User, ids.Group, ids.Wallet, 50, "Test contribution", "rollback", Pin, null, cancellation.Token));
            if (cancel) Assert.IsAssignableFrom<OperationCanceledException>(error);
            else Assert.IsType<InvalidOperationException>(error);
        }
        await using (var verify = database.Context())
        {
            Assert.Empty(await verify.GroupContributions.ToListAsync());
            Assert.Empty(await verify.GroupFundShares.ToListAsync());
            Assert.Empty(await verify.FundTransactions.ToListAsync());
            Assert.Empty(await verify.GroupActivities.ToListAsync());
            Assert.Empty(await verify.LedgerTransactions.ToListAsync());
            Assert.Empty(await verify.LedgerEntries.ToListAsync());
            Assert.Equal(500, (await verify.Wallets.SingleAsync(x => x.Id == ids.Wallet)).AvailableBalance);
            Assert.Equal(0, (await verify.SharedFunds.SingleAsync()).Balance);
        }
        await using (var retry = database.Context()) await Contribution(retry, database.Clock, ids, 50, "rollback");
        await AssertAccounting(database, ids, 50, 1);
    }

    [Fact]
    public async Task Different_groups_make_progress_while_one_contribution_is_held()
    {
        var database = await OwnAsync(OwnedDatabase.CreateAsync());
        var ids = await database.SeedAsync(); var other = await database.SeedAsync();
        var gate = new WalletReadGate();
        await using var heldDb = database.Context(gate);
        var held = Contribution(heldDb, database.Clock, ids, 50, "independent");
        try
        {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await using var independent = database.Context();
            await Contribution(independent, database.Clock, other, 50, "independent").WaitAsync(TimeSpan.FromSeconds(10));
            gate.Release.TrySetResult(); await held.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally { gate.Release.TrySetResult(); await Record.ExceptionAsync(() => held); }
        await using var verify = database.Context();
        Assert.Equal(2, await verify.GroupContributions.CountAsync());
        Assert.All(await verify.SharedFunds.ToListAsync(), x => Assert.Equal(50, x.Balance));
        await AssertBalanced(verify);
    }

    [Fact]
    public async Task Deletion_overlapping_contribution_freezes_only_after_consistent_fund_commit()
    {
        var database = await OwnAsync(OwnedDatabase.CreateAsync());
        var ids = await database.SeedAsync();
        var gate = new WalletReadGate();
        await using var contributionDb = database.Context(gate);
        await using var deletionDb = database.Context();
        await deletionDb.Database.OpenConnectionAsync();
        var session = ((SqlConnection)deletionDb.Database.GetDbConnection()).ServerProcessId;
        var contribution = Contribution(contributionDb, database.Clock, ids, 50, "before-delete");
        Task<GroupDeletionRequestDto>? deletion = null;
        try
        {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var notifications = new NotificationService(deletionDb);
            deletion = new CreateGroupDeletionRequestCommandHandler(deletionDb, new GroupAccess(deletionDb), database.Clock,
                notifications, new GroupDeletionSupport(deletionDb, database.Clock, notifications, Accounting(deletionDb, database.Clock)))
                .Handle(new CreateGroupDeletionRequestCommand(ids.User, ids.Group), default);
            // Wait on an observed lock or completion; the test never assumes a race from elapsed sleep.
            await Task.WhenAny(deletion, database.ObserveLockWaitAsync(session, deletion)).WaitAsync(TimeSpan.FromSeconds(10));
            gate.Release.TrySetResult(); await contribution.WaitAsync(TimeSpan.FromSeconds(10));
            var result = await deletion.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(GroupDeletionRequestStatus.Ready.ToString(), result.Status);
            Assert.Equal(50, result.FundBalanceSnapshot);
        }
        finally
        {
            gate.Release.TrySetResult(); await Record.ExceptionAsync(() => contribution);
            if (deletion is not null) await Record.ExceptionAsync(() => deletion);
        }
        await using (var blocked = database.Context())
            Assert.Equal("group_deletion_frozen", (await Assert.ThrowsAsync<ConflictException>(() =>
                Contribution(blocked, database.Clock, ids, 50, "after-delete"))).Code);
        await AssertAccounting(database, ids, 50, 1);
        await using var verify = database.Context();
        Assert.Equal(50, (await verify.GroupRefunds.SingleAsync()).RefundAmount);
        Assert.Equal(GroupStatus.DeletionPending, (await verify.Groups.SingleAsync()).Status);
    }

    [Fact]
    public async Task Fund_expense_replay_is_once_and_insufficient_fund_preserves_member_shares()
    {
        var database = await OwnAsync(OwnedDatabase.CreateAsync());
        var ids = await database.SeedAsync();
        await using (var db = database.Context()) await Contribution(db, database.Clock, ids, 100, "fund");
        var request = new CreateGroupExpenseRequest("Expense", 60, null, "Test", "Fund", null, null, null, Pin, "expense");
        async Task<GroupExpenseDto> Spend(SpendWiseDbContext db, CreateGroupExpenseRequest input) =>
            await new CreateCurrentGroupExpenseCommandHandler(db, new GroupAccess(db), database.Clock,
                new FinancialSecurityGuard(db, new SecurityPinService(), new AccountCredentialLockout(db, database.Clock)),
                Accounting(db, database.Clock), new LedgerService(db, database.Clock))
                .Handle(new CreateCurrentGroupExpenseCommand(ids.User, ids.Group, input), default);
        Guid expense;
        await using (var db = database.Context()) expense = (await Spend(db, request)).Id;
        await using (var db = database.Context()) Assert.Equal(expense, (await Spend(db, request)).Id);
        await using (var db = database.Context())
            Assert.Equal("group_fund_insufficient", (await Assert.ThrowsAsync<ConflictException>(() =>
                Spend(db, request with { Amount = 50, IdempotencyKey = "too-much" }))).Code);
        await using var verify = database.Context();
        Assert.Single(await verify.GroupExpenses.ToListAsync());
        Assert.Equal(40, (await verify.SharedFunds.SingleAsync()).Balance);
        Assert.Equal(40, await verify.GroupFundShares.SumAsync(x => x.RemainingAmount));
        Assert.Equal(40, (await verify.LedgerAccounts.SingleAsync(x => x.GroupId == ids.Group)).Balance);
        Assert.Equal(2, await verify.LedgerTransactions.CountAsync());
        await AssertBalanced(verify);
    }

    [Fact]
    public async Task Overdue_listing_cannot_overwrite_a_concurrently_paid_request_and_allow_second_payment()
    {
        var database = await OwnAsync(OwnedDatabase.CreateAsync());
        var ids = await database.SeedAsync();
        await using (var setup = database.Context())
            await setup.FundRequests.ExecuteUpdateAsync(s => s.SetProperty(x => x.DueDate, database.Clock.UtcNow.AddMinutes(-1)));
        var gate = new OverdueSaveGate();
        await using var listingDb = database.Context(gate);
        await using var paymentDb = database.Context();
        await paymentDb.Database.OpenConnectionAsync();
        var session = ((SqlConnection)paymentDb.Database.GetDbConnection()).ServerProcessId;
        var listing = new GetFundRequestsQueryHandler(listingDb, new GroupAccess(listingDb), database.Clock)
            .Handle(new GetFundRequestsQuery(ids.User, ids.Group), default);
        Task<GroupContributionDto>? payment = null;
        try
        {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            payment = Pay(paymentDb, database.Clock, ids, false, 100, "paid-during-listing");
            var observed = await Task.WhenAny(payment, database.ObserveLockWaitAsync(session, payment))
                .WaitAsync(TimeSpan.FromSeconds(10));
            if (observed == payment) await payment;
            else Assert.True(await (Task<bool>)observed);
            gate.Release.TrySetResult();
            await listing.WaitAsync(TimeSpan.FromSeconds(10));
            await payment.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            gate.Release.TrySetResult(); await Record.ExceptionAsync(() => listing);
            if (payment is not null) await Record.ExceptionAsync(() => payment);
        }
        await using (var verify = database.Context())
        {
            var recipient = await verify.FundRequestRecipients.SingleAsync();
            Assert.Equal(FundRequestRecipientStatus.Paid, recipient.Status);
            Assert.NotNull(recipient.PaymentId);
        }
        await using (var duplicate = database.Context())
            Assert.IsAssignableFrom<AppException>(await Record.ExceptionAsync(() =>
                Pay(duplicate, database.Clock, ids, false, 100, "second-payment")));
        await AssertAccounting(database, ids, 100, 1);
    }

    [Fact]
    public async Task Fund_expense_overlapping_deletion_cannot_leave_an_outdated_refund_plan()
    {
        var database = await OwnAsync(OwnedDatabase.CreateAsync());
        var ids = await database.SeedAsync();
        await using (var seed = database.Context())
        {
            await Contribution(seed, database.Clock, ids, 100, "initial-fund");
            seed.LedgerAccounts.Add(new LedgerAccount
            {
                Code = "SYSTEM:GroupExpenseClearing:VND", Name = "Test expense clearing",
                Type = LedgerAccountType.ExpenseClearing, IsSystem = true
            });
            await seed.SaveChangesAsync();
        }
        var gate = new ClearingReadGate();
        await using var expenseDb = database.Context(gate);
        await using var deletionDb = database.Context();
        await deletionDb.Database.OpenConnectionAsync();
        var session = ((SqlConnection)deletionDb.Database.GetDbConnection()).ServerProcessId;
        var request = new CreateGroupExpenseRequest("Expense before deletion", 60, null, "Test", "Fund", null, null, null, Pin, "overlap-expense");
        var expense = new CreateCurrentGroupExpenseCommandHandler(expenseDb, new GroupAccess(expenseDb), database.Clock,
            new FinancialSecurityGuard(expenseDb, new SecurityPinService(), new AccountCredentialLockout(expenseDb, database.Clock)),
            Accounting(expenseDb, database.Clock), new LedgerService(expenseDb, database.Clock))
            .Handle(new CreateCurrentGroupExpenseCommand(ids.User, ids.Group, request), default);
        Task<GroupDeletionRequestDto>? deletion = null;
        try
        {
            // Existing mutable-state check has completed; no fund balance snapshot was loaded yet.
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var notifications = new NotificationService(deletionDb);
            deletion = new CreateGroupDeletionRequestCommandHandler(deletionDb, new GroupAccess(deletionDb), database.Clock,
                notifications, new GroupDeletionSupport(deletionDb, database.Clock, notifications, Accounting(deletionDb, database.Clock)))
                .Handle(new CreateGroupDeletionRequestCommand(ids.User, ids.Group), default);
            var observed = await Task.WhenAny(deletion, database.ObserveLockWaitAsync(session, deletion))
                .WaitAsync(TimeSpan.FromSeconds(10));
            if (observed == deletion) await deletion;
            else Assert.True(await (Task<bool>)observed);
            gate.Release.TrySetResult(); await expense.WaitAsync(TimeSpan.FromSeconds(10));
            var plan = await deletion.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(40, plan.FundBalanceSnapshot);
        }
        finally
        {
            gate.Release.TrySetResult(); await Record.ExceptionAsync(() => expense);
            if (deletion is not null) await Record.ExceptionAsync(() => deletion);
        }
        await using var verify = database.Context();
        Assert.Equal(40, (await verify.SharedFunds.SingleAsync()).Balance);
        Assert.Equal(40, (await verify.LedgerAccounts.SingleAsync(x => x.GroupId == ids.Group)).Balance);
        Assert.Equal(40, await verify.GroupFundShares.SumAsync(x => x.RemainingAmount));
        Assert.Equal(40, (await verify.GroupRefunds.SingleAsync()).RefundAmount);
        Assert.Equal(2, await verify.LedgerTransactions.CountAsync());
        await AssertBalanced(verify);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Creating_fund_request_or_round_cannot_commit_after_deletion_has_frozen_the_group(bool round)
    {
        var database = await OwnAsync(OwnedDatabase.CreateAsync());
        var ids = await database.SeedAsync();
        var gate = new FundCreationSaveGate(round);
        await using var creationDb = database.Context(gate);
        await using var deletionDb = database.Context();
        await deletionDb.Database.OpenConnectionAsync();
        var session = ((SqlConnection)deletionDb.Database.GetDbConnection()).ServerProcessId;
        var creation = CreateFundObligation(creationDb, database.Clock, ids, round, new NotificationService(creationDb));
        Task<GroupDeletionRequestDto>? deletion = null;
        try
        {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var notifications = new NotificationService(deletionDb);
            deletion = new CreateGroupDeletionRequestCommandHandler(deletionDb, new GroupAccess(deletionDb), database.Clock,
                notifications, new GroupDeletionSupport(deletionDb, database.Clock, notifications, Accounting(deletionDb, database.Clock)))
                .Handle(new CreateGroupDeletionRequestCommand(ids.User, ids.Group), default);
            var observed = await Task.WhenAny(deletion, database.ObserveLockWaitAsync(session, deletion))
                .WaitAsync(TimeSpan.FromSeconds(10));
            var deletionWon = observed == deletion;
            if (deletionWon) await deletion;
            else Assert.True(await (Task<bool>)observed);
            gate.Release.TrySetResult();
            var error = await Record.ExceptionAsync(() => creation.WaitAsync(TimeSpan.FromSeconds(10)));
            if (deletionWon)
                Assert.IsAssignableFrom<AppException>(error);
            else Assert.Null(error);
            await deletion.WaitAsync(TimeSpan.FromSeconds(10));
            await using var verify = database.Context();
            var count = round ? await verify.GroupFundRounds.CountAsync() : await verify.FundRequests.CountAsync();
            Assert.Equal(deletionWon ? 1 : 2, count);
            Assert.Equal(GroupStatus.DeletionPending, (await verify.Groups.SingleAsync()).Status);
            Assert.Equal(GroupDeletionRequestStatus.Ready, (await verify.GroupDeletionRequests.SingleAsync()).Status);
            Assert.Equal(0, (await verify.SharedFunds.SingleAsync()).Balance);
            Assert.Empty(await verify.LedgerTransactions.ToListAsync());
        }
        finally
        {
            gate.Release.TrySetResult(); await Record.ExceptionAsync(() => creation);
            if (deletion is not null) await Record.ExceptionAsync(() => deletion);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Fund_request_or_round_creation_rolls_back_when_persisted_notification_fails(bool round)
    {
        var database = await OwnAsync(OwnedDatabase.CreateAsync());
        var ids = await database.SeedAsync(twoMembers: true);
        await using (var failing = database.Context())
            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateFundObligation(failing, database.Clock, ids,
                round, new FailAfterNotification(new NotificationService(failing))));
        await using var verify = database.Context();
        Assert.Single(await verify.FundRequests.ToListAsync());
        Assert.Single(await verify.GroupFundRounds.ToListAsync());
        Assert.Single(await verify.FundRequestRecipients.ToListAsync());
        Assert.Equal(2, await verify.GroupFundRoundMembers.CountAsync());
        Assert.Empty(await verify.GroupActivities.ToListAsync());
        Assert.Empty(await verify.Notifications.ToListAsync());
        Assert.Empty(await verify.OutboxMessages.ToListAsync());
        Assert.Equal(GroupStatus.Active, (await verify.Groups.SingleAsync()).Status);
        Assert.Equal(0, (await verify.SharedFunds.SingleAsync()).Balance);
    }

    private static async Task CreateFundObligation(SpendWiseDbContext db, IClock clock, Ids ids, bool round, INotificationService notifications)
    {
        if (round)
            await new CreateFundRoundCommandHandler(db, new GroupAccess(db), notifications).Handle(
                new CreateFundRoundCommand(ids.User, ids.Group, new CreateFundRoundRequest("New round", null, 100)), default);
        else
        {
            var recipient = await db.GroupMembers.AnyAsync(x => x.GroupId == ids.Group && x.UserId == ids.SecondUser)
                ? ids.SecondUser : ids.User;
            await new CreateFundRequestCommandHandler(db, new GroupAccess(db), clock, notifications).Handle(
                new CreateFundRequestCommand(ids.User, ids.Group, new CreateFundRequestRequest("New request", 100, null,
                    [recipient])), default);
        }
    }

    private sealed class FundCreationSaveGate(bool round) : SaveChangesInterceptor
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int gated;
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var pending = round
                ? eventData.Context!.ChangeTracker.Entries<GroupFundRound>().Any(x => x.State == EntityState.Added)
                : eventData.Context!.ChangeTracker.Entries<FundRequest>().Any(x => x.State == EntityState.Added);
            if (pending && Interlocked.Exchange(ref gated, 1) == 0)
            {
                Entered.TrySetResult(); await Release.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
            }
            return result;
        }
    }

    private sealed class FailAfterNotification(NotificationService real) : INotificationService
    {
        public Task<NotificationDto> CreateAsync(Guid userId, string type, string title, string message,
            object? data, CancellationToken token) => real.CreateAsync(userId, type, title, message, data, token);
        public async Task<IReadOnlyList<NotificationDto>> CreateManyAsync(IReadOnlyCollection<CreateNotificationRequest> requests, CancellationToken token)
        {
            await real.CreateManyAsync(requests, token);
            throw new InvalidOperationException("Controlled obligation notification failure.");
        }
    }

    private sealed class OverdueSaveGate : SaveChangesInterceptor
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int gated;
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<FundRequestRecipient>().Any(x =>
                    x.State == EntityState.Modified && x.Entity.Status == FundRequestRecipientStatus.Overdue)
                && Interlocked.Exchange(ref gated, 1) == 0)
            {
                Entered.TrySetResult(); await Release.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
            }
            return result;
        }
    }

    private sealed class ClearingReadGate : DbCommandInterceptor
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int gated;
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("FROM [LedgerAccounts]", StringComparison.Ordinal)
                && (command.CommandText.Contains("SYSTEM:GroupExpenseClearing:VND", StringComparison.Ordinal)
                    || command.Parameters.Cast<DbParameter>().Any(p => Equals(p.Value, "SYSTEM:GroupExpenseClearing:VND")))
                && Interlocked.Exchange(ref gated, 1) == 0)
            {
                Entered.TrySetResult(); await Release.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
            }
            return result;
        }
    }

    private sealed class WalletReadGate : DbCommandInterceptor
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int gated;
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("FROM [Wallets]", StringComparison.Ordinal) && Interlocked.Exchange(ref gated, 1) == 0)
            {
                Entered.TrySetResult(); await Release.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
            }
            return result;
        }
    }

    private sealed class FailContributionSave(CancellationTokenSource cancellation, bool cancel) : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<GroupContribution>().Any(x => x.State == EntityState.Added))
            {
                if (!cancel) throw new InvalidOperationException("Controlled contribution final-save failure.");
                cancellation.Cancel(); throw new OperationCanceledException(cancellation.Token);
            }
            return ValueTask.FromResult(result);
        }
    }

    private static Task<GroupContributionDto> Pay(SpendWiseDbContext db, IClock clock, Ids ids, bool round, decimal amount, string key) =>
        round ? new PayFundRoundCommandHandler(db, Accounting(db, clock), clock).Handle(
            new PayFundRoundCommand(ids.User, ids.Group, ids.Round, new PayFundRoundRequest(amount, key, ids.Wallet, Pin)), default)
        : new PayFundRequestCommandHandler(db, Accounting(db, clock)).Handle(
            new PayFundRequestCommand(ids.User, ids.Group, ids.Request, new PayFundRequestRequest(key, ids.Wallet, Pin)), default);

    private static Task<GroupContributionDto> Contribution(SpendWiseDbContext db, IClock clock, Ids ids, decimal amount, string key) =>
        Accounting(db, clock).ContributeAsync(ids.User, ids.Group, ids.Wallet, amount, "Test contribution", key, Pin, null, default);

    private static WalletFundAccounting Accounting(SpendWiseDbContext db, IClock clock) => new(db, new GroupAccess(db),
        new LedgerService(db, clock), new FinancialSecurityGuard(db, new SecurityPinService(), new AccountCredentialLockout(db, clock)), clock);

    private static async Task AssertAccounting(OwnedDatabase database, Ids ids, decimal paid, int postings)
    {
        await using var db = database.Context();
        Assert.Equal(500 - paid, (await db.Wallets.SingleAsync(x => x.Id == ids.Wallet)).AvailableBalance);
        Assert.Equal(500 - paid, (await db.LedgerAccounts.SingleAsync(x => x.WalletId == ids.Wallet)).Balance);
        Assert.Equal(paid, (await db.SharedFunds.SingleAsync()).Balance);
        Assert.Equal(paid, (await db.LedgerAccounts.SingleAsync(x => x.GroupId == ids.Group)).Balance);
        Assert.Equal(paid, await db.GroupFundShares.SumAsync(x => x.RemainingAmount));
        Assert.Equal(postings, await db.GroupContributions.CountAsync());
        Assert.Equal(postings, await db.FundTransactions.CountAsync());
        Assert.Equal(postings, await db.LedgerTransactions.CountAsync());
        await AssertBalanced(db);
    }

    private static async Task AssertBalanced(SpendWiseDbContext db)
    {
        foreach (var posting in await db.LedgerTransactions.Include(x => x.Entries).ToListAsync())
            Assert.Equal(posting.Entries.Where(x => x.Direction == LedgerEntryDirection.Debit).Sum(x => x.Amount),
                posting.Entries.Where(x => x.Direction == LedgerEntryDirection.Credit).Sum(x => x.Amount));
    }

    private sealed class BusinessSnapshotGate(string table) : DbCommandInterceptor
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool snapshotRead;
        private int gated;
        public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains($"FROM [{table}]", StringComparison.Ordinal)) snapshotRead = true;
            return ValueTask.FromResult(result);
        }
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            // The business entity is fully materialized before PIN reads and incidental User-row writes.
            if (snapshotRead && command.CommandText.Contains("FROM [Users]", StringComparison.Ordinal)
                && Interlocked.Exchange(ref gated, 1) == 0)
            {
                Entered.TrySetResult();
                await Release.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
            }
            return result;
        }
    }

    private sealed record Ids(Guid User, Guid Wallet, Guid Group, Guid Request, Guid Round, Guid SecondUser, Guid SecondWallet);
    private sealed class FixedClock : IClock { public DateTime UtcNow => new(2026, 10, 12, 4, 0, 0, DateTimeKind.Utc); }

    private sealed class OwnedDatabase : IAsyncDisposable
    {
        private readonly string name = "SpendWise_GroupFundS052_Test_" + Guid.NewGuid().ToString("N");
        private readonly string connection;
        private bool ownsCatalog;
        public IClock Clock { get; } = new FixedClock();
        private OwnedDatabase() => connection = SqlTestFixtureLifecycle.CreateOwnedConnectionString(name,
            Environment.GetEnvironmentVariable(SqlTestFixtureLifecycle.InstanceEnvironmentVariable));
        public SpendWiseDbContext Context(params IInterceptor[] interceptors) => new(
            new DbContextOptionsBuilder<SpendWiseDbContext>().UseSqlServer(connection).AddInterceptors(interceptors).Options, Clock);
        public static async Task<OwnedDatabase> CreateAsync()
        {
            var owned = new OwnedDatabase(); owned.ValidateOwnership();
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
        public async Task<bool> ObserveLockWaitAsync(int session, Task pending)
        {
            ValidateOwnership();
            await using var observer = new SqlConnection(connection); await observer.OpenAsync();
            await using var command = observer.CreateCommand(); command.CommandTimeout = 2;
            command.CommandText = "SELECT COUNT(*) FROM sys.dm_tran_locks WHERE request_session_id=@session AND resource_database_id=DB_ID() AND request_status IN ('WAIT','LOW_PRIORITY_WAIT')";
            command.Parameters.AddWithValue("@session", session);
            var deadline = System.Diagnostics.Stopwatch.StartNew();
            while (!pending.IsCompleted && deadline.Elapsed < TimeSpan.FromSeconds(5))
            {
                if (Convert.ToInt32(await command.ExecuteScalarAsync()) > 0) return true;
                await Task.Delay(10); // Poll observed SQL state, not a sleep-based race assertion.
            }
            return false;
        }
        public async Task<Ids> SeedAsync(bool twoMembers = false)
        {
            await using var db = Context();
            User User(string label)
            {
                var unique = Guid.NewGuid().ToString("N");
                return new User { FullName = label, Email = unique + "@test.local", NormalizedEmail = unique.ToUpperInvariant(),
                    SpendId = unique, NormalizedSpendId = unique.ToUpperInvariant(), PasswordHash = "test-only-hash",
                    SecurityPinHash = new SecurityPinService().Hash(Pin) };
            }
            var owner = User("Owner"); var other = User("Member");
            var wallet = WalletFactory.Create(owner.Id, "Owner wallet", WalletType.Main, true);
            var secondWallet = WalletFactory.Create(other.Id, "Member wallet", WalletType.Main, true);
            foreach (var w in new[] { wallet, secondWallet }) { w.AvailableBalance = 500; w.LedgerAccount.Balance = 500; }
            var fundAccount = new LedgerAccount { Code = "GROUP-FUND:" + Guid.NewGuid().ToString("N"), Name = "Group fund", Type = LedgerAccountType.SharedFund };
            var group = new Group { Name = "SQL test group", Type = GroupType.Friends,
                SharedFund = new SharedFund { LedgerAccount = fundAccount, LedgerAccountId = fundAccount.Id },
                Members = [new GroupMember { UserId = owner.Id, Role = GroupRole.Owner, JoinedAt = Clock.UtcNow }] };
            if (twoMembers) group.Members.Add(new GroupMember { UserId = other.Id, Role = GroupRole.Member, JoinedAt = Clock.UtcNow });
            fundAccount.GroupId = group.Id; group.SharedFund.GroupId = group.Id;
            var request = new FundRequest { GroupId = group.Id, CreatedByUserId = owner.Id, Purpose = "Request", AmountPerMember = 100,
                Recipients = [new FundRequestRecipient { UserId = owner.Id, UserNameSnapshot = owner.FullName }] };
            var round = new GroupFundRound { GroupId = group.Id, CreatedByUserId = owner.Id, Title = "Round", TargetAmount = twoMembers ? 200 : 100,
                Members = [new GroupFundRoundMember { UserId = owner.Id, UserNameSnapshot = owner.FullName, RequiredAmount = 100 }] };
            if (twoMembers) round.Members.Add(new GroupFundRoundMember { UserId = other.Id, UserNameSnapshot = other.FullName, RequiredAmount = 100 });
            db.AddRange(owner, other, wallet, secondWallet, group, request, round);
            await SqlTestFixtureLifecycle.MeasureAsync(name, "seed-save", () => db.SaveChangesAsync());
            return new(owner.Id, wallet.Id, group.Id, request.Id, round.Id, other.Id, secondWallet.Id);
        }
        private void ValidateOwnership()
        {
            SqlTestFixtureLifecycle.ValidateOwnedConnection(connection);
            var parsed = new SqlConnectionStringBuilder(connection);
            if (!Regex.IsMatch(name, @"\ASpendWise_GroupFundS052_Test_[a-f0-9]{32}\z") || parsed.InitialCatalog != name
                || !parsed.IntegratedSecurity || parsed.UserID.Length != 0 || parsed.Password.Length != 0)
                throw new InvalidOperationException("Refusing non-owned SQL test database access.");
        }
        public async ValueTask DisposeAsync()
        {
            ValidateOwnership(); await using var db = Context();
            if (!ownsCatalog) throw new InvalidOperationException("Catalog was not created by this fixture.");
            await SqlTestFixtureLifecycle.VerifyCatalogOwnerAsync(connection);
            await SqlTestFixtureLifecycle.MeasureAsync(name, "cleanup", () => db.Database.EnsureDeletedAsync());
            ownsCatalog = false;
        }
    }
}
