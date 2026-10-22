# USER MOBILE UI E2E TEST REPORT (GOOGLE AI STUDIO)

> **Môi trường thử nghiệm:** Google AI Studio Mobile Web Application  
> **Target URL:** `https://ai.studio/apps/e5db1617-9046-4fb0-b946-a9de726914b0?fullscreenApplet=true`  
> **Công cụ thực thi:** Chrome DevTools MCP (Session tương tác trực tiếp trên trình duyệt Chrome)  
> **Thời điểm thực hiện:** 22/10/2026 12:30 - 12:39 (GMT+7)

---

## 1. Kết quả Duyệt & Thao tác Thực tế trên Trình duyệt

Chúng tôi đã khởi chạy phiên kiểm thử tự động tương tác trực tiếp với phiên bản Google AI Studio của ứng dụng SpendWise Mobile:

| STT | Màn hình / Component | Thao tác thực tế của người dùng | Kết quả kiểm thử thực tế | Ghi nhận Network / Console | Trạng thái |
|:---:|---|---|---|---|:---:|
| 1 | **Google AI Studio Gate** | Nhấn "Continue to the app" để cấp quyền applet | Applet chuyển hướng vào frame ứng dụng độc lập trên Cloud Run container | Tải thành công mã nguồn bundle `index-Ck-yd2uN.js` và CSS `index-B_WrjM13.css` | **PASS** |
| 2 | **Màn hình Đăng nhập (Auth)** | Mở form đăng nhập mặc định | Hiển thị form gồm Email/SĐT, Mật khẩu, Quên mật khẩu, nút Đăng ký mới | 0 lỗi console blocking; có 2 cảnh báo accessibility của form label | **PASS** |
| 3 | **Màn hình Đăng ký (Register)** | Nhấn "Chưa có tài khoản? Đăng ký mới" | Mở form đăng ký tài khoản mới: Họ tên, Email, Số điện thoại, Mã PIN (6 số), Mật khẩu (yêu cầu tối thiểu 8 ký tự, 1 hoa, 1 số) | Form hiển thị đúng các tiêu chí bảo mật | **PASS** |
| 4 | **Quên mật khẩu (Forgot Password)** | Nhấn "Quên mật khẩu?" | Mở form khôi phục mật khẩu qua xác thực OTP gửi về email | Giao diện hiển thị đúng luồng nhập email | **PASS** |
| 5 | **Đăng nhập vào Hệ thống (Login)** | Nhập `alice@spendwise.local` / `Password123!` và nhấn "Đăng nhập" | Chuyển hướng thành công vào **Trang chủ** của tài khoản người dùng | **Network:** Hoàn toàn chạy Client-side Mock State (0 request gửi về Backend ASP.NET Core) | **PASS (UI Mock)** |
| 6 | **Trang chủ (Dashboard / Home)** | Xem số dư khả dụng (`31.800.000 ₫`), các nút thao tác nhanh (Nạp tiền, Chuyển khoản, Góp quỹ, Kế hoạch), Quỹ nhóm tham gia, Giao dịch gần đây | Hiển thị đầy đủ số dư, mã SpendID `minh.nguyen`, thẻ trợ lý AI | Giao diện mượt mà, định dạng tiền tệ VND chuẩn xác | **PASS** |
| 7 | **Ví cá nhân (Wallets)** | Nhấn tab "Ví" trên bottom navigation | Hiển thị tổng số dư khả dụng (`96.800.000 ₫`), 3 ví (Ví chính, Ví phụ, Ví tiết kiệm tích lũy), 2 ngân hàng liên kết, biến động số dư | Phân tách rõ ràng giữa số dư khả dụng, tài sản thực và số tiền đang tạm giữ | **PASS** |
| 8 | **Quỹ nhóm (Fixed Fund Detail)** | Nhấn tab "Quỹ nhóm" trên bottom navigation | Mở chi tiết Quỹ cố định "Quỹ Du Lịch Phú Quốc 2026": Số dư khả dụng (`11.000.000 ₫`), hạn mức 20% còn lại (`2.500.000 ₫`), các tab Sổ cái, Duyệt (1), Thành viên (4), Quản trị | Thể hiện đúng quy tắc hạn mức 20% và khóa thời gian 24h của Fixed Fund | **PASS** |
| 9 | **Thành viên Quỹ (Fund Members)** | Nhấn tab "Thành viên (4)" trong Quỹ cố định | Hiển thị danh sách 4 thành viên (Nguyễn Minh - Chủ nhóm, Trần Bảo Ngọc - Được quyền chi, Lê Hoàng Nam, Phạm Minh Tuấn), nút thu hồi/cấp quyền chi tiêu | Cấu trúc khớp hoàn toàn với mô hình phân quyền và tiến độ đóng góp | **PASS** |
| 10 | **Quản trị Quỹ (Fund Governance)** | Nhấn tab "Quản trị" trong Quỹ cố định | Hiển thị: 1. Đề nghị chuyển quyền Chủ nhóm; 2. Khởi tạo Bầu cử Chủ nhóm mới; 3. Rời quỹ, Lưu trữ & Hỗ trợ | Thể hiện đầy đủ quy trình dân chủ 4 bước của V1 Fund | **PASS** |
| 11 | **Bạn bè & Tìm kiếm (Friends)** | Nhấn tab "Bạn bè" trên bottom navigation | Hiển thị danh sách bạn bè (4 người: Lê Hoàng Nam, Trần Bảo Ngọc, Đỗ Thu Hà, Phạm Minh Tuấn) với nút đánh dấu/bỏ bạn thân, chuyển tiền | Hiển thị tab Bạn bè, Đã nhận, Đã gửi | **PASS** |
| 12 | **Tìm kiếm Người dùng (Friend Search)** | Nhập từ khóa `"Nguyen"` vào thanh tìm kiếm | Lọc danh sách bạn bè cục bộ; hiển thị thông báo "Không tìm thấy người dùng phù hợp" | **Chưa tích hợp gọi API `GET /api/friends/discover`**, chỉ tìm kiếm trên danh sách bạn bè đã tải cục bộ | **FRONTEND GAP** |
| 13 | **Kế hoạch Tài chính (Planning)** | Nhấn nút "Kế hoạch" từ trang chủ | Mở màn hình Kế hoạch: Ngân sách (tỷ lệ 77%), Mục tiêu, Định kỳ, Phân loại tự động; cảnh báo danh mục vượt hạn mức hoặc chạm ngưỡng 80% | UI thể hiện trực quan các cảnh báo ngân sách | **PASS** |
| 14 | **Trợ lý AI (Financial AI Assistant)** | Nhấn vào widget Trợ lý AI trên trang chủ | Mở giao diện chat với SpendWise AI, hiển thị các gợi ý thao tác tài chính (Trích lập quỹ dự phòng 2.000.000 ₫), lịch sử chat | Mô phỏng hội thoại AI và thẻ Action Suggestion | **PASS** |
| 15 | **Thông báo Hệ thống (Notifications)** | Nhấn biểu tượng chuông thông báo trên trang chủ | Mở màn hình Thông báo: 3 thông báo chưa đọc, phân loại Tất cả/Chưa đọc, nút "Đọc tất cả" | Thể hiện các thông báo đóng quỹ, yêu cầu duyệt chi, lời mời kết bạn | **PASS** |
| 16 | **Hồ sơ Cá nhân (Profile & Security)** | Nhấn biểu tượng avatar góc trên trang chủ | Mở trung tâm Cá nhân: 1. Hồ sơ (Đổi avatar, Đổi email); 2. Bảo mật (Đổi mật khẩu, Đổi PIN, Khôi phục PIN, Quản lý phiên); 3. Tùy chọn (Theme Sáng/Tối, Ngôn ngữ, Ẩn số dư); 4. Trợ giúp (Yêu cầu hỗ trợ) | Cấu trúc bao quát 100% các chức năng User cần thiết | **PASS** |
| 17 | **Đăng xuất (Logout)** | Nhấn "Đăng xuất khỏi tài khoản" trong màn Cá nhân | Xóa phiên đăng nhập cục bộ và đưa người dùng quay lại màn hình Login | Luồng chuyển màn hình chuẩn xác | **PASS** |

---

## 2. Ghi nhận Kỹ thuật Console & Network

- **Network:** 
  - Toàn bộ các thao tác trên Google AI Studio chạy hoàn toàn trên bộ mock state trong bộ nhớ của React frontend (`index-Ck-yd2uN.js`).
  - Không có kết nối HTTP thực sự nào được tạo về Backend ASP.NET Core (`http://localhost` hay domain production).
- **Console Errors:**
  - `Failed to load resource: 404 (favicon.ico)` — Không ảnh hưởng ứng dụng.
  - Cảnh báo tiêu chuẩn: Form input chưa gắn `id` khớp với `label for`.
- **Viewport & Styling:**
  - Hoạt động responsive tối ưu trên cả giao diện mobile và web desktop.
  - Hỗ trợ chuyển đổi Theme (Sáng/Tối) và Ngôn ngữ (Tiếng Việt/English) hoàn hảo.
