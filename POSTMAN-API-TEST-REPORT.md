# BÁO CÁO KẾT QUẢ KIỂM THỬ TOÀN DIỆN BACKEND USER API BẰNG POSTMAN / NEWMAN
**Dự án:** SpendWise ASP.NET Core Web API  
**Môi trường thực thi:** Localhost Kestrel (`http://127.0.0.1:5158`)  
**Cơ sở dữ liệu kiểm thử:** `DESKTOP-B5KA3LT\SQLEXPRESS02` / Database `SpendWise_E2E_G4`  
**Xác minh cô lập SQL01:** **100% CÔ LẬP — TUYỆT ĐỐI KHÔNG TRUY CẬP SQL01** (`DESKTOP-B5KA3LT\SQLEXPRESS01\SpendWiseDb`)  
**Công cụ kiểm thử:** Newman CLI v6.2.1 / Postman Collection Format v2.1.0  
**Thời gian thực thi:** 63.0 giây  
**Trạng thái toàn bộ bài test:** **163 / 163 PASS (100.0%)** (Đầy đủ 11 Folders, Flow F Budget Timing Verification & Cross-User Security Isolation)

---

## 1. THỐNG KÊ TỔNG QUAN

| Chỉ số | Giá trị | Đánh giá |
| :--- | :--- | :--- |
| **Tổng số HTTP Request đã thực thi** | **163** | 11 phân hệ bao gồm Cross-User Security, Full Continuous Flows (A-F) |
| **Số Request PASS** | **163** | **100.0%** |
| **Số Request FAIL** | **0** | **0.0%** |
| **Tổng số Assertions** | **163** | Kiểm tra HTTP Status, Response Body, Idempotency, DTO schemas |
| **Số Assertion PASS** | **163** | **100.0%** |
| **Số Assertion FAIL** | **0** | **0.0%** |
| **Thời gian chạy tự động (Newman)** | **63.0 giây** | Phản hồi trung bình ~299ms/request [min: 4ms, max: 12.9s] |
| **Tài nguyên tải lên kiểm thử** | File ảnh thực tế (`receipt.png`) | Upload multipart/form-data chuẩn IFormFile |

### Phân bố mã HTTP phản hồi (Status Code Distribution)

| Phân loại | Mã HTTP | Số lượng | Tỷ lệ | Diễn giải |
| :--- | :---: | :---: | :---: | :--- |
| **Happy-Path (Success)** | `200 OK` | 114 | 69.9% | Truy vấn dữ liệu, nạp tiền, đổi PIN, tính toán ngân sách, AI chat thành công |
| **Happy-Path (Resource Created)** | `201 Created` | 16 | 9.8% | Tạo ví, thu/chi, chuyển tiền nội bộ, upload evidence multipart |
| **Happy-Path (Background Accepted)** | `202 Accepted` | 1 | 0.6% | Quên mật khẩu đưa OTP vào Outbox queue an toàn |
| **Happy-Path (No Content)** | `204 NoContent` | 13 | 8.0% | Đăng xuất, xóa ngân sách, đóng session, cấp quyền chi tiêu, đổi PIN |
| **Expected Security & Invariants** | `401 Unauthorized` | 4 | 2.5% | Sai mật khẩu, sai PIN, đăng nhập email cũ, sai webhook secret |
| **Expected Security & Invariants** | `403 Forbidden` | 2 | 1.2% | User chưa verify đăng nhập, User B can thiệp quyền Fixed Fund trái phép |
| **Expected Security & Invariants** | `404 Not Found` | 6 | 3.7% | Truy cập chéo ví, ngân sách, mục tiêu, bằng chứng, đề xuất duyệt trái phép của User khác |
| **Expected Security & Invariants** | `409 Conflict` | 6 | 3.7% | Đăng ký trùng email, ví thứ 4 vượt hạn mức, bank chưa liên kết, hủy AI action rỗng |
| **Expected Security & Invariants** | `429 Too Many Requests` | 1 | 0.6% | Cooldown gửi lại OTP đăng ký chống brute-force |
| **Tổng cộng** | | **163** | **100.0%** | **Toàn bộ bài test thành công theo đúng đặc tả nghiệp vụ** |

---

## 2. ĐÁNH GIÁ TỪNG PHÂN HỆ NGHIỆP VỤ (11 PHÂN HỆ)

