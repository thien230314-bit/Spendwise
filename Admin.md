# SPENDWISE — ADMIN HỆ THỐNG (WEB) — PRODUCT SPECIFICATION

> **Sản phẩm:** SpendWise V1.0 (bản phát hành ứng dụng đầu tiên)  
> **Tệp:** `Admin.md` · **Ngày chốt phạm vi:** 18/10/2026  
> **Trạng thái:** **PRODUCT SCOPE ĐÃ ĐƯỢC CHỦ DỰ ÁN DUYỆT** — 6 menu chính và 34 chức năng con, mô hình quyền B. **KHÔNG PHẢI xác nhận Backend/UI đã hoàn thiện hoặc được phép sửa code.**  
> **Đối tượng:** Admin hệ thống trên Web; không phải chủ nhóm, không phải Super Admin.  
> **Tài liệu liên quan:** `User.md` mới nhất (nội dung biên tập v1.2 ngày 18/10/2026; là nguồn nghiệp vụ User cho bản phát hành sản phẩm V1.0), `SuperAdmin.md`; `SpendWise_BanGiao_ChatMoi_Admin_User_20261018(1).docx` chỉ là tư liệu lịch sử/bàn giao, không ưu tiên hơn quyết định mới.  
> **Định hướng triển khai:** Web dành cho Admin/Super Admin; Mobile chỉ dành cho User; website công khai chỉ marketing. Không tạo Web User app hoặc tính năng Money Request.

## 0. Cách đọc đặc tả và nguồn sự thật

- **[CHỐT]**: đã được chủ dự án duyệt trong cuộc trao đổi về 2 role, bao gồm quyền và S7 liên quan.
- **[CẦN KIỂM TRA SOURCE]**: chức năng/luồng có phạm vi sản phẩm nhưng chưa chứng minh có API, dữ liệu, phân quyền hoặc E2E thực.
- **[BLOCKER]**: chưa có contract/ngưỡng/kỹ thuật được duyệt; **không** tự điền số hoặc bật hành động ghi tiền/quyền.
- **[THIẾT KẾ ĐỀ XUẤT]**: cách tổ chức màn hình, trường dữ liệu hoặc quy tắc kỹ thuật bổ trợ để triển khai dễ hiểu; cần xem xét khi audit UI/API, không tự xem là chính sách mới được duyệt.
- Một mục con có thể là **tab, bộ lọc, ô tìm kiếm hoặc màn chi tiết**; 34 mục con **không** có nghĩa cần xây 34 URL hay thay giao diện Web đã khóa ngay.
- `User.md` được ưu tiên đối với tiền/quỹ/bầu cử/lịch sử User. Nếu phát hiện điểm còn mơ hồ hoặc xung đột, **đánh dấu và dừng hành động bị ảnh hưởng**; không lấy đặc tả Admin để lấp chỗ trống của `User.md`.
- Tài liệu giao diện Admin cũ ghi 13 màn: Overview, Users, Transactions Audit, Ledger, Risk, Disputes, Support, Security Audit, Banking, Reconciliation, Health, Configuration, Feature Flags. Đó là **inventory lịch sử**, không phải xác nhận tất cả API ghi đã chạy. Cấu trúc được duyệt ở đây tổ chức lại chúng theo nghiệp vụ.

## 1. Nhiệm vụ, ranh giới và quyền truy cập

### 1.1. Admin làm gì?

1. Xem công việc, cảnh báo và dữ liệu vận hành có thật.
2. Tìm, tra cứu và hỗ trợ người dùng; thực hiện một số **biện pháp bảo vệ tài khoản thông thường được kiểm soát**.
3. Điều tra quỹ nhóm, giao dịch, ví, ngân hàng và hồ sơ hỗ trợ.
4. Theo dõi **từng khoản chi** đã được báo cáo/tạm dừng; chuyển quyết định cấp quỹ hoặc tài chính nhạy cảm cho Super Admin.
5. Tra cứu thông báo và lịch bảo trì do Super Admin công bố để hỗ trợ User.

### 1.2. Admin tuyệt đối không làm gì?

