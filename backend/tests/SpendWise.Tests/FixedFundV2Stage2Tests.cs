using Microsoft.EntityFrameworkCore;
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

    private static V1FundService CreateService(SpendWise.Infrastructure.Persistence.SpendWiseDbContext db, IClock clock, TargetedCorrectnessFixture f) => new(
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
        new TestInactivity());

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
}
