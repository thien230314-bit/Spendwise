# SpendWise V1.0 — Current Backend Acceptance (Task 16.3)

**Date:** 2026-10-20  
**Overall Core Backend Status:** **CORE BACKEND PARTIAL**
**Current evidence:** `CODEX-FINAL-CORE-ACCEPTANCE.md` supersedes historical completion claims and counts below. A1 remains LOCKED; A2.3 remains PARTIAL / POLICY_BLOCKED; maintenance deployment safety remains unverified. Targeted SQL02/HTTP acceptance does not verify live providers or all production operations.
**Evidence Records:** `TASK16.2-FINAL-AUDIT.md`, `TASK16.3-STABILIZATION.md`, `TEST-ENVIRONMENT-BASELINE.md`  

---

## 1. Executive Summary & Verification Evidence

All 4 Product Owner approved business decisions have been officially implemented, audited, stabilized, and verified:
- **Build Status:** 6/6 projects compiled in Release configuration with **0 errors**.
- **Non-SQL Suite:** **1,132 tests PASS** across domain, application, workflow, security, and integration fixtures.
- **SQL Server Integration Suite on SQL02 (`DESKTOP-B5KA3LT\SQLEXPRESS02`):** **179 / 179 PASS** (0 failed, 0 skipped); targeted `V1FundSqlServerTests` (15/15 PASS).
- **Targeted Business Tests:** `Task16_1_ApprovedDecisionsTests` (7/7 PASS), `Task16_2_AdversarialAuditTests` (6/6 PASS), `EmailDeliveryModeTests` & `TargetedEmailDispatchTests` (48/48 PASS).
- **Full Newman API E2E Suite:** **105 / 105 requests PASS**, **177 / 177 assertions PASS** (0 failed) against Release API on loopback port 5164 connected to SQL02 `SpendWise_E2E_Test`.

---

## 2. Implemented Business Decisions

1. **Fixed Fund Dispute Resolution:**
   - Members reporting a large expense hold in `Pending24h` transitions the expense to `Suspended` and opens an assigned `SupportCase` (`Category = "V1FundDispute"`, `LinkedTransactionId = item.Id`).
   - Admin resolves via `POST /api/v1/admin/support/disputes/{requestId}/resolve`:
     - **Valid report (`!IsRejected`):** Expense cancelled (`Cancelled`), reserved hold released back to fund available balance, `SupportCase` marked `Resolved`, audit logged, fund members notified.
     - **Invalid report (`IsRejected`):** Expense unsuspended back to `Pending24h` with remaining 24h timer preserved, `SupportCase` marked `Rejected`, audit logged, member notified.

2. **Admin Account Restrictions:**
   - **Level 1 — Financial Restriction (`UserStatus.Restricted`):** Outbound debit operations blocked (P2P transfer, wallet transfer, manual expense, multi-wallet expense, fund contribution, create fund spending request). User retains login, token refresh, read-only balance/profile/history, support ticket intake, and receipt of incoming transfers and refunds.
   - **Level 2 — Account Lock (`UserStatus.Locked`):** Immediate revocation of active sessions, 403 `account_unavailable` on login, token refresh blocked. Reversible by Admin.

3. **Fixed & Free Fund Membership Entitlements & Rejoin:**
   - Members who left and received refund/settlement start a fresh membership episode (`m.Id` new, `m.JoinedAt` new) upon rejoining.
   - Entitlement/refund for a fresh episode without new contribution is exactly `0 VND` (`NoContribution`).
   - Unified refund calculator scopes contributions to current membership episode (`c.MembershipId == m.Id`) and prior deductions/withdrawals to `CreatedAt >= m.JoinedAt`.
   - Preview equals execution invariant verified.

4. **Owner Election & Governance:**
   - Approved default timelines in `V1ElectionPolicy.cs`: Registration 24h (86,400s), Round 1 Nomination 48h (172,800s), Runoff 24h (86,400s), Round 2 Confirmation 24h (86,400s), Acceptance 24h (86,400s), `FreezeMembership = true`, `AllowContributions = false`, Outcomes = `"Fail"`.
   - Elected winner rejection endpoint implemented: `POST /api/v1/funds/elections/{id}/reject` -> fails election with reason `WinnerRejected`, nominal owner remains, members can leave and refund.
   - Locked voter cannot cast ballot; locked winner cannot accept; electorate denominator frozen at election snapshot.
   - Financial operations and invitations paused during active election (`election_finance_paused`).

---

## 3. Operational Boundaries & External Providers

- **System Roles:** Exactly 1 active system Admin. SuperAdmin remains retired and blocked with 403.
- **External Providers (Deferred):**
  - Live Gemini AI: Fail-safe 503 response when key/model is unconfigured; does not block core V1.0.
  - Live SePay Banking: TestMode routes denied in non-dev; live bank settlement not required for core V1.0.
  - Live External Push / Live SMTP Mailbox: Outbox persisted; live network transmission not verified.
- **Frontend / Mobile:** Read-only / out of scope for Backend V1.0 completion.
