# SPENDWISE — ĐẶC TẢ NGHIỆP VỤ QUỸ CỐ ĐỊNH (FIXED FUND V2)

> Phiên bản tài liệu: 01/11/2026 — BẢN LOGIC ĐÃ THỐNG NHẤT.
> Mục đích: mô tả quy tắc, ví dụ và tiêu chí nghiệm thu để trao đổi với giảng viên và triển khai sản phẩm.
> Ranh giới bằng chứng: đây là **đặc tả nghiệp vụ mục tiêu**, KHÔNG phải xác nhận tất cả chức năng đã được lập trình hoặc kiểm thử thành công. API/backend hiện hữu được mô tả riêng trong User.md.

## 1. Mục tiêu và khác biệt với Free Fund

Fixed Fund là quỹ chung theo **nghĩa vụ đóng góp được chốt trước**, có hạn góp cá nhân, quyền chi do Owner kiểm soát và quy trình hoàn tiền/giải tán nghiêm ngặt. Không giống Free Fund, thành viên không được tùy ý rút phần vốn hoặc rời quỹ trong kỳ hoạt động bình thường chỉ vì muốn rời. Cơ chế cam kết tin tưởng và kênh khiếu nại Admin bảo vệ quyền lợi khi có tranh chấp; **cam kết không miễn trách nhiệm của Owner và không thay thế kiểm soát tài chính**.

- Vai trò: **Owner** (chủ nhóm), **Member** (thành viên), **Admin** (xử lý khiếu nại theo thẩm quyền).
- Mỗi Fixed Fund tối đa **3 kỳ hoạt động**, tính cả kỳ tạo nhóm ban đầu; tối đa 30 thành viên theo giới hạn quỹ Fixed hiện hành.
- Mỗi kỳ có tên, mục tiêu đóng góp VND, thời lượng góp cá nhân, danh sách ghế, nghĩa vụ, sổ góp/chi và lịch sử riêng.
- Số dư, quyền lợi từng người và giao dịch do sổ cái (ledger) xác định, không dựa vào con số UI.

## 2. Cam kết tin tưởng trước khi tham gia

Khi người dùng chuẩn bị chấp nhận lời mời/tham gia Fixed Fund, hiển thị hộp xác nhận:

> **Bạn có tin tưởng nhóm này không?**
>
> Đây là quỹ chung có nghĩa vụ đóng góp và thời hạn đã thống nhất. Owner quản lý thời gian hoạt động và quyền chi trong giới hạn nghiệp vụ. Trong kỳ đang hoạt động, bạn không được rời nhóm hoặc yêu cầu hoàn vốn tùy ý. Mọi khoản góp, chi và hoàn được ghi nhận để đối soát. Nếu xảy ra tranh chấp hoặc nghi ngờ chi tiêu sai, bạn có thể gửi khiếu nại lên Admin kèm bằng chứng để được xem xét, can thiệp theo thẩm quyền.
>
> **[Không tin tưởng] [Tin tưởng, tiếp tục]**

- Không tin tưởng: không tham gia, không phát sinh nghĩa vụ.
- Đồng ý: ghi lại phiên bản cam kết, người xác nhận và thời điểm; việc chấp nhận không trao cho Owner quyền lấy tiền ngoài quy tắc.
- Khi mở kỳ mới, thành viên phải được thấy **mục tiêu, thời lượng góp và nghĩa vụ dự kiến/cuối cùng** trước khi xác nhận nhận nghĩa vụ của kỳ đó. Nếu cấu hình làm thay đổi nghĩa vụ đã hiển thị, không được âm thầm áp mức mới: phải thông báo và lấy lại sự đồng ý.
- Kênh khiếu nại/hỗ trợ SpendWise đã có là nơi yêu cầu Admin kiểm tra; một khiếu nại không đồng nghĩa khoản tiền đã chi được tự động đảo ngược hoặc chắc chắn thu hồi.

## 3. Tạo nhóm và chốt phân bổ (kỳ 1)

