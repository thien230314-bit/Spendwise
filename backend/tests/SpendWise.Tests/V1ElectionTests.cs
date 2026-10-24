using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Features.V1Groups;
using SpendWise.Application.Common.Interfaces;
using Microsoft.Extensions.Options;
using SpendWise.Domain.V1Groups;
using Xunit;

namespace SpendWise.Tests;

public sealed class V1ElectionTests
{
    [Fact]
    public async Task System_invalidation_releases_all_ineligible_elector_freeze_without_faking_human_activity()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Owner"); var member = await f.AddUserAsync("Member"); var service = Configured(f);
        var fund = await service.CreateAsync(owner.Id, new("Free", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);
        f.Db.Add(new V1FundMembership { FundId = fund.Id, UserId = member.Id, Seat = 2 }); await f.Db.SaveChangesAsync();
        var e = await service.OpenElectionAsync(member.Id, fund.Id, new(0), default);
        var actionAt = await f.Db.V1UserActivities.Where(x => x.UserId == member.Id).Select(x => x.LastActionAt).SingleAsync();
        await f.Db.Users.Where(u => u.Id == member.Id).ExecuteUpdateAsync(s => s.SetProperty(u => u.MustChangePassword, true));
        await service.InvalidateIneligibleElectionsAsync(default); await service.InvalidateIneligibleElectionsAsync(default);
        var current = await f.Db.V1Elections.AsNoTracking().SingleAsync();
        Assert.Equal(V1ElectionPhase.Failed, current.Phase); Assert.NotNull(current.FinishedAt); Assert.Equal("EligibilityChanged", current.FailureReason);
        var audit = Assert.Single(await f.Db.AuditLogs.Where(x => x.Action == "V1_ELECTION_INVALIDATED").ToListAsync());
        Assert.Null(audit.ActorUserId); Assert.Equal(e.Id.ToString(), audit.TargetId);
        Assert.Equal(actionAt, await f.Db.V1UserActivities.Where(x => x.UserId == member.Id).Select(x => x.LastActionAt).SingleAsync());
        Assert.Equal(owner.Id, await f.Db.V1Funds.Select(x => x.OwnerUserId).SingleAsync()); Assert.Empty(await f.Db.LedgerTransactions.ToListAsync());
    }
    [Fact]
    public async Task Changed_electorate_terminates_without_changing_denominator_or_leaving_permanent_freeze()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Owner"); var a = await f.AddUserAsync("A"); var b = await f.AddUserAsync("B"); var service = Configured(f);
        var fund = await service.CreateAsync(owner.Id, new("Free", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);
        f.Db.AddRange(new V1FundMembership { FundId = fund.Id, UserId = a.Id, Seat = 2 }, new V1FundMembership { FundId = fund.Id, UserId = b.Id, Seat = 3 }); await f.Db.SaveChangesAsync();
        var e = await service.OpenElectionAsync(a.Id, fund.Id, new(0), default);
        await f.Db.Users.Where(u => u.Id == b.Id).ExecuteUpdateAsync(s => s.SetProperty(u => u.MustChangePassword, true));
        await Assert.ThrowsAsync<ConflictException>(() => service.CastBallotAsync(a.Id, e.Id, new(1, a.Id, null), default));
        var ended = await service.AdvanceElectionAsync(a.Id, e.Id, default);
        Assert.Equal("Failed", ended.Phase); Assert.Equal("EligibilityChanged", ended.FailureReason); Assert.NotNull(ended.FinishedAt);
        Assert.Equal(2, await f.Db.V1ElectionVoters.CountAsync()); Assert.Empty(await f.Db.V1ElectionBallots.ToListAsync());
        Assert.Equal(owner.Id, await f.Db.V1Funds.Select(x => x.OwnerUserId).SingleAsync());
        Assert.Single(await f.Db.V1FundActivities.Where(x => x.Event == "ElectionInvalidated").ToListAsync());
        Assert.Equal("Failed", (await service.AdvanceElectionAsync(a.Id, e.Id, default)).Phase);
        Assert.Empty(await f.Db.LedgerTransactions.ToListAsync());
    }
    [Theory]
    [InlineData("NoCandidates")]
    [InlineData("StaleEpoch")]
    [InlineData("EligibilityChanged")]
    [InlineData("MalformedPolicy")]
    public async Task Closed_or_changed_election_cannot_silently_replace_owner(string scenario)
    {
        var clock = new Clock(); await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);
        var owner = await f.AddUserAsync("Owner"); var member = await f.AddUserAsync("Member"); var service = Configured(f);
        var fund = await service.CreateAsync(owner.Id, new("Free", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);
        var membership = new V1FundMembership { FundId = fund.Id, UserId = member.Id, Seat = 2, JoinedAt = clock.UtcNow };
        f.Db.Add(membership); await f.Db.SaveChangesAsync(); var e = await service.OpenElectionAsync(member.Id, fund.Id, new(0), default);
        clock.UtcNow = clock.UtcNow.AddSeconds(61);
        if (scenario == "NoCandidates")
        {
            var ended = await service.AdvanceElectionAsync(member.Id, e.Id, default);
            Assert.Equal("Failed", ended.Phase); Assert.Equal("NoCandidates", ended.FailureReason);
        }
        else
        {
            if (scenario == "StaleEpoch") await f.Db.V1Funds.ExecuteUpdateAsync(s => s.SetProperty(x => x.OwnershipRevision, 1));
            if (scenario == "EligibilityChanged") await f.Db.V1FundMemberships.Where(m => m.Id == membership.Id).ExecuteUpdateAsync(s => s.SetProperty(m => m.LeftAt, clock.UtcNow));
            if (scenario == "MalformedPolicy") await f.Db.V1Elections.ExecuteUpdateAsync(s => s.SetProperty(x => x.PolicyJson, "{}"));
            await Assert.ThrowsAnyAsync<AppException>(() => service.AdvanceElectionAsync(member.Id, e.Id, default));
            Assert.Null(await f.Db.V1Elections.Select(x => x.FinishedAt).SingleAsync());
        }
        Assert.Equal(owner.Id, await f.Db.V1Funds.Select(x => x.OwnerUserId).SingleAsync());
        Assert.Empty(await f.Db.LedgerTransactions.ToListAsync());
    }

