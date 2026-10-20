using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SpendWise.Application.Features.Operations;
using SpendWise.Application.Features.Administration;
using SpendWise.Application.Features.Admin;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Features;
using SpendWise.Application.Features.Authentication;
using SpendWise.Application.Features.V1Groups;
using SpendWise.Application.Features.Groups;
using SpendWise.Application.Features.Groups.Commands.CreateGroup;
using SpendWise.Domain.Groups;
using SpendWise.Domain.Identity;
using SpendWise.Domain.V1Groups;
using SpendWise.Infrastructure.Identity;
using SpendWise.Infrastructure.Persistence;
using SpendWise.Infrastructure.Services;
using Xunit;

namespace SpendWise.Tests;

public sealed partial class V1FundSqlServerTests : SqlTestFixtureScope
{
    [Fact]
    public async Task Admin_A1_SQL_projections_read_actual_sources_and_preserve_nullable_task_deadline()
    {
        var owned = await OwnAsync(Owned.CreateAsync()); var actor = SyntheticUser(); actor.SystemRole = SystemRole.Admin; var subject = SyntheticUser();
        var session = new UserSession { UserId = actor.Id, LastSeenAt = DateTime.UtcNow };
        var ticket = new SpendWise.Domain.Admin.SupportCase { AssignedAdminId = actor.Id, LinkedUserId = subject.Id, TicketNumber = "A1-SQL", DueAt = DateTime.UtcNow.AddMinutes(-1), Category = "V1FundGovernance" };
        await using var db = owned.Context();
        db.AddRange(actor, subject, session, ticket, new SpendWise.Domain.Admin.RiskCase { UserId = subject.Id },
            new SpendWise.Domain.Admin.CaseHistory { CaseKind = SpendWise.Domain.Admin.CaseKind.Support, CaseId = ticket.Id, ActorId = actor.Id, Action = "V1_escalate", ActorRole = "Admin" });
        await db.SaveChangesAsync(); var service = new AdminA1Service(db, new(db), new SystemClock(), new(db)); var principal = V1PermissionTests.Principal(actor.Id, session.Id, "Admin");
        Assert.Equal(1, (await service.SummaryAsync(principal, null, default)).TotalOrdinaryUsers);
        Assert.Equal("Overdue", Assert.Single((await service.TasksAsync(principal, new(), default)).Items).DeadlineState);
        Assert.Single((await service.AlertsAsync(principal, new(Source: "RiskCase"), default)).Items);
        Assert.Single((await service.ActivityAsync(principal, new(), default)).Items);
        foreach (var metric in new[] { "usersCreated", "postingStatuses", "completedDebitVolumes", "groupsCreated", "supportCasesCreated", "disputesCreated" })
            _ = await service.SourceRecordsAsync(principal, metric, null, new(), default);
        Assert.Empty(await db.LedgerTransactions.ToListAsync());
    }

    [Fact]
    public async Task Admin_A1_SQL_session_revocation_waits_until_authorized_read_finishes_then_denies_new_reads()
    {
        var owned = await OwnAsync(Owned.CreateAsync()); var actor = SyntheticUser(); actor.SystemRole = SystemRole.Admin;
        var session = new UserSession { UserId = actor.Id, LastSeenAt = DateTime.UtcNow };
        await using (var seed = owned.Context()) { seed.AddRange(actor, session); await seed.SaveChangesAsync(); }
        var gate = new AdmissionGate("[UserSessions]"); await using var reader = owned.Context(gate); await using var revoker = owned.Context();
        await revoker.Database.OpenConnectionAsync(); var sqlSession = ((SqlConnection)revoker.Database.GetDbConnection()).ServerProcessId;
        var service = new AdminA1Service(reader, new(reader), new SystemClock(), new(reader)); var principal = V1PermissionTests.Principal(actor.Id, session.Id, "Admin");
        var read = service.SummaryAsync(principal, null, default); Task<int>? revoke = null;
        try
        {
            await SqlTestFixtureLifecycle.WaitForGateAsync(gate.Entered.Task, read, TimeSpan.FromSeconds(10));
            revoke = revoker.UserSessions.Where(s => s.Id == session.Id).ExecuteUpdateAsync(s => s.SetProperty(s => s.RevokedAt, (DateTime?)DateTime.UtcNow));
            Assert.True(await owned.ObserveWaitAsync(sqlSession, revoke)); gate.Release.TrySetResult();
            await read.WaitAsync(TimeSpan.FromSeconds(10)); Assert.Equal(1, await revoke.WaitAsync(TimeSpan.FromSeconds(10)));
            await Assert.ThrowsAsync<ForbiddenException>(() => service.SummaryAsync(principal, null, default));
        }
        finally { gate.Release.TrySetResult(); await Record.ExceptionAsync(() => read); if (revoke is not null) await Record.ExceptionAsync(() => revoke); }
    }

