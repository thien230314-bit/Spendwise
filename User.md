# SPENDWISE V1.0 — ĐẶC TẢ KỸ THUẬT VÀ TÀI LIỆU API PHÂN HỆ USER

> **Phiên bản:** V1.0 (Production Candidate)  
> **Đối tượng sử dụng:** Frontend Web & Mobile Client Developers (Codex / Engineering Team).  
> **Trạng thái đối chiếu:** Tài liệu đồng bộ API User theo code controller/DTO trên nhánh `main` ngày 30/10/2026. Kết quả Newman 306 requests / 343 assertions là bằng chứng kiểm thử lịch sử, không tự chứng nhận mọi luồng vận hành hiện tại. **Free Fund V1: đặc tả nghiệp vụ chốt 01/11/2026 ở mục 1.3, 1.3.1 và 3.6 mục 10 (phân biệt với nghiệm thu code). Fixed Fund V2: đặc tả nghiệp vụ mục tiêu chốt 01/11/2026 tại [`fixed.md`](fixed.md); các bảng API Fixed ở mục 3.6 phản ánh backend cũ trước khi triển khai logic mới.**  
> **Ranh giới tài liệu:** Thuần tài liệu kỹ thuật, API contract, ràng buộc dữ liệu và mã lỗi. Không chứa lý thuyết kiến trúc chung, tiếp thị hay hướng dẫn framework UI.

---

## 1. NGUYÊN TẮC NGHIỆP VỤ & BẢO MẬT BẮT BUỘC (CORE BUSINESS RULES)

### 1.1. Hai vai trò, vòng đời tài khoản, quyền sở hữu

- **Chỉ có hai vai trò hệ thống sử dụng trong V1.0:** `User` (mobile User) và `Admin` (web quản trị). Mã enum `SystemRole` trong Domain còn giữ giá trị legacy `SystemAdmin=0`, `SuperAdmin=3` để tương thích dữ liệu cũ, **không phải hai role sản phẩm đang triển khai**. `Fund Owner`/chủ nhóm là một **User** có quyền đối với quỹ, không phải Admin.
- `UserStatus`: `Active` (hoạt động), `Restricted` (vẫn đọc dữ liệu và dùng chức năng phi tài chính; thao tác ghi nợ tài chính bị chặn theo từng handler), `Locked` (chặn đăng nhập/phiên, thu hồi session), `Suspended` (tài khoản bị đình chỉ). `MustChangePassword` có thể buộc đổi mật khẩu trước khi tiếp tục.
- Đăng ký email + OTP, thiết lập PIN và đăng nhập hai bước (mật khẩu → challenge → PIN); JWT access + refresh rotation; danh sách/thu hồi phiên; khôi phục mật khẩu/PIN và Account Recovery. Đổi email phải xác minh OTP, avatar thật multipart.
- Mỗi yêu cầu sửa dữ liệu phải xác thực user từ JWT; **không** nhận `userId` tự khai báo là nguồn phân quyền. Lỗi xác thực 401/403, lỗi nghiệp vụ thường 400/409, quá giới hạn 429; xử lý theo `code` từ API, không so sánh chuỗi `message`.

### 1.2. Ví, sổ cái, tiền và khả năng nhà cung cấp

- Ví chính được backend tạo khi onboard. **Tối đa 3 ví cá nhân, bao gồm ví chính; ví đã đóng vẫn tính vào giới hạn**. API chỉ cho tạo ví phụ `type` hợp lệ với tên và `isDefault`, **không** cho client tự tạo ví Main. Đóng ví giữ lịch sử/ledger.
- Tiền và lịch sử ghi theo ledger hai vế; không dùng giao diện làm nguồn sự thật số dư. `WalletDto` dùng `availableBalance`, không phải thuộc tính `balance`. Quy trình kiểm soát ngân hàng hoạt động (`active_bank_required`) có thể chặn thao tác tài chính tùy use case.
- Giao dịch tài chính cần `idempotencyKey` ở **trường request body theo DTO tương ứng** (một số clients thêm `X-Idempotency-Key`, nhưng header **không thay thế** trường body khi DTO có yêu cầu). Khi retry cùng mục đích, dùng lại key; không sinh key mới cho cùng một intent. Dùng PIN khi DTO/handler bắt buộc; không giữ PIN trên client.
- P2P là chuyển tiền **nội bộ SpendWise**; bank-direct là tạo lệnh/hướng dẫn chuyển khoản ngân hàng, **không tự ghi giảm ví SpendWise**. SePay Test Mode cho kiểm chứng luồng sandbox và ledger; không đồng nghĩa API rút/chuyển tiền ngân hàng thật đã được cấp quyền.

### 1.3. Quỹ nhóm Cố định V2 & Quỹ nhóm Tự do (Group Funds)
1. **Quỹ nhóm Cố định V2 (Fixed Fund V2) — ĐẶC TẢ NGHIỆP VỤ MỤC TIÊU ĐÃ CHỐT NGÀY 01/11/2026 (CHƯA XÁC NHẬN TRIỂN KHAI):**
   - **Tài liệu nguồn chính thức:** [`fixed.md`](fixed.md), gồm vòng đời, ví dụ tiền, ngoại lệ, bầu Owner, cam kết tin tưởng và ma trận 30 tình huống. Nếu bảng API theo code cũ dưới mục 3.6 khác đặc tả nghiệp vụ, **`fixed.md` là quy tắc sản phẩm cần triển khai**, không khẳng định API đã hỗ trợ.
   - **Tạo quỹ:** Owner đặt mục tiêu, tên và thời lượng góp cá nhân; trước khi Owner chốt danh sách/phân bổ, quỹ hiển thị `0đ/target`, chưa chạy deadline. Ít nhất 2 người để chốt, tối đa 30 thành viên theo giới hạn Fixed. Target **phải chia hết đúng 1 VND cho số ghế** (9 triệu/3 hợp lệ, 10 triệu/3 từ chối), không tự rải 1đ phần dư ở bước xác nhận nghĩa vụ. Đã chốt thì khóa target, nghĩa vụ và hạn góp của kỳ.
   - **Lời mời và cam kết:** Owner mời trực tiếp; Member đề xuất mời phải qua Owner duyệt; người được mời tự chấp nhận. Trước khi vào Fixed cần hộp cam kết **"Bạn có tin tưởng nhóm này không?"** với lựa chọn từ chối/tham gia; cam kết không xóa quyền khiếu nại hay hợp thức hóa chi sai. Người vào kỳ mới phải thấy nghĩa vụ, mục tiêu và hạn góp trước khi bị ràng buộc.
   - **Góp/Auto-Kick:** Chưa đủ target thì không được chi. Member không góp đủ đúng hạn: hệ thống tự Kick và hoàn entitlement hợp lệ, để lại ghế trống **giữ nguyên nghĩa vụ**, người cũ không phải góp bù. Từ Auto-Kick đầu mở **72 giờ tìm người thay** cho toàn kỳ, không reset; người nhận ghế hợp lệ sát hạn vẫn được **đủ thời lượng góp cá nhân**. Người thay là membership episode mới. Nếu không thay được, kỳ thất bại và giải tán; Owner có quyền kết thúc thất bại sớm khi còn ghế trống. **Nếu chính Owner thiếu nghĩa vụ khi hết hạn**, tự động quyết toán hoàn và giải tán, không đòi Owner online/PIN.
   - **Chi tiền:** Đạt target thì báo Owner, không tự đóng kỳ. Owner ON/OFF `CanSpend` cho Member. Người có quyền chi được thực hiện Expense trực tiếp nhưng bắt buộc có **người nhận + mục đích hợp lệ**, không rút vốn cá nhân trá hình; khóa protected excess, các khoản giữ/đang hoàn và khoản chưa đối soát. Member không tùy ý Out trong kỳ bình thường; Owner không được tùy ý Kick người còn entitlement để chiếm dụng tiền.
   - **Đóng kỳ/chuyển kỳ:** Owner chủ động đóng hoạt động, sau đó giải tán/hoàn tiền hoặc đề xuất kỳ mới. **Không áp dụng đa số Yes > 50%**: Member tự chọn Có tiếp tục hoặc Không (hoàn tiền ngay rồi rời); không phản hồi **24 giờ** thì tự hoàn và rời. Entitlement snapshot phải cố định từ trước khi mở phản hồi; khoản đã hoàn không đảo ngược dù Owner hủy đề xuất. Kỳ mới ít nhất 2 người; nếu chỉ còn Owner, cho **72 giờ tìm thành viên** hoặc giải tán. Owner chọn lại tên, target, hạn góp cho kỳ mới; kỳ cũ bất biến; tổng cộng **tối đa 3 kỳ**, không có kỳ 4.
   - **Hoàn/đồng lẻ/carry-over:** Quyền lợi hoàn dựa vốn thực tế sau chi chung và lịch sử refund, không mặc định chia bằng nhau chỉ vì nghĩa vụ ngang nhau. Bảo vệ tiền vượt nghĩa vụ do carry-over; chia đồng lẻ theo ghế cố định, tổng hoàn phải bằng số dư hợp lệ. Một khoản góp/hoàn/carry-over không được ghi hai lần; giải tán thành công yêu cầu ledger quỹ về **0đ**.
   - **Bầu Owner:** Owner vắng **15 ngày** có bằng chứng: Member mở bầu. Đợt 1 **12h** chọn người cao phiếu nhất, có thể phiếu trống; Owner cũ không bầu hoặc can thiệp. Đợt 2 người thắng có **12h xác nhận nhận quyền bằng PIN**; không nhận/quá hạn/hòa phiếu/không có người kế nhiệm thì bầu thất bại. Member có **24h Out/nhận hoàn**, hết hạn bắt buộc quyết toán/giải tán; nếu chỉ Owner cũ thì hoàn về ví Owner. Chủ mới không tự thấy dữ liệu riêng kỳ cũ. Owner cũ quay lại trong bầu hợp lệ chỉ được xem.
   - **Tranh chấp & lịch sử:** Cam kết tin tưởng **không thay thế** quyền gửi khiếu nại lên Admin; can thiệp cần bằng chứng, phân quyền và audit, không hứa tự động thu hồi tiền đã chi. Lịch sử các kỳ cũ bất biến, người mới không xem nội dung riêng kỳ cũ; người rời còn chứng từ của mình; dữ liệu giải tán lưu cho quản trị.
   - **Trạng thái bằng chứng:** toàn bộ mô tả trên là **ĐÃ CHỐT NGHIỆP VỤ, CHƯA TRIỂN KHAI/CHƯA KIỂM THỬ theo logic mới**. Các API ở mục 3.6 mục 6–9 tiếp tục mô tả code hiện có để đối chiếu, không được áp logic đa số cũ vào đặc tả mới.

