# SPENDWISE V1.0 — TASK 16.3 STABILIZATION & SEPAY CAPABILITY AUDIT

**Author:** Principal .NET Backend Engineer, Financial Systems Architect & QA  
**Date:** 2026-10-20  
**Baseline Git Commit:** `b75ecca603a9add3032a04772591a22b8d1ccf17`  
**Workspace:** `E:\source\SpendWise-final-merged`  
**Status:** **STABILIZED & VERIFIED**

---

## 1. Executive Summary & Objectives

Task 16.3 focuses on core backend stabilization, targeted verification of approved business decisions, and an exhaustive technical capability assessment of the SePay integration without entering endless Newman E2E loops.

### Key Objectives Completed:
1. **Dispute Transaction Atomicity (B1):** Replaced disjointed assignment and resolution logic in `AdminA5Service` with atomic execution within `workflow.WithCaseAsync`. Enforced strict state validation requiring disputed requests to be in `Suspended` state before resolution can occur, throwing `ConflictException("disputed_request_not_suspended")` on violation.
2. **Email Test Isolation Hardening (B3):** Hardened `ConfiguredEmailSender.cs` with RFC 2606 / RFC 6761 reserved domain checking (`.test`, `.invalid`). Ensured all testing environments enforce `EmailDeliveryMode.Pickup`, preventing synthetic emails from ever contacting real SMTP or leaking to external networks.
3. **Targeted Test Execution (B2):** Ran focused integration and unit suites for the four approved Product Owner decisions and adversarial edge cases rather than unnecessary 100+ Newman request runs.
4. **SePay Provider Capability Check:** Conducted a comprehensive audit of official SePay documentation (`developer.sepay.vn`) compared against SpendWise V1.0 implementation.

---

## 2. Technical Stabilization & Bug Fixes

### A. Admin Dispute Resolution Atomicity & State Guard
- **Root Cause Identified in Task 16.2 Review:**
  - `AdminA5Service.ResolveDisputeRequestAsync` performed an out-of-transaction database commit (`await db.SaveChangesAsync(ct)`) to assign unassigned dispute tickets to the current operator prior to calling `ResolveAsync`. If resolution subsequently failed (e.g., due to invalid dispute state), the ticket was left assigned to the admin in a dirty state.
  - Furthermore, `ResolveAsync` silently bypassed dispute state checks when `disputedRequest.State != "Suspended"`, resolving the support ticket without updating or guarding the underlying financial hold.
- **Implemented Fix:**
  1. In `V1CaseWorkflow.WithCaseAsync`: Within the single snapshot database transaction, if `ticket.Category == "V1FundDispute"` and `ticket.AssignedAdminId == null`, self-assignment to the acting operator is performed inside the transaction context. If the action aborts, the assignment is rolled back automatically.
  2. In `AdminA5Service.ResolveAsync`: Enforced that if `disputedRequest is null || disputedRequest.State != "Suspended"`, the service immediately throws `ConflictException("disputed_request_not_suspended", "Disputed request must be in Suspended state to be resolved.")`.
  3. Removed the external `SaveChangesAsync` in `ResolveDisputeRequestAsync`.
- **Verification:** `Task16_2_AdversarialAuditTests.CaseD4_D5_DisputeResolution_NonSuspendedRequest_ThrowsConflict` passes with exact error code match.

### B. Email Test Isolation
- **Security & Network Safety Enforcement:**
  - Verified `ConfiguredEmailSender.cs`:
    - `IsReservedTestRecipient` matches domains ending in `.test` and `.invalid`.
    - In `Testing` environment, `EmailDeliveryMode.Pickup` is unconditionally returned, writing email artifacts to local temporary files.
    - If `SendSmtpAsync` is ever invoked directly targeting a reserved test domain, an immediate `InvalidOperationException("Reserved test recipients cannot be sent through SMTP.")` is thrown before any network socket is opened.
  - Verified `EmailDeliveryModeTests` and `TargetedEmailDispatchTests` (48/48 PASS).

---

## 3. SePay Capability Audit Matrix

