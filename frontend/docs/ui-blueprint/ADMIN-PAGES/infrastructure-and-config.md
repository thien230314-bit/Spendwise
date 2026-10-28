# ADMIN PAGES GROUP: INFRASTRUCTURE & CONFIGURATION (HẠ TẦNG & CẤU HÌNH)
**Hệ thống:** SpendWise System Administrator Console  
**Layout:** `AdminAppShell` (`RoleRoute allowedRoles={['SystemAdmin']}`)  
**Quyền hạn yêu cầu:** `Role: SystemAdmin`  
**Trạng thái Xác thực:** `SOURCE_VERIFIED`

---

## MỤC TIÊU VÀ ĐIỀU HƯỚNG TÀI LIỆU
Tài liệu này là mục lục nhóm điều hướng cho 3 màn hình quản trị hạ tầng kỹ thuật, cấu hình toàn hệ thống và cờ tính năng. Mỗi màn hình được đặc tả chi tiết trong file riêng biệt:

1. [Admin Health (Giám sát Sức khỏe Hạ tầng)](./admin-health.md) — `ADMIN-HEALTH` (`/admin/health`)
   - Section: `ADM-HLT-SEC-01`
   - Features: `ADM-HLT-001`, `ADM-HLT-002`
   - *Kiến trúc kỹ thuật:* Backend ASP.NET Core (.NET 10.0) Web API, Cơ sở dữ liệu Microsoft SQL Server (EF Core 10.0.8), SignalR Realtime, Outbox pattern, Background recurring services.

2. [Admin Configuration (Cấu hình Tham số Toàn hệ thống)](./admin-configuration.md) — `ADMIN-CONFIG` (`/admin/configuration`)
   - Section: `ADM-CFG-SEC-01`
   - Features: `ADM-CFG-001`, `ADM-CFG-002`

3. [Admin Feature Flags (Quản lý Cờ Tính năng)](./admin-feature-flags.md) — `ADMIN-FLAGS` (`/admin/feature-flags`)
   - Section: `ADM-FLG-SEC-01`
   - Features: `ADM-FLG-001`, `ADM-FLG-002`
