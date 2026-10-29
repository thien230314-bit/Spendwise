# SPENDWISE — USER MOBILE PRODUCT SPECIFICATION & BACKEND API CONTRACT

> **Nghiệm thu User baseline: 2026-10-27; hướng dẫn UI cập nhật: 2026-10-29.**  
> **`USER BASELINE — ACCEPTED`** (306 requests/343 assertions là lượt chạy lịch sử; chưa chứng minh đã chạy lại trên mọi thay đổi Backend Admin sau đó).  
> - **Full Newman E2E Suite (Backend User Demo Baseline):** **306 requests, 343/343 assertions PASS, 0 FAIL, Exit Code 0**.  
> - **Bằng chứng Nghiệm thu (User Demo Locked):** [`reports/newman/backend-user-final-verified-summary.md`](./reports/newman/backend-user-final-verified-summary.md), [`reports/newman/backend-user-final-lock.md`](./reports/newman/backend-user-final-lock.md) (file raw execution JSON `backend-user-final-verified.json` lưu trữ nội bộ).  
> - **Baseline Tested Commit SHA:** `5ce3ab8ff02adf2dd267bcbb03f41627bed37f98` (cùng các commit hardening sở hữu ngân hàng và bảo mật sinh trắc học tiếp theo).  
> - **Môi trường nghiệm thu:** API Kestrel Release (`http://127.0.0.1:5158`) kết nối SQL Server `DESKTOP-B5KA3LT\SQLEXPRESS02`, Database `SpendWise_E2E_G4`.  
> - **Phân định ranh giới nghiệm thu & Module Sinh trắc học mới:**  
>   1. **Backend User Demo:** Toàn bộ nghiệp vụ quản lý ví, thu chi cá nhân, ngân sách, mục tiêu, giao dịch định kỳ, quỹ Nhóm Cố định V2 & Tự do, phân quyền dữ liệu, idempotency, sổ cái kép và SePay Test Mode Sandbox (Nạp/Rút Demo) đã được nghiệm thu và **KHÓA CHỨC NĂNG (LOCKED)**.  
>   2. **Module Sinh trắc học mới (WebAuthn / Passkeys & Transaction Authorizations):** Là feature bảo mật mới được bổ sung sau mốc khóa User, được xác thực độc lập 100% qua test suite chuyên dụng ([`reports/biometric/biometric-final-acceptance.md`](./reports/biometric/biometric-final-acceptance.md), [`reports/biometric/biometric-implementation-summary.md`](./reports/biometric/biometric-implementation-summary.md); kèm file raw test results JSON 27/27 security PASS và 14/14 integration PASS lưu trữ nội bộ). Module này cung cấp cơ chế ký mật mã WebAuthn Level 2 phía Backend; **tuyệt đối không ghi nhận sinh trắc học phần cứng (Hardware Biometric trên thiết bị thật) đã nghiệm thu**, vì việc truy cập Secure Enclave / Keystore vật lý sẽ được thực hiện khi đóng gói ứng dụng native ở giai đoạn sau.  
>   3. **Cổng thanh toán & Thiết bị ngoài:** SePay Live payout, SMTP production và giao diện người dùng Mobile thật chưa nằm trong phạm vi nghiệm thu.

---

## 1. TỔNG QUAN HỆ THỐNG & ĐẶC TẢ SẢN PHẨM USER MOBILE

### 1.1. Tầm nhìn sản phẩm
SpendWise là ứng dụng di động hỗ trợ quản lý tài chính cá nhân thông minh và quản lý quỹ nhóm minh bạch, tin cậy:
1. **Quản lý tài chính cá nhân:** Theo dõi dòng tiền thu/chi tức thời, phân loại danh mục, lập ngân sách chi tiêu, lập mục tiêu tích lũy và giao dịch định kỳ với độ toàn vẹn sổ cái kế toán kép (Double-Entry Ledger).
2. **Quản lý quỹ nhóm minh bạch:**
   - **Quỹ Cố định (Fixed Fund V2):** Mô hình góp quỹ theo nghĩa vụ bằng nhau hoặc tùy biến, quy tắc chi tiêu động nghiêm ngặt không giữ chỗ giả tạo (đã loại bỏ ngưỡng 20% và Pending24h).
   - **Quỹ Tự do (Free Fund):** Mô hình quỹ linh hoạt theo phong cách Quỹ Nhóm hiện đại, hỗ trợ góp tự nguyện, gửi yêu cầu chi tiêu/rút tiền có sự phê duyệt của Chủ quỹ (Owner).
3. **Trải nghiệm người dùng:** Giao diện trực quan, hỗ trợ 3 chủ đề (`light`, `dark`, `system`) và 2 ngôn ngữ (`vi-VN`, `en-US`), phân biệt rõ ràng trạng thái giao dịch (`Pending`, `Processing`, `Completed`, `Failed`).

