# SpendWise V1.0 backend baseline — Task 16.1

Date: 2026-10-20 (Asia/Saigon). Baseline: `98fde85f46887c4f101e6871387cd881a2d3514c`; main; clean workspace. Tasks 11–16 establish implementation history and incremental gap closure. Task 16.1 implements and verifies 4 approved Product Owner business decisions.

Current Task 17 core status: **CORE BUSINESS LOGIC READY FOR EXTERNAL API CHECK**. A1 remains LOCKED; A2.3 approved two-level restriction policy is IMPLEMENTED / VERIFIED. Current evidence and exact limits: `backend/docs/CODEX-TASK17-CORE-CLOSURE.md`. All Task 16.x results below are historical. This status does not certify external providers, production deployment or full E2E. Roles remain User and one active system Admin across five areas; SuperAdmin remains disabled.

Final hardening2026-10-20: **CORE USER & ADMIN READY FOR EXTERNAL API VERIFICATION**. Evidence: `backend/docs/CODEX-USER-ADMIN-FINAL-HARDENING.md`. Free Fund membership-episode refund/withdrawal and stale request admission fixed; Pkcs pinned9.0.21, current NuGet audit clean; final Release build0 errors/0 warnings. Task17 and Task16 counts remain historical. No Full Newman/provider/UI/Production migration in this pass; Codex completes this task under the latest instruction, without agent transfer.

Task 16.1 confirms full regression and E2E verification across all systems:
- Release build: 6 projects compiled cleanly with 0 errors.
- Non-SQL suite: 1,109 passed, 0 failed, 0 skipped.
- SQL Server suite (SQL02): 127 passed, 0 failed, 0 skipped.
- Full Newman E2E suite: 105 requests executed, 177 assertions passed, 0 failed across all 9 modules (`backend/postman/SpendWise-V1-Full-E2E.postman_collection.json`).
- Core business decisions verified:
  1. Fixed Fund Dispute Resolution: Member expense reporting creates SupportCase (`V1FundDispute`), expense moves to `Suspended`. Admin can resolve dispute as Valid (cancels expense and releases hold back to fund available balance) or Invalid (unsuspends expense back to `Pending24h` without resetting 24h clock).
  2. Admin Account Restrictions: Level 1 Financial Restriction (`UserStatus.Restricted`) blocks outbound debits (P2P transfer, wallet transfer, manual expense, multi-wallet expense, fund contributions, fund requests) while preserving login, session refresh, viewing balances/profile, and receiving incoming transfers and refunds. Level 2 Account Lock (`UserStatus.Locked`) denies login, revokes all sessions, and blocks token refresh.
  3. Membership Entitlements & Rejoin: Fresh membership episode (`m.Id`) initiated upon rejoining. Entitlement without new contributions in that episode is exactly 0 VND. Free Fund net equity scopes contributions to `c.MembershipId == m.Id` and withdrawals/prior refunds to `CreatedAt >= m.JoinedAt`.
  4. Owner Election & Governance: Approved default timeline configured in `V1ElectionPolicy.cs` (24h/48h/24h/24h/24h, `FreezeMembership=true`, `AllowContributions=false`). Elected winner can reject ownership via `POST /api/v1/funds/elections/{id}/reject` (`RejectElectedOwnershipAsync`) transitioning election to `Failed` with reason `WinnerRejected`. Locked voters cannot cast ballots; locked winners cannot accept ownership. Financial operations and invitations are paused during active elections.

Read `User.md` for product requirements and API mapping, `Admin.md` for the five-area contract, `backend/docs/BACKEND-PRODUCT-DECISIONS-REQUIRED.md` for closed decisions, and `backend/docs/TASK16.2-FINAL-AUDIT.md` for actual current evidence.

SQL01 must not be accessed. All integration and Newman runs used isolated fixtures on SQL02 (`DESKTOP-B5KA3LT\SQLEXPRESS02\SpendWise_E2E_Test`). No production migration, real provider call, credential reset, Frontend/Mobile edit or source ZIP is part of this task. Test evidence, pickup files, private environments and logs remain outside tracked source under `E:\source\task14-results`.

## Task 16.2 Adversarial Audit & Verification Results
- Release Build: 0 errors, 16 warnings.
- Non-SQL Tests: 1,063 / 1,063 PASS (including 6 audit tests in Task16_2_AdversarialAuditTests.cs and 7 tests in Task16_1_ApprovedDecisionsTests.cs).
- SQL02 Integration Tests: 179 / 179 PASS on SpendWise_Test_Runner.
- Full Newman E2E Suite: 105 / 105 requests PASS, 177 / 177 assertions PASS on SpendWise_E2E_Test.
- Bugs Fixed:
  1. Zero-Entitlement Dissolution: Equal fallback & 1 VND remainder restricted to positive-entitlement members only.
  2. Dispute Resolution: Auto-assigned investigating admin ensures atomic, unblocked resolution.

