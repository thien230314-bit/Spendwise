import { chromium } from 'playwright';

(async () => {
  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: { width: 1400, height: 1100 } });
  await page.goto('http://localhost:3000/preview/ui?tab=mobile', { waitUntil: 'networkidle' });
  const authButton = page.locator('button:has-text("2. Đăng nhập / Đăng ký")');
  await authButton.click();
  await page.waitForTimeout(500);

  const regInner = page.locator('section[aria-label="Trang đăng ký"] .nb-inner');
  const details = await regInner.evaluate(el => {
    const children = Array.from(el.children);
    return children.map(c => ({
      tag: c.tagName,
      className: c.className,
      offsetHeight: c.offsetHeight,
      marginBottom: window.getComputedStyle(c).marginBottom,
      marginTop: window.getComputedStyle(c).marginTop,
    }));
  });
  console.log('Reg Inner Children:', details);

  const goldBtn = page.locator('.nb-btn-gold:has-text("Đăng nhập ngay")');
  console.log('Gold btn box:', await goldBtn.boundingBox());

  const bookBox = await page.locator('.nb-book').boundingBox();
  console.log('Book box:', bookBox);

  await browser.close();
})();
