import { chromium } from 'playwright';
import fs from 'node:fs';

const screenshotDir = 'C:/Users/Hi/.gemini/antigravity/brain/a961b8df-6f94-48c7-9e61-71fc8f2c6e73/admin_screenshots';
if (!fs.existsSync(screenshotDir)) {
  fs.mkdirSync(screenshotDir, { recursive: true });
}

(async () => {
  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext({
    viewport: { width: 1440, height: 900 }
  });
  const page = await context.newPage();

  page.on('console', msg => console.log('PAGE LOG:', msg.text()));
  page.on('pageerror', err => console.log('PAGE ERROR:', err.message));

  console.log('1. Checking Root & Forbidden redirection...');
  await page.goto('http://127.0.0.1:3000/');
  console.log('Current URL after /:', page.url());

  // Check /forbidden page
  await page.goto('http://127.0.0.1:3000/forbidden');
  await page.waitForSelector('#forbidden-page', { timeout: 5000 });
  await page.screenshot({ path: `${screenshotDir}/01_forbidden_page.png` });
  console.log('Forbidden page verified.');

  console.log('2. Emulating SystemAdmin session in sessionStorage...');
  await page.evaluate(() => {
    const expiresAt = new Date(Date.now() + 24 * 3600 * 1000).toISOString();
    sessionStorage.setItem('spendwise_access_token', 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJ1c3ItYWRtaW4iLCJyb2xlIjoiU3lzdGVtQWRtaW4iLCJleHAiOjk5OTk5OTk5OTl9.signature');
    sessionStorage.setItem('spendwise_refresh_token', 'fake_refresh_token');
    sessionStorage.setItem('spendwise_access_token_expires_at', expiresAt);
    sessionStorage.setItem('spendwise_user_profile', JSON.stringify({
      id: 'usr-admin-master',
      email: 'admin@spendwise.vn',
      fullName: 'Trần Minh Quân',
      role: 'SystemAdmin',
      phone: '0981234567',
      isEmailVerified: true,
      isPhoneVerified: true,
      hasSecurityPin: true,
      requiresPasswordChange: false,
      biometricEnrolled: true,
      kycStatus: 'Verified',
      status: 'Active'
    }));
  });

  console.log('3. Navigating to /admin overview...');
  await page.goto('http://127.0.0.1:3000/admin');
  await page.waitForSelector('[data-testid="sw-admin-desktop-sidebar"]', { timeout: 5000 });
  await page.screenshot({ path: `${screenshotDir}/02_overview_light.png` });
  console.log('Overview Light captured.');

  // Click Dark Mode Toggle
  const themeToggle = page.locator('button[aria-label*="Giao diện"], button[aria-label*="Theme"], button:has(svg.lucide-moon), button:has(svg.lucide-sun)').first();
  if (await themeToggle.count() > 0) {
    await themeToggle.click();
    await page.waitForTimeout(500);
    await page.screenshot({ path: `${screenshotDir}/02_overview_dark.png` });
    console.log('Overview Dark captured.');
    // Toggle back to light
    await themeToggle.click();
    await page.waitForTimeout(500);
  }

  // Navigate to Users
  console.log('4. Navigating to Users...');
  await page.locator('[data-testid="sw-admin-nav-users"]').click();
  await page.waitForTimeout(500);
  await page.screenshot({ path: `${screenshotDir}/03_users_section.png` });
  console.log('Users Section captured.');

  // Navigate to Transactions & Ledger
  console.log('5. Navigating to Transactions & Ledger...');
  await page.locator('[data-testid="sw-admin-nav-transactions"]').click();
  await page.waitForTimeout(500);
  await page.screenshot({ path: `${screenshotDir}/04_transactions_ledger.png` });
  console.log('Transactions & Ledger captured.');

  // Navigate to Support & Complaints
  console.log('6. Navigating to Support...');
  await page.locator('[data-testid="sw-admin-nav-support"]').click();
  await page.waitForTimeout(500);
  await page.screenshot({ path: `${screenshotDir}/05_support_section.png` });
  console.log('Support Section captured.');

  // Navigate to System Operations
  console.log('7. Navigating to System Operations...');
  await page.locator('[data-testid="sw-admin-nav-system"]').click();
  await page.waitForTimeout(500);
  await page.screenshot({ path: `${screenshotDir}/06_system_operations.png` });
  console.log('System Operations captured.');

  await browser.close();
  console.log('ALL SCREENSHOTS CAPTURED PERFECTLY!');
})();
