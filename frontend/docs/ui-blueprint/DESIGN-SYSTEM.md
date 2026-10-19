# SPENDWISE DESIGN SYSTEM INVENTORY
**Tài liệu Kiểm kê Hệ thống Thiết kế & Visual Tokens Hiện có**  
**Dự án:** SpendWise Web Application  
**Ngày kiểm kê:** 12/10/2026  
**Nguồn xác thực:** `src/index.css`, `@tailwindcss/vite` v4.1.14, `src/components/ui/`, Browser Computed Styles  
**Nguyên tắc:** Ghi nhận chính xác hệ thống đang chạy trong code; không phát minh token mới; không redesign.

---

## 1. TYPOGRAPHY (HỆ THỐNG PHÔNG CHỮ & CỠ CHỮ)

### 1.1. Font Families
Xác thực trực tiếp từ `src/index.css` (Layer Base `:root`):

| Token CSS | Tên Font | Fallback Stack | Mục đích sử dụng |
|---|---|---|---|
| `--font-sans` | **Be Vietnam Pro** | `'Plus Jakarta Sans', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif` | Phông chữ chính cho toàn bộ nội dung body, label, input, button, table |
| `--font-display`| **Plus Jakarta Sans**| `-apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif` | Tiêu đề các cấp (`h1` đến `h6`), thẻ KPI, số dư chính |
| `--font-mono` | **IBM Plex Mono** | `monospace` | Hiển thị số tiền tệ (`tabular-nums`), mã giao dịch (`referenceCode`), số tài khoản, mã OTP/PIN, địa chỉ IP |
| `--font-serif`| **Fraunces** | `Georgia, serif` | Dùng trang trí trên bìa sổ cái (Desk Tabletop Notebook bìa da) |

### 1.2. Typography Scale (Thang cỡ chữ)

| Cấp bậc | Utility Class Tailwind | Kích thước | Line-Height | Tracking | Trọng số (Font Weight) |
|---|---|---|---|---|---|
| **Display H1** | `text-3xl` / `sm:text-4xl` | 30px / 36px | 1.15 | `-0.02em` | `font-bold` (700) hoặc `font-extrabold` (800) |
| **Section H2** | `text-xl` / `sm:text-2xl` | 20px / 24px | 1.25 | `-0.02em` | `font-bold` (700) |
| **Card H3** | `text-base` / `text-lg` | 16px / 18px | 1.3 | `-0.01em` | `font-bold` (700) |
| **Subtitle** | `text-sm` | 14px | 1.4 | Bình thường | `font-medium` (500) hoặc `font-semibold` (600) |
| **Body Regular** | `text-sm` / `text-xs` | 14px / 12px | 1.5 | Bình thường | `font-normal` (400) |
| **Caption / Label** | `text-xs` / `text-[11px]` | 12px / 11px | 1.4 | `uppercase tracking-wider` | `font-semibold` (600) |
| **Badge / Micro** | `text-[10px]` / `text-[9px]` | 10px / 9px | 1.2 | `tracking-tight` | `font-bold` (700) |
| **Financial Figures** | `font-mono` + `tabular-nums` | Tùy vị trí | Tương ứng | Chặt chẽ | `font-bold` (700) |

---

## 2. COLOR PALETTE (BẢNG MÀU THƯƠNG HIỆU & CHỦ ĐỀ)

SpendWise sử dụng màu chủ đạo là **Emerald (Ngọc lục bảo tài chính)** kết hợp hệ màu trung tính Slate cao cấp.

### 2.1. Brand & Functional Colors

