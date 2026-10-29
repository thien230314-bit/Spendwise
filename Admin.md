# SPENDWISE V1.0 — ADMIN BACKEND CONTRACT & OPERATING SPECIFICATION

> **Thời điểm xác nhận nghiệm thu:** 29/10/2026 — 12:28:00+07:00  
> **Trạng thái cốt lõi:** `BACKEND ADMIN V1.0 — LOCKED & VERIFIED 100% PASS (80/80 REQUESTS, 138/138 ASSERTIONS, 0 FAIL)`  
> **Bộ kiểm thử Hỗ trợ & Khiếu nại:** `SUPPORT & DIRECT COMPLAINT E2E — 22/22 REQUESTS, 42/42 ASSERTIONS PASS (100%)`  
> **Môi trường nghiệm thu:** Microsoft SQL Server `DESKTOP-B5KA3LT\SQLEXPRESS02` / `SpendWise_E2E_G4` (SQL02 cách ly tuyệt đối, không kết nối SQL01).  
> **Báo cáo kiểm thử chính thức:** [`ADMIN-V1-FINAL-NEWMAN-REPORT.json`](./ADMIN-V1-FINAL-NEWMAN-REPORT.json) và [`reports/newman/support_complaint_e2e_newman_report.json`](./reports/newman/support_complaint_e2e_newman_report.json).  
> **Ranh giới công nghệ:**
> - **Backend Admin Core:** Đã kiểm thử tự động toàn diện đủ 5 nhóm lớn, 21 chức năng quản trị (ADM-01 đến ADM-05), cơ chế kiểm soát truy cập thông tin nhạy cảm theo Hồ sơ điều tra (`Case-Scoped Access`), 22 chốt bảo mật (SEC-01 $\rightarrow$ SEC-22), và vòng đời phát hành thông báo Broadcast đa kênh.
> - **Chính sách Quản trị viên độc tôn:** Duy nhất tài khoản `lethien241205@gmail.com` với vai trò `SystemRole.Admin = 2` được cấp quyền truy cập. Các vai trò legacy `SystemAdmin = 0` và `SuperAdmin = 3` bị từ chối triệt để ngay cả khi token mang chữ ký hợp lệ (`403 Forbidden` `legacy_role_rejected`).
> - **Dịch vụ Ngoại vi (Gemini AI & SePay):** Nghiệm thu theo **Phương án B (Telemetry, Giám sát vận hành & Sandbox Test Mode)**. Cổng nạp/rút tiền thật và hạn ngạch trực tiếp không giới hạn của bên thứ ba được bảo lưu trạng thái `NOT VERIFIED (BY SAFETY POLICY)`.

---

## 1. VAI TRÒ VÀ RANH GIỚI BẢO MẬT (ROLES & SECURITY BOUNDARIES)

### 1.1. Chính sách Quản trị viên duy nhất (Single Admin Policy)
- Hệ thống SpendWise V1.0 thiết lập **duy nhất một tài khoản Quản trị viên hệ thống hoạt động**:
  - **Email:** `lethien241205@gmail.com`
  - **Role:** `SystemRole.Admin = 2`
  - **User ID:** `A6D30123-AE6E-43B1-A3D3-0185FF8A46EC`
- **Loại bỏ hoàn toàn SuperAdmin & SystemAdmin:**
  - Tuyệt đối không còn quyền SuperAdmin trên hệ thống. Mọi nỗ lực gọi endpoint nâng cấp quản trị viên hoặc cấp Admin thứ hai (`/api/v1/admin/operators/*`) đều bị vô hiệu hóa với mã lỗi `403 Forbidden` (`operator_management_disabled`).
  - Mọi JWT token mang claim role 0 hoặc 3 đều bị chặn ngay tại middleware với lỗi `legacy_role_rejected`.
