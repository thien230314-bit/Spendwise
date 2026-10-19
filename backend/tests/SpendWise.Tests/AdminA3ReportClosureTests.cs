using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Domain.Admin;
using SpendWise.Domain.Finance;
using SpendWise.Domain.Groups;
using SpendWise.Domain.Identity;
using Xunit;

namespace SpendWise.Tests;

public sealed class AdminA3ReportClosureTests
{
    [Fact]
    public async Task Explicit_fund_task_reads_its_subject_disputes_without_requiring_duplicate_support_cases()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync(); var a = await f.AddUserAsync("Operator", role: SystemRole.Admin); var user = await f.AddUserAsync("Subject");
        var fund = new SpendWise.Domain.V1Groups.V1Fund("V1", "SPW000001", SpendWise.Domain.V1Groups.V1FundKind.Free, user.Id, null);
        var ticket = new SupportCase { AssignedAdminId = a.Id, LinkedUserId = user.Id }; var wallet = await f.AddWalletAsync(user.Id, "Main", true);
        var membership = new SpendWise.Domain.V1Groups.V1FundMembership { FundId = fund.Id, UserId = user.Id, Seat = 1 };
        var posting = new LedgerTransaction { Reference = "Fixture", BusinessType = "V1FundContribution" };
        var tx = new FinancialTransaction { OwnerUserId = user.Id, LedgerTransactionId = posting.Id, Amount = 100 };
        var dispute = new Dispute { UserId = user.Id, LinkedTransactionId = tx.Id };
        f.Db.AddRange(fund, ticket, membership, posting, tx, dispute,
            new SpendWise.Domain.V1Groups.V1FundContribution { FundId = fund.Id, UserId = user.Id, MembershipId = membership.Id, WalletId = wallet.Id, AmountVnd = 100, LedgerTransactionId = posting.Id });
        await f.Db.SaveChangesAsync(); var p = V1PermissionTests.Principal(a.Id, (await f.AddSessionAsync(a.Id)).Id, "Admin"); var s = AdminA3Tests.Service(f);
        Assert.Empty((await s.CasesAsync(p, ticket.Id, fund.Id, new("Membership is not a report link"), default)).Items);
        f.Db.Add(new SpendWise.Domain.V1Groups.V1FundActivity { FundId = fund.Id, ActorUserId = user.Id, Event = "OwnerResponseEscalated", ReferenceId = ticket.Id }); await f.Db.SaveChangesAsync();
        var rows = await s.CasesAsync(p, ticket.Id, fund.Id, new("Investigate explicit fund task"), default);
        Assert.Equal(2, rows.Total); Assert.Contains(rows.Items, r => r.Source == "Dispute" && r.Id == dispute.Id);
        Assert.Equal(1, await f.Db.SupportCases.CountAsync());
    }

    [Theory]
    [InlineData(SystemRole.Admin)] [InlineData(SystemRole.SuperAdmin)]
    public async Task Multiple_reports_require_assigned_task_exact_subject_fund_and_transaction(SystemRole role)
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync(); var actor = await f.AddUserAsync("Operator", role: role); var user = await f.AddUserAsync("Subject"); var peer = await f.AddUserAsync("Peer", role: SystemRole.Admin); var other = await f.AddUserAsync("Other");
        var group = new Group { Name = "Fund" }; var account = new LedgerAccount { Code = "FixtureFund", Type = LedgerAccountType.SharedFund }; var posting = new LedgerTransaction { Reference = "Fixture", BusinessType = "Historical", Status = LedgerTransactionStatus.Pending };
        var tx = new FinancialTransaction { OwnerUserId = user.Id, LedgerTransactionId = posting.Id, Amount = 100 };
        SupportCase Ticket(Guid assignee, Guid subject) => new() { TicketNumber = Guid.NewGuid().ToString("N"), AssignedAdminId = role == SystemRole.Admin || assignee != actor.Id ? assignee : null, LinkedUserId = subject, LinkedTransactionId = tx.Id,
            EscalatedAt = role == SystemRole.SuperAdmin && assignee == actor.Id ? f.Clock.UtcNow : null, EscalationReviewerId = role == SystemRole.SuperAdmin && assignee == actor.Id ? actor.Id : null };
        var root = Ticket(actor.Id, user.Id); var second = Ticket(actor.Id, user.Id); var foreign = Ticket(peer.Id, user.Id); var crossUser = Ticket(actor.Id, other.Id); var closed = Ticket(actor.Id, user.Id); closed.Status = CaseStatus.Closed;
        var d1 = new Dispute { UserId = user.Id, LinkedTransactionId = tx.Id }; var d2 = new Dispute { UserId = user.Id, LinkedTransactionId = tx.Id, Status = CaseStatus.Investigating }; var otherDispute = new Dispute { UserId = other.Id, LinkedTransactionId = tx.Id };
        var history = new CaseHistory { CaseId = d1.Id, CaseKind = CaseKind.Dispute, ActorId = user.Id, ActorRole = "User", Action = "Submitted", Message = "private narrative" };
        f.Db.AddRange(group, account, posting, tx, root, second, foreign, crossUser, closed, d1, d2, otherDispute, history,
            new CaseAttachment { History = history, CaseOwnerUserId = user.Id, Content = [1], Length = 1 },
            new GroupMember { GroupId = group.Id, UserId = user.Id, Role = GroupRole.Owner }, new SharedFund { Group = group, LedgerAccount = account },
            new FundTransaction { GroupId = group.Id, UserId = user.Id, Amount = 100, LedgerTransactionId = posting.Id });
        await f.Db.SaveChangesAsync(); var session = await f.AddSessionAsync(actor.Id); var p = V1PermissionTests.Principal(actor.Id, session.Id, role.ToString()); var s = AdminA3Tests.Service(f);
        var reports = await s.CasesAsync(p, root.Id, group.Id, new("Investigate affected transaction"), default);
        Assert.Equal(4, reports.Total); Assert.Equal(4, reports.Items.Select(r => (r.Source, r.Id)).Distinct().Count());
        Assert.DoesNotContain(reports.Items, r => r.Id == foreign.Id || r.Id == closed.Id || r.Id == crossUser.Id || r.Id == otherDispute.Id);
        var dispute = Assert.Single(reports.Items, r => r.Id == d1.Id); Assert.Equal(1, dispute.EvidenceCount); Assert.StartsWith("NOT_IMPLEMENTED", dispute.HoldState); Assert.Contains("/reports/Dispute/", dispute.DetailEndpoint);
        var events = await s.ReportHistoryAsync(p, root.Id, group.Id, "Dispute", d1.Id, new("Trace report evidence"), default); Assert.Equal(history.Id, Assert.Single(events.Items).Id);
        Assert.DoesNotContain("private narrative", System.Text.Json.JsonSerializer.Serialize(events));
        await Assert.ThrowsAsync<ForbiddenException>(() => s.ReportHistoryAsync(p, root.Id, group.Id, "Support", foreign.Id, new("Reject foreign case"), default));
        await Assert.ThrowsAsync<ForbiddenException>(() => s.ReportHistoryAsync(p, root.Id, group.Id, "Dispute", otherDispute.Id, new("Reject foreign dispute"), default));
        Assert.Equal(GroupStatus.Active, (await f.Db.Groups.AsNoTracking().SingleAsync(g => g.Id == group.Id)).Status); Assert.Empty(await f.Db.LedgerEntries.ToListAsync());
        await f.Db.UserSessions.Where(x => x.Id == session.Id).ExecuteUpdateAsync(x => x.SetProperty(v => v.RevokedAt, f.Clock.UtcNow));
        await Assert.ThrowsAsync<ForbiddenException>(() => s.CasesAsync(p, root.Id, group.Id, new("Revoked report scope"), default));
    }

    [Fact]
    public async Task Legacy_invitation_states_and_missing_history_are_not_fabricated()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync(); var a = await f.AddUserAsync("Operator", role: SystemRole.Admin); var user = await f.AddUserAsync("Subject");
        var group = new Group { Name = "Legacy" }; var ticket = new SupportCase { AssignedAdminId = a.Id, LinkedUserId = user.Id };
        f.Db.AddRange(group, ticket, new GroupMember { GroupId = group.Id, UserId = user.Id, Role = GroupRole.Owner },
            new GroupActivity { GroupId = group.Id, ActorUserId = user.Id, Type = GroupActivityType.MemberInvited, DataJson = "{\"InvitedUserId\":\"" + user.Id + "\",\"Token\":\"private-token\"}" },
            new GroupActivity { GroupId = group.Id, ActorUserId = user.Id, Type = GroupActivityType.MemberInvited, DataJson = "broken-json" });
        foreach (var state in Enum.GetValues<InviteStatus>()) f.Db.Add(new GroupInvite { GroupId = group.Id, InvitedByUserId = user.Id, InvitedUserId = user.Id, Status = state, Token = "private-token-" + state, ExpiresAt = f.Clock.UtcNow.AddDays(1) });
        await f.Db.SaveChangesAsync(); var p = V1PermissionTests.Principal(a.Id, (await f.AddSessionAsync(a.Id)).Id, "Admin"); var s = AdminA3Tests.Service(f);
        var invites = await s.InvitationsAsync(p, ticket.Id, group.Id, new("Trace invitations"), default); Assert.Equal(5, invites.Total); Assert.All(invites.Items, i => Assert.Null(i.AcceptedAt));
        var events = await s.ActivityAsync(p, ticket.Id, group.Id, new("Trace missing history"), default); Assert.Equal(2, events.Total); Assert.All(events.Items, i => { Assert.Null(i.CorrelationId); Assert.Equal("NOT_RECORDED", i.ReferenceVerification); Assert.Equal("NOT_RECORDED", i.Cause); });
        Assert.DoesNotContain("private-token", System.Text.Json.JsonSerializer.Serialize(events)); Assert.DoesNotContain("private-token", System.Text.Json.JsonSerializer.Serialize(invites));
        Assert.Empty((await s.CasesAsync(p, ticket.Id, group.Id, new("No report inferred from membership"), default)).Items);
    }
}
