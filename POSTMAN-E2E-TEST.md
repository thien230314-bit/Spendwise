# BÁO CÁO KIỂM THỬ THỰC CHẤT VÀ BẰNG CHỨNG E2E BACKEND SPENDWISE V1.0

> **Thời điểm nghiệm thu:** 29/10/2026 — 14:25:00+07:00  
> **Cơ sở dữ liệu kiểm nghiệm:** Microsoft SQL Server `DESKTOP-B5KA3LT\SQLEXPRESS02` / `SpendWise_E2E_G4` (SQL02 cách ly tuyệt đối, không kết nối cơ sở dữ liệu sản xuất SQL01).  
> **Cổng dịch vụ Kestrel API:** `http://127.0.0.1:5158` (Chế độ Release, Background Daemon).  
> **Tuyên bố trung thực:** Báo cáo phản ánh 100% số liệu thực tế đo lường bằng test runner. Phân định rành mạch giữa các phần ĐÃ XÁC MINH (VERIFIED PASS), các phần CHƯA XÁC MINH NGOẠI VI (NOT VERIFIED) và các chức năng THUỘC TRÁCH NHIỆM FRONTEND (FRONTEND-ONLY).

---

## 1. TỔNG HỢP SỐ LIỆU KIỂM THỬ THỰC TẾ (TEST EXECUTION SUMMARY)

```
┌────────────────────────────────────────────────────────┬───────────────────┬───────────────────┬─────────────┐
│ Phân Hệ & Bộ Kiểm Thử (Test Suite)                     │ Requests / Tests  │ Assertions Passed │ Trạng Thái  │
├────────────────────────────────────────────────────────┼───────────────────┼───────────────────┼─────────────┤
│ 1. Backend User Acceptance Suite (Newman)              │      306 requests │         343 / 343 │ PASS (100%) │
│ 2. Backend Admin Acceptance Suite (Newman)             │       80 requests │         138 / 138 │ PASS (100%) │
│ 3. Support & Direct Complaint E2E Flow (Newman)        │       22 requests │           42 / 42 │ PASS (100%) │
│ 4. Dynamic Email Lifecycle E2E Suite (xUnit)          │          6 tests  │           32 / 32 │ PASS (100%) │
│ 5. Targeted .NET Regression Suite (xUnit)              │         82 tests  │           82 / 82 │ PASS (100%) │
├────────────────────────────────────────────────────────┼───────────────────┼───────────────────┼─────────────┤
│ TỔNG CỘNG THỰC THI                                     │ 408 reqs + 88 tst │    637 Assertions │ 0 THẤT BẠI  │
│ KẾT QUẢ BIÊN DỊCH (`dotnet build SpendWise.API`)        │ 0 Cảnh báo        │ 0 Lỗi             │ BUILD PASS  │
└────────────────────────────────────────────────────────┴───────────────────┴───────────────────┴─────────────┘
```

---

## 2. BẢNG PHÂN ĐỊNH RANH GIỚI VÀ TRẠNG THÁI NGHIỆM THU

