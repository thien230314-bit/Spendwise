using Microsoft.EntityFrameworkCore;
using SpendWise.Domain.Finance;
using SpendWise.Domain.V1Groups;
using Xunit;

namespace SpendWise.Tests;

public sealed class FreeFundEquityIntegrationTests
{
    [Fact]
    public async Task FreeFund_OwnerReviewsMemberExpense_CalculatesEquityWithoutDoubleCounting()
    {
        // Integration test verifying:
        // A contributes 600,000 VND, B contributes 400,000 VND.
        // Member B requests Expense 200,000 VND. Owner A reviews & approves.
        // Verify: Expense is NOT double counted! A remaining equity = 480k, B = 320k. Total equity = 800k.
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("OwnerA");
        var member = await f.AddUserAsync("MemberB");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);

        var s = V1FundMembershipTests.Service(f);
        var fundDto = await s.CreateAsync(owner.Id, new("TripFund", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);

        var invite = await s.InviteAsync(owner.Id, fundDto.Id, member.Id, default);
        await s.AcceptAsync(member.Id, invite.Id, default);

        var ownerWallet = await f.AddWalletAsync(owner.Id, "OwnerMain", true, balance: 1_000_000);
        var memberWallet = await f.AddWalletAsync(member.Id, "MemberMain", true, balance: 1_000_000);

        // A contributes 600k
        await s.ContributeAsync(owner.Id, fundDto.Id,
            new(ownerWallet.Id, 600_000, "contrib-a", TargetedCorrectnessFixture.SecurityPin), default);

        // B contributes 400k
        await s.ContributeAsync(member.Id, fundDto.Id,
            new(memberWallet.Id, 400_000, "contrib-b", TargetedCorrectnessFixture.SecurityPin), default);

        // B requests 200k expense
        var request = await s.CreateFundRequestAsync(member.Id, fundDto.Id,
            new("Expense", 200_000, member.Id, "Dinner", "req-b-exp", TargetedCorrectnessFixture.SecurityPin), default);

        // Owner A approves expense
        await s.ReviewFundRequestAsync(owner.Id, request.Id, true, "Approved", TargetedCorrectnessFixture.SecurityPin, default);

        var fund = await f.Db.V1Funds.FirstAsync(fd => fd.Id == fundDto.Id);
        var treasury = await f.Db.V1FundTreasuries.FirstAsync(t => t.FundId == fund.Id);
        var equityResult = await s.CalculateFreeFundEquityAsync(fund, treasury, null, default);

        Assert.Equal(800_000, equityResult.CurrentTreasuryBalanceVnd);
        Assert.Equal(800_000, equityResult.TotalFundEquityVnd);

        var eqA = equityResult.MemberEquities.Single(m => m.UserId == owner.Id);
        var eqB = equityResult.MemberEquities.Single(m => m.UserId == member.Id);

        Assert.Equal(480_000, eqA.EquityVnd);
        Assert.Equal(320_000, eqB.EquityVnd);
        Assert.Equal(120_000, eqA.TotalSharedExpenseChargedVnd);
        Assert.Equal(80_000, eqB.TotalSharedExpenseChargedVnd);
    }

    [Fact]
    public async Task FreeFund_DirectOwnerSpend_CalculatesEquityCorrectly()
    {
        // Owner spends directly 200k
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("OwnerA");
        var member = await f.AddUserAsync("MemberB");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);

        var s = V1FundMembershipTests.Service(f);
        var fundDto = await s.CreateAsync(owner.Id, new("DirectSpendFund", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);

        var invite = await s.InviteAsync(owner.Id, fundDto.Id, member.Id, default);
        await s.AcceptAsync(member.Id, invite.Id, default);

        var ownerWallet = await f.AddWalletAsync(owner.Id, "OwnerMain", true, balance: 1_000_000);
        var memberWallet = await f.AddWalletAsync(member.Id, "MemberMain", true, balance: 1_000_000);

        await s.ContributeAsync(owner.Id, fundDto.Id,
            new(ownerWallet.Id, 600_000, "contrib-a", TargetedCorrectnessFixture.SecurityPin), default);
        await s.ContributeAsync(member.Id, fundDto.Id,
            new(memberWallet.Id, 400_000, "contrib-b", TargetedCorrectnessFixture.SecurityPin), default);

        // Owner direct expense
        await s.CreateFundRequestAsync(owner.Id, fundDto.Id,
            new("Expense", 200_000, owner.Id, "Hotel", "owner-direct-exp", TargetedCorrectnessFixture.SecurityPin), default);

        var fund = await f.Db.V1Funds.FirstAsync(fd => fd.Id == fundDto.Id);
        var treasury = await f.Db.V1FundTreasuries.FirstAsync(t => t.FundId == fund.Id);
        var equityResult = await s.CalculateFreeFundEquityAsync(fund, treasury, null, default);

        var eqA = equityResult.MemberEquities.Single(m => m.UserId == owner.Id);
        var eqB = equityResult.MemberEquities.Single(m => m.UserId == member.Id);

        Assert.Equal(480_000, eqA.EquityVnd);
        Assert.Equal(320_000, eqB.EquityVnd);
    }

