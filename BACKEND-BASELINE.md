# SpendWise V1.0 baseline — TASK 12 FINAL ACCEPTANCE

Public source snapshot through Task 12 backend completion and full Newman API E2E acceptance, 2026-10-19. Publication is not production approval.

- Emergency Submission Scope (2026-10-19): SuperAdmin completely removed from V1.0 scope per Product Owner emergency directive. Admin backend simplified to 5 core menus (ADM-01 Tổng quan, ADM-02 Quản lý người dùng, ADM-03 Khiếu nại & yêu cầu, ADM-04 Thông báo, ADM-05 Bảo trì).
- SuperAdmin login blocked (403 account_unavailable); operations permissions revoked for SuperAdmin; single active Admin cardinality strictly enforced across platform.
- Full non-SQL regression test suite: **1064/1064 PASS (0 FAIL, 0 SKIP)** across full test suite.
- Postman / Newman Full API E2E: **63/63 requests PASS (0 FAIL)**, **111/111 assertions PASS (0 FAIL)** across all 9 modules:
  - 01-Health: 2/2 requests PASS
  - 02-Auth & Session: 10/10 requests PASS
  - 03-Wallet & Finance: 9/9 requests PASS
  - 04-Friends & Social: 6/6 requests PASS
  - 05-Fixed Fund (Group): 12/12 requests PASS
  - 06-Free Fund (Group): 10/10 requests PASS
  - 07-Support Tickets & Disputes: 5/5 requests PASS
  - 08-Admin 5 Areas: 6/6 requests PASS
  - 09-Integrations & Webhook: 3/3 requests PASS
- Approved Refund Business Rules (Task 12 Correction): Implemented production code in `V1FundRefunds.cs` for Cases A, B, C, D:
  - Case A (Unspent): 100% refund of unspent contributions on Leave/Kick.
  - Case B (Fixed Fund spent): Proportional share of remaining balance + full refund of locked personal excess.
  - Case C (Free Fund dissolve): Proportional share of treasury balance to active members.
  - Case D (Accounting & Governance): Authoritative calculation, wallet refund transaction type `Refund`, treasury zeroed on dissolve, atomic & idempotent, owner transfer enforcement.
  - Complex Contract Guard: Free fund with prior personal withdrawals guarded with `REFUND_BLOCKED_COMPLEX_CONTRACT`.
- Admin A1.1–A1.4: DONE, functional Backend LOCKED.
- Admin A2.1, A2.2, A2.4, A2.5: DONE; A2.3: PARTIAL / POLICY_BLOCKED (emergency balance/money locks disabled).
- Admin A3.1–A3.8: 8/8 DONE, functional Backend LOCKED.
- Admin A4.1–A4.6: 6/6 DONE, functional Backend LOCKED with unified read model and batch optimization.
- Admin A5.1–A5.7: 7/7 DONE, functional Backend LOCKED (simplified for single Admin direct resolution without SuperAdmin escalation).
- Admin A6.1–A6.4: 4/4 DONE, functional Backend LOCKED with credential masking (ADM-FIX-01), feed sanitization (ADM-FIX-02), exact pagination > 200 (ADM-FIX-03), and synchronized Admin maintenance authority (ADM-FIX-04).
- User Mobile: Full `User.md` v1.3 scope preserved intact, verified across Auth, Wallets, P2P Transfers (`POST /api/transfers/p2p`), Friends, Fixed/Free Groups, Disputes, Email, Gemini AI fallback, and SePay TestMode.
- Documentation: Created `backend/docs/BACKEND-V1-FINAL-ACCEPTANCE.md` and updated `backend/docs/BACKEND-PRODUCT-DECISIONS-REQUIRED.md`.

## Configuration / publication boundary

Current Task 12 evidence (2026-10-19): **1064 PASS / 0 FAIL / 0 SKIP** across full non-SQL test suite; **63/63 requests (111/111 assertions) PASS** in live Newman E2E pipeline. Release build: **0 errors / 16 existing dependency warnings**. SQL02: Isolated `SpendWise_E2E_Test` database used for live Newman execution; `SpendWiseDb` on SQL01 strictly untouched. Real relational SQLite and HTTP pipeline tests are the recorded alternative, not SQL Server production proof. Unfiltered full suite and live external providers were NOT RUN. No migration/model change, SQL01 connection/mutation, Frontend/Mobile edit was performed.

Local Development/AdminE2E settings, `.env` files, User Secrets, checkpoints, database/backup/ZIP/log/test output, generated web output and sensitive historical operational runbooks are intentionally not tracked. Existing local files remain intact. Supply authorized configuration privately; never copy actual SQL/SMTP/API/JWT secrets into source.

The root `.gitignore` preserves the remote VisualStudio rules and adds .NET/React/Expo/privacy exclusions. The nested mobile repository's metadata/history is not imported: mobile source files are published as ordinary files, not a submodule. Its local `.git` is retained untouched.

Operational recovery's email target is process-local `SPENDWISE_RECOVERY_EMAIL`, with a non-deliverable synthetic default for tests. This public configuration adaptation does not execute a recovery or change credentials.
