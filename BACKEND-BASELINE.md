# SpendWise V1.0 baseline — PARTIAL

Public source snapshot through Admin A3 final closure, 2026-10-19. Publication is not production approval.

- Admin A1.1–A1.4: DONE, functional Backend LOCKED per local checkpoint `V1-MODULE-LOCK-ADMIN-A1-20261019-113811`.
- Admin A2.1, A2.2, A2.4, A2.5: DONE per local checkpoint `V1-MODULE-LOCK-ADMIN-A2-20261019-120919`.
- A2.3: PARTIAL / POLICY_BLOCKED. Emergency restriction duration, verification/evidence, restoration/expiry, notification/escalation and in-flight/refund treatment need owner-approved policy. No arbitrary money controls are enabled.
- Admin A3.1–A3.8: 8/8 DONE, functional Backend LOCKED for scoped read-only investigation (see `backend/docs/ADMIN-A3-CONTRACT.md`).
- Admin A4.1–A4.6: 6/6 DONE, functional Backend LOCKED with targeted correctness repair for currency check, business binding verification, and transaction listing (see `backend/docs/ADMIN-A4-CONTRACT.md`).
- Admin A5.1–A5.7: 7/7 DONE, functional Backend LOCKED for case workflow and support operations (see `backend/docs/ADMIN-A5-CONTRACT.md`).
- Backend V1.0 overall remains PARTIAL. A6 has not been started by this closure. Functional A5 lock is not production SQL/concurrency or User Finance execution approval.
- Historical A2 evidence: 146 related regression tests PASS (15 A2, 14 A1, 117 related); 4 SQL02 tests PASS; Release build 0 errors / 4 existing Pkcs advisory warnings. Full unfiltered suite and live UI E2E were NOT RUN for that closure. These are historical results, not a claim that every current consumer passes.
- A1 additive migration is source-only and was not applied to the application database. Do not start Development against a real database simply to review source: startup may perform migrations/seeding. AdminE2E has explicit startup guards, but must also be separately configured and authorized.
- Canonical Admin/SuperAdmin are not automatically inferred from legacy SystemAdmin. Legacy A2 HTTP management routes are deliberately denied; use scoped V1 APIs with current session, assigned case and purpose. No real account is promoted or reset here.

## Configuration / publication boundary

Current A3 evidence (2026-10-19): **162 PASS / 0 FAIL / 0 SKIP**, including **54 A3**, **29 A1/A2** and **79 related/OpenAPI** cases. Release build: **0 errors / 4 existing dependency warnings**. SQL02: **NOT RUN**; preflight available RAM 1,375,384 KB was below the unchanged 1,500,000 KB safety gate; 0 owned test catalogs remained. Real relational SQLite and HTTP pipeline tests are the recorded alternative, not SQL Server production proof. Unfiltered full suite and live providers were NOT RUN. No migration/model change, SQL01 connection/mutation, Frontend/Mobile edit or next module was performed.

Local Development/AdminE2E settings, `.env` files, User Secrets, checkpoints, database/backup/ZIP/log/test output, generated web output and sensitive historical operational runbooks are intentionally not tracked. Existing local files remain intact. Supply authorized configuration privately; never copy actual SQL/SMTP/API/JWT secrets into source.

The root `.gitignore` preserves the remote VisualStudio rules and adds .NET/React/Expo/privacy exclusions. The nested mobile repository's metadata/history is not imported: mobile source files are published as ordinary files, not a submodule. Its local `.git` is retained untouched.

Operational recovery's email target is process-local `SPENDWISE_RECOVERY_EMAIL`, with a non-deliverable synthetic default for tests. This public configuration adaptation does not execute a recovery or change credentials.