### 1.2. Ranh giới kiến trúc & Nguyên tắc bảo mật
- **Nền tảng Backend:** ASP.NET Core (.NET 10 Kestrel) kiến trúc Clean Architecture: Domain Entities $\rightarrow$ Application CQRS Handlers $\rightarrow$ Infrastructure EF Core $\rightarrow$ API Controllers.
- **Ranh giới Actor:** `SystemRole.User = 1` là User, `SystemRole.Admin = 2` là Admin hệ thống duy nhất. `SystemAdmin = 0` và `SuperAdmin = 3` là giá trị legacy bị chặn quyền hoạt động. `Fund Owner` vẫn là User, chỉ quản lý quỹ mình sở hữu, không có quyền Admin hệ thống.
- **Bảo mật giao dịch:** Mọi lệnh ghi biến động số dư yêu cầu mã PIN bảo mật 6 số (`SecurityPin`) hoặc chữ ký sinh trắc học phần cứng (`WebAuthn / FIDO2 Level 2`). Khóa chống trùng lặp (`IdempotencyKey`) và phạm vi (`IdempotencyScope`) bảo đảm tính bất biến (idempotent) tuyệt đối, không double-posting.

---

> [!IMPORTANT]
> **HƯỚNG DẪN CODEX / FRONTEND:** File này là đặc tả UI User. Backend Admin/Dispute/role đã thay đổi trên **workspace local** nhưng chưa được push lên GitHub `main` trong lượt kiểm toán cuối. Khi tích hợp cần kiểm tra method/route/DTO ở API đang chạy; không coi code `main` cũ là hợp đồng đã triển khai. SePay real money và Gemini live provider chưa được xác minh đầy đủ.

### 1.3. Bảng phân quyền và trạng thái tài khoản cho giao diện

| Actor / trạng thái | Quyền và hành vi UI |
|---|---|
| `User` / `Active` | Dashboard cá nhân, ví, giao dịch, chuyển tiền, ngân sách, mục tiêu, định kỳ, nhóm quỹ, ngân hàng demo, AI, thông báo, hỗ trợ/khiếu nại, hồ sơ |
| `User` / `Restricted` | Đăng nhập và đọc dữ liệu, nhận tiền hợp lệ, gửi SupportCase; **không được ghi nợ tiền ra** (`403 account_restricted`) |
| `User` / `Locked` | Chặn login/refresh, thu hồi phiên. Sau khi mở khóa phải đăng nhập lại; không tạo màn hình giao dịch khi đã bị khóa |
| `Fund Owner` | User có quyền duyệt/cấp quyền và quản lý thành viên trong quỹ sở hữu, **không** có quyền Admin hệ thống |
| `Admin = 2` | UI quản trị riêng, không được dùng để giả vai trò User trong nghiệp vụ tài chính |
| `SystemAdmin = 0`, `SuperAdmin = 3` | Legacy, không cấp quyền hoặc hiển thị UI Admin |

**Sitemap UI User:** Đăng ký/OTP → Đăng nhập/PIN/Passkey → Home → Ví/Giao dịch → Ngân sách/Mục tiêu/Định kỳ → Nhóm quỹ (Owner/Member) → Ngân hàng demo → AI Assistant → Thông báo → Hỗ trợ/Khiếu nại → Hồ sơ/Cài đặt/Bảo mật.

**Không chỉ ẩn nút UI:** Backend luôn xác thực ownership, trạng thái User và role. Frontend xử lý `401`, `403`, `409`, lỗi PIN và trường hợp phiên hết hạn bằng UX phù hợp.

---
## 2. XÁC THỰC, TÀI KHOẢN & SINH TRẮC HỌC (AUTHENTICATION & SECURITY)

### 2.1. Chu trình xác thực người dùng cơ bản
1. **Đăng ký (`POST /api/auth/register`):** Nhận email, mật khẩu mạnh (tối thiểu 8 ký tự, chữ hoa, chữ thường, số, ký tự đặc biệt), họ tên. Hệ thống phát hành `EmailVerificationToken` và gửi OTP 6 số qua email.
2. **Xác thực Email (`POST /api/auth/email-verification/verify`):** Nhập mã OTP. Kích hoạt tài khoản (`EmailVerifiedAt != null`). Môi trường TEST sử dụng OTP cấu hình an toàn.
3. **Thiết lập mã PIN (`POST /api/auth/pin/setup`):** Thiết lập mã PIN bảo mật 6 chữ số dùng cho các thao tác tài chính. Mã PIN được băm an toàn (`SecurityPinHash`).
4. **Đăng nhập (`POST /api/auth/login`):** Nhận diện qua Email hoặc SpendId (`identifier`) và mật khẩu. Trả về `challengeToken` để xác nhận mã PIN (`POST /api/auth/pin/verify`). Sau khi xác nhận PIN, hệ thống cấp phát cặp JWT:
   - `accessToken`: Hạn dùng ngắn (15 phút), chứa `sub` (UserId), `email`, `role`, `sid` (SessionId).
   - `refreshToken`: Hạn dùng dài (30 ngày), lưu trữ trong `UserSessions` để cấp mới token.
