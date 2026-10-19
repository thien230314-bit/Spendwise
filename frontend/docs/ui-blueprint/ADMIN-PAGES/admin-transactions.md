# ADMIN PAGE INVENTORY: TRANSACTIONS MONITORING (GIÁM SÁT GIAO DỊCH TOÀN HỆ THỐNG)
**Page ID:** `ADMIN-TRANSACTIONS`  
**Route:** `/admin/transactions`  
**Layout:** `AdminAppShell`  
**Quyền hạn:** `Role: SystemAdmin`  
**File Source:** `src/features/admin/AdminTransactionsPage.tsx`  
**Trạng thái Xác thực:** `SOURCE_VERIFIED`

---

## 1. MỤC ĐÍCH NGHIỆP VỤ & TỔNG QUAN
Kiểm toán luồng tiền di chuyển giữa tất cả người dùng, các tài khoản ví nội bộ và cổng thanh toán ngân hàng SePay. Cung cấp công cụ điều tra dấu vết số và can thiệp giao dịch bất thường.

---

## 2. KIỂM KÊ CÁC SECTION TRÊN TRANG

| Section ID | Tên Section | Component Source | Vị trí Bố cục | Trạng thái Hiển thị |
|---|---|---|---|---|
| `ADM-TX-SEC-01` | Global Transactions Audit Table | `AdminTransactionsPage.tsx` lines 1-505 | Thân trang toàn diện | Luôn hiển thị |

---

## 3. DANH MỤC TÍNH NĂNG CHI TIẾT (FEATURE INVENTORY)

### Section ADM-TX-SEC-01: Global Transactions Audit Table
- **Feature ID:** `ADM-TX-001`
  - **Tên:** Truy vấn giao dịch toàn hệ thống (Global Transaction Query)
  - **Mục đích:** Tìm kiếm theo mã bút toán, khoảng thời gian, loại giao dịch (Nạp/Rút/Chuyển) và hạn mức tiền.
  - **Verification:** `SOURCE_VERIFIED`.

- **Feature ID:** `ADM-TX-002`
  - **Tên:** Điều tra gói tin và dấu vết giao dịch (Forensic Audit Trace)
  - **Mục đích:** Xem chi tiết IP nguồn, User-Agent, chữ ký số, và lý do thất bại nếu có.
  - **Verification:** `SOURCE_VERIFIED`.

- **Feature ID:** `ADM-TX-003`
  - **Tên:** Can thiệp đảo ngược giao dịch lỗi (Force Reverse Tx)
  - **Mục đích:** Hoàn lại số dư an toàn trong trường hợp giao dịch bị treo hoặc xử lý sai lệch.
  - **Verification:** `SOURCE_VERIFIED`.
