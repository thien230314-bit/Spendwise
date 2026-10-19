import { chromium } from 'playwright';

(async () => {
  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: { width: 1400, height: 1100 } });
  await page.goto('http://localhost:3000/preview/ui?tab=mobile', { waitUntil: 'networkidle' });
  const authButton = page.locator('button:has-text("2. Đăng nhập / Đăng ký")');
  await authButton.click();
  await page.waitForTimeout(500);

  const regInner = page.locator('section[aria-label="Trang đăng ký"] .nb-inner');
  const loginInner = page.locator('section[aria-label="Trang đăng nhập"] .nb-inner');
  
  const regScrollH = await regInner.evaluate(el => el.scrollHeight);
  const loginScrollH = await loginInner.evaluate(el => el.scrollHeight);
  console.log('Inner heights:', { regScrollH, loginScrollH });

  await browser.close();
})();