- Không chuyển tiền thay User, điều chỉnh số dư, sửa/xóa ledger hoặc đánh dấu giao dịch tiền thành công thủ công.
- Không tạo khoản nạp/hoàn/chi quỹ giả, tự định công thức hoàn, thay đổi quyền lợi góp, chuyển tiền quỹ ra ngân hàng hoặc dùng số dư khóa của người khác.
- Không tự **khóa tiền đi của toàn bộ quỹ**, tự đổi chủ, tự kick thành viên, tự sửa tổng dự kiến `T` hoặc phiếu bầu; chủ nhóm là quyền **nội bộ nhóm**.
- Không tự cấp/sửa quyền Admin, bổ nhiệm Super Admin hoặc thay cấu hình chính sách hệ thống.
- Không tự lên lịch/kích hoạt/kết thúc bảo trì; không tự phát thông báo toàn hệ thống hoặc chương trình ưu đãi.
- Không xem mật khẩu, OTP, PIN gốc, secret SePay hoặc thông tin cá nhân không liên quan đến công việc.
- Không truy cập dữ liệu chỉ nhờ đoán đúng mã `SPW...`.

### 1.3. Mô hình quyền B đã chốt

| Mức | Hành động | Admin | Bàn giao khi nào |
|---|---|---|---|
| P1 | Tra cứu hồ sơ và lịch sử | Có, khi có nhiệm vụ/quyền/lý do hợp lệ và audit | Vượt phạm vi xem dữ liệu |
| P2 | Phân công, hỗ trợ, thu thập chứng cứ, xử lý ticket thường | Có | Tranh chấp/vụ vượt quyền |
| P2 khẩn | Thu hồi phiên, hạn chế truy cập; **tạm chặn tiền đi từ tài khoản có căn cứ nghi bị chiếm quyền** | Có theo quy trình xác minh, lý do, thông báo, thời hạn, audit | Gia hạn/ngoại lệ lên Super Admin |
| P3 | Hạn chế tiền đi cấp **toàn quỹ**, can thiệp nhạy cảm | Không | Super Admin theo policy + contract |
| P4 | Ghi tiền/ledger ngoài contract, tự ý vượt quyền, xóa audit | Không | Không role nào được tự làm |

**[BLOCKER — CHÍNH SÁCH]** Thời lượng hạn chế khẩn cấp của Admin, tiêu chuẩn chứng cứ, luồng khi User không tự xác minh được, quy tắc tự hết hạn/khôi phục và biện pháp chống khóa ác ý chưa ấn định. Không tự chọn giá trị hoặc bỏ yêu cầu kiểm soát. Với giao dịch đã được Backend nhận trước khi hạn chế, phải truy vết trạng thái, không hủy/hoàn giả hoặc thực hiện lại một cách mù quáng.

### 1.4. Quyền xem dữ liệu đã chốt

- Chỉ khi có **ticket được phân công, cảnh báo rủi ro đang điều tra hoặc nhiệm vụ kiểm tra hợp lệ được cấp quyền**; hành động đọc dữ liệu nhạy cảm phải có lý do, phạm vi truy cập tối thiểu và nhật ký audit. Áp dụng cả với Super Admin.
- Mặc định hạn chế/che bớt email, định danh ngân hàng, số dư hoặc dữ liệu riêng tư khi không cần; chỉ mở phần có liên quan với hồ sơ.
- Danh sách User cơ bản có thể tìm kiếm theo quyền; mở **chi tiết** tài chính/nhóm là thao tác có audit riêng.
- Xuất chứng cứ chỉ cho phép đúng hồ sơ, theo quyền và phạm vi đã duyệt; **[BLOCKER]** quyền xuất dữ liệu nguyên bản, chính sách lưu giữ/xóa bản xuất và quyền tải về cần xác định trước khi mở nút.
- Audit tối thiểu cần actor/admin ID, quyền đã dùng, case/task ID, đối tượng/loại dữ liệu, thời điểm server, lý do, kết quả, thao tác (tra cứu/biện pháp/xuất); không đưa secret hoặc OTP vào log.

## 2. Sơ đồ điều hướng Web Admin V1.0 (6 menu / 34 mục con)

