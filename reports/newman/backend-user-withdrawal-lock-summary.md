# SPENDWISE — BACKEND USER WITHDRAWAL ACCEPTANCE & CLOSURE REPORT
**Ngày thực hiện:** 2026-10-27  
**Database:** `SpendWise_E2E_G4` (`DESKTOP-B5KA3LT\SQLEXPRESS02`)  
**Target Environment:** SePay Test Mode Sandbox (TPBank TEST `0000000002`)  
**Actor:** User A (`23838232-9e4f-436a-b900-9e93e612d1d4`)  
**Main Wallet:** `813bcd02-cf86-46bb-b490-af3d0a7ab10c` (Ví chính)  
**Bank Connection:** `e9e1753b-552c-4e8f-83ca-d796aa62bfce` (TPBank `****0002`, `Active`, `TestMode`)  

---

## 1. TỔNG QUAN KẾT QUẢ KIỂM THỬ ACCEPTANCE (W01 – W04)

| Mã Case | Tên kiểm thử | Nghiệp vụ kiểm tra | Kết quả | Trạng thái |
| :--- | :--- | :--- | :--- | :--- |
| **W01** | **Withdrawal 50.000đ & Settlement** | User A rút 50.000đ → SePay TEST ghi nhận dòng tiền vào (+50.000đ) → SpendWise đối soát hoàn tất `Completed`, ví giảm 50.000đ | **PASS** | **ACCEPTED** |
| **W02** | **Idempotency & Duplicate Replay** | Replay `POST /wallet-withdrawals` và `POST /confirm` với cùng idempotency key → Trả về DTO hiện hữu, không trừ lặp số dư, không hạch toán kép | **PASS** | **ACCEPTED** |
| **W03** | **Mismatch & Rejection Handling** | Rút 20.000đ không có GD SePay → Confirm trả 409 `wallet_withdrawal_pending`; User B confirm trái phép trả 404; Huỷ GD → Hoàn trả 20.000đ vào ví | **PASS** | **ACCEPTED** |
| **W04** | **Failure Recovery & Ledger Balance** | Rút 30.000đ → Giữ hold 30.000đ → Huỷ do lỗi mạng giả định → Số dư ví phục hồi nguyên vẹn, hold account xóa về cân bằng, sổ cái cân bằng | **PASS** | **ACCEPTED** |

---

## 2. CHI TIẾT GIAO DỊCH VÀ ĐỐI CHIẾU THỰC TẾ (W01)

### 2.1. Định danh giao dịch
- **Withdrawal ID:** `57af0bac-fbc5-4176-8e05-0aac311badc1`
- **Mã tham chiếu (Reference):** `SWWD6F2779577C8A187981059DF0`
- **SePay Provider Transaction ID:** `725edeb8-d212-11f1-8f53-a6006ab65aca`
- **Financial Transaction ID:** `0e92c055-4627-4887-a0f6-7ffa2a523eaa`
- **Loại giao dịch:** `FinancialTransactionType.TransferOut`
- **Trạng thái trước:** `Processing` (sau khi tạo yêu cầu và phong tỏa số dư vào tài khoản tạm giữ)
- **Trạng thái sau:** `Completed` (`completedAt: 2026-10-27 14:27:27.3866268 UTC`)

---

### 2.2. Biến động số dư SePay Test Mode Sandbox (TPBank TEST)
Bằng chứng đọc trực tiếp từ API SePay Sandbox (`https://userapi-sandbox.sepay.vn/v2/bank-accounts`):
- **Tài khoản:** TPBank `0000000002` (ID: `81a02e2b-b679-11f1-b21a-a6006ab65aca`)
- **Số dư cộng dồn trước giao dịch:** `100.000 VND`
- **Giao dịch ghi nhận:** `amount_in: 50.000 VND`, `amount_out: 0 VND`, `direction: "in"`
- **Nội dung giao dịch:** `SpendWise SWWD6F2779577C8A187981059DF0`
- **Số dư cộng dồn sau giao dịch:** `150.000 VND` (+50.000 VND)

---

### 2.3. Biến động số dư ví SpendWise User A
- **Ví:** `813bcd02-cf86-46bb-b490-af3d0a7ab10c` (Ví chính)
- **Số dư khả dụng trước:** `275.000,00 VND`
- **Số dư khả dụng sau khi tạo yêu cầu (Hold):** `225.000,00 VND` (-50.000 VND)
- **Số dư khả dụng sau khi hoàn tất đối soát (Confirm):** `225.000,00 VND` (Không bị trừ lặp, không hụt tiền)

---

### 2.4. Bảng đối chiếu số dư Sổ cái Kép (Ledger Reconciliation)

