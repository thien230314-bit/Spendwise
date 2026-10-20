# Task 17 — đóng khoảng trống Core Backend và bàn giao

Ngày 2026-10-20, nhánh `main`. HEAD đầu vào `6064f932a22f81f80346d2d85284357993b7963e`, workspace sạch; fetch đầu/cuối không phân kỳ với origin/main. Báo cáo này thay thế các trạng thái còn thiếu của báo cáo nghiệm thu trước; không thay lịch sử các lượt kiểm thử cũ.

**CORE BUSINESS LOGIC READY FOR EXTERNAL API CHECK**. A1 giữ LOCKED (4/4); A2.3 **APPROVED / IMPLEMENTED / VERIFIED** theo chính sách hai cấp đã được Product Owner phê duyệt. Đây không phải chứng nhận Production hoặc 100% Final E2E. Chỉ đạo cuối chuyển External API và Full Postman/Newman sang **Antigravity**; Codex dừng tại bàn giao Task 17.

## A. Completed gates

| Gate | Khoảng trống trước đây | Đã sửa / kiểm chứng thực tế | Evidence |
|---|---|---|---|
| A2.3 | Marker POLICY_BLOCKED lỗi thời; thiếu inbound/refund HTTP | Restricted giữ login/refresh/read, chặn outbound; Locked thu hồi session/refresh, chặn login; unlock cần fresh login; PIN, assigned case, ordinary User, audit/notif và unauthorized denial | `CoreAcceptanceHttpTests.Admin_restrict_lock_unlock_HTTP_enforces_sessions_and_preserves_money`; 5 ca `Account_status_HTTP_requires_pin_case_subject_and_approved_status`; regression AdminA2/role/session |
| Restricted inbound/refunds | P2P từ chối mọi recipient không Active | P2P nhận Active hoặc Restricted; không mở quyền sender. Fixed và Free preview/refund 500.000 VND, replay không trả hai lần; số dư/ledger và status được kiểm tra trên SQL02 | `Task17AccountSqlHttpTests`: 3 HTTP/SQL02 PASS |
| ADM-05 | Adapter đóng maintenance luôn unconfigured; thiếu luồng đầy đủ | Adapter thực đối soát wallet/account/net entries, trạng thái processing/pending, entry dương, cân bằng một currency; tính cả posting gốc Reversed. Chỉ cấp evidence trong transaction có exclusive admission. Schedule/reminders/reschedule/activate/emergency/extend/review/close, PIN/audit/permission; giữ financial lock đang chạy và retry admission sau commit | `Task17MaintenanceSqlHttpTests`: 5 HTTP/SQL02 PASS; healthy close thành công; unbalanced/stale balance/mixed currency đều409 không có close evidence/audit; emergency gặp shared lock trả409, không cắt giao dịch |
| Fixed carry-over | Không có settlement nối kỳ; lifetime contribution sai entitlement | Endpoint Owner+PIN settlement; tính phân bổ exact một lần trước refund/removal; consenting carry-over lưu snapshot, không tạo tiền/ledger giả; revision kỳ mới tránh đếm đôi contribution cùng timestamp. New/rejoin =0 entitlement cũ; contribution mới hợp lệ; preview/execute dùng chung đồng dư theo seat | 2 ca `Task17CarryOverSqlHttpTests` qua ba kỳ, nonconsent seat2/3, preview466.667/466.666, fault rollback sau saved ledger, concurrent settle/replay, new member/rejoin; SQL02 HTTP. Newman phát hiện và kiểm chứng thêm timestamp trùng |
| Election edges | Thiếu HTTP tie/no-votes/expiry/locked winner | Timeline thật qua fake clock, frozen electorate, tài chính dừng, Owner/ledger không đổi; Failed đúng RunoffTie/NoVotes/AcceptanceExpired/EligibilityChanged; winner rejection có regression riêng | 4 ca `Task17ElectionSqlHttpTests`; `CoreAcceptanceHttpTests.Election_HTTP_real_timeline_freezes_finance_and_fails_on_rejection_or_locked_voter` |
| ADM-03 | Thiếu full support flow HTTP | Intake → claim → request-info → user reply → resolve/reject; outsider404, User403, Admin không được chiếm case; history/audit/notification, không tạo tiền | 2 ca `Task17SupportBroadcastSqlHttpTests.Support_HTTP_SQL02_intake_assignment_information_reply_and_terminal_decision` |
| ADM-04 | Thiếu publish/cancel/status HTTP | Draft/publish/local persist/outbox/status; wrong PIN/User denied; cancel future broadcast không phát; audit/dedup, không gọi push thật | `Task17SupportBroadcastSqlHttpTests.Broadcast_HTTP_SQL02_publish_cancel_dispatch_outbox_and_permission_are_consistent` |
| Admin authorization | Cần giữ một Admin / năm khu vực và legacy denial | ADM01/02/03 routes, notification/maintenance permissions, disabled SuperAdmin login/session/refresh, không phục hồi escalation/role assignment | AdminA1/A2/A3 HTTP; CoreAcceptance HTTP; Task14RoleSession; V1LegacyRoleBoundary; Task16_4; permission/platform regression |
| Theme/Language | Tài liệu chưa nêu đủ tính bắt buộc, thiếu account-switch proof | Giữ nguyên API PUT preferences / GET me / login profile, `light|dark|system`, `vi-VN|en-US`; SQL02 xác nhận lưu, đổi tài khoản không lẫn, fresh login trả đúng, invalid400 không đổi dữ liệu | 3 ca `Task17PreferencesSqlHttpTests` PASS. **Chỉ Backend VERIFIED; FRONTEND PENDING** |

