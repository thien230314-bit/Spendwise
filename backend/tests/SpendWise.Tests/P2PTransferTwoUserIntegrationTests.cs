using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpendWise.API.Controllers;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features;
using SpendWise.Application.Features.Authentication;
using SpendWise.Application.Features.Transfers;
using SpendWise.Application.Features.Transfers.Commands.P2PTransfer;
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

public sealed class P2PTransferTwoUserIntegrationTests
{
    private const string Pin = "123456";

    private sealed class StubCurrentUser(Guid? userId, string? sessionId = null) : ICurrentUser
    {
        public Guid? UserId { get; } = userId;
        public string? SessionId { get; } = sessionId ?? Guid.NewGuid().ToString("N");
        public string SystemRole { get; } = "User";
        public string Locale { get; } = "vi-VN";
    }

    [Fact]
    public async Task TwoUser_P2PTransfer_200k_FullFlow_Success_And_Replay_And_LedgerBalanced()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();

        // 1. Arrange two users: A (Sender) and B (Recipient)
        var userA = await f.AddUserAsync("UserA_Sender", role: SystemRole.User);
        var userB = await f.AddUserAsync("UserB_Recipient", role: SystemRole.User);

        // Security PIN setup for User A (123456)
        userA.SecurityPinHash = new SecurityPinService().Hash(Pin);
        await f.Db.SaveChangesAsync();

        // Ensure FeatureFlag P2PTransfer is enabled
        var flag = await f.Db.FeatureFlags.FirstOrDefaultAsync(x => x.Name == "P2PTransfer");
        if (flag is null)
        {
            f.Db.FeatureFlags.Add(new Domain.Admin.FeatureFlag { Name = "P2PTransfer", IsEnabled = true, Description = "P2P" });
        }
        else
        {
            flag.IsEnabled = true;
        }

        // Create Main Wallets for User A (500,000 VND) and User B (50,000 VND)
        var walletA = await f.AddWalletAsync(userA.Id, "Ví Chính A", isDefault: true, balance: 500_000m, type: WalletType.Main);
        var walletB = await f.AddWalletAsync(userB.Id, "Ví Chính B", isDefault: true, balance: 50_000m, type: WalletType.Main);

        var handler = CreateHandler(f.Db, f.Clock);

        var idempotencyKey = $"p2p-test-{Guid.NewGuid():N}";
        var request = new P2PTransferRequest(
            SourceWalletId: walletA.Id,
            RecipientUserId: userB.Id,
            Amount: 200_000m,
            Description: "A chuyển cho B 200.000đ",
            IdempotencyKey: idempotencyKey,
            SecurityPin: Pin
        );

        // 2. Act: Transfer 200,000 VND from User A to User B
        var transferDto = await handler.Handle(new P2PTransferCommand(userA.Id, request), default);

        Assert.NotNull(transferDto);
        Assert.False(transferDto.Replayed);
        Assert.Equal(200_000m, transferDto.Amount);
        Assert.Equal(userA.Id, transferDto.SenderUserId);
        Assert.Equal(userB.Id, transferDto.RecipientUserId);
        Assert.Equal("Completed", transferDto.Status);

        // 3. Assert balances
        var updatedWalletA = await f.Db.Wallets.AsNoTracking().SingleAsync(w => w.Id == walletA.Id);
        var updatedWalletB = await f.Db.Wallets.AsNoTracking().SingleAsync(w => w.Id == walletB.Id);

        Assert.Equal(300_000m, updatedWalletA.AvailableBalance); // 500k - 200k = 300k
        Assert.Equal(250_000m, updatedWalletB.AvailableBalance); // 50k + 200k = 250k

        // 4. Assert Ledger balance & double-entry consistency
        var ledgerTx = await f.Db.LedgerTransactions
            .Include(t => t.Entries)
            .SingleAsync(t => t.Id == transferDto.LedgerTransactionId);