- **Phân định ranh giới Chủ quỹ (Fund Owner) và Quản trị viên (Admin):**
  - Chủ quỹ (`Fund Owner`) là người dùng thông thường (`SystemRole.User`) có quyền quản trị trong quỹ mình sở hữu; hoàn toàn **không có quyền Admin hệ thống**.
  - Quản trị viên hệ thống không can thiệp số dư cá nhân hoặc quỹ nhóm nếu không có hồ sơ khiếu nại hợp lệ.

### 1.2. Ranh giới truy cập nhạy cảm theo Hồ sơ hỗ trợ (Case-Scoped Access)
- **Nguyên tắc không đọc tự do (Zero Unbounded Reads):**
  - Admin **không có quyền đọc toàn cục** dữ liệu số dư ví, danh sách bạn bè, hoặc lịch sử giao dịch cá nhân của người dùng.
  - Mọi thao tác tra cứu hồ sơ người dùng, phiên thiết bị, nhóm quỹ, hoặc lịch sử giao dịch **bắt buộc phải gắn liền với một Hồ sơ hỗ trợ đang mở (`active SupportCase`)** được phân công trực tiếp cho Admin đó:
    - `POST /api/v1/admin/support/{caseId}/users/{userId}/profile`
    - `POST /api/v1/admin/support/{caseId}/users/{userId}/sessions`
    - `POST /api/v1/admin/support/{caseId}/users/{userId}/restrictions`
    - `POST /api/v1/admin/support/{caseId}/users/{userId}/revoke-sessions`
    - `POST /api/v1/admin/support/{caseId}/users/{userId}/groups`
    - `POST /api/v1/admin/support/{caseId}/users/{userId}/history`
  - Mỗi truy cập bắt buộc phải truyền lý do nghiệp vụ (`reason`) và được ghi nhật ký kiểm toán (`AuditLog`) bất biến.
- **Bảo mật tuyệt đối:** Admin không bao giờ thấy mật khẩu thô, mã PIN, mã OTP, khóa bí mật sinh trắc học, hoặc token JWT của người dùng. Admin không có quyền sửa trực tiếp số dư tài khoản sổ cái (`LedgerAccount.Balance`).

---

## 2. NĂM NHÓM CHỨC NĂNG ADMIN V1.0 (21 CHỨC NĂNG TỔNG THỂ)

```
┌─────────────────────────────────────────────────────────────────────────────────────────────┐
│                            HỆ THỐNG QUẢN TRỊ SPENDWISE V1.0                                │
├──────────────────────────┬──────────────────────────┬───────────────────────────────────────┤
│ ADM-01: TỔNG QUAN        │ ADM-02: QUẢN LÝ USER     │ ADM-03: HỖ TRỢ & KHIẾU NẠI            │
│ 1.1 Thống kê Dashboard   │ 2.1 Tra cứu & Phân trang │ 3.1 Tiếp nhận & Phân loại Hồ sơ       │
│ 1.2 Tổng hợp Quỹ & GD    │ 2.2 Xem Hồ sơ theo Case  │ 3.2 Xem Chi tiết & Lịch sử Trao đổi   │
│ 1.3 Quản lý Tác vụ       │ 2.3 Xem Phiên theo Case  │ 3.3 Phản hồi & Yêu cầu Bổ sung        │
│ 1.4 Cảnh báo Hệ thống    │ 2.3b Thu hồi Phiên (All) │ 3.4 Cập nhật Trạng thái Hồ sơ         │
│                          │ 2.4 Đổi Trạng thái User  │ 3.5 Luồng Xử lý Khiếu nại Trực tiếp   │
├──────────────────────────┴──────────────────────────┼───────────────────────────────────────┤
│ ADM-04: THÔNG BÁO VẬN HÀNH                          │ ADM-05: HỆ THỐNG & NGOẠI VI           │
│ 4.1 Lịch sử Chuyển phát theo Case                   │ 5.1 Giám sát Sức khỏe Toàn diện       │
│ 4.2 Báo cáo Hàng đợi Outbox                         │ 5.2 Giám sát Trợ lý Gemini AI         │
│ 4.3 Danh mục Mẫu Thông báo Chuẩn                    │ 5.3 Giám sát Cổng Ngân hàng SePay     │
│ 4.4 Soạn & Phát hành Broadcast (Full Lifecycle)     │ 5.4 Quản lý Chế độ Bảo trì            │
│                                                     │ 5.5 Nhật ký Kiểm toán & Feature Flags │
└─────────────────────────────────────────────────────┴───────────────────────────────────────┘
```