```
ADMIN HỆ THỐNG
├─ A1. Tổng quan
│  ├─ Số liệu hôm nay
│  ├─ Việc cần xử lý
│  ├─ Cảnh báo
│  └─ Hoạt động gần đây
├─ A2. Người dùng
│  ├─ Danh sách tài khoản
│  ├─ Chi tiết hồ sơ
│  ├─ Tài khoản bị hạn chế / khóa
│  ├─ Đăng nhập & thiết bị
│  └─ Lịch sử xử lý
├─ A3. Quỹ nhóm
│  ├─ Tra cứu mã nhóm SPW
│  ├─ Danh sách nhóm
│  ├─ Thành viên & chủ nhóm
│  ├─ Dòng thời gian nhóm
│  ├─ Góp / Chi / Hoàn quỹ
│  ├─ Bầu chủ nhóm & hoạt động
│  ├─ Nhóm đã giải tán
│  └─ Nhóm đang bị khiếu nại
├─ A4. Giao dịch & ví
│  ├─ Tất cả giao dịch
│  ├─ Tra cứu mã giao dịch
│  ├─ Ví người dùng
│  ├─ Giao dịch chờ / lỗi
│  ├─ Chi tiết sổ cái (chỉ đọc)
│  └─ Truy vết giao dịch
├─ A5. Khiếu nại & hỗ trợ
│  ├─ Ticket mới
│  ├─ Đang xử lý / phân công
│  ├─ Chờ người dùng phản hồi
│  ├─ Hồ sơ điều tra khiếu nại quỹ nhóm
│  ├─ Đề xuất chuyển Super Admin
│  ├─ Đã giải quyết
│  └─ Mẫu phản hồi
└─ A6. Thông báo
   ├─ Thông báo nghiệp vụ
   ├─ Lịch sử gửi
   ├─ Lỗi gửi
   └─ Mẫu thông báo
```

### A1. Tổng quan — nhìn thấy việc cần làm

| Mục | Dữ liệu/tác vụ cần có | Vì sao tồn tại và giới hạn |
|---|---|---|
| A1.1 Số liệu hôm nay | User, giao dịch, quỹ và ticket với mốc thời gian rõ ràng; dẫn tới danh sách nguồn | Tránh dashboard số liệu không thể kiểm chứng; không bịa doanh thu |
| A1.2 Việc cần xử lý | Ticket được giao, khiếu nại chờ, vấn đề quá hạn, liên kết mở case | Để Admin bắt đầu làm việc ngay, không phải mở nhiều menu |
| A1.3 Cảnh báo | Giao dịch lỗi, nạp tiền sai lệch, quỹ bị báo cáo, thông báo và bảo trì có ảnh hưởng | Chỉ hiển thị cảnh báo có event/source đáng tin; cảnh báo không tự là kết luận gian lận |
| A1.4 Hoạt động gần đây | Dòng sự kiện xử lý ticket, quyết định bảo vệ, chuyển cấp, bảo trì | Truy trách nhiệm; ẩn phần dữ liệu không liên quan quyền |

**Chấp nhận:** Nhấn cảnh báo phải mở đúng hồ sơ; dữ liệu rỗng hiển thị rỗng, không demo fake; số liệu gắn nguồn và mốc cập nhật. **[CẦN KIỂM TRA SOURCE]** telemetry và endpoint tổng hợp.

### A2. Người dùng — tra cứu, bảo vệ tài khoản

| Mục | Dữ liệu/tác vụ cần có | Vì sao tồn tại và giới hạn |
|---|---|---|
| A2.1 Danh sách tài khoản | Tìm theo ID/email được phép; lọc hoạt động/hạn chế | Xác định đúng User; trạng thái là bộ lọc, không bắt buộc trang riêng |
| A2.2 Chi tiết hồ sơ | Hồ sơ, 1 Main + tối đa 2 ví, nhóm hiện/từng tham gia, bạn bè liên quan | Hỗ trợ và điều tra; riêng danh sách bạn bè hoặc tài chính cần mục đích hợp lệ |
| A2.3 Bị hạn chế/khóa | Lý do, thời điểm, mức hạn chế, người tạo, tình trạng xem xét | Tránh khóa vô hạn; hành động khẩn theo mục 1.3; khóa dài hạn phải chuyển cấp |
| A2.4 Đăng nhập & thiết bị | Sự kiện phiên, đăng nhập, thu hồi phiên có căn cứ | Xử lý mất máy/bị chiếm quyền; không hiện token/secret |
| A2.5 Lịch sử xử lý | Các ticket, quyết định hạn chế, yêu cầu xác minh, actor | Không xử lý trùng hoặc che giấu hành động của Admin |

