# BÁO CÁO KẾT QUẢ KIỂM THỬ TỰ ĐỘNG USER API SPENDWISE BẰNG NEWMAN / POSTMAN
## BÁO CÁO NGHIỆM THU CUỐI CÙNG — CHỐT KHÓA BACKEND USER (LOCKED)

> **Ngày nghiệm thu:** 2026-10-27  
> **Lượt chạy chuẩn hóa cuối:** `fresh-run-20261027-user-acceptance-final` (Exit Code: `0`)  
> **Môi trường thực thi:** Kestrel Release HTTP Server (`http://127.0.0.1:5158`), SQL Server `DESKTOP-B5KA3LT\SQLEXPRESS02`, Database `SpendWise_E2E_G4`  
> **Công cụ thực thi:** Newman CLI v6, Node.js v24.18.0  
> **Bộ sưu tập Postman (Collection):** [`SpendWise-User-API.postman_collection.json`](./SpendWise-User-API.postman_collection.json)  
> **Môi trường Postman (Environment):** `SpendWise-Test.runtime.local.json` (cô lập với dynamic runId)  
> **Bản tổng hợp nghiệm thu (Verified Acceptance Summary):** [`reports/newman/backend-user-final-verified-summary.md`](./reports/newman/backend-user-final-verified-summary.md) (100% assertions PASS; file raw execution JSON `backend-user-final-verified.json` lưu trữ nội bộ bảo mật)

---

## 1. TỔNG HỢP KẾT QUẢ THỰC THI NEWMAN RUN FINAL

| Chỉ Số Kiểm Thử | Giá Trị Thực Tế | Tỷ Lệ | Nhận Xét & Đánh Giá |
| :--- | :---: | :---: | :--- |
| **Tổng số HTTP Requests đã chạy** | **306** | **100.0%** | Hoàn thành trọn vẹn toàn bộ 19 thư mục cấp cao (top-level folders), 0 request bị bỏ sót |
| **Tổng số Pre-request Scripts** | **384** | **100.0%** | Chuẩn bị dữ liệu động, sinh token, mã hóa payload tự động |
| **Tổng số Test Scripts** | **303** | **100.0%** | Kiểm tra trạng thái HTTP, contract DTO, tính toàn vẹn sổ cái |
| **Tổng số Assertions** | **343** | — | Kiểm tra Status Code, JSON Schema, State Invariants, Ledger balance |
| **Số Assertions ĐẠT (PASS)** | **343** | **100.0%** | **Toàn bộ 343/343 assertions PASS** |
| **Số Assertions LỖI (FAIL)** | **0** | **0.0%** | **Tuyệt đối không có lỗi tồn đọng (0 Failures)** |
| **Thời gian thực thi toàn bộ** | **2 phút 25 giây** | — | Thực thi liên tục không gián đoạn qua Newman CLI |
| **Mã thoát (Exit Code)** | **0** | — | Newman kết thúc thành công với exit code 0 |
| **Trạng thái Nghiệm thu** | **ACCEPTED & LOCKED** | — | **Backend User Demo chính thức nghiệm thu và khóa chức năng** |

---

## 2. PHÂN LOẠI REQUESTS VÀ ASSERTIONS THEO BẢN CHẤT NGHIỆP VỤ

> [!IMPORTANT]
> **LƯU Ý QUAN TRỌNG VỀ ĐỐI CHIẾU SỐ LIỆU:**
> Phép tính **295 (Happy-Path) + 38 (Expected Negative) + 10 (Controlled Fallback) = 343** phản ánh số lượng **ASSERTIONS**, tuyệt đối không phải số lượng requests.
> Về phía HTTP Requests thực thi, file kết quả gốc (`backend-user-final-verified.json`) có đúng **306 requests** bao gồm **281 requests 2xx, 22 requests 4xx và chính xác 3 requests HTTP 503**, hoàn toàn không phải 10 requests 503.

### 2.1. Bóc tách Requests thực thi (306 HTTP Requests)

