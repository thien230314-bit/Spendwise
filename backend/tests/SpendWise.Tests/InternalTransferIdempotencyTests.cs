using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Features;
using SpendWise.Application.Features.Authentication;
using SpendWise.Application.Features.Transfers.Commands.TransferBetweenWallets;
using SpendWise.Application.Features.Wallets;
using SpendWise.Application.Finance;
using SpendWise.Domain.Finance;
using SpendWise.Domain.Identity;
using SpendWise.Infrastructure.Admin;
using SpendWise.Infrastructure.Finance;
using SpendWise.Infrastructure.Identity;
using SpendWise.Infrastructure.Persistence;
using SpendWise.Infrastructure.Services;
using Xunit;

namespace SpendWise.Tests;

public sealed class InternalTransferIdempotencyTests
{
    private const string Pin = "123456";

    [Fact]
    public async Task Concurrent_same_key_internal_transfers_return_one_posting_and_different_payload_conflicts()
    {
        // Arrange: interceptor dừng hai INSERT sau fast lookup rồi mô phỏng unique-index loser.
        var databaseName = Guid.NewGuid().ToString("N");
        var root = new InMemoryDatabaseRoot();
        var coordinator = new DuplicateTransferRaceInterceptor();
        var options = new DbContextOptionsBuilder<SpendWiseDbContext>()
            .UseInMemoryDatabase(databaseName, root)
            .AddInterceptors(coordinator)
            .Options;
        var clock = new SystemClock();
        Guid userId;
        Guid sourceWalletId;
        Guid destinationWalletId;
        await using (var seed = new SpendWiseDbContext(options, clock))
        {
            var user = new User
            {
                FullName = "Transfer race user",
                Email = $"{Guid.NewGuid():N}@test.local",
                NormalizedEmail = Guid.NewGuid().ToString("N"),
                SpendId = Guid.NewGuid().ToString("N"),
                NormalizedSpendId = Guid.NewGuid().ToString("N"),
                PasswordHash = "test",
                SecurityPinHash = new SecurityPinService().Hash(Pin)
            };
            var source = WalletFactory.Create(user.Id, "Main", WalletType.Main, true);
            source.AvailableBalance = 1_000_000;
            source.LedgerAccount.Balance = 1_000_000;
            var destination = WalletFactory.Create(user.Id, "Savings", WalletType.Savings, false);
            var bank = new SpendWise.Domain.Banking.BankConnection
            {
                UserId = user.Id,
                Provider = SpendWise.Domain.Banking.BankProvider.SePayBankHub,
                AccountEnvironment = SpendWise.Domain.Banking.SePayAccountEnvironment.TestMode,
                ExternalAccountId = $"ACC_{user.Id:N}",
                BankBrandName = "MBBank",
                AccountHolderName = user.FullName,
                MaskedAccountNumber = "****1234",
                Status = SpendWise.Domain.Banking.BankConnectionStatus.Active,
                LinkedAt = DateTime.UtcNow
            };
            seed.AddRange(user, source, destination, bank);
            await seed.SaveChangesAsync();
            userId = user.Id;
            sourceWalletId = source.Id;
            destinationWalletId = destination.Id;
        }

        await using var firstDb = new SpendWiseDbContext(options, clock);
        await using var secondDb = new SpendWiseDbContext(options, clock);
        var firstHandler = CreateHandler(firstDb, clock);
        var secondHandler = CreateHandler(secondDb, clock);
        var request = new InternalTransferRequest(
            sourceWalletId,
            destinationWalletId,
            100_000,
            "Savings",
            "same-key",
            Pin);

        // Act: cả hai request đi qua lookup trước khi một INSERT thắng unique constraint.
        var results = await Task.WhenAll(
            firstHandler.Handle(new(userId, request), default),
            secondHandler.Handle(new(userId, request), default));

        // Assert: loser phục hồi transfer đã hoàn tất thay vì làm rò DbUpdateException/500.
        Assert.Equal(results[0].Id, results[1].Id);
        Assert.Single(results, result => !result.Replayed);
        Assert.Single(results, result => result.Replayed);
        await using var verify = new SpendWiseDbContext(options, clock);
        Assert.Equal(1, await verify.Transfers.CountAsync());
        Assert.Equal(1, await verify.LedgerTransactions.CountAsync());
        Assert.Equal(1, await verify.FinancialTransactions.CountAsync());
        Assert.Equal(900_000, (await verify.Wallets.FindAsync(sourceWalletId))!.AvailableBalance);
        Assert.Equal(100_000, (await verify.Wallets.FindAsync(destinationWalletId))!.AvailableBalance);

        // Cùng key nhưng payload khác vẫn dùng hành vi Conflict hiện có.
        var conflict = await Assert.ThrowsAsync<ConflictException>(() => CreateHandler(verify, clock).Handle(
            new(userId, request with { Amount = 120_000 }), default));
        Assert.Equal("idempotency_conflict", conflict.Code);
    }

    private static TransferBetweenWalletsCommandHandler CreateHandler(
        SpendWiseDbContext db,
        SystemClock clock) => new(
        db,
        new LedgerService(db, clock),
        clock,
        new OperationPolicy(db),
        new FinancialSecurityGuard(db, new SecurityPinService(),
            new AccountCredentialLockout(db, clock)),
        new SpendWise.Application.Features.Banking.ActiveBankGuard(db));

    private sealed class DuplicateTransferRaceInterceptor : SaveChangesInterceptor
    {
        private readonly TaskCompletionSource bothInsertsReached = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource winnerCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int insertCount;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            var db = eventData.Context!;
            var hasAddedInternalTransfer = db.ChangeTracker.Entries<Transfer>().Any(entry =>
                entry.State == EntityState.Added && entry.Entity.IdempotencyScope == "internal");
            if (!hasAddedInternalTransfer)
            {
                return result;
            }

            var position = Interlocked.Increment(ref insertCount);
            if (position == 1)
            {
                await bothInsertsReached.Task.WaitAsync(cancellationToken);
                return result;
            }

            bothInsertsReached.TrySetResult();
            await winnerCompleted.Task.WaitAsync(cancellationToken);
            throw new DbUpdateException(
                "Simulated unique constraint race for transfer idempotency.",
                new InvalidOperationException("duplicate key"));
        }

        public override ValueTask<int> SavedChangesAsync(
            SaveChangesCompletedEventData eventData,
            int result,
            CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<Transfer>().Any(entry =>
                    entry.Entity.IdempotencyScope == "internal"
                    && entry.Entity.Status == TransferStatus.Completed))
            {
                winnerCompleted.TrySetResult();
            }

            return ValueTask.FromResult(result);
        }
    }
}
