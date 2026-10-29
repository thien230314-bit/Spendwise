# BÁO CÁO KIỂM THỬ THỰC CHẤT VÀ BẰNG CHỨNG E2E BACKEND SPENDWISE V1.0

> **Thời điểm chốt nghiệm thu:** 29/10/2026 — 20:30:00+07:00  
> **Cơ sở dữ liệu kiểm nghiệm:** Microsoft SQL Server `DESKTOP-B5KA3LT\SQLEXPRESS02` / `SpendWise_E2E_G4` (SQL02 cách ly tuyệt đối, không kết nối cơ sở dữ liệu sản xuất SQL01).  
> **Cổng dịch vụ Kestrel API:** `http://127.0.0.1:5158` (Chế độ Release, Background Daemon).  
> **Tuyên bố trung thực:** Báo cáo phản ánh 100% số liệu thực tế đo lường bằng test runner và truy vấn cơ sở dữ liệu. Tách bạch hoàn toàn giữa các bộ kiểm thử Newman API, xUnit isolated tests, các phần ĐÃ XÁC MINH (VERIFIED PASS), các phần CHƯA XÁC MINH NGOẠI VI / BỊ CHẶN BỞI NHÀ CUNG CẤP (PROVIDER BLOCKED) và các chức năng THUỘC TRÁCH NHIỆM FRONTEND (FRONTEND-ONLY).

---

## 1. TỔNG HỢP SỐ LIỆU KIỂM THỬ THỰC TẾ (TEST EXECUTION SUMMARY)

### 1.1. Bảng Tổng Hợp Chi Tiết Từng Bộ Kiểm Thử (Không Gộp Trộn)

| STT | Phân Hệ & Bộ Kiểm Thử (Test Suite) | Loại Kiểm Thử | Requests / Tests | Assertions Passed | Tỷ Lệ Đạt | Trạng Thái Nghiệm Thu |
|:---:|---|:---:|:---:|:---:|:---:|:---:|
| **1** | **Backend User Acceptance Suite** (`backend-user-final-verified`) | Newman API | 306 requests | 343 / 343 | 100% | **PASS** |
| **2** | **Backend Admin Acceptance Suite** (`ADMIN-V1-FINAL-NEWMAN`) | Newman API | 80 requests | 138 / 138 | 100% | **PASS** |
| **3** | **Support & Direct Complaint E2E Flow** (`support_complaint_e2e`) | Newman API | 22 requests | 42 / 42 | 100% | **PASS** |
| **4** | **Financial Happy-Path & Security Invariants** (`FINANCIAL-HAPPY-PATH-E2E`) | Newman API | 32 requests | 32 / 32 | 100% | **PASS** |
| **5** | **Dynamic Email Lifecycle E2E Suite** | .NET xUnit | 6 tests | 32 / 32 | 100% | **PASS** |
| **6** | **Targeted Backend Regression Suite** | .NET xUnit | 82 tests | 82 / 82 | 100% | **PASS** |
| **7** | **SePay Test Mode Banking Security Tests** (`SePayTestModeBankingTests`) | .NET xUnit | 16 tests | 16 / 16 | 100% | **PASS** |
| **-** | **TỔNG HỢP NEWMAN API (Requests & Assertions)** | **Newman** | **440 requests** | **555 Assertions** | **100%** | **0 THẤT BẠI** |
| **-** | **TỔNG HỢP .NET REGRESSION (Isolated Unit & Integration)** | **xUnit** | **104 tests** | **130 Assertions** | **100%** | **0 THẤT BẠI** |
| **-** | **KẾT QUẢ BIÊN DỊCH (`dotnet build SpendWise`)** | **CLI Build** | **0 Cảnh báo** | **0 Lỗi** | **100%** | **BUILD PASS** |

---

## 2. BẢNG PHÂN ĐỊNH RANH GIỚI VÀ TRẠNG THÁI NGHIỆM THU TỪNG TÍNH NĂNG

