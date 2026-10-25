using System.Text.Json;
using Microsoft.Data.Sqlite;
using SpendWise.Application.Features;
using SpendWise.Application.Features.Authentication;
using SpendWise.Application.Features.BankIntegration.Commands.ProcessLifecycleEvent;
using SpendWise.Application.Features.Wallets;
using SpendWise.Infrastructure.Finance;
using SpendWise.Infrastructure.Identity;
using SpendWise.Infrastructure.Notifications;
using System.Net;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Banking;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.BankConnections;
using SpendWise.Application.Features.BankConnections.Commands.CreateBankLink;
using SpendWise.Application.Features.BankConnections.Commands.UnlinkBankConnection;
using SpendWise.Application.Features.Banking;
using SpendWise.Application.Features.Wallets.Commands.CreateWallet;
using SpendWise.Application.Finance;
using SpendWise.Domain.Banking;
using SpendWise.Domain.Finance;
using SpendWise.Domain.Identity;
using SpendWise.Infrastructure.Persistence;
using Xunit;

namespace SpendWise.Tests;

public sealed class BankLinkingAndWalletGuardTests
{
    private sealed class TestClock(DateTime now) : IClock
    {
        public DateTime UtcNow { get; } = now;
    }

    private static SpendWiseDbContext CreateInMemoryDb(IClock? clock = null)
    {
        var options = new DbContextOptionsBuilder<SpendWiseDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SpendWiseDbContext(options, clock ?? new TestClock(DateTime.UtcNow));
    }

