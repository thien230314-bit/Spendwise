# SPENDWISE BACKEND — MA TRẬN PHỦ ENDPOINT USER ĐẦY ĐỦ (ENDPOINT COVERAGE MATRIX)

> **Tài liệu kiểm toán độc lập đối chiếu toàn bộ 215 User Endpoints thực tế trong source code Backend với 144 requests trong Postman Collection.**
> **Mục tiêu:** Báo cáo trung thực, chính xác tỷ lệ bao phủ thực tế, ngăn chặn việc ngộ nhận "100% Endpoints" khi mới chỉ kiểm thử 84/215 endpoints.

---

## 1. BẢNG THỐNG KÊ TỔNG HỢP THEO 10 MIỀN NGHIỆP VỤ (DOMAIN SUMMARY)

| STT | Miền Nghiệp Vụ (Functional Domain) | Tổng Số Endpoint Code | Số Endpoint ĐÃ TEST | Số Endpoint CHƯA TEST | Tỷ Lệ Bao Phủ |
| :---: | :--- | :---: | :---: | :---: | :---: |
| 1 | **1. Authentication, PIN & Session Lifecycle** | **20** | **15** | **5** | ** 75.0%** |
| 2 | **10. Open Banking Hub & Payment Webhooks** | **17** | **0** | **17** | **  0.0%** |
| 3 | **2. User Profile, Avatar & Identity** | **6** | **5** | **1** | ** 83.3%** |
| 4 | **3. Social, Friends Discovery & Directory** | **11** | **8** | **3** | ** 72.7%** |
| 5 | **4. Wallets, Transactions & Internal Ledger** | **26** | **18** | **8** | ** 69.2%** |
| 6 | **5. Planning, Budgets & Recurring Rules** | **26** | **13** | **13** | ** 50.0%** |
| 7 | **6. Funds Ecosystem (Fixed & Free Groups)** | **45** | **14** | **31** | ** 31.1%** |
| 8 | **7. Legacy Groups & Community Splits** | **29** | **0** | **29** | **  0.0%** |
| 9 | **8. Notifications, Support & Operations** | **24** | **8** | **16** | ** 33.3%** |
| 10 | **9. AI Assistant & Financial Advisory** | **11** | **3** | **8** | ** 27.3%** |
| **TỔNG** | **TOÀN BỘ 10 PHÂN HỆ USER** | **215** | **84** | **131** | ** 39.1%** |

### Phân tích nhanh về mẫu số thực tế:
- **Số endpoint Backend User thực tế:** **215 endpoints** (được kiểm kê chính xác từ 26 Controller User trong source code).
- **Số endpoint đã được kiểm thử bởi Postman Collection:** **84 endpoints** (chiếm ** 39.1%**).
- **Số endpoint hoàn toàn CHƯA ĐƯỢC KIỂM THỬ:** **131 endpoints** (chiếm ** 60.9%**).
- **Tổng số lượt gửi request kiểm thử:** **144 requests** (do nhiều endpoint quan trọng như Register, Verify PIN, Fund Contributions, Budgets được kiểm thử nhiều lần qua các luồng E2E khác nhau hoặc kiểm thử cả 2 trường hợp Happy-path và Negative invariant).

---

## 2. MA TRẬN CHI TIẾT TOÀN BỘ 215 USER ENDPOINTS

### 1. Authentication, PIN & Session Lifecycle

