using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features;
using SpendWise.Application.Features.Authentication;
using SpendWise.Application.Features.Evidence;
using SpendWise.Application.Features.Transactions.Commands;
using SpendWise.Application.Features.Transactions.Commands.CreateExpense;
using SpendWise.Application.Features.Transactions.Commands.CreateIncome;
using SpendWise.Application.Features.TransactionRules.Commands.CreateTransactionRule;
using SpendWise.Application.Features.Wallets;
using SpendWise.Application.Finance;
using SpendWise.Application.Notifications;
using SpendWise.Application.Planning;
using SpendWise.Domain.Finance;
using SpendWise.Domain.Identity;
using SpendWise.Infrastructure.Admin;
using SpendWise.Infrastructure.Finance;
using SpendWise.Infrastructure.Identity;
using SpendWise.Infrastructure.Notifications;
using SpendWise.Infrastructure.Persistence;
using SpendWise.Infrastructure.Services;
using Xunit;

namespace SpendWise.Tests;

// Các test này dùng SQLite quan hệ để kiểm tra commit, rollback và idempotency tài chính.

public sealed class RelationalFinancialIntegrationTests
{
    private const string FinancialPin = "123456";
    private static readonly string FinancialPinHash = new SecurityPinService().Hash(FinancialPin);

    [Fact]
    public async Task Manual_income_commits_wallet_ledger_and_history_together()
    {
        // Arrange: tạo ví có 100 đồng và danh mục Thu nhập trên SQLite quan hệ.
        await using var fixture = await SqliteFixture.CreateAsync();
        var (user, wallet, category, evidence) = await fixture.SeedManualTransactionAsync(
            CategoryKind.Income,
            100);
        var handler = new CreateIncomeCommandHandler(CreateManualTransactionCreator(fixture));

        // Act: ghi khoản Thu nhập 400 đồng qua CreateIncomeCommand.
        var result = await handler.Handle(
            new CreateIncomeCommand(
                user.Id,
                new CreateManualTransactionRequest(
                    wallet.Id,
                    category.Id,
                    400,
                    "Salary",
                    IdempotencyKey: "income-1",
                    SecurityPin: FinancialPin,
                    EvidenceIds: [evidence.Id])),
            default);

        fixture.Db.ChangeTracker.Clear();
        var storedWallet = await fixture.Db.Wallets.SingleAsync(x => x.Id == wallet.Id);
        var storedHistory = await fixture.Db.FinancialTransactions.SingleAsync(
            x => x.Id == result.Id);
        var entries = await fixture.Db.LedgerEntries
            .Where(x => x.LedgerTransactionId == storedHistory.LedgerTransactionId)
            .ToListAsync();

        // Assert: ví tăng thành 500, lịch sử là Income và tổng Ledger bên Nợ bằng bên Có.
        Assert.Equal(500, storedWallet.AvailableBalance);
        Assert.Equal(FinancialTransactionType.Income, storedHistory.Type);
        Assert.Equal(400, storedHistory.Amount);
        Assert.Equal(
            entries.Where(x => x.Direction == LedgerEntryDirection.Debit).Sum(x => x.Amount),
            entries.Where(x => x.Direction == LedgerEntryDirection.Credit).Sum(x => x.Amount));
    }

    [Fact]
    public async Task Manual_expense_commits_wallet_ledger_and_history_together()
    {
        // Arrange: tạo ví có 1.000 đồng và danh mục Chi tiêu trên SQLite quan hệ.
        await using var fixture = await SqliteFixture.CreateAsync();
        var (user, wallet, category, evidence) = await fixture.SeedManualTransactionAsync(CategoryKind.Expense, 1_000);
        var handler = new CreateExpenseCommandHandler(CreateManualTransactionCreator(fixture));

        // Act: ghi khoản Chi tiêu 250 đồng qua CreateExpenseCommand.
        var result = await handler.Handle(
            new CreateExpenseCommand(
                user.Id,
                new CreateManualTransactionRequest(
                    wallet.Id,
                    category.Id,
                    250,
                    "Groceries",
                    IdempotencyKey: "expense-1",
                    SecurityPin: FinancialPin,
                    EvidenceIds: [evidence.Id])),
            default);

        fixture.Db.ChangeTracker.Clear();
        var storedWallet = await fixture.Db.Wallets.SingleAsync(x => x.Id == wallet.Id);
        var storedHistory = await fixture.Db.FinancialTransactions.SingleAsync(x => x.Id == result.Id);
        var entries = await fixture.Db.LedgerEntries
            .Where(x => x.LedgerTransactionId == storedHistory.LedgerTransactionId)
            .ToListAsync();

        // Assert: ví còn 750, lịch sử đúng 250 và Ledger vẫn cân bằng.
        Assert.Equal(750, storedWallet.AvailableBalance);
        Assert.Equal(250, storedHistory.Amount);
        Assert.Equal(
            entries.Where(x => x.Direction == LedgerEntryDirection.Debit).Sum(x => x.Amount),
            entries.Where(x => x.Direction == LedgerEntryDirection.Credit).Sum(x => x.Amount));
    }

