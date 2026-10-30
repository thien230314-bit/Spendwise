# SPENDWISE V1.0 — ĐẶC TẢ KỸ THUẬT VÀ TÀI LIỆU API PHÂN HỆ USER

> **Phiên bản:** V1.0 (Production Candidate)  
> **Đối tượng sử dụng:** Frontend Web & Mobile Client Developers (Codex / Engineering Team).  
> **Trạng thái đối chiếu:** Tài liệu đồng bộ API User theo code controller/DTO trên nhánh `main` ngày 30/10/2026. Kết quả Newman 306 requests / 343 assertions là bằng chứng kiểm thử lịch sử, không tự chứng nhận mọi luồng vận hành hiện tại. **Các quyết định Free Fund mới đang chờ code**; quỹ cố định V2 được hoãn rà soát theo yêu cầu.  
> **Ranh giới tài liệu:** Thuần tài liệu kỹ thuật, API contract, ràng buộc dữ liệu và mã lỗi. Không chứa lý thuyết kiến trúc chung, tiếp thị hay hướng dẫn framework UI.

---

## 1. NGUYÊN TẮC NGHIỆP VỤ & BẢO MẬT BẮT BUỘC (CORE BUSINESS RULES)

### 1.1. Hai vai trò, vòng đời tài khoản, quyền sở hữu

- **Chỉ có hai vai trò hệ thống sử dụng trong V1.0:** `User` (mobile User) và `Admin` (web quản trị). Mã enum `SystemRole` trong Domain còn giữ giá trị legacy `SystemAdmin=0`, `SuperAdmin=3` để tương thích dữ liệu cũ, **không phải hai role sản phẩm đang triển khai**. `Fund Owner`/chủ nhóm là một **User** có quyền đối với quỹ, không phải Admin.
- `UserStatus`: `Active` (hoạt động), `Restricted` (vẫn đọc dữ liệu và dùng chức năng phi tài chính; thao tác ghi nợ tài chính bị chặn theo từng handler), `Locked` (chặn đăng nhập/phiên, thu hồi session), `Suspended` (tài khoản bị đình chỉ). `MustChangePassword` có thể buộc đổi mật khẩu trước khi tiếp tục.
- Đăng ký email + OTP, thiết lập PIN và đăng nhập hai bước (mật khẩu → challenge → PIN); JWT access + refresh rotation; danh sách/thu hồi phiên; khôi phục mật khẩu/PIN và Account Recovery. Đổi email phải xác minh OTP, avatar thật multipart.
- Mỗi yêu cầu sửa dữ liệu phải xác thực user từ JWT; **không** nhận `userId` tự khai báo là nguồn phân quyền. Lỗi xác thực 401/403, lỗi nghiệp vụ thường 400/409, quá giới hạn 429; xử lý theo `code` từ API, không so sánh chuỗi `message`.

### 1.2. Ví, sổ cái, tiền và khả năng nhà cung cấp

- Ví chính được backend tạo khi onboard. **Tối đa 3 ví cá nhân, bao gồm ví chính; ví đã đóng vẫn tính vào giới hạn**. API chỉ cho tạo ví phụ `type` hợp lệ với tên và `isDefault`, **không** cho client tự tạo ví Main. Đóng ví giữ lịch sử/ledger.
- Tiền và lịch sử ghi theo ledger hai vế; không dùng giao diện làm nguồn sự thật số dư. `WalletDto` dùng `availableBalance`, không phải thuộc tính `balance`. Quy trình kiểm soát ngân hàng hoạt động (`active_bank_required`) có thể chặn thao tác tài chính tùy use case.
- Giao dịch tài chính cần `idempotencyKey` ở **trường request body theo DTO tương ứng** (một số clients thêm `X-Idempotency-Key`, nhưng header **không thay thế** trường body khi DTO có yêu cầu). Khi retry cùng mục đích, dùng lại key; không sinh key mới cho cùng một intent. Dùng PIN khi DTO/handler bắt buộc; không giữ PIN trên client.
- P2P là chuyển tiền **nội bộ SpendWise**; bank-direct là tạo lệnh/hướng dẫn chuyển khoản ngân hàng, **không tự ghi giảm ví SpendWise**. SePay Test Mode cho kiểm chứng luồng sandbox và ledger; không đồng nghĩa API rút/chuyển tiền ngân hàng thật đã được cấp quyền.

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

### 1.4. Hỗ trợ, tranh chấp, khiếu nại

