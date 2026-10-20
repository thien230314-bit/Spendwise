# SPENDWISE — USER MOBILE PRODUCT SPECIFICATION

> CURRENT STATUS (2026-10-20): Backend V1.0 remains PARTIAL; A1 LOCKED and A2.3 PARTIAL / POLICY_BLOCKED are retained. Current, directly executed acceptance evidence is in `backend/docs/CODEX-FINAL-CORE-ACCEPTANCE.md`. Earlier task test counts are historical, not current acceptance proof. The four decisions remain approved; later unapproved BLOCKER labels do not reopen those decisions.

Current implementation corrections: Admin restrict/lock/unlock uses the case-scoped PUT status route documented in Admin.md. Dispute resolution serializes with cancellation/payout on the fund lock. Fixed rejoin may confirm obligations and contribute after fully reconciled membership refunds; expense outflows retain their previous guard, and refund formulas are unchanged. Election coverage timestamps with explicit UTC/offset configuration are normalized to UTC before evaluating the 15-day condition.

## Task 16.1 — implementation and API map

Architecture: ASP.NET Core API (`backend/SpendWise/SpendWise.API.csproj`, net10.0), Application handlers/services, Domain entities, EF Core SQL Server Infrastructure. Tests use isolated SQLite/InMemory unless explicitly classified SQL Server. Controllers and their DTOs are canonical; `/openapi/v1.json` is available in Development/Testing. Financial writes use server-side PIN validation, transactions, double-entry ledger, idempotency, and concurrency controls.

| Module | Canonical API family / permission | Implementation status |
|---|---|---|
| Registration / email OTP | `/api/auth/register`, `/api/auth/email-verification/verify`, `/resend`; anonymous challenge | IMPLEMENTED; current verification in Task14 report |
| Password / PIN / sessions | `/api/auth/login`, `/pin/setup`, `/pin/verify`, `/refresh`, `/logout`, `/sessions`, `/change-password`, `/forgot-password`, `/reset-password`, `/pin/forgot`, `/pin/reset/validate`, `/pin/reset`; token or bearer as controller requires | IMPLEMENTED; credentials remain hashed; Production does not use static OTP |
| Profile / preferences | `/api/users/me`; authenticated self | IMPLEMENTED |
| Wallet / transactions / P2P | `/api/wallets`, `/api/transactions`, `/api/transfers/internal`, `/api/transfers/p2p`; authenticated owner, financial PIN and intent key | IMPLEMENTED; ledger-backed writes; no external bank payout implied |
| Budgets / savings / recurring / rules | `/api/budgets`, `/api/goals`, `/api/recurring-transactions`, `/api/transaction-rules`; authenticated self | IMPLEMENTED; API regression distinct from live-provider verification |
| Friends | `/api/friends`; authenticated parties | IMPLEMENTED; invitation acceptance and ownership enforced |
| Fixed / Free membership | `/api/v1/funds`, `/{id}/members`, `/{id}/invitations`, `/invitations/{id}/accept`; User membership | IMPLEMENTED; Fixed max30, Free max6, User max10 active funds |
| Fixed obligations / contributions | `/{id}/obligations/confirm`, `/{id}/contributions`, `/{id}/finance`; Owner confirms, member contributes with PIN | IMPLEMENTED; integer VND, stable seats; stale configuration blocks spending |
| Fixed spending | `/{id}/requests`, `/requests/{id}/review`, `/requests/{id}/cancel`, `/disputes/{requestId}/resolve`; member CanSpend or Owner, PIN | IMPLEMENTED: immediate small internal payouts; cumulative threshold 20% and Pending24h; reporting large expense suspends expense and creates SupportCase; Admin dispute resolution endpoint cancels or unsuspends expense |
| Free expense / withdrawal | same request routes; own Withdrawal only; member expense awaits Owner | IMPLEMENTED internal payout; recipient must have active VND wallet and valid ledger account; no Completed without posting |
| Refund / leave / kick / dissolve | `/{id}/refund-preview`, `/{id}/leave`, `/{id}/members/{userId}/kick`, `/{id}/dissolve`; member self / Owner + PIN | IMPLEMENTED: common unified calculator; Rejoin begins fresh membership episode (0 VND prior contribution, net scoped equity); exact-zero dissolution; pending/suspended expense blocks preview and execution until settled/cancelled |
| Fund activities / ownership | `/{id}/activities`, `/{id}/ownership-offers`, `/ownership-offers/{id}/accept` | IMPLEMENTED; financial and activity history separate; current Owner differs from system Admin |
| Elections / new activity | `/{id}/elections`, `/elections/{id}/...`, `/{id}/elections/{id}/reject`, `/{id}/activity-proposals` | IMPLEMENTED: Approved default timeline (24h/48h/24h/24h/24h, FreezeMembership=true, AllowContributions=false, Outcomes=Fail); winner rejection endpoint; financial operations and invitations paused during active election |
| Notifications | `/api/notifications`, `/search`, `/{id}/read`, `/read-all`, `/preferences`; authenticated self | IMPLEMENTED; email SMTP acceptance is not mailbox delivery; push LIVE NOT VERIFIED |
| Support / disputes | `/api/cases/support`, `/support/{id}`, `/support/{id}/messages`, `/disputes`, `/disputes/{id}`; authenticated case owner | IMPLEMENTED; Admin access separately assigned and audited |
| AI | `/api/ai/*`, `/api/admin/ai/*`; authenticated role/tools | PARTIAL / DEFERRED live Gemini; no legacy OpenAI provider/SDK/fallback. Missing Gemini key or explicitly configured model gives controlled503; model availability LIVE NOT VERIFIED |
| Banking | `/api/bank-connections/*`; authenticated self, provider-gated | PARTIAL / DEFERRED live SePay; Testing denies Development TestMode routes; no simulated successful bank settlement |

Task14 safety changes do not resolve the product decisions in section12 automatically. In particular, reported/suspended expense resolution, complex multi-activity financial entitlements, and election timing remain BLOCKED where evidence of approval is missing. Current build/test counts and NOT RUN checks are recorded in `backend/docs/TASK14-VERIFICATION.md`; old counts below are historical.

Email environments: Development defaults to local pickup and may explicitly use SMTP for real recipients. Testing always captures to local pickup even if SMTP settings are inherited. Production requires SMTP configuration; `.test` recipients are rejected before a network connection. Pickup files contain sensitive recovery content and must stay outside tracked source. Outbox retries are bounded to five attempts; failure logs contain message identifiers, not OTP/token content. LIVE DELIVERY NOT VERIFIED.

---


**Tệp:** `User.md`  
**Phiên bản:** 1.2 — 18/10/2026 (giữ nguyên toàn bộ v1.1; chỉ bổ sung vá các lỗ hổng sau rà soát: chi tiêu của chủ nhóm, thời hạn phản hồi của chủ nhóm, lệch 1đ khi dịch thứ tự, biểu quyết im lặng, điều kiện vận hành tiền thật)  
**Trạng thái:** **USER MOBILE PRODUCT SCOPE CHỐT; CÁC CONTRACT TIỀN CÒN OPEN BLOCKER** — chỉ phần có nhãn **ĐÃ CHỐT** được xem là quyết định sản phẩm; không triển khai các khoản tiền còn thiếu công thức/điều kiện.  
**Đối tượng:** User sử dụng ứng dụng Mobile SpendWise; người tạo/chủ nhóm và thành viên là quyền **trong từng nhóm**, không phải Admin hệ thống.  
**Mục đích:** Nguồn tham chiếu yêu cầu sản phẩm cho bước đối chiếu Backend/Mobile, thiết kế UI/UX và lập kế hoạch triển khai. Đây **không phải** bằng chứng tất cả chức năng đã có API, được triển khai hoặc đã kiểm thử thành công.

**Chính sách hiệu lực:** Quyết định User mới nhất ưu tiên so với các mô tả cũ: **một chủ nhóm hiện tại; có chuyển quyền; không dùng SpendWise đủ 15 ngày thì mở bầu lại hai vòng; giữ nguyên quyền tiền khi đổi chủ; chủ nhóm loại 1 được tự quyết giữ/tăng/giảm tổng dự kiến, không biểu quyết; kick phải hoàn tiền bằng công thức Backend; đóng hoạt động thì không chi tiếp; mời phải được chấp nhận; lịch sử nhóm tách lịch sử giao dịch.** Loại bỏ Money Request hoàn toàn khỏi User Mobile. Bản v1.0 chỉ lưu đối chiếu, không dùng để triển khai những điểm đã bị v1.1 thay thế. **v1.2 chỉ bổ sung các quy tắc ở mục 4.5 (điểm 11), 4.6, 5.7 (điểm 7–10), 5.10 (điểm 9–10), 9.2 (phần “ghế đồng dư”), 9.4, 12 (điểm 10–11 và phần bổ sung điểm 7) và Phụ lục B; mọi nội dung v1.1 khác được giữ nguyên.**

**Nhãn:** `ĐÃ CHỐT` = người dùng quyết định; `QUY TẮC AN TOÀN` = hệ thống không được vi phạm tính bảo toàn/ủy quyền; `CHƯA CHỐT — BLOCKER` = phải được người dùng phê duyệt trước khi triển khai tính năng liên quan, agent không được tự lấp chỗ trống. Tài liệu không chứng minh tính năng đã có code/API/E2E và không cho phép sửa source mà chưa được giao. **v1.2 thêm nhãn `THAM SỐ MẶC ĐỊNH` = giá trị khởi điểm do tài liệu đề xuất để chạy thử; Product Owner có quyền đổi con số mà không ảnh hưởng cơ chế đã chốt.**

---

## 1. Tầm nhìn sản phẩm và lý do người dùng tải SpendWise

SpendWise là ứng dụng Mobile hỗ trợ cá nhân quản lý thu chi, đồng thời cho phép nhiều người góp và quản lý một quỹ chung minh bạch.

**Lý do thứ nhất — quản lý tiền cá nhân:** User muốn thấy rõ tiền vào/ra, hiện còn bao nhiêu, đã chi vào việc gì, và theo dõi được tình hình tài chính thay vì ghi chép phân tán.

**Lý do thứ hai — quỹ nhóm, trọng tâm khác biệt:** Các cặp đôi góp tiền đám cưới hoặc nhóm khoảng 20 người đi biển thường nhắn trong nhóm chat và ghi sổ thủ công. Họ khó biết chắc ai đã góp, ai chưa góp, ai đã chi, còn dư bao nhiêu. SpendWise phải cung cấp tiến trình, lịch sử và quyền quản lý rõ ràng. **Nhóm góp cố định** là mô hình riêng được chủ dự án phát triển; **nhóm tự do** theo hướng Quỹ Nhóm MoMo, với các giới hạn và quy tắc giải tán riêng đã duyệt.

**Chất lượng trải nghiệm:** Mobile phải đẹp, dễ đọc, trực quan, ít thao tác, không rối mắt; khi giao dịch, người dùng phải phân biệt được trạng thái đang xử lý, thất bại và thành công thật. Trải nghiệm thuận tiện giúp tăng khả năng sử dụng lâu dài và giới thiệu cho bạn bè. Không vì đẹp mà làm khó sử dụng hay giả trạng thái tiền.

## 2. Phạm vi ứng dụng và ranh giới quyền

- User chỉ sử dụng **ứng dụng Mobile**; không xây lại Web User App. Website công khai là trang giới thiệu; Admin và quản trị hệ thống nằm ở Web riêng, không có menu Admin trên Mobile.
- Mobile dùng **Backend SpendWise và dữ liệu authoritative từ Backend**; không tự tạo cơ sở dữ liệu nghiệp vụ riêng hoặc lấy state giả làm sự thật.
- **Chủ nhóm / thành viên** là quyền trong một nhóm cụ thể, không phải vai trò `SystemAdmin`.
- Không giả định Business Admin hoặc Super Admin có quyền nào ngoài các phần User đã yêu cầu; các vai trò Admin sẽ được thảo luận, phê duyệt trong tài liệu riêng.
- **Loại khỏi sản phẩm User Mobile:** chức năng **Yêu cầu tiền (Money Request)**, tức gửi một đề nghị thanh toán trực tiếp cho User khác. Không hiển thị trong danh mục chức năng, màn hình hay luồng chính. Việc xóa mã nguồn/API cũ (nếu có) cần kiểm tra dependency và phê duyệt riêng, không tự xóa khi đọc tài liệu này.

