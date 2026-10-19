# SPENDWISE COMPLETE UI BLUEPRINT
**Tài liệu Định danh & Hướng dẫn Sử dụng Bộ Blueprint Kiến trúc Giao diện SpendWise**  
**Hệ thống:** SpendWise Web Application  
**Phiên bản Blueprint:** v1.0.0 (Release: 12/10/2026)  
**Tác giả:** Antigravity AI Pair Programmer  

---

## 1. MỤC TIÊU VÀ SỨ MỆNH CỦA BỘ BLUEPRINT
Bộ tài liệu này được tạo ra để giải quyết triệt để bài toán: **Làm sao để thiết kế lại (Redesign) giao diện đẹp hơn, hiện đại hơn mà KHÔNG BỎ SÓT BẤT KỲ TÍNH NĂNG NÀO của SpendWise**.  
Tài liệu đóng vai trò là "Bản vẽ kỹ thuật chi tiết" để các công cụ thiết kế bên ngoài (Google Stitch, Figma, Codex) và các AI Agent khác có thể đọc hiểu và đối chiếu tính năng với độ chính xác 100%.

---

## 2. CẤU TRÚC THƯ MỤC VÀ TÀI LIỆU
Toàn bộ tài liệu được đặt tập trung trong: `frontend/docs/ui-blueprint/`

```
frontend/docs/ui-blueprint/
├── README.md                      # Tài liệu tổng quan & Hướng dẫn sử dụng này
├── PAGE-INVENTORY.md              # Kiểm kê toàn bộ 36 routes và 29 màn hình thực tế
├── DESIGN-SYSTEM.md               # Kiểm kê Font chữ, Bảng màu, Component UI, Layout
├── FUNCTIONAL-FLOWS.md            # Sơ đồ và mô tả 7 luồng tương tác tài chính cốt lõi
├── DATA-AND-STATE-MAPPING.md      # Ánh xạ React Providers, Hooks, API Services & Công thức
├── EXTERNAL-DESIGN-HANDOFF.md     # Hướng dẫn quy tắc bắt buộc cho Google Stitch & Figma
├── VERIFICATION-REPORT.md         # Báo cáo thẩm tra mức độ xác minh (Browser vs Source)
├── feature-manifest.json          # File cấu trúc dữ liệu JSON chuẩn cho AI & Công cụ máy đọc
├── FEATURE-ID-MIGRATION.md        # Sổ tay phân tách Feature ID nguyên tử (DASH-004A-D, WALLET-005A-D)
├── validate-blueprint.js          # Script tự động thẩm tra tính toàn vẹn và parity 100%
├── USER-PAGES/                    # Kiểm kê chi tiết 11 trang người dùng cá nhân
│   ├── dashboard.md               # Tổng quan tài chính (/dashboard)
│   ├── wallet.md                  # Ví của tôi & Ngân hàng SePay (/wallet)
│   ├── transactions.md            # Lịch sử, Định kỳ & Quy tắc (/transactions)
│   ├── transfer.md                # Chuyển tiền trực tiếp & Bạn bè (/transfer)
│   ├── budget.md                  # Quản lý ngân sách danh mục (/budget)
│   ├── goals.md                   # Mục tiêu tích lũy (/goals)
│   ├── groups.md                  # Quỹ nhóm & Góp quỹ (/groups)
│   ├── friends.md                 # Danh bạ bạn bè (/friends)
│   ├── notifications.md           # Trung tâm thông báo (/notifications)
│   ├── support-disputes.md        # Hỗ trợ & Khiếu nại (/support, /disputes)
│   └── settings.md                # Cài đặt tài khoản & Bảo mật (/settings)
├── ADMIN-PAGES/                   # Kiểm kê chi tiết 13 trang Quản trị Admin riêng biệt
│   ├── admin-overview.md          # Tổng quan vận hành (/admin/overview)
│   ├── admin-users.md             # Quản trị người dùng (/admin/users)
│   ├── admin-transactions.md      # Giám sát giao dịch (/admin/transactions)
│   ├── admin-ledger.md            # Sổ cái kép (/admin/ledger)
│   ├── admin-risk.md              # Quản trị rủi ro (/admin/risk)
│   ├── admin-disputes.md          # Xử lý tranh chấp (/admin/disputes)
│   ├── admin-support.md           # Hỗ trợ khách hàng (/admin/support)
│   ├── admin-audit.md             # Nhật ký kiểm toán (/admin/audit)
│   ├── admin-banking.md           # Giám sát cổng ngân hàng SePay (/admin/banking)
│   ├── admin-reconciliation.md    # Đối soát dòng tiền (/admin/reconciliation)
│   ├── admin-health.md            # Sức khỏe hạ tầng .NET 10 & SQL Server (/admin/health)
│   ├── admin-configuration.md     # Cấu hình tham số hệ thống (/admin/configuration)
│   ├── admin-feature-flags.md     # Quản lý cờ tính năng (/admin/feature-flags)
│   ├── core-operations.md         # Index nhóm Vận hành cốt lõi
│   ├── compliance-and-banking.md  # Index nhóm Tuân thủ & Ngân hàng
│   └── infrastructure-and-config.md# Index nhóm Hạ tầng & Cấu hình
└── PUBLIC-AUTH-PAGES/             # Kiểm kê các trang Xác thực & Công khai
    └── authentication.md          # Login, Register, Reset PIN, 403 Forbidden
```

---

## 3. NGUYÊN TẮC BẤT DI BẤT DỊCH
1. **Redesign the presentation, NOT the product:** Chỉ cải thiện cách hiển thị; không xóa tính năng.
2. **Feature ID Contract:** Mỗi tính năng được gán một mã định danh duy nhất (ví dụ: `DASH-001`, `WALLET-006`, `TRANSFER-004`). Mọi bản thiết kế mới phải chứng minh được vị trí của toàn bộ các mã này.
3. **Data Integrity:** Không lưu giữ thông tin nhạy cảm (mật khẩu, PIN, token, OTP) trong tài liệu.
