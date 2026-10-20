# ANTIGRAVITY TASK G2 — SEPAY BANKING INTEGRATION & FINANCIAL SAFETY REPORT

## 1. Executive Summary & Status
- **Task Goal**: Evaluate SePay capabilities (API v2, Bank Hub, Payment Gateway), verify bank linking, wallet top-up, wallet withdrawal, and financial safety invariants (All-or-Nothing top-up, double-spend prevention, balance verification constraints).
- **Core Verification Status**:
  - `BANK LINKING`: **DOCUMENTED_ONLY / CREDENTIALS_REQUIRED** (Bank Hub requires `CompanyXid`, `ClientId`, `ClientSecret`; API contract and mock gateway verified).
  - `BALANCE VERIFICATION`: **FEATURE_NOT_ENTITLED / BALANCE_UNAVAILABLE** (Neither Bank Hub nor API v2 provides real-time account balances of end-user linked personal bank accounts. MoMo-like dimming requires `BALANCE_UNAVAILABLE` fallback when live balance is absent).
  - `BANK DEBIT / TOPUP`: **ALL_OR_NOTHING VERIFIED** (In-app unit and relational invariant tests pass 100%; top-ups execute atomically with exact-reference matching, zero partial credits, and single-flight lock).
  - `WALLET WITHDRAWAL`: **PAYOUT_PROVIDER_REQUIRED / DEFERRED** (SePay retail accounts do not support automated outbound payout API; withdrawal handled via internal ledger hold + manual admin review workflow).
  - `WEBHOOK / RECONCILIATION`: **SANDBOX_VERIFIED** (API v2 Test Mode transactions and accounts verified with live SePay sandbox token; IPN HMAC/API key authentication and deduplication verified).

---

## 2. SePay Capabilities Matrix

| Capability | API hỗ trợ | SpendWise Code | Sandbox Tested | Status |
|---|---|---|---|---|
| **Bank Linking** | SePay Bank Hub `/v1/link-token/create` | `SePayBankHubGateway.CreateLinkAsync` | Mock/Contract Tested | `DOCUMENTED_ONLY / CREDENTIALS_REQUIRED` |
| **Read Available Balance** | `GET /v2/bank-accounts` & `/v1/bank-account/{id}` | `SePayTestModeAccountReader` | Live Sandbox Tested | `FEATURE_NOT_ENTITLED / BALANCE_UNAVAILABLE` |
| **Authorized Bank Debit** | Chưa hỗ trợ debit qua Bank Hub | N/A | Không có API | `UNSUPPORTED` |
| **Payment Gateway Top-Up** | `https://pay-sandbox.sepay.vn/v1/checkout/init` | Tách biệt với Bank Hub | Documented | `PAYMENT_GATEWAY_TOPUP_SUPPORTED` |
| **Wallet Withdrawal/Payout** | Chưa hỗ trợ tự động bán lẻ | N/A (Internal Ledger Hold) | Không có API | `PAYOUT_PROVIDER_REQUIRED` |
| **Webhook IPN Ingestion** | SePay Webhook IPN (`transfer_type=in/out`) | `SePayBankTransactionIpnRequest`, `TestModeSePayFeature` | Live Sandbox Tested | `SANDBOX_VERIFIED` |
| **Reconciliation** | `GET /v2/transactions?q={ref}` | `SePayTestModeAccountReader.GetTransactionsAsync` | Live Sandbox Tested (200 OK) | `SANDBOX_VERIFIED` |

---

## 3. Product Owner Requirement: Trải nghiệm Nạp Ví Giống MoMo

### A. Phân tích Kỹ thuật về Đọc Số Dư Khả Dụng
1. **Thực tế Provider:**
   - Trường `accumulated` trong SePay API v2 và Webhook phản ánh **số dư tài khoản merchant/doanh nghiệp** tại SePay sau khi phát sinh giao dịch, **KHÔNG PHẢI** số dư tài khoản ngân hàng cá nhân của người dùng.
   - Bank Hub webhook định nghĩa trường `accumulated` là: `accumulated number: Balance after transaction (currently not supported, default = 0)`.
   - Các ngân hàng tại Việt Nam (NAPAS/Open Banking) chưa mở API truy vấn số dư khả dụng thời gian thực của tài khoản cá nhân cho các trung gian thanh toán bán lẻ không có giấy phép ví điện tử trực tiếp liên kết ngân hàng (Direct API).
2. **Quy tắc Xử lý Backend & Frontend:**
   - Khi provider không trả về số dư khả dụng đáng tin cậy của tài khoản nguồn cá nhân, trạng thái được trả về là `BALANCE_UNAVAILABLE`.
   - **Tuyệt đối không** lấy số dư merchant gán cho tài khoản cá nhân.
   - **Tuyệt đối không** mặc định số dư bằng 0 để làm mờ sai nguồn tiền của người dùng.
   - Trên giao diện Mobile/Frontend: Khi `balance` có giá trị xác thực, thực hiện so sánh `UsableBankBalance >= RequestedAmount + ApplicableSourceFees` để làm mờ/sáng ô ngân hàng. Khi trạng thái là `BALANCE_UNAVAILABLE`, hiển thị nhãn cảnh báo nguồn tiền chưa có số dư thời gian thực và thực hiện kiểm tra tại thời điểm thanh toán.

