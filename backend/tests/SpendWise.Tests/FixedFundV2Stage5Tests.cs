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
    // Worker retries produce 0 duplicate transactions
    // =========================================================================
    [Fact]
    public async Task T28_Idempotency_DoubleRefund_And_WorkerRetry_ZeroDuplicateLedgerEntries()
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

        // 1. Member A opts out with explicit idempotency key
        var idempotencyKey = "idemp-leave-stage5-" + Guid.NewGuid().ToString("N");
        var refund1 = await service.LeaveFundAsync(memberA.Id, fund.Id, new V1LeaveFundRequest(Pin, idempotencyKey), default);
        Assert.Equal("Completed", refund1.Status);
        Assert.Equal(1_000_000, refund1.RefundAmountVnd);
        Assert.NotNull(refund1.LedgerTransactionId);

        // 2. Replay: Same idempotency key replayed
        var refund2 = await service.LeaveFundAsync(memberA.Id, fund.Id, new V1LeaveFundRequest(Pin, idempotencyKey), default);
        Assert.Equal("Completed", refund2.Status);
        Assert.Equal("Replayed", refund2.CaseType);
        Assert.Equal(refund1.RefundAmountVnd, refund2.RefundAmountVnd);
        Assert.Equal(refund1.LedgerTransactionId, refund2.LedgerTransactionId);

        // 3. Worker retry: Run background deadline worker multiple times
        var workerRuns1 = await service.ProgressFixedFundDeadlinesAsync(default);
        var workerRuns2 = await service.ProgressFixedFundDeadlinesAsync(default);

        // Verify in database: exactly 1 LedgerTransaction exists with this idempotency key
        var ledgerTxCount = await f.Db.LedgerTransactions.CountAsync(t => t.IdempotencyKey == idempotencyKey);
        Assert.Equal(1, ledgerTxCount);

        // Verify total LedgerTransactions of type V1FundRefund for this fund is exactly 1
        var totalRefundTxs = await f.Db.LedgerTransactions.CountAsync(t => t.BusinessType == "V1FundRefund" && t.BusinessId == fund.Id);
        Assert.Equal(1, totalRefundTxs);

        // Verify Member A's wallet was credited exactly once (+1,000,000 VND)
        var memAWallet = await f.Db.Wallets.AsNoTracking().SingleAsync(w => w.Name == "WA5");
        Assert.Equal(10_000_000, memAWallet.AvailableBalance);
    }

    // =========================================================================
    // T29: Concurrency & State Ordering Safety
    // Simultaneous worker execution and contribution request:
    // Atomic state transition: either worker auto-kicks first and rejects contribution,
    // or contribution succeeds first and worker aborts auto-kick.
    // Strict assertion on error code; no hybrid state.
    // =========================================================================
    [Fact]
    public async Task T29_ConcurrencyAndOrdering_SimultaneousDeadlineWorkerAndContribution_StrictStateIntegrity()
    {
        var clock = new MutableClock(new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc));
        await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);

        var owner = await f.AddUserAsync("Owner T29");
        var memberA = await f.AddUserAsync("Member A T29");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, memberA.Id);

        var service1 = V1ElectionTests.Configured(f);
        var service2 = V1ElectionTests.Configured(f);

        var fund = await service1.CreateAsync(owner.Id, new("Fund T29", "Fixed", 2_000_000, Pin, 24, true, "v2.0"), default);

        var invA = await service1.InviteAsync(owner.Id, fund.Id, memberA.Id, default);
        await service1.AcceptAsync(memberA.Id, invA.Id, true, "v2.0", default);

        var config = await service1.ConfirmObligationsAsync(owner.Id, fund.Id, new(2_000_000, Pin), default);

        var wOwner = await f.AddWalletAsync(owner.Id, "WOwner29", true, balance: 5_000_000);
        var wA = await f.AddWalletAsync(memberA.Id, "WA29", true, balance: 5_000_000);

        // Owner contributes 1,000,000 VND
        await service1.ContributeAsync(owner.Id, fund.Id, new(wOwner.Id, 1_000_000, "c-owner-29", Pin, config.Revision), default);

        // Member A only contributes partial 500,000 VND
        await service1.ContributeAsync(memberA.Id, fund.Id, new(wA.Id, 500_000, "c-a-partial-29", Pin, config.Revision), default);

        // Advance clock to exactly at/after deadline
        clock.Advance(TimeSpan.FromHours(25));

        // Simultaneous execution:
        // Worker task executing deadline processing vs Member A concurrently attempting to contribute remaining 500,000 VND
        Exception? contributeEx = null;
        var workerTask = Task.Run(async () => await service1.ProgressFixedFundDeadlinesAsync(default));
        var contributeTask = Task.Run(async () =>
        {
            try
            {
                await service2.ContributeAsync(memberA.Id, fund.Id,
                    new(wA.Id, 500_000, "c-a-late-concurrent", Pin, config.Revision), default);
            }
            catch (Exception ex)
            {
                contributeEx = ex;
            }
        });

        await Task.WhenAll(workerTask, contributeTask);

        // Evaluate the serialized outcome:
        var memADb = await f.Db.V1FundMemberships.AsNoTracking().SingleAsync(m => m.FundId == fund.Id && m.UserId == memberA.Id);

        if (memADb.LeftAt != null)
        {
            // Case 1: Worker executed first and Auto-Kicked Member A
            // Member A must have had their late contribution rejected with specific exception
            Assert.NotNull(contributeEx);
            var appEx = Assert.IsAssignableFrom<AppException>(contributeEx);
            Assert.Contains(appEx.Code, new[] { "fund_not_found", "forbidden_member_not_active", "fund_obligation_not_found", "conflict_member_departed" });

            // Exactly 500,000 VND was refunded to Member A
            var memAWallet = await f.Db.Wallets.AsNoTracking().SingleAsync(w => w.Id == wA.Id);
            Assert.Equal(5_000_000, memAWallet.AvailableBalance);

            // Fund has remaining balance 1,000,000 VND (Owner's share)
            var fin = await service1.FinanceAsync(owner.Id, fund.Id, default);
            Assert.Equal(1_000_000, fin.BalanceVnd);
        }
        else
        {
            // Case 2: Contribution was serialized first and succeeded
            Assert.Null(contributeEx);
            Assert.Null(memADb.LeftAt);
            var members = await service1.MembersAsync(owner.Id, fund.Id, default);
            var mADto = Assert.Single(members, m => m.UserId == memberA.Id);
            Assert.True(mADto.IndividualContributionCompleted);

            // Fund is fully funded at 2,000,000 VND
            var fin = await service1.FinanceAsync(owner.Id, fund.Id, default);
            Assert.Equal(2_000_000, fin.BalanceVnd);
        }

        // Strict Mutual Exclusion Assertion: NEVER a hybrid state (cannot be both kicked and credited)
        var totalContributionsA = await f.Db.V1FundContributions.Where(c => c.FundId == fund.Id && c.UserId == memberA.Id).SumAsync(c => c.AmountVnd);
        Assert.False(memADb.LeftAt != null && totalContributionsA == 1_000_000, "Invariant violation: Member was kicked yet registered full contribution!");
    }

    // =========================================================================
    // T30: Spend Dispute Flow
    // Reporting completed expense opens SupportCase with Category V1FundDispute
    // Transaction status remains Completed, ledger is immutable, balance unaffected
    // FinancialTransaction.LedgerTransactionId & LedgerTransaction verified
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

        // Verify the FinancialTransaction and linked LedgerTransaction before dispute
        var finTxBefore = await f.Db.FinancialTransactions.AsNoTracking().SingleAsync(t => t.RelatedBusinessId == expenseReq.Id);
        Assert.Equal(FinancialTransactionStatus.Completed, finTxBefore.Status);
        Assert.NotEqual(Guid.Empty, finTxBefore.LedgerTransactionId);

        var ledgerTxBefore = await f.Db.LedgerTransactions.AsNoTracking().SingleAsync(lt => lt.Id == finTxBefore.LedgerTransactionId);
        Assert.Equal(LedgerTransactionStatus.Completed, ledgerTxBefore.Status);

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

        // 2. FinancialTransaction and LedgerTransaction remain unchanged with Status = Completed
        var finTxAfter = await f.Db.FinancialTransactions.AsNoTracking().SingleAsync(t => t.RelatedBusinessId == expenseReq.Id);
        Assert.Equal(FinancialTransactionStatus.Completed, finTxAfter.Status);
        Assert.Equal(finTxBefore.LedgerTransactionId, finTxAfter.LedgerTransactionId);

        var ledgerTxAfter = await f.Db.LedgerTransactions.AsNoTracking().SingleAsync(lt => lt.Id == finTxAfter.LedgerTransactionId);
        Assert.Equal(LedgerTransactionStatus.Completed, ledgerTxAfter.Status);

        // 3. Fund treasury balance is NOT altered or deducted
        var financeAfterReport = await service.FinanceAsync(owner.Id, fund.Id, default);
        Assert.Equal(1_800_000, financeAfterReport.BalanceVnd);

        // 4. Merchant wallet balance is NOT reversed
        var merchWallet = await f.Db.Wallets.AsNoTracking().SingleAsync(w => w.Id == wMerchant.Id);
        Assert.Equal(1_200_000, merchWallet.AvailableBalance);

        // 5. Exactly one SupportCase of category V1FundDispute was created
        var supportCase = await f.Db.SupportCases.AsNoTracking().SingleAsync(c =>
            c.Category == "V1FundDispute" && c.LinkedTransactionId == expenseReq.Id && c.LinkedUserId == memberB.Id);
        Assert.Equal("New", supportCase.Status.ToString());
        Assert.Contains(reportReason, supportCase.Description);

        // 6. Duplicate report by same member is idempotent and does not create duplicate support cases
        var reportedAgain = await service.ReportFundRequestAsync(memberB.Id, expenseReq.Id, reportReason, default);
        Assert.Equal("Completed", reportedAgain.State);
        var caseCount = await f.Db.SupportCases.CountAsync(c =>
            c.Category == "V1FundDispute" && c.LinkedTransactionId == expenseReq.Id && c.LinkedUserId == memberB.Id);
        Assert.Equal(1, caseCount);

        // 7. Ledger records still intact after duplicate report
        var finTxDuplicate = await f.Db.FinancialTransactions.AsNoTracking().SingleAsync(t => t.RelatedBusinessId == expenseReq.Id);
        Assert.Equal(finTxBefore.LedgerTransactionId, finTxDuplicate.LedgerTransactionId);
    }

    // =========================================================================
    // T31: True Comprehensive End-to-End Multi-Period Lifecycle Verification
    // Cycle 1: Creation -> Obligation Snapshot -> Contributions -> Spending (CanSpend) ->
    // Dispute Reporting -> Cycle 1 Close -> Period 2 Proposal -> Obligation Consent ->
    // Opt-out Refund for non-consenting member -> Proposal Settlement ->
    // Owner Inactivity (16 days) -> Election -> Candidate PIN Confirmation ->
    // New Owner nhậm chức -> Period 2 Closure & Dissolution ->
    // Exact whole-dong refunds according to entitlement -> Final Treasury Balance = 0 VND.
    // =========================================================================
    [Fact]
    public async Task T31_MultiPeriod_Dispute_CarryOver_Election_FullLifecycle_EndToEnd()
    {
        var clock = new MutableClock(new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc));
        await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);

        var founder = await f.AddUserAsync("Founder Full E2E");
        founder.EmailVerifiedAt = clock.UtcNow;
        var memberA = await f.AddUserAsync("MemberA Full E2E");
        memberA.EmailVerifiedAt = clock.UtcNow;
        var memberB = await f.AddUserAsync("MemberB Full E2E");
        memberB.EmailVerifiedAt = clock.UtcNow;
        var merchant = await f.AddUserAsync("Merchant Full E2E");
        await f.Db.SaveChangesAsync();

        await V1FundMembershipTests.MakeFriends(f, founder.Id, memberA.Id);
        await V1FundMembershipTests.MakeFriends(f, founder.Id, memberB.Id);

        var service = V1ElectionTests.Configured(f, new QualifiedInactivityEvidence());

        // ---------------------------------------------------------------------
        // PERIOD 1: Create fund (Target 6,000,000 VND, 3 members, 24h duration)
        // ---------------------------------------------------------------------
        var fund = await service.CreateAsync(founder.Id, new("MultiCycle Fixed Fund", "Fixed", 6_000_000, Pin, 24, true, "v2.0"), default);

        var invA = await service.InviteAsync(founder.Id, fund.Id, memberA.Id, default);
        await service.AcceptAsync(memberA.Id, invA.Id, true, "v2.0", default);

        var invB = await service.InviteAsync(founder.Id, fund.Id, memberB.Id, default);
        await service.AcceptAsync(memberB.Id, invB.Id, true, "v2.0", default);

        var config = await service.ConfirmObligationsAsync(founder.Id, fund.Id, new(6_000_000, Pin), default);
        Assert.Equal(6_000_000, config.TargetVnd);

        var wFounder = await f.AddWalletAsync(founder.Id, "WFounder", true, balance: 10_000_000);
        var wA = await f.AddWalletAsync(memberA.Id, "WA", true, balance: 10_000_000);
        var wB = await f.AddWalletAsync(memberB.Id, "WB", true, balance: 10_000_000);
        var wMerch = await f.AddWalletAsync(merchant.Id, "WMerch", true, balance: 0);

        // All 3 contribute 2,000,000 VND
        await service.ContributeAsync(founder.Id, fund.Id, new(wFounder.Id, 2_000_000, "c-f1", Pin, config.Revision), default);
        await service.ContributeAsync(memberA.Id, fund.Id, new(wA.Id, 2_000_000, "c-a1", Pin, config.Revision), default);
        await service.ContributeAsync(memberB.Id, fund.Id, new(wB.Id, 2_000_000, "c-b1", Pin, config.Revision), default);

        var fin1 = await service.FinanceAsync(founder.Id, fund.Id, default);
        Assert.Equal(6_000_000, fin1.BalanceVnd);
        Assert.Equal("OPEN", fin1.SpendingState);

        // Spending with CanSpend: Founder grants Member A spending permission
        await service.SetSpendingPermissionAsync(founder.Id, fund.Id, memberA.Id, true, Pin, default);
        var exp1 = await service.CreateFundRequestAsync(memberA.Id, fund.Id,
            new V1FundRequestInput("Expense", 1_500_000, merchant.Id, "Team supplies Period 1", "exp-e2e-p1", Pin), default);
        Assert.Equal("Completed", exp1.State);

        var finAfterSpend = await service.FinanceAsync(founder.Id, fund.Id, default);
        Assert.Equal(4_500_000, finAfterSpend.BalanceVnd); // 6M - 1.5M = 4.5M

        // Dispute reporting: Member B reports completed expense
        var disputeReq = await service.ReportFundRequestAsync(memberB.Id, exp1.Id, "Kiem toan chung tu chi tieu", default);
        Assert.Equal("Completed", disputeReq.State);
        var supportCase = await f.Db.SupportCases.AsNoTracking().SingleAsync(c => c.Category == "V1FundDispute" && c.LinkedTransactionId == exp1.Id);
        Assert.Equal("New", supportCase.Status.ToString());

        // ---------------------------------------------------------------------
        // PERIOD TRANSITION: Close Period 1, Propose Period 2 (Target 4,000,000 VND)
        // ---------------------------------------------------------------------
        await service.CloseActivityAsync(founder.Id, fund.Id, Pin, default);
        var proposal = await service.ProposeActivityAsync(founder.Id, fund.Id, new(4_000_000, "p2-proposal", Pin), default);

        // Snapshot entitlements: Each member has (2M contributed - 500k shared expense) = 1,500,000 VND carry-over
        var voters = await f.Db.V1ActivityVoters.Where(v => v.ProposalId == proposal.Id).ToListAsync();
        Assert.Equal(3, voters.Count);
        Assert.All(voters, v => Assert.Equal(1_500_000, v.CarryOverVnd));

        // Founder and Member A vote Yes, Member B votes No (opts out)
        // Member B immediately receives 1,500,000 VND refund and departs!
        await service.VoteActivityAsync(founder.Id, proposal.Id, true, default);
        await service.VoteActivityAsync(memberA.Id, proposal.Id, true, default);
        await service.VoteActivityAsync(memberB.Id, proposal.Id, false, default);

        var memBWallet = await f.Db.Wallets.AsNoTracking().SingleAsync(w => w.Id == wB.Id);
        // Initial was 10M, contributed 2M (balance 8M), refunded 1.5M -> 9.5M
        Assert.Equal(9_500_000, memBWallet.AvailableBalance);

        var memBDb = await f.Db.V1FundMemberships.AsNoTracking().SingleAsync(m => m.FundId == fund.Id && m.UserId == memberB.Id);
        Assert.NotNull(memBDb.LeftAt);

        // Remaining active members (Founder and Member A): Target is 4M for 2 seats = 2M each.
        // Carry-over is 1.5M each -> new obligation is 2M each. Both confirm obligation consent!
        await service.ConfirmActivityObligationAsync(founder.Id, proposal.Id, new V1ConfirmActivityObligationRequest(2_000_000, 24, Pin), default);
        await service.ConfirmActivityObligationAsync(memberA.Id, proposal.Id, new V1ConfirmActivityObligationRequest(2_000_000, 24, Pin), default);

        // Settle proposal -> Fund enters Period 2!
        clock.Advance(TimeSpan.FromHours(1));
        await service.SettleActivityAsync(founder.Id, proposal.Id, Pin, default);

        var finP2 = await service.FinanceAsync(founder.Id, fund.Id, default);
        Assert.Equal(4_000_000, finP2.TargetVnd);
        Assert.Equal(3_000_000, finP2.BalanceVnd); // 4.5M before - 1.5M refunded to B = 3.0M remaining
        var obFounder = finP2.Obligations.Single(o => o.UserId == founder.Id);
        var obA = finP2.Obligations.Single(o => o.UserId == memberA.Id);
        Assert.Equal(500_000, obFounder.RemainingVnd); // 2M required - 1.5M carry-over = 500k
        Assert.Equal(500_000, obA.RemainingVnd);

        // Founder and Member A contribute remaining 500k each -> Period 2 is 100% funded!
        await service.ContributeAsync(founder.Id, fund.Id, new(wFounder.Id, 500_000, "c-f2", Pin, finP2.Revision), default);
        await service.ContributeAsync(memberA.Id, fund.Id, new(wA.Id, 500_000, "c-a2", Pin, finP2.Revision), default);

        var finP2Funded = await service.FinanceAsync(founder.Id, fund.Id, default);
        Assert.Equal(4_000_000, finP2Funded.BalanceVnd);
        Assert.Equal("OPEN", finP2Funded.SpendingState);

        // ---------------------------------------------------------------------
        // INACTIVITY & ELECTION: Founder goes inactive for 16 days
        // Member A opens election, votes, advances, and confirms PIN -> New Owner!
        // ---------------------------------------------------------------------
        clock.Advance(TimeSpan.FromDays(16));
        var election = await service.OpenElectionAsync(memberA.Id, fund.Id, new V1OpenElectionRequest(0), default);

        // Member A votes for self
        await service.RegisterCandidateAsync(memberA.Id, election.Id, Pin, default);
        await service.CastBallotAsync(memberA.Id, election.Id, new V1BallotRequest(1, memberA.Id, null), default);

        // Advance to round 2 (Member A won)
        await service.AdvanceElectionAsync(memberA.Id, election.Id, default);

        // Member A confirms acceptance with PIN via AcceptElectedOwnershipAsync
        var acceptedElection = await service.AcceptElectedOwnershipAsync(memberA.Id, election.Id, Pin, default);
        Assert.Equal("Completed", acceptedElection.Phase);
        Assert.Equal(memberA.Id, acceptedElection.WinnerUserId);

        // Verify fund owner is now Member A and revision increased
        var fundAfterElection = await f.Db.V1Funds.AsNoTracking().SingleAsync(x => x.Id == fund.Id);
        Assert.Equal(memberA.Id, fundAfterElection.OwnerUserId);
        Assert.True(fundAfterElection.OwnershipRevision >= 1);

        // ---------------------------------------------------------------------
        // PERIOD 2 CLOSURE & FINAL DISSOLUTION
        // New Owner (Member A) closes activity and dissolves fund
        // ---------------------------------------------------------------------
        await service.CloseActivityAsync(memberA.Id, fund.Id, Pin, default);
        var dissolve = await service.DissolveFundAsync(memberA.Id, fund.Id, new V1DissolveFundRequest(Pin, "final-dissolve-key"), default);

        Assert.Equal(4_000_000, dissolve.TotalRefundedVnd);
        Assert.Equal(0, dissolve.FinalBalanceVnd);

        // Active members in Period 2 (Founder and Member A) each contributed 2M net for Period 2 -> refunded 2M each!
        Assert.Equal(2, dissolve.Refunds.Count);
        foreach (var refItem in dissolve.Refunds)
        {
            Assert.Equal(2_000_000, refItem.RefundAmountVnd);
        }

        // Final treasury ledger balance is exactly 0 VND!
        var treasuryAcc = await f.Db.LedgerAccounts.Where(a => a.Code == "V1-FUND:" + fund.Id.ToString("N")).SingleAsync();
        Assert.Equal(0, treasuryAcc.Balance);

        // Fund is officially closed
        var finalFundDb = await f.Db.V1Funds.AsNoTracking().SingleAsync(x => x.Id == fund.Id);
        Assert.NotNull(finalFundDb.DissolvedAt);
    }
}
