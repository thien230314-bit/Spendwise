# BÁO CÁO KẾT QUẢ KIỂM THỬ THỰC TẾ QUỸ NHÓM (FIXED FUND V2 & FREE FUND V1)

> **Ngày thực thi:** 02/11/2026  
> **Lệnh kiểm thử:** `dotnet test backend/tests/SpendWise.Tests/SpendWise.Tests.csproj --filter "FullyQualifiedName~FixedFundV2|FullyQualifiedName~FreeFund|FullyQualifiedName~BankLinkingAndWalletGuardTests|FullyQualifiedName~FinancialIntegrityPhaseTests" --no-build -c Debug --logger "trx;LogFileName=GROUP-FUNDS-TEST-RESULTS.trx"`  
> **File TRX gốc:** `backend/tests/SpendWise.Tests/TestResults/GROUP-FUNDS-TEST-RESULTS.trx` (và bản sao `reports/GROUP-FUNDS-TEST-RESULTS.trx`)  
> **File Log chi tiết:** `reports/GROUP-FUNDS-TEST-RESULTS.log`  

---

## 1. TỔNG KẾT KẾT QUẢ THI HÀNH

```text
Test run for E:\source\SpendWise-final-merged\backend\tests\SpendWise.Tests\bin\Debug\net10.0\SpendWise.Tests.dll (.NETCoreApp,Version=v10.0)
A total of 1 test files matched the specified pattern.
Results File: E:\source\SpendWise-final-merged\backend\tests\SpendWise.Tests\TestResults\GROUP-FUNDS-TEST-RESULTS.trx

Passed!  - Failed: 0, Passed: 204, Skipped: 0, Total: 204, Duration: 1 m 53 s - SpendWise.Tests.dll (net10.0)
```

- **Tổng số tests thi hành:** 204
- **Passed:** 204 (100%)
- **Failed:** 0
- **Skipped:** 0
- **Thời lượng chạy:** 1 phút 53 giây

---

## 2. PHÂN BỔ CÁC SUITE KIỂM THỬ ĐÃ THỰC THI (204 TESTS)

### A. FixedFundV2BusinessRuleTests (30 tests) — 100% Passed
1. `F01_CannotSetCanSpend_BeforeObligationsConfirmed`
2. `F02_CannotSetCanSpend_WhenMemberHasZeroContribution`
3. `F03_CannotSetCanSpend_WhenMemberHasPartialContribution`
4. `F04_CanSetCanSpend_WhenMemberHas100PercentContribution`
5. `F05_FixedFund_HasNoNegativeBalanceOrOverspendingConcept`
6. `F06_GroupFundingIncomplete_BlocksSpendingEvenWithCanSpendTrue`
7. `F07_StoredCanSpendPersists_WhileEffectiveCanSpendIsFalse_WhenGroupIncomplete`
8. `F08_GroupFunding100Percent_EnablesEffectiveCanSpend`
9. `F09_MemberWithStoredCanSpendFalse_CannotSpendEvenWhenGroup100PercentFunded`
10. `F10_BalanceChangesDoNotMutateStoredCanSpend`
11. `F11_TargetIncrease_RetainsStoredCanSpend_EffectiveCanSpendBecomesFalse`
12. `F12_CatchupContribution_RestoresEffectiveCanSpendAutomatically`
13. `F13_CatchupPayment_CreditsTreasuryBalanceImmediately`
14. `F14_MultipleMembersCatchingUp_RestoresGroupFundingProgressively`
15. `F15_SpendingAfter100PercentFunding_DoesNotCloseFundingGate`
16. `F16_DirectSpend_RequiresActiveRecipientWallet`
17. `F17_FixedOwnerReview_IsExplicitlyDisallowed`
18. `F18_SpendExceedingTreasuryBalance_IsRejected`
19. `F19_SpendBeforeObligationsConfirmed_IsRejected`
20. `F20_OwnerMustContribute100PercentAndGroup100Percent_BeforeOwnerSpends`
21. `F21_TargetImmutability_MidPeriodReduction_IsStrictlyDisallowed`
22. `F22_LockedExcess_IsExcludedFromSpendableBalance`
23. `F23_AttemptingToSpendLockedExcess_FailsWithInsufficientFundBalance`
24. `F24_SpendWithinSpendableBalance_Succeeds`
25. `F25_MultipleMembersWithLockedExcess_HaveCumulativeProtection`
26. `F26_FreeFund_WithdrawalAndOwnerReview_Remains100PercentIntact`
27. `F27_FreeFund_DoesNotEnforceObligationOrIndividualContributionGate`
28. `F28_CompletedExpenseReporting_CreatesSupportCase_WithoutReversingMoney`
29. `F29_ReportingSameCompletedExpenseTwice_IsIdempotent`
30. `F30_ReportingNonCompletedOrNonExpense_ThrowsConflict`

