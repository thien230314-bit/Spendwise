# SPENDWISE V1.0 — USER BACKEND SUBMISSION HANDOFF & CONTRACT (TASK 11 FINAL)

**Date:** 2026-10-19  
**Version:** V1.0 Submission Ready  
**Baseline Commit:** `0015b6efe1a1a0f571b6147b3c70c98ff263c76a`  
**Quality Bar:** 1049/1049 PASS (0 FAIL, 0 SKIP across full isolated regression suite).

---

## 1. Hệ thống & Phân vai V1.0

Hệ thống SpendWise V1.0 có đúng 2 vai trò hoạt động:
1. **User (Mobile App / Expo React Native)**: Toàn bộ nghiệp vụ cá nhân, ví điện tử, quản lý giao dịch, chuyển tiền P2P, kết bạn, quỹ nhóm (Fixed & Free) và tạo khiếu nại/yêu cầu hỗ trợ theo `User.md` v1.3.
2. **Admin (Web Admin / SPA)**: Duy nhất **1 tài khoản Admin** trên toàn hệ thống quản trị 5 menu tinh gọn (ADM-01 Tổng quan, ADM-02 Người dùng, ADM-03 Khiếu nại & Yêu cầu, ADM-04 Thông báo, ADM-05 Bảo trì).

> [!IMPORTANT]
> **SuperAdmin hoàn toàn bị bãi bỏ khỏi phạm vi V1.0** theo quyết định khẩn cấp của Product Owner. Không còn Web SuperAdmin, không còn quy trình thăng cấp/chuyển cấp SuperAdmin. Chức năng bảo trì hệ thống được quản lý tập trung bởi Admin với xác thực Security PIN.

---

## 2. Bảng Tra Cứu API Thực Tế Cho Frontend & Mobile (Canonical Routes & Contracts)

Tất cả endpoint đều được ánh xạ trực tiếp từ Controllers thực tế của Backend, không sử dụng đường dẫn giả định.

### 2.1. Xác thực & Tài khoản (`/api/auth`)
| Feature | Method | Endpoint | Request Body / Params | Response | Auth / PIN | Trạng thái |
|---|---|---|---|---|---|---|
| Register | POST | `/api/auth/register` | `RegisterRequest`: `FullName`, `Email`, `Password`, `ConfirmPassword` | `EmailVerificationPendingResponse` | Không | READY |
| Verify OTP Email | POST | `/api/auth/email-verification/verify` | `VerifyRegistrationEmailRequest`: `Email`, `VerificationCode` | `LoginChallengeResponse` | Không | READY |
| Resend OTP Email | POST | `/api/auth/email-verification/resend` | `ResendRegistrationEmailRequest`: `Email` | `EmailVerificationPendingResponse` | Không | READY |
| Login | POST | `/api/auth/login` | `LoginRequest`: `Email`, `Password` | `LoginChallengeResponse` (Cần verify PIN hoặc setup PIN) | Không | READY |
| Setup PIN | POST | `/api/auth/pin/setup` | `SetupSecurityPinRequest`: `PinSetupToken`, `SecurityPin` | `AuthResponse`: `AccessToken`, `RefreshToken`, `ExpiresIn` | Challenge Token | READY |
| Verify PIN for Login | POST | `/api/auth/pin/verify` | `CompleteSecurityPinLoginRequest`: `LoginChallengeToken`, `SecurityPin` | `AuthResponse`: `AccessToken`, `RefreshToken`, `ExpiresIn` | Challenge Token | READY |
| Refresh Token | POST | `/api/auth/refresh` | `RefreshRequest`: `RefreshToken` | `AuthResponse`: `AccessToken`, `RefreshToken`, `ExpiresIn` | Không | READY |
| Logout | POST | `/api/auth/logout` | `RefreshRequest?`: `RefreshToken` | `204 NoContent` | Bearer Token | READY |
| Danh sách Sessions | GET | `/api/auth/sessions` | Không | `IReadOnlyList<SessionDto>` | Bearer Token | READY |
| Thu hồi Session | DELETE | `/api/auth/sessions/{sessionId}` | Route `sessionId` | `204 NoContent` | Bearer Token | READY |
| Đổi mật khẩu | POST | `/api/auth/change-password` | `ChangePasswordRequest`: `CurrentPassword`, `NewPassword` | `204 NoContent` | Bearer Token | READY |
| Quên mật khẩu | POST | `/api/auth/forgot-password` | `ForgotPasswordRequest`: `Email` | `202 Accepted` | Không | READY |
| Đặt lại mật khẩu | POST | `/api/auth/reset-password` | `ResetPasswordRequest`: `Email`, `Token`, `NewPassword` | `204 NoContent` | Không | READY |
| Quên mã PIN | POST | `/api/auth/pin/forgot` | `ForgotSecurityPinRequest`: `Email` | `202 Accepted` | Không | READY |
| Validate Token Reset PIN | POST | `/api/auth/pin/reset/validate` | `ValidateSecurityPinResetTokenRequest`: `Email`, `Token` | `SecurityPinResetTokenStatusDto` | Không | READY |
| Đặt lại mã PIN | POST | `/api/auth/pin/reset` | `ResetSecurityPinRequest`: `Email`, `Token`, `NewSecurityPin` | `204 NoContent` | Không | READY |

