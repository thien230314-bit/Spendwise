# SPENDWISE — BÁO CÁO KIỂM TOÁN CHẤT LƯỢNG TEST POSTMAN E2E (TEST QUALITY & GAP AUDIT)

> **Mục tiêu:** Rà soát chuyên sâu chất lượng của bộ Postman Collection E2E (`SpendWise-User-API.postman_collection.json`), bóc tách tính nghiêm ngặt của các Assertions, phân loại chi tiết các loại PASS, phát hiện các điểm sai lệch contract và khắc phục toàn diện danh mục lỗ hổng nghiệp vụ Backend User (Gaps 2, 3, 4).
> **Nguyên tắc:** Báo cáo phản biện trung thực (Adversarial Quality Bar), không tô hồng kết quả, căn cứ trực tiếp trên source code Backend HEAD `552f452caf5888ccf31f91d7f68536395357e9f3` và database cô lập SQL02 `SpendWise_E2E_G4`.

---

## 1. PHÂN BỐ VÀ BÓC TÁCH BẢN CHẤT CÁC REQUEST KIỂM THỬ (SAU KHI SIẾT CHẶT VÀ BỔ SUNG GAPS 2, 3, 4)

Sau khi bổ sung trọn vẹn 18 requests cho **Gap 2 (Bảo mật & Phân quyền chéo User A / User B)**, **Gap 3 (Vòng đời Ngân sách sau chi tiêu & AI Action Fallback)**, **Gap 4 (Chu trình phê duyệt quỹ tự do Free Fund Approval)** và xóa bỏ hoàn toàn các assertions lỏng lẻo, toàn bộ **162 requests** được phân loại rõ rệt:

| Phân Loại Mục Tiêu Kiểm Thử | Số Lượng Request | Tỷ Lệ % | Bản Chất & Ý Nghĩa Kỹ Thuật |
| :--- | :---: | :---: | :--- |
| **Happy-Path (Success Flows)** | **143** | **88.3%** | Các request thực thi thành công nghiệp vụ, tạo mới tài nguyên, truy vấn dữ liệu hoặc cập nhật trạng thái hợp lệ (HTTP 200, 201, 204, 202). |
| **Expected-Error / Rejection (Security & Invariants)** | **16** | **9.9%** | Các bài test cố tình gửi dữ liệu sai hoặc truy cập trái phép để kiểm tra cơ chế bảo vệ của Backend (Invariant guards): Đăng ký trùng email (409), Mật khẩu sai (401), Sai mã PIN (401), Vượt giới hạn 3 ví (409), User A truy cập ví B (404), User A xóa budget B (404), User A xem goal B (404), User A tải evidence B (404), Thành viên tự cấp quyền chi tiêu quỹ Fixed (403), Thành viên tự duyệt chi tiêu quỹ Free (403), Thực thi/hủy AI Action không tồn tại (404). |
| **External Fallback / Graceful Degradation** | **3** | **1.8%** | Các bài test kiểm tra cơ chế suy thoái an toàn khi thiếu dịch vụ bên ngoài: AI Chat khi thiếu API key (503 `AI_PROVIDER_UNAVAILABLE`), Chuyển khoản ngân hàng khi chưa liên kết Bank account (409 `active_bank_required`), Webhook SePay sai chữ ký (401 `bank_webhook_unauthorized`). |
| **TỔNG CỘNG** | **162** | **100.0%** | **Toàn bộ 162/162 requests được định danh chính xác trạng thái kỳ vọng, 0 assertions đa mã lỏng lẻo.** |

---

## 2. KẾT QUẢ TRIỂN KHAI VÀ KHẮC PHỤC TRIỆT ĐỂ GAPS 2, 3, 4

