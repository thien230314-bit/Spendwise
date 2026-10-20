# Final User & Admin Hardening — 2026-10-20

Baseline `e3f28b4991d481e7c31994c033699622a35149a3`, main, clean, origin/main0/0. Current Product Owner instruction supersedes Task17 handoff: Codex completes this hardening; no agent transfer, Full Newman, provider live, SQL01 or Frontend/Mobile edits. Matrix được lập trước implementation và cập nhật kết quả cuối bên dưới; bằng chứng Task17 kế thừa được tách khỏi lượt chạy mới.

## A. FEATURE → API → IMPLEMENTATION → TEST → EVIDENCE → GAP

| User group | API / implementation | Existing direct test / evidence | Initial gap / classification |
|---|---|---|---|
| Auth + Settings credentials | `/api/auth/*`, `/api/security-pin/*`; identity handlers | HttpIntegrationFlowTests.Authentication_supports_rotation_logout_and_immediate_revocation; RegistrationEmailVerificationTests, PasswordResetFlowTests, PinRecoveryDeliveryHttpTests | PASS current: rotation/logout; PasswordResetFlowTests.Http_contract_accepts_identifier_otp_and_new_password; PinRecoveryDeliveryHttpTests.Admin_forgot_PIN_queues_real_link_then_single_use_reset_preserves_password_role_and_revokes_sessions |
| Sessions | `/api/auth/sessions`, logout/refresh; SessionManagement handlers | SessionManagementHttpTests; Task17 lock/unlock HTTP PASS | PASS current: SessionManagementHttpTests5; reuse Task17 lock/unlock evidence |
| Profile ownership | `/api/users/me`; current-user query | GET self in auth/session and preferences HTTP | Input account id ignored; no public arbitrary profile mutation implied |
| Preferences | PUT `/api/users/me/preferences`; UpdatePreferences handler | Task17PreferencesSqlHttpTests3 PASS | PASS current SQL02 preferences3; FRONTEND UI PENDING |
| Wallet | `/api/wallets`, `/api/transfers/internal`; wallet/ledger handlers | HttpIntegrationFlowTests; PersonalWalletLimitTests; WalletDefaultInvariantTests | PASS mới: Wallet_HTTP_SQL02_three_wallet_limit_default_ownership_and_internal_retry |
| Transactions | `/api/transactions`, PATCH `/api/transactions/{id}`, categories/rules | RelationalFinancialIntegrationTests; HttpIntegrationFlowTests | PASS mới: Transactions_HTTP_SQL02_correction_preserves_history_and_denies_foreign_mutation |
| P2P | `/api/transfers/p2p`; P2PTransfer handler | Task17AccountSqlHttpTests PASS; P2PTransferTwoUserIntegrationTests | VERIFIED inbound/retry; Locked/session evidence inherited |
| Budgets | `/api/budgets`; BudgetReader + handlers | OperationsPlanningCompletionTests; TargetedCalendarAndRolloverTests | PASS mới: Budgets_HTTP_SQL02_crud_progress_and_foreign_owner_are_consistent |
| Goals | `/api/goals/*`; GoalMovementExecutor | GoalSqlServerConcurrencyTests; FinancialRulesTests | PASS mới: Goals_HTTP_SQL02_contribute_withdraw_replay_and_cross_account_denial; existing SQL concurrency được rerun targeted |
| Recurring | `/api/recurring-transactions/*`; RecordRecurringOccurrence + schedule | FinancialIntegrityPhaseTests | PASS mới: Recurring_HTTP_SQL02_due_record_retry_pause_resume_and_ownership |
| Friends | `/api/friends/*`; handlers | V1FriendLimitTests; prior collection invitation flow | PASS mới: Friends_HTTP_SQL02_allows_hundredth_and_denies_101st_without_accepting_request |
| Fixed | `/api/v1/funds/*`; V1FundService | Task17 carry-over2, dispute HTTP+SQL02 concurrency, finance HTTP | VERIFIED targeted; reuse without full rerun |
| Free | Same fund APIs, request/refund calculator | Task17 Fixed/Free refund and prior rejoin HTTP | CONFIRMED BUG → FIXED/VERIFIED: cùng timestamp refund/withdrawal cũ và stale pending request mới/legacy;4 HTTP SQL02 ca |
| Elections | `/api/v1/funds/elections/*`; frozen policy | Task17ElectionSqlHttpTests4 + Core election HTTP | VERIFIED targeted; no timeline policy changes |
| Support | `/api/cases/support/*`; case handlers | Task17 Support HTTP SQL02 resolve/reject2, CaseMultipartHttpTests | VERIFIED core; attachment contract existing |
| Notifications | `/api/notifications/*`; notification handlers | MobilePushHttpTests; outbox tests | PASS mới: Notifications_HTTP_SQL02_reads_and_mutations_never_touch_foreign_notifications; raw recovery delivery không được mở endpoint mới |
| Banking invariants | bank connection/top-up contract handlers | SePayTestModeBankingTests isolated exact settlement/no early credit | PROVIDER DEPENDENCY: real balance/debit/payout remains unverified |

