# PAGE INVENTORY — SPENDWISE WEB FRONTEND
**Tài liệu Kiểm kê Toàn bộ Route & Màn hình Ứng dụng SpendWise**  
**Hệ thống:** SpendWise Web Application  
**Ngày kiểm kê:** 12/10/2026  
**Nguồn xác thực:** `src/app/router/AppRouter.tsx`, Chrome DevTools MCP Runtime  
**Nguyên tắc:** Phân loại chính xác, không tự suy diễn số lượng, đối chiếu 100% với Router thực tế.

---

## 1. TỔNG HỢP TOÀN CỤC (GLOBAL METRICS)

- **Tổng số Route khai báo trong AppRouter:** 36 routes
- **Tổng số Screen / Màn hình nghiệp vụ thực tế độc lập:** 29 screens
  - **Màn hình User Protected (`Role: User`):** 11 screens
  - **Màn hình Admin Console (`Role: SystemAdmin`):** 13 screens
  - **Màn hình Public / Auth:** 4 screens (`/login`, `/register`, `/reset-pin`, `/forbidden`)
  - **Màn hình Development Preview:** 1 screen (`/preview/ui`)
- **Số Route Aliases / Redirects:** 8 routes
- **Số Fallback / Dispatcher Routes:** 2 routes (`/`, `*`)

---

## 2. BẢNG KIỂM KÊ CHI TIẾT THEO PHÂN LOẠI

### 2.1. Public & Authentication Routes (4 Screens)

| # | Route / Path | Page ID | Tên trang tiếng Việt | Tên tiếng Anh | Vai trò (Role) | Layout / Shell | Component chính | Trạng thái |
|---|---|---|---|---|---|---|---|---|
| 1 | `/login` | `AUTH-LOGIN` | Đăng nhập SpendWise | Sign In / Login | Public | Standalone Desk Tabletop | `LoginPage.tsx` (wraps `AuthPage.tsx` mode='login') | `ACTIVE` |
| 2 | `/register` | `AUTH-REGISTER` | Đăng ký tài khoản | Sign Up / Register | Public | Standalone Desk Tabletop | `RegisterPage.tsx` (wraps `AuthPage.tsx` mode='register') | `ACTIVE` |
| 3 | `/reset-pin` | `AUTH-RESET-PIN` | Khôi phục mã PIN bảo mật 6 số | Security PIN Recovery | Public (Token required) | Standalone Centered Card | `ResetPinPage.tsx` | `ACTIVE` |
| 4 | `/forbidden` | `SYS-FORBIDDEN` | Truy cập bị từ chối (403) | 403 Forbidden | Public / Authenticated | Standalone Clean Error Card | `ForbiddenPage.tsx` | `ACTIVE` |
| * | `/reset-password` | `AUTH-RESET-PWD` | Chuyển hướng Quên mật khẩu | Reset Password Redirect | Public | Redirect | Chuyển hướng ngay về `/login?action=forgot` | `REDIRECT` |

---

### 2.2. User Personal Finance Routes (11 Screens)
*Tất cả được bảo vệ bởi `<RoleRoute allowedRoles={['User']}>` và hiển thị trong `<AppShell>`.*