- Hai loại hồ sơ: **Support** (`POST /api/cases/support` với `category,priority,subject,description,linkedTransactionId?`) và **Dispute** (`POST /api/cases/disputes` với `linkedTransactionId,reason,description?`). Cả hai có danh sách, chi tiết, tin nhắn và đính kèm, riêng biệt theo chủ sở hữu.
- Người dùng có thể trao đổi với Admin trong hồ sơ, đính kèm tài liệu. Backend chỉ cho liên kết giao dịch thực sự thuộc chính người tạo. Khi Admin cập nhật trạng thái hồ sơ **không được mặc nhiên suy ra hoàn tiền tự động**; xử lý điều chỉnh/hoàn tiền là nghiệp vụ tài chính riêng có điều kiện và chứng từ.
- Tranh chấp khoản chi Fixed V2 được hoãn rà soát chi tiết cùng quỹ cố định, không áp dụng quy tắc suy diễn từ Support thường cho nó.

## 2. Quy ước API, dữ liệu và frontend

1. **URL:** Base URL backend + đường dẫn tuyệt đối `/api/...`. JSON body mặc định `Content-Type: application/json`; upload avatar/evidence/case/AI attachment dùng `multipart/form-data`. Phần lớn API yêu cầu `Authorization: Bearer <accessToken>`, các endpoint đăng ký/đăng nhập/khôi phục là anonymous.
2. **DTO là nguồn sự thật:** Thuộc tính JSON camelCase theo tên record C#, ví dụ `idempotencyKey`, `securityPin`, `sourceWalletId`, `recipientUserId`; không đoán bằng mẫu UI hoặc bản Postman cũ. Enum input thường là chuỗi đúng chính tả như `"Free"`, `"Expense"`.
3. **Thời gian:** Backend trả ISO 8601 UTC/offset; client chỉ đổi múi giờ khi hiển thị. Số tiền VND truyền dưới dạng JSON number theo DTO `decimal`/`long`, không gửi chuỗi đã định dạng có dấu chấm/phẩy.
4. **Lỗi:** Phân biệt 400 input, 401 hết phiên, 403 hạn chế quyền, 404 ID không thuộc chủ sở hữu, 409 trạng thái xung đột và 429 giới hạn yêu cầu. Không tự retry lệnh ghi tiền bằng idempotency key mới. UI phải hiển thị lỗi rõ và không giả lập thành công.
5. **Realtime/thông báo:** Dùng API notifications làm dữ liệu gốc, realtime/push chỉ là tín hiệu làm mới; không coi push đã giao thành ledger commit.
6. **Phạm vi:** Mục `/api/groups` là **legacy compatibility**, khác quỹ `/api/v1/funds`. Hệ thống chỉ có vai trò hoạt động `User`/`Admin`. **Không triển khai frontend Fixed V2 theo phần chưa rà soát**.

## 3. DANH MỤC API PHÂN HỆ USER

### 3.1. Đăng ký, đăng nhập, PIN, phiên, khôi phục

**Nguồn:** `AuthController`, `SecurityPinController`, `AccountRecoveryController`; DTO `Identity/AuthContracts.cs`.

