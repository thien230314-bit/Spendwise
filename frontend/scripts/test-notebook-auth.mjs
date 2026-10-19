import { chromium } from 'playwright';

(async () => {
  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: { width: 1400, height: 1100 } });
  
  await page.goto('http://localhost:3000/preview/ui?tab=mobile', { waitUntil: 'networkidle' });
  
  // Click on "2. Đăng nhập / Đăng ký" button in the sidebar
  const authButton = page.locator('button:has-text("2. Đăng nhập / Đăng ký")');
  await authButton.click();
  await page.waitForTimeout(1000);

  // Capture Login State
  await page.screenshot({
    path: 'C:/Users/Hi/.gemini/antigravity/brain/b80684d2-69e3-4692-9706-e765dc5fd8ae/notebook_auth_login_verified.png'
  });

  // Click on "ĐĂNG KÝ NGAY" gold button
  const regBtn = page.locator('.nb-btn-gold:has-text("Đăng ký ngay")');
  console.log('Reg button count:', await regBtn.count());
  await regBtn.click();

  // Wait for 1150ms animation to complete + settle
  await page.waitForTimeout(1500);

  // Capture Register State (flipped up)
  await page.screenshot({
    path: 'C:/Users/Hi/.gemini/antigravity/brain/b80684d2-69e3-4692-9706-e765dc5fd8ae/notebook_auth_register_flipped_verified.png'
  });

  // Click on "ĐĂNG NHẬP NGAY" gold button to flip back
  const loginBtn = page.locator('.nb-btn-gold:has-text("Đăng nhập ngay")');
  await loginBtn.click();
  await page.waitForTimeout(1500);

  // Capture back to Login State
  await page.screenshot({
    path: 'C:/Users/Hi/.gemini/antigravity/brain/b80684d2-69e3-4692-9706-e765dc5fd8ae/notebook_auth_login_back_verified.png'
  });

  await browser.close();
  console.log('Notebook Auth verification screenshots captured.');
})();
