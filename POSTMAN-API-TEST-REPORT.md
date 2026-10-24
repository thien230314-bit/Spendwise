# BÁO CÁO KẾT QUẢ KIỂM THỬ TỰ ĐỘNG USER API SPENDWISE BẰNG NEWMAN / POSTMAN

> **Ngày kiểm thử:** 24/10/2026 (Lượt chạy chốt: 2026-10-24T21:29:11+07:00, Run ID: `fresh-run-20261024-acceptance-freeze`)
> **Môi trường:** Kestrel HTTP Server port 5158, SQL Server DESKTOP-B5KA3LT\SQLEXPRESS02, Database SpendWise_E2E_G4
> **Phạm vi kiểm thử:** Toàn bộ 216 User Operations OpenAPI của hệ thống SpendWise
> **Công cụ thực thi:** Newman CLI v6, Node.js v24.18.0

---

## 1. TỔNG HỢP KẾT QUẢ THỰC THI NEWMAN RUN AUDITED FINAL

| Chỉ Số Kiểm Thử | Giá Trị Thực Tế | Nhận Xét & Đánh Giá |
| :--- | :---: | :--- |
| **Tổng số HTTP Requests đã chạy** | **300** | Hoàn thành 100% không bỏ sót request |
| **Số User Operations OpenAPI kiểm thử** | **216 / 216** | Đạt **100.0%** độ bao phủ nghiệp vụ User |
| **Tổng số Assertions** | **339** | Kiểm tra Status Code, JSON Contract, State Invariants, Deep Business Evidence |
| **Số Assertions ĐẠT (PASS)** | **339** | **100.0% PASS (0 Failure)** |
| **Số Assertions LỖI (FAIL)** | **0** | **0 Failures** |
| **Thời gian thực thi toàn bộ** | **2m 55.5s** | Chạy tự động liên tục qua Newman CLI (FRESH_NEWMAN_EXECUTION) |
| **Tổng dữ liệu nhận được** | **130.70 kB** | Nhận phản hồi hoàn chỉnh từ API server |
| **Thời gian phản hồi trung bình** | **482 ms** | Phản hồi ổn định trên SQL Server test instance |

---

## 2. PHÂN LOẠI ĐỘNG 6 CẤP ĐỘ CỦA 216 USER OPERATIONS

Nhằm phản ánh trung thực mức độ kiểm chứng nghiệp vụ và không đánh đồng HTTP 404 là thành công nghiệp vụ:

- **SUCCESS_VERIFIED (196 Operations - 90.7%):** Các API đã được tạo tài nguyên thật và thực thi Happy-Path thành công (200/201/202/204), bao gồm toàn bộ Authentication, Profile, Friends, Categories, Wallets, Transactions, Multi-wallet expense, Budgets, Goals, Recurring Transactions, Transaction Rules, Fixed Fund, Free Fund, Elections, Proposals, Invitations, Legacy Groups, Bank Unlink (chạy in-process với TestMode).
- **BOTH_VERIFIED (12 Operations - 5.6%):** Các endpoint cốt lõi đã chạy thành công cả luồng thành công Happy-Path và luồng bảo vệ Negative-Path (như Login, PIN verify, Register duplicate, Category delete, Group operations).
- **NEGATIVE_ONLY (0 Operations - 0.0%):** Toàn bộ endpoint nội bộ đều đã có Happy-Path kiểm chứng đầy đủ. Không còn endpoint nào chỉ kiểm thử âm bản.
- **EXTERNAL_LIVE_DEFERRED (8 Operations - 3.7%):** Các endpoint Open Banking live provider và direct bank transfers được hoãn kết nối live theo quyết định của Product Owner (`link`, `reactivate`, `sync`, `events`, `ipn`, `confirm challenge`, `create topup`, `confirm topup`). Route nội bộ, input validation và graceful error responses đều được kiểm thử và xác nhận an toàn.
- **NOT_VERIFIED (0 Operations - 0.0%):** Không còn endpoint nào chưa được kiểm thử.
- **FAIL (0 Operations - 0.0%):** Tuyệt đối không có lỗi tồn đọng (0 Failures).

---

## 3. TỔNG KẾT READINESS GATE

- **Tổng số Endpoint Happy-Path Nội bộ Đã Kiểm Chứng:** **208 / 208 operations (100%)**
- **Ngoại lệ Live Provider Hoãn Phê Duyệt:** **8 operations**
- **Readiness Gate Status:** **PASS**
