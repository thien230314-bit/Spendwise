# SPENDWISE — BACKEND USER FINAL ACCEPTANCE & LOCK REPORT
**Thời gian thực hiện:** 2026-10-27  
**Database TEST:** `SpendWise_E2E_G4` (`DESKTOP-B5KA3LT\SQLEXPRESS02`)  
**Target Environment:** SePay Test Mode Sandbox (TPBank TEST `0000000002`)  
**Tình trạng:** Khôi phục đúng chủ sở hữu gốc theo `BankLinkSessions`, xác thực API phân quyền chéo, nghiệm thu Withdrawal DEMO chính thức bằng User B và TPBank TEST.

---

## 1. PHỤC HỒI QUYỀN SỞ HỮU GỐC (OWNERSHIP RESTORATION)

### 1.1. Bằng chứng đối chiếu `BankLinkSessions`
- **VPBank (`****0001`, SePay ID `2fbaffe3-b66f-11f1-b21a-a6006ab65aca`):**
  - Khởi tạo qua session: `350247BE-596D-40A1-BC66-52DA22E72C5B`
  - Chủ sở hữu gốc: **User A** (`23838232-9E4F-436A-B900-9E93E612D1D4`)
  - `BankConnectionId`: `8B5E9450-44C6-481F-80A7-551FC6817054`
- **TPBank (`****0002`, SePay ID `81a02e2b-b679-11f1-b21a-a6006ab65aca`):**
  - Khởi tạo qua session: `67AC47AB-73A5-440C-9573-7D220226A773`
  - Chủ sở hữu gốc: **User B** (`3A8B3FD7-3AF2-46AB-8FA2-BD43D6B1E67A`)
  - `BankConnectionId`: `E9E1753B-552C-4E8F-83CA-D796AA62BFCE`

### 1.2. Thực thi khôi phục giao dịch an toàn (SQL Transaction)
Giao dịch phục hồi được thực hiện có kiểm tra an toàn pre-condition:
- Kiểm tra chính xác DB `SpendWise_E2E_G4`.
- Sử dụng trung gian tạm thời để thỏa mãn unique constraint `IX_BankConnections_UserId`.
- Chuyển `8B5E9450-44C6-481F-80A7-551FC6817054` (VPBank) về đúng **User A**.
- Chuyển `E9E1753B-552C-4E8F-83CA-D796AA62BFCE` (TPBank) về đúng **User B**.
- Xác minh sau UPDATE: Khớp 100% `BankConnections.UserId = BankLinkSessions.UserId` (`OwnershipStatus: MATCH`).

| BankConnectionId | Ngân hàng | Chủ sở hữu trước sửa | Chủ sở hữu sau phục hồi | Khớp BankLinkSessions |
| :--- | :--- | :--- | :--- | :---: |
| `8B5E9450-44C6-481F-80A7-551FC6817054` | VPBank `****0001` | User B | **User A** (`23838232...`) | **MATCH** |
| `E9E1753B-552C-4E8F-83CA-D796AA62BFCE` | TPBank `****0002` | User A | **User B** (`3A8B3FD7...`) | **MATCH** |

---

## 2. XÁC MINH PHÂN QUYỀN API THỰC TẾ (API AUTHORIZATION)

Thực hiện kiểm thử thực tế với JWT của User A và User B (`scratch/test_restored_ownership_api.ps1`):
1. **User A truy vấn danh sách ngân hàng liên kết:**
   - Trả về đúng VPBank `****0001` (`8B5E9450...`).
2. **User B truy vấn danh sách ngân hàng liên kết:**
   - Trả về đúng TPBank `****0002` (`E9E1753B...`).
3. **User A gọi Withdrawal bằng TPBank của User B:**
   - API chặn hoàn toàn, trả về **HTTP 404 Not Found** (`ConnectionNotFound`).
4. **User B gọi Withdrawal bằng VPBank của User A:**
   - API chặn hoàn toàn, trả về **HTTP 404 Not Found** (`ConnectionNotFound`).
- **Kết quả phân quyền:** **PASS** (100% cô lập dữ liệu theo JWT actor).

---

## 3. NGHIỆM THU WITHDRAWAL MỚI BẰNG USER B + TPBANK TEST

Thực hiện toàn trình giao dịch Withdrawal DEMO mới với đúng chủ sở hữu:

### 3.1. Định danh giao dịch chính thức
- **Actor:** User B (`3A8B3FD7-3AF2-46AB-8FA2-BD43D6B1E67A`)
- **Tài khoản ngân hàng:** TPBank TEST `0000000002` (`E9E1753B-552C-4E8F-83CA-D796AA62BFCE`)
- **Ví trích tiền:** Ví chính User B (`2CC3161F-17A1-4BEB-90A3-8E8D6ECBFD70`)
- **Số tiền rút:** `50.000 VND`
- **Withdrawal ID:** `5F89A5D9-A160-4242-B595-25DA35063411`
- **Mã tham chiếu (Reference):** `SWWD3EEBA411C767A510FB7BD57C`
- **SePay Provider Transaction ID:** `fd3de6f4-d215-11f1-8f53-a6006ab65aca`
- **Financial Transaction ID:** `C5B6948D-E881-4671-98E3-5D548B2FD829` (Loại: `TransferOut`, Owner: User B)
- **Tiến trình trạng thái:** `Processing` (sau khi trích hold ví) $\rightarrow$ `Completed` (`completedAt: 2026-10-27 14:53:03.8135028 UTC`).

