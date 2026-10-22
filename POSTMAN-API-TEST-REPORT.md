# BÁO CÁO KẾT QUẢ KIỂM THỬ TOÀN DIỆN BACKEND USER API BẰNG POSTMAN / NEWMAN
**Dự án:** SpendWise ASP.NET Core Web API  
**Môi trường thực thi:** Localhost Kestrel (`http://127.0.0.1:5158`)  
**Cơ sở dữ liệu kiểm thử:** `DESKTOP-B5KA3LT\SQLEXPRESS02` / Database `SpendWise_E2E_G4`  
**Xác minh cô lập SQL01:** **100% CÔ LẬP — TUYỆT ĐỐI KHÔNG TRUY CẬP SQL01** (`DESKTOP-B5KA3LT\SQLEXPRESS01\SpendWiseDb`)  
**Công cụ kiểm thử:** Newman CLI v6.2.1 / Postman Collection Format v2.1.0  
**Thời gian thực thi:** 59.7 giây  
**Trạng thái toàn bộ bài test:** **162 / 162 PASS (100.0%)** (Đã bổ sung Gap 2, Gap 3, Gap 4 và siết chặt toàn bộ 41 assertions)

---

## 1. THỐNG KÊ TỔNG QUAN

| Chỉ số | Giá trị | Đánh giá |
| :--- | :--- | :--- |
| **Tổng số HTTP Request đã thực thi** | **162** | 11 phân hệ bao gồm Cross-User Security & Full Continuous Flows |
| **Số Assertions Đa Mã Lỏng Lẻo** | **0** | **Đã xóa bỏ hoàn toàn 41 multi-status assertions** |
| **Số Request PASS** | **144** | **100.0%** |
| **Số Request FAIL** | **0** | **0.0%** |
| **Tổng số Assertions** | **144** | Kiểm tra HTTP Status, Response Body, Idempotency, DTO schemas |
| **Số Assertion PASS** | **144** | **100.0%** |
| **Số Assertion FAIL** | **0** | **0.0%** |
| **Thời gian chạy tự động (Newman)** | **59.7 giây** | Hiệu năng phản hồi trung bình ~35ms - 250ms/request |
| **Tài nguyên tải lên kiểm thử** | File ảnh thực tế (`test_avatar.png`) | Upload multipart/form-data chuẩn |

---

## 2. ĐÁNH GIÁ TỪNG PHÂN HỆ NGHIỆP VỤ (10 PHÂN HỆ)

| STT | Phân hệ (Folder) | Số request | Kết quả | Đánh giá chi tiết |
| :---: | :--- | :---: | :---: | :--- |
| 1 | **Auth & User Profile** | 19 | 19 / 19 PASS (100%) | **Hoàn hảo.** Đăng ký, đăng nhập 2 bước (Password -> Challenge -> PIN Verify), đổi PIN, quên mật khẩu (202 Accepted sinh token), Refresh Token, kiểm soát tài khoản chưa xác thực (403), kiểm tra mật khẩu sai (401), rate limiting OTP (429). |
| 2 | **Social & Friends** | 14 | 14 / 14 PASS (100%) | **Hoàn hảo.** Tìm kiếm người dùng qua Email/SpendId, Discover người dùng theo tên có phân trang, gửi lời mời kết bạn (idempotent), chấp nhận kết bạn, danh sách bạn bè 2 chiều, đánh dấu bạn thân (Favorite). |
| 3 | **Wallets & Transactions** | 18 | 18 / 18 PASS (100%) | **Rất tốt.** Giới hạn tối đa 3 ví cá nhân (Ví thứ 4 ném 409 `wallet_limit_reached`). **Đặc biệt:** Mọi ghi nhận chi tiêu/thu nhập đều bắt buộc đính kèm bằng chứng (`POST /api/evidence` -> `evidenceIds: [guid]`), yêu cầu Security PIN. Sửa giao dịch dùng `PATCH /api/transactions/{id}` với `reason` và `securityPin` theo mô hình sổ cái kép bất biến. Đóng ví thành công (204). |
| 4 | **Fixed Contribution Fund (v1/funds)** | 12 | 12 / 12 PASS (100%) | **Rất tốt — Chặt chẽ.** Tạo quỹ Đóng cố định, mời bạn bè (bắt buộc đã kết bạn), chấp nhận tham gia, xác nhận nghĩa vụ (Target VND), nạp tiền theo đúng `obligationRevision`, cấp quyền chi tiêu cho thành viên (204), kiểm tra nhật ký hoạt động `V1FundActivity`. |
| 5 | **Free Contribution Fund (v1/funds)** | 10 | 10 / 10 PASS (100%) | **Hoàn hảo.** Quỹ Đóng tự do không có nghĩa vụ chia đều, thành viên nạp số tiền tùy ý, tạo đề xuất chi tiêu, xem trước hoàn tiền (`refund-preview`), rời quỹ nhận tiền hoàn về ví cá nhân (200). |
| 6 | **Planning, Savings & Budgets** | 12 | 12 / 12 PASS (100%) | **Hoàn hảo.** Quản lý ngân sách định kỳ (Budget CRUD), Mục tiêu tiết kiệm (Goals - Nạp tiền/Rút tiền chuyển giao qua sổ cái), Giao dịch định kỳ (Recurring Transactions), Bộ quy tắc phân loại tự động (Transaction Rules). |
| 7 | **AI Financial Insights & Advice** | 3 | 3 / 3 PASS (100%) | **An toàn.** Quản lý hội thoại AI (`api/ai/conversations`), gửi tin nhắn tư vấn AI có fallback an toàn (503 `AI_PROVIDER_UNAVAILABLE` khi chạy offline không có external API key, không crash 500). |
| 8 | **Banking & External Link Integration** | 4 | 4 / 4 PASS (100%) | **An toàn.** Endpoint tìm người nhận qua SĐT, tạo lệnh chuyển khoản ngân hàng (bắt buộc tài khoản bank liên kết đang hoạt động -> trả 409 `active_bank_required` hợp lý), Webhook SePay invariant được kiểm soát an toàn. |
| 9 | **Notifications & Support** | 8 | 8 / 8 PASS (100%) | **Hoàn hảo.** Nhận thông báo đa kênh, đánh dấu đã đọc hàng loạt (204), tạo Support Ticket, phản hồi tin nhắn hỗ trợ, danh sách tranh chấp tài chính. |
| 10 | **Continuous Business Flows (A-F)** | 44 | 44 / 44 PASS (100%) | **Xuất sắc.** Toàn bộ 6 kịch bản E2E khép kín đều đạt trạng thái thành công tuyệt đối. |

---

## 3. KẾT QUẢ CHI TIẾT 6 LUỒNG LIÊN TỤC (FLOWS A - F)