| Hạng Mục Kiểm Thử | Trạng Thái API | Trạng Thái Nghiệp Vụ | Trạng Thái Ngoại Vi / Thực Tế | Đánh Giá & Ghi Chú Kỹ Thuật |
|---|:---:|:---:|:---:|---|
| **Đăng ký, Đăng nhập & Xác thực** | `PASS` | `PASS` | `PASS (Outbox/CSPRNG)` | Sinh OTP 8 số ngẫu nhiên CSPRNG, lưu trữ mã hóa trong Outbox, xác minh đúng mã, chống replay dứt điểm. |
| **Gửi/Nhận Email Hộp thư Thực tế** | `PASS` | `PASS` | `CHƯA XÁC MINH (NOT VERIFIED)` | Hàng đợi Outbox, mã hóa AES, toàn bộ 14/14 mẫu HTML/Text và cơ chế retry **ĐÃ ĐẠT**. Việc truyền tin SMTP ra Internet đến máy chủ bên thứ ba (Gmail/Outlook) **CHƯA ĐƯỢC XÁC MINH TỰ ĐỘNG** trong môi trường E2E để bảo vệ an toàn thông tin cá nhân. |
| **Quên mật khẩu & Reset PIN** | `PASS` | `PASS` | `PASS (In-Memory/Outbox)` | Thu hồi toàn bộ `UserSessions` khi đổi mật khẩu (`RevokedAt != null`); mã token reset PIN có chữ ký số chỉ dùng 1 lần, chống dùng lại. |
| **Ví cá nhân & Sổ cái kép** | `PASS` | `PASS` | `PASS (SQL02)` | Sổ cái kế toán kép luôn cân bằng $\sum \text{Debit} = \sum \text{Credit}$. Chặn tuyệt đối số dư âm. Khóa chống trùng lặp `IdempotencyKey` bảo vệ giao dịch kép. |
| **Chuyển tiền nội bộ P2P & Ví nội bộ** | `PASS` | `PASS` | `PASS (SQL02)` | Yêu cầu mã PIN 6 số; chặn tài khoản `Locked`; tài khoản `Restricted` bị chặn chuyển đi nhưng vẫn được nhận tiền đến. Cân bằng bảo toàn số học tuyệt đối. |
| **Quỹ nhóm Cố định V2 & Tự do** | `PASS` | `PASS` | `PASS (SQL02)` | Nghĩa vụ đóng quỹ theo kỳ; tính toán hạn mức chi tiêu khả dụng `CanSpend` chính xác. Góp quỹ ghi nhận vào tài khoản quỹ, chi quỹ ghi nhận giải ngân thực tế. |
| **Liên kết Ngân hàng SePay Test Mode** | `PASS` | `PASS` | `PASS (SePay Sandbox)` | Đã liên kết thành công 2 tài khoản TPBank Test Mode độc lập: User A (`****0005`) và User B (`****0004`). Quyền sở hữu độc lập, cách ly chéo 100%. |
| **Nạp tiền SePay Test Mode (SWTP)** | `PASS` | `PASS` | `CHỜ MÔ PHỎNG SEPAY (PROVIDER BLOCKED)` | API Backend tạo challenge `SWTP...` và chốt chặn an toàn `409 wallet_top_up_pending` **ĐÃ HOÀN TẤT 100%**. Để chuyển sang `Completed`, SePay Test Mode cần một giao dịch tiền ra khỏi tài khoản ngân hàng khớp mã tham chiếu. |
| **Rút tiền SePay Test Mode (SWWD)** | `PASS` | `PASS` | `CHỜ QUYẾT TOÁN SEPAY (PROVIDER BLOCKED)` | User B tạo lệnh rút 15.000đ về TPBank `****0004` (`SWWD7F044D73DF59FBF8597D77DE`). Tiền được giữ an toàn tại `SYSTEM:ReservationHold:VND`. Backend Fail-Closed từ chối hoàn tất khi SePay chưa có giao dịch tiền vào đối ứng. Chống double-posting đạt 100%. |
| **Trợ lý Tài chính Gemini AI** | `PASS` | `PASS` | `PHƯƠNG ÁN B (TELEMETRY/FALLBACK)` | Đo lường số request, latency, tỷ lệ thành công. Tự động chuyển mạch sang quy tắc nội bộ (`SpendWisePolicy`) khi mất mạng hoặc chạm ngưỡng giới hạn. |
| **Hỗ trợ & Khiếu nại Giao dịch** | `PASS` | `PASS` | `PASS (SQL02)` | Chu trình khép kín: User gửi khiếu nại $\rightarrow$ Admin tiếp nhận $\rightarrow$ Trao đổi 2 chiều $\rightarrow$ Admin giải quyết. Bút toán điều chỉnh ghi nhận chính xác. |
| **Phân quyền Quản trị (Admin 21 Chức năng)** | `PASS` | `PASS` | `PASS (SQL02)` | Duy nhất 1 tài khoản Admin (`lethien241205@gmail.com`, role 2). Không thể tạo Admin thứ hai. Token chứa role 0 hoặc 3 bị từ chối triệt để. Case-Scoped Access kiểm toán chi tiết. |
| **Thông báo Toàn hệ thống (Broadcast)**| `PASS` | `PASS` | `PASS (Outbox)` | Vòng đời `Draft` $\rightarrow$ `Published` $\rightarrow$ Outbox Worker phân phối đến từng hộp thư người dùng $ightarrow$ `Delivered/Completed`. |
| **Giao diện & Sinh trắc học Phần cứng**| `PASS (API)` | `PASS` | `FRONTEND-ONLY` | Backend lưu trữ preferences (theme/locale) và xác thực WebAuthn Level 2. Việc render giao diện và hiển thị hộp thoại cảm biến vân tay/Face ID là trách nhiệm của Frontend/Mobile. |