2. **Quỹ nhóm Tự do (Free Fund V1) — ĐẶC TẢ NGHIỆP VỤ MỚI CHỐT 01/11/2026, CHỜ TRIỂN KHAI:**
   - **Phạm vi:** Quỹ chung góp tự nguyện, không có nghĩa vụ đóng `T/n`; tối đa **6 người** gồm Owner và Member. Mỗi User tối đa 10 nhóm/quỹ đang hoạt động theo giới hạn chung hiện có. Chủ quỹ và thành viên đều được góp thêm tiền. Mời bạn bè: Owner hoặc Member có thể mời người đã kết bạn; người nhận phải tự chấp nhận, **không cần Owner duyệt lời mời do Member gửi**.
   - **Không có** bầu cử khi Owner vắng mặt 15 ngày, chuyển quyền Owner, Activity Proposal của Fixed, `Pending24h`, `Suspended` nội bộ hoặc phán xử tranh chấp quỹ riêng. Khiếu nại giao dịch sử dụng chức năng hỗ trợ chung SpendWise. **Owner muốn rời thì phải giải tán quỹ**; không được chuyển chủ.
   - **Hai nút Member:** `Yêu cầu chi chung` (`Expense`) và `Yêu cầu rút vốn cá nhân` (`Withdrawal`). Mỗi người, trong mỗi quỹ, chỉ được có **1 yêu cầu đang hoạt động cho mỗi loại** (tối đa 2 loại khác nhau). Nút đã gửi bị khóa; phía server phải từ chối tạo trùng. Member xem yêu cầu bản thân; Owner xem và duyệt/từ chối yêu cầu của toàn nhóm. Lịch sử bao gồm đã duyệt, hoàn tất, đang chờ, từ chối, hủy và lý do; tất cả thành viên thấy lịch sử giao dịch tài chính chung theo phạm vi tham gia.
   - **Expense của Member:** Owner duyệt **cấp quyền chi đúng số tiền yêu cầu**, **chưa chuyển tiền về ví và chưa trừ số dư quỹ**. Quyền chi đã duyệt chỉ thực sự giảm quỹ sau khi thanh toán bên ngoài thành công và được đối soát; không dùng một quyền hai lần. Quyền chưa dùng hết hạn sau **7 ngày tính từ thời điểm duyệt** và có thể hủy sớm; đang thanh toán phải xác minh kết quả trước khi hủy. Khoản chi ngoài là **mở rộng chờ nhà cung cấp**, không tuyên bố đã vận hành thật hoặc cần demo nộp báo cáo; SePay hiện tại giữ nguyên.
   - **Withdrawal của Member:** Owner duyệt thì chuyển phần vốn thực tế của người đó về ví cá nhân ngay; không được vượt vốn còn lại; không chia là chi phí chung. **Nếu quỹ còn bất kỳ quyền chi đã duyệt nhưng chưa thanh toán, không được phê duyệt rút tiền cá nhân của bất kỳ Member nào** (yêu cầu rút vẫn chờ, không tự từ chối). Chỉ mở lại khi các quyền này hoàn tất hoặc hủy. Khi chi thành công làm yêu cầu rút chờ vượt vốn, tự động hủy và thông báo.
   - **Quyền chi đồng thời:** Tổng các quyền chi đã duyệt chưa sử dụng không được vượt số dư quỹ; Owner chi trực tiếp chỉ được dùng số dư **chưa phân bổ cho các quyền chi đó**. Nếu một người rời quỹ được hoàn tiền mà tổng quyền chi không còn đủ, hủy các quyền chưa dùng **mới nhất trước**, giữ các quyền cũ hợp lệ khi còn đủ tiền; thông báo cho người bị hủy. Nếu đang xử lý thanh toán, chưa được hoàn tiền trong tình trạng không xác định.
   - **Vốn và hoàn tiền SpendWise:** Mỗi lượt tham gia có phần vốn riêng; góp thêm làm tăng vốn đúng thời điểm; Withdrawal và hoàn tiền giảm vốn người nhận; **chi chung thành công** được chia theo tỷ lệ vốn thực tế ngay trước thời điểm chi, làm tròn nguyên VND sao cho tổng phân bổ bằng đúng số tiền chi. Người mới/góp thêm không chịu khoản chi xảy ra trước đó. Rời rồi tham gia lại là **lượt thành viên mới về mặt tài chính**, nhưng giữ lịch sử cũ để đối soát. Vốn 0đ vẫn được gửi yêu cầu chi chung; nếu chi thành công thì chi phí phân bổ cho các thành viên có vốn dương.
   - **Rời/Kick/Giải tán:** Member tự rời không cần Owner duyệt, nhưng phải tự hủy hoặc chờ giải quyết các yêu cầu của mình còn `AwaitingOwnerReview`. Quyền chi đã duyệt chưa dùng của chính Member được hủy lúc rời. Kick chỉ khi các yêu cầu chờ của người bị kick đã được duyệt/thực hiện hoặc từ chối/hủy hợp lệ. Giải tán hủy mọi yêu cầu chưa duyệt và quyền chi chưa dùng, thông báo rồi hoàn tiền; giao dịch đã hoàn tất không đảo lại. Hành động rời/kick/giải tán phải đối soát đúng tiền và chống đồng thời.
   - **Ranh giới bằng chứng:** Đây là **chính sách sản phẩm đã chốt**, **KHÔNG PHẢI trạng thái đã có trong backend**. Code trên `main` còn luồng bầu cử/chuyển chủ Free, Expense duyệt chuyển về ví và phép tính vốn ròng không xử lý được người góp sau chi cũ; cần sửa và kiểm thử mới được coi là hoàn thành.

#### 1.3.1. Free Fund — bảng tình huống chi tiết và kết quả đã chốt

| Tình huống | Kết quả bắt buộc |
|---|---|
| A góp 600.000đ, B góp 400.000đ; chi chung thành công 200.000đ | Chi theo tỷ lệ 60%/40%: A chịu 120.000đ, B chịu 80.000đ; A còn **480.000đ**, B còn **320.000đ**, tổng quỹ **800.000đ**. |
| B rời sau khoản chi trên | Hoàn B **320.000đ**; A còn 480.000đ. |
| B đang có 320.000đ, góp thêm 200.000đ rồi rời ngay, chưa chi mới | Hoàn B **520.000đ**; khoản góp mới không chịu phí cũ. |
| A/B còn tổng vốn 800.000đ sau chi; C mới góp 200.000đ rồi rời trước khi có chi mới | Tổng quỹ 1.000.000đ; hoàn C **đủ 200.000đ**. Không chia lại chi phí cũ cho C. |
| B góp 400.000đ, rút cá nhân 100.000đ trước mọi chi chung | B còn **300.000đ**; lần chi chung kế tiếp lấy trọng số vốn mới của B (không còn 400.000đ). |
| B rời và được hoàn 320.000đ, sau đó vào lại góp 200.000đ | Lượt mới bắt đầu với **200.000đ**; rời trước chi mới hoàn 200.000đ. Lượt cũ không cộng trừ sang lượt mới. |
| B còn 320.000đ, rút cá nhân 300.000đ thành công trước khoản chi chung | B còn **20.000đ**, vẫn chịu chi phí chung về sau theo tỷ trọng 20.000đ trên tổng vốn lúc chi. |
| Khoản chi chung 100.000đ hoàn tất trước khi Owner duyệt yêu cầu rút 300.000đ của B (B trước chi có 320.000đ, vốn sau chi còn 280.000đ) | Tự động **`Cancelled`** yêu cầu rút 300.000đ, thông báo thiếu vốn; B có thể tạo yêu cầu mới tối đa 280.000đ. |
| B có `Expense` đã duyệt 100.000đ nhưng chưa thanh toán; C đang chờ rút 50.000đ | Owner **không được duyệt bất kỳ `Withdrawal` nào trong quỹ**. Tiền và vốn chưa giảm; quyền chi được thanh toán/hủy mới mở khóa rút. |
| Member có hai yêu cầu: 1 Expense và 1 Withdrawal cùng chờ | Hợp lệ vì khác loại; mọi yêu cầu thứ hai cùng loại trong khi loại đó còn hoạt động đều bị chặn, kể cả gọi API trực tiếp. |
| Owner duyệt Expense 200.000đ và Member không thanh toán trong 7 ngày | Quyền chi tự hết hạn và hủy; không trừ quỹ; thông báo, mở lại quyền xin chi và xét duyệt rút nếu không còn quyền chi khác. Không hủy mù khi `PaymentProcessing`. |
| Quỹ 1.000.000đ (A 400.000đ, B 300.000đ, C 300.000đ). B đã được duyệt chi 800.000đ chưa dùng; C rời và được hoàn 300.000đ | Quỹ còn 700.000đ, hủy quyền chi 800.000đ của B vì không đủ tiền; thông báo B có thể yêu cầu lại; **C được rời/hoàn 300.000đ**. |
| Quỹ 800.000đ, duyệt cho B chi 500.000đ trước, C chi 300.000đ sau; hoàn thành viên khiến quỹ còn 600.000đ | Giữ quyền 500.000đ của B, **hủy quyền 300.000đ mới hơn của C**; số dư 600.000đ, 100.000đ không bị phân bổ quyền. |
| Quỹ 1.000.000đ, B có quyền chi chưa dùng 800.000đ; Owner muốn chi trực tiếp 500.000đ | **Không cho chi 500.000đ**; Owner chỉ được chi trong 200.000đ chưa phân bổ, hoặc hủy hợp lệ quyền 800.000đ trước. |
| B có vốn 0đ, muốn gửi Expense 100.000đ | Vẫn được xin chi. Nếu hợp lệ và chi thành công, phần 100.000đ được phân bổ cho các thành viên có vốn dương; B không thể rút vốn riêng khi vốn 0đ. |
| Member có `AwaitingOwnerReview` khi muốn rời; Owner không hoạt động lâu | Member tự hủy request chờ (không cần Owner duyệt hủy), sau đó được rời và nhận vốn còn lại. Không bầu cử hay tự chuyển chủ do Owner vắng 15 ngày. |
| Member tự rời khi có quyền chi `ApprovedUnused` của chính mình | Hủy quyền chi chưa dùng rồi rời; không trừ tiền chưa chi. Các quyền chi của người khác chỉ hủy nếu cần thiết để quỹ đủ hoàn tiền. |
| Owner kick Member có request chờ | Phải giải quyết request chờ trước; Owner có thể từ chối, không buộc duyệt chi. Không để quyền chi chưa dùng tiếp tục hoạt động sau kick. |
| Owner giải tán quỹ còn request chưa duyệt hoặc quyền chi chưa dùng | Hủy request/quyền chưa dùng và gửi thông báo; hoàn tiền theo vốn thực, tổng số dư sau giải tán = **0đ**. Giao dịch đang xử lý cần đối soát trước. |
| Owner muốn rời dù B/C muốn duy trì quỹ | Owner **phải giải tán** và hoàn tiền; B/C có thể tạo quỹ mới. Không chuyển chủ. |

