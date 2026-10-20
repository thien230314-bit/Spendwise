# SpendWise Task14 — Postman / Newman core verification

Task 17 handoff: canonical collection now contains **141 requests / 227 assertions / 10 folders**, including 36 requests / 50 assertions for preferences, restrictions/inbound/refund, carried activity, election, support, broadcast and maintenance. Current execution details and limits: `backend/docs/CODEX-TASK17-CORE-CLOSURE.md`. Full external-provider and full Postman acceptance are assigned to Antigravity; Codex has stopped Newman work under the latest Product Owner instruction.

## Safety prerequisites

1. Never launch the historical AdminE2E profile for this suite: its hard-coded SQL01 target is prohibited by Task14. Do not connect to SQL01 even for a preflight.
2. Use a dedicated Testing process bound to loopback and a privately supplied SQL02 connection targeting the independently verified `SpendWise_E2E_Test` fixture. Verify server/database identity before writes. Do not copy production configuration or run Development seeding/migrations against a protected database.
3. Testing forces email to pickup even if SMTP is inherited. Set `PasswordReset:PickupDirectory` outside tracked source, supply private JWT/OTP pepper values and test-only static OTP. No SMTP credentials are needed; Production ignores the static OTP setting. Pickup/outbox artifacts contain sensitive recovery content.
4. Keep Gemini/SePay/push providers unconfigured/disabled. Set `test_mode_enabled=false` (template default). Development TestMode routes must return404 `sepay_test_mode_unavailable`; the AI request must return503 `AI_PROVIDER_UNAVAILABLE`. These are exact safety assertions, not simulated provider success.
5. Supply existing authorized test Admin credentials through a private environment file. Never reset an account or redirect test emails to a real mailbox just to run the suite. Increase rate-limit values only in the isolated test process.

## Run

Run from repository root after Release build and API readiness. Configure `baseUrl` in your private copy of the public environment template; it must point at your verified loopback fixture, not an arbitrary existing service.

```powershell
npx --no-install newman run backend/postman/SpendWise-V1-Full-E2E.postman_collection.json -e <private-test-environment> --reporters json,junit --reporter-json-export <outside-repo>/newman.json --reporter-junit-export <outside-repo>/newman.xml --timeout 180000 --timeout-request 10000
```

Do not publish private environments, raw reports, API logs, pickup messages or tokens. A terminated/timed-out run is FAIL/NOT COMPLETE. Inspect totals and per-module executions/assertions in JSON and reconcile them with JUnit.

## Coverage

| Module | Requests | Assertions |
|---|---:|---:|
| Health |3|6|
| Auth / PIN / password / sessions |26|41|
| Wallet / finance / P2P / evidence |15|26|
| Friends |5|10|
| Fixed funds |25|40|
| Free funds |9|15|
| Support |3|5|
| Admin five areas |12|20|
| Integrations unavailable paths |7|14|
| Task17 Core closure + preferences |36|50|
| Total |141|227|

Task14 adds Owner early-review rejection, refund rejection while a hold exists, and explicit hold cancellation before refund. Existing Development SePay TestMode assertions remain behind explicit `test_mode_enabled=true`; do not enable them without separately authorized provider configuration. That optional branch is NOT RUN in Task14. Gemini/SePay live and real mailbox delivery are LIVE NOT VERIFIED / DEFERRED.

The collection source is `generate_postman_collection.py`; regenerate from repository root after editing it. No assertion is dropped to hide a failing financial flow. Unit/SQLite regressions separately verify fake-clock boundary timing, zero money on rejection, valid mature payout/replay, and actor-scoped idempotency.

## Owned fixture runner and handoff

`CoreNewmanSqlTests` is explicit opt-in (`SPENDWISE_RUN_CORE_NEWMAN=1`, `SPENDWISE_SQL_TEST_INSTANCE` pinned to SQL02), starts Kestrel on an ephemeral loopback port, creates a new allowlisted randomized catalog with an ownership marker, and cleans only its verified own catalog. `SPENDWISE_NEWMAN_FOLDER` selects only an affected folder; no automatic full rerun. The canonical collection requires the runner's private synthetic fixture variables for folder10. Do not point it at an existing application DB. Ready-approved activity and mature no-vote election are synthetic boundary fixtures; targeted fake-clock HTTP tests separately cover real vote/timeline transitions. This collection is not evidence that every possible workflow or UI is verified.

Runner output stays in `%TEMP%/spendwise-task17`: private environments, tokens, JSON/JUnit and pickup are never committed. SePay/Gemini/push are unavailable/fake; email is local pickup/outbox. Do not use AdminE2E. Antigravity should verify coverage, add missing full lifecycle scenarios in its authorized task, and perform its own full run rather than treating the prior partial-rerun evidence as a new full PASS.
