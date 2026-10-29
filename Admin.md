# SPENDWISE V1.0 — ADMIN BACKEND CONTRACT & OPERATING SPECIFICATION

> **Frontend handoff update: 2026-10-29 — based on latest local Admin E2E audit.**  
> **`ADMIN CORE E2E PASSED — 80/80 requests, 138/138 assertions; Support User ↔ Admin 22/22 requests, 42/42 assertions.`**  
> - **Backend User Status:** Đã nghiệm thu và KHÓA CHỨC NĂNG (`BACKEND USER DEMO — ACCEPTED & LOCKED`, 306 requests, 343/343 assertions PASS).  
> - **Backend Admin Status:** Bộ kiểm thử Admin local cuối ngày 29/10/2026 đã PASS; .NET targeted 82/82 PASS. **Chưa tuyên bố toàn bộ Backend User regression đã chạy lại sau thay đổi Admin** (bộ 306/343 là baseline 27/10). Gemini/SePay real-provider và giao dịch tiền thật không nằm trong phạm vi đã xác minh.  
> - **Bằng chứng Nghiệm thu Lịch sử (Historical Acceptance Evidence):** Đợt kiểm thử Full Newman E2E toàn diện ngày 2026-10-20 trên database `SpendWise_E2E_G4` (141/141 requests PASS, 227/227 assertions PASS) được ghi nhận tại [`backend/docs/ANTIGRAVITY-G5-FINAL-BACKEND-SECURITY-ACCEPTANCE.md`](./backend/docs/ANTIGRAVITY-G5-FINAL-BACKEND-SECURITY-ACCEPTANCE.md). Cần phân biệt rõ: con số 141 requests / 227 assertions này là của **bộ kiểm thử tích hợp E2E toàn trình (Full E2E suite)** kết hợp các luồng User liên hoàn và các thao tác kiểm chứng Admin ADM-01 đến ADM-05, tuyệt đối không phải toàn bộ 141 test là Admin-only. Đây là bằng chứng lịch sử (historical baseline), không phải lượt chạy mới.  
> - **Mục đích hiện tại:** Định nghĩa chính xác UI Admin V1.0, 5 phân hệ/21 chức năng, quyền truy cập, route và vòng đời tương tác để Codex triển khai Frontend. Các chỉnh sửa Backend Admin mới nhất còn **ở workspace local, chưa push lên GitHub `main`** tại thời điểm tài liệu được cập nhật.

---

## 1. VAI TRÒ VÀ RANH GIỚI BẢO MẬT (ROLES & SECURITY BOUNDARIES)

### 1.1. Chính sách Quản trị viên duy nhất (Single Admin Policy)
- Hệ thống SpendWise V1.0 thiết lập **duy nhất một tài khoản Quản trị viên hệ thống hoạt động** (`SystemRole.Admin`).
- **Chỉ `SystemRole.Admin = 2` được phép quản trị.** Giá trị legacy `SystemAdmin = 0`, `SuperAdmin = 3` không được cấp quyền Admin hoặc đăng nhập để sử dụng tính năng. Không xây UI quản trị operator hay cấp Admin thứ hai. Code local đã bổ sung guard; E2E 0/3 trả 401, chưa coi đây là bằng chứng kiểm thử token legacy được ký hợp lệ.
- **Phân định ranh giới Chủ quỹ (Fund Owner) và Quản trị viên (System Admin):**
  - Chủ quỹ (`Fund Owner`) là một người dùng thông thường (`SystemRole.User`) có quyền điều hành **quỹ mình sở hữu**; hoàn toàn **không có quyền Admin hệ thống**.
  - Quản trị viên hệ thống (`Admin`) không tự ý can thiệp hoặc thay đổi số dư quỹ nhóm trừ khi có yêu cầu xử lý khiếu nại/tranh chấp hợp lệ được ghi nhận qua Case hồ sơ.

