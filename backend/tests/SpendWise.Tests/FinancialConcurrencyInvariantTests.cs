using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Features;
using SpendWise.Application.Features.Authentication;
using SpendWise.Application.Features.Goals.Commands;
using SpendWise.Application.Features.Goals.Commands.ContributeGoal;
using SpendWise.Application.Features.Groups;
using SpendWise.Application.Groups;
using SpendWise.Application.Planning;
using SpendWise.Domain.Finance;
using SpendWise.Domain.Groups;
using SpendWise.Domain.Identity;
using SpendWise.Domain.Planning;
using SpendWise.Infrastructure.Admin;
using SpendWise.Infrastructure.Finance;
using SpendWise.Infrastructure.Identity;
using SpendWise.Infrastructure.Persistence;
using SpendWise.Application.Features.Wallets;
using Xunit;

namespace SpendWise.Tests;

// Các test ép hai DbContext ghi cùng lúc để kiểm tra rowversion bảo vệ số dư.
public sealed class FinancialConcurrencyInvariantTests
{
    private const string FinancialPin = "123456";
    private static readonly string FinancialPinHash = new SecurityPinService().Hash(FinancialPin);

    [Fact]
    public async Task Two_simultaneous_debits_from_one_wallet_allow_only_one_posting()
    {
        // Arrange: một ví có 100 đồng và hai đích nhận độc lập.
        await using var fixture = await ConcurrentFinanceFixture.CreateAsync();
        var seeded = await fixture.SeedLedgerDebitAsync();
        await using var firstDb = fixture.CreateCoordinatedContext();
        await using var secondDb = fixture.CreateCoordinatedContext();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        // Act: hai request P2P khác key cùng cố trừ 80 đồng.
        var results = await Task.WhenAll(
            CaptureAsync(new LedgerService(firstDb, fixture.Clock).PostAsync(
                seeded.SourceAccountId,
                seeded.FirstDestinationId,
                80,
                "VND",
                "P2PTransfer",
                Guid.NewGuid(),
                "user:concurrent:direct",
                "p2p-a",
                "Concurrent debit A",
                timeout.Token)),
            CaptureAsync(new LedgerService(secondDb, fixture.Clock).PostAsync(
                seeded.SourceAccountId,
                seeded.SecondDestinationId,
                80,
                "VND",
                "P2PTransfer",
                Guid.NewGuid(),
                "user:concurrent:direct",
                "p2p-b",
                "Concurrent debit B",
                timeout.Token)));

        // Assert: đúng một posting thành công; ví không âm và Ledger vẫn cân bằng.
        AssertSingleConcurrencyWinner(results);
        await using var verify = fixture.CreateVerificationContext();
        Assert.Equal(20, (await verify.Wallets.SingleAsync()).AvailableBalance);
        Assert.Equal(20, (await verify.LedgerAccounts.SingleAsync(
            account => account.Id == seeded.SourceAccountId)).Balance);
        Assert.Equal(1, await verify.LedgerTransactions.CountAsync());
        await AssertAllPostingsBalancedAsync(verify);
    }