| API | Body chính xác / kết quả | Quy tắc UI |
|---|---|---|
| `POST /api/auth/register` | `{fullName,email,phone,password,confirmPassword}` → `EmailVerificationPendingResponse {status,expiresAt,resendAfterSeconds}` | Email OTP 8 số, không cấp phiên trước khi xác minh. |
| `POST /api/auth/email-verification/verify` | `{email,otp,deviceName?}` → `{challengeToken,status,expiresAt}` | `status` chỉ bước PIN cần làm, không tự suy đoán từ ID. |
| `POST /api/auth/email-verification/resend` | `{email}` → trạng thái chờ và thời gian gửi lại | Chống spam/cooldown. |
| `POST /api/auth/login` | `{identifier,password,deviceName?}` → `LoginChallengeResponse` | `identifier` có thể là email hoặc SpendId; chưa phải access token. |
| `POST /api/auth/pin/setup` | `{challengeToken,pin,confirmPin}` → `AuthResponse` | Thiết lập PIN lần đầu. |
| `POST /api/auth/pin/verify` | `{challengeToken,pin,deviceTrustToken?}` → `AuthResponse` | Đây là **bước hoàn tất đăng nhập**, khác `/api/security-pin/verify`. |
| `POST /api/auth/refresh` | `{refreshToken,deviceName?}` → `AuthResponse` | Xoay refresh token, thay cặp token sau mỗi lần refresh hợp lệ. |
| `POST /api/auth/logout` | Thu hồi refresh/session của phiên theo request controller | Xóa credentials lưu trên thiết bị khi đăng xuất. |
| `GET /api/auth/sessions` | `SessionDto[]` | Danh sách thiết bị/phiên của chính user. |
| `DELETE /api/auth/sessions/{sessionId}` | `204` | Thu hồi đúng phiên được chọn. |
| `POST /api/auth/change-password` | `{currentPassword,newPassword}` → `204` | Không có `confirmPassword` trong DTO backend của endpoint này. |
| `POST /api/auth/forgot-password` | `{identifier}` → `202` | Phản hồi trung tính, không dò tài khoản. |
| `POST /api/auth/reset-password` | `{identifier,otp,newPassword}` → `204` | OTP dùng một lần; xử lý phiên cũ theo backend. |
| `POST /api/auth/pin/forgot` | `{identifier}` | Gửi luồng khôi phục PIN an toàn qua email. |
| `POST /api/auth/pin/reset/validate` | `{token}` → `{valid,expiresAt}` | Kiểm tra token khôi phục, không nhầm OTP đăng ký. |
| `POST /api/auth/pin/reset` | `{token,newPin,confirmPin}` → `204` | Token reset PIN chỉ dùng một lần. |
| `GET /api/security-pin/status` | `{isSet}` | Kiểm tra trạng thái PIN từ phiên đã đăng nhập. |
| `POST /api/security-pin/verify` | `{pin}` → `204` | Kiểm tra PIN giao dịch, **không cấp access token**. |
| `PUT /api/security-pin` | `{currentPin,newPin,confirmPin}` → `204` | Đổi PIN khi đang đăng nhập. |
| `POST /api/account-recovery` | Yêu cầu khôi phục tài khoản (DTO riêng của AccountRecovery) | Gửi yêu cầu cho quy trình kiểm tra Admin, không tự mở khóa tài khoản. |

**Định dạng token:** `AuthResponse` gồm `accessToken, refreshToken, accessTokenExpiresAt, user, deviceTrustToken?`. `UserProfile` gồm `id,fullName,email,spendId,phone,systemRole,status,mustChangePassword,preferredLanguage,preferredTheme,defaultCurrency,createdAt,lastLoginAt,engagementEmailOptIn,avatarUrl`. Không hiển thị `expiresIn` hoặc `mainWalletBalance` như trường của chính các DTO này. PIN/mật khẩu/OTP không log ra console/analytics.

**Passkey (WebAuthn):** `POST /api/auth/passkeys/register/options`, `POST /api/auth/passkeys/register/verify` (cần đăng nhập); `POST /api/auth/passkeys/login/options`, `POST /api/auth/passkeys/login/verify` (anonymous); `GET /api/auth/passkeys`, `DELETE /api/auth/passkeys/{id}`. Client lấy challenge/options từ server và hoàn tất bằng browser/native authenticator; không được giả lập xác thực sinh trắc học. Đăng nhập passkey trả `AuthResponse`.

### 3.2. Hồ sơ, bạn bè, thiết bị và thông tin người dùng

**Hồ sơ cá nhân (`UsersController`):**

| Endpoint | Contract và quy tắc |
|---|---|
| `GET /api/users/me` | Trả **`UserProfile`** với `preferredTheme,preferredLanguage,defaultCurrency,engagementEmailOptIn,avatarUrl`, không có `preferences` object lồng và `mainWalletBalance`. Số dư lấy từ Wallet/Dashboard. |
| `PUT /api/users/me/preferences` | Body `{preferredLanguage:"vi-VN",preferredTheme:"system",defaultCurrency:"VND",engagementEmailOptIn:false}` → **`UserProfile`**. Cờ biometric/notification hardware không phải thuộc tính DTO này. |
| `POST /api/users/me/avatar` | Multipart key **`file`**, JPG/PNG/WebP, tối đa 5 MB; trả `UserProfile` mới. |
| `POST /api/users/me/email/request` | Body `{newEmail}` → `EmailVerificationPendingResponse`. |
| `POST /api/users/me/email/confirm` | Body `{newEmail,otp}` → `UserProfile`. |
| `DELETE /api/users/me/sessions` | Thu hồi các phiên khác, `204`. |

