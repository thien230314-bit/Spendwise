# USER PAGE INVENTORY: CATEGORY BUDGETS (QUẢN LÝ NGÂN SÁCH)
**Page ID:** `USER-BUDGET`  
**Route:** `/budget` (Alias: `/budgets`)  
**Layout:** `AppShell` (User Layout)  
**File Source:** `src/features/budget/BudgetPage.tsx` (1344 dòng)  
**Trạng thái Xác thực:** `SOURCE_VERIFIED`

---

## 1. MỤC ĐÍCH NGHIỆP VỤ & TỔNG QUAN
Công cụ lập kế hoạch và kiểm soát chi tiêu theo tháng và theo từng danh mục (Ăn uống, Di chuyển, Mua sắm,...): đo lường tiến độ chi tiêu thời gian thực dựa trên các giao dịch thực tế phát sinh trong tháng, phát cảnh báo khi chi tiêu chạm ngưỡng nguy hiểm (>80% hoặc vượt 100%), hỗ trợ cấu hình chuyển tiếp số dư thừa sang tháng sau (Rollover Budget).

---

## 2. KIỂM KÊ CÁC SECTION TRÊN TRANG

| Section ID | Tên Section | Component Source | Vị trí Bố cục | Trạng thái Hiển thị |
|---|---|---|---|---|
| `BDG-SEC-01` | Dynamic Month Selector & Header | `BudgetPage.tsx` lines 420-480 | Đầu trang (Top bar) | Luôn hiển thị |
| `BDG-SEC-02` | Monthly Budget Overview Gauge | `BudgetPage.tsx` lines 490-560 | Hàng 1 (Tổng quan hạn mức) | Luôn hiển thị |
| `BDG-SEC-03` | Category Budgets Cards Grid | `BudgetPage.tsx` lines 570-750 | Hàng 2 (Danh sách ngân sách) | Luôn hiển thị |
| `BDG-SEC-04` | Budget Detail & Child Transactions | `BudgetPage.tsx` lines 760-900 | Modal / Drawer | Khi chọn 1 ngân sách |
| `BDG-SEC-05` | Create / Edit Budget Modal | `BudgetPage.tsx` lines 910-1200 | Modal Layer | Theo trigger |

---

## 3. DANH MỤC TÍNH NĂNG CHI TIẾT (FEATURE INVENTORY)

### Section BDG-SEC-01: Dynamic Month Selector
- **Feature ID:** `BUDGET-001`
  - **Tên:** Bộ chọn tháng linh hoạt (Dynamic Month Selector)
  - **Mục đích:** Chọn xem ngân sách của tháng hiện tại, các tháng trước hoặc lập kế hoạch cho tháng tới (`MM/YYYY` từ -6 tháng đến +6 tháng).
  - **API Call:** Tự động kích hoạt `loadBudgetsForMonth(year, month)` khi đổi tháng.
  - **Verification:** `SOURCE_VERIFIED`.

- **Feature ID:** `BUDGET-002`
  - **Tên:** Nút Thiết lập ngân sách mới (+ Tạo ngân sách)
  - **Mục đích:** Mở modal tạo hạn mức chi tiêu cho một danh mục chưa có ngân sách trong tháng.
  - **Verification:** `SOURCE_VERIFIED`.

### Section BDG-SEC-02: Monthly Budget Overview Gauge
- **Feature ID:** `BUDGET-003`
  - **Tên:** Thước đo tổng thể hạn mức tháng (Total Budget Gauge)
  - **Mục đích:** Tổng hợp: 1. Tổng ngân sách đã đặt, 2. Tổng số tiền đã chi thực tế, 3. Số dư còn lại khả dụng, 4. Tỷ lệ % đã tiêu toàn tháng.
  - **Data Calculation:** Lọc giao dịch hợp lệ bằng `isTransactionInSelectedMonth(tx, selectedMonth)`.
  - **Verification:** `SOURCE_VERIFIED`.

### Section BDG-SEC-03: Category Budgets Grid
- **Feature ID:** `BUDGET-004`
  - **Tên:** Thẻ ngân sách từng danh mục chi tiêu
  - **Mục đích:** Hiển thị tên danh mục, icon, hạn mức, số tiền đã tiêu, số tiền còn lại, thanh tiến độ đổi màu theo ngưỡng cảnh báo:
    - Xanh (`Emerald`): Tiến độ an toàn (<80%)
    - Vàng (`Amber`): Tiến độ cảnh báo (80% - 99%)
    - Đỏ (`Rose`): Đã vượt hạn mức (>=100%)
  - **Verification:** `SOURCE_VERIFIED`.

- **Feature ID:** `BUDGET-005A`
  - **Tên:** Chỉnh sửa hạn mức ngân sách danh mục (Edit Budget)
  - **Mục đích:** Điều chỉnh số tiền hạn mức, cờ chuyển tiếp số dư (rollover) và cờ thông báo ngưỡng 80%.
  - **Parent Feature ID:** `BUDGET-005`
  - **Trigger:** Click icon Sửa trên thẻ ngân sách.
  - **API Call:** `financialApiService.updateBudget / editBudget`.
  - **Verification:** `SOURCE_VERIFIED`.

- **Feature ID:** `BUDGET-005B`
  - **Tên:** Xóa ngân sách danh mục với xác thực PIN (Delete Budget)
  - **Mục đích:** Xóa bỏ ngân sách danh mục khỏi tháng được chọn sau khi xác thực PIN 6 số.
  - **Parent Feature ID:** `BUDGET-005`
  - **Trigger:** Click icon Thùng rác trên thẻ ngân sách và xác nhận PIN.
  - **API Call:** `financialApiService.deleteBudget(id, pin)`.
  - **Verification:** `SOURCE_VERIFIED`.

### Section BDG-SEC-04: Child Transactions Drill-Down
- **Feature ID:** `BUDGET-006`
  - **Tên:** Xem chi tiết giao dịch phát sinh trong danh mục ngân sách
  - **Mục đích:** Mở danh sách liệt kê chính xác các khoản chi tiêu thực tế đã trừ vào ngân sách này trong tháng đã chọn.
  - **Verification:** `SOURCE_VERIFIED`.

### Section BDG-SEC-05: Rollover & Alerts Configuration
- **Feature ID:** `BUDGET-007`
  - **Tên:** Bật/Tắt Chuyển tiếp ngân sách (Rollover Budget) & Nhận thông báo cảnh báo (Alerts)
  - **Mục đích:** Cho phép cộng dồn tiền dư tháng trước sang tháng sau hoặc gửi thông báo khi chạm mốc 80%.
  - **Verification:** `SOURCE_VERIFIED`.
