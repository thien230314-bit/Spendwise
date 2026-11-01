using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features;
using SpendWise.Application.Features.Authentication;
using SpendWise.Application.Features.Banking;
using SpendWise.Application.Features.V1Groups;
using SpendWise.Application.Features.Wallets;
using SpendWise.Application.Identity;
using SpendWise.Domain.Banking;
using SpendWise.Domain.Finance;
using SpendWise.Domain.Identity;
using SpendWise.Domain.Social;
using SpendWise.Domain.V1Groups;
using SpendWise.Infrastructure.Finance;
using SpendWise.Infrastructure.Identity;
using SpendWise.Infrastructure.Notifications;
using SpendWise.Infrastructure.Persistence;
using SpendWise.Infrastructure.Services;
using Xunit;

namespace SpendWise.Tests;

public sealed class FixedFundV2Stage5Tests : SqlTestFixtureScope
{
    private const string Pin = TargetedCorrectnessFixture.SecurityPin;

    private sealed class MutableClock(DateTime initial) : IClock
    {
        public DateTime UtcNow { get; set; } = initial;
        public void Advance(TimeSpan d) => UtcNow += d;
    }

    private sealed class QualifiedInactivityEvidence : IV1InactivityEvidence
    {
        public Task<V1InactivityProof> GetAsync(Guid userId, CancellationToken ct) =>
            Task.FromResult(new V1InactivityProof(true, true, "Synthetic-qualified-inactivity-proof"));
    }

    private sealed class Stage5SqlDatabase : IAsyncDisposable
    {
        private readonly string name = "SpendWise_FinancialS05_Test_" + Guid.NewGuid().ToString("N");
        private readonly string connection;
        private bool ownsCatalog;
        public MutableClock Clock { get; }

        public Stage5SqlDatabase(MutableClock clock)
        {
            Clock = clock;
            connection = SqlTestFixtureLifecycle.CreateOwnedConnectionString(name,
                Environment.GetEnvironmentVariable(SqlTestFixtureLifecycle.InstanceEnvironmentVariable) ?? @"DESKTOP-B5KA3LT\SQLEXPRESS02");
        }

        public SpendWiseDbContext Context(params IInterceptor[] interceptors) => new(
            new DbContextOptionsBuilder<SpendWiseDbContext>().UseSqlServer(connection).AddInterceptors(interceptors).Options, Clock);

        public static async Task<Stage5SqlDatabase> CreateAsync(MutableClock clock)
        {
            var owned = new Stage5SqlDatabase(clock);
            SqlTestFixtureLifecycle.ValidateOwnedConnection(owned.connection);
            await SqlTestFixtureLifecycle.CreateOwnedCatalogAsync(owned.connection);
            owned.ownsCatalog = true;
            return await SqlTestFixtureLifecycle.CreateAsync(async () =>
            {
                await using var db = owned.Context();
                await db.Database.EnsureCreatedAsync();
                await SqlTestFixtureLifecycle.ConfigureOwnedCatalogAsync(owned.connection);
                return owned;
            }, owned.DisposeAsync);
        }

        public async ValueTask DisposeAsync()
        {
            if (ownsCatalog)
            {
                await using var db = Context();
                await db.Database.EnsureDeletedAsync();
                ownsCatalog = false;
            }
        }
    }

    private sealed class LockContentionGate(bool pauseAfterLock = false) : DbCommandInterceptor
    {
        public TaskCompletionSource ExecutingReached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource LockAcquired { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private int executingTriggered;
        private int executedTriggered;

        private static bool IsFundLockCommand(string commandText) =>
            commandText.Contains("UPDATE", StringComparison.OrdinalIgnoreCase) &&
            commandText.Contains("V1Funds", StringComparison.OrdinalIgnoreCase) &&
            commandText.Contains("UpdatedAt", StringComparison.OrdinalIgnoreCase);

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            System.Data.Common.DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (IsFundLockCommand(command.CommandText) && Interlocked.Exchange(ref executingTriggered, 1) == 0)
            {
                ExecutingReached.TrySetResult();
            }
            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override async ValueTask<int> NonQueryExecutedAsync(
            System.Data.Common.DbCommand command,
            CommandExecutedEventData eventData,
            int result,
            CancellationToken cancellationToken = default)
        {
            if (IsFundLockCommand(command.CommandText) && Interlocked.Exchange(ref executedTriggered, 1) == 0)
            {
                LockAcquired.TrySetResult();
                if (pauseAfterLock)
                {
                    await Release.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
                }
            }
            return await base.NonQueryExecutedAsync(command, eventData, result, cancellationToken);
        }
    }

    private sealed class TransactionCommitGate(bool pauseBeforeCommit = false) : DbTransactionInterceptor
    {
        public TaskCompletionSource HoldingLock { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int triggered;

        public override async ValueTask<InterceptionResult> TransactionCommittingAsync(
            System.Data.Common.DbTransaction transaction,
            TransactionEventData eventData,
            InterceptionResult result,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref triggered, 1) == 0)
            {
                HoldingLock.TrySetResult();
                if (pauseBeforeCommit)
                {
                    await Release.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
                }
            }
            return await base.TransactionCommittingAsync(transaction, eventData, result, cancellationToken);
        }
    }

    private static V1FundService CreateService(SpendWiseDbContext db, IClock clock, IV1InactivityEvidence? evidence = null)
    {
        var security = new FinancialSecurityGuard(db, new SecurityPinService(), new AccountCredentialLockout(db, clock));
        var notifications = new NotificationService(db);
        var ledger = new LedgerService(db, clock);
        var activeBank = new ActiveBankGuard(db);
        var options = Options.Create(new V1ElectionPolicy
        {
            Version = "STAGE5-TEST-POLICY",
            RegistrationSeconds = 60,
            NominationSeconds = 60,
            RunoffSeconds = 60,
            ConfirmationSeconds = 60,
            AcceptanceSeconds = 60,
            FreezeMembership = true,
            AllowContributions = true,
            NoCandidatesOutcome = "Fail",
            NoVotesOutcome = "Fail",
            AcceptanceExpiryOutcome = "Fail",
            EligibilityChangeOutcome = "Fail"
        });
        return new V1FundService(db, clock, security, notifications, ledger, activeBank, options, evidence ?? new QualifiedInactivityEvidence());
    }