    [Fact]
    public async Task Maintenance_financial_admission_is_shared_for_postings_exclusive_for_activation_and_transaction_owned()
    {
        var owned = await OwnAsync(Owned.CreateAsync()); await using var posting = owned.Context(); await using var maintenance = owned.Context();
        await using (var tx = await posting.Database.BeginTransactionAsync())
        {
            await posting.AcquireMaintenanceAdmissionAsync(false, default);
            await using var competing = await maintenance.Database.BeginTransactionAsync();
            var error = await Assert.ThrowsAsync<AppException>(() => maintenance.AcquireMaintenanceAdmissionAsync(true, default));
            Assert.Equal(409, error.StatusCode); await competing.RollbackAsync(); await tx.CommitAsync();
        }
        await using (var tx = await maintenance.Database.BeginTransactionAsync())
        {
            await maintenance.AcquireMaintenanceAdmissionAsync(true, default);
            await using var competing = await posting.Database.BeginTransactionAsync();
            var error = await Assert.ThrowsAsync<AppException>(() => posting.AcquireMaintenanceAdmissionAsync(false, default));
            Assert.Equal(503, error.StatusCode); await competing.RollbackAsync(); await tx.CommitAsync();
        }
        await posting.ExecuteInTransactionAsync(async ct => { await posting.AcquireMaintenanceAdmissionAsync(false, ct); return true; });
        await using var verify = owned.Context(); Assert.Empty(await verify.LedgerTransactions.ToListAsync());
    }
    [Fact]
    public async Task Concurrent_election_ballot_retries_wait_and_commit_one_ballot_without_money_changes()
    {
        var owned = await OwnAsync(Owned.CreateAsync()); var owner = SyntheticUser(); var voter = SyntheticUser();
        var fund = new V1Fund("Synthetic election", "SPW000001", V1FundKind.Free, owner.Id, null);
        var membership = new V1FundMembership { FundId = fund.Id, UserId = voter.Id, Seat = 2 };
        var policy = new V1ElectionPolicySnapshot("Synthetic", 60, 60, 60, 60, 60, false);
        var election = new V1Election { FundId = fund.Id, IncumbentUserId = owner.Id, OpenedByUserId = voter.Id,
            Phase = V1ElectionPhase.Nomination, PhaseStartedAt = DateTime.UtcNow, PolicyJson = System.Text.Json.JsonSerializer.Serialize(policy), InactivityEvidenceId = "Synthetic-only" };
        await using (var seed = owned.Context())
        {
            seed.AddRange(owner, voter, fund, membership, election,
                new V1ElectionVoter { ElectionId = election.Id, UserId = voter.Id, MembershipId = membership.Id },
                new V1ElectionCandidate { ElectionId = election.Id, UserId = voter.Id });
            await seed.SaveChangesAsync();
        }
        var gate = new AdmissionGate("[V1Funds]"); await using var firstDb = owned.Context(gate); await using var secondDb = owned.Context();
        await secondDb.Database.OpenConnectionAsync(); var session = ((SqlConnection)secondDb.Database.GetDbConnection()).ServerProcessId;
        var request = new V1BallotRequest(1, voter.Id, null);
        var first = Service(firstDb).CastBallotAsync(voter.Id, election.Id, request, default); Task<V1BallotReceipt>? second = null;
        try
        {
            await SqlTestFixtureLifecycle.WaitForGateAsync(gate.Entered.Task, first, TimeSpan.FromSeconds(10));
            second = Service(secondDb).CastBallotAsync(voter.Id, election.Id, request, default);
            Assert.True(await owned.ObserveWaitAsync(session, second)); gate.Release.TrySetResult();
            var a = await first.WaitAsync(TimeSpan.FromSeconds(10)); var b = await second.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(a.Id, b.Id); Assert.False(a.Replayed); Assert.True(b.Replayed);
            await using var verify = owned.Context(); Assert.Single(await verify.V1ElectionBallots.ToListAsync());
            Assert.Single(await verify.V1FundActivities.Where(x => x.Event == "ElectionBallotCast").ToListAsync());
            Assert.Equal(owner.Id, await verify.V1Funds.Select(x => x.OwnerUserId).SingleAsync()); Assert.Empty(await verify.LedgerTransactions.ToListAsync());
        }
        finally { gate.Release.TrySetResult(); await Record.ExceptionAsync(() => first); if (second is not null) await Record.ExceptionAsync(() => second); }
    }

