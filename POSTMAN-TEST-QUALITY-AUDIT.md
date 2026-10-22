# SPENDWISE — BÁO CÁO KIỂM TOÁN CHẤT LƯỢNG TEST POSTMAN E2E (TEST QUALITY & GAP AUDIT)

> **Mục tiêu:** Rà soát chuyên sâu chất lượng của bộ Postman Collection E2E (`SpendWise-User-API.postman_collection.json`), bóc tách tính nghiêm ngặt của các Assertions, phân loại chi tiết các loại PASS, phát hiện các điểm sai lệch contract và xác định danh mục lỗ hổng nghiệp vụ Backend User chưa có kịch bản E2E.
> **Nguyên tắc:** Báo cáo phản biện trung thực (Adversarial Quality Bar), không tô hồng kết quả, căn cứ trực tiếp trên source code Backend HEAD `c2e0abe` và database cô lập SQL02 `SpendWise_E2E_G4`.

---

## 1. PHÂN BỐ VÀ BÓC TÁCH BẢN CHẤT CÁC REQUEST KIỂM THỬ

Toàn bộ **144 requests** được thực thi trong Newman không phải tất cả đều là "Happy-path gọi thành công trả về 200". Để đánh giá độ tin cậy thực tế, chúng tôi phân loại thành 3 nhóm rõ rệt:

| Phân Loại Mục Tiêu Kiểm Thử | Số Lượng Request | Tỷ Lệ % | Bản Chất & Ý Nghĩa Kỹ Thuật |
| :--- | :---: | :---: | :--- |
| **Happy-Path (Success Flows)** | **132** | **91.7%** | Các request thực thi thành công nghiệp vụ, tạo mới tài nguyên, truy vấn dữ liệu hoặc cập nhật trạng thái hợp lệ (HTTP 200, 201, 204, 202). |
| **Expected-Error / Rejection (Negative & Invariants)** | **8** | **5.5%** | Các bài test cố tình gửi dữ liệu sai để kiểm tra cơ chế bảo vệ của Backend (Invariant guards): Đăng ký trùng email (409), Đăng nhập khi chưa xác thực OTP (403), Mật khẩu sai (401), Sai mã PIN (401), Vượt giới hạn 3 ví cá nhân (409 `wallet_limit_reached`), Rate limit OTP (429). |
| **External Fallback / Out-of-Scope Checks** | **4** | **2.8%** | Các bài test kiểm tra cơ chế suy thoái an toàn (Graceful degradation) khi thiếu dịch vụ bên ngoài: AI Advice khi thiếu key (503 `AI_PROVIDER_UNAVAILABLE`), Chuyển khoản ngân hàng khi chưa liên kết Bank account (409 `active_bank_required`), Webhook SePay invariant check (400/404). |
| **TỔNG CỘNG** | **144** | **100.0%** | **Toàn bộ 144/144 requests PASS đúng theo thiết kế của từng test case.** |

---

## 2. KIỂM TOÁN TÍNH NGHIÊM NGẶT CỦA CÁC ASSERTIONS (ASSERTION RIGOR AUDIT)

Khi rà soát toàn bộ mã kiểm thử JavaScript trong `event[listen=test].script.exec` của 144 requests, chúng tôi phát hiện **41 requests** sử dụng cú pháp assertion chấp nhận nhiều mã trạng thái HTTP (Multi-status inclusion).

### 2.1. Nhóm Assertions Chấp Nhận Được (Justified Flexibility)
- **`[200, 204]` (12 requests):** Thường gặp ở các thao tác như `POST /api/security-pin/verify`, `PUT /api/security-pin/change`, `DELETE /api/auth/sessions/{id}`. Điều này hợp lệ vì RFC 7231 cho phép server trả về 204 NoContent khi không có body hoặc 200 OK kèm payload xác nhận.
- **`[200, 201]` (18 requests):** Thường gặp ở `POST /api/wallets`, `POST /api/goals`, `POST /api/v1/funds`. Cả 2 status code đều đại diện cho thành công tạo mới tài nguyên.
- **`[200, 202]` (1 request):** Gặp ở `POST /api/auth/forgot-password`. Chấp nhận cả trường hợp server xử lý gửi mail đồng bộ (200) hoặc đẩy vào Outbox ngầm (202).
- **`[200, 429]` (3 requests):** Gặp ở các API gửi lại OTP (`/resend`). Khi chạy toàn bộ test liên tục trong thời gian ngắn, rate limit có thể kích hoạt trả 429 (Too Many Requests), hoặc nếu cooldown đã hết thì trả 200 OK. Cả hai đều chứng minh hệ thống hoạt động ổn định.