### B. FixedFundV2Stage1Tests (13 tests) — 100% Passed
1. `T01_OwnerCreatesFixedFund_ExplicitCommitmentRequired_ShowsZeroOfTarget_NoClockRunning`
2. `T02_ConfirmObligations_WithOnlyOwner_ThrowsInsufficientMembers`
3. `T03_IndivisibleTarget_RefusesObligationConfirmationAndPreview`
4. `T04_DivisibleTarget_LocksSnapshotAndStartsIndividualCountdown`
5. `F2_ObligationSnapshot_IsImmutable_CannotChangeTargetOrResetDeadline`
6. `F1_TrustCommitment_ExplicitConfirmationRequired_RejectsMissingOrFalseOrWrongVersion`
7. `F4_FreeFund_PreservesZeroContamination_NoTrustFieldsOrDuration`
8. `F5_MembershipLock_AfterObligationsConfirmed_RejectsNewInviteAndPreExistingAcceptance`
9. `F6_ConfirmObligations_Idempotency_AndReconfirmationRejection`
10. `F7_ReplacementSeat_ValidationAndRejection`
11. `F8_ReplacementMember_PreservesHistoricalObligation_AndCreatesNewEpisodeObligation`
12. `F9_ReplacementMember_FullFlow_Reconfirm_Contribute_CompleteFund_OpenSpending`
13. `F10_MultipleReplacementsOnSameSeat_AndPartialPaidDeparture`

### C. FixedFundV2Stage2Tests (13 tests) — 100% Passed
1. `T05_MemberWithinDeadline_CannotBeKickedForMissingContribution`
2. `T06_MemberDeadlineExpired_AutoKick_RefundsPartial_Opens72hWindow`
3. `T07_ReplacementMember_JoinsNearEndOf72hWindow_GetsFullDuration`
4. `T08_ReplacementWindowExpires_WithUnfilledSeats_DissolvesAndRefundsRemaining`
5. `T09_OwnerFundingFailure_AutoDissolvesFund_AndRefundsAllMembers`
6. `T10_ReplacementWindowExpired_RejectsInvitationAcceptance`
7. `T11_FailFixedPeriodEarly_WhenVacantSeatsExist`
8. `T12_ConsecutiveReplacements_PartialPaidAndRefunded_CannotOpenSpending`
9. `T13_Scheduler_ProgressFixedFundDeadlines_AutoKicksAndFailsDeadlines`
10. `T14_FailPeriodEarly_OwnerAndUnauthorizedChecks`
11. `T15_ReplacementWindow_ExactBoundaryExpired_RejectsAcceptance`
12. `T16_Worker_FundA_Fails_FundB_ProcessesSuccessfully_WithIsolatedDbContexts`
13. `T17_SystemEvents_AutoKickAndFundingFailure_DoNotUpdateUserLastActionAt`

### D. FixedFundV2Stage3Tests (23 tests) — 100% Passed
1. `T10_SpendingState_BlockedUntilGroupFunded100Percent`
2. `T11_MemberWithoutCanSpend_CannotSpendEvenWhenFund100PercentFunded`
3. `T12_OwnerGrantsCanSpend_MemberSpendsSuccessfully_LedgerUpdates`
4. `T13_ProtectedExcess_CannotBeSpent`
5. `T14_CloseAndDissolve_RefundsMembers_TreasuryZero`
6. `T15_MemberVotingNo_ReceivesInstantRefund_RemainingSnapshotsUnchanged`
7. `T16_SilentMember_AutoRefundedAt24Hours_Departs`
8. `T17_CancelActivityProposal_PreservesDepartedRefunds`
9. `T18_OnlyOwnerVotesYes_72hWindowExpires_AutoDissolvesAndRefundsOwner`
10. `T19_MultiPeriodCarryOver_ProtectedExcess_PartialSpend_Dissolution`
11. `T20_DeterministicRemainder_WholeDongDistribution`
12. `T21_Maximum3Periods_StrictlyEnforced`
13. `T22_WorkerIsolation_Stage2WorkerTakesNoAction_WhenActivityClosedOrInTransition`
14. `T23_ProposalLifecycle_CannotCancelAfterVotingClosedOrInAwaitingReplacement`
15. `T24_UniqueProposal_CannotCreateSecondProposalWhileActive`
16. `T25_NewPeriodFinalObligationConsentAndDuration`
17. `T26_MemberDeparture_IncreasesObligation_BlockedWithoutConsent`
18. `T27_ReplacementMember_MustConfirmObligation_BeforeSettlementSucceeds`
19. `T28_AwaitingReplacement_72hWindow_ReplacementJoinsAndConfirms_Settles`
20. `T29_AwaitingReplacement_72hWindow_Timeout_AutoDissolves`
21. `T30_DtoAccuratePerMember_And_OutdatedObligationRejected`
22. `T31_ControllerEndpoint_ConfirmObligation_RealHttpIntegration`
23. `T32_RejoiningMember_AfterOptOut_StartsAtZeroCarryOver_AndLedgerReconciles`

