# SPENDWISE V1.0 — PRODUCT DECISIONS REQUIRED (FINANCIAL & GOVERNANCE BLOCKERS)

Tài liệu này tổng hợp toàn bộ các điểm nghẽn nghiệp vụ thuộc **Mục 12 — `User.md` v1.3** chưa được Product Owner phê duyệt quyết định cuối cùng.
Theo nguyên tắc an toàn tài chính (`AGENTS.md` và `User.md`), Backend **không tự ý lấp chỗ trống, không tự chọn công thức tính tiền giả lập**, và bảo lưu trạng thái `FINANCE_BLOCKED` / `POLICY_BLOCKED` cho các thao tác giải ngân/hoàn tiền phức tạp cho đến khi Product Owner có quyết định chính thức bằng văn bản.

---

## 1. TRẠNG THÁI TRIỂN KHAI HOÀN TIỀN & BLOCKER CÒN LẠI (CẬP NHẬT TASK 12)

### 1.1. Các nhánh nghiệp vụ đã được Product Owner phê duyệt & ĐÃ TRIỂN KHAI HOÀN TẤT (Task 12)
Theo chỉ thị chính thức của Product Owner trong Task 12 Correction, các quy tắc hoàn tiền sau đã được hiện thực hóa đầy đủ trong production code (`V1FundRefunds.cs`):
- **Trường hợp A — Chưa phát sinh chi tiêu (Unspent):** Thành viên rời hoặc bị kick được hoàn 100% số tiền đã góp hợp lệ chưa hoàn, bao gồm cả quyền lợi chuyển tiếp, không hoàn trùng.
- **Trường hợp B — Nhóm Cố định (Fixed) đã phát sinh chi tiêu:** Tính toán minh bạch dựa trên số dư còn lại sau chi tiêu. Tiền dư khóa cá nhân (`LockedExcessVnd`) được hoàn nguyên trạng trước, phần số dư khả dụng được phân bổ theo tỷ lệ nghĩa vụ hợp lệ của từng thành viên.
- **Trường hợp C — Giải tán nhóm Tự do (Free) chưa có rút cá nhân:** Toàn bộ số dư quỹ được phân bổ hoàn trả tỷ lệ theo tỷ trọng đóng góp của các thành viên đang hoạt động.
- **Trường hợp D — Chuẩn mực hạch toán:** Toàn bộ tính toán được thực thi tập trung tại Backend; hoàn tiền về ví cá nhân hợp lệ qua giao dịch loại `Refund`; giải tán nhóm đưa số dư tài khoản quỹ về đúng 0 VND; giao dịch nguyên tử (atomic) và chống trùng lặp (idempotent); chủ nhóm phải chuyển quyền trước khi rời nhóm.

### 1.2. Nhánh duy nhất còn bảo lưu bảo vệ (`REFUND_BLOCKED_COMPLEX_CONTRACT`)
- **Tình huống Nhóm Free đã có rút cá nhân trước đó và yêu cầu giải tán:**
  - Khi một hoặc nhiều thành viên đã rút cá nhân một phần quỹ về ví riêng (`Personal Withdrawal`), sau đó nhóm chi tiêu chung rồi yêu cầu giải tán.
  - Hiện tại hệ thống tự động chặn và trả lỗi nghiệp vụ có mã `REFUND_BLOCKED_COMPLEX_CONTRACT` kèm thông điệp hướng dẫn rõ ràng, bảo đảm tuyệt đối không gây sai lệch số dư sổ cái kép (`Sum Debit = Sum Credit`).
  - Hướng xử lý: Admin hỗ trợ quyết toán thủ công qua Case Hỗ trợ (ADM-03).

---

## 2. BLOCKER P0-02: Cơ chế chuyển tiền trực tiếp ra ngoài nhóm / Giải ngân thực tế (`Payout Execution`)

