# SPENDWISE — BÁO CÁO ĐỒNG BỘ TOÀN DIỆN TÀI LIỆU MARKDOWN
## SYNCHRONIZATION SUMMARY PRIOR TO GITHUB PUSH

**Thời gian lập báo cáo:** 2026-10-27  
**Workspace:** `E:\source\SpendWise-final-merged`  
**Database TEST:** `SpendWise_E2E_G4` trên `DESKTOP-B5KA3LT\SQLEXPRESS02`  
**Trạng thái mục tiêu:** Đã đồng bộ 100% tài liệu theo hiện trạng thực tế Backend mới nhất; chuẩn bị bàn giao sang Phase Backend Admin.

---

## 1. DANH SÁCH CÁC TÀI LIỆU MARKDOWN ĐÃ ĐỒNG BỘ

| STT | Đường dẫn tệp | Phân hệ tài liệu | Tóm tắt các thay đổi trọng yếu |
| :---: | :--- | :--- | :--- |
| 1 | [`User.md`](../../User.md) | **User Product Spec & API Contract** | Cập nhật trạng thái `BACKEND USER DEMO — ACCEPTED & LOCKED`; ghi nhận mốc Newman Full Run (306 requests, 343 assertions PASS); chuẩn hóa luồng Sinh trắc học Demo (UI Mockup vs Backend WebAuthn); đồng bộ Fixed Fund V2 (bãi bỏ hoàn toàn 20% và Pending24h); ghi nhận đầy đủ bằng chứng SePay Test Mode Nạp/Rút Demo (User B TPBank TEST rút 50k); hoàn thiện bảng API Contract và hướng dẫn tích hợp Frontend (Google AI Studio). |
| 2 | [`Admin.md`](../../Admin.md) | **Admin Backend Contract** | Cập nhật trạng thái chuẩn bị Phase Admin (`PREPARING FOR PHASE BACKEND ADMIN`); phân biệt rõ ràng bằng chứng nghiệm thu lịch sử Task G5 (141 requests, 227 assertions) với đợt kiểm thử mới; khẳng định chính sách duy nhất 1 System Admin, cấm SuperAdmin; mô tả chi tiết 5 nhóm ADM-01 đến ADM-05, nguyên tắc truy cập nhạy cảm theo Case hỗ trợ và chế tài tài khoản (Level 1 Restricted vs Level 2 Locked). |
| 3 | [`POSTMAN-API-TEST-REPORT.md`](../../POSTMAN-API-TEST-REPORT.md) | **Postman Execution Report** | Nâng cấp toàn diện từ kết quả 300 requests lên kết quả Audited Final chính thức: **306 requests, 343/343 assertions PASS, 0 FAIL, exit code 0**; phân loại chi tiết Happy-Path (295 assertions / 281 requests), Negative (38 assertions / 22 requests) và Controlled Fallback (10 assertions / 3 requests HTTP 503); liệt kê chi tiết kết quả thực thi của toàn bộ 19 thư mục cấp cao. |
| 4 | [`POSTMAN-ENDPOINT-COVERAGE.md`](../../POSTMAN-ENDPOINT-COVERAGE.md) | **Endpoint Coverage Matrix** | Đồng bộ hóa chỉ số bao phủ: 216 User operations thuộc Historical Baseline, 306 requests, 343 assertions PASS (100% baseline); phân định rõ không tuyên bố coverage 100% của toàn bộ endpoint mới sau khi thêm biometric trong Postman suite mà xác thực biometric qua test suite tự động chuyên dụng; phân loại chính xác 192 `SUCCESS_VERIFIED`, 12 `BOTH_VERIFIED`, 4 `DEMO_SETTLEMENT_VERIFIED` và 8 `PROVIDER_FALLBACK_VERIFIED`. |
| 5 | [`POSTMAN-TEST-QUALITY-AUDIT.md`](../../POSTMAN-TEST-QUALITY-AUDIT.md) | **Test Quality & Forensic Audit** | Ghi nhận chi tiết các vấn đề nghiệp vụ đã xử lý dứt điểm: dynamic runId, session revocation, idempotency keys, giới hạn 3 ví, Budget Flow F; bóc tách **bài học kỹ thuật số 1 về việc không dùng SQL hoán đổi quyền sở hữu BankConnection**; mô tả kiến trúc Reservation Hold và SePay Sandbox settlement. |
| 6 | [`backend/postman/README.md`](../../backend/postman/README.md) | **Postman Guide & Run Instructions** | Viết lại toàn bộ hướng dẫn chạy Newman CLI với `SpendWise-User-API.postman_collection.json` và `SpendWise-Test.runtime.local.json`; hướng dẫn chạy các bộ test tự động Sinh trắc học (`run_security_suite.js`, `run_integration_suite.js`); chuyển Task 14/17 thành mục tham khảo lịch sử. |
| 7 | [`BACKEND-USER-READINESS-REPORT.md`](../../BACKEND-USER-READINESS-REPORT.md) | **Backend Readiness Report** | Đồng bộ trạng thái đỉnh trang: `BACKEND USER DEMO — ACCEPTED & LOCKED`, ghi nhận 100% Ready trên toàn bộ 11 miền nghiệp vụ User, duy trì ranh giới minh bạch với Live Payout. |
| 8 | [`BACKEND-BASELINE.md`](../../BACKEND-BASELINE.md) | **Backend Baseline & Architecture Traceability** | Thêm khối trạng thái hiện tại `BACKEND USER DEMO — ACCEPTED & LOCKED`, lưu giữ toàn bộ tiến trình lịch sử kiến trúc từ Task 16.1 đến Task G5. |

