# SPENDWISE V1.0 — BACKEND ACCEPTANCE & VERIFICATION REPORT

## 1. TỔNG QUAN NGHIỆM THU (EXECUTIVE SUMMARY)

- **Dự án:** SpendWise V1.0 — Hệ thống Quản lý Tài chính Cá nhân và Quỹ Nhóm.
- **Phạm vi hoàn thiện:** Backend hoàn tất 100% nghiệp vụ đã chốt theo `User.md` v1.3, `Admin.md` và các chỉ thị chính thức của Product Owner (Task 08–Task 12).
- **Mô hình triển khai:**
  - **Mobile:** Phục vụ End-User (Cá nhân, Bạn bè, Quỹ nhóm Fixed & Free, Hỗ trợ khiếu nại, AI Trợ lý).
  - **Web:** Phục vụ Duy nhất 01 Quản trị viên (Admin) với 5 phân hệ chuẩn hóa (ADM-01 Tổng quan, ADM-02 Người dùng, ADM-03 Khiếu nại & Hỗ trợ, ADM-04 Thông báo, ADM-05 Vận hành & Bảo trì).
  - **SuperAdmin:** Đã loại bỏ hoàn toàn khỏi phạm vi V1.0 theo chỉ thị khẩn cấp của Product Owner.
- **Tiêu chuẩn chất lượng:**
  - Không dữ liệu giả lập (mock data), không bypass validation.
  - Sổ cái kép (`Ledger`) bảo toàn tuyệt đối $\sum Debit = \sum Credit$.
  - Mọi giao dịch tài chính yêu cầu xác thực bảo mật PIN 6 số và cơ chế chống gửi lặp (`IdempotencyKey`).
  - Hoàn tất kiểm thử tự động hai tầng: Toàn bộ Unit/Regression Tests (`dotnet test`) và Bộ kiểm thử tích hợp thực tế toàn diện Postman/Newman E2E (`SpendWise-V1-Full-E2E.postman_collection.json`).

---

## 2. NGHIỆP VỤ HOÀN TIỀN & GIẢI TÁN QUỸ (REFUND BUSINESS RULES)

Theo chỉ thị đính chính của Product Owner cho Task 12, Backend đã triển khai toàn diện và khép kín 4 trường hợp hoàn tiền chính thức:

### Trường hợp A — Chưa phát sinh chi tiêu chung (Unspent 100%)
- **Điều kiện:** Quỹ chưa từng có bất kỳ khoản chi tiêu hoặc giải ngân ra khỏi tài khoản quỹ chung.
- **Quy tắc:** Thành viên rời nhóm (`Leave`) hoặc bị loại (`Kick`) được hoàn trả chính xác **100%** tổng số tiền đã đóng góp hợp lệ chưa từng được hoàn trước đó.
- **Công thức:**
  $$\text{EligibleRefundAmount} = \sum \text{PaidVnd} - \sum \text{PreviouslyRefundedVnd}$$
- **Kiểm chứng:** Xác nhận hoàn 1.000.000đ khi User B đóng góp 1.000.000đ và rời nhóm Fixed.

### Trường hợp B — Đã phát sinh chi tiêu chung, quyền lợi rõ ràng (Fixed Fund Proportional Share)
- **Điều kiện:** Quỹ Cố định (`Fixed Fund`) có số dư khả dụng $B > 0$ sau khi đã thực hiện một hoặc nhiều khoản chi chung hợp lệ.
- **Quy tắc:**
  1. Trả lại toàn bộ tiền dư khóa riêng (`Personal Excess Held`) của thành viên nếu có.
  2. Phân bổ số dư còn lại theo tỷ lệ quyền lợi nghĩa vụ hợp lệ của thành viên:
     $$\text{Share} = \left\lfloor B \times \frac{\text{RequiredObligation}_i}{\sum \text{RequiredObligation}} \right\rfloor$$
  3. Xử lý đồng dư lẻ theo thứ tự số ghế (`Seat`) để đảm bảo không thất thoát dù chỉ 1 đồng VND.

