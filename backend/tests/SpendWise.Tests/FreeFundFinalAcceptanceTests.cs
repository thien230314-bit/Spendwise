using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features;
using SpendWise.Application.Features.V1Groups;
using SpendWise.Domain.Finance;
using SpendWise.Domain.Notifications;
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
        // Total system capital = 3,000,000 VND
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

        // 4. Contributions: A contributes 500k, B contributes 300k, C contributes 200k (Total fund: 1,000,000)
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

        // 5. Member C requests a Withdrawal of 50,000 -> Owner approves -> Real cash withdrawal
        var wthReq = await s.CreateFundRequestAsync(memberC.Id, fund.Id,
            new("Withdrawal", 50_000, memberC.Id, "Personal cash needed", "req-wth-e2e", TargetedCorrectnessFixture.SecurityPin), default);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);
        await s.ReviewFundRequestAsync(ownerA.Id, wthReq.Id, true, "Approved withdrawal", TargetedCorrectnessFixture.SecurityPin, default);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);

        // Verify Wallet C received the 50k withdrawal
        var wCBalanceAfterWithdrawal = (await f.Db.Wallets.AsNoTracking().SingleAsync(w => w.Id == walletC.Id)).AvailableBalance;
        Assert.Equal(initialUserMoney - 200_000 + 50_000, (long)wCBalanceAfterWithdrawal); // 850,000

        // 6. Member B requests an Expense of 100,000 -> Owner approves -> Reserved (ApprovedUnused)
        var expReq = await s.CreateFundRequestAsync(memberB.Id, fund.Id,
            new("Expense", 100_000, memberB.Id, "Group dinner", "req-exp-e2e", TargetedCorrectnessFixture.SecurityPin), default);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);
        await s.ReviewFundRequestAsync(ownerA.Id, expReq.Id, true, "Approved dinner", TargetedCorrectnessFixture.SecurityPin, default);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);

        var equityMid2 = await s.EquityAsync(ownerA.Id, fund.Id, default);
        Assert.Equal(950_000, equityMid2.TotalBalanceVnd);
        Assert.Equal(100_000, equityMid2.ActiveReservedVnd);
        Assert.Equal(850_000, equityMid2.AvailableBalanceVnd);

        // 7. Member B leaves the fund:
        // B's approved unused expense (100k) is automatically canceled
        // B gets refunded their remaining equity (300,000 VND)
        var leaveB = await s.LeaveFundAsync(memberB.Id, fund.Id, new(TargetedCorrectnessFixture.SecurityPin, "leave-b-e2e"), default);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);
        Assert.Equal(300_000, leaveB.RefundAmountVnd);

        // Verify B's approved request is now Cancelled
        var expReqAfterLeave = await s.GetFundRequestAsync(ownerA.Id, expReq.Id, default);
        Assert.Equal("Cancelled", expReqAfterLeave.State);

        // 8. Owner A kicks Member C:
        // C had 200k contributed - 50k withdrawn = 150k net equity
        var kickC = await s.KickMemberAsync(ownerA.Id, fund.Id, memberC.Id,
            new(TargetedCorrectnessFixture.SecurityPin, "Kicking C in lifecycle test", "kick-c-e2e"), default);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);
        Assert.Equal(150_000, kickC.RefundAmountVnd);

        // 9. Owner A dissolves the fund:
        // A receives the remaining balance (500,000 VND)
        var dissolveResult = await s.DissolveFundAsync(ownerA.Id, fund.Id,
            new(TargetedCorrectnessFixture.SecurityPin, "dissolve-e2e"), default);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);
        Assert.Equal(500_000, dissolveResult.TotalRefundedVnd);
        Assert.Equal(0, (long)dissolveResult.FinalBalanceVnd);

        // 10. POST-DISSOLUTION GLOBAL FINANCIAL CONSERVATION INVARIANT:
        var finalWA = (long)(await f.Db.Wallets.AsNoTracking().SingleAsync(w => w.Id == walletA.Id)).AvailableBalance;
        var finalWB = (long)(await f.Db.Wallets.AsNoTracking().SingleAsync(w => w.Id == walletB.Id)).AvailableBalance;
        var finalWC = (long)(await f.Db.Wallets.AsNoTracking().SingleAsync(w => w.Id == walletC.Id)).AvailableBalance;

        // Post-dissolution fund balance from dissolveResult
        var finalFundBalance = (long)dissolveResult.FinalBalanceVnd;

        Assert.Equal(0, finalFundBalance);
        Assert.Equal(initialUserMoney, finalWA); // 1,000,000
        Assert.Equal(initialUserMoney, finalWB); // 1,000,000
        Assert.Equal(initialUserMoney, finalWC); // 1,000,000

        var finalTotalSystemMoney = finalWA + finalWB + finalWC + finalFundBalance;
        Assert.Equal(totalSystemCapital, finalTotalSystemMoney);
    }

    [Fact]
    public async Task FreeFund_StressAndConcurrency_ParallelIdempotentOperations_PreservesFinancialIntegrity()
    {
        var clock = new MutableClock(new DateTime(2026, 11, 1, 9, 0, 0, DateTimeKind.Utc));
        await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);

        var ownerA = await f.AddUserAsync("OwnerA");
        var memberB = await f.AddUserAsync("MemberB");
        await V1FundMembershipTests.MakeFriends(f, ownerA.Id, memberB.Id);

        var walletA = await f.AddWalletAsync(ownerA.Id, "WalletA", true, balance: 2_000_000);
        var walletB = await f.AddWalletAsync(memberB.Id, "WalletB", true, balance: 2_000_000);

        var s = V1FundMembershipTests.Service(f);
        var fund = await s.CreateAsync(ownerA.Id, new("ConcurrencyFund", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);

        var invB = await s.InviteAsync(ownerA.Id, fund.Id, memberB.Id, default);
        await s.AcceptAsync(memberB.Id, invB.Id, default);

        // Initial contributions
        await s.ContributeAsync(ownerA.Id, fund.Id, new(walletA.Id, 500_000, "c-a-init", TargetedCorrectnessFixture.SecurityPin), default);
        await s.ContributeAsync(memberB.Id, fund.Id, new(walletB.Id, 500_000, "c-b-init", TargetedCorrectnessFixture.SecurityPin), default);

        // Parallel idempotent contribution attempts with identical idempotency key
        var key = "parallel-key-b-1";
        var tasks = Enumerable.Range(0, 5).Select(_ => Task.Run(async () =>
        {
            try
            {
                await s.ContributeAsync(memberB.Id, fund.Id, new(walletB.Id, 100_000, key, TargetedCorrectnessFixture.SecurityPin), default);
                return true;
            }
            catch
            {
                return false;
            }
        })).ToArray();

        var results = await Task.WhenAll(tasks);
        Assert.Contains(true, results); // At least one succeeded

        // Treasury balance must have increased by EXACTLY 100,000, NOT 500,000
        var equity = await s.EquityAsync(ownerA.Id, fund.Id, default);
        Assert.Equal(1_100_000, equity.TotalBalanceVnd);
    }

    [Fact]
    public async Task FixedFundV2_BackwardCompatibility_PreservedAndUnmodified()
    {
        var clock = new MutableClock(new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc));
        await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);

        var ownerA = await f.AddUserAsync("OwnerA");
        var memberB = await f.AddUserAsync("MemberB");
        await V1FundMembershipTests.MakeFriends(f, ownerA.Id, memberB.Id);

        var s = V1FundMembershipTests.Service(f);
        // Fixed fund creation (Kind == Fixed)
        var fund = await s.CreateAsync(ownerA.Id, new("FixedFundV2_Compat", "Fixed", 10_000_000, TargetedCorrectnessFixture.SecurityPin), default);

        var invB = await s.InviteAsync(ownerA.Id, fund.Id, memberB.Id, default);
        await s.AcceptAsync(memberB.Id, invB.Id, default);

        // In Fixed Fund V2, owner attempting to set spending permission before treasury/obligations are confirmed throws ConflictException
        var ex = await Assert.ThrowsAsync<ConflictException>(async () =>
        {
            await s.SetSpendingPermissionAsync(ownerA.Id, fund.Id, memberB.Id, true, TargetedCorrectnessFixture.SecurityPin, default);
        });
        Assert.Equal("treasury_not_found", ex.Code);

        // Fixed fund kind remains untouched
        var fetchedFund = await s.GetAsync(ownerA.Id, fund.Id, default);
        Assert.Equal("Fixed", fetchedFund.Kind);
    }
}