### 2.2. Nhóm Assertions Cần Siết Chặt (Loose Assertions - High Risk)
Có **8 requests** sử dụng assertion quá rộng, có nguy cơ bỏ lọt lỗi nghiêm trọng:

1. **SePay Webhook Verification (`POST /api/v1/bank-hub/sepay/webhook`):**
   - **Assertion hiện tại:** `pm.expect([200, 400, 401, 404]).to.include(pm.response.code)`.
   - **Rủi ro:** Nếu router bị hỏng hoàn toàn hoặc endpoint bị gỡ bỏ dẫn đến **HTTP 404 Not Found**, test case **vẫn tính là PASS**! Tương tự, nếu middleware xác thực API key bị sập trả 401 Unauthorized, test vẫn pass.
   - **Khắc phục đề xuất:** Tách biệt rõ 2 test: Một test gửi payload có `Api-Key` hợp lệ kỳ vọng `200 OK`, và một negative test gửi sai key kỳ vọng chính xác `401 Unauthorized`. Không bao giờ gộp `[200, 400, 401, 404]`.
2. **Bank Direct Transfer (`POST /api/banking/transfers`):**
   - **Assertion hiện tại:** `pm.expect([200, 201, 400, 409]).to.include(pm.response.code)`.
   - **Rủi ro:** Khi tài khoản không đủ tiền, thiếu liên kết ngân hàng hoặc tham số sai, việc gộp cả 400/409 với 200/201 khiến test case không xác định được luồng đang chạy là thành công hay thất bại.
   - **Khắc phục đề xuất:** Nếu môi trường test không có Open Banking sandbox thật, test case phải được đặt tên rõ ràng là `Negative Check: Transfer without Linked Bank` và assert chặt chẽ duy nhất `409 Conflict` (`active_bank_required`).
3. **Login với sai mật khẩu:**
   - **Assertion hiện tại:** `pm.expect([400, 401]).to.include(pm.response.code)`.
   - **Đánh giá:** Controller `AuthController.Login` trả về chính xác `401 Unauthorized`. Nên cố định `pm.response.to.have.status(401)` thay vì chấp nhận cả 400.

---

## 3. ĐỐI SOÁT VÀ ĐÍNH CHÍNH CONTRACT DISCREPANCIES

Qua kiểm toán chi tiết đối chiếu giữa Postman Collection, Newman Report và source code C# Controllers/DTOs, chúng tôi đã phát hiện và đính chính 3 sai lệch contract quan trọng:

### 3.1. Phân biệt PIN Login (2 Bước) vs PIN Nghiệp Vụ (Re-authentication)
- **Sai lệch trước đây:** Tài liệu báo cáo trước đây ghi nhầm: *"Endpoint `POST /api/auth/pin/verify` trả về 204 NoContent"*.
- **Thực tế trong source code:**
  - `POST /api/auth/pin/verify` (`AuthController.VerifyPinLogin`): Nhận `CompleteSecurityPinLoginRequest(ChallengeToken, Pin)`. Đây là **bước 2 của quy trình đăng nhập 2 lớp**. Endpoint này trả về **HTTP 200 OK** kèm theo `AuthResponse` (`accessToken`, `refreshToken`, `user`).
  - `POST /api/security-pin/verify` (`SecurityPinController.Verify`): Nhận `VerifyPinRequest(Pin)`. Đây là API kiểm tra mã PIN trong phiên đã đăng nhập (dùng cho các thao tác nhạy cảm). Endpoint này trả về **HTTP 204 NoContent**.
