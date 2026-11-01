using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features;
using SpendWise.Application.Features.Authentication;
using SpendWise.Application.Features.V1Groups;
using SpendWise.Application.Features.Wallets;
using SpendWise.Application.Identity;
using SpendWise.Domain.Banking;
using SpendWise.Domain.Finance;
using SpendWise.Domain.Identity;
using SpendWise.Domain.Notifications;
using SpendWise.Domain.Social;
using SpendWise.Domain.V1Groups;
using SpendWise.Infrastructure.Identity;
using SpendWise.Infrastructure.Persistence;
using Xunit;

namespace SpendWise.Tests;

public class FreeFundFinalAcceptanceTests
{
    private sealed class MutableClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; set; } = utcNow;
    }

    [Fact]
    public async Task FreeFund_FullLifecycle_EndToEnd_FromCreationToDissolution_WithFullAccountingReconciliation()
    {
        var clock = new MutableClock(new DateTime(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc));
        await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);

        // 1. Setup 3 actors with 1,000,000 VND each in personal wallets
        // Total initial system capital = 3,000,000 VND
        var initialUserMoney = 1_000_000L;
        var totalSystemCapital = initialUserMoney * 3;

        var ownerA = await f.AddUserAsync("OwnerA");
        var memberB = await f.AddUserAsync("MemberB");
        var memberC = await f.AddUserAsync("MemberC");

        await V1FundMembershipTests.MakeFriends(f, ownerA.Id, memberB.Id);
        await V1FundMembershipTests.MakeFriends(f, ownerA.Id, memberC.Id);

        var walletA = await f.AddWalletAsync(ownerA.Id, "WalletA", true, balance: (decimal)initialUserMoney);
        var walletB = await f.AddWalletAsync(memberB.Id, "WalletB", true, balance: (decimal)initialUserMoney);
        var walletC = await f.AddWalletAsync(memberC.Id, "WalletC", true, balance: (decimal)initialUserMoney);

        var s = V1FundMembershipTests.Service(f);

        // 2. Owner A creates Free Fund
        var fund = await s.CreateAsync(ownerA.Id, new("E2E_FreeFund", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);

        // 3. Invitations & Joins
        var invB = await s.InviteAsync(ownerA.Id, fund.Id, memberB.Id, default);
        await s.AcceptAsync(memberB.Id, invB.Id, default);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);

        var invC = await s.InviteAsync(ownerA.Id, fund.Id, memberC.Id, default);
        await s.AcceptAsync(memberC.Id, invC.Id, default);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);

        // 4. Contributions: A contributes 500k, B contributes 300k, C contributes 200k (Total fund: 1,000,000 VND)
        await s.ContributeAsync(ownerA.Id, fund.Id, new(walletA.Id, 500_000, "c-a", TargetedCorrectnessFixture.SecurityPin), default);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);
        await s.ContributeAsync(memberB.Id, fund.Id, new(walletB.Id, 300_000, "c-b", TargetedCorrectnessFixture.SecurityPin), default);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);
        await s.ContributeAsync(memberC.Id, fund.Id, new(walletC.Id, 200_000, "c-c", TargetedCorrectnessFixture.SecurityPin), default);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);

        var equityMid1 = await s.EquityAsync(ownerA.Id, fund.Id, default);
        Assert.Equal(1_000_000, equityMid1.TotalBalanceVnd);
        Assert.Equal(0, equityMid1.ActiveReservedVnd);
        Assert.Equal(1_000_000, equityMid1.AvailableBalanceVnd);

        // 5. Completed Expense: Owner A executes a direct completed group expense of 100,000 VND
        // This is shared proportionally: A bears 50k, B bears 30k, C bears 20k
        var completedExp = await s.CreateFundRequestAsync(ownerA.Id, fund.Id,
            new("Expense", 100_000, ownerA.Id, "Shared team equipment", "req-completed-exp", TargetedCorrectnessFixture.SecurityPin), default);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);
        Assert.Equal("Completed", completedExp.State);

        var equityAfterExp = await s.EquityAsync(ownerA.Id, fund.Id, default);
        Assert.Equal(900_000, equityAfterExp.TotalBalanceVnd);
        Assert.Equal(0, equityAfterExp.ActiveReservedVnd);
        Assert.Equal(900_000, equityAfterExp.AvailableBalanceVnd);

        // 6. Member C requests a Withdrawal of 50,000 VND -> Owner approves -> Real cash withdrawal
        var wthReq = await s.CreateFundRequestAsync(memberC.Id, fund.Id,
            new("Withdrawal", 50_000, memberC.Id, "Personal cash needed", "req-wth-e2e", TargetedCorrectnessFixture.SecurityPin), default);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);
        await s.ReviewFundRequestAsync(ownerA.Id, wthReq.Id, true, "Approved withdrawal", TargetedCorrectnessFixture.SecurityPin, default);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);

        // Verify Wallet C received the 50k withdrawal
        var wCBalanceAfterWithdrawal = (await f.Db.Wallets.AsNoTracking().SingleAsync(w => w.Id == walletC.Id)).AvailableBalance;
        Assert.Equal(initialUserMoney - 200_000 + 50_000, (long)wCBalanceAfterWithdrawal); // 850,000

        // 7. Member B requests an Expense of 50,000 VND -> Owner approves -> Reserved (ApprovedUnused)
        var expReq = await s.CreateFundRequestAsync(memberB.Id, fund.Id,
            new("Expense", 50_000, memberB.Id, "Group dinner", "req-exp-e2e", TargetedCorrectnessFixture.SecurityPin), default);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);
        await s.ReviewFundRequestAsync(ownerA.Id, expReq.Id, true, "Approved dinner", TargetedCorrectnessFixture.SecurityPin, default);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);

        var equityMid2 = await s.EquityAsync(ownerA.Id, fund.Id, default);
        Assert.Equal(850_000, equityMid2.TotalBalanceVnd);
        Assert.Equal(50_000, equityMid2.ActiveReservedVnd);
        Assert.Equal(800_000, equityMid2.AvailableBalanceVnd);

        // 8. Member B leaves the fund:
        // B's approved unused expense (50k) is automatically canceled
        // B had 300k contributed - 30k shared expense = 270k net equity refunded
        var leaveB = await s.LeaveFundAsync(memberB.Id, fund.Id, new(TargetedCorrectnessFixture.SecurityPin, "leave-b-e2e"), default);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);
        Assert.Equal(270_000, leaveB.RefundAmountVnd);

        // Verify B's approved request is now Cancelled
        var expReqAfterLeave = await s.GetFundRequestAsync(ownerA.Id, expReq.Id, default);
        Assert.Equal("Cancelled", expReqAfterLeave.State);

        // 9. Owner A kicks Member C:
        // C had 200k contributed - 20k shared expense - 50k withdrawn = 130k net equity refunded
        var kickC = await s.KickMemberAsync(ownerA.Id, fund.Id, memberC.Id,
            new(TargetedCorrectnessFixture.SecurityPin, "Kicking C in lifecycle test", "kick-c-e2e"), default);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);
        Assert.Equal(130_000, kickC.RefundAmountVnd);

        // 10. Owner A dissolves the fund:
        // Fund balance remaining = 850k - 270k (B refund) - 130k (C refund) = 450,000 VND
        // (Which equals A's net equity: 500k contributed - 50k shared expense = 450k)
        var dissolveResult = await s.DissolveFundAsync(ownerA.Id, fund.Id,
            new(TargetedCorrectnessFixture.SecurityPin, "dissolve-e2e"), default);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);
        Assert.Equal(450_000, dissolveResult.TotalRefundedVnd);
        Assert.Equal(0, (long)dissolveResult.FinalBalanceVnd);

        // 11. POST-DISSOLUTION COMPLETE FINANCIAL CONSERVATION INVARIANT:
        var finalWA = (long)(await f.Db.Wallets.AsNoTracking().SingleAsync(w => w.Id == walletA.Id)).AvailableBalance;
        var finalWB = (long)(await f.Db.Wallets.AsNoTracking().SingleAsync(w => w.Id == walletB.Id)).AvailableBalance;
        var finalWC = (long)(await f.Db.Wallets.AsNoTracking().SingleAsync(w => w.Id == walletC.Id)).AvailableBalance;

        // Final wallet balances:
        // A: 1,000,000 - 500,000 (contribution) + 100,000 (expense payout) + 450,000 (dissolution refund) = 1,050,000
        //    (Owner A bore 50k shared expense, but received 100k reimbursement to their wallet)
        // B: 1,000,000 - 300,000 (contribution) + 270,000 (leave refund) = 970,000 (bore 30k shared expense)
        // C: 1,000,000 - 200,000 (contribution) + 50,000 (withdrawal) + 130,000 (kick refund) = 980,000 (bore 20k shared expense)
        Assert.Equal(1_050_000, finalWA);
        Assert.Equal(970_000, finalWB);
        Assert.Equal(980_000, finalWC);

        var totalFinalWallets = finalWA + finalWB + finalWC; // 3,000,000 VND
        var finalFundBalance = (long)dissolveResult.FinalBalanceVnd; // 0 VND

        Assert.Equal(0, finalFundBalance);
        // Conservation Invariant: Sum of Wallets + Fund Balance == Total Initial Capital (3,000,000 VND)
        Assert.Equal(totalSystemCapital, totalFinalWallets + finalFundBalance);
    }

    [Fact]
    public async Task FreeFund_StressAndConcurrency_ParallelIdempotentOperations_OnSqlServer_WithIndependentConnections()
    {
        var clock = new MutableClock(new DateTime(2026, 11, 1, 9, 0, 0, DateTimeKind.Utc));
        var catalogName = $"SpendWise_GroupFundS052_Test_{Guid.NewGuid():N}";
        var instance = Environment.GetEnvironmentVariable(SqlTestFixtureLifecycle.InstanceEnvironmentVariable) ?? @"DESKTOP-B5KA3LT\SQLEXPRESS02";
        var connectionString = SqlTestFixtureLifecycle.CreateOwnedConnectionString(catalogName, instance);
        await SqlTestFixtureLifecycle.CreateOwnedCatalogAsync(connectionString);
        var ownsCatalog = true;

        try
        {
            var options = new DbContextOptionsBuilder<SpendWiseDbContext>().UseSqlServer(connectionString).Options;
            Guid ownerId, memberId, walletAId, walletBId, fundId;
            var pinService = new SecurityPinService();

            await using (var seedDb = new SpendWiseDbContext(options, clock))
            {
                await seedDb.Database.EnsureCreatedAsync();
                await SqlTestFixtureLifecycle.ConfigureOwnedCatalogAsync(connectionString);

                // Seed User A (Owner), User B (Member), Friendships, Wallets, BankConnections
                var uniqueA = Guid.NewGuid().ToString("N");
                var uniqueB = Guid.NewGuid().ToString("N");

                var owner = new User
                {
                    FullName = "OwnerA_SQL",
                    Email = $"{uniqueA}@test.local",
                    NormalizedEmail = $"{uniqueA.ToUpperInvariant()}@TEST.LOCAL",
                    SpendId = uniqueA[..12],
                    NormalizedSpendId = uniqueA[..12].ToUpperInvariant(),
                    PasswordHash = "hash",
                    SecurityPinHash = pinService.Hash(TargetedCorrectnessFixture.SecurityPin),
                    Status = UserStatus.Active,
                    SystemRole = SystemRole.User
                };
                var member = new User
                {
                    FullName = "MemberB_SQL",
                    Email = $"{uniqueB}@test.local",
                    NormalizedEmail = $"{uniqueB.ToUpperInvariant()}@TEST.LOCAL",
                    SpendId = uniqueB[..12],
                    NormalizedSpendId = uniqueB[..12].ToUpperInvariant(),
                    PasswordHash = "hash",
                    SecurityPinHash = pinService.Hash(TargetedCorrectnessFixture.SecurityPin),
                    Status = UserStatus.Active,
                    SystemRole = SystemRole.User
                };
                seedDb.Users.AddRange(owner, member);

                var bankA = new BankConnection
                {
                    UserId = owner.Id,
                    Provider = BankProvider.SePayBankHub,
                    AccountEnvironment = SePayAccountEnvironment.TestMode,
                    ExternalAccountId = $"ACC_{uniqueA}",
                    BankBrandName = "MBBank",
                    AccountHolderName = "OwnerA_SQL",
                    MaskedAccountNumber = "****1234",
                    Status = BankConnectionStatus.Active,
                    LinkedAt = clock.UtcNow
                };
                var bankB = new BankConnection
                {
                    UserId = member.Id,
                    Provider = BankProvider.SePayBankHub,
                    AccountEnvironment = SePayAccountEnvironment.TestMode,
                    ExternalAccountId = $"ACC_{uniqueB}",
                    BankBrandName = "MBBank",
                    AccountHolderName = "MemberB_SQL",
                    MaskedAccountNumber = "****5678",
                    Status = BankConnectionStatus.Active,
                    LinkedAt = clock.UtcNow
                };
                seedDb.BankConnections.AddRange(bankA, bankB);

                var walletA = WalletFactory.Create(owner.Id, "WalletA", WalletType.Main, true);
                walletA.AvailableBalance = 2_000_000;
                walletA.LedgerAccount.Balance = 2_000_000;
                var walletB = WalletFactory.Create(member.Id, "WalletB", WalletType.Main, true);
                walletB.AvailableBalance = 2_000_000;
                walletB.LedgerAccount.Balance = 2_000_000;
                seedDb.Wallets.AddRange(walletA, walletB);

                var lower = owner.Id.CompareTo(member.Id) < 0 ? owner.Id : member.Id;
                var higher = lower == owner.Id ? member.Id : owner.Id;
                var req = new FriendRequest
                {
                    RequesterUserId = owner.Id,
                    AddresseeUserId = member.Id,
                    LowerUserId = lower,
                    HigherUserId = higher,
                    Status = FriendRequestStatus.Accepted
                };
                seedDb.Friendships.Add(new Friendship
                {
                    LowerUserId = lower,
                    HigherUserId = higher,
                    AcceptedRequest = req,
                    AcceptedAt = clock.UtcNow
                });

                await seedDb.SaveChangesAsync();

                ownerId = owner.Id;
                memberId = member.Id;
                walletAId = walletA.Id;
                walletBId = walletB.Id;

                // Create Free Fund and accept invitation
                var seedService = CreateSqlService(seedDb, clock);
                var fund = await seedService.CreateAsync(owner.Id,
                    new("ConcurrencySqlFund", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);
                fundId = fund.Id;

                var invB = await seedService.InviteAsync(owner.Id, fund.Id, member.Id, default);
                await seedService.AcceptAsync(member.Id, invB.Id, default);

                // Initial contribution by Owner A (500k)
                await seedService.ContributeAsync(owner.Id, fund.Id,
                    new(walletA.Id, 500_000, "init-a-sql", TargetedCorrectnessFixture.SecurityPin), default);
            }

            // NOW: 5 parallel workers with 5 INDEPENDENT SQL Server connections!
            var key = "parallel-key-sql-dedup";
            var tasks = Enumerable.Range(0, 5).Select(_ => Task.Run(async () =>
            {
                await using var taskDb = new SpendWiseDbContext(options, clock);
                var taskService = CreateSqlService(taskDb, clock);
                try
                {
                    var res = await taskService.ContributeAsync(memberId, fundId,
                        new(walletBId, 100_000, key, TargetedCorrectnessFixture.SecurityPin), default);
                    return (Success: true, Receipt: res, Error: (string?)null);
                }
                catch (Exception ex)
                {
                    return (Success: false, Receipt: (V1ContributionDto?)null, Error: ex.Message);
                }
            })).ToArray();

            var results = await Task.WhenAll(tasks);
            var successCount = results.Count(r => r.Success);
            Assert.True(successCount >= 1, "At least one task succeeded");

            // VERIFICATION on a fresh independent SQL Server connection
            await using var verifyDb = new SpendWiseDbContext(options, clock);

            // 1. EXACTLY ONE LedgerTransaction was posted with this key in SQL Server:
            var txList = await verifyDb.LedgerTransactions.AsNoTracking()
                .Where(t => t.IdempotencyKey == key && t.BusinessType == "V1FundContribution")
                .ToListAsync();
            Assert.Single(txList);

            // 2. EXACTLY ONE FinancialTransaction contribution:
            var ftCount = await verifyDb.FinancialTransactions.AsNoTracking()
                .CountAsync(ft => ft.Type == FinancialTransactionType.GroupFundContribution && ft.RelatedBusinessId == fundId && ft.Amount == 100_000);
            Assert.Equal(1, ftCount);

            // 3. Fund Treasury balance increased by EXACTLY 100,000 VND (from 500k to 600k)
            var treasury = await verifyDb.V1FundTreasuries.AsNoTracking().SingleAsync(t => t.FundId == fundId);
            var treasuryAccount = await verifyDb.LedgerAccounts.AsNoTracking().SingleAsync(a => a.Id == treasury.LedgerAccountId);
            Assert.Equal(600_000, (long)treasuryAccount.Balance);

            // 4. Member Wallet B was debited EXACTLY 100,000 VND (from 2,000,000 down to 1,900,000 VND)
            var freshWalletB = await verifyDb.Wallets.AsNoTracking().SingleAsync(w => w.Id == walletBId);
            Assert.Equal(1_900_000, (long)freshWalletB.AvailableBalance);
            var freshWalletBAccount = await verifyDb.LedgerAccounts.AsNoTracking().SingleAsync(a => a.WalletId == walletBId);
            Assert.Equal(1_900_000, (long)freshWalletBAccount.Balance);
        }
        finally
        {
            if (ownsCatalog)
            {
                await using var cleanupDb = new SpendWiseDbContext(
                    new DbContextOptionsBuilder<SpendWiseDbContext>().UseSqlServer(connectionString).Options, clock);
                await cleanupDb.Database.EnsureDeletedAsync();
            }
        }
    }

    [Fact]
    public async Task FixedFundV2_BackwardCompatibility_WithLegacyRefundScope_PreservedAndUnmodified()
    {
        var clock = new MutableClock(new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc));
        await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);

        var ownerA = await f.AddUserAsync("OwnerA");
        var memberB = await f.AddUserAsync("MemberB");
        await V1FundMembershipTests.MakeFriends(f, ownerA.Id, memberB.Id);

        var s = V1FundMembershipTests.Service(f);
        // 1. Fixed fund creation (Kind == Fixed)
        var fund = await s.CreateAsync(ownerA.Id, new("FixedFundV2_Compat", "Fixed", 200_000, TargetedCorrectnessFixture.SecurityPin), default);
        Assert.Equal("Fixed", (await s.GetAsync(ownerA.Id, fund.Id, default)).Kind);

        var invB = await s.InviteAsync(ownerA.Id, fund.Id, memberB.Id, default);
        await s.AcceptAsync(memberB.Id, invB.Id, default);

        // 2. Fixed Fund V2 Obligation Confirmation
        var walletA = await f.AddWalletAsync(ownerA.Id, "WalletA", true, balance: 1_000_000);
        var walletB = await f.AddWalletAsync(memberB.Id, "WalletB", true, balance: 1_000_000);

        var config = await s.ConfirmObligationsAsync(ownerA.Id, fund.Id, new(200_000, TargetedCorrectnessFixture.SecurityPin), default);

        // Member B contributes 100k
        await s.ContributeAsync(memberB.Id, fund.Id,
            new(walletB.Id, 100_000, "contrib-compat-b", TargetedCorrectnessFixture.SecurityPin, config.Revision), default);

        // 3. Member B leaves fund -> verify Legacy Refund Scope format:
        // Scope is strictly formatted as "v1-fund-refund:{fundId:N}:{membershipId:N}"
        var membershipB = await f.Db.V1FundMemberships.SingleAsync(m => m.FundId == fund.Id && m.UserId == memberB.Id);
        var expectedLegacyScope = $"v1-fund-refund:{fund.Id:N}:{membershipB.Id:N}";

        var leaveKey = "legacy-scope-leave-key";
        var leaveResult = await s.LeaveFundAsync(memberB.Id, fund.Id,
            new(TargetedCorrectnessFixture.SecurityPin, leaveKey), default);

        Assert.Equal(100_000, leaveResult.RefundAmountVnd);
        Assert.Equal("CaseA_Unspent", leaveResult.CaseType);
        Assert.Equal("Completed", leaveResult.Status);

        // Verify LedgerTransaction recorded with the exact legacy idempotency scope:
        var refundTx = await f.Db.LedgerTransactions.SingleAsync(t => t.Id == leaveResult.LedgerTransactionId);
        Assert.Equal(expectedLegacyScope, refundTx.IdempotencyScope);

        // 4. Verify Idempotent Replay with legacy refund scope:
        // Retrying with same key returns Replayed without creating any new transaction
        var replayResult = await s.LeaveFundAsync(memberB.Id, fund.Id,
            new(TargetedCorrectnessFixture.SecurityPin, leaveKey), default);
        Assert.Equal("Replayed", replayResult.CaseType);
        Assert.Equal(leaveResult.LedgerTransactionId, replayResult.LedgerTransactionId);
        Assert.Equal(100_000, replayResult.RefundAmountVnd);

        // 5. Subsequent calculation compatibility:
        // When owner previews remaining refund, verify treasury accounting is strictly reconciled:
        var previewOwner = await s.PreviewRefundAsync(ownerA.Id, fund.Id, null, default);
        Assert.Equal(0, previewOwner.EligibleRefundAmountVnd);
    }

    [Fact]
    public async Task FreeFund_HttpApi_OpenApiContract_RealServerSmokeTest()
    {
        var clock = new MutableClock(new DateTime(2026, 11, 1, 11, 0, 0, DateTimeKind.Utc));
        using var factory = new SpendWiseApiFactory(clock: clock);
        using var client = factory.CreateHttpsClient();

        // 1. OpenAPI Specification Contract Verification
        using var swaggerResponse = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, swaggerResponse.StatusCode);
        var swaggerJson = await swaggerResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(swaggerJson);
        var paths = doc.RootElement.GetProperty("paths");
        Assert.True(paths.TryGetProperty("/api/v1/funds", out _), "OpenAPI must contain /api/v1/funds");
        Assert.True(paths.TryGetProperty("/api/v1/funds/{id}/equity", out _), "OpenAPI must contain /api/v1/funds/{id}/equity");
        Assert.True(paths.TryGetProperty("/api/v1/funds/{id}/refund-preview", out _), "OpenAPI must contain /api/v1/funds/{id}/refund-preview");
        Assert.True(paths.TryGetProperty("/api/v1/funds/requests/{id}", out _), "OpenAPI must contain /api/v1/funds/requests/{id}");

        // 2. Unauthenticated Access is Rejected with 401 Unauthorized
        var dummyFundId = Guid.NewGuid();
        using var unauthResponse = await client.GetAsync($"/api/v1/funds/{dummyFundId}/equity");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthResponse.StatusCode);

        // 3. Setup Authenticated Users in Test DB
        Guid ownerId, walletAId;
        string ownerToken, strangerToken;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWiseDbContext>();
            var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
            var pinService = new SecurityPinService();

            var owner = new User
            {
                FullName = "OwnerHttp",
                Email = "owner_http@test.local",
                NormalizedEmail = "OWNER_HTTP@TEST.LOCAL",
                SpendId = "ownerhttp",
                NormalizedSpendId = "OWNERHTTP",
                PasswordHash = "hash",
                SecurityPinHash = pinService.Hash(TargetedCorrectnessFixture.SecurityPin),
                Status = UserStatus.Active,
                SystemRole = SystemRole.User
            };
            var stranger = new User
            {
                FullName = "StrangerHttp",
                Email = "stranger_http@test.local",
                NormalizedEmail = "STRANGER_HTTP@TEST.LOCAL",
                SpendId = "strangerhttp",
                NormalizedSpendId = "STRANGERHTTP",
                PasswordHash = "hash",
                SecurityPinHash = pinService.Hash(TargetedCorrectnessFixture.SecurityPin),
                Status = UserStatus.Active,
                SystemRole = SystemRole.User
            };
            db.Users.AddRange(owner, stranger);

            var sessionOwner = new UserSession { UserId = owner.Id, LastSeenAt = clock.UtcNow };
            var sessionStranger = new UserSession { UserId = stranger.Id, LastSeenAt = clock.UtcNow };
            db.UserSessions.AddRange(sessionOwner, sessionStranger);

            var bankA = new BankConnection
            {
                UserId = owner.Id,
                Provider = BankProvider.SePayBankHub,
                AccountEnvironment = SePayAccountEnvironment.TestMode,
                ExternalAccountId = "ACC_HTTP_A",
                BankBrandName = "MBBank",
                AccountHolderName = "OwnerHttp",
                MaskedAccountNumber = "****1234",
                Status = BankConnectionStatus.Active,
                LinkedAt = clock.UtcNow
            };
            db.BankConnections.Add(bankA);

            var walletA = WalletFactory.Create(owner.Id, "OwnerMain", WalletType.Main, true);
            walletA.AvailableBalance = 1_000_000;
            walletA.LedgerAccount.Balance = 1_000_000;
            db.Wallets.Add(walletA);

            await db.SaveChangesAsync();

            ownerId = owner.Id;
            walletAId = walletA.Id;
            ownerToken = tokenService.CreateAccessToken(owner, sessionOwner.Id).Token;
            strangerToken = tokenService.CreateAccessToken(stranger, sessionStranger.Id).Token;
        }

        // 4. Authenticated HTTP POST /api/v1/funds to create Free Fund
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var createPayload = new CreateV1FundRequest("HttpCreatedFreeFund", "Free", null, TargetedCorrectnessFixture.SecurityPin);
        using var createRes = await client.PostAsJsonAsync("/api/v1/funds", createPayload);
        Assert.Equal(HttpStatusCode.OK, createRes.StatusCode);
        var createdFund = await createRes.Content.ReadFromJsonAsync<V1FundDto>();
        Assert.NotNull(createdFund);
        Assert.Equal("HttpCreatedFreeFund", createdFund.Name);
        Assert.Equal("Free", createdFund.Kind);
        Assert.Equal(ownerId, createdFund.OwnerUserId);

        // 5. Authenticated HTTP POST /api/v1/funds/{id}/contributions to make initial contribution
        var contribPayload = new V1ContributeRequest(walletAId, 100_000, "http-contrib-key", TargetedCorrectnessFixture.SecurityPin);
        using var contribRes = await client.PostAsJsonAsync($"/api/v1/funds/{createdFund.Id}/contributions", contribPayload);
        Assert.Equal(HttpStatusCode.OK, contribRes.StatusCode);

        // 6. Authenticated HTTP GET /api/v1/funds/{id}/equity
        using var equityRes = await client.GetAsync($"/api/v1/funds/{createdFund.Id}/equity");
        Assert.Equal(HttpStatusCode.OK, equityRes.StatusCode);
        var equityDto = await equityRes.Content.ReadFromJsonAsync<V1FundEquityDto>();
        Assert.NotNull(equityDto);
        Assert.Equal("Free", equityDto.FundKind);
        Assert.Equal(100_000, equityDto.TotalBalanceVnd);
        Assert.Equal(100_000, equityDto.AvailableBalanceVnd);

        // 7. Authenticated HTTP GET /api/v1/funds/{id}/refund-preview
        using var previewRes = await client.GetAsync($"/api/v1/funds/{createdFund.Id}/refund-preview");
        Assert.Equal(HttpStatusCode.OK, previewRes.StatusCode);
        var previewDto = await previewRes.Content.ReadFromJsonAsync<V1RefundPreviewDto>();
        Assert.NotNull(previewDto);
        Assert.Equal("Eligible", previewDto.Status);
        Assert.Equal(100_000, previewDto.EligibleRefundAmountVnd);

        // 8. Outsider Access Control: Stranger attempting to access /equity gets 404/403
        using var outsiderClient = factory.CreateHttpsClient();
        outsiderClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", strangerToken);
        using var outsiderRes = await outsiderClient.GetAsync($"/api/v1/funds/{createdFund.Id}/equity");
        Assert.True(outsiderRes.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden,
            $"Expected 404 or 403, got {outsiderRes.StatusCode}");
    }

    private static V1FundService CreateSqlService(SpendWiseDbContext db, IClock clock) => new(
        db, clock,
        new FinancialSecurityGuard(db, new SecurityPinService(), new AccountCredentialLockout(db, clock)),
        new SpendWise.Infrastructure.Notifications.NotificationService(db),
        new SpendWise.Infrastructure.Finance.LedgerService(db, clock),
        new SpendWise.Application.Features.Banking.ActiveBankGuard(db));
}
