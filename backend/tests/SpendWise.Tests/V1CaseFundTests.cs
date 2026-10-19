using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Features.Administration;
using SpendWise.Domain.Admin;
using SpendWise.Domain.Identity;
using SpendWise.Domain.V1Groups;
using Xunit;

namespace SpendWise.Tests;

public sealed class V1CaseFundTests
{
    [Theory]
    [InlineData(SystemRole.Admin, true)]
    [InlineData(SystemRole.SuperAdmin, true)]
    [InlineData(SystemRole.SystemAdmin, false)]
    [InlineData(SystemRole.User, false)]
    public async Task Assigned_case_reads_only_subject_memberships_and_audits_access(SystemRole role, bool allowed)
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var actor = await f.AddUserAsync("Operator", role: role); var subject = await f.AddUserAsync("Subject"); var other = await f.AddUserAsync("Other");
        var session = await f.AddSessionAsync(actor.Id);
        var ticket = new SupportCase { LinkedUserId = subject.Id, AssignedAdminId = actor.Id, Subject = "Synthetic", Description = "Synthetic" };
        var fund = new V1Fund("Subject fund", "SPW000001", V1FundKind.Free, subject.Id, null);
        var unrelated = new V1Fund("Private unrelated", "SPW000002", V1FundKind.Free, other.Id, null);
        f.Db.AddRange(ticket, fund, unrelated, new V1FundMembership { FundId = fund.Id, UserId = subject.Id, Seat = 1, JoinedAt = f.Clock.UtcNow, LeftAt = f.Clock.UtcNow },
            new V1FundMembership { FundId = unrelated.Id, UserId = other.Id, Seat = 1 }); await f.Db.SaveChangesAsync();
        var reader = new ScopedCaseFundReader(f.Db, new(f.Db)); var principal = V1PermissionTests.Principal(actor.Id, session.Id, role.ToString());
        if (allowed)
        {
            var row = Assert.Single(await reader.GetAsync(principal, ticket.Id, subject.Id, "Investigate assigned fund membership", null, 1, 20, default));
            Assert.Equal(fund.Id, row.FundId); Assert.NotNull(row.LeftAt);
            Assert.Empty(await reader.GetAsync(principal, ticket.Id, subject.Id, "Find exact fund code", unrelated.Code, 1, 20, default));
            ticket.AssignedAdminId = null; await f.Db.SaveChangesAsync();
            await Assert.ThrowsAsync<ForbiddenException>(() => reader.GetAsync(principal, ticket.Id, subject.Id, "Check reassigned case", null, 1, 20, default));
        }
        else await Assert.ThrowsAsync<ForbiddenException>(() => reader.GetAsync(principal, ticket.Id, subject.Id, "Investigate assigned fund membership", null, 1, 20, default));
        Assert.NotEmpty(await f.Db.AuditLogs.ToListAsync()); Assert.Empty(await f.Db.LedgerTransactions.ToListAsync());
    }
}
