using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Admin;
using SpendWise.Application.Features.Administration;
using SpendWise.Application.Features.Authentication;
using SpendWise.Application.Features.Operations;
using SpendWise.Domain.Identity;
using SpendWise.Infrastructure.Identity;
using Xunit;

namespace SpendWise.Tests;

public sealed class V1MaintenanceTests
{
    [Fact]
    public async Task Existing_public_maintenance_contract_reflects_new_global_gate()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync(); var actor = await f.AddUserAsync("Supervisor", role: SystemRole.SuperAdmin);
        f.Db.V1MaintenancePlans.Add(new() { CreatedByUserId = actor.Id, StartsAt = f.Clock.UtcNow.AddMinutes(-2), ExpectedEndsAt = f.Clock.UtcNow.AddMinutes(-1), Reason = "Synthetic" }); await f.Db.SaveChangesAsync();
        var handler = new OperationsHandler(f.Db, f.Clock, null!, null!, null!, null!);
        Assert.True((await handler.Handle(new GetMaintenanceStateQuery(), default)).IsEnabled);
    }

    [Fact]
    public async Task Global_maintenance_blocks_ledger_and_user_API_but_keeps_status_accessible()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync(); var actor = await f.AddUserAsync("Supervisor", role: SystemRole.SuperAdmin);
        var user = await f.AddUserAsync("User"); var a = await f.AddWalletAsync(user.Id, "Main", true, balance: 100);
        var b = await f.AddWalletAsync(user.Id, "Extra", false);
        f.Db.V1MaintenancePlans.Add(new() { CreatedByUserId = actor.Id, StartsAt = f.Clock.UtcNow.AddHours(-1), ExpectedEndsAt = f.Clock.UtcNow.AddMinutes(-1), Reason = "Synthetic" }); await f.Db.SaveChangesAsync();
        var ledger = new SpendWise.Infrastructure.Finance.LedgerService(f.Db, f.Clock);
        var error = await Assert.ThrowsAsync<AppException>(() => ledger.PostAsync(a.LedgerAccount.Id, b.LedgerAccount.Id, 20, "VND", "Synthetic", Guid.NewGuid(), "synthetic", "once", "Synthetic", default));
        Assert.Equal(503, error.StatusCode); Assert.Empty(await f.Db.LedgerTransactions.ToListAsync());
        var called = false; var middleware = new SpendWise.API.Infrastructure.BusinessAccessMiddleware(_ => { called = true; return Task.CompletedTask; });
        var context = new Microsoft.AspNetCore.Http.DefaultHttpContext(); context.Request.Path = "/api/wallets";
        await Assert.ThrowsAsync<AppException>(() => middleware.InvokeAsync(context, f.Db)); Assert.False(called);
        context.Request.Path = "/api/v1/system/maintenance"; await middleware.InvokeAsync(context, f.Db); Assert.True(called);
    }

    [Fact]
    public async Task Overlapping_maintenance_intervals_pause_inactivity_once_not_twice()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync(new Clock()); var actor = await f.AddUserAsync("Supervisor", role: SystemRole.SuperAdmin);
        var start = f.Clock.UtcNow.AddDays(-20);
        f.Db.V1MaintenancePlans.AddRange(new() { CreatedByUserId = actor.Id, StartsAt = start.AddDays(1), ExpectedEndsAt = start.AddDays(2), ClosedAt = start.AddDays(3), Reason = "Synthetic A" },
            new() { CreatedByUserId = actor.Id, StartsAt = start.AddDays(2), ExpectedEndsAt = start.AddDays(3), ClosedAt = start.AddDays(4), Reason = "Synthetic B" });
        await f.Db.SaveChangesAsync();
        Assert.Equal(TimeSpan.FromDays(17), await V1MaintenanceGate.ActiveElapsedAsync(f.Db, start, f.Clock.UtcNow, default));
    }

    [Fact]
    public async Task Scheduled_global_gate_starts_on_time_never_auto_reopens_and_unconfigured_safety_denies_close()
    {
        var clock = new Clock(); await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);
        var actor = await f.AddUserAsync("Administrator", role: SystemRole.Admin); var session = await f.AddSessionAsync(actor.Id);
        var principal = V1PermissionTests.Principal(actor.Id, session.Id, "Admin");
        var s = Service(f);
        var plan = await s.ScheduleAsync(principal, new(clock.UtcNow.AddMinutes(1), clock.UtcNow.AddMinutes(2), "Global", TargetedCorrectnessFixture.SecurityPin, "Synthetic maintenance"), default);
        Assert.False(await V1MaintenanceGate.IsActiveAsync(f.Db, clock.UtcNow, default));
        clock.UtcNow = clock.UtcNow.AddMinutes(3);
        Assert.True(await V1MaintenanceGate.IsActiveAsync(f.Db, clock.UtcNow, default));
        await s.ActivateDueAsync(default); await s.ActivateDueAsync(default);
        Assert.Single(await f.Db.AuditLogs.Where(a => a.Action == "V1_MAINTENANCE_ACTIVATED").ToListAsync());
        await Assert.ThrowsAsync<ConflictException>(() => s.CloseAsync(principal, plan.Id, new(TargetedCorrectnessFixture.SecurityPin, "Request manual close"), default));
        Assert.True(await V1MaintenanceGate.IsActiveAsync(f.Db, clock.UtcNow, default));
    }

    [Theory]
    [InlineData(SystemRole.SuperAdmin)]
    [InlineData(SystemRole.User)]
    [InlineData(SystemRole.SystemAdmin)]
    public async Task Non_admin_cannot_schedule(SystemRole role)
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync(); var actor = await f.AddUserAsync("Actor", role: role);
        var session = await f.AddSessionAsync(actor.Id);
        await Assert.ThrowsAsync<ForbiddenException>(() => Service(f).ScheduleAsync(V1PermissionTests.Principal(actor.Id, session.Id, role.ToString()),
            new(f.Clock.UtcNow.AddHours(1), f.Clock.UtcNow.AddHours(2), "Global", TargetedCorrectnessFixture.SecurityPin, "Synthetic plan"), default));
        Assert.Empty(await f.Db.V1MaintenancePlans.ToListAsync());
    }

    internal static V1MaintenanceService Service(TargetedCorrectnessFixture f) => new(f.Db, f.Clock, new(f.Db),
        new AdminSecurityGuard(f.Db, new SecurityPinService(), new AccountCredentialLockout(f.Db, f.Clock)), new(f.Db), new UnconfiguredMaintenanceSafety());
    private sealed class Clock : IClock { public DateTime UtcNow { get; set; } = new(2026, 10, 18, 12, 0, 0, DateTimeKind.Utc); }
}