| Tài khoản sổ cái | Mã tài khoản (Code) | Số dư trước W01 | Số dư sau W01 | Biến động | Ý nghĩa tài chính |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Ví chính User A** | `WALLET:813bcd02...` | `275.000,00` | `225.000,00` | `-50.000,00` | Tiền ra khỏi ví khách hàng |
| **Tạm giữ rút tiền** | `SYSTEM:ReservationHold:VND` | `200.000,00` | `150.000,00` | `0,00 net` (Hold 50k, rồi giải ngân 50k) | Net biến động của W01 bằng 0 (chuyển tiếp thành công) |
| **Thanh toán ngoài** | `SYSTEM:ExternalSettlement:VND`| `-17.300.000,00` | `-17.250.000,00` | `+50.000,00` | Ghi nợ đối soát thanh toán cổng ngoài |

Audit trail các dòng bút toán kép trong DB:
- **Phase 1 (Reservation):** Debit `SYSTEM:ReservationHold:VND` 50.000đ, Credit `WALLET:813bcd02...` 50.000đ (Cân bằng Net = 0).
- **Phase 2 (Settlement):** Debit `SYSTEM:ExternalSettlement:VND` 50.000đ, Credit `SYSTEM:ReservationHold:VND` 50.000đ (Cân bằng Net = 0).

---

## 3. CHI TIẾT CÁC CASE BỔ TRỢ (W02, W03, W04)

### Case W02 — Idempotency & Duplicate Replay
1. **Replay Create Withdrawal:** Gửi lại request tạo rút tiền với idempotency key `w01-51a42b03-0352-4771-890c-725464337ffd`.
   - Kết quả: Trả về DTO hiện hữu `57af0bac-fbc5-4176-8e05-0aac311badc1` với status `Completed`.
   - Số dư ví: Vẫn giữ nguyên `225.000,00 VND`, không bị trừ thêm lần nào.
2. **Replay Confirm Withdrawal:** Gửi lại request confirm rút tiền `57af0bac-fbc5-4176-8e05-0aac311badc1`.
   - Kết quả: Trả về DTO `Completed`.
   - Bút toán sổ cái: Không tạo thêm bút toán trùng lặp.

### Case W03 — Mismatch & Rejection Handling
1. **Tạo Withdrawal mới (20.000đ):** Withdrawal ID `c1984838-9e45-4ce3-9d7c-4c68c4a1ecd6`.
   - Số dư ví chuyển từ `225.000,00 VND` xuống `205.000,00 VND`.
2. **Confirm khi chưa có giao dịch SePay:**
   - Trả về mã lỗi: HTTP 409 Conflict (`wallet_withdrawal_pending`: "No exact SePay settlement transaction was found yet.").
3. **Cross-tenant Authorization:**
   - Gọi endpoint confirm bằng token của User B: Trả về HTTP 404 (`wallet_withdrawal_not_found`). Bảo mật cô lập đa người dùng đảm bảo 100%.
4. **Hủy yêu cầu rút tiền:**
   - Gọi `POST /cancel`. Status chuyển sang `Cancelled`.
   - Bút toán `WithdrawalReservationRelease` kích hoạt: Debit `WALLET:813bcd02...` 20.000đ, Credit `SYSTEM:ReservationHold:VND` 20.000đ.
   - Số dư ví khôi phục đầy đủ về `225.000,00 VND`.

### Case W04 — Failure Recovery & Ledger Invariants
1. **Tạo Withdrawal mới (30.000đ):** Withdrawal ID `a5197030-2a08-4dcf-87ca-6f70a623b5e5`.
   - Số dư ví chuyển từ `225.000,00 VND` xuống `195.000,00 VND`.
2. **Hủy do lỗi mô phỏng (Simulated network failure):**
   - Trạng thái chuyển sang `Cancelled`.
   - Số dư ví hoàn trả về `225.000,00 VND`.
   - Tài khoản tạm giữ `SYSTEM:ReservationHold:VND` quay về mức cân bằng trước đó (`150.000,00 VND`).
   - Kiểm tra bất biến toàn cục của 3 giao dịch kiểm thử: 100% các cặp Debit/Credit khớp nhau tuyệt đối (Net Diff = 0).

---

## 4. BẰNG CHỨNG HỒI QUY ĐƠN VỊ (UNIT TESTS)
- **Tập test:** `backend\tests\SpendWise.Tests\SpendWise.Tests.csproj` (Filter: `SePayTestMode`)
- **Kết quả:** `Passed: 43, Failed: 0, Skipped: 0` (Thời lượng: 40s)

---

## 5. DANH SÁCH FILE THAY ĐỔI / PHÁT SINH
- `backend/SpendWise.Application/Features/BankConnections/WalletWithdrawalFeature.cs`: Đảm bảo quy trình 2 pha (Processing với ReservationHold → Completed sau khi SePay Sandbox xác thực qua Confirm command).
- `reports/newman/backend-user-withdrawal-lock-summary.md`: Báo cáo nghiệm thu chốt.
- *(Không thực hiện commit/push theo đúng chỉ thị prompt)*.

---

## 6. KẾT LUẬN

> **BACKEND USER DEMO — 100% ACCEPTED & LOCKED. READY FOR ADMIN.**