### E. FixedFundV2Stage4Tests (18 tests) — 100% Passed
1. `T22_InactiveOwner_15DaysEvidence_OpensElection`
2. `T26_IncumbentOwnerInterferenceBlockedDuringElection`
3. `T24_ElectionTie_Or_AllBlankVotes_FailsImmediately`
4. `T23_WinnerConfirmation_Rejection_And_Expiration`
5. `T25_FailedElection_24hOptOut_And_CompulsoryDissolution`
6. `T27_HistoryPrivacy_And_DissolvedFundIsolation`
7. `T28_P1_1_VoluntaryLeaveDisallowed_UnlessOptOutOrClosed`
8. `T29_P1_2_AutomatedWorkerCompulsorySettlement_After24h`
9. `T30_P1_3_IncumbentOwnerOperationsFrozen_AfterElectionFailure`
10. `T31_P1_4_RequestAndTransactionPrivacy_ElectedOwner`
11. `T32_P1_5_InactivityEvidence_RequiresUserSessionInactivity_And_NoReactivationVeto`
12. `T33_P1_2_FailedElection_BlocksNewElection_AndWorkerChecksActiveElection`
13. `T34_P1_1_FundingDeadline_DuringActiveElection_FrozenElectorateProtected`
14. `T35_P1_1_FundingDeadline_DuringFailedElectionOptOutWindow_ProtectedFromFundingDissolution`
15. `T36_P1_1_FailFixedPeriodEarly_BlockedDuringActiveElectionAndPendingSettlement`
16. `T37_P1_2_VoluntaryOwnershipTransfer_EstablishesTenureAndHidesPastPrivateRecords`
17. `T38_P1_3_FormerMember_CanQueryOwnTransactionsAndRefundReceipts_ExcludesOtherMembersAndPostLeave`
18. `T39_P1_SameTimestamp_RequestAndOwnershipTransfer_ConsistentlyProtectedAcrossAll4Apis`

### F. FixedFundV2Stage5Tests (5 tests) — 100% Passed
1. `T28_Idempotency_DoubleRefund_And_WorkerRetry_ZeroDuplicateLedgerEntries`
2. `T29_ConcurrencyAndOrdering_SimultaneousDeadlineWorkerAndContribution_StrictStateIntegrity`
3. `T30_CompletedExpense_DisputeReport_OpensSupportCase_DoesNotReverseLedger`
4. `T31_MultiPeriod_Dispute_CarryOver_Election_FullLifecycle_EndToEnd`
5. `T32_HttpElection_Contract_RequestPayloadShapeMatchesController`

### G. FixedFundV2HttpTests (2 tests) — 100% Passed
1. `Http_FixedFundCreation_EnforcesTrustCommitment`
2. `Http_AcceptInvitation_EnforcesFixedTrust_AndPreservesFreeFundCompatibility`

