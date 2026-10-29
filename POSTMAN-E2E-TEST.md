# BÁO CÁO KIỂM THỬ THỰC CHẤT VÀ NGHIỆM THU E2E BACKEND SPENDWISE V1.0

> **Thời điểm nghiệm thu:** 29/10/2026 — 13:55:00+07:00  
> **Cơ sở dữ liệu kiểm nghiệm:** Microsoft SQL Server `DESKTOP-B5KA3LT\SQLEXPRESS02` / `SpendWise_E2E_G4` (SQL02 cách ly tuyệt đối, không kết nối cơ sở dữ liệu sản xuất SQL01).  
> **Cổng dịch vụ Kestrel API:** `http://127.0.0.1:5158` (Chế độ Release, Background Daemon).  
> **Tuyên bố trung thực:** Báo cáo phản ánh chính xác kết quả kiểm thử thực tế, phân định rõ ràng giữa các phần đã xác minh (VERIFIED), các phần chưa xác minh do phụ thuộc môi trường ngoài (NOT VERIFIED) và các chức năng thuộc trách nhiệm độc quyền của Frontend/Mobile (FRONTEND-ONLY).

---

## 1. TỔNG HỢP KẾT QUẢ KIỂM THỬ (EXECUTIVE SUMMARY)

```
┌────────────────────────────────────────────────────────┬───────────────────┬───────────────────┬─────────────┐
│ Phân Hệ & Bộ Kiểm Thử (Test Suite)                     │ Requests / Tests  │ Assertions Passed │ Trạng Thái  │
├────────────────────────────────────────────────────────┼───────────────────┼───────────────────┼─────────────┤
│ 1. Backend User Acceptance Suite (Newman)              │      306 requests │         343 / 343 │ PASS (100%) │
│ 2. Backend Admin Acceptance Suite (Newman)             │       80 requests │         138 / 138 │ PASS (100%) │
│ 3. Support & Direct Complaint E2E Flow (Newman)        │       22 requests │           42 / 42 │ PASS (100%) │
│ 4. Dynamic Email Lifecycle E2E Suite (xUnit)          │          6 tests  │           32 / 32 │ PASS (100%) │
│ 5. Targeted .NET Regression Suite (xUnit)              │         82 tests  │           82 / 82 │ PASS (100%) │
├────────────────────────────────────────────────────────┼───────────────────┼───────────────────┼─────────────┤
│ TỔNG CỘNG THỰC THI                                     │ 408 reqs + 88 tst │    637 Assertions │ 0 THẤT BẠI  │
│ KẾT QUẢ BIÊN DỊCH (`dotnet build SpendWise.API`)        │ 0 Cảnh báo        │ 0 Lỗi             │ BUILD PASS  │
└────────────────────────────────────────────────────────┴───────────────────┴───────────────────┴─────────────┘
```

---

## 2. BẢNG PHÂN ĐỊNH RANH GIỚI VÀ TRẠNG THÁI NGHIỆM THU CHI TIẾT

