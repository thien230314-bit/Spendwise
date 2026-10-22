# USER BACKEND ↔ FRONTEND GAP ANALYSIS MATRIX (TOÀN DIỆN)

> **Baseline Backend:** ASP.NET Core 10 / SQL Server (`b5e1dcbd5ba425057814257818936873daefc390`)  
> **Target Frontend:** Google AI Studio Mobile Web Application (Client-side Simulation)  
> **Cơ sở Phân loại Đánh giá:**  
> - `MATCH`: Giao diện và logic mô phỏng của Frontend khớp hoàn toàn với quy chuẩn Backend.  
> - `FRONTEND_WRONG`: Giao diện có sẵn nhưng mô phỏng sai quy trình, điều kiện bảo mật hoặc logic nghiệp vụ Backend.  
> - `FRONTEND_MISSING`: Backend đã có API và quy chuẩn nhưng Frontend chưa có chức năng hoặc chưa gọi API.  
> - `BACKEND_MISSING`: Frontend yêu cầu chức năng mà Backend chưa hỗ trợ (hiện tại: 0 mục).  
> - `UI_ONLY`: Các hiệu ứng animation, sound, theme cục bộ không phụ thuộc Backend.

---

## 1. Ma trận Kiểm kê Chi tiết 32 Nhóm Chức năng & Hành động User

