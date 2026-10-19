# SPENDWISE — SUPER ADMIN (WEB) — PRODUCT SPECIFICATION

> **Sản phẩm:** SpendWise V1.0 (bản phát hành ứng dụng đầu tiên)  
> **Tệp:** `SuperAdmin.md` · **Ngày chốt phạm vi:** 18/10/2026  
> **Trạng thái:** **PRODUCT SCOPE ĐÃ ĐƯỢC CHỦ DỰ ÁN DUYỆT** — 7 menu chính, 42 chức năng con và các quy tắc S7. **Không chứng minh hiện đã có Backend/UI/API và không tự cấp quyền sửa code.**  
> **Đối tượng:** Super Admin quản trị nền tảng SpendWise trên Web, phân biệt Admin hệ thống (`Admin.md`) và chủ nhóm User Mobile (`User.md`).  
> **Căn cứ nghiệp vụ:** Các quyết định chốt trong cuộc trao đổi về hai role và S7; `User.md` mới nhất (tệp nội dung biên tập v1.2 ngày 18/10/2026 là nguồn nghiệp vụ User cho **sản phẩm V1.0**); tài liệu bàn giao 18/10 chỉ là inventory lịch sử.  
> **Ranh giới:** User chỉ có Mobile. Web công khai chỉ marketing; không làm User Web app; không thêm role Business Admin/B2B hoặc Money Request vào phạm vi V1.0.

## 0. Nhãn quyết định và nguyên tắc sử dụng

- **[CHỐT]**: đã được chủ dự án duyệt, không hỏi lại khi triển khai đặc tả.
- **[CẦN KIỂM TRA SOURCE]**: scope sản phẩm được duyệt nhưng chưa có bằng chứng API/DB/UI, khả năng kỹ thuật hoặc kiểm thử hoạt động.
- **[BLOCKER]**: thiếu contract, con số hoặc biện pháp bảo mật cần được phê duyệt trước khi triển khai chức năng nhạy cảm; agent không tự giả định.
- **[THIẾT KẾ ĐỀ XUẤT]**: tổ chức tab/field/state/data-flow/test gợi ý để đạt mục tiêu; không tự nhận đã được chủ dự án chốt như một chính sách mới.
- 42 chức năng con là **nhóm tác vụ/tab/bộ lọc**, không bắt buộc 42 URL. Tách theo công việc để người điều hành hiểu mục đích, không đưa toàn bộ thông số kỹ thuật ra sidebar.
- Super Admin là vai trò sản phẩm đã chốt; role `SystemAdmin` hiện có trong Backend **chưa được xác minh tương đương**, phải read-only audit trước mọi mapping/migration.
- `User.md` ưu tiên tuyệt đối với tiền, quỹ, biểu quyết, quyền chủ nhóm và giới hạn sản phẩm. Tài liệu này không tạo công thức tiền còn thiếu.

## 1. Vai trò, quyền hạn và cơ chế bổ nhiệm

### 1.1. Super Admin chịu trách nhiệm

1. Quan sát toàn nền tảng, công việc cần phê duyệt và sự cố ưu tiên.
2. Tạo/phân quyền/vô hiệu hóa **Admin hệ thống thông thường**, giám sát công việc và hành động Admin.
3. Tiếp nhận ca chuyển cấp, quyết định biện pháp bảo vệ nhạy cảm **theo chính sách và contract đã duyệt**; không tùy ý thực hiện giao dịch tiền.
4. Kiểm tra rủi ro tài chính, đối soát, ledger, SePay và các sự cố vận hành.
5. Quản trị cấu hình được phê duyệt, phiên bản Mobile và khả năng tích hợp.
6. Xem báo cáo và nhật ký kiểm toán, xuất thông tin có quyền.
7. Quản lý bảo trì và thông báo toàn hệ thống theo S7 đã chốt, kể cả khẩn cấp.

### 1.2. Bổ nhiệm và thu hồi Super Admin — [CHỐT]

- **Chỉ chủ sở hữu SpendWise** được bổ nhiệm hoặc thu hồi Super Admin qua **quy trình bảo mật riêng**. Không mặc định có role thứ ba trong Admin Web.
- Một Super Admin **không** tự tạo, tự nâng cấp hay thu hồi một Super Admin khác; chỉ trực tiếp quản trị Admin thông thường.
- **[BLOCKER — SECURITY_DESIGN_PENDING]** Cách nhận diện và bảo vệ chủ sở hữu, khôi phục khi mất quyền truy cập, xác thực bổ sung, chống lock-out tài khoản Super Admin cuối cùng, xử lý cấp quyền khẩn chưa được thiết kế chi tiết. Không tự thêm nút bổ nhiệm Super Admin trên Web.
- **[SOURCE_AUDIT_REQUIRED]** Kiểm tra `SystemAdmin`/permissions/migrations trong source thật trước khi quyết định map/đổi tên/thêm role. Không tự sửa dữ liệu/role production.

### 1.3. Hạn chế bất biến

- Không ai được tùy ý cộng/trừ ví, chỉnh ledger, xóa lịch sử gốc, hoàn/chia quỹ không theo contract, tự tước quyền tiền người bị kick hoặc tự chuyển tiền sang tài khoản khác.
- Super Admin không mặc định có quyền đọc mọi dữ liệu riêng tư của mọi User. Phải có ticket được giao, cảnh báo rủi ro hoặc nhiệm vụ hợp lệ được cấp quyền; ghi lý do và audit, tối thiểu hóa thông tin.
- Không thay công thức, mốc, giới hạn đã chốt của User Mobile chỉ vì Web có menu Cấu hình.
- Không cho Super Admin tự coi lỗi quỹ là quyền chiếm vị trí chủ nhóm, kick thành viên hoặc sửa kết quả bầu cử.
- Không tạo chương trình ưu đãi nạp tiền bằng cách phát thông báo. Chương trình liên quan tiền phải được **chủ dự án duyệt trước**; khâu vận hành phải có contract thật trước khi công bố.

## 2. Sơ đồ điều hướng Super Admin (7 menu / 42 chức năng con)

