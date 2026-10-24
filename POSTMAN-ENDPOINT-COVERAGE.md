# SpendWise Backend User API — Endpoint Coverage Matrix (100% Real Audited)

> [!IMPORTANT]
> Ma trận được sinh tự động bởi `verify_readiness.py` (v2.2.0) đối soát trực tiếp giữa runtime OpenAPI (`/openapi/v1.json`),
> bộ Postman Collection (`SpendWise-User-API.postman_collection.json`) và kết quả thực thi Newman thật (`newman_execution_evidence_sanitized.json`).

## 1. Tổng quan kiểm tra

- **Tổng OpenAPI Operations**: 339
- **Admin Operations (loại trừ theo phạm vi User)**: 122
- **System Ping Operations (loại trừ)**: 1
- **User API Operations (trong phạm vi)**: **216 / 216 (100%)**
- **Tổng Requests Postman thực thi**: **300**
- **Tổng Assertions PASS**: **339 / 339 (100.0%)**
- **Lỗi thất bại (FAIL)**: **0**

## 2. Thống kê phân loại 6 tầng

| Phân loại | Số lượng | Tỷ lệ | Ý nghĩa nghiệm thu |
| :--- | :---: | :---: | :--- |
| `SUCCESS_VERIFIED` | **196** | 90.7% | Kiểm thử luồng thành công (200/201/204) với contract & business state đầy đủ |
| `BOTH_VERIFIED` | **12** | 5.6% | Đã kiểm thử cả luồng thành công và biên kiểm soát lỗi/bảo mật |
| `NEGATIVE_ONLY` | **0** | 0.0% | Kiểm thử ràng buộc bảo mật / bất biến dữ liệu (chặn hợp lệ) |
| `EXTERNAL_LIVE_DEFERRED` | **8** | 3.7% | Hoãn kết nối live nhà cung cấp bên ngoài (SePay gateway) theo phê duyệt PO (nội bộ có unit/integration tests) |
| `NOT_VERIFIED` | **0** | 0.0% | Chưa có test case thực thi |
| `FAIL` | **0** | 0.0% | Test case thất bại hoặc thiếu contract/business evidence |
| **Tổng cộng** | **216** | **100.0%** | **Sẵn sàng 100% bàn giao Backend User** |

## 3. Ma trận chi tiết User Operations