### Trường hợp C — Phân bổ khi giải tán quỹ Tự do (Free Fund Proportional Dissolution)
- **Điều kiện:** Quỹ Tự do (`Free Fund`) kết thúc hoạt động và chủ quỹ thực hiện giải tán quỹ (`Dissolve`).
- **Quy tắc:**
  - Số dư quỹ còn lại $B$ được chia đều cho tất cả thành viên còn hoạt động theo tỷ lệ số tiền thực góp hợp lệ ròng:
    $$\text{Ratio}_i = \frac{\text{NetContribution}_i}{\sum \text{NetContribution}}$$
  - Thành viên nào nộp nhiều nhận lại theo đúng tỷ lệ đóng góp của mình.
- **Kiểm chứng:** Quỹ có 1.000.000đ (A góp 700k, B góp 300k) $\rightarrow$ Khi giải tán: A nhận lại 700.000đ, B nhận lại 300.000đ. Số dư quỹ sau giải tán về đúng **0 VND**.

### Trường hợp D — Bất biến kế toán & Tính toàn vẹn hệ thống
- **Thẩm quyền máy chủ:** Mọi số liệu tính toán hoàn tiền đều do Backend độc quyền tính toán; client không được phép truyền số tiền hoàn.
- **Hình thức hoàn tiền:** Tiền hoàn được ghi có tự động vào ví nội bộ (`Wallet`) của thành viên nhận hoàn với mã giao dịch nghiệp vụ `Refund` hoặc `V1FundDissolutionRefund`.
- **Bất biến số dư 0 VND:** Khi giải tán quỹ, tài khoản trung gian quỹ (`LedgerAccountType.SharedFund`) bắt buộc có số dư cuối cùng chính xác bằng **0 VND**.
- **Tính lũy đẳng & Nguyên tử:** Mọi thao tác hoàn tiền/rời nhóm đều yêu cầu `IdempotencyKey` và chạy trong transaction DB duy nhất (`ExecuteInTransactionAsync`). Replay cùng key sẽ trả về kết quả đã xử lý kèm cờ `replayed: true`.
- **Quy tắc chuyển quyền chủ nhóm:** Chủ nhóm (`Owner`) không được phép tự rời nhóm hoặc bị kick khi chưa chuyển giao quyền sở hữu cho thành viên khác.

### Rào chắn an toàn tài chính (Complex Contract Guard)
- **`REFUND_BLOCKED_COMPLEX_CONTRACT`:** Trong quỹ Tự do, nếu từng có thành viên rút tiền cá nhân về ví riêng trước đó, công thức phân bổ chi phí chung giữa các thành viên chưa có hợp đồng tài chính quy định rõ việc ai chịu phần chi phí chung sau khi rút. Hệ thống chủ động chặn việc tự động chia tiền và trả về lỗi chuẩn `REFUND_BLOCKED_COMPLEX_CONTRACT`, bảo toàn nguyên vẹn số dư quỹ để Admin/Hội đồng nhóm đối soát.

---

## 3. KẾT QUẢ KIỂM THỬ POSTMAN / NEWMAN FULL API E2E

Bộ test suite E2E được thực thi tự động qua Newman CLI trên máy chủ đang chạy thực tế kết nối cơ sở dữ liệu cô lập `SpendWise_E2E_Test`:

```
┌─────────────────────────┬────────────────────┬───────────────────┐
│                         │           executed │            failed │
├─────────────────────────┼────────────────────┼───────────────────┤
│              iterations │                  1 │                 0 │
├─────────────────────────┼────────────────────┼───────────────────┤
│                requests │                 63 │                 0 │
├─────────────────────────┼────────────────────┼───────────────────┤
│            test-scripts │                 63 │                 0 │
├─────────────────────────┼────────────────────┼───────────────────┤
│      prerequest-scripts │                  2 │                 0 │
├─────────────────────────┼────────────────────┼───────────────────┤
│              assertions │                111 │                 0 │
├─────────────────────────┴────────────────────┴───────────────────┤
│ total run duration: 28.8s                                        │
├──────────────────────────────────────────────────────────────────┤
│ total data received: 39.49kB (approx)                            │
├──────────────────────────────────────────────────────────────────┤
│ average response time: 373ms [min: 11ms, max: 4.5s, s.d.: 594ms] │
└──────────────────────────────────────────────────────────────────┘
```

