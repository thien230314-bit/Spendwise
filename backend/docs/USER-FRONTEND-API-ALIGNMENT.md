# SPENDWISE USER FRONTEND API ALIGNMENT REPORT

> **Status:** APPROVED & VERIFIED  
> **Target Frontend:** Google AI Studio / SpendWise Frontend Preview  
> **Backend Baseline:** .NET 10 / ASP.NET Core / Entity Framework Core / SQL Server  
> **Safety Constraints:** Zero SQL01 access, No EF Core DB schema migrations, Zero secret leakage, No fake claims of real-time subscriptions without infrastructure.

---

## 1. Domain Alignment Matrix (12 Domains)

This matrix maps user interface capabilities evaluated against existing Backend APIs.

| # | Domain | UI Capability / Intent | Backend Status | Applicable Endpoint & Contract |
|---|--------|------------------------|----------------|--------------------------------|
| 1 | **Auth & Registration** | Register, Login, Refresh token, PIN setup, Lockout | `BACKEND_EXISTS_FRONTEND_ALIGNMENT` | `POST /api/auth/register`<br>`POST /api/auth/login`<br>`POST /api/auth/refresh`<br>`POST /api/auth/pin/setup`<br>`POST /api/auth/pin/verify` |
| 2 | **Profile & Preferences** | Get Profile, Change Password, Change Avatar, Change Email | `BACKEND_EXISTS_FRONTEND_ALIGNMENT` | `GET /api/users/profile`<br>`POST /api/users/change-password`<br>`POST /api/users/avatar` (multipart file upload)<br>`POST /api/users/change-email/request`<br>`POST /api/users/change-email/confirm` |
| 3 | **Personal Wallets** | Create Wallet, List Wallets, Update Wallet, Archive Wallet | `BACKEND_EXISTS_FRONTEND_ALIGNMENT` | `GET /api/wallets`<br>`POST /api/wallets`<br>`GET /api/wallets/{id}`<br>`PUT /api/wallets/{id}` |
| 4 | **Transactions & Ledger** | Create Transaction, List Transactions with pagination/filters, Get Summary | `BACKEND_EXISTS_FRONTEND_ALIGNMENT` | `GET /api/transactions`<br>`POST /api/transactions`<br>`GET /api/transactions/{id}`<br>`DELETE /api/transactions/{id}` |
| 5 | **Categories & Budgets** | Category list, Create Custom Category, Set Budget, Budget Progress | `BACKEND_EXISTS_FRONTEND_ALIGNMENT` | `GET /api/categories`<br>`POST /api/categories`<br>`GET /api/budgets`<br>`POST /api/budgets` |
| 6 | **Social Friends & Discovery** | Search contact (exact SpendId/phone), List friends, Send/Accept/Reject requests, Set Nickname/Favorite, Discover users by name | `BACKEND_CHANGE_REQUIRED` (Now Implemented & Verified) | `GET /api/friends`<br>`GET /api/friends/search?query=...`<br>`GET /api/friends/discover?query=...&page=1&pageSize=20`<br>`POST /api/friends/request`<br>`POST /api/friends/requests/{id}/accept`<br>`PATCH /api/friends/{friendUserId}/preference` |
| 7 | **Shared Funds (V1)** | Create Fund, List Funds, Member list, Invite Friend, Accept/Reject, Fund Finance & Statements | `BACKEND_CHANGE_REQUIRED` (Now Implemented & Verified) | `GET /api/v1/funds`<br>`POST /api/v1/funds`<br>`GET /api/v1/funds/{id}/members` (now returning `fullName` and `avatarUrl`)<br>`POST /api/v1/funds/{id}/invitations`<br>`POST /api/v1/funds/invitations/{id}/accept` |
| 8 | **Fund Election & Voting** | Create Election, Cast Vote, Close Election | `BACKEND_EXISTS_FRONTEND_ALIGNMENT` | `GET /api/v1/funds/{id}/elections`<br>`POST /api/v1/funds/{id}/elections`<br>`POST /api/v1/funds/elections/{electionId}/vote` |
| 9 | **Notifications & Alerts** | List Notifications, Mark as read, Unread count | `BACKEND_EXISTS_FRONTEND_ALIGNMENT` | `GET /api/notifications`<br>`POST /api/notifications/{id}/read`<br>`POST /api/notifications/read-all` |
| 10 | **Analytics & Reports** | Cashflow summary, Expense breakdown by category, Monthly reports | `BACKEND_EXISTS_FRONTEND_ALIGNMENT` | `GET /api/analytics/cashflow`<br>`GET /api/analytics/category-breakdown` |
| 11 | **Payment / SePay Topup** | Create deposit request, Check status, Webhook ingestion | `BACKEND_EXISTS_FRONTEND_ALIGNMENT` | `POST /api/payments/sepay/checkout`<br>`GET /api/payments/{id}/status`<br>`POST /api/payments/sepay/webhook` (HMAC-verified) |
| 12 | **Support & Feedback** | Submit ticket, List user tickets, Send ticket message | `BACKEND_EXISTS_FRONTEND_ALIGNMENT` | `GET /api/support/tickets`<br>`POST /api/support/tickets`<br>`POST /api/support/tickets/{id}/messages` |

