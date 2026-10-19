# SpendWise V1.0 backend baseline — Task 16

Date: 2026-10-20 (Asia/Saigon). Baseline: `fbdc6f2578be08c844c6c90b6d9bd02778819b55`; main; clean workspace. Tasks 11–15.1 establish implementation history and incremental gap closure.

Overall Backend V1.0: **PARTIAL (A1 LOCKED 4/4; A2 4 DONE, A2.3 PARTIAL / POLICY_BLOCKED)**. There are User and one active Admin operating five Admin areas; SuperAdmin is disabled.

Task 16 confirms full regression and E2E verification across all systems:
- Non-SQL suite: 1,102 passed, 0 failed, 0 skipped.
- SQL Server suite (SQL02): 127 passed, 0 failed, 0 skipped.
- Full Newman E2E suite: 105 requests executed, 177 assertions passed, 0 failed across all 9 modules (`backend/postman/SpendWise-V1-Full-E2E.postman_collection.json`).
- Fund safety, 24h holds, large expense dispute reporting (`Suspended`), balanced ledger postings, and invariant-preserving fund dissolutions are verified.

Read `User.md` for product requirements and API mapping, `Admin.md` for the five-area contract, `backend/docs/BACKEND-PRODUCT-DECISIONS-REQUIRED.md` for deferred policies, and `backend/docs/TASK16-VERIFICATION.md` for actual current evidence.

SQL01 must not be accessed. All integration and Newman runs used isolated fixtures on SQL02 (`DESKTOP-B5KA3LT\SQLEXPRESS02\SpendWise_E2E_Test`). No production migration, real provider call, credential reset, Frontend/Mobile edit or source ZIP is part of this task. Test evidence, pickup files, private environments and logs remain outside tracked source under `E:\source\task14-results`.
