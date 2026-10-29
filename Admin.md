# SPENDWISE V1.0 — ĐẶC TẢ KỸ THUẬT VÀ TÀI LIỆU API PHÂN HỆ ADMIN

> **Phiên bản:** V1.0 (Production Candidate)  
> **Đối tượng sử dụng:** Frontend Admin Web Developers (Codex / Engineering Team).  
> **Trạng thái Backend:** Locked 100% (Đã kiểm thử hồi quy tự động 80 requests / 138 assertions Newman PASS).  
> **Ranh giới tài liệu:** Thuần đặc tả nghiệp vụ quản trị, 21 chức năng (5 nhóm), API contracts, kiểm soát truy cập và ranh giới ngoại vi. Không chứa lý thuyết chung hay tiếp thị.

---

## 1. NGUYÊN TẮC BẢO MẬT & KIỂM SOÁT QUYỀN HẠN (SECURITY POLICY)

### 1.1. Chính sách Quản trị viên Duy nhất (Single Admin Policy)
1. **Duy nhất một tài khoản Quản trị viên hệ thống:**
   - **Tài khoản định danh:** `lethien241205@gmail.com`
   - **Vai trò bắt buộc:** `SystemRole.Admin = 2`
   - Khóa chặt ở cấp độ logic và dữ liệu: Tuyệt đối cấm tạo tài khoản Admin thứ hai (`403 Forbidden` - `operator_management_disabled`).
2. **Loại bỏ triệt để các vai trò kế thừa (Legacy Roles Elimination):**
   - Các vai trò cũ `SystemAdmin = 0` và `SuperAdmin = 3` bị từ chối quyền quản trị ngay tại tầng lọc bảo mật (`AdminSecurityGuard`), bất kể token JWT có chữ ký hợp lệ hay không (`403 Forbidden` - `legacy_role_rejected`).
3. **Bảo vệ bằng mã PIN Quản trị (`AdminPin`):**
   - Mọi thao tác quản trị nhạy cảm (đổi trạng thái người dùng, cưỡng chế thu hồi phiên, phê duyệt hoàn tiền khiếu nại, phát hành thông báo broadcast, đóng băng ví) **bắt buộc phải truyền kèm `adminPin`** hợp lệ trong request body.
   - Nhập sai mã PIN quản trị quá số lần quy định sẽ bị khóa tạm thời.

### 1.2. Kiểm soát Truy cập Dữ liệu Nhạy cảm theo Hồ sơ Hỗ trợ (Case-Scoped Access)
1. **Nguyên tắc không đọc dữ liệu tùy tiện (Zero Unbounded Access):**
   - Admin không có quyền truy cập tùy tiện vào số dư tài khoản, danh sách phiên, các nhóm tham gia hoặc lịch sử chi tiêu cá nhân của người dùng nếu không gắn với một yêu cầu nghiệp vụ cụ thể.
   - Các API truy cập dữ liệu nhạy cảm của người dùng **bắt buộc phải gắn liền với một Hồ sơ hỗ trợ / khiếu nại đang thụ lý (`caseId`)** và cung cấp lý do điều tra (`reason`):
     - `POST /api/v1/admin/support/{caseId}/users/{userId}/profile`
     - `POST /api/v1/admin/support/{caseId}/users/{userId}/sessions`
     - `POST /api/v1/admin/support/{caseId}/users/{userId}/restrictions`
     - `POST /api/v1/admin/support/{caseId}/users/{userId}/groups`
     - `POST /api/v1/admin/support/{caseId}/users/{userId}/friends`
     - `POST /api/v1/admin/support/{caseId}/users/{userId}/history`
2. **Nhật ký Kiểm toán Bất biến (Immutable Audit Logs):**
   - Mọi thao tác quản trị được tự động ghi nhận vào bảng `AdminAuditLogs`: thời gian (`UtcTimestamp`), IP, User-Agent, hành động (`Action`), đối tượng tác động (`TargetEntity`), dữ liệu trước và sau thao tác (`BeforeState`, `AfterState`).
   - Admin không thể chỉnh sửa hay xóa bất kỳ bản ghi nhật ký kiểm toán nào.

---

## 2. MA TRẬN 21 CHỨC NĂNG QUẢN TRỊ ADMIN THEO 5 NHÓM LỚN