1. A tạo nhóm, đặt tên, **mục tiêu góp chung** (ví dụ 9.000.000đ), chọn **thời lượng góp cá nhân** (ví dụ 24 giờ hoặc 3 ngày). Sau tạo, hiển thị **0đ/9.000.000đ**.
2. A mời B, C. Lúc này **chưa tính giờ góp**, vì Owner chưa xác nhận danh sách và phân bổ.
3. Có **ít nhất 2 người tính cả Owner** mới được chốt phân bổ. Trước khi chốt, hệ thống xem trước số tiền của từng người.
4. **Mục tiêu bắt buộc chia hết chính xác cho số ghế đã chốt, đến từng VND**. Ví dụ 9.000.000đ/3 = 3.000.000đ/người: hợp lệ. 10.000.000đ/3: từ chối chốt, yêu cầu điều chỉnh mục tiêu hoặc danh sách.
5. Owner bấm **Xác nhận phân bổ**: tạo snapshot bất biến cho kỳ (target, ghế, số tiền/ghế, thời lượng và thời điểm hạn của từng người); gửi thông báo và bắt đầu đếm ngược cho các thành viên hiện có, kể cả Owner.
6. **Sau chốt, không được sửa target, thời lượng góp và nghĩa vụ của các ghế** trong kỳ. Trước thời hạn, không được loại người chỉ vì họ chưa góp đủ. Member không được tự Out trong kỳ góp/chi bình thường; Owner không được tự ý giải tán khi kỳ chưa thuộc đường kết thúc hợp lệ.

Lời mời: Owner được mời trực tiếp người đã kết bạn; Member có thể đề xuất mời, **Owner phải duyệt** trước khi người nhận chấp nhận. Mọi lời mời có xác nhận của người được mời; người từ chối không bị phát sinh nghĩa vụ.

## 4. Góp tiền, Auto-Kick và thay ghế trong 72 giờ

- Thành viên góp từng lần hoặc một lần, tối đa **phần nghĩa vụ chưa hoàn thành**; khoản chuyển phải được xác nhận hợp lệ trước hạn theo thời gian server. Chưa đủ toàn bộ mục tiêu quỹ thì **không được chi**.
- Hết hạn cá nhân mà **Member** góp thiếu hoặc không góp: hệ thống **tự loại (Auto-Kick)**, hoàn đúng phần vốn/quyền lợi hợp lệ của lượt tham gia đó, giải phóng ghế nhưng **giữ nguyên nghĩa vụ tiền của ghế**. Người đã góp đủ không phải gánh thêm.
- **Owner** góp thiếu khi hết hạn: không Auto-Kick Owner; kỳ góp **thất bại**, hệ thống đối soát, hoàn tiền hợp lệ cho mọi người và giải tán nhóm, không yêu cầu Owner đăng nhập.
- Từ **Auto-Kick đầu tiên** khiến kỳ thiếu ghế, mở **một cửa sổ tìm người thay duy nhất 72 giờ**. Các lần Auto-Kick sau không đặt lại mốc 72 giờ.
- Người được mời chỉ có thể **chấp nhận vào ghế trống trước khi hết 72 giờ**. Sau khi vào hợp lệ, họ có **đủ thời lượng góp cá nhân đã chốt cho kỳ**, kể cả vào sát cuối cửa sổ 72 giờ. Hết cửa sổ thì không nhận thêm người, nhưng chờ người đã vào hợp lệ hoàn tất hạn góp riêng.
- Người thay thế nhận **nghĩa vụ cố định của ghế trống** nhưng là **lượt thành viên (membership episode) mới**; không kế thừa giao dịch, khoản đã hoàn hoặc quyền lịch sử của người rời.
- Nếu hết quy trình thay người mà vẫn thiếu nghĩa vụ, kỳ góp thất bại: quyết toán và giải tán. Owner cũng được **kết thúc thất bại sớm khi còn ghế trống**, rồi hoàn tiền; không phải đợi hết 72 giờ.

**Ví dụ:** A/B/C mỗi người 3 triệu; A/B đã góp đủ, C mới góp 1 triệu. C hết hạn và bị Auto-Kick: hoàn C 1 triệu; quỹ còn 6 triệu, mục tiêu vẫn 9 triệu; D thay ghế C phải góp 3 triệu. Không có D hợp lệ thì hoàn A/B mỗi người 3 triệu và giải tán (giả sử quỹ chưa chi).

## 5. Chi tiêu khi đã đạt mục tiêu

