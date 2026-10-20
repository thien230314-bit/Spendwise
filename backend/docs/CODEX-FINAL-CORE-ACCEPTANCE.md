# SpendWise V1.0 — Codex core acceptance

Date: 2026-10-20. Result: **CORE BACKEND PARTIAL**.

## A. Actual source baseline

- Before HEAD: `94e0c94725341f02c18be650d968572dbbe3c795`; branch `main`; clean working tree.
- Remote: `https://github.com/thien230314-bit/Spendwise`; fetched origin/main, initially 0 ahead / 0 behind.
- Verified source: `V1CaseWorkflow.cs`, `V1CaseAssignment.cs`, `AdminA1Service.cs`, `AdminA2Service.cs`, `AdminA2Relations.cs`, `AdminA5Service.cs`, `AdminA6Service.cs`, `PlatformPermissionService.cs`, `PlatformPermissions.cs`, Admin controllers, `V1Authorization.cs`, `V1BroadcastService.cs`, `V1MaintenanceService.cs`, fund requests/contributions/finance/refunds/election/activity services, session validation, email sender and SQL/HTTP fixtures.
- Read the prescribed User/Admin, baseline, decision, acceptance, test-environment and Task 16.2–16.4 documents. Historical PASS totals were not used as current proof. SuperAdmin.md was not opened or restored.

## B. Actual acceptance results

Final successful runs: `core-final-regression.trx` **221/221**, `final-contracts-restored.trx` **11/11**. One restriction test overlaps: **231 distinct tests**, **0 final failures**, **0 skipped**. This is a selected regression set, not the full suite. SQL02 accounts for **22 tests**, including **7 new dispute cases**. `CoreAcceptanceHttpTests` has 20 cases: **19 actual HTTP cases and one service-level expense-outflow safeguard**; that safeguard is not counted as HTTP proof.

Evidence files remain outside Git in `%TEMP%/spendwise-codex-acceptance/`. Release solution build after the final changes: **PASS**, **0 errors, 8 warnings**. Warnings are existing NU1902/NU1903 advisories for `System.Security.Cryptography.Pkcs 9.0.4`; dependency remediation was not part of this task.

| Gate | Actual test | Passed | Failed | Not run / limit | Evidence |
|---|---|---:|---:|---|---|
| SQL02 Dispute | `Core_dispute_SQL_resolution_preserves_hold_ledger_and_deadline` (2); `Core_dispute_SQL_failure_after_saved_decision_rolls_back_every_business_effect` (1); `Core_dispute_SQL_overlapping_resolution_or_cancel_has_one_consistent_outcome` (2); `Core_dispute_SQL_user_or_unassigned_admin_cannot_resolve` (2) | 7 | 0 | Full SQL suite not run | `core-final-regression.trx`; independently created SQL02 catalogs, separate contexts and lock-wait observation |
| HTTP Decision 1 | `Dispute_HTTP_report_resolve_and_authorization_preserve_financial_state` | 2 | 0 | Live provider settlement excluded | Real report/resolve routes; Suspended, valid/invalid resolution, denied User, retry denial, ticket/history/ledger/wallet/hold/deadline assertions |
| HTTP Decision 2 | `Admin_restrict_lock_unlock_HTTP_enforces_sessions_and_preserves_money`; `Account_status_HTTP_requires_pin_case_subject_and_approved_status` (5) | 6 | 0 | Incoming transfers/refunds to Restricted users not exercised through HTTP in this task | Final restriction rerun additionally proves old read token and refresh work while Restricted; lock rejects old token, refresh and login; unlock permits fresh login; PIN/case/peer/status guards and persisted audit/notifications |
| HTTP Decision 3 | `Membership_HTTP_leave_rejoin_zero_old_entitlement_and_new_contribution` | 2 | 0 | New multi-period carry-over HTTP scenario not run | Fixed + Free: refund 500,000; rejoin preview 0; new contribution/refund 100,000; refund replay; two membership episodes; exact wallet balance and ledger count |
| HTTP Decision 4 | `Election_HTTP_real_timeline_freezes_finance_and_fails_on_rejection_or_locked_voter` | 2 | 0 | HTTP tie-break and acceptance-expiry paths not run | Real inactivity evidence/configuration; fake clock for 15d/24h/48h/24h; registration/ballot/confirmation/reject/read endpoints; locked voter/accept denied; stable voter count, unchanged Owner/ledger; Failed outcomes |
| ADM-01 | `All_A1_and_legacy_aggregate_routes_enforce_live_V1_roles` | 4 | 0 | A1 remains LOCKED; no source edit | Actual dashboard/tasks/alerts/activity/source routes, Admin positive, User/SystemAdmin/SuperAdmin negative |
| ADM-02 | `Directory_and_legacy_mutations_cannot_bypass_V1_user_management`, plus Decision 2 above | 10 | 0 | A2.3 wider baseline remains PARTIAL / POLICY_BLOCKED | Scoped profile/sessions/groups/friends/history/restrictions; legacy arbitrary mutation denial; status-change HTTP and service rollback proof |
| ADM-03 | `Support_HTTP_queue_claim_request_info_obeys_role_and_persists_assignment`, plus Decision 1 above | 5 | 0 | Exhaustive template/dossier/action transition matrix not run through HTTP | Admin/User/SuperAdmin: queue, search, templates, self-claim, request-info; persisted assignee/state; no ledger changes |
| ADM-04 | `Admin_notifications_and_maintenance_HTTP_permissions_and_persisted_changes` | 3 | 0 | Manual broadcast publish/cancel/status not HTTP-exercised | Admin/User/SuperAdmin; all five notification read routes, operational draft persistence and rejected SuperAdmin target; no money changes |
| ADM-05 | Same combined notification/maintenance test | 3 | 0 | Emergency/extend/reschedule/review/reminders/close not HTTP-exercised; deployment safety not configured or verified | Schedule/cancel authorization and persisted window/cancellation; existing safety adapter remains fail-closed |
| Email Isolation | `EmailDeliveryModeTests` (22); `TargetedEmailDispatchTests` (29) | 51 | 0 | Live SMTP/push not run | Testing forces Pickup; .test/.invalid rejected before network; valid Production SMTP contract tested only against a loopback SMTP fixture |