**Luồng khẩn cấp đã duyệt:** Nhận báo cáo → kiểm tra nhiệm vụ/xác minh → ghi căn cứ → thu hồi phiên khi phù hợp → tạm chặn **giao dịch tiền đi từ tài khoản** nếu có căn cứ bị chiếm quyền → lưu audit/thông báo → gửi Super Admin khi kéo dài hoặc ngoại lệ. Không được làm mất quyền tiền của tài khoản trong quỹ hoặc tự động thay chủ vì bị khóa. 15 ngày chủ nhóm không hoạt động chịu quy tắc `User.md` và ngoại lệ bảo trì của `SuperAdmin.md`.

**[BLOCKER]** Thời hạn mức chặn, xử lý hoàn tiền hợp lệ trong khi tài khoản hạn chế, xác minh khi User mất mọi yếu tố xác thực; cần contract trước khi bật P2 khẩn trên production.

### A3. Quỹ nhóm — tra cứu và điều tra (hai loại nhóm)

| Mục | Dữ liệu/tác vụ cần có | Vì sao tồn tại và giới hạn |
|---|---|---|
| A3.1 Tra cứu mã nhóm SPW | Ô tìm kiếm nổi bật, ví dụ `SPW000001`, kiểm tra quyền | Hỗ trợ tra cứu nhanh; đúng mã không cấp quyền truy cập |
| A3.2 Danh sách nhóm | Lọc loại 1 cố định (≤30), loại 2 tự do (≤6), trạng thái và thời gian | Không gộp hai nghiệp vụ có quy tắc tiền khác nhau |
| A3.3 Thành viên & chủ nhóm | Người sáng lập bất biến, một chủ hiện tại, thành viên hiện/từng tham gia, rời/kick | Xác định thay quyền/khiếu nại; Admin không tự đổi thành viên/chủ |
| A3.4 Dòng thời gian nhóm | Mời/chấp nhận, quyền chi, chủ nhóm, thay `T`, mở bầu, bỏ phiếu, rời/kick, giải tán | Có actor, thời gian, nguyên nhân, liên kết giao dịch; chỉ đọc |
| A3.5 Góp/Chi/Hoàn quỹ | Góp từ ví cá nhân, chi, phần tiền dư khóa/hold, hoàn về ví, trạng thái/ledger | Truy vết quyền lợi mà không tự tính lại theo công thức giả |
| A3.6 Bầu chủ nhóm & hoạt động | Chuyển chủ/bầu lại sau 15 ngày hoặc quá hạn phản hồi: **cả hai loại**; biểu quyết hoạt động mới và chỉnh `T`: **chỉ loại 1** | Tránh lỗi nhầm "biểu quyết hoạt động loại 2"; không tự thay phiếu/chủ |
| A3.7 Nhóm đã giải tán | Lưu mã SPW, người sáng lập, hai lịch sử, kết quả hoàn/quỹ về 0 | Phục vụ khiếu nại sau giải tán; không tính 10 nhóm active |
| A3.8 Nhóm đang bị khiếu nại | Lọc report/hold/case, trạng thái xử lý và nguồn | Ưu tiên rủi ro; một case dùng chung với A5, không tạo bản sao |

**Quy tắc User phải giữ:**
- Mỗi User ≤10 nhóm hoạt động, ≤100 bạn; mã SPW toàn hệ thống không tái sử dụng. Góp quỹ **chỉ từ ví cá nhân SpendWise**; hoàn **nội bộ về ví cá nhân**, mặc định Main; không hoàn trực tiếp ngân hàng.
- Loại 1 có `T`, nghĩa vụ góp theo VND nguyên, ghế đồng dư cố định, dư cá nhân khóa khác điều kiện khóa chi cả hoạt động; chủ nhóm tự quyết giữ/tăng/giảm `T` trong giới hạn bảo toàn, **không biểu quyết về `T`**. Hoạt động đã đóng không chi thêm; muốn mở hoạt động mới phải biểu quyết riêng 24 giờ; tỷ lệ chấp thuận >50% cử tri hợp lệ.
- Loại 2 góp linh hoạt, tối đa 6 người; **không áp biểu quyết hoạt động mới của loại 1**. Rời/kick/giải tán phải bảo toàn quyền lợi ròng, công thức phức tạp vẫn BLOCKER.
- Bầu lại chủ nhóm là **hai vòng**, khác biểu quyết hoạt động mới. Khi chủ nhóm không dùng app 15 ngày hoặc không phản hồi việc bắt buộc quá hạn theo quy tắc User, mở bầu lại; không giáng quyền tức thì, không tự kick chủ cũ hoặc tước quyền tiền.
- **Hai lịch sử riêng:** Lịch sử hoạt động nhóm và Lịch sử giao dịch, liên kết nhau bằng tham chiếu. Không sửa/xóa bản ghi gốc. Người đã rời chỉ xem phần lịch sử liên quan trong thời gian tham gia theo User.md; Admin có quyền điều tra rộng hơn nhưng bắt buộc nhiệm vụ và audit.

