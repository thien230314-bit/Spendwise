# Kiểm chứng hợp đồng API Frontend — đang đối chiếu

Backend nguồn: `53b6d6e24bf0612647fd8a61409623eb05187136`. Chỉ sửa Markdown.

Bảng là inventory các route đầy đủ có HTTP method trong User.md/Admin.md. Tìm thấy attribute chỉ là ứng viên, chưa xác nhận DTO, policy, status hoặc tác động. Route viết tắt/ghép, nhiều controller prefix hoặc route ràng buộc đặc biệt cần mở source thủ công. `BLOCKED` ở từng dòng nghĩa là chưa đủ bằng chứng cho dòng đó; không hàm ý backend lỗi.

## Bằng chứng tái sử dụng

- [Coverage User lịch sử](../POSTMAN-ENDPOINT-COVERAGE.md) và [Newman User final](../reports/newman/backend-user-final-verified-summary.md): 216 operations của baseline, 306 requests, 343/343 assertions. [Báo cáo E2E](../POSTMAN-E2E-TEST.md) giữ riêng suite Admin và Support.
- [Newman đã làm sạch](../backend/postman/evidence/newman_execution_evidence_sanitized.json): 300 requests, 339 assertions, 0 lỗi ghi nhận; testRunSha `852c182370bd4c7286d7b72c0276c13d22e6d200`. Không gộp với lượt 306/343.
- Raw Admin Newman hiện có tại `reports/newman/ADMIN-V1-FINAL-NEWMAN-REPORT-LIVE.json`: 80 requests, 138 assertions, 0 lỗi theo run.stats. Chỉ tái dùng số liệu; không chép token, dữ liệu cá nhân hay payload raw.
- [OpenAPI operation baseline](../backend/postman/contracts/openapi_contract_baseline.json) có 339 operations; [CSV operation evidence](../backend/docs/USER-FINAL-OPERATION-EVIDENCE.csv) có 219 dòng. Baseline này là chỉ mục method/path/operationId, không phải schema DTO đầy đủ.
- Đối chiếu bối cảnh với [alignment](../backend/docs/USER-FRONTEND-API-ALIGNMENT.md), [gap matrix](../backend/docs/USER-BACKEND-FRONTEND-GAP-MATRIX.md), [Postman README](../backend/postman/README.md), [documentation sync](../reports/documentation/documentation-sync-summary.md), [Fixed V2](../fixed.md) và [Free V1](../FREE-FUND-BUSINESS-CONFORMANCE.md).

| Collection | Request khai báo | Có body | Có test script riêng |
|---|---:|---:|---:|
| `SpendWise-User-API.postman_collection.json` | 303 | 168 | 303 |
| `reports/postman/spendwise_e2e_comprehensive_collection.json` | 116 | 85 | 116 |
| `ADMIN-V1-FINAL-NEWMAN.postman_collection.json` | 80 | 49 | 80 |

Ghép method + đường dẫn, bỏ query và chuẩn hóa tên placeholder. URL thực thi chứa ID cụ thể có thể không ghép với template; số ghép là cận dưới, không phải tỷ lệ coverage.

| Phạm vi | Route trong ma trận | Ghép OpenAPI | Ghép CSV | Ghép sanitized Newman |
|---|---:|---:|---:|---:|
| User | 221 | 200 | 202 | 85 |
| Admin | 47 | 47 | 0 | 0 |

Không nâng các dòng cũ lên VERIFIED chỉ vì có execution lịch sử. Các dòng chưa xác minh đủ là điểm kiểm tra khi nối UI, không phải lý do chặn mọi màn hình. Dùng source SHA khóa cho DTO, quyền và tác động chưa có assertion. Đổi email hai OTP thay contract một OTP; Fixed V2 không dùng quyền chi 7 ngày của Free V1; Free V1 `initiate-payment` trả 409 `fund_payment_provider_unavailable` và giữ `ApprovedUnused` (V1FundRequests.cs:476–491).

Contract sửa theo source: Admin tickets dùng A2Page; request-info trả WaitingForUser; Broadcast publish/cancel trả 204, status trả bộ đếm riêng; health dùng JSON object theo GetAdminHealthQueryHandler; SePay status dùng environment, không có webhookEndpoint. ADM-18 không chuyển tiền.

## User: 220 route ban đầu + 1 legacy confirm được tách riêng