    [Fact]
    public async Task FreeFund_NewcomerAfterOldExpense_StartsAtZeroAndDoesNotBearPastExpense()
    {
        // A & B contribute 600k & 400k. Shared expense 200k.
        // Then C joins and contributes 200k.
        // Expected: A=480k, B=320k, C=200k. Total = 1,000,000 VND.
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("OwnerA");
        var memberB = await f.AddUserAsync("MemberB");
        var memberC = await f.AddUserAsync("MemberC");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, memberB.Id);
        await V1FundMembershipTests.MakeFriends(f, owner.Id, memberC.Id);

        var s = V1FundMembershipTests.Service(f);
        var fundDto = await s.CreateAsync(owner.Id, new("TimelineFund", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);

        var inviteB = await s.InviteAsync(owner.Id, fundDto.Id, memberB.Id, default);
        await s.AcceptAsync(memberB.Id, inviteB.Id, default);

        var walletA = await f.AddWalletAsync(owner.Id, "A", true, balance: 1_000_000);
        var walletB = await f.AddWalletAsync(memberB.Id, "B", true, balance: 1_000_000);
        var walletC = await f.AddWalletAsync(memberC.Id, "C", true, balance: 1_000_000);

        await s.ContributeAsync(owner.Id, fundDto.Id, new(walletA.Id, 600_000, "c-a", TargetedCorrectnessFixture.SecurityPin), default);
        await s.ContributeAsync(memberB.Id, fundDto.Id, new(walletB.Id, 400_000, "c-b", TargetedCorrectnessFixture.SecurityPin), default);

        await s.CreateFundRequestAsync(owner.Id, fundDto.Id,
            new("Expense", 200_000, owner.Id, "Shared lunch", "exp-1", TargetedCorrectnessFixture.SecurityPin), default);

        // Advance time for C's join
        var inviteC = await s.InviteAsync(owner.Id, fundDto.Id, memberC.Id, default);
        await s.AcceptAsync(memberC.Id, inviteC.Id, default);

        await s.ContributeAsync(memberC.Id, fundDto.Id, new(walletC.Id, 200_000, "c-c", TargetedCorrectnessFixture.SecurityPin), default);

        var fund = await f.Db.V1Funds.FirstAsync(fd => fd.Id == fundDto.Id);
        var treasury = await f.Db.V1FundTreasuries.FirstAsync(t => t.FundId == fund.Id);
        var equityResult = await s.CalculateFreeFundEquityAsync(fund, treasury, null, default);

        Assert.Equal(1_000_000, equityResult.CurrentTreasuryBalanceVnd);
        Assert.Equal(1_000_000, equityResult.TotalFundEquityVnd);

        var eqA = equityResult.MemberEquities.Single(m => m.UserId == owner.Id);
        var eqB = equityResult.MemberEquities.Single(m => m.UserId == memberB.Id);
        var eqC = equityResult.MemberEquities.Single(m => m.UserId == memberC.Id);

        Assert.Equal(480_000, eqA.EquityVnd);
        Assert.Equal(320_000, eqB.EquityVnd);
        Assert.Equal(200_000, eqC.EquityVnd);
        Assert.Equal(0, eqC.TotalSharedExpenseChargedVnd);

        // Preview C leave: exactly 200,000 VND
        var previewC = await s.PreviewRefundAsync(memberC.Id, fund.Id, null, default);
        Assert.Equal(200_000, previewC.EligibleRefundAmountVnd);
    }

    [Fact]
    public async Task FreeFund_Dissolution_DiscrepancyThrowsAndHaltsSettlement()
    {
        // If treasury balance does not match total equity, CalculateRefunds throws InvalidOperationException
        var members = new List<FreeFundEquityEngine.MemberEpochState>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), 1, 400_000),
            new(Guid.NewGuid(), Guid.NewGuid(), 2, 300_000)
        };

        // Total equity = 700k, but balance provided is 750k -> throws!
        var ex = Assert.Throws<InvalidOperationException>(() =>
            FreeFundEquityEngine.CalculateRefunds(members, 750_000, null));

        Assert.Contains("Dissolution equity mismatch", ex.Message);
    }
}
