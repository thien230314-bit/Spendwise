# Admin A5 Case Workflow & Customer Support — 7/7 DONE, FUNCTIONAL BACKEND LOCKED

Date: 2026-10-19.
Baseline: `7518d58578a8090e83f409d47e1699d706cda14a`.
A1 is LOCKED (4/4). A2 retains four DONE functions and A2.3 POLICY_BLOCKED. A3 is LOCKED (8/8). A4 is FUNCTIONAL LOCKED (6/6 with A4-R1/R2/R3 fixes). This does not approve production operation or complete Backend V1.0 overall.

## HTTP contract

All routes are under `/api/v1/admin/support`. Supply standard bearer access token via Authorization header. Every request requires an audit `reason` (max 1000 characters). Session revocation, transaction admission locking, and canonical operator authorization apply.

| Route | Method | Body / Query | Purpose & Source |
| --- | --- | --- | --- |
| `/api/v1/admin/support/tickets/search` | POST | `A5TicketQuery`: `status?`, `category?`, `priority?`, `linkedUserId?`, `linkedTransactionId?`, `searchText?`, `fromUtc?`, `toUtc?`, `page=1`, `pageSize=20`, `reason` | A5.1: Search real support tickets from users with multi-dimensional filtering, stable pagination, and scope boundary checks. |
| `/api/v1/admin/support/{caseId}/assign` | POST | Route `caseId`, Body `A5AssignTicketRequest`: `reason`, `operatorId` | A5.2: Assign ticket to ordinary admin (self-assignment for ordinary Admin; SuperAdmin can assign across operators). Atomic lock & state transition to `Investigating`. |
| `/api/v1/admin/support/assigned` | POST | Query `page=1`, `pageSize=20`, Body `OperatorDirectoryRequest`: `reason` | A5.2: Read queue of cases assigned to the current operator. SuperAdmin sees all active cases. |
| `/api/v1/admin/support/{caseId}/request-info` | POST | Route `caseId`, Body `A5RequestInfoRequest`: `reason`, `message`, `attachments?` | A5.3: Transition case to `WaitingForUser`, record `CaseHistory`, notify customer, validate up to 5 attachments under 5MB. |
| `/api/v1/admin/support/{caseId}/dossier` | POST | Route `caseId`, Body `OperatorDirectoryRequest`: `reason` | A5.4: Read-only fund/group investigation dossier. Aggregates case subject, linked fund/group overview, disputes, chronological timeline, attachments, and financial notice. |
| `/api/v1/admin/support/{caseId}/escalate` | POST | Route `caseId`, Body `A5EscalateRequest`: `reason`, `escalationDetails` | A5.5: Escalate complex case to SuperAdmin with immutable escalation record, reason, and notification. |
| `/api/v1/admin/support/{caseId}/resolve` | POST | Route `caseId`, Body `A5ResolveRequest`: `reason`, `resolutionMessage`, `isRejected` | A5.6: Resolve or reject case with validated rationale (>= 5 chars), terminal status transition, user notification, and closure locking. No direct refund/money execution. |
| `/api/v1/admin/support/templates` | POST | Body `OperatorDirectoryRequest`: `reason` | A5.7: Standard template catalog for customer responses. Read-only. |
| `/api/v1/admin/support/{caseId}/reply-template` | POST | Route `caseId`, Body `A5ApplyTemplateRequest`: `reason`, `templateKey`, `parameters`, `transitionToWaitingForUser?` | A5.7: Apply approved template with sanitized parameter substitution (prohibiting credentials/tokens/secrets), record case history, and notify user. |

## Acceptance matrix against Admin.md A5