| Method | Route | Auth | Request DTO | Response DTO | Status | Business effect | Code evidence | Kết quả |
|---|---|---|---|---|---|---|---|---|
| POST | `/api/cases/support` | — | — | — | — | — | Chưa ghép attribute; User.md:101 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/cases/disputes` | — | — | — | — | — | Chưa ghép attribute; User.md:101 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/v1/funds/requests/{id}/report` | — | — | — | — | — | V1FundsController.cs:34 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/auth/register` | — | — | — | — | — | AuthController.cs:36 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/auth/email-verification/verify` | — | — | — | — | — | AuthController.cs:51 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/auth/email-verification/resend` | — | — | — | — | — | AuthController.cs:62 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/auth/login` | — | — | — | — | — | AuthController.cs:70 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/auth/pin/setup` | — | — | — | — | — | AuthController.cs:85 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/auth/pin/verify` | — | — | — | — | — | AuthController.cs:95 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/auth/refresh` | — | — | — | — | — | AuthController.cs:105 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/auth/logout` | — | — | — | — | — | AuthController.cs:120 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/auth/sessions` | — | — | — | — | — | AuthController.cs:135 | BLOCKED: chưa hoàn tất đối chiếu |
| DELETE | `/api/auth/sessions/{sessionId}` | — | — | — | — | — | AuthController.cs:140 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/auth/change-password` | — | — | — | — | — | AuthController.cs:150 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/auth/forgot-password` | — | — | — | — | — | AuthController.cs:164 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/auth/reset-password` | — | — | — | — | — | AuthController.cs:191 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/auth/pin/forgot` | — | — | — | — | — | AuthController.cs:203 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/auth/pin/reset/validate` | — | — | — | — | — | AuthController.cs:218 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/auth/pin/reset` | — | — | — | — | — | AuthController.cs:225 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/security-pin/status` | — | — | — | — | — | SecurityPinController.cs:21 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/security-pin/verify` | — | — | — | — | — | SecurityPinController.cs:25 | BLOCKED: chưa hoàn tất đối chiếu |
| PUT | `/api/security-pin` | — | — | — | — | — | SecurityPinController.cs:36 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/account-recovery` | — | — | — | — | — | AccountRecoveryController.cs:16 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/auth/passkeys/register/options` | — | — | — | — | — | PasskeysController.cs:18 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/auth/passkeys/register/verify` | — | — | — | — | — | PasskeysController.cs:26 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/auth/passkeys/login/options` | — | — | — | — | — | PasskeysController.cs:36 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/auth/passkeys/login/verify` | — | — | — | — | — | PasskeysController.cs:46 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/auth/passkeys` | — | — | — | — | — | PasskeysController.cs:58 | BLOCKED: chưa hoàn tất đối chiếu |
| DELETE | `/api/auth/passkeys/{id}` | — | — | — | — | — | PasskeysController.cs:66 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/users/me` | — | — | — | — | — | UsersController.cs:27 | BLOCKED: chưa hoàn tất đối chiếu |
| PUT | `/api/users/me/preferences` | — | — | — | — | — | UsersController.cs:137 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/users/me/avatar` | — | — | — | — | — | UsersController.cs:37 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/users/me/email/current/request` | Bearer; self | `Không body` | `EmailVerificationPendingResponse {status,expiresAt,resendAfterSeconds}` | 200 | Xếp OTP email hiện tại vào outbox; 403 account_restricted; 429 cooldown | UsersController.cs:67; ChangeEmailCommands.cs:15; EmailVerificationSupport.cs:295 | VERIFIED |
| POST | `/api/users/me/email/current/verify` | Bearer; self | `VerifyCurrentEmailOtpRequest {otp}` | `VerifyCurrentEmailOtpResponse {ticket,expiresAt}` | 200 | Xác thực OTP email hiện tại, cấp ticket gắn user/session; OTP không hợp lệ bị từ chối | UsersController.cs:76; ChangeEmailCommands.cs:41; EmailVerificationSupport.cs:391 | VERIFIED |
| POST | `/api/users/me/email/new/request` | Bearer; self | `RequestNewEmailOtpRequest {ticket,newEmail}` | `EmailVerificationPendingResponse {status,expiresAt,resendAfterSeconds}` | 200 | Lưu email chờ và xếp OTP thứ hai; 400 email_change_ticket_invalid, 409 duplicate_email | UsersController.cs:86; ChangeEmailCommands.cs:72; EmailVerificationSupport.cs:486 | VERIFIED |
| POST | `/api/users/me/email/new/confirm` | Bearer; self | `ConfirmNewEmailOtpRequest {ticket,otp}` | `UserProfile` | 200 | Đổi email, cảnh báo địa chỉ cũ, thu hồi các phiên khác; 401 session_revoked, 400 no_pending_email, 409 duplicate_email | UsersController.cs:96; ChangeEmailCommands.cs:102; EmailVerificationSupport.cs:619 | VERIFIED |
| POST | `/api/users/me/email/cancel` | Bearer; self | `CancelEmailChangeRequest? {ticket?}` | `Không body` | 204 | Thu hồi mọi token đổi email chưa dùng của user; xóa email chờ; ticket không thu hẹp phạm vi | UsersController.cs:106; ChangeEmailCommands.cs:132; EmailVerificationSupport.cs:775 | CORRECTED |
| POST | `/api/users/me/email/request` | Bearer; self | `Không body` | `{code,message}` | 400 | Chặn legacy_email_change_deprecated | UsersController.cs:115 | VERIFIED |
| POST | `/api/users/me/email/confirm` | Bearer; self | `Không body` | `{code,message}` | 400 | Chặn legacy_email_change_deprecated | UsersController.cs:126 | VERIFIED |
| DELETE | `/api/users/me/sessions` | — | — | — | — | — | UsersController.cs:149 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/friends/search` | — | — | — | — | — | FriendsController.cs:17 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/friends/discover` | — | — | — | — | — | FriendsController.cs:21 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/friends` | — | — | — | — | — | FriendsController.cs:30 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/friends/requests/incoming` | — | — | — | — | — | FriendsController.cs:34 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/friends/requests/outgoing` | — | — | — | — | — | FriendsController.cs:38 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/friends/requests` | — | — | — | — | — | FriendsController.cs:42 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/friends/requests/{requestId}/accept` | — | — | — | — | — | FriendsController.cs:50 | BLOCKED: chưa hoàn tất đối chiếu |
| DELETE | `/api/friends/requests/{requestId}` | — | — | — | — | — | FriendsController.cs:46 | BLOCKED: chưa hoàn tất đối chiếu |
| DELETE | `/api/friends/{friendUserId}` | — | — | — | — | — | FriendsController.cs:58 | BLOCKED: chưa hoàn tất đối chiếu |
| PATCH | `/api/friends/{friendUserId}/preference` | — | — | — | — | — | FriendsController.cs:65 | BLOCKED: chưa hoàn tất đối chiếu |
| PUT | `/api/mobile/push/installations/{installationId}` | — | — | — | — | — | MobilePushController.cs:16 | BLOCKED: chưa hoàn tất đối chiếu |
| DELETE | `/api/mobile/push/installations/{installationId}` | — | — | — | — | — | MobilePushController.cs:28 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/wallets` | — | — | — | — | — | WalletsController.cs:21 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/wallets/{walletId}` | — | — | — | — | — | WalletsController.cs:31 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/wallets` | — | — | — | — | — | WalletsController.cs:41 | BLOCKED: chưa hoàn tất đối chiếu |
| PUT | `/api/wallets/{walletId}` | — | — | — | — | — | WalletsController.cs:57 | BLOCKED: chưa hoàn tất đối chiếu |
| DELETE | `/api/wallets/{walletId}` | — | — | — | — | — | WalletsController.cs:67 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/transactions` | — | — | — | — | — | TransactionsController.cs:25 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/transactions/{transactionId}` | — | — | — | — | — | TransactionsController.cs:37 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/transactions/income` | — | — | — | — | — | TransactionsController.cs:49 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/transactions/expense` | — | — | — | — | — | TransactionsController.cs:66 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/transactions/expense/multi-wallet` | — | — | — | — | — | TransactionsController.cs:83 | BLOCKED: chưa hoàn tất đối chiếu |
| PATCH | `/api/transactions/{transactionId}` | — | — | — | — | — | TransactionsController.cs:96 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/categories` | — | — | — | — | — | CategoriesController.cs:21 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/categories/{id}` | — | — | — | — | — | CategoriesController.cs:34 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/categories` | — | — | — | — | — | CategoriesController.cs:41 | BLOCKED: chưa hoàn tất đối chiếu |
| PUT | `/api/categories/{id}` | — | — | — | — | — | CategoriesController.cs:54 | BLOCKED: chưa hoàn tất đối chiếu |
| DELETE | `/api/categories/{id}` | — | — | — | — | — | CategoriesController.cs:64 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/evidence` | — | — | — | — | — | EvidenceController.cs:19 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/evidence/{id}` | — | — | — | — | — | EvidenceController.cs:43 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/dashboard` | — | — | — | — | — | DashboardController.cs:14 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/transaction-authorizations/options` | — | — | — | — | — | TransactionAuthorizationsController.cs:17 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/transaction-authorizations/verify` | — | — | — | — | — | TransactionAuthorizationsController.cs:27 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/transfers/internal` | — | — | — | — | — | TransfersController.cs:80 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/transfers/p2p` | — | — | — | — | — | TransfersController.cs:95 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/transfers/recipients` | — | — | — | — | — | TransfersController.cs:22 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/transfers` | — | — | — | — | — | TransfersController.cs:46 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/transfers` | — | — | — | — | — | TransfersController.cs:66 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/transfers/{transferId}` | — | — | — | — | — | TransfersController.cs:34 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/transfers/{paymentId}/confirm` | — | — | — | — | — | TransfersController.cs:72 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/bank-connections/link` | — | — | — | — | — | BankConnectionsController.cs:25 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/bank-connections` | — | — | — | — | — | BankConnectionsController.cs:35 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/bank-connections/{id}` | — | — | — | — | — | BankConnectionsController.cs:41 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/bank-connections/{id}/unlink` | — | — | — | — | — | BankConnectionsController.cs:47 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/bank-connections/{id}/reactivate` | — | — | — | — | — | BankConnectionsController.cs:59 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/bank-connections/{id}/sync` | — | — | — | — | — | BankConnectionsController.cs:69 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/bank-transactions` | — | — | — | — | — | BankTransactionsController.cs:20 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/bank-transactions/{id}` | — | — | — | — | — | BankTransactionsController.cs:29 | BLOCKED: chưa hoàn tất đối chiếu |
| PATCH | `/api/bank-transactions/{id}/category` | — | — | — | — | — | BankTransactionsController.cs:35 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/bank-connections/test-mode/link-challenges` | — | — | — | — | — | BankConnectionsController.cs:76 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/bank-connections/test-mode/link-challenges/{challengeId}/confirm` | — | — | — | — | — | BankConnectionsController.cs:81 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/bank-connections/test-mode/linked-accounts` | — | — | — | — | — | BankConnectionsController.cs:101 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/bank-connections/test-mode/wallet-top-ups` | — | — | — | — | — | BankConnectionsController.cs:88 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/bank-connections/test-mode/wallet-top-ups` | — | — | — | — | — | BankConnectionsController.cs:106 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/bank-connections/test-mode/wallet-top-ups/{topUpId}/confirm` | — | — | — | — | — | BankConnectionsController.cs:95 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/bank-connections/test-mode/wallet-withdrawals` | — | — | — | — | — | BankConnectionsController.cs:111 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/bank-connections/test-mode/wallet-withdrawals` | — | — | — | — | — | BankConnectionsController.cs:118 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/bank-connections/test-mode/wallet-withdrawals/{id}` | — | — | — | — | — | BankConnectionsController.cs:123 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/bank-connections/test-mode/wallet-withdrawals/{id}/confirm` | — | — | — | — | — | BankConnectionsController.cs:129 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/bank-connections/test-mode/wallet-withdrawals/{id}/cancel` | — | — | — | — | — | BankConnectionsController.cs:136 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/groups` | — | — | — | — | — | GroupsController.cs:34 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/groups/{groupId}/invites` | — | — | — | — | — | GroupsController.cs:42 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/groups/invites` | — | — | — | — | — | GroupsController.cs:46 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/groups/invites/{inviteId}/accept` | — | — | — | — | — | GroupsController.cs:50 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/groups/invites/{inviteId}/reject` | — | — | — | — | — | GroupsController.cs:54 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/groups` | — | — | — | — | — | GroupsController.cs:26 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/groups/{groupId}` | — | — | — | — | — | GroupsController.cs:30 | BLOCKED: chưa hoàn tất đối chiếu |
| PUT | `/api/groups/{groupId}` | — | — | — | — | — | GroupsController.cs:38 | BLOCKED: chưa hoàn tất đối chiếu |
| DELETE | `/api/groups/{groupId}/members/{memberUserId}` | — | — | — | — | — | GroupsController.cs:58 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/groups/{groupId}/ownership/{memberUserId}` | — | — | — | — | — | GroupsController.cs:65 | BLOCKED: chưa hoàn tất đối chiếu |
| PUT | `/api/groups/{groupId}/receiving-bank` | — | — | — | — | — | GroupsController.cs:72 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/groups/{groupId}/fund` | — | — | — | — | — | GroupsController.cs:76 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/groups/{groupId}/fund/transactions` | — | — | — | — | — | GroupsController.cs:80 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/groups/{groupId}/fund/contributions` | — | — | — | — | — | GroupsController.cs:84 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/groups/{groupId}/fund-requests` | — | — | — | — | — | GroupsController.cs:89 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/groups/{groupId}/fund-requests` | — | — | — | — | — | GroupsController.cs:93 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/groups/{groupId}/fund-requests/{requestId}/pay` | — | — | — | — | — | GroupsController.cs:97 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/groups/{groupId}/fund-rounds` | — | — | — | — | — | GroupsController.cs:102 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/groups/{groupId}/fund-rounds` | — | — | — | — | — | GroupsController.cs:106 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/groups/{groupId}/fund-rounds/{roundId}/pay` | — | — | — | — | — | GroupsController.cs:110 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/groups/{groupId}/expenses` | — | — | — | — | — | GroupsController.cs:115 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/groups/{groupId}/expenses` | — | — | — | — | — | GroupsController.cs:119 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/groups/{groupId}/activities` | — | — | — | — | — | GroupsController.cs:124 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/groups/{groupId}/deletion-request` | — | — | — | — | — | GroupsController.cs:128 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/groups/{groupId}/deletion-requests` | — | — | — | — | — | GroupsController.cs:132 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/groups/{groupId}/deletion-requests/{requestId}/vote` | — | — | — | — | — | GroupsController.cs:136 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/groups/{groupId}/deletion-requests/{requestId}/cancel` | — | — | — | — | — | GroupsController.cs:140 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/groups/{groupId}/deletion-requests/{requestId}/refunds/{refundId}/payment` | — | — | — | — | — | GroupsController.cs:144 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/groups/{groupId}/deletion-requests/{requestId}/finalize` | — | — | — | — | — | GroupsController.cs:150 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/v1/funds` | — | — | — | — | — | V1FundsController.cs:44 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/v1/funds/{id}/obligations/confirm` | — | — | — | — | — | V1FundsController.cs:127 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/v1/funds/{id}/contribution-allocation` | — | — | — | — | — | V1FundsController.cs:121 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/v1/funds/{id}/finance` | — | — | — | — | — | V1FundsController.cs:124 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/v1/funds/{id}/contributions` | — | — | — | — | — | V1FundsController.cs:131 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/v1/funds/{id}/contributions` | — | — | — | — | — | V1FundsController.cs:135 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/v1/funds/{id}/members` | — | — | — | — | — | V1FundsController.cs:54 | BLOCKED: chưa hoàn tất đối chiếu |
| PUT | `/api/v1/funds/{id}/members/{userId}/spending-permission` | — | — | — | — | — | V1FundsController.cs:172 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/v1/funds/{id}/requests` | — | — | — | — | — | V1FundsController.cs:25 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/v1/funds/{id}/requests` | — | — | — | — | — | V1FundsController.cs:27 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/v1/funds/{id}/activity/close` | — | — | — | — | — | V1FundsController.cs:66 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/v1/funds/{id}/activity-proposals` | — | — | — | — | — | V1FundsController.cs:70 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/v1/funds/activity-proposals/{id}` | — | — | — | — | — | V1FundsController.cs:73 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/v1/funds/activity-proposals/{id}/vote` | — | — | — | — | — | V1FundsController.cs:83 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/v1/funds/activity-proposals/{id}/confirm-obligation` | — | — | — | — | — | V1FundsController.cs:76 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/v1/funds/activity-proposals/{id}/settle` | — | — | — | — | — | V1FundsController.cs:80 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/v1/funds/{id}/elections` | — | — | — | — | — | V1FundsController.cs:94 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/v1/funds/elections/{id}/ballots` | — | — | — | — | — | V1FundsController.cs:105 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/v1/funds/elections/{id}/advance` | — | — | — | — | — | V1FundsController.cs:109 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/v1/funds/elections/{id}/accept` | — | — | — | — | — | V1FundsController.cs:113 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/v1/funds/elections/{id}/reject` | — | — | — | — | — | V1FundsController.cs:117 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/v1/funds/{id}/refund-preview` | — | — | — | — | — | V1FundsController.cs:196 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/v1/funds/{id}/leave` | — | — | — | — | — | V1FundsController.cs:200 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/v1/funds/{id}/members/{userId}/kick` | — | — | — | — | — | V1FundsController.cs:205 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/v1/funds/{id}/dissolve` | — | — | — | — | — | V1FundsController.cs:210 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/v1/funds/{id}/fail-period-early` | — | — | — | — | — | V1FundsController.cs:215 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/v1/funds/{id}/activities` | — | — | — | — | — | V1FundsController.cs:139 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/v1/funds/{id}/owner-response` | — | — | — | — | — | V1FundsController.cs:60 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/v1/funds/{id}/owner-response/escalate` | — | — | — | — | — | V1FundsController.cs:63 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/v1/funds` | — | — | — | — | — | V1FundsController.cs:41 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/v1/funds/{id}` | — | — | — | — | — | V1FundsController.cs:48 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/v1/funds/{id}/invitations` | — | — | — | — | — | V1FundsController.cs:147 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/v1/funds/invitations` | — | — | — | — | — | V1FundsController.cs:150 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/v1/funds/invitations/{id}/accept` | — | — | — | — | — | V1FundsController.cs:168 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/v1/funds/requests/{id}/review` | — | — | — | — | — | V1FundsController.cs:31 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/v1/funds/requests/{id}/cancel` | — | — | — | — | — | V1FundsController.cs:36 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/v1/funds/requests/{id}/initiate-payment` | — | — | — | — | — | V1FundsController.cs:38 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/budgets` | — | — | — | — | — | PlanningControllers.cs:42; PlanningControllers.cs:130; PlanningControllers.cs:219; PlanningControllers.cs:297 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/budgets/{id}` | — | — | — | — | — | PlanningControllers.cs:55; PlanningControllers.cs:140; PlanningControllers.cs:229; PlanningControllers.cs:307 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/budgets` | — | — | — | — | — | PlanningControllers.cs:62; PlanningControllers.cs:147; PlanningControllers.cs:236; PlanningControllers.cs:314 | BLOCKED: chưa hoàn tất đối chiếu |
| PUT | `/api/budgets` | — | — | — | — | — | PlanningControllers.cs:72 | BLOCKED: chưa hoàn tất đối chiếu |
| PUT | `/api/budgets/{id}` | — | — | — | — | — | PlanningControllers.cs:95; PlanningControllers.cs:159; PlanningControllers.cs:248; PlanningControllers.cs:326 | BLOCKED: chưa hoàn tất đối chiếu |
| DELETE | `/api/budgets/{id}` | — | — | — | — | — | PlanningControllers.cs:105; PlanningControllers.cs:169; PlanningControllers.cs:258; PlanningControllers.cs:336 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/goals` | — | — | — | — | — | Chưa ghép attribute; User.md:480 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/goals/{id}` | — | — | — | — | — | Chưa ghép attribute; User.md:480 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/goals` | — | — | — | — | — | Chưa ghép attribute; User.md:481 | BLOCKED: chưa hoàn tất đối chiếu |
| PUT | `/api/goals/{id}` | — | — | — | — | — | Chưa ghép attribute; User.md:482 | BLOCKED: chưa hoàn tất đối chiếu |
| DELETE | `/api/goals/{id}` | — | — | — | — | — | Chưa ghép attribute; User.md:483 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/goals/{id}/contributions` | — | — | — | — | — | Chưa ghép attribute; User.md:484 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/goals/{id}/withdrawals` | — | — | — | — | — | Chưa ghép attribute; User.md:485 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/recurring-transactions` | — | — | — | — | — | Chưa ghép attribute; User.md:491 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/recurring-transactions/{id}` | — | — | — | — | — | Chưa ghép attribute; User.md:491 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/recurring-transactions` | — | — | — | — | — | Chưa ghép attribute; User.md:492 | BLOCKED: chưa hoàn tất đối chiếu |
| PUT | `/api/recurring-transactions/{id}` | — | — | — | — | — | Chưa ghép attribute; User.md:493 | BLOCKED: chưa hoàn tất đối chiếu |
| DELETE | `/api/recurring-transactions/{id}` | — | — | — | — | — | Chưa ghép attribute; User.md:494 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/recurring-transactions/{id}/pause` | — | — | — | — | — | Chưa ghép attribute; User.md:495 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/recurring-transactions/{id}/occurrences/{occurrenceId}/record` | — | — | — | — | — | Chưa ghép attribute; User.md:496 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/transaction-rules` | — | — | — | — | — | Chưa ghép attribute; User.md:498 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/transaction-rules/{id}` | — | — | — | — | — | Chưa ghép attribute; User.md:498 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/transaction-rules` | — | — | — | — | — | Chưa ghép attribute; User.md:498 | BLOCKED: chưa hoàn tất đối chiếu |
| PUT | `/api/transaction-rules/{id}` | — | — | — | — | — | Chưa ghép attribute; User.md:498 | BLOCKED: chưa hoàn tất đối chiếu |
| DELETE | `/api/transaction-rules/{id}` | — | — | — | — | — | Chưa ghép attribute; User.md:498 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/cases/support` | — | — | — | — | — | Chưa ghép attribute; User.md:507 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/cases/support/{id}` | — | — | — | — | — | Chưa ghép attribute; User.md:507 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/cases/support/{id}/messages` | — | — | — | — | — | Chưa ghép attribute; User.md:508 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/cases/support/{id}/messages/multipart` | — | — | — | — | — | Chưa ghép attribute; User.md:509 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/cases/disputes` | — | — | — | — | — | Chưa ghép attribute; User.md:511 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/cases/disputes/{id}` | — | — | — | — | — | Chưa ghép attribute; User.md:511 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/cases/disputes/{id}/messages` | — | — | — | — | — | Chưa ghép attribute; User.md:512 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/cases/attachments/{id}` | — | — | — | — | — | Chưa ghép attribute; User.md:513 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/ai/conversations` | — | — | — | — | — | AiController.cs:25 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/ai/conversations/{id}` | — | — | — | — | — | AiController.cs:34 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/ai/conversations` | — | — | — | — | — | AiController.cs:41 | BLOCKED: chưa hoàn tất đối chiếu |
| PUT | `/api/ai/conversations/{id}` | — | — | — | — | — | AiController.cs:54 | BLOCKED: chưa hoàn tất đối chiếu |
| DELETE | `/api/ai/conversations/{id}` | — | — | — | — | — | AiController.cs:64 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/ai/chat` | — | — | — | — | — | AiController.cs:74 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/ai/attachments` | — | — | — | — | — | AiController.cs:91 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/ai/attachments/{id}` | — | — | — | — | — | AiController.cs:102 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/ai/actions/{id}` | — | — | — | — | — | AiController.cs:109 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/ai/actions/{id}/execute` | — | — | — | — | — | AiController.cs:112 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/ai/actions/{id}/cancel` | — | — | — | — | — | AiController.cs:116 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/notifications` | — | — | — | — | — | NotificationsController.cs:22 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/notifications/search` | — | — | — | — | — | NotificationsController.cs:53 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/notifications/{id}/read` | — | — | — | — | — | NotificationsController.cs:33 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/notifications/read-all` | — | — | — | — | — | NotificationsController.cs:43 | BLOCKED: chưa hoàn tất đối chiếu |
| DELETE | `/api/notifications/{id}` | — | — | — | — | — | NotificationsController.cs:57 | BLOCKED: chưa hoàn tất đối chiếu |
| DELETE | `/api/notifications` | — | — | — | — | — | NotificationsController.cs:60 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/notifications/preferences` | — | — | — | — | — | NotificationsController.cs:63 | BLOCKED: chưa hoàn tất đối chiếu |
| PUT | `/api/notifications/preferences` | — | — | — | — | — | NotificationsController.cs:66 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/system/maintenance` | — | — | — | — | — | OperationsController.cs:14 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/system/announcements/active` | — | — | — | — | — | OperationsController.cs:19 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/v1/system/maintenance` | — | — | — | — | — | V1MaintenanceController.cs:12 | BLOCKED: chưa hoàn tất đối chiếu |

