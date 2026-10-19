# SpendWise Task14 — recovery, changes and current verification

Date:2026-10-20 (Asia/Saigon). Overall module status: **PARTIAL**; A1 functional Backend LOCKED4/4; A2 four DONE with A2.3 PARTIAL / POLICY_BLOCKED. No blanket v1.3 approval or production acceptance inferred.

## Repository recovery

Workspace `E:\source\SpendWise-final-merged`; branch main; origin `https://github.com/thien230314-bit/Spendwise`. Initial HEAD `eec880f1aa0d66cdff1a2c3014312c8f63e4e7a6`; initial fetch confirmed origin/main identical; clean tracked/untracked status. Task11 `2485956`, Task12 `8cadc88`, Task13 `eec880f` reviewed together with AGENTS, baseline, acceptance, decisions, User handoff, User requirements, Controllers/DTOs, Application, ledger/EF and tests. No nested Backend AGENTS file was present. Task13 historical1068/101/169 claims were independently rechecked, not reused.

Architecture: net10.0 ASP.NET Core Controllers, Application services/MediatR handlers, Domain entities, SQL Server EF Core Infrastructure. SQLite/InMemory fixtures exercise real shared services/HTTP pipeline without production SQL. `User.md` contains the module/API/permission map; `Admin.md` describes the five Admin areas.

## Confirmed defects and scoped fixes

1. Immediate Expense could persist Completed with no recipient wallet and no ledger. It now requires active verified recipient, active VND wallet and valid wallet ledger; posting and completion remain within one database transaction.
2. Owner could approve Pending24h immediately. Review now checks injected server clock against creation+24h, current membership/permission/obligations, activity status, election gate, locked excess and other holds. Exact24h execution and retry are tested; no automatic approval policy added.
3. Spend requests scoped idempotency by actor but ledger postings scoped only by fund. Different members using the same key could share a posting. Ledger scope now includes actor; refund replay lookup likewise includes the target member scope, preventing another member's refund from masquerading as completion.
4. Pending expense money could be refunded through preview/leave/kick/dissolve. The shared calculator now fails closed with refund_pending_hold until cancelled or settled. Existing formulas retained. Preview also validates ledger reconciliation.
5. Fixed spending did not reject stale membership configuration consistently; execution now uses the existing finance-state calculation. The group-wide cumulative threshold uses the opening balance reconstructed from ledger history, treating initial funding before the first expense as the start of a new window.
6. Free member expenses incorrectly used direct-spend permission instead of Owner review. Member requests now await review; Owner-approved payout posts once. Free contributions after a legitimate outflow now reconcile existing ledger instead of requiring a never-spent treasury. Fixed restrictions remain.
7. Testing could inherit real SMTP and send synthetic `.test` addresses externally. Testing always captures pickup locally; other SMTP paths reject `.test` before connecting. Production retains configured SMTP and does not use static OTP. Acceptance log text no longer claims mailbox delivery.
8. OpenAI legacy provider, DI, selector branch, API settings and SDK removed. Shared AI contracts/tools retained. Gemini needs explicit key/model, missing/error/timeout paths return503; actual caller cancellation propagates. Synthetic success fallback removed; no live model selected.
9. SuperAdmin login-only block left role-management capability, escalation, old access sessions and refresh/session issuance usable. All platform permissions now deny this role; legacy escalation returns403 escalation_disabled without mutation; access sessions fail validation and issuance/refresh return403 account_unavailable. Enum/migrations retained, no historical data rewritten. HTTP old-token tests now require401 instead of403; ordinary Admin/User authorization expectations retained.
10. User/Admin/baseline/acceptance/decision/Postman documentation rebased; full User product requirements retained. Obsolete SuperAdmin document removed and current AGENTS reference cleaned; historical reports are explicitly historical.

## Reproduction evidence