5. **Đổi mật khẩu / Khôi phục mật khẩu:** `/api/auth/change-password`, `/api/auth/forgot-password`, `/api/auth/reset-password`.
6. **Quản lý phiên hoạt động (`/api/auth/sessions`):** Liệt kê các phiên thiết bị đang hoạt động; thu hồi phiên cụ thể (`DELETE /api/auth/sessions/{id}`) hoặc toàn bộ phiên khác (`POST /api/auth/sessions/revoke-others`).

### 2.2. Sinh trắc học & Passkey Backend (WebAuthn / FIDO2 Level 2)
Backend SpendWise hỗ trợ đầy đủ chuẩn W3C WebAuthn / FIDO2 Level 2 bảo mật Zero-Knowledge:
- **Thuật toán hỗ trợ:** `ES256` (ECDSA P-256 / SHA-256, chuẩn của Apple Secure Enclave & Android Keystore) và `RS256`.
- **Lưu trữ Zero-Knowledge:** Backend **chỉ lưu trữ khóa công khai SPKI Base64** trong bảng `UserPasskeyCredentials`. Tuyệt đối không lưu trữ khóa riêng tư (Private Key) hay dữ liệu khuôn mặt/vân tay thô.
- **Xác minh chữ ký & Tính toàn vẹn:**
  - `clientDataJSON`: Kiểm tra `type` (`webauthn.get` / `webauthn.create`), đối soát `challenge`, và xác thực `origin` tin cậy.
  - `authenticatorData`: Đối soát hash SHA-256 của `rpId` (`spendwise.test`), cờ hiện diện `UP = 1`, và **bắt buộc cờ xác thực người dùng `UV = 1`** cho mọi giao dịch tài chính.
  - Chống phát lại & nhân bản: Challenge chỉ sử dụng 1 lần; theo dõi số đếm `signCount` ngăn chặn credential cloning (counter rollback rejection).
- **Bộ API Passkey / Biometric:**
  - `POST /api/auth/passkeys/register/options`: Cấp phát challenge đăng ký passkey mới.
  - `POST /api/auth/passkeys/register/verify`: Xác minh khóa công khai và kích hoạt passkey thiết bị.
  - `POST /api/auth/passkeys/login/options`: Cấp phát challenge đăng nhập nhanh bằng sinh trắc học.
  - `POST /api/auth/passkeys/login/verify`: Xác minh chữ ký sinh trắc học, cấp phát trực tiếp phiên JWT hợp lệ.
  - `GET /api/auth/passkeys`: Danh sách các passkey/thiết bị đã liên kết của người dùng.
  - `DELETE /api/auth/passkeys/{id}`: Thu hồi ngay lập tức passkey đã đăng ký.

### 2.3. Ranh giới Sinh trắc học Demo (Frontend UI vs Backend Cryptography)
> [!IMPORTANT]
> **QUY ĐỊNH PHẠM VI SINH TRẮC HỌC DEMO (GOOGLE AI STUDIO & MOBILE CLIENT):**
> 1. Giao diện Frontend phát triển trên Google AI Studio trình diễn Face ID / Vân tay là **DEMO SIMULATION**.
> 2. Quá trình quét sinh trắc học trên UI là hoạt ảnh trình diễn (animation & mockup prompt), **không yêu cầu và không phải bằng chứng đã tương tác với cảm biến phần cứng thật của iPhone/Android**.
> 3. Tuyệt đối **không được ghi nhận trạng thái "Hardware Biometric Verified"** trên hệ thống và **không được gửi cờ giả mạo kiểu `biometricVerified: true`** để vượt qua các lớp bảo mật backend.
> 4. Khi thực hiện xác thực giao dịch tài chính bằng sinh trắc học qua API backend, Client bắt buộc phải tuân thủ luồng ký mật mã (Cryptographic Authorization) chuẩn: lấy challenge từ backend $\rightarrow$ ký bằng private key hợp lệ $\rightarrow$ gửi `signature`, `clientDataJSON`, `authenticatorData` về endpoint `/api/transaction-authorizations/verify`.

---

## 3. QUẢN LÝ VÍ & NGHIỆP VỤ TÀI CHÍNH CÁ NHÂN (FINANCE & WALLET)

### 3.1. Giới hạn và Phân loại Ví cá nhân
Mỗi người dùng được quản lý tối đa **3 ví cá nhân hoạt động đồng thời** (`WalletLimit: 3`):
1. **Ví Mặc định (Default Spending Wallet):** Tạo tự động khi kích hoạt tài khoản, dùng cho chi tiêu sinh hoạt thường ngày.
2. **Ví Tiết kiệm (Savings Wallet):** Ví tích lũy dành cho các mục tiêu cá nhân.
3. **Ví Khẩn cấp (Emergency Wallet):** Ví dự phòng tài chính.
- *Quy tắc an toàn:* Cố gắng tạo ví thứ 4 sẽ bị từ chối ngay lập tức với mã lỗi `wallet_limit_reached` (HTTP 409).
- *Đóng ví:* Chỉ được đóng ví phụ khi số dư khả dụng bằng 0 và không có giao dịch đang giữ chỗ.