## Admin: 47 route trong inventory

| Method | Route | Auth | Request DTO | Response DTO | Status | Business effect | Code evidence | Kết quả |
|---|---|---|---|---|---|---|---|---|
| POST | `/api/v1/admin/support/{caseId}/users/{userId}/profile` | — | — | — | — | — | AdminA2Controller.cs:17 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/v1/admin/support/{caseId}/users/{userId}/sessions` | OperationsRead; case-scoped | `A2Query` | `A2Page<A2Session>` | 200 | Đọc phiên trong hồ sơ | AdminA2Controller.cs:21 | CORRECTED |
| POST | `/api/v1/admin/support/{caseId}/users/{userId}/restrictions` | — | — | — | — | — | AdminA2Controller.cs:24 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/v1/admin/support/{caseId}/users/{userId}/groups` | — | — | — | — | — | AdminA2Controller.cs:38 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/v1/admin/support/{caseId}/users/{userId}/friends` | — | — | — | — | — | AdminA2Controller.cs:40 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/v1/admin/support/{caseId}/users/{userId}/history` | — | — | — | — | — | AdminA2Controller.cs:42 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/admin/dashboard` | OperationsRead | `query from, to` | `AdminDashboardDto` | 200 | Đọc dashboard | AdminA1Controller.cs:14 | CORRECTED |
| GET | `/api/v1/admin/overview` | OperationsRead | `query date` | `A1Summary` | 200 | Đọc snapshot tổng quan | AdminA1Controller.cs:18 | CORRECTED |
| GET | `/api/admin/reports` | OperationsRead | `query from, to` | `AdminDashboardDto` | 200 | Cùng action dashboard legacy | AdminA1Controller.cs:15 | CORRECTED |
| GET | `/api/v1/admin/overview/activity` | OperationsRead | `A1ListQuery (query)` | `A1Page<A1FeedItem>` | 200 | Đọc feed hoạt động | AdminA1Controller.cs:26 | CORRECTED |
| GET | `/api/v1/admin/overview/tasks` | OperationsRead | `A1ListQuery (query)` | `A1Page<A1Task>` | 200 | Đọc feed tác vụ | AdminA1Controller.cs:20 | CORRECTED |
| GET | `/api/v1/admin/overview/alerts` | OperationsRead | `A1ListQuery (query)` | `A1Page<A1FeedItem>` | 200 | Đọc feed cảnh báo | AdminA1Controller.cs:24 | CORRECTED |
| GET | `/api/admin/users` | — | — | — | — | — | AdminController.cs:45 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/v1/admin/users/search` | — | — | — | — | — | AdminA2Controller.cs:14 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/admin/users/{id}/revoke-sessions` | — | — | — | — | — | AdminController.cs:132 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/v1/admin/support/{caseId}/users/{userId}/revoke-sessions` | OperationsRead; case-scoped; PIN | A2RevokeRequest {reason,securityPin,sessionId?} | Không body | 204; 404 session_not_found | Thu hồi phiên chọn hoặc tất cả | AdminA2Controller.cs:30–33; AdminA2Service.cs:116–134 | CORRECTED |
| PUT | `/api/admin/users/{id}/status` | — | — | — | — | — | AdminController.cs:55 | BLOCKED: chưa hoàn tất đối chiếu |
| PUT | `/api/v1/admin/support/{caseId}/users/{userId}/status` | OperationsRead; case-scoped | `A2StatusRequest` | `Không body` | 204 | Đổi trạng thái qua ChangeStatusAsync | AdminA2Controller.cs:33 | CORRECTED |
| POST | `/api/admin/users/{id}/reset-password` | — | — | — | — | — | AdminController.cs:71 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/admin/users/{id}/reset-pin` | — | — | — | — | — | AdminController.cs:80 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/admin/ledger/search` | — | — | — | — | — | AdminController.cs:175 | BLOCKED: chưa hoàn tất đối chiếu |
| PUT | `/api/admin/wallets/{id}/freeze` | — | — | — | — | — | AdminController.cs:148 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/admin/transactions/search` | — | — | — | — | — | AdminController.cs:162 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/admin/bank-transactions` | — | — | — | — | — | AdminBankIntegrationController.cs:43 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/admin/audit-logs` | — | — | — | — | — | AdminController.cs:253 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/admin/configuration` | — | — | — | — | — | AdminController.cs:210 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/admin/feature-flags` | — | — | — | — | — | AdminController.cs:232 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/admin/cases/support` | — | — | — | — | — | Chưa ghép attribute; Admin.md:59 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/v1/admin/support/tickets/search` | — | — | — | — | — | AdminA5Controller.cs:14 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/v1/admin/support/{caseId}/assign` | — | — | — | — | — | AdminA5Controller.cs:19 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/admin/cases/support/{id}` | — | — | — | — | — | Chưa ghép attribute; Admin.md:60 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/admin/cases/support/{id}/messages` | — | — | — | — | — | Chưa ghép attribute; Admin.md:61 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/v1/admin/support/{caseId}/reply-template` | — | — | — | — | — | AdminA5Controller.cs:64 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/v1/admin/support/{caseId}/request-info` | — | — | — | — | — | AdminA5Controller.cs:32 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/v1/admin/support/{caseId}/dossier` | — | — | — | — | — | AdminA5Controller.cs:40 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/admin/cases/support/{id}/status` | — | — | — | — | — | Chưa ghép attribute; Admin.md:63 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/admin/cases/support/{id}/correction-grant` | — | — | — | — | — | Chưa ghép attribute; Admin.md:63 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/v1/system/broadcasts` | — | — | — | — | — | V1BroadcastController.cs:12 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/v1/system/broadcasts/{id}/publish` | — | — | — | — | — | V1BroadcastController.cs:14 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/v1/system/broadcasts/{id}/status` | — | — | — | — | — | V1BroadcastController.cs:18 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/admin/health` | — | — | — | — | — | AdminController.cs:260 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/admin/ai/chat` | — | — | — | — | — | AdminAiController.cs:40 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/admin/integrations/sepay/status` | — | — | — | — | — | AdminBankIntegrationController.cs:79 | BLOCKED: chưa hoàn tất đối chiếu |
| GET | `/api/admin/bank-integration/reconciliation` | — | — | — | — | — | AdminBankIntegrationController.cs:75 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/v1/admin/support/{caseId}/user-profile` | — | — | — | — | — | V1AdministrationController.cs:48 | BLOCKED: chưa hoàn tất đối chiếu |
| PUT | `/api/admin/configuration/{key}` | — | — | — | — | — | AdminController.cs:218 | BLOCKED: chưa hoàn tất đối chiếu |
| POST | `/api/v1/system/broadcasts/{id}/cancel` | — | — | — | — | — | V1BroadcastController.cs:16 | BLOCKED: chưa hoàn tất đối chiếu |

