# SPENDWISE USER FRONTEND API ALIGNMENT REPORT

> **Status:** REVIEWED & ACCURATELY ALIGNED  
> **Target Frontend:** Google AI Studio / SpendWise Frontend Integration  
> **Backend Baseline:** ASP.NET Core 10, EF Core, SQL Server  
> **Database Isolation Guard:** Tested exclusively on isolated SQLite in-memory fixtures. ZERO access or modification to SQL01 / `SpendWiseDb`. No EF Core migrations added.  
> **Backend Verification Baseline:** `04aefeca3a525ecb22977d550b8d40a0c444ffac` + rate limiting & HTTP validation hardening.

---

## 1. Domain Alignment Matrix (12 Domains)

This matrix compares the user frontend capabilities directly against the actual ASP.NET Core controllers, routes, DTOs, and handlers in `backend/SpendWise/Controllers/`.

| # | Domain | UI Capability / Intent | Backend Status | Exact Controller Route & Method | Requirements & Payload / Notes |
|---|--------|------------------------|----------------|---------------------------------|--------------------------------|
| 1 | **Auth, Session & Security PIN** | Register, Login, Refresh token, PIN setup, PIN verify, Password change | `BACKEND_EXISTS_FRONTEND_ALIGNMENT` | `POST /api/auth/register`<br>`POST /api/auth/login`<br>`POST /api/auth/refresh`<br>`POST /api/auth/logout`<br>`POST /api/auth/pin/setup`<br>`POST /api/auth/pin/verify`<br>`POST /api/auth/change-password` | Bearer Token for protected endpoints.<br>Password change requires `{ "currentPassword": "...", "newPassword": "..." }`.<br>Rate limit: `auth` (10 req/min). |
| 2 | **Profile & Preferences** | Get Profile, Upload Avatar, Request Email Change, Confirm Email Change, Preferences | `BACKEND_EXISTS_FRONTEND_ALIGNMENT` | `GET /api/users/me`<br>`POST /api/users/me/avatar`<br>`POST /api/users/me/email/request`<br>`POST /api/users/me/email/confirm`<br>`PUT /api/users/me/preferences` | `avatar`: `multipart/form-data`, file key `file`, max 5 MB (PNG/JPEG/WEBP).<br>`email/request`: `{ "newEmail": "..." }`<br>`email/confirm`: `{ "newEmail": "...", "token": "..." }`<br>Rate limit: `emailVerification` on email change. |
| 3 | **Personal Wallets** | List Wallets, Create Wallet, Get Wallet Details, Update Wallet, Delete/Archive | `BACKEND_EXISTS_FRONTEND_ALIGNMENT` | `GET /api/wallets`<br>`POST /api/wallets`<br>`GET /api/wallets/{id:guid}`<br>`PUT /api/wallets/{id:guid}`<br>`DELETE /api/wallets/{id:guid}` | Create/Update: `CreateWalletRequest` / `UpdateWalletRequest` (`Name`, `Currency`, `InitialBalance`, etc.).<br>Financial mutations require active User. |
| 4 | **Transactions & Ledger** | List Transactions, Get Transaction, Record Income, Record Expense, Multi-wallet Expense, Update | `BACKEND_EXISTS_FRONTEND_ALIGNMENT` | `GET /api/transactions`<br>`GET /api/transactions/{id:guid}`<br>`POST /api/transactions/income`<br>`POST /api/transactions/expense`<br>`POST /api/transactions/expense/multi-wallet`<br>`PATCH /api/transactions/{id:guid}` | List supports query filters (`WalletId`, `CategoryId`, `From`, `To`, `Page`, `PageSize`).<br>Mutations rate limited by `financial` (30 req/min). |
| 5 | **Categories, Budgets & Goals** | List Categories, Create Category, Budget CRUD, Goal CRUD & Contributions | `BACKEND_EXISTS_FRONTEND_ALIGNMENT` | `GET /api/categories`<br>`POST /api/categories`<br>`GET /api/budgets`<br>`POST /api/budgets`<br>`GET /api/goals`<br>`POST /api/goals`<br>`POST /api/goals/{id:guid}/contributions`<br>`POST /api/goals/{id:guid}/withdrawals` | Categories allow filtering by `kind` (`Income`/`Expense`).<br>Budgets defined on `CategoryId` and amount with period (`Month`/`Year`). |
| 6 | **Social Friends & User Discovery** | Search contact (exact lookup), Discover users by name, List friends, Send/Cancel/Accept friend request, Update preference | `BACKEND_EXISTS_FRONTEND_ALIGNMENT` (Enhanced in B1/B2) | `GET /api/friends`<br>`GET /api/friends/search?contact={contact}&kind={kind}`<br>`GET /api/friends/discover?query={query}&page={page}&pageSize={pageSize}`<br>`GET /api/friends/requests/incoming`<br>`GET /api/friends/requests/outgoing`<br>`POST /api/friends/requests`<br>`DELETE /api/friends/requests/{requestId:guid}`<br>`POST /api/friends/requests/{requestId:guid}/accept`<br>`PATCH /api/friends/{friendUserId:guid}/preference` | `discover` has dedicated `userDiscovery` rate limiter (30 req/min).<br>Exact search takes `?contact=` (NOT `?query=`).<br>Send request takes `{ "contact": "...", "message": "..." }`.<br>Preference takes `{ "nickname": "...", "isFavorite": true/false }`. |
| 7 | **Shared Funds (V1)** | Create Fund, List Funds, Fund Members, Invite Friend, Accept/Decline Invite, Contributions, Activities | `BACKEND_EXISTS_FRONTEND_ALIGNMENT` (Enhanced in B1) | `GET /api/v1/funds`<br>`POST /api/v1/funds`<br>`GET /api/v1/funds/{id:guid}`<br>`GET /api/v1/funds/{id:guid}/members`<br>`POST /api/v1/funds/{id:guid}/invitations`<br>`GET /api/v1/funds/invitations`<br>`POST /api/v1/funds/invitations/{id:guid}/accept`<br>`POST /api/v1/funds/{id:guid}/contributions`<br>`GET /api/v1/funds/{id:guid}/finance` | `V1MemberDto` now projects `fullName`, `avatarUrl`, and `displayName`.<br>Fund creation requires `Kind` (`Fixed` or `Free`), Target (for Fixed), and `SecurityPin`. |
| 8 | **Fund Elections & Governance** | Create Election, Nominate Candidate, Cast Ballot, Advance Election, Accept/Reject Election | `BACKEND_EXISTS_FRONTEND_ALIGNMENT` | `POST /api/v1/funds/{id:guid}/elections`<br>`GET /api/v1/funds/elections/{id:guid}`<br>`POST /api/v1/funds/elections/{id:guid}/candidates`<br>`POST /api/v1/funds/elections/{id:guid}/ballots`<br>`POST /api/v1/funds/elections/{id:guid}/advance`<br>`POST /api/v1/funds/elections/{id:guid}/accept` | Democratic governance engine for funds.<br>Enforces fund membership and quorum. |
| 9 | **Notifications & Alerts** | List Notifications, Mark as Read, Mark All Read, Search, Preferences | `BACKEND_EXISTS_FRONTEND_ALIGNMENT` | `GET /api/notifications`<br>`POST /api/notifications/{id:guid}/read`<br>`POST /api/notifications/read-all`<br>`GET /api/notifications/preferences`<br>`PUT /api/notifications/preferences` | Supports paginated notification inbox and preference toggles. |
| 10 | **Dashboard & Overview** | Dashboard summary, Balances, Cashflow, Category Breakdown, Attention Items | `BACKEND_EXISTS_FRONTEND_ALIGNMENT` | `GET /api/dashboard` | Route is `GET /api/dashboard` (NOT `/api/analytics/*`).<br>Returns `DashboardDto` containing `TotalBalance`, `TotalWalletsBalance`, `IncomeThisMonth`, `ExpenseThisMonth`, `BudgetRemaining`, `CategoryDistribution`, `SixMonthCashFlow`, `AttentionItems`. |
| 11 | **Payment / Bank Hub Integration** | User Bank Connections, SePay Server-to-Server Webhook / IPN, Sandbox Topup | `BACKEND_EXISTS_FRONTEND_ALIGNMENT` (User) / `INSUFFICIENT_EVIDENCE` (Live real provider checkout) | User: `GET /api/bank-connections`<br>`POST /api/bank-connections/link`<br>`POST /api/bank-connections/test-mode/wallet-top-ups`<br>Webhook: `POST /api/integrations/sepay/bankhub/events`<br>`POST /api/integrations/sepay/bankhub/ipn` | User bank linking and sandbox simulated top-up exist.<br>Direct interactive checkout redirect endpoint (`POST /api/payments/sepay/checkout`) is NOT verified/implemented in Backend; real bank-hub ingestion uses server-to-server webhook/IPN secret validation. |
| 12 | **Support & User Disputes** | Create Support Case, List User Cases, Get Case Detail, Reply to Case | `BACKEND_EXISTS_FRONTEND_ALIGNMENT` | `POST /api/cases/support`<br>`GET /api/cases/support`<br>`GET /api/cases/support/{id:guid}`<br>`POST /api/cases/support/{id:guid}/messages`<br>`POST /api/cases/support/{id:guid}/messages/multipart` | Controller is `CasesController` mapped to route `api/cases` (NOT `/api/support/tickets`).<br>Disputes for transactions: `POST /api/cases/disputes`. |

