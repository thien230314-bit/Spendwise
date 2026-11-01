using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SpendWise.Infrastructure.Identity;
using SpendWise.Infrastructure.Services;
using SpendWise.Application.Features;
using SpendWise.Application.Features.Authentication;
using SpendWise.Application.Features.Banking;
using SpendWise.Application.Finance;
using SpendWise.Application.Identity;
using SpendWise.Application.Notifications;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.V1Groups;
using SpendWise.Domain.Finance;
using SpendWise.Domain.V1Groups;
using Xunit;

namespace SpendWise.Tests;

public sealed class FixedFundV2Stage2Tests
{
    private const string Pin = TargetedCorrectnessFixture.SecurityPin;

    private sealed class MutableClock(DateTime initial) : IClock
    {
        public DateTime UtcNow { get; set; } = initial;
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
        IServiceScopeFactory? scopes = null,
        ILogger<V1FundService>? logger = null) => new(
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
        scopes: scopes,
        logger: logger);

    [Fact]
    public async Task T05_MemberWithinDeadline_CannotBeKickedForMissingContribution()
    {
        var clock = new MutableClock(DateTime.UtcNow);
        await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);
        var owner = await f.AddUserAsync("OwnerT05");
        var member = await f.AddUserAsync("MemberT05");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);

        var service = CreateService(f.Db, clock, f);
        var fund = await service.CreateAsync(owner.Id, new("FundT05", "Fixed", 4_000_000, Pin, 24, true, "v2.0"), default);
        var inv = await service.InviteAsync(owner.Id, fund.Id, member.Id, default);
        await service.AcceptAsync(member.Id, inv.Id, true, "v2.0", default);
        await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(4_000_000, Pin), default);

        // Advance 2 hours (deadline is 24 hours -> 22 hours remaining)
        clock.UtcNow = clock.UtcNow.AddHours(2);

        // Owner attempts to kick member before deadline -> rejected!
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            service.KickMemberAsync(owner.Id, fund.Id, member.Id, new(Pin, "kick-t05", "Chưa nộp tiền"), default));
        Assert.Equal("member_deadline_active", ex.Code);

        // Sweeper run before deadline -> no action taken
        var sweep = await service.ProcessFixedFundDeadlinesAsync(fund.Id, default);
        Assert.Equal("NoAction", sweep.Status);
        Assert.Empty(sweep.AutoKickedUserIds);
        Assert.False(sweep.Dissolved);

        // Member is still active
        var activeMembers = await service.MembersAsync(owner.Id, fund.Id, default);
        Assert.Equal(2, activeMembers.Count);
    }

    [Fact]
    public async Task T06_MemberDeadlineExpired_AutoKick_RefundsPartial_Opens72hWindow()
    {
        var clock = new MutableClock(DateTime.UtcNow);
        await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);
        var owner = await f.AddUserAsync("OwnerT06");
        var memberB = await f.AddUserAsync("MemberBT06");
        var memberC = await f.AddUserAsync("MemberCT06");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, memberB.Id);
        await V1FundMembershipTests.MakeFriends(f, owner.Id, memberC.Id);

        var service = CreateService(f.Db, clock, f);
        var fund = await service.CreateAsync(owner.Id, new("FundT06", "Fixed", 6_000_000, Pin, 24, true, "v2.0"), default);
        var invB = await service.InviteAsync(owner.Id, fund.Id, memberB.Id, default);
        await service.AcceptAsync(memberB.Id, invB.Id, true, "v2.0", default);
        var invC = await service.InviteAsync(owner.Id, fund.Id, memberC.Id, default);
        await service.AcceptAsync(memberC.Id, invC.Id, true, "v2.0", default);

        var config = await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(6_000_000, Pin), default);
        Assert.Equal(1, config.Revision);

        // Owner contributes their full 2,000,000 VND
        var oWallet = await f.AddWalletAsync(owner.Id, "OWalletT06", true, balance: 2_000_000);
        await service.ContributeAsync(owner.Id, fund.Id, new(oWallet.Id, 2_000_000, "o-full-t06", Pin, config.Revision), default);

        // Member C contributes partial: 1,000,000 / 2,000,000 VND
        var cWallet = await f.AddWalletAsync(memberC.Id, "CWalletT06", true, balance: 1_000_000);
        await service.ContributeAsync(memberC.Id, fund.Id, new(cWallet.Id, 1_000_000, "c-part-c", Pin, config.Revision), default);

        var cWalletAfterContrib = await f.Db.LedgerAccounts.AsNoTracking().SingleAsync(a => a.WalletId == cWallet.Id);
        Assert.Equal(0, cWalletAfterContrib.Balance);

        // Advance past deadline (25 hours)
        clock.UtcNow = clock.UtcNow.AddHours(25);

        // Process deadlines
        var result = await service.ProcessFixedFundDeadlinesAsync(fund.Id, default);
        Assert.Equal("MembersAutoKicked", result.Status);
        Assert.Contains(memberB.Id, result.AutoKickedUserIds);
        Assert.Contains(memberC.Id, result.AutoKickedUserIds);
        Assert.False(result.Dissolved);

        // Verify Member C received real refund of 1,000,000 VND
        var cWalletAfterRefund = await f.Db.LedgerAccounts.AsNoTracking().SingleAsync(a => a.WalletId == cWallet.Id);
        Assert.Equal(1_000_000, cWalletAfterRefund.Balance);

        // Verify 72-hour replacement window is opened
        var treasury = await f.Db.V1FundTreasuries.AsNoTracking().SingleAsync(t => t.FundId == fund.Id);
        Assert.NotNull(treasury.ReplacementWindowExpiresAt);
        Assert.Equal(clock.UtcNow.AddHours(72), treasury.ReplacementWindowExpiresAt.Value);

        // Verify historical obligations remain intact in DB
        var obligations = await f.Db.V1FundObligations.AsNoTracking().Where(o => o.FundId == fund.Id).ToListAsync();
        Assert.Equal(3, obligations.Count);
        Assert.All(obligations, o => Assert.Equal(2_000_000, o.RequiredVnd));
    }

    [Fact]
    public async Task T07_ReplacementMember_JoinsNearEndOf72hWindow_GetsFullDuration()
    {
        var clock = new MutableClock(DateTime.UtcNow);
        await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);
        var owner = await f.AddUserAsync("OwnerT07");
        var memberB = await f.AddUserAsync("MemberBT07");
        var replacementD = await f.AddUserAsync("ReplacementDT07");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, memberB.Id);
        await V1FundMembershipTests.MakeFriends(f, owner.Id, replacementD.Id);

        var service = CreateService(f.Db, clock, f);
        var fund = await service.CreateAsync(owner.Id, new("FundT07", "Fixed", 4_000_000, Pin, 24, true, "v2.0"), default);
        var invB = await service.InviteAsync(owner.Id, fund.Id, memberB.Id, default);
        await service.AcceptAsync(memberB.Id, invB.Id, true, "v2.0", default);
        var config = await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(4_000_000, Pin), default);

        // Owner contributes full obligation (2,000,000 VND)
        var oWallet = await f.AddWalletAsync(owner.Id, "OWalletT07", true, balance: 2_000_000);
        await service.ContributeAsync(owner.Id, fund.Id, new(oWallet.Id, 2_000_000, "o-full-t07", Pin, config.Revision), default);

        // Advance 25 hours -> Member B missed deadline and is auto-kicked
        clock.UtcNow = clock.UtcNow.AddHours(25);
        await service.ProcessFixedFundDeadlinesAsync(fund.Id, default);

        var treasury = await f.Db.V1FundTreasuries.AsNoTracking().SingleAsync(t => t.FundId == fund.Id);
        var windowEnd = treasury.ReplacementWindowExpiresAt!.Value;

        // Advance clock to hour 71.5 of the 72h window (30 minutes before window closes)
        clock.UtcNow = windowEnd.AddMinutes(-30);

        // Invite replacement D for seat 2
        var invD = new V1FundInvitation
        {
            FundId = fund.Id,
            InvitedUserId = replacementD.Id,
            InvitedByUserId = owner.Id,
            Status = V1InvitationStatus.Pending,
            ExpiresAt = clock.UtcNow.AddDays(7),
            IsReplacement = true,
            TargetSeat = 2
        };
        f.Db.V1FundInvitations.Add(invD);
        await f.Db.SaveChangesAsync();

        // D accepts successfully before window expires
        await service.AcceptAsync(replacementD.Id, invD.Id, true, "v2.0", default);

        // Verification: D gets FULL individual duration (24h from when they joined, NOT truncated by window!)
        var dMembership = await f.Db.V1FundMemberships.SingleAsync(m => m.FundId == fund.Id && m.UserId == replacementD.Id && m.LeftAt == null);
        Assert.NotNull(dMembership.ContributionDeadline);
        Assert.Equal(clock.UtcNow.AddHours(24), dMembership.ContributionDeadline.Value);
    }

    [Fact]
    public async Task T08_ReplacementWindowExpires_WithUnfilledSeats_DissolvesAndRefundsRemaining()
    {
        var clock = new MutableClock(DateTime.UtcNow);
        await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);
        var owner = await f.AddUserAsync("OwnerT08");
        var memberB = await f.AddUserAsync("MemberBT08");
        var memberC = await f.AddUserAsync("MemberCT08");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, memberB.Id);
        await V1FundMembershipTests.MakeFriends(f, owner.Id, memberC.Id);

        var service = CreateService(f.Db, clock, f);
        var fund = await service.CreateAsync(owner.Id, new("FundT08", "Fixed", 6_000_000, Pin, 24, true, "v2.0"), default);
        var invB = await service.InviteAsync(owner.Id, fund.Id, memberB.Id, default);
        await service.AcceptAsync(memberB.Id, invB.Id, true, "v2.0", default);
        var invC = await service.InviteAsync(owner.Id, fund.Id, memberC.Id, default);
        await service.AcceptAsync(memberC.Id, invC.Id, true, "v2.0", default);

        var config = await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(6_000_000, Pin), default);

        // Owner and Member B contribute their full obligations (2M each)
        var oWallet = await f.AddWalletAsync(owner.Id, "OWalletT08", true, balance: 5_000_000);
        var bWallet = await f.AddWalletAsync(memberB.Id, "BWalletT08", true, balance: 5_000_000);
        await service.ContributeAsync(owner.Id, fund.Id, new(oWallet.Id, 2_000_000, "c-owner-t08", Pin, config.Revision), default);
        await service.ContributeAsync(memberB.Id, fund.Id, new(bWallet.Id, 2_000_000, "c-b-t08", Pin, config.Revision), default);

        // Advance 25 hours -> Member C contributes 0 and is auto-kicked, opening 72h window
        clock.UtcNow = clock.UtcNow.AddHours(25);
        await service.ProcessFixedFundDeadlinesAsync(fund.Id, default);

        var treasury = await f.Db.V1FundTreasuries.AsNoTracking().SingleAsync(t => t.FundId == fund.Id);
        var windowEnd = treasury.ReplacementWindowExpiresAt!.Value;

        // Advance past 72h replacement window without finding replacement
        clock.UtcNow = windowEnd.AddMinutes(5);

        // Sweeper detects expired replacement window with vacant seat 3 -> Fails period and dissolves
        var result = await service.ProcessFixedFundDeadlinesAsync(fund.Id, default);
        Assert.Equal("ReplacementWindowExpired", result.Status);
        Assert.True(result.Dissolved);

        // Verification of invariants:
        // 1. Fund is dissolved
        var dbFund = await f.Db.V1Funds.AsNoTracking().SingleAsync(x => x.Id == fund.Id);
        Assert.NotNull(dbFund.DissolvedAt);

        // 2. Owner refunded 2,000,000 VND -> wallet balance restored to 5,000,000 VND
        var oAcc = await f.Db.LedgerAccounts.AsNoTracking().SingleAsync(a => a.WalletId == oWallet.Id);
        Assert.Equal(5_000_000, oAcc.Balance);

        // 3. Member B refunded 2,000,000 VND -> wallet balance restored to 5,000,000 VND
        var bAcc = await f.Db.LedgerAccounts.AsNoTracking().SingleAsync(a => a.WalletId == bWallet.Id);
        Assert.Equal(5_000_000, bAcc.Balance);

        // 4. Fund treasury balance is EXACTLY 0 VND!
        var tAcc = await f.Db.LedgerAccounts.AsNoTracking().SingleAsync(a => a.Id == treasury.LedgerAccountId);
        Assert.Equal(0, tAcc.Balance);
    }

    [Fact]
    public async Task T09_OwnerFundingFailure_AutoDissolvesFund_AndRefundsAllMembers()
    {
        var clock = new MutableClock(DateTime.UtcNow);
        await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);
        var owner = await f.AddUserAsync("OwnerT09");
        var member = await f.AddUserAsync("MemberT09");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);

        var service = CreateService(f.Db, clock, f);
        var fund = await service.CreateAsync(owner.Id, new("FundT09", "Fixed", 4_000_000, Pin, 24, true, "v2.0"), default);
        var inv = await service.InviteAsync(owner.Id, fund.Id, member.Id, default);
        await service.AcceptAsync(member.Id, inv.Id, true, "v2.0", default);
        var config = await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(4_000_000, Pin), default);

        // Member contributes full 2,000,000 VND
        var mWallet = await f.AddWalletAsync(member.Id, "MWalletT09", true, balance: 3_000_000);
        await service.ContributeAsync(member.Id, fund.Id, new(mWallet.Id, 2_000_000, "c-m-t09", Pin, config.Revision), default);

        // Owner contributes only partial (500,000 VND out of 2,000,000 VND)
        var oWallet = await f.AddWalletAsync(owner.Id, "OWalletT09", true, balance: 3_000_000);
        await service.ContributeAsync(owner.Id, fund.Id, new(oWallet.Id, 500_000, "c-o-t09", Pin, config.Revision), default);

        // Advance 25 hours past deadline
        clock.UtcNow = clock.UtcNow.AddHours(25);

        // Process deadlines: triggers Owner Funding Failure!
        var result = await service.ProcessFixedFundDeadlinesAsync(fund.Id, default);
        Assert.Equal("OwnerFundingFailed", result.Status);
        Assert.True(result.Dissolved);

        // Invariants:
        // 1. Owner is NOT auto-kicked, but fund is dissolved
        var dbFund = await f.Db.V1Funds.AsNoTracking().SingleAsync(x => x.Id == fund.Id);
        Assert.NotNull(dbFund.DissolvedAt);

        // 2. Member refunded full 2,000,000 VND -> wallet balance restored to 3,000,000 VND
        var mAcc = await f.Db.LedgerAccounts.AsNoTracking().SingleAsync(a => a.WalletId == mWallet.Id);
        Assert.Equal(3_000_000, mAcc.Balance);

        // 3. Owner refunded their 500,000 VND partial contribution -> wallet balance restored to 3,000,000 VND
        var oAcc = await f.Db.LedgerAccounts.AsNoTracking().SingleAsync(a => a.WalletId == oWallet.Id);
        Assert.Equal(3_000_000, oAcc.Balance);

        // 4. Fund treasury balance is EXACTLY 0 VND!
        var treasury = await f.Db.V1FundTreasuries.AsNoTracking().SingleAsync(t => t.FundId == fund.Id);
        var tAcc = await f.Db.LedgerAccounts.AsNoTracking().SingleAsync(a => a.Id == treasury.LedgerAccountId);
        Assert.Equal(0, tAcc.Balance);
    }

    [Fact]
    public async Task T10_ReplacementWindowExpired_RejectsInvitationAcceptance()
    {
        var clock = new MutableClock(DateTime.UtcNow);
        await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);
        var owner = await f.AddUserAsync("OwnerT10");
        var member = await f.AddUserAsync("MemberT10");
        var replacement = await f.AddUserAsync("ReplacementT10");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);
        await V1FundMembershipTests.MakeFriends(f, owner.Id, replacement.Id);

        var service = CreateService(f.Db, clock, f);
        var fund = await service.CreateAsync(owner.Id, new("FundT10", "Fixed", 4_000_000, Pin, 24, true, "v2.0"), default);
        var inv = await service.InviteAsync(owner.Id, fund.Id, member.Id, default);
        await service.AcceptAsync(member.Id, inv.Id, true, "v2.0", default);
        var config = await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(4_000_000, Pin), default);

        // Owner contributes full obligation (2,000,000 VND)
        var oWallet = await f.AddWalletAsync(owner.Id, "OWalletT10", true, balance: 2_000_000);
        await service.ContributeAsync(owner.Id, fund.Id, new(oWallet.Id, 2_000_000, "o-full-t10", Pin, config.Revision), default);

        // Advance 25h -> member missed deadline and is auto-kicked
        clock.UtcNow = clock.UtcNow.AddHours(25);
        await service.ProcessFixedFundDeadlinesAsync(fund.Id, default);

        var treasury = await f.Db.V1FundTreasuries.AsNoTracking().SingleAsync(t => t.FundId == fund.Id);
        var windowEnd = treasury.ReplacementWindowExpiresAt!.Value;

        // Advance clock past 72h window
        clock.UtcNow = windowEnd.AddHours(1);

        var invRep = new V1FundInvitation
        {
            FundId = fund.Id,
            InvitedUserId = replacement.Id,
            InvitedByUserId = owner.Id,
            Status = V1InvitationStatus.Pending,
            ExpiresAt = clock.UtcNow.AddDays(7),
            IsReplacement = true,
            TargetSeat = 2
        };
        f.Db.V1FundInvitations.Add(invRep);
        await f.Db.SaveChangesAsync();

        // Attempting to accept after 72h window closed -> rejected!
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            service.AcceptAsync(replacement.Id, invRep.Id, true, "v2.0", default));
        Assert.Equal("replacement_window_expired", ex.Code);
    }

    [Fact]
    public async Task T11_FailFixedPeriodEarly_WhenVacantSeatsExist()
    {
        var clock = new MutableClock(DateTime.UtcNow);
        await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);
        var owner = await f.AddUserAsync("OwnerT11");
        var memberB = await f.AddUserAsync("MemberBT11");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, memberB.Id);

        var service = CreateService(f.Db, clock, f);
        var fund = await service.CreateAsync(owner.Id, new("FundT11", "Fixed", 4_000_000, Pin, 24, true, "v2.0"), default);
        var invB = await service.InviteAsync(owner.Id, fund.Id, memberB.Id, default);
        await service.AcceptAsync(memberB.Id, invB.Id, true, "v2.0", default);
        var config = await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(4_000_000, Pin), default);

        // Owner cannot fail early when all seats are full
        var exFull = await Assert.ThrowsAsync<ConflictException>(() =>
            service.FailFixedPeriodEarlyAsync(owner.Id, fund.Id, Pin, default));
        Assert.Equal("no_vacant_seats", exFull.Code);

        // Owner contributes 2,000,000 VND within deadline
        var oWallet = await f.AddWalletAsync(owner.Id, "OWalletT11", true, balance: 3_000_000);
        await service.ContributeAsync(owner.Id, fund.Id, new(oWallet.Id, 2_000_000, "c-o-t11", Pin, config.Revision), default);

        // Advance 25h -> Member B auto-kicked, leaving seat 2 vacant
        clock.UtcNow = clock.UtcNow.AddHours(25);
        await service.ProcessFixedFundDeadlinesAsync(fund.Id, default);

        // Owner chooses to fail period early without waiting 72 hours
        var dissolveResult = await service.FailFixedPeriodEarlyAsync(owner.Id, fund.Id, Pin, default);
        Assert.NotNull(dissolveResult);
        Assert.Equal(2_000_000, dissolveResult.TotalRefundedVnd);

        // Verify fund dissolved and balance is 0
        var dbFund = await f.Db.V1Funds.AsNoTracking().SingleAsync(x => x.Id == fund.Id);
        Assert.NotNull(dbFund.DissolvedAt);

        var treasury = await f.Db.V1FundTreasuries.AsNoTracking().SingleAsync(t => t.FundId == fund.Id);
        var tAcc = await f.Db.LedgerAccounts.AsNoTracking().SingleAsync(a => a.Id == treasury.LedgerAccountId);
        Assert.Equal(0, tAcc.Balance);

        // Owner wallet restored
        var oAcc = await f.Db.LedgerAccounts.AsNoTracking().SingleAsync(a => a.WalletId == oWallet.Id);
        Assert.Equal(3_000_000, oAcc.Balance);
    }

    [Fact]
    public async Task T12_ConsecutiveReplacements_PartialPaidAndRefunded_CannotOpenSpending()
    {
        var clock = new MutableClock(DateTime.UtcNow);
        await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);
        var owner = await f.AddUserAsync("OwnerT12");
        var memberB = await f.AddUserAsync("MemberBT12");
        var memberC = await f.AddUserAsync("MemberCT12");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, memberB.Id);
        await V1FundMembershipTests.MakeFriends(f, owner.Id, memberC.Id);

        var service = CreateService(f.Db, clock, f);
        var fund = await service.CreateAsync(owner.Id, new("FundT12", "Fixed", 4_000_000, Pin, 24, true, "v2.0"), default);
        var invB = await service.InviteAsync(owner.Id, fund.Id, memberB.Id, default);
        await service.AcceptAsync(memberB.Id, invB.Id, true, "v2.0", default);
        var config = await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(4_000_000, Pin), default);

        // Owner contributes full 2,000,000 VND
        var oWallet = await f.AddWalletAsync(owner.Id, "OWalletT12", true, balance: 2_000_000);
        await service.ContributeAsync(owner.Id, fund.Id, new(oWallet.Id, 2_000_000, "o-full-t12", Pin, config.Revision), default);

        // Member B contributes 1,000,000 / 2,000,000 VND partial
        var bWallet = await f.AddWalletAsync(memberB.Id, "BWalletT12", true, balance: 1_000_000);
        await service.ContributeAsync(memberB.Id, fund.Id, new(bWallet.Id, 1_000_000, "b-part-t12", Pin, config.Revision), default);

        // Advance 25h -> Member B auto-kicked, refunded 1,000,000 VND
        clock.UtcNow = clock.UtcNow.AddHours(25);
        var res1 = await service.ProcessFixedFundDeadlinesAsync(fund.Id, default);
        Assert.Equal("MembersAutoKicked", res1.Status);
        Assert.Contains(memberB.Id, res1.AutoKickedUserIds);

        // Replacement member C joins seat 2 within 72h window
        var invC = new V1FundInvitation
        {
            FundId = fund.Id,
            InvitedUserId = memberC.Id,
            InvitedByUserId = owner.Id,
            Status = V1InvitationStatus.Pending,
            ExpiresAt = clock.UtcNow.AddDays(7),
            IsReplacement = true,
            TargetSeat = 2
        };
        f.Db.V1FundInvitations.Add(invC);
        await f.Db.SaveChangesAsync();
        await service.AcceptAsync(memberC.Id, invC.Id, true, "v2.0", default);

        // Replacement member C contributes 1,000,000 / 2,000,000 VND partial
        var cWallet = await f.AddWalletAsync(memberC.Id, "CWalletT12", true, balance: 1_000_000);
        await service.ContributeAsync(memberC.Id, fund.Id, new(cWallet.Id, 1_000_000, "c-part-t12", Pin, config.Revision), default);

        // Advance past C's 24h deadline -> Member C auto-kicked, refunded 1,000,000 VND
        clock.UtcNow = clock.UtcNow.AddHours(25);
        var res2 = await service.ProcessFixedFundDeadlinesAsync(fund.Id, default);
        Assert.Equal("MembersAutoKicked", res2.Status);
        Assert.Contains(memberC.Id, res2.AutoKickedUserIds);

        // VERIFICATION:
        // Historical payments sum to 2M (Owner) + 1M (B) + 1M (C) = 4M.
        // BUT B and C were refunded! Real balance in treasury is ONLY 2,000,000 VND!
        // Seat 2 is currently vacant!
        var finance = await service.FinanceAsync(owner.Id, fund.Id, default);
        Assert.Equal("FINANCE_BLOCKED", finance.SpendingState);

        // Attempting to create an expense request MUST fail because vacant seats exist and active paid < target!
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            service.CreateFundRequestAsync(owner.Id, fund.Id, new("Expense", 1_000_000, owner.Id, "Chi mua dụng cụ", "expense-t12", Pin), default));
        Assert.True(ex.Code == "fixed_vacant_seats" || ex.Code == "fixed_group_funding_incomplete");
    }

    [Fact]
    public async Task T13_Scheduler_ProgressFixedFundDeadlines_AutoKicksAndFailsDeadlines()
    {
        var clock = new MutableClock(DateTime.UtcNow);
        await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);
        var owner = await f.AddUserAsync("OwnerT13");
        var member = await f.AddUserAsync("MemberT13");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);

        var service = CreateService(f.Db, clock, f);
        var fund = await service.CreateAsync(owner.Id, new("FundT13", "Fixed", 4_000_000, Pin, 24, true, "v2.0"), default);
        var inv = await service.InviteAsync(owner.Id, fund.Id, member.Id, default);
        await service.AcceptAsync(member.Id, inv.Id, true, "v2.0", default);
        var config = await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(4_000_000, Pin), default);

        // Owner contributes full
        var oWallet = await f.AddWalletAsync(owner.Id, "OWalletT13", true, balance: 2_000_000);
        await service.ContributeAsync(owner.Id, fund.Id, new(oWallet.Id, 2_000_000, "o-full-t13", Pin, config.Revision), default);

        // Advance 25h past deadline
        clock.UtcNow = clock.UtcNow.AddHours(25);

        // Background worker executes ProgressFixedFundDeadlinesAsync without manual fund-specific trigger
        var processed = await service.ProgressFixedFundDeadlinesAsync(default);
        Assert.True(processed >= 1);

        // Member is auto-kicked and 72h window opened
        var treasury = await f.Db.V1FundTreasuries.AsNoTracking().SingleAsync(t => t.FundId == fund.Id);
        Assert.NotNull(treasury.ReplacementWindowExpiresAt);

        var activeMembers = await service.MembersAsync(owner.Id, fund.Id, default);
        Assert.Single(activeMembers);
    }

    [Fact]
    public async Task T14_FailPeriodEarly_OwnerAndUnauthorizedChecks()
    {
        var clock = new MutableClock(DateTime.UtcNow);
        await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);
        var owner = await f.AddUserAsync("OwnerT14");
        var memberB = await f.AddUserAsync("MemberBT14");
        var stranger = await f.AddUserAsync("StrangerT14");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, memberB.Id);

        var service = CreateService(f.Db, clock, f);
        var fund = await service.CreateAsync(owner.Id, new("FundT14", "Fixed", 4_000_000, Pin, 24, true, "v2.0"), default);
        var invB = await service.InviteAsync(owner.Id, fund.Id, memberB.Id, default);
        await service.AcceptAsync(memberB.Id, invB.Id, true, "v2.0", default);
        var config = await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(4_000_000, Pin), default);

        // Advance 25h -> Member B auto-kicked
        var oWallet = await f.AddWalletAsync(owner.Id, "OWalletT14", true, balance: 2_000_000);
        await service.ContributeAsync(owner.Id, fund.Id, new(oWallet.Id, 2_000_000, "o-t14", Pin, config.Revision), default);
        clock.UtcNow = clock.UtcNow.AddHours(25);
        await service.ProcessFixedFundDeadlinesAsync(fund.Id, default);

        // 1. Non-owner (stranger) attempts to fail period early -> rejected!
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.FailFixedPeriodEarlyAsync(stranger.Id, fund.Id, Pin, default));

        // 2. Owner with wrong PIN -> rejected!
        await Assert.ThrowsAsync<AppException>(() =>
            service.FailFixedPeriodEarlyAsync(owner.Id, fund.Id, "999999", default));

        // 3. Valid owner with correct PIN -> succeeds!
        var result = await service.FailFixedPeriodEarlyAsync(owner.Id, fund.Id, Pin, default);
        Assert.NotNull(result);
        Assert.Equal(2_000_000, result.TotalRefundedVnd);

        var dbFund = await f.Db.V1Funds.AsNoTracking().SingleAsync(x => x.Id == fund.Id);
        Assert.NotNull(dbFund.DissolvedAt);
    }

    [Fact]
    public async Task T15_ReplacementWindow_ExactBoundaryExpired_RejectsAcceptance()
    {
        var clock = new MutableClock(DateTime.UtcNow);
        await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);
        var owner = await f.AddUserAsync("OwnerT15");
        var member = await f.AddUserAsync("MemberT15");
        var replacement = await f.AddUserAsync("ReplacementT15");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);
        await V1FundMembershipTests.MakeFriends(f, owner.Id, replacement.Id);

        var service = CreateService(f.Db, clock, f);
        var fund = await service.CreateAsync(owner.Id, new("FundT15", "Fixed", 4_000_000, Pin, 24, true, "v2.0"), default);
        var inv = await service.InviteAsync(owner.Id, fund.Id, member.Id, default);
        await service.AcceptAsync(member.Id, inv.Id, true, "v2.0", default);
        var config = await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(4_000_000, Pin), default);

        var oWallet = await f.AddWalletAsync(owner.Id, "OWalletT15", true, balance: 2_000_000);
        await service.ContributeAsync(owner.Id, fund.Id, new(oWallet.Id, 2_000_000, "o-t15", Pin, config.Revision), default);

        // Advance 25h -> member auto-kicked, opening 72h window
        clock.UtcNow = clock.UtcNow.AddHours(25);
        await service.ProcessFixedFundDeadlinesAsync(fund.Id, default);

        var treasury = await f.Db.V1FundTreasuries.AsNoTracking().SingleAsync(t => t.FundId == fund.Id);
        var windowEnd = treasury.ReplacementWindowExpiresAt!.Value;

        var invRep = new V1FundInvitation
        {
            FundId = fund.Id,
            InvitedUserId = replacement.Id,
            InvitedByUserId = owner.Id,
            Status = V1InvitationStatus.Pending,
            ExpiresAt = clock.UtcNow.AddDays(7),
            IsReplacement = true,
            TargetSeat = 2
        };
        f.Db.V1FundInvitations.Add(invRep);
        await f.Db.SaveChangesAsync();

        // Advance clock to EXACT boundary of window expiration
        clock.UtcNow = windowEnd;

        // At exact windowEnd, acceptance must be rejected!
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            service.AcceptAsync(replacement.Id, invRep.Id, true, "v2.0", default));
        Assert.Equal("replacement_window_expired", ex.Code);
    }

    [Fact]
    public async Task T16_Worker_FundA_Fails_FundB_ProcessesSuccessfully_WithIsolatedDbContexts()
    {
        var clock = new MutableClock(DateTime.UtcNow);
        await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);
        var ownerA = await f.AddUserAsync("OwnerT16A");
        var memberA = await f.AddUserAsync("MemberT16A");
        var ownerB = await f.AddUserAsync("OwnerT16B");
        var memberB = await f.AddUserAsync("MemberT16B");

        await V1FundMembershipTests.MakeFriends(f, ownerA.Id, memberA.Id);
        await V1FundMembershipTests.MakeFriends(f, ownerB.Id, memberB.Id);

        // Build real IServiceScopeFactory where each scope creates a fresh SpendWiseDbContext attached to the test fixture connection
        var services = new ServiceCollection();
        services.AddScoped<ISpendWiseDbContext>(_ => f.CreateNewDbContext());
        services.AddSingleton<IClock>(clock);
        services.AddScoped(sp =>
        {
            var scopedDb = (SpendWise.Infrastructure.Persistence.SpendWiseDbContext)sp.GetRequiredService<ISpendWiseDbContext>();
            return new FinancialSecurityGuard(scopedDb, new SecurityPinService(), new AccountCredentialLockout(scopedDb, clock));
        });
        services.AddScoped<INotificationService>(sp =>
            new SpendWise.Infrastructure.Notifications.NotificationService((SpendWise.Infrastructure.Persistence.SpendWiseDbContext)sp.GetRequiredService<ISpendWiseDbContext>()));
        services.AddScoped<ILedgerService>(sp =>
            new SpendWise.Infrastructure.Finance.LedgerService((SpendWise.Infrastructure.Persistence.SpendWiseDbContext)sp.GetRequiredService<ISpendWiseDbContext>(), clock));
        services.AddScoped<IActiveBankGuard>(sp =>
            new SpendWise.Application.Features.Banking.ActiveBankGuard((SpendWise.Infrastructure.Persistence.SpendWiseDbContext)sp.GetRequiredService<ISpendWiseDbContext>()));
        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(new V1ElectionPolicy
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
        }));
        services.AddSingleton<IV1InactivityEvidence>(new TestInactivity());
        services.AddLogging();
        services.AddScoped<V1FundService>();

        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        // Root service wired with IServiceScopeFactory so ProgressFixedFundDeadlinesAsync executes using scopes.CreateScope()
        var service = CreateService(f.Db, clock, f, scopes: scopeFactory);

        // Fund A setup: Target 4,000,000 VND (2,000,000 VND each)
        var fundA = await service.CreateAsync(ownerA.Id, new("FundT16A", "Fixed", 4_000_000, Pin, 24, true, "v2.0"), default);
        var invA = await service.InviteAsync(ownerA.Id, fundA.Id, memberA.Id, default);
        await service.AcceptAsync(memberA.Id, invA.Id, true, "v2.0", default);
        var configA = await service.ConfirmObligationsAsync(ownerA.Id, fundA.Id, new(4_000_000, Pin), default);
        var oWalletA = await f.AddWalletAsync(ownerA.Id, "OWalletT16A", true, balance: 2_000_000);
        await service.ContributeAsync(ownerA.Id, fundA.Id, new(oWalletA.Id, 2_000_000, "o-a-t16", Pin, configA.Revision), default);

        // Member A contributes partial (> 0 VND) so refund is attempted during Auto-Kick
        var aWallet = await f.AddWalletAsync(memberA.Id, "AWalletT16", true, balance: 1_000_000);
        await service.ContributeAsync(memberA.Id, fundA.Id, new(aWallet.Id, 1_000_000, "a-part-t16", Pin, configA.Revision), default);

        // Fund B setup: Target 4,000,000 VND (2,000,000 VND each)
        var fundB = await service.CreateAsync(ownerB.Id, new("FundT16B", "Fixed", 4_000_000, Pin, 24, true, "v2.0"), default);
        var invB = await service.InviteAsync(ownerB.Id, fundB.Id, memberB.Id, default);
        await service.AcceptAsync(memberB.Id, invB.Id, true, "v2.0", default);
        var configB = await service.ConfirmObligationsAsync(ownerB.Id, fundB.Id, new(4_000_000, Pin), default);
        var oWalletB = await f.AddWalletAsync(ownerB.Id, "OWalletT16B", true, balance: 2_000_000);
        await service.ContributeAsync(ownerB.Id, fundB.Id, new(oWalletB.Id, 2_000_000, "o-b-t16", Pin, configB.Revision), default);

        // Member B contributes partial (> 0 VND) so refund is processed during Auto-Kick
        var bWallet = await f.AddWalletAsync(memberB.Id, "BWalletT16", true, balance: 500_000);
        await service.ContributeAsync(memberB.Id, fundB.Id, new(bWallet.Id, 500_000, "b-part-t16", Pin, configB.Revision), default);

        // Advance 25h past deadline
        clock.UtcNow = clock.UtcNow.AddHours(25);

        // Corrupt Member A's destination wallet by removing it so Auto-Kick refund throws ConflictException("refund_wallet_unavailable")
        var aWallets = await f.Db.Wallets.Where(w => w.OwnerUserId == memberA.Id).ToListAsync();
        foreach (var w in aWallets) { w.Status = WalletStatus.Closed; }
        await f.Db.SaveChangesAsync();

        // Worker executes ProgressFixedFundDeadlinesAsync:
        // Fund A executes in its own IServiceScope and fresh DbContext, encounters refund error, logs and rolls back transaction.
        // Fund B executes in its own independent IServiceScope and fresh DbContext, succeeding without corruption!
        var processed = await service.ProgressFixedFundDeadlinesAsync(default);
        Assert.Equal(1, processed);

        // Verify isolation and rollback with a brand new DbContext
        await using var verifyDb = f.CreateNewDbContext();

        // Fund A: Transaction rolled back completely, no partial state
        var memA = await verifyDb.V1FundMemberships.AsNoTracking().SingleAsync(m => m.FundId == fundA.Id && m.UserId == memberA.Id);
        Assert.Null(memA.LeftAt);
        Assert.True(memA.RefundedVnd is null or 0);
        var treasuryA = await verifyDb.V1FundTreasuries.AsNoTracking().SingleAsync(t => t.FundId == fundA.Id);
        Assert.Null(treasuryA.ReplacementWindowExpiresAt);

        // Fund B: Processed successfully, Member B auto-kicked, refunded, 72h window opened
        var memB = await verifyDb.V1FundMemberships.AsNoTracking().SingleAsync(m => m.FundId == fundB.Id && m.UserId == memberB.Id);
        Assert.NotNull(memB.LeftAt);
        Assert.Equal(500_000, memB.RefundedVnd);
        var treasuryB = await verifyDb.V1FundTreasuries.AsNoTracking().SingleAsync(t => t.FundId == fundB.Id);
        Assert.NotNull(treasuryB.ReplacementWindowExpiresAt);
        var membersB = await verifyDb.V1FundMemberships.AsNoTracking().Where(m => m.FundId == fundB.Id && m.LeftAt == null).ToListAsync();
        Assert.Single(membersB);
    }

    [Fact]
    public async Task T17_SystemEvents_AutoKickAndFundingFailure_DoNotUpdateUserLastActionAt()
    {
        var clock = new MutableClock(DateTime.UtcNow);
        await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);

        // -------------------------------------------------------------
        // PART 1: Member Auto-Kick System Event
        // -------------------------------------------------------------
        var owner1 = await f.AddUserAsync("OwnerT17_1");
        var member1 = await f.AddUserAsync("MemberT17_1");
        await V1FundMembershipTests.MakeFriends(f, owner1.Id, member1.Id);

        var service = CreateService(f.Db, clock, f);
        var fund1 = await service.CreateAsync(owner1.Id, new("FundT17_1", "Fixed", 4_000_000, Pin, 24, true, "v2.0"), default);
        var inv1 = await service.InviteAsync(owner1.Id, fund1.Id, member1.Id, default);
        await service.AcceptAsync(member1.Id, inv1.Id, true, "v2.0", default);
        var config1 = await service.ConfirmObligationsAsync(owner1.Id, fund1.Id, new(4_000_000, Pin), default);

        var oWallet1 = await f.AddWalletAsync(owner1.Id, "OWalletT17_1", true, balance: 2_000_000);
        await service.ContributeAsync(owner1.Id, fund1.Id, new(oWallet1.Id, 2_000_000, "o1-t17", Pin, config1.Revision), default);

        // Record member1's baseline LastActionAt
        var initialMemberAction = await f.Db.V1UserActivities.AsNoTracking().FirstOrDefaultAsync(a => a.UserId == member1.Id);
        var baselineMemberLastActionAt = initialMemberAction?.LastActionAt;

        // Advance 25h past deadline
        clock.UtcNow = clock.UtcNow.AddHours(25);

        // Auto-Kick triggered by system worker
        var result1 = await service.ProcessFixedFundDeadlinesAsync(fund1.Id, default);
        Assert.Equal("MembersAutoKicked", result1.Status);

        // 1. Audit Activity recorded with ActorUserId == null (System Event) and SubjectUserId == member1.Id
        var autoKickActivity = await f.Db.V1FundActivities.AsNoTracking()
            .SingleAsync(a => a.FundId == fund1.Id && a.Event == "MemberAutoKicked");
        Assert.Null(autoKickActivity.ActorUserId);
        Assert.Equal(member1.Id, autoKickActivity.SubjectUserId);

        // 2. Member1's LastActionAt in V1UserActivities was NOT updated by Auto-Kick!
        var afterMemberAction = await f.Db.V1UserActivities.AsNoTracking().FirstOrDefaultAsync(a => a.UserId == member1.Id);
        Assert.Equal(baselineMemberLastActionAt, afterMemberAction?.LastActionAt);

        // 3. Notification specifically targets auto-kick event payload
        var autoKickNotif = await f.Db.Notifications.AsNoTracking()
            .SingleOrDefaultAsync(n => n.UserId == member1.Id && (n.Message == "MemberAutoKicked" || (n.DataJson != null && n.DataJson.Contains("MemberAutoKicked"))));
        Assert.NotNull(autoKickNotif);
        Assert.Equal("MemberAutoKicked", autoKickNotif.Message);
        Assert.Contains("MemberAutoKicked", autoKickNotif.DataJson!);
        Assert.Contains(member1.Id.ToString(), autoKickNotif.DataJson!);

        // -------------------------------------------------------------
        // PART 2: Owner Funding Failure System Event
        // -------------------------------------------------------------
        var owner2 = await f.AddUserAsync("OwnerT17_2");
        var member2 = await f.AddUserAsync("MemberT17_2");
        await V1FundMembershipTests.MakeFriends(f, owner2.Id, member2.Id);

        var fund2 = await service.CreateAsync(owner2.Id, new("FundT17_2", "Fixed", 4_000_000, Pin, 24, true, "v2.0"), default);
        var inv2 = await service.InviteAsync(owner2.Id, fund2.Id, member2.Id, default);
        await service.AcceptAsync(member2.Id, inv2.Id, true, "v2.0", default);
        var config2 = await service.ConfirmObligationsAsync(owner2.Id, fund2.Id, new(4_000_000, Pin), default);

        // Member2 contributes full 2,000,000 VND, Owner2 contributes 0 VND (fails deadline)
        var mWallet2 = await f.AddWalletAsync(member2.Id, "MWalletT17_2", true, balance: 2_000_000);
        await service.ContributeAsync(member2.Id, fund2.Id, new(mWallet2.Id, 2_000_000, "m2-t17", Pin, config2.Revision), default);

        // Record owner2's baseline LastActionAt
        var initialOwnerAction = await f.Db.V1UserActivities.AsNoTracking().FirstOrDefaultAsync(a => a.UserId == owner2.Id);
        var baselineOwnerLastActionAt = initialOwnerAction?.LastActionAt;

        // Advance 25h past deadline
        clock.UtcNow = clock.UtcNow.AddHours(25);

        // Owner funding failure triggered by system worker
        var result2 = await service.ProcessFixedFundDeadlinesAsync(fund2.Id, default);
        Assert.Equal("OwnerFundingFailed", result2.Status);
        Assert.True(result2.Dissolved);

        // 1. Audit Activity recorded with ActorUserId == null (System Event) and SubjectUserId == owner2.Id
        var ownerFailedActivity = await f.Db.V1FundActivities.AsNoTracking()
            .SingleAsync(a => a.FundId == fund2.Id && a.Event == "OwnerFundingFailed");
        Assert.Null(ownerFailedActivity.ActorUserId);
        Assert.Equal(owner2.Id, ownerFailedActivity.SubjectUserId);

        // 2. Owner2's LastActionAt in V1UserActivities was NOT updated by OwnerFundingFailed!
        var afterOwnerAction = await f.Db.V1UserActivities.AsNoTracking().FirstOrDefaultAsync(a => a.UserId == owner2.Id);
        Assert.Equal(baselineOwnerLastActionAt, afterOwnerAction?.LastActionAt);

        // 3. System event notification delivered for OwnerFundingFailed
        var ownerFailedNotifs = await f.Db.Notifications.AsNoTracking()
            .Where(n => (n.UserId == owner2.Id || n.UserId == member2.Id) && (n.Message == "OwnerFundingFailed" || (n.DataJson != null && n.DataJson.Contains("OwnerFundingFailed"))))
            .ToListAsync();
        Assert.Equal(2, ownerFailedNotifs.Count);
    }
}
