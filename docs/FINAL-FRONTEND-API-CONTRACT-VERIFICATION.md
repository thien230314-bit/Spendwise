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
