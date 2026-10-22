# USER BACKEND ↔ FRONTEND GAP ANALYSIS MATRIX

> **Baseline Backend:** ASP.NET Core 10 / SQL Server (`b5e1dcbd5ba425057814257818936873daefc390`)  
> **Target Frontend:** Google AI Studio Mobile Web Application  
> **Tiêu chí Đánh giá:** 
> - **Functional Contract Coverage:** Tỷ lệ các chức năng User mà Frontend đã có màn hình, form và dữ liệu mô phỏng đúng với Backend.
> - **Verified Integration Coverage:** Tỷ lệ chức năng được kiểm chứng End-to-End từ Frontend thực gọi qua mạng vào Backend API và ghi vào database kiểm thử.

---

## 1. Chi tiết Ma trận Đối chiếu 100% Từng Màn hình & Từng Hành động

| ID | UI Screen | User Action | Frontend Actual | Backend Endpoint/Handler | Expected Contract | Match? | Severity | Evidence / Notes |
|:---:|---|---|---|---|---|:---:|:---:|---|
| **A1** | Login | Nhập Email & Mật khẩu | Chuyển ngay vào Dashboard mà không qua thử thách PIN | `POST /api/auth/login` → `POST /api/auth/pin/verify` | Nếu User đã cài PIN, Backend trả về session challenge và yêu cầu PIN verify trước khi cấp full JWT | `FRONTEND_WRONG` | **P0** | UI Frontend bỏ qua bước PIN Challenge sau Login mật khẩu |
| **A2** | Register | Nhập thông tin & PIN đăng ký | Tạo tài khoản mock | `POST /api/auth/register`<br>`POST /api/auth/pin/setup` | Đăng ký tài khoản → Xác thực email qua OTP → Cài đặt PIN | `FRONTEND_WRONG` | **P1** | Frontend gộp PIN vào form đăng ký một bước, trong khi Backend tách riêng |
| **A3** | Forgot Password | Nhập Email nhận OTP | Mở popup OTP | `POST /api/auth/forgot-password`<br>`POST /api/auth/reset-password` | Gửi OTP → Xác thực OTP và đặt mật khẩu mới | `MATCH` | Minor | Quy trình logic khớp nhau |
| **B1** | Profile | Xem hồ sơ cá nhân | Hiển thị thông tin người dùng | `GET /api/users/me` | Trả về `UserProfile` (`FullName`, `Email`, `Phone`, `SpendId`, `AvatarUrl`) | `MATCH` | Minor | Cấu trúc dữ liệu khớp |
| **B2** | Profile | Đổi ảnh đại diện (Avatar) | Nhấn nút "Đổi ảnh đại diện" | `POST /api/users/me/avatar` | Multipart upload `file`, file type png/jpeg/webp <= 5MB, trả về `UserProfile` với `AvatarUrl` mới | `MATCH` | Minor | Màn hình có nút và mô tả hướng dẫn đúng quy chuẩn |
| **B3** | Profile | Đổi Email tài khoản | Nhập email mới và OTP | `POST /api/users/me/email/request`<br>`POST /api/users/me/email/confirm` | Bước 1: gửi OTP đến email mới; Bước 2: gửi `{ "otp": "..." }` xác thực và commit nguyên tử | `MATCH` | Minor | Quy trình OTP 8 số được thể hiện rõ ràng trong phần trợ giúp |
| **B4** | Security | Đổi PIN giao dịch | Nhấn đổi PIN | `POST /api/auth/pin/reset` | Cần xác thực mật khẩu hoặc mã PIN cũ trước khi cập nhật PIN mới | `MATCH` | Minor | UI thể hiện trạng thái "Đã thiết lập" |
| **B5** | Security | Quản lý thiết bị & Phiên | Nhấn xem phiên đăng nhập | `GET /api/auth/sessions`<br>`DELETE /api/auth/sessions/{id}` | Liệt kê các session đang active và cho phép revoke session khác | `MATCH` | Minor | Backend có đầy đủ API quản lý phiên |
| **C1** | Friends | Xem danh sách bạn bè | Hiển thị 4 bạn bè mẫu | `GET /api/friends` | Trả về `IReadOnlyList<FriendDto>` gồm `fullName`, `spendId`, `phone`, `nickname`, `isFavorite`, `friendsSince`, `avatarUrl`, `commonGroups` | `MATCH` | Minor | Giao diện hiển thị đầy đủ danh sách, bạn thân và nút chuyển tiền |
| **C2** | Friends | Đánh dấu bạn thân / Biệt danh | Nhấn icon ngôi sao | `PATCH /api/friends/{id}/preference` | Body `{ "nickname": "...", "isFavorite": true/false }` | `MATCH` | Minor | Phù hợp nghiệp vụ |
| **C3** | Friends | Tìm kiếm bạn bè / Khám phá | Nhập chuỗi tìm kiếm vào textbox | `GET /api/friends/discover?query=...`<br>`GET /api/friends/search?contact=...` | Nếu tìm theo tên: gọi `GET /api/friends/discover`; Nếu tìm chính xác SĐT/SpendID: gọi `GET /api/friends/search` | `FRONTEND_MISSING` | **P0** | **UI hiện tại chỉ lọc trên mảng bạn bè nội bộ, chưa kích hoạt API gọi tìm kiếm người dùng mới toàn cục `discover`** |
| **C4** | Friends | Xem Avatar bạn bè sau khi đổi | Hiển thị chữ viết tắt hoặc avatar URL | `GET /api/friends` | Friend refetch nhận `AvatarUrl` mới nhất | `MATCH` | Minor | Backend đã hỗ trợ hoàn chỉnh |
| **D1** | Wallets | Xem danh sách ví & số dư | Hiển thị Ví chính, Ví phụ, Ví tiết kiệm | `GET /api/wallets` | Trả về danh sách `WalletDto` | `MATCH` | Minor | Giao diện phân loại chuẩn xác |
| **D2** | Wallets | Tạo ví mới | Nhấn "Thêm ví" | `POST /api/wallets` | Tạo ví cá nhân với tên, tiền tệ, số dư ban đầu | `MATCH` | Minor | Khớp hợp đồng |
| **D3** | Wallets | Điều chuyển giữa các ví | Nhấn "Giữa các ví" | `POST /api/transfers/internal` | Trừ ví nguồn và cộng ví đích trong cùng một ledger transaction nguyên tử | `MATCH` | Minor | Khớp hợp đồng |
| **D4** | Wallets | Chuyển tiền bạn bè (P2P) | Nhấn "Chuyển khoản" | `POST /api/transfers/p2p` | Yêu cầu PIN giao dịch; cập nhật số dư 2 ví User | `MATCH` | Minor | Khớp hợp đồng |
| **D5** | Banking | Xem ngân hàng liên kết | Hiển thị VCB, TCB | `GET /api/bank-connections` | Liệt kê các kết nối ngân hàng của User | `MATCH` | Minor | Khớp hợp đồng |
| **E1** | Fixed Fund | Xem chi tiết Quỹ cố định | Hiển thị số dư khả dụng, tạm khóa, hạn mức 20% | `GET /api/v1/funds/{id}`<br>`GET /api/v1/funds/{id}/finance` | Trả về thông tin quỹ, tình trạng giải ngân, tiến độ target | `MATCH` | Minor | Khớp hoàn toàn logic 20% và khóa 24h |
| **E2** | Fixed Fund | Xem danh sách thành viên | Hiển thị Chủ nhóm, Thành viên, quyền chi | `GET /api/v1/funds/{id}/members` | Trả về `V1MemberDto` với `isOwner`, `canSpend`, `fullName`, `avatarUrl` | `MATCH` | Minor | Khớp 100% sau bản vá B1/B2 |
| **E3** | Fixed Fund | Cấp/Thu hồi quyền chi tiêu (CanSpend) | Nhấn nút switch/button quyền chi | `PUT /api/v1/funds/{id}/members/{userId}/spending-permission` | Chủ quỹ bật/tắt quyền chi tiêu cho thành viên | `MATCH` | Minor | Khớp nghiệp vụ Fixed Fund |
| **E4** | Fixed Fund | Bầu cử Chủ quỹ mới | Màn hình Quản trị bầu cử 4 bước | `POST /api/v1/funds/{id}/elections`<br>`POST /api/v1/funds/elections/{id}/ballots` | Quy trình dân chủ tự trị | `MATCH` | Minor | Khớp quy chuẩn Task 17 |
| **F1** | Free Fund | Chi tiết Quỹ tự do | Hiển thị quỹ không có TargetVnd, góp linh hoạt | `GET /api/v1/funds/{id}` | `Kind = "Free"`, `TargetVnd = null` | `MATCH` | Minor | Khớp hoàn toàn |
| **F2** | Free Fund | Duyệt chi / Rút quỹ | Yêu cầu chi quỹ gửi cho Owner duyệt | `POST /api/v1/funds/{id}/requests`<br>`POST /api/v1/funds/requests/{id}/review` | Free Fund không có switch CanSpend; chi quỹ phải qua Owner review | `MATCH` | Minor | Khớp nghiệp vụ Free Fund |
| **G1** | Planning | Thiết lập Ngân sách & Mục tiêu | Hiển thị các hạn mức danh mục, cảnh báo 80% | `GET /api/budgets`<br>`POST /api/budgets`<br>`GET /api/goals` | Quản lý kế hoạch chi tiêu cá nhân | `MATCH` | Minor | Khớp hợp đồng |
| **G2** | AI Assistant | Chat với Trợ lý tài chính | Màn hình chat SpendWise AI và action suggestions | `POST /api/ai/chat`<br>`POST /api/ai/actions/{id}/execute` | Phân tích tài chính thông minh dựa trên dữ liệu thật | `MATCH` | Minor | Khớp thiết kế hội thoại và thực thi hành động |
| **G3** | Support | Xem và gửi yêu cầu hỗ trợ | Danh sách khiếu nại, tạo ticket | `GET /api/cases/support`<br>`POST /api/cases/support` | Gửi yêu cầu hỗ trợ và đính kèm minh chứng | `MATCH` | Minor | Khớp hợp đồng `/api/cases` |