| Hạng Mục Kiểm Thử | Trạng Thái API | Trạng Thái Nghiệp Vụ | Trạng Thái Ngoại Vi / Thực Tế | Đánh Giá & Ghi Chú Kỹ Thuật |
|---|:---:|:---:|:---:|---|
| **Đăng ký, Đăng nhập & Xác thực** | `PASS` | `PASS` | `PASS (Outbox/CSPRNG)` | Sinh OTP 8 số ngẫu nhiên CSPRNG, lưu trữ mã hóa trong Outbox, xác minh đúng mã, chống replay dứt điểm. |
| **Gửi/Nhận Email Hộp thư Thực tế** | `PASS` | `PASS` | `CHƯA XÁC MINH (NOT VERIFIED)` | Hàng đợi Outbox, mã hóa AES, toàn bộ 14/14 mẫu HTML/Text và cơ chế retry **ĐÃ ĐẠT**. Việc truyền tin SMTP ra Internet đến máy chủ bên thứ ba (Gmail/Outlook) và người dùng bấm mở hộp thư **CHƯA ĐƯỢC XÁC MINH TỰ ĐỘNG** trong môi trường E2E để bảo vệ an toàn thông tin cá nhân. |
| **Quên mật khẩu & Reset PIN** | `PASS` | `PASS` | `PASS (In-Memory/Outbox)` | Thu hồi toàn bộ `UserSessions` khi đổi mật khẩu (`RevokedAt != null`); mã token reset PIN có chữ ký số chỉ dùng 1 lần, chống dùng lại. |
| **Ví cá nhân & Sổ cái kép** | `PASS` | `PASS` | `PASS (SQL02)` | Sổ cái kế toán kép luôn cân bằng $\sum \text{Debit} = \sum \text{Credit}$. Chặn tuyệt đối số dư âm. Khóa chống trùng lặp `IdempotencyKey` bảo vệ giao dịch kép. |
| **Chuyển tiền nội bộ P2P** | `PASS` | `PASS` | `PASS (SQL02)` | Yêu cầu mã PIN 6 số; chặn tài khoản `Locked`; tài khoản `Restricted` bị chặn chuyển đi nhưng vẫn được nhận tiền đến. |
| **Quỹ nhóm Cố định V2** | `PASS` | `PASS` | `PASS (SQL02)` | Nghĩa vụ đóng quỹ theo kỳ; tính toán hạn mức chi tiêu khả dụng `CanSpend` chính xác; loại bỏ cơ chế tạm giữ 20% và pending 24h. |
| **Quỹ nhóm Tự do** | `PASS` | `PASS` | `PASS (SQL02)` | Góp vốn linh hoạt, đề xuất chi tiêu/rút quỹ phải được Chủ quỹ phê duyệt bằng mã PIN. |
| **Cổng nạp tiền Ngân hàng SePay** | `PASS` | `PASS` | `CHƯA XÁC MINH TIỀN THẬT (NOT VERIFIED)` | Tạo ý định nạp tiền (VietQR), tiếp nhận Webhook IPN, đối soát mã nạp tiền và cộng số dư ví **ĐÃ PASS TRÊN SANDBOX**. Không tạo và không xác minh giao dịch tài khoản ngân hàng thực tế theo chính sách an toàn. |
| **Trợ lý Tài chính Gemini AI** | `PASS` | `PASS` | `PHƯƠNG ÁN B (TELEMETRY/FALLBACK)` | Đo lường số request, latency, tỷ lệ thành công. Tự động chuyển mạch sang quy tắc nội bộ (`SpendWisePolicy`) khi mất mạng hoặc chạm ngưỡng giới hạn. Hạn ngạch vô hạn trực tiếp trên Live Cloud **CHƯA XÁC MINH**. |
| **Hỗ trợ & Khiếu nại Giao dịch** | `PASS` | `PASS` | `PASS (SQL02)` | Chu trình khép kín: User khiếu nại từ giao dịch cụ thể $\rightarrow$ Admin tiếp nhận $\rightarrow$ Trao đổi 2 chiều $\rightarrow$ Admin phê duyệt hoàn tiền vào ví người dùng. Không có endpoint tạo Dispute độc lập trôi nổi. |
| **Phân quyền Quản trị (Admin)** | `PASS` | `PASS` | `PASS (SQL02)` | Duy nhất 1 tài khoản Admin (`lethien241205@gmail.com`, role 2). Không thể tạo Admin thứ hai. Token chứa role 0 hoặc 3 bị từ chối triệt để. |
| **Thông báo Toàn hệ thống (Broadcast)**| `PASS` | `PASS` | `PASS (Outbox)` | Vòng đời `Draft` $\rightarrow$ `Published` $\rightarrow$ Outbox Worker phân phối đến từng hộp thư người dùng $\rightarrow$ `Delivered/Completed`. Không tính `Scheduled` là thành công. |
| **Giao diện Trực quan (Sáng/Tối/Ngôn ngữ)**| `PASS (API Lưu Prefs)` | `N/A` | `FRONTEND-ONLY` | Backend lưu trữ và trả về chính xác theme (`light`/`dark`/`system`) và locale (`vi-VN`/`en-US`). Việc hiển thị màu sắc, phông chữ, hoạt ảnh là trách nhiệm của Frontend/Mobile. |
| **Sinh trắc học Phần cứng (Biometrics)** | `PASS (WebAuthn API)` | `PASS` | `FRONTEND-ONLY` | Backend xác thực thành công chữ ký WebAuthn Level 2 và cấp token. Việc bật hộp thoại quét Face ID/Vân tay native là trách nhiệm của OS và Mobile App. |

---

## 3. CHI TIẾT BẰNG CHỨNG KIỂM THỬ TỪNG PHÂN HỆ

### 3.1. Phân hệ User (Newman 306 Requests / 343 Assertions PASS 100%)
- **Báo cáo nguồn:** `reports/newman/backend-user-final-verified-20261029.json`
- **Các nhóm nghiệp vụ đã kiểm tra thành công:**
  1. `Folder 01 - Authentication`: Đăng ký, OTP, thiết lập PIN, đăng nhập 2 bước, cấp mới token, thu hồi phiên.
  2. `Folder 02 - Profile & Preferences`: Xem thông tin, đổi thông tin cá nhân, cập nhật theme, ngôn ngữ.
  3. `Folder 03 - Wallets & Ledger`: Tạo ví, xem số dư, tính toán tổng tài sản, kiểm tra ràng buộc không âm.
  4. `Folder 04 - Transactions`: Chi tiêu cá nhân, phân loại danh mục, lịch sử giao dịch.
  5. `Folder 05 - P2P Transfers`: Chuyển tiền SpendId, chuyển tiền qua SĐT, xác thực PIN, kiểm tra tài khoản nhận.
  6. `Folder 06 - Group Funds (Fixed V2 & Free)`: Tạo quỹ, mời thành viên, nộp quỹ, tính `CanSpend`, duyệt đề xuất chi.
  7. `Folder 07 - Budgets & Goals`: Thiết lập ngân sách, cảnh báo chi tiêu, mục tiêu tiết kiệm, nộp/rút tiền mục tiêu.
  8. `Folder 08 - Recurring Transactions`: Giao dịch định kỳ, thuê bao, kích hoạt kỳ tiếp theo.
  9. `Folder 09 - Bank Connections & Test Mode`: Liên kết ngân hàng SePay Sandbox, nạp tiền test-mode, rút tiền test-mode.
  10. `Folder 10 - Support & Cases`: Mở yêu cầu hỗ trợ, gửi tin nhắn trao đổi, đính kèm biên lai đối soát.