| # | Controller | Action Name | Method | Route | Trạng Thái Kiểm Thử | Tên Test Case trong Postman Collection |
| :---: | :--- | :--- | :---: | :--- | :---: | :--- |
| 1 | `AccountRecoveryController` | `Submit` | `POST` | `/api/account-recovery` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 2 | `AuthController` | `Register` | `POST` | `/api/auth/register` | `TESTED_PASS_SUCCESS` | 02 - Register User A (Valid); 03 - Register User A Duplicate (Negative Check); 07 - Register User B; 11 - Register Unverified User (for Inactive Check) |
| 3 | `AuthController` | `VerifyRegistrationEmail` | `POST` | `/api/auth/email-verification/verify` | `TESTED_PASS_SUCCESS` | 05 - Verify Registration Email User A; 08 - Verify Registration Email User B |
| 4 | `AuthController` | `ResendRegistrationEmail` | `POST` | `/api/auth/email-verification/resend` | `TESTED_PASS_SUCCESS` | 04 - Resend Verification OTP User A |
| 5 | `AuthController` | `Login` | `POST` | `/api/auth/login` | `TESTED_PASS_SUCCESS` | 12 - Login with Unverified User (Negative Check); 13 - Login with Wrong Password (Negative Check); 20 - Re-login User A after Reset Password; 24 - Refresh Token after Single Session Revoke (Re-establish Session); Flow A05 - Re-login User A; Flow B04 - Login with New Email (Expected: Success); Flow B06 - Login with Old Email (Expected: 401 / Denied) |
| 6 | `AuthController` | `SetupPin` | `POST` | `/api/auth/pin/setup` | `TESTED_PASS_SUCCESS` | 06 - Setup Security PIN User A; 09 - Setup Security PIN User B |
| 7 | `AuthController` | `VerifyPinForLogin` | `POST` | `/api/auth/pin/verify` | `TESTED_PASS_SUCCESS` | 21 - Complete PIN Login User A; 25 - Complete PIN Login for User A; Flow A06 - Complete PIN Login for Re-login; Flow B05 - Complete PIN Verification with New Email |
| 8 | `AuthController` | `Refresh` | `POST` | `/api/auth/refresh` | `TESTED_PASS_SUCCESS` | 10 - Refresh Access Token User A |
| 9 | `AuthController` | `Logout` | `POST` | `/api/auth/logout` | `TESTED_PASS_SUCCESS` | Flow A04 - Logout User A; Flow B03 - Logout After Email Change |
| 10 | `AuthController` | `GetSessions` | `GET` | `/api/auth/sessions` | `TESTED_PASS_SUCCESS` | 22 - List Active User Sessions |
| 11 | `AuthController` | `RevokeSession` | `DELETE` | `/api/auth/sessions/{sessionId}` | `TESTED_PASS_SUCCESS` | 23 - Revoke Single Session |
| 12 | `AuthController` | `ChangePassword` | `POST` | `/api/auth/change-password` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 13 | `AuthController` | `ForgotPassword` | `POST` | `/api/auth/forgot-password` | `TESTED_PASS_SUCCESS` | 18 - Forgot Password User A |
| 14 | `AuthController` | `ResetPassword` | `POST` | `/api/auth/reset-password` | `TESTED_PASS_SUCCESS` | 19 - Reset Password User A |
| 15 | `AuthController` | `ForgotSecurityPin` | `POST` | `/api/auth/pin/forgot` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 16 | `AuthController` | `ValidateSecurityPinReset` | `POST` | `/api/auth/pin/reset/validate` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 17 | `AuthController` | `ResetSecurityPin` | `POST` | `/api/auth/pin/reset` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 18 | `SecurityPinController` | `Status` | `GET` | `/api/security-pin/status` | `TESTED_PASS_SUCCESS` | 26 - Security PIN Status Check |
| 19 | `SecurityPinController` | `Verify` | `POST` | `/api/security-pin/verify` | `TESTED_PASS_SUCCESS` | 14 - Verify Security PIN Valid; 15 - Verify Security PIN Invalid (Negative Check) |
| 20 | `SecurityPinController` | `Change` | `PUT` | `/api/security-pin` | `TESTED_PASS_SUCCESS` | 16 - Change Security PIN User A; 17 - Revert Security PIN User A |

### 10. Open Banking Hub & Payment Webhooks

| # | Controller | Action Name | Method | Route | Trạng Thái Kiểm Thử | Tên Test Case trong Postman Collection |
| :---: | :--- | :--- | :---: | :--- | :---: | :--- |
| 21 | `BankConnectionsController` | `Link` | `POST` | `/api/bank-connections/link` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 22 | `BankConnectionsController` | `List` | `GET` | `/api/bank-connections` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 23 | `BankConnectionsController` | `Get` | `GET` | `/api/bank-connections/{id}` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 24 | `BankConnectionsController` | `Unlink` | `POST` | `/api/bank-connections/{id}/unlink` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 25 | `BankConnectionsController` | `Reactivate` | `POST` | `/api/bank-connections/{id}/reactivate` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 26 | `BankConnectionsController` | `Sync` | `POST` | `/api/bank-connections/{id}/sync` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 27 | `BankConnectionsController` | `StartTestModeLink` | `POST` | `/api/bank-connections/test-mode/link-challenges` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 28 | `BankConnectionsController` | `ConfirmTestModeLink` | `POST` | `/api/bank-connections/test-mode/link-challenges/{challengeId}/confirm` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 29 | `BankConnectionsController` | `CreateTestModeTopUp` | `POST` | `/api/bank-connections/test-mode/wallet-top-ups` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 30 | `BankConnectionsController` | `ConfirmTestModeTopUp` | `POST` | `/api/bank-connections/test-mode/wallet-top-ups/{topUpId}/confirm` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 31 | `BankConnectionsController` | `GetTestModeLinkedAccounts` | `GET` | `/api/bank-connections/test-mode/linked-accounts` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 32 | `BankConnectionsController` | `GetTestModeWalletTopUps` | `GET` | `/api/bank-connections/test-mode/wallet-top-ups` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 33 | `BankTransactionsController` | `List` | `GET` | `/api/bank-transactions` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 34 | `BankTransactionsController` | `Get` | `GET` | `/api/bank-transactions/{id}` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 35 | `BankTransactionsController` | `Reclassify` | `PATCH` | `/api/bank-transactions/{id}/category` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 36 | `SePayBankHubIntegrationController` | `Events` | `POST` | `/api/integrations/sepay/bankhub/events` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 37 | `SePayBankHubIntegrationController` | `Ipn` | `POST` | `/api/integrations/sepay/bankhub/ipn` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |

### 2. User Profile, Avatar & Identity

| # | Controller | Action Name | Method | Route | Trạng Thái Kiểm Thử | Tên Test Case trong Postman Collection |
| :---: | :--- | :--- | :---: | :--- | :---: | :--- |
| 38 | `UsersController` | `Get` | `GET` | `/api/users/me` | `TESTED_PASS_SUCCESS` | 01 - Get Current User Profile (User A); Flow A01 - Pre-check Profile Avatar; Flow A03 - Check Profile Immediately Has New Avatar; Flow A07 - Check Profile After Fresh Login Retains Avatar |
| 39 | `UsersController` | `UploadAvatar` | `POST` | `/api/users/me/avatar` | `TESTED_PASS_SUCCESS` | 02 - Upload User Avatar (User A); Flow A02 - Upload Real Avatar Image |
| 40 | `UsersController` | `RequestEmailChange` | `POST` | `/api/users/me/email/request` | `TESTED_PASS_SUCCESS` | Flow B01 - Request Email Change to New Email |
| 41 | `UsersController` | `ConfirmEmailChange` | `POST` | `/api/users/me/email/confirm` | `TESTED_PASS_SUCCESS` | Flow B02 - Confirm Email Change with OTP |
| 42 | `UsersController` | `Preferences` | `PUT` | `/api/users/me/preferences` | `TESTED_PASS_SUCCESS` | 03 - Update User Preferences |
| 43 | `UsersController` | `RevokeSessions` | `DELETE` | `/api/users/me/sessions` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |

### 3. Social, Friends Discovery & Directory

| # | Controller | Action Name | Method | Route | Trạng Thái Kiểm Thử | Tên Test Case trong Postman Collection |
| :---: | :--- | :--- | :---: | :--- | :---: | :--- |
| 44 | `FriendsController` | `Search` | `GET` | `/api/friends/search` | `TESTED_PASS_SUCCESS` | 04 - Search User B by Contact (Email); 05 - Search User B by SpendId; Flow C01 - User A Searches User B by Contact |
| 45 | `FriendsController` | `Discover` | `GET` | `/api/friends/discover` | `TESTED_PASS_SUCCESS` | 06 - Discover Users by Name with Pagination |
| 46 | `FriendsController` | `List` | `GET` | `/api/friends` | `TESTED_PASS_SUCCESS` | 12 - User A Lists Friends; 13 - User B Lists Friends; Flow C03 - User A Checks Friend List (Has User B); Flow C04 - User B Checks Friend List (Has User A) |
| 47 | `FriendsController` | `Incoming` | `GET` | `/api/friends/requests/incoming` | `TESTED_PASS_SUCCESS` | 10 - User B Checks Incoming Requests |
| 48 | `FriendsController` | `Outgoing` | `GET` | `/api/friends/requests/outgoing` | `TESTED_PASS_SUCCESS` | 09 - User A Checks Outgoing Requests |
| 49 | `FriendsController` | `Send` | `POST` | `/api/friends/requests` | `TESTED_PASS_SUCCESS` | 07 - Send Friend Request (User A to User B); 08 - Send Friend Request Duplicate (Idempotency Check); Flow C02 - User A Checks Friend Request Status to User B |
| 50 | `FriendsController` | `Cancel` | `DELETE` | `/api/friends/requests/{requestId}` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 51 | `FriendsController` | `Accept` | `POST` | `/api/friends/requests/{requestId}/accept` | `TESTED_PASS_SUCCESS` | 11 - User B Accepts Friend Request |
| 52 | `FriendsController` | `Reject` | `POST` | `/api/friends/requests/{requestId}/reject` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 53 | `FriendsController` | `Unfriend` | `DELETE` | `/api/friends/{friendUserId}` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 54 | `FriendsController` | `Preference` | `PATCH` | `/api/friends/{friendUserId}/preference` | `TESTED_PASS_SUCCESS` | 14 - Update Friend Preference (User A sets Favorite) |

### 4. Wallets, Transactions & Internal Ledger