### 3.2. Ràng buộc An toàn Ngân hàng (`IActiveBankGuard` Fail-Closed)
Mọi biến động số dư ví (ghi có hoặc ghi nợ) đều được bảo vệ bởi `IActiveBankGuard`:
- Người dùng **bắt buộc phải có ít nhất một tài khoản ngân hàng ở trạng thái Active (`Status == Active`)** mới được thực hiện các thao tác: mở ví mới, nạp tiền, rút tiền, chuyển tiền nội bộ giữa các ví, chuyển tiền P2P, tạo khoản thu/chi cá nhân, đóng góp quỹ nhóm.
- Nếu chưa liên kết hoặc đã hủy liên kết ngân hàng, API trả về `active_bank_required` (HTTP 409).

### 3.3. Thu nhập, Chi tiêu & Chuyển tiền nội bộ
- **Ghi nhận Thu nhập (`POST /api/transactions/income`):** Tăng số dư khả dụng ví, hạch toán ghi có sổ cái, gắn danh mục thu nhập.
- **Ghi nhận Chi tiêu (`POST /api/transactions/expenses`):** Trừ số dư khả dụng ví, kiểm tra số dư không âm, hạch toán ghi nợ sổ cái, kiểm tra hạn mức ngân sách tháng.
- **Chi tiêu đa ví (`POST /api/transactions/expenses/multi-wallet`):** Phân bổ một khoản chi tiêu lớn từ nhiều ví cá nhân trong cùng một giao dịch nguyên tử.
- **Chuyển tiền giữa các ví cá nhân (`POST /api/transfers/internal`):**
  - Chuyển tiền từ Ví A sang Ví B của cùng một người dùng.
  - Yêu cầu xác thực `SecurityPin` hoặc chữ ký sinh trắc học.
  - Hạch toán đồng thời Có tài khoản nguồn và Nợ tài khoản đích, hạch toán loại `InternalTransfer` (không làm sai lệch báo cáo Thu nhập / Chi tiêu tổng thể).
- **Chuyển tiền P2P (`POST /api/transfers/p2p`):**
  - Chuyển tiền sang ví người dùng SpendWise khác qua Email, Số điện thoại hoặc SpendId.
  - Ngăn chặn tuyệt đối chuyển tiền cho chính mình (`self_transfer`, HTTP 400).
  - Kiểm tra `IActiveBankGuard` của cả người gửi và người nhận.
  - Yêu cầu `SecurityPin` hoặc Biometric Authorization; hạch toán `TransferOut` cho người gửi và `TransferIn` cho người nhận.

### 3.4. Kế hoạch tài chính: Ngân sách, Mục tiêu & Giao dịch định kỳ
- **Ngân sách chi tiêu (`/api/budgets`):** Thiết lập hạn mức chi tiêu theo tháng và theo danh mục (Category). Cảnh báo khi chi tiêu chạm ngưỡng 80% và 100%.
- **Mục tiêu tích lũy (`/api/goals`):** Đặt mục tiêu số tiền và thời hạn. Nộp tiền vào mục tiêu (`/contribute`) hoặc rút về ví (`/withdraw`).
- **Giao dịch định kỳ (`/api/recurring-transactions`):** Lập lịch tự động ghi nhận thu/chi theo chu kỳ ngày, tuần, tháng.
- **Quy tắc phân loại tự động (`/api/transaction-rules`):** Tự động gán danh mục dựa trên từ khóa mô tả giao dịch.

---

## 4. QUỸ NHÓM: QUỸ CỐ ĐỊNH V2 & QUỸ TỰ DO (GROUP FUNDS)

