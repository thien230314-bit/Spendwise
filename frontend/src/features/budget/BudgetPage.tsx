import React, { useState, useMemo, useEffect } from 'react';
import { useSearchParams } from 'react-router-dom';
import {
  PieChart as PieIcon,
  Plus,
  Edit3,
  Trash2,
  AlertTriangle,
  CheckCircle2,
  Calendar,
  Utensils,
  Car,
  ShoppingBag,
  Tv,
  Home,
  Briefcase,
  Layers,
  ChevronRight,
  TrendingUp,
  X,
  ArrowRight,
  Sparkles,
  Info,
  ShieldCheck,
  Zap,
} from 'lucide-react';
import { useI18n } from '../../i18n/I18nContext';
import { useAuth } from '../../providers/AuthProvider';
import { useBalanceVisibility } from '../../providers/BalanceVisibilityProvider';
import { useFinancial } from '../../providers/FinancialProvider';
import { BudgetCategoryItem, Transaction } from '../../types';
import { formatMoney } from '../../utils/money';
import { getTransactionEffectiveDate } from '../../utils/analyticsHelpers';
import { Button } from '../../components/ui/Button';
import { Select } from '../../components/ui/Select';
import { cn } from '../../utils/cn';
import { sanitizeIntegerInput, parseSafePositiveInteger } from '../../utils/inputValidation';
import {
  getLocalizedBudgetName,
  getLocalizedTxTitle,
  getLocalizedWalletName,
  getLocalizedCategoryName,
} from '../../utils/localizationHelpers';
import { useSecurityPinChallenge } from '../../providers/SecurityPinChallengeProvider';
import { BudgetSkeleton } from '../../components/skeletons/BudgetSkeleton';

