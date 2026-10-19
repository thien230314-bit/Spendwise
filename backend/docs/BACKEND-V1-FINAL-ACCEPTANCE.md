# SpendWise V1.0 — current backend acceptance (Task 16)

2026-10-20. **PARTIAL; not production acceptance and not 100% complete.** A1 functional Backend LOCKED (4/4); A2 four DONE with A2.3 PARTIAL / POLICY_BLOCKED.

The current evidence record is `TASK16-VERIFICATION.md`; it records actual results across 1,102 non-SQL tests (PASS), 127 SQL Server integration tests on SQL02 (PASS), and 105 requests / 177 assertions on Newman E2E (PASS). Do not substitute historical tests or SMTP acceptance for current evidence.

## Current implementation boundary

- A1 functional Backend LOCKED (4/4); A2 four DONE with A2.3 PARTIAL / POLICY_BLOCKED.
- User auth/OTP/PIN/sessions, wallets, transactions/P2P, planning, friends, Fixed/Free funds, support and notifications have implementations. Coverage is the current non-SQL and HTTP evidence, not a claim that every product decision is closed.
- A fund payout cannot finish without a real balanced internal ledger posting. Recipient account checks, server hold timing, reserved balance, stale obligations, authorization and scoped replay are enforced in the shared service.
- Existing Fixed protected-excess/obligation-share and Free net-equity refund calculations are retained from Task12/13. The presence of those formulas does not establish complete approval of every v1.3 or complex carryover scenario.
- Refund preview/execution is blocked while Pending24h/Suspended expenses reserve funds; cancel or settle first. No new hold/refund formula is invented.
- Owner approval remains explicit; no automatic approval or scheduled payout policy is introduced.
- Exactly one system Admin operates ADM-01 Dashboard, ADM-02 Users, ADM-03 Support/Disputes, ADM-04 Notifications and ADM-05 Maintenance. Legacy enum/migrations remain without granting SuperAdmin privileges.
- Email Testing is isolated local pickup, Production uses configured SMTP for real recipients; reserved `.test` recipients never reach SMTP. Live mailbox delivery is **LIVE NOT VERIFIED**.
- OpenAI legacy integration removed; shared AI contracts and Gemini tools remain. Unconfigured/erroring Gemini returns503 and never impersonates a successful online answer.
- Gemini and SePay live integration **DEFERRED / LIVE NOT VERIFIED**. Testing SePay TestMode routes are denied; no provider call or fake bank settlement is required for core acceptance.

## Remaining release restrictions

Unresolved governance timing/approval provenance, suspended-expense resolution, multi-activity financial entitlements and deployment maintenance safety remain BLOCKED as documented in the decision log. SQL Server concurrency fixtures outside the verified E2E run are NOT RUN unless explicitly listed. Build dependency advisories are recorded as residual risks rather than hidden. Production readiness requires these decisions/evidence; publication of a partial fix does not approve production rollout.