Review độc lập đã tìm ba vấn đề: net ledger không đối soát account, mixed-currency cân bằng giả, preview lệch settlement1 VND. Cả ba có ca RED trước sửa và PASS sau sửa. Review bổ sung sửa dấu Debit-positive theo LedgerService. Nghi vấn nhiều proposal approved đã được SQL02 bác bỏ bằng unique filtered index hiện có; HTTP duplicate proposal409 được giữ trong test, không thêm cơ chế dư thừa. Admission settlement vẫn kiểm tra current closed period. Không gọi thêm mô hình AI sau lựa chọn tiết kiệm quota của Product Owner.

## B. Testing — actual runs

| Lượt | PASS | FAIL / SKIP cuối | Phạm vi / thời điểm |
|---|---:|---|---|
| `task17-final-regression.trx` |217|0 / 0|Regression chọn lọc User/Admin/finance/election/maintenance/email/OpenAPI; trước sửa ranh giới revision do Newman tìm ra |
| `task17-preferences.trx` |3|0 / 0|HTTP + owned SQL02, ba theme/hai language/account switch/fresh login |
| `task17-affected-final.trx` |16|0 / 0|Sau sửa revision: carry-over2, preferences3, refund10, runner Newman affected folder1 |
| `task17-final-related.trx` |43|0 / 0|Sau final production source: finance13, membership16, activity3, maintenance SQL02 5, account SQL02 3, OpenAPI2, migration upgrade1 |
| `task17-bank-policy.trx` |2|0 / 0|Existing fake-provider/isolated tests: fresh balance/ownership/expiry/exact-reference và pending không cộng tiền + exact settlement once. Không chứng minh provider thật |
| Release solution build cuối |PASS|0 errors / 8 warnings|`dotnet build backend/SpendWise.slnx -c Release --no-restore` |
| EF model/migration |PASS|Không pending changes|Release `has-pending-model-changes`; migration chain upgrade/reapply trên owned SQL02 PASS |

Có **224 test phân biệt PASS** trong union regression217 + affected16 + related43 + bank2; các lượt overlap, không cộng thẳng. Không tái sử dụng PASS lịch sử Task16 làm kết quả mới. New Task17 SQL02 HTTP:20 ca; migration và Newman runner là2 ca SQL02 khác. Các test SQLite/InMemory cũ vẫn là test loại đó, không đổi nhãn thành SQL02. Build song song từng gặp file lock MSB3021/3027; đã chạy lại tuần tự sau testhost và PASS. NU1902/NU1903 của `System.Security.Cryptography.Pkcs 9.0.4` còn tồn tại; không thay dependency ngoài scope.

### Newman đã chạy trước khi Product Owner chuyển phân công

- Collection canonical đã bổ sung folder10, tổng **141 HTTP requests / 227 assertions**. Không mở rộng tiếp sau chỉ đạo cuối.
- Lượt đầy đủ thực tế:141 requests được thực thi, không network failure; **225/227 assertions PASS, 2 FAIL** (carry-over trùng timestamp, inbound fixture dưới minimum). Không che lượt FAIL này.
- Đã sửa ranh giới kỳ bằng `StartedRevision`, sửa amount fixture; chỉ chạy lại folder10 trên owned fixture mới: **36/36 requests, 50/50 assertions PASS**, kèm SQL final-state checks ledger/net account, refund, hold0, election Failed/Owner không đổi, maintenance closed, broadcast Published local.
- Kết quả cuối theo nhóm sau sửa:141 requests/227 assertions có bằng chứng đạt từ full run + affected rerun, gồm7 requests/14 assertions về provider-unavailable. **Không có một full clean rerun sau sửa, không tuyên bố FULL E2E PASS hoặc100%.** Full Core E2E acceptance chuyển Antigravity; current focused Core workflows PASS.
- Lần khởi tạo runner trước đó thiếu dynamic port nên ECONNREFUSED, chưa tới API; đã sửa lấy IServerAddressesFeature và preflight readiness. Không tính lượt này là business PASS.
- Activity approved và election mature trong Newman là synthetic boundary fixtures; targeted fake-clock HTTP tests riêng kiểm tra actual voting/timeline. Collection chưa thay thế mọi tổ hợp nghiệp vụ hoặc UI.

