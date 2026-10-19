# FEATURE ID MIGRATION & DECOMPOSITION LEDGER (SỔ TAY DI CHUYỂN & PHÂN TÁCH FEATURE ID)

Tài liệu này ghi lại toàn bộ lịch sử phân tách (Decomposition) và ánh xạ (Mapping) của các Feature ID trong hệ thống UI Blueprint SpendWise Phase 1 Repair.

Mục đích:
1. Đảm bảo tính nguyên tử (Atomicity): Mỗi Feature ID đại diện cho một tương tác hoặc luồng nghiệp vụ độc lập duy nhất.
2. Không gộp nhiều hành vi khác nhau vào một mã chung (tránh gộp 4 nút hoặc 4 thao tác vòng đời vào 1 ID).
3. Cho phép các công cụ bên ngoài (Google Stitch, Figma, Codex) và kiểm thử tự động tham chiếu chính xác từng phần tử UI.

---

## 1. BẢNG TỔNG HỢP ÁNH XẠ (MIGRATION MAPPING TABLE)

| Mã ID Cũ (Old ID) | Mã ID Mới (New Sub-ID) | Tên Tính năng (Feature Name) | Lý do Phân tách (Decomposition Rationale) | UI Element / Trigger |
|---|---|---|---|---|
| `DASH-004` | `DASH-004A` | Nút Thao tác nhanh: Ghi nhận Thu nhập | Phân tách nút "+ Thu" riêng biệt khỏi 3 nút còn lại | Quick Action Button (Green), mở Income Modal |
| `DASH-004` | `DASH-004B` | Nút Thao tác nhanh: Ghi nhận Chi tiêu | Phân tách nút "- Chi" riêng biệt để quản lý luồng chi phí | Quick Action Button (Red), mở Expense Modal |
| `DASH-004` | `DASH-004C` | Nút Thao tác nhanh: Chuyển tiền | Phân tách nút điều hướng chuyển khoản cá nhân | Quick Action Button (Blue), điều hướng `/transfer` |
| `DASH-004` | `DASH-004D` | Nút Thao tác nhanh: Quỹ nhóm | Phân tách nút điều hướng quản trị quỹ nhóm | Quick Action Button (Purple), điều hướng `/groups` |
| `WALLET-005` | `WALLET-005A` | Thao tác tạo ví nội bộ mới | Hành vi tạo ví mới qua modal với số dư khởi tạo | Nút "+ Tạo ví", mở Create Wallet Modal |
| `WALLET-005` | `WALLET-005B` | Thao tác đổi tên và chỉnh sửa ví | Hành vi cập nhật thông tin ví hiện có | Icon Chỉnh sửa trên thẻ ví, mở Edit Modal |
| `WALLET-005` | `WALLET-005C` | Thao tác đặt ví làm mặc định | Hành vi thay đổi trạng thái mặc định của nguồn tiền | Nút "Đặt làm mặc định" trên thẻ ví |
| `WALLET-005` | `WALLET-005D` | Thao tác đóng/xóa ví nội bộ | Hành vi hủy ví nhạy cảm: Yêu cầu số dư = 0 & PIN 6 số | Nút "Đóng ví", mở xác thực PIN bảo mật |
| `WALLET-007` | `WALLET-007A` | Đồng bộ kết nối ngân hàng tức thời | Tách riêng hành vi gọi API sync để đối chiếu độc lập | Nút "Đồng bộ ngay" (RefreshCw icon) |
| `WALLET-007` | `WALLET-007B` | Hủy liên kết tài khoản ngân hàng | Tách riêng hành vi hủy kết nối kèm modal xác nhận | Nút "Hủy liên kết" (Unlink icon) |
| `WALLET-007` | `WALLET-007C` | Tái kích hoạt / Liên kết lại ngân hàng | Tách riêng hành vi kết nối lại tài khoản Unlinked | Nút "Liên kết lại" (ExternalLink icon) |
| `GOAL-006` | `GOAL-006A` | Chỉnh sửa mục tiêu tích lũy | Tách riêng hành vi sửa metadata qua Edit Modal | Icon Sửa trên thẻ mục tiêu |
| `GOAL-006` | `GOAL-006B` | Xóa mục tiêu tích lũy (Yêu cầu PIN) | Tách riêng hành vi xóa với ràng buộc số dư = 0 & PIN | Icon Thùng rác trên thẻ mục tiêu |
| `BUDGET-005` | `BUDGET-005A` | Chỉnh sửa hạn mức ngân sách | Tách riêng hành vi điều chỉnh hạn mức và rollover | Icon Sửa trên thẻ ngân sách |
| `BUDGET-005` | `BUDGET-005B` | Xóa ngân sách danh mục (Yêu cầu PIN) | Tách riêng hành vi xóa ngân sách tháng với PIN | Icon Thùng rác trên thẻ ngân sách |
| `TXN-001` | `TXN-001A` | Chuyển Tab Lịch sử giao dịch | Tách riêng điều hướng xem bảng lịch sử và bộ lọc | Tab Button "Lịch sử" (`?tab=history`) |
| `TXN-001` | `TXN-001B` | Chuyển Tab Giao dịch định kỳ | Tách riêng điều hướng sang quản lý giao dịch lặp lại | Tab Button "Định kỳ" (`?tab=recurring`) |
| `TXN-001` | `TXN-001C` | Chuyển Tab Quy tắc phân loại | Tách riêng điều hướng sang quản lý quy tắc gán nhãn | Tab Button "Quy tắc" (`?tab=rules`) |

---

## 2. QUY TẮC TƯƠNG THÍCH NGƯỢC (BACKWARD COMPATIBILITY)

- Khi tham chiếu cấp độ nhóm (Section-level / Feature Grouping), các hệ thống bên ngoài có thể truy vấn `parentFeatureId: "DASH-004"` hoặc `parentFeatureId: "WALLET-005"`.
- Trong `feature-manifest.json`, mỗi sub-feature được định nghĩa là một item hoàn chỉnh độc lập có trường `parentFeatureId` trỏ về mã gốc.
- Các bài kiểm thử và validator kiểm tra sự hiện diện của toàn bộ sub-ID để đảm bảo độ bao phủ 100%.
