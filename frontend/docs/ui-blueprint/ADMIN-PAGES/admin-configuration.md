# ADMIN PAGE INVENTORY: SYSTEM CONFIGURATION (CẤU HÌNH THAM SỐ TOÀN HỆ THỐNG)
**Page ID:** `ADMIN-CONFIG`  
**Route:** `/admin/configuration`  
**Layout:** `AdminAppShell`  
**Quyền hạn:** `Role: SystemAdmin`  
**File Source:** `src/features/admin/AdminConfigurationPage.tsx`  
**Trạng thái Xác thực:** `SOURCE_VERIFIED`

---

## 1. MỤC ĐÍCH NGHIỆP VỤ & TỔNG QUAN
Trung tâm thiết lập các chính sách vận hành tài chính, hạn mức rủi ro chuyển tiền, chính sách xác thực và bảo mật phiên truy cập, cùng chức năng phát thông báo khẩn cấp toàn trang.

---

## 2. KIỂM KÊ CÁC SECTION TRÊN TRANG

| Section ID | Tên Section | Component Source | Vị trí Bố cục | Trạng thái Hiển thị |
|---|---|---|---|---|
| `ADM-CFG-SEC-01` | Global Financial Limits & Policies Editor | `AdminConfigurationPage.tsx` lines 1-970 | Thân trang toàn diện | Luôn hiển thị |

---

## 3. DANH MỤC TÍNH NĂNG CHI TIẾT (FEATURE INVENTORY)

### Section ADM-CFG-SEC-01: Global Financial Limits & Policies Editor
- **Feature ID:** `ADM-CFG-001`
  - **Tên:** Cập nhật hạn mức tài chính và chính sách bảo mật (Update Global Policies)
  - **Mục đích:** Cấu hình hạn mức chuyển tiền giao dịch đơn, hạn mức ngày, số lần thử PIN tối đa trước khi khóa tạm thời, và thời hạn sống JWT Token.
  - **Verification:** `SOURCE_VERIFIED`.

- **Feature ID:** `ADM-CFG-002`
  - **Tên:** Soạn thảo và phát thanh thông báo khẩn cấp (Emergency Announcement Banner)
  - **Mục đích:** Bật/tắt thanh banner cảnh báo màu đỏ hoặc vàng hiển thị trên mọi trang cho toàn bộ người dùng.
  - **Verification:** `SOURCE_VERIFIED`.
