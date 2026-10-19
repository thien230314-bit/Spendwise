using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Features.V1Groups;
using SpendWise.Domain.Finance;
using SpendWise.Domain.V1Groups;
using Xunit;

namespace SpendWise.Tests;

public sealed class V1FundRefundRegressionTests
{
    [Fact]
    public async Task CaseA_Unspent_MemberLeaves_Receives100PercentRefund()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Owner");
        var member = await f.AddUserAsync("Member");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);

        var s = V1FundMembershipTests.Service(f);
        var fund = await s.CreateAsync(owner.Id, new("FixedTrip", "Fixed", 200, TargetedCorrectnessFixture.SecurityPin), default);

        var invite = await s.InviteAsync(owner.Id, fund.Id, member.Id, default);
        await s.AcceptAsync(member.Id, invite.Id, default);

        var memberWallet = await f.AddWalletAsync(member.Id, "MemberMain", true, balance: 500);
        var config = await s.ConfirmObligationsAsync(owner.Id, fund.Id, new(200, TargetedCorrectnessFixture.SecurityPin), default);

        // Member contributes 100 VND
        await s.ContributeAsync(member.Id, fund.Id,
            new(memberWallet.Id, 100, "contrib-m1", TargetedCorrectnessFixture.SecurityPin, config.Revision), default);

        f.Db.ChangeTracker.Clear();
        var walletAfterContrib = await f.Db.Wallets.SingleAsync(w => w.Id == memberWallet.Id);
        Assert.Equal(400, walletAfterContrib.AvailableBalance);

        // Preview refund: should be 100 VND (CaseA_Unspent)
        var preview = await s.PreviewRefundAsync(member.Id, fund.Id, null, default);
        Assert.Equal(100, preview.EligibleRefundAmountVnd);
        Assert.Equal("CaseA_Unspent", preview.CaseType);

        // Member leaves fund
        var result = await s.LeaveFundAsync(member.Id, fund.Id,
            new(TargetedCorrectnessFixture.SecurityPin, "leave-m1"), default);

        Assert.Equal(100, result.RefundAmountVnd);
        Assert.Equal("CaseA_Unspent", result.CaseType);
        Assert.Equal("Completed", result.Status);

        f.Db.ChangeTracker.Clear();
        // Wallet balance restored to 500 VND!
        var walletAfterLeave = await f.Db.Wallets.SingleAsync(w => w.Id == memberWallet.Id);
        Assert.Equal(500, walletAfterLeave.AvailableBalance);

        // Membership left
        var membership = await f.Db.V1FundMemberships.SingleAsync(m => m.FundId == fund.Id && m.UserId == member.Id);
        Assert.NotNull(membership.LeftAt);

        // FinancialTransaction recorded with type Refund (not income)
        var ft = await f.Db.FinancialTransactions.SingleAsync(t => t.LedgerTransactionId == result.LedgerTransactionId);
        Assert.Equal(FinancialTransactionType.Refund, ft.Type);
        Assert.Equal(100, ft.Amount);
    }

    [Fact]
    public async Task CaseA_Unspent_OwnerKicksMember_Receives100PercentRefund()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Owner");
        var member = await f.AddUserAsync("Member");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);

        var s = V1FundMembershipTests.Service(f);
        var fund = await s.CreateAsync(owner.Id, new("FreeFund", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);

        var invite = await s.InviteAsync(owner.Id, fund.Id, member.Id, default);
        await s.AcceptAsync(member.Id, invite.Id, default);

        var memberWallet = await f.AddWalletAsync(member.Id, "MemberMain", true, balance: 300);

        // Member contributes 150 VND
        await s.ContributeAsync(member.Id, fund.Id,
            new(memberWallet.Id, 150, "contrib-free-m1", TargetedCorrectnessFixture.SecurityPin), default);

        f.Db.ChangeTracker.Clear();
        var walletBeforeKick = await f.Db.Wallets.SingleAsync(w => w.Id == memberWallet.Id);
        Assert.Equal(150, walletBeforeKick.AvailableBalance);

        // Owner kicks member
        var result = await s.KickMemberAsync(owner.Id, fund.Id, member.Id,
            new(TargetedCorrectnessFixture.SecurityPin, "Vi phạm nội quy nhóm", "kick-m1"), default);

        Assert.Equal(150, result.RefundAmountVnd);
        Assert.Equal("CaseA_Unspent", result.CaseType);
        Assert.Equal("Completed", result.Status);

        f.Db.ChangeTracker.Clear();
        // Wallet refunded back to 300 VND
        var walletAfterKick = await f.Db.Wallets.SingleAsync(w => w.Id == memberWallet.Id);
        Assert.Equal(300, walletAfterKick.AvailableBalance);

        var membership = await f.Db.V1FundMemberships.SingleAsync(m => m.FundId == fund.Id && m.UserId == member.Id);
        Assert.NotNull(membership.LeftAt);

        // Activity logged as MemberKicked
        var activity = await f.Db.V1FundActivities.OrderByDescending(a => a.CreatedAt).FirstAsync(a => a.FundId == fund.Id);
        Assert.Equal("MemberKicked", activity.Event);
        Assert.Equal(member.Id, activity.SubjectUserId);
        Assert.Contains("150", activity.Detail);
    }

    [Fact]
    public async Task CaseB_FixedFund_ProportionalShare_OnRemainingBalance()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Owner");
        var member = await f.AddUserAsync("Member");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);

        var s = V1FundMembershipTests.Service(f);
        var fund = await s.CreateAsync(owner.Id, new("FixedTrip", "Fixed", 2_000_000, TargetedCorrectnessFixture.SecurityPin), default);

        var invite = await s.InviteAsync(owner.Id, fund.Id, member.Id, default);
        await s.AcceptAsync(member.Id, invite.Id, default);

        var ownerWallet = await f.AddWalletAsync(owner.Id, "OwnerMain", true, balance: 1_000_000);
        var memberWallet = await f.AddWalletAsync(member.Id, "MemberMain", true, balance: 1_000_000);

        var config = await s.ConfirmObligationsAsync(owner.Id, fund.Id, new(2_000_000, TargetedCorrectnessFixture.SecurityPin), default);

        // Owner contributes 1,000,000 VND
        await s.ContributeAsync(owner.Id, fund.Id,
            new(ownerWallet.Id, 1_000_000, "contrib-owner", TargetedCorrectnessFixture.SecurityPin, config.Revision), default);

        // Member contributes 1,000,000 VND
        await s.ContributeAsync(member.Id, fund.Id,
            new(memberWallet.Id, 1_000_000, "contrib-member", TargetedCorrectnessFixture.SecurityPin, config.Revision), default);

        // Treasury balance is now 2,000,000 VND.
        // Simulate legitimate expenditure of 1,200,000 VND via ledger posting to a vendor account
        var treasury = await f.Db.V1FundTreasuries.SingleAsync(t => t.FundId == fund.Id);
        var vendorAccount = new LedgerAccount { Code = "EXPENSE:HOTEL", Name = "Hotel Expense", Type = LedgerAccountType.ExpenseClearing };
        f.Db.LedgerAccounts.Add(vendorAccount);
        await f.Db.SaveChangesAsync();

        var ledgerService = new SpendWise.Infrastructure.Finance.LedgerService(f.Db, f.Clock);
        await ledgerService.PostAsync(treasury.LedgerAccountId, vendorAccount.Id, 1_200_000, "VND",
            "V1FundExpense", fund.Id, $"v1-fund-expense:{fund.Id:N}", "expense-1", "Hotel booking", default);

        f.Db.ChangeTracker.Clear();
        var treasuryAccount = await f.Db.LedgerAccounts.SingleAsync(a => a.Id == treasury.LedgerAccountId);
        Assert.Equal(800_000, treasuryAccount.Balance);

        // Member previews refund: 50% of 800,000 = 400,000 VND!
        var preview = await s.PreviewRefundAsync(member.Id, fund.Id, null, default);
        Assert.Equal(400_000, preview.EligibleRefundAmountVnd);
        Assert.Equal("CaseB_FixedSpentClearShare", preview.CaseType);

        // Member leaves fund: receives exactly 400,000 VND
        var result = await s.LeaveFundAsync(member.Id, fund.Id,
            new(TargetedCorrectnessFixture.SecurityPin, "leave-member-caseb"), default);

        Assert.Equal(400_000, result.RefundAmountVnd);
        Assert.Equal("CaseB_FixedSpentClearShare", result.CaseType);

        f.Db.ChangeTracker.Clear();
        // Member wallet has 400,000 VND
        var mw = await f.Db.Wallets.SingleAsync(w => w.Id == memberWallet.Id);
        Assert.Equal(400_000, mw.AvailableBalance);

        // Fund treasury has remaining 400,000 VND belonging to Owner
        var finalTreasury = await f.Db.LedgerAccounts.SingleAsync(a => a.Id == treasury.LedgerAccountId);
        Assert.Equal(400_000, finalTreasury.Balance);
    }

    [Fact]
    public async Task CaseC_FreeFund_Dissolve_ProportionalShares()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("OwnerA");
        var member = await f.AddUserAsync("MemberB");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);

        var s = V1FundMembershipTests.Service(f);
        var fund = await s.CreateAsync(owner.Id, new("FreeFundDinner", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);

        var invite = await s.InviteAsync(owner.Id, fund.Id, member.Id, default);
        await s.AcceptAsync(member.Id, invite.Id, default);

        var ownerWallet = await f.AddWalletAsync(owner.Id, "OwnerMain", true, balance: 1_000_000);
        var memberWallet = await f.AddWalletAsync(member.Id, "MemberMain", true, balance: 1_000_000);

        // A contributes 700,000 VND
        await s.ContributeAsync(owner.Id, fund.Id,
            new(ownerWallet.Id, 700_000, "contrib-owner-700k", TargetedCorrectnessFixture.SecurityPin), default);

        // B contributes 300,000 VND
        await s.ContributeAsync(member.Id, fund.Id,
            new(memberWallet.Id, 300_000, "contrib-member-300k", TargetedCorrectnessFixture.SecurityPin), default);

        // Total contributed = 1,000,000 VND.
        // Simulate shared expense of 600,000 VND
        var treasury = await f.Db.V1FundTreasuries.SingleAsync(t => t.FundId == fund.Id);
        var vendorAccount = new LedgerAccount { Code = "EXPENSE:DINNER", Name = "Dinner Expense", Type = LedgerAccountType.ExpenseClearing };
        f.Db.LedgerAccounts.Add(vendorAccount);
        await f.Db.SaveChangesAsync();

        var ledgerService = new SpendWise.Infrastructure.Finance.LedgerService(f.Db, f.Clock);
        await ledgerService.PostAsync(treasury.LedgerAccountId, vendorAccount.Id, 600_000, "VND",
            "V1FundExpense", fund.Id, $"v1-fund-expense:{fund.Id:N}", "dinner-1", "Dinner shared expense", default);

        f.Db.ChangeTracker.Clear();
        var treasuryAccount = await f.Db.LedgerAccounts.SingleAsync(a => a.Id == treasury.LedgerAccountId);
        Assert.Equal(400_000, treasuryAccount.Balance);

        // Owner dissolves fund
        var dissolveResult = await s.DissolveFundAsync(owner.Id, fund.Id,
            new(TargetedCorrectnessFixture.SecurityPin, "dissolve-free-1"), default);

        Assert.Equal(400_000, dissolveResult.TotalRefundedVnd);
        Assert.Equal(0, dissolveResult.FinalBalanceVnd);
        Assert.Equal(2, dissolveResult.Refunds.Count);

        // A gets 280,000 VND (70% of 400,000)
        var refundA = dissolveResult.Refunds.Single(r => r.UserId == owner.Id);
        Assert.Equal(280_000, refundA.RefundAmountVnd);

        // B gets 120,000 VND (30% of 400,000)
        var refundB = dissolveResult.Refunds.Single(r => r.UserId == member.Id);
        Assert.Equal(120_000, refundB.RefundAmountVnd);

        f.Db.ChangeTracker.Clear();
        // Wallets updated
        var ow = await f.Db.Wallets.SingleAsync(w => w.Id == ownerWallet.Id);
        Assert.Equal(300_000 + 280_000, ow.AvailableBalance); // initial 1M - 700k + 280k = 580k

        var mw = await f.Db.Wallets.SingleAsync(w => w.Id == memberWallet.Id);
        Assert.Equal(700_000 + 120_000, mw.AvailableBalance); // initial 1M - 300k + 120k = 820k

        // Treasury balance is EXACTLY 0 VND!
        var finalTreasury = await f.Db.LedgerAccounts.SingleAsync(a => a.Id == treasury.LedgerAccountId);
        Assert.Equal(0, finalTreasury.Balance);

        // Fund is marked Dissolved
        var finalFund = await f.Db.V1Funds.SingleAsync(fd => fd.Id == fund.Id);
        Assert.NotNull(finalFund.DissolvedAt);
    }

    [Fact]
    public async Task CaseC_FreeFund_WithPersonalWithdrawals_DissolvesProportionallyByNetEquity()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("OwnerA");
        var member = await f.AddUserAsync("MemberB");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);

        var s = V1FundMembershipTests.Service(f);
        var fund = await s.CreateAsync(owner.Id, new("FreeFundDinner", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);

        var invite = await s.InviteAsync(owner.Id, fund.Id, member.Id, default);
        await s.AcceptAsync(member.Id, invite.Id, default);

        var ownerWallet = await f.AddWalletAsync(owner.Id, "OwnerMain", true, balance: 3_000_000);
        var memberWallet = await f.AddWalletAsync(member.Id, "MemberMain", true, balance: 2_000_000);

        // A contributes 2,000,000 VND
        await s.ContributeAsync(owner.Id, fund.Id,
            new(ownerWallet.Id, 2_000_000, "contrib-owner-2m", TargetedCorrectnessFixture.SecurityPin), default);

        // B contributes 1,000,000 VND
        await s.ContributeAsync(member.Id, fund.Id,
            new(memberWallet.Id, 1_000_000, "contrib-member-1m", TargetedCorrectnessFixture.SecurityPin), default);

        // Owner A requested and withdrew 1,000,000 VND personal withdrawal
        var treasury = await f.Db.V1FundTreasuries.SingleAsync(t => t.FundId == fund.Id);
        var ownerAccount = await f.Db.LedgerAccounts.SingleAsync(a => a.WalletId == ownerWallet.Id);
        var ledgerService = new SpendWise.Infrastructure.Finance.LedgerService(f.Db, f.Clock);

        await ledgerService.PostAsync(treasury.LedgerAccountId, ownerAccount.Id, 1_000_000, "VND",
            "V1FundWithdrawal", fund.Id, $"v1-fund-payout:{fund.Id:N}", "wd-post-1", "Personal withdrawal", default);

        f.Db.V1FundRequests.Add(new V1FundRequest
        {
            FundId = fund.Id,
            RequestedByUserId = owner.Id,
            RecipientUserId = owner.Id,
            Kind = "Withdrawal",
            AmountVnd = 1_000_000,
            Purpose = "Personal withdrawal",
            State = "Completed",
            IdempotencyKey = "wd-1"
        });
        await f.Db.SaveChangesAsync();

        // Simulate general shared expense of 800,000 VND
        var vendorAccount = new LedgerAccount { Code = "EXPENSE:TEAM", Name = "Team Outing", Type = LedgerAccountType.ExpenseClearing };
        f.Db.LedgerAccounts.Add(vendorAccount);
        await f.Db.SaveChangesAsync();

        await ledgerService.PostAsync(treasury.LedgerAccountId, vendorAccount.Id, 800_000, "VND",
            "V1FundExpense", fund.Id, $"v1-fund-expense:{fund.Id:N}", "team-1", "Team outing expense", default);

        // Net equity:
        // A: 2M contributed - 1M withdrawn = 1M net equity
        // B: 1M contributed - 0 withdrawn = 1M net equity
        // Remaining treasury balance: 3M - 1M - 800k = 1,200,000 VND.
        // On dissolution: A gets 600,000 VND, B gets 600,000 VND.
        var dissolveResult = await s.DissolveFundAsync(owner.Id, fund.Id,
            new(TargetedCorrectnessFixture.SecurityPin, "dissolve-netequity-1"), default);

        Assert.Equal(1_200_000, dissolveResult.TotalRefundedVnd);
        Assert.Equal(0, dissolveResult.FinalBalanceVnd);
        Assert.Equal(2, dissolveResult.Refunds.Count);

        var refA = dissolveResult.Refunds.Single(r => r.UserId == owner.Id);
        var refB = dissolveResult.Refunds.Single(r => r.UserId == member.Id);
        Assert.Equal(600_000, refA.RefundAmountVnd);
        Assert.Equal(600_000, refB.RefundAmountVnd);

        // Final treasury balance is EXACTLY 0 VND
        f.Db.ChangeTracker.Clear();
        var finalTreasury = await f.Db.LedgerAccounts.SingleAsync(a => a.Id == treasury.LedgerAccountId);
        Assert.Equal(0, finalTreasury.Balance);
    }

    [Fact]
    public async Task CaseB_FixedFund_WithLockedExcess_RefundsProtectedExcessFirst_ThenSharesAvailable()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Owner");
        var member = await f.AddUserAsync("Member");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);

        var s = V1FundMembershipTests.Service(f);
        var fund = await s.CreateAsync(owner.Id, new("FixedExcessTrip", "Fixed", 130, TargetedCorrectnessFixture.SecurityPin), default);
        var ownerWallet = await f.AddWalletAsync(owner.Id, "OwnerMain", true, balance: 1_000);
        var memberWallet = await f.AddWalletAsync(member.Id, "MemberMain", true, balance: 1_000);

        var config1 = await s.ConfirmObligationsAsync(owner.Id, fund.Id, new(130, TargetedCorrectnessFixture.SecurityPin), default);
        // Owner contributes 130 VND (obligation in revision 1 is 130 VND)
        await s.ContributeAsync(owner.Id, fund.Id,
            new(ownerWallet.Id, 130, "contrib-owner-excess", TargetedCorrectnessFixture.SecurityPin, config1.Revision), default);

        var invite = await s.InviteAsync(owner.Id, fund.Id, member.Id, default);
        await s.AcceptAsync(member.Id, invite.Id, default);

        // Owner reconfirms with target 200 VND (100 VND per member)
        // Owner now has required = 100, paid = 130 -> 30 VND locked excess!
        var config2 = await s.ConfirmObligationsAsync(owner.Id, fund.Id, new(200, TargetedCorrectnessFixture.SecurityPin), default);

        // Member contributes 100 VND (exact obligation)
        await s.ContributeAsync(member.Id, fund.Id,
            new(memberWallet.Id, 100, "contrib-member-exact", TargetedCorrectnessFixture.SecurityPin, config2.Revision), default);

        // Treasury has 230 VND.
        // Simulate shared expense of 130 VND
        var treasury = await f.Db.V1FundTreasuries.SingleAsync(t => t.FundId == fund.Id);
        var vendorAccount = new LedgerAccount { Code = "EXPENSE:LUNCH", Name = "Lunch", Type = LedgerAccountType.ExpenseClearing };
        f.Db.LedgerAccounts.Add(vendorAccount);
        await f.Db.SaveChangesAsync();

        var ledgerService = new SpendWise.Infrastructure.Finance.LedgerService(f.Db, f.Clock);
        await ledgerService.PostAsync(treasury.LedgerAccountId, vendorAccount.Id, 130, "VND",
            "V1FundExpense", fund.Id, $"v1-fund-expense:{fund.Id:N}", "lunch-1", "Lunch", default);

        // Remaining balance: 100 VND.
        // Protected excess for Owner: 30 VND.
        // Available for sharing: 100 - 30 = 70 VND.
        // Weights: Owner = 100, Member = 100 (50/50).
        // Member share = 35 VND.
        var previewMember = await s.PreviewRefundAsync(member.Id, fund.Id, null, default);
        Assert.Equal(35, previewMember.EligibleRefundAmountVnd);

        // Member leaves fund: gets 35 VND
        var leaveResult = await s.LeaveFundAsync(member.Id, fund.Id,
            new(TargetedCorrectnessFixture.SecurityPin, "leave-member-excess"), default);
        Assert.Equal(35, leaveResult.RefundAmountVnd);
        Assert.Equal("CaseB_FixedSpentClearShare", leaveResult.CaseType);

        // Owner remaining entitlement: 30 VND (excess) + 35 VND (share) = 65 VND
        var previewOwner = await s.PreviewRefundAsync(owner.Id, fund.Id, null, default);
        Assert.Equal(65, previewOwner.EligibleRefundAmountVnd);
    }

    [Fact]
    public async Task Idempotency_LeaveFundAsync_RetriedWithSameKey_ReturnsReplayedResult()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Owner");
        var member = await f.AddUserAsync("Member");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);

        var s = V1FundMembershipTests.Service(f);
        var fund = await s.CreateAsync(owner.Id, new("FixedReplay", "Fixed", 200, TargetedCorrectnessFixture.SecurityPin), default);

        var invite = await s.InviteAsync(owner.Id, fund.Id, member.Id, default);
        await s.AcceptAsync(member.Id, invite.Id, default);

        var memberWallet = await f.AddWalletAsync(member.Id, "MemberMain", true, balance: 500);
        var config = await s.ConfirmObligationsAsync(owner.Id, fund.Id, new(200, TargetedCorrectnessFixture.SecurityPin), default);

        await s.ContributeAsync(member.Id, fund.Id,
            new(memberWallet.Id, 100, "contrib-m1", TargetedCorrectnessFixture.SecurityPin, config.Revision), default);

        // First leave call
        var first = await s.LeaveFundAsync(member.Id, fund.Id,
            new(TargetedCorrectnessFixture.SecurityPin, "leave-replay-key-1"), default);
        Assert.Equal(100, first.RefundAmountVnd);
        Assert.Equal("Completed", first.Status);

        // Second leave call with same idempotency key
        var second = await s.LeaveFundAsync(member.Id, fund.Id,
            new(TargetedCorrectnessFixture.SecurityPin, "leave-replay-key-1"), default);
        Assert.Equal(100, second.RefundAmountVnd);
        Assert.Equal("Completed", second.Status);
        Assert.Equal("Replayed", second.CaseType);
        Assert.Equal(first.LedgerTransactionId, second.LedgerTransactionId);

        // Verify wallet was NOT refunded twice (balance is 500, not 600!)
        f.Db.ChangeTracker.Clear();
        var mw = await f.Db.Wallets.SingleAsync(w => w.Id == memberWallet.Id);
        Assert.Equal(500, mw.AvailableBalance);
    }

    [Fact]
    public async Task SafetyGate_OwnerCannotLeaveWithoutTransferringOwnership()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Owner");
        var member = await f.AddUserAsync("Member");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);

        var s = V1FundMembershipTests.Service(f);
        var fund = await s.CreateAsync(owner.Id, new("FixedFund", "Fixed", 200, TargetedCorrectnessFixture.SecurityPin), default);

        var invite = await s.InviteAsync(owner.Id, fund.Id, member.Id, default);
        await s.AcceptAsync(member.Id, invite.Id, default);

        // Owner trying to leave without transfer throws owner_transfer_required
        var ex = await Assert.ThrowsAsync<AppException>(() => s.LeaveFundAsync(owner.Id, fund.Id,
            new(TargetedCorrectnessFixture.SecurityPin, "leave-owner"), default));
        Assert.Equal("owner_transfer_required", ex.Code);
    }

    [Fact]
    public async Task SafetyGate_SingleMemberMustDissolve()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Owner");

        var s = V1FundMembershipTests.Service(f);
        var fund = await s.CreateAsync(owner.Id, new("SoloFund", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);

        // Owner is the only member; leaving is rejected, must dissolve
        var ex = await Assert.ThrowsAsync<AppException>(() => s.LeaveFundAsync(owner.Id, fund.Id,
            new(TargetedCorrectnessFixture.SecurityPin, "leave-solo"), default));
        Assert.Equal("single_member_must_dissolve", ex.Code);
    }

    [Fact]
    public async Task SafetyGate_NonOwnerCannotKick_ThrowsForbidden()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Owner");
        var memberA = await f.AddUserAsync("MemberA");
        var memberB = await f.AddUserAsync("MemberB");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, memberA.Id);
        await V1FundMembershipTests.MakeFriends(f, owner.Id, memberB.Id);

        var s = V1FundMembershipTests.Service(f);
        var fund = await s.CreateAsync(owner.Id, new("FixedFund", "Fixed", 300, TargetedCorrectnessFixture.SecurityPin), default);

        var invA = await s.InviteAsync(owner.Id, fund.Id, memberA.Id, default);
        await s.AcceptAsync(memberA.Id, invA.Id, default);

        var invB = await s.InviteAsync(owner.Id, fund.Id, memberB.Id, default);
        await s.AcceptAsync(memberB.Id, invB.Id, default);

        // Member A tries to kick Member B -> ForbiddenException
        await Assert.ThrowsAsync<ForbiddenException>(() => s.KickMemberAsync(memberA.Id, fund.Id, memberB.Id,
            new(TargetedCorrectnessFixture.SecurityPin, "Not allowed", "kick-forbidden"), default));
    }
}
