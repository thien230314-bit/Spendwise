using System.Data.Common;
using System.Text.Json;
using MediatR;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Banking;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features;
using SpendWise.Application.Features.Authentication;
using SpendWise.Application.Features.BankConnections;
using SpendWise.Application.Features.Banking;
using SpendWise.Application.Features.BankIntegration.Commands.ProcessLifecycleEvent;
using SpendWise.Application.Features.Transfers.Commands.P2PTransfer;
using SpendWise.Application.Features.V1Groups;
using SpendWise.Application.Finance;
using SpendWise.Application.Identity;
using SpendWise.Domain.Banking;
using SpendWise.Domain.Finance;
using SpendWise.Domain.Identity;
using SpendWise.Domain.V1Groups;
using SpendWise.Infrastructure.Finance;
using SpendWise.Infrastructure.Identity;
using SpendWise.Infrastructure.Notifications;
using SpendWise.Infrastructure.Persistence;
using SpendWise.Infrastructure.Services;
using Xunit;

namespace SpendWise.Tests;

public sealed class SqlServerRealFinancialConcurrencyTests
{
    private const string TargetServer = @"DESKTOP-B5KA3LT\SQLEXPRESS02";
    private const string TargetDatabase = "SpendWise_E2E_G4";
    private const string ConnectionString =
        $@"Server={TargetServer};Database={TargetDatabase};Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;Pooling=False";
    private const string DefaultPin = "123456";