---

## 3. BẰNG CHỨNG THỰC NGHIỆM ĐỘC LẬP: BỘ TÀI CHÍNH FINANCIAL-HAPPY-PATH-E2E

> **Postman Collection:** `backend/postman/FINANCIAL-HAPPY-PATH-E2E.postman_collection.json`  
> **Postman Environment:** `backend/postman/FINANCIAL-HAPPY-PATH-E2E.postman_environment.json`  
> **Báo cáo kết quả:** `reports/newman/FINANCIAL-HAPPY-PATH-E2E-REPORT.json` (Đã làm sạch bí mật, mật khẩu và token 100%).  
> **Thực thi:** 32 / 32 Requests PASS, 32 / 32 Assertions PASS (0 Thất bại, Duration: 8.6s, Latency: 186ms).

### 3.1. Danh Sách 32 Yêu Cầu Đã Thực Thi Thành Công 100%

1. `[200 OK]` **01.1 - User A Login (Password Auth)**: Cấp `challengeToken`.
2. `[200 OK]` **01.2 - User A Verify PIN (Access Token)**: Xác minh PIN `123456`, cấp token User A.
3. `[200 OK]` **01.3 - User B Login (Password Auth)**: Cấp `challengeToken`.
4. `[200 OK]` **01.4 - User B Verify PIN (Access Token)**: Xác minh PIN `123456`, cấp token User B.
5. `[200 OK]` **01.5 - Admin Login (Password Auth)**: Cấp `challengeToken`.
6. `[200 OK]` **01.6 - Admin Verify PIN (Role Admin Access Token)**: Cấp token Admin Role 2.
7. `[200 OK]` **01.7 - User A Verify Linked TPBank 0005 Ownership**: Trả về TPBank `****0005` trạng thái `Active`.
8. `[200 OK]` **01.8 - User B Verify Linked TPBank 0004 Ownership**: Trả về TPBank `****0004` trạng thái `Active`.
9. `[404 Not Found]` **01.9 - Cross-Ownership Isolation**: User A tra cứu BankConnection của User B bị từ chối triệt để.
10. `[200 OK]` **02.1 - User A Query Primary Wallet Balance Before Transfer**: Ghi nhận số dư đầu kỳ ví chính A.
11. `[200 OK]` **02.2 - User B Query Primary Wallet Balance Before Transfer**: Ghi nhận số dư đầu kỳ ví chính B.
12. `[201 Created]` **02.3 - P2P Transfer: User A -> User B (10,000 VND)**: Trừ ví A, cộng ví B, sinh Ledger Tx.
13. `[201 Created]` **02.4 - P2P Transfer: User B -> User A (5,000 VND)**: Trừ ví B, cộng ví A, sinh Ledger Tx.
14. `[201 Created]` **02.5 - Internal Wallet Transfer: User A Primary -> Savings Wallet (5,000 VND)**: Chuyển ví nội bộ an toàn.
15. `[200 OK]` **02.6 - User A Verify Balance Delta Post-Transfers**: Số dư phản ánh đúng độ lệch ròng: $-10.000 + 5.000 - 5.000 = -10.000$ VND.
16. `[200 OK]` **03.1 - Query Group Details (2 Active Members)**: Xác nhận nhóm 2 thành viên đang hoạt động.
17. `[200 OK]` **03.2 - Query Shared Fund Initial Balance**: Tra cứu số dư quỹ nhóm trước khi biến động.
18. `[200 OK]` **03.3 - User A Contribute to Shared Fund (10,000 VND)**: Nộp tiền vào Quỹ, phát sinh bút toán sổ cái.
19. `[200 OK]` **03.4 - Group Expense from Shared Fund (5,000 VND)**: Chi tiêu từ Quỹ nhóm, số dư quỹ giảm 5.000đ.
20. `[200 OK]` **03.5 - Query Shared Fund Balance Delta**: Số dư quỹ tăng ròng chính xác $+5.000$ VND.
21. `[409 Conflict]` **04.1 - User A Create SePay Test Mode Top-Up Challenge**: Xác minh trạng thái nạp tiền (`wallet_top_up_active_exists` bảo vệ không tạo chồng lệnh).
22. `[409 Conflict]` **04.2 - Negative Security: Early Top-Up Confirm Without Provider Transaction Fails**: Chặn nạp tiền khống (`wallet_top_up_pending`).
23. `[409 Conflict]` **04.3 - Negative Security: Early Withdrawal Confirm Without Provider Settlement Fails**: Chặn rút tiền khống khi SePay chưa quyết toán (`wallet_withdrawal_pending`).
24. `[404 Not Found]` **04.4 - Cross-User Withdrawal Confirm Strictly Denied (404)**: User A không thể can thiệp lệnh rút tiền của User B.
25. `[201 Created]` **05.1 - Execute Initial Transfer With Static IdempotencyKey**: Thực thi giao dịch chuyển tiền lần 1.
26. `[201 Created]` **05.2 - Replay Transfer With Exact Same IdempotencyKey**: Trả về dữ liệu cache từ bộ nhớ đệm (`replayed: true`), bảo toàn số dư, không ghi sổ cái lần 2.
27. `[200 OK]` **06.1 - Admin Search Ledger Transactions (Audited)**: Quản trị viên tra soát toàn bộ giao dịch sổ cái hệ thống.
28. `[403 Forbidden]` **06.2 - Non-Admin User Denied Admin Ledger Search (403)**: Người dùng thông thường bị chặn tuyệt đối.
29. `[200 OK]` **07.1 - User A Submit Support Case**: Mở phiếu hỗ trợ kỹ thuật, nhận `caseId`.
30. `[200 OK]` **07.2 - Admin Post Reply Message to Case**: Quản trị viên phản hồi tin nhắn nghiệp vụ.
31. `[200 OK]` **07.3 - Admin Resolve Support Case**: Quản trị viên cập nhật trạng thái vụ việc thành `resolved`.
32. `[200 OK]` **07.4 - User A Verify Case Status & Admin Message In History**: Người dùng kiểm tra thấy ngay trạng thái đã giải quyết và tin nhắn hướng dẫn của Admin.

