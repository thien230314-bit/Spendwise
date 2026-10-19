# SPENDWISE V1.0 — USER BACKEND SUBMISSION HANDOFF & CONTRACT

**Date:** 2026-10-19  
**Version:** V1.0 Final Submission  
**Baseline Commit:** `7b2691630e7c002114b97e54c01ddd767b567eea`  
**Quality Bar:** 1048/1048 PASS (0 FAIL, 0 SKIP across full isolated regression suite).

---

## 1. Hệ thống & Phân vai V1.0

Hệ thống SpendWise V1.0 được đóng băng với đúng 2 vai trò hoạt động:
1. **User (Mobile App / Expo React Native)**: Toàn bộ nghiệp vụ cá nhân, ví điện tử, quản lý giao dịch, chuyển tiền P2P, kết bạn, quỹ nhóm (Fixed & Free) và tạo khiếu nại/yêu cầu hỗ trợ theo `User.md` v1.3.
2. **Admin (Web Admin / SPA)**: Duy nhất **1 tài khoản Admin** trên toàn hệ thống quản trị 5 menu tinh gọn (ADM-01 Tổng quan, ADM-02 Người dùng, ADM-03 Khiếu nại & Yêu cầu, ADM-04 Thông báo, ADM-05 Bảo trì).
> [!IMPORTANT]
> **SuperAdmin hoàn toàn bị bãi bỏ khỏi phạm vi V1.0** theo quyết định khẩn cấp của Product Owner. Không còn giao diện SuperAdmin, không còn quy trình thăng cấp/chuyển cấp SuperAdmin. Chức năng bảo trì hệ thống được quản lý tập trung bởi Admin với xác thực Security PIN.

---

## 2. Bảng tổng hợp API User Mobile thực tế (User.md v1.3)

Tất cả endpoint đều được triển khai bằng dữ liệu thực (SQLite isolated fixtures & SQL Server migration ready), không có dữ liệu giả (mock data).

### 2.1. Xác thực & Tài khoản (Auth & Identity)
- `POST /api/v1/auth/register`: Đăng ký tài khoản User mới (yêu cầu Email hợp lệ, họ tên, mật khẩu tối thiểu 8 ký tự, tạo mã định danh cá nhân duy nhất `SpendId` dạng 8 ký tự).
- `POST /api/v1/auth/login`: Đăng nhập cấp Bearer JWT Access Token & Refresh Token.
- `POST /api/v1/auth/refresh`: Làm mới phiên làm việc.
- `POST /api/v1/auth/logout`: Đăng xuất và thu hồi Session.
- `POST /api/v1/auth/change-password`: Đổi mật khẩu đăng nhập.
- `POST /api/v1/auth/pin/setup`: Thiết lập mã PIN bảo mật giao dịch (6 chữ số băm SHA-256 an toàn).
- `POST /api/v1/auth/pin/verify`: Xác thực mã PIN trước khi thực hiện chuyển tiền/chi tiêu.

### 2.2. Ví SpendWise & Chuyển tiền (Wallets & Transfers)
- `GET /api/v1/wallets`: Lấy danh sách ví SpendWise của User (tối đa 1 Ví Chính và 2 Ví Phụ mục đích riêng).
- `POST /api/v1/wallets`: Mở thêm ví phụ (đặt tên ví, mục đích chi tiêu).
- `GET /api/v1/wallets/{walletId}/balance`: Xem số dư thực tế theo sổ cái (LedgerAccount).
- `POST /api/v1/transfers/internal`: Chuyển tiền giữa các ví của chính mình (từ Ví Chính sang Ví Phụ và ngược lại).
- `POST /api/v1/transfers/p2p`: Chuyển tiền P2P cho bạn bè qua `SpendId` hoặc Email (yêu cầu mã PIN giao dịch, tự động sinh bút toán kép Debit/Credit trên Ledger, chống số dư âm).

### 2.3. Bạn bè (Friends)
- `GET /api/v1/friends`: Danh sách bạn bè hiện tại (giới hạn tối đa 100 bạn bè theo quy định `User.md`).
- `POST /api/v1/friends/requests`: Gửi lời mời kết bạn bằng `SpendId` hoặc Email.
- `POST /api/v1/friends/requests/{requestId}/accept`: Chấp nhận lời mời kết bạn (thiết lập quan hệ bạn bè 2 chiều).
- `POST /api/v1/friends/requests/{requestId}/reject`: Từ chối lời mời kết bạn.
- `DELETE /api/v1/friends/{friendId}`: Hủy kết bạn.

