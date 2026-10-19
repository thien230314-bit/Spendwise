import { SupportedLocale } from '../types';

export function parseDateSafely(date: Date | string | undefined | null): Date | null {
  if (!date) return null;
  if (date instanceof Date) return isNaN(date.getTime()) ? null : date;
  if (typeof date !== 'string') return null;

  // Check if it's DD/MM/YYYY or DD-MM-YYYY
  const dmyMatch = date.match(/^(\d{1,2})[\/\-](\d{1,2})[\/\-](\d{4})/);
  if (dmyMatch) {
    const day = parseInt(dmyMatch[1], 10);
    const month = parseInt(dmyMatch[2], 10) - 1;
    const year = parseInt(dmyMatch[3], 10);
    const d = new Date(year, month, day);
    return isNaN(d.getTime()) ? null : d;
  }

  const d = new Date(date);
  return isNaN(d.getTime()) ? null : d;
}

/**
 * Format date string according to locale
 * vi-VN: 24/08/2026
 * en-US: 08/24/2026
 */
export function formatDate(date: Date | string | undefined | null, locale: SupportedLocale = 'vi-VN'): string {
  if (!date) return '';
  const d = parseDateSafely(date);
  if (!d) return typeof date === 'string' ? date : '';

  if (locale === 'vi-VN') {
    const day = String(d.getDate()).padStart(2, '0');
    const month = String(d.getMonth() + 1).padStart(2, '0');
    const year = d.getFullYear();
    return `${day}/${month}/${year}`;
  } else {
    const day = String(d.getDate()).padStart(2, '0');
    const month = String(d.getMonth() + 1).padStart(2, '0');
    const year = d.getFullYear();
    return `${month}/${day}/${year}`;
  }
}

/**
 * Format relative or contextual date for transactions
 */
export function formatTransactionTime(dateStr: string, locale: SupportedLocale = 'vi-VN'): string {
  const d = parseDateSafely(dateStr);
  if (!d) return dateStr;

  const hours = String(d.getHours()).padStart(2, '0');
  const minutes = String(d.getMinutes()).padStart(2, '0');
  const timePart = `${hours}:${minutes}`;

  const formattedDate = formatDate(d, locale);
  return `${formattedDate} • ${timePart}`;
}

/**
 * Format full exact transaction time: HH:mm:ss + dd/MM/yyyy
 */
export function formatExactTransactionTime(
  dateStr: string | Date | undefined | null,
  locale: SupportedLocale = 'vi-VN'
): string {
  const d = parseDateSafely(dateStr);
  if (!d) return typeof dateStr === 'string' ? dateStr : '';

  const hours = String(d.getHours()).padStart(2, '0');
  const minutes = String(d.getMinutes()).padStart(2, '0');
  const seconds = String(d.getSeconds()).padStart(2, '0');
  const timePart = `${hours}:${minutes}:${seconds}`;

  const day = String(d.getDate()).padStart(2, '0');
  const month = String(d.getMonth() + 1).padStart(2, '0');
  const year = d.getFullYear();

  if (locale === 'en-US') {
    return `${timePart} • ${month}/${day}/${year}`;
  }
  return `${timePart} • ${day}/${month}/${year}`;
}

/**
 * Get greeting context based on time of day
 */
export function getTimeOfDay(): 'morning' | 'afternoon' | 'evening' {
  const hour = new Date().getHours();
  if (hour < 12) return 'morning';
  if (hour < 18) return 'afternoon';
  return 'evening';
}
