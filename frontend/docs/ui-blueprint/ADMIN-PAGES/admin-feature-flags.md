# ADMIN PAGE INVENTORY: FEATURE FLAGS (QUẢN LÝ CỜ TÍNH NĂNG HỆ THỐNG)
**Page ID:** `ADMIN-FLAGS`  
**Route:** `/admin/feature-flags`  
**Layout:** `AdminAppShell`  
**Quyền hạn:** `Role: SystemAdmin`  
**File Source:** `src/features/admin/AdminFeatureFlagsPage.tsx`  
**Trạng thái Xác thực:** `SOURCE_VERIFIED`

---

## 1. MỤC ĐÍCH NGHIỆP VỤ & TỔNG QUAN
Cơ chế bật/tắt linh hoạt các module và luồng tính năng nâng cao mà không cần build hay deploy lại mã nguồn (Zero-downtime Release Controls). Hỗ trợ triển khai theo phần trăm người dùng và cô lập sự cố tức thời.

---

## 2. KIỂM KÊ CÁC SECTION TRÊN TRANG

| Section ID | Tên Section | Component Source | Vị trí Bố cục | Trạng thái Hiển thị |
|---|---|---|---|---|
| `ADM-FLG-SEC-01` | System Feature Flags Control Panel | `AdminFeatureFlagsPage.tsx` lines 1-310 | Thân trang toàn diện | Luôn hiển thị |

---

## 3. DANH MỤC TÍNH NĂNG CHI TIẾT (FEATURE INVENTORY)

### Section ADM-FLG-SEC-01: System Feature Flags Control Panel
- **Feature ID:** `ADM-FLG-001`
  - **Tên:** Bật/tắt cờ tính năng tức thời (Toggle Feature Flag)
  - **Mục đích:** Kích hoạt hoặc vô hiệu hóa các cờ: Trợ lý tài chính AI, Cổng ngân hàng SePay, Quỹ nhóm nâng cao, Giao diện tối V2.
  - **Verification:** `SOURCE_VERIFIED`.

- **Feature ID:** `ADM-FLG-002`
  - **Tên:** Điều chỉnh tỷ lệ phần trăm triển khai dần (Gradual Rollout Percentage)
  - **Mục đích:** Thiết lập tỷ lệ % đối tượng người dùng được tiếp cận tính năng thử nghiệm trước khi phát hành toàn diện.
  - **Verification:** `SOURCE_VERIFIED`.