| # | Route / Path | Page ID | Tên trang tiếng Việt | Tên tiếng Anh | Component chính | API Services / Providers | Trạng thái |
|---|---|---|---|---|---|---|---|
| 1 | `/dashboard` | `USER-DASHBOARD` | Tổng quan tài chính | Financial Dashboard | `DashboardPage.tsx` | `FinancialProvider`, `GroupProvider`, `BalanceVisibilityProvider` | `ACTIVE` (Browser Verified) |
| 2 | `/wallet` | `USER-WALLET` | Ví của tôi & Ngân hàng | My Wallets & Bank Hub | `WalletPage.tsx` | `FinancialProvider`, `bankApiService`, `SecurityPinChallengeProvider` | `ACTIVE` (Source Verified) |
| 3 | `/transactions` | `USER-TRANSACTIONS` | Lịch sử & Quản lý giao dịch | Transactions History | `TransactionsPage.tsx` | `FinancialProvider`, `recurringService`, `transactionClassificationService` | `ACTIVE` (Source Verified) |
| 4 | `/transfer` | `USER-TRANSFER` | Chuyển tiền nội bộ & bạn bè | Direct Money Transfer | `TransferPage.tsx` | `transfersApiService`, `friendService`, `SecurityPinChallengeProvider` | `ACTIVE` (Source Verified) |
| 5 | `/budget` | `USER-BUDGET` | Quản lý ngân sách | Category Budgets | `BudgetPage.tsx` | `FinancialProvider`, `SecurityPinChallengeProvider` | `ACTIVE` (Source Verified) |
| 6 | `/goals` | `USER-GOALS` | Mục tiêu tích lũy tài chính | Savings Goals | `GoalsPage.tsx` | `FinancialProvider`, `SecurityPinChallengeProvider` | `ACTIVE` (Source Verified) |
| 7 | `/groups` | `USER-GROUPS` | Quản lý quỹ nhóm | Shared Group Funds | `GroupsPage.tsx` | `GroupProvider`, `groupApiService`, `friendService` | `ACTIVE` (Source Verified) |
| 8 | `/groups/:groupId` | `USER-GROUP-DETAIL` | Chi tiết quỹ & thành viên nhóm | Group Fund Detail | `GroupDetailPage.tsx` | `GroupProvider`, `groupApiService` | `ACTIVE` (Source Verified) |
| 9 | `/friends` | `USER-FRIENDS` | Danh bạ bạn bè & Liên hệ | Friends & Contacts | `FriendsPage.tsx` | `friendService`, `friendsApiService` | `ACTIVE` (Source Verified) |
| 10 | `/notifications` | `USER-NOTIFICATIONS` | Trung tâm thông báo hệ thống | Notification Center | `NotificationsPage.tsx` | `NotificationProvider`, `notificationsApiService` | `ACTIVE` (Source Verified) |
| 11 | `/settings` | `USER-SETTINGS` | Cài đặt tài khoản & Bảo mật | Account & Security Settings | `SettingsPage.tsx` | `AuthProvider`, `securityPinService`, `ThemeProvider`, `I18nContext` | `ACTIVE` (Browser Verified) |
| * | `/support` | `USER-SUPPORT` | Hỗ trợ & Khiếu nại (Tab Yêu cầu) | Support & Disputes (Cases) | `SupportDisputesPage.tsx` defaultTab="cases" | `casesApiService`, `supportDisputeService` | `ACTIVE (Sub-view)` |
| * | `/disputes` | `USER-DISPUTES` | Hỗ trợ & Khiếu nại (Tab Khiếu nại) | Support & Disputes (Disputes) | `SupportDisputesPage.tsx` defaultTab="disputes" | `casesApiService`, `supportDisputeService` | `ACTIVE (Sub-view)` |

---

### 2.3. System Admin Console Routes (13 Screens)
*Tất cả được bảo vệ bởi `<RoleRoute allowedRoles={['SystemAdmin']}>` và hiển thị trong `<AdminAppShell>`.*

