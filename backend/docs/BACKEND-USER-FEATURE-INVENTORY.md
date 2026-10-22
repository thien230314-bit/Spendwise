# SPENDWISE — TOÀN BỘ DANH MỤC TÍNH NĂNG & API USER BACKEND (100% COVERAGE)

> **Mục tiêu tài liệu:** Ghi nhận và phân loại đầy đủ 100% các tính năng, API endpoint, quyền truy cập, nghiệp vụ, ràng buộc dữ liệu và trạng thái kiểm thử của toàn bộ domain User Backend SpendWise tại Git HEAD hiện tại.
> **Nguyên tắc bất biến:** Tài liệu đồng bộ chuẩn xác theo thực tế mã nguồn Backend; không cắt giảm hoặc thay thế tính năng Backend để ép khớp tài liệu cũ; không xóa các tính năng backwards compatibility (như legacy Groups); giữ nguyên toàn bộ các quy tắc bảo toàn tài chính và các trạng thái `CHƯA CHỐT — BLOCKER` của Product Owner.

---

## TỔNG QUAN HỆ THỐNG USER BACKEND

SpendWise Backend cung cấp tổng cộng **205 User endpoints** (trên tổng số 320 API endpoints của toàn bộ hệ thống gồm 115 Admin endpoints và 205 User endpoints), phân bổ trên 20 bộ Controller nghiệp vụ User.

### Bảng tổng hợp Controller và số lượng Endpoint

| STT | Nhóm chức năng (Controller) | Đường dẫn Route gốc | Số Endpoint | Rate Limit Policy | Quyền truy cập |
|---|---|---|:---:|---|---|
| 1 | **Xác thực & Bảo mật (`AuthController`)** | `/api/auth` | 16 | `auth`, `emailVerification`, `financial` | Anonymous / Token / Bearer |
| 2 | **Hồ sơ & Người dùng (`UsersController`)** | `/api/users` | 6 | `financial` (preferences, avatar) | Authenticated User |
| 3 | **Mã PIN giao dịch (`SecurityPinController`)** | `/api/pin` | 3 | `financial` | Authenticated User |
| 4 | **Khôi phục tài khoản (`AccountRecoveryController`)** | `/api/account-recovery` | 1 | Không | Anonymous / Challenged |
| 5 | **Đẩy thông báo thiết bị (`MobilePushController`)** | `/api/mobile/push` | 2 | Không | Authenticated User |
| 6 | **Bạn bè & Mạng xã hội (`FriendsController`)** | `/api/friends` | 11 | `userDiscovery` (30 req/min) | Authenticated User |
| 7 | **Ví cá nhân (`WalletsController`)** | `/api/wallets` | 5 | `financial` | Authenticated User |
| 8 | **Giao dịch thu/chi (`TransactionsController`)** | `/api/transactions` | 6 | `financial` | Authenticated User |
| 9 | **Chuyển tiền P2P & Nội bộ (`TransfersController`)** | `/api/transfers` | 7 | `financial` | Authenticated User |
| 10 | **Quỹ V1 authoritative (`V1FundsController`)** | `/api/v1/funds` | 36 | `financial` | Authenticated User (Member/Owner) |
| 11 | **Ghi nhận tương tác thực (`V1ActivityController`)** | `/api/v1/activity` | 1 | `financial` | Authenticated User |
| 12 | **Danh mục thu chi (`CategoriesController`)** | `/api/categories` | 5 | Không | Authenticated User |
| 13 | **Bằng chứng & Hóa đơn (`EvidenceController`)** | `/api/evidence` | 2 | `financial` | Authenticated User |
| 14 | **Ngân sách chi tiêu (`BudgetsController`)** | `/api/budgets` | 6 | Không | Authenticated User |
| 15 | **Mục tiêu tích lũy (`GoalsController`)** | `/api/goals` | 7 | Không | Authenticated User |
| 16 | **Giao dịch định kỳ (`RecurringTransactionsController`)** | `/api/recurring-transactions` | 8 | Không | Authenticated User |
| 17 | **Quy tắc phân loại tự động (`TransactionRulesController`)** | `/api/transaction-rules` | 5 | Không | Authenticated User |
| 18 | **Trợ lý AI Gemini (`AiController`)** | `/api/ai` | 11 | `ai` (15/min), `user-chat` (2 concurrent) | Authenticated User |
| 19 | **Khiếu nại & Hỗ trợ User (`CasesController`)** | `/api/cases` | 11 | Không | Authenticated User |
| 20 | **Thông báo trong ứng dụng (`NotificationsController`)** | `/api/notifications` | 8 | Không | Authenticated User |
| 21 | **Tích hợp Ngân hàng (`BankConnectionsController`)** | `/api/bank-connections` | 12 | Không | Authenticated User |
| 22 | **Lịch sử giao dịch Ngân hàng (`BankTransactionsController`)** | `/api/bank-transactions` | 3 | Không | Authenticated User |
| 23 | **SePay Webhook & IPN (`SePayBankHubIntegrationController`)** | `/api/bank-integrations/sepay` | 2 | IPN Signature Guard | Webhook Caller (SePay) |
| 24 | **Tổng quan tài chính (`DashboardController`)** | `/api/dashboard` | 1 | Không | Authenticated User |
| 25 | **Trạng thái bảo trì & Thông báo (`OperationsController`)** | `/api/system` | 2 | Không | Public / Authenticated |
| 26 | **Nhóm cũ (`GroupsController`)** *(Legacy / Compatibility)* | `/api/groups` | 29 | `financial` | Authenticated User |
| **Tổng cộng** | | | **205** | | |

---

## CHI TIẾT 11 NHÓM TÍNH NĂNG NGUYÊN BẢN CỦA BACKEND USER

---

### PHẦN 1: TÀI KHOẢN, XÁC THỰC, BẢO MẬT & HỒ SƠ CÁ NHÂN

#### 1. Mục đích & Phạm vi nghiệp vụ
Cung cấp toàn bộ luồng đăng ký tài khoản, gửi và xác thực mã OTP qua email, thiết lập và xác thực mật khẩu, thiết lập và đổi mã PIN giao dịch, refresh rotation token, quản lý phiên đăng nhập và thiết bị, cập nhật sở thích hiển thị (Theme, Language, Currency), đổi email nguyên tử bằng OTP xác thực, và upload ảnh đại diện Avatar thật qua multipart/form-data.

#### 2. Đối tượng thực hiện (Actors) & Phân quyền
- **Khách vãng lai (Anonymous):** Đăng ký tài khoản, xác thực OTP email, resend OTP email, quên/đặt lại mật khẩu qua email challenge, quên/đặt lại mã PIN.
- **Người dùng đã xác thực (Authenticated User):** Đăng nhập, refresh token, đổi mật khẩu, thiết lập/xác thực PIN, cập nhật tùy chọn Theme/Language, yêu cầu và xác nhận đổi email, tải lên Avatar ảnh đại diện, xem danh sách phiên đăng nhập và thu hồi phiên từ xa.

#### 3. Danh sách Endpoints và Phương thức HTTP
- `POST /api/auth/register` — Đăng ký tài khoản với Email, SpendId, FullName, Password. Rate limit: `auth` (10 req/min).
- `POST /api/auth/email-verification/verify` — Xác thực OTP kích hoạt tài khoản. Rate limit: `emailVerification` (10 req/min).
- `POST /api/auth/email-verification/resend` — Gửi lại OTP kích hoạt tài khoản. Rate limit: `emailVerification`.
- `POST /api/auth/login` — Đăng nhập bằng Email/SpendId và Password (hoặc PIN Login flow). Rate limit: `auth`.
- `POST /api/auth/refresh` — Làm mới Access Token thông qua Refresh Token xoay vòng (rotation token).
- `POST /api/auth/logout` — Đăng xuất và thu hồi Refresh Token của phiên hiện tại.
- `GET /api/auth/sessions` — Lấy danh sách các phiên đăng nhập đang hoạt động của tài khoản.
- `DELETE /api/auth/sessions/{sessionId}` — Thu hồi một phiên đăng nhập chỉ định.
- `POST /api/auth/change-password` — Đổi mật khẩu tài khoản khi đã đăng nhập (yêu cầu mật khẩu cũ).
- `POST /api/auth/forgot-password` — Yêu cầu gửi mã OTP khôi phục mật khẩu qua Email.
- `POST /api/auth/reset-password` — Đặt lại mật khẩu mới bằng mã OTP đã nhận.
- `POST /api/auth/pin/setup` — Thiết lập mã PIN 6 số lần đầu.
- `POST /api/auth/pin/verify` — Xác thực mã PIN giao dịch.
- `POST /api/auth/pin/forgot` — Yêu cầu cấp OTP khôi phục mã PIN.
- `POST /api/auth/pin/reset/validate` — Xác thực mã OTP khôi phục PIN trước khi đổi.
- `POST /api/auth/pin/reset` — Đặt lại mã PIN mới bằng token hợp lệ.
- `GET /api/users/me` — Lấy hồ sơ tài khoản hiện tại (bao gồm Email, SpendId, FullName, AvatarUrl, Theme, Language, DefaultCurrency).
- `PUT /api/users/me/preferences` — Cập nhật tùy chọn hiển thị (`preferredTheme`: light/dark/system, `preferredLanguage`: vi-VN/en-US, `defaultCurrency`: VND).
- `POST /api/users/me/avatar` — Upload ảnh đại diện Avatar thật (multipart/form-data, hỗ trợ JPG/PNG/WebP, dung lượng tối đa 5MB).
- `POST /api/users/me/email/request` — Yêu cầu đổi Email tài khoản, kiểm tra chống trùng lặp và gửi mã OTP về email mới.
- `POST /api/users/me/email/confirm` — Xác nhận OTP để hoàn tất đổi Email nguyên tử (cập nhật đồng bộ `Email` và `NormalizedEmail`).
- `DELETE /api/users/me/sessions` — Đăng xuất và thu hồi tất cả các phiên đăng nhập khác của người dùng.
- `GET /api/pin/status` — Kiểm tra trạng thái đã cài đặt mã PIN hay chưa.
- `POST /api/pin/verify` — Xác thực mã PIN tài khoản.
- `PUT /api/pin` — Cập nhật mã PIN mới khi cung cấp mã PIN cũ.
- `POST /api/account-recovery` — Gửi yêu cầu khôi phục tài khoản bị khóa/mất thiết bị.
- `PUT /api/mobile/push/{installationId}` — Đăng ký thiết bị nhận thông báo Expo Push Token.
- `DELETE /api/mobile/push/{installationId}` — Hủy đăng ký thiết bị nhận thông báo push.

