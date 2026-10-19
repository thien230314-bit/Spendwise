import { chromium } from 'playwright';

(async () => {
  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: { width: 1400, height: 1100 } });
  await page.goto('http://localhost:3000/preview/ui?tab=mobile', { waitUntil: 'networkidle' });
  const authButton = page.locator('button:has-text("2. Đăng nhập / Đăng ký")');
  await authButton.click();
  await page.waitForTimeout(500);

  const goldBtn = page.locator('.nb-btn-gold:has-text("Đăng nhập ngay")');
  const geom = await goldBtn.evaluate(el => {
    return {
      clientHeight: el.clientHeight,
      offsetHeight: el.offsetHeight,
      scrollHeight: el.scrollHeight,
      rect: el.getBoundingClientRect(),
      parentRect: el.parentElement.getBoundingClientRect(),
      grandParentRect: el.parentElement.parentElement.getBoundingClientRect(),
    };
  });
  console.log('Geom:', geom);

  await browser.close();
})();
