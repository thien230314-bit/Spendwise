# ADMIN PAGE INVENTORY: SYSTEM OVERVIEW (TỔNG QUAN VẬN HÀNH HỆ THỐNG)
**Page ID:** `ADMIN-OVERVIEW`  
**Route:** `/admin` hoặc `/admin/overview`  
**Layout:** `AdminAppShell`  
**Quyền hạn:** `Role: SystemAdmin`  
**File Source:** `src/features/admin/AdminOverviewPage.tsx`  
**Trạng thái Xác thực:** `SOURCE_VERIFIED`

---

## 1. MỤC ĐÍCH NGHIỆP VỤ & TỔNG QUAN
Bảng điều khiển trung tâm dành cho Quản trị viên hệ thống theo dõi tức thời sức khỏe nền tảng, lưu lượng giao dịch 24h, cảnh báo rủi ro gian lận mở, và thực thi các thao tác can thiệp vận hành (bật/tắt bảo trì, xóa cache hệ thống, đồng bộ đối soát).

---

## 2. KIỂM KÊ CÁC SECTION TRÊN TRANG

| Section ID | Tên Section | Component Source | Vị trí Bố cục | Trạng thái Hiển thị |
|---|---|---|---|---|
| `ADM-OVR-SEC-01` | System KPIs & Operations Bar | `AdminOverviewPage.tsx` lines 1-431 | Thân trang toàn diện | Luôn hiển thị |

---

## 3. DANH MỤC TÍNH NĂNG CHI TIẾT (FEATURE INVENTORY)

### Section ADM-OVR-SEC-01: System KPIs & Operations Bar
- **Feature ID:** `ADM-OVR-001`
  - **Tên:** Bảng chỉ số tổng quan hệ thống (System Stats Telemetry Feed)
  - **Mục đích:** Giám sát 4 chỉ số cốt lõi: Người dùng hoạt động, Khối lượng giao dịch 24h, Cảnh báo rủi ro mở, Trạng thái API Health.
  - **Data Dependency:** API giám sát vận hành `useFinancial()`.
  - **Verification:** `SOURCE_VERIFIED`.

- **Feature ID:** `ADM-OVR-002`
  - **Tên:** Nút kích hoạt chế độ bảo trì hệ thống (Maintenance Mode Switch)
  - **Mục đích:** Bật/tắt trạng thái bảo trì khẩn cấp toàn ứng dụng, chặn người dùng thường đăng nhập khi có sự cố.
  - **Verification:** `SOURCE_VERIFIED`.

- **Feature ID:** `ADM-OVR-003`
  - **Tên:** Thao tác xóa bộ nhớ đệm hệ thống (System Cache Purge)
  - **Mục đích:** Xóa Redis / Memory cache để đồng bộ dữ liệu tức thời khi có thay đổi cấu hình.
  - **Verification:** `SOURCE_VERIFIED`.