| # | Controller | Action Name | Method | Route | Trạng Thái Kiểm Thử | Tên Test Case trong Postman Collection |
| :---: | :--- | :--- | :---: | :--- | :---: | :--- |
| 55 | `CategoriesController` | `List` | `GET` | `/api/categories` | `TESTED_PASS_SUCCESS` | 02 - Get System Categories (Income & Expense); Flow F01 - Get Expense Category for Budgeting |
| 56 | `CategoriesController` | `Get` | `GET` | `/api/categories/{categoryId}` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 57 | `CategoriesController` | `Create` | `POST` | `/api/categories` | `TESTED_PASS_SUCCESS` | 09 - Create Custom Category |
| 58 | `CategoriesController` | `Update` | `PUT` | `/api/categories/{categoryId}` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 59 | `CategoriesController` | `Archive` | `DELETE` | `/api/categories/{categoryId}` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 60 | `DashboardController` | `Get` | `GET` | `/api/dashboard` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 61 | `EvidenceController` | `Upload` | `POST` | `/api/evidence` | `TESTED_PASS_SUCCESS` | 02b - Upload Evidence for Income Posting; 09b - Upload Evidence for Expense Transaction |
| 62 | `EvidenceController` | `Download` | `GET` | `/api/evidence/{id}` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 63 | `TransactionsController` | `Search` | `GET` | `/api/transactions` | `TESTED_PASS_SUCCESS` | 13 - List Transactions with Query Filters; Flow E04 - Check Linked Transactions List |
| 64 | `TransactionsController` | `Get` | `GET` | `/api/transactions/{transactionId}` | `TESTED_PASS_SUCCESS` | 14 - Get Transaction Details |
| 65 | `TransactionsController` | `Income` | `POST` | `/api/transactions/income` | `TESTED_PASS_SUCCESS` | 03 - Fund Main Wallet via Income Posting (10,000,000 VND) |
| 66 | `TransactionsController` | `Expense` | `POST` | `/api/transactions/expense` | `TESTED_PASS_SUCCESS` | 10 - Record Expense Transaction (45,000 VND) |
| 67 | `TransactionsController` | `MultiWalletExpense` | `POST` | `/api/transactions/expense/multi-wallet` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 68 | `TransactionsController` | `Update` | `PATCH` | `/api/transactions/{transactionId}` | `TESTED_PASS_SUCCESS` | 15 - Update Transaction Description |
| 69 | `TransfersController` | `Find` | `GET` | `/api/transfers/recipients` | `TESTED_PASS_SUCCESS` | 01 - Find Bank Transfer Recipient by Phone |
| 70 | `TransfersController` | `Get` | `GET` | `/api/transfers/{transferId}` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 71 | `TransfersController` | `Send` | `POST` | `/api/transfers` | `TESTED_PASS_SUCCESS` | 02 - Create Bank Direct Transfer Instruction |
| 72 | `TransfersController` | `History` | `GET` | `/api/transfers` | `TESTED_PASS_SUCCESS` | 03 - Bank Direct Transfer History |
| 73 | `TransfersController` | `Confirm` | `POST` | `/api/transfers/{paymentId}/confirm` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 74 | `TransfersController` | `Internal` | `POST` | `/api/transfers/internal` | `TESTED_PASS_SUCCESS` | 11 - Internal Transfer Between Wallets (3,000,000 VND); Flow E02 - Transfer 300,000 VND from Main to Savings Wallet |
| 75 | `TransfersController` | `P2P` | `POST` | `/api/transfers/p2p` | `TESTED_PASS_SUCCESS` | 00 - Fund User B Main Wallet (P2P Transfer 2,000,000 VND) |
| 76 | `WalletsController` | `List` | `GET` | `/api/wallets` | `TESTED_PASS_SUCCESS` | 01 - List Default Wallets for User A; 12 - Check Balances After Expense and Transfer; 00b - Get User B Main Wallet; Flow E01 - Identify Main and Savings Wallets for Flow E |
| 77 | `WalletsController` | `Get` | `GET` | `/api/wallets/{walletId}` | `TESTED_PASS_SUCCESS` | 07 - Get Main Wallet Details; Flow E03 - Check Savings Wallet Balance Increased by 300,000 VND |
| 78 | `WalletsController` | `Create` | `POST` | `/api/wallets` | `TESTED_PASS_SUCCESS` | 04 - Create Savings Wallet User A; 05 - Create Emergency Wallet User A; 06 - Check Wallet Limit (Try Creating 4th Wallet) |
| 79 | `WalletsController` | `Update` | `PUT` | `/api/wallets/{walletId}` | `TESTED_PASS_SUCCESS` | 08 - Update Savings Wallet Name |
| 80 | `WalletsController` | `Close` | `DELETE` | `/api/wallets/{walletId}` | `TESTED_PASS_SUCCESS` | 16 - Close Emergency Wallet |

### 5. Planning, Budgets & Recurring Rules