### 2.2. Ví Điện Tử & Chuyển Tiền (`/api/wallets` & `/api/transfers`)
| Feature | Method | Endpoint | Request Body / Params | Response | Auth / PIN | Trạng thái |
|---|---|---|---|---|---|---|
| Danh sách ví | GET | `/api/wallets` | Không | `IReadOnlyList<WalletDto>` (1 Main, max 2 phụ) | Bearer Token | READY |
| Chi tiết ví | GET | `/api/wallets/{walletId}` | Route `walletId` | `WalletDto`: `AvailableBalance`, `Type`, `Status` | Bearer Token | READY |
| Mở ví phụ | POST | `/api/wallets` | `CreateWalletRequest`: `Name`, `Type` (`Savings`/`Emergency`) | `WalletDto` (201 Created) | Bearer Token | READY |
| Cập nhật ví | PUT | `/api/wallets/{walletId}` | `UpdateWalletRequest`: `Name`, `Type` | `WalletDto` | Bearer Token | READY |
| Đóng ví phụ | DELETE | `/api/wallets/{walletId}` | Route `walletId` | `204 NoContent` | Bearer Token | READY |
| Chuyển tiền nội bộ | POST | `/api/transfers/internal` | `InternalTransferRequest`: `SourceWalletId`, `DestinationWalletId`, `Amount`, `Description`, `IdempotencyKey`, `SecurityPin` | `TransferDto` (201 Created) | Bearer + PIN | READY |
| Chuyển tiền P2P | POST | `/api/transfers/p2p` | `P2PTransferRequest`: `SourceWalletId`, `RecipientUserId`, `Amount`, `Description`, `IdempotencyKey`, `SecurityPin` | `TransferDto` (201 Created, sinh bút toán kép Ledger) | Bearer + PIN | READY |

### 2.3. Bạn Bè (`/api/friends`)
| Feature | Method | Endpoint | Request Body / Params | Response | Auth / PIN | Trạng thái |
|---|---|---|---|---|---|---|
| Tìm kiếm bạn bè | GET | `/api/friends/search` | Query `contact` (`SpendId` hoặc Email/Phone), `kind?` | `FriendContactDto` | Bearer Token | READY |
| Danh sách bạn bè | GET | `/api/friends` | Không | `IReadOnlyList<FriendDto>` (giới hạn tối đa 100) | Bearer Token | READY |
| Lời mời đến | GET | `/api/friends/requests/incoming` | Không | `IReadOnlyList<FriendRequestDto>` | Bearer Token | READY |
| Lời mời đi | GET | `/api/friends/requests/outgoing` | Không | `IReadOnlyList<FriendRequestDto>` | Bearer Token | READY |
| Gửi lời mời kết bạn | POST | `/api/friends/requests` | `SendFriendRequest`: `RecipientUserId`, `Note` | `FriendRequestDto` | Bearer Token | READY |
| Hủy lời mời đã gửi | DELETE | `/api/friends/requests/{requestId}` | Route `requestId` | `FriendRequestDto` | Bearer Token | READY |
| Chấp nhận kết bạn | POST | `/api/friends/requests/{requestId}/accept` | Route `requestId` | `FriendDto` (quan hệ 2 chiều) | Bearer Token | READY |
| Từ chối kết bạn | POST | `/api/friends/requests/{requestId}/reject` | Route `requestId` | `FriendRequestDto` | Bearer Token | READY |
| Hủy kết bạn | DELETE | `/api/friends/{friendUserId}` | Route `friendUserId` | `204 NoContent` | Bearer Token | READY |
| Cập nhật ưu tiên/biệt danh | PATCH | `/api/friends/{friendUserId}/preference` | `UpdateFriendPreferenceRequest`: `Alias`, `IsFavorite` | `FriendDto` | Bearer Token | READY |