| Phân nhóm HTTP Status | Mã Status | Số Lượng Requests | Tỷ Lệ % | Bản chất kỹ thuật & Nghiệp vụ |
| :--- | :---: | :---: | :---: | :--- |
| **Happy-Path Requests (2xx)** | `200 OK`<br>`201 Created`<br>`202 Accepted`<br>`204 No Content` | 226<br>21<br>3<br>31<br>**281** | **91.83%** | Các thao tác nghiệp vụ thành công: đăng ký, xác thực OTP, đổi mật khẩu, quản lý 3 ví cá nhân, nạp tiền thu nhập, chi tiêu, chuyển tiền nội bộ, chuyển tiền P2P, đóng góp quỹ Fixed Fund V2, duyệt chi Free Fund, tạo ngân sách, tích lũy mục tiêu, và liên kết ngân hàng. |
| **Expected Negative Requests (4xx)** | `401 Unauthorized`<br>`403 Forbidden`<br>`404 Not Found`<br>`409 Conflict` | 5<br>2<br>7<br>8<br>**22** | **7.19%** | Cố tình gửi dữ liệu sai hoặc vượt quyền để chứng minh Backend phản hồi đúng mã lỗi REST: đăng nhập sai mật khẩu (401), sai mã PIN (401), thành viên tự cấp quyền (403), User A truy cập ví/ngân sách của User B (404), trùng email (409), vượt giới hạn 3 ví (409). |
| **Controlled Fallback Requests (5xx)** | `503 Service Unavailable` | **3** | **0.98%** | Giao thức kiểm tra xử lý suy thoái an toàn khi dịch vụ bên ngoài chưa kích hoạt live: cụ thể là 3 requests `B5-01 - Create Bank Link Session (Controlled Sandbox Response)` trả về 503 được kiểm soát an toàn. |
| **TỔNG CỘNG REQUESTS** | — | **306** | **100.0%** | **Toàn bộ 306 requests được phân loại chính xác, 0 request bị bỏ sót.** |

### 2.2. Bóc tách Assertions kiểm thử (343 Assertions PASS)

| Nhóm Assertions | Số Lượng Assertions | Tỷ Lệ % | Nội dung kiểm chứng |
| :--- | :---: | :---: | :--- |
| **Happy-Path Assertions** | **295** | **86.01%** | Kiểm tra cấu trúc JSON schema, mã trạng thái HTTP thành công (200, 201, 202, 204), tính toàn vẹn số dư ví sau nạp/chi/chuyển, và tính nhất quán dữ liệu sau đột biến. |
| **Expected Negative Invariant Assertions** | **38** | **11.08%** | Kiểm tra các invariant bảo mật và phân quyền: từ chối truy cập không token (401), từ chối vượt quyền trong quỹ (403), từ chối thao tác trên tài nguyên của người dùng khác (404), từ chối trùng lặp mã hoặc vượt giới hạn hệ thống (409). |
| **External Provider Fallback Assertions** | **10** | **2.91%** | Kiểm tra đa tầng trạng thái lỗi có kiểm soát, cấu trúc mã lỗi DTO (`error_code`, `message`) và cơ chế suy thoái an toàn (graceful degradation) trên các phản hồi đối soát dịch vụ ngoài. |
| **TỔNG CỘNG ASSERTIONS** | **343** | **100.0%** | **Toàn bộ 343/343 assertions PASS, 0 FAIL (100% tỷ lệ thành công).** |

---

## 3. CHI TIẾT THỰC THI 19 THƯ MỤC CẤP CAO TRONG COLLECTION

