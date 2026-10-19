# SpendWise V1.0 baseline — PARTIAL

Public source snapshot after Admin A2, 2026-10-19. Publication is not production approval.

- Admin A1.1–A1.4: DONE, functional Backend LOCKED per local checkpoint `V1-MODULE-LOCK-ADMIN-A1-20261019-113811`.
- Admin A2.1, A2.2, A2.4, A2.5: DONE per local checkpoint `V1-MODULE-LOCK-ADMIN-A2-20261019-120919`.
- A2.3: PARTIAL / POLICY_BLOCKED. Emergency restriction duration, verification/evidence, restoration/expiry, notification/escalation and in-flight/refund treatment need owner-approved policy. No arbitrary money controls are enabled.
- Backend V1.0 overall: PARTIAL. A3 implementation was subsequently approved; its current scoped investigation readers are PARTIAL / NOT LOCKED (see `backend/docs/ADMIN-A3-CONTRACT.md`). Later modules have not been started.
- Latest saved module evidence: 146 related regression tests PASS (15 A2, 14 A1, 117 related); 4 SQL02 tests PASS; Release build 0 errors / 4 existing Pkcs advisory warnings. Full unfiltered suite and live UI E2E were NOT RUN for that closure. These are historical results, not a claim that every current consumer passes.
- A1 additive migration is source-only and was not applied to the application database. Do not start Development against a real database simply to review source: startup may perform migrations/seeding. AdminE2E has explicit startup guards, but must also be separately configured and authorized.
- Canonical Admin/SuperAdmin are not automatically inferred from legacy SystemAdmin. Legacy A2 HTTP management routes are deliberately denied; use scoped V1 APIs with current session, assigned case and purpose. No real account is promoted or reset here.

## Configuration / publication boundary

Current A3 evidence (2026-10-19): 91 related tests PASS / 0 FAIL / 0 SKIP, including 22 A3 and 29 A1/A2 cases; Release build 0 errors / 4 existing dependency warnings. SQL02 preflight did not meet the existing RAM gate, so SQL concurrency tests were NOT RUN. A3.1/.2/.3/.6 are DONE; .4/.5/.7/.8 remain PARTIAL, with exact reader/coverage gaps in the A3 contract. No A3 lock, migration, live SQL write, Frontend/Mobile edit or next module was performed.

Local Development/AdminE2E settings, `.env` files, User Secrets, checkpoints, database/backup/ZIP/log/test output, generated web output and sensitive historical operational runbooks are intentionally not tracked. Existing local files remain intact. Supply authorized configuration privately; never copy actual SQL/SMTP/API/JWT secrets into source.

The root `.gitignore` preserves the remote VisualStudio rules and adds .NET/React/Expo/privacy exclusions. The nested mobile repository's metadata/history is not imported: mobile source files are published as ordinary files, not a submodule. Its local `.git` is retained untouched.

Operational recovery's email target is process-local `SPENDWISE_RECOVERY_EMAIL`, with a non-deliverable synthetic default for tests. This public configuration adaptation does not execute a recovery or change credentials.
