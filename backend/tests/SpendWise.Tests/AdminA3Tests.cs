using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Features.Administration;
using SpendWise.Domain.Admin;
using SpendWise.Domain.Identity;
using SpendWise.Domain.Groups;
using SpendWise.Domain.V1Groups;
using Xunit;

namespace SpendWise.Tests;

public sealed class AdminA3Tests
{
    [Theory]
    [InlineData("Unassigned")] [InlineData("Reassigned")] [InlineData("Revoked")]
    [InlineData("User")] [InlineData("SystemAdmin")] [InlineData("Closed")]
    [InlineData("WrongSubject")] [InlineData("UnclaimedSuperAdmin")]
    public async Task Every_investigation_feed_fails_closed(string condition)
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var role = condition is "User" ? SystemRole.User : condition is "SystemAdmin" ? SystemRole.SystemAdmin : condition is "UnclaimedSuperAdmin" ? SystemRole.SuperAdmin : SystemRole.Admin;
        var actor = await f.AddUserAsync("Operator", role: role); var user = await f.AddUserAsync("Subject"); var peer = await f.AddUserAsync("Peer", role: SystemRole.Admin);
        var fund = new V1Fund("Scoped", "SPW000001", V1FundKind.Free, user.Id, null);
        var ticket = new SupportCase { AssignedAdminId = actor.Id, LinkedUserId = user.Id };
        f.Db.AddRange(ticket, fund, new V1FundMembership { FundId = fund.Id, UserId = user.Id, Seat = 1 }); await f.Db.SaveChangesAsync();
        var session = await f.AddSessionAsync(actor.Id);
        if (condition == "Unassigned" || condition == "UnclaimedSuperAdmin") ticket.AssignedAdminId = null;
        if (condition == "Reassigned") ticket.AssignedAdminId = peer.Id;
        if (condition == "Closed") ticket.Status = CaseStatus.Closed;
        if (condition == "WrongSubject") ticket.LinkedUserId = peer.Id;
        if (condition == "Revoked") session.RevokedAt = f.Clock.UtcNow;
        await f.Db.SaveChangesAsync(); var p = V1PermissionTests.Principal(actor.Id, session.Id, role.ToString()); var s = Service(f);
        Func<Task>[] reads = [() => s.SearchAsync(p, ticket.Id, new("Investigate scope"), default), () => s.DetailAsync(p, ticket.Id, fund.Id, "Investigate scope", default),
            () => s.MembersAsync(p, ticket.Id, fund.Id, new("Investigate scope"), default), () => s.ActivityAsync(p, ticket.Id, fund.Id, new("Investigate scope"), default),
            () => s.FinanceAsync(p, ticket.Id, fund.Id, "Requests", new("Investigate scope"), default), () => s.TreasuryAsync(p, ticket.Id, fund.Id, "Investigate scope", default),
            () => s.GovernanceAsync(p, ticket.Id, fund.Id, "Elections", new("Investigate scope"), default), () => s.CasesAsync(p, ticket.Id, fund.Id, new("Investigate scope"), default),
            () => s.DissolutionAsync(p, ticket.Id, fund.Id, "Investigate scope", default), () => s.InvitationsAsync(p, ticket.Id, fund.Id, new("Investigate scope"), default),
            () => s.ReportHistoryAsync(p, ticket.Id, fund.Id, "Support", ticket.Id, new("Investigate scope"), default),
            () => s.ExpensePostingsAsync(p, ticket.Id, fund.Id, Guid.NewGuid(), new("Investigate scope"), default)];
        foreach (var read in reads) await Assert.ThrowsAsync<ForbiddenException>(read);
        Assert.DoesNotContain(await f.Db.AuditLogs.ToListAsync(), a => a.Action == "A3_FUND_READ");
    }

    [Fact]
    public async Task Dissolved_history_preserves_scope_without_inventing_zero_or_legacy_founder()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync(); var actor = await f.AddUserAsync("Operator", role: SystemRole.Admin); var user = await f.AddUserAsync("Past subject");
        var ticket = new SupportCase { AssignedAdminId = actor.Id, LinkedUserId = user.Id };
        var fund = new V1Fund("Dissolved", "SPW000001", V1FundKind.Free, user.Id, null);
        f.Db.AddRange(ticket, fund, new V1FundMembership { FundId = fund.Id, UserId = user.Id, Seat = 1, LeftAt = f.Clock.UtcNow });
        var legacy = new Group { Name = "Closed legacy", Status = GroupStatus.Closed }; f.Db.AddRange(legacy, new GroupMember { GroupId = legacy.Id, UserId = user.Id, Role = GroupRole.Owner });
        await f.Db.SaveChangesAsync(); await f.Db.V1Funds.Where(x => x.Id == fund.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.DissolvedAt, f.Clock.UtcNow));
        var p = V1PermissionTests.Principal(actor.Id, (await f.AddSessionAsync(actor.Id)).Id, "Admin"); var service = Service(f);
        Assert.Equal(2, (await service.SearchAsync(p, ticket.Id, new("Read dissolved", Status: "Dissolved"), default)).Total);
        var detail = await service.DetailAsync(p, ticket.Id, fund.Id, "Read history", default); Assert.False(detail.CountsAsActiveGroup); Assert.Equal(user.Id, detail.FounderUserId); Assert.NotNull(detail.DissolvedAt);
        var unknown = await service.DetailAsync(p, ticket.Id, legacy.Id, "Read legacy", default); Assert.Null(unknown.FounderUserId); Assert.Null(unknown.DissolvedAt);
        var treasury = await service.TreasuryAsync(p, ticket.Id, fund.Id, "Read treasury", default); Assert.False(treasury.ZeroLedgerEvidence); Assert.Null(treasury.StoredBalance);
        await Assert.ThrowsAsync<AppException>(() => service.SearchAsync(p, ticket.Id, new("Bad paging", Page: 0), default));
        await Assert.ThrowsAsync<ForbiddenException>(() => service.SearchAsync(p, ticket.Id, new("Wrong subject", RelatedUserId: actor.Id), default));
        Assert.Contains(await f.Db.AuditLogs.ToListAsync(), a => a.Action == "A3_FUND_READ" && a.TargetId == fund.Id.ToString());
    }

    [Fact]
    public async Task Members_activity_and_governance_show_persisted_records_without_financial_events_or_private_payloads()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync(); var actor = await f.AddUserAsync("Operator", role: SystemRole.Admin); var user = await f.AddUserAsync("Subject"); var old = await f.AddUserAsync("Past member");
        var ticket = new SupportCase { AssignedAdminId = actor.Id, LinkedUserId = user.Id };
        var fund = new V1Fund("Fixed", "SPW000001", V1FundKind.Fixed, old.Id, 100);
        var election = new V1Election { FundId = fund.Id, IncumbentUserId = old.Id, OpenedByUserId = user.Id, Phase = V1ElectionPhase.Confirmation, PhaseStartedAt = f.Clock.UtcNow, PolicyJson = "private-policy" };
        var proposal = new V1ActivityProposal { FundId = fund.Id, ProposedByUserId = user.Id, TargetVnd = 100, OpenedAt = f.Clock.UtcNow };
        var membership = new V1FundMembership { FundId = fund.Id, UserId = user.Id, Seat = 2, CanSpend = true, JoinedAt = f.Clock.UtcNow };
        f.Db.AddRange(ticket, fund, election, proposal,
            membership,
            new V1FundMembership { FundId = fund.Id, UserId = old.Id, Seat = 1, JoinedAt = f.Clock.UtcNow.AddDays(-2), LeftAt = f.Clock.UtcNow.AddDays(-1) },
            new V1FundActivity { FundId = fund.Id, ActorUserId = user.Id, Event = "SpendingPermissionChanged", Detail = "private-payload" },
            new V1FundActivity { FundId = fund.Id, ActorUserId = user.Id, Event = "Contributed", Detail = "private-money" },
            new V1ElectionBallot { ElectionId = election.Id, VoterUserId = user.Id, Round = 3, Support = true },
            new V1ActivityVoter { ProposalId = proposal.Id, UserId = user.Id, MembershipId = membership.Id, Support = false });
        await f.Db.SaveChangesAsync(); var p = V1PermissionTests.Principal(actor.Id, (await f.AddSessionAsync(actor.Id)).Id, "Admin"); var service = Service(f);
        Assert.Equal(2, (await service.MembersAsync(p, ticket.Id, fund.Id, new("Read membership"), default)).Total);
        Assert.True(Assert.Single((await service.MembersAsync(p, ticket.Id, fund.Id, new("Active members", Status: "Current"), default)).Items).CanSpend);
        var history = await service.ActivityAsync(p, ticket.Id, fund.Id, new("Read activity"), default); Assert.Single(history.Items);
        Assert.DoesNotContain("private-payload", System.Text.Json.JsonSerializer.Serialize(history));
        Assert.Single((await service.GovernanceAsync(p, ticket.Id, fund.Id, "Elections", new("Read election"), default)).Items);
        var ballot = Assert.Single((await service.GovernanceAsync(p, ticket.Id, fund.Id, "Ballots", new("Read votes", ParentId: election.Id), default)).Items); Assert.Equal(3, ballot.Round); Assert.True(ballot.Support);
        Assert.False(Assert.Single((await service.GovernanceAsync(p, ticket.Id, fund.Id, "ActivityVoters", new("Read proposal", ParentId: proposal.Id), default)).Items).Support);
        Assert.Empty(await f.Db.LedgerTransactions.ToListAsync());
    }

    [Theory]
    [InlineData("Expense")] [InlineData("Withdrawal")]
    public async Task Finance_approval_without_ledger_stays_blocked_and_linked_case_is_not_duplicated(string kind)
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync(); var actor = await f.AddUserAsync("Operator", role: SystemRole.Admin); var user = await f.AddUserAsync("Subject");
        var ticket = new SupportCase { AssignedAdminId = actor.Id, LinkedUserId = user.Id, Category = "V1FundGovernance" };
        var fund = new V1Fund("Free", "SPW000001", V1FundKind.Free, user.Id, null);
        var request = new V1FundRequest { FundId = fund.Id, RequestedByUserId = user.Id, RecipientUserId = user.Id, Kind = kind, AmountVnd = 100, State = "ApprovedAwaitingFinanceContract" };
        var history = new CaseHistory { CaseId = ticket.Id, CaseKind = CaseKind.Support, ActorId = user.Id };
        f.Db.AddRange(history, new CaseAttachment { History = history, CaseOwnerUserId = user.Id, ContentType = "image/png", Content = [1, 2], Length = 2 });
        f.Db.AddRange(ticket, fund, request, new V1FundMembership { FundId = fund.Id, UserId = user.Id, Seat = 1 },
            new V1FundActivity { FundId = fund.Id, ActorUserId = user.Id, Event = "OwnerResponseEscalated", ReferenceId = ticket.Id },
            new V1FundActivity { FundId = fund.Id, ActorUserId = user.Id, Event = "OwnerResponseEscalated", ReferenceId = ticket.Id });
        await f.Db.SaveChangesAsync(); var p = V1PermissionTests.Principal(actor.Id, (await f.AddSessionAsync(actor.Id)).Id, "Admin"); var service = Service(f);
        var row = Assert.Single((await service.FinanceAsync(p, ticket.Id, fund.Id, "Requests", new("Read financial intent"), default)).Items);
        Assert.Equal("FINANCE_BLOCKED", row.Verification); Assert.Null(row.LedgerTransactionId); Assert.Equal(request.State, row.State);
        Assert.Equal(1, Assert.Single((await service.CasesAsync(p, ticket.Id, fund.Id, new("Read linked report"), default)).Items).EvidenceCount);
        await Assert.ThrowsAsync<AppException>(() => service.GovernanceAsync(p, ticket.Id, fund.Id, "ActivityProposals", new("Wrong fund type"), default));
        Assert.Equal(1, await f.Db.SupportCases.CountAsync()); Assert.Empty(await f.Db.LedgerEntries.ToListAsync());
    }

    internal static AdminA3Service Service(TargetedCorrectnessFixture f) => new(f.Db,
        new(f.Db, new(f.Db), new(f.Db), new SpendWise.Infrastructure.Notifications.NotificationService(f.Db), f.Clock), new(f.Db), f.Clock);

    [Theory]
    [InlineData(SystemRole.Admin)] [InlineData(SystemRole.SuperAdmin)]
    public async Task Exact_SPW_lookup_and_filtered_lists_require_assigned_subject_membership(SystemRole role)
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync(); var actor = await f.AddUserAsync("Operator", role: role); var user = await f.AddUserAsync("Subject"); var other = await f.AddUserAsync("Other");
        var ticket = new SupportCase { LinkedUserId = user.Id, AssignedAdminId = role == SystemRole.Admin ? actor.Id : null,
            EscalatedAt = role == SystemRole.SuperAdmin ? f.Clock.UtcNow : null, EscalationReviewerId = role == SystemRole.SuperAdmin ? actor.Id : null };
        var fund = new V1Fund("In scope", "SPW000001", V1FundKind.Fixed, user.Id, 100); var foreign = new V1Fund("Not in scope", "SPW000002", V1FundKind.Free, other.Id, null);
        var legacy = new Group { Name = "Legacy", Type = GroupType.Family };
        f.Db.AddRange(ticket, fund, foreign, legacy, new V1FundMembership { FundId = fund.Id, UserId = user.Id, Seat = 1, JoinedAt = f.Clock.UtcNow },
            new V1FundMembership { FundId = foreign.Id, UserId = other.Id, Seat = 1, JoinedAt = f.Clock.UtcNow }, new GroupMember { GroupId = legacy.Id, UserId = user.Id, JoinedAt = f.Clock.UtcNow });
        await f.Db.SaveChangesAsync(); var p = V1PermissionTests.Principal(actor.Id, (await f.AddSessionAsync(actor.Id)).Id, role.ToString()); var service = Service(f);
        Assert.Equal(2, (await service.SearchAsync(p, ticket.Id, new("Investigate groups"), default)).Total);
        Assert.Equal(fund.Id, Assert.Single((await service.SearchAsync(p, ticket.Id, new("Exact code", Code: "SPW000001"), default)).Items).Id);
        Assert.Empty((await service.SearchAsync(p, ticket.Id, new("Unknown code", Code: "SPW999999"), default)).Items);
        await Assert.ThrowsAsync<AppException>(() => service.SearchAsync(p, ticket.Id, new("Bad code", Code: "SPW1"), default));
        Assert.Equal(legacy.Id, Assert.Single((await service.SearchAsync(p, ticket.Id, new("Legacy taxonomy", Kind: "Legacy"), default)).Items).Id);
        Assert.Equal(30, Assert.Single((await service.SearchAsync(p, ticket.Id, new("Fixed kind", Kind: "Fixed"), default)).Items).MaximumMembers);
        var first = await service.SearchAsync(p, ticket.Id, new("Page one", PageSize: 1), default); var second = await service.SearchAsync(p, ticket.Id, new("Page two", Page: 2, PageSize: 1), default); Assert.NotEqual(first.Items[0].Id, second.Items[0].Id);
        await Assert.ThrowsAsync<ForbiddenException>(() => service.DetailAsync(p, ticket.Id, foreign.Id, "Cross group attempt", default));
        await f.Db.SupportCases.Where(c => c.Id == ticket.Id).ExecuteUpdateAsync(s => s.SetProperty(c => c.Status, CaseStatus.Closed));
        await Assert.ThrowsAsync<ForbiddenException>(() => service.DetailAsync(p, ticket.Id, fund.Id, "Closed case", default));
    }
}