| Mã CN | Nhóm | Tên Chức Năng | API Endpoint Chính | Phương Thức |
|---|---|---|---|:---:|
| **ADM-01** | Nhóm 1: Tổng quan | 1. Dashboard Vận hành | `/api/admin/dashboard` & `/api/v1/admin/overview` | `GET` |
| **ADM-02** | Nhóm 1: Tổng quan | 2. Tổng hợp Báo cáo & Hoạt động | `/api/admin/reports` & `/api/v1/admin/overview/activity` | `GET` |
| **ADM-03** | Nhóm 1: Tổng quan | 3. Quản lý Tác vụ Vận hành | `/api/v1/admin/overview/tasks` | `GET` |
| **ADM-04** | Nhóm 1: Tổng quan | 4. Cảnh báo Hệ thống Vận hành | `/api/v1/admin/overview/alerts` | `GET` |
| **ADM-05** | Nhóm 2: Người dùng | 5. Tra cứu & Phân trang Người dùng | `/api/admin/users` & `/api/v1/admin/users/search` | `GET` / `POST` |
| **ADM-06** | Nhóm 2: Người dùng | 6. Xem Hồ sơ (Case-Scoped) | `/api/v1/admin/support/{caseId}/users/{userId}/profile` | `POST` |
| **ADM-07** | Nhóm 2: Người dùng | 7. Xem Phiên & Quan hệ (Case-Scoped) | `/api/v1/admin/support/{caseId}/users/{userId}/sessions` | `POST` |
| **ADM-08** | Nhóm 2: Người dùng | 8. Cưỡng chế Thu hồi Phiên | `/api/admin/users/{id}/revoke-sessions` | `POST` |
| **ADM-09** | Nhóm 2: Người dùng | 9. Điều chỉnh Trạng thái Người dùng | `/api/admin/users/{id}/status` | `PUT` |
| **ADM-10** | Nhóm 2: Người dùng | 10. Đặt lại Mật khẩu / PIN An toàn | `/api/admin/users/{id}/reset-password` / `reset-pin` | `POST` |
| **ADM-11** | Nhóm 3: Sổ cái & GD | 11. Giám sát Bảng cân đối Sổ cái | `/api/admin/ledger/search` & `/api/admin/wallets/{id}/freeze` | `POST` / `PUT` |
| **ADM-12** | Nhóm 3: Sổ cái & GD | 12. Tra cứu Giao dịch Toàn hệ thống | `/api/admin/transactions/search` & `/api/admin/bank-transactions` | `POST` / `GET` |
| **ADM-13** | Nhóm 3: Sổ cái & GD | 13. Nhật ký Kiểm toán & Cấu hình | `/api/admin/audit-logs` & `/api/admin/configuration` | `GET` / `PUT` |
| **ADM-14** | Nhóm 4: Hỗ trợ | 14. Tiếp nhận & Tra cứu Hồ sơ | `/api/admin/cases/support` & `/api/v1/admin/support/tickets/search` | `GET` / `POST` |
| **ADM-15** | Nhóm 4: Hỗ trợ | 15. Phân công & Xem Chi tiết Hồ sơ | `/api/v1/admin/support/{caseId}/assign` & `GET .../support/{id}` | `POST` / `GET` |
| **ADM-16** | Nhóm 4: Hỗ trợ | 16. Phản hồi Khách hàng & Mẫu trả lời | `/api/admin/cases/support/{id}/messages` & `/templates` | `POST` |
| **ADM-17** | Nhóm 4: Hỗ trợ | 17. Yêu cầu Thông tin & Hồ sơ Quỹ | `/api/v1/admin/support/{caseId}/request-info` & `/dossier` | `POST` |
| **ADM-18** | Nhóm 4: Hỗ trợ | 18. Phê duyệt Hoàn tiền / Đóng hồ sơ | `/api/admin/cases/support/{id}/status` & `/correction-grant` | `POST` |
| **ADM-19** | Nhóm 5: Ngoại vi | 19. Phát hành Thông báo Broadcast | `/api/v1/system/broadcasts` & `/publish` & `/status` | `POST` |
| **ADM-20** | Nhóm 5: Ngoại vi | 20. Giám sát Vận hành Gemini AI | `/api/admin/health` & `/api/admin/ai/chat` | `GET` / `POST` |
| **ADM-21** | Nhóm 5: Ngoại vi | 21. Giám sát Ngân hàng SePay | `/api/admin/integrations/sepay/status` & `/reconciliation` | `GET` |

