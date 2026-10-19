import { chromium } from 'playwright';

(async () => {
  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: { width: 1400, height: 1100 } });
  await page.goto('http://localhost:3000/notebook-auth-standalone.html', { waitUntil: 'networkidle' });

  const regInner = page.locator('#pg-register .inner');
  const loginInner = page.locator('#pg-login .inner');
  const book = page.locator('#book');

  console.log('Standalone Heights:', {
    regH: await regInner.evaluate(el => el.offsetHeight),
    loginH: await loginInner.evaluate(el => el.offsetHeight),
    bookH: await book.evaluate(el => el.offsetHeight),
  });

  const goldBtn = page.locator('#pg-register .btn-gold');
  console.log('Standalone Gold Btn Box:', await goldBtn.boundingBox());

  await browser.close();
})();
