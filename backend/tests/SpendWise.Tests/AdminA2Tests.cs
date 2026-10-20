using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Features.Administration;
using SpendWise.Application.Features.Admin;
using SpendWise.Application.Features.Authentication;
using SpendWise.Domain.Admin;
using SpendWise.Domain.Identity;
using SpendWise.Infrastructure.Identity;
using Xunit;

namespace SpendWise.Tests;

public sealed class AdminA2Tests
{
    [Fact]
    public async Task Session_states_use_live_refresh_evidence_and_reject_ignored_or_unbounded_filters()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync(); var actor = await f.AddUserAsync("Operator", role: SystemRole.Admin); var user = await f.AddUserAsync("Subject");
        var p = V1PermissionTests.Principal(actor.Id, (await f.AddSessionAsync(actor.Id)).Id, "Admin");
        var active = await f.AddSessionAsync(user.Id); var old = await f.AddSessionAsync(user.Id); var revoked = await f.AddSessionAsync(user.Id, f.Clock.UtcNow);
        await f.AddRefreshTokenAsync(user.Id, "synthetic-live", userSessionId: active.Id);
        await f.AddRefreshTokenAsync(user.Id, "synthetic-expired", userSessionId: old.Id, expiresAt: f.Clock.UtcNow.AddDays(-1));
        var ticket = new SupportCase { AssignedAdminId = actor.Id, LinkedUserId = user.Id }; f.Db.Add(ticket); await f.Db.SaveChangesAsync(); var service = Service(f);
        foreach (var (id, state) in new[] { (active.Id, "RefreshEligible"), (old.Id, "NoLiveRefreshEvidence"), (revoked.Id, "Revoked") })
        { var row = Assert.Single((await service.SessionsAsync(p, ticket.Id, user.Id, new("Read device state", Status: state), default)).Items); Assert.Equal(id, row.Id); Assert.Equal(state, row.State); }
        await Assert.ThrowsAsync<AppException>(() => service.SessionsAsync(p, ticket.Id, user.Id, new("Invalid device filter", Status: "Online"), default));
        await Assert.ThrowsAsync<AppException>(() => service.HistoryAsync(p, ticket.Id, user.Id, new("Invalid pagination", Page: int.MaxValue, PageSize: 100), default));
        await Assert.ThrowsAsync<AppException>(() => service.ProfileAsync(p, ticket.Id, user.Id, " ", default));
        Assert.True(await f.Db.AuditLogs.AnyAsync(a => a.Action == "A2_ACCESS_DENIED" && a.MetadataJson!.Contains("InvalidReason")));
        var json = JsonSerializer.Serialize(await service.SessionsAsync(p, ticket.Id, user.Id, new("Minimum disclosure"), default));
        Assert.DoesNotContain("synthetic-live", json); Assert.DoesNotContain("IpAddress", json); Assert.DoesNotContain("UserAgent", json);
        await f.Db.Users.Where(u => u.Id == actor.Id).ExecuteUpdateAsync(s => s.SetProperty(u => u.SystemRole, SystemRole.User));
        await Assert.ThrowsAsync<ForbiddenException>(() => service.ProfileAsync(p, ticket.Id, user.Id, "Downgraded operator", default));
    }

    [Fact]
    public async Task Audit_failure_rolls_back_session_and_refresh_revocation()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync(); var actor = await f.AddUserAsync("Operator", role: SystemRole.Admin); var user = await f.AddUserAsync("Subject");
        var p = V1PermissionTests.Principal(actor.Id, (await f.AddSessionAsync(actor.Id)).Id, "Admin"); var target = await f.AddSessionAsync(user.Id);
        await f.AddRefreshTokenAsync(user.Id, "synthetic-rollback", userSessionId: target.Id);
        var ticket = new SupportCase { AssignedAdminId = actor.Id, LinkedUserId = user.Id }; f.Db.Add(ticket); await f.Db.SaveChangesAsync();
        await using var db = new SpendWise.Infrastructure.Persistence.SpendWiseDbContext(new DbContextOptionsBuilder<SpendWise.Infrastructure.Persistence.SpendWiseDbContext>()
            .UseSqlite(f.Db.Database.GetDbConnection()).AddInterceptors(new FailDecisionAudit()).Options, f.Clock);
        var service = new AdminA2Service(db, new(db), new(db, new(db), new(db), new SpendWise.Infrastructure.Notifications.NotificationService(db), f.Clock), new(db),
            new(db, new SecurityPinService(), new AccountCredentialLockout(db, f.Clock)), new(db, null!, f.Clock), f.Clock);
        var failure = await Assert.ThrowsAsync<DbUpdateException>(() => service.RevokeAsync(p, ticket.Id, user.Id, new("Rollback proof", TargetedCorrectnessFixture.SecurityPin, target.Id), default));
        Assert.Equal("Synthetic audit failure", Assert.IsType<InvalidOperationException>(failure.InnerException).Message);
        Assert.Null(await f.Db.UserSessions.Where(s => s.Id == target.Id).Select(s => s.RevokedAt).SingleAsync());
        Assert.Null(await f.Db.RefreshTokens.Where(r => r.UserSessionId == target.Id).Select(r => r.RevokedAt).SingleAsync());
        Assert.False(await f.Db.AuditLogs.AnyAsync(a => a.Action == "A2_SESSION_REVOKED"));
    }

    private sealed class FailDecisionAudit : Microsoft.EntityFrameworkCore.Diagnostics.DbCommandInterceptor
    {
        public override ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<System.Data.Common.DbDataReader>> ReaderExecutingAsync(
            System.Data.Common.DbCommand command, Microsoft.EntityFrameworkCore.Diagnostics.CommandEventData eventData,
            Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<System.Data.Common.DbDataReader> result, CancellationToken ct = default)
        {
            if (command.Parameters.Cast<System.Data.Common.DbParameter>().Any(p => p.Value is string value && value == "A2_SESSION_REVOKED")) throw new InvalidOperationException("Synthetic audit failure");
            return base.ReaderExecutingAsync(command, eventData, result, ct);
        }
    }

    [Fact]
    public async Task Group_and_friend_views_use_only_subject_relations_and_retained_history()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync(); var actor = await f.AddUserAsync("Operator", role: SystemRole.Admin);
        var user = await f.AddUserAsync("Subject"); var friend = await f.AddUserAsync("Friend"); var stranger = await f.AddUserAsync("Stranger");
        var ticket = new SupportCase { AssignedAdminId = actor.Id, LinkedUserId = user.Id };
        var legacy = new SpendWise.Domain.Groups.Group { Name = "Legacy current" }; var past = new SpendWise.Domain.Groups.Group { Name = "Legacy past" };
        var v1 = new SpendWise.Domain.V1Groups.V1Fund("Historical V1", "SPW000001", SpendWise.Domain.V1Groups.V1FundKind.Free, user.Id, null);
        var request = new SpendWise.Domain.Social.FriendRequest { RequesterUserId = user.Id, AddresseeUserId = friend.Id, LowerUserId = user.Id, HigherUserId = friend.Id, Status = SpendWise.Domain.Social.FriendRequestStatus.Accepted };
        f.Db.AddRange(ticket, legacy, past, v1, request,
            new SpendWise.Domain.Groups.GroupMember { GroupId = legacy.Id, UserId = user.Id, JoinedAt = f.Clock.UtcNow },
            new SpendWise.Domain.Groups.GroupInvite { GroupId = past.Id, InvitedUserId = user.Id, InvitedByUserId = actor.Id, Status = SpendWise.Domain.Groups.InviteStatus.Accepted, Token = "synthetic" },
            new SpendWise.Domain.V1Groups.V1FundMembership { FundId = v1.Id, UserId = user.Id, Seat = 1, JoinedAt = f.Clock.UtcNow.AddDays(-1), LeftAt = f.Clock.UtcNow },
            new SpendWise.Domain.Social.Friendship { LowerUserId = user.Id, HigherUserId = friend.Id, AcceptedRequestId = request.Id, AcceptedAt = f.Clock.UtcNow });
        await f.Db.SaveChangesAsync(); var p = V1PermissionTests.Principal(actor.Id, (await f.AddSessionAsync(actor.Id)).Id, "Admin"); var service = Service(f);
        var groups = await service.GroupsAsync(p, ticket.Id, user.Id, new("Trace membership"), default); Assert.Equal(3, groups.Total);
        Assert.Equal(2, (await service.GroupsAsync(p, ticket.Id, user.Id, new("Trace past", Status: "Historical"), default)).Total);
        Assert.Null(Assert.Single(groups.Items, g => g.Id == past.Id).JoinedAt);
        Assert.Equal(friend.Id, Assert.Single((await service.FriendsAsync(p, ticket.Id, user.Id, new("Verify relation"), default)).Items).UserId);
        Assert.DoesNotContain(stranger.Id.ToString(), JsonSerializer.Serialize(groups));
        await f.Db.SupportCases.Where(c => c.Id == ticket.Id).ExecuteUpdateAsync(s => s.SetProperty(c => c.AssignedAdminId, (Guid?)null));
        await Assert.ThrowsAsync<ForbiddenException>(() => service.FriendsAsync(p, ticket.Id, user.Id, new("Reassigned case"), default));
    }

    [Fact]
    public async Task Timeline_and_restrictions_are_real_scoped_paginated_and_do_not_invent_money_controls()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync(); var actor = await f.AddUserAsync("Operator", role: SystemRole.Admin);
        var user = await f.AddUserAsync("Subject", UserStatus.Locked); var other = await f.AddUserAsync("Other");
        var ticket = new SupportCase { AssignedAdminId = actor.Id, LinkedUserId = user.Id };
        var wallet = await f.AddWalletAsync(user.Id, "Frozen legacy", true, SpendWise.Domain.Finance.WalletStatus.Frozen);
        f.Db.AddRange(ticket, new CaseHistory { CaseId = ticket.Id, CaseKind = CaseKind.Support, ActorId = actor.Id, ActorRole = "Admin", Action = "V1_status", Message = "Verified report", NewStatus = "WaitingForUser" },
            new AuditLog { ActorUserId = actor.Id, Action = "LOCK_USER", TargetType = "User", TargetId = user.Id.ToString(), Reason = "Verified compromise" },
            new AuditLog { ActorUserId = actor.Id, Action = "V1_CASE_STATUS", TargetType = "SupportCase", TargetId = ticket.Id.ToString(), Reason = "Duplicate companion" },
            new AuditLog { ActorUserId = actor.Id, Action = "LOCK_USER", TargetType = "User", TargetId = other.Id.ToString(), Reason = "private-unrelated" },
            new AccountRecoveryRequest { ResolvedUserId = user.Id, ReviewedByAdminId = actor.Id, Status = AccountRecoveryStatus.MoreInfoNeeded, AdminNote = "private-recovery-note" },
            new AuditLog { ActorUserId = actor.Id, Action = "FREEZE_WALLET", TargetType = "Wallet", TargetId = wallet.Id.ToString(), Reason = "Legacy wallet decision" });
        await f.Db.SaveChangesAsync(); var p = V1PermissionTests.Principal(actor.Id, (await f.AddSessionAsync(actor.Id)).Id, "Admin"); var service = Service(f);
        var history = await service.HistoryAsync(p, ticket.Id, user.Id, new("Trace decisions"), default); Assert.Equal(4, history.Total);
        Assert.Equal(4, history.Items.Select(i => (i.Source, i.Id)).Distinct().Count());
        var page1 = await service.HistoryAsync(p, ticket.Id, user.Id, new("Read page", PageSize: 1), default);
        var page2 = await service.HistoryAsync(p, ticket.Id, user.Id, new("Read page", Page: 2, PageSize: 1), default); Assert.NotEqual(page1.Items[0].Id, page2.Items[0].Id);
        var json = JsonSerializer.Serialize(history); Assert.DoesNotContain("private-unrelated", json); Assert.DoesNotContain("private-recovery-note", json); Assert.DoesNotContain("Duplicate companion", json);
        var state = await service.RestrictionsAsync(p, ticket.Id, user.Id, "Inspect restrictions", default);
        Assert.Equal("Locked", state.LoginStatus); Assert.True(state.EmergencyOutgoingControlAvailable); Assert.Contains("Level 2", state.OutgoingControlStatus);
        Assert.Empty(await f.Db.LedgerTransactions.ToListAsync());
    }

    [Theory]
    [InlineData(SystemRole.Admin)] [InlineData(SystemRole.SuperAdmin)] [InlineData(SystemRole.SystemAdmin)]
    public async Task No_case_can_authorize_peer_account_and_revoked_actor_cannot_read(SystemRole targetRole)
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync(); var actor = await f.AddUserAsync("Operator", role: SystemRole.Admin);
        var target = await f.AddUserAsync("Protected peer", role: targetRole); var session = await f.AddSessionAsync(actor.Id);
        var ticket = new SupportCase { AssignedAdminId = actor.Id, LinkedUserId = target.Id }; f.Db.Add(ticket); await f.Db.SaveChangesAsync();
        var p = V1PermissionTests.Principal(actor.Id, session.Id, "Admin"); var service = Service(f);
        await Assert.ThrowsAsync<ForbiddenException>(() => service.ProfileAsync(p, ticket.Id, target.Id, "Peer attempt", default));
        await Assert.ThrowsAsync<ForbiddenException>(() => service.RevokeAsync(p, ticket.Id, target.Id, new("Peer attempt", TargetedCorrectnessFixture.SecurityPin), default));
        await f.Db.UserSessions.Where(s => s.Id == session.Id).ExecuteUpdateAsync(s => s.SetProperty(s => s.RevokedAt, (DateTime?)f.Clock.UtcNow));
        await Assert.ThrowsAsync<ForbiddenException>(() => service.SearchAsync(p, new("Revoked actor"), default));
    }

    internal static AdminA2Service Service(TargetedCorrectnessFixture f) => new(f.Db, new(f.Db),
        new(f.Db, new(f.Db), new(f.Db), new SpendWise.Infrastructure.Notifications.NotificationService(f.Db), f.Clock),
        new(f.Db), new(f.Db, new SecurityPinService(), new AccountCredentialLockout(f.Db, f.Clock)),
        new(f.Db, null!, f.Clock), f.Clock);

    [Fact]
    public async Task Directory_is_masked_exact_search_filtered_stable_and_excludes_operator_accounts()
    {
        var role = SystemRole.Admin;
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var actor = await f.AddUserAsync("Operator", role: role); var a = await f.AddUserAsync("A"); var b = await f.AddUserAsync("B", UserStatus.Locked);
        var p = V1PermissionTests.Principal(actor.Id, (await f.AddSessionAsync(actor.Id)).Id, role.ToString()); var service = Service(f);
        var all = await service.SearchAsync(p, new("Investigate directory", PageSize: 1), default);
        Assert.Equal(2, all.Total); Assert.Single(all.Items); Assert.NotEqual(actor.Id, all.Items[0].Id);
        var next = await service.SearchAsync(p, new("Investigate directory", Page: 2, PageSize: 1), default); Assert.NotEqual(all.Items[0].Id, next.Items[0].Id);
        Assert.Equal(b.Id, Assert.Single((await service.SearchAsync(p, new("Investigate directory", Status: "Locked"), default)).Items).Id);
        foreach (var search in new[] { a.Id.ToString(), a.Email, a.SpendId })
            Assert.Equal(a.Id, Assert.Single((await service.SearchAsync(p, new("Find account", Search: search), default)).Items).Id);
        Assert.Empty((await service.SearchAsync(p, new("Find missing", Search: "missing@test.local"), default)).Items);
        Assert.DoesNotContain(a.Email, JsonSerializer.Serialize(await service.SearchAsync(p, new("Read minimum fields"), default)));
        await Assert.ThrowsAsync<AppException>(() => service.SearchAsync(p, new("Read", Status: "Admin"), default));
    }

    [Fact]
    public async Task Profile_wallet_disclosure_requires_exact_live_case_subject_and_audits_denial()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync(); var actor = await f.AddUserAsync("Operator", role: SystemRole.Admin);
        var subject = await f.AddUserAsync("Subject"); var other = await f.AddUserAsync("Other");
        var wallet = await f.AddWalletAsync(subject.Id, "Main", true, balance: 123); var session = await f.AddSessionAsync(actor.Id);
        var ticket = new SupportCase { AssignedAdminId = actor.Id, LinkedUserId = subject.Id }; f.Db.Add(ticket); await f.Db.SaveChangesAsync();
        var p = V1PermissionTests.Principal(actor.Id, session.Id, "Admin"); var service = Service(f);
        var result = await service.ProfileAsync(p, ticket.Id, subject.Id, "Investigate wallet", default);
        Assert.Equal(wallet.Id, Assert.Single(result.Wallets).Id); Assert.Equal(123, result.Wallets[0].AvailableBalance);
        Assert.DoesNotContain(subject.Email, JsonSerializer.Serialize(result));
        await Assert.ThrowsAsync<ForbiddenException>(() => service.ProfileAsync(p, ticket.Id, other.Id, "Cross user attempt", default));
        await f.Db.SupportCases.Where(c => c.Id == ticket.Id).ExecuteUpdateAsync(s => s.SetProperty(c => c.Status, CaseStatus.Closed));
        await Assert.ThrowsAsync<ForbiddenException>(() => service.ProfileAsync(p, ticket.Id, subject.Id, "Closed scope", default));
        Assert.True(await f.Db.AuditLogs.AnyAsync(a => a.Action == "A2_ACCESS_DENIED"));
        Assert.Equal(123, await f.Db.Wallets.Where(w => w.Id == wallet.Id).Select(w => w.AvailableBalance).SingleAsync());
    }

    [Fact]
    public async Task Targeted_revocation_is_PIN_scoped_transactional_and_retry_does_not_duplicate_audit()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync(); var actor = await f.AddUserAsync("Operator", role: SystemRole.Admin);
        var subject = await f.AddUserAsync("Subject"); var other = await f.AddUserAsync("Other"); var mine = await f.AddSessionAsync(actor.Id);
        var one = await f.AddSessionAsync(subject.Id); var two = await f.AddSessionAsync(subject.Id); var foreign = await f.AddSessionAsync(other.Id);
        await f.AddRefreshTokenAsync(subject.Id, "synthetic-refresh", userSessionId: one.Id);
        var ticket = new SupportCase { AssignedAdminId = actor.Id, LinkedUserId = subject.Id }; f.Db.Add(ticket); await f.Db.SaveChangesAsync();
        var p = V1PermissionTests.Principal(actor.Id, mine.Id, "Admin"); var service = Service(f);
        await Assert.ThrowsAsync<ForbiddenException>(() => service.RevokeAsync(p, ticket.Id, subject.Id, new("Lost device", "654321", one.Id), default));
        await Assert.ThrowsAsync<NotFoundException>(() => service.RevokeAsync(p, ticket.Id, subject.Id, new("Wrong device", TargetedCorrectnessFixture.SecurityPin, foreign.Id), default));
        var request = new A2RevokeRequest("Verified lost device", TargetedCorrectnessFixture.SecurityPin, one.Id);
        await service.RevokeAsync(p, ticket.Id, subject.Id, request, default); await service.RevokeAsync(p, ticket.Id, subject.Id, request, default);
        Assert.NotNull(await f.Db.UserSessions.Where(s => s.Id == one.Id).Select(s => s.RevokedAt).SingleAsync());
        Assert.Null(await f.Db.UserSessions.Where(s => s.Id == two.Id).Select(s => s.RevokedAt).SingleAsync());
        Assert.NotNull(await f.Db.RefreshTokens.Where(r => r.UserSessionId == one.Id).Select(r => r.RevokedAt).SingleAsync());
        Assert.Equal(1, await f.Db.AuditLogs.CountAsync(a => a.Action == "A2_SESSION_REVOKED"));
        await service.RevokeAsync(p, ticket.Id, subject.Id, request with { SessionId = null }, default);
        var states = await service.SessionsAsync(p, ticket.Id, subject.Id, new("Verify revocation"), default);
        Assert.All(states.Items, s => Assert.Equal("Revoked", s.State));
        Assert.Equal(subject.PasswordHash, await f.Db.Users.Where(u => u.Id == subject.Id).Select(u => u.PasswordHash).SingleAsync());
    }
}