## Bằng chứng bổ sung

- Token: `backend/SpendWise/appsettings.json:9` (15 phút); `AuthenticationSupport.cs:59` và `RefreshTokenCommandHandler.cs:201` (30 ngày).
- Đây là tiến độ kiểm chứng source, không phải nghiệm thu HTTP. Full E2E không chạy.

### Batch User — đổi email

- Inventory gốc: 6/220 đã đối chiếu (5 VERIFIED, 1 CORRECTED); 214 dòng gốc còn lại. Thêm legacy confirm bị chặn vốn ghi tắt trong User.md: đã đối chiếu riêng, không tính vào mẫu số 220.
- Tests tham chiếu: `EmailChangeSecurityTests.cs` (bao gồm hủy bằng ticket ở dòng 432–433), `UserAvatarAndEmailChangeTests.cs`. Chỉ đọc source; không chạy lại.

## Kiểm tra bổ sung sau mốc 721f66b

- ADM-08: tách contract thu hồi phiên case-scoped khỏi legacy; tham chiếu AdminA2Service.cs:116–134. ADM-09: sửa tên ChangeUserStatusRequest ở bảng tổng hợp (AdminController.cs:58–60; AdminContracts.cs:11).
- Handoff: phân biệt securityPin với adminPin, sửa checklist Broadcast và mô tả reset credentials; phạm vi hủy OTP theo EmailVerificationSupport.cs:775–794.
- OpenAPI runtime tại SHA khóa chưa được lấy; Program.cs có đường khởi động DevelopmentSeeder. Chỉ dùng OpenAPI lịch sử và kiểm tra tĩnh. Chưa khóa các dòng chưa đối chiếu DTO, policy và tác động.

