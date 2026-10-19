# EXTERNAL DESIGN HANDOFF — GOOGLE STITCH, FIGMA & CODEX
**Hướng dẫn Bàn giao Thiết kế & Khung Đối chiếu Feature Parity dành cho Công cụ Thiết kế Bên ngoài**  
**Hệ thống:** SpendWise Web Application  
**Phiên bản:** Functional Design Contract v2.0 (12/10/2026)  
**Nguyên tắc cốt lõi:** *"Redesign the presentation, NOT the product"*

---

## 1. MỤC TIÊU VÀ BỐI CẢNH DỰ ÁN
SpendWise là nền tảng quản lý tài chính cá nhân và tài chính xã hội thông minh (Smart Personal & Social Finance).  
Các công cụ thiết kế bên ngoài (Google Stitch, Figma Designer, Codex, UI/UX Agents) được trao **toàn quyền tái cấu trúc diện mạo giao diện** để đạt mức độ chuyên nghiệp, hiện đại, tối giản và cao cấp nhất (Clean Financial UI), **NHƯNG BẮT BUỘC BẢO TOÀN 100% CÁC CHỨC NĂNG NGHIỆP VỤ ĐÃ ĐƯỢC ĐỊNH DANH BẰNG FEATURE ID**.

---

## 2. QUY TẮC BẮT BUỘC DÀNH CHO CÔNG CỤ THIẾT KẾ

### ĐƯỢC PHÉP (ALLOWED — TỰ DO THAY ĐỔI MẠNH):
1. **Tái cấu trúc bố cục (Composition):** Thay đổi vị trí sắp xếp giữa các section, thay đổi số cột lưới (Grid / Flexbox), chuyển từ card stack sang split-view hoặc dynamic canvas.
2. **Cải thiện phân cấp thị giác (Information Hierarchy):** Nhấn mạnh vào số dư khả dụng, giảm bớt sự lộn xộn của các card con, gom nhóm bộ lọc tinh tế, làm nổi bật KPI chính.
3. **Typography & Spacing:** Cân chỉnh lại kích thước chữ, sử dụng font chữ sans-serif hiện đại kết hợp font số đơn cách (tabular-nums font-mono) cho số tiền, tăng khoảng cách đệm (padding, margin) cho thoáng đãng, dễ đọc.
4. **Hiện đại hóa biểu đồ:** Thay đổi hình thức trực quan của biểu đồ (Bar Chart, Donut, Sparklines, Area chart) sao cho trực quan, tinh gọn và bắt mắt hơn.
5. **Tối ưu Responsive:** Bố trí lại các thành phần trên di động (Bottom Navigation, Drawer), máy tính bảng và màn hình siêu rộng (Ultra-wide).
6. **Hệ thống màu sắc:** Giữ tông màu xanh ngọc lục bảo Emerald làm chủ đạo, tăng cường độ tương phản cho Light/Dark mode, tuân thủ tiêu chuẩn tương phản WCAG AA.

### TUYỆT ĐỐI CẤM (STRICTLY FORBIDDEN):
1. **XÓA BẤT KỲ FEATURE ID NÀO:** Không được loại bỏ nút bấm, biểu đồ, bảng dữ liệu hoặc form chỉ để làm giao diện "trông tối giản hơn".
2. **CẮT BỚT BỘ LỌC HOẶC TABS:** Không được gộp ép các tab nghiệp vụ độc lập (ví dụ: gộp tab Lịch sử, Định kỳ và Quy tắc làm một mà làm mất đi khả năng quản lý).
3. **TẠO SỐ LIỆU TÀI CHÍNH GIẢ MẠO:** Không phát minh thêm các chỉ số KPI mà Backend không cung cấp (ví dụ: "Tỷ suất sinh lời cổ phiếu", "Điểm tín dụng AI giả").
4. **THAY ĐỔI LUỒNG BẢO MẬT & RÀNG BUỘC NGHIỆP VỤ:**
   - Không được bỏ bước nhập mã PIN bảo mật 6 số khi: Đóng ví (`WALLET-005D`), Chuyển tiền (`TRANSFER-006`), Xóa ngân sách (`BUDGET-005B`), Xóa mục tiêu (`GOAL-006B`), Nạp/Rút mục tiêu (`GOAL-007`).
   - Không được bỏ bước xác thực Email OTP 8 chữ số khi đăng ký (`AUTH-006`).
   - Email người dùng là định danh bất biến (Read-only trong Profile `SET-001`).