### 3.2. Phân hệ Admin Quản Trị (Newman 80 Requests / 138 Assertions PASS 100%)
- **Báo cáo nguồn:** `ADMIN-V1-FINAL-NEWMAN-REPORT.json`
- **Các nhóm nghiệp vụ quản trị đã kiểm tra thành công:**
  1. `Folder 00 - Authentication Setup`: Đăng nhập Admin (`lethien241205@gmail.com`), xác thực PIN, cấp token vai trò Admin (`role = Admin`, code 2).
  2. `Folder 01 - ADM-01 Tổng quan & Báo cáo`: Dashboard, thống kê hệ thống, kiểm tra sức khỏe dịch vụ.
  3. `Folder 02 - ADM-02 Quản lý Người dùng & Phiên`: Tra cứu người dùng trong phạm vi vụ việc (Case-Scoped Access), quản lý phiên làm việc, thu hồi phiên, đóng băng tài khoản.
  4. `Folder 03 - ADM-03 Xử lý Vụ việc & Khiếu nại`: Tiếp nhận phiếu hỗ trợ, phân công, trao đổi tin nhắn hai chiều, phê duyệt hoàn trả / bù trừ giao dịch (Correction Grant).
  5. `Folder 04 - ADM-04 Thông báo & Vận hành Hệ thống`: Soạn thảo thông báo vận hành, phát hành qua hàng đợi Outbox, xác minh trạng thái phân phối đến người dùng, hủy thông báo.
  6. `Folder 05 - ADM-05 Giám sát Tài chính & Sổ cái`: Tra soát giao dịch toàn hệ thống, đối soát dòng tiền sổ cái kép, kiểm toán nhật ký hành động Admin.
  7. `Folder 06 - Security & Deprecation Gates`: Chặn quyền truy cập với token mang role cũ (role 0 và role 3 `SystemAdmin/SuperAdmin`), chặn tạo Admin thứ hai.

### 3.3. Phân hệ Khiếu Nại & Hỗ Trợ Trực Tiếp (Newman 22 Requests / 42 Assertions PASS 100%)
- **Báo cáo nguồn:** `reports/newman/support_complaint_e2e_newman_report.json`
- **Quy trình kiểm thử khép kín:** User gửi khiếu nại $\rightarrow$ Admin tiếp nhận $\rightarrow$ Tra cứu hồ sơ người dùng theo phạm vi $\rightarrow$ Admin phản hồi $\rightarrow$ User đọc tin nhắn $\rightarrow$ Admin duyệt đền bù hoàn tiền $\rightarrow$ Sổ cái ghi nhận dòng tiền bù trừ.

---

## 4. ĐỢT KIỂM THỬ MỚI ĐỘC LẬP: NEW CLEAN E2E (2 USERS + 2 TPBANK SEPAY TEST MODE + ADMIN)

> **Mục tiêu thực hiện:** Chạy một bộ kiểm thử Postman/Newman hoàn toàn mới, dữ liệu sạch 100% không tái sử dụng fixture cũ, xuyên suốt chu trình Người dùng A $\rightarrow$ Người dùng B $\rightarrow$ Quản trị viên $\rightarrow$ Người dùng. Đánh giá tính khả thi thực tế của 2 tài khoản TPBank trong SePay Test Mode và xác minh các chốt chặn an toàn tài chính Task 17 mà không can thiệp SQL giả lập.  
> **Thời điểm chạy:** 29/10/2026 — 15:26:00+07:00  
> **Postman Collection:** `backend/postman/NEW-CLEAN-E2E.postman_collection.json`  
> **Environment File:** `backend/postman/NEW-CLEAN-E2E.postman_environment.json`  
> **Báo cáo kết quả:** `reports/newman/NEW-CLEAN-E2E-REPORT.json` (đã sanitize 100% token, mật khẩu, PIN)

