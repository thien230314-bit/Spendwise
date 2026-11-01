using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.V1Groups;
using SpendWise.Domain.Finance;
using SpendWise.Domain.Identity;
using SpendWise.Domain.V1Groups;
using Xunit;

namespace SpendWise.Tests;

public sealed class FixedFundV2Stage4Tests
{
    private const string Pin = TargetedCorrectnessFixture.SecurityPin;

    private sealed class MutableClock(DateTime initial) : IClock
    {
        public DateTime UtcNow { get; set; } = initial;
        public void Advance(TimeSpan d) => UtcNow += d;
    }

    private static async Task<(TargetedCorrectnessFixture F, MutableClock Clock, V1FundService Service, V1FundDto Fund, User Owner, User MemberA, User MemberB)>
        Setup3MemberFixedFundAsync(long targetVnd = 3_000_000)
    {
        var clock = new MutableClock(new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc));
        var f = await TargetedCorrectnessFixture.CreateAsync(clock);

        var owner = await f.AddUserAsync("Owner User");
        owner.EmailVerifiedAt = clock.UtcNow;
        var memberA = await f.AddUserAsync("Member A");
        memberA.EmailVerifiedAt = clock.UtcNow;
        var memberB = await f.AddUserAsync("Member B");
        memberB.EmailVerifiedAt = clock.UtcNow;
        await f.Db.SaveChangesAsync();

        await V1FundMembershipTests.MakeFriends(f, owner.Id, memberA.Id);
        await V1FundMembershipTests.MakeFriends(f, owner.Id, memberB.Id);

        var service = V1ElectionTests.Configured(f, new IncompleteInactivityEvidence());
        var fund = await service.CreateAsync(owner.Id, new("Trip Fund Fixed", "Fixed", targetVnd, Pin, 24, true, "v2.0"), default);

        var invA = await service.InviteAsync(owner.Id, fund.Id, memberA.Id, default);
        await service.AcceptAsync(memberA.Id, invA.Id, true, "v2.0", default);

        var invB = await service.InviteAsync(owner.Id, fund.Id, memberB.Id, default);
        await service.AcceptAsync(memberB.Id, invB.Id, true, "v2.0", default);

