# SPENDWISE — USER FRONTEND API HANDOFF CONTRACT

> **Cập nhật:** 26/10/2026 (Phase Final Gate: Post-E2E Contract Freeze)
> **Đối tượng sử dụng:** Frontend Web Engineer & Mobile React Native / Expo Engineer
> **Base URL:** `http://localhost:5158` (Dev) / Production Gateway
> **Tài liệu tham chiếu:** `User.md`, `openapi_runtime_verified.json`, `BACKEND-USER-READINESS-REPORT.md`

---

## 1. TỔNG QUAN XÁC THỰC VÀ BẢO MẬT (AUTH & SECURITY)

### 1.1. Flow Đăng Ký và Kích Hoạt
1. **Đăng ký tài khoản:** `POST /api/auth/register`
   - **Body:** `{ fullName, email, phone, password, confirmPassword }`
   - **Lưu ý:** `phone` bắt buộc chuẩn Việt Nam 10 chữ số (bắt đầu bằng `0`, ví dụ: `09XXXXXXXX`).
   - **Phản hồi 200:** Trả về `{ status: "EMAIL_VERIFICATION_REQUIRED" }`.
2. **Xác thực OTP Email:** `POST /api/auth/email-verification/verify`
   - **Body:** `{ email, otp, deviceName }` (Môi trường Dev sử dụng OTP tĩnh `12345678`).
   - **Phản hồi 200:** Trả về `{ challengeToken: "...", status: "PIN_SETUP_REQUIRED" }`.
3. **Thiết lập mã PIN bảo mật ban đầu:** `POST /api/auth/pin/setup`
   - **Body:** `{ challengeToken, pin, confirmPin, deviceName }`
   - **Phản hồi 200:** Trả về bộ JWT Token hoàn chỉnh `{ accessToken, refreshToken, accessTokenExpiresAt, user }`.

### 1.2. Flow Đăng Nhập Đầy Đủ
1. **Bước 1 (Password):** `POST /api/auth/login`
   - **Body:** `{ identifier, password, deviceName }`
   - **Phản hồi 200:** `{ challengeToken, status: "PIN_REQUIRED", expiresAt }`.
2. **Bước 2 (Xác thực PIN):** `POST /api/auth/pin/verify`
   - **Body:** `{ challengeToken, pin, deviceName }`
   - **Phản hồi 200:** `{ accessToken, refreshToken, user }`.

### 1.3. Quản lý Mã PIN Bảo Mật (Security PIN)
- **Kiểm tra trạng thái PIN:** `GET /api/security-pin/status` -> `{ isSet: boolean }`.
- **Xác thực PIN giao dịch:** `POST /api/security-pin/verify` -> `{ pin: "123456" }` (HTTP 204 No Content).
- **Đổi mã PIN:** `PUT /api/security-pin` -> `{ currentPin, newPin, confirmPin }` (HTTP 204 No Content).

---

## 2. HỒ SƠ NGƯỜI DÙNG & BẠN BÈ (PROFILE & SOCIAL)

- **Xem thông tin cá nhân:** `GET /api/users/me` -> Trả về `UserProfile` (gồm `id`, `fullName`, `email`, `spendId`, `phone`, `preferredLanguage`, `preferredTheme`, `defaultCurrency`).
- **Cập nhật tùy chọn hiển thị:** `PUT /api/users/me/preferences`
  - **Body:** `{ preferredLanguage: "vi-VN"|"en-US", preferredTheme: "light"|"dark"|"system", defaultCurrency: "VND" }`.
- **Upload Avatar:** `POST /api/users/me/avatar` (Multipart form-data: `file`).
- **Tìm kiếm bạn bè qua SpendId:** `GET /api/friends/search?query={spendId}`.
- **Gửi lời mời kết bạn:** `POST /api/friends/requests` -> `{ contact: "SW..." }`.
- **Chấp nhận kết bạn:** `POST /api/friends/requests/{id}/accept`.

---

## 3. VÍ, SỐ CÁI & CHUYỂN TIỀN P2P (WALLETS & TRANSFERS)

- **Danh sách ví của tôi:** `GET /api/wallets` -> Trả về danh sách ví, bao gồm ví chính (`isDefault: true`), `availableBalance`.
- **Chi tiết ví:** `GET /api/wallets/{id}`.
- **Lịch sử giao dịch cá nhân:** `GET /api/transactions?page=1&pageSize=20&type=...`.
- **Chuyển tiền P2P (Peer-to-Peer):** `POST /api/transfers/p2p`
  - **Body:** `{ sourceWalletId, recipientUserId, amount, description, idempotencyKey, securityPin }`.
  - **Quy tắc nghiệp vụ:** Người gửi và người nhận bắt buộc phải có liên kết ngân hàng `Active` (hoặc TestMode Connection). IdempotencyKey chống trùng lặp tuyệt đối.

