# USER PAGE INVENTORY: DIRECT TRANSFER (CHUYỂN TIỀN TRỰC TIẾP)
**Page ID:** `USER-TRANSFER`  
**Route:** `/transfer` (Alias: `/transfers`)  
**Layout:** `AppShell` (User Layout)  
**File Source:** `src/features/transfer/TransferPage.tsx` (1125 dòng)  
**Trạng thái Xác thực:** `SOURCE_VERIFIED`

---

## 1. MỤC ĐÍCH NGHIỆP VỤ & TỔNG QUAN
Chức năng chuyển tiền trực tiếp an toàn giữa các thành viên SpendWise: hỗ trợ tra cứu thông tin người nhận thời gian thực qua số điện thoại 10 số Việt Nam chuẩn, gợi ý danh sách bạn bè thân thiết, form chuyển tiền 3 bước rõ ràng, xác thực bảo mật 2 lớp qua mã PIN 6 số và bảng theo dõi lịch sử các lệnh chuyển tiền gần đây (`BankDirectPaymentDto`).

---

## 2. KIỂM KÊ CÁC SECTION TRÊN TRANG

| Section ID | Tên Section | Component Source | Vị trí Bố cục | Trạng thái Hiển thị |
|---|---|---|---|---|
| `XFER-SEC-01` | Source Account & Balance Panel | `TransferPage.tsx` lines 320-380 | Form chuyển tiền (Bước 1) | Luôn hiển thị |
| `XFER-SEC-02` | Recipient Phone Lookup & Friend Quick Pick | `TransferPage.tsx` lines 390-520 | Form chuyển tiền (Bước 2) | Luôn hiển thị |
| `XFER-SEC-03` | Amount & Transfer Note Input | `TransferPage.tsx` lines 530-610 | Form chuyển tiền (Bước 3) | Luôn hiển thị |
| `XFER-SEC-04` | Transaction Review & PIN 6-Digit Challenge | `TransferPage.tsx` lines 620-720 | Modal / Review Step | Khi bấm Tiếp tục |
| `XFER-SEC-05` | Transfer Success Receipt Card | `TransferPage.tsx` lines 730-840 | Màn hình thành công | Khi hoàn tất lệnh |
| `XFER-SEC-06` | Recent Direct Payments History | `TransferPage.tsx` lines 850-1050 | Bên dưới Form hoặc Cột phải | Luôn hiển thị |

---

## 3. DANH MỤC TÍNH NĂNG CHI TIẾT (FEATURE INVENTORY)

### Section XFER-SEC-01: Source Wallet Selection
- **Feature ID:** `TRANSFER-001`
  - **Tên:** Lựa chọn ví nguồn trích tiền & Hiển thị số dư khả dụng
  - **Mục đích:** Chọn ví thanh toán (ưu tiên ví Mặc định), kiểm tra số dư tức thời trước khi thực hiện giao dịch.
  - **Validation:** Báo lỗi nếu số dư khả dụng nhỏ hơn số tiền muốn chuyển.
  - **Verification:** `SOURCE_VERIFIED`.

### Section XFER-SEC-02: Recipient Phone Lookup & Friends Quick Pick
- **Feature ID:** `TRANSFER-002`
  - **Tên:** Tra cứu người nhận qua số điện thoại (Phone Lookup)
  - **Mục đích:** Kiểm tra tài khoản người nhận theo đúng định dạng SĐT Việt Nam (bắt đầu bằng 0, đủ 10 số).
  - **API Call:** `transfersApiService.getRecipientByPhone(phone)`.
  - **Lookup State Machine:** `idle` -> `searching` -> `found` / `not_found` / `self` (tự chuyển cho chính mình) / `inactive` / `invalid_format`.
  - **Verification:** `SOURCE_VERIFIED`.

- **Feature ID:** `TRANSFER-003`
  - **Tên:** Danh sách bạn bè gợi ý chuyển tiền nhanh (Favorite Friends)
  - **Mục đích:** Bấm chọn 1 chạm từ danh bạ bạn bè đã kết bạn để điền tự động thông tin người nhận.
  - **Data Dependency:** `friendService.getFriends()`, sắp xếp bạn bè đánh dấu sao (`isFavorite`) lên đầu.
  - **Verification:** `SOURCE_VERIFIED`.

### Section XFER-SEC-03: Amount & Note Input
- **Feature ID:** `TRANSFER-004`
  - **Tên:** Nhập số tiền chuyển với Mask tiền tệ tự động
  - **Mục đích:** Hạn chế nhầm lẫn mệnh giá tiền, hiển thị chữ số đọc bằng chữ (ví dụ: "Năm trăm nghìn đồng").
  - **Validation:** Tối thiểu 1.000 ₫, tối đa theo hạn mức tài khoản, không vượt quá số dư ví.
  - **Verification:** `SOURCE_VERIFIED`.

- **Feature ID:** `TRANSFER-005`
  - **Tên:** Nhập lời nhắn / nội dung chuyển tiền
  - **Mục đích:** Ghi rõ lý do chuyển khoản (ví dụ: "Trả tiền ăn trưa", "Góp tiền sinh nhật").
  - **Verification:** `SOURCE_VERIFIED`.

### Section XFER-SEC-04: Security PIN Challenge
- **Feature ID:** `TRANSFER-006`
  - **Tên:** Xác thực lệnh chuyển tiền bằng mã PIN 6 số
  - **Mục đích:** Hàng rào bảo vệ chống gian lận và chuyển tiền ngoài ý muốn.
  - **Security Rule:** Không lưu PIN trong plain-text; gọi qua `challengePinWithValue(pin)`.
  - **API Call:** `transfersApiService.createTransfer({ recipientId, amount, note, pin })`.
  - **Verification:** `SOURCE_VERIFIED`.

### Section XFER-SEC-05 & 06: Receipt & Recent Payments
- **Feature ID:** `TRANSFER-007`
  - **Tên:** Biên lai xác nhận giao dịch thành công (Transfer Receipt)
  - **Mục đích:** Hiển thị mã giao dịch duy nhất, thời gian hoàn tất, nút Sao chép mã hoặc Tải biên lai.
  - **Verification:** `SOURCE_VERIFIED`.

- **Feature ID:** `TRANSFER-008`
  - **Tên:** Bảng lịch sử các lệnh chuyển tiền trực tiếp gần đây
  - **Mục đích:** Tra cứu danh sách các lệnh chuyển đi (`transfersApiService.getTransfers({ page: 1, pageSize: 50 })`).
  - **Verification:** `SOURCE_VERIFIED`.