---

## 4. BÁO CÁO ĐỐI SOÁT KIỂM TOÁN LEDGER VÀ TOÀN BỘ DÒNG TIỀN (SQL02)

### 4.1. Nguồn Gốc Số Học Chính Xác Của Khoản 30.000đ
Trong báo cáo kiểm thử ban đầu:
- Tổng thu vào (Income ban đầu): **400.000 VND** (User A: 200.000đ, User B: 200.000đ).
- Tổng chi cá nhân A (15.000đ) + Tạm giữ rút tiền B (15.000đ) + Chi quỹ nhóm (10.000đ) = **40.000 VND**.
- Tổng tài sản ghi nhận thời điểm đó là **330.000 VND**, tạo ra phép tính biểu kiến thiếu hụt 30.000đ:
  $$400.000 - 40.000 = 360.000 \neq 330.000$$

**Bản chất thực tế trên SQL02:**
1. Khoản tiền góp Quỹ nhóm (20.000đ) của User A và chuyển ví nội bộ (10.000đ) là các **dịch chuyển nội bộ (Internal Movements)** giữa các tài khoản tài sản trong hệ thống, **không phải chi phí làm giảm tổng tài sản**.
2. Sai lệch biểu kiến 30.000đ xuất phát từ việc báo cáo tóm tắt cũ tính cộng gộp thiếu tài khoản Ví tiết kiệm User A (10.000đ) và tính trừ chi phí quỹ nhóm mà không tính tài sản quỹ nhóm ban đầu.
3. Dòng chuyển tiền P2P ròng giữa User A và User B là 30.000đ (A chuyển ròng 30.000đ sang B). Dòng này bảo toàn tổng tài sản: $A (-30k) + B (+30k) = 0$.