## Đối chiếu cuối bằng OpenAPI và endpoint metadata

Backend `53b6d6e24bf0612647fd8a61409623eb05187136`: TestServer Testing trả 200 tại `/openapi/v1.json`, 365 operations (339 baseline + 26). Toàn bộ method/route đầy đủ khớp: User 221/221, Admin 47/47. Không chạy lại E2E.

Ba sai lệch đã sửa: Login chấp nhận email, SpendId và số điện thoại (LoginCommandHandler.cs:32–36); Ledger và Transaction search nhận `SensitiveAccessRequest {reason}` (AdminContracts.cs:16, AdminController.cs:163–186), trả 200 với mảng tương ứng. Freeze riêng trả 204 (AdminController.cs:149–159).

`EndpointDataSource` của TestServer: ghép 221 User (203 có Authorize, 18 AllowAnonymous) và 47 Admin (47 có Authorize, 0 AllowAnonymous). Ledger/Transaction search mang policy `SystemAdmin`; Broadcast publish mang `V1.ManageMaintenance`. Đây là tên policy trong metadata, không phải tên role được phép tự suy diễn. Điều kiện PIN, case scope, quyền đối tượng và custom policy handler chưa được xác minh toàn diện bằng HTTP. OpenAPI không chứa security tường minh; không dùng sự vắng mặt này để suy ra quyền truy cập.