### 2.4. Quỹ Nhóm Fixed & Free (`/api/v1/funds`)
| Feature | Method | Endpoint | Request Body / Params | Response | Auth / PIN | Trạng thái |
|---|---|---|---|---|---|---|
| Danh sách quỹ | GET | `/api/v1/funds` | Không | `IReadOnlyList<V1FundDto>` | Bearer Token | READY |
| Tạo quỹ mới | POST | `/api/v1/funds` | `CreateV1FundRequest`: `Name`, `Kind` (`Fixed`/`Free`), `TargetVnd`, `SecurityPin` | `V1FundDto` | Bearer + PIN | READY |
| Chi tiết quỹ | GET | `/api/v1/funds/{id}` | Route `id` | `V1FundDto` (Mã SPW, Số dư, Chủ nhóm, Trạng thái) | Bearer Token | READY |
| Danh sách thành viên | GET | `/api/v1/funds/{id}/members` | Route `id` | `IReadOnlyList<V1MemberDto>` (`CanSpend`, `JoinedAt`) | Bearer Token | READY |
| Mời thành viên | POST | `/api/v1/funds/{id}/invitations` | `V1InviteRequest`: `UserId` | `V1InvitationDto` | Bearer Token | READY |
| Nhận lời mời | POST | `/api/v1/funds/invitations/{id}/accept` | Route `id` | `V1FundDto` | Bearer Token | READY |
| Từ chối lời mời | POST | `/api/v1/funds/invitations/{id}/decline` | Route `id` | `V1InvitationDto` | Bearer Token | READY |
| Xác nhận phân bổ T/n (Fixed) | POST | `/api/v1/funds/{id}/obligations/confirm` | `V1ConfirmObligationsRequest`: `SecurityPin`, `Reason` | `V1FinanceDto` | Bearer + PIN | READY |
| Góp tiền vào quỹ | POST | `/api/v1/funds/{id}/contributions` | `V1ContributeRequest`: `AmountVnd`, `SecurityPin`, `IdempotencyKey` | `V1ContributionDto` (Ví giảm, Quỹ tăng) | Bearer + PIN | READY |
| Lịch sử góp tiền | GET | `/api/v1/funds/{id}/contributions` | `page=1`, `pageSize=20` | `IReadOnlyList<V1ContributionDto>` | Bearer Token | READY |
| Lịch sử hoạt động nhóm | GET | `/api/v1/funds/{id}/activities` | `page=1`, `pageSize=20` | `IReadOnlyList<V1ActivityDto>` | Bearer Token | READY |
| Bật/tắt CanSpend (Fixed) | PUT | `/api/v1/funds/{id}/members/{userId}/spending-permission` | `V1PermissionRequest`: `Enabled` (true/false), `SecurityPin` | `204 NoContent` | Owner + PIN | READY |
| Tạo yêu cầu chi/rút | POST | `/api/v1/funds/{id}/requests` | `V1FundRequestInput`: `Kind`, `AmountVnd`, `RecipientUserId`, `Purpose`, `IdempotencyKey`, `SecurityPin` | `V1FundRequestDto` | Member + PIN | READY |
| Duyệt yêu cầu chi/rút | POST | `/api/v1/funds/requests/{id}/review` | `V1RequestReview`: `Approved`, `Reason`, `SecurityPin` | `V1FundRequestDto` | Owner + PIN | POLICY_BLOCKED |

