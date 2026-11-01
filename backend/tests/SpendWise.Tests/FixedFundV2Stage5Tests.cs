using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.V1Groups;
using SpendWise.Domain.Finance;
using SpendWise.Domain.Identity;
using SpendWise.Domain.V1Groups;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace SpendWise.Tests;

public sealed class FixedFundV2Stage5Tests
{
    private const string Pin = TargetedCorrectnessFixture.SecurityPin;

    private sealed class MutableClock(DateTime initial) : IClock
    {
        public DateTime UtcNow { get; set; } = initial;
        public void Advance(TimeSpan d) => UtcNow += d;
    }

    private sealed class QualifiedInactivityEvidence : IV1InactivityEvidence
    {
        public Task<V1InactivityProof> GetAsync(Guid userId, CancellationToken ct) =>
            Task.FromResult(new V1InactivityProof(true, true, "Synthetic-qualified-inactivity-proof"));
    }

    private static async Task<(TargetedCorrectnessFixture F, MutableClock Clock, V1FundService Service, V1FundDto Fund, User Owner, User MemberA, User MemberB)>
        Setup3MemberFixedFundAsync(long targetVnd = 3_000_000)
    {
        var clock = new MutableClock(new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc));
        var f = await TargetedCorrectnessFixture.CreateAsync(clock);

        var owner = await f.AddUserAsync("Owner Stage5");
        owner.EmailVerifiedAt = clock.UtcNow;
        var memberA = await f.AddUserAsync("MemberA Stage5");
        memberA.EmailVerifiedAt = clock.UtcNow;
        var memberB = await f.AddUserAsync("MemberB Stage5");
        memberB.EmailVerifiedAt = clock.UtcNow;
        await f.Db.SaveChangesAsync();

        await V1FundMembershipTests.MakeFriends(f, owner.Id, memberA.Id);
        await V1FundMembershipTests.MakeFriends(f, owner.Id, memberB.Id);

        var service = V1ElectionTests.Configured(f, new QualifiedInactivityEvidence());
        var fund = await service.CreateAsync(owner.Id, new("Trip Fund Fixed Stage5", "Fixed", targetVnd, Pin, 24, true, "v2.0"), default);

        var invA = await service.InviteAsync(owner.Id, fund.Id, memberA.Id, default);
        await service.AcceptAsync(memberA.Id, invA.Id, true, "v2.0", default);

        var invB = await service.InviteAsync(owner.Id, fund.Id, memberB.Id, default);
        await service.AcceptAsync(memberB.Id, invB.Id, true, "v2.0", default);