---

## 3. DANH SÁCH 28 MÀN HÌNH VÀ BỘ KHUNG THIẾT KẾ CỐT LÕI

| STT | Page ID | Tên Trang | Route | Layout | Chức năng cốt lõi bắt buộc bảo toàn |
|---|---|---|---|---|---|
| 1 | `USER-DASHBOARD` | Tổng quan tài chính | `/dashboard` | `AppShell` | Lời chào (`DASH-001`), Mắt số dư (`DASH-002`), Số dư tổng (`DASH-003`), 4 Quick Actions Thu/Chi/Chuyển/Nhóm (`DASH-004A-D`), 4 KPI Cards (`DASH-005-008`), Tỷ lệ dòng tiền (`DASH-009`), Banner cảnh báo (`DASH-010`), Bar Chart 6 tháng (`DASH-011`), Donut Danh mục (`DASH-012`), 5 Giao dịch gần nhất (`DASH-013`). |
| 2 | `USER-WALLET` | Ví & Ngân hàng | `/wallet` | `AppShell` | Tổng số dư ví (`WALLET-001`), Nạp ví Test (`WALLET-002`), Chuyển giữa các ví (`WALLET-003`), Danh sách thẻ ví (`WALLET-004`), Vòng đời ví: Tạo/Sửa/Mặc định/Đóng ví với PIN (`WALLET-005A-D`), Hub SePay (`WALLET-006`), Đồng bộ/Hủy/Liên kết lại ngân hàng (`WALLET-007A-C`), Bảng sao kê SePay & Phân loại (`WALLET-008`), Nhật ký nội bộ (`WALLET-009`). |
| 3 | `USER-TRANSACTIONS` | Lịch sử & Quy tắc | `/transactions` | `AppShell` | 3 Tabs chính: Lịch sử (`TXN-001A`), Định kỳ (`TXN-001B`), Quy tắc (`TXN-001C`); Nút Thêm giao dịch (`TXN-002`), Tìm kiếm Omni (`TXN-003`), Lọc Loại/Ví/Danh mục/Hạn mức (`TXN-004-007`), Bảng giao dịch (`TXN-008`), Phân trang (`TXN-009`), Quản lý định kỳ (`TXN-010`), Quy tắc phân loại (`TXN-011`), Tải & xem biên lai đính kèm (`TXN-012`). |
| 4 | `USER-TRANSFER` | Chuyển tiền cá nhân | `/transfer` | `AppShell` | Chọn ví nguồn (`TRANSFER-001`), Tra cứu SĐT 10 số VN (`TRANSFER-002`), Bạn bè thân thiết gợi ý (`TRANSFER-003`), Nhập tiền + Dịch số thành chữ (`TRANSFER-004`), Lời nhắn (`TRANSFER-005`), Xác thực PIN 6 số (`TRANSFER-006`), Biên nhận thành công (`TRANSFER-007`), Lịch sử chuyển gần nhất (`TRANSFER-008`). |
| 5 | `USER-BUDGET` | Quản lý ngân sách | `/budget` | `AppShell` | Bộ chọn tháng MM/YYYY (`BUDGET-001`), Tạo ngân sách (`BUDGET-002`), Thước đo Gauge tổng (`BUDGET-003`), Thẻ ngân sách danh mục đổi màu xanh/vàng/đỏ (`BUDGET-004`), Sửa hạn mức & Xóa có PIN (`BUDGET-005A-B`), Drawer giao dịch con (`BUDGET-006`), Rollover & Alert 80% (`BUDGET-007`). |
| 6 | `USER-GOALS` | Mục tiêu tích lũy | `/goals` | `AppShell` | Thống kê tổng quan (`GOAL-001`), Tạo mục tiêu (`GOAL-002`), Thẻ tiến độ mục tiêu (`GOAL-003`), Nạp tiền từ ví (`GOAL-004`), Rút tiền về ví (`GOAL-005`), Sửa & Xóa mục tiêu (`GOAL-006A-B`), Xác thực PIN bảo mật (`GOAL-007`). |
| 7 | `USER-GROUPS` | Quỹ nhóm chung | `/groups` | `AppShell` | Tổng quan quỹ nhóm (`GROUP-001`), Tạo nhóm mới (`GROUP-002`), Danh sách thẻ nhóm (`GROUP-003`), Đợt vận động đóng quỹ (`GROUP-004`), Mời bạn bè (`GROUP-005`), Ghi nhận chi tiêu quỹ nhóm (`GROUP-006`). |
| 8 | `USER-FRIENDS` | Danh bạ bạn bè | `/friends` | `AppShell` | Gửi lời mời kết bạn (`FRIEND-001`), Tab Bạn bè & Lời mời (`FRIEND-002`), Đánh dấu sao Bạn thân (`FRIEND-003`), Biệt danh tùy chỉnh (`FRIEND-004`), Chuyển tiền nhanh 1 chạm sang `/transfer` (`FRIEND-005`), Chấp nhận/Từ chối lời mời (`FRIEND-006`), Drawer lịch sử chuyển qua lại (`FRIEND-007`). |
| 9 | `USER-NOTIFICATIONS`| Trung tâm thông báo | `/notifications` | `AppShell` | Đánh dấu đọc tất cả (`NOTIF-001`), Xóa thông báo đã đọc (`NOTIF-002`), Tabs lọc danh mục (`NOTIF-003`), Item thông báo tương tác + Deep-link (`NOTIF-004`), Xóa từng thông báo (`NOTIF-005`), Modal cấu hình kênh nhận thông báo (`NOTIF-006`). |
| 10| `USER-SUPPORT` | Hỗ trợ & Khiếu nại | `/support` | `AppShell` | 2 Tabs Yêu cầu vs Khiếu nại (`SUPPORT-001`), Gửi yêu cầu hỗ trợ (`SUPPORT-002`), Danh sách ticket (`SUPPORT-003`), Tạo khiếu nại giao dịch (`SUPPORT-004`), Thước đo tiến trình thẩm tra (`SUPPORT-005`), Drawer chat 2 chiều (`SUPPORT-006`). |
| 11| `USER-SETTINGS` | Cài đặt & Bảo mật | `/settings` | `AppShell` | Sửa họ tên & SĐT (`SET-001`), Chuyển chủ đề Sáng/Tối/Hệ thống (`SET-002`), Ngôn ngữ vi-VN/en-US (`SET-003`), Mặc định ẩn số dư (`SET-004`), Đổi mã PIN 6 số (`SET-005`), Quên PIN (`SET-006`), Đổi mật khẩu (`SET-007`), Quản lý thiết bị tin cậy (`SET-008`), Đăng xuất cuối cùng (`SET-009`). |
| 12| `AUTH-LOGIN` | Đăng nhập | `/login` | `Tabletop` | Form đăng nhập email/SĐT + mật khẩu (`AUTH-001`), Modal PIN 2FA 6 số (`AUTH-002`), Quên mật khẩu (`AUTH-003`), Quên PIN (`AUTH-004`). |
| 13| `AUTH-REGISTER` | Đăng ký | `/register` | `Tabletop` | Bước 1 thông tin cá nhân & độ mạnh mật khẩu (`AUTH-005`), Bước 2 xác thực OTP 8 số (`AUTH-006`), Bước 3 thiết lập PIN 6 số (`AUTH-007`). |
| 14| `AUTH-RESET-PIN` | Khôi phục PIN | `/reset-pin` | `Standalone` | Xác thực token một lần từ email và thiết lập lại PIN 6 số mới (`AUTH-008`). |
| 15| `SYS-FORBIDDEN` | Truy cập bị từ chối | `/forbidden` | `Standalone` | Màn hình 403 Forbidden kèm nút quay về trang chủ theo vai trò tài khoản (`AUTH-009`). |
| 16-28| 13 Màn hình Admin | Quản trị hệ thống | `/admin/*` | `AdminAppShell` | Giám sát KPI (`ADM-OVR`), Quản trị người dùng & Phân quyền (`ADM-USR`), Giám sát giao dịch (`ADM-TX`), Sổ cái kép Debit==Credit (`ADM-LDG`), Radar rủi ro & Đóng băng tài khoản (`ADM-RSK`), Phán quyết khiếu nại (`ADM-DSP`), Hỗ trợ CS (`ADM-SPT`), Kiểm toán bất biến (`ADM-ADT`), Giám sát kết nối SePay & Webhook (`ADM-BNK`), Đối soát ngân hàng (`ADM-RCN`), Sức khỏe hạ tầng .NET 10/SQL Server (`ADM-HLT`), Tham số hệ thống & Banner khẩn cấp (`ADM-CFG`), Bật/tắt Feature Flags (`ADM-FLG`). |