| Hạng Mục Kiểm Thử | Trạng Thái API | Trạng Thái Nghiệp Vụ | Trạng Thái Ngoại Vi / Thực Tế | Đánh Giá & Ghi Chú Trung Thực |
|---|:---:|:---:|:---:|---|
| **Đăng ký, Đăng nhập & Xác thực** | `PASS` | `PASS` | `PASS (Outbox/CSPRNG)` | Sinh OTP 8 số ngẫu nhiên CSPRNG, lưu trữ mã hóa trong Outbox, xác minh đúng mã, chống replay dứt điểm. |
| **Gửi/Nhận Email Hộp thư Thực tế** | `PASS` | `PASS` | `CHƯA XÁC MINH (NOT VERIFIED)` | Hàng đợi Outbox, mã hóa AES, mẫu HTML/Text (14/14 template) và cơ chế retry **ĐÃ ĐẠT**. Việc truyền tin SMTP ra Internet đến máy chủ bên thứ ba (Gmail/Outlook) và người dùng bấm mở hộp thư **CHƯA ĐƯỢC XÁC MINH TỰ ĐỘNG** trong môi trường E2E để bảo vệ an toàn thông tin cá nhân. |
| **Quên mật khẩu & Reset PIN** | `PASS` | `PASS` | `PASS (In-Memory/Outbox)` | Thu hồi toàn bộ `UserSessions` khi đổi mật khẩu (`RevokedAt != null`); mã token reset PIN có chữ ký số chỉ dùng 1 lần, chống dùng lại. |
| **Ví cá nhân & Sổ cái kép** | `PASS` | `PASS` | `PASS (SQL02)` | Sổ cái kế toán kép luôn cân bằng $\sum \text{Debit} = \sum \text{Credit}$. Chặn tuyệt đối số dư âm. Khóa chống trùng lặp `IdempotencyKey` bảo vệ giao dịch kép. |
| **Chuyển tiền nội bộ P2P** | `PASS` | `PASS` | `PASS (SQL02)` | Yêu cầu mã PIN 6 số; chặn tài khoản `Locked`; tài khoản `Restricted` bị chặn chuyển đi nhưng vẫn được nhận tiền đến. |
| **Quỹ nhóm Cố định V2** | `PASS` | `PASS` | `PASS (SQL02)` | Nghĩa vụ đóng quỹ theo kỳ; tính toán hạn mức chi tiêu khả dụng `CanSpend` chính xác; loại bỏ cơ chế tạm giữ 20% và pending 24h. |
| **Quỹ nhóm Tự do** | `PASS` | `PASS` | `PASS (SQL02)` | Góp vốn linh hoạt, đề xuất chi tiêu/rút quỹ phải được Chủ quỹ phê duyệt bằng mã PIN. |
| **Cổng nạp tiền Ngân hàng SePay** | `PASS` | `PASS` | `CHƯA XÁC MINH TIỀN THẬT (NOT VERIFIED)` | Tạo ý định nạp tiền (VietQR), tiếp nhận Webhook IPN, đối soát mã nạp tiền và cộng số dư ví **ĐÃ PASS TRÊN SANDBOX**. Không tạo và không xác minh giao dịch tài khoản ngân hàng thực tế theo chính sách an toàn. |
| **Trợ lý Tài chính Gemini AI** | `PASS` | `PASS` | `PHƯƠNG ÁN B (TELEMETRY/FALLBACK)` | Đo lường số request, latency, tỷ lệ thành công. Tự động chuyển mạch sang quy tắc nội bộ (`SpendWisePolicy`) khi mất mạng hoặc chạm ngưỡng giới hạn. Hạn ngạch vô hạn trực tiếp trên Live Cloud **CHƯA XÁC MINH**. |
| **Hỗ trợ & Khiếu nại Giao dịch** | `PASS` | `PASS` | `PASS (SQL02)` | Chu trình khép kín: User khiếu nại từ giao dịch cụ thể $\rightarrow$ Admin tiếp nhận $\rightarrow$ Trao đổi 2 chiều $\rightarrow$ Admin phê duyệt hoàn tiền vào ví người dùng. Không có endpoint tạo Dispute độc lập trôi nổi. |
| **Phân quyền Quản trị (Admin)** | `PASS` | `PASS` | `PASS (SQL02)` | Duy nhất 1 tài khoản Admin (`lethien241205@gmail.com`, role 2). Không thể tạo Admin thứ hai. Token chứa role 0 hoặc 3 bị từ chối triệt để. |
| **Thông báo Toàn hệ thống (Broadcast)**| `PASS` | `PASS` | `PASS (Outbox)` | Vòng đời `Draft` $\rightarrow$ `Published` $\rightarrow$ Outbox Worker phân phối đến từng hộp thư người dùng $\rightarrow$ `Delivered/Completed`. Không tính `Scheduled` là thành công. |
| **Giao diện Trực quan (Sáng/Tối/Ngôn ngữ)**| `PASS (API Lưu Prefs)` | `N/A` | `FRONTEND-ONLY` | Backend lưu trữ và trả về chính xác theme (`light`/`dark`/`system`) và locale (`vi-VN`/`en-US`). Việc hiển thị màu sắc, phông chữ, hoạt ảnh là trách nhiệm của Frontend/Mobile. |
| **Sinh trắc học Phần cứng (Biometrics)** | `PASS (WebAuthn API)` | `PASS` | `FRONTEND-ONLY` | Backend xác thực thành công chữ ký WebAuthn Level 2 và cấp token. Việc bật hộp thoại quét Face ID/Vân tay native là trách nhiệm của OS và Mobile App. |

---

## 3. CHI TIẾT BẰNG CHỨNG KIỂM THỬ TỪNG PHÂN HỆ

