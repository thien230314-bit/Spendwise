using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Features.Administration;
using SpendWise.Domain.Admin;
using SpendWise.Domain.Finance;
using SpendWise.Domain.Identity;
using SpendWise.Infrastructure.Persistence;
using Xunit;

namespace SpendWise.Tests;

public sealed class V1CaseTransactionTests
{
    [Theory]
    [InlineData(SystemRole.Admin, true)]
    [InlineData(SystemRole.SuperAdmin, false)]
    [InlineData(SystemRole.SystemAdmin, false)]
    [InlineData(SystemRole.User, false)]
    public async Task Exact_assigned_transaction_read_is_scoped_audited_and_never_repairs_ledger(SystemRole role, bool allowed)
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var actor = await f.AddUserAsync("Operator", role: role); var subject = await f.AddUserAsync("Subject");
        var session = await f.AddSessionAsync(actor.Id);
        var (ticket, transaction) = await SeedAsync(f, actor.Id, subject.Id, balanced: false);
        var reader = new ScopedCaseTransactionReader(f.Db, new(f.Db));
        var principal = V1PermissionTests.Principal(actor.Id, session.Id, role.ToString());
        if (allowed)
        {
            var result = await reader.GetAsync(principal, ticket.Id, subject.Id, "Investigate assigned payment", default);
            Assert.Equal(transaction.Id, result.Id);
            var totals = Assert.Single(result.LedgerTotals);
            Assert.Equal(25, totals.Debit); Assert.Equal(20, totals.Credit); Assert.False(totals.Balanced);
            Assert.Equal("V1_CASE_TRANSACTION_READ", Assert.Single(await f.Db.AuditLogs.ToListAsync()).Action);
        }
        else await Assert.ThrowsAsync<ForbiddenException>(() => reader.GetAsync(principal, ticket.Id, subject.Id, "Investigate assigned payment", default));
        Assert.Single(await f.Db.FinancialTransactions.ToListAsync());
        Assert.Equal(new decimal[] { 20, 25 }, await f.Db.LedgerEntries.OrderBy(e => e.Amount).Select(e => e.Amount).ToArrayAsync());
    }

    [Theory]
    [InlineData("subject")]
    [InlineData("reassigned")]
    [InlineData("closed")]
    [InlineData("revoked")]
    public async Task Case_and_session_changes_deny_without_returning_financial_data(string change)
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var actor = await f.AddUserAsync("Operator", role: SystemRole.Admin); var subject = await f.AddUserAsync("Subject");
        var session = await f.AddSessionAsync(actor.Id);
        var (ticket, _) = await SeedAsync(f, actor.Id, subject.Id);
        if (change == "reassigned") ticket.AssignedAdminId = null;
        if (change == "closed") ticket.Status = CaseStatus.Closed;
        if (change == "revoked") session.RevokedAt = f.Clock.UtcNow;
        await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<ForbiddenException>(() => new ScopedCaseTransactionReader(f.Db, new(f.Db)).GetAsync(
            V1PermissionTests.Principal(actor.Id, session.Id, "Admin"), ticket.Id,
            change == "subject" ? Guid.NewGuid() : subject.Id, "Investigate assigned payment", default));
        Assert.Equal("V1_CASE_TRANSACTION_READ_DENIED", Assert.Single(await f.Db.AuditLogs.ToListAsync()).Action);
    }

    [Fact]
    public async Task Case_link_to_another_users_transaction_does_not_grant_access()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var actor = await f.AddUserAsync("Operator", role: SystemRole.Admin); var subject = await f.AddUserAsync("Subject");
        var other = await f.AddUserAsync("Other"); var session = await f.AddSessionAsync(actor.Id);
        var (ticket, _) = await SeedAsync(f, actor.Id, other.Id);
        ticket.LinkedUserId = subject.Id; await f.Db.SaveChangesAsync();
        var error = await Assert.ThrowsAsync<NotFoundException>(() => new ScopedCaseTransactionReader(f.Db, new(f.Db)).GetAsync(
            V1PermissionTests.Principal(actor.Id, session.Id, "Admin"), ticket.Id, subject.Id, "Investigate assigned payment", default));
        Assert.Equal("case_transaction_not_found", error.Code);
    }

    [Fact]
    public async Task Reassignment_before_ledger_read_prevents_response()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var actor = await f.AddUserAsync("Operator", role: SystemRole.Admin); var subject = await f.AddUserAsync("Subject");
        var session = await f.AddSessionAsync(actor.Id); var (ticket, _) = await SeedAsync(f, actor.Id, subject.Id);
        var gate = new ReassignBeforeEntries(async () => await f.Db.SupportCases.Where(c => c.Id == ticket.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.AssignedAdminId, (Guid?)null)));
        await using var request = new SpendWiseDbContext(new DbContextOptionsBuilder<SpendWiseDbContext>()
            .UseSqlite(f.Db.Database.GetDbConnection()).AddInterceptors(gate).Options, f.Clock);
        await Assert.ThrowsAsync<ForbiddenException>(() => new ScopedCaseTransactionReader(request, new(request)).GetAsync(
            V1PermissionTests.Principal(actor.Id, session.Id, "Admin"), ticket.Id, subject.Id, "Investigate assigned payment", default));
        Assert.True(gate.Triggered);
    }

    private static async Task<(SupportCase, FinancialTransaction)> SeedAsync(TargetedCorrectnessFixture f, Guid actor, Guid user, bool balanced = true)
    {
        var account = new LedgerAccount { Code = Guid.NewGuid().ToString(), Name = "Synthetic", Type = LedgerAccountType.ExternalSource };
        var ledger = new LedgerTransaction { Reference = Guid.NewGuid().ToString(), BusinessType = "Synthetic", Status = LedgerTransactionStatus.Completed };
        var transaction = new FinancialTransaction { OwnerUserId = user, Amount = 25, LedgerTransaction = ledger,
            LedgerTransactionId = ledger.Id, Type = FinancialTransactionType.Income, OccurredAt = f.Clock.UtcNow };
        var ticket = new SupportCase { LinkedUserId = user, LinkedTransactionId = transaction.Id, AssignedAdminId = actor,
            Subject = "Synthetic case", Description = "Synthetic evidence" };
        f.Db.AddRange(account, transaction, ticket);
        f.Db.LedgerEntries.AddRange(new LedgerEntry { LedgerTransactionId = ledger.Id, LedgerAccountId = account.Id, Direction = LedgerEntryDirection.Debit, Amount = 25 },
            new LedgerEntry { LedgerTransactionId = ledger.Id, LedgerAccountId = account.Id, Direction = LedgerEntryDirection.Credit, Amount = balanced ? 25 : 20 });
        await f.Db.SaveChangesAsync();
        return (ticket, transaction);
    }

    private sealed class ReassignBeforeEntries(Func<Task> reassign) : DbCommandInterceptor
    {
        public bool Triggered { get; private set; }
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData data,
            InterceptionResult<DbDataReader> result, CancellationToken ct = default)
        {
            if (!Triggered && command.CommandText.Contains("FROM \"LedgerEntries\"", StringComparison.Ordinal))
            { Triggered = true; await reassign(); }
            return result;
        }
    }
}