#### 4. Dữ liệu Đầu vào & Đầu ra (DTOs)
- `RegisterRequest`: `{ email, spendId, fullName, password }` -> `AuthResponse`: `{ accessToken, refreshToken, expiresAt, user: UserDto }`
- `VerifyEmailRequest`: `{ email, code }` -> `ResultDto`
- `LoginRequest`: `{ identifier, password }` -> `AuthResponse`
- `UpdatePreferencesRequest`: `{ preferredTheme, preferredLanguage, defaultCurrency }` -> `UserProfileDto`
- `UploadAvatarRequest`: `IFormFile File` -> `UserProfileDto` (`avatarUrl: "/avatars/<userId>_<timestamp>.jpg"`)
- `RequestChangeEmailRequest`: `{ newEmail }` -> `ResultDto`
- `ConfirmChangeEmailRequest`: `{ newEmail, otp }` -> `UserProfileDto`

#### 5. Ràng buộc & Validation
- **Mật khẩu:** Tối thiểu 8 ký tự, gồm ít nhất 1 chữ hoa, 1 chữ thường, 1 số, 1 ký tự đặc biệt.
- **Mã PIN:** Đúng 6 chữ số, được băm bảo mật bằng HMAC-SHA256 với salt độc lập. Khóa tạm thời nếu nhập sai liên tiếp vượt ngưỡng.
- **Avatar:** Định dạng `.jpg`, `.jpeg`, `.png`, `.webp`. Kích thước tối đa 5,242,880 bytes (5MB). Magic byte validation ngăn chặn giả mạo extension.
- **Email Change:** Email mới không được trùng với Email hiện tại hoặc Email của tài khoản khác trong hệ thống. Mã OTP có hiệu lực 15 phút, chỉ được sử dụng đúng 1 lần.
- **Tài khoản Restricted:** Vẫn được đổi mật khẩu, sửa tùy chọn hiển thị và upload Avatar; nhưng bị chặn mọi thao tác rút/chi/góp tiền.
- **Tài khoản Locked:** Bị từ chối ngay lập tức tại middleware xác thực (HTTP 403 `account_unavailable`), thu hồi toàn bộ token và phiên làm việc.

---

### PHẦN 2: BẠN BÈ & MẠNG XÃ HỘI TÀI CHÍNH

#### 1. Mục đích & Phạm vi nghiệp vụ
Quản lý kết nối bạn bè hai chiều giữa các người dùng trong hệ thống: gửi lời mời kết bạn, chấp nhận/từ chối lời mời, hủy kết bạn, tìm kiếm bạn bè trong danh bạ, đặt tên gợi nhớ cá nhân (Nickname), đánh dấu bạn thân (Favorite), và khám phá người dùng hệ thống (`GET /api/friends/discover`) với tính năng bảo mật tuyệt đối không làm lộ số điện thoại, email hay nickname riêng tư.

#### 2. Đối tượng thực hiện & Phân quyền
- **Authenticated User:** Toàn quyền quản lý danh sách bạn bè của chính mình. Giới hạn tối đa **100 bạn bè** cho mỗi người dùng.

#### 3. Danh sách Endpoints
- `GET /api/friends` — Lấy danh sách bạn bè hiện tại (kèm `friendUserId`, `displayName`, `spendId`, `avatarUrl`, `isFavorite`, `commonGroupsCount`).
- `GET /api/friends/search?query=...` — Tìm kiếm bạn bè trong danh bạ bạn bè hiện có theo tên hoặc SpendId.
- `GET /api/friends/discover?query=...&page=1&pageSize=20` — Tìm kiếm công khai người dùng hệ thống để kết bạn. Phân trang an toàn (max 100,000 page). Áp dụng Rate Limit riêng `userDiscovery` (30 req/min). Parameter chính xác là `query`.
- `GET /api/friends/requests/incoming` — Lấy danh sách lời mời kết bạn gửi đến cho người dùng hiện tại.
- `GET /api/friends/requests/outgoing` — Lấy danh sách lời mời kết bạn do người dùng hiện tại đã gửi đi.
- `POST /api/friends/requests` — Gửi lời mời kết bạn đến một người dùng qua `TargetUserId`.
- `POST /api/friends/requests/{requestId}/accept` — Chấp nhận lời mời kết bạn (chuyển quan hệ thành Bạn bè hai chiều).
- `POST /api/friends/requests/{requestId}/reject` — Từ chối lời mời kết bạn.
- `DELETE /api/friends/requests/{requestId}` — Hủy lời mời kết bạn đã gửi đi.
- `DELETE /api/friends/{friendUserId}` — Hủy quan hệ bạn bè với một người dùng.
- `PATCH /api/friends/{friendUserId}/preference` — Đặt Nickname cá nhân hóa hoặc bật/tắt đánh dấu bạn thân (`isFavorite`).

#### 4. Bảo mật & Phòng ngừa rò rỉ dữ liệu (Privacy Guard)
- **`GET /api/friends/discover`:** Chỉ trả về thông tin tối thiểu cần thiết để nhận diện: `userId`, `spendId`, `displayName` (ưu tiên `DisplayName` nếu có, fallback `FullName`), `avatarUrl`, và `relationshipStatus` (`None`, `OutgoingPending`, `IncomingPending`, `Friends`).
- **Tuyệt đối không trả về:** `email`, `phoneNumber`, `nickname`, hoặc các thông tin tài chính riêng tư.
- **Ràng buộc số lượng:** Kiểm tra chặt chẽ giới hạn 100 bạn bè trước khi chấp nhận lời mời (`friend_limit_reached` 400).

---

### PHẦN 3: VÍ CÁ NHÂN, GIAO DỊCH THU/CHI & SỔ CÁI BẢO TOÀN (LEDGER)

#### 1. Mục đích & Phạm vi nghiệp vụ
Quản lý các ví tiền nội bộ của người dùng: 1 Ví chính (Main) và tối đa 2 ví phụ (tổng tối đa **3 ví/User**). Ghi nhận các giao dịch thu tiền (Income), chi tiêu đơn ví (Expense), chi tiêu đa ví (Multi-wallet Expense phân bổ tự động), sửa đổi giao dịch có truy vết kiểm toán (Ledger Correction qua HTTP PATCH), và tải lên chứng từ hóa đơn (Evidence attachment).

#### 2. Đối tượng thực hiện & Phân quyền
- **Authenticated User (Chủ sở hữu ví):** Toàn quyền xem và thực hiện giao dịch trên các ví của mình.
- Bắt buộc kiểm tra mã PIN giao dịch hợp lệ và khóa định danh thao tác (Intent / Idempotency Key) cho mọi giao dịch xuất tiền.

#### 3. Danh sách Endpoints
- `GET /api/wallets` — Lấy danh sách ví của người dùng hiện tại (kèm số dư khả dụng `availableBalance`, loại ví `walletType`, trạng thái ví chính `isDefault`).
- `GET /api/wallets/{walletId}` — Lấy chi tiết một ví.
- `POST /api/wallets` — Tạo ví phụ mới (tối đa không quá 3 ví hoạt động). Rate limit: `financial`.
- `PUT /api/wallets/{walletId}` — Cập nhật tên ví hoặc chuyển đổi ví mặc định.
- `DELETE /api/wallets/{walletId}` — Xóa ví (chỉ được xóa khi số dư bằng 0 và không phải Ví chính).
- `GET /api/transactions?page=1&pageSize=20&walletId=...&categoryId=...` — Lấy lịch sử giao dịch cá nhân có phân trang và bộ lọc.
- `GET /api/transactions/{transactionId}` — Lấy chi tiết một giao dịch.
- `POST /api/transactions/income` — Ghi nhận khoản thu tiền vào ví. Rate limit: `financial`.
- `POST /api/transactions/expense` — Ghi nhận khoản chi tiêu từ một ví cá nhân. Rate limit: `financial`.
- `POST /api/transactions/expense/multi-wallet` — Ghi nhận khoản chi tiêu phân bổ trừ tiền trên nhiều ví đồng thời. Rate limit: `financial`.
- `HttpPatch /api/transactions/{transactionId}` — Điều chỉnh thông tin giao dịch (danh mục, ghi chú, bằng chứng) có kiểm soát quyền và truy vết kiểm toán.
- `POST /api/evidence` — Tải lên tệp chứng từ/hóa đơn cho giao dịch (JPG, PNG, PDF <= 10MB). Rate limit: `financial`.
- `GET /api/evidence/{id}` — Tải về tệp chứng từ hóa đơn hợp lệ.