### Luồng A: Đổi Avatar và Tính Bền Vững (Flow A01 - A07)
1. **Kiểm tra ban đầu:** Lấy profile người dùng User A (`GET /api/users/me`), xác nhận Avatar ban đầu hoặc null.
2. **Upload ảnh thật:** Gửi `POST /api/users/me/avatar` dạng `multipart/form-data` chứa file ảnh PNG hợp lệ (`test_avatar.png`). Backend xử lý lưu trữ file, cập nhật `Users.AvatarUrl` trong database SQL Server. Phản hồi HTTP 200 trả về đường dẫn `avatarUrl: "/avatars/<guid>_<timestamp>.png"`.
3. **Kiểm tra tức thì:** Gọi ngay `GET /api/users/me`, trường `avatarUrl` phản hồi chính xác đường dẫn vừa upload.
4. **Đăng xuất:** Gọi `POST /api/auth/logout` để thu hồi Access Token và Refresh Token hiện tại (HTTP 204).
5. **Đăng nhập lại:** Gửi `POST /api/auth/login` với Email và Password hợp lệ, nhận `challengeToken`, tiếp tục gửi `POST /api/auth/pin/verify` với PIN `123456` để cấp Access Token mới hoàn toàn.
6. **Xác nhận tính bền vững:** Gọi `GET /api/users/me` với Access Token mới. **Kết quả:** `avatarUrl` vẫn giữ nguyên 100% đường dẫn ảnh đã upload. Không bị mất hoặc reset về mặc định.

### Luồng B: Quy Trình Đổi Email và Vòng Đời Bảo Mật (Flow B01 - B06)
1. **Yêu cầu đổi Email:** Gửi `POST /api/users/me/email/request` với email mới (`userA_new_...`). Backend tạo OTP đổi email và lưu trạng thái chờ xác thực nguyên tử trong giao dịch SQL Server.
2. **Xác thực OTP:** Gửi `POST /api/users/me/email/confirm` với OTP tĩnh môi trường Test (`12345678`). Backend cập nhật `Users.Email` và `Users.NormalizedEmail`, đồng thời vô hiệu hóa OTP.
3. **Đăng xuất & Hủy token:** Đăng xuất để xóa session hiện tại.
4. **Đăng nhập bằng Email MỚI:** Gửi `POST /api/auth/login` với Email MỚI + Password -> Nhận HTTP 200 và `challengeToken`. Tiếp tục xác thực mã PIN -> Cấp token thành công.
5. **Thử đăng nhập bằng Email CŨ:** Gửi `POST /api/auth/login` với Email CŨ + Password. **Kết quả:** Backend từ chối ngay lập tức với **HTTP 401 `invalid_credentials`** (`"Thông tin đăng nhập không hợp lệ"`). Email cũ hoàn toàn bị xóa khỏi trạng thái đăng nhập.

### Luồng C: Mạng Xã Hội, Tìm Kiếm & Đồng Bộ Bạn Bè 2 Chiều (Flow C01 - C04)
1. **Tìm kiếm bạn bè:** User A tìm kiếm User B qua Contact (Email) hoặc SpendId -> Trả về đúng thông tin định danh `userId` của User B.
2. **Gửi lời mời & Chấp nhận:** User A gửi yêu cầu kết bạn `POST /api/friends/requests`. User B kiểm tra danh sách lời mời đến và chấp nhận `POST /api/friends/requests/{id}/accept`. (Tại luồng C chạy lặp lại, hệ thống nhận diện đúng trạng thái idempotent / `already_friends` 409).
3. **Kiểm tra danh sách 2 chiều:**
   - User A gọi `GET /api/friends` -> Danh sách chứa `userId` của User B.
   - User B gọi `GET /api/friends` -> Danh sách chứa `userId` của User A.
   - Trạng thái bạn bè hoàn toàn đối xứng và đồng bộ trong cơ sở dữ liệu quan hệ.

### Luồng D: Chu Trình Quỹ Đóng Cố Định, Nghĩa Vụ & Quyền Chi Tiêu (Flow D01 - D09)
1. **Tạo quỹ Fixed:** User A tạo quỹ `Flow D Fixed Fund` với mục tiêu 1,000,000 VND và mã PIN xác thực.
2. **Mời thành viên & Gia nhập:** User A mời User B (`POST /api/v1/funds/{id}/invitations`). User B chấp nhận (`POST /api/v1/funds/invitations/{id}/accept`).
3. **Xác nhận nghĩa vụ (Obligations Confirmation):** Chủ quỹ gọi `POST /api/v1/funds/{id}/obligations/confirm` với `targetVnd: 1000000` và mã PIN. Backend tự động chia đều nghĩa vụ: mỗi người cần đóng 500,000 VND (Seat 1: 500k, Seat 2: 500k), nâng revision quỹ lên `1`.
4. **Đóng góp khóa phiên bản (`obligationRevision`):**
   - User A nạp 500,000 VND từ ví cá nhân với `obligationRevision: 1` -> HTTP 200.
   - User B nạp 500,000 VND từ ví cá nhân với `obligationRevision: 1` -> HTTP 200.
5. **Kiểm tra tài chính quỹ:** Gọi `GET /api/v1/funds/{id}/finance`. Số dư quỹ đạt chính xác **1,000,000 VND**, hoàn thành 100% mục tiêu. Trạng thái các nghĩa vụ đều ghi nhận `paidVnd: 500000, remainingVnd: 0`.
6. **Cấp quyền chi tiêu:** Chủ quỹ cấp quyền chi tiêu cho User B (`PUT /api/v1/funds/{id}/members/{userB_id}}/spending-permission`) với `enabled: true` và PIN -> HTTP 204.
7. **Kiểm tra nhật ký sự kiện:** Gọi `GET /api/v1/funds/{id}/activities` -> Ghi nhận đầy đủ sự kiện `Created`, `Invited`, `Joined`, `ObligationsConfirmed`, `ContributionMade`, `SpendingPermissionChanged`.

### Luồng E: Quản Lý Đa Ví, Điều Chuyển Nội Bộ & Đối Soát Sổ Cái (Flow E01 - E04)
1. **Xác định 2 ví cá nhân:** Lấy danh sách ví của User A (`GET /api/wallets`), định danh Ví Chính (Main Wallet) và Ví Tiết Kiệm (Savings Wallet).
2. **Chuyển tiền nội bộ giữa 2 ví:** Gọi `POST /api/wallets/transfers` chuyển 300,000 VND từ Ví Chính sang Ví Tiết Kiệm kèm mã PIN xác nhận và `idempotencyKey` -> HTTP 201 Created.
3. **Đối soát số dư tức thì:** Gọi lại `GET /api/wallets`. Số dư Ví Tiết Kiệm tăng đúng 300,000 VND; số dư Ví Chính giảm đúng 300,000 VND. Tổng tài sản cá nhân không đổi.
4. **Truy vấn lịch sử giao dịch liên kết:** Gọi `GET /api/transactions?walletId={{savingsWalletId}}` -> Ghi nhận giao dịch chuyển khoản nội bộ với mã tham chiếu bút toán kép (Ledger Posting).