---

## 3. CHI TIẾT API CONTRACT 21 CHỨC NĂNG QUẢN TRỊ ADMIN

Header bắt buộc cho mọi Admin API:
```http
Content-Type: application/json
Authorization: Bearer <admin_access_token>
```

### 3.1. Nhóm 1: Tổng quan & Vận hành Hệ thống (ADM-01 $\rightarrow$ ADM-04)

#### ADM-01: Thống kê Dashboard Vận hành
- **Endpoint:** `GET /api/admin/dashboard` và `GET /api/v1/admin/overview`
- **Mô tả:** Trả về số liệu tổng quan về tài khoản, số vụ hỗ trợ đang chờ, tỷ lệ lỗi và trạng thái hệ thống.
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

#### ADM-02: Tổng hợp Báo cáo & Hoạt động
- **Endpoint:** `GET /api/admin/reports` và `GET /api/v1/admin/overview/activity`
- **Mô tả:** Báo cáo tổng lượng giao dịch, biến động tiền tệ và hoạt động người dùng trong kỳ.
- **Response `200 OK`:** Mảng danh sách các bản ghi hoạt động phân trang.

#### ADM-03: Quản lý Tác vụ Vận hành
- **Endpoint:** `GET /api/v1/admin/overview/tasks`
- **Mô tả:** Theo dõi trạng thái hàng đợi nền, Outbox Worker và các tiến trình đồng bộ tự động.
- **Response `200 OK`:** Danh sách tác vụ kèm trạng thái (`Running`, `Queued`, `Completed`, `Failed`).

#### ADM-04: Cảnh báo Hệ thống Vận hành
- **Endpoint:** `GET /api/v1/admin/overview/alerts`
- **Mô tả:** Danh sách các cảnh báo an ninh vận hành (đăng nhập thất bại hàng loạt, lỗi webhook IPN, cảnh báo giới hạn hạn ngạch AI).
- **Response `200 OK`:** Mảng các bản ghi cảnh báo kèm mức độ nghiêm trọng (`Info`, `Warning`, `Critical`).

---

### 3.2. Nhóm 2: Quản lý Người dùng & Trạng thái (ADM-05 $\rightarrow$ ADM-10)

#### ADM-05: Tra cứu & Phân trang Người dùng
- **Endpoint:** `GET /api/admin/users?search={keyword}` hoặc `POST /api/v1/admin/users/search`
- **Mô tả:** Tìm kiếm người dùng theo Email, SĐT, Họ tên hoặc SpendId.
- **Response `200 OK`:**
```json
[
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "email": "user@example.com",
    "fullName": "Nguyen Van A",
    "phone": "0912345678",
    "spendId": "SPEND_123456",
    "status": "Active",
    "createdAt": "2026-10-20T08:00:00Z"
  }
]
```

#### ADM-06: Xem Hồ sơ Người dùng (Case-Scoped Access - A2Profile)
- **Endpoint:** `POST /api/v1/admin/support/{caseId}/users/{userId}/profile`
- **Mô tả:** Xem chi tiết thông tin và tài sản ví người dùng gắn liền với vụ việc hỗ trợ đang thụ lý. Controller `AdminA2Controller` trả về cấu trúc `A2Profile` (gồm đối tượng `User`, `CaseId` và mảng ví đang hoạt động `Wallets`).
- **Quyền:** Admin Role 2 (`OperationsRead`).
- **Request Body (`A2ReasonRequest`):**
```json
{
  "reason": "Điều tra khiếu nại chuyển tiền sai số dư hồ sơ CASE-891"
}
```
- **Response `200 OK` (`A2Profile`):**
```json
{
  "user": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "spendId": "SPEND_9921",
    "displayName": "Nguyen Van A",
    "maskedEmail": "u***@example.com",
    "status": "Active",
    "createdAt": "2026-10-20T08:00:00Z"
  },
  "caseId": "c43a8401-2742-4260-bf0c-16652e944e9a",
  "wallets": [
    {
      "id": "0acc5688-857e-48b2-b60f-03718ed265cf",
      "type": "Main",
      "status": "Active",
      "currency": "VND",
      "availableBalance": 32000.00
    },
    {
      "id": "e7101f2a-e334-44ab-9d34-a6f322aa5467",
      "type": "Savings",
      "status": "Active",
      "currency": "VND",
      "availableBalance": 25000.00
    }
  ]
}
```

