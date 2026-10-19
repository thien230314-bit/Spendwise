using SpendWise.Application.Features.Administration;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Domain.Admin;
using SpendWise.Domain.Identity;
using Xunit;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SpendWise.Infrastructure.Persistence;

namespace SpendWise.Tests;

public sealed class V1CaseProfileTests
{
    [Fact]
    public async Task Assigned_read_is_masked_and_audited_but_another_subject_is_denied()
    {
        var role = SystemRole.Admin;
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var actor = await f.AddUserAsync("Assigned operator", role: role);
        var target = await f.AddUserAsync("Case subject");
        var other = await f.AddUserAsync("Outside case");
        var session = new UserSession { UserId = actor.Id, LastSeenAt = f.Clock.UtcNow };
        var ticket = new SupportCase { LinkedUserId = target.Id, AssignedAdminId = actor.Id, Subject = "Case", Description = "Synthetic" };
        f.Db.AddRange(session, ticket);
        await f.Db.SaveChangesAsync();
        var reader = new ScopedCaseProfileReader(f.Db, new PlatformPermissionService(f.Db));
        var principal = V1PermissionTests.Principal(actor.Id, session.Id, role.ToString());

        var profile = await reader.GetAsync(principal, ticket.Id, target.Id, "Investigate assigned case", default);
        Assert.Equal(target.Id, profile.Id);
        Assert.NotEqual(target.Email, profile.MaskedEmail);
        Assert.DoesNotContain(target.Email.Split('@')[0], profile.MaskedEmail);
        var denied = await Assert.ThrowsAsync<ForbiddenException>(() =>
            reader.GetAsync(principal, ticket.Id, other.Id, "Investigate assigned case", default));
        Assert.Equal("case_access_denied", denied.Code);
        Assert.Contains(f.Db.AuditLogs, x => x.Action == "V1_PROFILE_READ");
        Assert.Contains(f.Db.AuditLogs, x => x.Action == "V1_PROFILE_READ_DENIED");
        Assert.Contains(other.Id.ToString(), f.Db.AuditLogs.Single(x => x.Action == "V1_PROFILE_READ_DENIED").MetadataJson);
        Assert.Empty(f.Db.LedgerTransactions);
    }

    [Fact]
    public async Task Reassignment_between_precheck_and_data_query_denies_profile()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var actor = await f.AddUserAsync("Operator", role: SystemRole.Admin);
        var target = await f.AddUserAsync("Subject");
        var session = await f.AddSessionAsync(actor.Id);
        var ticket = new SupportCase { LinkedUserId = target.Id, AssignedAdminId = actor.Id, Subject = "Case", Description = "Synthetic" };
        f.Db.SupportCases.Add(ticket); await f.Db.SaveChangesAsync();
        var gate = new ReassignBeforeRead(async () => await f.Db.SupportCases.Where(c => c.Id == ticket.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.AssignedAdminId, (Guid?)null)));
        await using var requestDb = new SpendWiseDbContext(new DbContextOptionsBuilder<SpendWiseDbContext>()
            .UseSqlite(f.Db.Database.GetDbConnection()).AddInterceptors(gate).Options, f.Clock);
        var reader = new ScopedCaseProfileReader(requestDb, new PlatformPermissionService(requestDb));
        await Assert.ThrowsAsync<ForbiddenException>(() => reader.GetAsync(
            V1PermissionTests.Principal(actor.Id, session.Id, "Admin"), ticket.Id, target.Id, "Assigned purpose", default));
        Assert.True(gate.Triggered);
    }

    [Fact]
    public async Task Invalid_reason_attempt_is_audited_without_persisting_untrusted_reason()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var actor = await f.AddUserAsync("Operator", role: SystemRole.Admin);
        var target = await f.AddUserAsync("Subject");
        var session = await f.AddSessionAsync(actor.Id);
        await Assert.ThrowsAsync<AppException>(() => new ScopedCaseProfileReader(f.Db, new(f.Db)).GetAsync(
            V1PermissionTests.Principal(actor.Id, session.Id, "Admin"), Guid.NewGuid(), target.Id, "x", default));
        var denied = Assert.Single(await f.Db.AuditLogs.ToListAsync());
        Assert.Equal("V1_PROFILE_READ_DENIED", denied.Action);
        Assert.NotEqual("x", denied.Reason);
        Assert.Contains(target.Id.ToString(), denied.MetadataJson);
    }

    private sealed class ReassignBeforeRead(Func<Task> reassign) : DbCommandInterceptor
    {
        public bool Triggered { get; private set; }
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken ct = default)
        {
            if (!Triggered && command.CommandText.Contains("FROM \"Users\"", StringComparison.Ordinal))
            {
                Triggered = true;
                await reassign();
            }
            return result;
        }
    }
}
