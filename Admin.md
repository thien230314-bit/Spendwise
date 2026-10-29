# SPENDWISE V1.0 — ĐẶC TẢ NGHIỆP VỤ QUẢN TRỊ ADMIN VÀ VẬN HÀNH HỆ THỐNG

> **Phiên bản:** V1.0 (Production Candidate)  
> **Phạm vi tài liệu:** Thuần đặc tả nghiệp vụ quản trị, 21 chức năng vận hành (5 nhóm), API contracts, kiểm soát truy cập và ranh giới hệ thống ngoại vi.  
> **Trạng thái Backend:** Đã khóa nghiệp vụ (Backend Admin Locked), 100% endpoint được kiểm thử hồi quy tự động.

---

## 1. NGUYÊN TẮC BẢO MẬT & PHÂN QUYỀN QUẢN TRỊ (SECURITY RULES)

### 1.1. Chính sách Quản trị viên duy nhất (Single Admin Policy)
1. **Duy nhất một tài khoản Quản trị viên hệ thống:**
   - **Tài khoản định danh:** `lethien241205@gmail.com`
   - **Vai trò:** `SystemRole.Admin = 2`
   - Khóa duy nhất ở cấp độ cơ sở dữ liệu và logic ứng dụng: Ngăn chặn tuyệt đối việc tạo tài khoản Admin thứ hai (`403 Forbidden` `operator_management_disabled`).
2. **Loại bỏ triệt để các vai trò kế thừa (Legacy Roles Elimination):**
   - Các vai trò cũ `SystemAdmin = 0` và `SuperAdmin = 3` bị từ chối quyền quản trị ngay tại tầng lọc bảo mật (`AdminSecurityGuard`), bất kể token JWT có chữ ký hợp lệ hay không (`403 Forbidden` `legacy_role_rejected`).
3. **Bảo mật đa lớp cho Admin:**
   - Đăng nhập yêu cầu Mật khẩu quản trị + Mã PIN Admin chuyên biệt (`AdminPin`).
   - Mọi thao tác nhạy cảm (khóa người dùng, hoàn tiền khiếu nại, phát hành broadcast) đều yêu cầu xác thực lại mã PIN quản trị.

### 1.2. Kiểm soát Truy cập Dữ liệu Nhạy cảm theo Hồ sơ Hỗ trợ (Case-Scoped Access)
1. **Nguyên tắc không đọc dữ liệu tùy tiện (Zero Unbounded Access):**
   - Admin không có quyền truy cập tùy tiện vào số dư chi tiết, danh sách bạn bè hay lịch sử giao dịch cá nhân của người dùng nếu không có yêu cầu nghiệp vụ rõ ràng.
   - Thao tác xem hồ sơ, xem phiên đăng nhập, xem quỹ nhóm hoặc lịch sử tài chính của người dùng **bắt buộc phải gắn liền với một Hồ sơ hỗ trợ / khiếu nại đang hoạt động (`active SupportCase`)** và truyền lý do điều tra (`reason`):
     - `POST /api/v1/admin/support/{caseId}/users/{userId}/profile`
     - `POST /api/v1/admin/support/{caseId}/users/{userId}/sessions`
     - `POST /api/v1/admin/support/{caseId}/users/{userId}/groups`
     - `POST /api/v1/admin/support/{caseId}/users/{userId}/history`
2. **Nhật ký kiểm toán bất biến (Immutable Audit Trail):**
   - Mọi hành động của Admin được ghi nhận vào bảng `AdminAuditLogs`: thời điểm (`UtcTimestamp`), địa chỉ IP, User-Agent, hành động (`Action`), đối tượng tác động (`TargetEntity`), dữ liệu trước và sau thao tác (`BeforeState`, `AfterState`).
   - Admin không có quyền sửa hoặc xóa bản ghi Audit Log.

---

## 2. MA TRẬN 21 CHỨC NĂNG QUẢN TRỊ ADMIN THEO 5 NHÓM

