# SPENDWISE V1.0 — TEST ENVIRONMENT BASELINE

**Document Version:** 1.0  
**Date:** 2026-10-20  
**Scope:** Core Backend Stabilization, Targeted Verification & Safe Test Environment Contracts.

---

## 1. General Principles & Safety Boundaries

- **ABSOLUTE PROHIBITION:** `DESKTOP-B5KA3LT\SQLEXPRESS01\SpendWiseDb` (SQL01) must **NEVER** be accessed, queried, modified, or migrated.
- **AUTHORIZED DATABASES:** All SQL Server operations are restricted exclusively to SQL02 (`DESKTOP-B5KA3LT\SQLEXPRESS02`):
  - Integration Test Runner: `Server=DESKTOP-B5KA3LT\SQLEXPRESS02;Database=SpendWise_Test_Runner;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True`
  - E2E Test Database: `Server=DESKTOP-B5KA3LT\SQLEXPRESS02;Database=SpendWise_E2E_Test;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True`
- **IN-MEMORY / SQLITE TEST FIXTURES:** Non-SQL unit tests use `TargetedCorrectnessFixture` with SQLite in-memory or EF Core in-memory providers.
- **FRONTEND / MOBILE:** Read-only baseline; no source edits permitted.
- **SECRETS HYGIENE:** Never print or commit real passwords, tokens, API keys, or personal credentials.

---

## 2. API Startup & Runtime Environments

### A. Development Mode (API Server on Port 5164)
- **Startup Command:**
  ```powershell
  $env:ASPNETCORE_ENVIRONMENT="Development"
  $env:ConnectionStrings__DefaultConnection="Server=DESKTOP-B5KA3LT\SQLEXPRESS02;Database=SpendWise_E2E_Test;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
  dotnet run --no-build -c Release --project backend/SpendWise/SpendWise.API.csproj --urls http://127.0.0.1:5164
  ```
- **Configuration & Behavioral Rules:**
  - Static OTP enabled: `"EmailVerification:StaticOtp": "12345678"`
  - SePay TestMode enabled: Allows simulating inbound webhook credit and bank-link challenge confirmations without calling external banks.
  - Rate limiting permit limits: Relaxed to 1,000 req/min for seamless automated testing.
  - Seeding: Seeded automatically with 5 standard demo users and 1 system admin (`admin@spendwise.local`).

### B. Testing Environment (Automated Integration / Unit Tests)
- **Environment:** `ASPNETCORE_ENVIRONMENT="Testing"`
- **Behavioral Rules:**
  - Email Delivery: Forces `EmailDeliveryMode.Pickup`. Emails are written locally to temporary pickup directories and NEVER dispatched via real SMTP to the internet.
  - Background Outbox: In-memory or disabled during unit runs.

### C. Production Mode (Reference Contract)
- **Environment:** `ASPNETCORE_ENVIRONMENT="Production"`
- **Rules:**
  - Requires valid SMTP configuration (`Email:Smtp:Host`, `Email:Smtp:From`, `Email:Smtp:Port`).
  - No static OTP; real OTP generated and hashed with short expiry.
  - Rate limiting strictly enforced (10-30 req/min).

---

## 3. Targeted Test Execution Strategy

### A. Non-SQL Unit & Logic Tests
Execute only the relevant targeted test class:
```powershell
# Run single test class:
dotnet test backend/tests/SpendWise.Tests/SpendWise.Tests.csproj --filter "FullyQualifiedName~Task16_2_AdversarialAuditTests" -c Release

# Run entire non-SQL test suite (1,063 tests):
dotnet test backend/tests/SpendWise.Tests/SpendWise.Tests.csproj --filter "Category!=SqlServer" -c Release
```

### B. SQL02 Integration Tests
Execute integration tests specifically against SQL02:
```powershell
dotnet test backend/tests/SpendWise.Tests/SpendWise.Tests.csproj --filter "Category=SqlServer" -c Release
```

### C. Newman E2E Tests (Module Scoped)
Do NOT run all 105+ requests on minor changes. Run folder-targeted Newman runs:
```powershell
# Example: Run only Admin folder
npx newman run backend/postman/SpendWise-V1-Full-E2E.postman_collection.json -e E:\source\task14-results\newman-private.environment.json --folder "08-Admin 5 Areas" --reporters cli

# Example: Run only Fixed Fund folder
npx newman run backend/postman/SpendWise-V1-Full-E2E.postman_collection.json -e E:\source\task14-results\newman-private.environment.json --folder "05-Fixed Fund" --reporters cli
```

---

## 4. Email Test Isolation Policy

1. **Test Domains:** All `@spendwise.test` or `.test` domains are strictly reserved test domains.
2. **SMTP Guard:** `ConfiguredEmailSender.cs` enforces a hard guard throwing `InvalidOperationException("Reserved test recipients cannot be sent through SMTP.")` if any message targeting `.test` attempts to leave via SMTP.
3. **Outbox Dispatcher Guard:** `EmailOutboxDispatcher.cs` skips external network dispatch for test domains in development/testing mode and diverts to local pickup.
4. **Internet Protection:** Under NO circumstance will test or synthetic user emails be routed to external Google/Gmail/Outlook mail servers.

---

## 5. SePay Provider Capability Baseline

- **Bank Hub (Incoming / Account Link):** Supported via Hosted Link and webhook events.
- **Wallet Top-Up (Incoming Funds):** Supported via Webhook IPN (`transfer_type=in`) and reconciliation.
- **Wallet Withdrawal (Payout / Outbound):**
  - SePay Bank Hub does NOT provide a generic automated payout/disbursement API for retail/individual accounts. Payouts require corporate enterprise bank direct integration (e.g. TPBank/Sacombank/VietinBank VA Corporate) with distinct contracts and API keys.
  - In SpendWise V1.0, withdrawal is managed via verified internal ledger reservation + admin approval workflow; live external bank payout is categorized as DEFERRED external provider integration.