## 3. Danh mục chức năng User Mobile được giữ trong phạm vi

| Hạng mục | Phạm vi nghiệp vụ đã duyệt/kế thừa | Ghi chú trạng thái |
|---|---|---|
| Tài khoản và bảo mật | Đăng ký, đăng nhập, OTP, PIN, khôi phục mật khẩu/PIN, đăng xuất, phiên/thiết bị | Phải dùng Auth Backend thật |
| Trang chủ | Số dư, tổng quan thu chi, biểu đồ, giao dịch gần đây, lối tắt | Mobile UI dễ hiểu, không dồn quá nhiều nội dung |
| Ví cá nhân | Ví chính, tối đa hai ví tạo thêm, số dư, chuyển nội bộ giữa ví | Tối đa 3 ví/User |
| Thu/chi | Ghi thu chi, danh mục, lịch sử, bằng chứng và chỉnh sửa có truy vết | Không xóa cứng giao dịch đã hoàn tất |
| Ngân hàng/SePay | Liên kết, ngắt/kích hoạt lại, các luồng nạp tiền hiện có | Theo API được phép; không mở rộng tự ý |
| Chuyển tiền | Chọn nguồn, người nhận, tiền, xác nhận/trạng thái | Không báo thành công trước Backend |
| Ngân sách | Giới hạn chi tiêu theo danh mục/tháng và tiến độ | Phân biệt chi sinh hoạt với chuyển nội bộ |
| Mục tiêu | Tạo, góp, rút phần tích lũy, theo dõi tiến trình | Chuyển sang mục tiêu không phải chi sinh hoạt |
| Giao dịch định kỳ | Các chức năng định kỳ đã có trong phạm vi cũ | Hành vi thực tế theo contract, không suy diễn auto-charge |
| Phân loại | Danh mục và quy tắc phân loại giao dịch | Không đổi bản chất giao dịch khi đổi nhãn |
| Bạn bè | Kết bạn, quản lý bạn bè, tên nhóm chung | Tối đa 100 bạn |
| Quỹ nhóm | Loại 1 cố định, loại 2 tự do, góp/chi, quản lý thành viên, một chủ nhóm, chuyển quyền/bầu lại, lịch sử nhóm riêng, giải tán | Quy tắc ở mục 4–9; các BLOCKER tiền ở mục 12 |
| Thông báo | Xem, đánh dấu đã đọc, tùy chọn thông báo | Thông báo theo kết quả nghiệp vụ thực |
| Hỗ trợ | Gửi ticket, bằng chứng, trả lời và theo dõi trạng thái | Liên kết xử lý với Admin được phân quyền |
| Tranh chấp | Khiếu nại giao dịch/quỹ, bổ sung bằng chứng và theo dõi | Không buộc nhớ mã nhóm mới được báo cáo |
| SpendWise AI | Giữ khu vực AI trong phạm vi cũ | Không tự gán thêm quyền/năng lực mới; nguồn cũ có AI Preview |
| Hồ sơ và tùy chọn | Hồ sơ, PIN/mật khẩu, phiên/thiết bị, thông báo, VI/EN, Light/Dark/System, ẩn/hiện số dư khi phù hợp | Theo API và thiết kế hiện hành |

**Không đưa Money Request vào danh mục trên.**

## 4. Quy tắc chung về ví, tiền, bạn bè và nhóm

### 4.1. Ví cá nhân

1. Mỗi User tối đa **3 ví**: một **Ví chính (Main)** được tạo mặc định và tối đa hai ví bổ sung.
2. Có thể chuyển tiền nội bộ giữa các ví khi Backend xác nhận đủ điều kiện/số dư.
3. Ví SpendWise và tài khoản ngân hàng liên kết **không phải cùng một đối tượng**.
4. **Mọi khoản đóng góp vào quỹ nhóm chỉ lấy từ ví SpendWise cá nhân.** Không góp trực tiếp từ tài khoản ngân hàng vào nhóm.
5. Hoàn quỹ là giao dịch **nội bộ về ví cá nhân** được chọn, mặc định Ví chính nếu không chỉ định. Không hoàn trực tiếp ra ngân hàng, không tạo thu nhập giả.
6. Không dùng số dư hoặc kết quả giao dịch giả trên UI thay cho dữ liệu server.

### 4.2. Giới hạn và mã nhóm

1. Mỗi User tham gia tối đa **10 nhóm đang hoạt động**, tính chung cả loại 1 và loại 2, gồm nhóm tự tạo và nhóm được mời. Nhóm đã giải tán không tính.
2. Nếu người được mời đã đủ 10 nhóm hoạt động, bên mời thấy: **“Người này hiện đã tối đa nhóm”**; không gửi lời mời thành công giả.
3. Mỗi User có tối đa **100 bạn bè**.
4. Mỗi nhóm được cấp mã toàn hệ thống theo thứ tự tạo: `SPW000001`, `SPW000002`, `SPW000003`… không có sequence riêng theo User.
5. Mã nhóm không thay đổi và **không bao giờ được tái cấp**, kể cả sau giải tán; mã dùng để người dùng cung cấp khi cần hỗ trợ/khiếu nại.
6. Mã nhóm **không thay thế xác thực/phân quyền**. Không ai được xem dữ liệu riêng chỉ vì đoán đúng mã.
7. Trong danh sách bạn bè, dưới tên bạn bè có dòng tên nhóm chung; nếu không có nhóm chung thì để trống. Cách hiển thị khi có nhiều nhóm chung chưa được quyết định, không tự thay đổi nghiệp vụ.

### 4.3. Hai loại nhóm

| Đặc điểm | **Loại 1 — Góp cố định** | **Loại 2 — Tự do** |
|---|---|---|
| Mục đích | Góp đủ một tổng dự kiến để thực hiện hoạt động | Góp/chi linh hoạt theo cơ chế Quỹ Nhóm MoMo tham khảo |
| Quy mô | Tối đa **30** người, gồm chủ nhóm | Tối đa **6** người = chủ nhóm + 5 |
| Mức góp | Hệ thống tính theo tổng dự kiến và người có nghĩa vụ góp | Góp linh hoạt, không có mức góp bắt buộc kiểu cố định |
| Chi tiền | Phải đạt điều kiện góp đủ; chủ nhóm chi trực tiếp; thành viên chi trực tiếp nếu được chủ nhóm bật CanSpend (mặc định OFF), không cần chủ duyệt từng khoản; kiểm soát tiền khóa & ngưỡng chi lớn | Theo cơ chế yêu cầu chi/rút và duyệt kiểu MoMo; chủ nhóm duyệt; không vượt số dư |
| Thay đổi thành viên | Tính lại mức góp theo quyết định chủ nhóm | Theo mô hình MoMo trong giới hạn SpendWise |
| Hoạt động mới | Có kết thúc hoạt động và biểu quyết | Không áp quy trình riêng loại 1 |
| Giải tán | Chia lại/hoàn đúng quyền lợi, quỹ = 0 | **Ngoại lệ SpendWise:** chia số dư theo tỷ lệ đóng góp; quỹ = 0 |
| Lịch sử | Ghi đầy đủ | Ghi đầy đủ |

### 4.4. Hai lịch sử riêng, không làm mất liên kết

**ĐÃ CHỐT:** Giao diện nhóm có **Lịch sử hoạt động nhóm** (mời/nhận lời mời, tham gia, rời/kick, thay đổi vai trò, bỏ phiếu, mở bầu cử, chuyển chủ, chỉnh tổng dự kiến, bật/tắt quyền chi CanSpend, báo cáo/tạm dừng khoản chi, chủ nhóm quá hạn phản hồi, hoàn thành hoạt động, giải tán) và **Lịch sử giao dịch** (đóng góp, chi trực tiếp Fixed, yêu cầu/duyệt/từ chối chi Free, khoản chi chờ thực hiện, chi, hoàn tiền, chuyển tiếp). Hai danh sách **hiển thị riêng**, không trộn hoạt động phi tài chính với bút toán tiền.

Mỗi bản ghi cần người/hệ thống thực hiện, hành động, thời điểm server, trạng thái, tham chiếu có liên quan; giao dịch tiền có số tiền, nguồn/đích, người nhận, mục đích và bằng chứng nếu đính kèm. Có mã liên kết hai lịch sử khi một hoạt động gây ra giao dịch (ví dụ rời nhóm dẫn đến hoàn tiền). Không User/chủ nhóm nào tự sửa/xóa lịch sử gốc; điều chỉnh dùng bút toán/sự kiện mới có tham chiếu ngược. Thông báo nhóm sử dụng dữ liệu nghiệp vụ đã lưu, không tự bịa thành công.

### 4.5. Một chủ nhóm, chuyển quyền và thay chủ do không hoạt động

**ĐÃ CHỐT — áp dụng cho CẢ HAI LOẠI NHÓM:**

1. Có đúng **một chủ nhóm hiện tại** tại mọi thời điểm; **không có phó nhóm/đồng chủ nhóm**. **Người sáng lập** là danh tính người tạo ban đầu, được lưu bất biến trong lịch sử; chức danh chủ nhóm hiện tại có thể thay đổi.
2. Chủ nhóm có thể chủ động chọn **bất kỳ thành viên hợp lệ** để chuyển quyền; người được chọn **chấp nhận** thì Backend chuyển quyền nguyên tử, chủ cũ thành thành viên. Không tự chuyển tài sản riêng hoặc xóa lịch sử. Chủ nhóm muốn rời phải chuyển quyền thành công trước, trừ tình huống chỉ còn một người thì không có người nhận để chuyển: không cho rời khiến nhóm vô chủ; phải xử lý giải tán đúng tài chính.
3. **Không sử dụng SpendWise thực sự trong 15 ngày liên tiếp**: đủ điều kiện **mở bầu lại chủ nhóm**, không phải lập tức mất quyền. Tính theo hành động sử dụng app do người dùng thực hiện có xác thực; **không** tính refresh-token, thông báo push, đồng bộ hoặc tác vụ nền là hoạt động. **Không yêu cầu có giao dịch tiền**. Tài khoản bị Admin khóa hoặc mất khả năng truy cập cũng không tự bị giáng quyền ngay; nếu không có hoạt động hợp lệ đủ 15 ngày thì áp dụng quá trình bầu lại. Khóa tài khoản vẫn có hiệu lực riêng, không bị bầu cử vô hiệu hóa.
4. Bầu lại gồm **vòng 1 — đề cử/bầu ứng viên**, mỗi cử tri hợp lệ một phiếu; người được nhiều phiếu nhất vào vòng 2. Nếu **các ứng viên cùng cao phiếu**, tổ chức **một lần bầu phụ** chỉ giữa các ứng viên hòa cao nhất; nếu **vẫn hòa** thì bầu cử **thất bại**, không bốc thăm, không tự chỉ định. Ứng viên phải là thành viên đủ điều kiện và chấp nhận nhận quyền.
5. **Vòng 2 — xác nhận** ứng viên thắng vòng 1 bằng phiếu **Đồng ý/Không đồng ý**. Thông qua khi **trên 50% tổng cử tri hợp lệ đã chốt danh sách lúc mở cuộc bầu** chọn Đồng ý. Người không trả lời không phải phiếu Đồng ý. Nếu không đủ phiếu thì bầu cử thất bại. Chủ nhóm cũ đang không hoạt động/không thể dùng tài khoản **không nằm trong danh sách bỏ phiếu thay chủ**, nhưng **vẫn giữ đầy đủ quyền tài chính**.
6. Bầu thành công: người trúng cử là **chủ nhóm duy nhất**; chủ cũ thành **thành viên thường**, vẫn giữ lịch sử và quyền lợi tiền. Khi khôi phục tài khoản sau đó, chủ cũ **không tự động lấy lại quyền quản lý**. Nếu chủ mới giải tán thì phần hoàn thuộc chủ cũ chuyển về **Ví chính** của họ theo quyền lợi hợp lệ.
7. Người chọn **Không** ở vòng xác nhận có thể chọn rời: bắt đầu mốc **24 giờ chờ rời** theo quyết định; sau khi đủ 24 giờ **và khoản hoàn đúng quyền lợi đã hoàn tất**, Backend mới kết thúc tư cách thành viên. Không được đảo phiếu để gia nhập lại ngay; muốn quay lại phải **được mời, chấp nhận và tham gia như thành viên mới**. Người im lặng **không tự bị kick** chỉ vì không bỏ phiếu, trừ khi họ chủ động chọn rời sau kết quả. Nếu bầu cử thất bại, các thành viên vẫn được **rời tự do theo quy trình hoàn tiền hợp lệ, không cần chủ cũ phê duyệt**; ai muốn ở lại thì ở lại. Trường hợp hoàn tiền lỗi, hết 24 giờ **không** có nghĩa được kick và bỏ quyền tiền.
8. Khi chủ cũ còn quyền lợi tiền/quỹ, **không được kick chỉ vì mất chức**; chuyển quyền không phải khoản rút hay hoàn tiền. Các quy tắc bảo vệ tiền và hoàn khi rời/kick vẫn áp dụng cho những trường hợp đủ điều kiện khác.
9. Nếu tất cả thành viên khác đều rời hợp lệ, nhóm **không tự giải tán** chỉ vì còn một người. Nếu người sáng lập/chủ cũ là người ở lại duy nhất thì nhóm còn **một mình họ cùng phần tiền hợp lệ của họ**; chức danh chủ nhóm gắn cho thành viên duy nhất để tránh nhóm vô chủ. Nếu tài khoản đang khóa, chức danh đó **không được phép vượt qua trạng thái khóa**; chỉ thao tác sau khi khôi phục hợp lệ. Không được chuyển tiền của người đã rời sang người còn lại.
10. **Khóa danh sách cử tri** và thành viên hợp lệ khi mở cuộc bầu, lưu đợt bầu, đề cử, phiếu, kết quả từng vòng, người nhận quyền, thời điểm và lý do; không bỏ phiếu trùng, không chuyển quyền hai lần do retry. Thông báo mẫu: “A không hoạt động trên SpendWise 15 ngày; nhóm mở bầu chủ mới”; “B đã trở thành chủ nhóm mới”; “D đã tham gia nhóm”; “D đã rời nhóm”. Các thông báo thuộc **Lịch sử hoạt động nhóm**, không phải Lịch sử giao dịch.
11. **Bổ sung v1.2:** Chủ nhóm vẫn dùng app nhưng không phản hồi các việc bắt buộc ở **mục 4.6** cũng là một điều kiện để mở bầu lại chủ nhóm, theo đúng cơ chế hai vòng ở các điểm 4–10 nêu trên.