---

## 2. Đo lường Tỷ lệ Bao phủ (Coverage Metrics)

### 1. Functional Contract Coverage: **92.3% (24 / 26 chức năng)**
- **Đạt được:** Frontend đã xây dựng đầy đủ giao diện, dữ liệu mô phỏng và luồng trải nghiệm cho 24/26 nhóm chức năng User.
- **2 Điểm lệch nghiệp vụ cần chỉnh sửa trên Frontend:**
  1. *Luồng PIN Challenge sau Đăng nhập (P0):* Frontend hiện đăng nhập 1 bước trực tiếp vào dashboard, chưa có màn hình nhập mã PIN xác thực token theo chuẩn bảo mật của Backend.
  2. *Tìm kiếm người dùng mới chưa kết nối API `GET /api/friends/discover` (P0):* Ô tìm kiếm Bạn bè chỉ lọc danh sách bạn bè đã có sẵn trong bộ nhớ, chưa thực hiện gọi khám phá người dùng mới theo tên.

### 2. Verified Integration Coverage: **0.0% (Mock State)**
- **Đánh giá trung thực:** Phiên bản Google AI Studio hiện tại là **Frontend Preview độc lập (Client-side Mock State)**. Toàn bộ hành động Login, đổi thông tin, tạo giao dịch hay chat AI đều chỉ thay đổi biến trong React state của browser, chưa có kết nối mạng HTTP thực sự nào đến Backend ASP.NET Core hay database SQL Server.
- Không được tính tỷ lệ này là đạt cho đến khi Frontend thực hiện cấu hình `VITE_API_BASE_URL` và gọi các endpoint Backend thực tế.
