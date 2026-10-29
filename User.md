# SPENDWISE V1.0 — USER MOBILE SPECIFICATION & BACKEND API CONTRACT

> **Thời điểm xác nhận nghiệm thu:** 29/10/2026 — 12:26:00+07:00  
> **Trạng thái cốt lõi:** `BACKEND USER V1.0 — LOCKED & VERIFIED 100% PASS (306/306 REQUESTS, 343/343 ASSERTIONS, 0 FAIL)`  
> **Môi trường nghiệm thu:** ASP.NET Core 10 Release Kestrel (`http://127.0.0.1:5158`) kết nối Microsoft SQL Server `DESKTOP-B5KA3LT\SQLEXPRESS02`, Database `SpendWise_E2E_G4` (SQL02 cách ly tuyệt đối, không kết nối SQL01).  
> **Báo cáo kiểm thử chính thức:** [`reports/newman/backend-user-final-verified-20261029.json`](./reports/newman/backend-user-final-verified-20261029.json) và [`reports/newman/backend-user-final-verified.json`](./reports/newman/backend-user-final-verified.json).  
> **Ranh giới công nghệ:**
> - **Backend Core:** Đã kiểm thử tự động toàn diện qua Postman/Newman và xUnit; bảo đảm toàn vẹn sổ cái kế toán kép, phân quyền dữ liệu cá nhân, giới hạn ví, quy tắc quỹ nhóm Fixed V2/Free và chu trình Hỗ trợ/Khiếu nại trực tiếp.
> - **Frontend-Only / Pending UI E2E:** Các yếu tố trực quan (Giao diện Sáng/Tối/Hệ thống, Ngôn ngữ Tiếng Việt/Tiếng Anh trên UI, Hoạt ảnh chuyển động, Layout responsive và cảm biến sinh trắc học phần cứng native Face ID/Vân tay) thuộc phạm vi triển khai của ứng dụng Mobile/Frontend. Backend đã kiểm thử thành công các API lưu/đọc tùy chọn giao diện (`PUT /api/users/me/preferences`) và xác thực mật mã WebAuthn Level 2.

---

## 1. TỔNG QUAN HỆ THỐNG & ĐẶC TẢ SẢN PHẨM USER MOBILE

### 1.1. Tầm nhìn sản phẩm
SpendWise là ứng dụng di động hỗ trợ quản lý tài chính cá nhân thông minh và quản lý quỹ nhóm minh bạch, tin cậy:
1. **Quản lý tài chính cá nhân:** Theo dõi dòng tiền thu/chi tức thời, phân loại danh mục, lập ngân sách chi tiêu, lập mục tiêu tích lũy và giao dịch định kỳ với độ toàn vẹn sổ cái kế toán kép (Double-Entry Ledger).
2. **Quản lý quỹ nhóm minh bạch:**
   - **Quỹ Cố định (Fixed Fund V2):** Mô hình góp quỹ theo nghĩa vụ bằng nhau hoặc tùy biến, quy tắc chi tiêu động nghiêm ngặt không giữ chỗ giả tạo (đã loại bỏ ngưỡng 20% và trạng thái tạm giữ Pending24h).
   - **Quỹ Tự do (Free Fund):** Mô hình quỹ linh hoạt theo phong cách Quỹ Nhóm hiện đại, hỗ trợ góp tự nguyện, gửi yêu cầu chi tiêu/rút vốn có sự phê duyệt của Chủ quỹ (Owner).
3. **Trải nghiệm người dùng:** Giao diện trực quan, hỗ trợ 3 chủ đề (`light`, `dark`, `system`) và 2 ngôn ngữ (`vi-VN`, `en-US`), phân biệt rõ ràng trạng thái giao dịch (`Pending`, `Processing`, `Completed`, `Failed`).

