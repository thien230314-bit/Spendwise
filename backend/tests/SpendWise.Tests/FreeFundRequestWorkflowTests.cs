using SpendWise.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features;
using SpendWise.Application.Features.Authentication;
using SpendWise.Application.Features.V1Groups;
using SpendWise.Domain.Finance;
using SpendWise.Domain.V1Groups;
using Xunit;

namespace SpendWise.Tests;

public class FreeFundRequestWorkflowTests
{
    private sealed class MutableClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; set; } = utcNow;
    }

    [Fact]
    public async Task FreeFund_Member_CannotCreateTwoActiveExpenses_Sequential()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("OwnerA");
        var member = await f.AddUserAsync("MemberB");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);

        var s = V1FundMembershipTests.Service(f);
        var fund = await s.CreateAsync(owner.Id, new("LimitFund", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);

        var invite = await s.InviteAsync(owner.Id, fund.Id, member.Id, default);
        await s.AcceptAsync(member.Id, invite.Id, default);

        // Expense 1 succeeds
        var req1 = await s.CreateFundRequestAsync(member.Id, fund.Id,
            new("Expense", 50_000, member.Id, "Exp 1", "exp-1", TargetedCorrectnessFixture.SecurityPin), default);
        Assert.Equal("AwaitingOwnerReview", req1.State);

        // Expense 2 fails with granular limit exceeded code
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            s.CreateFundRequestAsync(member.Id, fund.Id,
                new("Expense", 30_000, member.Id, "Exp 2", "exp-2", TargetedCorrectnessFixture.SecurityPin), default));
        Assert.Equal("fund_active_expense_limit_exceeded", ex.Code);
    }

    [Fact]
    public async Task FreeFund_Member_CannotCreateTwoActiveWithdrawals_Sequential()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("OwnerA");
        var member = await f.AddUserAsync("MemberB");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);

        var s = V1FundMembershipTests.Service(f);
        var fund = await s.CreateAsync(owner.Id, new("LimitFundWth", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);

        var invite = await s.InviteAsync(owner.Id, fund.Id, member.Id, default);
        await s.AcceptAsync(member.Id, invite.Id, default);

        // Withdrawal 1 succeeds
        var req1 = await s.CreateFundRequestAsync(member.Id, fund.Id,
            new("Withdrawal", 50_000, member.Id, "Wth 1", "wth-1", TargetedCorrectnessFixture.SecurityPin), default);
        Assert.Equal("AwaitingOwnerReview", req1.State);

        // Withdrawal 2 fails with granular limit exceeded code
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            s.CreateFundRequestAsync(member.Id, fund.Id,
                new("Withdrawal", 30_000, member.Id, "Wth 2", "wth-2", TargetedCorrectnessFixture.SecurityPin), default));
        Assert.Equal("fund_active_withdrawal_limit_exceeded", ex.Code);
    }

    [Fact]
    public async Task FreeFund_ConcurrentExpenseCreation_FromTwoSeparateContexts_EnforcesActiveLimit()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("OwnerA");
        var member = await f.AddUserAsync("MemberB");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);

        var s1 = V1FundMembershipTests.Service(f);
        var fund = await s1.CreateAsync(owner.Id, new("ConcurFund", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);

        var invite = await s1.InviteAsync(owner.Id, fund.Id, member.Id, default);
        await s1.AcceptAsync(member.Id, invite.Id, default);

        // Expense 1 created via service context 1
        var req1 = await s1.CreateFundRequestAsync(member.Id, fund.Id,
            new("Expense", 40_000, member.Id, "Parallel 1", "par-1", TargetedCorrectnessFixture.SecurityPin), default);
        Assert.Equal("AwaitingOwnerReview", req1.State);

        // Attempt creating a second active expense from a separate DbContext instance
        // to verify active request limit is strictly enforced across contexts
        await using var db2 = f.CreateNewDbContext();
        var sec2 = new FinancialSecurityGuard(db2, new SecurityPinService(), new AccountCredentialLockout(db2, f.Clock));
        var s2 = new V1FundService(db2, f.Clock, sec2,
            new SpendWise.Infrastructure.Notifications.NotificationService(db2),
            new SpendWise.Infrastructure.Finance.LedgerService(db2, f.Clock),
            new SpendWise.Application.Features.Banking.ActiveBankGuard(db2));

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            s2.CreateFundRequestAsync(member.Id, fund.Id,
                new("Expense", 60_000, member.Id, "Parallel 2", "par-2", TargetedCorrectnessFixture.SecurityPin), default));
        Assert.Equal("fund_active_expense_limit_exceeded", ex.Code);

        // Verify database state: exactly 1 active request exists for this member
        var dbRequests = await f.Db.V1FundRequests
            .Where(r => r.FundId == fund.Id && r.RequestedByUserId == member.Id && r.State == "AwaitingOwnerReview")
            .ToListAsync();
        Assert.Single(dbRequests);
    }

    [Fact]
    public async Task FreeFund_Member_CanCreateOneExpenseAndOneWithdrawalConcurrently()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("OwnerA");
        var member = await f.AddUserAsync("MemberB");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);

        var s = V1FundMembershipTests.Service(f);
        var fund = await s.CreateAsync(owner.Id, new("DualKindFund", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);

        var invite = await s.InviteAsync(owner.Id, fund.Id, member.Id, default);
        await s.AcceptAsync(member.Id, invite.Id, default);

        // 1 Expense and 1 Withdrawal simultaneously are allowed
        var reqExp = await s.CreateFundRequestAsync(member.Id, fund.Id,
            new("Expense", 50_000, member.Id, "Dinner", "exp-dual", TargetedCorrectnessFixture.SecurityPin), default);
        var reqWth = await s.CreateFundRequestAsync(member.Id, fund.Id,
            new("Withdrawal", 30_000, member.Id, "Cash out", "wth-dual", TargetedCorrectnessFixture.SecurityPin), default);

        Assert.Equal("AwaitingOwnerReview", reqExp.State);
        Assert.Equal("AwaitingOwnerReview", reqWth.State);
    }

    [Fact]
    public async Task FreeFund_Expense_ApprovedByOwner_BecomesApprovedUnused_DoesNotTransferMoneyOrComplete()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("OwnerA");
        var member = await f.AddUserAsync("MemberB");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);

        var s = V1FundMembershipTests.Service(f);
        var fund = await s.CreateAsync(owner.Id, new("ApprUnusedFund", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);

        var invite = await s.InviteAsync(owner.Id, fund.Id, member.Id, default);
        await s.AcceptAsync(member.Id, invite.Id, default);

        var ownerWallet = await f.AddWalletAsync(owner.Id, "OwnerWallet", true, balance: 1_000_000);
        var memberWallet = await f.AddWalletAsync(member.Id, "MemberWallet", true, balance: 500_000);

        await s.ContributeAsync(owner.Id, fund.Id,
            new(ownerWallet.Id, 500_000, "c-owner", TargetedCorrectnessFixture.SecurityPin), default);

        var treasury = await f.Db.V1FundTreasuries.FirstAsync(t => t.FundId == fund.Id);
        var initialTreasuryBalance = (await f.Db.LedgerAccounts.FirstAsync(a => a.Id == treasury.LedgerAccountId)).Balance;
        Assert.Equal(500_000, initialTreasuryBalance);

        // Member requests Expense
        var req = await s.CreateFundRequestAsync(member.Id, fund.Id,
            new("Expense", 200_000, member.Id, "Team food", "exp-food", TargetedCorrectnessFixture.SecurityPin), default);

        // Owner approves
        var reviewed = await s.ReviewFundRequestAsync(owner.Id, req.Id, true, "Approved for spending", TargetedCorrectnessFixture.SecurityPin, default);

        // Assert state is ApprovedUnused
        Assert.Equal("ApprovedUnused", reviewed.State);
        Assert.Equal("ApprovedUnused", reviewed.ExecutionAvailability);

        // Assert NO funds transfer occurred: treasury balance unchanged
        var currentTreasuryBalance = (await f.Db.LedgerAccounts.FirstAsync(a => a.Id == treasury.LedgerAccountId)).Balance;
        Assert.Equal(500_000, currentTreasuryBalance);

        // Assert member wallet unchanged
        var currentMemberWallet = await f.Db.Wallets.FirstAsync(w => w.Id == memberWallet.Id);
        Assert.Equal(500_000, currentMemberWallet.AvailableBalance);

        // Assert no ledger payout transaction created
        var payoutTxs = await f.Db.LedgerTransactions.Where(t => t.BusinessType == "V1FundExpense" && t.BusinessId == fund.Id).ToListAsync();
        Assert.Empty(payoutTxs);
    }

    [Fact]
    public async Task FreeFund_ApprovedUnused_ExpiresAfterSevenDays_AndPersistsToDatabase()
    {
        var clock = new MutableClock(new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc));
        await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);
        var owner = await f.AddUserAsync("OwnerA");
        var member = await f.AddUserAsync("MemberB");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);

        var s = V1FundMembershipTests.Service(f);
        var fund = await s.CreateAsync(owner.Id, new("ExpireFund", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);

        var invite = await s.InviteAsync(owner.Id, fund.Id, member.Id, default);
        await s.AcceptAsync(member.Id, invite.Id, default);

        var ownerWallet = await f.AddWalletAsync(owner.Id, "OwnerWallet", true, balance: 1_000_000);
        await s.ContributeAsync(owner.Id, fund.Id,
            new(ownerWallet.Id, 500_000, "c-owner", TargetedCorrectnessFixture.SecurityPin), default);

        var req = await s.CreateFundRequestAsync(member.Id, fund.Id,
            new("Expense", 100_000, member.Id, "Office supplies", "exp-supplies", TargetedCorrectnessFixture.SecurityPin), default);
        await s.ReviewFundRequestAsync(owner.Id, req.Id, true, "Approved", TargetedCorrectnessFixture.SecurityPin, default);

        // Within 7 days, state is ApprovedUnused
        var reqsDay1 = await s.FundRequestsAsync(owner.Id, fund.Id, 1, 10, default);
        Assert.Equal("ApprovedUnused", reqsDay1.Single().State);

        // Fast forward 7 days + 1 minute
        clock.UtcNow = clock.UtcNow.AddDays(7).AddMinutes(1);

        // Querying requests triggers the persistent sweep and returns Expired
        var reqsDay8 = await s.FundRequestsAsync(owner.Id, fund.Id, 1, 10, default);
        Assert.Equal("Expired", reqsDay8.Single().State);

        // VERIFY DATABASE PERSISTENCE:
        var dbItem = await f.Db.V1FundRequests.AsNoTracking().FirstAsync(r => r.Id == req.Id);
        Assert.Equal("Expired", dbItem.State);
        Assert.NotNull(dbItem.CancelledAt);
        Assert.Contains("tự hết hạn sau 7 ngày", dbItem.ReviewReason);

        // VERIFY RESERVATION RELEASED: Owner can now spend the full 500,000 VND
        var fullSpend = await s.CreateFundRequestAsync(owner.Id, fund.Id,
            new("Expense", 500_000, owner.Id, "Spend all after expiration", "exp-all", TargetedCorrectnessFixture.SecurityPin), default);
        Assert.Equal("Completed", fullSpend.State);

        // VERIFY NO DUPLICATE ACTIVITIES: Calling sweep again does not duplicate activities
        var count = await s.ExpireStaleApprovedUnusedRequestsAsync(fund.Id, default);
        Assert.Equal(0, count);

        var activities = await f.Db.V1FundActivities
            .Where(a => a.FundId == fund.Id && a.Event == "FundRequestExpired")
            .ToListAsync();
        Assert.Single(activities);
    }

    [Fact]
    public async Task FreeFund_Requests_MemberCanOnlyViewOwnRequests_OwnerCanViewAll()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("OwnerA");
        var memberB = await f.AddUserAsync("MemberB");
        var memberC = await f.AddUserAsync("MemberC");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, memberB.Id);
        await V1FundMembershipTests.MakeFriends(f, owner.Id, memberC.Id);

        var s = V1FundMembershipTests.Service(f);
        var fund = await s.CreateAsync(owner.Id, new("PrivacyFund", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);

        var inviteB = await s.InviteAsync(owner.Id, fund.Id, memberB.Id, default);
        await s.AcceptAsync(memberB.Id, inviteB.Id, default);
        var inviteC = await s.InviteAsync(owner.Id, fund.Id, memberC.Id, default);
        await s.AcceptAsync(memberC.Id, inviteC.Id, default);

        var ownerWallet = await f.AddWalletAsync(owner.Id, "OwnerWallet", true, balance: 1_000_000);
        var memberBWallet = await f.AddWalletAsync(memberB.Id, "MemberBWallet", true, balance: 1_000_000);
        var memberCWallet = await f.AddWalletAsync(memberC.Id, "MemberCWallet", true, balance: 1_000_000);

        await s.ContributeAsync(owner.Id, fund.Id, new(ownerWallet.Id, 200_000, "c-owner", TargetedCorrectnessFixture.SecurityPin), default);
        await s.ContributeAsync(memberB.Id, fund.Id, new(memberBWallet.Id, 200_000, "c-b", TargetedCorrectnessFixture.SecurityPin), default);
        await s.ContributeAsync(memberC.Id, fund.Id, new(memberCWallet.Id, 200_000, "c-c", TargetedCorrectnessFixture.SecurityPin), default);

        // Member B creates Expense request
        var reqB = await s.CreateFundRequestAsync(memberB.Id, fund.Id,
            new("Expense", 50_000, memberB.Id, "B Expense", "exp-b", TargetedCorrectnessFixture.SecurityPin), default);

        // Member C creates Withdrawal request
        var reqC = await s.CreateFundRequestAsync(memberC.Id, fund.Id,
            new("Withdrawal", 60_000, memberC.Id, "C Withdrawal", "wth-c", TargetedCorrectnessFixture.SecurityPin), default);

        // Member B views requests: ONLY sees Member B's request
        var listB = await s.FundRequestsAsync(memberB.Id, fund.Id, 1, 10, default);
        Assert.Single(listB);
        Assert.Equal(reqB.Id, listB.Single().Id);

        // Member C views requests: ONLY sees Member C's request
        var listC = await s.FundRequestsAsync(memberC.Id, fund.Id, 1, 10, default);
        Assert.Single(listC);
        Assert.Equal(reqC.Id, listC.Single().Id);

        // Owner views requests: sees BOTH requests
        var listOwner = await s.FundRequestsAsync(owner.Id, fund.Id, 1, 10, default);
        Assert.Equal(2, listOwner.Count);
        Assert.Contains(listOwner, r => r.Id == reqB.Id);
        Assert.Contains(listOwner, r => r.Id == reqC.Id);
    }

    [Fact]
    public async Task FreeFund_InitiatePayment_WhenProviderUnavailable_RejectsAndKeepsApprovedUnused()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("OwnerA");
        var member = await f.AddUserAsync("MemberB");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);

        var s = V1FundMembershipTests.Service(f);
        var fund = await s.CreateAsync(owner.Id, new("ProviderGuardFund", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);

        var invite = await s.InviteAsync(owner.Id, fund.Id, member.Id, default);
        await s.AcceptAsync(member.Id, invite.Id, default);

        var ownerWallet = await f.AddWalletAsync(owner.Id, "OwnerWallet", true, balance: 1_000_000);
        await s.ContributeAsync(owner.Id, fund.Id,
            new(ownerWallet.Id, 500_000, "c-owner", TargetedCorrectnessFixture.SecurityPin), default);

        var reqExp = await s.CreateFundRequestAsync(member.Id, fund.Id,
            new("Expense", 100_000, member.Id, "Internet bill", "exp-net", TargetedCorrectnessFixture.SecurityPin), default);
        await s.ReviewFundRequestAsync(owner.Id, reqExp.Id, true, "Approved", TargetedCorrectnessFixture.SecurityPin, default);

        // Attempt to initiate payment when no provider is integrated
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            s.InitiateExpensePaymentAsync(member.Id, reqExp.Id, TargetedCorrectnessFixture.SecurityPin, default));

        Assert.Equal("fund_payment_provider_unavailable", ex.Code);

        // CRITICAL CHECK: State remains ApprovedUnused in DB, does NOT become PaymentProcessing, fund is not locked!
        var dbItem = await f.Db.V1FundRequests.AsNoTracking().FirstAsync(r => r.Id == reqExp.Id);
        Assert.Equal("ApprovedUnused", dbItem.State);

        // Member CAN still cancel the request since it is ApprovedUnused (not PaymentProcessing)
        var cancelled = await s.CancelFundRequestAsync(member.Id, reqExp.Id, TargetedCorrectnessFixture.SecurityPin, default);
        Assert.Equal("Cancelled", cancelled.State);
    }

    [Fact]
    public async Task FreeFund_Withdrawal_BlockedWhenActiveApprovedUnusedExists()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("OwnerA");
        var member = await f.AddUserAsync("MemberB");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);

        var s = V1FundMembershipTests.Service(f);
        var fund = await s.CreateAsync(owner.Id, new("BlockWthFund", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);

        var invite = await s.InviteAsync(owner.Id, fund.Id, member.Id, default);
        await s.AcceptAsync(member.Id, invite.Id, default);

        var ownerWallet = await f.AddWalletAsync(owner.Id, "OwnerWallet", true, balance: 1_000_000);
        var memberWallet = await f.AddWalletAsync(member.Id, "MemberWallet", true, balance: 1_000_000);

        await s.ContributeAsync(owner.Id, fund.Id,
            new(ownerWallet.Id, 600_000, "c-a", TargetedCorrectnessFixture.SecurityPin), default);
        await s.ContributeAsync(member.Id, fund.Id,
            new(memberWallet.Id, 400_000, "c-b", TargetedCorrectnessFixture.SecurityPin), default);

        // Member creates Expense and Owner approves -> ApprovedUnused
        var reqExp = await s.CreateFundRequestAsync(member.Id, fund.Id,
            new("Expense", 100_000, member.Id, "Snacks", "exp-snack", TargetedCorrectnessFixture.SecurityPin), default);
        await s.ReviewFundRequestAsync(owner.Id, reqExp.Id, true, "Approved", TargetedCorrectnessFixture.SecurityPin, default);

        // Member requests Withdrawal
        var reqWth = await s.CreateFundRequestAsync(member.Id, fund.Id,
            new("Withdrawal", 50_000, member.Id, "Personal cash", "wth-cash", TargetedCorrectnessFixture.SecurityPin), default);

        // Owner attempts to approve Withdrawal -> Blocked!
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            s.ReviewFundRequestAsync(owner.Id, reqWth.Id, true, "Approve withdrawal", TargetedCorrectnessFixture.SecurityPin, default));
        Assert.Equal("fund_withdrawal_blocked_by_active_expense", ex.Code);

        // Verify withdrawal is still AwaitingOwnerReview (not rejected)
        var wthInDb = await f.Db.V1FundRequests.FirstAsync(r => r.Id == reqWth.Id);
        Assert.Equal("AwaitingOwnerReview", wthInDb.State);
    }

    [Fact]
    public async Task FreeFund_Withdrawal_UnblockedAfterApprovedUnusedCancelled()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("OwnerA");
        var member = await f.AddUserAsync("MemberB");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);

        var s = V1FundMembershipTests.Service(f);
        var fund = await s.CreateAsync(owner.Id, new("UnblockFund", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);

        var invite = await s.InviteAsync(owner.Id, fund.Id, member.Id, default);
        await s.AcceptAsync(member.Id, invite.Id, default);

        var ownerWallet = await f.AddWalletAsync(owner.Id, "OwnerWallet", true, balance: 1_000_000);
        var memberWallet = await f.AddWalletAsync(member.Id, "MemberWallet", true, balance: 1_000_000);

        await s.ContributeAsync(owner.Id, fund.Id,
            new(ownerWallet.Id, 600_000, "c-a", TargetedCorrectnessFixture.SecurityPin), default);
        await s.ContributeAsync(member.Id, fund.Id,
            new(memberWallet.Id, 400_000, "c-b", TargetedCorrectnessFixture.SecurityPin), default);

        // Member creates Expense and Owner approves -> ApprovedUnused
        var reqExp = await s.CreateFundRequestAsync(member.Id, fund.Id,
            new("Expense", 100_000, member.Id, "Snacks", "exp-snack", TargetedCorrectnessFixture.SecurityPin), default);
        await s.ReviewFundRequestAsync(owner.Id, reqExp.Id, true, "Approved", TargetedCorrectnessFixture.SecurityPin, default);

        // Member requests Withdrawal
        var reqWth = await s.CreateFundRequestAsync(member.Id, fund.Id,
            new("Withdrawal", 50_000, member.Id, "Personal cash", "wth-cash", TargetedCorrectnessFixture.SecurityPin), default);

        // Cancel the ApprovedUnused expense
        await s.CancelFundRequestAsync(member.Id, reqExp.Id, TargetedCorrectnessFixture.SecurityPin, default);
        var expInDb = await f.Db.V1FundRequests.FirstAsync(r => r.Id == reqExp.Id);
        Assert.Equal("Cancelled", expInDb.State);

        // Now Owner reviews and approves Withdrawal -> SUCCEEDS!
        var approvedWth = await s.ReviewFundRequestAsync(owner.Id, reqWth.Id, true, "Approve withdrawal", TargetedCorrectnessFixture.SecurityPin, default);
        Assert.Equal("Completed", approvedWth.State);

        // Funds transferred to member wallet
        var memberWalletAfter = await f.Db.Wallets.FirstAsync(w => w.Id == memberWallet.Id);
        Assert.Equal(650_000, memberWalletAfter.AvailableBalance); // 1,000,000 - 400,000 contribution + 50,000 withdrawal
    }

    [Fact]
    public async Task FreeFund_OwnerDirectSpend_CannotExceedUnreservedBalance()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("OwnerA");
        var member = await f.AddUserAsync("MemberB");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);

        var s = V1FundMembershipTests.Service(f);
        var fund = await s.CreateAsync(owner.Id, new("DirectSpendFund", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);

        var invite = await s.InviteAsync(owner.Id, fund.Id, member.Id, default);
        await s.AcceptAsync(member.Id, invite.Id, default);

        var ownerWallet = await f.AddWalletAsync(owner.Id, "OwnerWallet", true, balance: 1_000_000);
        await s.ContributeAsync(owner.Id, fund.Id,
            new(ownerWallet.Id, 1_000_000, "c-owner", TargetedCorrectnessFixture.SecurityPin), default);

        // Member creates Expense of 800,000 VND and Owner approves -> reserves 800,000 VND
        var reqExp = await s.CreateFundRequestAsync(member.Id, fund.Id,
            new("Expense", 800_000, member.Id, "Conference ticket", "exp-conf", TargetedCorrectnessFixture.SecurityPin), default);
        await s.ReviewFundRequestAsync(owner.Id, reqExp.Id, true, "Approved", TargetedCorrectnessFixture.SecurityPin, default);

        // Owner attempts direct spend of 300,000 VND (total unreserved is only 200,000 VND) -> FAILS!
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            s.CreateFundRequestAsync(owner.Id, fund.Id,
                new("Expense", 300_000, owner.Id, "Office desk", "exp-desk", TargetedCorrectnessFixture.SecurityPin), default));
        Assert.Equal("insufficient_unreserved_fund_balance", ex.Code);

        // Owner direct spend of 200,000 VND -> SUCCEEDS!
        var directSpend = await s.CreateFundRequestAsync(owner.Id, fund.Id,
            new("Expense", 200_000, owner.Id, "Office chair", "exp-chair", TargetedCorrectnessFixture.SecurityPin), default);
        Assert.Equal("Completed", directSpend.State);
    }

    [Fact]
    public async Task FreeFund_Withdrawal_ExceedingMemberEquity_ThrowsInsufficientMemberEquity()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("OwnerA");
        var member = await f.AddUserAsync("MemberB");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);

        var s = V1FundMembershipTests.Service(f);
        var fund = await s.CreateAsync(owner.Id, new("EquityWthFund", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);

        var invite = await s.InviteAsync(owner.Id, fund.Id, member.Id, default);
        await s.AcceptAsync(member.Id, invite.Id, default);

        var ownerWallet = await f.AddWalletAsync(owner.Id, "OwnerWallet", true, balance: 1_000_000);
        var memberWallet = await f.AddWalletAsync(member.Id, "MemberWallet", true, balance: 1_000_000);

        await s.ContributeAsync(owner.Id, fund.Id,
            new(ownerWallet.Id, 800_000, "c-owner", TargetedCorrectnessFixture.SecurityPin), default);
        await s.ContributeAsync(member.Id, fund.Id,
            new(memberWallet.Id, 200_000, "c-member", TargetedCorrectnessFixture.SecurityPin), default);

        // Member B requests Withdrawal of 250,000 VND (equity is only 200,000 VND)
        var reqWth = await s.CreateFundRequestAsync(member.Id, fund.Id,
            new("Withdrawal", 250_000, member.Id, "Too much withdrawal", "wth-excess", TargetedCorrectnessFixture.SecurityPin), default);

        // Owner attempts to approve -> Throws insufficient_member_equity
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            s.ReviewFundRequestAsync(owner.Id, reqWth.Id, true, "Approved", TargetedCorrectnessFixture.SecurityPin, default));
        Assert.Equal("insufficient_member_equity", ex.Code);
    }

    [Fact]
    public async Task FreeFund_AutoCancelPendingWithdrawal_WhenExpenseDropsMemberEquityBelowRequestedAmount()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("OwnerA");
        var member = await f.AddUserAsync("MemberB");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);

        var s = V1FundMembershipTests.Service(f);
        var fund = await s.CreateAsync(owner.Id, new("AutoCancelWthFund", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);

        var invite = await s.InviteAsync(owner.Id, fund.Id, member.Id, default);
        await s.AcceptAsync(member.Id, invite.Id, default);

        var ownerWallet = await f.AddWalletAsync(owner.Id, "OwnerWallet", true, balance: 1_000_000);
        var memberWallet = await f.AddWalletAsync(member.Id, "MemberWallet", true, balance: 1_000_000);

        // A contributes 600k (60%), B contributes 400k (40%). Total = 1,000,000 VND
        await s.ContributeAsync(owner.Id, fund.Id,
            new(ownerWallet.Id, 600_000, "c-a", TargetedCorrectnessFixture.SecurityPin), default);
        await s.ContributeAsync(member.Id, fund.Id,
            new(memberWallet.Id, 400_000, "c-b", TargetedCorrectnessFixture.SecurityPin), default);

        // Member B requests Withdrawal of 350,000 VND (valid when created, since equity is 400k)
        var reqWth = await s.CreateFundRequestAsync(member.Id, fund.Id,
            new("Withdrawal", 350_000, member.Id, "Cash out", "wth-pending", TargetedCorrectnessFixture.SecurityPin), default);
        Assert.Equal("AwaitingOwnerReview", reqWth.State);

        // Owner directly spends 200,000 VND on fund expense
        // Expense distribution: A pays 120k (remains 480k), B pays 80k (remains 320k)
        // Since B's equity (320k) is now less than requested withdrawal (350k),
        // the pending withdrawal must be automatically cancelled!
        await s.CreateFundRequestAsync(owner.Id, fund.Id,
            new("Expense", 200_000, owner.Id, "Team dinner", "exp-direct-team", TargetedCorrectnessFixture.SecurityPin), default);

        var wthAfterExpense = await f.Db.V1FundRequests.FirstAsync(r => r.Id == reqWth.Id);
        Assert.Equal("Cancelled", wthAfterExpense.State);
        Assert.NotNull(wthAfterExpense.CancelledAt);
        Assert.Contains("Tự động hủy do vốn cá nhân không còn đủ", wthAfterExpense.ReviewReason);
    }

    [Fact]
    public async Task FixedFundV2_RegressionCheck_ExpenseWorkflow_Unchanged()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("OwnerA");
        var member = await f.AddUserAsync("MemberB");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);

        var s = V1FundMembershipTests.Service(f);
        var fund = await s.CreateAsync(owner.Id, new("FixedFundRegression", "Fixed", 1_000_000, TargetedCorrectnessFixture.SecurityPin), default);

        var invite = await s.InviteAsync(owner.Id, fund.Id, member.Id, default);
        await s.AcceptAsync(member.Id, invite.Id, default);

        // Fixed Fund V2 requires owner to confirm obligations before contributions
        var config = await s.ConfirmObligationsAsync(owner.Id, fund.Id, new(1_000_000, TargetedCorrectnessFixture.SecurityPin), default);

        var ownerWallet = await f.AddWalletAsync(owner.Id, "OwnerWallet", true, balance: 1_000_000);
        var memberWallet = await f.AddWalletAsync(member.Id, "MemberWallet", true, balance: 1_000_000);

        // Fixed fund: owner and member contribute to 100% target (500k each)
        await s.ContributeAsync(owner.Id, fund.Id, new(ownerWallet.Id, 500_000, "c-owner", TargetedCorrectnessFixture.SecurityPin, config.Revision), default);
        await s.ContributeAsync(member.Id, fund.Id, new(memberWallet.Id, 500_000, "c-member", TargetedCorrectnessFixture.SecurityPin, config.Revision), default);

        // Member without spending permission cannot create expense
        var exPerm = await Assert.ThrowsAsync<ForbiddenException>(() =>
            s.CreateFundRequestAsync(member.Id, fund.Id,
                new("Expense", 100_000, member.Id, "Buy tools", "exp-tools", TargetedCorrectnessFixture.SecurityPin), default));
        Assert.Equal("fund_spending_permission_required", exPerm.Code);

        // Owner grants spending permission to member
        await s.SetSpendingPermissionAsync(owner.Id, fund.Id, member.Id, true, TargetedCorrectnessFixture.SecurityPin, default);

        // Now member creates expense: in Fixed Fund V2, it executes directly to Completed and transfers money
        var expDto = await s.CreateFundRequestAsync(member.Id, fund.Id,
            new("Expense", 100_000, member.Id, "Buy tools with perm", "exp-tools-ok", TargetedCorrectnessFixture.SecurityPin), default);

        Assert.Equal("Completed", expDto.State);
        Assert.Equal("Executed", expDto.ExecutionAvailability);

        // Fixed Fund does NOT allow Owner to review expenses (it throws fixed_owner_review_disallowed)
        var exReview = await Assert.ThrowsAsync<ConflictException>(() =>
            s.ReviewFundRequestAsync(owner.Id, expDto.Id, true, "Cannot review fixed", TargetedCorrectnessFixture.SecurityPin, default));
        Assert.Equal("fixed_owner_review_disallowed", exReview.Code);
    }

    [Fact]
    public async Task FreeFund_InitiatePayment_UnauthorizedCaller_ThrowsForbidden_BeforeExpirationCheck()
    {
        var clock = new MutableClock(new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc));
        await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);
        var owner = await f.AddUserAsync("OwnerA");
        var memberB = await f.AddUserAsync("MemberB");
        var memberC = await f.AddUserAsync("MemberC");
        var outsider = await f.AddUserAsync("Outsider");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, memberB.Id);
        await V1FundMembershipTests.MakeFriends(f, owner.Id, memberC.Id);

        var s = V1FundMembershipTests.Service(f);
        var fund = await s.CreateAsync(owner.Id, new("AuthFund", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);

        var inviteB = await s.InviteAsync(owner.Id, fund.Id, memberB.Id, default);
        await s.AcceptAsync(memberB.Id, inviteB.Id, default);
        var inviteC = await s.InviteAsync(owner.Id, fund.Id, memberC.Id, default);
        await s.AcceptAsync(memberC.Id, inviteC.Id, default);

        var ownerWallet = await f.AddWalletAsync(owner.Id, "OwnerWallet", true, balance: 1_000_000);
        await s.ContributeAsync(owner.Id, fund.Id,
            new(ownerWallet.Id, 500_000, "c-owner", TargetedCorrectnessFixture.SecurityPin), default);

        // Member B requests Expense and Owner approves -> ApprovedUnused
        var req = await s.CreateFundRequestAsync(memberB.Id, fund.Id,
            new("Expense", 100_000, memberB.Id, "Supplies", "exp-auth", TargetedCorrectnessFixture.SecurityPin), default);
        await s.ReviewFundRequestAsync(owner.Id, req.Id, true, "Approved", TargetedCorrectnessFixture.SecurityPin, default);

        // Fast forward 7 days + 1 min so it is expired
        clock.UtcNow = clock.UtcNow.AddDays(7).AddMinutes(1);

        // 1. Outsider calls initiate-payment -> Throws ForbiddenException / NotFoundException (not member)
        await Assert.ThrowsAnyAsync<Exception>(() =>
            s.InitiateExpensePaymentAsync(outsider.Id, req.Id, TargetedCorrectnessFixture.SecurityPin, default));

        // 2. Unrelated Member C calls initiate-payment on Member B's request -> Throws ForbiddenException
        var exPerm = await Assert.ThrowsAsync<ForbiddenException>(() =>
            s.InitiateExpensePaymentAsync(memberC.Id, req.Id, TargetedCorrectnessFixture.SecurityPin, default));
        Assert.Equal("fund_request_permission_required", exPerm.Code);

        // Assert: NO expiration activity was created with outsider or Member C as actor!
        var maliciousActivities = await f.Db.V1FundActivities
            .Where(a => a.Event == "FundRequestExpired" && (a.ActorUserId == memberC.Id || a.ActorUserId == outsider.Id))
            .ToListAsync();
        Assert.Empty(maliciousActivities);
    }

    [Fact]
    public async Task FreeFund_AtomicExpirationSweep_ConcurrentWorkers_CreatesExactlyOneActivity()
    {
        var clock = new MutableClock(new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc));
        await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);
        var owner = await f.AddUserAsync("OwnerA");
        var member = await f.AddUserAsync("MemberB");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);

        var s = V1FundMembershipTests.Service(f);
        var fund = await s.CreateAsync(owner.Id, new("AtomicSweepFund", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);

        var invite = await s.InviteAsync(owner.Id, fund.Id, member.Id, default);
        await s.AcceptAsync(member.Id, invite.Id, default);

        var ownerWallet = await f.AddWalletAsync(owner.Id, "OwnerWallet", true, balance: 1_000_000);
        await s.ContributeAsync(owner.Id, fund.Id,
            new(ownerWallet.Id, 500_000, "c-owner", TargetedCorrectnessFixture.SecurityPin), default);

        var req = await s.CreateFundRequestAsync(member.Id, fund.Id,
            new("Expense", 100_000, member.Id, "Supplies", "exp-atomic", TargetedCorrectnessFixture.SecurityPin), default);
        await s.ReviewFundRequestAsync(owner.Id, req.Id, true, "Approved", TargetedCorrectnessFixture.SecurityPin, default);

        // Fast forward 7 days + 1 min
        clock.UtcNow = clock.UtcNow.AddDays(7).AddMinutes(1);

        // Concurrently run 5 sweep workers for the same fund
        var tasks = Enumerable.Range(0, 5).Select(_ => s.ExpireStaleApprovedUnusedRequestsAsync(fund.Id, default)).ToArray();
        var results = await Task.WhenAll(tasks);

        // Exactly one worker claims the transition (sum of counts is 1)
        var totalExpiredCount = results.Sum();
        Assert.Equal(1, totalExpiredCount);

        // Verify database state: State is Expired
        var dbItem = await f.Db.V1FundRequests.AsNoTracking().FirstAsync(r => r.Id == req.Id);
        Assert.Equal("Expired", dbItem.State);
        Assert.NotNull(dbItem.CancelledAt);

        // Verify EXACTLY ONE activity was created, and ActorUserId is null (system event)
        var activities = await f.Db.V1FundActivities
            .Where(a => a.FundId == fund.Id && a.Event == "FundRequestExpired")
            .ToListAsync();
        Assert.Single(activities);
        Assert.Null(activities.Single().ActorUserId);
    }
}