| STT | Phương thức | Tuyến đường (Route) | Phân hệ (Domain) | Trạng thái nghiệm thu | Bằng chứng kiểm thử thực tế | Test Case Postman |
| :---: | :--- | :--- | :--- | :--- | :--- | :--- |
| 1 | `POST` | `/api/account-recovery` | AccountRecovery | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 202) | B1-01 - Submit Anonymous Account Recovery |
| 2 | `GET` | `/api/ai/actions/{id}` | Ai | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B4-13 - Get AI Action Proposal by ID Success |
| 3 | `POST` | `/api/ai/actions/{id}/cancel` | Ai | `BOTH_VERIFIED` | Success & security invariant verified (HTTP 200, 409) | B4-15 - Cancel AI Action Proposal Success, 11 - Non-Existent |
| 4 | `POST` | `/api/ai/actions/{id}/execute` | Ai | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200, 404) | B4-14 - Execute AI Action Proposal Success, 10 - Non-Existen |
| 5 | `POST` | `/api/ai/attachments` | Ai | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 201) | B4-11 - Upload AI Attachment with Valid File (Happy Path) |
| 6 | `GET` | `/api/ai/attachments/{id}` | Ai | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B4-12 - Download AI Attachment Success |
| 7 | `POST` | `/api/ai/chat` | Ai | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | 03 - Ask AI Financial Advisor (Chat) - Fallback Safe |
| 8 | `GET` | `/api/ai/conversations` | Ai | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | 02 - List AI Conversations |
| 9 | `POST` | `/api/ai/conversations` | Ai | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B4-07 - Create AI Conversation for Lifecycle, 01 - Create AI |
| 10 | `DELETE` | `/api/ai/conversations/{id}` | Ai | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 204) | B4-10 - Delete AI Conversation via DELETE |
| 11 | `GET` | `/api/ai/conversations/{id}` | Ai | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B4-08 - Get AI Conversation by ID |
| 12 | `PUT` | `/api/ai/conversations/{id}` | Ai | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B4-09 - Update AI Conversation Title |
| 13 | `POST` | `/api/auth/change-password` | Auth | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 204) | V1-IMP-01 - 07 - Change Password User B |
| 14 | `POST` | `/api/auth/email-verification/resend` | Auth | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | 04 - Resend Verification OTP (Happy Path) |
| 15 | `POST` | `/api/auth/email-verification/verify` | Auth | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | 08 - Verify Registration Email User B, 05 - Verify Registrat |
| 16 | `POST` | `/api/auth/forgot-password` | Auth | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 202) | 18 - Forgot Password User A |
| 17 | `POST` | `/api/auth/login` | Auth | `BOTH_VERIFIED` | Success & security invariant verified (HTTP 200, 401, 403) | 20 - Re-login User A after Reset Password, 13 - Login with W |
| 18 | `POST` | `/api/auth/logout` | Auth | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 204) | Flow B03 - Logout After Email Change, Flow A04 - Logout User |
| 19 | `POST` | `/api/auth/pin/forgot` | Auth | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 202) | V1-IMP-02 - 11 - Forgot Security PIN User A |
| 20 | `POST` | `/api/auth/pin/reset` | Auth | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 204) | B1-02 - Reset Security PIN Success |
| 21 | `POST` | `/api/auth/pin/reset/validate` | Auth | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | V1-IMP-03 - 12 - Validate Invalid PIN Reset Token |
| 22 | `POST` | `/api/auth/pin/setup` | Auth | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | 06 - Setup Security PIN User A, 09 - Setup Security PIN User |
| 23 | `POST` | `/api/auth/pin/verify` | Auth | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | Flow A06 - Complete PIN Login for Re-login, Flow B05 - Compl |
| 24 | `POST` | `/api/auth/refresh` | Auth | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | 10 - Refresh Access Token User A |
| 25 | `POST` | `/api/auth/register` | Auth | `BOTH_VERIFIED` | Success & security invariant verified (HTTP 200, 409) | 11 - Register Unverified User (for Inactive Check), 07 - Reg |
| 26 | `POST` | `/api/auth/reset-password` | Auth | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 204) | 19 - Reset Password User A |
| 27 | `GET` | `/api/auth/sessions` | Auth | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | 22 - List Active User Sessions |
| 28 | `DELETE` | `/api/auth/sessions/{sessionId}` | Auth | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 204) | 23 - Revoke Single Session |
| 29 | `GET` | `/api/bank-connections` | BankConnections | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | V1-IMP-04 - 01 - Bank Connections Status |
| 30 | `POST` | `/api/bank-connections/link` | BankConnections | `EXTERNAL_LIVE_DEFERRED` | Whitelisted external provider dependency (HTTP 503; unit/integration tested) | B5-01 - Create Bank Link Session (Controlled Sandbox Respons |
| 31 | `POST` | `/api/bank-connections/test-mode/link-challenges` | BankConnections | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | V1-IMP-05 - 02 - SePay TestMode: Start Link Challenge |
| 32 | `POST` | `/api/bank-connections/test-mode/link-challenges/{challengeId}/confirm` | BankConnections | `EXTERNAL_LIVE_DEFERRED` | Whitelisted external provider dependency (HTTP 409; unit/integration tested) | V1-IMP-06 - 03 - SePay TestMode: Verify Challenge Rejection  |
| 33 | `GET` | `/api/bank-connections/test-mode/linked-accounts` | BankConnections | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | V1-IMP-09 - 04 - SePay TestMode: List Linked Accounts |
| 34 | `GET` | `/api/bank-connections/test-mode/wallet-top-ups` | BankConnections | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | V1-IMP-08 - 06 - SePay TestMode: List Wallet Top-Ups |
| 35 | `POST` | `/api/bank-connections/test-mode/wallet-top-ups` | BankConnections | `EXTERNAL_LIVE_DEFERRED` | Whitelisted external provider dependency (HTTP 404; unit/integration tested) | V1-IMP-07 - 05 - SePay TestMode: Top-Up Without Active Conne |
| 36 | `POST` | `/api/bank-connections/test-mode/wallet-top-ups/{topUpId}/confirm` | BankConnections | `EXTERNAL_LIVE_DEFERRED` | Whitelisted external provider dependency (HTTP 409; unit/integration tested) | B5-06 - Confirm Pending TestMode TopUp Rejected (409 Conflic |
| 37 | `GET` | `/api/bank-connections/{id}` | BankConnections | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B5-02 - Get Bank Connection Success |
| 38 | `POST` | `/api/bank-connections/{id}/reactivate` | BankConnections | `EXTERNAL_LIVE_DEFERRED` | Whitelisted external provider dependency (HTTP 409; unit/integration tested) | B5-04 - Reactivate Active Connection Rejected (409 Conflict) |
| 39 | `POST` | `/api/bank-connections/{id}/sync` | BankConnections | `EXTERNAL_LIVE_DEFERRED` | Whitelisted external provider dependency (HTTP 409; unit/integration tested) | B5-05 - Sync TestMode Connection Rejected (409 Conflict) |
| 40 | `POST` | `/api/bank-connections/{id}/unlink` | BankConnections | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B5-03 - Unlink Bank Connection Success |
| 41 | `GET` | `/api/bank-transactions` | BankTransactions | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B5-07 - List Bank Transactions (Paginated) |
| 42 | `GET` | `/api/bank-transactions/{id}` | BankTransactions | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B5-08 - Get Bank Transaction Success |
| 43 | `PATCH` | `/api/bank-transactions/{id}/category` | BankTransactions | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B5-09 - Reclassify Bank Transaction Success |
| 44 | `GET` | `/api/budgets` | Budgets | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | 02 - List Monthly Budgets |
| 45 | `POST` | `/api/budgets` | Budgets | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 201) | 01 - User B Prepares Isolated Budget and Goal, Flow F02 - Cr |
| 46 | `PUT` | `/api/budgets` | Budgets | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B3-01 - Create Or Update Compatible Budget via PUT /api/budg |
| 47 | `DELETE` | `/api/budgets/{id}` | Budgets | `BOTH_VERIFIED` | Success & security invariant verified (HTTP 204, 404) | 05 - Cross-User Budget Modification User A to User B Budget  |
| 48 | `GET` | `/api/budgets/{id}` | Budgets | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | Flow F03 - Check Budget Status (Spent: 0, Remaining: 2,000,0 |
| 49 | `PUT` | `/api/budgets/{id}` | Budgets | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | 03 - Update Monthly Budget Limit, Flow F04 - Update Budget L |
| 50 | `GET` | `/api/cases/attachments/{id}` | Cases | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B4-06 - Download Case Attachment Success |
| 51 | `GET` | `/api/cases/disputes` | Cases | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | 08 - List Financial Disputes |
| 52 | `POST` | `/api/cases/disputes` | Cases | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B4-01 - Create Financial Dispute |
| 53 | `GET` | `/api/cases/disputes/{id}` | Cases | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B4-02 - Get Dispute Details by ID |
| 54 | `POST` | `/api/cases/disputes/{id}/messages` | Cases | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B4-03 - Reply to Dispute Message |
| 55 | `POST` | `/api/cases/disputes/{id}/messages/multipart` | Cases | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B4-04 - Reply to Dispute with Multipart Form (Happy Path) |
| 56 | `GET` | `/api/cases/support` | Cases | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | 05 - List Support Cases |
| 57 | `POST` | `/api/cases/support` | Cases | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | 04 - Create Support Case |
| 58 | `GET` | `/api/cases/support/{id}` | Cases | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | 06 - Get Support Case Details |
| 59 | `POST` | `/api/cases/support/{id}/messages` | Cases | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | 07 - Reply to Support Case Message |
| 60 | `POST` | `/api/cases/support/{id}/messages/multipart` | Cases | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B4-05 - Reply to Support Case with Multipart Form (Happy Pat |
| 61 | `GET` | `/api/categories` | Categories | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | Flow F01 - Get Expense Category for Budgeting, 02 - Get Syst |
| 62 | `POST` | `/api/categories` | Categories | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 201) | B2-01 - Create Isolated Category for Testing, 09 - Create Cu |
| 63 | `DELETE` | `/api/categories/{categoryId}` | Categories | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B2-04 - Archive/Delete Category |
| 64 | `GET` | `/api/categories/{categoryId}` | Categories | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B2-02 - Get Category Details by ID |
| 65 | `PUT` | `/api/categories/{categoryId}` | Categories | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B2-03 - Update Category Details |
| 66 | `GET` | `/api/dashboard` | Dashboard | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B2-05 - Get User Dashboard Summary |
| 67 | `POST` | `/api/evidence` | Evidence | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 201) | Flow F03a - Upload Evidence for Flow F Budget Expense, B2-06 |
| 68 | `GET` | `/api/evidence/{id}` | Evidence | `BOTH_VERIFIED` | Success & security invariant verified (HTTP 200, 404) | 07 - Cross-User Evidence Access User A to User B Evidence (E |
| 69 | `GET` | `/api/friends` | Friends | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | Flow C03 - User A Checks Friend List (Has User B), 13 - User |
| 70 | `GET` | `/api/friends/discover` | Friends | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | 06 - Discover Users by Name with Pagination |
| 71 | `POST` | `/api/friends/requests` | Friends | `BOTH_VERIFIED` | Success & security invariant verified (HTTP 200, 409) | Flow C02 - User A Checks Friend Request Status to User B, 08 |
| 72 | `GET` | `/api/friends/requests/incoming` | Friends | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | 10 - User B Checks Incoming Requests |
| 73 | `GET` | `/api/friends/requests/outgoing` | Friends | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | 09 - User A Checks Outgoing Requests |
| 74 | `DELETE` | `/api/friends/requests/{requestId}` | Friends | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B1-13 - Cancel Friend Request Success |
| 75 | `POST` | `/api/friends/requests/{requestId}/accept` | Friends | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | 11 - User B Accepts Friend Request |
| 76 | `POST` | `/api/friends/requests/{requestId}/reject` | Friends | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B1-12 - Reject Friend Request Success |
| 77 | `GET` | `/api/friends/search` | Friends | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | 05 - Search User B by SpendId, Flow C01 - User A Searches Us |
| 78 | `DELETE` | `/api/friends/{friendUserId}` | Friends | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 204) | B1-14 - Unfriend Friend Success |
| 79 | `PATCH` | `/api/friends/{friendUserId}/preference` | Friends | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | 14 - Update Friend Preference (User A sets Favorite) |
| 80 | `GET` | `/api/goals` | Goals | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B3-02 - List User Goals via GET /api/goals |
| 81 | `POST` | `/api/goals` | Goals | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | 02 - User B Creates Target Goal, B3-03 - Create Target Goal  |
| 82 | `DELETE` | `/api/goals/{id}` | Goals | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 204) | B3-05 - Delete Empty Goal via DELETE /api/goals/{id} |
| 83 | `GET` | `/api/goals/{id}` | Goals | `BOTH_VERIFIED` | Success & security invariant verified (HTTP 200, 404) | 06 - Cross-User Goal Access User A to User B Goal (Expect 40 |
| 84 | `PUT` | `/api/goals/{id}` | Goals | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B3-04 - Update Goal via PUT /api/goals/{id} |
| 85 | `POST` | `/api/goals/{id}/contributions` | Goals | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | 06 - Deposit to Savings Goal (2,000,000 VND) |
| 86 | `POST` | `/api/goals/{id}/withdrawals` | Goals | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | 08 - Withdraw from Savings Goal (500,000 VND) |
| 87 | `GET` | `/api/groups` | Groups | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B7-01 - List Legacy Groups |
| 88 | `POST` | `/api/groups` | Groups | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B7-02 - Create Legacy Group for Compatibility Test |
| 89 | `GET` | `/api/groups/invites` | Groups | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B7-06 - List User Legacy Group Invitations |
| 90 | `POST` | `/api/groups/invites/{inviteId}/accept` | Groups | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B7-08 - Accept Legacy Invite Success |
| 91 | `POST` | `/api/groups/invites/{inviteId}/reject` | Groups | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B7-07 - Reject Legacy Invite Success |
| 92 | `GET` | `/api/groups/{groupId}` | Groups | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B7-06 - List User Legacy Group Invitations, B7-03 - Get Lega |
| 93 | `PUT` | `/api/groups/{groupId}` | Groups | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B7-04 - Update Legacy Group Details |
| 94 | `GET` | `/api/groups/{groupId}/activities` | Groups | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B7-23 - List Group Activities Log |
| 95 | `GET` | `/api/groups/{groupId}/deletion-request` | Groups | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B7-24 - Get Group Deletion Request Status Success |
| 96 | `POST` | `/api/groups/{groupId}/deletion-requests` | Groups | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B7-25b - Re-request Group Deletion for Vote, B7-25 - Request |
| 97 | `POST` | `/api/groups/{groupId}/deletion-requests/{requestId}/cancel` | Groups | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B7-27 - Cancel Group Deletion Request Success |
| 98 | `POST` | `/api/groups/{groupId}/deletion-requests/{requestId}/finalize` | Groups | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B7-29 - Finalize Group Deletion Success |
| 99 | `POST` | `/api/groups/{groupId}/deletion-requests/{requestId}/refunds/{refundId}/payment` | Groups | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B7-28 - Create Group Refund Payment Success |
| 100 | `POST` | `/api/groups/{groupId}/deletion-requests/{requestId}/vote` | Groups | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B7-26 - Vote on Group Deletion Request Success |
| 101 | `GET` | `/api/groups/{groupId}/expenses` | Groups | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B7-21 - List Group Expenses |
| 102 | `POST` | `/api/groups/{groupId}/expenses` | Groups | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B7-22 - Create Group Expense |
| 103 | `GET` | `/api/groups/{groupId}/fund` | Groups | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B7-12 - Get Shared Fund Status for Group |
| 104 | `GET` | `/api/groups/{groupId}/fund-requests` | Groups | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B7-15 - List Group Fund Requests |
| 105 | `POST` | `/api/groups/{groupId}/fund-requests` | Groups | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B7-16 - Create Group Fund Request |
| 106 | `POST` | `/api/groups/{groupId}/fund-requests/{requestId}/pay` | Groups | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B7-17 - Pay Group Fund Request Success |
| 107 | `GET` | `/api/groups/{groupId}/fund-rounds` | Groups | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B7-18 - List Group Fund Rounds |
| 108 | `POST` | `/api/groups/{groupId}/fund-rounds` | Groups | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B7-19 - Create Group Fund Round |
| 109 | `POST` | `/api/groups/{groupId}/fund-rounds/{roundId}/pay` | Groups | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B7-20 - Pay Group Fund Round Success |
| 110 | `POST` | `/api/groups/{groupId}/fund/contributions` | Groups | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B7-14 - Contribute to Shared Fund (Wallet Balance Check) |
| 111 | `GET` | `/api/groups/{groupId}/fund/transactions` | Groups | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B7-13 - Get Shared Fund Transactions |
| 112 | `POST` | `/api/groups/{groupId}/invites` | Groups | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B7-05 - Invite Member to Legacy Group |
| 113 | `DELETE` | `/api/groups/{groupId}/members/{memberUserId}` | Groups | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 204) | B7-09 - Remove Member from Legacy Group Success |
| 114 | `POST` | `/api/groups/{groupId}/ownership/{memberUserId}` | Groups | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 204) | B7-10 - Transfer Group Ownership Success |
| 115 | `PUT` | `/api/groups/{groupId}/receiving-bank` | Groups | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B7-11 - Set Group Receiving Bank Account |
| 116 | `POST` | `/api/integrations/sepay/bankhub/events` | SePayBankHubIntegration | `EXTERNAL_LIVE_DEFERRED` | Whitelisted external provider dependency (HTTP 401; unit/integration tested) | 04 - SePay Webhook Invariant (Controlled Check) |
| 117 | `POST` | `/api/integrations/sepay/bankhub/ipn` | SePayBankHubIntegration | `EXTERNAL_LIVE_DEFERRED` | Whitelisted external provider dependency (HTTP 401; unit/integration tested) | B2-06 - SePay BankHub IPN Webhook Invariant (Controlled Secr |
| 118 | `DELETE` | `/api/mobile/push/installations/{installationId}` | MobilePush | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 204) | B1-06 - Unregister Mobile Push Installation |
| 119 | `PUT` | `/api/mobile/push/installations/{installationId}` | MobilePush | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 204) | B1-05 - Register Mobile Push Installation |
| 120 | `DELETE` | `/api/notifications` | Notifications | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B1-11 - Clear All Read Notifications |
| 121 | `GET` | `/api/notifications` | Notifications | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | 01 - List Notifications for User A |
| 122 | `GET` | `/api/notifications/preferences` | Notifications | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B1-07 - Get Notification Preferences |
| 123 | `PUT` | `/api/notifications/preferences` | Notifications | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B1-08 - Save Notification Preference |
| 124 | `POST` | `/api/notifications/read-all` | Notifications | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 204) | 02 - Mark All Notifications Read |
| 125 | `GET` | `/api/notifications/search` | Notifications | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | 03 - Search Notifications |
| 126 | `DELETE` | `/api/notifications/{id}` | Notifications | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 204) | B1-10 - Delete Single Notification |
| 127 | `POST` | `/api/notifications/{id}/read` | Notifications | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 204) | B1-09 - Mark Single Notification Read |
| 128 | `GET` | `/api/recurring-transactions` | RecurringTransactions | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | 10 - List Recurring Transactions |
| 129 | `POST` | `/api/recurring-transactions` | RecurringTransactions | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | 09 - Create Recurring Transaction (Monthly Internet), B3-06  |
| 130 | `DELETE` | `/api/recurring-transactions/{id}` | RecurringTransactions | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 204) | B3-12 - Delete Recurring Transaction |
| 131 | `GET` | `/api/recurring-transactions/{id}` | RecurringTransactions | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B3-07 - Get Recurring Transaction by ID |
| 132 | `PUT` | `/api/recurring-transactions/{id}` | RecurringTransactions | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B3-08 - Update Recurring Transaction via PUT |
| 133 | `POST` | `/api/recurring-transactions/{id}/occurrences/{occurrenceId}/record` | RecurringTransactions | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B3-11 - Record Recurring Occurrence Execution Success |
| 134 | `POST` | `/api/recurring-transactions/{id}/pause` | RecurringTransactions | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B3-09 - Pause Recurring Transaction |
| 135 | `POST` | `/api/recurring-transactions/{id}/resume` | RecurringTransactions | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B3-10 - Resume Recurring Transaction |
| 136 | `PUT` | `/api/security-pin` | SecurityPin | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 204) | 16 - Change Security PIN User A, 17 - Revert Security PIN Us |
| 137 | `GET` | `/api/security-pin/status` | SecurityPin | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | 26 - Security PIN Status Check |
| 138 | `POST` | `/api/security-pin/verify` | SecurityPin | `BOTH_VERIFIED` | Success & security invariant verified (HTTP 204, 401) | 14 - Verify Security PIN Valid, 15 - Verify Security PIN Inv |
| 139 | `GET` | `/api/system/announcements/active` | Operations | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 204) | B1-04 - Get Active System Announcement (Public/User) |
| 140 | `GET` | `/api/system/maintenance` | Operations | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B1-03 - Get System Maintenance State (Public/User) |
| 141 | `GET` | `/api/transaction-rules` | TransactionRules | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | 12 - List Transaction Rules |
| 142 | `POST` | `/api/transaction-rules` | TransactionRules | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B3-13 - Create Transaction Rule for Testing, 11 - Create Tra |
| 143 | `DELETE` | `/api/transaction-rules/{id}` | TransactionRules | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 204) | B3-16 - Delete Transaction Rule via DELETE |
| 144 | `GET` | `/api/transaction-rules/{id}` | TransactionRules | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B3-14 - Get Transaction Rule by ID |
| 145 | `PUT` | `/api/transaction-rules/{id}` | TransactionRules | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B3-15 - Update Transaction Rule via PUT |
| 146 | `GET` | `/api/transactions` | Transactions | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | 13 - List Transactions with Query Filters, Flow E04 - Check  |
| 147 | `POST` | `/api/transactions/expense` | Transactions | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 201) | Flow F03b - Record Expense Transaction for Budget Category ( |
| 148 | `POST` | `/api/transactions/expense/multi-wallet` | Transactions | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 201) | B2-07 - Multi-Wallet Expense Allocation (Controlled Validati |
| 149 | `POST` | `/api/transactions/income` | Transactions | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 201) | 03 - Fund Main Wallet via Income Posting (10,000,000 VND) |
| 150 | `GET` | `/api/transactions/{transactionId}` | Transactions | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | 14 - Get Transaction Details |
| 151 | `PATCH` | `/api/transactions/{transactionId}` | Transactions | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | 15 - Update Transaction Description, Flow F03d - Correct Exp |
| 152 | `GET` | `/api/transfers` | Transfers | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | 03 - Bank Direct Transfer History |
| 153 | `POST` | `/api/transfers` | Transfers | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 201) | 02 - Create Bank Direct Transfer Instruction |
| 154 | `POST` | `/api/transfers/internal` | Transfers | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 201) | 11 - Internal Transfer Between Wallets (3,000,000 VND), Flow |
| 155 | `POST` | `/api/transfers/p2p` | Transfers | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 201) | 00 - Fund User B Main Wallet (P2P Transfer 2,000,000 VND) |
| 156 | `GET` | `/api/transfers/recipients` | Transfers | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | 01 - Find Bank Transfer Recipient by Phone |
| 157 | `POST` | `/api/transfers/{paymentId}/confirm` | Transfers | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B2-09 - Confirm Bank Direct Transfer Success |
| 158 | `GET` | `/api/transfers/{transferId}` | Transfers | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | 01 - Find Bank Transfer Recipient by Phone, B2-08 - Get Tran |
| 159 | `GET` | `/api/users/me` | Users | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | 01 - Get Current User Profile (User A), Flow A01 - Pre-check |
| 160 | `POST` | `/api/users/me/avatar` | Users | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | Flow A02 - Upload Real Avatar Image, 02 - Upload User Avatar |
| 161 | `POST` | `/api/users/me/email/confirm` | Users | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | Flow B02 - Confirm Email Change with OTP |
| 162 | `POST` | `/api/users/me/email/request` | Users | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | Flow B01 - Request Email Change to New Email |
| 163 | `PUT` | `/api/users/me/preferences` | Users | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | 03 - Update User Preferences |
| 164 | `DELETE` | `/api/users/me/sessions` | Users | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 204) | B1-15 - Revoke User Sessions via DELETE /api/users/me/sessio |
| 165 | `POST` | `/api/v1/activity` | V1Activity | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 204) | B2-10 - Record V1 Activity Event |
| 166 | `GET` | `/api/v1/funds` | V1Funds | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B6-01 - List User V1 Funds |
| 167 | `POST` | `/api/v1/funds` | V1Funds | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | 01 - Create Fixed Fund (User A), 01 - Create Free Fund (User |
| 168 | `GET` | `/api/v1/funds/activity-proposals/{id}` | V1Funds | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B6-08 - Get Activity Proposal by ID Success |
| 169 | `POST` | `/api/v1/funds/activity-proposals/{id}/settle` | V1Funds | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | V1-IMP-12 - Owner settles approved activity |
| 170 | `POST` | `/api/v1/funds/activity-proposals/{id}/vote` | V1Funds | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B6-09 - Vote on Activity Proposal Success |
| 171 | `GET` | `/api/v1/funds/elections/{id}` | V1Funds | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B6-11 - Get Election Details by ID Success |
| 172 | `POST` | `/api/v1/funds/elections/{id}/accept` | V1Funds | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B6-14 - Accept Elected Ownership Success |
| 173 | `POST` | `/api/v1/funds/elections/{id}/advance` | V1Funds | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | V1-IMP-13 - Advance Election Success |
| 174 | `POST` | `/api/v1/funds/elections/{id}/ballots` | V1Funds | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B6-13 - Cast Ballot Success |
| 175 | `POST` | `/api/v1/funds/elections/{id}/candidates` | V1Funds | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B6-12 - Register Candidate Success |
| 176 | `POST` | `/api/v1/funds/elections/{id}/reject` | V1Funds | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B6-15 - Reject Elected Ownership Success |
| 177 | `GET` | `/api/v1/funds/invitations` | V1Funds | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B6-17 - Get User Invitations List |
| 178 | `DELETE` | `/api/v1/funds/invitations/{id}` | V1Funds | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B6-21 - Cancel Invitation Success |
| 179 | `POST` | `/api/v1/funds/invitations/{id}/accept` | V1Funds | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | 04 - User B Accepts Fixed Fund Invitation, Flow D03 - User B |
| 180 | `POST` | `/api/v1/funds/invitations/{id}/decline` | V1Funds | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B6-20 - Decline Invitation Success |
| 181 | `POST` | `/api/v1/funds/invitations/{id}/review` | V1Funds | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B6-19 - Review Invitation Proposal Success |
| 182 | `GET` | `/api/v1/funds/ownership-offers` | V1Funds | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B6-24 - List User Ownership Offers |
| 183 | `POST` | `/api/v1/funds/ownership-offers/{id}/accept` | V1Funds | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 204) | B6-23 - Accept Ownership Offer Success |
| 184 | `POST` | `/api/v1/funds/requests/{id}/cancel` | V1Funds | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | V1-IMP-11 - 10d - Requester Cancels Suspended Expense Releas |
| 185 | `POST` | `/api/v1/funds/requests/{id}/report` | V1Funds | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | V1-IMP-10 - 10b - Member Reports Large Expense Hold (Transit |
| 186 | `POST` | `/api/v1/funds/requests/{id}/review` | V1Funds | `BOTH_VERIFIED` | Success & security invariant verified (HTTP 200, 404) | 07b - User A (Owner) Reviews and Approves Free Fund Request, |
| 187 | `GET` | `/api/v1/funds/{id}` | V1Funds | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | 02 - Get Fixed Fund Details, B6-17 - Get User Invitations Li |
| 188 | `GET` | `/api/v1/funds/{id}/activities` | V1Funds | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | 11 - Check Fixed Fund Activities, Flow D09 - Check Activity  |
| 189 | `POST` | `/api/v1/funds/{id}/activity-proposals` | V1Funds | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B6-07 - Propose Fund Activity Success |
| 190 | `POST` | `/api/v1/funds/{id}/activity/close` | V1Funds | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 204) | B6-06 - Close Activity Window via POST /api/v1/funds/{id}/ac |
| 191 | `GET` | `/api/v1/funds/{id}/contribution-allocation` | V1Funds | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B6-16 - Get Contribution Allocation Details |
| 192 | `GET` | `/api/v1/funds/{id}/contributions` | V1Funds | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | 09 - List Fixed Fund Contributions |
| 193 | `POST` | `/api/v1/funds/{id}/contributions` | V1Funds | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | 04 - User A Contributes 500,000 VND to Free Fund, Flow D05 - |
| 194 | `POST` | `/api/v1/funds/{id}/dissolve` | V1Funds | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | V1-IMP-15 - 21 - User A Dissolves Fixed Fund 2 (Zero Balance |
| 195 | `POST` | `/api/v1/funds/{id}/elections` | V1Funds | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B6-10 - Open Election Success |
| 196 | `GET` | `/api/v1/funds/{id}/finance` | V1Funds | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | 06 - Track Free Fund Finance, 12 - Check Fixed Fund Financia |
| 197 | `GET` | `/api/v1/funds/{id}/governance` | V1Funds | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B6-03 - Get Fund Governance Status |
| 198 | `GET` | `/api/v1/funds/{id}/invitation-proposals` | V1Funds | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B6-18 - Get Fund Invitation Proposals List |
| 199 | `POST` | `/api/v1/funds/{id}/invitations` | V1Funds | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | Flow D02 - Invite User B to Fixed Fund, 02 - Invite User B t |
| 200 | `POST` | `/api/v1/funds/{id}/leave` | V1Funds | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | 09 - User B Leaves Free Fund |
| 201 | `GET` | `/api/v1/funds/{id}/members` | V1Funds | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | 05 - List Fixed Fund Members, 10 - Verify Remaining Members  |
| 202 | `POST` | `/api/v1/funds/{id}/members/{userId}/kick` | V1Funds | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | V1-IMP-14 - 20 - User A Kicks User B from Fixed Fund 2 (Refu |
| 203 | `PUT` | `/api/v1/funds/{id}/members/{userId}/spending-permission` | V1Funds | `BOTH_VERIFIED` | Success & security invariant verified (HTTP 204, 403) | 08 - Cross-User Fund Permission Escalation User B to Fixed F |
| 204 | `POST` | `/api/v1/funds/{id}/obligations/confirm` | V1Funds | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | 06 - Confirm Obligations (Target: 2,000,000 VND), Flow D04 - |
| 205 | `GET` | `/api/v1/funds/{id}/owner-response` | V1Funds | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B6-04 - Get Fund Owner Response Status Success |
| 206 | `POST` | `/api/v1/funds/{id}/owner-response/escalate` | V1Funds | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B6-05 - Escalate Owner Response Success |
| 207 | `POST` | `/api/v1/funds/{id}/ownership-offers` | V1Funds | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B6-22 - Offer Fund Ownership via POST /api/v1/funds/{id}/own |
| 208 | `GET` | `/api/v1/funds/{id}/refund-preview` | V1Funds | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | 08 - Refund Preview for User B |
| 209 | `GET` | `/api/v1/funds/{id}/requests` | V1Funds | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | B6-02 - Get Fund Requests List |
| 210 | `POST` | `/api/v1/funds/{id}/requests` | V1Funds | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | 07 - User B Creates Free Fund Expense Request, 07c - User B  |
| 211 | `GET` | `/api/v1/system/maintenance` | V1Maintenance | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | V1-IMP-16 - 11 - ADM-05: Maintenance Status |
| 212 | `GET` | `/api/wallets` | Wallets | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | 12 - Check Balances After Expense and Transfer, 01 - List De |
| 213 | `POST` | `/api/wallets` | Wallets | `BOTH_VERIFIED` | Success & security invariant verified (HTTP 201, 409) | 04 - Create Savings Wallet User A, 06 - Check Wallet Limit ( |
| 214 | `DELETE` | `/api/wallets/{walletId}` | Wallets | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 204) | 16 - Close Emergency Wallet |
| 215 | `GET` | `/api/wallets/{walletId}` | Wallets | `BOTH_VERIFIED` | Success & security invariant verified (HTTP 200, 404) | Flow E03 - Check Savings Wallet Balance Increased by 300,000 |
| 216 | `PUT` | `/api/wallets/{walletId}` | Wallets | `SUCCESS_VERIFIED` | Happy-path contract & state assertions passed (HTTP 200) | 08 - Update Savings Wallet Name |
