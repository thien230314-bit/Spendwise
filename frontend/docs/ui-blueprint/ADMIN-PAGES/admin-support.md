# ADMIN PAGE INVENTORY: SUPPORT WORKBENCH (TRUNG TÂM PHIẾU HỖ TRỢ KHÁCH HÀNG)
**Page ID:** `ADMIN-SUPPORT`  
**Route:** `/admin/support`  
**Layout:** `AdminAppShell`  
**Quyền hạn:** `Role: SystemAdmin`  
**File Source:** `src/features/admin/AdminSupportPage.tsx`  
**Trạng thái Xác thực:** `SOURCE_VERIFIED`

---

## 1. MỤC ĐÍCH NGHIỆP VỤ & TỔNG QUAN
Trung tâm điều phối dịch vụ hỗ trợ khách hàng toàn diện. Quản lý luồng xử lý phiếu hỗ trợ theo trạng thái (Open, In Progress, Pending Customer, Closed), phân công chuyên viên và nhắn tin trực tiếp với người dùng.

---

## 2. KIỂM KÊ CÁC SECTION TRÊN TRANG

| Section ID | Tên Section | Component Source | Vị trí Bố cục | Trạng thái Hiển thị |
|---|---|---|---|---|
| `ADM-SPT-SEC-01` | Support Tickets Queue & Live Help Desk | `AdminSupportPage.tsx` lines 1-1693 | Thân trang toàn diện | Luôn hiển thị |

---

## 3. DANH MỤC TÍNH NĂNG CHI TIẾT (FEATURE INVENTORY)

### Section ADM-SPT-SEC-01: Support Tickets Queue & Live Help Desk
- **Feature ID:** `ADM-SPT-001`
  - **Tên:** Điều phối và phân loại phiếu hỗ trợ (Ticket Routing & Queue)
  - **Mục đích:** Gán phiếu theo danh mục (Tài khoản, Kỹ thuật, Giao dịch) và phân bổ cho nhân viên CS.
  - **Verification:** `SOURCE_VERIFIED`.

- **Feature ID:** `ADM-SPT-002`
  - **Tên:** Không gian nhắn tin giải đáp trực tiếp (Agent Reply Chat Workspace)
  - **Mục đích:** Trao đổi với khách hàng qua luồng tin nhắn, gửi câu trả lời mẫu (canned responses) và tệp đính kèm.
  - **Verification:** `SOURCE_VERIFIED`.

- **Feature ID:** `ADM-SPT-003`
  - **Tên:** Đóng phiếu và ghi nhận kết quả giải quyết (Close Ticket & Resolution)
  - **Mục đích:** Cập nhật kết luận giải quyết và chuyển trạng thái phiếu sang Closed.
  - **Verification:** `SOURCE_VERIFIED`.
