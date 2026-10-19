using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using SpendWise.Application.Banking;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.BankConnections;
using SpendWise.Application.Features.Transactions.Queries.GetTransactions;
using SpendWise.Application.Features.Wallets;
using SpendWise.Application.Finance;
using SpendWise.Domain.Banking;
using SpendWise.Domain.Finance;
using SpendWise.Domain.Identity;
using SpendWise.Infrastructure.Finance;
using SpendWise.Infrastructure.Persistence;
using SpendWise.API.Infrastructure;
using Xunit;

namespace SpendWise.Tests;

public sealed class SePayTestModeBankingTests
{
    private static readonly Guid ProviderAccountId = Guid.Parse("f9e8d7c6-b5a4-3210-fedc-ba0987654321");

    [Fact]
    public void Test_mode_self_link_and_top_up_errors_have_vietnamese_messages()
    {
        var originalCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("vi-VN");
            foreach (var code in new[]
            {
                "bank_link_challenge_not_found", "bank_link_challenge_unavailable",
                "bank_link_challenge_expired", "bank_link_not_confirmed", "bank_link_ambiguous",
                "bank_account_already_linked", "bank_connection_invalid", "sepay_account_not_found",
                "sepay_account_inactive", "sepay_insufficient_balance", "wallet_top_up_not_found",
                "wallet_top_up_unavailable", "wallet_top_up_expired", "wallet_top_up_pending",
                "wallet_top_up_ambiguous", "wallet_top_up_in_progress", "wallet_top_up_active_exists",
                "provider_transaction_already_used"
            })
            {
                Assert.NotEqual("fallback", ApiErrorLocalizer.Localize(code, "fallback"));
            }
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalCulture;
        }
    }

    [Fact]
    public async Task Self_link_uses_transaction_account_identity_and_is_idempotent()
    {
        await using var fixture = await BankingFixture.CreateAsync();
        var user = await fixture.AddUserAsync();
        var start = new StartTestModeBankLinkCommandHandler(fixture.Db, fixture.Clock);
        var challenge = await start.Handle(new(true, user.Id), default);
        fixture.Provider.Transactions.Add(fixture.Transaction(challenge.Reference, 1));
        var confirm = new ConfirmTestModeBankLinkCommandHandler(fixture.Db, fixture.Provider, fixture.Clock);

        var first = await confirm.Handle(new(true, user.Id, challenge.ChallengeId), default);
        var replay = await confirm.Handle(new(true, user.Id, challenge.ChallengeId), default);

        Assert.Equal(first.Id, replay.Id);
        Assert.Equal("****6789", first.MaskedAccountNumber);
        Assert.Equal("TestMode", first.AccountEnvironment);
        var stored = Assert.Single(await fixture.Db.BankConnections.ToListAsync());
        Assert.Equal(ProviderAccountId.ToString(), stored.ExternalAccountId);
        Assert.Equal(SePayAccountEnvironment.TestMode, stored.AccountEnvironment);
    }

    [Fact]
    public async Task Link_challenge_rejects_cross_user_inactive_and_duplicate_account()
    {
        await using var fixture = await BankingFixture.CreateAsync();
        var firstUser = await fixture.AddUserAsync();
        var secondUser = await fixture.AddUserAsync();
        var start = new StartTestModeBankLinkCommandHandler(fixture.Db, fixture.Clock);
        var firstChallenge = await start.Handle(new(true, firstUser.Id), default);
        fixture.Provider.Transactions.Add(fixture.Transaction(firstChallenge.Reference, 1));
        var confirm = new ConfirmTestModeBankLinkCommandHandler(fixture.Db, fixture.Provider, fixture.Clock);
        await Assert.ThrowsAsync<NotFoundException>(() => confirm.Handle(
            new(true, secondUser.Id, firstChallenge.ChallengeId), default));
        await confirm.Handle(new(true, firstUser.Id, firstChallenge.ChallengeId), default);

        var secondChallenge = await start.Handle(new(true, secondUser.Id), default);
        fixture.Provider.Transactions.Clear();
        fixture.Provider.Transactions.Add(fixture.Transaction(secondChallenge.Reference, 2));
        var conflict = await Assert.ThrowsAsync<ConflictException>(() => confirm.Handle(
            new(true, secondUser.Id, secondChallenge.ChallengeId), default));
        Assert.Equal("bank_account_already_linked", conflict.Code);

        fixture.Provider.Accounts[0] = fixture.Provider.Accounts[0] with { Status = "Inactive" };
        var thirdChallenge = await start.Handle(new(true, secondUser.Id), default);
        fixture.Provider.Transactions.Clear();
        fixture.Provider.Transactions.Add(fixture.Transaction(thirdChallenge.Reference, 3));
        var inactive = await Assert.ThrowsAsync<ConflictException>(() => confirm.Handle(
            new(true, secondUser.Id, thirdChallenge.ChallengeId), default));
        Assert.Equal("sepay_account_inactive", inactive.Code);
    }

    [Fact]
    public async Task Pending_top_up_changes_no_balance_and_exact_settlement_posts_once()
    {
        await using var fixture = await BankingFixture.CreateAsync();
        var user = await fixture.AddUserAsync();
        var wallet = await fixture.AddWalletAsync(user.Id);
        var connection = await fixture.AddConnectionAsync(user.Id);
        var create = new CreateTestModeWalletTopUpCommandHandler(fixture.Db, fixture.Provider, fixture.Clock);

        var pending = await create.Handle(new(true, user.Id,
            new(connection.Id, wallet.Id, 125000)), default);
        Assert.Equal("Pending", pending.Status);
        Assert.Equal(0m, (await fixture.Db.Wallets.FindAsync(wallet.Id))!.AvailableBalance);
        Assert.Empty(await fixture.Db.LedgerTransactions.ToListAsync());

        fixture.Provider.Transactions.Add(fixture.Transaction(pending.Reference, 10, 125000));
        var confirm = new ConfirmTestModeWalletTopUpCommandHandler(
            fixture.Db, fixture.Provider, new LedgerService(fixture.Db, fixture.Clock), fixture.Clock);
        var completed = await confirm.Handle(new(true, user.Id, pending.Id), default);
        var replay = await confirm.Handle(new(true, user.Id, pending.Id), default);

        Assert.Equal("Completed", completed.Status);
        Assert.Equal(completed.FinancialTransactionId, replay.FinancialTransactionId);
        var refreshedWallet = await fixture.Db.Wallets.Include(item => item.LedgerAccount)
            .SingleAsync(item => item.Id == wallet.Id);
        Assert.Equal(125000m, refreshedWallet.AvailableBalance);
        Assert.Equal(refreshedWallet.AvailableBalance, refreshedWallet.LedgerAccount.Balance);
        var ledger = Assert.Single(await fixture.Db.LedgerTransactions.Include(item => item.Entries).ToListAsync());
        Assert.Equal(ledger.Entries.Where(item => item.Direction == LedgerEntryDirection.Debit).Sum(item => item.Amount),
            ledger.Entries.Where(item => item.Direction == LedgerEntryDirection.Credit).Sum(item => item.Amount));
        var history = Assert.Single(await fixture.Db.FinancialTransactions.ToListAsync());
        Assert.Equal(FinancialTransactionType.BankTopUp, history.Type);
        Assert.False(history.IsManual);
    }

    [Fact]
    public async Task Transaction_history_maps_top_up_and_existing_direction_signs()
    {
        await using var fixture = await BankingFixture.CreateAsync();
        var user = await fixture.AddUserAsync();
        var transactions = new[]
        {
            Transaction(user.Id, FinancialTransactionType.BankTopUp, 50_000, "top-up"),
            Transaction(user.Id, FinancialTransactionType.Income, 20_000, "income"),
            Transaction(user.Id, FinancialTransactionType.Expense, 10_000, "expense")
        };
        fixture.Db.FinancialTransactions.AddRange(transactions);
        await fixture.Db.SaveChangesAsync();

        var page = await new GetTransactionsQueryHandler(fixture.Db).Handle(
            new GetTransactionsQuery(user.Id, new TransactionQuery(PageSize: 100)),
            default);

        Assert.Equal(50_000m, page.Items.Single(x => x.Type == "BankTopUp").SignedAmount);
        Assert.Equal(20_000m, page.Items.Single(x => x.Type == "Income").SignedAmount);
        Assert.Equal(-10_000m, page.Items.Single(x => x.Type == "Expense").SignedAmount);
    }

    [Fact]
    public async Task Top_up_validates_fresh_balance_ownership_expiry_and_exact_reference()
    {
        await using var fixture = await BankingFixture.CreateAsync();
        var owner = await fixture.AddUserAsync();
        var other = await fixture.AddUserAsync();
        var wallet = await fixture.AddWalletAsync(owner.Id);
        var connection = await fixture.AddConnectionAsync(owner.Id);
        var create = new CreateTestModeWalletTopUpCommandHandler(fixture.Db, fixture.Provider, fixture.Clock);
        fixture.Provider.Accounts[0] = fixture.Provider.Accounts[0] with { AccumulatedBalance = 99m };
        var insufficient = await Assert.ThrowsAsync<ConflictException>(() => create.Handle(new(true, owner.Id,
            new(connection.Id, wallet.Id, 100)), default));
        Assert.Equal("sepay_insufficient_balance", insufficient.Code);
        await Assert.ThrowsAsync<NotFoundException>(() => create.Handle(new(true, other.Id,
            new(connection.Id, wallet.Id, 50)), default));

        fixture.Provider.Accounts[0] = fixture.Provider.Accounts[0] with { AccumulatedBalance = 1_000_000m };
        var pending = await create.Handle(new(true, owner.Id, new(connection.Id, wallet.Id, 100)), default);
        fixture.Provider.Transactions.Add(fixture.Transaction("WRONG" + pending.Reference, 20, 100));
        var confirm = new ConfirmTestModeWalletTopUpCommandHandler(
            fixture.Db, fixture.Provider, new LedgerService(fixture.Db, fixture.Clock), fixture.Clock);
        var missing = await Assert.ThrowsAsync<ConflictException>(() => confirm.Handle(
            new(true, owner.Id, pending.Id), default));
        Assert.Equal("wallet_top_up_pending", missing.Code);
        Assert.Equal(0m, wallet.AvailableBalance);

        var stored = await fixture.Db.WalletTopUps.FindAsync(pending.Id);
        stored!.ExpiresAt = fixture.Clock.UtcNow.AddSeconds(-1);
        await fixture.Db.SaveChangesAsync();
        var expired = await Assert.ThrowsAsync<ConflictException>(() => confirm.Handle(
            new(true, owner.Id, pending.Id), default));
        Assert.Equal("wallet_top_up_expired", expired.Code);
    }

    private static FinancialTransaction Transaction(
        Guid ownerId,
        FinancialTransactionType type,
        decimal amount,
        string reference)
    {
        var ledger = new LedgerTransaction
        {
            Reference = reference,
            BusinessType = "Test",
            BusinessId = Guid.NewGuid(),
            Status = LedgerTransactionStatus.Completed,
            CompletedAt = DateTime.UtcNow
        };
        return new FinancialTransaction
        {
            OwnerUserId = ownerId,
            LedgerTransaction = ledger,
            LedgerTransactionId = ledger.Id,
            Type = type,
            Amount = amount,
            Description = reference,
            OccurredAt = DateTime.UtcNow
        };
    }

    [Fact]
    public async Task Active_top_up_blocks_another_request_but_expired_pending_does_not()
    {
        await using var fixture = await BankingFixture.CreateAsync();
        var user = await fixture.AddUserAsync();
        var wallet = await fixture.AddWalletAsync(user.Id);
        var connection = await fixture.AddConnectionAsync(user.Id);
        var handler = new CreateTestModeWalletTopUpCommandHandler(fixture.Db, fixture.Provider, fixture.Clock);

        var first = await handler.Handle(new(true, user.Id, new(connection.Id, wallet.Id, 100)), default);
        var conflict = await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(
            new(true, user.Id, new(connection.Id, wallet.Id, 200)), default));
        Assert.Equal("wallet_top_up_active_exists", conflict.Code);

        var stored = await fixture.Db.WalletTopUps.SingleAsync(item => item.Id == first.Id);
        stored.ExpiresAt = fixture.Clock.UtcNow.AddSeconds(-1);
        await fixture.Db.SaveChangesAsync();
        var replacement = await handler.Handle(new(true, user.Id, new(connection.Id, wallet.Id, 200)), default);

        fixture.Db.ChangeTracker.Clear();
        Assert.Equal(WalletTopUpStatus.Expired,
            (await fixture.Db.WalletTopUps.SingleAsync(item => item.Id == first.Id)).Status);
        Assert.Equal(WalletTopUpStatus.Pending,
            (await fixture.Db.WalletTopUps.SingleAsync(item => item.Id == replacement.Id)).Status);
    }

    [Fact]
    public async Task Test_mode_reads_are_development_only_owned_and_use_fresh_provider_balance()
    {
        await using var fixture = await BankingFixture.CreateAsync();
        var owner = await fixture.AddUserAsync();
        var other = await fixture.AddUserAsync();
        var wallet = await fixture.AddWalletAsync(owner.Id);
        var connection = await fixture.AddConnectionAsync(owner.Id);
        var pending = await new CreateTestModeWalletTopUpCommandHandler(
            fixture.Db, fixture.Provider, fixture.Clock).Handle(
                new(true, owner.Id, new(connection.Id, wallet.Id, 100)), default);
        fixture.Provider.Accounts[0] = fixture.Provider.Accounts[0] with { AccumulatedBalance = 777_777m };

        var linkedHandler = new GetTestModeLinkedAccountsQueryHandler(fixture.Db, fixture.Provider);
        var linked = Assert.Single(await linkedHandler.Handle(new(true, owner.Id), default));
        Assert.Equal(connection.Id, linked.BankConnectionId);
        Assert.Equal(777_777m, linked.AccumulatedBalance);
        Assert.Empty(await linkedHandler.Handle(new(true, other.Id), default));
        await Assert.ThrowsAsync<NotFoundException>(() => linkedHandler.Handle(new(false, owner.Id), default));

        var topUpsHandler = new GetTestModeWalletTopUpsQueryHandler(fixture.Db, fixture.Clock);
        var ownTopUp = Assert.Single(await topUpsHandler.Handle(new(true, owner.Id), default));
        Assert.Equal(pending.Id, ownTopUp.Id);
        Assert.Empty(await topUpsHandler.Handle(new(true, other.Id), default));
    }

    [Fact]
    public async Task Test_mode_commands_are_unavailable_outside_development()
    {
        await using var fixture = await BankingFixture.CreateAsync();
        var user = await fixture.AddUserAsync();
        var error = await Assert.ThrowsAsync<NotFoundException>(() =>
            new StartTestModeBankLinkCommandHandler(fixture.Db, fixture.Clock).Handle(new(false, user.Id), default));
        Assert.Equal("sepay_test_mode_unavailable", error.Code);
        Assert.Equal(0, fixture.Provider.Calls);
    }

    [Fact]
    public async Task Posting_failure_rolls_back_claim_and_all_financial_changes()
    {
        await using var fixture = await BankingFixture.CreateAsync();
        var user = await fixture.AddUserAsync();
        var wallet = await fixture.AddWalletAsync(user.Id);
        var connection = await fixture.AddConnectionAsync(user.Id);
        var pending = await new CreateTestModeWalletTopUpCommandHandler(fixture.Db, fixture.Provider, fixture.Clock)
            .Handle(new(true, user.Id, new(connection.Id, wallet.Id, 100)), default);
        fixture.Provider.Transactions.Add(fixture.Transaction(pending.Reference, 30, 100));

        var handler = new ConfirmTestModeWalletTopUpCommandHandler(
            fixture.Db, fixture.Provider, new FailingLedger(), fixture.Clock);
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(
            new(true, user.Id, pending.Id), default));

        fixture.Db.ChangeTracker.Clear();
        Assert.Equal(WalletTopUpStatus.Pending, (await fixture.Db.WalletTopUps.FindAsync(pending.Id))!.Status);
        Assert.Equal(0m, (await fixture.Db.Wallets.FindAsync(wallet.Id))!.AvailableBalance);
        Assert.Empty(await fixture.Db.LedgerTransactions.ToListAsync());
        Assert.Empty(await fixture.Db.FinancialTransactions.ToListAsync());
    }

    [Fact]
    public async Task Concurrent_confirmation_credits_the_wallet_exactly_once()
    {
        var path = Path.Combine(Path.GetTempPath(), $"spendwise-topup-{Guid.NewGuid():N}.db");
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Pooling = false,
            DefaultTimeout = 30
        }.ToString();
        var clock = new FixedClock(new DateTime(2026, 9, 26, 2, 0, 0, DateTimeKind.Utc));
        Guid userId;
        Guid walletId;
        Guid topUpId;
        var reference = "SWTP" + new string('A', 24);
        try
        {
            await using (var seed = CreateContext(connectionString, clock))
            {
                await seed.Database.EnsureCreatedAsync();
                var key = Guid.NewGuid().ToString("N");
                var user = new User
                {
                    FullName = "Concurrent User",
                    Email = key + "@test.local",
                    NormalizedEmail = (key + "@test.local").ToUpperInvariant(),
                    SpendId = key,
                    NormalizedSpendId = key.ToUpperInvariant(),
                    PasswordHash = "hash"
                };
                var wallet = WalletFactory.Create(user.Id, "Main", WalletType.Main, true);
                var bank = new BankConnection
                {
                    UserId = user.Id,
                    Provider = BankProvider.SePayBankHub,
                    AccountEnvironment = SePayAccountEnvironment.TestMode,
                    ExternalAccountId = ProviderAccountId.ToString(),
                    BankBrandName = "TPBank",
                    MaskedAccountNumber = "****6789",
                    Status = BankConnectionStatus.Active
                };
                var topUp = new WalletTopUp
                {
                    UserId = user.Id,
                    BankConnectionId = bank.Id,
                    WalletId = wallet.Id,
                    Amount = 200,
                    Reference = reference,
                    ExpiresAt = clock.UtcNow.AddMinutes(10)
                };
                seed.AddRange(user, wallet, bank, topUp);
                await seed.SaveChangesAsync();
                userId = user.Id;
                walletId = wallet.Id;
                topUpId = topUp.Id;
            }

            var provider = new FakeProvider();
            provider.Accounts.Add(new(ProviderAccountId, "TPBank", "****6789", "personal", "Active", 1000));
            provider.Transactions.Add(new(
                Guid.NewGuid(), ProviderAccountId, "out", 0, 200, 800,
                "Demo " + reference, null, reference, clock.UtcNow));
            await using var firstDb = CreateContext(connectionString, clock);
            await using var secondDb = CreateContext(connectionString, clock);
            var first = new ConfirmTestModeWalletTopUpCommandHandler(
                firstDb, provider, new LedgerService(firstDb, clock), clock);
            var second = new ConfirmTestModeWalletTopUpCommandHandler(
                secondDb, provider, new LedgerService(secondDb, clock), clock);

            var outcomes = await Task.WhenAll(
                CaptureAsync(() => first.Handle(new(true, userId, topUpId), default)),
                CaptureAsync(() => second.Handle(new(true, userId, topUpId), default)));
            Assert.Contains(outcomes, outcome => outcome.Result?.Status == "Completed");

            await using var verify = CreateContext(connectionString, clock);
            Assert.Equal(200m, (await verify.Wallets.FindAsync(walletId))!.AvailableBalance);
            Assert.Single(await verify.LedgerTransactions.ToListAsync());
            Assert.Single(await verify.FinancialTransactions.ToListAsync());
            Assert.Equal(WalletTopUpStatus.Completed, (await verify.WalletTopUps.FindAsync(topUpId))!.Status);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task Concurrent_creation_cannot_leave_two_active_top_ups_for_one_connection()
    {
        var path = Path.Combine(Path.GetTempPath(), $"spendwise-topup-create-{Guid.NewGuid():N}.db");
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Pooling = false,
            DefaultTimeout = 30
        }.ToString();
        var clock = new FixedClock(new DateTime(2026, 9, 26, 2, 0, 0, DateTimeKind.Utc));
        Guid userId;
        Guid walletId;
        Guid connectionId;
        try
        {
            await using (var seed = CreateContext(connectionString, clock))
            {
                await seed.Database.EnsureCreatedAsync();
                var key = Guid.NewGuid().ToString("N");
                var user = new User
                {
                    FullName = "Concurrent User",
                    Email = key + "@test.local",
                    NormalizedEmail = (key + "@test.local").ToUpperInvariant(),
                    SpendId = key,
                    NormalizedSpendId = key.ToUpperInvariant(),
                    PasswordHash = "hash"
                };
                var wallet = WalletFactory.Create(user.Id, "Main", WalletType.Main, true);
                var bank = new BankConnection
                {
                    UserId = user.Id,
                    Provider = BankProvider.SePayBankHub,
                    AccountEnvironment = SePayAccountEnvironment.TestMode,
                    ExternalAccountId = ProviderAccountId.ToString(),
                    BankBrandName = "TPBank",
                    MaskedAccountNumber = "****6789",
                    Status = BankConnectionStatus.Active
                };
                seed.AddRange(user, wallet, bank);
                await seed.SaveChangesAsync();
                userId = user.Id;
                walletId = wallet.Id;
                connectionId = bank.Id;
            }

            var provider = new FakeProvider();
            provider.Accounts.Add(new(ProviderAccountId, "TPBank", "****6789", "personal", "Active", 1000));
            await using var firstDb = CreateContext(connectionString, clock);
            await using var secondDb = CreateContext(connectionString, clock);
            var first = new CreateTestModeWalletTopUpCommandHandler(firstDb, provider, clock);
            var second = new CreateTestModeWalletTopUpCommandHandler(secondDb, provider, clock);

            var outcomes = await Task.WhenAll(
                CaptureAsync(() => first.Handle(new(true, userId, new(connectionId, walletId, 100)), default)),
                CaptureAsync(() => second.Handle(new(true, userId, new(connectionId, walletId, 100)), default)));

            Assert.Single(outcomes, outcome => outcome.Result?.Status == "Pending");
            Assert.Single(outcomes, outcome => outcome.Error is ConflictException error
                && error.Code == "wallet_top_up_active_exists");
            await using var verify = CreateContext(connectionString, clock);
            Assert.Single(await verify.WalletTopUps.Where(item =>
                item.Status == WalletTopUpStatus.Pending || item.Status == WalletTopUpStatus.Processing).ToListAsync());
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static SpendWiseDbContext CreateContext(string connectionString, IClock clock) => new(
        new DbContextOptionsBuilder<SpendWiseDbContext>().UseSqlite(connectionString).Options,
        clock);

    private static async Task<(WalletTopUpDto? Result, Exception? Error)> CaptureAsync(
        Func<Task<WalletTopUpDto>> action)
    {
        try { return (await action(), null); }
        catch (Exception error) { return (null, error); }
    }

    private sealed class BankingFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private BankingFixture(SqliteConnection connection, SpendWiseDbContext db)
        {
            this.connection = connection;
            Db = db;
            Clock = new FixedClock(new DateTime(2026, 9, 26, 2, 0, 0, DateTimeKind.Utc));
            Provider = new FakeProvider();
            Provider.Accounts.Add(new(ProviderAccountId, "TPBank", "****6789", "personal", "Active", 1_000_000m));
        }

        public SpendWiseDbContext Db { get; }
        public FixedClock Clock { get; }
        public FakeProvider Provider { get; }

        public static async Task<BankingFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var clock = new FixedClock(new DateTime(2026, 9, 26, 2, 0, 0, DateTimeKind.Utc));
            var db = new SpendWiseDbContext(new DbContextOptionsBuilder<SpendWiseDbContext>()
                .UseSqlite(connection).Options, clock);
            await db.Database.EnsureCreatedAsync();
            return new BankingFixture(connection, db);
        }

        public async Task<User> AddUserAsync()
        {
            var id = Guid.NewGuid().ToString("N");
            var user = new User
            {
                FullName = "Test User",
                Email = id + "@test.local",
                NormalizedEmail = (id + "@test.local").ToUpperInvariant(),
                SpendId = id,
                NormalizedSpendId = id.ToUpperInvariant(),
                PasswordHash = "hash"
            };
            Db.Users.Add(user);
            await Db.SaveChangesAsync();
            return user;
        }

        public async Task<Wallet> AddWalletAsync(Guid userId)
        {
            var wallet = WalletFactory.Create(userId, "Main wallet", WalletType.Main, true);
            Db.Wallets.Add(wallet);
            await Db.SaveChangesAsync();
            return wallet;
        }

        public async Task<BankConnection> AddConnectionAsync(Guid userId)
        {
            var connection = new BankConnection
            {
                UserId = userId,
                Provider = BankProvider.SePayBankHub,
                AccountEnvironment = SePayAccountEnvironment.TestMode,
                ExternalAccountId = ProviderAccountId.ToString(),
                BankBrandName = "TPBank",
                AccountType = "personal",
                MaskedAccountNumber = "****6789",
                Status = BankConnectionStatus.Active,
                LinkedAt = Clock.UtcNow
            };
            Db.BankConnections.Add(connection);
            await Db.SaveChangesAsync();
            return connection;
        }

        public SePayTestModeTransactionDto Transaction(string reference, int number, decimal amount = 100) => new(
            Guid.Parse($"00000000-0000-0000-0000-{number:D12}"),
            ProviderAccountId,
            "out",
            0,
            amount,
            900000,
            $"Demo {reference}",
            null,
            reference,
            Clock.UtcNow);

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    private sealed class FakeProvider : ISePayTestModeAccountReader
    {
        public List<SePayTestModeAccountDto> Accounts { get; } = [];
        public List<SePayTestModeTransactionDto> Transactions { get; } = [];
        public int Calls { get; private set; }
        public Task<IReadOnlyList<SePayTestModeAccountDto>> GetAccountsAsync(CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult<IReadOnlyList<SePayTestModeAccountDto>>(Accounts);
        }
        public Task<IReadOnlyList<SePayTestModeTransactionDto>> GetTransactionsAsync(string reference, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult<IReadOnlyList<SePayTestModeTransactionDto>>(Transactions);
        }
    }

    private sealed class FailingLedger : SpendWise.Application.Finance.ILedgerService
    {
        public Task<SpendWise.Application.Finance.LedgerPostingResult> PostAsync(
            Guid sourceAccountId, Guid destinationAccountId, decimal amount, string currency,
            string businessType, Guid businessId, string? idempotencyScope, string? idempotencyKey,
            string description, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Simulated posting failure.");

        public Task<SpendWise.Application.Finance.LedgerPostingResult> PostEntriesAsync(
            IReadOnlyList<SpendWise.Application.Finance.LedgerPostingEntry> entries, string currency,
            string businessType, Guid businessId, string? idempotencyScope, string? idempotencyKey,
            string description, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<SpendWise.Application.Finance.LedgerPostingResult> ReverseAsync(
            Guid originalLedgerTransactionId, Guid reversalBusinessId, string idempotencyKey,
            string reason, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FixedClock(DateTime value) : IClock
    {
        public DateTime UtcNow { get; set; } = value;
    }
}