```
SUPER ADMIN
├─ S1. Tổng quan
│  ├─ Toàn cảnh hệ thống
│  ├─ Việc chờ phê duyệt
│  ├─ Sự cố ưu tiên
│  └─ Hoạt động gần đây
├─ S2. Admin & phân quyền
│  ├─ Danh sách Admin
│  ├─ Thêm / thu hồi Admin
│  ├─ Nhóm quyền
│  ├─ Phiên & thiết bị
│  ├─ Lịch sử phân quyền
│  └─ Giám sát công việc Admin
├─ S3. Phê duyệt cấp cao
│  ├─ Chờ phê duyệt
│  ├─ Khiếu nại do Admin chuyển lên
│  ├─ Biện pháp bảo vệ tài khoản / quỹ
│  ├─ Xem xét hoàn tiền / điều chỉnh hợp lệ
│  ├─ Quyết định đã thực hiện
│  └─ Kháng nghị & xem xét lại
├─ S4. Rủi ro & đối soát
│  ├─ Cảnh báo rủi ro
│  ├─ Ngân hàng liên kết & nạp tiền
│  ├─ Webhook & phản hồi
│  ├─ Đối soát
│  ├─ Sai lệch cần xử lý
│  ├─ Kiểm tra sổ cái
│  ├─ Quỹ dư bị khóa cần giám sát
│  └─ Hồ sơ sự cố tài chính
├─ S5. Cấu hình & tích hợp
│  ├─ Cấu hình chung
│  ├─ Giới hạn sản phẩm
│  ├─ Feature Flags
│  ├─ Tích hợp SePay
│  ├─ Sức khỏe hệ thống & nhật ký lỗi
│  ├─ Phiên bản Mobile & bắt buộc cập nhật
│  └─ Lịch sử thay đổi cấu hình
├─ S6. Báo cáo & kiểm toán
│  ├─ Báo cáo tổng hợp
│  ├─ Báo cáo tài chính kỹ thuật
│  ├─ Chất lượng dịch vụ
│  ├─ Nhật ký kiểm toán
│  └─ Xuất báo cáo theo quyền
└─ S7. Bảo trì & Thông báo hệ thống
   ├─ Lịch bảo trì
   ├─ Phạm vi bảo trì
   ├─ Điều khiển bảo trì
   ├─ Lịch nhắc & thông báo bảo trì
   ├─ Thông báo toàn hệ thống
   └─ Lịch sử, kết quả gửi & kiểm toán
```

## 3. Chi tiết sáu menu quản trị S1–S6

### S1. Tổng quan — điều hành thay vì xử lý mọi ticket

| Mục | Tác vụ và dữ liệu | Mục đích/giới hạn |
|---|---|---|
| S1.1 Toàn cảnh hệ thống | Người dùng, nhóm, giao dịch, tích hợp, dịch vụ đang hoạt động; thời gian cập nhật | Nhìn sự cố và quy mô trên toàn nền tảng; không tự bịa biểu đồ doanh thu |
| S1.2 Việc chờ phê duyệt | Hồ sơ P3, biện pháp bảo vệ, kháng nghị, khiếu nại chuyển cấp | Cửa vào trực tiếp S3, không tạo case thứ hai |
| S1.3 Sự cố ưu tiên | Outage, lệch đối soát, nguy cơ tiền, sự cố bảo mật, bảo trì | Giám sát sự cố có ảnh hưởng hàng loạt; severity cần nguồn thật |
| S1.4 Hoạt động gần đây | Quyết định của Super Admin, thay đổi quyền, tình trạng S7, sự kiện audit | Có thể truy ngược hồ sơ và người thực hiện |

**[CẦN KIỂM TRA SOURCE]** Aggregation/telemetry không được coi là đã có. Dashboard không cho phép chạy hành động tiền chỉ bằng một cú nhấn tổng quan.

### S2. Admin & phân quyền — quản lý người vận hành thông thường

| Mục | Tác vụ và dữ liệu | Mục đích/giới hạn |
|---|---|---|
| S2.1 Danh sách Admin | Account ID, trạng thái, nhóm quyền, thời điểm tạo, hoạt động liên quan theo quyền | Biết ai đang giữ quyền truy cập hệ thống |
| S2.2 Thêm/thu hồi Admin | Onboard Admin thường, vô hiệu hóa, thu hồi phiên, thay đổi nhiệm vụ | Thu hồi quyền kịp thời; không tạo hoặc nâng cấp Super Admin |
| S2.3 Nhóm quyền | Nhóm quyền hỗ trợ, điều tra, tra cứu tài chính và quyền P2 đã duyệt | Tránh cấp quyền P3 mặc định; **[BLOCKER]** ma trận permission/role server-side chi tiết cần mapping source |
| S2.4 Phiên & thiết bị | Theo dõi phiên quản trị, kiểm tra bất thường, thu hồi phiên | Giảm nguy cơ bị chiếm quyền; không lộ token |
| S2.5 Lịch sử phân quyền | Actor, target, permission trước/sau, thời gian, lý do | Kiểm soát leo thang quyền; lịch sử gốc không sửa/xóa |
| S2.6 Giám sát công việc Admin | Việc được giao, kết quả, quá hạn, thao tác nhạy cảm, truy cập dữ liệu, bất thường | Kiểm soát chất lượng và lạm quyền; không biến thành giám sát cá nhân không liên quan công việc |

**Quy trình gợi ý:** Lập yêu cầu cấp quyền → xác thực Super Admin → cấp role Admin thường + phạm vi nhiệm vụ → lưu audit → kiểm tra session/permissions. Trường hợp nhạy cảm phải có xác nhận phù hợp; **không tự coi yêu cầu này là policy phê duyệt kép**. Quyền bổ nhiệm Super Admin thuộc chủ sở hữu qua kênh riêng.

### S3. Phê duyệt cấp cao — vụ việc vượt quyền Admin