        // Confirm obligations (1,000,000 VND each)
        var config = await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(targetVnd, Pin), default);

        var wOwner = await f.AddWalletAsync(owner.Id, "WOwner", true, balance: 5_000_000);
        var wA = await f.AddWalletAsync(memberA.Id, "WA", true, balance: 5_000_000);
        var wB = await f.AddWalletAsync(memberB.Id, "WB", true, balance: 5_000_000);

        // All 3 contribute 1,000,000 VND
        await service.ContributeAsync(owner.Id, fund.Id, new(wOwner.Id, 1_000_000, "c-owner", Pin, config.Revision), default);
        await service.ContributeAsync(memberA.Id, fund.Id, new(wA.Id, 1_000_000, "c-memA", Pin, config.Revision), default);
        await service.ContributeAsync(memberB.Id, fund.Id, new(wB.Id, 1_000_000, "c-memB", Pin, config.Revision), default);

        return (f, clock, service, fund, owner, memberA, memberB);
    }

    [Fact]
    public async Task T22_InactiveOwner_15DaysEvidence_OpensElection()
    {
        var (f, clock, service, fund, owner, memberA, memberB) = await Setup3MemberFixedFundAsync();
        await using var _ = f;

        // 1. When owner was active recently (less than 15 days ago), opening election is rejected with ACTIVITY_EVIDENCE_BLOCKED
        clock.Advance(TimeSpan.FromDays(14));
        var exBlocked = await Assert.ThrowsAsync<ConflictException>(() =>
            service.OpenElectionAsync(memberA.Id, fund.Id, new V1OpenElectionRequest(0), default));
        Assert.Equal("ACTIVITY_EVIDENCE_BLOCKED", exBlocked.Code);

        // 2. Incumbent Owner cannot open election themselves
        clock.Advance(TimeSpan.FromDays(2)); // Now 16 days inactive
        var exOwner = await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.OpenElectionAsync(owner.Id, fund.Id, new V1OpenElectionRequest(0), default));
        Assert.Equal("incumbent_not_elector", exOwner.Code);

        // 3. Non-owner active member successfully opens election with 15-day evidence
        var election = await service.OpenElectionAsync(memberA.Id, fund.Id, new V1OpenElectionRequest(0), default);
        Assert.NotNull(election);
        Assert.Equal(fund.Id, election.FundId);
        Assert.Equal(V1ElectionPhase.Nomination.ToString(), election.Phase); // Fixed Fund starts directly in Đợt 1

        var dbElection = await f.Db.V1Elections.SingleAsync(e => e.Id == election.Id);
        Assert.Contains("\"NominationSeconds\":43200", dbElection.PolicyJson); // 12 hours
        Assert.Contains("\"AcceptanceSeconds\":43200", dbElection.PolicyJson); // 12 hours

        // Voters and candidates are seeded
        var voters = await f.Db.V1ElectionVoters.Where(v => v.ElectionId == election.Id).ToListAsync();
        Assert.Equal(2, voters.Count);
        Assert.Contains(voters, v => v.UserId == memberA.Id);
        Assert.Contains(voters, v => v.UserId == memberB.Id);

        var candidates = await f.Db.V1ElectionCandidates.Where(c => c.ElectionId == election.Id).ToListAsync();
        Assert.Equal(2, candidates.Count);
        Assert.Contains(candidates, c => c.UserId == memberA.Id);
        Assert.Contains(candidates, c => c.UserId == memberB.Id);

        // 4. Re-opening while election is active returns existing election
        var duplicate = await service.OpenElectionAsync(memberB.Id, fund.Id, new V1OpenElectionRequest(0), default);
        Assert.Equal(election.Id, duplicate.Id);
    }

    [Fact]
    public async Task T26_IncumbentOwnerInterferenceBlockedDuringElection()
    {
        var (f, clock, service, fund, owner, memberA, memberB) = await Setup3MemberFixedFundAsync();
        await using var _ = f;

        clock.Advance(TimeSpan.FromDays(16));
        var election = await service.OpenElectionAsync(memberA.Id, fund.Id, new V1OpenElectionRequest(0), default);

        // 1. Owner can view election progress via ElectionAsync
        var view = await service.ElectionAsync(owner.Id, election.Id, default);
        Assert.NotNull(view);
        Assert.Equal(election.Id, view.Election.Id);

        // 2. Owner cannot spend (CreateFundRequestAsync) -> election_finance_paused
        var exSpend = await Assert.ThrowsAsync<ConflictException>(() =>
            service.CreateFundRequestAsync(owner.Id, fund.Id, new V1FundRequestInput("Expense", 500_000, owner.Id, "Owner spend during election", "key-owner-spend", Pin), default));
        Assert.Equal("election_finance_paused", exSpend.Code);

        // 3. Owner cannot kick members (KickMemberAsync) -> election_in_progress
        var exKick = await Assert.ThrowsAsync<ConflictException>(() =>
            service.KickMemberAsync(owner.Id, fund.Id, memberA.Id, new V1KickMemberRequest(Pin, "Kick during election", "kick-key"), default));
        Assert.Equal("election_in_progress", exKick.Code);

        // 4. Owner cannot dissolve fund (DissolveFundAsync) -> election_in_progress
        var exDissolve = await Assert.ThrowsAsync<ConflictException>(() =>
            service.DissolveFundAsync(owner.Id, fund.Id, new V1DissolveFundRequest(Pin, "dissolve-key"), default));
        Assert.Equal("election_in_progress", exDissolve.Code);

        // 5. Owner cannot invite new regular members during election -> election_in_progress or fund_obligations_locked
        var memberC = await f.AddUserAsync("Member C");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, memberC.Id);
        var exInvite = await Assert.ThrowsAsync<ConflictException>(() =>
            service.InviteAsync(owner.Id, fund.Id, memberC.Id, default));
        Assert.True(exInvite.Code == "election_in_progress" || exInvite.Code == "fund_obligations_locked");
    }

    [Fact]
    public async Task T24_ElectionTie_Or_AllBlankVotes_FailsImmediately()
    {
        // Sub-test 1: 1-1 Tie fails immediately (RunoffTie)
        {
            var (f, clock, service, fund, owner, memberA, memberB) = await Setup3MemberFixedFundAsync();
            await using var _ = f;

            clock.Advance(TimeSpan.FromDays(16));
            var election = await service.OpenElectionAsync(memberA.Id, fund.Id, new V1OpenElectionRequest(0), default);

            // Member A votes for Member A; Member B votes for Member B
            await service.CastBallotAsync(memberA.Id, election.Id, new V1BallotRequest(1, memberA.Id, null), default);
            await service.CastBallotAsync(memberB.Id, election.Id, new V1BallotRequest(1, memberB.Id, null), default);

            // Advance election -> tie immediately results in Failed phase with RunoffTie
            var advanced = await service.AdvanceElectionAsync(memberA.Id, election.Id, default);
            Assert.Equal(V1ElectionPhase.Failed.ToString(), advanced.Phase);
            Assert.Equal("RunoffTie", advanced.FailureReason);
            Assert.NotNull(advanced.FinishedAt);
        }

        // Sub-test 2: All blank votes fail immediately (NoVotes)
        {
            var (f, clock, service, fund, owner, memberA, memberB) = await Setup3MemberFixedFundAsync();
            await using var _ = f;

            clock.Advance(TimeSpan.FromDays(16));
            var election = await service.OpenElectionAsync(memberA.Id, fund.Id, new V1OpenElectionRequest(0), default);

            // Member A and Member B cast blank votes (null candidate)
            await service.CastBallotAsync(memberA.Id, election.Id, new V1BallotRequest(1, null, null), default);
            await service.CastBallotAsync(memberB.Id, election.Id, new V1BallotRequest(1, null, null), default);

            // Advance election -> immediately fails with NoVotes
            var advanced = await service.AdvanceElectionAsync(memberA.Id, election.Id, default);
            Assert.Equal(V1ElectionPhase.Failed.ToString(), advanced.Phase);
            Assert.Equal("NoVotes", advanced.FailureReason);
            Assert.NotNull(advanced.FinishedAt);
        }
    }

    [Fact]
    public async Task T23_WinnerConfirmation_Rejection_And_Expiration()
    {
        // Sub-test 1: Acceptance succeeds -> Ownership transferred, old owner becomes member, seat/entitlements preserved
        {
            var (f, clock, service, fund, owner, memberA, memberB) = await Setup3MemberFixedFundAsync();
            await using var _ = f;

            clock.Advance(TimeSpan.FromDays(16));
            var election = await service.OpenElectionAsync(memberA.Id, fund.Id, new V1OpenElectionRequest(0), default);

            // Member A and Member B both vote for Member A
            await service.CastBallotAsync(memberA.Id, election.Id, new V1BallotRequest(1, memberA.Id, null), default);
            await service.CastBallotAsync(memberB.Id, election.Id, new V1BallotRequest(1, memberA.Id, null), default);

            // Advance to Round 2 (AwaitingAcceptance)
            var advanced = await service.AdvanceElectionAsync(memberA.Id, election.Id, default);
            Assert.Equal(V1ElectionPhase.AwaitingAcceptance.ToString(), advanced.Phase);
            Assert.Equal(memberA.Id, advanced.WinnerUserId);

            // Member A accepts ownership with PIN
            var completed = await service.AcceptElectedOwnershipAsync(memberA.Id, election.Id, Pin, default);
            Assert.Equal(V1ElectionPhase.Completed.ToString(), completed.Phase);

            // Verify fund ownership changed
            var updatedFund = await f.Db.V1Funds.AsNoTracking().SingleAsync(x => x.Id == fund.Id);
            Assert.Equal(memberA.Id, updatedFund.OwnerUserId);
            Assert.True(updatedFund.OwnershipRevision > 0);

            // Verify old Owner is still a member, seat 1 preserved
            var oldOwnerMem = await f.Db.V1FundMemberships.AsNoTracking().SingleAsync(m => m.FundId == fund.Id && m.UserId == owner.Id);
            Assert.Null(oldOwnerMem.LeftAt);
            Assert.Equal(1, oldOwnerMem.Seat);

            // Verify Member A is active member, seat 2 preserved
            var newOwnerMem = await f.Db.V1FundMemberships.AsNoTracking().SingleAsync(m => m.FundId == fund.Id && m.UserId == memberA.Id);
            Assert.Null(newOwnerMem.LeftAt);
            Assert.Equal(2, newOwnerMem.Seat);
        }

        // Sub-test 2: Winner rejects -> Election marked Failed with WinnerRejected
        {
            var (f, clock, service, fund, owner, memberA, memberB) = await Setup3MemberFixedFundAsync();
            await using var _ = f;

            clock.Advance(TimeSpan.FromDays(16));
            var election = await service.OpenElectionAsync(memberA.Id, fund.Id, new V1OpenElectionRequest(0), default);

            await service.CastBallotAsync(memberA.Id, election.Id, new V1BallotRequest(1, memberA.Id, null), default);
            await service.CastBallotAsync(memberB.Id, election.Id, new V1BallotRequest(1, memberA.Id, null), default);

            await service.AdvanceElectionAsync(memberA.Id, election.Id, default);

            // Winner rejects
            var rejected = await service.RejectElectedOwnershipAsync(memberA.Id, election.Id, Pin, default);
            Assert.Equal(V1ElectionPhase.Failed.ToString(), rejected.Phase);
            Assert.Equal("WinnerRejected", rejected.FailureReason);
            Assert.NotNull(rejected.FinishedAt);
        }

        // Sub-test 3: Winner acceptance expires after 12 hours -> Marked Failed with AcceptanceExpired
        {
            var (f, clock, service, fund, owner, memberA, memberB) = await Setup3MemberFixedFundAsync();
            await using var _ = f;

            clock.Advance(TimeSpan.FromDays(16));
            var election = await service.OpenElectionAsync(memberA.Id, fund.Id, new V1OpenElectionRequest(0), default);

            await service.CastBallotAsync(memberA.Id, election.Id, new V1BallotRequest(1, memberA.Id, null), default);
            await service.CastBallotAsync(memberB.Id, election.Id, new V1BallotRequest(1, memberA.Id, null), default);

            await service.AdvanceElectionAsync(memberA.Id, election.Id, default);

            // Advance clock past 12 hours (43,200 seconds)
            clock.Advance(TimeSpan.FromHours(13));

            var expired = await service.AdvanceElectionAsync(memberA.Id, election.Id, default);
            Assert.Equal(V1ElectionPhase.Failed.ToString(), expired.Phase);
            Assert.Equal("AcceptanceExpired", expired.FailureReason);
            Assert.NotNull(expired.FinishedAt);
        }
    }

    [Fact]
    public async Task T25_FailedElection_24hOptOut_And_CompulsoryDissolution()
    {
        var (f, clock, service, fund, owner, memberA, memberB) = await Setup3MemberFixedFundAsync();
        await using var _ = f;

        clock.Advance(TimeSpan.FromDays(16));
        var election = await service.OpenElectionAsync(memberA.Id, fund.Id, new V1OpenElectionRequest(0), default);

        // Both cast blank votes to trigger election failure
        await service.CastBallotAsync(memberA.Id, election.Id, new V1BallotRequest(1, null, null), default);
        await service.CastBallotAsync(memberB.Id, election.Id, new V1BallotRequest(1, null, null), default);
        var failed = await service.AdvanceElectionAsync(memberA.Id, election.Id, default);
        Assert.Equal(V1ElectionPhase.Failed.ToString(), failed.Phase);

        // 1. Within 24-hour window, voluntary out (LeaveFundAsync) is ALLOWED and refunds unspent entitlement
        var leaveResultA = await service.LeaveFundAsync(memberA.Id, fund.Id, new V1LeaveFundRequest(Pin, "out-memA"), default);
        Assert.Equal("Completed", leaveResultA.Status);
        Assert.Equal(1_000_000, leaveResultA.RefundAmountVnd); // Full 1,000,000 VND unspent entitlement refunded

        // Member A is now marked left
        var memARow = await f.Db.V1FundMemberships.AsNoTracking().SingleAsync(m => m.FundId == fund.Id && m.UserId == memberA.Id);
        Assert.NotNull(memARow.LeftAt);

        // 2. Compulsory settlement during 24h while Member B is still present is BLOCKED with election_opt_out_window_active
        var exBlockedSettlement = await Assert.ThrowsAsync<ConflictException>(() =>
            service.ResolveFailedElectionSettlementAsync(memberB.Id, fund.Id, default));
        Assert.Equal("election_opt_out_window_active", exBlockedSettlement.Code);

        // 3. Advance clock past 24 hours (25 hours later)
        clock.Advance(TimeSpan.FromHours(25));

        // 4. Compulsory settlement executes: remaining members (Member B and Owner) receive remaining entitlement
        var (totalRefunded, refunds) = await service.ResolveFailedElectionSettlementAsync(memberB.Id, fund.Id, default);
        Assert.Equal(2_000_000, totalRefunded); // 1,000,000 for Member B + 1,000,000 for Owner
        Assert.Equal(2, refunds.Count);

        // Verify treasury balance is 0
        var treasury = await f.Db.V1FundTreasuries.AsNoTracking().SingleAsync(t => t.FundId == fund.Id);
        var treasuryAcc = await f.Db.LedgerAccounts.AsNoTracking().SingleAsync(a => a.Id == treasury.LedgerAccountId);
        Assert.Equal(0, treasuryAcc.Balance);

        // Verify fund is dissolved
        var dissolvedFund = await f.Db.V1Funds.AsNoTracking().SingleAsync(x => x.Id == fund.Id);
        Assert.NotNull(dissolvedFund.DissolvedAt);
    }

    [Fact]
    public async Task T27_HistoryPrivacy_And_DissolvedFundIsolation()
    {
        var (f, clock, service, fund, owner, memberA, memberB) = await Setup3MemberFixedFundAsync();
        await using var _ = f;

        // 1. Owner creates an expense request before election
        clock.Advance(TimeSpan.FromHours(1));
        var expReq = await service.CreateFundRequestAsync(owner.Id, fund.Id,
            new V1FundRequestInput("Expense", 100_000, owner.Id, "Old owner secret purchase", "secret-buy", Pin), default);

        // Member A (regular member) cannot see Owner's private request event
        var memberAActsBefore = await service.ActivitiesAsync(memberA.Id, fund.Id, 1, 50, default);
        Assert.DoesNotContain(memberAActsBefore, a => a.Detail?.Contains("Old owner secret purchase") == true);

        // 2. 16 days pass, election is opened and Member A wins and accepts ownership
        clock.Advance(TimeSpan.FromDays(16));
        var election = await service.OpenElectionAsync(memberA.Id, fund.Id, new V1OpenElectionRequest(0), default);
        await service.CastBallotAsync(memberA.Id, election.Id, new V1BallotRequest(1, memberA.Id, null), default);
        await service.CastBallotAsync(memberB.Id, election.Id, new V1BallotRequest(1, memberA.Id, null), default);
        await service.AdvanceElectionAsync(memberA.Id, election.Id, default);
        await service.AcceptElectedOwnershipAsync(memberA.Id, election.Id, Pin, default);

        // 3. New Owner (Member A) still CANNOT see old Owner's private request event from before they became owner
        var memberAActsAfterWin = await service.ActivitiesAsync(memberA.Id, fund.Id, 1, 50, default);
        Assert.DoesNotContain(memberAActsAfterWin, a => a.Detail?.Contains("Old owner secret purchase") == true);

        // 4. New Owner creates an expense after election win -> visible to new Owner
        var newOwnerExp = await service.CreateFundRequestAsync(memberA.Id, fund.Id,
            new V1FundRequestInput("Expense", 50_000, memberA.Id, "New owner expense", "new-owner-exp", Pin), default);
        var memberAActsAfterNewExp = await service.ActivitiesAsync(memberA.Id, fund.Id, 1, 50, default);
        Assert.Contains(memberAActsAfterNewExp, a => a.Detail?.Contains("New owner expense") == true);

        // 5. Dissolved fund isolation: once fund is dissolved, it does not appear in ListAsync for any member
        var updatedFund = await f.Db.V1Funds.SingleAsync(x => x.Id == fund.Id);
        updatedFund.DissolvedAt = clock.UtcNow;
        await f.Db.SaveChangesAsync();

        var listOwner = await service.ListAsync(owner.Id, default);
        Assert.DoesNotContain(listOwner, x => x.Id == fund.Id);

        var listMemberA = await service.ListAsync(memberA.Id, default);
        Assert.DoesNotContain(listMemberA, x => x.Id == fund.Id);
    }

    [Fact]
    public async Task T28_P1_1_VoluntaryLeaveDisallowed_UnlessOptOutOrClosed()
    {
        var (f, clock, service, fund, owner, memberA, memberB) = await Setup3MemberFixedFundAsync();
        await using var _ = f;

        // In normal operating cycle, voluntary leave is strictly disallowed
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            service.LeaveFundAsync(memberA.Id, fund.Id, new V1LeaveFundRequest(Pin, "leave-now"), default));
        Assert.Equal("fixed_fund_voluntary_leave_disallowed", ex.Code);
    }

        [Fact]
    public async Task T29_P1_2_AutomatedWorkerCompulsorySettlement_After24h()
    {
        var (f, clock, service, fund, owner, memberA, memberB) = await Setup3MemberFixedFundAsync();
        await using var _ = f;

        clock.Advance(TimeSpan.FromDays(16));
        var election = await service.OpenElectionAsync(memberA.Id, fund.Id, new V1OpenElectionRequest(0), default);
        await service.CastBallotAsync(memberA.Id, election.Id, new V1BallotRequest(1, null, null), default);
        await service.CastBallotAsync(memberB.Id, election.Id, new V1BallotRequest(1, null, null), default);
        await service.AdvanceElectionAsync(memberA.Id, election.Id, default);

        // Even if ActivityClosedAt != null, failed election dissolution takes absolute precedence
        var dbFundOpen = await f.Db.V1Funds.SingleAsync(x => x.Id == fund.Id);
        await f.Db.V1Funds.Where(x => x.Id == fund.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.ActivityClosedAt, clock.UtcNow));

        // Advance 25 hours past election failure
        clock.Advance(TimeSpan.FromHours(25));

        // Background worker deadline progression runs
        var processedCount = await service.ProgressFixedFundDeadlinesAsync(default);
        Assert.True(processedCount >= 1);

        // Verify fund is dissolved
        var dbFund = await f.Db.V1Funds.AsNoTracking().SingleAsync(x => x.Id == fund.Id);
        Assert.NotNull(dbFund.DissolvedAt);

        // Treasury balance is 0
        var treasury = await f.Db.V1FundTreasuries.AsNoTracking().SingleAsync(t => t.FundId == fund.Id);
        var treasuryAcc = await f.Db.LedgerAccounts.AsNoTracking().SingleAsync(a => a.Id == treasury.LedgerAccountId);
        Assert.Equal(0, treasuryAcc.Balance);
    }

    [Fact]
    public async Task T30_P1_3_IncumbentOwnerOperationsFrozen_AfterElectionFailure()
    {
        var (f, clock, service, fund, owner, memberA, memberB) = await Setup3MemberFixedFundAsync();
        await using var _ = f;

        clock.Advance(TimeSpan.FromDays(16));
        var election = await service.OpenElectionAsync(memberA.Id, fund.Id, new V1OpenElectionRequest(0), default);
        await service.CastBallotAsync(memberA.Id, election.Id, new V1BallotRequest(1, null, null), default);
        await service.CastBallotAsync(memberB.Id, election.Id, new V1BallotRequest(1, null, null), default);
        await service.AdvanceElectionAsync(memberA.Id, election.Id, default);

        // Within the 24h failed election window:
        // 1. Incumbent owner expense request is frozen
        var exExp = await Assert.ThrowsAsync<ConflictException>(() =>
            service.CreateFundRequestAsync(owner.Id, fund.Id, new V1FundRequestInput("Expense", 100_000, owner.Id, "Freeze test", "freeze-exp", Pin), default));
        Assert.Equal("election_finance_paused", exExp.Code);

        // 2. Incumbent owner kick member is frozen
        var exKick = await Assert.ThrowsAsync<ConflictException>(() =>
            service.KickMemberAsync(owner.Id, fund.Id, memberA.Id, new V1KickMemberRequest(Pin, "freeze-kick", "freeze reason"), default));
        Assert.Equal("election_failed_pending_settlement", exKick.Code);

        // 3. Incumbent owner close activity is frozen
        var exClose = await Assert.ThrowsAsync<ConflictException>(() =>
            service.CloseActivityAsync(owner.Id, fund.Id, Pin, default));
        Assert.Equal("election_failed_pending_settlement", exClose.Code);

        // 4. Incumbent owner invite member is frozen
        var newUser = await f.AddUserAsync("New Invited User");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, newUser.Id);
        var exInvite = await Assert.ThrowsAsync<ConflictException>(() =>
            service.InviteAsync(owner.Id, fund.Id, newUser.Id, default));
        Assert.Equal("election_failed_pending_settlement", exInvite.Code);
    }

        [Fact]
    public async Task T31_P1_4_RequestAndTransactionPrivacy_ElectedOwner()
    {
        var (f, clock, service, fund, owner, memberA, memberB) = await Setup3MemberFixedFundAsync();
        await using var _ = f;

        clock.Advance(TimeSpan.FromHours(1));
        var expReq = await service.CreateFundRequestAsync(owner.Id, fund.Id,
            new V1FundRequestInput("Expense", 100_000, owner.Id, "Old owner secret purchase", "secret-buy-2", Pin), default);

        clock.Advance(TimeSpan.FromDays(16));
        var election = await service.OpenElectionAsync(memberA.Id, fund.Id, new V1OpenElectionRequest(0), default);
        await service.CastBallotAsync(memberA.Id, election.Id, new V1BallotRequest(1, memberA.Id, null), default);
        await service.CastBallotAsync(memberB.Id, election.Id, new V1BallotRequest(1, memberA.Id, null), default);
        await service.AdvanceElectionAsync(memberA.Id, election.Id, default);
        await service.AcceptElectedOwnershipAsync(memberA.Id, election.Id, Pin, default);

        // 1. Elected owner calling FundRequestsAsync cannot see previous owner's requests
        var requests = await service.FundRequestsAsync(memberA.Id, fund.Id, 1, 50, default);
        Assert.DoesNotContain(requests, r => r.Id == expReq.Id);

        // 2. Elected owner calling GetFundRequestAsync for previous owner's request gets ForbiddenException
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.GetFundRequestAsync(memberA.Id, expReq.Id, default));

        // 3. Historical Transaction Privacy: A member who was NOT in Period 1 (joined later)
        // and later elected owner cannot view internal transactions prior to their membership
        var lateMember = await f.AddUserAsync("Late Member");
        f.Db.V1FundMemberships.Add(new V1FundMembership
        {
            FundId = fund.Id,
            UserId = lateMember.Id,
            Seat = 4,
            JoinedAt = clock.UtcNow
        });
        await f.Db.V1Funds.Where(x => x.Id == fund.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.OwnerUserId, lateMember.Id));
        f.Db.V1Elections.Add(new V1Election
        {
            FundId = fund.Id,
            IncumbentUserId = owner.Id,
            OpenedByUserId = memberA.Id,
            WinnerUserId = lateMember.Id,
            Phase = V1ElectionPhase.Completed,
            FinishedAt = clock.UtcNow,
            PolicyJson = "{}"
        });
        await f.Db.SaveChangesAsync();

        var lateTxs = await service.FundTransactionsAsync(lateMember.Id, fund.Id, 1, 50, default);
        // The transaction occurred before lateMember joined, so it is strictly excluded from results
        Assert.DoesNotContain(lateTxs, t => t.OccurredAt < clock.UtcNow.AddDays(-10));
    }

        [Fact]
    public async Task T32_P1_5_InactivityEvidence_RequiresUserSessionInactivity_And_NoReactivationVeto()
    {
        var (f, clock, service, fund, owner, memberA, memberB) = await Setup3MemberFixedFundAsync();
        await using var _ = f;

        // 1. Owner inactive in fund for 16 days, BUT session exists within 15 days (even if revoked!)
        clock.Advance(TimeSpan.FromDays(16));
        f.Db.UserSessions.Add(new UserSession
        {
            UserId = owner.Id,
            LastSeenAt = clock.UtcNow.AddDays(-2),
            RevokedAt = clock.UtcNow.AddDays(-1) // Revoked session still counts as recent activity!
        });
        await f.Db.SaveChangesAsync();

        var exSessionActive = await Assert.ThrowsAsync<ConflictException>(() =>
            service.OpenElectionAsync(memberA.Id, fund.Id, new V1OpenElectionRequest(0), default));
        Assert.Equal("ACTIVITY_EVIDENCE_BLOCKED", exSessionActive.Code);

        // Advance clock past 15 days of that session so evidence becomes truly valid
        clock.Advance(TimeSpan.FromDays(16));

        var election = await service.OpenElectionAsync(memberA.Id, fund.Id, new V1OpenElectionRequest(0), default);
        await service.CastBallotAsync(memberA.Id, election.Id, new V1BallotRequest(1, memberA.Id, null), default);
        await service.CastBallotAsync(memberB.Id, election.Id, new V1BallotRequest(1, memberA.Id, null), default);
        await service.AdvanceElectionAsync(memberA.Id, election.Id, default);

        // 2. During AwaitingAcceptance, incumbent owner logs in (new session)
        // Per fixed.md Section 8, incumbent owner cannot interfere or veto the ongoing election!
        f.Db.UserSessions.Add(new UserSession
        {
            UserId = owner.Id,
            LastSeenAt = clock.UtcNow,
            RevokedAt = null
        });
        await f.Db.SaveChangesAsync();

        // Elected member accepts ownership successfully
        var accepted = await service.AcceptElectedOwnershipAsync(memberA.Id, election.Id, Pin, default);
        Assert.Equal(V1ElectionPhase.Completed.ToString(), accepted.Phase);
        Assert.Equal(memberA.Id, accepted.WinnerUserId);

        var updatedFund = await f.Db.V1Funds.AsNoTracking().SingleAsync(x => x.Id == fund.Id);
        Assert.Equal(memberA.Id, updatedFund.OwnerUserId);
        Assert.Equal(1, updatedFund.OwnershipRevision);
    }

    [Fact]
    public async Task T33_P1_2_FailedElection_BlocksNewElection_AndWorkerChecksActiveElection()
    {
        var (f, clock, service, fund, owner, memberA, memberB) = await Setup3MemberFixedFundAsync();
        await using var _ = f;

        clock.Advance(TimeSpan.FromDays(16));
        var election = await service.OpenElectionAsync(memberA.Id, fund.Id, new V1OpenElectionRequest(0), default);
        await service.CastBallotAsync(memberA.Id, election.Id, new V1BallotRequest(1, null, null), default);
        await service.CastBallotAsync(memberB.Id, election.Id, new V1BallotRequest(1, null, null), default);
        await service.AdvanceElectionAsync(memberA.Id, election.Id, default);

        // 1. Attempting to open a new election while failed election is pending settlement is blocked
        var exOpen = await Assert.ThrowsAsync<ConflictException>(() =>
            service.OpenElectionAsync(memberB.Id, fund.Id, new V1OpenElectionRequest(0), default));
        Assert.Equal("election_failed_pending_settlement", exOpen.Code);

        // 2. If an active election is in progress, worker does not trigger failed settlement
        // (Simulate an active election entity present)
        var activeElec = new V1Election
        {
            FundId = fund.Id,
            IncumbentUserId = owner.Id,
            OpenedByUserId = memberB.Id,
            OwnershipRevision = 0,
            Phase = V1ElectionPhase.Nomination,
            PhaseStartedAt = clock.UtcNow,
            PolicyJson = "{}"
        };
        f.Db.V1Elections.Add(activeElec);
        await f.Db.SaveChangesAsync();

        clock.Advance(TimeSpan.FromHours(25));
        var res = await service.ProcessFixedFundDeadlinesAsync(fund.Id, default);
        Assert.NotEqual("FailedElectionSettled", res.Status);
        Assert.False(res.Dissolved);

        // Verify fund is NOT dissolved because active election protected it
        var dbFund = await f.Db.V1Funds.AsNoTracking().SingleAsync(x => x.Id == fund.Id);
        Assert.Null(dbFund.DissolvedAt);
    }

    [Fact]
    public async Task T34_P1_1_FundingDeadline_DuringActiveElection_FrozenElectorateProtected()
    {
        var (f, clock, service, fund, owner, memberA, memberB) = await Setup3MemberFixedFundAsync();
        await using var _ = f;

        // Set member A's required obligation to 2,000,000 VND (paid was only 1,000,000 VND), creating an actual deficit
        var oblA = await f.Db.V1FundObligations.SingleAsync(o => o.FundId == fund.Id && o.UserId == memberA.Id);
        oblA.RequiredVnd = 2_000_000;
        // Set contribution deadlines on members
        var memA = await f.Db.V1FundMemberships.SingleAsync(m => m.FundId == fund.Id && m.UserId == memberA.Id);
        memA.ContributionDeadline = clock.UtcNow.AddHours(2);
        await f.Db.SaveChangesAsync();

        // 16 days inactive to allow opening election
        clock.Advance(TimeSpan.FromDays(16));
        var election = await service.OpenElectionAsync(memberA.Id, fund.Id, new V1OpenElectionRequest(0), default);

        // Advance clock past Member A's contribution deadline
        clock.Advance(TimeSpan.FromHours(5));

        // Process deadlines
        var res = await service.ProcessFixedFundDeadlinesAsync(fund.Id, default);
        Assert.Equal("ElectionActiveFrozen", res.Status);
        Assert.False(res.Dissolved);
        Assert.Empty(res.AutoKickedUserIds);

        // Verify member A was NOT kicked and remains active despite the deficit
        var memACheck = await f.Db.V1FundMemberships.AsNoTracking().SingleAsync(m => m.Id == memA.Id);
        Assert.Null(memACheck.LeftAt);

        // Verify fund is NOT dissolved
        var fundCheck = await f.Db.V1Funds.AsNoTracking().SingleAsync(x => x.Id == fund.Id);
        Assert.Null(fundCheck.DissolvedAt);
    }

    [Fact]
    public async Task T35_P1_1_FundingDeadline_DuringFailedElectionOptOutWindow_ProtectedFromFundingDissolution()
    {
        var (f, clock, service, fund, owner, memberA, memberB) = await Setup3MemberFixedFundAsync();
        await using var _ = f;

        clock.Advance(TimeSpan.FromDays(16));
        var election = await service.OpenElectionAsync(memberA.Id, fund.Id, new V1OpenElectionRequest(0), default);
        // Fail election
        await service.CastBallotAsync(memberA.Id, election.Id, new V1BallotRequest(1, null, null), default);
        await service.CastBallotAsync(memberB.Id, election.Id, new V1BallotRequest(1, null, null), default);
        await service.AdvanceElectionAsync(memberA.Id, election.Id, default);

        // Now election failed. FinishedAt is now.
        // Set owner's required obligation to 2,000,000 VND (paid was only 1,000,000 VND), creating an actual deficit
        var oblOwner = await f.Db.V1FundObligations.SingleAsync(o => o.FundId == fund.Id && o.UserId == owner.Id);
        oblOwner.RequiredVnd = 2_000_000;
        var ownerMem = await f.Db.V1FundMemberships.SingleAsync(m => m.FundId == fund.Id && m.UserId == owner.Id);
        ownerMem.ContributionDeadline = clock.UtcNow.AddMinutes(30);
        await f.Db.SaveChangesAsync();

        // Advance 1 hour (inside 24h opt-out window, but past owner contribution deadline)
        clock.Advance(TimeSpan.FromHours(1));

        var res = await service.ProcessFixedFundDeadlinesAsync(fund.Id, default);
        Assert.Equal("FailedElectionPendingOptOut", res.Status);
        Assert.False(res.Dissolved);

        var fundCheck = await f.Db.V1Funds.AsNoTracking().SingleAsync(x => x.Id == fund.Id);
        Assert.Null(fundCheck.DissolvedAt);
    }

    [Fact]
    public async Task T36_P1_1_FailFixedPeriodEarly_BlockedDuringActiveElectionAndPendingSettlement()
    {
        var (f, clock, service, fund, owner, memberA, memberB) = await Setup3MemberFixedFundAsync();
        await using var _ = f;

        // Set member B's contribution deadline to past, then owner kicks member B to create a vacant seat
        var memB = await f.Db.V1FundMemberships.SingleAsync(m => m.FundId == fund.Id && m.UserId == memberB.Id);
        memB.ContributionDeadline = clock.UtcNow.AddMinutes(5);
        await f.Db.SaveChangesAsync();

        clock.Advance(TimeSpan.FromMinutes(10));
        await service.KickMemberAsync(owner.Id, fund.Id, memberB.Id, new V1KickMemberRequest(Pin, "missed deadline", Guid.NewGuid().ToString("N")), default);

        clock.Advance(TimeSpan.FromDays(16));
        var election = await service.OpenElectionAsync(memberA.Id, fund.Id, new V1OpenElectionRequest(0), default);

        // 1. Owner tries to sabotage active election via early period failure
        var exActive = await Assert.ThrowsAsync<ConflictException>(() =>
            service.FailFixedPeriodEarlyAsync(owner.Id, fund.Id, Pin, default));
        Assert.Equal("election_in_progress", exActive.Code);

        // 2. Election fails
        await service.CastBallotAsync(memberA.Id, election.Id, new V1BallotRequest(1, null, null), default);
        await service.AdvanceElectionAsync(memberA.Id, election.Id, default);

        // Owner tries to call early period failure during failed election pending settlement
        var exFailed = await Assert.ThrowsAsync<ConflictException>(() =>
            service.FailFixedPeriodEarlyAsync(owner.Id, fund.Id, Pin, default));
        Assert.Equal("election_failed_pending_settlement", exFailed.Code);
    }

    [Fact]
    public async Task T37_P1_2_VoluntaryOwnershipTransfer_EstablishesTenureAndHidesPastPrivateRecords()
    {
        var (f, clock, service, fund, owner, memberA, memberB) = await Setup3MemberFixedFundAsync();
        await using var _ = f;

        // 1. Founder creates private request in Period 1
        var req1 = await service.CreateFundRequestAsync(owner.Id, fund.Id,
            new V1FundRequestInput("Expense", 500_000, owner.Id, "Founder Private Request", Guid.NewGuid().ToString("N"), Pin), default);

        // 2. Founder transfers ownership to member A voluntarily
        var offer = await service.OfferOwnershipAsync(owner.Id, fund.Id, memberA.Id, Pin, default);
        clock.Advance(TimeSpan.FromMinutes(5));
        await service.AcceptOwnershipAsync(memberA.Id, offer.Id, Pin, default);

        // Member A is now Owner with Revision 1
        var fundDb = await f.Db.V1Funds.AsNoTracking().SingleAsync(x => x.Id == fund.Id);
        Assert.Equal(memberA.Id, fundDb.OwnerUserId);
        Assert.Equal(1, fundDb.OwnershipRevision);

        // 3. Member A queries requests: cannot see Founder's pre-tenure private request
        var requestsA = await service.FundRequestsAsync(memberA.Id, fund.Id, 1, 50, default);
        Assert.DoesNotContain(requestsA, r => r.Id == req1.Id);

        // 4. Member A tries to GetFundRequestAsync on req1: forbidden!
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.GetFundRequestAsync(memberA.Id, req1.Id, default));

        // 5. Member A creates a new request in their tenure
        var req2 = await service.CreateFundRequestAsync(memberA.Id, fund.Id,
            new V1FundRequestInput("Expense", 200_000, memberA.Id, "New Owner Request", Guid.NewGuid().ToString("N"), Pin), default);
        var requestsAAfter = await service.FundRequestsAsync(memberA.Id, fund.Id, 1, 50, default);
        Assert.Contains(requestsAAfter, r => r.Id == req2.Id);
        Assert.DoesNotContain(requestsAAfter, r => r.Id == req1.Id);
    }

    [Fact]
    public async Task T38_P1_3_FormerMember_CanQueryOwnTransactionsAndRefundReceipts_ExcludesOtherMembersAndPostLeave()
    {
        var (f, clock, service, fund, owner, memberA, memberB) = await Setup3MemberFixedFundAsync();
        await using var _ = f;

        // Advance 16 days and run an election that fails, opening the 24h opt-out window
        clock.Advance(TimeSpan.FromDays(16));
        var election = await service.OpenElectionAsync(memberA.Id, fund.Id, new V1OpenElectionRequest(0), default);
        await service.CastBallotAsync(memberA.Id, election.Id, new V1BallotRequest(1, null, null), default);
        await service.CastBallotAsync(memberB.Id, election.Id, new V1BallotRequest(1, null, null), default);
        await service.AdvanceElectionAsync(memberA.Id, election.Id, default);

        // 1. Member A opts out voluntarily during the 24h window after failed election
        var refund = await service.LeaveFundAsync(memberA.Id, fund.Id, new V1LeaveFundRequest(Pin, Guid.NewGuid().ToString("N")), default);
        Assert.True(refund.RefundAmountVnd > 0);
        Assert.NotNull(refund.LedgerTransactionId);

        // Verify Member A has LeftAt != null
        var memA = await f.Db.V1FundMemberships.AsNoTracking().SingleAsync(m => m.FundId == fund.Id && m.UserId == memberA.Id);
        Assert.NotNull(memA.LeftAt);

        // 2. Later, after Member A has left, another transaction occurs in the fund
        clock.Advance(TimeSpan.FromDays(2));
        var ltx = new LedgerTransaction
        {
            IdempotencyKey = Guid.NewGuid().ToString("N"),
            Status = LedgerTransactionStatus.Completed,
            CompletedAt = clock.UtcNow
        };
        f.Db.LedgerTransactions.Add(ltx);
        var postLeaveTx = new FinancialTransaction
        {
            OwnerUserId = owner.Id,
            RelatedBusinessId = fund.Id,
            LedgerTransactionId = ltx.Id,
            LedgerTransaction = ltx,
            Type = FinancialTransactionType.Expense,
            Status = FinancialTransactionStatus.Completed,
            Amount = 100_000,
            Description = "Chi tieu sau khi A roi quy",
            OccurredAt = clock.UtcNow
        };
        f.Db.FinancialTransactions.Add(postLeaveTx);
        await f.Db.SaveChangesAsync();

        // 3. Former Member A queries fund transactions
        var txs = await service.FundTransactionsAsync(memberA.Id, fund.Id, 1, 50, default);

        // Former member CAN query and see their own transactions (and refund receipts)
        Assert.NotEmpty(txs);
        Assert.Contains(txs, t => t.AmountVnd == refund.RefundAmountVnd);
        // BUT CANNOT see post-leave transaction
        Assert.DoesNotContain(txs, t => t.Id == postLeaveTx.Id);
        // And cannot see other members' transactions occurred after left
        foreach (var t in txs)
        {
            Assert.True(t.OccurredAt <= memA.LeftAt.Value);
        }

        // Non-member (stranger) is rejected with NotFoundException
        var stranger = await f.AddUserAsync("Stranger User");
        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.FundTransactionsAsync(stranger.Id, fund.Id, 1, 50, default));
    }

    [Fact]
    public async Task T39_P1_SameTimestamp_RequestAndOwnershipTransfer_ConsistentlyProtectedAcrossAll4Apis()
    {
        var (f, clock, service, fund, owner, memberA, memberB) = await Setup3MemberFixedFundAsync();
        await using var _ = f;

        // 1. Founder creates private expense request at timestamp T (OwnershipRevision = 0)
        var req1 = await service.CreateFundRequestAsync(owner.Id, fund.Id,
            new V1FundRequestInput("Expense", 500_000, owner.Id, "Founder Secret Expense", Guid.NewGuid().ToString("N"), Pin), default);

        // 2. Founder offers and member A accepts ownership at THE EXACT SAME TIMESTAMP T (clock is NOT advanced!)
        var offer = await service.OfferOwnershipAsync(owner.Id, fund.Id, memberA.Id, Pin, default);
        await service.AcceptOwnershipAsync(memberA.Id, offer.Id, Pin, default);

        // Verify fund is now revision 1 and owner is member A
        var fundDb = await f.Db.V1Funds.AsNoTracking().SingleAsync(x => x.Id == fund.Id);
        Assert.Equal(memberA.Id, fundDb.OwnerUserId);
        Assert.Equal(1, fundDb.OwnershipRevision);

        // 3. API 1: GetFundRequestAsync - throws ForbiddenException because req1 belongs to Revision 0
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.GetFundRequestAsync(memberA.Id, req1.Id, default));

        // 4. API 2: FundRequestsAsync - does NOT return req1 even though CreatedAt == becameOwnerAt!
        var requestsA = await service.FundRequestsAsync(memberA.Id, fund.Id, 1, 50, default);
        Assert.DoesNotContain(requestsA, r => r.Id == req1.Id);

        // 5. API 3: ActivitiesAsync - does NOT return private request events for req1
        var activitiesA = await service.ActivitiesAsync(memberA.Id, fund.Id, 1, 50, default);
        Assert.DoesNotContain(activitiesA, a => a.ReferenceId == req1.Id);

        // 6. API 4: FundTransactionsAsync - transactions tied to req1 are not privileged, description masked or omitted
        var txs = await service.FundTransactionsAsync(memberA.Id, fund.Id, 1, 50, default);
        Assert.DoesNotContain(txs, t => t.RelatedRequestId == req1.Id);
        Assert.DoesNotContain(txs, t => t.Description.Contains("Founder Secret Expense"));
    }
}