### B. Chính Sách Bắt Buộc: All-Or-Nothing Wallet Top-Up
- Không chấp nhận nạp tiền một phần (Partial Top-Up).
- Yêu cầu nạp 500.000 VND từ ngân hàng có 300.000 VND:
  - Nếu số dư được xác minh trước: Ô ngân hàng bị làm mờ, không cho chọn.
  - Khi thực hiện giao dịch: Nếu số tiền thực nhận không đủ 500.000 VND, giao dịch không được credit vào ví.
  - Không trừ 300.000 VND, không cộng 300.000 VND, không cộng 500.000 VND khi chưa đủ tiền.
  - Trường hợp chuyển khoản thủ công sai số tiền: Ghi nhận giao dịch thực tế vào `BankTransaction` để đối soát; không tự động đánh dấu hoàn tất và không tự ý chuyển tiền đi nơi khác.

---

## 4. Wallet Withdrawal: Rút Tiền Về Ngân Hàng
- **Khả năng SePay:** SePay hiện tại không cung cấp API phát lệnh chuyển tiền tự động ra ngoài (Payout API) cho tài khoản thông thường. Webhook "tiền ra" chỉ là thông báo biến động số dư khi tài khoản có giao dịch chuyển đi.
- **Giải pháp Kiến trúc V1.0:**
  - Lệnh rút tiền tạo một khoản giữ chỗ nội bộ (`Ledger hold` / `Pending`).
  - Số dư khả dụng của ví bị trừ ngay lập tức để tránh double-spending.
  - Yêu cầu được chuyển vào hàng đợi xử lý/đối soát của Admin.
  - Sau khi Admin thực hiện chuyển khoản và xác nhận, lệnh rút tiền mới chuyển sang `Completed`. Nếu thất bại, hold được hoàn trả (reversal) về ví.

---

## 5. Hướng Dẫn Kỹ Thuật Dành Cho Product Owner (Cấp Quyền & Đăng Ký SePay)

Nếu muốn kích hoạt kiểm thử Live/Sandbox đầy đủ cho các tính năng trên, Product Owner cần:
1. **SePay Bank Hub:**
   - Đăng ký sản phẩm **Bank Hub** tại portal SePay (`my.sepay.vn`).
   - Lấy bộ thông tin: `CompanyXid`, `ClientId`, `ClientSecret`, `LifecycleWebhookSecret`, `IpnApiKey`.
   - Cấu hình redirect URL (`CompletionRedirectUri`) về domain của SpendWise (`https://spendwise.local/bank-connections/callback`).
2. **Cổng Thanh Toán SePay (Payment Gateway):**
   - Đăng ký Cổng thanh toán Sandbox để nhận `Merchant ID` và `Secret Key`.
   - Endpoint checkout: `https://pay-sandbox.sepay.vn/v1/checkout/init`.
   - API tra cứu: `https://pgapi-sandbox.sepay.vn` (Basic Auth).
3. **Câu hỏi kỹ thuật cần gửi tới SePay Support:**
   - *Câu hỏi 1:* SePay Bank Hub có kế hoạch mở API truy vấn số dư khả dụng thời gian thực (`available_balance`) của tài khoản ngân hàng cá nhân sau khi User hoàn tất liên kết qua Hosted Link không?
   - *Câu hỏi 2:* Doanh nghiệp cần đáp ứng điều kiện gì để được mở API Payout/Chi tiền tự động từ tài khoản SePay về số tài khoản ngân hàng bất kỳ?

---

## 6. Kết Quả Kiểm Thử Nhắm Mục Tiêu (Targeted Tests)

| Bộ Kiểm Thử | Mục Đích | Số Test | Kết Quả |
|---|---|---|---|
| `SePayTestModeBankingTests` | All-or-nothing top-up, duplicate reference, single-flight lock, amount validation | 12 | **PASS (12/12)** |
| `SePayBankHubGatewayTests` | Basic auth, token cache, Hosted Link URL generation, transaction query | 10 | **PASS (10/10)** |
| `SePayTestModeAccountReaderTests` | Sandbox API v2 parsing, pagination, transaction list envelope | 27 | **PASS (27/27)** |
| `BankHubApplicationTests` | Bank linking lifecycle, reactivation, unlink, transaction paging | 35 | **PASS (35/35)** |
| **Tổng số Targeted Tests** | | **84** | **PASS (84/84)** |

- **Release Build**: 6 projects compiled cleanly: **0 errors, 0 warnings**.
- **An toàn Dữ liệu**: SQL01 (`DESKTOP-B5KA3LT\SQLEXPRESS01\SpendWiseDb`) **tuyệt đối không bị truy cập**.
- **Tiền thật**: Hoàn toàn không phát sinh giao dịch tiền thật.
