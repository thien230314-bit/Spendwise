import { chromium } from 'playwright';

(async () => {
  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: { width: 1400, height: 1100 } });
  
  await page.goto('http://localhost:3000/preview/ui?tab=mobile', { waitUntil: 'networkidle' });
  
  // Click on "Trang chủ (Home)" button in the sidebar
  const homeTabButton = page.locator('button:has-text("Trang chủ (Home)")');
  await homeTabButton.click();
  await page.waitForTimeout(1000);

  // Scroll to absolute bottom (to reveal Savings Goals and 5 Recent Transactions)
  await page.evaluate(() => {
    const scrollables = Array.from(document.querySelectorAll('*')).filter(el => {
      return el.scrollHeight > el.clientHeight && el.clientHeight > 200;
    });
    if (scrollables.length > 0) {
      scrollables[scrollables.length - 1].scrollTop = 2200;
    }
  });

  await page.waitForTimeout(600);
  await page.screenshot({
    path: 'C:/Users/Hi/.gemini/antigravity/brain/b80684d2-69e3-4692-9706-e765dc5fd8ae/mobile_home_scroll_very_bottom.png'
  });

  await browser.close();
  console.log('Very bottom scroll captured.');
})();
