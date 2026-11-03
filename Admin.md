# SPENDWISE V1.0 — ĐẶC TẢ KỸ THUẬT VÀ TÀI LIỆU API PHÂN HỆ ADMIN

> **Phiên bản:** V1.0 (Production Candidate)
> **Đối tượng sử dụng:** Frontend Admin Web Developers (Codex / Engineering Team).
> **Trạng thái Backend:** Core Admin V1.0 Backend LOCKED 100% (Kiểm thử tự động hồi quy 80 requests / 138 assertions Newman PASS, 22 requests / 42 assertions Support E2E PASS, 82/82 xUnit unit/integration PASS). Ngoại vi SePay Sandbox & Gemini Plan B Telemetry đã kiểm chứng; Provider tiền thật và live enterprise quota là ranh giới ngoại vi.
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

## 2. MA TRẬN ĐỐI CHIẾU 21 CHỨC NĂNG QUẢN TRỊ ADMIN (ADM-01 ĐẾN ADM-21)

> Chuẩn hóa đối chiếu: `MÃ CN → NHÓM & YÊU CẦU CỐT LÕI → CONTROLLER & API ROUTE → DTO & POLICY → E2E / UNIT TEST BẰNG CHỨNG → TRẠNG THÁI TRIỂN KHAI & NGHIỆM THU`.
> Phân biệt rõ: **Số dư đã quyết toán (Settled Balance)** với **Yêu cầu hỗ trợ đang chờ (Pending Cases)**; **Mô phỏng SePay Sandbox & Gemini Plan B Telemetry** với **Tích hợp tài khoản đối tác tiền thật**.

