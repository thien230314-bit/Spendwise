using SpendWise.Infrastructure.Persistence;
using SpendWise.Infrastructure.Identity;
using SpendWise.Application.Features.Authentication;
using SpendWise.Application.Notifications;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features;
using SpendWise.Application.Features.V1Groups;
using SpendWise.Domain.Finance;
using SpendWise.Domain.V1Groups;
using Xunit;

namespace SpendWise.Tests;

public class FreeFundMembershipRefundTests
{
    private sealed class MutableClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; set; } = utcNow;
    }

    private sealed class ConcurrentMonotonicClock(DateTime baseTime) : IClock
    {
        private long _offsetTicks;
        public DateTime UtcNow
        {
            get => baseTime.AddTicks(System.Threading.Interlocked.Add(ref _offsetTicks, TimeSpan.FromSeconds(1).Ticks));
            set => System.Threading.Interlocked.Exchange(ref _offsetTicks, (value - baseTime).Ticks);
        }
    }

    [Fact]
    public async Task FreeFund_Leave_RefundsExactEquity_FromCurrentMembershipEpisode()
    {
        // Scenario 1: A contributes 600k, B contributes 400k. Shared expense 200k.
        // A pays 120k (remains 480k), B pays 80k (remains 320k).
        // B leaves -> receives exactly 320,000 VND. A remains with 480,000 VND.
        var clock = new MutableClock(new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc));
        await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);
        var owner = await f.AddUserAsync("OwnerA");
        var memberB = await f.AddUserAsync("MemberB");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, memberB.Id);

        var s = V1FundMembershipTests.Service(f);
        var fund = await s.CreateAsync(owner.Id, new("LeaveFund", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);

        var invite = await s.InviteAsync(owner.Id, fund.Id, memberB.Id, default);
        await s.AcceptAsync(memberB.Id, invite.Id, default);

        var ownerWallet = await f.AddWalletAsync(owner.Id, "OwnerWallet", true, balance: 1_000_000);
        var memberBWallet = await f.AddWalletAsync(memberB.Id, "MemberBWallet", true, balance: 1_000_000);

        await s.ContributeAsync(owner.Id, fund.Id,
            new(ownerWallet.Id, 600_000, "c-a", TargetedCorrectnessFixture.SecurityPin), default);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);

        await s.ContributeAsync(memberB.Id, fund.Id,
            new(memberBWallet.Id, 400_000, "c-b", TargetedCorrectnessFixture.SecurityPin), default);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);

        // Direct expense 200k
        await s.CreateFundRequestAsync(owner.Id, fund.Id,
            new("Expense", 200_000, owner.Id, "Team Dinner", "exp-team", TargetedCorrectnessFixture.SecurityPin), default);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);

        // Preview refund for B
        var preview = await s.PreviewRefundAsync(memberB.Id, fund.Id, null, default);
        Assert.Equal(320_000, preview.EligibleRefundAmountVnd);
        Assert.Equal(800_000, preview.CurrentFundBalanceVnd);

        // B leaves fund
        var leaveResult = await s.LeaveFundAsync(memberB.Id, fund.Id,
            new(TargetedCorrectnessFixture.SecurityPin, "leave-b-key"), default);

        Assert.Equal(320_000, leaveResult.RefundAmountVnd);
        Assert.Equal("Completed", leaveResult.Status);

        // Verify B's wallet received 320,000 VND
        var bAccountAfter = await f.Db.LedgerAccounts.AsNoTracking().FirstAsync(a => a.WalletId == memberBWallet.Id);
        Assert.Equal(600_000 + 320_000, bAccountAfter.Balance);

        // Verify fund treasury balance is now 480,000 VND
        var treasury = await f.Db.V1FundTreasuries.AsNoTracking().FirstAsync(t => t.FundId == fund.Id);
        var treasuryAccount = await f.Db.LedgerAccounts.AsNoTracking().FirstAsync(a => a.Id == treasury.LedgerAccountId);
        Assert.Equal(480_000, treasuryAccount.Balance);

        // Verify B membership has LeftAt set
        var memB = await f.Db.V1FundMemberships.AsNoTracking().FirstAsync(m => m.FundId == fund.Id && m.UserId == memberB.Id);
        Assert.NotNull(memB.LeftAt);
        Assert.Equal(320_000, memB.RefundedVnd);
    }

    [Fact]
    public async Task FreeFund_Leave_WhenMemberHasPendingReview_ThrowsMemberPendingRequests()
    {
        var clock = new MutableClock(new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc));
        await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);
        var owner = await f.AddUserAsync("OwnerA");
        var memberB = await f.AddUserAsync("MemberB");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, memberB.Id);

        var s = V1FundMembershipTests.Service(f);
        var fund = await s.CreateAsync(owner.Id, new("PendingReviewFund", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);

        var invite = await s.InviteAsync(owner.Id, fund.Id, memberB.Id, default);
        await s.AcceptAsync(memberB.Id, invite.Id, default);

        var ownerWallet = await f.AddWalletAsync(owner.Id, "OwnerWallet", true, balance: 1_000_000);
        var memberBWallet = await f.AddWalletAsync(memberB.Id, "MemberBWallet", true, balance: 1_000_000);

        await s.ContributeAsync(owner.Id, fund.Id,
            new(ownerWallet.Id, 500_000, "c-a", TargetedCorrectnessFixture.SecurityPin), default);
        await s.ContributeAsync(memberB.Id, fund.Id,
            new(memberBWallet.Id, 500_000, "c-b", TargetedCorrectnessFixture.SecurityPin), default);

        // Member B creates request in AwaitingOwnerReview
        var req = await s.CreateFundRequestAsync(memberB.Id, fund.Id,
            new("Expense", 100_000, memberB.Id, "Taxi", "exp-taxi", TargetedCorrectnessFixture.SecurityPin), default);
        Assert.Equal("AwaitingOwnerReview", req.State);

        // B attempts to leave: rejected because of pending request
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            s.LeaveFundAsync(memberB.Id, fund.Id, new(TargetedCorrectnessFixture.SecurityPin, "leave-b-pending"), default));
        Assert.Equal("member_pending_requests", ex.Code);

        // B cancels the request
        await s.CancelFundRequestAsync(memberB.Id, req.Id, TargetedCorrectnessFixture.SecurityPin, default);

        // Now B can leave successfully
        var leaveResult = await s.LeaveFundAsync(memberB.Id, fund.Id,
            new(TargetedCorrectnessFixture.SecurityPin, "leave-b-ok"), default);
        Assert.Equal(500_000, leaveResult.RefundAmountVnd);
    }

    [Fact]
    public async Task FreeFund_Leave_CancelsOwnApprovedUnusedExpense_AndRefundsCleanly()
    {
        var clock = new MutableClock(new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc));
        await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);
        var owner = await f.AddUserAsync("OwnerA");
        var memberB = await f.AddUserAsync("MemberB");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, memberB.Id);

        var s = V1FundMembershipTests.Service(f);
        var fund = await s.CreateAsync(owner.Id, new("CancelOwnApprovedFund", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);

        var invite = await s.InviteAsync(owner.Id, fund.Id, memberB.Id, default);
        await s.AcceptAsync(memberB.Id, invite.Id, default);

        var ownerWallet = await f.AddWalletAsync(owner.Id, "OwnerWallet", true, balance: 1_000_000);
        var memberBWallet = await f.AddWalletAsync(memberB.Id, "MemberBWallet", true, balance: 1_000_000);

        await s.ContributeAsync(owner.Id, fund.Id,
            new(ownerWallet.Id, 500_000, "c-a", TargetedCorrectnessFixture.SecurityPin), default);
        await s.ContributeAsync(memberB.Id, fund.Id,
            new(memberBWallet.Id, 500_000, "c-b", TargetedCorrectnessFixture.SecurityPin), default);

        // B creates expense and Owner approves -> ApprovedUnused
        var req = await s.CreateFundRequestAsync(memberB.Id, fund.Id,
            new("Expense", 200_000, memberB.Id, "Office supplies", "exp-supplies", TargetedCorrectnessFixture.SecurityPin), default);
        await s.ReviewFundRequestAsync(owner.Id, req.Id, true, "Approved", TargetedCorrectnessFixture.SecurityPin, default);

        // B leaves: B's own ApprovedUnused request is automatically cancelled, and B gets full 500k refund
        var leaveResult = await s.LeaveFundAsync(memberB.Id, fund.Id,
            new(TargetedCorrectnessFixture.SecurityPin, "leave-b-approved"), default);
        Assert.Equal(500_000, leaveResult.RefundAmountVnd);

        var reqAfter = await f.Db.V1FundRequests.AsNoTracking().FirstAsync(r => r.Id == req.Id);
        Assert.Equal("Cancelled", reqAfter.State);
    }

    [Fact]
    public async Task FreeFund_Leave_CancelsOtherActiveApprovedUnusedNewestFirst_WhenBalanceInsufficient()
    {
        // User.md row 65:
        // Fund 1,000,000đ (A 400k, B 300k, C 300k).
        // B has ApprovedUnused 800k.
        // C leaves and gets refunded 300k -> Fund remaining balance = 700k.
        // B's 800k reservation > 700k -> B's 800k request is cancelled, C leaves with 300k!
        var clock = new MutableClock(new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc));
        await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);
        var ownerA = await f.AddUserAsync("OwnerA");
        var memberB = await f.AddUserAsync("MemberB");
        var memberC = await f.AddUserAsync("MemberC");
        await V1FundMembershipTests.MakeFriends(f, ownerA.Id, memberB.Id);
        await V1FundMembershipTests.MakeFriends(f, ownerA.Id, memberC.Id);

        var s = V1FundMembershipTests.Service(f);
        var fund = await s.CreateAsync(ownerA.Id, new("HeadroomFund", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);

        var inviteB = await s.InviteAsync(ownerA.Id, fund.Id, memberB.Id, default);
        await s.AcceptAsync(memberB.Id, inviteB.Id, default);
        var inviteC = await s.InviteAsync(ownerA.Id, fund.Id, memberC.Id, default);
        await s.AcceptAsync(memberC.Id, inviteC.Id, default);

        var walletA = await f.AddWalletAsync(ownerA.Id, "WalletA", true, balance: 1_000_000);
        var walletB = await f.AddWalletAsync(memberB.Id, "WalletB", true, balance: 1_000_000);
        var walletC = await f.AddWalletAsync(memberC.Id, "WalletC", true, balance: 1_000_000);

        await s.ContributeAsync(ownerA.Id, fund.Id, new(walletA.Id, 400_000, "c-a", TargetedCorrectnessFixture.SecurityPin), default);
        await s.ContributeAsync(memberB.Id, fund.Id, new(walletB.Id, 300_000, "c-b", TargetedCorrectnessFixture.SecurityPin), default);
        await s.ContributeAsync(memberC.Id, fund.Id, new(walletC.Id, 300_000, "c-c", TargetedCorrectnessFixture.SecurityPin), default);

        // B creates expense of 800,000 VND and Owner approves
        var reqB = await s.CreateFundRequestAsync(memberB.Id, fund.Id,
            new("Expense", 800_000, memberB.Id, "Big event", "exp-big", TargetedCorrectnessFixture.SecurityPin), default);
        await s.ReviewFundRequestAsync(ownerA.Id, reqB.Id, true, "Approved", TargetedCorrectnessFixture.SecurityPin, default);

        // C leaves: C receives 300,000 VND. Because 700k < 800k, B's ApprovedUnused is automatically cancelled!
        var leaveResult = await s.LeaveFundAsync(memberC.Id, fund.Id,
            new(TargetedCorrectnessFixture.SecurityPin, "leave-c-key"), default);

        Assert.Equal(300_000, leaveResult.RefundAmountVnd);

        var reqBAfter = await f.Db.V1FundRequests.AsNoTracking().FirstAsync(r => r.Id == reqB.Id);
        Assert.Equal("Cancelled", reqBAfter.State);
        Assert.Contains("không còn đủ số dư dự trù", reqBAfter.ReviewReason);

        var treasury = await f.Db.V1FundTreasuries.AsNoTracking().FirstAsync(t => t.FundId == fund.Id);
        var treasuryAccount = await f.Db.LedgerAccounts.AsNoTracking().FirstAsync(a => a.Id == treasury.LedgerAccountId);
        Assert.Equal(700_000, treasuryAccount.Balance);
    }

    [Fact]
    public async Task FreeFund_Owner_CannotLeaveFreeFund_ThrowsOwnerCannotLeaveFreeFund()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var ownerA = await f.AddUserAsync("OwnerA");
        var memberB = await f.AddUserAsync("MemberB");
        await V1FundMembershipTests.MakeFriends(f, ownerA.Id, memberB.Id);

        var s = V1FundMembershipTests.Service(f);
        var fund = await s.CreateAsync(ownerA.Id, new("OwnerLeaveFund", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);

        var invite = await s.InviteAsync(ownerA.Id, fund.Id, memberB.Id, default);
        await s.AcceptAsync(memberB.Id, invite.Id, default);

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            s.LeaveFundAsync(ownerA.Id, fund.Id, new(TargetedCorrectnessFixture.SecurityPin, "owner-leave"), default));
        Assert.Equal("owner_cannot_leave_free_fund", ex.Code);
    }

    [Fact]
    public async Task FreeFund_Kick_RefundsTargetExactEquity_AndCancelsTargetApprovedUnused()
    {
        var clock = new MutableClock(new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc));
        await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);
        var ownerA = await f.AddUserAsync("OwnerA");
        var memberB = await f.AddUserAsync("MemberB");
        await V1FundMembershipTests.MakeFriends(f, ownerA.Id, memberB.Id);

        var s = V1FundMembershipTests.Service(f);
        var fund = await s.CreateAsync(ownerA.Id, new("KickFund", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);

        var invite = await s.InviteAsync(ownerA.Id, fund.Id, memberB.Id, default);
        await s.AcceptAsync(memberB.Id, invite.Id, default);

        var walletA = await f.AddWalletAsync(ownerA.Id, "WalletA", true, balance: 1_000_000);
        var walletB = await f.AddWalletAsync(memberB.Id, "WalletB", true, balance: 1_000_000);

        await s.ContributeAsync(ownerA.Id, fund.Id, new(walletA.Id, 600_000, "c-a", TargetedCorrectnessFixture.SecurityPin), default);
        await s.ContributeAsync(memberB.Id, fund.Id, new(walletB.Id, 400_000, "c-b", TargetedCorrectnessFixture.SecurityPin), default);

        // B has ApprovedUnused 100k
        var reqB = await s.CreateFundRequestAsync(memberB.Id, fund.Id,
            new("Expense", 100_000, memberB.Id, "Tools", "exp-tools", TargetedCorrectnessFixture.SecurityPin), default);
        await s.ReviewFundRequestAsync(ownerA.Id, reqB.Id, true, "Approved", TargetedCorrectnessFixture.SecurityPin, default);

        // Owner kicks B
        var kickResult = await s.KickMemberAsync(ownerA.Id, fund.Id, memberB.Id,
            new(TargetedCorrectnessFixture.SecurityPin, "Vi phạm nội quy", "kick-b-key"), default);

        Assert.Equal(400_000, kickResult.RefundAmountVnd);
        Assert.Equal("Completed", kickResult.Status);

        // B's ApprovedUnused is cancelled
        var reqBAfter = await f.Db.V1FundRequests.AsNoTracking().FirstAsync(r => r.Id == reqB.Id);
        Assert.Equal("Cancelled", reqBAfter.State);

        // B's wallet received 400k
        var walletBAfter = await f.Db.LedgerAccounts.AsNoTracking().FirstAsync(a => a.WalletId == walletB.Id);
        Assert.Equal(600_000 + 400_000, walletBAfter.Balance);
    }

    [Fact]
    public async Task FreeFund_Kick_WhenTargetHasPendingReview_ThrowsMemberPendingRequests()
    {
        var clock = new MutableClock(new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc));
        await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);
        var ownerA = await f.AddUserAsync("OwnerA");
        var memberB = await f.AddUserAsync("MemberB");
        await V1FundMembershipTests.MakeFriends(f, ownerA.Id, memberB.Id);

        var s = V1FundMembershipTests.Service(f);
        var fund = await s.CreateAsync(ownerA.Id, new("KickPendingFund", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);

        var invite = await s.InviteAsync(ownerA.Id, fund.Id, memberB.Id, default);
        await s.AcceptAsync(memberB.Id, invite.Id, default);

        var walletA = await f.AddWalletAsync(ownerA.Id, "WalletA", true, balance: 1_000_000);
        var walletB = await f.AddWalletAsync(memberB.Id, "WalletB", true, balance: 1_000_000);

        await s.ContributeAsync(ownerA.Id, fund.Id, new(walletA.Id, 500_000, "c-a", TargetedCorrectnessFixture.SecurityPin), default);
        await s.ContributeAsync(memberB.Id, fund.Id, new(walletB.Id, 500_000, "c-b", TargetedCorrectnessFixture.SecurityPin), default);

        // B creates request awaiting review
        var reqB = await s.CreateFundRequestAsync(memberB.Id, fund.Id,
            new("Withdrawal", 200_000, memberB.Id, "Need cash", "wth-need", TargetedCorrectnessFixture.SecurityPin), default);

        // Owner attempts to kick B while request is pending -> Throws member_pending_requests
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            s.KickMemberAsync(ownerA.Id, fund.Id, memberB.Id,
                new(TargetedCorrectnessFixture.SecurityPin, "Reason", "kick-fail-key"), default));
        Assert.Equal("member_pending_requests", ex.Code);

        // Owner rejects the request
        await s.ReviewFundRequestAsync(ownerA.Id, reqB.Id, false, "Rejected by owner", TargetedCorrectnessFixture.SecurityPin, default);

        // Now kick succeeds
        var kickResult = await s.KickMemberAsync(ownerA.Id, fund.Id, memberB.Id,
            new(TargetedCorrectnessFixture.SecurityPin, "Reason", "kick-ok-key"), default);
        Assert.Equal(500_000, kickResult.RefundAmountVnd);
    }

    [Fact]
    public async Task FreeFund_Dissolve_RefundsAllActiveMembers_AndBringsTreasuryToZero()
    {
        var clock = new MutableClock(new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc));
        await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);
        var ownerA = await f.AddUserAsync("OwnerA");
        var memberB = await f.AddUserAsync("MemberB");
        var memberC = await f.AddUserAsync("MemberC");
        await V1FundMembershipTests.MakeFriends(f, ownerA.Id, memberB.Id);
        await V1FundMembershipTests.MakeFriends(f, ownerA.Id, memberC.Id);

        var s = V1FundMembershipTests.Service(f);
        var fund = await s.CreateAsync(ownerA.Id, new("DissolveFund", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);

        var inviteB = await s.InviteAsync(ownerA.Id, fund.Id, memberB.Id, default);
        await s.AcceptAsync(memberB.Id, inviteB.Id, default);
        var inviteC = await s.InviteAsync(ownerA.Id, fund.Id, memberC.Id, default);
        await s.AcceptAsync(memberC.Id, inviteC.Id, default);

        var walletA = await f.AddWalletAsync(ownerA.Id, "WalletA", true, balance: 1_000_000);
        var walletB = await f.AddWalletAsync(memberB.Id, "WalletB", true, balance: 1_000_000);
        var walletC = await f.AddWalletAsync(memberC.Id, "WalletC", true, balance: 1_000_000);

        await s.ContributeAsync(ownerA.Id, fund.Id, new(walletA.Id, 500_000, "c-a", TargetedCorrectnessFixture.SecurityPin), default);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);

        await s.ContributeAsync(memberB.Id, fund.Id, new(walletB.Id, 300_000, "c-b", TargetedCorrectnessFixture.SecurityPin), default);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);

        await s.ContributeAsync(memberC.Id, fund.Id, new(walletC.Id, 200_000, "c-c", TargetedCorrectnessFixture.SecurityPin), default);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);

        // Expense 100k: A pays 50k, B pays 30k, C pays 20k -> Remaining: A 450k, B 270k, C 180k. Total = 900k.
        await s.CreateFundRequestAsync(ownerA.Id, fund.Id,
            new("Expense", 100_000, ownerA.Id, "Coffee", "exp-coffee", TargetedCorrectnessFixture.SecurityPin), default);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);

        // Active request awaiting review and approved unused request
        var reqPending = await s.CreateFundRequestAsync(memberB.Id, fund.Id,
            new("Withdrawal", 50_000, memberB.Id, "Small wth", "wth-small", TargetedCorrectnessFixture.SecurityPin), default);

        // Owner dissolves fund
        var dissolveResult = await s.DissolveFundAsync(ownerA.Id, fund.Id,
            new(TargetedCorrectnessFixture.SecurityPin, "dissolve-key"), default);

        Assert.Equal(900_000, dissolveResult.TotalRefundedVnd);
        Assert.Equal(0, dissolveResult.FinalBalanceVnd);
        Assert.Equal(3, dissolveResult.Refunds.Count);

        var refundA = dissolveResult.Refunds.Single(r => r.UserId == ownerA.Id);
        var refundB = dissolveResult.Refunds.Single(r => r.UserId == memberB.Id);
        var refundC = dissolveResult.Refunds.Single(r => r.UserId == memberC.Id);

        Assert.Equal(450_000, refundA.RefundAmountVnd);
        Assert.Equal(270_000, refundB.RefundAmountVnd);
        Assert.Equal(180_000, refundC.RefundAmountVnd);

        // Pending request was cancelled
        var reqPendingAfter = await f.Db.V1FundRequests.AsNoTracking().FirstAsync(r => r.Id == reqPending.Id);
        Assert.Equal("Cancelled", reqPendingAfter.State);

        // Treasury balance is exactly 0 VND
        var treasury = await f.Db.V1FundTreasuries.AsNoTracking().FirstAsync(t => t.FundId == fund.Id);
        var treasuryAccount = await f.Db.LedgerAccounts.AsNoTracking().FirstAsync(a => a.Id == treasury.LedgerAccountId);
        Assert.Equal(0, treasuryAccount.Balance);

        // Fund has DissolvedAt set
        var fundAfter = await f.Db.V1Funds.AsNoTracking().FirstAsync(f => f.Id == fund.Id);
        Assert.NotNull(fundAfter.DissolvedAt);
    }

    [Fact]
    public async Task FreeFund_Governance_ElectionsAndOwnershipTransfers_AreDisallowed()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var ownerA = await f.AddUserAsync("OwnerA");
        var memberB = await f.AddUserAsync("MemberB");
        await V1FundMembershipTests.MakeFriends(f, ownerA.Id, memberB.Id);

        var s = V1FundMembershipTests.Service(f);
        var fund = await s.CreateAsync(ownerA.Id, new("GovFund", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);

        var invite = await s.InviteAsync(ownerA.Id, fund.Id, memberB.Id, default);
        await s.AcceptAsync(memberB.Id, invite.Id, default);

        // 1. Election disallowed
        var exElection = await Assert.ThrowsAsync<ConflictException>(() =>
            s.OpenElectionAsync(memberB.Id, fund.Id, new(0), default));
        Assert.Equal("free_fund_election_disallowed", exElection.Code);

        // 2. Ownership offer disallowed
        var exOffer = await Assert.ThrowsAsync<ConflictException>(() =>
            s.OfferOwnershipAsync(ownerA.Id, fund.Id, memberB.Id, TargetedCorrectnessFixture.SecurityPin, default));
        Assert.Equal("free_fund_ownership_transfer_disallowed", exOffer.Code);

        // 3. Activity cycle close disallowed
        var exClose = await Assert.ThrowsAsync<ConflictException>(() =>
            s.CloseActivityAsync(ownerA.Id, fund.Id, TargetedCorrectnessFixture.SecurityPin, default));
        Assert.Equal("fixed_fund_required", exClose.Code);
    }

    [Fact]
    public async Task FreeFund_Leave_IdempotentReplay_ReturnsPreviousResultWithoutDoubleDebit()
    {
        var clock = new MutableClock(new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc));
        await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);
        var ownerA = await f.AddUserAsync("OwnerA");
        var memberB = await f.AddUserAsync("MemberB");
        await V1FundMembershipTests.MakeFriends(f, ownerA.Id, memberB.Id);

        var s = V1FundMembershipTests.Service(f);
        var fund = await s.CreateAsync(ownerA.Id, new("IdempotentLeaveFund", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);

        var invite = await s.InviteAsync(ownerA.Id, fund.Id, memberB.Id, default);
        await s.AcceptAsync(memberB.Id, invite.Id, default);

        var walletA = await f.AddWalletAsync(ownerA.Id, "WalletA", true, balance: 1_000_000);
        var walletB = await f.AddWalletAsync(memberB.Id, "WalletB", true, balance: 1_000_000);

        await s.ContributeAsync(ownerA.Id, fund.Id, new(walletA.Id, 500_000, "c-a", TargetedCorrectnessFixture.SecurityPin), default);
        await s.ContributeAsync(memberB.Id, fund.Id, new(walletB.Id, 500_000, "c-b", TargetedCorrectnessFixture.SecurityPin), default);

        // 1st call
        var result1 = await s.LeaveFundAsync(memberB.Id, fund.Id,
            new(TargetedCorrectnessFixture.SecurityPin, "replay-leave-key"), default);
        Assert.Equal(500_000, result1.RefundAmountVnd);

        // 2nd call (replay)
        var result2 = await s.LeaveFundAsync(memberB.Id, fund.Id,
            new(TargetedCorrectnessFixture.SecurityPin, "replay-leave-key"), default);
        Assert.Equal(500_000, result2.RefundAmountVnd);
        Assert.Equal("Replayed", result2.CaseType);

        // Wallet balance increased by exactly 500,000 VND (not double)
        var walletBAfter = await f.Db.LedgerAccounts.AsNoTracking().FirstAsync(a => a.WalletId == walletB.Id);
        Assert.Equal(500_000 + 500_000, walletBAfter.Balance);
    }

    private sealed class FaultyNotificationService(INotificationService inner) : INotificationService
    {
        public bool FailOnCreateMany { get; set; } = true;

        public Task<NotificationDto> CreateAsync(Guid userId, string type, string title, string message, object? data, CancellationToken cancellationToken) =>
            inner.CreateAsync(userId, type, title, message, data, cancellationToken);

        public Task<IReadOnlyList<NotificationDto>> CreateManyAsync(
            IReadOnlyCollection<CreateNotificationRequest> requests,
            CancellationToken cancellationToken)
        {
            if (FailOnCreateMany)
            {
                throw new InvalidOperationException("Injected notification failure during refund activity.");
            }
            return inner.CreateManyAsync(requests, cancellationToken);
        }
    }

    private static V1FundService CreateService(SpendWiseDbContext db, IClock clock, INotificationService? notif = null)
    {
        var sec = new FinancialSecurityGuard(db, new SecurityPinService(), new AccountCredentialLockout(db, clock));
        var n = notif ?? new SpendWise.Infrastructure.Notifications.NotificationService(db);
        var ledger = new SpendWise.Infrastructure.Finance.LedgerService(db, clock);
        var bankGuard = new SpendWise.Application.Features.Banking.ActiveBankGuard(db);
        return new V1FundService(db, clock, sec, n, ledger, bankGuard);
    }

    [Fact]
    public async Task FreeFund_Leave_Rejoin_IdempotencyConflict_AndEpisodeIsolation()
    {
        var clock = new MutableClock(new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc));
        await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);
        var ownerA = await f.AddUserAsync("OwnerA");
        var memberB = await f.AddUserAsync("MemberB");
        await V1FundMembershipTests.MakeFriends(f, ownerA.Id, memberB.Id);

        var s = V1FundMembershipTests.Service(f);
        var fund = await s.CreateAsync(ownerA.Id, new("RejoinFund", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);

        var invite1 = await s.InviteAsync(ownerA.Id, fund.Id, memberB.Id, default);
        await s.AcceptAsync(memberB.Id, invite1.Id, default);

        var walletA = await f.AddWalletAsync(ownerA.Id, "WalletA", true, balance: 1_000_000);
        var walletB = await f.AddWalletAsync(memberB.Id, "WalletB", true, balance: 1_000_000);

        await s.ContributeAsync(ownerA.Id, fund.Id, new(walletA.Id, 500_000, "c-a1", TargetedCorrectnessFixture.SecurityPin), default);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);
        await s.ContributeAsync(memberB.Id, fund.Id, new(walletB.Id, 500_000, "c-b1", TargetedCorrectnessFixture.SecurityPin), default);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);

        // Episode 1: B leaves with key K1
        var leave1 = await s.LeaveFundAsync(memberB.Id, fund.Id,
            new(TargetedCorrectnessFixture.SecurityPin, "leave-key-ep1"), default);
        Assert.Equal(500_000, leave1.RefundAmountVnd);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);

        // B rejoins fund (Episode 2)
        var invite2 = await s.InviteAsync(ownerA.Id, fund.Id, memberB.Id, default);
        await s.AcceptAsync(memberB.Id, invite2.Id, default);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);

        // B contributes 200k in Episode 2
        await s.ContributeAsync(memberB.Id, fund.Id, new(walletB.Id, 200_000, "c-b2", TargetedCorrectnessFixture.SecurityPin), default);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);

        // B attempts to leave Episode 2 using K1 from Episode 1 -> throws idempotency_conflict
        var exConflict = await Assert.ThrowsAsync<ConflictException>(() =>
            s.LeaveFundAsync(memberB.Id, fund.Id, new(TargetedCorrectnessFixture.SecurityPin, "leave-key-ep1"), default));
        Assert.Equal("idempotency_conflict", exConflict.Code);

        // B leaves Episode 2 using new key K2 -> succeeds with Episode 2 equity (200k)
        var leave2 = await s.LeaveFundAsync(memberB.Id, fund.Id,
            new(TargetedCorrectnessFixture.SecurityPin, "leave-key-ep2"), default);
        Assert.Equal(200_000, leave2.RefundAmountVnd);
        Assert.Equal("Completed", leave2.Status);

        // Replaying K1 now returns past left replay for Episode 1 without conflict or double debit
        var replay1 = await s.LeaveFundAsync(memberB.Id, fund.Id,
            new(TargetedCorrectnessFixture.SecurityPin, "leave-key-ep1"), default);
        Assert.Equal(500_000, replay1.RefundAmountVnd);
        Assert.Equal("Replayed", replay1.CaseType);
    }

    [Fact]
    public async Task FreeFund_Leave_MultiRequestHeadroom_CancelsNewestFirst_KeepsOlderRequest()
    {
        // User.md row 66:
        // Fund 800k. B approved 500k (older), C approved 300k (newer).
        // Member D refund leaves fund at 600k -> Cancels C (300k, newest), keeps B (500k, older, 500k <= 600k).
        var clock = new MutableClock(new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc));
        await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);
        var ownerA = await f.AddUserAsync("OwnerA");
        var memberB = await f.AddUserAsync("MemberB");
        var memberC = await f.AddUserAsync("MemberC");
        var memberD = await f.AddUserAsync("MemberD");
        await V1FundMembershipTests.MakeFriends(f, ownerA.Id, memberB.Id);
        await V1FundMembershipTests.MakeFriends(f, ownerA.Id, memberC.Id);
        await V1FundMembershipTests.MakeFriends(f, ownerA.Id, memberD.Id);

        var s = V1FundMembershipTests.Service(f);
        var fund = await s.CreateAsync(ownerA.Id, new("MultiHeadroomFund", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);

        var invB = await s.InviteAsync(ownerA.Id, fund.Id, memberB.Id, default);
        await s.AcceptAsync(memberB.Id, invB.Id, default);
        var invC = await s.InviteAsync(ownerA.Id, fund.Id, memberC.Id, default);
        await s.AcceptAsync(memberC.Id, invC.Id, default);
        var invD = await s.InviteAsync(ownerA.Id, fund.Id, memberD.Id, default);
        await s.AcceptAsync(memberD.Id, invD.Id, default);

        var walletA = await f.AddWalletAsync(ownerA.Id, "WalletA", true, balance: 1_000_000);
        var walletB = await f.AddWalletAsync(memberB.Id, "WalletB", true, balance: 1_000_000);
        var walletC = await f.AddWalletAsync(memberC.Id, "WalletC", true, balance: 1_000_000);
        var walletD = await f.AddWalletAsync(memberD.Id, "WalletD", true, balance: 1_000_000);

        // Total 800k: A=300k, B=200k, C=100k, D=200k
        await s.ContributeAsync(ownerA.Id, fund.Id, new(walletA.Id, 300_000, "c-a", TargetedCorrectnessFixture.SecurityPin), default);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);
        await s.ContributeAsync(memberB.Id, fund.Id, new(walletB.Id, 200_000, "c-b", TargetedCorrectnessFixture.SecurityPin), default);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);
        await s.ContributeAsync(memberC.Id, fund.Id, new(walletC.Id, 100_000, "c-c", TargetedCorrectnessFixture.SecurityPin), default);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);
        await s.ContributeAsync(memberD.Id, fund.Id, new(walletD.Id, 200_000, "c-d", TargetedCorrectnessFixture.SecurityPin), default);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);

        // B creates 500k request, approved at T1 (older)
        var reqB = await s.CreateFundRequestAsync(memberB.Id, fund.Id,
            new("Expense", 500_000, memberB.Id, "Equip B", "exp-b", TargetedCorrectnessFixture.SecurityPin), default);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);
        await s.ReviewFundRequestAsync(ownerA.Id, reqB.Id, true, "Approved B", TargetedCorrectnessFixture.SecurityPin, default);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);

        // C creates 300k request, approved at T2 (newer)
        var reqC = await s.CreateFundRequestAsync(memberC.Id, fund.Id,
            new("Expense", 300_000, memberC.Id, "Equip C", "exp-c", TargetedCorrectnessFixture.SecurityPin), default);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);
        await s.ReviewFundRequestAsync(ownerA.Id, reqC.Id, true, "Approved C", TargetedCorrectnessFixture.SecurityPin, default);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);

        // D leaves fund: D's equity is 200k. Fund balance after refund = 800k - 200k = 600,000 VND.
        // Total active ApprovedUnused = 500k + 300k = 800k > 600k.
        // Cancellation loop cancels newest (C, 300k). Remaining = 500k <= 600k.
        // B (500k) is KEPT!
        var leaveResult = await s.LeaveFundAsync(memberD.Id, fund.Id,
            new(TargetedCorrectnessFixture.SecurityPin, "leave-d-headroom"), default);
        Assert.Equal(200_000, leaveResult.RefundAmountVnd);

        // Verify C is Cancelled
        var reqCAfter = await f.Db.V1FundRequests.AsNoTracking().FirstAsync(r => r.Id == reqC.Id);
        Assert.Equal("Cancelled", reqCAfter.State);
        Assert.Contains("không còn đủ số dư dự trù", reqCAfter.ReviewReason);

        // Verify B is STILL ApprovedUnused
        var reqBAfter = await f.Db.V1FundRequests.AsNoTracking().FirstAsync(r => r.Id == reqB.Id);
        Assert.Equal("ApprovedUnused", reqBAfter.State);
        Assert.Null(reqBAfter.CancelledAt);

        // Verify Treasury balance is exactly 600,000 VND
        var treasury = await f.Db.V1FundTreasuries.AsNoTracking().FirstAsync(t => t.FundId == fund.Id);
        var treasuryAccount = await f.Db.LedgerAccounts.AsNoTracking().FirstAsync(a => a.Id == treasury.LedgerAccountId);
        Assert.Equal(600_000, treasuryAccount.Balance);
    }

    [Fact]
    public async Task FreeFund_ConcurrentLeaveAndKick_OnlyOneSucceeds_LedgerConsistent()
    {
        var clock = new ConcurrentMonotonicClock(new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc));
        await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);
        var ownerA = await f.AddUserAsync("OwnerA");
        var memberB = await f.AddUserAsync("MemberB");
        await V1FundMembershipTests.MakeFriends(f, ownerA.Id, memberB.Id);

        var s = V1FundMembershipTests.Service(f);
        var fund = await s.CreateAsync(ownerA.Id, new("ConcurrentLeaveKickFund", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);

        var invite = await s.InviteAsync(ownerA.Id, fund.Id, memberB.Id, default);
        await s.AcceptAsync(memberB.Id, invite.Id, default);

        var walletA = await f.AddWalletAsync(ownerA.Id, "WalletA", true, balance: 1_000_000);
        var walletB = await f.AddWalletAsync(memberB.Id, "WalletB", true, balance: 1_000_000);

        await s.ContributeAsync(ownerA.Id, fund.Id, new(walletA.Id, 500_000, "c-a", TargetedCorrectnessFixture.SecurityPin), default);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);
        await s.ContributeAsync(memberB.Id, fund.Id, new(walletB.Id, 500_000, "c-b", TargetedCorrectnessFixture.SecurityPin), default);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);

        await using var db1 = f.CreateNewDbContext();
        await using var db2 = f.CreateNewDbContext();
        var s1 = CreateService(db1, clock);
        var s2 = CreateService(db2, clock);

        var t1 = Task.Run(async () =>
        {
            try
            {
                var res = await s1.LeaveFundAsync(memberB.Id, fund.Id, new(TargetedCorrectnessFixture.SecurityPin, "concurrent-leave"), default);
                return (Success: true, Result: (V1RefundResultDto?)res, Error: (string?)null);
            }
            catch (Exception ex)
            {
                return (Success: false, Result: (V1RefundResultDto?)null, Error: ex.ToString());
            }
        });

        var t2 = Task.Run(async () =>
        {
            try
            {
                var res = await s2.KickMemberAsync(ownerA.Id, fund.Id, memberB.Id, new(TargetedCorrectnessFixture.SecurityPin, "Rule violation", "concurrent-kick"), default);
                return (Success: true, Result: (V1RefundResultDto?)res, Error: (string?)null);
            }
            catch (Exception ex)
            {
                return (Success: false, Result: (V1RefundResultDto?)null, Error: ex.ToString());
            }
        });

        var results = await Task.WhenAll(t1, t2);
        var successes = results.Count(r => r.Success);
        var failures = results.Count(r => !r.Success);

        Assert.Equal(1, successes);
        Assert.Equal(1, failures);

        await using var verifyDb = f.CreateNewDbContext();
        var refundTxs = await verifyDb.LedgerTransactions.AsNoTracking()
            .Where(t => t.BusinessType == "V1FundRefund" && t.BusinessId == fund.Id)
            .ToListAsync();
        Assert.Single(refundTxs);

        var refundFts = await verifyDb.FinancialTransactions.AsNoTracking()
            .Where(f => f.RelatedBusinessId == fund.Id && f.Type == FinancialTransactionType.Refund)
            .ToListAsync();
        Assert.Single(refundFts);
        Assert.Equal(500_000, refundFts[0].Amount);

        var treasury = await verifyDb.V1FundTreasuries.AsNoTracking().FirstAsync(t => t.FundId == fund.Id);
        var treasuryAccount = await verifyDb.LedgerAccounts.AsNoTracking().FirstAsync(a => a.Id == treasury.LedgerAccountId);
        Assert.Equal(500_000, treasuryAccount.Balance);

        var walletBAccount = await verifyDb.LedgerAccounts.AsNoTracking().FirstAsync(a => a.WalletId == walletB.Id);
        Assert.Equal(1_000_000, walletBAccount.Balance);
    }

    [Fact]
    public async Task FreeFund_ConcurrentLeaveAndDissolve_PreservesZeroBalanceInvariant()
    {
        var clock = new ConcurrentMonotonicClock(new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc));
        await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);
        var ownerA = await f.AddUserAsync("OwnerA");
        var memberB = await f.AddUserAsync("MemberB");
        await V1FundMembershipTests.MakeFriends(f, ownerA.Id, memberB.Id);

        var s = V1FundMembershipTests.Service(f);
        var fund = await s.CreateAsync(ownerA.Id, new("ConcurrentLeaveDissolveFund", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);

        var invite = await s.InviteAsync(ownerA.Id, fund.Id, memberB.Id, default);
        await s.AcceptAsync(memberB.Id, invite.Id, default);

        var walletA = await f.AddWalletAsync(ownerA.Id, "WalletA", true, balance: 1_000_000);
        var walletB = await f.AddWalletAsync(memberB.Id, "WalletB", true, balance: 1_000_000);

        await s.ContributeAsync(ownerA.Id, fund.Id, new(walletA.Id, 600_000, "c-a", TargetedCorrectnessFixture.SecurityPin), default);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);
        await s.ContributeAsync(memberB.Id, fund.Id, new(walletB.Id, 400_000, "c-b", TargetedCorrectnessFixture.SecurityPin), default);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);

        await using var db1 = f.CreateNewDbContext();
        await using var db2 = f.CreateNewDbContext();
        var s1 = CreateService(db1, clock);
        var s2 = CreateService(db2, clock);

        var taskLeave = Task.Run(async () =>
        {
            try
            {
                await s1.LeaveFundAsync(memberB.Id, fund.Id, new(TargetedCorrectnessFixture.SecurityPin, "conc-leave"), default);
                return (Op: "Leave", Success: true, Error: (string?)null);
            }
            catch (Exception ex)
            {
                return (Op: "Leave", Success: false, Error: ex.ToString());
            }
        });

        var taskDissolve = Task.Run(async () =>
        {
            try
            {
                await s2.DissolveFundAsync(ownerA.Id, fund.Id, new(TargetedCorrectnessFixture.SecurityPin, "conc-dissolve"), default);
                return (Op: "Dissolve", Success: true, Error: (string?)null);
            }
            catch (Exception ex)
            {
                return (Op: "Dissolve", Success: false, Error: ex.ToString());
            }
        });

        var resLeave = await taskLeave;
        var resDissolve = await taskDissolve;

        // If dissolve was rejected due to concurrent transaction lock, retry dissolve to complete dissolution
        if (!resDissolve.Success)
        {
            await using var dbRetry = f.CreateNewDbContext();
            var sRetry = CreateService(dbRetry, clock);
            await sRetry.DissolveFundAsync(ownerA.Id, fund.Id, new(TargetedCorrectnessFixture.SecurityPin, "conc-dissolve-retry"), default);
        }

        // Verify Invariants on fresh context
        await using var verifyDb = f.CreateNewDbContext();
        var fundDb = await verifyDb.V1Funds.AsNoTracking().FirstAsync(u => u.Id == fund.Id);
        Assert.NotNull(fundDb.DissolvedAt);

        var treasury = await verifyDb.V1FundTreasuries.AsNoTracking().FirstAsync(t => t.FundId == fund.Id);
        var treasuryAccount = await verifyDb.LedgerAccounts.AsNoTracking().FirstAsync(a => a.Id == treasury.LedgerAccountId);
        Assert.Equal(0, treasuryAccount.Balance);

        var totalRefunds = await verifyDb.FinancialTransactions.AsNoTracking()
            .Where(f => f.RelatedBusinessId == fund.Id && f.Type == FinancialTransactionType.Refund)
            .SumAsync(f => f.Amount);
        Assert.Equal(1_000_000, totalRefunds);
    }

    [Fact]
    public async Task FreeFund_Leave_RollsBackTransaction_WhenNotificationFails()
    {
        var clock = new MutableClock(new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc));
        await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);
        var ownerA = await f.AddUserAsync("OwnerA");
        var memberB = await f.AddUserAsync("MemberB");
        await V1FundMembershipTests.MakeFriends(f, ownerA.Id, memberB.Id);

        var s = V1FundMembershipTests.Service(f);
        var fund = await s.CreateAsync(ownerA.Id, new("RollbackLeaveFund", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);

        var invite = await s.InviteAsync(ownerA.Id, fund.Id, memberB.Id, default);
        await s.AcceptAsync(memberB.Id, invite.Id, default);

        var walletA = await f.AddWalletAsync(ownerA.Id, "WalletA", true, balance: 1_000_000);
        var walletB = await f.AddWalletAsync(memberB.Id, "WalletB", true, balance: 1_000_000);

        await s.ContributeAsync(ownerA.Id, fund.Id, new(walletA.Id, 500_000, "c-a", TargetedCorrectnessFixture.SecurityPin), default);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);
        await s.ContributeAsync(memberB.Id, fund.Id, new(walletB.Id, 500_000, "c-b", TargetedCorrectnessFixture.SecurityPin), default);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);

        // Inject faulty notification
        await using var isolatedDb = f.CreateNewDbContext();
        var baseNotif = new SpendWise.Infrastructure.Notifications.NotificationService(isolatedDb);
        var faultyNotif = new FaultyNotificationService(baseNotif) { FailOnCreateMany = true };
        var faultyService = CreateService(isolatedDb, clock, faultyNotif);

        // Attempt leave: throws during notification inside transaction
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            faultyService.LeaveFundAsync(memberB.Id, fund.Id, new(TargetedCorrectnessFixture.SecurityPin, "leave-fail"), default));
        Assert.Contains("Injected notification failure", ex.Message);

        // Verify transaction rolled back on fresh context:
        await using var verifyDb = f.CreateNewDbContext();
        var memB = await verifyDb.V1FundMemberships.AsNoTracking().FirstAsync(m => m.FundId == fund.Id && m.UserId == memberB.Id);
        Assert.Null(memB.LeftAt);
        Assert.Equal(0, memB.RefundedVnd);

        var refundTxs = await verifyDb.LedgerTransactions.AsNoTracking()
            .Where(t => t.BusinessType == "V1FundRefund" && t.BusinessId == fund.Id)
            .ToListAsync();
        Assert.Empty(refundTxs);

        var treasury = await verifyDb.V1FundTreasuries.AsNoTracking().FirstAsync(t => t.FundId == fund.Id);
        var treasuryAccount = await verifyDb.LedgerAccounts.AsNoTracking().FirstAsync(a => a.Id == treasury.LedgerAccountId);
        Assert.Equal(1_000_000, treasuryAccount.Balance);

        // Retry on fresh request/service with healthy notification succeeds
        await using var retryDb = f.CreateNewDbContext();
        var retryNotif = new SpendWise.Infrastructure.Notifications.NotificationService(retryDb);
        var retryService = CreateService(retryDb, clock, retryNotif);
        var retryResult = await retryService.LeaveFundAsync(memberB.Id, fund.Id, new(TargetedCorrectnessFixture.SecurityPin, "leave-fail"), default);
        Assert.Equal(500_000, retryResult.RefundAmountVnd);
        Assert.Equal("Completed", retryResult.Status);

        await using var verifyDb2 = f.CreateNewDbContext();
        var memBAfter = await verifyDb2.V1FundMemberships.AsNoTracking().FirstAsync(m => m.FundId == fund.Id && m.UserId == memberB.Id);
        Assert.NotNull(memBAfter.LeftAt);
        Assert.Equal(500_000, memBAfter.RefundedVnd);
    }
}