| Mã CN | Nhóm Chức Năng | Tên Chức Năng | Mục Tiêu & Nghiệp Vụ Chính | API Endpoint |
|---|---|---|---|---|
| **ADM-01** | Nhóm 1: Tổng quan & Vận hành | 1. Thống kê Dashboard Vận hành | Thống kê số lượng người dùng, số vụ việc chờ xử lý, cảnh báo hệ thống | `GET /api/v1/admin/operations/dashboard` |
| **ADM-02** | Nhóm 1: Tổng quan & Vận hành | 2. Tổng hợp Quỹ & Giao dịch | Giám sát tổng biến động dòng tiền, quỹ nhóm đang hoạt động | `GET /api/v1/admin/operations/funds-summary` |
| **ADM-03** | Nhóm 1: Tổng quan & Vận hành | 3. Quản lý Tác vụ Vận hành | Theo dõi hàng đợi tác vụ nền, trạng thái Outbox Processor | `GET /api/v1/admin/operations/tasks` |
| **ADM-04** | Nhóm 1: Tổng quan & Vận hành | 4. Cảnh báo Hệ thống Tức thời | Cảnh báo khi có giao dịch bất thường, ngưỡng lỗi hệ thống | `GET /api/v1/admin/operations/alerts` |
| **ADM-05** | Nhóm 2: Quản lý Người dùng | 5. Tra cứu & Phân trang Người dùng | Tìm kiếm theo Email, SĐT, SpendId, lọc theo trạng thái | `GET /api/v1/admin/users` |
| **ADM-06** | Nhóm 2: Quản lý Người dùng | 6. Xem Hồ sơ Người dùng (Case-Scoped) | Xem thông tin định danh và số dư gắn với mã hồ sơ hỗ trợ | `POST /api/v1/admin/support/{caseId}/users/{userId}/profile` |
| **ADM-07** | Nhóm 2: Quản lý Người dùng | 7. Xem Danh sách Phiên (Case-Scoped) | Xem danh sách thiết bị và phiên đăng nhập của người dùng | `POST /api/v1/admin/support/{caseId}/users/{userId}/sessions` |
| **ADM-08** | Nhóm 2: Quản lý Người dùng | 8. Thu hồi Phiên (Revoke Sessions) | Cưỡng chế đăng xuất tất cả phiên của người dùng có dấu hiệu rủi ro | `POST /api/v1/admin/support/{caseId}/users/{userId}/revoke-sessions` |
| **ADM-09** | Nhóm 2: Quản lý Người dùng | 9. Điều chỉnh Trạng thái Người dùng | Chuyển trạng thái giữa `Active`, `Restricted` và `Locked` | `PUT /api/v1/admin/users/{userId}/status` |
| **ADM-10** | Nhóm 2: Quản lý Người dùng | 10. Đặt lại Mật khẩu / PIN An toàn | Kích hoạt gửi email khôi phục mật khẩu hoặc đặt lại PIN an toàn | `POST /api/v1/admin/users/{userId}/reset-pin` |
| **ADM-11** | Nhóm 3: Giám sát Sổ cái & GD | 11. Giám sát Bảng cân đối Sổ cái | Kiểm tra tính bất biến $\sum \text{Debit} = \sum \text{Credit}$ trên toàn hệ thống | `GET /api/v1/admin/ledger/trial-balance` |
| **ADM-12** | Nhóm 3: Giám sát Sổ cái & GD | 12. Tra cứu Giao dịch Toàn hệ thống | Tra cứu, đối soát giao dịch tài chính, lọc theo trạng thái và thời gian | `GET /api/v1/admin/transactions` |
| **ADM-13** | Nhóm 3: Giám sát Sổ cái & GD | 13. Nhật ký Kiểm toán (Audit Logs) | Truy vết lịch sử mọi thao tác quản trị, không thể chỉnh sửa | `GET /api/v1/admin/audit-logs` |
| **ADM-14** | Nhóm 4: Hỗ trợ & Khiếu nại | 14. Tiếp nhận & Phân loại Hồ sơ | Xem danh sách khiếu nại/hỗ trợ, lọc theo trạng thái (`Pending`, `InReview`) | `GET /api/v1/admin/support` |
| **ADM-15** | Nhóm 4: Hỗ trợ & Khiếu nại | 15. Chi tiết Hồ sơ & Lịch sử Trao đổi | Xem nội dung khiếu nại, giao dịch liên quan và lịch sử tin nhắn | `GET /api/v1/admin/support/{caseId}` |
| **ADM-16** | Nhóm 4: Hỗ trợ & Khiếu nại | 16. Phản hồi Người dùng | Gửi phản hồi chính thức hoặc yêu cầu bổ sung bằng chứng từ Admin | `POST /api/v1/admin/support/{caseId}/messages` |
| **ADM-17** | Nhóm 4: Hỗ trợ & Khiếu nại | 17. Phê duyệt Khiếu nại (`Resolved`) | Chấp thuận khiếu nại, kích hoạt hoàn tiền hoặc điều chỉnh trạng thái | `POST /api/v1/admin/support/{caseId}/resolve` |
| **ADM-18** | Nhóm 4: Hỗ trợ & Khiếu nại | 18. Từ chối Khiếu nại (`Rejected`) | Bác bỏ khiếu nại, nêu rõ căn cứ và lý do từ chối chính thức | `POST /api/v1/admin/support/{caseId}/reject` |
| **ADM-19** | Nhóm 5: Truyền thông & Ngoại vi | 19. Phát hành Thông báo Broadcast | Soạn và phát hành thông báo toàn hệ sinh thái theo chu trình Outbox | `POST /api/v1/admin/broadcasts` |
| **ADM-20** | Nhóm 5: Truyền thông & Ngoại vi | 20. Giám sát Vận hành Trợ lý Gemini AI | Đo lường số request, latency, tỷ lệ thành công và chuyển mạch fallback | `GET /api/v1/admin/system/gemini/monitoring` |
| **ADM-21** | Nhóm 5: Truyền thông & Ngoại vi | 21. Giám sát Webhook Ngân hàng SePay | Giám sát trạng thái kết nối IPN, đối soát sandbox, cảnh báo giao dịch ảo | `GET /api/v1/admin/system/sepay/monitoring` |

