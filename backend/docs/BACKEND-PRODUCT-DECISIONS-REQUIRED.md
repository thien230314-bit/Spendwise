# SpendWise V1.0 — Product Decisions and Status (Task 16.1)

Updated 2026-10-20. Product Owner Task 16.1 officially approved and closed the 4 remaining core business logic decisions. All 4 decisions have been implemented in production code, integrated into routing/services, and verified by targeted tests, full test suites, and Newman E2E.

## 1. Approved & Implemented Decisions (Task 16.1)

### Decision 1: Fixed Fund Dispute Resolution
- **Business Behavior:** When a member reports a large expense hold (> 200,000 VND), the expense immediately enters `Suspended` status and creates a SupportCase with `Category = "V1FundDispute"` and `LinkedTransactionId = expense.Id`.
- **Admin Resolution Workflow:** Admin resolves the dispute via `POST /api/v1/admin/support/disputes/{requestId}/resolve` (`AdminA5Service.ResolveDisputeRequestAsync`):
  - *Valid Report (`!IsRejected`):* Cancels the expense (`Cancelled`), releases the held funds back to the fund's available balance, closes the SupportCase (`Resolved`), logs an audit event, and notifies members.
  - *Invalid Report (`IsRejected`):* Unsuspends the expense back to `Pending24h` without resetting the 24-hour clock, does not auto-payout, closes the SupportCase (`Rejected`), logs an audit event, and notifies members.

### Decision 2: Admin Account Restrictions
- **Level 1 — Financial Restriction (`UserStatus.Restricted`):**
  - Outbound debits are blocked (P2P transfer, wallet transfer, manual expense, multi-wallet expense, fund contributions, fund requests).
  - Permitted operations: Login, session refresh, viewing balances/history/profile, submitting SupportCases, receiving incoming P2P transfers, receiving refunds.
- **Level 2 — Account Lock (`UserStatus.Locked`):**
  - Login denied (403 `account_unavailable`).
  - Active sessions revoked immediately upon lock.
  - Refresh token rotation denied.
  - Support case channel retained for appeal.
  - Admin can unlock back to `Active` (which revokes credentials to force a fresh login).

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
