using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Features.V1Groups;
using SpendWise.Domain.Admin;
using SpendWise.Domain.Finance;
using SpendWise.Domain.V1Groups;
using Xunit;

namespace SpendWise.Tests;

public sealed class FixedFundV2BusinessRuleTests
{
    private const string Pin = TargetedCorrectnessFixture.SecurityPin;

    [Fact]
    public async Task F01_CannotSetCanSpend_BeforeObligationsConfirmed()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Owner");
        var member = await f.AddUserAsync("Member");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);
        var service = V1ElectionTests.Configured(f);
        var fund = await service.CreateAsync(owner.Id, new("FundF01", "Fixed", 200, Pin), default);
        var inv = await service.InviteAsync(owner.Id, fund.Id, member.Id, default);
        await service.AcceptAsync(member.Id, inv.Id, default);

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            service.SetSpendingPermissionAsync(owner.Id, fund.Id, member.Id, true, Pin, default));
        Assert.Equal("treasury_not_found", ex.Code);
    }

    [Fact]
    public async Task F02_F03_CannotSetCanSpend_WhenContributionIsIncomplete()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Owner");
        var member = await f.AddUserAsync("Member");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);
        var service = V1ElectionTests.Configured(f);
        var fund = await service.CreateAsync(owner.Id, new("FundF02", "Fixed", 200, Pin), default);
        var inv = await service.InviteAsync(owner.Id, fund.Id, member.Id, default);
        await service.AcceptAsync(member.Id, inv.Id, default);
        var config = await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(200, Pin), default);
        var mWallet = await f.AddWalletAsync(member.Id, "MWallet", true, balance: 500);

        // 0 contribution
        var ex0 = await Assert.ThrowsAsync<ConflictException>(() =>
            service.SetSpendingPermissionAsync(owner.Id, fund.Id, member.Id, true, Pin, default));
        Assert.Equal("fixed_member_contribution_incomplete", ex0.Code);

        // Partial contribution (50 / 100)
        await service.ContributeAsync(member.Id, fund.Id, new(mWallet.Id, 50, "c-p", Pin, config.Revision), default);
        var exPart = await Assert.ThrowsAsync<ConflictException>(() =>
            service.SetSpendingPermissionAsync(owner.Id, fund.Id, member.Id, true, Pin, default));
        Assert.Equal("fixed_member_contribution_incomplete", exPart.Code);
    }

    [Fact]
    public async Task F04_F05_CanSetCanSpend_When100PercentContributed_EffectiveCanSpendReflectsGroupFunding()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Owner");
        var member = await f.AddUserAsync("Member");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);
        var service = V1ElectionTests.Configured(f);
        var fund = await service.CreateAsync(owner.Id, new("FundF04", "Fixed", 200, Pin), default);
        var inv = await service.InviteAsync(owner.Id, fund.Id, member.Id, default);
        await service.AcceptAsync(member.Id, inv.Id, default);
        var config = await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(200, Pin), default);
        var mWallet = await f.AddWalletAsync(member.Id, "MWallet", true, balance: 500);

        await service.ContributeAsync(member.Id, fund.Id, new(mWallet.Id, 100, "c-full", Pin, config.Revision), default);
        await service.SetSpendingPermissionAsync(owner.Id, fund.Id, member.Id, true, Pin, default);

        var members = await service.MembersAsync(owner.Id, fund.Id, default);
        var mDto = Assert.Single(members, m => m.UserId == member.Id);
        Assert.True(mDto.CanSpend); // Stored
        Assert.True(mDto.IndividualContributionCompleted);
        Assert.False(mDto.GroupFundingCompleted); // Only 100/200
        Assert.False(mDto.EffectiveCanSpend); // False because group is not 100%
    }

    [Fact]
    public async Task F06_F07_CannotSpend_WhenGroupFundingIncomplete_EvenWithCanSpendOn()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Owner");
        var member = await f.AddUserAsync("Member");
        var merchant = await f.AddUserAsync("Merchant");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);
        var service = V1ElectionTests.Configured(f);
        var fund = await service.CreateAsync(owner.Id, new("FundF06", "Fixed", 200, Pin), default);
        var inv = await service.InviteAsync(owner.Id, fund.Id, member.Id, default);
        await service.AcceptAsync(member.Id, inv.Id, default);
        var config = await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(200, Pin), default);
        var mWallet = await f.AddWalletAsync(member.Id, "MWallet", true, balance: 500);
        await f.AddWalletAsync(merchant.Id, "MerchWallet", true, balance: 0);

        await service.ContributeAsync(member.Id, fund.Id, new(mWallet.Id, 100, "c-full", Pin, config.Revision), default);
        await service.SetSpendingPermissionAsync(owner.Id, fund.Id, member.Id, true, Pin, default);

        var exp = new V1FundRequestInput("Expense", 30, merchant.Id, "Premature expense", "exp-pre", Pin);
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            service.CreateFundRequestAsync(member.Id, fund.Id, exp, default));
        Assert.Equal("fixed_group_funding_incomplete", ex.Code);
    }

    [Fact]
    public async Task F08_F09_F10_OwnerMustContribute100Percent_AndGroupMustBe100Percent_BeforeOwnerSpends()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Owner");
        var member = await f.AddUserAsync("Member");
        var merchant = await f.AddUserAsync("Merchant");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);
        var service = V1ElectionTests.Configured(f);
        var fund = await service.CreateAsync(owner.Id, new("FundF08", "Fixed", 200, Pin), default);
        var inv = await service.InviteAsync(owner.Id, fund.Id, member.Id, default);
        await service.AcceptAsync(member.Id, inv.Id, default);
        var config = await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(200, Pin), default);
        var oWallet = await f.AddWalletAsync(owner.Id, "OWallet", true, balance: 500);
        var mWallet = await f.AddWalletAsync(member.Id, "MWallet", true, balance: 500);
        await f.AddWalletAsync(merchant.Id, "MerchWallet", true, balance: 0);

        var exp = new V1FundRequestInput("Expense", 40, merchant.Id, "Owner expense", "exp-own", Pin);

        // Owner 0 contribution -> throws fixed_member_contribution_incomplete
        var exInd = await Assert.ThrowsAsync<ConflictException>(() =>
            service.CreateFundRequestAsync(owner.Id, fund.Id, exp, default));
        Assert.Equal("fixed_member_contribution_incomplete", exInd.Code);

        // Owner contributes 100, but member hasn't contributed -> group is only 100/200
        await service.ContributeAsync(owner.Id, fund.Id, new(oWallet.Id, 100, "c-own", Pin, config.Revision), default);
        var exGroup = await Assert.ThrowsAsync<ConflictException>(() =>
            service.CreateFundRequestAsync(owner.Id, fund.Id, exp, default));
        Assert.Equal("fixed_group_funding_incomplete", exGroup.Code);

        // Member contributes 100 -> group is 200/200 (100% funded)
        await service.ContributeAsync(member.Id, fund.Id, new(mWallet.Id, 100, "c-mem", Pin, config.Revision), default);

        // Owner spends directly -> succeeds!
        var res = await service.CreateFundRequestAsync(owner.Id, fund.Id, exp, default);
        Assert.Equal("Completed", res.State);
        Assert.Equal("Executed", res.ExecutionAvailability);
    }

    [Fact]
    public async Task F11_F12_TargetIncrease_RetainsStoredCanSpend_EffectiveCanSpendRestoresOnCatchup()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Owner");
        var member = await f.AddUserAsync("Member");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);
        var service = V1ElectionTests.Configured(f);
        var fund = await service.CreateAsync(owner.Id, new("FundF11", "Fixed", 200, Pin), default);
        var inv = await service.InviteAsync(owner.Id, fund.Id, member.Id, default);
        await service.AcceptAsync(member.Id, inv.Id, default);
        var config1 = await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(200, Pin), default);
        var oWallet = await f.AddWalletAsync(owner.Id, "OWallet", true, balance: 1000);
        var mWallet = await f.AddWalletAsync(member.Id, "MWallet", true, balance: 1000);

        await service.ContributeAsync(owner.Id, fund.Id, new(oWallet.Id, 100, "c-o1", Pin, config1.Revision), default);
        await service.ContributeAsync(member.Id, fund.Id, new(mWallet.Id, 100, "c-m1", Pin, config1.Revision), default);
        await service.SetSpendingPermissionAsync(owner.Id, fund.Id, member.Id, true, Pin, default);

        // Initial state: EffectiveCanSpend is True
        var mems1 = await service.MembersAsync(owner.Id, fund.Id, default);
        Assert.True(Assert.Single(mems1, m => m.UserId == member.Id).EffectiveCanSpend);

        // Increase target: 200 -> 400 (each needs 200)
        var config2 = await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(400, Pin), default);

        // Stored CanSpend is still True, but EffectiveCanSpend becomes False
        var mems2 = await service.MembersAsync(owner.Id, fund.Id, default);
        var mDto2 = Assert.Single(mems2, m => m.UserId == member.Id);
        Assert.True(mDto2.CanSpend); // Stored persists
        Assert.False(mDto2.IndividualContributionCompleted); // 100 / 200
        Assert.False(mDto2.EffectiveCanSpend);

        // Catchup: Member contributes remaining 100, Owner contributes remaining 100
        await service.ContributeAsync(member.Id, fund.Id, new(mWallet.Id, 100, "c-m2", Pin, config2.Revision), default);
        await service.ContributeAsync(owner.Id, fund.Id, new(oWallet.Id, 100, "c-o2", Pin, config2.Revision), default);

        // EffectiveCanSpend automatically restores to True!
        var mems3 = await service.MembersAsync(owner.Id, fund.Id, default);
        var mDto3 = Assert.Single(mems3, m => m.UserId == member.Id);
        Assert.True(mDto3.CanSpend);
        Assert.True(mDto3.IndividualContributionCompleted);
        Assert.True(mDto3.GroupFundingCompleted);
        Assert.True(mDto3.EffectiveCanSpend);
    }

    [Fact]
    public async Task F15_F16_SpendingAfter100PercentFunding_DoesNotCloseFundingGate_AndNo20PercentThreshold()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Owner");
        var member = await f.AddUserAsync("Member");
        var merchant = await f.AddUserAsync("Merchant");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);
        var service = V1ElectionTests.Configured(f);
        var fund = await service.CreateAsync(owner.Id, new("FundF15", "Fixed", 200, Pin), default);
        var inv = await service.InviteAsync(owner.Id, fund.Id, member.Id, default);
        await service.AcceptAsync(member.Id, inv.Id, default);
        var config = await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(200, Pin), default);
        var oWallet = await f.AddWalletAsync(owner.Id, "OWallet", true, balance: 1000);
        var mWallet = await f.AddWalletAsync(member.Id, "MWallet", true, balance: 1000);
        await f.AddWalletAsync(merchant.Id, "MerchWallet", true, balance: 0);

        await service.ContributeAsync(owner.Id, fund.Id, new(oWallet.Id, 100, "c-o", Pin, config.Revision), default);
        await service.ContributeAsync(member.Id, fund.Id, new(mWallet.Id, 100, "c-m", Pin, config.Revision), default);
        await service.SetSpendingPermissionAsync(owner.Id, fund.Id, member.Id, true, Pin, default);

        // Spend 80 VND (40% of target, > 20%): Completes immediately without Pending24h
        var exp1 = new V1FundRequestInput("Expense", 80, merchant.Id, "First expense 40%", "exp-1", Pin);
        var res1 = await service.CreateFundRequestAsync(member.Id, fund.Id, exp1, default);
        Assert.Equal("Completed", res1.State);

        // Treasury balance is now 120 < 200 target. Group funding is STILL 100% because total paid = 200 >= 200.
        // Subsequent spend of 50 VND succeeds immediately without re-blocking funding!
        var exp2 = new V1FundRequestInput("Expense", 50, merchant.Id, "Second expense", "exp-2", Pin);
        var res2 = await service.CreateFundRequestAsync(member.Id, fund.Id, exp2, default);
        Assert.Equal("Completed", res2.State);
    }

    [Fact]
    public async Task F17_FixedOwnerReview_IsExplicitlyDisallowed()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Owner");
        var member = await f.AddUserAsync("Member");
        var merchant = await f.AddUserAsync("Merchant");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);
        var service = V1ElectionTests.Configured(f);
        var fund = await service.CreateAsync(owner.Id, new("FundF17", "Fixed", 200, Pin), default);
        var inv = await service.InviteAsync(owner.Id, fund.Id, member.Id, default);
        await service.AcceptAsync(member.Id, inv.Id, default);
        var config = await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(200, Pin), default);
        var oWallet = await f.AddWalletAsync(owner.Id, "OWallet", true, balance: 1000);
        var mWallet = await f.AddWalletAsync(member.Id, "MWallet", true, balance: 1000);
        await f.AddWalletAsync(merchant.Id, "MerchWallet", true, balance: 0);

        await service.ContributeAsync(owner.Id, fund.Id, new(oWallet.Id, 100, "c-o", Pin, config.Revision), default);
        await service.ContributeAsync(member.Id, fund.Id, new(mWallet.Id, 100, "c-m", Pin, config.Revision), default);

        var exp = new V1FundRequestInput("Expense", 50, merchant.Id, "Direct expense", "exp-fixed", Pin);
        var res = await service.CreateFundRequestAsync(owner.Id, fund.Id, exp, default);

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            service.ReviewFundRequestAsync(owner.Id, res.Id, true, "Cannot review", Pin, default));
        Assert.Equal("fixed_owner_review_disallowed", ex.Code);
    }

    [Fact]
    public async Task F18_F19_FreeFund_WithdrawalAndOwnerReview_Remains100PercentIntact()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Owner");
        var member = await f.AddUserAsync("Member");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);
        var service = V1ElectionTests.Configured(f);
        var fund = await service.CreateAsync(owner.Id, new("FundFree", "Free", null, Pin), default);
        var inv = await service.InviteAsync(owner.Id, fund.Id, member.Id, default);
        await service.AcceptAsync(member.Id, inv.Id, default);
        var oWallet = await f.AddWalletAsync(owner.Id, "OWallet", true, balance: 1000);
        var mWallet = await f.AddWalletAsync(member.Id, "MWallet", true, balance: 100);

        // Owner contributes 300 to Free fund
        await service.ContributeAsync(owner.Id, fund.Id, new(oWallet.Id, 300, "c-free", Pin), default);

        // Member requests withdrawal -> AwaitingOwnerReview
        var wd = new V1FundRequestInput("Withdrawal", 100, member.Id, "Reimbursement", "wd-free", Pin);
        var req = await service.CreateFundRequestAsync(member.Id, fund.Id, wd, default);
        Assert.Equal("AwaitingOwnerReview", req.State);

        // Owner approves -> Completed
        var reviewed = await service.ReviewFundRequestAsync(owner.Id, req.Id, true, "Approved", Pin, default);
        Assert.Equal("Completed", reviewed.State);

        f.Db.ChangeTracker.Clear();
        var mw = await f.Db.Wallets.SingleAsync(w => w.Id == mWallet.Id);
        Assert.Equal(200, mw.AvailableBalance); // 100 + 100
    }

    [Fact]
    public async Task F20_LockedExcess_IsExcludedFromSpendableBalance()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Owner");
        var member = await f.AddUserAsync("Member");
        var merchant = await f.AddUserAsync("Merchant");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);
        var service = V1ElectionTests.Configured(f);
        var fund = await service.CreateAsync(owner.Id, new("FundExcess", "Fixed", 400, Pin), default);
        var inv = await service.InviteAsync(owner.Id, fund.Id, member.Id, default);
        await service.AcceptAsync(member.Id, inv.Id, default);

        // Revision 1: Target 400 -> each owes 200
        var config1 = await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(400, Pin), default);
        var oWallet = await f.AddWalletAsync(owner.Id, "OWallet", true, balance: 1000);
        var mWallet = await f.AddWalletAsync(member.Id, "MWallet", true, balance: 1000);
        await f.AddWalletAsync(merchant.Id, "MerchWallet", true, balance: 0);

        // Both contribute 200
        await service.ContributeAsync(owner.Id, fund.Id, new(oWallet.Id, 200, "c-o", Pin, config1.Revision), default);
        await service.ContributeAsync(member.Id, fund.Id, new(mWallet.Id, 200, "c-m", Pin, config1.Revision), default);

        // Revision 2: Target reduced to 200 -> each owes 100, so each has 100 LockedExcess (Total Locked Excess = 200)
        await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(200, Pin), default);

        // Treasury balance is 400 VND. Total Locked Excess is 200 VND. Spendable balance = 400 - 200 = 200 VND.
        // Attempting to spend 220 VND (less than treasury balance 400, but greater than spendable 200) throws insufficient_fund_balance
        var expOver = new V1FundRequestInput("Expense", 220, merchant.Id, "Over spendable", "exp-over", Pin);
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            service.CreateFundRequestAsync(owner.Id, fund.Id, expOver, default));
        Assert.Equal("insufficient_fund_balance", ex.Code);

        // Spending 200 VND succeeds exactly
        var expValid = new V1FundRequestInput("Expense", 200, merchant.Id, "Exact spendable", "exp-exact", Pin);
        var res = await service.CreateFundRequestAsync(owner.Id, fund.Id, expValid, default);
        Assert.Equal("Completed", res.State);
    }

    [Fact]
    public async Task F28_CompletedExpenseReporting_CreatesSupportCase_WithoutReversingMoney()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Owner");
        var member = await f.AddUserAsync("Member");
        var merchant = await f.AddUserAsync("Merchant");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);
        var service = V1ElectionTests.Configured(f);
        var fund = await service.CreateAsync(owner.Id, new("FundReport", "Fixed", 200, Pin), default);
        var inv = await service.InviteAsync(owner.Id, fund.Id, member.Id, default);
        await service.AcceptAsync(member.Id, inv.Id, default);
        var config = await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(200, Pin), default);
        var oWallet = await f.AddWalletAsync(owner.Id, "OWallet", true, balance: 1000);
        var mWallet = await f.AddWalletAsync(member.Id, "MWallet", true, balance: 1000);
        var merchWallet = await f.AddWalletAsync(merchant.Id, "MerchWallet", true, balance: 0);

        await service.ContributeAsync(owner.Id, fund.Id, new(oWallet.Id, 100, "c-o", Pin, config.Revision), default);
        await service.ContributeAsync(member.Id, fund.Id, new(mWallet.Id, 100, "c-m", Pin, config.Revision), default);

        var exp = new V1FundRequestInput("Expense", 50, merchant.Id, "Valid expense", "exp-rep", Pin);
        var res = await service.CreateFundRequestAsync(owner.Id, fund.Id, exp, default);
        Assert.Equal("Completed", res.State);

        // Member reports this completed expense
        var reported = await service.ReportFundRequestAsync(member.Id, res.Id, "Suspicious item reported", default);
        Assert.Equal("Completed", reported.State); // State remains Completed, no automatic reversal!

        f.Db.ChangeTracker.Clear();
        var mw = await f.Db.Wallets.SingleAsync(w => w.Id == merchWallet.Id);
        Assert.Equal(50, mw.AvailableBalance); // Money untouched!

        var supportCase = await f.Db.SupportCases.SingleAsync(c => c.LinkedTransactionId == res.Id);
        Assert.Equal("V1FundDispute", supportCase.Category);
        Assert.Equal(member.Id, supportCase.LinkedUserId);
        Assert.Equal(CaseStatus.New, supportCase.Status);
    }
}