#### 4. Bất biến tài chính & Sổ cái kép (Double-Entry Ledger)
- Mọi biến động số dư ví đều được hạch toán qua bút toán sổ cái kép nguyên tử (`LedgerTransactions` và `LedgerEntries`).
- Tổng Debit luôn bằng tổng Credit. Số dư khả dụng của ví (`wallet.AvailableBalance`) khớp tuyệt đối với số dư tài khoản sổ cái (`wallet.LedgerAccount.Balance`).
- Không cho phép số dư ví bị âm (`insufficient_balance` 400).
- Không xóa cứng các giao dịch tài chính đã hoàn tất.

---

### PHẦN 4: CHUYỂN TIỀN NỘI BỘ VÀ CHUYỂN TIỀN P2P (TRANSFERS)

#### 1. Mục đích & Phạm vi nghiệp vụ
Cho phép chuyển tiền tức thời giữa các ví cá nhân của cùng một người dùng (Internal Transfer) và chuyển tiền P2P trực tiếp giữa hai người dùng trong hệ thống SpendWise dựa trên SpendId hoặc số tài khoản ví.

#### 2. Đối tượng thực hiện & Phân quyền
- **Authenticated User:** Bắt buộc nhập mã PIN giao dịch hợp lệ và cung cấp `IdempotencyKey`. Tài khoản người gửi không được ở trạng thái `Restricted` hoặc `Locked`.

#### 3. Danh sách Endpoints
- `POST /api/transfers/internal` — Chuyển tiền giữa 2 ví cá nhân của người dùng hiện tại. Rate limit: `financial`.
- `POST /api/transfers/p2p` — Chuyển tiền từ ví người gửi sang ví người nhận (qua SpendId hoặc UserId). Rate limit: `financial`.
- `GET /api/transfers/recipients` — Lấy danh sách người nhận gần đây để chuyển tiền nhanh.
- `GET /api/transfers/{transferId}` — Lấy chi tiết lệnh chuyển tiền.
- `POST /api/transfers` — Tạo giao dịch thanh toán chuyển tiền tổng quát.
- `GET /api/transfers` — Lấy danh sách các lệnh chuyển tiền.
- `POST /api/transfers/{paymentId}/confirm` — Xác nhận hoàn tất lệnh chuyển tiền.

#### 4. Ràng buộc bảo mật & Chống gian lận
- **Chống tự chuyển cho chính mình qua P2P:** Ngăn chặn chuyển P2P nếu người nhận chính là người gửi (`cannot_transfer_to_self` 400).
- **Chống replay & Race condition:** `IdempotencyKey` được ghi nhận nguyên tử trong database; nếu gửi lại cùng key trong thời gian hiệu lực, hệ thống trả lại kết quả giao dịch ban đầu, không trừ tiền hai lần.
- **Tài khoản người nhận bị Restricted:** Vẫn được nhận tiền P2P bình thường; chỉ người gửi bị hạn chế xuất tiền.

---

### PHẦN 5: QUỸ NHÓM V1 AUTHORITATIVE — NHÓM GÓP CỐ ĐỊNH (FIXED FUND)

#### 1. Mục đích & Ràng buộc cốt lõi
Nhóm góp cố định được thiết kế cho các hoạt động có mục tiêu chi phí định trước (ví dụ: du lịch nhóm, tổ chức sự kiện). Quy mô tối đa **30 thành viên**. Tổng chi phí dự kiến `T` được chia thành nghĩa vụ đóng góp nguyên số VND cho từng thành viên theo thứ tự ghế tham gia ổn định (`q = floor(T/n)`, `r = T % n`, `r` ghế đầu góp `q + 1`, các ghế sau góp `q`).

#### 2. Cơ chế Chi tiêu & Quyền `CanSpend` (v1.3 ĐÃ CHỐT)
- **Chủ nhóm (Owner):** Có quyền bật/tắt `CanSpend` (ON/OFF) cho từng thành viên. Mặc định thành viên mới là **OFF**.
- **Khi `CanSpend = ON`:** Thành viên được **trực tiếp thực hiện khoản chi**, **KHÔNG phải gửi yêu cầu chờ chủ nhóm phê duyệt từng khoản**.
- **Khi `CanSpend = OFF`:** Thành viên không có quyền chi trực tiếp từ quỹ.
- **Điều kiện chi:** Quỹ chỉ được chi khi hoạt động đã đạt **100% mục tiêu đóng góp**. Khoản chi không được vượt số dư khả dụng và không được xâm phạm phần tiền khóa dư của thành viên khác.

#### 3. Kiểm soát Khoản chi lớn (Large Expense 20% / Pending24h)
- Mọi khoản chi từ **20% số dư khả dụng trở lên** (hoặc tổng chi cộng dồn của nhóm trong 24h vượt 20%) sẽ tự động rơi vào trạng thái giữ chờ **`Pending24h`** trong 24 giờ. Áp dụng cho cả Chủ nhóm và Thành viên có `CanSpend = ON`.
- Hệ thống lập tức gửi thông báo cảnh báo đến toàn bộ thành viên trong nhóm.
- Số tiền của khoản chi lớn được giữ chỗ (`Hold`), không bị trừ khỏi quỹ nhưng không thể dùng cho việc chi khác.

#### 4. Báo cáo khoản chi & Khiếu nại Admin (Dispute Resolution)
- Trong thời gian giữ chờ 24h, bất kỳ thành viên nào trong nhóm cũng có quyền bấm nút **Báo cáo khoản chi** kèm lý do.
- Khi bị báo cáo, khoản chi lập tức chuyển sang trạng thái **`Suspended`**, đồng thời hệ thống tự động mở một `SupportCase` với `Category = "V1FundDispute"`.
- **Thẩm quyền xử lý:** Chỉ Admin hệ thống (`SystemAdmin`) mới có quyền phân xử:
  - Nếu Admin xác định báo cáo đúng (`IsApproved = true`): Hủy khoản chi (`Cancelled`), giải phóng tiền hold trả lại quỹ.
  - Nếu Admin xác định báo cáo sai (`IsApproved = false`): Khoản chi quay trở lại trạng thái `Pending24h` với đồng hồ đếm ngược giữ nguyên mốc thời gian gốc.

#### 5. Danh sách Endpoints liên quan
- `POST /api/v1/funds` — Tạo quỹ V1 mới (chọn Kind: Fixed, đặt tổng dự kiến `TargetAmountVnd`).
- `GET /api/v1/funds/{id}` — Lấy thông tin chi tiết quỹ, tiến độ góp, số dư, danh tính chủ quỹ.
- `GET /api/v1/funds/{id}/finance` — Lấy chi tiết tài chính: số dư quỹ, tổng mục tiêu, số đã góp, tiền khóa dư.
- `POST /api/v1/funds/{id}/obligations/confirm` — Chủ quỹ xác nhận/cập nhật tổng chi phí dự kiến `T` sau khi thay đổi thành viên.
- `POST /api/v1/funds/{id}/contributions` — Thành viên góp tiền vào quỹ từ ví cá nhân (yêu cầu mã PIN).
- `GET /api/v1/funds/{id}/contributions` — Lấy lịch sử các khoản đóng góp của quỹ.
- `PUT /api/v1/funds/{id}/members/{userId}/spending-permission` — Chủ quỹ bật/tắt quyền `CanSpend` (ON/OFF) cho thành viên.
- `POST /api/v1/funds/{id}/requests` — Thực hiện chi tiền từ quỹ (chi trực tiếp hoặc tạo request).
- `POST /api/v1/funds/requests/{id}/report` — Thành viên báo cáo khoản chi lớn trong giai đoạn Pending24h.
- `POST /api/v1/funds/requests/{id}/cancel` — Hủy bỏ khoản chi đang chờ hoặc đang bị tạm dừng.

---

### PHẦN 6: QUỸ NHÓM V1 AUTHORITATIVE — NHÓM TỰ DO (FREE FUND)

#### 1. Mục đích & Quy mô
Mô hình quỹ linh hoạt theo phong cách Quỹ Nhóm MoMo. Quy mô tối đa **6 thành viên** (1 chủ quỹ + 5 thành viên). Không áp đặt tổng dự kiến bắt buộc `T` hay nghĩa vụ góp cố định.

#### 2. Cơ chế Chi tiêu & Xét duyệt (Approval Workflow)
- **Không sử dụng công tắc `CanSpend`:** Thành viên không có quyền chi trực tiếp từ quỹ.
- Khi thành viên cần chi tiền hoặc rút tiền tích lũy của mình, thành viên tạo yêu cầu (`V1FundRequestInput`).
- **Chủ quỹ (Owner) xét duyệt:** Chủ quỹ nhận được thông báo và thực hiện phê duyệt (`Approved = true`) hoặc từ chối (`Approved = false`) có cung cấp mã PIN.
- Khoản chi chỉ được giải ngân nội bộ vào ví cá nhân sau khi Chủ quỹ duyệt thành công. Chủ quỹ có quyền chi trực tiếp theo vai trò quản trị.

