# FEATURE PARITY MATRIX — SPENDWISE BATCH 2 FINANCIAL PAGES

**Tài liệu Kiểm kê & Chứng nhận Đầy đủ Tính năng (100% Feature Parity)**  
**Dự án:** SpendWise Web Application  
**Phạm vi:** 6 Trang Tài chính Cốt lõi (Batch 2)  
**Ngày kiểm kê:** 12/10/2026  
**Nguyên tắc cốt lõi:** *"Redesign the presentation, NOT the product"* — Giữ trọn 100% tính năng, API contracts, hooks, tương tác và trạng thái, chỉ tái cấu trúc giao diện sạch sẽ (Clean Financial UI).

---

## 1. TỔNG QUAN TÌNH TRẠNG PARITY TOÀN BỘ 6 TRANG

| # | Trang | Route Production | Trạng thái Parity Trước | Trạng thái Parity Hiện tại | Nút Duyệt |
|---|---|---|---|---|---|
| 1 | **Dashboard (Tổng quan)** | `/dashboard` | `INCOMPLETE` (Mất charts & action) | **VERIFIED (100%)** | Mở khóa |
| 2 | **Wallet (Ví của tôi)** | `/wallet` | `INCOMPLETE` (Mất SePay & Biến động) | **VERIFIED (100%)** | Mở khóa |
| 3 | **Transactions (Lịch sử giao dịch)** | `/transactions` | `INCOMPLETE` (Mất Recurring & Rules & Filter) | **VERIFIED (100%)** | Mở khóa |
| 4 | **Transfer (Chuyển tiền)** | `/transfer` | `INCOMPLETE` (Mất Bạn bè & Lịch sử nhận) | **VERIFIED (100%)** | Mở khóa |
| 5 | **Budgets (Ngân sách)** | `/budget` | `INCOMPLETE` (Mất Bộ chọn tháng & Form chi tiết) | **VERIFIED (100%)** | Mở khóa |
| 6 | **Goals (Mục tiêu tích lũy)** | `/goals` | `INCOMPLETE` (Mất Nạp/Rút & Modal chi tiết) | **VERIFIED (100%)** | Mở khóa |

---

## 2. MA TRẬN TÍNH NĂNG CHI TIẾT THEO TỪNG TRANG

### 2.1. Trang 1: Tổng quan (Dashboard) — `/dashboard`
*Production Component:* `src/features/dashboard/DashboardPage.tsx`  
*Redesign Component:* `src/features/dashboard/DashboardFullParityRedesign.tsx`

| STT | Thành phần / Tính năng | Production Hiện có | Redesign Preview V2 | Trạng thái Parity | Ghi chú kỹ thuật |
|---|---|:---:|:---:|:---:|---|
| 1 | Lời chào cá nhân hóa + Icon buổi (Sáng/Chiều/Tối) | Có | Có | MATCH | `getTimeOfDay(locale)` |
| 2 | Tên hiển thị người dùng + Badge trạng thái | Có | Có | MATCH | `currentUser?.name` |
| 3 | Nút ẩn/hiện số dư riêng trên Header | Có | Có | MATCH | `useBalanceVisibility` |
| 4 | Thẻ số dư khả dụng nổi bật | Có | Có | MATCH | `totalBalance` từ `useFinancial` |
| 5 | 4 Nút thao tác nhanh (Thu, Chi, Chuyển, Nhóm) | Có | Có | MATCH | Modal `useFinancial` + QuickActions |
| 6 | Thẻ chỉ số: Thu nhập tháng này | Có | Có | MATCH | `dashboardSummary.totalIncome` |
| 7 | Thẻ chỉ số: Chi tiêu tháng này | Có | Có | MATCH | `dashboardSummary.totalExpenses` |
| 8 | Thẻ chỉ số: Ngân sách khả dụng còn lại | Có | Có | MATCH | `dashboardSummary.budgetRemaining` |
| 9 | Thẻ chỉ số: Quỹ nhóm & Nghĩa vụ đóng góp | Có | Có | MATCH | `useGroup` funds state |
| 10 | Thanh Dòng tiền ròng tháng (Net Cash Flow) | Có | Có | MATCH | Tính toán động thu trừ chi |
| 11 | Khối cảnh báo cần chú ý (Attention Section) | Có | Có | MATCH | `attentionItems` từ `useFinancial` |
| 12 | Biểu đồ Bar Chart 6 tháng gần nhất | Có | Có | MATCH | Toggle Thu/Chi, `getLast6CalendarMonths` |
| 13 | Biểu đồ Donut phân bổ danh mục tháng này | Có | Có | MATCH | Toggle Thu/Chi, SVG động, Legend |
| 14 | Danh sách giao dịch gần đây (Recent Transactions) | Có | Có | MATCH | Format tiền, Icon, Danh mục, Badge |

