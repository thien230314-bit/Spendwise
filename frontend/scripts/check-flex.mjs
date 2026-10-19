import { chromium } from 'playwright';

(async () => {
  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: { width: 1400, height: 1100 } });
  await page.goto('http://localhost:3000/preview/ui?tab=mobile', { waitUntil: 'networkidle' });
  const authButton = page.locator('button:has-text("2. Đăng nhập / Đăng ký")');
  await authButton.click();
  await page.waitForTimeout(500);

  const goldBtn = page.locator('.nb-btn-gold:has-text("Đăng nhập ngay")');
  const details = await goldBtn.evaluate(el => {
    const s = window.getComputedStyle(el);
    return {
      height: s.height,
      minHeight: s.minHeight,
      maxHeight: s.maxHeight,
      flexShrink: s.flexShrink,
      flexGrow: s.flexGrow,
      flexBasis: s.flexBasis,
      parentHeight: el.parentElement.offsetHeight,
      parentScrollHeight: el.parentElement.scrollHeight,
    };
  });
  console.log('Flex details:', details);

  await browser.close();
})();