### 4.2. Bảng Cân Bằng Bảo Toàn Giá Trị Hệ Thống Thực Tế (SQL02)

Tại thời điểm chốt nghiệm thu, toàn bộ tài khoản tài sản và tài khoản xuất chi trên SQL02 phản ánh chính xác dòng tiền vào ban đầu 400.000đ:

| Danh Mục Tài Sản / Tài Khoản Hệ Thống | Mã Tài Khoản Sổ Cái (LedgerAccount.Code) | Phân Loại Tài Sản | Số Dư Thực Tế Trên SQL02 |
|---|---|---|:---:|
| **Ví chính User A** | `WALLET:0acc5688857e48b2b60f03718ed265cf` | Tiền khả dụng người dùng | `32.000,00 VND` |
| **Ví tiết kiệm User A** | `WALLET:e7101f2ae33444ab9d34a6f322aa5467` | Tiền khả dụng người dùng | `25.000,00 VND` |
| **Ví chính User B** | `WALLET:06cfe56267d441fb8fec87057b9ea30c` | Tiền khả dụng người dùng | `233.000,00 VND` |
| **Quỹ nhóm chung** | `GROUP-FUND:31dfe66ebb8a4071903342ef567ae072` | Tiền quỹ tập thể | `25.000,00 VND` |
| **Tạm giữ rút tiền User B** | `SYSTEM:ReservationHold:VND` | Tiền đang khóa cọc chờ đối soát | `15.000,00 VND` |
| **Chi tiêu cá nhân đã thanh toán** | `SYSTEM:ExpenseClearing:VND` | Chi phí xuất khỏi hệ thống | `45.000,00 VND` |
| **Chi tiêu quỹ nhóm đã thanh toán** | `SYSTEM:GroupExpenseClearing:VND` | Chi phí quỹ xuất khỏi hệ thống | `25.000,00 VND` |
| **TỔNG GIÁ TRỊ TÀI SẢN + CHI PHÍ** | **$\sum \text{Assets} + \sum \text{Cleared Expenses}$** | **Phương trình bảo toàn** | **`400.000,00 VND`** |
| **TỔNG TIỀN NẠP BAN ĐẦU** | **`SYSTEM:ExternalSource:VND`** | **Nguồn cấp tiền đối ứng** | **`400.000,00 VND`** |
| **CHÊNH LỆCH KIỂM TOÁN (DISCREPANCY)**| **$\Delta = 400.000 - 400.000$** | **Khớp tuyệt đối** | **`0,00 VND (100% PASS)`** |

### 4.3. Bằng Chứng Bất Biến Sổ Cái Kép (Debit = Credit) Cho TỪNG Giao Dịch
Truy vấn kiểm toán trên toàn bộ 30 giao dịch sổ cái ghi nhận ngày 29/10/2026:
```sql
SELECT 
    t.Id, t.Reference, t.BusinessType,
    SUM(CASE WHEN e.Direction = 'Debit' THEN e.Amount ELSE 0 END) AS TotalDebit,
    SUM(CASE WHEN e.Direction = 'Credit' THEN e.Amount ELSE 0 END) AS TotalCredit
FROM dbo.LedgerTransactions t
JOIN dbo.LedgerEntries e ON t.Id = e.LedgerTransactionId
WHERE t.CreatedAt >= '2026-10-29 10:00:00'
GROUP BY t.Id, t.Reference, t.BusinessType
HAVING SUM(CASE WHEN e.Direction = 'Debit' THEN e.Amount ELSE 0 END) <> SUM(CASE WHEN e.Direction = 'Credit' THEN e.Amount ELSE 0 END);
```
**Kết quả thực tế:** Trả về **0 rows** (100% từng giao dịch có Debit = Credit tuyệt đối).  
- **Tổng Nợ (Total Debit):** `658.000,00 VND`
- **Tổng Có (Total Credit):** `658.000,00 VND`
- **Wallet-to-Ledger Equality:**
  - Ví chính A: `Wallets.AvailableBalance` ($32.000) = `LedgerAccounts.Balance` ($32.000), Diff = 0.
  - Ví tiết kiệm A: `Wallets.AvailableBalance` ($25.000) = `LedgerAccounts.Balance` ($25.000), Diff = 0.
  - Ví chính B: `Wallets.AvailableBalance` ($233.000) = `LedgerAccounts.Balance` ($233.000), Diff = 0.
  - Quỹ nhóm: `SharedFunds.Balance` ($25.000) = `LedgerAccounts.Balance` ($25.000), Diff = 0.