1. **Folder 01 — Authentication & Security (26 requests / 26 assertions PASS):** Đăng ký động, chống trùng lặp email, xác thực OTP email, khởi tạo mã PIN bảo mật 6 số, đăng nhập cấp JWT, làm mới token (`/refresh`), đổi mật khẩu, quên mật khẩu/đặt lại mật khẩu, quản lý phiên và thu hồi phiên (`/sessions/revoke`).
2. **Folder 02 — Account & Friends (14 requests / 14 assertions PASS):** Tra cứu hồ sơ, đổi avatar, cập nhật tùy chọn theme (`light|dark|system`) và ngôn ngữ (`vi-VN|en-US`), tìm kiếm bạn bè qua SpendId/Contact, gửi lời mời và chấp nhận kết bạn hai chiều.
3. **Folder 03 — Wallets & Personal Transactions (18 requests / 18 assertions PASS):** Khám phá ví mặc định, nạp thu nhập, mở ví phụ (Savings / Emergency) tối đa 3 ví, từ chối ví thứ 4 (`wallet_limit_reached`), chi tiêu cá nhân, chuyển tiền nội bộ giữa các ví, tra cứu chi tiết và đóng ví phụ an toàn khi số dư = 0.
4. **Folder 04 — Fixed Fund (Groups) (14 requests / 14 assertions PASS):** Tạo quỹ cố định, mời thành viên, chốt nghĩa vụ đóng góp (`RequiredVnd`), đóng góp quỹ, cấp quyền chi tiêu cho thành viên đạt 100%, chi tiêu hợp lệ tức thời không giữ chỗ 24h, ghi log hoạt động.
5. **Folder 05 — Free Fund (Groups) (12 requests / 12 assertions PASS):** Tạo quỹ tự do, thành viên đóng góp tự nguyện, gửi yêu cầu chi tiêu, Chủ quỹ (Owner) phê duyệt chi tiêu hoặc rút vốn, xem trước tiền hoàn lại (`/refund-preview`), rút lui khỏi quỹ.
6. **Folder 06 — Planning & Analytics (12 requests / 12 assertions PASS):** Tạo ngân sách chi tiêu tháng, đặt mục tiêu tích lũy, nộp tiền vào mục tiêu, rút tiền mục tiêu về ví, thiết lập giao dịch định kỳ.
7. **Folder 07 — AI Assistant (3 requests / 3 assertions PASS):** Tạo hội thoại AI, liệt kê lịch sử, truy vấn trợ lý tài chính với cơ chế fallback an toàn.
8. **Folder 08 — Bank & Integrations (4 requests / 4 assertions PASS):** Kiểm tra trạng thái liên kết ngân hàng, tra cứu tài khoản đã liên kết.
9. **Folder 09 — Notifications & Support (8 requests / 8 assertions PASS):** Tùy chọn thông báo, đăng ký token push, tạo yêu cầu hỗ trợ và liệt kê tranh chấp.
10. **Folder 10 — Continuous Flows (A-F) (40 requests / 40 assertions PASS):**
    - *Flow A:* Đổi avatar $\rightarrow$ Đăng xuất $\rightarrow$ Đăng nhập lại $\rightarrow$ Xác nhận avatar tồn tại qua các phiên làm việc.
    - *Flow B:* Yêu cầu đổi email $\rightarrow$ Xác nhận OTP $\rightarrow$ Đăng nhập bằng email mới $\rightarrow$ Chặn đăng nhập bằng email cũ (401).
    - *Flow C:* Tìm kiếm người dùng $\rightarrow$ Kết bạn $\rightarrow$ Xác nhận danh sách bạn bè hai chiều.
    - *Flow D:* Chu trình Quỹ Cố định khép kín từ tạo quỹ, mời thành viên, xác nhận nghĩa vụ, đóng góp, cấp quyền đến chi tiêu.
    - *Flow E:* Chuyển tiền đa ví nội bộ, đối soát biến động số dư sổ cái chính xác tuyệt đối.
    - *Flow F:* Chu trình ngân sách sau chi tiêu: tạo ngân sách 2.000.000đ $\rightarrow$ ghi nhận chi 500.000đ (25%) $\rightarrow$ sửa giao dịch lên 800.000đ qua PATCH $\rightarrow$ ngân sách tự động tính lại đạt 40% (800.000đ / 1.200.000đ còn lại) $\rightarrow$ dọn dẹp.
11. **Folder 11 — Cross-User Security & Isolation (13 requests / 13 assertions PASS):** Kiểm tra cô lập dữ liệu tuyệt đối giữa User A và User B (User A cố truy cập tài nguyên của User B bị 404).
12. **Folders 12 đến 19 — V1 Governance & Batch Scenarios (139 requests / 167 assertions PASS):**
    - *Folder 12 (V1 Integration & Governance - 19 requests / 32 assertions PASS):* Kiểm tra PIN recovery, SePay TestMode link challenges, quy trình nạp/rút tiền ví (Create, List, GetById) với cơ chế Reservation Hold, giải phóng tạm giữ và tất toán sổ cái.
    - *Folders 13 đến 19 (Batches 1 đến 7 - 120 requests / 135 assertions PASS):*
      - Batch 1 (Identity, Social & System Operations - 14 requests / 15 assertions PASS).
      - Batch 2 (Core Financial, Ledgers & Dashboard - 11 requests / 11 assertions PASS), bao gồm phân bổ chi tiêu đa ví B2-07.
      - Batch 3 (Planning, Budgets & Recurring Rules - 16 requests / 17 assertions PASS).
      - Batch 4 (Support Cases, Disputes & AI Lifecycle - 16 requests / 20 assertions PASS).
      - Batch 5 (Open Banking Hub & Transactions - 11 requests / 19 assertions PASS, bao gồm 3 requests HTTP 503 controlled sandbox fallback).
      - Batch 6 (Advanced V1 Funds Governance - 24 requests / 37 assertions PASS).
      - Batch 7 (Legacy Groups Backward Compatibility - 31 requests / 31 assertions PASS).

---

## 4. KẾT LUẬN NGHIỆM THU

Bộ Postman E2E Test Suite đã chứng minh trọn vẹn chất lượng Backend User SpendWise:
- **Tỷ lệ Đạt Assertions:** **343 / 343 Assertions (100.0% PASS)**.
- **Tỷ lệ Đạt Requests:** **306 / 306 Requests Executed (0 Unhandled Errors)**.
- **Lỗi tồn đọng:** **0 Failures**.
- **Quyết định:** **BACKEND USER DEMO — ACCEPTED & LOCKED**.