---

## 3. CHI TIẾT API CONTRACT PHÂN HỆ QUẢN TRỊ ADMIN

Header bắt buộc cho mọi Admin API:
```http
Content-Type: application/json
Authorization: Bearer <admin_access_token>
```

### 3.1. Nhóm 1: Tổng quan & Vận hành Hệ thống (Operations & Dashboard)

#### `GET /api/v1/admin/operations/dashboard`
- **Mô tả:** Trả về các chỉ số vận hành tổng quan: tổng số tài khoản (`Active`, `Restricted`, `Locked`), số vụ hỗ trợ đang chờ (`PendingSupportCases`), tỷ lệ lỗi giao dịch trong 24 giờ qua.
- **Quyền:** Admin duy nhất (`SystemRole.Admin = 2`).
- **Response `200 OK`:**
```json
{
  "totalUsers": 1250,
  "activeUsers": 1210,
  "restrictedUsers": 25,
  "lockedUsers": 15,
  "pendingSupportCases": 8,
  "systemHealthStatus": "Healthy",
  "lastUpdated": "2026-10-29T14:45:00Z"
}
```

#### `GET /api/v1/admin/operations/alerts`
- **Mô tả:** Danh sách các cảnh báo an ninh vận hành (đăng nhập thất bại hàng loạt, lỗi webhook SePay, lỗi kết nối Gemini AI).
- **Response `200 OK`:** Danh sách bản ghi cảnh báo kèm cấp độ rủi ro (`Info`, `Warning`, `Critical`).

---

### 3.2. Nhóm 2: Quản lý Người dùng & Trạng thái (User Governance)

#### `GET /api/v1/admin/users`
- **Mô tả:** Danh sách người dùng có phân trang và bộ lọc tìm kiếm.
- **Query Params:** `searchTerm=nguyen&status=Active&page=1&pageSize=20`
- **Response `200 OK`:**
```json
{
  "items": [
    {
      "id": "uuid-user-1",
      "email": "user1@example.com",
      "fullName": "Nguyen Van A",
      "phone": "0912345678",
      "spendId": "SPEND_123456",
      "status": "Active",
      "createdAt": "2026-10-20T08:00:00Z"
    }
  ],
  "totalCount": 1,
  "page": 1,
  "pageSize": 20
}
```