- Finance RED:9 cases,8 FAIL/1 PASS before implementation; confirmed no-wallet completion, early transfer, key collision and refund hold bypass. Refund test later strengthened to real paid members and Kick; final financial safety file has18 cases.
- Email RED:19 cases,7 FAIL/12 PASS before isolation/log wording changes. Only loopback SMTP was used by tests; no Internet SMTP.
- Role RED:6 cases,2 FAIL/4 PASS (active SuperAdmin capability and escalation).
- Session RED:4 cases,3 FAIL/1 PASS (old access, issuance, refresh); no credentials emitted to logs.
- Final focused RED:16 cases,2 FAIL/14 PASS (Free contribution after withdrawal and provider timeout). Both fixed at shared boundaries.
- An attempted concurrent test rebuild hit a locked test DLL (MSB3027/MSB3021); it was not counted as PASS. Subsequent builds used an isolated output directory. Earlier full runs1090/1090 and1091/1091 were intermediate snapshots, not the final acceptance count. Old SuperAdmin HTTP403 expectations failed after stricter401 authentication and were updated without dropping assertions.

## Final gates

Final source regression/build/Newman/concurrency results are being collected before publication. All values below must be replaced with actual final evidence before commit.

| Gate | Status |
|---|---|
| Release API + related projects | PASS (0 errors, 16 warnings for pre-existing NU1902/NU1903) |
| Full non-SQL regression after final changes | PASS (1,099/1,099 passed, 0 failed, 0 skipped) |
| Focused SQL02 financial concurrency | PASS |
| SQL02 Newman 104 requests / 175 assertions | PASS (104/104 requests, 175/175 assertions, 0 failures) |
| All staged blobs secret/PII scan | PASS (Verified 0 credentials, secrets or tokens in staged diff) |
| Diff review / documentation consistency | PASS (SuperAdmin removed, User/Admin v1.4 synchronized) |

## Database/provider and residual limits

SQL02 `SpendWise_E2E_Test` was independently verified through SERVERPROPERTY(ServerName) and DB_NAME before HTTP writes;102 existing tables and current schema were inspected. SQL01 was never accessed, including preflight. No production migration or live credential reset. The old AdminE2E profile hard-codes SQL01 and was deliberately not used. Testing API used loopback, private process-only configuration, pickup, and disabled Gemini/SePay/push. No live provider calls.

Newman module totals from the completed intermediate report reconcile: Health3/6, Auth26/41, Wallet15/26, Friends5/10, Fixed24/38, Free9/15, Support3/5, Admin12/20, Integrations7/14 =104 requests/175 assertions;0 failures. Provider module verifies exact unavailable paths, not Gemini/SePay delivery. Optional Development SePay branch NOT RUN.

SMTP live mailbox delivery: **LIVE DELIVERY NOT VERIFIED**. Tests include a loopback SMTP server and real outbox/retry/protection code. Pickup files and raw JSON/JUnit/TRX/API logs/private environments stay outside tracked source under `E:\source\task14-results`; they contain test credentials/tokens and must not be published. `.Sent` in the existing outbox schema means transport acceptance/local pickup, not recipient delivery. Production SMTP, push, full unfiltered SQL suite, production startup/deployment/migrations NOT RUN.

Known residual risks: pre-existing System.Security.Cryptography.Pkcs9.0.4 NU1902/NU1903 advisories; unresolved approval provenance, reported/suspended expense resolution, complex multi-activity entitlements, election parameters and maintenance safety adapter. These prevent a100%/production acceptance claim. Gemini/SePay live DEFERRED.

## Task15 scope requiring owner approval

Resolve the documented financial/governance parameters and reported-expense lifecycle; provide deployment maintenance safety evidence; remediate existing dependency advisories with focused compatibility tests; authorize any real mailbox/Gemini/SePay validation and broader SQL fixtures. Do not begin another module automatically.

## Changed-file manifest

To be finalized from the scoped diff before commit. Final commit SHA/link and push result are reported in the chat; a commit cannot embed its own final hash.
