import { SupportedLocale } from '../types';

interface FormatMoneyOptions {
  showSign?: boolean;
  currencySymbol?: string;
  isHidden?: boolean;
  forceSign?: boolean;
}

/**
 * Format number according to locale
 * vi-VN: 18.450.000
 * en-US: 18,450,000
 */
export function formatNumber(amount: number, locale: SupportedLocale = 'vi-VN'): string {
  const absAmount = Math.abs(amount);
  if (locale === 'vi-VN') {
    return new Intl.NumberFormat('vi-VN').format(absAmount);
  }
  return new Intl.NumberFormat('en-US').format(absAmount);
}

/**
 * Format currency according to locale
 * vi-VN: 18.450.000 ₫ (or +18.450.000 ₫ / -8.750.000 ₫)
 * en-US: ₫18,450,000 (or +₫18,450,000 / -₫8,750,000)
 */
export function formatMoney(
  amount: number,
  locale: SupportedLocale = 'vi-VN',
  options: FormatMoneyOptions = {}
): string {
  const { showSign = false, currencySymbol = '₫', isHidden = false, forceSign = false } = options;

  if (isHidden) {
    return '••••••••';
  }

  const formattedNum = formatNumber(amount, locale);
  let sign = '';
  if (showSign || forceSign) {
    if (amount > 0) sign = '+';
    else if (amount < 0) sign = '−';
  } else if (amount < 0) {
    sign = '−';
  }

  if (locale === 'vi-VN') {
    return `${sign}${formattedNum} ${currencySymbol}`.trim();
  } else {
    return `${sign}${currencySymbol}${formattedNum}`.trim();
  }
}

export { moneyToWords, formatReadableAmount } from './moneyToWords';
