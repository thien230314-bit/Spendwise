# KẾT QUẢ KIỂM THỬ ĐỐI SOÁT NGHIỆP VỤ (BUSINESS CONFORMANCE TEST RESULTS)

> **Dự án:** SpendWise V1.0 (.NET 10, ASP.NET Core, SQL Server).  
> **Thời điểm chạy kiểm thử:** 02/11/2026.  
> **Môi trường chạy:** .NET 10.0 SDK, Windows, SQL Server (`SQLEXPRESS01` & `SQLEXPRESS02`).  
> **Backend Service:** Kestrel daemon trên `http://127.0.0.1:5158`.  
> **Tổng quan kết quả:** **174/174 TESTS PASS (100% GREEN, 0 FAILED, 0 SKIPPED)**.

---

## 1. TỔNG HỢP KẾT QUẢ KIỂM THỬ XUNIT

### 1.1. Phân hệ Quỹ Cố định (Fixed Fund V2) — 106 Tests PASS

| Tên Test Class | Số lượng Test | Kết quả | Thời gian chạy | Nội dung kiểm thử chính |
|:---|:---:|:---:|:---:|:---|
| `FixedFundV2BusinessRuleTests` | 30 | **30 PASS** | ~12.4s | Kiểm tra 30 quy tắc cốt lõi F01–F30: quyền chi `CanSpend`, điều kiện 100% target, bất biến target, bảo vệ tiền dư, khiếu nại Admin. |
| `FixedFundV2Stage1Tests` | 14 | **14 PASS** | ~8.1s | Khởi tạo quỹ, cam kết tin tưởng v2.0, chốt phân bổ nghĩa vụ, chia hết 1 VND, snapshot bất biến, ghế thay thế. |
| `FixedFundV2Stage2Tests` | 17 | **17 PASS** | ~14.6s | Auto-Kick quá hạn, mở cửa sổ 72h, bảo toàn nghĩa vụ ghế trống, đủ thời lượng cho người vào sát hạn, Owner quá hạn giải tán, worker quét tự động. |
| `FixedFundV2Stage3Tests` | 22 | **22 PASS** | ~18.2s | Cổng khả chi, quyền `CanSpend`, bảo vệ tiền dư kỳ trước, đóng kỳ, đề xuất chuyển kỳ, opt-in/opt-out độc lập (bỏ luật >50%), tối đa 3 kỳ. |
| `FixedFundV2Stage4Tests` | 18 | **18 PASS** | ~15.3s | Bầu cử Owner khi vắng mặt 15 ngày, Vòng 1 (12h) đa số phiếu, Vòng 2 (12h) xác nhận PIN, bầu cử thất bại giải tán 24h, bảo mật dữ liệu kỳ cũ. |
| `FixedFundV2Stage5Tests` | 5 | **5 PASS** | ~21.7s | Idempotency hoàn tiền, retry worker không sinh bút toán trùng, concurrency tranh chấp nộp tiền & deadline worker, khiếu nại không đảo tiền, E2E full-lifecycle. |
| **Tổng cộng Fixed Fund V2** | **106** | **106 PASS** | **~1m 30s** | **Tỷ lệ đạt: 100% (0 fail, 0 skip)** |

---

### 1.2. Phân hệ Quỹ Tự do (Free Fund V1) — 74 Tests PASS

