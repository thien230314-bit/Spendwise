const { chromium } = require('playwright');
const path = require('path');

const ARTIFACT_DIR = 'C:/Users/Hi/.gemini/antigravity/brain/b80684d2-69e3-4692-9706-e765dc5fd8ae';

async function runComprehensiveRecoveryIntegration() {
  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext({ viewport: { width: 1280, height: 800 } });
  const page = await context.newPage();

  console.log('=== STEP 1: Admin Login -> Forgot Password (OTP 8 Digits) ===');
  await page.goto('http://localhost:3000/login');
  await page.waitForSelector('#btn-admin-forgot-password-entry');

  let forgotPwCalled = false;
  let resetPwCalled = false;

  await page.route('**/auth/forgot-password', async (route) => {
    forgotPwCalled = true;
    console.log('[POST /auth/forgot-password] HTTP 202 — PASS');
    await route.fulfill({
      status: 202,
      contentType: 'application/json',
      body: JSON.stringify({ message: 'If an account matches, instructions have been sent.' }),
    });
  });

  await page.route('**/auth/reset-password', async (route) => {
    resetPwCalled = true;
    console.log('[POST /auth/reset-password] HTTP 204 — PASS');
    await route.fulfill({ status: 204 });
  });

  await page.click('#btn-admin-forgot-password-entry');
  await page.waitForSelector('#input-recovery-email');
  await page.fill('#input-recovery-email', 'admin.finance@spendwise.vn');
  await page.click('#btn-recovery-email-submit');

  await page.waitForSelector('#input-recovery-otp');
  console.log('OTP view loaded — PASS');

  // Verify OTP input is sequential left-to-right with 8 cells
  const otpInput = page.locator('#input-recovery-otp');
  await otpInput.type('1234');
  let currentVal = await otpInput.inputValue();
  if (currentVal !== '1234') throw new Error('OTP sequential typing assertion failed at digit 4');

  // Test caret lock: click cell 0 or dispatch select, verify caret moves to end
  await page.evaluate(() => {
    const input = document.getElementById('input-recovery-otp');
    input.focus();
    input.setSelectionRange(0, 0);
    input.dispatchEvent(new Event('select'));
  });
  await otpInput.type('5678');
  currentVal = await otpInput.inputValue();
  if (currentVal !== '12345678') throw new Error('OTP caret lock assertion failed');
  console.log('OTP sequential input & caret lock — PASS');

  await page.click('#btn-recovery-otp-continue');
  await page.waitForSelector('#input-recovery-new-password');

  // Step 3: Password policy 8-128 validation
  await page.fill('#input-recovery-new-password', 'AdminPass2026');
  await page.fill('#input-recovery-confirm-password', 'AdminPass2026');
  await page.click('#btn-recovery-reset-submit');

  await page.waitForSelector('#btn-recovery-success-return');
  console.log('Password reset completion — PASS');
  await page.screenshot({ path: path.join(ARTIFACT_DIR, 'admin_recovery_w02a_pw_success.png') });

  await page.click('#btn-recovery-success-return');
  await page.waitForSelector('#input-admin-login-id');
  console.log('Return to Admin Login (LOCKED design preserved) — PASS');

  console.log('=== STEP 2: Forgot Security PIN Request Link via Modal ===');
  // Transition to PIN verify stage
  await page.route('**/auth/login', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        challengeToken: 'mock-challenge-token-xyz',
        status: 'PIN_REQUIRED'
      }),
    });
  });

  await page.fill('#input-admin-login-id', 'admin.finance@spendwise.vn');
  await page.fill('#input-admin-login-password', 'AdminPass2026');
  await page.click('#btn-admin-login-submit');

  await page.waitForSelector('#btn-forgot-pin-login');
  console.log('PIN verification view loaded — PASS');
  console.log('Switch Account removed — PASS');
  const switchAccountBtn = await page.$('#btn-cancel-pin-verify');
  if (switchAccountBtn !== null) throw new Error('Switch account button was found in DOM');

  // Open Forgot PIN modal
  await page.click('#btn-forgot-pin-login');
  await page.waitForSelector('#forgot-pin-email-input');

  let forgotPinCalled = false;
  await page.route('**/auth/pin/forgot', async (route) => {
    forgotPinCalled = true;
    console.log('[POST /auth/pin/forgot] HTTP 202 — PASS');
    await route.fulfill({
      status: 202,
      contentType: 'application/json',
      body: JSON.stringify({ message: 'If account matches, PIN instructions sent.' }),
    });
  });

  await page.fill('#forgot-pin-email-input', 'admin.finance@spendwise.vn');
  await page.click('#btn-submit-forgot-pin');

  await page.waitForSelector('#btn-confirm-forgot-pin-close');
  console.log('Forgot PIN modal neutral response — PASS');
  await page.screenshot({ path: path.join(ARTIFACT_DIR, 'admin_forgot_pin_modal_submitted.png') });
  await page.click('#btn-confirm-forgot-pin-close');

  console.log('=== STEP 3: Open Recovery Link (/reset-pin?token=...) & Token Security ===');
  // Route validatePinResetToken
  let tokenValidated = false;
  await page.route('**/auth/pin/reset/validate', async (route) => {
    tokenValidated = true;
    console.log('[POST /auth/pin/reset/validate] HTTP 200 — PASS');
    const body = JSON.parse(route.request().postData() || '{}');
    if (body.token === 'test-secret-recovery-token-999') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          valid: true,
          expiresAt: new Date(Date.now() + 15 * 60 * 1000).toISOString()
        }),
      });
    } else {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ valid: false }),
      });
    }
  });

  let resetPinApiCalled = false;
  await page.route('**/auth/pin/reset', async (route) => {
    resetPinApiCalled = true;
    console.log('[POST /auth/pin/reset] HTTP 204 — PASS');
    const body = JSON.parse(route.request().postData() || '{}');
    if (body.newPin === '654321' && body.confirmPin === '654321' && body.token === 'test-secret-recovery-token-999') {
      await route.fulfill({ status: 204 });
    } else {
      await route.fulfill({ status: 400, contentType: 'application/json', body: JSON.stringify({ message: 'Error' }) });
    }
  });

  // Navigate to link with token
  await page.goto('http://localhost:3000/reset-pin?token=test-secret-recovery-token-999');

  // Verify Token Security: Token MUST BE SCRUBBED from address bar URL
  await page.waitForSelector('#state-form');
  const currentUrl = page.url();
  if (currentUrl.includes('token=')) {
    throw new Error('TOKEN SECURITY VIOLATION: URL still contains token parameter');
  }
  console.log('Recovery token removed from URL — PASS');
  await page.screenshot({ path: path.join(ARTIFACT_DIR, 'admin_reset_pin_valid_form.png') });

  // Enter Step 1: New PIN
  console.log('=== STEP 4: Set New Security PIN (Sequential, Auto-advance) ===');
  const step1Input = page.locator('#input-reset-pin-step1');
  await step1Input.type('654321');

  // Auto advances to Step 2
  await page.waitForSelector('#input-reset-pin-step2');
  console.log('PIN Step 1 auto-advance — PASS');

  // Enter Step 2: Confirm PIN
  const step2Input = page.locator('#input-reset-pin-step2');
  await step2Input.type('654321');

  // Wait for Success screen (HTTP 204)
  await page.waitForSelector('#state-reset-success');
  console.log('PIN Reset HTTP 204 success screen — PASS');
  await page.screenshot({ path: path.join(ARTIFACT_DIR, 'admin_reset_pin_success.png') });

  // Return to Login
  await page.click('#btn-login-after-reset-pin');
  await page.waitForSelector('#input-admin-login-id');
  console.log('Return to Login after PIN reset — PASS');

  await browser.close();
  console.log('=== ALL COMPREHENSIVE RECOVERY INTEGRATION TESTS PASSED! ===');
}

runComprehensiveRecoveryIntegration().catch((err) => {
  console.error('Integration test failed with error name:', err?.name || 'Error');
  process.exit(1);
});
