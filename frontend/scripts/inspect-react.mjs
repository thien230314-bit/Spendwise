import { chromium } from 'playwright';

(async () => {
  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: { width: 1400, height: 1100 } });
  await page.goto('http://localhost:3000/preview/ui?tab=mobile', { waitUntil: 'networkidle' });
  const authButton = page.locator('button:has-text("2. Đăng nhập / Đăng ký")');
  await authButton.click();
  await page.waitForTimeout(500);

  const ir = page.locator('section[aria-label="Trang đăng ký"] .nb-inner');
  const details = await ir.evaluate(el => {
    return {
      styleMinHeight: el.style.minHeight,
      offsetHeight: el.offsetHeight,
      scrollHeight: el.scrollHeight,
      parentHeight: el.parentElement.offsetHeight,
      grandParentHeight: el.parentElement.parentElement.offsetHeight,
    };
  });
  console.log('React details:', details);

  await browser.close();
})();