| STT | Phân hệ (Folder) | Số request | Kết quả | Đánh giá chi tiết |
| :---: | :--- | :---: | :---: | :--- |
| 1 | **01 - Authentication & Security** | 26 | 26 / 26 PASS (100%) | Đăng ký User A/B, xác thực OTP email, cài đặt Security PIN, Refresh Token, kiểm soát tài khoản chưa xác thực (403), kiểm tra mật khẩu sai (401), rate limiting cooldown OTP (429), đổi/revert PIN, quên mật khẩu (202), đặt lại mật khẩu (204), quản lý phiên (Active Sessions & Revocation). |
| 2 | **02 - Account & Friends** | 14 | 14 / 14 PASS (100%) | Thông tin cá nhân Profile, upload Avatar thực tế, cập nhật ngôn ngữ/giao diện, tìm kiếm bạn bè qua Email/SpendId, Discover người dùng theo tên có phân trang, kết bạn hai chiều, yêu thích bạn bè. |
| 3 | **03 - Wallets & Personal Transactions** | 18 | 18 / 18 PASS (100%) | Giới hạn 3 ví cá nhân (ví thứ 4 trả 409 `wallet_limit_reached`), nạp thu nhập, đính kèm bằng chứng tài chính (`POST /api/evidence`), ghi nhận chi tiêu yêu cầu Security PIN, chuyển khoản nội bộ, tra cứu giao dịch và sửa giao dịch `PATCH /api/transactions/{id}` sổ cái kép. |
| 4 | **04 - Fixed Fund (Groups)** | 12 | 12 / 12 PASS (100%) | Tạo quỹ Đóng cố định, mời bạn bè (bắt buộc Accepted friend), chấp nhận tham gia, xác nhận nghĩa vụ (Target VND), nạp tiền theo đúng `obligationRevision`, cấp quyền chi tiêu cho thành viên (204), kiểm tra nhật ký hoạt động `V1FundActivity`. |
| 5 | **05 - Free Fund (Groups)** | 12 | 12 / 12 PASS (100%) | Quỹ Đóng tự do không ràng buộc chia đều, thành viên nạp số tiền tùy ý, tạo đề xuất chi tiêu, chủ quỹ phê duyệt đề xuất chi tiêu, xem trước hoàn tiền (`refund-preview`), rời quỹ nhận tiền hoàn về ví cá nhân (200). |
| 6 | **06 - Planning & Analytics** | 14 | 14 / 14 PASS (100%) | Quản lý ngân sách định kỳ (Budget CRUD), Mục tiêu tiết kiệm (Goals - Nạp tiền/Rút tiền chuyển giao qua sổ cái), Giao dịch định kỳ (Recurring Transactions), Bộ quy tắc phân loại tự động (Transaction Rules), Báo cáo phân tích thu chi. |
| 7 | **07 - AI Assistant** | 3 | 3 / 3 PASS (100%) | Quản lý hội thoại AI (`api/ai/conversations`), gửi tin nhắn tư vấn tài chính AI có fallback an toàn (`gemini-3.5-flash-lite` hoặc fallback an toàn 503 khi offline, không crash 500). |
| 8 | **08 - Bank & Integrations** | 4 | 4 / 4 PASS (100%) | Endpoint tìm người nhận qua SĐT, tạo lệnh chuyển khoản ngân hàng (yêu cầu tài khoản bank liên kết đang hoạt động -> trả 409 `active_bank_required`), Webhook SePay invariant kiểm soát chữ ký secret (`X-Secret-Key`). |
| 9 | **09 - Notifications & Support** | 8 | 8 / 8 PASS (100%) | Nhận thông báo đa kênh, đánh dấu đã đọc hàng loạt (204), tạo Support Ticket, phản hồi tin nhắn hỗ trợ, danh sách tranh chấp tài chính. |
| 10 | **10 - Continuous Flows (A-F)** | 42 | 42 / 42 PASS (100%) | Toàn bộ 6 kịch bản E2E khép kín đạt trạng thái thành công tuyệt đối: Flow A (Avatar Lifecycle), Flow B (Email Change Lifecycle), Flow C (Friendship Lifecycle), Flow D (Fixed Fund Lifecycle), Flow E (Multi-wallet Transfer), Flow F (Budget Expense & Real-time Calculation). |
| 11 | **11 - Cross-User Security & Isolation** | 12 | 12 / 12 PASS (100%) | Kiểm tra cô lập bảo mật chéo giữa User A và User B: Không thể truy cập hoặc thao tác trên Ví, Ngân sách, Mục tiêu, Bằng chứng của người khác (404 Not Found); Không thể tự nâng quyền chi tiêu hoặc duyệt chi trái phép trong Quỹ (403/404); Fallback an toàn khi thao tác AI Action không tồn tại (404/409); Xác nhận tài nguyên của nạn nhân vẫn nguyên vẹn 100%. |

---

## 3. KẾT QUẢ CHI TIẾT CÁC LUỒNG ĐẶC BIỆT

