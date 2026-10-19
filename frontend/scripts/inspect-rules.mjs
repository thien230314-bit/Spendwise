import { chromium } from 'playwright';

(async () => {
  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: { width: 1400, height: 1100 } });
  await page.goto('http://localhost:3000/preview/ui?tab=mobile', { waitUntil: 'networkidle' });
  const authButton = page.locator('button:has-text("2. Đăng nhập / Đăng ký")');
  await authButton.click();
  await page.waitForTimeout(500);

  const matched = await page.evaluate(() => {
    const el = document.querySelector('.nb-btn-gold');
    const rules = [];
    for (const sheet of document.styleSheets) {
      try {
        for (const rule of sheet.cssRules) {
          if (rule.selectorText && el.matches(rule.selectorText)) {
            rules.push({ selector: rule.selectorText, cssText: rule.cssText });
          }
        }
      } catch (e) {}
    }
    return rules;
  });
  console.log('Matched rules:', matched);

  await browser.close();
})();