### 3.2. Biến động số dư SePay Sandbox (TPBank TEST `0000000002`)
Bằng chứng đọc từ API SePay Sandbox (`https://userapi-sandbox.sepay.vn/v2/bank-accounts`):
- **Số dư trước giao dịch:** `150.000 VND`
- **Giao dịch mô phỏng ghi nhận:** `amount_in: 50.000 VND`, `amount_out: 0 VND`, `direction: "in"`
- **Nội dung:** `SpendWise SWWD3EEBA411C767A510FB7BD57C`
- **Số dư sau giao dịch:** `200.000 VND` (tăng chính xác +50.000đ).

### 3.3. Biến động số dư ví SpendWise User B
- **Số dư khả dụng trước:** `4.875.000,00 VND`
- **Số dư khả dụng sau khi tạo yêu cầu (Hold):** `4.825.000,00 VND` (-50.000đ)
- **Số dư khả dụng sau khi hoàn tất đối soát (Confirm):** `4.825.000,00 VND` (Giữ nguyên, không trừ lặp).

### 3.4. Đối chiếu bút toán Sổ cái kép (Ledger Reconciliation)
Các dòng bút toán được ghi nhận trong bảng `LedgerEntries` cho giao dịch `5F89A5D9...`:
- **Giai đoạn 1 (WithdrawalReservation):**
  - Debit `SYSTEM:ReservationHold:VND`: `50.000,00 VND`
  - Credit `WALLET:2cc3161f...` (Ví chính User B): `50.000,00 VND`
- **Giai đoạn 2 (WalletWithdrawal Confirm):**
  - Debit `SYSTEM:ExternalSettlement:VND`: `50.000,00 VND`
  - Credit `SYSTEM:ReservationHold:VND`: `50.000,00 VND`
- **Net biến động tài khoản tạm giữ (`SYSTEM:ReservationHold:VND`):** Bằng `0,00 VND`. Cân bằng sổ cái tuyệt đối.

### 3.5. Kiểm tra Idempotency & Replay Protection
1. **Replay Confirm:** Gửi lại confirm với token User B $\rightarrow$ Trả về DTO `Completed`, không tạo thêm bút toán sổ cái.
2. **Replay Create:** Gửi lại create với cùng idempotency key `w-userb-final-...` $\rightarrow$ Trả về DTO hiện hữu `5F89A5D9...`, số dư ví User B giữ nguyên `4.825.000,00 VND`.
3. **Cross-user Confirm:** User A gọi confirm cho withdrawal của User B $\rightarrow$ Trả về **HTTP 404** (`wallet_withdrawal_not_found`).

---

## 4. ĐỐI CHIẾU CÁC GIAO DỊCH LỊCH SỬ PHÁT SINH TRONG THỜI GIAN SWAP

Hệ thống bảo lưu toàn vẹn dữ liệu kiểm thử, không xóa hay chỉnh sửa lịch sử:

| Mã Withdrawal | UserId tạo GD | BankConnection sử dụng | Số tiền | Trạng thái | Diễn giải |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `57AF0BAC-FBC5-4176-8E05-0AAC311BADC1` | User A | `E9E1753B...` (TPBank) | 50.000đ | `Completed` | Phát sinh khi swap ownership để test tích hợp SePay Sandbox lần đầu. Bút toán sổ cái và số dư ví User A được giữ nguyên. |
| `C1984838-9E45-4CE3-9D7C-4C68C4A1ECD6` | User A | `E9E1753B...` (TPBank) | 20.000đ | `Cancelled` | Giao dịch kiểm thử mismatch/rejection. Đã hoàn trả 20.000đ về ví User A. |
| `A5197030-2A08-4DCF-87CA-6F70A623B5E5` | User A | `E9E1753B...` (TPBank) | 30.000đ | `Cancelled` | Giao dịch kiểm thử failure recovery. Đã hoàn trả 30.000đ về ví User A. |
| `5F89A5D9-A160-4242-B595-25DA35063411` | **User B** | **`E9E1753B...` (TPBank)** | **50.000đ** | **`Completed`** | **Giao dịch nghiệm thu chính thức sau khi khôi phục đúng quyền sở hữu gốc.** |

---

## 5. DANH SÁCH THAY ĐỔI
- **Database TEST (`SpendWise_E2E_G4`):** Đã hoàn nguyên 2 bản ghi `BankConnections` về đúng chủ sở hữu gốc (`8B5E9450...` $\rightarrow$ User A, `E9E1753B...` $\rightarrow$ User B).
- **Mã nguồn:** Không thay đổi thêm.
- **Git:** Không commit/push theo quy định.

---

## 6. KẾT LUẬN

> **BACKEND USER DEMO — 100% ACCEPTED & LOCKED. READY FOR ADMIN.**