    [Fact]
    public async Task Maintenance_close_serializes_with_operator_session_revocation()
    {
        var owned = await OwnAsync(Owned.CreateAsync()); var user = SyntheticUser(); user.SystemRole = SystemRole.Admin;
        user.SecurityPinHash = new SecurityPinService().Hash(IdentityTestData.DefaultPin);
        var session = new UserSession { UserId = user.Id, LastSeenAt = DateTime.UtcNow };
        var plan = new SpendWise.Domain.Admin.V1MaintenancePlan { CreatedByUserId = user.Id, StartsAt = DateTime.UtcNow.AddHours(-1), ExpectedEndsAt = DateTime.UtcNow.AddMinutes(-1), Reason = "Synthetic" };
        await using (var seed = owned.Context()) { seed.AddRange(user, session, plan); await seed.SaveChangesAsync(); }
        var safety = new ReopenGate(); var clock = new SystemClock();
        await using var firstDb = owned.Context(); await using var secondDb = owned.Context();
        var service = new V1MaintenanceService(firstDb, clock, new(firstDb), new AdminSecurityGuard(firstDb, new SecurityPinService(), new AccountCredentialLockout(firstDb, clock)), new(firstDb), safety);
        await secondDb.Database.OpenConnectionAsync(); var contenderSession = ((SqlConnection)secondDb.Database.GetDbConnection()).ServerProcessId;
        var first = service.CloseAsync(V1PermissionTests.Principal(user.Id, session.Id, "Admin"), plan.Id, new(IdentityTestData.DefaultPin, "Synthetic manual close"), default);
        Task<int>? revoke = null;
        try
        {
            await SqlTestFixtureLifecycle.WaitForGateAsync(safety.Entered.Task, first, TimeSpan.FromSeconds(10));
            revoke = secondDb.UserSessions.Where(s => s.Id == session.Id).ExecuteUpdateAsync(s => s.SetProperty(s => s.RevokedAt, (DateTime?)DateTime.UtcNow));
            Assert.True(await owned.ObserveWaitAsync(contenderSession, revoke)); safety.Release.TrySetResult();
            await first.WaitAsync(TimeSpan.FromSeconds(10)); Assert.Equal(1, await revoke.WaitAsync(TimeSpan.FromSeconds(10)));
            await using var verify = owned.Context(); Assert.NotNull(await verify.V1MaintenancePlans.Select(p => p.ClosedAt).SingleAsync());
            Assert.NotNull(await verify.UserSessions.Select(s => s.RevokedAt).SingleAsync());
        }
        finally { safety.Release.TrySetResult(); await Record.ExceptionAsync(() => first); if (revoke is not null) await Record.ExceptionAsync(() => revoke); }
    }