Rows overlap and must not be summed. Additional successful evidence includes `Legacy_SuperAdmin_HTTP_password_login_is_denied_without_issuing_credentials`, `Task14RoleSessionTests`, and `OpenApiContractTests`.

SQL safety: the effective instance was explicitly `DESKTOP-B5KA3LT\SQLEXPRESS02`, environment `Testing`, integrated authentication. `SqlTestFixtureLifecycle` rejects any other instance and any catalog outside its randomized test-only allowlist; it creates and verifies a catalog ownership marker before cleanup. All SQL runs used `SpendWise_FinancialS05_Test_<32-hex-id>` catalogs, not application connection configuration. HTTP used isolated SQLite, removed background workers and a fake banking gateway. **SQL01 was never connected to, queried, read, written or migrated. No real provider was called.**

Reproduction commands (PowerShell; result files stay outside source):

```powershell
$env:SPENDWISE_SQL_TEST_INSTANCE = 'DESKTOP-B5KA3LT\SQLEXPRESS02'
$env:ASPNETCORE_ENVIRONMENT = 'Testing'
dotnet build backend/SpendWise.slnx -c Release --no-restore
dotnet test backend/tests/SpendWise.Tests/SpendWise.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~CoreAcceptanceHttpTests|FullyQualifiedName~V1FundSqlServerTests|FullyQualifiedName~AdminA2Tests|FullyQualifiedName~AdminA1HttpTests|FullyQualifiedName~AdminA2HttpTests|FullyQualifiedName~AdminA3HttpTests|FullyQualifiedName~Task16_|FullyQualifiedName~V1FundFinanceTests|FullyQualifiedName~V1FundRefundRegressionTests|FullyQualifiedName~V1FixedAllocationTests|FullyQualifiedName~V1Election|FullyQualifiedName~V1Governance|FullyQualifiedName~V1HttpContractTests|FullyQualifiedName~V1LegacyRoleBoundaryTests|FullyQualifiedName~V1PermissionTests|FullyQualifiedName~V1PlatformAccessTests|FullyQualifiedName~EmailDeliveryModeTests|FullyQualifiedName~TargetedEmailDispatchTests'
dotnet test backend/tests/SpendWise.Tests/SpendWise.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~Admin_restrict_lock_unlock_HTTP|FullyQualifiedName~Support_HTTP_queue|FullyQualifiedName~Legacy_SuperAdmin_HTTP|FullyQualifiedName~Task14RoleSessionTests|FullyQualifiedName~OpenApiContractTests'
```

## C. Confirmed bugs fixed

1. **Missing supported Admin status command.** The legacy status route is intentionally closed and no V1 replacement existed (HTTP 404 before fix). Added case-scoped PUT status, explicit Active/Restricted/Locked validation, Admin PIN, ordinary-user/active-assigned-case protection, audit and SecurityAlert. Lock revocation and notifications share the existing transaction. HTTP workflow/negative cases PASS; `Audit_failure_rolls_back_session_refresh_and_account_status` also proves rollback. A controlled condition-inversion mutation failed as expected; original bytes were restored and the final 11-test run passed.
2. **Resolve/cancel race.** SQL02 reproduced Cancelled being overwritten with Pending24h. Resolve read the request without the shared fund lock. Admin resolution now acquires that same fund lock and reloads tracked request state. The SQL tests force overlap, observe the competing SQL lock wait, then verify consistent outcomes, one decision, unchanged balanced ledger and correct effective hold. Before: FAIL in `core-new-before.trx`; after: PASS.
3. **Fixed rejoin blocked after a valid refund.** The contribution-only accounting guard rejected any historical credit, so obligation reconfirmation failed after leave/refund. The new guard keeps ledger reconciliation and requires every prior outflow to be a completed, balanced, matching membership refund before allowing confirmation/contribution. Refund formulas and the activity-closing guard are unchanged. Fixed HTTP rejoin failed before; now PASS, including new contribution and refund replay. `Fixed_reconfirmation_still_rejects_expense_outflows` prevents treating paid expenses as refunds.
4. **UTC coverage configuration rejected in a non-UTC host timezone.** Configuration binding converted an explicit UTC timestamp to Local DateTime; the inactivity evidence service correctly required UTC and therefore returned ACTIVITY_EVIDENCE_BLOCKED. V1 activity options now normalize Local bound timestamps to UTC while retaining the unspecified/absent evidence guard. Real HTTP election timelines failed before and PASS after; no fabricated inactivity-proof adapter was used.