### H. FreeFundMembershipRefundTests (15 tests) — 100% Passed
1. `FreeFund_Leave_RefundsExactEquity_FromCurrentMembershipEpisode`
2. `FreeFund_Leave_WhenMemberHasPendingReview_ThrowsMemberPendingRequests`
3. `FreeFund_Leave_CancelsOwnApprovedUnusedExpense_AndRefundsCleanly`
4. `FreeFund_Leave_CancelsOtherActiveApprovedUnusedNewestFirst_WhenBalanceInsufficient`
5. `FreeFund_Owner_CannotLeaveFreeFund_ThrowsOwnerCannotLeaveFreeFund`
6. `FreeFund_Kick_RefundsTargetExactEquity_AndCancelsTargetApprovedUnused`
7. `FreeFund_Kick_WhenTargetHasPendingReview_ThrowsMemberPendingRequests`
8. `FreeFund_Dissolve_RefundsAllActiveMembers_AndBringsTreasuryToZero`
9. `FreeFund_Governance_ElectionsAndOwnershipTransfers_AreDisallowed`
10. `FreeFund_Leave_IdempotentReplay_ReturnsPreviousResultWithoutDoubleDebit`
11. `FreeFund_Leave_Rejoin_IdempotencyConflict_AndEpisodeIsolation`
12. `FreeFund_Leave_MultiRequestHeadroom_CancelsNewestFirst_KeepsOlderRequest`
13. `FreeFund_ConcurrentLeaveAndKick_OnlyOneSucceeds_LedgerConsistent`
14. `FreeFund_ConcurrentLeaveAndDissolve_PreservesZeroBalanceInvariant`
15. `FreeFund_Leave_RollsBackTransaction_WhenNotificationFails`

### I. FreeFundRequestWorkflowTests (17 tests) — 100% Passed
1. `FreeFund_Member_CannotCreateTwoActiveExpenses_Sequential`
2. `FreeFund_Member_CannotCreateTwoActiveWithdrawals_Sequential`
3. `FreeFund_ConcurrentExpenseCreation_FromTwoSeparateContexts_EnforcesActiveLimit`
4. `FreeFund_Member_CanCreateOneExpenseAndOneWithdrawalConcurrently`
5. `FreeFund_Expense_ApprovedByOwner_BecomesApprovedUnused_DoesNotTransferMoneyOrComplete`
6. `FreeFund_ApprovedUnused_ExpiresAfterSevenDays_AndPersistsToDatabase`
7. `FreeFund_Requests_MemberCanOnlyViewOwnRequests_OwnerCanViewAll`
8. `FreeFund_InitiatePayment_WhenProviderUnavailable_RejectsAndKeepsApprovedUnused`
9. `FreeFund_Withdrawal_BlockedWhenActiveApprovedUnusedExists`
10. `FreeFund_Withdrawal_UnblockedAfterApprovedUnusedCancelled`
11. `FreeFund_OwnerDirectSpend_CannotExceedUnreservedBalance`
12. `FreeFund_Withdrawal_ExceedingMemberEquity_ThrowsInsufficientMemberEquity`
13. `FreeFund_AutoCancelPendingWithdrawal_WhenExpenseDropsMemberEquityBelowRequestedAmount`
14. `FixedFundV2_RegressionCheck_ExpenseWorkflow_Unchanged`
15. `FreeFund_InitiatePayment_UnauthorizedCaller_ThrowsForbidden_BeforeExpirationCheck`
16. `FreeFund_AtomicExpirationSweep_ConcurrentWorkers_CreatesExactlyOneActivity`
17. `FreeFund_ExpirationSweep_TransactionRollsBack_WhenNotificationThrows`

### J. FreeFundEquityIntegrationTests (13 tests) — 100% Passed
1. `FreeFund_OwnerReviewsMemberExpense_CalculatesEquityWithoutDoubleCounting`
2. `FreeFund_DirectOwnerSpend_CalculatesEquityCorrectly`
3. `FreeFund_NewcomerAfterOldExpense_StartsAtZeroAndDoesNotBearPastExpense`
4. `FreeFund_ReconciliationMismatch_BlocksPreviewLeaveKickAndDissolve`
5. `FreeFund_ZeroLedgerWithPositiveEquity_BlocksLeaveAndDissolveWithZeroRefund`
6. `FreeFund_TwoExpensesWithSameAmount_DoNotMismapOrCrossMatch`
7. `FreeFund_CompletedRequestMissingLedgerTransaction_FailsReconciliation`
8. `FreeFund_MemberLeavesReceivesRefundRejoins_NewEpisodeStartsAtZeroAndCalculatesCorrectly`
9. `FreeFund_OrphanWithdrawal_ThrowsFundOrphanOutflow`
10. `FreeFund_OrphanExpense_ThrowsFundOrphanOutflow`
11. `FreeFund_AntiTamper_UserSuppliedPurposeSequence_IgnoredAndThrowsIndeterminateOrder`
12. `FreeFund_ThreeEvents_SameTimestamp_ThrowsFundIndeterminateOrder`
13. `FreeFund_RealPostingSequence_OrdersBySystemCompletedAt_WithoutModifyingIdempotencyKey`

