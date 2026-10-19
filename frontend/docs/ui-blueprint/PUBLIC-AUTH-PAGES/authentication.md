# PUBLIC & AUTHENTICATION PAGES INVENTORY
**Hệ thống:** SpendWise Authentication & Security Gateway  
**Phạm vi:** `/login`, `/register`, `/reset-pin`, `/forbidden`  
**File Sources:** `src/features/auth/*`, `src/pages/ForbiddenPage.tsx`  
**Trạng thái Xác thực:** `SOURCE_VERIFIED` & `BROWSER_VERIFIED`

---

## 1. MỤC ĐÍCH NGHIỆP VỤ & TỔNG QUAN
Cổng xác thực danh tính và kiểm soát truy cập an toàn cao của SpendWise. Được xây dựng trên môi trường bàn làm việc vật lý (Realistic Desk Tabletop) với mô hình sổ tay gấp (Notebook Flipped Book) tích hợp hiệu ứng lật trang 3D mượt mà. Đảm bảo quy trình đăng ký 3 bước chuẩn Fintech: Thông tin cá nhân -> Xác thực Email OTP 8 số -> Thiết lập mã PIN bảo mật 6 số; hỗ trợ đăng nhập 2 lớp (Mật khẩu + PIN 6 số) và quy trình cấp lại mã PIN / Mật khẩu an toàn.

---

## 2. KIỂM KÊ CÁC SECTION VÀ MÀN HÌNH CON

### 2.1. Đăng nhập SpendWise (`AUTH-LOGIN` — `/login`)
Component: `src/features/auth/AuthPage.tsx` (mode: `login`)
- **Section AUTH-SEC-01 (Desk Tabletop Environment):** Nền bàn làm việc gỗ sồi tự nhiên (Light Mode) hoặc gỗ óc chó hun khói (Dark Mode) kèm bóng đổ đèn bàn và viền sổ da.
- **Section AUTH-SEC-02 (Login Credentials Form):**
  - Input Email hoặc Số điện thoại (Identifier)
  - Input Mật khẩu với nút ẩn/hiện mắt (`showPassword`)
  - Checkbox "Ghi nhớ đăng nhập" (`rememberMe`)
  - Link "Quên mật khẩu?" (Mở modal khôi phục tài khoản)
  - Nút Submit với hiệu ứng vòng tròn `SpendWiseSLoading` theo vòng đời
- **Section AUTH-SEC-03 (Two-Factor PIN Challenge Modal):**
  - Hiển thị khi nhập đúng mật khẩu và tài khoản đã kích hoạt mã PIN.
  - Component 6 ô số `SixDigitPinInput`.
  - Link "Quên mã PIN?" (Mở modal yêu cầu gửi email cấp lại PIN).
- **Features:** `AUTH-001` (Submit Login), `AUTH-002` (Verify PIN Challenge), `AUTH-003` (Forgot Password Modal), `AUTH-004` (Forgot PIN Modal).

### 2.2. Đăng ký tài khoản (`AUTH-REGISTER` — `/register`)
Component: `src/features/auth/AuthPage.tsx` (mode: `register`)
- **Section AUTH-SEC-04 (Register Form — Step 1):**
  - Input Họ và tên (Kiểm tra 2-100 ký tự hợp lệ tiếng Việt qua `isValidFullName`)
  - Input Địa chỉ Email (Kiểm tra trùng lặp thời gian thực `isEmailDuplicate`)
  - Input Số điện thoại (Kiểm tra đúng 10 số VN qua `isValidVietnamPhone`)
  - Input Mật khẩu & Nhập lại mật khẩu kèm thanh đo độ mạnh (Password Strength Meter: Rất yếu, Yếu, Trung bình, Mạnh)
- **Section AUTH-SEC-05 (Email OTP Verification — Step 2):**
  - Nhập mã xác thực email 8 chữ số do máy chủ gửi đến email đăng ký.
  - Bộ đếm thời gian gửi lại (Cooldown 60s) chống spam 429.
- **Section AUTH-SEC-06 (Security PIN Setup — Step 3):**
  - Thiết lập mã PIN bảo mật 6 chữ số lần đầu.
  - Bước 1: Nhập PIN 6 số. Bước 2: Nhập lại xác nhận PIN 6 số.
- **Features:** `AUTH-005` (Submit Registration), `AUTH-006` (Verify 8-Digit Email OTP), `AUTH-007` (Initial PIN Setup).

### 2.3. Khôi phục mã PIN bảo mật (`AUTH-RESET-PIN` — `/reset-pin?token=...`)
Component: `src/features/auth/ResetPinPage.tsx` (457 dòng)
- **Section AUTH-SEC-07 (Token Validation & PIN Setup):**
  - Tự động thẩm tra tính hợp lệ của token một lần gửi qua email (`securityPinService.validatePinResetToken`).
  - Giao diện thiết lập PIN 2 bước (Nhập PIN mới -> Xác nhận PIN mới).
  - Trạng thái thông báo: Đang xác thực (`validating`), Token không hợp lệ/Hết hạn (`invalid`/`expired`), Thành công (`success`).
- **Features:** `AUTH-008` (Reset PIN via Token).

### 2.4. Trang từ chối quyền truy cập (`SYS-FORBIDDEN` — `/forbidden`)
Component: `src/pages/ForbiddenPage.tsx` (63 dòng)
- **Section AUTH-SEC-08 (403 Forbidden Shield):**
  - Thông báo người dùng không có quyền truy cập trang này (ví dụ: User cố vào `/admin` hoặc Admin cố vào `/dashboard`).
  - Nút quay về trang chủ hợp lệ tương ứng với vai trò của tài khoản.
- **Features:** `AUTH-009` (403 Role Recovery Navigation).