---

## 4. QUỸ NHÓM CỐ ĐỊNH V2 (FIXED FUND V2) — BÃI BỎ PENDING24H

> [!IMPORTANT]
> **Quy tắc cốt lõi Quỹ Cố định V2 (Fixed Fund V2):**
> 1. Không có duyệt chi (no owner approval), không có Pending24h / 24h cooloff.
> 2. Thành viên có cờ `CanSpend` chi tiêu trực tiếp: Tiền trừ ngay khỏi quỹ và giải ngân sang ví người nhận (`Completed`).
> 3. Cờ `EffectiveCanSpend` được tính toán tự động: Member đạt 100% nghĩa vụ cá nhân (`RequiredVnd`) + Nhóm đạt 100% mục tiêu quỹ (`TargetVnd`) + Owner đã bật `CanSpend = true` + Quỹ chưa giải thể.
> 4. Chủ quỹ (Owner) không cần cờ `CanSpend`, nhưng bắt buộc phải tự đóng đủ 100% nghĩa vụ cá nhân và quỹ đạt 100% mục tiêu mới được chi.
> 5. Số dư khả dụng chi tiêu = `Balance - LockedExcess - holds`. Tiền nạp vượt mức sau điều chỉnh nghĩa vụ (`LockedExcessVnd`) bị khóa không cho chi.

### Endpoints Quỹ Cố định V2:
- **Tạo Quỹ:** `POST /api/v1/funds` -> `{ name, kind: "Fixed", targetVnd, securityPin }`.
- **Mời thành viên:** `POST /api/v1/funds/{fundId}/invitations` -> `{ userId }` (Bắt buộc đã là bạn bè).
- **Chấp nhận lời mời:** `POST /api/v1/funds/invitations/{id}/accept`.
- **Xác nhận bảng nghĩa vụ:** `POST /api/v1/funds/{fundId}/obligations` -> `{ targetVnd, securityPin }`.
- **Đóng góp quỹ:** `POST /api/v1/funds/{fundId}/contributions`
  - **Body:** `{ walletId, amountVnd, idempotencyKey, securityPin, expectedRevision }`.
- **Bật/Tắt quyền chi tiêu (Owner only):** `PUT /api/v1/funds/{fundId}/members/{userId}/spending-permission`
  - **Body:** `{ canSpend: boolean, securityPin }`.
  - **Lỗi:** Nếu thành viên chưa hoàn thành 100% nghĩa vụ đóng góp, backend trả về `409 member_obligation_not_fulfilled`.
- **Thực hiện chi tiêu:** `POST /api/v1/funds/{fundId}/requests`
  - **Body:** `{ kind: "Expense", amountVnd, recipientUserId, purpose, idempotencyKey, securityPin }`.
  - **Phản hồi:** Trạng thái trả về là `Completed` ngay lập tức (không qua `AwaitingOwnerReview`).
- **Báo cáo chi tiêu bất thường:** `POST /api/v1/funds/requests/{id}/report`
  - **Body:** `{ reason: "..." }`.
  - **Cơ chế:** Tạo một `SupportCase` cho Admin điều tra. Không tự động clawback tiền và không đảo ngược giao dịch.

---


## 5. QUỸ NHÓM TỰ DO (FREE FUND V1) — ĐẶC TẢ MỚI 01/11/2026, CHƯA TRIỂN KHAI

**Nguồn quyết định đầy đủ:** [`User.md`](../../User.md) mục **1.3, 1.3.1 và 3.6 mục 10**. Không dùng bảng API này để suy ra backend đã hỗ trợ toàn bộ Free mới.

