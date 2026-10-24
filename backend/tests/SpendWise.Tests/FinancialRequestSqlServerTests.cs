using System.Data.Common;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features;
using SpendWise.Application.Features.Authentication;
using SpendWise.Application.Features.Evidence;
using SpendWise.Application.Features.RecurringTransactions.Commands.RecordRecurringOccurrence;
using SpendWise.Application.Features.Transactions.Commands;
using SpendWise.Application.Features.Wallets;
using SpendWise.Application.Planning;
using SpendWise.Domain.Admin;
using SpendWise.Domain.Finance;
using SpendWise.Domain.Identity;
using SpendWise.Domain.Planning;
using SpendWise.Infrastructure.Finance;
using SpendWise.Infrastructure.Identity;
using SpendWise.Infrastructure.Persistence;
using Xunit;

namespace SpendWise.Tests;

public sealed class FinancialRequestSqlServerTests : SqlTestFixtureScope
{
    private const string Pin = "123456";

    [Fact]
    public async Task Recurring_another_actor_cannot_record_owned_occurrence_or_mutate_financial_state()
    {
        var database = await OwnAsync(OwnedDatabase.CreateAsync());
        var owner = await database.SeedAsync();
        var actor = await database.SeedAsync();
        await using (var db = database.Context())
            Assert.Equal("recurring_occurrence_not_found", (await Assert.ThrowsAsync<NotFoundException>(() =>
                Handler(db, database.Clock).Handle(Command(owner, "unauthorized", owner.Evidence1)
                    with { UserId = actor.User }, default))).Code);
        await using var verify = database.Context();
        Assert.Empty(await verify.FinancialTransactions.ToListAsync());
        Assert.Empty(await verify.LedgerTransactions.ToListAsync());
        Assert.Empty(await verify.LedgerEntries.ToListAsync());
        Assert.Empty(await verify.AuditLogs.ToListAsync());
        Assert.All(await verify.Wallets.ToListAsync(), x => Assert.Equal(500m, x.AvailableBalance));
        Assert.All(await verify.RecurringOccurrences.ToListAsync(), x =>
        {
            Assert.Equal(RecurringOccurrenceStatus.Due, x.Status);
            Assert.Null(x.RecordedTransactionId);
        });
        Assert.All(await verify.EvidenceAttachments.ToListAsync(), x => Assert.Null(x.FinancialTransactionId));
    }

