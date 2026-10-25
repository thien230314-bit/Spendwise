using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Banking;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.BankConnections;
using SpendWise.Application.Features.BankIntegration.Commands.ProcessLifecycleEvent;
using SpendWise.Application.Features.V1Groups;
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

    [Fact]
    public async Task ReconcileWalletWithdrawal_SubstringMatchOnly_WithoutValidJsonTxId_ThrowsConflictException()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var user = await f.AddUserAsync("WdUserSubstring");
        var wallet = await f.AddWalletAsync(user.Id, "WdWalletSub", true, balance: 100000);
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
            "wd-hold:sub",
            "wd-hold:sub",
            "Hold for withdrawal",
            CancellationToken.None);

        var provTxId = "tx_wanted_999";
        // Raw string has tx_wanted_999 inside comments, but the actual transaction_id property is different
        var deceptiveResponseData = $"{{\"transaction_id\":\"different_tx_000\",\"comment\":\"contains {provTxId} deceptively\",\"amount\":50000}}";

        var withdrawal = new WalletWithdrawal
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            BankConnectionId = bankConn.Id,
            WalletId = wallet.Id,
            Amount = 50000m,
            Currency = "VND",
            Status = WalletWithdrawalStatus.PendingReconciliation,
            Reference = "WD_TEST_SUB",
            IdempotencyKey = "wd_key_sub",
            ReservationLedgerTransactionId = holdPosting.LedgerTransactionId,
            ProviderResponseData = deceptiveResponseData,
            CreatedAt = f.Clock.UtcNow
        };
        f.Db.WalletWithdrawals.Add(withdrawal);
        await f.Db.SaveChangesAsync();

        var handler = new ReconcileWalletWithdrawalCommandHandler(f.Db, ledgerService, f.Clock);
        var cmd = new ReconcileWalletWithdrawalCommand(
            WithdrawalId: withdrawal.Id,
            Action: WithdrawalReconciliationAction.FinalizeSuccess,
            ProviderTransactionId: provTxId);

        // String Contains is no longer accepted: strict JSON schema must match transaction_id exactly
        var ex = await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(cmd, CancellationToken.None));
        Assert.Equal("unverified_provider_evidence", ex.Code);
    }

    [Fact]
    public async Task ReconcileWalletWithdrawal_ReleaseFailure_WithoutDefinitiveFailureProof_ThrowsConflictException()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var user = await f.AddUserAsync("WdUserReleaseFailNoProof");
        var wallet = await f.AddWalletAsync(user.Id, "WdWalletRel", true, balance: 100000);
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
            "wd-hold:rel-no-proof",
            "wd-hold:rel-no-proof",
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
            Reference = "WD_TEST_REL_NO_PROOF",
            IdempotencyKey = "wd_key_rel_no_proof",
            ReservationLedgerTransactionId = holdPosting.LedgerTransactionId,
            ProviderResponseData = "{\"isTimeoutOrUnknown\":true,\"providerStatus\":\"PENDING\"}",
            CreatedAt = f.Clock.UtcNow
        };
        f.Db.WalletWithdrawals.Add(withdrawal);
        await f.Db.SaveChangesAsync();

        var handler = new ReconcileWalletWithdrawalCommandHandler(f.Db, ledgerService, f.Clock);
        var cmd = new ReconcileWalletWithdrawalCommand(
            WithdrawalId: withdrawal.Id,
            Action: WithdrawalReconciliationAction.ReleaseFailure,
            Reason: "User claims provider failed but no official failure proof");

        var ex = await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(cmd, CancellationToken.None));
        Assert.Equal("unverified_provider_failure_evidence", ex.Code);

        // Status remains PendingReconciliation
        f.Db.ChangeTracker.Clear();
        var untouchedWd = await f.Db.WalletWithdrawals.SingleAsync(w => w.Id == withdrawal.Id);
        Assert.Equal(WalletWithdrawalStatus.PendingReconciliation, untouchedWd.Status);
    }

    [Fact]
    public async Task ReconcileWalletWithdrawal_ReleaseFailure_WithDefinitiveFailureProof_ReleasesHold()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var user = await f.AddUserAsync("WdUserReleaseProof");
        var wallet = await f.AddWalletAsync(user.Id, "WdWalletRelProof", true, balance: 100000);
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
            "wd-hold:rel-proof",
            "wd-hold:rel-proof",
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
            Reference = "WD_TEST_REL_PROOF",
            IdempotencyKey = "wd_key_rel_proof",
            ReservationLedgerTransactionId = holdPosting.LedgerTransactionId,
            ProviderResponseData = null,
            CreatedAt = f.Clock.UtcNow
        };
        f.Db.WalletWithdrawals.Add(withdrawal);
        await f.Db.SaveChangesAsync();

        var handler = new ReconcileWalletWithdrawalCommandHandler(f.Db, ledgerService, f.Clock);
        var cmd = new ReconcileWalletWithdrawalCommand(
            WithdrawalId: withdrawal.Id,
            Action: WithdrawalReconciliationAction.ReleaseFailure,
            Reason: "Account invalid confirmed by gateway",
            FailureEvidence: "{\"status\":\"REJECTED\",\"errorCode\":\"ACCOUNT_INVALID\"}");

        var res = await handler.Handle(cmd, CancellationToken.None);
        Assert.Equal("Failed", res.Status);

        f.Db.ChangeTracker.Clear();
        var freshWd = await f.Db.WalletWithdrawals.SingleAsync(w => w.Id == withdrawal.Id);
        Assert.Equal(WalletWithdrawalStatus.Failed, freshWd.Status);

        // Balance restored in wallet ledger
        var freshWalletLedger = await f.Db.LedgerAccounts.SingleAsync(a => a.Id == walletLedger.Id);
        Assert.Equal(100000m, freshWalletLedger.Balance);
    }

    [Fact]
    public async Task ProcessLifecycleEvent_WithInvalidSessionToken_QuarantinesWithInvalidSession()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var user = await f.AddUserAsync("SessionUser", linkActiveBank: false);
        var externalAccId = "acc_invalid_sess_" + Guid.NewGuid().ToString("N")[..8];

        var conn = new BankConnection
        {
            UserId = user.Id,
            Provider = BankProvider.SePayBankHub,
            AccountEnvironment = SePayAccountEnvironment.TestMode,
            ExternalAccountId = externalAccId,
            Status = BankConnectionStatus.Active,
            LinkedAt = f.Clock.UtcNow.AddDays(-1)
        };
        f.Db.BankConnections.Add(conn);
        await f.Db.SaveChangesAsync();

        var handler = new ProcessLifecycleEventCommandHandler(f.Db, new NotificationService(f.Db), f.Clock, new DummyBankConfig());
        var metadata = JsonDocument.Parse($"{{\"bank_account_xid\":\"{externalAccId}\",\"link_token_xid\":\"non_existent_token_999\"}}").RootElement;
        var evtId = "evt_invalid_token_" + Guid.NewGuid().ToString("N")[..8];
        var evt = new SePayLifecycleEventRequest(
            DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            evtId,
            "1.0",
            "BANK_ACCOUNT_UNLINKED",
            metadata);

        var result = await handler.Handle(new ProcessLifecycleEventCommand(evt), CancellationToken.None);
        Assert.True(result.Success);

        // Connection must NOT be touched
        var freshConn = await f.Db.BankConnections.SingleAsync(c => c.Id == conn.Id);
        Assert.Equal(BankConnectionStatus.Active, freshConn.Status);

        var recordedEvent = await f.Db.BankIntegrationEvents.SingleAsync(e => e.ExternalEventId == evtId);
        Assert.Equal(BankIntegrationEventStatus.Quarantined, recordedEvent.ProcessingStatus);
        Assert.Equal("quarantined_invalid_session", recordedEvent.FailureCode);
    }

    [Fact]
    public async Task V1FundRequest_DisputeCompletedExpense_CorrelatesExactRequestId_WhenMultipleIdenticalAmountsExist()
    {
        await using var f = await TargetedCorrectnessFixture.CreateAsync();
        var owner = await f.AddUserAsync("OwnerP03");
        var member = await f.AddUserAsync("MemberP03");
        var merchant = await f.AddUserAsync("MerchantP03");
        await V1FundMembershipTests.MakeFriends(f, owner.Id, member.Id);
        var service = V1ElectionTests.Configured(f);
        var fund = await service.CreateAsync(owner.Id, new("FundP03", "Fixed", 400, TargetedCorrectnessFixture.SecurityPin), default);
        var inv = await service.InviteAsync(owner.Id, fund.Id, member.Id, default);
        await service.AcceptAsync(member.Id, inv.Id, default);
        var config = await service.ConfirmObligationsAsync(owner.Id, fund.Id, new(400, TargetedCorrectnessFixture.SecurityPin), default);

        var oWallet = await f.AddWalletAsync(owner.Id, "OWalletP03", true, balance: 1000);
        var mWallet = await f.AddWalletAsync(member.Id, "MWalletP03", true, balance: 1000);
        await f.AddWalletAsync(merchant.Id, "MerchWalletP03", true, balance: 0);

        await service.ContributeAsync(owner.Id, fund.Id, new(oWallet.Id, 200, "c-o-p03", TargetedCorrectnessFixture.SecurityPin, config.Revision), default);
        await service.ContributeAsync(member.Id, fund.Id, new(mWallet.Id, 200, "c-m-p03", TargetedCorrectnessFixture.SecurityPin, config.Revision), default);
        await service.SetSpendingPermissionAsync(owner.Id, fund.Id, member.Id, true, TargetedCorrectnessFixture.SecurityPin, default);

        // Create Expense 1 with 50 VND
        var exp1 = new V1FundRequestInput("Expense", 50, merchant.Id, "First expense 50 VND", "exp-p03-1", TargetedCorrectnessFixture.SecurityPin);
        var res1 = await service.CreateFundRequestAsync(member.Id, fund.Id, exp1, default);
        Assert.Equal("Completed", res1.State);

        // Create Expense 2 with identical amount (50 VND)
        var exp2 = new V1FundRequestInput("Expense", 50, merchant.Id, "Second expense 50 VND", "exp-p03-2", TargetedCorrectnessFixture.SecurityPin);
        var res2 = await service.CreateFundRequestAsync(member.Id, fund.Id, exp2, default);
        Assert.Equal("Completed", res2.State);
        Assert.NotEqual(res1.Id, res2.Id);

        // Find the distinct FinancialTransactions created for res1 and res2
        var finTx1 = await f.Db.FinancialTransactions.SingleAsync(ft => ft.RelatedBusinessId == res1.Id);
        var finTx2 = await f.Db.FinancialTransactions.SingleAsync(ft => ft.RelatedBusinessId == res2.Id);
        Assert.NotEqual(finTx1.LedgerTransactionId, finTx2.LedgerTransactionId);

        // Member reports ONLY Expense 2
        var reported = await service.ReportFundRequestAsync(owner.Id, res2.Id, "Expense 2 suspicious", default);
        Assert.Equal("Completed", reported.State);

        // Verify the SupportCase correlates exactly to res2 and finTx2's LedgerTransactionId
        var supportCase = await f.Db.SupportCases.SingleAsync(c => c.LinkedTransactionId == res2.Id);
        Assert.Equal(res2.Id, supportCase.LinkedTransactionId);
        Assert.Contains(res2.Id.ToString(), supportCase.Description);
        Assert.Contains(finTx2.LedgerTransactionId.ToString(), supportCase.Description);
        Assert.DoesNotContain(finTx1.LedgerTransactionId.ToString(), supportCase.Description);
    }
}