    private static async Task<User> SeedUserAsync(SpendWiseDbContext db, string name, DateTime? emailVerifiedAt = null)
    {
        var unique = Guid.NewGuid().ToString("N");
        var email = $"{unique}@test.local";
        var user = new User
        {
            FullName = name,
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            SpendId = unique,
            NormalizedSpendId = unique.ToUpperInvariant(),
            PasswordHash = "test-hash",
            SecurityPinHash = new SecurityPinService().Hash(Pin),
            Status = UserStatus.Active,
            SystemRole = SystemRole.User,
            EmailVerifiedAt = emailVerifiedAt ?? DateTime.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var bankConnection = new BankConnection
        {
            UserId = user.Id,
            Provider = BankProvider.SePayBankHub,
            AccountEnvironment = SePayAccountEnvironment.TestMode,
            ExternalAccountId = $"ACC_{unique}",
            BankBrandName = "MBBank",
            AccountHolderName = name,
            MaskedAccountNumber = "****1234",
            Status = BankConnectionStatus.Active,
            LinkedAt = DateTime.UtcNow
        };
        db.BankConnections.Add(bankConnection);
        await db.SaveChangesAsync();

        return user;
    }

    private static async Task<Wallet> SeedWalletAsync(SpendWiseDbContext db, Guid ownerUserId, string name, decimal balance)
    {
        var wallet = WalletFactory.Create(ownerUserId, name, WalletType.Main, true);
        wallet.Status = WalletStatus.Active;
        wallet.AvailableBalance = balance;
        wallet.LedgerAccount.Balance = balance;
        db.Wallets.Add(wallet);
        await db.SaveChangesAsync();
        return wallet;
    }

    private static async Task MakeFriendsAsync(SpendWiseDbContext db, Guid first, Guid second, DateTime utcNow)
    {
        var lower = first.CompareTo(second) < 0 ? first : second;
        var higher = lower == first ? second : first;
        var request = new FriendRequest
        {
            RequesterUserId = first,
            AddresseeUserId = second,
            LowerUserId = lower,
            HigherUserId = higher,
            Status = FriendRequestStatus.Accepted
        };
        db.Friendships.Add(new Friendship
        {
            LowerUserId = lower,
            HigherUserId = higher,
            AcceptedRequest = request,
            AcceptedAt = utcNow
        });
        await db.SaveChangesAsync();
    }

    private static async Task ActorAsync(SpendWiseApiFactory factory, HttpClient client, User user)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
        var session = new UserSession { UserId = user.Id, LastSeenAt = DateTime.UtcNow };
        db.UserSessions.Add(session);
        await db.SaveChangesAsync();
        var token = scope.ServiceProvider.GetRequiredService<ITokenService>().CreateAccessToken(user, session.Id).Token;
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    private static async Task<(TargetedCorrectnessFixture F, MutableClock Clock, V1FundService Service, V1FundDto Fund, User Owner, User MemberA, User MemberB)>
        Setup3MemberFixedFundAsync(long targetVnd = 3_000_000)
    {
        var clock = new MutableClock(new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc));
        var f = await TargetedCorrectnessFixture.CreateAsync(clock);

        var owner = await f.AddUserAsync("Owner Stage5");
        owner.EmailVerifiedAt = clock.UtcNow;
        var memberA = await f.AddUserAsync("MemberA Stage5");
        memberA.EmailVerifiedAt = clock.UtcNow;
        var memberB = await f.AddUserAsync("MemberB Stage5");
        memberB.EmailVerifiedAt = clock.UtcNow;
        await f.Db.SaveChangesAsync();

        await V1FundMembershipTests.MakeFriends(f, owner.Id, memberA.Id);
        await V1FundMembershipTests.MakeFriends(f, owner.Id, memberB.Id);

        var service = V1ElectionTests.Configured(f, new QualifiedInactivityEvidence());
        var fund = await service.CreateAsync(owner.Id, new("Trip Fund Fixed Stage5", "Fixed", targetVnd, Pin, 24, true, "v2.0"), default);

        var invA = await service.InviteAsync(owner.Id, fund.Id, memberA.Id, default);
        await service.AcceptAsync(memberA.Id, invA.Id, true, "v2.0", default);

        var invB = await service.InviteAsync(owner.Id, fund.Id, memberB.Id, default);
        await service.AcceptAsync(memberB.Id, invB.Id, true, "v2.0", default);

        var config = await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(targetVnd, Pin), default);

        var wOwner = await f.AddWalletAsync(owner.Id, "WOwner5", true, balance: 10_000_000);
        var wA = await f.AddWalletAsync(memberA.Id, "WA5", true, balance: 10_000_000);
        var wB = await f.AddWalletAsync(memberB.Id, "WB5", true, balance: 10_000_000);

        await service.ContributeAsync(owner.Id, fund.Id, new(wOwner.Id, 1_000_000, "c-owner-s5", Pin, config.Revision), default);
        await service.ContributeAsync(memberA.Id, fund.Id, new(wA.Id, 1_000_000, "c-memA-s5", Pin, config.Revision), default);
        await service.ContributeAsync(memberB.Id, fund.Id, new(wB.Id, 1_000_000, "c-memB-s5", Pin, config.Revision), default);