| Mục | Tác vụ và dữ liệu | Mục đích/giới hạn |
|---|---|---|
| S3.1 Chờ phê duyệt | Các yêu cầu P3 còn hiệu lực, trạng thái, case và evidence | Tập trung hồ sơ quyết định; không cho người tạo tự phê duyệt nếu policy yêu cầu hai người |
| S3.2 Khiếu nại do Admin chuyển lên | Xem chung case ID, dòng thời gian, chứng cứ và đề xuất Admin | Không nhân đôi hệ thống khiếu nại |
| S3.3 Biện pháp bảo vệ tài khoản/quỹ | Gia hạn/ngoại lệ account lock; hạn chế tiền đi **toàn quỹ** nếu có chính sách/contract hợp lệ | Hạn chế phải tương xứng, có phạm vi, lý do, thời hạn, audit; không tự chặn hoàn tiền hợp lệ hoặc tước quyền lợi |
| S3.4 Xem xét hoàn/điều chỉnh hợp lệ | Xem xét hồ sơ hoàn/điều chỉnh qua workflow đã định lượng và được duyệt | Không tự nhập số tiền bất kỳ, không sửa ledger; **[FINANCE_BLOCKER]** contract còn thiếu thì nút execute không được mở |
| S3.5 Quyết định đã thực hiện | Kết quả, người duyệt, bằng chứng, trạng thái thực tế từ Backend | Quyết định phê duyệt ≠ giao dịch tiền đã Completed |
| S3.6 Kháng nghị/xem xét lại | User hoặc Admin yêu cầu rà soát quyết định, kèm kết quả và lịch sử | Giảm rủi ro lạm quyền; **[BLOCKER]** SLA/kênh kháng nghị/recusal chi tiết chưa chốt |

**[CHỐT] Ranh giới quỹ nhóm:** Admin A3/A5 chỉ điều tra khoản chi riêng lẻ bị báo cáo/tạm dừng; không khóa tiền đi cả quỹ. Super Admin có thẩm quyền xem xét biện pháp **cấp toàn quỹ** theo quy trình đã duyệt; không mặc nhiên được thực hiện luồng tiền mới. Giữ phân biệt `Chờ thực hiện`, `Tạm dừng`, hold và quỹ khóa vì chưa đủ góp của nhóm loại 1. Thành viên báo cáo khoản chi lớn → đúng khoản đó tạm dừng và mở ticket; chủ nhóm/người đề nghị có quyền hủy theo User.md; không xóa lịch sử.

**[BLOCKER]** Điều kiện khóa/hủy/bỏ hạn chế toàn quỹ, ranh giới bảo vệ hoàn tiền, thẩm quyền release hold, hoàn/rút/kick/giải tán còn thiếu contract tài chính và state machine. Không mở hành động ghi tiền V1.0 thật trước khi được phê duyệt.

### S4. Rủi ro & đối soát — giám sát chất lượng tiền

| Mục | Tác vụ và dữ liệu | Mục đích/giới hạn |
|---|---|---|
| S4.1 Cảnh báo rủi ro | Sự kiện như chuỗi chi lớn, đổi quyền chi, giao dịch lặp, bất thường có nguồn | Tín hiệu điều tra chứ không phải kết luận gian lận; không fake AI fraud score |
| S4.2 Ngân hàng liên kết & nạp tiền | Trạng thái liên kết, giao dịch nạp, mã tham chiếu, phản hồi đối tác | Phân biệt ngân hàng liên kết với ví nội bộ; TestMode ≠ tiền thật |
| S4.3 Webhook & phản hồi | Sự kiện từ SePay, thời điểm nhận/xử lý, retry, thất bại nếu có log | Chỉ xem metadata hợp lệ; không lộ secret, không phát webhook giả |
| S4.4 Đối soát | So kết quả đối tác → transaction → ledger | Phát hiện tiền không khớp mà không cộng tay cho cân |
| S4.5 Sai lệch cần xử lý | Danh sách mismatches, severity, case, người phụ trách | Chênh lệch 1đ do chia số nguyên **hợp lệ** không tự coi là lỗi; cần kiểm tra công thức/ledger |
| S4.6 Kiểm tra sổ cái | Balanced entries, thiếu/không có entries, liên kết nguồn | Chỉ đọc và điều tra; sửa bằng contract điều chỉnh hợp lệ, không sửa DB trực tiếp |
| S4.7 Quỹ dư bị khóa cần giám sát | Dư cá nhân khóa, khoản hold chờ thực hiện/tạm dừng, hoàn pending, chi bị đóng | Biết tiền nào không khả dụng và lý do; không giải phóng bằng thao tác tùy ý |
| S4.8 Hồ sơ sự cố tài chính | Timeline, affected users, ảnh hưởng, giải pháp, status/evidence | Một incident liên kết nhiều ticket/giao dịch; không nhân bản ledger |

**[CẦN KIỂM TRA SOURCE]** Ledger cũ từng có ghi nhận các posting không hoàn chỉnh theo báo cáo lịch sử; chỉ source/log mới nhất mới kết luận. Không dùng dữ liệu tổng hợp từ một bài test cũ để khẳng định current system PASS.

### S5. Cấu hình & tích hợp — có kiểm soát, không đổi luật tiền

| Mục | Tác vụ và dữ liệu | Mục đích/giới hạn |
|---|---|---|
| S5.1 Cấu hình chung | Các tham số vận hành được owner duyệt và có whitelist | Không biến UI cấu hình thành trình chỉnh toàn bộ policy |
| S5.2 Giới hạn sản phẩm | Hiển thị: 3 ví/User (Main+2), 100 bạn, 10 nhóm hoạt động/User, loại 1 ≤30, loại 2 ≤6 | **Mặc định chỉ đọc**; Super Admin không tùy ý tăng giới hạn/sửa mô hình nhóm |
| S5.3 Feature Flags | Trạng thái/đối tượng/tác động của flag đã được thiết kế, bật/tắt theo policy | Không gỡ kiểm tra Backend, không biến TestMode thành tiền thật |
| S5.4 Tích hợp SePay | Trạng thái dịch vụ, TestMode, lỗi, cấu hình được phép | Không tiết lộ API key/secret; không tự mở payout chưa có contract |
| S5.5 Sức khỏe hệ thống & log | API, DB, queue/jobs, logs đã khử nhạy cảm, alert tình trạng | Phục vụ phát hiện và điều phối kỹ thuật; không phải giao diện sửa database |
| S5.6 Phiên bản Mobile & bắt buộc cập nhật | Bản hiện hành/minimum supported, thông điệp cập nhật, kiểm tra tương thích API | Chỉ áp dụng nếu Mobile/Backend/store đã hỗ trợ; không thể tự cài app qua Backend; tránh khóa tất cả User do cấu hình nhầm |
| S5.7 Lịch sử đổi cấu hình | Actor, lý do, trước/sau, thời gian, đánh giá kết quả, rollback được phép | Audit và phục hồi; không ghi raw secret vào nhật ký |