**Quy tắc tính đồng lẻ:** VND là số nguyên; chia chi phí chung theo vốn hiện thời, phân bổ phần đồng dư bằng thứ tự ổn định và có thể kiểm chứng (ví dụ thứ tự `seat`), không tạo/mất 1đ. Sau mỗi giao dịch đã quyết toán: **tổng vốn còn lại của các thành viên đang có quyền sở hữu = số dư quỹ**. Preview/Leave/Kick/Dissolve phải sử dụng cùng dữ liệu quyền lợi. Không chuyển quyền lợi từ lượt tham gia cũ sang lượt mới.

**Thứ tự trạng thái nghiệp vụ đề xuất để triển khai:** `Expense`: `AwaitingOwnerReview` → `ApprovedUnused` → `PaymentProcessing` → `Completed`; có nhánh `Rejected`, `Cancelled`, `Expired` (có thể biểu diễn hết hạn bằng `Cancelled` kèm lý do); `Withdrawal`: `AwaitingOwnerReview` → `Completed` hoặc `Rejected`/`Cancelled`. Những tên chưa tồn tại trong code là **đề xuất contract**, không phải endpoint/DTO hiện hữu.

**Yêu cầu thanh toán ngoài:** quyền chi đúng số tiền được duyệt, trả kết quả từ đơn vị cung cấp thật; thiếu provider thì không xác nhận thành công, không tự trừ tiền hoặc ghi `Completed` giả. Đây là hạng mục bổ sung **không bắt buộc demo/thử thanh toán thực trong báo cáo**, nhưng khi triển khai để sử dụng thực tế vẫn phải kiểm thử an toàn. **SePay không thay đổi.**

**Lịch sử và hiển thị:** Member có hai nút chi/rút, nút đang có request/quyền chi hiệu lực phải vô hiệu; thêm `Xem yêu cầu` (đã chấp nhận/đang xử lý/từ chối/đã hủy, phân biệt đã duyệt với đã thanh toán). Owner có hàng chờ phê duyệt và lịch sử mọi yêu cầu. Toàn nhóm thấy lịch sử góp/chi/rút/hoàn thực tế (không nhầm request với giao dịch thành công). Giữ thông tin requester, reviewer, thời gian, lý do, mã đối soát và thông báo thay đổi.

**Ranh giới bảo toàn:** thay đổi nghiệp vụ Fixed Fund V2 được phê duyệt độc lập trong [`fixed.md`](fixed.md); không sửa Fixed, legacy `/api/groups`, SePay, ví và dữ liệu tài chính thật chỉ vì việc thay logic Free. Phải phân biệt **`ĐÃ CHỐT NGHIỆP VỤ`** và **`ĐÃ TRIỂN KHAI / ĐÃ KIỂM THỬ`** trong mọi tài liệu, UI và báo cáo.

### 1.4. Hỗ trợ, tranh chấp, khiếu nại

- Hai loại hồ sơ: **Support** (`POST /api/cases/support` với `category,priority,subject,description,linkedTransactionId?`) và **Dispute** (`POST /api/cases/disputes` với `linkedTransactionId,reason,description?`). Cả hai có danh sách, chi tiết, tin nhắn và đính kèm, riêng biệt theo chủ sở hữu.
- Người dùng có thể trao đổi với Admin trong hồ sơ, đính kèm tài liệu. Backend chỉ cho liên kết giao dịch thực sự thuộc chính người tạo. Khi Admin cập nhật trạng thái hồ sơ **không được mặc nhiên suy ra hoàn tiền tự động**; xử lý điều chỉnh/hoàn tiền là nghiệp vụ tài chính riêng có điều kiện và chứng từ.
- `POST /api/v1/funds/requests/{id}/report` với `Expense` Fixed V2 đã `Completed` có thể tạo SupportCase gắn đúng giao dịch, **không tự đảo ledger hoặc hoàn tiền**. Các trường hợp report trạng thái legacy vẫn có guard riêng.

## 2. Quy ước API, dữ liệu và frontend

1. **URL:** Base URL backend + đường dẫn tuyệt đối `/api/...`. JSON body mặc định `Content-Type: application/json`; upload avatar/evidence/case/AI attachment dùng `multipart/form-data`. Phần lớn API yêu cầu `Authorization: Bearer <accessToken>`, các endpoint đăng ký/đăng nhập/khôi phục là anonymous.
2. **DTO là nguồn sự thật:** Thuộc tính JSON camelCase theo tên record C#, ví dụ `idempotencyKey`, `securityPin`, `sourceWalletId`, `recipientUserId`; không đoán bằng mẫu UI hoặc bản Postman cũ. Enum input thường là chuỗi đúng chính tả như `"Free"`, `"Expense"`.
3. **Thời gian:** Backend trả ISO 8601 UTC/offset; client chỉ đổi múi giờ khi hiển thị. Số tiền VND truyền dưới dạng JSON number theo DTO `decimal`/`long`, không gửi chuỗi đã định dạng có dấu chấm/phẩy.
4. **Lỗi:** Phân biệt 400 input, 401 hết phiên, 403 hạn chế quyền, 404 ID không thuộc chủ sở hữu, 409 trạng thái xung đột và 429 giới hạn yêu cầu. Không tự retry lệnh ghi tiền bằng idempotency key mới. UI phải hiển thị lỗi rõ và không giả lập thành công.
5. **Realtime/thông báo:** Dùng API notifications làm dữ liệu gốc, realtime/push chỉ là tín hiệu làm mới; không coi push đã giao thành ledger commit.
6. **Phạm vi:** Mục `/api/groups` là **legacy compatibility**, khác quỹ `/api/v1/funds`. Hệ thống chỉ có vai trò hoạt động `User`/`Admin`. **Fixed V2: dùng [`fixed.md`](fixed.md) làm đặc tả nghiệp vụ chốt; bảng mục 3.6 là contract backend cũ và chưa nghiệm thu các luồng mới**.

## 3. DANH MỤC API PHÂN HỆ USER

### 3.1. Đăng ký, đăng nhập, PIN, phiên, khôi phục

**Nguồn:** `AuthController`, `SecurityPinController`, `AccountRecoveryController`; DTO `Identity/AuthContracts.cs`.

| API | Body chính xác / kết quả | Quy tắc UI |
|---|---|---|
| `POST /api/auth/register` | `{fullName,email,phone,password,confirmPassword}` → `EmailVerificationPendingResponse {status,expiresAt,resendAfterSeconds}` | Email OTP 8 số, không cấp phiên trước khi xác minh. |
| `POST /api/auth/email-verification/verify` | `{email,otp,deviceName?}` → `{challengeToken,status,expiresAt}` | `status` chỉ bước PIN cần làm, không tự suy đoán từ ID. |
| `POST /api/auth/email-verification/resend` | `{email}` → trạng thái chờ và thời gian gửi lại | Chống spam/cooldown. |
| `POST /api/auth/login` | `{identifier,password,deviceName?}` → `LoginChallengeResponse` | `identifier` có thể là email hoặc SpendId; chưa phải access token. |
| `POST /api/auth/pin/setup` | `{challengeToken,pin,confirmPin}` → `AuthResponse` | Thiết lập PIN lần đầu. |
| `POST /api/auth/pin/verify` | `{challengeToken,pin,deviceTrustToken?}` → `AuthResponse` | Đây là **bước hoàn tất đăng nhập**, khác `/api/security-pin/verify`. |
| `POST /api/auth/refresh` | `{refreshToken,deviceName?}` → `AuthResponse` | Xoay refresh token, thay cặp token sau mỗi lần refresh hợp lệ. |
| `POST /api/auth/logout` | Thu hồi refresh/session của phiên theo request controller | Xóa credentials lưu trên thiết bị khi đăng xuất. |
| `GET /api/auth/sessions` | `SessionDto[]` | Danh sách thiết bị/phiên của chính user. |
| `DELETE /api/auth/sessions/{sessionId}` | `204` | Thu hồi đúng phiên được chọn. |
| `POST /api/auth/change-password` | `{currentPassword,newPassword}` → `204` | Không có `confirmPassword` trong DTO backend của endpoint này. |
| `POST /api/auth/forgot-password` | `{identifier}` → `202` | Phản hồi trung tính, không dò tài khoản. |
| `POST /api/auth/reset-password` | `{identifier,otp,newPassword}` → `204` | OTP dùng một lần; xử lý phiên cũ theo backend. |
| `POST /api/auth/pin/forgot` | `{identifier}` | Gửi luồng khôi phục PIN an toàn qua email. |
| `POST /api/auth/pin/reset/validate` | `{token}` → `{valid,expiresAt}` | Kiểm tra token khôi phục, không nhầm OTP đăng ký. |
| `POST /api/auth/pin/reset` | `{token,newPin,confirmPin}` → `204` | Token reset PIN chỉ dùng một lần. |
| `GET /api/security-pin/status` | `{isSet}` | Kiểm tra trạng thái PIN từ phiên đã đăng nhập. |
| `POST /api/security-pin/verify` | `{pin}` → `204` | Kiểm tra PIN giao dịch, **không cấp access token**. |
| `PUT /api/security-pin` | `{currentPin,newPin,confirmPin}` → `204` | Đổi PIN khi đang đăng nhập. |
| `POST /api/account-recovery` | `{fullName,phone,description,emailHint?}` → `202 Accepted`, `AccountRecoverySubmissionResponse {message}` | Đề nghị Admin xét khôi phục, không tự mở khóa; phản hồi trung tính chống dò tài khoản. |

**Định dạng token:** `AuthResponse` gồm `accessToken, refreshToken, accessTokenExpiresAt, user, deviceTrustToken?`. `UserProfile` gồm `id,fullName,email,spendId,phone,systemRole,status,mustChangePassword,preferredLanguage,preferredTheme,defaultCurrency,createdAt,lastLoginAt,engagementEmailOptIn,avatarUrl`. Không hiển thị `expiresIn` hoặc `mainWalletBalance` như trường của chính các DTO này. PIN/mật khẩu/OTP không log ra console/analytics.

**Passkey (WebAuthn):** `POST /api/auth/passkeys/register/options`, `POST /api/auth/passkeys/register/verify` (cần đăng nhập); `POST /api/auth/passkeys/login/options`, `POST /api/auth/passkeys/login/verify` (anonymous); `GET /api/auth/passkeys`, `DELETE /api/auth/passkeys/{id}`. Client lấy challenge/options từ server và hoàn tất bằng browser/native authenticator; không được giả lập xác thực sinh trắc học. Đăng nhập passkey trả `AuthResponse`.

### 3.2. Hồ sơ, bạn bè, thiết bị và thông tin người dùng

**Hồ sơ cá nhân (`UsersController`):**

