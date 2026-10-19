import React, { useMemo, useState } from 'react';
import {
  Calendar as CalendarIcon,
  TrendingUp,
  TrendingDown,
  Eye,
  EyeOff,
  Plus,
  Minus,
  ArrowUpRight,
  Send,
  Users,
  PiggyBank,
  ArrowDownRight,
  Sparkles,
  BarChart3,
  Calendar,
  Tag,
  AlertCircle,
  PieChart as PieIcon,
  Clock,
  Briefcase,
  Utensils,
  Car,
  Tv,
  ArrowDownLeft,
  ChevronRight,
  ExternalLink,
  ShieldCheck,
  CheckCircle2,
} from 'lucide-react';
import { NavLink, useNavigate } from 'react-router-dom';
import { useI18n } from '../../i18n/I18nContext';
import { useAuth } from '../../providers/AuthProvider';
import { useFinancial } from '../../providers/FinancialProvider';
import { useGroup } from '../../providers/GroupProvider';
import { useBalanceVisibility } from '../../providers/BalanceVisibilityProvider';
import { formatMoney } from '../../utils/money';
import { formatDate, getTimeOfDay } from '../../utils/date';
import { getLocalizedCategoryName } from '../../utils/localizationHelpers';
import { Button } from '../../components/ui/Button';
import { cn } from '../../utils/cn';
import {
  getTransactionEffectiveDate,
  isUserCompletedTx,
  getAnalyticsFlow,
  getLast6CalendarMonths,
  getAnalyticsCategoryKey,
  EXPENSE_RED_PALETTE,
  INCOME_GREEN_PALETTE,
  MonthSlot,
} from '../../utils/analyticsHelpers';
import { DashboardSummary } from '../../types';

