# SPENDWISE — BÁO CÁO KIỂM TOÁN CHẤT LƯỢNG TEST POSTMAN E2E (TEST QUALITY & GAP AUDIT)
## ĐÁNH GIÁ CHUYÊN SÂU TÍNH NGHIÊM NGẶT & TOÀN VẸN CỦA BỘ TEST E2E

> **Mục tiêu:** Rà soát toàn diện chất lượng của bộ Postman Collection E2E (`SpendWise-User-API.postman_collection.json`), bóc tách tính nghiêm ngặt của các Assertions, phân loại chi tiết các loại PASS, phát hiện các điểm sai lệch contract và ghi nhận các bài học kỹ thuật cốt lõi sau khi hoàn tất khóa Backend User.  
> **Nguyên tắc:** Phản biện trung thực (Adversarial Quality Bar), căn cứ trực tiếp trên source code Backend, kết quả Newman Full Run (306 requests, 343 assertions PASS) và database cô lập SQL02 `SpendWise_E2E_G4`.

---

## 1. PHÂN BỐ VÀ BÓC TÁCH BẢN CHẤT CỦA 306 REQUESTS VÀ 343 ASSERTIONS

Toàn bộ **306 requests** và **343 assertions** trong bộ sưu tập chính thức đã được rà soát độc lập trên file kết quả gốc `reports/newman/backend-user-final-verified.json`, loại bỏ 100% các assertions lỏng lẻo (multi-status assertions) và phân loại minh bạch giữa số lượng Requests và số lượng Assertions:

> [!IMPORTANT]
> **ĐỐI CHIẾU CHUẨN XÁC REQUESTS VS ASSERTIONS:**
> - **Phân loại Assertions (343 Assertions):** 295 Happy-Path (86.01%) + 38 Expected Negative (11.08%) + 10 Controlled Fallback (2.91%) = **343 Assertions PASS**.
> - **Phân loại Requests (306 Requests):** 281 Thành công (226 200, 21 201, 3 202, 31 204) + 22 Lỗi kỳ vọng (5 401, 2 403, 7 404, 8 409) + **chính xác 3 requests HTTP 503** (fallback sandbox ngân hàng ngoài có kiểm soát). Hoàn toàn không nhầm lẫn 10 fallback assertions thành 10 HTTP 503 requests.

### Bảng đối chiếu Requests và Assertions

| Phân Loại Mục Tiêu Kiểm Thử | Số Lượng Requests | Tỷ Lệ Requests % | Số Lượng Assertions | Tỷ Lệ Assertions % | Bản Chất & Ý Nghĩa Kỹ Thuật |
| :--- | :---: | :---: | :---: | :---: | :--- |
| **Happy-Path (Success Flows)** | **281** | **91.8%** | **295** | **86.0%** | Các thao tác thực thi thành công nghiệp vụ, tạo mới tài nguyên, truy vấn dữ liệu hoặc cập nhật trạng thái hợp lệ (HTTP 200, 201, 204, 202). |
| **Expected-Error / Invariant Guards** | **22** | **7.2%** | **38** | **11.1%** | Các bài test cố tình gửi dữ liệu sai hoặc vượt quyền để kiểm tra cơ chế bảo vệ của Backend (Invariant guards): Đăng ký trùng email (409), Sai mật khẩu (401), Sai mã PIN (401), Vượt giới hạn 3 ví (409), User A truy cập ví/ngân sách/mục tiêu của User B (404), Thành viên tự cấp quyền chi tiêu Fixed Fund (403), Thành viên tự duyệt chi tiêu Free Fund (403). |
| **External Provider Fallback (Graceful Degradation)** | **3** | **1.0%** | **10** | **2.9%** | Kiểm tra cơ chế suy thoái an toàn khi thiếu kết nối trực tiếp nhà cung cấp ngoài: 3 requests HTTP 503 trên endpoint tạo phiên link ngân hàng sandbox ngoài (B5-01), với 10 assertions xác thực mã lỗi và cấu trúc payload an toàn. |
| **TỔNG CỘNG** | **306** | **100.0%** | **343** | **100.0%** | **Toàn bộ 306 requests và 343 assertions đạt chuẩn tuyệt đối, 0 failures, 0 assertions đa mã lỏng lẻo.** |

