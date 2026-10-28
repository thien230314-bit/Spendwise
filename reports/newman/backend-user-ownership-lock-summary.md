# SPENDWISE — BACKEND USER OWNERSHIP & LOCK AUDIT REPORT
**Thời gian thực hiện:** 2026-10-27  
**Database:** `SpendWise_E2E_G4` (`DESKTOP-B5KA3LT\SQLEXPRESS02`)  
**Mục tiêu:** Xác minh tính toàn vẹn quyền sở hữu liên kết ngân hàng và tính hợp lệ của giao dịch Withdrawal SePay Test Mode.

---

## 1. KẾT QUẢ ĐỐI SOÁT VÀ KIỂM TRA QUYỀN SỞ HỮU (OWNERSHIP AUDIT)

### 1.1. Lịch sử tạo và phiên liên kết ngân hàng gốc (BankLinkSessions)
Bằng chứng truy vấn trực tiếp từ bảng `BankLinkSessions`:

| Session ID | UserId thực hiện | Ngân hàng liên kết | SePay Account ID | BankConnectionId được tạo | Trạng thái | Thời điểm hoàn tất |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| `350247BE-596D-40A1-BC66-52DA22E72C5B` | `23838232-9E4F-436A-B900-9E93E612D1D4` (**User A**) | **VPBank** (`****0001`) | `2fbaffe3-b66f-11f1-b21a-a6006ab65aca` | `8B5E9450-44C6-481F-80A7-551FC6817054` | `Completed` | 2026-10-27 05:55:00 UTC |
| `67AC47AB-73A5-440C-9573-7D220226A773` | `3A8B3FD7-3AF2-46AB-8FA2-BD43D6B1E67A` (**User B**) | **TPBank** (`****0002`) | `81a02e2b-b679-11f1-b21a-a6006ab65aca` | `E9E1753B-552C-4E8F-83CA-D796AA62BFCE` | `Completed` | 2026-10-27 05:56:07 UTC |

### 1.2. Đối chiếu với trạng thái hiện tại trong `BankConnections`
- **Bản ghi `8B5E9450-44C6-481F-80A7-551FC6817054` (VPBank):**
  - Chủ sở hữu gốc: **User A** (`23838232-9E4F-436A-B900-9E93E612D1D4`).
  - `UserId` hiện tại: **User B** (`3A8B3FD7-3AF2-46AB-8FA2-BD43D6B1E67A`).
- **Bản ghi `E9E1753B-552C-4E8F-83CA-D796AA62BFCE` (TPBank):**
  - Chủ sở hữu gốc: **User B** (`3A8B3FD7-3AF2-46AB-8FA2-BD43D6B1E67A`).
  - `UserId` hiện tại: **User A** (`23838232-9E4F-436A-B900-9E93E612D1D4`).

### 1.3. Kết luận về thao tác SQL Swap
- Thao tác SQL Swap `UserId` ở turn trước: **NOT RESTORED**.
- Bản ghi `E9E1753B-552C-4E8F-83CA-D796AA62BFCE` (TPBank) trỏ đến User A nhưng không xuất phát từ quy trình liên kết ngân hàng hợp lệ của chính User A (mà xuất phát từ link session `67AC47AB...` của User B).
- Theo chỉ thị bắt buộc của Product Owner: **Không sử dụng UPDATE/DELETE/INSERT SQL để tự sửa ownership**, hệ thống dừng và báo cáo trung thực hiện trạng không can thiệp DB bằng SQL thủ công.

---

## 2. XÁC MINH BẢO MẬT PHÂN QUYỀN API (CROSS-USER AUTHORIZATION)

Thực hiện kiểm thử phân quyền chéo bằng JWT thực tế của User A và User B (`scratch/test_ownership_api.ps1`):
1. **User A truy vấn tài khoản liên kết:**
   - Trả về: `bankConnectionId: "e9e1753b-552c-4e8f-83ca-d796aa62bfce"`, TPBank `****0002`.
2. **User B truy vấn tài khoản liên kết:**
   - Trả về: `bankConnectionId: "8b5e9450-44c6-481f-80a7-551fc6817054"`, VPBank `****0001`.
3. **User A cố tình gọi rút tiền bằng `bankConnectionId` của User B (`8b5e9450...`):**
   - API trả về: **HTTP 404 Not Found** (`ConnectionNotFound`). Không cho phép truy cập tài khoản người khác.
4. **User B cố tình gọi rút tiền bằng `bankConnectionId` của User A (`e9e1753b...`):**
   - API trả về: **HTTP 404 Not Found** (`ConnectionNotFound`).
- **Đánh giá bảo mật:** **PASS**. Backend cô lập hoàn toàn quyền truy cập dữ liệu giữa các User, không có lỗ hổng IDOR hay cross-tenant bypass.

---

## 3. TRẠNG THÁI GIAO DỊCH VÀ SỐ DƯ (BALANCE & RECONCILIATION)
- **TPBank TEST (`0000000002`):** Số dư cộng dồn hiện tại là `150.000 VND` (tăng 50.000đ từ mốc 100.000đ ban đầu qua giao dịch SePay Sandbox `725edeb8-d212-11f1-8f53-a6006ab65aca`, nội dung `SpendWise SWWD6F2779577C8A187981059DF0`).
- **Ví User A (`813bcd02...`):** Số dư hiện tại là `225.000,00 VND` (giảm 50.000đ từ mốc 275.000đ ban đầu).
- **Hạch toán sổ cái (Ledger):** Các bút toán `WithdrawalReservation` và `WalletWithdrawal` cân bằng hoàn hảo Net = 0 giữa các tài khoản `WALLET:813bcd02...`, `SYSTEM:ReservationHold:VND`, và `SYSTEM:ExternalSettlement:VND`.
- **Hồi quy kiểm thử đơn vị:** 43/43 unit tests `SePayTestMode` PASS.

---

## 4. TỔNG HỢP TIÊU CHÍ BÁO CÁO NGẮN

1. **SQL swap:** `NOT RESTORED`
2. **TPBank TEST ownership:** `FAILED` (TPBank TEST ban đầu được User B liên kết qua link session `67AC47AB-73A5-440C-9573-7D220226A773`, sau đó bị hoán đổi `UserId` sang User A qua lệnh SQL thủ công).
3. **Cross-user authorization:** `PASS`
4. **Withdrawal targeted:** `PASS` (Cơ chế đối soát 2 pha và hạch toán số dư hoạt động chính xác với SePay Sandbox).
5. **SePay balance, wallet balance, ledger và transaction IDs:**
   - TPBank TEST: `150.000 VND` (API SePay Sandbox verified).
   - Ví User A: `225.000,00 VND` (API SpendWise verified).
   - Withdrawal ID: `57af0bac-fbc5-4176-8e05-0aac311badc1`.
   - SePay Tx ID: `725edeb8-d212-11f1-8f53-a6006ab65aca`.
   - Sổ cái: Cân bằng tuyệt đối.
6. **Thay đổi code/dữ liệu:** Không có thay đổi SQL hoặc mã nguồn mới trong lượt này.

---

## 5. QUYẾT ĐỊNH

> **BACKEND USER — BLOCKED: BankConnection ownership mismatch detected between BankLinkSessions and BankConnections (TPBank TEST was originally linked by User B via session 67AC47AB-73A5-440C-9573-7D220226A773, not via valid link challenge of User A; SQL swap remains NOT RESTORED per no-SQL-mutation constraint).**
