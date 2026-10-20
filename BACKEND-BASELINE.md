# SpendWise V1.0 backend baseline — Task 16.1

Date: 2026-10-20 (Asia/Saigon). Baseline: `98fde85f46887c4f101e6871387cd881a2d3514c`; main; clean workspace. Tasks 11–16 establish implementation history and incremental gap closure. Task 16.1 implements and verifies 4 approved Product Owner business decisions.

Overall Backend V1.0: **FUNCTIONAL COMPLETE (4/4 Approved Decisions Implemented: Fixed Fund Dispute Resolution, Level 1 & Level 2 Account Restrictions, Fresh Membership Episodes & Scoped Rejoin Equity, Owner Election Governance & Timeline Defaults)**. Roles: User and one active system Admin operating five Admin areas; SuperAdmin is disabled.

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

Read `User.md` for product requirements and API mapping, `Admin.md` for the five-area contract, `backend/docs/BACKEND-PRODUCT-DECISIONS-REQUIRED.md` for closed decisions, and `backend/docs/TASK16.1-VERIFICATION.md` for actual current evidence.

SQL01 must not be accessed. All integration and Newman runs used isolated fixtures on SQL02 (`DESKTOP-B5KA3LT\SQLEXPRESS02\SpendWise_E2E_Test`). No production migration, real provider call, credential reset, Frontend/Mobile edit or source ZIP is part of this task. Test evidence, pickup files, private environments and logs remain outside tracked source under `E:\source\task14-results`.