> [!WARNING]
> **Hiện trạng CanSpend & Thực hiện chi tiền:**
> - Chủ nhóm Fixed có thể bật `CanSpend = ON` cho thành viên và ghi nhận audit event thành công (`READY`).
> - Khi thành viên hoặc chủ nhóm tạo yêu cầu chi/rút qua `/api/v1/funds/{id}/requests`, hệ thống tiếp nhận và thẩm tra quyền chi tiêu hợp lệ (`READY`).
> - Tuy nhiên, nghiệp vụ **thực hiện chuyển tiền ra ngoài tài khoản quỹ / payout** hiện đang ở trạng thái `ApprovedAwaitingFinanceContract` và trả mã `FINANCE_BLOCKED` theo thiết kế an toàn của `User.md` (mục 12 - Quyết định tài chính về đối tác thanh toán và công thức quyền lợi ròng khi đã phát sinh chi). Hệ thống **tuyệt đối không trừ tiền giả lập** khi chưa có hợp đồng tài chính đích.

### 2.5. Khiếu Nại & Hỗ Trợ (`/api/cases`)
| Feature | Method | Endpoint | Request Body / Params | Response | Auth / PIN | Trạng thái |
|---|---|---|---|---|---|---|
| Tạo yêu cầu hỗ trợ | POST | `/api/cases/support` | `CreateSupportCaseRequest`: `Category`, `Priority`, `Subject`, `Description` | `CaseDto` | Bearer Token | READY |
| Danh sách hỗ trợ của tôi | GET | `/api/cases/support` | Không | `IReadOnlyList<SupportCaseDetailDto>` | Bearer Token | READY |
| Chi tiết yêu cầu hỗ trợ | GET | `/api/cases/support/{id}` | Route `id` | `SupportCaseDetailDto` (Lịch sử trao đổi, phản hồi) | Bearer Token | READY |
| Phản hồi / bổ sung chứng từ | POST | `/api/cases/support/{id}/messages` | `CaseMessageRequest`: `Message`, `Attachments` | `SupportCaseDetailDto` | Bearer Token | READY |
| Tạo khiếu nại giao dịch | POST | `/api/cases/disputes` | `CreateDisputeRequest`: `FinancialTransactionId`, `Reason`, `Description` | `CaseDto` | Bearer Token | READY |
| Danh sách khiếu nại | GET | `/api/cases/disputes` | Không | `IReadOnlyList<DisputeDetailDto>` | Bearer Token | READY |
| Chi tiết khiếu nại | GET | `/api/cases/disputes/{id}` | Route `id` | `DisputeDetailDto` | Bearer Token | READY |
| Phản hồi khiếu nại | POST | `/api/cases/disputes/{id}/messages` | `CaseMessageRequest`: `Message`, `Attachments` | `DisputeDetailDto` | Bearer Token | READY |

---

## 3. Ba Tích Hợp Bắt Buộc (Integrations Status)

### 3.1. Email Outbox & Dispatcher
- **Trạng thái:** **READY (LIVE NOT VERIFIED)**.
- **Cơ chế:** Lưu thông điệp email vào bảng `EmailOutboxMessages` đảm bảo tính tin cậy transactional, sau đó background worker `EmailOutboxDispatcher` gửi qua SMTP provider. Các template OTP, Quên mật khẩu, Tiếp nhận hỗ trợ hoạt động chuẩn xác qua `EmailTemplateRenderer`. Môi trường thử nghiệm chưa cấu hình SMTP thực của Product Owner nên chưa gửi email live ra ngoài.

### 3.2. Gemini AI Completion Provider
- **Trạng thái:** **READY (LIVE NOT VERIFIED)**.
- **Cơ chế:** Phục vụ trợ lý tài chính thông minh, phân loại chi tiêu và gợi ý ngân sách. Có provider fallback tự động khi không có API key/Internet, đảm bảo an toàn 100% không làm crash API. Cần cung cấp API key trong môi trường để kích hoạt live AI.

### 3.3. SePay BankHub
- **Trạng thái:** **READY (TestMode VERIFIED)**.
- **Cơ chế:** Tích hợp nạp tiền qua Bank QR / Webhook hoạt động trong chế độ `TestMode`, xác thực webhook signature, đối soát tham chiếu chống replay và tự động ghi có vào Ví Chính theo sổ cái.

---

## 4. Kịch Bản Demo Đã Được Kiểm Thử Thực Tế