- Khi toàn bộ nghĩa vụ hợp lệ hoàn thành và quỹ đạt đúng target, gửi thông báo cho Owner; **đạt target không tự đóng kỳ**.
- Owner bật/tắt **quyền chi (CanSpend)** cho từng Member. Member chỉ được chi khi đang có quyền, đã hoàn thành nghĩa vụ và kỳ đang mở chi; Owner có quyền chi nhưng vẫn phải đáp ứng điều kiện góp đầy đủ của cá nhân và nhóm.
- Người được cấp quyền được **chi chung trực tiếp**, không cần Owner duyệt lại từng lệnh. Mỗi khoản chi cần số tiền, **người nhận và mục đích chi hợp lệ**, ghi giao dịch và chứng từ khi có; không được biến tính năng chi chung thành **rút vốn cá nhân trá hình**. Không tự động hoàn tác một khoản chi đã hoàn tất chỉ vì sau đó có khiếu nại.
- Chỉ dùng **số dư có thể chi**, không được chi vào khoản tiền vượt nghĩa vụ được bảo vệ, số tiền đang bị giữ để đối soát hoặc khoản đã cam kết hoàn.
- Trong kỳ hoạt động bình thường, Member không được tùy ý Out để rút phần vốn; Owner không được Kick người có quyền lợi tiền để chiếm dụng vốn. Tranh chấp chuyển qua cơ chế khiếu nại/Admin có kiểm toán.
- Nếu Owner vẫn hoạt động nhưng kéo dài kỳ hoặc chi bất thường, Member được gửi khiếu nại; Admin xác minh và áp dụng biện pháp quản lý/đối soát hợp lệ. **Không coi hộp cam kết tin tưởng là biện pháp kỹ thuật ngăn chi sai**.

## 6. Owner đóng kỳ: giải tán hoặc chuyển sang kỳ tiếp theo

- Khi kết thúc sử dụng tiền chung, Owner **chủ động bấm Hoạt động hoàn thành**. Trước đóng kỳ, mọi giao dịch/giữ tiền đang xử lý phải có kết quả rõ ràng.
- Sau đóng kỳ, Owner được chọn **(A) giải tán** hoặc **(B) đề xuất kỳ kế tiếp**; không tạo kỳ thứ tư. Giải tán không cần cuộc bỏ phiếu, hoàn đúng entitlement và đưa số dư về 0đ trước khi đánh dấu hoàn tất.
- Khi đề xuất kỳ mới, **không bỏ phiếu đa số**: mỗi Member chọn **Có — tiếp tục**, hoặc **Không — rời và hoàn tiền ngay**. Owner tạo đề xuất và mặc định muốn tiếp tục.
- Mỗi Member có **24 giờ** để phản hồi; **im lặng = không tiếp tục**, hết hạn được hoàn và rời. Mọi người phải được thông báo rõ thông số kỳ mới và quyền lợi của mình.
- **Chụp cố định quyền lợi từng người trước khi bắt đầu cho phản hồi**; khi B nhận tiền trước, không được chia lại phần của A/C. Giao dịch hoàn thành công mới đánh dấu rời hoàn tất; lỗi chuyển tiền phải hiển thị chờ xử lý, không báo hoàn giả.
- Owner có thể **hủy đề xuất khi giai đoạn phản hồi còn mở**, xóa hiệu lực các phản hồi chưa quyết toán; **tiền đã hoàn thành công không được đảo ngược**. Muốn đề xuất lại thì dùng cấu hình và lượt tham gia hiện tại, không phục hồi người đã rời.
- Kỳ mới phải có **ít nhất hai người**. Nếu chỉ còn Owner, cho cửa sổ tìm thêm Member **72 giờ**; không đủ người thì hoàn quyền lợi Owner và giải tán. Kỳ chưa chốt phân bổ không được mở chi.
- Sau khi tập người tiếp tục và người mới đã xác nhận, Owner đặt lại **tên kỳ/tên nhóm, target, thời lượng góp** rồi chốt phân bổ mới. Mục tiêu phải chia hết theo số ghế cuối cùng, thời hạn mới khóa riêng cho kỳ mới.
- Số dư carry-over của người tiếp tục được ghi vào quyền lợi kỳ mới; người mới bắt đầu với 0đ, không bị tính lại chi phí kỳ cũ. Không tạo giao dịch góp tiền giả cho carry-over.
- **Kỳ 3 là kỳ cuối**; khi kết thúc chỉ còn quyết toán và giải tán.

## 7. Nguyên tắc hoàn tiền và số tiền lẻ