#### 3. Rút tiền cá nhân & Episode thành viên
- Thành viên chỉ được yêu cầu rút tối đa bằng số tiền đóng góp ròng hợp lệ của chính mình trong kỳ thành viên hiện tại (`RequestedByMembershipId`).
- Khi thành viên rời nhóm và sau đó được mời vào lại, một kỳ thành viên mới (`Episode`) được khởi tạo với số tiền đóng góp lũy kế ban đầu bằng đúng `0 VND`.

#### 4. Danh sách Endpoints liên quan
- `POST /api/v1/funds/{id}/requests` — Thành viên gửi yêu cầu chi/rút tiền quỹ.
- `GET /api/v1/funds/{id}/requests` — Lấy danh sách các yêu cầu chi/rút tiền của quỹ.
- `POST /api/v1/funds/requests/{id}/review` — Chủ quỹ phê duyệt hoặc từ chối yêu cầu chi tiền (yêu cầu PIN).
- `GET /api/v1/funds/{id}/refund-preview` — Xem trước số tiền hoàn trả chính xác nếu rời nhóm hoặc giải tán quỹ.

---

### PHẦN 7: QUẢN TRỊ QUỸ, BẦU CỬ CHỦ QUỸ & CHUYỂN HOẠT ĐỘNG (GOVERNANCE & ELECTIONS)

#### 1. Mục đích & Nguyên tắc Quản trị
Mỗi quỹ tại mọi thời điểm có **đúng 1 Chủ quỹ duy nhất**. Người sáng lập (`FounderUserId`) được ghi nhận bất biến trong lịch sử quỹ.

#### 2. Chuyển quyền Chủ quỹ tự nguyện (Ownership Transfer)
- Chủ quỹ có thể gửi lời đề nghị chuyển quyền chủ quỹ cho bất kỳ thành viên hợp lệ nào trong quỹ (`POST /api/v1/funds/{id}/ownership-offers`).
- Thành viên được đề nghị chấp nhận (`POST /api/v1/funds/ownership-offers/{id}/accept`), hệ thống chuyển quyền nguyên tử: thành viên trở thành Chủ quỹ mới, Chủ cũ chuyển thành thành viên thường, bảo toàn nguyên vẹn quyền lợi tiền bạc.

#### 3. Bầu cử Chủ quỹ do không hoạt động (Inactivity Election)
- **Điều kiện kích hoạt:** Chủ quỹ không có bất kỳ tương tác xác thực nào trên ứng dụng SpendWise trong **15 ngày liên tiếp** (được ghi nhận qua `POST /api/v1/activity`).
- **Tiến trình bầu cử 2 vòng:**
  - Vòng 1 (Đề cử & Bỏ phiếu ứng viên): Các thành viên hợp lệ bỏ phiếu bầu ứng viên mới. Nếu hòa phiếu cao nhất, tổ chức 1 lần bầu phụ duy nhất; nếu tiếp tục hòa thì cuộc bầu cử thất bại.
  - Vòng 2 (Xác nhận ứng viên): Cần **trên 50% tổng số cử tri hợp lệ** chọn Đồng ý.
- **Người thắng từ chối (`WinnerRejected`):** Người thắng cử có quyền từ chối tiếp nhận quyền chủ quỹ (`POST /api/v1/funds/elections/{id}/reject`). Bầu cử kết thúc thất bại, chủ cũ tạm thời giữ vai trò danh nghĩa, các thành viên được tự do rời nhóm kèm hoàn tiền.
- Trong suốt thời gian bầu cử, hệ thống tạm dừng toàn bộ giao dịch tài chính và lời mời mới (`election_finance_paused`).

#### 4. Kết thúc hoạt động cũ & Biểu quyết Hoạt động mới (Activity Proposals & Settlement)
- Sau khi hoạt động nhóm Fixed hoàn thành, Chủ quỹ đóng hoạt động (`POST /api/v1/funds/{id}/activity/close`).
- Chủ quỹ tạo đề xuất hoạt động mới (`POST /api/v1/funds/{id}/activity-proposals`). Biểu quyết diễn ra trong **24 giờ**.
- Nếu thông qua (>50% cử tri đồng ý): Những người đồng ý được chuyển số dư bảo lưu sang hoạt động mới; những người không đồng ý hoặc không phản hồi được hoàn tiền đầy đủ và tự động rời nhóm sau khi hoàn tất.
- Quyết toán đề xuất: `POST /api/v1/funds/activity-proposals/{id}/settle` (yêu cầu PIN chủ quỹ).

#### 5. Rời nhóm, Kick thành viên & Giải thể quỹ (Leave, Kick, Dissolve)
- `POST /api/v1/funds/{id}/leave` — Thành viên tự rời nhóm (nhận hoàn tiền về ví cá nhân theo công thức bảo toàn).
- `POST /api/v1/funds/{id}/members/{userId}/kick` — Chủ quỹ kick thành viên (bắt buộc hoàn trả đúng số tiền quyền lợi hợp lệ của thành viên về ví cá nhân của họ).
- `POST /api/v1/funds/{id}/dissolve` — Giải thể quỹ. Chỉ cho phép giải thể khi số dư quỹ về **đúng 0 VND** sau khi đã thanh toán hoàn trả toàn bộ quyền lợi cho các thành viên.

#### 6. Danh sách Endpoints liên quan
- `GET /api/v1/funds/{id}/governance` — Lấy thông tin quản trị, danh tính chủ quỹ, trạng thái bầu cử.
- `POST /api/v1/funds/{id}/elections` — Kích hoạt mở cuộc bầu cử thay thế chủ quỹ.
- `GET /api/v1/funds/elections/{id}` — Lấy chi tiết tiến trình bầu cử.
- `POST /api/v1/funds/elections/{id}/candidates` — Đăng ký ứng cử viên.
- `POST /api/v1/funds/elections/{id}/ballots` — Bỏ phiếu bầu cử.
- `POST /api/v1/funds/elections/{id}/advance` — Chuyển pha bầu cử theo thời hạn.
- `POST /api/v1/funds/elections/{id}/accept` — Người trúng cử chấp nhận tiếp nhận quyền chủ quỹ.
- `POST /api/v1/funds/elections/{id}/reject` — Người trúng cử từ chối tiếp nhận quyền.
- `POST /api/v1/funds/{id}/ownership-offers` — Tạo lời đề nghị chuyển giao quyền chủ quỹ.
- `POST /api/v1/funds/ownership-offers/{id}/accept` — Chấp nhận lời đề nghị làm chủ quỹ.
- `GET /api/v1/funds/ownership-offers` — Lấy danh sách các lời đề nghị chuyển quyền chủ quỹ gửi đến người dùng.
- `POST /api/v1/activity` — Ghi nhận tương tác tiền cảnh của người dùng để cập nhật mốc 15 ngày.

---

### PHẦN 8: KẾ HOẠCH TÀI CHÍNH (NGÂN SÁCH, MỤC TIÊU, ĐỊNH KỲ, QUY TẮC)

#### 1. Mục đích & Nghiệp vụ
Hỗ trợ người dùng lập kế hoạch tài chính cá nhân toàn diện:
- **Ngân sách chi tiêu (Budgets):** Đặt giới hạn chi tiêu theo tháng và theo danh mục (Category). Cảnh báo tiến độ sử dụng ngân sách.
- **Mục tiêu tích lũy (Goals):** Đặt mục tiêu số tiền cần tiết kiệm đến một thời hạn; nạp tiền vào mục tiêu và rút tiền về ví cá nhân khi cần.
- **Giao dịch định kỳ (Recurring Transactions):** Thiết lập lịch thu/chi định kỳ (hàng ngày, hàng tuần, hàng tháng); kích hoạt thực thi tự động.
- **Quy tắc phân loại tự động (Transaction Rules):** Tự động gán danh mục cho giao dịch dựa trên từ khóa mô tả.

#### 2. Danh sách Endpoints
- `GET /api/budgets` — Lấy danh sách ngân sách tài chính.
- `GET /api/budgets/{budgetId}` — Lấy chi tiết một ngân sách.
- `POST /api/budgets` — Tạo ngân sách chi tiêu mới.
- `PUT /api/budgets/{budgetId}` — Cập nhật hạn mức ngân sách.
- `DELETE /api/budgets/{budgetId}` — Xóa ngân sách.
- `GET /api/budgets/summary` — Lấy báo cáo tổng quan tiến độ ngân sách trong tháng.
- `GET /api/goals` — Lấy danh sách các mục tiêu tiết kiệm.
- `GET /api/goals/{goalId}` — Lấy chi tiết một mục tiêu.
- `POST /api/goals` — Tạo mục tiêu tích lũy mới.
- `PUT /api/goals/{goalId}` — Cập nhật mục tiêu.
- `DELETE /api/goals/{goalId}` — Xóa mục tiêu.
- `POST /api/goals/{goalId}/deposit` — Nạp tiền tích lũy vào mục tiêu từ ví cá nhân.
- `POST /api/goals/{goalId}/withdraw` — Rút tiền tích lũy từ mục tiêu về ví cá nhân.
- `GET /api/recurring-transactions` — Lấy danh sách các giao dịch định kỳ đã lập lịch.
- `GET /api/recurring-transactions/{id}` — Lấy chi tiết một giao dịch định kỳ.
- `POST /api/recurring-transactions` — Tạo giao dịch định kỳ mới.
- `PUT /api/recurring-transactions/{id}` — Cập nhật thông số lịch định kỳ.
- `DELETE /api/recurring-transactions/{id}` — Hủy giao dịch định kỳ.
- `POST /api/recurring-transactions/{id}/pause` — Tạm dừng lịch định kỳ.
- `POST /api/recurring-transactions/{id}/resume` — Kích hoạt lại lịch định kỳ.
- `POST /api/recurring-transactions/process-due` — Kích hoạt xử lý các giao dịch định kỳ đã đến hạn.
- `GET /api/transaction-rules` — Lấy danh sách các quy tắc phân loại tự động.
- `GET /api/transaction-rules/{id}` — Lấy chi tiết một quy tắc.
- `POST /api/transaction-rules` — Tạo quy tắc phân loại mới.
- `PUT /api/transaction-rules/{id}` — Cập nhật quy tắc.
- `DELETE /api/transaction-rules/{id}` — Xóa quy tắc.