> [!NOTE]
> Phân biệt rõ endpoint này với `POST /api/v1/admin/support/{caseId}/user-profile` trả `ScopedUserProfile`. Endpoint `AdminA2Controller.Profile` trả về đúng chuẩn `A2Profile` với đối tượng người dùng đã mask email và danh sách ví kèm số dư khả dụng thực tế.

#### ADM-07: Xem Danh sách Phiên & Quan hệ (Case-Scoped Access)
- **Endpoint:** `POST /api/v1/admin/support/{caseId}/users/{userId}/sessions`
- **Mô tả:** Xem các phiên thiết bị đang đăng nhập của người dùng trong phạm vi vụ việc.
- **Response `200 OK`:** Danh sách phiên thiết bị, IP, thời điểm hoạt động cuối cùng.

#### ADM-08: Cưỡng chế Thu hồi Toàn bộ Phiên (Revoke Sessions)
- **Endpoint:** `POST /api/admin/users/{id}/revoke-sessions`
- **Mô tả:** Đăng xuất tức thì tất cả thiết bị của người dùng khi phát hiện rủi ro an ninh.
- **Request Body:**
```json
{
  "reason": "Người dùng báo mất điện thoại yêu cầu thu hồi phiên khẩn cấp"
}
```
- **Response:** `204 NoContent`.

#### ADM-09: Điều chỉnh Trạng thái Tài khoản Người dùng
- **Endpoint:** `PUT /api/admin/users/{id}/status`
- **Mô tả:** Chuyển đổi trạng thái giữa `Active`, `Restricted`, `Locked`.
  - Chuyển `Locked`: Tự động thu hồi tức thì toàn bộ Refresh Token và User Sessions (`RevokedAt = UtcNow`). Chặn đăng nhập.
  - Chuyển `Restricted`: Chặn toàn bộ lệnh ghi nợ tiền ra, vẫn cho phép đăng nhập và nhận tiền.
- **Request Body:**
```json
{
  "status": "Locked",
  "reason": "Tài khoản có dấu hiệu gian lận theo khiếu nại",
  "adminPin": "123456"
}
```
- **Response `200 OK`:** `AdminUserDto` với trạng thái mới.

#### ADM-10: Đặt lại Mật khẩu / PIN An toàn
- **Endpoint:** `POST /api/admin/users/{id}/reset-password` hoặc `POST /api/admin/users/{id}/reset-pin`
- **Mô tả:** Kích hoạt gửi email khôi phục mật khẩu hoặc reset PIN bảo mật an toàn tới người dùng.
- **Request Body:**
```json
{
  "reason": "Hỗ trợ khách hàng quên mật khẩu và mất số điện thoại",
  "adminPin": "123456"
}
```
- **Response `200 OK`:** Thông báo kích hoạt gửi hướng dẫn qua email thành công.

---

### 3.3. Nhóm 3: Giám sát Sổ cái & Giao dịch Toàn hệ thống (ADM-11 $\rightarrow$ ADM-13)

#### ADM-11: Giám sát Bảng cân đối Sổ cái & Đóng băng Ví
- **Endpoint:** `POST /api/admin/ledger/search` & `PUT /api/admin/wallets/{id}/freeze`
- **Mô tả:** Kiểm tra tính bất biến $\sum \text{Debit} = \sum \text{Credit}$ của Sổ cái kép và thực hiện đóng băng ví tạm thời nếu phát hiện gian lận.
- **Freeze Wallet Request Body:**
```json
{
  "isFrozen": true,
  "reason": "Đóng băng tài sản theo lệnh điều tra",
  "adminPin": "123456"
}
```
- **Response:** `204 NoContent`.

