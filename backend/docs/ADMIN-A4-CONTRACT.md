# Admin A4 transaction & wallet investigation — 6/6 DONE, FUNCTIONAL BACKEND LOCKED

Date: 2026-10-19. Final-closure baseline: `4d82e54f9319d3faea0d22145e9bc83d5f5febd6`.
A1 remains LOCKED (4/4). A2 retains four DONE functions and A2.3 POLICY_BLOCKED. A3 remains LOCKED (8/8). This does not approve production operation or complete Backend V1.0.

## HTTP contract

All routes are POST below `/api/v1/admin/support/{caseId}/transactions`. Supply the normal access token privately, never in the body or URL. Every request requires a meaningful `reason` (maximum 1000 characters). Canonical Admin needs the assigned active support case; SuperAdmin needs an assigned case or an escalated case claimed by that actor. Legacy SystemAdmin is not implicitly promoted. A current, verified, active operator with an unrevoked session is required. The subject must remain an ordinary User.

| Route | Parameters / Body | Purpose & Source |
| --- | --- | --- |
| `search` | `A4Query`: `type?`, `status?`, `fromUtc?`, `toUtc?`, `page=1`, `pageSize=20`, `reason` | A4.1: Scoped list of financial transactions for case subject. Supports filtering by business type and status. Stable pagination. |
| `lookup?code={code}` | Query `code`, Body `OperatorDirectoryRequest`: `reason` | A4.2: Lookup transaction by transaction ID, ledger ID, related business ID, ledger reference, top-up reference or bank reference. Cross-user IDOR strictly denied. PII masked. |
| `wallets` | Body `OperatorDirectoryRequest`: `reason` | A4.3: User SpendWise wallets (1 Main + max 2 secondary) and linked bank connections. Read-only balance inspection; masked account numbers; balance mutation prohibited. |
| `pending-failed` | `A4Query`: `status?`, `page=1`, `pageSize=20`, `reason` | A4.4: Focused investigation feed for Pending, Failed, Cancelled, Processing transactions and provider webhook retries/failure codes. |
| `ledger/{ledgerTransactionId}` | Route `ledgerTransactionId`, Body `OperatorDirectoryRequest`: `reason` | A4.5: Read-only ledger details, debit/credit entries, balance evaluation (BALANCED/UNBALANCED/EMPTY) and original business binding. |
| `{transactionId}/trace` | Route `transactionId`, Body `OperatorDirectoryRequest`: `reason` | A4.6: End-to-end lifecycle trace (RequestInitiation -> AuthorizationAndSecurity -> BusinessProcessing -> ProviderWebhook -> LedgerOutcome). Telemetry not recorded is marked NOT_RECORDED. Tokens/PIN/secrets omitted. |

## Acceptance matrix against Admin.md A4

| Function | Status | Implemented / Acceptance |
| --- | --- | --- |
| A4.1 Tất cả giao dịch | DONE | Danh sách giao dịch từ dữ liệu Backend thực của case subject; hỗ trợ lọc theo loại, trạng thái, thời gian; pagination an toàn; phân biệt business type và ledger posting; không nhân đôi giao dịch. |
| A4.2 Tra cứu mã giao dịch | DONE | Tra cứu qua FinancialTransactionId, LedgerTransactionId, RelatedBusinessId, LedgerReference, TopUpReference, BankTransaction. Chống IDOR và cross-user access; che PII/thông tin nhạy cảm. |
| A4.3 Ví người dùng | DONE | Đọc ví SpendWise thực tế (Main + tối đa 2 ví phụ); trạng thái, số dư khả dụng; đọc liên kết ngân hàng và che số tài khoản; tuyệt đối không cho phép sửa số dư hay đổi trạng thái ví từ Admin A4. |
| A4.4 Giao dịch chờ/lỗi | DONE | Phân biệt Pending, Processing, Failed, Cancelled; tích hợp lịch sử BankIntegrationEvent duplicate/retry và TransactionCorrections; không tự tiện gán Pending là Failed; không cấp quyền retry tiền khi chưa có contract. |
| A4.5 Chi tiết sổ cái | DONE | Đọc LedgerTransaction, LedgerEntries, kiểm tra tính cân bằng đa tiền tệ độc lập theo từng currency, amount dương, khớp đơn vị tiền LedgerAccount, liên kết nghiệp vụ gốc với đối chiếu amount/user/status thực sự; chỉ đọc, không sửa sổ. |
| A4.6 Truy vết giao dịch | DONE | Truy vết request -> auth/security -> processing -> provider/webhook -> ledger outcome; ghi nhận NOT_RECORDED khi thiếu telemetry; bảo mật tuyệt đối, không lộ PIN, OTP, password hash, secret token. |

## Targeted Correctness Fixes (A4-R1, A4-R2, A4-R3)
- `A4-R1`: Kiểm tra cân bằng đa tiền tệ độc lập theo nhóm Currency, cấm cộng gộp giữa các đơn vị tiền khác nhau (phát hiện `MULTICURRENCY_MISMATCH`), kiểm tra amount dương và đơn vị tiền khớp với LedgerAccount.
- `A4-R2`: Xác minh nghiệp vụ gốc dựa trên việc đối chiếu thực sự số tiền, trạng thái nghiệp vụ, quyền sở hữu User, gắn cờ `BUSINESS_BINDING_MISMATCH` khi không khớp thay vì chỉ kiểm tra ID tồn tại.
- `A4-R3`: Bổ sung thông tin nguồn nghiệp vụ `RelatedBusinessSource` trong danh sách giao dịch `TransactionsAsync`.

## Verification and Safety Gates

- Release build: `backend/SpendWise.slnx` builds with 0 errors.
- Focused A4 tests: `AdminA4Tests` — 13 PASS / 0 FAIL / 0 SKIP (kèm các test case kiểm tra A4-R1, A4-R2, A4-R3).
- Regression suite: `AdminA1HttpTests`, `AdminA2HttpTests`, `AdminA3HttpTests`, `AdminA4Tests` — PASS.
- SQL02: NOT RUN (resource gate / memory constraints preserved per project guidelines).