### Kịch bản 1: Chuyển tiền P2P Giữa Hai Tài Khoản (`P2PTransferTwoUserIntegrationTests`)
1. **User A (`UserA_Sender`)** sở hữu Ví Chính có số dư ban đầu `500,000 VND`.
2. **User B (`UserB_Recipient`)** sở hữu Ví Chính có số dư ban đầu `50,000 VND`.
3. **User A** gọi `POST /api/transfers/p2p` chuyển `200,000 VND` cho **User B** kèm PIN `123456` và IdempotencyKey.
4. **Kết quả:**
   - Ví A giảm chính xác về `300,000 VND`.
   - Ví B tăng chính xác lên `250,000 VND`.
   - Sổ cái `LedgerTransactions` sinh 2 bút toán: Có (Credit) ví A `200,000 VND` và Nợ (Debit) ví B `200,000 VND`, cân bằng tuyệt đối.
   - Ghi nhận `TransferOut` cho User A và `TransferIn` cho User B.
   - Gửi lại cùng IdempotencyKey trả về `Replayed = true`, không trừ tiền lần hai.
   - Thử nghiệm với PIN sai hoặc số dư không đủ bị chặn bằng `pin_verification_failed` (401) hoặc `insufficient_balance` (409).

### Kịch bản 2: Quỹ Nhóm Fixed & Cấu Hình Quyền Chi CanSpend
1. **User A** tạo quỹ cố định (Fixed) với mục tiêu 2 thành viên x `500,000 VND`.
2. **User A** mời **User B** tham gia qua `/api/v1/funds/{id}/invitations`. User B chấp nhận.
3. Cả hai thực hiện nộp tiền qua `/api/v1/funds/{id}/contributions`. Tiền được chuyển từ ví cá nhân sang quỹ nhóm.
4. **User A** (Chủ nhóm) bật `CanSpend = ON` cho **User B** qua `PUT /api/v1/funds/{id}/members/{userId}/spending-permission`.
5. Sự kiện bật quyền chi tiêu được ghi nhận vào `Lịch sử hoạt động nhóm` (`/api/v1/funds/{id}/activities`).

### Kịch bản 3: Hỗ Trợ & Khiếu Nại (User ↔ Admin)
1. **User** tạo ticket hỗ trợ qua `POST /api/cases/support`.
2. **Admin** tra cứu ticket tại `POST /api/v1/admin/support/tickets/search`, gán xử lý qua `POST /api/v1/admin/support/{id}/assign`.
3. **Admin** yêu cầu bổ sung thông tin qua `POST /api/v1/admin/support/{id}/request-info`. Trạng thái chuyển sang `WaitingForUser`.
4. **User** phản hồi và đính kèm thông tin qua `POST /api/cases/support/{id}/messages`. Trạng thái tự động quay lại `Open`.
5. **Admin** hoàn tất giải quyết qua `POST /api/v1/admin/support/{id}/resolve`.

---

## 5. Kết Quả Kiểm Thử Nghiệm Thu (Safety Gates)

- **Release Build:** `dotnet build backend/SpendWise.slnx -c Release` -> **0 Error(s), 16 Dependency Warning(s)**.
- **P2P Transfer 2-User Integration Test:** `P2PTransferTwoUserIntegrationTests` -> **PASS (1/1)**.
- **Admin Focused Suites:** `FullyQualifiedName~AdminA` -> **129 PASS / 0 FAIL / 0 SKIP**.
- **User Core Suites (Auth, Wallet, Transfer, Friends, Group, Dispute):** -> **184 PASS / 0 FAIL / 0 SKIP**.
- **Integrations Suite (Email, Gemini, SePay):** -> **187 PASS / 0 FAIL / 0 SKIP**.
- **Full Non-SQL Regression Suite:** `FullyQualifiedName!~SqlServer` -> **1049 PASS / 0 FAIL / 0 SKIP**.
- **SQL02 Concurrency Suite:** `NOT RUN` (bảo vệ tài nguyên RAM/instance theo quy định).
- **Handoff Quality:** Toàn bộ API path đã được đồng bộ chuẩn xác với controller production code (`/api/auth`, `/api/wallets`, `/api/transfers`, `/api/friends`, `/api/v1/funds`, `/api/cases`).