### ADM-01: Tổng quan Hệ thống (System Overview & Dashboards)
- **ADM-01.1 — Thống kê User & Trạng thái Dashboard (`GET /api/admin/dashboard`):** Trả về tổng người dùng, số lượng active, số lượng locked, tổng số dư toàn hệ thống và các chỉ số tài chính trọng yếu.
- **ADM-01.2 — Tổng hợp Giao dịch & Quỹ nhóm Overview (`GET /api/v1/admin/overview`):** Báo cáo số lượng giao dịch, biến động quỹ nhóm trong ngày làm việc theo giờ Việt Nam (UTC+7).
- **ADM-01.3 — Quản lý Tác vụ Vận hành (`GET /api/v1/admin/overview/tasks`):** Danh sách các tác vụ cần phê duyệt, hồ sơ khiếu nại chờ xử lý, phân trang tiêu chuẩn.
- **ADM-01.4 — Cảnh báo Hệ thống & Hoạt động Gần đây (`GET /api/v1/admin/overview/alerts`):** Dòng sự kiện cảnh báo bảo mật thời gian thực, sự cố phát sinh và hoạt động bất thường.

### ADM-02: Quản lý Người dùng & Chế tài Tài khoản (User Management)
- **ADM-02.1 — Tra cứu & Phân trang (`GET /api/admin/users?search=...`):** Tìm kiếm người dùng theo Email, Tên, Số điện thoại hoặc SpendId, phân trang dữ liệu.
- **ADM-02.2 — Xem Hồ sơ Chi tiết theo Case (`POST /api/v1/admin/support/{caseId}/users/{userId}/profile`):** Trả về hồ sơ che phủ thông tin nhạy cảm (Masked Email/Phone), danh sách ví liên kết và trạng thái hiện tại.
- **ADM-02.3 — Xem Phiên Đăng nhập theo Case (`POST /api/v1/admin/support/{caseId}/users/{userId}/sessions`):** Liệt kê các phiên làm việc, thiết bị và IP của người dùng trong phạm vi điều tra.
- **ADM-02.3b — Thu hồi Toàn bộ Phiên Làm việc (`POST /api/admin/users/{userId}/revoke-sessions`):** Vô hiệu hóa ngay lập tức toàn bộ refresh token và phiên thiết bị của người dùng, trả `204 NoContent`.
- **ADM-02.4 — Áp dụng Chế tài Tài khoản (`PUT /api/admin/users/{userId}/status`):**
  - Yêu cầu: `adminPin` (Mã PIN của Admin), `status`, `reason`.
  - **Hai mức độ chế tài:**
    - `UserStatus.Restricted`: Chặn lệnh ghi nợ tiền ra (`403 account_restricted`), vẫn cho phép đọc số dư, liên hệ hỗ trợ và nhận tiền vào.
    - `UserStatus.Locked`: Thu hồi phiên tức thì, chặn đăng nhập và refresh token (`403 account_unavailable`). Sau khi mở khóa (`Active`), người dùng phải đăng nhập lại từ đầu.