**[BLOCKER]** Danh mục key được phép thay đổi, policy duyệt/config publish, đặc tả cập nhật bắt buộc và phương án rollback, phạm vi Feature Flags, tích hợp SePay thật cần được audit; không tự mở chức năng ghi production.

### S6. Báo cáo & kiểm toán — nguồn đáng tin, quyền tối thiểu

| Mục | Tác vụ và dữ liệu | Mục đích/giới hạn |
|---|---|---|
| S6.1 Báo cáo tổng hợp | Hoạt động User, giao dịch, quỹ nhóm, hỗ trợ theo kỳ và timestamp | Dùng nguồn thật, định nghĩa chỉ số rõ ràng, không suy ra doanh thu tùy ý |
| S6.2 Báo cáo tài chính kỹ thuật | Transaction states, nạp/hoàn, hold, đối soát, lỗi ledger | Không mặc định là sổ kế toán pháp lý của doanh nghiệp |
| S6.3 Chất lượng dịch vụ | Thời gian phản hồi, ticket quá hạn, sự cố lặp, đánh giá năng lực hỗ trợ | Chỉ dùng nguồn dữ liệu/tính toán kiểm chứng được |
| S6.4 Nhật ký kiểm toán | Actor, role, action, object, time, reason, outcome, prior/after metadata | Lịch sử gốc không có nút sửa/xóa; retention/tamper-evidence thực tế cần contract kỹ thuật |
| S6.5 Xuất báo cáo theo quyền | Dữ liệu được phép, lý do, scope, số liệu tối thiểu, audit lượt xuất | Không tải dữ liệu User hàng loạt nếu không có mục đích/thẩm quyền phù hợp |

**Bảo vệ dữ liệu:** Ticket/cảnh báo/task hợp lệ + lý do + phân quyền + audit áp dụng cả Super Admin. **[BLOCKER]** Chính sách retention, phạm vi CSV/PDF, phê duyệt xuất dữ liệu nhạy cảm và quyền truy cập file đã xuất chưa chốt.

## 4. S7 — Bảo trì & Thông báo hệ thống (ĐÃ DUYỆT CƠ CHẾ)

> **Đây là module độc lập thứ bảy**, không giấu trong S5 và không trao cho Admin thường. Super Admin là người tạo lịch, điều khiển, kết thúc và phát thông báo hệ thống. Backend phải thực thi khóa thật; Mobile chỉ phản ánh trạng thái server.

### 4.1. Sáu chức năng con và mục đích

| Mục | Nội dung chức năng | Vì sao cần |
|---|---|---|
| S7.1 Lịch bảo trì | Tạo/xem/sửa/hủy lịch, ngày giờ bắt đầu, giờ kết thúc **dự kiến**, lý do và đối tượng ảnh hưởng | Người vận hành/User chủ động chuẩn bị, tránh lịch chồng nhau |
| S7.2 Phạm vi bảo trì | Chọn **toàn hệ thống** hoặc **theo chức năng/module**, xác định API phụ thuộc | Không gián đoạn toàn ứng dụng nếu chỉ một phần gặp lỗi, nhưng phải an toàn Backend |
| S7.3 Điều khiển bảo trì | Kích hoạt theo lịch, bảo trì khẩn, gia hạn, trạng thái kiểm tra sức khỏe, **Super Admin xác nhận kết thúc** | Không cần chờ mới bắt đầu khẩn cấp; không tự mở lại khi hệ thống chưa được xác nhận |
| S7.4 Lịch nhắc & thông báo bảo trì | Cấu hình các mốc nhắc trước theo lịch tùy chọn, bộ mặc định và giới hạn chống spam; cập nhật khi đổi lịch | Giúp User hoàn tất thao tác kịp, tránh quá nhiều push |
| S7.5 Thông báo toàn hệ thống | Soạn/xem trước, chọn nhóm nhận, kênh, lịch gửi, khẩn/vận hành/ưu đãi đã duyệt, đính chính | Không cần nâng cấp app để truyền tải cảnh báo; ngăn lạm dụng quảng cáo khẩn |
| S7.6 Lịch sử, kết quả gửi & kiểm toán | Lịch sử bảo trì, người tạo/sửa/bắt đầu/gia hạn/kết thúc, nội dung thông báo và kết quả phân phối | Truy trách nhiệm; phân biệt gửi/đến/đọc; không xóa lịch sử phát hành |

### 4.2. Trạng thái bảo trì và phân quyền [CHỐT]

- **Bảo trì toàn hệ thống**: Mobile User đang dùng chuyển sang **màn hình bảo trì chuyên dụng**, không phải Login và không chủ động xóa token/đăng xuất. Người mở mới ứng dụng thấy trạng thái bảo trì. Khi kết thúc, kiểm tra phiên và tải lại trạng thái authoritative; User chỉ phải đăng nhập nếu phiên không còn hợp lệ.
- **Bảo trì theo chức năng**: phần bị ảnh hưởng hiển thị thông báo và chặn thao tác; phần độc lập còn hoạt động **nếu Backend chứng minh an toàn**. Nếu có phụ thuộc ghi tiền/tác vụ nền, chặn thêm các đường ảnh hưởng, hoặc chuyển sang phạm vi rộng hơn. Không cho luồng gián tiếp lách bảo trì.
- **Super Admin** tạo/sửa/hủy lịch, kích hoạt/gia hạn và xác nhận kết thúc; **Admin** chỉ xem lịch, thông báo và hỗ trợ User, không tự điều khiển bảo trì.
- Scheduler có thể **tự kích hoạt bảo trì đúng giờ bắt đầu** theo lịch đã được phê duyệt (đó là hành động đã định trước do Super Admin cấu hình). **Không tự kết thúc** ở giờ ước tính.
- Backend authoritative kiểm soát trạng thái cho API Mobile; chặn ở UI thôi không đủ. Giao dịch đã tiếp nhận phải kết luận theo ledger/status thực, không tự báo thất bại/thành công vì mất kết nối hoặc popup.