Status: dùng `NoContent()` cho email cancel (UsersController.cs:112), freeze (AdminController.cs:159), Broadcast publish/cancel (V1BroadcastController.cs:15–17); OpenAPI mặc định 200 cho các action này. Chưa xác minh mọi nhánh lỗi và exception mapping.

### Phân loại 18 cảnh báo payload lịch sử

Nguồn: comparison.json của TestServer; đối chiếu item và assertion trong ba collection tương ứng. HISTORICAL_TEST chỉ nói về trường không còn trong schema hiện tại, không suy ra request cũ nhất định thất bại. Required trong OpenAPI có thể gồm trường nullable; chỉ thiếu trường chưa đủ kết luận lỗi validation.

| Collection | Operation | Phân loại | Bằng chứng |
|---|---|---|---|
| User | PATCH friends/{id}/preference | HISTORICAL_TEST | Item 14 kỳ vọng 200 nhưng gửi alias; contract dùng nickname. |
| User | PATCH transactions/{id} | INCONCLUSIVE | Item 15/F03d kỳ vọng 200, thiếu categoryId; cần phân biệt nullable và validation. |
| User | POST bank-connections/link | HISTORICAL_TEST | B5-01 gửi redirectUrl; assertion chấp nhận fallback 503, không xác minh trường redirect. |
| User | POST bank-connections/{id}/reactivate | NEGATIVE_TEST | B5-04 chủ đích kiểm tra active connection trả 409, không xác minh payload happy path. |
| User | POST bank-connections/{id}/unlink | HISTORICAL_TEST | B5-03 kỳ vọng 200 với redirectUrl ngoài schema. |
| User | POST categories | HISTORICAL_TEST | B2-01 gửi type ngoài schema hiện tại. |
| User | POST goals | HISTORICAL_TEST | Item tạo goal gửi deadline ngoài schema hiện tại. |
| User | POST groups | INCONCLUSIVE | B7-02 thiếu description; assertion 200/201 không chứng minh required hiện tại. |
| User | POST groups/{id}/expenses | INCONCLUSIVE | B7-22 thiếu categoryId/description/paidByUserId; chưa xác minh binding/default. |
| User | POST integrations/sepay/bankhub/ipn | NEGATIVE_TEST | B2-06 kiểm tra secret và kỳ vọng 401; không phải mẫu IPN hợp lệ. |
| User | POST v1/funds/elections/{id}/ballots | INCONCLUSIVE | B6-13 kỳ vọng 200 nhưng thiếu support; cần kiểm tra quy tắc ballot. |
| User | PUT groups/{id} | HISTORICAL_TEST | B7-04 gửi currency ngoài schema và thiếu membersCanCreateExpenses. |
| User | PUT mobile/push/installations/{id} | INCONCLUSIVE | B1-05 thiếu deviceName; assertion 200/204, không chứng minh trường bắt buộc. |
| User | PUT v1/funds/{id}/members/{userId}/spending-permission | NEGATIVE_TEST | Item 08 kỳ vọng 403 do leo quyền cross-user; không dùng canSpend làm mẫu contract. |
| Comprehensive | POST v1/funds | INCONCLUSIVE | EP01/FF01a kỳ vọng 200 nhưng thiếu targetVnd; không dùng kết quả cũ suy ra quy tắc Fixed/Free hiện tại. |
| Comprehensive | POST v1/funds/elections/{id}/ballots | NEGATIVE_TEST | EP48 kỳ vọng 404 với ID không tồn tại, thiếu round/support. |
| Admin | POST admin/ai/chat | INCONCLUSIVE | ADM-05.2a kỳ vọng 200, thiếu conversationId; không chứng minh binding hoặc Gemini live. |
| Admin | POST cases/disputes | NEGATIVE_TEST | DEP-01 chủ đích kiểm tra legacy create bị chặn 400. |

Tổng: 5 NEGATIVE_TEST, 6 HISTORICAL_TEST, 0 DOC_MISMATCH phát sinh từ nhóm cảnh báo, 7 INCONCLUSIVE. Không sửa collection. Tài liệu có thể dùng để tích hợp theo các contract đã xác nhận; chưa tuyên bố khóa 100% schema/quyền/status.

## Nghiệm thu HTTP bổ sung tại backend LOCK

Build test từ source khóa, artifact tách khỏi output đang dùng. `Task17SqlHttpFixture` tạo catalog SQL02 mới theo mẫu `SpendWise_FinancialS05_Test_<guid>`, dùng dữ liệu tổng hợp, kiểm tra server và quyền sở hữu trước khi dọn. Không dùng SQL01, SpendWiseDb hay SpendWise_E2E_G4. TestServer dùng pipeline JWT và PIN thật; token cho actor được tạo bằng token service sau khi tạo fixture session, không phải bằng chứng login. Hosted services bị tắt; test Broadcast gọi dispatch thực tế chủ động. Email pickup, SePay stub, không Gemini/Push LIVE.

| Scenario | Test tại SHA khóa | Kết quả và giới hạn |
|---|---|---|
| A — Support | Task17SupportBroadcastSqlHttpTests.Support_HTTP_SQL02_intake_assignment_information_reply_and_terminal_decision (reject true/false) | 2 PASS; intake, phân công, bổ sung thông tin, trả lời, kết quả, cách ly hồ sơ; không ghi ledger. |
| F — Broadcast | Task17SupportBroadcastSqlHttpTests.Broadcast_HTTP_SQL02_publish_cancel_dispatch_outbox_and_permission_are_consistent | PASS; draft/status 200, publish/cancel 204, sai role/PIN 403; chạy ProcessDueAsync, kiểm tra notification, outbox, dedup và audit. Không chứng minh push live hoặc người dùng đọc. |
| G — Security | Cùng test Support/Broadcast | PARTIAL: có wrong role/PIN và truy cập hồ sơ người khác; không bao phủ toàn bộ negative cases. |
| Account / Fixed | Task17AccountSqlHttpTests.Restricted_member_receives_Fixed_or_Free_refund_once_via_HTTP_SQL02 (fixedFund true) | FAIL: test cũ kỳ vọng tự rời kỳ bình thường, source hiện tại trả 409 fixed_fund_voluntary_leave_disallowed. Không chứng minh refund. |
| Account / Free và P2P | Cùng test (fixedFund false); Restricted_P2P_recipient_receives_credit_with_balanced_SQL02_ledger | 2 FAIL ở setup: CoreAcceptanceData.cs:79 truyền contribution duration cho Free; V1FundService.cs:77 từ chối. Không có kết luận HTTP về refund/P2P. |

Còn thiếu bằng chứng SQL HTTP cho B (Admin thu hồi một/tất cả session), C (User transaction rồi Admin ledger/search), D (Admin đổi trạng thái và phiên), E (correction-grant tăng allowance, không ghi tiền/ledger/đóng hồ sơ). Support/Broadcast pass không thay thế các scenario này. Số 221/47 vẫn là phạm vi route đầy đủ đã ghép, không phải số đã nghiệm thu đủ schema/policy/status; chưa hoàn tất các tham chiếu viết tắt.

