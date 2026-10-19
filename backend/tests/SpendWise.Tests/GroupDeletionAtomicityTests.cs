using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features;
using SpendWise.Application.Features.Authentication;
using SpendWise.Application.Features.Groups;
using SpendWise.Application.Notifications;
using SpendWise.Domain.Finance;
using SpendWise.Domain.Groups;
using SpendWise.Domain.Identity;
using SpendWise.Infrastructure.Finance;
using SpendWise.Infrastructure.Notifications;
using SpendWise.Infrastructure.Persistence;
using SpendWise.Infrastructure.Identity;
using Xunit;

namespace SpendWise.Tests;

public sealed class GroupDeletionAtomicityTests
{
    [Theory]
    [InlineData(1, false)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(2, true)]
    public async Task Owner_only_deletion_rolls_back_every_write_when_notification_fails(
        int failingBatch, bool cancel)
    {
        await using var fixture = await Fixture.CreateAsync();
        using var cancellation = new CancellationTokenSource();
        await using (var db = fixture.CreateContext())
        {
            var notifications = new FailingNotifications(new NotificationService(db), failingBatch, cancel, cancellation);
            var handler = fixture.Handler(db, notifications);
            var exception = await Record.ExceptionAsync(() => handler.Handle(
                new CreateGroupDeletionRequestCommand(fixture.OwnerId, fixture.GroupId), cancellation.Token));
            if (cancel)
            {
                var canceled = Assert.IsType<OperationCanceledException>(exception);
                Assert.Equal(cancellation.Token, canceled.CancellationToken);
            }
            else
                Assert.IsType<InvalidOperationException>(exception);
        }

        // A fresh context sees persisted state, not entities left in the failing change tracker.
        await using var verify = fixture.CreateContext();
        Assert.Equal(GroupStatus.Active, (await verify.Groups.SingleAsync()).Status);
        Assert.Empty(await verify.GroupDeletionRequests.ToListAsync());
        Assert.Empty(await verify.GroupDeletionVotes.ToListAsync());
        Assert.Empty(await verify.GroupRefunds.ToListAsync());
        Assert.Empty(await verify.Notifications.ToListAsync());
        Assert.Empty(await verify.OutboxMessages.ToListAsync());
        Assert.Empty(await verify.AuditLogs.ToListAsync());
        Assert.Empty(await verify.LedgerTransactions.ToListAsync());
        Assert.Equal(0, (await verify.SharedFunds.SingleAsync()).Balance);
        Assert.Equal(0, (await verify.LedgerAccounts.SingleAsync()).Balance);
    }

    [Fact]
    public async Task Successful_owner_only_deletion_is_ready_but_requires_explicit_finalize()
    {
        await using var fixture = await Fixture.CreateAsync();
        await using (var db = fixture.CreateContext())
        {
            var result = await fixture.Handler(db, new NotificationService(db)).Handle(
                new CreateGroupDeletionRequestCommand(fixture.OwnerId, fixture.GroupId), default);
            Assert.Equal(GroupDeletionRequestStatus.Ready.ToString(), result.Status);
        }
        await using var verify = fixture.CreateContext();
        Assert.Equal(GroupStatus.DeletionPending, (await verify.Groups.SingleAsync()).Status);
        var request = await verify.GroupDeletionRequests.SingleAsync();
        Assert.Equal(GroupDeletionRequestStatus.Ready, request.Status);
        Assert.NotNull(request.ReadyAt);
        Assert.Null(request.CompletedAt);
        Assert.Equal(GroupDeletionVoteStatus.Approved, (await verify.GroupDeletionVotes.SingleAsync()).Status);
        var refund = await verify.GroupRefunds.SingleAsync();
        Assert.Equal(0, refund.RefundAmount);
        Assert.Equal(GroupRefundStatus.Completed, refund.Status);
        Assert.Single(await verify.Notifications.ToListAsync());
        Assert.Single(await verify.OutboxMessages.ToListAsync());
        Assert.Empty(await verify.LedgerTransactions.ToListAsync());
    }

