using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpendWise.Application.Features.Operations;
using SpendWise.Domain.Finance;
using SpendWise.Infrastructure.Finance;
using Xunit;

namespace SpendWise.Tests;

[Trait("Category", "SqlServer")]
public sealed class Task17MaintenanceSqlHttpTests
{
    private const string Pin = IdentityTestData.DefaultPin;
    [Theory]
    [InlineData(null)] [InlineData("unbalanced")] [InlineData("stale-balances")] [InlineData("mixed-currency")]
    public async Task Maintenance_HTTP_SQL02_schedule_activate_close_checks_real_ledger_safety(string? corruption)
    {
        var corruptLedger = corruption is not null;
        await using var f = await Task17SqlHttpFixture.CreateAsync(); CoreFundSeed s;
        await using (var db = f.Context()) s = await CoreAcceptanceData.SeedAsync(db, false, ledgerOpening: true);
        var request = new ScheduleMaintenanceRequest(f.Clock.UtcNow.AddHours(1), f.Clock.UtcNow.AddHours(2), "Money", Pin, "Synthetic maintenance");
        await f.ActorAsync(s.Member);
        using var user = await f.Client.PostAsJsonAsync("/api/v1/system/maintenance", request); Assert.Equal(HttpStatusCode.Forbidden, user.StatusCode);
        await f.ActorAsync(s.Admin);
        using var wrong = await f.Client.PostAsJsonAsync("/api/v1/system/maintenance", request with { SecurityPin = "654321" }); Assert.Equal(HttpStatusCode.Forbidden, wrong.StatusCode);
        using var scheduled = await f.Client.PostAsJsonAsync("/api/v1/system/maintenance", request); Assert.Equal(HttpStatusCode.OK, scheduled.StatusCode);
        var plan = (await scheduled.Content.ReadFromJsonAsync<MaintenancePlanDto>())!;
        using var reminders = await f.Client.PostAsJsonAsync($"/api/v1/system/maintenance/{plan.Id}/reminders", new MaintenanceReminderRequest([10], 1, Pin, "Synthetic reminders")); Assert.Equal(HttpStatusCode.NoContent, reminders.StatusCode);
        using var reschedule = await f.Client.PutAsJsonAsync($"/api/v1/system/maintenance/{plan.Id}", new RescheduleMaintenanceRequest(f.Clock.UtcNow.AddMinutes(30), f.Clock.UtcNow.AddHours(3), "Money", 2, Pin, "Synthetic reschedule"));
        Assert.Equal(HttpStatusCode.NoContent, reschedule.StatusCode);
        f.Clock.UtcNow = f.Clock.UtcNow.AddMinutes(20);
        using (var scope = f.Factory.Services.CreateScope()) await scope.ServiceProvider.GetRequiredService<V1MaintenanceReminders>().ProcessAsync(default);
        f.Clock.UtcNow = f.Clock.UtcNow.AddMinutes(11);
        using (var scope = f.Factory.Services.CreateScope()) await scope.ServiceProvider.GetRequiredService<V1MaintenanceService>().ActivateDueAsync(default);
        using var status = await f.Client.GetAsync("/api/v1/system/maintenance"); Assert.Equal(HttpStatusCode.OK, status.StatusCode);
        Assert.Equal("Active", Assert.Single((await status.Content.ReadFromJsonAsync<List<MaintenancePlanDto>>())!).State);
        await f.ActorAsync(s.Member); using var blocked = await f.Client.GetAsync("/api/wallets"); Assert.Equal(HttpStatusCode.ServiceUnavailable, blocked.StatusCode);
        await f.ActorAsync(s.Admin);
        if (corruption == "unbalanced")
        {
            await using var bad = f.Context(); var account = await bad.LedgerAccounts.FirstAsync();
            bad.LedgerTransactions.Add(new() { Reference = "SYNTHETIC-UNBALANCED", BusinessType = "Synthetic", Status = LedgerTransactionStatus.Completed,
                Entries = [new() { LedgerAccountId = account.Id, Amount = 1, Currency = "VND", Direction = LedgerEntryDirection.Debit }] }); await bad.SaveChangesAsync();
        }
        else if (corruption == "stale-balances")
        {
            await using var bad = f.Context(); var source = await bad.LedgerAccounts.SingleAsync(a => a.WalletId == s.OwnerWallet); var destination = await bad.LedgerAccounts.SingleAsync(a => a.WalletId == s.MemberWallet);
            bad.LedgerTransactions.Add(new() { Reference = "SYNTHETIC-STALE", BusinessType = "Synthetic", Status = LedgerTransactionStatus.Completed,
                Entries = [new() { LedgerAccountId = source.Id, Amount = 1, Currency = "VND", Direction = LedgerEntryDirection.Credit }, new() { LedgerAccountId = destination.Id, Amount = 1, Currency = "VND", Direction = LedgerEntryDirection.Debit }] }); await bad.SaveChangesAsync();
        }
        else if (corruption == "mixed-currency")
        {
            await using var bad = f.Context(); var source = await bad.LedgerAccounts.SingleAsync(a => a.Code == "SYNTHETIC-OPENING"); source.Balance -= 100;
            var usd = new LedgerAccount { Code = "SYNTHETIC-USD", Name = "Synthetic foreign account", Type = LedgerAccountType.ExternalSource, IsSystem = true, Currency = "USD", Balance = 100 }; bad.LedgerAccounts.Add(usd);
            bad.LedgerTransactions.Add(new() { Reference = "SYNTHETIC-FX-INVALID", BusinessType = "Synthetic", Status = LedgerTransactionStatus.Completed,
                Entries = [new() { LedgerAccountId = source.Id, Amount = 100, Currency = "VND", Direction = LedgerEntryDirection.Credit }, new() { LedgerAccountId = usd.Id, Amount = 100, Currency = "USD", Direction = LedgerEntryDirection.Debit }] }); await bad.SaveChangesAsync();
        }
        using var close = await f.Client.PostAsJsonAsync($"/api/v1/system/maintenance/{plan.Id}/close", new MaintenanceActionRequest(Pin, "Synthetic safe reopen"));
        Assert.True(close.StatusCode == (corruptLedger ? HttpStatusCode.Conflict : HttpStatusCode.NoContent), await close.Content.ReadAsStringAsync());
        await using var verify = f.Context(); var saved = await verify.V1MaintenancePlans.SingleAsync();
        Assert.Equal(!corruptLedger, saved.ClosedAt is not null);
        Assert.Equal(!corruptLedger, saved.CloseSafetyEvidenceId is not null);
        Assert.Equal(corruptLedger ? 0 : 1, await verify.AuditLogs.CountAsync(a => a.Action == "V1_MAINTENANCE_CLOSED"));
        if (corruptLedger) return;
        await f.ActorAsync(s.Member); using var restored = await f.Client.GetAsync("/api/wallets"); Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
        await f.ActorAsync(s.Admin);
        using var emergency = await f.Client.PostAsJsonAsync("/api/v1/system/maintenance/emergency", new EmergencyMaintenanceRequest(f.Clock.UtcNow.AddHours(1), "Money", Pin, "Synthetic incident", "CONFIRM_MAINTENANCE_IMPACT", "task17-incident"));
        Assert.Equal(HttpStatusCode.OK, emergency.StatusCode); var e = (await emergency.Content.ReadFromJsonAsync<MaintenancePlanDto>())!;
        using var extend = await f.Client.PostAsJsonAsync($"/api/v1/system/maintenance/{e.Id}/extend", new ExtendMaintenanceRequest(f.Clock.UtcNow.AddHours(2), 1, Pin, "Synthetic extend")); Assert.Equal(HttpStatusCode.NoContent, extend.StatusCode);
        using var review = await f.Client.PostAsJsonAsync($"/api/v1/system/maintenance/{e.Id}/review", new MaintenanceActionRequest(Pin, "Synthetic incident reviewed")); Assert.Equal(HttpStatusCode.NoContent, review.StatusCode);
        using var final = await f.Client.PostAsJsonAsync($"/api/v1/system/maintenance/{e.Id}/close", new MaintenanceActionRequest(Pin, "Synthetic incident resolved")); Assert.Equal(HttpStatusCode.NoContent, final.StatusCode);
        foreach (var action in new[] { "V1_MAINTENANCE_SCHEDULED", "V1_MAINTENANCE_REMINDERS_CONFIGURED", "V1_MAINTENANCE_RESCHEDULED", "V1_MAINTENANCE_ACTIVATED", "V1_MAINTENANCE_EMERGENCY", "V1_MAINTENANCE_EXTENDED", "V1_MAINTENANCE_REVIEWED" })
            Assert.Equal(1, await verify.AuditLogs.CountAsync(a => a.Action == action));
        Assert.Equal(2, await verify.AuditLogs.CountAsync(a => a.Action == "V1_MAINTENANCE_CLOSED"));
        Assert.Equal(4, await verify.LedgerTransactions.CountAsync());
    }

