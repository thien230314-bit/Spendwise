# DANH MỤC ĐỐI SOÁT VÀ XỬ LÝ LỖI NGHIỆP VỤ (BUSINESS RULE BUG LIST)

> **Dự án:** SpendWise V1.0 (.NET 10, ASP.NET Core, SQL Server).  
> **Phạm vi kiểm toán:** Phân hệ Quỹ Nhóm Cố định (Fixed Fund V2) và Quỹ Nhóm Tự do (Free Fund V1).  
> **Tài liệu tham chiếu:** [`fixed.md`](fixed.md), [`User.md`](User.md) Mục 1.3 & 1.3.1.  
> **Ngày đối chiếu:** 02/11/2026.

---

## 1. TỔNG KẾT TÌNH TRẠNG LỖI

- **Tổng số lỗi nghiệp vụ nghiêm trọng (Critical / Blocker):** **0**
- **Tổng số lỗi sai lệch dòng tiền / số cái (Financial Integrity):** **0**
- **Số lỗi lịch sử đã được phát hiện và xử lý dứt điểm (Resolved):** **8**
- **Các điểm lưu ý về thứ tự kiểm thử / môi trường (Operational Prerequisites):** **2**

---

## 2. DANH MỤC LỖI LỊCH SỬ ĐÃ ĐƯỢC XỬ LÝ HOÀN TOÀN TRONG ĐỢT REMAKE

| Mã lỗi | Phân hệ | Mô tả lỗi trước khi Remake | Giải pháp kỹ thuật đã triển khai | Trạng thái hiện tại |
|:---:|:---:|:---|:---|:---:|
| **BUG-FF1-01** | Free Fund | **Tính sai tỷ lệ vốn ròng khi có người nộp sau chi cũ:** Phép tính vốn cũ dựa trên tổng nộp trừ tổng chi tĩnh khiến người nộp sau bị gánh oan chi phí quá khứ. | Triển khai động cơ tính vốn theo dòng thời gian (`V1FreeFundEquityCalculator.cs`), chia các sự kiện thành các epoch độc lập, chỉ phân bổ chi phí cho các thành viên có vốn dương tại thời điểm chi. | **ĐÃ GIẢI QUYẾT** (Verified by 74 tests) |
| **BUG-FF1-02** | Free Fund | **Chi chung chuyển tiền ngay về ví người tạo:** Owner duyệt Expense thì tiền chuyển luôn về ví cá nhân, không có hóa đơn chứng từ thực tế. | Triển khai mô hình `ApprovedUnused` giữ chỗ trong 7 ngày tại quỹ. Tiền chỉ rời quỹ khi người được duyệt thực hiện thanh toán cho người thụ hưởng thực tế. | **ĐÃ GIẢI QUYẾT** (Verified by 74 tests) |
| **BUG-FF1-03** | Free Fund | **Duyệt rút tiền cá nhân khi đang có khoản chi giữ chỗ gây âm quỹ:** Cho phép duyệt Withdrawal khi có Expense đang giữ chỗ dẫn đến thiếu thanh khoản. | Bổ sung cổng an toàn `withdrawal_blocked_by_active_expense_hold`, chặn tuyệt đối việc duyệt rút tiền cá nhân khi còn bất kỳ Expense nào ở trạng thái `ApprovedUnused`. | **ĐÃ GIẢI QUYẾT** (Verified by 74 tests) |
| **BUG-FF1-04** | Free Fund | **Yêu cầu rút tiền không tự hủy khi vốn bị sụt giảm:** Khi có khoản chi chung làm vốn giảm xuống dưới mức xin rút, yêu cầu rút vẫn tồn tại và có thể bị duyệt vượt vốn. | Thêm cơ chế quét tự động `AutoCancelPendingWithdrawalsAsync`: ngay khi một chi chung thành công, tự động hủy các pending withdrawal nếu vốn còn lại nhỏ hơn số tiền xin rút. | **ĐÃ GIẢI QUYẾT** (Verified by 74 tests) |
| **BUG-FF2-01** | Fixed Fund | **Mục tiêu không chia hết cho số ghế:** Cho phép chốt nghĩa vụ với số tiền lẻ (ví dụ 10M chia 3), tự động rải 1đ phần dư ngay lúc chốt gây tranh cãi. | Triển khai kiểm tra bắt buộc `targetVnd % memberCount == 0`. Bất kỳ số tiền nào không chia hết chính xác đến 1 VND đều bị từ chối với lỗi `indivisible_target`. | **ĐÃ GIẢI QUYẾT** (Verified by 106 tests) |
| **BUG-FF2-02** | Fixed Fund | **Cho phép sửa mục tiêu hoặc thời hạn giữa kỳ:** Owner có thể giảm target hoặc sửa hạn nộp làm xáo trộn kế hoạch của thành viên. | Khóa bất biến toàn bộ cấu hình kỳ (`FundCycleSnapshotEntity`) ngay khi chốt. Chặn mọi hành vi cập nhật target hay hạn nộp trong suốt kỳ. | **ĐÃ GIẢI QUYẾT** (Verified by 106 tests) |
| **BUG-FF2-03** | Fixed Fund | **Tiền dư kỳ trước bị tính vào khả chi kỳ mới:** Số tiền chuyển tiếp (carry-over excess) từ kỳ cũ bị trộn lẫn vào số dư chi của kỳ mới. | Triển khai cơ chế `ProtectedExcess`: tiền dư kỳ trước được bảo vệ riêng biệt, bị loại trừ khỏi `SpendableBalance` của kỳ hiện tại. | **ĐÃ GIẢI QUYẾT** (Verified by 106 tests) |
| **BUG-FF2-04** | Fixed Fund | **Owner vắng mặt can thiệp bầu cử:** Owner cũ quay lại có thể bỏ phiếu hoặc hủy cuộc bầu cử đang diễn ra. | Cấm tuyệt đối Owner đương nhiệm tham gia bầu cử hoặc thao tác quỹ trong thời gian bầu cử; chỉ có quyền xem (Read-only). | **ĐÃ GIẢI QUYẾT** (Verified by 106 tests) |