- **Đã đính chính:** Đã cập nhật đúng đặc tả vào Section 4.1 của `POSTMAN-API-TEST-REPORT.md`.

### 3.2. Route Quên Mật Khẩu (Forgot Password)
- **Sai lệch trước đây:** Báo cáo ghi route là `POST /api/auth/password/forgot`.
- **Thực tế trong source code:** Controller route thực tế được định nghĩa tại `AuthController.cs`:
  ```csharp
  [HttpPost("forgot-password")]
  public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
  ```
  Route đúng là `POST /api/auth/forgot-password`, trả về **HTTP 202 Accepted** với body rỗng `{}` khi gửi mail Outbox thành công.
- **Đã đính chính:** Sửa tên route trong báo cáo tổng quan.

### 3.3. Contract Xác Nhận Đổi Email (Confirm Email Change)
- **Sai lệch trước đây:** Trong collection `Flow B02 - Confirm Email Change with Valid OTP`, payload gửi lên là:
  ```json
  {
    "newEmail": "{{new_email_userA}}",
    "otp": "{{static_otp}}"
  }
  ```
- **Thực tế trong source code:** DTO Backend định nghĩa:
  ```csharp
  public record ConfirmChangeEmailRequest([Required] string Otp);
  ```
  Backend chỉ nhận thuộc tính `otp`. Địa chỉ `newEmail` đã được lưu trữ an toàn trong bảng `EmailChangeRequest` ở bước khởi tạo `POST /api/users/me/email/change`. Việc truyền dư thừa `newEmail` tuy không làm lỗi backend do System.Text.Json bỏ qua thuộc tính thừa, nhưng vi phạm tính chuẩn xác của contract.
- **Đã đính chính:** Đã cập nhật payload trong `SpendWise-User-API.postman_collection.json` và `backend/postman/SpendWise-User-API.postman_collection.json` về đúng chuẩn `{ "otp": "{{static_otp}}" }`.

---

## 4. DANH MỤC LỖ HỔNG NGHIỆP VỤ CHƯA CÓ E2E ĐẦY ĐỦ (UNTESTED BUSINESS GAPS)

Mặc dù 144 requests kiểm thử đã bao phủ rất tốt các luồng cốt lõi của User cá nhân, vẫn còn **131/215 endpoints (60.9%)** chưa được kiểm thử trong Postman E2E. Cụ thể có 6 khoảng trống nghiệp vụ lớn:

### 4.1. Chu Trình Phê Duyệt / Bác Bỏ Chi Tiêu Quỹ Đóng Tự Do (Free Fund Request Review)
- **Hiện trạng test:** Test case số 07 của Quỹ Tự do chỉ mới gọi `POST /api/v1/funds/{id}/requests` để User B tạo yêu cầu chi tiêu 200,000 VND.
- **Khoảng trống:**
  - Chưa test endpoint phê duyệt: `POST /api/v1/funds/requests/{id}/review` với hành động `Approve` hoặc `Reject`.
  - Chưa kiểm tra logic bỏ phiếu: Trong Quỹ tự do, khi các thành viên bỏ phiếu đồng thuận thì tiền từ quỹ chung mới được giải ngân về ví người yêu cầu.
  - Chưa kiểm tra endpoint hủy đề xuất: `POST /api/v1/funds/requests/{id}/cancel`.

### 4.2. Quản Trị Quỹ, Bầu Cử & Bỏ Phiếu Trưởng Quỹ (Elections & Governance)
- **Hiện trạng test:** Trong `V1FundsController`, toàn bộ 7 endpoints liên quan đến bầu cử trưởng quỹ chưa có bất kỳ request Postman nào:
  - `POST /api/v1/funds/{id}/elections` (Mở bầu cử).
  - `POST /api/v1/funds/elections/{id}/candidates` (Ứng cử).
  - `POST /api/v1/funds/elections/{id}/ballots` (Bỏ phiếu tín nhiệm).
  - `POST /api/v1/funds/elections/{id}/advance` (Tổng kết vòng bầu cử).
  - `POST /api/v1/funds/elections/{id}/accept` / `reject` (Tiếp nhận chức vụ trưởng quỹ).
  - `POST /api/v1/funds/{id}/ownership-offers` (Chuyển giao quyền sở hữu quỹ trực tiếp).