**CHƯA CHỐT — BLOCKER cho lập trình bầu cử tự động:** Thời lượng/mốc đóng **vòng 1, vòng bầu phụ, vòng 2**, cách xử lý không có ai nhận đề cử hoặc ứng viên không nhận chức, thời điểm chốt số cử tri nếu giữa hai vòng có tài khoản bị khóa, và thao tác khóa/mở đóng góp–chi–rời trong khi bầu cử. Không tự lấy mốc 24h chờ rời để áp cho thời gian bỏ phiếu. **Ngưỡng vòng 2 trên 50% đã CHỐT**.

### 4.6. Thời hạn phản hồi của chủ nhóm (v1.2 — vá lỗ hổng “giữ quyết định vô thời hạn”)

**ĐÃ CHỐT (v1.2):** Chủ nhóm còn mở app không có nghĩa là nhóm đang được quản lý. Điều kiện 15 ngày ở mục 4.5 chỉ bắt được chủ nhóm biến mất, không bắt được chủ nhóm vẫn đăng nhập nhưng không làm gì.

1. Ba việc sau **phải được chủ nhóm phản hồi** (đồng ý hoặc từ chối đều là phản hồi hợp lệ) trong thời hạn: (a) đơn xin rời của thành viên đã góp/chi (mục 5.8.1); (b) yêu cầu chi/rút đang chờ duyệt thuộc nhóm Free (loại 2) hoặc các yêu cầu chi bắt buộc phê duyệt theo quy trình áp dụng; (c) sau khi hoạt động đã Hoàn thành/Đóng, chọn chia lại/giải tán hoặc đề xuất hoạt động mới (mục 5.9). (Lưu ý: Đối với nhóm Fixed - loại 1, thành viên được bật CanSpend thực hiện chi trực tiếp, không tạo yêu cầu chi chờ chủ duyệt).
2. **THAM SỐ MẶC ĐỊNH:** thời hạn **7 ngày**, tính từ lúc đơn (a) hoặc yêu cầu (b) được gửi, hoặc từ lúc hoạt động (c) được đóng.
3. Quá hạn mà chủ nhóm chưa phản hồi, người đang bị chờ (với (c) là bất kỳ thành viên nào còn quyền lợi tiền trong nhóm) có nút **“Yêu cầu xử lý”**, gồm hai đường: (i) mở ticket Support tự động gắn mã nhóm và mã yêu cầu; (ii) được mở **bầu lại chủ nhóm** theo mục 4.5, điểm 4–10. Trong cuộc bầu này **chủ nhóm hiện tại không nằm trong danh sách cử tri** (giống trường hợp không hoạt động) nhưng vẫn giữ đầy đủ quyền lợi tiền. Vòng xác nhận vẫn cần trên 50% tổng cử tri hợp lệ.
4. **Không tự duyệt thay chủ nhóm.** Quá hạn không làm đơn xin rời hay khoản chi tự được chấp thuận, không có tiền nào tự di chuyển. Hệ thống chỉ mở đường xử lý; mọi khoản hoàn/chi sau đó vẫn phải qua kiểm tra bảo toàn ở mục 9.4.
5. Mỗi nhóm chỉ có **một cuộc bầu lại tại một thời điểm**; nếu đang có cuộc bầu do 15 ngày không hoạt động thì không mở thêm cuộc bầu do không phản hồi.
6. Quy tắc này **không ép chủ nhóm phải đồng ý** và không thay đổi quyền duyệt/từ chối đã chốt. Các trường hợp lạm quyền khác (không phải quá hạn) vẫn giải quyết bằng Support/Admin theo mục 5.5.1.
7. Ghi sự kiện quá hạn, yêu cầu xử lý và mở bầu vào **Lịch sử hoạt động nhóm**; thông báo cho toàn bộ thành viên. Các mốc còn lại của cuộc bầu thuộc BLOCKER mục 12.

---

## 5. Loại 1 — Nhóm góp cố định (đặc trưng SpendWise)

### 5.1. Tạo nhóm

1. User tạo nhóm, tự do đặt tên; tên như “Đi biển 20 người” **không** quyết định số lượng thực tế.
2. Người tạo là **chủ nhóm**: có thêm quyền quản lý nhưng **vẫn phải góp/tuân thủ việc chi như các thành viên**.
3. Mỗi nhóm tối đa **30** thành viên và được cấp mã SPW bất biến như mục 4.
4. Chủ nhóm nhập **tổng chi phí dự kiến** của hoạt động.
5. Backend chia tổng dự kiến thành nghĩa vụ góp bằng **số nguyên VND**, theo thứ tự tham gia ổn định và quy tắc ở mục 9.

### 5.2. Mời thành viên

1. **ĐÃ CHỐT:** Chủ nhóm có thể **gửi lời mời trực tiếp** cho bạn bè mà không cần một người khác duyệt lời mời. Thành viên thường có thể **đề nghị mời** bạn bè, cần chủ nhóm duyệt đề nghị trước khi gửi lời mời.
2. **Bất kỳ người được mời nào cũng phải chủ động chấp nhận** trước khi trở thành thành viên, bị tính vào giới hạn 10 nhóm đang hoạt động và phát sinh nghĩa vụ góp. Không thể dùng quyền chủ nhóm ép người khác tham gia.
3. Trước khi chấp nhận, Backend kiểm tra lại giới hạn nhóm (30 với loại 1), giới hạn nhóm/User (10), tư cách tài khoản và sự trùng lặp; tránh nhận lời mời song song vượt số chỗ.
4. Ghi lịch sử người mời, người duyệt đề nghị, người nhận lời mời, sự kiện chấp nhận/rời/kick và thời gian.
5. Khi số thành viên thay đổi, **chủ nhóm hiện tại tự quyết** giữ/tăng/giảm tổng dự kiến. Không tạo cuộc biểu quyết thay đổi tổng tiền.

### 5.3. Góp tiền và tiến trình

1. Tổng dự kiến `T` chia cho số người có nghĩa vụ góp `n`, áp dụng phép chia nguyên có rải đồng dư ở mục 9.
2. User có thể **góp từng phần**, kể cả khi nhóm chưa đủ thành viên: ví dụ `300.000 / 1.000.000đ`.
3. **Không được góp vượt mức yêu cầu tại thời điểm thực hiện góp.** Nếu phải góp 1.000.000đ thì các khoản góp thực tế của người ấy không được vượt 1.000.000đ.
4. Lịch sử và màn hình nhóm cho biết ai đã góp, ai chưa góp, số đã góp và còn phải góp.
5. Mỗi khoản góp chỉ từ **ví SpendWise cá nhân**. Không được hiển thị góp thành công khi Backend chưa xác nhận ghi tiền/sổ.
6. Người dùng được xem mức tiền chính xác, không chỉ một cách viết rút gọn.

### 5.4. Điều kiện chi và hai loại khóa

1. **ĐÃ CHỐT:** Nhóm loại 1 **không được chi** khi mục tiêu góp của hoạt động hiện tại chưa được hoàn thành. Người mới chưa góp đủ nghĩa vụ dù có tiền dư của người khác vẫn chưa thỏa điều kiện. Kiểm tra nghĩa vụ đúng từng người và phần đóng góp được công nhận cho hoạt động; khoản dư cá nhân bị khóa **không** dùng để lấp số thiếu của người khác.
2. **Khóa dư cá nhân:** tiền `max(0, số tiền hợp lệ được ghi cho người đó - mức góp yêu cầu)` do thay đổi T/số người; đây là khoản được bảo vệ riêng, không cho dùng để chi quỹ chung. Khi mức yêu cầu tăng, khoản dư có thể được hấp thụ đúng vào nghĩa vụ mới.
3. **Khóa chi cả hoạt động:** dù không có khoản dư cá nhân, nếu chưa đủ điều kiện góp của hoạt động thì cả hoạt động chưa được chi. Đây là trạng thái/quyền chi, **không phải một khoản dư cá nhân để hoàn riêng**.
4. Khi mục tiêu đã hoàn thành, vẫn không chi vượt số dư có thể sử dụng hoặc tổng chi phí được duyệt. Kiểm tra nguyên tử ở Backend trong cùng thao tác ghi, kể cả khi có nhiều yêu cầu đồng thời.
5. Sau khi hoạt động được **Hoàn thành/Đóng**, không được tạo/duyệt/ghi khoản chi mới thuộc hoạt động đó. Số dư còn lại chỉ chờ quyết toán, chia lại, hoặc chuyển sang hoạt động mới **sau biểu quyết hợp lệ**.

### 5.5. Thay đổi số thành viên và tổng dự kiến — chủ nhóm quyết định

1. Khi thêm/giảm thành viên, chủ nhóm nhận thông báo và pop-up chọn **giữ nguyên / tăng / giảm** tổng chi phí dự kiến `T`. **ĐÃ CHỐT: chủ nhóm quyết định hợp lệ là áp dụng ngay, KHÔNG CẦN bỏ phiếu hoặc thành viên chấp thuận.** Khiếu nại lạm quyền được giải quyết bằng Support/Admin, không phải quyền phủ quyết tự động của thành viên.
2. Thay đổi thành viên có thể chờ xác nhận cấu hình T của chủ nhóm; **ngay khi chủ nhóm xác nhận và Backend kiểm tra hợp lệ**, tính lại mức góp, lưu cấu hình mới và thông báo cho thành viên; không chờ biểu quyết. Không cho giao dịch dựa trên cấu hình tài chính nửa cũ nửa mới.
3. Backend chia lại nghĩa vụ `T/n` theo số nguyên VND và thứ tự xác định; không sửa các khoản góp/chi đã phát sinh. Chủ nhóm **không** tự sửa số hoàn, tự di chuyển đóng góp giữa các cá nhân, hoặc xóa phần góp/chi cũ.
4. Nếu hoạt động **đã phát sinh chi**, không được tính lại như thể toàn bộ `T` vẫn chưa dùng: cần đối soát `T`, chi phí đã phát sinh, số dư quỹ, phần hoàn và nghĩa vụ góp còn lại. Không giảm T xuống dưới phần chi phí đã phát sinh hoặc cấu hình gây mất tiền khóa/âm quỹ. Cách quy đổi nghĩa vụ sau khi đã chi là **BLOCKER ở mục 12**; không cho agent tự chọn công thức.
5. Chủ nhóm đang không hoạt động thì thay đổi chưa giải quyết không được dùng làm lý do rút tiền vượt thực tế. Phải xử lý theo cơ chế thay chủ 15 ngày và các trường hợp thoát nhóm đặc biệt; không bỏ qua an toàn số dư.