### ADM-03: Hỗ trợ Khách hàng & Khiếu nại Giao dịch (Support & Complaints)
- **ADM-03.1 — Tiếp nhận & Phân loại Hồ sơ (`GET /api/admin/cases/support`):** Đọc danh sách phiếu hỗ trợ và khiếu nại của hệ thống với các bộ lọc trạng thái (`open`, `in_progress`, `resolved`, `rejected`).
- **ADM-03.2 — Chi tiết Hồ sơ & Lịch sử Trao đổi (`GET /api/admin/cases/support/{id}`):** Xem toàn bộ nội dung khiếu nại, giao dịch liên quan (`linkedTransactionId`), biên lai đối soát và lịch sử tin nhắn.
- **ADM-03.3 — Phản hồi & Yêu cầu Bổ sung Thông tin (`POST /api/admin/cases/support/{id}/messages`):** Gửi thông điệp phản hồi từ Admin đến người dùng.
- **ADM-03.4 — Cập nhật Tiến độ & Kết luận Hồ sơ (`POST /api/admin/cases/support/{id}/status`):** Chuyển trạng thái hồ sơ sang `in_progress`, `need_more_info`, `resolved`, hoặc `rejected`.
- **ADM-03.5 — Luồng Xử lý Khiếu nại Giao dịch Trực tiếp (Full E2E):** Chuỗi liên hoàn từ lúc User tạo khiếu nại giao dịch $\rightarrow$ Admin tiếp nhận điều tra $\rightarrow$ Admin yêu cầu bổ sung $\rightarrow$ User phản hồi $\rightarrow$ Admin giải quyết $\rightarrow$ User nhận thông báo kết quả.
- *Quy tắc an toàn:* Endpoint tạo Dispute độc lập cũ (`POST /api/cases/disputes`) bị chặn với mã `400 Bad Request` (`dispute_workflow_deprecated`), toàn bộ dữ liệu lịch sử được bảo tồn an toàn.

### ADM-04: Quản lý Thông báo Vận hành (Notifications & Broadcasts)
- **ADM-04.1 — Lịch sử Chuyển phát Thông báo theo Case (`POST /api/v1/admin/notifications/delivery-history`):** Truy vết các thông báo hệ thống đã phát cho người dùng trong phạm vi điều tra.
- **ADM-04.2 — Báo cáo Trạng thái Hàng đợi Outbox (`GET /api/admin/health`):** Giám sát hàng đợi tin nhắn `OutboxMessages` (số lượng tin chờ xử lý, số lượng tin lỗi).
- **ADM-04.3 — Danh mục Mẫu Thông báo Chuẩn (`POST /api/v1/admin/notifications/templates`):** Danh sách các template thông báo chuẩn hóa có sẵn trong hệ thống.
- **ADM-04.4 — Vòng đời Phát hành Thông báo Broadcast Toàn diện (Full E2E Lifecycle):**
  1. Admin soạn thảo thông báo dạng nháp (`status: "Draft"`).
  2. Admin phát hành thông báo tức thời (`POST /api/v1/system/broadcasts/{id}/publish`) kèm `adminPin`.
  3. Worker chuyển phát qua Outbox Queue thành công.
  4. Người dùng mở ứng dụng và nhận thông báo trong hộp thư (`GET /api/notifications`).
  5. Người dùng đánh dấu đã đọc (`POST /api/notifications/{id}/read` trả `204 NoContent`).
  6. Admin kiểm tra trạng thái broadcast: xác nhận số lượt người dùng đã đọc $\ge 1$.

### ADM-05: Hệ thống & Giám sát Ngoại vi (System Health & Integration Monitoring)
- **ADM-05.1 — Giám sát Sức khỏe Toàn diện (`GET /api/admin/health`):** Báo cáo trạng thái hoạt động của Web API, Microsoft SQL Server, SignalR Hub, Background Workers và Outbox Queue.
- **ADM-05.2 — Giám sát & Telemetry Trí tuệ Nhân tạo Gemini AI:**
  - Báo cáo telemetry thời gian thực qua `GET /api/admin/health`.
  - Fallback an toàn `SpendWisePolicy`: Khi gặp sự cố mạng hoặc cạn kiệt hạn ngạch, hệ thống tự động chuyển sang chính sách nội bộ thông minh, bảo toàn trải nghiệm người dùng với độ trễ tối ưu.
  - Quản lý phiên hội thoại và bộ nhớ ngữ cảnh qua `POST /api/admin/ai/chat`.