| # | Controller | Action Name | Method | Route | Trạng Thái Kiểm Thử | Tên Test Case trong Postman Collection |
| :---: | :--- | :--- | :---: | :--- | :---: | :--- |
| 81 | `BudgetsController` | `List` | `GET` | `/api/budgets` | `TESTED_PASS_SUCCESS` | 02 - List Monthly Budgets |
| 82 | `BudgetsController` | `Get` | `GET` | `/api/budgets/{id}` | `TESTED_PASS_SUCCESS` | Flow F03 - Check Budget Status (Spent: 0, Remaining: 2,000,000 VND) |
| 83 | `BudgetsController` | `Create` | `POST` | `/api/budgets` | `TESTED_PASS_SUCCESS` | 01 - Create Monthly Budget User A; Flow F02 - Create Budget (2,000,000 VND limit) |
| 84 | `BudgetsController` | `CreateOrUpdateCompatible` | `PUT` | `/api/budgets` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 85 | `BudgetsController` | `Update` | `PUT` | `/api/budgets/{id}` | `TESTED_PASS_SUCCESS` | 03 - Update Monthly Budget Limit; Flow F04 - Update Budget Limit to 3,000,000 VND |
| 86 | `BudgetsController` | `Delete` | `DELETE` | `/api/budgets/{id}` | `TESTED_PASS_SUCCESS` | 04 - Delete Monthly Budget; Flow F05 - Clean Up Budget via Delete |
| 87 | `GoalsController` | `List` | `GET` | `/api/goals` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 88 | `GoalsController` | `Get` | `GET` | `/api/goals/{id}` | `TESTED_PASS_SUCCESS` | 07 - Get Goal Details and Verify Progress |
| 89 | `GoalsController` | `Create` | `POST` | `/api/goals` | `TESTED_PASS_SUCCESS` | 05 - Create Savings Goal (MacBook M4) |
| 90 | `GoalsController` | `Update` | `PUT` | `/api/goals/{id}` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 91 | `GoalsController` | `Delete` | `DELETE` | `/api/goals/{id}` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 92 | `GoalsController` | `Contribute` | `POST` | `/api/goals/{id}/contributions` | `TESTED_PASS_SUCCESS` | 06 - Deposit to Savings Goal (2,000,000 VND) |
| 93 | `GoalsController` | `Withdraw` | `POST` | `/api/goals/{id}/withdrawals` | `TESTED_PASS_SUCCESS` | 08 - Withdraw from Savings Goal (500,000 VND) |
| 94 | `RecurringTransactionsController` | `List` | `GET` | `/api/recurring-transactions` | `TESTED_PASS_SUCCESS` | 10 - List Recurring Transactions |
| 95 | `RecurringTransactionsController` | `Get` | `GET` | `/api/recurring-transactions/{id}` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 96 | `RecurringTransactionsController` | `Create` | `POST` | `/api/recurring-transactions` | `TESTED_PASS_SUCCESS` | 09 - Create Recurring Transaction (Monthly Internet) |
| 97 | `RecurringTransactionsController` | `Update` | `PUT` | `/api/recurring-transactions/{id}` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 98 | `RecurringTransactionsController` | `Delete` | `DELETE` | `/api/recurring-transactions/{id}` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 99 | `RecurringTransactionsController` | `Pause` | `POST` | `/api/recurring-transactions/{id}/pause` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 100 | `RecurringTransactionsController` | `Resume` | `POST` | `/api/recurring-transactions/{id}/resume` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 101 | `RecurringTransactionsController` | `` | `POST` | `/api/recurring-transactions/{id}/occurrences/{occurrenceId}/record` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 102 | `TransactionRulesController` | `List` | `GET` | `/api/transaction-rules` | `TESTED_PASS_SUCCESS` | 12 - List Transaction Rules |
| 103 | `TransactionRulesController` | `Get` | `GET` | `/api/transaction-rules/{id}` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 104 | `TransactionRulesController` | `Create` | `POST` | `/api/transaction-rules` | `TESTED_PASS_SUCCESS` | 11 - Create Transaction Rule |
| 105 | `TransactionRulesController` | `Update` | `PUT` | `/api/transaction-rules/{id}` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 106 | `TransactionRulesController` | `Delete` | `DELETE` | `/api/transaction-rules/{id}` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |

### 6. Funds Ecosystem (Fixed & Free Groups)