### 4.1. Quỹ Cố định V2 (Fixed Fund V2) — Quy tắc nghiệp vụ chuẩn hóa
> [!IMPORTANT]
> **ĐỒNG BỘ QUYẾT ĐỊNH PRODUCT OWNER CHO FIXED FUND V2:**
> 1. **Bãi bỏ hoàn toàn ngưỡng 20% và trạng thái Pending24h:** Không còn cơ chế tạm giữ 24 giờ hay giới hạn lũy kế 20% như phiên bản cũ. Các khoản chi hợp lệ được thực thi tức thời.
> 2. **Cấp quyền chi tiêu (`CanSpend`):** Chủ quỹ (Owner) chỉ có quyền cấp cờ `CanSpend = true` cho thành viên đã **hoàn thành 100% nghĩa vụ đóng góp cá nhân** (`RequiredVnd`).
> 3. **Quyền chi thực tế (`EffectiveCanSpend`):** Được hệ thống tính toán động dựa trên 4 điều kiện bắt buộc:
>    - Thành viên đã đóng đủ 100% nghĩa vụ cá nhân.
>    - Quỹ nhóm đã đạt 100% mục tiêu ban đầu (`TargetVnd`).
>    - Thành viên đã được cấp quyền `CanSpend`.
>    - Quỹ đang ở trạng thái Hoạt động (`Active`), tuân thủ các quy tắc governance và điều kiện chi tiêu của quỹ (báo cáo chi tiêu đã hoàn thành không tự đảo giao dịch).
> 4. **Quyền chi của Chủ quỹ (Owner):** Chủ quỹ không cần cờ `CanSpend`, nhưng **bắt buộc phải tự đóng đủ 100% nghĩa vụ của mình và quỹ phải đạt 100% mục tiêu nhóm** mới được chi tiêu.
> 5. **Bảo toàn trạng thái hoàn thành:** Khi quỹ đã đạt 100% mục tiêu, các khoản chi hợp lệ làm giảm số dư tiền mặt trong ví quỹ **KHÔNG làm mất trạng thái hoàn thành của nhóm** (hệ thống so sánh tổng đóng góp lịch sử hợp lệ với `TargetVnd`).
> 6. **Báo cáo khoản chi bất thường (`POST /api/v1/funds/requests/{id}/report`):** Tạo `SupportCase` Khiếu nại gửi Admin điều tra, không tạo Dispute độc lập mới; **tuyệt đối không tự động đảo ngược tiền (no auto-clawback)** và không can thiệp vào bút toán sổ cái của khoản chi đã hoàn tất.

### 4.2. Quỹ Tự do (Free Fund)
- Không áp đặt nghĩa vụ đóng góp cố định; các thành viên tự nguyện đóng góp theo nhu cầu.
- Mọi yêu cầu chi tiêu hoặc rút tiền từ thành viên đều phải được Chủ quỹ (Owner) xem xét và phê duyệt (`/review`).
- Rút tiền giải tán hoặc rút vốn cá nhân được tính toán dựa trên tỷ lệ đóng góp ròng lịch sử của từng thành viên qua thuật toán hoàn tiền chuẩn (`/refund-preview`).

---

## 5. SEPAY TEST MODE SANDBOX: NẠP & RÚT TIỀN DEMO

### 5.1. Bản chất Môi trường Demo SePay Sandbox
- Hệ thống SpendWise kết nối với môi trường **SePay Test Mode Sandbox** (`userapi-sandbox.sepay.vn/v2`), không sử dụng tiền thật và không liên kết cổng thanh toán thật.
- **Phân định quyền sở hữu tài khoản ngân hàng TEST đã nghiệm thu:**
  - **User A (`23838232-9E4F-436A-B900-9E93E612D1D4`):** Sở hữu liên kết **VPBank TEST `****0001`** (`BankConnectionId: 8B5E9450-44C6-481F-80A7-551FC6817054`).
  - **User B (`3A8B3FD7-3AF2-46AB-8FA2-BD43D6B1E67A`):** Sở hữu liên kết **TPBank TEST `****0002`** (`BankConnectionId: E9E1753B-552C-4E8F-83CA-D796AA62BFCE`).
  - Cô lập tuyệt đối: User A không thể nạp/rút qua TPBank của User B (API chặn trả về `404 ConnectionNotFound`), và ngược lại.

### 5.2. Luồng Nạp tiền DEMO (Top-Up)
1. User chọn số tiền nạp từ ngân hàng liên kết vào ví SpendWise.
2. Backend tạo `WalletTopUp` trạng thái `Pending` với mã tham chiếu duy nhất dạng `SWTP...`.
3. Khi SePay Sandbox ghi nhận giao dịch mô phỏng (Tiền ra khỏi ngân hàng TEST) với đúng mã tham chiếu và số tiền $\rightarrow$ Backend chuyển trạng thái `Completed`, hạch toán ghi có ví người dùng.
4. *Quy tắc an toàn:* Biometric intent confirmation không tự ý cộng tiền vào ví nếu chưa có đối soát giao dịch sandbox hợp lệ.

### 5.3. Luồng Rút tiền DEMO (Withdrawal) & Bằng chứng Nghiệm thu Khóa
1. **Khởi tạo & Tạm giữ (Hold Reservation):**
   - User yêu cầu rút tiền về ngân hàng liên kết kèm mã PIN hoặc WebAuthn assertion.
   - Hệ thống tạo `WalletWithdrawal` trạng thái `Processing` với mã tham chiếu `SWWD...`.
   - Ngay lập tức khấu trừ số dư khả dụng trong ví SpendWise và hạch toán chuyển tiền vào tài khoản tạm giữ hệ thống `SYSTEM:ReservationHold:VND`.
