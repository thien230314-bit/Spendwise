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
        public Task CreateSandboxTransactionAsync(string externalBankAccountId, BankTransactionDirection direction, decimal amount, string content, CancellationToken cancellationToken) =>
            Task.CompletedTask;
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
}
