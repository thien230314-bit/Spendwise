import { chromium } from 'playwright';

(async () => {
  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: { width: 1400, height: 1100 } });
  await page.goto('http://localhost:3000/preview/ui?tab=mobile', { waitUntil: 'networkidle' });
  const authButton = page.locator('button:has-text("2. Đăng nhập / Đăng ký")');
  await authButton.click();
  await page.waitForTimeout(500);

  const goldBtn = page.locator('.nb-btn-gold:has-text("Đăng nhập ngay")');
  const computed = await goldBtn.evaluate(el => {
    const s = window.getComputedStyle(el);
    return {
      display: s.display,
      height: s.height,
      boxSizing: s.boxSizing,
      overflow: s.overflow,
      clip: s.clip,
      clipPath: s.clipPath,
      parentHeight: el.parentElement.clientHeight,
      parentOverflow: window.getComputedStyle(el.parentElement).overflow,
      grandParentHeight: el.parentElement.parentElement.clientHeight,
      grandParentOverflow: window.getComputedStyle(el.parentElement.parentElement).overflow,
    };
  });
  console.log('Gold btn computed:', computed);

  await browser.close();
})();
