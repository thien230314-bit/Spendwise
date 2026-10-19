# SpendWise V1.0 — Postman / Newman Full E2E Test Suite

Bộ tài liệu và kịch bản kiểm thử tự động End-to-End (E2E) qua Postman và Newman phục vụ nghiệm thu toàn diện Backend SpendWise V1.0.

---

## 1. Cấu trúc thư mục

```
backend/postman/
├── SpendWise-V1-Full-E2E.postman_collection.json    # Kịch bản kiểm thử E2E đầy đủ (9 modules, 101 requests, 169 assertions)
├── SpendWise-V1-E2E.postman_environment.json        # Môi trường mẫu E2E
├── generate_postman_collection.py                   # Script sinh bộ kịch bản tự động
├── receipt.png                                      # Fixture chứng từ mẫu cho kiểm thử upload evidence
└── README.md                                        # Hướng dẫn chi tiết
```

---

## 2. Phạm vi kiểm thử (9 Modules — 101 Requests, 169 Assertions)

Bộ kịch bản kiểm thử đạt tỷ lệ thành công **100% (169/169 assertions PASS)**:

1. **01-Health (3 requests)**:
   - `GET /health/live`: Liveness probe (200 Healthy).
   - `GET /health/ready`: Readiness probe kiểm tra kết nối CSDL (200 Healthy).
   - `GET /health`: Health ping tổng quan (200 Healthy).
2. **02-Auth (24 requests)**:
   - Đăng ký tài khoản (Register) User Alpha và User Beta.
   - Xác thực Email bằng mã OTP tĩnh (`12345678`) trên môi trường Development/Testing.
   - Khởi tạo mã PIN bảo mật 6 số bắt buộc trước khi cấp phiên đầu tiên (`PIN_SETUP_REQUIRED`).
   - Đăng nhập 2 bước: Nhập mật khẩu nhận challenge -> Xác thực PIN nhận Access Token và Refresh Token.
   - Xoay vòng Refresh Token (Token Rotation).
   - Đổi mật khẩu với cơ chế thu hồi phiên và làm mới token an toàn.
   - Đổi mã PIN bảo mật và khôi phục mã PIN qua email.
   - Kiểm tra bảo mật tiêu cực (Negative checks): Mã PIN sai (401), Mã OTP sai (400/401), JWT giả mạo (401).
   - Khôi phục và đặt lại mật khẩu với Static OTP.
3. **03-Wallet & Finance (15 requests)**:
   - Truy vấn danh sách ví và danh mục thu/chi hệ thống.
   - Tạo các ví phụ (Savings, Emergency).
   - Kiểm tra ràng buộc âm bản: Tối đa 3 ví phụ cá nhân (tạo ví phụ thứ 4 trả về `409 Conflict`).
   - Tải file chứng từ hóa đơn lên `POST /api/evidence` (`receipt.png`).
   - Ghi nhận giao dịch Thu (Income) 10.000.000 đ kèm ID chứng từ.
   - Ghi nhận giao dịch Chi (Expense) 500.000 đ kèm ID chứng từ.
   - Chuyển tiền nội bộ giữa các ví cùng chủ sở hữu (Internal Transfer).
   - Chuyển tiền P2P giữa User A và User B kèm xác thực PIN và Idempotency Key.
   - Chống gửi lặp (Replay Protection): gửi lại request cùng key trả kết quả `replayed: true`.
   - Kiểm tra số dư không đủ (409 Conflict) và mã PIN sai (401 Unauthorized).
4. **04-Friends (5 requests)**:
   - Tìm kiếm bạn bè qua email/số điện thoại.
   - Gửi lời mời kết bạn (Friend Request).
   - Tiếp nhận và chấp nhận lời mời (Accept Friend Request).
   - Truy vấn danh sách bạn bè 2 chiều.
5. **05-Fixed Fund (21 requests)**:
   - Tạo quỹ Cố định mục tiêu 2.000.000 đ.
   - Mời User B vào quỹ và User B chấp thuận.
   - Chủ quỹ chốt nghĩa vụ đợt 1 (1.000.000 đ / người).
   - Thành viên đóng góp quỹ (User A và User B mỗi người 1.000.000 đ).
   - Kiểm tra quyền chi tiêu: Chặn chi tiêu khi `CanSpend = OFF` (`403 Forbidden`).
   - Bật quyền chi tiêu `CanSpend = ON` cho User B.
   - Chi tiêu trực tiếp dưới 20% ngưỡng hoàn tất ngay lập tức (`Completed`), tiền giải ngân về ví người nhận.
   - Chi tiêu lớn từ 20% trở lên vào trạng thái đệm 24h (`Pending24h`).
   - Tóm tắt tài chính quỹ (`/finance`).
   - Xem trước hoàn tiền Case B (Partially Spent).
   - User B rời quỹ và nhận hoàn tiền Case B về ví nội bộ.
   - Tạo quỹ Cố định 2, User A kick User B kèm hoàn tiền.
   - User A giải tán quỹ Cố định 2 với bất biến số dư quỹ về đúng 0 đ.
