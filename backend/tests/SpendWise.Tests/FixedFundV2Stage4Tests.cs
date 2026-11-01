using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.V1Groups;
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
}