| ID | Nhóm Chức năng | Hành động của Người dùng | Hành vi Thực tế trên Frontend AI Studio | Endpoint & Nghiệp vụ Backend tương ứng | Hợp đồng / Kỳ vọng | Kết quả Đánh giá | Mức độ Ưu tiên | Bằng chứng Kiểm thử (DevTools / Code) |
|:---:|---|---|---|---|---|:---:|:---:|---|
| **A1** | Auth | Đăng nhập bằng Email & Mật khẩu | Nhập email/pass hợp lệ → Vào thẳng Dashboard ngay lập tức | `POST /api/auth/login` → `POST /api/auth/pin/verify` | Nếu User có PIN, `login` trả về challenge; bắt buộc qua `pin/verify` mới cấp full JWT | `FRONTEND_WRONG` | **P0** | DevTools Snapshot: Form login bỏ qua bước nhập PIN challenge |
| **A2** | Auth | Đăng ký tài khoản mới | Form gộp chung Họ tên, Email, SĐT, PIN 6 số, Mật khẩu trên 1 màn hình duy nhất | `POST /api/auth/register`<br>`POST /api/auth/email-verification/verify`<br>`POST /api/auth/pin/setup` | Đăng ký 3 bước rời: Nhập thông tin → Xác thực OTP qua email → Cài đặt mã PIN lần đầu | `FRONTEND_WRONG` | **P1** | DevTools Snapshot: Form tạo tài khoản gộp PIN và không có bước xác thực OTP email |
| **A3** | Auth | Khôi phục mật khẩu khi quên | Mở popup nhập email nhận OTP | `POST /api/auth/forgot-password`<br>`POST /api/auth/reset-password` | Gửi OTP qua email → Xác thực OTP và đặt mật khẩu mới | `MATCH` | Minor | Đã test form khôi phục mật khẩu OTP |
| **A4** | Auth | Đăng xuất (Logout) | Nhấn nút đăng xuất trong mục Cá nhân | `POST /api/auth/logout` | Thu hồi Access Token và Refresh Token của session | `MATCH` | Minor | Đã test: Xóa auth state và đưa người dùng về Login screen |
| **B1** | Profile | Xem hồ sơ cá nhân | Hiển thị Họ tên, Email, SpendID, STK ảo | `GET /api/users/me` | Trả về `UserProfile` (`FullName`, `Email`, `Phone`, `SpendId`, `AvatarUrl`) | `MATCH` | Minor | Đã test: Màn Cá nhân hiển thị đầy đủ thông tin định danh |
| **B2** | Profile | Đổi ảnh đại diện (Avatar) | Nhấn "Đổi ảnh đại diện" | `POST /api/users/me/avatar` | Upload multipart `file`, type png/jpeg/webp <= 5MB, trả về `UserProfile` cập nhật | `MATCH` | Minor | Nút và thông tin hướng dẫn rõ ràng |
| **B3** | Profile | Đổi Email qua 2 bước OTP | Nhập email mới → Nhận OTP 8 số → Nhập OTP xác thực | `POST /api/users/me/email/request`<br>`POST /api/users/me/email/confirm` | Bước 1 gửi OTP; Bước 2 gửi `{ "otp": "..." }` xác thực nguyên tử | `MATCH` | Minor | **Đã test thành công trên DevTools**: Email đổi từ `testuser@` sang `tranvantest.new@` |
| **B4** | Security | Đổi / Khôi phục PIN giao dịch | Nhấn đổi PIN hoặc khôi phục PIN | `POST /api/auth/pin/reset` | Cần xác thực mật khẩu hoặc OTP trước khi lưu hash PIN mới | `MATCH` | Minor | Hiển thị trạng thái "Đã thiết lập" và form khôi phục |
| **B5** | Security | Quản lý thiết bị & Phiên | Nhấn xem phiên đăng nhập | `GET /api/auth/sessions`<br>`DELETE /api/auth/sessions/{id}` | Liệt kê các session và cho phép thu hồi từ xa | `MATCH` | Minor | Có sẵn màn hình quản lý phiên |
| **B6** | Preferences | Đổi giao diện, ngôn ngữ, ẩn số dư | Chuyển Sáng/Tối, Tiếng Việt/English, che số tiền | `PUT /api/users/me/preferences` | Lưu cài đặt người dùng vào profile | `MATCH` | Minor | **Đã test**: Chuyển tab theme/ngôn ngữ hoạt động tức thì |
| **C1** | Friends | Xem danh sách bạn bè | Hiển thị danh sách bạn bè kèm tag Bạn thân | `GET /api/friends` | Trả về `IReadOnlyList<FriendDto>` gồm `friendsSince`, `avatarUrl`, `isFavorite` | `MATCH` | Minor | Khớp hoàn toàn cấu trúc DTO |
| **C2** | Friends | Đánh dấu bạn thân / Biệt danh | Nhấn icon ngôi sao trên danh thiếp bạn | `PATCH /api/friends/{id}/preference` | Body `{ "nickname": "...", "isFavorite": true/false }` | `MATCH` | Minor | Đã test: Bật/tắt trạng thái bạn thân |
| **C3** | Friends | Tìm kiếm người dùng mới theo tên | Gõ tên người lạ ("Nguyen") vào ô tìm kiếm | `GET /api/friends/discover?query=...` | Gọi `discover` với pagination, trả `PagedResult<UserDiscoveryDto>` | `FRONTEND_MISSING` | **P0** | **Đã test**: Hiện chỉ lọc trên mảng bạn bè nội bộ, chưa kích hoạt API gọi tìm người mới |
| **C4** | Friends | Tra cứu danh bạ chính xác theo SĐT | Nhập số điện thoại `0912345678` | `GET /api/friends/search?contact=...&kind=phone` | Trả về `FriendContactDto` | `MATCH` | Minor | **Đã test thành công trên DevTools**: Tìm ra người dùng "Lê Hoàng Nam" |
| **C5** | Friends | Gửi lời mời kết bạn | Nhấn nút "Kết bạn" sau khi tìm | `POST /api/friends/requests` | Body `{ "contact": "..." }`, tạo pending request | `MATCH` | Minor | **Đã test thành công trên DevTools**: Trạng thái đổi sang "Đã gửi" |
| **C6** | Friends | Hủy lời mời kết bạn đã gửi | Nhấn nút "Hủy lời mời" | `DELETE /api/friends/requests/{requestId}` | Xóa pending request | `MATCH` | Minor | **Đã test thành công trên DevTools**: Hủy lời mời và nút đổi lại thành "Kết bạn" |
| **C7** | Friends | Chấp nhận / Từ chối lời mời đến | Nhấn Chấp nhận trong tab Đã nhận | `POST /api/friends/requests/{requestId}/accept` | Chuyển request sang Accepted, tạo quan hệ Friends | `MATCH` | Minor | Giao diện đã có tab "Đã nhận" |
| **D1** | Wallets | Xem danh sách ví cá nhân | Hiển thị Ví chính, Ví phụ và số dư | `GET /api/wallets` | Trả về danh sách `WalletDto` | `MATCH` | Minor | **Đã test**: Hiển thị danh sách ví và tổng số dư |
| **D2** | Wallets | Tạo ví cá nhân mới | Nhấn "Thêm ví" → Nhập tên ví, số dư ban đầu | `POST /api/wallets` | Tạo bản ghi ví trong database | `MATCH` | Minor | **Đã test thành công trên DevTools**: Tạo thêm "Ví Tiền Mặt Khẩn Cấp" (2.000.000 ₫) |
| **D3** | Wallets | Điều chuyển nội bộ giữa các ví | Nhấn "Giữa các ví" → Chọn ví nguồn, ví đích | `POST /api/transfers/internal` | Cập nhật số dư 2 ví trong cùng 1 transaction | `MATCH` | Minor | **Đã test**: Form điều chuyển hiển thị đúng số dư khả dụng |
| **D4** | Wallets | Chuyển tiền bạn bè (P2P) | Nhấn nút "Chuyển tiền" từ danh sách bạn bè | `POST /api/transfers/p2p` | Yêu cầu PIN giao dịch, ghi sổ cái chuyển khoản P2P | `MATCH` | Minor | Có nút chuyển tiền nhanh trên từng thẻ bạn bè |
| **D5** | Banking | Xem & Quản lý ngân hàng liên kết | Hiển thị thẻ Vietcombank, Techcombank | `GET /api/bank-connections`<br>`POST /api/bank-connections/{id}/unlink` | Quản lý tài khoản ngân hàng liên kết | `MATCH` | Minor | Đã test: Hiển thị 2 tài khoản ngân hàng kết nối |
| **E1** | Fixed Fund | Chi tiết Quỹ cố định & Tiến độ | Hiển thị số dư, % target (73%), tạm khóa | `GET /api/v1/funds/{id}` | Phản ánh chính xác mục tiêu và số dư | `MATCH` | Minor | Đã test màn hình Quỹ Du Lịch Phú Quốc 2026 |
| **E2** | Fixed Fund | Danh sách thành viên Quỹ cố định | Hiển thị Chủ nhóm, Thành viên, nghĩa vụ kỳ | `GET /api/v1/funds/{id}/members` | Trả về `V1MemberDto` với `isOwner`, `canSpend`, `fullName`, `avatarUrl` | `MATCH` | Minor | Đã test tab Thành viên (4 người) |
| **E3** | Fixed Fund | Bật/tắt quyền chi tiêu (CanSpend) | Nút "Đang cấp quyền · Thu hồi" / "Cấp quyền" | `PUT /api/v1/funds/{id}/members/{userId}/spending-permission` | Chủ quỹ phân quyền chi tiêu cho thành viên | `MATCH` | Minor | **Đã test**: Nút phân quyền hoạt động đúng logic Fixed Fund |
| **E4** | Fixed Fund | Giới hạn 20% & Khóa 24h | Hiển thị "HẠN MỨC 20% CÒN LẠI", quy tắc 24h | Nghiệp vụ V1 Fixed Fund Backend | Chi trong hạn mức 20% qua khóa 24h; chi vượt cần Chủ nhóm duyệt | `MATCH` | Minor | Text quy tắc và số dư hạn mức hiển thị chuẩn xác |
| **E5** | Fixed Fund | Bầu cử Chủ nhóm mới (Governance) | Màn hình Quản trị bầu cử 4 bước | `POST /api/v1/funds/{id}/elections`... | Quy trình bầu cử khi chủ quỹ vắng mặt | `MATCH` | Minor | Đã test tab Quản trị bầu cử |
| **F1** | Free Fund | Chi tiết Quỹ tự do (Free Fund) | Hiển thị số dư khả dụng, góp linh hoạt | `GET /api/v1/funds/{id}` (`Kind = Free`) | Không có hạn mức rút 20% cố định | `MATCH` | Minor | **Đã test**: Màn hình Quỹ Ăn Trưa Văn Phòng thể hiện đúng bản chất Quỹ tự do |
| **F2** | Free Fund | Duyệt chi / Rút quỹ tự do | Tab "Duyệt (1)" hiển thị yêu cầu chi của thành viên | `POST /api/v1/funds/{id}/requests`<br>`POST /api/v1/funds/requests/{id}/review` | Mọi yêu cầu chi của thành viên đều cần Chủ nhóm duyệt | `MATCH` | Minor | **Đã test**: Hiển thị yêu cầu chi "Đặt tiệc bánh ngọt" chờ duyệt |
| **G1** | Planning | Quản lý Ngân sách & Cảnh báo | Hiển thị thanh tiến độ %, cảnh báo chạm trần 80% | `GET /api/budgets`<br>`POST /api/budgets` | Giám sát hạn mức danh mục hàng tháng | `MATCH` | Minor | Đã test màn Kế hoạch tài chính (77%) |
| **G2** | AI Assistant | Chat với Trợ lý Tài chính & Action | Giao diện chat spendwise AI, gợi ý trích lập quỹ | `POST /api/ai/chat`<br>`POST /api/ai/actions/{id}/execute` | Phân tích tài chính dựa trên dữ liệu thật | `MATCH` | Minor | Đã test: Hiển thị thẻ Đề xuất thao tác tài chính 2.000.000 ₫ |
| **G3** | Support | Yêu cầu hỗ trợ & Tra soát (Cases) | Danh sách yêu cầu hỗ trợ, bộ lọc Đang mở/Đã đóng | `GET /api/cases/support`<br>`POST /api/cases/support` | Gửi yêu cầu hỗ trợ và đính kèm biên lai đối soát | `MATCH` | Minor | **Đã test thành công trên DevTools**: Mở chi tiết ticket `#SW-SUP-261018` |