Bảy cảnh báo Postman vẫn chờ kiểm chứng HTTP: PATCH transactions thiếu categoryId (nullable); POST groups thiếu description (nullable); POST groups/{id}/expenses thiếu categoryId/description/paidByUserId (nullable); POST ballot thiếu support (V1ElectionVoting.cs:42–46 yêu cầu Yes/No ở confirmation, loại support ở vòng chọn ứng viên); PUT push installation thiếu deviceName (OptionalText, MobilePushContracts.cs:73); POST fund thiếu targetVnd (V1FundService.cs:65–77 yêu cầu cho Fixed, phải null cho Free); POST Admin AI chat thiếu conversationId (AiContracts.cs:8 nullable). Nullable không tự chứng minh cả binding lẫn mọi điều kiện nghiệp vụ.

Kết quả runner tại SHA khóa: **17 test cases, 10 PASS, 7 FAIL, 0 SKIP**. Đây là số test case xUnit, không phải số request HTTP. Nhóm `FinalUserAdminSqlHttpTests` có 7 PASS (Friends limit, Goals, Transaction correction, Budgets, Notifications, Wallet/internal transfer, Recurring), 4 FAIL. Hai biến thể refund khi rejoin và hai biến thể pending withdrawal sau rejoin cùng lỗi setup Free ở CoreAcceptanceData.cs:79; không chạy tới assertion HTTP nghiệp vụ. Nhóm Support/Broadcast/Account có 3 PASS, 3 FAIL như bảng trên. Build thành công; gate kiểm thử không đạt nên không commit/push. User.md và Admin.md chưa LOCK.

### Chạy lại sau khi sửa fixture và test Fixed

Fixture không truyền thời lượng góp cho Free. Test Fixed xác nhận 409, số dư không đổi và không có bút toán refund; nhánh Free vẫn yêu cầu hoàn tiền đúng một lần. Chạy lại cùng ba nhóm trên catalog SQL02 cách ly: **17 test, 14 PASS, 3 FAIL, 0 SKIP**. Còn ba lỗi tại `FinalUserAdminSqlHttpTests.cs`: dòng 35, Free leave sau withdrawal đã duyệt trả 500; dòng 63, cả hai biến thể có pending withdrawal trả 409 thay vì 200. Giữ nguyên assertion và production source. Chưa xác định nguyên nhân 500 hoặc phân loại 409 là lỗi contract hay kỳ vọng test cũ. Không commit/push khi gate chưa đạt.

### Gate sau khi tách kỳ vọng cũ khỏi hành vi hiện tại

- Chạy cùng 17 ca gốc, cộng 1 ca refund Fixed mới: **17 PASS / 1 FAIL / 0 SKIP** (riêng nhóm gốc **16/17 PASS**). Ca refund Fixed độc lập cũng PASS: đóng kỳ qua HTTP, thành viên Restricted nhận 500.000 VND đúng một lần; replay giữ nguyên số dư và ledger cân.
- Pending withdrawal: `V1FundRefunds.cs:185–192` cố ý trả 409 `member_pending_requests` khi thành viên còn request `AwaitingOwnerReview`. Hai test giờ xác nhận 409, hủy request qua HTTP rồi rời/tái gia nhập; seed lại hàng lịch sử trong catalog test để kiểm tra review trả 409, số dư và ledger không đổi. Cả hai biến thể PASS. User.md mô tả guard này.
- Free refund/rejoin không có lần rút trước và Restricted P2P đã PASS sau sửa fixture. **Lỗi còn tái hiện**: `FinalUserAdminSqlHttpTests.cs:35`, `POST /api/v1/funds/{id}/leave` sau withdrawal được duyệt, kỳ vọng 200, thực tế 500. Chưa bắt được exception gốc; không hạ assertion, không sửa production Backend LOCK.
- Test changes chỉ nằm local trong `CoreAcceptanceData.cs`, `Task17AccountSqlHttpTests.cs`, `FinalUserAdminSqlHttpTests.cs`. Không stage hoặc commit. Gate chưa đạt; chưa khóa User/Admin 100%.

- Cross-role B/D: `Task17AccountSqlHttpTests.Case_scoped_Admin_revokes_selected_and_all_sessions_and_lock_invalidates_session` **PASS** trên catalog SQL02 fixture. Case-scoped revoke chọn một phiên (204, phiên được chọn trả 401, phiên còn lại vẫn 200), thu hồi tất cả (204, token còn lại trả 401), và lock (204, token mới bị từ chối 401; DB xác nhận Locked và không còn phiên active). Test dùng JWT thật từ token service trên session fixture; không xác minh login/refresh/recovery hoặc việc thu hồi refresh token riêng.

- Nguyên nhân HTTP 500 đã xác định: seed dùng SystemClock trong khi HTTP dùng đồng hồ cố định sớm hơn. Equity replay thấy withdrawal trước contribution và ném InvalidOperationException tại `V1FreeFundEquityCalculator.cs:262`. Fixture dùng chung IClock, tách timestamp các lượt ghi tiền và giữ rời/tái gia nhập cùng timestamp. Nhánh có prior withdrawal đã PASS trên SQL02; không sửa calculator hay thứ tự ghi ledger production.
- Cross-role C/E: `DocsAcceptanceCrossRoleSqlHttpTests.Manual_transaction_Admin_search_and_correction_grant_preserve_money_and_open_case` PASS. User tạo income với chứng từ test; User bị chặn ledger search 403; Admin nhận hai mảng 200, thấy giao dịch và ledger cân. Correction grant tăng allowance đúng 1, giữ số dư/số dòng ledger/số giao dịch tài chính/trạng thái case. Grant lặp trả 409 và giữ snapshot.
- So sánh lại OpenAPI khóa: 365 operations; User **222/222** route đầy đủ (đã mở rộng thêm `POST /api/recurring-transactions/{id}/resume`), Admin **47/47**. 21 khối JSON fenced trong User/Admin/handoff đều parse được. Kết quả không chứng minh mọi tham chiếu tắt và mọi schema/error branch đã LOCK.

- Kết quả cuối: **17/17 test gốc PASS trong cùng lượt chạy; 4/4 test bổ sung PASS** tính theo kết quả cuối của từng test. Lượt gộp đạt 20/21; Auth ban đầu so expiry với IClock trong khi JwtTokenService dùng TimeProvider. Test Auth chạy riêng sau khi sửa assertion: 1/1 PASS. Xác nhận registration, OTP fixture qua handler, PIN setup/verify, login bằng ba định danh, rotation, JWT 15 phút từ claims, refresh expiry 30 ngày trong DB và logout vô hiệu hóa access token. Chưa kiểm thử chờ JWT hết hạn hoặc recovery/đổi email qua SQL02 trong lượt này.
- Hai test mới `DocsAcceptanceCrossRoleSqlHttpTests.cs` và `DocsAcceptanceAuthSqlHttpTests.cs` giữ local, không stage. Logging chẩn đoán tạm trong factory đã được hoàn nguyên. Lượt chạy song song có lỗi môi trường Query Store khi cấp catalog; không sửa SQL instance, chỉ dọn artifact tạm do probe tạo và chạy tuần tự.
- Chưa LOCK 100%: còn validation HTTP cho bảy payload Postman nêu trên; nhánh lỗi/DTO/policy chưa đối chiếu đủ cho từng operation, tham chiếu viết tắt chưa kiểm kê hết, Banking Sandbox, recovery/đổi email và các nhánh nghiệp vụ quỹ ngoài bộ đã chạy chưa có nghiệm thu mới. Không commit/push khi gate nghiệm thu tài liệu chưa hoàn tất.