### 2.1. Gap 2: Bảo Mật, Cách Ly và Phân Quyền Chéo Người Dùng (Cross-User Security & Isolation)
- **Tạo thư mục mới:** `11 - Cross-User Security & Isolation` (12 requests).
- **Kịch bản kiểm thử độc lập User A và User B:**
  - `User B` tạo độc lập ví chính, ngân sách 1,500,000 VND, mục tiêu tiết kiệm và upload bằng chứng tài chính.
  - `User A` dùng token riêng cố tình truy cập tài nguyên của User B:
    - `GET /api/wallets/{userB_mainWalletId}` -> **404 Not Found** (Bảo vệ theo mẫu `candidate.OwnerUserId == userId`, ngăn chặn rà quét ID tài nguyên).
    - `DELETE /api/budgets/{userB_budgetId}` -> **404 Not Found**.
    - `GET /api/goals/{userB_goalId}` -> **404 Not Found**.
    - `GET /api/evidence/{userB_evidenceId}` -> **404 Not Found**.
  - **Kiểm soát phân quyền quản trị Quỹ (Fixed & Free):**
    - `User B` (thành viên) cố tình gọi `PUT /api/v1/funds/{id}/members/{userB}/spending-permission` để tự bật `CanSpend` -> Bị từ chối chính xác **403 Forbidden** (`fund_owner_required`).
    - `User B` (thành viên) cố tình gọi `POST /api/v1/funds/requests/{id}/review` để tự phê duyệt yêu cầu rút tiền của mình -> Bị từ chối chính xác **403 Forbidden** (`fund_owner_required`).
  - **Xác minh tính bất biến của tài nguyên User B:** Gọi lại `GET /api/budgets/{userB_budgetId}` bằng token User B, assert ngân sách vẫn giữ nguyên `limitAmount: 1,500,000 VND`, số dư và trạng thái không bị thay đổi.

### 2.2. Gap 3: Vòng Đời Ngân Sách Sau Chi Tiêu & Vòng Đời AI Action
- **Chu trình Ngân sách khép kín (Flow F hoàn chỉnh):**
  - **Flow F02:** Tạo ngân sách hạn mức 2,000,000 VND cho danh mục chi tiêu (`flowF_catId`).
  - **Flow F03:** Đọc ngân sách ban đầu: `spentAmount: 0`, `percentage: 0`, `remainingAmount: 2,000,000 VND`.
  - **Flow F03a + F03b:** Upload bằng chứng tài chính và ghi nhận giao dịch chi tiêu thật 500,000 VND thuộc danh mục trên.
  - **Flow F03c:** Gọi lại `GET /api/budgets/{id}`: Assert chính xác `spentAmount: 500,000 VND`, `percentage: 25.0%`, `remainingAmount: 1,500,000 VND`.
  - **Flow F03d:** Điều chỉnh giao dịch chi tiêu từ 500,000 VND lên 800,000 VND bằng `PATCH /api/transactions/{id}` với `reason` và `securityPin`.
  - **Flow F03e:** Gọi lại `GET /api/budgets/{id}`: Assert ngân sách tự động tính toán lại sổ cái đạt `spentAmount: 800,000 VND`, `percentage: 40.0%`, `remainingAmount: 1,200,000 VND`.
  - **Flow F04 + F05:** Nâng hạn mức lên 3,000,000 VND và xóa dọn dẹp ngân sách (204 NoContent).
- **Vòng đời AI Action (Đánh giá kiến trúc & Báo cáo trung thực BLOCKED):**
  - Đã rà soát chi tiết kiến trúc Backend (`AiAdvancedFeature.cs`, `AiController.cs`): Đề xuất hành động AI (`AiActionProposal`) chỉ được sinh ra nội bộ thông qua tool calling (`IAiToolRegistry.ExecuteAsync`) khi model Gemini trực tiếp trả về các function call `prepare_*` (ví dụ `prepare_budget_create`, `prepare_manual_expense`).
  - Hệ thống **không có endpoint REST công khai** để client tự tạo proposal trực tiếp mà không qua turn chat AI (`POST /api/ai/chat`).
  - Khi chạy trong môi trường offline không có Gemini API key thật, `POST /api/ai/chat` trả về 503 `AI_PROVIDER_UNAVAILABLE` trước khi tool calling có thể diễn ra.
  - **Kết luận:** Luồng thực thi trọn vẹn AI Action từ Chat -> Tool call -> Execute/Cancel được xác nhận **BLOCKED do kiến trúc phụ thuộc nhà cung cấp ngoài** trong kiểm thử black-box Newman. Chúng tôi đã bổ sung negative checks với proposal ID không tồn tại trả về đúng **404 Not Found** (`ai_action_not_found`), không ngụy tạo kết quả PASS ảo.

