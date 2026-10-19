// Static aliases keep real weights consistent on iOS, Android and Web.
// All family names live here; components consume semantic styles.
const family = {
  body: { regular: 'BeVietnamPro-Regular', medium: 'BeVietnamPro-Medium', semibold: 'BeVietnamPro-SemiBold', bold: 'BeVietnamPro-Bold', black: 'BeVietnamPro-Black' },
  display: { bold: 'PlusJakartaSans-Bold', extraBold: 'PlusJakartaSans-ExtraBold' },
} as const;

export const fontAssets = {
  [family.body.regular]: require('../../assets/fonts/BeVietnamPro-Regular.ttf'),
  [family.body.medium]: require('../../assets/fonts/BeVietnamPro-Medium.ttf'),
  [family.body.semibold]: require('../../assets/fonts/BeVietnamPro-SemiBold.ttf'),
  [family.body.bold]: require('../../assets/fonts/BeVietnamPro-Bold.ttf'),
  [family.body.black]: require('../../assets/fonts/BeVietnamPro-Black.ttf'),
  [family.display.bold]: require('../../assets/fonts/PlusJakartaSans-Bold.ttf'),
  [family.display.extraBold]: require('../../assets/fonts/PlusJakartaSans-ExtraBold.ttf'),
};

// Each alias already contains its weight; don't synthesize another bold face.
export const fontStyles = {
  body: { fontFamily: family.body.regular, fontWeight: 'normal' },
  medium: { fontFamily: family.body.medium, fontWeight: 'normal' },
  semibold: { fontFamily: family.body.semibold, fontWeight: 'normal' },
  bold: { fontFamily: family.body.bold, fontWeight: 'normal' },
  wordmarkStrong: { fontFamily: family.body.black, fontWeight: 'normal' },
  heading: { fontFamily: family.display.bold, fontWeight: 'normal' },
  display: { fontFamily: family.display.extraBold, fontWeight: 'normal' },
} as const;