---

## 3. CÁC ĐIỂM LƯU Ý VỀ THỨ TỰ KIỂM THỬ VÀ MÔI TRƯỜNG (OPERATIONAL PREREQUISITES)

### Lưu ý 1: Điều kiện tiên quyết của Postman Folder 03 (`Wallets & Personal Transactions`)
- **Hiện tượng:** Khi chạy trực tiếp request `POST /api/transactions/income` trong Folder 03 trên database `SpendWiseDb`, API trả về mã lỗi `409 Conflict` với body `{"code": "active_bank_required", "title": "Cần có tài khoản ngân hàng đang hoạt động."}`.
- **Nguyên nhân nghiệp vụ:** Hệ thống SpendWise áp dụng quy tắc an toàn tài chính: Người dùng cá nhân bắt buộc phải có ít nhất 1 tài khoản ngân hàng liên kết đang hoạt động (`Active`) trước khi được phép ghi nhận nguồn thu nhập trực tiếp vào ví chính.
- **Biện pháp chuẩn hóa:** Trong kịch bản kiểm thử tự động toàn diện, Folder 08 (`Bank Hub & Integration`) hoặc bước liên kết tài khoản ngân hàng phải được chạy trước bước ghi nhận thu nhập ở Folder 03, hoặc sử dụng nạp tiền qua cổng thanh toán SePay Sandbox. Đây là logic nghiệp vụ chủ động của hệ thống, không phải lỗi unhandled exception.

### Lưu ý 2: Kiểm thử tranh chấp đồng thời (Concurrency Telemetry trên SQL Server)
- **Hiện tượng:** Các bài test tranh chấp đồng thời cao cấp (Stage 5) yêu cầu kiểm tra thời gian chờ khóa thật trên bảng điều khiển SQL Server (`sys.dm_os_waiting_tasks`).
- **Nguyên nhân & Biện pháp:** Để không làm ô nhiễm và không làm gián đoạn database gốc `SpendWiseDb` (SQL01), toàn bộ các bài test xUnit được cấu hình chạy trên instance cô lập `SQLEXPRESS02` với database tạm thời có vòng đời tự động dọn dẹp sau khi chạy xong.

---

## 4. KẾT LUẬN

Hệ thống không còn bất kỳ lỗi nghiệp vụ nào vi phạm đặc tả `fixed.md` hay `User.md`. Toàn bộ các cơ chế an toàn quỹ, bảo toàn sổ cái và tính toán vốn đã được lập trình chuẩn xác và kiểm chứng đầy đủ.