    [Fact]
    public async Task Transaction_rule_matches_canonically_equivalent_vietnamese_text()
    {
        // Arrange: pattern dùng Unicode tách dấu (NFD), còn mô tả giao dịch dùng Unicode dựng sẵn (NFC).
        await using var fixture = await SqliteFixture.CreateAsync();
        var (user, wallet, originalCategory, evidence) = await fixture.SeedManualTransactionAsync(
            CategoryKind.Expense,
            1_000);
        var targetCategory = new Category
        {
            Key = $"test:{Guid.NewGuid():N}",
            Name = "Coffee",
            Kind = CategoryKind.Expense,
            OwnerUserId = user.Id
        };
        fixture.Db.Categories.Add(targetCategory);
        await fixture.Db.SaveChangesAsync();
        var decomposedPattern = "CA\u0300 PHE\u0302";
        var rule = await new CreateTransactionRuleCommandHandler(fixture.Db).Handle(
            new(user.Id, new CreateTransactionRuleRequest(decomposedPattern, targetCategory.Id)),
            default);

        // Act: rule phải nhận ra chuỗi nhìn giống hệt dù cách mã hóa Unicode khác nhau.
        var result = await new CreateExpenseCommandHandler(CreateManualTransactionCreator(fixture)).Handle(
            new(user.Id, new CreateManualTransactionRequest(
                wallet.Id,
                originalCategory.Id,
                100,
                "CÀ PHÊ sáng",
                IdempotencyKey: "unicode-rule",
                SecurityPin: FinancialPin,
                EvidenceIds: [evidence.Id])),
            default);

        Assert.Equal("CÀ PHÊ", rule.DescriptionPattern);
        Assert.Equal(targetCategory.Id, result.CategoryId);
    }

    [Fact]
    public async Task Manual_expense_rolls_back_wallet_and_ledger_when_history_insert_fails()
    {
        // Arrange: tạo trigger SQLite cố tình chặn INSERT FinancialTransaction để ép bước cuối thất bại.
        await using var fixture = await SqliteFixture.CreateAsync();
        var (user, wallet, category, evidence) = await fixture.SeedManualTransactionAsync(CategoryKind.Expense, 1_000);
        await fixture.Db.Database.ExecuteSqlRawAsync(
            """
            CREATE TRIGGER fail_financial_history
            BEFORE INSERT ON FinancialTransactions
            BEGIN
                SELECT RAISE(ABORT, 'forced history failure');
            END;
            """);
        var handler = new CreateExpenseCommandHandler(CreateManualTransactionCreator(fixture));

        // Act và Assert: handler phải ném lỗi; số dư, Ledger và lịch sử đều rollback về ban đầu.
        await Assert.ThrowsAsync<DbUpdateException>(() => handler.Handle(
            new CreateExpenseCommand(
                user.Id,
                new CreateManualTransactionRequest(
                    wallet.Id,
                    category.Id,
                    250,
                    "Groceries",
                    IdempotencyKey: "expense-failure",
                    SecurityPin: FinancialPin,
                    EvidenceIds: [evidence.Id])),
            default));

        fixture.Db.ChangeTracker.Clear();
        Assert.Equal(1_000, (await fixture.Db.Wallets.SingleAsync(x => x.Id == wallet.Id)).AvailableBalance);
        Assert.Empty(await fixture.Db.LedgerTransactions.ToListAsync());
        Assert.Empty(await fixture.Db.FinancialTransactions.ToListAsync());
    }

