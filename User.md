# SPENDWISE V1.0 — ĐẶC TẢ KỸ THUẬT VÀ TÀI LIỆU API PHÂN HỆ USER

> **Phiên bản:** V1.0 (Production Candidate)  
> **Đối tượng sử dụng:** Frontend Web & Mobile Client Developers (Codex / Engineering Team).  
> **Trạng thái Backend:** Locked 100% (Đã kiểm thử hồi quy tự động 306 requests / 343 assertions Newman PASS).  
> **Ranh giới tài liệu:** Thuần tài liệu kỹ thuật, API contract, ràng buộc dữ liệu và mã lỗi. Không chứa lý thuyết kiến trúc chung, tiếp thị hay hướng dẫn framework UI.

---

## 1. NGUYÊN TẮC NGHIỆP VỤ & BẢO MẬT BẮT BUỘC (CORE BUSINESS RULES)

### 1.1. Phân quyền & Vòng đời Tài khoản (Actor & Lifecycle)
1. **Phân quyền người dùng:**
   - Người dùng thông thường sở hữu vai trò `SystemRole.User = 1`.
   - Các vai trò cũ (`SystemAdmin = 0`, `SuperAdmin = 3`) bị loại bỏ hoàn toàn. Token chứa các vai trò này sẽ bị từ chối truy cập ngay lập tức (`403 Forbidden` - `legacy_role_rejected`).
   - Chủ quỹ nhóm (`Fund Owner`) là người dùng thông thường có thêm quyền quản trị trong phạm vi quỹ mình sở hữu, không có bất kỳ quyền quản trị hệ thống nào.
2. **Trạng thái tài khoản (`AccountStatus`):**
   - `Active (0)`: Tài khoản hoạt động bình thường, toàn quyền truy cập tất cả chức năng.
   - `Restricted (1)`: Tài khoản bị hạn chế rủi ro. Người dùng **vẫn được phép** đăng nhập, xem số dư, đọc dữ liệu, nhận tiền chuyển đến (P2P Receive) và gửi yêu cầu hỗ trợ. **Chặn toàn bộ lệnh ghi nợ tiền ra** (chuyển tiền đi, rút tiền, chi tiêu cá nhân, nộp quỹ nhóm). Mã lỗi trả về khi vi phạm: `403 Forbidden` (`account_restricted`).
   - `Locked (2)`: Tài khoản bị khóa an ninh. Toàn bộ `UserSessions` và `RefreshToken` bị thu hồi ngay lập tức (`RevokedAt = UtcNow`). **Chặn hoàn toàn đăng nhập và refresh token**. Khi được mở khóa, người dùng bắt buộc phải đăng nhập lại từ đầu.
3. **Mã PIN bảo mật 6 số (`SecurityPin`):**
   - Bắt buộc thiết lập ngay sau khi xác minh email thành công (`PIN_SETUP_REQUIRED`).
   - Yêu cầu xác thực mã PIN cho mọi hành vi biến động số dư: chuyển tiền P2P, rút tiền, nộp quỹ nhóm, giải tán quỹ nhóm.
   - Tự động khóa tạm thời (`Lockout`) nếu nhập sai quá 5 lần liên tiếp.

### 1.2. Quản lý Ví & Sổ cái Kép (Wallets & Double-Entry Ledger)
1. **Cấu trúc Ví:**
   - Mỗi người dùng khi kích hoạt được cấp một Ví chính (`Main Wallet`, mặc định, không thể xóa).
   - Người dùng có thể tạo thêm Ví phụ (`Sub-wallets`) để quản lý các mục đích chi tiêu riêng.
   - Tổng số dư khả dụng hiển thị trên UI = Số dư Ví chính + Tổng số dư các Ví phụ.
2. **Nguyên tắc Sổ cái kép:**
   - Mọi biến động số dư ghi nhận đồng thời hai vế Nợ và Có ($\sum \text{Debit} = \sum \text{Credit}$).
   - Số dư ví không bao giờ được phép âm. Nếu số dư khả dụng nhỏ hơn số tiền yêu cầu, giao dịch bị từ chối ngay lập tức (`400 BadRequest` - `insufficient_funds`).
3. **Chống lặp giao dịch (Idempotency):**
   - Mọi request biến động số dư yêu cầu gửi Header `X-Idempotency-Key` (chuẩn UUIDv4).
   - Nếu client gửi lại cùng một key khi gặp sự cố mạng, Backend trả về kết quả đã ghi nhận trước đó mà không trừ tiền lần hai.

### 1.3. Quỹ nhóm Cố định V2 & Quỹ nhóm Tự do (Group Funds)
1. **Quỹ nhóm Cố định V2 (Fixed Fund V2):**
   - Quản lý theo mô hình Nghĩa vụ kỳ (`Obligation Model`). Mỗi thành viên có định mức đóng góp theo chu kỳ.
   - Hạn mức chi tiêu khả dụng cá nhân (`CanSpend`):
     $$\text{CanSpend} = \text{Tổng tiền thành viên đã đóng} - \text{Nghĩa vụ đã phân bổ/sử dụng}$$
   - Thành viên được bật quyền chi tiêu có thể thực hiện chi trong hạn mức `CanSpend` mà không cần chờ duyệt. Chi tiêu vượt định mức cá nhân phải gửi đề xuất hoạt động (`Activity Proposal`) để Chủ quỹ phê duyệt.
   - Loại bỏ hoàn toàn cơ chế tạm giữ 20% và trạng thái giữ chỗ giả tạo (Pending24h).