#### `POST /api/v1/admin/support/{caseId}/users/{userId}/profile`
- **Mô tả:** Xem chi tiết hồ sơ người dùng trong phạm vi vụ việc hỗ trợ đang thụ lý.
- **Request Body:**
```json
{
  "reason": "Điều tra khiếu nại chuyển tiền sai số dư"
}
```
- **Response `200 OK`:**
```json
{
  "userId": "uuid-user-1",
  "fullName": "Nguyen Van A",
  "email": "user1@example.com",
  "status": "Active",
  "totalBalance": 5000000.00,
  "walletsCount": 2,
  "linkedBankAccounts": [
    {
      "bankCode": "MB",
      "accountNumberMasked": "******5678",
      "accountHolderName": "NGUYEN VAN A"
    }
  ]
}
```
- **Mã lỗi:** `400 BadRequest` (`invalid_case_id_or_unassigned`), `403 Forbidden` (`unauthorized_case_scope`).

#### `PUT /api/v1/admin/users/{userId}/status`
- **Mô tả:** Cập nhật trạng thái tài khoản người dùng (`Active`, `Restricted`, `Locked`).
- **Quy tắc:**
  - Nếu chuyển sang `Locked`: Tự động thu hồi tức thời toàn bộ phiên (`UserSessions`) và Refresh Token (`RevokedAt = Now`).
  - Nếu chuyển sang `Restricted`: Người dùng bị chặn toàn bộ lệnh rút tiền, chuyển tiền, nộp quỹ.
- **Request Body:**
```json
{
  "status": "Locked",
  "reason": "Tài khoản có dấu hiệu gian lận theo hồ sơ CASE-1092",
  "adminPin": "{ adminPin }"
}
```
- **Response `200 OK`:**
```json
{
  "userId": "uuid-user-1",
  "previousStatus": "Active",
  "currentStatus": "Locked",
  "updatedAt": "2026-10-29T14:50:00Z"
}
```
- **Mã lỗi:** `400 BadRequest` (`invalid_admin_pin`), `404 NotFound` (`user_not_found`).

#### `POST /api/v1/admin/support/{caseId}/users/{userId}/revoke-sessions`
- **Mô tả:** Cưỡng chế đăng xuất toàn bộ thiết bị đang đăng nhập của người dùng.
- **Request Body:**
```json
{
  "reason": "Khách hàng báo mất điện thoại yêu cầu bảo vệ số dư"
}
```
- **Response `200 OK`:** `{ "revokedSessionsCount": 3, "status": "Success" }`

---

### 3.3. Nhóm 3: Giám sát Tài chính & Sổ cái Kép (Financial & Ledger Monitoring)

#### `GET /api/v1/admin/ledger/trial-balance`
- **Mô tả:** Bảng cân đối thử sổ cái kế toán kép toàn hệ thống. Xác minh tính cân bằng tuyệt đối giữa Nợ và Có.
- **Response `200 OK`:**
```json
{
  "totalDebit": 15820000000.00,
  "totalCredit": 15820000000.00,
  "isBalanced": true,
  "variance": 0.00,
  "checkedAt": "2026-10-29T14:52:00Z"
}
```

#### `GET /api/v1/admin/transactions`
- **Mô tả:** Tra cứu danh sách giao dịch tài chính toàn hệ thống.
- **Query Params:** `type=Transfer&status=Completed&page=1&pageSize=50`
- **Response `200 OK`:** Danh sách giao dịch tài chính có phân trang.

