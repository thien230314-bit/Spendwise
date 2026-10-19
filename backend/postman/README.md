# SpendWise Task14 — Postman / Newman core verification

Current offline-provider suite:104 requests /175 assertions across9 modules. The historical Task13 report said101/169; its per-module counts were inconsistent. Only current JSON/JUnit evidence establishes executed totals. `backend/docs/TASK14-VERIFICATION.md` records actual results.

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
| Fixed funds |24|38|
| Free funds |9|15|
| Support |3|5|
| Admin five areas |12|20|
| Integrations unavailable paths |7|14|
| Total |104|175|

Task14 adds Owner early-review rejection, refund rejection while a hold exists, and explicit hold cancellation before refund. Existing Development SePay TestMode assertions remain behind explicit `test_mode_enabled=true`; do not enable them without separately authorized provider configuration. That optional branch is NOT RUN in Task14. Gemini/SePay live and real mailbox delivery are LIVE NOT VERIFIED / DEFERRED.

The collection source is `generate_postman_collection.py`; regenerate from repository root after editing it. No assertion is dropped to hide a failing financial flow. Unit/SQLite regressions separately verify fake-clock boundary timing, zero money on rejection, valid mature payout/replay, and actor-scoped idempotency.
