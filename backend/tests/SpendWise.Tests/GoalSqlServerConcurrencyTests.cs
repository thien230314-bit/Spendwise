using System.Text.RegularExpressions;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features;
using SpendWise.Application.Features.Authentication;
using SpendWise.Application.Features.Goals.Commands;
using SpendWise.Application.Features.Goals.Commands.UpdateGoal;
using SpendWise.Application.Features.Wallets;
using SpendWise.Application.Planning;
using SpendWise.Domain.Finance;
using SpendWise.Domain.Identity;
using SpendWise.Domain.Planning;
using SpendWise.Infrastructure.Finance;
using SpendWise.Infrastructure.Identity;
using SpendWise.Infrastructure.Persistence;
using Xunit;

namespace SpendWise.Tests;

public sealed class GoalSqlServerConcurrencyTests : SqlTestFixtureScope
{
    private const string Pin = "123456";

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task Goal_ownership_precedes_foreign_key_replay_and_owner_replay_is_unchanged(
        bool contribute, bool matchingDetails)
    {
        var fixture = await OwnAsync(OwnedDatabase.CreateAsync());
        var owner = await fixture.SeedAsync();
        var outsider = await fixture.SeedAsync();
        var request = new GoalMovementRequest(owner.FirstWallet, contribute ? 100 : 40, "owned-key", Pin);
        await using (var initial = fixture.Context())
        {
            await fixture.Executor(initial).MoveAsync(owner.User, owner.Goal,
                new(owner.FirstWallet, 100, "owned-key", Pin), true, default);
            if (!contribute) await fixture.Executor(initial).MoveAsync(owner.User, owner.Goal, request, false, default);
        }
        var observer = new GoalMovementReadObserver();
        await using (var foreign = fixture.Context(observer))
        {
            var error = await Assert.ThrowsAsync<NotFoundException>(() => fixture.Executor(foreign).MoveAsync(
                outsider.User, owner.Goal,
                matchingDetails ? request : request with { WalletId = outsider.FirstWallet, Amount = 1 },
                contribute, default));
            Assert.Equal("goal_not_found", error.Code);
            Assert.Equal(404, error.StatusCode);
            Assert.Equal(0, observer.Reads);
        }
        await using var replayDb = fixture.Context();
        var replay = await fixture.Executor(replayDb).MoveAsync(owner.User, owner.Goal,
            request with { IdempotencyKey = " owned-key " }, contribute, default);
        var allocated = contribute ? 100m : 60m;
        var count = contribute ? 1 : 2;
        Assert.Equal(allocated, replay.CurrentAllocatedAmount);
        await using var verify = fixture.Context();
        Assert.Equal(count, await verify.GoalMovements.CountAsync());
        Assert.Equal(count, await verify.FinancialTransactions.CountAsync());
        Assert.Equal(count, await verify.LedgerTransactions.CountAsync());
        Assert.Equal(allocated, (await verify.Goals.Include(x => x.LedgerAccount)
            .SingleAsync(x => x.Id == owner.Goal)).LedgerAccount.Balance);
        Assert.Equal(500m - allocated, (await verify.Wallets.SingleAsync(x => x.Id == owner.FirstWallet)).AvailableBalance);
        Assert.Equal(500m, (await verify.Wallets.SingleAsync(x => x.Id == outsider.FirstWallet)).AvailableBalance);
        await AssertBalanced(verify);
    }

