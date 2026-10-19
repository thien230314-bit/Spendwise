import { RecurringTransaction, RecurringFrequency } from '../types';

export const RECURRING_UPDATED_EVENT = 'spendwise_recurring_updated';

export function getTodayDateString(): string {
  const now = new Date();
  const y = now.getFullYear();
  const m = String(now.getMonth() + 1).padStart(2, '0');
  const d = String(now.getDate()).padStart(2, '0');
  return `${y}-${m}-${d}`;
}

/**
 * Calculates the next safe due date based on current frequency.
 * Safely handles month-ends (e.g., 31 January -> 28/29 February -> 31 March)
 * and leap years (e.g., 29 February -> 28 February -> 29 February in next leap year)
 * preserving the original schedule anchor.
 */
export function calculateNextRunDate(
  currentDateStr: string,
  frequency: RecurringFrequency,
  scheduleAnchorDay?: number,
  scheduleAnchorMonth?: number
): string {
  if (!currentDateStr || !/^\d{4}-\d{2}-\d{2}$/.test(currentDateStr)) {
    const now = new Date();
    return now.toISOString().split('T')[0];
  }

  const parts = currentDateStr.split('-').map(Number);
  const y = parts[0];
  const m = parts[1]; // 1-indexed (1..12)
  const d = parts[2]; // 1-indexed (1..31)

  const anchorDay = scheduleAnchorDay ?? d;
  const anchorMonth = scheduleAnchorMonth ?? m;

  switch (frequency) {
    case 'daily': {
      const nextDate = new Date(y, m - 1, d + 1);
      const nextY = nextDate.getFullYear();
      const nextM = String(nextDate.getMonth() + 1).padStart(2, '0');
      const nextD = String(nextDate.getDate()).padStart(2, '0');
      return `${nextY}-${nextM}-${nextD}`;
    }
    case 'weekly': {
      const nextDate = new Date(y, m - 1, d + 7);
      const nextY = nextDate.getFullYear();
      const nextM = String(nextDate.getMonth() + 1).padStart(2, '0');
      const nextD = String(nextDate.getDate()).padStart(2, '0');
      return `${nextY}-${nextM}-${nextD}`;
    }
    case 'monthly': {
      const targetYear = m === 12 ? y + 1 : y;
      const targetMonth = m === 12 ? 1 : m + 1; // 1-indexed
      // Get number of days in target month: new Date(year, month, 0)
      const maxDaysInTargetMonth = new Date(targetYear, targetMonth, 0).getDate();
      const targetDay = Math.min(anchorDay, maxDaysInTargetMonth);
      return `${targetYear}-${String(targetMonth).padStart(2, '0')}-${String(targetDay).padStart(2, '0')}`;
    }
    case 'yearly': {
      const targetYear = y + 1;
      const targetMonth = anchorMonth;
      const maxDaysInTargetMonth = new Date(targetYear, targetMonth, 0).getDate();
      const targetDay = Math.min(anchorDay, maxDaysInTargetMonth);
      return `${targetYear}-${String(targetMonth).padStart(2, '0')}-${String(targetDay).padStart(2, '0')}`;
    }
    default:
      return currentDateStr;
  }
}

/**
 * Determines if a recurring definition is actively due today or past due.
 * Backend-authoritative occurrences ('Due' or 'Overdue') take precedence.
 */
export function isRecurringDue(
  recurring: RecurringTransaction,
  _userId?: string,
  todayStr: string = getTodayDateString()
): boolean {
  if (recurring.status !== 'active') return false;
  if (recurring.occurrences && recurring.occurrences.length > 0) {
    return recurring.occurrences.some((o) => o.status === 'Due' || o.status === 'Overdue');
  }
  if (!recurring.nextRunDate) return false;
  return recurring.nextRunDate <= todayStr;
}