| Ý nghĩa | Tên Palette | Màu Light Mode | Màu Dark Mode | Tailwind Classes Tiêu biểu |
|---|---|---|---|---|
| **Primary Brand** | Emerald | `#059669` (Emerald 600) | `#10b981` (Emerald 500) | `bg-emerald-600`, `text-emerald-500`, `border-emerald-500` |
| **Primary Hover** | Emerald Dark | `#047857` (Emerald 700) | `#059669` (Emerald 600) | `hover:bg-emerald-700`, `hover:bg-emerald-600` |
| **Income / Dòng tiền vào** | Emerald / Green | `#10b981` | `#34d399` | `text-emerald-600 dark:text-emerald-400` |
| **Expense / Dòng tiền ra** | Rose / Red | `#e11d48` (Rose 600) | `#f43f5e` (Rose 500) | `text-rose-600 dark:text-rose-400` |
| **Transfer / Điều chuyển** | Sky / Blue | `#0284c7` (Sky 600) | `#38bdf8` (Sky 400) | `text-sky-600 dark:text-sky-400` |
| **Warning / Chú ý** | Amber | `#d97706` (Amber 600) | `#fbbf24` (Amber 400) | `bg-amber-50`, `text-amber-600`, `border-amber-200` |
| **Info / Liên kết ngoài** | Indigo / Blue | `#4f46e5` (Indigo 600) | `#818cf8` (Indigo 400) | `text-indigo-600`, `bg-indigo-50` |

### 2.2. Surface & Background (Light vs Dark)

| Vị trí giao diện | Light Mode (`html.light`) | Dark Mode (`html.dark`) | Ghi chú CSS |
|---|---|---|---|
| **App Canvas / Page Body** | `#F8FAFC` (Slate 50) | `#090D16` / `#0B0F19` | `src/index.css` dòng 23, 31 |
| **Card / Panel Surface** | `#FFFFFF` (Pure White) | `#0F172A` (Slate 900) hoặc `#111827` | `bg-white dark:bg-slate-900` |
| **Subtle Sub-surface / Well** | `#F1F5F9` (Slate 100) | `#1E293B` (Slate 800) | `bg-slate-50 dark:bg-slate-800/50` |
| **Border Thường** | `#E2E8F0` (Slate 200) | `#1E293B` (Slate 800) | `border-slate-200/80 dark:border-slate-800` |
| **Border Nhẹ (Divider)** | `#F1F5F9` (Slate 100) | `#334155` (Slate 700) | `divide-slate-100 dark:divide-slate-800` |
| **Text Chính (Heading/Value)**| `#0F172A` (Slate 900) | `#F8FAFC` (Slate 50) | `text-slate-900 dark:text-white` |
| **Text Phụ (Body)** | `#475569` (Slate 600) | `#94A3B8` (Slate 400) | `text-slate-600 dark:text-slate-400` |
| **Text Làm mờ (Muted/Hint)** | `#94A3B8` (Slate 400) | `#64748B` (Slate 500) | `text-slate-400 dark:text-slate-500` |

### 2.3. Realistic Desk Tabletop Environment (Auth Pages)
Hệ thống theme bàn làm việc vật lý (Scandinavian Oak Light vs Executive Walnut Dark):
- **Light:** `--desk-wood-bg: #d7c1a2`, vân ván ghép thanh rãnh ngang 150px kèm ánh đèn bàn ấm áp.
- **Dark:** `--desk-wood-bg: #140f0c`, gỗ óc chó hun khói mờ viền ngọc lục bảo.

---

## 3. COMPONENT PRIMITIVES INVENTORY