| Endpoint | Contract và quy tắc |
|---|---|
| `GET /api/users/me` | Trả **`UserProfile`** với `preferredTheme,preferredLanguage,defaultCurrency,engagementEmailOptIn,avatarUrl`, không có `preferences` object lồng và `mainWalletBalance`. Số dư lấy từ Wallet/Dashboard. |
| `PUT /api/users/me/preferences` | Body `{preferredLanguage:"vi-VN",preferredTheme:"system",defaultCurrency:"VND",engagementEmailOptIn:false}` → **`UserProfile`**. Cờ biometric/notification hardware không phải thuộc tính DTO này. |
| `POST /api/users/me/avatar` | Multipart key **`file`**, JPG/PNG/WebP, tối đa 5 MB; trả `UserProfile` mới. |
| `POST /api/users/me/email/request` | Body `{newEmail}` → `EmailVerificationPendingResponse`. |
| `POST /api/users/me/email/confirm` | Body `{newEmail,otp}` → `UserProfile`. |
| `DELETE /api/users/me/sessions` | Thu hồi các phiên khác, `204`. |

**Bạn bè (`FriendsController`; tối đa 100 bạn bè):** Người dùng phải kết bạn thành công trước khi mời vào quỹ V1. Không lộ email/số điện thoại người lạ tùy tiện.

| Endpoint | Chức năng |
|---|---|
| `GET /api/friends/search?contact=...&kind=...` | Tìm chính xác theo `contact` (+ `kind` tùy chọn); **không phải** tham số `query`. |
| `GET /api/friends/discover?query=...&page=1&pageSize=20` | Khám phá user hệ thống, trả phân trang với thông tin công khai/relationshipStatus. |
| `GET /api/friends` | Danh sách bạn bè, tên gợi nhớ và đánh dấu yêu thích. |
| `GET /api/friends/requests/incoming` / `GET /api/friends/requests/outgoing` | Lời mời đến/đi. |
| `POST /api/friends/requests` | Body `{contact,message?,contactKind?}` (`SendFriendRequest`), không dùng `targetUserId` thay cho `contact`; chống tự mời và lời mời trùng. |
| `POST /api/friends/requests/{requestId}/accept` / `reject` | Nhận hoặc từ chối lời mời. |
| `DELETE /api/friends/requests/{requestId}` | Hủy lời mời do mình gửi. |
| `DELETE /api/friends/{friendUserId}` | Hủy kết bạn. |
| `PATCH /api/friends/{friendUserId}/preference` | Body `{nickname?,isFavorite}` theo `UpdateFriendPreferenceRequest(string? Nickname,bool IsFavorite)`; **không có `alias`**. |

**Thiết bị nhận push:** `PUT /api/mobile/push/installations/{installationId}` body `{platform,pushToken,deviceName?}` theo `RegisterMobilePushInstallationRequest`, trả `204`; `DELETE /api/mobile/push/installations/{installationId}` xóa đăng ký, trả `204`. `installationId` là GUID trong route. Đăng ký token **không phải** chứng nhận Expo Push đã giao thật.

### 3.3. Ví, thu/chi, chỉnh giao dịch, danh mục, bằng chứng, dashboard

**Ví cá nhân — `WalletsController`, `CreateWalletRequest`:**

- `GET /api/wallets` → `WalletDto[]`; `GET /api/wallets/{walletId}` → một ví.
- `POST /api/wallets`: `{name:"Ví du lịch",type:"Sub",isDefault:false}` → `WalletDto`. **Không có** `currency`/`targetAmount` trong request tạo ví. Main do hệ thống tạo; tối đa 3 ví cá nhân tính cả ví đã đóng. `WalletDto`: `id,name,type,currency,status,availableBalance,isDefault,createdAt`.
- `PUT /api/wallets/{walletId}`: `{name,type,isDefault}` → `WalletDto`; thay thông tin/tùy chọn ví, không được sửa số dư trực tiếp.
- `DELETE /api/wallets/{walletId}`: đóng ví có điều kiện, không xóa ledger/giao dịch lịch sử; không giả định đóng ví sẽ giải phóng suất 3 ví đã tạo.
- Các thao tác tài chính có thể yêu cầu ngân hàng Active, PIN, ví Active, kiểm tra số dư và idempotency. Trường `availableBalance` là số dư hiển thị.

**Thu chi — `TransactionsController`, `TransactionContracts.cs`:**

| API | Body, bộ lọc và tác dụng |
|---|---|
| `GET /api/transactions` | `page,pageSize,walletId?,categoryId?,type?,from?,to?,minimumAmount?,maximumAmount?,search?`; trả `PagedResult<TransactionDto>`. **Không dùng** `fromDate` thay `from`. |
| `GET /api/transactions/{transactionId}` | Chi tiết giao dịch, liên kết evidence, correction và phân bổ ví nếu có. |
| `POST /api/transactions/income` | `{walletId,categoryId,amount,description,occurredAt?,idempotencyKey?,securityPin?,evidenceIds?,counterparty?}` → `201 TransactionDto`. |
| `POST /api/transactions/expense` | Cùng `CreateManualTransactionRequest`, trừ tiền khi hợp lệ; không coi chỉ cần `walletId,amount` là đầy đủ điều kiện tài chính. |
| `POST /api/transactions/expense/multi-wallet` | `{amount,walletAllocations:[{walletId,amount},...],categoryId,description,occurredAt?,idempotencyKey?,securityPin?,evidenceIds?,counterparty?}`; tổng phân bổ phải đúng tổng khoản chi. |
| `PATCH /api/transactions/{transactionId}` | `{categoryId?,description,securityPin?,evidenceIds?,reason?,occurredAt?,walletId?,amount?,counterparty?,walletAllocations?}`; chỉnh sửa tài chính phải thực hiện **correction có sổ cái đảo/thay thế**, không sửa lịch sử bằng ghi đè tùy tiện. |

`TransactionDto` trả `id,walletId?,walletName?,categoryId?,categoryName?,type,status,amount,signedAmount,currency,description,counterpartyName?,occurredAt,ledgerTransactionId,relatedBusinessId?,correctionAllowance,correctionCount,canCorrect,corrections?,evidence?,correctionGrants?,walletAllocations?`. UI dùng `canCorrect` và quyền sửa được trả về, không tự cấp quyền.

**Danh mục cá nhân/hệ thống:** `GET /api/categories`, `GET /api/categories/{id}`, `POST /api/categories`, `PUT /api/categories/{id}`, `DELETE /api/categories/{id}`. Create/Update body `{name,kind,icon?,color?}`, với `kind` theo enum dữ liệu danh mục; `CategoryDto` có `isSystem,isActive`. Xóa có thể là archive khi đã được tham chiếu, không xóa lịch sử.

**Bằng chứng giao dịch:** `POST /api/evidence` (`multipart/form-data`, key `file`) → `EvidenceDto {id,originalFileName,contentType,length,contentHash,createdAt}`; `GET /api/evidence/{id}` tải file đã qua kiểm tra quyền. Dùng `evidenceIds` trên giao dịch/chỉnh sửa nơi hỗ trợ.

**Tổng quan:** `GET /api/dashboard` → `DashboardDto` tổng hợp từ backend. Không tính số dư hoặc tổng chi tiêu chỉ từ danh sách sample/mock trong UI.

**Xác thực giao dịch nâng cao bằng Passkey/WebAuthn (`TransactionAuthorizationsController`):**

| Endpoint | Body và response |
|---|---|
| `POST /api/transaction-authorizations/options` | `{transactionType,amount,currency?,sourceId,destinationId,idempotencyKey,payloadJson?}` → `TransactionAuthorizationOptionsResponse {authorizationId,challenge,transactionType,amount,currency,sourceId,destinationId,expiresAt,allowedCredentials}`. |
| `POST /api/transaction-authorizations/verify` | `{authorizationId,credentialId,signature,clientDataJSON?,authenticatorData?}` → `TransactionAuthorizationExecutionResult {success,authorizationId,status,transactionType,amount,currency,executedAt?,result?}`. |

Lấy challenge từ backend, ký bằng credential hợp lệ, chỉ coi giao dịch thực hiện khi backend xác minh và trả trạng thái tương ứng. Không nhầm việc nhận `options` với thực hiện tài chính.

### 3.4. Chuyển tiền giữa ví, P2P SpendWise và chuyển khoản ngân hàng trực tiếp

Ba luồng **khác nhau**, không được gộp một màn xác nhận có cùng hậu quả về số dư:

| Luồng / endpoint | Body chính xác | Kết quả |
|---|---|---|
| `POST /api/transfers/internal` | `{sourceWalletId,destinationWalletId,amount,description,idempotencyKey,securityPin}` | Chuyển giữa hai ví **cùng một user**, trả `201 TransferDto`, ghi ledger. |
| `POST /api/transfers/p2p` | `{sourceWalletId,recipientUserId,amount,description,idempotencyKey,securityPin}` | Chuyển giữa hai User SpendWise; `recipientUserId` là GUID lấy từ lựa chọn người nhận đã được backend trả; không dùng `recipientIdentifier` hay `note` làm trường DTO. Trả `201 TransferDto`. |
| `GET /api/transfers/recipients?phone=...` | Số điện thoại người nhận | **Tra cứu người nhận chuyển khoản ngân hàng**, trả `BankTransferRecipientDto {userId,fullName,bankBrand,accountHolder,maskedAccount}`, **không phải** API tra cứu người nhận P2P. |
| `POST /api/transfers` | `{recipientUserId,amount,note?,idempotencyKey}` | **Bank-direct**, tạo `BankDirectPaymentDto` và hướng dẫn thanh toán, **không trừ ví SpendWise khi vừa tạo**. |
| `GET /api/transfers` | `page?,pageSize?` | Lịch sử lệnh bank-direct, `PagedResult<BankDirectPaymentDto>`. |
| `GET /api/transfers/{transferId}` | `transferId` | Chi tiết lệnh bank-direct theo quyền gửi/nhận. |
| `POST /api/transfers/{paymentId}/confirm` | `{securityPin}` | Xác nhận luồng bank-direct theo kiểm tra ngân hàng và nghiệp vụ, không đồng nhất với chuyển P2P. |

`TransferDto` (internal/P2P) gồm `id,senderUserId,senderName,recipientUserId,recipientName,sourceWalletId,destinationWalletId,amount,currency,description,status,ledgerTransactionId?,completedAt?,replayed`. Bank-direct gồm thông tin ngân hàng đã che, `paymentReference,contextType,groupId?,status,senderConfirmedAt?,completedAt?,matchedBankTransactionId?`. UI không báo `Completed` khi mới có hướng dẫn/QR mà chưa có bằng chứng settlement.

### 3.5. Liên kết ngân hàng, lịch sử sao kê, SePay Test Mode

**Phân biệt nhà cung cấp thực và sandbox:** SePay Test Mode là môi trường mô phỏng nhà cung cấp nhưng API/đối soát có kiểm chứng. Không được trình bày Test Mode là rút tiền ngân hàng thật, cũng không cấp số dư ngân hàng thực tế khi provider không hỗ trợ. UI dựa trên trạng thái server (`Pending,Processing,PendingReconciliation,Completed,Failed,Cancelled` nếu được DTO/handler trả về), không tự đoán thành công.

**API quản lý kết nối thật (`BankConnectionsController`):**

