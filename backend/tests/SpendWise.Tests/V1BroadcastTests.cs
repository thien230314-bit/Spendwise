using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Features.Admin;
using SpendWise.Application.Features.Authentication;
using SpendWise.Application.Features.Operations;
using SpendWise.Domain.Identity;
using SpendWise.Infrastructure.Identity;
using Xunit;

namespace SpendWise.Tests;

public sealed class V1BroadcastTests
{
    [Fact]
    public async Task Maintenance_state_change_queues_current_revision_without_manual_broadcast_policy_or_backlog_send()
    {
        var clock = new V1GovernanceFinishTests.Clock(); await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);
        var actor = await f.AddUserAsync("Supervisor", role: SystemRole.SuperAdmin); await f.AddUserAsync("User"); var session = await f.AddSessionAsync(actor.Id);
        var principal = V1PermissionTests.Principal(actor.Id, session.Id, "SuperAdmin"); var maintenance = V1MaintenanceTests.Service(f);
        var planned = await maintenance.ScheduleAsync(principal, new(clock.UtcNow.AddHours(1), clock.UtcNow.AddHours(2), "Groups", TargetedCorrectnessFixture.SecurityPin, "Internal incident details never broadcast"), default);
        await maintenance.ExtendAsync(principal, planned.Id, new(clock.UtcNow.AddHours(3), planned.Revision, TargetedCorrectnessFixture.SecurityPin, "Revised estimate"), default);
        var service = Service(f, false); await service.ProcessDueAsync(default); await service.ProcessDueAsync(default);
        var notice = Assert.Single(await f.Db.Notifications.ToListAsync()); Assert.DoesNotContain("Internal incident", notice.Message);
        Assert.Single(await f.Db.V1Broadcasts.Where(b => b.State == "Superseded").ToListAsync()); Assert.Single(await f.Db.V1Broadcasts.Where(b => b.State == "Published").ToListAsync());
        await f.Db.Notifications.ExecuteDeleteAsync();
        var id = await f.Db.V1Broadcasts.Where(b => b.State == "Published").Select(b => b.Id).SingleAsync();
        var status = await service.StatusAsync(principal, id, "Inspect removed notification evidence", default); Assert.Equal(1, status.ReadUnknown);
        await service.ProcessDueAsync(default); Assert.Empty(await f.Db.Notifications.ToListAsync());
    }
    [Fact]
    public async Task Operational_broadcast_is_draft_then_scheduled_targets_roles_once_and_reports_real_in_app_state()
    {
        var clock = new V1GovernanceFinishTests.Clock(); await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);
        var actor = await f.AddUserAsync("Supervisor", role: SystemRole.SuperAdmin); await f.AddUserAsync("User A"); await f.AddUserAsync("User B");
        var session = await f.AddSessionAsync(actor.Id); var principal = V1PermissionTests.Principal(actor.Id, session.Id, "SuperAdmin"); var service = Service(f);
        var draft = await service.DraftAsync(principal, new("Operational", "User", "Service notice", "Synthetic operational information", "draft-1", TargetedCorrectnessFixture.SecurityPin, "Publish operational notice"), default);
        await service.ProcessDueAsync(default); Assert.Empty(await f.Db.Notifications.ToListAsync());
        await service.PublishAsync(principal, draft.Id, new(clock.UtcNow, draft.Revision, TargetedCorrectnessFixture.SecurityPin, "Publish reviewed notice"), default);
        await service.ProcessDueAsync(default); await service.ProcessDueAsync(default);
        var status = await service.StatusAsync(principal, draft.Id, "Inspect delivery outcome", default);
        Assert.Equal("Published", status.State); Assert.Equal(2, status.Persisted); Assert.Equal(0, status.Read); Assert.Equal(0, status.PushSubmitted);
        Assert.Equal(2, await f.Db.NotificationDeduplications.CountAsync());
        await Assert.ThrowsAsync<ConflictException>(() => service.PublishAsync(principal, draft.Id, new(clock.UtcNow, draft.Revision, TargetedCorrectnessFixture.SecurityPin, "No republish allowed"), default));
        await Assert.ThrowsAsync<ConflictException>(() => service.DraftAsync(principal, new("Promotion", "User", "Offer", "No approved money program", "promo", TargetedCorrectnessFixture.SecurityPin, "Promotion must be refused"), default));
    }
    internal static V1BroadcastService Service(TargetedCorrectnessFixture f, bool configured = true) => new(f.Db, f.Clock, new(f.Db),
        new AdminSecurityGuard(f.Db, new SecurityPinService(), new AccountCredentialLockout(f.Db, f.Clock)), new(f.Db), new SpendWise.Infrastructure.Notifications.NotificationService(f.Db),
        Options.Create(configured ? new V1BroadcastPolicy { Version = "SYNTHETIC", MaxRecipients = 100, MinSpacingSeconds = 60 } : new V1BroadcastPolicy()));
}