**Bạn bè (`FriendsController`; tối đa 100 bạn bè):** Người dùng phải kết bạn thành công trước khi mời vào quỹ V1. Không lộ email/số điện thoại người lạ tùy tiện.

| Endpoint | Chức năng |
|---|---|
| `GET /api/friends/search?contact=...&kind=...` | Tìm chính xác theo `contact` (+ `kind` tùy chọn); **không phải** tham số `query`. |
| `GET /api/friends/discover?query=...&page=1&pageSize=20` | Khám phá user hệ thống, trả phân trang với thông tin công khai/relationshipStatus. |
| `GET /api/friends` | Danh sách bạn bè, tên gợi nhớ và đánh dấu yêu thích. |
| `GET /api/friends/requests/incoming` / `GET /api/friends/requests/outgoing` | Lời mời đến/đi. |
| `POST /api/friends/requests` | Gửi lời mời kết bạn (xem `SendFriendRequest` về trường `contact`); chống tự mời/trùng lời mời. |
| `POST /api/friends/requests/{requestId}/accept` / `reject` | Nhận hoặc từ chối lời mời. |
| `DELETE /api/friends/requests/{requestId}` | Hủy lời mời do mình gửi. |
| `DELETE /api/friends/{friendUserId}` | Hủy kết bạn. |
| `PATCH /api/friends/{friendUserId}/preference` | Body `{alias?,isFavorite?}` theo DTO `UpdateFriendPreferenceRequest`. |

**Thiết bị nhận push:** `PUT /api/mobile/push/installations/{installationId}` đăng ký Expo push token + metadata thiết bị; `DELETE /api/mobile/push/installations/{installationId}` gỡ đăng ký. Đây là API lưu token/thiết bị, không phải bằng chứng đã thử push đến thiết bị thật.

### 3.3. Ví, thu/chi, chỉnh giao dịch, danh mục, bằng chứng, dashboard

**Ví cá nhân — `WalletsController`, `CreateWalletRequest`:**

- `GET /api/wallets` → `WalletDto[]`; `GET /api/wallets/{walletId}` → một ví.
- `POST /api/wallets`: `{name:"Ví du lịch",type:"Sub",isDefault:false}` → `WalletDto`. **Không có** `currency`/`targetAmount` trong request tạo ví. Main do hệ thống tạo; tối đa 3 ví cá nhân tính cả ví đã đóng. `WalletDto`: `id,name,type,currency,status,availableBalance,isDefault,createdAt`.
- `PUT /api/wallets/{walletId}`: `{name,type,isDefault}` → `WalletDto`; thay thông tin/tùy chọn ví, không được sửa số dư trực tiếp.
- `DELETE /api/wallets/{walletId}`: đóng ví có điều kiện, không xóa ledger/giao dịch lịch sử; không giả định đóng ví sẽ giải phóng suất 3 ví đã tạo.
- Các thao tác tài chính có thể yêu cầu ngân hàng Active, PIN, ví Active, kiểm tra số dư và idempotency. Trường `availableBalance` là số dư hiển thị.

**Thu chi — `TransactionsController`, `TransactionContracts.cs`:**

| API | Body, bộ lọc và tác dụng |
|---|---|
| `GET /api/transactions` | `page,pageSize,walletId?,categoryId?,type?,from?,to?,minimumAmount?,maximumAmount?,search?`; trả `PagedResult<TransactionDto>`. **Không dùng** `fromDate` thay `from`. |
| `GET /api/transactions/{transactionId}` | Chi tiết giao dịch, liên kết evidence, correction và phân bổ ví nếu có. |
| `POST /api/transactions/income` | `{walletId,categoryId,amount,description,occurredAt?,idempotencyKey?,securityPin?,evidenceIds?,counterparty?}` → `201 TransactionDto`. |
| `POST /api/transactions/expense` | Cùng `CreateManualTransactionRequest`, trừ tiền khi hợp lệ; không coi chỉ cần `walletId,amount` là đầy đủ điều kiện tài chính. |
| `POST /api/transactions/expense/multi-wallet` | `{amount,walletAllocations:[{walletId,amount},...],categoryId,description,occurredAt?,idempotencyKey?,securityPin?,evidenceIds?,counterparty?}`; tổng phân bổ phải đúng tổng khoản chi. |
| `PATCH /api/transactions/{transactionId}` | `{categoryId?,description,securityPin?,evidenceIds?,reason?,occurredAt?,walletId?,amount?,counterparty?,walletAllocations?}`; chỉnh sửa tài chính phải thực hiện **correction có sổ cái đảo/thay thế**, không sửa lịch sử bằng ghi đè tùy tiện. |