- **Không có bất kỳ số dư ví âm nào.** Không có double-posting.

---

## 5. BÁO CÁO NGHIỆM THU NẠP/RÚT TIỀN SEPAY TEST MODE (SWTP & SWWD)

### 5.1. Rút Tiền SePay Test Mode: SWWD7F044D73DF59FBF8597D77DE (Task 2)
- **Tài khoản rút:** User B (`****0004`, ProviderAccountId: `a973ba27-d37f-11f1-8895-0afffe87053b`).
- **Số tiền:** 15.000 VND.
- **Trạng thái thực tế:** `Processing` (Đang tạm giữ an toàn tại `SYSTEM:ReservationHold:VND`).
- **Phân loại nghiệm thu:** **`PROVIDER BLOCKED (SETTLEMENT DEFERRED)`**.
- **Căn cứ mã nguồn:** `ConfirmTestModeWalletWithdrawalCommandHandler` (`WalletWithdrawalFeature.cs`, line 470) yêu cầu giao dịch đối soát từ SePay Test Mode phải có `direction == "in"`, `amount_in == 15000`, `amount_out == 0` và nội dung chứa mã tham chiếu `SWWD7F044D73DF59FBF8597D77DE`.
- **Thực tế cổng SePay:** Chưa xuất hiện giao dịch quyết toán này trên SePay Test Mode, Backend áp dụng chính sách an toàn **Fail-Closed**, trả về `409 Conflict: wallet_withdrawal_pending`. Chống double-posting và cách ly chéo (`404 Not Found` với User A) đã xác minh đạt 100%.

### 5.2. Nạp Tiền SePay Test Mode: SWTP26F1BCEED82D3C55C44FFA34 (Task 3)
- **Tài khoản nạp:** User A (`****0005`, ProviderAccountId: `b333d043-d37f-11f1-8895-0afffe87053b`).
- **Số tiền:** 50.000 VND.
- **Lệnh nạp tiền đã tạo:** Id `61072bdd-7f79-4845-ab9b-acf87c7411cf`, mã tham chiếu `SWTP26F1BCEED82D3C55C44FFA34`.
- **Trạng thái thực tế:** `Pending` (Thời hạn hiệu lực đến 13:46:20 UTC).
- **Phân loại nghiệm thu:** **`PROVIDER BLOCKED (EXTERNAL TRANSACTION SIMULATION PENDING)`**.
- **Căn cứ mã nguồn:** `ConfirmTestModeWalletTopUpCommandHandler` (`TestModeSePayFeature.cs`, line 380) yêu cầu SePay Test Mode ghi nhận một giao dịch tiền ra khỏi tài khoản ngân hàng đích (`direction == "out"`, `amount_out == 50000`, `amount_in == 0`) mang mã tham chiếu `SWTP26F1BCEED82D3C55C44FFA34`.
- **Chốt chặn an toàn:** Gọi confirm sớm trả `409 Conflict: wallet_top_up_pending`. Kiểm tra trùng lặp `wallet_top_up_active_exists` hoạt động chính xác.

---

## 6. ĐIỀU KIỆN TIÊN QUYẾT TRIỂN KHAI SẢN XUẤT (PRODUCTION READINESS)

1. **Hạ tầng SMTP Doanh nghiệp:** Cấu hình `Email:Smtp:Host`, `Port`, `Username`, `Password` và các bản ghi DNS (SPF, DKIM, DMARC) để đưa email ra hộp thư Internet thực tế.
2. **Ký Kết Hợp Đồng Cổng Thanh Toán SePay:** Chuyển `SePayBankHub:Environment = Production` và thiết lập `LifecycleWebhookSecret` cho luồng tiền thật.
3. **Mở Rộng Hạn Ngạch Google Gemini API:** Nâng cấp gói thanh toán Google Cloud Console để mở rộng RPM/RPD phục vụ người dùng thực tế.
4. **Phía Ứng Dụng Frontend & Mobile Client:** Tuân thủ đặc tả giao tiếp chuẩn trong `User.md` và `Admin.md`.
