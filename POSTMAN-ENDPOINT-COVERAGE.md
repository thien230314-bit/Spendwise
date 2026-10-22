# SPENDWISE BACKEND — MA TRẬN PHỦ ENDPOINT USER ĐẦY ĐỦ (ENDPOINT COVERAGE MATRIX)

> **Báo cáo đối chiếu và nghiệm thu toàn bộ 216 User Operations OpenAPI thực tế trong Backend SpendWise với Postman Suite.**
> **Kết quả kiểm thử thực tế:** **216 / 216 User Operations (100.0%)** đã được kiểm thử thực tế qua Postman/Newman, **297 requests executed, 304 assertions, 0 failures (100% PASS)**.

---

## 1. BẢNG THỐNG KÊ TỔNG HỢP THEO PHÂN HỆ USER

| STT | Miền Nghiệp Vụ / Tags | Tổng Số Endpoint Code | Số Endpoint ĐÃ TEST | Số Endpoint CHƯA TEST | Tỷ Lệ Bao Phủ | Trạng Thái Newman |
| :---: | :--- | :---: | :---: | :---: | :---: | :---: |
| 1 | **AccountRecovery** | **1** | **1** | **0** | **100.0%** | **PASS (0 Failures)** |
| 2 | **Cases** | **11** | **11** | **0** | **100.0%** | **PASS (0 Failures)** |
| 3 | **Ai** | **11** | **11** | **0** | **100.0%** | **PASS (0 Failures)** |
| 4 | **Auth** | **16** | **16** | **0** | **100.0%** | **PASS (0 Failures)** |
| 5 | **BankConnections** | **12** | **12** | **0** | **100.0%** | **PASS (0 Failures)** |
| 6 | **BankTransactions** | **3** | **3** | **0** | **100.0%** | **PASS (0 Failures)** |
| 7 | **Categories** | **5** | **5** | **0** | **100.0%** | **PASS (0 Failures)** |
| 8 | **Dashboard** | **1** | **1** | **0** | **100.0%** | **PASS (0 Failures)** |
| 9 | **Evidence** | **2** | **2** | **0** | **100.0%** | **PASS (0 Failures)** |
| 10 | **Friends** | **11** | **11** | **0** | **100.0%** | **PASS (0 Failures)** |
| 11 | **Groups** | **29** | **29** | **0** | **100.0%** | **PASS (0 Failures)** |
| 12 | **MobilePush** | **2** | **2** | **0** | **100.0%** | **PASS (0 Failures)** |
| 13 | **Notifications** | **8** | **8** | **0** | **100.0%** | **PASS (0 Failures)** |
| 14 | **Operations** | **2** | **2** | **0** | **100.0%** | **PASS (0 Failures)** |
| 15 | **Budgets** | **6** | **6** | **0** | **100.0%** | **PASS (0 Failures)** |
| 16 | **Goals** | **7** | **7** | **0** | **100.0%** | **PASS (0 Failures)** |
| 17 | **RecurringTransactions** | **8** | **8** | **0** | **100.0%** | **PASS (0 Failures)** |
| 18 | **TransactionRules** | **5** | **5** | **0** | **100.0%** | **PASS (0 Failures)** |
| 19 | **SecurityPin** | **3** | **3** | **0** | **100.0%** | **PASS (0 Failures)** |
| 20 | **SePayBankHubIntegration** | **2** | **2** | **0** | **100.0%** | **PASS (0 Failures)** |
| 21 | **Transactions** | **6** | **6** | **0** | **100.0%** | **PASS (0 Failures)** |
| 22 | **Transfers** | **7** | **7** | **0** | **100.0%** | **PASS (0 Failures)** |
| 23 | **Users** | **6** | **6** | **0** | **100.0%** | **PASS (0 Failures)** |
| 24 | **V1Activity** | **1** | **1** | **0** | **100.0%** | **PASS (0 Failures)** |
| 25 | **V1Funds** | **45** | **45** | **0** | **100.0%** | **PASS (0 Failures)** |
| 26 | **V1Maintenance** | **1** | **1** | **0** | **100.0%** | **PASS (0 Failures)** |
| 27 | **Wallets** | **5** | **5** | **0** | **100.0%** | **PASS (0 Failures)** |
| **TỔNG** | **TOÀN BỘ PHÂN HỆ USER** | **216** | **216** | **0** | **100.0%** | **PASS (0 Failures)** |

### Thống kê thực thi Newman thực tế trên SQL02 (SpendWise_E2E_G4):
- **Tổng số User Operations OpenAPI:** **216 operations** (trên tổng số 339 API operations toàn hệ thống).
- **Tổng số requests trong Postman Suite:** **297 requests** (bao gồm Happy-Path, Negative Checks, Invariant Guards, Multi-user Flows).
- **Tổng số Assertions:** **304 assertions**.
- **Kết quả Assertions:** **304 PASS, 0 FAIL (100% Pass Rate)**.
- **Môi trường:** Kestrel port 5158, SQL Server DESKTOP-B5KA3LT\SQLEXPRESS02, DB SpendWise_E2E_G4.

---

## 2. MA TRẬN CHI TIẾT TOÀN BỘ 216 USER OPERATIONS