### 1.2. Ranh giới truy cập thông tin nhạy cảm theo Hồ sơ hỗ trợ (Case-Scoped Access)
- Tài khoản Admin phải đáp ứng đồng thời 5 điều kiện: vai trò `Admin`, trạng thái `Active`, email đã xác thực, không bị yêu cầu đổi mật khẩu (`MustChangePassword = false`), và phiên hoạt động hợp lệ.
- **Nguyên tắc truy cập nhạy cảm (Zero Unbounded Reads):**
  - Admin **không có quyền đọc toàn cục** dữ liệu tài chính, số dư ví, danh sách bạn bè hay lịch sử giao dịch cá nhân của người dùng.
  - Mọi thao tác tra cứu hồ sơ người dùng, phiên thiết bị, nhóm quỹ, hoặc lịch sử giao dịch **bắt buộc phải gắn liền với một Hồ sơ hỗ trợ đang hoạt động (`active SupportCase`)** được phân công trực tiếp cho Admin đó (`POST /api/v1/admin/support/{caseId}/users/{userId}/...`).
  - Mỗi truy cập nhạy cảm đều được ghi nhận dấu vết kiểm toán (Audit Trail) không thể đảo ngược kèm lý do nghiệp vụ bắt buộc (`reason`).
- **Bảo mật tuyệt đối:** Admin không bao giờ được nhìn thấy mật khẩu thô, mã PIN, mã OTP, khóa bí mật sinh trắc học, hoặc token JWT của người dùng. Admin tuyệt đối không có quyền tự ý sửa đổi số dư tài khoản sổ cái (`LedgerAccount.Balance`).

---

## 2. NĂM NHÓM CHỨC NĂNG ADMIN CHUẨN HÓA (ADM-01 ĐẾN ADM-05)

### ADM-01 — Dashboard & Báo cáo Tổng quan (Overview)
- **Tuyến đường chuẩn (Canonical Root):** `GET /api/v1/admin/overview` (hỗ trợ tham số `date`).
- **Tuyến đường đã kiểm thử:** `GET /api/v1/admin/overview/tasks` (công việc), `GET /api/v1/admin/overview/alerts` (cảnh báo). Các route nguồn dữ liệu/hoạt động khác chỉ đưa vào UI nếu có trong API của Backend triển khai.
  - *Tương thích ngược:* `GET /api/admin/dashboard` và `/api/admin/reports` đóng vai trò là các projection tương thích.
- **Quy tắc an toàn:**
  - Thống kê tổng hợp số lượng người dùng, quỹ nhóm, vụ việc khiếu nại và khối lượng giao dịch từ dữ liệu thật.
  - Ranh giới ngày làm việc tính chuẩn theo múi giờ Việt Nam (UTC+7).
  - Nghiêm cấm đếm trùng (double-counting) các giao dịch phản chiếu (mirrored financial transactions).
  - Số liệu tổng quan không cấp quyền truy cập chi tiết vào dữ liệu người dùng nếu không có Case được chỉ định.

### ADM-02 — Quản lý Người dùng & Chế tài Tài khoản (User Management)
- **Tra cứu người dùng:** `GET /api/admin/users?search={keyword}&page={page}&pageSize={pageSize}` (tìm kiếm, phân trang, lọc trạng thái theo API thực tế).
- **Truy cập chi tiết theo Case hỗ trợ:**
  - `POST /api/v1/admin/support/{caseId}/users/{userId}/profile`
  - `POST /api/v1/admin/support/{caseId}/users/{userId}/sessions`
  - `POST /api/v1/admin/support/{caseId}/users/{userId}/restrictions`
  - `POST /api/v1/admin/support/{caseId}/users/{userId}/revoke-sessions`
  - `POST /api/v1/admin/support/{caseId}/users/{userId}/groups`
  - `POST /api/v1/admin/support/{caseId}/users/{userId}/history`