6. **06-Free Fund (9 requests)**:
   - Tạo quỹ Tự do (Free Fund).
   - Mời thành viên và tiếp nhận đóng góp (User A 700.000 đ, User B 300.000 đ).
   - Thành viên tạo yêu cầu rút tiền cá nhân (Withdrawal Request).
   - Chủ quỹ duyệt yêu cầu rút tiền: Sổ cái giải ngân tiền thật về ví của thành viên.
   - Xem trước hoàn tiền giải tán quỹ theo công thức Vốn ròng rạch ròi: $\text{NetEquity}_i = C_i - W_i - R_i$.
   - Giải tán quỹ (Dissolve Fund): Hoàn tiền nội bộ cho toàn bộ thành viên và kiểm tra bất biến số dư quỹ về đúng 0 đ.
7. **07-Support (3 requests)**:
   - Người dùng gửi yêu cầu hỗ trợ / khiếu nại (Support Ticket).
   - Người dùng tra cứu danh sách ticket và gửi tin nhắn trao đổi bổ sung.
8. **08-Admin 5 Areas (12 requests — Duy nhất 1 Admin)**:
   - Đăng nhập tài khoản Quản trị viên (`admin@spendwise.local`).
   - ADM-01: Tổng quan Dashboard & thống kê vận hành (`/api/v1/admin/overview`).
   - ADM-02: Quản lý người dùng, tìm kiếm tài khoản (`/api/v1/admin/users/search`).
   - ADM-03: Khiếu nại và Hỗ trợ (`/api/v1/admin/support/tickets/search`).
   - ADM-03: Điều phối viên tiếp nhận xử lý khiếu nại (`/assign`).
   - ADM-03: Yêu cầu người dùng bổ sung thông tin -> Trạng thái chuyển `WaitingForUser`.
   - Người dùng phản hồi thông tin bổ sung.
   - ADM-03: Admin giải quyết khiếu nại -> Trạng thái chuyển `Resolved`.
   - ADM-04: Tra cứu danh mục mẫu thông báo (`/api/v1/admin/notifications/templates`).
   - ADM-05: Kế hoạch và trạng thái bảo trì hệ thống (`/api/v1/system/maintenance`).
   - Kiểm tra an ninh: Token User thường truy cập khu vực Admin bị chặn `403 Forbidden`.
9. **09-Integrations (7 requests)**:
   - Kiểm tra danh sách kết nối ngân hàng.
   - Khởi tạo phiên liên kết ngân hàng SePay TestMode (`SWLINK...`).
   - Kiểm tra an toàn: Chặn xác nhận khi chưa có giao dịch hợp lệ trên sandbox (`409 bank_link_not_confirmed`).
   - Danh sách tài khoản ngân hàng liên kết.
   - Chặn nạp tiền khi chưa có kết nối hợp lệ (`404 bank_connection_not_found`).
   - Danh sách yêu cầu nạp tiền.
   - Tư vấn tài chính thông minh qua trợ lý AI Gemini (`gemini-1.5-flash`).

---

## 3. Hướng dẫn chạy kiểm thử với Newman

### Yêu cầu tiên quyết
1. Node.js (đã có `npx` hoặc cài sẵn `newman` toàn cục).
2. Backend API đang khởi chạy trên cổng `http://127.0.0.1:5159` kết nối database cô lập `SpendWise_E2E_Test` trên `DESKTOP-B5KA3LT\SQLEXPRESS02`.

### Khởi chạy API Server
```powershell
dotnet run --project backend/SpendWise/SpendWise.API.csproj --urls "http://127.0.0.1:5159" --ConnectionStrings:DefaultConnection "Server=DESKTOP-B5KA3LT\SQLEXPRESS02;Database=SpendWise_E2E_Test;Trusted_Connection=True;TrustServerCertificate=True;"
```

### Thực thi Newman
```powershell
npx --yes newman run backend/postman/SpendWise-V1-Full-E2E.postman_collection.json -e backend/postman/SpendWise-V1-E2E.local.json --reporters cli --delay-request 50
```