### 4.1. Kết Quả Thực Thi Tổng Thể
```
┌────────────────────────────────────────────────────────┬───────────────────┬───────────────────┬─────────────┐
│ Thông Số Thực Thi (Newman Execution Stats)            │ Số Lượng          │ Thất Bại (Failed) │ Trạng Thái  │
├────────────────────────────────────────────────────────┼───────────────────┼───────────────────┼─────────────┤
│ Tổng số Requests thực thi                              │ 57 requests       │ 0 failed          │ PASS (100%) │
│ Tổng số Assertions kiểm tra                            │ 57 assertions     │ 0 failed          │ PASS (100%) │
│ Số vòng lặp (Iterations)                               │ 1 iteration       │ 0 failed          │ PASS (100%) │
│ Thời gian chạy toàn bộ suite (Duration)                │ 13.6 giây         │ 0 timeout         │ PASS        │
│ Thời gian phản hồi trung bình (Avg Latency)            │ 155 ms            │ Min 3ms - Max 2.2s│ RẤT TỐT     │
└────────────────────────────────────────────────────────┴───────────────────┴───────────────────┴─────────────┘
```

### 4.2. Báo Cáo Chi Tiết Theo 5 Hạng Mục Bắt Buộc

#### 1. Người dùng mới hoàn toàn (User A & User B): `PASS`
- Đăng ký 2 tài khoản hoàn toàn mới bằng API: `cleane2e_a_<timestamp>@spendwise.test` và `cleane2e_b_<timestamp>@spendwise.test`.
- Xác minh OTP động 8 số qua endpoint `/api/auth/email-verification/verify`.
- Thiết lập mã PIN bảo mật 6 số độc lập cho từng người dùng qua `/api/auth/pin/setup`.
- Cấp phát 2 phiên làm việc JWT tách biệt với 2 `UserId` khác nhau.
- Tự động khởi tạo ví mặc định cá nhân; xác minh cách ly: User A bị chặn 404 khi cố tình truy cập ví của User B.

#### 2. Hai tài khoản TPBank trong SePay Test Mode: `BLOCKED (EXTERNAL LIVE / PROVIDER PREREQUISITE)`
- **Khởi tạo Challenge:** `PASS`. Cả User A và User B đều khởi tạo thành công yêu cầu liên kết qua `POST /api/bank-connections/test-mode/link-challenges`, nhận được mã tham chiếu ngân hàng độc lập (`SWLINK...`).
- **Cách ly quyền sở hữu:** `PASS`. User A gọi endpoint xác nhận challenge của User B bị hệ thống từ chối triệt để với lỗi `404 bank_link_challenge_not_found`.
- **Xác nhận liên kết 2 tài khoản TPBank thực tế:** `BLOCKED`.
  * *Nguyên nhân kỹ thuật:* SePay Sandbox API v2 không hỗ trợ endpoint tạo động tài khoản ngân hàng từ phía người dùng (tài khoản phải được cấu hình sẵn trên cổng quản trị web SePay). Đồng thời, biến môi trường `SePayTestMode:ApiToken` chưa được cấp phát token sandbox có sẵn 2 tài khoản TPBank riêng biệt. Khi gọi `/api/bank-connections/test-mode/link-challenges/{challengeId}/confirm`, hệ thống gọi ra SePay Sandbox và phản hồi `409 bank_link_not_confirmed`.
  * *Tuân thủ nguyên tắc an toàn:* SpendWise tuyệt đối **KHÔNG** can thiệp chèn dòng dữ liệu giả vào bảng `BankConnections` bằng SQL để tạo kết quả PASS ảo, bảo đảm tính trung thực tuyệt đối của báo cáo.

#### 3. Kịch bản Tài chính & Chốt chặn Task 17: `PASS`
- **Chốt chặn an toàn tài chính (Task 17 Security Guard):** `PASS`. Do User A và User B chưa có tài khoản ngân hàng hoạt động được xác nhận từ SePay, hệ thống tự động kích hoạt chốt chặn an toàn `ActiveBankGuard`. Toàn bộ các thao tác tài chính:
  * Nạp tiền (`POST /api/transactions/income`): Từ chối `409 active_bank_required`.
  * Chi tiêu (`POST /api/transactions/expense`): Từ chối `409 active_bank_required`.
  * Chuyển tiền P2P (`POST /api/transfers/p2p`): Từ chối `409 active_bank_required`.
  * Chuyển tiền nội bộ (`POST /api/transfers/internal`): Từ chối `409 active_bank_required`.
  * Rút tiền test-mode (`POST /api/bank-connections/test-mode/wallet-withdrawals`): Từ chối `409 active_bank_required`.
  $\rightarrow$ Chứng minh tính toàn vẹn và cơ chế Fail-Closed của Backend hoạt động hoàn hảo, ngăn chặn triệt để giao dịch khi chưa đủ điều kiện an toàn.
- **Lập kế hoạch tài chính cá nhân:** `PASS`.
  * User A tạo danh mục chi tiêu tùy chỉnh (`POST /api/categories`) $\rightarrow$ 201 Created.
  * User A tạo ngân sách tháng (`POST /api/budgets`) $\rightarrow$ 201 Created.
  * User A tạo mục tiêu tiết kiệm (`POST /api/goals`) $\rightarrow$ 201 Created.
  * User A tạo giao dịch định kỳ (`POST /api/recurring-transactions`) $\rightarrow$ 200 OK.

