# SpendWise V1.0 baseline — PARTIAL

Public source snapshot through Admin A3 final closure, 2026-10-19. Publication is not production approval.

- Emergency Submission Scope (2026-10-19): SuperAdmin completely removed from V1.0 scope per Product Owner emergency directive. Admin backend simplified to 5 core menus (ADM-01 Tổng quan, ADM-02 Quản lý người dùng, ADM-03 Khiếu nại & yêu cầu, ADM-04 Thông báo, ADM-05 Bảo trì).
- SuperAdmin login blocked (403 account_unavailable); operations permissions revoked for SuperAdmin; single active Admin cardinality strictly enforced across platform.
- Full non-SQL regression test suite: 1040/1040 PASS (0 FAIL, 0 SKIP).
- Admin A1.1–A1.4: DONE, functional Backend LOCKED.
- Admin A2.1, A2.2, A2.4, A2.5: DONE; A2.3: PARTIAL / POLICY_BLOCKED (emergency balance/money locks disabled).
- Admin A3.1–A3.8: 8/8 DONE, functional Backend LOCKED.
- Admin A4.1–A4.6: 6/6 DONE, functional Backend LOCKED with unified read model and batch optimization.
- Admin A5.1–A5.7: 7/7 DONE, functional Backend LOCKED (simplified for single Admin direct resolution without SuperAdmin escalation).
- Admin A6.1–A6.4: 4/4 DONE, functional Backend LOCKED with credential masking and dynamic pagination.
- User Mobile: Full `User.md` v1.3 scope preserved intact.

## Configuration / publication boundary

Current A3 evidence (2026-10-19): **162 PASS / 0 FAIL / 0 SKIP**, including **54 A3**, **29 A1/A2** and **79 related/OpenAPI** cases. Release build: **0 errors / 4 existing dependency warnings**. SQL02: **NOT RUN**; preflight available RAM 1,375,384 KB was below the unchanged 1,500,000 KB safety gate; 0 owned test catalogs remained. Real relational SQLite and HTTP pipeline tests are the recorded alternative, not SQL Server production proof. Unfiltered full suite and live providers were NOT RUN. No migration/model change, SQL01 connection/mutation, Frontend/Mobile edit or next module was performed.

Local Development/AdminE2E settings, `.env` files, User Secrets, checkpoints, database/backup/ZIP/log/test output, generated web output and sensitive historical operational runbooks are intentionally not tracked. Existing local files remain intact. Supply authorized configuration privately; never copy actual SQL/SMTP/API/JWT secrets into source.

The root `.gitignore` preserves the remote VisualStudio rules and adds .NET/React/Expo/privacy exclusions. The nested mobile repository's metadata/history is not imported: mobile source files are published as ordinary files, not a submodule. Its local `.git` is retained untouched.

Operational recovery's email target is process-local `SPENDWISE_RECOVERY_EMAIL`, with a non-deliverable synthetic default for tests. This public configuration adaptation does not execute a recovery or change credentials.