---

## 2. Kết luận Chính xác về Coverage

### 1. Functional Contract Coverage: **90.6% (29 / 32 chức năng MATCH)**
- **Mẫu số chính xác:** **32** nhóm chức năng & hành vi User đã được kiểm kê đầy đủ theo `User.md` và hệ thống Controller/Handler của SpendWise.
- **Số lượng MATCH:** **29** mục.
- **3 Mục lệch cần hoàn thiện trên Frontend:**
  1. **A1 (FRONTEND_WRONG):** Đăng nhập bỏ qua bước PIN Challenge khi User đã kích hoạt mã PIN.
  2. **A2 (FRONTEND_WRONG):** Form Đăng ký gộp cả PIN và bỏ qua bước gửi OTP xác thực email qua mã OTP 8 số.
  3. **C3 (FRONTEND_MISSING):** Ô tìm kiếm Bạn bè chỉ lọc mảng tĩnh nội bộ, chưa nối API gọi `GET /api/friends/discover` để tìm người lạ theo tên.

### 2. Verified Integration Coverage: **0.0% (Client-side Mock Mode)**
- Google AI Studio hiện đang lưu trữ và cập nhật trạng thái trong `localStorage.spendwise_sim_store_v5` (chạy hoàn toàn Client-side).
- Tỷ lệ kết nối mạng thực tế Frontend → Backend → database thử nghiệm là **0.0%** vì chưa cấu hình gọi HTTP thật. Đây là trạng thái mô phỏng có chủ đích theo thiết kế của vòng phát triển hiện tại.