**[THIẾT KẾ ĐỀ XUẤT]** State machine vận hành: `Draft → Scheduled → Active → SafetyCheck/ReadyForManualClose → Closed` (hoặc `Cancelled` trước khi Active; `Extended` là sự kiện cập nhật lịch khi Active). Tên enum thực tế phải audit; không ép đổi schema chỉ để trùng tài liệu. Thời điểm dự kiến khác với thời điểm kết thúc thực.

### 4.3. Bảo trì khẩn cấp [CHỐT]

- **Một Super Admin** được bật ngay, **không cần người thứ hai phê duyệt trước**.
- Bắt buộc **xác thực lại bằng biện pháp bổ sung**, nhập lý do, xác nhận rõ phạm vi/tác động, lưu audit, và **rà soát sau sự cố**.
- Có thể chọn toàn hệ thống hoặc module riêng; nếu không cô lập an toàn thì phải mở rộng phạm vi theo chính sách an toàn Backend.
- Khi kích hoạt gửi cảnh báo **ngay trong app** và push khi khả dụng, không chờ lịch nhắc. Chống phát trùng theo incident/message ID.
- Không tự thực thi lại các giao dịch có trạng thái chưa rõ; điều tra/đối soát trước khi tiếp tục.
- **[BLOCKER — TECH]** Phương thức xác thực bổ sung, cấu hình audit bất biến, bảo vệ khỏi một tài khoản Super Admin bị chiếm quyền, cơ chế break-glass phục hồi và tiêu chí cho phép chọn phạm vi module cần thiết kế/test. Quyền hành động đã chốt **không** có nghĩa bỏ điều kiện kỹ thuật.

### 4.4. Gia hạn, kết thúc và khôi phục [CHỐT]

- **Chỉ Super Admin xác nhận kết thúc**, **sau khi** các kiểm tra an toàn bắt buộc đạt. **Không tự mở lại** vì đến giờ dự kiến, không tự mở lại chỉ vì health check xanh.
- Nếu dịch vụ chưa an toàn hoặc người xác nhận chưa sẵn sàng: trạng thái bảo trì giữ nguyên; thông báo thời gian dự kiến mới hoặc tình trạng đang xác minh, không hiển thị lời hứa sai.
- Khi Super Admin xác nhận: Backend chuyển trạng thái atomically, các chức năng bị chặn được mở lại khi đạt điều kiện, Mobile xác minh phiên/lấy lại dữ liệu. Giao dịch đã pending cần đối soát, không auto replay thiếu idempotency.
- Hủy lịch **trước khi kích hoạt** khác kết thúc bảo trì **đang chạy**. Mọi thao tác sửa, hủy, gia hạn, kết thúc phải ghi lý do và thông báo thay đổi ảnh hưởng người dùng.
- **[BLOCKER — OPS]** Danh sách health checks bắt buộc, ai vận hành khi không có Super Admin trực, hạn thông báo gia hạn, phương án rollback khi readiness lỗi, và thủ tục ứng phó sự cố chưa có contract kiểm chứng. Không mặc định kết thúc theo đồng hồ.

### 4.5. Lịch nhắc tùy chọn và chống spam [CHỐT CƠ CHẾ, CHƯA CHỐT CON SỐ]

- Super Admin **tự cấu hình các mốc nhắc trước bảo trì**; có preset mặc định, chống spam và chống gửi trùng. **Không** ép cố định 2 hoặc 3 lần nhắc.
- **[THAM SỐ ĐỀ XUẤT — CHƯA DUYỆT]** Preset tham khảo: 24 giờ, 1 giờ, 15 phút trước bảo trì; có thể giới hạn số mốc/đợt và khoảng cách tối thiểu. **Không xem các con số này là cấu hình đã chốt.**
- Lịch tạo quá sát giờ: chỉ gửi các mốc còn thích hợp; **không dồn gửi bù** những mốc đã qua. Dùng idempotency theo `maintenance_id` + loại cảnh báo + mốc/số revision, tránh nhân đôi khi retry.
- Khi đổi/hủy/gia hạn, cập nhật banner và gửi thông báo điều chỉnh có nội dung rõ, không tái gửi mọi cảnh báo cũ. Bảo trì khẩn phát ngay và không bị lịch nhắc khuyến mại chặn, nhưng vẫn chống trùng cùng sự cố.
- Banner/thông báo trong app và trạng thái Backend là bắt buộc; **push có thể không đến** thiết bị offline/tắt quyền, nên không được xem push thành công là bằng chứng User đã đọc.
- **[BLOCKER — CONFIG_PENDING]** Số lần tối đa, min-spacing, thứ tự kênh, giới hạn đối tượng, chính sách chống spam cụ thể cần phê duyệt hoặc audit mức an toàn trước triển khai.

### 4.6. Thông báo toàn hệ thống [CHỐT QUYỀN]

| Loại | Ai có quyền phát? | Điều kiện và giới hạn |
|---|---|---|
| **Khẩn cấp** | Super Admin | Sự cố/bảo mật có thật, gửi ngay theo quy trình, hiển thị in-app/push khi khả dụng, audit |
| **Vận hành/bảo trì** | Super Admin | Gửi ngay/hẹn lịch, thông tin đúng trạng thái và thời điểm; chỉnh lịch phải đính chính |
| **Cập nhật phiên bản Mobile** | Super Admin theo phạm vi vận hành | Chỉ khi version/release có thật và API tương thích, không ép người dùng tải một phiên bản chưa phát hành |
| **Ưu đãi/khuyến mại liên quan tiền** | Super Admin **chỉ phát sau khi chủ dự án phê duyệt chương trình** | Phải có điều kiện, tỷ lệ, thời gian, nguồn kinh phí, cơ chế Backend/đối tác áp dụng thật; không tạo thưởng 1% bằng notification |
| **Phản hồi hỗ trợ từng User** | Admin theo ticket/quyền | Không phát broadcast hoặc masquerade thông báo khẩn |