| Endpoint | Body và tác dụng |
|---|---|
| `POST /api/bank-connections/link` | `{completionRedirectUri?,language:"vi",clientPlatform:"Web" hoặc "Mobile"}` → `BankLinkSessionDto {sessionId,hostedLinkUrl,expiresAt,purpose}`. Client phải dùng hosted link provider, không giả lập liên kết. |
| `GET /api/bank-connections` | Danh sách `BankConnectionDto`: `id,provider,accountEnvironment,bankBrandName,accountType?,accountHolderName?,maskedAccountNumber,status,linkedAt?,lastSyncedAt?`. |
| `GET /api/bank-connections/{id}` | Chi tiết kết nối thuộc người dùng. |
| `POST /api/bank-connections/{id}/unlink` | Body `CreateBankLinkRequest`, trả session; chờ xác nhận provider. |
| `POST /api/bank-connections/{id}/reactivate` | Body `CreateBankLinkRequest`; kích hoạt lại qua luồng provider. |
| `POST /api/bank-connections/{id}/sync` | Đồng bộ, `BankSyncDto {importedCount,duplicateCount,lastSyncedAt}`. |
| `GET /api/bank-transactions` | Các filter `page,pageSize,bankConnectionId?,direction?,from?,to?,search?`; phân trang. |
| `GET /api/bank-transactions/{id}` | Chi tiết giao dịch ngân hàng thuộc quyền. |
| `PATCH /api/bank-transactions/{id}/category` | `{categoryId}`, phân loại giao dịch sao kê. |

**SePay Test Mode (chỉ môi trường Development theo guard backend):**

| Endpoint | Body / trạng thái |
|---|---|
| `POST /api/bank-connections/test-mode/link-challenges` | Không body, trả `{challengeId,reference,expiresAt,instruction}` để thực hiện mô phỏng kết nối. |
| `POST /api/bank-connections/test-mode/link-challenges/{challengeId}/confirm` | Không body; xác nhận dựa trên giao dịch provider tương ứng, trả `BankConnectionDto`. |
| `GET /api/bank-connections/test-mode/linked-accounts` | `TestModeLinkedAccountDto[]` (gồm `accumulatedBalance` mô phỏng, **không** là số dư ngân hàng thật). |
| `POST /api/bank-connections/test-mode/wallet-top-ups` | `{bankConnectionId,walletId,amount}`, trả `WalletTopUpDto` có reference/thời hạn/trạng thái Pending. |
| `GET /api/bank-connections/test-mode/wallet-top-ups` | Danh sách top-up. |
| `POST /api/bank-connections/test-mode/wallet-top-ups/{topUpId}/confirm` | Chỉ ghi có ví khi provider xác thực giao dịch khớp; không tự cộng khi chưa đối soát. |
| `POST /api/bank-connections/test-mode/wallet-withdrawals` | `{bankConnectionId,walletId,amount,securityPin,idempotencyKey}`; dùng cơ chế reservation/ledger, không coi Pending là tiền đã về ngân hàng. |
| `GET /api/bank-connections/test-mode/wallet-withdrawals` | Danh sách `WalletWithdrawalDto`. |
| `GET /api/bank-connections/test-mode/wallet-withdrawals/{id}` | Chi tiết yêu cầu rút. |
| `POST /api/bank-connections/test-mode/wallet-withdrawals/{id}/confirm` | Xử lý xác minh settlement theo provider/test mode. |
| `POST /api/bank-connections/test-mode/wallet-withdrawals/{id}/cancel` | Hủy theo trạng thái cho phép và hoàn reservation tương ứng. |

**Mã lỗi UI cần phân biệt:** `active_bank_required`, `wallet_unavailable`, `insufficient_wallet_balance`, `active_withdrawal_in_progress`, `wallet_top_up_pending`, `withdrawal_pending_reconciliation`, `withdrawal_failed`, `idempotency_conflict`. Khi `PendingReconciliation`, UI hiển thị cần đối soát; không trả tiền hoặc thực hiện lại với key mới cho tới khi có kết quả xác minh.

### 3.6. Quỹ nhóm & Phân bổ Nghĩa vụ V2 (Groups & V1 Funds)

#### 1. `POST /api/groups`
- **Mô tả:** Tạo nhóm chi tiêu mới. Người tạo trở thành Chủ nhóm (`Owner`).
- **Quyền:** User (`Active`).
- **Request Body:**
```json
{
  "name": "Nhà Trọ 402",
  "description": "Nhóm sinh hoạt phòng 402",
  "currency": "VND"
}
```
- **Response `201 Created`:** `GroupDto`.

#### 2. `POST /api/groups/{groupId}/invites`
- **Mô tả:** Chủ nhóm (`Owner`) gửi lời mời tham gia nhóm cho người dùng khác (yêu cầu hai bên đã là bạn bè được chấp thuận, người được mời chưa quá giới hạn 10 nhóm hoạt động).
- **Quyền:** Chủ nhóm (`Owner`).
- **Request Body:**
```json
{
  "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
}
```
- **Response `200 OK`:**
```json
{
  "id": "uuid-invite",
  "groupId": "uuid-group",
  "groupName": "Nhà Trọ 402",
  "invitedUserId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "status": "Pending",
  "expiresAt": "2026-11-05T08:00:00Z",
  "token": "hex-token-string"
}
```
- **Lỗi thường gặp:** `404 NotFound` (`group_not_found`, `user_not_found`), `409 Conflict` (`already_member`, `active_group_limit`).

#### 3. `GET /api/groups/invites`
- **Mô tả:** Người dùng xem danh sách các lời mời vào nhóm đang chờ xử lý của chính mình.
- **Quyền:** User (`Active`).
- **Response `200 OK`:**
```json
[
  {
    "id": "uuid-invite",
    "groupId": "uuid-group",
    "groupName": "Nhà Trọ 402",
    "invitedUserId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "status": "Pending",
    "expiresAt": "2026-11-05T08:00:00Z",
    "token": "hex-token-string"
  }
]
```

#### 4. `POST /api/groups/invites/{inviteId}/accept?token={token}`
- **Mô tả:** Người dùng chấp thuận lời mời vào nhóm bằng `inviteId` và `token` tương ứng. Tự động thêm người dùng vào danh sách thành viên nhóm.
- **Quyền:** User được mời (`Active`).
- **Query Params:** `token=hex-token-string` (bắt buộc).
- **Response `200 OK`:** Trả về chi tiết `GroupDto` của nhóm vừa gia nhập.
- **Lỗi thường gặp:** `404 NotFound` (`invite_not_found`, `group_not_found`), `409 Conflict` (`invite_unavailable`, `already_member`, `active_group_limit`, `group_unavailable`), `403 Forbidden` (`group_member_ineligible`).

#### 5. `POST /api/groups/invites/{inviteId}/reject`
- **Mô tả:** Người dùng từ chối lời mời vào nhóm.
- **Quyền:** User được mời (`Active`).
- **Response `200 OK`:**
```json
{
  "id": "uuid-invite",
  "groupId": "uuid-group",
  "groupName": "Nhà Trọ 402",
  "invitedUserId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "status": "Rejected",
  "expiresAt": "2026-11-05T08:00:00Z",
  "token": "hex-token-string"
}
```
- **Lỗi thường gặp:** `404 NotFound` (`invite_not_found`), `409 Conflict` (`invite_unavailable`).

#### 5a. Kiểm kê 24 endpoint còn lại của `/api/groups` (Legacy)

> `GroupsController` có tổng cộng **29 endpoint** (5 mục 1–5 bên trên + 24 endpoint dưới đây). Đây là nhóm tương thích, **không thay thế các API V1 Fixed/Free**. Các body/DTO phải dùng contract riêng của legacy.

| API legacy | Chức năng |
|---|---|
| `GET /api/groups`; `GET /api/groups/{groupId}`; `PUT /api/groups/{groupId}` | Danh sách, chi tiết, sửa nhóm |
| `DELETE /api/groups/{groupId}/members/{memberUserId}`; `POST /api/groups/{groupId}/ownership/{memberUserId}` | Loại thành viên, chuyển chủ |
| `PUT /api/groups/{groupId}/receiving-bank` | Cài đặt tài khoản nhận tiền |
| `GET /api/groups/{groupId}/fund`; `GET /api/groups/{groupId}/fund/transactions` | Số dư và lịch sử quỹ |
| `POST /api/groups/{groupId}/fund/contributions` | Góp tiền |
| `GET /api/groups/{groupId}/fund-requests`; `POST /api/groups/{groupId}/fund-requests`; `POST /api/groups/{groupId}/fund-requests/{requestId}/pay` | Yêu cầu đóng góp và thanh toán |
| `GET /api/groups/{groupId}/fund-rounds`; `POST /api/groups/{groupId}/fund-rounds`; `POST /api/groups/{groupId}/fund-rounds/{roundId}/pay` | Vòng thu tiền và thanh toán |
| `GET /api/groups/{groupId}/expenses`; `POST /api/groups/{groupId}/expenses`; `GET /api/groups/{groupId}/activities` | Chi phí và lịch sử |
| `GET /api/groups/{groupId}/deletion-request`; `POST /api/groups/{groupId}/deletion-requests`; `POST /api/groups/{groupId}/deletion-requests/{requestId}/vote` | Yêu cầu xóa nhóm và bỏ phiếu |
| `POST /api/groups/{groupId}/deletion-requests/{requestId}/cancel`; `POST /api/groups/{groupId}/deletion-requests/{requestId}/refunds/{refundId}/payment`; `POST /api/groups/{groupId}/deletion-requests/{requestId}/finalize` | Hủy xóa, xác nhận hoàn tiền và hoàn tất giải thể |

#### 6. Quỹ cố định V2 — Hợp đồng API HIỆN HỮU trước khi triển khai logic mới: xác nhận và đóng góp

> **Không nhầm API hiện hữu với nghiệp vụ đã chốt:** Theo [`fixed.md`](fixed.md), tạo Fixed cần thời hạn góp cá nhân, chốt danh sách >=2, chia hết tuyệt đối và khóa nghĩa vụ/ghế; sau hạn có Auto-Kick, 72 giờ tìm thay, giải tán khi thất bại. Những hành vi này **chưa có đủ trong các route/DTO hiện tại**; bảng bên dưới là bằng chứng đối chiếu trước khi sửa code.