### Claim coverage — phạm vi bổ sung

Bảng dưới đây chỉ theo dõi các claim được xử lý trong lượt bổ sung, không phải mẫu số cuối của toàn bộ User/Admin.

| Claim | Doc / nhóm | Method / route | Loại | Kỳ vọng / thực tế | Source / test | Status |
|---|---|---|---|---|---|---|
| A20-chat-schema | Admin.md:65, ADM-20 | POST /api/admin/ai/chat | SCHEMA | Đã sửa tên DTO: AiChatRequest → AiChatResponse. | AdminAiController.cs:41; AiContracts.cs:8 | CORRECTED |
| A20-chat-no-id | Admin.md:65, ADM-20 | POST /api/admin/ai/chat | SCHEMA | Bỏ conversationId trả 200, tạo hội thoại SQL; provider=SpendWisePolicy, không gọi Gemini. | AdminAiFeature.cs:143–160; DocsAcceptanceRemainingSqlHttpTests.Admin_AI_omitted_conversation_id_creates_conversation_without_provider PASS | VERIFIED |
| U03-category-omitted | User.md:201, Transactions | PATCH /api/transactions/{id} | SCHEMA | Bỏ categoryId, trả 200; giữ category và số tiền, cập nhật mô tả. | UpdateTransactionCommandHandler.cs:59–65; DocsAcceptanceRemainingSqlHttpTests.Patch_transaction_without_category_retains_category PASS | VERIFIED |
| U06-group-description | User.md:274, Groups legacy | POST /api/groups | SCHEMA | Bỏ description vẫn trả 200 với payload User hợp lệ. | GroupContracts.cs:5; GroupsController.cs:35; DocsAcceptanceRemainingSqlHttpTests.Legacy_group_without_description_and_Free_fund_without_target_bind PASS | VERIFIED |
| U06-expense-fields | User.md:365, Groups legacy | POST /api/groups/{id}/expenses | SCHEMA | paidByType=user, gửi category dạng text: bỏ cả 3 trường trả 200, người trả mặc định là actor, không ghi ledger. Không suy rộng sang paidByType=fund. | CurrentGroupFundFeature.cs:454–509; DocsAcceptanceRemainingSqlHttpTests.Personal_group_expense_omitted_fields_bind_and_default_to_actor PASS | VERIFIED |
| U06-ballot-support | User.md:407, Fixed | POST /api/v1/funds/elections/{id}/ballots | SCHEMA | Omission vòng 1 trả 200 và lưu null; vòng 3 trả 400 invalid_ballot, gửi support=true thì 200. | V1ElectionVoting.cs:37–62; DocsAcceptanceRemainingSqlHttpTests.Ballot_support_omission_depends_on_round (2 PASS) | VERIFIED |
| U06-fund-target | User.md:375, Fixed/Free | POST /api/v1/funds | SCHEMA | Free bỏ targetVnd trả 200 và lưu null; Fixed bỏ targetVnd trả 400 invalid_fund_configuration, không tạo thêm quỹ. | V1FundService.cs:65–77; DocsAcceptanceRemainingSqlHttpTests.Legacy_group_without_description_and_Free_fund_without_target_bind PASS | VERIFIED |
| U01-recovery | User.md:133–141, Auth | POST /api/auth/forgot-password và routes recovery | BUSINESS | Chưa có SQL02 HTTP evidence hiện tại về vòng đời token và tác động phiên. | AuthController; recovery handlers | UNVERIFIED |
| U01-expired-jwt | User.md:145, Auth | GET /api/users/me | AUTH | JWT ký đúng nhưng quá hạn trả 401; token còn hạn của cùng session vẫn trả 200. | Program.cs:119–128; DocsAcceptanceRemainingSqlHttpTests.Expired_signed_JWT_is_rejected_while_session_remains_valid_and_recovery_is_neutral PASS | VERIFIED |
| U05-settlement | User.md:261–268, Banking | Test Mode top-ups/withdrawals | BUSINESS | Chưa chạy SQL02 mới với provider stub cho reserve, settlement và hoàn reserve. | BankConnectionsController; historical SePayTestModeBankingTests | UNVERIFIED |
| U10-push-no-name | User.md:180, Push | PUT /api/mobile/push/installations/{id} | SCHEMA / AUTH / STATUS | Không gửi deviceName: anonymous 401, User 204 body rỗng; DELETE 204. Không gửi push. | MobilePushController.cs:11–36; MobilePushContracts.cs:73; DocsAcceptanceRemainingSqlHttpTests.Push_registration_omits_device_name_and_requires_user PASS | VERIFIED |
| U01-email-confirm | User.md:158–161, Profile | POST /api/users/me/email/current/request, current/verify, new/request, new/confirm | BUSINESS / STATUS | Bốn action trả 200; OTP test tạo ticket, OTP mới cập nhật địa chỉ trong DB. | UsersController.cs:67–104; DocsAcceptanceRemainingSqlHttpTests.Email_change_cancel_or_confirmation_uses_two_otp_and_blocks_legacy(false) PASS | VERIFIED |
| U01-email-cancel | User.md:162, Profile | POST /api/users/me/email/cancel | BUSINESS / STATUS | Ticket khác không giới hạn hủy; 204, ticket cũ không confirm được (400), email cũ giữ nguyên và PendingNewEmail null. | EmailVerificationSupport.cs:775–794; cùng test (true) PASS | VERIFIED |
| U01-email-legacy | User.md:163, Profile | POST /api/users/me/email/request và /confirm | STATUS | Cả hai trả 400 legacy_email_change_deprecated. | UsersController.cs:115–134; cả hai biến thể test PASS | VERIFIED |

Đã đóng 7/7 warnings qua SQL02 HTTP, theo phạm vi payload ghi trong bảng. Không sửa collection và không dùng kết quả nhánh policy nội bộ làm bằng chứng Gemini LIVE.

Hai biến thể đổi email chạy trên hai catalog SQL02 riêng, dùng OTP sink. Ca gộp trước đó chạm 429 rate limit; hai ca tách giữ nguyên limiter và PASS. Chưa dùng kết quả này để xác nhận SMTP delivery hoặc toàn bộ kiểm tra session chéo của đổi email.

- Recovery: cùng test xác nhận forgot-password trả 202 và body giống nhau cho email có và không có tài khoản. Chưa kiểm tra reset password/PIN và thu hồi phiên sau reset.
- Banking: `DocsAcceptanceRemainingSqlHttpTests.Banking_Test_Mode_is_closed_in_Testing_without_posting_money` PASS; GET danh sách top-up và withdrawal trả 404 sepay_test_mode_unavailable, không có ledger. Đây là kiểm thử guard, không phải Sandbox settlement. Hai handler dùng EnsureDevelopment; cần harness riêng thay adapter TestMode bằng fixture để chạy nạp/rút mà không kết nối provider.
- Không chạy lại các ca đã PASS. 7/7 cảnh báo payload đã đóng, nhưng chưa nghiệm thu hết recovery và Banking Sandbox, cũng chưa chốt các claim UNVERIFIED của toàn bộ User/Admin. Không stage, commit hoặc push.

Đồng bộ cuối: User/Admin và handoff phân biệt khóa source, bằng chứng lịch sử và kiểm chứng hiện tại. Sửa ranh giới PIN theo DTO, legacy 401/403, và tác động thu hồi phiên của reset password (ResetPasswordCommandHandler.cs:125). Không chạy thêm test; không nâng các dòng UNVERIFIED.

Đối chiếu source bổ sung: AuthController.cs:120–133,203–216 xác nhận logout 204 và PIN forgot 202. AdminController.cs:207–258 và AdminContracts.cs:24–26 xác nhận ADM-13 có response riêng cho audit, configuration và flags. Không chạy thêm test và không nâng trạng thái nghiệm thu toàn tài liệu.
