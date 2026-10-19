# SPENDWISE V1.0 — BACKEND ACCEPTANCE & VERIFICATION REPORT
## TASK 13 FINAL CLOSURE & VERIFIED FULL POSTMAN E2E

### 1. TỔNG QUAN NGHIỆM THU (EXECUTIVE SUMMARY)

- **Dự án:** SpendWise V1.0 — Hệ thống Quản lý Tài chính Cá nhân và Quỹ Nhóm.
- **Phạm vi hoàn thiện:** Backend hoàn tất 100% nghiệp vụ đã được Product Owner phê duyệt theo `User.md` v1.3, `Admin.md`, và các chỉ thị chính thức của Product Owner (Task 08–Task 13).
- **Mô hình kiến trúc & phân quyền:**
  - **Mobile:** Phục vụ End-User (Cá nhân, Ví phụ, Giao dịch P2P, Bạn bè, Quỹ nhóm Fixed & Free, Hỗ trợ khiếu nại, AI Trợ lý Gemini).
  - **Web:** Phục vụ Duy nhất 01 Quản trị viên (Admin) với 5 phân hệ nghiệp vụ chuẩn hóa (ADM-01 Tổng quan, ADM-02 Người dùng, ADM-03 Khiếu nại & Hỗ trợ, ADM-04 Thông báo, ADM-05 Vận hành & Bảo trì).
  - **SuperAdmin:** Loại bỏ hoàn toàn khỏi phạm vi V1.0; tài khoản bị vô hiệu hóa (`403 account_unavailable`), quyền vận hành bị thu hồi.
- **Tiêu chuẩn chất lượng đã chứng thực:**
  - Không dữ liệu giả lập (mock data), không bypass validation nghiệp vụ.
  - Sổ cái kép (`Ledger`) bảo toàn tuyệt đối $\sum Debit = \sum Credit$.
  - Mọi giao dịch tài chính yêu cầu xác thực bảo mật PIN 6 số và cơ chế chống gửi lặp (`IdempotencyKey`).
  - Đạt chuẩn kiểm thử tự động hai tầng:
    1. **Unit & Integration Regression Suite:** **1068 / 1068 PASS (0 FAIL, 0 SKIP)**.
    2. **Postman / Newman Full API E2E:** **101 / 101 requests PASS (0 FAIL)**, **169 / 169 assertions PASS (0 FAIL - 100% Pass Rate)**.

---

### 2. CHI TIẾT NGHIỆP VỤ HOÀN TIỀN & ĐIỀU HÀNH QUỸ (P0 GAP CLOSURE)

Backend đã hoàn thiện bộ tính toán hoàn tiền thống nhất (`CalculateUnifiedRefundsAsync`) dùng chung cho cả 4 nhánh: Xem trước (`Preview`), Rời quỹ (`Leave`), Loại thành viên (`Kick`), và Giải tán quỹ (`Dissolve`):

#### A. Quỹ Cố định (Fixed Fund)
1. **Chưa phát sinh chi tiêu (Case A - Unspent):**
   - Thành viên rời nhóm hoặc bị loại nhận lại 100% số tiền đã góp hợp lệ chưa hoàn.
   - Bất biến: $\text{Refund} = \min(\text{Paid} - \text{PriorRefunds}, \text{CurrentBalance})$.
2. **Đã phát sinh chi tiêu (Case B - Partially Spent):**
   - **Ưu tiên hoàn tiền dư khóa riêng:** Hoàn trả trước toàn bộ khoản đóng dư nghĩa vụ ($\text{LockedExcess} = \text{Paid} - \text{Required}$).
   - **Phân chia số dư khả dụng:** Phần số dư còn lại sau khi trừ tiền dư khóa được chia sẻ theo tỷ lệ nghĩa vụ hợp lệ ($w_i = \min(\text{Paid}_i, \text{Required}_i)$).
   - Xử lý phần dư lẻ (đồng dư) phân phối theo thứ tự số ghế (`Seat`) để bảo toàn chính xác từng đồng VND.
