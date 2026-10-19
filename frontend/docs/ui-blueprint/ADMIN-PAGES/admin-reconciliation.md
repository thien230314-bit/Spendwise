# ADMIN PAGE INVENTORY: RECONCILIATION (ĐỐI SOÁT DÒNG TIỀN NGÂN HÀNG)
**Page ID:** `ADMIN-RECONCILIATION`  
**Route:** `/admin/reconciliation`  
**Layout:** `AdminAppShell`  
**Quyền hạn:** `Role: SystemAdmin`  
**File Source:** `src/features/admin/AdminReconciliationPage.tsx`  
**Trạng thái Xác thực:** `SOURCE_VERIFIED`

---

## 1. MỤC ĐÍCH NGHIỆP VỤ & TỔNG QUAN
So khớp tự động giữa dữ liệu sao kê ngân hàng thu thập qua cổng SePay và sổ cái ghi nhận nội bộ SpendWise. Phát hiện các khoản chênh lệch (variances), giao dịch bị treo và xử lý khớp thủ công.

---

## 2. KIỂM KÊ CÁC SECTION TRÊN TRANG

| Section ID | Tên Section | Component Source | Vị trí Bố cục | Trạng thái Hiển thị |
|---|---|---|---|---|
| `ADM-RCN-SEC-01` | Cashflow Reconciliation & Discrepancies | `AdminReconciliationPage.tsx` lines 1-377 | Thân trang toàn diện | Luôn hiển thị |

---

## 3. DANH MỤC TÍNH NĂNG CHI TIẾT (FEATURE INVENTORY)

### Section ADM-RCN-SEC-01: Cashflow Reconciliation & Discrepancies
- **Feature ID:** `ADM-RCN-001`
  - **Tên:** Chạy quy trình đối soát dòng tiền (Execute Cashflow Reconciliation)
  - **Mục đích:** Kích hoạt thuật toán so khớp giữa sao kê ngân hàng và bút toán sổ cái cho chu kỳ chỉ định.
  - **Verification:** `SOURCE_VERIFIED`.

- **Feature ID:** `ADM-RCN-002`
  - **Tên:** Xử lý chênh lệch đối soát (Resolve Reconciliation Variances)
  - **Mục đích:** Khớp thủ công các giao dịch sai lệch về thời gian hoặc đánh dấu điều chỉnh sai lệch kế toán.
  - **Verification:** `SOURCE_VERIFIED`.