`TransactionDto` trả `id,walletId?,walletName?,categoryId?,categoryName?,type,status,amount,signedAmount,currency,description,counterpartyName?,occurredAt,ledgerTransactionId,relatedBusinessId?,correctionAllowance,correctionCount,canCorrect,corrections?,evidence?,correctionGrants?,walletAllocations?`. UI dùng `canCorrect` và quyền sửa được trả về, không tự cấp quyền.

**Danh mục cá nhân/hệ thống:** `GET /api/categories`, `GET /api/categories/{id}`, `POST /api/categories`, `PUT /api/categories/{id}`, `DELETE /api/categories/{id}`. Create/Update body `{name,kind,icon?,color?}`, với `kind` theo enum dữ liệu danh mục; `CategoryDto` có `isSystem,isActive`. Xóa có thể là archive khi đã được tham chiếu, không xóa lịch sử.

**Bằng chứng giao dịch:** `POST /api/evidence` (`multipart/form-data`, key `file`) → `EvidenceDto {id,originalFileName,contentType,length,contentHash,createdAt}`; `GET /api/evidence/{id}` tải file đã qua kiểm tra quyền. Dùng `evidenceIds` trên giao dịch/chỉnh sửa nơi hỗ trợ.

**Tổng quan:** `GET /api/dashboard` → `DashboardDto` tổng hợp từ backend. Không tính số dư hoặc tổng chi tiêu chỉ từ danh sách sample/mock trong UI.

### 3.4. Chuyển tiền giữa ví, P2P SpendWise và chuyển khoản ngân hàng trực tiếp

Ba luồng **khác nhau**, không được gộp một màn xác nhận có cùng hậu quả về số dư:

| Luồng / endpoint | Body chính xác | Kết quả |
|---|---|---|
| `POST /api/transfers/internal` | `{sourceWalletId,destinationWalletId,amount,description,idempotencyKey,securityPin}` | Chuyển giữa hai ví **cùng một user**, trả `201 TransferDto`, ghi ledger. |
| `POST /api/transfers/p2p` | `{sourceWalletId,recipientUserId,amount,description,idempotencyKey,securityPin}` | Chuyển giữa hai User SpendWise; `recipientUserId` là GUID lấy từ lựa chọn người nhận đã được backend trả; không dùng `recipientIdentifier` hay `note` làm trường DTO. Trả `201 TransferDto`. |
| `GET /api/transfers/recipients?phone=...` | Số điện thoại người nhận | **Tra cứu người nhận chuyển khoản ngân hàng**, trả `BankTransferRecipientDto {userId,fullName,bankBrand,accountHolder,maskedAccount}`, **không phải** API tra cứu người nhận P2P. |
| `POST /api/transfers` | `{recipientUserId,amount,note?,idempotencyKey}` | **Bank-direct**, tạo `BankDirectPaymentDto` và hướng dẫn thanh toán, **không trừ ví SpendWise khi vừa tạo**. |
| `GET /api/transfers` | `page?,pageSize?` | Lịch sử lệnh bank-direct, `PagedResult<BankDirectPaymentDto>`. |
| `GET /api/transfers/{transferId}` | `transferId` | Chi tiết lệnh bank-direct theo quyền gửi/nhận. |
| `POST /api/transfers/{paymentId}/confirm` | `{securityPin}` | Xác nhận luồng bank-direct theo kiểm tra ngân hàng và nghiệp vụ, không đồng nhất với chuyển P2P. |

`TransferDto` (internal/P2P) gồm `id,senderUserId,senderName,recipientUserId,recipientName,sourceWalletId,destinationWalletId,amount,currency,description,status,ledgerTransactionId?,completedAt?,replayed`. Bank-direct gồm thông tin ngân hàng đã che, `paymentReference,contextType,groupId?,status,senderConfirmedAt?,completedAt?,matchedBankTransactionId?`. UI không báo `Completed` khi mới có hướng dẫn/QR mà chưa có bằng chứng settlement.

### 3.5. Liên kết ngân hàng, lịch sử sao kê, SePay Test Mode