**Các chức năng trong tab S7.5**: soạn bản nháp → loại thông báo/mức độ → chọn người nhận (User, Admin hoặc nhóm được hỗ trợ) → kênh (in-app/banner/popup/push theo khả năng thật) → xem trước → gửi ngay/lên lịch → sửa/hủy nếu chưa phát → theo dõi → đính chính có tham chiếu thông báo cũ. Nội dung đã phát được lưu bất biến; không xóa dấu vết sai sót. Kênh marketing tuân chính sách tùy chọn nhận thông báo và pháp lý liên quan; không giả trang cảnh báo bắt buộc để quảng cáo.

**[BLOCKER]** Phân khúc người nhận, policy duyệt nội dung mẫu, ưu tiên push, opt-in tiếp thị, SLA và trạng thái delivery/read thực tế cần audit; nếu kênh không có provider data thì không hiển thị phần trăm "đã đọc" giả.

### 4.7. Bù deadline nghiệp vụ khi bảo trì [CHỐT]

**Quy tắc cốt lõi:** Bù **đúng khoảng thời gian User không thể thực hiện hành động bắt buộc** do bảo trì của SpendWise; **không cộng bù cho mọi module hoặc mọi bộ đếm**. Nguồn thời gian phải từ Backend, dùng cùng mốc server, không dựa clock thiết bị và không cộng hai lần.

1. **Deadline cần hành động:** bỏ phiếu hoạt động mới của **nhóm loại 1** (mốc 24h); bầu/chấp nhận chủ nhóm của **hai loại**; phản hồi bắt buộc của chủ nhóm; lời mời/xác nhận có deadline nếu nghiệp vụ đó thực có deadline. Nếu module bị khóa khiến không hành động được, giữ đúng phần thời gian còn lại và tiếp tục sau khi khả dụng. Ví dụ bảo trì 01:20–02:00, biểu quyết sẽ hết 01:30 → khi mở 02:00 còn 10 phút, hạn mới 02:10.
2. **Bảo trì module khác không liên quan:** nếu User vẫn sử dụng chức năng cần hành động, deadline ấy **không được kéo dài**. Với toàn hệ thống thì tính phạm vi toàn bộ chức năng Mobile bị chặn.
3. **Thời gian chờ thụ động 24 giờ:** **tiếp tục chạy** trong bảo trì; thao tác rời/kick/hoàn chỉ được hoàn tất khi Backend an toàn và **đầy đủ điều kiện tiền/quyền** (đặc biệt hoàn thành công trước khi loại tư cách). Không ép hoàn tất một sự kiện chỉ vì đồng hồ hết.
4. **Mốc 15 ngày không sử dụng SpendWise của chủ nhóm:** loại trừ **chính xác** khoảng bảo trì khiến User **không thể sử dụng app**; bảo trì một chức năng khác mà vẫn dùng app được thì không loại trừ. Tác vụ nền/token refresh/push không tính hoạt động thực. Việc đủ ngưỡng chỉ mở quy trình bầu, không tự giáng chủ.
5. **Thông báo nhắc & trạng thái bầu cử:** tính lại theo deadline sau bù, chống gửi trùng, không tự đồng nhất bầu hai vòng với biểu quyết hoạt động mới 24 giờ.

**[BLOCKER — THỰC THI]** Cách tính khoảng gián đoạn chính xác theo service/feature availability; bảo trì kế hoạch vs mất dịch vụ ngoài lịch; tính nhiều lần gián đoạn chồng nhau; state machine nhóm lúc vượt hạn; thời gian của từng vòng bầu chủ chưa chốt trong User.md. Cần ví dụ số học/state transitions/test trước code, **không tự ghi giá trị deadline mới vào production chỉ từ đặc tả này**.

### 4.8. Tiêu chí nghiệm thu S7

| Mã | Trường hợp | Kết quả bắt buộc khi triển khai |
|---|---|---|
| MNT-01 | Hẹn bảo trì 01:20 AM | Scheduler kích hoạt đúng lịch/server timezone đã cấu hình; audit người tạo |
| MNT-02 | Bảo trì toàn hệ thống, User đang login | Chuyển màn bảo trì, **không xóa phiên chỉ vì bảo trì**, API tiền bị chặn an toàn |
| MNT-03 | Bảo trì riêng Quỹ nhóm | Quỹ nhóm bị chặn; phần không liên quan còn chạy nếu Backend bảo đảm cô lập |
| MNT-04 | Mobile offline hoặc push tắt | In-app state khi quay lại có thông tin đúng; không dựa duy nhất push |
| MNT-05 | Hết giờ dự kiến nhưng chưa có Super Admin xác nhận | **Vẫn bảo trì**, dù health-check đã PASS |
| MNT-06 | Super Admin xác nhận nhưng safety-check không đạt | Từ chối kết thúc, ghi lý do và audit |
| MNT-07 | Bảo trì khẩn cấp | Một Super Admin xác thực bổ sung, xác nhận tác động, nhập lý do, ghi audit, kích hoạt ngay, review sau |
| MNT-08 | Thay lịch khi đã gửi thông báo | Thông báo điều chỉnh/đính chính, không spam trùng; banner cập nhật |
| MNT-09 | Deadline còn 10 phút khi bắt đầu 40 phút bảo trì ảnh hưởng | Sau mở lại còn đúng 10 phút, không kéo dài gấp đôi |
| MNT-10 | Đang chờ 24h passive leave | Clock chạy, **không hoàn tất rời/kick** nếu Backend/hoàn tiền chưa an toàn |
| MNT-11 | Bảo trì toàn app 2h khi chủ còn 1h trước 15 ngày | Sau bảo trì vẫn còn 1h, không mở bầu sai |
| MNT-12 | Bảo trì AI riêng, chức năng app khác vẫn dùng được | Không trừ 2h khỏi mốc 15 ngày, không gia hạn phiếu nhóm |
| MNT-13 | Phát thông báo nạp +1% khi chưa có chương trình owner duyệt | Không cho phát như chương trình chính thức; không tự cộng tiền |
| MNT-14 | Xác nhận kết thúc | Server mở phạm vi an toàn, Mobile revalidate session/cache/transaction, không replay tiền trùng |
| MNT-15 | Admin thường gọi endpoint tạo lịch/kết thúc | Backend từ chối; không chỉ ẩn nút UI |