### 1.2. Ranh giới kiến trúc & Nguyên tắc bảo mật
- **Nền tảng Backend:** ASP.NET Core (.NET 10 Kestrel) kiến trúc Clean Architecture: Domain Entities $\rightarrow$ Application CQRS Handlers $\rightarrow$ Infrastructure EF Core $\rightarrow$ API Controllers.
- **Ranh giới Actor:** `SystemRole.User = 1` là User thường, `SystemRole.Admin = 2` là Admin hệ thống duy nhất. `SystemAdmin = 0` và `SuperAdmin = 3` là các giá trị legacy bị chặn quyền hoàn toàn. `Fund Owner` vẫn là User, chỉ có quyền trong phạm vi quỹ mình sở hữu, không có quyền Admin hệ thống.
- **Bảo mật giao dịch:** Mọi lệnh ghi biến động số dư yêu cầu mã PIN bảo mật 6 số (`SecurityPin`) hoặc chữ ký sinh trắc học WebAuthn Level 2. Khóa chống trùng lặp (`IdempotencyKey`) và phạm vi (`IdempotencyScope`) bảo đảm tính bất biến (idempotent) tuyệt đối, không double-posting.

### 1.3. Bảng phân quyền và trạng thái tài khoản cho giao diện

| Actor / Trạng Thái | Quyền Hạn và Hành Vi Giao Diện (UI UX) | Backend Enforcement (HTTP Code) |
|---|---|:---:|
| **`User` / `Active`** | Toàn quyền sử dụng Dashboard, ví cá nhân, thu chi, chuyển tiền, ngân sách, mục tiêu, định kỳ, quỹ nhóm, SePay sandbox, AI assistant, thông báo, hỗ trợ/khiếu nại, hồ sơ. | `200 OK` / `201 Created` / `204 NoContent` |
| **`User` / `Restricted`** | Đăng nhập và đọc dữ liệu, xem số dư, nhận tiền P2P hợp lệ, gửi SupportCase; **bị chặn toàn bộ lệnh ghi nợ tiền ra** (rút tiền, chuyển tiền, nộp quỹ, chi tiêu). | `403 Forbidden` (`account_restricted`) |
| **`User` / `Locked`** | Bị thu hồi toàn bộ phiên hoạt động. **Chặn hoàn toàn đăng nhập và refresh token**. Khi Admin mở khóa (`Active`), bắt buộc phải đăng nhập lại từ đầu. | `403 Forbidden` (`account_unavailable`) / `401 Unauthorized` |
| **`Fund Owner`** | Là User có quyền điều hành trong quỹ mình sở hữu (chốt định mức, cấp quyền `CanSpend`, duyệt chi/rút quỹ tự do). **Không có quyền Admin hệ thống**. | Theo quyền hạn Quỹ (`fund_owner_required`) |
| **`Admin = 2`** | UI Quản trị riêng biệt, không dùng tài khoản Admin để giao dịch tài chính người dùng. | Theo phân hệ Admin |
| **`Legacy (0, 3)`** | Không được cấp quyền, chặn truy cập ngay từ tầng xác thực. | `403 Forbidden` (`legacy_role_rejected`) |

---

## 2. DANH SÁCH MÀN HÌNH & HÀNH TRÌNH NGƯỜI DÙNG (USER JOURNEYS)

### 2.1. Danh mục màn hình Mobile Client

1. **Nhóm Xác thực & Onboarding:**
   - `SCR-AUTH-01`: Màn hình Giới thiệu & Onboarding.
   - `SCR-AUTH-02`: Đăng ký tài khoản (`POST /api/auth/register`).
   - `SCR-AUTH-03`: Xác thực OTP Email (`POST /api/auth/email-verification/verify`).
   - `SCR-AUTH-04`: Thiết lập mã PIN 6 chữ số lần đầu (`POST /api/auth/pin/setup`).
   - `SCR-AUTH-05`: Đăng nhập Email/SpendId & Mật khẩu (`POST /api/auth/login`).
   - `SCR-AUTH-06`: Xác thực mã PIN để hoàn tất đăng nhập (`POST /api/auth/pin/verify`).
   - `SCR-AUTH-07`: Quên mật khẩu & Đặt lại mật khẩu (`/forgot-password`, `/reset-password`).
   - `SCR-AUTH-08`: Quên / Đặt lại mã PIN (`/api/security-pin/request-reset`, `/api/security-pin/reset`).
   - `SCR-AUTH-09`: Đăng nhập nhanh bằng Passkey / Sinh trắc học (`/api/auth/passkeys/login/*`).