---

## 2. MỐC SOURCE VÀ BẰNG CHỨNG KIỂM THỬ ĐÃ DẪN CHỨNG

1. **Commit SHA & Baseline Source:**
   - Commit cơ sở: `5ce3ab8ff02adf2dd267bcbb03f41627bed37f98`.
   - Các commit/sửa đổi hardening tiếp theo: Phục hồi Bank Connection Ownership, Tích hợp WebAuthn Level 2 và Xử lý an toàn Unique Filtered Index cho TopUp/Withdrawal.
2. **Bằng chứng Kiểm thử Tự động (Test Acceptance Evidence):**
   - **Báo cáo Nghiệm thu Khóa User:** [`reports/newman/backend-user-final-lock.md`](../newman/backend-user-final-lock.md) và [`reports/newman/backend-user-final-verified-summary.md`](../newman/backend-user-final-verified-summary.md) (Full Newman User Suite 306 requests, 343 assertions PASS, Exit code 0, 0 failures; raw JSON `backend-user-final-verified.json` lưu trữ nội bộ).
   - **Tài liệu Nghiệm thu Sinh trắc học:** [`reports/biometric/biometric-final-acceptance.md`](../biometric/biometric-final-acceptance.md) và [`reports/biometric/biometric-implementation-summary.md`](../biometric/biometric-implementation-summary.md) (kèm kết quả kiểm thử bảo mật 27/27 PASS và tích hợp tài chính 14/14 PASS lưu trữ nội bộ).
   - **Giao dịch Nghiệm thu Rút tiền Cuối:**
     - Actor: User B (`3A8B3FD7-3AF2-46AB-8FA2-BD43D6B1E67A`).
     - Tài khoản ngân hàng: TPBank TEST `****0002` (`E9E1753B-552C-4E8F-83CA-D796AA62BFCE`).
     - Withdrawal ID: `5F89A5D9-A160-4242-B595-25DA35063411`.
     - Mã tham chiếu: `SWWD3EEBA411C767A510FB7BD57C`.
     - Số tiền: `50.000 VND`.
     - Biến động TPBank TEST: `150.000đ` $\rightarrow$ `200.000đ` (+50.000đ).
     - Biến động ví User B: `4.875.000đ` $\rightarrow$ `4.825.000đ` (-50.000đ, không double-debiting).
     - Trạng thái: `Completed`, net biến động trên `SYSTEM:ReservationHold:VND` bằng đúng 0đ.

---

