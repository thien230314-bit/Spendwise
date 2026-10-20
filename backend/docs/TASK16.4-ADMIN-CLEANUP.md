# TASK 16.4 — ADMIN ARCHITECTURE CLEANUP & FINAL TARGETED VERIFICATION

## 1. Context & Objectives
In SpendWise V1.0, the system architecture operates strictly with two roles:
- `User`
- `Admin`

`SuperAdmin` is completely excluded from the active V1.0 operational model. However, Admins retain full, unblocked authority across all 5 operational admin modules (ADM-01 through ADM-05) per `Admin.md`.

This task executed:
1. Systematic removal of residual SuperAdmin dependencies and checks in active administrative workflows (`V1CaseWorkflow`, `V1CaseAssignment`, `AdminA1Service`, `AdminA2Relations`, `AdminA3Reports`, `AdminA5Service`, `PlatformPermissionService`, `V1BroadcastService`).
2. Resolution of the unassigned ticket visibility bug in `POST /api/v1/admin/support/search` (`V1CaseAssignment.QueueAsync`), enabling ordinary Admins to discover and claim unassigned tickets (`c.AssignedAdminId == actor || c.AssignedAdminId == null`).
3. Explicit disabling of legacy SuperAdmin escalation actions (`escalate`, `claim`, `guidance`) with `403 Forbidden` (`escalation_disabled`).
4. Rejection of `SuperAdmin` as a target role in operational broadcasts (`V1BroadcastService`).
5. Targeted verification confirming Admin capabilities, ticket assignment safety, and concurrency bounds.

---

## 2. Changes Summary

### A. Core Administrative Workflows
- **`V1CaseWorkflow.cs`**:
  - Replaced legacy escalation switch case with an immediate check rejecting `escalate`, `claim`, and `guidance` with `403 Forbidden` (`escalation_disabled`).
  - Removed SuperAdmin bypass condition in `WithCaseAsync`; enforced active case assignment scope.
  - Hardcoded `ActorRole = "Admin"` in case history audit logs.
- **`V1CaseAssignment.cs`**:
  - In `QueueAsync`, fixed queue query predicate from `(supervisor || c.AssignedAdminId == actor)` to `(c.AssignedAdminId == actor || c.AssignedAdminId == null)`. Admins can now view unassigned tickets in addition to their own claimed tickets.
- **`AdminA5Service.cs`**:
  - Cleaned legacy `supervisor` flags.
  - In ticket assignment, updated error message from `"Case is already assigned to another Admin. Reassignment requires SuperAdmin."` to `"Case is already assigned to another Admin."`
  - Normalized `ActorRole` snapshot to `"Admin"`.
- **`AdminA1Service.cs`, `AdminA2Relations.cs`, `AdminA3Reports.cs`, `PlatformPermissionService.cs`**:
  - Cleaned dead `supervisor` / `SuperAdmin` conditions, ensuring ticket scoping relies strictly on active Admin assignment.
- **`V1BroadcastService.cs`**:
  - Restricted operational broadcast target roles strictly to `User` and `Admin`. Rejected `SuperAdmin`.

### B. Automated Verification
- Created `backend/tests/SpendWise.Tests/Task16_4_AdminCleanupTests.cs`:
  1. `Admin_Can_View_Unassigned_Tickets_In_Support_Queue`: Verified unassigned tickets are visible alongside self-assigned tickets; foreign assigned tickets remain hidden.
  2. `Admin_Can_Self_Assign_Unassigned_Ticket_Through_AdminA5Service`: Verified Admin can take ownership of unassigned tickets and history logs `ActorRole = "Admin"`.
  3. `Admin_Stealing_Another_Admins_Ticket_Is_Denied`: Verified anti-theft protection throws `403 Forbidden` (`assignment_denied`).
  4. `V1CaseWorkflow_Escalate_Or_Claim_Is_Disabled_In_V1`: Verified escalation actions are disabled with `escalation_disabled`.
  5. `V1Broadcast_Rejects_SuperAdmin_TargetRole`: Verified broadcast API rejects `SuperAdmin` targeting with `broadcast_contract_required`.

---

## 3. Verification Results
- **Full Non-SQL Test Suite**: 1,120 / 1,120 tests PASS (100% green, 0 failures).
- **Security & DB Isolation**: SQL01 (`DESKTOP-B5KA3LT\SQLEXPRESS01\SpendWiseDb`) was never accessed or targeted.