2. **Nhóm Trang chủ & Tài chính Cá nhân:**
   - `SCR-HOME-01`: Trang chủ / Dashboard tổng quan số dư các ví, thu chi gần đây, ngân sách tháng (`GET /api/dashboard`).
   - `SCR-WALLET-01`: Danh sách ví cá nhân (Ví mặc định, Tiết kiệm, Khẩn cấp — tối đa 3 ví) (`GET /api/wallets`).
   - `SCR-WALLET-02`: Mở thêm ví phụ / Đổi tên ví / Đóng ví số dư 0 (`POST/PUT/DELETE /api/wallets`).
   - `SCR-TX-01`: Tạo khoản thu nhập (`POST /api/transactions/income`).
   - `SCR-TX-02`: Tạo khoản chi tiêu đơn ví (`POST /api/transactions/expenses`).
   - `SCR-TX-03`: Tạo khoản chi tiêu đa ví (`POST /api/transactions/expenses/multi-wallet`).
   - `SCR-TX-04`: Lịch sử giao dịch, tìm kiếm, lọc theo danh mục / thời gian (`GET /api/transactions`).
   - `SCR-TX-05`: Chi tiết giao dịch, sửa ghi chú, xem chứng từ hóa đơn (`PATCH /api/transactions/{id}`, `/api/evidence/*`).
   - `SCR-TRANSFER-01`: Chuyển tiền nội bộ giữa các ví cá nhân (`POST /api/transfers/internal`).
   - `SCR-TRANSFER-02`: Chuyển tiền P2P sang người dùng SpendWise khác (`POST /api/transfers/p2p`).

3. **Nhóm Kế hoạch Tài chính:**
   - `SCR-PLAN-01`: Quản lý ngân sách chi tiêu tháng theo danh mục (`GET/POST/PUT/DELETE /api/budgets`).
   - `SCR-PLAN-02`: Quản lý mục tiêu tích lũy, nộp tiền/rút tiền mục tiêu (`GET/POST /api/goals`, `/contribute`, `/withdraw`).
   - `SCR-PLAN-03`: Lập lịch giao dịch định kỳ, tạm dừng/tiếp tục (`GET/POST /api/recurring-transactions`).
   - `SCR-PLAN-04`: Quy tắc phân loại chi tiêu tự động theo từ khóa (`GET/POST /api/transaction-rules`).

4. **Nhóm Quỹ Nhóm (Group Funds):**
   - `SCR-FUND-01`: Danh sách quỹ đang tham gia & Khám phá quỹ (`GET /api/v1/funds`).
   - `SCR-FUND-02`: Tạo quỹ nhóm mới (Cố định Fixed Fund V2 hoặc Tự do Free Fund) (`POST /api/v1/funds`).
   - `SCR-FUND-03`: Chi tiết quỹ nhóm (Số dư, tổng đóng góp, danh sách thành viên, trạng thái mục tiêu).
   - `SCR-FUND-04`: Đóng góp quỹ nhóm (PIN/WebAuthn) (`POST /api/v1/funds/{id}/contributions`).
   - `SCR-FUND-05`: Quản lý thành viên & Cấp quyền chi tiêu `CanSpend` (Chủ quỹ) (`PUT .../spending-permission`).
   - `SCR-FUND-06`: Tạo yêu cầu chi tiêu từ quỹ (`POST /api/v1/funds/{id}/requests`).
   - `SCR-FUND-07`: Báo cáo khoản chi bất thường (Tạo SupportCase gửi Admin) (`POST .../requests/{id}/report`).
   - `SCR-FUND-08`: Rút vốn / Rời quỹ tự do có tính toán hoàn tiền (`POST .../refund-preview`, `.../leave`).
   - `SCR-FUND-09`: Đề xuất hoạt động quỹ & Bỏ phiếu (Governance V1) (`/activity-proposals`, `/vote`, `/settle`).
   - `SCR-FUND-10`: Bầu cử Chủ quỹ mới (Ứng cử, Bỏ phiếu, Nhận/Từ chối quyền) (`/elections/*`).