    [Fact]
    public async Task Failed_deletion_can_be_retried_from_a_new_context_without_a_frozen_group()
    {
        await using var fixture = await Fixture.CreateAsync();
        using var cancellation = new CancellationTokenSource();
        await using (var db = fixture.CreateContext())
        {
            var notifications = new FailingNotifications(new NotificationService(db), 2, false, cancellation);
            await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Handler(db, notifications).Handle(
                new CreateGroupDeletionRequestCommand(fixture.OwnerId, fixture.GroupId), default));
        }
        await using (var retry = fixture.CreateContext())
        {
            var result = await fixture.Handler(retry, new NotificationService(retry)).Handle(
                new CreateGroupDeletionRequestCommand(fixture.OwnerId, fixture.GroupId), default);
            Assert.Equal(GroupDeletionRequestStatus.Ready.ToString(), result.Status);
        }
        await using var verify = fixture.CreateContext();
        Assert.Single(await verify.GroupDeletionRequests.ToListAsync());
        Assert.Single(await verify.GroupRefunds.ToListAsync());
        Assert.Single(await verify.Notifications.ToListAsync());
        Assert.Single(await verify.OutboxMessages.ToListAsync());
    }

    private sealed class FailingNotifications(
        NotificationService real, int failingBatch, bool cancel, CancellationTokenSource cancellation)
        : INotificationService
    {
        private int batches;

        public Task<NotificationDto> CreateAsync(Guid userId, string type, string title, string message,
            object? data, CancellationToken token) => real.CreateAsync(userId, type, title, message, data, token);

        public async Task<IReadOnlyList<NotificationDto>> CreateManyAsync(
            IReadOnlyCollection<CreateNotificationRequest> requests, CancellationToken token)
        {
            // Use the application's real persistence path before injecting the boundary failure.
            var result = await real.CreateManyAsync(requests, token);
            if (++batches == failingBatch)
            {
                if (cancel)
                {
                    cancellation.Cancel();
                    throw new OperationCanceledException(cancellation.Token);
                }
                throw new InvalidOperationException("Injected deletion notification failure.");
            }
            return result;
        }
    }

    private sealed class Fixture(SqliteConnection connection, DbContextOptions<SpendWiseDbContext> options)
        : IAsyncDisposable
    {
        public Guid OwnerId { get; private set; }
        public Guid GroupId { get; private set; }
        private readonly IClock clock = new FixedClock();
        public SpendWiseDbContext CreateContext() => new(options, clock);

        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<SpendWiseDbContext>().UseSqlite(connection).Options;
            var fixture = new Fixture(connection, options);
            await using var db = fixture.CreateContext();
            await db.Database.EnsureCreatedAsync();
            var unique = Guid.NewGuid().ToString("N");
            var owner = new User
            {
                FullName = "Deletion owner", Email = $"{unique}@test.local", NormalizedEmail = unique.ToUpperInvariant(),
                SpendId = unique, NormalizedSpendId = unique.ToUpperInvariant(), PasswordHash = "test-only",
                SystemRole = SystemRole.User, Status = UserStatus.Active
            };
            var account = new LedgerAccount
            {
                Code = $"GROUP-FUND:{unique}", Name = "Empty fund", Type = LedgerAccountType.SharedFund
            };
            var group = new Group
            {
                Name = "Owner only", Type = GroupType.Friends,
                Members = [new GroupMember { UserId = owner.Id, Role = GroupRole.Owner, JoinedAt = fixture.clock.UtcNow }],
                SharedFund = new SharedFund { LedgerAccount = account, LedgerAccountId = account.Id }
            };
            group.SharedFund.GroupId = group.Id;
            account.GroupId = group.Id;
            db.AddRange(owner, group);
            await db.SaveChangesAsync();
            fixture.OwnerId = owner.Id;
            fixture.GroupId = group.Id;
            return fixture;
        }

        public CreateGroupDeletionRequestCommandHandler Handler(SpendWiseDbContext db, INotificationService notifications)
        {
            var access = new GroupAccess(db);
            var security = new FinancialSecurityGuard(db, new SecurityPinService(), new AccountCredentialLockout(db, clock));
            var accounting = new WalletFundAccounting(db, access, new LedgerService(db, clock), security, clock);
            return new(db, access, clock, notifications, new GroupDeletionSupport(db, clock, notifications, accounting));
        }

        public ValueTask DisposeAsync() => connection.DisposeAsync();
    }

    private sealed class FixedClock : IClock
    {
        public DateTime UtcNow => new(2026, 10, 12, 0, 0, 0, DateTimeKind.Utc);
    }
}