| Mã CN | Nhóm & Tên Chức Năng | Yêu Cầu Nghiệp Vụ Cốt Lõi | Controller & Route API | DTO Request / Response & Policy | Bằng Chứng Kiểm Thử (Newman / xUnit) | Trạng Thái Nghiệm Thu |
|---|---|---|---|---|---|:---:|
| **ADM-01** | Nhóm 1: Tổng quan<br>1. Dashboard Vận hành | Thống kê số lượng User theo trạng thái (Active, Restricted, Locked), số vụ hỗ trợ chờ xử lý, tổng số dư hệ thống và sức khỏe vận hành. | `AdminA1Controller.cs`<br>`GET /api/admin/dashboard`<br>`V1AdministrationController.cs`<br>`GET /api/v1/admin/overview` | `DashboardSummaryDto`<br>`AdminOverviewDto`<br>Policy: `OperationsRead` | `reports/newman/ADMIN-V1-FINAL-NEWMAN-REPORT-LIVE.json`<br>(Folder 01: ADM-01.1 & ADM-01.2 PASS) | **LOCKED & VERIFIED** |
| **ADM-02** | Nhóm 1: Tổng quan<br>2. Tổng hợp Báo cáo & Hoạt động | Thống kê tổng lượng giao dịch, biến động tiền tệ và nhật ký hoạt động người dùng trong kỳ quan sát. | `AdminController.cs`<br>`GET /api/admin/reports`<br>`V1AdministrationController.cs`<br>`GET /api/v1/admin/overview/activity` | `AdminReportDto`<br>`AdminActivityLogDto`<br>Policy: `OperationsRead` | `reports/newman/ADMIN-V1-FINAL-NEWMAN-REPORT-LIVE.json`<br>(Folder 01: ADM-02 PASS) | **LOCKED & VERIFIED** |
| **ADM-03** | Nhóm 1: Tổng quan<br>3. Quản lý Tác vụ Vận hành | Theo dõi trạng thái hàng đợi nền, Outbox Worker và các tiến trình đồng bộ tự động (`Running, Queued, Completed, Failed`). | `V1AdministrationController.cs`<br>`GET /api/v1/admin/overview/tasks` | `AdminTaskDto[]`<br>Policy: `OperationsRead` | `reports/newman/ADMIN-V1-FINAL-NEWMAN-REPORT-LIVE.json`<br>(Folder 01: ADM-01.3 PASS) | **LOCKED & VERIFIED** |
| **ADM-04** | Nhóm 1: Tổng quan<br>4. Cảnh báo Hệ thống Vận hành | Luồng sự kiện cảnh báo an ninh vận hành (đăng nhập thất bại hàng loạt, lỗi webhook IPN, cảnh báo quota AI) kèm mức độ nghiêm trọng. | `V1AdministrationController.cs`<br>`GET /api/v1/admin/overview/alerts` | `AdminAlertDto[]`<br>(observedAtUtc, severity)<br>Policy: `OperationsRead` | `reports/newman/ADMIN-V1-FINAL-NEWMAN-REPORT-LIVE.json`<br>(Folder 01: ADM-01.4 PASS) | **LOCKED & VERIFIED** |
| **ADM-05** | Nhóm 2: Người dùng<br>5. Tra cứu & Phân trang Người dùng | Tìm kiếm và phân trang người dùng theo Email, SĐT, Tên, SpendId; trả về trạng thái tài khoản và ngày tham gia. | `AdminController.cs`<br>`GET /api/admin/users?search=...`<br>`AdminA2Controller.cs`<br>`POST /api/v1/admin/users/search` | `AdminUserDto[]`<br>`PagedResult<AdminUserDto>`<br>Auth: JWT Admin Role 2 | `reports/newman/ADMIN-V1-FINAL-NEWMAN-REPORT-LIVE.json`<br>(Folder 02: ADM-02.1 PASS) | **LOCKED & VERIFIED** |
| **ADM-06** | Nhóm 2: Người dùng<br>6. Xem Hồ sơ (Case-Scoped) | Xem chi tiết thông tin và tài sản ví người dùng gắn liền với vụ việc hỗ trợ đang thụ lý (`caseId`); che email (masked) và ghi audit log. | `AdminA2Controller.cs`<br>`POST /api/v1/admin/support/{caseId}/users/{userId}/profile` | Request: `A2ReasonRequest`<br>Response: `A2Profile` (user, caseId, wallets)<br>Policy: `OperationsRead` | `reports/newman/ADMIN-V1-FINAL-NEWMAN-REPORT-LIVE.json`<br>(Folder 02: ADM-02.2 PASS) | **LOCKED & VERIFIED** |
| **ADM-07** | Nhóm 2: Người dùng<br>7. Xem Phiên & Quan hệ (Case-Scoped) | Xem các phiên thiết bị đang đăng nhập, IP, User-Agent của người dùng trong phạm vi vụ việc hỗ trợ đang điều tra. | `AdminA2Controller.cs`<br>`POST /api/v1/admin/support/{caseId}/users/{userId}/sessions` | Request: `A2ReasonRequest`<br>Response: `A2SessionDto[]`<br>Policy: `OperationsRead` | `reports/newman/ADMIN-V1-FINAL-NEWMAN-REPORT-LIVE.json`<br>(Folder 02: ADM-02.3 PASS) | **LOCKED & VERIFIED** |
| **ADM-08** | Nhóm 2: Người dùng<br>8. Cưỡng chế Thu hồi Phiên | Đăng xuất tức thì tất cả thiết bị của người dùng khi có dấu hiệu rủi ro an ninh; thu hồi toàn bộ refresh token (`RevokedAt = UtcNow`). | `AdminController.cs`<br>`POST /api/admin/users/{id}/revoke-sessions`<br>`AdminA2Controller.cs`<br>`POST /api/v1/admin/support/{caseId}/users/{userId}/revoke-sessions` | Request: `RevokeSessionsRequest` (`reason`)<br>Response: `204 NoContent`<br>Auth: JWT Admin Role 2 | `reports/newman/ADMIN-V1-FINAL-NEWMAN-REPORT-LIVE.json`<br>(Folder 02: ADM-02.3b PASS, SEC-19 & SEC-20 PASS) | **LOCKED & VERIFIED** |
| **ADM-09** | Nhóm 2: Người dùng<br>9. Điều chỉnh Trạng thái Người dùng | Chuyển đổi trạng thái tài khoản: `Active`, `Restricted` (chặn ghi nợ tiền ra, cho phép đọc/nhận), `Locked` (chặn đăng nhập/phiên hoàn toàn). Bắt buộc `adminPin`. | `AdminController.cs`<br>`PUT /api/admin/users/{id}/status`<br>`AdminA2Controller.cs`<br>`PUT /api/v1/admin/support/{caseId}/users/{userId}/status` | Request: `UpdateUserStatusRequest` (`status, reason, adminPin`)<br>Response: `AdminUserDto` | `reports/newman/ADMIN-V1-FINAL-NEWMAN-REPORT-LIVE.json`<br>(Folder 02: ADM-02.4 PASS, SEC-08, SEC-12, SEC-13 PASS) | **LOCKED & VERIFIED** |
| **ADM-10** | Nhóm 2: Người dùng<br>10. Đặt lại Mật khẩu / PIN An toàn | Kích hoạt gửi hướng dẫn khôi phục mật khẩu hoặc reset PIN bảo mật an toàn tới email chính chủ của người dùng. Bắt buộc `adminPin`. | `AdminController.cs`<br>`POST /api/admin/users/{id}/reset-password`<br>`POST /api/admin/users/{id}/reset-pin` | Request: `AdminResetCredentialRequest` (`reason, adminPin`)<br>Response: `200 OK` | `reports/newman/ADMIN-V1-FINAL-NEWMAN-REPORT-LIVE.json`<br>(Folder 02: ADM-02.5 PASS) | **LOCKED & VERIFIED** |
| **ADM-11** | Nhóm 3: Sổ cái & GD<br>11. Giám sát Bảng cân đối Sổ cái | Kiểm tra tính bất biến $\sum \text{Debit} = \sum \text{Credit}$ của Sổ cái kép; đóng băng / mở băng ví tạm thời theo lệnh điều tra tài chính với `adminPin`. | `AdminController.cs`<br>`POST /api/admin/ledger/search`<br>`PUT /api/admin/wallets/{id}/freeze` | Request: `SearchAdminLedgerRequest`, `FreezeWalletRequest` (`isFrozen, reason, adminPin`)<br>Response: `204 NoContent` | `reports/newman/ADMIN-V1-FINAL-NEWMAN-REPORT-LIVE.json`<br>(Folder 03: ADM-03.1 PASS) | **LOCKED & VERIFIED** |
| **ADM-12** | Nhóm 3: Sổ cái & GD<br>12. Tra cứu Giao dịch Toàn hệ thống | Tra cứu toàn bộ biến động tài chính, đối soát giao dịch sao kê ngân hàng và liên kết đối tượng nghiệp vụ. | `AdminController.cs`<br>`POST /api/admin/transactions/search`<br>`GET /api/admin/bank-transactions` | Request: `SearchAdminTransactionsRequest` (`reason`)<br>Response: `PagedResult<AdminTransactionDto>` | `reports/newman/ADMIN-V1-FINAL-NEWMAN-REPORT-LIVE.json`<br>(Folder 03: ADM-03.2 PASS) | **LOCKED & VERIFIED** |
| **ADM-13** | Nhóm 3: Sổ cái & GD<br>13. Nhật ký Kiểm toán & Cấu hình | Xem nhật ký kiểm toán bất biến `AdminAuditLogs` (không thể sửa/xóa) và quản lý cờ tính năng hệ thống (`Feature Flags`). | `AdminController.cs`<br>`GET /api/admin/audit-logs`<br>`GET /api/admin/configuration`<br>`GET /api/admin/feature-flags` | `AuditLogDto[]`, `AdminConfigurationDto`<br>Auth: JWT Admin Role 2 | `reports/newman/ADMIN-V1-FINAL-NEWMAN-REPORT-LIVE.json`<br>(Folder 03: ADM-03.3 PASS, HIS-01 PASS) | **LOCKED & VERIFIED** |
| **ADM-14** | Nhóm 4: Hỗ trợ<br>14. Tiếp nhận & Tra cứu Hồ sơ | Danh sách hồ sơ khiếu nại/hỗ trợ của người dùng, lọc theo trạng thái (`Pending`, `InReview`, `Resolved`, `Rejected`) và danh mục. | `AdminController.cs`<br>`GET /api/admin/cases/support`<br>`AdminA5Controller.cs`<br>`POST /api/v1/admin/support/tickets/search` | `SupportCaseDetailDto[]`<br>Auth: JWT Admin Role 2 | `reports/newman/ADMIN-V1-FINAL-NEWMAN-REPORT-LIVE.json`<br>`reports/newman/support_complaint_e2e_regression_report.json` | **LOCKED & VERIFIED** |
| **ADM-15** | Nhóm 4: Hỗ trợ<br>15. Phân công & Xem Chi tiết Hồ sơ | Tiếp nhận phân công hồ sơ cho Admin thụ lý và xem toàn diện nội dung khiếu nại, giao dịch liên quan, lịch sử trao đổi. | `AdminA5Controller.cs`<br>`POST /api/v1/admin/support/{caseId}/assign`<br>`AdminController.cs`<br>`GET /api/admin/cases/support/{id}` | Assign: `A5AssignTicketRequest` (`reason, operatorId`) $\rightarrow$ `{success, caseId}`<br>Detail: `SupportCaseDetailDto`<br>Policy: `OperationsRead` | `reports/newman/ADMIN-V1-FINAL-NEWMAN-REPORT-LIVE.json`<br>`reports/newman/support_complaint_e2e_regression_report.json` | **LOCKED & VERIFIED** |
| **ADM-16** | Nhóm 4: Hỗ trợ<br>16. Phản hồi Khách hàng & Mẫu trả lời | Gửi phản hồi chính thức từ Admin tới khách hàng hoặc áp dụng mẫu trả lời chuẩn hóa (`templateKey`) trong luồng hỗ trợ. | `AdminController.cs`<br>`POST /api/admin/cases/support/{id}/messages`<br>`AdminA5Controller.cs`<br>`POST /api/v1/admin/support/{caseId}/reply-template` | Message: `CaseMessageRequest` (`message`) $\rightarrow$ `SupportCaseDetailDto`<br>Template: `A5ApplyTemplateRequest` (`reason, templateKey, parameters`) $\rightarrow$ `A5AppliedTemplateResult` | `reports/newman/ADMIN-V1-FINAL-NEWMAN-REPORT-LIVE.json`<br>`reports/newman/support_complaint_e2e_regression_report.json` | **LOCKED & VERIFIED** |
| **ADM-17** | Nhóm 4: Hỗ trợ<br>17. Yêu cầu Thông tin & Hồ sơ Quỹ | Chuyển trạng thái hồ sơ sang chờ khách hàng bổ sung chứng từ (`need_more_info`), hoặc trích xuất hồ sơ tài chính quỹ nhóm liên quan (`dossier`). | `AdminA5Controller.cs`<br>`POST /api/v1/admin/support/{caseId}/request-info`<br>`POST /api/v1/admin/support/{caseId}/dossier` | Request-Info: `A5RequestInfoRequest` (`reason, message`) $\rightarrow$ `{success, caseId, status}`<br>Dossier: `OperatorDirectoryRequest` (`reason`) $\rightarrow$ `A5FundDossierResult` | `reports/newman/ADMIN-V1-FINAL-NEWMAN-REPORT-LIVE.json`<br>(Folder 04: ADM-04.4 PASS) | **LOCKED & VERIFIED** |
| **ADM-18** | Nhóm 4: Hỗ trợ<br>18. Phê duyệt Hoàn tiền / Đóng hồ sơ | Cập nhật kết quả giải quyết (`Resolved` hoặc `Rejected`). Nếu chấp thuận sai lệch giao dịch, cấp lệnh hoàn tiền qua `correction-grant` kèm `adminPin`. | `AdminController.cs`<br>`POST /api/admin/cases/support/{id}/status`<br>`POST /api/admin/cases/support/{id}/correction-grant` | Request: `UpdateCaseStatusRequest`, `GrantCorrectionRequest` (`amount, reason, adminPin`)<br>Response: `SupportCaseDetailDto` | `reports/newman/ADMIN-V1-FINAL-NEWMAN-REPORT-LIVE.json`<br>`reports/newman/support_complaint_e2e_regression_report.json` | **LOCKED & VERIFIED** |
| **ADM-19** | Nhóm 5: Ngoại vi<br>19. Phát hành Thông báo Broadcast | Vòng đời thông báo broadcast: Soạn thảo Draft $\rightarrow$ Phát hành Publish với `adminPin` $\rightarrow$ Outbox Worker phân phối đến User In-App $\rightarrow$ User đọc $\rightarrow$ Kiểm tra trạng thái Status. | `V1BroadcastController.cs`, `OperationsController.cs`<br>`POST /api/v1/system/broadcasts`<br>`POST /api/v1/system/broadcasts/{id}/publish`<br>`POST /api/v1/system/broadcasts/{id}/status` | Request: `CreateBroadcastDraftRequest`, `PublishBroadcastRequest` (`reason, adminPin`)<br>Response: `BroadcastDto` | `reports/newman/ADMIN-V1-FINAL-NEWMAN-REPORT-LIVE.json`<br>(Folder 05: ADM-05.4 PASS, chuỗi 5 bước khép kín) | **LOCKED & VERIFIED** |
| **ADM-20** | Nhóm 5: Ngoại vi<br>20. Giám sát Vận hành Gemini AI | Giám sát đo lường telemetry Trợ lý Gemini AI theo Phương án B: Request rate, Latency, Fallback Policy `SpendWisePolicy`, cảnh báo hạn ngạch. | `AdminController.cs`<br>`GET /api/admin/health`<br>`AdminAiController.cs`<br>`POST /api/admin/ai/chat` | `AdminHealthDto` (AiProvider, Database, Outbox)<br>`AdminAiChatRequest`<br>Auth: JWT Admin Role 2 | `reports/newman/ADMIN-V1-FINAL-NEWMAN-REPORT-LIVE.json`<br>(Folder 05: ADM-05.2 PASS) | **LOCKED & VERIFIED (Plan B)**<br>*(Live enterprise quota là cấu hình Google Cloud đối tác)* |
| **ADM-21** | Nhóm 5: Ngoại vi<br>21. Giám sát Ngân hàng SePay | Giám sát trạng thái kết nối Webhook IPN và kết quả đối soát nạp tiền Sandbox SePay BankHub. | `AdminBankIntegrationController.cs`<br>`GET /api/admin/integrations/sepay/status`<br>`GET /api/admin/bank-integration/reconciliation` | `SepayStatusDto` (`configured, ipnConfigured, mode: Sandbox`)<br>`BankReconciliationDto` | `reports/newman/ADMIN-V1-FINAL-NEWMAN-REPORT-LIVE.json`<br>(Folder 05: ADM-05.3 PASS) | **LOCKED & VERIFIED (Sandbox)**<br>*(Không thực hiện chuyển tiền thật trên tài khoản sản xuất)* |

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
- **Mô tả:** Tiếp nhận phân công hồ sơ cho Quản trị viên (`A5.2`) và xem chi tiết nội dung khiếu nại, giao dịch liên quan.
- **Request Body (`POST .../assign` - `A5AssignTicketRequest`):**
```json
{
  "reason": "Tiếp nhận thụ lý điều tra khiếu nại giao dịch",
  "operatorId": "a6d30123-ae6e-43b1-a3d3-0185ff8a46ec"
}
```
- **Response `200 OK` (`POST .../assign`):**
```json
{
  "success": true,
  "caseId": "c43a8401-2742-4260-bf0c-16652e944e9a"
}
```
- **Response `200 OK` (`GET .../cases/support/{id}`):** `SupportCaseDetailDto` (chi tiết hồ sơ kèm lịch sử tin nhắn).