    private sealed class GoalMovementReadObserver : DbCommandInterceptor
    {
        public int Reads { get; private set; }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("FROM [GoalMovements]", StringComparison.Ordinal)) Reads++;
            return ValueTask.FromResult(result);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Stale_goal_without_tracked_ledger_cannot_overwrite_a_committed_movement(bool contribute)
    {
        var fixture = await OwnAsync(OwnedDatabase.CreateAsync());
        var ids = await fixture.SeedAsync();
        await using (var initial = fixture.Context())
            await fixture.Executor(initial).MoveAsync(ids.User, ids.Goal, new(ids.FirstWallet, 100, "initial", Pin), true, default);

        await using var stale = fixture.Context();
        var trackedGoal = await stale.Goals.SingleAsync(x => x.Id == ids.Goal);
        Assert.Equal(100m, trackedGoal.CurrentAllocatedAmount);
        Assert.Empty(stale.ChangeTracker.Entries<LedgerAccount>());
        await using (var other = fixture.Context())
            await fixture.Executor(other).MoveAsync(ids.User, ids.Goal, new(ids.SecondWallet, 100, "other", Pin), true, default);

        // Only Goal is stale; ledger accounts are loaded after the other transaction commits.
        await fixture.Executor(stale).MoveAsync(ids.User, ids.Goal,
            new(ids.FirstWallet, 50, "after-other", Pin), contribute, default);
        await using var verify = fixture.Context();
        var goal = await verify.Goals.Include(x => x.LedgerAccount).SingleAsync();
        var expected = contribute ? 250m : 150m;
        Assert.Equal(expected, goal.CurrentAllocatedAmount);
        Assert.Equal(expected, goal.LedgerAccount.Balance);
        Assert.Equal(3, await verify.GoalMovements.CountAsync());
        Assert.Equal(3, await verify.FinancialTransactions.CountAsync());
        Assert.Equal(3, await verify.LedgerTransactions.CountAsync());
        Assert.Equal(expected, await verify.GoalMovements.SumAsync(x =>
            x.Type == GoalMovementType.Contribution ? x.Amount : -x.Amount));
        Assert.Equal(1000m - expected, await verify.Wallets.SumAsync(x => x.AvailableBalance));
        await AssertBalanced(verify);
    }

    [Fact]
    public async Task Stale_goal_metadata_cannot_lower_target_below_committed_allocation()
    {
        var fixture = await OwnAsync(OwnedDatabase.CreateAsync());
        var ids = await fixture.SeedAsync();
        await using var stale = fixture.Context();
        await stale.Goals.SingleAsync(x => x.Id == ids.Goal);
        await using (var other = fixture.Context())
            await fixture.Executor(other).MoveAsync(ids.User, ids.Goal, new(ids.FirstWallet, 100, "committed", Pin), true, default);

        var error = await Assert.ThrowsAsync<ConflictException>(() =>
            new UpdateGoalCommandHandler(stale, fixture.Security(stale), fixture.Clock)
                .Handle(new(ids.User, ids.Goal, new("Invalid lowered target", 50, SecurityPin: Pin)), default));
        Assert.Equal("goal_target_below_allocated", error.Code);
        await using var verify = fixture.Context();
        var goal = await verify.Goals.Include(x => x.LedgerAccount).SingleAsync();
        Assert.Equal("Owned test goal", goal.Name);
        Assert.Equal(1000m, goal.TargetAmount);
        Assert.Equal(100m, goal.CurrentAllocatedAmount);
        Assert.Equal(100m, goal.LedgerAccount.Balance);
        Assert.Single(await verify.GoalMovements.ToListAsync());
        await AssertBalanced(verify);
    }

