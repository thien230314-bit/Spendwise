# ADMIN PAGE INVENTORY: AUDIT LOG (NHẬT KÝ KIỂM TOÁN AN TOÀN HỆ THỐNG)
**Page ID:** `ADMIN-AUDIT`  
**Route:** `/admin/audit`  
**Layout:** `AdminAppShell`  
**Quyền hạn:** `Role: SystemAdmin`  
**File Source:** `src/features/admin/AdminAuditPage.tsx`  
**Trạng thái Xác thực:** `SOURCE_VERIFIED`

---

## 1. MỤC ĐÍCH NGHIỆP VỤ & TỔNG QUAN
Ghi nhận chuỗi hành động không thể chối bỏ (Tamper-evident Audit Trail) của mọi quản trị viên và các tiến trình tự động: thay đổi cấu hình hệ thống, khóa tài khoản, phán quyết bồi hoàn, can thiệp số dư.

---

## 2. KIỂM KÊ CÁC SECTION TRÊN TRANG

| Section ID | Tên Section | Component Source | Vị trí Bố cục | Trạng thái Hiển thị |
|---|---|---|---|---|
| `ADM-ADT-SEC-01` | Tamper-Evident System Audit Trail | `AdminAuditPage.tsx` lines 1-376 | Thân trang toàn diện | Luôn hiển thị |

---

## 3. DANH MỤC TÍNH NĂNG CHI TIẾT (FEATURE INVENTORY)

### Section ADM-ADT-SEC-01: Tamper-Evident System Audit Trail
- **Feature ID:** `ADM-ADT-001`
  - **Tên:** Tìm kiếm và lọc sự kiện kiểm toán (Audit Trail Search & Stream)
  - **Mục đích:** Lọc sự kiện theo Admin thực hiện, loại hành động, thực thể bị tác động, mốc thời gian và địa chỉ IP.
  - **Verification:** `SOURCE_VERIFIED`.

- **Feature ID:** `ADM-ADT-002`
  - **Tên:** Xuất dữ liệu nhật ký kiểm toán (Export Audit Log CSV)
  - **Mục đích:** Kết xuất tệp tin phục vụ công tác thanh tra và tuân thủ bảo mật định kỳ.
  - **Verification:** `SOURCE_VERIFIED`.