**Ví dụ giữ tổng 20.000.000đ (chưa chi):**

- 20 người → mỗi người 1.000.000đ.
- Giảm còn 19 người → 11 người 1.052.632đ, 8 người 1.052.631đ. Người đã góp 1.000.000đ chưa hoàn thành mức mới.
- Tăng thành 25 người → mỗi người 800.000đ. Người đã góp 1.000.000đ có **200.000đ dư được khóa riêng**, không sửa lịch sử gốc.

### 5.6. Tiền dư phát sinh do tính lại — sửa mâu thuẫn hai kiểu khóa

1. Không được góp vượt mục tiêu tại thời điểm góp. Tiền dư có thể sinh ra khi nghĩa vụ thay đổi hoặc khoản quyền lợi được chuyển kỳ hợp lệ.
2. **Khoản dư thực tế của cá nhân** tại cấu hình hiện hành là phần đóng góp/tiền chuyển tiếp còn gắn cho cá nhân **vượt** nghĩa vụ của họ; khoản dư này bị khóa riêng, chỉ được chủ trương chi/hoàn theo đúng quy tắc tiền dư. Không phải mọi tiền bị cấm chi đều là dư cá nhân.
3. Ví dụ người đã góp **1.000.000đ**, chưa có khoản chi/hoàn nào làm biến động quyền lợi:

   | Mức yêu cầu hiện tại | Dư cá nhân | Còn thiếu | Ý nghĩa |
   |---|---:|---:|---|
   | 800.000đ | 200.000đ | 0đ | Hoàn thành, 200.000đ dư khóa riêng |
   | 1.000.000đ | 0đ | 0đ | Hoàn thành; khoản dư trước đã được hấp thụ |
   | 1.200.000đ | 0đ | 200.000đ | **Chưa hoàn thành; khóa CHI CẢ HOẠT ĐỘNG** theo điều kiện góp, không phải còn 200.000đ dư |

4. Nếu đã chi hoặc hoàn, **không thể đơn giản lấy tổng đóng góp gốc trừ nghĩa vụ mới** để kết luận số dư khóa bằng bao nhiêu: còn phải đối chiếu ledger, khoản đã phân bổ cho chi phí, đã rút/hoàn, chuyển kỳ. Không được tạo khoản khóa lớn hơn tiền thực được bảo vệ.
5. Khi chia quỹ, hoàn **khoản dư thực còn bảo vệ** cho đúng người trước; chỉ sau đó phân chia quỹ khả dụng còn lại theo trọng số đã quyết toán. **Không dùng tiền dư vừa hoàn làm trọng số lần hai.** Phần công thức ledger đầy đủ khi đã phát sinh chi nằm trong **BLOCKER mục 12**.

### 5.7. Quyền chi tiền và thực hiện khoản chi (v1.3 — ĐÃ CHỐT)

1. **Quyền chi tiền (`CanSpend`):** Chủ nhóm (không phải Admin hệ thống) có quyền bật/tắt `CanSpend` (ON/OFF) cho từng thành viên; ghi lại sự kiện bật/tắt vào **Lịch sử hoạt động nhóm**. Thành viên mới tham gia mặc định **OFF**.
2. **Cơ chế chi tiền theo trạng thái `CanSpend`:**
   - **OFF:** Thành viên không được trực tiếp chi tiền từ quỹ.
   - **ON:** Thành viên được **trực tiếp thực hiện khoản chi**, **KHÔNG phải gửi yêu cầu để chủ nhóm phê duyệt từng khoản**.
   - Chủ nhóm có quyền thực hiện khoản chi theo vai trò chủ nhóm, không cần tự phê duyệt từng khoản của mình.
   - Cả chủ nhóm và thành viên đều **không có quyền sửa ledger, bỏ qua điều kiện chi hoặc che lịch sử**.
3. **Thuộc tính khoản chi:** Mỗi khoản chi phải có **số tiền, mục đích, người nhận, người thực hiện, thời điểm** và trạng thái. Hỗ trợ **đính kèm chứng từ/hình ảnh/hóa đơn** phù hợp, có tham chiếu được khi tra cứu. Quy tắc bắt buộc chứng từ cho mọi khoản chi hay loại chi nào chưa được người dùng ấn định, **không tự chặn tất cả giao dịch nếu thiếu ảnh**.
4. **Ghi nhận và thông báo:** Chi thành công thì ghi giao dịch tài chính bất biến, cập nhật số dư qua Backend và thông báo cho các thành viên. Lưu rõ kênh/đích chi đúng với API được phép, không bịa người nhận hoặc ghi thành công khi tiền chưa đi.
5. Sai sót phải có giao dịch điều chỉnh mới, có chứng cứ/lý do và liên kết giao dịch cũ; không sửa/xóa bút toán gốc.
6. **Kiểm tra an toàn bắt buộc:** Mọi khoản chi vẫn bắt buộc tuân thủ xác thực/PIN, số dư thực, nghĩa vụ góp (mục tiêu góp hoàn thành), tiền dư khóa, trạng thái hoạt động, giới hạn chi và tính nguyên tử ở Backend trong cùng thao tác ghi, kể cả khi có nhiều thao tác đồng thời. Bật `CanSpend` (ON) tuyệt đối không được bỏ qua các điều kiện an toàn này. **CHƯA CHỐT — BLOCKER:** Quy trình chuyển tiền ra khỏi quỹ/đích người nhận ngoài ví SpendWise nếu Backend/đối tác chưa có luồng hợp lệ.
7. **Khoản chi lớn phải chờ (v1.2 — ĐÃ CHỐT cơ chế):** khoản chi từ ngưỡng trở lên **không được thực hiện ngay** mà vào trạng thái **Chờ thực hiện** trong một khoảng thời gian; áp dụng cho mọi khoản chi, **kể cả khoản do chính chủ nhóm hoặc thành viên có CanSpend = ON thực hiện**. **THAM SỐ MẶC ĐỊNH:** ngưỡng = từ **20% số dư quỹ có thể chi** (cách tính ở câu sau); thời gian chờ **24 giờ**. Các khoản chi **của cả nhóm** trong 24 giờ liên tiếp được **cộng dồn** và so với số dư quỹ có thể chi ở đầu khoảng 24 giờ đó, không phân biệt người thực hiện hay người nhận, để không lách bằng cách chia nhỏ hoặc chia cho nhiều tài khoản. Ngưỡng tính bằng số nguyên đồng, làm tròn xuống. Khoản dưới ngưỡng (kể cả sau khi cộng dồn) vẫn thực hiện ngay như trước.
8. **Thông báo trước khi tiền đi (v1.2):** ngay khi khoản chi vào trạng thái Chờ thực hiện, hệ thống gửi thông báo cho **toàn bộ thành viên** gồm người thực hiện, số tiền, người nhận, mục đích và thời điểm dự kiến thực hiện. Thực hiện xong thì thông báo kết quả như điểm 4. Mọi thành viên đều xem được trạng thái này, không chỉ chủ nhóm.
9. **Nút “Báo cáo” khoản chi (v1.2):** trong thời gian Chờ thực hiện, **bất kỳ thành viên nào** của nhóm (không cần quyền chi) được báo cáo khoản chi kèm lý do. Khoản bị báo cáo chuyển sang **Tạm dừng**, không tự thực hiện khi hết thời gian chờ, đồng thời mở ticket Support gắn mã nhóm và mã khoản chi. Chỉ tạm dừng đúng khoản đó, không khóa các khoản khác hay phần còn lại của nhóm, và **không ai tự di chuyển tiền**; khoản Tạm dừng chỉ được thực hiện hoặc hủy theo kết quả xử lý của người có thẩm quyền theo tài liệu Admin riêng. Chủ nhóm hoặc người thực hiện khoản chi cũng có thể hủy khoản đang Chờ thực hiện hoặc đang Tạm dừng (hủy chỉ kết thúc khoản chi, không xóa lịch sử và báo cáo), để một khoản bị treo không chặn việc đóng hoạt động hay giải tán; mọi hành động đều ghi lịch sử. Báo cáo ác ý lặp lại xử lý qua Support.
10. **Giữ chỗ tiền (v1.2):** số tiền của khoản Chờ thực hiện/Tạm dừng được giữ chỗ (hold), không dùng cho khoản chi hoặc hoàn khác và không làm đổi quyền lợi tiền của từng người. Cách tính hoàn khi rời/kick lúc đang có tiền giữ chỗ thuộc BLOCKER mục 12 (điểm 3 và 10); không tự chọn công thức.

### 5.8. Rời nhóm, kick và hoàn tiền — nhóm loại 1

**ĐÃ CHỐT:** Hoàn tiền dựa trên **lịch sử chi thật của nhóm**, **không** dựa vào người đó có đi chơi/đi du lịch ngoài đời hay không.

| Tình trạng hoạt động/quỹ | Tiền hoàn cho thành viên đã góp khi rời hoặc bị kick |
|---|---|
| **Chưa phát sinh khoản chi nào** | **100% tiền đã góp hợp lệ chưa được hoàn** (cộng khoản quyền lợi chuyển tiếp nếu có, không hoàn trùng) |
| **Đã phát sinh khoản chi** | **Phần thuộc người đó theo tỷ lệ đóng góp trên số dư còn lại**, sau khi xử lý các khoản tiền được khóa riêng và đã hoàn theo nguyên tắc bảo toàn |

1. Người chưa góp và chưa chi có thể rời tự do. Người đã góp/chi **thường cần chủ nhóm duyệt nếu chủ động xin rời** (quy tắc trước đó). Chủ nhóm phải phản hồi trong thời hạn ở **mục 4.6 (v1.2)**; quá hạn thì không tự duyệt thay mà mở đường xử lý ở mục đó. **Ngoại lệ ĐÃ CHỐT:** Khi bầu lại chủ nhóm do không hoạt động và thành viên chọn rời (kể cả bầu thất bại), họ **không cần** chủ nhóm cũ duyệt; Backend tự hoàn đúng quyền lợi.
2. Chủ nhóm có thể kick theo thẩm quyền nhưng **không được chỉnh số tiền hoàn**, không được đánh dấu thành viên đã rời/kick trước khi hoàn hợp lệ, không được khiến tài khoản mất quyền nhận tiền. Người bị kick áp dụng **cùng nguyên tắc tính tiền** với người tự rời; Backend kiểm tra và thực thi, không có tùy chọn giữ lại tiền của chủ nhóm. **Ngoại lệ:** không kick chủ cũ chỉ vì bị giáng chức trong bầu cử trong lúc họ còn quyền lợi tiền chưa được giải quyết.
3. Thành viên là **chủ nhóm hiện tại** muốn rời thì phải chuyển quyền thành công trước cho thành viên khác; nếu chỉ còn một người thì cần quyết toán và giải tán đúng điều kiện, không thể tự bỏ nhóm vô chủ.
4. Ví dụ A/B mỗi người góp 1.000.000đ, đã chi 1.200.000đ, quỹ còn 800.000đ. A được duyệt rời (hoặc bị kick hợp lệ) nhận **400.000đ** từ 50% số dư; còn 400.000đ gắn phần quyền lợi còn lại của B. Nếu A là chủ nhóm, **chuyển quyền cho B trước khi A rời**. B được hoàn tất hoạt động để chia lại và giải tán, hoặc đề xuất hoạt động mới theo quy tắc; **không gán toàn bộ khoản đã chi của hoạt động cũ thành nghĩa vụ mới của B**.
5. Hoàn thành công trước khi rời/kick hoàn tất; tính lại thành viên và nghĩa vụ mới theo quyết định của chủ nhóm (nếu nhóm/hoạt động tiếp tục). Ghi sự kiện rời/kick ở **Lịch sử hoạt động nhóm**, giao dịch hoàn ở **Lịch sử giao dịch**, liên kết được hai bản ghi.
6. Không đánh dấu hoàn thành giả nếu chưa đủ số dư hoặc xung đột với phần tiền khóa của người khác. Công thức phân phối đủ mọi nhánh khi đã có chi, tồn tại khóa dư và rút/hoàn trước đó vẫn là **CHƯA CHỐT — BLOCKER** ở mục 12; không tự điều chỉnh bằng góp bù/bồi thường hoặc trả tiền không có trong quỹ.
7. Không hỗ trợ xác định ai có tham gia hoạt động ngoài đời, đền cọc, hủy vé hoặc xử lý tranh chấp bồi thường thay người dùng.

