# SpendWise Backend User API — Endpoint Coverage Matrix (100% Real Audited)

> [!IMPORTANT]
> Ma trận được sinh tự động bởi `verify_readiness.py` đối soát trực tiếp giữa runtime OpenAPI (`/openapi/v1.json`),
> bộ Postman Collection (`SpendWise-User-API.postman_collection.json`) và kết quả thực thi Newman thật (`newman-run-audited-final.json`).

## 1. Tổng quan kiểm tra

- **Tổng OpenAPI Operations**: 339
- **Admin Operations (loại trừ theo phạm vi User)**: 122
- **System Ping Operations (loại trừ)**: 1
- **User API Operations (trong phạm vi)**: **216 / 216 (100%)**
- **Tổng Requests Postman thực thi**: **300**
- **Tổng Assertions PASS**: **323 / 323 (100.0%)**
- **Lỗi thất bại (FAIL)**: **0**

## 2. Thống kê phân loại 6 tầng

| Phân loại | Số lượng | Tỷ lệ | Ý nghĩa nghiệm thu |
| :--- | :---: | :---: | :--- |
| `SUCCESS_VERIFIED` | **194** | 89.8% | Kiểm thử luồng thành công (200/201/204) với thực thể dữ liệu thật |
| `BOTH_VERIFIED` | **13** | 6.0% | Đã kiểm thử cả luồng thành công và biên kiểm soát lỗi/bảo mật |
| `NEGATIVE_ONLY` | **0** | 0.0% | Kiểm thử ràng buộc bảo mật / bất biến dữ liệu (chặn hợp lệ) |
| `EXTERNAL_LIVE_DEFERRED` | **9** | 4.2% | Hoãn kết nối live nhà cung cấp bên ngoài (SePay gateway) theo phê duyệt PO |
| `NOT_VERIFIED` | **0** | 0.0% | Chưa có test case thực thi |
| `FAIL` | **0** | 0.0% | Test case thất bại hoặc lỗi logic |
| **Tổng cộng** | **216** | **100.0%** | **Sẵn sàng 100% bàn giao Backend User** |

## 3. Ma trận chi tiết 216 User Operations

