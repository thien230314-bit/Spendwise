using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
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
    public async Task FreeFund_Member_CannotCreateTwoActiveExpensesConcurrently()
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

        // Expense 2 fails with limit exceeded
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            s.CreateFundRequestAsync(member.Id, fund.Id,
                new("Expense", 30_000, member.Id, "Exp 2", "exp-2", TargetedCorrectnessFixture.SecurityPin), default));
        Assert.Equal("fund_request_limit_exceeded", ex.Code);
    }

    [Fact]
    public async Task FreeFund_Member_CannotCreateTwoActiveWithdrawalsConcurrently()
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

        // Withdrawal 2 fails with limit exceeded
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            s.CreateFundRequestAsync(member.Id, fund.Id,
                new("Withdrawal", 30_000, member.Id, "Wth 2", "wth-2", TargetedCorrectnessFixture.SecurityPin), default));
        Assert.Equal("fund_request_limit_exceeded", ex.Code);
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
    public async Task FreeFund_ApprovedUnused_ExpiresAfterSevenDays()
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
        var reqsDay1 = await s.FundRequestsAsync(member.Id, fund.Id, 1, 10, default);
        Assert.Equal("ApprovedUnused", reqsDay1.Single().State);

        // Fast forward 7 days + 1 minute
        clock.UtcNow = clock.UtcNow.AddDays(7).AddMinutes(1);

        // State is now Expired
        var reqsDay8 = await s.FundRequestsAsync(member.Id, fund.Id, 1, 10, default);
        Assert.Equal("Expired", reqsDay8.Single().State);

        // Initiating payment on expired request fails
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            s.InitiateExpensePaymentAsync(member.Id, req.Id, TargetedCorrectnessFixture.SecurityPin, default));
        Assert.Equal("fund_request_expired", ex.Code);
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
        Assert.Equal("insufficient_fund_balance", ex.Code);

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
    public async Task FreeFund_InitiatePayment_TransitionsToPaymentProcessing_AndBlocksCancellation()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("OwnerA");
        var member = await f.AddUserAsync("MemberB");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);

        var s = V1FundMembershipTests.Service(f);
        var fund = await s.CreateAsync(owner.Id, new("PaymentProcFund", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);

        var invite = await s.InviteAsync(owner.Id, fund.Id, member.Id, default);
        await s.AcceptAsync(member.Id, invite.Id, default);

        var ownerWallet = await f.AddWalletAsync(owner.Id, "OwnerWallet", true, balance: 1_000_000);
        await s.ContributeAsync(owner.Id, fund.Id,
            new(ownerWallet.Id, 500_000, "c-owner", TargetedCorrectnessFixture.SecurityPin), default);

        var reqExp = await s.CreateFundRequestAsync(member.Id, fund.Id,
            new("Expense", 100_000, member.Id, "Internet bill", "exp-net", TargetedCorrectnessFixture.SecurityPin), default);
        await s.ReviewFundRequestAsync(owner.Id, reqExp.Id, true, "Approved", TargetedCorrectnessFixture.SecurityPin, default);

        // Initiate payment -> PaymentProcessing
        var paying = await s.InitiateExpensePaymentAsync(member.Id, reqExp.Id, TargetedCorrectnessFixture.SecurityPin, default);
        Assert.Equal("PaymentProcessing", paying.State);
        Assert.Equal("PaymentProcessing", paying.ExecutionAvailability);

        // Cancellation is blocked while in PaymentProcessing
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            s.CancelFundRequestAsync(member.Id, reqExp.Id, TargetedCorrectnessFixture.SecurityPin, default));
        Assert.Equal("fund_payment_processing", ex.Code);
    }
}