        return (f, clock, service, fund, owner, memberA, memberB);
    }

    // =========================================================================
    // T28: Idempotency & Replay Safety (LeaveFund / Kick / Worker retry)
    // Exactly one ledger debit, duplicate execution returns cached/replayed result
    // Post-settlement worker retry produces 0 duplicate transactions
    // =========================================================================
    [Fact]
    public async Task T28_Idempotency_DoubleRefund_And_WorkerRetry_ZeroDuplicateLedgerEntries()
    {
        var (f, clock, service, fund, owner, memberA, memberB) = await Setup3MemberFixedFundAsync();
        await using var _ = f;

        // Advance past 15 days so Member A can trigger election or opt out after failed election
        clock.Advance(TimeSpan.FromDays(16));
        var election = await service.OpenElectionAsync(memberA.Id, fund.Id, new V1OpenElectionRequest(0), default);
        // Both vote blank (0) -> election fails immediately
        await service.CastBallotAsync(memberA.Id, election.Id, new V1BallotRequest(1, null, null), default);
        await service.CastBallotAsync(memberB.Id, election.Id, new V1BallotRequest(1, null, null), default);
        await service.AdvanceElectionAsync(memberA.Id, election.Id, default);

        // 1. Member A opts out with explicit idempotency key
        var idempotencyKey = "idemp-leave-stage5-" + Guid.NewGuid().ToString("N");
        var refund1 = await service.LeaveFundAsync(memberA.Id, fund.Id, new V1LeaveFundRequest(Pin, idempotencyKey), default);
        Assert.Equal("Completed", refund1.Status);
        Assert.Equal(1_000_000, refund1.RefundAmountVnd);
        Assert.NotNull(refund1.LedgerTransactionId);

        // 2. Replay: Same idempotency key replayed
        var refund2 = await service.LeaveFundAsync(memberA.Id, fund.Id, new V1LeaveFundRequest(Pin, idempotencyKey), default);
        Assert.Equal("Completed", refund2.Status);
        Assert.Equal("Replayed", refund2.CaseType);
        Assert.Equal(refund1.RefundAmountVnd, refund2.RefundAmountVnd);
        Assert.Equal(refund1.LedgerTransactionId, refund2.LedgerTransactionId);

        // Exactly 1 LedgerTransaction exists with this idempotency key
        var ledgerTxCount = await f.Db.LedgerTransactions.CountAsync(t => t.IdempotencyKey == idempotencyKey);
        Assert.Equal(1, ledgerTxCount);

        // Verify Member A's wallet was credited exactly once (+1,000,000 VND)
        var memAWallet = await f.Db.Wallets.AsNoTracking().SingleAsync(w => w.Name == "WA5");
        Assert.Equal(10_000_000, memAWallet.AvailableBalance);

        // 3. Post-Settlement Worker Retry Hardening:
        // Advance clock past the 24-hour failed election opt-out window
        clock.Advance(TimeSpan.FromHours(25));

        // 4. Worker executes compulsory dissolution and refunds remaining active members (Owner and Member B)
        var workerRuns1 = await service.ProgressFixedFundDeadlinesAsync(default);
        Assert.True(workerRuns1 > 0);

        var fundDbAfterDissolve = await f.Db.V1Funds.AsNoTracking().SingleAsync(x => x.Id == fund.Id);
        Assert.NotNull(fundDbAfterDissolve.DissolvedAt);

        // Treasury ledger account is exactly 0 VND
        var treasuryAcc = await f.Db.LedgerAccounts.SingleAsync(a => a.Code == "V1-FUND:" + fund.Id.ToString("N"));
        Assert.Equal(0, treasuryAcc.Balance);

        // Record total refund LedgerTransactions and wallet balances after dissolution
        var totalRefundTxsAfterDissolve = await f.Db.LedgerTransactions.CountAsync(t => t.BusinessType == "V1FundRefund" && t.BusinessId == fund.Id);
        var ownerWalletBal = (await f.Db.Wallets.AsNoTracking().SingleAsync(w => w.Name == "WOwner5")).AvailableBalance;
        var memBWalletBal = (await f.Db.Wallets.AsNoTracking().SingleAsync(w => w.Name == "WB5")).AvailableBalance;

        // 5. Worker retry idempotency: Run worker repeatedly after dissolution
        var retryRuns1 = await service.ProgressFixedFundDeadlinesAsync(default);
        var retryRuns2 = await service.ProgressFixedFundDeadlinesAsync(default);

        // Assert zero new refund transactions created on subsequent retries
        var totalRefundTxsAfterRetries = await f.Db.LedgerTransactions.CountAsync(t => t.BusinessType == "V1FundRefund" && t.BusinessId == fund.Id);
        Assert.Equal(totalRefundTxsAfterDissolve, totalRefundTxsAfterRetries);

        // Assert wallet balances remain identical (no double crediting)
        var ownerWalletBalRetry = (await f.Db.Wallets.AsNoTracking().SingleAsync(w => w.Name == "WOwner5")).AvailableBalance;
        var memBWalletBalRetry = (await f.Db.Wallets.AsNoTracking().SingleAsync(w => w.Name == "WB5")).AvailableBalance;
        Assert.Equal(ownerWalletBal, ownerWalletBalRetry);
        Assert.Equal(memBWalletBal, memBWalletBalRetry);

        // Treasury remains strictly 0 VND
        var treasuryAccRetry = await f.Db.LedgerAccounts.SingleAsync(a => a.Code == "V1-FUND:" + fund.Id.ToString("N"));
        Assert.Equal(0, treasuryAccRetry.Balance);
    }

    // =========================================================================
    // T29: Concurrency & State Ordering Safety with Deterministic Synchronization Gates
    // Uses isolated SQL Server database, independent connections and synchronization gates
    // to deterministically control and prove BOTH lock acquisition orders:
    // Order 1: Worker acquires lock first -> auto-kicks Member A, late contribution blocked.
    // Order 2: Contribution acquires lock first on time -> completes, worker afterwards does not kick.
    // Strict assertion: Zero hybrid state, deadline bypass strictly prohibited, exact ledger integrity.
    // =========================================================================
    [Fact]
    public async Task T29_ConcurrencyAndOrdering_SimultaneousDeadlineWorkerAndContribution_StrictStateIntegrity()
    {
        var clock = new MutableClock(new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc));
        var database = await OwnAsync(Stage5SqlDatabase.CreateAsync(clock));

        // ---------------------------------------------------------------------
        // DETERMINISTIC ORDER 1: Worker Acquires Lock First -> Auto-Kicks Member A
        // Late contribution arrives concurrently while worker is in transaction -> Blocked!
        // ---------------------------------------------------------------------
        Guid fundId;
        Guid ownerId;
        Guid memberAId;
        Guid wOwnerId;
        Guid wAId;
        long obligationRevision;

        await using (var dbSetup = database.Context())
        {
            var owner = await SeedUserAsync(dbSetup, "Owner T29 SQL", clock.UtcNow);
            var memberA = await SeedUserAsync(dbSetup, "Member A T29 SQL", clock.UtcNow);
            await MakeFriendsAsync(dbSetup, owner.Id, memberA.Id, clock.UtcNow);

            var serviceSetup = CreateService(dbSetup, clock);
            var fund = await serviceSetup.CreateAsync(owner.Id, new("Fund T29 SQL", "Fixed", 2_000_000, Pin, 24, true, "v2.0"), default);

            var invA = await serviceSetup.InviteAsync(owner.Id, fund.Id, memberA.Id, default);
            await serviceSetup.AcceptAsync(memberA.Id, invA.Id, true, "v2.0", default);

            var config = await serviceSetup.ConfirmObligationsAsync(owner.Id, fund.Id, new(2_000_000, Pin), default);

            var wOwner = await SeedWalletAsync(dbSetup, owner.Id, "WOwner29", balance: 5_000_000);
            var wA = await SeedWalletAsync(dbSetup, memberA.Id, "WA29", balance: 5_000_000);

            // Owner contributes 1,000,000 VND
            await serviceSetup.ContributeAsync(owner.Id, fund.Id, new(wOwner.Id, 1_000_000, "c-owner-29-sql", Pin, config.Revision), default);

            // Member A contributes partial 500,000 VND on time
            await serviceSetup.ContributeAsync(memberA.Id, fund.Id, new(wA.Id, 500_000, "c-a-partial-29-sql", Pin, config.Revision), default);

            fundId = fund.Id;
            ownerId = owner.Id;
            memberAId = memberA.Id;
            wOwnerId = wOwner.Id;
            wAId = wA.Id;
            obligationRevision = config.Revision;
        }

        // Advance clock to 25 hours (1 hour past the 24-hour contribution deadline)
        clock.Advance(TimeSpan.FromHours(25));

        // Synchronization Gates for Order 1:
        // workerGate pauses in NonQueryExecutedAsync after the UPDATE on V1Funds completes,
        // proving the exclusive lock on V1Funds is held on SQL Server within its active transaction.
        var workerGate = new LockContentionGate(pauseAfterLock: true);
        // contributeGate signals ExecutingReached in NonQueryExecutingAsync when the UPDATE is about
        // to execute on SQL Server, proving it has arrived at the lock contention point.
        var contributeGate = new LockContentionGate(pauseAfterLock: false);

        await using var dbWorker = database.Context(workerGate);
        await using var dbContribute = database.Context(contributeGate);

        var serviceWorker = CreateService(dbWorker, clock);
        var serviceContribute = CreateService(dbContribute, clock);

        Exception? contributeEx = null;
        Exception? workerEx = null;
        int workerCount = 0;

        // Start worker task
        var workerTask = Task.Run(async () =>
        {
            try
            {
                workerCount = await serviceWorker.ProgressFixedFundDeadlinesAsync(default);
            }
            catch (Exception ex)
            {
                workerEx = ex;
            }
        });

        // Deterministically wait until Worker has executed the UPDATE and acquired the exclusive fund lock on SQL Server
        await workerGate.LockAcquired.Task.WaitAsync(TimeSpan.FromSeconds(10));

        // Start late contribute task while Worker is holding the transaction lock
        var contributeTask = Task.Run(async () =>
        {
            try
            {
                await serviceContribute.ContributeAsync(memberAId, fundId,
                    new(wAId, 500_000, "c-a-late-sql-concurrent", Pin, obligationRevision), default);
            }
            catch (Exception ex)
            {
                contributeEx = ex;
            }
        });

        // Deterministically wait until Contribute has reached the lock contention point on SQL Server
        await contributeGate.ExecutingReached.Task.WaitAsync(TimeSpan.FromSeconds(10));

        // Release the worker to complete its auto-kick transaction and release the lock
        workerGate.Release.TrySetResult();

        await Task.WhenAll(workerTask, contributeTask);

        // Assert worker execution did not throw an unhandled exception
        Assert.Null(workerEx);
        Assert.True(workerCount > 0);

        // Member A's late contribution MUST have failed due to either:
        // - "contribution_deadline_passed": Contribution deadline passed per server clock
        // - "fund_not_found" / "forbidden_member_not_active": Member was already kicked by worker
        Assert.NotNull(contributeEx);
        var appEx = Assert.IsAssignableFrom<AppException>(contributeEx);
        Assert.Contains(appEx.Code, new[] { "contribution_deadline_passed", "fund_not_found", "forbidden_member_not_active" });

        // Verify state in independent verification DbContext
        await using (var dbVerify = database.Context())
        {
            var memADb = await dbVerify.V1FundMemberships.AsNoTracking().SingleAsync(m => m.FundId == fundId && m.UserId == memberAId);

            // Member A must have been auto-kicked by the deadline worker
            Assert.NotNull(memADb.LeftAt);

            // Exactly 500,000 VND deposit was refunded to Member A
            var memAWallet = await dbVerify.Wallets.AsNoTracking().SingleAsync(w => w.Id == wAId);
            Assert.Equal(5_000_000, memAWallet.AvailableBalance);

            // Owner wallet: 4,000,000 VND (5M - 1M contribution)
            var ownerWallet = await dbVerify.Wallets.AsNoTracking().SingleAsync(w => w.Id == wOwnerId);
            Assert.Equal(4_000_000, ownerWallet.AvailableBalance);

            // Fund treasury ledger account has exactly 1,000,000 VND (Owner's share)
            var treasuryAcc = await dbVerify.LedgerAccounts.SingleAsync(a => a.Code == "V1-FUND:" + fundId.ToString("N"));
            Assert.Equal(1_000_000, treasuryAcc.Balance);

            // Verify financial transaction and refund ledger entries: exactly 1 refund posting
            var refundLedgerCount = await dbVerify.LedgerTransactions.CountAsync(lt => lt.BusinessType == "V1FundRefund" && lt.BusinessId == fundId);
            Assert.Equal(1, refundLedgerCount);

            // Verify FinancialTransaction maps to LedgerTransaction
            var finRefund = await dbVerify.FinancialTransactions.SingleAsync(ft => ft.OwnerUserId == memberAId && ft.Type == FinancialTransactionType.Refund);
            Assert.Equal(500_000, finRefund.Amount);
            Assert.NotEqual(Guid.Empty, finRefund.LedgerTransactionId);
            var ledgerRefund = await dbVerify.LedgerTransactions.SingleAsync(lt => lt.Id == finRefund.LedgerTransactionId);
            Assert.Equal("V1FundRefund", ledgerRefund.BusinessType);
            Assert.Equal(fundId, ledgerRefund.BusinessId);

            // STRICT MUTUAL EXCLUSION ASSERTION:
            // Under no circumstances can Member A be kicked AND have full 1,000,000 VND contributions registered
            var totalContributionsA = await dbVerify.V1FundContributions.Where(c => c.FundId == fundId && c.UserId == memberAId).SumAsync(c => c.AmountVnd);
            Assert.Equal(500_000, totalContributionsA);
            Assert.False(memADb.LeftAt != null && totalContributionsA == 1_000_000,
                "INVARIANT VIOLATION: Member was kicked yet registered full 1,000,000 VND contribution!");
        }

        // ---------------------------------------------------------------------
        // DETERMINISTIC ORDER 2: Valid On-Time Contribution Acquires Lock First
        // Worker arrives concurrently, waits for contribution to complete, then inspects -> No kick!
        // ---------------------------------------------------------------------
        clock.UtcNow = new DateTime(2026, 11, 10, 0, 0, 0, DateTimeKind.Utc);
        Guid fund2Id;
        Guid owner2Id;
        Guid memberBId;
        Guid wO2Id;
        Guid wB2Id;
        long config2Revision;

        await using (var db2Setup = database.Context())
        {
            var owner2 = await SeedUserAsync(db2Setup, "Owner2 T29", clock.UtcNow);
            var memberB = await SeedUserAsync(db2Setup, "Member B T29", clock.UtcNow);
            await MakeFriendsAsync(db2Setup, owner2.Id, memberB.Id, clock.UtcNow);

            var s2 = CreateService(db2Setup, clock);
            var fund2 = await s2.CreateAsync(owner2.Id, new("Fund 2 T29", "Fixed", 2_000_000, Pin, 24, true, "v2.0"), default);

            var invB = await s2.InviteAsync(owner2.Id, fund2.Id, memberB.Id, default);
            await s2.AcceptAsync(memberB.Id, invB.Id, true, "v2.0", default);

            var config2 = await s2.ConfirmObligationsAsync(owner2.Id, fund2.Id, new(2_000_000, Pin), default);

            var wO2 = await SeedWalletAsync(db2Setup, owner2.Id, "WO2", 5_000_000);
            var wB2 = await SeedWalletAsync(db2Setup, memberB.Id, "WB2", 5_000_000);

            // Owner contributes 1,000,000 VND on time
            await s2.ContributeAsync(owner2.Id, fund2.Id, new(wO2.Id, 1_000_000, "c-o2-init", Pin, config2.Revision), default);

            fund2Id = fund2.Id;
            owner2Id = owner2.Id;
            memberBId = memberB.Id;
            wO2Id = wO2.Id;
            wB2Id = wB2.Id;
            config2Revision = config2.Revision;
        }

        // Clock is at hour 23 (before 24h deadline: contribution is on time)
        clock.Advance(TimeSpan.FromHours(23));

        // Synchronization Gates for Order 2:
        // contributeCommitGate pauses in TransactionCommittingAsync:
        // this proves Contribution has checked its deadline on-time (hour 23 < 24h),
        // executed LockFundAsync, posted ledger entries, and is holding the active transaction
        // right before commit.
        var contributeCommitGate = new TransactionCommitGate(pauseBeforeCommit: true);
        // workerGate2 signals ExecutingReached in NonQueryExecutingAsync when the UPDATE is about
        // to execute on SQL Server, proving it has arrived at the lock contention point.
        var workerGate2 = new LockContentionGate(pauseAfterLock: false);

        await using var dbContribute2 = database.Context(contributeCommitGate);
        await using var dbWorker2 = database.Context(workerGate2);

        var sContribute2 = CreateService(dbContribute2, clock);
        var sWorker2 = CreateService(dbWorker2, clock);

        Exception? contribute2Ex = null;
        Exception? worker2Ex = null;

        var contribute2Task = Task.Run(async () =>
        {
            try
            {
                await sContribute2.ContributeAsync(memberBId, fund2Id,
                    new(wB2Id, 1_000_000, "c-b2-ontime-gated", Pin, config2Revision), default);
            }
            catch (Exception ex)
            {
                contribute2Ex = ex;
            }
        });

        // Deterministically wait until Contribution has validated on-time deadline,
        // acquired the lock, processed postings, and reached the pre-commit state
        await contributeCommitGate.HoldingLock.Task.WaitAsync(TimeSpan.FromSeconds(10));

        // Advance clock past the deadline (now at hour 25, past the 24h deadline)
        // while Contribution is actively holding the fund transaction lock!
        clock.Advance(TimeSpan.FromHours(2));

        // Start Worker concurrently: it will scan funds, enter transaction, and attempt to lock fund2
        var worker2Task = Task.Run(async () =>
        {
            try
            {
                await sWorker2.ProgressFixedFundDeadlinesAsync(default);
            }
            catch (Exception ex)
            {
                worker2Ex = ex;
            }
        });

        // Deterministically wait until Worker has reached the lock contention point on SQL Server
        await workerGate2.ExecutingReached.Task.WaitAsync(TimeSpan.FromSeconds(10));

        // Release Contribution to commit successfully and release the lock to Worker
        contributeCommitGate.Release.TrySetResult();

        await Task.WhenAll(contribute2Task, worker2Task);

        // Assert neither task threw an error
        Assert.Null(contribute2Ex);
        Assert.Null(worker2Ex);

        // Verify state in independent DbContext
        await using (var dbWorker2Verify = database.Context())
        {
            var sWorker2Verify = CreateService(dbWorker2Verify, clock);

            var memBDb = await dbWorker2Verify.V1FundMemberships.AsNoTracking().SingleAsync(m => m.FundId == fund2Id && m.UserId == memberBId);
            // Member B was NOT kicked because full obligation was paid on time
            Assert.Null(memBDb.LeftAt);

            // Member B wallet: 4,000,000 VND (5M - 1M)
            var memBWallet = await dbWorker2Verify.Wallets.AsNoTracking().SingleAsync(w => w.Id == wB2Id);
            Assert.Equal(4_000_000, memBWallet.AvailableBalance);

            // Owner 2 wallet: 4,000,000 VND (5M - 1M)
            var owner2Wallet = await dbWorker2Verify.Wallets.AsNoTracking().SingleAsync(w => w.Id == wO2Id);
            Assert.Equal(4_000_000, owner2Wallet.AvailableBalance);

            // Fund treasury ledger account has exactly 2,000,000 VND
            var treasuryAcc2 = await dbWorker2Verify.LedgerAccounts.SingleAsync(a => a.Code == "V1-FUND:" + fund2Id.ToString("N"));
            Assert.Equal(2_000_000, treasuryAcc2.Balance);

            var fin2 = await sWorker2Verify.FinanceAsync(memberBId, fund2Id, default);
            Assert.Equal(2_000_000, fin2.BalanceVnd);
            Assert.Equal("OPEN", fin2.SpendingState);

            // Exactly 0 refund ledger entries for fund 2
            var refundsFund2 = await dbWorker2Verify.LedgerTransactions.CountAsync(lt => lt.BusinessType == "V1FundRefund" && lt.BusinessId == fund2Id);
            Assert.Equal(0, refundsFund2);

            // Verify contribution FinancialTransaction maps to LedgerTransaction
            var finContribB = await dbWorker2Verify.FinancialTransactions.SingleAsync(ft => ft.OwnerUserId == memberBId && ft.Type == FinancialTransactionType.GroupFundContribution);
            Assert.Equal(1_000_000, finContribB.Amount);
            Assert.Equal(fund2Id, finContribB.RelatedBusinessId);
            Assert.NotEqual(Guid.Empty, finContribB.LedgerTransactionId);
            var ledgerContribB = await dbWorker2Verify.LedgerTransactions.SingleAsync(lt => lt.Id == finContribB.LedgerTransactionId);
            Assert.Equal("V1FundContribution", ledgerContribB.BusinessType);
            var contribB = await dbWorker2Verify.V1FundContributions.SingleAsync(c => c.FundId == fund2Id && c.UserId == memberBId);
            Assert.Equal(contribB.Id, ledgerContribB.BusinessId);
        }
    }

    // =========================================================================
    // T30: Spend Dispute Flow
    // Reporting completed expense opens SupportCase with Category V1FundDispute
    // Transaction status remains Completed, ledger is immutable, balance unaffected
    // FinancialTransaction.LedgerTransactionId & LedgerTransaction verified
    // Duplicate report is idempotent
    // =========================================================================
    [Fact]
    public async Task T30_CompletedExpense_DisputeReport_OpensSupportCase_DoesNotReverseLedger()
    {
        var (f, clock, service, fund, owner, memberA, memberB) = await Setup3MemberFixedFundAsync();
        await using var _ = f;

        var merchant = await f.AddUserAsync("Merchant Stage5");
        var wMerchant = await f.AddWalletAsync(merchant.Id, "WMerchant", true, balance: 0);

        // Fund is 100% funded (3,000,000 / 3,000,000 VND), finance is OPEN
        // Owner grants spending permission to Member A
        await service.SetSpendingPermissionAsync(owner.Id, fund.Id, memberA.Id, true, Pin, default);

        // Member A makes a valid common expense of 1,200,000 VND
        var expInput = new V1FundRequestInput("Expense", 1_200_000, merchant.Id, "Team dinner banquet", "exp-stage5-1", Pin);
        var expenseReq = await service.CreateFundRequestAsync(memberA.Id, fund.Id, expInput, default);
        Assert.Equal("Completed", expenseReq.State);

        // Verify the FinancialTransaction and linked LedgerTransaction before dispute
        var finTxBefore = await f.Db.FinancialTransactions.AsNoTracking().SingleAsync(t => t.RelatedBusinessId == expenseReq.Id);
        Assert.Equal(FinancialTransactionStatus.Completed, finTxBefore.Status);
        Assert.NotEqual(Guid.Empty, finTxBefore.LedgerTransactionId);

        var ledgerTxBefore = await f.Db.LedgerTransactions.AsNoTracking().SingleAsync(lt => lt.Id == finTxBefore.LedgerTransactionId);
        Assert.Equal(LedgerTransactionStatus.Completed, ledgerTxBefore.Status);

        // Treasury balance is now 3,000,000 - 1,200,000 = 1,800,000 VND
        var financeBeforeReport = await service.FinanceAsync(owner.Id, fund.Id, default);
        Assert.Equal(1_800_000, financeBeforeReport.BalanceVnd);

        // Member B suspects mismanagement and reports the completed expense
        var reportReason = "Chi tieu nha hang khong co hoa don do va vuot muc cho phep";
        var reportedReq = await service.ReportFundRequestAsync(memberB.Id, expenseReq.Id, reportReason, default);

        // 1. Transaction and request state remains Completed (immutable ledger rule)
        Assert.Equal("Completed", reportedReq.State);
        var reqDb = await f.Db.V1FundRequests.AsNoTracking().SingleAsync(r => r.Id == expenseReq.Id);
        Assert.Equal("Completed", reqDb.State);

        // 2. FinancialTransaction and LedgerTransaction remain unchanged with Status = Completed
        var finTxAfter = await f.Db.FinancialTransactions.AsNoTracking().SingleAsync(t => t.RelatedBusinessId == expenseReq.Id);
        Assert.Equal(FinancialTransactionStatus.Completed, finTxAfter.Status);
        Assert.Equal(finTxBefore.LedgerTransactionId, finTxAfter.LedgerTransactionId);

        var ledgerTxAfter = await f.Db.LedgerTransactions.AsNoTracking().SingleAsync(lt => lt.Id == finTxAfter.LedgerTransactionId);
        Assert.Equal(LedgerTransactionStatus.Completed, ledgerTxAfter.Status);

        // 3. Fund treasury balance is NOT altered or deducted
        var financeAfterReport = await service.FinanceAsync(owner.Id, fund.Id, default);
        Assert.Equal(1_800_000, financeAfterReport.BalanceVnd);

        // 4. Merchant wallet balance is NOT reversed
        var merchWallet = await f.Db.Wallets.AsNoTracking().SingleAsync(w => w.Id == wMerchant.Id);
        Assert.Equal(1_200_000, merchWallet.AvailableBalance);

        // 5. Exactly one SupportCase of category V1FundDispute was created
        var supportCase = await f.Db.SupportCases.AsNoTracking().SingleAsync(c =>
            c.Category == "V1FundDispute" && c.LinkedTransactionId == expenseReq.Id && c.LinkedUserId == memberB.Id);
        Assert.Equal("New", supportCase.Status.ToString());
        Assert.Contains(reportReason, supportCase.Description);

        // 6. Duplicate report by same member is idempotent and does not create duplicate support cases
        var reportedAgain = await service.ReportFundRequestAsync(memberB.Id, expenseReq.Id, reportReason, default);
        Assert.Equal("Completed", reportedAgain.State);
        var caseCount = await f.Db.SupportCases.CountAsync(c =>
            c.Category == "V1FundDispute" && c.LinkedTransactionId == expenseReq.Id && c.LinkedUserId == memberB.Id);
        Assert.Equal(1, caseCount);

        // 7. Ledger records still intact after duplicate report
        var finTxDuplicate = await f.Db.FinancialTransactions.AsNoTracking().SingleAsync(t => t.RelatedBusinessId == expenseReq.Id);
        Assert.Equal(finTxBefore.LedgerTransactionId, finTxDuplicate.LedgerTransactionId);
    }

    // =========================================================================
    // T31: True Comprehensive End-to-End Multi-Period Lifecycle Verification
    // Cycle 1: Creation -> Obligation Snapshot -> Contributions -> Spending (CanSpend) ->
    // Dispute Reporting -> Cycle 1 Close -> Period 2 Proposal -> Obligation Consent ->
    // Opt-out Refund for non-consenting member -> Proposal Settlement ->
    // Owner Inactivity (16 days) -> Election -> Candidate PIN Confirmation ->
    // New Owner nhậm chức -> Period 2 Closure & Dissolution ->
    // Exact whole-dong refunds according to entitlement -> Final Treasury Balance = 0 VND.
    // =========================================================================
    [Fact]
    public async Task T31_MultiPeriod_Dispute_CarryOver_Election_FullLifecycle_EndToEnd()
    {
        var clock = new MutableClock(new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc));
        await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);

        var founder = await f.AddUserAsync("Founder Full E2E");
        founder.EmailVerifiedAt = clock.UtcNow;
        var memberA = await f.AddUserAsync("MemberA Full E2E");
        memberA.EmailVerifiedAt = clock.UtcNow;
        var memberB = await f.AddUserAsync("MemberB Full E2E");
        memberB.EmailVerifiedAt = clock.UtcNow;
        var merchant = await f.AddUserAsync("Merchant Full E2E");
        await f.Db.SaveChangesAsync();

        await V1FundMembershipTests.MakeFriends(f, founder.Id, memberA.Id);
        await V1FundMembershipTests.MakeFriends(f, founder.Id, memberB.Id);

        var service = V1ElectionTests.Configured(f, new QualifiedInactivityEvidence());

        // ---------------------------------------------------------------------
        // PERIOD 1: Create fund (Target 6,000,000 VND, 3 members, 24h duration)
        // ---------------------------------------------------------------------
        var fund = await service.CreateAsync(founder.Id, new("MultiCycle Fixed Fund", "Fixed", 6_000_000, Pin, 24, true, "v2.0"), default);

        var invA = await service.InviteAsync(founder.Id, fund.Id, memberA.Id, default);
        await service.AcceptAsync(memberA.Id, invA.Id, true, "v2.0", default);

        var invB = await service.InviteAsync(founder.Id, fund.Id, memberB.Id, default);
        await service.AcceptAsync(memberB.Id, invB.Id, true, "v2.0", default);

        var config = await service.ConfirmObligationsAsync(founder.Id, fund.Id, new(6_000_000, Pin), default);
        Assert.Equal(6_000_000, config.TargetVnd);

        var wFounder = await f.AddWalletAsync(founder.Id, "WFounder", true, balance: 10_000_000);
        var wA = await f.AddWalletAsync(memberA.Id, "WA", true, balance: 10_000_000);
        var wB = await f.AddWalletAsync(memberB.Id, "WB", true, balance: 10_000_000);
        var wMerch = await f.AddWalletAsync(merchant.Id, "WMerch", true, balance: 0);

        // All 3 contribute 2,000,000 VND
        await service.ContributeAsync(founder.Id, fund.Id, new(wFounder.Id, 2_000_000, "c-f1", Pin, config.Revision), default);
        await service.ContributeAsync(memberA.Id, fund.Id, new(wA.Id, 2_000_000, "c-a1", Pin, config.Revision), default);
        await service.ContributeAsync(memberB.Id, fund.Id, new(wB.Id, 2_000_000, "c-b1", Pin, config.Revision), default);

        var fin1 = await service.FinanceAsync(founder.Id, fund.Id, default);
        Assert.Equal(6_000_000, fin1.BalanceVnd);
        Assert.Equal("OPEN", fin1.SpendingState);

        // Spending with CanSpend: Founder grants Member A spending permission
        await service.SetSpendingPermissionAsync(founder.Id, fund.Id, memberA.Id, true, Pin, default);
        var exp1 = await service.CreateFundRequestAsync(memberA.Id, fund.Id,
            new V1FundRequestInput("Expense", 1_500_000, merchant.Id, "Team supplies Period 1", "exp-e2e-p1", Pin), default);
        Assert.Equal("Completed", exp1.State);

        var finAfterSpend = await service.FinanceAsync(founder.Id, fund.Id, default);
        Assert.Equal(4_500_000, finAfterSpend.BalanceVnd); // 6M - 1.5M = 4.5M

        // Dispute reporting: Member B reports completed expense
        var disputeReq = await service.ReportFundRequestAsync(memberB.Id, exp1.Id, "Kiem toan chung tu chi tieu", default);
        Assert.Equal("Completed", disputeReq.State);
        var supportCase = await f.Db.SupportCases.AsNoTracking().SingleAsync(c => c.Category == "V1FundDispute" && c.LinkedTransactionId == exp1.Id);
        Assert.Equal("New", supportCase.Status.ToString());

        // ---------------------------------------------------------------------
        // PERIOD TRANSITION: Close Period 1, Propose Period 2 (Target 4,000,000 VND)
        // ---------------------------------------------------------------------
        await service.CloseActivityAsync(founder.Id, fund.Id, Pin, default);
        var proposal = await service.ProposeActivityAsync(founder.Id, fund.Id, new(4_000_000, "p2-proposal", Pin), default);

        // Snapshot entitlements: Each member has (2M contributed - 500k shared expense) = 1,500,000 VND carry-over
        var voters = await f.Db.V1ActivityVoters.Where(v => v.ProposalId == proposal.Id).ToListAsync();
        Assert.Equal(3, voters.Count);
        Assert.All(voters, v => Assert.Equal(1_500_000, v.CarryOverVnd));

        // Founder and Member A vote Yes, Member B votes No (opts out)
        // Member B immediately receives 1,500,000 VND refund and departs!
        await service.VoteActivityAsync(founder.Id, proposal.Id, true, default);
        await service.VoteActivityAsync(memberA.Id, proposal.Id, true, default);
        await service.VoteActivityAsync(memberB.Id, proposal.Id, false, default);

        var memBWallet = await f.Db.Wallets.AsNoTracking().SingleAsync(w => w.Id == wB.Id);
        // Initial was 10M, contributed 2M (balance 8M), refunded 1.5M -> 9.5M
        Assert.Equal(9_500_000, memBWallet.AvailableBalance);

        var memBDb = await f.Db.V1FundMemberships.AsNoTracking().SingleAsync(m => m.FundId == fund.Id && m.UserId == memberB.Id);
        Assert.NotNull(memBDb.LeftAt);

        // Remaining active members (Founder and Member A): Target is 4M for 2 seats = 2M each.
        // Carry-over is 1.5M each -> new obligation is 2M each. Both confirm obligation consent!
        await service.ConfirmActivityObligationAsync(founder.Id, proposal.Id, new V1ConfirmActivityObligationRequest(2_000_000, 24, Pin), default);
        await service.ConfirmActivityObligationAsync(memberA.Id, proposal.Id, new V1ConfirmActivityObligationRequest(2_000_000, 24, Pin), default);

        // Settle proposal -> Fund enters Period 2!
        clock.Advance(TimeSpan.FromHours(1));
        await service.SettleActivityAsync(founder.Id, proposal.Id, Pin, default);

        var finP2 = await service.FinanceAsync(founder.Id, fund.Id, default);
        Assert.Equal(4_000_000, finP2.TargetVnd);
        Assert.Equal(3_000_000, finP2.BalanceVnd); // 4.5M before - 1.5M refunded to B = 3.0M remaining
        var obFounder = finP2.Obligations.Single(o => o.UserId == founder.Id);
        var obA = finP2.Obligations.Single(o => o.UserId == memberA.Id);
        Assert.Equal(500_000, obFounder.RemainingVnd); // 2M required - 1.5M carry-over = 500k
        Assert.Equal(500_000, obA.RemainingVnd);

        // Founder and Member A contribute remaining 500k each -> Period 2 is 100% funded!
        await service.ContributeAsync(founder.Id, fund.Id, new(wFounder.Id, 500_000, "c-f2", Pin, finP2.Revision), default);
        await service.ContributeAsync(memberA.Id, fund.Id, new(wA.Id, 500_000, "c-a2", Pin, finP2.Revision), default);

        var finP2Funded = await service.FinanceAsync(founder.Id, fund.Id, default);
        Assert.Equal(4_000_000, finP2Funded.BalanceVnd);
        Assert.Equal("OPEN", finP2Funded.SpendingState);

        // ---------------------------------------------------------------------
        // INACTIVITY & ELECTION: Founder goes inactive for 16 days
        // Member A opens election, votes, advances, and confirms PIN -> New Owner!
        // ---------------------------------------------------------------------
        clock.Advance(TimeSpan.FromDays(16));
        var election = await service.OpenElectionAsync(memberA.Id, fund.Id, new V1OpenElectionRequest(0), default);

        // Member A votes for self
        await service.RegisterCandidateAsync(memberA.Id, election.Id, Pin, default);
        await service.CastBallotAsync(memberA.Id, election.Id, new V1BallotRequest(1, memberA.Id, null), default);

        // Advance to round 2 (Member A won)
        await service.AdvanceElectionAsync(memberA.Id, election.Id, default);

        // Member A confirms acceptance with PIN via AcceptElectedOwnershipAsync
        var acceptedElection = await service.AcceptElectedOwnershipAsync(memberA.Id, election.Id, Pin, default);
        Assert.Equal("Completed", acceptedElection.Phase);
        Assert.Equal(memberA.Id, acceptedElection.WinnerUserId);

        // Verify fund owner is now Member A and revision increased
        var fundAfterElection = await f.Db.V1Funds.AsNoTracking().SingleAsync(x => x.Id == fund.Id);
        Assert.Equal(memberA.Id, fundAfterElection.OwnerUserId);
        Assert.True(fundAfterElection.OwnershipRevision >= 1);

        // ---------------------------------------------------------------------
        // PERIOD 2 CLOSURE & FINAL DISSOLUTION
        // New Owner (Member A) closes activity and dissolves fund
        // ---------------------------------------------------------------------
        await service.CloseActivityAsync(memberA.Id, fund.Id, Pin, default);
        var dissolve = await service.DissolveFundAsync(memberA.Id, fund.Id, new V1DissolveFundRequest(Pin, "final-dissolve-key"), default);

        Assert.Equal(4_000_000, dissolve.TotalRefundedVnd);
        Assert.Equal(0, dissolve.FinalBalanceVnd);

        // Active members in Period 2 (Founder and Member A) each contributed 2M net for Period 2 -> refunded 2M each!
        Assert.Equal(2, dissolve.Refunds.Count);
        foreach (var refItem in dissolve.Refunds)
        {
            Assert.Equal(2_000_000, refItem.RefundAmountVnd);
        }

        // Final treasury ledger balance is exactly 0 VND!
        var treasuryAcc = await f.Db.LedgerAccounts.Where(a => a.Code == "V1-FUND:" + fund.Id.ToString("N")).SingleAsync();
        Assert.Equal(0, treasuryAcc.Balance);

        // Fund is officially closed
        var finalFundDb = await f.Db.V1Funds.AsNoTracking().SingleAsync(x => x.Id == fund.Id);
        Assert.NotNull(finalFundDb.DissolvedAt);
    }

    // =========================================================================
    // T32: HTTP API Contract Conformance
    // Validates exact JSON request payloads documented in User.md bind correctly
    // to V1FundsController without parameter or serialization mismatch.
    // Explicitly verifies CandidateUserId, Round, Support, and VoterUserId in DB.
    // =========================================================================
    [Fact]
    public async Task T32_HttpElection_Contract_RequestPayloadShapeMatchesController()
    {
        var clock = new MutableClock(DateTime.UtcNow);
        using var factory = new SpendWiseApiFactory(new Dictionary<string, string?>
        {
            ["V1:Activity:CoverageVerified"] = "true",
            ["V1:Activity:CoverageStartsAtUtc"] = clock.UtcNow.AddDays(-1).ToString("O"),
            ["RateLimiting:FinancialPermitLimit"] = "100"
        }, clock: clock);

        using var client = factory.CreateHttpsClient();
        CoreFundSeed seed;

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            seed = await CoreAcceptanceData.SeedAsync(db, true);
        }

        await ActorAsync(factory, client, seed.Member);

        // Fast-forward 16 days to satisfy inactivity proof
        clock.UtcNow = clock.UtcNow.AddDays(16);

        // 1. Contract Test: POST /api/v1/funds/{id}/elections with {"expectedOwnershipRevision": 0}
        using var openResp = await client.PostAsJsonAsync(
            $"/api/v1/funds/{seed.FundId}/elections",
            new { expectedOwnershipRevision = 0L });
        Assert.True(openResp.StatusCode == HttpStatusCode.OK, await openResp.Content.ReadAsStringAsync());
        var election = (await openResp.Content.ReadFromJsonAsync<V1ElectionDto>())!;
        Assert.NotNull(election);
        Assert.Equal("Nomination", election.Phase);

        // 2. Contract Test: POST /api/v1/funds/elections/{id}/ballots with {"round": 1, "candidateUserId": "...", "support": null}
        using var ballotResp = await client.PostAsJsonAsync(
            $"/api/v1/funds/elections/{election.Id}/ballots",
            new { round = 1, candidateUserId = seed.Member.Id, support = (bool?)null });
        Assert.True(ballotResp.StatusCode == HttpStatusCode.OK, await ballotResp.Content.ReadAsStringAsync());

        // P2: Query DB directly with independent DbContext to verify candidateUserId was deserialized and stored accurately
        using (var verifyScope = factory.Services.CreateScope())
        {
            var dbVerify = verifyScope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            var ballotDb = await dbVerify.V1ElectionBallots.SingleAsync(b => b.ElectionId == election.Id && b.VoterUserId == seed.Member.Id);
            Assert.Equal(seed.Member.Id, ballotDb.CandidateUserId);
            Assert.Equal(1, ballotDb.Round);
            Assert.Null(ballotDb.Support);
            Assert.Equal(seed.Member.Id, ballotDb.VoterUserId);
        }

        // Advance to round 2
        clock.UtcNow = clock.UtcNow.AddHours(24);
        using var adv = await client.PostAsync($"/api/v1/funds/elections/{election.Id}/advance", null);
        Assert.Equal(HttpStatusCode.OK, adv.StatusCode);

        // 3. Contract Test: POST /api/v1/funds/elections/{id}/accept with {"securityPin": "123456"}
        using var acceptResp = await client.PostAsJsonAsync(
            $"/api/v1/funds/elections/{election.Id}/accept",
            new { securityPin = Pin });
        Assert.True(acceptResp.StatusCode == HttpStatusCode.OK, await acceptResp.Content.ReadAsStringAsync());
        var acceptedDto = (await acceptResp.Content.ReadFromJsonAsync<V1ElectionDto>())!;
        Assert.Equal("Completed", acceptedDto.Phase);
        Assert.Equal(seed.Member.Id, acceptedDto.WinnerUserId);
    }
}
