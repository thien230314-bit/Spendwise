# USER PAGE INVENTORY: FINANCIAL DASHBOARD (TỔNG QUAN)
**Page ID:** `USER-DASHBOARD`  
**Route:** `/dashboard`  
**Layout:** `AppShell` (User Layout)  
**File Source:** `src/features/dashboard/DashboardPage.tsx`  
**Subcomponents:** `src/features/dashboard/components/*`  
**Trạng thái Xác thực:** `BROWSER_VERIFIED` (Đã kiểm tra runtime trên Chrome DevTools MCP localhost:5173/dashboard)

---

## 1. MỤC ĐÍCH NGHIỆP VỤ & TỔNG QUAN
Màn hình trung tâm tổng hợp toàn bộ bức tranh tài chính cá nhân của người dùng SpendWise: hiển thị số dư khả dụng tổng thể, dòng tiền vào/ra, cảnh báo tài chính cần xử lý, 2 biểu đồ trực quan hóa dữ liệu 6 tháng gần nhất & cơ cấu chi tiêu tháng hiện tại, cùng 5 giao dịch vừa phát sinh.

---

## 2. KIỂM KÊ CÁC SECTION TRÊN TRANG

| Section ID | Tên Section | Component Source | Vị trí Bố cục | Trạng thái Hiển thị |
|---|---|---|---|---|
| `DASH-SEC-01` | Greeting Header | `components/GreetingHeader.tsx` | Đầu trang (Top full width) | Luôn hiển thị |
| `DASH-SEC-02` | Available Balance & Quick Actions | `components/PrimaryBalancePanel.tsx` | Hàng 1, Cột trái (Grid 6/12) | Luôn hiển thị |
| `DASH-SEC-03` | Financial KPI Metrics 2x2 | `components/FinancialSummaryCards.tsx` | Hàng 1, Cột phải (Grid 6/12) | Luôn hiển thị |
| `DASH-SEC-04` | Attention & Action Center Banner | `components/AttentionSection.tsx` | Hàng 2 (Full width) | Hiển thị khi `attentionItems.length > 0` hoặc loading |
| `DASH-SEC-05` | 6 Months Cash Flow Bar Chart | `components/LastSixMonthsBarChart.tsx` | Hàng 3, Cột trái (Grid 7/12) | Luôn hiển thị |
| `DASH-SEC-06` | Current Month Category Donut | `components/CurrentMonthCategoryDonut.tsx` | Hàng 3, Cột phải (Grid 5/12) | Luôn hiển thị |
| `DASH-SEC-07` | Recent Transactions Feed | `components/RecentTransactionsList.tsx` | Cuối trang (Bottom full width)| Luôn hiển thị |

---

## 3. DANH MỤC TÍNH NĂNG CHI TIẾT (FEATURE INVENTORY)

### Section DASH-SEC-01: Greeting Header
- **Feature ID:** `DASH-001`
  - **Tên:** Lời chào theo buổi trong ngày & Tên người dùng
  - **Mục đích:** Cá nhân hóa trải nghiệm đăng nhập theo thời gian thực (Chào buổi sáng / Buổi chiều / Buổi tối).
  - **UI Element:** Header text + Icon mặt trời/mặt trăng + Huy hiệu vai trò tài khoản.
  - **Trigger:** Tự động tính từ hàm `getTimeOfDay(locale)`.
  - **Data Dependency:** `currentUser.name`, `currentUser.email` từ `useAuth()`.
  - **Verification:** `BROWSER_VERIFIED` (Đã quan sát thấy: "Chào buổi chiều, SpendWise Reviewer").