    private sealed class ReopenGate : IV1MaintenanceSafety
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<MaintenanceSafetyResult> CheckAsync(Guid id, CancellationToken ct)
        { Entered.TrySetResult(); await Release.Task.WaitAsync(TimeSpan.FromSeconds(15), ct); return new(true, "Synthetic-approved-proof"); }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Cross_fund_wallet_or_cross_wallet_key_races_cannot_spend_or_replay_twice(bool crossFund)
    {
        var owned = await OwnAsync(Owned.CreateAsync()); var user = SyntheticUser();
        user.SecurityPinHash = new SecurityPinService().Hash(IdentityTestData.DefaultPin);
        var firstFund = new V1Fund("First", "SPW000001", V1FundKind.Free, user.Id, null);
        var secondFund = new V1Fund("Second", "SPW000002", V1FundKind.Free, user.Id, null);
        var a = SpendWise.Application.Features.Wallets.WalletFactory.Create(user.Id, "Main", SpendWise.Domain.Finance.WalletType.Main, true);
        var b = SpendWise.Application.Features.Wallets.WalletFactory.Create(user.Id, "Extra", SpendWise.Domain.Finance.WalletType.Savings, false);
        a.AvailableBalance = a.LedgerAccount.Balance = b.AvailableBalance = b.LedgerAccount.Balance = 60;
        await using (var seed = owned.Context())
        {
            seed.AddRange(user, firstFund, secondFund, a, b,
                new V1FundMembership { FundId = firstFund.Id, UserId = user.Id, Seat = 1 }, new V1FundMembership { FundId = secondFund.Id, UserId = user.Id, Seat = 1 });
            await seed.SaveChangesAsync();
        }
        var gate = new AdmissionGate("[V1Funds]"); await using var firstDb = owned.Context(gate); await using var secondDb = owned.Context();
        await secondDb.Database.OpenConnectionAsync(); var session = ((SqlConnection)secondDb.Database.GetDbConnection()).ServerProcessId;
        var first = Service(firstDb).ContributeAsync(user.Id, firstFund.Id, new(a.Id, 45, "first", IdentityTestData.DefaultPin), default);
        Task<V1ContributionDto>? second = null;
        try
        {
            await SqlTestFixtureLifecycle.WaitForGateAsync(gate.Entered.Task, first, TimeSpan.FromSeconds(10));
            second = Service(secondDb).ContributeAsync(user.Id, crossFund ? secondFund.Id : firstFund.Id,
                new(crossFund ? a.Id : b.Id, 45, crossFund ? "second" : "first", IdentityTestData.DefaultPin), default);
            Assert.True(await owned.ObserveWaitAsync(session, second)); gate.Release.TrySetResult(); await first.WaitAsync(TimeSpan.FromSeconds(10));
            var error = await Assert.ThrowsAsync<ConflictException>(() => second.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.Equal(crossFund ? "insufficient_balance" : "idempotency_conflict", error.Code);
            await using var verify = owned.Context(); Assert.Single(await verify.V1FundContributions.ToListAsync()); Assert.Single(await verify.LedgerTransactions.ToListAsync());
            Assert.Equal(15, await verify.Wallets.Where(w => w.Id == a.Id).Select(w => w.AvailableBalance).SingleAsync());
            Assert.Equal(60, await verify.Wallets.Where(w => w.Id == b.Id).Select(w => w.AvailableBalance).SingleAsync());
        }
        finally { gate.Release.TrySetResult(); await Record.ExceptionAsync(() => first); if (second is not null) await Record.ExceptionAsync(() => second); }
    }

    [Fact]
    public async Task Legacy_migration_chain_upgrades_to_V1_without_rewriting_synthetic_money_and_reapplies_safely()
    {
        var owned = await OwnAsync(Owned.CreateAsync(migrateLegacy: true));
        var user = SyntheticUser(); user.SecurityPinHash = new SecurityPinService().Hash(IdentityTestData.DefaultPin);
        var wallet = SpendWise.Application.Features.Wallets.WalletFactory.Create(user.Id, "Legacy synthetic", SpendWise.Domain.Finance.WalletType.Main, true);
        wallet.AvailableBalance = wallet.LedgerAccount.Balance = 100;
        await using (var legacy = owned.Context()) { legacy.AddRange(user, wallet); await legacy.SaveChangesAsync(); }
        await using (var upgrade = owned.Context())
        {
            await upgrade.GetService<IMigrator>().MigrateAsync();
            await upgrade.GetService<IMigrator>().MigrateAsync();
            Assert.Empty(await upgrade.Database.GetPendingMigrationsAsync());
            Assert.Equal(100, await upgrade.Wallets.Select(w => w.AvailableBalance).SingleAsync());
            Assert.Equal(user.PasswordHash, await upgrade.Users.Select(u => u.PasswordHash).SingleAsync());
            var service = Service(upgrade);
            var fund = await service.CreateAsync(user.Id, new("Upgraded fund", "Fixed", 50, IdentityTestData.DefaultPin), default);
            var revision = await service.ConfirmObligationsAsync(user.Id, fund.Id, new(50, IdentityTestData.DefaultPin), default);
            await service.ContributeAsync(user.Id, fund.Id, new(wallet.Id, 20, "upgrade-posting", IdentityTestData.DefaultPin, revision.Revision), default);
            Assert.Equal(80, await upgrade.Wallets.Select(w => w.AvailableBalance).SingleAsync());
            Assert.Equal(20, (await service.FinanceAsync(user.Id, fund.Id, default)).BalanceVnd);
        }
    }

    [Fact]
    public async Task Concurrent_same_contribution_intent_posts_one_balanced_ledger_and_replays()
    {
        var owned = await OwnAsync(Owned.CreateAsync()); var user = SyntheticUser();
        user.SecurityPinHash = new SecurityPinService().Hash(IdentityTestData.DefaultPin);
        var fund = new V1Fund("Synthetic Free", "SPW000001", V1FundKind.Free, user.Id, null);
        var wallet = SpendWise.Application.Features.Wallets.WalletFactory.Create(user.Id, "Synthetic wallet", SpendWise.Domain.Finance.WalletType.Main, true);
        wallet.AvailableBalance = wallet.LedgerAccount.Balance = 100;
        await using (var seed = owned.Context())
        {
            seed.AddRange(user, fund, wallet, new V1FundMembership { FundId = fund.Id, UserId = user.Id, Seat = 1 });
            await seed.SaveChangesAsync();
        }
        var gate = new AdmissionGate("[V1Funds]");
        await using var firstDb = owned.Context(gate); await using var secondDb = owned.Context();
        await secondDb.Database.OpenConnectionAsync(); var session = ((SqlConnection)secondDb.Database.GetDbConnection()).ServerProcessId;
        var request = new V1ContributeRequest(wallet.Id, 25, "synthetic-once", IdentityTestData.DefaultPin);
        var first = Service(firstDb).ContributeAsync(user.Id, fund.Id, request, default); Task<V1ContributionDto>? second = null;
        try
        {
            await SqlTestFixtureLifecycle.WaitForGateAsync(gate.Entered.Task, first, TimeSpan.FromSeconds(10));
            second = Service(secondDb).ContributeAsync(user.Id, fund.Id, request, default);
            Assert.True(await owned.ObserveWaitAsync(session, second)); gate.Release.TrySetResult();
            var a = await first.WaitAsync(TimeSpan.FromSeconds(10)); var b = await second.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(a.Id, b.Id); Assert.False(a.Replayed); Assert.True(b.Replayed);
            await using var verify = owned.Context();
            Assert.Single(await verify.V1FundContributions.ToListAsync()); Assert.Single(await verify.LedgerTransactions.ToListAsync());
            Assert.Equal(2, await verify.LedgerEntries.CountAsync());
            Assert.Equal(75, await verify.Wallets.Select(w => w.AvailableBalance).SingleAsync());
            Assert.Equal(25, (await Service(verify).FinanceAsync(user.Id, fund.Id, default)).BalanceVnd);
        }
        finally
        {
            gate.Release.TrySetResult(); await Record.ExceptionAsync(() => first);
            if (second is not null) await Record.ExceptionAsync(() => second);
        }
    }

    [Fact]
    public async Task Existing_member_mutation_waits_on_root_without_blocking_foreign_key_notifications()
    {
        var owned = await OwnAsync(Owned.CreateAsync());
        var users = Enumerable.Range(0, 3).Select(_ => SyntheticUser()).ToArray();
        users[0].SecurityPinHash = new SecurityPinService().Hash(IdentityTestData.DefaultPin);
        var fund = new V1Fund("Synthetic Fixed", "SPW000001", V1FundKind.Fixed, users[0].Id, 100);
        await using (var seed = owned.Context())
        {
            seed.AddRange(users); seed.Add(fund);
            for (var i = 0; i < 2; i++) seed.Add(new V1FundMembership { FundId = fund.Id, UserId = users[i].Id, Seat = i + 1 });
            var lower = users[1].Id.CompareTo(users[2].Id) < 0 ? users[1].Id : users[2].Id;
            var higher = lower == users[1].Id ? users[2].Id : users[1].Id;
            var request = new SpendWise.Domain.Social.FriendRequest { RequesterUserId = users[1].Id, AddresseeUserId = users[2].Id,
                LowerUserId = lower, HigherUserId = higher, Status = SpendWise.Domain.Social.FriendRequestStatus.Accepted };
            seed.Add(new SpendWise.Domain.Social.Friendship { LowerUserId = lower, HigherUserId = higher, AcceptedRequest = request });
            await seed.SaveChangesAsync();
        }
        var gate = new AdmissionGate("[V1Funds]");
        await using var firstDb = owned.Context(gate); await using var secondDb = owned.Context();
        await secondDb.Database.OpenConnectionAsync();
        var session = ((SqlConnection)secondDb.Database.GetDbConnection()).ServerProcessId;
        var first = Service(firstDb).SetSpendingPermissionAsync(users[0].Id, fund.Id, users[1].Id, true, IdentityTestData.DefaultPin, default);
        Task<V1InvitationDto>? second = null;
        try
        {
            await SqlTestFixtureLifecycle.WaitForGateAsync(gate.Entered.Task, first, TimeSpan.FromSeconds(10));
            second = Service(secondDb).InviteAsync(users[1].Id, fund.Id, users[2].Id, default);
            Assert.True(await owned.ObserveWaitAsync(session, second));
            gate.Release.TrySetResult();
            await first.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal("Proposed", (await second.WaitAsync(TimeSpan.FromSeconds(10))).Status);
            await using var verify = owned.Context();
            Assert.True(await verify.V1FundMemberships.Where(m => m.UserId == users[1].Id).Select(m => m.CanSpend).SingleAsync());
            Assert.Equal(2, await verify.Notifications.CountAsync());
            Assert.Equal(2, await verify.OutboxMessages.CountAsync());
            Assert.Empty(await verify.LedgerTransactions.ToListAsync());
        }
        finally
        {
            gate.Release.TrySetResult(); await Record.ExceptionAsync(() => first);
            if (second is not null) await Record.ExceptionAsync(() => second);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Legacy_and_V1_admissions_share_the_last_combined_slot(bool legacyFirst)
    {
        var owned = await OwnAsync(Owned.CreateAsync());
        var user = SyntheticUser();
        user.SecurityPinHash = new SecurityPinService().Hash(IdentityTestData.DefaultPin);
        await using (var seed = owned.Context())
        {
            seed.Users.Add(user);
            for (var i = 0; i < 9; i++) seed.Groups.Add(new Group { Name = "Synthetic Legacy", Members =
                [new GroupMember { UserId = user.Id, Role = GroupRole.Owner, JoinedAt = DateTime.UtcNow }] });
            await seed.SaveChangesAsync();
        }
        var gate = new AdmissionGate("[Users]");
        await using var firstDb = owned.Context(gate);
        await using var secondDb = owned.Context();
        await secondDb.Database.OpenConnectionAsync();
        var session = ((SqlConnection)secondDb.Database.GetDbConnection()).ServerProcessId;
        Task Admit(SpendWiseDbContext db, bool legacy) => legacy
            ? new CreateGroupCommandHandler(db, new SystemClock(), new GroupAccess(db),
                new FinancialSecurityGuard(db, new SecurityPinService(), new AccountCredentialLockout(db, new SystemClock())))
                .Handle(new(user.Id, new("Synthetic", null, "Trip", SecurityPin: IdentityTestData.DefaultPin)), default)
            : Service(db).CreateAsync(user.Id, new("Synthetic", "Free", null, IdentityTestData.DefaultPin), default);
        var first = Admit(firstDb, legacyFirst);
        Task? second = null;
        try
        {
            await SqlTestFixtureLifecycle.WaitForGateAsync(gate.Entered.Task, first, TimeSpan.FromSeconds(10));
            second = Admit(secondDb, !legacyFirst);
            Assert.True(await owned.ObserveWaitAsync(session, second), "Cross-version admission must actually wait on the same user lock.");
            gate.Release.TrySetResult();
            await first.WaitAsync(TimeSpan.FromSeconds(10));
            var error = await Assert.ThrowsAsync<ConflictException>(() => second.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.Equal("active_group_limit", error.Code);
            await using var verify = owned.Context();
            Assert.Equal(10, await verify.GroupMembers.CountAsync(m => m.UserId == user.Id)
                + await verify.V1FundMemberships.CountAsync(m => m.UserId == user.Id));
            Assert.Empty(await verify.LedgerTransactions.ToListAsync());
        }
        finally
        {
            gate.Release.TrySetResult();
            await Record.ExceptionAsync(() => first);
            if (second is not null) await Record.ExceptionAsync(() => second);
        }
    }

    [Fact]
    public async Task Overlapping_acceptances_serialize_at_root_and_cannot_overfill_Free_fund()
    {
        var owned = await OwnAsync(Owned.CreateAsync());
        Guid fundId, firstInvite, secondInvite, aId, bId;
        await using (var seed = owned.Context())
        {
            var users = Enumerable.Range(0, 7).Select(_ => SyntheticUser()).ToArray();
            seed.Users.AddRange(users);
            var fund = new V1Fund("Isolated SQL fund", "SPW000001", V1FundKind.Free, users[0].Id, null);
            seed.V1Funds.Add(fund);
            for (var i = 0; i < 5; i++) seed.V1FundMemberships.Add(new V1FundMembership
                { FundId = fund.Id, UserId = users[i].Id, Seat = i + 1, JoinedAt = DateTime.UtcNow });
            var a = new V1FundInvitation { FundId = fund.Id, InvitedByUserId = users[0].Id, InvitedUserId = users[5].Id, ExpiresAt = DateTime.UtcNow.AddDays(1) };
            var b = new V1FundInvitation { FundId = fund.Id, InvitedByUserId = users[0].Id, InvitedUserId = users[6].Id, ExpiresAt = DateTime.UtcNow.AddDays(1) };
            seed.AddRange(a, b);
            await seed.SaveChangesAsync();
            fundId = fund.Id; firstInvite = a.Id; secondInvite = b.Id; aId = users[5].Id; bId = users[6].Id;
        }
        var gate = new AdmissionGate("[V1Funds]");
        await using var firstDb = owned.Context(gate);
        await using var secondDb = owned.Context();
        await secondDb.Database.OpenConnectionAsync();
        var session = ((SqlConnection)secondDb.Database.GetDbConnection()).ServerProcessId;
        var first = Service(firstDb).AcceptAsync(aId, firstInvite, default);
        Task<V1FundDto>? second = null;
        try
        {
            await SqlTestFixtureLifecycle.WaitForGateAsync(gate.Entered.Task, first, TimeSpan.FromSeconds(10));
            second = Service(secondDb).AcceptAsync(bId, secondInvite, default);
            Assert.True(await owned.ObserveWaitAsync(session, second), "Contender must actually wait on SQL root admission.");
            gate.Release.TrySetResult();
            await first.WaitAsync(TimeSpan.FromSeconds(10));
            var error = await Assert.ThrowsAsync<ConflictException>(() => second.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.Equal("fund_member_limit", error.Code);
            await using var verify = owned.Context();
            Assert.Equal(6, await verify.V1FundMemberships.CountAsync(m => m.FundId == fundId && m.LeftAt == null));
            Assert.Equal(V1InvitationStatus.Pending, await verify.V1FundInvitations.Where(i => i.Id == secondInvite).Select(i => i.Status).SingleAsync());
            Assert.Empty(await verify.LedgerTransactions.ToListAsync());
        }
        finally
        {
            gate.Release.TrySetResult();
            await Record.ExceptionAsync(() => first);
            if (second is not null) await Record.ExceptionAsync(() => second);
        }
    }

    [Fact]
    public async Task Admin_A2_SQL_scoped_read_projections_are_translatable_and_do_not_change_money()
    {
        var owned = await OwnAsync(Owned.CreateAsync()); await using var db = owned.Context();
        var actor = SyntheticUser(); actor.SystemRole = SystemRole.Admin; var subject = SyntheticUser();
        var session = new UserSession { UserId = actor.Id, LastSeenAt = DateTime.UtcNow };
        var ticket = new SpendWise.Domain.Admin.SupportCase { AssignedAdminId = actor.Id, LinkedUserId = subject.Id, TicketNumber = "A2-SQL" };
        var fund = new V1Fund("Synthetic A2", "SPW000001", V1FundKind.Free, subject.Id, null);
        db.AddRange(actor, subject, session, ticket, fund,
            new V1FundMembership { FundId = fund.Id, UserId = subject.Id, Seat = 1, JoinedAt = DateTime.UtcNow },
            new SpendWise.Domain.Admin.CaseHistory { CaseId = ticket.Id, CaseKind = SpendWise.Domain.Admin.CaseKind.Support, ActorId = actor.Id, Action = "V1_reply", Message = "Synthetic response" });
        await db.SaveChangesAsync(); var p = V1PermissionTests.Principal(actor.Id, session.Id, "Admin"); var service = A2Service(db);
        Assert.Single((await service.SearchAsync(p, new("Read directory"), default)).Items);
        Assert.Equal(subject.Id, (await service.ProfileAsync(p, ticket.Id, subject.Id, "Read profile", default)).User.Id);
        Assert.Single((await service.GroupsAsync(p, ticket.Id, subject.Id, new("Read memberships"), default)).Items);
        Assert.Empty((await service.FriendsAsync(p, ticket.Id, subject.Id, new("Read related friends"), default)).Items);
        Assert.Empty((await service.SessionsAsync(p, ticket.Id, subject.Id, new("Read subject sessions"), default)).Items);
        Assert.Single((await service.HistoryAsync(p, ticket.Id, subject.Id, new("Read handling history"), default)).Items);
        Assert.False((await service.RestrictionsAsync(p, ticket.Id, subject.Id, "Read restrictions", default)).EmergencyOutgoingControlAvailable);
        Assert.Empty(await db.LedgerTransactions.ToListAsync());
    }

    [Fact]
    public async Task Admin_A2_SQL_overlapping_revocations_commit_one_effect_and_one_decision_audit()
    {
        var owned = await OwnAsync(Owned.CreateAsync()); var actor = SyntheticUser(); actor.SystemRole = SystemRole.Admin;
        actor.SecurityPinHash = new SecurityPinService().Hash(IdentityTestData.DefaultPin); var subject = SyntheticUser();
        var session = new UserSession { UserId = actor.Id, LastSeenAt = DateTime.UtcNow }; var target = new UserSession { UserId = subject.Id, LastSeenAt = DateTime.UtcNow };
        var ticket = new SpendWise.Domain.Admin.SupportCase { AssignedAdminId = actor.Id, LinkedUserId = subject.Id };
        await using (var seed = owned.Context())
        {
            seed.AddRange(actor, subject, session, target, ticket,
                new RefreshToken { UserId = subject.Id, UserSessionId = target.Id, TokenHash = "synthetic-revocation", ExpiresAt = DateTime.UtcNow.AddDays(1) });
            await seed.SaveChangesAsync();
        }
        var gate = new AdmissionGate("[SupportCases]"); await using var firstDb = owned.Context(gate); await using var secondDb = owned.Context();
        await secondDb.Database.OpenConnectionAsync(); var sqlSession = ((SqlConnection)secondDb.Database.GetDbConnection()).ServerProcessId;
        var p = V1PermissionTests.Principal(actor.Id, session.Id, "Admin"); var request = new A2RevokeRequest("Verified device loss", IdentityTestData.DefaultPin, target.Id);
        var first = A2Service(firstDb).RevokeAsync(p, ticket.Id, subject.Id, request, default); Task? second = null;
        try
        {
            await SqlTestFixtureLifecycle.WaitForGateAsync(gate.Entered.Task, first, TimeSpan.FromSeconds(10));
            second = A2Service(secondDb).RevokeAsync(p, ticket.Id, subject.Id, request, default);
            Assert.True(await owned.ObserveWaitAsync(sqlSession, second)); gate.Release.TrySetResult();
            await first.WaitAsync(TimeSpan.FromSeconds(10)); await second.WaitAsync(TimeSpan.FromSeconds(10));
            await using var verify = owned.Context();
            Assert.NotNull(await verify.UserSessions.Where(s => s.Id == target.Id).Select(s => s.RevokedAt).SingleAsync());
            Assert.NotNull(await verify.RefreshTokens.Where(r => r.UserSessionId == target.Id).Select(r => r.RevokedAt).SingleAsync());
            Assert.Equal(1, await verify.AuditLogs.CountAsync(a => a.Action == "A2_SESSION_REVOKED"));
            Assert.Equal("synthetic-only", await verify.Users.Where(u => u.Id == subject.Id).Select(u => u.PasswordHash).SingleAsync());
            Assert.Empty(await verify.LedgerTransactions.ToListAsync());
        }
        finally { gate.Release.TrySetResult(); await Record.ExceptionAsync(() => first); if (second is not null) await Record.ExceptionAsync(() => second); }
    }

    private static AdminA2Service A2Service(SpendWiseDbContext db)
    {
        var clock = new SystemClock();
        return new(db, new(db), new(db, new(db), new(db), new SpendWise.Infrastructure.Notifications.NotificationService(db), clock), new(db),
            new(db, new SecurityPinService(), new AccountCredentialLockout(db, clock)), new(db, null!, clock), clock, new SpendWise.Infrastructure.Notifications.NotificationService(db));
    }

    private static User SyntheticUser()
    {
        var id = Guid.NewGuid().ToString("N");
        return new User { FullName = "Synthetic", Email = id + "@test.local", NormalizedEmail = id.ToUpperInvariant() + "@TEST.LOCAL",
            SpendId = id, NormalizedSpendId = id.ToUpperInvariant(), PasswordHash = "synthetic-only" };
    }

    private static V1FundService Service(SpendWiseDbContext db)
    {
        var clock = new SystemClock();
        return new(db, clock, new FinancialSecurityGuard(db, new SecurityPinService(), new AccountCredentialLockout(db, clock)),
            new SpendWise.Infrastructure.Notifications.NotificationService(db), new SpendWise.Infrastructure.Finance.LedgerService(db, clock));
    }

    private sealed class AdmissionGate(string table) : DbCommandInterceptor
    {
        private int used;
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            DbDataReader result, CancellationToken ct = default)
        {
            if (table == "[Users]" && command.Transaction is not null && command.CommandText.Contains("WITH (UPDLOCK, ROWLOCK)", StringComparison.Ordinal)
                && Interlocked.Exchange(ref used, 1) == 0)
            {
                Entered.TrySetResult();
                await Release.Task.WaitAsync(TimeSpan.FromSeconds(15), ct);
            }
            return result;
        }
        public override async ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData, int result, CancellationToken ct = default)
        {
            if (command.Transaction is not null && command.CommandText.TrimStart().StartsWith("UPDATE", StringComparison.Ordinal)
                && command.CommandText.Contains(table, StringComparison.Ordinal) && Interlocked.Exchange(ref used, 1) == 0)
            {
                Entered.TrySetResult();
                await Release.Task.WaitAsync(TimeSpan.FromSeconds(15), ct);
            }
            return result;
        }
    }

    // Reuse the existing guarded financial-test catalog family; never read application config.
    private sealed class Owned : IAsyncDisposable
    {
        private readonly string connection = SqlTestFixtureLifecycle.CreateOwnedConnectionString(
            "SpendWise_FinancialS05_Test_" + Guid.NewGuid().ToString("N"),
            Environment.GetEnvironmentVariable(SqlTestFixtureLifecycle.InstanceEnvironmentVariable));
        private bool owns;
        public SpendWiseDbContext Context(params IInterceptor[] interceptors) => new(
            new DbContextOptionsBuilder<SpendWiseDbContext>().UseSqlServer(connection).AddInterceptors(interceptors).Options, new SystemClock());
        public static async Task<Owned> CreateAsync(bool migrateLegacy = false)
        {
            var owned = new Owned();
            await SqlTestFixtureLifecycle.CreateOwnedCatalogAsync(owned.connection);
            owned.owns = true;
            return await SqlTestFixtureLifecycle.CreateAsync(async () =>
            {
                await using var db = owned.Context();
                if (migrateLegacy) await db.GetService<IMigrator>().MigrateAsync("20260930032346_AddMobilePushNotifications");
                else await db.Database.EnsureCreatedAsync();
                await SqlTestFixtureLifecycle.ConfigureOwnedCatalogAsync(owned.connection);
                return owned;
            }, owned.DisposeAsync);
        }
        public async Task<bool> ObserveWaitAsync(int session, Task contender)
        {
            await SqlTestFixtureLifecycle.VerifyCatalogOwnerAsync(connection);
            await using var observer = new SqlConnection(connection);
            await observer.OpenAsync();
            await using var command = observer.CreateCommand();
            command.CommandTimeout = 2;
            command.CommandText = "SELECT COUNT(*) FROM sys.dm_tran_locks WHERE request_session_id=@session AND resource_database_id=DB_ID() AND request_status IN ('WAIT','LOW_PRIORITY_WAIT')";
            command.Parameters.AddWithValue("@session", session);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            while (!contender.IsCompleted && watch.Elapsed < TimeSpan.FromSeconds(5))
            {
                if (Convert.ToInt32(await command.ExecuteScalarAsync()) > 0) return true;
                await Task.Delay(10);
            }
            return false;
        }
        public async ValueTask DisposeAsync()
        {
            if (!owns) throw new InvalidOperationException("Test catalog ownership was not established.");
            await SqlTestFixtureLifecycle.VerifyCatalogOwnerAsync(connection);
            await using var db = Context();
            await db.Database.EnsureDeletedAsync();
            owns = false;
        }
    }
}
