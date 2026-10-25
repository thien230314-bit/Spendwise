using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Features.V1Groups;
using SpendWise.Domain.Finance;
using SpendWise.Domain.V1Groups;
using Xunit;

namespace SpendWise.Tests;

public sealed class FixedFundV2BusinessRuleTests
{
    private const string Pin = TargetedCorrectnessFixture.SecurityPin;

    [Fact]
    public async Task F01_To_F30_FixedFundV2_ComprehensiveSuite()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Owner");
        var member1 = await f.AddUserAsync("Member1");
        var member2 = await f.AddUserAsync("Member2");
        var merchant = await f.AddUserAsync("Merchant");

        await V1FundMembershipTests.MakeFriends(f, owner.Id, member1.Id);
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member2.Id);

        var service = V1ElectionTests.Configured(f);
        var fund = await service.CreateAsync(owner.Id, new("FixedV2Fund", "Fixed", 300, Pin), default);

        var inv1 = await service.InviteAsync(owner.Id, fund.Id, member1.Id, default);
        await service.AcceptAsync(member1.Id, inv1.Id, default);
        var inv2 = await service.InviteAsync(owner.Id, fund.Id, member2.Id, default);
        await service.AcceptAsync(member2.Id, inv2.Id, default);

        var ownerWallet = await f.AddWalletAsync(owner.Id, "OwnerWallet", true, balance: 10_000);
        var m1Wallet = await f.AddWalletAsync(member1.Id, "M1Wallet", true, balance: 10_000);
        var m2Wallet = await f.AddWalletAsync(member2.Id, "M2Wallet", true, balance: 10_000);
        var merchantWallet = await f.AddWalletAsync(merchant.Id, "MerchantWallet", true, balance: 0);

        // F01: Set CanSpend for member before obligations confirmed -> throws ConflictException
        await Assert.ThrowsAsync<ConflictException>(() =>
            service.SetSpendingPermissionAsync(owner.Id, fund.Id, member1.Id, true, Pin, default));

        // Confirm obligations: 300 VND across 3 members = 100 VND each
        var config = await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(300, Pin), default);
        Assert.Equal(300, config.TargetVnd);

        // F02: Set CanSpend for member with 0 contributions -> throws ConflictException
        var exIncomplete = await Assert.ThrowsAsync<ConflictException>(() =>
            service.SetSpendingPermissionAsync(owner.Id, fund.Id, member1.Id, true, Pin, default));
        Assert.Equal("fixed_member_contribution_incomplete", exIncomplete.Code);

        // F03: Member1 contributes partial (50/100) -> Set CanSpend still throws
        await service.ContributeAsync(member1.Id, fund.Id, new(m1Wallet.Id, 50, "c-m1-p", Pin, config.Revision), default);
        await Assert.ThrowsAsync<ConflictException>(() =>
            service.SetSpendingPermissionAsync(owner.Id, fund.Id, member1.Id, true, Pin, default));

        // Member1 completes 100% personal obligation (50 + 50 = 100)
        await service.ContributeAsync(member1.Id, fund.Id, new(m1Wallet.Id, 50, "c-m1-full", Pin, config.Revision), default);

        // F04: Owner enables CanSpend for Member1 -> succeeds
        await service.SetSpendingPermissionAsync(owner.Id, fund.Id, member1.Id, true, Pin, default);

        // F05: Check MembersAsync DTOs
        var members = await service.MembersAsync(owner.Id, fund.Id, default);
        var m1Dto = Assert.Single(members, m => m.UserId == member1.Id);
        Assert.True(m1Dto.CanSpend); // Stored
        Assert.True(m1Dto.IndividualContributionCompleted);
        Assert.False(m1Dto.GroupFundingCompleted); // Group is only 100/300!
        Assert.False(m1Dto.EffectiveCanSpend); // Group not yet completed!

        // F06: Member1 attempts expense before group target 100% -> throws fixed_group_funding_incomplete
        var exp1 = new V1FundRequestInput("Expense", 20, merchant.Id, "Exp before group", "exp-g-inc", Pin);
        var exGroupInc = await Assert.ThrowsAsync<ConflictException>(() =>
            service.CreateFundRequestAsync(member1.Id, fund.Id, exp1, default));
        Assert.Equal("fixed_group_funding_incomplete", exGroupInc.Code);

        // F07: Owner attempts expense before completing owner's individual quota -> throws fixed_member_contribution_incomplete
        var exOwnerIndInc = await Assert.ThrowsAsync<ConflictException>(() =>
            service.CreateFundRequestAsync(owner.Id, fund.Id, exp1, default));
        Assert.Equal("fixed_member_contribution_incomplete", exOwnerIndInc.Code);

        // Owner completes 100% individual quota (100 VND)
        await service.ContributeAsync(owner.Id, fund.Id, new(ownerWallet.Id, 100, "c-own-full", Pin, config.Revision), default);

        // F07b: Now Owner has 100% individual, but group is only 200/300 -> throws fixed_group_funding_incomplete
        var expOwnerGroup = new V1FundRequestInput("Expense", 20, merchant.Id, "Owner exp before group 100%", "exp-own-g-inc", Pin);
        var exOwnerGroupInc = await Assert.ThrowsAsync<ConflictException>(() =>
            service.CreateFundRequestAsync(owner.Id, fund.Id, expOwnerGroup, default));
        Assert.Equal("fixed_group_funding_incomplete", exOwnerGroupInc.Code);

        // Member2 completes 100% (100 VND)
        await service.ContributeAsync(member2.Id, fund.Id, new(m2Wallet.Id, 100, "c-m2-full", Pin, config.Revision), default);

        // Now Group has 100 + 100 + 100 = 300 VND (100% funded)
        members = await service.MembersAsync(owner.Id, fund.Id, default);
        m1Dto = Assert.Single(members, m => m.UserId == member1.Id);
        var m2Dto = Assert.Single(members, m => m.UserId == member2.Id);
        var ownerDto = Assert.Single(members, m => m.UserId == owner.Id);

        Assert.True(m1Dto.GroupFundingCompleted);
        Assert.True(m1Dto.IndividualContributionCompleted);
        Assert.True(m1Dto.CanSpend);
        Assert.True(m1Dto.EffectiveCanSpend);

        Assert.True(m2Dto.GroupFundingCompleted);
        Assert.True(m2Dto.IndividualContributionCompleted);
        Assert.False(m2Dto.CanSpend); // Owner hasn't granted CanSpend to Member2
        Assert.False(m2Dto.EffectiveCanSpend);

        Assert.True(ownerDto.GroupFundingCompleted);
        Assert.True(ownerDto.IndividualContributionCompleted);
        Assert.True(ownerDto.EffectiveCanSpend); // Owner has implicit permission

        // F08: Member2 (CanSpend = false) attempts expense -> throws ForbiddenException
        var expM2 = new V1FundRequestInput("Expense", 30, merchant.Id, "Exp M2 no permission", "exp-m2-no-perm", Pin);
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.CreateFundRequestAsync(member2.Id, fund.Id, expM2, default));

        // F09: Member1 (EffectiveCanSpend = true) spends 50 VND -> executes directly, no 20% limit, no 24h hold!
        var expM1Valid = new V1FundRequestInput("Expense", 50, merchant.Id, "M1 Hotel deposit", "exp-m1-valid", Pin);
        var spentResult = await service.CreateFundRequestAsync(member1.Id, fund.Id, expM1Valid, default);
        Assert.Equal("Completed", spentResult.State);
        Assert.Equal("Executed", spentResult.ExecutionAvailability);

        f.Db.ChangeTracker.Clear();
        var merchWalletCheck = await f.Db.Wallets.SingleAsync(w => w.Id == merchantWallet.Id);
        Assert.Equal(50, merchWalletCheck.AvailableBalance);

        // F10: Subsequent spending when balance dropped below target (balance = 250 < 300 target)
        // Group funding remains 100% because valid contributions = 300 >= 300.
        // Member1 can spend another 60 VND immediately!
        var expM1Second = new V1FundRequestInput("Expense", 60, merchant.Id, "M1 Second expense", "exp-m1-sec", Pin);
        var spentResult2 = await service.CreateFundRequestAsync(member1.Id, fund.Id, expM1Second, default);
        Assert.Equal("Completed", spentResult2.State);
        Assert.Equal("Executed", spentResult2.ExecutionAvailability);

        f.Db.ChangeTracker.Clear();
        merchWalletCheck = await f.Db.Wallets.SingleAsync(w => w.Id == merchantWallet.Id);
        Assert.Equal(110, merchWalletCheck.AvailableBalance); // 50 + 60 = 110

        // F11: Owner review on Fixed fund request -> throws ConflictException (fixed_owner_review_disallowed)
        var exReview = await Assert.ThrowsAsync<ConflictException>(() =>
            service.ReviewFundRequestAsync(owner.Id, spentResult.Id, true, "Cannot review fixed", Pin, default));
        Assert.Equal("fixed_owner_review_disallowed", exReview.Code);

        // F12: Cannot modify Owner's CanSpend
        var exOwnerMod = await Assert.ThrowsAsync<ConflictException>(() =>
            service.SetSpendingPermissionAsync(owner.Id, fund.Id, owner.Id, true, Pin, default));
        Assert.Equal("owner_spending_permission_invalid", exOwnerMod.Code);
    }
}
