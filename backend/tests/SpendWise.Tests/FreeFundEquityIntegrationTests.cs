using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
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
    public async Task FreeFund_ReconciliationMismatch_BlocksPreviewLeaveKickAndDissolve()
    {
        // Total equity is 700k (A=400k, B=300k).
        // Ledger balance is 650k (deficit of 50k).
        // Verify: Preview, Leave, Kick, and Dissolve are ALL blocked with free_fund_reconciliation_mismatch!
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("OwnerA");
        var member = await f.AddUserAsync("MemberB");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);

        var s = V1FundMembershipTests.Service(f);
        var fundDto = await s.CreateAsync(owner.Id, new("ReconcileMismatchFund", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);

        var invite = await s.InviteAsync(owner.Id, fundDto.Id, member.Id, default);
        await s.AcceptAsync(member.Id, invite.Id, default);

        var ownerWallet = await f.AddWalletAsync(owner.Id, "OwnerMain", true, balance: 1_000_000);
        var memberWallet = await f.AddWalletAsync(member.Id, "MemberMain", true, balance: 1_000_000);

        await s.ContributeAsync(owner.Id, fundDto.Id,
            new(ownerWallet.Id, 400_000, "c-a-400k", TargetedCorrectnessFixture.SecurityPin), default);
        await s.ContributeAsync(member.Id, fundDto.Id,
            new(memberWallet.Id, 300_000, "c-b-300k", TargetedCorrectnessFixture.SecurityPin), default);

        // Manually introduce ledger discrepancy with balanced entries:
        // Post an unlinked external clearing debit of 50,000 VND so ledger is internally consistent at 650,000 VND,
        // but dynamic member equity (700k) deviates from treasury balance (650k).
        var treasury = await f.Db.V1FundTreasuries.SingleAsync(t => t.FundId == fundDto.Id);
        var clearingAcc = new LedgerAccount { Code = "MISMATCH:CLEARING", Name = "External Clearing", Type = LedgerAccountType.ExpenseClearing };
        f.Db.LedgerAccounts.Add(clearingAcc);
        await f.Db.SaveChangesAsync();

        var ledgerService = new SpendWise.Infrastructure.Finance.LedgerService(f.Db, f.Clock);
        await ledgerService.PostAsync(treasury.LedgerAccountId, clearingAcc.Id, 50_000, "VND",
            "ExternalAdjustment", fundDto.Id, $"adjust:{fundDto.Id:N}", "adj-1", "External discrepancy", default);

        // 1. PreviewRefundAsync must throw free_fund_reconciliation_mismatch
        var exPreview = await Assert.ThrowsAsync<ConflictException>(() =>
            s.PreviewRefundAsync(member.Id, fundDto.Id, null, default));
        Assert.Equal("free_fund_reconciliation_mismatch", exPreview.Code);

        // 2. LeaveFundAsync must throw free_fund_reconciliation_mismatch
        var exLeave = await Assert.ThrowsAsync<ConflictException>(() =>
            s.LeaveFundAsync(member.Id, fundDto.Id, new(TargetedCorrectnessFixture.SecurityPin, "leave-mismatch"), default));
        Assert.Equal("free_fund_reconciliation_mismatch", exLeave.Code);

        // 3. KickMemberAsync must throw free_fund_reconciliation_mismatch
        var exKick = await Assert.ThrowsAsync<ConflictException>(() =>
            s.KickMemberAsync(owner.Id, fundDto.Id, member.Id,
                new(TargetedCorrectnessFixture.SecurityPin, "Violation", "kick-mismatch", null), default));
        Assert.Equal("free_fund_reconciliation_mismatch", exKick.Code);

        // 4. DissolveFundAsync must throw free_fund_reconciliation_mismatch
        var exDissolve = await Assert.ThrowsAsync<ConflictException>(() =>
            s.DissolveFundAsync(owner.Id, fundDto.Id,
                new(TargetedCorrectnessFixture.SecurityPin, "dissolve-mismatch"), default));
        Assert.Equal("free_fund_reconciliation_mismatch", exDissolve.Code);
    }

    [Fact]
    public async Task FreeFund_ZeroLedgerWithPositiveEquity_BlocksLeaveAndDissolveWithZeroRefund()
    {
        // Equity is positive (500k), but treasury ledger balance is 0 VND.
        // System MUST NOT silently return 0 VND refund or permit leave/dissolve.
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("OwnerA");
        var member = await f.AddUserAsync("MemberB");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);

        var s = V1FundMembershipTests.Service(f);
        var fundDto = await s.CreateAsync(owner.Id, new("ZeroLedgerFund", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);

        var invite = await s.InviteAsync(owner.Id, fundDto.Id, member.Id, default);
        await s.AcceptAsync(member.Id, invite.Id, default);

        var ownerWallet = await f.AddWalletAsync(owner.Id, "OwnerMain", true, balance: 1_000_000);
        var memberWallet = await f.AddWalletAsync(member.Id, "MemberMain", true, balance: 1_000_000);

        await s.ContributeAsync(owner.Id, fundDto.Id,
            new(ownerWallet.Id, 500_000, "c-a-500k", TargetedCorrectnessFixture.SecurityPin), default);

        // Drain treasury ledger to 0 VND with balanced posting to external clearing account
        var treasury = await f.Db.V1FundTreasuries.SingleAsync(t => t.FundId == fundDto.Id);
        var clearingAcc = new LedgerAccount { Code = "DRAIN:CLEARING", Name = "Drain Clearing", Type = LedgerAccountType.ExpenseClearing };
        f.Db.LedgerAccounts.Add(clearingAcc);
        await f.Db.SaveChangesAsync();

        var ledgerService = new SpendWise.Infrastructure.Finance.LedgerService(f.Db, f.Clock);
        await ledgerService.PostAsync(treasury.LedgerAccountId, clearingAcc.Id, 500_000, "VND",
            "ExternalDrain", fundDto.Id, $"drain:{fundDto.Id:N}", "drain-1", "External drain", default);

        // Preview blocked with free_fund_reconciliation_mismatch
        var exPreview = await Assert.ThrowsAsync<ConflictException>(() =>
            s.PreviewRefundAsync(owner.Id, fundDto.Id, null, default));
        Assert.Equal("free_fund_reconciliation_mismatch", exPreview.Code);

        // Dissolution blocked with free_fund_reconciliation_mismatch
        var exDissolve = await Assert.ThrowsAsync<ConflictException>(() =>
            s.DissolveFundAsync(owner.Id, fundDto.Id,
                new(TargetedCorrectnessFixture.SecurityPin, "dissolve-zero-ledger"), default));
        Assert.Equal("free_fund_reconciliation_mismatch", exDissolve.Code);
    }

    [Fact]
    public async Task FreeFund_TwoExpensesWithSameAmount_DoNotMismapOrCrossMatch()
    {
        // A contributes 600k, B contributes 400k. Total = 1,000,000 VND.
        // Two distinct expenses of exactly 100,000 VND each are requested and approved.
        // Invariant: Both are matched 1-to-1 without double counting or cross-matching.
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("OwnerA");
        var member = await f.AddUserAsync("MemberB");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);

        var s = V1FundMembershipTests.Service(f);
        var fundDto = await s.CreateAsync(owner.Id, new("TwoExpensesSameAmount", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);

        var invite = await s.InviteAsync(owner.Id, fundDto.Id, member.Id, default);
        await s.AcceptAsync(member.Id, invite.Id, default);

        var ownerWallet = await f.AddWalletAsync(owner.Id, "OwnerMain", true, balance: 1_000_000);
        var memberWallet = await f.AddWalletAsync(member.Id, "MemberMain", true, balance: 1_000_000);

        await s.ContributeAsync(owner.Id, fundDto.Id,
            new(ownerWallet.Id, 600_000, "c-a", TargetedCorrectnessFixture.SecurityPin), default);
        await s.ContributeAsync(member.Id, fundDto.Id,
            new(memberWallet.Id, 400_000, "c-b", TargetedCorrectnessFixture.SecurityPin), default);

        // Expense 1: 100,000 VND
        var req1 = await s.CreateFundRequestAsync(member.Id, fundDto.Id,
            new("Expense", 100_000, member.Id, "Expense 1", "exp-same-1", TargetedCorrectnessFixture.SecurityPin), default);
        await s.ReviewFundRequestAsync(owner.Id, req1.Id, true, "Approved", TargetedCorrectnessFixture.SecurityPin, default);

        // Expense 2: 100,000 VND
        var req2 = await s.CreateFundRequestAsync(member.Id, fundDto.Id,
            new("Expense", 100_000, member.Id, "Expense 2", "exp-same-2", TargetedCorrectnessFixture.SecurityPin), default);
        await s.ReviewFundRequestAsync(owner.Id, req2.Id, true, "Approved", TargetedCorrectnessFixture.SecurityPin, default);

        var fund = await f.Db.V1Funds.FirstAsync(fd => fd.Id == fundDto.Id);
        var treasury = await f.Db.V1FundTreasuries.FirstAsync(t => t.FundId == fund.Id);
        var equityResult = await s.CalculateFreeFundEquityAsync(fund, treasury, null, default);

        // 1,000,000 - 100k - 100k = 800,000 VND
        Assert.Equal(800_000, equityResult.CurrentTreasuryBalanceVnd);
        Assert.Equal(800_000, equityResult.TotalFundEquityVnd);

        var eqA = equityResult.MemberEquities.Single(m => m.UserId == owner.Id);
        var eqB = equityResult.MemberEquities.Single(m => m.UserId == member.Id);

        // Expense 1: 100k distributed 60%/40% -> A: -60k (540k), B: -40k (360k)
        // Expense 2: 100k distributed 540/900=60%, 360/900=40% -> A: -60k (480k), B: -40k (320k)
        Assert.Equal(480_000, eqA.EquityVnd);
        Assert.Equal(320_000, eqB.EquityVnd);
        Assert.Equal(120_000, eqA.TotalSharedExpenseChargedVnd);
        Assert.Equal(80_000, eqB.TotalSharedExpenseChargedVnd);
    }

    [Fact]
    public async Task FreeFund_CompletedRequestMissingLedgerTransaction_FailsReconciliation()
    {
        // A completed request exists without a corresponding ledger debit transaction in treasury.
        // Audit must fail with fund_request_ledger_mismatch.
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("OwnerA");
        var s = V1FundMembershipTests.Service(f);
        var fundDto = await s.CreateAsync(owner.Id, new("MissingLedgerFund", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);

        var ownerWallet = await f.AddWalletAsync(owner.Id, "OwnerMain", true, balance: 1_000_000);
        await s.ContributeAsync(owner.Id, fundDto.Id,
            new(ownerWallet.Id, 500_000, "c-a", TargetedCorrectnessFixture.SecurityPin), default);

        // Manually inject a Completed V1FundRequest without any ledger transaction
        f.Db.V1FundRequests.Add(new V1FundRequest
        {
            FundId = fundDto.Id,
            RequestedByUserId = owner.Id,
            RecipientUserId = owner.Id,
            Kind = "Withdrawal",
            AmountVnd = 100_000,
            Purpose = "Ghost withdrawal",
            State = "Completed",
            IdempotencyKey = "ghost-wd"
        });
        await f.Db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            s.PreviewRefundAsync(owner.Id, fundDto.Id, null, default));
        Assert.Equal("fund_request_ledger_mismatch", ex.Code);
    }

    [Fact]
    public async Task FreeFund_MemberLeavesReceivesRefundRejoins_NewEpisodeStartsAtZeroAndCalculatesCorrectly()
    {
        // Real full service integration test:
        // A contributes 600k, B contributes 400k. Total = 1,000,000 VND.
        // B leaves the fund via service -> receives exactly 400,000 VND refund.
        // A spends 200k while B is gone -> A equity becomes 400k.
        // B is re-invited and joins again -> B's new episode starts at 0 VND, B bears 0 past expenses.
        // B contributes 300k -> A equity = 400k, B equity = 300k. Total = 700k.
        // Dissolve fund via service -> A gets 400k, B gets 300k, treasury balance is exactly 0 VND!
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("OwnerA");
        var member = await f.AddUserAsync("MemberB");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);

        var s = V1FundMembershipTests.Service(f);
        var fundDto = await s.CreateAsync(owner.Id, new("RejoinLifecycleFund", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);

        var invite1 = await s.InviteAsync(owner.Id, fundDto.Id, member.Id, default);
        await s.AcceptAsync(member.Id, invite1.Id, default);

        var walletA = await f.AddWalletAsync(owner.Id, "WalletA", true, balance: 2_000_000);
        var walletB = await f.AddWalletAsync(member.Id, "WalletB", true, balance: 2_000_000);

        // 1. Initial contributions
        await s.ContributeAsync(owner.Id, fundDto.Id, new(walletA.Id, 600_000, "c-a-init", TargetedCorrectnessFixture.SecurityPin), default);
        await s.ContributeAsync(member.Id, fundDto.Id, new(walletB.Id, 400_000, "c-b-init", TargetedCorrectnessFixture.SecurityPin), default);

        // 2. Member B leaves fund
        var leaveResult = await s.LeaveFundAsync(member.Id, fundDto.Id,
            new(TargetedCorrectnessFixture.SecurityPin, "leave-b-1"), default);
        Assert.Equal(400_000, leaveResult.RefundAmountVnd);
        Assert.Equal("Completed", leaveResult.Status);

        // Verify B's wallet received 400k refund
        var accB = await f.Db.LedgerAccounts.SingleAsync(a => a.WalletId == walletB.Id);
        // Original 2M - 400k contributed + 400k refunded = 2,000,000 VND
        Assert.Equal(2_000_000, accB.Balance);

        // 3. Owner A spends 200k on team expense while B is gone
        await s.CreateFundRequestAsync(owner.Id, fundDto.Id,
            new("Expense", 200_000, owner.Id, "Dinner while B absent", "exp-owner-alone", TargetedCorrectnessFixture.SecurityPin), default);

        // 4. Re-invite B and accept
        var invite2 = await s.InviteAsync(owner.Id, fundDto.Id, member.Id, default);
        await s.AcceptAsync(member.Id, invite2.Id, default);

        // Before B contributes, B's equity in the new episode is 0 VND!
        var fund = await f.Db.V1Funds.FirstAsync(fd => fd.Id == fundDto.Id);
        var treasury = await f.Db.V1FundTreasuries.FirstAsync(t => t.FundId == fund.Id);
        var intermediateEquity = await s.CalculateFreeFundEquityAsync(fund, treasury, null, default);

        var intB = intermediateEquity.MemberEquities.Single(m => m.UserId == member.Id);
        Assert.Equal(0, intB.EquityVnd);
        Assert.Equal(0, intB.TotalSharedExpenseChargedVnd);

        // 5. B contributes 300k
        await s.ContributeAsync(member.Id, fundDto.Id, new(walletB.Id, 300_000, "c-b-rejoin", TargetedCorrectnessFixture.SecurityPin), default);

        var finalEquity = await s.CalculateFreeFundEquityAsync(fund, treasury, null, default);
        Assert.Equal(700_000, finalEquity.CurrentTreasuryBalanceVnd);
        Assert.Equal(700_000, finalEquity.TotalFundEquityVnd);

        var eqA = finalEquity.MemberEquities.Single(m => m.UserId == owner.Id);
        var eqB = finalEquity.MemberEquities.Single(m => m.UserId == member.Id);

        Assert.Equal(400_000, eqA.EquityVnd);
        Assert.Equal(300_000, eqB.EquityVnd);

        // 6. Dissolve fund via service
        var dissolveResult = await s.DissolveFundAsync(owner.Id, fundDto.Id,
            new(TargetedCorrectnessFixture.SecurityPin, "dissolve-rejoin-fund"), default);

        Assert.Equal(700_000, dissolveResult.TotalRefundedVnd);
        Assert.Equal(0, dissolveResult.FinalBalanceVnd);

        var refA = dissolveResult.Refunds.Single(r => r.UserId == owner.Id);
        var refB = dissolveResult.Refunds.Single(r => r.UserId == member.Id);

        Assert.Equal(400_000, refA.RefundAmountVnd);
        Assert.Equal(300_000, refB.RefundAmountVnd);

        var finalTreasury = await f.Db.LedgerAccounts.SingleAsync(a => a.Id == treasury.LedgerAccountId);
        Assert.Equal(0, finalTreasury.Balance);
    }

    [Fact]
    public async Task FreeFund_OrphanWithdrawal_ThrowsFundOrphanOutflow()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("OwnerA");
        var member = await f.AddUserAsync("MemberB");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);

        var s = V1FundMembershipTests.Service(f);
        var fundDto = await s.CreateAsync(owner.Id, new("OrphanWthFund", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);

        var invite = await s.InviteAsync(owner.Id, fundDto.Id, member.Id, default);
        await s.AcceptAsync(member.Id, invite.Id, default);

        var ownerWallet = await f.AddWalletAsync(owner.Id, "OwnerMain", true, balance: 1_000_000);
        await s.ContributeAsync(owner.Id, fundDto.Id,
            new(ownerWallet.Id, 500_000, "contrib-a", TargetedCorrectnessFixture.SecurityPin), default);

        var fund = await f.Db.V1Funds.FirstAsync(fd => fd.Id == fundDto.Id);
        var treasury = await f.Db.V1FundTreasuries.FirstAsync(t => t.FundId == fund.Id);

        // Inject unmapped ledger withdrawal crediting treasury with no FinancialTransaction recipient
        var extAccount = new LedgerAccount
        {
            Code = "ORPHAN:WTH:CLEARING",
            Name = "Orphan Withdrawal Clearing",
            Type = LedgerAccountType.ExpenseClearing,
            Currency = "VND"
        };
        f.Db.LedgerAccounts.Add(extAccount);
        await f.Db.SaveChangesAsync();

        var ledgerService = new SpendWise.Infrastructure.Finance.LedgerService(f.Db, f.Clock);
        await ledgerService.PostAsync(
            treasury.LedgerAccountId,
            extAccount.Id,
            50_000,
            "VND",
            "V1FundWithdrawal",
            Guid.NewGuid(),
            "scope-orphan-wth",
            "key-orphan-wth",
            "Orphan withdrawal without recipient",
            default);

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            s.CalculateFreeFundEquityAsync(fund, treasury, null, default));
        Assert.Equal("fund_orphan_outflow", ex.Code);
    }

    [Fact]
    public async Task FreeFund_OrphanExpense_ThrowsFundOrphanOutflow()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("OwnerA");
        var member = await f.AddUserAsync("MemberB");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);

        var s = V1FundMembershipTests.Service(f);
        var fundDto = await s.CreateAsync(owner.Id, new("OrphanExpFund", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);

        var invite = await s.InviteAsync(owner.Id, fundDto.Id, member.Id, default);
        await s.AcceptAsync(member.Id, invite.Id, default);

        var ownerWallet = await f.AddWalletAsync(owner.Id, "OwnerMain", true, balance: 1_000_000);
        await s.ContributeAsync(owner.Id, fundDto.Id,
            new(ownerWallet.Id, 500_000, "contrib-a", TargetedCorrectnessFixture.SecurityPin), default);

        var fund = await f.Db.V1Funds.FirstAsync(fd => fd.Id == fundDto.Id);
        var treasury = await f.Db.V1FundTreasuries.FirstAsync(t => t.FundId == fund.Id);

        var extAccount = new LedgerAccount
        {
            Code = "ORPHAN:EXP:CLEARING",
            Name = "Orphan Expense Clearing",
            Type = LedgerAccountType.ExpenseClearing,
            Currency = "VND"
        };
        f.Db.LedgerAccounts.Add(extAccount);
        await f.Db.SaveChangesAsync();

        var ledgerService = new SpendWise.Infrastructure.Finance.LedgerService(f.Db, f.Clock);
        await ledgerService.PostAsync(
            treasury.LedgerAccountId,
            extAccount.Id,
            50_000,
            "VND",
            "V1FundExpense",
            Guid.NewGuid(), // Arbitrary ID not equal to fund.Id
            "scope-orphan-exp", // Not v1-fund-expense:{fund.Id:N}
            "key-orphan-exp",
            "Orphan expense without valid fund business link",
            default);

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            s.CalculateFreeFundEquityAsync(fund, treasury, null, default));
        Assert.Equal("fund_orphan_outflow", ex.Code);
    }

    [Fact]
    public async Task FreeFund_AntiTamper_UserSuppliedPurposeSequence_IgnoredAndThrowsIndeterminateOrder()
    {
        // Anti-tamper verification:
        // A user crafts a request with Purpose = "seq:1" or "order:1" to artificially manipulate equity execution sequence.
        // The financial engine MUST ignore user-supplied purpose strings and rely strictly on authoritative system posting evidence.
        // When concurrent events share identical system posting timestamps, it must detect the ambiguity and throw fund_indeterminate_order.
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("OwnerA");
        var member = await f.AddUserAsync("MemberB");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);

        var s = V1FundMembershipTests.Service(f);
        var fundDto = await s.CreateAsync(owner.Id, new("AntiTamperFund", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);

        var invite = await s.InviteAsync(owner.Id, fundDto.Id, member.Id, default);
        await s.AcceptAsync(member.Id, invite.Id, default);

        var ownerWallet = await f.AddWalletAsync(owner.Id, "OwnerMain", true, balance: 1_000_000);
        var memberWallet = await f.AddWalletAsync(member.Id, "MemberMain", true, balance: 1_000_000);

        await s.ContributeAsync(owner.Id, fundDto.Id,
            new(ownerWallet.Id, 600_000, "contrib-a", TargetedCorrectnessFixture.SecurityPin), default);
        await s.ContributeAsync(member.Id, fundDto.Id,
            new(memberWallet.Id, 400_000, "contrib-b", TargetedCorrectnessFixture.SecurityPin), default);

        // Member crafts forged sequence in Purpose
        var reqExp = await s.CreateFundRequestAsync(member.Id, fundDto.Id,
            new("Expense", 200_000, member.Id, "seq:1 - Forged sequence attempt", "req-exp-tamper", TargetedCorrectnessFixture.SecurityPin), default);
        await s.ReviewFundRequestAsync(owner.Id, reqExp.Id, true, "Approved", TargetedCorrectnessFixture.SecurityPin, default);

        var reqWth = await s.CreateFundRequestAsync(member.Id, fundDto.Id,
            new("Withdrawal", 100_000, member.Id, "seq:2 - Forged sequence attempt", "req-wth-tamper", TargetedCorrectnessFixture.SecurityPin), default);
        await s.ReviewFundRequestAsync(owner.Id, reqWth.Id, true, "Approved", TargetedCorrectnessFixture.SecurityPin, default);

        var fund = await f.Db.V1Funds.FirstAsync(fd => fd.Id == fundDto.Id);
        var treasury = await f.Db.V1FundTreasuries.FirstAsync(t => t.FundId == fund.Id);

        // Align both ledger transactions to the exact same system timestamp
        var baseTime = DateTime.UtcNow.AddMinutes(5);
        var txExp = await f.Db.LedgerTransactions.SingleAsync(tx => tx.IdempotencyScope == $"v1-fund-payout:{fund.Id:N}:{reqExp.Id:N}");
        var txWth = await f.Db.LedgerTransactions.SingleAsync(tx => tx.IdempotencyScope == $"v1-fund-payout:{fund.Id:N}:{reqWth.Id:N}");

        txExp.CreatedAt = baseTime;
        txExp.CompletedAt = baseTime;
        txWth.CreatedAt = baseTime;
        txWth.CompletedAt = baseTime;
        await f.Db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            s.CalculateFreeFundEquityAsync(fund, treasury, null, default));
        Assert.Equal("fund_indeterminate_order", ex.Code);
    }