### 2.1. Quy tắc còn thiếu
- Quyền chi `CanSpend = ON` của nhóm Fixed cho phép thành viên thực hiện khoản chi trực tiếp mà không cần chủ nhóm duyệt từng khoản.
- Tuy nhiên, điểm đến của dòng tiền giải ngân (`Payout Destination`):
  - Tiền từ Quỹ nhóm chuyển về đâu? (Ví SpendWise của người chi / Ví SpendWise của người bán / Tài khoản ngân hàng bên ngoài qua VietQR/SePay?)
  - Hiện tại hệ thống chỉ hỗ trợ chuyển tiền nội bộ giữa các tài khoản User SpendWise. Chưa có cổng chi hộ ngân hàng đối tác (`Outward Bank Transfer API`).

### 2.2. Tình huống số học minh họa
- Thành viên B có `CanSpend = ON` mua vé xe 400.000đ từ quỹ 1.000.000đ:
  - Nếu chuyển về Ví cá nhân của B (để B đi trả tiền mặt bên ngoài): Đây thực chất là hành vi "Rút quỹ về ví cá nhân".
  - Nếu chuyển trực tiếp cho nhà xe: Nhà xe không có tài khoản SpendWise!

### 2.3. Tác động tới Ledger
- Nếu hạch toán chi tiêu: Nợ tài khoản Quỹ, Có tài khoản Trung gian Chi tiêu (`Clearing Account`).
- Nhưng nếu tiền chưa thực sự rời khỏi hệ thống ngân hàng hoặc ví, tiền bị treo ở `Clearing Account`.

### 2.4. Quyết định cần Product Owner xác nhận
- Xác nhận V1.0 chỉ cho phép chi tiêu trực tiếp sang tài khoản User SpendWise khác (như P2P) hoặc chuyển hoàn về ví cá nhân của thành viên chi tiêu kèm chứng từ hóa đơn, KHÔNG hỗ trợ chuyển tiền ra số tài khoản ngân hàng ngoài.

---

## 3. BLOCKER P0-03: Tham số định lượng và quy trình Bầu lại chủ nhóm (`Governance Election`)

### 3.1. Quy tắc còn thiếu
- Thời gian mở từng vòng bầu cử: Đề cử (Vòng 1), Bầu phụ khi hòa phiếu, Xác nhận (Vòng 2).
- Xử lý khi không có ứng viên nào chấp nhận nhận quyền chủ nhóm.
- Xử lý các giao dịch nộp/rút tiền đang chờ duyệt trong thời gian diễn ra bầu cử.

### 3.2. Đề xuất tham số mặc định từ V1.2
- Vòng 1 (Đề cử/Bầu ứng viên): **48 giờ**.
- Vòng bầu phụ (nếu hòa phiếu cao nhất): **24 giờ**.
- Vòng 2 (Xác nhận trên 50% cử tri hợp lệ): **24 giờ**.
- Trong thời gian bầu cử: Đóng băng tạm thời quyền thay đổi mục tiêu quỹ và quyền bật/tắt `CanSpend`. Các khoản nộp tiền vẫn tiếp tục ghi nhận.
- Nếu bầu cử thất bại: Thành viên có quyền tự do rời nhóm theo quy trình hoàn tiền, không cần chủ nhóm duyệt.

### 3.3. Quyết định cần Product Owner xác nhận
- Phê duyệt bộ tham số 48h / 24h / 24h và cơ chế đóng băng quản trị trong lúc bầu cử.

---

## 4. TỔNG KẾT TRẠNG THÁI HIỆN TẠI TRONG BACKEND

1. **Nhóm Fixed:** Tạo nhóm, chia nghĩa vụ đồng dư ghế cố định, nộp tiền từ ví cá nhân, xem tiến độ, lịch sử hoạt động, bật/tắt `CanSpend` bằng PIN: **HOẠT ĐỘNG THẬT 100% (READY)**.
2. **Chi tiêu trực tiếp giải ngân ra ngoài:** Giữ **`FINANCE_BLOCKED`** / `ApprovedAwaitingFinanceContract` cho đến khi Product Owner chốt các quyết định trên.
3. **Nhóm Free:** Tạo nhóm, mời thành viên, nộp tiền, xem lịch sử, tạo yêu cầu rút, chủ nhóm duyệt: **HOẠT ĐỘNG THẬT 100% (READY)**.