#### ADM-16: Phản hồi Khách hàng & Áp dụng Mẫu trả lời
- **Endpoint:** `POST /api/admin/cases/support/{id}/messages` & `POST /api/v1/admin/support/{caseId}/reply-template`
- **Mô tả:** Gửi phản hồi tin nhắn trực tiếp hoặc áp dụng mẫu trả lời chuẩn hóa (`A5.7`) với `templateKey`.
- **Request Body (`POST .../reply-template` - `A5ApplyTemplateRequest`):**
```json
{
  "reason": "Áp dụng mẫu hướng dẫn cung cấp bằng chứng chuyển khoản",
  "templateKey": "NEED_MORE_INFO_BANK_RECEIPT",
  "parameters": {
    "CustomerName": "Nguyen Van A",
    "TransactionReference": "FT26102099"
  },
  "transitionToWaitingForUser": true
}
```
- **Response `200 OK` (`POST .../reply-template`):** `A5AppliedTemplateResult` (`caseId, templateKey, renderedSubject, renderedTextBody, sentAsNotification, updatedCaseStatus`).
- **Request Body (`POST .../messages` - `CaseMessageRequest`):**
```json
{
  "message": "Bộ phận kỹ thuật đã đối soát dữ liệu và tiến hành xử lý hoàn tiền."
}
```
- **Response `200 OK` (`POST .../messages`):** `SupportCaseDetailDto` đã cập nhật tin nhắn.