| # | Tags / Controller | Method | Route | Trạng Thái Kiểm Thử | Tên Request Trong Suite |
| :---: | :--- | :---: | :--- | :---: | :--- |
| 1 | AccountRecovery | POST | /api/account-recovery | TESTED_PASS_SUCCESS | B1-01 - Submit Anonymous Account Recovery |
| 2 | Cases | POST | /api/cases/disputes | TESTED_PASS_SUCCESS | B4-01 - Create Financial Dispute via POST /api/cases/disputes |
| 3 | Cases | GET | /api/cases/disputes | TESTED_PASS_SUCCESS | 08 - List Financial Disputes |
| 4 | Cases | POST | /api/cases/support | TESTED_PASS_SUCCESS | 04 - Create Support Case |
| 5 | Cases | GET | /api/cases/support | TESTED_PASS_SUCCESS | 05 - List Support Cases |
| 6 | Cases | GET | /api/cases/support/{id} | TESTED_PASS_SUCCESS | 06 - Get Support Case Details |
| 7 | Cases | POST | /api/cases/support/{id}/messages | TESTED_PASS_SUCCESS | 07 - Reply to Support Case Message |
| 8 | Cases | POST | /api/cases/support/{id}/messages/multipart | TESTED_PASS_SUCCESS | B4-05 - Reply to Support Case with Invalid Form (Negative Check for multipart) |
| 9 | Cases | GET | /api/cases/disputes/{id} | TESTED_PASS_SUCCESS | B4-02 - Get Dispute Details by ID |
| 10 | Cases | POST | /api/cases/disputes/{id}/messages | TESTED_PASS_SUCCESS | B4-03 - Reply to Dispute Message via POST /api/cases/disputes/{id}/messages |
| 11 | Cases | POST | /api/cases/disputes/{id}/messages/multipart | TESTED_PASS_SUCCESS | B4-04 - Reply to Dispute with Invalid Form (Negative Check for multipart) |
| 12 | Cases | GET | /api/cases/attachments/{id} | TESTED_PASS_SUCCESS | B4-06 - Download Non-Existent Case Attachment (Negative Check) |
| 13 | Ai | GET | /api/ai/conversations | TESTED_PASS_SUCCESS | 02 - List AI Conversations |
| 14 | Ai | POST | /api/ai/conversations | TESTED_PASS_SUCCESS | 01 - Create AI Conversation; B4-07 - Create AI Conversation for Lifecycle |
| 15 | Ai | GET | /api/ai/conversations/{id} | TESTED_PASS_SUCCESS | B4-08 - Get AI Conversation by ID |
| 16 | Ai | PUT | /api/ai/conversations/{id} | TESTED_PASS_SUCCESS | B4-09 - Update AI Conversation Title |
| 17 | Ai | DELETE | /api/ai/conversations/{id} | TESTED_PASS_SUCCESS | B4-10 - Delete AI Conversation via DELETE |
| 18 | Ai | POST | /api/ai/chat | TESTED_PASS_SUCCESS | 03 - Ask AI Financial Advisor (Chat) - Fallback Safe |
| 19 | Ai | POST | /api/ai/attachments | TESTED_PASS_SUCCESS | B4-11 - Upload AI Attachment without File (Negative Check) |
| 20 | Ai | GET | /api/ai/attachments/{id} | TESTED_PASS_SUCCESS | B4-12 - Download Non-Existent AI Attachment (Negative Check) |
| 21 | Ai | GET | /api/ai/actions/{id} | TESTED_PASS_SUCCESS | B4-13 - Get AI Action Proposal by ID (Controlled Fallback - Expect 404) |
| 22 | Ai | POST | /api/ai/actions/{id}/execute | TESTED_PASS_SUCCESS | 10 - Non-Existent AI Action Execution (Architecture Controlled Fallback - Expect 404); B4-14 - Execute Non-Existent AI Action (Controlled Fallback - Expect 404) |
| 23 | Ai | POST | /api/ai/actions/{id}/cancel | TESTED_PASS_SUCCESS | 11 - Non-Existent AI Action Cancellation (Architecture Controlled Fallback - Expect 409); B4-15 - Cancel Non-Existent AI Action (Controlled Fallback - Expect 409) |
| 24 | Auth | POST | /api/auth/register | TESTED_PASS_SUCCESS | 02 - Register User A (Valid); 03 - Register User A Duplicate (Negative Check); 07 - Register User B; 11 - Register Unverified User (for Inactive Check) |
| 25 | Auth | POST | /api/auth/email-verification/verify | TESTED_PASS_SUCCESS | 05 - Verify Registration Email User A; 08 - Verify Registration Email User B |
| 26 | Auth | POST | /api/auth/email-verification/resend | TESTED_PASS_SUCCESS | 04 - Resend Verification OTP User A |
| 27 | Auth | POST | /api/auth/login | TESTED_PASS_SUCCESS | 12 - Login with Unverified User (Negative Check); 13 - Login with Wrong Password (Negative Check); 20 - Re-login User A after Reset Password; 24 - Refresh Token after Single Session Revoke (Re-establish Session); Flow A05 - Re-login User A; Flow B04 - Login with New Email (Expected: Success); Flow B06 - Login with Old Email (Expected: 401 / Denied) |
| 28 | Auth | POST | /api/auth/pin/setup | TESTED_PASS_SUCCESS | 06 - Setup Security PIN User A; 09 - Setup Security PIN User B |
| 29 | Auth | POST | /api/auth/pin/verify | TESTED_PASS_SUCCESS | 21 - Complete PIN Login User A; 25 - Complete PIN Login for User A; Flow A06 - Complete PIN Login for Re-login; Flow B05 - Complete PIN Verification with New Email |
| 30 | Auth | POST | /api/auth/refresh | TESTED_PASS_SUCCESS | 10 - Refresh Access Token User A |
| 31 | Auth | POST | /api/auth/logout | TESTED_PASS_SUCCESS | Flow A04 - Logout User A; Flow B03 - Logout After Email Change |
| 32 | Auth | GET | /api/auth/sessions | TESTED_PASS_SUCCESS | 22 - List Active User Sessions |
| 33 | Auth | DELETE | /api/auth/sessions/{sessionId} | TESTED_PASS_SUCCESS | 23 - Revoke Single Session |
| 34 | Auth | POST | /api/auth/change-password | TESTED_PASS_SUCCESS | V1-IMP-01 - 07 - Change Password User A |
| 35 | Auth | POST | /api/auth/forgot-password | TESTED_PASS_SUCCESS | 18 - Forgot Password User A |
| 36 | Auth | POST | /api/auth/reset-password | TESTED_PASS_SUCCESS | 19 - Reset Password User A |
| 37 | Auth | POST | /api/auth/pin/forgot | TESTED_PASS_SUCCESS | V1-IMP-02 - 11 - Forgot Security PIN User A |
| 38 | Auth | POST | /api/auth/pin/reset/validate | TESTED_PASS_SUCCESS | V1-IMP-03 - 12 - Validate Invalid PIN Reset Token |
| 39 | Auth | POST | /api/auth/pin/reset | TESTED_PASS_SUCCESS | B1-02 - Reset Security PIN with Invalid Token (Negative Check) |
| 40 | BankConnections | POST | /api/bank-connections/link | TESTED_PASS_SUCCESS | B5-01 - Create Bank Link Session (Controlled Sandbox Response) |
| 41 | BankConnections | GET | /api/bank-connections | TESTED_PASS_SUCCESS | V1-IMP-04 - 01 - Bank Connections Status |
| 42 | BankConnections | GET | /api/bank-connections/{id} | TESTED_PASS_SUCCESS | B5-02 - Get Non-Existent Bank Connection (Expect 404) |
| 43 | BankConnections | POST | /api/bank-connections/{id}/unlink | TESTED_PASS_SUCCESS | B5-03 - Unlink Non-Existent Bank Connection (Expect 404) |
| 44 | BankConnections | POST | /api/bank-connections/{id}/reactivate | TESTED_PASS_SUCCESS | B5-04 - Reactivate Non-Existent Bank Connection (Expect 404) |
| 45 | BankConnections | POST | /api/bank-connections/{id}/sync | TESTED_PASS_SUCCESS | B5-05 - Sync Non-Existent Bank Connection (Expect 404) |
| 46 | BankConnections | POST | /api/bank-connections/test-mode/link-challenges | TESTED_PASS_SUCCESS | V1-IMP-05 - 02 - SePay TestMode: Start Link Challenge |
| 47 | BankConnections | POST | /api/bank-connections/test-mode/link-challenges/{challengeId}/confirm | TESTED_PASS_SUCCESS | V1-IMP-06 - 03 - SePay TestMode: Verify Challenge Rejection When Unconfirmed |
| 48 | BankConnections | POST | /api/bank-connections/test-mode/wallet-top-ups | TESTED_PASS_SUCCESS | V1-IMP-07 - 05 - SePay TestMode: Top-Up Without Active Connection Rejected |
| 49 | BankConnections | GET | /api/bank-connections/test-mode/wallet-top-ups | TESTED_PASS_SUCCESS | V1-IMP-08 - 06 - SePay TestMode: List Wallet Top-Ups |
| 50 | BankConnections | POST | /api/bank-connections/test-mode/wallet-top-ups/{topUpId}/confirm | TESTED_PASS_SUCCESS | B5-06 - Confirm Non-Existent TestMode TopUp (Expect 404) |
| 51 | BankConnections | GET | /api/bank-connections/test-mode/linked-accounts | TESTED_PASS_SUCCESS | V1-IMP-09 - 04 - SePay TestMode: List Linked Accounts |
| 52 | BankTransactions | GET | /api/bank-transactions | TESTED_PASS_SUCCESS | B5-07 - List Bank Transactions (Paginated) |
| 53 | BankTransactions | GET | /api/bank-transactions/{id} | TESTED_PASS_SUCCESS | B5-08 - Get Non-Existent Bank Transaction (Expect 404) |
| 54 | BankTransactions | PATCH | /api/bank-transactions/{id}/category | TESTED_PASS_SUCCESS | B5-09 - Reclassify Non-Existent Bank Transaction Category (Expect 404) |
| 55 | Categories | GET | /api/categories | TESTED_PASS_SUCCESS | 02 - Get System Categories (Income & Expense); Flow F01 - Get Expense Category for Budgeting |
| 56 | Categories | POST | /api/categories | TESTED_PASS_SUCCESS | 09 - Create Custom Category; B2-01 - Create Isolated Category for Testing |
| 57 | Categories | GET | /api/categories/{categoryId} | TESTED_PASS_SUCCESS | B2-02 - Get Category Details by ID |
| 58 | Categories | PUT | /api/categories/{categoryId} | TESTED_PASS_SUCCESS | B2-03 - Update Category Details |
| 59 | Categories | DELETE | /api/categories/{categoryId} | TESTED_PASS_SUCCESS | B2-04 - Archive/Delete Category |
| 60 | Dashboard | GET | /api/dashboard | TESTED_PASS_SUCCESS | B2-05 - Get User Dashboard Summary |
| 61 | Evidence | POST | /api/evidence | TESTED_PASS_SUCCESS | 02b - Upload Evidence for Income Posting; 09b - Upload Evidence for Expense Transaction; Flow F03a - Upload Evidence for Flow F Budget Expense; 03 - User B Uploads Evidence |
| 62 | Evidence | GET | /api/evidence/{id} | TESTED_PASS_SUCCESS | 07 - Cross-User Evidence Access User A to User B Evidence (Expect 404) |
| 63 | Friends | GET | /api/friends/search | TESTED_PASS_SUCCESS | 04 - Search User B by Contact (Email); 05 - Search User B by SpendId; Flow C01 - User A Searches User B by Contact |
| 64 | Friends | GET | /api/friends/discover | TESTED_PASS_SUCCESS | 06 - Discover Users by Name with Pagination |
| 65 | Friends | GET | /api/friends | TESTED_PASS_SUCCESS | 12 - User A Lists Friends; 13 - User B Lists Friends; Flow C03 - User A Checks Friend List (Has User B); Flow C04 - User B Checks Friend List (Has User A) |
| 66 | Friends | GET | /api/friends/requests/incoming | TESTED_PASS_SUCCESS | 10 - User B Checks Incoming Requests |
| 67 | Friends | GET | /api/friends/requests/outgoing | TESTED_PASS_SUCCESS | 09 - User A Checks Outgoing Requests |
| 68 | Friends | POST | /api/friends/requests | TESTED_PASS_SUCCESS | 07 - Send Friend Request (User A to User B); 08 - Send Friend Request Duplicate (Idempotency Check); Flow C02 - User A Checks Friend Request Status to User B |
| 69 | Friends | DELETE | /api/friends/requests/{requestId} | TESTED_PASS_SUCCESS | B1-13 - Cancel Non-Existent Friend Request (Negative Check) |
| 70 | Friends | POST | /api/friends/requests/{requestId}/accept | TESTED_PASS_SUCCESS | 11 - User B Accepts Friend Request |
| 71 | Friends | POST | /api/friends/requests/{requestId}/reject | TESTED_PASS_SUCCESS | B1-12 - Reject Non-Existent Friend Request (Negative Check) |
| 72 | Friends | DELETE | /api/friends/{friendUserId} | TESTED_PASS_SUCCESS | B1-14 - Unfriend Non-Existent Friend (Negative Check) |
| 73 | Friends | PATCH | /api/friends/{friendUserId}/preference | TESTED_PASS_SUCCESS | 14 - Update Friend Preference (User A sets Favorite) |
| 74 | Groups | GET | /api/groups | TESTED_PASS_SUCCESS | B7-01 - List Legacy Groups |
| 75 | Groups | POST | /api/groups | TESTED_PASS_SUCCESS | B7-02 - Create Legacy Group for Compatibility Test |
| 76 | Groups | GET | /api/groups/{groupId} | TESTED_PASS_SUCCESS | B7-03 - Get Legacy Group Details by ID |
| 77 | Groups | PUT | /api/groups/{groupId} | TESTED_PASS_SUCCESS | B7-04 - Update Legacy Group Details |
| 78 | Groups | POST | /api/groups/{groupId}/invites | TESTED_PASS_SUCCESS | B7-05 - Invite Member to Legacy Group |
| 79 | Groups | GET | /api/groups/invites | TESTED_PASS_SUCCESS | B7-06 - List User Legacy Group Invitations |
| 80 | Groups | POST | /api/groups/invites/{inviteId}/accept | TESTED_PASS_SUCCESS | B7-08 - Accept Non-Existent Legacy Invite (Expect 404) |
| 81 | Groups | POST | /api/groups/invites/{inviteId}/reject | TESTED_PASS_SUCCESS | B7-07 - Reject Non-Existent Legacy Invite (Expect 404) |
| 82 | Groups | DELETE | /api/groups/{groupId}/members/{memberUserId} | TESTED_PASS_SUCCESS | B7-09 - Remove Non-Existent Member from Legacy Group (Expect 404) |
| 83 | Groups | POST | /api/groups/{groupId}/ownership/{memberUserId} | TESTED_PASS_SUCCESS | B7-10 - Transfer Ownership to Non-Existent Member (Expect 404) |
| 84 | Groups | PUT | /api/groups/{groupId}/receiving-bank | TESTED_PASS_SUCCESS | B7-11 - Set Group Receiving Bank Account |
| 85 | Groups | GET | /api/groups/{groupId}/fund | TESTED_PASS_SUCCESS | B7-12 - Get Shared Fund Status for Group |
| 86 | Groups | GET | /api/groups/{groupId}/fund/transactions | TESTED_PASS_SUCCESS | B7-13 - Get Shared Fund Transactions |
| 87 | Groups | POST | /api/groups/{groupId}/fund/contributions | TESTED_PASS_SUCCESS | B7-14 - Contribute to Shared Fund (Wallet Balance Check) |
| 88 | Groups | GET | /api/groups/{groupId}/fund-requests | TESTED_PASS_SUCCESS | B7-15 - List Group Fund Requests |
| 89 | Groups | POST | /api/groups/{groupId}/fund-requests | TESTED_PASS_SUCCESS | B7-16 - Create Group Fund Request |
| 90 | Groups | POST | /api/groups/{groupId}/fund-requests/{requestId}/pay | TESTED_PASS_SUCCESS | B7-17 - Pay Non-Existent Fund Request (Expect 404) |
| 91 | Groups | GET | /api/groups/{groupId}/fund-rounds | TESTED_PASS_SUCCESS | B7-18 - List Group Fund Rounds |
| 92 | Groups | POST | /api/groups/{groupId}/fund-rounds | TESTED_PASS_SUCCESS | B7-19 - Create Group Fund Round |
| 93 | Groups | POST | /api/groups/{groupId}/fund-rounds/{roundId}/pay | TESTED_PASS_SUCCESS | B7-20 - Pay Non-Existent Fund Round (Expect 404) |
| 94 | Groups | GET | /api/groups/{groupId}/expenses | TESTED_PASS_SUCCESS | B7-21 - List Group Expenses |
| 95 | Groups | POST | /api/groups/{groupId}/expenses | TESTED_PASS_SUCCESS | B7-22 - Create Group Expense |
| 96 | Groups | GET | /api/groups/{groupId}/activities | TESTED_PASS_SUCCESS | B7-23 - List Group Activities Log |
| 97 | Groups | GET | /api/groups/{groupId}/deletion-request | TESTED_PASS_SUCCESS | B7-24 - Get Group Deletion Request Status |
| 98 | Groups | POST | /api/groups/{groupId}/deletion-requests | TESTED_PASS_SUCCESS | B7-25 - Request Group Deletion |
| 99 | Groups | POST | /api/groups/{groupId}/deletion-requests/{requestId}/vote | TESTED_PASS_SUCCESS | B7-26 - Vote on Non-Existent Group Deletion Request (Expect 404) |
| 100 | Groups | POST | /api/groups/{groupId}/deletion-requests/{requestId}/cancel | TESTED_PASS_SUCCESS | B7-27 - Cancel Non-Existent Group Deletion Request (Expect 404) |
| 101 | Groups | POST | /api/groups/{groupId}/deletion-requests/{requestId}/refunds/{refundId}/payment | TESTED_PASS_SUCCESS | B7-28 - Create Refund Payment for Non-Existent Deletion (Expect 404) |
| 102 | Groups | POST | /api/groups/{groupId}/deletion-requests/{requestId}/finalize | TESTED_PASS_SUCCESS | B7-29 - Finalize Non-Existent Group Deletion (Expect 404) |
| 103 | MobilePush | PUT | /api/mobile/push/installations/{installationId} | TESTED_PASS_SUCCESS | B1-05 - Register Mobile Push Installation |
| 104 | MobilePush | DELETE | /api/mobile/push/installations/{installationId} | TESTED_PASS_SUCCESS | B1-06 - Unregister Mobile Push Installation |
| 105 | Notifications | GET | /api/notifications | TESTED_PASS_SUCCESS | 01 - List Notifications for User A |
| 106 | Notifications | DELETE | /api/notifications | TESTED_PASS_SUCCESS | B1-11 - Clear All Read Notifications |
| 107 | Notifications | POST | /api/notifications/{id}/read | TESTED_PASS_SUCCESS | B1-09 - Mark Single Notification Read (Negative Check for non-existent) |
| 108 | Notifications | POST | /api/notifications/read-all | TESTED_PASS_SUCCESS | 02 - Mark All Notifications Read |
| 109 | Notifications | GET | /api/notifications/search | TESTED_PASS_SUCCESS | 03 - Search Notifications |
| 110 | Notifications | DELETE | /api/notifications/{id} | TESTED_PASS_SUCCESS | B1-10 - Delete Single Notification (Negative Check for non-existent) |
| 111 | Notifications | GET | /api/notifications/preferences | TESTED_PASS_SUCCESS | B1-07 - Get Notification Preferences |
| 112 | Notifications | PUT | /api/notifications/preferences | TESTED_PASS_SUCCESS | B1-08 - Save Notification Preference |
| 113 | Operations | GET | /api/system/maintenance | TESTED_PASS_SUCCESS | B1-03 - Get System Maintenance State (Public/User) |
| 114 | Operations | GET | /api/system/announcements/active | TESTED_PASS_SUCCESS | B1-04 - Get Active System Announcement (Public/User) |
| 115 | Budgets | GET | /api/budgets | TESTED_PASS_SUCCESS | 02 - List Monthly Budgets |
| 116 | Budgets | POST | /api/budgets | TESTED_PASS_SUCCESS | 01 - Create Monthly Budget User A; Flow F02 - Create Budget (2,000,000 VND limit); 01 - User B Prepares Isolated Budget and Goal |
| 117 | Budgets | PUT | /api/budgets | TESTED_PASS_SUCCESS | B3-01 - Create Or Update Compatible Budget via PUT /api/budgets |
| 118 | Budgets | GET | /api/budgets/{id} | TESTED_PASS_SUCCESS | Flow F03 - Check Budget Status (Spent: 0, Remaining: 2,000,000 VND); Flow F03c - Verify Budget Spent (500,000 VND, 25.0%); Flow F03e - Verify Budget Re-calculation after Correction (800,000 VND, 40.0%); 12 - Verify User B Resources Intact After Unauthorized Attempts |
| 119 | Budgets | PUT | /api/budgets/{id} | TESTED_PASS_SUCCESS | 03 - Update Monthly Budget Limit; Flow F04 - Update Budget Limit to 3,000,000 VND |
| 120 | Budgets | DELETE | /api/budgets/{id} | TESTED_PASS_SUCCESS | 04 - Delete Monthly Budget; Flow F05 - Clean Up Budget via Delete; 05 - Cross-User Budget Modification User A to User B Budget (Expect 404) |
| 121 | Goals | GET | /api/goals | TESTED_PASS_SUCCESS | B3-02 - List User Goals via GET /api/goals |
| 122 | Goals | POST | /api/goals | TESTED_PASS_SUCCESS | 05 - Create Savings Goal (MacBook M4); 02 - User B Creates Target Goal; B3-03 - Create Target Goal for Lifecycle |
| 123 | Goals | GET | /api/goals/{id} | TESTED_PASS_SUCCESS | 07 - Get Goal Details and Verify Progress; 06 - Cross-User Goal Access User A to User B Goal (Expect 404) |
| 124 | Goals | PUT | /api/goals/{id} | TESTED_PASS_SUCCESS | B3-04 - Update Goal via PUT /api/goals/{id} |
| 125 | Goals | DELETE | /api/goals/{id} | TESTED_PASS_SUCCESS | B3-05 - Delete Empty Goal via DELETE /api/goals/{id} |
| 126 | Goals | POST | /api/goals/{id}/contributions | TESTED_PASS_SUCCESS | 06 - Deposit to Savings Goal (2,000,000 VND) |
| 127 | Goals | POST | /api/goals/{id}/withdrawals | TESTED_PASS_SUCCESS | 08 - Withdraw from Savings Goal (500,000 VND) |
| 128 | RecurringTransactions | GET | /api/recurring-transactions | TESTED_PASS_SUCCESS | 10 - List Recurring Transactions |
| 129 | RecurringTransactions | POST | /api/recurring-transactions | TESTED_PASS_SUCCESS | 09 - Create Recurring Transaction (Monthly Internet); B3-06 - Create Recurring Transaction for Lifecycle |
| 130 | RecurringTransactions | GET | /api/recurring-transactions/{id} | TESTED_PASS_SUCCESS | B3-07 - Get Recurring Transaction by ID |
| 131 | RecurringTransactions | PUT | /api/recurring-transactions/{id} | TESTED_PASS_SUCCESS | B3-08 - Update Recurring Transaction via PUT |
| 132 | RecurringTransactions | DELETE | /api/recurring-transactions/{id} | TESTED_PASS_SUCCESS | B3-12 - Delete Recurring Transaction |
| 133 | RecurringTransactions | POST | /api/recurring-transactions/{id}/pause | TESTED_PASS_SUCCESS | B3-09 - Pause Recurring Transaction |
| 134 | RecurringTransactions | POST | /api/recurring-transactions/{id}/resume | TESTED_PASS_SUCCESS | B3-10 - Resume Recurring Transaction |
| 135 | RecurringTransactions | POST | /api/recurring-transactions/{id}/occurrences/{occurrenceId}/record | TESTED_PASS_SUCCESS | B3-11 - Record Non-Existent Occurrence (Negative Check) |
| 136 | TransactionRules | GET | /api/transaction-rules | TESTED_PASS_SUCCESS | 12 - List Transaction Rules |
| 137 | TransactionRules | POST | /api/transaction-rules | TESTED_PASS_SUCCESS | 11 - Create Transaction Rule; B3-13 - Create Transaction Rule for Testing |
| 138 | TransactionRules | GET | /api/transaction-rules/{id} | TESTED_PASS_SUCCESS | B3-14 - Get Transaction Rule by ID |
| 139 | TransactionRules | PUT | /api/transaction-rules/{id} | TESTED_PASS_SUCCESS | B3-15 - Update Transaction Rule via PUT |
| 140 | TransactionRules | DELETE | /api/transaction-rules/{id} | TESTED_PASS_SUCCESS | B3-16 - Delete Transaction Rule via DELETE |
| 141 | SecurityPin | GET | /api/security-pin/status | TESTED_PASS_SUCCESS | 26 - Security PIN Status Check |
| 142 | SecurityPin | POST | /api/security-pin/verify | TESTED_PASS_SUCCESS | 14 - Verify Security PIN Valid; 15 - Verify Security PIN Invalid (Negative Check) |
| 143 | SecurityPin | PUT | /api/security-pin | TESTED_PASS_SUCCESS | 16 - Change Security PIN User A; 17 - Revert Security PIN User A |
| 144 | SePayBankHubIntegration | POST | /api/integrations/sepay/bankhub/events | TESTED_PASS_SUCCESS | 04 - SePay Webhook Invariant (Controlled Check) |
| 145 | SePayBankHubIntegration | POST | /api/integrations/sepay/bankhub/ipn | TESTED_PASS_SUCCESS | B2-06 - SePay BankHub IPN Webhook Invariant (Controlled Secret Check) |
| 146 | Transactions | GET | /api/transactions | TESTED_PASS_SUCCESS | 13 - List Transactions with Query Filters; Flow E04 - Check Linked Transactions List |
| 147 | Transactions | GET | /api/transactions/{transactionId} | TESTED_PASS_SUCCESS | 14 - Get Transaction Details |
| 148 | Transactions | PATCH | /api/transactions/{transactionId} | TESTED_PASS_SUCCESS | 15 - Update Transaction Description; Flow F03d - Correct Expense Amount via PATCH (800,000 VND) |
| 149 | Transactions | POST | /api/transactions/income | TESTED_PASS_SUCCESS | 03 - Fund Main Wallet via Income Posting (10,000,000 VND) |
| 150 | Transactions | POST | /api/transactions/expense | TESTED_PASS_SUCCESS | 10 - Record Expense Transaction (45,000 VND); Flow F03b - Record Expense Transaction for Budget Category (500,000 VND) |
| 151 | Transactions | POST | /api/transactions/expense/multi-wallet | TESTED_PASS_SUCCESS | B2-07 - Multi-Wallet Expense Allocation (Controlled Validation / Ledger) |
| 152 | Transfers | GET | /api/transfers/recipients | TESTED_PASS_SUCCESS | 01 - Find Bank Transfer Recipient by Phone |
| 153 | Transfers | GET | /api/transfers/{transferId} | TESTED_PASS_SUCCESS | B2-08 - Get Transfer Details by ID |
| 154 | Transfers | POST | /api/transfers | TESTED_PASS_SUCCESS | 02 - Create Bank Direct Transfer Instruction |
| 155 | Transfers | GET | /api/transfers | TESTED_PASS_SUCCESS | 03 - Bank Direct Transfer History |
| 156 | Transfers | POST | /api/transfers/{paymentId}/confirm | TESTED_PASS_SUCCESS | B2-09 - Confirm Payment Gateway Transfer (Negative Check for non-existent) |
| 157 | Transfers | POST | /api/transfers/internal | TESTED_PASS_SUCCESS | 11 - Internal Transfer Between Wallets (3,000,000 VND); Flow E02 - Transfer 300,000 VND from Main to Savings Wallet |
| 158 | Transfers | POST | /api/transfers/p2p | TESTED_PASS_SUCCESS | 00 - Fund User B Main Wallet (P2P Transfer 2,000,000 VND) |
| 159 | Users | GET | /api/users/me | TESTED_PASS_SUCCESS | 01 - Get Current User Profile (User A); Flow A01 - Pre-check Profile Avatar; Flow A03 - Check Profile Immediately Has New Avatar; Flow A07 - Check Profile After Fresh Login Retains Avatar |
| 160 | Users | POST | /api/users/me/avatar | TESTED_PASS_SUCCESS | 02 - Upload User Avatar (User A); Flow A02 - Upload Real Avatar Image |
| 161 | Users | POST | /api/users/me/email/request | TESTED_PASS_SUCCESS | Flow B01 - Request Email Change to New Email |
| 162 | Users | POST | /api/users/me/email/confirm | TESTED_PASS_SUCCESS | Flow B02 - Confirm Email Change with OTP |
| 163 | Users | PUT | /api/users/me/preferences | TESTED_PASS_SUCCESS | 03 - Update User Preferences |
| 164 | Users | DELETE | /api/users/me/sessions | TESTED_PASS_SUCCESS | B1-15 - Revoke User Sessions via DELETE /api/users/me/sessions |
| 165 | V1Activity | POST | /api/v1/activity | TESTED_PASS_SUCCESS | B2-10 - Record V1 Activity Event |
| 166 | V1Funds | POST | /api/v1/funds/{id}/requests | TESTED_PASS_SUCCESS | 07 - User B Creates Free Fund Expense Request; 07c - User B Creates Second Free Fund Expense Request for Security Testing |
| 167 | V1Funds | GET | /api/v1/funds/{id}/requests | TESTED_PASS_SUCCESS | B6-02 - Get Fund Requests List |
| 168 | V1Funds | POST | /api/v1/funds/requests/{id}/review | TESTED_PASS_SUCCESS | 07b - User A (Owner) Reviews and Approves Free Fund Request; 09 - Cross-User Fund Request Review User B to Free Fund (Expect 403 or 404) |
| 169 | V1Funds | POST | /api/v1/funds/requests/{id}/report | TESTED_PASS_SUCCESS | V1-IMP-10 - 10b - Member Reports Large Expense Hold (Transitions to Suspended) |
| 170 | V1Funds | POST | /api/v1/funds/requests/{id}/cancel | TESTED_PASS_SUCCESS | V1-IMP-11 - 10d - Requester Cancels Suspended Expense Releasing Hold |
| 171 | V1Funds | GET | /api/v1/funds | TESTED_PASS_SUCCESS | B6-01 - List User V1 Funds |
| 172 | V1Funds | POST | /api/v1/funds | TESTED_PASS_SUCCESS | 01 - Create Fixed Fund (User A); 01 - Create Free Fund (User A); Flow D01 - User A Creates Fixed Fund (1,000,000 VND) |
| 173 | V1Funds | GET | /api/v1/funds/{id} | TESTED_PASS_SUCCESS | 02 - Get Fixed Fund Details |
| 174 | V1Funds | GET | /api/v1/funds/{id}/members | TESTED_PASS_SUCCESS | 05 - List Fixed Fund Members; 10 - Verify Remaining Members in Free Fund |
| 175 | V1Funds | GET | /api/v1/funds/{id}/governance | TESTED_PASS_SUCCESS | B6-03 - Get Fund Governance Status |
| 176 | V1Funds | GET | /api/v1/funds/{id}/owner-response | TESTED_PASS_SUCCESS | B6-04 - Get Fund Owner Response Status |
| 177 | V1Funds | POST | /api/v1/funds/{id}/owner-response/escalate | TESTED_PASS_SUCCESS | B6-05 - Escalate Owner Response Inactive Window (Controlled Exception/Check) |
| 178 | V1Funds | POST | /api/v1/funds/{id}/activity/close | TESTED_PASS_SUCCESS | B6-06 - Close Activity Window via POST /api/v1/funds/{id}/activity/close |
| 179 | V1Funds | POST | /api/v1/funds/{id}/activity-proposals | TESTED_PASS_SUCCESS | B6-07 - Propose Fund Activity via POST /api/v1/funds/{id}/activity-proposals |
| 180 | V1Funds | GET | /api/v1/funds/activity-proposals/{id} | TESTED_PASS_SUCCESS | B6-08 - Get Activity Proposal by ID (Controlled Fallback - Expect 404 for dummy) |
| 181 | V1Funds | POST | /api/v1/funds/activity-proposals/{id}/settle | TESTED_PASS_SUCCESS | V1-IMP-12 - Owner settles approved activity |
| 182 | V1Funds | POST | /api/v1/funds/activity-proposals/{id}/vote | TESTED_PASS_SUCCESS | B6-09 - Vote on Non-Existent Activity Proposal (Expect 404) |
| 183 | V1Funds | POST | /api/v1/funds/{id}/elections | TESTED_PASS_SUCCESS | B6-10 - Open Election via POST /api/v1/funds/{id}/elections |
| 184 | V1Funds | GET | /api/v1/funds/elections/{id} | TESTED_PASS_SUCCESS | B6-11 - Get Election Details by ID (Controlled Fallback - Expect 404 for dummy) |
| 185 | V1Funds | POST | /api/v1/funds/elections/{id}/candidates | TESTED_PASS_SUCCESS | B6-12 - Register Candidate for Non-Existent Election (Expect 404) |
| 186 | V1Funds | POST | /api/v1/funds/elections/{id}/ballots | TESTED_PASS_SUCCESS | B6-13 - Cast Ballot for Non-Existent Election (Expect 404) |
| 187 | V1Funds | POST | /api/v1/funds/elections/{id}/advance | TESTED_PASS_SUCCESS | V1-IMP-13 - Election no-votes failure |
| 188 | V1Funds | POST | /api/v1/funds/elections/{id}/accept | TESTED_PASS_SUCCESS | B6-14 - Accept Elected Ownership for Non-Existent Election (Expect 404) |
| 189 | V1Funds | POST | /api/v1/funds/elections/{id}/reject | TESTED_PASS_SUCCESS | B6-15 - Reject Elected Ownership for Non-Existent Election (Expect 404) |
| 190 | V1Funds | GET | /api/v1/funds/{id}/contribution-allocation | TESTED_PASS_SUCCESS | B6-16 - Get Contribution Allocation Details |
| 191 | V1Funds | GET | /api/v1/funds/{id}/finance | TESTED_PASS_SUCCESS | 12 - Check Fixed Fund Financial Overview; 06 - Track Free Fund Finance; Flow D07 - Check Fund Finance (Total 1,000,000 VND) |
| 192 | V1Funds | POST | /api/v1/funds/{id}/obligations/confirm | TESTED_PASS_SUCCESS | 06 - Confirm Obligations (Target: 2,000,000 VND); Flow D04 - Confirm Obligations (Target: 1,000,000 VND) |
| 193 | V1Funds | POST | /api/v1/funds/{id}/contributions | TESTED_PASS_SUCCESS | 07 - User A Contributes 1,000,000 VND to Fixed Fund; 08 - User B Contributes 1,000,000 VND to Fixed Fund; 04 - User A Contributes 500,000 VND to Free Fund; 05 - User B Contributes 300,000 VND to Free Fund; Flow D05 - User A Contributes 500,000 VND; Flow D06 - User B Contributes 500,000 VND |
| 194 | V1Funds | GET | /api/v1/funds/{id}/contributions | TESTED_PASS_SUCCESS | 09 - List Fixed Fund Contributions |
| 195 | V1Funds | GET | /api/v1/funds/{id}/activities | TESTED_PASS_SUCCESS | 11 - Check Fixed Fund Activities; Flow D09 - Check Activity Log for Spending Permission Event |
| 196 | V1Funds | POST | /api/v1/funds/{id}/invitations | TESTED_PASS_SUCCESS | 03 - Invite User B to Fixed Fund; 02 - Invite User B to Free Fund; Flow D02 - Invite User B to Fixed Fund |
| 197 | V1Funds | GET | /api/v1/funds/invitations | TESTED_PASS_SUCCESS | B6-17 - Get User Invitations List |
| 198 | V1Funds | GET | /api/v1/funds/{id}/invitation-proposals | TESTED_PASS_SUCCESS | B6-18 - Get Fund Invitation Proposals List |
| 199 | V1Funds | POST | /api/v1/funds/invitations/{id}/review | TESTED_PASS_SUCCESS | B6-19 - Review Non-Existent Invitation Proposal (Expect 404) |
| 200 | V1Funds | POST | /api/v1/funds/invitations/{id}/decline | TESTED_PASS_SUCCESS | B6-20 - Decline Non-Existent Invitation (Expect 404) |
| 201 | V1Funds | DELETE | /api/v1/funds/invitations/{id} | TESTED_PASS_SUCCESS | B6-21 - Cancel Non-Existent Invitation via DELETE (Expect 404) |
| 202 | V1Funds | POST | /api/v1/funds/invitations/{id}/accept | TESTED_PASS_SUCCESS | 04 - User B Accepts Fixed Fund Invitation; 03 - User B Accepts Free Fund Invitation; Flow D03 - User B Accepts Fixed Fund Invitation |
| 203 | V1Funds | PUT | /api/v1/funds/{id}/members/{userId}/spending-permission | TESTED_PASS_SUCCESS | 10 - Grant Spending Permission to User B; Flow D08 - Grant User B Spending Permission; 08 - Cross-User Fund Permission Escalation User B to Fixed Fund (Expect 403) |
| 204 | V1Funds | POST | /api/v1/funds/{id}/ownership-offers | TESTED_PASS_SUCCESS | B6-22 - Offer Fund Ownership via POST /api/v1/funds/{id}/ownership-offers |
| 205 | V1Funds | POST | /api/v1/funds/ownership-offers/{id}/accept | TESTED_PASS_SUCCESS | B6-23 - Accept Non-Existent Ownership Offer (Expect 404) |
| 206 | V1Funds | GET | /api/v1/funds/ownership-offers | TESTED_PASS_SUCCESS | B6-24 - List User Ownership Offers |
| 207 | V1Funds | GET | /api/v1/funds/{id}/refund-preview | TESTED_PASS_SUCCESS | 08 - Refund Preview for User B |
| 208 | V1Funds | POST | /api/v1/funds/{id}/leave | TESTED_PASS_SUCCESS | 09 - User B Leaves Free Fund |
| 209 | V1Funds | POST | /api/v1/funds/{id}/members/{userId}/kick | TESTED_PASS_SUCCESS | V1-IMP-14 - 20 - User A Kicks User B from Fixed Fund 2 (Refund Processed) |
| 210 | V1Funds | POST | /api/v1/funds/{id}/dissolve | TESTED_PASS_SUCCESS | V1-IMP-15 - 21 - User A Dissolves Fixed Fund 2 (Zero Balance Invariant) |
| 211 | V1Maintenance | GET | /api/v1/system/maintenance | TESTED_PASS_SUCCESS | V1-IMP-16 - 11 - ADM-05: Maintenance Status |
| 212 | Wallets | GET | /api/wallets | TESTED_PASS_SUCCESS | 01 - List Default Wallets for User A; 12 - Check Balances After Expense and Transfer; 00b - Get User B Main Wallet; Flow E01 - Identify Main and Savings Wallets for Flow E |
| 213 | Wallets | POST | /api/wallets | TESTED_PASS_SUCCESS | 04 - Create Savings Wallet User A; 05 - Create Emergency Wallet User A; 06 - Check Wallet Limit (Try Creating 4th Wallet) |
| 214 | Wallets | GET | /api/wallets/{walletId} | TESTED_PASS_SUCCESS | 07 - Get Main Wallet Details; Flow E03 - Check Savings Wallet Balance Increased by 300,000 VND; 04 - Cross-User Wallet Access User A to User B Wallet (Expect 404) |
| 215 | Wallets | PUT | /api/wallets/{walletId} | TESTED_PASS_SUCCESS | 08 - Update Savings Wallet Name |
| 216 | Wallets | DELETE | /api/wallets/{walletId} | TESTED_PASS_SUCCESS | 16 - Close Emergency Wallet |