#### `GET /api/v1/admin/audit-logs`
- **Mô tả:** Báo cáo nhật ký kiểm toán hành động quản trị.
- **Query Params:** `page=1&pageSize=50&action=ChangeUserStatus`
- **Response `200 OK`:**
```json
{
  "items": [
    {
      "id": "uuid-log-1",
      "adminEmail": "lethien241205@gmail.com",
      "action": "ChangeUserStatus",
      "targetEntity": "User:uuid-user-1",
      "details": "Changed status from Active to Locked. Reason: Fraud report",
      "ipAddress": "127.0.0.1",
      "timestamp": "2026-10-29T14:50:00Z"
    }
  ]
}
```

---

### 3.4. Nhóm 4: Xử lý Hỗ trợ & Khiếu nại Trực tiếp (Support & Dispute Governance)

#### `GET /api/v1/admin/support`
- **Mô tả:** Lấy danh sách các yêu cầu hỗ trợ / khiếu nại của người dùng.
- **Query Params:** `status=Pending&page=1&pageSize=20`
- **Response `200 OK`:** Danh sách hồ sơ hỗ trợ.

#### `GET /api/v1/admin/support/{caseId}`
- **Mô tả:** Xem chi tiết một hồ sơ hỗ trợ: thông tin người khiếu nại, giao dịch liên quan, lịch sử đối thoại.
- **Response `200 OK`:**
```json
{
  "caseId": "uuid-case-1092",
  "status": "InReview",
  "userId": "uuid-user-1",
  "userFullName": "Nguyen Van A",
  "transactionId": "uuid-tx-disputed",
  "title": "Chuyển tiền bị trừ 2 lần",
  "category": "TransactionIssue",
  "messages": [
    {
      "senderType": "User",
      "message": "Giao dịch lúc 14:02 bị timeout nhưng tài khoản trừ tiền 2 lần",
      "createdAt": "2026-10-29T14:40:00Z"
    }
  ],
  "createdAt": "2026-10-29T14:40:00Z"
}
```

#### `POST /api/v1/admin/support/{caseId}/messages`
- **Mô tả:** Gửi tin nhắn phản hồi từ Admin tới người dùng trong hồ sơ.
- **Request Body:**
```json
{
  "message": "Bộ phận kỹ thuật đã kiểm tra bản ghi sổ cái. Chúng tôi xác nhận giao dịch bị timeout và tiến hành hoàn tiền."
}
```
- **Response `200 OK`**

#### `POST /api/v1/admin/support/{caseId}/resolve`
- **Mô tả:** Phê duyệt giải quyết khiếu nại. Kích hoạt hoàn tiền tự động vào ví người dùng nếu có chỉ định.
- **Request Body:**
```json
{
  "resolutionNote": "Hoàn trả 250.000 VNĐ do lỗi mạng giao dịch kép",
  "refundAmount": 250000.00,
  "adminPin": "{ adminPin }"
}
```
- **Response `200 OK`:**
```json
{
  "caseId": "uuid-case-1092",
  "status": "Resolved",
  "refundStatus": "Completed",
  "resolvedAt": "2026-10-29T14:55:00Z"
}
```

#### `POST /api/v1/admin/support/{caseId}/reject`
- **Mô tả:** Từ chối khiếu nại với lý do căn cứ rõ ràng.
- **Request Body:**
```json
{
  "reason": "Giao dịch đã được ngân hàng thụ hưởng xác nhận thành công và người nhận đã rút tiền.",
  "adminPin": "{ adminPin }"
}
```
- **Response `200 OK`:** `{ "caseId": "uuid-case-1092", "status": "Rejected" }`

---

### 3.5. Nhóm 5: Truyền thông Hệ thống & Giám sát Ngoại vi (Broadcast & External API Monitoring)

#### `POST /api/v1/admin/broadcasts`
- **Mô tả:** Tạo và phát hành thông báo toàn hệ sinh thái.
- **Vòng đời chuẩn:**
  1. Admin tạo bản ghi trạng thái `Draft` hoặc `Published`.
  2. Outbox Processor nhận diện bản ghi `Published`, phân phối vào hộp thư In-App của từng người dùng (`Active`).
  3. Cập nhật trạng thái `Delivered/Completed` kèm số lượng người dùng thực tế đã nhận (`deliveredCount`). Tuyệt đối không coi trạng thái `Scheduled` hoặc `persisted = 0` là đã gửi thành công.