- **Feature ID:** `DASH-002`
  - **Tên:** Nút ẩn/hiện số dư tài chính (Eye toggle)
  - **Mục đích:** Bảo vệ quyền riêng tư nơi công cộng, che toàn bộ số tiền trên màn hình thành dạng `••••••••`.
  - **UI Element:** Icon Button (`Eye` / `EyeOff`).
  - **Trigger:** Click nút mắt trên Header hoặc thẻ số dư.
  - **Handler:** `toggleVisibility()` từ `useBalanceVisibility()`.
  - **State:** Lưu vào `localStorage: spendwise_balance_hidden`.
  - **Verification:** `BROWSER_VERIFIED`.

### Section DASH-SEC-02: Available Balance & Quick Actions
- **Feature ID:** `DASH-003`
  - **Tên:** Thẻ Tổng số dư khả dụng (Total Balance Display)
  - **Mục đích:** Hiển thị tổng tài sản tức thời của tất cả ví cá nhân cộng lại.
  - **UI Element:** Card lớn nền xanh Emerald mờ, hiển thị số tiền `font-mono tabular-nums`.
  - **Data:** `dashboardSummary.totalBalance`, format theo locale `vi-VN` (₫) hoặc `en-US` ($).
  - **Verification:** `BROWSER_VERIFIED`.

- **Feature ID:** `DASH-004A`
  - **Tên:** Nút Thao tác nhanh: Ghi nhận Thu nhập (+ Thu nhập)
  - **Mục đích:** Mở modal ghi nhận giao dịch thu tiền nhanh vào ví mặc định.
  - **Parent Feature ID:** `DASH-004` (Đã phân tách để quản lý riêng rẽ hành vi)
  - **Trigger:** Click nút "Thu" (Màu xanh lục).
  - **Verification:** `BROWSER_VERIFIED`.

- **Feature ID:** `DASH-004B`
  - **Tên:** Nút Thao tác nhanh: Ghi nhận Chi tiêu (- Chi tiêu)
  - **Mục đích:** Mở modal ghi nhận khoản chi tiền nhanh từ ví.
  - **Parent Feature ID:** `DASH-004` (Đã phân tách để quản lý riêng rẽ hành vi)
  - **Trigger:** Click nút "Chi" (Màu đỏ/hồng).
  - **Verification:** `BROWSER_VERIFIED`.

- **Feature ID:** `DASH-004C`
  - **Tên:** Nút Thao tác nhanh: Chuyển tiền (Chuyển khoản)
  - **Mục đích:** Điều hướng trực tiếp tới trang Chuyển tiền cá nhân (`/transfer`).
  - **Parent Feature ID:** `DASH-004` (Đã phân tách để quản lý riêng rẽ hành vi)
  - **Trigger:** Click nút "Chuyển" (Màu xanh dương).
  - **Verification:** `BROWSER_VERIFIED`.

- **Feature ID:** `DASH-004D`
  - **Tên:** Nút Thao tác nhanh: Quỹ nhóm (Quản lý quỹ)
  - **Mục đích:** Điều hướng trực tiếp tới trung tâm Quỹ nhóm (`/groups`).
  - **Parent Feature ID:** `DASH-004` (Đã phân tách để quản lý riêng rẽ hành vi)
  - **Trigger:** Click nút "Nhóm" (Màu tím).
  - **Verification:** `BROWSER_VERIFIED`.

### Section DASH-SEC-03: Financial KPI Metrics 2x2
- **Feature ID:** `DASH-005`
  - **Tên:** Thẻ Chỉ số Thu nhập tháng này
  - **Mục đích:** Tổng số tiền thu về trong tháng lịch hiện tại.
  - **Data:** `dashboardSummary.totalIncome`.
  - **Verification:** `BROWSER_VERIFIED`.

- **Feature ID:** `DASH-006`
  - **Tên:** Thẻ Chỉ số Chi tiêu tháng này
  - **Mục đích:** Tổng số tiền đã tiêu trong tháng.
  - **Data:** `dashboardSummary.totalExpenses`.
  - **Verification:** `BROWSER_VERIFIED`.