export const DashboardFullParityRedesign: React.FC = () => {
  const { t, locale } = useI18n();
  const { currentUser } = useAuth();
  const {
    isLoading,
    dashboardSummary: baseSummary,
    attentionItems,
    transactions,
    categories,
    wallets,
  } = useFinancial();
  const { groups } = useGroup();
  const { isHidden, toggleVisibility } = useBalanceVisibility();
  const navigate = useNavigate();

  // Chart state toggles
  const [barChartFlow, setBarChartFlow] = useState<'expense' | 'income'>('expense');
  const [donutFlow, setDonutFlow] = useState<'expense' | 'income'>('expense');
  const [hoveredSlice, setHoveredSlice] = useState<string | null>(null);

  // Derive real group fund obligations
  const { groupFundBalance, userPendingFundObligations } = useMemo(() => {
    let fundBalance = 0;
    let pendingObligations = 0;
    const currentUserId = currentUser?.id;

    if (currentUserId) {
      groups.forEach((group) => {
        const isMember = group.members.some((m) => m.userId === currentUserId && m.status === 'active');
        if (isMember) {
          fundBalance += group.sharedFund?.balance || 0;
          (group.fundRounds || []).forEach((r) => {
            if (r.status === 'Active') {
              const mem = r.members.find((m) => m.userId === currentUserId);
              if (mem && mem.status !== 'Paid') {
                pendingObligations += Math.max(0, mem.requiredAmount - mem.paidAmount);
              }
            }
          });
        }
      });
    }

    return {
      groupFundBalance: fundBalance,
      userPendingFundObligations: pendingObligations,
    };
  }, [groups, currentUser]);

  const dashboardSummary: DashboardSummary = useMemo(() => {
    return {
      ...baseSummary,
      othersOweUser: 0,
      userOwesOthers: 0,
      groupFundBalance,
      userPendingFundObligations,
    };
  }, [baseSummary, groupFundBalance, userPendingFundObligations]);

  // Greeting copy
  const timeOfDay = getTimeOfDay();
  const displayName = (() => {
    const raw = currentUser?.name || 'User';
    const parts = raw.split(' ');
    return parts.length > 2 ? parts.slice(-2).join(' ') : raw;
  })();

  const greetingText =
    timeOfDay === 'morning'
      ? t('dashboard.greetingMorning', { name: displayName })
      : timeOfDay === 'afternoon'
      ? t('dashboard.greetingAfternoon', { name: displayName })
      : t('dashboard.greetingEvening', { name: displayName });

  const currentDateFormatted = formatDate(new Date(), locale);

  // Format main balance value and symbol
  const rawFormatted = formatMoney(dashboardSummary.totalBalance, locale, { isHidden });
  let valuePart = rawFormatted;
  let symbolPart = '';
  if (!isHidden && locale === 'vi-VN') {
    const parts = rawFormatted.split(' ');
    if (parts.length > 1) {
      symbolPart = parts.pop() || '';
      valuePart = parts.join(' ');
    }
  }

  // Last 6 months bar chart calculation
  const monthlyData = useMemo(() => {
    const slots = getLast6CalendarMonths(new Date());
    return slots.map((slot) => {
      let total = 0;
      let count = 0;
      transactions.forEach((tx) => {
        if (!isUserCompletedTx(tx, currentUser?.id)) return;
        if (getAnalyticsFlow(tx) !== barChartFlow) return;
        const txDate = getTransactionEffectiveDate(tx);
        if (!txDate) return;
        if (txDate.getMonth() === slot.month && txDate.getFullYear() === slot.year) {
          total += Math.abs(tx.amount);
          count += 1;
        }
      });
      return { ...slot, total, count };
    });
  }, [transactions, currentUser?.id, barChartFlow]);

  const totalSixMonths = useMemo(() => monthlyData.reduce((acc, curr) => acc + curr.total, 0), [monthlyData]);
  const maxMonthValue = useMemo(() => Math.max(1000000, ...monthlyData.map((m) => m.total)), [monthlyData]);
  const averageMonthly = Math.round(totalSixMonths / 6);

  // Category donut calculation
  const now = new Date();
  const currentMonth = now.getMonth();
  const currentYear = now.getFullYear();

  const { categorySlices, totalAmount } = useMemo(() => {
    const categoryTotals: Record<string, number> = {};
    let total = 0;

    transactions.forEach((tx) => {
      if (!isUserCompletedTx(tx, currentUser?.id)) return;
      if (getAnalyticsFlow(tx) !== donutFlow) return;
      const txDate = getTransactionEffectiveDate(tx);
      if (!txDate || txDate.getMonth() !== currentMonth || txDate.getFullYear() !== currentYear) return;

      const rawCat = tx.category?.trim() || '';
      const matchedCat = categories.find((c) => c.id === rawCat || c.key === rawCat || c.name === rawCat);
      const catKey = matchedCat ? matchedCat.id : getAnalyticsCategoryKey(tx, donutFlow);
      const amt = Math.abs(tx.amount);

      categoryTotals[catKey] = (categoryTotals[catKey] || 0) + amt;
      total += amt;
    });

    const palette = donutFlow === 'expense' ? EXPENSE_RED_PALETTE : INCOME_GREEN_PALETTE;
    const slices = Object.entries(categoryTotals)
      .filter(([_, amt]) => amt > 0)
      .sort((a, b) => b[1] - a[1])
      .map(([key, amount], index) => {
        const matchingCategory = categories.find((c) => c.id === key || c.key === key || c.name === key);
        let name = '';
        if (matchingCategory) {
          name = matchingCategory.isSystem
            ? matchingCategory.key.startsWith('categories.')
              ? t(matchingCategory.key)
              : getLocalizedCategoryName(matchingCategory.name, t, categories)
            : matchingCategory.name;
        } else {
          name = getLocalizedCategoryName(key, t, categories);
        }
        const percentage = total > 0 ? Math.round((amount / total) * 100) : 0;
        const color = matchingCategory?.color || palette[index % palette.length];
        return { key, name, amount, percentage, color };
      });

    return { categorySlices: slices, totalAmount: total };
  }, [transactions, categories, currentUser?.id, donutFlow, currentMonth, currentYear, t]);

  // Donut geometry
  const radius = 64;
  const strokeWidth = 24;
  const circumference = 2 * Math.PI * radius;
  let cumulativeOffset = 0;
  const svgSegments = categorySlices.map((slice) => {
    const fraction = totalAmount > 0 ? slice.amount / totalAmount : 0;
    const strokeDash = fraction * circumference;
    const offset = cumulativeOffset;
    cumulativeOffset += strokeDash;
    return {
      ...slice,
      strokeDasharray: `${strokeDash} ${circumference - strokeDash}`,
      strokeDashoffset: -offset,
    };
  });

  // Recent 5 transactions
  const recentTransactions = useMemo(() => {
    return [...transactions]
      .sort((a, b) => {
        const timeA = new Date(a.date).getTime();
        const timeB = new Date(b.date).getTime();
        return (isNaN(timeB) ? 0 : timeB) - (isNaN(timeA) ? 0 : timeA);
      })
      .slice(0, 5);
  }, [transactions]);

  return (
    <div className="space-y-6">
      {/* 1. Header with Clean Greeting & Date Chip */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 pb-1">
        <div>
          <h2 className="text-xl sm:text-2xl font-extrabold tracking-tight text-slate-900 dark:text-white">
            {greetingText}
          </h2>
          <p className="text-xs sm:text-sm text-slate-500 dark:text-slate-400 mt-0.5">
            {t('dashboard.greetingSubtitle')}
          </p>
        </div>

        <div className="flex items-center gap-2 self-start sm:self-auto">
          <div className="flex items-center gap-1.5 px-3 py-1.5 rounded-xl bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 text-xs font-semibold text-slate-700 dark:text-slate-300 shadow-2xs">
            <CalendarIcon className="h-3.5 w-3.5 text-slate-400" />
            <span>{currentDateFormatted}</span>
          </div>
          <span className="text-[10px] font-bold px-2.5 py-1 rounded-full bg-emerald-100 dark:bg-emerald-950/60 text-emerald-800 dark:text-emerald-300 border border-emerald-300 dark:border-emerald-800">
            Preview Chỉ Xem Trước
          </span>
        </div>
      </div>

      {/* 2. Primary Financial Hub: Available Balance Panel + 4 Core Metrics Grid */}
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-5 items-stretch">
        {/* Left Column: Balance Card with Integrated Action Controls */}
        <div className="lg:col-span-6 flex flex-col">
          <div className="bg-white dark:bg-slate-900 rounded-2xl p-5 sm:p-6 border border-slate-200/80 dark:border-slate-800 shadow-xs flex flex-col justify-between h-full">
            <div>
              <div className="flex items-center justify-between">
                <span className="text-xs font-bold uppercase tracking-wider text-slate-400">
                  {locale === 'vi-VN' ? 'Số dư ví khả dụng' : 'Available Wallet Balance'}
                </span>
                <button
                  type="button"
                  onClick={toggleVisibility}
                  className="p-1 rounded-md text-slate-400 hover:text-slate-600 dark:hover:text-slate-200 hover:bg-slate-100 dark:hover:bg-slate-800 transition-colors cursor-pointer"
                  aria-label={isHidden ? 'Hiện số dư' : 'Ẩn số dư'}
                >
                  {isHidden ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
                </button>
              </div>

              <div className="mt-2 flex items-baseline font-mono">
                <h2 className="text-3xl sm:text-4xl font-extrabold tracking-tight text-slate-900 dark:text-white tabular-nums">
                  {valuePart}
                  {symbolPart && (
                    <span className="text-xl sm:text-2xl font-normal text-slate-400 ml-1.5">
                      {symbolPart}
                    </span>
                  )}
                </h2>
              </div>

              {dashboardSummary.totalGoalsReserved && dashboardSummary.totalGoalsReserved > 0 ? (
                <p className="text-xs text-slate-500 dark:text-slate-400 mt-2">
                  {locale === 'vi-VN'
                    ? `Tổng tài sản theo dõi: ${formatMoney(dashboardSummary.totalTrackedAssets ?? (dashboardSummary.totalBalance + dashboardSummary.totalGoalsReserved), locale, { isHidden })} (gồm ${formatMoney(dashboardSummary.totalGoalsReserved, locale, { isHidden })} trong mục tiêu)`
                    : `Total tracked assets: ${formatMoney(dashboardSummary.totalTrackedAssets ?? (dashboardSummary.totalBalance + dashboardSummary.totalGoalsReserved), locale, { isHidden })}`}
                </p>
              ) : null}

              {typeof dashboardSummary.balanceChangePercentage === 'number' && (
                <p className="text-xs text-emerald-600 dark:text-emerald-400 mt-2 font-semibold flex items-center gap-1">
                  <TrendingUp className="h-3.5 w-3.5" />
                  <span>+{dashboardSummary.balanceChangePercentage}% {t('dashboard.balanceGrowth')}</span>
                </p>
              )}
            </div>

            {/* Quick Financial Action Toolbar */}
            <div className="pt-5 mt-4 border-t border-slate-100 dark:border-slate-800/80 flex items-center gap-2.5">
              <Button
                size="sm"
                variant="outline"
                onClick={() => navigate('/transactions?tab=income')}
                className="flex-1 text-xs font-semibold gap-1.5 text-emerald-700 dark:text-emerald-300 border-emerald-200 dark:border-emerald-800/60 hover:bg-emerald-50 dark:hover:bg-emerald-950/40 cursor-pointer"
              >
                <Plus className="h-3.5 w-3.5" />
                <span>{t('quickActions.income')}</span>
              </Button>

              <Button
                size="sm"
                variant="outline"
                onClick={() => navigate('/transactions?tab=expense')}
                className="flex-1 text-xs font-semibold gap-1.5 text-rose-700 dark:text-rose-300 border-rose-200 dark:border-rose-800/60 hover:bg-rose-50 dark:hover:bg-rose-950/40 cursor-pointer"
              >
                <Minus className="h-3.5 w-3.5" />
                <span>{t('quickActions.expense')}</span>
              </Button>

              <Button
                size="sm"
                variant="outline"
                onClick={() => navigate('/transfer')}
                className="px-3 text-xs font-semibold text-slate-700 dark:text-slate-300 border-slate-200 dark:border-slate-700 hover:bg-slate-50 dark:hover:bg-slate-800 cursor-pointer"
                title={t('quickActions.transfer')}
              >
                <ArrowUpRight className="h-4 w-4" />
                <span className="hidden sm:inline ml-1">{locale === 'vi-VN' ? 'Chuyển' : 'Transfer'}</span>
              </Button>

              <Button
                size="sm"
                variant="outline"
                onClick={() => navigate('/groups')}
                className="px-3 text-xs font-semibold text-indigo-700 dark:text-indigo-300 border-indigo-200 dark:border-indigo-800/60 hover:bg-indigo-50 dark:hover:bg-indigo-950/40 cursor-pointer"
                title={t('quickActions.groups')}
              >
                <Users className="h-4 w-4" />
                <span className="hidden sm:inline ml-1">{locale === 'vi-VN' ? 'Nhóm' : 'Groups'}</span>
              </Button>
            </div>
          </div>
        </div>

        {/* Right Column: 4 Metric Cards + Net Cash Flow Bar */}
        <div className="lg:col-span-6 flex flex-col justify-between gap-3">
          <div className="grid grid-cols-2 gap-3 flex-1">
            {/* Income Card */}
            <div className="bg-white dark:bg-slate-900 rounded-2xl p-4 border border-slate-200/80 dark:border-slate-800 border-l-[3px] border-l-emerald-500 shadow-xs flex flex-col justify-between">
              <div className="flex items-center justify-between gap-1 mb-1">
                <span className="text-[11px] font-bold uppercase tracking-wider text-slate-400">
                  {t('dashboard.incomeThisMonth')}
                </span>
                <div className="h-6 w-6 rounded-lg bg-emerald-50 dark:bg-emerald-950/60 text-emerald-600 dark:text-emerald-400 flex items-center justify-center">
                  <ArrowDownRight className="h-3.5 w-3.5" />
                </div>
              </div>
              <div className="text-base sm:text-lg font-bold font-mono text-emerald-600 dark:text-emerald-400 tabular-nums">
                {formatMoney(dashboardSummary.incomeThisMonth, locale, { showSign: true, forceSign: true, isHidden })}
              </div>
            </div>

            {/* Expense Card */}
            <div className="bg-white dark:bg-slate-900 rounded-2xl p-4 border border-slate-200/80 dark:border-slate-800 border-l-[3px] border-l-rose-500 shadow-xs flex flex-col justify-between">
              <div className="flex items-center justify-between gap-1 mb-1">
                <span className="text-[11px] font-bold uppercase tracking-wider text-slate-400">
                  {t('dashboard.expenseThisMonth')}
                </span>
                <div className="h-6 w-6 rounded-lg bg-rose-50 dark:bg-rose-950/60 text-rose-600 dark:text-rose-400 flex items-center justify-center">
                  <ArrowUpRight className="h-3.5 w-3.5" />
                </div>
              </div>
              <div className="text-base sm:text-lg font-bold font-mono text-rose-600 dark:text-rose-400 tabular-nums">
                {isHidden ? '••••••••' : `−${formatMoney(dashboardSummary.expenseThisMonth, locale).replace('+', '').replace('−', '')}`}
              </div>
            </div>

            {/* Budget Remaining Card */}
            <div className="bg-white dark:bg-slate-900 rounded-2xl p-4 border border-slate-200/80 dark:border-slate-800 border-l-[3px] border-l-sky-500 shadow-xs flex flex-col justify-between">
              <div className="flex items-center justify-between gap-1 mb-1">
                <span className="text-[11px] font-bold uppercase tracking-wider text-slate-400">
                  {t('dashboard.budgetRemaining')}
                </span>
                <div className="h-6 w-6 rounded-lg bg-sky-50 dark:bg-sky-950/60 text-sky-600 dark:text-sky-400 flex items-center justify-center">
                  <PiggyBank className="h-3.5 w-3.5" />
                </div>
              </div>
              <div className="text-base sm:text-lg font-bold font-mono text-slate-900 dark:text-white tabular-nums">
                {formatMoney(dashboardSummary.budgetRemaining, locale, { isHidden })}
              </div>
            </div>

            {/* Group Fund Card */}
            <div className="bg-white dark:bg-slate-900 rounded-2xl p-4 border border-slate-200/80 dark:border-slate-800 border-l-[3px] border-l-indigo-500 shadow-xs flex flex-col justify-between">
              <div className="flex items-center justify-between gap-1 mb-1">
                <span className="text-[11px] font-bold uppercase tracking-wider text-slate-400 truncate">
                  {dashboardSummary.userPendingFundObligations && dashboardSummary.userPendingFundObligations > 0
                    ? (locale === 'vi-VN' ? 'Cần góp quỹ' : 'Pending Fund')
                    : (locale === 'vi-VN' ? 'Quỹ nhóm' : 'Group Fund')}
                </span>
                <div className="h-6 w-6 rounded-lg bg-indigo-50 dark:bg-indigo-950/60 text-indigo-600 dark:text-indigo-400 flex items-center justify-center">
                  <Users className="h-3.5 w-3.5" />
                </div>
              </div>
              <div className="text-base sm:text-lg font-bold font-mono text-indigo-600 dark:text-indigo-400 tabular-nums">
                {formatMoney(
                  dashboardSummary.userPendingFundObligations && dashboardSummary.userPendingFundObligations > 0
                    ? dashboardSummary.userPendingFundObligations
                    : dashboardSummary.groupFundBalance || 0,
                  locale,
                  { isHidden }
                )}
              </div>
            </div>
          </div>

          {/* Integrated Net Monthly Cash Flow Strip */}
          <div className="flex items-center justify-between px-4 py-2.5 rounded-xl bg-slate-50 dark:bg-slate-900/60 border border-slate-200/70 dark:border-slate-800 text-xs">
            <div className="flex items-center gap-2 text-slate-600 dark:text-slate-300 font-medium">
              <span className={cn('w-2 h-2 rounded-full', dashboardSummary.cashFlowDifference > 0 ? 'bg-emerald-500' : dashboardSummary.cashFlowDifference < 0 ? 'bg-rose-500' : 'bg-slate-400')} />
              <span>{locale === 'vi-VN' ? 'Dòng tiền ròng tháng này:' : 'Net monthly cash flow:'}</span>
            </div>
            <div className="flex items-center gap-2 font-mono">
              <span className={cn('font-bold text-xs sm:text-sm tabular-nums', dashboardSummary.cashFlowDifference > 0 ? 'text-emerald-600 dark:text-emerald-400' : dashboardSummary.cashFlowDifference < 0 ? 'text-rose-600 dark:text-rose-400' : 'text-slate-700 dark:text-slate-300')}>
                {formatMoney(dashboardSummary.cashFlowDifference, locale, {
                  showSign: dashboardSummary.cashFlowDifference !== 0,
                  forceSign: dashboardSummary.cashFlowDifference !== 0,
                  isHidden,
                })}
              </span>
              {dashboardSummary.cashFlowDifference === 0 && !isHidden && (
                <span className="text-[11px] text-slate-400 font-sans font-normal">
                  ({locale === 'vi-VN' ? 'Cân bằng' : 'Balanced'})
                </span>
              )}
            </div>
          </div>
        </div>
      </div>

      {/* 3. Attention & System Notifications (If items exist) */}
      {attentionItems.length > 0 && (
        <div className="p-4 rounded-2xl bg-amber-50/80 dark:bg-amber-950/20 border border-amber-200/80 dark:border-amber-900/40 text-xs space-y-2">
          <div className="flex items-center gap-2 font-bold text-amber-900 dark:text-amber-200">
            <AlertCircle className="h-4 w-4 text-amber-600" />
            <span>{locale === 'vi-VN' ? 'Việc cần chú ý trong tài khoản' : 'Items Needing Attention'}</span>
          </div>
          <div className="space-y-1.5 pl-6 text-slate-700 dark:text-slate-300">
            {attentionItems.map((item) => (
              <div key={item.id} className="flex items-center justify-between gap-3">
                <span>{item.title}: {item.message}</span>
                {item.actionRoute && (
                  <NavLink to={item.actionRoute} className="text-emerald-600 dark:text-emerald-400 font-bold hover:underline shrink-0">
                    {locale === 'vi-VN' ? 'Xem ngay →' : 'View →'}
                  </NavLink>
                )}
              </div>
            ))}
          </div>
        </div>
      )}

      {/* 4. Analytics Section: 6 Months Trend Bar Chart + Current Month Category Donut */}
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 items-stretch">
        {/* Left (7 cols): Last 6 Months Trend Bar Chart with Semantic Toggle */}
        <div className="lg:col-span-7 bg-white dark:bg-slate-900 rounded-2xl p-5 sm:p-6 border border-slate-200/80 dark:border-slate-800 shadow-xs flex flex-col justify-between">
          <div>
            <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 pb-4 border-b border-slate-100 dark:border-slate-800">
              <div>
                <h3 className="text-sm font-bold text-slate-900 dark:text-white flex items-center gap-2">
                  <BarChart3 className="h-4 w-4 text-slate-400" />
                  <span>{locale === 'vi-VN' ? 'Thu / Chi 6 tháng gần nhất' : 'Last 6 Months Trend'}</span>
                </h3>
                <p className="text-xs text-slate-400 font-medium mt-0.5">
                  {(locale === 'vi-VN' ? monthlyData[0]?.labelVi : monthlyData[0]?.labelEn)} - {(locale === 'vi-VN' ? monthlyData[5]?.labelVi : monthlyData[5]?.labelEn)}
                </p>
              </div>

              {/* Semantic Flow Toggle: Chi (Expense) vs Thu (Income) */}
              <div className="flex items-center gap-1 bg-slate-100 dark:bg-slate-800 p-1 rounded-xl self-start sm:self-auto">
                <button
                  type="button"
                  onClick={() => setBarChartFlow('expense')}
                  className={cn(
                    'px-3 py-1 text-xs font-bold rounded-lg transition-all cursor-pointer',
                    barChartFlow === 'expense'
                      ? 'bg-rose-500 text-white shadow-2xs'
                      : 'text-slate-600 dark:text-slate-400 hover:text-rose-500'
                  )}
                >
                  {locale === 'vi-VN' ? 'Chi tiêu' : 'Expenses'}
                </button>
                <button
                  type="button"
                  onClick={() => setBarChartFlow('income')}
                  className={cn(
                    'px-3 py-1 text-xs font-bold rounded-lg transition-all cursor-pointer',
                    barChartFlow === 'income'
                      ? 'bg-emerald-600 text-white shadow-2xs'
                      : 'text-slate-600 dark:text-slate-400 hover:text-emerald-500'
                  )}
                >
                  {locale === 'vi-VN' ? 'Thu nhập' : 'Income'}
                </button>
              </div>
            </div>

            {/* Total & Average Summary Strips */}
            <div className="grid grid-cols-2 gap-3 py-4 text-xs font-mono">
              <div className="p-3 rounded-xl bg-slate-50 dark:bg-slate-800/40 border border-slate-100 dark:border-slate-800">
                <span className="text-[11px] text-slate-400 font-sans block mb-0.5">
                  {locale === 'vi-VN' ? 'Tổng số tiền (6 tháng)' : 'Total (6 months)'}
                </span>
                <span className="text-base font-bold text-slate-900 dark:text-white tabular-nums">
                  {formatMoney(totalSixMonths, locale, { isHidden })}
                </span>
              </div>
              <div className="p-3 rounded-xl bg-slate-50 dark:bg-slate-800/40 border border-slate-100 dark:border-slate-800">
                <span className="text-[11px] text-slate-400 font-sans block mb-0.5">
                  {locale === 'vi-VN' ? 'Trung bình mỗi tháng' : 'Monthly average'}
                </span>
                <span className="text-base font-bold text-slate-900 dark:text-white tabular-nums">
                  {formatMoney(averageMonthly, locale, { isHidden })}
                </span>
              </div>
            </div>

            {/* Bar Chart Visualization Canvas */}
            <div className="h-44 flex items-end justify-between gap-3 pt-4 px-2">
              {monthlyData.map((m, idx) => {
                const heightPct = maxMonthValue > 0 ? Math.max(8, Math.round((m.total / maxMonthValue) * 100)) : 8;
                return (
                  <div key={idx} className="flex-1 flex flex-col items-center gap-2 h-full justify-end group">
                    <span className="text-[10px] font-mono text-slate-400 opacity-0 group-hover:opacity-100 transition-opacity">
                      {formatMoney(m.total, locale, { isHidden })}
                    </span>
                    <div
                      style={{ height: `${heightPct}%` }}
                      className={cn(
                        'w-full max-w-[42px] rounded-t-xl transition-all',
                        barChartFlow === 'expense'
                          ? 'bg-rose-400 dark:bg-rose-500 hover:bg-rose-500'
                          : 'bg-emerald-500 hover:bg-emerald-600'
                      )}
                    />
                    <span className="text-[11px] font-semibold text-slate-500 dark:text-slate-400">
                      {locale === 'vi-VN' ? m.labelVi : m.labelEn}
                    </span>
                  </div>
                );
              })}
            </div>
          </div>

          <div className="pt-3 border-t border-slate-100 dark:border-slate-800 flex items-center justify-between text-[11px] text-slate-400">
            <span>Dữ liệu tính từ các giao dịch đã hoàn tất</span>
            <span className="font-mono">{monthlyData.reduce((acc, c) => acc + c.count, 0)} giao dịch</span>
          </div>
        </div>

        {/* Right (5 cols): Current Month Category Donut Chart with Selector */}
        <div className="lg:col-span-5 bg-white dark:bg-slate-900 rounded-2xl p-5 sm:p-6 border border-slate-200/80 dark:border-slate-800 shadow-xs flex flex-col justify-between">
          <div>
            <div className="flex items-center justify-between gap-3 pb-4 border-b border-slate-100 dark:border-slate-800">
              <div>
                <h3 className="text-sm font-bold text-slate-900 dark:text-white flex items-center gap-2">
                  <PieIcon className="h-4 w-4 text-slate-400" />
                  <span>{locale === 'vi-VN' ? `Phân bổ danh mục T${currentMonth + 1}/${currentYear}` : `Distribution M${currentMonth + 1}/${currentYear}`}</span>
                </h3>
                <p className="text-xs text-slate-400 font-medium mt-0.5">
                  {categorySlices.length} {locale === 'vi-VN' ? 'danh mục phát sinh' : 'categories'}
                </p>
              </div>

              {/* Selector Chi / Thu */}
              <div className="flex items-center gap-1 bg-slate-100 dark:bg-slate-800 p-1 rounded-xl">
                <button
                  type="button"
                  onClick={() => setDonutFlow('expense')}
                  className={cn(
                    'px-2.5 py-1 text-xs font-bold rounded-lg transition-all cursor-pointer',
                    donutFlow === 'expense'
                      ? 'bg-rose-500 text-white shadow-2xs'
                      : 'text-slate-600 dark:text-slate-400 hover:text-rose-500'
                  )}
                >
                  {locale === 'vi-VN' ? 'Chi' : 'Exp'}
                </button>
                <button
                  type="button"
                  onClick={() => setDonutFlow('income')}
                  className={cn(
                    'px-2.5 py-1 text-xs font-bold rounded-lg transition-all cursor-pointer',
                    donutFlow === 'income'
                      ? 'bg-emerald-600 text-white shadow-2xs'
                      : 'text-slate-600 dark:text-slate-400 hover:text-emerald-500'
                  )}
                >
                  {locale === 'vi-VN' ? 'Thu' : 'Inc'}
                </button>
              </div>
            </div>

            {/* SVG Donut / Empty state */}
            {categorySlices.length === 0 ? (
              <div className="py-12 text-center space-y-2">
                <PieIcon className="h-10 w-10 text-slate-300 dark:text-slate-600 mx-auto" />
                <p className="text-xs text-slate-400">
                  {locale === 'vi-VN' ? 'Chưa có dữ liệu danh mục trong tháng này.' : 'No category data recorded this month.'}
                </p>
              </div>
            ) : (
              <div className="pt-4 flex flex-col sm:flex-row items-center gap-6">
                <div className="relative w-36 h-36 shrink-0">
                  <svg className="w-full h-full -rotate-90" viewBox="0 0 160 160">
                    <circle cx="80" cy="80" r={radius} fill="none" stroke="currentColor" strokeWidth={strokeWidth} className="text-slate-100 dark:text-slate-800" />
                    {svgSegments.map((seg) => (
                      <circle
                        key={seg.key}
                        cx="80"
                        cy="80"
                        r={radius}
                        fill="none"
                        stroke={seg.color}
                        strokeWidth={strokeWidth}
                        strokeDasharray={seg.strokeDasharray}
                        strokeDashoffset={seg.strokeDashoffset}
                        className="transition-all hover:opacity-80 cursor-pointer"
                        onMouseEnter={() => setHoveredSlice(seg.key)}
                        onMouseLeave={() => setHoveredSlice(null)}
                      />
                    ))}
                  </svg>
                  <div className="absolute inset-0 flex flex-col items-center justify-center pointer-events-none">
                    <span className="text-[10px] uppercase font-bold text-slate-400">
                      {locale === 'vi-VN' ? 'Tổng' : 'Total'}
                    </span>
                    <span className="text-xs font-bold font-mono text-slate-900 dark:text-white truncate max-w-[90px]">
                      {formatMoney(totalAmount, locale, { isHidden })}
                    </span>
                  </div>
                </div>

                {/* Category Legend list */}
                <div className="flex-1 space-y-2 w-full text-xs">
                  {categorySlices.slice(0, 4).map((slice) => (
                    <div key={slice.key} className="flex items-center justify-between gap-2">
                      <div className="flex items-center gap-2 min-w-0">
                        <span className="w-2.5 h-2.5 rounded-full shrink-0" style={{ backgroundColor: slice.color }} />
                        <span className="font-medium text-slate-700 dark:text-slate-300 truncate">{slice.name}</span>
                      </div>
                      <div className="text-right font-mono shrink-0">
                        <span className="font-bold text-slate-900 dark:text-white mr-1.5">{slice.percentage}%</span>
                        <span className="text-[11px] text-slate-400">{formatMoney(slice.amount, locale, { isHidden })}</span>
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            )}
          </div>

          <div className="pt-3 mt-4 border-t border-slate-100 dark:border-slate-800 flex items-center justify-between text-xs">
            <NavLink to="/transactions" className="text-emerald-600 dark:text-emerald-400 font-semibold hover:underline">
              {locale === 'vi-VN' ? 'Xem chi tiết giao dịch →' : 'View all transactions →'}
            </NavLink>
          </div>
        </div>
      </div>

      {/* 5. Recent Transactions Table */}
      <div className="bg-white dark:bg-slate-900 rounded-2xl border border-slate-200/80 dark:border-slate-800 shadow-xs overflow-hidden">
        <div className="px-5 py-4 border-b border-slate-100 dark:border-slate-800 flex items-center justify-between">
          <h3 className="text-sm font-bold text-slate-900 dark:text-white">
            {locale === 'vi-VN' ? 'Giao dịch gần đây' : 'Recent Transactions'}
          </h3>
          <NavLink to="/transactions" className="text-xs font-semibold text-emerald-600 dark:text-emerald-400 hover:underline">
            {locale === 'vi-VN' ? 'Tất cả giao dịch →' : 'View all →'}
          </NavLink>
        </div>

        {recentTransactions.length === 0 ? (
          <div className="p-8 text-center space-y-2">
            <Clock className="h-8 w-8 text-slate-300 dark:text-slate-600 mx-auto" />
            <p className="text-xs font-semibold text-slate-600 dark:text-slate-300">
              {locale === 'vi-VN' ? 'Chưa phát sinh giao dịch nào' : 'No transactions recorded yet'}
            </p>
            <p className="text-[11px] text-slate-400">
              {locale === 'vi-VN' ? 'Dòng tiền chi tiêu và thu nhập sẽ hiển thị trực tiếp tại đây.' : 'Income and expenses will be recorded here.'}
            </p>
          </div>
        ) : (
          <div className="divide-y divide-slate-100 dark:divide-slate-800">
            {recentTransactions.map((tx) => (
              <div key={tx.id} className="p-4 sm:p-5 flex items-center justify-between gap-3 hover:bg-slate-50/50 dark:hover:bg-slate-800/30 transition-colors">
                <div className="flex items-center gap-3 min-w-0">
                  <div className={cn(
                    'h-10 w-10 rounded-xl flex items-center justify-center shrink-0 font-bold',
                    tx.type === 'income'
                      ? 'bg-emerald-100 text-emerald-600 dark:bg-emerald-950/60 dark:text-emerald-400'
                      : 'bg-slate-100 text-slate-600 dark:bg-slate-800 dark:text-slate-400'
                  )}>
                    {tx.type === 'income' ? <ArrowDownLeft className="h-4 w-4" /> : <ArrowUpRight className="h-4 w-4" />}
                  </div>
                  <div className="min-w-0">
                    <p className="text-xs sm:text-sm font-bold text-slate-900 dark:text-white truncate">
                      {tx.title}
                    </p>
                    <p className="text-[11px] text-slate-400 mt-0.5 font-mono">
                      {formatDate(tx.date, locale)} • {tx.category || 'Chung'}
                    </p>
                  </div>
                </div>

                <div className="text-right font-mono shrink-0">
                  <span className={cn(
                    'text-xs sm:text-sm font-bold tabular-nums block',
                    tx.type === 'income' ? 'text-emerald-600 dark:text-emerald-400' : 'text-slate-900 dark:text-white'
                  )}>
                    {tx.type === 'income' ? '+' : '−'}
                    {formatMoney(Math.abs(tx.amount), locale, { isHidden })}
                  </span>
                  <span className="text-[10px] text-slate-400 font-sans block">
                    {tx.walletId ? 'Ví chính' : ''}
                  </span>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
};