    [Fact]
    public async Task Active_election_freezes_new_membership_voluntary_transfer_and_new_contribution()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Owner"); var member = await f.AddUserAsync("Member"); var outsider = await f.AddUserAsync("Outsider"); var service = Configured(f);
        var fund = await service.CreateAsync(owner.Id, new("Free", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);
        f.Db.Add(new V1FundMembership { FundId = fund.Id, UserId = member.Id, Seat = 2, JoinedAt = f.Clock.UtcNow }); await f.Db.SaveChangesAsync();
        await V1FundMembershipTests.MakeFriends(f, owner.Id, outsider.Id);
        var invitation = await service.InviteAsync(owner.Id, fund.Id, outsider.Id, default);
        var offer = await service.OfferOwnershipAsync(owner.Id, fund.Id, member.Id, TargetedCorrectnessFixture.SecurityPin, default);
        await service.OpenElectionAsync(member.Id, fund.Id, new(0), default);
        await Assert.ThrowsAsync<ConflictException>(() => service.AcceptAsync(outsider.Id, invitation.Id, default));
        await Assert.ThrowsAsync<ConflictException>(() => service.AcceptOwnershipAsync(member.Id, offer.Id, TargetedCorrectnessFixture.SecurityPin, default));
        var error = await Assert.ThrowsAsync<ConflictException>(() => service.ContributeAsync(member.Id, fund.Id, new(Guid.NewGuid(), 1, "Synthetic", TargetedCorrectnessFixture.SecurityPin), default));
        Assert.Equal("election_contributions_paused", error.Code);
        Assert.Equal(2, await f.Db.V1FundMemberships.CountAsync()); Assert.Empty(await f.Db.LedgerTransactions.ToListAsync());
        Assert.Equal(owner.Id, await f.Db.V1Funds.Select(x => x.OwnerUserId).SingleAsync());
    }
    [Theory]
    [InlineData(false, "NoAbsoluteMajority")]
    [InlineData(true, "RunoffTie")]
    public async Task Silence_is_not_consent_and_a_second_tie_cannot_transfer_ownership(bool tie, string failure)
    {
        var clock = new Clock(); await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);
        var owner = await f.AddUserAsync("Owner"); var a = await f.AddUserAsync("A"); var b = await f.AddUserAsync("B");
        var service = Configured(f); var fund = await service.CreateAsync(owner.Id, new("Free", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);
        f.Db.V1FundMemberships.AddRange(new V1FundMembership { FundId = fund.Id, UserId = a.Id, Seat = 2, JoinedAt = clock.UtcNow },
            new V1FundMembership { FundId = fund.Id, UserId = b.Id, Seat = 3, JoinedAt = clock.UtcNow }); await f.Db.SaveChangesAsync();
        var e = await service.OpenElectionAsync(a.Id, fund.Id, new(0), default);
        await Assert.ThrowsAsync<ForbiddenException>(() => service.RegisterCandidateAsync(owner.Id, e.Id, TargetedCorrectnessFixture.SecurityPin, default));
        await service.RegisterCandidateAsync(a.Id, e.Id, TargetedCorrectnessFixture.SecurityPin, default);
        await service.RegisterCandidateAsync(b.Id, e.Id, TargetedCorrectnessFixture.SecurityPin, default);
        clock.UtcNow = clock.UtcNow.AddSeconds(61); await service.AdvanceElectionAsync(a.Id, e.Id, default);
        await service.CastBallotAsync(a.Id, e.Id, new(1, a.Id, null), default);
        if (tie) await service.CastBallotAsync(b.Id, e.Id, new(1, b.Id, null), default);
        clock.UtcNow = clock.UtcNow.AddSeconds(61); var next = await service.AdvanceElectionAsync(a.Id, e.Id, default);
        Assert.Equal(tie ? "Runoff" : "Confirmation", next.Phase);
        if (tie)
        {
            await service.CastBallotAsync(a.Id, e.Id, new(2, a.Id, null), default);
            await service.CastBallotAsync(b.Id, e.Id, new(2, b.Id, null), default);
        }
        else await service.CastBallotAsync(a.Id, e.Id, new(3, null, true), default);
        clock.UtcNow = clock.UtcNow.AddSeconds(61); var failed = await service.AdvanceElectionAsync(a.Id, e.Id, default);
        Assert.Equal("Failed", failed.Phase); Assert.Equal(failure, failed.FailureReason);
        Assert.Equal(owner.Id, (await service.GetAsync(a.Id, fund.Id, default)).OwnerUserId);
        Assert.Empty(await f.Db.LedgerTransactions.ToListAsync());
    }

