import { chromium } from 'playwright';

(async () => {
  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: { width: 1440, height: 1100 } });
  
  // Navigate to preview mobile center with home screen
  await page.goto('http://localhost:3000/preview/ui?tab=mobile&screen=home', { waitUntil: 'networkidle' });
  await page.waitForTimeout(1000);

  const phoneFrame = page.locator('main').first();

  // 1. Initial State: Home tab active (Ring centered around Home icon)
  await page.waitForTimeout(500);
  await phoneFrame.screenshot({ path: 'C:/Users/Hi/.gemini/antigravity/brain/b80684d2-69e3-4692-9706-e765dc5fd8ae/nav_elastic_ring_home_active.png' });
  console.log('Captured: nav_elastic_ring_home_active.png');

  // 2. Click adjacent tab "Ví & GD"
  const walletBtn = page.locator('nav[aria-label="SpendWise Mobile Navigation"] button:has-text("Ví & GD")').first();
  await walletBtn.click();
  // Capture mid-transition stretch / settled
  await page.waitForTimeout(100);
  await phoneFrame.screenshot({ path: 'C:/Users/Hi/.gemini/antigravity/brain/b80684d2-69e3-4692-9706-e765dc5fd8ae/nav_elastic_ring_mid_stretch.png' });
  console.log('Captured: nav_elastic_ring_mid_stretch.png');

  await page.waitForTimeout(500);
  await phoneFrame.screenshot({ path: 'C:/Users/Hi/.gemini/antigravity/brain/b80684d2-69e3-4692-9706-e765dc5fd8ae/nav_elastic_ring_wallet_settled.png' });
  console.log('Captured: nav_elastic_ring_wallet_settled.png');

  // 3. Jump to far tab "Tiện ích" (demonstrating wide capsule stretch)
  const menuBtn = page.locator('nav[aria-label="SpendWise Mobile Navigation"] button:has-text("Tiện ích")').first();
  await menuBtn.click();
  await page.waitForTimeout(120);
  await phoneFrame.screenshot({ path: 'C:/Users/Hi/.gemini/antigravity/brain/b80684d2-69e3-4692-9706-e765dc5fd8ae/nav_elastic_ring_far_stretch.png' });
  console.log('Captured: nav_elastic_ring_far_stretch.png');

  await page.waitForTimeout(500);
  await phoneFrame.screenshot({ path: 'C:/Users/Hi/.gemini/antigravity/brain/b80684d2-69e3-4692-9706-e765dc5fd8ae/nav_elastic_ring_menu_settled.png' });
  console.log('Captured: nav_elastic_ring_menu_settled.png');

  // 4. Test sound toggle in header: click Âm thanh: TẮT -> BẬT
  const soundToggle = page.locator('header button:has-text("Âm thanh")').first();
  console.log('Sound toggle text before click:', await soundToggle.innerText());
  await soundToggle.click();
  await page.waitForTimeout(300);
  console.log('Sound toggle text after click:', await soundToggle.innerText());
  await page.screenshot({ path: 'C:/Users/Hi/.gemini/antigravity/brain/b80684d2-69e3-4692-9706-e765dc5fd8ae/nav_sound_toggle_enabled.png' });

  // 5. Test dark mode with glowing emerald ring
  const themeToggle = page.locator('header button[title="Đổi Light/Dark"]').first();
  await themeToggle.click();
  await page.waitForTimeout(500);

  // Jump to transfer tab in dark mode
  const transferBtn = page.locator('nav[aria-label="SpendWise Mobile Navigation"] button:has-text("Chuyển tiền")').first();
  await transferBtn.click();
  await page.waitForTimeout(500);
  await phoneFrame.screenshot({ path: 'C:/Users/Hi/.gemini/antigravity/brain/b80684d2-69e3-4692-9706-e765dc5fd8ae/nav_elastic_ring_dark_mode.png' });
  console.log('Captured: nav_elastic_ring_dark_mode.png');

  await browser.close();
  console.log('Test completed successfully.');
})();