---

### PHẦN 9: TRỢ LÝ TÀI CHÍNH THÔNG MINH (SPENDWISE AI)

#### 1. Mục đích & Kiến trúc
Cung cấp trợ lý AI tương tác tự nhiên với người dùng về tình hình tài chính cá nhân, phân tích thu chi, giải đáp thắc mắc và đề xuất các hành động tài chính (AI Action Proposals: gợi ý lập ngân sách, tạo mục tiêu, cảnh báo chi tiêu).

#### 2. Ranh giới Tích hợp & Kiểm soát Nhà cung cấp (Provider Boundary)
- Sử dụng mô hình chính thức: **`gemini-3.5-flash-lite`** thuộc Google Gemini Free Tier. Tuyệt đối không sử dụng OpenAI SDK cũ.
- Khi không có khóa API (`Gemini:ApiKey = ""`), hệ thống trả về mã lỗi có kiểm soát **HTTP 503 `ai_service_unavailable`**, tuyệt đối không làm crash ứng dụng.
- **Thực thi hành động có xác nhận người dùng:** AI chỉ đề xuất hành động (`AI Action`); hành động chỉ được thực hiện khi người dùng chủ động bấm xác nhận (`POST /api/ai/actions/{id}/execute`) kèm mã PIN nếu có biến động tài chính.

#### 3. Kiểm soát Tải & Rate Limiting
- Rate limit tần suất gọi: Policy `ai` (**15 requests/phút**).
- Giới hạn luồng đồng thời: `user-chat:{userId}` (tối đa **2 cuộc gọi đồng thời** cho mỗi người dùng).

#### 4. Danh sách Endpoints
- `GET /api/ai/conversations` — Lấy danh sách các cuộc hội thoại AI của người dùng.
- `GET /api/ai/conversations/{id}` — Lấy nội dung chi tiết các lượt chat trong một cuộc hội thoại.
- `POST /api/ai/conversations` — Mở một phiên hội thoại AI mới.
- `PUT /api/ai/conversations/{id}` — Đổi tên chủ đề cuộc hội thoại.
- `DELETE /api/ai/conversations/{id}` — Xóa cuộc hội thoại AI.
- `POST /api/ai/chat` — Gửi tin nhắn chat đến AI và nhận câu trả lời phân tích tài chính.
- `POST /api/ai/attachments` — Tải lên tệp đính kèm (hóa đơn, sao kê) để AI phân tích.
- `GET /api/ai/attachments/{id}` — Xem tệp đính kèm AI.
- `GET /api/ai/actions/{id}` — Xem chi tiết đề xuất hành động tài chính do AI tạo ra.
- `POST /api/ai/actions/{id}/execute` — Người dùng phê duyệt thực thi đề xuất hành động tài chính.
- `POST /api/ai/actions/{id}/cancel` — Người dùng hủy bỏ đề xuất hành động của AI.

---

### PHẦN 10: HỖ TRỢ, KHIẾU NẠI & THÔNG BÁO HỆ THỐNG

#### 1. Mục đích & Nghiệp vụ
- **Hỗ trợ người dùng (Support Cases):** Người dùng gửi yêu cầu hỗ trợ khi gặp sự cố, trao đổi tin nhắn hai chiều với Admin, đính kèm tệp chứng từ và theo dõi tiến độ xử lý ticket.
- **Khiếu nại giao dịch (Disputes):** Khiếu nại giao dịch sai sót hoặc khiếu nại khoản chi bất thường trong nhóm, theo dõi phân xử của Admin.
- **Thông báo (Notifications):** Nhận thông báo đa kênh (in-app, push Expo, email SMTP). Đánh dấu đã đọc, tùy chỉnh loại thông báo muốn nhận.
- **Thông báo bảo trì hệ thống (Maintenance Announcements):** Xem thông báo bảo trì chủ động từ Admin hệ thống.

#### 2. Danh sách Endpoints
- `POST /api/cases/support` — Mở phiếu yêu cầu hỗ trợ kỹ thuật mới.
- `GET /api/cases/support` — Lấy danh sách các phiếu hỗ trợ của người dùng hiện tại.
- `GET /api/cases/support/{id}` — Lấy chi tiết một phiếu hỗ trợ và lịch sử tin nhắn.
- `POST /api/cases/support/{id}/messages` — Gửi tin nhắn phản hồi trong phiếu hỗ trợ.
- `POST /api/cases/support/{id}/messages/multipart` — Gửi tin nhắn hỗ trợ kèm tệp đính kèm bằng multipart/form-data.
- `POST /api/cases/disputes` — Tạo phiếu khiếu nại giao dịch hoặc quỹ nhóm.
- `GET /api/cases/disputes` — Lấy danh sách các phiếu khiếu nại của người dùng hiện tại.
- `GET /api/cases/disputes/{id}` — Lấy chi tiết phiếu khiếu nại và tiến trình giải quyết.
- `POST /api/cases/disputes/{id}/messages` — Gửi phản hồi trong phiếu khiếu nại.
- `POST /api/cases/disputes/{id}/messages/multipart` — Gửi phản hồi khiếu nại kèm chứng từ.
- `GET /api/cases/attachments/{id}` — Tải tệp đính kèm trong phiếu hỗ trợ/khiếu nại.
- `GET /api/notifications` — Lấy danh sách thông báo của người dùng (kèm phân trang).
- `POST /api/notifications/{id}/read` — Đánh dấu 1 thông báo là đã đọc.
- `POST /api/notifications/read-all` — Đánh dấu tất cả thông báo là đã đọc.
- `GET /api/notifications/search?keyword=...` — Tìm kiếm thông báo theo từ khóa.
- `DELETE /api/notifications/{id}` — Xóa 1 thông báo.
- `DELETE /api/notifications` — Xóa tất cả thông báo đã đọc.
- `GET /api/notifications/preferences` — Lấy cấu hình tùy chọn nhận thông báo.
- `PUT /api/notifications/preferences` — Cập nhật cấu hình nhận thông báo (email, push, in-app).
- `GET /api/system/maintenance` — Kiểm tra trạng thái bảo trì hệ thống hiện tại.
- `GET /api/system/announcements/active` — Lấy các thông báo vận hành đang phát sóng từ Admin.

---

### PHẦN 11: TÍCH HỢP NGÂN HÀNG & SEPAY (TEST MODE & WEBHOOK GUARD)

#### 1. Mục đích & Ranh giới hiện hành
- Hỗ trợ liên kết tài khoản ngân hàng của người dùng.
- Cung cấp luồng nạp tiền vào ví cá nhân trong môi trường Test Mode (`test-mode/wallet-top-ups`) phục vụ kiểm thử và nghiệm thu nội bộ.
- Xử lý Webhook và IPN từ cổng SePay: Xác thực chữ ký webhook, kiểm tra chống lặp giao dịch (Idempotency), chỉ ghi nhận cộng tiền ví khi giao dịch thành công 100% với đúng mã tham chiếu và số tiền chính xác (All-Or-Nothing policy).

#### 2. Danh sách Endpoints
- `POST /api/bank-connections/link` — Bắt đầu luồng liên kết tài khoản ngân hàng.
- `GET /api/bank-connections` — Lấy danh sách các tài khoản ngân hàng đã liên kết của người dùng.
- `GET /api/bank-connections/{id}` — Lấy chi tiết một liên kết ngân hàng.
- `POST /api/bank-connections/{id}/unlink` — Hủy liên kết tài khoản ngân hàng.
- `POST /api/bank-connections/{id}/reactivate` — Kích hoạt lại liên kết ngân hàng.
- `POST /api/bank-connections/{id}/sync` — Đồng bộ dữ liệu từ ngân hàng.
- `POST /api/bank-connections/test-mode/link-challenges` — Tạo yêu cầu liên kết thử nghiệm trong Test Mode.
- `POST /api/bank-connections/test-mode/link-challenges/{challengeId}/confirm` — Xác nhận liên kết thử nghiệm.
- `POST /api/bank-connections/test-mode/wallet-top-ups` — Tạo yêu cầu nạp tiền vào ví từ tài khoản ngân hàng Test Mode.
- `POST /api/bank-connections/test-mode/wallet-top-ups/{topUpId}/confirm` — Xác nhận hoàn tất nạp tiền vào ví (cộng số dư ví và ghi sổ cái).
- `GET /api/bank-connections/test-mode/linked-accounts` — Xem danh sách tài khoản liên kết Test Mode.
- `GET /api/bank-connections/test-mode/wallet-top-ups` — Xem lịch sử nạp tiền Test Mode.
- `GET /api/bank-transactions` — Lấy danh sách giao dịch ngân hàng đã ghi nhận.
- `GET /api/bank-transactions/{id}` — Lấy chi tiết một giao dịch ngân hàng.
- `PATCH /api/bank-transactions/{id}/category` — Gán nhãn phân loại danh mục cho giao dịch ngân hàng.
- `POST /api/bank-integrations/sepay/events` — Webhook nhận sự kiện giao dịch từ SePay.
- `POST /api/bank-integrations/sepay/ipn` — Instant Payment Notification từ SePay với bảo mật chữ ký điện tử.

