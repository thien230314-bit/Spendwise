using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Banking;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.BankConnections;
using SpendWise.Application.Features.BankIntegration.Commands.ProcessLifecycleEvent;
using SpendWise.Application.Finance;
using SpendWise.Domain.Banking;
using SpendWise.Domain.Finance;
using SpendWise.Infrastructure.Finance;
using SpendWise.Infrastructure.Notifications;
using Xunit;

namespace SpendWise.Tests;

public sealed class FinancialIntegrityPhase14Tests
{
    private sealed class DummyBankConfig : IBankHubConfiguration
    {
        public bool IsSandbox => true;
        public void EnsureAvailable() { }
        public string ResolveCompletionRedirectUri(string? requestedUri) => requestedUri ?? "";
        public string ResolveMobileCompletionRedirectUri(string? requestedUri) => requestedUri ?? "";
        public bool VerifyLifecycleSecret(string suppliedSecret) => true;
        public bool VerifyIpnAuthorization(string authorizationHeader) => true;
        public SePayBankHubStatusDto GetSafeStatus() => new(true, "Sandbox", true, true, true, true, null, null);
        public void MarkWebhookReceived(DateTime receivedAt) { }
    }

    [Fact]
    public async Task ProcessLifecycleEvent_SameUserMultipleEpisodes_WithoutSession_QuarantinesWithUnmatchedSessionEpisode()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var user = await f.AddUserAsync("MultiEpisodeUser", linkActiveBank: false);
        var wallet = await f.AddWalletAsync(user.Id, "Active Wallet", true, balance: 1000);

        var externalAccId = "acc_multi_episodes_" + Guid.NewGuid().ToString("N")[..8];

        // Episode 1: Unlinked in the past
        var connOld = new BankConnection
        {
            UserId = user.Id,
            Provider = BankProvider.SePayBankHub,
            AccountEnvironment = SePayAccountEnvironment.TestMode,
            ExternalAccountId = externalAccId,
            Status = BankConnectionStatus.Unlinked,
            UnlinkedAt = f.Clock.UtcNow.AddDays(-10)
        };
        // Episode 2: Currently Active
        var connNew = new BankConnection
        {
            UserId = user.Id,
            Provider = BankProvider.SePayBankHub,
            AccountEnvironment = SePayAccountEnvironment.TestMode,
            ExternalAccountId = externalAccId,
            Status = BankConnectionStatus.Active,
            LinkedAt = f.Clock.UtcNow.AddDays(-1)
        };
        f.Db.BankConnections.AddRange(connOld, connNew);
        await f.Db.SaveChangesAsync();

        var handler = new ProcessLifecycleEventCommandHandler(f.Db, new NotificationService(f.Db), f.Clock, new DummyBankConfig());
        var metadata = JsonDocument.Parse($"{{\"bank_account_xid\":\"{externalAccId}\"}}").RootElement;
        var evtId = "evt_delayed_same_user_unmatched_" + Guid.NewGuid().ToString("N")[..8];
        var evt = new SePayLifecycleEventRequest(
            DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            evtId,
            "1.0",
            "BANK_ACCOUNT_UNLINKED",
            metadata);

        var result = await handler.Handle(new ProcessLifecycleEventCommand(evt), CancellationToken.None);

        Assert.True(result.Success);

        // Active connection and wallet must NOT be touched!
        var freshConn = await f.Db.BankConnections.SingleAsync(c => c.Id == connNew.Id);
        Assert.Equal(BankConnectionStatus.Active, freshConn.Status);

        var freshWallet = await f.Db.Wallets.SingleAsync(w => w.Id == wallet.Id);
        Assert.Equal(WalletStatus.Active, freshWallet.Status);