- `POST /api/v1/funds`: tạo `{"name":"Quỹ sinh hoạt","kind":"Free","targetVnd":null,"securityPin":"<PIN>"}`; quỹ tối đa 6 người, góp tự nguyện.
- `POST /api/v1/funds/{fundId}/invitations`, `.../accept`/`.../decline`: Member/Owner mời bạn bè, người nhận chấp nhận; Member mời không cần Owner duyệt.
- `POST /api/v1/funds/{fundId}/contributions`: góp vốn theo đúng thời điểm và lượt thành viên; `GET .../contributions` và `GET .../activities` phục vụ lịch sử.
- **UI Member có hai nút:** `Yêu cầu chi chung` (`Expense`), `Yêu cầu rút cá nhân` (`Withdrawal`). Gọi `POST /api/v1/funds/{fundId}/requests`, mỗi Member tối đa **1 request đang hoạt động mỗi loại**; nút tương ứng khóa cho đến khi kết thúc. `GET .../requests` cần bổ sung cách lọc request của bản thân và hàng chờ duyệt của Owner.
- `POST /api/v1/funds/requests/{id}/review`: Owner duyệt/từ chối. **Expense mới**: duyệt chỉ cấp quyền `ApprovedUnused` đúng số tiền, chưa chuyển ví/chưa trừ quỹ; hết hạn sau **7 ngày** nếu chưa sử dụng; chỉ ghi chi khi thanh toán ngoài thành công. **Withdrawal mới**: chỉ duyệt/chuyển về ví Member nếu đủ vốn và **toàn quỹ không còn quyền chi đã duyệt chưa thanh toán**.
- `POST /api/v1/funds/requests/{id}/cancel`: thêm hủy quyền chưa dùng/hết hạn/hủy do biến động quỹ; `PaymentProcessing` phải đối soát trước khi hoàn/hủy.
- `GET /api/v1/funds/{id}/refund-preview`; `POST .../{id}/leave`/`members/{userId}/kick`/`dissolve`: hoàn vốn theo từng lượt tham gia và chi phí chung theo tỷ lệ vốn hiện thời; không tính lại chi cũ cho người mới/góp thêm. Member tự rời sau khi tự hủy hoặc giải quyết request chờ; giải tán hủy các quyền chi chưa dùng.
- **Không hiển thị cho Free:** chuyển chủ, bầu cử, giới hạn 20%/hold 24h, `Suspended` hay Activity Proposal Fixed. **SePay không thay đổi**. Thanh toán hóa đơn thật bên ngoài là mở rộng chưa có PSP; không demo như giao dịch thành công thật.

**Ví dụ kiểm tra:** A góp 600.000đ, B góp 400.000đ, chi chung thành công 200.000đ → vốn A **480.000đ**, B **320.000đ**. B góp thêm 200.000đ rồi rời trước chi mới → hoàn **520.000đ**. C mới góp 200.000đ sau chi cũ rồi rời ngay → hoàn **200.000đ**. B còn 320.000đ, chờ rút 300.000đ; chi chung khiến B chỉ còn 280.000đ → tự hủy yêu cầu rút và thông báo.

**Ví dụ quyền chi:** Quỹ 1 triệu, B được duyệt chi 800.000đ chưa dùng; C rời và hoàn 300.000đ → quỹ còn 700.000đ, hủy quyền B chưa dùng. Nếu nhiều quyền, giữ quyền duyệt trước và hủy quyền mới nhất trước khi cần; Owner không được chi vào số đã cấp quyền.

**Trạng thái triển khai:** `/requests` và `/review` hiện hữu nhưng luồng Expense hiện tại **chuyển tiền vào ví khi Owner duyệt**, khác yêu cầu mới. Chưa có trạng thái `ApprovedUnused` và cơ chế vốn theo thời điểm. Không báo cáo đã hoàn thiện trước khi code và test.

## 6. NGÂN HÀNG LIÊN KẾT & RÚT TIỀN (BANK CONNECTIONS & WITHDRAWAL)

- **Danh sách tài khoản ngân hàng liên kết:** `GET /api/bank-connections`.
- **Tạo yêu cầu rút tiền:** `POST /api/wallet-withdrawals`
  - **Body:** `{ bankConnectionId, walletId, amount, securityPin, idempotencyKey }`.
  - **Cơ chế:** Tiền được khóa vào tài khoản tạm giữ (`PendingReconciliation` / hold).
- **Hạn chế nhà cung cấp bên ngoài (PROVIDER_CAPABILITY_BLOCKED):**
  - Môi trường Sandbox SePay hiện tại chưa hỗ trợ API tự động trích nợ tài khoản ngoài và API kiểm tra số dư thực tế.
  - Giao diện UI cần hiển thị nhãn: `"Rút tiền đang trong quá trình đối soát từ ngân hàng"` khi giao dịch ở trạng thái `PendingReconciliation`.

---

## 7. QUY ƯỚC LỖI & MÃ PHẢN HỒI (PROBLEM DETAILS)

Toàn bộ API trả về lỗi theo chuẩn RFC 7807:
```json
{
  "type": "https://spendwise.local/errors/{code}",
  "title": "Thông báo lỗi tiếng Việt dễ hiểu",
  "status": 400,
  "code": "error_code_string",
  "traceId": "..."
}
```
Các mã lỗi quan trọng Frontend cần xử lý:
- `member_obligation_not_fulfilled`: Thành viên chưa góp đủ 100% nghĩa vụ.
- `group_target_not_reached`: Quỹ nhóm chưa đạt 100% mục tiêu.
- `unauthorized_spender`: Người dùng không có quyền chi tiêu (chưa bật CanSpend hoặc quỹ chưa đủ điều kiện).
- `insufficient_fund_balance`: Số dư chi tiêu khả dụng không đủ.
- `invalid_security_pin`: Mã PIN giao dịch không chính xác.
- `active_bank_required`: Bắt buộc phải có liên kết ngân hàng hoạt động trước khi giao dịch.