- **Nghĩa vụ góp bằng nhau không có nghĩa tiền hoàn luôn bằng nhau.** Sau khi chi chung hoặc carry-over khác nhau, quyền lợi thực tế của từng người có thể khác.
- Trước khi chi: hoàn đúng phần góp/carry-over chưa chi và chưa hoàn, không trả lần hai.
- Sau khi chi: bảo vệ khoản người đó có **vượt nghĩa vụ hiện hành**; phần có thể phân chia còn lại dựa vào mức vốn hợp lệ tham gia chi chung, theo snapshot quyền lợi tài chính.
- Làm tròn toàn bộ ở **đơn vị 1 VND**; phần lẻ được phân bổ theo thứ tự ghế ổn định, không làm mất hoặc tạo thêm tiền.
- Ví dụ: ba người mỗi người góp 300đ, chi 1đ, quỹ còn 899đ. Hoàn lần lượt 300đ, 300đ, 299đ theo ghế xác định; tổng hoàn đúng 899đ.
- Ví dụ carry-over: A có 3 triệu, B có 1 triệu; mục tiêu mới 4 triệu, hai người nghĩa vụ 2 triệu/người. A có 1 triệu **tiền vượt được bảo vệ**; B phải góp thêm 1 triệu. Khi B góp xong, quỹ 5 triệu nhưng chỉ **4 triệu** được phép chi. Nếu chi 2 triệu, quỹ 3 triệu; quyết toán A 2 triệu, B 1 triệu.
- Các bất biến bắt buộc: **không số dư âm; tổng tiền thuộc các quyền lợi chưa trả khớp số dư quỹ; một intent chỉ ghi ledger một lần; một quyền lợi không được hoàn hai lần; giải tán hoàn tất có số dư 0đ**.

## 8. Bầu Owner khi chủ nhóm vắng mặt

- Sau **15 ngày vắng hoạt động** với bằng chứng hợp lệ, Member được mở bầu Owner mới. Owner cũ không tham gia bỏ phiếu và không được tự hủy cuộc bầu hợp lệ.
- **Đợt 1 (12 giờ):** các Member chọn người họ tin tưởng (không chọn Owner đang vắng); có thể bỏ phiếu trống; không trả lời cũng tính phiếu trống. Người có số phiếu hợp lệ cao nhất duy nhất được đề nghị tiếp quản.
- **Đợt 2 (12 giờ):** người thắng xác nhận nhận quyền bằng **PIN**. Nếu đồng ý đúng hạn, chuyển Owner, Owner cũ xuống Member, giữ nguyên quyền lợi tài chính của Owner cũ. Nếu từ chối hoặc hết hạn, **bầu thất bại**, không ép nhận quyền.
- **Hòa phiếu cao nhất:** không tự chọn một người thắng; bầu thất bại. **Tất cả phiếu trống:** không có Owner mới; Owner cũ chỉ được giữ vai trò danh nghĩa trong giai đoạn kết thúc, không tiếp tục sử dụng quỹ vô hạn.
- Khi bầu thất bại, các Member có **24 giờ tự Out và nhận hoàn**; hết hạn hệ thống **bắt buộc quyết toán/hoàn phần còn lại và giải tán**. Nếu chỉ còn Owner cũ, hoàn nốt quyền lợi vào ví Owner rồi giải tán.
- Nếu Owner cũ quay lại trong khi bầu hợp lệ đã mở, người đó chỉ **được xem tiến trình**, không được bỏ phiếu, chi, Kick, giải tán hoặc thay đổi nhóm để can thiệp cuộc bầu.
- Khi kết quả bầu thành công, Member xem thông báo người giữ Owner mới. Người thắng không tự được xem tài liệu riêng hoặc lịch sử chi/góp của các kỳ trước ngoài quyền truy cập hợp lệ vốn có.

## 9. Quyền truy cập lịch sử và khiếu nại

- Kỳ cũ bất biến về góp, chi, hoàn và kết quả; không xóa bản ghi ledger khi chuyển kỳ hoặc giải tán.
- Người mới vào kỳ 2/3 **không xem lịch sử nội bộ kỳ trước**. Người rời vẫn được tra cứu **giao dịch, khoản hoàn và chứng từ của chính họ**, không được xem hoạt động mới sau khi rời.
- Owner mới quản lý kỳ hiện hành và xem quyền lợi carry-over cần thiết, **không tự có quyền xem mọi dữ liệu riêng của Owner cũ**.
- Nhóm đã giải tán biến mất khỏi danh sách nhóm đang hoạt động của User, nhưng lưu lịch sử trên hệ thống cho kiểm toán, quản trị và khiếu nại theo phân quyền.
- Admin chỉ can thiệp thông qua hồ sơ, bằng chứng, phân quyền, nhật ký kiểm toán và nghiệp vụ tài chính hợp lệ; xử lý khiếu nại **không bảo đảm tự động thu hồi tiền đã chuyển ra ngoài**.

## 10. Ma trận tình huống nghiệm thu tối thiểu

