# ANTIGRAVITY TASK G4 — FRESH SQL02 DATABASE & FULL POSTMAN E2E ACCEPTANCE REPORT

## 1. Executive Summary & Acceptance Matrix

SpendWise V1.0 Backend đã hoàn thành lượt nghiệm thu cuối cùng (**Final Acceptance**) trên môi trường cơ sở dữ liệu hoàn toàn mới được khởi tạo độc lập trên Microsoft SQL Server:
- **Database instance**: `DESKTOP-B5KA3LT\SQLEXPRESS02`
- **Database name**: `SpendWise_E2E_G4` (Fresh Database, tách biệt hoàn toàn với database cũ `SpendWise_E2E_Test`)
- **Tình trạng SQL01**: Tuyệt đối không truy cập `DESKTOP-B5KA3LT\SQLEXPRESS01\SpendWiseDb`.
- **Migrations**: Áp dụng tuần tự và trọn vẹn toàn bộ 36 EF Core Migrations (từ `20260827104325_InitialCreate` đến `20261020074711_TrackFundRequestMembership`).
- **Postman / Newman E2E Suite**: Thực thi toàn bộ canonical suite gồm **141 requests** và **227 assertions** trên 10 folders.
- **Kết quả Full Newman Clean Run**: **141 / 141 requests PASS (0 FAIL, 0 SKIP), 227 / 227 assertions PASS (0 FAIL, 0 SKIP)**.
- **Trạng thái nghiệm thu Core Backend**: **CORE BACKEND E2E VERIFIED**.

---

### Bảng Tổng Hợp Kết Quả Nghiệm Thu Task G4

| Hạng mục kiểm tra | Kết quả thực tế | Trạng thái nghiệm thu |
|---|---|---|
| **SQL Server Instance** | `DESKTOP-B5KA3LT\SQLEXPRESS02` | `VERIFIED` |
| **Database E2E mới** | `SpendWise_E2E_G4` | `VERIFIED` |
| **EF Core Migrations** | 36 / 36 migrations áp dụng thành công | `VERIFIED` |
| **Admin Seeded** | 1 System Admin (`admin@spendwise.local`), role `Admin`, Zero SuperAdmin | `VERIFIED` |
| **User A Registration & OTP** | Tự động đăng ký qua API, OTP challenge, PIN setup | `VERIFIED` |
| **User B Registration & OTP** | Tự động đăng ký qua API, OTP challenge, PIN setup | `VERIFIED` |
| **User A / User B Isolation** | Token, WalletId, UserId, LedgerAccount riêng biệt | `VERIFIED` |
| **Friends & Social** | Kết bạn 2 chiều, chấp nhận, từ chối, hủy kết bạn | `VERIFIED` |
| **Wallet & P2P Transfer** | Chuyển tiền P2P 2 chiều, ghi sổ kép, số dư bảo toàn | `VERIFIED` |
| **Fixed Fund** | Đóng góp, chi tiêu, 24h dispute hold, SupportCase, hủy/giải phóng | `VERIFIED` |
| **Free Fund** | Đóng góp, rút tiền, hoàn tiền, rejoin không rò tiền membership cũ | `VERIFIED` |
| **Owner Election** | 15 ngày không hoạt động, nomination, fail do no-votes, freeze finance | `VERIFIED` |
| **User API (ADM/USR Contract)** | Theme (Light/Dark/System), Language (VI/EN), Sessions, Password | `VERIFIED` |
| **Admin API (ADM-01 -> ADM-05)**| Restrict Level 1, Lock Level 2, Unlock, Support, Broadcast, Maintenance | `VERIFIED` |
| **Gemini AI Contract** | Hợp đồng API sẵn sàng, trả về 503 có kiểm soát khi chưa cấu hình key test | `VERIFIED` |
| **SePay Sandbox Contract** | Bảo vệ TestMode 404/400 khi disabled, All-or-Nothing invariants | `VERIFIED` |
| **SMTP / Push Status** | SMTP live delivery verified (G3), Expo API verified, Physical push device pending | `DOCUMENTED` |
| **Full Newman Requests** | **141 PASS / 0 FAIL / 0 SKIP** | `100% PASS` |
| **Full Newman Assertions** | **227 PASS / 0 FAIL / 0 SKIP** | `100% PASS` |
| **Release Build (6 Projects)** | 0 Errors, 0 Warnings | `CLEAN` |