## B. Admin inventory

| Area | Workflow / implementation | Direct positive + negative evidence | Initial classification |
|---|---|---|---|
| ADM01 | overview/tasks/sources/alerts/activity; AdminA1Service | AdminA1HttpTests4 PASS Task17; User/legacy denial | VERIFIED inherited; A1 LOCKED |
| ADM02 | search + assigned-case views/status/revoke; AdminA2/status handler | AdminA2HttpTests; CoreAcceptance restriction6; session tests | VERIFIED inherited; no balance editing |
| ADM03 | intake/claim/info/reply/resolve/reject; V1CaseWorkflow/AdminA5 | Task17 Support2 + dispute HTTP; SQL rollback/resolve-cancel | VERIFIED inherited; no case stealing/SuperAdmin |
| ADM04 | draft/publish/cancel/status/outbox; V1BroadcastService | Task17 Broadcast HTTP SQL02 PASS, admin notification routes | VERIFIED core; SMTP/Push live NOT RUN |
| ADM05 | schedule/activate/emergency/extend/review/close; maintenance service/safety | Task17MaintenanceSqlHttpTests5 PASS; exclusive admission+corrupt ledger denial | VERIFIED core; deployment reconciliation pending |

## C. Finance & Security — lỗi tái hiện và sửa

Ba lỗi cùng gốc thiếu định danh membership episode:

1. Free Fund leave/rejoin cùng timestamp: refund500.000 của episode cũ bị trừ vào khoản góp100.000 mới; preview thực tế0 thay vì100.000. Test FAIL trước, PASS sau khi dùng `membership.RefundedVnd` (episode mới0), legacy null giữ fallback lịch sử.
2. Withdrawal100.000 đã Completed của episode cũ cũng bị tính sang episode mới khi timestamp trùng. Thêm `V1FundRequest.RequestedByMembershipId`, ghi từ membership đang active dưới User/fund admission lock ở cả3 nhánh tạo request. Refund chỉ trừ withdrawal đúng episode, legacy null giữ fallback cũ. Ca oldWithdrawal=true FAIL trước/PASS sau.
3. Pending Withdrawal của episode cũ có thể được Owner duyệt sau leave/rejoin và debit episode mới: test thực tế nhận200 thay vì409. Review nay chặn episode gốc đã kết thúc, không tạo posting/sửa số dư/state. Owner vẫn có thể reject request cũ; không tự hủy hoặc chuyển tiền. Legacy request thiếu membership ID với lịch sử membership đã kết thúc cũng bị chặn409 để tránh gán nhầm; ca legacyRequest=true FAIL trước guard bổ sung và PASS sau.