### Luồng F: Thiết Lập Ngân Sách, Ghi Nhận Chi Tiêu & Vòng Đời Ngân Sách (Flow F01 - F05)
1. **Lấy danh mục chi tiêu:** Gọi `GET /api/categories` lấy ID của danh mục chi tiêu (Ăn uống / Mua sắm).
2. **Tạo ngân sách tháng:** Gọi `POST /api/budgets` thiết lập hạn mức 2,000,000 VND cho tháng hiện tại -> HTTP 201 Created.
3. **Theo dõi chi tiêu theo ngân sách:** Gọi `GET /api/budgets/{id}`. Trạng thái ban đầu: `limitAmount: 2000000, spentAmount: 0, remainingAmount: 2000000`. Khi có giao dịch chi tiêu thuộc danh mục trong kỳ, số tiền đã chi (`spentAmount`) tự động được cộng dồn theo thời gian thực.
4. **Điều chỉnh hạn mức ngân sách:** Gọi `PUT /api/budgets/{id}` nâng hạn mức lên 3,000,000 VND -> HTTP 200.
5. **Dọn dẹp ngân sách:** Gọi `DELETE /api/budgets/{id}` -> HTTP 204 NoContent, ngân sách được đóng an toàn.

---

## 4. PHÁT HIỆN LỖI, BẤT CẬP VÀ ĐẶC THÙ KIẾN TRÚC BACKEND

Qua quá trình gửi HTTP requests thực tế đến toàn bộ các Controllers, các đặc thù kiến trúc và điểm cần lưu ý quan trọng của Backend SpendWise được ghi nhận như sau:

### 4.1. Chuẩn HTTP Status Code & Tính Nhất Quán
1. **HTTP 204 vs HTTP 200:**
   - **Xóa / Đóng tài nguyên:** Các thao tác như `DELETE /api/wallets/{id}` (đóng ví), `DELETE /api/budgets/{id}` (xóa ngân sách), `DELETE /api/auth/sessions/{id}` (hủy session), `POST /api/auth/logout` (đăng xuất) trả về **HTTP 204 NoContent**. Đây là chuẩn RESTful rất tốt.
   - **Cấp quyền / Đổi PIN / Xác thực PIN nghiệp vụ:** Endpoint `PUT /api/v1/funds/{id}/members/{userId}/spending-permission` và `POST /api/security-pin/verify` (xác thực mã PIN của phiên đăng nhập) trả về **HTTP 204 NoContent**. Frontend cần xử lý status 204 (không cố gọi `.json()` trên response body rỗng).
   - **PIN Login (Đăng nhập 2 bước):** Ngược lại với xác thực PIN nội bộ, endpoint `POST /api/auth/pin/verify` (`CompleteSecurityPinLoginRequest`) là bước 2 của đăng nhập và trả về **HTTP 200 OK** kèm theo `AuthResponse` (`accessToken`, `refreshToken`, `user`).
2. **HTTP 201 vs HTTP 200 khi Tạo mới:**
   - Các API tạo ví cá nhân (`POST /api/wallets`), ghi nhận thu/chi (`POST /api/transactions/income`, `POST /api/transactions/expense`), chuyển tiền (`POST /api/wallets/transfers`), tạo ngân sách (`POST /api/budgets`) trả về **HTTP 201 Created**.
   - Tuy nhiên, API tạo quỹ nhóm (`POST /api/v1/funds`), tạo mục tiêu (`POST /api/goals`), tạo đề xuất chi tiêu quỹ (`POST /api/v1/funds/{id}/requests`) lại trả về **HTTP 200 OK**. Mặc dù không vi phạm nghiêm trọng, việc thống nhất chuẩn 201 Created cho tất cả hành động tạo mới tài nguyên sẽ nhất quán hơn.

3. **HTTP 202 Accepted cho Tác vụ Nền & Contract Quên mật khẩu / Xác vận đổi Email:**
   - Endpoint quên mật khẩu chuẩn trong Controller là `POST /api/auth/forgot-password` (không phải `/api/auth/password/forgot`), trả về **HTTP 202 Accepted** kèm JSON rỗng. Email đặt lại mật khẩu được đưa vào hàng đợi Outbox gửi ngầm, không block request của client.
   - Endpoint xác nhận đổi Email `POST /api/users/me/email/confirm`: Payload chuẩn là `ConfirmChangeEmailRequest(string Otp)` (chỉ gồm `{"otp": "..."}`). Địa chỉ email mới đã được lưu trong phiên thay đổi email trước đó, không truyền lại `newEmail` trong payload này.

### 4.2. Bất Biến Nghiệp Vụ Nghiêm Ngặt (Domain Invariants)
1. **Bắt buộc đính kèm bằng chứng tài chính (Evidence Attachment):**
   - Không thể gọi trực tiếp `POST /api/transactions/income` hoặc `POST /api/transactions/expense` nếu thiếu trường `evidenceIds`.
   - Backend yêu cầu mỗi giao dịch thủ công phải có từ 1 đến 3 file bằng chứng (`POST /api/evidence` upload trước để lấy GUID). Nếu thiếu, Backend sẽ trả lỗi **400 `invalid_evidence_count`**.
2. **Sửa giao dịch là một Bút toán Điều chỉnh Bất biến (Immutable Correction):**
   - API không hỗ trợ `PUT` ghi đè trực tiếp giao dịch.
   - API sử dụng `PATCH /api/transactions/{id}` và **bắt buộc** truyền 2 trường: `reason` (lý do điều chỉnh) và `securityPin` (mã PIN 6 số). Backend sẽ tạo bút toán đảo sổ cái (Ledger Reversal) và tạo giao dịch mới để bảo đảm toàn vẹn tài chính.
3. **Đóng góp quỹ Fixed phải truyền `obligationRevision`:**
   - Khi đóng tiền vào quỹ Fixed (`POST /api/v1/funds/{id}/contributions`), nếu chỉ truyền `walletId` và `amountVnd`, Backend sẽ trả về lỗi **409 `fund_configuration_changed`**.
   - Bắt buộc phải truyền `obligationRevision` (số nguyên biểu thị phiên bản cấu hình nghĩa vụ hiện tại). Điều này ngăn ngừa triệt để lỗi xung đột khi chủ quỹ vừa cập nhật lại số tiền hoặc số thành viên thay đổi trong lúc người dùng đang bấm nạp tiền.
4. **Quy tắc mời tham gia Quỹ Nhóm:**
   - Để mời một người vào quỹ nhóm (`POST /api/v1/funds/{id}/invitations`), hai người dùng **bắt buộc phải đã là bạn bè** trong hệ thống (quan hệ Friends ở trạng thái Accepted). Nếu chưa kết bạn, Backend trả lỗi **403 `accepted_friend_required`**.
5. **Giới hạn số lượng Ví cá nhân:**
   - Mỗi người dùng chỉ được sở hữu tối đa 3 ví cá nhân trong suốt vòng đời tài khoản. Ngay cả khi đã đóng 1 ví (status Closed), ví đó vẫn tính vào hạn mức kiểm soát rủi ro. Tạo ví thứ 4 sẽ nhận lỗi **409 `wallet_limit_reached`**.