2. **Đối soát & Tất toán (Settlement):**
   - SePay Sandbox ghi nhận giao dịch tiền vào tài khoản ngân hàng TEST tương ứng.
   - Hệ thống đối soát thành công: chuyển `WalletWithdrawal` sang `Completed`, tất toán từ `SYSTEM:ReservationHold:VND` sang tài khoản quyết toán ngoài `SYSTEM:ExternalSettlement:VND`.
   - Net biến động trên tài khoản tạm giữ bằng đúng `0 VND`.
3. **Bằng chứng Nghiệm thu Khóa (User B + TPBank TEST):**
   - **Giao dịch ID:** `5F89A5D9-A160-4242-B595-25DA35063411`.
   - **Mã tham chiếu:** `SWWD3EEBA411C767A510FB7BD57C`.
   - **Số tiền rút:** `50.000 VND`.
   - **Biến động số dư TPBank TEST:** Tăng chính xác từ `150.000 VND` lên `200.000 VND` (xác nhận từ SePay Sandbox API).
   - **Biến động số dư ví SpendWise User B:** Giảm từ `4.875.000 VND` xuống `4.825.000 VND`.
   - **Trạng thái:** `Completed`, replay protection PASS, không double-debiting.

---

## 5B. HỖ TRỢ/KHIẾU NẠI, THÔNG BÁO VÀ AI ASSISTANT

### 5B.1. Luồng Hỗ trợ/Khiếu nại trực tiếp cho Admin

- `POST /api/cases/support`: User tạo hồ sơ với `category`, `priority`, `subject`, `description`; `linkedTransactionId` tùy chọn nếu có giao dịch hợp lệ của User. Danh mục ví dụ: `general`, `complaint_transaction`. Không bắt buộc đi qua Gemini.
- `GET /api/cases/support`: danh sách Case của User. `GET /api/cases/support/{id}`: chi tiết, trạng thái, lịch sử và tệp.
- `POST /api/cases/support/{id}/messages`: User bổ sung thông tin hoặc trả lời Admin; có API multipart riêng nếu đính kèm file.
- Luồng UI: User gửi → Admin tiếp nhận → Admin phản hồi/yêu cầu thông tin → User bổ sung → Admin `resolved`/`rejected` → User đọc phản hồi và thông báo.
- `POST /api/v1/funds/requests/{id}/report`: báo cáo chi tiêu quỹ bất thường, tạo **SupportCase** để Admin điều tra, **không tự hoàn tiền, không tự sửa Ledger**.
- `POST /api/cases/disputes` đã deprecated trong Backend local, trả `400 dispute_workflow_deprecated`. **Không thiết kế form tạo Tranh chấp độc lập mới.** Hồ sơ Dispute lịch sử chỉ hiển thị read-only theo API còn hỗ trợ.

### 5B.2. Hộp thư thông báo User

- `GET /api/notifications`: danh sách; `POST /api/notifications/{id}/read`: đọc một; `POST /api/notifications/read-all`: đọc tất cả.
- `GET /api/notifications/preferences`, `PUT /api/notifications/preferences`: thiết lập nhận thông báo.
- User nhận được broadcast từ Admin; hiển thị unread count, detail, trạng thái loading/empty/error. Không đánh đồng in-app delivered với email/push delivered.

### 5B.3. AI Assistant

- AI hỗ trợ tư vấn và gợi ý; **không thay thế** việc User gửi Khiếu nại trực tiếp và không tự thực thi hành động ghi nợ chỉ bằng nội dung chat.
- Các công cụ AI tạo hành động nhạy cảm cần xác nhận và authorization ở Backend; không dùng UI simulation để vượt qua PIN/WebAuthn.
- Endpoint/DTO AI User phải đối chiếu Controller đang chạy khi tích hợp, không tự sáng tạo endpoint dựa trên ảnh UI.

---
## 6. DANH MỤC API USER CHI TIẾT (API CONTRACT)