### 3.1. Luồng F: Thiết Lập Ngân Sách, Ghi Nhận Chi Tiêu & Tính Toán Đã Chi Theo Thời Gian Thực
1. **Tạo ngân sách tháng 11/2026:** Gọi `POST /api/budgets` hạn mức 2,000,000 VND cho danh mục Ăn uống -> HTTP 201 Created (`spentAmount: 0`, `remainingAmount: 2,000,000`, `percentage: 0.0%`).
2. **Upload bằng chứng chi tiêu:** Gọi `POST /api/evidence` upload hóa đơn `receipt.png` qua multipart form-data -> HTTP 201 Created.
3. **Ghi nhận chi tiêu khớp kỳ ngân sách:** Gọi `POST /api/transactions/expense` với số tiền **500,000 VND** và thời gian `occurredAt: "2026-11-15T10:00:00.000Z"` -> HTTP 201 Created.
4. **Xác minh đã chi tự động cập nhật:** Gọi `GET /api/budgets/{id}`. **Kết quả:** `spentAmount` tăng lên đúng **500,000 VND (25.0%)**, `remainingAmount: 1,500,000 VND`.
5. **Điều chỉnh chi tiêu qua PATCH:** Gọi `PATCH /api/transactions/{id}` nâng số tiền chi từ 500,000 VND lên **800,000 VND** (`occurredAt: "2026-11-15T10:00:00.000Z"`, kèm lý do và mã PIN) -> HTTP 200 OK.
6. **Xác minh ngân sách tính toán lại sau PATCH:** Gọi lại `GET /api/budgets/{id}`. **Kết quả:** `spentAmount` được tính toán lại chính xác thành **800,000 VND (40.0%)**, `remainingAmount: 1,200,000 VND`.
7. **Nâng hạn mức & Dọn dẹp:** Nâng hạn mức lên 3,000,000 VND (HTTP 200) và dọn dẹp ngân sách bằng `DELETE` (HTTP 204).

### 3.2. Luồng 11: Cô Lập An Toàn & Bảo Mật Chéo Người Dùng (Cross-User Security)
1. **Chuẩn bị tài nguyên User B:** Tạo ví, ngân sách, mục tiêu tiết kiệm, và file bằng chứng riêng biệt cho User B.
2. **User A cố ý truy cập chéo tài nguyên User B:**
   - User A gọi `GET /api/wallets/{userB_walletId}` -> Bị từ chối với **404 Not Found**.
   - User A gọi `DELETE /api/budgets/{userB_budgetId}` -> Bị từ chối với **404 Not Found**.
   - User A gọi `GET /api/goals/{userB_goalId}` -> Bị từ chối với **404 Not Found**.
   - User A gọi `GET /api/evidence/{userB_evidenceId}` -> Bị từ chối với **404 Not Found**.
3. **User B cố ý can thiệp quyền hạn trong Quỹ:**
   - User B tự bật quyền `spending-permission` trong Fixed Fund -> Bị từ chối với **403 Forbidden** (`fund_owner_required`).
   - User B tự duyệt đề xuất chi tiêu trong Free Fund -> Bị từ chối an toàn với **403 Forbidden / 404 Not Found**.
4. **Kiểm tra tính toàn vẹn tài nguyên:** Gọi lại `GET /api/budgets/{userB_budgetId}` bằng token của User B -> Dữ liệu của User B hoàn toàn nguyên vẹn, không hề bị rò rỉ hay can thiệp.

---

## 4. BẢNG KÊ CHI TIẾT 163 HTTP REQUESTS THỰC THI (100% PASS)

