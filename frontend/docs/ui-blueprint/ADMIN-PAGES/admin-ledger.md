# ADMIN PAGE INVENTORY: GENERAL LEDGER (SỔ CÁI KÉP TOÀN HỆ THỐNG)
**Page ID:** `ADMIN-LEDGER`  
**Route:** `/admin/ledger`  
**Layout:** `AdminAppShell`  
**Quyền hạn:** `Role: SystemAdmin`  
**File Source:** `src/features/admin/AdminLedgerPage.tsx`  
**Trạng thái Xác thực:** `SOURCE_VERIFIED`

---

## 1. MỤC ĐÍCH NGHIỆP VỤ & TỔNG QUAN
Bảo đảm tính toàn vẹn của tiền tệ theo nguyên tắc kế toán kép (Double-Entry Bookkeeping: Nợ/Debit - Có/Credit). Giám sát phương trình kế toán cân bằng, danh sách bút toán sổ nhật ký chung và sơ đồ tài khoản hệ thống.

---

## 2. KIỂM KÊ CÁC SECTION TRÊN TRANG

| Section ID | Tên Section | Component Source | Vị trí Bố cục | Trạng thái Hiển thị |
|---|---|---|---|---|
| `ADM-LDG-SEC-01` | Double-Entry Verification & Journal Ledger | `AdminLedgerPage.tsx` lines 1-543 | Thân trang toàn diện | Luôn hiển thị |

---

## 3. DANH MỤC TÍNH NĂNG CHI TIẾT (FEATURE INVENTORY)

### Section ADM-LDG-SEC-01: Double-Entry Verification & Journal Ledger
- **Feature ID:** `ADM-LDG-001`
  - **Tên:** Kiểm tra cân bằng kế toán kép (Double-Entry Verification)
  - **Mục đích:** Đảm bảo `Tổng Nợ == Tổng Có`; cảnh báo đỏ tức thì nếu phát hiện lệch dù chỉ 1 đồng.
  - **Verification:** `SOURCE_VERIFIED`.

- **Feature ID:** `ADM-LDG-002`
  - **Tên:** Truy vấn nhật ký sổ cái chung (Journal Search)
  - **Mục đích:** Tra cứu các bút toán hạch toán kế toán theo mã tài khoản đối ứng và thời gian.
  - **Verification:** `SOURCE_VERIFIED`.

- **Feature ID:** `ADM-LDG-003`
  - **Tên:** Xuất báo cáo sổ cái (Export Ledger CSV/Excel)
  - **Mục đích:** Kết xuất dữ liệu kế toán phục vụ kiểm toán tài chính độc lập.
  - **Verification:** `SOURCE_VERIFIED`.