### 3.1. Phân hệ User (Newman 306 Requests / 343 Assertions PASS 100%)
- **Báo cáo nguồn:** `reports/newman/backend-user-final-verified-20261029.json`
- **Các nhóm nghiệp vụ đã kiểm tra thành công:**
  1. `Folder 01 - Authentication`: Đăng ký, OTP, thiết lập PIN, đăng nhập 2 bước, cấp mới token, thu hồi phiên.
  2. `Folder 02 - Profile & Preferences`: Xem thông tin, đổi thông tin cá nhân, cập nhật theme, ngôn ngữ.
  3. `Folder 03 - Wallets & Ledger`: Tạo ví, xem số dư, tính toán tổng tài sản, kiểm tra ràng buộc không âm.
  4. `Folder 04 - Transactions`: Chi tiêu cá nhân, phân loại danh mục, lịch sử giao dịch.
  5. `Folder 05 - P2P Transfers`: Chuyển tiền SpendId, chuyển tiền qua SĐT, xác thực PIN, kiểm tra tài khoản nhận.
  6. `Folder 06 - Group Funds (Fixed V2 & Free)`: Tạo quỹ, mời thành viên, nộp quỹ, tính `CanSpend`, duyệt đề xuất chi.
  7. `Folder 07 - Budgets`: Thiết lập hạn mức ngân sách theo tháng, tính tỷ lệ chi tiêu thực tế, cảnh báo vượt ngưỡng.
  8. `Folder 08 - Savings Goals`: Mục tiêu tích lũy, trích nạp từ ví chính, hoàn thành mục tiêu.
  9. `Folder 09 - Recurring Transactions`: Thiết lập lệnh định kỳ, lịch chạy tiếp theo, kiểm tra số dư tự động.
  10. `Folder 10 - Support & Complaints`: Gửi khiếu nại từ giao dịch, nhận phản hồi từ Admin, theo dõi tiến độ.
  11. `Folder 11 - Banking Sandbox (SePay)`: Tạo ý định nạp tiền, sinh mã VietQR, giả lập IPN Webhook.
  12. `Folder 12 - AI Assistant`: Tạo hội thoại, hỏi đáp tài chính, kích hoạt chính sách an toàn.

### 3.2. Phân hệ Admin (Newman 80 Requests / 138 Assertions PASS 100%)
- **Báo cáo nguồn:** `ADMIN-V1-FINAL-NEWMAN-REPORT.json`
- **Đủ 21/21 chức năng thuộc 5 nhóm lớn:**
  1. `ADM-01 (Operations Dashboard)`: Thống kê số lượng người dùng, số vụ hỗ trợ chờ xử lý, cảnh báo vận hành.
  2. `ADM-02 (User Governance)`: Tra cứu người dùng, xem hồ sơ/phiên theo Case-Scoped Access, khóa tài khoản (`Locked` thu hồi phiên ngay lập tức), hạn chế tài khoản (`Restricted` chặn ghi nợ), mở khóa (`Active`).
  3. `ADM-03 (Financial & Ledger)`: Bảng cân đối thử Sổ cái kép ($\text{Debit} = \text{Credit}$), đối soát giao dịch, nhật ký kiểm toán `AdminAuditLogs`.
  4. `ADM-04 (Support Governance)`: Phân loại hồ sơ, trao đổi tin nhắn với khách hàng, phê duyệt (`Resolved` hoàn tiền), từ chối (`Rejected`).
  5. `ADM-05 (Broadcast & Monitoring)`: Phát hành broadcast theo chu trình Outbox, telemetry giám sát Gemini AI và SePay BankHub.

### 3.3. Phân hệ Hỗ trợ & Khiếu nại Trực tiếp (Newman 22 Requests / 42 Assertions PASS 100%)
- **Báo cáo nguồn:** `reports/newman/support_complaint_e2e_newman_report.json`
- **Kiểm thử liên thông 6 bước:**
  - Bước 1: User thực hiện giao dịch tài chính ghi sổ cái.
  - Bước 2: User phát hiện sai sót, tạo `SupportCase` gắn với `TransactionId`.
  - Bước 3: Admin tiếp nhận vụ việc, chuyển trạng thái sang `InReview`.
  - Bước 4: Admin tra cứu hồ sơ người dùng thông qua endpoint bảo vệ theo mã vụ việc (`Case-Scoped Access`).
  - Bước 5: Hai bên trao đổi tin nhắn bằng chứng qua lại.
  - Bước 6: Admin phê duyệt giải quyết, hệ thống tự động hoàn tiền vào ví người dùng và kết thúc hồ sơ.