    [Fact]
    public async Task Manual_expense_retry_with_changed_details_is_rejected()
    {
        // Arrange: tạo ví và khoản Chi tiêu đầu tiên với IdempotencyKey cố định.
        await using var fixture = await SqliteFixture.CreateAsync();
        var (user, wallet, category, evidence) = await fixture.SeedManualTransactionAsync(
            CategoryKind.Expense,
            1_000);
        var handler = new CreateExpenseCommandHandler(CreateManualTransactionCreator(fixture));

        // Act: ghi request đầu tiên để tạo lịch sử và posting.
        await handler.Handle(
            new CreateExpenseCommand(
                user.Id,
                new CreateManualTransactionRequest(
                    wallet.Id,
                    category.Id,
                    250,
                    "Groceries",
                    IdempotencyKey: "expense-details",
                    SecurityPin: FinancialPin,
                    EvidenceIds: [evidence.Id])),
            default);

        // Assert: gửi lại cùng key nhưng đổi mô tả phải Conflict và không trừ tiền lần hai.
        var error = await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(
                new CreateExpenseCommand(
                    user.Id,
                    new CreateManualTransactionRequest(
                        wallet.Id,
                        category.Id,
                        250,
                        "Different purchase",
                        IdempotencyKey: "expense-details",
                        SecurityPin: FinancialPin,
                        EvidenceIds: [evidence.Id])),
                default));

