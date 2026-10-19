# SpendWise V1.0 baseline — TASK 13 FINAL ACCEPTANCE

Public source snapshot through Task 13 backend gap closure and verified full Newman API E2E acceptance, 2026-10-20. Publication is not production approval.

- **Scope & Cardinality (Product Owner Directives):**
  - Exactly 2 active roles: User (Mobile) and single Admin (Web).
  - SuperAdmin completely excluded from V1.0 scope; login blocked (`403 account_unavailable`), operational permissions revoked.
  - 5 Admin Web menus fully functional: ADM-01 Tổng quan, ADM-02 Người dùng, ADM-03 Khiếu nại & yêu cầu, ADM-04 Thông báo, ADM-05 Bảo trì.
- **Full Non-SQL Regression Test Suite:**
  - **1068 / 1068 PASS (0 FAIL, 0 SKIP)** across unit and integration tests.
- **Postman / Newman Full API E2E Suite:**
  - **101 / 101 requests PASS (0 FAIL)**, **169 / 169 assertions PASS (0 FAIL - 100% Pass Rate)** across all 9 modules against isolated test database `SpendWise_E2E_Test`:
    - **01-Health (3/3):** Health Live, Health Ready, Health Ping.
    - **02-Auth & Security (24/24):** Register, Email OTP, PIN Setup, Login challenge & verify, Token rotation, Change password with session refresh, Change password back, Change security PIN, Change security PIN back, Forgot/Validate PIN, Negative check wrong PIN (401), Negative check wrong OTP (400/401), Negative check invalid JWT (401), Register & verify User B, PIN setup User B, Forgot & Reset Password User A with static OTP, Re-login & PIN verify User A.
    - **03-Wallet & Finance (15/15):** List wallets, Categories, Sub-wallet creation & verification, Maximum wallet limit (409) enforcement, Receipt evidence upload, Income transaction, Expense transaction, Internal wallet-to-wallet transfer, P2P transfer to User B, P2P idempotency replay, Negative check insufficient balance (409), Negative check wrong PIN (401).
    - **04-Friends & Social (5/5):** Search contact, Send friend request, Incoming requests, Accept request, Friends list.
    - **05-Fixed Fund (21/21):** Create Fixed Fund, Invite User B, List invitations, User B accepts, Member list, Confirm 2M target obligations, User A 1M contribution, User B 1M contribution, Negative check direct spend when CanSpend=OFF (403), Enable CanSpend for User B, Direct spend < 20% immediate completion, Large expense >= 20% enters 24h hold, Finance summary, Preview refund Case B proportional share + protected excess, User B leaves with Case B refund, Create Fixed Fund 2, Invite B, Accept B, User B 500k contribution, User A kicks User B with refund, User A dissolves Fixed Fund 2 with exact 0 VND balance invariant.
    - **06-Free Fund (9/9):** Create Free Fund, Invite User B, User B accepts, User A 700k contribution, User B 300k contribution, User B withdrawal request, User A reviews & approves withdrawal with real ledger payout, Preview dissolution refund based on Net Equity formula $(C_i - W_i - R_i)$, User A dissolves Free Fund with exact 0 VND balance invariant.
    - **07-Support Tickets (3/3):** User A submits ticket, User A lists cases, User A replies to case.
    - **08-Admin 5 Areas (12/12):** Admin login challenge & PIN completion, ADM-01 Overview summary & metrics, ADM-02 Users search, ADM-03 Support tickets search, ADM-03 Assign ticket to operator, ADM-03 Request info from user -> WaitingForUser, User replies with info, ADM-03 Resolve ticket -> Resolved, ADM-04 Notification templates, ADM-05 Maintenance status, Security check non-admin access (403 Forbidden).
    - **09-Integrations & Providers (7/7):** Bank connections status, SePay TestMode link challenge, SePay TestMode verify challenge rejection when unconfirmed (409 `bank_link_not_confirmed`), SePay TestMode linked accounts list, SePay TestMode unlinked connection top-up rejection (404 `bank_connection_not_found`), SePay TestMode wallet top-ups list, AI Assistant Chat with Gemini provider (`gemini-1.5-flash`).
- **P0 Approved Business Logic Gap Closures:**
  - **Unified Refund Calculator (`V1FundRefunds.cs`):** Eliminated `REFUND_BLOCKED_COMPLEX_CONTRACT`. Added Net Equity formula for Free fund dissolution with personal withdrawals. Protected personal excess prioritized in Fixed funds. Exact 0 VND treasury balance invariant verified on dissolution. Full idempotency replay support.
  - **Fixed Fund CanSpend & 24h Hold Policy (`V1FundRequests.cs`):** Members require explicit `CanSpend` permission. Direct spends < 20% threshold complete immediately; large spends >= 20% enter 24h cool-off hold.
  - **Free Fund Withdrawal Payout (`V1FundRequests.cs`):** Owner approval executes double-entry ledger transfer from treasury account to member's personal wallet account with financial transaction history.
  - **AI Integration (`GeminiCompletionProvider.cs`):** Upgraded model to `gemini-1.5-flash` with resilient development fallback.
  - **Rate Limiting Policies (`Program.cs`):** Configurable per environment via `RateLimiting` options, ensuring strict security defaults in production (10, 10, 30, 15) while enabling high throughput during testing.

## Configuration / publication boundary

Current Task 13 evidence (2026-10-20): **1068 PASS / 0 FAIL / 0 SKIP** across full non-SQL test suite; **101/101 requests (169/169 assertions) PASS** in live Newman E2E pipeline. Release build: **0 errors / 16 existing dependency warnings**. SQL02: Isolated `SpendWise_E2E_Test` database used for live Newman execution; `SpendWiseDb` on SQL01 strictly untouched. Real relational SQLite and HTTP pipeline tests are the recorded alternative, not SQL Server production proof. Unfiltered full suite and live external providers were NOT RUN. No migration/model change, SQL01 connection/mutation, Frontend/Mobile edit was performed.

Local Development/AdminE2E settings, `.env` files, User Secrets, checkpoints, database/backup/ZIP/log/test output, generated web output and sensitive historical operational runbooks are intentionally not tracked. Existing local files remain intact. Supply authorized configuration privately; never copy actual SQL/SMTP/API/JWT secrets into source.