| API | DTO/body và điều kiện |
|---|---|
| `POST /api/v1/funds` | Tạo Fixed: `{"name":"Quỹ định kỳ","kind":"Fixed","targetVnd":600000,"securityPin":"<PIN>"}` → `V1FundDto`. |
| `POST /api/v1/funds/{id}/obligations/confirm` | Chỉ **chủ quỹ**, `{"targetVnd":600000,"securityPin":"<PIN>"}` → `V1FinanceDto`; xác nhận revision nghĩa vụ cho tập thành viên và target hiện hành. |
| `GET /api/v1/funds/{id}/contribution-allocation` | **Xem trước, không ghi sổ:** `V1ContributionAllocationDto {fundId,targetVnd,basis:"CurrentMembershipPreview",postingEnabled:false,members:[{userId,seat,requiredVnd}]}`. Không có `canSpend` hoặc số tiền đã góp trong response này. |
| `GET /api/v1/funds/{id}/finance` | `V1FinanceDto {fundId,revision,targetVnd?,balanceVnd,configurationCurrent,contributionsEnabled,spendingState,obligations:[{userId,seat,requiredVnd,paidVnd,remainingVnd,lockedExcessVnd}]}`. |
| `POST /api/v1/funds/{id}/contributions` | Thành viên đóng bằng `{"walletId":"<wallet-guid>","amountVnd":200000,"idempotencyKey":"<unique-key>","securityPin":"<PIN>","obligationRevision":1,"purpose":"Đóng chu kỳ"}`. **Lấy revision từ finance thực tế, không hardcode 1.** |
| `GET /api/v1/funds/{id}/contributions?page=1&pageSize=20` | `V1ContributionDto[]` có `id,fundId,userId,walletId,amountVnd,obligationRevision,ledgerTransactionId,createdAt,replayed`. |

**Kiểm tra Fixed:** không đóng vượt `requiredVnd-paidVnd`; `obligationRevision` hiện hành; ví `Active` thuộc chính thành viên và VND, PIN hợp lệ, intent idempotent. Đổi danh sách thành viên hoặc target có thể khiến revision/bảng nghĩa vụ cần xác nhận lại và làm sai lệch `configurationCurrent`.

#### 7. Quỹ cố định V2 — Quyền và chi tiêu

| API | Nội dung |
|---|---|
| `GET /api/v1/funds/{id}/members` | `V1MemberDto[]`: `userId,seat,isOwner,canSpend,joinedAt,fullName,avatarUrl?,effectiveCanSpend,individualContributionCompleted,groupFundingCompleted,canManageSpendingPermission`. |
| `PUT /api/v1/funds/{id}/members/{userId}/spending-permission` | **Chủ quỹ** `{"enabled":true,"securityPin":"<PIN>"}` → `204`. Chỉ đổi quyền khi thành viên có nghĩa vụ hợp lệ và đã đóng đủ; chủ quỹ không cần cấp quyền cho chính mình. |
| `POST /api/v1/funds/{id}/requests` | `{"kind":"Expense","amountVnd":100000,"recipientUserId":"<user-guid>","purpose":"Chi chung","idempotencyKey":"<unique-key>","securityPin":"<PIN>"}` → `V1FundRequestDto.state:"Completed"` khi đủ điều kiện, chi trực tiếp, **không tạo yêu cầu chờ chủ quỹ duyệt**. |
| `GET /api/v1/funds/{id}/requests` | Lịch sử `V1FundRequestDto {id,fundId,requestedByUserId,recipientUserId,kind,amountVnd,purpose,state,reviewedByUserId?,reviewedAt?,executionAvailability}`. |
| `POST /api/v1/funds/requests/{id}/report` | `{"reason":"Nội dung báo cáo"}`; báo chi Fixed đã Completed, mở case hỗ trợ/tranh chấp, không tự hoàn tác ledger. |

**Các cờ khác nhau:** `canSpend` chỉ là quyền boolean do chủ cấp; `effectiveCanSpend` là điều kiện tổng hợp; `canManageSpendingPermission` không thay quyền của chủ nhóm trong API. Không có công thức hạn mức chi tiền cá nhân dựa trên đóng góp. `spendingState:"OPEN"` và số dư thực trừ `lockedExcessVnd`/hold legacy mới xác định có thể chi.

#### 8. Quỹ cố định V2 — Đóng kỳ và đề xuất kỳ tiếp theo (API CŨ; nghiệp vụ mới tại fixed.md)

| API | Body, ý nghĩa |
|---|---|
| `POST /api/v1/funds/{id}/activity/close` | **Chủ quỹ**, `{"securityPin":"<PIN>"}` → `204`, đóng chu kỳ hiện tại trước khi tạo đề xuất mới. |
| `POST /api/v1/funds/{id}/activity-proposals` | **Chủ quỹ**, `{"targetVnd":900000,"idempotencyKey":"<unique-key>","securityPin":"<PIN>"}` → `V1ActivityProposalDto`; mục tiêu **chu kỳ mới**, không phải khoản chi phát sinh. |
| `GET /api/v1/funds/activity-proposals/{id}` | `{id,fundId,targetVnd,state,electorateSize,yes,no,openedAt,deadline,ownVote?}`. |
| `POST /api/v1/funds/activity-proposals/{id}/vote` | Cử tri cố định, `{"support":true}` hoặc `{"support":false}`. Một phiếu không đổi sau khi bỏ. |
| `POST /api/v1/funds/activity-proposals/{id}/settle` | **Chủ quỹ**, body **chỉ** `{"securityPin":"<PIN>"}`, xử lý đề xuất đã `ApprovedAwaitingSettlement`. Không có `actualAmount`, `status` hay `note`. |

**CHỈ ĐÚNG VỚI BACKEND CŨ, KHÔNG CÒN LÀ QUY TẮC SẢN PHẨM:** `Voting` 24 giờ hoạt động; `Yes * 2 > electorateSize` mới được `ApprovedAwaitingSettlement`. **Nghiệp vụ mới tại fixed.md không cần đa số**; Member chọn tiếp tục hoặc nhận hoàn/rời; bấm Không hoàn ngay và im lặng 24h tự hoàn, snapshot quyền lợi bất biến. Chuyển kỳ/chốt nghĩa vụ mới chỉ được thực hiện sau khi xác định danh sách tiếp tục.

**Ghi chú lịch sử backend cũ (B1):** Luồng đa số có thể kẹt settlement/ownership nếu Owner bỏ Không. Đặc tả Fixed mới **loại bỏ điều kiện đa số và Owner bỏ phiếu**; không sao chép workaround B1 vào nghiệp vụ mới. Chưa xác nhận code đã được thay thế.

#### 9. Quỹ cố định V2 — Hoàn tiền, quản trị, tình huống biên

| API | Body và mục đích |
|---|---|
| `GET /api/v1/funds/{id}/refund-preview?memberUserId=...` | `V1RefundPreviewDto {fundId,userId,currentFundBalanceVnd,eligibleRefundAmountVnd,caseType,status}`; số dự kiến tại thời điểm đọc. |
| `POST /api/v1/funds/{id}/leave` | `{"securityPin":"<PIN>","idempotencyKey":"<unique-key>","destinationWalletId":null}`; chủ quỹ phải chuyển quyền trước khi rời nhóm nhiều thành viên. |
| `POST /api/v1/funds/{id}/members/{userId}/kick` | Chủ quỹ, `{"securityPin":"<PIN>","reason":"Lý do","idempotencyKey":"<unique-key>","destinationWalletId":null}`. |
| `POST /api/v1/funds/{id}/dissolve` | Chủ quỹ, `{"securityPin":"<PIN>","idempotencyKey":"<unique-key>"}`; giải tán chỉ khi ledger sau hoàn tiền về **0 VND**. |
| `GET /api/v1/funds/{id}/activities` | Lịch sử hoạt động theo `page,pageSize`, `V1ActivityDto` có `event,actorUserId?,subjectUserId?,detail?,createdAt,referenceId?`. |
| `GET /api/v1/funds/{id}/owner-response` | `V1OwnerResponseDto {fundId,requiredSince?,deadline?,overdue,respondedAt?,caseId?}`. |
| `POST /api/v1/funds/{id}/owner-response/escalate` | Thành viên góp tiền được mở case hỗ trợ khi chủ quỹ không phản hồi sau **7 ngày hoạt động** kể từ lúc đóng kỳ; không tự sửa ledger hay đổi chủ. |

**Hoàn tiền theo nhánh đã triển khai:** `CaseA_Unspent` nếu chưa phát sinh chi (đóng góp chưa hoàn), `CaseB_FixedSpentClearShare` khi rời/loại sau chi, `CaseB_DissolveFixed` khi giải tán sau chi. Khi đã chi, giữ lại `protectedExcess=max(0,max(0,paid-required)-refundsBefore)` cho mỗi người, rồi phân bổ phần số dư còn lại theo trọng số `min(paid,required)` và đồng lẻ theo ghế. Cần xét quyền lợi theo **membership episode**; không dùng công thức Free Fund. Các mã như `zero_entitlement_dissolution_blocked` ngăn giải tán nếu số dư còn nhưng không có người có quyền nhận.

**Chưa nghiệm thu theo đặc tả mới:** API cũ chưa đáp ứng đầy đủ chia hết nghĩa vụ, Auto-Kick, thay ghế 72h, hòa giải/bầu Owner 12h+12h, chuyển kỳ không đa số, hoàn ngay khi chọn Không, giới hạn ba kỳ, cam kết tin tưởng và quyền đọc lịch sử. `Pending24h`/`Suspended` còn trong đường xử lý request legacy, không phải luồng chi Fixed mới.

> **Quan trọng cho frontend V1:** Các route `/api/groups` ở mục 1–5a là **legacy**, không phải V1 Fixed/Free. Mục 6–9 đã cập nhật theo DTO Fixed V2 nhưng **không chứng nhận B1/C1–C3 đã PASS**. Free Fund dùng bản đồ API ở mục 10; không áp dụng cơ chế token của lời mời legacy.


#### 10. Free Fund V1 — API hiện hữu, API cần bổ sung và hợp đồng UI mới (CHƯA TRIỂN KHAI)

> **Nguồn sự thật nghiệp vụ:** phần **1.3** và **1.3.1** của chính file này. Bảng dưới tách rõ API đã có theo code ngày 31/10/2026 với **nghiệp vụ mới phải bổ sung**, không tuyên bố đã chạy.

