# Admin A6 Notification & Maintenance Overview — 4/4 DONE, FUNCTIONAL BACKEND LOCKED

Date: 2026-10-19.
Baseline: `d69c97927a81210c2d1112d6172322fb9696aca3`.
A1 is LOCKED (4/4). A2 retains four DONE functions and A2.3 POLICY_BLOCKED. A3 is LOCKED (8/8). A4 is FUNCTIONAL LOCKED (6/6 with FIX-1). A5 is FUNCTIONAL LOCKED (7/7 with FIX-2 and FIX-3). This does not approve production operation or complete Backend V1.0 overall.

## HTTP contract

All routes are under `/api/v1/admin/notifications`. Supply standard bearer access token via Authorization header. Every request requires an audit `reason` (max 1000 characters). Session revocation, transaction admission locking, and canonical operator authorization apply.

| Route | Method | Body / Query | Purpose & Source |
| --- | --- | --- | --- |
| `/api/v1/admin/notifications/search` | POST | `A6NotificationQuery`: `caseId?`, `userId?`, `type?`, `fromUtc?`, `toUtc?`, `page=1`, `pageSize=20`, `reason` | A6.1: Tra cứu thông báo nghiệp vụ thực tế; scoped theo case/user/type; che/redact mã OTP (4-6 chữ số) và bearer/access tokens; phân biệt trạng thái đọc `ReadAt`. |
| `/api/v1/admin/notifications/delivery-history` | POST | `A6DeliveryHistoryQuery`: `caseId?`, `userId?`, `channel?` (InApp, Email, Push), `status?`, `fromUtc?`, `toUtc?`, `page=1`, `pageSize=20`, `reason` | A6.2: Hợp nhất lịch sử gửi trên 3 kênh In-App, Email, Mobile Push; phân biệt rõ các trạng thái `Created`, `Queued`, `Processing`, `Sent`, `ProviderAccepted`, `Failed`, `UserRead`; che email và push token. |
| `/api/v1/admin/notifications/failures` | POST | `A6DeliveryFailureQuery`: `channel?`, `fromUtc?`, `toUtc?`, `page=1`, `pageSize=20`, `reason` | A6.3: Feed tra cứu lỗi gửi thông báo (Email và Push); bao gồm số lần thử, lịch thử tiếp theo, lỗi đã được khử nhạy cảm (sanitized); read-only điều tra, không force-retry tiền/OTP. |
| `/api/v1/admin/notifications/templates` | POST | Body `OperatorDirectoryRequest`: `reason` | A6.4: Danh mục mẫu thông báo chuẩn của hệ thống (Support, Transaction, Maintenance, Security) cùng danh sách placeholder được phép; chế độ chỉ đọc, cấm sửa mẫu global. |
| `/api/v1/admin/notifications/maintenance-overview` | POST | Body `OperatorDirectoryRequest`: `reason` | A6.4: Tổng quan trạng thái bảo trì hệ thống (SystemMaintenanceState, V1MaintenancePlan đang chạy, V1MaintenancePlan đã lên lịch, EmergencyAnnouncements đang kích hoạt); chỉ đọc, cấm Admin can thiệp điều khiển bảo trì. |

## Acceptance matrix against Admin.md A6

| Function | Status | Implemented / Acceptance |
| --- | --- | --- |
| A6.1 Thông báo nghiệp vụ | DONE | Tra cứu thông báo nghiệp vụ thực tế phát sinh (OTP, giao dịch, quỹ, bảo trì, hỗ trợ); scoped theo đối tượng điều tra; khử nhạy cảm hoàn toàn không để lộ OTP/token/password; phân biệt `Sent` và `UserRead` (`ReadAt != null`). |
| A6.2 Lịch sử gửi | DONE | Tích hợp dữ liệu từ Notification (InApp), EmailOutboxMessage (Email), MobilePushDelivery (Push); đối soát trạng thái theo kênh; che số tài khoản, email nhận và push token; không giả lập trạng thái gửi thành công. |
| A6.3 Lỗi gửi | DONE | Feed tra cứu lỗi gửi thông báo từ outbox và push dispatcher; hiển thị sanitized LastError, số lần retry và thời điểm thử kế tiếp; phục vụ điều tra ticket không nhận được thông báo mà không gây side-effect. |
| A6.4 Mẫu thông báo & Bảo trì | DONE | Cung cấp danh mục mẫu thông báo chuẩn kèm allowlist placeholder an toàn; cung cấp view chỉ đọc trạng thái bảo trì toàn hệ thống / module và thông báo khẩn cấp từ SuperAdmin; nghiêm cấm Admin tạo/sửa lịch bảo trì. |

## Targeted Hardening & Security (Task 07 Hardening)
- `P0 Security Sanitization`: Nâng cấp regex khử nhạy cảm trong `AdminA6Service` để che tất cả mã số bí mật/OTP từ 4 đến 8 chữ số (`[REDACTED_CODE]`), Bearer/JWT tokens, `secret`, `password`, và `apiKey`.
- `Dynamic Pagination`: Xóa bỏ giới hạn cố định `.Take(200)` trong các truy vấn InApp, Email, và Push, cho phép phân trang linh hoạt, chính xác theo `Skip((page - 1) * pageSize).Take(pageSize)`.
- `Channel Provenance`: Đảm bảo trạng thái gửi và phân luồng kênh nguồn được phản ánh trung thực, an toàn.

## Verification and Safety Gates

- Release build: `backend/SpendWise.slnx` builds with 0 errors.
- Focused A6 tests: `AdminA6Tests` — 8 PASS / 0 FAIL / 0 SKIP (bao gồm kiểm thử canonical HTTP role/scope, khử nhạy cảm A6.1, hợp nhất đa kênh A6.2, sanitization lỗi A6.3, read-only maintenance A6.4, và kiểm tra khử mã 8 chữ số / secrets).
- Regression suite: `AdminA1HttpTests`, `AdminA2HttpTests`, `AdminA3HttpTests`, `AdminA4Tests`, `AdminA5Tests`, `AdminA6Tests`, `V1AdminLifecycleTests` — 127 PASS / 0 FAIL / 0 SKIP.
- User.md v1.3 compliance: `CanSpend = ON` direct spending rule preserved without disruption.
- SuperAdmin.md S7 boundary: Admin tuyệt đối không có endpoint kích hoạt/điều khiển/kết thúc bảo trì.
- SQL02: NOT RUN (resource gate / memory constraints preserved per project guidelines).
