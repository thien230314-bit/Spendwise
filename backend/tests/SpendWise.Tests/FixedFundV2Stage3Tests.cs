using System.Net;
using System.Net.Http.Json;
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

        // Settle Period 2 without replacement: blocked because remaining members' obligation would increase without consent
        var exConsent = await Assert.ThrowsAsync<ConflictException>(() => service.SettleActivityAsync(owner.Id, proposal.Id, Pin, default));
        Assert.Equal("obligation_changed_requires_consent", exConsent.Code);

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
    // Matches fixed.md line 83 through end-to-end realistic multi-period flow:
    // Period 1: 6M target (A: 3M, B: 3M). A pays 3M, B pays 3M.
    // B receives 2M partial refund -> A has 3M unspent, B has 1M unspent.
    // Close Period 1. Proposal for Period 2: 4M target (2M each).
    // Snapshot: A carries 3M (1M protected excess), B carries 1M.
    // B contributes 1M to satisfy obligation -> Treasury has 5M, spendable 4M.
    // Spend 2M on expense -> Treasury leaves 3M.
    // Dissolution: A gets 2M (1M protected + 1M shared), B gets 1M. Quỹ về 0 VND!
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
        var fund = await service.CreateAsync(owner.Id, new("FundT19", "Fixed", 6_000_000, Pin, 24, true, "v2.0"), default);

        var inv = await service.InviteAsync(owner.Id, fund.Id, memB.Id, default);
        await service.AcceptAsync(memB.Id, inv.Id, true, "v2.0", default);

        var config = await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(6_000_000, Pin), default);

        var wOwner = await f.AddWalletAsync(owner.Id, "WOwner", true, balance: 10_000_000);
        var wB = await f.AddWalletAsync(memB.Id, "WB", true, balance: 10_000_000);
        await f.AddWalletAsync(merchant.Id, "WMerchant", true, balance: 0);

        // Period 1: Owner contributes 3,000,000 VND, Member B contributes 1,000,000 VND via application logic
        await service.ContributeAsync(owner.Id, fund.Id, new(wOwner.Id, 3_000_000, "c-o-19", Pin, config.Revision), default);
        await service.ContributeAsync(memB.Id, fund.Id, new(wB.Id, 1_000_000, "c-b-19", Pin, config.Revision), default);

        // Close Period 1. Balance is now 4M (3M for A, 1M for B).
        clock.Advance(TimeSpan.FromHours(1));
        await service.CloseActivityAsync(owner.Id, fund.Id, Pin, default);

        // Propose Period 2 with target 4M (2M required each).
        clock.Advance(TimeSpan.FromHours(1));
        var proposal = await service.ProposeActivityAsync(owner.Id, fund.Id, new(4_000_000, "p2-t19", Pin), default);

        // Verify snapshot accurately captured: Owner A = 3M, Member B = 1M
        var vOwner = await f.Db.V1ActivityVoters.SingleAsync(v => v.ProposalId == proposal.Id && v.UserId == owner.Id);
        var vB = await f.Db.V1ActivityVoters.SingleAsync(v => v.ProposalId == proposal.Id && v.UserId == memB.Id);
        Assert.Equal(3_000_000, vOwner.CarryOverVnd);
        Assert.Equal(1_000_000, vB.CarryOverVnd);

        await service.VoteActivityAsync(owner.Id, proposal.Id, true, default);
        await service.VoteActivityAsync(memB.Id, proposal.Id, true, default);

        // Settle proposal
        clock.Advance(TimeSpan.FromHours(1));
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
        clock.Advance(TimeSpan.FromHours(1));
        await service.ContributeAsync(memB.Id, fund.Id, new(wB.Id, 1_000_000, "c-b-19-complete", Pin, financeP2.Revision), default);

        // Now fund has 5M balance (4M target + 1M locked excess)
        var financeFunded = await service.FinanceAsync(owner.Id, fund.Id, default);
        Assert.Equal(5_000_000, financeFunded.BalanceVnd);
        Assert.Equal("OPEN", financeFunded.SpendingState);

        // Spend 2M on expense to merchant
        clock.Advance(TimeSpan.FromHours(1));
        var exp = new V1FundRequestInput("Expense", 2_000_000, merchant.Id, "Supplies P2", "exp-t19", Pin);
        await service.CreateFundRequestAsync(owner.Id, fund.Id, exp, default);

        // Treasury balance is now 3M (5M - 2M). Close activity and dissolve.
        clock.Advance(TimeSpan.FromHours(1));
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
    // T22: Two-worker coordination across 72h replacement window
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

        // Advance clock way past the old period's deadline (50 hours)
        clock.Advance(TimeSpan.FromHours(50));

        // 1. Stage 2 worker: returns NoAction, does NOT auto-kick anyone
        var res = await service.ProcessFixedFundDeadlinesAsync(fund.Id, default);
        Assert.Equal("NoAction", res.Status);
        Assert.Empty(res.AutoKickedUserIds);
        Assert.False(res.Dissolved);

        // 2. Open proposal and transition to AwaitingReplacement
        var proposal = await service.ProposeActivityAsync(owner.Id, fund.Id, new(4_000_000, "p2-t22", Pin), default);
        await service.VoteActivityAsync(owner.Id, proposal.Id, true, default);
        await service.VoteActivityAsync(memA.Id, proposal.Id, false, default); // MemA opts out -> proposal is now AwaitingReplacement

        var pCheck = await f.Db.V1ActivityProposals.SingleAsync(p => p.Id == proposal.Id);
        Assert.Equal("AwaitingReplacement", pCheck.State);

        // 3. At 71 hours (before 72h): Stage 2 worker still returns NoAction
        clock.Advance(TimeSpan.FromHours(71));
        var res2 = await service.ProcessFixedFundDeadlinesAsync(fund.Id, default);
        Assert.Equal("NoAction", res2.Status);

        // Stage 3 progression before 72h: returns false (no dissolution yet)
        var progressedBefore = await service.ProcessActivityProposalDeadlineAsync(proposal.Id, default);
        Assert.False(progressedBefore);

        // 4. At 73 hours (after 72h): Stage 2 worker still returns NoAction
        clock.Advance(TimeSpan.FromHours(2));
        var res3 = await service.ProcessFixedFundDeadlinesAsync(fund.Id, default);
        Assert.Equal("NoAction", res3.Status);

        // Stage 3 progression triggers dissolution and marks proposal Failed
        var progressedAfter = await service.ProcessActivityProposalDeadlineAsync(proposal.Id, default);
        Assert.True(progressedAfter);

        var pFinal = await f.Db.V1ActivityProposals.AsNoTracking().SingleAsync(p => p.Id == proposal.Id);
        Assert.Equal("Failed", pFinal.State);

        var fundFinal = await f.Db.V1Funds.AsNoTracking().SingleAsync(x => x.Id == fund.Id);
        Assert.NotNull(fundFinal.DissolvedAt);
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

        // 1. Advancing clock past 24 hours prevents cancel
        clock.Advance(TimeSpan.FromHours(25));
        var exTimeout = await Assert.ThrowsAsync<ConflictException>(() =>
            service.CancelActivityProposalAsync(owner.Id, proposal.Id, Pin, default));
        Assert.Equal("proposal_voting_closed", exTimeout.Code);

        // 2. Proposal in AwaitingReplacement cannot be cancelled
        var fund2 = await service.CreateAsync(owner.Id, new("FundT23-2", "Fixed", 4_000_000, Pin, 24, true, "v2.0"), default);
        var inv2 = await service.InviteAsync(owner.Id, fund2.Id, memA.Id, default);
        await service.AcceptAsync(memA.Id, inv2.Id, true, "v2.0", default);
        await service.ConfirmObligationsAsync(owner.Id, fund2.Id, new(4_000_000, Pin), default);
        await service.CloseActivityAsync(owner.Id, fund2.Id, Pin, default);

        var proposal2 = await service.ProposeActivityAsync(owner.Id, fund2.Id, new(4_000_000, "p2-t23-2", Pin), default);
        await service.VoteActivityAsync(owner.Id, proposal2.Id, true, default);
        await service.VoteActivityAsync(memA.Id, proposal2.Id, false, default); // opts out -> enters AwaitingReplacement

        var exAwaiting = await Assert.ThrowsAsync<ConflictException>(() =>
            service.CancelActivityProposalAsync(owner.Id, proposal2.Id, Pin, default));
        Assert.Equal("proposal_voting_closed", exAwaiting.Code);
    }

    // =========================================================================
    // T24: Unique Active Proposal & Idempotency Replay
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

        // 1. Idempotency replay with SAME key returns the existing proposal cleanly!
        var replay = await service.ProposeActivityAsync(owner.Id, fund.Id, new(4_000_000, "p1-t24", Pin), default);
        Assert.Equal(p1.Id, replay.Id);

        // 1b. Replay with SAME key but different NewName throws idempotency_conflict (P2)
        var exConflict = await Assert.ThrowsAsync<ConflictException>(() =>
            service.ProposeActivityAsync(owner.Id, fund.Id, new(4_000_000, "p1-t24", Pin, NewName: "DifferentName"), default));
        Assert.Equal("idempotency_conflict", exConflict.Code);

        // 2. Attempt second proposal with DIFFERENT key while p1 is Voting: throws active_proposal_exists
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            service.ProposeActivityAsync(owner.Id, fund.Id, new(4_000_000, "p2-t24", Pin), default));
        Assert.Equal("active_proposal_exists", ex.Code);

        // 3. Move p1 to AwaitingReplacement: attempting second proposal with different key still throws active_proposal_exists
        await service.VoteActivityAsync(owner.Id, p1.Id, true, default);
        await service.VoteActivityAsync(memA.Id, p1.Id, false, default);

        var ex2 = await Assert.ThrowsAsync<ConflictException>(() =>
            service.ProposeActivityAsync(owner.Id, fund.Id, new(4_000_000, "p3-t24", Pin), default));
        Assert.Equal("active_proposal_exists", ex2.Code);
    }

    // =========================================================================
    // T25: Member consent protection: altering financial parameters at settlement rejected
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

        // Propose with 48 hours duration, 6,000,000 VND target, and proposed name "RenamedT25"
        var p = await service.ProposeActivityAsync(owner.Id, fund.Id, new(6_000_000, "p2-t25", Pin, 48, "RenamedT25"), default);
        Assert.Equal(48, p.ContributionDurationHours); // Accurately returns proposed 48h!
        Assert.Equal(3_000_000, p.ExpectedPerMemberVnd); // 6M / 2 = 3M expected per member!

        await service.VoteActivityAsync(owner.Id, p.Id, true, default);
        await service.VoteActivityAsync(memA.Id, p.Id, true, default);

        // 1. Owner attempts to alter target from 6M to 8M without member consent: BLOCKED!
        var exTarget = await Assert.ThrowsAsync<ConflictException>(() =>
            service.SettleActivityAsync(owner.Id, p.Id, new V1SettleActivityRequest(Pin, TargetVnd: 8_000_000), default));
        Assert.Equal("configuration_changed_requires_consent", exTarget.Code);

        // 2. Owner attempts to alter duration from 48h to 72h without member consent: BLOCKED!
        var exDuration = await Assert.ThrowsAsync<ConflictException>(() =>
            service.SettleActivityAsync(owner.Id, p.Id, new V1SettleActivityRequest(Pin, ContributionDurationHours: 72), default));
        Assert.Equal("configuration_changed_requires_consent", exDuration.Code);

        // 3. Owner attempts to alter name from "RenamedT25" to "UnconsentedName" without member consent: BLOCKED!
        var exName = await Assert.ThrowsAsync<ConflictException>(() =>
            service.SettleActivityAsync(owner.Id, p.Id, new V1SettleActivityRequest(Pin, NewName: "UnconsentedName"), default));
        Assert.Equal("configuration_changed_requires_consent", exName.Code);

        // 4. Settle with consented configuration succeeds cleanly!
        var settle = await service.SettleActivityAsync(owner.Id, p.Id, new V1SettleActivityRequest(Pin), default);
        Assert.Equal("Settled", settle.State);

        var updatedFund = await f.Db.V1Funds.AsNoTracking().SingleAsync(x => x.Id == fund.Id);
        Assert.Equal("RenamedT25", updatedFund.Name);
        Assert.Equal(6_000_000, updatedFund.TargetVnd);
        Assert.Equal(48, updatedFund.ContributionDurationHours);

        var treasury = await f.Db.V1FundTreasuries.SingleAsync(t => t.FundId == fund.Id);
        var obligations = await f.Db.V1FundObligations.Where(o => o.FundId == fund.Id && o.Revision == treasury.Revision).ToListAsync();
        Assert.Equal(2, obligations.Count);
        Assert.All(obligations, o => Assert.Equal(3_000_000, o.RequiredVnd));
        Assert.All(obligations, o => Assert.Equal(clock.UtcNow.AddHours(48), o.Deadline));
    }

    // =========================================================================
    // T26: Blocker 2 — Member departure increases obligation from 2M to 3M -> BLOCKED without consent
    // =========================================================================
    [Fact]
    public async Task T26_MemberDeparture_IncreasesObligation_BlockedWithoutConsent()
    {
        var clock = new MutableClock(DateTime.UtcNow);
        await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);
        var owner = await f.AddUserAsync("OwnerT26");
        var memB = await f.AddUserAsync("MemBT26");
        var memC = await f.AddUserAsync("MemCT26");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, memB.Id);
        await V1FundMembershipTests.MakeFriends(f, owner.Id, memC.Id);

        var service = CreateService(f.Db, clock, f);
        var fund = await service.CreateAsync(owner.Id, new("FundT26", "Fixed", 6_000_000, Pin, 24, true, "v2.0"), default);

        var invB = await service.InviteAsync(owner.Id, fund.Id, memB.Id, default);
        await service.AcceptAsync(memB.Id, invB.Id, true, "v2.0", default);
        var invC = await service.InviteAsync(owner.Id, fund.Id, memC.Id, default);
        await service.AcceptAsync(memC.Id, invC.Id, true, "v2.0", default);

        await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(6_000_000, Pin), default);
        await service.CloseActivityAsync(owner.Id, fund.Id, Pin, default);

        // Open proposal with 3 members: Target = 6M -> Expected per member is 2,000,000 VND
        var proposal = await service.ProposeActivityAsync(owner.Id, fund.Id, new(6_000_000, "p2-t26", Pin), default);
        Assert.Equal(2_000_000, proposal.ExpectedPerMemberVnd);

        // Owner votes true (consented to 2M), Member B votes true (consented to 2M)
        await service.VoteActivityAsync(owner.Id, proposal.Id, true, default);
        await service.VoteActivityAsync(memB.Id, proposal.Id, true, default);

        // Member C rejects and leaves (Support = false)
        await service.VoteActivityAsync(memC.Id, proposal.Id, false, default);

        // Active members is now 2 (Owner and Member B).
        // If Owner attempts to settle 6M with 2 members, Member B's obligation would become 3M instead of 2M!
        // System MUST block this unilateral increase with obligation_changed_requires_consent!
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            service.SettleActivityAsync(owner.Id, proposal.Id, Pin, default));
        Assert.Equal("obligation_changed_requires_consent", ex.Code);

        // Re-confirmation: Continuing members can agree to the new 3M obligation!
        await service.ConfirmActivityObligationAsync(owner.Id, proposal.Id, new V1ConfirmActivityObligationRequest(3_000_000, 24, Pin), default);
        await service.ConfirmActivityObligationAsync(memB.Id, proposal.Id, new V1ConfirmActivityObligationRequest(3_000_000, 24, Pin), default);

        // Now that all active members confirmed 3M, settlement SUCCEEDS!
        var settle = await service.SettleActivityAsync(owner.Id, proposal.Id, Pin, default);
        Assert.Equal("Settled", settle.State);

        var treasury = await f.Db.V1FundTreasuries.SingleAsync(t => t.FundId == fund.Id);
        var obligations = await f.Db.V1FundObligations.Where(o => o.FundId == fund.Id && o.Revision == treasury.Revision).ToListAsync();
        Assert.Equal(2, obligations.Count);
        Assert.All(obligations, o => Assert.Equal(3_000_000, o.RequiredVnd));
    }

    // =========================================================================
    // T27: Replacement member fills vacant seat -> must confirm obligation before settlement succeeds
    // =========================================================================
    [Fact]
    public async Task T27_ReplacementMember_MustConfirmObligation_BeforeSettlementSucceeds()
    {
        var clock = new MutableClock(DateTime.UtcNow);
        await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);
        var owner = await f.AddUserAsync("OwnerT27");
        var memB = await f.AddUserAsync("MemBT27");
        var memC = await f.AddUserAsync("MemCT27");
        var memD = await f.AddUserAsync("MemDT27");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, memB.Id);
        await V1FundMembershipTests.MakeFriends(f, owner.Id, memC.Id);
        await V1FundMembershipTests.MakeFriends(f, owner.Id, memD.Id);

        var service = CreateService(f.Db, clock, f);
        var fund = await service.CreateAsync(owner.Id, new("FundT27", "Fixed", 6_000_000, Pin, 24, true, "v2.0"), default);

        var invB = await service.InviteAsync(owner.Id, fund.Id, memB.Id, default);
        await service.AcceptAsync(memB.Id, invB.Id, true, "v2.0", default);
        var invC = await service.InviteAsync(owner.Id, fund.Id, memC.Id, default);
        await service.AcceptAsync(memC.Id, invC.Id, true, "v2.0", default);

        await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(6_000_000, Pin), default);
        await service.CloseActivityAsync(owner.Id, fund.Id, Pin, default);

        // Target 6M, 3 members -> Expected per member = 2,000,000 VND
        var proposal = await service.ProposeActivityAsync(owner.Id, fund.Id, new(6_000_000, "p2-t27", Pin), default);
        await service.VoteActivityAsync(owner.Id, proposal.Id, true, default);
        await service.VoteActivityAsync(memB.Id, proposal.Id, true, default);

        // Member C rejects and leaves
        await service.VoteActivityAsync(memC.Id, proposal.Id, false, default);

        // Owner invites Member D as replacement for vacant seat
        var invD = await service.InviteAsync(owner.Id, fund.Id, memD.Id, default);
        await service.AcceptAsync(memD.Id, invD.Id, true, "v2.0", default);

        // Settle immediately is BLOCKED because Member D has NOT confirmed their obligation yet!
        var exUnconfirmed = await Assert.ThrowsAsync<ConflictException>(() =>
            service.SettleActivityAsync(owner.Id, proposal.Id, Pin, default));
        Assert.Equal("obligation_changed_requires_consent", exUnconfirmed.Code);

        // Member D explicitly confirms their obligation (2M, 24h)
        await service.ConfirmActivityObligationAsync(memD.Id, proposal.Id, new V1ConfirmActivityObligationRequest(2_000_000, 24, Pin), default);

        // Now settlement SUCCEEDS!
        var settle = await service.SettleActivityAsync(owner.Id, proposal.Id, Pin, default);
        Assert.Equal("Settled", settle.State);

        var treasury = await f.Db.V1FundTreasuries.SingleAsync(t => t.FundId == fund.Id);
        var obligations = await f.Db.V1FundObligations.Where(o => o.FundId == fund.Id && o.Revision == treasury.Revision).ToListAsync();
        Assert.Equal(3, obligations.Count);
        Assert.All(obligations, o => Assert.Equal(2_000_000, o.RequiredVnd));
    }

    // =========================================================================
    // T28: True 72h window when only Owner remains -> Replacement member joins & confirms within window -> Settles
    // =========================================================================
    [Fact]
    public async Task T28_AwaitingReplacement_72hWindow_ReplacementJoinsAndConfirms_Settles()
    {
        var clock = new MutableClock(DateTime.UtcNow);
        await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);
        var owner = await f.AddUserAsync("OwnerT28");
        var memB = await f.AddUserAsync("MemBT28");
        var memC = await f.AddUserAsync("MemCT28");
        var memD = await f.AddUserAsync("MemDT28");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, memB.Id);
        await V1FundMembershipTests.MakeFriends(f, owner.Id, memC.Id);
        await V1FundMembershipTests.MakeFriends(f, owner.Id, memD.Id);

        var service = CreateService(f.Db, clock, f);
        var fund = await service.CreateAsync(owner.Id, new("FundT28", "Fixed", 6_000_000, Pin, 24, true, "v2.0"), default);

        var invB = await service.InviteAsync(owner.Id, fund.Id, memB.Id, default);
        await service.AcceptAsync(memB.Id, invB.Id, true, "v2.0", default);
        var invC = await service.InviteAsync(owner.Id, fund.Id, memC.Id, default);
        await service.AcceptAsync(memC.Id, invC.Id, true, "v2.0", default);

        await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(6_000_000, Pin), default);
        await service.CloseActivityAsync(owner.Id, fund.Id, Pin, default);

        // Propose Period 2 with 6M target
        var proposal = await service.ProposeActivityAsync(owner.Id, fund.Id, new(6_000_000, "p2-t28", Pin), default);

        // Owner votes YES, but BOTH members B and C reject!
        await service.VoteActivityAsync(owner.Id, proposal.Id, true, default);
        await service.VoteActivityAsync(memB.Id, proposal.Id, false, default);
        await service.VoteActivityAsync(memC.Id, proposal.Id, false, default);

        // Proposal MUST transition to AwaitingReplacement!
        var pUpdated = await f.Db.V1ActivityProposals.SingleAsync(p => p.Id == proposal.Id);
        Assert.Equal("AwaitingReplacement", pUpdated.State);

        // Treasury MUST have 72h replacement window set!
        var treasury = await f.Db.V1FundTreasuries.SingleAsync(t => t.FundId == fund.Id);
        Assert.NotNull(treasury.ReplacementWindowExpiresAt);
        Assert.Equal(clock.UtcNow.AddHours(72), treasury.ReplacementWindowExpiresAt.Value);

        // Advance 24 hours into the 72h window
        clock.UtcNow = clock.UtcNow.AddHours(24);

        // Owner invites Member D as replacement
        var invD = await service.InviteAsync(owner.Id, fund.Id, memD.Id, default);
        await service.AcceptAsync(memD.Id, invD.Id, true, "v2.0", default);

        // Settle blocked until obligations confirmed for the 2 members
        var exConsent = await Assert.ThrowsAsync<ConflictException>(() =>
            service.SettleActivityAsync(owner.Id, proposal.Id, Pin, default));
        Assert.Equal("obligation_changed_requires_consent", exConsent.Code);

        // Owner and D confirm their 3M obligation (6M / 2)
        await service.ConfirmActivityObligationAsync(owner.Id, proposal.Id, new V1ConfirmActivityObligationRequest(3_000_000, 24, Pin), default);
        await service.ConfirmActivityObligationAsync(memD.Id, proposal.Id, new V1ConfirmActivityObligationRequest(3_000_000, 24, Pin), default);

        // Settle succeeds!
        var settle = await service.SettleActivityAsync(owner.Id, proposal.Id, Pin, default);
        Assert.Equal("Settled", settle.State);

        var updatedTreasury = await f.Db.V1FundTreasuries.SingleAsync(t => t.FundId == fund.Id);
        var obligations = await f.Db.V1FundObligations.Where(o => o.FundId == fund.Id && o.Revision == updatedTreasury.Revision).ToListAsync();
        Assert.Equal(2, obligations.Count);
        Assert.All(obligations, o => Assert.Equal(3_000_000, o.RequiredVnd));
    }

    // =========================================================================
    // T29: True 72h window timeout -> Worker auto-dissolves and refunds Owner
    // =========================================================================
    [Fact]
    public async Task T29_AwaitingReplacement_72hWindow_Timeout_AutoDissolves()
    {
        var clock = new MutableClock(DateTime.UtcNow);
        await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);
        var owner = await f.AddUserAsync("OwnerT29");
        var memB = await f.AddUserAsync("MemBT29");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, memB.Id);

        var service = CreateService(f.Db, clock, f);
        var fund = await service.CreateAsync(owner.Id, new("FundT29", "Fixed", 4_000_000, Pin, 24, true, "v2.0"), default);

        var invB = await service.InviteAsync(owner.Id, fund.Id, memB.Id, default);
        await service.AcceptAsync(memB.Id, invB.Id, true, "v2.0", default);

        var config = await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(4_000_000, Pin), default);

        var wOwner = await f.AddWalletAsync(owner.Id, "WOwner29", true, balance: 5_000_000);
        var wB = await f.AddWalletAsync(memB.Id, "WB29", true, balance: 5_000_000);

        await service.ContributeAsync(owner.Id, fund.Id, new(wOwner.Id, 2_000_000, "c-o-29", Pin, config.Revision), default);
        await service.ContributeAsync(memB.Id, fund.Id, new(wB.Id, 2_000_000, "c-b-29", Pin, config.Revision), default);

        await service.CloseActivityAsync(owner.Id, fund.Id, Pin, default);

        var proposal = await service.ProposeActivityAsync(owner.Id, fund.Id, new(4_000_000, "p2-t29", Pin), default);

        // Owner votes YES, B rejects and leaves
        await service.VoteActivityAsync(owner.Id, proposal.Id, true, default);
        await service.VoteActivityAsync(memB.Id, proposal.Id, false, default);

        // Proposal enters AwaitingReplacement
        var p = await f.Db.V1ActivityProposals.SingleAsync(p => p.Id == proposal.Id);
        Assert.Equal("AwaitingReplacement", p.State);

        // Advance past 72 hours
        clock.UtcNow = clock.UtcNow.AddHours(73);

        // Worker runs and processes expired 72h replacement window
        var processed = await service.ProgressActivitiesAsync(default);
        Assert.True(processed >= 1);

        // Proposal is Failed, fund is Dissolved, Owner is refunded
        var pFailed = await f.Db.V1ActivityProposals.AsNoTracking().SingleAsync(p => p.Id == proposal.Id);
        Assert.Equal("Failed", pFailed.State);

        var fundDissolved = await f.Db.V1Funds.AsNoTracking().SingleAsync(fu => fu.Id == fund.Id);
        Assert.NotNull(fundDissolved.DissolvedAt);

        var ownerBalance = await f.Db.Wallets.Where(w => w.Id == wOwner.Id).Select(w => w.AvailableBalance).SingleAsync();
        Assert.Equal(5_000_000, ownerBalance);
    }

    private sealed class StubCurrentUser(Guid? userId, string? sessionId = null) : SpendWise.Application.Common.Interfaces.ICurrentUser
    {
        public Guid? UserId { get; } = userId;
        public string? SessionId { get; } = sessionId ?? Guid.NewGuid().ToString("N");
        public string SystemRole { get; } = "User";
        public string Locale { get; } = "vi-VN";
    }

    // =========================================================================
    // T30: Blocker P1-2 — DTO accurate calculation & outdated obligation consent rejection
    // =========================================================================
    [Fact]
    public async Task T30_DtoAccuratePerMember_And_OutdatedObligationRejected()
    {
        var clock = new MutableClock(DateTime.UtcNow);
        await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);
        var owner = await f.AddUserAsync("OwnerT30");
        var memB = await f.AddUserAsync("MemBT30");
        var memC = await f.AddUserAsync("MemCT30");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, memB.Id);
        await V1FundMembershipTests.MakeFriends(f, owner.Id, memC.Id);

        var service = CreateService(f.Db, clock, f);
        var fund = await service.CreateAsync(owner.Id, new("FundT30", "Fixed", 6_000_000, Pin, 24, true, "v2.0"), default);

        var invB = await service.InviteAsync(owner.Id, fund.Id, memB.Id, default);
        await service.AcceptAsync(memB.Id, invB.Id, true, "v2.0", default);
        var invC = await service.InviteAsync(owner.Id, fund.Id, memC.Id, default);
        await service.AcceptAsync(memC.Id, invC.Id, true, "v2.0", default);

        await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(6_000_000, Pin), default);
        await service.CloseActivityAsync(owner.Id, fund.Id, Pin, default);

        var proposal = await service.ProposeActivityAsync(owner.Id, fund.Id, new(6_000_000, "p2-t30", Pin), default);

        // Initially with 3 members, DTO returns 2,000,000 VND
        var dtoInitial = await service.ActivityProposalAsync(memB.Id, proposal.Id, default);
        Assert.Equal(2_000_000, dtoInitial.ExpectedPerMemberVnd);

        // Owner votes YES, B votes YES, C rejects and leaves
        await service.VoteActivityAsync(owner.Id, proposal.Id, true, default);
        await service.VoteActivityAsync(memB.Id, proposal.Id, true, default);
        await service.VoteActivityAsync(memC.Id, proposal.Id, false, default);

        // After C departs, active members = 2 -> DTO returns accurately 3,000,000 VND!
        var dtoUpdated = await service.ActivityProposalAsync(memB.Id, proposal.Id, default);
        Assert.Equal(3_000_000, dtoUpdated.ExpectedPerMemberVnd);

        // Member B attempts to confirm with outdated amount 2,000,000 VND -> BLOCKED with obligation_outdated!
        var exOutdated = await Assert.ThrowsAsync<ConflictException>(() =>
            service.ConfirmActivityObligationAsync(memB.Id, proposal.Id, new V1ConfirmActivityObligationRequest(2_000_000, 24, Pin), default));
        Assert.Equal("obligation_outdated", exOutdated.Code);

        // Member B attempts to confirm with outdated duration 48h -> BLOCKED with duration_outdated!
        var exDur = await Assert.ThrowsAsync<ConflictException>(() =>
            service.ConfirmActivityObligationAsync(memB.Id, proposal.Id, new V1ConfirmActivityObligationRequest(3_000_000, 48, Pin), default));
        Assert.Equal("duration_outdated", exDur.Code);

        // P1-1: Attempting to confirm with 0 VND, negative, or 0 hours is REJECTED and leaves ConfirmedAt as null!
        var exZeroAmt = await Assert.ThrowsAsync<ConflictException>(() =>
            service.ConfirmActivityObligationAsync(memB.Id, proposal.Id, new V1ConfirmActivityObligationRequest(0, 24, Pin), default));
        Assert.Equal("obligation_outdated", exZeroAmt.Code);

        var exZeroBoth = await Assert.ThrowsAsync<ConflictException>(() =>
            service.ConfirmActivityObligationAsync(memB.Id, proposal.Id, new V1ConfirmActivityObligationRequest(0, 0, Pin), default));
        Assert.Equal("obligation_outdated", exZeroBoth.Code);

        var exZeroDur = await Assert.ThrowsAsync<ConflictException>(() =>
            service.ConfirmActivityObligationAsync(memB.Id, proposal.Id, new V1ConfirmActivityObligationRequest(3_000_000, 0, Pin), default));
        Assert.Equal("duration_outdated", exZeroDur.Code);

        var exNegAmt = await Assert.ThrowsAsync<ConflictException>(() =>
            service.ConfirmActivityObligationAsync(memB.Id, proposal.Id, new V1ConfirmActivityObligationRequest(-1, 24, Pin), default));
        Assert.Equal("obligation_outdated", exNegAmt.Code);

        var exNegDur = await Assert.ThrowsAsync<ConflictException>(() =>
            service.ConfirmActivityObligationAsync(memB.Id, proposal.Id, new V1ConfirmActivityObligationRequest(3_000_000, -1, Pin), default));
        Assert.Equal("duration_outdated", exNegDur.Code);

        // Verify voter record was NOT modified by invalid requests: ConfirmedObligationVnd is STILL the stale 2M, not updated to 3M or 0!
        var voterBCheck = await f.Db.V1ActivityVoters.SingleAsync(v => v.ProposalId == proposal.Id && v.UserId == memB.Id);
        Assert.Equal(2_000_000, voterBCheck.ConfirmedObligationVnd);

        // Member B confirms with accurate 3M and 24h -> SUCCEEDS!
        await service.ConfirmActivityObligationAsync(memB.Id, proposal.Id, new V1ConfirmActivityObligationRequest(3_000_000, 24, Pin), default);
        await service.ConfirmActivityObligationAsync(owner.Id, proposal.Id, new V1ConfirmActivityObligationRequest(3_000_000, 24, Pin), default);

        // Settlement SUCCEEDS!
        var settle = await service.SettleActivityAsync(owner.Id, proposal.Id, Pin, default);
        Assert.Equal("Settled", settle.State);
    }

    private static async Task ActorAsync(SpendWiseApiFactory factory, HttpClient client, SpendWise.Domain.Identity.User user)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SpendWise.Infrastructure.Persistence.SpendWiseDbContext>();
        var session = new SpendWise.Domain.Identity.UserSession { UserId = user.Id, LastSeenAt = DateTime.UtcNow };
        db.UserSessions.Add(session);
        await db.SaveChangesAsync();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer", scope.ServiceProvider.GetRequiredService<ITokenService>().CreateAccessToken(user, session.Id).Token);
    }

    // =========================================================================
    // T31: Blocker P1-1 — Real ASP.NET Core HTTP Integration Test for confirm-obligation
    // =========================================================================
    [Fact]
    public async Task T31_ControllerEndpoint_ConfirmObligation_RealHttpIntegration()
    {
        using var factory = new SpendWiseApiFactory();
        using var client = factory.CreateHttpsClient();
        
        SpendWise.Domain.Identity.User owner;
        SpendWise.Domain.Identity.User memB;
        SpendWise.Domain.Identity.User memC;
        Guid proposalId;

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SpendWise.Infrastructure.Persistence.SpendWiseDbContext>();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();
            var funds = CoreAcceptanceData.Funds(db, clock);

            owner = CoreAcceptanceHttpTests.SyntheticUser();
            memB = CoreAcceptanceHttpTests.SyntheticUser();
            memC = CoreAcceptanceHttpTests.SyntheticUser();
            db.Users.AddRange(owner, memB, memC);
            await db.SaveChangesAsync();

            var fund = await funds.CreateAsync(owner.Id, new("FundT31Http", "Fixed", 6_000_000, Pin, 24, true, "v2.0"), default);
            db.V1FundMemberships.Add(new() { FundId = fund.Id, UserId = memB.Id, Seat = 2, JoinedAt = clock.UtcNow });
            db.V1FundMemberships.Add(new() { FundId = fund.Id, UserId = memC.Id, Seat = 3, JoinedAt = clock.UtcNow });
            await db.SaveChangesAsync();

            await funds.ConfirmObligationsAsync(owner.Id, fund.Id, new(6_000_000, Pin), default);
            await funds.CloseActivityAsync(owner.Id, fund.Id, Pin, default);

            var prop = await funds.ProposeActivityAsync(owner.Id, fund.Id, new(6_000_000, "p2-t31-http", Pin), default);
            proposalId = prop.Id;
            await funds.VoteActivityAsync(owner.Id, proposalId, true, default);
            await funds.VoteActivityAsync(memB.Id, proposalId, true, default);
            await funds.VoteActivityAsync(memC.Id, proposalId, false, default);
        }

        var endpoint = $"/api/v1/funds/activity-proposals/{proposalId}/confirm-obligation";

        // 1. Unauthenticated request -> 401 Unauthorized
        client.DefaultRequestHeaders.Authorization = null;
        using var unauthResp = await client.PostAsJsonAsync(endpoint, new V1ConfirmActivityObligationRequest(3_000_000, 24, Pin));
        Assert.Equal(HttpStatusCode.Unauthorized, unauthResp.StatusCode);

        // 2. Authorize as Member B
        await ActorAsync(factory, client, memB);

        // 3. Outdated amount (2M instead of 3M) -> HTTP 409 Conflict with obligation_outdated
        using var outdatedResp = await client.PostAsJsonAsync(endpoint, new V1ConfirmActivityObligationRequest(2_000_000, 24, Pin));
        Assert.Equal(HttpStatusCode.Conflict, outdatedResp.StatusCode);
        var outdatedContent = await outdatedResp.Content.ReadAsStringAsync();
        Assert.Contains("obligation_outdated", outdatedContent);

        // 4. Zero amount -> HTTP 409 Conflict with obligation_outdated
        using var zeroAmtResp = await client.PostAsJsonAsync(endpoint, new V1ConfirmActivityObligationRequest(0, 24, Pin));
        Assert.Equal(HttpStatusCode.Conflict, zeroAmtResp.StatusCode);
        Assert.Contains("obligation_outdated", await zeroAmtResp.Content.ReadAsStringAsync());

        // 5. Zero duration -> HTTP 409 Conflict with duration_outdated
        using var zeroDurResp = await client.PostAsJsonAsync(endpoint, new V1ConfirmActivityObligationRequest(3_000_000, 0, Pin));
        Assert.Equal(HttpStatusCode.Conflict, zeroDurResp.StatusCode);
        Assert.Contains("duration_outdated", await zeroDurResp.Content.ReadAsStringAsync());

        // 6. Valid confirmation -> HTTP 200 OK with accurate DTO
        using var validResp = await client.PostAsJsonAsync(endpoint, new V1ConfirmActivityObligationRequest(3_000_000, 24, Pin));
        Assert.Equal(HttpStatusCode.OK, validResp.StatusCode);
        var validDto = await validResp.Content.ReadFromJsonAsync<V1ActivityProposalDto>();
        Assert.NotNull(validDto);
        Assert.Equal(3_000_000, validDto.ExpectedPerMemberVnd);

        // 7. Authorize as Owner and confirm
        await ActorAsync(factory, client, owner);
        using var ownerConfirmResp = await client.PostAsJsonAsync(endpoint, new V1ConfirmActivityObligationRequest(3_000_000, 24, Pin));
        Assert.Equal(HttpStatusCode.OK, ownerConfirmResp.StatusCode);

        // 8. Settle activity proposal via HTTP -> HTTP 200 OK Settled!
        using var settleResp = await client.PostAsJsonAsync($"/api/v1/funds/activity-proposals/{proposalId}/settle", new V1SettleActivityRequest(Pin));
        Assert.Equal(HttpStatusCode.OK, settleResp.StatusCode);
        var settledDto = await settleResp.Content.ReadFromJsonAsync<V1ActivityProposalDto>();
        Assert.NotNull(settledDto);
        Assert.Equal("Settled", settledDto.State);
    }

    // =========================================================================
    // T32: Blocker P1-2 — Rejoining member after opt-out starts at CarryOverVnd = 0 and ledger reconciles
    // =========================================================================
    [Fact]
    public async Task T32_RejoiningMember_AfterOptOut_StartsAtZeroCarryOver_AndLedgerReconciles()
    {
        var clock = new MutableClock(DateTime.UtcNow);
        await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);
        var owner = await f.AddUserAsync("OwnerT32");
        var memB = await f.AddUserAsync("MemBT32");
        var memC = await f.AddUserAsync("MemCT32");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, memB.Id);
        await V1FundMembershipTests.MakeFriends(f, owner.Id, memC.Id);

        var service = CreateService(f.Db, clock, f);
        var fund = await service.CreateAsync(owner.Id, new("FundT32", "Fixed", 6_000_000, Pin, 24, true, "v2.0"), default);

        var invB = await service.InviteAsync(owner.Id, fund.Id, memB.Id, default);
        await service.AcceptAsync(memB.Id, invB.Id, true, "v2.0", default);
        var invC = await service.InviteAsync(owner.Id, fund.Id, memC.Id, default);
        await service.AcceptAsync(memC.Id, invC.Id, true, "v2.0", default);

        var config = await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(6_000_000, Pin), default);

        var wOwner = await f.AddWalletAsync(owner.Id, "WOwnerT32", true, balance: 10_000_000);
        var wB = await f.AddWalletAsync(memB.Id, "WBT32", true, balance: 10_000_000);
        var wC = await f.AddWalletAsync(memC.Id, "WCT32", true, balance: 10_000_000);

        await service.ContributeAsync(owner.Id, fund.Id, new(wOwner.Id, 2_000_000, "c-owner-t32", Pin, config.Revision), default);
        await service.ContributeAsync(memB.Id, fund.Id, new(wB.Id, 2_000_000, "c-b-t32", Pin, config.Revision), default);
        await service.ContributeAsync(memC.Id, fund.Id, new(wC.Id, 2_000_000, "c-c-t32", Pin, config.Revision), default);

        var treasury = await f.Db.V1FundTreasuries.SingleAsync(t => t.FundId == fund.Id);
        var tAcc = await f.Db.LedgerAccounts.SingleAsync(a => a.Id == treasury.LedgerAccountId);
        Assert.Equal(6_000_000, tAcc.Balance);

        // Period 1 completes and closes
        await service.CloseActivityAsync(owner.Id, fund.Id, Pin, default);

        // Owner proposes Period 2 with Target 6M
        var proposal = await service.ProposeActivityAsync(owner.Id, fund.Id, new(6_000_000, "p2-t32", Pin), default);

        // Snapshot has 2M for each of A, B, C
        var voterCBefore = await f.Db.V1ActivityVoters.SingleAsync(v => v.ProposalId == proposal.Id && v.UserId == memC.Id);
        Assert.Equal(2_000_000, voterCBefore.CarryOverVnd);

        // C votes NO (opts out and receives 2M refund)
        var cWalletBefore = (await f.Db.LedgerAccounts.SingleAsync(a => a.WalletId == wC.Id)).Balance;
        await service.VoteActivityAsync(memC.Id, proposal.Id, false, default);

        // Check refund posted to ledger
        var cWalletAfter = (await f.Db.LedgerAccounts.SingleAsync(a => a.WalletId == wC.Id)).Balance;
        Assert.Equal(cWalletBefore + 2_000_000, cWalletAfter);

        // Treasury balance decreased by 2M
        var tAccAfterRefund = await f.Db.LedgerAccounts.SingleAsync(a => a.Id == treasury.LedgerAccountId);
        Assert.Equal(4_000_000, tAccAfterRefund.Balance);

        // C's original membership is closed
        var memCOriginal = await f.Db.V1FundMemberships.SingleAsync(m => m.UserId == memC.Id && m.FundId == fund.Id && m.LeftAt != null);
        Assert.Equal(2_000_000, memCOriginal.RefundedVnd);

        // Voting ends (A and B voted yes)
        await service.VoteActivityAsync(owner.Id, proposal.Id, true, default);
        await service.VoteActivityAsync(memB.Id, proposal.Id, true, default);

        var propDb = await f.Db.V1ActivityProposals.SingleAsync(p => p.Id == proposal.Id);
        Assert.Equal("ApprovedAwaitingSettlement", propDb.State);

        // Advance clock by 1 hour into replacement window so new episode joined time > proposal opened time
        clock.Advance(TimeSpan.FromHours(1));

        // Owner invites C back to fill the empty seat!
        var reinviteC = await service.InviteAsync(owner.Id, fund.Id, memC.Id, default);
        await service.AcceptAsync(memC.Id, reinviteC.Id, true, "v2.0", default);

        var newMemC = await f.Db.V1FundMemberships.SingleAsync(m => m.UserId == memC.Id && m.FundId == fund.Id && m.LeftAt == null);
        Assert.NotEqual(memCOriginal.Id, newMemC.Id);

        // Blocker P1 Audit: Both membership episodes exist with independent, immutable records!
        // 1. Episode 1 voter record (Refunded):
        var voterCOriginalAfterRejoin = await f.Db.V1ActivityVoters.SingleAsync(v => v.ProposalId == proposal.Id && v.MembershipId == memCOriginal.Id);
        Assert.Equal(2_000_000, voterCOriginalAfterRejoin.CarryOverVnd); // Historical 2M entitlement preserved!
        Assert.False(voterCOriginalAfterRejoin.Support); // Voted NO preserved!
        Assert.Equal(memCOriginal.Id, voterCOriginalAfterRejoin.MembershipId);

        // 2. Episode 2 voter record (Rejoined):
        var voterCAfterRejoin = await f.Db.V1ActivityVoters.SingleAsync(v => v.ProposalId == proposal.Id && v.MembershipId == newMemC.Id);
        Assert.Equal(newMemC.Id, voterCAfterRejoin.MembershipId);
        Assert.Equal(0L, voterCAfterRejoin.CarryOverVnd); // MUST BE 0!
        Assert.Null(voterCAfterRejoin.Support); // Must confirm obligation anew!

        // Active members is back to 3: Owner, MemB, MemC
        // Target 6M / 3 = 2M each
        await service.ConfirmActivityObligationAsync(owner.Id, proposal.Id, new V1ConfirmActivityObligationRequest(2_000_000, 24, Pin), default);
        await service.ConfirmActivityObligationAsync(memB.Id, proposal.Id, new V1ConfirmActivityObligationRequest(2_000_000, 24, Pin), default);
        await service.ConfirmActivityObligationAsync(memC.Id, proposal.Id, new V1ConfirmActivityObligationRequest(2_000_000, 24, Pin), default);

        // Owner settles proposal!
        var settle = await service.SettleActivityAsync(owner.Id, proposal.Id, Pin, default);
        Assert.Equal("Settled", settle.State);

        // CRITICAL LEDGER AUDIT: Verify Episode 2 carry-over in Period 2 is exactly 0!
        var voterCFinalEpisode2 = await f.Db.V1ActivityVoters.SingleAsync(v => v.ProposalId == proposal.Id && v.MembershipId == newMemC.Id);
        Assert.Equal(0L, voterCFinalEpisode2.CarryOverVnd);
        Assert.True(voterCFinalEpisode2.Support);
        Assert.Equal(2_000_000, voterCFinalEpisode2.ConfirmedObligationVnd);

        // And Episode 1 voter record remains completely unchanged:
        var voterCOriginalFinal = await f.Db.V1ActivityVoters.SingleAsync(v => v.ProposalId == proposal.Id && v.MembershipId == memCOriginal.Id);
        Assert.Equal(2_000_000, voterCOriginalFinal.CarryOverVnd);
        Assert.False(voterCOriginalFinal.Support);

        // C contributes 2M fresh to fulfill Period 2 obligation:
        var treasuryPeriod2 = await f.Db.V1FundTreasuries.SingleAsync(t => t.FundId == fund.Id);
        await service.ContributeAsync(memC.Id, fund.Id, new(wC.Id, 2_000_000, "c-rejoin-contrib", Pin, treasuryPeriod2.Revision), default);

        // Treasury balance is now 4M + 2M = 6M
        var finalTreasuryAcc = await f.Db.LedgerAccounts.SingleAsync(a => a.Id == treasury.LedgerAccountId);
        Assert.Equal(6_000_000, finalTreasuryAcc.Balance);

        // DTO electorate statistics audit: verify electorate size and Yes/No votes are cleanly separated from replacement episode
        var proposalDtoCheck = await service.ActivityProposalAsync(owner.Id, proposal.Id, default);
        Assert.Equal(3, proposalDtoCheck.ElectorateSize); // Frozen electorate of 3 (Owner, B, C-episode1)
        Assert.Equal(2, proposalDtoCheck.Yes); // Owner + B voted Yes
        Assert.Equal(1, proposalDtoCheck.No); // C-episode1 voted No

        // Dissolution lifecycle validation: attempting voluntary dissolution while activity is open MUST BE REJECTED!
        var exActiveDissolve = await Assert.ThrowsAsync<ConflictException>(() =>
            service.DissolveFundAsync(owner.Id, fund.Id, new V1DissolveFundRequest(Pin, "dissolve-mid-cycle-blocked"), default));
        Assert.Equal("active_activity_not_closed", exActiveDissolve.Code);

        // Owner officially closes activity before dissolution:
        await service.CloseActivityAsync(owner.Id, fund.Id, Pin, default);

        // Fund dissolution audit: run official Owner dissolution flow (DissolveFundAsync)
        var wOwnerBefore = (await f.Db.LedgerAccounts.SingleAsync(a => a.WalletId == wOwner.Id)).Balance;
        var wBBefore = (await f.Db.LedgerAccounts.SingleAsync(a => a.WalletId == wB.Id)).Balance;
        var wCBefore = (await f.Db.LedgerAccounts.SingleAsync(a => a.WalletId == wC.Id)).Balance;

        var dissolveResult = await service.DissolveFundAsync(owner.Id, fund.Id, new V1DissolveFundRequest(Pin, "dissolve-t32-key"), default);
        Assert.Equal(6_000_000, dissolveResult.TotalRefundedVnd);
        Assert.Equal(3, dissolveResult.Refunds.Count);

        // Assert exact wallet refund amounts:
        var wOwnerAfter = (await f.Db.LedgerAccounts.SingleAsync(a => a.WalletId == wOwner.Id)).Balance;
        var wBAfter = (await f.Db.LedgerAccounts.SingleAsync(a => a.WalletId == wB.Id)).Balance;
        var wCAfter = (await f.Db.LedgerAccounts.SingleAsync(a => a.WalletId == wC.Id)).Balance;
        Assert.Equal(wOwnerBefore + 2_000_000, wOwnerAfter); // A receives exactly 2,000,000 VND
        Assert.Equal(wBBefore + 2_000_000, wBAfter); // B receives exactly 2,000,000 VND
        Assert.Equal(wCBefore + 2_000_000, wCAfter); // C receives exactly 2,000,000 VND from dissolution (in addition to 2M opt-out)

        // Assert FinancialTransactions match Ledger postings:
        var refundTxs = await f.Db.FinancialTransactions
            .Where(t => t.RelatedBusinessId == fund.Id && t.Type == FinancialTransactionType.Refund)
            .ToListAsync();
        Assert.Equal(4, refundTxs.Count); // 1 opt-out refund + 3 dissolution refunds
        Assert.All(refundTxs, tx => Assert.True(tx.LedgerTransactionId != Guid.Empty));
        var ledgerTxIds = (await f.Db.LedgerTransactions.Select(l => l.Id).ToListAsync()).ToHashSet();
        Assert.All(refundTxs, tx => Assert.Contains(tx.LedgerTransactionId, ledgerTxIds));

        // Assert Treasury account is completely drained to 0 VND:
        var dissolvedTreasuryAcc = await f.Db.LedgerAccounts.SingleAsync(a => a.Id == treasury.LedgerAccountId);
        Assert.Equal(0L, dissolvedTreasuryAcc.Balance);

        // Assert Fund state is Dissolved:
        var dissolvedFund = await f.Db.V1Funds.AsNoTracking().SingleAsync(f => f.Id == fund.Id);
        Assert.NotNull(dissolvedFund.DissolvedAt);

        // Assert Idempotency: second dissolution call is rejected and creates no duplicate refunds
        await Assert.ThrowsAsync<NotFoundException>(() => service.DissolveFundAsync(owner.Id, fund.Id, new V1DissolveFundRequest(Pin, "dissolve-t32-retry"), default));

        // Final assertion: Episode 1 voter record remains 100% immutable in DB:
        var voterCOriginalFinalCheck = await f.Db.V1ActivityVoters.SingleAsync(v => v.ProposalId == proposal.Id && v.MembershipId == memCOriginal.Id);
        Assert.Equal(2_000_000, voterCOriginalFinalCheck.CarryOverVnd);
        Assert.False(voterCOriginalFinalCheck.Support);
        Assert.Equal(memCOriginal.Id, voterCOriginalFinalCheck.MembershipId);
        // Financial integrity: zero phantom balance, perfect reconciliation!
    }
}
