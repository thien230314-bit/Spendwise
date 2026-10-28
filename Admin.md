# SPENDWISE V1.0 — ADMIN BACKEND CONTRACT & OPERATING SPECIFICATION

> **Current Phase Status (2026-10-27):**  
> **`PREPARING FOR PHASE BACKEND ADMIN (USER DEMO LOCKED)`**  
> - **Backend User Status:** Đã nghiệm thu và KHÓA CHỨC NĂNG (`BACKEND USER DEMO — ACCEPTED & LOCKED`, 306 requests, 343/343 assertions PASS).  
> - **Backend Admin Status:** **CHƯA KIỂM THỬ LẠI SAU CÁC THAY ĐỔI MỚI (PENDING RE-VERIFICATION)**. Không đánh đồng việc Backend User đã nghiệm thu với việc Phase Admin đã hoàn tất.  
> - **Bằng chứng Nghiệm thu Lịch sử (Historical Acceptance Evidence):** Đợt kiểm thử Full Newman E2E toàn diện ngày 2026-10-20 trên database `SpendWise_E2E_G4` (141/141 requests PASS, 227/227 assertions PASS) được ghi nhận tại [`backend/docs/ANTIGRAVITY-G5-FINAL-BACKEND-SECURITY-ACCEPTANCE.md`](./backend/docs/ANTIGRAVITY-G5-FINAL-BACKEND-SECURITY-ACCEPTANCE.md). Cần phân biệt rõ: con số 141 requests / 227 assertions này là của **bộ kiểm thử tích hợp E2E toàn trình (Full E2E suite)** kết hợp các luồng User liên hoàn và các thao tác kiểm chứng Admin ADM-01 đến ADM-05, tuyệt đối không phải toàn bộ 141 test là Admin-only. Đây là bằng chứng lịch sử (historical baseline), không phải lượt chạy mới.  
> - **Nhiệm vụ trọng tâm Phase Admin tiếp theo:** Re-verify toàn bộ các test cases thực sự thuộc 5 phân hệ Admin (ADM-01 đến ADM-05), bảo đảm tương thích tuyệt đối với các thay đổi mới nhất về Bank Ownership, Passkeys/WebAuthn, và Fixed Fund V2.

---

## 1. VAI TRÒ VÀ RANH GIỚI BẢO MẬT (ROLES & SECURITY BOUNDARIES)

### 1.1. Chính sách Quản trị viên duy nhất (Single Admin Policy)
- Hệ thống SpendWise V1.0 thiết lập **duy nhất một tài khoản Quản trị viên hệ thống hoạt động** (`SystemRole.Admin`).
- **Tuyệt đối không có vai trò `SuperAdmin`:** Không có endpoint đăng nhập SuperAdmin, không có quy trình leo thang quyền hạn (escalation), và không có giao diện quản lý phân quyền Admin. Mọi yêu cầu đăng nhập với vai trò SuperAdmin đều bị chặn dứt khoát với mã lỗi `account_unavailable` (HTTP 403).
- **Phân định ranh giới Chủ quỹ (Fund Owner) và Quản trị viên (System Admin):**
  - Chủ quỹ (`Fund Owner`) là một người dùng thông thường (`SystemRole.User`) có quyền hạn điều hành bên trong một Quỹ Nhóm cụ thể; hoàn toàn **không có bất kỳ quyền hạn Admin hệ thống nào**.
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
- **Các tuyến đường chi tiết:**
  - `GET /api/v1/admin/tasks`: Danh sách các tác vụ quản trị cần xử lý.
  - `GET /api/v1/admin/sources/{metric}`: Chỉ số thống kê tăng trưởng người dùng, số lượng quỹ tạo mới, khối lượng giao dịch.
  - `GET /api/v1/admin/alerts`: Cảnh báo hệ thống và dấu hiệu bất thường.
  - `GET /api/v1/admin/activity`: Nhật ký hoạt động quản trị gần nhất.
  - *Tương thích ngược:* `GET /api/admin/dashboard` và `/api/admin/reports` đóng vai trò là các projection tương thích.