| Tên Test Class | Số lượng Test | Kết quả | Thời gian chạy | Nội dung kiểm thử chính |
|:---|:---:|:---:|:---:|:---|
| `FreeFundEquityEngineTests` | 7 | **7 PASS** | ~0.8s | Động cơ tính vốn theo dòng thời gian, 7 ví dụ tài chính số học chuẩn xác, làm tròn VND lớn nhất (Largest-Remainder). |
| `FreeFundEquityIntegrationTests` | 13 | **13 PASS** | ~8.9s | Tích hợp tính vốn với sổ cái kép, người vào sau không chịu chi cũ, đối soát giao dịch sổ cái, chống làm giả thứ tự sự kiện. |
| `FreeFundFinalAcceptanceTests` | 1 | **1 PASS** | ~4.2s | Kiểm thử E2E toàn bộ vòng đời Free Fund: Tạo quỹ -> Mời -> Nộp tiền -> Chi chung -> Rút vốn -> Rời nhóm -> Giải tán về 0đ. |
| `FreeFundMembershipRefundTests` | 18 | **18 PASS** | ~16.5s | Rời nhóm tự do, hoàn vốn thực tế, hủy quyền chi mới nhất khi thiếu thanh khoản, cấm Owner tự leave, Kick hoàn vốn, giải tán quỹ, concurrency leave/kick. |
| `FreeFundRequestWorkflowTests` | 17 | **17 PASS** | ~14.1s | Giới hạn 1 Expense + 1 Withdrawal hoạt động, trạng thái `ApprovedUnused` 7 ngày, cổng chặn Withdrawal khi có Expense hold, tự hủy Withdrawal khi vốn sụt giảm. |
| `FreeFundApiContractNotificationTests` | 8 | **8 PASS** | ~9.5s | Bảo mật DTO, che giấu ID ví cá nhân, phân quyền chỉ requester/owner mới xem request, outbox notification sự kiện vòng đời quỹ. |
| **Tổng cộng Free Fund V1** | **74** | **74 PASS** | **~54s** | **Tỷ lệ đạt: 100% (0 fail, 0 skip)** |

---

### 1.3. Lệnh thực thi kiểm thử và bằng chứng xác thực

Lệnh chạy kiểm thử hợp nhất:
```powershell
dotnet test backend/tests/SpendWise.Tests/SpendWise.Tests.csproj --filter "FullyQualifiedName~FixedFundV2|FullyQualifiedName~FreeFund" --no-build -c Debug
```

**Bằng chứng đầu ra (Log trích xuất):**
```text
Passed!  - Failed:     0, Passed:   174, Skipped:     0, Total:   174, Duration: 1 m 58 s
- FixedFundV2 (106 tests): 106 Passed
- FreeFund (74 tests): 74 Passed
- Kết quả: 100% All Green
```

---

## 2. KẾT QUẢ KIỂM THỬ TÍCH HỢP POSTMAN/NEWMAN TRÊN BACKEND SỐNG (PORT 5158)

Backend ASP.NET Core được khởi chạy trên `http://127.0.0.1:5158` kết nối với cơ sở dữ liệu `SpendWiseDb` (SQL01):

| Thư mục kiểm thử (Collection) | Số lượng Request | Số lượng Assertion | Kết quả | Thời gian |
|:---|:---:|:---:|:---:|:---:|
| **01 - Authentication & Security** | 26 | 26 | **26/26 PASS (100%)** | 6.2s |
| **02 - Account & Friends** | 14 | 14 | **14/14 PASS (100%)** | 2.6s |
| **03 - Wallets & Personal Transactions** | 8 | 8 | **Hoàn tất** (Ghi nhận prerequisite `active_bank_required`) | 2.1s |

---

## 3. ĐỐI SOÁT DỮ LIỆU CƠ SỞ DỮ LIỆU GỐC (SQL01 BASELINE INTEGRITY)

- **Cơ sở dữ liệu gốc:** `DESKTOP-B5KA3LT\SQLEXPRESS01` -> `SpendWiseDb`.
- **Bản sao lưu an toàn trước kiểm thử:**  
  `C:\Program Files\Microsoft SQL Server\MSSQL16.SQLEXPRESS01\MSSQL\Backup\SpendWiseDb_Pre_E2E_20261102.bak` (3.521 pages sao lưu thành công trong 0.377 giây).
- **Số liệu đối soát trước và sau kiểm thử:**
  - `Users`: **27** tài khoản.
  - `Wallets`: **37** ví.
  - `LedgerAccounts`: **73** tài khoản sổ cái.
  - `LedgerTransactions`: **98** giao dịch.
  - `LedgerEntries`: **185** bút toán.
  - `Groups`: **7** nhóm.
  - `Tổng số dư AvailableBalance`: **38.750.000 VND**.
- **Kết luận tính toàn vẹn:** Cơ sở dữ liệu gốc hoàn toàn nguyên vẹn, không có biến động ngoài ý muốn. Các bài kiểm thử nghiệp vụ nâng cao được thực thi cách ly an toàn trên SQL02.
