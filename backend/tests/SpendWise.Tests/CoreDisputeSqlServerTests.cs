using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Features.Administration;
using SpendWise.Application.Features.Admin.Cases;
using SpendWise.Application.Identity;
using SpendWise.Domain.Admin;
using SpendWise.Infrastructure.Admin;
using SpendWise.Infrastructure.Identity;
using SpendWise.Infrastructure.Notifications;
using SpendWise.Infrastructure.Persistence;
using SpendWise.Infrastructure.Services;
using Xunit;

namespace SpendWise.Tests;

public sealed partial class V1FundSqlServerTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Core_dispute_SQL_resolution_preserves_hold_ledger_and_deadline(bool rejected)
    {
        var owned = await OwnAsync(Owned.CreateAsync());
        await using var db = owned.Context(); var seed = await CoreAcceptanceData.SeedAsync(db);
        var id = await CoreAcceptanceData.PendingAsync(db, seed);
        await CoreAcceptanceData.Funds(db).ReportFundRequestAsync(seed.Member.Id, id, "Synthetic report", default);
        var created = await db.V1FundRequests.Where(r => r.Id == id).Select(r => r.CreatedAt).SingleAsync();
        await A5(db).ResolveDisputeRequestAsync(V1PermissionTests.Principal(seed.Admin.Id, seed.AdminSession.Id, "Admin"), id,
            new("Synthetic investigation", "Synthetic verified decision", rejected), default);
        await using var verify = owned.Context();
        Assert.Equal(rejected ? "Pending24h" : "Cancelled", (await verify.V1FundRequests.SingleAsync(r => r.Id == id)).State);
        Assert.Equal(rejected ? CaseStatus.Rejected : CaseStatus.Resolved, (await verify.SupportCases.SingleAsync()).Status);
        Assert.Equal(created, await verify.V1FundRequests.Where(r => r.Id == id).Select(r => r.CreatedAt).SingleAsync());
        await AssertDisputeMoneyAsync(verify, seed, rejected ? 300_000 : 0);
    }

    [Fact]
    public async Task Core_dispute_SQL_failure_after_saved_decision_rolls_back_every_business_effect()
    {
        var owned = await OwnAsync(Owned.CreateAsync()); CoreFundSeed seed; Guid id;
        await using (var setup = owned.Context())
        {
            seed = await CoreAcceptanceData.SeedAsync(setup); id = await CoreAcceptanceData.PendingAsync(setup, seed);
            await CoreAcceptanceData.Funds(setup).ReportFundRequestAsync(seed.Member.Id, id, "Synthetic report", default);
        }
        await using var db = owned.Context(new FailDisputeNotification());
        await Assert.ThrowsAsync<InvalidOperationException>(() => A5(db).ResolveDisputeRequestAsync(
            V1PermissionTests.Principal(seed.Admin.Id, seed.AdminSession.Id, "Admin"), id, new("Synthetic investigation", "Synthetic rollback proof", false), default));
        await using var verify = owned.Context();
        var ticket = await verify.SupportCases.SingleAsync();
        Assert.Null(ticket.AssignedAdminId); Assert.Equal(CaseStatus.New, ticket.Status);
        Assert.Equal("Suspended", await verify.V1FundRequests.Where(r => r.Id == id).Select(r => r.State).SingleAsync());
        Assert.Empty(await verify.CaseHistories.Where(h => h.Action == "status_change").ToListAsync());
        Assert.Empty(await verify.AuditLogs.Where(a => a.Action == "A5_CASE_RESOLVED").ToListAsync());
        await AssertDisputeMoneyAsync(verify, seed, 300_000);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Core_dispute_SQL_overlapping_resolution_or_cancel_has_one_consistent_outcome(bool cancel)
    {
        var owned = await OwnAsync(Owned.CreateAsync()); CoreFundSeed seed; Guid id;
        await using (var setup = owned.Context())
        {
            seed = await CoreAcceptanceData.SeedAsync(setup); id = await CoreAcceptanceData.PendingAsync(setup, seed);
            await CoreAcceptanceData.Funds(setup).ReportFundRequestAsync(seed.Member.Id, id, "Synthetic report", default);
        }
        var gate = new DisputeReadGate(); await using var firstDb = owned.Context(gate); await using var secondDb = owned.Context();
        await secondDb.Database.OpenConnectionAsync();
        var sqlSession = ((Microsoft.Data.SqlClient.SqlConnection)secondDb.Database.GetDbConnection()).ServerProcessId;
        var principal = V1PermissionTests.Principal(seed.Admin.Id, seed.AdminSession.Id, "Admin");
        var first = A5(firstDb).ResolveDisputeRequestAsync(principal, id, new("Synthetic investigation", "Synthetic invalid report", true), default);
        Task<Exception?>? second = null;
        try
        {
            await SqlTestFixtureLifecycle.WaitForGateAsync(gate.Entered.Task, first, TimeSpan.FromSeconds(10));
            second = Record.ExceptionAsync(async () =>
            {
                if (cancel) await CoreAcceptanceData.Funds(secondDb).CancelFundRequestAsync(seed.Owner.Id, id, CoreAcceptanceData.Pin, default);
                else await A5(secondDb).ResolveDisputeRequestAsync(principal, id, new("Synthetic investigation", "Synthetic valid report", false), default);
            });
            Assert.True(await owned.ObserveWaitAsync(sqlSession, second));
            gate.Release.TrySetResult();
            var firstError = await Record.ExceptionAsync(() => first.WaitAsync(TimeSpan.FromSeconds(15)));
            var secondError = await second.WaitAsync(TimeSpan.FromSeconds(15));
            if (cancel) { Assert.Null(firstError); Assert.Null(secondError); }
            if (!cancel) Assert.True(firstError is null ^ secondError is null);
            await using var verify = owned.Context(); var request = await verify.V1FundRequests.SingleAsync(r => r.Id == id);
            var ticket = await verify.SupportCases.SingleAsync();
            if (cancel) Assert.Equal("Cancelled", request.State);
            else Assert.Equal(ticket.Status == CaseStatus.Rejected ? "Pending24h" : "Cancelled", request.State);
            Assert.InRange(await verify.CaseHistories.CountAsync(h => h.Action == "status_change"), 0, 1);
            await AssertDisputeMoneyAsync(verify, seed, request.State == "Pending24h" ? 300_000 : 0);
        }
        finally { gate.Release.TrySetResult(); await Record.ExceptionAsync(() => first); if (second is not null) await second; }
    }

    private static async Task AssertDisputeMoneyAsync(SpendWiseDbContext db, CoreFundSeed seed, decimal held)
    {
        Assert.Equal(held, await CoreAcceptanceData.HeldAsync(db, seed.FundId));
        var account = await db.V1FundTreasuries.Where(t => t.FundId == seed.FundId).Select(t => t.LedgerAccountId).SingleAsync();
        Assert.Equal(1_000_000, await db.LedgerAccounts.Where(a => a.Id == account).Select(a => a.Balance).SingleAsync());
        Assert.Equal(1_000_000 - held, (await db.LedgerAccounts.SingleAsync(a => a.Id == account)).Balance - await CoreAcceptanceData.HeldAsync(db, seed.FundId));
        Assert.Equal(2, await db.LedgerTransactions.CountAsync());
        var entries = await db.LedgerEntries.ToListAsync();
        Assert.Equal(entries.Where(e => e.Direction == SpendWise.Domain.Finance.LedgerEntryDirection.Debit).Sum(e => e.Amount),
            entries.Where(e => e.Direction == SpendWise.Domain.Finance.LedgerEntryDirection.Credit).Sum(e => e.Amount));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Core_dispute_SQL_user_or_unassigned_admin_cannot_resolve(bool outsiderAdmin)
    {
        var owned = await OwnAsync(Owned.CreateAsync()); await using var db = owned.Context();
        var seed = await CoreAcceptanceData.SeedAsync(db); var id = await CoreAcceptanceData.PendingAsync(db, seed);
        await CoreAcceptanceData.Funds(db).ReportFundRequestAsync(seed.Member.Id, id, "Synthetic report", default);
        var ticket = await db.SupportCases.SingleAsync(); ticket.AssignedAdminId = seed.Admin.Id;
        var actor = CoreAcceptanceHttpTests.SyntheticUser(outsiderAdmin ? SpendWise.Domain.Identity.SystemRole.Admin : SpendWise.Domain.Identity.SystemRole.User);
        var session = new SpendWise.Domain.Identity.UserSession { UserId = actor.Id, LastSeenAt = DateTime.UtcNow };
        db.AddRange(actor, session); await db.SaveChangesAsync();
        await Assert.ThrowsAsync<ForbiddenException>(() => A5(db).ResolveDisputeRequestAsync(
            V1PermissionTests.Principal(actor.Id, session.Id, outsiderAdmin ? "Admin" : "User"), id, new("Synthetic denied attempt", "Synthetic decision", false), default));
        await using var verify = owned.Context();
        Assert.Equal("Suspended", await verify.V1FundRequests.Where(r => r.Id == id).Select(r => r.State).SingleAsync());
        Assert.Equal(seed.Admin.Id, (await verify.SupportCases.SingleAsync()).AssignedAdminId);
        await AssertDisputeMoneyAsync(verify, seed, 300_000);
    }

    private static AdminA5Service A5(SpendWiseDbContext db) => new(db, new(db),
        new(db, new(db), new(db), new NotificationService(db), new SystemClock()), new(db), new NotificationService(db),
        new(new EmailTemplateRenderer(), new NoDisputeEmail(), new DisputeEmailPolicy()), new EmailTemplateRenderer(), new CaseAttachmentValidator(), new SystemClock());

    private sealed class NoDisputeEmail : IEmailDeliveryQueue
    {
        public Task<bool> EnqueueAsync(EmailMessage message, string key, CancellationToken ct) => Task.FromResult(true);
    }
    private sealed class DisputeEmailPolicy : IEmailVerificationPolicy
    {
        public TimeSpan OtpLifetime => TimeSpan.FromMinutes(15);
        public TimeSpan ResendCooldown => TimeSpan.FromMinutes(1);
        public int MaximumFailedAttempts => 5;
        public int MaximumSendsPerHourPerAccount => 10;
        public int MaximumSendsPerHourPerIp => 30;
        public string WebsiteUrl => "https://spendwise.test";
    }
    private sealed class FailDisputeNotification : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (data.Context!.ChangeTracker.Entries<SpendWise.Domain.Notifications.Notification>().Any(e => e.State == EntityState.Added))
                throw new InvalidOperationException("Synthetic dispute notification failure after decision save");
            return ValueTask.FromResult(result);
        }
    }
    private sealed class DisputeReadGate : SaveChangesInterceptor
    {
        private int used;
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (data.Context!.ChangeTracker.Entries<SpendWise.Domain.V1Groups.V1FundRequest>().Any(e => e.State == EntityState.Modified)
                && Interlocked.Exchange(ref used, 1) == 0)
            { Entered.TrySetResult(); await Release.Task.WaitAsync(TimeSpan.FromSeconds(20), ct); }
            return result;
        }
    }
}