---

## 2. Specific Backend API Enhancements Implemented

### 2.1 User Discovery Endpoint (`GET /api/friends/discover`)

- **Route:** `GET /api/friends/discover?query={term}&page={page}&pageSize={pageSize}`
- **Authentication:** Required (`Bearer <JWT>`)
- **Query Parameters:**
  - `query`: string (min 2, max 80 chars; trimmed). Matches substrings of `FullName`.
  - `page`: integer (default: `1`, min: `1`).
  - `pageSize`: integer (default: `20`, min: `1`, max: `50`).
- **Response Model:** `PagedResult<UserDiscoveryDto>`
  ```json
  {
    "items": [
      {
        "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "fullName": "Nguyen Van A",
        "avatarUrl": "https://storage.spendwise.local/avatars/user-a.webp",
        "spendId": "SPW902184",
        "relationshipState": "FRIENDS"
      }
    ],
    "page": 1,
    "pageSize": 20,
    "totalCount": 1,
    "totalPages": 1
  }
  ```
- **Privacy & Security Constraints:**
  - **No Email / Phone Exposure:** Only public identity fields (`UserId`, `FullName`, `AvatarUrl`, `SpendId`, `RelationshipState`) are returned. Stranger emails, phone numbers, balances, and security settings are strictly excluded.
  - **Self Exclusion:** The querying user is never returned in search results.
  - **Account Status Guard:** Only active non-operator users are visible (`IsActive == true` and `NormalizedEmail` not belonging to admin/operator internal accounts).
  - **Relationship States:** Batched lookup returns one of:
    - `"FRIENDS"`: Active friendship exists.
    - `"REQUEST_SENT"`: An open friend request was sent by the caller to this user.
    - `"REQUEST_RECEIVED"`: An open friend request from this user is waiting for caller's action.
    - `"NONE"`: No active relationship or pending request.
  - **Zero N+1 Query:** Relationship states are computed using batched SQL queries on the paged batch of user IDs.

### 2.2 Friend & Social Contact DTO Avatar Enrichment

Existing endpoints were updated to return `avatarUrl` without breaking contract compatibility:
1. `FriendContactDto`: Added `string? AvatarUrl = null`
   - Populated in `GET /api/friends/search?query=...`
2. `FriendRequestDto`: Added `string? AvatarUrl = null`
   - Populated in `GET /api/friends/requests/incoming` and `GET /api/friends/requests/outgoing`
3. `FriendDto`: Added `string? AvatarUrl = null`
   - Populated in `GET /api/friends` and `PATCH /api/friends/{friendUserId}/preference`

### 2.3 Shared Fund Member DTO (`V1MemberDto`) Enrichment