---

## 2. TỔNG HỢP CÁC LỖ HỔNG & VẤN ĐỀ NGHIỆP VỤ ĐÃ GIẢI QUYẾT TRIỆT ĐỂ

### 2.1. Phân lập dữ liệu kiểm thử bằng Dynamic Run ID
- **Vấn đề trước đây:** Sử dụng các email hoặc SpendId cố định giữa các lần chạy khiến dữ liệu bị trùng lặp, gây lỗi `409 Conflict` giả khi chạy lại test mà chưa xóa database.
- **Giải pháp:** Sử dụng biến môi trường động `runId` sinh theo timestamp (`{{$timestamp}}`) cho toàn bộ tài khoản thử nghiệm sinh động trong Folder 01 và các luồng liên tục A–F. Database TEST giữ nguyên dữ liệu sạch mà không bị xung đột khóa chính.

### 2.2. Vòng đời Token và Thu hồi Phiên (Session Revocation)
- **Vấn đề trước đây:** Sau khi người dùng gọi `POST /api/auth/logout` hoặc `DELETE /api/auth/sessions/{id}`, một số test script vẫn tái sử dụng access token cũ để gửi request tiếp theo.
- **Giải pháp:** Tách biệt rõ ràng token của các actor (User A, User B, Member). Kiểm tra tính vô hiệu lực của token sau khi revoke (`HTTP 401 unauthorized`) và yêu cầu đăng nhập lại (fresh login) để lấy cặp token mới cho các luồng kế tiếp.

### 2.3. Khóa chống trùng lặp (Idempotency Key) & Chống Double-Posting
- **Vấn đề trước đây:** Request gửi lặp với cùng một `IdempotencyKey` nhưng khác tham số số tiền có nguy cơ làm sai lệch số dư nếu handler chỉ kiểm tra key tồn tại mà không đối soát payload.
- **Giải pháp:** Áp dụng phương thức `TransferFeatureSupport.EnsureReplayMatches` và `ILedgerService`: khi phát hiện trùng khóa, hệ thống kiểm tra toàn bộ thuộc tính tài chính (số tiền, người nhận, tài khoản nguồn/đích). Nếu trùng khớp: trả lại kết quả cũ (idempotent replay, 0 VND ledger mutation). Nếu sai lệch: ném ngay lỗi `ConflictException` (HTTP 409).

### 2.4. Giới hạn 3 Ví cá nhân & Ràng buộc đóng ví (`WalletLimit: 3`)
- **Vấn đề trước đây:** Hệ thống cho phép người dùng mở vô số ví phụ, dẫn đến phân mảnh số dư và khó quản lý trên giao diện di động.
- **Giải pháp:** Áp dụng giới hạn cứng tối đa 3 ví cá nhân hoạt động cho mỗi người dùng (`Default`, `Savings`, `Emergency`). Kiểm tra nguyên tử khi tạo ví mới (`wallets.Count >= 3` $\rightarrow$ `409 wallet_limit_reached`). Chỉ cho phép đóng ví phụ khi số dư khả dụng bằng đúng 0 VND.

### 2.5. Chu trình Ngân sách sau Chi tiêu (Budget Lifecycle Flow F)
- **Vấn đề trước đây:** Khi người dùng điều chỉnh số tiền của một giao dịch chi tiêu cũ (qua `PATCH /api/transactions/{id}`), ngân sách tháng tương ứng không được tự động tính toán lại.
- **Giải pháp:** Hoàn thiện luồng liên tục Flow F: Tạo ngân sách 2.000.000đ $\rightarrow$ ghi nhận chi 500.000đ (25%) $\rightarrow$ điều chỉnh giao dịch lên 800.000đ $\rightarrow$ ngân sách tự động cập nhật đạt 40% (800.000đ đã chi, 1.200.000đ còn lại).

