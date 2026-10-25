# SPENDWISE — ADMIN FRONTEND API HANDOFF CONTRACT

> **Cập nhật:** 26/10/2026 (Phase Final Gate: Post-E2E Contract Freeze)
> **Đối tượng sử dụng:** Frontend Admin Dashboard Engineer
> **Base URL:** `http://localhost:5158` (Dev) / Production Admin Gateway
> **Tài liệu tham chiếu:** `Admin.md`, `openapi_runtime_verified.json`, `ADMIN-A1` đến `ADMIN-A6` Contracts

---

## 1. NGUYÊN TẮC BẢO MẬT VÀ PHÂN QUYỀN ADMIN

- **Hệ thống phân quyền chuẩn:** Chỉ hỗ trợ vai trò `Admin` (Operator) với chính sách `V1Authorization.OperationsRead`.
- **Tuyệt đối không có `SuperAdmin`:** Không tồn tại role `SuperAdmin` trong hệ thống; mọi truy cập yêu cầu SuperAdmin đều bị từ chối `401/403`.
- **Cơ chế xác thực Session:** Kiểm tra động bảng `UserSessions` tại mỗi request (`PlatformPermissionService`). Nếu phiên bị thu hồi (`RevokedAt != null`) hoặc User bị khóa, API lập tức trả về `401/403`.

---

## 2. QUẢN TRỊ VÀ TỔNG QUAN HỆ THỐNG (ADMIN A1 OVERVIEW)

### 2.1. Bảng Điều Khiển Tổng Quan (Operational Overview)
- **Endpoint:** `GET /api/v1/admin/overview?date=YYYY-MM-DD`
- **Quyền:** `OperationsRead`
- **Dữ liệu trả về:**
  - `date`: Ngày quan sát (theo múi giờ Asia/Ho_Chi_Minh).
  - `totalOrdinaryUsers`: Tổng số người dùng thông thường trong hệ thống.
  - `activeOrdinaryUsers`: Số người dùng đang hoạt động (`Active`).
  - `usersCreated`: Số tài khoản mới tạo trong ngày.
  - `groupsCreated`: Số nhóm/quỹ được tạo trong ngày.
  - `postingStatuses`: Thống kê số lượng giao dịch số cái theo trạng thái (`Completed`, `Pending`, v.v.).
  - `completedDebitVolumes`: Tổng khối lượng ghi nợ đã hoàn tất theo đơn vị tiền tệ (`VND`).
  - `sources`: Mảng metadata đối soát số liệu chi tiết.

### 2.2. Kiểm Tra Cảnh Báo Hệ Thống (Alerts)
- **Endpoint:** `GET /api/v1/admin/overview/alerts`
- **Mục đích:** Hiển thị danh sách cảnh báo thời gian thực về sự cố xử lý ngân hàng, tranh chấp tài chính chưa xử lý, và các ngoại lệ hệ thống.

---

## 3. QUẢN LÝ NGƯỜI DÙNG & TÀI KHOẢN (USER MANAGEMENT)

### 3.1. Tìm Kiếm Người Dùng (User Search)
- **Endpoint:** `POST /api/v1/admin/users/search`
- **Body:** `{ query: "string", reason: "Lý do tra cứu phục vụ kiểm toán" }`
- **Phản hồi:** Trả về danh sách người dùng với email đã che (`maskedEmail`: `s***@spendwise.test`), `spendId`, `displayName`, `status`, `createdAt`.
- **Lưu ý bảo mật:** Không bao giờ trả về trường `passwordHash`, `tokenHash`, hay `securityPinHash`.

### 3.2. Giám Sát Chi Tiết Trong Phạm Vi Hỗ Trợ (Case-Scoped Support Projections)
Khi Admin được phân công xử lý Support Case cho một User, Admin có thể đọc dữ liệu scoped:
- **Hồ sơ:** `POST /api/v1/admin/support/{caseId}/users/{targetId}/profile`
- **Phiên đăng nhập:** `POST /api/v1/admin/support/{caseId}/users/{targetId}/sessions`
- **Nhóm đã tham gia:** `POST /api/v1/admin/support/{caseId}/users/{targetId}/groups`
- **Bạn bè:** `POST /api/v1/admin/support/{caseId}/users/{targetId}/friends`
- **Lịch sử giao dịch:** `POST /api/v1/admin/support/{caseId}/users/{targetId}/history`
- **Hạn chế tài khoản:** `POST /api/v1/admin/support/{caseId}/users/{targetId}/restrictions`

---

## 4. XỬ LÝ KHIẾU NẠI & TRANH CHẤP QUỸ (SUPPORT CASES & DISPUTES)

### 4.1. Danh Sách Support Cases
- **Endpoint:** `GET /api/v1/admin/cases?status=...&page=1&pageSize=20`
- **Phân loại Case:**
  - `AccountAssistance`: Hỗ trợ tài khoản người dùng.
  - `V1FundDispute`: Tranh chấp phát sinh khi thành viên báo cáo khoản chi bất thường từ Quỹ Cố định (`/api/v1/funds/requests/{id}/report`).

### 4.2. Liên Kết Tranh Chấp Khoản Chi 1-1 (Exact Expense Correlation)
- Khi xử lý `V1FundDispute`, Admin có thể xem:
  - `LinkedTransactionId`: Khớp 1-1 với `FundRequest.Id`.
  - `Description`: Chứa chuỗi định danh chính xác `LedgerTransactionId` của đúng khoản chi bị khiếu nại.
- **Quy tắc giải quyết:** Admin có thể cập nhật trạng thái Case (`Investigating`, `Resolved`, `Rejected`). Hệ thống giữ nguyên số cái tài chính ban đầu cho đến khi có quyết định điều tra chính thức.

---

## 5. GIÁM SÁT NGÂN HÀNG & ĐỐI SOÁT (BANK INTEGRATION & RECONCILIATION)

### 5.1. Thống Kê Đối Soát Ngân Hàng
- **Endpoint:** `GET /api/admin/bank-integration/reconciliation`
- **Dữ liệu trả về:**
  - `activeConnections`: Số liên kết ngân hàng đang hoạt động.
  - `inactiveConnections`: Số liên kết tạm ngưng.
  - `processedEvents`: Số lượng sự kiện webhook ngân hàng đã xử lý thành công.
  - `duplicateDeliveryCount`: Số lần webhook gửi trùng lặp được phát hiện và khử trùng.
  - `unmatchedEvents`: Số sự kiện webhook không tìm thấy tài khoản ngân hàng tương ứng.
  - `failedEvents`: Số sự kiện webhook thất bại cần rà soát.

### 5.2. Danh Sách Sự Kiện Webhook (Bank Integration Events)
- **Endpoint:** `GET /api/admin/bank-integration/events?status=...&eventType=...&from=...&to=...`
- **Dữ liệu:** Mảng `AdminBankIntegrationEventDto` gồm `id`, `provider`, `externalEventId`, `externalTransactionId`, `eventType`, `processingStatus`, `receivedAt`.

---

## 6. KIỂM TRA SỨC KHỎE HỆ THỐNG & CẤU HÌNH (OPERATIONS)

- **Health Check Hệ Thống:** `GET /health` -> `{ status: "Healthy" }`.
- **Giám Sát Bảo Trì:** `GET /api/v1/admin/maintenance` -> Thông tin các khung giờ bảo trì hệ thống.