### 5.9. Hoàn thành hoạt động cũ

1. Chủ nhóm có quyền **“Hoàn thành hoạt động”** mà **không cần biểu quyết**, khi không còn yêu cầu chi đang xử lý và các khoản chi cần thiết của hoạt động đã được quyết toán.
2. Ghi chủ thể/thời điểm và **đóng khả năng tạo, duyệt, thực hiện chi mới** thuộc hoạt động cũ. Không có cơ chế chi tiếp dưới nhãn hoạt động đã hoàn thành.
3. Bảo toàn số dư; lựa chọn tiếp theo chỉ là **chia lại/giải tán theo quyền lợi**, hoặc **đề xuất hoạt động mới** để biểu quyết và chuyển quyền lợi theo quy tắc. Không tự chuyển tiền của người từ chối sang hoạt động mới.

### 5.10. Biểu quyết hoạt động mới — 24 giờ (KHÁC bầu lại chủ nhóm)

1. Sau khi đóng hoạt động cũ, chủ nhóm đề xuất hoạt động mới, tổng tiền dự kiến mới và gửi thông báo; thời hạn bỏ phiếu là **24 giờ**.
2. **Một thành viên có quyền hợp lệ = một phiếu**, bao gồm chủ nhóm; **đóng băng danh sách cử tri hợp lệ khi mở**; **trên 50% tổng số cử tri** chọn Đồng ý thì đề xuất thông qua; hòa hoặc không quá bán = không thông qua. Không trả lời không được xem là phiếu Đồng ý.
3. Nếu không đạt đa số, **không ai bị kick chỉ do không đồng ý**. Giữ nguyên nhóm/trạng thái và tiền, xử lý kế hoạch tiếp theo theo quyết định của chủ nhóm và quy tắc tài chính được phép.
4. Nếu được thông qua, chỉ những người **Đồng ý** được tính vào hoạt động mới; số dư cũ thuộc quyền lợi họ được chuyển đúng chủ thể và tính vào tiến trình góp mới. Người **Không đồng ý** hoặc không phản hồi được hoàn **phần thuộc mình**, không được chi tiền của họ cho hoạt động mới.
5. Người không tham gia vẫn tạm hiện trong danh sách nhóm để xử lý, **không tính vào `n` của hoạt động mới**, tiến trình mới = 0. Chỉ tự động kick **24 giờ sau kết thúc biểu quyết được thông qua VÀ sau khi hoàn tiền thành công**; nếu hoàn chưa thành công, không khởi động/hoàn tất kick. Chống retry tạo hoàn/kick lặp.
6. Người đã chọn không tham gia không được đổi ý để gia nhập **giữa chừng hoạt động mới**; sau khi out, nếu muốn quay lại phải được **mời và chấp nhận lại** như thành viên mới và chờ đợt hoạt động tiếp theo phù hợp. **Bỏ câu cũ “chờ hoạt động tiếp theo” khi họ vẫn còn tư cách thành viên.**
7. Chủ nhóm không được tự động bị kick khiến nhóm vô chủ. Nếu chủ nhóm thuộc diện rời/không tham gia, **cần chuyển chủ hợp lệ trước khi kick**; cách xử lý khi chủ nhóm không thể chuyển quyền là **BLOCKER** ở mục 12.
8. Lưu sự kiện mở/đóng biểu quyết, phiếu, kết quả, hoàn tiền, chuyển hoạt động, lịch đếm 24h và auto-kick ở lịch sử tương ứng; số dư đã chuyển không tạo thêm tiền hoặc thành phần thu nhập giả.
9. **Nhắc bỏ phiếu và nói rõ hậu quả im lặng (v1.2):** thông báo hoạt động mới phải ghi rõ **không phản hồi = không tham gia hoạt động mới** (được hoàn phần thuộc mình và có thể bị tự động kick theo điểm 5). Hệ thống gửi thông báo nhắc người chưa phản hồi trước khi biểu quyết kết thúc. **THAM SỐ MẶC ĐỊNH:** nhắc khi còn **6 giờ**. Thời hạn biểu quyết 24 giờ giữ nguyên (ĐÃ CHỐT).
10. Người bị tự động kick vì không phản hồi **không mất tiền** (hoàn đủ phần thuộc mình trước khi kick) và có thể được mời lại như thành viên mới. Cách xử lý này khác bầu lại chủ nhóm, nơi người im lặng không bị kick (mục 4.5.7), vì hai cuộc bỏ phiếu có mục đích và logic riêng.

**Ví dụ với 20 cử tri:** 15 đồng ý/5 không → thông qua; 10/10 → không; 8 đồng ý/7 không/5 im lặng → không; 12 đồng ý/5 không/3 im lặng → thông qua và 8 người không tiếp tục được hoàn theo quyền lợi.

### 5.11. Chuyển phần số dư còn lại sang hoạt động mới

1. Số dư còn lại không bị biến thành khoản thu mới. Hệ thống xác định quyền lợi tiền của những người đồng ý và tính **phần thuộc mỗi người** vào tiến trình góp hoạt động mới.
2. Không mặc định chia đều tiền thừa nếu quyền lợi/đóng góp thực tế không đều nhau; khoản thuộc người không tham gia phải được hoàn theo quy tắc.
3. Ví dụ: hoạt động trước còn 200.000đ; hai người đồng ý có quyền lợi mỗi người 100.000đ. Hoạt động mới có 5 người tham gia, tổng dự kiến 1.000.000đ → mỗi người phải góp 200.000đ: hai người thấy **100.000/200.000đ**, ba người còn lại thấy **0/200.000đ**.
4. Khoản tiền chuyển tiếp vẫn chịu **điều kiện khóa chi của hoạt động mới** cho đến khi hoàn thành mục tiêu góp theo quy tắc nhóm cố định.

### 5.12. Chia lại và giải tán loại 1

1. Chủ nhóm yêu cầu **“Chia lại”**. Chỉ giải tán khi đã hoàn xong các khoản theo quyền lợi và quỹ về **đúng 0đ**; không có khoản hoàn đang chờ/thất bại.
2. Tính các khoản dư **thực đang được bảo vệ riêng**, hoàn đúng người trước. Phần dư đã hấp thụ vào nghĩa vụ mới hoặc đã hoàn thì không hoàn lần hai.
3. Phần số dư còn lại sau khi giữ/hoàn dư khóa mới được chia theo **trọng số quyền lợi đóng góp ròng phù hợp với ledger**; không dùng trực tiếp tổng góp gốc đã bao gồm tiền đã hoàn, tiền đã rút hoặc tiền dư hoàn riêng. Công thức trọng số cho chuỗi nhiều đợt chi/chuyển tiếp/rời nhóm là **BLOCKER mục 12**, không tự mặc định.
4. Hoàn là giao dịch nội bộ về ví cá nhân được chọn (mặc định Ví chính), không hạch toán Income sinh hoạt. Ghi ledger và lịch sử bất biến, chống hoàn trùng.
5. Thông báo sau khi hoàn thành; nhóm sang trạng thái **Đã giải tán**, không tính 10 nhóm đang hoạt động, nhưng **mã nhóm, người sáng lập, lịch sử nhóm và lịch sử giao dịch vẫn được lưu**.

---

## 6. Loại 2 — Nhóm tự do (tham khảo Quỹ Nhóm MoMo)

1. **ĐÃ CHỐT:** Góp/chi linh hoạt như hướng Quỹ Nhóm MoMo, **tối đa 6 người** = 1 chủ + 5 thành viên, đồng thời mỗi User tối đa 10 nhóm hoạt động. Không áp tổng dự kiến `T`, nghĩa vụ góp cố định hay biểu quyết chuyển hoạt động như loại 1.
2. Chủ nhóm và thành viên có thể mời theo phạm vi MoMo được người dùng lựa chọn; **không giới hạn chỉ chủ nhóm được mời**. **Người nhận lời mời phải chấp nhận**, kiểm tra slot lúc chấp nhận; không ép tham gia.
3. Thành viên góp linh hoạt từ ví SpendWise; có yêu cầu chi/rút gửi chủ nhóm duyệt, chủ nhóm có thể tự chi theo quyền. **Không chi quá số dư**, mọi giao dịch phải có lịch sử và không tạo thành công giả.
4. **Một chủ nhóm duy nhất**, chuyển quyền tự nguyện và **bầu lại khi 15 ngày không sử dụng SpendWise** theo mục 4.5; tiền/quyền lợi của chủ cũ không mất khi bị thay chức.
5. **ĐÃ CHỐT khác MoMo:** Khi giải tán, số dư còn lại **phân bổ về ví cá nhân theo tỷ lệ đóng góp/quyền lợi ròng hợp lệ**, không tự trả hết cho chủ nhóm. Quỹ về **0đ** mới giải tán; lưu toàn bộ lịch sử.
6. Khi tính tỷ lệ phải **tách góp ban đầu, góp thêm, khoản rút cá nhân, hoàn đã trả và khoản chi chung**; không được đem khoản đã hoàn/rút để tính chia tiền lần nữa. **Trọng số ròng/công thức đầy đủ chưa CHỐT — BLOCKER**, không mặc định góp gộp hay một phép trừ đơn giản thiếu đối soát.
7. Thành viên bị kick hoặc chọn rời khi cuộc bầu lại chủ thất bại/vòng xác nhận: **không được mất quyền tiền**. Thao tác rời/kick và việc tính phần hoàn ở loại 2 còn thiếu quy tắc định lượng cụ thể ngoài nguyên tắc không âm, không trùng và bảo toàn tiền; xem mục 12. **Không tự áp công thức loại 1 cho loại 2.**
8. Các hành vi “giống MoMo” khác cần kiểm tra nguồn chức năng thực tế và xin duyệt, không sao chép cả giới hạn MoMo hoặc lấy tính năng không xác minh làm bắt buộc.

**Ví dụ chưa có rút cá nhân trước đó:** A góp 700.000đ, B góp 300.000đ, quỹ chi chung hợp lệ 600.000đ, còn 400.000đ ⇒ A nhận **280.000đ**, B nhận **120.000đ**; chỉ đúng cho trạng thái ví dụ này. Nếu trước đó có hoàn/rút cá nhân thì phải dùng trọng số ròng đã được đối soát, không nhân tỷ lệ 70/30 máy móc.

---

## 7. Tra cứu khiếu nại nhóm và lịch sử dành cho Admin (yêu cầu xuất phát từ User)

> Mục này chỉ định **dữ liệu User/Group phải lưu và nhu cầu hỗ trợ**, không tự thiết kế toàn bộ Admin Web hoặc Super Admin.

1. User có thể gửi **khiếu nại** hoặc **yêu cầu Admin rà soát toàn bộ nhóm mình từng tham gia**, không bắt buộc nhớ mã nhóm.
2. Nếu nhớ mã `SPW...`, User có thể cung cấp trong đơn để hỗ trợ tra cứu nhanh.
3. Admin có quyền phù hợp có thể tìm **đúng tài khoản User** để xem **toàn bộ nhóm từng tham gia**, kể cả nhóm User đã rời, bị kick hay nhóm đã giải tán.
4. Có thể tra cứu theo mã nhóm bất biến để xem trạng thái (đang hoạt động/đã giải tán; trạng thái bổ sung do Product Owner quyết định sau), chủ nhóm, thành viên, giao dịch và hoạt động.
5. Lịch sử cần giữ các thao tác: góp/chi/hoàn, thêm/mời/duyệt/rời/kick thành viên, bật/tắt quyền chi, duyệt/từ chối chi, thay đổi số dự kiến, biểu quyết, báo cáo/tạm dừng/hủy khoản chi chờ thực hiện, yêu cầu xử lý khi chủ nhóm quá hạn phản hồi và giải tán.
6. Không ai được sửa/xóa các bản ghi lịch sử nghiệp vụ đã phát sinh. Bản thân việc Admin tra cứu phải có phân quyền và ghi audit; biết mã nhóm không có nghĩa được truy cập nhóm.
7. **ĐÃ CHỐT MỚI:** User **được xem lại ở chế độ chỉ đọc** lịch sử liên quan đến **thời gian mình từng tham gia** nhóm, kể cả sau khi rời, bị kick hoặc nhóm giải tán. Không tự xem được giao dịch/thông tin riêng phát sinh sau khi họ không còn tư cách nếu không được cấp quyền; Admin được phân quyền vẫn có thể điều tra toàn bộ lịch sử. Đường khiếu nại qua Admin vẫn tồn tại.