### 2.6. Chu trình Rút tiền Ví SePay Test Mode & Bút toán Tạm giữ (Reservation Hold State Machine)
- **Vấn đề trước đây:** Rút tiền chuyển thẳng sang `Completed` hoặc trừ tiền ví 2 lần khi xác nhận đối soát.
- **Giải pháp:** Triển khai quy trình 2 giai đoạn an toàn:
  1. *Khởi tạo & Tạm giữ:* Trừ số dư khả dụng của ví, chuyển tiền vào tài khoản tạm giữ `SYSTEM:ReservationHold:VND`, đặt trạng thái `Processing`.
  2. *Đối soát thành công:* Chuyển tiền từ `SYSTEM:ReservationHold:VND` sang tài khoản thanh toán ngoài `SYSTEM:ExternalSettlement:VND`, chuyển trạng thái `Completed`. Net biến động trên tài khoản tạm giữ bằng đúng 0đ.

---

## 3. BÀI HỌC KỸ THUẬT QUAN TRỌNG: KHÔI PHỤC QUYỀN SỞ HỮU NGÂN HÀNG (BANK OWNERSHIP LESSON)

> [!CAUTION]
> **BÀI HỌC SỐ 1 VỀ TÍNH TOÀN VẸN CỦA DỮ LIỆU & QUY TRÌNH KIỂM THỬ:**
> - **Sự cố:** Trong giai đoạn trước, để nhanh chóng kiểm thử tính năng Rút tiền SePay Test Mode cho User A, thao tác hoán đổi trực tiếp `UserId` giữa 2 bản ghi `BankConnections` (VPBank và TPBank) đã được thực hiện bằng lệnh SQL UPDATE thủ công.
> - **Hậu quả:** Thao tác SQL này làm sai lệch nguồn gốc liên kết thực tế được lưu trong `BankLinkSessions`, khiến tài khoản TPBank của User B bị gán cho User A mà không thông qua quy trình xác thực OAuth/Link token hợp lệ, tạo ra kết quả kiểm thử PASS giả tạo về quyền sở hữu.
> - **Biện pháp khắc phục triệt để:**
>   1. Product Owner đã chỉ đạo thực hiện transaction SQL khôi phục hoàn nguyên chính xác 100% hai bản ghi về đúng chủ sở hữu gốc: VPBank `****0001` (`8B5E9450...`) thuộc về User A; TPBank `****0002` (`E9E1753B...`) thuộc về User B.
>   2. Thực hiện toàn trình giao dịch Withdrawal DEMO mới bằng **chính User B và tài khoản TPBank TEST thực sự thuộc về User B**, rút 50.000đ, số dư TPBank tăng từ 150k lên 200k, ví User B giảm từ 4.875M xuống 4.825M, đối soát `Completed` hợp lệ.
>   3. Kiểm thử phân quyền chéo API: User A gọi withdrawal bằng TPBank của User B bị chặn ngay lập tức với mã `404 ConnectionNotFound`.
> - **Nguyên tắc cốt lõi:** **TUYỆT ĐỐI KHÔNG DÙNG SQL UPDATE/INSERT TRỰC TIẾP ĐỂ TẠO DỮ LIỆU KHỚP VỚI TEST**. Mọi quyền sở hữu tài nguyên bắt buộc phải được thiết lập và xác minh thông qua chính API nghiệp vụ và token xác thực hợp lệ của người dùng.

---

## 4. TỔNG KẾT CHẤT LƯỢNG TOÀN DIỆN

- **Số Assertions Đa mã (Multi-status) còn lại:** **0 / 343 (Đã xóa bỏ hoàn toàn)**.
- **Tỷ lệ Pass Full Newman Suite:** **343 / 343 Assertions (100.0% PASS)**.
- **Rò rỉ dữ liệu hoặc lộ bí mật:** **0 (Zero leaks)**.
- **Kết luận:** Bộ test Postman E2E SpendWise đạt cấp độ tin cậy tối đa, sẵn sàng làm baseline bảo vệ vững chắc cho Phase Backend Admin tiếp theo.