---

## 2. API Contract Specifications for B1 & B2

### 2.1 User Discovery Endpoint (`GET /api/friends/discover`)

- **Route:** `GET /api/friends/discover?query={term}&page={page}&pageSize={pageSize}`
- **Authentication:** Required (`Bearer <JWT>`)
- **Rate Limit Policy:** `userDiscovery` (30 requests / minute per authenticated user). Exceeding the rate limit yields `HTTP 429 Too Many Requests`.
- **Query Parameters:**
  - `query`: string (min 2, max 80 characters, trimmed). Searches substrings of `FullName`.
  - `page`: integer (default: `1`, min: `1`, max: `100,000`). Prevents integer overflow on LINQ `Skip()`.
  - `pageSize`: integer (default: `20`, min: `1`, max: `50`).
- **Response Model:** `PagedResult<UserDiscoveryDto>`
  ```json
  {
    "items": [
      {
        "userId": "b41cfbe1-4b19-48ce-88f5-93108c4ea78e",
        "fullName": "Alice InWonderland",
        "avatarUrl": "/avatars/a7e3...webp",
        "spendId": "SPW902184123",
        "relationshipState": "FRIENDS"
      }
    ],
    "page": 1,
    "pageSize": 20,
    "totalCount": 1
  }
  ```
  *(Note: `PagedResult<T>` has exactly 4 fields: `items`, `page`, `pageSize`, `totalCount`)*.