5. **Nhóm Bạn bè & Ngân hàng:**
   - `SCR-FRIEND-01`: Danh sách bạn bè, tìm kiếm qua SpendId/Email (`GET /api/friends`).
   - `SCR-FRIEND-02`: Gửi lời mời kết bạn, Chấp nhận/Từ chối lời mời (`POST /api/friends/requests/*`).
   - `SCR-BANK-01`: Quản lý liên kết tài khoản ngân hàng (`GET/POST/DELETE /api/bank-connections`).
   - `SCR-BANK-02`: Nạp tiền Sandbox SePay Test Mode (`POST /api/bank-connections/test-mode/wallet-top-ups`).
   - `SCR-BANK-03`: Rút tiền Sandbox SePay Test Mode về ngân hàng (`POST /api/bank-connections/test-mode/wallet-withdrawals`).

6. **Nhóm Trợ lý AI, Thông báo & Hỗ trợ:**
   - `SCR-AI-01`: Trò chuyện với Trợ lý AI tài chính cá nhân (`POST /api/ai/conversations`, `/chat`).
   - `SCR-AI-02`: Xem và xác nhận thực thi đề xuất hành động từ AI (`GET /api/ai/action-proposals`, `/execute`).
   - `SCR-NOTIF-01`: Hộp thư thông báo hệ thống, Broadcast từ Admin, đánh dấu đã đọc (`/api/notifications/*`).
   - `SCR-NOTIF-02`: Tùy chọn kênh thông báo In-App / Email / Push (`/api/notifications/preferences`).
   - `SCR-SUPP-01`: Danh sách hồ sơ Hỗ trợ / Khiếu nại cá nhân (`GET /api/cases/support`).
   - `SCR-SUPP-02`: Tạo phiếu Hỗ trợ / Khiếu nại giao dịch trực tiếp gửi Admin (`POST /api/cases/support`).
   - `SCR-SUPP-03`: Chi tiết phiếu hỗ trợ, gửi phản hồi trao đổi, đính kèm biên lai (`/api/cases/support/{id}/messages`).

7. **Nhóm Cài đặt & Bảo mật:**
   - `SCR-SET-01`: Hồ sơ cá nhân, đổi ảnh đại diện, đổi SpendId (`GET/PUT /api/users/me`, `/avatar`).
   - `SCR-SET-02`: Cài đặt giao diện (Chủ đề Sáng/Tối/Hệ thống, Ngôn ngữ Tiếng Việt/English) (`PUT /api/users/me/preferences`).
   - `SCR-SET-03`: Đổi mật khẩu đăng nhập & Đổi mã PIN bảo mật (`/change-password`, `PUT /api/security-pin`).
   - `SCR-SET-04`: Quản lý thiết bị Passkeys / Sinh trắc học WebAuthn (`/api/auth/passkeys/*`).
   - `SCR-SET-05`: Danh sách phiên đăng nhập hoạt động, thu hồi phiên từ xa (`/api/auth/sessions/*`).

---

## 3. CÁC QUY TẮC NGHIỆP VỤ BẮT BUỘC (BUSINESS INVARIANTS)

### 3.1. Ràng buộc Ví cá nhân & An toàn Ngân hàng (`IActiveBankGuard`)
- Mỗi tài khoản tối đa **3 ví cá nhân hoạt động đồng thời** (`WalletLimit = 3`). Cố gắng tạo ví thứ 4 trả về `409 Conflict` (`wallet_limit_reached`).
- Chỉ được đóng ví phụ khi số dư khả dụng bằng 0 và không có giao dịch đang giữ chỗ (`409 wallet_not_empty`).
- **Fail-Closed Bank Guard:** Mọi thao tác ghi nợ hoặc biến động tiền mặt bắt buộc người dùng phải có ít nhất 1 tài khoản ngân hàng ở trạng thái `Active`. Chưa có hoặc đã hủy liên kết trả về `409 Conflict` (`active_bank_required`).