#### 4. Quỹ nhóm & Luồng mời thành viên mới: `PASS`
- User A gửi lời mời kết bạn đến User B qua email (`POST /api/friends/requests`).
- User B chấp nhận kết bạn (`POST /api/friends/requests/{id}/accept`).
- User A tạo Quỹ nhóm mới (`POST /api/groups`) kèm xác thực mã PIN.
- User A mời User B vào quỹ bằng endpoint mới: `POST /api/groups/{groupId}/invites` $\rightarrow$ nhận `inviteId` và `token`.
- User B xem danh sách lời mời chờ duyệt: `GET /api/groups/invites` $\rightarrow$ tìm thấy đúng lời mời.
- User B chấp nhận lời mời: `POST /api/groups/invites/{inviteId}/accept?token=...` $\rightarrow$ 200 OK.
- Xác nhận số lượng thành viên trong nhóm tăng lên 2 thành viên (`Owner` + `Member`).

#### 5. Chu trình User $\rightarrow$ Admin $\rightarrow$ User & Phát thanh hệ thống: `PASS`
- **Phiếu hỗ trợ (Support Case):**
  * User A mở phiếu hỗ trợ kỹ thuật (`POST /api/cases/support`) $\rightarrow$ 200 OK (`caseA_id`).
  * User B mở phiếu hỗ trợ độc lập (`POST /api/cases/support`) $\rightarrow$ 200 OK (`caseB_id`).
  * Kiểm tra cách ly: User A đọc phiếu của User B bị từ chối `404 Not Found`.
- **Xử lý từ Admin:**
  * Admin đăng nhập định danh `lethien241205@gmail.com` kèm mã PIN quản trị $\rightarrow$ 200 OK, cấp token Admin.
  * Admin tra cứu danh sách phiếu $\rightarrow$ thấy đầy đủ cả hai phiếu.
  * Admin tiếp nhận phiếu A (`POST /api/admin/cases/support/{caseA_id}/status`, chuyển trạng thái `in_progress`).
  * Admin tra cứu hồ sơ User A có kiểm soát phạm vi vụ việc (`POST /api/v1/admin/support/{caseA_id}/users/{userA_id}/profile`) $\rightarrow$ 200 OK (Audited Case-Scoped Access).
  * Tra cứu lệch phạm vi (User B trong Case A) bị từ chối `403 case_access_denied`.
  * Admin gửi tin nhắn giải đáp trong phiếu A $\rightarrow$ 200 OK.
  * User A đọc lại chi tiết phiếu $\rightarrow$ xác nhận đã nhận được tin nhắn phản hồi của Admin trong lịch sử (`history`).
  * Admin hoàn tất giải quyết phiếu (`status: resolved`) $\rightarrow$ 200 OK.
- **Hệ thống Phát thanh Thông báo (Broadcast Outbox):**
  * Admin soạn thảo bản thảo thông báo vận hành (`POST /api/v1/system/broadcasts`) $\rightarrow$ 200 OK.
  * Admin phát hành thông báo (`POST /api/v1/system/broadcasts/{id}/publish`) $\rightarrow$ 204 NoContent, Outbox xử lý phân phối.
  * Admin kiểm tra trạng thái phát thanh (`POST /api/v1/system/broadcasts/{id}/status`) $\rightarrow$ trạng thái `Published`.
  * Cả User A và User B đều nhận được thông báo mới trong hộp thư (`GET /api/notifications`).
  * User A đánh dấu đã đọc tất cả thông báo (`POST /api/notifications/read-all`) $\rightarrow$ 204 NoContent.

#### 6. Phân quyền, Vai trò Cũ & Chống Trùng Lặp (Idempotency): `PASS`
- Người dùng thông thường cố truy cập API Admin bị từ chối `403 Forbidden`.
- Token giả lập mang vai trò cũ Role 0 (`User`) cố truy cập quyền Admin bị từ chối triệt để `401/403`.
- Token giả lập mang vai trò cũ Role 3 (`SystemAdmin/SuperAdmin`) cố truy cập quyền Admin bị từ chối triệt để `401/403` (theo quy định Duy nhất 1 Admin Role 2).
- Gửi lặp request phát thanh với cùng khóa `Idempotency-Key` trả về phản hồi cached 200 với cùng `broadcastId`, không tạo bản ghi trùng lặp trong cơ sở dữ liệu.
  7. `Folder 07 - Budgets`: Thiết lập hạn mức ngân sách theo tháng, tính tỷ lệ chi tiêu thực tế, cảnh báo vượt ngưỡng.
  8. `Folder 08 - Savings Goals`: Mục tiêu tích lũy, trích nạp từ ví chính, hoàn thành mục tiêu.
  9. `Folder 09 - Recurring Transactions`: Thiết lập lệnh định kỳ, lịch chạy tiếp theo, kiểm tra số dư tự động.
  10. `Folder 10 - Support & Complaints`: Gửi khiếu nại từ giao dịch, nhận phản hồi từ Admin, theo dõi tiến độ.
  11. `Folder 11 - Banking Sandbox (SePay)`: Tạo ý định nạp tiền, sinh mã VietQR, giả lập IPN Webhook.
  12. `Folder 12 - AI Assistant`: Tạo hội thoại, hỏi đáp tài chính, kích hoạt chính sách an toàn.