        Assert.Equal("idempotency_conflict", error.Code);
        Assert.Equal(750, (await fixture.Db.Wallets.FindAsync(wallet.Id))!.AvailableBalance);
        Assert.Equal(1, await fixture.Db.LedgerTransactions.CountAsync());
        Assert.Equal(1, await fixture.Db.FinancialTransactions.CountAsync());
    }

    [Fact]
    public async Task Same_user_retry_replays_without_a_second_balance_change()
    {
        // Arrange: tạo hai tài khoản Ledger và dùng chung một scope của người dùng.
        await using var fixture = await SqliteFixture.CreateAsync();
        var source = Account(1_000);
        var destination = Account(0);
        fixture.Db.AddRange(source, destination);
        await fixture.Db.SaveChangesAsync();
        var ledger = new LedgerService(fixture.Db, fixture.Clock);

        // Act: gửi hai posting giống nhau với cùng IdempotencyKey.
        var first = await ledger.PostAsync(
            source.Id, destination.Id, 200, "VND", "Test", Guid.NewGuid(), "user:one", "same-key", "test", default);
        var second = await ledger.PostAsync(
            source.Id, destination.Id, 200, "VND", "Test", Guid.NewGuid(), "user:one", "same-key", "test", default);

        // Assert: lần hai là replay, dùng posting cũ và không thay đổi số dư thêm.
        Assert.False(first.Replayed);
        Assert.True(second.Replayed);
        Assert.Equal(first.LedgerTransactionId, second.LedgerTransactionId);
        Assert.Equal(800, source.Balance);
        Assert.Equal(200, destination.Balance);
        Assert.Equal(1, await fixture.Db.LedgerTransactions.CountAsync());
    }

    [Fact]
    public async Task Different_users_can_reuse_the_same_client_key()
    {
        // Arrange: tạo hai cặp tài khoản với hai scope người dùng khác nhau.
        await using var fixture = await SqliteFixture.CreateAsync();
        var sourceOne = Account(1_000);
        var destinationOne = Account(0);
        var sourceTwo = Account(1_000);
        var destinationTwo = Account(0);
        fixture.Db.AddRange(sourceOne, destinationOne, sourceTwo, destinationTwo);
        await fixture.Db.SaveChangesAsync();
        var ledger = new LedgerService(fixture.Db, fixture.Clock);

        // Act: hai người dùng gửi cùng chuỗi IdempotencyKey cho hai posting riêng.
        var first = await ledger.PostAsync(
            sourceOne.Id, destinationOne.Id, 100, "VND", "P2PTransfer", Guid.NewGuid(), "user:one:direct", "shared-key", "one", default);
        var second = await ledger.PostAsync(
            sourceTwo.Id, destinationTwo.Id, 150, "VND", "P2PTransfer", Guid.NewGuid(), "user:two:direct", "shared-key", "two", default);

        // Assert: cả hai posting đều mới vì scope khác nhau.
        Assert.False(first.Replayed);
        Assert.False(second.Replayed);
        Assert.NotEqual(first.LedgerTransactionId, second.LedgerTransactionId);
        Assert.Equal(2, await fixture.Db.LedgerTransactions.CountAsync());
    }

    [Fact]
    public async Task Reusing_a_key_with_different_details_is_rejected()
    {
        // Arrange: tạo posting đầu tiên với scope và IdempotencyKey cố định.
        await using var fixture = await SqliteFixture.CreateAsync();
        var source = Account(1_000);
        var destination = Account(0);
        fixture.Db.AddRange(source, destination);
        await fixture.Db.SaveChangesAsync();
        var ledger = new LedgerService(fixture.Db, fixture.Clock);
        // Act: ghi posting 100 đồng trước khi thử dùng lại key.
        await ledger.PostAsync(
            source.Id, destination.Id, 100, "VND", "Test", Guid.NewGuid(), "user:one", "same-key", "test", default);

        // Assert: cùng key nhưng đổi Amount phải Conflict và số dư chỉ thay đổi một lần.
        var error = await Assert.ThrowsAsync<ConflictException>(() => ledger.PostAsync(
            source.Id, destination.Id, 101, "VND", "Test", Guid.NewGuid(), "user:one", "same-key", "test", default));

        Assert.Equal("idempotency_conflict", error.Code);
        Assert.Equal(900, source.Balance);
        Assert.Equal(100, destination.Balance);
    }

 #if RETIRED_LEGACY_TESTS
    [Fact]
    public async Task Money_request_payment_rolls_back_transfer_when_request_completion_fails()
    {
        // Arrange: tạo trigger SQLite chặn MoneyRequest chuyển sang Paid sau khi luồng chuyển tiền chạy.
        await using var fixture = await SqliteFixture.CreateAsync();
        var (requester, requesterWallet) = await fixture.SeedUserWalletAsync("Requester", 0);
        var (payer, payerWallet) = await fixture.SeedUserWalletAsync("Payer", 5_000);
        var moneyRequest = new MoneyRequest
        {
            RequesterUserId = requester.Id,
            PayerUserId = payer.Id,
            Amount = 2_000,
            Description = "Shared bill"
        };
        fixture.Db.MoneyRequests.Add(moneyRequest);
        await fixture.Db.SaveChangesAsync();
        await fixture.Db.Database.ExecuteSqlRawAsync(
            """
            CREATE TRIGGER fail_money_request_completion
            BEFORE UPDATE ON MoneyRequests
            WHEN NEW.Status = 'Paid'
            BEGIN
                SELECT RAISE(ABORT, 'forced request completion failure');
            END;
            """);

        var policy = new OperationPolicy(fixture.Db);
        var notifications = new NotificationService(fixture.Db);
        var transfers = new TransferExecutor(
            fixture.Db,
            new LedgerService(fixture.Db, fixture.Clock),
            fixture.Clock,
            notifications,
            new RiskService(fixture.Db, policy, fixture.Clock),
            policy);
        var handler = new PayMoneyRequestCommandHandler(fixture.Db, transfers, policy);

        // Act và Assert: lỗi cập nhật trạng thái phải rollback ví, Transfer, Ledger, lịch sử và Notification.
        await Assert.ThrowsAsync<DbUpdateException>(() => handler.Handle(
            new PayMoneyRequestCommand(
                payer.Id,
                null,
                moneyRequest.Id,
                new PayMoneyRequest(payerWallet.Id, "pay-request-1")),
            default));

        fixture.Db.ChangeTracker.Clear();
        Assert.Equal(MoneyRequestStatus.Pending, (await fixture.Db.MoneyRequests.FindAsync(moneyRequest.Id))!.Status);
        Assert.Equal(5_000, (await fixture.Db.Wallets.FindAsync(payerWallet.Id))!.AvailableBalance);
        Assert.Equal(0, (await fixture.Db.Wallets.FindAsync(requesterWallet.Id))!.AvailableBalance);
        Assert.Empty(await fixture.Db.Transfers.ToListAsync());
        Assert.Empty(await fixture.Db.LedgerTransactions.ToListAsync());
        Assert.Empty(await fixture.Db.FinancialTransactions.ToListAsync());
        Assert.Empty(await fixture.Db.Notifications.ToListAsync());
    }

 #endif
    private static LedgerAccount Account(decimal balance) => new()
    {
        Code = Guid.NewGuid().ToString("N"),
        Name = "Account",
        Type = LedgerAccountType.Wallet,
        Balance = balance
    };

    private static ManualTransactionCreator CreateManualTransactionCreator(
        SqliteFixture fixture)
    {
        // Tạo ManualTransactionCreator bằng DbContext và LedgerService của cùng fixture.
        return new ManualTransactionCreator(
            fixture.Db,
            new LedgerService(fixture.Db, fixture.Clock),
            fixture.Clock,
            new FinancialSecurityGuard(fixture.Db, new SecurityPinService(),
                new AccountCredentialLockout(fixture.Db, fixture.Clock)),
            new EvidenceSupport(fixture.Db),
            new SpendWise.Application.Features.Banking.ActiveBankGuard(fixture.Db));
    }

    private sealed class SqliteFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;

        private SqliteFixture(SqliteConnection connection, SpendWiseDbContext db, IClock clock)
        {
            // Giữ kết nối mở suốt test để database SQLite in-memory không bị mất.
            this.connection = connection;
            Db = db;
            Clock = clock;
        }

        public SpendWiseDbContext Db { get; }
        public IClock Clock { get; }

        public static async Task<SqliteFixture> CreateAsync()
        {
            // Mở SQLite in-memory, tạo schema thật và trả fixture dùng chung cho test.
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<SpendWiseDbContext>()
                .UseSqlite(connection)
                .Options;
            var clock = new SystemClock();
            var db = new SpendWiseDbContext(options, clock);
            await db.Database.EnsureCreatedAsync();
            return new SqliteFixture(connection, db, clock);
        }

        public async Task<(User User, Wallet Wallet, Category Category, EvidenceAttachment Evidence)> SeedManualTransactionAsync(
            CategoryKind kind,
            decimal balance)
        {
            // Tạo User, Wallet có số dư và Category phù hợp cho test Thu/Chi thủ công.
            var user = new User
            {
                FullName = "Relational User",
                Email = $"{Guid.NewGuid():N}@test.local",
                NormalizedEmail = Guid.NewGuid().ToString("N"),
                SpendId = Guid.NewGuid().ToString("N"),
                NormalizedSpendId = Guid.NewGuid().ToString("N"),
                PasswordHash = "test",
                SecurityPinHash = FinancialPinHash
            };
            var wallet = WalletFactory.Create(user.Id, "Main", WalletType.Main, true);
            wallet.AvailableBalance = balance;
            wallet.LedgerAccount.Balance = balance;
            var category = new Category
            {
                Key = $"test:{Guid.NewGuid():N}",
                Name = kind.ToString(),
                Kind = kind,
                IsSystem = true
            };
            var evidence = new EvidenceAttachment
            {
                OwnerUserId = user.Id,
                OriginalFileName = "receipt.png",
                ContentType = "image/png",
                Length = 8,
                ContentHash = "test-hash",
                Content = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]
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
            Db.AddRange(user, wallet, category, evidence, bank);
            await Db.SaveChangesAsync();
            return (user, wallet, category, evidence);
        }

        public async Task<(User User, Wallet Wallet)> SeedUserWalletAsync(string name, decimal balance)
        {
            // Tạo một User cùng Wallet/Ledger có số dư ban đầu được chỉ định.
            var user = new User
            {
                FullName = name,
                Email = $"{Guid.NewGuid():N}@test.local",
                NormalizedEmail = Guid.NewGuid().ToString("N"),
                SpendId = Guid.NewGuid().ToString("N"),
                NormalizedSpendId = Guid.NewGuid().ToString("N"),
                PasswordHash = "test"
            };
            var wallet = WalletFactory.Create(user.Id, "Main", WalletType.Main, true);
            wallet.AvailableBalance = balance;
            wallet.LedgerAccount.Balance = balance;
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
            Db.AddRange(user, wallet, bank);
            await Db.SaveChangesAsync();
            return (user, wallet);
        }

        public async ValueTask DisposeAsync()
        {
            // Đóng DbContext và kết nối SQLite sau khi test hoàn tất.
            await Db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    private sealed class NullRealtime : IRealtimeNotifier
    {
        public Task SendToUserAsync(
            Guid userId,
            string eventName,
            object payload,
            CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class TestCurrentUser : ICurrentUser
    {
        // Danh tính giả không đại diện người đăng nhập vì các test gọi Handler trực tiếp.
        public Guid? UserId => null;
        public string SystemRole => "User";
        public string Locale => "vi-VN";
        public string? SessionId => null;
    }
}
