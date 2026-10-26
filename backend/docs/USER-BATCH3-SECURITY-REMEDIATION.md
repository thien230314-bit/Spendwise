# SpendWise User Backend — Batch 3 Security Remediation Report

**Date:** 2026-10-26  
**Scope:** Remediation of Credential and Token Leak from Batch 2 Postman Environment Files  
**Target Database:** `DESKTOP-B5KA3LT\SQLEXPRESS02` / `SpendWise_E2E_G4`  
**Target API:** Kestrel Test Instance (`http://127.0.0.1:5158`)  
**Status:** **FULLY REMEDIATED & VERIFIED PASS**

---

## 1. Problem Statement & Incident Description

During the Batch 2 execution, `seed_acceptance_fixtures.ps1` automatically updated Postman environment files upon test initialization. Because `SpendWise-Test.postman_environment.json` and `backend/postman/SpendWise-Test.postman_environment.json` were tracked in git, two active JWTs and one static cleartext PIN reset token were committed into git history:
- `fixture_user_token`: Active JWT for User `E2E00000-0000-0000-0000-000000000001` (`fixture_user@spendwise.test`), session ID `b9dde3e8-f69d-4e5b-ace2-c202ff3a347b`.
- `fixture_member_token`: Active JWT for User `E2E00000-0000-0000-0000-00000000000D` (`fixture_member@spendwise.test`), session ID `d36e9f57-daa4-4a6c-8cf3-089fcd989929`.
- `fixture_pin_reset_token`: Static cleartext PIN reset token `"E2E_PIN_RESET_SECRET_TOKEN_[REDACTED]"` (Id `E2E00000-0000-0000-0000-00000000000B`) with an excessive 2-day expiration.

---

## 2. Immediate Database Revocation Execution

All exposed tokens and associated active sessions were immediately revoked in the test database (`SpendWise_E2E_G4`):

1. **User Sessions Revocation:**
   ```sql
   UPDATE UserSessions
   SET RevokedAt = GETUTCDATE()
   WHERE Id IN (
       'b9dde3e8-f69d-4e5b-ace2-c202ff3a347b',
       'd36e9f57-daa4-4a6c-8cf3-089fcd989929'
   ) OR UserId IN (
       'E2E00000-0000-0000-0000-000000000001',
       'E2E00000-0000-0000-0000-00000000000D'
   );
   ```

2. **PIN Reset Tokens Revocation:**
   ```sql
   UPDATE SecurityPinResetTokens
   SET RevokedAt = GETUTCDATE()
   WHERE Id = 'E2E00000-0000-0000-0000-00000000000B'
      OR TokenHash = 'A3DBF5E3071378378E56B0AE9DCD90483A14DF27A8A36687DBFE92F787265FEE';
   ```

3. **HTTP 401 Rejection Verification:**
   HTTP requests were sent to the running Kestrel server (`http://127.0.0.1:5158`) with the exposed tokens:
   - `GET /api/users/me` with `fixture_user_token` -> **HTTP 401 Unauthorized** (CONFIRMED).
   - `GET /api/users/me` with `fixture_member_token` -> **HTTP 401 Unauthorized** (CONFIRMED).
   - `POST /api/auth/pin/reset` with static `"E2E_PIN_RESET_SECRET_TOKEN_[REDACTED]"` -> **HTTP 401 Unauthorized (`pin_reset_token_invalid`)** (CONFIRMED).

---

## 3. Git Repository & Tracked File Sanitization

1. **Tracked Environment Sanitization:**
   In both `SpendWise-Test.postman_environment.json` and `backend/postman/SpendWise-Test.postman_environment.json`:
   - `fixture_pin_reset_token`: `""`
   - `fixture_user_token`: `""`
   - `fixture_member_token`: `""`
   - `core_owner_token`: `""`
   - `core_member_token`: `""`
   All runtime secrets have been completely stripped and replaced with empty strings.

2. **Seeder Script Refactoring (`backend/scripts/seed_acceptance_fixtures.ps1`):**
   - **Zero Secrets to Tracked Files:** The seeder script was modified so that it will never write active tokens to tracked git files.
   - **Gitignored Runtime Environment Generation:** Active runtime tokens and variables are strictly written to `SpendWise-Test.runtime.local.json` and `backend/postman/SpendWise-Test.runtime.local.json`.
   - **Gitignore Protection:** Both files match the `.gitignore` pattern `*.local.json` and are untracked and ignored by git.
   - **Dynamic PIN Reset Token Generation:** Replaced hardcoded string with cryptographically secure random token generation:
     ```powershell
     $tokenBytes = New-Object byte[] 48
     [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($tokenBytes)
     $rawToken = [System.Convert]::ToBase64String($tokenBytes)
     ```
   - **Strict 15-Minute Expiration:** Seeded token expiration was aligned strictly to the application design lifetime:
     ```sql
     DATEADD(minute, 15, GETUTCDATE())
     ```

---

## 4. Secret Scan Results

Pre-commit automated secret scanning was performed across all modified files and git diffs:
- Scanning for JWT patterns (`eyJ...`): **0 matches**
- Scanning for hardcoded tokens: **0 matches**
- Scanning for private keys / certificates: **0 matches**
- Scan Status: **PASS (Zero Secrets Detected)**

---

## 5. Verification Gate Status

| Gate Requirement | Status | Evidence |
|---|---|---|
| Revoke exposed tokens in DB | **PASS** | `UserSessions` & `SecurityPinResetTokens` revoked in `SpendWise_E2E_G4` |
| Sanitize tracked Postman environments | **PASS** | Empty token values `""` in both environment files |
| Dynamic random token generation per run | **PASS** | `seed_acceptance_fixtures.ps1` uses `RandomNumberGenerator` |
| 15-minute expiration policy adherence | **PASS** | `DATEADD(minute, 15, GETUTCDATE())` matches `SecurityPinResetSupport` |
| Runtime tokens written only to `.local.json` | **PASS** | Tested and verified ignored by `.gitignore` |
| HTTP 401 rejection proof for leaked tokens | **PASS** | Verified via `scratch/verify_revocation.py` |
| Staged secret scan | **PASS** | `scratch/scan_git_diff.py` returns PASS |