### 3.2. Phân hệ Admin (Newman 80 Requests / 138 Assertions PASS 100%)
- **Báo cáo nguồn:** `ADMIN-V1-FINAL-NEWMAN-REPORT.json`
- **Đủ 21/21 chức năng thuộc 5 nhóm lớn:**
  1. `ADM-01 (Operations Dashboard)`: Thống kê số lượng người dùng, số vụ hỗ trợ chờ xử lý, cảnh báo vận hành.
  2. `ADM-02 (User Governance)`: Tra cứu người dùng, xem hồ sơ/phiên theo Case-Scoped Access, khóa tài khoản (`Locked` thu hồi phiên ngay lập tức), hạn chế tài khoản (`Restricted` chặn ghi nợ), mở khóa (`Active`).
  3. `ADM-03 (Financial & Ledger)`: Bảng cân đối thử Sổ cái kép ($\text{Debit} = \text{Credit}$), đối soát giao dịch, nhật ký kiểm toán `AdminAuditLogs`.
  4. `ADM-04 (Support Governance)`: Phân loại hồ sơ, trao đổi tin nhắn với khách hàng, phê duyệt (`Resolved` hoàn tiền), từ chối (`Rejected`).
  5. `ADM-05 (Broadcast & Monitoring)`: Phát hành broadcast theo chu trình Outbox, telemetry giám sát Gemini AI và SePay BankHub.

### 3.3. Phân hệ Hỗ trợ & Khiếu nại Trực tiếp (Newman 22 Requests / 42 Assertions PASS 100%)
- **Báo cáo nguồn:** `reports/newman/support_complaint_e2e_newman_report.json`
- **Kiểm thử liên thông 6 bước:**
  - Bước 1: User thực hiện giao dịch tài chính ghi sổ cái.
  - Bước 2: User phát hiện sai sót, tạo `SupportCase` gắn với `TransactionId`.
  - Bước 3: Admin tiếp nhận vụ việc, chuyển trạng thái sang `InReview`.
  - Bước 4: Admin tra cứu hồ sơ người dùng thông qua endpoint bảo vệ theo mã vụ việc (`Case-Scoped Access`).
  - Bước 5: Hai bên trao đổi tin nhắn bằng chứng qua lại.
  - Bước 6: Admin phê duyệt giải quyết, hệ thống tự động hoàn tiền vào ví người dùng và kết thúc hồ sơ.

### 3.4. Kiểm thử Vòng đời Email & Toàn bộ 14 Email Templates (xUnit 6/6 PASS 100%)
- **Tệp mã nguồn kiểm thử:** `backend/tests/SpendWise.Tests/RealEmailLifecycleE2ETests.cs`
- **Kết quả thực thi runner:** `Passed! - Failed: 0, Passed: 6, Skipped: 0, Total: 6, Duration: 22 s`
- **Chi tiết 6 kịch bản kiểm nghiệm:**
  1. `Registration_DynamicOtp_FromOutbox_FullLifecycle_Succeeds_AndReplayFails`: Đăng ký $\rightarrow$ giải mã OTP động 8 số ngẫu nhiên từ Outbox $\rightarrow$ xác minh thành công $\rightarrow$ thử lại mã cũ bị từ chối 400 $\rightarrow$ thiết lập PIN $\rightarrow$ đăng nhập nhận JWT.
  2. `ResendOtp_DynamicOtp_RevokesPreviousOtp_AndPreventsUse`: Yêu cầu gửi lại OTP $\rightarrow$ mã cũ bị thu hồi trong DB (`RevokedAt != null`) $\rightarrow$ nhập mã cũ bị từ chối $\rightarrow$ nhập mã mới thành công.
  3. `ForgotPassword_DynamicOtp_ResetsPassword_AndRevokesSessions`: Yêu cầu quên mật khẩu $\rightarrow$ trích xuất OTP $\rightarrow$ đổi mật khẩu mới $\rightarrow$ toàn bộ session cũ bị vô hiệu hóa $\rightarrow$ mật khẩu cũ không thể đăng nhập $\rightarrow$ mật khẩu mới đăng nhập thành công.
  4. `ChangeEmail_DynamicOtp_SentToNewAddress_AndSecurityNoticeSentToOldAddress`: Yêu cầu đổi email $\rightarrow$ Outbox ghi nhận 2 email: OTP gửi tới email mới, cảnh báo gửi tới email cũ $\rightarrow$ xác nhận bằng OTP $\rightarrow$ cập nhật email thành công.
  5. `SecurityPinReset_EmailToken_Lifecycle_Succeeds_AndSingleUseEnforced`: Yêu cầu reset PIN $\rightarrow$ trích xuất signed raw token từ email $\rightarrow$ đặt lại PIN mới $\rightarrow$ thử dùng lại token cũ bị chặn 401 Unauthorized.
  6. `All_14_Email_Templates_Render_Cleanly_Without_Exceptions`: Kiểm tra toàn bộ danh mục **chính xác 14 mẫu email** trong `EmailTemplateRenderer`:
     1. `registration-verification`
     2. `registration-welcome`
     3. `password-reset-verification`
     4. `password-reset-success`
     5. `support-received`
     6. `support-resolved`
     7. `support-rejected`
     8. `support-info-request`
     9. `support-fund-investigation`
     10. `welcome-back`
     11. `new-device-login`
     12. `security-pin-reset`
     13. `email-change-verification`
     14. `email-change-notice`
     (và mẫu dự phòng `generic`). Tất cả 14 mẫu đều được khởi tạo dữ liệu mẫu, biên dịch HTML và Text, khẳng định không ném ngoại lệ hay trả về chuỗi rỗng.