---

### PHẦN 12: NHÓM CŨ (LEGACY GROUPS — TASK 14 / BACKWARDS COMPATIBILITY)

#### 1. Mục đích & Chính sách duy trì
Controller `/api/groups` (29 endpoints trong `GroupsController.cs`) là hệ thống quỹ nhóm đời đầu (Task 14 và trước đó). Được duy trì hoàn toàn nguyên vẹn trong Backend nhằm bảo đảm tính tương thích ngược cho dữ liệu lịch sử và các client cũ.
Hệ thống quỹ authoritative hiện hành là **Quỹ V1 (`/api/v1/funds`)**. Tuyệt đối không xóa bỏ `GroupsController` để bảo vệ tính toàn vẹn của mã nguồn.

#### 2. Danh sách Endpoints
- `GET /api/groups` — Danh sách nhóm cũ.
- `POST /api/groups` — Tạo nhóm cũ.
- `GET /api/groups/{groupId}` — Chi tiết nhóm cũ.
- `PUT /api/groups/{groupId}` — Sửa thông tin nhóm cũ.
- `POST /api/groups/{groupId}/invites` — Mời thành viên vào nhóm cũ.
- `GET /api/groups/invites` — Danh sách lời mời nhóm cũ.
- `POST /api/groups/invites/{inviteId}/accept` — Chấp nhận lời mời nhóm cũ.
- `POST /api/groups/invites/{inviteId}/reject` — Từ chối lời mời nhóm cũ.
- `DELETE /api/groups/{groupId}/members/{memberUserId}` — Xóa thành viên nhóm cũ.
- `POST /api/groups/{groupId}/ownership/{memberUserId}` — Chuyển chủ nhóm cũ.
- `PUT /api/groups/{groupId}/receiving-bank` — Đổi tài khoản nhận tiền nhóm cũ.
- `GET /api/groups/{groupId}/fund` — Chi tiết quỹ nhóm cũ.
- `GET /api/groups/{groupId}/fund/transactions` — Lịch sử giao dịch quỹ cũ.
- `POST /api/groups/{groupId}/fund/contributions` — Đóng góp quỹ cũ.
- `GET /api/groups/{groupId}/fund-requests` — Danh sách yêu cầu chi quỹ cũ.
- `POST /api/groups/{groupId}/fund-requests` — Tạo yêu cầu chi quỹ cũ.
- `POST /api/groups/{groupId}/fund-requests/{requestId}/pay` — Thanh toán yêu cầu chi quỹ cũ.
- `GET /api/groups/{groupId}/fund-rounds` — Danh sách vòng góp quỹ cũ.
- `POST /api/groups/{groupId}/fund-rounds` — Tạo vòng góp quỹ cũ.
- `POST /api/groups/{groupId}/fund-rounds/{roundId}/pay` — Thanh toán vòng góp quỹ cũ.
- `GET /api/groups/{groupId}/expenses` — Danh sách khoản chi quỹ cũ.
- `POST /api/groups/{groupId}/expenses` — Tạo khoản chi quỹ cũ.
- `GET /api/groups/{groupId}/activities` — Danh sách hoạt động nhóm cũ.
- `GET /api/groups/{groupId}/deletion-request` — Xem yêu cầu giải tán nhóm cũ.
- `POST /api/groups/{groupId}/deletion-requests` — Tạo yêu cầu giải tán nhóm cũ.
- `POST /api/groups/{groupId}/deletion-requests/{requestId}/vote` — Bỏ phiếu giải tán nhóm cũ.
- `POST /api/groups/{groupId}/deletion-requests/{requestId}/cancel` — Hủy yêu cầu giải tán nhóm cũ.
- `POST /api/groups/{groupId}/deletion-requests/{requestId}/refunds/{refundId}/payment` — Thanh toán hoàn tiền giải tán nhóm cũ.
- `POST /api/groups/{groupId}/deletion-requests/{requestId}/finalize` — Hoàn tất giải tán nhóm cũ.

---

## MA TRẬN PHÂN LOẠI & TRẠNG THÁI KIỂM TOÁN TỪNG ENDPOINT

