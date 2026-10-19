using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Features.Operations;
using SpendWise.Domain.Identity;
using Xunit;

namespace SpendWise.Tests;

public sealed class V1MaintenanceReminderTests
{
    [Fact]
    public async Task Reminders_require_policy_deduplicate_and_do_not_replay_missed_milestones()
    {
        var clock = new V1GovernanceFinishTests.Clock(); await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);
        var actor = await f.AddUserAsync("Administrator", role: SystemRole.Admin); var session = await f.AddSessionAsync(actor.Id);
        var principal = V1PermissionTests.Principal(actor.Id, session.Id, "Admin"); var maintenance = V1MaintenanceTests.Service(f);
        var plan = await maintenance.ScheduleAsync(principal, new(clock.UtcNow.AddHours(2), clock.UtcNow.AddHours(3), "Global", TargetedCorrectnessFixture.SecurityPin, "Synthetic maintenance"), default);
        var unconfigured = new V1MaintenanceReminders(f.Db, f.Clock, maintenance, Options.Create(new V1ReminderPolicy()));
        await Assert.ThrowsAsync<ConflictException>(() => unconfigured.ConfigureAsync(principal, plan.Id, new([60], plan.Revision, TargetedCorrectnessFixture.SecurityPin, "Configure reminders"), default));
        var reminders = new V1MaintenanceReminders(f.Db, f.Clock, maintenance, Options.Create(new V1ReminderPolicy { Version = "SYNTHETIC", MaximumCount = 3, MinimumSpacingMinutes = 10, LateToleranceSeconds = 60 }));
        await reminders.ConfigureAsync(principal, plan.Id, new([60, 15], plan.Revision, TargetedCorrectnessFixture.SecurityPin, "Configure reminders"), default);
        var snapshot = await f.Db.V1MaintenancePlans.AsNoTracking().SingleAsync();
        Assert.Equal("SYNTHETIC", snapshot.ReminderPolicyVersion); Assert.Equal(60, snapshot.ReminderLateToleranceSeconds);
        clock.UtcNow = clock.UtcNow.AddHours(1); await reminders.ProcessAsync(default); await reminders.ProcessAsync(default);
        Assert.Single(await f.Db.V1Broadcasts.Where(b => b.EventKey.Contains("V1_MAINTENANCE_REMINDER:")).ToListAsync());
        clock.UtcNow = clock.UtcNow.AddMinutes(50); await reminders.ProcessAsync(default);
        Assert.Single(await f.Db.V1Broadcasts.Where(b => b.EventKey.Contains("V1_MAINTENANCE_REMINDER:")).ToListAsync());
        Assert.True(await V1MaintenanceGate.IsActiveAsync(f.Db, clock.UtcNow.AddHours(3), default));
    }
}