- **Quy tắc an toàn:**
  - Thống kê tổng hợp số lượng người dùng, quỹ nhóm, vụ việc khiếu nại và khối lượng giao dịch từ dữ liệu thật.
  - Ranh giới ngày làm việc tính chuẩn theo múi giờ Việt Nam (UTC+7).
  - Nghiêm cấm đếm trùng (double-counting) các giao dịch phản chiếu (mirrored financial transactions).
  - Số liệu tổng quan không cấp quyền truy cập chi tiết vào dữ liệu người dùng nếu không có Case được chỉ định.

### ADM-02 — Quản lý Người dùng & Chế tài Tài khoản (User Management)
- **Tra cứu người dùng:** `POST /api/v1/admin/users/search` (phân trang, lọc theo trạng thái).
- **Truy cập chi tiết theo Case hỗ trợ:**
  - `POST /api/v1/admin/support/{caseId}/users/{userId}/profile`
  - `POST /api/v1/admin/support/{caseId}/users/{userId}/sessions`
  - `POST /api/v1/admin/support/{caseId}/users/{userId}/restrictions`
  - `POST /api/v1/admin/support/{caseId}/users/{userId}/revoke-sessions`
  - `POST /api/v1/admin/support/{caseId}/users/{userId}/groups`
  - `POST /api/v1/admin/support/{caseId}/users/{userId}/history`
- **Áp dụng chế tài tài khoản (`PUT /api/v1/admin/support/{caseId}/users/{userId}/status`):**
  - Payload yêu cầu: `{ "status": "Active|Restricted|Locked", "reason": "Lý do cụ thể", "securityPin": "<admin-pin-6-so>" }`.
  - Thực thi nguyên tử trong transaction: cập nhật trạng thái, thu hồi phiên làm việc, ghi nhận nhật ký kiểm toán (`AuditLog`), và gửi thông báo cảnh báo bảo mật (`SecurityAlert`).
  - **Hai cấp độ chế tài tài khoản:**
    - **Cấp độ 1 — Hạn chế tài chính (`UserStatus.Restricted`):** Chặn toàn bộ lệnh ghi nợ tiền ra (chuyển tiền ví, P2P gửi đi, chi tiêu, nộp quỹ nhóm, rút tiền); **vẫn cho phép đăng nhập, làm mới token, đọc số dư/hồ sơ, gửi yêu cầu hỗ trợ và nhận tiền vào (inbound P2P credits / refunds)**.
    - **Cấp độ 2 — Khóa tài khoản (`UserStatus.Locked`):** Thu hồi ngay lập tức toàn bộ phiên hoạt động và refresh token. Chặn hoàn toàn đăng nhập (`HTTP 403 account_unavailable`), chặn làm mới token. Khi Admin mở khóa (`Active`), người dùng bắt buộc phải đăng nhập lại từ đầu (không thể phục hồi phiên cũ).
  - Không thể áp dụng chế tài lên tài khoản Admin đồng cấp hoặc tài khoản đã xóa/vô hiệu hóa vĩnh viễn.

### ADM-03 — Hỗ trợ Khách hàng & Giải quyết Tranh chấp (Support & Disputes)
- **Tiếp nhận khiếu nại:** Người dùng gửi yêu cầu qua `/api/cases/support` và `/api/cases/disputes`.
- **Xử lý quản trị (`/api/v1/admin/support`):**
  - `POST /api/v1/admin/support/tickets/search`: Tìm kiếm danh sách yêu cầu.
  - `POST /api/v1/admin/support/assigned`: Danh sách yêu cầu được phân công cho Admin.
  - `POST /api/v1/admin/support/{caseId}/assign`: Tự nhận hoặc phân công hồ sơ.
  - `POST /api/v1/admin/support/{caseId}/request-info`: Yêu cầu người dùng bổ sung thông tin (`WaitingForUser`).
  - `POST /api/v1/admin/support/{caseId}/dossier`: Hồ sơ bằng chứng đầy đủ của vụ việc.
  - `POST /api/v1/admin/support/{caseId}/resolve`: Đóng/giải quyết vụ việc thông thường (`Resolved` / `Rejected`).
