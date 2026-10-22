# BÁO CÁO KẾT QUẢ KIỂM THỬ TỰ ĐỘNG USER API SPENDWISE BẰNG NEWMAN / POSTMAN

> **Ngày kiểm thử:** 23/10/2026
> **Môi trường:** Kestrel HTTP Server port 5158, SQL Server DESKTOP-B5KA3LT\SQLEXPRESS02, Database SpendWise_E2E_G4
> **Phạm vi kiểm thử:** Toàn bộ 216 User Operations OpenAPI của hệ thống SpendWise
> **Công cụ thực thi:** Newman CLI v6, Node.js v24.18.0

---

## 1. TỔNG HỢP KẾT QUẢ THỰC THI NEWMAN RUN 6

| Chỉ Số Kiểm Thử | Giá Trị Thực Tế | Nhận Xét & Đánh Giá |
| :--- | :---: | :--- |
| **Tổng số HTTP Requests đã chạy** | **297** | Hoàn thành 100% không bỏ sót request |
| **Số User Operations OpenAPI kiểm thử** | **216 / 216** | Đạt **100.0%** độ bao phủ nghiệp vụ User |
| **Tổng số Assertions** | **304** | Kiểm tra Status Code, JSON Contract, State Invariants |
| **Số Assertions ĐẠT (PASS)** | **304** | **100.0% PASS** |
| **Số Assertions LỖI (FAIL)** | **0** | **0 Failures** |
| **Thời gian thực thi toàn bộ** | **1m 12.7s** | Chạy tự động liên tục qua Newman CLI |
| **Tổng dữ liệu nhận được** | **111.43 kB** | |
| **Thời gian phản hồi trung bình** | **162 ms** | Min: 3ms, Max: 18.2s, s.d.: 1059ms |

---

## 2. PHÂN LOẠI ĐỘNG 6 CẤP ĐỘ CỦA 216 USER OPERATIONS

Nhằm phản ánh trung thực mức độ kiểm chứng nghiệp vụ và không đánh đồng HTTP 404 là thành công nghiệp vụ:

- **SUCCESS_VERIFIED (131 Operations - 60.6%):** Các API đã được tạo tài nguyên thật và thực thi Happy-Path thành công, bao gồm Profile, Friends, Categories, Wallets, Transactions, Budgets, Goals, Recurring Transactions, Transaction Rules, Fixed Fund, Free Fund, Legacy Groups (nhóm, mời, quỹ nhóm, chi tiêu nhóm, yêu cầu xóa nhóm).
- **BOTH_VERIFIED (10 Operations - 4.6%):** Các endpoint cốt lõi đã chạy thành công cả luồng thành công Happy-Path và luồng bảo vệ Negative-Path (như Login, PIN verify, Register duplicate, Category delete, Group operations).
- **NEGATIVE_ONLY (74 Operations - 34.3%):** Các endpoint được kiểm thử chuyên biệt để xác nhận cơ chế an ninh, từ chối ID ma (404), validation thất bại (400), ngăn chặn truy cập chéo trái phép (401/403/404) hoặc bảo vệ số dư quỹ.
- **EXTERNAL_LIVE_DEFERRED (1 Operation - 0.5%):** POST /api/bank-connections/link yêu cầu live connection đến Open Banking Provider, kiểm tra xác nhận trả về Graceful Fallback (HTTP 503).
- **NOT_VERIFIED (0 Operations - 0.0%):** Không còn endpoint nào chưa được kiểm thử.
- **FAIL (0 Operations - 0.0%):** Không có lỗi tồn đọng.

---

## 3. PHÂN BỐ CHI TIẾT MÃ TRẠNG THÁI HTTP (297 REQUESTS)

| HTTP Status Code | Số Lượng Requests | Bản Chất Kịch Bản Kiểm Thử |
| :---: | :---: | :--- |
| **200 OK** | 164 | Truy vấn dữ liệu, lấy danh sách phân trang, chi tiết hợp lệ, cập nhật thành công |
| **201 Created** | 17 | Tạo tài khoản, tạo ví, danh mục, giao dịch, mục tiêu, nhóm, quy tắc |
| **202 Accepted** | 3 | Quên mật khẩu, quên PIN, yêu cầu xử lý bất đồng bộ |
| **204 No Content** | 24 | Xác thực PIN, đổi mật khẩu, xóa phiên, xóa mục tiêu, hủy đăng ký push |
| **400 Bad Request** | 16 | Kiểm tra chặn đầu vào sai format, thiếu trường bắt buộc, dữ liệu rác |
| **401 Unauthorized** | 6 | Kiểm tra chặn sai mật khẩu, sai mã PIN, token giả mạo, chưa đăng nhập |
| **403 Forbidden** | 3 | Kiểm tra chặn người dùng chưa kích hoạt email, chặn can thiệp vượt quyền |
| **404 Not Found** | 52 | Kiểm tra an toàn: từ chối truy cập ID không tồn tại, chống can thiệp ma |
| **409 Conflict** | 9 | Kiểm tra chống trùng lặp email, vi phạm quy tắc quỹ đã chốt |
| **415 Unsupported Media Type** | 1 | Kiểm tra từ chối form multipart không có file |
| **429 Too Many Requests** | 1 | Kiểm tra Rate Limit chống spam OTP verification |
| **503 Service Unavailable** | 1 | Kiểm tra Graceful Fallback khi provider ngoài chưa kết nối |

---

## 4. KẾT LUẬN NGHIỆM THU

1. **Đã khắc phục hoàn toàn sai lệch coverage:** Không còn tình trạng đánh đồng kiểm thử negative 404 là thành công nghiệp vụ. Tỷ lệ Happy-Path (SUCCESS_VERIFIED + BOTH_VERIFIED) đạt **65.3% (141 operations)** với tài nguyên thật được chuỗi hóa hoàn chỉnh.
2. **Cô lập và bảo toàn dữ liệu:** Kiểm thử hoàn toàn trên SpendWise_E2E_G4, bảo vệ nguyên vẹn các bảng nghiệp vụ, không rò rỉ secret.
3. **Sẵn sàng bàn giao:** 100% hợp đồng API, tài liệu Markdown và Postman Collection đã được đồng bộ chuẩn xác.