| # | Route / Path | Page ID | Tên trang tiếng Việt | Tên tiếng Anh | Component chính | API Services / Providers | Trạng thái |
|---|---|---|---|---|---|---|---|
| 1 | `/admin` / `/admin/overview` | `ADMIN-OVERVIEW` | Tổng quan điều hành hệ thống | Admin System Overview | `AdminOverviewPage.tsx` | `systemOperationsService`, `adminAiApiService` | `ACTIVE` (Source Verified) |
| 2 | `/admin/users` | `ADMIN-USERS` | Quản trị người dùng & Phân quyền | User Management | `AdminUsersPage.tsx` | `systemOperationsService` | `ACTIVE` (Source Verified) |
| 3 | `/admin/transactions` | `ADMIN-TRANSACTIONS` | Giám sát giao dịch toàn hệ thống | Global Transactions Audit | `AdminTransactionsPage.tsx` | `systemOperationsService` | `ACTIVE` (Source Verified) |
| 4 | `/admin/ledger` | `ADMIN-LEDGER` | Kiểm toán sổ cái kép (Ledger) | Double-Entry General Ledger | `AdminLedgerPage.tsx` | `systemOperationsService` | `ACTIVE` (Source Verified) |
| 5 | `/admin/risk` | `ADMIN-RISK` | Cảnh báo rủi ro & Gian lận | Anti-Fraud & Risk Radar | `AdminRiskPage.tsx` | `systemOperationsService` | `ACTIVE` (Source Verified) |
| 6 | `/admin/disputes` | `ADMIN-DISPUTES` | Xử lý khiếu nại giao dịch | Dispute Resolution Console | `AdminDisputesPage.tsx` | `systemOperationsService`, `casesApiService` | `ACTIVE` (Source Verified) |
| 7 | `/admin/support` | `ADMIN-SUPPORT` | Trung tâm hỗ trợ khách hàng | Support Ticketing Operations | `AdminSupportPage.tsx` | `casesApiService`, `systemOperationsService` | `ACTIVE` (Source Verified) |
| 8 | `/admin/audit` | `ADMIN-AUDIT` | Nhật ký kiểm toán bảo mật | Security Audit Trails | `AdminAuditPage.tsx` | `systemOperationsService` | `ACTIVE` (Source Verified) |
| 9 | `/admin/banking` | `ADMIN-BANKING` | Giám sát kết nối ngân hàng SePay | Open Banking & SePay Monitoring | `AdminBankingPage.tsx` | `systemOperationsService`, `bankApiService` | `ACTIVE` (Source Verified) |
| 10 | `/admin/reconciliation` | `ADMIN-RECONCILIATION` | Đối soát dòng tiền ngân hàng | Bank Statement Reconciliation | `AdminReconciliationPage.tsx` | `systemOperationsService` | `ACTIVE` (Source Verified) |
| 11 | `/admin/health` | `ADMIN-HEALTH` | Giám sát sức khỏe hạ tầng | Infrastructure & Health Monitor | `AdminHealthPage.tsx` | `systemOperationsService` | `ACTIVE` (Source Verified) |
| 12 | `/admin/configuration` | `ADMIN-CONFIG` | Cấu hình tham số hệ thống | System Master Configuration | `AdminConfigurationPage.tsx` | `systemOperationsService` | `ACTIVE` (Source Verified) |
| 13 | `/admin/feature-flags` | `ADMIN-FLAGS` | Quản lý cờ tính năng (Feature Flags) | Feature Flags Manager | `AdminFeatureFlagsPage.tsx` | `systemOperationsService` | `ACTIVE` (Source Verified) |
| * | `/admin/sandbox` | `ADMIN-SANDBOX-ALIAS` | Điều hướng Sandbox cũ | Sandbox Legacy Alias | Redirect | Chuyển hướng tự động về `/admin/banking` | `REDIRECT` |

---

### 2.4. Development & Preview Routes (1 Screen)

| # | Route / Path | Page ID | Tên trang | Mục đích | Component chính | Trạng thái |
|---|---|---|---|---|---|---|
| 1 | `/preview/ui` | `DEV-PREVIEW-CENTER` | SpendWise UI Preview Center | Môi trường kiểm thử trực quan các bản thiết kế, S-Loading, Skeletons, Onboarding, và UI Approval | `UiPreviewCenterPage.tsx` | `ACTIVE` (Browser Verified) |

---

### 2.5. Route Aliases & Redirects Mapping

| Route gốc | Loại | Đích đến (Target) | Mục đích nghiệp vụ |
|---|---|---|---|
| `/` | Role Dispatcher | `/dashboard` (User) hoặc `/admin` (SystemAdmin) hoặc `/login` (Unauthenticated) | Điều hướng thông minh dựa trên phiên đăng nhập và quyền hạn |
| `/contacts` | Alias | `/friends` | Tương thích ngược với định danh danh bạ |
| `/wallets` | Alias | `/wallet` | Chuyển số nhiều về route số ít chuẩn |
| `/budgets` | Alias | `/budget` | Chuyển số nhiều về route số ít chuẩn |
| `/transfers` | Alias | `/transfer` | Chuyển số nhiều về route số ít chuẩn |
| `/recurring` | Deep-link Alias | `/transactions?tab=recurring` | Mở trực tiếp tab Giao dịch định kỳ |
| `/rules` | Deep-link Alias | `/transactions?tab=rules` | Mở trực tiếp tab Quy tắc phân loại |
| `/categories` | Deep-link Alias | `/transactions?tab=categories` | Mở trực tiếp modal/tab Quản lý danh mục |
| `/admin/sandbox` | Alias | `/admin/banking` | Hợp nhất môi trường Sandbox vào Banking |
| `*` | Fallback Dispatcher | Tự động điều hướng về `/dashboard` (User) hoặc `/admin` (Admin) hoặc `/login` | Xử lý URL không tồn tại |
