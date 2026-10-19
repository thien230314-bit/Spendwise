import {
  BudgetCategoryItem,
  CategoryItem,
  GoalContributionItem,
  GoalItem,
  GoalStatus,
  RecurringFrequency,
  RecurringOccurrence,
  RecurringTransaction,
  TransactionRule,
  WalletAccount,
} from '../types';
import { BudgetDto, GoalDto, RecurringDto, TransactionRuleDto } from './planningApiService';

export function mapBudget(dto: BudgetDto, categories: CategoryItem[]): BudgetCategoryItem {
  const matchedCategory = categories.find(
    (c) => c.id === dto.categoryId || c.key === dto.categoryId
  );
  const color = matchedCategory?.color || '#10B981';
  const iconName = matchedCategory?.icon || 'PieChart';

  return {
    id: dto.id,
    categoryId: dto.categoryId,
    categoryKey: matchedCategory ? matchedCategory.key : dto.categoryId,
    name: dto.categoryName || matchedCategory?.name || 'Ngân sách',
    limit: dto.limitAmount,
    month: `${String(dto.month).padStart(2, '0')}/${dto.year}`,
    iconName,
    color,
    iconBg: `${color}20`,
    iconColor: color,
    rollover: dto.rollover,
    alertsEnabled: dto.alertsEnabled,
    spent: dto.spentAmount,
    remaining: dto.remainingAmount,
    percentage: dto.percentage,
    alertLevel: dto.alertLevel,
  };
}

export function mapGoal(dto: GoalDto): GoalItem {
  const colorHex = dto.colorHex || '#10B981';
  const iconName = dto.iconName || 'Target';

  const remainingDays = dto.targetDate
    ? Math.max(0, Math.ceil((new Date(dto.targetDate).getTime() - Date.now()) / (1000 * 60 * 60 * 24)))
    : undefined;

  const statusLower = (dto.status || '').toLowerCase();
  const status: GoalStatus =
    statusLower === 'completed'
      ? 'completed'
      : statusLower === 'paused'
      ? 'paused'
      : statusLower === 'cancelled' || statusLower === 'failed'
      ? 'cancelled'
      : 'active';

  const history: GoalContributionItem[] = (dto.movements || []).map((m) => ({
    id: m.id,
    amount: m.amount,
    date: m.date,
    sourceWalletName: m.walletName || '',
    type: m.type === 'Withdrawal' ? 'withdraw' : 'deposit',
  }));

  return {
    id: dto.id,
    title: dto.name,
    category: dto.categoryName || 'Mục tiêu tích lũy',
    categoryId: dto.categoryId || undefined,
    categoryName: dto.categoryName || undefined,
    targetAmount: dto.targetAmount,
    currentAmount: dto.currentAllocatedAmount,
    deadline: dto.targetDate ? dto.targetDate.split('T')[0] : '',
    remainingDays,
    iconName,
    iconBg: `${colorHex}20`,
    iconColor: colorHex,
    colorHex,
    status,
    history,
  };
}

export function mapRecurring(
  dto: RecurringDto,
  wallets: WalletAccount[],
  categories: CategoryItem[]
): RecurringTransaction {
  const matchedWallet = wallets.find((w) => w.id === dto.walletId);
  const matchedCategory = categories.find((c) => c.id === dto.categoryId);

  const freqLower = (dto.frequency || 'Monthly').toLowerCase() as RecurringFrequency;
  const statusLower = (dto.status || 'Active').toLowerCase() === 'paused' ? 'paused' : 'active';
  const kindLower = (dto.kind || 'Expense').toLowerCase() === 'income' ? 'income' : 'expense';

  const occurrences: RecurringOccurrence[] = (dto.occurrences || []).map((o) => ({
    id: o.id,
    scheduledFor: o.scheduledFor,
    status: o.status,
    recordedTransactionId: o.recordedTransactionId || undefined,
    recordedAt: o.recordedAt || undefined,
  }));

  return {
    id: dto.id,
    title: dto.title || dto.description || 'Giao dịch định kỳ',
    type: kindLower,
    walletId: dto.walletId,
    walletName: matchedWallet?.name,
    categoryId: dto.categoryId,
    categoryName: matchedCategory?.name || dto.categoryId,
    amount: dto.amount,
    frequency: freqLower,
    nextRunDate: dto.nextRunAt ? dto.nextRunAt.split('T')[0] : '',
    scheduleAnchorDay: dto.anchorDay,
    scheduleAnchorMonth: dto.anchorMonth,
    description: dto.description,
    status: statusLower,
    isSubscription: dto.isSubscription,
    lastRunAt: dto.lastRunAt || undefined,
    occurrences,
  };
}

export function mapTransactionRule(dto: TransactionRuleDto): TransactionRule {
  return {
    id: dto.id,
    descriptionPattern: dto.descriptionPattern,
    targetCategoryId: dto.targetCategoryId,
    targetCategoryName: dto.targetCategoryName,
    priority: dto.priority,
    isActive: dto.isActive,
  };
}