## 8. Thông báo và hai lịch sử nhóm

**ĐÃ CHỐT:** Lịch sử hoạt động nhóm tách riêng lịch sử giao dịch như mục 4.4. Các sự kiện phi tài chính có người thực hiện + thời gian, các sự kiện tiền có số tiền/nguồn/đích và tham chiếu.

Các thông báo cần có: mời và nhận lời mời vào nhóm, người mới vào/rời/bị kick; thay đổi chủ nhóm; chủ nhóm không hoạt động 15 ngày và bắt đầu bầu lại; ứng viên và kết quả vòng 1, hòa/bầu phụ, xác nhận vòng 2; phát sinh quyền rời và mốc chờ 24h; góp, chi, hoàn, giao dịch bị từ chối; thay đổi T/số người; hoàn thành hoạt động; đề xuất hoạt động mới, kết quả biểu quyết 24h; giải tán. Thông báo lấy trạng thái đã xác nhận từ Backend, không thông báo chuyển tiền thành công khi giao dịch chưa thành công.

**Bổ sung v1.2:** khoản chi vào trạng thái Chờ thực hiện (gửi trước khi tiền đi, mục 5.7 điểm 8); khoản chi bị báo cáo/tạm dừng/hủy; chủ nhóm quá hạn phản hồi và việc mở bầu lại theo mục 4.6; nhắc bỏ phiếu hoạt động mới khi còn 6 giờ.

**Ví dụ lịch sử hoạt động (không nằm trong lịch sử giao dịch):**

- “A không hoạt động trên SpendWise trong 15 ngày; nhóm đã mở cuộc bầu chủ mới.”
- “B đã được xác nhận trở thành chủ nhóm; A trở thành thành viên.”
- “D vừa gia nhập nhóm chúng ta.”
- “D đã rời nhóm chúng ta.”

**Thông báo hoạt động mới (gợi ý câu chữ, chưa khóa nội dung UI):** “Chủ nhóm đề xuất hoạt động mới với tổng dự kiến [T]đ. Hoạt động trước còn [B]đ. Hãy xem quyền lợi và chọn Đồng ý/Không đồng ý trong 24 giờ. Người không tiếp tục được hoàn phần thuộc mình trước khi rời nhóm theo quy tắc.”

**Lưu ý:** Khi bầu **chủ nhóm**, hai vòng và 24 giờ chờ rời có logic khác với cuộc biểu quyết 24 giờ **hoạt động mới**; không dùng chung một vòng bỏ phiếu hay cùng bộ trạng thái nghiệp vụ.

---

## 9. Tính tiền, làm tròn và bảo toàn quỹ

### 9.1. Biểu diễn tiền

- Mọi số tiền lưu/tính bằng **số nguyên đồng Việt Nam**, không lưu khoản tiền nghiệp vụ dưới dạng floating-point.
- UI được phép hiển thị rút gọn (ví dụ `1tr52`) để đọc nhanh, nhưng **chi tiết và xác nhận giao dịch dùng số tiền chính xác**.
- Phép tính trung gian nhân tỷ lệ phải dùng loại số/thuật toán không bị overflow và chia số nguyên một cách xác định.

### 9.2. Chia đều tổng tiền dự kiến

Với tổng dự kiến nguyên đồng `T`, `n` người tham gia nghĩa vụ góp:

- `q = floor(T / n)`.
- `r = T mod n`.
- `r` người đầu theo **thứ tự tham gia ổn định** phải góp `q + 1` đồng/người; số còn lại góp `q` đồng/người.
- Tổng mọi nghĩa vụ góp **bằng đúng T**, không dùng một giá trị hiển thị đã làm tròn để nhân ngược lại.
- Mỗi lần chủ nhóm chấp thuận cấu hình dự kiến mới sau thay đổi thành viên, **tính lại nghĩa vụ từ đầu** theo `T`, `n` mới mà không thay đổi lịch sử tiền thực góp.

**Ví dụ 10.000.000đ cho 19 người:** 15 người × 526.316đ + 4 người × 526.315đ = **10.000.000đ**.

**Ví dụ 20.000.000đ cho 19 người:** 11 người × 1.052.632đ + 8 người × 1.052.631đ = **20.000.000đ**.

**Bổ sung v1.2 — “ghế đồng dư” cố định (ĐÃ CHỐT, vá lỗ hổng lệch 1đ):**

- Vị trí nhận `q + 1` đồng gắn với **ghế** (số thứ tự tham gia), không gắn với người. Khi một người rời, ghế của họ **không bị dồn lại**; người mới vào lấp ghế trống có số thứ tự nhỏ nhất và kế thừa mức góp của ghế đó. `r` ghế đầu (chỉ tính ghế đang có người, theo số thứ tự ghế) nhận `q + 1`.
- Nếu `T` và số người `n` **không đổi** sau một thay đổi thành viên, nghĩa vụ góp của mọi người ở lại **không đổi**. Không được để người đã đạt đủ nghĩa vụ rơi về “chưa hoàn thành” chỉ vì thứ tự dịch chuyển, vì như vậy có thể khóa chi cả nhóm chỉ vì 1đ (xem mục 5.4.3).
- Khi `T` hoặc `n` đổi, tính lại từ đầu như các quy tắc trên, vẫn theo thứ tự ghế.

**Ví dụ (T = 10.000.000đ, n = 19):** ghế 1–15 góp 526.316đ, ghế 16–19 góp 526.315đ. Người ở ghế 16 đã góp 526.315đ nên đủ. Người ở ghế 3 rời, một người mới vào lấp ghế 3; `T` và `n` không đổi nên người ở ghế 16 vẫn đủ, người mới góp 526.316đ.

### 9.3. Chia số dư theo quyền lợi góp — nguyên tắc, chưa phải toàn bộ công thức nghiệp vụ

1. **ĐÃ CHỐT:** Trả khoản **dư khóa còn thực** đúng người trước; phần còn lại chia theo **quyền lợi đóng góp còn hiệu lực** sau khi đối soát các khoản góp/chi/rút/hoàn/chuyển kỳ. **Không lấy nguyên tổng góp gốc** làm trọng số nếu đã có rút/hoàn hoặc đã trả dư khóa; không đếm tiền hai lần.
2. Với tổng số dư thực sự cần chia `B` và trọng số hợp lệ `w_i` đã được xác định từ ledger, mỗi người nhận phần nguyên `floor(B * w_i / sum(w))`, sau đó rải đồng dư theo phần dư lớn nhất, hòa thì theo thứ tự ổn định. Phải dùng số học nguyên không tràn, kết quả tổng bằng đúng `B`.
3. **Ví dụ trọng số đã được quyết toán** `3:2:1` với 1.000đ ⇒ **500đ, 333đ, 167đ**.
4. **CHƯA CHỐT — BLOCKER:** Công thức xác định `w_i` **khi đã có nhiều lần thay T, chi, thành viên rời/kick, chuyển hoạt động, khoản rút cá nhân ở nhóm loại 2**. Thuật toán rải đồng chỉ giải quyết làm tròn, **không** giải quyết quyền hưởng tiền. Không tự đồng nhất `w_i` với `totalContributed` hoặc đơn thuần `totalContributed - withdrawals` nếu chưa chứng minh ledger bảo toàn đầy đủ.

### 9.4. Điều kiện bất biến tài chính

- Không được ghi thành công nếu số dư/điều kiện chưa được Backend xác nhận.
- Không chi hoặc hoàn vượt số tiền thực sự khả dụng.
- Không dùng tiền đang khóa của người khác để chi hay hoàn sai đối tượng; trước khi kick/rời phải đối soát quyền tiền và hoàn hợp lệ, chủ nhóm không được tự chỉnh số hoàn.
- Không cho hai yêu cầu cùng lúc chi vượt số dư; thao tác tài chính phải được xử lý nguyên tử, chống retry tạo giao dịch trùng.
- Sổ tiền phải có bút toán phù hợp, cân bằng và truy vết; không tự bù các khoản Ledger lịch sử đang bất thường.
- Không cho giải tán khi quỹ khác **0đ**, hoặc công bố hoàn tất trong khi tiền hoàn còn chờ/thất bại.
- Hệ thống giữ lịch sử bản ghi gốc, mọi chỉnh sai phải có bản ghi điều chỉnh truy vết được. Mọi chuyển quyền chủ nhóm phải bảo đảm **đúng một chủ** và độc lập với quyền nhận tiền của từng người.
- **(v1.2)** Số tiền của khoản chi **Chờ thực hiện/Tạm dừng** được giữ chỗ và không dùng hai lần; khoản bị Tạm dừng không được tự thực hiện khi hết thời gian chờ.

---

## 10. Trải nghiệm Mobile và tiêu chí nghiệm thu sản phẩm

### 10.1. Mục tiêu giao diện

- Một ứng dụng SpendWise thống nhất thương hiệu, màu sắc, typography và cách gọi nghiệp vụ; **không thu nhỏ nguyên bản Desktop Web**.
- Bố cục rõ cấp bậc; số dư, trạng thái, tiến độ, nút hành động quan trọng phải dễ thấy, dễ chạm.
- Không quá tải màn hình với nhiều biểu đồ, hiệu ứng hoặc thẻ lặp.
- Loading, không có dữ liệu, lỗi mạng, giao dịch thất bại và thành công phải dễ phân biệt; accessibility cơ bản, safe-area, keyboard và light/dark theo phạm vi hiện có.
- Mobile ưu tiên thực hiện trơn tru các hành trình: xem tiền → ghi thu/chi → quản lý ví → chuyển tiền → quản lý nhóm, bầu/chuyển chủ và lịch sử → hỗ trợ khi cần.

### 10.2. Điều kiện để đánh dấu một chức năng hoàn thiện

1. Hành vi phù hợp nghiệp vụ tại tài liệu này và API contract đã đối chiếu.
2. Dữ liệu/thay đổi tiền lấy từ Backend thật, không lấy state giả thay cho kết quả.
3. Quyền User/chủ nhóm/thành viên được server kiểm tra đúng.
4. Trạng thái lỗi, retry, đồng thời và ranh giới số dư được xử lý an toàn.
5. UI được duyệt từng màn hình; kiểm thử nhiều tài khoản với các ca nhóm/quỹ quan trọng.
6. Không tự mở rộng chức năng, role hoặc API chỉ vì agent thấy hợp lý. **SePay TestMode:** chỉ tích hợp luồng chuyển hai chiều ví ↔ ngân hàng/SePay nếu **API/Backend hiện có** thực sự hỗ trợ luồng đó và được kiểm tra qua contract/test; nếu chưa có, **không tạo tính năng rút tiền ngân hàng mới** hoặc giả lập thành giao dịch thực.

## 11. Phân biệt quyết định sản phẩm và trạng thái code hiện có