    [Fact]
    public async Task Emergency_HTTP_SQL02_cannot_interrupt_inflight_ledger_commit()
    {
        await using var f = await Task17SqlHttpFixture.CreateAsync(); CoreFundSeed s;
        await using (var setup = f.Context()) s = await CoreAcceptanceData.SeedAsync(setup, false, ledgerOpening: true);
        await f.ActorAsync(s.Admin); await using var posting = f.Context();
        await using var transaction = await posting.Database.BeginTransactionAsync();
        var source = await posting.LedgerAccounts.Where(a => a.WalletId == s.OwnerWallet).Select(a => a.Id).SingleAsync();
        var target = await posting.LedgerAccounts.Where(a => a.WalletId == s.MemberWallet).Select(a => a.Id).SingleAsync();
        await new LedgerService(posting, f.Clock).PostAsync(source, target, 100, "VND", "Synthetic", s.FundId, "task17", "inflight", "Synthetic posting", default);
        var request = new EmergencyMaintenanceRequest(f.Clock.UtcNow.AddHours(1), "Money", Pin, "Synthetic concurrent incident", "CONFIRM_MAINTENANCE_IMPACT", "task17-overlap");
        using var busy = await f.Client.PostAsJsonAsync("/api/v1/system/maintenance/emergency", request);
        Assert.Equal(HttpStatusCode.Conflict, busy.StatusCode); Assert.Contains("maintenance_admission_busy", await busy.Content.ReadAsStringAsync());
        await transaction.CommitAsync();
        using var retry = await f.Client.PostAsJsonAsync("/api/v1/system/maintenance/emergency", request); Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        await using var verify = f.Context();
        Assert.Equal(1_499_900, await verify.Wallets.Where(w => w.Id == s.OwnerWallet).Select(w => w.AvailableBalance).SingleAsync());
        Assert.Equal(1_500_100, await verify.Wallets.Where(w => w.Id == s.MemberWallet).Select(w => w.AvailableBalance).SingleAsync());
        Assert.Equal(5, await verify.LedgerTransactions.CountAsync()); Assert.Single(await verify.V1MaintenancePlans.ToListAsync());
    }
}