**Khoản chi bị báo cáo:** khi khoản chi lớn vào **Chờ thực hiện**, hệ thống thông báo thành viên; thành viên báo cáo → chỉ **khoản đó** sang **Tạm dừng**, mở ticket và giữ chỗ tiền. Admin điều tra từng khoản, yêu cầu chứng cứ, chuyển Super Admin khi cần. **Admin không được tự hạn chế giao dịch tiền đi của cả quỹ**. Chủ nhóm/người đề nghị có thể hủy khoản chờ/tạm dừng theo User.md; việc hủy không xóa báo cáo/audit. Cơ chế giải phóng/thực hiện tiền bị giữ là **[BLOCKER]** trước hành động ghi tiền.

**Tình huống nghiệm thu:** tìm nhóm theo ID và theo User (kể cả đã rời/đã giải tán); kiểm tra đổi chủ và biểu quyết đúng hai loại; một report không khóa cả nhóm; rời/kick không hoàn xong thì không mất tư cách và quyền tiền; lịch sử hai loại tách biệt.

### A4. Giao dịch & ví — truy vết và đối chiếu chỉ đọc

| Mục | Dữ liệu/tác vụ cần có | Vì sao tồn tại và giới hạn |
|---|---|---|
| A4.1 Tất cả giao dịch | Chuyển ví nội bộ, chuyển User, thu/chi, nạp tiền, góp/chi/hoàn quỹ; lọc theo loại/trạng thái | Một điểm tra cứu thống nhất, không biến chuyển ví thành Income/Expense |
| A4.2 Tra cứu mã giao dịch | Transaction ID/ref, chủ thể/nguồn-đích đã được che theo quyền | Tìm đúng khi nhận mã từ ticket; chống IDOR |
| A4.3 Ví người dùng | Main và tối đa 2 ví phụ, trạng thái, số dư và liên kết các giao dịch | Xem riêng ví SpendWise và ngân hàng liên kết; không điều chỉnh balance |
| A4.4 Giao dịch chờ/lỗi | Pending, failed, needs-review tùy enum có thật; retry history | Tập trung xử lý lỗi; không mặc định pending là thất bại |
| A4.5 Chi tiết sổ cái | Các ledger transactions/entries, số tiền nguyên VND, tham chiếu | Chỉ đọc; không bù một entry lệch bằng tay |
| A4.6 Truy vết giao dịch | Timeline yêu cầu/xác thực/kết quả/server/webhook/ledger | Phân biệt lỗi tích hợp và lỗi sổ nội bộ; phụ thuộc telemetry hiện có |

**[CẦN KIỂM TRA SOURCE]** Hệ thống cũ có màn Banking/Reconciliation/Ledger. Trong phạm vi Admin mới, có thể xem trạng thái nạp/SePay qua tham chiếu read-only từ giao dịch; kiểm soát đối soát cấp toàn hệ thống nằm ở S4 của `SuperAdmin.md`. Nếu một màn cũ chưa có API, đánh dấu GAP chứ không tuyên bố "đã vận hành".

**Trạng thái kết quả:** chỉ Backend là nguồn sự thật; không fake hoàn tiền hoặc chuyển thành công. Điều tra nạp TestMode khác với hỗ trợ chuyển ngân hàng thực; đường payout thực/đối tác/pháp lý chưa được chứng minh.

### A5. Khiếu nại & hỗ trợ — một hồ sơ xuyên suốt