- **Đã được Product Owner phê duyệt:** Phạm vi User Mobile, hai loại nhóm, công thức chia đồng dư, quyền một chủ nhóm và bầu lại, hoàn khi rời/kick theo nguyên tắc đã chốt, biểu quyết hoạt động mới, lịch sử riêng, tiền dư khóa và ngoại lệ chia quỹ loại 2. **Không được xem công thức trọng số ròng khi nhiều giao dịch là đã được duyệt**; xem BLOCKER mục 12.
- **Bổ sung v1.2 (theo yêu cầu của Product Owner):** cơ chế chi lớn phải chờ và nút Báo cáo, thời hạn phản hồi của chủ nhóm, ghế đồng dư cố định, nhắc bỏ phiếu. Các con số đi kèm là `THAM SỐ MẶC ĐỊNH`, chưa phải số liệu đã khóa.
- **Chưa được xác nhận là đã triển khai:** Nhiều quy tắc nhóm mới có thể chưa tồn tại trong Backend/Mobile hiện hành. Agent phải so sánh source, contract, DB và test trước khi lập kế hoạch sửa.
- **Đã có phạm vi chức năng từ trước nhưng mức độ triển khai cần kiểm chứng:** Auth, Wallet, Transactions, Transfer, Bank/SePay, Budget, Goals, Recurring, Categories, Friends, Notifications, Support/Disputes, AI, Settings/Sessions.
- **Khoản nợ kỹ thuật liên quan:** Ledger lịch sử từng có giao dịch thiếu posting; không được dùng dữ liệu lịch sử đó để tuyên bố financial correctness đã PASS.
- **Web User cũ** là nguồn tham khảo lịch sử chứ không phải ứng dụng User cần tiếp tục phát triển.

## 12. Những lỗ hổng còn lại — CHƯA CHỐT, KHÔNG CHE GIẤU (BLOCKER)

**Có thể duyệt phạm vi User và dùng tài liệu để kiểm kê/gap-analysis ngay. Không được tuyên bố nghiệp vụ tiền và bầu cử đã đủ chi tiết để implement end-to-end cho đến khi các BLOCKER liên quan được giải quyết.** Các điểm sau không phải đề xuất thêm module:

1. **[P0 — TIỀN] Công thức quyền lợi ròng nhiều sự kiện:** xác định số tiền gốc, tiền đã phân bổ vào chi phí chung, tiền đã rút/hoàn, tiền dư khóa thực còn, tiền chuyển kỳ, **trọng số `w_i`** và thứ tự thanh toán rời/kick/giải tán để tổng ledger bảo toàn và không hoàn trùng. Nhóm loại 2 đặc biệt phải phân biệt chi chung với rút về cá nhân. Cần bảng ca kiểm thử có số tiền cụ thể trước khi agent triển khai.
2. **[P0 — TIỀN] Thay thành viên sau khi đã chi:** thay `T`, số người và nghĩa vụ góp của những người ở lại sao cho không biến chi phí cũ thành khoản góp mới hoặc sinh khoản phải trả vô lý; ví dụ A/B đã chi 1.200.000đ, A hoàn 400.000đ, B còn 400.000đ. Khi B muốn kết thúc/giải tán thì cho thực hiện đúng phần quyền lợi; khi B chọn mở hoạt động mới, cần kế toán per-activity chứ không tái tính như chưa chi. Chủ nhóm vẫn có quyền **tự quyết** T trong ràng buộc bảo toàn, không tạo phiếu biểu quyết mức góp.
3. **[P0 — TIỀN] Rời/kick và tiền khóa:** nếu một yêu cầu hoàn hợp lệ va chạm khoản dư riêng người khác, khoản hoàn đang xử lý, hold hoặc số dư không đủ, cần chính sách trạng thái/chờ hoặc đóng hoạt động — **không âm quỹ, không giữ tiền, không tự yêu cầu người khác góp bù**. Việc cho phép người đã góp **tự rời trong trường hợp thông thường khi chưa chi** vẫn theo quy tắc cần chủ duyệt của v1.0; ngoại lệ bầu lại chủ đã chốt rời tự do.
4. **[P0 — TIỀN] Nghiệp vụ nhóm loại 2 khi rời/kick:** điều kiện rời, rút, hoàn, phân phối khi giải tán **sau nhiều giao dịch rút cá nhân**, bảo đảm không đếm lại đóng góp gộp. Chưa có công thức đã duyệt; không tự sao chép toàn bộ hành vi MoMo thiếu nguồn.
5. **[P0 — CHỦ NHÓM] Tiến trình bầu lại và trường hợp biên:** thời lượng và hết hạn đề cử/vòng 1/bầu phụ/vòng 2; trường hợp ứng viên thắng không nhận quyền, không có ứng viên, không có người bỏ phiếu; cách xử lý phiếu/rời/khoản chi phát sinh trong lúc bầu. **Chốt rồi:** >50% tổng cử tri vòng xác nhận; nếu hòa cao nhất thì một bầu phụ, hòa tiếp thì thất bại; 24 giờ là mốc chờ rời **không tự là độ dài vòng bỏ phiếu**.
6. **[P0 — CHỦ NHÓM] Chủ hiện tại từ chối hoạt động mới/được lên lịch auto-kick, hoặc chủ cuối cùng muốn rời:** không cho nhóm vô chủ hay giao quyền quản lý tiền cho tài khoản bị khóa. Quy tắc chuyển quyền trước khi rời và ngoại lệ nhóm một người đã chốt; chi tiết chuyển giữa các trạng thái cần kiểm thử và xử lý tranh chấp khi không có thành viên đủ điều kiện nhận quyền.
7. **[P0 — BANKING/PHÁP LÝ] Nguồn tiền và đường ra:** ví SpendWise là số dư sổ nội bộ, hay khoản tiền thật do tổ chức/đối tác được cấp phép quản lý? Phân biệt nạp TestMode đã được chứng minh với payout/rút về ngân hàng chưa được chứng minh. Không triển khai rút ngân hàng không có contract/đối tác hợp lệ; không coi TestMode là tiền thật. Với tiền thật cần kiểm tra pháp lý và lưu ký/đối tác trước khi thương mại hóa. **(v1.2) Điều kiện vận hành:** khi chưa có xác nhận pháp lý/đối tác, chỉ chạy TestMode; không nhận hay hứa hoàn tiền thật của người dùng.
8. **[P1 — ĐỒNG THỜI]** Nhận hai lời mời cùng lúc khi gần đủ 10 nhóm/30 hoặc 6 người; khóa danh sách phiếu, idempotency việc hoàn/kick/bầu chủ, tính mốc 15 ngày từ hoạt động thực có xác thực; xử lý khoản chi đang treo khi chủ bị khóa.
9. **[P1 — KIỂM KÊ]** Cách hiển thị bạn chung nhiều nhóm, giới hạn mã `SPW` sau 999999, tỷ lệ tài khoản mất quyền truy cập trong 15 ngày và nhật ký bảo vệ thông tin, khác biệt giữa đặc tả và Backend/Mobile hiện tại phải được báo cáo bằng chứng nguồn trước khi sửa.

10. **[P1 — CHI TIÊU] Tham số giữ chờ và báo cáo (v1.2):** ngưỡng khoản chi lớn, thời gian chờ, cách cộng dồn khi chia nhỏ; thời hạn xử lý khoản Tạm dừng của Admin (nếu chậm thì khoản bị treo bao lâu); cách tính hoàn/rời/kick khi đang có tiền giữ chỗ; chống báo cáo ác ý. Số liệu ở mục 5.7 chỉ là `THAM SỐ MẶC ĐỊNH` để chạy thử.
11. **[P1 — CHỦ NHÓM] Bầu lại do chủ nhóm không phản hồi (v1.2):** thời hạn 7 ngày đã phù hợp chưa; xử lý các yêu cầu đang chờ trong lúc bầu; nhóm chỉ có hai người (chỉ một cử tri); chống lạm dụng nút “Yêu cầu xử lý”. Thuộc cùng bộ mốc chưa chốt ở điểm 5.

**Cấm:** Lấy giả định của AI/Codex/Antigravity làm quyết định Product Owner; bỏ điều kiện hoàn tiền; tự sửa tiền trong database; tự chuyển tiền giữa thành viên; hoặc lén đổi mô hình nhóm khi xử lý BLOCKER.

---

## 13. Chỉ thị cho Codex và Antigravity khi sử dụng `User.md`

**ĐƯỢC PHÉP:** đọc tài liệu; kiểm kê nguồn code và API; lập gap report, đánh dấu điều đã có/chưa có/mâu thuẫn; đưa bằng chứng, rủi ro, đề xuất để chủ dự án duyệt. Chỉ được triển khai những batch được giao rõ ràng và phê duyệt riêng.

**KHÔNG ĐƯỢC PHÉP:** tự thêm Money Request, làm một nhóm thứ ba, tự sửa/đổi quyền và công thức, tự suy diễn mọi tính năng của MoMo, đổi phạm vi Admin, xóa Backend/API cũ không được giao, sửa Ledger hoặc dữ liệu SQL thật, tạo giao dịch tiền giả, hoặc tự redesign toàn bộ Mobile.

**Ưu tiên đối chiếu:** Quyết định mới của Product Owner trong **v1.2 này (kể cả phần v1.1 được giữ nguyên)** > `User.md` v1.0 > mô tả demo Web User cũ. Những chỗ gắn **CHƯA CHỐT — BLOCKER** không tạo quyền triển khai; cần báo cáo bằng chứng, ví dụ số học và xin duyệt trước khi sửa Backend/API/DB. Mọi thay đổi được thực hiện thành batch riêng có kiểm thử, không tự phá hợp đồng hay dữ liệu hiện hữu.

---

**KẾT LUẬN:** SpendWise Mobile giúp User quản lý thu chi cá nhân và quản lý quỹ chung. **Nhóm góp cố định** là điểm khác biệt, **nhóm tự do** tham khảo MoMo với tối đa 6 thành viên và nguyên tắc chia số dư theo quyền lợi góp hợp lệ. Mỗi nhóm có **một chủ nhóm hiện tại**, cơ chế chuyển quyền/bầu lại sau 15 ngày không sử dụng app; các khoản tiền không được mất khi thay chủ/kick/rời. **Tài liệu v1.2 (giữ nguyên v1.1, chỉ bổ sung vá lỗ hổng) chốt phạm vi sản phẩm nhưng chưa khóa toàn bộ hợp đồng tính tiền và các thời hạn bầu cử**; các BLOCKER mục 12 phải được giải quyết trước khi viết code nghiệp vụ liên quan.

---

## PHỤ LỤC A — Changelog v1.0 → v1.1 và nguyên tắc không hồi quy

1. **LOẠI BỎ VĨNH VIỄN KHỎI PHẠM VI USER:** Money Request; không biến yêu cầu chi quỹ nhóm thành Money Request giữa hai User.
2. **Sửa sai quyền quản lý:** Chủ nhóm loại 1 có quyền tự giữ/tăng/giảm T trong điều kiện an toàn Backend; không yêu cầu 100% thành viên đồng ý như phần giải thích sai trước đó.
3. **Một chủ nhóm duy nhất, chuyển quyền và bầu lại:** Người sáng lập bất biến trong lịch sử, chủ hiện tại chuyển được; 15 ngày không sử dụng thực sự → mở bầu lại hai vòng; hòa → bầu phụ một lần; hòa tiếp → thất bại; vòng xác nhận >50% tổng cử tri hợp lệ; người chọn Không hoặc bầu thất bại có quyền chọn rời; 24h chờ rời và chỉ out khi hoàn đúng tiền; muốn quay lại phải được mời lại.
4. **Quỹ và chủ cũ:** Người bị thay quyền thành mem, không mất tiền; khôi phục tài khoản không tự lấy lại chức; một người còn lại vẫn giữ nhóm và quyền tiền, khóa tài khoản vẫn chặn thao tác.
5. **Hai kiểu khóa:** Dư cá nhân khóa riêng khác điều kiện khóa chi do nhóm chưa góp đủ. Sửa trường hợp 1.000.000/1.200.000: thiếu 200.000đ chứ không còn dư 200.000đ.
6. **Đóng hoạt động là đóng chi:** Không dùng số dư cũ để chi tiếp, chỉ quyết toán/chia lại/chuyển hoạt động qua biểu quyết.
7. **Lời mời phải chấp nhận; khoản chi truy vết được:** Không ép slot; ghi rõ mục đích/người nhận; hỗ trợ chứng từ.
8. **Kick loại 1 bắt buộc hoàn theo Backend:** Chưa chi hoàn 100%; đã chi theo quyền lợi tỷ lệ số dư. Chủ nhóm không có quyền thay công thức/đổi tiền hoàn.
9. **Lịch sử nhóm tách lịch sử giao dịch; người rời xem lại chỉ đọc phần từng tham gia.**
10. **Banking/SePay TestMode:** Chỉ làm rút/chuyển hai chiều nếu chứng minh Backend và SePay/đối tác hiện hỗ trợ; không tự mở rộng hoặc giả tiền thật.
11. **BLOCKER tồn đọng ghi thật:** Công thức quyền lợi ròng phức tạp, nghiệp vụ rút/rời loại 2, mốc mỗi vòng bầu, người nhận chức và một số ca khóa/đóng hoạt động. Không được đánh dấu PASS/đã phê duyệt công thức khi thiếu.