    private static SpendWiseDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SpendWiseDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;
        return new SpendWiseDbContext(options, new SystemClock());
    }

    private static FinancialSecurityGuard CreateSecurity(SpendWiseDbContext db, IClock clock)
    {
        return new FinancialSecurityGuard(db, new SecurityPinService(), new AccountCredentialLockout(db, clock));
    }

    private static ILedgerService CreateLedger(SpendWiseDbContext db, IClock clock)
    {
        return new LedgerService(db, clock);
    }

    [Fact]
    public async Task C01_ConcurrentWithdrawals_PreventsDoubleReserveAndOverdraft()
    {
        var runId = Guid.NewGuid().ToString("N")[..8];
        var clock = new SystemClock();

        await using var seedDb = CreateDbContext();
        var user = await SeedUserWithActiveBankAsync(seedDb, clock, runId, 500_000m);
        var bankConn = await seedDb.BankConnections.SingleAsync(b => b.UserId == user.Id && b.Status == BankConnectionStatus.Active);
        var wallet = await seedDb.Wallets.SingleAsync(w => w.OwnerUserId == user.Id);

        var barrier = new Barrier(2);
        var withdrawalAmount = 400_000m;

        var mockGateway = new MockSuccessGateway();

        async Task<(bool Success, string? ErrorCode)> ExecuteWithdrawalAsync(string idempotencyKey)
        {
            await Task.Yield();
            barrier.SignalAndWait();

            await using var db = CreateDbContext();
            var handler = new CreateWalletWithdrawalCommandHandler(
                db,
                mockGateway,
                CreateLedger(db, clock),
                clock,
                CreateSecurity(db, clock),
                new ActiveBankGuard(db));

            try
            {
                var cmd = new CreateWalletWithdrawalCommand(
                    IsDevelopment: true,
                    UserId: user.Id,
                    Request: new CreateWalletWithdrawalRequest(
                        BankConnectionId: bankConn.Id,
                        WalletId: wallet.Id,
                        Amount: withdrawalAmount,
                        SecurityPin: DefaultPin,
                        IdempotencyKey: idempotencyKey));

                var res = await handler.Handle(cmd, CancellationToken.None);
                return (res.Status == "Completed", null);
            }
            catch (ConflictException ex)
            {
                return (false, ex.Code);
            }
            catch (AppException ex)
            {
                return (false, ex.Code);
            }
        }

        var task1 = Task.Run(() => ExecuteWithdrawalAsync($"c01_req1_{runId}"));
        var task2 = Task.Run(() => ExecuteWithdrawalAsync($"c01_req2_{runId}"));

        var results = await Task.WhenAll(task1, task2);

        // Verification on SQL02
        await using var verifyDb = CreateDbContext();
        var freshWallet = await verifyDb.Wallets.SingleAsync(w => w.Id == wallet.Id);
        var withdrawals = await verifyDb.WalletWithdrawals.Where(w => w.UserId == user.Id).ToListAsync();

        Assert.Single(results, r => r.Success);
        Assert.Single(results, r => !r.Success);
        var failedResult = results.Single(r => !r.Success);
        Assert.True(
            failedResult.ErrorCode is "insufficient_wallet_balance" or "concurrent_financial_operation",
            $"Unexpected error code: {failedResult.ErrorCode}");

        // Available balance must never be negative
        Assert.True(freshWallet.AvailableBalance >= 0);
        Assert.Equal(100_000m, freshWallet.AvailableBalance);

        // Exactly one completed withdrawal
        Assert.Single(withdrawals, w => w.Status == WalletWithdrawalStatus.Completed);

        // Ledger balance check
        var walletLedger = await verifyDb.LedgerAccounts.SingleAsync(a => a.OwnerUserId == user.Id && a.WalletId == wallet.Id && a.Currency == "VND");
        Assert.Equal(100_000m, walletLedger.Balance);
    }

    [Fact]
    public async Task C02_ConcurrentWithdrawalAndP2P_PreventsDoubleSpend()
    {
        var runId = Guid.NewGuid().ToString("N")[..8];
        var clock = new SystemClock();

        await using var seedDb = CreateDbContext();
        var sender = await SeedUserWithActiveBankAsync(seedDb, clock, $"s_{runId}", 500_000m);
        var recipient = await SeedUserWithActiveBankAsync(seedDb, clock, $"r_{runId}", 0m);

        var bankConn = await seedDb.BankConnections.SingleAsync(b => b.UserId == sender.Id && b.Status == BankConnectionStatus.Active);
        var senderWallet = await seedDb.Wallets.SingleAsync(w => w.OwnerUserId == sender.Id);
        var recipientWallet = await seedDb.Wallets.SingleAsync(w => w.OwnerUserId == recipient.Id);

        var barrier = new Barrier(2);
        var mockGateway = new MockSuccessGateway();

        async Task<(string Operation, bool Success, string? Error)> ExecuteWithdrawal()
        {
            await Task.Yield();
            barrier.SignalAndWait();

            await using var db = CreateDbContext();
            var handler = new CreateWalletWithdrawalCommandHandler(
                db,
                mockGateway,
                CreateLedger(db, clock),
                clock,
                CreateSecurity(db, clock),
                new ActiveBankGuard(db));

            try
            {
                var cmd = new CreateWalletWithdrawalCommand(
                    IsDevelopment: true,
                    UserId: sender.Id,
                    Request: new CreateWalletWithdrawalRequest(
                        BankConnectionId: bankConn.Id,
                        WalletId: senderWallet.Id,
                        Amount: 400_000m,
                        SecurityPin: DefaultPin,
                        IdempotencyKey: $"c02_wd_{runId}"));

                var res = await handler.Handle(cmd, CancellationToken.None);
                return ("Withdrawal", res.Status == "Completed", null);
            }
            catch (Exception ex)
            {
                return ("Withdrawal", false, ex.Message);
            }
        }

        async Task<(string Operation, bool Success, string? Error)> ExecuteP2P()
        {
            await Task.Yield();
            barrier.SignalAndWait();

            await using var db = CreateDbContext();
            var handler = new P2PTransferCommandHandler(
                db,
                CreateLedger(db, clock),
                clock,
                new AllowAllOperationPolicy(),
                CreateSecurity(db, clock),
                new ActiveBankGuard(db));

            try
            {
                var cmd = new P2PTransferCommand(
                    SenderUserId: sender.Id,
                    Request: new P2PTransferRequest(
                        SourceWalletId: senderWallet.Id,
                        RecipientUserId: recipient.Id,
                        Amount: 300_000m,
                        Description: "P2P Concurrency Test",
                        IdempotencyKey: $"c02_p2p_{runId}",
                        SecurityPin: DefaultPin));

                await handler.Handle(cmd, CancellationToken.None);
                return ("P2P", true, null);
            }
            catch (Exception ex)
            {
                return ("P2P", false, ex.Message);
            }
        }

        var task1 = Task.Run(ExecuteWithdrawal);
        var task2 = Task.Run(ExecuteP2P);

        var results = await Task.WhenAll(task1, task2);

        // Verification
        await using var verifyDb = CreateDbContext();
        var freshSenderWallet = await verifyDb.Wallets.SingleAsync(w => w.Id == senderWallet.Id);
        var freshRecipientWallet = await verifyDb.Wallets.SingleAsync(w => w.Id == recipientWallet.Id);

        // Exactly one should succeed, one should fail due to insufficient funds (400k + 300k = 700k > 500k)
        Assert.Single(results, r => r.Success);
        Assert.Single(results, r => !r.Success);

        // Balance must never be negative
        Assert.True(freshSenderWallet.AvailableBalance >= 0);

        if (results.Single(r => r.Operation == "Withdrawal").Success)
        {
            Assert.Equal(100_000m, freshSenderWallet.AvailableBalance);
            Assert.Equal(0m, freshRecipientWallet.AvailableBalance);
        }
        else
        {
            Assert.Equal(200_000m, freshSenderWallet.AvailableBalance);
            Assert.Equal(300_000m, freshRecipientWallet.AvailableBalance);
        }
    }

    [Fact]
    public async Task C03_ConcurrentWithdrawalAndFundContribution_PreservesAvailableBalance()
    {
        var runId = Guid.NewGuid().ToString("N")[..8];
        var clock = new SystemClock();

        await using var seedDb = CreateDbContext();
        var user = await SeedUserWithActiveBankAsync(seedDb, clock, $"f_{runId}", 500_000m);
        var bankConn = await seedDb.BankConnections.SingleAsync(b => b.UserId == user.Id && b.Status == BankConnectionStatus.Active);
        var wallet = await seedDb.Wallets.SingleAsync(w => w.OwnerUserId == user.Id);

        // Seed Fund and Membership
        var fund = new V1Fund($"Fund {runId}", $"CODE{runId}", V1FundKind.Free, user.Id, null);
        seedDb.V1Funds.Add(fund);

        var fundLedger = new LedgerAccount
        {
            Id = Guid.NewGuid(),
            Code = $"FUND:{fund.Id:N}:VND",
            Name = "Fund Ledger",
            Type = LedgerAccountType.SharedFund,
            Currency = "VND",
            IsSystem = false
        };
        seedDb.LedgerAccounts.Add(fundLedger);

        var membership = new V1FundMembership
        {
            Id = Guid.NewGuid(),
            FundId = fund.Id,
            UserId = user.Id,
            Seat = 1,
            CanSpend = true,
            JoinedAt = clock.UtcNow
        };
        seedDb.V1FundMemberships.Add(membership);
        await seedDb.SaveChangesAsync();

        var barrier = new Barrier(2);
        var mockGateway = new MockSuccessGateway();

        async Task<(string Operation, bool Success, string? Error)> ExecuteWithdrawal()
        {
            await Task.Yield();
            barrier.SignalAndWait();

            await using var db = CreateDbContext();
            var handler = new CreateWalletWithdrawalCommandHandler(
                db,
                mockGateway,
                CreateLedger(db, clock),
                clock,
                CreateSecurity(db, clock),
                new ActiveBankGuard(db));

            try
            {
                var cmd = new CreateWalletWithdrawalCommand(
                    IsDevelopment: true,
                    UserId: user.Id,
                    Request: new CreateWalletWithdrawalRequest(
                        BankConnectionId: bankConn.Id,
                        WalletId: wallet.Id,
                        Amount: 400_000m,
                        SecurityPin: DefaultPin,
                        IdempotencyKey: $"c03_wd_{runId}"));

                var res = await handler.Handle(cmd, CancellationToken.None);
                return ("Withdrawal", res.Status == "Completed", null);
            }
            catch (Exception ex)
            {
                return ("Withdrawal", false, ex.Message);
            }
        }

        async Task<(string Operation, bool Success, string? Error)> ExecuteContribution()
        {
            await Task.Yield();
            barrier.SignalAndWait();

            await using var db = CreateDbContext();
            var fundService = new V1FundService(
                db,
                clock,
                CreateSecurity(db, clock),
                new NotificationService(db),
                CreateLedger(db, clock),
                new ActiveBankGuard(db));

            try
            {
                var req = new V1ContributeRequest(wallet.Id, 300_000L, $"c03_contrib_{runId}", DefaultPin);
                await fundService.ContributeAsync(user.Id, fund.Id, req, CancellationToken.None);
                return ("Contribution", true, null);
            }
            catch (Exception ex)
            {
                return ("Contribution", false, ex.Message);
            }
        }

        var task1 = Task.Run(ExecuteWithdrawal);
        var task2 = Task.Run(ExecuteContribution);

        var results = await Task.WhenAll(task1, task2);

        // Verification
        await using var verifyDb = CreateDbContext();
        var freshWallet = await verifyDb.Wallets.SingleAsync(w => w.Id == wallet.Id);

        Assert.Single(results, r => r.Success);
        Assert.Single(results, r => !r.Success);
        Assert.True(freshWallet.AvailableBalance >= 0);
    }

    [Fact]
    public async Task C04_ConcurrentBankLinking_EnforcesSingleActiveOwnership()
    {
        var runId = Guid.NewGuid().ToString("N")[..8];
        var sharedExternalAccId = Guid.NewGuid().ToString();

        await using var seedDb = CreateDbContext();
        var clock = new SystemClock();
        var userA = await SeedUserWithActiveBankAsync(seedDb, clock, $"ba_{runId}", 0m, createBank: false);
        var userB = await SeedUserWithActiveBankAsync(seedDb, clock, $"bb_{runId}", 0m, createBank: false);

        var barrier = new Barrier(2);

        async Task<(Guid UserId, bool Success, string? Error)> LinkBankAsync(Guid userId)
        {
            await Task.Yield();
            barrier.SignalAndWait();

            await using var db = CreateDbContext();
            try
            {
                var conn = new BankConnection
                {
                    UserId = userId,
                    Provider = BankProvider.SePayBankHub,
                    AccountEnvironment = SePayAccountEnvironment.BankHub,
                    ExternalAccountId = sharedExternalAccId,
                    BankBrandName = "Test Bank",
                    MaskedAccountNumber = "****8888",
                    Status = BankConnectionStatus.Active,
                    LinkedAt = clock.UtcNow
                };
                db.BankConnections.Add(conn);
                await db.SaveChangesAsync();
                return (userId, true, null);
            }
            catch (DbUpdateException ex)
            {
                return (userId, false, ex.Message);
            }
        }

        var task1 = Task.Run(() => LinkBankAsync(userA.Id));
        var task2 = Task.Run(() => LinkBankAsync(userB.Id));

        var results = await Task.WhenAll(task1, task2);

        // Verification: Exactly 1 succeeds, 1 fails with unique constraint
        await using var verifyDb = CreateDbContext();
        var activeConnections = await verifyDb.BankConnections
            .Where(b => b.Provider == BankProvider.SePayBankHub
                && b.AccountEnvironment == SePayAccountEnvironment.BankHub
                && b.ExternalAccountId == sharedExternalAccId
                && b.Status == BankConnectionStatus.Active)
            .ToListAsync();

        Assert.Single(results, r => r.Success);
        Assert.Single(results, r => !r.Success);
        Assert.Single(activeConnections);
    }

    [Fact]
    public async Task C05_ConcurrentUnlinkAndFinancialMutation_PreventsOrphanReservation()
    {
        var runId = Guid.NewGuid().ToString("N")[..8];
        var clock = new SystemClock();

        await using var seedDb = CreateDbContext();
        var user = await SeedUserWithActiveBankAsync(seedDb, clock, $"u5_{runId}", 500_000m);
        var bankConn = await seedDb.BankConnections.SingleAsync(b => b.UserId == user.Id && b.Status == BankConnectionStatus.Active);
        var wallet = await seedDb.Wallets.SingleAsync(w => w.OwnerUserId == user.Id);

        var barrier = new Barrier(2);
        var mockGateway = new MockSuccessGateway();

        async Task<(string Operation, bool Success, string? Error)> UnlinkAsync()
        {
            await Task.Yield();
            barrier.SignalAndWait();

            await using var db = CreateDbContext();
            var handler = new ProcessLifecycleEventCommandHandler(
                db, new NotificationService(db), clock, new DummyBankHubConfig());

            try
            {
                var metadata = JsonDocument.Parse($@"{{""bank_account_xid"":""{bankConn.ExternalAccountId}""}}").RootElement;
                var evt = new SePayLifecycleEventRequest(
                    DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    $"evt_unlink_{runId}",
                    "1.0",
                    "BANK_ACCOUNT_UNLINKED",
                    metadata);

                var ack = await handler.Handle(new ProcessLifecycleEventCommand(evt), CancellationToken.None);
                return ("Unlink", ack.Success, null);
            }
            catch (Exception ex)
            {
                return ("Unlink", false, ex.Message);
            }
        }

        async Task<(string Operation, bool Success, string? Error)> WithdrawAsync()
        {
            await Task.Yield();
            barrier.SignalAndWait();

            await using var db = CreateDbContext();
            var handler = new CreateWalletWithdrawalCommandHandler(
                db,
                mockGateway,
                CreateLedger(db, clock),
                clock,
                CreateSecurity(db, clock),
                new ActiveBankGuard(db));

            try
            {
                var cmd = new CreateWalletWithdrawalCommand(
                    IsDevelopment: true,
                    UserId: user.Id,
                    Request: new CreateWalletWithdrawalRequest(
                        BankConnectionId: bankConn.Id,
                        WalletId: wallet.Id,
                        Amount: 200_000m,
                        SecurityPin: DefaultPin,
                        IdempotencyKey: $"c05_wd_{runId}"));

                var res = await handler.Handle(cmd, CancellationToken.None);
                return ("Withdraw", res.Status == "Completed", null);
            }
            catch (Exception ex)
            {
                return ("Withdraw", false, ex.Message);
            }
        }

        var task1 = Task.Run(UnlinkAsync);
        var task2 = Task.Run(WithdrawAsync);

        var results = await Task.WhenAll(task1, task2);

        // Verification
        await using var verifyDb = CreateDbContext();
        var freshConn = await verifyDb.BankConnections.SingleAsync(b => b.Id == bankConn.Id);
        var freshWallet = await verifyDb.Wallets.SingleAsync(w => w.Id == wallet.Id);
        var withdrawals = await verifyDb.WalletWithdrawals.Where(w => w.UserId == user.Id).ToListAsync();

        // No orphaned reservations: either 0 or completed
        var pendingReservations = withdrawals.Where(w => w.Status == WalletWithdrawalStatus.Reserved).ToList();
        Assert.Empty(pendingReservations);

        // Invariants check: AvailableBalance matches LedgerBalance
        var walletLedger = await verifyDb.LedgerAccounts.SingleAsync(a => a.OwnerUserId == user.Id && a.WalletId == wallet.Id && a.Currency == "VND");
        Assert.Equal(freshWallet.AvailableBalance, walletLedger.Balance);
    }

    [Fact]
    public async Task C06_ConcurrentReconciliation_GuaranteesExactlyOncePosting()
    {
        var runId = Guid.NewGuid().ToString("N")[..8];
        var clock = new SystemClock();

        await using var seedDb = CreateDbContext();
        var user = await SeedUserWithActiveBankAsync(seedDb, clock, $"u6_{runId}", 500_000m);
        var bankConn = await seedDb.BankConnections.SingleAsync(b => b.UserId == user.Id && b.Status == BankConnectionStatus.Active);
        var wallet = await seedDb.Wallets.SingleAsync(w => w.OwnerUserId == user.Id);
        var walletLedger = await seedDb.LedgerAccounts.SingleAsync(a => a.OwnerUserId == user.Id && a.WalletId == wallet.Id && a.Currency == "VND");

        // Seed a withdrawal in PendingReconciliation state with money held in ReservationHold account
        var holdAccount = await WalletWithdrawalHelpers.GetOrCreateReservationHoldAccountAsync(seedDb, CancellationToken.None);
        var ledgerService = CreateLedger(seedDb, clock);
        var amount = 200_000m;

        // Post hold
        var holdPosting = await ledgerService.PostAsync(
            walletLedger.Id,
            holdAccount.Id,
            amount,
            "VND",
            "WithdrawalReservation",
            wallet.Id,
            $"wd-hold:{runId}",
            $"wd-hold:{runId}",
            "Hold for withdrawal",
            CancellationToken.None);

        wallet.AvailableBalance -= amount;

        var barrier = new Barrier(2);
        var providerTxId = $"tx_prov_{runId}";

        var withdrawal = new WalletWithdrawal
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            BankConnectionId = bankConn.Id,
            WalletId = wallet.Id,
            Amount = amount,
            Currency = "VND",
            Status = WalletWithdrawalStatus.PendingReconciliation,
            Reference = $"WD{runId}",
            IdempotencyKey = $"c06_reconcile_{runId}",
            ReservationLedgerTransactionId = holdPosting.LedgerTransactionId,
            ProviderResponseData = $"{{\"transaction_id\":\"{providerTxId}\",\"amount\":{amount},\"externalBankAccountId\":\"{bankConn.ExternalAccountId}\",\"status\":\"SUCCESS\"}}",
            CreatedAt = clock.UtcNow
        };
        seedDb.WalletWithdrawals.Add(withdrawal);
        await seedDb.SaveChangesAsync();

        async Task<(int WorkerId, bool Success, string Status)> ReconcileAsync(int workerId)
        {
            await Task.Yield();
            barrier.SignalAndWait();

            await using var db = CreateDbContext();
            var handler = new ReconcileWalletWithdrawalCommandHandler(db, CreateLedger(db, clock), clock);

            var cmd = new ReconcileWalletWithdrawalCommand(
                WithdrawalId: withdrawal.Id,
                Action: WithdrawalReconciliationAction.FinalizeSuccess,
                ProviderTransactionId: providerTxId);

            var res = await handler.Handle(cmd, CancellationToken.None);
            return (workerId, true, res.Status);
        }

        var task1 = Task.Run(() => ReconcileAsync(1));
        var task2 = Task.Run(() => ReconcileAsync(2));

        var results = await Task.WhenAll(task1, task2);

        // Verification on SQL02
        await using var verifyDb = CreateDbContext();
        var finalWithdrawal = await verifyDb.WalletWithdrawals.SingleAsync(w => w.Id == withdrawal.Id);
        Assert.Equal(WalletWithdrawalStatus.Completed, finalWithdrawal.Status);
        Assert.Equal(providerTxId, finalWithdrawal.ProviderTransactionId);

        // Exactly-once terminal posting: exactly 1 financial transaction created for this withdrawal
        var financialTxCount = await verifyDb.FinancialTransactions
            .CountAsync(ft => ft.RelatedBusinessId == withdrawal.Id);
        Assert.Equal(1, financialTxCount);

        // Ledger postings for this withdrawal: exactly 1 reservation hold + exactly 1 terminal settlement
        var postings = await verifyDb.LedgerTransactions
            .Where(lt => lt.BusinessId == withdrawal.Id || lt.BusinessId == wallet.Id && lt.BusinessType == "WithdrawalReservation")
            .ToListAsync();
        var settlementPosting = postings.Single(p => p.BusinessType == "WalletWithdrawal");
        Assert.NotNull(settlementPosting);
        var entry = await verifyDb.LedgerEntries.FirstAsync(e => e.LedgerTransactionId == settlementPosting.Id && e.LedgerAccountId == holdAccount.Id);
        Assert.Equal(amount, entry.Amount);
    }

    [Fact]
    public async Task C07_MigrationLegacyCompatibility_MaterializesAllWalletsWithoutException()
    {
        await using var db = CreateDbContext();

        // Query all wallets in SQL02 to verify that EF Core materializes them cleanly without exception
        var wallets = await db.Wallets.AsNoTracking().ToListAsync();

        Assert.True(wallets.Count >= 257, $"Expected at least 257 wallets in SQL02, found {wallets.Count}");

        // Assert all legacy wallets have valid, non-null, non-empty enum values
        foreach (var w in wallets)
        {
            Assert.True(Enum.IsDefined(typeof(WalletStatus), w.Status), $"Invalid wallet status: {w.Status}");
            Assert.True(Enum.IsDefined(typeof(WalletRestrictionReason), w.RestrictionReason), $"Invalid restriction reason: {w.RestrictionReason}");
        }

        // Specifically check the legacy rows
        var activeWallets = wallets.Where(w => w.Status == WalletStatus.Active).ToList();
        var closedWallets = wallets.Where(w => w.Status == WalletStatus.Closed).ToList();

        Assert.True(activeWallets.Count >= 216);
        Assert.True(closedWallets.Count >= 41);

        // None of the active or closed wallets should have invalid string; they must be None
        Assert.All(activeWallets, w => Assert.Equal(WalletRestrictionReason.None, w.RestrictionReason));
        Assert.All(closedWallets, w => Assert.Equal(WalletRestrictionReason.None, w.RestrictionReason));
    }

    private static async Task<User> SeedUserWithActiveBankAsync(
        SpendWiseDbContext db,
        IClock clock,
        string prefix,
        decimal initialBalance,
        bool createBank = true)
    {
        var userId = Guid.NewGuid();
        var walletId = Guid.NewGuid();

        var user = new User
        {
            Id = userId,
            FullName = $"Concurrency User {prefix}",
            Email = $"{prefix}@test.local",
            NormalizedEmail = $"{prefix.ToUpperInvariant()}@TEST.LOCAL",
            SpendId = $"sp_{prefix}",
            NormalizedSpendId = $"SP_{prefix.ToUpperInvariant()}",
            PasswordHash = new PasswordService().Hash("TestPass123!"),
            SecurityPinHash = new SecurityPinService().Hash(DefaultPin),
            Status = UserStatus.Active,
            CreatedAt = clock.UtcNow
        };
        db.Users.Add(user);

        var wallet = new Wallet
        {
            Id = walletId,
            OwnerUserId = userId,
            Name = "Main Wallet",
            Type = WalletType.Main,
            Currency = "VND",
            Status = WalletStatus.Active,
            RestrictionReason = WalletRestrictionReason.None,
            IsDefault = true,
            AvailableBalance = initialBalance,
            CreatedAt = clock.UtcNow
        };
        db.Wallets.Add(wallet);

        var walletLedger = new LedgerAccount
        {
            Id = Guid.NewGuid(),
            Code = $"W:{walletId:N}:VND",
            Name = "Wallet Ledger",
            Type = LedgerAccountType.Wallet,
            Currency = "VND",
            OwnerUserId = userId,
            WalletId = walletId,
            IsSystem = false
        };
        db.LedgerAccounts.Add(walletLedger);

        if (createBank)
        {
            var bankConn = new BankConnection
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Provider = BankProvider.SePayBankHub,
                AccountEnvironment = SePayAccountEnvironment.BankHub,
                ExternalAccountId = $"acc_{Guid.NewGuid():N}",
                BankBrandName = "MBBank",
                MaskedAccountNumber = "****1234",
                Status = BankConnectionStatus.Active,
                LinkedAt = clock.UtcNow,
                CreatedAt = clock.UtcNow
            };
            db.BankConnections.Add(bankConn);
        }

        await db.SaveChangesAsync();

        if (initialBalance > 0)
        {
            var settlement = await WalletWithdrawalHelpers.GetOrCreateExternalSettlementAccountAsync(db, CancellationToken.None);
            var ledger = CreateLedger(db, clock);
            await ledger.PostAsync(
                settlement.Id,
                walletLedger.Id,
                initialBalance,
                "VND",
                "InitialSeed",
                walletId,
                $"seed:{prefix}",
                $"seed:{prefix}",
                "Seed initial balance",
                CancellationToken.None);
        }

        return user;
    }

    private sealed class MockSuccessGateway : IBankHubGateway
    {
        public Task<BankProviderLinkResult> CreateLinkAsync(BankProviderLinkRequest request, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<ExternalBankTransactionPage> GetTransactionsAsync(string externalBankAccountId, DateTime from, DateTime to, int page, int pageSize, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<SandboxTransactionResult> CreateSandboxTransactionAsync(string externalBankAccountId, BankTransactionDirection direction, decimal amount, string content, CancellationToken cancellationToken)
        {
            return Task.FromResult(new SandboxTransactionResult(
                Success: true,
                ProviderTransactionId: $"tx_mock_{Guid.NewGuid():N}",
                ReferenceNumber: $"REF_{Guid.NewGuid():N}"[..12],
                TransactionDate: DateTime.UtcNow,
                Amount: amount,
                Direction: direction.ToString(),
                ExternalBankAccountId: externalBankAccountId,
                ProviderStatus: "Success",
                ErrorCode: null,
                ErrorMessage: null,
                IsTimeoutOrUnknown: false,
                RawResponseData: "{\"code\":201,\"data\":{\"status\":\"success\"}}"));
        }
    }

    private sealed class DummyBankHubConfig : IBankHubConfiguration
    {
        public bool IsSandbox => true;
        public void EnsureAvailable() { }
        public string ResolveCompletionRedirectUri(string? requestedUri) => requestedUri ?? "https://localhost";
        public string ResolveMobileCompletionRedirectUri(string? requestedUri) => requestedUri ?? "https://localhost";
        public bool VerifyLifecycleSecret(string suppliedSecret) => true;
        public bool VerifyIpnAuthorization(string authorizationHeader) => true;
        public SePayBankHubStatusDto GetSafeStatus() => new(true, "Sandbox", true, true, true, true, null, null);
        public void MarkWebhookReceived(DateTime receivedAt) { }
    }

    private sealed class AllowAllOperationPolicy : IOperationPolicy
    {
        public Task EnsureFeatureEnabledAsync(string featureName, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<decimal> GetDecimalAsync(string key, decimal fallback, CancellationToken cancellationToken) => Task.FromResult(fallback);
    }
}