#### ADM-12: Tra cứu Giao dịch Toàn hệ thống
- **Endpoint:** `POST /api/admin/transactions/search` & `GET /api/admin/bank-transactions`
- **Mô tả:** Tra cứu mọi biến động tài chính, đối soát giao dịch chuyển khoản ngân hàng.
- **Request Body (`transactions/search`):**
```json
{
  "reason": "Đối soát giao dịch chuyển tiền khiếu nại"
}
```
- **Response `200 OK`:** Danh sách giao dịch có phân trang.

#### ADM-13: Nhật ký Kiểm toán & Cấu hình Hệ thống
- **Endpoint:** `GET /api/admin/audit-logs`, `GET /api/admin/configuration`, `PUT /api/admin/configuration/{key}`
- **Mô tả:** Xem lịch sử mọi thao tác quản trị (không thể xóa/sửa) và quản lý cờ tính năng (`Feature Flags`).
- **Response `200 OK`:** Mảng các bản ghi `AuditLogDto`.

---

### 3.4. Nhóm 4: Tiếp nhận & Xử lý Hỗ trợ/Khiếu nại (ADM-14 $\rightarrow$ ADM-18)

#### ADM-14: Tiếp nhận & Tra cứu Hồ sơ Hỗ trợ
- **Endpoint:** `GET /api/admin/cases/support` hoặc `POST /api/v1/admin/support/tickets/search`
- **Mô tả:** Danh sách các yêu cầu khiếu nại/hỗ trợ của người dùng, lọc theo trạng thái (`Pending`, `InReview`, `Resolved`, `Rejected`).
- **Response `200 OK`:** Mảng `SupportCaseDetailDto`.

#### ADM-15: Phân công & Xem Chi tiết Hồ sơ
- **Endpoint:** `POST /api/v1/admin/support/{caseId}/assign` & `GET /api/admin/cases/support/{id}`
- **Mô tả:** Tiếp nhận phân công hồ sơ và xem chi tiết nội dung khiếu nại, giao dịch liên quan.
- **Response `200 OK`:** Chi tiết hồ sơ kèm lịch sử tin nhắn.

#### ADM-16: Phản hồi Khách hàng & Áp dụng Mẫu trả lời
- **Endpoint:** `POST /api/admin/cases/support/{id}/messages` & `POST /api/v1/admin/support/{caseId}/reply-template`
- **Mô tả:** Gửi phản hồi chính thức từ Quản trị viên tới khách hàng hoặc áp dụng mẫu trả lời chuẩn hóa.
- **Request Body (`messages`):**
```json
{
  "message": "Bộ phận kỹ thuật đã đối soát dữ liệu và tiến hành xử lý hoàn tiền."
}
```
- **Response `200 OK`:** `SupportCaseDetailDto` đã cập nhật.

#### ADM-17: Yêu cầu Bổ sung Thông tin & Hồ sơ Quỹ
- **Endpoint:** `POST /api/v1/admin/support/{caseId}/request-info` & `POST /api/v1/admin/support/{caseId}/dossier`
- **Mô tả:** Chuyển trạng thái hồ sơ sang chờ khách hàng bổ sung chứng từ, hoặc trích xuất hồ sơ tài chính quỹ nhóm liên quan.
- **Response `200 OK`:** Kết quả chuyển trạng thái thành công.

#### ADM-18: Phê duyệt Hoàn tiền / Đóng Hồ sơ Hỗ trợ
- **Endpoint:** `POST /api/admin/cases/support/{id}/status` & `POST /api/admin/cases/support/{id}/correction-grant`
- **Mô tả:** Cập nhật kết quả giải quyết (`Resolved` hoặc `Rejected`). Nếu chấp thuận, cấp lệnh hoàn tiền tự động (`correction-grant`) vào ví người dùng.
- **Request Body (`correction-grant`):**
```json
{
  "amount": 250000.00,
  "reason": "Hoàn tiền giao dịch bị trừ đúp do timeout",
  "adminPin": "123456"
}
```
- **Response `200 OK`:** `SupportCaseDetailDto` với trạng thái `Resolved`.

---

### 3.5. Nhóm 5: Truyền thông Hệ thống & Giám sát Ngoại vi (ADM-19 $\rightarrow$ ADM-21)

