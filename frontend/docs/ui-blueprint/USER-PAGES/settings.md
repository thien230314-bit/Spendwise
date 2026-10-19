# USER PAGE INVENTORY: SETTINGS & SECURITY (CÀI ĐẶT TÀI KHOẢN & BẢO MẬT)
**Page ID:** `USER-SETTINGS`  
**Route:** `/settings`  
**Layout:** `AppShell` (User Layout)  
**File Source:** `src/features/settings/SettingsPage.tsx` (1342 dòng)  
**Trạng thái Xác thực:** `BROWSER_VERIFIED` (Đã kiểm tra runtime trên Chrome DevTools MCP)

---

## 1. MỤC ĐÍCH NGHIỆP VỤ & TỔNG QUAN
Trung tâm thiết lập cấu hình trải nghiệm và an toàn bảo mật tài khoản cá nhân: quản lý hồ sơ (Họ tên, SĐT, Email), tùy chỉnh giao diện (Light/Dark/System), tùy chỉnh ngôn ngữ (Tiếng Việt / English), thiết lập hiển thị số dư, quản lý bảo mật 2 lớp qua mã PIN 6 số (Đổi PIN, Quên PIN), đổi mật khẩu đăng nhập, xem danh sách thiết bị tin cậy đã đăng nhập và Đăng xuất an toàn.

---

## 2. KIỂM KÊ CÁC SECTION TRÊN TRANG
*Thứ tự section tuân thủ chính xác quyết định Clean Financial Settings V2:*

| Section ID | Tên Section | Component Source | Vị trí Bố cục | Trạng thái Hiển thị |
|---|---|---|---|---|
| `SET-SEC-01` | Settings Header & Profile Card | `SettingsPage.tsx` lines 340-420 | Đầu trang (Top header) | Luôn hiển thị |
| `SET-SEC-02` | Profile Information Form | `SettingsPage.tsx` lines 430-580 | Khối 1: Thông tin cá nhân | Luôn hiển thị |
| `SET-SEC-03` | Preferences & App Display | `SettingsPage.tsx` lines 590-710 | Khối 2: Tùy chọn giao diện | Luôn hiển thị |
| `SET-SEC-04` | Two-Factor Security PIN Management | `SettingsPage.tsx` lines 720-890 | Khối 3: Mã PIN bảo mật 6 số | Luôn hiển thị |
| `SET-SEC-05` | Account Password & Trusted Devices | `SettingsPage.tsx` lines 900-1120 | Khối 4: Mật khẩu & Thiết bị | Luôn hiển thị |
| `SET-SEC-06` | Logout Action Area | `SettingsPage.tsx` lines 1130-1200 | Khối cuối cùng (Bottom) | Luôn hiển thị |

---

## 3. DANH MỤC TÍNH NĂNG CHI TIẾT (FEATURE INVENTORY)

### Section SET-SEC-01 & 02: Profile Information
- **Feature ID:** `SET-001`
  - **Tên:** Xem và Cập nhật thông tin hồ sơ (Update Profile)
  - **Mục đích:** Sửa Họ và tên, Số điện thoại. Email là định danh bất biến (Read-only).
  - **Validation:** Họ tên từ 2 đến 100 ký tự tiếng Việt hợp lệ; SĐT đúng 10 số VN chuẩn.
  - **API Call:** `authApiService.updateProfile({ fullName, phone })`.
  - **Verification:** `BROWSER_VERIFIED`.

### Section SET-SEC-03: Preferences
- **Feature ID:** `SET-002`
  - **Tên:** Chuyển đổi Chủ đề Sáng / Tối (Theme Switcher)
  - **Mục đích:** Hỗ trợ 3 tùy chọn: Sáng (`Light`), Tối (`Dark`), và Theo hệ điều hành (`System`).
  - **Provider:** `useTheme()`. Lưu vào `localStorage: spendwise_theme`.
  - **Verification:** `BROWSER_VERIFIED`.

- **Feature ID:** `SET-003`
  - **Tên:** Chuyển đổi Ngôn ngữ ứng dụng (Language Switcher)
  - **Mục đích:** Hỗ trợ Tiếng Việt (`vi-VN`) và Tiếng Anh (`en-US`).
  - **Provider:** `useI18n()`. Lưu vào `localStorage: spendwise_locale`.
  - **Verification:** `BROWSER_VERIFIED`.

- **Feature ID:** `SET-004`
  - **Tên:** Mặc định ẩn số dư khi mở ứng dụng (Default Hide Balance)
  - **Mục đích:** Tự động che số tiền mỗi khi đăng nhập lại để đảm bảo tính riêng tư.
  - **Verification:** `BROWSER_VERIFIED`.

### Section SET-SEC-04: Security PIN Management
- **Feature ID:** `SET-005`
  - **Tên:** Thiết lập hoặc Đổi mã PIN bảo mật 6 số (Change PIN)
  - **Mục đích:** Quy trình 3 bước: Nhập PIN cũ -> Nhập PIN mới 6 số -> Xác nhận lại PIN mới.
  - **Security Rule:** Không cho phép đặt các mã PIN yếu (123456, 111111, 000000,...); dùng component `SixDigitPinInput`.
  - **API Call:** `securityPinService.changePin(oldPin, newPin)`.
  - **Verification:** `BROWSER_VERIFIED`.

- **Feature ID:** `SET-006`
  - **Tên:** Quên mã PIN bảo mật (Forgot PIN Flow)
  - **Mục đích:** Gửi email chứa liên kết token bảo mật một lần để cấp lại mã PIN mới qua route `/reset-pin?token=...`.
  - **Verification:** `BROWSER_VERIFIED`.

### Section SET-SEC-05: Password & Devices
- **Feature ID:** `SET-007`
  - **Tên:** Đổi mật khẩu đăng nhập (Change Password)
  - **Mục đích:** Nhập mật khẩu hiện tại và đặt mật khẩu mới với thanh đo độ mạnh (Password Strength Meter).
  - **Validation:** Tối thiểu 8 ký tự, có chữ hoa, chữ thường, chữ số và ký tự đặc biệt.
  - **API Call:** `authApiService.changePassword({ oldPassword, newPassword })`.
  - **Verification:** `BROWSER_VERIFIED`.

- **Feature ID:** `SET-008`
  - **Tên:** Quản lý phiên & Thiết bị tin cậy (Trusted Devices)
  - **Mục đích:** Xem danh sách trình duyệt/thiết bị đang đăng nhập, địa chỉ IP gần nhất, và nút Thu hồi quyền tin cậy của thiết bị khác (Revoke Device).
  - **Verification:** `BROWSER_VERIFIED`.

### Section SET-SEC-06: Logout Action
- **Feature ID:** `SET-009`
  - **Tên:** Đăng xuất an toàn khỏi tài khoản (Logout Action)
  - **Mục đích:** Xóa toàn bộ token trong `sessionStorage` và `localStorage`, hủy phiên API, đưa người dùng về trang `/login`.
  - **Vị trí:** Nằm ở cuối cùng của trang Settings theo đúng kiến trúc Clean Financial.
  - **Verification:** `BROWSER_VERIFIED`.
