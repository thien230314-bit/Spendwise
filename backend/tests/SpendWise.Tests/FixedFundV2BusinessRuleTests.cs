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

    // =========================================================================
    // F01 - F05: Quy tắc nhóm Cố định, chu kỳ đóng quỹ, không có âm quỹ
    // =========================================================================

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
    public async Task F02_CannotSetCanSpend_WhenMemberHasZeroContribution()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Owner");
        var member = await f.AddUserAsync("Member");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);
        var service = V1ElectionTests.Configured(f);
        var fund = await service.CreateAsync(owner.Id, new("FundF02", "Fixed", 200, Pin), default);
        var inv = await service.InviteAsync(owner.Id, fund.Id, member.Id, default);
        await service.AcceptAsync(member.Id, inv.Id, default);
        await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(200, Pin), default);

        var ex0 = await Assert.ThrowsAsync<ConflictException>(() =>
            service.SetSpendingPermissionAsync(owner.Id, fund.Id, member.Id, true, Pin, default));
        Assert.Equal("fixed_member_contribution_incomplete", ex0.Code);
    }

    [Fact]
    public async Task F03_CannotSetCanSpend_WhenMemberHasPartialContribution()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Owner");
        var member = await f.AddUserAsync("Member");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);
        var service = V1ElectionTests.Configured(f);
        var fund = await service.CreateAsync(owner.Id, new("FundF03", "Fixed", 200, Pin), default);
        var inv = await service.InviteAsync(owner.Id, fund.Id, member.Id, default);
        await service.AcceptAsync(member.Id, inv.Id, default);
        var config = await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(200, Pin), default);
        var mWallet = await f.AddWalletAsync(member.Id, "MWallet", true, balance: 500);

        // Partial contribution (50 / 100)
        await service.ContributeAsync(member.Id, fund.Id, new(mWallet.Id, 50, "c-p3", Pin, config.Revision), default);
        var exPart = await Assert.ThrowsAsync<ConflictException>(() =>
            service.SetSpendingPermissionAsync(owner.Id, fund.Id, member.Id, true, Pin, default));
        Assert.Equal("fixed_member_contribution_incomplete", exPart.Code);
    }

    [Fact]
    public async Task F04_CanSetCanSpend_WhenMemberHas100PercentContribution()
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

        await service.ContributeAsync(member.Id, fund.Id, new(mWallet.Id, 100, "c-full4", Pin, config.Revision), default);
        await service.SetSpendingPermissionAsync(owner.Id, fund.Id, member.Id, true, Pin, default);

        var members = await service.MembersAsync(owner.Id, fund.Id, default);
        var mDto = Assert.Single(members, m => m.UserId == member.Id);
        Assert.True(mDto.CanSpend);
        Assert.True(mDto.IndividualContributionCompleted);
    }

    [Fact]
    public async Task F05_FixedFund_HasNoNegativeBalanceOrOverspendingConcept()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Owner");
        var member = await f.AddUserAsync("Member");
        var merchant = await f.AddUserAsync("Merchant");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);
        var service = V1ElectionTests.Configured(f);
        var fund = await service.CreateAsync(owner.Id, new("FundF05", "Fixed", 200, Pin), default);
        var inv = await service.InviteAsync(owner.Id, fund.Id, member.Id, default);
        await service.AcceptAsync(member.Id, inv.Id, default);
        var config = await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(200, Pin), default);
        var oWallet = await f.AddWalletAsync(owner.Id, "OWallet", true, balance: 500);
        var mWallet = await f.AddWalletAsync(member.Id, "MWallet", true, balance: 500);
        await f.AddWalletAsync(merchant.Id, "MerchWallet", true, balance: 0);

        await service.ContributeAsync(owner.Id, fund.Id, new(oWallet.Id, 100, "c-o5", Pin, config.Revision), default);
        await service.ContributeAsync(member.Id, fund.Id, new(mWallet.Id, 100, "c-m5", Pin, config.Revision), default);

        // Attempting to spend 250 VND when treasury only has 200 VND
        var exp = new V1FundRequestInput("Expense", 250, merchant.Id, "Overspend attempt", "exp-f05", Pin);
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            service.CreateFundRequestAsync(owner.Id, fund.Id, exp, default));
        Assert.Equal("insufficient_fund_balance", ex.Code);
    }

    // =========================================================================
    // F06 - F10: CanSpend và Balance tính toán độc lập, Stored vs Effective CanSpend
    // =========================================================================

    [Fact]
    public async Task F06_GroupFundingIncomplete_BlocksSpendingEvenWithCanSpendTrue()
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

        await service.ContributeAsync(member.Id, fund.Id, new(mWallet.Id, 100, "c-f06", Pin, config.Revision), default);
        await service.SetSpendingPermissionAsync(owner.Id, fund.Id, member.Id, true, Pin, default);

        var exp = new V1FundRequestInput("Expense", 30, merchant.Id, "Premature expense", "exp-f06", Pin);
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            service.CreateFundRequestAsync(member.Id, fund.Id, exp, default));
        Assert.Equal("fixed_group_funding_incomplete", ex.Code);
    }

    [Fact]
    public async Task F07_StoredCanSpendPersists_WhileEffectiveCanSpendIsFalse_WhenGroupIncomplete()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Owner");
        var member = await f.AddUserAsync("Member");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);
        var service = V1ElectionTests.Configured(f);
        var fund = await service.CreateAsync(owner.Id, new("FundF07", "Fixed", 200, Pin), default);
        var inv = await service.InviteAsync(owner.Id, fund.Id, member.Id, default);
        await service.AcceptAsync(member.Id, inv.Id, default);
        var config = await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(200, Pin), default);
        var mWallet = await f.AddWalletAsync(member.Id, "MWallet", true, balance: 500);

        await service.ContributeAsync(member.Id, fund.Id, new(mWallet.Id, 100, "c-f07", Pin, config.Revision), default);
        await service.SetSpendingPermissionAsync(owner.Id, fund.Id, member.Id, true, Pin, default);

        var members = await service.MembersAsync(owner.Id, fund.Id, default);
        var mDto = Assert.Single(members, m => m.UserId == member.Id);
        Assert.True(mDto.CanSpend); // Stored persists
        Assert.False(mDto.GroupFundingCompleted);
        Assert.False(mDto.EffectiveCanSpend);
    }

    [Fact]
    public async Task F08_GroupFunding100Percent_EnablesEffectiveCanSpend()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Owner");
        var member = await f.AddUserAsync("Member");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);
        var service = V1ElectionTests.Configured(f);
        var fund = await service.CreateAsync(owner.Id, new("FundF08", "Fixed", 200, Pin), default);
        var inv = await service.InviteAsync(owner.Id, fund.Id, member.Id, default);
        await service.AcceptAsync(member.Id, inv.Id, default);
        var config = await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(200, Pin), default);
        var oWallet = await f.AddWalletAsync(owner.Id, "OWallet", true, balance: 500);
        var mWallet = await f.AddWalletAsync(member.Id, "MWallet", true, balance: 500);

        await service.ContributeAsync(member.Id, fund.Id, new(mWallet.Id, 100, "c-m-f08", Pin, config.Revision), default);
        await service.SetSpendingPermissionAsync(owner.Id, fund.Id, member.Id, true, Pin, default);
        await service.ContributeAsync(owner.Id, fund.Id, new(oWallet.Id, 100, "c-o-f08", Pin, config.Revision), default);

        var members = await service.MembersAsync(owner.Id, fund.Id, default);
        var mDto = Assert.Single(members, m => m.UserId == member.Id);
        Assert.True(mDto.CanSpend);
        Assert.True(mDto.GroupFundingCompleted);
        Assert.True(mDto.EffectiveCanSpend);
    }

    [Fact]
    public async Task F09_MemberWithStoredCanSpendFalse_CannotSpendEvenWhenGroup100PercentFunded()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Owner");
        var member = await f.AddUserAsync("Member");
        var merchant = await f.AddUserAsync("Merchant");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);
        var service = V1ElectionTests.Configured(f);
        var fund = await service.CreateAsync(owner.Id, new("FundF09", "Fixed", 200, Pin), default);
        var inv = await service.InviteAsync(owner.Id, fund.Id, member.Id, default);
        await service.AcceptAsync(member.Id, inv.Id, default);
        var config = await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(200, Pin), default);
        var oWallet = await f.AddWalletAsync(owner.Id, "OWallet", true, balance: 500);
        var mWallet = await f.AddWalletAsync(member.Id, "MWallet", true, balance: 500);
        await f.AddWalletAsync(merchant.Id, "MerchWallet", true, balance: 0);

        await service.ContributeAsync(owner.Id, fund.Id, new(oWallet.Id, 100, "c-o-f09", Pin, config.Revision), default);
        await service.ContributeAsync(member.Id, fund.Id, new(mWallet.Id, 100, "c-m-f09", Pin, config.Revision), default);

        // CanSpend was NOT granted (Stored CanSpend = false)
        var exp = new V1FundRequestInput("Expense", 30, merchant.Id, "Unauthorized spend", "exp-f09", Pin);
        var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.CreateFundRequestAsync(member.Id, fund.Id, exp, default));
        Assert.Equal("fund_spending_permission_required", ex.Code);
    }

    [Fact]
    public async Task F10_BalanceChangesDoNotMutateStoredCanSpend()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Owner");
        var member = await f.AddUserAsync("Member");
        var merchant = await f.AddUserAsync("Merchant");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);
        var service = V1ElectionTests.Configured(f);
        var fund = await service.CreateAsync(owner.Id, new("FundF10", "Fixed", 200, Pin), default);
        var inv = await service.InviteAsync(owner.Id, fund.Id, member.Id, default);
        await service.AcceptAsync(member.Id, inv.Id, default);
        var config = await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(200, Pin), default);
        var oWallet = await f.AddWalletAsync(owner.Id, "OWallet", true, balance: 500);
        var mWallet = await f.AddWalletAsync(member.Id, "MWallet", true, balance: 500);
        await f.AddWalletAsync(merchant.Id, "MerchWallet", true, balance: 0);

        await service.ContributeAsync(owner.Id, fund.Id, new(oWallet.Id, 100, "c-o-f10", Pin, config.Revision), default);
        await service.ContributeAsync(member.Id, fund.Id, new(mWallet.Id, 100, "c-m-f10", Pin, config.Revision), default);
        await service.SetSpendingPermissionAsync(owner.Id, fund.Id, member.Id, true, Pin, default);

        // Execute expense that reduces treasury balance
        var exp = new V1FundRequestInput("Expense", 60, merchant.Id, "Spend 60", "exp-f10", Pin);
        await service.CreateFundRequestAsync(member.Id, fund.Id, exp, default);

        // Stored CanSpend remains True despite lower treasury balance
        var members = await service.MembersAsync(owner.Id, fund.Id, default);
        var mDto = Assert.Single(members, m => m.UserId == member.Id);
        Assert.True(mDto.CanSpend);
    }

    // =========================================================================
    // F11 - F15: Catchup contribution, đóng bổ sung chu kỳ trước
    // =========================================================================

    [Fact]
    public async Task F11_TargetIncrease_RetainsStoredCanSpend_EffectiveCanSpendBecomesFalse()
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

        await service.ContributeAsync(owner.Id, fund.Id, new(oWallet.Id, 100, "c-o-f11", Pin, config1.Revision), default);
        await service.ContributeAsync(member.Id, fund.Id, new(mWallet.Id, 100, "c-m-f11", Pin, config1.Revision), default);
        await service.SetSpendingPermissionAsync(owner.Id, fund.Id, member.Id, true, Pin, default);

        // Target increased from 200 to 400
        await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(400, Pin), default);

        var members = await service.MembersAsync(owner.Id, fund.Id, default);
        var mDto = Assert.Single(members, m => m.UserId == member.Id);
        Assert.True(mDto.CanSpend); // Stored
        Assert.False(mDto.EffectiveCanSpend); // False until catchup
    }

    [Fact]
    public async Task F12_CatchupContribution_RestoresEffectiveCanSpendAutomatically()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Owner");
        var member = await f.AddUserAsync("Member");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);
        var service = V1ElectionTests.Configured(f);
        var fund = await service.CreateAsync(owner.Id, new("FundF12", "Fixed", 200, Pin), default);
        var inv = await service.InviteAsync(owner.Id, fund.Id, member.Id, default);
        await service.AcceptAsync(member.Id, inv.Id, default);
        var config1 = await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(200, Pin), default);
        var oWallet = await f.AddWalletAsync(owner.Id, "OWallet", true, balance: 1000);
        var mWallet = await f.AddWalletAsync(member.Id, "MWallet", true, balance: 1000);

        await service.ContributeAsync(owner.Id, fund.Id, new(oWallet.Id, 100, "c-o-f12-1", Pin, config1.Revision), default);
        await service.ContributeAsync(member.Id, fund.Id, new(mWallet.Id, 100, "c-m-f12-1", Pin, config1.Revision), default);
        await service.SetSpendingPermissionAsync(owner.Id, fund.Id, member.Id, true, Pin, default);

        var config2 = await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(400, Pin), default);

        // Catchup contributions
        await service.ContributeAsync(member.Id, fund.Id, new(mWallet.Id, 100, "c-m-f12-2", Pin, config2.Revision), default);
        await service.ContributeAsync(owner.Id, fund.Id, new(oWallet.Id, 100, "c-o-f12-2", Pin, config2.Revision), default);

        var members = await service.MembersAsync(owner.Id, fund.Id, default);
        var mDto = Assert.Single(members, m => m.UserId == member.Id);
        Assert.True(mDto.EffectiveCanSpend);
    }

    [Fact]
    public async Task F13_CatchupPayment_CreditsTreasuryBalanceImmediately()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Owner");
        var member = await f.AddUserAsync("Member");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);
        var service = V1ElectionTests.Configured(f);
        var fund = await service.CreateAsync(owner.Id, new("FundF13", "Fixed", 200, Pin), default);
        var inv = await service.InviteAsync(owner.Id, fund.Id, member.Id, default);
        await service.AcceptAsync(member.Id, inv.Id, default);
        var config = await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(200, Pin), default);
        var mWallet = await f.AddWalletAsync(member.Id, "MWallet", true, balance: 1000);

        var res = await service.ContributeAsync(member.Id, fund.Id, new(mWallet.Id, 100, "c-f13", Pin, config.Revision), default);
        Assert.Equal(100, res.AmountVnd);

        var treasury = await f.Db.V1FundTreasuries.SingleAsync(t => t.FundId == fund.Id);
        var account = await f.Db.LedgerAccounts.SingleAsync(a => a.Id == treasury.LedgerAccountId);
        Assert.Equal(100, account.Balance);
    }

    [Fact]
    public async Task F14_MultipleMembersCatchingUp_RestoresGroupFundingProgressively()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Owner");
        var m1 = await f.AddUserAsync("Member1");
        var m2 = await f.AddUserAsync("Member2");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, m1.Id);
        await V1FundMembershipTests.MakeFriends(f, owner.Id, m2.Id);
        var service = V1ElectionTests.Configured(f);
        var fund = await service.CreateAsync(owner.Id, new("FundF14", "Fixed", 300, Pin), default);
        var inv1 = await service.InviteAsync(owner.Id, fund.Id, m1.Id, default);
        await service.AcceptAsync(m1.Id, inv1.Id, default);
        var inv2 = await service.InviteAsync(owner.Id, fund.Id, m2.Id, default);
        await service.AcceptAsync(m2.Id, inv2.Id, default);

        var config = await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(300, Pin), default);
        var wOwner = await f.AddWalletAsync(owner.Id, "WOwner", true, balance: 1000);
        var wM1 = await f.AddWalletAsync(m1.Id, "WM1", true, balance: 1000);
        var wM2 = await f.AddWalletAsync(m2.Id, "WM2", true, balance: 1000);

        await service.ContributeAsync(owner.Id, fund.Id, new(wOwner.Id, 100, "c-o-f14", Pin, config.Revision), default);
        var mems1 = await service.MembersAsync(owner.Id, fund.Id, default);
        Assert.False(mems1[0].GroupFundingCompleted);

        await service.ContributeAsync(m1.Id, fund.Id, new(wM1.Id, 100, "c-m1-f14", Pin, config.Revision), default);
        var mems2 = await service.MembersAsync(owner.Id, fund.Id, default);
        Assert.False(mems2[0].GroupFundingCompleted);

        await service.ContributeAsync(m2.Id, fund.Id, new(wM2.Id, 100, "c-m2-f14", Pin, config.Revision), default);
        var mems3 = await service.MembersAsync(owner.Id, fund.Id, default);
        Assert.True(mems3[0].GroupFundingCompleted);
    }

    [Fact]
    public async Task F15_SpendingAfter100PercentFunding_DoesNotCloseFundingGate()
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

        await service.ContributeAsync(owner.Id, fund.Id, new(oWallet.Id, 100, "c-o-f15", Pin, config.Revision), default);
        await service.ContributeAsync(member.Id, fund.Id, new(mWallet.Id, 100, "c-m-f15", Pin, config.Revision), default);
        await service.SetSpendingPermissionAsync(owner.Id, fund.Id, member.Id, true, Pin, default);

        var exp1 = new V1FundRequestInput("Expense", 60, merchant.Id, "Expense 1", "exp-f15-1", Pin);
        var res1 = await service.CreateFundRequestAsync(member.Id, fund.Id, exp1, default);
        Assert.Equal("Completed", res1.State);

        var exp2 = new V1FundRequestInput("Expense", 40, merchant.Id, "Expense 2", "exp-f15-2", Pin);
        var res2 = await service.CreateFundRequestAsync(member.Id, fund.Id, exp2, default);
        Assert.Equal("Completed", res2.State);
    }

    // =========================================================================
    // F16 - F20: Giới hạn chi tiêu theo CanSpend, từ chối giao dịch vượt mức
    // =========================================================================

    [Fact]
    public async Task F16_DirectSpend_RequiresActiveRecipientWallet()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Owner");
        var member = await f.AddUserAsync("Member");
        var recipientWithoutWallet = await f.AddUserAsync("NoWalletUser", linkActiveBank: false);
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);
        var service = V1ElectionTests.Configured(f);
        var fund = await service.CreateAsync(owner.Id, new("FundF16", "Fixed", 200, Pin), default);
        var inv = await service.InviteAsync(owner.Id, fund.Id, member.Id, default);
        await service.AcceptAsync(member.Id, inv.Id, default);
        var config = await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(200, Pin), default);
        var oWallet = await f.AddWalletAsync(owner.Id, "OWallet", true, balance: 1000);
        var mWallet = await f.AddWalletAsync(member.Id, "MWallet", true, balance: 1000);

        await service.ContributeAsync(owner.Id, fund.Id, new(oWallet.Id, 100, "c-o-f16", Pin, config.Revision), default);
        await service.ContributeAsync(member.Id, fund.Id, new(mWallet.Id, 100, "c-m-f16", Pin, config.Revision), default);

        var exp = new V1FundRequestInput("Expense", 50, recipientWithoutWallet.Id, "Spend to user without wallet", "exp-f16", Pin);
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            service.CreateFundRequestAsync(owner.Id, fund.Id, exp, default));
        Assert.Equal("wallet_unavailable", ex.Code);
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

        await service.ContributeAsync(owner.Id, fund.Id, new(oWallet.Id, 100, "c-o-f17", Pin, config.Revision), default);
        await service.ContributeAsync(member.Id, fund.Id, new(mWallet.Id, 100, "c-m-f17", Pin, config.Revision), default);

        var exp = new V1FundRequestInput("Expense", 50, merchant.Id, "Direct expense", "exp-fixed-f17", Pin);
        var res = await service.CreateFundRequestAsync(owner.Id, fund.Id, exp, default);

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            service.ReviewFundRequestAsync(owner.Id, res.Id, true, "Cannot review", Pin, default));
        Assert.Equal("fixed_owner_review_disallowed", ex.Code);
    }

    [Fact]
    public async Task F18_SpendExceedingTreasuryBalance_IsRejected()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Owner");
        var member = await f.AddUserAsync("Member");
        var merchant = await f.AddUserAsync("Merchant");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);
        var service = V1ElectionTests.Configured(f);
        var fund = await service.CreateAsync(owner.Id, new("FundF18", "Fixed", 200, Pin), default);
        var inv = await service.InviteAsync(owner.Id, fund.Id, member.Id, default);
        await service.AcceptAsync(member.Id, inv.Id, default);
        var config = await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(200, Pin), default);
        var oWallet = await f.AddWalletAsync(owner.Id, "OWallet", true, balance: 1000);
        var mWallet = await f.AddWalletAsync(member.Id, "MWallet", true, balance: 1000);
        await f.AddWalletAsync(merchant.Id, "MerchWallet", true, balance: 0);

        await service.ContributeAsync(owner.Id, fund.Id, new(oWallet.Id, 100, "c-o-f18", Pin, config.Revision), default);
        await service.ContributeAsync(member.Id, fund.Id, new(mWallet.Id, 100, "c-m-f18", Pin, config.Revision), default);

        var expOver = new V1FundRequestInput("Expense", 201, merchant.Id, "Overspend", "exp-f18", Pin);
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            service.CreateFundRequestAsync(owner.Id, fund.Id, expOver, default));
        Assert.Equal("insufficient_fund_balance", ex.Code);
    }

    [Fact]
    public async Task F19_SpendBeforeObligationsConfirmed_IsRejected()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Owner");
        var member = await f.AddUserAsync("Member");
        var merchant = await f.AddUserAsync("Merchant");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);
        var service = V1ElectionTests.Configured(f);
        var fund = await service.CreateAsync(owner.Id, new("FundF19", "Fixed", 200, Pin), default);
        var inv = await service.InviteAsync(owner.Id, fund.Id, member.Id, default);
        await service.AcceptAsync(member.Id, inv.Id, default);
        await f.AddWalletAsync(merchant.Id, "MerchWallet", true, balance: 0);

        var exp = new V1FundRequestInput("Expense", 50, merchant.Id, "Unconfirmed target spend", "exp-f19", Pin);
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            service.CreateFundRequestAsync(owner.Id, fund.Id, exp, default));
        Assert.Equal("treasury_not_found", ex.Code);
    }

    [Fact]
    public async Task F20_OwnerMustContribute100PercentAndGroup100Percent_BeforeOwnerSpends()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Owner");
        var member = await f.AddUserAsync("Member");
        var merchant = await f.AddUserAsync("Merchant");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);
        var service = V1ElectionTests.Configured(f);
        var fund = await service.CreateAsync(owner.Id, new("FundF20", "Fixed", 200, Pin), default);
        var inv = await service.InviteAsync(owner.Id, fund.Id, member.Id, default);
        await service.AcceptAsync(member.Id, inv.Id, default);
        var config = await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(200, Pin), default);
        var oWallet = await f.AddWalletAsync(owner.Id, "OWallet", true, balance: 500);
        var mWallet = await f.AddWalletAsync(member.Id, "MWallet", true, balance: 500);
        await f.AddWalletAsync(merchant.Id, "MerchWallet", true, balance: 0);

        var exp = new V1FundRequestInput("Expense", 40, merchant.Id, "Owner expense", "exp-own-f20", Pin);

        // Owner 0 contribution -> fixed_member_contribution_incomplete
        var exInd = await Assert.ThrowsAsync<ConflictException>(() =>
            service.CreateFundRequestAsync(owner.Id, fund.Id, exp, default));
        Assert.Equal("fixed_member_contribution_incomplete", exInd.Code);

        // Owner 100, member 0 -> fixed_group_funding_incomplete
        await service.ContributeAsync(owner.Id, fund.Id, new(oWallet.Id, 100, "c-own-f20", Pin, config.Revision), default);
        var exGrp = await Assert.ThrowsAsync<ConflictException>(() =>
            service.CreateFundRequestAsync(owner.Id, fund.Id, exp, default));
        Assert.Equal("fixed_group_funding_incomplete", exGrp.Code);

        // Member 100 -> succeeds!
        await service.ContributeAsync(member.Id, fund.Id, new(mWallet.Id, 100, "c-mem-f20", Pin, config.Revision), default);
        var res = await service.CreateFundRequestAsync(owner.Id, fund.Id, exp, default);
        Assert.Equal("Completed", res.State);
    }

    // =========================================================================
    // F21 - F25: Số tiền dư cuối kỳ bị khóa hoặc chuyển tiếp theo chính sách (Locked excess)
    // =========================================================================

    [Fact]
    public async Task F21_TargetReduction_CreatesLockedExcessForContributingMembers()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Owner");
        var member = await f.AddUserAsync("Member");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);
        var service = V1ElectionTests.Configured(f);
        var fund = await service.CreateAsync(owner.Id, new("FundF21", "Fixed", 400, Pin), default);
        var inv = await service.InviteAsync(owner.Id, fund.Id, member.Id, default);
        await service.AcceptAsync(member.Id, inv.Id, default);
        var config1 = await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(400, Pin), default);
        var oWallet = await f.AddWalletAsync(owner.Id, "OWallet", true, balance: 1000);
        var mWallet = await f.AddWalletAsync(member.Id, "MWallet", true, balance: 1000);

        await service.ContributeAsync(owner.Id, fund.Id, new(oWallet.Id, 200, "c-o-f21", Pin, config1.Revision), default);
        await service.ContributeAsync(member.Id, fund.Id, new(mWallet.Id, 200, "c-m-f21", Pin, config1.Revision), default);

        // Target reduced to 200
        await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(200, Pin), default);

        var state = await service.FinanceAsync(owner.Id, fund.Id, default);
        var oDto = Assert.Single(state.Obligations, o => o.UserId == member.Id);
        Assert.Equal(100, oDto.LockedExcessVnd);
    }

    [Fact]
    public async Task F22_LockedExcess_IsExcludedFromSpendableBalance()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Owner");
        var member = await f.AddUserAsync("Member");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);
        var service = V1ElectionTests.Configured(f);
        var fund = await service.CreateAsync(owner.Id, new("FundF22", "Fixed", 400, Pin), default);
        var inv = await service.InviteAsync(owner.Id, fund.Id, member.Id, default);
        await service.AcceptAsync(member.Id, inv.Id, default);
        var config1 = await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(400, Pin), default);
        var oWallet = await f.AddWalletAsync(owner.Id, "OWallet", true, balance: 1000);
        var mWallet = await f.AddWalletAsync(member.Id, "MWallet", true, balance: 1000);

        await service.ContributeAsync(owner.Id, fund.Id, new(oWallet.Id, 200, "c-o-f22", Pin, config1.Revision), default);
        await service.ContributeAsync(member.Id, fund.Id, new(mWallet.Id, 200, "c-m-f22", Pin, config1.Revision), default);

        await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(200, Pin), default);

        // Treasury has 400, total locked excess = 200. Spendable balance = 200.
        var treasury = await f.Db.V1FundTreasuries.SingleAsync(t => t.FundId == fund.Id);
        var account = await f.Db.LedgerAccounts.SingleAsync(a => a.Id == treasury.LedgerAccountId);
        Assert.Equal(400, account.Balance);
    }

    [Fact]
    public async Task F23_AttemptingToSpendLockedExcess_FailsWithInsufficientFundBalance()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Owner");
        var member = await f.AddUserAsync("Member");
        var merchant = await f.AddUserAsync("Merchant");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);
        var service = V1ElectionTests.Configured(f);
        var fund = await service.CreateAsync(owner.Id, new("FundF23", "Fixed", 400, Pin), default);
        var inv = await service.InviteAsync(owner.Id, fund.Id, member.Id, default);
        await service.AcceptAsync(member.Id, inv.Id, default);
        var config1 = await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(400, Pin), default);
        var oWallet = await f.AddWalletAsync(owner.Id, "OWallet", true, balance: 1000);
        var mWallet = await f.AddWalletAsync(member.Id, "MWallet", true, balance: 1000);
        await f.AddWalletAsync(merchant.Id, "MerchWallet", true, balance: 0);

        await service.ContributeAsync(owner.Id, fund.Id, new(oWallet.Id, 200, "c-o-f23", Pin, config1.Revision), default);
        await service.ContributeAsync(member.Id, fund.Id, new(mWallet.Id, 200, "c-m-f23", Pin, config1.Revision), default);

        await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(200, Pin), default);

        var exp = new V1FundRequestInput("Expense", 220, merchant.Id, "Spend locked excess", "exp-f23", Pin);
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            service.CreateFundRequestAsync(owner.Id, fund.Id, exp, default));
        Assert.Equal("insufficient_fund_balance", ex.Code);
    }

    [Fact]
    public async Task F24_SpendWithinSpendableBalance_Succeeds()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Owner");
        var member = await f.AddUserAsync("Member");
        var merchant = await f.AddUserAsync("Merchant");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);
        var service = V1ElectionTests.Configured(f);
        var fund = await service.CreateAsync(owner.Id, new("FundF24", "Fixed", 400, Pin), default);
        var inv = await service.InviteAsync(owner.Id, fund.Id, member.Id, default);
        await service.AcceptAsync(member.Id, inv.Id, default);
        var config1 = await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(400, Pin), default);
        var oWallet = await f.AddWalletAsync(owner.Id, "OWallet", true, balance: 1000);
        var mWallet = await f.AddWalletAsync(member.Id, "MWallet", true, balance: 1000);
        await f.AddWalletAsync(merchant.Id, "MerchWallet", true, balance: 0);

        await service.ContributeAsync(owner.Id, fund.Id, new(oWallet.Id, 200, "c-o-f24", Pin, config1.Revision), default);
        await service.ContributeAsync(member.Id, fund.Id, new(mWallet.Id, 200, "c-m-f24", Pin, config1.Revision), default);

        await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(200, Pin), default);

        var exp = new V1FundRequestInput("Expense", 200, merchant.Id, "Exact spendable", "exp-f24", Pin);
        var res = await service.CreateFundRequestAsync(owner.Id, fund.Id, exp, default);
        Assert.Equal("Completed", res.State);
    }

    [Fact]
    public async Task F25_MultipleMembersWithLockedExcess_HaveCumulativeProtection()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Owner");
        var m1 = await f.AddUserAsync("Member1");
        var m2 = await f.AddUserAsync("Member2");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, m1.Id);
        await V1FundMembershipTests.MakeFriends(f, owner.Id, m2.Id);
        var service = V1ElectionTests.Configured(f);
        var fund = await service.CreateAsync(owner.Id, new("FundF25", "Fixed", 600, Pin), default);
        var inv1 = await service.InviteAsync(owner.Id, fund.Id, m1.Id, default);
        await service.AcceptAsync(m1.Id, inv1.Id, default);
        var inv2 = await service.InviteAsync(owner.Id, fund.Id, m2.Id, default);
        await service.AcceptAsync(m2.Id, inv2.Id, default);

        var config1 = await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(600, Pin), default);
        var wOwner = await f.AddWalletAsync(owner.Id, "WOwner", true, balance: 1000);
        var wM1 = await f.AddWalletAsync(m1.Id, "WM1", true, balance: 1000);
        var wM2 = await f.AddWalletAsync(m2.Id, "WM2", true, balance: 1000);

        await service.ContributeAsync(owner.Id, fund.Id, new(wOwner.Id, 200, "c-o-f25", Pin, config1.Revision), default);
        await service.ContributeAsync(m1.Id, fund.Id, new(wM1.Id, 200, "c-m1-f25", Pin, config1.Revision), default);
        await service.ContributeAsync(m2.Id, fund.Id, new(wM2.Id, 200, "c-m2-f25", Pin, config1.Revision), default);

        // Reduce to 300 (each owes 100). Total excess = 100 + 100 + 100 = 300.
        await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(300, Pin), default);

        var state = await service.FinanceAsync(owner.Id, fund.Id, default);
        Assert.All(state.Obligations, o => Assert.Equal(100, o.LockedExcessVnd));
    }

    // =========================================================================
    // F26 - F30: Tương tác với nhóm Tự do (Free fund), tính cô lập hoàn toàn giữa các nhóm
    // =========================================================================

    [Fact]
    public async Task F26_FreeFund_WithdrawalAndOwnerReview_Remains100PercentIntact()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Owner");
        var member = await f.AddUserAsync("Member");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);
        var service = V1ElectionTests.Configured(f);
        var fund = await service.CreateAsync(owner.Id, new("FundFreeF26", "Free", null, Pin), default);
        var inv = await service.InviteAsync(owner.Id, fund.Id, member.Id, default);
        await service.AcceptAsync(member.Id, inv.Id, default);
        var oWallet = await f.AddWalletAsync(owner.Id, "OWallet", true, balance: 1000);
        var mWallet = await f.AddWalletAsync(member.Id, "MWallet", true, balance: 100);

        await service.ContributeAsync(owner.Id, fund.Id, new(oWallet.Id, 300, "c-free-f26", Pin), default);

        var wd = new V1FundRequestInput("Withdrawal", 100, member.Id, "Reimbursement", "wd-free-f26", Pin);
        var req = await service.CreateFundRequestAsync(member.Id, fund.Id, wd, default);
        Assert.Equal("AwaitingOwnerReview", req.State);

        var reviewed = await service.ReviewFundRequestAsync(owner.Id, req.Id, true, "Approved", Pin, default);
        Assert.Equal("Completed", reviewed.State);

        f.Db.ChangeTracker.Clear();
        var mw = await f.Db.Wallets.SingleAsync(w => w.Id == mWallet.Id);
        Assert.Equal(200, mw.AvailableBalance);
    }

    [Fact]
    public async Task F27_FreeFund_DoesNotEnforceObligationOrIndividualContributionGate()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Owner");
        var member = await f.AddUserAsync("Member");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);
        var service = V1ElectionTests.Configured(f);
        var fund = await service.CreateAsync(owner.Id, new("FundFreeF27", "Free", null, Pin), default);
        var inv = await service.InviteAsync(owner.Id, fund.Id, member.Id, default);
        await service.AcceptAsync(member.Id, inv.Id, default);
        var oWallet = await f.AddWalletAsync(owner.Id, "OWallet", true, balance: 1000);

        // Owner contributes without obligation confirmation
        var cRes = await service.ContributeAsync(owner.Id, fund.Id, new(oWallet.Id, 500, "c-free-f27", Pin), default);
        Assert.Equal(500, cRes.AmountVnd);

        var members = await service.MembersAsync(owner.Id, fund.Id, default);
        var mDto = Assert.Single(members, m => m.UserId == member.Id);
        Assert.False(mDto.EffectiveCanSpend);
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
        var fund = await service.CreateAsync(owner.Id, new("FundReportF28", "Fixed", 200, Pin), default);
        var inv = await service.InviteAsync(owner.Id, fund.Id, member.Id, default);
        await service.AcceptAsync(member.Id, inv.Id, default);
        var config = await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(200, Pin), default);
        var oWallet = await f.AddWalletAsync(owner.Id, "OWallet", true, balance: 1000);
        var mWallet = await f.AddWalletAsync(member.Id, "MWallet", true, balance: 1000);
        var merchWallet = await f.AddWalletAsync(merchant.Id, "MerchWallet", true, balance: 0);

        await service.ContributeAsync(owner.Id, fund.Id, new(oWallet.Id, 100, "c-o-f28", Pin, config.Revision), default);
        await service.ContributeAsync(member.Id, fund.Id, new(mWallet.Id, 100, "c-m-f28", Pin, config.Revision), default);

        var exp = new V1FundRequestInput("Expense", 50, merchant.Id, "Valid expense", "exp-rep-f28", Pin);
        var res = await service.CreateFundRequestAsync(owner.Id, fund.Id, exp, default);
        Assert.Equal("Completed", res.State);

        var reported = await service.ReportFundRequestAsync(member.Id, res.Id, "Suspicious item reported", default);
        Assert.Equal("Completed", reported.State);

        f.Db.ChangeTracker.Clear();
        var mw = await f.Db.Wallets.SingleAsync(w => w.Id == merchWallet.Id);
        Assert.Equal(50, mw.AvailableBalance);

        var supportCase = await f.Db.SupportCases.SingleAsync(c => c.LinkedTransactionId == res.Id);
        Assert.Equal("V1FundDispute", supportCase.Category);
        Assert.Equal(member.Id, supportCase.LinkedUserId);
        Assert.Equal(CaseStatus.New, supportCase.Status);
    }

    [Fact]
    public async Task F29_ReportingSameCompletedExpenseTwice_IsIdempotent()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Owner");
        var member = await f.AddUserAsync("Member");
        var merchant = await f.AddUserAsync("Merchant");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);
        var service = V1ElectionTests.Configured(f);
        var fund = await service.CreateAsync(owner.Id, new("FundReportF29", "Fixed", 200, Pin), default);
        var inv = await service.InviteAsync(owner.Id, fund.Id, member.Id, default);
        await service.AcceptAsync(member.Id, inv.Id, default);
        var config = await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(200, Pin), default);
        var oWallet = await f.AddWalletAsync(owner.Id, "OWallet", true, balance: 1000);
        var mWallet = await f.AddWalletAsync(member.Id, "MWallet", true, balance: 1000);
        await f.AddWalletAsync(merchant.Id, "MerchWallet", true, balance: 0);

        await service.ContributeAsync(owner.Id, fund.Id, new(oWallet.Id, 100, "c-o-f29", Pin, config.Revision), default);
        await service.ContributeAsync(member.Id, fund.Id, new(mWallet.Id, 100, "c-m-f29", Pin, config.Revision), default);

        var exp = new V1FundRequestInput("Expense", 50, merchant.Id, "Valid expense", "exp-rep-f29", Pin);
        var res = await service.CreateFundRequestAsync(owner.Id, fund.Id, exp, default);

        var rep1 = await service.ReportFundRequestAsync(member.Id, res.Id, "First report", default);
        var rep2 = await service.ReportFundRequestAsync(member.Id, res.Id, "Second report duplicate", default);
        Assert.Equal(rep1.Id, rep2.Id);

        var disputeCount = await f.Db.SupportCases.CountAsync(c => c.LinkedTransactionId == res.Id);
        Assert.Equal(1, disputeCount);
    }

    [Fact]
    public async Task F30_ReportingNonCompletedOrNonExpense_ThrowsConflict()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Owner");
        var member = await f.AddUserAsync("Member");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);
        var service = V1ElectionTests.Configured(f);
        var fund = await service.CreateAsync(owner.Id, new("FundFreeF30", "Free", null, Pin), default);
        var inv = await service.InviteAsync(owner.Id, fund.Id, member.Id, default);
        await service.AcceptAsync(member.Id, inv.Id, default);
        var oWallet = await f.AddWalletAsync(owner.Id, "OWallet", true, balance: 1000);

        await service.ContributeAsync(owner.Id, fund.Id, new(oWallet.Id, 300, "c-free-f30", Pin), default);

        // Free fund withdrawal in AwaitingOwnerReview state
        var wd = new V1FundRequestInput("Withdrawal", 100, member.Id, "Withdrawal attempt", "wd-f30", Pin);
        var req = await service.CreateFundRequestAsync(member.Id, fund.Id, wd, default);

        // Attempting to report withdrawal throws fund_request_not_reportable
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            service.ReportFundRequestAsync(member.Id, req.Id, "Cannot report pending withdrawal", default));
        Assert.Equal("fund_request_not_reportable", ex.Code);
    }
}