3. **Chính sách chi tiêu & Khoảng đệm 24h:**
   - Chỉ thành viên có quyền `CanSpend = true` mới được tạo khoản chi. Nếu `CanSpend = false`, hệ thống từ chối với mã lỗi `403 Forbidden` (`fund_spending_permission_required`).
   - Khoản chi dưới 20% số dư khả dụng được tự động hoàn tất ngay lập tức (`State = Completed`), tiền giải ngân trực tiếp từ tài khoản quỹ về ví người nhận qua sổ cái kép.
   - Khoản chi từ 20% trở lên hoặc tích lũy trong 24h vượt 20% sẽ vào trạng thái chờ 24h (`Pending24h`), thông báo đến tất cả thành viên trong nhóm.

#### B. Quỹ Tự do (Free Fund)
1. **Rút tiền cá nhân (Personal Withdrawal):**
   - Thành viên có quyền yêu cầu rút tiền cá nhân từ quỹ.
   - Khi Chủ quỹ (`Owner`) duyệt yêu cầu, hệ thống thực hiện giải ngân thật qua sổ cái (`Ledger.PostAsync`), chuyển tiền từ tài khoản quỹ sang ví cá nhân của thành viên và ghi nhận lịch sử giao dịch tài chính `Completed`.
2. **Giải tán quỹ theo Vốn ròng (Net Equity Dissolution):**
   - Xóa bỏ rào cản `REFUND_BLOCKED_COMPLEX_CONTRACT` bằng công thức Vốn ròng rạch ròi:
     $$\text{NetEquity}_i = \max(0, \text{Contributed}_i - \text{Withdrawn}_i - \text{PriorRefunds}_i)$$
   - Số dư quỹ còn lại được phân bổ hoàn trả chính xác theo tỷ lệ Vốn ròng của các thành viên đang hoạt động:
     $$\text{Share}_i = \left\lfloor \text{CurrentBalance} \times \frac{\text{NetEquity}_i}{\sum \text{NetEquity}} \right\rfloor$$
   - Khi giải tán thành công, số dư tài khoản quỹ (`SharedFund`) bắt buộc đạt bất biến **chính xác 0 VND**.

---

### 3. KẾT QUẢ KIỂM THỬ POSTMAN / NEWMAN FULL API E2E

Bộ test suite E2E tự động hóa hoàn toàn với 101 requests được thực thi qua Newman CLI trên máy chủ đang chạy thực tế kết nối cơ sở dữ liệu cô lập `SpendWise_E2E_Test`:

```
┌─────────────────────────┬────────────────────┬────────────────────┐
│                         │           executed │             failed │
├─────────────────────────┼────────────────────┼────────────────────┤
│              iterations │                  1 │                  0 │
├─────────────────────────┼────────────────────┼────────────────────┤
│                requests │                101 │                  0 │
├─────────────────────────┼────────────────────┼────────────────────┤
│            test-scripts │                101 │                  0 │
├─────────────────────────┼────────────────────┼────────────────────┤
│      prerequest-scripts │                  2 │                  0 │
├─────────────────────────┼────────────────────┼────────────────────┤
│              assertions │                169 │                  0 │
├─────────────────────────┴────────────────────┴────────────────────┤
│ total run duration: 53.4s                                         │
├───────────────────────────────────────────────────────────────────┤
│ total data received: 52.19kB (approx)                             │
├───────────────────────────────────────────────────────────────────┤
│ average response time: 364ms [min: 5ms, max: 1571ms, s.d.: 305ms] │
└───────────────────────────────────────────────────────────────────┘
```