### 3.2. Quỹ Cố định V2 (Fixed Fund V2)
- **Bãi bỏ hoàn toàn ngưỡng 20% và trạng thái Pending24h:** Các khoản chi hợp lệ được thực thi tức thời.
- **Cấp quyền chi tiêu (`CanSpend`):** Chủ quỹ (Owner) chỉ được cấp cờ `CanSpend = true` cho thành viên đã đóng đủ 100% nghĩa vụ cá nhân (`RequiredVnd`).
- **Quyền chi thực tế (`EffectiveCanSpend`):** Được hệ thống tính toán động dựa trên 4 điều kiện:
  1. Thành viên đã đóng đủ 100% nghĩa vụ cá nhân.
  2. Quỹ nhóm đã đạt 100% mục tiêu ban đầu (`TargetVnd`).
  3. Thành viên đã được cấp cờ `CanSpend = true`.
  4. Quỹ đang ở trạng thái Hoạt động (`Active`).
- **Bảo toàn trạng thái hoàn thành:** Khi quỹ đã đạt 100% mục tiêu, các khoản chi hợp lệ làm giảm tiền mặt trong ví quỹ **KHÔNG làm mất trạng thái hoàn thành của nhóm**.
- **Báo cáo khoản chi bất thường (`POST /api/v1/funds/requests/{id}/report`):** Tạo `SupportCase` gửi Admin điều tra; **tuyệt đối không tự động đảo ngược tiền (no auto-clawback)** và không tự can thiệp vào bút toán sổ cái.

### 3.3. Hỗ trợ & Khiếu nại Trực tiếp (Direct Support & Complaints)
- **Không ép buộc qua AI:** Người dùng được phép gửi trực tiếp Khiếu nại giao dịch (`category = complaint_transaction`) hoặc Yêu cầu hỗ trợ kỹ thuật (`category = general`) tới Admin qua `POST /api/cases/support`.
- **Dừng tạo Dispute độc lập:** Endpoint cũ `POST /api/cases/disputes` trả về `400 Bad Request` (`dispute_workflow_deprecated`). Toàn bộ hồ sơ tranh chấp lịch sử được bảo tồn ở chế độ chỉ đọc.

### 3.4. Quản lý Phiên & Thu hồi (Session Management & Revocation)
- `POST /api/users/me/sessions/revoke-all` hoặc `DELETE /api/users/me/sessions`: Thu hồi ngay lập tức toàn bộ phiên làm việc của người dùng (`204 NoContent`).
- Mọi token cũ sau khi bị thu hồi sẽ nhận mã lỗi `401 Unauthorized`.

---

## 4. BẢNG DANH MỤC API USER CHI TIẾT & CONTRACT DTO