        var recordedEvent = await f.Db.BankIntegrationEvents.SingleAsync(e => e.ExternalEventId == evtId);
        Assert.Equal(BankIntegrationEventStatus.Quarantined, recordedEvent.ProcessingStatus);
        Assert.Equal("quarantined_unmatched_session_episode", recordedEvent.FailureCode);
    }

    [Fact]
    public async Task ReconcileWalletWithdrawal_WithoutVerifiedProviderEvidence_ThrowsConflictException()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var user = await f.AddUserAsync("WdUser");
        var wallet = await f.AddWalletAsync(user.Id, "WdWallet", true, balance: 100000);

        var bankConn = await f.Db.BankConnections.FirstAsync(b => b.UserId == user.Id);

        var walletLedger = await f.Db.LedgerAccounts.SingleAsync(a => a.WalletId == wallet.Id);
        var holdAccount = await WalletWithdrawalHelpers.GetOrCreateReservationHoldAccountAsync(f.Db, CancellationToken.None);
        await WalletWithdrawalHelpers.GetOrCreateExternalSettlementAccountAsync(f.Db, CancellationToken.None);

        var ledgerService = new LedgerService(f.Db, f.Clock);
        var holdPosting = await ledgerService.PostAsync(
            walletLedger.Id,
            holdAccount.Id,
            50000m,
            "VND",
            "WithdrawalReservation",
            wallet.Id,
            "wd-hold:test",
            "wd-hold:test",
            "Hold for withdrawal",
            CancellationToken.None);

        var withdrawal = new WalletWithdrawal
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            BankConnectionId = bankConn.Id,
            WalletId = wallet.Id,
            Amount = 50000m,
            Currency = "VND",
            Status = WalletWithdrawalStatus.PendingReconciliation,
            Reference = "WD_TEST_P03",
            IdempotencyKey = "wd_key_p03",
            ReservationLedgerTransactionId = holdPosting.LedgerTransactionId,
            ProviderResponseData = null, // No provider response data!
            CreatedAt = f.Clock.UtcNow
        };
        f.Db.WalletWithdrawals.Add(withdrawal);
        await f.Db.SaveChangesAsync();

        var handler = new ReconcileWalletWithdrawalCommandHandler(f.Db, ledgerService, f.Clock);
        var cmd = new ReconcileWalletWithdrawalCommand(
            WithdrawalId: withdrawal.Id,
            Action: WithdrawalReconciliationAction.FinalizeSuccess,
            ProviderTransactionId: "arbitrary_fake_tx_999");

        // Attempting to finalize with an unverified provider tx id throws unverified_provider_evidence
        var ex = await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(cmd, CancellationToken.None));
        Assert.Equal("unverified_provider_evidence", ex.Code);
    }

    [Fact]
    public async Task ReconcileWalletWithdrawal_WithDirectProviderResponseEvidence_Succeeds()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var user = await f.AddUserAsync("WdUserDirect");
        var wallet = await f.AddWalletAsync(user.Id, "WdWalletDirect", true, balance: 100000);

        var bankConn = await f.Db.BankConnections.FirstAsync(b => b.UserId == user.Id);

        var walletLedger = await f.Db.LedgerAccounts.SingleAsync(a => a.WalletId == wallet.Id);
        var holdAccount = await WalletWithdrawalHelpers.GetOrCreateReservationHoldAccountAsync(f.Db, CancellationToken.None);
        await WalletWithdrawalHelpers.GetOrCreateExternalSettlementAccountAsync(f.Db, CancellationToken.None);

        var ledgerService = new LedgerService(f.Db, f.Clock);
        var holdPosting = await ledgerService.PostAsync(
            walletLedger.Id,
            holdAccount.Id,
            50000m,
            "VND",
            "WithdrawalReservation",
            wallet.Id,
            "wd-hold:direct",
            "wd-hold:direct",
            "Hold for withdrawal",
            CancellationToken.None);

        var provTxId = "prov_tx_verified_direct_101";
        var withdrawal = new WalletWithdrawal
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            BankConnectionId = bankConn.Id,
            WalletId = wallet.Id,
            Amount = 50000m,
            Currency = "VND",
            Status = WalletWithdrawalStatus.PendingReconciliation,
            Reference = "WD_TEST_DIRECT",
            IdempotencyKey = "wd_key_direct",
            ReservationLedgerTransactionId = holdPosting.LedgerTransactionId,
            ProviderResponseData = $"{{\"transaction_id\":\"{provTxId}\",\"status\":\"SUCCESS\"}}",
            CreatedAt = f.Clock.UtcNow
        };
        f.Db.WalletWithdrawals.Add(withdrawal);
        await f.Db.SaveChangesAsync();

        var handler = new ReconcileWalletWithdrawalCommandHandler(f.Db, ledgerService, f.Clock);
        var cmd = new ReconcileWalletWithdrawalCommand(
            WithdrawalId: withdrawal.Id,
            Action: WithdrawalReconciliationAction.FinalizeSuccess,
            ProviderTransactionId: provTxId);

        var res = await handler.Handle(cmd, CancellationToken.None);
        Assert.Equal("Completed", res.Status);

        f.Db.ChangeTracker.Clear();
        var freshWd = await f.Db.WalletWithdrawals.SingleAsync(w => w.Id == withdrawal.Id);
        Assert.Equal(WalletWithdrawalStatus.Completed, freshWd.Status);
        Assert.Equal(provTxId, freshWd.ProviderTransactionId);
    }

    [Fact]
    public async Task ReconcileWalletWithdrawal_WithProcessedBankIntegrationEventEvidence_Succeeds()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var user = await f.AddUserAsync("WdUserEvent");
        var wallet = await f.AddWalletAsync(user.Id, "WdWalletEvent", true, balance: 100000);

        var bankConn = await f.Db.BankConnections.FirstAsync(b => b.UserId == user.Id);

        var walletLedger = await f.Db.LedgerAccounts.SingleAsync(a => a.WalletId == wallet.Id);
        var holdAccount = await WalletWithdrawalHelpers.GetOrCreateReservationHoldAccountAsync(f.Db, CancellationToken.None);
        await WalletWithdrawalHelpers.GetOrCreateExternalSettlementAccountAsync(f.Db, CancellationToken.None);

        var ledgerService = new LedgerService(f.Db, f.Clock);
        var holdPosting = await ledgerService.PostAsync(
            walletLedger.Id,
            holdAccount.Id,
            50000m,
            "VND",
            "WithdrawalReservation",
            wallet.Id,
            "wd-hold:event",
            "wd-hold:event",
            "Hold for withdrawal",
            CancellationToken.None);

        var provTxId = "prov_tx_verified_event_202";
        var withdrawal = new WalletWithdrawal
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            BankConnectionId = bankConn.Id,
            WalletId = wallet.Id,
            Amount = 50000m,
            Currency = "VND",
            Status = WalletWithdrawalStatus.PendingReconciliation,
            Reference = "WD_TEST_EVENT",
            IdempotencyKey = "wd_key_event",
            ReservationLedgerTransactionId = holdPosting.LedgerTransactionId,
            ProviderResponseData = null, // Empty provider response
            CreatedAt = f.Clock.UtcNow
        };
        f.Db.WalletWithdrawals.Add(withdrawal);

        // Processed bank integration event exists
        f.Db.BankIntegrationEvents.Add(new BankIntegrationEvent
        {
            Provider = BankProvider.SePayBankHub,
            ExternalEventId = "evt_verified_202",
            ExternalTransactionId = provTxId,
            EventType = "OUTFLOW_CONFIRMED",
            ProcessingStatus = BankIntegrationEventStatus.Processed
        });
        await f.Db.SaveChangesAsync();

        var handler = new ReconcileWalletWithdrawalCommandHandler(f.Db, ledgerService, f.Clock);
        var cmd = new ReconcileWalletWithdrawalCommand(
            WithdrawalId: withdrawal.Id,
            Action: WithdrawalReconciliationAction.FinalizeSuccess,
            ProviderTransactionId: provTxId);

        var res = await handler.Handle(cmd, CancellationToken.None);
        Assert.Equal("Completed", res.Status);

        f.Db.ChangeTracker.Clear();
        var freshWd = await f.Db.WalletWithdrawals.SingleAsync(w => w.Id == withdrawal.Id);
        Assert.Equal(WalletWithdrawalStatus.Completed, freshWd.Status);
        Assert.Equal(provTxId, freshWd.ProviderTransactionId);
    }
}