    private static async Task AssertBalanced(SpendWiseDbContext db)
    {
        foreach (var transaction in await db.LedgerTransactions.Include(x => x.Entries).ToListAsync())
            Assert.Equal(transaction.Entries.Where(x => x.Direction == LedgerEntryDirection.Debit).Sum(x => x.Amount),
                transaction.Entries.Where(x => x.Direction == LedgerEntryDirection.Credit).Sum(x => x.Amount));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Goal_final_save_failure_or_cancellation_rolls_back_ledger_and_retry_posts_once(bool cancel)
    {
        var fixture = await OwnAsync(OwnedDatabase.CreateAsync());
        var ids = await fixture.SeedAsync();
        using var cancellation = new CancellationTokenSource();
        await using (var failing = fixture.Context(new FailMovementSave(cancellation, cancel)))
        {
            var error = await Record.ExceptionAsync(() => fixture.Executor(failing).MoveAsync(ids.User, ids.Goal,
                new(ids.FirstWallet, 100, "rollback", Pin), true, cancellation.Token));
            if (cancel) Assert.IsAssignableFrom<OperationCanceledException>(error);
            else Assert.IsType<InvalidOperationException>(error);
        }
        await using (var verify = fixture.Context())
        {
            var goal = await verify.Goals.Include(x => x.LedgerAccount).SingleAsync();
            Assert.Equal(0m, goal.CurrentAllocatedAmount);
            Assert.Equal(0m, goal.LedgerAccount.Balance);
            Assert.All(await verify.Wallets.ToListAsync(), x => Assert.Equal(500m, x.AvailableBalance));
            Assert.Empty(await verify.GoalMovements.ToListAsync());
            Assert.Empty(await verify.FinancialTransactions.ToListAsync());
            Assert.Empty(await verify.LedgerTransactions.ToListAsync());
            Assert.Empty(await verify.LedgerEntries.ToListAsync());
        }
        await using (var retry = fixture.Context())
            await fixture.Executor(retry).MoveAsync(ids.User, ids.Goal, new(ids.FirstWallet, 100, "rollback", Pin), true, default);
        await using var final = fixture.Context();
        var stored = await final.Goals.Include(x => x.LedgerAccount).SingleAsync();
        Assert.Equal(100m, stored.CurrentAllocatedAmount);
        Assert.Equal(100m, stored.LedgerAccount.Balance);
        Assert.Single(await final.GoalMovements.ToListAsync());
        Assert.Single(await final.FinancialTransactions.ToListAsync());
        Assert.Single(await final.LedgerTransactions.ToListAsync());
        Assert.Equal(400m, (await final.Wallets.SingleAsync(x => x.Id == ids.FirstWallet)).AvailableBalance);
        await AssertBalanced(final);
    }

    private sealed class FailMovementSave(CancellationTokenSource cancellation, bool cancel) : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<GoalMovement>().Any(x => x.State == EntityState.Added))
            {
                if (cancel) { cancellation.Cancel(); throw new OperationCanceledException(cancellation.Token); }
                throw new InvalidOperationException("Controlled final GoalMovement save failure.");
            }
            return ValueTask.FromResult(result);
        }
    }

    [Fact]
    public async Task Fresh_overlapping_same_key_goal_contributions_replay_once_without_duplicate_business_rows()
    {
        var fixture = await OwnAsync(OwnedDatabase.CreateAsync());
        var ids = await fixture.SeedAsync();
        var firstGate = new WalletReadGate();
        var secondGate = new WalletReadGate();
        await using var firstDb = fixture.Context(firstGate);
        await using var secondDb = fixture.Context(secondGate);
        await secondDb.Database.OpenConnectionAsync();
        var session = ((SqlConnection)secondDb.Database.GetDbConnection()).ServerProcessId;
        var first = fixture.Executor(firstDb).MoveAsync(ids.User, ids.Goal, new(ids.FirstWallet, 100, "overlap", Pin), true, default);
        Task<GoalDto>? second = null;
        try
        {
            await firstGate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            second = fixture.Executor(secondDb).MoveAsync(ids.User, ids.Goal,
                new(ids.FirstWallet, 100, "overlap", Pin), true, default);
            var observed = await Task.WhenAny(secondGate.Entered.Task, fixture.ObserveWaitAsync(session, second))
                .WaitAsync(TimeSpan.FromSeconds(10));
            if (observed != secondGate.Entered.Task)
                Assert.True(await (Task<bool>)observed, "Contender neither reached wallet read nor waited on SQL admission.");
            firstGate.Release.TrySetResult();
            await first.WaitAsync(TimeSpan.FromSeconds(10));
            secondGate.Release.TrySetResult();
            await second.WaitAsync(TimeSpan.FromSeconds(10));
            await using var verify = fixture.Context();
            var goal = await verify.Goals.Include(x => x.LedgerAccount).SingleAsync();
            Assert.Equal(100m, goal.CurrentAllocatedAmount);
            Assert.Equal(100m, goal.LedgerAccount.Balance);
            Assert.Single(await verify.GoalMovements.ToListAsync());
            Assert.Single(await verify.FinancialTransactions.ToListAsync());
            Assert.Single(await verify.LedgerTransactions.ToListAsync());
            Assert.Equal(400m, (await verify.Wallets.SingleAsync(x => x.Id == ids.FirstWallet)).AvailableBalance);
            await AssertBalanced(verify);
        }
        finally
        {
            firstGate.Release.TrySetResult(); secondGate.Release.TrySetResult();
            await Record.ExceptionAsync(() => first.WaitAsync(TimeSpan.FromSeconds(10)));
            if (second is not null) await Record.ExceptionAsync(() => second.WaitAsync(TimeSpan.FromSeconds(10)));
        }
    }

    private sealed class WalletReadGate : DbCommandInterceptor
    {
        private int entered;
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("FROM [Wallets]", StringComparison.Ordinal)
                && Interlocked.CompareExchange(ref entered, 1, 0) == 0)
            {
                Entered.TrySetResult();
                await Release.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
            }
            return result;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Fresh_distinct_key_goal_movements_serialize_and_cannot_overdraw_reserve(bool withdraw)
    {
        var fixture = await OwnAsync(OwnedDatabase.CreateAsync());
        var ids = await fixture.SeedAsync();
        if (withdraw)
        {
            await using var initial = fixture.Context();
            await fixture.Executor(initial).MoveAsync(ids.User, ids.Goal,
                new(ids.FirstWallet, 100, "fund-reserve", Pin), true, default);
        }
        var gate = new WalletReadGate();
        await using var firstDb = fixture.Context(gate);
        await using var secondDb = fixture.Context();
        await secondDb.Database.OpenConnectionAsync();
        var session = ((SqlConnection)secondDb.Database.GetDbConnection()).ServerProcessId;
        var amount = withdraw ? 75m : 100m;
        var first = fixture.Executor(firstDb).MoveAsync(ids.User, ids.Goal,
            new(ids.FirstWallet, amount, "first-distinct", Pin), !withdraw, default);
        Task<GoalDto>? second = null;
        try
        {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            second = fixture.Executor(secondDb).MoveAsync(ids.User, ids.Goal,
                new(ids.SecondWallet, amount, "second-distinct", Pin), !withdraw, default);
            Assert.True(await fixture.ObserveWaitAsync(session, second), "Contender must wait on SQL admission.");
            gate.Release.TrySetResult();
            await first.WaitAsync(TimeSpan.FromSeconds(10));
            if (withdraw)
            {
                var error = await Assert.ThrowsAsync<ConflictException>(() => second.WaitAsync(TimeSpan.FromSeconds(10)));
                Assert.Equal("insufficient_goal_balance", error.Code);
            }
            else await second.WaitAsync(TimeSpan.FromSeconds(10));
            await using var verify = fixture.Context();
            var goal = await verify.Goals.Include(x => x.LedgerAccount).SingleAsync();
            var expected = withdraw ? 25m : 200m;
            Assert.Equal(expected, goal.CurrentAllocatedAmount);
            Assert.Equal(expected, goal.LedgerAccount.Balance);
            Assert.Equal(2, await verify.GoalMovements.CountAsync());
            Assert.Equal(2, await verify.FinancialTransactions.CountAsync());
            Assert.Equal(2, await verify.LedgerTransactions.CountAsync());
            Assert.Equal(1000m - expected, await verify.Wallets.SumAsync(x => x.AvailableBalance));
            Assert.All(await verify.Wallets.ToListAsync(), x => Assert.True(x.AvailableBalance >= 0));
            if (withdraw)
            {
                Assert.Equal(475m, (await verify.Wallets.SingleAsync(x => x.Id == ids.FirstWallet)).AvailableBalance);
                Assert.Equal(500m, (await verify.Wallets.SingleAsync(x => x.Id == ids.SecondWallet)).AvailableBalance);
                Assert.Single(await verify.GoalMovements.Where(x => x.Type == GoalMovementType.Withdrawal).ToListAsync());
            }
            await AssertBalanced(verify);
        }
        finally
        {
            gate.Release.TrySetResult();
            await Record.ExceptionAsync(() => first.WaitAsync(TimeSpan.FromSeconds(10)));
            if (second is not null) await Record.ExceptionAsync(() => second.WaitAsync(TimeSpan.FromSeconds(10)));
        }
    }

    [Fact]
    public async Task Metadata_update_overlapping_contribution_uses_committed_allocation_for_target_validation()
    {
        var fixture = await OwnAsync(OwnedDatabase.CreateAsync());
        var ids = await fixture.SeedAsync();
        var gate = new WalletReadGate();
        await using var movementDb = fixture.Context(gate);
        await using var metadataDb = fixture.Context();
        await metadataDb.Database.OpenConnectionAsync();
        var session = ((SqlConnection)metadataDb.Database.GetDbConnection()).ServerProcessId;
        var movement = fixture.Executor(movementDb).MoveAsync(ids.User, ids.Goal,
            new(ids.FirstWallet, 100, "overlap-allocation", Pin), true, default);
        Task<GoalDto>? metadata = null;
        try
        {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            metadata = new UpdateGoalCommandHandler(metadataDb, fixture.Security(metadataDb), fixture.Clock)
                .Handle(new(ids.User, ids.Goal, new("Must not commit", 50, SecurityPin: Pin)), default);
            Assert.True(await fixture.ObserveWaitAsync(session, metadata), "Metadata writer must wait on SQL admission.");
            gate.Release.TrySetResult();
            await movement.WaitAsync(TimeSpan.FromSeconds(10));
            var error = await Assert.ThrowsAsync<ConflictException>(() => metadata.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.Equal("goal_target_below_allocated", error.Code);
            await using var verify = fixture.Context();
            var goal = await verify.Goals.Include(x => x.LedgerAccount).SingleAsync();
            Assert.Equal("Owned test goal", goal.Name);
            Assert.Equal(1000m, goal.TargetAmount);
            Assert.Equal(100m, goal.CurrentAllocatedAmount);
            Assert.Equal(100m, goal.LedgerAccount.Balance);
            Assert.Single(await verify.GoalMovements.ToListAsync());
            Assert.Single(await verify.FinancialTransactions.ToListAsync());
            await AssertBalanced(verify);
        }
        finally
        {
            gate.Release.TrySetResult();
            await Record.ExceptionAsync(() => movement.WaitAsync(TimeSpan.FromSeconds(10)));
            if (metadata is not null) await Record.ExceptionAsync(() => metadata.WaitAsync(TimeSpan.FromSeconds(10)));
        }
    }

    [Fact]
    public async Task Withdrawal_overlapping_contribution_uses_new_reserve_and_preserves_all_financial_history()
    {
        var fixture = await OwnAsync(OwnedDatabase.CreateAsync());
        var ids = await fixture.SeedAsync();
        await using (var initial = fixture.Context())
            await fixture.Executor(initial).MoveAsync(ids.User, ids.Goal,
                new(ids.FirstWallet, 100, "initial-reserve", Pin), true, default);
        var gate = new WalletReadGate();
        await using var contributionDb = fixture.Context(gate);
        await using var withdrawalDb = fixture.Context();
        await withdrawalDb.Database.OpenConnectionAsync();
        var session = ((SqlConnection)withdrawalDb.Database.GetDbConnection()).ServerProcessId;
        var contribution = fixture.Executor(contributionDb).MoveAsync(ids.User, ids.Goal,
            new(ids.FirstWallet, 100, "held-contribution", Pin), true, default);
        Task<GoalDto>? withdrawal = null;
        try
        {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            withdrawal = fixture.Executor(withdrawalDb).MoveAsync(ids.User, ids.Goal,
                new(ids.SecondWallet, 150, "withdraw-new-reserve", Pin), false, default);
            Assert.True(await fixture.ObserveWaitAsync(session, withdrawal), "Withdrawal must wait on SQL admission.");
            gate.Release.TrySetResult();
            await contribution.WaitAsync(TimeSpan.FromSeconds(10));
            await withdrawal.WaitAsync(TimeSpan.FromSeconds(10));
            await using var verify = fixture.Context();
            var goal = await verify.Goals.Include(x => x.LedgerAccount).SingleAsync();
            Assert.Equal(50m, goal.CurrentAllocatedAmount);
            Assert.Equal(50m, goal.LedgerAccount.Balance);
            Assert.Equal(3, await verify.GoalMovements.CountAsync());
            Assert.Equal(3, await verify.FinancialTransactions.CountAsync());
            Assert.Equal(3, await verify.LedgerTransactions.CountAsync());
            Assert.Equal(50m, await verify.GoalMovements.SumAsync(x =>
                x.Type == GoalMovementType.Contribution ? x.Amount : -x.Amount));
            Assert.Equal(300m, (await verify.Wallets.SingleAsync(x => x.Id == ids.FirstWallet)).AvailableBalance);
            Assert.Equal(650m, (await verify.Wallets.SingleAsync(x => x.Id == ids.SecondWallet)).AvailableBalance);
            Assert.Equal(1000m, goal.LedgerAccount.Balance + await verify.Wallets.SumAsync(x => x.AvailableBalance));
            await AssertBalanced(verify);
        }
        finally
        {
            gate.Release.TrySetResult();
            await Record.ExceptionAsync(() => contribution.WaitAsync(TimeSpan.FromSeconds(10)));
            if (withdrawal is not null) await Record.ExceptionAsync(() => withdrawal.WaitAsync(TimeSpan.FromSeconds(10)));
        }
    }

    [Fact]
    public async Task Goal_and_wallet_ownership_failures_leave_all_financial_state_unchanged()
    {
        var fixture = await OwnAsync(OwnedDatabase.CreateAsync());
        var owner = await fixture.SeedAsync();
        var outsider = await fixture.SeedAsync();
        await using var db = fixture.Context();
        var goalError = await Assert.ThrowsAsync<NotFoundException>(() => fixture.Executor(db).MoveAsync(
            outsider.User, owner.Goal, new(outsider.FirstWallet, 100, "foreign-goal", Pin), true, default));
        Assert.Equal("goal_not_found", goalError.Code);
        var walletError = await Assert.ThrowsAsync<NotFoundException>(() => fixture.Executor(db).MoveAsync(
            owner.User, owner.Goal, new(outsider.FirstWallet, 100, "foreign-wallet", Pin), true, default));
        Assert.Equal("wallet_not_found", walletError.Code);
        var metadataError = await Assert.ThrowsAsync<NotFoundException>(() =>
            new UpdateGoalCommandHandler(db, fixture.Security(db), fixture.Clock)
                .Handle(new(outsider.User, owner.Goal, new("Foreign edit", 500, SecurityPin: Pin)), default));
        Assert.Equal("goal_not_found", metadataError.Code);
        await using var verify = fixture.Context();
        Assert.All(await verify.Goals.Include(x => x.LedgerAccount).ToListAsync(), goal =>
        {
            Assert.Equal(0m, goal.CurrentAllocatedAmount);
            Assert.Equal(0m, goal.LedgerAccount.Balance);
            Assert.Equal(1000m, goal.TargetAmount);
        });
        Assert.All(await verify.Wallets.ToListAsync(), x => Assert.Equal(500m, x.AvailableBalance));
        Assert.Empty(await verify.GoalMovements.ToListAsync());
        Assert.Empty(await verify.FinancialTransactions.ToListAsync());
        Assert.Empty(await verify.LedgerTransactions.ToListAsync());
    }

    [Fact]
    public async Task Different_user_and_goal_make_progress_while_another_goal_is_held()
    {
        var fixture = await OwnAsync(OwnedDatabase.CreateAsync());
        var firstIds = await fixture.SeedAsync();
        var independentIds = await fixture.SeedAsync();
        var gate = new WalletReadGate();
        await using var firstDb = fixture.Context(gate);
        await using var independentDb = fixture.Context();
        var first = fixture.Executor(firstDb).MoveAsync(firstIds.User, firstIds.Goal,
            new(firstIds.FirstWallet, 100, "held", Pin), true, default);
        Task<GoalDto>? independent = null;
        try
        {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            independent = fixture.Executor(independentDb).MoveAsync(independentIds.User, independentIds.Goal,
                new(independentIds.FirstWallet, 100, "independent", Pin), true, default);
            await independent.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(first.IsCompleted);
            gate.Release.TrySetResult();
            await first.WaitAsync(TimeSpan.FromSeconds(10));
            await using var verify = fixture.Context();
            Assert.All(await verify.Goals.Include(x => x.LedgerAccount).ToListAsync(), goal =>
            {
                Assert.Equal(100m, goal.CurrentAllocatedAmount);
                Assert.Equal(100m, goal.LedgerAccount.Balance);
            });
            Assert.Equal(2, await verify.GoalMovements.CountAsync());
            Assert.Equal(2, await verify.LedgerTransactions.CountAsync());
            Assert.Equal(1800m, await verify.Wallets.SumAsync(x => x.AvailableBalance));
            await AssertBalanced(verify);
        }
        finally
        {
            gate.Release.TrySetResult();
            await Record.ExceptionAsync(() => first.WaitAsync(TimeSpan.FromSeconds(10)));
            if (independent is not null) await Record.ExceptionAsync(() => independent.WaitAsync(TimeSpan.FromSeconds(10)));
        }
    }

    private sealed record Ids(Guid User, Guid Goal, Guid FirstWallet, Guid SecondWallet);
    private sealed class FixedClock : IClock
    {
        public DateTime UtcNow => new(2026, 10, 13, 2, 0, 0, DateTimeKind.Utc);
    }

    private sealed class OwnedDatabase : IAsyncDisposable
    {
        private readonly string name = "SpendWise_GoalS053_Test_" + Guid.NewGuid().ToString("N");
        private readonly string connection;
        private bool ownsCatalog;
        public IClock Clock { get; } = new FixedClock();
        private OwnedDatabase() => connection = SqlTestFixtureLifecycle.CreateOwnedConnectionString(name,
            Environment.GetEnvironmentVariable(SqlTestFixtureLifecycle.InstanceEnvironmentVariable));
        public SpendWiseDbContext Context(params IInterceptor[] interceptors) => new(
            new DbContextOptionsBuilder<SpendWiseDbContext>().UseSqlServer(connection).AddInterceptors(interceptors).Options, Clock);
        public FinancialSecurityGuard Security(SpendWiseDbContext db) => new(db, new SecurityPinService(), new AccountCredentialLockout(db, Clock));
        public GoalMovementExecutor Executor(SpendWiseDbContext db) => new(db, new LedgerService(db, Clock), Clock, Security(db), new SpendWise.Application.Features.Banking.ActiveBankGuard(db));
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
        public async Task<Ids> SeedAsync()
        {
            await using var db = Context();
            var unique = Guid.NewGuid().ToString("N");
            var user = new User { FullName = "Owned goal test", Email = unique + "@test.local",
                NormalizedEmail = (unique + "@test.local").ToUpperInvariant(), SpendId = unique,
                NormalizedSpendId = unique.ToUpperInvariant(), PasswordHash = "test-only-hash",
                SecurityPinHash = new SecurityPinService().Hash(Pin) };
            var first = WalletFactory.Create(user.Id, "First", WalletType.Main, true);
            var second = WalletFactory.Create(user.Id, "Second", WalletType.Savings, false);
            foreach (var wallet in new[] { first, second })
            {
                wallet.AvailableBalance = 500;
                wallet.LedgerAccount.Balance = 500;
            }
            var account = new LedgerAccount { Code = "goal:" + unique, Name = "Goal reserve",
                Type = LedgerAccountType.GoalReserve, OwnerUserId = user.Id };
            var goal = new Goal { OwnerUserId = user.Id, Name = "Owned test goal", TargetAmount = 1000,
                LedgerAccount = account, LedgerAccountId = account.Id };
            var bank = new SpendWise.Domain.Banking.BankConnection
            {
                UserId = user.Id,
                Provider = SpendWise.Domain.Banking.BankProvider.SePayBankHub,
                AccountEnvironment = SpendWise.Domain.Banking.SePayAccountEnvironment.TestMode,
                ExternalAccountId = $"ACC_{user.Id:N}",
                BankBrandName = "MBBank",
                AccountHolderName = user.FullName,
                MaskedAccountNumber = "****1234",
                Status = SpendWise.Domain.Banking.BankConnectionStatus.Active,
                LinkedAt = DateTime.UtcNow
            };
            db.AddRange(user, first, second, goal, bank);
            await SqlTestFixtureLifecycle.MeasureAsync(name, "seed-save", () => db.SaveChangesAsync());
            return new(user.Id, goal.Id, first.Id, second.Id);
        }
        public async Task<bool> ObserveWaitAsync(int session, Task pending)
        {
            ValidateOwnership();
            await using var observer = new SqlConnection(connection);
            await observer.OpenAsync();
            await using var command = observer.CreateCommand();
            command.CommandTimeout = 2;
            command.CommandText = "SELECT COUNT(*) FROM sys.dm_tran_locks WHERE request_session_id=@session AND resource_database_id=DB_ID() AND request_status IN ('WAIT','LOW_PRIORITY_WAIT')";
            command.Parameters.AddWithValue("@session", session);
            var deadline = System.Diagnostics.Stopwatch.StartNew();
            while (!pending.IsCompleted && deadline.Elapsed < TimeSpan.FromSeconds(5))
            {
                if (Convert.ToInt32(await command.ExecuteScalarAsync()) > 0) return true;
                await Task.Delay(10);
            }
            return false;
        }
        private void ValidateOwnership()
        {
            SqlTestFixtureLifecycle.ValidateOwnedConnection(connection);
            var parsed = new SqlConnectionStringBuilder(connection);
            if (!Regex.IsMatch(name, @"\ASpendWise_GoalS053_Test_[a-f0-9]{32}\z")
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