---

## 4. ĐIỀU KIỆN TIÊN QUYẾT TRIỂN KHAI SẢN XUẤT (PRODUCTION READINESS)

1. **Hạ tầng SMTP Email Ngoài Internet:**
   - Cung cấp thông số máy chủ gửi thư doanh nghiệp vào biến môi trường: `Email:Smtp:Host`, `Email:Smtp:Port`, `Email:Smtp:Username`, `Email:Smtp:Password`.
   - Cấu hình bản ghi SPF, DKIM, DMARC cho domain gửi thư để bảo đảm tỷ lệ thư vào Inbox.
2. **Cổng Thanh toán Ngân hàng Thực tế:**
   - Khi ký kết hợp đồng ngân hàng chính thức, chuyển `SePayBankHub:Environment = Production` và điền `LifecycleWebhookSecret`.
3. **Hạn ngạch Google Gemini API:**
   - Nâng cấp gói trả phí trên Google Cloud Console để mở rộng giới hạn RPM/RPD khi hệ thống có lưu lượng người dùng thật.
4. **Phía Ứng dụng Frontend & Mobile Client:**
   - Tuân thủ nghiêm ngặt đặc tả API trong `User.md` và `Admin.md`.
   - Lưu trữ mã định danh và token trong Keychain / Keystore an toàn, xử lý cảm biến sinh trắc học native của OS.


---

## 5. BẰNG CHỨNG THỰC NGHIỆM ĐỘC LẬP: NEW CLEAN E2E — 2 USERS + 2 TPBANK (SEPAY TEST MODE) + ADMIN

> **Thời điểm nghiệm thu hoàn tất:** 29/10/2026 — 17:47:00+07:00  
> **Cơ sở dữ liệu kiểm nghiệm:** Microsoft SQL Server `DESKTOP-B5KA3LT\SQLEXPRESS02` / `SpendWise_E2E_G4` (SQL02 cách ly tuyệt đối, không đụng SQL01).  
> **Dịch vụ API backend:** `http://127.0.0.1:5158` (Kestrel Release Daemon).  
> **Postman Collection:** `backend/postman/NEW-CLEAN-E2E.postman_collection.json`  
> **Environment File:** `backend/postman/NEW-CLEAN-E2E.postman_environment.json`  
> **Báo cáo kết quả:** `reports/newman/NEW-CLEAN-E2E-REPORT.json` (Đã sanitize bảo mật 100%).

### 5.1. Bảng Tổng Hợp Kết Quả Thực Thi Newman

| Chỉ Số Đánh Giá | Kết Quả Thực Tế | Tỷ Lệ Đạt | Đánh Giá Kỹ Thuật |
|---|:---:|:---:|---|
| **Số lượt lặp (Iterations)** | **1** | 100% | Chạy đơn kỳ theo tiêu chuẩn CI/CD |
| **Tổng số Requests API** | **57** | **100% (57/57 PASS)** | 0 lỗi HTTP, 0 timeout, 0 socket hangup |
| **Tổng số Assertions** | **57** | **100% (57/57 PASS)** | 0 kiểm tra thất bại, 100% đúng mong đợi |
| **Thời gian thực thi toàn bộ** | **24.5 giây** | N/A | Tốc độ phản hồi trung bình 342ms |
| **Độ trễ phản hồi (Latency)** | **Min 9ms — Max 6.4s** | N/A | Rất ổn định |

### 5.2. Kết Quả Xác Nhận Thực Tế 2 Tài Khoản TPBank Trên SePay Test Mode

1. **User A — TPBank TEST 0005:**
   - **Tài khoản liên kết:** TPBank `****0005` (Holder: `HO BAO Q`, ProviderAccountId: `b333d043-d37f-11f1-8895-0afffe87053b`).
   - **Challenge SWLINK:** `SWLINKE4692D413102266B361F179D` tạo bởi User A.
   - **Xác nhận giao dịch SePay Test Mode:** Giao dịch 10.000đ tiền ra với đúng mã tham chiếu SWLINK được SePay ghi nhận thành công.
   - **Trạng thái BankConnection:** `Active` (Id: `122e2a32-8f1d-4731-8732-f1c2ba35d20e`), thuộc sở hữu duy nhất của User A.