Fixture/contract corrections were kept separate from production diagnosis: accepted friendship links require an AcceptedRequest, the multi-login test needs a fixture-only rate budget, legacy refresh denial is HTTP 403, support claim/request-info return HTTP 200, and OpenAPI's expected route inventory gains the new PUT route. All failures were addressed; no failed test was removed or reported using an older PASS.

## D. Documentation and Admin consistency

- `User.md`, `Admin.md`, `BACKEND-BASELINE.md`, decision log and final acceptance: **UPDATED**. Removed current blanket completion claims, documented the status command and source corrections, retained historical results as historical.
- Runtime OpenAPI automatically exposes the new route/DTO; the route inventory test was updated and passed. Full Postman collection was not expanded or rerun.
- Admin permissions/scopes: **ALREADY CONSISTENT** in the reviewed V1 permission model. SuperAdmin has no platform permission and fails credential/session admission. Disabled legacy assignment/operator-management methods remain unreachable because ManageOrdinaryAdmins is granted to no role; canonical Admin self-claim uses `/support/{caseId}/assign`. No blanket role replacement or new privilege was introduced.
- A1 remains **LOCKED (4/4)**. A2 retains four DONE functions and **A2.3 PARTIAL / POLICY_BLOCKED**; this task does not approve a blanket module-status promotion.

## E. Git and changed files

- Before SHA: `94e0c94725341f02c18be650d968572dbbe3c795`.
- After source SHA: `70ac7694236f9d39afead8dcdab1a33c08f2a9c5` — [source commit](https://github.com/thien230314-bit/Spendwise/commit/70ac7694236f9d39afead8dcdab1a33c08f2a9c5).
- Source commit: `fix: close core acceptance gaps in disputes restrictions and rejoin`; **push PASS** to main after a fresh fetch. This evidence report is a separate documentation commit; its exact SHA is supplied in the final handoff (`git log -1 --format=%H -- backend/docs/CODEX-FINAL-CORE-ACCEPTANCE.md`).
- Changed production files (under backend): `SpendWise.Application/Features/Administration/{AdminA2Service,AdminA2Relations,AdminA5Service}.cs`; `SpendWise.Application/Features/V1Groups/{V1FundFinance,V1FundContributions}.cs`; `SpendWise/Controllers/AdminA2Controller.cs`; `SpendWise/Infrastructure/V1Authorization.cs`.
- Changed tests (under backend/tests/SpendWise.Tests): `CoreAcceptanceData.cs`, `CoreAcceptanceHttpTests.cs`, `CoreDisputeSqlServerTests.cs`, `CoreElectionHttpTests.cs`, `CoreWorkflowsHttpTests.cs`, `AdminA2Tests.cs`, `EmailDeliveryModeTests.cs`, `OpenApiContractTests.cs`, `SpendWiseApiFactory.cs`, `V1FundSqlServerTests.cs`.
- Changed docs: `User.md`, `Admin.md`, `BACKEND-BASELINE.md`, `backend/docs/BACKEND-PRODUCT-DECISIONS-REQUIRED.md`, `backend/docs/BACKEND-V1-FINAL-ACCEPTANCE.md`, this report.
- Reviewed staged diff and scanned every staged blob for private keys, provider tokens, JWTs, embedded connection passwords, personal email and credential literals. Six literal matches in tests were manually classified as synthetic fixture data; no real secret or personal record was found. No local config, result files, logs, pickup mail, database, screenshot, backup or generated artifact was staged.

## F. Final result

**CORE BACKEND PARTIAL**. Gate A's requested SQL dispute cases and the executed representative HTTP workflows pass. This is not a blanket acceptance of every Admin operation. Remaining evidence is listed explicitly in the matrix: Restricted inbound/refund HTTP; new multi-period carry-over HTTP; election tie-break/acceptance-expiry HTTP; additional support, broadcast and maintenance operations. A2.3 retains its owner-controlled baseline status. `IV1MaintenanceSafety` remains unconfigured/fail-closed, so deployment activation cannot be called verified.

Full Newman, full non-SQL, full SQL, live SePay/Gemini/SMTP/push, real bank transactions and production maintenance/migrations were **NOT RUN**. No Frontend/Mobile edit, source ZIP or whole-system audit was performed. External Integrations was not started; real SePay sandbox credentials and payout capability remain unverified.
