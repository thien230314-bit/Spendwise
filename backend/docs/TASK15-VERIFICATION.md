# SpendWise Task 15 & 15.1 — Core Backend Gap Closure & Verification Report

Date: 2026-10-20 (Asia/Saigon).
Baseline: `f17d9d22cedfe58af95e0c4787ac13966900837c`

## Executive Summary

Task 15 and 15.1 continue directly from Task 14 to close core backend gaps and fix defects:
1. **Large Expense Reporting & Suspension (User.md 5.7 point 9):**
   - Implemented `POST /api/v1/funds/requests/{id}/report` enabling any fund member to report a pending 24h hold (`Pending24h`).
   - Reporting transitions the request to `Suspended`, stores the report reason, and automatically registers a high-priority `SupportCase` in category `V1FundDispute` with full case history tracking.
   - While `Suspended`, the request reserves fund balance and fails-closed against early execution or liquidation/refunds (`refund_pending_hold`).
   - Requester or fund owner can cancel the suspended request via `POST /api/v1/funds/requests/{id}/cancel`, which releases the hold and restores refundability.
2. **Defect Fix — ExecutionAvailability Mapping (Task 15.1):**
   - Fixed `MapRequest` in `V1FundRequests.cs`: previously `Suspended` requests fell into the default `AwaitingReview` availability. Now correctly returns `"Suspended"` execution availability so API consumers and mobile clients do not treat suspended holds as actionable for review.
3. **OpenAPI & Postman E2E Test Suite Update:**
   - Updated `OpenApiContractTests` to assert the canonical presence of `/api/v1/funds/requests/{id}/report` (289 paths).
   - Updated `generate_postman_collection.py` and collection with 105 requests across 9 modules (187 pm.test assertions) covering large expense hold reporting, suspension hold verification, and cancellation.
4. **Automated Verification:**
   - 1,102/1,102 non-SQL regression tests pass (`dotnet test backend/tests/SpendWise.Tests/SpendWise.Tests.csproj --filter "FullyQualifiedName!~SqlServer" -c Release`).
   - 21/21 `Task14FundSafetyTests` pass (including 3 tests covering reporting, suspension, dispute support ticket generation, hold cancellation, anti-enumeration, and idempotency).

## Verification Gates

| Gate | Status | Evidence |
|---|---|---|
| Release Build | PASS | 0 Errors, 16 warnings (known `System.Security.Cryptography.Pkcs 9.0.4` NU1902/NU1903) |
| Non-SQL Regression Suite | PASS | 1,102 Passed, 0 Failed, 0 Skipped |
| Fund Safety Suite | PASS | 21 Passed, 0 Failed (includes report/suspend/cancel lifecycle & idempotency) |
| Postman Collection Updated | PASS | 105 requests / 187 assertions generated via script |
| Secret Scan | PASS | Staged diff scanned; 0 credentials, secrets or tokens |
| SQL01 DB Isolation | PASS | Zero connections to `DESKTOP-B5KA3LT\SQLEXPRESS01\SpendWiseDb` |