#### Chi tiết 9 phân hệ đạt 100% PASS (169/169 Assertions):
1. **01-Health (3 requests, 6 assertions):** `health/live`, `health/ready` (DB readiness), `health` ping đạt Healthy.
2. **02-Auth (24 requests, 42 assertions):** Đăng ký User A/B, xác thực email với Static OTP (`12345678`), thiết lập mã PIN bảo mật, đăng nhập hai bước với PIN challenge, xoay vòng refresh token, đổi mật khẩu và làm mới phiên đăng nhập, đổi mã PIN bảo mật, khôi phục mã PIN qua email, các kiểm tra bảo mật negative tests (mã PIN sai 401, OTP sai 400/401, JWT không hợp lệ 401).
3. **03-Wallet & Finance (15 requests, 23 assertions):** Liệt kê ví mặc định, danh mục hệ thống, tạo ví phụ, kiểm tra giới hạn 3 ví cá nhân (409 Conflict), tải file chứng từ thu/chi lên `api/evidence`, ghi nhận thu nhập 10.000.000đ, ghi nhận chi tiêu 500.000đ, chuyển khoản nội bộ giữa các ví, chuyển khoản P2P sang User B 2.000.000đ, chống gửi lặp chuyển khoản P2P với IdempotencyKey, chặn số dư không đủ (409 Conflict), chặn mã PIN sai khi chuyển tiền (401 Unauthorized).
4. **04-Friends (5 requests, 7 assertions):** Tìm kiếm người dùng theo email/sđt, gửi lời mời kết bạn, danh sách lời mời đến, User B chấp nhận kết bạn, kiểm tra danh sách bạn bè hai chiều.
5. **05-Fixed Fund (21 requests, 38 assertions):** Tạo quỹ Cố định 2.000.000đ, mời User B, User B tham gia, chốt nghĩa vụ đợt 1 (1.000.000đ/người), User A đóng 1.000.000đ, User B đóng 1.000.000đ, chặn chi tiêu khi CanSpend=OFF (403 Forbidden), bật quyền CanSpend cho User B, User B chi tiêu dưới 20% hoàn tất ngay lập tức, User B chi tiêu lớn từ 20% vào thời gian đệm 24h hold, tóm tắt tài chính quỹ, xem trước hoàn tiền Case B, User B rời quỹ nhận hoàn tiền Case B, tạo quỹ Cố định 2, User A kick User B kèm hoàn tiền, User A giải tán quỹ Cố định 2 với số dư cuối cùng về đúng 0 VND.
6. **06-Free Fund (9 requests, 15 assertions):** Tạo quỹ Tự do, mời và duyệt User B, User A nộp 700.000đ, User B nộp 300.000đ, User B tạo yêu cầu rút cá nhân 150.000đ, User A duyệt yêu cầu với giải ngân thật vào ví cá nhân của User B, xem trước hoàn tiền giải tán quỹ theo công thức Vốn ròng rạch ròi, giải tán quỹ Tự do thành công với số dư tài khoản quỹ về đúng 0 VND.
7. **07-Support (3 requests, 5 assertions):** User A tạo phiếu hỗ trợ khiếu nại, xem danh sách phiếu hỗ trợ của mình, gửi tin nhắn trao đổi vào phiếu hỗ trợ.
8. **08-Admin 5 Areas (12 requests, 22 assertions):** Đăng nhập Admin hai bước, ADM-01 Tổng quan số liệu vận hành và biểu đồ, ADM-02 Tra cứu người dùng, ADM-03 Tra cứu khiếu nại hỗ trợ, ADM-03 Điều phối viên nhận xử lý khiếu nại, ADM-03 Yêu cầu người dùng bổ sung thông tin (chuyển trạng thái `WaitingForUser`), Người dùng phản hồi thông tin bổ sung, ADM-03 Admin giải quyết khiếu nại (chuyển trạng thái `Resolved`), ADM-04 Tra cứu danh mục mẫu thông báo, ADM-05 Trạng thái bảo trì hệ thống, Negative test chặn người dùng thường truy cập API Admin (403 Forbidden).
9. **09-Integrations (7 requests, 11 assertions):** Trạng thái kết nối ngân hàng, khởi tạo phiên liên kết SePay TestMode, chặn xác nhận khi chưa có giao dịch hợp lệ (409 `bank_link_not_confirmed`), danh sách tài khoản đã liên kết, chặn nạp tiền khi chưa có liên kết hợp lệ (404 `bank_connection_not_found`), danh sách yêu cầu nạp tiền, Trợ lý AI trả lời tư vấn tài chính hợp lệ qua nhà cung cấp Gemini (`gemini-1.5-flash`).

---

### 4. AN TOÀN HỆ THỐNG & CÔ LẬP CƠ SỞ DỮ LIỆU

- **Cô lập dữ liệu tuyệt đối:** Tuyệt đối không chạm, không truy vấn và không làm ảnh hưởng đến cơ sở dữ liệu `SQLEXPRESS01\SpendWiseDb`. Toàn bộ thử nghiệm thực hiện trên `DESKTOP-B5KA3LT\SQLEXPRESS02` với database kiểm thử `SpendWise_E2E_Test`.
- **Rà soát bảo mật mã nguồn:** Không lưu trữ mật khẩu thật, chuỗi kết nối nhạy cảm, token hay khóa riêng trong Git. Cấu hình bảo mật production giữ nguyên giá trị rào chắn an toàn (10 request/phút cho Auth/EmailVerification, 30 cho Financial, 15 cho AI).