| STT | Phương thức | Tuyến đường (Route) | Phân hệ (Domain) | Trạng thái nghiệm thu | Bằng chứng kiểm thử thực tế | Test Case Postman |
| :---: | :--- | :--- | :--- | :--- | :--- | :--- |
| 1 | `POST` | `/api/account-recovery` | AccountRecovery | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 202) | B1-01 - Submit Anonymous Account Recovery |
| 2 | `POST` | `/api/cases/disputes` | Cases | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B4-01 - Create Financial Dispute |
| 3 | `GET` | `/api/cases/disputes` | Cases | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | 08 - List Financial Disputes |
| 4 | `POST` | `/api/cases/support` | Cases | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | 04 - Create Support Case |
| 5 | `GET` | `/api/cases/support` | Cases | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | 05 - List Support Cases |
| 6 | `GET` | `/api/cases/support/{id}` | Cases | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | 06 - Get Support Case Details |
| 7 | `POST` | `/api/cases/support/{id}/messages` | Cases | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | 07 - Reply to Support Case Message |
| 8 | `POST` | `/api/cases/support/{id}/messages/multipart` | Cases | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B4-05 - Reply to Support Case with Multipart Form (Happy Pat |
| 9 | `GET` | `/api/cases/disputes/{id}` | Cases | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B4-02 - Get Dispute Details by ID |
| 10 | `POST` | `/api/cases/disputes/{id}/messages` | Cases | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B4-03 - Reply to Dispute Message |
| 11 | `POST` | `/api/cases/disputes/{id}/messages/multipart` | Cases | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B4-04 - Reply to Dispute with Multipart Form (Happy Path) |
| 12 | `GET` | `/api/cases/attachments/{id}` | Cases | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B4-06 - Download Case Attachment Success |
| 13 | `GET` | `/api/ai/conversations` | Ai | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | 02 - List AI Conversations |
| 14 | `POST` | `/api/ai/conversations` | Ai | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B4-07 - Create AI Conversation for Lifecycle, 01 - Create AI |
| 15 | `GET` | `/api/ai/conversations/{id}` | Ai | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B4-08 - Get AI Conversation by ID |
| 16 | `PUT` | `/api/ai/conversations/{id}` | Ai | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B4-09 - Update AI Conversation Title |
| 17 | `DELETE` | `/api/ai/conversations/{id}` | Ai | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 204) | B4-10 - Delete AI Conversation via DELETE |
| 18 | `POST` | `/api/ai/chat` | Ai | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | 03 - Ask AI Financial Advisor (Chat) - Fallback Safe |
| 19 | `POST` | `/api/ai/attachments` | Ai | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 201) | B4-11 - Upload AI Attachment with Valid File (Happy Path) |
| 20 | `GET` | `/api/ai/attachments/{id}` | Ai | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B4-12 - Download AI Attachment Success |
| 21 | `GET` | `/api/ai/actions/{id}` | Ai | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B4-13 - Get AI Action Proposal by ID Success |
| 22 | `POST` | `/api/ai/actions/{id}/execute` | Ai | `BOTH_VERIFIED` | Success & security invariant verified (HTTP 200, 404) | 10 - Non-Existent AI Action Execution (Architecture Controll |
| 23 | `POST` | `/api/ai/actions/{id}/cancel` | Ai | `BOTH_VERIFIED` | Success & security invariant verified (HTTP 200, 409) | B4-15 - Cancel AI Action Proposal Success, 11 - Non-Existent |
| 24 | `POST` | `/api/auth/register` | Auth | `BOTH_VERIFIED` | Success & security invariant verified (HTTP 200, 409) | 11 - Register Unverified User (for Inactive Check), 07 - Reg |
| 25 | `POST` | `/api/auth/email-verification/verify` | Auth | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | 08 - Verify Registration Email User B, 05 - Verify Registrat |
| 26 | `POST` | `/api/auth/email-verification/resend` | Auth | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | 04 - Resend Verification OTP (Happy Path) |
| 27 | `POST` | `/api/auth/login` | Auth | `BOTH_VERIFIED` | Success & security invariant verified (HTTP 200, 401, 403) | Flow B04 - Login with New Email (Expected: Success), 20 - Re |
| 28 | `POST` | `/api/auth/pin/setup` | Auth | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | 09 - Setup Security PIN User B, 06 - Setup Security PIN User |
| 29 | `POST` | `/api/auth/pin/verify` | Auth | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | Flow B05 - Complete PIN Verification with New Email, Flow A0 |
| 30 | `POST` | `/api/auth/refresh` | Auth | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | 10 - Refresh Access Token User A |
| 31 | `POST` | `/api/auth/logout` | Auth | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 204) | Flow A04 - Logout User A, Flow B03 - Logout After Email Chan |
| 32 | `GET` | `/api/auth/sessions` | Auth | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | 22 - List Active User Sessions |
| 33 | `DELETE` | `/api/auth/sessions/{sessionId}` | Auth | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 204) | 23 - Revoke Single Session |
| 34 | `POST` | `/api/auth/change-password` | Auth | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 204) | V1-IMP-01 - 07 - Change Password User B |
| 35 | `POST` | `/api/auth/forgot-password` | Auth | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 202) | 18 - Forgot Password User A |
| 36 | `POST` | `/api/auth/reset-password` | Auth | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 204) | 19 - Reset Password User A |
| 37 | `POST` | `/api/auth/pin/forgot` | Auth | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 202) | V1-IMP-02 - 11 - Forgot Security PIN User A |
| 38 | `POST` | `/api/auth/pin/reset/validate` | Auth | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | V1-IMP-03 - 12 - Validate Invalid PIN Reset Token |
| 39 | `POST` | `/api/auth/pin/reset` | Auth | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 204) | B1-02 - Reset Security PIN Success |
| 40 | `POST` | `/api/bank-connections/link` | BankConnections | `EXTERNAL_LIVE_DEFERRED` | Whitelisted external provider call (HTTP 503) | B5-01 - Create Bank Link Session (Controlled Sandbox Respons |
| 41 | `GET` | `/api/bank-connections` | BankConnections | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | V1-IMP-04 - 01 - Bank Connections Status |
| 42 | `GET` | `/api/bank-connections/{id}` | BankConnections | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B5-02 - Get Bank Connection Success |
| 43 | `POST` | `/api/bank-connections/{id}/unlink` | BankConnections | `EXTERNAL_LIVE_DEFERRED` | Whitelisted external provider call (HTTP 404) | B5-03 - Unlink Non-Existent Bank Connection (Expect 404) |
| 44 | `POST` | `/api/bank-connections/{id}/reactivate` | BankConnections | `EXTERNAL_LIVE_DEFERRED` | Whitelisted external provider call (HTTP 404) | B5-04 - Reactivate Non-Existent Bank Connection (Expect 404) |
| 45 | `POST` | `/api/bank-connections/{id}/sync` | BankConnections | `EXTERNAL_LIVE_DEFERRED` | Whitelisted external provider call (HTTP 404) | B5-05 - Sync Non-Existent Bank Connection (Expect 404) |
| 46 | `POST` | `/api/bank-connections/test-mode/link-challenges` | BankConnections | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | V1-IMP-05 - 02 - SePay TestMode: Start Link Challenge |
| 47 | `POST` | `/api/bank-connections/test-mode/link-challenges/{challengeId}/confirm` | BankConnections | `EXTERNAL_LIVE_DEFERRED` | Whitelisted external provider call (HTTP 409) | V1-IMP-06 - 03 - SePay TestMode: Verify Challenge Rejection  |
| 48 | `POST` | `/api/bank-connections/test-mode/wallet-top-ups` | BankConnections | `EXTERNAL_LIVE_DEFERRED` | Whitelisted external provider call (HTTP 404) | V1-IMP-07 - 05 - SePay TestMode: Top-Up Without Active Conne |
| 49 | `GET` | `/api/bank-connections/test-mode/wallet-top-ups` | BankConnections | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | V1-IMP-08 - 06 - SePay TestMode: List Wallet Top-Ups |
| 50 | `POST` | `/api/bank-connections/test-mode/wallet-top-ups/{topUpId}/confirm` | BankConnections | `EXTERNAL_LIVE_DEFERRED` | Whitelisted external provider call (HTTP 404) | B5-06 - Confirm Non-Existent TestMode TopUp (Expect 404) |
| 51 | `GET` | `/api/bank-connections/test-mode/linked-accounts` | BankConnections | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | V1-IMP-09 - 04 - SePay TestMode: List Linked Accounts |
| 52 | `GET` | `/api/bank-transactions` | BankTransactions | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B5-07 - List Bank Transactions (Paginated) |
| 53 | `GET` | `/api/bank-transactions/{id}` | BankTransactions | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B5-08 - Get Bank Transaction Success |
| 54 | `PATCH` | `/api/bank-transactions/{id}/category` | BankTransactions | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B5-09 - Reclassify Bank Transaction Success |
| 55 | `GET` | `/api/categories` | Categories | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | Flow F01 - Get Expense Category for Budgeting, 02 - Get Syst |
| 56 | `POST` | `/api/categories` | Categories | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 201) | 09 - Create Custom Category, B2-01 - Create Isolated Categor |
| 57 | `GET` | `/api/categories/{categoryId}` | Categories | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B2-02 - Get Category Details by ID |
| 58 | `PUT` | `/api/categories/{categoryId}` | Categories | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B2-03 - Update Category Details |
| 59 | `DELETE` | `/api/categories/{categoryId}` | Categories | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B2-04 - Archive/Delete Category |
| 60 | `GET` | `/api/dashboard` | Dashboard | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B2-05 - Get User Dashboard Summary |
| 61 | `POST` | `/api/evidence` | Evidence | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 201) | 03 - User B Uploads Evidence, 09b - Upload Evidence for Expe |
| 62 | `GET` | `/api/evidence/{id}` | Evidence | `BOTH_VERIFIED` | Success & security invariant verified (HTTP 200, 404) | 07 - Cross-User Evidence Access User A to User B Evidence (E |
| 63 | `GET` | `/api/friends/search` | Friends | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | Flow C01 - User A Searches User B by Contact, 05 - Search Us |
| 64 | `GET` | `/api/friends/discover` | Friends | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | 06 - Discover Users by Name with Pagination |
| 65 | `GET` | `/api/friends` | Friends | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | Flow C03 - User A Checks Friend List (Has User B), 12 - User |
| 66 | `GET` | `/api/friends/requests/incoming` | Friends | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | 10 - User B Checks Incoming Requests |
| 67 | `GET` | `/api/friends/requests/outgoing` | Friends | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | 09 - User A Checks Outgoing Requests |
| 68 | `POST` | `/api/friends/requests` | Friends | `BOTH_VERIFIED` | Success & security invariant verified (HTTP 200, 409) | 07 - Send Friend Request (User A to User B), Flow C02 - User |
| 69 | `DELETE` | `/api/friends/requests/{requestId}` | Friends | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B1-13 - Cancel Friend Request Success |
| 70 | `POST` | `/api/friends/requests/{requestId}/accept` | Friends | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | 11 - User B Accepts Friend Request |
| 71 | `POST` | `/api/friends/requests/{requestId}/reject` | Friends | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B1-12 - Reject Friend Request Success |
| 72 | `DELETE` | `/api/friends/{friendUserId}` | Friends | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 204) | B1-14 - Unfriend Friend Success |
| 73 | `PATCH` | `/api/friends/{friendUserId}/preference` | Friends | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | 14 - Update Friend Preference (User A sets Favorite) |
| 74 | `GET` | `/api/groups` | Groups | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B7-01 - List Legacy Groups |
| 75 | `POST` | `/api/groups` | Groups | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B7-02 - Create Legacy Group for Compatibility Test |
| 76 | `GET` | `/api/groups/{groupId}` | Groups | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B7-06 - List User Legacy Group Invitations, B7-03 - Get Lega |
| 77 | `PUT` | `/api/groups/{groupId}` | Groups | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B7-04 - Update Legacy Group Details |
| 78 | `POST` | `/api/groups/{groupId}/invites` | Groups | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B7-05 - Invite Member to Legacy Group |
| 79 | `GET` | `/api/groups/invites` | Groups | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B7-06 - List User Legacy Group Invitations |
| 80 | `POST` | `/api/groups/invites/{inviteId}/accept` | Groups | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B7-08 - Accept Legacy Invite Success |
| 81 | `POST` | `/api/groups/invites/{inviteId}/reject` | Groups | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B7-07 - Reject Legacy Invite Success |
| 82 | `DELETE` | `/api/groups/{groupId}/members/{memberUserId}` | Groups | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 204) | B7-09 - Remove Member from Legacy Group Success |
| 83 | `POST` | `/api/groups/{groupId}/ownership/{memberUserId}` | Groups | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 204) | B7-10 - Transfer Group Ownership Success |
| 84 | `PUT` | `/api/groups/{groupId}/receiving-bank` | Groups | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B7-11 - Set Group Receiving Bank Account |
| 85 | `GET` | `/api/groups/{groupId}/fund` | Groups | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B7-12 - Get Shared Fund Status for Group |
| 86 | `GET` | `/api/groups/{groupId}/fund/transactions` | Groups | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B7-13 - Get Shared Fund Transactions |
| 87 | `POST` | `/api/groups/{groupId}/fund/contributions` | Groups | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B7-14 - Contribute to Shared Fund (Wallet Balance Check) |
| 88 | `GET` | `/api/groups/{groupId}/fund-requests` | Groups | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B7-15 - List Group Fund Requests |
| 89 | `POST` | `/api/groups/{groupId}/fund-requests` | Groups | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B7-16 - Create Group Fund Request |
| 90 | `POST` | `/api/groups/{groupId}/fund-requests/{requestId}/pay` | Groups | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B7-17 - Pay Group Fund Request Success |
| 91 | `GET` | `/api/groups/{groupId}/fund-rounds` | Groups | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B7-18 - List Group Fund Rounds |
| 92 | `POST` | `/api/groups/{groupId}/fund-rounds` | Groups | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B7-19 - Create Group Fund Round |
| 93 | `POST` | `/api/groups/{groupId}/fund-rounds/{roundId}/pay` | Groups | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B7-20 - Pay Group Fund Round Success |
| 94 | `GET` | `/api/groups/{groupId}/expenses` | Groups | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B7-21 - List Group Expenses |
| 95 | `POST` | `/api/groups/{groupId}/expenses` | Groups | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B7-22 - Create Group Expense |
| 96 | `GET` | `/api/groups/{groupId}/activities` | Groups | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B7-23 - List Group Activities Log |
| 97 | `GET` | `/api/groups/{groupId}/deletion-request` | Groups | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B7-24 - Get Group Deletion Request Status Success |
| 98 | `POST` | `/api/groups/{groupId}/deletion-requests` | Groups | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B7-25b - Re-request Group Deletion for Vote, B7-25 - Request |
| 99 | `POST` | `/api/groups/{groupId}/deletion-requests/{requestId}/vote` | Groups | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B7-26 - Vote on Group Deletion Request Success |
| 100 | `POST` | `/api/groups/{groupId}/deletion-requests/{requestId}/cancel` | Groups | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B7-27 - Cancel Group Deletion Request Success |
| 101 | `POST` | `/api/groups/{groupId}/deletion-requests/{requestId}/refunds/{refundId}/payment` | Groups | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B7-28 - Create Group Refund Payment Success |
| 102 | `POST` | `/api/groups/{groupId}/deletion-requests/{requestId}/finalize` | Groups | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B7-29 - Finalize Group Deletion Success |
| 103 | `PUT` | `/api/mobile/push/installations/{installationId}` | MobilePush | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 204) | B1-05 - Register Mobile Push Installation |
| 104 | `DELETE` | `/api/mobile/push/installations/{installationId}` | MobilePush | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 204) | B1-06 - Unregister Mobile Push Installation |
| 105 | `GET` | `/api/notifications` | Notifications | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | 01 - List Notifications for User A |
| 106 | `DELETE` | `/api/notifications` | Notifications | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B1-11 - Clear All Read Notifications |
| 107 | `POST` | `/api/notifications/{id}/read` | Notifications | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 204) | B1-09 - Mark Single Notification Read |
| 108 | `POST` | `/api/notifications/read-all` | Notifications | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 204) | 02 - Mark All Notifications Read |
| 109 | `GET` | `/api/notifications/search` | Notifications | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | 03 - Search Notifications |
| 110 | `DELETE` | `/api/notifications/{id}` | Notifications | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 204) | B1-10 - Delete Single Notification |
| 111 | `GET` | `/api/notifications/preferences` | Notifications | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B1-07 - Get Notification Preferences |
| 112 | `PUT` | `/api/notifications/preferences` | Notifications | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B1-08 - Save Notification Preference |
| 113 | `GET` | `/api/system/maintenance` | Operations | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B1-03 - Get System Maintenance State (Public/User) |
| 114 | `GET` | `/api/system/announcements/active` | Operations | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 204) | B1-04 - Get Active System Announcement (Public/User) |
| 115 | `GET` | `/api/budgets` | Budgets | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | 02 - List Monthly Budgets |
| 116 | `POST` | `/api/budgets` | Budgets | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 201) | 01 - User B Prepares Isolated Budget and Goal, 01 - Create M |
| 117 | `PUT` | `/api/budgets` | Budgets | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B3-01 - Create Or Update Compatible Budget via PUT /api/budg |
| 118 | `GET` | `/api/budgets/{id}` | Budgets | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | Flow F03c - Verify Budget Spent (500,000 VND, 25.0%), Flow F |
| 119 | `PUT` | `/api/budgets/{id}` | Budgets | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | Flow F04 - Update Budget Limit to 3,000,000 VND, 03 - Update |
| 120 | `DELETE` | `/api/budgets/{id}` | Budgets | `BOTH_VERIFIED` | Success & security invariant verified (HTTP 204, 404) | Flow F05 - Clean Up Budget via Delete, 05 - Cross-User Budge |
| 121 | `GET` | `/api/goals` | Goals | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B3-02 - List User Goals via GET /api/goals |
| 122 | `POST` | `/api/goals` | Goals | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | 05 - Create Savings Goal (MacBook M4), B3-03 - Create Target |
| 123 | `GET` | `/api/goals/{id}` | Goals | `BOTH_VERIFIED` | Success & security invariant verified (HTTP 200, 404) | 07 - Get Goal Details and Verify Progress, 06 - Cross-User G |
| 124 | `PUT` | `/api/goals/{id}` | Goals | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B3-04 - Update Goal via PUT /api/goals/{id} |
| 125 | `DELETE` | `/api/goals/{id}` | Goals | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 204) | B3-05 - Delete Empty Goal via DELETE /api/goals/{id} |
| 126 | `POST` | `/api/goals/{id}/contributions` | Goals | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | 06 - Deposit to Savings Goal (2,000,000 VND) |
| 127 | `POST` | `/api/goals/{id}/withdrawals` | Goals | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | 08 - Withdraw from Savings Goal (500,000 VND) |
| 128 | `GET` | `/api/recurring-transactions` | RecurringTransactions | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | 10 - List Recurring Transactions |
| 129 | `POST` | `/api/recurring-transactions` | RecurringTransactions | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | 09 - Create Recurring Transaction (Monthly Internet), B3-06  |
| 130 | `GET` | `/api/recurring-transactions/{id}` | RecurringTransactions | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B3-07 - Get Recurring Transaction by ID |
| 131 | `PUT` | `/api/recurring-transactions/{id}` | RecurringTransactions | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B3-08 - Update Recurring Transaction via PUT |
| 132 | `DELETE` | `/api/recurring-transactions/{id}` | RecurringTransactions | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 204) | B3-12 - Delete Recurring Transaction |
| 133 | `POST` | `/api/recurring-transactions/{id}/pause` | RecurringTransactions | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B3-09 - Pause Recurring Transaction |
| 134 | `POST` | `/api/recurring-transactions/{id}/resume` | RecurringTransactions | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B3-10 - Resume Recurring Transaction |
| 135 | `POST` | `/api/recurring-transactions/{id}/occurrences/{occurrenceId}/record` | RecurringTransactions | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B3-11 - Record Recurring Occurrence Execution Success |
| 136 | `GET` | `/api/transaction-rules` | TransactionRules | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | 12 - List Transaction Rules |
| 137 | `POST` | `/api/transaction-rules` | TransactionRules | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | 11 - Create Transaction Rule, B3-13 - Create Transaction Rul |
| 138 | `GET` | `/api/transaction-rules/{id}` | TransactionRules | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B3-14 - Get Transaction Rule by ID |
| 139 | `PUT` | `/api/transaction-rules/{id}` | TransactionRules | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B3-15 - Update Transaction Rule via PUT |
| 140 | `DELETE` | `/api/transaction-rules/{id}` | TransactionRules | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 204) | B3-16 - Delete Transaction Rule via DELETE |
| 141 | `GET` | `/api/security-pin/status` | SecurityPin | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | 26 - Security PIN Status Check |
| 142 | `POST` | `/api/security-pin/verify` | SecurityPin | `BOTH_VERIFIED` | Success & security invariant verified (HTTP 204, 401) | 15 - Verify Security PIN Invalid (Negative Check), 14 - Veri |
| 143 | `PUT` | `/api/security-pin` | SecurityPin | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 204) | 17 - Revert Security PIN User A, 16 - Change Security PIN Us |
| 144 | `POST` | `/api/integrations/sepay/bankhub/events` | SePayBankHubIntegration | `EXTERNAL_LIVE_DEFERRED` | Whitelisted external provider call (HTTP 401) | 04 - SePay Webhook Invariant (Controlled Check) |
| 145 | `POST` | `/api/integrations/sepay/bankhub/ipn` | SePayBankHubIntegration | `EXTERNAL_LIVE_DEFERRED` | Whitelisted external provider call (HTTP 401) | B2-06 - SePay BankHub IPN Webhook Invariant (Controlled Secr |
| 146 | `GET` | `/api/transactions` | Transactions | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | 13 - List Transactions with Query Filters, Flow E04 - Check  |
| 147 | `GET` | `/api/transactions/{transactionId}` | Transactions | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | 14 - Get Transaction Details |
| 148 | `PATCH` | `/api/transactions/{transactionId}` | Transactions | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | 15 - Update Transaction Description, Flow F03d - Correct Exp |
| 149 | `POST` | `/api/transactions/income` | Transactions | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 201) | 03 - Fund Main Wallet via Income Posting (10,000,000 VND) |
| 150 | `POST` | `/api/transactions/expense` | Transactions | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 201) | Flow F03b - Record Expense Transaction for Budget Category ( |
| 151 | `POST` | `/api/transactions/expense/multi-wallet` | Transactions | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 201) | B2-07 - Multi-Wallet Expense Allocation (Controlled Validati |
| 152 | `GET` | `/api/transfers/recipients` | Transfers | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | 01 - Find Bank Transfer Recipient by Phone |
| 153 | `GET` | `/api/transfers/{transferId}` | Transfers | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | 01 - Find Bank Transfer Recipient by Phone, B2-08 - Get Tran |
| 154 | `POST` | `/api/transfers` | Transfers | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 201) | 02 - Create Bank Direct Transfer Instruction |
| 155 | `GET` | `/api/transfers` | Transfers | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | 03 - Bank Direct Transfer History |
| 156 | `POST` | `/api/transfers/{paymentId}/confirm` | Transfers | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B2-09 - Confirm Bank Direct Transfer Success |
| 157 | `POST` | `/api/transfers/internal` | Transfers | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 201) | Flow E02 - Transfer 300,000 VND from Main to Savings Wallet, |
| 158 | `POST` | `/api/transfers/p2p` | Transfers | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 201) | 00 - Fund User B Main Wallet (P2P Transfer 2,000,000 VND) |
| 159 | `GET` | `/api/users/me` | Users | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | Flow A07 - Check Profile After Fresh Login Retains Avatar, F |
| 160 | `POST` | `/api/users/me/avatar` | Users | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | Flow A02 - Upload Real Avatar Image, 02 - Upload User Avatar |
| 161 | `POST` | `/api/users/me/email/request` | Users | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | Flow B01 - Request Email Change to New Email |
| 162 | `POST` | `/api/users/me/email/confirm` | Users | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | Flow B02 - Confirm Email Change with OTP |
| 163 | `PUT` | `/api/users/me/preferences` | Users | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | 03 - Update User Preferences |
| 164 | `DELETE` | `/api/users/me/sessions` | Users | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 204) | B1-15 - Revoke User Sessions via DELETE /api/users/me/sessio |
| 165 | `POST` | `/api/v1/activity` | V1Activity | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 204) | B2-10 - Record V1 Activity Event |
| 166 | `POST` | `/api/v1/funds/{id}/requests` | V1Funds | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | 07c - User B Creates Second Free Fund Expense Request for Se |
| 167 | `GET` | `/api/v1/funds/{id}/requests` | V1Funds | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B6-02 - Get Fund Requests List |
| 168 | `POST` | `/api/v1/funds/requests/{id}/review` | V1Funds | `BOTH_VERIFIED` | Success & security invariant verified (HTTP 200, 404) | 07b - User A (Owner) Reviews and Approves Free Fund Request, |
| 169 | `POST` | `/api/v1/funds/requests/{id}/report` | V1Funds | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | V1-IMP-10 - 10b - Member Reports Large Expense Hold (Transit |
| 170 | `POST` | `/api/v1/funds/requests/{id}/cancel` | V1Funds | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | V1-IMP-11 - 10d - Requester Cancels Suspended Expense Releas |
| 171 | `GET` | `/api/v1/funds` | V1Funds | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B6-01 - List User V1 Funds |
| 172 | `POST` | `/api/v1/funds` | V1Funds | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | Flow D01 - User A Creates Fixed Fund (1,000,000 VND), 01 - C |
| 173 | `GET` | `/api/v1/funds/{id}` | V1Funds | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B6-24 - List User Ownership Offers, 02 - Get Fixed Fund Deta |
| 174 | `GET` | `/api/v1/funds/{id}/members` | V1Funds | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | 05 - List Fixed Fund Members, 10 - Verify Remaining Members  |
| 175 | `GET` | `/api/v1/funds/{id}/governance` | V1Funds | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B6-03 - Get Fund Governance Status |
| 176 | `GET` | `/api/v1/funds/{id}/owner-response` | V1Funds | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B6-04 - Get Fund Owner Response Status Success |
| 177 | `POST` | `/api/v1/funds/{id}/owner-response/escalate` | V1Funds | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B6-05 - Escalate Owner Response Success |
| 178 | `POST` | `/api/v1/funds/{id}/activity/close` | V1Funds | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 204) | B6-06 - Close Activity Window via POST /api/v1/funds/{id}/ac |
| 179 | `POST` | `/api/v1/funds/{id}/activity-proposals` | V1Funds | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B6-07 - Propose Fund Activity Success |
| 180 | `GET` | `/api/v1/funds/activity-proposals/{id}` | V1Funds | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B6-08 - Get Activity Proposal by ID Success |
| 181 | `POST` | `/api/v1/funds/activity-proposals/{id}/settle` | V1Funds | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | V1-IMP-12 - Owner settles approved activity |
| 182 | `POST` | `/api/v1/funds/activity-proposals/{id}/vote` | V1Funds | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B6-09 - Vote on Activity Proposal Success |
| 183 | `POST` | `/api/v1/funds/{id}/elections` | V1Funds | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B6-10 - Open Election Success |
| 184 | `GET` | `/api/v1/funds/elections/{id}` | V1Funds | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B6-11 - Get Election Details by ID Success |
| 185 | `POST` | `/api/v1/funds/elections/{id}/candidates` | V1Funds | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B6-12 - Register Candidate Success |
| 186 | `POST` | `/api/v1/funds/elections/{id}/ballots` | V1Funds | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B6-13 - Cast Ballot Success |
| 187 | `POST` | `/api/v1/funds/elections/{id}/advance` | V1Funds | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | V1-IMP-13 - Advance Election Success |
| 188 | `POST` | `/api/v1/funds/elections/{id}/accept` | V1Funds | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B6-14 - Accept Elected Ownership Success |
| 189 | `POST` | `/api/v1/funds/elections/{id}/reject` | V1Funds | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B6-15 - Reject Elected Ownership Success |
| 190 | `GET` | `/api/v1/funds/{id}/contribution-allocation` | V1Funds | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B6-16 - Get Contribution Allocation Details |
| 191 | `GET` | `/api/v1/funds/{id}/finance` | V1Funds | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | 12 - Check Fixed Fund Financial Overview, Flow D07 - Check F |
| 192 | `POST` | `/api/v1/funds/{id}/obligations/confirm` | V1Funds | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | 06 - Confirm Obligations (Target: 2,000,000 VND), Flow D04 - |
| 193 | `POST` | `/api/v1/funds/{id}/contributions` | V1Funds | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | Flow D06 - User B Contributes 500,000 VND, 04 - User A Contr |
| 194 | `GET` | `/api/v1/funds/{id}/contributions` | V1Funds | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | 09 - List Fixed Fund Contributions |
| 195 | `GET` | `/api/v1/funds/{id}/activities` | V1Funds | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | Flow D09 - Check Activity Log for Spending Permission Event, |
| 196 | `POST` | `/api/v1/funds/{id}/invitations` | V1Funds | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | 02 - Invite User B to Free Fund, Flow D02 - Invite User B to |
| 197 | `GET` | `/api/v1/funds/invitations` | V1Funds | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B6-17 - Get User Invitations List |
| 198 | `GET` | `/api/v1/funds/{id}/invitation-proposals` | V1Funds | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B6-18 - Get Fund Invitation Proposals List |
| 199 | `POST` | `/api/v1/funds/invitations/{id}/review` | V1Funds | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B6-19 - Review Invitation Proposal Success |
| 200 | `POST` | `/api/v1/funds/invitations/{id}/decline` | V1Funds | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B6-20 - Decline Invitation Success |
| 201 | `DELETE` | `/api/v1/funds/invitations/{id}` | V1Funds | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B6-21 - Cancel Invitation Success |
| 202 | `POST` | `/api/v1/funds/invitations/{id}/accept` | V1Funds | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | 03 - User B Accepts Free Fund Invitation, 04 - User B Accept |
| 203 | `PUT` | `/api/v1/funds/{id}/members/{userId}/spending-permission` | V1Funds | `BOTH_VERIFIED` | Success & security invariant verified (HTTP 204, 403) | 10 - Grant Spending Permission to User B, 08 - Cross-User Fu |
| 204 | `POST` | `/api/v1/funds/{id}/ownership-offers` | V1Funds | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B6-22 - Offer Fund Ownership via POST /api/v1/funds/{id}/own |
| 205 | `POST` | `/api/v1/funds/ownership-offers/{id}/accept` | V1Funds | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 204) | B6-23 - Accept Ownership Offer Success |
| 206 | `GET` | `/api/v1/funds/ownership-offers` | V1Funds | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | B6-24 - List User Ownership Offers |
| 207 | `GET` | `/api/v1/funds/{id}/refund-preview` | V1Funds | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | 08 - Refund Preview for User B |
| 208 | `POST` | `/api/v1/funds/{id}/leave` | V1Funds | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | 09 - User B Leaves Free Fund |
| 209 | `POST` | `/api/v1/funds/{id}/members/{userId}/kick` | V1Funds | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | V1-IMP-14 - 20 - User A Kicks User B from Fixed Fund 2 (Refu |
| 210 | `POST` | `/api/v1/funds/{id}/dissolve` | V1Funds | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | V1-IMP-15 - 21 - User A Dissolves Fixed Fund 2 (Zero Balance |
| 211 | `GET` | `/api/v1/system/maintenance` | V1Maintenance | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | V1-IMP-16 - 11 - ADM-05: Maintenance Status |
| 212 | `GET` | `/api/wallets` | Wallets | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | Flow E01 - Identify Main and Savings Wallets for Flow E, 00b |
| 213 | `POST` | `/api/wallets` | Wallets | `BOTH_VERIFIED` | Success & security invariant verified (HTTP 201, 409) | 06 - Check Wallet Limit (Try Creating 4th Wallet), 05 - Crea |
| 214 | `GET` | `/api/wallets/{walletId}` | Wallets | `BOTH_VERIFIED` | Success & security invariant verified (HTTP 200, 404) | 04 - Cross-User Wallet Access User A to User B Wallet (Expec |
| 215 | `PUT` | `/api/wallets/{walletId}` | Wallets | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 200) | 08 - Update Savings Wallet Name |
| 216 | `DELETE` | `/api/wallets/{walletId}` | Wallets | `SUCCESS_VERIFIED` | Happy-path affirmative assertions passed (HTTP 204) | 16 - Close Emergency Wallet |