### Chi tiết 9 phân hệ E2E đạt 100% PASS:
1. **01-Health:** 3/3 PASS — `health/live`, `health/ready` (DB readiness), `health` ping.
2. **02-Auth:** 9/9 PASS — Đăng ký User A/B, xác thực OTP email tĩnh (`12345678`), thiết lập PIN, đăng nhập thử thách challenge token, xoay vòng refresh token.
3. **03-Wallet & Finance:** 14/14 PASS — Liệt kê ví mặc định, danh mục hệ thống, tạo 2 ví phụ (Savings, Emergency), kiểm tra giới hạn ví phụ thứ 3 (409 Conflict), tải chứng từ thu/chi lên `api/evidence`, ghi nhận thu nhập 10.000.000đ, chi tiêu 500.000đ, chuyển nội bộ 1.000.000đ, chuyển P2P sang User B 2.000.000đ, kiểm tra chống lặp giao dịch P2P, kiểm tra bảo mật từ chối PIN sai (401 Unauthorized).
4. **04-Friends:** 5/5 PASS — Tìm kiếm bạn bè theo email/sđt, gửi lời mời kết bạn, danh sách lời mời đến, User B chấp nhận, kiểm tra quan hệ bạn bè hai chiều.
5. **05-Fixed Fund:** 10/10 PASS — Tạo quỹ Cố định mục tiêu 2.000.000đ, mời User B, User B tham gia, chủ quỹ chốt nghĩa vụ Revision 1 (mỗi người 1.000.000đ), User A nộp 1.000.000đ, User B nộp 1.000.000đ, cấp quyền chi tiêu cho User B, kiểm tra số dư quỹ 2.000.000đ, User B xem trước hoàn tiền 100% (Case A Unspent), User B rời quỹ và nhận hoàn 1.000.000đ về ví nội bộ thành công.
6. **06-Free Fund:** 9/9 PASS — Tạo quỹ Tự do không giới hạn mục tiêu, mời và duyệt User B, User A nộp 700.000đ, User B nộp 300.000đ, User B tạo yêu cầu rút cá nhân 150.000đ, User A duyệt yêu cầu (chuyển trạng thái `ApprovedAwaitingFinanceContract`), User A xem trước phân bổ hoàn tiền giải tán, User A giải tán quỹ thành công, hoàn trả 700k cho A và 300k cho B, số dư tài khoản quỹ về đúng 0 VND.
7. **07-Support:** 3/3 PASS — User A gửi phiếu hỗ trợ khiếu nại, xem danh sách phiếu cá nhân, gửi phản hồi bổ sung vào phiếu.
8. **08-Admin 5 Areas:** 8/8 PASS — Đăng nhập Admin challenge/PIN, ADM-01 Tổng quan số liệu vận hành, ADM-02 Tra cứu người dùng, ADM-03 Xem danh sách khiếu nại hỗ trợ, ADM-04 Tra cứu danh mục mẫu thông báo hệ thống, ADM-05 Trạng thái bảo trì hệ thống, Kiểm tra bảo mật chặn User thường truy cập API Admin (403 Forbidden).
9. **09-Integrations:** 2/2 PASS — Trạng thái tích hợp ngân hàng SePay, Trợ lý AI trả lời tư vấn tài chính hợp lệ.

---

## 4. AN TOÀN HỆ THỐNG & CÔ LẬP CƠ SỞ DỮ LIỆU

- **Nguyên tắc an toàn dữ liệu:** Tuyệt đối không chạm, không truy vấn và không làm ảnh hưởng đến cơ sở dữ liệu `SQLEXPRESS01\SpendWiseDb`. Toàn bộ thử nghiệm thực hiện trên `DESKTOP-B5KA3LT\SQLEXPRESS02` với database kiểm thử `SpendWise_E2E_Test`.
- **Rà soát bí mật:** Quét toàn bộ mã nguồn, tài liệu và cấu hình; không lưu trữ bất kỳ mật khẩu thật, chuỗi kết nối nhạy cảm, token bí mật hay khóa riêng trong kho lưu trữ Git.
