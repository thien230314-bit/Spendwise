using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Features.Operations;
using SpendWise.Domain.Admin;
using SpendWise.Domain.Finance;
using SpendWise.Domain.Identity;
using Xunit;

namespace SpendWise.Tests;

public sealed class V1ScopedMaintenanceTests
{
    [Fact]
    public async Task Scheduled_edit_is_revision_checked_and_active_emergency_review_is_immutable()
    {
        var clock = new V1GovernanceFinishTests.Clock(); await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);
        var actor = await f.AddUserAsync("Supervisor", role: SystemRole.SuperAdmin); var session = await f.AddSessionAsync(actor.Id);
        var principal = V1PermissionTests.Principal(actor.Id, session.Id, "SuperAdmin"); var service = V1MaintenanceTests.Service(f);
        var plan = await service.ScheduleAsync(principal, new(clock.UtcNow.AddHours(1), clock.UtcNow.AddHours(2), "AI", TargetedCorrectnessFixture.SecurityPin, "Synthetic plan"), default);
        await service.RescheduleAsync(principal, plan.Id, new(clock.UtcNow.AddHours(2), clock.UtcNow.AddHours(3), "AI", plan.Revision, TargetedCorrectnessFixture.SecurityPin, "Changed schedule"), default);
        await Assert.ThrowsAsync<ConflictException>(() => service.RescheduleAsync(principal, plan.Id, new(clock.UtcNow.AddHours(3), clock.UtcNow.AddHours(4), "AI", plan.Revision, TargetedCorrectnessFixture.SecurityPin, "Stale client"), default));
        await service.CancelAsync(principal, plan.Id, new(TargetedCorrectnessFixture.SecurityPin, "Cancel future plan"), default);
        var emergency = await service.EmergencyAsync(principal, new(clock.UtcNow.AddHours(1), "Global", TargetedCorrectnessFixture.SecurityPin, "Actual incident review test", "CONFIRM_MAINTENANCE_IMPACT", "incident-2"), default);
        await service.ReviewEmergencyAsync(principal, emergency.Id, new(TargetedCorrectnessFixture.SecurityPin, "Incident reviewed with evidence"), default);
        await service.ReviewEmergencyAsync(principal, emergency.Id, new(TargetedCorrectnessFixture.SecurityPin, "Incident reviewed with evidence"), default);
        await Assert.ThrowsAsync<ConflictException>(() => service.ReviewEmergencyAsync(principal, emergency.Id, new(TargetedCorrectnessFixture.SecurityPin, "Cannot replace review history"), default));
        Assert.Single(await f.Db.AuditLogs.Where(a => a.Action == "V1_MAINTENANCE_REVIEWED").ToListAsync());
    }
    [Fact]
    public async Task Emergency_requires_impact_confirmation_and_supports_idempotent_activation_and_revision_checked_extension()
    {
        var clock = new V1GovernanceFinishTests.Clock(); await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);
        var actor = await f.AddUserAsync("Supervisor", role: SystemRole.SuperAdmin); var session = await f.AddSessionAsync(actor.Id);
        var principal = V1PermissionTests.Principal(actor.Id, session.Id, "SuperAdmin"); var service = V1MaintenanceTests.Service(f);
        await Assert.ThrowsAsync<AppException>(() => service.EmergencyAsync(principal, new(clock.UtcNow.AddHours(1), "Groups", TargetedCorrectnessFixture.SecurityPin, "Incident evidence", "", "incident-1"), default));
        var request = new EmergencyMaintenanceRequest(clock.UtcNow.AddHours(1), "Groups", TargetedCorrectnessFixture.SecurityPin, "Incident evidence", "CONFIRM_MAINTENANCE_IMPACT", "incident-1");
        var plan = await service.EmergencyAsync(principal, request, default);
        Assert.Equal(plan.Id, (await service.EmergencyAsync(principal, request, default)).Id); Assert.Equal("Groups", plan.Scope);
        Assert.True(await V1MaintenanceGate.IsActiveAsync(f.Db, clock.UtcNow, default, "Groups")); Assert.False(await V1MaintenanceGate.IsActiveAsync(f.Db, clock.UtcNow, default));
        await service.ExtendAsync(principal, plan.Id, new(clock.UtcNow.AddHours(2), plan.Revision, TargetedCorrectnessFixture.SecurityPin, "More verification required"), default);
        await Assert.ThrowsAsync<ConflictException>(() => service.ExtendAsync(principal, plan.Id, new(clock.UtcNow.AddHours(3), plan.Revision, TargetedCorrectnessFixture.SecurityPin, "Stale revision denied"), default));
        await Assert.ThrowsAsync<ConflictException>(() => service.CloseAsync(principal, plan.Id, new(TargetedCorrectnessFixture.SecurityPin, "Missing safety evidence"), default));
        Assert.Single(await f.Db.AuditLogs.Where(a => a.Action == "V1_MAINTENANCE_CLOSED_DENIED").ToListAsync());
        Assert.True(await V1MaintenanceGate.IsActiveAsync(f.Db, clock.UtcNow, default, "Groups"));
        Assert.Single(await f.Db.AuditLogs.Where(a => a.Action == "V1_MAINTENANCE_EMERGENCY").ToListAsync());
    }
    [Fact]
    public async Task Group_scope_blocks_API_and_direct_shared_fund_ledger_but_not_personal_wallets_or_global_inactivity()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync(); var supervisor = await f.AddUserAsync("Supervisor", role: SystemRole.SuperAdmin); var user = await f.AddUserAsync("User");
        var a = await f.AddWalletAsync(user.Id, "Main", true, balance: 100); var b = await f.AddWalletAsync(user.Id, "Extra", false);
        var group = new LedgerAccount { Code = "Synthetic-group", Name = "Group", Type = LedgerAccountType.SharedFund }; f.Db.Add(group); await f.Db.SaveChangesAsync();
        var ledger = new SpendWise.Infrastructure.Finance.LedgerService(f.Db, f.Clock);
        var posted = await ledger.PostAsync(a.LedgerAccount.Id, group.Id, 10, "VND", "Opaque", Guid.NewGuid(), "synthetic", "before", "Synthetic", default);
        var now = f.Clock.UtcNow;
        f.Db.Add(new V1MaintenancePlan { CreatedByUserId = supervisor.Id, Scope = "Groups", StartsAt = now.AddHours(-1), ExpectedEndsAt = now.AddHours(1), Reason = "Synthetic" }); await f.Db.SaveChangesAsync();
        await ledger.PostAsync(a.LedgerAccount.Id, b.LedgerAccount.Id, 20, "VND", "Opaque", Guid.NewGuid(), "synthetic", "personal", "Synthetic", default);
        var error = await Assert.ThrowsAsync<AppException>(() => ledger.PostAsync(a.LedgerAccount.Id, group.Id, 10, "VND", "Opaque", Guid.NewGuid(), "synthetic", "blocked", "Synthetic", default)); Assert.Equal(503, error.StatusCode);
        var reverse = await Assert.ThrowsAsync<AppException>(() => ledger.ReverseAsync(posted.LedgerTransactionId, Guid.NewGuid(), "reverse", "Synthetic", default)); Assert.Equal(503, reverse.StatusCode);
        Assert.False(await V1MaintenanceGate.IsActiveAsync(f.Db, now, default));
        Assert.Equal(TimeSpan.FromHours(1), await V1MaintenanceGate.ActiveElapsedAsync(f.Db, now.AddHours(-1), now, default));
        Assert.Equal(TimeSpan.Zero, await V1MaintenanceGate.ActiveElapsedAsync(f.Db, now.AddHours(-1), now, default, "Groups"));
        var called = false; var middleware = new SpendWise.API.Infrastructure.BusinessAccessMiddleware(_ => { called = true; return Task.CompletedTask; });
        var context = new Microsoft.AspNetCore.Http.DefaultHttpContext(); context.Request.Path = "/api/v1/funds";
        await Assert.ThrowsAsync<AppException>(() => middleware.InvokeAsync(context, f.Db)); Assert.False(called);
        context.Request.Path = "/api/wallets"; await middleware.InvokeAsync(context, f.Db); Assert.True(called);
        Assert.Equal(2, await f.Db.LedgerTransactions.CountAsync()); Assert.Equal(10, await f.Db.LedgerAccounts.Where(x => x.Id == group.Id).Select(x => x.Balance).SingleAsync());
    }
}