### 2.4. Quỹ nhóm (Group Funds — Fixed & Free)
- `POST /api/v1/groups`: Tạo nhóm chi tiêu mới (chọn loại quỹ: `Fixed` — định mức cố định cho từng thành viên, hoặc `Free` — đóng góp tự do).
- `POST /api/v1/groups/{groupId}/members`: Mời bạn bè vào nhóm quỹ.
- `POST /api/v1/groups/{groupId}/contribute`: Nộp tiền vào quỹ nhóm từ ví cá nhân (ghi nhận bút toán tăng quỹ nhóm và giảm ví cá nhân).
- `POST /api/v1/groups/{groupId}/expenses`: Tạo khoản chi tiêu từ quỹ nhóm:
  - Khi nhóm bật **`CanSpend = ON`** (Chỉ Trưởng nhóm/Thủ quỹ được chi trực tiếp): Tiền được trừ trực tiếp từ số dư khả dụng của quỹ nhóm để thanh toán hóa đơn mà không cần chờ biểu quyết.
  - Khi nhóm có yêu cầu giải thể/hoàn tiền: Quy trình biểu quyết tự động hoàn trả số dư quỹ về ví cá nhân theo tỷ lệ đóng góp thực tế.

### 2.5. Khiếu nại & Hỗ trợ (Support & Disputes)
- `POST /api/v1/support/tickets`: Tạo yêu cầu hỗ trợ / khiếu nại giao dịch (gửi tiêu đề, mô tả, đính kèm mã giao dịch liên quan).
- `GET /api/v1/support/tickets`: Xem danh sách vé hỗ trợ của User và trạng thái xử lý (`Open`, `Investigating`, `Resolved`, `Closed`).
- `GET /api/v1/support/tickets/{ticketId}`: Xem chi tiết phản hồi từ Admin.
- `POST /api/v1/support/tickets/{ticketId}/messages`: User gửi bổ sung thông tin hoặc bằng chứng theo yêu cầu của Admin.

---

## 3. Ba Tích Hợp Bắt Buộc (Mandatory Integrations)

### 3.1. Email Outbox & Dispatcher
- **Cơ chế:** Lưu thông điệp email vào bảng `EmailOutboxMessages` đảm bảo tính tin cậy transactional, sau đó background worker `EmailOutboxDispatcher` gửi qua SMTP provider.
- **Trạng thái thực tế:** Sẵn sàng cấu hình SMTP thật (`Host`, `Port`, `Username`, `Password`, `Sender`). Khi chạy test/dev nội bộ không có SMTP server thực, dispatcher tự động ghi nhận trạng thái và xử lý retry an toàn với backoff.

### 3.2. Gemini AI Completion Provider
- **Cơ chế:** Phục vụ trợ lý tài chính thông minh, phân loại chi tiêu và gợi ý ngân sách.
- **Trạng thái thực tế:** Hỗ trợ cấu hình `Gemini:ApiKey` và `Gemini:Model`. Đã có provider fallback dự phòng khi không có Internet hoặc chưa nhập API Key, đảm bảo toàn bộ hệ sinh thái Backend và Mobile không bao giờ bị sập (0 unhandled exceptions).

### 3.3. SePay BankHub
- **Cơ chế:** Tích hợp nạp tiền qua cổng ngân hàng tự động (Bank QR / Webhook).
- **Trạng thái thực tế:** Đang hoạt động ở chế độ `TestMode` (môi trường kiểm thử có webhook verification signature). Các sự kiện nạp tiền được đối soát với số tham chiếu duy nhất, chống replay webhook và tự động ghi có vào Ví Chính của User.

---

## 4. Kịch bản Demo 2 Người Dùng & Quỹ Nhóm (Demo Scenarios)

