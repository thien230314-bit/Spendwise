# USER PAGE INVENTORY: WALLET & BANK HUB (VÍ CỦA TÔI & NGÂN HÀNG)
**Page ID:** `USER-WALLET`  
**Route:** `/wallet` (Alias: `/wallets`)  
**Layout:** `AppShell` (User Layout)  
**File Source:** `src/features/wallet/WalletPage.tsx`  
**Trạng thái Xác thực:** `SOURCE_VERIFIED`

---

## 1. MỤC ĐÍCH NGHIỆP VỤ & TỔNG QUAN
Trung tâm quản lý nguồn vốn và cổng kết nối tài chính: quản lý các tài khoản ví nội bộ SpendWise (Ví chính, Ví chi tiêu, Ví tiết kiệm); kết nối tài khoản ngân hàng thực tế qua SePay Open Banking; quản lý dòng tiền biến động ngân hàng và nạp tiền (Top Up) vào ví.

---

## 2. KIỂM KÊ CÁC SECTION TRÊN TRANG

| Section ID | Tên Section | Component Source | Vị trí Bố cục | Trạng thái Hiển thị |
|---|---|---|---|---|
| `WALLET-SEC-01` | Balance & Action Banner | `WalletPage.tsx` lines 1050-1127 | Đầu trang (Top banner) | Luôn hiển thị |
| `WALLET-SEC-02` | Internal Wallets Deck | `WalletPage.tsx` lines 1129-1267 | Hàng 2 (Grid thẻ ví) | Luôn hiển thị |
| `WALLET-SEC-03` | SePay Open Banking Hub | `WalletPage.tsx` lines 1269-1528 | Hàng 3 (Panel liên kết) | Luôn hiển thị |
| `WALLET-SEC-04` | Bank Transactions Feed | `WalletPage.tsx` lines 1530-1676 | Hàng 4, Cột trái (Grid 7/12) | Luôn hiển thị |
| `WALLET-SEC-05` | Internal Wallet Activities | `WalletPage.tsx` lines 1678-1730 | Hàng 4, Cột phải (Grid 5/12) | Luôn hiển thị |
| `WALLET-SEC-06` | Modals & Pin Verification | `WalletPage.tsx` lines 1740-2660 | Lớp phủ (Modal Layer) | Theo trigger người dùng |

---

## 3. DANH MỤC TÍNH NĂNG CHI TIẾT (FEATURE INVENTORY)

### Section WALLET-SEC-01: Balance & Action Banner
- **Feature ID:** `WALLET-001`
  - **Tên:** Hiển thị Tổng số dư khả dụng các ví
  - **Mục đích:** Thống kê tổng tiền có sẵn trên tất cả các ví nội bộ.
  - **UI Element:** Display text `text-3xl font-bold font-mono` kèm nút ẩn/hiện mắt (`toggleVisibility`).
  - **Verification:** `SOURCE_VERIFIED`.

- **Feature ID:** `WALLET-002`
  - **Tên:** Nút Nạp tiền Ví (Top-Up Action)
  - **Mục đích:** Kích hoạt modal nạp tiền từ tài khoản ngân hàng TestMode vào ví nội bộ.
  - **Trigger:** Click nút "Nạp tiền" (Blue button).
  - **Handler:** `handleOpenTopUpModal()`.
  - **Verification:** `SOURCE_VERIFIED`.

- **Feature ID:** `WALLET-003`
  - **Tên:** Nút Chuyển tiền giữa các ví nội bộ (Internal Transfer)
  - **Mục đích:** Chuyển dịch số dư giữa 2 ví thuộc sở hữu của cùng một người dùng (Ví A -> Ví B) không mất phí.
  - **Trigger:** Click nút "Chuyển giữa các ví".
  - **Handler:** Mở modal chuyển ví nội bộ.
  - **Verification:** `SOURCE_VERIFIED`.

### Section WALLET-SEC-02: Internal Wallets Deck
- **Feature ID:** `WALLET-004`
  - **Tên:** Thẻ danh sách Ví nội bộ SpendWise
  - **Mục đích:** Hiển thị danh sách từng ví với tên, số tài khoản, số dư, huy hiệu Mặc định.
  - **UI Elements:** Thẻ ví gradient hiện đại, nút Đặt mặc định, nút Sửa tên ví, nút Đóng/Xóa ví.
  - **Verification:** `SOURCE_VERIFIED`.

- **Feature ID:** `WALLET-005A`
  - **Tên:** Thao tác tạo ví nội bộ mới (+ Tạo ví)
  - **Mục đích:** Khởi tạo thêm tài khoản ví phụ (ví dụ: Ví tiết kiệm, Ví ăn uống).
  - **Parent Feature ID:** `WALLET-005`
  - **Trigger:** Click nút "Tạo ví mới" mở modal tạo ví.
  - **API Call:** `financialApiService.createWallet({ name, initialBalance, currency })`.
  - **Verification:** `SOURCE_VERIFIED`.