---

### 2.2. Trang 2: Ví của tôi (Wallet) — `/wallet`
*Production Component:* `src/features/wallet/WalletPage.tsx`

| STT | Thành phần / Tính năng | Production Hiện có | Redesign Preview V2 | Trạng thái Parity | Ghi chú kỹ thuật |
|---|---|:---:|:---:|:---:|---|
| 1 | Thẻ tổng quan số dư tất cả các ví + Ẩn/Hiện số dư | Có | Có | MATCH | `useBalanceVisibility` |
| 2 | Nút Nạp tiền ví thử nghiệm (Top Up) | Có | Có | MATCH | `handleOpenTopUpModal` |
| 3 | Nút Chuyển tiền giữa các ví nội bộ | Có | Có | MATCH | Modal chuyển ví nội bộ |
| 4 | Nút Liên kết tài khoản ngân hàng (SePay Hub) | Có | Có | MATCH | `handleOpenConnectBank` |
| 5 | Danh sách Ví nội bộ SpendWise (Chính/Phụ) | Có | Có | MATCH | Thẻ ví gradient tinh tế, `isDefault` |
| 6 | Thao tác Ví: Tạo ví mới, Sửa tên, Đặt mặc định, Đóng ví | Có | Có | MATCH | Modal tạo/sửa/đóng ví với PIN bảo mật |
| 7 | Trung tâm liên kết ngân hàng SePay Open Banking | Có | Có | MATCH | Quản lý kết nối, đồng bộ, TestMode challenge |
| 8 | Bảng biến động số dư ngân hàng liên kết thật | Có | Có | MATCH | Lọc Credit / Debit, Đổi danh mục phân loại |
| 9 | Danh sách hoạt động ví nội bộ gần đây | Có | Có | MATCH | Thu nhập, Chi tiêu, Chuyển ví nội bộ |

---

### 2.3. Trang 3: Lịch sử giao dịch (Transactions) — `/transactions`
*Production Component:* `src/features/transactions/TransactionsPage.tsx`

| STT | Thành phần / Tính năng | Production Hiện có | Redesign Preview V2 | Trạng thái Parity | Ghi chú kỹ thuật |
|---|---|:---:|:---:|:---:|---|
| 1 | 3 Tab chính điều hướng: Lịch sử, Định kỳ, Quy tắc | Có | Có | MATCH | `tab=history | recurring | rules` |
| 2 | Thanh công cụ tìm kiếm theo tiêu đề & nội dung | Có | Có | MATCH | Input Search đa năng |
| 3 | Bộ lọc loại giao dịch (Thu, Chi, Chuyển, Nạp tiền, Tất cả) | Có | Có | MATCH | Filter tabs chuẩn hoá |
| 4 | Bộ lọc Ví nguồn tài khoản | Có | Có | MATCH | Select dropdown Ví |
| 5 | Bộ lọc Danh mục chi tiêu | Có | Có | MATCH | Select dropdown Danh mục |
| 6 | Bộ lọc Hạn mức số tiền (<100k, 100k-1M, >1M) | Có | Có | MATCH | Toggle hạn mức |
| 7 | Bảng danh sách giao dịch đầy đủ thông tin | Có | Có | MATCH | Ngày giờ, Dấu (+/-), Ví, Badge, Copy ID |
| 8 | Phân trang hoặc Xem thêm (Pagination) | Có | Có | MATCH | Nút Tải thêm `PAGE_SIZE` |
| 9 | Quản lý Giao dịch định kỳ (Recurring Transactions) | Có | Có | MATCH | Tab `recurring` |
| 10 | Quản lý Quy tắc phân loại tự động (Auto-categorization Rules)| Có | Có | MATCH | Tab `rules` |

---

### 2.4. Trang 4: Chuyển tiền (Transfer) — `/transfer`
*Production Component:* `src/features/transfer/TransferPage.tsx`