| Phân hệ | Phương thức | Tuyến đường (Route) | Yêu cầu xác thực | Mô tả nghiệp vụ |
|---|---|---|---|---|
| **Auth** | `POST` | `/api/auth/register` | Anonymous | Đăng ký tài khoản, phát hành challenge OTP |
| | `POST` | `/api/auth/email-verification/verify` | Anonymous | Xác minh OTP email |
| | `POST` | `/api/auth/pin/setup` | Bearer Token | Thiết lập mã PIN bảo mật 6 số |
| | `POST` | `/api/auth/login` | Anonymous | Đăng nhập tài khoản, trả challengeToken |
| | `POST` | `/api/auth/pin/verify` | Anonymous (Challenge) | Xác nhận PIN, phát hành cặp JWT Access/Refresh |
| | `POST` | `/api/auth/refresh` | Anonymous | Làm mới access token từ refresh token |
| | `POST` | `/api/auth/logout` | Bearer Token | Đăng xuất, hủy phiên hoạt động hiện hành |
| | `GET` | `/api/auth/sessions` | Bearer Token | Danh sách phiên hoạt động |
| | `DELETE` | `/api/auth/sessions/{id}` | Bearer Token | Thu hồi một phiên cụ thể |
| **Passkeys** | `POST` | `/api/auth/passkeys/register/options` | Bearer Token | Tạo challenge đăng ký WebAuthn |
| | `POST` | `/api/auth/passkeys/register/verify` | Bearer Token | Xác minh khóa công khai và kích hoạt passkey |
| | `POST` | `/api/auth/passkeys/login/options` | Anonymous | Cấp challenge đăng nhập sinh trắc học |
| | `POST` | `/api/auth/passkeys/login/verify` | Anonymous | Xác minh assertion, đăng nhập cấp JWT |
| | `GET` | `/api/auth/passkeys` | Bearer Token | Danh sách passkey của tài khoản |
| | `DELETE` | `/api/auth/passkeys/{id}` | Bearer Token | Thu hồi passkey |
| **Biometric Tx**| `POST` | `/api/transaction-authorizations/options` | Bearer Token | Chuẩn bị intent tài chính và cấp challenge |
| | `POST` | `/api/transaction-authorizations/verify` | Bearer Token | Ký assertion sinh trắc (UV=1) & tất toán |
| **Profile** | `GET` | `/api/users/me` | Bearer Token | Lấy hồ sơ tài khoản và tùy chọn giao diện |
| | `PUT` | `/api/users/me/preferences` | Bearer Token | Đổi theme (`light|dark|system`) và ngôn ngữ |
| | `POST` | `/api/users/me/avatar` | Bearer Token | Tải lên ảnh đại diện |
| **Wallets** | `GET` | `/api/wallets` | Bearer Token | Danh sách ví cá nhân (tối đa 3 ví) |
| | `POST` | `/api/wallets` | Bearer + BankGuard | Mở thêm ví phụ (Savings / Emergency) |
| | `PUT` | `/api/wallets/{id}` | Bearer Token | Cập nhật tên ví |
| | `DELETE` | `/api/wallets/{id}` | Bearer Token | Đóng ví phụ khi số dư = 0 |
| **Finance** | `POST` | `/api/transactions/income` | Bearer + BankGuard | Ghi nhận thu nhập vào ví |
| | `POST` | `/api/transactions/expenses` | Bearer + BankGuard | Ghi nhận chi tiêu từ ví |
| | `POST` | `/api/transactions/expenses/multi-wallet` | Bearer + BankGuard | Ghi nhận chi tiêu trừ nhiều ví |
| | `GET` | `/api/transactions` | Bearer Token | Danh sách lịch sử giao dịch |
| | `PATCH` | `/api/transactions/{id}` | Bearer Token | Sửa ghi chú/mô tả giao dịch |
| | `POST` | `/api/transfers/internal` | Bearer + PIN/Bio | Chuyển tiền giữa các ví cá nhân |
| | `POST` | `/api/transfers/p2p` | Bearer + PIN/Bio | Chuyển tiền P2P sang người dùng khác |
| **Planning** | `GET` / `POST` | `/api/budgets` | Bearer Token | Quản lý hạn mức ngân sách tháng |
| | `GET` / `POST` | `/api/goals` | Bearer Token | Quản lý mục tiêu tài chính |
| | `POST` | `/api/goals/{id}/contribute` | Bearer + PIN | Nộp tiền vào mục tiêu |
| | `POST` | `/api/goals/{id}/withdraw` | Bearer + PIN | Rút tiền từ mục tiêu về ví |
| | `GET` / `POST` | `/api/recurring-transactions` | Bearer Token | Quản lý giao dịch định kỳ |
| **Fixed Fund** | `POST` | `/api/v1/funds` | Bearer Token | Tạo quỹ nhóm mới (Fixed max 30) |
| | `POST` | `/api/v1/funds/{id}/obligations/confirm`| Bearer (Owner) | Chốt định mức đóng góp nhóm |
| | `POST` | `/api/v1/funds/{id}/contributions` | Bearer + PIN/Bio | Đóng góp tiền vào quỹ cố định |
| | `PUT` | `/api/v1/funds/{id}/members/{userId}/spending-permission` | Bearer (Owner) | Cấp quyền CanSpend khi thành viên đạt 100% |
| | `POST` | `/api/v1/funds/{id}/requests` | Bearer + EffectiveCanSpend | Chi tiêu tức thì từ quỹ cố định |
| | `POST` | `/api/v1/funds/requests/{id}/report`| Bearer (Member) | Báo cáo chi tiêu bất thường (tạo SupportCase) |
| **Free Fund** | `POST` | `/api/v1/funds/requests/{requestId}/review` | Bearer (Owner) | Duyệt/Từ chối chi tiêu hoặc rút vốn |
| | `POST` | `/api/v1/funds/{id}/refund-preview` | Bearer Token | Xem trước số tiền nhận lại khi rời quỹ |
| | `POST` | `/api/v1/funds/{id}/leave` | Bearer + PIN | Rút lui khỏi quỹ tự do |
| **Hỗ trợ/Khiếu nại** | `POST` | `/api/cases/support` | Bearer User | Tạo Case trực tiếp gửi Admin |
| | `GET` | `/api/cases/support` | Bearer User | Danh sách Case của chính User |
| | `GET` | `/api/cases/support/{id}` | Bearer Case Owner | Chi tiết/lịch sử |
| | `POST` | `/api/cases/support/{id}/messages` | Bearer Case Owner | Phản hồi/bổ sung thông tin |
| | `POST` | `/api/cases/disputes` | Bearer User | **Deprecated 400**; không thiết kế tạo mới |
| **Thông báo** | `GET` | `/api/notifications` | Bearer User | Inbox |
| | `POST` | `/api/notifications/{id}/read` | Bearer User | Đánh dấu đã đọc |
| | `POST` | `/api/notifications/read-all` | Bearer User | Đọc tất cả |
| | `GET` / `PUT` | `/api/notifications/preferences` | Bearer User | Cấu hình nhận thông báo |
| **SePay Demo** | `GET` | `/api/bank-connections` | Bearer Token | Xem liên kết ngân hàng của tài khoản |
| | `POST` | `/api/bank-connections/test-mode/wallet-withdrawals` | Bearer + PIN/Bio | Khởi tạo rút tiền DEMO về ngân hàng TEST |
| | `GET` | `/api/bank-connections/test-mode/wallet-withdrawals` | Bearer Token | Danh sách lịch sử yêu cầu rút tiền |
| | `POST` | `/api/bank-connections/test-mode/wallet-withdrawals/{id}/reconcile` | Bearer Token | Đối soát tất toán rút tiền sandbox |

