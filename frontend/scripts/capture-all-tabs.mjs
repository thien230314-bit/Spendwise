import { chromium } from 'playwright';

(async () => {
  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: { width: 1400, height: 1100 } });
  await page.goto('http://localhost:3000/preview/ui?tab=mobile', { waitUntil: 'networkidle' });

  const tabs = [
    { selector: 'button:has-text("Ví")', name: 'mobile_wallet_tab_verified.png' },
    { selector: 'button:has-text("Chuyển tiền")', name: 'mobile_transfer_tab_verified.png' },
    { selector: 'button:has-text("Kế hoạch")', name: 'mobile_planning_tab_verified.png' },
    { selector: 'button:has-text("Tiện ích")', name: 'mobile_menu_tab_verified.png' },
    { selector: 'button:has-text("Bạn bè")', name: 'mobile_friends_tab_verified.png' },
    { selector: 'button:has-text("Trợ lý AI")', name: 'mobile_aichat_tab_verified.png' },
  ];

  for (const t of tabs) {
    const btn = page.locator(t.selector).first();
    if (await btn.count() > 0) {
      await btn.click();
      await page.waitForTimeout(600);
      await page.screenshot({ path: `C:/Users/Hi/.gemini/antigravity/brain/b80684d2-69e3-4692-9706-e765dc5fd8ae/${t.name}` });
      console.log(`Captured ${t.name}`);
    } else {
      console.log(`Could not find ${t.selector}`);
    }
  }

  await browser.close();
  console.log('Finished capturing all tabs.');
})();
