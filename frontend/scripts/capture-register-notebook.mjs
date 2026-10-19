import { chromium } from 'playwright';
import fs from 'fs';

(async () => {
  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: { width: 1440, height: 1100 } });
  await page.goto('http://localhost:3000/preview/ui?tab=mobile', { waitUntil: 'networkidle' });
  await page.waitForTimeout(600);

  const phoneFrame = page.locator('main').first();

  await page.locator('button:has-text("2. Đăng nhập / Đăng ký")').first().click();
  await page.waitForTimeout(600);

  // Click the gold button that contains text "ĐĂNG KÝ NGAY"
  const registerFlipBtn = page.locator('.nb-btn-gold:has-text("ĐĂNG KÝ NGAY")').first();
  if (await registerFlipBtn.count() > 0) {
    await registerFlipBtn.click();
    await page.waitForTimeout(1400); // Wait for 1150ms 3D flip animation
    await phoneFrame.screenshot({ path: 'E:/source/SpendWise-Mobile-UI-Review/screenshots/auth/auth-register-notebook.png' });
    console.log('Successfully captured auth-register-notebook.png');

    // Update CAPTURE-MANIFEST.json
    const manifestPath = 'E:/source/SpendWise-Mobile-UI-Review/CAPTURE-MANIFEST.json';
    if (fs.existsSync(manifestPath)) {
      const data = JSON.parse(fs.readFileSync(manifestPath, 'utf-8'));
      const exists = data.items.some(i => i.filename.includes('auth-register-notebook.png'));
      if (!exists) {
        data.items.push({
          filename: 'screenshots/auth/auth-register-notebook.png',
          screen: 'Đăng ký Sổ tay (Notebook Register)',
          route: '/(guest)/register',
          sourceRuntime: 'Frontend Mobile Preview Adapter',
          viewport: '393x852',
          theme: 'light',
          locale: 'vi-VN',
          uiState: 'Normal / Page Flipped',
          captureResult: 'SUCCESS',
          privacyRedacted: false
        });
        data.totalScreenshots = data.items.length;
        fs.writeFileSync(manifestPath, JSON.stringify(data, null, 2), 'utf-8');
        console.log('Updated manifest with auth-register-notebook.png');
      }
    }
  } else {
    console.log('Could not find .nb-btn-gold:has-text("ĐĂNG KÝ NGAY")');
  }

  await browser.close();
})();