#### ADM-17: Yêu cầu Bổ sung Thông tin & Hồ sơ Quỹ
- **Endpoint:** `POST /api/v1/admin/support/{caseId}/request-info` & `POST /api/v1/admin/support/{caseId}/dossier`
- **Mô tả:** Chuyển trạng thái hồ sơ sang chờ khách hàng bổ sung chứng từ (`A5.3`), hoặc trích xuất hồ sơ tài chính quỹ nhóm liên quan (`A5.4`).
- **Request Body (`POST .../request-info` - `A5RequestInfoRequest`):**
```json
{
  "reason": "Cần thêm ảnh chụp sao kê ngân hàng có dấu mộc",
  "message": "Quý khách vui lòng đính kèm sao kê giao dịch ngày 20/10 để đối soát.",
  "attachments": []
}
```
- **Response `200 OK` (`POST .../request-info`):**
```json
{
  "success": true,
  "caseId": "c43a8401-2742-4260-bf0c-16652e944e9a",
  "status": "WaitingForUser"
}
```
- **Request Body (`POST .../dossier` - `OperatorDirectoryRequest`):**
```json
{
  "reason": "Trích xuất hồ sơ tài chính quỹ nhóm phục vụ phán xử khiếu nại"
}
```
- **Response `200 OK` (`POST .../dossier`):** `A5FundDossierResult` (`caseId, ticketNumber, linkedUserId, userFullName, userEmail, caseStatus, category, priority, linkedFund, linkedDisputes, timeline, attachments, financialAuditNotice`).

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
