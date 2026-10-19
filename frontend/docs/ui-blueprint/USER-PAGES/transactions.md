# USER PAGE INVENTORY: TRANSACTIONS (LỊCH SỬ & QUẢN LÝ GIAO DỊCH)
**Page ID:** `USER-TRANSACTIONS`  
**Route:** `/transactions` (Aliases: `/recurring`, `/rules`, `/categories`)  
**Layout:** `AppShell` (User Layout)  
**File Source:** `src/features/transactions/TransactionsPage.tsx` (3037 dòng)  
**Trạng thái Xác thực:** `SOURCE_VERIFIED`

---

## 1. MỤC ĐÍCH NGHIỆP VỤ & TỔNG QUAN
Trung tâm sổ cái giao dịch cá nhân toàn diện: xem lịch sử giao dịch phát sinh, tìm kiếm và lọc đa tiêu chí, tạo mới/sửa/xóa giao dịch, đính kèm hóa đơn chứng từ, quản lý các giao dịch định kỳ tự động (Recurring) và thiết lập quy tắc tự động phân loại giao dịch (Auto-Categorization Rules).

---

## 2. KIỂM KÊ CÁC SECTION TRÊN TRANG

| Section ID | Tên Section | Component Source | Vị trí Bố cục | Trạng thái Hiển thị |
|---|---|---|---|---|
| `TXN-SEC-01` | Header & 3 Main Tabs Navigation | `TransactionsPage.tsx` lines 380-450 | Đầu trang (Top full width) | Luôn hiển thị |
| `TXN-SEC-02` | Search & Multi-Filter Toolbar | `TransactionsPage.tsx` lines 460-650 | Tab Lịch sử (History) | Khi `activeMainTab === 'history'` |
| `TXN-SEC-03` | Comprehensive Transactions Table | `TransactionsPage.tsx` lines 660-950 | Tab Lịch sử (History) | Khi `activeMainTab === 'history'` |
| `TXN-SEC-04` | Recurring Transactions Management | `components/RecurringTransactionsTab.tsx` | Tab Định kỳ (Recurring) | Khi `activeMainTab === 'recurring'` |
| `TXN-SEC-05` | Auto-Categorization Rules Tab | `TransactionsPage.tsx` lines 1200-1450 | Tab Quy tắc (Rules) | Khi `activeMainTab === 'rules'` |
| `TXN-SEC-06` | Transaction Create/Edit Modal | `TransactionsPage.tsx` lines 1500-2100 | Modal Layer | Theo trigger |
| `TXN-SEC-07` | Evidence & Attachment Viewer Drawer | `TransactionsPage.tsx` lines 2200-2400 | Modal / Drawer | Theo trigger |

---

## 3. DANH MỤC TÍNH NĂNG CHI TIẾT (FEATURE INVENTORY)

### Section TXN-SEC-01: Header & Tabs Navigation
- **Feature ID:** `TXN-001A`
  - **Tên:** Chuyển Tab Lịch sử giao dịch (History Tab Switch)
  - **Mục đích:** Mở bảng lịch sử giao dịch toàn diện và các bộ lọc tìm kiếm.
  - **Parent Feature ID:** `TXN-001`
  - **Trigger:** Click tab "Lịch sử" (History).
  - **Deep-linking:** Đồng bộ query params URL `?tab=history`.
  - **Verification:** `SOURCE_VERIFIED`.

- **Feature ID:** `TXN-001B`
  - **Tên:** Chuyển Tab Giao dịch định kỳ (Recurring Tab Switch)
  - **Mục đích:** Kích hoạt quản lý lịch thanh toán giao dịch lặp lại.
  - **Parent Feature ID:** `TXN-001`
  - **Trigger:** Click tab "Định kỳ" (Recurring).
  - **Deep-linking:** Đồng bộ query params URL `?tab=recurring`.
  - **Verification:** `SOURCE_VERIFIED`.

- **Feature ID:** `TXN-001C`
  - **Tên:** Chuyển Tab Quy tắc phân loại tự động (Rules Tab Switch)
  - **Mục đích:** Kích hoạt quản lý các quy tắc nhận diện từ khóa và gán nhãn danh mục.
  - **Parent Feature ID:** `TXN-001`
  - **Trigger:** Click tab "Quy tắc" (Rules).
  - **Deep-linking:** Đồng bộ query params URL `?tab=rules`.
  - **Verification:** `SOURCE_VERIFIED`.