**[CẦN KIỂM TRA SOURCE]** Các kiểm thử này là hợp đồng nghiệm thu tương lai; chưa có bằng chứng PASS của Mobile/Backend hiện hành.

## 5. Ma trận quyền tổng hợp Admin ↔ Super Admin ↔ chủ sở hữu

| Hành động | Admin | Super Admin | Chủ sở hữu SpendWise |
|---|---|---|---|
| Đọc hồ sơ User nhạy cảm có căn cứ/nhiệm vụ | Theo quyền, audit | Theo quyền, audit | Không được miễn quy định dữ liệu chỉ vì là owner |
| Trả lời ticket thường, điều tra khoản chi đơn lẻ | Có | Xem/tiếp quản khi chuyển cấp | Không cần là người xử lý thường |
| Thu hồi phiên và tạm chặn **tiền đi tài khoản** nghi bị chiếm quyền | Có theo khẩn cấp P2 + audit | Gia hạn, ngoại lệ, giám sát | Theo cơ chế quản trị đã cho phép, không truy cập tùy ý |
| Hạn chế **tiền đi toàn quỹ** | Không | Có **theo policy/contract** | Không miễn bảo toàn tiền |
| Phê duyệt hoàn/điều chỉnh phức tạp | Không | Chỉ khi có financial contract được owner duyệt | Có quyền duyệt *chính sách*, không tự sửa tiền |
| Chuyển chủ/kick/chia quyền tiền trái quy tắc User | Không | Không | Không |
| Tạo, cấp/thu hồi quyền Admin thường | Không | Có | Qua cơ chế quản trị được thiết kế |
| Bổ nhiệm/thu hồi Super Admin | Không | **Không** | **Chỉ chủ sở hữu qua quy trình bảo mật riêng** |
| Bảo trì lịch/khẩn/cấp module và xác nhận kết thúc | Chỉ xem, hỗ trợ | Có, theo S7 | Không có lối bypass an toàn tài chính |
| Broadcast khẩn/vận hành | Không | Có | Có quyền phê duyệt chính sách liên quan |
| Broadcast ưu đãi có tiền | Không | Chỉ phát sau chương trình đã duyệt | **Duyệt chương trình trước** |
| Tự sửa số dư/ledger hoặc xóa audit | Không | Không | Không |

## 6. Những hợp đồng và giới hạn của User.md phải giữ nguyên

1. **Giới hạn:** tối đa 3 ví/User (1 Main + 2), 100 bạn, 10 nhóm active/User, loại 1 tối đa 30 người, loại 2 tối đa 6 người, mã nhóm `SPW000001...` bất biến toàn hệ thống.
2. **Tiền:** góp quỹ chỉ bằng ví cá nhân SpendWise; hoàn từ quỹ về ví cá nhân qua ledger nội bộ, không hoàn trực tiếp ngân hàng, không chuyển ngân hàng thật khi thiếu contract/đối tác.
3. **Một chủ nhóm hiện tại:** chuyển quyền hoặc bầu lại khi không hoạt động đủ 15 ngày (theo ngoại lệ thời gian S7) hoặc quá hạn phản hồi công việc bắt buộc; tiền của chủ cũ không mất khi bị thay quyền.
4. **Hai loại nhóm khác nhau:** loại 1 có nghĩa vụ góp cố định, `T` do chủ nhóm quyết trong điều kiện hợp lệ, đóng hoạt động cũ và biểu quyết hoạt động mới 24 giờ; loại 2 góp/chi tự do, không có quy trình chuyển hoạt động mới theo loại 1.
5. **Khoản chi lớn:** áp dụng chờ thực hiện, thông báo thành viên, quyền báo cáo và giữ chỗ tiền; cơ chế và ngưỡng mặc định ghi trong User.md nhưng contract cuối cùng để thực hiện/giải phóng tiền chưa đủ.
6. **Lịch sử:** lịch sử hoạt động nhóm và lịch sử giao dịch riêng nhưng có liên kết, bất biến và audit; không xóa lịch sử khi giải tán.
7. **Money Request:** loại bỏ, không đưa trở lại bằng một menu Admin mới.

## 7. Danh sách BLOCKER, không tự cấp phép triển khai

### P0 — tiền/quỹ/banking (từ User.md, áp dụng cả hai role)

- **FIN-01** Công thức quyền lợi đóng góp ròng qua nhiều lần chi/rút/hoàn/chuyển hoạt động/thay T, đặc biệt rút cá nhân ở nhóm loại 2; trọng số `w_i` và thứ tự trả tiền khóa/hold.
- **FIN-02** Rời/kick/giải tán khi có tiền giữ chỗ, khoản hoàn pending/failed, tài khoản khóa hoặc xung đột số dư; không hoàn hai lần và không âm quỹ.
- **FIN-03** Thay `T` khi đã chi, chủ nhóm cuối cùng muốn rời, chuyển quyền và bầu cử khi không ứng viên/không nhận chức; thời lượng từng vòng bầu chủ chưa chốt.
- **FIN-04** Quy trình quyết định và thực thi/hủy khoản chi bị báo cáo/tạm dừng, hold, timeout, thao tác release và phạm vi Super Admin có quyền; không tự thêm nút "chuyển tiền".
- **BANK-01** Tiền thật, đối tác được cấp phép, lưu ký và luồng payout/chuyển ngân hàng chưa được chứng minh; chỉ TestMode khi thiếu điều kiện hợp lệ.

### P0/P1 — quyền, bảo trì, hệ thống