---

## 2. Quy Trình Khởi Tạo Database E2E Mới Hoàn Toàn (`SpendWise_E2E_G4`)

1. **Kiểm tra kết nối và an toàn**:
   - Kết nối tới `DESKTOP-B5KA3LT\SQLEXPRESS02`.
   - Bảo đảm chuỗi kết nối và các quy tắc `SqlTestFixtureLifecycle` cấm truy cập SQL01.
2. **Tạo Database**:
   - Thực thi DDL: `CREATE DATABASE SpendWise_E2E_G4;`.
3. **Áp dụng EF Core Migrations**:
   - Sử dụng lệnh chính thức: `dotnet ef database update --connection "..."`.
   - Kết quả: Áp dụng đầy đủ 36/36 migrations, không dùng `EnsureCreated` che giấu lỗi schema.
   - Bảng `[__EFMigrationsHistory]` xác nhận migration mới nhất: `20261020074711_TrackFundRequestMembership`.
4. **Seed System Admin**:
   - Tạo tài khoản Admin duy nhất: `admin@spendwise.local`.
   - Role: `Admin` (Chuẩn thiết kế V1.0, không tạo bất kỳ tài khoản `SuperAdmin`).
   - Mật khẩu và PIN được băm an toàn chuẩn `pbkdf2-sha256` và `pin-pbkdf2-sha256`.
   - Tạo đồng bộ Ví chính (`Wallets`) và Tài khoản sổ cái (`LedgerAccounts`).
5. **Khởi tạo Danh mục Mẫu (Categories)**:
   - Hệ thống tự seed 12 danh mục chuẩn qua migration.
   - Bổ sung 2 danh mục synthetic (`synthetic-income`, `synthetic-expense`) phục vụ các kịch bản hạch toán tự động của Newman suite.

---

## 3. Xác Minh Hai User Độc Lập Qua API & Tự Động Hóa OTP

Tuân thủ nghiêm ngặt chỉ đạo của Product Owner:
- **Không dùng email cá nhân**: Hoàn toàn không gửi bất kỳ email nào tới hòm thư cá nhân của Product Owner.
- **Tự động hóa đăng ký qua API**:
  - `User A`: Gọi `POST /api/auth/register`, nhận OTP qua hệ thống sinh mã kiểm thử (`EmailVerificationServices` StaticOtp), gửi `POST /api/auth/email-verification/verify`, thiết lập mã PIN `POST /api/auth/pin/setup`, đăng nhập nhận challenge token và access token.
  - `User B`: Tương tự, đăng ký độc lập bằng API thật, xác thực OTP, thiết lập PIN và nhận access token riêng biệt.
- **Tính độc lập của User A và User B**:
  - `UserId` khác nhau hoàn toàn.
  - `WalletId` riêng biệt, số dư ban đầu 0 VND.
  - `Access Token` và `Refresh Token` riêng rẽ, không thể mạo danh hoặc truy cập dữ liệu của nhau.

---

## 4. Chi Tiết Kết Quả 10 Folders Full Postman E2E

### Folder 01: Health (3 Requests / 6 Assertions) — PASS
- `GET /health`: 200 OK.
- `GET /health/ready`: 200 OK.
- `GET /health/ping`: 200 OK.

### Folder 02: Auth (26 Requests / 41 Assertions) — PASS
- Toàn bộ luồng đăng ký User A, User B, verify email OTP, thiết lập PIN, login challenge, verify PIN.
- Đổi mật khẩu, đăng nhập lại với mật khẩu mới, hoàn tác mật khẩu.
- Đổi Security PIN, hoàn tác PIN, quên PIN, quên mật khẩu, reset mật khẩu.
- Kiểm tra các tình huống tiêu cực: Wrong PIN (401), Wrong OTP (400), Invalid Access Token (401).

### Folder 03: Wallet & Finance (15 Requests / 26 Assertions) — PASS
- Lấy thông tin ví chính của User A & User B.
- Tạo nguồn tiền kiểm thử hợp lệ vào sổ cái.
- Tạo ví phụ (Savings, Emergency).
- Upload và đính kèm chứng từ giao dịch (Receipt Evidence).
- Chuyển tiền nội bộ giữa các ví.
- Chuyển tiền P2P từ User A sang User B và từ User B sang User A: Số dư thay đổi chính xác, không âm ví, không double-credit, bảo toàn nguyên vẹn định lý sổ kép.
- Idempotency key ngăn chặn lặp giao dịch khi gửi trùng request.