- **Quy trình Xử lý Tranh chấp Chi tiêu Quỹ (ADM-03 & Fixed Fund V2):**
  - **Khoản chi đã hoàn tất (`Completed`):** Trong kiến trúc Fixed Fund V2, chi tiêu hợp lệ được giải ngân tức thì (không có trạng thái tạm giữ `Pending24h` hay tự động chuyển sang `Suspended`). Khi thành viên báo cáo bất thường về một khoản chi đã `Completed`, hệ thống **bắt buộc phải tạo Hồ sơ Hỗ trợ / Tranh chấp (`SupportCase` / `Dispute`)** qua `/api/cases/support` hoặc `/api/cases/disputes`. Admin tiếp nhận hồ sơ, tra cứu dữ liệu kế toán (`POST /api/v1/admin/support/{caseId}/funds/{fundId}/finance/Expenses`) và hồ sơ bằng chứng. **Admin tuyệt đối không tự ý đảo ngược bút toán kế toán (no arbitrary reverse posting)** của các giao dịch đã hoàn tất mà không thông qua quy trình hòa giải hoặc bồi hoàn được phê duyệt.
  - **Luồng xử lý tranh chấp giao dịch chưa hoàn tất (nếu có yêu cầu tạm hoãn / pending):** Đối với các yêu cầu chi tiêu chưa hoàn tất hoặc bị tạm hoãn xem xét (`POST /api/v1/admin/support/disputes/{requestId}/resolve`):
    - *Báo cáo hợp lệ (`!IsRejected`):* Hủy yêu cầu chi tiêu (`Cancelled`), giải phóng số tiền bảo lưu hoàn trả về số dư khả dụng của quỹ nhóm, đánh dấu SupportCase `Resolved`, ghi log kiểm toán và gửi thông báo cho toàn bộ thành viên quỹ.
    - *Báo cáo không hợp lệ (`IsRejected`):* Bác bỏ khiếu nại, đánh dấu SupportCase `Rejected`, tiếp tục tiến trình xử lý chi tiêu thông thường.

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
  - `GET /api/v1/system/broadcasts/{id}/status`: Kiểm tra tiến độ gửi thông báo.
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
| `disputed_request_not_suspended` | 409 | Yêu cầu chi tiêu không ở trạng thái chờ/tạm treo hợp lệ để xử lý tranh chấp trực tiếp |
| `maintenance_safety_gate_failed` | 409 | Sổ cái kế toán chưa cân bằng, không thể đóng cổng bảo trì |

---

## 4. KẾ HOẠCH THỰC THI CHO PHASE BACKEND ADMIN

1. **Bước 1 — Audit lại Test Suite Admin:**
   - Bóc tách và so chiếu số lượng requests thực sự thuộc phạm vi Admin (ADM-01 đến ADM-05) với controllers C# hiện hành (phân biệt rõ ràng với 141 requests của bộ Full E2E tích hợp lịch sử).
   - Chuẩn bị environment và dynamic test runId riêng biệt trên database `SpendWise_E2E_G4`.
2. **Bước 2 — Xác minh tính tương thích ngược (Regression Check):**
   - Đảm bảo các thay đổi mới nhất về `BankConnections` (Ownership restoration) và `Passkeys` không làm hỏng các luồng Admin tra cứu User sessions/history.
   - Xác minh các quy tắc Fixed Fund V2 (chi tiêu tức thời, bãi bỏ 20%/Pending24h) đồng bộ chính xác với API hỗ trợ ADM-03.
3. **Bước 3 — Thực thi Newman Run cho Phase Admin:**
   - Chạy bộ test kiểm thử Admin chuyên biệt theo phạm vi ADM-01 đến ADM-05.
   - Thu thập raw JSON report và lập bảng nghiệm thu chính thức cho Phase Admin.