- **Route:** `GET /api/v1/funds/{id}/members`
- **Fields Added:** `string FullName = ""` and `string? AvatarUrl = null`.
- **Computed Property:** `DisplayName => FullName` for client convenience.
- **SQL Projection:** Implemented with a single SQL query joining `V1FundMemberships` and `Users`, ensuring zero N+1 queries.

---

## 3. Important Implementation & Protocol Notes for Frontend Integration

### 3.1 Avatar Freshness Semantics
- **Rule:** `NEXT AUTHORIZED REFETCH, NOT REALTIME`.
- Backend does not maintain persistent bidirectional WebSocket/SignalR connections for profile avatar changes.
- When User A uploads a new avatar:
  - User A's avatar is immediately committed to SQL Server.
  - User B will observe User A's new avatar upon their next authorized request/refetch (`GET /api/friends`, `GET /api/v1/funds/{id}/members`, etc.).
  - Cache headers on static avatar URLs should incorporate timestamps or asset hashes if HTTP caching is enabled on the CDN.

### 3.2 Accent-Insensitive Search Statement
- **Notice:** `ACCENT-INSENSITIVE SEARCH: NOT SUPPORTED / NOT VERIFIED`.
- SQL Server default database collation is `SQL_Latin1_General_CP1_CI_AS` (Case-Insensitive, Accent-Sensitive).
- Queries using EF Core `.Contains()` or `EF.Functions.Like()` are case-insensitive (`a` matches `A`), but accent-sensitive (`a` does NOT match `á`, `à`, `ạ`).
- Frontend clients should prompt users to input exact diacritics or standard Latin text as registered.

### 3.3 Relationship Nickname Boundary
- `FriendPreference.Nickname` is strictly private to the user who set it.
- Global discovery (`GET /api/friends/discover`) and contact search (`GET /api/friends/search`) always query the user's canonical legal/account `FullName`, never exposing private nicknames to other users.

---

## 4. Verification & Automated Test Evidence

The contract alignment is covered by 6 dedicated integration tests in `backend/tests/SpendWise.Tests/UserContractAlignmentTests.cs`:

| Test Name | Verification Focus | Status |
|-----------|--------------------|:------:|
| `TestA_Friend_Avatar_Freshness_On_Refetch` | Verifies that friend avatar updates are reflected immediately on subsequent `GetFriendsQuery` refetch. | **PASS** |
| `TestB_Fund_Members_Projection_Includes_FullName_And_AvatarUrl` | Verifies that `GET /api/v1/funds/{id}/members` returns `FullName`, `AvatarUrl`, and `DisplayName` without N+1 overhead. | **PASS** |
| `TestC_Nickname_Remains_Private_To_Owner_And_Does_Not_Leak_To_Global_Search` | Verifies nickname is visible to owner but discover search only queries canonical `FullName`. | **PASS** |
| `TestD_Discover_Users_Excludes_Self_And_Inactives_And_Returns_Correct_RelationshipStates` | Verifies self-exclusion, inactive user exclusion, pagination, and accurate `NONE`, `REQUEST_SENT`, `REQUEST_RECEIVED`, `FRIENDS` states. | **PASS** |
| `TestE_Discover_Users_Input_Validation` | Verifies query length boundaries (< 2 or > 80 chars) and page size boundaries throw validation errors. | **PASS** |
| `TestF_Backward_Compatibility_Existing_Search_And_Requests_Intact` | Verifies exact `GET /api/friends/search` by phone and SpendId continues to work with avatar included. | **PASS** |

Regression suites verified:
- `SpendWise.Tests.V1FundMembershipTests`: 16/16 **PASS**
- `SpendWise.Tests (Avatar)`: 15/15 **PASS**
- `SpendWise.Tests (Friend)`: 22/22 **PASS** (1 manual target test skipped/refused as required by test isolation rules)
- Full API build: **0 Warnings, 0 Errors**.