    [Fact]
    public async Task Stale_rowversion_is_rejected_by_the_test_database_provider()
    {
        // Arrange: hai DbContext cùng đọc một LedgerAccount với chung rowversion ban đầu.
        await using var fixture = await ConcurrentFinanceFixture.CreateAsync();
        var seeded = await fixture.SeedLedgerDebitAsync();
        await using var firstDb = fixture.CreateVerificationContext();
        await using var staleDb = fixture.CreateVerificationContext();
        var first = await firstDb.LedgerAccounts.FindAsync(seeded.SourceAccountId);
        var stale = await staleDb.LedgerAccounts.FindAsync(seeded.SourceAccountId);

        // Act: context thứ nhất ghi token mới, context thứ hai vẫn dùng token cũ.
        first!.Balance -= 1;
        await firstDb.SaveChangesAsync();
        stale!.Balance -= 1;

        // Assert: EF phát hiện bản ghi đã đổi thay vì ghi đè số dư mới.
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => staleDb.SaveChangesAsync());
    }

    [Fact]
    public async Task Concurrent_goal_contributions_cannot_overspend_wallet()
    {
        // Arrange: ví có 100 đồng và Goal chưa được phân bổ tiền.
        await using var fixture = await ConcurrentFinanceFixture.CreateAsync();
        var seeded = await fixture.SeedGoalAsync();
        await using var firstDb = fixture.CreateCoordinatedContext();
        await using var secondDb = fixture.CreateCoordinatedContext();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        // Act: hai đóng góp 80 đồng chạy song song với key khác nhau.
        var results = await Task.WhenAll(
            CaptureAsync(GoalHandler(firstDb, fixture).Handle(
                new ContributeGoalCommand(
                    seeded.UserId,
                    seeded.GoalId,
                    new GoalMovementRequest(seeded.WalletId, 80, "goal-a", FinancialPin)),
                timeout.Token)),
            CaptureAsync(GoalHandler(secondDb, fixture).Handle(
                new ContributeGoalCommand(
                    seeded.UserId,
                    seeded.GoalId,
                    new GoalMovementRequest(seeded.WalletId, 80, "goal-b", FinancialPin)),
                timeout.Token)));

        // Assert: chỉ một contribution được ghi và Goal nhận đúng 80 đồng.
        AssertSingleConcurrencyWinner(results);
        await using var verify = fixture.CreateVerificationContext();
        Assert.Equal(20, (await verify.Wallets.FindAsync(seeded.WalletId))!.AvailableBalance);
        Assert.Equal(80, (await verify.Goals.FindAsync(seeded.GoalId))!.CurrentAllocatedAmount);
        Assert.Equal(1, await verify.GoalMovements.CountAsync());
        Assert.Equal(1, await verify.FinancialTransactions.CountAsync());
        await AssertAllPostingsBalancedAsync(verify);
    }

    [Fact]
    public async Task Concurrent_wallet_funded_group_contributions_cannot_overspend_wallet_or_lose_fund_updates()
    {
        await using var fixture = await ConcurrentFinanceFixture.CreateAsync();
        var seeded = await fixture.SeedCurrentSharedFundAsync();
        await using var firstDb = fixture.CreateCoordinatedContext();
        await using var secondDb = fixture.CreateCoordinatedContext();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var results = await Task.WhenAll(
            CaptureAsync(GroupFundAccounting(firstDb, fixture).ContributeAsync(seeded.UserId, seeded.GroupId,
                seeded.WalletId, 80, "Fund A", "fund-a", FinancialPin, null, timeout.Token)),
            CaptureAsync(GroupFundAccounting(secondDb, fixture).ContributeAsync(seeded.UserId, seeded.GroupId,
                seeded.WalletId, 80, "Fund B", "fund-b", FinancialPin, null, timeout.Token)));

        AssertSingleConcurrencyWinner(results);
        await using var verify = fixture.CreateVerificationContext();
        Assert.Equal(20, (await verify.Wallets.FindAsync(seeded.WalletId))!.AvailableBalance);
        var fund = await verify.SharedFunds.Include(item => item.LedgerAccount).SingleAsync();
        Assert.Equal(80, fund.Balance);
        Assert.Equal(fund.Balance, fund.LedgerAccount.Balance);
        Assert.Equal(80, (await verify.GroupFundShares.SingleAsync()).RemainingAmount);
        Assert.Equal(1, await verify.GroupContributions.CountAsync());
        await AssertAllPostingsBalancedAsync(verify);
    }

 #if RETIRED_LEGACY_TESTS
    [Fact]
    public async Task Concurrent_shared_fund_contributions_cannot_overspend_wallet()
    {
        // Arrange: thành viên có ví 100 đồng và quỹ nhóm đang rỗng.
        await using var fixture = await ConcurrentFinanceFixture.CreateAsync();
        var seeded = await fixture.SeedSharedFundAsync();
        await using var firstDb = fixture.CreateCoordinatedContext();
        await using var secondDb = fixture.CreateCoordinatedContext();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        // Act: hai đóng góp 80 đồng vào cùng quỹ chạy song song.
        var results = await Task.WhenAll(
            CaptureAsync(SharedFundHandler(firstDb, fixture).Handle(
                new ContributeSharedFundCommand(
                    seeded.UserId,
                    seeded.GroupId,
                    new ContributeFundRequest(seeded.WalletId, 80, "Fund A", "fund-a")),
                timeout.Token)),
            CaptureAsync(SharedFundHandler(secondDb, fixture).Handle(
                new ContributeSharedFundCommand(
                    seeded.UserId,
                    seeded.GroupId,
                    new ContributeFundRequest(seeded.WalletId, 80, "Fund B", "fund-b")),
                timeout.Token)));

        // Assert: quỹ và ví chỉ thay đổi theo request thắng concurrency.
        AssertSingleConcurrencyWinner(results);
        await using var verify = fixture.CreateVerificationContext();
        Assert.Equal(20, (await verify.Wallets.FindAsync(seeded.WalletId))!.AvailableBalance);
        Assert.Equal(80, (await verify.SharedFunds.SingleAsync()).Balance);
        Assert.Equal(1, await verify.GroupContributions.CountAsync());
        Assert.Equal(1, await verify.FinancialTransactions.CountAsync());
        await AssertAllPostingsBalancedAsync(verify);
    }

    [Fact]
    public async Task Concurrent_debt_settlements_never_exceed_remaining_debt()
    {
        // Arrange: khoản nợ 100 đồng và ví con nợ có 100 đồng.
        await using var fixture = await ConcurrentFinanceFixture.CreateAsync();
        var seeded = await fixture.SeedDebtAsync();
        await using var firstDb = fixture.CreateCoordinatedContext();
        await using var secondDb = fixture.CreateCoordinatedContext();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        // Act: hai settlement 80 đồng cùng nhắm vào một Debt.
        var results = await Task.WhenAll(
            CaptureAsync(DebtHandler(firstDb, fixture).Handle(
                new SettleDebtCommand(
                    seeded.DebtorId,
                    seeded.GroupId,
                    seeded.DebtId,
                    new SettleDebtRequest(seeded.DebtorWalletId, 80, "debt-a")),
                timeout.Token)),
            CaptureAsync(DebtHandler(secondDb, fixture).Handle(
                new SettleDebtCommand(
                    seeded.DebtorId,
                    seeded.GroupId,
                    seeded.DebtId,
                    new SettleDebtRequest(seeded.DebtorWalletId, 80, "debt-b")),
                timeout.Token)));

        // Assert: đúng một settlement tồn tại, tổng trả không vượt nợ và ví chỉ bị trừ một lần.
        AssertSingleConcurrencyWinner(results);
        await using var verify = fixture.CreateVerificationContext();
        var debt = await verify.GroupDebts.FindAsync(seeded.DebtId);
        Assert.Equal(80, debt!.PaidAmount);
        Assert.Equal(20, debt.RemainingAmount);
        Assert.Equal(20, (await verify.Wallets.FindAsync(seeded.DebtorWalletId))!.AvailableBalance);
        Assert.Equal(1, await verify.DebtSettlements.CountAsync());
        Assert.Equal(2, await verify.FinancialTransactions.CountAsync());
        await AssertAllPostingsBalancedAsync(verify);
    }

 #endif
    private static ContributeGoalCommandHandler GoalHandler(
        SpendWiseDbContext db,
        ConcurrentFinanceFixture fixture) => new(
            new GoalMovementExecutor(
                db,
                new LedgerService(db, fixture.Clock),
                fixture.Clock,
                new FinancialSecurityGuard(db, new SecurityPinService(),
                    new AccountCredentialLockout(db, fixture.Clock)),
                new SpendWise.Application.Features.Banking.ActiveBankGuard(db)));

    private static WalletFundAccounting GroupFundAccounting(
        SpendWiseDbContext db,
        ConcurrentFinanceFixture fixture) => new(
            db,
            new GroupAccess(db),
            new LedgerService(db, fixture.Clock),
            new FinancialSecurityGuard(db, new SecurityPinService(),
                new AccountCredentialLockout(db, fixture.Clock)),
            fixture.Clock);

 #if RETIRED_LEGACY_TESTS
    private static ContributeSharedFundCommandHandler SharedFundHandler(
        SpendWiseDbContext db,
        ConcurrentFinanceFixture fixture) => new(
            db,
            new LedgerService(db, fixture.Clock),
            fixture.Clock,
            new OperationPolicy(db),
            new GroupAccess(db));

    private static SettleDebtCommandHandler DebtHandler(
        SpendWiseDbContext db,
        ConcurrentFinanceFixture fixture) => new(
            db,
            new LedgerService(db, fixture.Clock),
            fixture.Clock,
            new GroupAccess(db));

 #endif
    private static async Task<Exception?> CaptureAsync(Task operation)
    {
        try
        {
            await operation;
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private static void AssertSingleConcurrencyWinner(IReadOnlyCollection<Exception?> results)
    {
        // Một request thành công; request còn lại nhận Conflict có thể retry cùng key.
        Assert.Single(results, result => result is null);
        var conflict = Assert.IsType<ConflictException>(
            Assert.Single(results, result => result is not null));
        Assert.Equal("concurrent_financial_operation", conflict.Code);
    }

    private static async Task AssertAllPostingsBalancedAsync(SpendWiseDbContext db)
    {
        // Mỗi posting phải có tổng Nợ bằng tổng Có sau cuộc đua.
        var transactions = await db.LedgerTransactions
            .Include(transaction => transaction.Entries)
            .ToListAsync();
        Assert.All(transactions, transaction =>
        {
            var debits = transaction.Entries
                .Where(entry => entry.Direction == LedgerEntryDirection.Debit)
                .Sum(entry => entry.Amount);
            var credits = transaction.Entries
                .Where(entry => entry.Direction == LedgerEntryDirection.Credit)
                .Sum(entry => entry.Amount);
            Assert.Equal(debits, credits);
        });
    }

    private sealed class ConcurrentFinanceFixture : IAsyncDisposable
    {
        private readonly string databaseName = Guid.NewGuid().ToString("N");
        private readonly InMemoryDatabaseRoot databaseRoot = new();
        private readonly CoordinatedLedgerSaveInterceptor coordinator = new(2);

        private ConcurrentFinanceFixture()
        {
            Clock = new SpendWise.Infrastructure.Services.SystemClock();
        }

        public SpendWise.Application.Common.Interfaces.IClock Clock { get; }

        public static Task<ConcurrentFinanceFixture> CreateAsync() =>
            Task.FromResult(new ConcurrentFinanceFixture());

        public SpendWiseDbContext CreateCoordinatedContext() =>
            CreateContext(coordinator);

        public SpendWiseDbContext CreateVerificationContext() => CreateContext();

        public async Task<LedgerDebitSeed> SeedLedgerDebitAsync()
        {
            await using var db = CreateContext();
            var user = User("Concurrent ledger user");
            var wallet = Wallet(user.Id, 100);
            var firstDestination = SystemAccount("P2P destination A");
            var secondDestination = SystemAccount("P2P destination B");
            db.AddRange(user, wallet, firstDestination, secondDestination);
            await db.SaveChangesAsync();
            return new LedgerDebitSeed(
                wallet.LedgerAccount.Id,
                firstDestination.Id,
                secondDestination.Id);
        }

        public async Task<GoalSeed> SeedGoalAsync()
        {
            await using var db = CreateContext();
            var user = User("Concurrent goal user");
            var wallet = Wallet(user.Id, 100);
            var reserve = SystemAccount("Goal reserve", LedgerAccountType.GoalReserve);
            var goal = new Goal
            {
                OwnerUserId = user.Id,
                Name = "Emergency goal",
                TargetAmount = 1_000,
                LedgerAccountId = reserve.Id,
                LedgerAccount = reserve
            };
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
            db.AddRange(user, wallet, goal, bank);
            await db.SaveChangesAsync();
            return new GoalSeed(user.Id, wallet.Id, goal.Id);
        }

        public async Task<SharedFundSeed> SeedCurrentSharedFundAsync()
        {
            await using var db = CreateContext();
            var user = User("Concurrent wallet-funded group user");
            var wallet = Wallet(user.Id, 100);
            var group = new Group { Name = "Concurrent group", Type = GroupType.Friends };
            group.Members.Add(new GroupMember
            {
                GroupId = group.Id,
                Group = group,
                UserId = user.Id,
                User = user,
                Role = GroupRole.Owner,
                JoinedAt = Clock.UtcNow
            });
            var fundAccount = SystemAccount("Shared fund", LedgerAccountType.SharedFund);
            fundAccount.IsSystem = false;
            fundAccount.GroupId = group.Id;
            group.SharedFund = new SharedFund
            {
                GroupId = group.Id,
                Group = group,
                LedgerAccountId = fundAccount.Id,
                LedgerAccount = fundAccount
            };
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
            db.AddRange(user, wallet, group, bank);
            await db.SaveChangesAsync();
            return new SharedFundSeed(user.Id, wallet.Id, group.Id);
        }

 #if RETIRED_LEGACY_TESTS
        public async Task<SharedFundSeed> SeedSharedFundAsync()
        {
            await using var db = CreateContext();
            var user = User("Concurrent fund user");
            var wallet = Wallet(user.Id, 100);
            var group = new Group { Name = "Concurrent group", Type = GroupType.Friends };
            var member = new GroupMember
            {
                GroupId = group.Id,
                Group = group,
                UserId = user.Id,
                User = user,
                Role = GroupRole.Owner,
                JoinedAt = Clock.UtcNow
            };
            var fundAccount = SystemAccount("Shared fund", LedgerAccountType.SharedFund);
            fundAccount.GroupId = group.Id;
            var fund = new SharedFund
            {
                GroupId = group.Id,
                Group = group,
                LedgerAccountId = fundAccount.Id,
                LedgerAccount = fundAccount
            };
            group.Members.Add(member);
            group.SharedFund = fund;
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
            db.AddRange(user, wallet, group, bank);
            await db.SaveChangesAsync();
            return new SharedFundSeed(user.Id, wallet.Id, group.Id);
        }

        public async Task<DebtSeed> SeedDebtAsync()
        {
            await using var db = CreateContext();
            var debtor = User("Concurrent debtor");
            var creditor = User("Concurrent creditor");
            var debtorWallet = Wallet(debtor.Id, 100);
            var creditorWallet = Wallet(creditor.Id, 0);
            var group = new Group { Name = "Debt group", Type = GroupType.Roommates };
            group.Members.Add(new GroupMember
            {
                GroupId = group.Id,
                Group = group,
                UserId = debtor.Id,
                User = debtor,
                Role = GroupRole.Member,
                JoinedAt = Clock.UtcNow
            });
            var debt = new GroupDebt
            {
                GroupId = group.Id,
                SharedExpenseId = Guid.NewGuid(),
                ExpenseSplitId = Guid.NewGuid(),
                DebtorUserId = debtor.Id,
                CreditorUserId = creditor.Id,
                OriginalAmount = 100
            };
            db.AddRange(debtor, creditor, debtorWallet, creditorWallet, group, debt);
            await db.SaveChangesAsync();
            return new DebtSeed(debtor.Id, debtorWallet.Id, group.Id, debt.Id);
        }

 #endif
        private SpendWiseDbContext CreateContext(SaveChangesInterceptor? interceptor = null)
        {
            var options = new DbContextOptionsBuilder<SpendWiseDbContext>()
                .UseInMemoryDatabase(databaseName, databaseRoot);
            if (interceptor is not null)
            {
                options.AddInterceptors(interceptor);
            }

            return new SpendWiseDbContext(options.Options, Clock);
        }

        private static User User(string name) => new()
        {
            FullName = name,
            Email = $"{Guid.NewGuid():N}@test.local",
            NormalizedEmail = Guid.NewGuid().ToString("N"),
            SpendId = Guid.NewGuid().ToString("N"),
            NormalizedSpendId = Guid.NewGuid().ToString("N"),
            PasswordHash = "test",
            SecurityPinHash = FinancialPinHash
        };

        private static Wallet Wallet(Guid ownerId, decimal balance)
        {
            var wallet = WalletFactory.Create(ownerId, "Main", WalletType.Main, true);
            wallet.AvailableBalance = balance;
            wallet.LedgerAccount.Balance = balance;
            return wallet;
        }

        private static LedgerAccount SystemAccount(
            string name,
            LedgerAccountType type = LedgerAccountType.ExternalSource) => new()
            {
                Code = Guid.NewGuid().ToString("N"),
                Name = name,
                Type = type,
                IsSystem = true
            };

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class CoordinatedLedgerSaveInterceptor(int participants)
        : SaveChangesInterceptor
    {
        private readonly TaskCompletionSource<bool> release = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private int arrivals;
        private int saveOrder;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            // Chỉ đồng bộ tại lần Save đang sửa LedgerAccount của hai request.
            var changesLedgerBalance = eventData.Context?.ChangeTracker
                .Entries<LedgerAccount>()
                .Any(entry => entry.State == EntityState.Modified) == true;
            if (!changesLedgerBalance)
            {
                return result;
            }

            if (Interlocked.Increment(ref arrivals) == participants)
            {
                release.TrySetResult(true);
            }

            await release.Task.WaitAsync(cancellationToken);
            if (Interlocked.Increment(ref saveOrder) > 1)
            {
                // Mô phỏng SQL Server từ chối request dùng rowversion cũ trước khi ghi batch.
                throw new DbUpdateConcurrencyException(
                    "The coordinated request used a stale financial rowversion.");
            }

            return result;
        }
    }

    private sealed record LedgerDebitSeed(
        Guid SourceAccountId,
        Guid FirstDestinationId,
        Guid SecondDestinationId);
    private sealed record GoalSeed(Guid UserId, Guid WalletId, Guid GoalId);
    private sealed record SharedFundSeed(Guid UserId, Guid WalletId, Guid GroupId);
 #if RETIRED_LEGACY_TESTS
    private sealed record DebtSeed(Guid DebtorId, Guid DebtorWalletId, Guid GroupId, Guid DebtId);
 #endif
}