Raw TRX/JSON/JUnit/private environments/pickup ở `%TEMP%/spendwise-task17`, không publish. SQL fixture chỉ nhận instance SQL02, catalog `SpendWise_FinancialS05_Test_<32hex>`, integrated auth, tạo catalog mới + ownership marker, xác minh identity và marker trước cleanup. Kestrel loopback ephemeral; background workers tắt, SePay/Gemini disabled/fake, không dispatch SMTP/push Internet. **Không kết nối, đọc, ghi hoặc migrate SQL01.**

## C. Source và documentation

- Application: P2P recipient guard; `V1LedgerMaintenanceSafety` + DI; maintenance failure code; Fixed close/settlement/period paid/contribution/refund allocation/membership episode counter.
- Domain/schema: proposal `SettledAt`, `StartedRevision`; voter `CarryOverVnd`; membership nullable `RefundedVnd` (legacy null đọc history, episode mới0). Migration `20261020065137_Task17ActivitySettlement` chỉ thêm4 nullable columns; Designer/model snapshot đồng bộ. **Chưa apply production migration**; đây là prerequisite deployment, cần operator authorization. Không tự sửa dữ liệu cũ.
- HTTP: POST `/api/v1/funds/activity-proposals/{id}/settle`, User/Owner/PIN, financial rate limit; idempotent replay. Current Owner phải đồng ý; không tự bổ nhiệm Owner. Voting/ownership/membership/election guards giữ nguyên.
- Tests: Task17 Account/CarryOver/Election/Maintenance/SupportBroadcast/Preferences SQL HTTP, owned fixture + factory SQL option, CoreNewman opt-in runner, OpenAPI route inventory; reuse existing fixtures/helpers.
- Docs: User.md, Admin.md, AGENTS.md (chỉ status lỗi thời được phép), BACKEND-BASELINE.md, product decisions, both acceptance records, report này; Postman generator/collection/README là phần đã hoàn tất trước handoff. Frontend/Mobile không sửa.

## D. Remaining scope / Antigravity nhận tiếp

1. **PROVIDER INTEGRATION NOT YET VERIFIED**: SePay live available balance/debit/link/deposit/capability contract; Gemini; SMTP/Push delivery. Không dùng fake/Test Mode thành chứng nhận API thật. Giữ logic nạp/rút đã duyệt: đủ available balance mới cho chọn, backend kiểm tra lại, không nạp phần, chỉ cộng ví khi xác nhận toàn bộ đúng amount/reference. Retail payout giữ giới hạn đã ghi trong baseline.
2. **FULL CORE E2E ACCEPTANCE PENDING — Antigravity**: kiểm tra coverage canonical và chạy Full Postman/Newman riêng trên owned SQL02, sau đó chỉ rerun nhóm lỗi. Tiếp nhận full141/227 fail2 và affected36/50 pass như lịch sử thực tế, không nhận như clean full PASS.
3. **FRONTEND UI NOT YET VERIFIED / FRONTEND PENDING**: web có ThemeProvider/I18nContext; Mobile `useTheme` đọc OS. Phải hoàn thiện/kiểm chứng Light/Dark/System, VI/EN, account switching/cache, đa thiết bị và bản dịch UI. Backend preferences đã verified; Postman không xác nhận giao diện.
4. Production migration, deployment/reconciliation, dependency advisory remediation chưa làm. Maintenance fail-closed nếu dữ liệu legacy không đối soát; không sửa tiền để vượt check.

Không còn Core blocker tái hiện được trong các gate Task17 sau final targeted tests. Không chạy audit toàn hệ thống, source ZIP, provider live, credential reset hay task mới.

## E. Git và final handoff

Source/docs commit và push được ghi bằng SHA thực tế trong bản handoff cuối. Stage explicit scoped paths, review staged diff, scan toàn bộ staged blobs; fetch main trước push, không force. Không publish config/secrets/DB/log/report raw/pickup/token. Sau bàn giao, Codex dừng Task17.