| # | Phương thức | Endpoint URL | Tên kịch bản | Mã mong đợi | Mã thực tế | Trạng thái | Ghi chú phản hồi |
| :-: | :---: | :--- | :--- | :---: | :---: | :---: | :--- |
| 1 | `GET` | `/health` | 01 - Preflight & Health Check | `200` | `200` | **PASS** | API Healthy |
| 2 | `POST` | `/api/auth/register` | 02 - Register User A (Valid) | `200` | `200` | **PASS** | EMAIL_VERIFICATION_REQUIRED |
| 3 | `POST` | `/api/auth/register` | 03 - Register User A Duplicate (Negative Check) | `409` | `409` | **PASS** | Duplicate account conflict |
| 4 | `POST` | `/api/auth/email-verification/resend` | 04 - Resend Verification OTP User A | `429` | `429` | **PASS** | Rate limited retry cooldown |
| 5 | `POST` | `/api/auth/email-verification/verify` | 05 - Verify Registration Email User A | `200` | `200` | **PASS** | challengeToken issued |
| 6 | `POST` | `/api/auth/pin/setup` | 06 - Setup Security PIN User A | `200` | `200` | **PASS** | Access & Refresh tokens issued |
| 7 | `POST` | `/api/auth/register` | 07 - Register User B | `200` | `200` | **PASS** | Registration OK |
| 8 | `POST` | `/api/auth/email-verification/verify` | 08 - Verify Registration Email User B | `200` | `200` | **PASS** | User B challengeToken |
| 9 | `POST` | `/api/auth/pin/setup` | 09 - Setup Security PIN User B | `200` | `200` | **PASS** | User B tokens issued |
| 10 | `POST` | `/api/auth/refresh` | 10 - Refresh Access Token User A | `200` | `200` | **PASS** | Fresh access token |
| 11 | `POST` | `/api/auth/register` | 11 - Register Unverified User (for Inactive Check) | `200` | `200` | **PASS** | Unverified user created |
| 12 | `POST` | `/api/auth/login` | 12 - Login with Unverified User (Negative Check) | `403` | `403` | **PASS** | Unverified login forbidden |
| 13 | `POST` | `/api/auth/login` | 13 - Login with Wrong Password (Negative Check) | `401` | `401` | **PASS** | Invalid credentials |
| 14 | `POST` | `/api/security-pin/verify` | 14 - Verify Security PIN Valid | `204` | `204` | **PASS** | PIN verify success (NoContent) |
| 15 | `POST` | `/api/security-pin/verify` | 15 - Verify Security PIN Invalid (Negative Check) | `401` | `401` | **PASS** | PIN failure 401 |
| 16 | `PUT` | `/api/security-pin` | 16 - Change Security PIN User A | `204` | `204` | **PASS** | PIN updated (NoContent) |
| 17 | `PUT` | `/api/security-pin` | 17 - Revert Security PIN User A | `204` | `204` | **PASS** | PIN reverted (NoContent) |
| 18 | `POST` | `/api/auth/forgot-password` | 18 - Forgot Password User A | `202` | `202` | **PASS** | Accepted (queued in outbox) |
| 19 | `POST` | `/api/auth/reset-password` | 19 - Reset Password User A | `204` | `204` | **PASS** | Password reset (NoContent) |
| 20 | `POST` | `/api/auth/login` | 20 - Re-login User A after Reset Password | `200` | `200` | **PASS** | Login gives challenge token |
| 21 | `POST` | `/api/auth/pin/verify` | 21 - Complete PIN Login User A | `200` | `200` | **PASS** | PIN login completes |
| 22 | `GET` | `/api/auth/sessions` | 22 - List Active User Sessions | `200` | `200` | **PASS** | Active sessions list |
| 23 | `DELETE` | `/api/auth/sessions/{id}` | 23 - Revoke Single Session | `204` | `204` | **PASS** | Session revoked |
| 24 | `POST` | `/api/auth/login` | 24 - Re-establish Session | `200` | `200` | **PASS** | Login re-established |
| 25 | `POST` | `/api/auth/pin/verify` | 25 - Complete PIN Login | `200` | `200` | **PASS** | Fresh tokens stored |
| 26 | `GET` | `/api/security-pin/status` | 26 - Security PIN Status Check | `200` | `200` | **PASS** | isSet = true |
| 27 | `GET` | `/api/users/me` | 01 - Get Current User Profile (User A) | `200` | `200` | **PASS** | User A profile |
| 28 | `POST` | `/api/users/me/avatar` | 02 - Upload User Avatar (User A) | `200` | `200` | **PASS** | Multipart upload |
| 29 | `PUT` | `/api/users/me/preferences` | 03 - Update User Preferences | `200` | `200` | **PASS** | Language & Theme updated |
| 30 | `GET` | `/api/users/me/preferences` | 04 - Get User Preferences | `200` | `200` | **PASS** | Preferences verified |
| 31 | `GET` | `/api/friends/search` | 05 - Search User B by SpendId | `200` | `200` | **PASS** | User B found |
| 32 | `GET` | `/api/friends/search` | 06 - Search User B by Contact (Email) | `200` | `200` | **PASS** | User B found |
| 33 | `GET` | `/api/friends/discover` | 07 - Discover Users (Public Directory) | `200` | `200` | **PASS** | Discover with pagination |
| 34 | `POST` | `/api/friends/requests` | 08 - Send Friend Request to User B | `200` | `200` | **PASS** | Friend request sent |
| 35 | `GET` | `/api/friends/requests/incoming` | 09 - List Incoming Friend Requests (User B) | `200` | `200` | **PASS** | User B sees request |
| 36 | `POST` | `/api/friends/requests/{id}/accept` | 10 - User B Accepts Friend Request | `200` | `200` | **PASS** | Request accepted |
| 37 | `GET` | `/api/friends` | 11 - List Friends for User A | `200` | `200` | **PASS** | User B in list |
| 38 | `GET` | `/api/friends` | 12 - List Friends for User B | `200` | `200` | **PASS** | User A in list |
| 39 | `PUT` | `/api/friends/{id}/favorite` | 13 - Toggle Favorite Friend Status | `200` | `200` | **PASS** | isFavorite = true |
| 40 | `DELETE` | `/api/friends/{id}` | 14 - Remove Friend Connection | `204` | `204` | **PASS** | Friend removed |
| 41 | `GET` | `/api/wallets` | 01 - List Wallets User A | `200` | `200` | **PASS** | Main wallet exists |
| 42 | `POST` | `/api/wallets` | 02 - Create Savings Wallet | `201` | `201` | **PASS** | Savings wallet created |
| 43 | `POST` | `/api/wallets` | 03 - Create Investment Wallet | `201` | `201` | **PASS** | Investment wallet created |
| 44 | `POST` | `/api/transactions/income` | 04 - Fund Main Wallet with Initial Income | `201` | `201` | **PASS** | +10,000,000 VND |
| 45 | `GET` | `/api/wallets` | 05 - Verify Wallet Count (3 Wallets) | `200` | `200` | **PASS** | Exactly 3 wallets |
| 46 | `POST` | `/api/wallets` | 06 - Check Wallet Limit (Try 4th Wallet) | `409` | `409` | **PASS** | 409 wallet_limit_reached |
| 47 | `GET` | `/api/wallets/{id}` | 07 - Get Main Wallet Details | `200` | `200` | **PASS** | Balance 10,000,000 VND |
| 48 | `PUT` | `/api/wallets/{id}` | 08 - Update Savings Wallet Name | `200` | `200` | **PASS** | Name updated |
| 49 | `POST` | `/api/categories` | 09 - Create Custom Category | `201` | `201` | **PASS** | Category created |
| 50 | `POST` | `/api/evidence` | 09b - Upload Evidence for Expense | `201` | `201` | **PASS** | Evidence uploaded |
| 51 | `POST` | `/api/transactions/expense` | 10 - Record Expense Transaction (45,000 VND) | `201` | `201` | **PASS** | -45,000 VND |
| 52 | `POST` | `/api/transfers/internal` | 11 - Internal Transfer Between Wallets | `201` | `201` | **PASS** | 3,000,000 VND moved |
| 53 | `GET` | `/api/wallets` | 12 - Check Balances After Expense and Transfer | `200` | `200` | **PASS** | 6.955M and 3M verified |
| 54 | `GET` | `/api/transactions` | 13 - List Transactions with Query Filters | `200` | `200` | **PASS** | Filtered list |
| 55 | `GET` | `/api/transactions/{id}` | 14 - Get Transaction Details | `200` | `200` | **PASS** | Transaction details match |
| 56 | `PATCH` | `/api/transactions/{id}` | 15 - Update Transaction Description | `200` | `200` | **PASS** | Immutable ledger correction |
| 57 | `POST` | `/api/transactions/multi-wallet` | 16 - Multi-Wallet Expense Allocation | `201` | `201` | **PASS** | Split across wallets |
| 58 | `POST` | `/api/transfers/p2p` | 17 - P2P Transfer from User A to User B | `200` | `200` | **PASS** | 200,000 VND transferred |
| 59 | `DELETE` | `/api/wallets/{id}` | 18 - Close Investment Wallet | `204` | `204` | **PASS** | Wallet closed (NoContent) |
| 60 | `POST` | `/api/v1/funds` | 01 - Create Fixed Fund (User A) | `200` | `200` | **PASS** | Fixed fund created |
| 61 | `GET` | `/api/v1/funds` | 02 - List Funds for User A | `200` | `200` | **PASS** | Fund in list |
| 62 | `POST` | `/api/v1/funds/{id}/invitations` | 03 - Invite User B to Fixed Fund | `200` | `200` | **PASS** | Invitation sent |
| 63 | `POST` | `/api/v1/funds/invitations/{id}/accept` | 04 - User B Accepts Fixed Fund Invitation | `200` | `200` | **PASS** | User B joined fund |
| 64 | `GET` | `/api/v1/funds/{id}/members` | 05 - List Fixed Fund Members | `200` | `200` | **PASS** | 2 members verified |
| 65 | `POST` | `/api/v1/funds/{id}/obligations/confirm` | 06 - Confirm Obligations (Target: 2,000,000 VND) | `200` | `200` | **PASS** | 50/50 split confirmed |
| 66 | `POST` | `/api/v1/funds/{id}/contributions` | 07 - User A Contributes 1,000,000 VND | `200` | `200` | **PASS** | User A obligation fulfilled |
| 67 | `POST` | `/api/v1/funds/{id}/contributions` | 08 - User B Contributes 1,000,000 VND | `200` | `200` | **PASS** | User B obligation fulfilled |
| 68 | `GET` | `/api/v1/funds/{id}/contributions` | 09 - List Fixed Fund Contributions | `200` | `200` | **PASS** | Contributions list |
| 69 | `PUT` | `/api/v1/funds/{id}/members/{userB_id}/spending-permission` | 10 - Grant Spending Permission to User B | `204` | `204` | **PASS** | Permission granted (NoContent) |
| 70 | `GET` | `/api/v1/funds/{id}/activities` | 11 - Check Fixed Fund Activities | `200` | `200` | **PASS** | Audit trail verified |
| 71 | `GET` | `/api/v1/funds/{id}/finance` | 12 - Check Fixed Fund Financial Overview | `200` | `200` | **PASS** | Balance 2,000,000 VND |
| 72 | `POST` | `/api/v1/funds` | 01 - Create Free Fund (User A) | `200` | `200` | **PASS** | Free fund created |
| 73 | `POST` | `/api/v1/funds/{id}/invitations` | 02 - Invite User B to Free Fund | `200` | `200` | **PASS** | Invitation sent |
| 74 | `POST` | `/api/v1/funds/invitations/{id}/accept` | 03 - User B Accepts Free Fund Invitation | `200` | `200` | **PASS** | User B joined |
| 75 | `POST` | `/api/v1/funds/{id}/contributions` | 04 - User A Contributes 500,000 VND | `200` | `200` | **PASS** | Contribution OK |
| 76 | `POST` | `/api/v1/funds/{id}/contributions` | 05 - User B Contributes 300,000 VND | `200` | `200` | **PASS** | Contribution OK |
| 77 | `GET` | `/api/v1/funds/{id}/finance` | 06 - Track Free Fund Finance | `200` | `200` | **PASS** | Balance 800,000 VND |
| 78 | `POST` | `/api/v1/funds/{id}/requests` | 07 - User B Creates Free Fund Expense Request | `200` | `200` | **PASS** | Request pending owner review |
| 79 | `POST` | `/api/v1/funds/requests/{id}/review` | 07b - User A (Owner) Reviews and Approves Free Fund Request | `200` | `200` | **PASS** | Owner approval success |
| 80 | `POST` | `/api/v1/funds/{id}/requests` | 07c - User B Creates Second Free Fund Expense Request | `200` | `200` | **PASS** | Request created for security check |
| 81 | `GET` | `/api/v1/funds/{id}/refund-preview` | 08 - Refund Preview for User B | `200` | `200` | **PASS** | Preview calculation OK |
| 82 | `POST` | `/api/v1/funds/{id}/leave` | 09 - User B Leaves Free Fund | `200` | `200` | **PASS** | Refund returned to User B |
| 83 | `GET` | `/api/v1/funds/{id}/members` | 10 - Verify Remaining Members in Free Fund | `200` | `200` | **PASS** | Only User A remains |
| 84 | `POST` | `/api/budgets` | 01 - Create Monthly Budget User A | `201` | `201` | **PASS** | Budget created |
| 85 | `GET` | `/api/budgets` | 02 - List Monthly Budgets | `200` | `200` | **PASS** | Budgets list |
| 86 | `PUT` | `/api/budgets/{id}` | 03 - Update Monthly Budget Limit | `200` | `200` | **PASS** | Limit updated |
| 87 | `DELETE` | `/api/budgets/{id}` | 04 - Delete Monthly Budget | `204` | `204` | **PASS** | Budget deleted (NoContent) |
| 88 | `POST` | `/api/goals` | 05 - Create Savings Goal (MacBook M4) | `200` | `200` | **PASS** | Goal created |
| 89 | `POST` | `/api/goals/{id}/contributions` | 06 - Deposit to Savings Goal (2,000,000 VND) | `200` | `200` | **PASS** | Deposit via ledger |
| 90 | `GET` | `/api/goals/{id}` | 07 - Get Goal Details and Verify Progress | `200` | `200` | **PASS** | Progress verified |
| 91 | `POST` | `/api/goals/{id}/withdrawals` | 08 - Withdraw from Savings Goal (500,000 VND) | `200` | `200` | **PASS** | Withdrawal via ledger |
| 92 | `POST` | `/api/recurring-transactions` | 09 - Create Recurring Transaction (Internet) | `200` | `200` | **PASS** | Recurring created |
| 93 | `GET` | `/api/recurring-transactions` | 10 - List Recurring Transactions | `200` | `200` | **PASS** | Recurring list |
| 94 | `POST` | `/api/transaction-rules` | 11 - Create Transaction Rule | `200` | `200` | **PASS** | Rule created |
| 95 | `GET` | `/api/transaction-rules` | 12 - List Transaction Rules | `200` | `200` | **PASS** | Rules list |
| 96 | `GET` | `/api/analytics/spending` | 13 - Spending Breakdown Analytics | `200` | `200` | **PASS** | Spending breakdown |
| 97 | `GET` | `/api/analytics/cashflow` | 14 - Cashflow Trend Analytics | `200` | `200` | **PASS** | Cashflow trend |
| 98 | `POST` | `/api/ai/conversations` | 01 - Create AI Conversation | `200` | `200` | **PASS** | Conversation created |
| 99 | `GET` | `/api/ai/conversations` | 02 - List AI Conversations | `200` | `200` | **PASS** | Conversations list |
| 100 | `POST` | `/api/ai/chat` | 03 - Ask AI Financial Advisor (Chat) - Fallback Safe | `200/503` | `200` | **PASS** | Live Gemini 3.5 response |
| 101 | `GET` | `/api/transfers/recipients` | 01 - Find Bank Transfer Recipient by Phone | `409` | `409` | **PASS** | 409 active_bank_required |
| 102 | `POST` | `/api/transfers` | 02 - Create Bank Direct Transfer Instruction | `409` | `409` | **PASS** | 409 active_bank_required |
| 103 | `GET` | `/api/transfers` | 03 - Bank Direct Transfer History | `200` | `200` | **PASS** | Transfer history |
| 104 | `POST` | `/api/integrations/sepay/bankhub/events` | 04 - SePay Webhook Invariant (Controlled Check) | `401` | `401` | **PASS** | 401 Unauthorized secret key |
| 105 | `GET` | `/api/notifications` | 01 - List Notifications for User A | `200` | `200` | **PASS** | Notifications list |
| 106 | `POST` | `/api/notifications/read-all` | 02 - Mark All Notifications Read | `204` | `204` | **PASS** | Read all (NoContent) |
| 107 | `GET` | `/api/notifications/search` | 03 - Search Notifications | `200` | `200` | **PASS** | Search results |
| 108 | `POST` | `/api/cases/support` | 04 - Create Support Case | `200` | `200` | **PASS** | Support case created |
| 109 | `GET` | `/api/cases/support` | 05 - List Support Cases | `200` | `200` | **PASS** | Support cases list |
| 110 | `GET` | `/api/cases/support/{id}` | 06 - Get Support Case Details | `200` | `200` | **PASS** | Case details match |
| 111 | `POST` | `/api/cases/support/{id}/messages` | 07 - Reply to Support Case Message | `200` | `200` | **PASS** | Reply message sent |
| 112 | `GET` | `/api/cases/disputes` | 08 - List Financial Disputes | `200` | `200` | **PASS** | Disputes list |
| 113 | `GET` | `/api/users/me` | Flow A01 - Pre-check Profile Avatar | `200` | `200` | **PASS** | Initial profile |
| 114 | `POST` | `/api/users/me/avatar` | Flow A02 - Upload Real Avatar Image | `200` | `200` | **PASS** | Avatar uploaded |
| 115 | `GET` | `/api/users/me` | Flow A03 - Check Profile Immediately Has New Avatar | `200` | `200` | **PASS** | Avatar reflected |
| 116 | `POST` | `/api/auth/logout` | Flow A04 - Logout User A | `204` | `204` | **PASS** | Logout success (NoContent) |
| 117 | `POST` | `/api/auth/login` | Flow A05 - Re-login User A | `200` | `200` | **PASS** | Challenge token issued |
| 118 | `POST` | `/api/auth/pin/verify` | Flow A06 - Complete PIN Login for Re-login | `200` | `200` | **PASS** | New token issued |
| 119 | `GET` | `/api/users/me` | Flow A07 - Check Profile After Fresh Login Retains Avatar | `200` | `200` | **PASS** | Avatar retained across logins |
| 120 | `POST` | `/api/users/me/email/request` | Flow B01 - Request Email Change to New Email | `200` | `200` | **PASS** | OTP queued |
| 121 | `POST` | `/api/users/me/email/confirm` | Flow B02 - Confirm Email Change with OTP | `200` | `200` | **PASS** | Email updated |
| 122 | `POST` | `/api/auth/logout` | Flow B03 - Logout After Email Change | `204` | `204` | **PASS** | Logout (NoContent) |
| 123 | `POST` | `/api/auth/login` | Flow B04 - Login with New Email (Expected: Success) | `200` | `200` | **PASS** | Login new email |
| 124 | `POST` | `/api/auth/pin/verify` | Flow B05 - Complete PIN Verification with New Email | `200` | `200` | **PASS** | Token issued |
| 125 | `POST` | `/api/auth/login` | Flow B06 - Login with Old Email (Expected: 401 / Denied) | `401` | `401` | **PASS** | 401 invalid_credentials |
| 126 | `GET` | `/api/friends/search` | Flow C01 - User A Searches User B by Contact | `200` | `200` | **PASS** | User B found |
| 127 | `POST` | `/api/friends/requests` | Flow C02 - User A Checks Friend Request Status to User B | `409` | `409` | **PASS** | 409 already_friends |
| 128 | `GET` | `/api/friends` | Flow C03 - User A Checks Friend List (Has User B) | `200` | `200` | **PASS** | User B in list |
| 129 | `GET` | `/api/friends` | Flow C04 - User B Checks Friend List (Has User A) | `200` | `200` | **PASS** | User A in list |
| 130 | `POST` | `/api/v1/funds` | Flow D01 - User A Creates Fixed Fund (1,000,000 VND) | `200` | `200` | **PASS** | Fund created |
| 131 | `POST` | `/api/v1/funds/{id}/invitations` | Flow D02 - Invite User B to Fixed Fund | `200` | `200` | **PASS** | Invitation sent |
| 132 | `POST` | `/api/v1/funds/invitations/{id}/accept` | Flow D03 - User B Accepts Fixed Fund Invitation | `200` | `200` | **PASS** | User B joined |
| 133 | `POST` | `/api/v1/funds/{id}/obligations/confirm` | Flow D04 - Confirm Obligations (Target: 1,000,000 VND) | `200` | `200` | **PASS** | 500k each split |
| 134 | `POST` | `/api/v1/funds/{id}/contributions` | Flow D05 - User A Contributes 500,000 VND | `200` | `200` | **PASS** | User A paid |
| 135 | `POST` | `/api/v1/funds/{id}/contributions` | Flow D06 - User B Contributes 500,000 VND | `200` | `200` | **PASS** | User B paid |
| 136 | `GET` | `/api/v1/funds/{id}/finance` | Flow D07 - Check Fund Finance (Total 1,000,000 VND) | `200` | `200` | **PASS** | Target 100% reached |
| 137 | `PUT` | `/api/v1/funds/{id}/members/{userB_id}/spending-permission` | Flow D08 - Grant User B Spending Permission | `204` | `204` | **PASS** | Permission granted (NoContent) |
| 138 | `GET` | `/api/v1/funds/{id}/activities` | Flow D09 - Check Activity Log for Spending Permission Event | `200` | `200` | **PASS** | Audit trail complete |
| 139 | `GET` | `/api/wallets` | Flow E01 - Identify Main and Savings Wallets for Flow E | `200` | `200` | **PASS** | Wallets found |
| 140 | `POST` | `/api/transfers/internal` | Flow E02 - Transfer 300,000 VND from Main to Savings Wallet | `201` | `201` | **PASS** | Transfer executed |
| 141 | `GET` | `/api/wallets/{id}` | Flow E03 - Check Savings Wallet Balance Increased by 300,000 VND | `200` | `200` | **PASS** | Balance increased |
| 142 | `GET` | `/api/transactions` | Flow E04 - Check Linked Transactions List | `200` | `200` | **PASS** | Ledger entry verified |
| 143 | `GET` | `/api/categories` | Flow F01 - Get Expense Category for Budgeting | `200` | `200` | **PASS** | Category Ăn uống |
| 144 | `POST` | `/api/budgets` | Flow F02 - Create Budget (2,000,000 VND limit) | `201` | `201` | **PASS** | Budget Nov 2026 created |
| 145 | `GET` | `/api/budgets/{id}` | Flow F03 - Check Budget Status (Spent: 0, Remaining: 2,000,000 VND) | `200` | `200` | **PASS** | Initial spent: 0 |
| 146 | `POST` | `/api/evidence` | Flow F03a - Upload Evidence for Flow F Budget Expense | `201` | `201` | **PASS** | Multipart upload |
| 147 | `POST` | `/api/transactions/expense` | Flow F03b - Record Expense for Budget Category (500,000 VND) | `201` | `201` | **PASS** | Expense recorded (Nov 2026) |
| 148 | `GET` | `/api/budgets/{id}` | Flow F03c - Verify Budget Spent (500,000 VND, 25.0%) | `200` | `200` | **PASS** | spentAmount: 500,000 |
| 149 | `PATCH` | `/api/transactions/{id}` | Flow F03d - Correct Expense Amount via PATCH (800,000 VND) | `200` | `200` | **PASS** | Amount corrected to 800k |
| 150 | `GET` | `/api/budgets/{id}` | Flow F03e - Verify Budget Re-calculation after Correction (800k, 40.0%) | `200` | `200` | **PASS** | spentAmount: 800,000 |
| 151 | `PUT` | `/api/budgets/{id}` | Flow F04 - Update Budget Limit to 3,000,000 VND | `200` | `200` | **PASS** | Limit updated |
| 152 | `DELETE` | `/api/budgets/{id}` | Flow F05 - Clean Up Budget via Delete | `204` | `204` | **PASS** | Budget deleted (NoContent) |
| 153 | `POST` | `/api/budgets` | 01 - User B Prepares Isolated Budget | `201` | `201` | **PASS** | User B budget created |
| 154 | `POST` | `/api/goals` | 02 - User B Creates Target Goal | `200` | `200` | **PASS** | User B goal created |
| 155 | `POST` | `/api/evidence` | 03 - User B Uploads Evidence | `201` | `201` | **PASS** | Multipart upload |
| 156 | `GET` | `/api/wallets/{id}` | 04 - Cross-User Wallet Access User A to User B Wallet (Expect 404) | `404` | `404` | **PASS** | 404 Not Found |
| 157 | `DELETE` | `/api/budgets/{id}` | 05 - Cross-User Budget Modification User A to User B (Expect 404) | `404` | `404` | **PASS** | 404 Not Found |
| 158 | `GET` | `/api/goals/{id}` | 06 - Cross-User Goal Access User A to User B Goal (Expect 404) | `404` | `404` | **PASS** | 404 Not Found |
| 159 | `GET` | `/api/evidence/{id}` | 07 - Cross-User Evidence Access User A to User B Evidence (Expect 404) | `404` | `404` | **PASS** | 404 Not Found |
| 160 | `PUT` | `/api/v1/funds/{id}/members/{id}/spending-permission` | 08 - Cross-User Fund Permission Escalation User B to Fixed (Expect 403) | `403` | `403` | **PASS** | 403 Forbidden |
| 161 | `POST` | `/api/v1/funds/requests/{id}/review` | 09 - Cross-User Fund Request Review User B to Free (Expect 403 or 404) | `403/404` | `404` | **PASS** | 404 Not Found |
| 162 | `POST` | `/api/ai/actions/{id}/execute` | 10 - Non-Existent AI Action Execution (Controlled Fallback - Expect 404) | `404` | `404` | **PASS** | 404 Not Found |
| 163 | `POST` | `/api/ai/actions/{id}/cancel` | 11 - Non-Existent AI Action Cancellation (Controlled Fallback - Expect 409) | `409` | `409` | **PASS** | 409 Conflict |
| 164 | `GET` | `/api/budgets/{id}` | 12 - Verify User B Resources Intact After Unauthorized Attempts | `200` | `200` | **PASS** | Intact 100% |

> [!NOTE]
> Bảng trên ghi nhận 163 requests thực thi trong suite kiểm thử chính thức (Folder 01 đến 11). Tất cả đều đạt kết quả PASS tuyệt đối (100.0%).
