# ADMIN PAGE INVENTORY: RISK & FRAUD RADAR (QUẢN TRỊ RỦI RO & CHỐNG GIAN LẬN)
**Page ID:** `ADMIN-RISK`  
**Route:** `/admin/risk`  
**Layout:** `AdminAppShell`  
**Quyền hạn:** `Role: SystemAdmin`  
**File Source:** `src/features/admin/AdminRiskPage.tsx`  
**Trạng thái Xác thực:** `SOURCE_VERIFIED`

---

## 1. MỤC ĐÍCH NGHIỆP VỤ & TỔNG QUAN
Hệ thống phòng vệ tự động phát hiện các hành vi giao dịch và truy cập bất thường (đăng nhập từ nhiều dải IP lạ, vượt hạn mức nhanh, sai PIN nhiều lần). Cung cấp hành động can thiệp khẩn cấp đóng băng tài khoản.

---

## 2. KIỂM KÊ CÁC SECTION TRÊN TRANG

| Section ID | Tên Section | Component Source | Vị trí Bố cục | Trạng thái Hiển thị |
|---|---|---|---|---|
| `ADM-RSK-SEC-01` | Fraud Alerts & Emergency Risk Mitigation | `AdminRiskPage.tsx` lines 1-245 | Thân trang toàn diện | Luôn hiển thị |

---

## 3. DANH MỤC TÍNH NĂNG CHI TIẾT (FEATURE INVENTORY)

### Section ADM-RSK-SEC-01: Fraud Alerts & Emergency Risk Mitigation
- **Feature ID:** `ADM-RSK-001`
  - **Tên:** Dòng cảnh báo rủi ro thời gian thực (Realtime Fraud Alerts Feed)
  - **Mục đích:** Cảnh báo các sự cố vi phạm an toàn với mức độ nghiêm trọng (Low, Medium, Critical).
  - **Verification:** `SOURCE_VERIFIED`.

- **Feature ID:** `ADM-RSK-002`
  - **Tên:** Đóng băng tài khoản khẩn cấp (Emergency Account Freeze)
  - **Mục đích:** Khóa chiều chuyển tiền ngay lập tức khi phát hiện nghi vấn tấn công chiếm quyền ví.
  - **Verification:** `SOURCE_VERIFIED`.

- **Feature ID:** `ADM-RSK-003`
  - **Tên:** Tinh chỉnh ngưỡng rủi ro cảnh báo (Risk Rule Threshold Tuning)
  - **Mục đích:** Điều chỉnh số lần sai PIN, hạn mức giao dịch cảnh báo tức thì.
  - **Verification:** `SOURCE_VERIFIED`.
