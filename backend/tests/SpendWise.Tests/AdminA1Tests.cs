using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Features.Administration;
using SpendWise.Domain.Admin;
using SpendWise.Domain.Finance;
using SpendWise.Domain.Identity;
using SpendWise.Domain.Notifications;
using SpendWise.Domain.V1Groups;
using Xunit;

namespace SpendWise.Tests;

public sealed class AdminA1Tests
{
    [Fact]
    public async Task Task_pagination_status_and_nullable_deadline_filters_preserve_order_without_inventing_SLA()
    {
        var clock = new V1GovernanceFinishTests.Clock(); await using var f = await TargetedCorrectnessFixture.CreateAsync(clock);
        var actor = await f.AddUserAsync("Operator", role: SystemRole.Admin); var user = await f.AddUserAsync("Subject");
        var principal = V1PermissionTests.Principal(actor.Id, (await f.AddSessionAsync(actor.Id)).Id, "Admin");
        var a = new SupportCase { AssignedAdminId = actor.Id, LinkedUserId = user.Id, TicketNumber = "FIRST", DueAt = clock.UtcNow.AddSeconds(-1) };
        var b = new SupportCase { AssignedAdminId = actor.Id, LinkedUserId = user.Id, TicketNumber = "SECOND", Status = CaseStatus.WaitingForUser, DueAt = clock.UtcNow.AddHours(1) };
        var c = new SupportCase { AssignedAdminId = actor.Id, LinkedUserId = user.Id, TicketNumber = "THIRD", Status = CaseStatus.Investigating };
        f.Db.AddRange(a, b, c); await f.Db.SaveChangesAsync();
        await f.Db.SupportCases.Where(x => x.Id == a.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.CreatedAt, clock.UtcNow.AddMinutes(-3)));
        await f.Db.SupportCases.Where(x => x.Id == b.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.CreatedAt, clock.UtcNow.AddMinutes(-2)));
        await f.Db.SupportCases.Where(x => x.Id == c.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.CreatedAt, clock.UtcNow.AddMinutes(-1)));
        var service = new AdminA1Service(f.Db, new(f.Db), clock, new(f.Db));
        Assert.Equal(DateTimeKind.Utc, Assert.Single((await service.TasksAsync(principal, new(1, 1), default)).Items).CreatedAt.Kind);
        Assert.Equal(a.Id, Assert.Single((await service.TasksAsync(principal, new(1, 1), default)).Items).Id);
        Assert.Equal(b.Id, Assert.Single((await service.TasksAsync(principal, new(2, 1), default)).Items).Id);
        Assert.Equal(3, (await service.TasksAsync(principal, new(3, 1), default)).Total);
        Assert.Equal(b.Id, Assert.Single((await service.TasksAsync(principal, new(Status: "WaitingForUser", Due: "Scheduled"), default)).Items).Id);
        Assert.Equal("NotSet", Assert.Single((await service.TasksAsync(principal, new(Due: "NotSet"), default)).Items).DeadlineState);
    }

    [Fact]
    public async Task Timeline_includes_authoritative_protection_and_system_operation_without_raw_notes_or_duplicate_case_audit()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync(); var actor = await f.AddUserAsync("Operator", role: SystemRole.Admin);
        var sup = await f.AddUserAsync("Supervisor", role: SystemRole.SuperAdmin); var subject = await f.AddUserAsync("Subject"); var unrelated = await f.AddUserAsync("Unrelated");
        var ticket = new SupportCase { AssignedAdminId = actor.Id, LinkedUserId = subject.Id, TicketNumber = "TIMELINE" };
        var plan = new V1MaintenancePlan { CreatedByUserId = sup.Id, StartsAt = f.Clock.UtcNow.AddMinutes(-1), ExpectedEndsAt = f.Clock.UtcNow.AddHours(1), Reason = "private-note" };
        f.Db.AddRange(ticket, plan, new CaseHistory { CaseId = ticket.Id, CaseKind = CaseKind.Support, ActorId = actor.Id, Action = "reply", Message = "private-note" },
            new AuditLog { ActorUserId = actor.Id, Action = "LOCK_USER", TargetType = "User", TargetId = subject.Id.ToString(), Reason = "private-note" },
            new AuditLog { ActorUserId = actor.Id, Action = "LOCK_USER", TargetType = "User", TargetId = unrelated.Id.ToString(), Reason = "private-note" },
            new AuditLog { ActorUserId = sup.Id, Action = "V1_MAINTENANCE_ACTIVATED", TargetType = "V1MaintenancePlan", TargetId = plan.Id.ToString(), Reason = "private-note" });
        await f.Db.SaveChangesAsync(); var principal = V1PermissionTests.Principal(actor.Id, (await f.AddSessionAsync(actor.Id)).Id, "Admin"); var service = new AdminA1Service(f.Db, new(f.Db), f.Clock, new(f.Db));
        var page = await service.ActivityAsync(principal, new(), default); Assert.Equal(3, page.Total);
        Assert.All(page.Items, e => Assert.Equal(DateTimeKind.Utc, e.At.Kind));
        Assert.Null(Assert.Single(page.Items, e => e.Source == "MaintenanceAudit").ActorId);
        Assert.Single(page.Items, e => e.Source == "ProtectionAudit"); Assert.DoesNotContain("private-note", JsonSerializer.Serialize(page));
        Assert.Equal(1, (await service.ActivityAsync(principal, new(Source: "ProtectionAudit"), default)).Total);
    }
    [Fact]
    public async Task Summary_zero_data_and_Vietnam_half_open_day_do_not_invent_revenue()
    {
        var role = SystemRole.Admin;
        var clock = new V1GovernanceFinishTests.Clock { UtcNow = new(2026, 10, 19, 18, 0, 0, DateTimeKind.Utc) };
        await using var f = await TargetedCorrectnessFixture.CreateAsync(clock); var actor = await f.AddUserAsync("Operator", role: role);
        var principal = V1PermissionTests.Principal(actor.Id, (await f.AddSessionAsync(actor.Id)).Id, role.ToString());
        var service = new AdminA1Service(f.Db, new(f.Db), f.Clock, new(f.Db));
        var empty = await service.SummaryAsync(principal, null, default);
        Assert.Equal(new DateOnly(2026, 10, 20), empty.Date); Assert.Equal(clock.UtcNow.Date.AddHours(17), empty.FromUtc);
        Assert.Equal(0, empty.UsersCreated); Assert.Equal(0, empty.GroupsCreated); Assert.Empty(empty.PostingStatuses); Assert.Empty(empty.CompletedDebitVolumes);
        var before = await f.AddUserAsync("Before"); var at = await f.AddUserAsync("At"); var end = await f.AddUserAsync("End");
        await f.Db.Users.Where(u => u.Id == before.Id).ExecuteUpdateAsync(s => s.SetProperty(u => u.CreatedAt, empty.FromUtc.AddTicks(-1)));
        await f.Db.Users.Where(u => u.Id == at.Id).ExecuteUpdateAsync(s => s.SetProperty(u => u.CreatedAt, empty.FromUtc));
        await f.Db.Users.Where(u => u.Id == end.Id).ExecuteUpdateAsync(s => s.SetProperty(u => u.CreatedAt, empty.ToUtc));
        var result = await service.SummaryAsync(principal, null, default);
        Assert.Equal(1, result.UsersCreated); Assert.Equal(clock.UtcNow, result.ObservedAtUtc);
        Assert.Contains(result.Sources, s => s.Code == "usersCreated" && s.TimestampField == "CreatedAt");
    }

    [Fact]
    public async Task Summary_counts_canonical_postings_not_mirrored_history_and_separates_currency_and_status()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync(); var actor = await f.AddUserAsync("Operator", role: SystemRole.Admin);
        var user = await f.AddUserAsync("User"); var wallet = await f.AddWalletAsync(user.Id, "Main", true);
        var tx = new LedgerTransaction { Reference = "A1-SYNTHETIC", BusinessType = "Synthetic", Status = LedgerTransactionStatus.Completed };
        var failed = new LedgerTransaction { Reference = "A1-FAILED", BusinessType = "Synthetic", Status = LedgerTransactionStatus.Failed };
        var usd = new LedgerTransaction { Reference = "A1-USD", BusinessType = "Synthetic", Status = LedgerTransactionStatus.Completed };
        var usdSource = new LedgerAccount { Code = "A1-USD-SOURCE", Currency = "USD", Type = LedgerAccountType.ExternalSource };
        var usdDestination = new LedgerAccount { Code = "A1-USD-DESTINATION", Currency = "USD", Type = LedgerAccountType.ExternalSource };
        f.Db.AddRange(tx, failed, usd, usdSource, usdDestination);
        f.Db.AddRange(new LedgerEntry { LedgerTransactionId = usd.Id, LedgerAccountId = usdSource.Id, Direction = LedgerEntryDirection.Debit, Amount = 3.25m, Currency = "USD" },
            new LedgerEntry { LedgerTransactionId = usd.Id, LedgerAccountId = usdDestination.Id, Direction = LedgerEntryDirection.Credit, Amount = 3.25m, Currency = "USD" });
        f.Db.AddRange(new LedgerEntry { LedgerTransactionId = tx.Id, LedgerAccountId = wallet.LedgerAccount.Id, Direction = LedgerEntryDirection.Debit, Amount = 100 },
            new LedgerEntry { LedgerTransactionId = tx.Id, LedgerAccountId = wallet.LedgerAccount.Id, Direction = LedgerEntryDirection.Credit, Amount = 100 });
        f.Db.AddRange(new FinancialTransaction { OwnerUserId = user.Id, LedgerTransactionId = tx.Id, Amount = 100, Type = FinancialTransactionType.TransferIn },
            new FinancialTransaction { OwnerUserId = user.Id, LedgerTransactionId = tx.Id, Amount = 100, Type = FinancialTransactionType.TransferOut });
        var groupId = Guid.NewGuid(); f.Db.Add(new SpendWise.Domain.Groups.Group { Id = groupId, Name = "Legacy" });
        f.Db.Add(new V1Fund("V1", "SPW000001", V1FundKind.Free, user.Id, null) { Id = groupId });
        await f.Db.SaveChangesAsync();
        var service = new AdminA1Service(f.Db, new(f.Db), f.Clock, new(f.Db));
        var result = await service.SummaryAsync(V1PermissionTests.Principal(actor.Id, (await f.AddSessionAsync(actor.Id)).Id, "Admin"), null, default);
        Assert.Equal(3, result.PostingStatuses.Sum(x => x.Count)); Assert.Equal(1, result.GroupsCreated);
        Assert.Equal(100, Assert.Single(result.CompletedDebitVolumes, v => v.Currency == "VND").Amount);
        Assert.Equal(3.25m, Assert.Single(result.CompletedDebitVolumes, v => v.Currency == "USD").Amount);
        Assert.DoesNotContain(typeof(A1Summary).GetProperties(), p => p.Name.Contains("Revenue", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Tasks_filter_authoritative_deadline_and_never_return_another_operators_case()
    {
        var role = SystemRole.Admin;
        await using var f = await TargetedCorrectnessFixture.CreateAsync(); var actor = await f.AddUserAsync("Operator", role: role); var other = await f.AddUserAsync("Other", role: SystemRole.Admin); var user = await f.AddUserAsync("Subject");
        var own = new SupportCase { AssignedAdminId = role == SystemRole.Admin ? actor.Id : other.Id, EscalationReviewerId = role == SystemRole.SuperAdmin ? actor.Id : null,
            EscalatedAt = role == SystemRole.SuperAdmin ? f.Clock.UtcNow : null, LinkedUserId = user.Id, TicketNumber = "OWN", DueAt = f.Clock.UtcNow.AddMinutes(-1) };
        var foreign = new SupportCase { AssignedAdminId = other.Id, LinkedUserId = user.Id, TicketNumber = "OTHER" };
        f.Db.AddRange(own, foreign); await f.Db.SaveChangesAsync(); var service = new AdminA1Service(f.Db, new(f.Db), f.Clock, new(f.Db));
        var principal = V1PermissionTests.Principal(actor.Id, (await f.AddSessionAsync(actor.Id)).Id, role.ToString());
        var page = await service.TasksAsync(principal, new(1, 1, "New", Due: "Overdue"), default);
        Assert.Equal(own.Id, Assert.Single(page.Items).Id); Assert.Equal(1, page.Total); Assert.NotNull(page.Items[0].CaseEndpoint);
        Assert.Empty((await service.TasksAsync(principal, new(2, 1), default)).Items);
        Assert.Empty((await service.TasksAsync(principal, new(CaseId: foreign.Id), default)).Items);
        Assert.Contains(await f.Db.AuditLogs.ToListAsync(), a => a.TargetId == own.Id.ToString() && a.Action == "A1_CASE_READ");
        await f.Db.Users.Where(u => u.Id == actor.Id).ExecuteUpdateAsync(s => s.SetProperty(u => u.SystemRole, SystemRole.User));
        await Assert.ThrowsAsync<ForbiddenException>(() => service.TasksAsync(principal, new(), default));
    }

    [Fact]
    public async Task Alerts_and_activity_use_scoped_authoritative_sources_deduplicate_and_strip_untrusted_text()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync(); var actor = await f.AddUserAsync("Operator", role: SystemRole.Admin); var user = await f.AddUserAsync("Subject"); var other = await f.AddUserAsync("Unrelated");
        var ticket = new SupportCase { LinkedUserId = user.Id, AssignedAdminId = actor.Id, TicketNumber = "A1-1", Category = "V1FundGovernance" };
        f.Db.AddRange(ticket, new SupportCase { LinkedUserId = user.Id, AssignedAdminId = actor.Id, TicketNumber = "A1-2" },
            new RiskCase { UserId = user.Id, Signals = "private-untrusted-text" }, new RiskCase { UserId = other.Id, Signals = "private-untrusted-text" },
            new Notification { UserId = actor.Id, Type = NotificationType.SecurityAlert, Message = "private-untrusted-text", DataJson = "private-untrusted-text" },
            new Notification { UserId = other.Id, Type = NotificationType.SecurityAlert },
            new V1MaintenancePlan { CreatedByUserId = actor.Id, StartsAt = f.Clock.UtcNow.AddHours(1), ExpectedEndsAt = f.Clock.UtcNow.AddHours(2), Reason = "private-untrusted-text" });
        f.Db.Add(new CaseHistory { CaseId = ticket.Id, CaseKind = CaseKind.Support, ActorId = actor.Id, ActorRole = "Admin", Action = "V1_escalate", Message = "private-untrusted-text" });
        f.Db.AddRange(new AuditLog { ActorUserId = actor.Id, Action = "V1_CASE_ESCALATE", TargetType = "SupportCase", TargetId = ticket.Id.ToString(), Reason = "private-untrusted-text" },
            new AuditLog { ActorUserId = actor.Id, Action = "UNTRUSTED_SUCCESS", TargetType = "User", TargetId = user.Id.ToString(), Reason = "private-untrusted-text" });
        await f.Db.SaveChangesAsync(); var service = new AdminA1Service(f.Db, new(f.Db), f.Clock, new(f.Db)); var principal = V1PermissionTests.Principal(actor.Id, (await f.AddSessionAsync(actor.Id)).Id, "Admin");
        var alerts = await service.AlertsAsync(principal, new(), default); Assert.Single(alerts.Items, x => x.Source == "RiskCase");
        Assert.Single(alerts.Items, x => x.Source == "Notification"); Assert.Single(alerts.Items, x => x.Source == "Maintenance");
        Assert.DoesNotContain("private-untrusted-text", JsonSerializer.Serialize(alerts)); Assert.All(alerts.Items, x => Assert.False(x.IsFraudConclusion));
        var activity = await service.ActivityAsync(principal, new(), default); Assert.Single(activity.Items); Assert.Equal("CaseHistory", activity.Items[0].Source);
        Assert.DoesNotContain("private-untrusted-text", JsonSerializer.Serialize(activity));
        var sessionId = Guid.Parse(principal.FindFirst("sid")!.Value); await f.Db.UserSessions.Where(s => s.Id == sessionId).ExecuteUpdateAsync(s => s.SetProperty(s => s.RevokedAt, f.Clock.UtcNow));
        await Assert.ThrowsAsync<ForbiddenException>(() => service.AlertsAsync(principal, new(), default));
    }
}