- **Áp dụng chế tài tài khoản (`PUT /api/admin/users/{userId}/status`):**
  - Payload yêu cầu: `{ "status": "Active|Restricted|Locked", "reason": "Lý do cụ thể", "adminPin": "<admin-pin-6-so>" }`.
  - Thực thi nguyên tử trong transaction: cập nhật trạng thái, thu hồi phiên làm việc, ghi nhận nhật ký kiểm toán (`AuditLog`), và gửi thông báo cảnh báo bảo mật (`SecurityAlert`).
  - **Hai cấp độ chế tài tài khoản:**
    - **Cấp độ 1 — Hạn chế tài chính (`UserStatus.Restricted`):** Chặn toàn bộ lệnh ghi nợ tiền ra (chuyển tiền ví, P2P gửi đi, chi tiêu, nộp quỹ nhóm, rút tiền); **vẫn cho phép đăng nhập, làm mới token, đọc số dư/hồ sơ, gửi yêu cầu hỗ trợ và nhận tiền vào (inbound P2P credits / refunds)**.
    - **Cấp độ 2 — Khóa tài khoản (`UserStatus.Locked`):** Thu hồi ngay lập tức toàn bộ phiên hoạt động và refresh token. Chặn hoàn toàn đăng nhập (`HTTP 403 account_unavailable`), chặn làm mới token. Khi Admin mở khóa (`Active`), người dùng bắt buộc phải đăng nhập lại từ đầu (không thể phục hồi phiên cũ).
  - Không thể áp dụng chế tài lên tài khoản Admin đồng cấp hoặc tài khoản đã xóa/vô hiệu hóa vĩnh viễn.

### ADM-03 — Hỗ trợ Khách hàng & Giải quyết Tranh chấp (Support & Disputes)
- **Tiếp nhận Hỗ trợ/Khiếu nại trực tiếp:** User gọi `POST /api/cases/support`, với `category = general` hoặc `complaint_transaction` theo trường hợp. Không tạo Dispute độc lập mới: `POST /api/cases/disputes` → `400 dispute_workflow_deprecated`; dữ liệu Dispute lịch sử vẫn được đọc.
- **API thực tế Admin Support:** `GET /api/admin/cases/support` (danh sách), `GET /api/admin/cases/support/{id}` (chi tiết), `POST /api/admin/cases/support/{id}/messages` (phản hồi), `POST /api/admin/cases/support/{id}/status` (cập nhật/giải quyết). Khi điều tra dữ liệu nhạy cảm cần **SupportCase đang hoạt động, được phân công**, kèm `reason` cho các endpoint scoped.
- **Luồng UI:** User mở yêu cầu → Admin xem/tiếp nhận → Admin phản hồi hoặc yêu cầu bổ sung → User trả lời → Admin `resolved`/`rejected` → User đọc kết quả và notification.
- **Fixed Fund V2:** `POST /api/v1/funds/requests/{id}/report` tạo SupportCase khi báo cáo chi tiêu quỹ bất thường. Admin đọc hồ sơ theo scope; **không tự đảo bút toán, không tự hoàn tiền**, không khôi phục quy trình Dispute/Pending24h cũ.
- **Lịch sử:** `GET /api/admin/cases/disputes` chỉ dành cho hồ sơ legacy nếu được Backend hỗ trợ; không có màn hình tạo mới.

### ADM-04 — Quản lý Thông báo Hệ thống (Notifications)
- **Truy vấn thông báo:**
  - `POST /api/v1/admin/notifications/search`: Lịch sử thông báo hệ thống.
  - `POST /api/v1/admin/notifications/delivery-history`: Lịch sử phát thông báo.
  - `POST /api/v1/admin/notifications/failures`: Danh sách thông báo gửi thất bại để xử lý lại.
  - `POST /api/v1/admin/notifications/templates`: Quản lý mẫu thông báo chuẩn.
- **Phát sóng thông báo toàn hệ thống (System Broadcasts):**
  - `POST /api/v1/system/broadcasts`: Tạo thông báo phát sóng mới.
  - `POST /api/v1/system/broadcasts/{id}/publish`: Phát hành thông báo (yêu cầu mã PIN Admin).
  - `POST /api/v1/system/broadcasts/{id}/cancel`: Hủy thông báo chưa phát.
  - `POST /api/v1/system/broadcasts/{id}/status`: Kiểm tra tiến độ gửi thông báo.
