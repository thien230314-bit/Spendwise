import { Transaction } from '../types';

/**
 * Helper to safely parse a date string.
 * Returns null if invalid or missing.
 */
function parseValidDate(val?: string | null): Date | null {
  if (!val || typeof val !== 'string') return null;
  const d = new Date(val);
  return isNaN(d.getTime()) ? null : d;
}

/**
 * Priority for transaction effective time:
 * occurredAt -> date -> rawDate
 * Returns the first VALID date, or null if none are valid.
 * NEVER substitutes current time / new Date().
 */
export function getTransactionEffectiveDate(tx: Transaction): Date | null {
  const d1 = parseValidDate(tx.occurredAt);
  if (d1) return d1;
  const d2 = parseValidDate(tx.date);
  if (d2) return d2;
  const d3 = parseValidDate(tx.rawDate);
  if (d3) return d3;
  return null;
}

/**
 * Filter out transactions that do not belong to the current user or are not completed.
 * Strict ownership: tx.userId === currentUserId. Missing userId is NOT owned by the current user.
 */
export function isUserCompletedTx(tx: Transaction, currentUserId?: string): boolean {
  if (tx.status !== 'completed') return false;
  if (!currentUserId) return false;
  return tx.userId === currentUserId;
}

/**
 * Determine if a transaction is normal Thu (income) or Chi (expense).
 * Excludes: transfer, group_settlement, refund, goal_allocation, unknown.
 * Falls back ONLY to tx.type === 'income' | 'expense' for records without semanticType.
 * Does NOT use direction (credit/debit) as fallback.
 */
export function getAnalyticsFlow(tx: Transaction): 'income' | 'expense' | null {
  // Excluded flows
  if (
    tx.semanticType === 'transfer' ||
    tx.semanticType === 'group_settlement' ||
    tx.semanticType === 'refund' ||
    tx.semanticType === 'goal_allocation' ||
    tx.semanticType === 'unknown'
  ) {
    return null;
  }

  if (tx.semanticType === 'income') return 'income';
  if (tx.semanticType === 'expense') return 'expense';

  // Fallback ONLY for legacy records without semanticType
  if (tx.type === 'income') return 'income';
  if (tx.type === 'expense') return 'expense';

  return null;
}

/**
 * Normalize and resolve category key for analytics.
 * Unclassified imported transactions default to:
 * - income -> categories.income_other
 * - expense -> categories.expense_other
 */
export function getAnalyticsCategoryKey(tx: Transaction, flow: 'income' | 'expense'): string {
  const raw = tx.category?.trim();
  if (!raw) {
    return flow === 'income' ? 'categories.income_other' : 'categories.expense_other';
  }

  // Legacy key migration
  if (raw === 'cat_inc_04' || (raw === 'categories.others' && flow === 'income')) {
    return 'categories.income_other';
  }
  if (raw === 'cat_exp_08' || (raw === 'categories.others' && flow === 'expense')) {
    return 'categories.expense_other';
  }

  return raw;
}

export interface MonthSlot {
  year: number;
  month: number; // 0-indexed (0 = Jan, 11 = Dec)
  labelVi: string; // e.g. "T4", "T9"
  labelEn: string; // e.g. "Apr", "Sep"
  fullLabelVi: string; // e.g. "Tháng 4, 2026"
  fullLabelEn: string; // e.g. "April 2026"
  isCurrentMonth: boolean;
}

/**
 * Returns exactly 6 calendar months ending with the reference date's month.
 * Handles year boundaries correctly (e.g. if current is February 2027: Sep, Oct, Nov, Dec 2026, Jan, Feb 2027).
 */
export function getLast6CalendarMonths(referenceDate: Date = new Date()): MonthSlot[] {
  const slots: MonthSlot[] = [];
  const curYear = referenceDate.getFullYear();
  const curMonth = referenceDate.getMonth(); // 0-11

  for (let i = 5; i >= 0; i--) {
    let targetMonth = curMonth - i;
    let targetYear = curYear;
    while (targetMonth < 0) {
      targetMonth += 12;
      targetYear -= 1;
    }

    const monthNum = targetMonth + 1;
    const enShortMonths = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
    const enFullMonths = [
      'January', 'February', 'March', 'April', 'May', 'June',
      'July', 'August', 'September', 'October', 'November', 'December'
    ];

    slots.push({
      year: targetYear,
      month: targetMonth,
      labelVi: `T${monthNum}`,
      labelEn: enShortMonths[targetMonth],
      fullLabelVi: `Tháng ${monthNum}, ${targetYear}`,
      fullLabelEn: `${enFullMonths[targetMonth]} ${targetYear}`,
      isCurrentMonth: i === 0,
    });
  }

  return slots;
}

/**
 * Color palettes for semantic categories
 * Chi (Expense) = RED family
 * Thu (Income) = GREEN family
 */
export const EXPENSE_RED_PALETTE = [
  '#EF4444', // Red 500
  '#DC2626', // Red 600
  '#F87171', // Red 400
  '#B91C1C', // Red 700
  '#F43F5E', // Rose 500
  '#E11D48', // Rose 600
  '#991B1B', // Red 800
  '#FB7185', // Rose 400
  '#BE123C', // Rose 700
  '#FDA4AF', // Rose 300
  '#881337', // Rose 900
];

export const INCOME_GREEN_PALETTE = [
  '#10B981', // Emerald 500
  '#059669', // Emerald 600
  '#34D399', // Emerald 400
  '#047857', // Emerald 700
  '#14B8A6', // Teal 500
  '#0D9488', // Teal 600
  '#065F46', // Emerald 800
  '#22C55E', // Green 500
  '#16A34A', // Green 600
  '#6EE7B7', // Emerald 300
  '#15803D', // Green 700
];