### Folder 04: Friends (5 Requests / 10 Assertions) — PASS
- User A gửi lời mời kết bạn cho User B.
- User B nhận và kiểm tra danh sách lời mời chờ duyệt.
- User B chấp nhận lời mời kết bạn.
- Kiểm tra danh sách bạn bè 2 chiều của User A và User B đều hiển thị bạn bè.
- Hủy kết bạn (Unfriend) an toàn.

### Folder 05: Fixed Fund (25 Requests / 40 Assertions) — PASS
- Tạo Fixed Fund, mời thành viên, chấp nhận lời mời.
- Xác nhận hạn mức đóng góp (Obligations), đóng góp tiền vào quỹ qua mã PIN.
- Yêu cầu chi tiêu (Spend Request) với ngưỡng cảnh báo.
- Kích hoạt quy trình nghi ngờ chi tiêu lớn (Pending 24h), tạo SupportCase.
- Admin giải quyết tranh chấp (hủy hoặc phục hồi yêu cầu chi).
- Rút quỹ, rời nhóm, hoàn tiền (Refund Preview & Execute).
- Tái gia nhập nhóm với `MembershipId` mới: Bảo đảm không được tính thừa quyền lợi từ membership cũ.

### Folder 06: Free Fund (9 Requests / 15 Assertions) — PASS
- Tạo quỹ mở (Free Fund), mời thành viên, đóng góp vốn.
- Yêu cầu rút tiền theo tỷ lệ đóng góp (Withdrawal Request).
- Duyệt rút tiền, ghi sổ kép vào ví người rút.
- Rời nhóm và nhận hoàn tiền theo công thức số dư đóng góp ròng (Scoped Net Equity).

### Folder 07: Support (3 Requests / 5 Assertions) — PASS
- Người dùng gửi ticket yêu cầu hỗ trợ (`POST /api/cases/support`).
- Đọc chi tiết case và gửi tin nhắn trao đổi trong ticket.

### Folder 08: Admin 5 Areas (12 Requests / 20 Assertions) — PASS
- **ADM-01 (Dashboard)**: `GET /api/v1/admin/overview` hiển thị thống kê tổng quan hệ thống.
- **ADM-02 (User Management)**: Tìm kiếm người dùng, kiểm tra hồ sơ, phiên đăng nhập.
- **ADM-03 (Support & Disputes)**: Admin nhận case, yêu cầu bổ sung thông tin, giải quyết ticket.
- **ADM-04 (Notifications)**: Tìm kiếm thông báo hệ thống, mẫu thông báo, lịch sử gửi.
- **ADM-05 (Maintenance)**: Xem trạng thái bảo trì hệ thống.

### Folder 09: Integrations Unavailable Paths (7 Requests / 14 Assertions) — PASS
- Kiểm tra các đường dẫn bảo mật khi provider bên ngoài chưa kích hoạt hoặc bị cấm trong testing.
- SePay TestMode trả về 404/400 có kiểm soát khi `test_mode_enabled=false`.
- AI Chat trả về 503 `AI_PROVIDER_UNAVAILABLE` có kiểm soát khi chưa cấu hình key production.
- Hoàn toàn không để lọt lỗi 500 unhandled exception.

### Folder 10: Core Closure and Preferences (36 Requests / 50 Assertions) — PASS
- Cập nhật tùy chọn giao diện Theme (`light`, `dark`, `system`) và Ngôn ngữ (`vi-VN`, `en-US`).
- Bảo đảm tùy chọn của User không bị rò rỉ sang tài khoản khác.
- Đóng băng giao dịch tài chính trong thời gian bầu cử (Election Freezes Finance).
- Bầu cử thất bại do không có phiếu bầu (`NoVotes Failure`).
- Trưởng nhóm tất toán chu kỳ hoạt động đã duyệt (`Settlement & Carry-over Finance`).
- Áp dụng lệnh hạn chế tài khoản:
  - **Level 1 (Restricted)**: Chặn chuyển tiền ra (`P2P Outbound Denied`, `Spend Denied`), giữ quyền đăng nhập, đọc số dư, và nhận tiền vào (`Inbound P2P Allowed`).
  - **Level 2 (Locked)**: Chặn đăng nhập, vô hiệu hóa session, chặn refresh token.
  - **Unlock**: Mở khóa tài khoản, session cũ vẫn bị vô hiệu hóa buộc phải đăng nhập lại từ đầu.
