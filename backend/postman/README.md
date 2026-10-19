# SpendWise V1.0 — Postman / Newman Full E2E Test Suite

Bộ tài liệu và tệp kiểm thử tự động End-to-End (E2E) qua Postman và Newman phục vụ nghiệm thu toàn diện Backend SpendWise V1.0.

---

## 1. Cấu trúc thư mục

```
backend/postman/
├── SpendWise-V1-Full-E2E.postman_collection.json    # Bộ kịch bản kiểm thử E2E đầy đủ (9 modules)
├── SpendWise-V1-E2E.postman_environment.json        # Môi trường E2E (URLs, test credentials)
├── generate_postman_collection.py                   # Script sinh bộ kịch bản tự động
└── README.md                                        # Hướng dẫn chi tiết
```

---

## 2. Phạm vi kiểm thử (9 Modules)

Bộ kịch bản kiểm thử 100% API theo hợp đồng nghiệp vụ V1.0:

1. **01-Health**: Kiểm tra trạng thái `/health/live`, `/health/ready`, `/health`.
2. **02-Auth (User A & B)**:
   - Đăng ký tài khoản (Register) User Alpha và User Beta.
   - Xác thực Email bằng mã OTP tĩnh (`12345678`) trên môi trường Development/Testing.
   - Khởi tạo mã PIN bảo mật 6 số bắt buộc trước khi cấp phiên đầu tiên.
   - Luồng Đăng nhập 2 bước: Nhập mật khẩu nhận challenge -> Xác thực PIN nhận Access Token và Refresh Token.
   - Cơ chế xoay vòng Refresh Token (Token Rotation).
3. **03-Wallet & Finance**:
   - Truy vấn danh sách ví và danh mục thu/chi hệ thống.
   - Tạo các ví phụ (Savings, Emergency).
   - Kiểm tra ràng buộc âm bản: Tối đa 3 ví phụ (tạo ví phụ thứ 4 trả về `409 Conflict`).
   - Ghi nhận giao dịch Thu (Income) và Chi (Expense).
   - Chuyển tiền nội bộ giữa các ví cùng chủ sở hữu (Internal Transfer).
   - Chuyển tiền P2P giữa User A và User B kèm xác thực PIN và Idempotency Key.
   - Kiểm tra chống gửi trùng (Replay Protection): gửi lại request cùng key trả kết quả `replayed: true`.
   - Kiểm tra mã PIN sai: trả về `400 Bad Request`.
4. **04-Friends**:
   - Tìm kiếm bạn bè qua email/số điện thoại.
   - Gửi lời mời kết bạn (Friend Request).
   - Tiếp nhận và chấp nhận lời mời (Accept Friend Request).
   - Truy vấn danh sách bạn bè 2 chiều.
5. **05-Fixed Fund (Nghiệp vụ Hoàn tiền Case A/B)**:
   - Tạo quỹ cố định (Fixed Fund).
   - Xác nhận nghĩa vụ đóng góp (Confirm Obligations).
   - Mời User B vào quỹ và User B chấp thuận.
   - Thành viên đóng góp quỹ (User A và User B mỗi người 1.000.000 đ).
   - Cấp quyền chi tiêu cho thành viên (`CanSpend` ON).
   - Truy vấn báo cáo tài chính quỹ (`/finance`).
   - Xem trước hoàn tiền (Refund Preview): Kiểm tra Case A (100% unspent, hoàn lại 1.000.000 đ).
   - Rời quỹ (Leave Fund): Tiền hoàn tự động ghi có vào ví SpendWise của User B với loại giao dịch `Refund`.
6. **06-Free Fund (Nghiệp vụ Hoàn tiền Case C/D & Chặn Rút Cá Nhân)**:
   - Tạo quỹ linh hoạt (Free Fund).
   - Mời thành viên và tiếp nhận đóng góp (User A 700.000 đ, User B 300.000 đ).
   - Thành viên tạo yêu cầu rút tiền (Withdrawal Request).
   - Chủ quỹ duyệt yêu cầu: Hệ thống giữ an toàn với trạng thái `ApprovedAwaitingFinanceContract` / chặn chi quỹ trực tiếp.
   - Xem trước hoàn tiền giải tán quỹ theo tỷ lệ đóng góp (70% - 30%).
   - Giải tán quỹ (Dissolve Fund): Hoàn tiền nội bộ cho toàn bộ thành viên và kiểm tra bất biến số dư quỹ về đúng 0 đ.
7. **07-Support**:
   - Người dùng gửi yêu cầu hỗ trợ / khiếu nại (Support Ticket).
   - Người dùng tra cứu danh sách ticket và gửi tin nhắn trao đổi.
8. **08-Admin 5 Areas (Duy nhất 1 Admin)**:
   - Đăng nhập tài khoản Quản trị viên (`admin@spendwise.local`).
   - ADM-01: Tổng quan Dashboard & thống kê vận hành (`/api/v1/admin/overview`).
   - ADM-02: Quản lý người dùng, tìm kiếm tài khoản (`/api/admin/users`).
   - ADM-03: Khiếu nại và Hỗ trợ (`/api/admin/cases/support`).
   - ADM-04: Tra cứu thông báo và lịch sử gửi (`/api/v1/admin/notifications/search`).
   - ADM-05: Kế hoạch và trạng thái bảo trì hệ thống (`/api/v1/system/maintenance`).
   - Kiểm tra an ninh: Token User thường truy cập khu vực Admin bị chặn `403 Forbidden`.
9. **09-Integrations**:
   - Trạng thái tích hợp ngân hàng / cổng SePay.
   - Truy vấn trợ lý AI tài chính (AI Financial Query) phản hồi nội dung tư vấn.

---

## 3. Hướng dẫn chạy kiểm thử với Newman

### Yêu cầu tiên quyết
1. Node.js (đã có `npx` hoặc cài sẵn `newman` toàn cục).
2. Backend API đang khởi chạy trên cổng `http://127.0.0.1:5159` kết nối database cô lập `SpendWise_E2E_Test` trên `DESKTOP-B5KA3LT\SQLEXPRESS02`.

### Lệnh khởi chạy API Server
```powershell
dotnet run --project backend/SpendWise/SpendWise.API.csproj --urls "http://127.0.0.1:5159" --ConnectionStrings:DefaultConnection "Server=DESKTOP-B5KA3LT\SQLEXPRESS02;Database=SpendWise_E2E_Test;Trusted_Connection=True;TrustServerCertificate=True;"
```

### Lệnh thực thi Newman
```powershell
npx --yes newman run backend/postman/SpendWise-V1-Full-E2E.postman_collection.json -e backend/postman/SpendWise-V1-E2E.postman_environment.json --reporters cli
```