| Phân Hệ | Method | Tuyến Đường (Route) | Quyền & Header | Request DTO Tóm Tắt | Trạng Thái Thành Công | Mã Lỗi Phổ Biến |
|---|:---:|---|---|---|:---:|---|
| **Auth** | `POST` | `/api/auth/register` | Anonymous | `{ email, password, fullName }` | `200 OK` | `409 duplicate_email` |
| | `POST` | `/api/auth/email-verification/verify` | Anonymous | `{ email, otp }` | `200 OK` | `400 invalid_otp` |
| | `POST` | `/api/auth/pin/setup` | Bearer TempToken | `{ pin: "123456" }` | `200 OK` | `400 weak_pin` |
| | `POST` | `/api/auth/login` | Anonymous | `{ identifier, password, deviceName }` | `200 OK` | `401 wrong_password`, `403 account_unavailable` |
| | `POST` | `/api/auth/pin/verify` | Anonymous | `{ challengeToken, pin, deviceName }` | `200 OK` | `401 pin_invalid`, `403 pin_locked` |
| | `POST` | `/api/auth/refresh` | Anonymous | `{ refreshToken, deviceName }` | `200 OK` | `401 invalid_token` |
| | `GET` | `/api/auth/sessions` | Bearer User | Không có | `200 OK` | `401 unauthorized` |
| | `DELETE`| `/api/auth/sessions/{id}` | Bearer User | Không có | `204 NoContent` | `404 session_not_found` |
| | `DELETE`| `/api/users/me/sessions` | Bearer User | Không có | `204 NoContent` | `401 unauthorized` |
| **Passkeys** | `POST` | `/api/auth/passkeys/register/options` | Bearer User | Không có | `200 OK` | `401 unauthorized` |
| | `POST` | `/api/auth/passkeys/register/verify` | Bearer User | `{ id, rawId, response: { clientDataJSON, attestationObject } }` | `200 OK` | `400 invalid_attestation` |
| | `POST` | `/api/auth/passkeys/login/options` | Anonymous | `{ identifier }` | `200 OK` | `404 user_not_found` |
| | `POST` | `/api/auth/passkeys/login/verify` | Anonymous | `{ id, rawId, response: { clientDataJSON, authenticatorData, signature, userHandle } }` | `200 OK` | `401 passkey_invalid` |
| **Profile** | `GET` | `/api/users/me` | Bearer User | Không có | `200 OK` | `401 unauthorized` |
| | `PUT` | `/api/users/me/preferences` | Bearer User | `{ theme: "dark", language: "vi-VN" }` | `200 OK` | `400 invalid_preference` |
| | `POST` | `/api/users/me/avatar` | Bearer User | Multipart `file` | `200 OK` | `400 file_too_large` |
| **Ví (Wallets)** | `GET` | `/api/wallets` | Bearer User | Không có | `200 OK` | `401 unauthorized` |
| | `POST` | `/api/wallets` | Bearer User | `{ name: "Ví Tiết Kiệm", type: "Savings" }` | `201 Created` | `409 wallet_limit_reached`, `active_bank_required` |
| | `PUT` | `/api/wallets/{id}` | Bearer User | `{ name: "Tên Mới" }` | `200 OK` | `404 wallet_not_found` |
| | `DELETE`| `/api/wallets/{id}` | Bearer User | Không có | `204 NoContent` | `409 wallet_not_empty` |
| **Thu/Chi** | `POST` | `/api/transactions/income` | Bearer User | `{ walletId, amount, categoryId, description }` | `200 OK` | `409 active_bank_required` |
| | `POST` | `/api/transactions/expenses` | Bearer User | `{ walletId, amount, categoryId, description }` | `200 OK` | `400 insufficient_balance` |
| | `POST` | `/api/transactions/expenses/multi-wallet` | Bearer User | `{ amount, categoryId, description, allocations: [{ walletId, amount }] }` | `200 OK` | `400 allocation_mismatch` |
| | `GET` | `/api/transactions` | Bearer User | Query: `page, pageSize, categoryId, fromDate, toDate` | `200 OK` | `401 unauthorized` |
| | `PATCH` | `/api/transactions/{id}` | Bearer User | `{ description: "Ghi chú mới" }` | `200 OK` | `404 transaction_not_found` |
| **Chuyển tiền**| `POST` | `/api/transfers/internal` | Bearer User | `{ sourceWalletId, destinationWalletId, amount, securityPin }` | `200 OK` | `401 pin_invalid`, `403 account_restricted` |
| | `POST` | `/api/transfers/p2p` | Bearer User | `{ recipientIdentifier, sourceWalletId, amount, note, securityPin }` | `200 OK` | `400 self_transfer`, `404 recipient_not_found` |
| **Kế hoạch** | `GET/POST`| `/api/budgets` | Bearer User | `{ categoryId, amount, year, month }` | `200 OK` | `409 budget_exists` |
| | `GET/POST`| `/api/goals` | Bearer User | `{ name, targetAmount, targetDate, walletId }` | `200 OK` | `400 invalid_target` |
| | `POST` | `/api/goals/{id}/contribute`| Bearer User | `{ amount, walletId, securityPin }` | `200 OK` | `400 insufficient_balance` |
| | `POST` | `/api/goals/{id}/withdraw` | Bearer User | `{ amount, walletId, securityPin }` | `200 OK` | `400 exceed_accumulated` |
| | `GET/POST`| `/api/recurring-transactions` | Bearer User | `{ walletId, categoryId, amount, frequency, startDate }` | `200 OK` | `400 invalid_frequency` |
| | `POST` | `/api/recurring-transactions/{id}/occurrences/{occId}/record` | Bearer User | `{ actualAmount, securityPin }` | `200 OK` | `409 occurrence_already_recorded` |
| **Quỹ Nhóm V1** | `POST` | `/api/v1/funds` | Bearer User | `{ name, targetVnd, fundType: "Fixed\|Free" }` | `201 Created` | `409 active_group_limit` |
| | `POST` | `/api/v1/funds/{id}/obligations/confirm` | Bearer (Owner) | `{ individualRequiredVnd: 500000 }` | `200 OK` | `403 fund_owner_required` |
| | `POST` | `/api/v1/funds/{id}/contributions` | Bearer Member | `{ amount: 500000, walletId, securityPin }` | `200 OK` | `400 insufficient_balance` |
| | `PUT` | `/api/v1/funds/{id}/members/{userId}/spending-permission` | Bearer (Owner) | `{ canSpend: true }` | `200 OK` | `400 contribution_not_completed` |
| | `POST` | `/api/v1/funds/{id}/requests` | Bearer Member | `{ amount, description, idempotencyKey }` | `200 OK` | `403 spending_permission_denied` |
| | `POST` | `/api/v1/funds/requests/{id}/report` | Bearer Member | `{ reason: "Báo cáo chi tiêu bất thường" }` | `200 OK` | `404 fund_not_found` |
| | `POST` | `/api/v1/funds/{id}/activity-proposals` | Bearer Member | `{ targetVnd: 1000000, idempotencyKey, securityPin }` | `200 OK` | `409 activity_transition_in_progress` |
| | `POST` | `/api/v1/funds/activity-proposals/{id}/settle` | Bearer (Owner) | `{ securityPin }` | `200 OK` | `409 activity_not_approved` |
| | `POST` | `/api/v1/funds/elections/{id}/accept` | Bearer Winner | `{ securityPin }` | `200 OK` | `404 election_not_found` |
| | `POST` | `/api/v1/funds/elections/{id}/reject` | Bearer Winner | `{ reason, securityPin }` | `200 OK` | `400 election_already_finished` |
| **Hỗ trợ / Khiếu nại** | `POST` | `/api/cases/support` | Bearer User | `{ category, priority, subject, description, linkedTransactionId }` | `200 OK` | `400 invalid_category` |
| | `GET` | `/api/cases/support` | Bearer User | Không có | `200 OK` | `401 unauthorized` |
| | `GET` | `/api/cases/support/{id}` | Bearer User | Không có | `200 OK` | `404 case_not_found` |
| | `POST` | `/api/cases/support/{id}/messages` | Bearer User | `{ message: "Nội dung phản hồi" }` | `200 OK` | `404 case_not_found` |
| | `POST` | `/api/cases/disputes` | Bearer User | `{ transactionId, reason }` | `400 Bad Request` | `dispute_workflow_deprecated` |
| **Thông báo** | `GET` | `/api/notifications` | Bearer User | Query: `unreadOnly, page, pageSize` | `200 OK` | `401 unauthorized` |
| | `POST` | `/api/notifications/{id}/read` | Bearer User | Không có | `204 NoContent` | `404 notification_not_found` |
| | `POST` | `/api/notifications/read-all` | Bearer User | Không có | `204 NoContent` | `401 unauthorized` |
| | `GET/PUT`| `/api/notifications/preferences` | Bearer User | `{ inAppEnabled, emailEnabled, pushEnabled }` | `200 OK` | `400 invalid_preference` |
| **AI Assistant**| `POST` | `/api/ai/conversations` | Bearer User | `{ title: "Tư vấn tài chính" }` | `201 Created` | `401 unauthorized` |
| | `POST` | `/api/ai/conversations/{id}/chat` | Bearer User | `{ message: "Tôi nên tiết kiệm thế nào?" }` | `200 OK` | `503 ai_unavailable` |
| | `POST` | `/api/ai/action-proposals/{id}/execute` | Bearer User | `{ confirmed: true, securityPin }` | `200 OK` | `409 ai_action_not_pending` |
| **SePay Sandbox**| `GET` | `/api/bank-connections` | Bearer User | Không có | `200 OK` | `401 unauthorized` |
| | `POST` | `/api/bank-connections/test-mode/wallet-top-ups` | Bearer User | `{ bankConnectionId, amount: 100000 }` | `200 OK` | `404 connection_not_found` |
| | `POST` | `/api/bank-connections/test-mode/wallet-withdrawals` | Bearer User | `{ bankConnectionId, amount: 50000, securityPin }` | `200 OK` | `400 insufficient_balance` |
| | `POST` | `/api/bank-connections/test-mode/wallet-withdrawals/{id}/reconcile` | Bearer User | Không có | `200 OK` | `404 withdrawal_not_found` |