---

## 4. HỢP ĐỒNG ĐỐI CHIẾU FEATURE PARITY BẮT BUỘC (MAPPING CONTRACT)

Mỗi phương án thiết kế mới khi xuất bản (Figma Frames hoặc Google Stitch Screens) phải cung cấp kèm một **Bảng ánh xạ Feature Parity**:

$$ \text{Feature ID} \longrightarrow \text{New Section / Frame} \longrightarrow \text{Component Name} \longrightarrow \text{UI Trigger / Interaction} $$

Ví dụ bảng nghiệm thu đối chiếu:

| Feature ID | Tên chức năng hiện có | Vị trí trong Bản thiết kế Mới | Tên Component mới | Loại tương tác | Trạng thái Parity |
|---|---|---|---|---|---|
| `DASH-001` | Lời chào theo buổi | Top Header | `HeroGreeting` | Hiển thị tự động theo giờ | **PASS** |
| `DASH-002` | Nút ẩn/hiện số dư mắt | Góc phải thẻ số dư chính | `PrivacyToggleBtn` | Click icon mắt đổi trạng thái | **PASS** |
| `DASH-003` | Thẻ tổng số dư | Hero Panel trên cùng | `BalanceHeroCard` | Hiển thị số tiền tabular-nums | **PASS** |
| `DASH-004A` | Nút Quick Action: Thu nhập | Floating Action Pill 1 | `QuickIncomeButton` | Click mở Income Modal | **PASS** |
| `DASH-004B` | Nút Quick Action: Chi tiêu | Floating Action Pill 2 | `QuickExpenseButton` | Click mở Expense Modal | **PASS** |
| `DASH-004C` | Nút Quick Action: Chuyển tiền | Floating Action Pill 3 | `QuickTransferButton` | Click điều hướng `/transfer` | **PASS** |
| `DASH-004D` | Nút Quick Action: Quỹ nhóm | Floating Action Pill 4 | `QuickGroupButton` | Click điều hướng `/groups` | **PASS** |
| `DASH-011` | Biểu đồ cột 6 tháng | Section Phân tích tài chính | `CashFlowChart` | Tương tác hover cột xem số tiền | **PASS** |
| `DASH-012` | Biểu đồ Donut danh mục | Section Cơ cấu chi tiêu | `CategoryDonutChart` | Hover cung tròn xem tỷ trọng % | **PASS** |

> **Quy tắc nghiệm thu sống còn:** Nếu một bản thiết kế mới bị thiếu dù chỉ 1 Feature ID trong danh sách 146 tính năng mà không có lý do được phê duyệt từ Product Owner, bản thiết kế đó sẽ bị **TỪ CHỐI DUYỆT NGAY LẬP TỨC (BLOCKED - INCOMPLETE PARITY)**.

---

## 5. TÍCH HỢP TỰ ĐỘNG VỚI GOOGLE STITCH & FIGMA

- Tệp máy đọc chuẩn: `frontend/docs/ui-blueprint/feature-manifest.json`.
- Chứa toàn bộ 146 đối tượng tính năng với đầy đủ 9 trường đặc tả chi tiết: `expectedBehavior`, `apiDependencies`, `validation`, `permissions`, `states`, `navigationTarget`, `sideEffects`, `evidence`, `verificationStatus`.
- Các prompt đưa vào Google Stitch hoặc các plugin Figma có thể nạp trực tiếp danh sách `sections` và `features` từ file JSON để sinh layout đảm bảo không bao giờ bị bỏ sót thành phần nào.
