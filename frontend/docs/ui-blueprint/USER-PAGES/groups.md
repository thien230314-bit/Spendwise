# USER PAGE INVENTORY: SHARED GROUP FUNDS (QUỸ NHÓM & QUẢN LÝ QUỸ CHUNG)
**Page ID:** `USER-GROUPS`  
**Route:** `/groups`  
**Layout:** `AppShell` (User Layout)  
**File Source:** `src/features/groups/GroupsPage.tsx` (524 dòng)  
**Related Page:** `src/pages/GroupDetailPage.tsx` (`/groups/:groupId`)  
**Trạng thái Xác thực:** `SOURCE_VERIFIED`

---

## 1. MỤC ĐÍCH NGHIỆP VỤ & TỔNG QUAN
Nghiệp vụ tài chính xã hội (Social Finance): cho phép người dùng lập các nhóm chi tiêu chung (Nhóm bạn thân, Phòng trọ, Du lịch công ty, Gia đình), góp quỹ định kỳ (Fund Rounds), theo dõi số dư quỹ chung và chia tiền hóa đơn minh bạch.

---

## 2. KIỂM KÊ CÁC SECTION TRÊN TRANG

| Section ID | Tên Section | Component Source | Vị trí Bố cục | Trạng thái Hiển thị |
|---|---|---|---|---|
| `GRP-SEC-01` | Groups Summary Header & New Group CTA | `GroupsPage.tsx` lines 150-210 | Đầu trang (Top header) | Luôn hiển thị |
| `GRP-SEC-02` | My Groups Cards List | `GroupsPage.tsx` lines 220-380 | Thân trang (Grid nhóm) | Luôn hiển thị |
| `GRP-SEC-03` | Create New Group Modal | `GroupsPage.tsx` lines 390-480 | Modal Layer | Khi bấm Tạo nhóm |
| `GRP-SEC-04` | Group Detail Page Hub | `pages/GroupDetailPage.tsx` | Route `/groups/:groupId` | Khi bấm vào nhóm |
| `GRP-SEC-05` | Group Fund Rounds & Contributions | `pages/GroupDetailPage.tsx` | Tab Đóng quỹ / Góp quỹ | Khi xem chi tiết nhóm |
| `GRP-SEC-06` | Group Members & Invite Friends | `pages/GroupDetailPage.tsx` | Tab Thành viên | Khi xem chi tiết nhóm |

---

## 3. DANH MỤC TÍNH NĂNG CHI TIẾT (FEATURE INVENTORY)

### Section GRP-SEC-01 & 02: Groups List
- **Feature ID:** `GROUP-001`
  - **Tên:** Thống kê tổng quan quỹ nhóm của tôi
  - **Mục đích:** Hiển thị tổng số nhóm đang tham gia, tổng số tiền trong các quỹ chung.
  - **Data Dependency:** `useGroup().groups`.
  - **Verification:** `SOURCE_VERIFIED`.

- **Feature ID:** `GROUP-002`
  - **Tên:** Nút Tạo nhóm chi tiêu mới (+ Tạo nhóm)
  - **Mục đích:** Khởi tạo nhóm mới với Tên nhóm, Mục tiêu quỹ, Mô tả và Thêm thành viên từ Danh bạ bạn bè.
  - **API Call:** `groupApiService.createGroup(payload)`.
  - **Verification:** `SOURCE_VERIFIED`.

- **Feature ID:** `GROUP-003`
  - **Tên:** Thẻ danh sách nhóm (Group Card)
  - **Mục đích:** Hiển thị ảnh bìa/avatar nhóm, Tên nhóm, Số lượng thành viên, Số dư quỹ hiện tại, Vai trò người dùng (Trưởng nhóm / Thành viên), Nút điều hướng vào chi tiết.
  - **Verification:** `SOURCE_VERIFIED`.

### Section GRP-SEC-04 to 06: Group Detail Page (`/groups/:groupId`)
- **Feature ID:** `GROUP-004`
  - **Tên:** Quản lý đợt góp quỹ (Fund Rounds)
  - **Mục đích:** Trưởng nhóm tạo đợt thu tiền (ví dụ: "Tiền nhà tháng 10" - mỗi người 1.500.000 ₫). Hiển thị danh sách ai đã nộp, ai chưa nộp, nút Đóng tiền quỹ trích từ ví.
  - **API Calls:** `groupApiService.createFundRound`, `contributeToFundRound`.
  - **Verification:** `SOURCE_VERIFIED`.

- **Feature ID:** `GROUP-005`
  - **Tên:** Mời bạn bè tham gia nhóm (Invite Members)
  - **Mục đích:** Chọn bạn bè từ danh sách bạn bè SpendWise để gửi lời mời vào nhóm.
  - **Verification:** `SOURCE_VERIFIED`.

- **Feature ID:** `GROUP-006`
  - **Tên:** Quản lý chi tiêu từ Quỹ chung (Group Expense)
  - **Mục đích:** Ghi nhận khoản chi trả bằng tiền quỹ nhóm kèm hóa đơn chứng từ.
  - **Verification:** `SOURCE_VERIFIED`.
