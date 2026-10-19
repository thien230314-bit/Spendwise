import { chromium } from 'playwright';

(async () => {
  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: { width: 1400, height: 1100 } });
  
  await page.goto('http://localhost:3000/preview/ui?tab=mobile', { waitUntil: 'networkidle' });
  
  // Click on "Trang chủ (Home)" button in the sidebar
  const homeTabButton = page.locator('button:has-text("Trang chủ (Home)")');
  await homeTabButton.click();
  await page.waitForTimeout(1000);

  // Scroll #mobile-home-screen-scroll-container directly
  const scrollElement = page.locator('#mobile-home-screen-scroll-container');
  console.log('Scroll container count:', await scrollElement.count());

  await scrollElement.evaluate((el) => {
    el.scrollTop = 500;
  });
  await page.waitForTimeout(600);
  await page.screenshot({
    path: 'C:/Users/Hi/.gemini/antigravity/brain/b80684d2-69e3-4692-9706-e765dc5fd8ae/mobile_home_scroll_middle_verified.png'
  });

  await scrollElement.evaluate((el) => {
    el.scrollTop = 1200;
  });
  await page.waitForTimeout(600);
  await page.screenshot({
    path: 'C:/Users/Hi/.gemini/antigravity/brain/b80684d2-69e3-4692-9706-e765dc5fd8ae/mobile_home_scroll_bottom_verified.png'
  });

  await browser.close();
  console.log('Precise scroll capture completed.');
})();