| Component | Đường dẫn File Source | Vai trò & Biến thể (Variants) | Đạo cụ (Props) chính |
|---|---|---|---|
| **Button** | `src/components/ui/Button.tsx` | Nút hành động tương tác. Variants: `primary`, `secondary`, `outline`, `ghost`, `danger`. Sizes: `sm`, `md`, `lg`. | `variant`, `size`, `isLoading`, `disabled`, `children` |
| **Select** | `src/components/ui/Select.tsx` | Ô chọn dropdown chuẩn Accessible | `options`, `value`, `onChange`, `label`, `error` |
| **ActionBadge** | `src/components/ui/ActionBadge.tsx` | Huy hiệu trạng thái giao dịch / bảo mật (Completed, Pending, Failed, Default) | `status`, `label`, `variant`, `size` |
| **SpendWiseSLoading** | `src/components/ui/SpendWiseSLoading.tsx` | Logo chữ "S" động chuyển trạng thái Lifecycle: `pending`, `success`, `error`, `offline`, `timeout` | `status`, `size`, `glowEffect`, `label` |
| **Spinner** | `src/components/ui/Spinner.tsx` | Con xoay tròn loading phụ | `size`, `className` |
| **Skeleton** | `src/components/ui/Skeleton.tsx` | Khung nạp dữ liệu chờ (Shimmer effect) | `className` |
| **Tooltip** | `src/components/ui/Tooltip.tsx` | Chú giải khi hover | `content`, `children`, `position` |
| **SixDigitPinInput** | `src/components/security/SixDigitPinInput.tsx` | 6 ô nhập mã PIN bảo mật tự động nhảy focus, mask dấu chấm tròn | `value`, `onChange`, `onComplete`, `autoFocus`, `disabled` |
| **SecurityPinChallengeModal** | `src/components/security/SecurityPinChallengeModal.tsx` | Modal chặn xác thực PIN 6 số toàn cục cho mọi giao dịch tiền bạc | Hook `useSecurityPinChallenge()` |

---

## 4. LAYOUT & SHELL SPECIFICATIONS

### 4.1. AppShell (User Dashboard Shell)
- **Top Header:** Cố định `h-16` (64px), thanh thông báo khẩn cấp (Emergency Banner), chuyển ngữ Tiếng Việt/English, chuyển Light/Dark mode, chuông thông báo có đếm số chưa đọc, menu tài khoản User với avatar initials.
- **Sidebar Navigation:**
  - Desktop: Thanh bên cố định bên trái `w-64` (256px), có thể thu gọn (collapsed mode `w-20`).
  - Mobile: Drawer trượt từ cạnh trái với nút đóng và backdrop mờ.
  - Menu Items: 11 mục chính kèm icon Lucide (Tổng quan, Ví, Giao dịch, Chuyển tiền, Ngân sách, Mục tiêu, Quỹ nhóm, Bạn bè, Thông báo, Hỗ trợ, Cài đặt).
- **Main Content Area:** `max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-6`.
- **Global Modals:** Nằm trong AppShell gồm `SessionExpiredModal`, `ForcePasswordChangeModal`, `SecurityPinChallengeModal`, `FloatingAiAssistant`.

### 4.2. AdminAppShell (System Admin Shell)
- **Admin Header:** `h-16`, hiển thị huy hiệu `System Admin Console`, breadcrumbs, System Health Status Indicator (Green dot), Quick Admin Search.
- **Admin Sidebar:** `w-64`, chứa 13 danh mục giám sát hệ thống (Overview, Users, Transactions, Ledger, Risk, Disputes, Support, Audit, Banking, Reconciliation, Health, Config, Flags).
- **Admin AI Assistant:** `FloatingAdminAiAssistant` hỗ trợ truy vấn nhanh chỉ số vận hành.

### 4.3. Breakpoints (Chuẩn Tailwind)
- `sm`: `640px` (Điện thoại nằm ngang / Tablet nhỏ)
- `md`: `768px` (Tablet dọc / Chuyển layout cột)
- `lg`: `1024px` (Laptop / Sidebar mở rộng)
- `xl`: `1280px` (Màn hình Desktop chuẩn)
- `2xl`: `1536px` (Màn hình lớn)

### 4.4. Spacing Scale & Border Radius
- **Border Radius:**
  - Cards lớn: `rounded-2xl` (16px) hoặc `rounded-3xl` (24px)
  - Buttons & Inputs: `rounded-xl` (12px)
  - Badges & Pills: `rounded-full` (9999px)
  - Icons boxes: `rounded-xl` (12px)
- **Card Shadows:** `shadow-xs` hoặc `shadow-sm` tinh tế, viền mảnh `border border-slate-200/80 dark:border-slate-800`.