export const BudgetPage: React.FC = () => {
  const { t, locale } = useI18n();
  const { currentUser } = useAuth();
  const { isHidden } = useBalanceVisibility();
  const { challengePin, challengePinWithValue } = useSecurityPinChallenge();
  const { budgets, transactions, categories, createBudget, editBudget, deleteBudget, loadBudgetsForMonth, isLoading } = useFinancial();
  const [searchParams] = useSearchParams();


  // State - default to current calendar month dynamically in MM/YYYY format
  const [selectedMonth, setSelectedMonth] = useState<string>(() => {
    const now = new Date();
    const mm = String(now.getMonth() + 1).padStart(2, '0');
    const yyyy = now.getFullYear();
    return `${mm}/${yyyy}`;
  });
  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);
  const [editingBudget, setEditingBudget] = useState<BudgetCategoryItem | null>(null);
  const [deletingBudget, setDeletingBudget] = useState<BudgetCategoryItem | null>(null);
  const [selectedBudgetForDetails, setSelectedBudgetForDetails] = useState<BudgetCategoryItem | null>(null);

  // Dynamic month options: previous, current, next, and surrounding months
  const monthOptions = useMemo(() => {
    const options: { value: string; label: string }[] = [];
    const now = new Date();
    const monthNamesVi = [
      'Tháng 01', 'Tháng 02', 'Tháng 03', 'Tháng 04', 'Tháng 05', 'Tháng 06',
      'Tháng 07', 'Tháng 08', 'Tháng 09', 'Tháng 10', 'Tháng 11', 'Tháng 12',
    ];
    const monthNamesEn = [
      'January', 'February', 'March', 'April', 'May', 'June',
      'July', 'August', 'September', 'October', 'November', 'December',
    ];

    for (let offset = -6; offset <= 6; offset++) {
      const d = new Date(now.getFullYear(), now.getMonth() + offset, 1);
      const mm = String(d.getMonth() + 1).padStart(2, '0');
      const yyyy = d.getFullYear();
      const val = `${mm}/${yyyy}`;
      const label =
        locale === 'vi-VN'
          ? `${monthNamesVi[d.getMonth()]}/${yyyy}`
          : `${monthNamesEn[d.getMonth()]} ${yyyy}`;
      options.push({ value: val, label });
    }

    if (selectedMonth && !options.some((o) => o.value === selectedMonth)) {
      options.unshift({ value: selectedMonth, label: selectedMonth });
    }
    return options;
  }, [locale, selectedMonth]);

  // Filter shared categories for active expense kind
  const expenseCategories = useMemo(() => categories.filter((c) => c.kind === 'expense' && !c.isArchived), [categories]);

  // Deep linking
  useEffect(() => {
    const month = searchParams.get('month');
    if (month) setSelectedMonth(month);

    const action = searchParams.get('action');
    if (action === 'create') setIsCreateModalOpen(true);

    const category = searchParams.get('category');
    if (category) {
      const match = budgets.find((b) => b.categoryKey === category || b.id === category);
      if (match) setSelectedBudgetForDetails(match);
    }
  }, [searchParams, budgets]);

  // Load budgets from backend whenever the selected month changes
  useEffect(() => {
    if (!loadBudgetsForMonth) return;
    const parts = selectedMonth.split('/').map(Number);
    if (parts.length === 2 && !isNaN(parts[0]) && !isNaN(parts[1])) {
      const [m, y] = parts;
      loadBudgetsForMonth(y, m).catch(() => {});
    }
  }, [selectedMonth, loadBudgetsForMonth]);

  // Form states
  const [formCategoryKey, setFormCategoryKey] = useState('categories.food');
  const [formName, setFormName] = useState('Ăn uống & Cà phê');
  const [formLimit, setFormLimit] = useState('');
  const [formRollover, setFormRollover] = useState(false);
  const [formAlertsEnabled, setFormAlertsEnabled] = useState(true);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [toastMessage, setToastMessage] = useState<string | null>(null);
  const [validationError, setValidationError] = useState<string | null>(null);

  const showToast = (msg: string) => {
    setToastMessage(msg);
    setTimeout(() => setToastMessage(null), 3000);
  };

  // Helper to check if a transaction belongs to the selected month (MM/YYYY)
  const isTransactionInSelectedMonth = (tx: Transaction, monthFormatted: string): boolean => {
    const parts = monthFormatted.split('/');
    if (parts.length !== 2) return false;
    const targetMonth = parseInt(parts[0], 10);
    const targetYear = parseInt(parts[1], 10);
    if (isNaN(targetMonth) || isNaN(targetYear)) return false;

    // Date priority: occurredAt -> date -> rawDate (skips if none valid, no substitute of current time)
    const d = getTransactionEffectiveDate(tx);
    if (!d) return false;

    return d.getMonth() + 1 === targetMonth && d.getFullYear() === targetYear;
  };

  // Helper to strictly check real expense semantics
  const isRealExpenseTx = (tx: Transaction): boolean => {
    if (tx.status !== 'completed') return false;

    // Strictly exclude: transfer, group_settlement, refund, goal_allocation, unknown
    if (
      tx.semanticType === 'transfer' ||
      tx.semanticType === 'group_settlement' ||
      tx.semanticType === 'refund' ||
      tx.semanticType === 'goal_allocation' ||
      tx.semanticType === 'unknown'
    ) {
      return false;
    }

    if (tx.semanticType) {
      return tx.semanticType === 'expense';
    }

    return tx.type === 'expense';
  };

  // Category Icon resolver
  const getCategoryIcon = (key: string) => {
    if (key.includes('food')) return <Utensils className="h-5 w-5" />;
    if (key.includes('transport')) return <Car className="h-5 w-5" />;
    if (key.includes('shopping')) return <ShoppingBag className="h-5 w-5" />;
    if (key.includes('entertainment')) return <Tv className="h-5 w-5" />;
    if (key.includes('utilities')) return <Home className="h-5 w-5" />;
    return <Layers className="h-5 w-5" />;
  };

  // Enriched budgets: use backend-authoritative spent/remaining/percentage/alertLevel when available;
  // only fall back to local computation if backend values are missing (should not happen after integration).
  const enrichedBudgets = useMemo(() => {
    return budgets
      .filter((b) => b.month === selectedMonth)
      .map((b) => {
        // Backend is authoritative for spent/remaining/percentage/alertLevel
        const spent = b.spent ?? 0;
        const remaining = b.remaining ?? b.limit - spent;
        const percentage = b.percentage ?? (b.limit > 0 ? Math.round((spent / b.limit) * 100) : 0);

        // Map backend alertLevel to local status
        let status: 'normal' | 'attention' | 'warning' | 'over' = 'normal';
        if (b.alertLevel === 'Exceeded') status = 'over';
        else if (b.alertLevel === 'Warning') status = 'warning';
        else if (percentage >= 70) status = 'attention';

        return {
          ...b,
          spent,
          remaining,
          percentage,
          status,
        };
      });
  }, [budgets, selectedMonth]);

  // Overall totals
  const overallStats = useMemo(() => {
    let totalLimit = 0;
    let totalSpent = 0;

    enrichedBudgets.forEach((b) => {
      totalLimit += b.limit;
      totalSpent += b.spent;
    });

    const totalRemaining = Math.max(0, totalLimit - totalSpent);
    const overallPercentage = totalLimit > 0 ? Math.min(100, Math.round((totalSpent / totalLimit) * 100)) : 0;

    return {
      totalLimit,
      totalSpent,
      totalRemaining,
      overallPercentage,
    };
  }, [enrichedBudgets]);

  // Smart alerts derived from real budget status
  const smartAlerts = useMemo(() => {
    const alerts: Array<{ id: string; type: 'warning' | 'danger' | 'info'; title: string; desc: string }> = [];

    enrichedBudgets.forEach((b) => {
      const budgetDisplayName = getLocalizedBudgetName(b, t, locale);
      if (b.status === 'over') {
        alerts.push({
          id: `alert-${b.id}`,
          type: 'danger',
          title:
            locale === 'vi-VN'
              ? `Vượt ngân sách ${budgetDisplayName}`
              : `Over budget for ${budgetDisplayName}`,
          desc:
            locale === 'vi-VN'
              ? `Bạn đã chi vượt hạn mức ${formatMoney(Math.abs(b.remaining), locale)}. Hãy hạn chế chi tiêu mục này trong tháng.`
              : `You have exceeded the limit by ${formatMoney(Math.abs(b.remaining), locale)}. Consider limiting expenses for this category this month.`,
        });
      } else if (b.status === 'warning') {
        alerts.push({
          id: `alert-${b.id}`,
          type: 'warning',
          title:
            locale === 'vi-VN'
              ? `Sắp chạm ngưỡng ngân sách ${budgetDisplayName}`
              : `Nearing budget limit for ${budgetDisplayName}`,
          desc:
            locale === 'vi-VN'
              ? `Đã sử dụng ${b.percentage}% hạn mức (${formatMoney(b.spent, locale)} / ${formatMoney(b.limit, locale)}). Còn lại ${formatMoney(b.remaining, locale)}.`
              : `Used ${b.percentage}% of limit (${formatMoney(b.spent, locale)} / ${formatMoney(b.limit, locale)}). Remaining: ${formatMoney(b.remaining, locale)}.`,
        });
      }
    });

    if (alerts.length === 0) {
      alerts.push({
        id: 'alert-good',
        type: 'info',
        title: locale === 'vi-VN' ? 'Kế hoạch chi tiêu tốt' : 'Spending plan on track',
        desc:
          locale === 'vi-VN'
            ? 'Tất cả các danh mục ngân sách tháng này đều đang trong ngưỡng an toàn.'
            : 'All budget categories for this month are within safe spending thresholds.',
      });
    }

    return alerts;
  }, [enrichedBudgets, locale, t]);

  // Priority / Highest risk category if genuinely at attention/warning/over threshold
  const highRiskBudget = useMemo(() => {
    if (enrichedBudgets.length <= 1) return null;
    return (
      enrichedBudgets.find((b) => b.status === 'over') ||
      enrichedBudgets.find((b) => b.status === 'warning') ||
      enrichedBudgets.find((b) => b.status === 'attention') ||
      null
    );
  }, [enrichedBudgets]);

  // Active danger or warning alerts
  const activeAlerts = useMemo(() => {
    return smartAlerts.filter((a) => a.type === 'danger' || a.type === 'warning');
  }, [smartAlerts]);

  // Contributing transactions for selected budget
  const contributingTransactions = useMemo(() => {
    if (!selectedBudgetForDetails) return [];
    return transactions.filter(
      (tx) =>
        Boolean(currentUser?.id) &&
        tx.userId === currentUser?.id &&
        isRealExpenseTx(tx) &&
        tx.category === selectedBudgetForDetails.categoryKey &&
        isTransactionInSelectedMonth(tx, selectedMonth)
    );
  }, [selectedBudgetForDetails, transactions, selectedMonth, currentUser?.id]);

  // Handlers
  const handleOpenCreate = () => {
    const firstCat = expenseCategories[0];
    const initialKey = firstCat ? firstCat.key : '';
    const initialName = firstCat
      ? firstCat.key.startsWith('categories.')
        ? t(firstCat.key)
        : firstCat.name
      : '';
    setFormCategoryKey(initialKey);
    setFormName(initialName);
    setFormLimit('3500000');
    setFormRollover(false);
    setFormAlertsEnabled(true);
    setValidationError(null);
    setIsCreateModalOpen(true);
  };

  const handleSaveCreate = async (e: React.FormEvent) => {
    e.preventDefault();
    if (isSubmitting) return;
    setValidationError(null);

    if (!formCategoryKey || !formName) {
      setValidationError(
        locale === 'en-US'
          ? 'No expense category available to create budget.'
          : 'Không có danh mục chi tiêu hợp lệ để tạo ngân sách.'
      );
      return;
    }

    // Duplicate check
    const isDuplicate = budgets.some(
      (b) => b.categoryKey === formCategoryKey && b.month === selectedMonth
    );
    if (isDuplicate) {
      setValidationError(t('budget.duplicateCategory'));
      return;
    }

    const { valid: isLimitValid, value: limitNum } = parseSafePositiveInteger(formLimit);
    if (!isLimitValid || limitNum < 100000) {
      setValidationError(t('budget.minLimitError'));
      return;
    }

    const pinValue = await challengePinWithValue({
      actionName: locale === 'vi-VN' ? 'Tạo ngân sách mới' : 'Create Budget',
      amount: limitNum,
    });
    if (pinValue === null) return;

    setIsSubmitting(true);
    try {
      const res = await createBudget({
        categoryKey: formCategoryKey,
        name: formName,
        limit: limitNum,
        month: selectedMonth,
        rollover: formRollover,
        alertsEnabled: formAlertsEnabled,
        securityPin: pinValue,
      });
      if (!res.success) {
        setValidationError(res.error || (locale === 'vi-VN' ? 'Tạo ngân sách thất bại.' : 'Failed to create budget.'));
        return;
      }

      setIsCreateModalOpen(false);
      showToast(t('budget.createSuccess'));
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleOpenEdit = (b: BudgetCategoryItem) => {
    setEditingBudget(b);
    setFormName(b.name);
    setFormCategoryKey(b.categoryKey);
    setFormLimit(b.limit.toString());
    setFormRollover(!!b.rollover);
    setFormAlertsEnabled(b.alertsEnabled ?? true);
    setValidationError(null);
  };

  const handleSaveEdit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (isSubmitting) return;
    if (!editingBudget) return;
    setValidationError(null);

    const { valid: isLimitValid, value: limitNum } = parseSafePositiveInteger(formLimit);
    if (!isLimitValid || limitNum < 100000) {
      setValidationError(t('budget.minLimitError'));
      return;
    }

    const pinValue = await challengePinWithValue({
      actionName: locale === 'vi-VN' ? 'Cập nhật ngân sách' : 'Update Budget',
      amount: limitNum,
    });
    if (pinValue === null) return;

    setIsSubmitting(true);
    try {
      const res = await editBudget(editingBudget.id, {
        limit: limitNum,
        rollover: formRollover,
        alertsEnabled: formAlertsEnabled,
        securityPin: pinValue,
      });
      if (!res.success) {
        setValidationError(res.error || (locale === 'vi-VN' ? 'Cập nhật ngân sách thất bại.' : 'Failed to update budget.'));
        return;
      }

      // Also update detail modal if it's currently open for this budget
      if (selectedBudgetForDetails?.id === editingBudget.id) {
        setSelectedBudgetForDetails((prev) =>
          prev
            ? {
                ...prev,
                limit: limitNum,
                rollover: formRollover,
                alertsEnabled: formAlertsEnabled,
              }
            : null
        );
      }

      setEditingBudget(null);
      showToast(t('budget.editSuccess'));
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleConfirmDelete = async () => {
    if (isSubmitting) return;
    if (!deletingBudget) return;
    setValidationError(null);

    const pinValue = await challengePinWithValue({
      actionName: locale === 'vi-VN' ? 'Xóa ngân sách' : 'Delete Budget',
    });
    if (pinValue === null) return;

    setIsSubmitting(true);
    try {
      const res = await deleteBudget(deletingBudget.id, pinValue);
      if (!res.success) {
        setValidationError(res.error || (locale === 'vi-VN' ? 'Xóa ngân sách thất bại.' : 'Failed to delete budget.'));
        return;
      }
      if (selectedBudgetForDetails?.id === deletingBudget.id) {
        setSelectedBudgetForDetails(null);
      }
      setDeletingBudget(null);
      showToast(t('budget.deleteSuccess'));
    } finally {
      setIsSubmitting(false);
    }
  };

  if (isLoading && budgets.length === 0) {
    return <BudgetSkeleton />;
  }

  return (
    <div className="space-y-6 max-w-7xl mx-auto animate-in fade-in-50 duration-200">
      {/* Toast */}
      {toastMessage && (
        <div className="fixed top-6 right-6 z-50 flex items-center gap-2 px-4 py-3 rounded-2xl bg-slate-900 text-white shadow-xl border border-emerald-500/30 text-xs font-semibold animate-in slide-in-from-top-3">
          <CheckCircle2 className="h-4 w-4 text-emerald-400" />
          <span>{toastMessage}</span>
        </div>
      )}

      {/* Header & Controls */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <h2 className="text-xl sm:text-2xl font-bold tracking-tight text-slate-900 dark:text-white">
            {t('budget.pageTitle')}
          </h2>
          <p className="text-xs sm:text-sm text-slate-500 dark:text-slate-400 mt-0.5">
            {t('budget.pageSubtitle')}
          </p>
        </div>

        <div className="flex items-center gap-3">
          {/* Month selector */}
          <Select
            value={selectedMonth}
            onChange={(val) => setSelectedMonth(val)}
            size="sm"
            variant="compact"
            icon={<Calendar className="h-3.5 w-3.5 text-emerald-600 dark:text-emerald-400" />}
            triggerClassName="!rounded-2xl !bg-white dark:!bg-slate-900 !font-bold shadow-2xs"
          >
            {monthOptions.map((opt) => (
              <option key={opt.value} value={opt.value}>
                {opt.label}
              </option>
            ))}
          </Select>

          <Button
            size="sm"
            onClick={handleOpenCreate}
            className="gap-1.5 bg-emerald-600 hover:bg-emerald-700 text-white font-bold cursor-pointer shadow-2xs"
          >
            <Plus className="h-4 w-4" />
            <span>{t('budget.createBudget')}</span>
          </Button>
        </div>
      </div>

      {/* Budget Overview Card - Spending Limit First */}
      <div className="bg-white dark:bg-slate-900 rounded-3xl border border-slate-100 dark:border-slate-800 p-6 shadow-sm">
        <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 items-center">
          {/* Left Column: Dominant Remaining & Financial Equation */}
          <div className="lg:col-span-7 space-y-3">
            <div>
              <span className="text-xs font-semibold text-slate-500 dark:text-slate-400">
                {locale === 'vi-VN' ? `Tổng quan ngân sách tháng ${selectedMonth}` : `Budget overview for ${selectedMonth}`}
              </span>
              <div className="flex items-baseline gap-3 mt-1">
                <span className="text-3xl sm:text-4xl font-extrabold text-slate-900 dark:text-white tabular-nums tracking-tight">
                  {isHidden ? '••••••••' : formatMoney(overallStats.totalRemaining, locale)}
                </span>
                <span
                  className={cn(
                    'text-xs font-bold px-2 py-0.5 rounded-full inline-flex items-center',
                    overallStats.totalRemaining < 0
                      ? 'bg-rose-100 dark:bg-rose-950/60 text-rose-700 dark:text-rose-300'
                      : 'bg-emerald-100 dark:bg-emerald-950/60 text-emerald-700 dark:text-emerald-300'
                  )}
                >
                  {overallStats.totalRemaining < 0
                    ? (locale === 'vi-VN' ? 'vượt hạn mức' : 'over limit')
                    : (locale === 'vi-VN' ? 'khả dụng còn lại' : 'available remaining')}
                </span>
              </div>
            </div>

            {/* Financial Equation: [Tổng hạn mức] - [Đã chi] = [Còn lại] */}
            <div className="inline-flex flex-wrap items-center gap-2 p-2.5 rounded-2xl bg-slate-50 dark:bg-slate-800/60 border border-slate-200/60 dark:border-slate-700/50 text-xs">
              <span className="text-slate-500 dark:text-slate-400">
                {locale === 'vi-VN' ? 'Hạn mức:' : 'Limit:'}{' '}
                <strong className="text-slate-900 dark:text-white font-bold tabular-nums">
                  {isHidden ? '••••••••' : formatMoney(overallStats.totalLimit, locale)}
                </strong>
              </span>
              <span className="text-slate-300 dark:text-slate-600 font-bold">-</span>
              <span className="text-slate-500 dark:text-slate-400">
                {locale === 'vi-VN' ? 'Đã chi:' : 'Spent:'}{' '}
                <strong className="text-rose-600 dark:text-rose-400 font-bold tabular-nums">
                  {isHidden ? '••••••••' : formatMoney(overallStats.totalSpent, locale)}
                </strong>
              </span>
              <span className="text-slate-300 dark:text-slate-600 font-bold">=</span>
              <span className="text-slate-500 dark:text-slate-400">
                {locale === 'vi-VN' ? 'Còn lại:' : 'Remaining:'}{' '}
                <strong
                  className={cn(
                    'font-bold tabular-nums',
                    overallStats.totalRemaining < 0
                      ? 'text-rose-600 dark:text-rose-400'
                      : 'text-emerald-600 dark:text-emerald-400'
                  )}
                >
                  {isHidden ? '••••••••' : formatMoney(overallStats.totalRemaining, locale)}
                </strong>
              </span>
            </div>
          </div>

          {/* Right Column: Progress & Gauge */}
          <div className="lg:col-span-5 p-4 rounded-2xl bg-slate-50 dark:bg-slate-800/40 border border-slate-100 dark:border-slate-800 space-y-2.5">
            <div className="flex items-center justify-between">
              <span className="text-xs font-semibold text-slate-600 dark:text-slate-300">
                {locale === 'vi-VN' ? 'Tiến độ chi tiêu' : 'Spending Progress'}
              </span>
              <div className="flex items-center gap-1.5">
                <span
                  className={cn(
                    'text-xs font-bold px-2 py-0.5 rounded-full',
                    overallStats.overallPercentage >= 90
                      ? 'bg-rose-100 dark:bg-rose-950/60 text-rose-700 dark:text-rose-300'
                      : overallStats.overallPercentage >= 70
                      ? 'bg-amber-100 dark:bg-amber-950/60 text-amber-700 dark:text-amber-300'
                      : 'bg-emerald-100 dark:bg-emerald-950/60 text-emerald-700 dark:text-emerald-300'
                  )}
                >
                  {overallStats.overallPercentage >= 90
                    ? (locale === 'vi-VN' ? 'Cảnh báo' : 'Danger')
                    : overallStats.overallPercentage >= 70
                    ? (locale === 'vi-VN' ? 'Chú ý' : 'Warning')
                    : (locale === 'vi-VN' ? 'An toàn' : 'On track')}
                </span>
                <span className="text-base font-extrabold text-slate-900 dark:text-white tabular-nums">
                  {overallStats.overallPercentage}%
                </span>
              </div>
            </div>

            <div className="h-3 w-full bg-slate-200/70 dark:bg-slate-700 rounded-full overflow-hidden flex">
              <div
                className={cn(
                  'h-full transition-all duration-500 rounded-full',
                  overallStats.overallPercentage >= 90
                    ? 'bg-rose-500'
                    : overallStats.overallPercentage >= 70
                    ? 'bg-amber-500'
                    : 'bg-emerald-500'
                )}
                style={{ width: `${Math.min(100, overallStats.overallPercentage)}%` }}
              />
            </div>

            <div className="flex justify-between text-[11px] font-medium text-slate-500 dark:text-slate-400">
              <span>0 ₫</span>
              <span>{locale === 'vi-VN' ? `Hạn mức: ${formatMoney(overallStats.totalLimit, locale)}` : `Limit: ${formatMoney(overallStats.totalLimit, locale)}`}</span>
            </div>
          </div>
        </div>

        {/* Integrated Alert Strip inside Overview */}
        {activeAlerts.length > 0 ? (
          <div className="mt-5 pt-4 border-t border-slate-100 dark:border-slate-800 space-y-2">
            {activeAlerts.map((alert) => (
              <div
                key={alert.id}
                className={cn(
                  'p-3 rounded-2xl border flex items-center justify-between gap-3 text-xs',
                  alert.type === 'danger'
                    ? 'bg-rose-50/80 dark:bg-rose-950/30 border-rose-200/80 dark:border-rose-900/50 text-rose-900 dark:text-rose-200'
                    : 'bg-amber-50/80 dark:bg-amber-950/30 border-amber-200/80 dark:border-amber-900/50 text-amber-900 dark:text-amber-200'
                )}
              >
                <div className="flex items-center gap-2.5">
                  <div
                    className={cn(
                      'h-6 w-6 rounded-lg flex items-center justify-center shrink-0',
                      alert.type === 'danger'
                        ? 'bg-rose-100 dark:bg-rose-900/60 text-rose-600 dark:text-rose-400'
                        : 'bg-amber-100 dark:bg-amber-900/60 text-amber-600 dark:text-amber-400'
                    )}
                  >
                    <AlertTriangle className="h-3.5 w-3.5" />
                  </div>
                  <div>
                    <span className="font-bold mr-2">{alert.title}:</span>
                    <span className="opacity-90">{alert.desc}</span>
                  </div>
                </div>
              </div>
            ))}
          </div>
        ) : enrichedBudgets.length > 0 ? (
          <div className="mt-5 pt-3.5 border-t border-slate-100 dark:border-slate-800 flex items-center gap-2 text-xs text-emerald-700 dark:text-emerald-400 font-medium">
            <CheckCircle2 className="h-4 w-4 shrink-0 text-emerald-600 dark:text-emerald-400" />
            <span>
              {locale === 'vi-VN'
                ? 'Tất cả các danh mục ngân sách tháng này đều đang trong ngưỡng an toàn.'
                : 'All budget categories for this month are within safe spending thresholds.'}
            </span>
          </div>
        ) : null}
      </div>

      {/* Category Budgets Grid */}
      <div className="space-y-3.5">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2.5">
            <h3 className="text-base font-bold text-slate-900 dark:text-white">
              {t('budget.categoryBudgets')}
            </h3>
            <span className="px-2 py-0.5 rounded-full text-xs font-bold bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-300">
              {enrichedBudgets.length}
            </span>
          </div>
          <Button
            size="sm"
            onClick={handleOpenCreate}
            className="text-xs bg-emerald-600 hover:bg-emerald-700 text-white font-bold gap-1.5 shadow-2xs cursor-pointer"
          >
            <Plus className="h-4 w-4" />
            <span>{t('budget.addCategory')}</span>
          </Button>
        </div>

        {enrichedBudgets.length === 0 ? (
          <div className="p-12 rounded-3xl bg-white dark:bg-slate-900 border border-slate-100 dark:border-slate-800 text-center space-y-3">
            <div className="h-12 w-12 rounded-2xl bg-emerald-50 dark:bg-emerald-950/50 text-emerald-600 dark:text-emerald-400 flex items-center justify-center mx-auto">
              <PieIcon className="h-6 w-6" />
            </div>
            <div className="space-y-1 max-w-sm mx-auto">
              <p className="text-sm font-bold text-slate-800 dark:text-slate-200">
                {t('budget.noBudgetsThisMonth')}
              </p>
              <p className="text-xs text-slate-500 dark:text-slate-400">
                {locale === 'vi-VN'
                  ? 'Thiết lập hạn mức chi tiêu cho các danh mục để kiểm soát tài chính hiệu quả hơn.'
                  : 'Set spending limits for categories to better control your personal finances.'}
              </p>
            </div>
            <Button
              size="sm"
              onClick={handleOpenCreate}
              className="bg-emerald-600 hover:bg-emerald-700 text-white font-bold text-xs gap-1.5 cursor-pointer mt-2"
            >
              <Plus className="h-3.5 w-3.5" />
              <span>{t('budget.createBudget')}</span>
            </Button>
          </div>
        ) : (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
            {enrichedBudgets.map((b) => {
              const isHighRiskFeatured = highRiskBudget && highRiskBudget.id === b.id;
              const isOver = b.status === 'over';
              const isWarning = b.status === 'warning';
              const isAttention = b.status === 'attention';

              return (
                <div
                  key={b.id}
                  onClick={() => setSelectedBudgetForDetails(b)}
                  className={cn(
                    'p-5 rounded-3xl bg-white dark:bg-slate-900 border shadow-xs hover:border-emerald-500/40 dark:hover:border-emerald-500/40 transition-all cursor-pointer group flex flex-col justify-between space-y-4',
                    isHighRiskFeatured
                      ? 'col-span-1 md:col-span-2 lg:col-span-2 border-amber-300/80 dark:border-amber-700/60 ring-2 ring-amber-500/20 bg-linear-to-br from-white to-amber-50/20 dark:from-slate-900 dark:to-amber-950/10'
                      : 'border-slate-100 dark:border-slate-800'
                  )}
                >
                  {/* Card Header */}
                  <div className="flex items-start justify-between">
                    <div className="flex items-center gap-3">
                      <div
                        className={cn(
                          'h-10 w-10 rounded-2xl flex items-center justify-center shrink-0',
                          isOver
                            ? 'bg-rose-100 dark:bg-rose-950 text-rose-600 dark:text-rose-400'
                            : isWarning
                            ? 'bg-amber-100 dark:bg-amber-950 text-amber-600 dark:text-amber-400'
                            : 'bg-emerald-100 dark:bg-emerald-950 text-emerald-600 dark:text-emerald-400'
                        )}
                      >
                        {getCategoryIcon(b.categoryKey)}
                      </div>
                      <div>
                        <div className="flex items-center gap-2">
                          <h4 className="text-sm font-bold text-slate-900 dark:text-white group-hover:text-emerald-600 transition-colors">
                            {getLocalizedBudgetName(b, t, locale)}
                          </h4>
                          {isHighRiskFeatured && (
                            <span className="px-2 py-0.5 rounded-full text-[10px] font-bold bg-amber-100 dark:bg-amber-950/80 text-amber-700 dark:text-amber-300">
                              {locale === 'vi-VN' ? 'Cần chú ý' : 'Attention needed'}
                            </span>
                          )}
                        </div>
                        <span className="text-xs text-slate-500 dark:text-slate-400 font-medium">
                          {b.rollover ? t('budget.rolloverAccumulate') : t('budget.monthlyFresh')}
                        </span>
                      </div>
                    </div>

                    <div className="flex items-center gap-1" onClick={(e) => e.stopPropagation()}>
                      <button
                        type="button"
                        onClick={() => handleOpenEdit(b)}
                        title={t('budget.editBudget')}
                        className="p-1.5 rounded-xl text-slate-400 hover:text-emerald-600 hover:bg-slate-100 dark:hover:bg-slate-800 transition-colors cursor-pointer"
                      >
                        <Edit3 className="h-4 w-4" />
                      </button>
                      <button
                        type="button"
                        onClick={() => setDeletingBudget(b)}
                        title={t('budget.deleteBudget')}
                        className="p-1.5 rounded-xl text-slate-400 hover:text-rose-600 hover:bg-rose-50 dark:hover:bg-rose-950/30 transition-colors cursor-pointer"
                      >
                        <Trash2 className="h-4 w-4" />
                      </button>
                    </div>
                  </div>

                  {/* Amounts Info */}
                  <div className="space-y-1.5">
                    <div className="flex justify-between text-xs">
                      <span className="text-slate-500 dark:text-slate-400 font-medium">{t('budget.used')}</span>
                      <span className={cn(
                        "font-bold tabular-nums",
                        isOver ? "text-rose-600 dark:text-rose-400" : "text-slate-900 dark:text-white"
                      )}>
                        {isHidden ? '••••••••' : formatMoney(b.spent, locale)}
                      </span>
                    </div>
                    <div className="flex justify-between text-xs">
                      <span className="text-slate-500 dark:text-slate-400 font-medium">{t('budget.limit')}</span>
                      <span className="font-semibold text-slate-700 dark:text-slate-300 tabular-nums">
                        {isHidden ? '••••••••' : formatMoney(b.limit, locale)}
                      </span>
                    </div>
                  </div>

                  {/* Progress bar */}
                  <div className="space-y-1.5">
                    <div className="h-2.5 w-full bg-slate-100 dark:bg-slate-800 rounded-full overflow-hidden">
                      <div
                        className={cn(
                          'h-full rounded-full transition-all duration-300',
                          isOver
                            ? 'bg-rose-500'
                            : isWarning
                            ? 'bg-orange-500'
                            : isAttention
                            ? 'bg-amber-500'
                            : 'bg-emerald-500'
                        )}
                        style={{ width: `${Math.min(100, b.percentage)}%` }}
                      />
                    </div>

                    <div className="flex items-center justify-between text-xs">
                      <span
                        className={cn(
                          'font-bold',
                          isOver
                            ? 'text-rose-600 dark:text-rose-400'
                            : isWarning
                            ? 'text-orange-600 dark:text-orange-400'
                            : isAttention
                            ? 'text-amber-600 dark:text-amber-400'
                            : 'text-emerald-600 dark:text-emerald-400'
                        )}
                      >
                        {isOver
                          ? t('budget.statusOver')
                          : `${t('budget.remaining')}: ${formatMoney(b.remaining, locale)}`}
                      </span>
                      <span className="font-bold text-slate-600 dark:text-slate-400 tabular-nums">
                        {b.percentage}%
                      </span>
                    </div>
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </div>

      {/* 1. CREATE BUDGET MODAL */}
      {isCreateModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-xs animate-in fade-in duration-150">
          <form
            onSubmit={handleSaveCreate}
            onKeyDown={(e) => {
              if (e.key === 'Enter' && (e.target as HTMLElement).tagName !== 'TEXTAREA') {
                e.preventDefault();
                handleSaveCreate(e);
              }
            }}
            className="w-full max-w-md bg-white dark:bg-slate-900 rounded-3xl p-6 sm:p-7 shadow-2xl border border-slate-100 dark:border-slate-800 space-y-5 animate-in zoom-in-95"
          >
            <div className="flex items-center justify-between">
              <div className="flex items-center gap-2.5">
                <h3 className="text-base font-bold text-slate-900 dark:text-white">
                  {t('budget.createBudget')}
                </h3>
                <span className="px-2.5 py-0.5 rounded-full text-xs font-bold bg-emerald-50 dark:bg-emerald-950/60 text-emerald-700 dark:text-emerald-300 border border-emerald-200/60 dark:border-emerald-800/60">
                  {selectedMonth}
                </span>
              </div>
              <button
                type="button"
                onClick={() => setIsCreateModalOpen(false)}
                className="p-1.5 rounded-xl text-slate-400 hover:bg-slate-100 dark:hover:bg-slate-800 cursor-pointer"
              >
                <X className="h-4 w-4" />
              </button>
            </div>

            {validationError && (
              <div className="p-3 rounded-2xl bg-rose-50 dark:bg-rose-950/30 border border-rose-200 dark:border-rose-900/50 flex items-center gap-2 text-xs text-rose-600 dark:text-rose-400 font-semibold">
                <AlertTriangle className="h-4 w-4 shrink-0" />
                <span>{validationError}</span>
              </div>
            )}

            <div className="space-y-4 text-xs">
              <div className="space-y-1.5">
                <label className="font-bold text-slate-700 dark:text-slate-300">
                  {t('budget.category')}
                </label>
                {expenseCategories.length === 0 ? (
                  <div className="p-3 rounded-2xl bg-amber-50 dark:bg-amber-950/30 border border-amber-200 dark:border-amber-900/50 text-amber-700 dark:text-amber-300 text-xs font-medium">
                    {locale === 'en-US' ? 'No expense categories available.' : 'Chưa có danh mục chi tiêu khả dụng.'}
                  </div>
                ) : (
                  <Select
                    value={formCategoryKey}
                    onChange={(selectedKey) => {
                      setFormCategoryKey(selectedKey);
                      const matchedCat = expenseCategories.find((c) => c.key === selectedKey);
                      if (matchedCat) {
                        setFormName(
                          matchedCat.key.startsWith('categories.')
                            ? t(matchedCat.key)
                            : matchedCat.name
                        );
                      }
                    }}
                    accentColor="emerald"
                  >
                    {expenseCategories.map((cat) => (
                      <option key={cat.id} value={cat.key}>
                        {cat.key.startsWith('categories.') ? t(cat.key) : (cat.isSystem ? getLocalizedCategoryName(cat.name, t, categories) : cat.name)}
                      </option>
                    ))}
                  </Select>
                )}
              </div>

              <div className="space-y-1.5">
                <div className="flex items-center justify-between">
                  <label className="font-bold text-slate-700 dark:text-slate-300">
                    {t('budget.limit')} (VND) *
                  </label>
                  {(() => {
                    const parsed = parseSafePositiveInteger(formLimit);
                    return parsed.valid && parsed.value > 0 ? (
                      <span className="text-xs font-bold text-emerald-600 dark:text-emerald-400 tabular-nums">
                        ≈ {formatMoney(parsed.value, locale)}
                      </span>
                    ) : null;
                  })()}
                </div>
                <input
                  type="text"
                  inputMode="numeric"
                  placeholder={locale === 'vi-VN' ? 'Ví dụ: 3000000' : 'e.g. 3000000'}
                  value={formLimit}
                  onChange={(e) => setFormLimit(sanitizeIntegerInput(e.target.value))}
                  required
                  className="w-full px-4 py-2.5 text-sm rounded-2xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-slate-900 dark:text-white font-bold"
                />
              </div>

              {/* Grouped Configuration Panel */}
              <div className="p-3.5 rounded-2xl bg-slate-50 dark:bg-slate-800/60 border border-slate-200/70 dark:border-slate-700/60 space-y-3">
                <span className="text-[11px] font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400 block">
                  {locale === 'vi-VN' ? 'Cấu hình hạn mức' : 'Limit Configuration'}
                </span>

                <label className="flex items-start gap-2.5 cursor-pointer">
                  <input
                    type="checkbox"
                    checked={formRollover}
                    onChange={(e) => setFormRollover(e.target.checked)}
                    className="rounded border-slate-300 text-emerald-600 focus:ring-emerald-500 h-4 w-4 mt-0.5 cursor-pointer"
                  />
                  <div className="space-y-0.5">
                    <span className="text-xs text-slate-800 dark:text-slate-200 font-semibold block">
                      {t('budget.rollover')}
                    </span>
                    <span className="text-[11px] text-slate-500 dark:text-slate-400 block">
                      {locale === 'vi-VN'
                        ? 'Cộng dồn số dư chưa dùng sang tháng kế tiếp.'
                        : 'Roll over unused balance to next month.'}
                    </span>
                  </div>
                </label>

                <div className="h-px bg-slate-200/60 dark:bg-slate-700/60" />

                <label className="flex items-start gap-2.5 cursor-pointer">
                  <input
                    type="checkbox"
                    checked={formAlertsEnabled}
                    onChange={(e) => setFormAlertsEnabled(e.target.checked)}
                    className="rounded border-slate-300 text-emerald-600 focus:ring-emerald-500 h-4 w-4 mt-0.5 cursor-pointer"
                  />
                  <div className="space-y-0.5">
                    <span className="text-xs text-slate-800 dark:text-slate-200 font-semibold block">
                      {locale === 'vi-VN' ? 'Bật cảnh báo chi tiêu' : 'Enable spending alerts'}
                    </span>
                    <span className="text-[11px] text-slate-500 dark:text-slate-400 block">
                      {locale === 'vi-VN'
                        ? 'Nhận thông báo khi chi tiêu vượt quá 80% hạn mức.'
                        : 'Receive notification when spending exceeds 80% of limit.'}
                    </span>
                  </div>
                </label>
              </div>
            </div>

            <div className="flex items-center justify-end gap-3 pt-3 border-t border-slate-100 dark:border-slate-800">
              <Button
                type="button"
                variant="outline"
                onClick={() => setIsCreateModalOpen(false)}
                disabled={isSubmitting}
                className="cursor-pointer"
              >
                {t('budget.cancel')}
              </Button>
              <Button
                type="submit"
                disabled={expenseCategories.length === 0 || !formCategoryKey || isSubmitting}
                className="bg-emerald-600 hover:bg-emerald-700 text-white font-bold cursor-pointer disabled:opacity-50 disabled:cursor-not-allowed"
              >
                {isSubmitting ? (locale === 'vi-VN' ? 'Đang lưu...' : 'Saving...') : t('budget.saveBudget')}
              </Button>
            </div>
          </form>
        </div>
      )}

      {/* 2. EDIT BUDGET MODAL */}
      {editingBudget && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-xs animate-in fade-in duration-150">
          <form
            onSubmit={handleSaveEdit}
            onKeyDown={(e) => {
              if (e.key === 'Enter' && (e.target as HTMLElement).tagName !== 'TEXTAREA') {
                e.preventDefault();
                handleSaveEdit(e);
              }
            }}
            className="w-full max-w-md bg-white dark:bg-slate-900 rounded-3xl p-6 sm:p-7 shadow-2xl border border-slate-100 dark:border-slate-800 space-y-5 animate-in zoom-in-95"
          >
            <div className="flex items-center justify-between">
              <div className="flex items-center gap-2.5">
                <h3 className="text-base font-bold text-slate-900 dark:text-white">
                  {t('budget.editBudget')}
                </h3>
                <span className="px-2.5 py-0.5 rounded-full text-xs font-bold bg-emerald-50 dark:bg-emerald-950/60 text-emerald-700 dark:text-emerald-300 border border-emerald-200/60 dark:border-emerald-800/60">
                  {selectedMonth}
                </span>
              </div>
              <button
                type="button"
                onClick={() => setEditingBudget(null)}
                className="p-1.5 rounded-xl text-slate-400 hover:bg-slate-100 dark:hover:bg-slate-800 cursor-pointer"
              >
                <X className="h-4 w-4" />
              </button>
            </div>

            {validationError && (
              <div className="p-3 rounded-2xl bg-rose-50 dark:bg-rose-950/30 border border-rose-200 dark:border-rose-900/50 flex items-center gap-2 text-xs text-rose-600 dark:text-rose-400 font-semibold">
                <AlertTriangle className="h-4 w-4 shrink-0" />
                <span>{validationError}</span>
              </div>
            )}

            <div className="space-y-4 text-xs">
              <div className="space-y-1.5">
                <label className="font-bold text-slate-700 dark:text-slate-300">
                  {t('budget.category')}
                </label>
                <input
                  type="text"
                  value={formName}
                  disabled
                  readOnly
                  className="w-full px-3.5 py-2.5 rounded-2xl border border-slate-200 dark:border-slate-700 bg-slate-100 dark:bg-slate-800 text-slate-500 font-semibold cursor-not-allowed"
                />
              </div>

              <div className="space-y-1.5">
                <div className="flex items-center justify-between">
                  <label className="font-bold text-slate-700 dark:text-slate-300">
                    {t('budget.limit')} (VND) *
                  </label>
                  {(() => {
                    const parsed = parseSafePositiveInteger(formLimit);
                    return parsed.valid && parsed.value > 0 ? (
                      <span className="text-xs font-bold text-emerald-600 dark:text-emerald-400 tabular-nums">
                        ≈ {formatMoney(parsed.value, locale)}
                      </span>
                    ) : null;
                  })()}
                </div>
                <input
                  type="text"
                  inputMode="numeric"
                  value={formLimit}
                  onChange={(e) => setFormLimit(sanitizeIntegerInput(e.target.value))}
                  required
                  className="w-full px-4 py-2.5 text-sm rounded-2xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-slate-900 dark:text-white font-bold"
                />
              </div>

              {/* Grouped Configuration Panel */}
              <div className="p-3.5 rounded-2xl bg-slate-50 dark:bg-slate-800/60 border border-slate-200/70 dark:border-slate-700/60 space-y-3">
                <span className="text-[11px] font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400 block">
                  {locale === 'vi-VN' ? 'Cấu hình hạn mức' : 'Limit Configuration'}
                </span>

                <label className="flex items-start gap-2.5 cursor-pointer">
                  <input
                    type="checkbox"
                    checked={formRollover}
                    onChange={(e) => setFormRollover(e.target.checked)}
                    className="rounded border-slate-300 text-emerald-600 focus:ring-emerald-500 h-4 w-4 mt-0.5 cursor-pointer"
                  />
                  <div className="space-y-0.5">
                    <span className="text-xs text-slate-800 dark:text-slate-200 font-semibold block">
                      {t('budget.rollover')}
                    </span>
                    <span className="text-[11px] text-slate-500 dark:text-slate-400 block">
                      {locale === 'vi-VN'
                        ? 'Cộng dồn số dư chưa dùng sang tháng kế tiếp.'
                        : 'Roll over unused balance to next month.'}
                    </span>
                  </div>
                </label>

                <div className="h-px bg-slate-200/60 dark:bg-slate-700/60" />

                <label className="flex items-start gap-2.5 cursor-pointer">
                  <input
                    type="checkbox"
                    checked={formAlertsEnabled}
                    onChange={(e) => setFormAlertsEnabled(e.target.checked)}
                    className="rounded border-slate-300 text-emerald-600 focus:ring-emerald-500 h-4 w-4 mt-0.5 cursor-pointer"
                  />
                  <div className="space-y-0.5">
                    <span className="text-xs text-slate-800 dark:text-slate-200 font-semibold block">
                      {locale === 'vi-VN' ? 'Bật cảnh báo chi tiêu' : 'Enable spending alerts'}
                    </span>
                    <span className="text-[11px] text-slate-500 dark:text-slate-400 block">
                      {locale === 'vi-VN'
                        ? 'Nhận thông báo khi chi tiêu vượt quá 80% hạn mức.'
                        : 'Receive notification when spending exceeds 80% of limit.'}
                    </span>
                  </div>
                </label>
              </div>
            </div>

            <div className="flex items-center justify-end gap-3 pt-3 border-t border-slate-100 dark:border-slate-800">
              <Button
                type="button"
                variant="outline"
                onClick={() => setEditingBudget(null)}
                disabled={isSubmitting}
                className="cursor-pointer"
              >
                {t('budget.cancel')}
              </Button>
              <Button
                type="submit"
                disabled={isSubmitting}
                className="bg-emerald-600 hover:bg-emerald-700 text-white font-bold cursor-pointer disabled:opacity-50 disabled:cursor-not-allowed"
              >
                {isSubmitting ? (locale === 'vi-VN' ? 'Đang lưu...' : 'Saving...') : t('budget.saveBudget')}
              </Button>
            </div>
          </form>
        </div>
      )}

      {/* 3. DELETE BUDGET CONFIRMATION MODAL */}
      {deletingBudget && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-xs animate-in fade-in duration-150">
          <div className="w-full max-w-sm bg-white dark:bg-slate-900 rounded-3xl p-6 shadow-2xl border border-slate-100 dark:border-slate-800 space-y-4 animate-in zoom-in-95">
            <div className="h-12 w-12 rounded-2xl bg-rose-100 dark:bg-rose-950 text-rose-600 dark:text-rose-400 flex items-center justify-center mx-auto">
              <Trash2 className="h-6 w-6" />
            </div>
            <div className="text-center space-y-1.5">
              <h3 className="text-base font-bold text-slate-900 dark:text-white">
                {t('budget.confirmDeleteTitle')}
              </h3>
              <p className="text-xs text-slate-400 leading-relaxed">
                {t('budget.confirmDeleteDesc', { name: getLocalizedBudgetName(deletingBudget, t, locale) })}
              </p>
            </div>
            {validationError && (
              <div className="p-3 rounded-2xl bg-rose-50 dark:bg-rose-950/30 border border-rose-200 dark:border-rose-900/50 flex items-center gap-2 text-xs text-rose-600 dark:text-rose-400 font-semibold">
                <AlertTriangle className="h-4 w-4 shrink-0" />
                <span>{validationError}</span>
              </div>
            )}

            <div className="flex items-center justify-center gap-3 pt-2">
              <Button
                type="button"
                variant="outline"
                onClick={() => {
                  setDeletingBudget(null);
                  setValidationError(null);
                }}
                disabled={isSubmitting}
                className="flex-1 text-xs cursor-pointer"
              >
                {t('budget.cancel')}
              </Button>
              <Button
                type="button"
                onClick={handleConfirmDelete}
                disabled={isSubmitting}
                className="flex-1 text-xs bg-rose-600 hover:bg-rose-700 text-white font-bold cursor-pointer disabled:opacity-50"
              >
                {isSubmitting ? (locale === 'vi-VN' ? 'Đang xóa...' : 'Deleting...') : t('budget.deleteBudget')}
              </Button>
            </div>
          </div>
        </div>
      )}

      {/* 4. SPENDING DETAILS MODAL */}
      {selectedBudgetForDetails && (() => {
        const detailBudget =
          enrichedBudgets.find((b) => b.id === selectedBudgetForDetails.id) || selectedBudgetForDetails;
        const spentVal = detailBudget.spent ?? 0;
        const remainingVal = detailBudget.remaining ?? Math.max(0, detailBudget.limit - spentVal);

        return (
          <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-xs animate-in fade-in duration-150">
            <div className="w-full max-w-lg bg-white dark:bg-slate-900 rounded-3xl p-6 sm:p-7 shadow-2xl border border-slate-100 dark:border-slate-800 space-y-5 animate-in zoom-in-95">
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-2.5">
                  <div className="h-10 w-10 rounded-2xl bg-emerald-100 dark:bg-emerald-950 text-emerald-600 dark:text-emerald-400 flex items-center justify-center">
                    {getCategoryIcon(detailBudget.categoryKey)}
                  </div>
                  <div>
                    <h3 className="text-base font-bold text-slate-900 dark:text-white">
                      {getLocalizedBudgetName(detailBudget, t, locale)}
                    </h3>
                    <span className="text-xs text-slate-400">
                      {t('budget.limit')}: {formatMoney(detailBudget.limit, locale)}
                    </span>
                  </div>
                </div>
                <button
                  type="button"
                  onClick={() => setSelectedBudgetForDetails(null)}
                  className="p-1.5 rounded-xl text-slate-400 hover:bg-slate-100 dark:hover:bg-slate-800 cursor-pointer"
                >
                  <X className="h-4 w-4" />
                </button>
              </div>

              {/* Quick Metrics Bar in Detail Drawer */}
              <div className="grid grid-cols-2 gap-3 p-3.5 bg-slate-50 dark:bg-slate-800/40 rounded-2xl border border-slate-100 dark:border-slate-800 text-xs">
                <div>
                  <span className="text-slate-400">{t('budget.used')}</span>
                  <p className="text-sm font-bold text-rose-600 dark:text-rose-400 tabular-nums mt-0.5">
                    {formatMoney(spentVal, locale)}
                  </p>
                </div>
                <div>
                  <span className="text-slate-400">{t('budget.remaining')}</span>
                  <p className="text-sm font-bold text-emerald-600 dark:text-emerald-400 tabular-nums mt-0.5">
                    {formatMoney(remainingVal, locale)}
                  </p>
                </div>
              </div>

            <div className="space-y-3">
              <span className="text-xs font-bold text-slate-700 dark:text-slate-300">
                {t('budget.contributingTx')} ({contributingTransactions.length})
              </span>

              <div className="max-h-60 overflow-y-auto divide-y divide-slate-100 dark:divide-slate-800 border border-slate-100 dark:border-slate-800 rounded-2xl p-2 bg-slate-50/50 dark:bg-slate-800/30">
                {contributingTransactions.length === 0 ? (
                  <p className="py-8 text-center text-xs text-slate-400 font-medium">
                    {t('budget.noTransactions')}
                  </p>
                ) : (
                  contributingTransactions.map((tx) => (
                    <div key={tx.id} className="py-2.5 px-3 flex items-center justify-between text-xs">
                      <div>
                        <p className="font-bold text-slate-800 dark:text-slate-200">{getLocalizedTxTitle(tx, locale)}</p>
                        <span className="text-[11px] text-slate-400">
                          {new Date(tx.date).toLocaleDateString(locale)} • {getLocalizedWalletName(tx.walletId, tx.walletName, locale)}
                        </span>
                      </div>
                      <span className="font-bold text-rose-600 dark:text-rose-400 tabular-nums">
                        {formatMoney(tx.amount, locale, { showSign: true, forceSign: true })}
                      </span>
                    </div>
                  ))
                )}
              </div>
            </div>

            <div className="flex items-center justify-between pt-2 border-t border-slate-100 dark:border-slate-800">
              <div className="flex items-center gap-2">
                <Button
                  size="sm"
                  variant="outline"
                  onClick={() => {
                    handleOpenEdit(selectedBudgetForDetails);
                  }}
                  className="text-xs font-semibold gap-1 cursor-pointer"
                >
                  <Edit3 className="h-3.5 w-3.5" />
                  <span>{t('budget.editBudget')}</span>
                </Button>
                <Button
                  size="sm"
                  variant="outline"
                  onClick={() => {
                    setDeletingBudget(selectedBudgetForDetails);
                  }}
                  className="text-xs text-rose-600 dark:text-rose-400 border-rose-200 dark:border-rose-900/40 hover:bg-rose-50 dark:hover:bg-rose-950/30 font-semibold gap-1 cursor-pointer"
                >
                  <Trash2 className="h-3.5 w-3.5" />
                  <span>{t('budget.deleteBudget')}</span>
                </Button>
              </div>

              <Button size="sm" onClick={() => setSelectedBudgetForDetails(null)} className="cursor-pointer">
                {t('common.close')}
              </Button>
            </div>
          </div>
        </div>
      ); })()}
    </div>
  );
};
