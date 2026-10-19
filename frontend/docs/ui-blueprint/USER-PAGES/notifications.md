# USER PAGE INVENTORY: NOTIFICATION CENTER (TRUNG TÂM THÔNG BÁO)
**Page ID:** `USER-NOTIFICATIONS`  
**Route:** `/notifications`  
**Layout:** `AppShell` (User Layout)  
**File Source:** `src/features/notifications/NotificationsPage.tsx` (931 dòng)  
**Subcomponents:** `src/features/notifications/AccountPrefModal.tsx`  
**Trạng thái Xác thực:** `SOURCE_VERIFIED`

---

## 1. MỤC ĐÍCH NGHIỆP VỤ & TỔNG QUAN
Trung tâm tiếp nhận và phân loại toàn bộ thông báo hệ thống của người dùng SpendWise: thông báo biến động số dư, cảnh báo an toàn bảo mật, lời mời tham gia quỹ nhóm, yêu cầu kết bạn, nhắc nhở ngân sách và cho phép tùy biến kênh nhận thông báo (In-App, Email, Push).

---

## 2. KIỂM KÊ CÁC SECTION TRÊN TRANG

| Section ID | Tên Section | Component Source | Vị trí Bố cục | Trạng thái Hiển thị |
|---|---|---|---|---|
| `NOTIF-SEC-01` | Header & Bulk Actions Bar | `NotificationsPage.tsx` lines 180-250 | Đầu trang (Top header) | Luôn hiển thị |
| `NOTIF-SEC-02` | Notification Categories Filter Tabs | `NotificationsPage.tsx` lines 260-320 | Hàng 1 (Tabs lọc danh mục) | Luôn hiển thị |
| `NOTIF-SEC-03` | Notifications Feed List | `NotificationsPage.tsx` lines 330-650 | Thân trang (Danh sách thông báo) | Luôn hiển thị |
| `NOTIF-SEC-04` | Notification Detail Modal | `NotificationsPage.tsx` lines 660-750 | Modal Layer | Khi click vào 1 thông báo |
| `NOTIF-SEC-05` | Notification Channels Preferences Modal| `AccountPrefModal.tsx` | Modal Layer | Khi click Cấu hình thông báo |

---

## 3. DANH MỤC TÍNH NĂNG CHI TIẾT (FEATURE INVENTORY)

### Section NOTIF-SEC-01 & 02: Header & Filters
- **Feature ID:** `NOTIF-001`
  - **Tên:** Đánh dấu đã đọc tất cả (Mark All as Read)
  - **Mục đích:** Xóa toàn bộ trạng thái chưa đọc của danh sách thông báo chỉ bằng 1 click.
  - **API Call:** `notificationsApiService.markAllAsRead()`.
  - **Verification:** `SOURCE_VERIFIED`.

- **Feature ID:** `NOTIF-002`
  - **Tên:** Xóa tất cả thông báo đã đọc (Clear Read Notifications)
  - **Mục đích:** Dọn dẹp hòm thư thông báo cho gọn gàng.
  - **Verification:** `SOURCE_VERIFIED`.

- **Feature ID:** `NOTIF-003`
  - **Tên:** Bộ lọc theo Loại thông báo (Type Filter Tabs)
  - **Mục đích:** Lọc theo: Tất cả (`All`), Giao dịch (`Transactions`), Bảo mật (`Security`), Quỹ nhóm (`Groups`), Bạn bè (`Friends`), Hệ thống (`System`).
  - **Verification:** `SOURCE_VERIFIED`.

### Section NOTIF-SEC-03 & 04: Feed & Interactions
- **Feature ID:** `NOTIF-004`
  - **Tên:** Đánh dấu đã đọc từng thông báo & Điều hướng thông minh
  - **Mục đích:** Click vào thông báo giao dịch tự động dẫn đến chi tiết giao dịch đó; click thông báo kết bạn dẫn sang Friends Page.
  - **Verification:** `SOURCE_VERIFIED`.

- **Feature ID:** `NOTIF-005`
  - **Tên:** Xóa từng thông báo riêng lẻ (Delete Single Notification)
  - **Mục đích:** Loại bỏ thông báo không quan trọng.
  - **API Call:** `notificationsApiService.deleteNotification(id)`.
  - **Verification:** `SOURCE_VERIFIED`.

### Section NOTIF-SEC-05: Channels Preferences
- **Feature ID:** `NOTIF-006`
  - **Tên:** Tùy chỉnh kênh nhận thông báo (Notification Preferences)
  - **Mục đích:** Bật/Tắt nhận email cho biến động số dư lớn, bật/tắt thông báo đẩy trình duyệt, tần suất thông báo tổng kết tuần.
  - **Component:** `AccountPrefModal.tsx`.
  - **Verification:** `SOURCE_VERIFIED`.
