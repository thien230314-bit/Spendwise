# SpendWise V1.0 — Product Decisions and Status (Task 16.1)

Updated 2026-10-20, Task 17. The four Product Owner decisions remain approved. A2.3 is APPROVED / IMPLEMENTED / VERIFIED; its old POLICY_BLOCKED marker is retired under explicit Task 17 authorization. Core status: **CORE BUSINESS LOGIC READY FOR EXTERNAL API CHECK**. Evidence: `CODEX-TASK17-CORE-CLOSURE.md`; prior counts/gaps are historical. External checks and full E2E are still required.

Task 17 implements approved Fixed settlement with stable-seat integer remainder allocation shared by preview and execution, persisted consenting carry-over, refund-before-removal for nonconsent, zero old entitlement for a new membership episode, and current-period proposal admission with the existing single-active-proposal index. Maintenance reopening now performs real ledger/account/wallet checks under exclusive admission. No new refund policy or automatic Owner assignment is introduced.

## 1. Approved & Implemented Decisions (Task 16.1)

### Decision 1: Fixed Fund Dispute Resolution
- **Business Behavior:** When a member reports a large expense hold (> 200,000 VND), the expense immediately enters `Suspended` status and creates a SupportCase with `Category = "V1FundDispute"` and `LinkedTransactionId = expense.Id`.
- **Admin Resolution Workflow:** Admin resolves the dispute via `POST /api/v1/admin/support/disputes/{requestId}/resolve` (`AdminA5Service.ResolveDisputeRequestAsync`):
  - *Valid Report (`!IsRejected`):* Cancels the expense (`Cancelled`), releases the held funds back to the fund's available balance, closes the SupportCase (`Resolved`), logs an audit event, and notifies members.
  - *Invalid Report (`IsRejected`):* Unsuspends the expense back to `Pending24h` without resetting the 24-hour clock, does not auto-payout, closes the SupportCase (`Rejected`), logs an audit event, and notifies members.

### Decision 2: Admin Account Restrictions
- Canonical command: PUT `/api/v1/admin/support/{caseId}/users/{userId}/status`, with Status, Reason and SecurityPin. Active assigned case, ordinary User target and Admin PIN are required; account status, audit, notification and lock-time credential revocation share one transaction.
- **Level 1 — Financial Restriction (`UserStatus.Restricted`):**
  - Outbound debits are blocked (P2P transfer, wallet transfer, manual expense, multi-wallet expense, fund contributions, fund requests).
  - Permitted operations: Login, session refresh, viewing balances/history/profile, submitting SupportCases, receiving incoming P2P transfers, receiving refunds.
- **Level 2 — Account Lock (`UserStatus.Locked`):**
  - Login denied (403 `account_unavailable`).
  - Active sessions revoked immediately upon lock.
  - Refresh token rotation denied.
  - Support case channel retained for appeal.
  - Admin can unlock back to `Active`; credentials revoked during locking remain revoked, so unlocking requires a fresh login.

### Decision 3: Fixed & Free Fund Membership Entitlements & Rejoin
- **Episode Scope:** When a member leaves a fund, receives their settlement/refund, and later rejoins, a fresh membership episode (`m.Id`) is established with `JoinedAt = now`.
- **Zero Historical Carryover:** The member's entitlement without new contributions in the new episode is exactly 0 VND.
- **Formulas:**
  - *Free Fund:* Net equity calculation scopes contributions to `c.MembershipId == m.Id`, and withdrawals/prior refunds to `CreatedAt >= m.JoinedAt`. If `net <= 0`, refund preview and leave/kick refund return 0 VND (`NoContribution`).
  - *Fixed Fund:* Contributions already scope to `c.MembershipId == m.Id`; prior refunds scope to `r.CreatedAt >= m.JoinedAt`.
  - Invariant: Preview refund == Execution refund in the same snapshot.

### Decision 4: Owner Election Governance & Timeline
- **Default Policy Timeline:** Initialized in `V1ElectionPolicy.cs`:
  - Registration: 24h (86,400s)
  - Round 1 Nomination: 48h (172,800s)
  - Runoff: 24h (86,400s)
  - Round 2 Confirmation: 24h (86,400s)
  - Acceptance: 24h (86,400s)
  - `FreezeMembership = true`
  - `AllowContributions = false`
  - Outcomes = `"Fail"`
- **Winner Rejection:** Elected winner can reject ownership via `POST /api/v1/funds/elections/{id}/reject` (`RejectElectedOwnershipAsync`), transitioning the election to `Failed` with `FailureReason = "WinnerRejected"`. Nominal owner remains in place, and members can voluntarily leave and receive refunds.
- **Account Status Guard:** Locked voters (`user.Status != UserStatus.Active`) cannot cast ballots. Locked winners cannot accept ownership. Electorate denominator is frozen at the snapshot count.
- **Election Pause:** Financial operations (fund requests, contributions) and member invitations are paused during an active election.

---

## 2. DEFERRED / LIVE NOT VERIFIED

The following external integrations remain intentionally deferred / unverified in V1.0 per Product Owner baseline:
- Real Gemini API live connectivity (offline fallback: 503 `AI_PROVIDER_UNAVAILABLE`).
- Real SePay bank webhooks and live external bank transfers (offline fallback: 404 outside Development).
- Real live external SMTP delivery and push notification delivery (offline testing uses Pickup mode).
- Upstream package advisories for `System.Security.Cryptography.Pkcs 9.0.4` remain documented.

## Task 17 — chỉ đạo bổ sung Product Owner

Theme Light/Dark/System, VI/EN bắt buộc; Backend lưu theo tài khoản, kiểm chứng HTTP/SQL02 đổi tài khoản và fresh login; Frontend/Mobile UI PENDING. Cho phép một lượt canonical Core Newman sau targeted PASS và SQL02 isolation; khi lỗi chỉ chạy lại nhóm bị ảnh hưởng. Không gọi thêm mô hình AI review. Phân loại Core E2E, provider và UI riêng. Giữ nguyên nạp/rút đã duyệt: đủ available balance mới cho chọn, backend kiểm tra lại, không nạp phần, chỉ cộng ví sau xác nhận toàn bộ; SePay live balance/debit phải xác minh riêng, không tuyên bố từ fake/Test Mode.

## Final User/Admin Hardening — 2026-10-20

Chỉ đạo mới yêu cầu Codex tiếp tục; tạm thay phân công chuyển Antigravity trong Task17. Không đổi bốn quyết định đã duyệt. Sửa triển khai Decision3: Free refund counter theo episode, withdrawal/request mới lưu membership ID; không dùng thời gian làm định danh episode và không duyệt request từ episode đã kết thúc. Legacy completed rows dùng fallback lịch sử; pending request thiếu ID với membership đã rời bị chặn duyệt thay vì đoán episode. Không backfill tiền/ownership tự động. Thêm một nullable column bằng migration nguồn, chưa apply Production. Dependency Pkcs9.0.4 từ SqlClient6.1.1 được override9.0.21 (bản vá cùng major). Evidence/giới hạn trong `CODEX-USER-ADMIN-FINAL-HARDENING.md`. Không Full Newman/provider live/UI trong lượt này.