[Fact]
    public async Task FreeFund_ThreeEvents_SameTimestamp_ThrowsFundIndeterminateOrder()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("OwnerA");
        var member = await f.AddUserAsync("MemberB");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);

        var s = V1FundMembershipTests.Service(f);
        var fundDto = await s.CreateAsync(owner.Id, new("ThreeSameTimeFund", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);

        var invite = await s.InviteAsync(owner.Id, fundDto.Id, member.Id, default);
        await s.AcceptAsync(member.Id, invite.Id, default);

        var ownerWallet = await f.AddWalletAsync(owner.Id, "OwnerMain", true, balance: 1_000_000);
        var memberWallet = await f.AddWalletAsync(member.Id, "MemberMain", true, balance: 1_000_000);

        await s.ContributeAsync(owner.Id, fundDto.Id,
            new(ownerWallet.Id, 600_000, "contrib-a", TargetedCorrectnessFixture.SecurityPin), default);
        await s.ContributeAsync(member.Id, fundDto.Id,
            new(memberWallet.Id, 400_000, "contrib-b", TargetedCorrectnessFixture.SecurityPin), default);

        var reqExp = await s.CreateFundRequestAsync(member.Id, fundDto.Id,
            new("Expense", 200_000, member.Id, "Dinner", "req-exp-3", TargetedCorrectnessFixture.SecurityPin), default);
        await s.ReviewFundRequestAsync(owner.Id, reqExp.Id, true, "Approved", TargetedCorrectnessFixture.SecurityPin, default);

        var reqWth = await s.CreateFundRequestAsync(member.Id, fundDto.Id,
            new("Withdrawal", 100_000, member.Id, "Personal withdrawal", "req-wth-3", TargetedCorrectnessFixture.SecurityPin), default);
        await s.ReviewFundRequestAsync(owner.Id, reqWth.Id, true, "Approved", TargetedCorrectnessFixture.SecurityPin, default);

        var fund = await f.Db.V1Funds.FirstAsync(fd => fd.Id == fundDto.Id);
        var treasury = await f.Db.V1FundTreasuries.FirstAsync(t => t.FundId == fund.Id);

        // Align 3 events (Member contribution, Expense, and Withdrawal) to the exact same system timestamp
        var sameTime = DateTime.UtcNow.AddMinutes(5);
        var contribB = await f.Db.V1FundContributions.SingleAsync(c => c.FundId == fund.Id && c.UserId == member.Id);
        var txContribB = await f.Db.LedgerTransactions.SingleAsync(tx => tx.Id == contribB.LedgerTransactionId);
        var txExp = await f.Db.LedgerTransactions.SingleAsync(tx => tx.IdempotencyScope == $"v1-fund-payout:{fund.Id:N}:{reqExp.Id:N}");
        var txWth = await f.Db.LedgerTransactions.SingleAsync(tx => tx.IdempotencyScope == $"v1-fund-payout:{fund.Id:N}:{reqWth.Id:N}");

        txContribB.CreatedAt = sameTime;
        txContribB.CompletedAt = sameTime;
        txExp.CreatedAt = sameTime;
        txExp.CompletedAt = sameTime;
        txWth.CreatedAt = sameTime;
        txWth.CompletedAt = sameTime;

        await f.Db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            s.CalculateFreeFundEquityAsync(fund, treasury, null, default));
        Assert.Equal("fund_indeterminate_order", ex.Code);
    }

    [Fact]
    public async Task FreeFund_RealPostingSequence_OrdersBySystemCompletedAt_WithoutModifyingIdempotencyKey()
    {
        // Scenario 1: Expense posted before Withdrawal (governed strictly by clock.UtcNow at PostAsync)
        {
            var clock1 = new MutableClock(new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc));
            await using var f1 = await TargetedCorrectnessFixture.CreateAsync(clock1);
            var owner = await f1.AddUserAsync("Owner1");
            var member = await f1.AddUserAsync("Member1");
            await V1FundMembershipTests.MakeFriends(f1, owner.Id, member.Id);

            var s1 = V1FundMembershipTests.Service(f1);
            var fundDto = await s1.CreateAsync(owner.Id, new("SeqExpFirst", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);

            var invite = await s1.InviteAsync(owner.Id, fundDto.Id, member.Id, default);
            await s1.AcceptAsync(member.Id, invite.Id, default);

            var ownerWallet = await f1.AddWalletAsync(owner.Id, "OwnerMain", true, balance: 1_000_000);
            var memberWallet = await f1.AddWalletAsync(member.Id, "MemberMain", true, balance: 1_000_000);

            clock1.UtcNow = clock1.UtcNow.AddMinutes(1);
            await s1.ContributeAsync(owner.Id, fundDto.Id,
                new(ownerWallet.Id, 600_000, "c-a", TargetedCorrectnessFixture.SecurityPin), default);

            clock1.UtcNow = clock1.UtcNow.AddMinutes(1);
            await s1.ContributeAsync(member.Id, fundDto.Id,
                new(memberWallet.Id, 400_000, "c-b", TargetedCorrectnessFixture.SecurityPin), default);

            // Expense created & reviewed first
            clock1.UtcNow = clock1.UtcNow.AddMinutes(1);
            var reqExp = await s1.CreateFundRequestAsync(member.Id, fundDto.Id,
                new("Expense", 200_000, member.Id, "Dinner", "req-exp-1", TargetedCorrectnessFixture.SecurityPin), default);
            clock1.UtcNow = clock1.UtcNow.AddMinutes(1);
            await s1.ReviewFundRequestAsync(owner.Id, reqExp.Id, true, "Approved", TargetedCorrectnessFixture.SecurityPin, default);

            // Withdrawal created & reviewed second
            clock1.UtcNow = clock1.UtcNow.AddMinutes(1);
            var reqWth = await s1.CreateFundRequestAsync(member.Id, fundDto.Id,
                new("Withdrawal", 100_000, member.Id, "Cash out", "req-wth-1", TargetedCorrectnessFixture.SecurityPin), default);
            clock1.UtcNow = clock1.UtcNow.AddMinutes(1);
            await s1.ReviewFundRequestAsync(owner.Id, reqWth.Id, true, "Approved", TargetedCorrectnessFixture.SecurityPin, default);

            var fund = await f1.Db.V1Funds.FirstAsync(fd => fd.Id == fundDto.Id);
            var treasury = await f1.Db.V1FundTreasuries.FirstAsync(t => t.FundId == fund.Id);

            var result = await s1.CalculateFreeFundEquityAsync(fund, treasury, null, default);
            Assert.Equal(700_000, result.CurrentTreasuryBalanceVnd);
            Assert.Equal(700_000, result.TotalFundEquityVnd);

            // Expense first:
            // Equity before expense: A=600k (60%), B=400k (40%). Expense=200k.
            // A pays 120k -> 480k. B pays 80k -> 320k.
            // Withdrawal second:
            // B withdraws 100k -> B has 220k. A has 480k.
            var eqA = result.MemberEquities.Single(m => m.UserId == owner.Id);
            var eqB = result.MemberEquities.Single(m => m.UserId == member.Id);
            Assert.Equal(480_000, eqA.EquityVnd);
            Assert.Equal(220_000, eqB.EquityVnd);
        }

        // Scenario 2: Withdrawal posted before Expense (governed strictly by clock.UtcNow at PostAsync)
        {
            var clock2 = new MutableClock(new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc));
            await using var f2 = await TargetedCorrectnessFixture.CreateAsync(clock2);
            var owner = await f2.AddUserAsync("Owner2");
            var member = await f2.AddUserAsync("Member2");
            await V1FundMembershipTests.MakeFriends(f2, owner.Id, member.Id);

            var s2 = V1FundMembershipTests.Service(f2);
            var fundDto = await s2.CreateAsync(owner.Id, new("SeqWthFirst", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);

            var invite = await s2.InviteAsync(owner.Id, fundDto.Id, member.Id, default);
            await s2.AcceptAsync(member.Id, invite.Id, default);

            var ownerWallet = await f2.AddWalletAsync(owner.Id, "OwnerMain", true, balance: 1_000_000);
            var memberWallet = await f2.AddWalletAsync(member.Id, "MemberMain", true, balance: 1_000_000);

            clock2.UtcNow = clock2.UtcNow.AddMinutes(1);
            await s2.ContributeAsync(owner.Id, fundDto.Id,
                new(ownerWallet.Id, 600_000, "c-a", TargetedCorrectnessFixture.SecurityPin), default);

            clock2.UtcNow = clock2.UtcNow.AddMinutes(1);
            await s2.ContributeAsync(member.Id, fundDto.Id,
                new(memberWallet.Id, 400_000, "c-b", TargetedCorrectnessFixture.SecurityPin), default);

            // Withdrawal created & reviewed first
            clock2.UtcNow = clock2.UtcNow.AddMinutes(1);
            var reqWth = await s2.CreateFundRequestAsync(member.Id, fundDto.Id,
                new("Withdrawal", 100_000, member.Id, "Cash out", "req-wth-2", TargetedCorrectnessFixture.SecurityPin), default);
            clock2.UtcNow = clock2.UtcNow.AddMinutes(1);
            await s2.ReviewFundRequestAsync(owner.Id, reqWth.Id, true, "Approved", TargetedCorrectnessFixture.SecurityPin, default);

            // Expense created & reviewed second
            clock2.UtcNow = clock2.UtcNow.AddMinutes(1);
            var reqExp = await s2.CreateFundRequestAsync(member.Id, fundDto.Id,
                new("Expense", 200_000, member.Id, "Dinner", "req-exp-2", TargetedCorrectnessFixture.SecurityPin), default);
            clock2.UtcNow = clock2.UtcNow.AddMinutes(1);
            await s2.ReviewFundRequestAsync(owner.Id, reqExp.Id, true, "Approved", TargetedCorrectnessFixture.SecurityPin, default);

            var fund = await f2.Db.V1Funds.FirstAsync(fd => fd.Id == fundDto.Id);
            var treasury = await f2.Db.V1FundTreasuries.FirstAsync(t => t.FundId == fund.Id);

            var result = await s2.CalculateFreeFundEquityAsync(fund, treasury, null, default);
            Assert.Equal(700_000, result.CurrentTreasuryBalanceVnd);
            Assert.Equal(700_000, result.TotalFundEquityVnd);

            // Withdrawal first:
            // B withdraws 100k -> B has 300k, A has 600k. Total = 900k.
            // Ratio: A = 600k/900k = 2/3 (66.6667%), B = 300k/900k = 1/3 (33.3333%).
            // Expense 200k second:
            // A pays 133,334 -> 466,666.
            // B pays 66,666 -> 233,334.
            var eqA = result.MemberEquities.Single(m => m.UserId == owner.Id);
            var eqB = result.MemberEquities.Single(m => m.UserId == member.Id);
            Assert.Equal(466_666, eqA.EquityVnd);
            Assert.Equal(233_334, eqB.EquityVnd);
        }
    }

    private sealed class MutableClock(DateTime utcNow) : SpendWise.Application.Common.Interfaces.IClock
    {
        public DateTime UtcNow { get; set; } = utcNow;
    }
}