- **Feature ID:** `WALLET-005B`
  - **Tên:** Thao tác đổi tên và chỉnh sửa thông tin ví
  - **Mục đích:** Cập nhật tên hiển thị của tài khoản ví cá nhân.
  - **Parent Feature ID:** `WALLET-005`
  - **Trigger:** Click icon chỉnh sửa trên thẻ ví.
  - **API Call:** `financialApiService.updateWallet(walletId, { name })`.
  - **Verification:** `SOURCE_VERIFIED`.

- **Feature ID:** `WALLET-005C`
  - **Tên:** Thao tác đặt làm ví mặc định (Set as Default)
  - **Mục đích:** Chọn ví nhận tiền mặc định và trích tiền ưu tiên khi thanh toán.
  - **Parent Feature ID:** `WALLET-005`
  - **Trigger:** Click nút "Đặt làm mặc định" trên thẻ ví.
  - **API Call:** `financialApiService.setDefaultWallet(walletId)`.
  - **Verification:** `SOURCE_VERIFIED`.

- **Feature ID:** `WALLET-005D`
  - **Tên:** Thao tác đóng/xóa ví nội bộ (Close Wallet)
  - **Mục đích:** Đóng tài khoản ví khi không còn nhu cầu sử dụng.
  - **Parent Feature ID:** `WALLET-005`
  - **Validation & Security:** Chỉ cho phép đóng ví khi số dư đã về đúng 0 và bắt buộc xác thực mã PIN bảo mật 6 số.
  - **API Call:** `financialApiService.deleteWallet(walletId, { pin })`.
  - **Verification:** `SOURCE_VERIFIED`.

### Section WALLET-SEC-03: SePay Open Banking Hub
- **Feature ID:** `WALLET-006`
  - **Tên:** Trung tâm quản lý liên kết SePay Open Banking
  - **Mục đích:** Cho phép liên kết tài khoản ngân hàng thật (VCB, TCB, ACB, MB, BIDV, VPB, VietinBank) hoặc môi trường TestMode.
  - **UI Elements:** Thẻ ngân hàng liên kết, huy hiệu trạng thái (Active, Pending, Unlinked, Inactive), thời gian đồng bộ lần cuối.
  - **Verification:** `SOURCE_VERIFIED`.

- **Feature ID:** `WALLET-007A`
  - **Tên:** Đồng bộ kết nối ngân hàng tức thời (Sync Connection)
  - **Mục đích:** Kéo các giao dịch phát sinh mới nhất từ SePay về hệ thống và cập nhật thời gian đồng bộ cuối.
  - **Parent Feature ID:** `WALLET-007`
  - **Trigger:** Click nút "Đồng bộ ngay" (RefreshCw icon).
  - **API Call:** `bankApiService.syncBankConnection(id)`.
  - **Verification:** `SOURCE_VERIFIED`.

- **Feature ID:** `WALLET-007B`
  - **Tên:** Hủy liên kết tài khoản ngân hàng (Unlink Bank)
  - **Mục đích:** Ngắt kết nối đồng bộ Open Banking với ngân hàng đã liên kết sau khi xác nhận cảnh báo.
  - **Parent Feature ID:** `WALLET-007`
  - **Trigger:** Click nút "Hủy liên kết" (Unlink icon) và xác nhận dialog.
  - **API Call:** `bankApiService.unlinkBankConnection(id)`.
  - **Verification:** `SOURCE_VERIFIED`.

- **Feature ID:** `WALLET-007C`
  - **Tên:** Tái kích hoạt / Liên kết lại ngân hàng (Reactivate / Re-link Bank)
  - **Mục đích:** Khởi tạo phiên cấp quyền lại khi tài khoản ngân hàng ở trạng thái ngắt kết nối hoặc gặp lỗi.
  - **Parent Feature ID:** `WALLET-007`
  - **Trigger:** Click nút "Liên kết lại" (ExternalLink icon).
  - **API Call:** `bankApiService.createBankLinkSession`.
  - **Verification:** `SOURCE_VERIFIED`.

### Section WALLET-SEC-04: Bank Transactions Feed
- **Feature ID:** `WALLET-008`
  - **Tên:** Bảng biến động số dư ngân hàng liên kết
  - **Mục đích:** Lịch sử sao kê tự động kéo về từ cổng Open Banking.
  - **UI Elements:** Bộ lọc hướng tiền (Tất cả / Tiền vào - Credit / Tiền ra - Debit), Mã bút toán, Nội dung chuyển khoản ngân hàng, Nút phân loại danh mục chi tiêu.
  - **Verification:** `SOURCE_VERIFIED`.

### Section WALLET-SEC-05: Internal Wallet Activities
- **Feature ID:** `WALLET-009`
  - **Tên:** Danh sách hoạt động giao dịch ví nội bộ
  - **Mục đích:** Ghi nhận các đợt nạp tiền, rút tiền và luân chuyển giữa các ví cá nhân.
  - **UI Elements:** Icon loại giao dịch, Ví đích, Thời gian, Số tiền tương ứng.
  - **Verification:** `SOURCE_VERIFIED`.