**Phân biệt nhà cung cấp thực và sandbox:** SePay Test Mode là môi trường mô phỏng nhà cung cấp nhưng API/đối soát có kiểm chứng. Không được trình bày Test Mode là rút tiền ngân hàng thật, cũng không cấp số dư ngân hàng thực tế khi provider không hỗ trợ. UI dựa trên trạng thái server (`Pending,Processing,PendingReconciliation,Completed,Failed,Cancelled` nếu được DTO/handler trả về), không tự đoán thành công.

**API quản lý kết nối thật (`BankConnectionsController`):**

| Endpoint | Body và tác dụng |
|---|---|
| `POST /api/bank-connections/link` | `{completionRedirectUri?,language:"vi",clientPlatform:"Web" | "Mobile"}` → `BankLinkSessionDto {sessionId,hostedLinkUrl,expiresAt,purpose}`. Client phải dùng hosted link provider, không giả lập liên kết. |
| `GET /api/bank-connections` | Danh sách `BankConnectionDto`: `id,provider,accountEnvironment,bankBrandName,accountType?,accountHolderName?,maskedAccountNumber,status,linkedAt?,lastSyncedAt?`. |
| `GET /api/bank-connections/{id}` | Chi tiết kết nối thuộc người dùng. |
| `POST /api/bank-connections/{id}/unlink` | Body `CreateBankLinkRequest`, trả session; chờ xác nhận provider. |
| `POST /api/bank-connections/{id}/reactivate` | Body `CreateBankLinkRequest`; kích hoạt lại qua luồng provider. |
| `POST /api/bank-connections/{id}/sync` | Đồng bộ, `BankSyncDto {importedCount,duplicateCount,lastSyncedAt}`. |
| `GET /api/bank-transactions` | Các filter `page,pageSize,bankConnectionId?,direction?,from?,to?,search?`; phân trang. |
| `GET /api/bank-transactions/{id}` | Chi tiết giao dịch ngân hàng thuộc quyền. |
| `PATCH /api/bank-transactions/{id}/category` | `{categoryId}`, phân loại giao dịch sao kê. |

**SePay Test Mode (chỉ môi trường Development theo guard backend):**

| Endpoint | Body / trạng thái |
|---|---|
| `POST /api/bank-connections/test-mode/link-challenges` | Không body, trả `{challengeId,reference,expiresAt,instruction}` để thực hiện mô phỏng kết nối. |
| `POST /api/bank-connections/test-mode/link-challenges/{challengeId}/confirm` | Không body; xác nhận dựa trên giao dịch provider tương ứng, trả `BankConnectionDto`. |
| `GET /api/bank-connections/test-mode/linked-accounts` | `TestModeLinkedAccountDto[]` (gồm `accumulatedBalance` mô phỏng, **không** là số dư ngân hàng thật). |
| `POST /api/bank-connections/test-mode/wallet-top-ups` | `{bankConnectionId,walletId,amount}`, trả `WalletTopUpDto` có reference/thời hạn/trạng thái Pending. |
| `GET /api/bank-connections/test-mode/wallet-top-ups` | Danh sách top-up. |
| `POST /api/bank-connections/test-mode/wallet-top-ups/{topUpId}/confirm` | Chỉ ghi có ví khi provider xác thực giao dịch khớp; không tự cộng khi chưa đối soát. |
| `POST /api/bank-connections/test-mode/wallet-withdrawals` | `{bankConnectionId,walletId,amount,securityPin,idempotencyKey}`; dùng cơ chế reservation/ledger, không coi Pending là tiền đã về ngân hàng. |
| `GET /api/bank-connections/test-mode/wallet-withdrawals` | Danh sách `WalletWithdrawalDto`. |
| `GET /api/bank-connections/test-mode/wallet-withdrawals/{id}` | Chi tiết yêu cầu rút. |
| `POST /api/bank-connections/test-mode/wallet-withdrawals/{id}/confirm` | Xử lý xác minh settlement theo provider/test mode. |
| `POST /api/bank-connections/test-mode/wallet-withdrawals/{id}/cancel` | Hủy theo trạng thái cho phép và hoàn reservation tương ứng. |

**Mã lỗi UI cần phân biệt:** `active_bank_required`, `wallet_unavailable`, `insufficient_wallet_balance`, `active_withdrawal_in_progress`, `wallet_top_up_pending`, `withdrawal_pending_reconciliation`, `withdrawal_failed`, `idempotency_conflict`. Khi `PendingReconciliation`, UI hiển thị cần đối soát; không trả tiền hoặc thực hiện lại với key mới cho tới khi có kết quả xác minh.

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
