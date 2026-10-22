# USER MOBILE UI E2E TEST REPORT (GOOGLE AI STUDIO)

> **Môi trường thử nghiệm:** Google AI Studio Mobile Web Application  
> **Target URL:** `https://ai.studio/apps/e5db1617-9046-4fb0-b946-a9de726914b0?fullscreenApplet=true`  
> **Công nghệ Client:** React 19 / TypeScript / Vite / Tailwind CSS / LocalStorage Simulation (`spendwise_sim_store_v5`)  
> **Công cụ thực thi:** Chrome DevTools MCP (Tương tác trực tiếp trên trình duyệt Chrome thật)  
> **Thời điểm cập nhật:** 22/10/2026 12:45 - 12:52 (GMT+7)

---

## 1. Bằng chứng Thực thi Từng Luồng Nghiệp vụ (Before / Action / After)

### Luồng 1: Đăng ký Tài khoản Mới (Register Flow)
- **Trạng thái Trước (Before):** Màn hình tạo tài khoản SpendWise với các trường Họ tên, Email, SĐT, PIN 6 số, Mật khẩu.
- **Hành động (Action):** Điền `Tran Van Test`, `testuser@spendwise.local`, `0912999888`, PIN `123456`, Mật khẩu `Password123!` và nhấn nút "Tạo tài khoản".
- **Trạng thái Sau (After):** Tạo thành công tài khoản mới trong bộ mô phỏng `spendwise_sim_store_v5`, tự động đăng nhập và mở Trang chủ với SpendID `testuser`, số dư khả dụng mặc định `15.000.000 ₫`.
- **Kết luận:** **PASS (Mock Simulation)**. Lưu ý: Quy trình UI gộp chung PIN vào form và chưa có bước xác nhận email OTP.

### Luồng 2: Tạo Ví Cá Nhân Mới (Wallet Creation)
- **Trạng thái Trước (Before):** Tab Ví chỉ có 1 ví mặc định "Ví Chính SpendWise" (Số dư: `15.000.000 ₫`).
- **Hành động (Action):** Nhấn "Thêm ví" → Điền tên ví "Ví Tiền Mặt Khẩn Cấp", chọn loại Ví chi tiêu, nhập số dư ban đầu `2.000.000 ₫` → Nhấn "Tạo ví ngay".
- **Trạng thái Sau (After):** Modal đóng lại, danh sách ví cập nhật thành 2 ví, tổng số dư khả dụng tăng lên `17.000.000 ₫`. Ví mới xuất hiện với tên "Ví Tiền Mặt Khẩn Cấp" và số dư `2.000.000 ₫`.
- **Kết luận:** **PASS (Mock Simulation)**.

### Luồng 3: Quy trình Đổi Email 2 Bước (Change Email Flow)
- **Trạng thái Trước (Before):** Màn Cá nhân hiển thị email tài khoản là `testuser@spendwise.local`.
- **Hành động (Action):** 
  1. Nhấn nút "Đổi email" → Hộp thoại Bước 1 mở ra.
  2. Điền email mới: `tranvantest.new@spendwise.local` → Nhấn "Nhận mã OTP (8 số)".
  3. Hộp thoại chuyển sang Bước 2: Hiển thị thông báo mã OTP đã gửi đến email mới, có đồng hồ đếm ngược 60s.
  4. Nhập mã OTP `12345678` → Nhấn "Xác nhận đổi Email".
- **Trạng thái Sau (After):** Modal đóng lại, màn hình Cá nhân cập nhật email hiển thị thành công sang `tranvantest.new@spendwise.local`.
- **Kết luận:** **PASS (Mock Simulation)**. Giao diện và luồng OTP 8 số hoạt động rất mượt mà.

### Luồng 4: Tra cứu Danh bạ & Vòng đời Kết bạn (Friend Request Lifecycle)
- **Trạng thái Trước (Before):** Tab Bạn bè trống (0 bạn bè, 0 đã gửi, 0 đã nhận).
- **Hành động (Action):**
  1. Nhập số điện thoại `0912345678` vào ô tìm kiếm.
  2. Kết quả hiển thị thẻ người dùng "Lê Hoàng Nam (0912 345 678)" kèm nút "Kết bạn".
  3. Nhấn "Kết bạn" → Nút chuyển sang "Hủy lời mời", badge hiển thị "Đã gửi", tab "Đã gửi · 1".
  4. Nhấn "Hủy lời mời" → Trạng thái đảo ngược về nút "Kết bạn", tab "Đã gửi · 0".
- **Trạng thái Sau (After):** Toàn bộ vòng đời Send Request → Cancel Request diễn ra chính xác.
- **Kết luận:** **PASS (Mock Simulation)**.

### Luồng 5: Kiểm tra Quỹ Cố định (Fixed Fund) vs Quỹ Tự do (Free Fund)
- **Quỹ cố định ("Quỹ Du Lịch Phú Quốc 2026"):**
  - Hiển thị thanh tiến độ 73% (11.000.000 ₫ / 20.000.000 ₫).
  - Hiển thị "HẠN MỨC 20% CÒN LẠI: 4.000.000 ₫" và quy tắc khóa 24 giờ.
  - Tab Thành viên có nút chuyển đổi quyền chi tiêu (CanSpend) cho từng thành viên.
- **Quỹ tự do ("Quỹ Ăn Trưa Văn Phòng"):**
  - Hiển thị không có TargetVnd, góp linh hoạt.
  - Nút chính là "Yêu cầu rút" (thay vì Chi trực tiếp).
  - Tab "Duyệt (1)" hiển thị khoản chi 1.650.000 ₫ đang chờ Chủ nhóm (Trần Bảo Ngọc) phê duyệt.
- **Kết luận:** **PASS (Mock Simulation)**. Cả hai mô hình quỹ đều thể hiện đúng bản chất nghiệp vụ Backend.

### Luồng 6: Yêu cầu Hỗ trợ & Đối soát (Support Cases)
- **Trạng thái Trước (Before):** Trong mục Trợ giúp, nhấn "Yêu cầu hỗ trợ của tôi".
- **Hành động (Action):** Mở màn hình danh sách Case, hiển thị ticket `#SW-SUP-261018` tiêu đề "Đối soát biên lai nạp tiền từ Techcombank".
- **Trạng thái Sau (After):** Xem chi tiết ticket, trạng thái "Đang xử lý" và đóng modal quay lại.
- **Kết luận:** **PASS (Mock Simulation)**.

---

## 2. Tổng kết Trạng thái Kỹ thuật
- **Console Errors:** 0 lỗi cú pháp hoặc crash React runtime. Chỉ có các cảnh báo standard accessibility HTML form.
- **Network Activities:** 0 cuộc gọi mạng ra ngoài server ASP.NET Core; dữ liệu đọc/ghi vào `localStorage.spendwise_sim_store_v5`.