`FinalUserAdminSqlHttpTests` có11 ca HTTP/SQL02 PASS qua các lượt targeted:4 episode cases, Budget CRUD/progress/IDOR, Goal contribution/withdrawal retry/IDOR, Recurring due/materialization/retry/pause/resume/IDOR, Notification read/read-all/delete ownership, Friends99→100/101st denial, Wallet3-cap/default/foreign ownership/internal retry, Transaction correction/foreign denial/reversal/history. Balance và ledger counts được kiểm tra bằng SQL, không dùng fake provider như chứng nhận ngân hàng.

Correction test ban đầu dùng evidence đã liên kết nên400 đúng contract; đã sửa dùng correction evidence list hợp lệ. Friends fixture ban đầu gửi Identifier thay Contact, Recurring fixture dùng past date thay future và expected201 thay200; sửa fixture và fake-clock advance, không sửa source để chiều test.

## D. Dependency advisory

Chain trước: EF.SqlServer10.0.8 → Microsoft.Data.SqlClient6.1.1 → **Pkcs9.0.4**. [Microsoft advisory GHSA-2fmv-rv2c-59x7](https://github.com/advisories/GHSA-2fmv-rv2c-59x7) mô tả EnvelopedCms decode CPU DoS trên Windows; [GHSA-846f-f764-76mx](https://github.com/advisories/GHSA-846f-f764-76mx) mô tả SignedCms signature CPU DoS đa nền tảng. Cả hai liệt kê bản vá9.0.21. [NuGet9.0.21](https://www.nuget.org/packages/System.Security.Cryptography.Pkcs/9.0.21) hỗ trợ net8/net9 assets, tương thích consumer net10.

Đã thêm một PackageReference **9.0.21** ở Infrastructure, override transitive cùng major; không nâng EF/SqlClient hoặc tắt audit. `dotnet nuget why` sau restore xác nhận cả direct và SqlClient đều resolve9.0.21. Không có call SignedCms/EnvelopedCms trực tiếp trong Application/Infrastructure, nhưng không suy diễn transitive risk là0. Native audit `dotnet list backend/SpendWise.slnx package --vulnerable --include-transitive --no-restore` và audit riêng AdminPasswordRecovery đều **không có vulnerable package** ở nguồn NuGet hiện tại. Final build không còn NU1902/NU1903; EF CLI10.0.7 vẫn báo cũ hơn runtime10.0.8 khi dùng tooling, không phải build warning hoặc lỗi model.

## E. Kết quả kiểm thử, tài liệu và giới hạn

| Evidence ngoài Git | PASS | FAIL cuối | SKIP | Phạm vi thực tế |
|---|---:|---:|---:|---|
| hardening-after.trx |39|0|0|Sau patch dependency/refund: HTTP/SQL02 gap7, preferences3, SQL Goal same-key concurrency1, Auth rotation1, Sessions HTTP5, banking isolated12, refund regression10 |
| hardening-final.trx |34|0|0|Sau source/migration cuối: mới10 SQL02 HTTP; FundRequest4; Refund10; Task17 account3 + carry-over2; Dispute HTTP2; password reset HTTP1; PIN reset HTTP1; migration upgrade/reapply SQL02 1 |
| hardening-legacy-final.trx |8|0|0|Final source guard: stale request mới/legacy2, FundRequest4, Dispute HTTP2; chỉ rerun nhóm bị ảnh hưởng |
| Final Release solution build |PASS|0 errors|—|0 warnings; `dotnet build backend/SpendWise.slnx -c Release --no-restore` |
| EF model check |PASS|0 pending|—|`has-pending-model-changes --configuration Release --no-build`; additive migration verified bằng owned SQL02 upgrade/reapply |

Các lượt39/34/8 overlap, không cộng thẳng số test. Raw RED evidence vẫn giữ: hardening-before (3 FAIL gồm2 fixture), hardening-episode-before (withdrawal attribution FAIL và evidence fixture FAIL), hardening-membership-before (stale request FAIL), hardening-legacy-before (legacy stale request FAIL). Tất cả lỗi có source/fixture fix và targeted PASS sau đó; không xóa test/skip/suppression để lấy PASS. Build/tests dùng DB fixtures độc lập, Testing pickup, fake/isolated banking contracts; không email Internet.

Bảng A/B đối chiếu source/controllers với test có sẵn trước implementation; các gap quan trọng có HTTP test mới thay vì viết lại unit tests trùng. **AdminA1/A2/A3, Task17 support/broadcast/maintenance/election và locked-session PASS trong Task17 là bằng chứng kế thừa, NOT RUN lại lượt này** vì source tương ứng không đổi; không gắn nhãn fresh PASS. Admin dispute positive/negative được rerun do request logic đổi. Full authorization matrix hoặc mọi tổ hợp input không được tuyên bố100%.

SQL safety: chỉ `DESKTOP-B5KA3LT\SQLEXPRESS02`, randomized allowlisted `SpendWise_FinancialS05_Test_<32hex>` / Goal test catalogs, integrated authentication. Helper kiểm tra effective connection, SERVERPROPERTY identity, ownership marker và cleanup chỉ catalog do fixture tạo; app config không được chọn DB. **SQL01 không được kết nối/đọc/ghi/migrate**. Schema source chỉ thêm một nullable GUID bằng `20261020074711_TrackFundRequestMembership`; không update/drop dữ liệu trong Up, chưa apply Production.

User.md/Admin.md đồng bộ episode guards và giới hạn; AGENTS.md chỉ đổi current status/evidence, giữ safety rules. BACKEND-BASELINE/product decision logs/acceptance đồng bộ; Task17 report giữ nguyên lịch sử và đánh dấu được supersede. Không route/DTO HTTP thay đổi nên không chỉnh OpenAPI/Postman; Money Request vẫn ngoài User Mobile, không thành gate bắt buộc.

- **PROVIDER DEPENDENCY / NOT RUN:** SePay thật balance/debit/link/deposit/payout capability, Gemini, SMTP/Push. Contract banking12 PASS chỉ chứng minh kiểm tra fresh balance, không nạp phần/credit sớm, exact settlement/retry bằng isolated fixture. Không báo provider payout thành công khi chưa xác nhận, không thêm fake balance endpoint.
- **FRONTEND UI PENDING:** Light/Dark/System và VI/EN mandatory; Backend preferences3 PASS account isolation/defaults/invalid inputs/fresh login/persistence. UI/cross-device display/cache/bank-option disabling chưa kiểm thử hoặc sửa.
- **PRODUCTION DEPLOYMENT PENDING:** additive migration cần operator authorization. Legacy null request rows còn dùng fallback timestamp, không tự gán episode/backfill hoặc sửa tiền thật; cần đối soát dữ liệu legacy trước deployment nếu có ambiguity.
- **Full Newman/Full system suites NOT RUN:** để dành giai đoạn sau provider theo Product Owner; không gọi thêm agent/model, không chuyển Antigravity, không audit/ZIP thêm hoặc tự bắt đầu task mới.

## F. Git

Before `e3f28b4991d481e7c31994c033699622a35149a3`, main sạch, origin/main0/0. Scoped changes: FundRequests/FundRefunds, V1FundRequest entity, one migration + Designer/model snapshot, Infrastructure.csproj, FinalUserAdminSqlHttpTests và các docs nêu trên. Commit/push SHA thực tế được ghi sau publish; explicit staging, full staged-blob secret scan và fetch trước push, không force.

## G. Kết luận

**CORE USER & ADMIN READY FOR EXTERNAL API VERIFICATION**.

Không còn confirmed Core bug chưa sửa trong các gate đã đối chiếu/kiểm thử. Đây là targeted Core readiness, không chứng nhận provider, UI, Production deployment hoặc Full E2E. Codex kết thúc lượt hardening này sau commit/push, không chuyển agent hoặc bắt đầu task tiếp.