---

## PHỤ LỤC B — Changelog v1.1 → v1.2 (chỉ bổ sung, không xóa quyết định cũ)

1. **Chi lớn phải chờ, thông báo trước khi tiền đi, nút Báo cáo, giữ chỗ tiền** (5.7 điểm 7–10, 9.4): vá việc chủ nhóm tự duyệt và rút sạch quỹ mà thành viên chỉ biết sau khi tiền đã đi.
2. **Thời hạn phản hồi của chủ nhóm và bầu lại khi không phản hồi** (4.5 điểm 11, 4.6, 5.8.1): vá việc chủ nhóm còn mở app nhưng giữ quyết định vô thời hạn.
3. **Ghế đồng dư cố định** (9.2): vá lệch 1đ khiến người đã đủ bị coi là thiếu và khóa chi cả nhóm.
4. **Nhắc bỏ phiếu và nói rõ hậu quả im lặng** ở biểu quyết hoạt động mới (5.10 điểm 9–10); thời hạn 24 giờ không đổi.
5. **Điều kiện vận hành tiền thật** (12.7) và hai BLOCKER mới (12 điểm 10–11).
6. **Không thay đổi:** hai loại nhóm, một chủ nhóm và bầu lại 15 ngày, hai kiểu khóa, đóng hoạt động, kick hoàn tiền, lịch sử tách đôi, mã nhóm SPW, giới hạn 10 nhóm/100 bạn, loại bỏ Money Request và toàn bộ BLOCKER cũ.
7. **Tham số mặc định chờ Product Owner xác nhận:** ngưỡng 20% số dư quỹ, thời gian chờ 24 giờ, hạn phản hồi 7 ngày, nhắc bỏ phiếu khi còn 6 giờ.

---

## PHỤ LỤC C — Changelog v1.2 → v1.3: Quyền chi trực tiếp nhóm Fixed (CanSpend)

1. **Quyết định Product Owner mới nhất (Thay thế cơ chế yêu cầu/duyệt từng khoản ở nhóm Fixed):**
   - Chủ nhóm có quyền bật/tắt `CanSpend` (ON/OFF) cho từng thành viên (mặc định thành viên mới là **OFF**).
   - **OFF:** Thành viên không được trực tiếp chi tiền từ quỹ.
   - **ON:** Thành viên được **trực tiếp thực hiện khoản chi**, **KHÔNG phải gửi yêu cầu để chủ nhóm phê duyệt từng khoản**.
   - Chủ nhóm thực hiện chi trực tiếp theo vai trò, không cần tự phê duyệt từng khoản của mình.
2. **Bảo toàn các điều kiện an toàn và kiểm soát rủi ro:**
   - Mọi khoản chi trực tiếp vẫn bắt buộc tuân thủ PIN/xác thực, số dư thực tế, mục tiêu góp đã hoàn thành, kiểm tra tiền khóa, trạng thái hoạt động và tính nguyên tử ở Backend.
   - Khoản chi lớn vẫn chịu ngưỡng 20%, cộng dồn 24 giờ, thời gian chờ 24 giờ, thông báo trước cho toàn nhóm và nút Báo cáo/Tạm dừng của thành viên. Trạng thái ON không được bỏ qua các điều kiện này.
3. **Nhóm loại 2 (Free):** Giữ nguyên cơ chế thành viên gửi yêu cầu chi/rút để chủ nhóm duyệt.


---

## PHỤ LỤC D — Changelog v1.3 → v1.4: Triển khai 4 quyết định nghiệp vụ đã được Product Owner phê duyệt chính thức (Task 16.1)

1. **Quyết định 1 — Fixed Fund Dispute Resolution (Xử lý khiếu nại khoản chi lớn nhóm Cố định):**
   - Thành viên bấm Báo cáo khoản chi lớn trong giai đoạn giữ chờ `Pending24h` tạo `SupportCase` (`Category = "V1FundDispute"`, `LinkedTransactionId = item.Id`), khoản chi chuyển sang trạng thái `Suspended`.
   - Endpoint Admin: `POST /api/v1/admin/support/disputes/{requestId}/resolve`.
   - Khi Admin xác định báo cáo đúng (`!IsRejected`): khoản chi chuyển sang `Cancelled`, số tiền đang hold được giải phóng trả lại số dư khả dụng của quỹ, `SupportCase` chuyển `Resolved`, ghi audit và gửi thông báo cho toàn nhóm.
   - Khi Admin xác định báo cáo sai (`IsRejected`): khoản chi chuyển lại `Pending24h` với thời hạn gốc được giữ nguyên (không làm mới chu kỳ 24h), `SupportCase` chuyển `Rejected`, ghi audit và thông báo cho thành viên.

2. **Quyết định 2 — Hai cấp độ hạn chế tài khoản Admin (Level 1 Restricted vs Level 2 Locked):**
   - **Level 1 — Hạn chế tài chính (`UserStatus.Restricted`):** Chặn mọi luồng ghi nợ xuất tiền (chuyển tiền P2P, chuyển ví nội bộ, tạo chi tiêu cá nhân, tạo chi tiêu nhiều ví, đóng góp quỹ nhóm, tạo yêu cầu rút/chi tiền quỹ). Tài khoản VẪN được đăng nhập, refresh token, xem số dư/lịch sử/profile, gửi khiếu nại/support, và nhận tiền chuyển đến/hoàn tiền.
   - **Level 2 — Khóa tài khoản (`UserStatus.Locked`):** Từ chối đăng nhập (403 `account_unavailable`), thu hồi toàn bộ phiên hoạt động ngay lập tức, từ chối refresh token.

3. **Quyết định 3 — Rời nhóm, hoàn tiền và quyền lợi khi gia nhập lại (Rejoin Entitlements):**
   - Thành viên đã rời nhóm và nhận hoàn tiền/quyết toán khi được mời lại sẽ bắt đầu một phiên thành viên hoàn toàn mới (`m.Id` mới, `m.JoinedAt` mới).
   - Quyền lợi hoàn tiền của phiên mới khi chưa có đóng góp mới là chính xác `0 VNĐ` (`NoContribution`).
   - Công thức hoàn tiền thống nhất ở cả nhóm Cố định (Fixed) và nhóm Tự do (Free) giới hạn các khoản góp theo `c.MembershipId == m.Id` và các khoản chi/rút/hoàn trước đó theo `CreatedAt >= m.JoinedAt`.

4. **Quyết định 4 — Bầu cử chủ nhóm và quản trị (Owner Election & Governance):**
   - Tham số mặc định được phê duyệt: Đăng ký ứng viên 24h, Đề cử vòng 1 48h, Bầu phụ 24h, Xác nhận vòng 2 24h, Chấp nhận quyền 24h; đóng băng thành viên (`FreezeMembership = true`), không cho góp thêm (`AllowContributions = false`), kết quả khi hòa lần 2 là thất bại (`Outcomes = Fail`).
   - Người thắng cử có quyền từ chối tiếp nhận quyền chủ nhóm qua `POST /api/v1/funds/elections/{id}/reject`. Khi từ chối, bầu cử kết thúc thất bại với lý do `WinnerRejected`, chủ nhóm danh nghĩa cũ vẫn được giữ, thành viên có quyền rời nhóm và nhận hoàn tiền.
   - Thành viên bị khóa không được bỏ phiếu và không được nhận quyền chủ nhóm. Cử tri đoàn được chốt tại thời điểm mở bầu cử.
   - Tạm dừng mọi thao tác tài chính (đóng góp, yêu cầu chi/rút) và lời mời mới trong suốt thời gian diễn ra bầu cử (`election_finance_paused`).

---

## PHỤ LỤC E — Kết quả kiểm tra độc lập và báo cáo nghiệm thu Task 16.2 (Adversarial Audit & Final Fixes)

1. **Khắc phục lõi tài chính khi giải thể quỹ (Zero-Entitlement Dissolution Invariant):**
   - Đã sửa cơ chế chia đều (equal split) và phân bổ tiền lẻ 1 VNĐ trong `V1FundRefunds.cs` để chỉ tính trên các thành viên có quyền lợi dương (`hasEntitlement`). Ngăn chặn tuyệt đối nguy cơ thành viên không có đóng góp/quyền lợi bị nhận nhầm tiền lẻ khi quỹ giải thể.
2. **Khắc phục tính nguyên tử và phân quyền khi xử lý khiếu nại (Dispute Resolution Operator Auto-Assignment):**
   - Đã bổ sung cơ chế tự động gán Admin (`ticket.AssignedAdminId = actorId`) và chuyển case sang `Investigating` trong `AdminA5Service.ResolveDisputeRequestAsync` nếu phiếu khiếu nại chưa có người tiếp nhận, đảm bảo quy trình phê duyệt khiếu nại khoản chi lớn luôn thành công nguyên tử theo đúng phân quyền của Admin hệ thống.
3. **Kết quả kiểm chứng:**
   - **Release Build:** 0 lỗi (0 errors, 16 warnings).
   - **Full Non-SQL Test Suite:** 1.063 / 1.063 test PASS (100% xanh, bao gồm 6 audit unit tests chuyên biệt trong `Task16_2_AdversarialAuditTests.cs` và 7 test nghiệp vụ trong `Task16_1_ApprovedDecisionsTests.cs`).
   - **SQL02 Integration Test Suite:** 179 / 179 test PASS trên instance cơ sở dữ liệu thực tế SQL02 `SpendWise_Test_Runner`.
   - **Full Newman E2E Suite:** 105 / 105 requests PASS, 177 / 177 assertions PASS (100% xanh trên database `SpendWise_E2E_Test`).

---

## PHỤ LỤC F — Ổn định hóa Core Backend & Đánh giá năng lực SePay (Task 16.3)

1. **Tính nguyên tử và chốt trạng thái giải quyết khiếu nại (Dispute Transaction Atomicity):**
   - Tự gán quyền xử lý và giải quyết khiếu nại được gom chung vào một transaction duy nhất trong `workflow.WithCaseAsync`.
   - Bắt buộc khoản chi bị khiếu nại phải ở trạng thái `Suspended` mới được xử lý; nếu không lập tức throw `ConflictException("disputed_request_not_suspended")`. Khi có lỗi, toàn bộ transaction rollback hoàn toàn (không lưu Admin gán dở dang).
2. **Cách ly email kiểm thử (Email Test Isolation):**
   - `ConfiguredEmailSender.cs` chặn tuyệt đối việc gửi email test ra internet; các domain `.test`, `.invalid` được lưu vào thư mục pickup nội bộ hoặc ném exception nếu cố tình dùng SMTP. Môi trường `Testing` luôn cưỡng chế chế độ `Pickup`.
3. **Nghiệm thu năng lực tích hợp SePay:**
   - **Liên kết ngân hàng:** Đã hỗ trợ qua SePay Hosted Link Hub và webhook.
   - **Nạp ví (Inbound):** Đã hỗ trợ qua Webhook IPN (`transfer_type=in`), xác thực chữ ký và chống trùng lặp.
   - **Rút tiền (Outbound / Payout):** Cổng SePay Bank Hub cho tài khoản cá nhân KHÔNG cung cấp API tự động chi tiền ra ngoài (yêu cầu hợp đồng doanh nghiệp kết nối trực tiếp với ngân hàng đối tác như TPBank/VietinBank Corporate). Trong V1.0, Backend xử lý giữ tiền và trừ sổ cái an toàn; luồng chi tiền tự động ra ngoài được phân loại là DEFERRED tích hợp đối tác sau.
4. **Bảo đảm an toàn dữ liệu:**
   - Tuyệt đối không chạm vào SQL01 (`DESKTOP-B5KA3LT\SQLEXPRESS01\SpendWiseDb`). Mọi test chạy trên SQL02 (`DESKTOP-B5KA3LT\SQLEXPRESS02`). Không can thiệp Frontend/Mobile.