    [Fact]
    public async Task Incomplete_evidence_blocks_even_when_server_policy_is_complete()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Owner"); var member = await f.AddUserAsync("Member"); var service = Configured(f, new IncompleteInactivityEvidence());
        var fund = await service.CreateAsync(owner.Id, new("Free", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);
        f.Db.V1FundMemberships.Add(new() { FundId = fund.Id, UserId = member.Id, Seat = 2, JoinedAt = f.Clock.UtcNow }); await f.Db.SaveChangesAsync();
        var error = await Assert.ThrowsAsync<ConflictException>(() => service.OpenElectionAsync(member.Id, fund.Id, new(0), default));
        Assert.Equal("ACTIVITY_EVIDENCE_BLOCKED", error.Code); Assert.Empty(await f.Db.V1Elections.ToListAsync());
    }
    [Fact]
    public async Task Synthetic_approved_policy_has_one_ballot_per_round_majority_and_one_owner_acceptance_without_money_change()
    {
        var clock = new Clock(); await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);
        var owner = await f.AddUserAsync("Owner"); var a = await f.AddUserAsync("A"); var b = await f.AddUserAsync("B"); var c = await f.AddUserAsync("C");
        var service = Configured(f); var fund = await service.CreateAsync(owner.Id, new("Free", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);
        f.Db.V1FundMemberships.AddRange(new V1FundMembership { FundId = fund.Id, UserId = a.Id, Seat = 2, JoinedAt = clock.UtcNow },
            new V1FundMembership { FundId = fund.Id, UserId = b.Id, Seat = 3, JoinedAt = clock.UtcNow }, new V1FundMembership { FundId = fund.Id, UserId = c.Id, Seat = 4, JoinedAt = clock.UtcNow }); await f.Db.SaveChangesAsync();
        var election = await service.OpenElectionAsync(a.Id, fund.Id, new(0), default);
        Assert.Equal(election.Id, (await service.OpenElectionAsync(c.Id, fund.Id, new(0), default)).Id);
        Assert.Equal(3, await f.Db.V1ElectionVoters.CountAsync());
        await service.RegisterCandidateAsync(b.Id, election.Id, TargetedCorrectnessFixture.SecurityPin, default);
        await service.RegisterCandidateAsync(c.Id, election.Id, TargetedCorrectnessFixture.SecurityPin, default);
        clock.UtcNow = clock.UtcNow.AddSeconds(61); await service.AdvanceElectionAsync(a.Id, election.Id, default);
        await service.CastBallotAsync(a.Id, election.Id, new(1, b.Id, null), default);
        await service.CastBallotAsync(a.Id, election.Id, new(1, b.Id, null), default);
        await Assert.ThrowsAsync<ConflictException>(() => service.CastBallotAsync(a.Id, election.Id, new(1, c.Id, null), default));
        await service.CastBallotAsync(b.Id, election.Id, new(1, b.Id, null), default);
        clock.UtcNow = clock.UtcNow.AddSeconds(61); await service.AdvanceElectionAsync(a.Id, election.Id, default);
        await service.CastBallotAsync(a.Id, election.Id, new(3, null, true), default);
        await service.CastBallotAsync(b.Id, election.Id, new(3, null, true), default);
        clock.UtcNow = clock.UtcNow.AddSeconds(61); var ready = await service.AdvanceElectionAsync(a.Id, election.Id, default);
        Assert.Equal("AwaitingAcceptance", ready.Phase);
        var done = await service.AcceptElectedOwnershipAsync(b.Id, election.Id, TargetedCorrectnessFixture.SecurityPin, default);
        Assert.Equal("Completed", done.Phase);
        await service.AcceptElectedOwnershipAsync(b.Id, election.Id, TargetedCorrectnessFixture.SecurityPin, default);
        var current = await service.GetAsync(a.Id, fund.Id, default); Assert.Equal(b.Id, current.OwnerUserId); Assert.Equal(owner.Id, current.FounderUserId);
        Assert.Equal(4, await f.Db.V1ElectionBallots.CountAsync());
        Assert.Single(await f.Db.V1FundActivities.Where(x => x.Event == "ElectionOwnershipAccepted").ToListAsync());
        Assert.Empty(await f.Db.LedgerTransactions.ToListAsync()); Assert.Empty(await f.Db.FinancialTransactions.ToListAsync());
    }