Official documentation (`developer.sepay.vn`) was audited against the SpendWise implementation:

| Capability Area | SePay Official API Support | SpendWise V1.0 Codebase | Environment Behavior (Sandbox vs Live) | Status / Classification |
|---|---|---|---|---|
| **Bank Account Linking** | Supported via Hosted Link Hub & Bank challenge webhook | `SePayBankService.cs`, `TestModeSePayFeature.cs` | Dev/Testing: Mocked challenge and instant confirmation. Prod: Webhook signature verification via API secret. | **SUPPORTED & TESTED** |
| **Inbound Top-Up (Deposit)** | Supported via Webhook IPN (`transfer_type=in`) | `SePayWebhookController.cs`, `WalletTopUpHandler.cs` | Dev/Testing: Mock inbound webhook credit. Prod: Cryptographic signature verification, idempotent reference check. | **SUPPORTED & TESTED** |
| **Outbound Withdrawal (Payout)** | **NOT SUPPORTED** for standard retail/individual SePay accounts. SePay Bank Hub does not offer a public disbursement API for retail users. Payouts require corporate enterprise bank direct integration (e.g. TPBank/VietinBank Corporate VA) with bespoke contracts. | `WalletWithdrawalService.cs` reserves funds and ledger balances; marks transaction pending admin approval. | Dev/Testing: Simulates ledger hold. Prod: Manual admin reconciliation / corporate batch payout. | **DEFERRED (EXTERNAL INTEGRATION)** |

### Conclusion on SePay Outbound Withdrawal:
SpendWise V1.0 correctly handles withdrawal at the financial core level: reserving ledger funds, preventing double spending, and providing complete auditability. Automated external payout execution is explicitly deferred until corporate banking agreements are finalized.

---

## 4. Verification Evidence & Test Results

All tests executed with zero access to `SQL01`. All relational tests executed on `DESKTOP-B5KA3LT\SQLEXPRESS02`.

| Test Suite / Scope | Command | Executed | Passed | Failed | Duration | Status |
|---|---|---|---|---|---|---|
| **Task 16.2 Adversarial Audit** | `dotnet test --filter "FullyQualifiedName~Task16_2_AdversarialAuditTests"` | 6 | 6 | 0 | 23 s | **PASS** |
| **Task 16.1 Approved Decisions** | `dotnet test --filter "FullyQualifiedName~Task16_1_ApprovedDecisionsTests"` | 7 | 7 | 0 | 16 s | **PASS** |
| **Email Isolation & Delivery** | `dotnet test --filter "FullyQualifiedName~EmailDeliveryModeTests\|FullyQualifiedName~TargetedEmailDispatchTests"` | 48 | 48 | 0 | 18 s | **PASS** |
| **SQL02 Concurrency Integration** | `dotnet test --filter "FullyQualifiedName~V1FundSqlServerTests"` (on SQL02) | 15 | 15 | 0 | 1 m 9 s | **PASS** |
| **Release Solution Build** | `dotnet build backend/SpendWise.slnx -c Release` | 6 projects | 6 projects | 0 errors | 45 s | **PASS** |

---

## 5. Summary of Modified Files

1. `backend/SpendWise.Application/Features/Administration/AdminA5Service.cs`:
   - Enforced `disputed_request_not_suspended` guard.
   - Removed un-isolated `SaveChangesAsync` before dispute resolution.
2. `backend/SpendWise.Application/Features/Administration/V1CaseWorkflow.cs`:
   - Unified self-assignment of unassigned `V1FundDispute` support cases inside the `WithCaseAsync` database transaction boundary.
3. `backend/SpendWise.Infrastructure/Identity/ConfiguredEmailSender.cs`:
   - Hardened `IsReservedTestRecipient` domain validation against RFC-reserved test domains.
4. `backend/docs/TEST-ENVIRONMENT-BASELINE.md`:
   - Established complete runbook for SQL02 databases, test environments, and isolation policies.
5. `backend/docs/TASK16.3-STABILIZATION.md`:
   - This stabilization and SePay capability report.