- **Feature ID:** `DASH-007`
  - **Tên:** Thẻ Chỉ số Ngân sách khả dụng còn lại
  - **Mục đích:** Hạn mức ngân sách tháng chưa chi hết.
  - **Data:** `dashboardSummary.budgetRemaining`.
  - **Verification:** `BROWSER_VERIFIED`.

- **Feature ID:** `DASH-008`
  - **Tên:** Thẻ Chỉ số Quỹ nhóm & Nghĩa vụ góp quỹ
  - **Mục đích:** Tổng số dư quỹ trong các nhóm người dùng tham gia và số tiền đang nợ/cần đóng góp.
  - **Data:** `groupFundBalance` và `userPendingFundObligations` từ `useGroup()`.
  - **Verification:** `BROWSER_VERIFIED`.

- **Feature ID:** `DASH-009`
  - **Tên:** Thước đo Dòng tiền ròng (Net Cash Flow Bar)
  - **Mục đích:** Thể hiện trực quan tỷ lệ Thu nhập vs Chi tiêu và số dư ròng dôi dư trong tháng.
  - **Verification:** `BROWSER_VERIFIED`.

### Section DASH-SEC-04: Attention & Action Center
- **Feature ID:** `DASH-010`
  - **Tên:** Khối thông báo tài chính cần xử lý (Attention Section)
  - **Mục đích:** Cảnh báo ngân sách vượt ngưỡng (>80% hoặc >100%), mục tiêu sắp đến hạn, yêu cầu đóng quỹ nhóm quá hạn.
  - **UI Element:** Banner cảnh báo màu Amber kèm nút hành động trực tiếp.
  - **Data:** `attentionItems` từ `useFinancial()`.
  - **Verification:** `BROWSER_VERIFIED` (Đã quan sát thấy banner "Cần chú ý: 1 thông báo").

### Section DASH-SEC-05: 6 Months Trend Bar Chart
- **Feature ID:** `DASH-011`
  - **Tên:** Biểu đồ cột Thu/Chi 6 tháng gần nhất
  - **Mục đích:** Phân tích xu hướng biến động tài chính theo từng tháng.
  - **UI Elements:**
    - Nút toggle chuyển chế độ xem: **Tất cả (Cột đôi)** / **Chỉ Thu (Emerald)** / **Chỉ Chi (Rose)**.
    - Nhãn chỉ số: Tổng 6 tháng & Mức trung bình hàng tháng.
    - 6 cột biểu đồ SVG có tooltip chi tiết khi hover từng tháng.
  - **Algorithm:** Hàm `getLast6CalendarMonths()`, lọc theo `getTransactionEffectiveDate()`.
  - **Verification:** `BROWSER_VERIFIED`.

### Section DASH-SEC-06: Category Donut Chart
- **Feature ID:** `DASH-012`
  - **Tên:** Biểu đồ tròn Donut phân bổ danh mục tháng
  - **Mục đích:** Phân tích tỷ trọng chi tiêu hoặc thu nhập theo từng nhóm (Ăn uống, Đi lại, Mua sắm, Hóa đơn,...).
  - **UI Elements:**
    - Selector tab: Chi tiêu vs Thu nhập.
    - Đồ họa SVG Donut động với bảng màu tương phản.
    - Danh sách Legend bên cạnh hiển thị tên danh mục, tỷ lệ % và số tiền tương ứng.
  - **Verification:** `BROWSER_VERIFIED`.

### Section DASH-SEC-07: Recent Transactions Feed
- **Feature ID:** `DASH-013`
  - **Tên:** Bảng danh sách 5 giao dịch gần đây nhất
  - **Mục đích:** Theo dõi tức thời dòng tiền vừa phát sinh.
  - **UI Elements:** Icon danh mục, Tiêu đề giao dịch, Ví nguồn, Thời gian định dạng chuẩn, Dấu tiền (+/-) đổi màu, Link "Xem tất cả" dẫn tới `/transactions`.
  - **Verification:** `BROWSER_VERIFIED`.