### 4.3. Kiểm Tra Cách Ly Dữ Liệu Chéo Người Dùng (Cross-User Isolation & Privacy Guards)
- **Hiện trạng test:** Hầu hết các test chỉ kiểm tra trên cùng 1 user (User A thao tác ví A, User B thao tác ví B).
- **Khoảng trống:**
  - Cần bổ sung các negative tests: User A dùng token của mình cố tình gọi `GET /api/wallets/{id_cua_User_B}` -> Phải bị từ chối với **403 Forbidden** hoặc **404 Not Found**.
  - User A cố tình gọi `DELETE /api/budgets/{id_cua_User_B}` -> Phải trả về **403 Forbidden**.
  - User A cố tình xem bằng chứng tài chính `GET /api/evidence/{id_cua_User_B}` -> Phải từ chối.

### 4.4. Vòng Đời Ngân Sách Sau Khi Chi Tiêu (Budget Tracking Post-Expense)
- **Hiện trạng test:** Test case Flow F kiểm tra tạo ngân sách 3,000,000 VND và đọc ngân sách khi `spentAmount = 0`. Sau đó tạo giao dịch chi tiêu 500,000 VND nhưng **chưa gọi lại `GET /api/budgets`** để assert trường `spentAmount` đã nhảy lên đúng `500,000 VND` và `percentage` đạt `16.67%`.

### 4.5. Vòng Đời Thực Thi Hành Động AI (AI Action Proposal Execution)
- **Hiện trạng test:** Hiện chỉ có 3 requests kiểm tra tạo cuộc hội thoại AI (`POST /api/ai/conversations`) và gửi chat message.
- **Khoảng trống:**
  - AI trong SpendWise có khả năng đề xuất hành động tài chính (ví dụ: tạo ngân sách, điều chỉnh ví).
  - Các endpoints `POST /api/ai/actions/{id}/execute` và `POST /api/ai/actions/{id}/cancel` hoàn toàn chưa được kiểm thử.

### 4.6. Phân Hệ Legacy Groups & Open Banking Hub
- **Phân hệ Groups (`GroupsController`):** Toàn bộ **29/29 endpoints** của kiến trúc chia tiền nhóm cũ chưa được test trong bộ Postman này (do hệ thống đang ưu tiên chuyển dịch sang `v1/funds`).
- **Phân hệ Banking (`BankConnectionsController`, `BankTransactionsController`):** **15/15 endpoints** chưa được test happy-path do phụ thuộc vào hạ tầng liên kết tài khoản ngân hàng thực tế.

---

## 5. TỔNG KẾT VÀ BƯỚC ĐI TIẾP THEO

1. **Về Báo cáo & Thống kê:** Báo cáo đã được đồng bộ hóa với thực tế:
   - **215 User Endpoints** thực tế trong code.
   - **84 Endpoints** được bao phủ trực tiếp (39.1%).
   - **144 Requests** thực thi trong Newman (132 success, 8 rejection, 4 fallback).
   - **144 Assertions** đã vượt qua trên SQL02 `SpendWise_E2E_G4`.
2. **Về Khuyến nghị Kỹ thuật:**
   - Trong đợt tới, cần siết chặt 8 assertions quá rộng (đặc biệt là Webhook SePay không chấp nhận 404).
   - Bổ sung kịch bản kiểm thử cho chu trình phê duyệt chi tiêu Free Fund và đối soát ngân sách sau khi trừ tiền.
   - Tuyệt đối giữ nguyên nguyên tắc không sửa Frontend/Mobile và không xâm phạm cơ sở dữ liệu `SQL01`.