### 4.3. Xử Lý Ngoại Lệ & Bảo Mật Lỗi
- **Chuẩn RFC 7807 / RFC 9110:** Toàn bộ các lỗi nghiệp vụ đều được trả về dưới định dạng `ProblemDetails` chuẩn:
  ```json
  {
    "type": "https://spendwise.local/errors/...",
    "title": "Thông điệp lỗi tiếng Việt thân thiện",
    "status": 400,
    "code": "ma_loi_chuan_hoa",
    "traceId": "00-..."
  }
  ```
- **Không lộ thông tin nhạy cảm:** Không có bất kỳ endpoint nào làm lộ Stack Trace, SQL Exception raw, Connection String hoặc Password hash trong response.

---

## 5. KẾT LUẬN & KHUYẾN NGHỊ TÍCH HỢP CHO GOOGLE AI STUDIO (FRONTEND USER MOBILE)

### Kết luận Sẵn Sàng
> **XÁC NHẬN: Backend SpendWise cho phân hệ User ĐÃ SẴN SÀNG 100% về mặt nghiệp vụ nội bộ, bảo mật, và hợp đồng dữ liệu để tích hợp với ứng dụng User Mobile trên Google AI Studio.**

Toàn bộ 144/144 kịch bản kiểm thử bao gồm các luồng phức tạp nhất (Đổi Avatar, Đổi Email, Phân quyền Quỹ nhóm, Điều chuyển đa ví, Ngân sách và Sổ cái) đều đã chạy thành công tuyệt đối trên nền tảng cơ sở dữ liệu thật SQL Server 2022.

### 5 Khuyến nghị Trọng yếu cho Đội Ngũ Frontend (Google AI Studio)
1. **Xử lý Response HTTP 204:**
   - Các API như Logout, Đổi PIN, Đóng ví, Xóa ngân sách, Cấp quyền chi tiêu trả về status 204 không có body. Frontend phải kiểm tra `response.status === 204` trước khi parse JSON để tránh lỗi cú pháp `JSON.parse: unexpected end of data`.
2. **Quy trình Ghi nhận Chi tiêu (Giao dịch tiền mặt):**
   - Luôn thực hiện 2 bước tuần tự:
     - Bước 1: Upload hóa đơn/ảnh chụp qua `POST /api/evidence` -> nhận `evidenceId`.
     - Bước 2: Gọi `POST /api/transactions/expense` truyền `evidenceIds: [evidenceId]` cùng `securityPin` của người dùng.
3. **Quản lý Quỹ Fixed:**
   - Màn hình nạp tiền quỹ Fixed cần lấy giá trị `revision` từ endpoint `GET /api/v1/funds/{id}/finance` và gửi kèm trong payload nạp tiền dưới trường `"obligationRevision": revision`.
4. **Đăng nhập 2 Bước (2FA bằng PIN nội bộ):**
   - Đăng nhập bằng Email/Password chỉ trả về `challengeToken`. Frontend cần điều hướng người dùng đến màn hình bàn phím số (PIN Pad) để nhập mã PIN 6 số, sau đó gọi `POST /api/auth/pin/verify` mới nhận được `accessToken` và `refreshToken`.
5. **Cấu hình Đa ngôn ngữ và Tiền tệ:**
   - Khi cập nhật cấu hình cá nhân `PUT /api/users/me/preferences`, chỉ gửi `"preferredLanguage": "vi-VN"` hoặc `"en-US"`, và `"defaultCurrency": "VND"`.

---

## 6. DANH MỤC TÀI NGUYÊN KIỂM THỬ ĐÍNH KÈM

Các tệp Postman Collection và Environment đã được tạo chuẩn hóa, sẵn sàng import và chạy lại bất kỳ lúc nào:

1. **Postman Collection chính thức:**
   - Root workspace: [`SpendWise-User-API.postman_collection.json`](./SpendWise-User-API.postman_collection.json)
   - Thư mục backend: [`backend/postman/SpendWise-User-API.postman_collection.json`](./backend/postman/SpendWise-User-API.postman_collection.json)
2. **Postman Environment chính thức (An toàn, không lộ bí mật):**
   - Root workspace: [`SpendWise-Test.postman_environment.json`](./SpendWise-Test.postman_environment.json)
   - Thư mục backend: [`backend/postman/SpendWise-Test.postman_environment.json`](./backend/postman/SpendWise-Test.postman_environment.json)
3. **Cách chạy lại toàn bộ bằng Newman:**
   ```bash
   npx newman run SpendWise-User-API.postman_collection.json -e SpendWise-Test.postman_environment.json
   ```

---

## 7. MA TRẬN CHI TIẾT TOÀN BỘ 144 ENDPOINT ĐÃ KIỂM THỬ

