using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features;
using SpendWise.Application.Features.Authentication;
using SpendWise.Application.Features.Banking;
using SpendWise.Application.Finance;
using SpendWise.Application.Identity;
using SpendWise.Application.Notifications;
using SpendWise.Application.Features.V1Groups;
using SpendWise.Domain.Finance;
using SpendWise.Domain.V1Groups;
using Xunit;

namespace SpendWise.Tests;

public sealed class FixedFundV2Stage3Tests
{
    private const string Pin = TargetedCorrectnessFixture.SecurityPin;

    private sealed class MutableClock(DateTime initial) : IClock
    {
        public DateTime UtcNow { get; set; } = initial;
        public void Advance(TimeSpan d) => UtcNow += d;
    }

    private sealed class TestInactivity : IV1InactivityEvidence
    {
        public Task<V1InactivityProof> GetAsync(Guid userId, CancellationToken ct) =>
            Task.FromResult(new V1InactivityProof(true, true, "Synthetic-proof"));
    }

    private static V1FundService CreateService(
        SpendWise.Infrastructure.Persistence.SpendWiseDbContext db,
        IClock clock,
        TargetedCorrectnessFixture f,
        IServiceScopeFactory? scopes = null) => new(
        db, clock, f.Security,
        new SpendWise.Infrastructure.Notifications.NotificationService(db),
        new SpendWise.Infrastructure.Finance.LedgerService(db, clock),
        new SpendWise.Application.Features.Banking.ActiveBankGuard(db),
        Microsoft.Extensions.Options.Options.Create(new V1ElectionPolicy
        {
            Version = "SYNTHETIC-TEST-POLICY",
            RegistrationSeconds = 60,
            NominationSeconds = 60,
            RunoffSeconds = 60,
            ConfirmationSeconds = 60,
            AcceptanceSeconds = 60,
            FreezeMembership = true,
            AllowContributions = false,
            NoCandidatesOutcome = "Fail",
            NoVotesOutcome = "Fail",
            AcceptanceExpiryOutcome = "Fail",
            EligibilityChangeOutcome = "Fail"
        }),
        new TestInactivity(),
        scopes: scopes);