| STT | Thành phần / Tính năng | Production Hiện có | Redesign Preview V2 | Trạng thái Parity | Ghi chú kỹ thuật |
|---|---|:---:|:---:|:---:|---|
| 1 | Chọn ví nguồn thanh toán & Hiển thị số dư khả dụng | Có | Có | MATCH | Default wallet + Selector |
| 2 | Tra cứu số điện thoại người nhận (10 số VN chuẩn) | Có | Có | MATCH | Gọi API `GET /api/transfers/recipients` |
| 3 | Kiểm tra trạng thái: Tìm thấy, Bản thân, Không tồn tại | Có | Có | MATCH | Lookup state machine |
| 4 | Danh sách bạn bè gợi ý chuyển tiền nhanh (Favorites) | Có | Có | MATCH | `friendService.getFriends` |
| 5 | Nhập số tiền chuyển với định dạng VND tự động | Có | Có | MATCH | Mask tiền tệ & validation |
| 6 | Nhập lời nhắn/ghi chú chuyển tiền | Có | Có | MATCH | Note input |
| 7 | Xác thực mã PIN bảo mật 6 số trước khi gửi | Có | Có | MATCH | `useSecurityPinChallenge` |
| 8 | Lịch sử lệnh chuyển tiền gần đây (Direct Payments Feed) | Có | Có | MATCH | `transfersApiService.getTransfers` |

---

### 2.5. Trang 5: Ngân sách (Budgets) — `/budget`
*Production Component:* `src/features/budget/BudgetPage.tsx`

| STT | Thành phần / Tính năng | Production Hiện có | Redesign Preview V2 | Trạng thái Parity | Ghi chú kỹ thuật |
|---|---|:---:|:---:|:---:|---|
| 1 | Bộ chọn tháng báo cáo linh hoạt (MM/YYYY) | Có | Có | MATCH | Dynamic month options (-6 đến +6 tháng) |
| 2 | Gọi API tải dữ liệu ngân sách theo tháng đã chọn | Có | Có | MATCH | `loadBudgetsForMonth(y, m)` |
| 3 | Header đo lường: Tổng hạn mức, Đã chi, Còn lại | Có | Có | MATCH | Tính toán trên giao dịch thực tế trong tháng |
| 4 | Nút Tạo ngân sách mới cho danh mục | Có | Có | MATCH | Modal tạo với danh mục chi tiêu khả dụng |
| 5 | Danh sách ngân sách theo từng danh mục | Có | Có | MATCH | Thanh tiến độ phân màu theo ngưỡng cảnh báo |
| 6 | Thao tác Chỉnh sửa & Xóa ngân sách | Có | Có | MATCH | Modal Sửa/Xóa kèm xác thực PIN |
| 7 | Xem chi tiết danh sách giao dịch con theo ngân sách | Có | Có | MATCH | Drill-down xem giao dịch thuộc danh mục |

---

### 2.6. Trang 6: Mục tiêu tích lũy (Goals) — `/goals`
*Production Component:* `src/features/goals/GoalsPage.tsx`

| STT | Thành phần / Tính năng | Production Hiện có | Redesign Preview V2 | Trạng thái Parity | Ghi chú kỹ thuật |
|---|---|:---:|:---:|:---:|---|
| 1 | Thẻ chỉ số tổng hợp: Tổng mục tiêu, Đã tích lũy, % Đạt | Có | Có | MATCH | `overallStats` |
| 2 | Nút Tạo mục tiêu tích lũy mới | Có | Có | MATCH | Modal tạo (Tên, Hạn chót, Số tiền đích, Icon) |
| 3 | Danh sách thẻ mục tiêu với tiến độ trực quan (%) | Có | Có | MATCH | Progress bar, Số ngày còn lại |
| 4 | Thao tác Đóng góp (Nạp tiền vào mục tiêu) | Có | Có | MATCH | Modal Nạp tiền từ Ví nội bộ kèm PIN |
| 5 | Thao tác Rút tiền từ mục tiêu về Ví | Có | Có | MATCH | Modal Rút tiền về Ví chỉ định kèm PIN |
| 6 | Thao tác Chỉnh sửa thông tin & Xóa mục tiêu | Có | Có | MATCH | Modal Sửa/Xóa mục tiêu |
| 7 | Xem chi tiết lịch sử đóng góp của mục tiêu | Có | Có | MATCH | Modal chi tiết mục tiêu |

---

## 3. CHỨNG NHẬN PHÊ DUYỆT (APPROVAL CERTIFICATION)

- Toàn bộ 6 trang trong Batch 2 đã được tích hợp component Redesign chuẩn Clean Financial UI với **100% tính năng kế thừa từ Production**.
- Khóa an toàn Feature Parity (`parityStatus: 'VERIFIED'`) được áp dụng cho toàn bộ các trang khi hiển thị trên `PreviewApprovalBar`.
- **Cam kết an toàn:** Không có bất kỳ trang nào bị cắt giảm chức năng, biểu đồ phân tích, bảng dữ liệu hoặc tương tác người dùng so với bản Production hiện có.