    private static async Task<(SqliteConnection Connection, SpendWiseDbContext Db)> CreateSqliteDbAsync(IClock? clock = null)
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<SpendWiseDbContext>()
            .UseSqlite(connection)
            .Options;
        var db = new SpendWiseDbContext(options, clock ?? new TestClock(DateTime.UtcNow));
        await db.Database.EnsureCreatedAsync();
        return (connection, db);
    }

    [Fact]
    public async Task ActiveBankGuard_ThrowsWhenUserHasNoActiveBank()
    {
        using var db = CreateInMemoryDb();
        var guard = new ActiveBankGuard(db);
        var userId = Guid.NewGuid();

        var ex = await Assert.ThrowsAsync<ConflictException>(() => guard.EnsureActiveBankAsync(userId, CancellationToken.None));
        Assert.Equal("active_bank_required", ex.Code);
        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task ActiveBankGuard_PassesWhenUserHasActiveBank()
    {
        using var db = CreateInMemoryDb();
        var userId = Guid.NewGuid();
        db.BankConnections.Add(new BankConnection
        {
            UserId = userId,
            Provider = BankProvider.SePayBankHub,
            ExternalAccountId = "acc_001",
            Status = BankConnectionStatus.Active
        });
        await db.SaveChangesAsync();

        var guard = new ActiveBankGuard(db);
        await guard.EnsureActiveBankAsync(userId, CancellationToken.None);
    }

    [Fact]
    public async Task CreateWallet_ThrowsWhenNoActiveBank()
    {
        using var db = CreateInMemoryDb();
        var userId = Guid.NewGuid();
        var guard = new ActiveBankGuard(db);
        var handler = new CreateWalletCommandHandler(db, guard);

        var command = new CreateWalletCommand(userId, new CreateWalletRequest("My Wallet", "Spending", false));
        var ex = await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Equal("active_bank_required", ex.Code);
    }

    [Fact]
    public async Task BankUnlinkSafety_BlocksUnlinkWhenWalletBalanceGreaterThanZero()
    {
        using var db = CreateInMemoryDb();
        var userId = Guid.NewGuid();
        var wallet = new Wallet
        {
            OwnerUserId = userId,
            Name = "Wallet 1",
            Status = WalletStatus.Active,
            Currency = "VND"
        };
        db.Wallets.Add(wallet);
        var ledgerAccount = new LedgerAccount
        {
            WalletId = wallet.Id,
            Type = LedgerAccountType.Wallet,
            Balance = 50000m,
            Currency = "VND"
        };
        db.LedgerAccounts.Add(ledgerAccount);
        await db.SaveChangesAsync();

        var validator = new BankUnlinkSafetyValidator(db);
        var ex = await Assert.ThrowsAsync<ConflictException>(() => validator.ValidateCanUnlinkAsync(userId, CancellationToken.None));
        Assert.Equal("unlink_balance_not_zero", ex.Code);
    }

    [Fact]
    public async Task BankUnlinkSafety_BlocksUnlinkWhenPendingTopUpExists()
    {
        using var db = CreateInMemoryDb();
        var userId = Guid.NewGuid();
        var wallet = new Wallet { OwnerUserId = userId, Name = "Wallet 1", Status = WalletStatus.Active };
        db.Wallets.Add(wallet);
        var ledgerAccount = new LedgerAccount { WalletId = wallet.Id, Type = LedgerAccountType.Wallet, Balance = 0m, Currency = "VND" };
        db.LedgerAccounts.Add(ledgerAccount);

        var connection = new BankConnection { UserId = userId, Provider = BankProvider.SePayBankHub, ExternalAccountId = "acc1", Status = BankConnectionStatus.Active };
        db.BankConnections.Add(connection);

        db.WalletTopUps.Add(new WalletTopUp
        {
            UserId = userId,
            BankConnectionId = connection.Id,
            WalletId = wallet.Id,
            Amount = 100000m,
            Status = WalletTopUpStatus.Pending,
            Reference = "REF1",
            ExpiresAt = DateTime.UtcNow.AddMinutes(10)
        });
        await db.SaveChangesAsync();

        var validator = new BankUnlinkSafetyValidator(db);
        var ex = await Assert.ThrowsAsync<ConflictException>(() => validator.ValidateCanUnlinkAsync(userId, CancellationToken.None));
        Assert.Equal("unlink_pending_topup_exists", ex.Code);
    }

    [Fact]
    public async Task BankUnlinkSafety_BlocksUnlinkWhenPendingWithdrawalExists()
    {
        using var db = CreateInMemoryDb();
        var userId = Guid.NewGuid();
        var wallet = new Wallet { OwnerUserId = userId, Name = "Wallet 1", Status = WalletStatus.Active };
        db.Wallets.Add(wallet);
        var ledgerAccount = new LedgerAccount { WalletId = wallet.Id, Type = LedgerAccountType.Wallet, Balance = 0m, Currency = "VND" };
        db.LedgerAccounts.Add(ledgerAccount);

        var connection = new BankConnection { UserId = userId, Provider = BankProvider.SePayBankHub, ExternalAccountId = "acc1", Status = BankConnectionStatus.Active };
        db.BankConnections.Add(connection);

        db.WalletWithdrawals.Add(new WalletWithdrawal
        {
            UserId = userId,
            BankConnectionId = connection.Id,
            WalletId = wallet.Id,
            Amount = 50000m,
            Status = WalletWithdrawalStatus.Pending,
            Reference = "WD1"
        });
        await db.SaveChangesAsync();

        var validator = new BankUnlinkSafetyValidator(db);
        var ex = await Assert.ThrowsAsync<ConflictException>(() => validator.ValidateCanUnlinkAsync(userId, CancellationToken.None));
        Assert.Equal("unlink_pending_withdrawal_exists", ex.Code);
    }

    [Fact]
    public async Task UnlinkBankConnection_TransitionsWalletsToRestricted()
    {
        var clock = new TestClock(DateTime.UtcNow);
        using var db = CreateInMemoryDb(clock);
        var userId = Guid.NewGuid();
        var connection = new BankConnection
        {
            UserId = userId,
            Provider = BankProvider.SePayBankHub,
            AccountEnvironment = SePayAccountEnvironment.TestMode,
            ExternalAccountId = "acc_test",
            Status = BankConnectionStatus.Active
        };
        db.BankConnections.Add(connection);

        var wallet = new Wallet
        {
            OwnerUserId = userId,
            Name = "My Wallet",
            Status = WalletStatus.Active,
            Currency = "VND"
        };
        db.Wallets.Add(wallet);
        var ledgerAccount = new LedgerAccount { WalletId = wallet.Id, Type = LedgerAccountType.Wallet, Balance = 0m, Currency = "VND" };
        db.LedgerAccounts.Add(ledgerAccount);
        await db.SaveChangesAsync();

        var validator = new BankUnlinkSafetyValidator(db);
        var dummyGateway = new DummyGateway();
        var dummyConfig = new DummyConfig();

        var handler = new UnlinkBankConnectionCommandHandler(db, dummyGateway, dummyConfig, clock, validator);
        var request = new CreateBankLinkRequest("https://example.com/done", "vi", BankLinkClientPlatform.Web);
        await handler.Handle(new UnlinkBankConnectionCommand(userId, connection.Id, request, true), CancellationToken.None);

        Assert.Equal(BankConnectionStatus.Unlinked, connection.Status);
        var updatedWallet = await db.Wallets.SingleAsync(w => w.Id == wallet.Id);
        Assert.Equal(WalletStatus.Restricted, updatedWallet.Status);
    }

    [Fact]
    public async Task Relink_UserBCanLinkAfterUserAUnlinks()
    {
        var clock = new TestClock(DateTime.UtcNow);
        using var db = CreateInMemoryDb(clock);

        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        // User A had linked account, then unlinked
        var providerId = Guid.NewGuid();
        var connA = new BankConnection
        {
            UserId = userA,
            Provider = BankProvider.SePayBankHub,
            AccountEnvironment = SePayAccountEnvironment.TestMode,
            ExternalAccountId = providerId.ToString(),
            Status = BankConnectionStatus.Unlinked,
            UnlinkedAt = clock.UtcNow.AddDays(-1)
        };
        db.BankConnections.Add(connA);
        await db.SaveChangesAsync();

        // User B starts and confirms link for account
        var challenge = new BankLinkSession
        {
            UserId = userB,
            Provider = BankProvider.SePayBankHub,
            AccountEnvironment = SePayAccountEnvironment.TestMode,
            Purpose = BankLinkPurpose.LinkBankAccount,
            ExternalLinkTokenXid = "SWLINK123",
            Status = BankLinkSessionStatus.Pending,
            ExpiresAt = clock.UtcNow.AddMinutes(10)
        };
        db.BankLinkSessions.Add(challenge);
        await db.SaveChangesAsync();

        var fakeProvider = new FakeSePayAccountReader(
            [new SePayTestModeAccountDto(providerId, "Vietcombank", "9876", "Saving", "Active", 1000000m)],
            [new SePayTestModeTransactionDto(Guid.NewGuid(), providerId, "out", 0m, 50000m, 950000m, "SWLINK123", "SWLINK123", null, clock.UtcNow)]
        );

        var confirmHandler = new ConfirmTestModeBankLinkCommandHandler(db, fakeProvider, clock);
        var result = await confirmHandler.Handle(new ConfirmTestModeBankLinkCommand(true, userB, challenge.Id), CancellationToken.None);

        Assert.NotNull(result);
        var bConnections = await db.BankConnections.Where(b => b.UserId == userB).ToListAsync();
        Assert.Single(bConnections);
        Assert.Equal(BankConnectionStatus.Active, bConnections[0].Status);
        Assert.Equal(providerId.ToString(), bConnections[0].ExternalAccountId);

        // Verify User A connection is still preserved with Unlinked status
        var aConnection = await db.BankConnections.SingleAsync(b => b.UserId == userA);
        Assert.Equal(BankConnectionStatus.Unlinked, aConnection.Status);
    }

    private sealed class DummyGateway : IBankHubGateway
    {
        public Task<BankProviderLinkResult> CreateLinkAsync(BankProviderLinkRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new BankProviderLinkResult("tok", "https://link", DateTime.UtcNow.AddHours(1)));
        public Task<ExternalBankTransactionPage> GetTransactionsAsync(string externalBankAccountId, DateTime from, DateTime to, int page, int pageSize, CancellationToken cancellationToken) =>
            throw new NotImplementedException();
        public Task<SandboxTransactionResult> CreateSandboxTransactionAsync(string externalBankAccountId, BankTransactionDirection direction, decimal amount, string content, CancellationToken cancellationToken) =>
            Task.FromResult(new SandboxTransactionResult(true, "test-prov-id", content, DateTime.UtcNow));
    }

    private sealed class DummyConfig : IBankHubConfiguration
    {
        public bool IsSandbox => true;
        public void EnsureAvailable() { }
        public string ResolveCompletionRedirectUri(string? requestedUri) => requestedUri ?? "";
        public string ResolveMobileCompletionRedirectUri(string? requestedUri) => requestedUri ?? "";
        public bool VerifyLifecycleSecret(string suppliedSecret) => true;
        public bool VerifyIpnAuthorization(string authorizationHeader) => true;
        public SePayBankHubStatusDto GetSafeStatus() => new(true, "Sandbox", true, true, true, true, null, null);
        public void MarkWebhookReceived(DateTime receivedAt) { }
    }

    private sealed class FakeSePayAccountReader(
        IReadOnlyList<SePayTestModeAccountDto> accounts,
        IReadOnlyList<SePayTestModeTransactionDto> transactions) : ISePayTestModeAccountReader
    {
        public Task<IReadOnlyList<SePayTestModeAccountDto>> GetAccountsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(accounts);
        public Task<IReadOnlyList<SePayTestModeTransactionDto>> GetTransactionsAsync(string pattern, CancellationToken cancellationToken) =>
            Task.FromResult(transactions);
    }

    [Fact]
    public async Task Withdrawal_Reservation_HoldsWalletBalance_BeforeProviderCall_AndCompletes()
    {
        var clock = new TestClock(DateTime.UtcNow);
        using var db = CreateInMemoryDb(clock);
        var ledger = new LedgerService(db, clock);
        var security = new FinancialSecurityGuard(db, new SecurityPinService(), new AccountCredentialLockout(db, clock));
        var guard = new ActiveBankGuard(db);

        var user = new User
        {
            FullName = "Withdrawal User",
            Email = "wd@test.local",
            NormalizedEmail = "WD@TEST.LOCAL",
            SpendId = "wd_user",
            NormalizedSpendId = "WD_USER",
            PasswordHash = "hash",
            SecurityPinHash = new SecurityPinService().Hash("123456")
        };
        db.Users.Add(user);

        var bankConn = new BankConnection
        {
            UserId = user.Id,
            Provider = BankProvider.SePayBankHub,
            AccountEnvironment = SePayAccountEnvironment.BankHub,
            ExternalAccountId = "acc_wd_01",
            BankBrandName = "MBBank",
            AccountHolderName = user.FullName,
            MaskedAccountNumber = "****1234",
            Status = BankConnectionStatus.Active,
            LinkedAt = clock.UtcNow
        };
        db.BankConnections.Add(bankConn);

        var wallet = WalletFactory.Create(user.Id, "Main", WalletType.Main, true);
        wallet.AvailableBalance = 100_000m;
        wallet.LedgerAccount.Balance = 100_000m;
        db.Wallets.Add(wallet);
        await db.SaveChangesAsync();

        var gateway = new ConfigurableGateway
        {
            Result = new SandboxTransactionResult(
                Success: true,
                ProviderTransactionId: "sepay_tx_123",
                ReferenceNumber: "REF-001",
                TransactionDate: clock.UtcNow,
                Amount: 40_000m)
        };

        var handler = new CreateWalletWithdrawalCommandHandler(db, gateway, ledger, clock, security, guard);
        var command = new CreateWalletWithdrawalCommand(
            true,
            user.Id,
            new CreateWalletWithdrawalRequest(bankConn.Id, wallet.Id, 40_000m, "123456", "idemp-wd-01"));

        var dto = await handler.Handle(command, CancellationToken.None);

        Assert.Equal("Completed", dto.Status);
        Assert.Equal(40_000m, dto.Amount);

        // Verify withdrawal record
        var wd = await db.WalletWithdrawals.SingleAsync(w => w.Id == dto.Id);
        Assert.Equal(WalletWithdrawalStatus.Completed, wd.Status);
        Assert.NotNull(wd.ReservationLedgerTransactionId);
        Assert.NotNull(wd.LedgerTransactionId);

        // Verify wallet ledger balance is decremented to 60,000
        var updatedWallet = await db.Wallets.SingleAsync(w => w.Id == wallet.Id);
        var updatedLedger = await db.LedgerAccounts.SingleAsync(a => a.WalletId == wallet.Id && a.Type == LedgerAccountType.Wallet);
        Assert.Equal(60_000m, updatedWallet.AvailableBalance);
        Assert.Equal(60_000m, updatedLedger.Balance);

        // Verify FinancialTransaction
        var ft = await db.FinancialTransactions.SingleAsync(f => f.RelatedBusinessId == wd.Id);
        Assert.Equal(FinancialTransactionType.TransferOut, ft.Type);
        Assert.Equal(40_000m, ft.Amount);
    }

    [Fact]
    public async Task Withdrawal_TimeoutOrUnknown_MovesToPendingReconciliation_AndMaintainsHold()
    {
        var clock = new TestClock(DateTime.UtcNow);
        using var db = CreateInMemoryDb(clock);
        var ledger = new LedgerService(db, clock);
        var security = new FinancialSecurityGuard(db, new SecurityPinService(), new AccountCredentialLockout(db, clock));
        var guard = new ActiveBankGuard(db);

        var user = new User
        {
            FullName = "Timeout User",
            Email = "timeout@test.local",
            NormalizedEmail = "TIMEOUT@TEST.LOCAL",
            SpendId = "timeout_user",
            NormalizedSpendId = "TIMEOUT_USER",
            PasswordHash = "hash",
            SecurityPinHash = new SecurityPinService().Hash("123456")
        };
        db.Users.Add(user);

        var bankConn = new BankConnection
        {
            UserId = user.Id,
            Provider = BankProvider.SePayBankHub,
            AccountEnvironment = SePayAccountEnvironment.BankHub,
            ExternalAccountId = "acc_wd_timeout",
            BankBrandName = "MBBank",
            AccountHolderName = user.FullName,
            MaskedAccountNumber = "****1234",
            Status = BankConnectionStatus.Active,
            LinkedAt = clock.UtcNow
        };
        db.BankConnections.Add(bankConn);

        var wallet = WalletFactory.Create(user.Id, "Main", WalletType.Main, true);
        wallet.AvailableBalance = 100_000m;
        wallet.LedgerAccount.Balance = 100_000m;
        db.Wallets.Add(wallet);
        await db.SaveChangesAsync();

        var gateway = new ConfigurableGateway
        {
            Result = new SandboxTransactionResult(
                Success: false,
                ProviderTransactionId: null,
                ReferenceNumber: null,
                TransactionDate: null,
                Amount: 40_000m,
                ErrorCode: "gateway_timeout",
                ErrorMessage: "Gateway timed out waiting for provider response",
                IsTimeoutOrUnknown: true)
        };

        var handler = new CreateWalletWithdrawalCommandHandler(db, gateway, ledger, clock, security, guard);
        var command = new CreateWalletWithdrawalCommand(
            true,
            user.Id,
            new CreateWalletWithdrawalRequest(bankConn.Id, wallet.Id, 40_000m, "123456", "idemp-wd-timeout"));

        var ex = await Assert.ThrowsAsync<AppException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Equal("withdrawal_pending_reconciliation", ex.Code);
        Assert.Equal(504, ex.StatusCode);

        // Verify withdrawal status in DB is PendingReconciliation
        var wd = await db.WalletWithdrawals.SingleAsync();
        Assert.Equal(WalletWithdrawalStatus.PendingReconciliation, wd.Status);
        Assert.NotNull(wd.ReservationLedgerTransactionId);
        Assert.Null(wd.LedgerTransactionId);

        // Verify wallet ledger balance is held (reduced to 60,000)
        var updatedWallet = await db.Wallets.SingleAsync(w => w.Id == wallet.Id);
        var updatedLedger = await db.LedgerAccounts.SingleAsync(a => a.WalletId == wallet.Id && a.Type == LedgerAccountType.Wallet);
        Assert.Equal(60_000m, updatedWallet.AvailableBalance);
        Assert.Equal(60_000m, updatedLedger.Balance);

        // Verify Hold Account has 40,000
        var holdAccount = await db.LedgerAccounts.SingleAsync(a => a.Code == "SYSTEM:ReservationHold:VND");
        Assert.Equal(40_000m, holdAccount.Balance);

        // Trying to withdraw 70,000 now fails with insufficient_wallet_balance
        var command2 = new CreateWalletWithdrawalCommand(
            true,
            user.Id,
            new CreateWalletWithdrawalRequest(bankConn.Id, wallet.Id, 70_000m, "123456", "idemp-wd-overdraft"));
        var ex2 = await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(command2, CancellationToken.None));
        Assert.Equal("insufficient_wallet_balance", ex2.Code);
    }

    [Fact]
    public async Task Withdrawal_DefinitiveFailure_ReleasesHold_BackToWallet()
    {
        var clock = new TestClock(DateTime.UtcNow);
        using var db = CreateInMemoryDb(clock);
        var ledger = new LedgerService(db, clock);
        var security = new FinancialSecurityGuard(db, new SecurityPinService(), new AccountCredentialLockout(db, clock));
        var guard = new ActiveBankGuard(db);

        var user = new User
        {
            FullName = "Failed User",
            Email = "failed@test.local",
            NormalizedEmail = "FAILED@TEST.LOCAL",
            SpendId = "failed_user",
            NormalizedSpendId = "FAILED_USER",
            PasswordHash = "hash",
            SecurityPinHash = new SecurityPinService().Hash("123456")
        };
        db.Users.Add(user);

        var bankConn = new BankConnection
        {
            UserId = user.Id,
            Provider = BankProvider.SePayBankHub,
            AccountEnvironment = SePayAccountEnvironment.BankHub,
            ExternalAccountId = "acc_wd_fail",
            BankBrandName = "MBBank",
            AccountHolderName = user.FullName,
            MaskedAccountNumber = "****1234",
            Status = BankConnectionStatus.Active,
            LinkedAt = clock.UtcNow
        };
        db.BankConnections.Add(bankConn);

        var wallet = WalletFactory.Create(user.Id, "Main", WalletType.Main, true);
        wallet.AvailableBalance = 100_000m;
        wallet.LedgerAccount.Balance = 100_000m;
        db.Wallets.Add(wallet);
        await db.SaveChangesAsync();

        var gateway = new ConfigurableGateway
        {
            Result = new SandboxTransactionResult(
                Success: false,
                ProviderTransactionId: null,
                ReferenceNumber: null,
                TransactionDate: null,
                Amount: 40_000m,
                ErrorCode: "insufficient_provider_balance",
                ErrorMessage: "Provider declined transaction",
                IsTimeoutOrUnknown: false)
        };

        var handler = new CreateWalletWithdrawalCommandHandler(db, gateway, ledger, clock, security, guard);
        var command = new CreateWalletWithdrawalCommand(
            true,
            user.Id,
            new CreateWalletWithdrawalRequest(bankConn.Id, wallet.Id, 40_000m, "123456", "idemp-wd-fail"));

        var ex = await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Equal("withdrawal_failed", ex.Code);

        // Verify withdrawal status in DB is Failed
        var wd = await db.WalletWithdrawals.SingleAsync();
        Assert.Equal(WalletWithdrawalStatus.Failed, wd.Status);
        Assert.NotNull(wd.ReservationLedgerTransactionId);
        Assert.NotNull(wd.FailedAt);

        // Verify wallet ledger balance is restored to 100,000!
        var updatedWallet = await db.Wallets.SingleAsync(w => w.Id == wallet.Id);
        var updatedLedger = await db.LedgerAccounts.SingleAsync(a => a.WalletId == wallet.Id && a.Type == LedgerAccountType.Wallet);
        Assert.Equal(100_000m, updatedWallet.AvailableBalance);
        Assert.Equal(100_000m, updatedLedger.Balance);

        // Verify Hold Account is 0
        var holdAccount = await db.LedgerAccounts.SingleAsync(a => a.Code == "SYSTEM:ReservationHold:VND");
        Assert.Equal(0m, holdAccount.Balance);
    }

    [Fact]
    public async Task BankLinking_OnlyReactivates_NoActiveBank_RestrictedWallets()
    {
        var clock = new TestClock(DateTime.UtcNow);
        using var db = CreateInMemoryDb(clock);

        var user = new User
        {
            FullName = "Reactivate User",
            Email = "react@test.local",
            NormalizedEmail = "REACT@TEST.LOCAL",
            SpendId = "react_user",
            NormalizedSpendId = "REACT_USER",
            PasswordHash = "hash"
        };
        db.Users.Add(user);

        var w1 = WalletFactory.Create(user.Id, "Wallet 1", WalletType.Main, true);
        w1.Status = WalletStatus.Restricted;
        w1.RestrictionReason = WalletRestrictionReason.NoActiveBank;

        var w2 = WalletFactory.Create(user.Id, "Wallet 2", WalletType.Savings, false);
        w2.Status = WalletStatus.Restricted;
        w2.RestrictionReason = WalletRestrictionReason.AdminAction;

        db.Wallets.AddRange(w1, w2);

        var providerId = Guid.NewGuid();
        var challenge = new BankLinkSession
        {
            UserId = user.Id,
            Provider = BankProvider.SePayBankHub,
            AccountEnvironment = SePayAccountEnvironment.TestMode,
            Purpose = BankLinkPurpose.LinkBankAccount,
            ExternalLinkTokenXid = "LINK_CHALLENGE",
            Status = BankLinkSessionStatus.Pending,
            ExpiresAt = clock.UtcNow.AddMinutes(10)
        };
        db.BankLinkSessions.Add(challenge);
        await db.SaveChangesAsync();

        var fakeProvider = new FakeSePayAccountReader(
            [new SePayTestModeAccountDto(providerId, "MBBank", "1234", "Saving", "Active", 500000m)],
            [new SePayTestModeTransactionDto(Guid.NewGuid(), providerId, "out", 0m, 50000m, 450000m, "LINK_CHALLENGE", "LINK_CHALLENGE", null, clock.UtcNow)]
        );

        var confirmHandler = new ConfirmTestModeBankLinkCommandHandler(db, fakeProvider, clock);
        await confirmHandler.Handle(new ConfirmTestModeBankLinkCommand(true, user.Id, challenge.Id), CancellationToken.None);

        // Wallet 1 must be reactivated
        var checkW1 = await db.Wallets.SingleAsync(w => w.Id == w1.Id);
        Assert.Equal(WalletStatus.Active, checkW1.Status);
        Assert.Equal(WalletRestrictionReason.None, checkW1.RestrictionReason);

        // Wallet 2 must REMAIN Restricted with AdminAction
        var checkW2 = await db.Wallets.SingleAsync(w => w.Id == w2.Id);
        Assert.Equal(WalletStatus.Restricted, checkW2.Status);
        Assert.Equal(WalletRestrictionReason.AdminAction, checkW2.RestrictionReason);
    }

    [Fact]
    public async Task DelayedWebhook_ForAlreadyUnlinkedConnection_DoesNotMutateNewOwner()
    {
        var clock = new TestClock(DateTime.UtcNow);
        var (connection, db) = await CreateSqliteDbAsync(clock);
        await using var _ = connection;
        await using var __ = db;

        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var externalAccId = "acc_shared_ext";

        var uA = new User { Id = userA, FullName = "User A", Email = "a@test.local", NormalizedEmail = "A@TEST.LOCAL", SpendId = "user_a", NormalizedSpendId = "USER_A", PasswordHash = "hash" };
        var uB = new User { Id = userB, FullName = "User B", Email = "b@test.local", NormalizedEmail = "B@TEST.LOCAL", SpendId = "user_b", NormalizedSpendId = "USER_B", PasswordHash = "hash" };
        db.Users.AddRange(uA, uB);

        // User A was unlinked in the past
        var connA = new BankConnection
        {
            UserId = userA,
            Provider = BankProvider.SePayBankHub,
            AccountEnvironment = SePayAccountEnvironment.TestMode,
            ExternalAccountId = externalAccId,
            Status = BankConnectionStatus.Unlinked,
            UnlinkedAt = clock.UtcNow.AddDays(-2)
        };
        db.BankConnections.Add(connA);

        // User B has active connection now
        var connB = new BankConnection
        {
            UserId = userB,
            Provider = BankProvider.SePayBankHub,
            AccountEnvironment = SePayAccountEnvironment.TestMode,
            ExternalAccountId = externalAccId,
            Status = BankConnectionStatus.Active,
            LinkedAt = clock.UtcNow.AddDays(-1)
        };
        db.BankConnections.Add(connB);

        var walletB = WalletFactory.Create(userB, "Wallet B", WalletType.Main, true);
        walletB.Status = WalletStatus.Active;
        db.Wallets.Add(walletB);
        await db.SaveChangesAsync();

        var handler = new ProcessLifecycleEventCommandHandler(db, new NotificationService(db), clock, new DummyConfig());
        var metadata = JsonDocument.Parse($"{{\"bank_account_xid\":\"{externalAccId}\"}}").RootElement;
        var evt = new SePayLifecycleEventRequest(
            DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            "evt_delayed_unlinked",
            "1.0",
            "BANK_ACCOUNT_UNLINKED",
            metadata);

        var result = await handler.Handle(new ProcessLifecycleEventCommand(evt), CancellationToken.None);

        Assert.True(result.Success);

        // User B's active connection and wallet must NOT be touched!
        var freshConnB = await db.BankConnections.SingleAsync(c => c.Id == connB.Id);
        Assert.Equal(BankConnectionStatus.Active, freshConnB.Status);

        var freshWalletB = await db.Wallets.SingleAsync(w => w.Id == walletB.Id);
        Assert.Equal(WalletStatus.Active, freshWalletB.Status);

        var recordedEvent = await db.BankIntegrationEvents.SingleAsync(e => e.ExternalEventId == "evt_delayed_unlinked");
        Assert.Equal(BankIntegrationEventStatus.Quarantined, recordedEvent.ProcessingStatus);
        Assert.Equal("quarantined_ambiguous_ownership", recordedEvent.FailureCode);
    }

    [Theory]
    [InlineData(WalletWithdrawalStatus.Reserved)]
    [InlineData(WalletWithdrawalStatus.Processing)]
    [InlineData(WalletWithdrawalStatus.PendingReconciliation)]
    public async Task BankUnlinkSafety_BlocksUnlink_WhenInFlightWithdrawalExists(WalletWithdrawalStatus inFlightStatus)
    {
        using var db = CreateInMemoryDb();
        var userId = Guid.NewGuid();
        var wallet = new Wallet { OwnerUserId = userId, Name = "Wallet 1", Status = WalletStatus.Active };
        db.Wallets.Add(wallet);
        var ledgerAccount = new LedgerAccount { WalletId = wallet.Id, Type = LedgerAccountType.Wallet, Balance = 0m, Currency = "VND" };
        db.LedgerAccounts.Add(ledgerAccount);

        var connection = new BankConnection { UserId = userId, Provider = BankProvider.SePayBankHub, ExternalAccountId = "acc1", Status = BankConnectionStatus.Active };
        db.BankConnections.Add(connection);

        db.WalletWithdrawals.Add(new WalletWithdrawal
        {
            UserId = userId,
            BankConnectionId = connection.Id,
            WalletId = wallet.Id,
            Amount = 50000m,
            Status = inFlightStatus,
            Reference = "WD_IN_FLIGHT"
        });
        await db.SaveChangesAsync();

        var validator = new BankUnlinkSafetyValidator(db);
        var ex = await Assert.ThrowsAsync<ConflictException>(() => validator.ValidateCanUnlinkAsync(userId, CancellationToken.None));
        Assert.Equal("unlink_pending_withdrawal_exists", ex.Code);
    }

    private sealed class ConfigurableGateway : IBankHubGateway
    {
        public SandboxTransactionResult Result { get; set; } = new(true);
        public Task<BankProviderLinkResult> CreateLinkAsync(BankProviderLinkRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new BankProviderLinkResult("tok", "https://link", DateTime.UtcNow.AddHours(1)));
        public Task<ExternalBankTransactionPage> GetTransactionsAsync(string externalBankAccountId, DateTime from, DateTime to, int page, int pageSize, CancellationToken cancellationToken) =>
            throw new NotImplementedException();
        public Task<SandboxTransactionResult> CreateSandboxTransactionAsync(string externalBankAccountId, BankTransactionDirection direction, decimal amount, string content, CancellationToken cancellationToken) =>
            Task.FromResult(Result);
    }
}