| # | Controller | Action Name | Method | Route | Trạng Thái Kiểm Thử | Tên Test Case trong Postman Collection |
| :---: | :--- | :--- | :---: | :--- | :---: | :--- |
| 107 | `V1FundsController` | `CreateRequest` | `POST` | `/api/v1/funds/{id}/requests` | `TESTED_PASS_SUCCESS` | 07 - User B Creates Free Fund Expense Request |
| 108 | `V1FundsController` | `Requests` | `GET` | `/api/v1/funds/{id}/requests` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 109 | `V1FundsController` | `ReviewRequest` | `POST` | `/api/v1/funds/requests/{id}/review` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 110 | `V1FundsController` | `ReportRequest` | `POST` | `/api/v1/funds/requests/{id}/report` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 111 | `V1FundsController` | `CancelRequest` | `POST` | `/api/v1/funds/requests/{id}/cancel` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 112 | `V1FundsController` | `List` | `GET` | `/api/v1/funds` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 113 | `V1FundsController` | `Create` | `POST` | `/api/v1/funds` | `TESTED_PASS_SUCCESS` | 01 - Create Fixed Fund (User A); 01 - Create Free Fund (User A); Flow D01 - User A Creates Fixed Fund (1,000,000 VND) |
| 114 | `V1FundsController` | `Get` | `GET` | `/api/v1/funds/{id}` | `TESTED_PASS_SUCCESS` | 02 - Get Fixed Fund Details |
| 115 | `V1FundsController` | `Members` | `GET` | `/api/v1/funds/{id}/members` | `TESTED_PASS_SUCCESS` | 05 - List Fixed Fund Members; 10 - Verify Remaining Members in Free Fund |
| 116 | `V1FundsController` | `Governance` | `GET` | `/api/v1/funds/{id}/governance` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 117 | `V1FundsController` | `OwnerResponse` | `GET` | `/api/v1/funds/{id}/owner-response` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 118 | `V1FundsController` | `EscalateOwnerResponse` | `POST` | `/api/v1/funds/{id}/owner-response/escalate` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 119 | `V1FundsController` | `CloseActivity` | `POST` | `/api/v1/funds/{id}/activity/close` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 120 | `V1FundsController` | `ProposeActivity` | `POST` | `/api/v1/funds/{id}/activity-proposals` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 121 | `V1FundsController` | `ActivityProposal` | `GET` | `/api/v1/funds/activity-proposals/{id}` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 122 | `V1FundsController` | `SettleActivity` | `POST` | `/api/v1/funds/activity-proposals/{id}/settle` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 123 | `V1FundsController` | `VoteActivity` | `POST` | `/api/v1/funds/activity-proposals/{id}/vote` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 124 | `V1FundsController` | `OpenElection` | `POST` | `/api/v1/funds/{id}/elections` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 125 | `V1FundsController` | `Election` | `GET` | `/api/v1/funds/elections/{id}` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 126 | `V1FundsController` | `Candidate` | `POST` | `/api/v1/funds/elections/{id}/candidates` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 127 | `V1FundsController` | `Ballot` | `POST` | `/api/v1/funds/elections/{id}/ballots` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 128 | `V1FundsController` | `AdvanceElection` | `POST` | `/api/v1/funds/elections/{id}/advance` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 129 | `V1FundsController` | `AcceptElection` | `POST` | `/api/v1/funds/elections/{id}/accept` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 130 | `V1FundsController` | `RejectElection` | `POST` | `/api/v1/funds/elections/{id}/reject` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 131 | `V1FundsController` | `Allocation` | `GET` | `/api/v1/funds/{id}/contribution-allocation` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 132 | `V1FundsController` | `Finance` | `GET` | `/api/v1/funds/{id}/finance` | `TESTED_PASS_SUCCESS` | 12 - Check Fixed Fund Financial Overview; 06 - Track Free Fund Finance; Flow D07 - Check Fund Finance (Total 1,000,000 VND) |
| 133 | `V1FundsController` | `Confirm` | `POST` | `/api/v1/funds/{id}/obligations/confirm` | `TESTED_PASS_SUCCESS` | 06 - Confirm Obligations (Target: 2,000,000 VND); Flow D04 - Confirm Obligations (Target: 1,000,000 VND) |
| 134 | `V1FundsController` | `Contribute` | `POST` | `/api/v1/funds/{id}/contributions` | `TESTED_PASS_SUCCESS` | 07 - User A Contributes 1,000,000 VND to Fixed Fund; 08 - User B Contributes 1,000,000 VND to Fixed Fund; 04 - User A Contributes 500,000 VND to Free Fund; 05 - User B Contributes 300,000 VND to Free Fund; Flow D05 - User A Contributes 500,000 VND; Flow D06 - User B Contributes 500,000 VND |
| 135 | `V1FundsController` | `Contributions` | `GET` | `/api/v1/funds/{id}/contributions` | `TESTED_PASS_SUCCESS` | 09 - List Fixed Fund Contributions |
| 136 | `V1FundsController` | `Activities` | `GET` | `/api/v1/funds/{id}/activities` | `TESTED_PASS_SUCCESS` | 11 - Check Fixed Fund Activities; Flow D09 - Check Activity Log for Spending Permission Event |
| 137 | `V1FundsController` | `Invite` | `POST` | `/api/v1/funds/{id}/invitations` | `TESTED_PASS_SUCCESS` | 03 - Invite User B to Fixed Fund; 02 - Invite User B to Free Fund; Flow D02 - Invite User B to Fixed Fund |
| 138 | `V1FundsController` | `Invitations` | `GET` | `/api/v1/funds/invitations` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 139 | `V1FundsController` | `Proposals` | `GET` | `/api/v1/funds/{id}/invitation-proposals` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 140 | `V1FundsController` | `Review` | `POST` | `/api/v1/funds/invitations/{id}/review` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 141 | `V1FundsController` | `Decline` | `POST` | `/api/v1/funds/invitations/{id}/decline` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 142 | `V1FundsController` | `Cancel` | `DELETE` | `/api/v1/funds/invitations/{id}` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 143 | `V1FundsController` | `Accept` | `POST` | `/api/v1/funds/invitations/{id}/accept` | `TESTED_PASS_SUCCESS` | 04 - User B Accepts Fixed Fund Invitation; 03 - User B Accepts Free Fund Invitation; Flow D03 - User B Accepts Fixed Fund Invitation |
| 144 | `V1FundsController` | `Permission` | `PUT` | `/api/v1/funds/{id}/members/{userId}/spending-permission` | `TESTED_PASS_SUCCESS` | 10 - Grant Spending Permission to User B; Flow D08 - Grant User B Spending Permission |
| 145 | `V1FundsController` | `Offer` | `POST` | `/api/v1/funds/{id}/ownership-offers` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 146 | `V1FundsController` | `AcceptOffer` | `POST` | `/api/v1/funds/ownership-offers/{id}/accept` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 147 | `V1FundsController` | `OwnershipOffers` | `GET` | `/api/v1/funds/ownership-offers` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 148 | `V1FundsController` | `RefundPreview` | `GET` | `/api/v1/funds/{id}/refund-preview` | `TESTED_PASS_SUCCESS` | 08 - Refund Preview for User B |
| 149 | `V1FundsController` | `Leave` | `POST` | `/api/v1/funds/{id}/leave` | `TESTED_PASS_SUCCESS` | 09 - User B Leaves Free Fund |
| 150 | `V1FundsController` | `Kick` | `POST` | `/api/v1/funds/{id}/members/{userId}/kick` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 151 | `V1FundsController` | `Dissolve` | `POST` | `/api/v1/funds/{id}/dissolve` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |

### 7. Legacy Groups & Community Splits

| # | Controller | Action Name | Method | Route | Trạng Thái Kiểm Thử | Tên Test Case trong Postman Collection |
| :---: | :--- | :--- | :---: | :--- | :---: | :--- |
| 152 | `GroupsController` | `List` | `GET` | `/api/groups` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 153 | `GroupsController` | `Get` | `GET` | `/api/groups/{groupId}` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 154 | `GroupsController` | `Create` | `POST` | `/api/groups` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 155 | `GroupsController` | `Update` | `PUT` | `/api/groups/{groupId}` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 156 | `GroupsController` | `Invite` | `POST` | `/api/groups/{groupId}/invites` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 157 | `GroupsController` | `Invitations` | `GET` | `/api/groups/invites` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 158 | `GroupsController` | `Accept` | `POST` | `/api/groups/invites/{inviteId}/accept` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 159 | `GroupsController` | `Reject` | `POST` | `/api/groups/invites/{inviteId}/reject` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 160 | `GroupsController` | `Remove` | `DELETE` | `/api/groups/{groupId}/members/{memberUserId}` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 161 | `GroupsController` | `TransferOwnership` | `POST` | `/api/groups/{groupId}/ownership/{memberUserId}` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 162 | `GroupsController` | `SetReceivingBank` | `PUT` | `/api/groups/{groupId}/receiving-bank` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 163 | `GroupsController` | `Fund` | `GET` | `/api/groups/{groupId}/fund` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 164 | `GroupsController` | `FundTransactions` | `GET` | `/api/groups/{groupId}/fund/transactions` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 165 | `GroupsController` | `Contribute` | `POST` | `/api/groups/{groupId}/fund/contributions` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 166 | `GroupsController` | `FundRequests` | `GET` | `/api/groups/{groupId}/fund-requests` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 167 | `GroupsController` | `CreateFundRequest` | `POST` | `/api/groups/{groupId}/fund-requests` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 168 | `GroupsController` | `PayFundRequest` | `POST` | `/api/groups/{groupId}/fund-requests/{requestId}/pay` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 169 | `GroupsController` | `FundRounds` | `GET` | `/api/groups/{groupId}/fund-rounds` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 170 | `GroupsController` | `CreateFundRound` | `POST` | `/api/groups/{groupId}/fund-rounds` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 171 | `GroupsController` | `PayFundRound` | `POST` | `/api/groups/{groupId}/fund-rounds/{roundId}/pay` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 172 | `GroupsController` | `Expenses` | `GET` | `/api/groups/{groupId}/expenses` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 173 | `GroupsController` | `CreateExpense` | `POST` | `/api/groups/{groupId}/expenses` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 174 | `GroupsController` | `Activities` | `GET` | `/api/groups/{groupId}/activities` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 175 | `GroupsController` | `Deletion` | `GET` | `/api/groups/{groupId}/deletion-request` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 176 | `GroupsController` | `RequestDeletion` | `POST` | `/api/groups/{groupId}/deletion-requests` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 177 | `GroupsController` | `VoteDeletion` | `POST` | `/api/groups/{groupId}/deletion-requests/{requestId}/vote` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 178 | `GroupsController` | `CancelDeletion` | `POST` | `/api/groups/{groupId}/deletion-requests/{requestId}/cancel` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 179 | `GroupsController` | `CreateRefundPayment` | `POST` | `/api/groups/{groupId}/deletion-requests/{requestId}/refunds/{refundId}/payment` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 180 | `GroupsController` | `FinalizeDeletion` | `POST` | `/api/groups/{groupId}/deletion-requests/{requestId}/finalize` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |

### 8. Notifications, Support & Operations

| # | Controller | Action Name | Method | Route | Trạng Thái Kiểm Thử | Tên Test Case trong Postman Collection |
| :---: | :--- | :--- | :---: | :--- | :---: | :--- |
| 181 | `CasesController` | `Dispute` | `POST` | `/api/cases/disputes` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 182 | `CasesController` | `Support` | `POST` | `/api/cases/support` | `TESTED_PASS_SUCCESS` | 04 - Create Support Case |
| 183 | `CasesController` | `SupportCases` | `GET` | `/api/cases/support` | `TESTED_PASS_SUCCESS` | 05 - List Support Cases |
| 184 | `CasesController` | `SupportCase` | `GET` | `/api/cases/support/{id}` | `TESTED_PASS_SUCCESS` | 06 - Get Support Case Details |
| 185 | `CasesController` | `ReplySupport` | `POST` | `/api/cases/support/{id}/messages` | `TESTED_PASS_SUCCESS` | 07 - Reply to Support Case Message |
| 186 | `CasesController` | `ReplySupportMultipart` | `POST` | `/api/cases/support/{id}/messages/multipart` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 187 | `CasesController` | `Disputes` | `GET` | `/api/cases/disputes` | `TESTED_PASS_SUCCESS` | 08 - List Financial Disputes |
| 188 | `CasesController` | `Dispute` | `GET` | `/api/cases/disputes/{id}` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 189 | `CasesController` | `ReplyDispute` | `POST` | `/api/cases/disputes/{id}/messages` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 190 | `CasesController` | `ReplyDisputeMultipart` | `POST` | `/api/cases/disputes/{id}/messages/multipart` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 191 | `CasesController` | `Attachment` | `GET` | `/api/cases/attachments/{id}` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 192 | `MobilePushController` | `Register` | `PUT` | `/api/mobile/push/installations/{installationId}` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 193 | `MobilePushController` | `Unregister` | `DELETE` | `/api/mobile/push/installations/{installationId}` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 194 | `NotificationsController` | `List` | `GET` | `/api/notifications` | `TESTED_PASS_SUCCESS` | 01 - List Notifications for User A |
| 195 | `NotificationsController` | `Read` | `POST` | `/api/notifications/{id}/read` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 196 | `NotificationsController` | `ReadAll` | `POST` | `/api/notifications/read-all` | `TESTED_PASS_SUCCESS` | 02 - Mark All Notifications Read |
| 197 | `NotificationsController` | `Search` | `GET` | `/api/notifications/search` | `TESTED_PASS_SUCCESS` | 03 - Search Notifications |
| 198 | `NotificationsController` | `Delete` | `DELETE` | `/api/notifications/{id}` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 199 | `NotificationsController` | `Clear` | `DELETE` | `/api/notifications` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 200 | `NotificationsController` | `Preferences` | `GET` | `/api/notifications/preferences` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 201 | `NotificationsController` | `SavePreference` | `PUT` | `/api/notifications/preferences` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 202 | `OperationsController` | `Maintenance` | `GET` | `/api/system/maintenance` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 203 | `OperationsController` | `ActiveAnnouncement` | `GET` | `/api/system/announcements/active` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 204 | `V1ActivityController` | `Record` | `POST` | `/api/v1/activity` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |

### 9. AI Assistant & Financial Advisory

| # | Controller | Action Name | Method | Route | Trạng Thái Kiểm Thử | Tên Test Case trong Postman Collection |
| :---: | :--- | :--- | :---: | :--- | :---: | :--- |
| 205 | `AiController` | `List` | `GET` | `/api/ai/conversations` | `TESTED_PASS_SUCCESS` | 02 - List AI Conversations |
| 206 | `AiController` | `Get` | `GET` | `/api/ai/conversations/{id}` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 207 | `AiController` | `Create` | `POST` | `/api/ai/conversations` | `TESTED_PASS_SUCCESS` | 01 - Create AI Conversation |
| 208 | `AiController` | `Update` | `PUT` | `/api/ai/conversations/{id}` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 209 | `AiController` | `Delete` | `DELETE` | `/api/ai/conversations/{id}` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 210 | `AiController` | `Chat` | `POST` | `/api/ai/chat` | `TESTED_PASS_FALLBACK` | 03 - Ask AI Financial Advisor (Chat) - Fallback Safe |
| 211 | `AiController` | `UploadAttachment` | `POST` | `/api/ai/attachments` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 212 | `AiController` | `DownloadAttachment` | `GET` | `/api/ai/attachments/{id}` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 213 | `AiController` | `GetAction` | `GET` | `/api/ai/actions/{id}` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 214 | `AiController` | `ExecuteAction` | `POST` | `/api/ai/actions/{id}/execute` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
| 215 | `AiController` | `CancelAction` | `POST` | `/api/ai/actions/{id}/cancel` | **`NOT_TESTED`** | *(Chưa có kịch bản test)* |