| Mục | Dữ liệu/tác vụ cần có | Vì sao tồn tại và giới hạn |
|---|---|---|
| A5.1 Ticket mới | Yêu cầu User từ Mobile, liên kết ID/mã SPW/mã giao dịch nếu có | User không cần nhớ mã nhóm mới khiếu nại được |
| A5.2 Đang xử lý/phân công | Assignee, thời điểm tiếp nhận, ưu tiên và deadline nếu có policy | Không để hai Admin xử lý mâu thuẫn; không tự phát SLA chưa chốt |
| A5.3 Chờ User phản hồi | Yêu cầu chứng cứ, đối thoại, ghi mốc thời gian | Phân biệt cần User bổ sung với Admin bỏ quên |
| A5.4 Hồ sơ điều tra quỹ | Tham chiếu nhóm, dòng thời gian, góp/chi/hoàn, bầu chủ, chứng từ, ticket | Không sao chép lịch sử tiền sang bản mutable; liên kết tới nguồn bất biến |
| A5.5 Đề xuất chuyển Super Admin | Hồ sơ, căn cứ, phạm vi đề nghị, rủi ro, người gửi | Super Admin ra quyết định P3; Admin không tự phê duyệt hành động tiền |
| A5.6 Đã giải quyết | Kết quả, lý do, phản hồi User, tình trạng có thể mở lại | Đóng ticket **không** chứng minh đã chuyển/hoàn tiền thành công |
| A5.7 Mẫu phản hồi | Mẫu đã duyệt, có biến/định danh phù hợp | Trả lời nhất quán; không sửa thông báo toàn hệ thống |

**[THIẾT KẾ ĐỀ XUẤT]** Vòng đời ticket: Mới → Đã tiếp nhận/Đang xử lý ↔ Chờ User/Chờ cấp trên → Đã giải quyết; có thể mở lại theo policy. Đây là tổ chức UI, không yêu cầu Backend tự đổi enum hiện có khi chưa audit. Không gộp trạng thái case với trạng thái giao dịch tiền.

**Luồng 2-role:** User gửi report → A5 nhận → A3/A4 tra cứu có lý do → thu chứng cứ → Admin giải quyết phần thuộc P2 hoặc chuyển S3 → nhận quyết định có audit → phản hồi User. Case ID giữ nguyên xuyên hai vai trò.

### A6. Thông báo — tra cứu và hỗ trợ, không phát hàng loạt

| Mục | Dữ liệu/tác vụ cần có | Vì sao tồn tại và giới hạn |
|---|---|---|
| A6.1 Thông báo nghiệp vụ | OTP (chỉ metadata, không OTP), giao dịch, quỹ, bầu chủ, bảo trì, hỗ trợ | Biết User phải nhận sự kiện gì; không giả thông báo tiền thành công |
| A6.2 Lịch sử gửi | Loại/kênh, thời gian, trạng thái do provider/backend trả | Phân biệt gửi/chuyển tới/đã đọc; không cam kết mọi push đều đọc |
| A6.3 Lỗi gửi | Thông báo lỗi, retry được quản trị bởi cơ chế hợp lệ | Chuyển điều tra nếu lỗi ảnh hưởng deadline/quyền User |
| A6.4 Mẫu thông báo | Xem/sử dụng mẫu phục vụ phản hồi, không sửa mẫu global | Tránh Admin tự phát quảng cáo hoặc thông báo bảo trì sai |

**Bảo trì:** Admin thấy banner lịch sắp tới, phạm vi và thời gian ước tính do Super Admin phát; hỗ trợ ticket tương ứng; **không** được tạo/kích hoạt/gia hạn/kết thúc bảo trì. Trong thời gian bảo trì hệ thống, chỉ những tác vụ quản trị được allowlist an toàn (nếu có contract) mới truy cập, không vượt chặn tiền.

## 3. Quy tắc liên vai trò và tình huống biên