2. **Quỹ nhóm Tự do (Free Fund) — nghiệp vụ đã chốt ngày 30/10/2026:**
   - Người tạo quỹ là chủ nhóm hiện tại (`OwnerUserId`), vẫn chỉ mang role hệ thống `User`. Giới hạn 6 thành viên/quỹ (bao gồm chủ nhóm); mỗi User được tham gia tối đa 10 nhóm đang hoạt động.
   - Chủ nhóm hoặc bất kỳ thành viên đang hoạt động đều có thể mời **bạn bè đã kết bạn thành công**; người được mời có quyền **chấp nhận/từ chối** và chỉ gia nhập sau khi tự chấp nhận. **Thành viên B mời C không cần A (chủ nhóm) duyệt.** Lời mời có thời hạn 7 ngày.
   - Thành viên góp tự nguyện, không có mức nghĩa vụ hay mục tiêu cố định; tiền được chuyển từ ví cá nhân vào sổ cái của quỹ.
   - Thành viên thường tạo yêu cầu `Expense` (chi tiền) hoặc `Withdrawal` (rút về ví chính mình): trạng thái `AwaitingOwnerReview`; **chủ nhóm hiện tại** duyệt hoặc từ chối, xác thực PIN khi duyệt. Chủ nhóm tạo `Expense` thì chi trực tiếp, không cần tự duyệt. Không dùng `Activity Proposal` cho quy trình chi/rút của Free Fund.
   - Thành viên có thể rời quỹ, chủ nhóm có thể loại thành viên có lý do hoặc giải tán quỹ; backend trả tiền theo phần quyền lợi tài chính thực tế (xem `refund-preview`), **không bảo đảm hoàn 100% số tiền từng góp nếu quỹ đã chi**. Chủ nhóm phải chuyển quyền trước khi rời nhóm có nhiều thành viên; quỹ chỉ còn một người thì chủ nhóm giải tán.
   - Nếu chủ nhóm thiếu hoạt động đủ **15 ngày theo bằng chứng hợp lệ**, các thành viên đủ điều kiện có thể mở bầu cử. Người thắng phải chấp nhận chức vụ; chủ nhóm cũ trở thành thành viên thường, không tự lấy lại quyền khi đăng nhập lại.
   - **Quy tắc bổ sung đã CHỐT nhưng CHƯA triển khai backend:** (1) Khi bầu cử đang diễn ra, không cho chủ nhóm loại thành viên hoặc giải tán quỹ. (2) Khi **chuyển quyền tự nguyện hoặc bầu cử hoàn tất**, tất cả yêu cầu `Expense`/**`Withdrawal`** thuộc nhiệm kỳ cũ và còn `AwaitingOwnerReview` phải **tự động `Cancelled` và gửi thông báo cho người tạo để gửi lại**, không chuyển quyền phê duyệt cho chủ mới; giao dịch đã `Completed` giữ nguyên. Việc hủy và đổi chủ phải nhất quán trong một giao dịch; gửi thông báo không trùng lặp khi thao tác lặp lại.
   - **Điều kiện vận hành cần xác minh, không bỏ qua:** chính sách bầu cử, ghi nhận tương tác foreground qua `POST /api/v1/activity`, cấu hình `V1:Activity:CoverageVerified` / `CoverageStartsAtUtc` và bằng chứng vắng mặt đủ 15 ngày. Chưa có bằng chứng tin cậy thì **chặn mở bầu cử**, không suy đoán chủ nhóm vắng mặt.
   - **Ranh giới trạng thái:** Các API tạo/mời/góp/chi duyệt/rút/hoàn tiền/bầu cử và kiểm tra nhiệm kỳ hiện **đã có trong code**. Hai quy tắc bổ sung về khóa thao tác khi bầu cử và tự hủy yêu cầu chờ khi đổi chủ là **đặc tả cần code + kiểm thử**, tuyệt đối không ghi vào báo cáo Word là đã hoàn thành khi chưa nghiệm thu.

#### 1.3.1. Quỹ tự do — trường hợp biên và quy tắc UI/Word

| Tình huống | Kết quả đúng / UI cần thể hiện |
|---|---|
| A mời B; B từ chối/hết hạn | Không thêm B vào thành viên; thông báo/lịch sử lời mời phản ánh trạng thái. |
| B đã là thành viên mời C | Chỉ cần B và C là bạn bè, quỹ chưa đủ chỗ; C tự chấp nhận, **không đợi A duyệt**. |
| B gửi yêu cầu chi/rút; A chưa quyết định | Hiện `AwaitingOwnerReview` cho B và danh sách chờ xử lý cho A; chưa trừ quỹ khi mới tạo yêu cầu. |
| A duyệt hoặc từ chối | Duyệt: kiểm tra số dư, ví nhận, tư cách thành viên và ghi giao dịch; từ chối: không chuyển tiền. |
| A tự tạo `Expense` | Chi trực tiếp nếu hợp lệ và quỹ đủ tiền; phân biệt với `Withdrawal`, không giả định mọi loại yêu cầu của A đều chi trực tiếp. |
| B rời nhóm khi quỹ chưa chi | Hoàn phần đóng góp chưa rút/chưa hoàn trước đó, giới hạn bởi số dư còn lại. |
| B rời nhóm khi quỹ đã chi | Hoàn theo tỷ lệ quyền lợi ròng: số dư quỹ × (đóng góp − rút cá nhân − đã hoàn) của B / tổng quyền lợi ròng của thành viên đang hoạt động; xử lý làm tròn theo backend. |
| B được bầu làm chủ mới | A giữ tư cách thành viên nếu chưa rời; quyền owner chuyển cho B sau khi B chấp nhận. |
| Đang bầu cử | Theo quyết định mới: khóa loại thành viên/giải tán; **cần triển khai**. Quy trình tài chính và mời/nhận thành viên đã có các chốt kiểm tra bầu cử riêng. |
| Thay chủ nhóm khi còn yêu cầu chi/rút chờ A | Theo quyết định mới: tự hủy `AwaitingOwnerReview` của nhiệm kỳ A, thông báo người gửi tạo lại cho B duyệt; **cần triển khai**. Không hủy các giao dịch `Completed` hay yêu cầu tranh chấp `Suspended` chỉ vì đổi chủ. |
| A không hoạt động 15 ngày nhưng bằng chứng thiếu | Không mở bầu cử; giao diện giải thích dữ liệu hoạt động/chính sách chưa được xác minh, không được tự cấp quyền. |

**Lưu ý dành cho báo cáo Word:** Mô tả đây là **quỹ chung đóng góp linh hoạt có phê duyệt chi của chủ nhóm, cơ chế bầu lại chủ khi đủ điều kiện và hoàn phần quyền lợi khi rời quỹ**. Không tuyên bố tính năng bầu chủ nhóm hoặc hoàn tiền là tính năng của MoMo; MoMo chỉ là sản phẩm tham khảo cho phần quỹ nhóm thông thường. Tách rõ `Đã có trong backend` và `Đã chốt — chờ triển khai`.

### 1.4. Hỗ trợ & Khiếu nại Giao dịch Trực tiếp (Direct Support Flow)
1. Người dùng khiếu nại giao dịch trực tiếp bằng cách tạo `SupportCase` gắn với `TransactionId`.
2. Không tồn tại endpoint tạo tranh chấp (Dispute) độc lập trôi nổi ngoài quy trình hỗ trợ.
3. Người dùng và Admin trao đổi tin nhắn, bằng chứng đính kèm ngay trong vụ việc. Khi được Admin phê duyệt (`Resolved`), số tiền khiếu nại sẽ được tự động hoàn trả vào ví.

---

## 2. QUY CHUẨN KỸ THUẬT GỌI API (CLIENT API CONTRACT)

### 2.1. Headers bắt buộc
```http
Content-Type: application/json
Authorization: Bearer <access_token>
X-Idempotency-Key: <uuid-v4> (Bắt buộc với các lệnh ghi nợ/chuyển tiền/chi tiêu)
```

---

## 3. DANH MỤC API PHÂN HỆ USER

### 3.1. Xác thực & Quản lý Phiên (Authentication & Sessions)

#### 1. `POST /api/auth/register`
- **Mô tả:** Đăng ký tài khoản mới. Sinh mã dynamic OTP ngẫu nhiên 8 chữ số từ nguồn mật mã an toàn (CSPRNG), xếp email vào hàng đợi Outbox.
- **Quyền:** Public.
- **Request Body:**
```json
{
  "fullName": "Nguyen Van A",
  "email": "user@example.com",
  "phone": "0912345678",
  "password": "Password123!",
  "confirmPassword": "Password123!"
}
```
- **Response `200 OK`:**
```json
{
  "status": "EMAIL_VERIFICATION_REQUIRED",
  "email": "user@example.com",
  "resendAfterSeconds": 60
}
```
- **Lỗi thường gặp:** `400 BadRequest` (`email_already_exists`, `invalid_phone_format`, `password_too_weak`).

#### 2. `POST /api/auth/email-verification/verify`
- **Mô tả:** Xác minh mã OTP 8 chữ số nhận từ email. Mã chỉ dùng một lần (single-use), chống tấn công phát lại (replay).
- **Quyền:** Public.
- **Request Body:**
```json
{
  "email": "user@example.com",
  "otp": "83749201"
}
```
- **Response `200 OK`:**
```json
{
  "status": "PIN_SETUP_REQUIRED",
  "challengeToken": "signed-challenge-jwt-string",
  "expiresIn": 900
}
```
- **Lỗi thường gặp:** `400 BadRequest` (`email_verification_failed`, `otp_expired`, `otp_already_used`, `too_many_failed_attempts`).

#### 3. `POST /api/auth/email-verification/resend`
- **Mô tả:** Gửi lại OTP xác minh email. Mã OTP cũ bị thu hồi ngay lập tức (`RevokedAt = UtcNow`). Áp dụng giới hạn tần suất (cooldown 60s).
- **Quyền:** Public.
- **Request Body:**
```json
{
  "email": "user@example.com"
}
```
- **Response `200 OK`:**
```json
{
  "status": "EMAIL_VERIFICATION_REQUIRED",
  "resendAfterSeconds": 60
}
```
- **Lỗi thường gặp:** `429 TooManyRequests` (`resend_cooldown_active`).

#### 4. `POST /api/auth/pin/setup`
- **Mô tả:** Thiết lập mã PIN 6 số lần đầu sau khi xác minh email. Hoàn tất onboarding, kích hoạt ví chính và cấp token.
- **Quyền:** Public (kèm `ChallengeToken`).
- **Request Body:**
```json
{
  "challengeToken": "signed-challenge-jwt-string",
  "pin": "123456",
  "confirmPin": "123456"
}
```
- **Response `200 OK`:**
```json
{
  "accessToken": "jwt-access-token",
  "refreshToken": "refresh-token-string",
  "expiresIn": 3600,
  "user": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "email": "user@example.com",
    "fullName": "Nguyen Van A",
    "spendId": "SPEND_123456",
    "status": "Active"
  }
}
```

#### 5. `POST /api/auth/login`
- **Mô tả:** Bước 1 đăng nhập bằng Email/SpendId và mật khẩu. Trả về `ChallengeToken` để thực hiện Bước 2 (nhập PIN).
- **Quyền:** Public.
- **Request Body:**
```json
{
  "identifier": "user@example.com",
  "password": "Password123!",
  "deviceName": "Chrome / Windows 11"
}
```
- **Response `200 OK`:**
```json
{
  "status": "PIN_REQUIRED",
  "challengeToken": "signed-challenge-token",
  "expiresIn": 300
}
```
- **Lỗi thường gặp:** `401 Unauthorized` (`invalid_credentials`), `403 Forbidden` (`account_locked`).

#### 6. `POST /api/auth/pin/verify`
- **Mô tả:** Bước 2 đăng nhập bằng mã PIN 6 số.
- **Quyền:** Public (kèm `ChallengeToken`).
- **Request Body:**
```json
{
  "challengeToken": "signed-challenge-token",
  "pin": "123456",
  "deviceName": "Chrome / Windows 11"
}
```
- **Response `200 OK`:** Trả về `AuthResponse` (`accessToken`, `refreshToken`, `expiresIn`, `user`).

#### 7. `POST /api/auth/refresh`
- **Mô tả:** Cấp mới Access Token bằng Refresh Token (Token Rotation). Refresh token cũ bị vô hiệu hóa ngay sau khi sử dụng.
- **Quyền:** Public.
- **Request Body:**
```json
{
  "refreshToken": "current-refresh-token"
}
```
- **Response `200 OK`:** Trả về `AuthResponse` mới.

#### 8. `POST /api/auth/logout`
- **Mô tả:** Đăng xuất phiên hiện tại, thu hồi refresh token của phiên đó.
- **Quyền:** User (`Authorize`).
- **Response:** `204 NoContent`.

#### 9. `GET /api/auth/sessions`
- **Mô tả:** Xem danh sách các phiên đăng nhập đang hoạt động của tài khoản.
- **Quyền:** User.
- **Response `200 OK`:** Mảng các đối tượng phiên (`id`, `deviceName`, `ipAddress`, `lastActiveAt`, `isCurrent`).

#### 10. `DELETE /api/auth/sessions/{id}`
- **Mô tả:** Hủy một phiên đăng nhập cụ thể theo ID phiên.
- **Quyền:** User.
- **Response:** `204 NoContent`.

#### 11. `POST /api/auth/change-password`
- **Mô tả:** Đổi mật khẩu tài khoản khi đang đăng nhập.
- **Quyền:** User.
- **Request Body:**
```json
{
  "currentPassword": "Password123!",
  "newPassword": "NewPassword456!",
  "confirmPassword": "NewPassword456!"
}
```
- **Response:** `204 NoContent`.

#### 12. `POST /api/auth/forgot-password`
- **Mô tả:** Yêu cầu đặt lại mật khẩu khi quên. Sinh mã OTP động gửi vào email.
- **Quyền:** Public.
- **Request Body:**
```json
{
  "identifier": "user@example.com"
}
```
- **Response:** `202 Accepted`.

#### 13. `POST /api/auth/reset-password`
- **Mô tả:** Đặt lại mật khẩu bằng mã OTP nhận từ email. Tự động thu hồi toàn bộ các phiên hoạt động cũ (`RevokedAt = UtcNow`).
- **Quyền:** Public.
- **Request Body:**
```json
{
  "identifier": "user@example.com",
  "otp": "83749201",
  "newPassword": "NewPassword123!"
}
```
- **Response:** `204 NoContent`.

#### 14. `POST /api/auth/pin/forgot`
- **Mô tả:** Yêu cầu đặt lại mã PIN. Hệ thống gửi email chứa signed reset link bảo mật.
- **Quyền:** Public.
- **Request Body:**
```json
{
  "identifier": "user@example.com"
}
```
- **Response:** `202 Accepted`.

#### 15. `POST /api/auth/pin/reset/validate`
- **Mô tả:** Kiểm tra token đặt lại mã PIN từ link email có hợp lệ hay đã hết hạn/đã dùng.
- **Quyền:** Public.
- **Request Body:**
```json
{
  "token": "raw-token-from-link"
}
```
- **Response `200 OK`:** `{ "valid": true }`.

#### 16. `POST /api/auth/pin/reset`
- **Mô tả:** Đặt lại mã PIN bằng signed token. Token chỉ được dùng một lần.
- **Quyền:** Public.
- **Request Body:**
```json
{
  "token": "raw-token-from-link",
  "newPin": "654321",
  "confirmPin": "654321"
}
```
- **Response:** `204 NoContent`.

---

### 3.2. Hồ sơ Người dùng & Tùy chọn (Users & Preferences)

#### 1. `GET /api/users/me`
- **Mô tả:** Lấy thông tin chi tiết hồ sơ người dùng hiện tại, bao gồm số dư ví chính và preferences.
- **Quyền:** User.
- **Response `200 OK`:**
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "email": "user@example.com",
  "fullName": "Nguyen Van A",
  "phone": "0912345678",
  "spendId": "SPEND_123456",
  "status": "Active",
  "mainWalletBalance": 5000000.00,
  "createdAt": "2026-10-20T10:00:00Z",
  "preferences": {
    "theme": "dark",
    "locale": "vi-VN",
    "enableBiometric": true,
    "notificationsEnabled": true
  }
}
```

#### 2. `PUT /api/users/me/preferences`
- **Mô tả:** Lưu cấu hình tùy chọn của người dùng (theme, locale, biometric flag).
- **Quyền:** User.
- **Request Body:**
```json
{
  "theme": "dark",
  "locale": "vi-VN",
  "enableBiometric": true,
  "notificationsEnabled": true
}
```
- **Response `200 OK`:** Trả về đối tượng `UserPreferencesDto` đã lưu.

#### 3. `POST /api/users/me/email/request`
- **Mô tả:** Yêu cầu đổi địa chỉ email. Sinh OTP gửi tới email mới và gửi thông báo cảnh báo tới email cũ.
- **Quyền:** User.
- **Request Body:**
```json
{
  "newEmail": "newemail@example.com"
}
```
- **Response `200 OK`:** `{ "status": "CONFIRMATION_REQUIRED", "resendAfterSeconds": 60 }`.

#### 4. `POST /api/users/me/email/confirm`
- **Mô tả:** Xác nhận đổi email bằng mã OTP nhận tại hộp thư mới.
- **Quyền:** User.
- **Request Body:**
```json
{
  "newEmail": "newemail@example.com",
  "otp": "94820153"
}
```
- **Response `200 OK`:** Trả về hồ sơ `UserProfileDto` với email đã cập nhật.

#### 5. `DELETE /api/users/me/sessions`
- **Mô tả:** Cưỡng chế đăng xuất toàn bộ các phiên khác của chính mình (ngoại trừ phiên hiện tại).
- **Quyền:** User.
- **Response:** `204 NoContent`.

---

### 3.3. Ví & Giao dịch Tài chính Cá nhân (Wallets & Transactions)

#### 1. `GET /api/wallets`
- **Mô tả:** Danh sách các ví người dùng đang sở hữu (Ví chính và các ví phụ).
- **Quyền:** User.
- **Response `200 OK`:**
```json
[
  {
    "id": "9b1deb4d-3b7d-4bad-9bdd-2b0d7b3dcb6d",
    "name": "Ví chính",
    "currency": "VND",
    "type": "Main",
    "balance": 5000000.00,
    "isDefault": true
  }
]
```

#### 2. `POST /api/wallets`
- **Mô tả:** Tạo ví phụ mới.
- **Quyền:** User (`Active`). Nếu tài khoản `Restricted`, không được tạo thêm ví mới.
- **Request Body:**
```json
{
  "name": "Quỹ Mua Sắm",
  "currency": "VND",
  "type": "Sub",
  "targetAmount": 10000000.00
}
```
- **Response `201 Created`:** Trả về `WalletDto` vừa tạo.

#### 3. `GET /api/transactions`
- **Mô tả:** Lịch sử giao dịch cá nhân có phân trang và bộ lọc.
- **Quyền:** User.
- **Query Params:** `page=1&pageSize=20&walletId={id}&type=Expense&categoryId={id}&fromDate=2026-10-01`
- **Response `200 OK`:** `PagedResult<TransactionDto>` (`items`, `totalCount`, `page`, `pageSize`).

#### 4. `POST /api/transactions/expense`
- **Mô tả:** Ghi nhận khoản chi tiêu cá nhân (ghi nợ ví, ghi có tài khoản chi phí để theo dõi ngân sách).
- **Quyền:** User (`Active`). Nếu `Restricted`, bị chặn `403 Forbidden` (`account_restricted`).
- **Request Body:**
```json
{
  "walletId": "9b1deb4d-3b7d-4bad-9bdd-2b0d7b3dcb6d",
  "amount": 45000.00,
  "categoryId": "2c963f66-afa6-4562-b3fc-3fa85f645717",
  "description": "Cà phê sáng",
  "occurredAt": "2026-10-29T08:30:00Z"
}
```
- **Response `201 Created`:** `TransactionDto`.

#### 5. `POST /api/transactions/income`
- **Mô tả:** Ghi nhận khoản thu nhập cá nhân vào ví.
- **Quyền:** User (`Active` hoặc `Restricted` - nhận tiền được phép).
- **Request Body:**
```json
{
  "walletId": "9b1deb4d-3b7d-4bad-9bdd-2b0d7b3dcb6d",
  "amount": 15000000.00,
  "categoryId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "description": "Lương tháng 10",
  "occurredAt": "2026-10-29T09:00:00Z"
}
```
- **Response `201 Created`:** `TransactionDto`.

---

### 3.4. Chuyển tiền (Transfers: Internal & P2P)

#### 1. `GET /api/transfers/recipients?phone=0912345678`
- **Mô tả:** Tra cứu thông tin người nhận trước khi chuyển tiền (hiển thị họ tên đã che bảo mật và SpendId).
- **Quyền:** User.
- **Response `200 OK`:**
```json
{
  "userId": "uuid-recipient",
  "fullName": "NGUYEN VAN B",
  "spendId": "SPEND_654321",
  "phoneMasked": "091****678"
}
```

#### 2. `POST /api/transfers/internal`
- **Mô tả:** Chuyển tiền giữa hai ví thuộc cùng một tài khoản người dùng.
- **Quyền:** User (`Active`). Yêu cầu mã PIN 6 số.
- **Request Body:**
```json
{
  "sourceWalletId": "uuid-wallet-main",
  "destinationWalletId": "uuid-wallet-sub",
  "amount": 1000000.00,
  "note": "Chuyển tiền vào quỹ du lịch",
  "securityPin": "123456"
}
```
- **Response `201 Created`:** `TransferDto`.

#### 3. `POST /api/transfers/p2p`
- **Mô tả:** Chuyển tiền P2P nội bộ giữa 2 tài khoản SpendWise qua SpendId hoặc số điện thoại.
- **Quyền:** User (`Active`). Nếu `Restricted`, bị chặn `403 Forbidden` (`account_restricted`). Người nhận ở trạng thái `Locked` cũng sẽ bị từ chối nhận tiền.
- **Request Body:**
```json
{
  "recipientIdentifier": "SPEND_654321",
  "sourceWalletId": "uuid-wallet-main",
  "amount": 250000.00,
  "note": "Tiền cơm trưa",
  "securityPin": "123456"
}
```
- **Response `201 Created`:** `TransferDto`.

---

### 3.5. Kết nối Ngân hàng & Nạp rút Test Mode (SePay Test Mode & Bank Connections)

#### 1. `POST /api/bank-connections/test-mode/link-challenges`
- **Mô tả:** Khởi tạo challenge liên kết tài khoản ngân hàng SePay Test Mode. Backend tự sinh mã tham chiếu `SWLINK...` ngẫu nhiên và đặt hạn 10 phút. Không yêu cầu Request Body.
- **Quyền:** User (`Active`).
- **Request Body:** Không có (empty body).
- **Response `200 OK` (`TestModeBankLinkChallengeDto`):**
```json
{
  "challengeId": "122e2a32-8f1d-4731-8732-f1c2ba35d20e",
  "reference": "SWLINKE4692D413102266B361F179D",
  "expiresAt": "2026-10-29T10:40:00Z",
  "instruction": "Tạo một giao dịch tiền ra trên SePay Test Mode với nội dung chứa mã SWLINKE4692D413102266B361F179D, sau đó xác nhận liên kết."
}
```

#### 2. `POST /api/bank-connections/test-mode/link-challenges/{challengeId}/confirm`
- **Mô tả:** Xác nhận liên kết tài khoản ngân hàng sau khi đã chuyển tiền mô phỏng mang mã `SWLINK...` trên cổng SePay Test Mode.
- **Quyền:** User (`Active`).
- **Response `200 OK` (`BankConnectionDto`):**
```json
{
  "id": "122e2a32-8f1d-4731-8732-f1c2ba35d20e",
  "bankName": "TPBank",
  "maskedAccountNumber": "****0005",
  "accountType": "individual",
  "status": "Active"
}
```

#### 3. `GET /api/bank-connections/test-mode/linked-accounts`
- **Mô tả:** Danh sách các tài khoản ngân hàng Test Mode đã liên kết và đang hoạt động (`Active`) của người dùng hiện tại.
- **Quyền:** User (`Active`).
- **Response `200 OK`:** Mảng `TestModeLinkedAccountDto` (chứa `bankConnectionId`, `bankName`, `maskedAccountNumber`, `accountType`, `status`, `accumulatedBalance`).

#### 4. `POST /api/bank-connections/test-mode/wallet-top-ups`
- **Mô tả:** Tạo yêu cầu nạp tiền vào ví qua cổng SePay Test Mode. Backend kiểm tra không có top-up Pending còn hiệu lực, sau đó sinh mã tham chiếu `SWTP...` với thời hạn 15 phút.
- **Quyền:** User (`Active`).
- **Request Body (`CreateWalletTopUpRequest`):**
```json
{
  "bankConnectionId": "122e2a32-8f1d-4731-8732-f1c2ba35d20e",
  "walletId": "0acc5688-857e-48b2-b60f-03718ed265cf",
  "amount": 50000.00
}
```
- **Response `200 OK` (`WalletTopUpDto`):**
```json
{
  "id": "61072bdd-7f79-4845-ab9b-acf87c7411cf",
  "bankConnectionId": "122e2a32-8f1d-4731-8732-f1c2ba35d20e",
  "walletId": "0acc5688-857e-48b2-b60f-03718ed265cf",
  "amount": 50000.00,
  "currency": "VND",
  "maskedAccountNumber": "****0005",
  "reference": "SWTP26F1BCEED82D3C55C44FFA34",
  "status": "Pending",
  "expiresAt": "2026-10-29T13:46:20Z",
  "completedAt": null,
  "financialTransactionId": null,
  "instruction": "Tạo giao dịch tiền ra đúng 50000 VND trên SePay Test Mode và nhập mã SWTP26F1BCEED82D3C55C44FFA34."
}
```

#### 5. `POST /api/bank-connections/test-mode/wallet-top-ups/{id}/confirm`
- **Mô tả:** Xác nhận nạp tiền Test Mode. Backend kiểm tra giao dịch tiền ra khỏi tài khoản ngân hàng khớp mã tham chiếu `SWTP...` từ SePay Provider API. Trả về `409 wallet_top_up_pending` nếu SePay chưa ghi nhận giao dịch.
- **Quyền:** User (`Active`).
- **Response `200 OK`:** `WalletTopUpDto` (`status`: "Completed").

#### 6. `POST /api/bank-connections/test-mode/wallet-withdrawals`
- **Mô tả:** Tạo lệnh rút tiền từ ví về tài khoản ngân hàng liên kết. Backend thực hiện đặt cọc tạm giữ (`SYSTEM:ReservationHold:VND`) và giảm số dư khả dụng ví ngay lập tức để bảo vệ thanh khoản.
- **Quyền:** User (`Active`).
- **Request Body (`CreateWalletWithdrawalRequest`):**
```json
{
  "bankConnectionId": "d84eaae8-a640-452d-921d-a01334026cab",
  "walletId": "06cfe562-67d4-41fb-8fec-87057b9ea30c",
  "amount": 15000.00,
  "securityPin": "123456",
  "idempotencyKey": "withdrawal-uuid-5678"
}
```
- **Response `200 OK` (`WalletWithdrawalDto`):**
```json
{
  "id": "23fea2cf-4792-4a59-bbc1-bdd9e09817c7",
  "bankConnectionId": "d84eaae8-a640-452d-921d-a01334026cab",
  "walletId": "06cfe562-67d4-41fb-8fec-87057b9ea30c",
  "amount": 15000.00,
  "status": "Processing",
  "maskedAccountNumber": "****0004",
  "reference": "SWWD7F044D73DF59FBF8597D77DE",
  "createdAt": "2026-10-29T10:45:00Z",
  "completedAt": null
}
```

#### 7. `POST /api/bank-connections/test-mode/wallet-withdrawals/{id}/confirm`
- **Mô tả:** Xác nhận giải ngân lệnh rút tiền sau khi SePay đã chuyển tiền thành công ra tài khoản ngân hàng đích.
- **Trạng thái thực tế:** Trả về `409 wallet_withdrawal_pending` khi cổng Test Mode chưa có giao dịch tiền về tài khoản ngân hàng. Sổ cái kép không ghi nhận hoàn tất và số dư tạm giữ được bảo toàn tuyệt đối, chống double-posting.

---

### 3.6. Quỹ nhóm & Phân bổ Nghĩa vụ V2 (Groups & V1 Funds)

#### 1. `POST /api/groups`
- **Mô tả:** Tạo nhóm chi tiêu mới. Người tạo trở thành Chủ nhóm (`Owner`).
- **Quyền:** User (`Active`).
- **Request Body:**
```json
{
  "name": "Nhà Trọ 402",
  "description": "Nhóm sinh hoạt phòng 402",
  "currency": "VND"
}
```
- **Response `201 Created`:** `GroupDto`.

#### 2. `POST /api/groups/{groupId}/invites`
- **Mô tả:** Chủ nhóm (`Owner`) gửi lời mời tham gia nhóm cho người dùng khác (yêu cầu hai bên đã là bạn bè được chấp thuận, người được mời chưa quá giới hạn 10 nhóm hoạt động).
- **Quyền:** Chủ nhóm (`Owner`).
- **Request Body:**
```json
{
  "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
}
```
- **Response `200 OK`:**
```json
{
  "id": "uuid-invite",
  "groupId": "uuid-group",
  "groupName": "Nhà Trọ 402",
  "invitedUserId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "status": "Pending",
  "expiresAt": "2026-11-05T08:00:00Z",
  "token": "hex-token-string"
}
```
- **Lỗi thường gặp:** `404 NotFound` (`group_not_found`, `user_not_found`), `409 Conflict` (`already_member`, `active_group_limit`).

#### 3. `GET /api/groups/invites`
- **Mô tả:** Người dùng xem danh sách các lời mời vào nhóm đang chờ xử lý của chính mình.
- **Quyền:** User (`Active`).
- **Response `200 OK`:**
```json
[
  {
    "id": "uuid-invite",
    "groupId": "uuid-group",
    "groupName": "Nhà Trọ 402",
    "invitedUserId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "status": "Pending",
    "expiresAt": "2026-11-05T08:00:00Z",
    "token": "hex-token-string"
  }
]
```

#### 4. `POST /api/groups/invites/{inviteId}/accept?token={token}`
- **Mô tả:** Người dùng chấp thuận lời mời vào nhóm bằng `inviteId` và `token` tương ứng. Tự động thêm người dùng vào danh sách thành viên nhóm.
- **Quyền:** User được mời (`Active`).
- **Query Params:** `token=hex-token-string` (bắt buộc).
- **Response `200 OK`:** Trả về chi tiết `GroupDto` của nhóm vừa gia nhập.
- **Lỗi thường gặp:** `404 NotFound` (`invite_not_found`, `group_not_found`), `409 Conflict` (`invite_unavailable`, `already_member`, `active_group_limit`, `group_unavailable`), `403 Forbidden` (`group_member_ineligible`).

#### 5. `POST /api/groups/invites/{inviteId}/reject`
- **Mô tả:** Người dùng từ chối lời mời vào nhóm.
- **Quyền:** User được mời (`Active`).
- **Response `200 OK`:**
```json
{
  "id": "uuid-invite",
  "groupId": "uuid-group",
  "groupName": "Nhà Trọ 402",
  "invitedUserId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "status": "Rejected",
  "expiresAt": "2026-11-05T08:00:00Z",
  "token": "hex-token-string"
}
```
- **Lỗi thường gặp:** `404 NotFound` (`invite_not_found`), `409 Conflict` (`invite_unavailable`).

#### 6. `POST /api/v1/funds/{id}/contributions`
- **Mô tả:** Nộp tiền vào Quỹ Cố định V2 từ ví chính của thành viên.
- **Quyền:** Thành viên quỹ (`Active`). Yêu cầu mã PIN 6 số.
- **Request Body:**
```json
{
  "amount": 500000.00,
  "sourceWalletId": "uuid-wallet-main",
  "securityPin": "123456"
}
```
- **Response `200 OK`**

#### 7. `GET /api/v1/funds/{id}/contribution-allocation`
- **Mô tả:** Lấy thông tin phân bổ nghĩa vụ đóng góp và hạn mức chi tiêu khả dụng cá nhân (`CanSpend`).
- **Quyền:** Thành viên quỹ.
- **Response `200 OK`:**
```json
{
  "fundId": "uuid-fund",
  "userId": "uuid-user",
  "obligationTotal": 1000000.00,
  "contributed": 1500000.00,
  "allocatedObligation": 1000000.00,
  "obligationRemaining": 0.00,
  "canSpend": 500000.00,
  "isEligibleToSpend": true
}
```

#### 8. `POST /api/v1/funds/{id}/activity-proposals`
- **Mô tả:** Tạo đề xuất hoạt động / chi tiêu chung từ quỹ nhóm.
- **Quyền:** Thành viên quỹ.
- **Request Body:**
```json
{
  "title": "Mua bình nước uống",
  "estimatedAmount": 120000.00,
  "description": "2 bình nước Lavie 19L"
}
```
- **Response `201 Created`:** `V1ActivityProposalDto`.

#### 9. `POST /api/v1/funds/activity-proposals/{id}/settle`
- **Mô tả:** Quyết toán đề xuất hoạt động (Chủ quỹ phê duyệt hoặc từ chối).
- **Quyền:** Chủ quỹ (`Owner`). Yêu cầu mã PIN 6 số.
- **Request Body:**
```json
{
  "actualAmount": 120000.00,
  "status": "Approved",
  "note": "Đã mua và có hóa đơn kèm theo",
  "securityPin": "123456"
}
```
- **Response `200 OK`**

> **Quan trọng cho frontend V1:** Các route `/api/groups` ở mục 1–5 phía trên là **nhóm legacy**, không phải API quản lý `V1FundKind.Free`; không lấy mô hình lời mời kèm token của legacy áp dụng cho quỹ tự do V1. Các ví dụ mục 6–9 là mô tả cũ, cần đối chiếu lại DTO thực tế trước khi sử dụng. Với Free Fund, dùng các API `/api/v1/funds` chính thức dưới đây, đã đối chiếu `V1FundsController` và DTO hiện hành.

#### 10. Bản đồ API quỹ tự do V1 (dành cho Mobile User và kiểm thử)

| Nghiệp vụ | API hiện có | Lưu ý |
|---|---|---|
| Tạo quỹ tự do | `POST /api/v1/funds` | Body `{ "name": "Quỹ sinh hoạt", "kind": "Free", "targetVnd": null, "securityPin": "<PIN>" }`. |
| Danh sách / chi tiết / thành viên | `GET /api/v1/funds`; `GET /api/v1/funds/{id}`; `GET /api/v1/funds/{id}/members` | `ownerUserId` phân biệt chủ nhóm, không dựa vào role Admin. |
| Mời bạn bè (kể cả thành viên thường) | `POST /api/v1/funds/{id}/invitations` | Body `{ "userId": "<user-guid>" }`. Free Fund gửi lời mời `Pending` trực tiếp, không cần chủ nhóm xét duyệt. |
| Hộp thư lời mời / đồng ý / từ chối | `GET /api/v1/funds/invitations`; `POST /api/v1/funds/invitations/{id}/accept`; `POST /api/v1/funds/invitations/{id}/decline` | Người nhận tự quyết định; `DELETE /api/v1/funds/invitations/{id}` để hủy lời mời theo quyền hiện tại. |
| Góp tiền | `POST /api/v1/funds/{id}/contributions` | Body `{ "walletId": "<wallet-guid>", "amountVnd": 100000, "idempotencyKey": "<unique-key>", "securityPin": "<PIN>", "obligationRevision": null, "purpose": "Góp quỹ" }`. |
| Yêu cầu chi/rút | `POST /api/v1/funds/{id}/requests` | Body `{ "kind": "Expense", "amountVnd": 100000, "recipientUserId": "<user-guid>", "purpose": "Mua đồ chung", "idempotencyKey": "<unique-key>", "securityPin": "<PIN>" }`. Dùng `"Withdrawal"` và `recipientUserId` là chính người yêu cầu khi rút cá nhân. |
| Danh sách, duyệt/từ chối, hủy yêu cầu | `GET /api/v1/funds/{id}/requests`; `POST /api/v1/funds/requests/{id}/review`; `POST /api/v1/funds/requests/{id}/cancel` | Duyệt/từ chối body `{ "approved": true, "reason": "Hợp lệ", "securityPin": "<PIN>" }`; hủy body `{ "securityPin": "<PIN>" }`. |
| Chuyển quyền tự nguyện | `POST /api/v1/funds/{id}/ownership-offers`; `POST /api/v1/funds/ownership-offers/{id}/accept` | Body tạo `{ "userId": "<member-guid>", "securityPin": "<PIN>" }`; nhận quyền `{ "securityPin": "<PIN>" }`. **Cần bổ sung hủy yêu cầu chờ ở thời điểm đổi owner.** |
| Kiểm tra vắng mặt / bầu cử | `GET /api/v1/funds/{id}/governance`; `POST /api/v1/funds/{id}/elections`; `GET /api/v1/funds/elections/{id}` | Mở bầu cử body `{ "expectedOwnershipRevision": 0 }` (giá trị phải lấy từ governance thực tế, không hardcode 0). Còn các API ứng cử/bỏ phiếu/chuyển vòng/chấp nhận chức tại controller. |
| Ghi tương tác người dùng | `POST /api/v1/activity` | Gọi khi user thực sự tương tác ở foreground; không gọi từ polling, refresh hay background sync. |
| Xem hoàn tiền, rời/loại/giải tán | `GET /api/v1/funds/{id}/refund-preview`; `POST /api/v1/funds/{id}/leave`; `POST /api/v1/funds/{id}/members/{userId}/kick`; `POST /api/v1/funds/{id}/dissolve` | API hoàn tiền cần PIN và idempotency; kick cần thêm lý do. **Cần bổ sung chặn kick/dissolve khi đang bầu cử.** |

**Thiết kế frontend:** Hộp thư mời phải có nút **Chấp nhận/Từ chối**; trang quỹ gồm thành viên, góp tiền, số dư, lịch sử và yêu cầu chờ duyệt; chủ nhóm có danh sách **Duyệt/Từ chối**; trang quản trị quỹ có **Chuyển quyền/Bầu cử/Rời quỹ/Giải tán** theo đúng quyền và trạng thái. Khi backend triển khai quyết định mới, hiển thị trạng thái `Cancelled` kèm thông báo “Chủ nhóm đã thay đổi; vui lòng gửi lại yêu cầu”, không tự gửi lại hay tự duyệt thay người dùng.

**Checklist nghiệm thu bổ sung (chưa tuyên bố PASS):** (a) B mời C trực tiếp và C phải chấp nhận; (b) thành viên yêu cầu chi/rút phải được chủ duyệt, chủ tự chi `Expense`; (c) khóa kick/dissolve khi có bầu cử; (d) cả hai luồng đổi chủ đều tự hủy `Expense` và `Withdrawal` còn `AwaitingOwnerReview`, thông báo đúng người, không hủy `Completed`; (e) dữ liệu 15 ngày chỉ hợp lệ khi xác minh bao phủ hoạt động; (f) refund-preview khớp Leave/Kick/Dissolve và không hoàn trùng.


---

### 3.7. Ngân sách & Mục tiêu Tiết kiệm (Budgets & Goals)

#### 1. `GET /api/budgets?year=2026&month=10`
- **Mô tả:** Lấy danh sách ngân sách và tiến độ chi tiêu theo tháng của các danh mục.
- **Quyền:** User.
- **Response `200 OK`:** Mảng `BudgetDto` (`id`, `categoryId`, `categoryName`, `amount`, `spentAmount`, `remainingAmount`, `warningThresholdPercent`, `isOverBudget`).

#### 2. `POST /api/budgets`
- **Mô tả:** Tạo ngân sách chi tiêu cho danh mục.
- **Quyền:** User (`Active`).
- **Request Body:**
```json
{
  "categoryId": "uuid-cat-food",
  "amount": 3000000.00,
  "year": 2026,
  "month": 11,
  "warningThresholdPercent": 80
}
```
- **Response `201 Created`:** `BudgetDto`.

#### 3. `GET /api/goals`
- **Mô tả:** Lấy danh sách mục tiêu tiết kiệm cá nhân.
- **Quyền:** User.
- **Response `200 OK`:** Mảng `GoalDto`.

#### 4. `POST /api/goals/{id}/contributions`
- **Mô tả:** Nạp tiền vào mục tiêu tiết kiệm từ Ví chính.
- **Quyền:** User (`Active`). Yêu cầu mã PIN 6 số.
- **Request Body:**
```json
{
  "sourceWalletId": "uuid-wallet-main",
  "amount": 1000000.00,
  "securityPin": "123456"
}
```
- **Response `200 OK`:** `GoalDto`.

#### 5. `POST /api/goals/{id}/withdrawals`
- **Mô tả:** Rút tiền từ mục tiêu tiết kiệm về Ví chính.
- **Quyền:** User (`Active`). Yêu cầu mã PIN 6 số.
- **Request Body:**
```json
{
  "destinationWalletId": "uuid-wallet-main",
  "amount": 500000.00,
  "securityPin": "123456"
}
```
- **Response `200 OK`:** `GoalDto`.

---

### 3.8. Hỗ trợ & Khiếu nại Giao dịch (Support & Complaints)

#### 1. `POST /api/cases/support`
- **Mô tả:** Mở yêu cầu hỗ trợ hoặc khiếu nại gắn liền với một giao dịch tài chính cụ thể.
- **Quyền:** User (`Active` hoặc `Restricted`).
- **Request Body:**
```json
{
  "transactionId": "uuid-transaction-issue",
  "title": "Chuyển tiền bị trừ 2 lần",
  "description": "Lúc 14:02 mạng bị chập chờn khiến lệnh trừ tiền lặp lại",
  "category": "TransactionIssue"
}
```
- **Response `201 Created`:** `CaseDto` (`id`, `title`, `description`, `status`: "Pending", `userId`, `transactionId`, `createdAt`).

#### 2. `GET /api/cases/support`
- **Mô tả:** Xem danh sách các yêu cầu hỗ trợ/khiếu nại của chính mình.
- **Quyền:** User.
- **Response `200 OK`:** Mảng `SupportCaseDetailDto`.

#### 3. `GET /api/cases/support/{id}`
- **Mô tả:** Xem chi tiết một hồ sơ hỗ trợ kèm lịch sử tin nhắn trao đổi với Admin.
- **Quyền:** User (chính chủ sở hữu hồ sơ).
- **Response `200 OK`:** `SupportCaseDetailDto` (`id`, `title`, `description`, `status`, `messages`: `[ { senderId, senderName, isAdmin, content, createdAt } ]`).

#### 4. `POST /api/cases/support/{id}/messages`
- **Mô tả:** Gửi tin nhắn phản hồi, bổ sung thông tin hoặc bằng chứng đối soát vào hồ sơ hỗ trợ.
- **Quyền:** User (chính chủ).
- **Request Body:**
```json
{
  "message": "Tôi xin gửi mã tra soát ngân hàng: REF839201"
}
```
- **Response `200 OK`:** `SupportCaseDetailDto`.

---

### 3.9. Trợ lý Tài chính AI (AI Assistant — Phương án B)

#### 1. `POST /api/ai/conversations`
- **Mô tả:** Mở phiên hội thoại tư vấn tài chính mới với AI.
- **Quyền:** User.
- **Request Body:**
```json
{
  "title": "Tư vấn chi tiêu tháng 10"
}
```
- **Response `201 Created`:** `AiConversationDto` (`id`, `title`, `createdAt`).

#### 2. `POST /api/ai/chat`
- **Mô tả:** Gửi câu hỏi cho trợ lý AI. Tự động chuyển mạch an toàn sang chính sách quy tắc nội bộ (`SpendWisePolicy`) nếu Gemini API ngoài gặp lỗi kết nối hoặc vượt hạn ngạch 429.
- **Quyền:** User.
- **Request Body:**
```json
{
  "conversationId": "uuid-conversation",
  "message": "Tháng này tôi đã tiêu bao nhiêu tiền cho ăn uống và có bị vượt ngân sách không?"
}
```
- **Response `200 OK`:**
```json
{
  "reply": "Bạn đã chi 2.850.000 VNĐ cho danh mục Ăn uống, đạt 95% ngân sách đề ra (3.000.000 VNĐ). Bạn còn 150.000 VNĐ cho 2 ngày tới.",
  "provider": "Gemini_Live",
  "latencyMs": 750
}
```

---

### 3.10. Thông báo Hệ thống (Notifications)

#### 1. `GET /api/notifications`
- **Mô tả:** Lấy danh sách thông báo người dùng (giao dịch, biến động số dư, thông báo broadcast toàn hệ thống).
- **Quyền:** User.
- **Query Params:** `unreadOnly=false&page=1&pageSize=20`
- **Response `200 OK`:** `PagedResult<NotificationDto>`.

#### 2. `POST /api/notifications/{id}/read`
- **Mô tả:** Đánh dấu một thông báo đã đọc.
- **Quyền:** User.
- **Response:** `204 NoContent`.

#### 3. `POST /api/notifications/read-all`
- **Mô tả:** Đánh dấu tất cả thông báo là đã đọc.
- **Quyền:** User.
- **Response:** `204 NoContent`.

---

## 4. RANH GIỚI TRÁCH NHIỆM FRONTEND / MOBILE APP (FRONTEND-ONLY)

1. **Giao diện & Trực quan hóa (UI/UX):**
   - Triển khai 3 chế độ Theme: Sáng (`light`), Tối (`dark`), Theo hệ thống (`system`).
   - Quản lý bộ chuỗi đa ngôn ngữ (Localization): Tiếng Việt (`vi-VN`) và Tiếng Anh (`en-US`).
   - Xử lý hiệu ứng chuyển động (animations), toast thông báo, skeleton loading khi chờ dữ liệu.
2. **Sinh trắc học Native (Biometrics Hardware):**
   - Tương tác với cảm biến phần cứng qua API Native của OS (Face ID / Touch ID qua `LocalAuthentication` trên iOS; `BiometricPrompt` trên Android).
   - Backend chỉ quản lý cờ trạng thái `enableBiometric` trong preferences và xác thực token JWT, không can thiệp trực tiếp vào phần cứng cảm biến.
3. **Lưu trữ an toàn trên thiết bị (Client Secure Storage):**
   - Lưu trữ `accessToken` trong RAM hoặc Secure Storage.
   - Quản lý `refreshToken` an toàn trong Keychain / Keystore; tự động gọi `POST /api/auth/refresh` ngầm trước khi token hết hạn.
   - Xóa toàn bộ token và điều hướng về màn hình Đăng nhập khi nhận mã lỗi `401 Unauthorized`.
4. **Idempotency & Mất mạng (Offline Resilience):**
   - Tự động sinh `X-Idempotency-Key` (UUIDv4 ngẫu nhiên) cho mỗi lần bấm nút chuyển tiền/thanh toán.
   - Hiển thị thông báo mất kết nối nếu thiết bị offline, không gọi API lặp lại khi chưa có mạng.
