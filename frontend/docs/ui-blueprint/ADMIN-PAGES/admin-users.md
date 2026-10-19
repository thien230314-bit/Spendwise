# ADMIN PAGE INVENTORY: USER MANAGEMENT (QUẢN TRỊ NGƯỜI DÙNG)
**Page ID:** `ADMIN-USERS`  
**Route:** `/admin/users`  
**Layout:** `AdminAppShell`  
**Quyền hạn:** `Role: SystemAdmin`  
**File Source:** `src/features/admin/AdminUsersPage.tsx`  
**Trạng thái Xác thực:** `SOURCE_VERIFIED`

---

## 1. MỤC ĐÍCH NGHIỆP VỤ & TỔNG QUAN
Giám sát danh tính người dùng, trạng thái bảo mật (2FA, PIN 6 số), lịch sử đăng nhập IP, các ví thuộc sở hữu và thực hiện các nghiệp vụ quản trị tài khoản (khóa/mở khóa, đổi quyền, cưỡng chế đổi mật khẩu).

---

## 2. KIỂM KÊ CÁC SECTION TRÊN TRANG

| Section ID | Tên Section | Component Source | Vị trí Bố cục | Trạng thái Hiển thị |
|---|---|---|---|---|
| `ADM-USR-SEC-01` | User Directory & Moderation Controls | `AdminUsersPage.tsx` lines 1-1343 | Thân trang & Modal | Luôn hiển thị |

---

## 3. DANH MỤC TÍNH NĂNG CHI TIẾT (FEATURE INVENTORY)

### Section ADM-USR-SEC-01: User Directory & Moderation Controls
- **Feature ID:** `ADM-USR-001`
  - **Tên:** Tra cứu và lọc danh sách người dùng (User Search & Filter Engine)
  - **Mục đích:** Tìm kiếm theo họ tên, email, số điện thoại; lọc theo vai trò (User/SystemAdmin) và trạng thái (Active/Suspended/Locked).
  - **Verification:** `SOURCE_VERIFIED`.

- **Feature ID:** `ADM-USR-002`
  - **Tên:** Khóa hoặc mở khóa tài khoản người dùng (Account Lock/Unlock)
  - **Mục đích:** Tạm dừng hoặc mở lại quyền truy cập khi phát hiện tài khoản có nghi vấn gian lận hoặc vi phạm chính sách.
  - **Verification:** `SOURCE_VERIFIED`.

- **Feature ID:** `ADM-USR-003`
  - **Tên:** Yêu cầu đổi mật khẩu ở lần đăng nhập tới (Force Password Change)
  - **Mục đích:** Đặt cờ bắt buộc người dùng thiết lập mật khẩu mới trong phiên truy cập kế tiếp vì lý do an toàn.
  - **Verification:** `SOURCE_VERIFIED`.

- **Feature ID:** `ADM-USR-004`
  - **Tên:** Phân quyền và thay đổi vai trò tài khoản (Role Assignment)
  - **Mục đích:** Nâng cấp hoặc hạ quyền người dùng giữa `User` và `SystemAdmin`.
  - **Verification:** `SOURCE_VERIFIED`.