| Nghiệp vụ | API hiện có trên main | Yêu cầu mới / ghi chú |
|---|---|---|
| Tạo, xem quỹ/Member | `POST /api/v1/funds`; `GET /api/v1/funds`; `GET /api/v1/funds/{id}`; `GET /api/v1/funds/{id}/members` | Body tạo Free: `{"name":"Quỹ sinh hoạt","kind":"Free","targetVnd":null,"securityPin":"<PIN>"}`. Owner/Member phân biệt theo quan hệ trong quỹ. |
| Mời, chấp nhận/từ chối | `POST /api/v1/funds/{id}/invitations`; `GET /api/v1/funds/invitations`; `POST /api/v1/funds/invitations/{id}/accept` / `decline` | Owner hoặc Member mời bạn bè; người được mời tự chấp nhận; không yêu cầu Owner xét lại lời mời Free. |
| Góp tiền, lịch sử góp | `POST /api/v1/funds/{id}/contributions`; `GET /api/v1/funds/{id}/contributions` | `walletId, amountVnd, idempotencyKey, securityPin`; ghi vào **vốn lượt tham gia hiện hành** ở thời điểm góp. |
| Tạo Expense/Withdrawal | `POST /api/v1/funds/{id}/requests` | Body có `kind`, `amountVnd`, `recipientUserId`, `purpose`, `idempotencyKey`, `securityPin`. Mỗi Member chỉ 1 request hiệu lực/mỗi kind; nút thứ hai cùng loại khóa và server trả xung đột. |
| Danh sách/lịch sử request | `GET /api/v1/funds/{id}/requests` | Hiện API chung chưa phân luồng Member/Owner; **cần bổ sung bộ lọc trạng thái/chủ thể và DTO** (lý do, thời điểm tạo/duyệt/chi, người duyệt). |
| Owner duyệt/từ chối | `POST /api/v1/funds/requests/{id}/review` | Với Expense: duyệt **không ghi payout**, tạo quyền `ApprovedUnused` trong 7 ngày. Với Withdrawal: chỉ chuyển vào ví Member nếu không có quyền chi chưa dùng ở quỹ và vốn Member còn đủ. Backend hiện tại **chưa đúng** quy tắc Expense mới. |
| Hủy request/quyền | `POST /api/v1/funds/requests/{id}/cancel` | Cần hỗ trợ hủy `ApprovedUnused`, hết hạn 7 ngày, tự hủy do số dư thay đổi và hủy khi rời/kick/giải tán; không hủy giao dịch đã chuyển qua provider mà chưa xác minh. |
| Thanh toán bên ngoài bằng quyền chi | **Chưa có API provider thanh toán hóa đơn thực tế** | Hạng mục mở rộng: xác thực quyền chi, đúng số tiền, một lần, kết quả provider và đối soát; **không tạo giao dịch thành công giả** và không bắt buộc demo thanh toán thật. SePay giữ nguyên. |
| Vốn / hoàn tiền | `GET /api/v1/funds/{id}/refund-preview`; `POST /api/v1/funds/{id}/leave`; `POST /api/v1/funds/{id}/members/{userId}/kick`; `POST /api/v1/funds/{id}/dissolve` | Phải thay bộ tính Free hiện hành bằng vốn theo thời điểm và lượt thành viên; giữ đúng tất cả ví dụ ở 1.3.1; hủy request/quyền hợp lệ trước refund. |
| Lịch sử tài chính | `GET /api/v1/funds/{id}/contributions`; `GET /api/v1/funds/{id}/activities`; `GET /api/v1/funds/{id}/requests` | Hiện có nguồn dữ liệu lịch sử; cần hiển thị riêng yêu cầu đang chờ và giao dịch đã chi/rút thực sự. Thành viên xem tài chính chung, lịch sử request cá nhân. |
| Quản trị/bầu cử/chuyển chủ | Một số route `/elections`, `/ownership-offers` và `/governance` vẫn tồn tại trong code dùng chung | **Free Fund không được sử dụng các hành động bầu cử hoặc chuyển Owner**; chỉ Fixed được sử dụng theo chính sách Fixed. Không dùng mốc vắng mặt 15 ngày cho Free. |

**Ví dụ UI:** B có 320.000đ vốn; đang chờ `Withdrawal` 300.000đ và `Expense` 100.000đ đã được duyệt nhưng chưa thanh toán. Hai nút tạo yêu cầu tương ứng đều bị khóa; Owner **không thể duyệt Withdrawal của bất cứ Member nào**. Khi Expense hoàn tất làm vốn B còn 280.000đ, yêu cầu rút 300.000đ tự hủy và gửi thông báo. Nếu quyền Expense hủy mà không chi, B vẫn có 320.000đ và Owner có thể duyệt rút.

**Không dùng cho Free:** `ownership-offers`, `elections`, `Pending24h`, `Suspended` và mô tả chuyển tiền Expense về ví ngay sau khi Owner duyệt. Những mục này còn được đề cập ở tài liệu/code cũ, không phải yêu cầu nghiệp vụ Free đã khóa.

**Tiêu chí triển khai/kiểm thử tương lai:** số dư quỹ = tổng vốn người tham gia; request không trùng; tổng quyền chi không vượt quỹ; chi/hoàn/rút không double-post; tự hết hạn quyền 7 ngày; bảo vệ thanh toán đang xử lý; refund-preview khớp thực nhận; Free không đổi hành vi Fixed, SePay và `/api/groups`. Chưa có kết quả kiểm thử cho đặc tả mới.

### 3.7. Kế hoạch tài chính: Ngân sách, mục tiêu, giao dịch định kỳ, quy tắc

**Nguồn:** `PlanningControllers.cs`, `PlanningContracts.cs`. Tất cả nghiệp vụ gắn với user trong access token, không gửi `userId` giả định.

**Ngân sách (Budgets) — chi theo danh mục / tháng:**

| API | Contract/ý nghĩa |
|---|---|
| `GET /api/budgets?year=2026&month=10` | `BudgetDto[]` gồm `categoryId,categoryName,year,month,limitAmount,spentAmount,remainingAmount,percentage,alertLevel,alertsEnabled,rollover`. |
| `GET /api/budgets/{id}` | Ngân sách cụ thể của user. |
| `POST /api/budgets` | `{categoryId,year,month,limitAmount,alertsEnabled:true,rollover:false,securityPin?}`. |
| `PUT /api/budgets` | **API tương thích legacy**: tạo nếu chưa có ngân sách cho danh mục/tháng, ngược lại cập nhật. Frontend mới nên dùng POST/PUT ID rõ nghĩa. |
| `PUT /api/budgets/{id}` | `{limitAmount,alertsEnabled:true,rollover:false,securityPin?}`; không chuyển ngân sách sang danh mục/tháng khác bằng update. |
| `DELETE /api/budgets/{id}` | Body `{securityPin}`; xóa cấu hình, không xóa chi tiêu đã phát sinh. |

**Mục tiêu tiết kiệm (Goals):**

| API | Contract/ý nghĩa |
|---|---|
| `GET /api/goals`, `GET /api/goals/{id}` | Danh sách/chi tiết `GoalDto` có `name,targetAmount,currentAllocatedAmount,targetDate?,status,percentage,categoryId?,categoryName?,iconName?,colorHex?,movements`. |
| `POST /api/goals` | `{name,targetAmount,targetDate?,categoryId?,iconName?,colorHex?,initialAmount:0,walletId?,idempotencyKey?,securityPin?}`. Nếu đặt `initialAmount` khác 0, phải cung cấp ví và thông tin xác thực/khử trùng lặp hợp lệ. |
| `PUT /api/goals/{id}` | `{name,targetAmount,targetDate?,categoryId?,iconName?,colorHex?,securityPin?}`; chỉ sửa cấu hình, không trực tiếp sửa tiền. |
| `DELETE /api/goals/{id}` | Body `{securityPin}`; không cho xóa nếu còn tiền hoặc lịch sử tài chính không đáp ứng điều kiện handler. |
| `POST /api/goals/{id}/contributions` | `{walletId,amount,idempotencyKey,securityPin?}` chuyển từ ví vào mục tiêu qua ledger. |
| `POST /api/goals/{id}/withdrawals` | **Cùng `GoalMovementRequest`**: `{walletId,amount,idempotencyKey,securityPin?}`, ví nhận phải hợp lệ; **không có trường `destinationWalletId`** trong DTO này. |

**Giao dịch định kỳ và Subscription:** Không được tự động hạch toán mọi giao dịch dự kiến thành số dư đã chi. Backend có định nghĩa, occurrence, lệnh ghi nhận, dừng và tiếp tục.

| API | Contract/ý nghĩa |
|---|---|
| `GET /api/recurring-transactions`, `GET /api/recurring-transactions/{id}` | `RecurringDto`: ví, danh mục, `kind,amount,title,description,frequency,nextRunAt,status,isSubscription,lastRunAt?,anchorDay,anchorMonth,occurrences?`. |
| `POST /api/recurring-transactions` | `{walletId,categoryId,kind,amount,description,frequency,nextRunAt,isSubscription:false,title?}`. |
| `PUT /api/recurring-transactions/{id}` | Cùng nhóm trường `UpdateRecurringRequest`, chỉ đổi cấu hình các kỳ tương lai. |
| `DELETE /api/recurring-transactions/{id}` | Hủy định nghĩa lịch định kỳ; **không xóa giao dịch đã ghi nhận**. |
| `POST /api/recurring-transactions/{id}/pause` và `/resume` | Tạm dừng/khôi phục lịch. |
| `POST /api/recurring-transactions/{id}/occurrences/{occurrenceId}/record` | `{securityPin,evidenceIds:[],idempotencyKey}` → `TransactionDto`. Chỉ khi lệnh ghi nhận hợp lệ, ledger mới thay đổi. |

**Quy tắc phân loại giao dịch (Transaction Rules):** `GET /api/transaction-rules`, `GET /api/transaction-rules/{id}`, `POST /api/transaction-rules`, `PUT /api/transaction-rules/{id}`, `DELETE /api/transaction-rules/{id}`. Tạo `{descriptionPattern,targetCategoryId,priority:0}`; sửa thêm `isActive:true`; `TransactionRuleDto` gồm `id,descriptionPattern,targetCategoryId,targetCategoryName,priority,isActive`. Không coi quy tắc là giao dịch có hạch toán.

### 3.8. Khiếu nại, tranh chấp, bằng chứng và trò chuyện với Admin

**Nguồn:** `AdminController.cs` (`CasesController`), `AdminContracts.cs`. User chỉ xem/sửa **hồ sơ thuộc chính mình**; Admin xử lý trên web. Support và Dispute là hai nhóm riêng, không gộp `status` làm chứng từ hoàn tiền.

| API | Contract chính xác |
|---|---|
| `POST /api/cases/support` | `{category:"transaction",priority:"Normal",linkedTransactionId?,subject:"Yêu cầu hỗ trợ",description:"Nội dung"}` → `CaseDto`. `category` phải thuộc danh sách backend cho phép (ví dụ `system_error,account,banking_sepay,transaction_edit,other,transaction,feature_request,general,complaint,complaint_transaction,complaint_banking,complaint_fund,complaint_account,v1funddispute`). |
| `GET /api/cases/support` / `GET /api/cases/support/{id}` | Danh sách `SupportCaseDetailDto[]`, chi tiết gồm `ticketNumber,subject,description,category,priority,status,linkedTransactionId?,assignedAdminId?,correctionGranted,history[]`. |
| `POST /api/cases/support/{id}/messages` | `{message,attachments?}`; đính kèm JSON nếu backend chấp nhận DTO `CaseAttachmentUpload`. |
| `POST /api/cases/support/{id}/messages/multipart` | Upload tin nhắn + đính kèm qua form-data; kiểm tra kích thước/loại file theo handler. |
| `POST /api/cases/disputes` | `{linkedTransactionId,reason,description?}`; chỉ gắn giao dịch thuộc user, tạo `CaseDto`. |
| `GET /api/cases/disputes` / `GET /api/cases/disputes/{id}` | Danh sách và chi tiết `DisputeDetailDto`: `linkedTransactionId,reference,title,amount,counterparty?,reason,description,status,resolution?,history`. |
| `POST /api/cases/disputes/{id}/messages` và `.../messages/multipart` | User phản hồi/đính kèm minh chứng trong tranh chấp của mình. |
| `GET /api/cases/attachments/{id}` | Tải đính kèm sau kiểm tra quyền. |