---

## 5. PHÂN ĐỊNH RANH GIỚI FRONTEND-ONLY VÀ HƯỚNG DẪN TÍCH HỢP

### 5.1. Các hạng mục FRONTEND-ONLY / PENDING UI E2E
Các tính năng sau đây bắt buộc phải có màn hình UI thiết bị thật hoặc native application wrapper mới kiểm chứng hoàn chỉnh:
1. **Giao diện Chủ đề (Theme):** Hiển thị màu sắc `light`, `dark`, hoặc theo chế độ hệ điều hành `system`. Backend cung cấp API lưu trữ preference (`PUT /api/users/me/preferences`), Frontend tự chịu trách nhiệm render CSS / Theme Provider.
2. **Ngôn ngữ (Localization):** Chuyển đổi hiển thị từ ngữ giữa Tiếng Việt (`vi-VN`) và Tiếng Anh (`en-US`) trên giao diện người dùng. Backend xử lý chuẩn format số tiền VND và UTC timestamp.
3. **Responsive, Navigation & Animations:** Điều hướng màn hình (Stack/Tab navigation), chuyển cảnh, animation quét vân tay/khuôn mặt mockup trên Google AI Studio.
4. **Cảm biến Sinh trắc học Phần cứng Native:** Việc tương tác trực tiếp với Secure Enclave (iOS) hoặc Android BiometricPrompt Keystore chỉ có thể thực hiện khi đóng gói ứng dụng native (React Native / Flutter / Swift / Kotlin). Backend đã sẵn sàng và nghiệm thu chuẩn W3C WebAuthn Level 2.

### 5.2. Hướng dẫn Tích hợp an toàn cho Codex
- **Quản lý Token:** Lưu trữ `accessToken` trong RAM/State, `refreshToken` trong Encrypted Storage. Tự động gọi `/api/auth/refresh` khi gặp mã `401`. Nếu refresh thất bại, chuyển hướng về màn hình đăng nhập.
- **Xử lý Chế tài Tài khoản:**
  - Nếu API trả về `403` với `code: "account_restricted"`: Hiển thị banner cảnh báo màu vàng "Tài khoản đang bị hạn chế giao dịch tài chính. Bạn vẫn có thể xem số dư và liên hệ Hỗ trợ". Vô hiệu hóa các nút Chuyển tiền, Chi tiêu, Rút tiền.
  - Nếu API trả về `403` với `code: "account_unavailable"`: Đăng xuất người dùng, chuyển về màn hình thông báo "Tài khoản của bạn đã bị khóa. Vui lòng liên hệ Quản trị viên để được hỗ trợ".
- **Giao dịch Sandbox SePay:** Không giả định một lệnh rút tiền là tiền thật; hiển thị rõ nhãn `Môi trường Thử nghiệm Sandbox` để tránh nhầm lẫn cho người dùng.