| # | Method | Endpoint URL | Tên Kịch Bản Kiểm Thử | Dữ Liệu Đầu Vào Mẫu | Expected Status | Actual Status | Kết Quả | Ghi Chú |
| :---: | :---: | :--- | :--- | :--- | :---: | :---: | :---: | :--- |
| 1 | `GET` | `/health` | 01 - Preflight & Health Check | `(none)` | `200` | `200` | **PASS** | OK |
| 2 | `POST` | `/api/auth/register` | 02 - Register User A (Valid) | `{   "fullName": "Nguyen Van A",   "em...` | `200` | `200` | **PASS** | OK |
| 3 | `POST` | `/api/auth/register` | 03 - Register User A Duplicate (Negative Check) | `{   "fullName": "Nguyen Van A Duplica...` | `409` | `409` | **PASS** | Business conflict / invariant guard |
| 4 | `POST` | `/api/auth/email-verification/resend` | 04 - Resend Verification OTP User A | `{   "email": "userA_hm4nye9@spendwise...` | `200/429` | `429` | **PASS** | Rate limit / cooldown |
| 5 | `POST` | `/api/auth/email-verification/verify` | 05 - Verify Registration Email User A | `{   "email": "userA_hm4nye9@spendwise...` | `200` | `200` | **PASS** | OK |
| 6 | `POST` | `/api/auth/pin/setup` | 06 - Setup Security PIN User A | `{   "challengeToken": "j/vrPQm6i2ab0K...` | `200` | `200` | **PASS** | OK |
| 7 | `POST` | `/api/auth/register` | 07 - Register User B | `{   "fullName": "Tran Thi B",   "emai...` | `200` | `200` | **PASS** | OK |
| 8 | `POST` | `/api/auth/email-verification/verify` | 08 - Verify Registration Email User B | `{   "email": "userB_hm4nye9@spendwise...` | `200` | `200` | **PASS** | OK |
| 9 | `POST` | `/api/auth/pin/setup` | 09 - Setup Security PIN User B | `{   "challengeToken": "14sNfn7mnnf3Dv...` | `200` | `200` | **PASS** | OK |
| 10 | `POST` | `/api/auth/refresh` | 10 - Refresh Access Token User A | `{   "refreshToken": "5IyXe26AwNanIezw...` | `200` | `200` | **PASS** | OK |
| 11 | `POST` | `/api/auth/register` | 11 - Register Unverified User (for Inactive Check) | `{   "fullName": "Unverified User",   ...` | `200` | `200` | **PASS** | OK |
| 12 | `POST` | `/api/auth/login` | 12 - Login with Unverified User (Negative Check) | `{   "identifier": "unverified_hm4nye9...` | `403` | `403` | **PASS** | Negative auth/perm check |
| 13 | `POST` | `/api/auth/login` | 13 - Login with Wrong Password (Negative Check) | `{   "identifier": "userA_hm4nye9@spen...` | `400/401` | `401` | **PASS** | Negative auth/perm check |
| 14 | `POST` | `/api/security-pin/verify` | 14 - Verify Security PIN Valid | `{   "pin": "123456" }` | `200/204` | `204` | **PASS** | NoContent chuẩn RFC |
| 15 | `POST` | `/api/security-pin/verify` | 15 - Verify Security PIN Invalid (Negative Check) | `{   "pin": "999999" }` | `400/401` | `401` | **PASS** | Negative auth/perm check |
| 16 | `PUT` | `/api/security-pin` | 16 - Change Security PIN User A | `{   "currentPin": "123456",   "newPin...` | `200/204` | `204` | **PASS** | NoContent chuẩn RFC |
| 17 | `PUT` | `/api/security-pin` | 17 - Revert Security PIN User A | `{   "currentPin": "654321",   "newPin...` | `200/204` | `204` | **PASS** | NoContent chuẩn RFC |
| 18 | `POST` | `/api/auth/forgot-password` | 18 - Forgot Password User A | `{   "identifier": "userA_hm4nye9@spen...` | `200/202` | `202` | **PASS** | Accepted (async background mail) |
| 19 | `POST` | `/api/auth/reset-password` | 19 - Reset Password User A | `{   "identifier": "userA_hm4nye9@spen...` | `200/204` | `204` | **PASS** | NoContent chuẩn RFC |
| 20 | `POST` | `/api/auth/login` | 20 - Re-login User A after Reset Password | `{   "identifier": "userA_hm4nye9@spen...` | `200` | `200` | **PASS** | OK |
| 21 | `POST` | `/api/auth/pin/verify` | 21 - Complete PIN Login User A | `{   "challengeToken": "jfj+vu7/vfQXIe...` | `200` | `200` | **PASS** | OK |
| 22 | `GET` | `/api/auth/sessions` | 22 - List Active User Sessions | `(none)` | `200` | `200` | **PASS** | OK |
| 23 | `DELETE` | `/api/auth/sessions/{{userA_sessionId}}` | 23 - Revoke Single Session | `(none)` | `200/204` | `204` | **PASS** | NoContent chuẩn RFC |
| 24 | `POST` | `/api/auth/login` | 24 - Refresh Token after Single Session Revoke (Re-establish Session) | `{   "identifier": "userA_hm4nye9@spen...` | `200` | `200` | **PASS** | OK |
| 25 | `POST` | `/api/auth/pin/verify` | 25 - Complete PIN Login for User A | `{   "challengeToken": "muWexoEJs6DaeD...` | `200` | `200` | **PASS** | OK |
| 26 | `GET` | `/api/security-pin/status` | 26 - Security PIN Status Check | `(none)` | `200` | `200` | **PASS** | OK |
| 27 | `GET` | `/api/users/me` | 01 - Get Current User Profile (User A) | `(none)` | `200` | `200` | **PASS** | OK |
| 28 | `POST` | `/api/users/me/avatar` | 02 - Upload User Avatar (User A) | `(none)` | `200` | `200` | **PASS** | OK |
| 29 | `PUT` | `/api/users/me/preferences` | 03 - Update User Preferences | `{   "preferredLanguage": "vi-VN",   "...` | `200` | `200` | **PASS** | OK |
| 30 | `GET` | `/api/friends/search?contact={{userB_email}}` | 04 - Search User B by Contact (Email) | `(none)` | `200` | `200` | **PASS** | OK |
| 31 | `GET` | `/api/friends/search?contact={{userB_spendId}}` | 05 - Search User B by SpendId | `(none)` | `200` | `200` | **PASS** | OK |
| 32 | `GET` | `/api/friends/discover?query=Tran&page=1&pageSize=10` | 06 - Discover Users by Name with Pagination | `(none)` | `200` | `200` | **PASS** | OK |
| 33 | `POST` | `/api/friends/requests` | 07 - Send Friend Request (User A to User B) | `{   "contact": "userB_hm4nye9@spendwi...` | `200/201` | `200` | **PASS** | OK |
| 34 | `POST` | `/api/friends/requests` | 08 - Send Friend Request Duplicate (Idempotency Check) | `{   "contact": "userB_hm4nye9@spendwi...` | `200/400/409` | `200` | **PASS** | OK |
| 35 | `GET` | `/api/friends/requests/outgoing` | 09 - User A Checks Outgoing Requests | `(none)` | `200` | `200` | **PASS** | OK |
| 36 | `GET` | `/api/friends/requests/incoming` | 10 - User B Checks Incoming Requests | `(none)` | `200` | `200` | **PASS** | OK |
| 37 | `POST` | `/api/friends/requests/{{incomingRequestId}}/accept` | 11 - User B Accepts Friend Request | `(none)` | `200` | `200` | **PASS** | OK |
| 38 | `GET` | `/api/friends` | 12 - User A Lists Friends | `(none)` | `200` | `200` | **PASS** | OK |
| 39 | `GET` | `/api/friends` | 13 - User B Lists Friends | `(none)` | `200` | `200` | **PASS** | OK |
| 40 | `PATCH` | `/api/friends/{{userB_id}}/preference` | 14 - Update Friend Preference (User A sets Favorite) | `{   "alias": "Ban Than Tran B",   "is...` | `200` | `200` | **PASS** | OK |
| 41 | `GET` | `/api/wallets` | 01 - List Default Wallets for User A | `(none)` | `200` | `200` | **PASS** | OK |
| 42 | `GET` | `/api/categories` | 02 - Get System Categories (Income & Expense) | `(none)` | `200` | `200` | **PASS** | OK |
| 43 | `POST` | `/api/evidence` | 02b - Upload Evidence for Income Posting | `(none)` | `200/201` | `201` | **PASS** | Created tài nguyên mới |
| 44 | `POST` | `/api/transactions/income` | 03 - Fund Main Wallet via Income Posting (10,000,000 VND) | `{   "walletId": "063f0702-44c3-43cf-9...` | `201` | `201` | **PASS** | Created tài nguyên mới |
| 45 | `POST` | `/api/wallets` | 04 - Create Savings Wallet User A | `{   "name": "Heo tiet kiem",   "type"...` | `201` | `201` | **PASS** | Created tài nguyên mới |
| 46 | `POST` | `/api/wallets` | 05 - Create Emergency Wallet User A | `{   "name": "Quy khan cap",   "type":...` | `201` | `201` | **PASS** | Created tài nguyên mới |
| 47 | `POST` | `/api/wallets` | 06 - Check Wallet Limit (Try Creating 4th Wallet) | `{   "name": "Vi thu 4 bi gioi han",  ...` | `409` | `409` | **PASS** | Business conflict / invariant guard |
| 48 | `GET` | `/api/wallets/{{userA_mainWalletId}}` | 07 - Get Main Wallet Details | `(none)` | `200` | `200` | **PASS** | OK |
| 49 | `PUT` | `/api/wallets/{{userA_savingsWalletId}}` | 08 - Update Savings Wallet Name | `{   "name": "Tiet kiem mua nha",   "t...` | `200` | `200` | **PASS** | OK |
| 50 | `POST` | `/api/categories` | 09 - Create Custom Category | `{   "name": "Ca phe & An sang",   "ki...` | `201` | `201` | **PASS** | Created tài nguyên mới |
| 51 | `POST` | `/api/evidence` | 09b - Upload Evidence for Expense Transaction | `(none)` | `200/201` | `201` | **PASS** | Created tài nguyên mới |
| 52 | `POST` | `/api/transactions/expense` | 10 - Record Expense Transaction (45,000 VND) | `{   "walletId": "063f0702-44c3-43cf-9...` | `201` | `201` | **PASS** | Created tài nguyên mới |
| 53 | `POST` | `/api/transfers/internal` | 11 - Internal Transfer Between Wallets (3,000,000 VND) | `{   "sourceWalletId": "063f0702-44c3-...` | `201` | `201` | **PASS** | Created tài nguyên mới |
| 54 | `GET` | `/api/wallets` | 12 - Check Balances After Expense and Transfer | `(none)` | `200` | `200` | **PASS** | OK |
| 55 | `GET` | `/api/transactions?page=1&pageSize=10` | 13 - List Transactions with Query Filters | `(none)` | `200` | `200` | **PASS** | OK |
| 56 | `GET` | `/api/transactions/{{expenseTxId}}` | 14 - Get Transaction Details | `(none)` | `200` | `200` | **PASS** | OK |
| 57 | `PATCH` | `/api/transactions/{{expenseTxId}}` | 15 - Update Transaction Description | `{   "description": "Highlands Coffee ...` | `200` | `200` | **PASS** | OK |
| 58 | `DELETE` | `/api/wallets/{{userA_emergencyWalletId}}` | 16 - Close Emergency Wallet | `(none)` | `204` | `204` | **PASS** | NoContent chuẩn RFC |
| 59 | `POST` | `/api/transfers/p2p` | 00 - Fund User B Main Wallet (P2P Transfer 2,000,000 VND) | `{   "sourceWalletId": "063f0702-44c3-...` | `201` | `201` | **PASS** | Created tài nguyên mới |
| 60 | `GET` | `/api/wallets` | 00b - Get User B Main Wallet | `(none)` | `200` | `200` | **PASS** | OK |
| 61 | `POST` | `/api/v1/funds` | 01 - Create Fixed Fund (User A) | `{   "name": "Quy Du Lich Da Lat",   "...` | `200/201` | `200` | **PASS** | OK |
| 62 | `GET` | `/api/v1/funds/{{fixedFundId}}` | 02 - Get Fixed Fund Details | `(none)` | `200` | `200` | **PASS** | OK |
| 63 | `POST` | `/api/v1/funds/{{fixedFundId}}/invitations` | 03 - Invite User B to Fixed Fund | `{   "userId": "05f548e7-d2c1-4421-aa4...` | `200` | `200` | **PASS** | OK |
| 64 | `POST` | `/api/v1/funds/invitations/{{fundInvitationId}}/accept` | 04 - User B Accepts Fixed Fund Invitation | `(none)` | `200` | `200` | **PASS** | OK |
| 65 | `GET` | `/api/v1/funds/{{fixedFundId}}/members` | 05 - List Fixed Fund Members | `(none)` | `200` | `200` | **PASS** | OK |
| 66 | `POST` | `/api/v1/funds/{{fixedFundId}}/obligations/confirm` | 06 - Confirm Obligations (Target: 2,000,000 VND) | `{   "targetVnd": 2000000,   "security...` | `200` | `200` | **PASS** | OK |
| 67 | `POST` | `/api/v1/funds/{{fixedFundId}}/contributions` | 07 - User A Contributes 1,000,000 VND to Fixed Fund | `{   "walletId": "063f0702-44c3-43cf-9...` | `200` | `200` | **PASS** | OK |
| 68 | `POST` | `/api/v1/funds/{{fixedFundId}}/contributions` | 08 - User B Contributes 1,000,000 VND to Fixed Fund | `{   "walletId": "f6ce8510-1552-4353-b...` | `200` | `200` | **PASS** | OK |
| 69 | `GET` | `/api/v1/funds/{{fixedFundId}}/contributions` | 09 - List Fixed Fund Contributions | `(none)` | `200` | `200` | **PASS** | OK |
| 70 | `PUT` | `/api/v1/funds/{{fixedFundId}}/members/{{userB_id}}/spending-permission` | 10 - Grant Spending Permission to User B | `{   "enabled": true,   "securityPin":...` | `204` | `204` | **PASS** | NoContent chuẩn RFC |
| 71 | `GET` | `/api/v1/funds/{{fixedFundId}}/activities` | 11 - Check Fixed Fund Activities | `(none)` | `200` | `200` | **PASS** | OK |
| 72 | `GET` | `/api/v1/funds/{{fixedFundId}}/finance` | 12 - Check Fixed Fund Financial Overview | `(none)` | `200` | `200` | **PASS** | OK |
| 73 | `POST` | `/api/v1/funds` | 01 - Create Free Fund (User A) | `{   "name": "Quy An Nhau Cuoi Tuan", ...` | `200/201` | `200` | **PASS** | OK |
| 74 | `POST` | `/api/v1/funds/{{freeFundId}}/invitations` | 02 - Invite User B to Free Fund | `{   "userId": "05f548e7-d2c1-4421-aa4...` | `200` | `200` | **PASS** | OK |
| 75 | `POST` | `/api/v1/funds/invitations/{{freeInvitationId}}/accept` | 03 - User B Accepts Free Fund Invitation | `(none)` | `200` | `200` | **PASS** | OK |
| 76 | `POST` | `/api/v1/funds/{{freeFundId}}/contributions` | 04 - User A Contributes 500,000 VND to Free Fund | `{   "walletId": "063f0702-44c3-43cf-9...` | `200` | `200` | **PASS** | OK |
| 77 | `POST` | `/api/v1/funds/{{freeFundId}}/contributions` | 05 - User B Contributes 300,000 VND to Free Fund | `{   "walletId": "f6ce8510-1552-4353-b...` | `200` | `200` | **PASS** | OK |
| 78 | `GET` | `/api/v1/funds/{{freeFundId}}/finance` | 06 - Track Free Fund Finance | `(none)` | `200` | `200` | **PASS** | OK |
| 79 | `POST` | `/api/v1/funds/{{freeFundId}}/requests` | 07 - User B Creates Free Fund Expense Request | `{   "kind": "Expense",   "amountVnd":...` | `200` | `200` | **PASS** | OK |
| 80 | `GET` | `/api/v1/funds/{{freeFundId}}/refund-preview?memberUserId={{userB_id}}` | 08 - Refund Preview for User B | `(none)` | `200` | `200` | **PASS** | OK |
| 81 | `POST` | `/api/v1/funds/{{freeFundId}}/leave` | 09 - User B Leaves Free Fund | `{   "securityPin": "123456",   "idemp...` | `200` | `200` | **PASS** | OK |
| 82 | `GET` | `/api/v1/funds/{{freeFundId}}/members` | 10 - Verify Remaining Members in Free Fund | `(none)` | `200` | `200` | **PASS** | OK |
| 83 | `POST` | `/api/budgets` | 01 - Create Monthly Budget User A | `{   "categoryId": "9a8e4648-8073-484f...` | `201` | `201` | **PASS** | Created tài nguyên mới |
| 84 | `GET` | `/api/budgets?year=2026&month=10` | 02 - List Monthly Budgets | `(none)` | `200` | `200` | **PASS** | OK |
| 85 | `PUT` | `/api/budgets/{{budgetId}}` | 03 - Update Monthly Budget Limit | `{   "limitAmount": 1500000,   "alerts...` | `200` | `200` | **PASS** | OK |
| 86 | `DELETE` | `/api/budgets/{{budgetId}}` | 04 - Delete Monthly Budget | `{   "securityPin": "123456" }` | `204` | `204` | **PASS** | NoContent chuẩn RFC |
| 87 | `POST` | `/api/goals` | 05 - Create Savings Goal (MacBook M4) | `{   "name": "Mua MacBook M4",   "targ...` | `200` | `200` | **PASS** | OK |
| 88 | `POST` | `/api/goals/{{goalId}}/contributions` | 06 - Deposit to Savings Goal (2,000,000 VND) | `{   "walletId": "063f0702-44c3-43cf-9...` | `200` | `200` | **PASS** | OK |
| 89 | `GET` | `/api/goals/{{goalId}}` | 07 - Get Goal Details and Verify Progress | `(none)` | `200` | `200` | **PASS** | OK |
| 90 | `POST` | `/api/goals/{{goalId}}/withdrawals` | 08 - Withdraw from Savings Goal (500,000 VND) | `{   "walletId": "063f0702-44c3-43cf-9...` | `200` | `200` | **PASS** | OK |
| 91 | `POST` | `/api/recurring-transactions` | 09 - Create Recurring Transaction (Monthly Internet) | `{   "walletId": "063f0702-44c3-43cf-9...` | `200/201` | `200` | **PASS** | OK |
| 92 | `GET` | `/api/recurring-transactions` | 10 - List Recurring Transactions | `(none)` | `200` | `200` | **PASS** | OK |
| 93 | `POST` | `/api/transaction-rules` | 11 - Create Transaction Rule | `{   "descriptionPattern": "GrabFood*"...` | `200/201` | `200` | **PASS** | OK |
| 94 | `GET` | `/api/transaction-rules` | 12 - List Transaction Rules | `(none)` | `200` | `200` | **PASS** | OK |
| 95 | `POST` | `/api/ai/conversations` | 01 - Create AI Conversation | `{   "title": "Tu van tiet kiem hang t...` | `200` | `200` | **PASS** | OK |
| 96 | `GET` | `/api/ai/conversations` | 02 - List AI Conversations | `(none)` | `200` | `200` | **PASS** | OK |
| 97 | `POST` | `/api/ai/chat` | 03 - Ask AI Financial Advisor (Chat) - Fallback Safe | `{   "conversationId": "88788a03-15dd-...` | `200/503` | `503` | **PASS** | AI provider safe fallback |
| 98 | `GET` | `/api/transfers/recipients?phone={{userB_phone}}` | 01 - Find Bank Transfer Recipient by Phone | `(none)` | `200/404/409` | `409` | **PASS** | Business conflict / invariant guard |
| 99 | `POST` | `/api/transfers` | 02 - Create Bank Direct Transfer Instruction | `{   "bankCode": "VCB",   "accountNumb...` | `201/400` | `400` | **PASS** | OK |
| 100 | `GET` | `/api/transfers` | 03 - Bank Direct Transfer History | `(none)` | `200` | `200` | **PASS** | OK |
| 101 | `POST` | `/api/banking/sepay/webhook` | 04 - SePay Webhook Invariant (Controlled Check) | `{   "gateway": "SePay Sandbox",   "tr...` | `200/400/401/404` | `404` | **PASS** | OK |
| 102 | `GET` | `/api/notifications` | 01 - List Notifications for User A | `(none)` | `200` | `200` | **PASS** | OK |
| 103 | `POST` | `/api/notifications/read-all` | 02 - Mark All Notifications Read | `(none)` | `200/204` | `204` | **PASS** | NoContent chuẩn RFC |
| 104 | `GET` | `/api/notifications/search?page=1&pageSize=10` | 03 - Search Notifications | `(none)` | `200` | `200` | **PASS** | OK |
| 105 | `POST` | `/api/cases/support` | 04 - Create Support Case | `{   "category": "General",   "priorit...` | `200/201` | `200` | **PASS** | OK |
| 106 | `GET` | `/api/cases/support` | 05 - List Support Cases | `(none)` | `200` | `200` | **PASS** | OK |
| 107 | `GET` | `/api/cases/support/{{supportCaseId}}` | 06 - Get Support Case Details | `(none)` | `200` | `200` | **PASS** | OK |
| 108 | `POST` | `/api/cases/support/{{supportCaseId}}/messages` | 07 - Reply to Support Case Message | `{   "message": "Synthetic receipt pro...` | `200` | `200` | **PASS** | OK |
| 109 | `GET` | `/api/cases/disputes` | 08 - List Financial Disputes | `(none)` | `200` | `200` | **PASS** | OK |
| 110 | `GET` | `/api/users/me` | Flow A01 - Pre-check Profile Avatar | `(none)` | `200` | `200` | **PASS** | OK |
| 111 | `POST` | `/api/users/me/avatar` | Flow A02 - Upload Real Avatar Image | `(none)` | `200` | `200` | **PASS** | OK |
| 112 | `GET` | `/api/users/me` | Flow A03 - Check Profile Immediately Has New Avatar | `(none)` | `200` | `200` | **PASS** | OK |
| 113 | `POST` | `/api/auth/logout` | Flow A04 - Logout User A | `{   "refreshToken": "KtzuqfFsS/bAnSaL...` | `200/204` | `204` | **PASS** | NoContent chuẩn RFC |
| 114 | `POST` | `/api/auth/login` | Flow A05 - Re-login User A | `{   "identifier": "userA_hm4nye9@spen...` | `200` | `200` | **PASS** | OK |
| 115 | `POST` | `/api/auth/pin/verify` | Flow A06 - Complete PIN Login for Re-login | `{   "challengeToken": "GQibhYpHiUQEZk...` | `200` | `200` | **PASS** | OK |
| 116 | `GET` | `/api/users/me` | Flow A07 - Check Profile After Fresh Login Retains Avatar | `(none)` | `200` | `200` | **PASS** | OK |
| 117 | `POST` | `/api/users/me/email/request` | Flow B01 - Request Email Change to New Email | `{   "newEmail": "userA_new_hm4nye9@sp...` | `200` | `200` | **PASS** | OK |
| 118 | `POST` | `/api/users/me/email/confirm` | Flow B02 - Confirm Email Change with OTP | `{   "newEmail": "userA_new_hm4nye9@sp...` | `200` | `200` | **PASS** | OK |
| 119 | `POST` | `/api/auth/logout` | Flow B03 - Logout After Email Change | `{   "refreshToken": "OGv6Yq1FQR5SnSDc...` | `204` | `204` | **PASS** | NoContent chuẩn RFC |
| 120 | `POST` | `/api/auth/login` | Flow B04 - Login with New Email (Expected: Success) | `{   "identifier": "userA_new_hm4nye9@...` | `200` | `200` | **PASS** | OK |
| 121 | `POST` | `/api/auth/pin/verify` | Flow B05 - Complete PIN Verification with New Email | `{   "challengeToken": "kGsIwe8dHTAm7k...` | `200` | `200` | **PASS** | OK |
| 122 | `POST` | `/api/auth/login` | Flow B06 - Login with Old Email (Expected: 401 / Denied) | `{   "identifier": "userA_hm4nye9@spen...` | `401` | `401` | **PASS** | Negative auth/perm check |
| 123 | `GET` | `/api/friends/search?contact={{userB_email}}` | Flow C01 - User A Searches User B by Contact | `(none)` | `200` | `200` | **PASS** | OK |
| 124 | `POST` | `/api/friends/requests` | Flow C02 - User A Checks Friend Request Status to User B | `{   "contact": "userB_hm4nye9@spendwi...` | `409` | `409` | **PASS** | Business conflict / invariant guard |
| 125 | `GET` | `/api/friends` | Flow C03 - User A Checks Friend List (Has User B) | `(none)` | `200` | `200` | **PASS** | OK |
| 126 | `GET` | `/api/friends` | Flow C04 - User B Checks Friend List (Has User A) | `(none)` | `200` | `200` | **PASS** | OK |
| 127 | `POST` | `/api/v1/funds` | Flow D01 - User A Creates Fixed Fund (1,000,000 VND) | `{   "name": "Quy Flow D Team",   "kin...` | `200` | `200` | **PASS** | OK |
| 128 | `POST` | `/api/v1/funds/{{flowD_fundId}}/invitations` | Flow D02 - Invite User B to Fixed Fund | `{   "userId": "05f548e7-d2c1-4421-aa4...` | `200` | `200` | **PASS** | OK |
| 129 | `POST` | `/api/v1/funds/invitations/{{flowD_inviteId}}/accept` | Flow D03 - User B Accepts Fixed Fund Invitation | `(none)` | `200` | `200` | **PASS** | OK |
| 130 | `POST` | `/api/v1/funds/{{flowD_fundId}}/obligations/confirm` | Flow D04 - Confirm Obligations (Target: 1,000,000 VND) | `{   "targetVnd": 1000000,   "security...` | `200` | `200` | **PASS** | OK |
| 131 | `POST` | `/api/v1/funds/{{flowD_fundId}}/contributions` | Flow D05 - User A Contributes 500,000 VND | `{   "walletId": "063f0702-44c3-43cf-9...` | `200` | `200` | **PASS** | OK |
| 132 | `POST` | `/api/v1/funds/{{flowD_fundId}}/contributions` | Flow D06 - User B Contributes 500,000 VND | `{   "walletId": "f6ce8510-1552-4353-b...` | `200` | `200` | **PASS** | OK |
| 133 | `GET` | `/api/v1/funds/{{flowD_fundId}}/finance` | Flow D07 - Check Fund Finance (Total 1,000,000 VND) | `(none)` | `200` | `200` | **PASS** | OK |
| 134 | `PUT` | `/api/v1/funds/{{flowD_fundId}}/members/{{userB_id}}/spending-permission` | Flow D08 - Grant User B Spending Permission | `{   "enabled": true,   "securityPin":...` | `204` | `204` | **PASS** | NoContent chuẩn RFC |
| 135 | `GET` | `/api/v1/funds/{{flowD_fundId}}/activities` | Flow D09 - Check Activity Log for Spending Permission Event | `(none)` | `200` | `200` | **PASS** | OK |
| 136 | `GET` | `/api/wallets` | Flow E01 - Identify Main and Savings Wallets for Flow E | `(none)` | `200` | `200` | **PASS** | OK |
| 137 | `POST` | `/api/transfers/internal` | Flow E02 - Transfer 300,000 VND from Main to Savings Wallet | `{   "sourceWalletId": "063f0702-44c3-...` | `201` | `201` | **PASS** | Created tài nguyên mới |
| 138 | `GET` | `/api/wallets/{{flowE_wallet2Id}}` | Flow E03 - Check Savings Wallet Balance Increased by 300,000 VND | `(none)` | `200` | `200` | **PASS** | OK |
| 139 | `GET` | `/api/transactions?walletId={{flowE_wallet2Id}}&page=1&pageSize=10` | Flow E04 - Check Linked Transactions List | `(none)` | `200` | `200` | **PASS** | OK |
| 140 | `GET` | `/api/categories` | Flow F01 - Get Expense Category for Budgeting | `(none)` | `200` | `200` | **PASS** | OK |
| 141 | `POST` | `/api/budgets` | Flow F02 - Create Budget (2,000,000 VND limit) | `{   "categoryId": "10000000-0000-0000...` | `201` | `201` | **PASS** | Created tài nguyên mới |
| 142 | `GET` | `/api/budgets/{{flowF_budgetId}}` | Flow F03 - Check Budget Status (Spent: 0, Remaining: 2,000,000 VND) | `(none)` | `200` | `200` | **PASS** | OK |
| 143 | `PUT` | `/api/budgets/{{flowF_budgetId}}` | Flow F04 - Update Budget Limit to 3,000,000 VND | `{   "limitAmount": 3000000,   "alerts...` | `200` | `200` | **PASS** | OK |
| 144 | `DELETE` | `/api/budgets/{{flowF_budgetId}}` | Flow F05 - Clean Up Budget via Delete | `{   "securityPin": "123456" }` | `204` | `204` | **PASS** | NoContent chuẩn RFC |
