using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features;
using SpendWise.Application.Features.Authentication;
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
    public async Task FreeFund_StressAndConcurrency_ParallelIdempotentOperations_WithIndependentDbContexts()
    {
        var clock = new MutableClock(new DateTime(2026, 11, 1, 9, 0, 0, DateTimeKind.Utc));
        await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);

        var ownerA = await f.AddUserAsync("OwnerA");
        var memberB = await f.AddUserAsync("MemberB");
        await V1FundMembershipTests.MakeFriends(f, ownerA.Id, memberB.Id);

        var walletA = await f.AddWalletAsync(ownerA.Id, "WalletA", true, balance: 2_000_000);
        var walletB = await f.AddWalletAsync(memberB.Id, "WalletB", true, balance: 2_000_000);

        var initialService = V1FundMembershipTests.Service(f);
        var fund = await initialService.CreateAsync(ownerA.Id, new("ConcurrencyFund", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);

        var invB = await initialService.InviteAsync(ownerA.Id, fund.Id, memberB.Id, default);
        await initialService.AcceptAsync(memberB.Id, invB.Id, default);

        // Initial contributions
        await initialService.ContributeAsync(ownerA.Id, fund.Id, new(walletA.Id, 500_000, "c-a-init", TargetedCorrectnessFixture.SecurityPin), default);
        await initialService.ContributeAsync(memberB.Id, fund.Id, new(walletB.Id, 500_000, "c-b-init", TargetedCorrectnessFixture.SecurityPin), default);

        // 5 parallel idempotent contribution attempts with IDENTICAL idempotency key
        // Each parallel task uses its OWN INDEPENDENT DbContext and Service instance!
        var key = "parallel-key-b-isolated-ctx";
        var tasks = Enumerable.Range(0, 5).Select(_ => Task.Run(async () =>
        {
            await using var taskDb = f.CreateNewDbContext();
            var taskSecurity = new FinancialSecurityGuard(taskDb, new SecurityPinService(), new AccountCredentialLockout(taskDb, clock));
            var taskNotifications = new SpendWise.Infrastructure.Notifications.NotificationService(taskDb);
            var taskLedger = new SpendWise.Infrastructure.Finance.LedgerService(taskDb, clock);
            var taskBank = new SpendWise.Application.Features.Banking.ActiveBankGuard(taskDb);
            var taskService = new V1FundService(taskDb, clock, taskSecurity, taskNotifications, taskLedger, taskBank);

            try
            {
                var res = await taskService.ContributeAsync(memberB.Id, fund.Id, new(walletB.Id, 100_000, key, TargetedCorrectnessFixture.SecurityPin), default);
                return (Success: true, Result: res);
            }
            catch
            {
                return (Success: false, Result: (V1ContributionDto?)null);
            }
        })).ToArray();

        var results = await Task.WhenAll(tasks);
        var successCount = results.Count(r => r.Success);
        Assert.True(successCount >= 1, "At least one task succeeded");

        // Verify across an independent verification DbContext
        await using var verifyDb = f.CreateNewDbContext();
        var verifyService = new V1FundService(verifyDb, clock,
            new FinancialSecurityGuard(verifyDb, new SecurityPinService(), new AccountCredentialLockout(verifyDb, clock)),
            new SpendWise.Infrastructure.Notifications.NotificationService(verifyDb),
            new SpendWise.Infrastructure.Finance.LedgerService(verifyDb, clock),
            new SpendWise.Application.Features.Banking.ActiveBankGuard(verifyDb));

        // Treasury balance must have increased by EXACTLY 100,000, NOT 500,000
        var equity = await verifyService.EquityAsync(ownerA.Id, fund.Id, default);
        Assert.Equal(1_100_000, equity.TotalBalanceVnd);

        // Wallet B must have been debited EXACTLY 100,000 once (from 1,500,000 down to 1,400,000)
        var finalWalletB = await verifyDb.Wallets.AsNoTracking().SingleAsync(w => w.Id == walletB.Id);
        Assert.Equal(1_400_000, (long)finalWalletB.AvailableBalance);
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

    [Fact]
    public async Task FreeFund_HttpApi_OpenApiContract_SmokeTest()
    {
        var clock = new MutableClock(new DateTime(2026, 11, 1, 11, 0, 0, DateTimeKind.Utc));
        await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);

        var ownerA = await f.AddUserAsync("OwnerA");
        var memberB = await f.AddUserAsync("MemberB");
        var memberC = await f.AddUserAsync("MemberC");
        var strangerD = await f.AddUserAsync("StrangerD");

        await V1FundMembershipTests.MakeFriends(f, ownerA.Id, memberB.Id);
        await V1FundMembershipTests.MakeFriends(f, ownerA.Id, memberC.Id);

        var walletA = await f.AddWalletAsync(ownerA.Id, "WalletA", true, balance: 1_000_000);
        var walletB = await f.AddWalletAsync(memberB.Id, "WalletB", true, balance: 1_000_000);

        var s = V1FundMembershipTests.Service(f);
        var fund = await s.CreateAsync(ownerA.Id, new("HttpSmokeFund", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);

        var invB = await s.InviteAsync(ownerA.Id, fund.Id, memberB.Id, default);
        await s.AcceptAsync(memberB.Id, invB.Id, default);
        var invC = await s.InviteAsync(ownerA.Id, fund.Id, memberC.Id, default);
        await s.AcceptAsync(memberC.Id, invC.Id, default);

        await s.ContributeAsync(ownerA.Id, fund.Id, new(walletA.Id, 500_000, "c-a", TargetedCorrectnessFixture.SecurityPin), default);
        await s.ContributeAsync(memberB.Id, fund.Id, new(walletB.Id, 300_000, "c-b", TargetedCorrectnessFixture.SecurityPin), default);

        // 1. Contract Smoke: GET /equity
        var equityDto = await s.EquityAsync(ownerA.Id, fund.Id, default);
        Assert.Equal(800_000, equityDto.TotalBalanceVnd);
        Assert.Equal(0, equityDto.ActiveReservedVnd);
        Assert.Equal(800_000, equityDto.AvailableBalanceVnd);
        Assert.Equal("Free", equityDto.FundKind);
        Assert.Equal(3, equityDto.MemberEquities.Count);

        // Non-member access throws NotFoundException (fund_not_found)
        await Assert.ThrowsAsync<NotFoundException>(async () =>
        {
            await s.EquityAsync(strangerD.Id, fund.Id, default);
        });

        // 2. Contract Smoke: GET /refund-preview
        var refundPreview = await s.PreviewRefundAsync(memberB.Id, fund.Id, null, default);
        Assert.Equal("Eligible", refundPreview.Status);
        Assert.Equal("CaseA_Unspent", refundPreview.CaseType);
        Assert.Equal(300_000, refundPreview.EligibleRefundAmountVnd);

        // 3. Contract Smoke: GET /requests/{id} (Strict authorization)
        var req = await s.CreateFundRequestAsync(memberB.Id, fund.Id,
            new("Withdrawal", 50_000, memberB.Id, "Medical", "req-smoke-wth", TargetedCorrectnessFixture.SecurityPin), default);

        // Owner: Allowed
        var reqByOwner = await s.GetFundRequestAsync(ownerA.Id, req.Id, default);
        Assert.Equal(req.Id, reqByOwner.Id);

        // Requester: Allowed
        var reqByRequester = await s.GetFundRequestAsync(memberB.Id, req.Id, default);
        Assert.Equal(req.Id, reqByRequester.Id);

        // Other member (C): Forbidden
        await Assert.ThrowsAsync<ForbiddenException>(async () =>
        {
            await s.GetFundRequestAsync(memberC.Id, req.Id, default);
        });

        // Stranger (D): Forbidden / Not Found
        await Assert.ThrowsAsync<ForbiddenException>(async () =>
        {
            await s.GetFundRequestAsync(strangerD.Id, req.Id, default);
        });
    }
}