| Function | Status | Implemented / Acceptance |
| --- | --- | --- |
| A5.1 Ticket mới | DONE | Tra cứu ticket thực tế do User gửi; lọc theo New/Open/WaitingForUser, category, priority, thời gian, text search; phân trang và sắp xếp ổn định; phân biệt quyền Admin thường và SuperAdmin. |
| A5.2 Đang xử lý / Phân công | DONE | Phân công và claim ticket với concurrency protection và lock transaction; Admin chỉ được nhận việc cho chính mình, SuperAdmin có quyền điều phối; queue danh sách ca làm việc ổn định. |
| A5.3 Chờ User phản hồi | DONE | Chuyển trạng thái sang `WaitingForUser`; ghi nhận thông điệp vào `CaseHistory`; gửi notification cho User; kiểm soát đính kèm tối đa 5 file, dung lượng <= 5MB, định dạng an toàn. |
| A5.4 Hồ sơ điều tra quỹ | DONE | Tập hợp hồ sơ điều tra quỹ/nhóm (V1 Fund hoặc Group Fund): thông tin đối tượng, trạng thái số dư, lịch sử đóng góp/chi tiêu, các tranh chấp liên quan, timeline sự kiện; chế độ chỉ đọc bất biến, cấm sửa tiền. |
| A5.5 Đề xuất chuyển Super Admin | DONE | Đề xuất chuyển ca phức tạp lên SuperAdmin; ghi nhận lý do và thời điểm escalate; ngăn chặn escalate trùng lặp; phân định rõ ranh giới quyền Admin và SuperAdmin. |
| A5.6 Đã giải quyết | DONE | Đóng ca với trạng thái Resolved hoặc Rejected; yêu cầu thông điệp giải quyết rõ ràng (>= 5 ký tự); gửi email và notification cho User; ngăn ngừa chuyển trạng thái khi đã đóng; không kích hoạt chuyển tiền trái phép. |
| A5.7 Mẫu phản hồi | DONE | Danh mục mẫu phản hồi chuẩn từ hệ thống; điền biến tự động; kiểm soát an toàn nghiêm ngặt ngăn chặn inject token/mật khẩu; cho phép tùy chọn chuyển sang trạng thái chờ phản hồi. |

## Targeted Correctness Fixes (FIX-2, FIX-3, Task 07 Verification)
- `FIX-2 (A5.2)`: Ngăn chặn triệt để Admin thường chiếm đoạt hoặc tự phân công lại ticket đã thuộc về Admin khác (`AssignedAdminId != null && AssignedAdminId != actor`). Việc tái phân công ticket bắt buộc cần thẩm quyền SuperAdmin.
- `FIX-3 (A5.4)`: Giới hạn hồ sơ tranh chấp `FundDossier` chỉ trả về các tranh chấp có cấu trúc ràng buộc với giao dịch của vụ việc (`LinkedTransactionId == ticket.LinkedTransactionId`), ngăn rò rỉ các tranh chấp ngoài phạm vi vụ việc.
- `Task 07 Verification (A5.4 & A5.7)`:
  - Chỉ tính tổng số dư quỹ trên các bút toán sổ cái có trạng thái `LedgerTransactionStatus.Completed`. Bút toán pending/failed/voided không được tính vào số dư thực tế. Ghi rõ cảnh báo nếu tài khoản thủ quỹ chưa được xác minh.
  - Hoàn thiện render email templates `support-info-request` và `support-fund-investigation` với fallback URL hợp lệ, bảo đảm an toàn injection.

## Verification and Safety Gates

- Release build: `backend/SpendWise.slnx` builds with 0 errors.
- Focused A5 tests: `AdminA5Tests` — 12 PASS / 0 FAIL / 0 SKIP (bao gồm kiểm thử xác minh FIX-2, FIX-3, completed ledger filtering và email template injection prevention).
- Regression suite: `AdminA1HttpTests`, `AdminA2HttpTests`, `AdminA3HttpTests`, `AdminA4Tests`, `AdminA5Tests`, `AdminA6Tests`, `V1AdminLifecycleTests` — 127 PASS / 0 FAIL / 0 SKIP.
- User.md v1.3 compliance: `CanSpend = ON` direct spending rule preserved without disruption.
- SQL02: NOT RUN (resource gate / memory constraints preserved per project guidelines).
