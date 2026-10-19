import { chromium } from 'playwright';

(async () => {
  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: { width: 1400, height: 1100 } });
  
  await page.goto('http://localhost:3000/preview/ui?tab=mobile', { waitUntil: 'networkidle' });
  
  // Click on "Trang chủ (Home)" button in the sidebar
  const homeTabButton = page.locator('button:has-text("Trang chủ (Home)")');
  await homeTabButton.click();
  
  // Wait a short bit for re-render
  await page.waitForTimeout(1000);
  
  // Capture full preview center screenshot
  await page.screenshot({
    path: 'C:/Users/Hi/.gemini/antigravity/brain/b80684d2-69e3-4692-9706-e765dc5fd8ae/mobile_home_tab_verified.png'
  });
  
  // Also capture specifically the phone frame content
  const phoneDevice = page.locator('.relative.bg-slate-900.rounded-\\[50px\\]').first();
  if (await phoneDevice.count() > 0) {
    await phoneDevice.screenshot({
      path: 'C:/Users/Hi/.gemini/antigravity/brain/b80684d2-69e3-4692-9706-e765dc5fd8ae/mobile_home_device_only.png'
    });
  }

  // Scroll down inside the phone container and take another screenshot to verify all bottom sections
  const scrollContainer = page.locator('.overflow-y-auto.no-scrollbar').first();
  if (await scrollContainer.count() > 0) {
    await scrollContainer.evaluate((el) => { el.scrollTop = 500; });
    await page.waitForTimeout(500);
    await page.screenshot({
      path: 'C:/Users/Hi/.gemini/antigravity/brain/b80684d2-69e3-4692-9706-e765dc5fd8ae/mobile_home_scrolled_middle.png'
    });

    await scrollContainer.evaluate((el) => { el.scrollTop = 1200; });
    await page.waitForTimeout(500);
    await page.screenshot({
      path: 'C:/Users/Hi/.gemini/antigravity/brain/b80684d2-69e3-4692-9706-e765dc5fd8ae/mobile_home_scrolled_bottom.png'
    });
  }

  await browser.close();
  console.log('Screenshots captured successfully.');
})();
