const { chromium } = require('playwright');
const path = require('path');

const ARTIFACT_DIR = 'C:/Users/Hi/.gemini/antigravity/brain/b80684d2-69e3-4692-9706-e765dc5fd8ae';

async function runSessionBootstrapVerification() {
  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext({ viewport: { width: 1280, height: 800 } });
  const page = await context.newPage();

  let refreshEndpointCalls = 0;
  let usersMeEndpointCalls = 0;

  page.on('request', req => {
    console.log(`[REQ] ${req.method()} ${req.url()}`);
  });
  page.on('console', msg => console.log(`[BROWSER] ${msg.type()}: ${msg.text()}`));

  // Mock generic backend API endpoints so unauthenticated background polls don't crash
  await page.route('**/api/**', async (route) => {
    const url = route.request().url();
    if (url.includes('/auth/refresh') || url.includes('/users/me')) {
      await route.fallback();
      return;
    }
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ items: [], totalCount: 0 })
    });
  });

  // Intercept network routes to monitor requests
  await page.route('**/users/me', async (route) => {
    usersMeEndpointCalls++;
    console.log('[GET users/me] intercepted (Role Check) — HTTP 403 user_role_required');
    await route.fulfill({
      status: 403,
      contentType: 'application/json',
      body: JSON.stringify({
        code: 'user_role_required',
        status: 403,
        title: 'Forbidden',
        detail: 'This endpoint is restricted to regular users.'
      })
    });
  });

  await page.route('**/auth/refresh', async (route) => {
    refreshEndpointCalls++;
    console.log('[POST /auth/refresh] intercepted (Single-flight) — HTTP 200');
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        accessToken: 'mock-rotated-access-token',
        refreshToken: 'mock-rotated-refresh-token',
        accessTokenExpiresAt: new Date(Date.now() + 3600 * 1000).toISOString(),
        user: {
          id: 'admin-session-id',
          fullName: 'System Administrator',
          email: 'admin.finance@spendwise.vn',
          spendId: 'ADMIN-001',
          systemRole: 'SystemAdmin',
          status: 'active',
          mustChangePassword: false,
          preferredLanguage: 'vi-VN',
          preferredTheme: 'light',
          defaultCurrency: 'VND',
          createdAt: '2026-01-01T00:00:00Z',
          engagementEmailOptIn: false
        }
      })
    });
  });

  console.log('=== TEST 1: Direct navigation to /admin/transactions with cached SystemAdmin session ===');
  
  // Set session storage with SystemAdmin credentials
  await page.goto('http://localhost:3000/login');
  await page.evaluate(() => {
    sessionStorage.setItem('spendwise_access_token', 'initial-admin-access-token');
    sessionStorage.setItem('spendwise_refresh_token', 'initial-admin-refresh-token');
    sessionStorage.setItem('spendwise_user_profile', JSON.stringify({
      id: 'admin-session-id',
      name: 'System Administrator',
      email: 'admin.finance@spendwise.vn',
      role: 'SystemAdmin',
      language: 'vi-VN',
      currency: 'VND',
      avatarInitials: 'SA',
      status: 'active',
      isLocked: false,
      mustChangePassword: false,
      createdAt: '2026-01-01T00:00:00Z',
      engagementEmailOptIn: false
    }));
  });

  // Navigate directly to /admin/transactions
  await page.goto('http://localhost:3000/admin/transactions');
  await page.waitForTimeout(1000);

  const currentUrl1 = page.url();
  console.log('Current URL after direct navigation:', currentUrl1);
  if (!currentUrl1.includes('/admin/transactions')) {
    throw new Error(`Direct navigation failed. Expected /admin/transactions but got ${currentUrl1}`);
  }
  console.log('Direct navigation retained /admin/transactions — PASS');

  // Verify GET /api/users/me was NOT called because cached user is SystemAdmin
  if (usersMeEndpointCalls > 0) {
    throw new Error(`Regression: GET /api/users/me was called ${usersMeEndpointCalls} times for SystemAdmin!`);
  }
  console.log('Zero calls to /api/users/me for SystemAdmin — PASS');

  console.log('=== TEST 2: Page reload at /admin/transactions ===');
  const initialRefreshCalls = refreshEndpointCalls;
  await page.reload();
  await page.waitForTimeout(1000);

  const currentUrl2 = page.url();
  console.log('Current URL after reload:', currentUrl2);
  if (!currentUrl2.includes('/admin/transactions')) {
    throw new Error(`Reload failed. Expected /admin/transactions but got ${currentUrl2}`);
  }
  console.log('Reload retained /admin/transactions — PASS');

  // StrictMode single-flight refresh verification
  const newRefreshCalls = refreshEndpointCalls - initialRefreshCalls;
  console.log(`Refresh calls triggered during reload: ${newRefreshCalls}`);
  if (newRefreshCalls > 1) {
    throw new Error(`Single-flight failed: Expected at most 1 refresh call during bootstrap, but got ${newRefreshCalls}`);
  }
  console.log('Single-flight token refresh maintained under StrictMode — PASS');

  // Take screenshot of protected admin page
  const screenshotPath = path.join(ARTIFACT_DIR, 'admin_transactions_bootstrap_success.png');
  await page.screenshot({ path: screenshotPath, fullPage: true });
  console.log(`Screenshot saved to ${screenshotPath}`);

  console.log('=== TEST 3: Regular User bootstrap invokes /users/me ===');
  usersMeEndpointCalls = 0;
  
  // Unroute the initial 403 mock and set 200 mock for regular user
  await page.unroute('**/api/users/me');
  await page.route('**/api/users/me', async (route) => {
    usersMeEndpointCalls++;
    console.log('[GET /api/users/me] regular user call — HTTP 200');
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        id: 'regular-user-id',
        fullName: 'Regular User',
        email: 'user@spendwise.vn',
        spendId: 'USER-001',
        systemRole: 'User',
        status: 'active',
        mustChangePassword: false,
        preferredLanguage: 'vi-VN',
        preferredTheme: 'light',
        defaultCurrency: 'VND',
        createdAt: '2026-01-01T00:00:00Z',
        engagementEmailOptIn: false
      })
    });
  });

  // Navigate to login with user credentials stored
  await page.goto('http://localhost:3000/login');
  await page.evaluate(() => {
    sessionStorage.setItem('spendwise_access_token', 'user-access-token');
    sessionStorage.setItem('spendwise_refresh_token', 'user-refresh-token');
    sessionStorage.setItem('spendwise_user_profile', JSON.stringify({
      id: 'regular-user-id',
      name: 'Regular User',
      email: 'user@spendwise.vn',
      role: 'User',
      language: 'vi-VN',
      currency: 'VND',
      avatarInitials: 'RU',
      status: 'active',
      isLocked: false,
      mustChangePassword: false,
      createdAt: '2026-01-01T00:00:00Z',
      engagementEmailOptIn: false
    }));
  });

  // Reload so AuthProvider init runs with User role in sessionStorage
  await page.reload();
  await page.waitForTimeout(1000);
  console.log(`GET /api/users/me calls for User role: ${usersMeEndpointCalls}`);
  if (usersMeEndpointCalls === 0) {
    throw new Error('Expected GET /api/users/me to be called for regular User role!');
  }
  console.log('User role successfully queries /api/users/me — PASS');

  await browser.close();
  console.log('=== ALL BOOTSTRAP VERIFICATION TESTS PASSED (SANITIZED) ===');
}

runSessionBootstrapVerification().catch((err) => {
  console.error('Test execution failed:', err);
  process.exit(1);
});

