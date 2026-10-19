# Admin A6 Notification & Maintenance Overview — 4/4 DONE, FUNCTIONAL BACKEND LOCKED

Date: 2026-10-19.
Baseline: `a5b19d9f13bcde0516c239ff87dc5b60075ba131`.
A1 is LOCKED (4/4). A2 retains four DONE functions and A2.3 POLICY_BLOCKED. A3 is LOCKED (8/8). A4 is FUNCTIONAL LOCKED (6/6). A5 is FUNCTIONAL LOCKED (7/7). A6 is FUNCTIONAL LOCKED (4/4 with Task 09 Case-Enforced Security Hardening). This does not approve production operation or complete Backend V1.0 overall.

## HTTP contract

All routes are under `/api/v1/admin/notifications`. Supply standard bearer access token via Authorization header. Every request requires an audit `reason` (max 1000 characters). Session revocation, transaction admission locking, active assigned case verification, and canonical operator authorization apply.

| Route | Method | Body / Query | Purpose & Source |
| --- | --- | --- | --- |
| `/api/v1/admin/notifications/search` | POST | `A6NotificationQuery`: `caseId` (bắt buộc), `userId?`, `type?`, `fromUtc?`, `toUtc?`, `page=1`, `pageSize=20`, `reason` | A6.1: Tra cứu thông báo nghiệp vụ thực tế; bắt buộc có CaseId đang mở được phân công cho Admin gọi; kiểm tra `HasSupportScopeAsync`; che/redact mã OTP (4-8 chữ số) và bearer/access tokens; phân biệt trạng thái đọc `ReadAt`. |
| `/api/v1/admin/notifications/delivery-history` | POST | `A6DeliveryHistoryQuery`: `caseId` (bắt buộc), `userId?`, `channel?` (InApp, Email, Push), `status?`, `fromUtc?`, `toUtc?`, `page=1`, `pageSize=20`, `reason` | A6.2: Hợp nhất lịch sử gửi trên 3 kênh In-App, Email, Mobile Push; bắt buộc có CaseId đang mở được phân công cho Admin gọi; phân biệt rõ các trạng thái `Created`, `Queued`, `Processing`, `Sent`, `ProviderAccepted`, `Failed`, `UserRead`; che email và push token. |
| `/api/v1/admin/notifications/failures` | POST | `A6DeliveryFailureQuery`: `channel?`, `fromUtc?`, `toUtc?`, `page=1`, `pageSize=20`, `reason` | A6.3: Feed tra cứu lỗi gửi thông báo (Email và Push); bao gồm số lần thử, lịch thử tiếp theo, lỗi đã được khử nhạy cảm (sanitized) và che email/push token; read-only điều tra, không lộ thông tin cá nhân. |
| `/api/v1/admin/notifications/templates` | POST | Body `OperatorDirectoryRequest`: `reason` | A6.4: Danh mục mẫu thông báo chuẩn của hệ thống (Support, Transaction, Maintenance, Security) cùng danh sách placeholder được phép; chế độ chỉ đọc, cấm sửa mẫu global. |
| `/api/v1/admin/notifications/maintenance-overview` | POST | Body `OperatorDirectoryRequest`: `reason` | A6.4: Tổng quan trạng thái bảo trì hệ thống (SystemMaintenanceState, V1MaintenancePlan đang chạy, V1MaintenancePlan đã lên lịch, EmergencyAnnouncements đang kích hoạt); chỉ đọc, cấm Admin can thiệp điều khiển bảo trì. |

## Acceptance matrix against Admin.md A6

| Function | Status | Implemented / Acceptance |
| --- | --- | --- |
| A6.1 Thông báo nghiệp vụ | DONE | Tra cứu thông báo nghiệp vụ thực tế phát sinh (OTP, giao dịch, quỹ, bảo trì, hỗ trợ); bắt buộc scoped theo support case đang hoạt động thuộc quyền quản lý; khử nhạy cảm hoàn toàn không để lộ OTP/token/password; phân biệt `Sent` và `UserRead` (`ReadAt != null`). |
| A6.2 Lịch sử gửi | DONE | Tích hợp dữ liệu từ Notification (InApp), EmailOutboxMessage (Email), MobilePushDelivery (Push); bắt buộc scoped theo support case của đối tượng điều tra; đối soát trạng thái theo kênh; che số tài khoản, email nhận và push token; không giả lập trạng thái gửi thành công. |
| A6.3 Lỗi gửi | DONE | Feed tra cứu lỗi gửi thông báo từ outbox và push dispatcher; hiển thị sanitized LastError, số lần retry và thời điểm thử kế tiếp; che email và push token người nhận; phục vụ điều tra ticket không nhận được thông báo mà không gây rò rỉ dữ liệu toàn hệ thống. |
| A6.4 Mẫu thông báo & Bảo trì | DONE | Cung cấp danh mục mẫu thông báo chuẩn kèm allowlist placeholder an toàn; cung cấp view chỉ đọc trạng thái bảo trì toàn hệ thống / module và thông báo khẩn cấp từ SuperAdmin; nghiêm cấm Admin tạo/sửa lịch bảo trì. |

## Targeted Hardening & Security (Task 09 Final Closure)
- `P0 Access Control & IDOR Protection`: Admin không được tra cứu thông báo hoặc lịch sử gửi của User tùy tiện. Mọi truy vấn `search` và `delivery-history` bắt buộc có `CaseId` hợp lệ, đang mở (không phải `Resolved`/`Closed`), và được phân công đúng cho Admin đang gửi request.
- `Negative Tests Added`: Bổ sung 6 negative tests kiểm tra các kịch bản:
  1. Thiếu `CaseId` trong `NotificationsAsync` -> ném `ForbiddenException("case_access_denied")`.
  2. Thiếu `CaseId` trong `DeliveryHistoryAsync` -> ném `ForbiddenException("case_access_denied")`.
  3. IDOR: Admin truy cập Case của Admin khác -> ném `ForbiddenException("case_access_denied")`.
  4. Mismatch UserId: `UserId` không khớp đối tượng trong Case -> ném `ForbiddenException("case_access_denied")`.
  5. Case đã Resolved/Closed -> ném `ForbiddenException("case_access_denied")`.
  6. Operator session bị revoked -> ném `ForbiddenException("permission_denied")`.
- `P0 Security Sanitization`: Toàn bộ mã số OTP từ 4-8 chữ số, bearer tokens, passwords, API keys đều được khử nhạy cảm.

## Verification and Safety Gates

- Release build: `backend/SpendWise.slnx` builds with 0 errors.
- Focused A6 tests: `AdminA6Tests` — 14 PASS / 0 FAIL / 0 SKIP (bao gồm kiểm thử canonical HTTP role/scope, khử nhạy cảm A6.1, hợp nhất đa kênh A6.2, sanitization lỗi A6.3, read-only maintenance A6.4, kiểm tra khử mã 8 chữ số, và toàn bộ 6 negative tests).
- Regression suite: 127 PASS / 0 FAIL / 0 SKIP cho Admin suites (`FullyQualifiedName~AdminA`).
- Full non-SQL regression: 1046 PASS / 0 FAIL / 0 SKIP (`FullyQualifiedName!~SqlServer`).
- User.md v1.3 compliance: `CanSpend = ON` direct spending rule preserved without disruption.
- SuperAdmin.md S7 boundary: Admin tuyệt đối không có endpoint kích hoạt/điều khiển/kết thúc bảo trì.
- SQL02: NOT RUN (resource gate / memory constraints preserved per project guidelines).