- **Privacy & Security Enforcement:**
  - **Exclusion of Self:** Querying user is never returned.
  - **Account Status Filter:** Only active ordinary users (`UserStatus.Active` and `SystemRole.User`) are returned; internal operator and suspended accounts are excluded.
  - **Zero Exposure of Private Data:** Stranger email addresses, phone numbers, balances, and security settings are NOT included in `UserDiscoveryDto`.
  - **Private Nickname Integrity:** `FriendPreference.Nickname` is only visible to the user who set it. Search only queries the canonical `FullName`.
  - **Zero N+1 Query:** Relationship states (`"NONE"`, `"REQUEST_SENT"`, `"REQUEST_RECEIVED"`, `"FRIENDS"`) are resolved in batched queries for the current page slice.

### 2.2 Exact Contact Search (`GET /api/friends/search`)

- **Route:** `GET /api/friends/search?contact={contact}&kind={kind}`
- **Note:** Parameter name is `contact` (NOT `query`).
- **Kind:** Optional (`"spendId"` or `"phone"`).
- **Response Model:** `FriendContactDto`
  ```json
  {
    "userId": "b41cfbe1-4b19-48ce-88f5-93108c4ea78e",
    "fullName": "Alice InWonderland",
    "spendId": "SPW902184123",
    "phone": null,
    "state": "FRIENDS",
    "avatarUrl": "/avatars/a7e3...webp"
  }
  ```
  *Stranger phone numbers are hidden unless already `FRIENDS`.*

### 2.3 Friends List (`GET /api/friends`)

- **Route:** `GET /api/friends`
- **Response Model:** `IReadOnlyList<FriendDto>`
  ```json
  [
    {
      "userId": "b41cfbe1-4b19-48ce-88f5-93108c4ea78e",
      "fullName": "Alice InWonderland",
      "spendId": "SPW902184123",
      "phone": "0912345678",
      "nickname": "Alice bestie",
      "isFavorite": true,
      "acceptedAt": "2026-10-22T04:45:00Z",
      "avatarUrl": "/avatars/a7e3...webp",
      "commonGroups": []
    }
  ]
  ```

