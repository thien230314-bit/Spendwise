const { chromium } = require('playwright');
const path = require('path');

const ARTIFACT_DIR = 'C:/Users/Hi/.gemini/antigravity/brain/b80684d2-69e3-4692-9706-e765dc5fd8ae';

async function runRecoveryVerification() {
  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext({ viewport: { width: 1280, height: 800 } });
  const page = await context.newPage();

  console.log('--- 1. Testing Admin Login and Forgot Password Entry ---');
  await page.goto('http://localhost:3000/login');
  await page.waitForSelector('#btn-admin-forgot-password-entry');
  console.log('✓ Found #btn-admin-forgot-password-entry on Admin Login');

  // Intercept backend calls with strict contracts
  let forgotPasswordPayload = null;
  let resetPasswordPayload = null;

  await page.route('**/auth/forgot-password', async (route) => {
    forgotPasswordPayload = JSON.parse(route.request().postData() || '{}');
    console.log('[POST /auth/forgot-password] HTTP 202 — PASS');
    await route.fulfill({
      status: 202,
      contentType: 'application/json',
      body: JSON.stringify({ message: 'If an account matches, instructions sent.' }),
    });
  });

  await page.route('**/auth/reset-password', async (route) => {
    resetPasswordPayload = JSON.parse(route.request().postData() || '{}');
    console.log('[POST /auth/reset-password] HTTP 204 — PASS');
    await route.fulfill({
      status: 204,
    });
  });

  // Click Forgot password entry
  await page.click('#btn-admin-forgot-password-entry');
  await page.waitForSelector('#input-recovery-email');
  console.log('Recovery Step 1 Email form — PASS');

  // Submit email
  await page.fill('#input-recovery-email', 'admin.finance@spendwise.vn');
  await page.click('#btn-recovery-email-submit');

  // Step 2: OTP
  await page.waitForSelector('#input-recovery-otp');
  console.log('Recovery Step 2 OTP form — PASS');
  await page.screenshot({ path: path.join(ARTIFACT_DIR, 'admin_recovery_step2_otp.png') });

  // Verify neutral text
  const neutralText = await page.textContent('text=Nếu email có thể sử dụng');
  console.log(`Neutral confirmation text — ${neutralText ? 'PASS' : 'FAIL'}`);

  // Verify 8-digit numeric enforcement
  await page.evaluate(() => {
    const input = document.getElementById('input-recovery-otp');
    input.focus();
    const dt = new DataTransfer();
    dt.setData('text', 'letters12345678extra');
    const pasteEv = new ClipboardEvent('paste', { clipboardData: dt, bubbles: true, cancelable: true });
    input.dispatchEvent(pasteEv);
  });
  const otpVal = await page.inputValue('#input-recovery-otp');
  if (otpVal !== '12345678') throw new Error('OTP paste filtering assertion failed');
  console.log('OTP paste filtering — PASS');

  // Advance to Step 3
  await page.click('#btn-recovery-otp-continue');
  await page.waitForSelector('#input-recovery-new-password');
  console.log('Recovery Step 3 New Password form — PASS');
  await page.screenshot({ path: path.join(ARTIFACT_DIR, 'admin_recovery_step3_password.png') });

  // Fill new password matching policy
  await page.fill('#input-recovery-new-password', 'AdminSecure2026');
  await page.fill('#input-recovery-confirm-password', 'AdminSecure2026');
  await page.click('#btn-recovery-reset-submit');

  // Step 4: Success Screen
  await page.waitForSelector('#btn-recovery-success-return');
  console.log('Recovery Step 4 Success screen — PASS');
  await page.screenshot({ path: path.join(ARTIFACT_DIR, 'admin_recovery_step4_success.png') });

  // Verify request payloads against Codex contract
  if (forgotPasswordPayload.identifier !== 'admin.finance@spendwise.vn') {
    throw new Error('Forgot password identifier mismatch!');
  }
  if (resetPasswordPayload.identifier !== 'admin.finance@spendwise.vn' ||
      resetPasswordPayload.otp !== '12345678' ||
      resetPasswordPayload.newPassword !== 'AdminSecure2026' ||
      resetPasswordPayload.confirmPassword !== undefined) {
    throw new Error('Reset password contract mismatch!');
  }
  console.log('Codex Backend contract assertions — PASS');

  // Return to Login
  await page.click('#btn-recovery-success-return');
  await page.waitForSelector('#btn-admin-forgot-password-entry');
  console.log('Return to Admin Login screen — PASS');

  await browser.close();
  console.log('=== ALL RECOVERY BROWSER VERIFICATION TESTS PASSED! ===');
}

runRecoveryVerification().catch((err) => {
  console.error('Test execution failed with error name:', err?.name || 'Error');
  process.exit(1);
});