1. **Phân quyền Backend là bắt buộc**: UI ẩn nút không thay thế 401/403 và kiểm tra role/permission, ownership, case scope. Không dùng quyền `SystemAdmin` chưa xác minh như thể đồng nghĩa Super Admin.
2. **Một trường hợp — một định danh:** `case_id` gắn `user_id`, `group_id`, `transaction_id` và nguồn bằng chứng liên quan. Mọi quyết định/đọc dữ liệu phải truy vết.
3. **Tiền và trạng thái không nhập nhằng:** `pending`, `on-hold`, `failed`, `completed`, `cancelled` theo enum có thật; không đổi kết quả nghiệp vụ bằng hành động đóng ticket.
4. **Lịch sử bất biến:** User/chủ nhóm/Admin/Super Admin không xóa/sửa ledger gốc hay event history; nếu có cơ chế điều chỉnh hợp lệ thì bằng sự kiện/bút toán mới, qua phê duyệt và contract.
5. **Tài khoản chủ nhóm bị hạn chế:** không làm mất chức tự động, không lấy quyền lợi tài chính. Cơ chế bầu 15 ngày và phản hồi quá hạn vẫn theo User.md; bảo trì làm thay đổi cách tính 15 ngày theo ngoại lệ đã được duyệt trong SuperAdmin.md.
6. **Tính riêng biệt nhóm:** không áp dụng ngưỡng nghĩa vụ góp, T hoặc biểu quyết hoạt động mới cho loại 2; không gán số dư của chủ cũ cho chủ mới.
7. **Đầu ra điều tra:** tóm tắt chứng cứ + quyết định thuộc quyền + người chịu trách nhiệm + phản hồi User; không giả định xét duyệt nghĩa vụ bồi thường ngoài đời.
8. **Lỗi bảo trì/ngoại tuyến:** Mobile thông báo theo quy tắc S7. Admin không tự tái kích hoạt giao dịch hoặc thay deadline một cách thủ công.

## 4. Tiêu chí nghiệm thu sản phẩm & kiểm thử dự kiến

> Danh mục kiểm thử là **yêu cầu nghiệm thu tương lai**, không phải kết quả PASS hiện tại.

| Mã | Trường hợp nghiệm thu | Kỳ vọng |
|---|---|---|
| ADM-01 | Admin không có case/nhiệm vụ thử mở chi tiết tài chính User | Bị từ chối/không lộ dữ liệu; ghi sự kiện truy cập theo policy |
| ADM-02 | Admin có ticket hợp lệ tra cứu User và nhóm đã giải tán | Xem đúng phạm vi, case/actor audit đầy đủ |
| ADM-03 | Tìm mã `SPW000001` không đủ quyền | Không lộ thành viên/số dư dù mã đúng |
| ADM-04 | Một nhóm loại 2 xuất hiện tab biểu quyết hoạt động mới | Không được áp dụng quy tắc chỉ dành loại 1 |
| ADM-05 | Khoản chi bị User báo cáo | Chỉ khoản đó tạm dừng/hold, mở case, không đóng băng cả nhóm |
| ADM-06 | Admin cố hạn chế tiền đi toàn quỹ | Backend từ chối, chỉ Super Admin có workflow hợp lệ |
| ADM-07 | Bảo vệ tài khoản nghi bị chiếm quyền | Kiểm tra căn cứ, hạn chế có thời hạn, thông báo, audit, không sửa tiền |
| ADM-08 | Admin cố sửa balance/ledger/quyền chủ nhóm | Backend từ chối, không tạo side effect |
| ADM-09 | Đóng ticket với khoản hoàn pending | Ticket và giao dịch có trạng thái độc lập, không fake completed |
| ADM-10 | Giao dịch đang xử lý lúc mất kết nối/bảo trì | Không tự gán success/failed; tra cứu lại Backend authoritative |
| ADM-11 | Audit việc xem thông tin nhạy cảm | Lưu actor/lý do/đối tượng/phạm vi/thời điểm/việc đã làm |
| ADM-12 | Admin đăng nhập Web khi bảo trì toàn hệ thống | Chỉ vào khu vực quản trị được cho phép, không gọi API tiền bị chặn |

## 5. Contract và phụ thuộc — chưa được tự triển khai

### 5.1. BLOCKER sản phẩm/tài chính chuyển tiếp từ `User.md`

