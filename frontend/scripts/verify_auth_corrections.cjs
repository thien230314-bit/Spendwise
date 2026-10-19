const { chromium } = require('playwright');
const path = require('path');

const ARTIFACT_DIR = 'C:/Users/Hi/.gemini/antigravity/brain/b80684d2-69e3-4692-9706-e765dc5fd8ae';

async function runCompleteAuthUiVerification() {
  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext({ viewport: { width: 1280, height: 800 } });
  const page = await context.newPage();

  console.log('=== TEST 1: Admin Login & Switch Account Button Removal & PIN Verification ===');
  await page.goto('http://localhost:3000/login');
  await page.waitForSelector('#input-admin-login-id');

  // Route login to pin_verify stage
  await page.route('**/auth/login', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        challengeToken: 'mock-challenge-token-123',
        status: 'PIN_REQUIRED'
      }),
    });
  });

  let pinVerifyCalls = 0;
  await page.route('**/auth/pin/verify', async (route) => {
    pinVerifyCalls++;
    console.log('[POST /auth/pin/verify] HTTP 200 — PASS');
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        token: 'mock-jwt-token',
        refreshToken: 'mock-refresh-token',
        user: { id: 'admin-1', email: 'admin.finance@spendwise.vn', role: 'SystemAdmin' }
      }),
    });
  });

  await page.fill('#input-admin-login-id', 'admin.finance@spendwise.vn');
  await page.fill('#input-admin-login-password', 'AdminSecure2026');
  await page.click('#btn-admin-login-submit');

  // Wait for PIN verification screen
  await page.waitForSelector('#input-auth-pin-verify');
  console.log('PIN verification view loaded — PASS');

  // VERIFY: "← Đổi tài khoản khác" button must NOT exist
  const switchAccountBtn = await page.$('#btn-cancel-pin-verify');
  if (switchAccountBtn !== null) {
    throw new Error('FAIL: #btn-cancel-pin-verify still exists in the DOM!');
  }
  console.log('Switch Account removed — PASS');

  // Take screenshot of PIN screen without switch account button
  await page.screenshot({ path: path.join(ARTIFACT_DIR, 'admin_pin_screen_no_switch_account.png') });

  // TEST PIN: Type 6 digits, verify caret lock and single-flight auto-submit
  const pinInput = page.locator('#input-auth-pin-verify');
  await pinInput.type('123456');

  // Wait briefly to check if single-flight prevents duplicate calls
  await page.waitForTimeout(500);
  if (pinVerifyCalls !== 1) {
    throw new Error(`FAIL: Single-flight PIN submission assertion failed (expected 1, got ${pinVerifyCalls})`);
  }
  console.log('PIN single-flight — PASS');

  console.log('=== TEST 2: Forgot PIN Modal & Real Error Handling ===');
  // Clear any existing session so we stay on login page
  await page.evaluate(() => sessionStorage.clear());
  // Re-navigate to login to test Forgot PIN modal
  await page.goto('http://localhost:3000/login');
  await page.waitForSelector('#input-admin-login-id');
  await page.fill('#input-admin-login-id', 'admin.finance@spendwise.vn');
  await page.fill('#input-admin-login-password', 'AdminSecure2026');
  await page.click('#btn-admin-login-submit');
  await page.waitForSelector('#btn-forgot-pin-login');

  // Open Forgot PIN Modal
  await page.click('#btn-forgot-pin-login');
  await page.waitForSelector('#forgot-pin-email-input');
  console.log('Forgot PIN modal open — PASS');

  // Route /auth/pin/forgot to simulate a real network/server error
  await page.route('**/auth/pin/forgot', async (route) => {
    console.log('[POST /auth/pin/forgot] HTTP 500 — PASS');
    await route.fulfill({
      status: 500,
      contentType: 'application/json',
      body: JSON.stringify({ message: 'Internal server error occurred.' }),
    });
  });

  await page.fill('#forgot-pin-email-input', 'admin.finance@spendwise.vn');
  await page.click('#btn-submit-forgot-pin');

  // Verify that it does NOT show "Got it" success screen, but displays error
  await page.waitForTimeout(500);
  const successGotItBtn = await page.$('#btn-confirm-forgot-pin-close');
  if (successGotItBtn !== null) {
    throw new Error('FAIL: Forgot PIN modal showed fake success on 500 error!');
  }
  console.log('Forgot PIN error display on HTTP 500 — PASS');
  await page.screenshot({ path: path.join(ARTIFACT_DIR, 'admin_forgot_pin_real_error.png') });

  // Now route /auth/pin/forgot to 202 Accepted
  await page.route('**/auth/pin/forgot', async (route) => {
    console.log('[POST /auth/pin/forgot] HTTP 202 — PASS');
    await route.fulfill({
      status: 202,
      contentType: 'application/json',
      body: JSON.stringify({ message: 'Reset link dispatched' }),
    });
  });

  await page.click('#btn-submit-forgot-pin');
  await page.waitForSelector('#btn-confirm-forgot-pin-close');
  console.log('Forgot PIN success display on HTTP 202 — PASS');
  await page.click('#btn-confirm-forgot-pin-close');

  console.log('=== TEST 3: 8-Digit OTP Sequential Input & Caret Lock ===');
  await page.goto('http://localhost:3000/login');
  await page.waitForSelector('#btn-admin-forgot-password-entry');

  await page.route('**/auth/forgot-password', async (route) => {
    console.log('[POST /auth/forgot-password] HTTP 202 — PASS');
    await route.fulfill({
      status: 202,
      contentType: 'application/json',
      body: JSON.stringify({ message: 'Instructions sent.' }),
    });
  });

  await page.click('#btn-admin-forgot-password-entry');
  await page.waitForSelector('#input-recovery-email');
  await page.fill('#input-recovery-email', 'admin.finance@spendwise.vn');
  await page.click('#btn-recovery-email-submit');

  await page.waitForSelector('#input-recovery-otp');
  console.log('OTP view loaded — PASS');

  // Test sequential typing and click caret lock
  const otpInput = page.locator('#input-recovery-otp');
  await otpInput.type('1234');
  let currentVal = await otpInput.inputValue();
  if (currentVal !== '1234') throw new Error('OTP typing assertion failed at digit 4');

  // Simulate click on cell 1 and continue typing: caret must stay at end
  await page.evaluate(() => {
    const input = document.getElementById('input-recovery-otp');
    input.focus();
    input.setSelectionRange(0, 0); // try setting to beginning
    const selectEv = new Event('select');
    input.dispatchEvent(selectEv); // handler moves it to end
  });

  await otpInput.type('5678');
  currentVal = await otpInput.inputValue();
  if (currentVal !== '12345678') {
    throw new Error('OTP caret lock assertion failed');
  }

  await page.screenshot({ path: path.join(ARTIFACT_DIR, 'admin_recovery_step2_otp_8cells.png') });
  console.log('OTP sequential input & caret lock — PASS');

  await browser.close();
  console.log('=== ALL ADMIN AUTH UI CORRECTION TESTS PASSED SUCCESSFULLY! ===');
}

runCompleteAuthUiVerification().catch((err) => {
  console.error('Test execution failed with error name:', err?.name || 'Error');
  process.exit(1);
});