- **SEC-OWNER-01** Xác thực chủ sở hữu và quy trình cấp/thu hồi Super Admin, chống mất Super Admin cuối cùng; mapping `SystemAdmin` trong Backend.
- **SEC-ACCESS-01** Permission matrices, masking, log, policy tra cứu theo case, phân quyền xuất dữ liệu; bảo vệ log/secret, thời gian retention.
- **SEC-ADM-01** Điều kiện chứng cứ, thời lượng, thông báo, tự hết hạn và kháng nghị đối với tạm chặn tiền đi cấp tài khoản do Admin; điều kiện hạn chế cấp toàn quỹ của Super Admin.
- **MNT-OPS-01** Preset và hạn chống spam bằng con số, min-spacing, kênh/thứ tự gửi; thủ tục hủy/gia hạn và cảnh báo khi không có Super Admin phản hồi.
- **MNT-OPS-02** Các health gates trước xác nhận kết thúc, phạm vi phụ thuộc API khi bảo trì một module, scheduler/backend state machine, nhận diện thời gian gián đoạn chính xác và bù deadline không nhân đôi.
- **MNT-SEC-01** Biện pháp xác thực lại Super Admin lúc bảo trì khẩn, review sau sự cố, chống abuse và log bất biến; khả năng khôi phục hệ thống khi Web Admin cũng bị outage.
- **CFG-01** Danh sách cấu hình có thể thay đổi, policy release/rollback, mobile minimum version semantics, feature flags, SePay integration/mode.
- **DATA-01** Nguồn dữ liệu báo cáo/đối soát/cảnh báo/notification delivery/read, tránh dashboard/AI giả hoặc dữ liệu stale.

### Trạng thái triển khai và hạn chế bằng chứng

- Tài liệu bàn giao ghi Admin Web đã có 13 màn lịch sử và đã được khóa UI desktop trước đây. **Tài liệu mới không tự cho phép redesign**; cần duyệt phạm vi thay đổi UI và kế hoạch chuyển menu riêng.
- Read-only Admin E2E theo báo cáo trước đây không chứng minh write API, auth permission, tài chính hoặc S7 đã chạy. Cần audit source/log/checkpoint mới nhất trước mọi nhãn DONE/PASS.
- Nếu source thiếu endpoint/DB event/permission đúng contract, tạo **GAP report** thay vì mock tiền hoặc cấp quyền admin ảo.

## 8. Acceptance criteria toàn hệ thống và quy trình triển khai

> Chưa kiểm thử thực tế; các mục sau là tiêu chí phải chứng minh **sau** khi được giao batch triển khai.

| Mã | Điều kiện nghiệm thu | Kỳ vọng |
|---|---|---|
| SUP-01 | Super Admin thử bổ nhiệm Super Admin mới | Từ chối, chuyển sang quy trình riêng của chủ sở hữu |
| SUP-02 | Super Admin tạo/thu hồi Admin thường | Permission server kiểm tra, phiên bị thu hồi phù hợp, audit |
| SUP-03 | Xem giao dịch User không có case/task hợp lệ | Từ chối/ẩn đúng phạm vi, audit |
| SUP-04 | Phê duyệt tài chính khi thiếu contract | Không thể kích hoạt chuyển tiền, nêu blocker |
| SUP-05 | Áp dụng biện pháp hạn chế toàn quỹ | Chỉ qua workflow an toàn đã được duyệt; bảo vệ quyền lợi/hoàn hợp lệ |
| SUP-06 | Thử sửa ledger, xóa audit hoặc tăng số dư | Server từ chối, không có side-effect |
| SUP-07 | Kiểm tra webhook SePay/TestMode | Tách biệt demo/tiền thật, không lộ secret hay giả payout |
| SUP-08 | Thử vượt giới hạn sản phẩm trong S5 | Không tự sửa quy tắc đã khóa |
| SUP-09 | Cập nhật Mobile chưa có trên store | Không phát yêu cầu bắt buộc cập nhật giả |
| SUP-10 | Báo cáo/xuất dữ liệu nhạy cảm | Quyền, mục đích, scope và audit được thực thi |
| SUP-11 | Bộ ca MNT-01 đến MNT-15 | Thực thi đầy đủ, gồm manual close và bù deadline |
| SUP-12 | Màn quản trị bị outage, User đang giao dịch | Không tự replay tiền, có quy trình phục hồi an toàn và evidence |

**Quy trình triển khai** (chỉ khi chủ dự án duyệt scope riêng):
1. Codex **read-only audit** Backend/DB/roles/API/ledger/scheduler/health/SePay và chứng cứ; Antigravity **read-only audit** Web Admin/Mobile, báo cáo menu→screen→API; giữ khóa UI desktop trừ batch được duyệt.
2. Lập permission matrix + incident/case contracts và giải quyết các BLOCKER tài chính/quỹ/bầu cử/pháp lý có test số học/state machine.
3. S7 cần mô tả rõ Backend maintenance middleware/job scheduler, feature dependency, Mobile gating, session handling, notifications, audit, deadline offsets; trình trước cho duyệt rồi mới code.
4. Batch triển khai độc lập có rollback/checkpoint, unit/integration tests và E2E nhiều tài khoản (hai User + Admin + Super Admin) trong môi trường cô lập. Không tự ghi vào production DB để làm test.
5. Không tự xóa User Web cũ, không thêm B2B/Business Admin, không xây marketing trong batch Admin/Super Admin.

## 9. Checklist hoàn tất đặc tả

- [x] Super Admin có 7 menu / 42 chức năng con và chức năng nào làm gì, vì sao cần.
- [x] Chủ sở hữu duy nhất bổ nhiệm/thu hồi Super Admin; Super Admin quản lý Admin thường.
- [x] Phân quyền giữa hai role rõ ràng; không role nào tự sửa tiền, ledger, lịch sử.
- [x] S7 chốt hai phạm vi bảo trì, giữ phiên User, lịch nhắc tùy chỉnh, khẩn cấp một Super Admin có xác thực bổ sung, manual close, phân quyền phát thông báo.
- [x] S7 chốt bù chính xác deadline cần hành động, passive wait vẫn chạy và ngoại lệ 15 ngày khi app không dùng được.
- [x] Ghi rõ những giả định chưa duyệt và các blocker liên quan tài chính/pháp lý/quyền/kỹ thuật.
- [ ] Audit endpoint, DB, role `SystemAdmin` và UI source hiện tại có bằng chứng.
- [ ] Chủ dự án phê duyệt các chính sách/contract còn block trước triển khai thao tác nhạy cảm.
- [ ] Có kết quả build/integration/E2E thực chứng cho phiên bản đã triển khai.