| Mã | Tình huống | Kết quả bắt buộc |
|---|---|---|
| T01 | Owner tạo quỹ 9 triệu và chưa chốt người | 0/9 triệu, không chạy đồng hồ góp |
| T02 | Một Owner muốn chốt phân bổ | Từ chối vì chưa đủ 2 người |
| T03 | Target 10 triệu / 3 người | Từ chối chốt, không phân bổ lệch 1đ |
| T04 | Target 9 triệu / 3 người | Mỗi ghế đúng 3 triệu, khóa cấu hình |
| T05 | Member đang còn hạn nhưng chưa góp đủ | Không Kick vì lý do góp thiếu |
| T06 | C góp 1/3 triệu, hết hạn | Hoàn C 1 triệu, ghế trống vẫn thiếu 3 triệu |
| T07 | D thay ghế sát hạn 72 giờ | D vẫn được đầy đủ hạn góp cá nhân |
| T08 | Không tìm được người thay | Kỳ thất bại, hoàn đúng tiền, giải tán |
| T09 | Owner góp thiếu đúng hạn | Tự kết thúc thất bại, hoàn các thành viên và giải tán |
| T10 | Quỹ mới 8/9 triệu | Không mở chi |
| T11 | Quỹ đủ 9 triệu và B chưa có CanSpend | B bị từ chối chi |
| T12 | B được bật CanSpend, giao dịch chung hợp lệ | Chi trực tiếp, ghi người nhận/mục đích/ledger |
| T13 | Có 1 triệu vượt nghĩa vụ được bảo vệ | Không được dùng 1 triệu ấy để chi |
| T14 | Owner đóng kỳ rồi chọn giải tán | Hoàn theo entitlement, balance = 0 |
| T15 | A/B chọn tiếp tục, C bấm Không | C nhận quyền lợi ngay, không tính lại phần A/B |
| T16 | C không phản hồi kỳ mới trong 24 giờ | C được hoàn, không nhận nghĩa vụ mới |
| T17 | Owner hủy đề xuất sau khi C nhận hoàn | Không đảo khoản hoàn C, phiếu đề xuất cũ mất hiệu lực |
| T18 | Chỉ còn Owner muốn kỳ 2 | Có 72 giờ tìm thêm người; không đủ thì giải tán |
| T19 | A carry 3 triệu, B carry 1 triệu, target 4 triệu | A có 1 triệu bảo vệ, B góp thêm 1 triệu |
| T20 | Quỹ 900đ, chi 1đ | Hoàn tổng 899đ, không thiếu/thừa đồng |
| T21 | Sau 3 kỳ Owner muốn kỳ 4 | Từ chối |
| T22 | Owner vắng 15 ngày, đủ bằng chứng | Member mở bầu thay Owner |
| T23 | Người thắng không xác nhận PIN trong 12 giờ | Bầu thất bại; mở đường Out/hoàn |
| T24 | Bầu Owner hòa hoặc không ai được chọn | Không chuyển quyền; áp dụng kết thúc theo quy định |
| T25 | Bầu thất bại, đã qua 24 giờ | Hoàn đủ quyền lợi, giải tán |
| T26 | Owner cũ trở lại giữa bầu | Chỉ xem, không có thao tác ghi |
| T27 | D vào kỳ 2 | Không có quyền xem dữ liệu riêng kỳ 1 |
| T28 | Một người bấm hoàn tiền hai lần / worker retry | Chỉ một lần ghi tiền, không hoàn trùng |
| T29 | Chấp nhận lời mời/chuyển tiền sát thời hạn | Một kết quả duy nhất theo thứ tự server, không vừa Kick vừa ghi góp |
| T30 | Tranh chấp chi chung | Có thể gửi case Admin; không tự đảo giao dịch Completed |

## 11. Ranh giới triển khai

- Tài liệu này là **chính sách nghiệp vụ Fixed Fund V2 đã thống nhất** ngày 01/11/2026, phục vụ giảng viên, UI/UX, Backend và QA.
- Không được coi 30 ca trên là test đã PASS; phải có bài kiểm thử thực tế, chứng cứ ledger/SQL/API và kiểm thử đồng thời.
- Không áp dụng các quy tắc Fixed (nghĩa vụ, Auto-Kick, bầu Owner, tối đa 3 kỳ) cho Free Fund V1.
- Bảng API hiện hữu và những chỗ cần thay đổi để đạt đặc tả mới được phân biệt trong User.md; không được suy luận rằng API hiện tại đã hỗ trợ deadline hay Auto-Kick.