- **Feature ID:** `TXN-002`
  - **Tên:** Nút Thêm giao dịch mới (+ Ghi nhận thu/chi)
  - **Mục đích:** Kích hoạt modal thêm mới giao dịch với số tiền, danh mục, ví nguồn, ghi chú, hóa đơn đính kèm.
  - **Verification:** `SOURCE_VERIFIED`.

### Section TXN-SEC-02: Search & Multi-Filter Toolbar
- **Feature ID:** `TXN-003`
  - **Tên:** Thanh tìm kiếm đa năng (Omni Search)
  - **Mục đích:** Tìm kiếm tức thời theo tiêu đề, ghi chú, mã tham chiếu (`referenceCode`) hoặc đối tác giao dịch.
  - **Verification:** `SOURCE_VERIFIED`.

- **Feature ID:** `TXN-004`
  - **Tên:** Bộ lọc Loại giao dịch (Type Filter)
  - **Mục đích:** Lọc theo: Tất cả / Thu nhập (`income`) / Chi tiêu (`expense`) / Chuyển tiền (`transfer`) / Nạp ví (`bank_top_up`).
  - **Verification:** `SOURCE_VERIFIED`.

- **Feature ID:** `TXN-005`
  - **Tên:** Bộ lọc Ví tài khoản nguồn (Wallet Filter)
  - **Mục đích:** Xem riêng giao dịch của từng ví cá nhân.
  - **Verification:** `SOURCE_VERIFIED`.

- **Feature ID:** `TXN-006`
  - **Tên:** Bộ lọc Danh mục chi tiêu (Category Filter)
  - **Mục đích:** Lọc theo từng khoản chi (Ăn uống, Nhà ở, Mua sắm, Di chuyển,...).
  - **Verification:** `SOURCE_VERIFIED`.

- **Feature ID:** `TXN-007`
  - **Tên:** Bộ lọc Hạn mức số tiền (Amount Range Filter)
  - **Mục đích:** Lọc các khoản nhỏ (<100k), trung bình (100k-1M) hoặc khoản lớn (>1M).
  - **Verification:** `SOURCE_VERIFIED`.

### Section TXN-SEC-03: Comprehensive Transactions Table
- **Feature ID:** `TXN-008`
  - **Tên:** Bảng danh sách chi tiết giao dịch
  - **Mục đích:** Hiển thị ngày giờ, loại, tiêu đề, danh mục, ví, số tiền phân màu (+/-), nút Copy mã giao dịch, nút Sửa, nút Xóa.
  - **Verification:** `SOURCE_VERIFIED`.

- **Feature ID:** `TXN-009`
  - **Tên:** Phân trang và Tải thêm (Pagination & Load More)
  - **Mục đích:** Duy trì hiệu năng khi số lượng giao dịch lớn (`PAGE_SIZE = 10`).
  - **Verification:** `SOURCE_VERIFIED`.

### Section TXN-SEC-04: Recurring Transactions
- **Feature ID:** `TXN-010`
  - **Tên:** Quản lý giao dịch định kỳ (Recurring Management)
  - **Mục đích:** Thiết lập các khoản tiền thanh toán lặp lại (tiền nhà, internet, lương định kỳ) theo ngày trong tháng/tuần.
  - **API Calls:** `recurringService.getRecurring()`, `createRecurring()`, `deleteRecurring()`.
  - **Verification:** `SOURCE_VERIFIED`.

### Section TXN-SEC-05: Auto-Categorization Rules
- **Feature ID:** `TXN-011`
  - **Tên:** Quản lý quy tắc phân loại tự động (Classification Rules)
  - **Mục đích:** Tự động nhận diện từ khóa trong nội dung giao dịch ngân hàng để gán danh mục tương ứng (ví dụ: "GRAB" -> Di chuyển, "HIGHLANDS" -> Ăn uống).
  - **API Calls:** `transactionClassificationService`.
  - **Verification:** `SOURCE_VERIFIED`.

### Section TXN-SEC-06 & 07: Modals & Attachments
- **Feature ID:** `TXN-012`
  - **Tên:** Tải lên và Quản lý tệp chứng từ đính kèm (Evidence Attachments)
  - **Mục đích:** Đính kèm ảnh chụp hóa đơn, biên lai chuyển tiền (`image/png`, `image/jpeg`, `application/pdf`).
  - **Verification:** `SOURCE_VERIFIED`.