- **Request Body:**
```json
{
  "title": "Bảo trì nâng cấp hệ thống",
  "content": "SpendWise sẽ bảo trì nâng cấp cụm máy chủ từ 01:00 đến 03:00 sáng ngày 30/10/2026.",
  "targetAudience": "AllUsers",
  "priority": "High",
  "adminPin": "{ adminPin }"
}
```
- **Response `201 Created`:**
```json
{
  "broadcastId": "uuid-broadcast",
  "status": "Published",
  "targetCount": 1210,
  "publishedAt": "2026-10-29T15:00:00Z"
}
```

#### `GET /api/v1/admin/system/gemini/monitoring`
- **Mô tả:** Bảng điều khiển telemetry Trợ lý Tài chính Gemini AI theo **Phương án B**.
- **Chỉ số đo lường:**
  - Tổng số request trong ngày.
  - Tỷ lệ thành công (`SuccessRate`).
  - Thời gian phản hồi trung bình (`AverageLatencyMs`).
  - Số lần chuyển mạch an toàn sang chính sách nội bộ (`FallbackCount` - `SpendWisePolicy`).
  - Hạn ngạch và cảnh báo lỗi quota (`QuotaExceededAlert`).
- **Response `200 OK`:**
```json
{
  "provider": "Gemini_1.5_Flash",
  "status": "Configured",
  "environment": "Sandbox/Production",
  "totalRequestsToday": 342,
  "successfulRequests": 338,
  "failedRequests": 4,
  "averageLatencyMs": 850,
  "fallbackActivations": 4,
  "quotaAlert": false,
  "lastSuccessAt": "2026-10-29T14:48:12Z",
  "lastFailureAt": "2026-10-29T11:20:05Z",
  "lastFailureReason": "rate_limit_exceeded"
}
```

#### `GET /api/v1/admin/system/sepay/monitoring`
- **Mô tả:** Bảng điều khiển giám sát cổng ngân hàng SePay BankHub.
- **Chính sách an toàn:** Chỉ giám sát hoạt động webhook IPN và môi trường Sandbox. Báo `NOT VERIFIED (EXTERNAL LIVE)` nếu chưa có traffic ngân hàng thực tế.
- **Response `200 OK`:**
```json
{
  "gateway": "SePayBankHub",
  "mode": "Sandbox",
  "ipnConfigured": true,
  "webhookEndpointStatus": "Listening",
  "totalIpnReceived": 89,
  "matchedDeposits": 89,
  "unmatchedIpn": 0,
  "lastIpnReceivedAt": "2026-10-29T14:35:10Z",
  "realMoneyTransferStatus": "NOT_VERIFIED_BY_SAFETY_POLICY"
}
```

---

## 4. RANH GIỚI VẬN HÀNH & HỆ THỐNG NGOẠI VI (EXTERNAL BOUNDARIES)

1. **Ranh giới Gemini AI:**
   - Backend hỗ trợ kết nối trực tiếp Google Gemini API qua cấu hình `Gemini:ApiKey`.
   - Khi API bên ngoài vượt hạn ngạch (429 Quota Exceeded) hoặc lỗi mạng, hệ thống tự động kích hoạt **Fallback Policy** (`SpendWisePolicy`) trả về câu trả lời phân tích dựa trên tập quy tắc nội bộ, bảo đảm ứng dụng không bao giờ bị gián đoạn hoạt động.
   - Hạn ngạch vô hạn và lưu lượng sống (Live Traffic) thuộc phạm vi quản trị tài khoản Google Cloud của doanh nghiệp.
2. **Ranh giới SePay BankHub:**
   - Hệ thống V1.0 chạy hoàn toàn trên môi trường kiểm thử Sandbox.
   - Cơ chế xác thực chữ ký Webhook HMAC bảo đảm tiền gửi chỉ được ghi có khi nhận đúng gói tin mã hóa từ cổng thanh toán.
   - Tuyệt đối không thử nghiệm chuyển tiền thật trên tài khoản sản xuất khi chưa hoàn tất kiểm toán an ninh ngân hàng độc lập.