- **Bảo mật:** Dữ liệu thông báo tuyệt đối không hiển thị mã OTP hoặc token khôi phục mật khẩu.

### ADM-05 — Giám sát & Bảo trì Hệ thống (System Operations & Maintenance)
- **Tuyến đường chuẩn:** `/api/v1/system/maintenance`.
  - `GET /api/v1/system/maintenance`: Xem trạng thái bảo trì hiện tại.
  - `POST /api/v1/system/maintenance`: Lập lịch kế hoạch bảo trì.
  - `POST /api/v1/system/maintenance/{id}/emergency`: Kích hoạt bảo trì khẩn cấp.
  - `POST /api/v1/system/maintenance/{id}/extend`: Gia hạn thời gian bảo trì.
  - `POST /api/v1/system/maintenance/{id}/close`: Đóng bảo trì và mở lại hệ thống.
- **Cơ chế Cổng An toàn Sổ cái (`V1LedgerMaintenanceSafety`):**
  - Trước khi cho phép mở lại hệ thống sau bảo trì, hệ thống tự động kiểm tra nghiêm ngặt tính toàn vẹn tài chính:
    1. Không có giao dịch sổ cái nào đang ở trạng thái `Processing` hoặc `Pending`.
    2. Tổng số dư hiển thị của toàn bộ ví phải khớp 100% với tài khoản sổ cái tương ứng (`wallet.AvailableBalance == account.Balance`).
    3. Tổng phát sinh Nợ phải bằng tổng phát sinh Có trên toàn bộ giao dịch sổ cái.
    4. Mọi bất thường sẽ chặn ngay lập tức việc mở lại cổng tài chính, kích hoạt cảnh báo đỏ cho quản trị viên.

---

## 2B. SITEMAP, 21 CHỨC NĂNG & ĐIỀU KIỆN UI ADMIN V1.0

| Phân hệ | Chức năng và màn hình UI | API chính đã kiểm thử |
|---|---|---|
| **ADM-01 (4)** | Dashboard tổng quan; overview; tasks; alerts | `GET /api/admin/dashboard`, `GET /api/v1/admin/overview`, `/overview/tasks`, `/overview/alerts` |
| **ADM-02 (4)** | User list/search; hồ sơ Case-scoped; xem session; đổi Active/Restricted/Locked và revoke sessions | `GET /api/admin/users`, `POST /api/v1/admin/support/{caseId}/users/{userId}/profile`, `.../sessions`, `PUT /api/admin/users/{id}/status`, `POST /api/admin/users/{id}/revoke-sessions` |
| **ADM-03 (4 + flow)** | Hàng đợi support; chi tiết/lịch sử; phản hồi; cập nhật trạng thái; chuỗi User↔Admin | `GET /api/admin/cases/support`, `GET .../{id}`, `POST .../{id}/messages`, `POST .../{id}/status` |
| **ADM-04 (4)** | Lịch sử delivery; Outbox health; templates; Broadcast draft → publish → User inbox/read → Admin read count | `POST /api/v1/admin/notifications/delivery-history`, `GET /api/admin/health`, `POST /api/v1/admin/notifications/templates`, `POST /api/v1/system/broadcasts` |
| **ADM-05 (5)** | Health; AI/Gemini telemetry; SePay status/reconciliation/events; maintenance; audit logs/feature flags | `GET /api/admin/health`, `POST /api/admin/ai/chat`, `GET /api/admin/integrations/sepay/status`, `GET /api/system/maintenance`, `GET /api/admin/audit-logs` |

**Xác thực:** `POST /api/auth/login` (nhận challengeToken) → `POST /api/auth/pin/verify` (nhận JWT) → gọi API với Bearer. Mọi thay đổi trạng thái User truyền **`adminPin`**; Broadcast dùng **`securityPin`**. Không hardcode PIN hay tài khoản test trong frontend.