- **ADM-05.3 — Giám sát Cổng Thanh toán SePay Sandbox:**
  - Giám sát trạng thái tích hợp qua `GET /api/admin/integrations/sepay/status`.
  - Hiển thị đúng thực tế Sandbox (`configured: false, ipnConfigured: false`).
  - Đối soát giao dịch mô phỏng qua `GET /api/admin/bank-integration/reconciliation`.
  - Giám sát webhook events ngân hàng qua `GET /api/admin/bank-integration/events`.
- **ADM-05.4 — Quản lý Chế độ Bảo trì Hệ thống (`GET /api/system/maintenance`, `POST /api/v1/system/maintenance/toggle`):** Kiểm tra và kích hoạt cờ bảo trì toàn hệ thống khi nâng cấp hạ tầng.
- **ADM-05.5 — Nhật ký Kiểm toán & Quản trị Cờ Tính năng:**
  - `GET /api/admin/audit-logs`: Luồng nhật ký bất biến ghi nhận toàn bộ thao tác quản trị.
  - `GET /api/admin/feature-flags`: Danh mục và trạng thái các cờ tính năng hệ thống.

---

## 3. BẢNG CONTRACT API ADMIN CHI TIẾT & MÃ LỖI

| Phân Hệ | Method | Tuyến Đường (Route) | Yêu Cầu Header / Payload | Trạng Thái Thành Công | Mã Lỗi & Ý Nghĩa |
|---|:---:|---|---|:---:|---|
| **ADM-01** | `GET` | `/api/admin/dashboard` | Bearer Admin | `200 OK` | `403 Forbidden` (User thường không được truy cập) |
| | `GET` | `/api/v1/admin/overview` | Bearer Admin | `200 OK` | `401 Unauthorized` |
| | `GET` | `/api/v1/admin/overview/tasks` | Bearer Admin | `200 OK` | `401 Unauthorized` |
| | `GET` | `/api/v1/admin/overview/alerts` | Bearer Admin | `200 OK` | `401 Unauthorized` |
| **ADM-02** | `GET` | `/api/admin/users` | Bearer Admin (Query: `search, page, pageSize`) | `200 OK` | `401 Unauthorized` |
| | `POST`| `/api/v1/admin/support/{caseId}/users/{userId}/profile` | Bearer Admin + `{ reason: "Điều tra" }` | `200 OK` | `404 CaseNotFound / UserNotFound` |
| | `POST`| `/api/v1/admin/support/{caseId}/users/{userId}/sessions` | Bearer Admin + `{ reason: "Kiểm tra phiên" }` | `200 OK` | `400 MissingReason` |
| | `POST`| `/api/admin/users/{userId}/revoke-sessions` | Bearer Admin | `204 NoContent` | `404 UserNotFound` |
| | `PUT` | `/api/admin/users/{userId}/status` | Bearer Admin + `{ status, reason, adminPin }` | `200 OK` | `400 InvalidAdminPin`, `403 CannotSanctionAdmin` |
| **ADM-03** | `GET` | `/api/admin/cases/support` | Bearer Admin (Query: `status, category, page`) | `200 OK` | `401 Unauthorized` |
| | `GET` | `/api/admin/cases/support/{id}` | Bearer Admin | `200 OK` | `404 CaseNotFound` |
| | `POST`| `/api/admin/cases/support/{id}/messages` | Bearer Admin + `{ message: "Phản hồi" }` | `200 OK` | `404 CaseNotFound` |
| | `POST`| `/api/admin/cases/support/{id}/status` | Bearer Admin + `{ status: "resolved", resolutionNote }` | `200 OK` | `400 InvalidStatusTransition` |
| | `GET` | `/api/admin/cases/disputes` | Bearer Admin | `200 OK` | Danh mục chỉ đọc hồ sơ lịch sử |
| **ADM-04** | `POST`| `/api/v1/admin/notifications/delivery-history` | Bearer Admin + `{ reason: "Audit" }` | `200 OK` | `400 MissingReason` |
| | `POST`| `/api/v1/admin/notifications/templates` | Bearer Admin | `200 OK` | `401 Unauthorized` |
| | `POST`| `/api/v1/system/broadcasts` | Bearer Admin + `{ title, message, targetRoles }` | `201 Created` | `400 InvalidBroadcast` |
| | `POST`| `/api/v1/system/broadcasts/{id}/publish` | Bearer Admin + `{ adminPin }` | `200 OK` | `400 InvalidAdminPin`, `409 AlreadyPublished` |
| | `GET` | `/api/v1/system/broadcasts/{id}/status` | Bearer Admin | `200 OK` | `404 BroadcastNotFound` |
| **ADM-05** | `GET` | `/api/admin/health` | Bearer Admin | `200 OK` | Báo cáo chi tiết component health |
| | `POST`| `/api/admin/ai/chat` | Bearer Admin + `{ message: "Kiểm tra" }` | `200 OK` | `503 AiUnavailable` (tự động fallback) |
| | `GET` | `/api/admin/integrations/sepay/status` | Bearer Admin | `200 OK` | Báo cáo sandbox status |
| | `GET` | `/api/admin/bank-integration/reconciliation` | Bearer Admin | `200 OK` | Đối soát giao dịch ngân hàng |
| | `GET` | `/api/admin/bank-integration/events` | Bearer Admin | `200 OK` | Nhật ký webhook events |
| | `GET` | `/api/system/maintenance` | Anonymous / Bearer | `200 OK` | `{ isUnderMaintenance: false }` |
| | `GET` | `/api/admin/audit-logs` | Bearer Admin | `200 OK` | `401 Unauthorized` |
| | `GET` | `/api/admin/feature-flags` | Bearer Admin | `200 OK` | `401 Unauthorized` |
| **Legacy** | `ALL` | `/api/v1/admin/operators/*` | Mọi token | `403 Forbidden` | `operator_management_disabled` |
| | `POST`| `/api/cases/disputes` | Mọi token | `400 Bad Request` | `dispute_workflow_deprecated` |

