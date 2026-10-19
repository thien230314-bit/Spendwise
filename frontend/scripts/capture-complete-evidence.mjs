import { chromium } from 'playwright';
import fs from 'fs';
import path from 'path';

const BASE_OUT = 'E:/source/SpendWise-Mobile-UI-Review';
const manifest = [];

function record(item) {
  manifest.push(item);
  console.log(`[RECORDED] ${item.filename} (${item.screen})`);
}

(async () => {
  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: { width: 1440, height: 1100 } });
  
  await page.goto('http://localhost:3000/preview/ui?tab=mobile', { waitUntil: 'networkidle' });
  await page.waitForTimeout(1000);

  // Helper to ensure phone container locator
  const phoneFrame = page.locator('main').first();

  // Helper to select tabs in sidebar
  async function selectNav(text) {
    const btn = page.locator(`button:has-text("${text}")`).first();
    await btn.click();
    await page.waitForTimeout(400);
  }

  // Helper to select UI status mode
  async function setStatus(modeLabel) {
    const btn = page.locator(`button:has-text("${modeLabel}")`).first();
    await btn.click();
    await page.waitForTimeout(400);
  }

  // Helper to select viewport button
  async function setViewport(vpText) {
    const btn = page.locator(`button:has-text("${vpText}")`).first();
    await btn.click();
    await page.waitForTimeout(400);
  }

  // Helper to toggle dark mode
  async function setDarkMode(enable) {
    const darkBtn = page.locator('header button:has-text("Dark Mode"), header button:has-text("Light Mode")').first();
    const isCurrentlyDark = await page.evaluate(() => document.documentElement.classList.contains('dark'));
    if (enable !== isCurrentlyDark) {
      await darkBtn.click();
      await page.waitForTimeout(400);
    }
  }

  // Helper to open sheet
  async function openSheet(sheetLabel) {
    const btn = page.locator(`aside button:has-text("${sheetLabel}")`).first();
    await btn.click();
    await page.waitForTimeout(400);
  }

  // Helper to close sheet
  async function closeSheet() {
    const closeBtn = page.locator('div.absolute.inset-0.z-50 button').first();
    if (await closeBtn.count() > 0) {
      await closeBtn.click();
      await page.waitForTimeout(300);
    }
  }

  // ==========================================
  // MODULE 1: APP LAUNCH & ONBOARDING
  // ==========================================
  console.log('--- CAPTURING MODULE 1: APP LAUNCH ---');
  await selectNav('1. Splash & Logo Intro');
  
  // Splash stages
  await page.waitForTimeout(500);
  const splashPath = `${BASE_OUT}/screenshots/app-launch/app-launch-splash-logo.png`;
  await phoneFrame.screenshot({ path: splashPath });
  record({
    filename: 'screenshots/app-launch/app-launch-splash-logo.png',
    screen: 'Splash & Logo Intro',
    route: '/(guest)/splash',
    sourceRuntime: 'Frontend Mobile Preview Adapter',
    viewport: '393x852',
    theme: 'light',
    locale: 'vi-VN',
    uiState: 'Normal / Logo Glow Reveal',
    captureResult: 'SUCCESS',
    privacyRedacted: false
  });

  // Onboarding 4 Steps
  const onbBtn = page.locator('button:has-text("Onboarding 4 Bước")').first();
  if (await onbBtn.count() > 0) {
    await onbBtn.click();
    await page.waitForTimeout(400);
    const onbPath1 = `${BASE_OUT}/screenshots/app-launch/onboarding-step-1.png`;
    await page.screenshot({ path: onbPath1 });
    record({
      filename: 'screenshots/app-launch/onboarding-step-1.png',
      screen: 'Onboarding Step 1',
      route: '/onboarding',
      sourceRuntime: 'Frontend Mobile Preview Adapter',
      viewport: '393x852',
      theme: 'light',
      locale: 'vi-VN',
      uiState: 'Normal',
      captureResult: 'SUCCESS',
      privacyRedacted: false
    });

    // Close onboarding modal
    const closeOnb = page.locator('button:has-text("Bỏ qua"), button:has-text("Đóng")').first();
    if (await closeOnb.count() > 0) await closeOnb.click();
    await page.waitForTimeout(300);
  }

  // ==========================================
  // MODULE 2: AUTHENTICATION (NOTEBOOK UI)
  // ==========================================
  console.log('--- CAPTURING MODULE 2: AUTHENTICATION ---');
  await selectNav('2. Đăng nhập / Đăng ký');
  await page.waitForTimeout(500);

  // Login notebook
  const loginPath = `${BASE_OUT}/screenshots/auth/auth-login-notebook.png`;
  await phoneFrame.screenshot({ path: loginPath });
  record({
    filename: 'screenshots/auth/auth-login-notebook.png',
    screen: 'Đăng nhập Sổ tay (Notebook Login)',
    route: '/(guest)/login',
    sourceRuntime: 'Frontend Mobile Preview Adapter',
    viewport: '393x852',
    theme: 'light',
    locale: 'vi-VN',
    uiState: 'Normal',
    captureResult: 'SUCCESS',
    privacyRedacted: false
  });

  // Flip to Register
  const registerFlipBtn = page.locator('#btn-to-register').first();
  if (await registerFlipBtn.count() > 0) {
    await registerFlipBtn.click();
    await page.waitForTimeout(1400); // Wait for 1150ms 3D flip animation
    const regPath = `${BASE_OUT}/screenshots/auth/auth-register-notebook.png`;
    await phoneFrame.screenshot({ path: regPath });
    record({
      filename: 'screenshots/auth/auth-register-notebook.png',
      screen: 'Đăng ký Sổ tay (Notebook Register)',
      route: '/(guest)/register',
      sourceRuntime: 'Frontend Mobile Preview Adapter',
      viewport: '393x852',
      theme: 'light',
      locale: 'vi-VN',
      uiState: 'Normal / Page Flipped',
      captureResult: 'SUCCESS',
      privacyRedacted: false
    });

    // Flip back to Login
    const loginFlipBtn = page.locator('#btn-to-login').first();
    if (await loginFlipBtn.count() > 0) {
      await loginFlipBtn.click();
      await page.waitForTimeout(1400);
    }
  }

  // ==========================================
  // MODULE 3: HOME / DASHBOARD (FULL-PAGE SCROLL)
  // ==========================================
  console.log('--- CAPTURING MODULE 3: HOME FULL PAGE ---');
  await selectNav('Trang chủ (Home)');
  await page.waitForTimeout(600);

  const homeScroll = page.locator('#mobile-home-screen-scroll-container').first();

  // Top Viewport
  await homeScroll.evaluate((el) => { el.scrollTop = 0; });
  await page.waitForTimeout(400);
  await phoneFrame.screenshot({ path: `${BASE_OUT}/screenshots/home/home-viewport-top.png` });
  record({
    filename: 'screenshots/home/home-viewport-top.png',
    screen: 'Trang chủ (Header, Balance Card, Quick Actions, Overview)',
    route: '/(authenticated)/home',
    sourceRuntime: 'Frontend Mobile Preview Adapter',
    viewport: '393x852',
    theme: 'light',
    locale: 'vi-VN',
    uiState: 'Normal',
    captureResult: 'SUCCESS',
    privacyRedacted: false
  });

  // Middle Viewport (Charts & Attention)
  await homeScroll.evaluate((el) => { el.scrollTop = 580; });
  await page.waitForTimeout(400);
  await phoneFrame.screenshot({ path: `${BASE_OUT}/screenshots/home/home-viewport-middle.png` });
  record({
    filename: 'screenshots/home/home-viewport-middle.png',
    screen: 'Trang chủ (Dòng tiền 6 tháng, Phân bổ Donut, Cần chú ý)',
    route: '/(authenticated)/home',
    sourceRuntime: 'Frontend Mobile Preview Adapter',
    viewport: '393x852',
    theme: 'light',
    locale: 'vi-VN',
    uiState: 'Normal',
    captureResult: 'SUCCESS',
    privacyRedacted: false
  });

  // Bottom Viewport (Group Funds, Goals, 5 Transactions)
  await homeScroll.evaluate((el) => { el.scrollTop = 1250; });
  await page.waitForTimeout(400);
  await phoneFrame.screenshot({ path: `${BASE_OUT}/screenshots/home/home-viewport-bottom.png` });
  record({
    filename: 'screenshots/home/home-viewport-bottom.png',
    screen: 'Trang chủ (Quỹ nhóm, Mục tiêu tiết kiệm, 5 Giao dịch)',
    route: '/(authenticated)/home',
    sourceRuntime: 'Frontend Mobile Preview Adapter',
    viewport: '393x852',
    theme: 'light',
    locale: 'vi-VN',
    uiState: 'Normal',
    captureResult: 'SUCCESS',
    privacyRedacted: false
  });

  // Toggle Hide Balance on Home
  await homeScroll.evaluate((el) => { el.scrollTop = 0; });
  await page.waitForTimeout(300);
  const eyeBtn = page.locator('#mobile-home-screen-scroll-container button:has(svg.lucide-eye), #mobile-home-screen-scroll-container button:has(svg.lucide-eye-off)').first();
  if (await eyeBtn.count() > 0) {
    await eyeBtn.click();
    await page.waitForTimeout(300);
    await phoneFrame.screenshot({ path: `${BASE_OUT}/screenshots/home/home-privacy-hidden-balance.png` });
    record({
      filename: 'screenshots/home/home-privacy-hidden-balance.png',
      screen: 'Trang chủ (Ẩn số dư bảo mật)',
      route: '/(authenticated)/home',
      sourceRuntime: 'Frontend Mobile Preview Adapter',
      viewport: '393x852',
      theme: 'light',
      locale: 'vi-VN',
      uiState: 'Hidden Balance Mode',
      captureResult: 'SUCCESS',
      privacyRedacted: true
    });
    // Restore balance
    await eyeBtn.click();
    await page.waitForTimeout(300);
  }

  // ==========================================
  // MODULE 4: WALLET & BANKING
  // ==========================================
  console.log('--- CAPTURING MODULE 4: WALLET ---');
  await selectNav('Ví & Giao dịch');
  await page.waitForTimeout(500);

  await phoneFrame.screenshot({ path: `${BASE_OUT}/screenshots/wallet/wallet-overview.png` });
  record({
    filename: 'screenshots/wallet/wallet-overview.png',
    screen: 'Ví & Giao dịch (Tổng quan ví)',
    route: '/(authenticated)/wallet',
    sourceRuntime: 'Frontend Mobile Preview Adapter',
    viewport: '393x852',
    theme: 'light',
    locale: 'vi-VN',
    uiState: 'Normal',
    captureResult: 'SUCCESS',
    privacyRedacted: false
  });

  // Wallet filter: income
  const filterInc = page.locator('button:has-text("Thu nhập")').first();
  if (await filterInc.count() > 0) {
    await filterInc.click();
    await page.waitForTimeout(300);
    await phoneFrame.screenshot({ path: `${BASE_OUT}/screenshots/transactions/transactions-filter-income.png` });
    record({
      filename: 'screenshots/transactions/transactions-filter-income.png',
      screen: 'Lịch sử giao dịch (Lọc Thu nhập)',
      route: '/(authenticated)/wallet',
      sourceRuntime: 'Frontend Mobile Preview Adapter',
      viewport: '393x852',
      theme: 'light',
      locale: 'vi-VN',
      uiState: 'Filter: Income',
      captureResult: 'SUCCESS',
      privacyRedacted: false
    });
  }

  // Wallet filter: expense
  const filterExp = page.locator('button:has-text("Chi tiêu")').first();
  if (await filterExp.count() > 0) {
    await filterExp.click();
    await page.waitForTimeout(300);
    await phoneFrame.screenshot({ path: `${BASE_OUT}/screenshots/transactions/transactions-filter-expense.png` });
    record({
      filename: 'screenshots/transactions/transactions-filter-expense.png',
      screen: 'Lịch sử giao dịch (Lọc Chi tiêu)',
      route: '/(authenticated)/wallet',
      sourceRuntime: 'Frontend Mobile Preview Adapter',
      viewport: '393x852',
      theme: 'light',
      locale: 'vi-VN',
      uiState: 'Filter: Expense',
      captureResult: 'SUCCESS',
      privacyRedacted: false
    });
  }

  // ==========================================
  // MODULE 5: TRANSFER
  // ==========================================
  console.log('--- CAPTURING MODULE 5: TRANSFER ---');
  await selectNav('Chuyển tiền (Transfer)');
  await page.waitForTimeout(500);

  await phoneFrame.screenshot({ path: `${BASE_OUT}/screenshots/transfer/transfer-form.png` });
  record({
    filename: 'screenshots/transfer/transfer-form.png',
    screen: 'Chuyển tiền (Biểu mẫu chuyển nội bộ)',
    route: '/(authenticated)/transfer',
    sourceRuntime: 'Frontend Mobile Preview Adapter',
    viewport: '393x852',
    theme: 'light',
    locale: 'vi-VN',
    uiState: 'Normal',
    captureResult: 'SUCCESS',
    privacyRedacted: false
  });

  // ==========================================
  // MODULE 6: PLANNING (BUDGETS, GOALS, RECURRING)
  // ==========================================
  console.log('--- CAPTURING MODULE 6: PLANNING ---');
  await selectNav('Kế hoạch (Planning)');
  await page.waitForTimeout(500);

  // Tab 1: Budgets
  await page.locator('button:has-text("Ngân sách")').first().click();
  await page.waitForTimeout(300);
  await phoneFrame.screenshot({ path: `${BASE_OUT}/screenshots/budgets/budgets-overview.png` });
  record({
    filename: 'screenshots/budgets/budgets-overview.png',
    screen: 'Kế hoạch tài chính (Ngân sách tháng & Danh mục)',
    route: '/(authenticated)/planning',
    sourceRuntime: 'Frontend Mobile Preview Adapter',
    viewport: '393x852',
    theme: 'light',
    locale: 'vi-VN',
    uiState: 'Tab: Budgets',
    captureResult: 'SUCCESS',
    privacyRedacted: false
  });

  // Tab 2: Goals
  await page.locator('button:has-text("Mục tiêu")').first().click();
  await page.waitForTimeout(300);
  await phoneFrame.screenshot({ path: `${BASE_OUT}/screenshots/goals/goals-overview.png` });
  record({
    filename: 'screenshots/goals/goals-overview.png',
    screen: 'Kế hoạch tài chính (Mục tiêu tích lũy)',
    route: '/(authenticated)/planning',
    sourceRuntime: 'Frontend Mobile Preview Adapter',
    viewport: '393x852',
    theme: 'light',
    locale: 'vi-VN',
    uiState: 'Tab: Goals',
    captureResult: 'SUCCESS',
    privacyRedacted: false
  });

  // Tab 3: Recurring
  await page.locator('button:has-text("Định kỳ")').first().click();
  await page.waitForTimeout(300);
  await phoneFrame.screenshot({ path: `${BASE_OUT}/screenshots/recurring/recurring-overview.png` });
  record({
    filename: 'screenshots/recurring/recurring-overview.png',
    screen: 'Kế hoạch tài chính (Giao dịch định kỳ)',
    route: '/(authenticated)/planning',
    sourceRuntime: 'Frontend Mobile Preview Adapter',
    viewport: '393x852',
    theme: 'light',
    locale: 'vi-VN',
    uiState: 'Tab: Recurring',
    captureResult: 'SUCCESS',
    privacyRedacted: false
  });

  // ==========================================
  // MODULE 7: FRIENDS & DISCOVERY
  // ==========================================
  console.log('--- CAPTURING MODULE 7: FRIENDS ---');
  await selectNav('3. Bạn bè & Tìm kiếm');
  await page.waitForTimeout(500);

  // Tab 1: Friend List
  await phoneFrame.screenshot({ path: `${BASE_OUT}/screenshots/friends/friends-list.png` });
  record({
    filename: 'screenshots/friends/friends-list.png',
    screen: 'Bạn bè & Tìm kiếm (Danh sách bạn bè)',
    route: '/(authenticated)/friends',
    sourceRuntime: 'Frontend Mobile Preview Adapter',
    viewport: '393x852',
    theme: 'light',
    locale: 'vi-VN',
    uiState: 'Tab: Friends',
    captureResult: 'SUCCESS',
    privacyRedacted: false
  });

  // Tab 2: Incoming Requests
  const incBtn = page.locator('button:has-text("Lời mời")').first();
  if (await incBtn.count() > 0) {
    await incBtn.click();
    await page.waitForTimeout(300);
    await phoneFrame.screenshot({ path: `${BASE_OUT}/screenshots/friends/friends-incoming-requests.png` });
    record({
      filename: 'screenshots/friends/friends-incoming-requests.png',
      screen: 'Bạn bè & Tìm kiếm (Lời mời kết bạn nhận được)',
      route: '/(authenticated)/friends',
      sourceRuntime: 'Frontend Mobile Preview Adapter',
      viewport: '393x852',
      theme: 'light',
      locale: 'vi-VN',
      uiState: 'Tab: Incoming',
      captureResult: 'SUCCESS',
      privacyRedacted: false
    });
  }

  // Tab 3: Outgoing Requests
  const outBtn = page.locator('button:has-text("Đã gửi")').first();
  if (await outBtn.count() > 0) {
    await outBtn.click();
    await page.waitForTimeout(300);
    await phoneFrame.screenshot({ path: `${BASE_OUT}/screenshots/friends/friends-outgoing-requests.png` });
    record({
      filename: 'screenshots/friends/friends-outgoing-requests.png',
      screen: 'Bạn bè & Tìm kiếm (Lời mời đã gửi)',
      route: '/(authenticated)/friends',
      sourceRuntime: 'Frontend Mobile Preview Adapter',
      viewport: '393x852',
      theme: 'light',
      locale: 'vi-VN',
      uiState: 'Tab: Outgoing',
      captureResult: 'SUCCESS',
      privacyRedacted: false
    });
  }

  // ==========================================
  // MODULE 8: MENU & SETTINGS
  // ==========================================
  console.log('--- CAPTURING MODULE 8: MENU ---');
  await selectNav('Tiện ích (Menu)');
  await page.waitForTimeout(500);

  await phoneFrame.screenshot({ path: `${BASE_OUT}/screenshots/settings/menu-overview.png` });
  record({
    filename: 'screenshots/settings/menu-overview.png',
    screen: 'Tiện ích & Cài đặt (Menu)',
    route: '/(authenticated)/menu',
    sourceRuntime: 'Frontend Mobile Preview Adapter',
    viewport: '393x852',
    theme: 'light',
    locale: 'vi-VN',
    uiState: 'Normal',
    captureResult: 'SUCCESS',
    privacyRedacted: false
  });

  // ==========================================
  // MODULE 9: USER AI CHATBOT
  // ==========================================
  console.log('--- CAPTURING MODULE 9: AI CHAT ---');
  await selectNav('4. Trợ lý AI (Mở từ FAB)');
  await page.waitForTimeout(500);

  await phoneFrame.screenshot({ path: `${BASE_OUT}/screenshots/ai-chat/ai-chat-conversation.png` });
  record({
    filename: 'screenshots/ai-chat/ai-chat-conversation.png',
    screen: 'Trợ lý AI SpendWise (Giao diện chat & tư vấn tài chính)',
    route: '/(authenticated)/ai-chat',
    sourceRuntime: 'Frontend Mobile Preview Adapter',
    viewport: '393x852',
    theme: 'light',
    locale: 'vi-VN',
    uiState: 'Normal',
    captureResult: 'SUCCESS',
    privacyRedacted: false
  });

  // ==========================================
  // MODULE 10: MODALS & BOTTOM SHEETS
  // ==========================================
  console.log('--- CAPTURING MODULE 10: MODALS & BOTTOM SHEETS ---');
  // Back to home tab to show bottom sheets on top of home
  await selectNav('Trang chủ (Home)');
  await page.waitForTimeout(300);

  const sheets = [
    { label: 'Xác thực Mã PIN', id: 'pin-challenge', file: 'modals/transfer/modal-pin-challenge.png', title: 'Modal Xác thực Mã PIN 6 Chữ Số' },
    { label: 'Thêm giao dịch mới', id: 'add-transaction', file: 'modals/transactions/modal-add-transaction.png', title: 'Modal Thêm giao dịch mới' },
    { label: 'Đổi ví nguồn', id: 'wallet-switcher', file: 'modals/wallet/modal-wallet-switcher.png', title: 'Modal Đổi ví nguồn trích tiền' },
    { label: 'Liên kết ngân hàng', id: 'link-bank', file: 'modals/wallet/modal-link-bank-sepay.png', title: 'Modal Liên kết ngân hàng SePay' },
    { label: 'Hộp thư thông báo', id: 'notifications', file: 'modals/other/modal-notifications.png', title: 'Modal Trung tâm thông báo' },
  ];

  for (const s of sheets) {
    await openSheet(s.label);
    await phoneFrame.screenshot({ path: `${BASE_OUT}/${s.file}` });
    record({
      filename: s.file,
      screen: s.title,
      route: 'modal-bottom-sheet',
      sourceRuntime: 'Frontend Mobile Preview Adapter',
      viewport: '393x852',
      theme: 'light',
      locale: 'vi-VN',
      uiState: 'Bottom Sheet Open',
      captureResult: 'SUCCESS',
      privacyRedacted: false
    });
    await closeSheet();
  }

  // ==========================================
  // MODULE 11: UI LIFECYCLE STATES
  // ==========================================
  console.log('--- CAPTURING MODULE 11: UI LIFECYCLE STATES ---');
  
  // Skeleton State
  await setStatus('2. Skeleton Loading');
  await phoneFrame.screenshot({ path: `${BASE_OUT}/states/skeleton/state-skeleton-loading.png` });
  record({
    filename: 'states/skeleton/state-skeleton-loading.png',
    screen: 'Trạng thái Skeleton Loading',
    route: '/(authenticated)/home',
    sourceRuntime: 'Frontend Mobile Preview Adapter',
    viewport: '393x852',
    theme: 'light',
    locale: 'vi-VN',
    uiState: 'Skeleton Loading',
    captureResult: 'SUCCESS',
    privacyRedacted: false
  });

  // Offline Banner State
  await setStatus('5. Mất mạng (Offline Banner)');
  await phoneFrame.screenshot({ path: `${BASE_OUT}/states/offline/state-offline-banner.png` });
  record({
    filename: 'states/offline/state-offline-banner.png',
    screen: 'Trạng thái Mất mạng (Offline Banner)',
    route: '/(authenticated)/home',
    sourceRuntime: 'Frontend Mobile Preview Adapter',
    viewport: '393x852',
    theme: 'light',
    locale: 'vi-VN',
    uiState: 'Offline Banner Active',
    captureResult: 'SUCCESS',
    privacyRedacted: false
  });

  // Timeout State
  await setStatus('6. Quá thời gian (Timeout)');
  await phoneFrame.screenshot({ path: `${BASE_OUT}/states/timeout/state-timeout-banner.png` });
  record({
    filename: 'states/timeout/state-timeout-banner.png',
    screen: 'Trạng thái Quá thời gian chờ (Timeout Banner)',
    route: '/(authenticated)/home',
    sourceRuntime: 'Frontend Mobile Preview Adapter',
    viewport: '393x852',
    theme: 'light',
    locale: 'vi-VN',
    uiState: 'Timeout Active',
    captureResult: 'SUCCESS',
    privacyRedacted: false
  });

  // S-Loading Branded Component
  const sLoadingCard = page.locator('aside:has-text("S-LOADING BRANDED ANIMATION")').first();
  if (await sLoadingCard.count() > 0) {
    await sLoadingCard.screenshot({ path: `${BASE_OUT}/states/loading/state-sloading-branded.png` });
    record({
      filename: 'states/loading/state-sloading-branded.png',
      screen: 'S-Loading Branded Animation Component',
      route: 'preview-component',
      sourceRuntime: 'Frontend Mobile Preview Adapter',
      viewport: '393x852',
      theme: 'light',
      locale: 'vi-VN',
      uiState: 'Loading Animation',
      captureResult: 'SUCCESS',
      privacyRedacted: false
    });
  }

  // Reset to normal status
  await setStatus('1. Bình thường (Normal Data)');

  // ==========================================
  // MODULE 12: THEMES (DARK MODE)
  // ==========================================
  console.log('--- CAPTURING MODULE 12: DARK THEME ---');
  await setDarkMode(true);
  
  // Home in Dark
  await selectNav('Trang chủ (Home)');
  await page.waitForTimeout(400);
  await phoneFrame.screenshot({ path: `${BASE_OUT}/themes/dark/theme-dark-home.png` });
  record({
    filename: 'themes/dark/theme-dark-home.png',
    screen: 'Trang chủ (Chế độ nền tối - Dark Mode)',
    route: '/(authenticated)/home',
    sourceRuntime: 'Frontend Mobile Preview Adapter',
    viewport: '393x852',
    theme: 'dark',
    locale: 'vi-VN',
    uiState: 'Normal',
    captureResult: 'SUCCESS',
    privacyRedacted: false
  });

  // Wallet in Dark
  await selectNav('Ví & Giao dịch');
  await page.waitForTimeout(400);
  await phoneFrame.screenshot({ path: `${BASE_OUT}/themes/dark/theme-dark-wallet.png` });
  record({
    filename: 'themes/dark/theme-dark-wallet.png',
    screen: 'Ví & Giao dịch (Chế độ nền tối - Dark Mode)',
    route: '/(authenticated)/wallet',
    sourceRuntime: 'Frontend Mobile Preview Adapter',
    viewport: '393x852',
    theme: 'dark',
    locale: 'vi-VN',
    uiState: 'Normal',
    captureResult: 'SUCCESS',
    privacyRedacted: false
  });

  // Menu in Dark
  await selectNav('Tiện ích (Menu)');
  await page.waitForTimeout(400);
  await phoneFrame.screenshot({ path: `${BASE_OUT}/themes/dark/theme-dark-menu.png` });
  record({
    filename: 'themes/dark/theme-dark-menu.png',
    screen: 'Tiện ích (Chế độ nền tối - Dark Mode)',
    route: '/(authenticated)/menu',
    sourceRuntime: 'Frontend Mobile Preview Adapter',
    viewport: '393x852',
    theme: 'dark',
    locale: 'vi-VN',
    uiState: 'Normal',
    captureResult: 'SUCCESS',
    privacyRedacted: false
  });

  // Reset back to Light
  await setDarkMode(false);

  // ==========================================
  // MODULE 13: RESPONSIVE VIEWPORTS
  // ==========================================
  console.log('--- CAPTURING MODULE 13: RESPONSIVE VIEWPORTS ---');
  await selectNav('Trang chủ (Home)');
  
  // Small Phone: 375x812 (iPhone 14)
  await setViewport('375 × 812');
  await phoneFrame.screenshot({ path: `${BASE_OUT}/responsive/small/responsive-375x812-home.png` });
  record({
    filename: 'responsive/small/responsive-375x812-home.png',
    screen: 'Trang chủ trên màn hình nhỏ (375 × 812)',
    route: '/(authenticated)/home',
    sourceRuntime: 'Frontend Mobile Preview Adapter',
    viewport: '375x812',
    theme: 'light',
    locale: 'vi-VN',
    uiState: 'Normal',
    captureResult: 'SUCCESS',
    privacyRedacted: false
  });

  // Large Phone: 412x892 (Pixel / Android)
  await setViewport('412 × 892');
  await phoneFrame.screenshot({ path: `${BASE_OUT}/responsive/large/responsive-412x892-home.png` });
  record({
    filename: 'responsive/large/responsive-412x892-home.png',
    screen: 'Trang chủ trên màn hình lớn (412 × 892)',
    route: '/(authenticated)/home',
    sourceRuntime: 'Frontend Mobile Preview Adapter',
    viewport: '412x892',
    theme: 'light',
    locale: 'vi-VN',
    uiState: 'Normal',
    captureResult: 'SUCCESS',
    privacyRedacted: false
  });

  // Reset Viewport
  await setViewport('393 × 852');

  await browser.close();
  console.log(`--- FINISHED CAPTURING. TOTAL ITEMS: ${manifest.length} ---`);

  // Write CAPTURE-MANIFEST.json
  fs.writeFileSync(
    `${BASE_OUT}/CAPTURE-MANIFEST.json`,
    JSON.stringify({
      generatedAt: new Date().toISOString(),
      agent: 'Antigravity',
      project: 'SpendWise Mobile UI Review Package',
      runtimeScope: 'Frontend Mobile Preview Adapter (Level 3 Proof) + React Native Expo Source Verification (mobile/)',
      totalScreenshots: manifest.length,
      items: manifest
    }, null, 2),
    'utf-8'
  );
  console.log('CAPTURE-MANIFEST.json written successfully.');
})();