#### ADM-19: Phát hành Thông báo Toàn hệ thống (Broadcast)
- **Endpoint:**
  - `POST /api/v1/system/broadcasts` (Tạo bản thảo - `Draft`)
  - `POST /api/v1/system/broadcasts/{id}/publish` (Phát hành - `Publish`)
  - `POST /api/v1/system/broadcasts/{id}/status` (Kiểm tra trạng thái phát tán)
  - `POST /api/v1/system/broadcasts/{id}/cancel` (Hủy phát hành)
- **Vòng đời chuẩn:**
  1. Admin tạo `Draft` $\rightarrow$ bấm `Publish`.
  2. Outbox Worker nhận diện và phân phối tới từng hộp thư In-App của người dùng `Active`.
  3. Trạng thái chuyển thành `Delivered/Completed`. Tuyệt đối không coi trạng thái `Scheduled` hoặc `persisted = 0` là đã gửi thành công.
- **Publish Request Body:**
```json
{
  "reason": "Phát hành thông báo bảo trì định kỳ lúc 01:00",
  "adminPin": "123456"
}
```
- **Response:** `204 NoContent`.

#### ADM-20: Giám sát Vận hành Trợ lý Gemini AI
- **Endpoint:** `GET /api/admin/health` & `POST /api/admin/ai/chat`
- **Mô tả:** Giám sát đo lường telemetry Trợ lý Gemini AI theo **Phương án B**.
- **Chỉ số đo lường:**
  - Tổng số request, tỷ lệ thành công (`SuccessRate`), thời gian phản hồi trung bình (`AverageLatencyMs`).
  - Số lần chuyển mạch an toàn sang quy tắc nội bộ (`FallbackCount` - `SpendWisePolicy`).
  - Cảnh báo giới hạn hạn ngạch (`QuotaExceededAlert`).
- **Response `200 OK` (`GET /api/admin/health`):** Trả về telemetry của AI Provider, Database, và Outbox Worker.

#### ADM-21: Giám sát Cổng Ngân hàng SePay BankHub
- **Endpoint:** `GET /api/admin/integrations/sepay/status` & `GET /api/admin/bank-integration/reconciliation`
- **Mô tả:** Giám sát trạng thái kết nối Webhook IPN và kết quả đối soát nạp tiền Sandbox.
- **Chính sách an toàn:** Báo trạng thái `Listening / Sandbox`. Không tạo giao dịch tiền thật trên tài khoản sản xuất.
- **Response `200 OK` (`integrations/sepay/status`):**
```json
{
  "configured": true,
  "enabled": true,
  "ipnConfigured": true,
  "webhookEndpoint": "/api/banking/sepay/webhook",
  "mode": "Sandbox",
  "lastIpnReceivedAt": "2026-10-29T14:35:10Z"
}
```

---

## 4. RANH GIỚI VẬN HÀNH & HỆ THỐNG NGOẠI VI (EXTERNAL BOUNDARIES)

1. **Ranh giới Gemini AI:**
   - Kết nối Google Gemini API qua cấu hình bảo mật `Gemini:ApiKey`.
   - Khi API gặp sự cố hoặc vượt hạn ngạch 429, hệ thống kích hoạt **Fallback Policy** (`SpendWisePolicy`) trả lời theo quy tắc nghiệp vụ nội bộ, bảo đảm hệ thống vận hành liên tục không gián đoạn.
   - Hạn ngạch vô hạn và lưu lượng người dùng thực tế trực tiếp thuộc quyền kiểm soát tài khoản Google Cloud của doanh nghiệp.
2. **Ranh giới SePay BankHub:**
   - Hoạt động kiểm thử và nghiệm thu V1.0 chạy hoàn toàn trên môi trường Sandbox thông qua xác thực chữ ký Webhook IPN.
   - Tuyệt đối không thực hiện giao dịch rút/chuyển tiền thật khi chưa có phê duyệt hạ tầng ngân hàng sống.
3. **Trách nhiệm Frontend Admin Web:**
   - Giao diện Admin quản trị được bảo vệ bởi phiên làm việc duy nhất của `lethien241205@gmail.com`.
   - Tự động hiển thị hộp thoại xác thực `AdminPin` cho mọi thao tác nhạy cảm trước khi gửi request tới Backend.
