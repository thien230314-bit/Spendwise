import { chromium } from 'playwright';

(async () => {
  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: { width: 1400, height: 1100 } });
  
  await page.goto('http://localhost:3000/preview/ui?tab=mobile', { waitUntil: 'networkidle' });
  
  const homeTabButton = page.locator('button:has-text("Trang chủ (Home)")');
  await homeTabButton.click();
  await page.waitForTimeout(1000);

  const scrollElement = page.locator('#mobile-home-screen-scroll-container');
  // Scroll to 2000px
  await scrollElement.evaluate((el) => {
    el.scrollTop = 2000;
  });
  await page.waitForTimeout(600);
  await page.screenshot({
    path: 'C:/Users/Hi/.gemini/antigravity/brain/b80684d2-69e3-4692-9706-e765dc5fd8ae/mobile_home_scroll_transactions_verified.png'
  });

  await browser.close();
  console.log('Transactions capture completed.');
})();