## 3. CÁC MÂU THUẪN TÀI LIỆU ĐÃ ĐƯỢC GIẢI QUYẾT TRIỆT ĐỂ

1. **Mâu thuẫn Quy tắc Quỹ Cố định V2 (Fixed Fund V2):**
   - *Trước đây:* Đầu tài liệu ghi đã bãi bỏ ngưỡng 20% và Pending24h, nhưng các bảng API và phụ lục cũ vẫn nhắc lại cơ chế hold 24h và giới hạn 20%.
   - *Đã xử lý:* Xóa bỏ triệt để toàn bộ các quy định cũ về ngưỡng 20% và Pending24h trên toàn bộ các tài liệu. Chuẩn hóa quy tắc: chi tiêu hợp lệ được giải ngân tức thì; quyền chi thực tế (`EffectiveCanSpend`) được tính động khi thành viên đạt 100% nghĩa vụ và quỹ đạt 100% mục tiêu nhóm.
2. **Mâu thuẫn Quyền sở hữu Ngân hàng (Bank Connection Ownership):**
   - *Trước đây:* Tài liệu chưa phản ánh rõ việc hoàn nguyên ownership sau thao tác SQL swap giữa User A và User B.
   - *Đã xử lý:* Ghi nhận chi tiết việc hoàn nguyên đúng chủ sở hữu gốc theo `BankLinkSessions` (User A sở hữu VPBank `****0001`; User B sở hữu TPBank `****0002`). Nêu rõ bài học về việc không sử dụng SQL để tạo kết quả PASS giả.
3. **Mâu thuẫn Phạm vi Sinh trắc học (Biometric Demo Scope):**
   - *Trước đây:* Nguy cơ hiểu lầm rằng Face ID / Vân tay trên giao diện Frontend đã được xác thực phần cứng thật.
   - *Đã xử lý:* Phân định rạch ròi: Frontend Google AI Studio là **DEMO SIMULATION** (hoạt ảnh trình diễn giao diện, không đại diện cho cảm biến phần cứng); còn Backend đã hỗ trợ và kiểm thử đầy đủ chuẩn mật mã WebAuthn Level 2 (`UV=1`, `UP=1`, `ES256`, zero-knowledge SPKI).
4. **Mâu thuẫn Trạng thái Phase Admin:**
   - *Trước đây:* Dễ gây hiểu lầm rằng Phase Admin đã hoàn tất kiểm thử cùng với Backend User.
   - *Đã xử lý:* Xác định rõ: Backend User đã LOCK; Backend Admin đang ở trạng thái **chuẩn bị kiểm thử (Preparing for Phase Admin)**; các kết quả 141 requests / 227 assertions trước đây được ghi nhận chính xác là **bằng chứng nghiệm thu lịch sử (historical baseline)** từ Task G5.

---

## 4. CÁC PHẦN CHƯA XÁC MINH & BÀN GIAO TIẾP THEO

1. **SePay Live Payout & Production Banking:** Chưa kết nối cổng thanh toán thật (yêu cầu hợp đồng đối tác doanh nghiệp SePay Live).
2. **Physical Mobile Device Receipt:** Chưa kiểm thử nhận thông báo trên thiết bị di động thật (yêu cầu ứng dụng native kết nối Expo project credentials).
3. **Frontend UI Integration:** Giao diện người dùng Mobile / Web đang phát triển trên Google AI Studio, sẽ được tích hợp và nghiệm thu ở giai đoạn riêng.
4. **Phase Backend Admin:** Sẽ được kích hoạt ngay sau khi Product Owner phê duyệt báo cáo đồng bộ tài liệu này.

---

## 5. KẾT LUẬN & KIỂM TRA BẢO MẬT

- **Kiểm tra rò rỉ bí mật:** 100% các file Markdown không chứa connection string thật, mật khẩu tài khoản thật, private keys, hay token nhạy cảm.
- **Tính toàn vẹn mã nguồn:** 0 dòng mã nguồn C# hay logic nghiệp vụ bị can thiệp.
- **Cam kết Git:** **Chưa thực hiện commit/push**, bảo lưu toàn bộ thay đổi để Product Owner rà soát.