    [Fact]
    public async Task Recurring_different_users_progress_independently_while_one_occurrence_is_held()
    {
        var database = await OwnAsync(OwnedDatabase.CreateAsync());
        var firstIds = await database.SeedAsync();
        var secondIds = await database.SeedAsync();
        var firstGate = new WalletReadGate();
        var secondGate = new WalletReadGate();
        await using var firstDb = database.Context(firstGate);
        await using var secondDb = database.Context(secondGate);
        var first = Handler(firstDb, database.Clock).Handle(Command(firstIds, "independent", firstIds.Evidence1), default);
        Task<SpendWise.Application.Finance.TransactionDto>? second = null;
        try
        {
            await firstGate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            second = Handler(secondDb, database.Clock).Handle(Command(secondIds, "independent", secondIds.Evidence1), default);
            // A global lock would prevent this signal until first released; no sleep inference.
            await secondGate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
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
        await using var verify = database.Context();
        foreach (var ids in new[] { firstIds, secondIds })
        {
            var financial = Assert.Single(await verify.FinancialTransactions.Where(x => x.OwnerUserId == ids.User).ToListAsync());
            var wallet = await verify.Wallets.SingleAsync(x => x.OwnerUserId == ids.User);
            Assert.Equal(400m, wallet.AvailableBalance);
            Assert.Equal(400m, (await verify.LedgerAccounts.SingleAsync(x => x.WalletId == wallet.Id)).Balance);
            Assert.Single(await verify.LedgerTransactions.Where(x => x.Id == financial.LedgerTransactionId).ToListAsync());
            Assert.Equal(financial.Id, (await verify.RecurringOccurrences.SingleAsync(x => x.Id == ids.Occurrence)).RecordedTransactionId);
            Assert.Single(await verify.AuditLogs.Where(x => x.ActorUserId == ids.User
                && x.Action == "RECORD_RECURRING_OCCURRENCE").ToListAsync());
            Assert.Equal(financial.Id, (await verify.EvidenceAttachments.SingleAsync(x => x.Id == ids.Evidence1)).FinancialTransactionId);
        }
        Assert.Equal(2, await verify.LedgerTransactions.CountAsync());
        Assert.Equal(4, await verify.LedgerEntries.CountAsync());
    }

    [Fact]
    public async Task Recurring_insufficient_balance_preserves_pending_state_and_retry_after_funding_posts_once()
    {
        var database = await OwnAsync(OwnedDatabase.CreateAsync());
        var ids = await database.SeedAsync();
        await using (var seed = database.Context())
        {
            await seed.Wallets.ExecuteUpdateAsync(x => x.SetProperty(w => w.AvailableBalance, 50m));
            await seed.LedgerAccounts.Where(x => !x.IsSystem).ExecuteUpdateAsync(x => x.SetProperty(a => a.Balance, 50m));
        }
        await using (var db = database.Context())
            Assert.Equal("insufficient_balance", (await Assert.ThrowsAsync<ConflictException>(() =>
                Handler(db, database.Clock).Handle(Command(ids, "funding-retry", ids.Evidence1), default))).Code);
        await using (var verify = database.Context())
        {
            Assert.Empty(await verify.FinancialTransactions.ToListAsync());
            Assert.Empty(await verify.LedgerTransactions.ToListAsync());
            Assert.Empty(await verify.LedgerEntries.ToListAsync());
            Assert.Empty(await verify.AuditLogs.ToListAsync());
            Assert.Equal(50m, (await verify.Wallets.SingleAsync()).AvailableBalance);
            Assert.Equal(50m, (await verify.LedgerAccounts.SingleAsync(x => !x.IsSystem)).Balance);
            Assert.Equal(RecurringOccurrenceStatus.Due, (await verify.RecurringOccurrences.SingleAsync()).Status);
            Assert.Null((await verify.RecurringOccurrences.SingleAsync()).RecordedTransactionId);
            Assert.Null((await verify.RecurringTransactions.SingleAsync()).LastRunAt);
            Assert.All(await verify.EvidenceAttachments.ToListAsync(), x => Assert.Null(x.FinancialTransactionId));
            // Controlled fixture funding, not a production database or funding workflow.
            await verify.Wallets.ExecuteUpdateAsync(x => x.SetProperty(w => w.AvailableBalance, 500m));
            await verify.LedgerAccounts.Where(x => !x.IsSystem).ExecuteUpdateAsync(x => x.SetProperty(a => a.Balance, 500m));
        }
        await using (var retry = database.Context())
            await Handler(retry, database.Clock).Handle(Command(ids, "funding-retry", ids.Evidence1), default);
        await AssertSinglePosting(database, ids);
    }

    [Fact]
    public async Task Recurring_overlapping_fresh_requests_with_different_keys_post_only_once()
    {
        var database = await OwnAsync(OwnedDatabase.CreateAsync());
        var ids = await database.SeedAsync();
        var winnerGate = new CreatorAdmissionGate();
        var contenderGate = new CreatorAdmissionGate();
        await using var winner = database.Context(winnerGate);
        await using var contender = database.Context(contenderGate);
        await contender.Database.OpenConnectionAsync();
        var contenderSession = ((SqlConnection)contender.Database.GetDbConnection()).ServerProcessId;
        var first = Handler(winner, database.Clock).Handle(Command(ids, "winner", ids.Evidence1), default);
        Task<SpendWise.Application.Finance.TransactionDto>? second = null;
        try
        {
            await winnerGate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            second = Handler(contender, database.Clock).Handle(Command(ids, "contender", ids.Evidence2), default);
            // Before the repair both fresh requests read Due, then reach creator admission.
            // With serialization the second instead waits on the occurrence row lock.
            var observed = await Task.WhenAny(contenderGate.Entered.Task,
                database.ObserveLockWaitAsync(contenderSession, second)).WaitAsync(TimeSpan.FromSeconds(10));
            if (observed != contenderGate.Entered.Task)
                Assert.True(await (Task<bool>)observed, "Contender neither reached creator admission nor waited on a SQL lock.");
            winnerGate.Release.TrySetResult();
            await first.WaitAsync(TimeSpan.FromSeconds(10));
            // If the contender already read Due, its wallet/ledger reads now see committed balances.
            contenderGate.Release.TrySetResult();
            var error = await Record.ExceptionAsync(() => second.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.Equal("idempotency_conflict", Assert.IsType<ConflictException>(error).Code);
            await AssertSinglePosting(database, ids);
        }
        finally
        {
            winnerGate.Release.TrySetResult();
            contenderGate.Release.TrySetResult();
            await Record.ExceptionAsync(() => first);
            if (second is not null) await Record.ExceptionAsync(() => second);
        }
    }

    [Fact]
    public async Task Recurring_stale_pending_request_with_different_key_cannot_post_a_second_transaction()
    {
        var database = await OwnAsync(OwnedDatabase.CreateAsync());
        var ids = await database.SeedAsync();
        await using var stale = database.Context();
        var pending = await stale.RecurringOccurrences.Include(x => x.RecurringTransaction)
            .SingleAsync(x => x.Id == ids.Occurrence);
        Assert.Equal(RecurringOccurrenceStatus.Due, pending.Status);
        await using (var winner = database.Context())
            await Handler(winner, database.Clock).Handle(Command(ids, "first", ids.Evidence1), default);
        // The second request has a genuinely stale tracked read, but loads wallet/ledger
        // only after the first commit. Row-version balance protection cannot mask this race.
        Assert.Equal(RecurringOccurrenceStatus.Due, pending.Status);
        var error = await Record.ExceptionAsync(() =>
            Handler(stale, database.Clock).Handle(Command(ids, "second", ids.Evidence2), default));
        Assert.Equal("idempotency_conflict", Assert.IsType<ConflictException>(error).Code);
        await AssertSinglePosting(database, ids);
    }

    [Fact]
    public async Task Recurring_same_key_replays_and_changed_key_conflicts_without_extra_posting()
    {
        var database = await OwnAsync(OwnedDatabase.CreateAsync());
        var ids = await database.SeedAsync();
        Guid first;
        await using (var db = database.Context())
            first = (await Handler(db, database.Clock).Handle(Command(ids, "retry", ids.Evidence1), default)).Id;
        await using (var db = database.Context())
            Assert.Equal(first, (await Handler(db, database.Clock).Handle(Command(ids, "retry", ids.Evidence1), default)).Id);
        await using (var db = database.Context())
            Assert.Equal("idempotency_conflict", (await Assert.ThrowsAsync<ConflictException>(() =>
                Handler(db, database.Clock).Handle(Command(ids, "changed", ids.Evidence2), default))).Code);
        await AssertSinglePosting(database, ids);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Recurring_final_save_failure_or_cancellation_rolls_back_posting_and_can_retry(bool cancel)
    {
        var database = await OwnAsync(OwnedDatabase.CreateAsync());
        var ids = await database.SeedAsync();
        using var cancellation = new CancellationTokenSource();
        await using (var failing = database.Context(new FailFinalSave(cancellation, cancel)))
        {
            var error = await Record.ExceptionAsync(() => Handler(failing, database.Clock)
                .Handle(Command(ids, "rollback", ids.Evidence1), cancellation.Token));
            if (cancel) Assert.IsAssignableFrom<OperationCanceledException>(error);
            else Assert.IsType<InvalidOperationException>(error);
        }
        await using (var verify = database.Context())
        {
            Assert.Empty(await verify.LedgerTransactions.ToListAsync());
            Assert.Empty(await verify.LedgerEntries.ToListAsync());
            Assert.Empty(await verify.FinancialTransactions.ToListAsync());
            Assert.Empty(await verify.AuditLogs.ToListAsync());
            Assert.Equal(500m, (await verify.Wallets.SingleAsync()).AvailableBalance);
            Assert.Equal(500m, (await verify.LedgerAccounts.SingleAsync(x => !x.IsSystem)).Balance);
            Assert.Equal(RecurringOccurrenceStatus.Due, (await verify.RecurringOccurrences.SingleAsync()).Status);
            Assert.Null((await verify.RecurringOccurrences.SingleAsync()).RecordedTransactionId);
            Assert.Null((await verify.RecurringTransactions.SingleAsync()).LastRunAt);
            Assert.All(await verify.EvidenceAttachments.ToListAsync(), x => Assert.Null(x.FinancialTransactionId));
        }
        await using (var retry = database.Context())
            await Handler(retry, database.Clock).Handle(Command(ids, "rollback", ids.Evidence1), default);
        await AssertSinglePosting(database, ids);
    }

    private static RecordRecurringOccurrenceCommand Command(Ids ids, string key, Guid evidence) =>
        new(ids.User, ids.Recurring, ids.Occurrence, new RecordRecurringOccurrenceRequest(Pin, [evidence], key));

    private static RecordRecurringOccurrenceCommandHandler Handler(SpendWiseDbContext db, IClock clock)
    {
        var security = new FinancialSecurityGuard(db, new SecurityPinService(), new AccountCredentialLockout(db, clock));
        return new(db, new ManualTransactionCreator(db, new LedgerService(db, clock), clock,
            security, new EvidenceSupport(db), new SpendWise.Application.Features.Banking.ActiveBankGuard(db)), security, clock);
    }

    private static async Task AssertSinglePosting(OwnedDatabase database, Ids ids)
    {
        await using var db = database.Context();
        var financial = Assert.Single(await db.FinancialTransactions.ToListAsync());
        var ledger = Assert.Single(await db.LedgerTransactions.Include(x => x.Entries).ToListAsync());
        Assert.Equal(2, ledger.Entries.Count);
        Assert.Equal(100m, ledger.Entries.Where(x => x.Direction == LedgerEntryDirection.Debit).Sum(x => x.Amount));
        Assert.Equal(100m, ledger.Entries.Where(x => x.Direction == LedgerEntryDirection.Credit).Sum(x => x.Amount));
        Assert.Equal(400m, (await db.Wallets.SingleAsync()).AvailableBalance);
        Assert.Equal(400m, (await db.LedgerAccounts.SingleAsync(x => !x.IsSystem)).Balance);
        var occurrence = await db.RecurringOccurrences.SingleAsync();
        Assert.Equal(RecurringOccurrenceStatus.Recorded, occurrence.Status);
        Assert.Equal(financial.Id, occurrence.RecordedTransactionId);
        Assert.Equal(ledger.Id, financial.LedgerTransactionId);
        Assert.Single(await db.AuditLogs.Where(x => x.Action == "RECORD_RECURRING_OCCURRENCE").ToListAsync());
        Assert.Null((await db.EvidenceAttachments.SingleAsync(x => x.Id == ids.Evidence2)).FinancialTransactionId);
    }

    private sealed class FailFinalSave(CancellationTokenSource cancellation, bool cancel) : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<AuditLog>()
                .Any(x => x.State == EntityState.Added && x.Entity.Action == "RECORD_RECURRING_OCCURRENCE"))
            {
                if (!cancel) throw new InvalidOperationException("Controlled test-only final save failure.");
                cancellation.Cancel();
                throw new OperationCanceledException(cancellation.Token);
            }
            return ValueTask.FromResult(result);
        }
    }