| Endpoint Route | HTTP Method | Nhóm chức năng | Tình trạng Implementation | Tình trạng Kiểm thử |
|---|:---:|---|:---:|:---:|
| `/api/auth/register` | POST | Auth | IMPLEMENTED | PASS (Newman & C#) |
| `/api/auth/email-verification/verify` | POST | Auth | IMPLEMENTED | PASS (Newman & C#) |
| `/api/auth/email-verification/resend` | POST | Auth | IMPLEMENTED | PASS (Newman & C#) |
| `/api/auth/login` | POST | Auth | IMPLEMENTED | PASS (Newman & C#) |
| `/api/auth/refresh` | POST | Auth | IMPLEMENTED | PASS (Newman & C#) |
| `/api/auth/logout` | POST | Auth | IMPLEMENTED | PASS (Newman & C#) |
| `/api/auth/sessions` | GET | Auth | IMPLEMENTED | PASS (Newman & C#) |
| `/api/auth/sessions/{sessionId}` | DELETE | Auth | IMPLEMENTED | PASS (Newman & C#) |
| `/api/auth/change-password` | POST | Auth | IMPLEMENTED | PASS (Newman & C#) |
| `/api/auth/forgot-password` | POST | Auth | IMPLEMENTED | PASS (Newman & C#) |
| `/api/auth/reset-password` | POST | Auth | IMPLEMENTED | PASS (Newman & C#) |
| `/api/auth/pin/setup` | POST | Auth | IMPLEMENTED | PASS (Newman & C#) |
| `/api/auth/pin/verify` | POST | Auth | IMPLEMENTED | PASS (Newman & C#) |
| `/api/auth/pin/forgot` | POST | Auth | IMPLEMENTED | PASS (Newman & C#) |
| `/api/auth/pin/reset/validate` | POST | Auth | IMPLEMENTED | PASS (Newman & C#) |
| `/api/auth/pin/reset` | POST | Auth | IMPLEMENTED | PASS (Newman & C#) |
| `/api/users/me` | GET | Profile | IMPLEMENTED | PASS (Newman & C#) |
| `/api/users/me/preferences` | PUT | Profile | IMPLEMENTED | PASS (Newman & C# SQL02) |
| `/api/users/me/avatar` | POST | Profile | IMPLEMENTED | PASS (C# SQL02 E2E) |
| `/api/users/me/email/request` | POST | Profile | IMPLEMENTED | PASS (C# SQL02 E2E) |
| `/api/users/me/email/confirm` | POST | Profile | IMPLEMENTED | PASS (C# SQL02 E2E) |
| `/api/users/me/sessions` | DELETE | Profile | IMPLEMENTED | PASS (C#) |
| `/api/pin/status` | GET | PIN | IMPLEMENTED | PASS (C#) |
| `/api/pin/verify` | POST | PIN | IMPLEMENTED | PASS (C#) |
| `/api/pin` | PUT | PIN | IMPLEMENTED | PASS (C#) |
| `/api/account-recovery` | POST | Recovery | IMPLEMENTED | PASS (C#) |
| `/api/mobile/push/{installationId}` | PUT | Push | IMPLEMENTED | PASS (C#) |
| `/api/mobile/push/{installationId}` | DELETE | Push | IMPLEMENTED | PASS (C#) |
| `/api/friends` | GET | Friends | IMPLEMENTED | PASS (Newman & C#) |
| `/api/friends/search` | GET | Friends | IMPLEMENTED | PASS (Newman & C#) |
| `/api/friends/discover` | GET | Friends | IMPLEMENTED | PASS (C# & RateLimiter) |
| `/api/friends/requests/incoming` | GET | Friends | IMPLEMENTED | PASS (Newman & C#) |
| `/api/friends/requests/outgoing` | GET | Friends | IMPLEMENTED | PASS (Newman & C#) |
| `/api/friends/requests` | POST | Friends | IMPLEMENTED | PASS (Newman & C#) |
| `/api/friends/requests/{requestId}/accept` | POST | Friends | IMPLEMENTED | PASS (Newman & C#) |
| `/api/friends/requests/{requestId}/reject` | POST | Friends | IMPLEMENTED | PASS (Newman & C#) |
| `/api/friends/requests/{requestId}` | DELETE | Friends | IMPLEMENTED | PASS (Newman & C#) |
| `/api/friends/{friendUserId}` | DELETE | Friends | IMPLEMENTED | PASS (Newman & C#) |
| `/api/friends/{friendUserId}/preference` | PATCH | Friends | IMPLEMENTED | PASS (Newman & C#) |
| `/api/wallets` | GET | Wallets | IMPLEMENTED | PASS (Newman & C#) |
| `/api/wallets/{walletId}` | GET | Wallets | IMPLEMENTED | PASS (Newman & C#) |
| `/api/wallets` | POST | Wallets | IMPLEMENTED | PASS (Newman & C#) |
| `/api/wallets/{walletId}` | PUT | Wallets | IMPLEMENTED | PASS (Newman & C#) |
| `/api/wallets/{walletId}` | DELETE | Wallets | IMPLEMENTED | PASS (Newman & C#) |
| `/api/transactions` | GET | Transactions | IMPLEMENTED | PASS (Newman & C#) |
| `/api/transactions/{transactionId}` | GET | Transactions | IMPLEMENTED | PASS (Newman & C#) |
| `/api/transactions/income` | POST | Transactions | IMPLEMENTED | PASS (Newman & C#) |
| `/api/transactions/expense` | POST | Transactions | IMPLEMENTED | PASS (Newman & C#) |
| `/api/transactions/expense/multi-wallet` | POST | Transactions | IMPLEMENTED | PASS (C#) |
| `/api/transactions/{transactionId}` | PATCH | Transactions | IMPLEMENTED | PASS (C#) |
| `/api/evidence` | POST | Evidence | IMPLEMENTED | PASS (Newman & C#) |
| `/api/evidence/{id}` | GET | Evidence | IMPLEMENTED | PASS (Newman & C#) |
| `/api/transfers/internal` | POST | Transfers | IMPLEMENTED | PASS (Newman & C#) |
| `/api/transfers/p2p` | POST | Transfers | IMPLEMENTED | PASS (Newman & C#) |
| `/api/transfers/recipients` | GET | Transfers | IMPLEMENTED | PASS (Newman & C#) |
| `/api/transfers/{transferId}` | GET | Transfers | IMPLEMENTED | PASS (Newman & C#) |
| `/api/transfers` | POST | Transfers | IMPLEMENTED | PASS (Newman & C#) |
| `/api/transfers` | GET | Transfers | IMPLEMENTED | PASS (Newman & C#) |
| `/api/transfers/{paymentId}/confirm` | POST | Transfers | IMPLEMENTED | PASS (Newman & C#) |
| `/api/v1/funds` | POST | V1 Funds | IMPLEMENTED | PASS (Newman & C#) |
| `/api/v1/funds` | GET | V1 Funds | IMPLEMENTED | PASS (Newman & C#) |
| `/api/v1/funds/{id}` | GET | V1 Funds | IMPLEMENTED | PASS (Newman & C#) |
| `/api/v1/funds/{id}/members` | GET | V1 Funds | IMPLEMENTED | PASS (Newman & C#) |
| `/api/v1/funds/{id}/governance` | GET | V1 Funds | IMPLEMENTED | PASS (Newman & C#) |
| `/api/v1/funds/{id}/owner-response` | GET | V1 Funds | IMPLEMENTED | PASS (Newman & C#) |
| `/api/v1/funds/{id}/owner-response/escalate` | POST | V1 Funds | IMPLEMENTED | PASS (Newman & C#) |
| `/api/v1/funds/{id}/activity/close` | POST | V1 Funds | IMPLEMENTED | PASS (Newman & C#) |
| `/api/v1/funds/{id}/activity-proposals` | POST | V1 Funds | IMPLEMENTED | PASS (Newman & C#) |
| `/api/v1/funds/activity-proposals/{id}` | GET | V1 Funds | IMPLEMENTED | PASS (Newman & C#) |
| `/api/v1/funds/activity-proposals/{id}/settle` | POST | V1 Funds | IMPLEMENTED | PASS (Newman & C#) |
| `/api/v1/funds/activity-proposals/{id}/vote` | POST | V1 Funds | IMPLEMENTED | PASS (Newman & C#) |
| `/api/v1/funds/{id}/elections` | POST | V1 Funds | IMPLEMENTED | PASS (C# SQL02) |
| `/api/v1/funds/elections/{id}` | GET | V1 Funds | IMPLEMENTED | PASS (C# SQL02) |
| `/api/v1/funds/elections/{id}/candidates` | POST | V1 Funds | IMPLEMENTED | PASS (C# SQL02) |
| `/api/v1/funds/elections/{id}/ballots` | POST | V1 Funds | IMPLEMENTED | PASS (C# SQL02) |
| `/api/v1/funds/elections/{id}/advance` | POST | V1 Funds | IMPLEMENTED | PASS (C# SQL02) |
| `/api/v1/funds/elections/{id}/accept` | POST | V1 Funds | IMPLEMENTED | PASS (C# SQL02) |
| `/api/v1/funds/elections/{id}/reject` | POST | V1 Funds | IMPLEMENTED | PASS (C# SQL02) |
| `/api/v1/funds/{id}/contribution-allocation` | GET | V1 Funds | IMPLEMENTED | PASS (Newman & C#) |
| `/api/v1/funds/{id}/finance` | GET | V1 Funds | IMPLEMENTED | PASS (Newman & C#) |
| `/api/v1/funds/{id}/obligations/confirm` | POST | V1 Funds | IMPLEMENTED | PASS (Newman & C#) |
| `/api/v1/funds/{id}/contributions` | POST | V1 Funds | IMPLEMENTED | PASS (Newman & C#) |
| `/api/v1/funds/{id}/contributions` | GET | V1 Funds | IMPLEMENTED | PASS (Newman & C#) |
| `/api/v1/funds/{id}/activities` | GET | V1 Funds | IMPLEMENTED | PASS (Newman & C#) |
| `/api/v1/funds/{id}/invitations` | POST | V1 Funds | IMPLEMENTED | PASS (Newman & C#) |
| `/api/v1/funds/invitations` | GET | V1 Funds | IMPLEMENTED | PASS (Newman & C#) |
| `/api/v1/funds/{id}/invitation-proposals` | GET | V1 Funds | IMPLEMENTED | PASS (Newman & C#) |
| `/api/v1/funds/invitations/{id}/review` | POST | V1 Funds | IMPLEMENTED | PASS (Newman & C#) |
| `/api/v1/funds/invitations/{id}/decline` | POST | V1 Funds | IMPLEMENTED | PASS (Newman & C#) |
| `/api/v1/funds/invitations/{id}` | DELETE | V1 Funds | IMPLEMENTED | PASS (Newman & C#) |
| `/api/v1/funds/invitations/{id}/accept` | POST | V1 Funds | IMPLEMENTED | PASS (Newman & C#) |
| `/api/v1/funds/{id}/members/{userId}/spending-permission` | PUT | V1 Funds | IMPLEMENTED | PASS (Newman & C#) |
| `/api/v1/funds/{id}/ownership-offers` | POST | V1 Funds | IMPLEMENTED | PASS (Newman & C#) |
| `/api/v1/funds/ownership-offers/{id}/accept` | POST | V1 Funds | IMPLEMENTED | PASS (Newman & C#) |
| `/api/v1/funds/ownership-offers` | GET | V1 Funds | IMPLEMENTED | PASS (Newman & C#) |
| `/api/v1/funds/{id}/refund-preview` | GET | V1 Funds | IMPLEMENTED | PASS (Newman & C#) |
| `/api/v1/funds/{id}/leave` | POST | V1 Funds | IMPLEMENTED | PASS (Newman & C#) |
| `/api/v1/funds/{id}/members/{userId}/kick` | POST | V1 Funds | IMPLEMENTED | PASS (Newman & C#) |
| `/api/v1/funds/{id}/dissolve` | POST | V1 Funds | IMPLEMENTED | PASS (Newman & C#) |
| `/api/v1/funds/{id}/requests` | POST | V1 Funds | IMPLEMENTED | PASS (Newman & C#) |
| `/api/v1/funds/{id}/requests` | GET | V1 Funds | IMPLEMENTED | PASS (Newman & C#) |
| `/api/v1/funds/requests/{id}/review` | POST | V1 Funds | IMPLEMENTED | PASS (Newman & C#) |
| `/api/v1/funds/requests/{id}/report` | POST | V1 Funds | IMPLEMENTED | PASS (Newman & C#) |
| `/api/v1/funds/requests/{id}/cancel` | POST | V1 Funds | IMPLEMENTED | PASS (Newman & C#) |
| `/api/v1/activity` | POST | Activity | IMPLEMENTED | PASS (C#) |
| `/api/budgets` | GET | Budgets | IMPLEMENTED | PASS (Newman & C#) |
| `/api/budgets/{budgetId}` | GET | Budgets | IMPLEMENTED | PASS (Newman & C#) |
| `/api/budgets` | POST | Budgets | IMPLEMENTED | PASS (Newman & C#) |
| `/api/budgets/{budgetId}` | PUT | Budgets | IMPLEMENTED | PASS (Newman & C#) |
| `/api/budgets/{budgetId}` | DELETE | Budgets | IMPLEMENTED | PASS (Newman & C#) |
| `/api/budgets/summary` | GET | Budgets | IMPLEMENTED | PASS (Newman & C#) |
| `/api/goals` | GET | Goals | IMPLEMENTED | PASS (Newman & C#) |
| `/api/goals/{goalId}` | GET | Goals | IMPLEMENTED | PASS (Newman & C#) |
| `/api/goals` | POST | Goals | IMPLEMENTED | PASS (Newman & C#) |
| `/api/goals/{goalId}` | PUT | Goals | IMPLEMENTED | PASS (Newman & C#) |
| `/api/goals/{goalId}` | DELETE | Goals | IMPLEMENTED | PASS (Newman & C#) |
| `/api/goals/{goalId}/deposit` | POST | Goals | IMPLEMENTED | PASS (Newman & C#) |
| `/api/goals/{goalId}/withdraw` | POST | Goals | IMPLEMENTED | PASS (Newman & C#) |
| `/api/recurring-transactions` | GET | Recurring | IMPLEMENTED | PASS (C#) |
| `/api/recurring-transactions/{id}` | GET | Recurring | IMPLEMENTED | PASS (C#) |
| `/api/recurring-transactions` | POST | Recurring | IMPLEMENTED | PASS (C#) |
| `/api/recurring-transactions/{id}` | PUT | Recurring | IMPLEMENTED | PASS (C#) |
| `/api/recurring-transactions/{id}` | DELETE | Recurring | IMPLEMENTED | PASS (C#) |
| `/api/recurring-transactions/{id}/pause` | POST | Recurring | IMPLEMENTED | PASS (C#) |
| `/api/recurring-transactions/{id}/resume` | POST | Recurring | IMPLEMENTED | PASS (C#) |
| `/api/recurring-transactions/process-due` | POST | Recurring | IMPLEMENTED | PASS (C#) |
| `/api/transaction-rules` | GET | Rules | IMPLEMENTED | PASS (C#) |
| `/api/transaction-rules/{id}` | GET | Rules | IMPLEMENTED | PASS (C#) |
| `/api/transaction-rules` | POST | Rules | IMPLEMENTED | PASS (C#) |
| `/api/transaction-rules/{id}` | PUT | Rules | IMPLEMENTED | PASS (C#) |
| `/api/transaction-rules/{id}` | DELETE | Rules | IMPLEMENTED | PASS (C#) |
| `/api/categories` | GET | Categories | IMPLEMENTED | PASS (Newman & C#) |
| `/api/categories/{categoryId}` | GET | Categories | IMPLEMENTED | PASS (Newman & C#) |
| `/api/categories` | POST | Categories | IMPLEMENTED | PASS (Newman & C#) |
| `/api/categories/{categoryId}` | PUT | Categories | IMPLEMENTED | PASS (Newman & C#) |
| `/api/categories/{categoryId}` | DELETE | Categories | IMPLEMENTED | PASS (Newman & C#) |
| `/api/ai/conversations` | GET | AI | IMPLEMENTED | PASS (C#) |
| `/api/ai/conversations/{id}` | GET | AI | IMPLEMENTED | PASS (C#) |
| `/api/ai/conversations` | POST | AI | IMPLEMENTED | PASS (C#) |
| `/api/ai/conversations/{id}` | PUT | AI | IMPLEMENTED | PASS (C#) |
| `/api/ai/conversations/{id}` | DELETE | AI | IMPLEMENTED | PASS (C#) |
| `/api/ai/chat` | POST | AI | IMPLEMENTED | PASS (C# Concurrency & Smoke) |
| `/api/ai/attachments` | POST | AI | IMPLEMENTED | PASS (C#) |
| `/api/ai/attachments/{id}` | GET | AI | IMPLEMENTED | PASS (C#) |
| `/api/ai/actions/{id}` | GET | AI | IMPLEMENTED | PASS (C#) |
| `/api/ai/actions/{id}/execute` | POST | AI | IMPLEMENTED | PASS (C#) |
| `/api/ai/actions/{id}/cancel` | POST | AI | IMPLEMENTED | PASS (C#) |
| `/api/cases/support` | POST | Cases | IMPLEMENTED | PASS (Newman & C#) |
| `/api/cases/support` | GET | Cases | IMPLEMENTED | PASS (Newman & C#) |
| `/api/cases/support/{id}` | GET | Cases | IMPLEMENTED | PASS (Newman & C#) |
| `/api/cases/support/{id}/messages` | POST | Cases | IMPLEMENTED | PASS (Newman & C#) |
| `/api/cases/support/{id}/messages/multipart` | POST | Cases | IMPLEMENTED | PASS (C# SQL02) |
| `/api/cases/disputes` | POST | Cases | IMPLEMENTED | PASS (Newman & C#) |
| `/api/cases/disputes` | GET | Cases | IMPLEMENTED | PASS (Newman & C#) |
| `/api/cases/disputes/{id}` | GET | Cases | IMPLEMENTED | PASS (Newman & C#) |
| `/api/cases/disputes/{id}/messages` | POST | Cases | IMPLEMENTED | PASS (Newman & C#) |
| `/api/cases/disputes/{id}/messages/multipart` | POST | Cases | IMPLEMENTED | PASS (C# SQL02) |
| `/api/cases/attachments/{id}` | GET | Cases | IMPLEMENTED | PASS (C#) |
| `/api/notifications` | GET | Notifications | IMPLEMENTED | PASS (Newman & C#) |
| `/api/notifications/{id}/read` | POST | Notifications | IMPLEMENTED | PASS (Newman & C#) |
| `/api/notifications/read-all` | POST | Notifications | IMPLEMENTED | PASS (Newman & C#) |
| `/api/notifications/search` | GET | Notifications | IMPLEMENTED | PASS (Newman & C#) |
| `/api/notifications/{id}` | DELETE | Notifications | IMPLEMENTED | PASS (Newman & C#) |
| `/api/notifications` | DELETE | Notifications | IMPLEMENTED | PASS (Newman & C#) |
| `/api/notifications/preferences` | GET | Notifications | IMPLEMENTED | PASS (Newman & C#) |
| `/api/notifications/preferences` | PUT | Notifications | IMPLEMENTED | PASS (Newman & C#) |
| `/api/dashboard` | GET | Dashboard | IMPLEMENTED | PASS (Newman & C#) |
| `/api/system/maintenance` | GET | Operations | IMPLEMENTED | PASS (C#) |
| `/api/system/announcements/active` | GET | Operations | IMPLEMENTED | PASS (C#) |
| `/api/bank-connections/link` | POST | Banking | IMPLEMENTED | PASS (C#) |
| `/api/bank-connections` | GET | Banking | IMPLEMENTED | PASS (Newman & C#) |
| `/api/bank-connections/{id}` | GET | Banking | IMPLEMENTED | PASS (Newman & C#) |
| `/api/bank-connections/{id}/unlink` | POST | Banking | IMPLEMENTED | PASS (Newman & C#) |
| `/api/bank-connections/{id}/reactivate` | POST | Banking | IMPLEMENTED | PASS (Newman & C#) |
| `/api/bank-connections/{id}/sync` | POST | Banking | IMPLEMENTED | PASS (Newman & C#) |
| `/api/bank-connections/test-mode/link-challenges` | POST | Banking | IMPLEMENTED | PASS (Newman & C#) |
| `/api/bank-connections/test-mode/link-challenges/{challengeId}/confirm` | POST | Banking | IMPLEMENTED | PASS (Newman & C#) |
| `/api/bank-connections/test-mode/wallet-top-ups` | POST | Banking | IMPLEMENTED | PASS (Newman & C#) |
| `/api/bank-connections/test-mode/wallet-top-ups/{topUpId}/confirm` | POST | Banking | IMPLEMENTED | PASS (Newman & C#) |
| `/api/bank-connections/test-mode/linked-accounts` | GET | Banking | IMPLEMENTED | PASS (Newman & C#) |
| `/api/bank-connections/test-mode/wallet-top-ups` | GET | Banking | IMPLEMENTED | PASS (Newman & C#) |
| `/api/bank-transactions` | GET | Bank Transactions | IMPLEMENTED | PASS (Newman & C#) |
| `/api/bank-transactions/{id}` | GET | Bank Transactions | IMPLEMENTED | PASS (Newman & C#) |
| `/api/bank-transactions/{id}/category` | PATCH | Bank Transactions | IMPLEMENTED | PASS (Newman & C#) |
| `/api/bank-integrations/sepay/events` | POST | SePay Webhook | IMPLEMENTED | PASS (C#) |
| `/api/bank-integrations/sepay/ipn` | POST | SePay IPN | IMPLEMENTED | PASS (C#) |
| `/api/groups` (và 28 endpoints nhóm cũ) | * | Legacy Groups | IMPLEMENTED (Backwards Compatibility) | PASS (C#) |

---

## KẾT LUẬN & SỰ SẴN SÀNG NỘI BỘ (100% INTERNAL READINESS)

1. **Về Mã nguồn & Nghiệp vụ nội bộ:** Đạt **100% Sẵn sàng (100% Ready)**. Mọi endpoint User nội bộ đều có đầy đủ Implementation, DTO, Validation, Phân quyền User, Rate Limiter và kiểm soát bất biến tài chính sổ cái kép.
2. **Về Tích hợp Nhà cung cấp bên ngoài (External Providers):**
   - **Google Gemini:** Hoạt động ở chế độ Free Tier (`gemini-3.5-flash-lite`), có fallback HTTP 503 khi thiếu key.
   - **SePay:** Nạp TestMode và Webhook signature đã sẵn sàng; live provider outbound payout giữ nguyên ranh giới đối soát.
   - **Email SMTP:** Đã kiểm chứng kết nối thực và chế độ Pickup Outbox an toàn khi test.
   - **Expo Push:** API contract và cơ chế đăng ký thiết bị đã hoàn thiện; tiếp nhận trên thiết bị vật lý thực tế thuộc phạm vi giai đoạn Mobile app.