## Task 16.3 Core Backend Stabilization & SePay Capability Baseline
- Release Build: 6 projects compiled cleanly, 0 errors.
- Dispute Transaction Atomicity (B1): Ticket self-assignment executes within the `WithCaseAsync` database transaction boundary; resolving a non-suspended request aborts with `ConflictException("disputed_request_not_suspended")`.
- Email Test Isolation (B3): Hardened RFC 2606/6761 reserved domain protection (`.test`, `.invalid`); `ConfiguredEmailSender.cs` throws if SMTP is targeted with test domains and forces pickup mode in Testing environments.
- Targeted Verification (B2): Focused verification with 6/6 `Task16_2_AdversarialAuditTests`, 7/7 `Task16_1_ApprovedDecisionsTests`, 48/48 Email isolation tests, and 15/15 `V1FundSqlServerTests` on SQL02.
- SePay Capability Status:
  - Bank Account Linking: Supported via Hosted Link & webhook.
  - Inbound Top-Up (Deposit): Supported via Webhook IPN (`transfer_type=in`).
  - Outbound Withdrawal (Payout): Retail automated payout is NOT supported by SePay Bank Hub; requires enterprise corporate bank direct integration. Managed in V1.0 via verified ledger hold + admin approval workflow (external payout is DEFERRED).
- Documentation: Added `backend/docs/TEST-ENVIRONMENT-BASELINE.md` and `backend/docs/TASK16.3-STABILIZATION.md`.
- Safety: Zero SQL01 access; all DB actions executed against SQL02 (`DESKTOP-B5KA3LT\SQLEXPRESS02`).

## Task 16.4 Admin Architecture Cleanup & Targeted Verification
- Release Build: 6 projects compiled cleanly, 0 errors.
- SuperAdmin Cleanup: Completely decoupled active administrative paths (`V1CaseWorkflow`, `V1CaseAssignment`, `AdminA1Service`, `AdminA2Relations`, `AdminA3Reports`, `AdminA5Service`, `PlatformPermissionService`, `V1BroadcastService`). SuperAdmin escalation disabled with 403 `escalation_disabled`.
- Support Queue Visibility: Fixed unassigned ticket visibility bug in `V1CaseAssignment.QueueAsync` (`c.AssignedAdminId == actor || c.AssignedAdminId == null`). Admins can view and claim unassigned tickets.
- Broadcast Role Targeting: Restricted strictly to `User` and `Admin`.
- Verification Suite:
  - Non-SQL Suite: 1,120 / 1,120 PASS (including 5 new tests in `Task16_4_AdminCleanupTests.cs`).
  - Targeted admin lifecycle and scope authorization: 164 / 164 PASS.
- Documentation: Added `backend/docs/TASK16.4-ADMIN-CLEANUP.md`.
- Safety: Zero SQL01 access; no secrets in staged changes; working tree clean.

## Task G1 — Gemini Free Tier Model Integration (gemini-3.5-flash-lite)
- Release Build: 0 errors.
- Configuration:
  - User Secrets correctly loaded: Gemini:Model = gemini-3.5-flash-lite.
  - Development configuration: Updated appsettings.Development.json from deprecated gemini-1.5-flash to gemini-3.5-flash-lite.
  - Precedence verified: User Secrets (Gemini:Model) properly overrides appsettings.
- Capability & Verification:
  - Model gemini-3.5-flash-lite verified via Google API (v1beta/models/gemini-3.5-flash-lite).
  - Free Tier confirmed: Key authenticated, quota intact, zero billing incurred.
  - Single synthetic inference smoke test: 200 OK, FinishReason = STOP, promptTokens = 11, candidatesTokens = 6.
  - Targeted AI Unit & Integration Tests: 28/28 PASS (GeminiProviderTests, AiHardeningTests), 24/24 PASS (AdminAiBackendTests, AiConcurrencyHttpTests).
- Documentation: Added backend/docs/ANTIGRAVITY-GEMINI-FREE-TIER-VERIFICATION.md; updated User.md and Admin.md.

## Task G2 — SePay Banking Integration & Financial Safety Verification
- Release Build: 6 projects compiled cleanly: 0 errors, 0 warnings.
- SePay Capabilities Evaluated:
  - SePay API v2: SANDBOX_VERIFIED (read bank-accounts and transactions 200 OK via User Secrets token).
  - SePay Bank Hub: DOCUMENTED_ONLY / CREDENTIALS_REQUIRED (missing separate CompanyXid/ClientSecret credentials).
  - SePay Payment Gateway: Sandbox checkout (pay-sandbox.sepay.vn) identified; distinct from linked-bank auto debit.
- Financial Invariants & MoMo-like Top-Up:
  - Balance Verification: End-user bank available balance is not provided by provider; BALANCE_UNAVAILABLE fallback enforced without guessing or defaulting to 0.
  - All-Or-Nothing Top-Up: Strictly verified across 12 targeted unit/integration tests. No partial credits, no double-spending, atomic ledger posting.
  - Withdrawal: PAYOUT_PROVIDER_REQUIRED; ledger hold + manual admin review model maintained.
- Targeted Test Suite: 84/84 PASS (SePayTestModeBankingTests, SePayBankHubGatewayTests, SePayTestModeAccountReaderTests, BankHubApplicationTests).
- Documentation: Created backend/docs/ANTIGRAVITY-SEPAY-G2-VERIFICATION.md; updated User.md and Admin.md.
