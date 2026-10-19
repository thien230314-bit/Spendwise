# USER PAGE INVENTORY: SUPPORT & DISPUTES (HỖ TRỢ & KHIẾU NẠI)
**Page ID:** `USER-SUPPORT` & `USER-DISPUTES`  
**Route:** `/support` (mặc định tab Yêu cầu) & `/disputes` (mặc định tab Khiếu nại)  
**Layout:** `AppShell` (User Layout)  
**File Source:** `src/features/support/SupportDisputesPage.tsx` (1812 dòng)  
**Trạng thái Xác thực:** `SOURCE_VERIFIED`

---

## 1. MỤC ĐÍCH NGHIỆP VỤ & TỔNG QUAN
Cổng liên lạc và bảo vệ quyền lợi người dùng: gửi yêu cầu hỗ trợ kỹ thuật/tài khoản (Support Tickets/Cases), tạo khiếu nại tranh chấp giao dịch tài chính (Transaction Disputes), đính kèm chứng từ sao kê, theo dõi dòng thời gian giải quyết tranh chấp (Dispute Timeline) và trao đổi tin nhắn trực tiếp với đội ngũ Quản trị viên/Chuyên viên hỗ trợ.

---

## 2. KIỂM KÊ CÁC SECTION TRÊN TRANG

| Section ID | Tên Section | Component Source | Vị trí Bố cục | Trạng thái Hiển thị |
|---|---|---|---|---|
| `SUP-SEC-01` | Header & Two Primary Tabs Navigation | `SupportDisputesPage.tsx` lines 230-310 | Đầu trang (Top header) | Luôn hiển thị |
| `SUP-SEC-02` | Support Cases Tickets Management | `SupportDisputesPage.tsx` lines 320-620 | Tab Hỗ trợ (`cases`) | Khi `activeTab === 'cases'` |
| `SUP-SEC-03` | Financial Disputes Management | `SupportDisputesPage.tsx` lines 630-950 | Tab Khiếu nại (`disputes`) | Khi `activeTab === 'disputes'` |
| `SUP-SEC-04` | Case & Dispute Message Thread Drawer | `SupportDisputesPage.tsx` lines 960-1350 | Drawer bên phải | Khi xem chi tiết 1 yêu cầu |
| `SUP-SEC-05` | Create Support Ticket Modal | `SupportDisputesPage.tsx` lines 1360-1550 | Modal Layer | Theo trigger |
| `SUP-SEC-06` | File Dispute Against Tx Modal | `SupportDisputesPage.tsx` lines 1560-1800 | Modal Layer | Theo trigger |

---

## 3. DANH MỤC TÍNH NĂNG CHI TIẾT (FEATURE INVENTORY)

### Section SUP-SEC-01: Header & Tabs
- **Feature ID:** `SUPPORT-001`
  - **Tên:** Chuyển đổi Tab Yêu cầu hỗ trợ vs Tab Khiếu nại giao dịch
  - **Mục đích:** Đồng bộ với route `/support` (default: cases) hoặc `/disputes` (default: disputes).
  - **Verification:** `SOURCE_VERIFIED`.

### Section SUP-SEC-02: Support Cases
- **Feature ID:** `SUPPORT-002`
  - **Tên:** Tạo yêu cầu hỗ trợ mới (+ Gửi yêu cầu)
  - **Mục đích:** Gửi thắc mắc về tài khoản, lỗi chức năng hoặc tư vấn, kèm tệp ảnh chụp màn hình.
  - **API Call:** `casesApiService.createCase({ title, category, priority, description, attachments })`.
  - **Verification:** `SOURCE_VERIFIED`.

- **Feature ID:** `SUPPORT-003`
  - **Tên:** Danh sách yêu cầu hỗ trợ & Trạng thái xử lý
  - **Mục đích:** Hiển thị mã phiếu (`CAS-xxxxx`), mức độ ưu tiên (Thấp/Vừa/Cao), trạng thái (Mới/Đang xử lý/Đã giải quyết/Đã đóng).
  - **Verification:** `SOURCE_VERIFIED`.

### Section SUP-SEC-03: Financial Disputes
- **Feature ID:** `SUPPORT-004`
  - **Tên:** Tạo khiếu nại giao dịch (+ Tạo khiếu nại)
  - **Mục đích:** Khởi tạo tranh chấp khi phát hiện giao dịch bị trừ tiền sai, chuyển nhầm hoặc nghi ngờ gian lận.
  - **UI Elements:** Chọn giao dịch nghi vấn từ danh sách giao dịch gần đây, nhập lý do tranh chấp, số tiền đòi bồi hoàn, đính kèm chứng từ ngân hàng.
  - **API Call:** `casesApiService.createDispute({ transactionId, reason, requestedAmount, evidence })`.
  - **Verification:** `SOURCE_VERIFIED`.

- **Feature ID:** `SUPPORT-005`
  - **Tên:** Danh sách khiếu nại & Thước đo tiến trình giải quyết
  - **Mục đích:** Thể hiện tiến độ điều tra của Admin (Tiếp nhận -> Đang thẩm tra -> Yêu cầu bổ sung -> Đã hoàn tiền / Bác bỏ).
  - **Verification:** `SOURCE_VERIFIED`.

### Section SUP-SEC-04: Thread Messages
- **Feature ID:** `SUPPORT-006`
  - **Tên:** Trao đổi tin nhắn hai chiều với Admin (Support Chat Thread)
  - **Mục đích:** Nhắn tin trực tiếp với nhân viên hỗ trợ, gửi thêm bằng chứng chứng từ trong luồng trao đổi.
  - **API Calls:** `casesApiService.getCaseMessages(id)`, `sendMessage(id, message)`.
  - **Verification:** `SOURCE_VERIFIED`.