**Trạng thái/luồng:** Người dùng tạo hồ sơ → backend ghi lịch sử và xếp thông báo/email → Admin xem/trao đổi/đổi trạng thái → user xem kết quả. **Không khẳng định hỗ trợ `Resolved` tự động hoàn tiền**; tài chính được sửa qua nghiệp vụ ledger/correction tương ứng. `transaction_edit` chỉ áp dụng cho giao dịch thủ công đủ điều kiện để xin quyền correction.

### 3.9. AI Assistant: Gemini, hội thoại, file và xác nhận hành động

**Nguồn:** `AiController`, `AiContracts.cs`. AI là trợ lý phân tích tài chính theo dữ liệu mà User được phép xem, không phải role quản trị. Provider có thể trả lời trực tiếp hoặc fallback; không hardcode `"Gemini_Live"` trong UI và không tự giả lập `latencyMs`.

| API | Contract chính xác |
|---|---|
| `GET /api/ai/conversations?search=...` | Danh sách hội thoại của user. |
| `GET /api/ai/conversations/{id}` | Chi tiết có `messages[]`. |
| `POST /api/ai/conversations` | `{title?,contextType:"General",contextGroupId?}` → `AiConversationDto`. |
| `PUT /api/ai/conversations/{id}` | `{title?,isPinned?,contextType?,contextGroupId?}` → `AiConversationDto`. |
| `DELETE /api/ai/conversations/{id}` | Xóa hội thoại thuộc chủ sở hữu. |
| `POST /api/ai/chat` | `{conversationId?,message,mode:"FinancialQuery",groupId?,attachmentIds?}` → `AiChatResponse {conversationId,message,provider,isFallback,proposedAction?}`. |
| `POST /api/ai/attachments` | `multipart/form-data` key `file` (theo controller, tối đa request 10 MB cộng overhead) → `AiAttachmentDto`. |
| `GET /api/ai/attachments/{id}` | Tải attachment thuộc user. |
| `GET /api/ai/actions/{id}` | Xem `AiActionDto` với `status,expiresAt,executedAt?,failureCode?`. |
| `POST /api/ai/actions/{id}/execute` | `{securityPin?,evidenceIds?,explicitConfirmation:true}`. Nếu hành động ghi tiền, kiểm tra PIN/chứng từ theo chính sách; phải xác nhận tường minh, không chạy từ câu chat đơn thuần. |
| `POST /api/ai/actions/{id}/cancel` | Hủy hành động đề xuất còn hiệu lực. |

**Phân quyền và giao diện:** Hiển thị context nhóm chỉ khi user có quyền; đề xuất thao tác phải cho xem nội dung/ảnh hưởng trước khi bấm đồng ý. `isFallback` báo AI provider có sử dụng fallback; backend chặn nội dung/đầu vào không hợp lệ theo bộ lọc an toàn.

### 3.10. Thông báo: Inbox, tìm kiếm, xóa, preference và Push

**Nguồn:** `NotificationsController`, `NotificationContracts.cs`, `MobilePushController`.

| API | Contract và UI |
|---|---|
| `GET /api/notifications` | Danh sách `NotificationDto[]` thuộc User, gồm `id,type,title,message,dataJson,isRead,createdAt,readAt?`. |
| `GET /api/notifications/search?unreadOnly=&type=&search=&page=1&pageSize=20` | `NotificationPageDto {items,page,pageSize,total}`, **không phải** `PagedResult` của endpoint danh sách thường. |
| `POST /api/notifications/{id}/read` | Đánh dấu đã đọc. |
| `POST /api/notifications/read-all` | Đánh dấu tất cả đã đọc. |
| `DELETE /api/notifications/{id}` | Xóa thông báo theo ID và quyền sở hữu. |
| `DELETE /api/notifications` | Xóa toàn bộ thông báo của chính User; trả số lượng đã xóa. |
| `GET /api/notifications/preferences` | Danh sách `NotificationPreferenceDto` cấu hình theo tài khoản/ví. |
| `PUT /api/notifications/preferences` | `{accountKey,accountName,walletId?,notifyBalanceChanges,notifyBudgetAlerts,notifyLargeTransactions,largeTransactionThreshold,notifyMinBalanceAlert,minBalanceThreshold}` → `NotificationPreferenceDto`. |
| `PUT /api/mobile/push/installations/{installationId}` | Đăng ký cài đặt thiết bị/push token với backend. |
| `DELETE /api/mobile/push/installations/{installationId}` | Gỡ cài đặt/đăng ký nhận push của thiết bị. |

Thông báo sự kiện quỹ và kết quả chi/rút phải dựa trên dữ liệu backend, phân biệt thông báo đã ghi vào inbox với việc đã gửi push thật lên thiết bị. Thông báo hủy request khi đổi chủ Free Fund là **yêu cầu đã chốt nhưng chưa triển khai**.

---

### 3.10.1. Bảo trì và thông báo khẩn cấp dành cho User

| API | Quyền và dữ liệu |
|---|---|
| `GET /api/system/maintenance` | `AllowAnonymous`, trả `MaintenanceStateDto` cho màn hình trạng thái bảo trì. |
| `GET /api/system/announcements/active` | `AllowAnonymous`, trả `EmergencyAnnouncementDto?` (có thể null). |
| `GET /api/v1/system/maintenance` | `AllowAnonymous`, trả `MaintenancePlanDto[]`, trạng thái bảo trì V1. |

Các route tạo/sửa/hủy bảo trì thuộc Admin, **không cấp quyền thao tác cho User**.

### 3.11. Bảng đối chiếu backend ↔ User.md, phạm vi và các trường hợp cần kiểm thử

| Phân hệ | Những mục đã được kiểm và đồng bộ trong tài liệu này | Giới hạn xác nhận |
|---|---|---|
| Auth, PIN, Passkey, Recovery | Đúng tên body, response, routes, xác thực hai bước, session | Không chứng nhận end-to-end passkey trên thiết bị khi chưa chạy |
| Profile, Friends, Push | Preferences là `UserProfile`, danh sách API kết bạn, ảnh đại diện multipart, thiết bị push | Push provider/thiết bị thật phải test riêng |
| Wallet, Transaction, Category | 3 ví, `availableBalance`, manual/multi-wallet, correction/reversal, category archive, evidence | Cần test dữ liệu ledger thực khi tích hợp UI |
| Transfers | Phân biệt `p2p`/`internal`/`bank-direct` và DTO | Không xem bank-direct là tiền đã tới chỉ vì tạo lệnh |
| Bank + SePay | Liên kết/sync, sao kê, Test Mode top-up/withdraw/cancel/history, trạng thái đối soát | Khả năng payout ngân hàng thật tùy provider |
| Planning | Budget CRUD, Goal CRUD + money movement, recurring + occurrence, transaction rule CRUD | Không tự ghi sổ recurring khi mới tới hạn |
| Support/Dispute | Hai loại case, tin nhắn, attachments, correction quyền | Resolved không đồng nghĩa hoàn tiền |
| AI | Hội thoại, search, pin, upload, proposed actions & execution | Phụ thuộc provider và chính sách an toàn |
| Notifications | List/search/read/delete/clear/preferences/push | Không suy diễn push delivered |
| Free Fund | **Đặc tả mới 01/11/2026 tại 1.3, 1.3.1 và 3.6 mục 10**; bao gồm ví dụ số tiền, quyền chi 7 ngày, rời/kick/dissolve, lịch sử và chống spam | **CHƯA TRIỂN KHAI/CHƯA KIỂM THỬ**; không áp dụng bầu cử/chuyển chủ/hold 24h cho Free |
| Fixed Fund V2 | **Nghiệp vụ mới đã chốt 01/11/2026: [`fixed.md`](fixed.md)**; bảng API cũ ở 3.6 chỉ phản ánh code trước thay đổi | **CHƯA TRIỂN KHAI/CHƯA KIỂM THỬ theo logic mới**: thời hạn góp, Auto-Kick, ghế cố định, chuyển kỳ không đa số, bầu Owner, refund và privacy |

**Quy định cho frontend và Word:** Áp dụng những contract đã đối chiếu trong 1.3, 3.1–3.10.1; riêng B1/B2/B3 và C1–C6 **không được mô tả là backend đã sửa/PASS**. Đọc DTO/controller nếu cần thêm trường chuyên sâu. Không gọi `User.md` “100% đã kiểm thử”: tài liệu này đối chiếu tĩnh mã nguồn, không phải kết quả chạy E2E hoặc xác nhận provider.

## 4. RANH GIỚI TRÁCH NHIỆM FRONTEND / MOBILE APP (FRONTEND-ONLY)

1. **Giao diện & Trực quan hóa (UI/UX):**
   - Triển khai 3 chế độ Theme: Sáng (`light`), Tối (`dark`), Theo hệ thống (`system`).
   - Quản lý bộ chuỗi đa ngôn ngữ (Localization): Tiếng Việt (`vi-VN`) và Tiếng Anh (`en-US`).
   - Xử lý hiệu ứng chuyển động (animations), toast thông báo, skeleton loading khi chờ dữ liệu.
2. **Sinh trắc học Native (Biometrics Hardware):**
   - Tương tác với cảm biến phần cứng qua API Native của OS (Face ID / Touch ID qua `LocalAuthentication` trên iOS; `BiometricPrompt` trên Android).
   - **DTO preferences backend không có `enableBiometric`.** Mở khóa sinh trắc học cục bộ là việc của thiết bị/client; xác thực Passkey/WebAuthn dùng `/api/auth/passkeys/*` và khi thích hợp `/api/transaction-authorizations/*`, không tự thêm trường vào preferences.
3. **Lưu trữ an toàn trên thiết bị (Client Secure Storage):**
   - Lưu trữ `accessToken` trong RAM hoặc Secure Storage.
   - Quản lý `refreshToken` an toàn trong Keychain / Keystore; tự động gọi `POST /api/auth/refresh` ngầm trước khi token hết hạn.
   - Xóa toàn bộ token và điều hướng về màn hình Đăng nhập khi nhận mã lỗi `401 Unauthorized`.
4. **Idempotency & Mất mạng (Offline Resilience):**
   - Sinh `idempotencyKey` trong **request body** theo DTO từng nghiệp vụ tài chính, lưu và dùng lại cho cùng intent nếu retry. Chỉ gửi thêm `X-Idempotency-Key` nếu endpoint có hỗ trợ; header không thay body bắt buộc.
   - Hiển thị thông báo mất kết nối nếu thiết bị offline, không gọi API lặp lại khi chưa có mạng.
