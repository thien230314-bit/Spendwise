# SpendWise V1.0 — Admin backend contract (Task 16.1)

Updated 2026-10-20 from current Controllers, Application services, role tests and the Task 16.1 Product Owner instruction. Overall backend status: COMPLETE for V1.0 scope with 4 approved product decisions implemented. Test results are recorded in `backend/docs/TASK16.1-VERIFICATION.md`.

## Roles and access

V1.0 has User and exactly one active system Admin. The Admin manages five areas below. A fund Owner is a User with authority inside that fund, never a platform Admin. There is no SuperAdmin product, login, escalation workflow or role-management screen.

Legacy enum values/migrations remain for data compatibility. SuperAdmin login returns403 `account_unavailable`; platform permissions deny that role. Legacy role-assignment endpoints do not grant a supported onboarding path. Do not create another Admin or change historical records to satisfy tests. Existing role cardinality checks are retained; production account provisioning is an authorized operational procedure, not registration.

Admin APIs validate current database role, account status, verified email, password-change requirement and active session. Sensitive case reads require an active assigned case linked to the target User, a reason and audit evidence; listing users does not authorize global access to their financial details. PIN applies where specified by the request DTO. Failed authorization must not reveal another User's records.

## ADM-01 — Dashboard

Canonical root: GET `/api/v1/admin/overview` with optional `date`. Related GET routes: `/tasks`, `/sources/{metric}`, `/alerts`, `/activity`. Legacy `/api/admin/dashboard` and `/api/admin/reports` are compatibility projections.

Reports aggregate Users, group/fund creation, cases and ledger status/volume from persisted data. Vietnam business-day boundaries are explicit. Mirrored financial histories must not double-count money. Drill-down retains assigned-case scope; aggregate totals do not grant unrestricted detail reads.

Status: A1 remains functional Backend LOCKED (4/4), subject to actual regression results. Task14 does not promote the baseline.

## ADM-02 — User Management

POST `/api/v1/admin/users/search` provides paginated ordinary-user search. Scoped POST routes under `/api/v1/admin/support/{caseId}/users/{userId}`: `/profile`, `/sessions`, `/restrictions`, `/revoke-sessions`, `/groups`, `/friends`, `/history`.

Read or restrict only within the authorized active support case. Session revocation prevents reuse; account restrictions do not transfer fund ownership or erase financial entitlements. No password/PIN/OTP/hash/token is exposed to Admin. No arbitrary ledger balance editing.

Status: A2 User Management IMPLEMENTED & VERIFIED.
- Level 1 — Financial Restriction (`UserStatus.Restricted`): blocks outbound debit transfers/expenses/fund contributions/fund requests; preserves login, token refresh, read-only balance/profile, support intake, and inbound credits/refunds.
- Level 2 — Account Lock (`UserStatus.Locked`): immediate session revocation, 403 `account_unavailable` on login, token refresh blocked. Reversible by Admin.

## ADM-03 — Support / Disputes

User intake remains `/api/cases/support` and `/api/cases/disputes`. Admin canonical root `/api/v1/admin/support`: POST `/tickets/search`, `/assigned`, `/{caseId}/assign`, `/{caseId}/request-info`, `/{caseId}/dossier`, `/{caseId}/resolve`, `/templates`, `/{caseId}/reply-template`, and dispute resolution route `/disputes/{requestId}/resolve`. Additional case detail/accounts/actions and assignment routes live in `V1AdministrationController`.

State transitions include New, Assigned/InProgress, WaitingForUser, Resolved/Rejected/Closed as allowed by the implementation. User replies and case history persist. Resolve/reject requires the current operator's valid scope and rationale; closed cases cannot authorize fresh sensitive reads. Transaction/fund investigation APIs remain case-bound and read-only unless a specific approved financial command exists. Support resolution is not a generic payout API.

The legacy `/{caseId}/escalate` route is retained only as a disabled compatibility surface; it must not create SuperAdmin work. Do not restore old escalation menus or use old Admin requirements to expand scope.

Status: IMPLEMENTED support and dispute resolution workflow with related regression coverage.
- Dispute Resolution endpoint: POST `/api/v1/admin/support/disputes/{requestId}/resolve`.
  - Valid report (`!IsRejected`): Cancels suspended expense (`Cancelled`), releases hold to fund available balance, marks SupportCase `Resolved`, records audit log and notifies fund members.
  - Invalid report (`IsRejected`): Unsuspends expense back to `Pending24h` retaining remaining timer, marks SupportCase `Rejected`, records audit log and notifies reporter.

## ADM-04 — Notifications

POST `/api/v1/admin/notifications/search`, `/delivery-history`, `/failures`, `/templates`, `/maintenance-overview`. Case and recipient scoping applies to notification/detail queries. Templates and delivery status do not grant read access to raw OTP/recovery content.

Approved system broadcast routes: POST `/api/v1/system/broadcasts`, `/{id}/publish`, `/{id}/cancel`, `/{id}/status`, protected by maintenance permission and PIN/request safeguards. Existing drafts, recipient snapshots and publication state are preserved.

Status: IMPLEMENTED internal notifications/outbox; SMTP acceptance and local pickup are not proof of mailbox delivery. LIVE DELIVERY NOT VERIFIED. External push delivery LIVE NOT VERIFIED.

## ADM-05 — Maintenance

Canonical root `/api/v1/system/maintenance`: GET status; POST create; POST `/{id}/reminders`, `/emergency`, `/{id}/extend`, `/{id}/review`, `/{id}/cancel`, `/{id}/close`; PUT `/{id}`. Read permissions and write permissions follow controller policies and current session checks. User status is also exposed via `/api/system/maintenance`.

Maintenance gates protect money/group operations and preserve committed ledger history. Safety-sensitive activation can remain blocked by the configured `IV1MaintenanceSafety` implementation. Do not bypass a safety adapter or simulate a successful production maintenance exercise.

Status: PARTIAL where deployment safety evidence/configuration is unavailable; production activation NOT RUN.

## Verification and operating boundary

Task14 covers Backend only. No Frontend/Mobile work, SQL01 access, production migration, credential reset or live Gemini/SePay call is authorized by this contract. Release/non-SQL/Newman evidence and remaining Task15 work are in `backend/docs/TASK14-VERIFICATION.md`. Legacy historical reports remain evidence of their own task only.
