using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Features.V1Groups;
using SpendWise.Domain.Finance;
using SpendWise.Domain.V1Groups;
using Xunit;

namespace SpendWise.Tests;

public sealed class V1FundRequestTests
{
    [Fact]
    public async Task Fixed_DirectSpend_CanSpendOff_ThrowsForbidden()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Owner");
        var member = await f.AddUserAsync("Member");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);

        var service = V1ElectionTests.Configured(f);
        var fund = await service.CreateAsync(owner.Id, new("FixedTrip", "Fixed", 200, TargetedCorrectnessFixture.SecurityPin), default);

        var invite = await service.InviteAsync(owner.Id, fund.Id, member.Id, default);
        await service.AcceptAsync(member.Id, invite.Id, default);

        var request = new V1FundRequestInput("Expense", 10, member.Id, "Snacks", "exp-1", TargetedCorrectnessFixture.SecurityPin);

        // CanSpend is OFF by default -> Forbidden
        var ex = await Assert.ThrowsAsync<ForbiddenException>(() => service.CreateFundRequestAsync(member.Id, fund.Id, request, default));
        Assert.Equal("fund_spending_permission_required", ex.Code);
    }

    [Fact]
    public async Task Fixed_DirectSpend_CanSpendOn_UnderThreshold_ExecutesImmediately()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Owner");
        var member = await f.AddUserAsync("Member");
        var merchant = await f.AddUserAsync("Merchant");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);

        var service = V1ElectionTests.Configured(f);
        var fund = await service.CreateAsync(owner.Id, new("FixedTrip", "Fixed", 200, TargetedCorrectnessFixture.SecurityPin), default);

        var invite = await service.InviteAsync(owner.Id, fund.Id, member.Id, default);
        await service.AcceptAsync(member.Id, invite.Id, default);

        var ownerWallet = await f.AddWalletAsync(owner.Id, "OwnerMain", true, balance: 1_000);
        var memberWallet = await f.AddWalletAsync(member.Id, "MemberMain", true, balance: 1_000);
        var merchantWallet = await f.AddWalletAsync(merchant.Id, "MerchantMain", true, balance: 0);

        var config = await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(200, TargetedCorrectnessFixture.SecurityPin), default);

        // Both contribute obligations
        await service.ContributeAsync(owner.Id, fund.Id,
            new(ownerWallet.Id, 100, "c-owner", TargetedCorrectnessFixture.SecurityPin, config.Revision), default);
        await service.ContributeAsync(member.Id, fund.Id,
            new(memberWallet.Id, 100, "c-member", TargetedCorrectnessFixture.SecurityPin, config.Revision), default);

        // Owner enables CanSpend for Member
        await service.SetSpendingPermissionAsync(owner.Id, fund.Id, member.Id, true, TargetedCorrectnessFixture.SecurityPin, default);

        // Member executes expense under 20% threshold (e.g. 20 VND < 40 VND threshold on 200 VND balance)
        var request = new V1FundRequestInput("Expense", 20, merchant.Id, "Bus tickets", "exp-direct-1", TargetedCorrectnessFixture.SecurityPin);
        var executed = await service.CreateFundRequestAsync(member.Id, fund.Id, request, default);

        Assert.Equal("Completed", executed.State);
        Assert.Equal("Executed", executed.ExecutionAvailability);

        f.Db.ChangeTracker.Clear();
        // Merchant received 20 VND
        var mw = await f.Db.Wallets.SingleAsync(w => w.Id == merchantWallet.Id);
        Assert.Equal(20, mw.AvailableBalance);

        // Treasury has 180 VND left
        var treasury = await f.Db.V1FundTreasuries.SingleAsync(t => t.FundId == fund.Id);
        var ta = await f.Db.LedgerAccounts.SingleAsync(a => a.Id == treasury.LedgerAccountId);
        Assert.Equal(180, ta.Balance);
    }

    [Fact]
    public async Task Fixed_DirectSpend_LargeExpense_Enters24hHold()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Owner");
        var member = await f.AddUserAsync("Member");
        var merchant = await f.AddUserAsync("Merchant");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);

        var service = V1ElectionTests.Configured(f);
        var fund = await service.CreateAsync(owner.Id, new("FixedTripLarge", "Fixed", 200, TargetedCorrectnessFixture.SecurityPin), default);

        var invite = await service.InviteAsync(owner.Id, fund.Id, member.Id, default);
        await service.AcceptAsync(member.Id, invite.Id, default);

        var ownerWallet = await f.AddWalletAsync(owner.Id, "OwnerMain", true, balance: 1_000);
        var memberWallet = await f.AddWalletAsync(member.Id, "MemberMain", true, balance: 1_000);
        await f.AddWalletAsync(merchant.Id, "MerchantMain", true, balance: 0);

        var config = await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(200, TargetedCorrectnessFixture.SecurityPin), default);

        await service.ContributeAsync(owner.Id, fund.Id,
            new(ownerWallet.Id, 100, "c-owner-lg", TargetedCorrectnessFixture.SecurityPin, config.Revision), default);
        await service.ContributeAsync(member.Id, fund.Id,
            new(memberWallet.Id, 100, "c-member-lg", TargetedCorrectnessFixture.SecurityPin, config.Revision), default);

        await service.SetSpendingPermissionAsync(owner.Id, fund.Id, member.Id, true, TargetedCorrectnessFixture.SecurityPin, default);

        // 50 VND >= 20% of 200 VND (40 VND) -> Enters 24h hold
        var request = new V1FundRequestInput("Expense", 50, merchant.Id, "Hotel deposit", "exp-large-1", TargetedCorrectnessFixture.SecurityPin);
        var held = await service.CreateFundRequestAsync(member.Id, fund.Id, request, default);

        Assert.Equal("Pending24h", held.State);
        Assert.Equal("Hold24h", held.ExecutionAvailability);

        // Cancellation by member
        var cancelled = await service.CancelFundRequestAsync(member.Id, held.Id, TargetedCorrectnessFixture.SecurityPin, default);
        Assert.Equal("Cancelled", cancelled.State);
        Assert.Equal("Cancelled", cancelled.ExecutionAvailability);
    }

    [Fact]
    public async Task Free_Withdrawal_Flow_RequiresOwnerApproval_AndTransfersRealMoney()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Owner");
        var member = await f.AddUserAsync("Member");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);

        var service = V1ElectionTests.Configured(f);
        var fund = await service.CreateAsync(owner.Id, new("FreeFundWD", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);

        var invite = await service.InviteAsync(owner.Id, fund.Id, member.Id, default);
        await service.AcceptAsync(member.Id, invite.Id, default);

        var ownerWallet = await f.AddWalletAsync(owner.Id, "OwnerMain", true, balance: 500);
        var memberWallet = await f.AddWalletAsync(member.Id, "MemberMain", true, balance: 100);

        // Owner contributes 300 VND to fund
        await service.ContributeAsync(owner.Id, fund.Id,
            new(ownerWallet.Id, 300, "c-free-wd", TargetedCorrectnessFixture.SecurityPin), default);

        // Member creates a withdrawal request for 150 VND
        var wdRequest = new V1FundRequestInput("Withdrawal", 150, member.Id, "Snacks reimbursement", "wd-req-1", TargetedCorrectnessFixture.SecurityPin);
        var created = await service.CreateFundRequestAsync(member.Id, fund.Id, wdRequest, default);

        Assert.Equal("AwaitingOwnerReview", created.State);
        Assert.Equal("AwaitingReview", created.ExecutionAvailability);

        // Non-owner review throws Forbidden
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.ReviewFundRequestAsync(member.Id, created.Id, true, "Self approve", TargetedCorrectnessFixture.SecurityPin, default));

        // Owner approves
        var reviewed = await service.ReviewFundRequestAsync(owner.Id, created.Id, true, "Approved expense receipts", TargetedCorrectnessFixture.SecurityPin, default);
        Assert.Equal("Completed", reviewed.State);
        Assert.Equal("Executed", reviewed.ExecutionAvailability);

        // Verify real money transfer: member wallet increased by 150 VND
        f.Db.ChangeTracker.Clear();
        var mw = await f.Db.Wallets.SingleAsync(w => w.Id == memberWallet.Id);
        Assert.Equal(250, mw.AvailableBalance); // 100 + 150 = 250

        // Treasury balance reduced to 150 VND
        var treasury = await f.Db.V1FundTreasuries.SingleAsync(t => t.FundId == fund.Id);
        var ta = await f.Db.LedgerAccounts.SingleAsync(a => a.Id == treasury.LedgerAccountId);
        Assert.Equal(150, ta.Balance);
    }
}
