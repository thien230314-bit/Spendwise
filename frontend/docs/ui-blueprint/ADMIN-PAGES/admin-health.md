# ADMIN PAGE INVENTORY: INFRASTRUCTURE HEALTH (GIÁM SÁT SỨC KHỎE HẠ TẦNG)
**Page ID:** `ADMIN-HEALTH`  
**Route:** `/admin/health`  
**Layout:** `AdminAppShell`  
**Quyền hạn:** `Role: SystemAdmin`  
**File Source:** `src/features/admin/AdminHealthPage.tsx`  
**Trạng thái Xác thực:** `SOURCE_VERIFIED`

---

## 1. MỤC ĐÍCH NGHIỆP VỤ & TỔNG QUAN
Bảng điều khiển giám sát tình trạng hoạt động thực tế của toàn bộ các nút hạ tầng kỹ thuật phụ trợ SpendWise.

Kiến trúc thực tế đã đối chiếu chuẩn:
- **Core Backend API:** ASP.NET Core (.NET 10.0) Web API
- **Primary Database:** Microsoft SQL Server (EF Core 10.0.8)
- **AI Financial Assistant Service:** Trợ lý tài chính tích hợp
- **SePay Open Banking Gateway:** Cổng đồng bộ ngân hàng
- **SignalR Realtime Server:** Kênh truyền tin thời gian thực
- **Notifications Outbox:** Bộ chuyển tiếp thông báo theo mẫu Outbox
- **Background Jobs & Recurring Tasks:** Tác vụ chạy ngầm định kỳ

---

## 2. KIỂM KÊ CÁC SECTION TRÊN TRANG

| Section ID | Tên Section | Component Source | Vị trí Bố cục | Trạng thái Hiển thị |
|---|---|---|---|---|
| `ADM-HLT-SEC-01` | System Infrastructure Health Monitor | `AdminHealthPage.tsx` lines 1-435 | Thân trang toàn diện | Luôn hiển thị |

---

## 3. DANH MỤC TÍNH NĂNG CHI TIẾT (FEATURE INVENTORY)

### Section ADM-HLT-SEC-01: System Infrastructure Health Monitor
- **Feature ID:** `ADM-HLT-001`
  - **Tên:** Kiểm tra xung nhịp dịch vụ thời gian thực (Service Nodes Pulse Ping)
  - **Mục đích:** Gửi ping kiểm tra tức thời trạng thái của Core API, SQL Server Database, AI Provider, SePay Gateway, SignalR, Outbox và Background Jobs.
  - **Verification:** `SOURCE_VERIFIED`.

- **Feature ID:** `ADM-HLT-002`
  - **Tên:** Giám sát độ trễ và tài nguyên hệ thống (Resource Telemetry & Latency Metrics)
  - **Mục đích:** Theo dõi độ trễ phản hồi (ms) của từng node hạ tầng và tỷ lệ sẵn sàng phục vụ.
  - **Verification:** `SOURCE_VERIFIED`.