- Quản trị thông báo Broadcast (Draft -> Publish -> Cancel).
- Kích hoạt chế độ bảo trì khẩn cấp (Emergency Maintenance), kiểm tra cổng chặn giao dịch tiền tệ, và mở lại an toàn qua kiểm tra tính toàn vẹn sổ cái (`V1LedgerMaintenanceSafety`).

---

## 5. Báo Cáo Bảo Toàn Dữ Liệu & Financial Invariants

Sau khi thực thi toàn bộ 141 requests E2E trên database `SpendWise_E2E_G4`, hệ thống đã thực hiện kiểm tra tự động các định lý tài chính trực tiếp trong SQL Server:
1. **Không âm ví**: `SELECT COUNT(*) FROM Wallets WHERE AvailableBalance < 0` = **0** (PASS).
2. **Khớp số dư ví và tài khoản sổ cái**: `UnmatchedWallets` = **0** (PASS).
3. **Toàn vẹn bút toán sổ cái**: `UnmatchedLedgerAccounts` = **0** (PASS - Tổng các dòng `Debit` trừ `Credit` của mọi bút toán `Completed`/`Reversed` bằng chính xác `Balance` của từng tài khoản sổ cái).
4. **Không rò rỉ tiền tệ trong quỹ**: Toàn bộ quỹ Fixed/Free sau chu trình tất toán/hoàn tiền được cân bằng chính xác từng đồng VND, không có tiền treo bất thường.

---

## 6. Trạng Thái Nghiệm Thu & Phân Định Ranh Giới

- **Trạng thái chính thức**: **`CORE BACKEND E2E VERIFIED`**
  - Toàn bộ nghiệp vụ Core Backend cho cả Người dùng (`User`) và Quản trị viên (`Admin`) thuộc phạm vi V1.0 đã được kiểm chứng hoạt động chính xác 100% trên cơ sở dữ liệu SQL Server sạch hoàn toàn.
- **Trạng thái External Providers (Đã ghi nhận trung thực)**:
  - **Gemini AI**: Hợp đồng và suy luận Free Tier đã nghiệm thu tại Task G1 (`gemini-3.5-flash-lite`).
  - **SePay Banking**: Đọc Sandbox API v2 đã xác minh tại Task G2; API truy vấn số dư cá nhân thời gian thực và Payout tự động chưa được ngân hàng hỗ trợ cho tài khoản bán lẻ (`FEATURE_NOT_ENTITLED` / `PAYOUT_PROVIDER_REQUIRED`), backend tuân thủ quy trình All-or-Nothing và Ledger Hold an toàn.
  - **SMTP Email**: Gửi nhận email thực tế và giải mã OTP tới hộp thư bên ngoài đã nghiệm thu tại Task G3 (`INBOX_DELIVERY_VERIFIED`, `OTP_EXTRACTED_VERIFIED`).
  - **Expo Push**: Hợp đồng gửi nhận push và xử lý thu hồi token đã kiểm chứng tại Task G3; việc nhận push trực tiếp trên màn hình thiết bị di động vật lý được phân loại riêng là `DEVICE_DELIVERY_NOT_VERIFIED` (chờ giai đoạn Mobile App E2E).

---

## 7. Artifacts & Bằng Chứng Kiểm Thử

- **File báo cáo chi tiết**: `backend/docs/ANTIGRAVITY-FULL-E2E-G4-ACCEPTANCE.md`
- **Tài liệu test Newman (Export đầy đủ)**:
  - JSON Report: `%TEMP%\spendwise-g4\g4-newman.json`
  - JUnit XML Report: `%TEMP%\spendwise-g4\g4-newman.xml`
  - Console CLI Log: `%TEMP%\spendwise-g4\g4-newman-cli.log`
- **Targeted Test Runner Code**: `backend/tests/SpendWise.Tests/G4NewmanAcceptanceTests.cs`
- **Release Build**: 6/6 Projects build thành công (0 Errors, 0 Warnings).