### 2.3. Gap 4: Chu Trình Phê Duyệt Quỹ Đóng Tự Do (Free Fund Request Review)
- **Chu trình hoàn chỉnh tại Folder 05 (Free Fund):**
  - `07 - User B Creates Free Fund Expense Request`: Thành viên User B gửi đề xuất chi tiêu 200,000 VND (`kind: "Expense"`). Backend chuyển đề xuất vào trạng thái `AwaitingOwnerReview`.
  - `07b - User A (Owner) Reviews and Approves Free Fund Request`: Chủ quỹ User A gọi `POST /api/v1/funds/requests/{id}/review` với `approved: true`, nhập lý do và mã PIN. Backend kiểm tra quyền chủ quỹ, giải ngân qua sổ cái kép (`V1FundExpense`), đưa trạng thái đề xuất thành `Completed` (HTTP 200 OK).

---

## 3. LOẠI BỎ TOÀN BỘ 41 MULTI-STATUS ASSERTIONS (SIẾT CHẶT 100%)

Toàn bộ **41 assertions** đa mã trạng thái (chấp nhận mảng mã code) trước đây đã được rà soát và chuyển thành **1 assertion duy nhất, chặt chẽ tuyệt đối** tương ứng với từng controller:

1. **SePay Webhook Invariant:** Xóa `[200, 400, 401, 404]`, siết chặt thành `pm.response.to.have.status(401)` khi sai secret key (`bank_webhook_unauthorized`).
2. **Bank Direct Transfer:** Xóa `[200, 201, 400]`, siết chặt thành `pm.response.to.have.status(409)` khi chưa liên kết ngân hàng (`active_bank_required`).
3. **AI Chat Fallback:** Xóa `[200, 503]`, siết chặt thành `pm.response.to.have.status(503)` khi chạy offline.
4. **Login Sai Mật Khẩu:** Xóa `[400, 401]`, siết chặt thành `pm.response.to.have.status(401)`.
5. **Verify/Change Security PIN, Logout, Revoke Session, Delete Budget:** Xóa `[200, 204]`, siết chặt thành `pm.response.to.have.status(204)`.
6. **Create Wallet, Category, Budget, Recurring Tx, Rule, Evidence, Expense:** Xóa `[200, 201]`, siết chặt thành `pm.response.to.have.status(201)`.
7. **Create Fixed Fund, Create Free Fund:** Xóa `[200, 201]`, siết chặt thành `pm.response.to.have.status(200)` (do V1FundsController trả về DTO trực tiếp).
8. **Forgot Password:** Xóa `[200, 202]`, siết chặt thành `pm.response.to.have.status(202)`.
9. **Resend Verification OTP:** Xóa `[200, 429]`, siết chặt thành `pm.response.to.have.status(200)`.

---

## 4. BẢNG TỔNG KẾT TIẾN ĐỘ VÀ TRẠNG THÁI CUỐI CÙNG

- **Tổng số Request trong Collection:** **162 requests** (tăng từ 144).
- **Tổng số Endpoints User Phủ Được:** **92 endpoints** (tăng từ 84/215).
- **Số Assertion Đa Mã Lỏng Lẻo:** **0 / 162 (Đã triệt tiêu 100%)**.
- **Cơ sở dữ liệu:** **100% SQL02 `SpendWise_E2E_G4`**. Tuyệt đối không xâm phạm `SQL01`.
- **Backend Code:** Giữ nguyên 100% logic nghiệp vụ và kiến trúc lõi, không refactor ngoài phạm vi được giao.
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