### Kịch bản 1: Chuyển tiền P2P & Đối soát Ví
1. **User A (`userA@spendwise.local`)** đăng nhập trên Mobile, nạp tiền vào Ví Chính thông qua SePay TestMode (`+500,000 VND`).
2. **User A** kết bạn với **User B (`userB@spendwise.local`)** qua `SpendId`.
3. **User A** thực hiện lệnh chuyển P2P `200,000 VND` sang cho **User B** kèm lời nhắn "Tra tien an trua" và nhập PIN xác thực.
4. Hệ thống tạo giao dịch tài chính, ghi giảm Ví Chính User A và ghi tăng Ví Chính User B. Cả 2 User nhận thông báo In-App.

### Kịch bản 2: Quỹ nhóm Free Fund & Chi tiêu trực tiếp (`CanSpend = ON`)
1. **User A** tạo nhóm quỹ "Du Lich Vung Tau" với cơ chế **Quỹ Tự Do (Free Fund)** và kích hoạt quyền chi tiêu trực tiếp cho Trưởng nhóm (`CanSpend = ON`).
2. **User A** mời **User B** vào nhóm. Cả hai cùng nộp tiền vào quỹ nhóm (A nộp `500,000 VND`, B nộp `500,000 VND`, tổng quỹ = `1,000,000 VND`).
3. **User A** tạo khoản chi `400,000 VND` mua vé xe. Tiền lập tức được trừ trực tiếp từ quỹ nhóm về `600,000 VND` mà không bị gián đoạn hay đòi hỏi Admin can thiệp.

### Kịch bản 3: Khiếu nại & Admin xử lý (Dispute Resolution Flow)
1. **User B** khiếu nại về một giao dịch chuyển tiền bị gián đoạn bằng cách tạo Ticket hỗ trợ tại Mobile.
2. **Admin** đăng nhập Web Admin, truy cập Menu ADM-03 (Khiếu nại & Yêu cầu), mở Ticket của User B.
3. **Admin** tra cứu giao dịch trong phạm vi Case (A4), kiểm tra lịch sử gửi thông báo (A6), phản hồi yêu cầu User B gửi thêm ảnh biên lai.
4. **User B** nhận thông báo phản hồi và cung cấp thông tin. Admin cập nhật trạng thái Case sang `Resolved`.

---

## 5. Bảng Hướng Dẫn Cấu Hình Môi Trường (Appsettings & Placeholders)

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=SpendWiseDb;Trusted_Connection=True;TrustServerCertificate=True;"
  },
  "JwtSettings": {
    "Secret": "SpendWise_Secret_Key_For_Jwt_Signing_Must_Be_Long_Enough_2026",
    "Issuer": "SpendWise.API",
    "Audience": "SpendWise.App",
    "AccessTokenExpirationMinutes": 60,
    "RefreshTokenExpirationDays": 7
  },
  "EmailSettings": {
    "SmtpHost": "smtp.gmail.com",
    "SmtpPort": 587,
    "SenderEmail": "no-reply@spendwise.local",
    "SenderName": "SpendWise Support",
    "Username": "your-email@gmail.com",
    "Password": "your-app-password"
  },
  "Gemini": {
    "ApiKey": "AIzaSy_YOUR_GEMINI_API_KEY",
    "Model": "gemini-1.5-flash"
  },
  "SePay": {
    "ApiToken": "YOUR_SEPAY_API_TOKEN",
    "WebhookSecret": "YOUR_WEBHOOK_SECRET",
    "TestMode": true
  }
}
```

---

## 6. Kết Quả Kiểm Thử Nghiệm Thu (Verification Gates)

- **Release Build:** `dotnet build backend/SpendWise.slnx -c Release` -> **0 Errors, 16 Warnings (dependencies)**.
- **Admin Focused Suites:** 129 PASS / 0 FAIL / 0 SKIP.
- **User Core Suites (Auth, Wallet, Transfer, Friends, Group, Dispute):** 183 PASS / 0 FAIL / 0 SKIP.
- **Integrations Suite (Email, Gemini, SePay):** 187 PASS / 0 FAIL / 0 SKIP.
- **Full Non-SQL Regression:** **1048 PASS / 0 FAIL / 0 SKIP**.
- **Admin Hardening:** Đã xử lý triệt để 4 vấn đề ADM-FIX-01 đến ADM-FIX-04.
- **Bàn giao:** Đạt điều kiện bàn giao cho Frontend Web Admin và Mobile App.