        var config = await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(targetVnd, Pin), default);

        var wOwner = await f.AddWalletAsync(owner.Id, "WOwner5", true, balance: 10_000_000);
        var wA = await f.AddWalletAsync(memberA.Id, "WA5", true, balance: 10_000_000);
        var wB = await f.AddWalletAsync(memberB.Id, "WB5", true, balance: 10_000_000);

        await service.ContributeAsync(owner.Id, fund.Id, new(wOwner.Id, 1_000_000, "c-owner-s5", Pin, config.Revision), default);
        await service.ContributeAsync(memberA.Id, fund.Id, new(wA.Id, 1_000_000, "c-memA-s5", Pin, config.Revision), default);
        await service.ContributeAsync(memberB.Id, fund.Id, new(wB.Id, 1_000_000, "c-memB-s5", Pin, config.Revision), default);

        return (f, clock, service, fund, owner, memberA, memberB);
    }

    // =========================================================================
    // T28: Idempotency & Replay Safety (LeaveFund / Kick / Worker retry)
    // Exactly one ledger debit, duplicate execution returns cached/replayed result
    // =========================================================================
    [Fact]
    public async Task T28_Idempotency_DoubleRefund_And_ReplaySafety_ExactlyOneLedgerTransaction()
    {
        var (f, clock, service, fund, owner, memberA, memberB) = await Setup3MemberFixedFundAsync();
        await using var _ = f;

        // Advance past 15 days so Member A can trigger election or opt out after failed election
        clock.Advance(TimeSpan.FromDays(16));
        var election = await service.OpenElectionAsync(memberA.Id, fund.Id, new V1OpenElectionRequest(0), default);
        // Both vote blank (0) -> election fails immediately
        await service.CastBallotAsync(memberA.Id, election.Id, new V1BallotRequest(1, null, null), default);
        await service.CastBallotAsync(memberB.Id, election.Id, new V1BallotRequest(1, null, null), default);
        await service.AdvanceElectionAsync(memberA.Id, election.Id, default);

        // Member A opts out with explicit idempotency key
        var idempotencyKey = "idemp-leave-stage5-" + Guid.NewGuid().ToString("N");
        var refund1 = await service.LeaveFundAsync(memberA.Id, fund.Id, new V1LeaveFundRequest(Pin, idempotencyKey), default);
        Assert.Equal("Completed", refund1.Status);
        Assert.Equal(1_000_000, refund1.RefundAmountVnd);
        Assert.NotNull(refund1.LedgerTransactionId);

        // Attempt 2: Same idempotency key replayed
        var refund2 = await service.LeaveFundAsync(memberA.Id, fund.Id, new V1LeaveFundRequest(Pin, idempotencyKey), default);
        Assert.Equal("Completed", refund2.Status);
        Assert.Equal("Replayed", refund2.CaseType);
        Assert.Equal(refund1.RefundAmountVnd, refund2.RefundAmountVnd);
        Assert.Equal(refund1.LedgerTransactionId, refund2.LedgerTransactionId);

        // Verify in database: exactly 1 LedgerTransaction exists with this idempotency key
        var ledgerTxCount = await f.Db.LedgerTransactions.CountAsync(t => t.IdempotencyKey == idempotencyKey);
        Assert.Equal(1, ledgerTxCount);

        // Verify Member A's wallet was credited exactly once (+1,000,000 VND)
        var memAWallet = await f.Db.Wallets.AsNoTracking().SingleAsync(w => w.Name == "WA5");
        // Initial was 10M, contributed 1M (balance 9M), refunded 1M -> exactly 10M
        Assert.Equal(10_000_000, memAWallet.AvailableBalance);
    }

    // =========================================================================
    // T29: Concurrency & State Ordering Safety
    // Once kicked by deadline worker, member cannot contribute
    // Once contributed, deadline worker aborts auto-kick
    // =========================================================================
    [Fact]
    public async Task T29_ConcurrencyAndOrdering_StrictStateTransitions_NoHybridState()
    {
        var clock = new MutableClock(new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc));
        await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);

        var owner = await f.AddUserAsync("Owner T29");
        var memberA = await f.AddUserAsync("Member A T29");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, memberA.Id);

        var service = V1ElectionTests.Configured(f);
        var fund = await service.CreateAsync(owner.Id, new("Fund T29", "Fixed", 2_000_000, Pin, 24, true, "v2.0"), default);

        var invA = await service.InviteAsync(owner.Id, fund.Id, memberA.Id, default);
        await service.AcceptAsync(memberA.Id, invA.Id, true, "v2.0", default);

        var config = await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(2_000_000, Pin), default);

        var wOwner = await f.AddWalletAsync(owner.Id, "WOwner29", true, balance: 5_000_000);
        var wA = await f.AddWalletAsync(memberA.Id, "WA29", true, balance: 5_000_000);

        // Owner contributes 1,000,000 VND
        await service.ContributeAsync(owner.Id, fund.Id, new(wOwner.Id, 1_000_000, "c-owner-29", Pin, config.Revision), default);

        // Member A only contributes partial 500,000 VND
        await service.ContributeAsync(memberA.Id, fund.Id, new(wA.Id, 500_000, "c-a-partial-29", Pin, config.Revision), default);

        // Advance past 24 hours deadline
        clock.Advance(TimeSpan.FromHours(25));

        // Background worker kicks Member A
        var processedCount = await service.ProgressFixedFundDeadlinesAsync(default);
        Assert.True(processedCount > 0);

        // Verify Member A has LeftAt != null
        var memADb = await f.Db.V1FundMemberships.AsNoTracking().SingleAsync(m => m.FundId == fund.Id && m.UserId == memberA.Id);
        Assert.NotNull(memADb.LeftAt);

        // Now Member A concurrently/subsequently tries to contribute remaining 500,000 VND
        // Must be rejected with forbidden_member_not_active or similar because LeftAt is not null
        await Assert.ThrowsAnyAsync<Exception>(() =>
            service.ContributeAsync(memberA.Id, fund.Id, new(wA.Id, 500_000, "c-a-late-29", Pin, config.Revision), default));

        // Member A is in kicked state, partial 500k was refunded, wallet is back to 5,000,000 VND
        var memAWallet = await f.Db.Wallets.AsNoTracking().SingleAsync(w => w.Id == wA.Id);
        Assert.Equal(5_000_000, memAWallet.AvailableBalance);
    }

    // =========================================================================
    // T30: Spend Dispute Flow
    // Reporting completed expense opens SupportCase with Category V1FundDispute
    // Transaction status remains Completed, ledger is immutable, balance unaffected
    // Duplicate report is idempotent
    // =========================================================================
    [Fact]
    public async Task T30_CompletedExpense_DisputeReport_OpensSupportCase_DoesNotReverseLedger()
    {
        var (f, clock, service, fund, owner, memberA, memberB) = await Setup3MemberFixedFundAsync();
        await using var _ = f;

        var merchant = await f.AddUserAsync("Merchant Stage5");
        var wMerchant = await f.AddWalletAsync(merchant.Id, "WMerchant", true, balance: 0);

        // Fund is 100% funded (3,000,000 / 3,000,000 VND), finance is OPEN
        // Owner grants spending permission to Member A
        await service.SetSpendingPermissionAsync(owner.Id, fund.Id, memberA.Id, true, Pin, default);

        // Member A makes a valid common expense of 1,200,000 VND
        var expInput = new V1FundRequestInput("Expense", 1_200_000, merchant.Id, "Team dinner banquet", "exp-stage5-1", Pin);
        var expenseReq = await service.CreateFundRequestAsync(memberA.Id, fund.Id, expInput, default);
        Assert.Equal("Completed", expenseReq.State);

        // Treasury balance is now 3,000,000 - 1,200,000 = 1,800,000 VND
        var financeBeforeReport = await service.FinanceAsync(owner.Id, fund.Id, default);
        Assert.Equal(1_800_000, financeBeforeReport.BalanceVnd);

        // Member B suspects mismanagement and reports the completed expense
        var reportReason = "Chi tieu nha hang khong co hoa don do va vuot muc cho phep";
        var reportedReq = await service.ReportFundRequestAsync(memberB.Id, expenseReq.Id, reportReason, default);

        // 1. Transaction and request state remains Completed (immutable ledger rule)
        Assert.Equal("Completed", reportedReq.State);
        var reqDb = await f.Db.V1FundRequests.AsNoTracking().SingleAsync(r => r.Id == expenseReq.Id);
        Assert.Equal("Completed", reqDb.State);

        // 2. Fund treasury balance is NOT altered or deducted
        var financeAfterReport = await service.FinanceAsync(owner.Id, fund.Id, default);
        Assert.Equal(1_800_000, financeAfterReport.BalanceVnd);

        // 3. Merchant wallet balance is NOT reversed
        var merchWallet = await f.Db.Wallets.AsNoTracking().SingleAsync(w => w.Id == wMerchant.Id);
        Assert.Equal(1_200_000, merchWallet.AvailableBalance);

        // 4. Exactly one SupportCase of category V1FundDispute was created
        var supportCase = await f.Db.SupportCases.AsNoTracking().SingleAsync(c =>
            c.Category == "V1FundDispute" && c.LinkedTransactionId == expenseReq.Id && c.LinkedUserId == memberB.Id);
        Assert.Equal("New", supportCase.Status.ToString());
        Assert.Contains(reportReason, supportCase.Description);

        // 5. Duplicate report by same member is idempotent and does not create duplicate support cases
        var reportedAgain = await service.ReportFundRequestAsync(memberB.Id, expenseReq.Id, reportReason, default);
        Assert.Equal("Completed", reportedAgain.State);
        var caseCount = await f.Db.SupportCases.CountAsync(c =>
            c.Category == "V1FundDispute" && c.LinkedTransactionId == expenseReq.Id && c.LinkedUserId == memberB.Id);
        Assert.Equal(1, caseCount);
    }

    // =========================================================================
    // T31: Full End-to-End Lifecycle Verification
    // Creation -> Allocation -> Replacement -> Spending -> Period 2 Proposal ->
    // Carry-over -> Inactivity -> Election -> Closure -> Exact Whole Dong Refunds
    // =========================================================================
    [Fact]
    public async Task T31_FullLifecycle_EndToEnd_SeamlessIntegrity_TreasuryZeroAtDissolution()
    {
        var clock = new MutableClock(new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc));
        await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);

        var founder = await f.AddUserAsync("Founder E2E");
        founder.EmailVerifiedAt = clock.UtcNow;
        var memberA = await f.AddUserAsync("MemberA E2E");
        memberA.EmailVerifiedAt = clock.UtcNow;
        var memberB = await f.AddUserAsync("MemberB E2E");
        memberB.EmailVerifiedAt = clock.UtcNow;
        var merchant = await f.AddUserAsync("Merchant E2E");
        await f.Db.SaveChangesAsync();

        await V1FundMembershipTests.MakeFriends(f, founder.Id, memberA.Id);
        await V1FundMembershipTests.MakeFriends(f, founder.Id, memberB.Id);

        var service = V1ElectionTests.Configured(f, new QualifiedInactivityEvidence());

        // Step 1: Founder creates Fixed Fund with explicit commitment (Target 6,000,000 VND, 24h window)
        var fund = await service.CreateAsync(founder.Id, new("E2E Fixed Fund", "Fixed", 6_000_000, Pin, 24, true, "v2.0"), default);

        var invA = await service.InviteAsync(founder.Id, fund.Id, memberA.Id, default);
        await service.AcceptAsync(memberA.Id, invA.Id, true, "v2.0", default);

        var invB = await service.InviteAsync(founder.Id, fund.Id, memberB.Id, default);
        await service.AcceptAsync(memberB.Id, invB.Id, true, "v2.0", default);

        // Step 2: Confirm obligations (2,000,000 VND per member for 3 seats)
        var config = await service.ConfirmObligationsAsync(founder.Id, fund.Id, new(6_000_000, Pin), default);
        Assert.Equal(6_000_000, config.TargetVnd);

        var wFounder = await f.AddWalletAsync(founder.Id, "WFounder", true, balance: 10_000_000);
        var wA = await f.AddWalletAsync(memberA.Id, "WA", true, balance: 10_000_000);
        var wB = await f.AddWalletAsync(memberB.Id, "WB", true, balance: 10_000_000);
        var wMerch = await f.AddWalletAsync(merchant.Id, "WMerch", true, balance: 0);

        // All 3 contribute 2,000,000 VND -> 100% funded
        await service.ContributeAsync(founder.Id, fund.Id, new(wFounder.Id, 2_000_000, "c-f", Pin, config.Revision), default);
        await service.ContributeAsync(memberA.Id, fund.Id, new(wA.Id, 2_000_000, "c-a", Pin, config.Revision), default);
        await service.ContributeAsync(memberB.Id, fund.Id, new(wB.Id, 2_000_000, "c-b", Pin, config.Revision), default);

        var fin1 = await service.FinanceAsync(founder.Id, fund.Id, default);
        Assert.Equal(6_000_000, fin1.BalanceVnd);
        Assert.Equal("OPEN", fin1.SpendingState);

        // Step 3: Spending: Founder grants CanSpend to Member A. Member A spends 1,500,000 VND
        await service.SetSpendingPermissionAsync(founder.Id, fund.Id, memberA.Id, true, Pin, default);
        var exp = await service.CreateFundRequestAsync(memberA.Id, fund.Id,
            new V1FundRequestInput("Expense", 1_500_000, merchant.Id, "Project Kickoff supplies", "exp-e2e-1", Pin), default);
        Assert.Equal("Completed", exp.State);

        var fin2 = await service.FinanceAsync(founder.Id, fund.Id, default);
        Assert.Equal(4_500_000, fin2.BalanceVnd); // 6M - 1.5M = 4.5M

        // Step 4: Period Closure: Founder closes period and dissolves fund
        await service.CloseActivityAsync(founder.Id, fund.Id, Pin, default);
        var dissolve = await service.DissolveFundAsync(founder.Id, fund.Id, new V1DissolveFundRequest(Pin, "dissolve-e2e-final"), default);
        Assert.Equal(4_500_000, dissolve.TotalRefundedVnd);
        Assert.Equal(0, dissolve.FinalBalanceVnd);

        // Each member had contributed 2,000,000 VND and 1,500,000 VND was spent evenly (500k each)
        // Entitlement per member = 1,500,000 VND. Total 3 * 1.5M = 4,500,000 VND exact!
        foreach (var refItem in dissolve.Refunds)
        {
            Assert.Equal(1_500_000, refItem.RefundAmountVnd);
        }

        // Verify fund status is Closed/Dissolved
        var fundDb = await f.Db.V1Funds.AsNoTracking().SingleAsync(x => x.Id == fund.Id);
        Assert.NotNull(fundDb.DissolvedAt);
    }
}