    internal static V1FundService Configured(TargetedCorrectnessFixture f, IV1InactivityEvidence? evidence = null) => new(f.Db, f.Clock, f.Security,
        new SpendWise.Infrastructure.Notifications.NotificationService(f.Db), new SpendWise.Infrastructure.Finance.LedgerService(f.Db, f.Clock),
        new SpendWise.Application.Features.Banking.ActiveBankGuard(f.Db),
        Options.Create(new V1ElectionPolicy { Version = "SYNTHETIC-TEST-POLICY", RegistrationSeconds = 60, NominationSeconds = 60, RunoffSeconds = 60,
            ConfirmationSeconds = 60, AcceptanceSeconds = 60, FreezeMembership = true, AllowContributions = false,
            NoCandidatesOutcome = "Fail", NoVotesOutcome = "Fail", AcceptanceExpiryOutcome = "Fail", EligibilityChangeOutcome = "Fail" }), evidence ?? new SyntheticInactivity());
    private sealed class SyntheticInactivity : IV1InactivityEvidence
    { public Task<V1InactivityProof> GetAsync(Guid userId, CancellationToken ct) => Task.FromResult(new V1InactivityProof(true, true, "Synthetic-complete-proof")); }
    private sealed class Clock : IClock { public DateTime UtcNow { get; set; } = new(2026, 10, 18, 12, 0, 0, DateTimeKind.Utc); }

    [Fact]
    public async Task Missing_server_policy_and_complete_activity_evidence_cannot_open_election()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("Owner"); var member = await f.AddUserAsync("Member"); var funds = V1FundMembershipTests.Service(f);
        var fund = await funds.CreateAsync(owner.Id, new("Free", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);
        f.Db.V1FundMemberships.Add(new() { FundId = fund.Id, UserId = member.Id, Seat = 2, JoinedAt = f.Clock.UtcNow }); await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<ConflictException>(() => funds.OpenElectionAsync(member.Id, fund.Id, new(0), default));
        Assert.Empty(await f.Db.V1Elections.ToListAsync());
        Assert.Equal(owner.Id, (await funds.GetAsync(member.Id, fund.Id, default)).OwnerUserId);
        Assert.Empty(await f.Db.LedgerTransactions.ToListAsync());
    }
}