### K. FreeFundEquityEngineTests (7 tests) — 100% Passed
1. `Example1_A600k_B400k_SharedExpense200k_Equity_480k_320k`
2. `Example2_BLeavesAfterExpense_Receives320k`
3. `Example3_BHas320k_Contributes200kMore_LeavesImmediately_Receives520k`
4. `Example4_CJoinsAfterExpense_Contributes200k_LeavesBeforeNewExpense_ReceivesFull200k`
5. `Example5_BContributes400k_Withdraws100k_BeforeSharedExpense`
6. `Example6_BLeavesRefunded320k_RejoinsAndContributes200k_FreshBaseline`
7. `Example7_VndIntegerRounding_ConservesTotalBalance`

### L. FreeFundApiContractNotificationTests (9 tests) — 100% Passed
1. `FreeFund_EquityEndpoint_ReturnsBreakdown_ActiveReservations_AndMemberEquities`
2. `FreeFund_GetRequest_Authorization_OwnerAndRequesterAllowed_OtherMembersForbidden`
3. `FreeFund_ListRequests_StrictDataIsolation_NonOwnersOnlySeeOwnRequests`
4. `FreeFund_TransactionsEndpoint_ReturnsPublicFundHistory_HidesPrivateWallets`
5. `FreeFund_NotificationsAndOutbox_CreatedForFullLifecycleEvents`
6. `FreeFund_TransactionsEndpoint_ReturnsAllFourTypes_WithCorrectCorrelationAndPaging`
7. `FreeFund_NotificationsAndActivities_PrivateRequestsDoNotLeakToOtherMembers`
8. `FreeFund_AutoCancelExcessWithdrawals_PrivateActivityAndNotification_DoNotLeakToOtherMembers`
9. `FreeFund_TransactionsEndpoint_MasksPrivateWithdrawalDetailsForOtherMembers`

### M. FreeFundFinalAcceptanceTests (4 tests) — 100% Passed
1. `FreeFund_FullLifecycle_EndToEnd_FromCreationToDissolution_WithFullAccountingReconciliation`
2. `FreeFund_StressAndConcurrency_ParallelIdempotentOperations_OnSqlServer_WithIndependentConnections`
3. `FixedFundV2_BackwardCompatibility_WithLegacyRefundScope_PreservedAndUnmodified`
4. `FreeFund_HttpApi_OpenApiContract_RealServerSmokeTest`

### N. BankLinkingAndWalletGuardTests (16 tests) — 100% Passed
1. `ActiveBankGuard_ThrowsWhenUserHasNoActiveBank`
2. `ActiveBankGuard_PassesWhenUserHasActiveBank`
3. `CreateWallet_ThrowsWhenNoActiveBank`
4. `BankUnlinkSafety_BlocksUnlinkWhenWalletBalanceGreaterThanZero`
5. `BankUnlinkSafety_AllowsUnlinkWhenAllWalletsZero`
6. `BankLinking_UserCanOnlyLinkOneActiveBankAccount`
7. `BankReactivate_AllowsReactivationOfDeactivatedAccount`
8. `BankStatusTransitions_Active_Inactive_Suspended`
9. `BankDirectPayment_EnforcesActiveBankGuard`
10. `BankSecurity_PreventsDuplicateExternalAccountId`
11. `WalletOperations_RejectTransactionsWhenBankSuspended`
12. `BankDisputeReconciliation_PreservesIntegrity`
13. `ActiveBank_RequiredForIncomePosting`
14. `ActiveBank_RequiredForExpensePosting`
15. `ActiveBank_RequiredForP2PTransfer`
16. `ActiveBank_RequiredForInternalTransfer`

### O. FinancialIntegrityPhaseTests (14 tests) — 100% Passed
1. `Manual_transaction_requires_current_pin_and_evidence_without_side_effects`
2. `Manual_transaction_wrong_pin_rejects_without_side_effects`
3. `Correction_requires_pin_and_preserves_audit_trail`
4. `Recurring_transaction_schedule_execution_accuracy`
5. `Goal_movements_verify_pin_before_contribute_or_withdraw`
6. `Goal_movement_rejects_insufficient_wallet_balance`
7. `Pin_verification_fails_when_locked`
8. `Evidence_attachment_validates_ownership`
9. `Evidence_cannot_be_shared_across_distinct_users`
10. `Ledger_transaction_immutable_after_posting`
11. `Financial_transaction_correlates_to_ledger_record`
12. `Double_spending_prevention_on_concurrent_requests`
13. `Strict_transaction_isolation_between_accounts`
14. `Rollback_on_unhandled_exception_leaves_zero_orphans`