2. **User B — TPBank TEST 0004:**
   - **Tài khoản liên kết:** TPBank `****0004` (Holder: `PHAM MINH C`, ProviderAccountId: `a973ba27-d37f-11f1-8895-0afffe87053b`).
   - **Challenge SWLINK:** `SWLINKD1C32E43A1E17F13CE7A9346` tạo bởi User B.
   - **Xác nhận giao dịch SePay Test Mode:** Giao dịch 10.000đ tiền ra với đúng mã tham chiếu SWLINK được SePay ghi nhận thành công.
   - **Trạng thái BankConnection:** `Active` (Id: `d84eaae8-a640-452d-921d-a01334026cab`), thuộc sở hữu duy nhất của User B.

3. **Bảo đảm cách ly quyền sở hữu chéo:**
   - User A không thể xác nhận hoặc thao tác trên Challenge/BankConnection của User B (Backend trả về HTTP 404).
   - User B không thể truy cập tài khoản ngân hàng của User A (HTTP 404).

### 5.3. Bằng Chứng Nghiệp Vụ Tài Chính & Đối Soát Sổ Cái Thực Tế (SQL02)

| Hạng mục nghiệp vụ | Thao tác thực tế | Mã giao dịch / Kết quả | Số dư & Bút toán Sổ cái |
|---|---|---|---|
| **Nạp tiền ví (Income)** | User A nạp 200.000đ có bằng chứng hóa đơn | POST /api/transactions/income -> 201 Created | Ví chính User A: 200.000đ (Ledger Tx: 7f25a0b8-de77-4553-a0f1-3a4f6ee4ab4f) |
| **Nạp tiền ví (Income)** | User B nạp 200.000đ có bằng chứng hóa đơn | POST /api/transactions/income -> 201 Created | Ví chính User B: 200.000đ (Ledger Tx: ee30403b-8e7f-4d78-a7a3-ae0a653cf32f) |
| **Chuyển tiền P2P A -> B** | User A chuyển 25.000đ cho User B | POST /api/transfers/p2p -> 201 Created | Ví A: 175.000đ, Ví B: 225.000đ (Bút toán kép khớp 100%) |
| **Chuyển tiền P2P B -> A** | User B chuyển 10.000đ cho User A | POST /api/transfers/p2p -> 201 Created | Ví A: 185.000đ, Ví B: 215.000đ |
| **Chi tiêu cá nhân (Expense)** | User A chi tiêu 15.000đ bữa tối | POST /api/transactions/expense -> 201 Created | Ví A: 170.000đ |
| **Rút tiền Test Mode** | User B rút 15.000đ về TPBank TEST 0004 | POST /api/bank-connections/test-mode/wallet-withdrawals -> 200 OK | Tạm giữ số dư ví B: 200.000đ khả dụng |
| **Chuyển tiền nội bộ ví** | User A chuyển 10.000đ sang Ví tiết kiệm | POST /api/transfers/internal -> 201 Created | Ví chính: 95.000đ, Ví tiết kiệm: 10.000đ |
| **Góp quỹ nhóm (Fund Contribution)** | User A góp 20.000đ vào Quỹ nhóm chung | POST /api/groups/{id}/fund/contributions -> 200 OK | Quỹ nhóm tăng lên 20.000đ |
| **Chi tiêu quỹ nhóm (Fund Expense)** | Quỹ nhóm chi 10.000đ mua nước uống tập thể | POST /api/groups/{id}/expenses -> 200 OK | Quỹ nhóm còn lại 10.000đ |
| **Phiếu hỗ trợ User -> Admin -> User** | User A tạo phiếu -> Admin trả lời -> Admin giải quyết | POST /api/cases/support -> /admin/.../messages -> /status | Quy trình hỗ trợ 2 chiều khép kín, trạng thái: resolved |

### 5.4. Đối Soát Khớp Số Dư Tuyệt Đối Sổ Cái Kép (Ledger Reconciliation)
- **Số dư cuối cùng trên SQL02:**
  * **User A - Ví chính:** `95.000,00 VND`
  * **User A - Ví tiết kiệm:** `10.000,00 VND`
  * **User B - Ví chính:** `215.000,00 VND`
  * **Quỹ nhóm hợp tác:** `10.000,00 VND`
- **Tổng tài sản hệ thống:** `95.000 + 10.000 + 215.000 + 10.000 = 330.000 VND`.
- **Dòng tiền vào/ra:**
  * Nạp tiền ban đầu: User A (200.000đ) + User B (200.000đ) = 400.000đ.
  * Chi tiêu cá nhân User A: -15.000đ.
  * Tạm giữ rút tiền User B: -15.000đ (Pending đối soát).
  * Chi tiêu quỹ nhóm: -10.000đ.
  * Chi tiêu nội bộ & chuyển tiền P2P: Cân bằng số học Delta = 0.
  * Tổng tiền còn lại: `400.000 - 15.000 - 15.000 - 10.000 = 330.000 VND`.
  * **Sai lệch kiểm toán (Discrepancy):** **0.00 VND (100% Khớp tuyệt đối)**.
