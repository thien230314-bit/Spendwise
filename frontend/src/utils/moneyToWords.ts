import { SupportedLocale } from '../types';
import { formatNumber } from './money';

const DIGITS_VI = ['không', 'một', 'hai', 'ba', 'bốn', 'năm', 'sáu', 'bảy', 'tám', 'chín'];
const SCALES_VI = ['', 'nghìn', 'triệu', 'tỷ', 'nghìn tỷ', 'triệu tỷ'];

const ONES_EN = ['', 'one', 'two', 'three', 'four', 'five', 'six', 'seven', 'eight', 'nine'];
const TEENS_EN = [
  'ten',
  'eleven',
  'twelve',
  'thirteen',
  'fourteen',
  'fifteen',
  'sixteen',
  'seventeen',
  'eighteen',
  'nineteen',
];
const TENS_EN = [
  '',
  '',
  'twenty',
  'thirty',
  'forty',
  'fifty',
  'sixty',
  'seventy',
  'eighty',
  'ninety',
];
const SCALES_EN = ['', 'thousand', 'million', 'billion', 'trillion', 'quadrillion'];

function capitalizeFirstLetter(str: string): string {
  if (!str) return '';
  return str.charAt(0).toUpperCase() + str.slice(1);
}

/**
 * Reads a 3-digit group (0..999) in Vietnamese.
 * @param n Group integer (0..999)
 * @param isHighest Whether this is the highest non-zero group in the whole number
 */
function readGroupVi(n: number, isHighest: boolean): string {
  const h = Math.floor(n / 100);
  const t = Math.floor((n % 100) / 10);
  const u = n % 10;

  if (h === 0 && t === 0 && u === 0) {
    return '';
  }

  let res = '';

  // Hundreds
  if (h > 0) {
    res += DIGITS_VI[h] + ' trăm';
  } else if (!isHighest) {
    res += 'không trăm';
  }

  // Tens and units
  if (t === 0 && u === 0) {
    return res.trim();
  }

  if (t === 0 && u > 0) {
    if (h > 0 || !isHighest) {
      res += (res ? ' ' : '') + 'lẻ ' + (u === 5 && !res ? 'năm' : DIGITS_VI[u]);
    } else {
      res += (res ? ' ' : '') + DIGITS_VI[u];
    }
    return res.trim();
  }

  if (t === 1) {
    res += (res ? ' ' : '') + 'mười';
    if (u === 1) {
      res += ' một';
    } else if (u === 5) {
      res += ' lăm';
    } else if (u > 0) {
      res += ' ' + DIGITS_VI[u];
    }
    return res.trim();
  }

  if (t >= 2) {
    res += (res ? ' ' : '') + DIGITS_VI[t] + ' mươi';
    if (u === 1) {
      res += ' mốt';
    } else if (u === 5) {
      res += ' lăm';
    } else if (u > 0) {
      res += ' ' + DIGITS_VI[u];
    }
    return res.trim();
  }

  return res.trim();
}

/**
 * Reads a 3-digit group (0..999) in English without 'and'.
 */
function readGroupEn(n: number): string {
  const h = Math.floor(n / 100);
  const remainder = n % 100;
  let str = '';

  if (h > 0) {
    str += `${ONES_EN[h]} hundred`;
  }

  if (remainder > 0) {
    let remainderStr = '';
    if (remainder < 10) {
      remainderStr = ONES_EN[remainder];
    } else if (remainder < 20) {
      remainderStr = TEENS_EN[remainder - 10];
    } else {
      const ten = Math.floor(remainder / 10);
      const unit = remainder % 10;
      remainderStr = unit > 0 ? `${TENS_EN[ten]}-${ONES_EN[unit]}` : TENS_EN[ten];
    }
    str = str ? `${str} ${remainderStr}` : remainderStr;
  }

  return str;
}

/**
 * Converts a positive safe integer VND amount into written words in Vietnamese or English.
 * Examples:
 * 1000 -> VI: "Một nghìn đồng", EN: "One thousand Vietnamese dong"
 * 1500000 -> VI: "Một triệu năm trăm nghìn đồng", EN: "One million five hundred thousand Vietnamese dong"
 * 50000000 -> VI: "Năm mươi triệu đồng", EN: "Fifty million Vietnamese dong"
 * 125000000 -> VI: "Một trăm hai mươi lăm triệu đồng", EN: "One hundred twenty-five million Vietnamese dong"
 * 1000000000 -> VI: "Một tỷ đồng", EN: "One billion Vietnamese dong"
 */
export function moneyToWords(amount: number, locale: SupportedLocale = 'vi-VN'): string {
  if (!Number.isFinite(amount) || !Number.isSafeInteger(amount) || amount <= 0) {
    return '';
  }

  let temp = Math.floor(amount);
  const chunks: number[] = [];
  while (temp > 0) {
    chunks.push(temp % 1000);
    temp = Math.floor(temp / 1000);
  }

  if (locale === 'vi-VN') {
    const parts: string[] = [];
    let isHighest = true;

    for (let i = chunks.length - 1; i >= 0; i--) {
      const chunk = chunks[i];
      if (chunk === 0) {
        continue;
      }

      const chunkText = readGroupVi(chunk, isHighest);
      isHighest = false;

      const scale = SCALES_VI[i] || '';
      if (chunkText) {
        parts.push(scale ? `${chunkText} ${scale}` : chunkText);
      }
    }

    const words = parts.join(' ').trim();
    if (!words) return '';
    return `${capitalizeFirstLetter(words)} đồng`;
  } else {
    const parts: string[] = [];

    for (let i = chunks.length - 1; i >= 0; i--) {
      const chunk = chunks[i];
      if (chunk === 0) {
        continue;
      }

      const chunkText = readGroupEn(chunk);
      const scale = SCALES_EN[i] || '';
      if (chunkText) {
        parts.push(scale ? `${chunkText} ${scale}` : chunkText);
      }
    }

    const words = parts.join(' ').trim();
    if (!words) return '';
    return `${capitalizeFirstLetter(words)} Vietnamese dong`;
  }
}

/**
 * Returns formatted VND amount with approximate prefix:
 * vi-VN: ≈ 50.000.000 ₫
 * en-US: ≈ 50,000,000 VND
 */
export function formatReadableAmount(amount: number, locale: SupportedLocale = 'vi-VN'): string {
  const formattedNum = formatNumber(amount, locale);
  return locale === 'vi-VN' ? `≈ ${formattedNum} ₫` : `≈ ${formattedNum} VND`;
}