---

## 4. HƯỚNG DẪN TRIỂN KHAI FRONTEND ADMIN CHO CODEX

1. **Giao diện Quản trị Chuyên biệt:**
   - Xây dựng layout Admin riêng biệt (Admin Portal Layout), có Sidebar điều hướng đủ 5 nhóm chức năng: Tổng quan, Người dùng, Hỗ trợ/Khiếu nại, Thông báo vận hành, Giám sát hệ thống.
   - Không tích hợp màn hình quản lý Operator hay phân quyền Admin cấp cao; hệ thống chỉ có 1 Admin duy nhất.
2. **Xử lý Mã PIN Quản trị (`adminPin`):**
   - Các thao tác nhạy cảm (Khóa/Mở khóa người dùng, Phát hành thông báo Broadcast toàn hệ thống, Kích hoạt chế độ bảo trì) yêu cầu popup nhập mã PIN 6 số của Admin trước khi gửi request.
3. **Luồng Điều tra theo Case Hồ sơ (Scoped Investigation Modal):**
   - Khi Admin xem hồ sơ người dùng từ danh sách User, nếu truy cập vào các tab nhạy cảm (Lịch sử giao dịch, Phiên hoạt động, Nhóm quỹ), hiển thị modal yêu cầu chọn một `Hồ sơ Hỗ trợ đang mở (SupportCase)` và nhập `Lý do điều tra (reason)`.
4. **Giám sát Trạng thái Thực tế:**
   - Trên trang Giám sát Tích hợp SePay, hiển thị rõ ràng nhãn `Môi trường Thử nghiệm Sandbox (Test Mode)`, không hiển thị trạng thái kết nối cổng thanh toán tiền thật khi chưa cấu hình.
   - Trên trang Trợ lý Gemini AI, hiển thị thẻ telemetry chỉ số thành công, độ trễ phản hồi và trạng thái chế độ dự phòng `SpendWisePolicy Fallback`.
