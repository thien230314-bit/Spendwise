# USER PAGE INVENTORY: SAVINGS GOALS (MỤC TIÊU TÍCH LŨY)
**Page ID:** `USER-GOALS`  
**Route:** `/goals`  
**Layout:** `AppShell` (User Layout)  
**File Source:** `src/features/goals/GoalsPage.tsx` (1721 dòng)  
**Trạng thái Xác thực:** `SOURCE_VERIFIED`

---

## 1. MỤC ĐÍCH NGHIỆP VỤ & TỔNG QUAN
Công cụ quản lý tài chính tích lũy hướng tới mục tiêu tương lai (Mua xe, Mua nhà, Quỹ khẩn cấp, Du lịch, Thiết bị công nghệ): thiết lập số tiền mục tiêu và hạn chót (Deadline), theo dõi tỷ lệ hoàn thành (%), hỗ trợ nạp tiền tích lũy từ ví nội bộ và rút tiền từ quỹ mục tiêu về ví khi cần sử dụng.

---

## 2. KIỂM KÊ CÁC SECTION TRÊN TRANG

| Section ID | Tên Section | Component Source | Vị trí Bố cục | Trạng thái Hiển thị |
|---|---|---|---|---|
| `GOAL-SEC-01` | Savings Overview & Action Header | `GoalsPage.tsx` lines 350-410 | Đầu trang (Top header) | Luôn hiển thị |
| `GOAL-SEC-02` | Active Goals Cards Grid | `GoalsPage.tsx` lines 420-650 | Thân trang (Grid thẻ mục tiêu) | Luôn hiển thị |
| `GOAL-SEC-03` | Goal Detail & Progress Drill-Down | `GoalsPage.tsx` lines 660-820 | Modal / Drawer | Khi chọn 1 mục tiêu |
| `GOAL-SEC-04` | Create / Edit Goal Modal | `GoalsPage.tsx` lines 830-1080 | Modal Layer | Theo trigger |
| `GOAL-SEC-05` | Contribute (Nạp tiền tích lũy) Modal | `GoalsPage.tsx` lines 1090-1350 | Modal Layer | Theo trigger |
| `GOAL-SEC-06` | Withdraw (Rút tiền về ví) Modal | `GoalsPage.tsx` lines 1360-1650 | Modal Layer | Theo trigger |

---

## 3. DANH MỤC TÍNH NĂNG CHI TIẾT (FEATURE INVENTORY)

### Section GOAL-SEC-01: Savings Overview Header
- **Feature ID:** `GOAL-001`
  - **Tên:** Thống kê tổng quan tích lũy (Overall Savings Metrics)
  - **Mục đích:** Thống kê: 1. Tổng số mục tiêu đang chạy, 2. Tổng số tiền đã gom được, 3. Tổng số tiền đích, 4. Tỷ lệ % hoàn thành toàn diện.
  - **Data Dependency:** `overallStats` tính từ `goals` mảng.
  - **Verification:** `SOURCE_VERIFIED`.

- **Feature ID:** `GOAL-002`
  - **Tên:** Nút Tạo mục tiêu mới (+ Mục tiêu)
  - **Mục đích:** Kích hoạt modal khởi tạo mục tiêu tích lũy mới.
  - **Verification:** `SOURCE_VERIFIED`.

### Section GOAL-SEC-02: Active Goals Cards Grid
- **Feature ID:** `GOAL-003`
  - **Tên:** Thẻ hiển thị tiến độ từng mục tiêu
  - **Mục đích:** Hiển thị tên mục tiêu, icon đại diện (Laptop, Xe hơi, Ngôi nhà, Máy bay,...), số tiền hiện tại / đích, thanh tiến độ %, đếm ngược số ngày còn lại đến hạn chót (`daysLeft`).
  - **Verification:** `SOURCE_VERIFIED`.

- **Feature ID:** `GOAL-004`
  - **Tên:** Nút Nạp tiền tích lũy (Contribute Action)
  - **Mục đích:** Trích tiền từ ví nội bộ SpendWise chuyển vào quỹ mục tiêu này.
  - **Verification:** `SOURCE_VERIFIED`.

- **Feature ID:** `GOAL-005`
  - **Tên:** Nút Rút tiền về ví (Withdraw Action)
  - **Mục đích:** Rút một phần hoặc toàn bộ số tiền đã tích lũy trong mục tiêu về ví chỉ định.
  - **Verification:** `SOURCE_VERIFIED`.

- **Feature ID:** `GOAL-006A`
  - **Tên:** Chỉnh sửa thông tin mục tiêu tích lũy (Edit Goal)
  - **Mục đích:** Cập nhật hạn chót, đổi tên mục tiêu hoặc danh mục liên kết.
  - **Parent Feature ID:** `GOAL-006`
  - **Trigger:** Click icon Sửa trên thẻ mục tiêu.
  - **API Call:** `financialApiService.updateGoal / editGoal`.
  - **Verification:** `SOURCE_VERIFIED`.

- **Feature ID:** `GOAL-006B`
  - **Tên:** Xóa mục tiêu tích lũy với xác thực PIN (Delete Goal)
  - **Mục đích:** Xóa bỏ hoàn toàn mục tiêu khỏi hệ thống (yêu cầu số dư phải rút về 0).
  - **Parent Feature ID:** `GOAL-006`
  - **Trigger:** Click icon Thùng rác trên thẻ mục tiêu và xác nhận PIN 6 số.
  - **API Call:** `financialApiService.deleteGoal(id, pin)`.
  - **Verification:** `SOURCE_VERIFIED`.

### Section GOAL-SEC-04 to 06: Modals & Security PIN
- **Feature ID:** `GOAL-007`
  - **Tên:** Xác thực mã PIN 6 số cho thao tác Nạp / Rút tiền mục tiêu
  - **Mục đích:** Ngăn chặn việc chuyển dịch tiền khỏi quỹ mà không có sự đồng ý của chủ tài khoản.
  - **Handler:** `challengePinWithValue(pin)`.
  - **API Calls:** `contributeToGoal(id, amount, walletId, pin)`, `withdrawFromGoal(id, amount, walletId, pin)`.
  - **Verification:** `SOURCE_VERIFIED`.
