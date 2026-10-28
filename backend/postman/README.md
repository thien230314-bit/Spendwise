# SpendWise — Postman & Newman E2E Verification Guide

> **Current State (2026-10-27):**  
> **`BACKEND USER DEMO — ACCEPTED & LOCKED`**  
> - Canonical Collection: `SpendWise-User-API.postman_collection.json` (**306 requests, 343/343 assertions PASS, 0 FAIL, exit code 0**).  
> - Verified Database: `SpendWise_E2E_G4` on SQL Server `DESKTOP-B5KA3LT\SQLEXPRESS02`.  
> - Target Host: `http://127.0.0.1:5158`.

---

## 1. Safety Prerequisites & Operating Rules

1. **Database Isolation & Allowlist:**
   - The test suite is strictly bound to SQL02 (`DESKTOP-B5KA3LT\SQLEXPRESS02`) and database `SpendWise_E2E_G4`.
   - **Strictly prohibited:** Connecting to production or any other database instance (e.g. `SQL01`).
2. **Environment Variables & Secrets:**
   - Never commit sensitive connection strings, JWT private keys, real personal data, or payment provider credentials.
   - All tests run against local Kestrel loopback with configured test environment settings.
3. **No Database Shortcut Hacks:**
   - Never use manual SQL `UPDATE` / `INSERT` commands to bypass API business rules, fake bank connection ownership, or force test passes. All test data must flow through the canonical API endpoints.

---

## 2. Running the Full Newman Test Suite

### Step 1: Start the Backend API Server
Ensure the API is built in Release configuration and started with test environment settings:
```powershell
$env:ConnectionStrings__DefaultConnection = "Server=DESKTOP-B5KA3LT\SQLEXPRESS02;Database=SpendWise_E2E_G4;Integrated Security=True;TrustServerCertificate=True;"
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:EmailVerification__StaticOtp = "12345678"
$env:RateLimiting__AuthPermitLimit = "5000"
$env:RateLimiting__FinancialPermitLimit = "5000"
$env:Jwt__Issuer = "SpendWise.API"
$env:Jwt__Audience = "SpendWise.Frontend"
$env:Jwt__Key = "SpendWise_E2E_Test_Secret_Key_At_Least_32_Bytes_Long_1234567890!"

dotnet backend/SpendWise/bin/Release/net10.0/SpendWise.API.dll --urls "http://127.0.0.1:5158"
```
Verify health:
```powershell
curl http://127.0.0.1:5158/health
# Expected: HTTP 200 "Healthy"
```

### Step 2: Prepare Dynamic Environment & Execute Newman
Run Newman CLI using the canonical collection and dynamic runtime environment:
```powershell
npx newman run SpendWise-User-API.postman_collection.json `
  -e SpendWise-Test.runtime.local.json `
  --reporters cli,json `
  --reporter-json-export reports/newman/newman-run-results.json `
  --timeout 180000 `
  --timeout-request 10000
```

### Step 3: Running a Targeted Folder
To run a specific functional folder (e.g., Folder 01 Authentication or Folder 10 Continuous Flows):
```powershell
npx newman run SpendWise-User-API.postman_collection.json `
  -e SpendWise-Test.runtime.local.json `
  --folder "01 - Authentication & Security" `
  --reporters cli
```

---

## 3. Running Biometric / Passkey Automated Test Suites

The SpendWise WebAuthn Level 2 cryptographic biometric authentication suite is executed via dedicated automated Node.js test runners:

### A. Biometric Security & WebAuthn Compliance Suite (27 Tests)
Validates registration, negative inputs, challenge replay, bad rpIdHash, invalid origin, forged signature, authenticator counter rollback detection, and Zero-Knowledge storage:
```powershell
node backend/tests/biometric/run_security_suite.js
# Output report: reports/biometric/biometric-final-security-results.json (27/27 PASS)
```

### B. Biometric Financial Operations Integration Suite (14 Tests)
Validates two-phase cryptographic biometric authorization across all 5 financial operations (Internal Transfer, P2P Transfer, Group Funds, SePay Top-Up intent, SePay Withdrawal balance reservation) and non-regression:
```powershell
node backend/tests/biometric/run_integration_suite.js
# Output report: reports/biometric/biometric-final-integration-results.json (14/14 PASS)
```

---

## 4. SePay Test Mode Sandbox Guidelines

- **Sandbox Host:** `https://userapi-sandbox.sepay.vn/v2`
- **Verified Linked Accounts:**
  - User A (`23838232...`): VPBank TEST `****0001` (`8B5E9450...`).
  - User B (`3A8B3FD7...`): TPBank TEST `****0002` (`E9E1753B...`).
- **Top-Up Simulation:** Sandbox records debit $\rightarrow$ SpendWise credits wallet balance upon transaction verification.
- **Withdrawal Simulation:** SpendWise reserves balance in `SYSTEM:ReservationHold:VND` $\rightarrow$ Sandbox records credit to bank account $\rightarrow$ SpendWise settles to `Completed`. Verified with User B withdrawal of 50,000 VND (TPBank balance: 150,000đ $\rightarrow$ 200,000đ, Wallet: 4,875,000đ $\rightarrow$ 4,825,000đ).

---

## 5. Historical Reference (Task 14 & Task 17 Baseline)

*(The following section is retained for audit traceability and historical reference.)*

- Task 17 Core Baseline: 141 requests / 227 assertions / 10 folders, documented in `backend/docs/CODEX-TASK17-CORE-CLOSURE.md`.
- Task 16.2 / Task G5 Admin Baseline: 141 requests / 227 assertions across ADM-01 through ADM-05 on `SpendWise_E2E_G4`, documented in `backend/docs/ANTIGRAVITY-G5-FINAL-BACKEND-SECURITY-ACCEPTANCE.md`.
- The historical Task 14 suite verified initial user registration and basic ledger postings. All modern verification is governed by the 306-request `SpendWise-User-API.postman_collection.json` and the dedicated Biometric suites.