    private sealed class WalletReadGate : DbCommandInterceptor
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int gated;
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("FROM [Wallets]", StringComparison.Ordinal)
                && Interlocked.Exchange(ref gated, 1) == 0)
            {
                Entered.TrySetResult();
                await Release.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
            }
            return result;
        }
    }

    private sealed class CreatorAdmissionGate : DbCommandInterceptor
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool occurrenceRead;
        private int gated;
        public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command,
            CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("FROM [RecurringOccurrences]", StringComparison.Ordinal))
                occurrenceRead = true;
            return ValueTask.FromResult(result);
        }
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            // This query occurs after occurrence materialization/disposal, but before
            // creator PIN reset writes Users and acquires its incidental transaction lock.
            if (occurrenceRead && command.CommandText.Contains("FROM [Users]", StringComparison.Ordinal)
                && Interlocked.Exchange(ref gated, 1) == 0)
            {
                Entered.TrySetResult();
                await Release.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
            }
            return result;
        }
    }

    private sealed record Ids(Guid User, Guid Recurring, Guid Occurrence, Guid Evidence1, Guid Evidence2);
    private sealed class FixedClock : IClock
    {
        public DateTime UtcNow => new(2026, 10, 12, 2, 0, 0, DateTimeKind.Utc);
    }

    private sealed class OwnedDatabase : IAsyncDisposable
    {
        private readonly string name = "SpendWise_FinancialS05_Test_" + Guid.NewGuid().ToString("N");
        private readonly string connection;
        private bool ownsCatalog;
        public IClock Clock { get; } = new FixedClock();
        private OwnedDatabase() => connection = SqlTestFixtureLifecycle.CreateOwnedConnectionString(name,
            Environment.GetEnvironmentVariable(SqlTestFixtureLifecycle.InstanceEnvironmentVariable));
        public SpendWiseDbContext Context(params IInterceptor[] interceptors) => new(
            new DbContextOptionsBuilder<SpendWiseDbContext>().UseSqlServer(connection)
                .AddInterceptors(interceptors).Options, Clock);
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
        public async Task<bool> ObserveLockWaitAsync(int session, Task pending)
        {
            ValidateOwnership();
            await using var observer = new SqlConnection(connection);
            await observer.OpenAsync();
            await using var command = observer.CreateCommand();
            command.CommandTimeout = 2;
            command.CommandText = """
                SELECT COUNT(*) FROM sys.dm_tran_locks
                WHERE request_session_id = @session AND resource_database_id = DB_ID()
                  AND request_status IN ('WAIT', 'LOW_PRIORITY_WAIT')
                """;
            command.Parameters.AddWithValue("@session", session);
            var deadline = System.Diagnostics.Stopwatch.StartNew();
            while (!pending.IsCompleted && deadline.Elapsed < TimeSpan.FromSeconds(5))
            {
                if (Convert.ToInt32(await command.ExecuteScalarAsync()) > 0) return true;
                // Poll actual SQL lock state; no conclusion depends on a fixed sleep interval.
                await Task.Delay(10);
            }
            return false;
        }
        public async Task<Ids> SeedAsync()
        {
            await using var db = Context();
            var unique = Guid.NewGuid().ToString("N");
            var user = new User { FullName = "Isolated financial test", Email = unique + "@test.local",
                NormalizedEmail = (unique + "@test.local").ToUpperInvariant(), SpendId = unique,
                NormalizedSpendId = unique.ToUpperInvariant(), PasswordHash = "test-only-hash",
                SecurityPinHash = new SecurityPinService().Hash(Pin) };
            var wallet = WalletFactory.Create(user.Id, "Owned test wallet", WalletType.Main, true);
            wallet.AvailableBalance = 500;
            wallet.LedgerAccount.Balance = 500;
            var category = new Category { OwnerUserId = user.Id, Key = "test:" + unique,
                Kind = CategoryKind.Expense, Name = "Test expense", IsActive = true };
            var recurring = new RecurringTransaction { OwnerUserId = user.Id, WalletId = wallet.Id,
                CategoryId = category.Id, Kind = CategoryKind.Expense, Amount = 100,
                Description = "Owned recurring test", Frequency = RecurringFrequency.Monthly,
                NextRunAt = Clock.UtcNow.AddMonths(1), AnchorDay = 12, AnchorMonth = 10 };
            var occurrence = new RecurringOccurrence { RecurringTransaction = recurring,
                RecurringTransactionId = recurring.Id, ScheduledFor = Clock.UtcNow.AddMinutes(-5) };
            var evidence = new EvidenceSupport(db);
            byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1];
            var first = evidence.Create(user.Id, new("first.png", "image/png", png));
            var second = evidence.Create(user.Id, new("second.png", "image/png", [.. png, 2]));
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
            db.AddRange(user, wallet, category, recurring, occurrence, first, second, bank);
            await SqlTestFixtureLifecycle.MeasureAsync(name, "seed-save", () => db.SaveChangesAsync());
            return new(user.Id, recurring.Id, occurrence.Id, first.Id, second.Id);
        }
        private void ValidateOwnership()
        {
            SqlTestFixtureLifecycle.ValidateOwnedConnection(connection);
            var parsed = new SqlConnectionStringBuilder(connection);
            if (!Regex.IsMatch(name, @"\ASpendWise_FinancialS05_Test_[a-f0-9]{32}\z")
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
