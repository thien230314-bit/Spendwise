using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Domain.V1Groups;
using Xunit;

namespace SpendWise.Tests;

public sealed class V1GovernanceFinishTests
{
    [Fact]
    public async Task Revoked_request_session_cannot_commit_a_fund_mutation()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync(); var user = await f.AddUserAsync("User");
        var session = new SpendWise.Domain.Identity.UserSession { UserId = user.Id, RevokedAt = f.Clock.UtcNow }; f.Db.Add(session); await f.Db.SaveChangesAsync();
        var service = new SpendWise.Application.Features.V1Groups.V1FundService(f.Db, f.Clock, f.Security,
            new SpendWise.Infrastructure.Notifications.NotificationService(f.Db), new SpendWise.Infrastructure.Finance.LedgerService(f.Db, f.Clock), new SpendWise.Application.Features.Banking.ActiveBankGuard(f.Db), requestUser: new RequestUser(user.Id, session.Id));
        await Assert.ThrowsAsync<SpendWise.Application.Common.Exceptions.ForbiddenException>(() => service.CreateAsync(user.Id, new("Fund", "Free", null, TargetedCorrectnessFixture.SecurityPin), default));
        Assert.Empty(await f.Db.V1Funds.ToListAsync()); Assert.Empty(await f.Db.V1FundActivities.ToListAsync());
    }
    private sealed class RequestUser(Guid user, Guid session) : ICurrentUser
    { public Guid? UserId => user; public string SessionId => session.ToString(); public string SystemRole => "User"; public string Locale => "vi"; }
    [Fact]
    public async Task Deadline_compensation_unions_overlaps_and_late_worker_does_not_extend_voting_window()
    {
        var clock = new Clock(); await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);
        var user = await f.AddUserAsync("User"); var start = clock.UtcNow;
        f.Db.AddRange(new SpendWise.Domain.Admin.V1MaintenancePlan { CreatedByUserId = user.Id, StartsAt = start.AddHours(1), ClosedAt = start.AddHours(3) },
            new SpendWise.Domain.Admin.V1MaintenancePlan { CreatedByUserId = user.Id, StartsAt = start.AddHours(2), ClosedAt = start.AddHours(4) }); await f.Db.SaveChangesAsync();
        Assert.Equal(start.AddHours(5), await SpendWise.Application.Features.Operations.V1MaintenanceGate.DeadlineAsync(f.Db, start, TimeSpan.FromHours(2), start.AddHours(8), default));
    }
    [Fact]
    public async Task Foreground_signal_uses_server_time_live_session_and_maintenance_adjusted_inactivity()
    {
        var clock = new Clock(); await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);
        var user = await f.AddUserAsync("User"); var session = new SpendWise.Domain.Identity.UserSession { UserId = user.Id, LastSeenAt = clock.UtcNow };
        f.Db.Add(session); await f.Db.SaveChangesAsync();
        var options = Microsoft.Extensions.Options.Options.Create(new SpendWise.Application.Features.V1Groups.V1ActivityPolicy { CoverageVerified = true, CoverageStartsAtUtc = clock.UtcNow });
        var activity = new SpendWise.Application.Features.V1Groups.V1ActivityService(f.Db, clock, options);
        await activity.RecordAsync(user.Id, session.Id, default);
        clock.UtcNow = clock.UtcNow.AddDays(15);
        var proof = await activity.GetAsync(user.Id, default); Assert.True(proof.CoverageComplete); Assert.True(proof.Eligible);
        f.Db.Add(new SpendWise.Domain.Admin.V1MaintenancePlan { CreatedByUserId = user.Id, StartsAt = clock.UtcNow.AddDays(-2), ClosedAt = clock.UtcNow.AddDays(-1), ExpectedEndsAt = clock.UtcNow.AddDays(-1) }); await f.Db.SaveChangesAsync();
        Assert.False((await activity.GetAsync(user.Id, default)).Eligible);
        await f.Db.UserSessions.ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, clock.UtcNow));
        await Assert.ThrowsAsync<SpendWise.Application.Common.Exceptions.ForbiddenException>(() => activity.RecordAsync(user.Id, session.Id, default));
        Assert.Single(await f.Db.V1UserActivities.ToListAsync());
        var unverified = new SpendWise.Application.Features.V1Groups.V1ActivityService(f.Db, clock, Microsoft.Extensions.Options.Options.Create(new SpendWise.Application.Features.V1Groups.V1ActivityPolicy()));
        Assert.False((await unverified.GetAsync(user.Id, default)).CoverageComplete);
    }
    [Fact]
    public async Task Automatic_round_closure_records_system_history_without_extending_user_activity()
    {
        var clock = new Clock(); await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);
        var owner = await f.AddUserAsync("Owner"); var member = await f.AddUserAsync("Member"); var service = V1ElectionTests.Configured(f);
        var fund = await service.CreateAsync(owner.Id, new("Fund", "Free", null, TargetedCorrectnessFixture.SecurityPin), default);
        f.Db.Add(new V1FundMembership { FundId = fund.Id, UserId = member.Id, Seat = 2, JoinedAt = clock.UtcNow }); await f.Db.SaveChangesAsync();
        var e = await service.OpenElectionAsync(member.Id, fund.Id, new(0), default);
        var actionAt = clock.UtcNow; clock.UtcNow = clock.UtcNow.AddMinutes(3);
        await service.ProgressElectionsAsync(default); await service.ProgressElectionsAsync(default);
        var current = await f.Db.V1Elections.AsNoTracking().SingleAsync();
        Assert.Equal(V1ElectionPhase.Failed, current.Phase); Assert.Equal("NoCandidates", current.FailureReason);
        var activity = Assert.Single(await f.Db.V1FundActivities.Where(a => a.Event == "ElectionPhaseAdvanced").ToListAsync());
        Assert.Null(activity.ActorUserId);
        Assert.Equal(actionAt, await f.Db.V1UserActivities.Where(a => a.UserId == member.Id).Select(a => a.LastActionAt).SingleAsync());
        Assert.Equal(owner.Id, await f.Db.V1Funds.Select(a => a.OwnerUserId).SingleAsync()); Assert.Empty(await f.Db.LedgerTransactions.ToListAsync());
    }
    internal sealed class Clock : IClock { public DateTime UtcNow { get; set; } = new(2026, 10, 18, 12, 0, 0, DateTimeKind.Utc); }
}
