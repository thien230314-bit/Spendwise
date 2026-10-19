# ADMIN PAGE INVENTORY: BANKING & SEPAY INTEGRATION (GIÁM SÁT KẾT NỐI NGÂN HÀNG & SEPAY)
**Page ID:** `ADMIN-BANKING`  
**Route:** `/admin/banking` (Alias: `/admin/sandbox`)  
**Layout:** `AdminAppShell`  
**Quyền hạn:** `Role: SystemAdmin`  
**File Source:** `src/features/admin/AdminBankingPage.tsx`  
**Trạng thái Xác thực:** `SOURCE_VERIFIED`

---

## 1. MỤC ĐÍCH NGHIỆP VỤ & TỔNG QUAN
Giám sát và kiểm tra tình trạng tích hợp cổng SePay Open Banking thực tế: quản lý các kết nối ngân hàng của người dùng (Connections), theo dõi luồng giao dịch đồng bộ (Synced Transactions) và xem xét các sự kiện Webhook cần can thiệp (Events to Review).

*Lưu ý kiến trúc chuẩn:* Đây là màn hình giám sát và kiểm tra vận hành hệ thống kết nối ngân hàng; không phải là công cụ phát sinh tiền giả hay giao dịch ảo trái phép trong môi trường tài chính thực.

---

## 2. KIỂM KÊ CÁC SECTION TRÊN TRANG

| Section ID | Tên Section | Component Source | Vị trí Bố cục | Trạng thái Hiển thị |
|---|---|---|---|---|
| `ADM-BNK-SEC-01` | Bank Connections & Webhooks Inspection | `AdminBankingPage.tsx` lines 1-593 | Thân trang toàn diện | Luôn hiển thị |

---

## 3. DANH MỤC TÍNH NĂNG CHI TIẾT (FEATURE INVENTORY)

### Section ADM-BNK-SEC-01: Bank Connections & Webhooks Inspection
- **Feature ID:** `ADM-BNK-001`
  - **Tên:** Giám sát trạng thái kết nối cổng ngân hàng (Gateway & Connections Health)
  - **Mục đích:** Theo dõi tình trạng kết nối tới các tài khoản ngân hàng liên kết, trạng thái kích hoạt và thời gian đồng bộ cuối.
  - **Data Dependency:** `useFinancial()`, `bankApiService`.
  - **Verification:** `SOURCE_VERIFIED`.

- **Feature ID:** `ADM-BNK-002`
  - **Tên:** Giám sát giao dịch ngân hàng đồng bộ (Synced Bank Transactions Monitor)
  - **Mục đích:** Theo dõi dữ liệu biến động số dư thực tế được SePay chuyển giao vào hệ thống.
  - **Verification:** `SOURCE_VERIFIED`.

- **Feature ID:** `ADM-BNK-003`
  - **Tên:** Kiểm tra và xử lý sự kiện Webhook (Webhook Events Review)
  - **Mục đích:** Xem xét các gói tin webhook từ SePay gặp lỗi hoặc cần xác minh tính hợp lệ của chữ ký và nội dung chuyển khoản.
  - **Verification:** `SOURCE_VERIFIED`.