        Assert.Equal(2, ledgerTx.Entries.Count);
        var debitEntry = ledgerTx.Entries.Single(e => e.Direction == LedgerEntryDirection.Debit);
        var creditEntry = ledgerTx.Entries.Single(e => e.Direction == LedgerEntryDirection.Credit);

        Assert.Equal(walletA.LedgerAccount.Id, creditEntry.LedgerAccountId);
        Assert.Equal(walletB.LedgerAccount.Id, debitEntry.LedgerAccountId);
        Assert.Equal(200_000m, debitEntry.Amount);
        Assert.Equal(200_000m, creditEntry.Amount);

        // 5. Assert FinancialTransactions for both sides
        var txA = await f.Db.FinancialTransactions
            .SingleAsync(t => t.OwnerUserId == userA.Id && t.RelatedBusinessId == transferDto.Id);
        Assert.Equal(FinancialTransactionType.TransferOut, txA.Type);
        Assert.Equal(200_000m, txA.Amount);
        Assert.Equal(userB.FullName, txA.CounterpartyName);

        var txB = await f.Db.FinancialTransactions
            .SingleAsync(t => t.OwnerUserId == userB.Id && t.RelatedBusinessId == transferDto.Id);
        Assert.Equal(FinancialTransactionType.TransferIn, txB.Type);
        Assert.Equal(200_000m, txB.Amount);
        Assert.Equal(userA.FullName, txB.CounterpartyName);

        // 6. Assert Idempotency Replay (submitting identical request returns replay without double-spending)
        var replayDto = await handler.Handle(new P2PTransferCommand(userA.Id, request), default);

        Assert.True(replayDto.Replayed);
        Assert.Equal(transferDto.Id, replayDto.Id);

        var recheckedWalletA = await f.Db.Wallets.AsNoTracking().SingleAsync(w => w.Id == walletA.Id);
        var recheckedWalletB = await f.Db.Wallets.AsNoTracking().SingleAsync(w => w.Id == walletB.Id);
        Assert.Equal(300_000m, recheckedWalletA.AvailableBalance);
        Assert.Equal(250_000m, recheckedWalletB.AvailableBalance);

        // 7. Negative Test: Invalid PIN
        var badPinRequest = request with
        {
            IdempotencyKey = $"p2p-badpin-{Guid.NewGuid():N}",
            SecurityPin = "000000"
        };
        var exPin = await Assert.ThrowsAsync<AppException>(() =>
            handler.Handle(new P2PTransferCommand(userA.Id, badPinRequest), default));
        Assert.Equal("pin_verification_failed", exPin.Code);

        // 8. Negative Test: Insufficient balance
        var overspendRequest = request with
        {
            IdempotencyKey = $"p2p-overspend-{Guid.NewGuid():N}",
            Amount = 1_000_000m,
            SecurityPin = Pin
        };
        var exOverspend = await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(new P2PTransferCommand(userA.Id, overspendRequest), default));
        Assert.Equal("insufficient_balance", exOverspend.Code);

        // 9. Negative Test: Self-transfer
        var selfTransferRequest = request with
        {
            IdempotencyKey = $"p2p-self-{Guid.NewGuid():N}",
            RecipientUserId = userA.Id,
            SecurityPin = Pin
        };
        var exSelf = await Assert.ThrowsAsync<AppException>(() =>
            handler.Handle(new P2PTransferCommand(userA.Id, selfTransferRequest), default));
        Assert.Equal("self_transfer", exSelf.Code);
    }

    private static P2PTransferCommandHandler CreateHandler(
        SpendWiseDbContext db,
        IClock clock) => new(
        db,
        new LedgerService(db, clock),
        clock,
        new OperationPolicy(db),
        new FinancialSecurityGuard(db, new SecurityPinService(),
            new AccountCredentialLockout(db, clock)),
        new SpendWise.Application.Features.Banking.ActiveBankGuard(db));
}