### 2.4 Fund Members (`GET /api/v1/funds/{id:guid}/members`)

- **Route:** `GET /api/v1/funds/{id:guid}/members`
- **Response Model:** `IReadOnlyList<V1MemberDto>`
  ```json
  [
    {
      "userId": "b41cfbe1-4b19-48ce-88f5-93108c4ea78e",
      "seat": 1,
      "canSpend": true,
      "joinedAt": "2026-10-22T04:45:00Z",
      "leftAt": null,
      "fullName": "Alice InWonderland",
      "avatarUrl": "/avatars/a7e3...webp",
      "displayName": "Alice InWonderland"
    }
  ]
  ```
  *Executed via a single SQL query join between `V1FundMemberships` and `Users` (Zero N+1).*

---

## 3. Important Implementation & Protocol Rules for Frontend

1. **Avatar Freshness Semantics:**
   - **`NEXT AUTHORIZED REFETCH, NOT REALTIME`**.
   - The backend does not maintain WebSocket avatar push subscriptions.
   - When User A uploads a new avatar via `POST /api/users/me/avatar`, it is instantly persisted to the database.
   - User B will see User A's updated avatar upon the next authorized GET request (`/api/friends`, `/api/v1/funds/{id}/members`, `/api/friends/search`, etc.).
2. **Vietnamese Diacritics & Collation:**
   - **`ACCENT-INSENSITIVE SEARCH: NOT SUPPORTED / NOT VERIFIED`**.
   - Database collation is Case-Insensitive, Accent-Sensitive (`SQL_Latin1_General_CP1_CI_AS`).
   - `a` matches `A`, but does NOT match `á`, `à`, `ạ`. Frontend must display instructions for users to input search queries with appropriate diacritics.
3. **Dispute & Support Ticketing:**
   - The route prefix is `/api/cases` (specifically `POST /api/cases/support` and `POST /api/cases/disputes`), NOT `/api/support/tickets`.
4. **Dashboard & Analytics:**
   - User overview data is aggregated at `GET /api/dashboard`. There is no `/api/analytics/*` endpoint in the backend.

---

## 4. Verification & Automated Test Evidence

All tests pass on an isolated SQLite database test fixture (`SpendWiseApiFactory` / `TargetedCorrectnessFixture`). Zero connection to `SQL01`.

| Test Class / Suite | Test Case | Status | Evidence |
|--------------------|-----------|:------:|----------|
| `UserContractAlignmentTests` | `TestA_Friend_Avatar_Freshness_On_Refetch` | **PASS** | Handler-level: friend avatar updates reflect on next fetch |
| `UserContractAlignmentTests` | `TestB_Fund_Members_Projection_Includes_FullName_And_AvatarUrl` | **PASS** | `V1MemberDto` single-SQL join projection |
| `UserContractAlignmentTests` | `TestC_Nickname_Remains_Private_To_Owner_And_Does_Not_Leak_To_Global_Search` | **PASS** | Nicknames remain private; discovery searches canonical `FullName` |
| `UserContractAlignmentTests` | `TestD_Discover_Users_Excludes_Self_And_Inactives_And_Returns_Correct_RelationshipStates` | **PASS** | Self exclusion, inactive user exclusion, pagination overflow check |
| `UserContractAlignmentTests` | `TestE_User_Discovery_Accurately_Reflects_All_Four_Relationship_States` | **PASS** | Accurately tests `NONE`, `FRIENDS`, `REQUEST_SENT`, `REQUEST_RECEIVED` |
| `UserContractAlignmentTests` | `TestF_Backward_Compatibility_Existing_Search_And_Requests_Intact` | **PASS** | Exact contact search `?contact=` remains backwards-compatible |
| `UserContractAlignmentTests` | `TestG_Http_EndToEnd_AvatarUpload_FriendRefetch_FundMembers_Discovery_And_RateLimit` | **PASS** | **Full HTTP API Pipeline**: Real multipart avatar upload, friend refetch, fund members, user discovery, and **HTTP 429 rate limit verification** |
| `RateLimitPolicyTests` | `FinancialMutations_HaveFinancialPolicy` | **PASS** | Rate limit policies intact |
| `RateLimitPolicyTests` | `ReadOnlyNavigation_DoesNotUseFinancialPolicy` | **PASS** | Read-only endpoints not blocked |
