using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Features;
using SpendWise.Application.Features.Authentication;
using SpendWise.Application.Features.Budgets;
using SpendWise.Application.Features.Dashboard;
using SpendWise.Application.Features.Evidence;
using SpendWise.Application.Features.Transactions.Commands;
using SpendWise.Application.Features.Transactions.Commands.CreateExpense;
using SpendWise.Application.Features.Transactions.Commands.CreateMultiWalletExpense;
using SpendWise.Application.Features.Transactions.Commands.UpdateTransaction;
using SpendWise.Application.Features.Transactions.Queries.GetTransactions;
using SpendWise.Application.Features.Wallets;
using SpendWise.Application.Finance;
using SpendWise.Domain.Finance;
using SpendWise.Domain.Identity;
using SpendWise.Domain.Planning;
using SpendWise.Infrastructure.Finance;
using SpendWise.Infrastructure.Identity;
using SpendWise.Infrastructure.Persistence;
using SpendWise.Infrastructure.Services;
using Xunit;

namespace SpendWise.Tests;

// Bảo vệ một khoản chi duy nhất được tài trợ chính xác bởi 1-3 ví đã chọn.
public sealed class MultiWalletExpenseTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Multi_wallet_expense_requires_a_non_blank_idempotency_key(string? key)
    {
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var seed = await SeedAsync(fixture, 1);
        var request = Request(seed, 100, [(seed.Wallets[0].Id, 100m)], "valid") with
        {
            IdempotencyKey = key
        };

        var error = await Assert.ThrowsAsync<AppException>(() =>
            Handler(fixture).Handle(new(seed.UserId, request), default));

        Assert.Equal("invalid_idempotency_key", error.Code);
        Assert.Empty(await fixture.Db.LedgerTransactions.ToListAsync());
        Assert.Empty(await fixture.Db.FinancialTransactions.ToListAsync());
        Assert.Equal(1_000, seed.Wallets[0].AvailableBalance);
    }

    [Fact]
    public async Task Invalid_allocation_precision_and_range_are_rejected_before_posting()
    {
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var seed = await SeedAsync(fixture, 2);
        var handler = Handler(fixture);
        var excessivePrecision = Request(seed, 2,
            [(seed.Wallets[0].Id, 1.001m), (seed.Wallets[1].Id, .999m)], "precision");
        var excessiveRange = Request(seed, InputBoundary.MaximumMoney + .01m,
            [(seed.Wallets[0].Id, InputBoundary.MaximumMoney + .01m)], "range");

        var precisionError = await Assert.ThrowsAsync<AppException>(() =>
            handler.Handle(new(seed.UserId, excessivePrecision), default));
        var rangeError = await Assert.ThrowsAsync<AppException>(() =>
            handler.Handle(new(seed.UserId, excessiveRange), default));

        Assert.Equal("invalid_amount", precisionError.Code);
        Assert.Equal("invalid_amount", rangeError.Code);
        Assert.Empty(await fixture.Db.LedgerTransactions.ToListAsync());
        Assert.Empty(await fixture.Db.FinancialTransactions.ToListAsync());
        Assert.All(seed.Wallets, wallet => Assert.Equal(1_000, wallet.AvailableBalance));
    }

    [Fact]
    public async Task Correction_allocation_precision_is_rejected_before_reversal()
    {
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var seed = await SeedAsync(fixture, 2);
        var original = await Handler(fixture).Handle(new(seed.UserId,
            Request(seed, 2, [(seed.Wallets[0].Id, 1m), (seed.Wallets[1].Id, 1m)], "original")), default);
        var correctionEvidence = Evidence(seed.UserId, "precision-correction.png");
        fixture.Db.EvidenceAttachments.Add(correctionEvidence);
        await fixture.Db.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<AppException>(() => new UpdateTransactionCommandHandler(
            fixture.Db,
            new LedgerService(fixture.Db, fixture.Clock),
            fixture.Security,
            new EvidenceSupport(fixture.Db)).Handle(new(
                seed.UserId,
                original.Id,
                new UpdateTransactionRequest(
                    seed.CategoryId,
                    "Invalid precision correction",
                    TargetedCorrectnessFixture.SecurityPin,
                    [correctionEvidence.Id],
                    "Precision must remain exact",
                    Amount: 2,
                    WalletAllocations:
                    [
                        new WalletAllocationRequest(seed.Wallets[0].Id, 1.001m),
                        new WalletAllocationRequest(seed.Wallets[1].Id, .999m)
                    ])), default));

        Assert.Equal("invalid_amount", error.Code);
        Assert.Single(await fixture.Db.LedgerTransactions.ToListAsync());
        Assert.Equal(999, seed.Wallets[0].AvailableBalance);
        Assert.Equal(999, seed.Wallets[1].AvailableBalance);
    }

    [Fact]
    public async Task Multi_then_single_same_key_returns_controlled_conflict_without_second_debit()
    {
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var seed = await SeedAsync(fixture, 2);
        await Handler(fixture).Handle(new(seed.UserId,
            Request(seed, 300, [(seed.Wallets[0].Id, 200m), (seed.Wallets[1].Id, 100m)], "cross-key")), default);
        var secondEvidence = Evidence(seed.UserId, "single-cross.png");
        fixture.Db.EvidenceAttachments.Add(secondEvidence);
        await fixture.Db.SaveChangesAsync();

        var conflict = await Assert.ThrowsAsync<ConflictException>(() => SingleHandler(fixture).Handle(new(
            seed.UserId,
            new CreateManualTransactionRequest(
                seed.Wallets[0].Id,
                seed.CategoryId,
                300,
                "Single collision",
                IdempotencyKey: "cross-key",
                SecurityPin: TargetedCorrectnessFixture.SecurityPin,
                EvidenceIds: [secondEvidence.Id])), default));

        Assert.Equal("idempotency_conflict", conflict.Code);
        Assert.Single(await fixture.Db.FinancialTransactions.ToListAsync());
        Assert.Equal(800, seed.Wallets[0].AvailableBalance);
        Assert.Equal(900, seed.Wallets[1].AvailableBalance);
    }

    [Fact]
    public async Task Single_then_multi_same_key_conflicts_and_each_endpoint_replays_normally()
    {
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var seed = await SeedAsync(fixture, 2);
        var singleRequest = new CreateManualTransactionRequest(
            seed.Wallets[0].Id,
            seed.CategoryId,
            100,
            "Single replay",
            IdempotencyKey: "single-key",
            SecurityPin: TargetedCorrectnessFixture.SecurityPin,
            EvidenceIds: [seed.EvidenceId]);
        var single = await SingleHandler(fixture).Handle(new(seed.UserId, singleRequest), default);
        var singleReplay = await SingleHandler(fixture).Handle(new(seed.UserId, singleRequest), default);

        var conflict = await Assert.ThrowsAsync<ConflictException>(() => Handler(fixture).Handle(new(
            seed.UserId,
            Request(seed, 100,
                [(seed.Wallets[0].Id, 50m), (seed.Wallets[1].Id, 50m)], "single-key")), default));

        Assert.Equal(single.Id, singleReplay.Id);
        Assert.Equal("idempotency_conflict", conflict.Code);
        Assert.Single(await fixture.Db.FinancialTransactions.ToListAsync());
        Assert.Equal(900, seed.Wallets[0].AvailableBalance);
        Assert.Equal(1_000, seed.Wallets[1].AvailableBalance);
    }

    [Fact]
    public async Task Two_wallet_expense_posts_once_and_preserves_allocation_history()
    {
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var seed = await SeedAsync(fixture, 3);

        var result = await Handler(fixture).Handle(new(
            seed.UserId,
            Request(seed, 250, [(seed.Wallets[0].Id, 150m), (seed.Wallets[1].Id, 100m)])), default);

        fixture.Db.ChangeTracker.Clear();
        Assert.Null(result.WalletId);
        Assert.Equal(2, result.WalletAllocations!.Count);
        Assert.Equal(250, result.Amount);
        Assert.Equal(850, (await fixture.Db.Wallets.FindAsync(seed.Wallets[0].Id))!.AvailableBalance);
        Assert.Equal(900, (await fixture.Db.Wallets.FindAsync(seed.Wallets[1].Id))!.AvailableBalance);
        Assert.Equal(1_000, (await fixture.Db.Wallets.FindAsync(seed.Wallets[2].Id))!.AvailableBalance);
        Assert.Single(await fixture.Db.FinancialTransactions.ToListAsync());
        Assert.Equal(2, await fixture.Db.TransactionWalletAllocations.CountAsync());
        var ledger = await fixture.Db.LedgerTransactions.Include(item => item.Entries)
            .SingleAsync(item => item.Id == result.LedgerTransactionId);
        Assert.Equal(3, ledger.Entries.Count);
        Assert.Equal(250, ledger.Entries.Where(item => item.Direction == LedgerEntryDirection.Credit).Sum(item => item.Amount));
        Assert.Equal(250, ledger.Entries.Where(item => item.Direction == LedgerEntryDirection.Debit).Sum(item => item.Amount));
    }

    [Fact]
    public async Task One_and_three_wallet_requests_use_only_selected_wallets()
    {
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var seed = await SeedAsync(fixture, 3);
        var handler = Handler(fixture);

        var one = await handler.Handle(new(seed.UserId,
            Request(seed, 100, [(seed.Wallets[0].Id, 100m)], key: "one-wallet")), default);
        var secondEvidence = Evidence(seed.UserId, "second-receipt.png");
        fixture.Db.EvidenceAttachments.Add(secondEvidence);
        await fixture.Db.SaveChangesAsync();
        var three = await handler.Handle(new(seed.UserId,
            Request(seed with { EvidenceId = secondEvidence.Id }, 300,
                [(seed.Wallets[0].Id, 100m), (seed.Wallets[1].Id, 100m),
                    (seed.Wallets[2].Id, 100m)], key: "three-wallet")), default);

        Assert.Equal(seed.Wallets[0].Id, one.WalletId);
        Assert.Single(one.WalletAllocations!);
        Assert.Null(three.WalletId);
        Assert.Equal(3, three.WalletAllocations!.Count);
        fixture.Db.ChangeTracker.Clear();
        Assert.Equal(800, (await fixture.Db.Wallets.FindAsync(seed.Wallets[0].Id))!.AvailableBalance);
        Assert.Equal(900, (await fixture.Db.Wallets.FindAsync(seed.Wallets[1].Id))!.AvailableBalance);
        Assert.Equal(900, (await fixture.Db.Wallets.FindAsync(seed.Wallets[2].Id))!.AvailableBalance);
    }

    [Fact]
    public async Task Allocation_validation_rejects_mismatch_and_duplicate_wallets()
    {
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var seed = await SeedAsync(fixture, 2);
        var handler = Handler(fixture);

        var mismatch = await Assert.ThrowsAsync<AppException>(() => handler.Handle(new(seed.UserId,
            Request(seed, 200, [(seed.Wallets[0].Id, 150m)])), default));
        var duplicate = await Assert.ThrowsAsync<AppException>(() => handler.Handle(new(seed.UserId,
            Request(seed, 200, [(seed.Wallets[0].Id, 100m), (seed.Wallets[0].Id, 100m)])), default));

        Assert.Equal("allocation_sum_mismatch", mismatch.Code);
        Assert.Equal("duplicate_wallet_allocation", duplicate.Code);
        Assert.Empty(await fixture.Db.FinancialTransactions.ToListAsync());
    }

    [Fact]
    public async Task Insufficient_inactive_and_foreign_wallets_are_rejected_before_posting()
    {
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var seed = await SeedAsync(fixture, 2);
        var stranger = await fixture.AddUserAsync("Stranger");
        var foreign = await fixture.AddWalletAsync(stranger.Id, "Foreign", true, balance: 1_000);
        var handler = Handler(fixture);

        var insufficient = await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(new(seed.UserId,
            Request(seed, 1_100, [(seed.Wallets[0].Id, 1_100m)], key: "insufficient")), default));
        seed.Wallets[1].Status = WalletStatus.Frozen;
        await fixture.Db.SaveChangesAsync();
        var inactive = await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(new(seed.UserId,
            Request(seed, 100, [(seed.Wallets[1].Id, 100m)], key: "inactive")), default));
        var unauthorized = await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new(seed.UserId,
            Request(seed, 100, [(foreign.Id, 100m)], key: "foreign")), default));

        Assert.Equal("insufficient_balance", insufficient.Code);
        Assert.Equal("wallet_unavailable", inactive.Code);
        Assert.Equal("wallet_not_found", unauthorized.Code);
        Assert.Empty(await fixture.Db.FinancialTransactions.ToListAsync());
    }

    [Fact]
    public async Task Posting_failure_rolls_back_every_wallet_and_history_row()
    {
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var seed = await SeedAsync(fixture, 2);
        await fixture.Db.Database.ExecuteSqlRawAsync(
            """
            CREATE TRIGGER fail_multi_wallet_entry
            BEFORE INSERT ON LedgerEntries
            BEGIN
                SELECT RAISE(ABORT, 'forced multi-wallet failure');
            END;
            """);

        await Assert.ThrowsAsync<DbUpdateException>(() => Handler(fixture).Handle(new(seed.UserId,
            Request(seed, 250, [(seed.Wallets[0].Id, 125m), (seed.Wallets[1].Id, 125m)])), default));

        fixture.Db.ChangeTracker.Clear();
        Assert.All(await fixture.Db.Wallets.Where(item => item.OwnerUserId == seed.UserId).ToListAsync(),
            wallet => Assert.Equal(1_000, wallet.AvailableBalance));
        Assert.Empty(await fixture.Db.FinancialTransactions.ToListAsync());
        Assert.Empty(await fixture.Db.TransactionWalletAllocations.ToListAsync());
    }

    [Fact]
    public async Task Idempotency_replays_identical_allocations_and_rejects_changed_allocations()
    {
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var seed = await SeedAsync(fixture, 2);
        var handler = Handler(fixture);
        var request = Request(seed, 300,
            [(seed.Wallets[0].Id, 200m), (seed.Wallets[1].Id, 100m)], key: "same-key");

        var first = await handler.Handle(new(seed.UserId, request), default);
        var replay = await handler.Handle(new(seed.UserId, request), default);
        var changed = request with
        {
            WalletAllocations =
            [
                new WalletAllocationRequest(seed.Wallets[0].Id, 100),
                new WalletAllocationRequest(seed.Wallets[1].Id, 200)
            ]
        };
        var conflict = await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(new(seed.UserId, changed), default));

        Assert.Equal(first.Id, replay.Id);
        Assert.Equal("idempotency_conflict", conflict.Code);
        Assert.Single(await fixture.Db.FinancialTransactions.ToListAsync());
        fixture.Db.ChangeTracker.Clear();
        Assert.Equal(800, (await fixture.Db.Wallets.FindAsync(seed.Wallets[0].Id))!.AvailableBalance);
        Assert.Equal(900, (await fixture.Db.Wallets.FindAsync(seed.Wallets[1].Id))!.AvailableBalance);
    }

    [Fact]
    public async Task Wallet_filter_finds_combined_expense_and_statistics_count_total_once()
    {
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var seed = await SeedAsync(fixture, 2);
        var result = await Handler(fixture).Handle(new(seed.UserId,
            Request(seed, 300, [(seed.Wallets[0].Id, 200m), (seed.Wallets[1].Id, 100m)])), default);
        var localNow = SpendWise.Application.Common.VietnamBusinessTime.ToLocal(fixture.Clock.UtcNow);
        fixture.Db.Budgets.Add(new Budget
        {
            OwnerUserId = seed.UserId,
            CategoryId = seed.CategoryId,
            Year = localNow.Year,
            Month = localNow.Month,
            LimitAmount = 1_000
        });
        await fixture.Db.SaveChangesAsync();

        var filtered = await new GetTransactionsQueryHandler(fixture.Db).Handle(
            new GetTransactionsQuery(seed.UserId, new TransactionQuery(WalletId: seed.Wallets[1].Id)), default);
        var dashboard = await new GetDashboardQueryHandler(
            fixture.Db, fixture.Clock, new BudgetReader(fixture.Db)).Handle(new(seed.UserId), default);
        var budget = await new BudgetReader(fixture.Db).CalculateAsync(
            seed.UserId, seed.CategoryId, localNow.Year, localNow.Month, default);

        Assert.Single(filtered.Items);
        Assert.Equal(result.Id, filtered.Items[0].Id);
        Assert.Equal(2, filtered.Items[0].WalletAllocations!.Count);
        Assert.Equal(300, dashboard.ExpenseThisMonth);
        Assert.Equal(300, dashboard.CategoryDistribution.Single(item => item.CategoryId == seed.CategoryId).Amount);
        Assert.Equal(300, budget.SpentAmount);
    }

    [Fact]
    public async Task Combined_expense_correction_reverses_and_reposts_every_allocation()
    {
        await using var fixture = await TargetedCorrectnessFixture.CreateAsync();
        var seed = await SeedAsync(fixture, 3);
        var original = await Handler(fixture).Handle(new(seed.UserId,
            Request(seed, 300, [(seed.Wallets[0].Id, 200m), (seed.Wallets[1].Id, 100m)])), default);
        var correctionEvidence = Evidence(seed.UserId, "correction.png");
        fixture.Db.EvidenceAttachments.Add(correctionEvidence);
        await fixture.Db.SaveChangesAsync();

        var corrected = await new UpdateTransactionCommandHandler(
            fixture.Db,
            new LedgerService(fixture.Db, fixture.Clock),
            fixture.Security,
            new EvidenceSupport(fixture.Db)).Handle(new(
                seed.UserId,
                original.Id,
                new UpdateTransactionRequest(
                    seed.CategoryId,
                    "Corrected combined expense",
                    TargetedCorrectnessFixture.SecurityPin,
                    [correctionEvidence.Id],
                    "Correct allocations",
                    Amount: 400,
                    WalletAllocations:
                    [
                        new WalletAllocationRequest(seed.Wallets[1].Id, 150),
                        new WalletAllocationRequest(seed.Wallets[2].Id, 250)
                    ])), default);

        fixture.Db.ChangeTracker.Clear();
        Assert.Equal(400, corrected.Amount);
        Assert.Equal(2, corrected.WalletAllocations!.Count);
        Assert.Equal(1_000, (await fixture.Db.Wallets.FindAsync(seed.Wallets[0].Id))!.AvailableBalance);
        Assert.Equal(850, (await fixture.Db.Wallets.FindAsync(seed.Wallets[1].Id))!.AvailableBalance);
        Assert.Equal(750, (await fixture.Db.Wallets.FindAsync(seed.Wallets[2].Id))!.AvailableBalance);
        var stored = await fixture.Db.FinancialTransactions
            .Include(item => item.Corrections).ThenInclude(item => item.WalletAllocations)
            .SingleAsync(item => item.Id == original.Id);
        var correction = Assert.Single(stored.Corrections);
        Assert.Equal(4, correction.WalletAllocations.Count);
        Assert.Equal(300, correction.WalletAllocations.Where(item => item.IsOriginal).Sum(item => item.Amount));
        Assert.Equal(400, correction.WalletAllocations.Where(item => !item.IsOriginal).Sum(item => item.Amount));
        var originalLedger = await fixture.Db.LedgerTransactions.FindAsync(correction.OriginalLedgerTransactionId);
        Assert.Equal(LedgerTransactionStatus.Reversed, originalLedger!.Status);
    }

    [Fact]
    public async Task Concurrent_same_key_returns_one_transaction_without_double_debit()
    {
        var result = await RunConcurrentAsync(sameKey: true);

        Assert.All(result.Outcomes, outcome => Assert.Null(outcome.Error));
        Assert.Equal(result.Outcomes[0].Result!.Id, result.Outcomes[1].Result!.Id);
        Assert.Equal(1, result.TransactionCount);
        Assert.Equal(20, result.Balance);
    }

    [Fact]
    public async Task Concurrent_different_keys_cannot_overspend_the_same_wallet()
    {
        var result = await RunConcurrentAsync(sameKey: false);

        Assert.Single(result.Outcomes, outcome => outcome.Result is not null);
        var conflict = Assert.IsType<ConflictException>(
            Assert.Single(result.Outcomes, outcome => outcome.Error is not null).Error);
        Assert.Equal("insufficient_balance", conflict.Code);
        Assert.Equal(1, result.TransactionCount);
        Assert.Equal(20, result.Balance);
    }

    private static CreateMultiWalletExpenseCommandHandler Handler(TargetedCorrectnessFixture fixture) => new(
        fixture.Db,
        new LedgerService(fixture.Db, fixture.Clock),
        fixture.Clock,
        fixture.Security,
        new EvidenceSupport(fixture.Db));

    private static CreateExpenseCommandHandler SingleHandler(TargetedCorrectnessFixture fixture) => new(
        new ManualTransactionCreator(
            fixture.Db,
            new LedgerService(fixture.Db, fixture.Clock),
            fixture.Clock,
            fixture.Security,
            new EvidenceSupport(fixture.Db)));

    private static CreateMultiWalletExpenseRequest Request(
        Seed seed,
        decimal total,
        IReadOnlyList<(Guid WalletId, decimal Amount)> allocations,
        string? key = null) => new(
            total,
            allocations.Select(item => new WalletAllocationRequest(item.WalletId, item.Amount)).ToList(),
            seed.CategoryId,
            "Combined expense",
            IdempotencyKey: key ?? Guid.NewGuid().ToString("N"),
            SecurityPin: TargetedCorrectnessFixture.SecurityPin,
            EvidenceIds: [seed.EvidenceId]);

    private static async Task<Seed> SeedAsync(TargetedCorrectnessFixture fixture, int walletCount)
    {
        var user = await fixture.AddUserAsync("Multi-wallet owner");
        var wallets = new List<Wallet>();
        for (var index = 0; index < walletCount; index++)
        {
            wallets.Add(await fixture.AddWalletAsync(
                user.Id,
                index == 0 ? "Main" : $"Wallet {index + 1}",
                index == 0,
                balance: 1_000,
                type: index == 0 ? WalletType.Main : WalletType.Savings));
        }
        var categoryId = await fixture.Db.Categories
            .Where(item => item.Kind == CategoryKind.Expense && item.IsSystem)
            .Select(item => item.Id)
            .FirstAsync();
        var evidence = Evidence(user.Id, "receipt.png");
        fixture.Db.EvidenceAttachments.Add(evidence);
        await fixture.Db.SaveChangesAsync();
        return new Seed(user.Id, wallets, categoryId, evidence.Id);
    }

    private static EvidenceAttachment Evidence(Guid ownerId, string fileName) => new()
    {
        OwnerUserId = ownerId,
        OriginalFileName = fileName,
        ContentType = "image/png",
        Length = 8,
        ContentHash = Guid.NewGuid().ToString("N"),
        Content = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]
    };

    private static async Task<ConcurrentResult> RunConcurrentAsync(bool sameKey)
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"spendwise-multi-wallet-{Guid.NewGuid():N}.db");
        try
        {
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                DefaultTimeout = 30,
                Pooling = false
            }.ToString();
            var options = new DbContextOptionsBuilder<SpendWiseDbContext>()
                .UseSqlite(connectionString).Options;
            var clock = new SystemClock();
            Guid userId;
            Guid walletId;
            Guid categoryId;
            Guid firstEvidenceId;
            Guid secondEvidenceId;
            await using (var setup = new SpendWiseDbContext(options, clock))
            {
                await setup.Database.EnsureCreatedAsync();
                var unique = Guid.NewGuid().ToString("N");
                var user = new User
                {
                    FullName = "Concurrent expense owner",
                    Email = $"{unique}@test.local",
                    NormalizedEmail = $"{unique}@TEST.LOCAL",
                    SpendId = unique,
                    NormalizedSpendId = unique.ToUpperInvariant(),
                    PasswordHash = "test-hash",
                    SecurityPinHash = new SecurityPinService().Hash(TargetedCorrectnessFixture.SecurityPin)
                };
                var wallet = WalletFactory.Create(user.Id, "Main", WalletType.Main, true);
                wallet.AvailableBalance = 100;
                wallet.LedgerAccount.Balance = 100;
                var firstEvidence = Evidence(user.Id, "first.png");
                var secondEvidence = Evidence(user.Id, "second.png");
                setup.AddRange(user, wallet, firstEvidence, secondEvidence);
                await setup.SaveChangesAsync();
                userId = user.Id;
                walletId = wallet.Id;
                firstEvidenceId = firstEvidence.Id;
                secondEvidenceId = secondEvidence.Id;
                categoryId = await setup.Categories.Where(item => item.Kind == CategoryKind.Expense)
                    .Select(item => item.Id).FirstAsync();
            }

            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var key = sameKey ? "concurrent-same" : null;
            var first = ExecuteConcurrentAsync(options, clock, gate.Task, userId, walletId,
                categoryId, firstEvidenceId, key ?? "concurrent-a");
            var second = ExecuteConcurrentAsync(options, clock, gate.Task, userId, walletId,
                categoryId, sameKey ? firstEvidenceId : secondEvidenceId, key ?? "concurrent-b");
            gate.SetResult();
            var outcomes = await Task.WhenAll(first, second);
            await using var verify = new SpendWiseDbContext(options, clock);
            return new ConcurrentResult(
                outcomes,
                await verify.FinancialTransactions.CountAsync(),
                (await verify.Wallets.FindAsync(walletId))!.AvailableBalance);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath)) File.Delete(databasePath);
        }
    }

    private static async Task<ConcurrentOutcome> ExecuteConcurrentAsync(
        DbContextOptions<SpendWiseDbContext> options,
        SystemClock clock,
        Task gate,
        Guid userId,
        Guid walletId,
        Guid categoryId,
        Guid evidenceId,
        string key)
    {
        await gate;
        await using var db = new SpendWiseDbContext(options, clock);
        var handler = new CreateMultiWalletExpenseCommandHandler(
            db,
            new LedgerService(db, clock),
            clock,
            new FinancialSecurityGuard(db, new SecurityPinService(), new AccountCredentialLockout(db, clock)),
            new EvidenceSupport(db));
        try
        {
            var result = await handler.Handle(new(userId, new CreateMultiWalletExpenseRequest(
                80,
                [new WalletAllocationRequest(walletId, 80)],
                categoryId,
                "Concurrent expense",
                IdempotencyKey: key,
                SecurityPin: TargetedCorrectnessFixture.SecurityPin,
                EvidenceIds: [evidenceId])), default);
            return new ConcurrentOutcome(result, null);
        }
        catch (Exception exception)
        {
            return new ConcurrentOutcome(null, exception);
        }
    }

    private sealed record Seed(Guid UserId, IReadOnlyList<Wallet> Wallets, Guid CategoryId, Guid EvidenceId);
    private sealed record ConcurrentOutcome(TransactionDto? Result, Exception? Error);
    private sealed record ConcurrentResult(IReadOnlyList<ConcurrentOutcome> Outcomes, int TransactionCount, decimal Balance);
}