### 3.4. Kiểm thử Chuyên sâu Vòng đời Email & Dynamic OTP (xUnit 6/6 PASS 100%)
- **Tệp mã nguồn kiểm thử:** `backend/tests/SpendWise.Tests/RealEmailLifecycleE2ETests.cs`
- **Kết quả thực thi:** `Passed! - Failed: 0, Passed: 6, Skipped: 0, Total: 6`
- **Bằng chứng chi tiết:**
  1. `Registration_DynamicOtp_FromOutbox_FullLifecycle_Succeeds_AndReplayFails`: Đăng ký $\rightarrow$ giải mã OTP động 8 số ngẫu nhiên từ Outbox $\rightarrow$ xác minh thành công $\rightarrow$ thử lại mã cũ bị từ chối 400 $\rightarrow$ thiết lập PIN $\rightarrow$ đăng nhập nhận JWT.
  2. `ResendOtp_DynamicOtp_RevokesPreviousOtp_AndPreventsUse`: Yêu cầu gửi lại OTP $\rightarrow$ mã cũ bị thu hồi trong DB (`RevokedAt != null`) $\rightarrow$ nhập mã cũ bị từ chối $\rightarrow$ nhập mã mới thành công.
  3. `ForgotPassword_DynamicOtp_ResetsPassword_AndRevokesSessions`: Yêu cầu quên mật khẩu $\rightarrow$ trích xuất OTP $\rightarrow$ đổi mật khẩu mới $\rightarrow$ toàn bộ session cũ bị vô hiệu hóa $\rightarrow$ mật khẩu cũ không thể đăng nhập $\rightarrow$ mật khẩu mới đăng nhập thành công.
  4. `ChangeEmail_DynamicOtp_SentToNewAddress_AndSecurityNoticeSentToOldAddress`: Yêu cầu đổi email $\rightarrow$ Outbox ghi nhận 2 email: OTP gửi tới email mới, cảnh báo gửi tới email cũ $\rightarrow$ xác nhận bằng OTP $\rightarrow$ cập nhật email thành công.
  5. `SecurityPinReset_EmailToken_Lifecycle_Succeeds_AndSingleUseEnforced`: Yêu cầu reset PIN $\rightarrow$ trích xuất signed raw token từ email $\rightarrow$ đặt lại PIN mới $\rightarrow$ thử dùng lại token cũ bị chặn 401 Unauthorized.
  6. `All_14_Email_Templates_Render_Cleanly_Without_Exceptions`: Kiểm tra cả 14 mẫu email hệ thống (chào mừng, OTP đăng ký, OTP quên mật khẩu, thông báo đổi mật khẩu, link reset PIN, cảnh báo bảo mật, thông báo giao dịch, cập nhật khiếu nại...) biên dịch và hiển thị dữ liệu đầy đủ không lỗi.

---

## 4. CÁC HẠN CHẾ VÀ ĐIỀU KIỆN TIÊN QUYẾT CHO TRIỂN KHAI SẢN XUẤT

1. **Hạ tầng Gửi Email Internet Thực tế:**
   - Để kích hoạt gửi email ra ngoài Internet, môi trường triển khai Production cần cấu hình biến môi trường SMTP hợp lệ: `Email:Smtp:Host`, `Email:Smtp:Port`, `Email:Smtp:Username`, `Email:Smtp:Password`.
   - Cần cấu hình bản ghi SPF, DKIM và DMARC cho tên miền gửi thư để tránh bị máy chủ đích đánh dấu thư rác.
2. **Hạ tầng Cổng Thanh toán Ngân hàng:**
   - Khi doanh nghiệp hoàn tất hợp đồng đối tác với SePay/Ngân hàng thương mại, cấu hình `SePayBankHub:Environment = Production` và cập nhật khóa ký `LifecycleWebhookSecret`.
3. **Quản trị Tài nguyên Google Gemini:**
   - Đăng ký hạn ngạch trả phí (Pay-as-you-go) trên Google Cloud Console cho Gemini API để tránh chạm ngưỡng 15 RPM/1500 RPD của gói miễn phí khi lưu lượng người dùng tăng cao.
4. **Phía Ứng dụng Frontend/Mobile:**
   - Bắt buộc triển khai đầy đủ cơ chế lưu trữ bảo mật Secure Storage (Keychain/Keystore), xử lý giao tiếp cảm biến sinh trắc học native và chuỗi giao diện đa ngôn ngữ theo tài liệu `User.md`.