- **FIN-01** Quyền lợi ròng `w_i` và thứ tự xử lý góp/chi/hoàn/rút/chuyển kỳ/tiền dư khóa cho cả hai loại nhóm; đặc biệt khi loại 2 rút cá nhân và có chi chung.
- **FIN-02** Rời/kick/giải tán khi có khoản hold, pending, đã hoàn một phần hoặc quỹ thiếu số dư thực; không tự hoàn trùng hoặc âm quỹ.
- **FIN-03** Thay `T` và nghĩa vụ sau khi hoạt động đã chi; phân biệt quyền thay T của chủ loại 1 với quyền duyệt Admin.
- **FIN-04** Người/cơ chế giải quyết khoản chi bị report/tạm dừng, thời hạn xử lý, thực hiện/hủy, giữ chỗ và điều kiện release hold; không tự cho Admin nút “duyệt chi” tiền thật.
- **GOV-01** Thời lượng các vòng bầu chủ, tình huống không ứng viên, không chấp nhận quyền, bầu thất bại, chủ cuối cùng, tài khoản khóa và trạng thái tiền trong bầu cử.
- **BANK-01** Luồng tiền thật/đối tác được cấp phép, trạng thái rút/payout hai chiều; TestMode không đủ chứng minh tiền thật. Không nhận tiền thật/đưa ra cam kết thật nếu chưa được xác minh pháp lý và đối tác.

### 5.2. BLOCKER quyền/bảo mật & dữ liệu

- **ADM-POLICY-01:** Điều kiện, thời lượng, giới hạn và kết thúc hạn chế tài khoản/giao dịch tiền đi do Admin thực hiện, xác minh khẩn, kháng nghị, chống lạm dụng.
- **ADM-ACCESS-01:** Permission matrix, cách liên kết ticket/task/risk-alert với quyền xem, masking, audit retention, quyền xuất và nơi lưu file xuất.
- **ADM-API-01:** Gap inventory màn→endpoint→DB→permission→log→test cho 6 menu; telemetry nạp/SePay và notification delivery; không fake dữ liệu.
- **ADM-OPS-01:** Trạng thái Admin Web cũ đã khóa và báo cáo E2E read-only không chứng minh thao tác ghi; audit source trước mọi thay đổi.
- **ADM-ROLE-01:** Role `SystemAdmin` lịch sử cần audit/mapping trước khi dùng; tài liệu này **không** cho phép migration/tự đổi role.

## 6. Bàn giao kỹ thuật và quy trình triển khai sau đặc tả

- **Bước 1 — READ-ONLY audit:** Codex xác minh Backend/DB/permissions/case/ledger, Antigravity kiểm tra Admin UI và Mobile consumers; lập bảng Evidence/Gap theo file/endpoint/test; không sửa source.
- **Bước 2 — Contract:** Chủ dự án giải quyết riêng các BLOCKER ảnh hưởng tiền, hold, quyền, bầu cử và thời hạn; có ví dụ số học, state machine, test cases.
- **Bước 3 — Batch được duyệt:** mới triển khai một phần, chỉ sửa đúng thư mục được giao; xác minh API và role server-side, không tự redesign UI đã khóa.
- **Bước 4 — E2E cô lập:** hai User và Admin tra cứu ticket/nhóm/chuyển tiền/góp/chi/kick/hoàn/bầu, có log, state và ledger; không dùng dữ liệu production để ép PASS.
- **Lưu ý:** Đây là tài liệu đặc tả, không phải prompt chạy agent hoặc lệnh xóa User Web cũ. Việc xóa source legacy, marketing hay B2B là scope khác, cần phê duyệt riêng.

## 7. Checklist phạm vi hoàn tất của `Admin.md`

- [x] Phạm vi 6 menu/34 mục con và cách phân nhóm đã được phê duyệt ở cấp sản phẩm.
- [x] Phân cấp quyền P1/P2/P3/P4, đặc biệt bảo vệ tài khoản khẩn cấp và không tự hạn chế toàn quỹ.
- [x] Quyền xem dữ liệu tối thiểu và audit cho các hồ sơ hợp lệ.
- [x] Các quy tắc quỹ nhóm và nghiệp vụ tài chính không được tự thay đổi.
- [x] Liên kết trách nhiệm A5 với Super Admin S3, trách nhiệm thông báo/bảo trì với S7.
- [ ] Nguồn code/API/DB/role cụ thể được kiểm chứng trên workspace hiện tại.
- [ ] FIN/GOV/BANK/ACCESS/POLICY blocker được chốt đủ điều kiện triển khai.
- [ ] UI và các luồng ghi được nghiệm thu bằng test/E2E thực tế.