    // =========================================================================
    // T10: Spending blocked until group funding is 100% complete
    // =========================================================================
    [Fact]
    public async Task T10_SpendingState_BlockedUntilGroupFunded100Percent()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("OwnerT10");
        var memA = await f.AddUserAsync("MemAT10");
        var memB = await f.AddUserAsync("MemBT10");
        var merchant = await f.AddUserAsync("MerchantT10");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, memA.Id);
        await V1FundMembershipTests.MakeFriends(f, owner.Id, memB.Id);

        var service = V1ElectionTests.Configured(f);
        var fund = await service.CreateAsync(owner.Id, new("FundT10", "Fixed", 9_000_000, Pin, 24, true, "v2.0"), default);

        var invA = await service.InviteAsync(owner.Id, fund.Id, memA.Id, default);
        await service.AcceptAsync(memA.Id, invA.Id, true, "v2.0", default);
        var invB = await service.InviteAsync(owner.Id, fund.Id, memB.Id, default);
        await service.AcceptAsync(memB.Id, invB.Id, true, "v2.0", default);

        var config = await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(9_000_000, Pin), default);

        var wOwner = await f.AddWalletAsync(owner.Id, "WOwner", true, balance: 5_000_000);
        var wA = await f.AddWalletAsync(memA.Id, "WA", true, balance: 5_000_000);
        var wB = await f.AddWalletAsync(memB.Id, "WB", true, balance: 5_000_000);
        await f.AddWalletAsync(merchant.Id, "WMerchant", true, balance: 0);

        // Owner contributes 3M, A contributes 3M, B contributes 2M -> Total 8M / 9M
        await service.ContributeAsync(owner.Id, fund.Id, new(wOwner.Id, 3_000_000, "c-o-10", Pin, config.Revision), default);
        await service.ContributeAsync(memA.Id, fund.Id, new(wA.Id, 3_000_000, "c-a-10", Pin, config.Revision), default);
        await service.ContributeAsync(memB.Id, fund.Id, new(wB.Id, 2_000_000, "c-b-10", Pin, config.Revision), default);

        var finance = await service.FinanceAsync(owner.Id, fund.Id, default);
        Assert.Equal("FINANCE_BLOCKED", finance.SpendingState);

        var exp = new V1FundRequestInput("Expense", 1_000_000, merchant.Id, "Premature expense", "exp-t10", Pin);
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            service.CreateFundRequestAsync(owner.Id, fund.Id, exp, default));
        Assert.Equal("fixed_group_funding_incomplete", ex.Code);
    }

    // =========================================================================
    // T11: Member without CanSpend cannot spend even when fund 100% funded
    // =========================================================================
    [Fact]
    public async Task T11_MemberWithoutCanSpend_CannotSpendEvenWhenFund100PercentFunded()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("OwnerT11");
        var memB = await f.AddUserAsync("MemBT11");
        var merchant = await f.AddUserAsync("MerchantT11");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, memB.Id);

        var service = V1ElectionTests.Configured(f);
        var fund = await service.CreateAsync(owner.Id, new("FundT11", "Fixed", 4_000_000, Pin, 24, true, "v2.0"), default);

        var inv = await service.InviteAsync(owner.Id, fund.Id, memB.Id, default);
        await service.AcceptAsync(memB.Id, inv.Id, true, "v2.0", default);

        var config = await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(4_000_000, Pin), default);

        var wOwner = await f.AddWalletAsync(owner.Id, "WOwner", true, balance: 5_000_000);
        var wB = await f.AddWalletAsync(memB.Id, "WB", true, balance: 5_000_000);
        await f.AddWalletAsync(merchant.Id, "WMerchant", true, balance: 0);

        await service.ContributeAsync(owner.Id, fund.Id, new(wOwner.Id, 2_000_000, "c-o-11", Pin, config.Revision), default);
        await service.ContributeAsync(memB.Id, fund.Id, new(wB.Id, 2_000_000, "c-b-11", Pin, config.Revision), default);

        var finance = await service.FinanceAsync(owner.Id, fund.Id, default);
        Assert.Equal("OPEN", finance.SpendingState);

        // B has CanSpend == false by default
        var members = await service.MembersAsync(owner.Id, fund.Id, default);
        var bDto = Assert.Single(members, m => m.UserId == memB.Id);
        Assert.False(bDto.CanSpend);

        var exp = new V1FundRequestInput("Expense", 500_000, merchant.Id, "B spending without permission", "exp-t11", Pin);
        var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.CreateFundRequestAsync(memB.Id, fund.Id, exp, default));
        Assert.Equal("fund_spending_permission_required", ex.Code);
    }

    // =========================================================================
    // T12: Owner grants CanSpend to Member B -> Expense succeeds and ledger updates
    // =========================================================================
    [Fact]
    public async Task T12_OwnerGrantsCanSpend_MemberSpendsSuccessfully_LedgerUpdates()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("OwnerT12");
        var memB = await f.AddUserAsync("MemBT12");
        var merchant = await f.AddUserAsync("MerchantT12");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, memB.Id);

        var service = V1ElectionTests.Configured(f);
        var fund = await service.CreateAsync(owner.Id, new("FundT12", "Fixed", 4_000_000, Pin, 24, true, "v2.0"), default);

        var inv = await service.InviteAsync(owner.Id, fund.Id, memB.Id, default);
        await service.AcceptAsync(memB.Id, inv.Id, true, "v2.0", default);

        var config = await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(4_000_000, Pin), default);

        var wOwner = await f.AddWalletAsync(owner.Id, "WOwner", true, balance: 5_000_000);
        var wB = await f.AddWalletAsync(memB.Id, "WB", true, balance: 5_000_000);
        var wMerch = await f.AddWalletAsync(merchant.Id, "WMerchant", true, balance: 0);

        await service.ContributeAsync(owner.Id, fund.Id, new(wOwner.Id, 2_000_000, "c-o-12", Pin, config.Revision), default);
        await service.ContributeAsync(memB.Id, fund.Id, new(wB.Id, 2_000_000, "c-b-12", Pin, config.Revision), default);

        // Owner grants CanSpend to B
        await service.SetSpendingPermissionAsync(owner.Id, fund.Id, memB.Id, true, Pin, default);

        var exp = new V1FundRequestInput("Expense", 1_500_000, merchant.Id, "B spending on supplies", "exp-t12", Pin);
        var req = await service.CreateFundRequestAsync(memB.Id, fund.Id, exp, default);
        Assert.Equal("Completed", req.State);

        var finance = await service.FinanceAsync(owner.Id, fund.Id, default);
        Assert.Equal(2_500_000, finance.BalanceVnd);

        var merchBalance = await f.Db.Wallets.Where(w => w.Id == wMerch.Id).Select(w => w.AvailableBalance).SingleAsync();
        Assert.Equal(1_500_000, merchBalance);
    }

    // =========================================================================
    // T13: Protected Excess cannot be spent
    // =========================================================================
    [Fact]
    public async Task T13_ProtectedExcess_CannotBeSpent()
    {
        var clock = new MutableClock(DateTime.UtcNow);
        await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);
        var owner = await f.AddUserAsync("OwnerT13");
        var memA = await f.AddUserAsync("MemAT13");
        var merchant = await f.AddUserAsync("MerchantT13");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, memA.Id);

        var service = CreateService(f.Db, clock, f);
        var fund = await service.CreateAsync(owner.Id, new("FundT13", "Fixed", 4_000_000, Pin, 24, true, "v2.0"), default);

        var inv = await service.InviteAsync(owner.Id, fund.Id, memA.Id, default);
        await service.AcceptAsync(memA.Id, inv.Id, true, "v2.0", default);

        var config = await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(4_000_000, Pin), default);

        var wOwner = await f.AddWalletAsync(owner.Id, "WOwner", true, balance: 10_000_000);
        var wA = await f.AddWalletAsync(memA.Id, "WA", true, balance: 10_000_000);
        await f.AddWalletAsync(merchant.Id, "WMerch", true, balance: 0);

        // Period 1: Target 4M. Owner contributes 2M, A contributes 2M. Total 4M.
        await service.ContributeAsync(owner.Id, fund.Id, new(wOwner.Id, 2_000_000, "c-o-13", Pin, config.Revision), default);
        await service.ContributeAsync(memA.Id, fund.Id, new(wA.Id, 2_000_000, "c-a-13", Pin, config.Revision), default);

        // Close Period 1 with 4M unspent (2M entitlement each)
        await service.CloseActivityAsync(owner.Id, fund.Id, Pin, default);

        // Propose Period 2 with target 2M (1M required each).
        // A carries over 2M -> required is 1M -> A will have 1M PROTECTED EXCESS!
        // Owner carries over 2M -> required is 1M -> Owner will have 1M PROTECTED EXCESS!
        var proposal = await service.ProposeActivityAsync(owner.Id, fund.Id, new(2_000_000, "p2-t13", Pin), default);
        await service.VoteActivityAsync(owner.Id, proposal.Id, true, default);
        await service.VoteActivityAsync(memA.Id, proposal.Id, true, default);

        // Settle Period 2
        await service.SettleActivityAsync(owner.Id, proposal.Id, Pin, default);

        // Treasury has 4M balance. Target is 2M.
        // Protected excess = 1M (Owner) + 1M (A) = 2M.
        // Spendable = 4M - 2M = 2M.
        var finance = await service.FinanceAsync(owner.Id, fund.Id, default);
        Assert.Equal(4_000_000, finance.BalanceVnd);
        Assert.Equal(2_000_000, finance.Obligations.Sum(o => o.LockedExcessVnd));

        // Spending 2M succeeds
        var expValid = new V1FundRequestInput("Expense", 2_000_000, merchant.Id, "Spend all spendable", "exp-t13-ok", Pin);
        var req = await service.CreateFundRequestAsync(owner.Id, fund.Id, expValid, default);
        Assert.Equal("Completed", req.State);

        // Treasury balance is now 2M, all of which is protected excess! Available spendable = 0.
        // Attempting to spend even 100 VND fails with insufficient_fund_balance!
        var expInvalid = new V1FundRequestInput("Expense", 100, merchant.Id, "Spend protected excess", "exp-t13-fail", Pin);
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            service.CreateFundRequestAsync(owner.Id, fund.Id, expInvalid, default));
        Assert.Equal("insufficient_fund_balance", ex.Code);
    }

    // =========================================================================
    // T14: Close period and Dissolve fund refunds according to entitlement
    // =========================================================================
    [Fact]
    public async Task T14_CloseAndDissolve_RefundsMembers_TreasuryZero()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("OwnerT14");
        var memA = await f.AddUserAsync("MemAT14");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, memA.Id);

        var service = V1ElectionTests.Configured(f);
        var fund = await service.CreateAsync(owner.Id, new("FundT14", "Fixed", 4_000_000, Pin, 24, true, "v2.0"), default);

        var inv = await service.InviteAsync(owner.Id, fund.Id, memA.Id, default);
        await service.AcceptAsync(memA.Id, inv.Id, true, "v2.0", default);

        var config = await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(4_000_000, Pin), default);

        var wOwner = await f.AddWalletAsync(owner.Id, "WOwner", true, balance: 5_000_000);
        var wA = await f.AddWalletAsync(memA.Id, "WA", true, balance: 5_000_000);

        await service.ContributeAsync(owner.Id, fund.Id, new(wOwner.Id, 2_000_000, "c-o-14", Pin, config.Revision), default);
        await service.ContributeAsync(memA.Id, fund.Id, new(wA.Id, 2_000_000, "c-a-14", Pin, config.Revision), default);

        await service.CloseActivityAsync(owner.Id, fund.Id, Pin, default);

        var dissolveResult = await service.DissolveFundAsync(owner.Id, fund.Id, new(Pin, "dissolve-t14"), default);
        Assert.Equal(0, dissolveResult.FinalBalanceVnd);

        var treasuryAcc = await f.Db.LedgerAccounts.Where(a => a.Code == "V1-FUND:" + fund.Id.ToString("N")).SingleAsync();
        Assert.Equal(0, treasuryAcc.Balance);

        var oBalance = await f.Db.Wallets.Where(w => w.Id == wOwner.Id).Select(w => w.AvailableBalance).SingleAsync();
        var aBalance = await f.Db.Wallets.Where(w => w.Id == wA.Id).Select(w => w.AvailableBalance).SingleAsync();
        Assert.Equal(5_000_000, oBalance); // 3M remaining + 2M refund = 5M
        Assert.Equal(5_000_000, aBalance); // 3M remaining + 2M refund = 5M
    }

    // =========================================================================
    // T15: Member voting 'Không' is refunded snapshot entitlement immediately
    // =========================================================================
    [Fact]
    public async Task T15_MemberVotingNo_ReceivesInstantRefund_RemainingSnapshotsUnchanged()
    {
        var clock = new MutableClock(DateTime.UtcNow);
        await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);
        var owner = await f.AddUserAsync("OwnerT15");
        var memA = await f.AddUserAsync("MemAT15");
        var memC = await f.AddUserAsync("MemCT15");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, memA.Id);
        await V1FundMembershipTests.MakeFriends(f, owner.Id, memC.Id);

        var service = CreateService(f.Db, clock, f);
        var fund = await service.CreateAsync(owner.Id, new("FundT15", "Fixed", 6_000_000, Pin, 24, true, "v2.0"), default);

        var invA = await service.InviteAsync(owner.Id, fund.Id, memA.Id, default);
        await service.AcceptAsync(memA.Id, invA.Id, true, "v2.0", default);
        var invC = await service.InviteAsync(owner.Id, fund.Id, memC.Id, default);
        await service.AcceptAsync(memC.Id, invC.Id, true, "v2.0", default);

        var config = await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(6_000_000, Pin), default);

        var wOwner = await f.AddWalletAsync(owner.Id, "WOwner", true, balance: 5_000_000);
        var wA = await f.AddWalletAsync(memA.Id, "WA", true, balance: 5_000_000);
        var wC = await f.AddWalletAsync(memC.Id, "WC", true, balance: 5_000_000);

        await service.ContributeAsync(owner.Id, fund.Id, new(wOwner.Id, 2_000_000, "c-o-15", Pin, config.Revision), default);
        await service.ContributeAsync(memA.Id, fund.Id, new(wA.Id, 2_000_000, "c-a-15", Pin, config.Revision), default);
        await service.ContributeAsync(memC.Id, fund.Id, new(wC.Id, 2_000_000, "c-c-15", Pin, config.Revision), default);

        // Close Period 1 (6M balance, 2M entitlement each)
        await service.CloseActivityAsync(owner.Id, fund.Id, Pin, default);

        // Propose Period 2 with target 4M
        var proposal = await service.ProposeActivityAsync(owner.Id, fund.Id, new(4_000_000, "p2-t15", Pin), default);

        // Verify snapshot taken for all 3 members: 2M each
        var voters = await f.Db.V1ActivityVoters.Where(v => v.ProposalId == proposal.Id).ToListAsync();
        Assert.Equal(3, voters.Count);
        Assert.All(voters, v => Assert.Equal(2_000_000, v.CarryOverVnd));

        // Member C votes 'Không' (support = false)
        await service.VoteActivityAsync(memC.Id, proposal.Id, false, default);

        // C is refunded 2M instantly and departs
        var cWalletBalance = await f.Db.Wallets.Where(w => w.Id == wC.Id).Select(w => w.AvailableBalance).SingleAsync();
        Assert.Equal(5_000_000, cWalletBalance); // 3M remaining + 2M refund = 5M

        var cMember = await f.Db.V1FundMemberships.SingleAsync(m => m.FundId == fund.Id && m.UserId == memC.Id);
        Assert.NotNull(cMember.LeftAt);
        Assert.Equal(2_000_000, cMember.RefundedVnd);

        // Owner and A's snapshot CarryOverVnd remain exactly 2_000_000
        var remainingVoters = await f.Db.V1ActivityVoters.Where(v => v.ProposalId == proposal.Id && v.UserId != memC.Id).ToListAsync();
        Assert.All(remainingVoters, v => Assert.Equal(2_000_000, v.CarryOverVnd));
    }

    // =========================================================================
    // T16: Silent member after 24 hours is auto-refunded and departs
    // =========================================================================
    [Fact]
    public async Task T16_SilentMember_AutoRefundedAt24Hours_Departs()
    {
        var clock = new MutableClock(DateTime.UtcNow);
        await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);
        var owner = await f.AddUserAsync("OwnerT16");
        var memA = await f.AddUserAsync("MemAT16");
        var memC = await f.AddUserAsync("MemCT16");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, memA.Id);
        await V1FundMembershipTests.MakeFriends(f, owner.Id, memC.Id);

        var service = CreateService(f.Db, clock, f);
        var fund = await service.CreateAsync(owner.Id, new("FundT16", "Fixed", 6_000_000, Pin, 24, true, "v2.0"), default);

        var invA = await service.InviteAsync(owner.Id, fund.Id, memA.Id, default);
        await service.AcceptAsync(memA.Id, invA.Id, true, "v2.0", default);
        var invC = await service.InviteAsync(owner.Id, fund.Id, memC.Id, default);
        await service.AcceptAsync(memC.Id, invC.Id, true, "v2.0", default);

        var config = await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(6_000_000, Pin), default);

        var wOwner = await f.AddWalletAsync(owner.Id, "WOwner", true, balance: 5_000_000);
        var wA = await f.AddWalletAsync(memA.Id, "WA", true, balance: 5_000_000);
        var wC = await f.AddWalletAsync(memC.Id, "WC", true, balance: 5_000_000);

        await service.ContributeAsync(owner.Id, fund.Id, new(wOwner.Id, 2_000_000, "c-o-16", Pin, config.Revision), default);
        await service.ContributeAsync(memA.Id, fund.Id, new(wA.Id, 2_000_000, "c-a-16", Pin, config.Revision), default);
        await service.ContributeAsync(memC.Id, fund.Id, new(wC.Id, 2_000_000, "c-c-16", Pin, config.Revision), default);

        await service.CloseActivityAsync(owner.Id, fund.Id, Pin, default);

        var proposal = await service.ProposeActivityAsync(owner.Id, fund.Id, new(4_000_000, "p2-t16", Pin), default);

        // Owner and A vote 'Có' (true)
        await service.VoteActivityAsync(owner.Id, proposal.Id, true, default);
        await service.VoteActivityAsync(memA.Id, proposal.Id, true, default);
        // C stays silent (Support == null)

        // Advance 24 hours
        clock.UtcNow = clock.UtcNow.AddHours(24);
        await service.ProgressActivitiesAsync(default);

        // C has been auto-refunded and marked LeftAt
        var cBalance = await f.Db.Wallets.Where(w => w.Id == wC.Id).Select(w => w.AvailableBalance).SingleAsync();
        Assert.Equal(5_000_000, cBalance);

        var cMember = await f.Db.V1FundMemberships.SingleAsync(m => m.FundId == fund.Id && m.UserId == memC.Id);
        Assert.NotNull(cMember.LeftAt);

        // Proposal is ApprovedAwaitingSettlement with 2 continuing members (Owner & A)
        var pUpdated = await f.Db.V1ActivityProposals.SingleAsync(p => p.Id == proposal.Id);
        Assert.Equal("ApprovedAwaitingSettlement", pUpdated.State);

        // Settle Period 2
        await service.SettleActivityAsync(owner.Id, proposal.Id, Pin, default);

        // Active members in Period 2 are Owner and A only
        var activeMembers = await service.MembersAsync(owner.Id, fund.Id, default);
        Assert.Equal(2, activeMembers.Count);
        Assert.DoesNotContain(activeMembers, m => m.UserId == memC.Id);
    }

    // =========================================================================
    // T17: Cancel proposal does not reverse already refunded departed members
    // =========================================================================
    [Fact]
    public async Task T17_CancelActivityProposal_PreservesDepartedRefunds()
    {
        var clock = new MutableClock(DateTime.UtcNow);
        await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);
        var owner = await f.AddUserAsync("OwnerT17");
        var memA = await f.AddUserAsync("MemAT17");
        var memC = await f.AddUserAsync("MemCT17");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, memA.Id);
        await V1FundMembershipTests.MakeFriends(f, owner.Id, memC.Id);

        var service = CreateService(f.Db, clock, f);
        var fund = await service.CreateAsync(owner.Id, new("FundT17", "Fixed", 6_000_000, Pin, 24, true, "v2.0"), default);

        var invA = await service.InviteAsync(owner.Id, fund.Id, memA.Id, default);
        await service.AcceptAsync(memA.Id, invA.Id, true, "v2.0", default);
        var invC = await service.InviteAsync(owner.Id, fund.Id, memC.Id, default);
        await service.AcceptAsync(memC.Id, invC.Id, true, "v2.0", default);

        var config = await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(6_000_000, Pin), default);

        var wOwner = await f.AddWalletAsync(owner.Id, "WOwner", true, balance: 5_000_000);
        var wA = await f.AddWalletAsync(memA.Id, "WA", true, balance: 5_000_000);
        var wC = await f.AddWalletAsync(memC.Id, "WC", true, balance: 5_000_000);

        await service.ContributeAsync(owner.Id, fund.Id, new(wOwner.Id, 2_000_000, "c-o-17", Pin, config.Revision), default);
        await service.ContributeAsync(memA.Id, fund.Id, new(wA.Id, 2_000_000, "c-a-17", Pin, config.Revision), default);
        await service.ContributeAsync(memC.Id, fund.Id, new(wC.Id, 2_000_000, "c-c-17", Pin, config.Revision), default);

        await service.CloseActivityAsync(owner.Id, fund.Id, Pin, default);

        var proposal = await service.ProposeActivityAsync(owner.Id, fund.Id, new(4_000_000, "p2-t17", Pin), default);

        // C votes 'Không' -> C refunded and departs
        await service.VoteActivityAsync(memC.Id, proposal.Id, false, default);

        // Owner cancels proposal
        await service.CancelActivityProposalAsync(owner.Id, proposal.Id, Pin, default);

        var pCancelled = await f.Db.V1ActivityProposals.SingleAsync(p => p.Id == proposal.Id);
        Assert.Equal("Cancelled", pCancelled.State);

        // C's refund remains intact (not reversed)
        var cBalance = await f.Db.Wallets.Where(w => w.Id == wC.Id).Select(w => w.AvailableBalance).SingleAsync();
        Assert.Equal(5_000_000, cBalance);

        var cMember = await f.Db.V1FundMemberships.SingleAsync(m => m.FundId == fund.Id && m.UserId == memC.Id);
        Assert.NotNull(cMember.LeftAt);
    }

    // =========================================================================
    // T18: All members vote 'Không' leaving only Owner -> 72h window -> AutoDissolve
    // =========================================================================
    [Fact]
    public async Task T18_OnlyOwnerVotesYes_72hWindowExpires_AutoDissolvesAndRefundsOwner()
    {
        var clock = new MutableClock(DateTime.UtcNow);
        await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);
        var owner = await f.AddUserAsync("OwnerT18");
        var memA = await f.AddUserAsync("MemAT18");
        var memB = await f.AddUserAsync("MemBT18");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, memA.Id);
        await V1FundMembershipTests.MakeFriends(f, owner.Id, memB.Id);

        var service = CreateService(f.Db, clock, f);
        var fund = await service.CreateAsync(owner.Id, new("FundT18", "Fixed", 6_000_000, Pin, 24, true, "v2.0"), default);

        var invA = await service.InviteAsync(owner.Id, fund.Id, memA.Id, default);
        await service.AcceptAsync(memA.Id, invA.Id, true, "v2.0", default);
        var invB = await service.InviteAsync(owner.Id, fund.Id, memB.Id, default);
        await service.AcceptAsync(memB.Id, invB.Id, true, "v2.0", default);

        var config = await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(6_000_000, Pin), default);

        var wOwner = await f.AddWalletAsync(owner.Id, "WOwner", true, balance: 5_000_000);
        var wA = await f.AddWalletAsync(memA.Id, "WA", true, balance: 5_000_000);
        var wB = await f.AddWalletAsync(memB.Id, "WB", true, balance: 5_000_000);

        await service.ContributeAsync(owner.Id, fund.Id, new(wOwner.Id, 2_000_000, "c-o-18", Pin, config.Revision), default);
        await service.ContributeAsync(memA.Id, fund.Id, new(wA.Id, 2_000_000, "c-a-18", Pin, config.Revision), default);
        await service.ContributeAsync(memB.Id, fund.Id, new(wB.Id, 2_000_000, "c-b-18", Pin, config.Revision), default);

        await service.CloseActivityAsync(owner.Id, fund.Id, Pin, default);

        var proposal = await service.ProposeActivityAsync(owner.Id, fund.Id, new(4_000_000, "p2-t18", Pin), default);

        // Owner votes 'Có'
        await service.VoteActivityAsync(owner.Id, proposal.Id, true, default);
        // A and B vote 'Không'
        await service.VoteActivityAsync(memA.Id, proposal.Id, false, default);
        await service.VoteActivityAsync(memB.Id, proposal.Id, false, default);

        // Proposal transitions to AwaitingReplacement with 72h window
        var pState = await f.Db.V1ActivityProposals.SingleAsync(p => p.Id == proposal.Id);
        Assert.Equal("AwaitingReplacement", pState.State);

        var treasury = await f.Db.V1FundTreasuries.SingleAsync(t => t.FundId == fund.Id);
        Assert.NotNull(treasury.ReplacementWindowExpiresAt);

        // Advance 73 hours without replacement
        clock.UtcNow = clock.UtcNow.AddHours(73);
        var changed = await service.ProcessActivityProposalDeadlineAsync(proposal.Id, default);
        Assert.True(changed);

        // Fund auto-dissolved and Owner refunded
        var updatedFund = await f.Db.V1Funds.AsNoTracking().SingleAsync(f => f.Id == fund.Id);
        Assert.NotNull(updatedFund.DissolvedAt);

        var oBalance = await f.Db.Wallets.Where(w => w.Id == wOwner.Id).Select(w => w.AvailableBalance).SingleAsync();
        Assert.Equal(5_000_000, oBalance); // 3M remaining + 2M refund = 5M
    }

    // =========================================================================
    // T19: Multi-period carry-over + protected excess + partial spend + dissolution
    // Matches fixed.md line 83:
    // A has 3M, B has 1M; new target 4M (2M each).
    // A has 1M protected excess. B contributes 1M to reach 2M.
    // Fund total balance = 5M, spendable = 4M.
    // Spend 2M on expense -> balance leaves 3M.
    // Settle / dissolve: A gets 2M (1M protected + 1M unspent), B gets 1M.
    // =========================================================================
    [Fact]
    public async Task T19_MultiPeriodCarryOver_ProtectedExcess_PartialSpend_Dissolution()
    {
        var clock = new MutableClock(DateTime.UtcNow);
        await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);
        var owner = await f.AddUserAsync("OwnerT19");
        var memB = await f.AddUserAsync("MemBT19");
        var merchant = await f.AddUserAsync("MerchantT19");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, memB.Id);

        var service = CreateService(f.Db, clock, f);
        var fund = await service.CreateAsync(owner.Id, new("FundT19", "Fixed", 4_000_000, Pin, 24, true, "v2.0"), default);

        var inv = await service.InviteAsync(owner.Id, fund.Id, memB.Id, default);
        await service.AcceptAsync(memB.Id, inv.Id, true, "v2.0", default);

        var config = await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(4_000_000, Pin), default);

        var wOwner = await f.AddWalletAsync(owner.Id, "WOwner", true, balance: 10_000_000);
        var wB = await f.AddWalletAsync(memB.Id, "WB", true, balance: 10_000_000);
        await f.AddWalletAsync(merchant.Id, "WMerchant", true, balance: 0);

        // Close Period 1.
        await service.CloseActivityAsync(owner.Id, fund.Id, Pin, default);

        // Propose Period 2 with target 4M (2M required each).
        var proposal = await service.ProposeActivityAsync(owner.Id, fund.Id, new(4_000_000, "p2-t19", Pin), default);
        await service.VoteActivityAsync(owner.Id, proposal.Id, true, default);
        await service.VoteActivityAsync(memB.Id, proposal.Id, true, default);

        // Simulate asymmetric carry-over per fixed.md line 83: Owner A has 3M, Member B has 1M carry-over
        var treasury = await f.Db.V1FundTreasuries.SingleAsync(t => t.FundId == fund.Id);
        var vOwner = await f.Db.V1ActivityVoters.SingleAsync(v => v.ProposalId == proposal.Id && v.UserId == owner.Id);
        var vB = await f.Db.V1ActivityVoters.SingleAsync(v => v.ProposalId == proposal.Id && v.UserId == memB.Id);
        vOwner.CarryOverVnd = 3_000_000;
        vB.CarryOverVnd = 1_000_000;
        // Seed treasury ledger via double-entry post with 4_000_000 (3M from A + 1M from B)
        var accOwner = await f.Db.LedgerAccounts.SingleAsync(a => a.WalletId == wOwner.Id);
        var accB = await f.Db.LedgerAccounts.SingleAsync(a => a.WalletId == wB.Id);
        var ledger = new SpendWise.Infrastructure.Finance.LedgerService(f.Db, clock);
        await ledger.PostAsync(accOwner.Id, treasury.LedgerAccountId, 3_000_000, "VND", "V1FundContribution", fund.Id, "p1-a", "p1-a", "P1 A", default);
        await ledger.PostAsync(accB.Id, treasury.LedgerAccountId, 1_000_000, "VND", "V1FundContribution", fund.Id, "p1-b", "p1-b", "P1 B", default);
        await f.Db.SaveChangesAsync();

        // Settle proposal
        await service.SettleActivityAsync(owner.Id, proposal.Id, Pin, default);

        // Check finance state:
        // Target is 4M. A has 3M carry-over (2M required + 1M locked excess).
        // B has 1M carry-over (2M required -> 1M remaining).
        var financeP2 = await service.FinanceAsync(owner.Id, fund.Id, default);
        var obA = financeP2.Obligations.Single(o => o.UserId == owner.Id);
        var obB = financeP2.Obligations.Single(o => o.UserId == memB.Id);
        Assert.Equal(1_000_000, obA.LockedExcessVnd);
        Assert.Equal(0, obA.RemainingVnd);
        Assert.Equal(0, obB.LockedExcessVnd);
        Assert.Equal(1_000_000, obB.RemainingVnd);

        // Member B contributes 1M to satisfy obligation
        await service.ContributeAsync(memB.Id, fund.Id, new(wB.Id, 1_000_000, "c-b-19-complete", Pin, financeP2.Revision), default);

        // Now fund has 5M balance (4M target + 1M locked excess)
        var financeFunded = await service.FinanceAsync(owner.Id, fund.Id, default);
        Assert.Equal(5_000_000, financeFunded.BalanceVnd);
        Assert.Equal("OPEN", financeFunded.SpendingState);

        // Spend 2M on expense to merchant
        var exp = new V1FundRequestInput("Expense", 2_000_000, merchant.Id, "Supplies P2", "exp-t19", Pin);
        await service.CreateFundRequestAsync(owner.Id, fund.Id, exp, default);

        // Treasury balance is now 3M (5M - 2M). Close activity and dissolve.
        await service.CloseActivityAsync(owner.Id, fund.Id, Pin, default);
        var dissolve = await service.DissolveFundAsync(owner.Id, fund.Id, new(Pin, "dissolve-t19"), default);
        Assert.Equal(0, dissolve.FinalBalanceVnd);

        // Per line 83: Owner A gets 2M (1M protected excess + 1M unspent share), B gets 1M (unspent share)!
        var refA = dissolve.Refunds.Single(r => r.UserId == owner.Id).RefundAmountVnd;
        var refB = dissolve.Refunds.Single(r => r.UserId == memB.Id).RefundAmountVnd;
        Assert.Equal(2_000_000, refA);
        Assert.Equal(1_000_000, refB);
    }

    // =========================================================================
    // T20: Deterministic remainder whole-đồng distribution
    // =========================================================================
    [Fact]
    public async Task T20_DeterministicRemainder_WholeDongDistribution()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("OwnerT20");
        var memA = await f.AddUserAsync("MemAT20");
        var memB = await f.AddUserAsync("MemBT20");
        var merchant = await f.AddUserAsync("MerchantT20");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, memA.Id);
        await V1FundMembershipTests.MakeFriends(f, owner.Id, memB.Id);

        var service = V1ElectionTests.Configured(f);
        var fund = await service.CreateAsync(owner.Id, new("FundT20", "Fixed", 900, Pin, 24, true, "v2.0"), default);

        var invA = await service.InviteAsync(owner.Id, fund.Id, memA.Id, default);
        await service.AcceptAsync(memA.Id, invA.Id, true, "v2.0", default);
        var invB = await service.InviteAsync(owner.Id, fund.Id, memB.Id, default);
        await service.AcceptAsync(memB.Id, invB.Id, true, "v2.0", default);

        var config = await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(900, Pin), default);

        var wOwner = await f.AddWalletAsync(owner.Id, "WOwner", true, balance: 1_000);
        var wA = await f.AddWalletAsync(memA.Id, "WA", true, balance: 1_000);
        var wB = await f.AddWalletAsync(memB.Id, "WB", true, balance: 1_000);
        await f.AddWalletAsync(merchant.Id, "WMerch", true, balance: 0);

        // 3 members contribute 300 VND each
        await service.ContributeAsync(owner.Id, fund.Id, new(wOwner.Id, 300, "c-o-20", Pin, config.Revision), default);
        await service.ContributeAsync(memA.Id, fund.Id, new(wA.Id, 300, "c-a-20", Pin, config.Revision), default);
        await service.ContributeAsync(memB.Id, fund.Id, new(wB.Id, 300, "c-b-20", Pin, config.Revision), default);

        // 1 VND spent -> 899 VND remaining
        var exp = new V1FundRequestInput("Expense", 1, merchant.Id, "1 VND fee", "exp-t20", Pin);
        await service.CreateFundRequestAsync(owner.Id, fund.Id, exp, default);

        // Close and dissolve
        await service.CloseActivityAsync(owner.Id, fund.Id, Pin, default);
        var dissolve = await service.DissolveFundAsync(owner.Id, fund.Id, new(Pin, "dissolve-t20"), default);
        Assert.Equal(0, dissolve.FinalBalanceVnd);

        // Seat 1 gets 300 VND, Seat 2 gets 300 VND, Seat 3 gets 299 VND. Sum = 899 VND!
        var oBalance = await f.Db.Wallets.Where(w => w.Id == wOwner.Id).Select(w => w.AvailableBalance).SingleAsync();
        var aBalance = await f.Db.Wallets.Where(w => w.Id == wA.Id).Select(w => w.AvailableBalance).SingleAsync();
        var bBalance = await f.Db.Wallets.Where(w => w.Id == wB.Id).Select(w => w.AvailableBalance).SingleAsync();

        var oRefund = oBalance - 700;
        var aRefund = aBalance - 700;
        var bRefund = bBalance - 700;

        Assert.Equal(300, oRefund); // Seat 1
        Assert.Equal(300, aRefund); // Seat 2
        Assert.Equal(299, bRefund); // Seat 3
        Assert.Equal(899, oRefund + aRefund + bRefund);
    }

    // =========================================================================
    // T21: Maximum 3 periods cap strictly enforced
    // =========================================================================
    [Fact]
    public async Task T21_Maximum3Periods_StrictlyEnforced()
    {
        var clock = new MutableClock(DateTime.UtcNow);
        await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);
        var owner = await f.AddUserAsync("OwnerT21");
        var memA = await f.AddUserAsync("MemAT21");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, memA.Id);

        var service = CreateService(f.Db, clock, f);
        var fund = await service.CreateAsync(owner.Id, new("FundT21", "Fixed", 4_000_000, Pin, 24, true, "v2.0"), default);

        var inv = await service.InviteAsync(owner.Id, fund.Id, memA.Id, default);
        await service.AcceptAsync(memA.Id, inv.Id, true, "v2.0", default);

        var config = await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(4_000_000, Pin), default);

        var wOwner = await f.AddWalletAsync(owner.Id, "WOwner", true, balance: 10_000_000);
        var wA = await f.AddWalletAsync(memA.Id, "WA", true, balance: 10_000_000);

        // Period 1 (creation is Period 1)
        await service.ContributeAsync(owner.Id, fund.Id, new(wOwner.Id, 2_000_000, "c-o-21-1", Pin, config.Revision), default);
        await service.ContributeAsync(memA.Id, fund.Id, new(wA.Id, 2_000_000, "c-a-21-1", Pin, config.Revision), default);
        await service.CloseActivityAsync(owner.Id, fund.Id, Pin, default);

        // Period 2 proposal and settlement
        var p2 = await service.ProposeActivityAsync(owner.Id, fund.Id, new(4_000_000, "p2-t21", Pin), default);
        await service.VoteActivityAsync(owner.Id, p2.Id, true, default);
        await service.VoteActivityAsync(memA.Id, p2.Id, true, default);
        await service.SettleActivityAsync(owner.Id, p2.Id, Pin, default);
        await service.CloseActivityAsync(owner.Id, fund.Id, Pin, default);

        // Period 3 proposal and settlement
        var p3 = await service.ProposeActivityAsync(owner.Id, fund.Id, new(4_000_000, "p3-t21", Pin), default);
        await service.VoteActivityAsync(owner.Id, p3.Id, true, default);
        await service.VoteActivityAsync(memA.Id, p3.Id, true, default);
        await service.SettleActivityAsync(owner.Id, p3.Id, Pin, default);
        await service.CloseActivityAsync(owner.Id, fund.Id, Pin, default);

        // Attempting to propose Period 4 (settledCount is 2, total periods would be 4)
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            service.ProposeActivityAsync(owner.Id, fund.Id, new(4_000_000, "p4-t21", Pin), default));
        Assert.Equal("maximum_periods_exceeded", ex.Code);
    }

    // =========================================================================
    // T22: Stage 2 worker isolation: No action when activity closed or proposal active
    // =========================================================================
    [Fact]
    public async Task T22_WorkerIsolation_Stage2WorkerTakesNoAction_WhenActivityClosedOrInTransition()
    {
        var clock = new MutableClock(DateTime.UtcNow);
        await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);
        var owner = await f.AddUserAsync("OwnerT22");
        var memA = await f.AddUserAsync("MemAT22");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, memA.Id);

        var service = CreateService(f.Db, clock, f);
        var fund = await service.CreateAsync(owner.Id, new("FundT22", "Fixed", 4_000_000, Pin, 24, true, "v2.0"), default);
        var inv = await service.InviteAsync(owner.Id, fund.Id, memA.Id, default);
        await service.AcceptAsync(memA.Id, inv.Id, true, "v2.0", default);
        await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(4_000_000, Pin), default);

        // Close activity
        await service.CloseActivityAsync(owner.Id, fund.Id, Pin, default);

        // Advance clock way past the old period's deadline
        clock.Advance(TimeSpan.FromHours(50));

        // Process Stage 2 deadlines worker: must return NoAction and not auto-kick anyone!
        var res = await service.ProcessFixedFundDeadlinesAsync(fund.Id, default);
        Assert.Equal("NoAction", res.Status);
        Assert.Empty(res.AutoKickedUserIds);
        Assert.False(res.Dissolved);

        // Propose activity and advance to AwaitingReplacement
        var proposal = await service.ProposeActivityAsync(owner.Id, fund.Id, new(4_000_000, "p2-t22", Pin), default);
        await service.VoteActivityAsync(owner.Id, proposal.Id, true, default);
        await service.VoteActivityAsync(memA.Id, proposal.Id, false, default); // MemA opts out

        // Now proposal is in AwaitingReplacement
        var pCheck = await f.Db.V1ActivityProposals.SingleAsync(p => p.Id == proposal.Id);
        Assert.Equal("AwaitingReplacement", pCheck.State);

        // Stage 2 worker still must return NoAction and not touch the fund!
        var res2 = await service.ProcessFixedFundDeadlinesAsync(fund.Id, default);
        Assert.Equal("NoAction", res2.Status);
        Assert.Empty(res2.AutoKickedUserIds);
        Assert.False(res2.Dissolved);
    }

    // =========================================================================
    // T23: Proposal Lifecycle: Cannot cancel once voting ends or in AwaitingReplacement
    // =========================================================================
    [Fact]
    public async Task T23_ProposalLifecycle_CannotCancelAfterVotingClosedOrInAwaitingReplacement()
    {
        var clock = new MutableClock(DateTime.UtcNow);
        await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);
        var owner = await f.AddUserAsync("OwnerT23");
        var memA = await f.AddUserAsync("MemAT23");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, memA.Id);

        var service = CreateService(f.Db, clock, f);
        var fund = await service.CreateAsync(owner.Id, new("FundT23", "Fixed", 4_000_000, Pin, 24, true, "v2.0"), default);
        var inv = await service.InviteAsync(owner.Id, fund.Id, memA.Id, default);
        await service.AcceptAsync(memA.Id, inv.Id, true, "v2.0", default);
        await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(4_000_000, Pin), default);
        await service.CloseActivityAsync(owner.Id, fund.Id, Pin, default);

        var proposal = await service.ProposeActivityAsync(owner.Id, fund.Id, new(4_000_000, "p2-t23", Pin), default);

        // Advancing clock past 24 hours prevents cancel
        clock.Advance(TimeSpan.FromHours(25));
        var exTimeout = await Assert.ThrowsAsync<ConflictException>(() =>
            service.CancelActivityProposalAsync(owner.Id, proposal.Id, Pin, default));
        Assert.Equal("proposal_voting_closed", exTimeout.Code);
    }

    // =========================================================================
    // T24: Unique Active Proposal: Cannot create duplicate proposals
    // =========================================================================
    [Fact]
    public async Task T24_UniqueProposal_CannotCreateSecondProposalWhileActive()
    {
        var clock = new MutableClock(DateTime.UtcNow);
        await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);
        var owner = await f.AddUserAsync("OwnerT24");
        var memA = await f.AddUserAsync("MemAT24");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, memA.Id);

        var service = CreateService(f.Db, clock, f);
        var fund = await service.CreateAsync(owner.Id, new("FundT24", "Fixed", 4_000_000, Pin, 24, true, "v2.0"), default);
        var inv = await service.InviteAsync(owner.Id, fund.Id, memA.Id, default);
        await service.AcceptAsync(memA.Id, inv.Id, true, "v2.0", default);
        await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(4_000_000, Pin), default);
        await service.CloseActivityAsync(owner.Id, fund.Id, Pin, default);

        var p1 = await service.ProposeActivityAsync(owner.Id, fund.Id, new(4_000_000, "p1-t24", Pin), default);
        Assert.Equal("Voting", p1.State);

        // Attempt second proposal while p1 is Voting
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            service.ProposeActivityAsync(owner.Id, fund.Id, new(4_000_000, "p2-t24", Pin), default));
        Assert.Equal("active_proposal_exists", ex.Code);
    }

    // =========================================================================
    // T25: New Period Final Obligation Consent and Duration
    // =========================================================================
    [Fact]
    public async Task T25_NewPeriodFinalObligationConsentAndDuration()
    {
        var clock = new MutableClock(DateTime.UtcNow);
        await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);
        var owner = await f.AddUserAsync("OwnerT25");
        var memA = await f.AddUserAsync("MemAT25");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, memA.Id);

        var service = CreateService(f.Db, clock, f);
        var fund = await service.CreateAsync(owner.Id, new("FundT25", "Fixed", 4_000_000, Pin, 24, true, "v2.0"), default);
        var inv = await service.InviteAsync(owner.Id, fund.Id, memA.Id, default);
        await service.AcceptAsync(memA.Id, inv.Id, true, "v2.0", default);
        await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(4_000_000, Pin), default);
        await service.CloseActivityAsync(owner.Id, fund.Id, Pin, default);

        // Propose with 48 hours duration
        var p = await service.ProposeActivityAsync(owner.Id, fund.Id, new(6_000_000, "p2-t25", Pin, 48), default);
        Assert.Equal(24, p.ContributionDurationHours); // Current fund duration
        Assert.Equal(3_000_000, p.ExpectedPerMemberVnd); // 6M / 2 = 3M

        await service.VoteActivityAsync(owner.Id, p.Id, true, default);
        await service.VoteActivityAsync(memA.Id, p.Id, true, default);

        // Settle with custom duration 72h, new target 8M, and renamed fund
        var settle = await service.SettleActivityAsync(owner.Id, p.Id, new V1SettleActivityRequest(Pin, TargetVnd: 8_000_000, ContributionDurationHours: 72, NewName: "RenamedT25"), default);
        Assert.Equal("Settled", settle.State);

        var updatedFund = await f.Db.V1Funds.AsNoTracking().SingleAsync(x => x.Id == fund.Id);
        Assert.Equal("RenamedT25", updatedFund.Name);
        Assert.Equal(8_000_000, updatedFund.TargetVnd);
        Assert.Equal(72, updatedFund.ContributionDurationHours);

        var treasury = await f.Db.V1FundTreasuries.SingleAsync(t => t.FundId == fund.Id);
        var obligations = await f.Db.V1FundObligations.Where(o => o.FundId == fund.Id && o.Revision == treasury.Revision).ToListAsync();
        Assert.Equal(2, obligations.Count);
        Assert.All(obligations, o => Assert.Equal(4_000_000, o.RequiredVnd));
        Assert.All(obligations, o => Assert.Equal(clock.UtcNow.AddHours(72), o.Deadline));
    }
}