**Lưu ý quyền:** Admin chỉ 1 tài khoản, không UI tạo Admin thứ hai. User không vào API/Admin UI. Fund Owner là quyền User trong quỹ. Legacy role 0/3 không vào Admin. Các màn chi tiết tài chính/case phải tuân thủ scope + reason + audit; không tạo màn hình đọc toàn bộ lịch sử giao dịch cá nhân nếu API không cấp phép.

**Restricted:** chặn ghi nợ, vẫn login/đọc/nhận tiền/gửi Support. **Locked:** chặn login/refresh, phiên cũ hết hiệu lực, mở khóa phải login lại. Admin có thể revoke sessions nhưng không được sửa trực tiếp Ledger hoặc số dư.

**Thông báo:** Một Broadcast chỉ coi là phát thành công khi User nhận trong `GET /api/notifications`, có thể `POST /api/notifications/{id}/read`, và Admin thấy read count > 0. Không coi `Scheduled` hay queue enabled đồng nghĩa đã phát thành công.

**Telemetry trung thực:** Gemini `Configured`/fallback `SpendWisePolicy` không chứng minh Live Gemini; SePay ở SQL02 `configured: false, ipnConfigured: false`, webhook/provider live chưa được xác minh. UI hiển thị trạng thái thật, không tô nhãn connected/live sai.

**Ranh giới repo:** Local Backend acceptance mới nhất: Admin Newman 80/138 PASS; Support 22/42 PASS, .NET targeted 82/82 PASS. User 306/343 là baseline 27/10, **chưa phải regression chạy lại sau thay đổi local**. GitHub `main` có thể chưa chứa diff Backend local, Codex không được xem route chưa tồn tại trên server đang chạy là đã triển khai. Khi tích hợp, kiểm tra response/DTO tại API thật trước.

---

## 3. DANH MỤC MÃ LỖI VÀ XỬ LÝ LỖI ADMIN (ERROR CODES)

| Mã lỗi (Code) | HTTP Status | Ý nghĩa nghiệp vụ |
|---|---|---|
| `unauthorized` | 401 | Thiếu Bearer Token hoặc token đã hết hạn |
| `forbidden` | 403 | Tài khoản không có vai trò `Admin` |
| `account_unavailable` | 403 | Tài khoản Admin bị vô hiệu hóa hoặc cố tình dùng vai trò SuperAdmin |
| `admin_must_change_password` | 403 | Admin bắt buộc phải đổi mật khẩu khởi tạo trước khi thao tác |
| `case_required` | 400 | Thao tác tra cứu nhạy cảm thiếu mã Case hồ sơ hỗ trợ |
| `case_not_found` | 404 | Không tìm thấy hồ sơ hỗ trợ tương ứng |
| `case_not_assigned_to_caller` | 403 | Hồ sơ hỗ trợ chưa được phân công cho Admin hiện tại |
| `invalid_admin_pin` | 400 | Mã PIN quản trị viên không chính xác |
| `dispute_workflow_deprecated` | 400 | Không cho tạo Dispute độc lập mới (lịch sử cũ vẫn đọc được) |
| `maintenance_safety_gate_failed` | 409 | Sổ cái kế toán chưa cân bằng, không thể đóng cổng bảo trì |

---

## 4. HƯỚNG DẪN TÍCH HỢP FRONTEND CHO CODEX

1. Dùng bảng sitemap và API ở mục 2B cho 5 menu Admin; không sinh menu SuperAdmin/Operators hay mở Dispute mới.
2. Xây client API typed, error/loading/empty states, auth token/PIN flow, các modal xác nhận thay đổi trạng thái và Broadcast.
3. Không hardcode response mẫu hoặc hiển thị số dư/lịch sử giao dịch User ngoài quyền Case-scoped; mọi mutation cần reason và xác nhận phù hợp.
4. Theo API đang chạy ở workspace đã nghiệm thu. Cập nhật đồng bộ Backend local → GitHub hoặc kết nối đúng endpoint trước khi kiểm thử UI.
5. Không thay đổi Backend/User Ledger để phù hợp với giả định của UI; điều chỉnh UI theo contract thực tế.