---

## 7. HƯỚNG DẪN TÍCH HỢP CHO FRONTEND (GOOGLE AI STUDIO & MOBILE)

1. **Quản lý Token & Làm mới phiên tự động:**
   - Lưu trữ `accessToken` trong bộ nhớ ứng dụng và `refreshToken` trong Secure Storage.
   - Khi API trả về `HTTP 401 unauthorized`, tự động gọi `POST /api/auth/refresh` với `refreshToken`. Nếu thất bại, điều hướng về màn hình Đăng nhập.
2. **Hiển thị Sinh trắc học Demo trên giao diện:**
   - Màn hình Face ID / Vân tay hiển thị dialog trực quan cho người dùng theo thiết kế Demo Simulation của Frontend Google AI Studio (hoạt ảnh quét sinh trắc học trình diễn).
   - **Quy tắc đăng nhập và xác thực API:** Để đăng nhập vào hệ thống hoặc thực hiện giao dịch qua API thực tế, Client phải sử dụng thông tin đăng nhập/mã PIN hợp lệ (`POST /api/auth/login`, `POST /api/auth/pin/verify`) hoặc WebAuthn credential đã đăng ký hợp lệ được ký mật mã từ client (`POST /api/auth/passkeys/login`), tuyệt đối không dùng shared demo private key hay cặp khóa mẫu sinh sẵn.
3. **Xác nhận giao dịch tài chính lớn:**
   - Với giao dịch chuyển tiền, rút tiền hoặc đóng quỹ: hiển thị popup tóm tắt (Số tiền, Người nhận, Phí 0đ).
   - Yêu cầu người dùng xác nhận bằng Mã PIN 6 số hoặc Xác thực Sinh trắc học.
   - Nếu chọn Sinh trắc học: gọi `POST /api/transaction-authorizations/options` lấy challenge $\rightarrow$ ký assertion $\rightarrow$ gọi `/verify`.
4. **Xử lý trạng thái Nạp/Rút SePay Sandbox:**
   - Sau khi tạo lệnh rút tiền thành công, trạng thái ban đầu là `Processing` (tiền trong ví đã được trừ/giữ chỗ).
   - Hiển thị badge màu cam `Đang xử lý đối soát ngân hàng`.
   - Cho phép người dùng bấm `Kiểm tra đối soát` (gọi endpoint `/reconcile`) hoặc chờ đối soát hoàn tất để chuyển sang badge màu xanh `Completed` (Đã chuyển về ngân hàng).

## 8. Nguyên tắc triển khai UI User cho Codex

- Chỉ dựng màn hình/route cho role thực sự có quyền. Fund Owner là quyền trong quỹ, không tạo màn Admin thứ hai.
- Với `Restricted`, vẫn cho người dùng xem dữ liệu và liên hệ Support; chặn thao tác ghi nợ. Với `Locked`, điều hướng về màn hình khóa/đăng nhập theo lỗi Backend.
- Chỉ dùng dữ liệu động và DTO từ API đang chạy. Dữ liệu minh họa/fixture trong tài liệu không phải dữ liệu sản phẩm.
- Không biến sandbox thành thanh toán tiền thật, không giả lập một giao dịch tài chính đã thành công.
- Ưu tiên kiểm tra API của Backend local đã nghiệm thu và trạng thái đồng bộ GitHub trước khi kết nối UI.
