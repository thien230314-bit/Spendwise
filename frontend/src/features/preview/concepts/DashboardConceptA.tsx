import React, { useMemo, useState } from 'react';
import {
  Calendar as CalendarIcon,
  TrendingUp,
  TrendingDown,
  Eye,
  EyeOff,
  Plus,
  Minus,
  Send,
  Users,
  AlertCircle,
  Clock,
  ChevronRight,
  Sparkles,
  ArrowUpRight,
  ShieldCheck,
  PieChart as PieIcon,
  BarChart3,
  ArrowDownLeft,
} from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { useI18n } from '../../../i18n/I18nContext';
import { useAuth } from '../../../providers/AuthProvider';
import { useFinancial } from '../../../providers/FinancialProvider';
import { useGroup } from '../../../providers/GroupProvider';
import { useBalanceVisibility } from '../../../providers/BalanceVisibilityProvider';
import { formatMoney } from '../../../utils/money';
import { formatDate, getTimeOfDay } from '../../../utils/date';
import { getLocalizedCategoryName } from '../../../utils/localizationHelpers';
import { Button } from '../../../components/ui/Button';
import { cn } from '../../../utils/cn';
import {
  getTransactionEffectiveDate,
  isUserCompletedTx,
  getAnalyticsFlow,
  getLast6CalendarMonths,
  getAnalyticsCategoryKey,
  EXPENSE_RED_PALETTE,
  INCOME_GREEN_PALETTE,
} from '../../../utils/analyticsHelpers';

/**
 * DASHBOARD CONCEPT A: Editorial Finance Workspace
 * Triết lý thiết kế:
 * - Bất đối xứng có chủ đích (Asymmetric Editorial Layout), phân cấp thông tin cao cấp.
 * - Typography ấn tượng: Header chào mừng phong cách tạp chí tài chính hiện đại.
 * - Brand SpendWise Emerald làm điểm nhấn sang trọng, tối giản card lồng nhau.
 * - Bảo toàn 100% 16 Feature IDs (DASH-001 -> DASH-013).
 */
export const DashboardConceptA: React.FC = () => {
  const { t, locale } = useI18n();
  const { currentUser } = useAuth();
  const {
    isLoading,
    dashboardSummary: baseSummary,
    attentionItems,
    transactions,
    categories,
  } = useFinancial();
  const { groups } = useGroup();
  const { isHidden, toggleVisibility } = useBalanceVisibility();
  const navigate = useNavigate();

  // Chart state toggles
  const [barChartFlow, setBarChartFlow] = useState<'expense' | 'income'>('expense');
  const [donutFlow, setDonutFlow] = useState<'expense' | 'income'>('expense');
  const [hoveredSlice, setHoveredSlice] = useState<string | null>(null);

  // Group obligations derivation
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

    return { groupFundBalance: fundBalance, userPendingFundObligations: pendingObligations };
  }, [groups, currentUser]);

  const dashboardSummary = useMemo(() => {
    return {
      ...baseSummary,
      groupFundBalance,
      userPendingFundObligations,
    };
  }, [baseSummary, groupFundBalance, userPendingFundObligations]);

  // Greeting copy
  const timeOfDay = getTimeOfDay();
  const userName = currentUser?.name || 'User';
  const greetingText =
    locale === 'vi-VN'
      ? `${timeOfDay === 'morning' ? 'Chào buổi sáng' : timeOfDay === 'afternoon' ? 'Chào buổi chiều' : 'Chào buổi tối'}, ${userName}`
      : `Good ${timeOfDay}, ${userName}`;

  // Net Cash Flow metrics
  const incomeThisMonth = dashboardSummary.incomeThisMonth || 0;
  const expenseThisMonth = dashboardSummary.expenseThisMonth || 0;
  const netFlow = incomeThisMonth - expenseThisMonth;
  const totalFlow = incomeThisMonth + expenseThisMonth;
  const incomeRatio = totalFlow > 0 ? Math.round((incomeThisMonth / totalFlow) * 100) : 50;

  // 6 Months bar chart derivation
  const last6Months = useMemo(() => getLast6CalendarMonths(new Date()), []);
  const monthlyData = useMemo(() => {
    return last6Months.map((slot) => {
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
  }, [transactions, currentUser?.id, barChartFlow, last6Months]);

  const totalSixMonths = useMemo(() => monthlyData.reduce((acc, curr) => acc + curr.total, 0), [monthlyData]);
  const maxMonthValue = useMemo(() => Math.max(1000000, ...monthlyData.map((m) => m.total)), [monthlyData]);

  // Current month category donut derivation
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

  const radius = 64;
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
    <div className="space-y-8 max-w-7xl mx-auto font-sans">
      {/* CONCEPT BADGE */}
      <div className="flex flex-wrap items-center justify-between gap-3 px-5 py-3.5 rounded-2xl bg-slate-900 text-white dark:bg-slate-950 border border-slate-800 shadow-md">
        <div className="flex items-center gap-2.5">
          <span className="flex h-2.5 w-2.5 rounded-full bg-emerald-400 animate-pulse" />
          <span className="text-xs font-bold uppercase tracking-widest text-emerald-400">Concept A</span>
          <span className="text-xs text-slate-400">|</span>
          <span className="text-xs font-semibold text-slate-200">Editorial Finance Workspace</span>
        </div>
        <div className="flex items-center gap-3 text-xs text-slate-400">
          <span>Desktop 1440px Focus</span>
          <span>•</span>
          <span className="text-emerald-300 font-medium">Bảo toàn 16/16 Feature IDs</span>
        </div>
      </div>

      {/* DASH-SEC-01: GREETING & EDITORIAL HEADER */}
      <header className="flex flex-col md:flex-row md:items-end justify-between gap-4 pb-2 border-b border-slate-200/80 dark:border-slate-800/80">
        <div className="space-y-1">
          <div className="flex items-center gap-2 text-xs font-semibold uppercase tracking-wider text-emerald-600 dark:text-emerald-400">
            <Sparkles className="h-3.5 w-3.5" />
            <span>SpendWise Intelligence Workspace</span>
          </div>
          <h1 className="text-2xl sm:text-3xl lg:text-4xl font-extrabold tracking-tight text-slate-900 dark:text-white">
            {greetingText}
          </h1>
          <p className="text-sm text-slate-500 dark:text-slate-400">
            {locale === 'vi-VN'
              ? 'Bức tranh tài chính tổng thể và dữ liệu dòng tiền theo thời gian thực.'
              : 'Holistic financial health and real-time cash flow overview.'}
          </p>
        </div>

        {/* Feature DASH-002: Balance Visibility Toggle */}
        <div className="flex items-center gap-3 shrink-0">
          <div className="flex items-center gap-2 px-3.5 py-1.5 rounded-full bg-slate-100 dark:bg-slate-800/80 text-xs font-medium text-slate-600 dark:text-slate-300 border border-slate-200/60 dark:border-slate-700/60">
            <CalendarIcon className="h-3.5 w-3.5 text-emerald-600 dark:text-emerald-400" />
            <span>{formatDate(new Date(), locale)}</span>
          </div>
          <button
            type="button"
            onClick={toggleVisibility}
            title={isHidden ? (locale === 'vi-VN' ? 'Hiện số dư' : 'Show balance') : (locale === 'vi-VN' ? 'Ẩn số dư' : 'Hide balance')}
            className="flex items-center gap-1.5 px-3 py-1.5 rounded-full border border-slate-300 dark:border-slate-700 hover:bg-slate-100 dark:hover:bg-slate-800 text-xs font-medium text-slate-700 dark:text-slate-200 transition-colors cursor-pointer"
          >
            {isHidden ? <Eye className="h-3.5 w-3.5 text-emerald-500" /> : <EyeOff className="h-3.5 w-3.5 text-slate-400" />}
            <span>{isHidden ? (locale === 'vi-VN' ? 'Hiện số dư' : 'Show') : (locale === 'vi-VN' ? 'Riêng tư' : 'Private')}</span>
          </button>
        </div>
      </header>

      {/* DASH-SEC-04: ATTENTION & ACTION CENTER (DASH-010) */}
      {(isLoading || attentionItems.length > 0) && (
        <section className="rounded-2xl p-4 bg-amber-500/10 border border-amber-500/30 dark:bg-amber-500/15 dark:border-amber-500/40 text-amber-900 dark:text-amber-200">
          <div className="flex items-start gap-3">
            <AlertCircle className="h-5 w-5 text-amber-600 dark:text-amber-400 mt-0.5 shrink-0" />
            <div className="flex-1 space-y-1">
              <h3 className="text-sm font-bold">
                {locale === 'vi-VN' ? 'Cảnh báo tài chính cần xử lý ngay' : 'Financial Items Requiring Attention'}
              </h3>
              <p className="text-xs text-amber-800/90 dark:text-amber-300/90 leading-relaxed">
                {attentionItems.length > 0
                  ? attentionItems[0].rawMessage
                  : locale === 'vi-VN'
                  ? 'Một số hạng mục ngân sách hoặc mục tiêu tiết kiệm đang chạm ngưỡng cảnh báo.'
                  : 'Several budget items or savings goals are nearing their limits.'}
              </p>
            </div>
            <Button
              size="sm"
              variant="outline"
              className="text-xs shrink-0 border-amber-300 dark:border-amber-700 text-amber-900 dark:text-amber-200 hover:bg-amber-100 dark:hover:bg-amber-900/40"
              onClick={() => navigate('/budget')}
            >
              {locale === 'vi-VN' ? 'Xem chi tiết' : 'Review'}
            </Button>
          </div>
        </section>
      )}

      {/* ASYMMETRIC TOP WORKSPACE: EDITORIAL BALANCE HERO + METRIC COLUMNS */}
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 items-stretch">
        {/* DASH-SEC-02: AVAILABLE BALANCE HERO (DASH-003 & DASH-004A-D) -> 5 Cols Editorial */}
        <div className="lg:col-span-5 flex flex-col justify-between p-6 sm:p-7 rounded-3xl bg-linear-to-br from-emerald-950 via-slate-900 to-slate-950 text-white border border-emerald-900/40 shadow-xl relative overflow-hidden group">
          <div className="absolute -top-24 -right-24 w-64 h-64 bg-emerald-500/10 rounded-full blur-3xl pointer-events-none group-hover:bg-emerald-500/20 transition-all duration-700" />
          
          <div className="space-y-4 relative z-10">
            <div className="flex items-center justify-between">
              <div className="flex items-center gap-2">
                <span className="px-2.5 py-0.5 rounded-full text-[10px] font-bold uppercase tracking-wider bg-emerald-500/20 text-emerald-300 border border-emerald-500/30">
                  {locale === 'vi-VN' ? 'Khả dụng tức thời' : 'Liquid Assets'}
                </span>
                <span className="text-xs text-slate-400">|</span>
                <span className="text-xs text-slate-400 font-medium">
                  {locale === 'vi-VN' ? 'Tất cả ví cá nhân' : 'All Wallets'}
                </span>
              </div>
              <ShieldCheck className="h-4 w-4 text-emerald-400/80" />
            </div>

            {/* Total Balance Amount (DASH-003) */}
            <div className="space-y-1">
              <p className="text-xs font-semibold tracking-wider text-slate-400 uppercase">
                {locale === 'vi-VN' ? 'Tổng số dư khả dụng' : 'Total Available Balance'}
              </p>
              <div className="text-3xl sm:text-4xl lg:text-4xl font-black font-mono tracking-tight text-white tabular-nums drop-shadow-xs">
                {isHidden ? '••••••••' : formatMoney(dashboardSummary.totalBalance || 0, locale)}
              </div>
            </div>

            <p className="text-xs text-slate-400/90 leading-relaxed pt-1">
              {locale === 'vi-VN'
                ? 'Được đồng bộ trực tiếp từ các tài khoản ngân hàng liên kết và ví tiền mặt cá nhân.'
                : 'Directly synchronized with linked bank accounts and personal cash wallets.'}
            </p>
          </div>

          {/* DASH-004A-D: QUICK ACTION WORKBENCH */}
          <div className="pt-6 mt-6 border-t border-slate-800/80 relative z-10">
            <p className="text-[11px] font-bold uppercase tracking-wider text-slate-400 mb-3">
              {locale === 'vi-VN' ? 'Thao tác nhanh tức thời' : 'Quick Actions'}
            </p>
            <div className="grid grid-cols-4 gap-2">
              <button
                type="button"
                onClick={() => navigate('/transactions?action=income')}
                className="flex flex-col items-center justify-center p-2.5 rounded-2xl bg-emerald-500/15 hover:bg-emerald-500/25 border border-emerald-500/30 transition-all text-emerald-300 hover:text-white group/btn cursor-pointer"
              >
                <div className="p-1.5 rounded-xl bg-emerald-500/20 text-emerald-300 group-hover/btn:scale-110 transition-transform">
                  <Plus className="h-4 w-4" />
                </div>
                <span className="text-[11px] font-bold mt-1.5">{locale === 'vi-VN' ? 'Thu' : 'Income'}</span>
              </button>

              <button
                type="button"
                onClick={() => navigate('/transactions?action=expense')}
                className="flex flex-col items-center justify-center p-2.5 rounded-2xl bg-rose-500/15 hover:bg-rose-500/25 border border-rose-500/30 transition-all text-rose-300 hover:text-white group/btn cursor-pointer"
              >
                <div className="p-1.5 rounded-xl bg-rose-500/20 text-rose-300 group-hover/btn:scale-110 transition-transform">
                  <Minus className="h-4 w-4" />
                </div>
                <span className="text-[11px] font-bold mt-1.5">{locale === 'vi-VN' ? 'Chi' : 'Expense'}</span>
              </button>

              <button
                type="button"
                onClick={() => navigate('/transfer')}
                className="flex flex-col items-center justify-center p-2.5 rounded-2xl bg-sky-500/15 hover:bg-sky-500/25 border border-sky-500/30 transition-all text-sky-300 hover:text-white group/btn cursor-pointer"
              >
                <div className="p-1.5 rounded-xl bg-sky-500/20 text-sky-300 group-hover/btn:scale-110 transition-transform">
                  <Send className="h-4 w-4" />
                </div>
                <span className="text-[11px] font-bold mt-1.5">{locale === 'vi-VN' ? 'Chuyển' : 'Transfer'}</span>
              </button>

              <button
                type="button"
                onClick={() => navigate('/groups')}
                className="flex flex-col items-center justify-center p-2.5 rounded-2xl bg-purple-500/15 hover:bg-purple-500/25 border border-purple-500/30 transition-all text-purple-300 hover:text-white group/btn cursor-pointer"
              >
                <div className="p-1.5 rounded-xl bg-purple-500/20 text-purple-300 group-hover/btn:scale-110 transition-transform">
                  <Users className="h-4 w-4" />
                </div>
                <span className="text-[11px] font-bold mt-1.5">{locale === 'vi-VN' ? 'Nhóm' : 'Groups'}</span>
              </button>
            </div>
          </div>
        </div>

        {/* DASH-SEC-03: 4 FINANCIAL KPIS + NET RATIO BAR -> 7 Cols Editorial Grid */}
        <div className="lg:col-span-7 flex flex-col justify-between space-y-4">
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            {/* DASH-005: Income KPI */}
            <div className="p-5 rounded-3xl bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 shadow-xs hover:border-emerald-500/40 transition-colors">
              <div className="flex items-center justify-between mb-3">
                <span className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400">
                  {locale === 'vi-VN' ? 'Thu nhập tháng này' : 'Monthly Income'}
                </span>
                <div className="p-2 rounded-xl bg-emerald-50 dark:bg-emerald-950/40 text-emerald-600 dark:text-emerald-400">
                  <TrendingUp className="h-4 w-4" />
                </div>
              </div>
              <div className="text-xl sm:text-2xl font-extrabold font-mono text-emerald-600 dark:text-emerald-400 tabular-nums">
                {isHidden ? '••••••••' : formatMoney(incomeThisMonth, locale)}
              </div>
              <p className="text-[11px] text-slate-400 dark:text-slate-500 mt-2">
                {locale === 'vi-VN' ? 'Dòng tiền vào đã quyết toán' : 'Settled inflows this month'}
              </p>
            </div>

            {/* DASH-006: Expense KPI */}
            <div className="p-5 rounded-3xl bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 shadow-xs hover:border-rose-500/40 transition-colors">
              <div className="flex items-center justify-between mb-3">
                <span className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400">
                  {locale === 'vi-VN' ? 'Chi tiêu tháng này' : 'Monthly Expenses'}
                </span>
                <div className="p-2 rounded-xl bg-rose-50 dark:bg-rose-950/40 text-rose-600 dark:text-rose-400">
                  <TrendingDown className="h-4 w-4" />
                </div>
              </div>
              <div className="text-xl sm:text-2xl font-extrabold font-mono text-rose-600 dark:text-rose-400 tabular-nums">
                {isHidden ? '••••••••' : `-${formatMoney(expenseThisMonth, locale)}`}
              </div>
              <p className="text-[11px] text-slate-400 dark:text-slate-500 mt-2">
                {locale === 'vi-VN' ? 'Đã khấu trừ từ các ví' : 'Outflows recorded'}
              </p>
            </div>

            {/* DASH-007: Budget Remaining KPI */}
            <div className="p-5 rounded-3xl bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 shadow-xs hover:border-sky-500/40 transition-colors">
              <div className="flex items-center justify-between mb-3">
                <span className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400">
                  {locale === 'vi-VN' ? 'Ngân sách còn lại' : 'Remaining Budget'}
                </span>
                <div className="p-2 rounded-xl bg-sky-50 dark:bg-sky-950/40 text-sky-600 dark:text-sky-400">
                  <PieIcon className="h-4 w-4" />
                </div>
              </div>
              <div className="text-xl sm:text-2xl font-extrabold font-mono text-slate-900 dark:text-white tabular-nums">
                {isHidden ? '••••••••' : formatMoney(dashboardSummary.budgetRemaining || 0, locale)}
              </div>
              <p className="text-[11px] text-slate-400 dark:text-slate-500 mt-2">
                {locale === 'vi-VN' ? 'Hạn mức chi tiêu chưa dùng' : 'Unspent monthly allocation'}
              </p>
            </div>

            {/* DASH-008: Group Funds KPI */}
            <div className="p-5 rounded-3xl bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 shadow-xs hover:border-purple-500/40 transition-colors">
              <div className="flex items-center justify-between mb-3">
                <span className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400">
                  {locale === 'vi-VN' ? 'Quỹ nhóm & Nghĩa vụ' : 'Group Funds'}
                </span>
                <div className="p-2 rounded-xl bg-purple-50 dark:bg-purple-950/40 text-purple-600 dark:text-purple-400">
                  <Users className="h-4 w-4" />
                </div>
              </div>
              <div className="text-xl sm:text-2xl font-extrabold font-mono text-slate-900 dark:text-white tabular-nums">
                {isHidden ? '••••••••' : formatMoney(dashboardSummary.groupFundBalance || 0, locale)}
              </div>
              <p className="text-[11px] text-slate-400 dark:text-slate-500 mt-2">
                {(dashboardSummary.userPendingFundObligations || 0) > 0
                  ? `${locale === 'vi-VN' ? 'Cần đóng:' : 'Due:'} ${formatMoney(dashboardSummary.userPendingFundObligations || 0, locale)}`
                  : locale === 'vi-VN' ? 'Không có nghĩa vụ nợ quỹ' : 'All obligations settled'}
              </p>
            </div>
          </div>

          {/* DASH-009: Net Cash Flow Ratio Bar */}
          <div className="p-5 rounded-3xl bg-slate-50 dark:bg-slate-900/60 border border-slate-200/80 dark:border-slate-800 flex flex-col justify-between">
            <div className="flex items-center justify-between text-xs font-semibold mb-2">
              <span className="text-slate-700 dark:text-slate-300">
                {locale === 'vi-VN' ? 'Dòng tiền ròng tháng này' : 'Net Cash Flow Ratio'}
              </span>
              <span className={cn('font-mono font-bold', netFlow >= 0 ? 'text-emerald-600 dark:text-emerald-400' : 'text-rose-600 dark:text-rose-400')}>
                {isHidden ? '••••••••' : `${netFlow >= 0 ? '+' : ''}${formatMoney(netFlow, locale)}`}
              </span>
            </div>
            <div className="w-full h-2.5 rounded-full bg-slate-200 dark:bg-slate-800 overflow-hidden flex">
              <div
                style={{ width: `${incomeRatio}%` }}
                className="h-full bg-emerald-500 transition-all duration-500"
                title={`${locale === 'vi-VN' ? 'Thu nhập' : 'Income'}: ${incomeRatio}%`}
              />
              <div
                style={{ width: `${100 - incomeRatio}%` }}
                className="h-full bg-rose-500 transition-all duration-500"
                title={`${locale === 'vi-VN' ? 'Chi tiêu' : 'Expense'}: ${100 - incomeRatio}%`}
              />
            </div>
            <div className="flex items-center justify-between text-[10px] text-slate-400 mt-2">
              <span>{locale === 'vi-VN' ? `Thu: ${incomeRatio}%` : `Income: ${incomeRatio}%`}</span>
              <span>{locale === 'vi-VN' ? `Chi: ${100 - incomeRatio}%` : `Expense: ${100 - incomeRatio}%`}</span>
            </div>
          </div>
        </div>
      </div>

      {/* ANALYTICS SECTION: TWO EDITORIAL COMPARISON TILES (DASH-011 & DASH-012) */}
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 items-stretch">
        {/* DASH-SEC-05: 6 MONTHS CASH FLOW BAR CHART (DASH-011) -> 7 Cols */}
        <div className="lg:col-span-7 p-6 rounded-3xl bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 shadow-xs flex flex-col justify-between">
          <div>
            <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 pb-4 border-b border-slate-100 dark:border-slate-800/80">
              <div className="space-y-0.5">
                <div className="flex items-center gap-2">
                  <BarChart3 className="h-4 w-4 text-emerald-600 dark:text-emerald-400" />
                  <h3 className="text-base font-bold text-slate-900 dark:text-white">
                    {locale === 'vi-VN' ? 'Biến động dòng tiền 6 tháng' : '6 Months Cash Flow Dynamics'}
                  </h3>
                </div>
                <p className="text-xs text-slate-500 dark:text-slate-400">
                  {locale === 'vi-VN' ? `Tổng 6 tháng: ${formatMoney(totalSixMonths, locale)}` : `6-mo total: ${formatMoney(totalSixMonths, locale)}`}
                </p>
              </div>

              <div className="flex items-center p-1 rounded-xl bg-slate-100 dark:bg-slate-800 text-xs font-semibold">
                <button
                  type="button"
                  onClick={() => setBarChartFlow('expense')}
                  className={cn(
                    'px-3 py-1 rounded-lg transition-all cursor-pointer',
                    barChartFlow === 'expense'
                      ? 'bg-rose-500 text-white shadow-xs'
                      : 'text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white'
                  )}
                >
                  {locale === 'vi-VN' ? 'Chi tiêu' : 'Expense'}
                </button>
                <button
                  type="button"
                  onClick={() => setBarChartFlow('income')}
                  className={cn(
                    'px-3 py-1 rounded-lg transition-all cursor-pointer',
                    barChartFlow === 'income'
                      ? 'bg-emerald-500 text-white shadow-xs'
                      : 'text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white'
                  )}
                >
                  {locale === 'vi-VN' ? 'Thu nhập' : 'Income'}
                </button>
              </div>
            </div>

            <div className="pt-6 pb-2">
              <div className="grid grid-cols-6 gap-2 sm:gap-4 h-48 items-end">
                {monthlyData.map((slot) => {
                  const barHeight = Math.max(12, Math.round((slot.total / maxMonthValue) * 100));
                  return (
                    <div key={`${slot.year}-${slot.month}`} className="flex flex-col items-center gap-2 h-full justify-end group/bar">
                      <span className="text-[10px] font-mono font-medium text-slate-400 opacity-0 group-hover/bar:opacity-100 transition-opacity">
                        {formatMoney(slot.total, locale).replace('₫', '').trim()}
                      </span>
                      <div className="w-full max-w-[38px] h-full flex items-end">
                        <div
                          style={{ height: `${barHeight}%` }}
                          className={cn(
                            'w-full rounded-xl transition-all duration-500',
                            barChartFlow === 'expense'
                              ? 'bg-rose-500/80 group-hover/bar:bg-rose-600'
                              : 'bg-emerald-500/80 group-hover/bar:bg-emerald-600'
                          )}
                        />
                      </div>
                      <span className="text-xs font-bold text-slate-600 dark:text-slate-400 truncate">
                        {locale === 'vi-VN' ? slot.labelVi : slot.labelEn}
                      </span>
                    </div>
                  );
                })}
              </div>
            </div>
          </div>

          <div className="pt-4 border-t border-slate-100 dark:border-slate-800 text-[11px] text-slate-400 flex items-center justify-between">
            <span>{locale === 'vi-VN' ? 'Dữ liệu được làm mới theo từng giao dịch' : 'Live data synchronized'}</span>
            <span className="font-mono">{locale === 'vi-VN' ? `TB: ${formatMoney(Math.round(totalSixMonths / 6), locale)}/tháng` : `Avg: ${formatMoney(Math.round(totalSixMonths / 6), locale)}/mo`}</span>
          </div>
        </div>

        {/* DASH-SEC-06: CATEGORY DONUT ALLOCATION (DASH-012) -> 5 Cols */}
        <div className="lg:col-span-5 p-6 rounded-3xl bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 shadow-xs flex flex-col justify-between">
          <div>
            <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 pb-4 border-b border-slate-100 dark:border-slate-800/80">
              <div className="space-y-0.5">
                <div className="flex items-center gap-2">
                  <PieIcon className="h-4 w-4 text-emerald-600 dark:text-emerald-400" />
                  <h3 className="text-base font-bold text-slate-900 dark:text-white">
                    {locale === 'vi-VN' ? 'Cơ cấu phân bổ tháng' : 'Category Allocation'}
                  </h3>
                </div>
                <p className="text-xs text-slate-500 dark:text-slate-400">
                  {categorySlices.length > 0
                    ? `${categorySlices.length} ${locale === 'vi-VN' ? 'danh mục phát sinh' : 'active categories'}`
                    : locale === 'vi-VN' ? 'Chưa có dữ liệu tháng' : 'No data yet'}
                </p>
              </div>

              <div className="flex items-center p-1 rounded-xl bg-slate-100 dark:bg-slate-800 text-xs font-semibold">
                <button
                  type="button"
                  onClick={() => setDonutFlow('expense')}
                  className={cn(
                    'px-3 py-1 rounded-lg transition-all cursor-pointer',
                    donutFlow === 'expense'
                      ? 'bg-rose-500 text-white shadow-xs'
                      : 'text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white'
                  )}
                >
                  {locale === 'vi-VN' ? 'Chi' : 'Exp'}
                </button>
                <button
                  type="button"
                  onClick={() => setDonutFlow('income')}
                  className={cn(
                    'px-3 py-1 rounded-lg transition-all cursor-pointer',
                    donutFlow === 'income'
                      ? 'bg-emerald-500 text-white shadow-xs'
                      : 'text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white'
                  )}
                >
                  {locale === 'vi-VN' ? 'Thu' : 'Inc'}
                </button>
              </div>
            </div>

            <div className="py-6 flex flex-col sm:flex-row items-center justify-center gap-6">
              <div className="relative w-36 h-36 shrink-0 flex items-center justify-center">
                <svg className="w-full h-full -rotate-90" viewBox="0 0 160 160">
                  <circle
                    cx="80"
                    cy="80"
                    r={radius}
                    fill="transparent"
                    stroke="currentColor"
                    strokeWidth={20}
                    className="text-slate-100 dark:text-slate-800"
                  />
                  {svgSegments.map((segment) => (
                    <circle
                      key={segment.key}
                      cx="80"
                      cy="80"
                      r={radius}
                      fill="transparent"
                      stroke={segment.color}
                      strokeWidth={hoveredSlice === segment.key ? 24 : 20}
                      strokeDasharray={segment.strokeDasharray}
                      strokeDashoffset={segment.strokeDashoffset}
                      className="transition-all duration-300 cursor-pointer"
                      onMouseEnter={() => setHoveredSlice(segment.key)}
                      onMouseLeave={() => setHoveredSlice(null)}
                    />
                  ))}
                </svg>
                <div className="absolute inset-0 flex flex-col items-center justify-center pointer-events-none text-center">
                  <span className="text-[10px] font-bold uppercase tracking-wider text-slate-400">
                    {locale === 'vi-VN' ? 'Tổng' : 'Total'}
                  </span>
                  <span className="text-xs font-black font-mono text-slate-900 dark:text-white">
                    {formatMoney(totalAmount, locale).replace('₫', '').trim()}
                  </span>
                </div>
              </div>

              <div className="space-y-1.5 flex-1 w-full max-h-44 overflow-y-auto pr-1">
                {categorySlices.length === 0 ? (
                  <p className="text-xs text-slate-400 italic text-center py-4">
                    {locale === 'vi-VN' ? 'Chưa có giao dịch trong tháng' : 'No transactions recorded'}
                  </p>
                ) : (
                  categorySlices.map((slice) => (
                    <div
                      key={slice.key}
                      onMouseEnter={() => setHoveredSlice(slice.key)}
                      onMouseLeave={() => setHoveredSlice(null)}
                      className={cn(
                        'flex items-center justify-between p-1.5 rounded-xl text-xs transition-colors',
                        hoveredSlice === slice.key ? 'bg-slate-100 dark:bg-slate-800' : ''
                      )}
                    >
                      <div className="flex items-center gap-2 truncate">
                        <span className="w-2.5 h-2.5 rounded-full shrink-0" style={{ backgroundColor: slice.color }} />
                        <span className="font-medium text-slate-700 dark:text-slate-300 truncate">{slice.name}</span>
                      </div>
                      <span className="font-mono font-bold text-slate-900 dark:text-white shrink-0">
                        {slice.percentage}%
                      </span>
                    </div>
                  ))
                )}
              </div>
            </div>
          </div>

          <div className="pt-4 border-t border-slate-100 dark:border-slate-800 text-[11px] text-slate-400 flex items-center justify-between">
            <span>{locale === 'vi-VN' ? 'Tháng hiện tại' : 'Current calendar month'}</span>
            <span className="font-mono text-emerald-600 dark:text-emerald-400 font-semibold">{locale === 'vi-VN' ? 'Tự động phân nhóm' : 'Auto categorized'}</span>
          </div>
        </div>
      </div>

      {/* DASH-SEC-07: RECENT TRANSACTIONS FEED (DASH-013) */}
      <section className="p-6 rounded-3xl bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 shadow-xs">
        <div className="flex items-center justify-between pb-4 border-b border-slate-100 dark:border-slate-800">
          <div className="flex items-center gap-2">
            <Clock className="h-4 w-4 text-emerald-600 dark:text-emerald-400" />
            <h3 className="text-base font-bold text-slate-900 dark:text-white">
              {locale === 'vi-VN' ? 'Giao dịch gần đây' : 'Recent Transactions Feed'}
            </h3>
          </div>
          <button
            type="button"
            onClick={() => navigate('/transactions')}
            className="flex items-center gap-1 text-xs font-bold text-emerald-600 dark:text-emerald-400 hover:underline cursor-pointer"
          >
            <span>{locale === 'vi-VN' ? 'Xem tất cả giao dịch' : 'View All'}</span>
            <ChevronRight className="h-4 w-4" />
          </button>
        </div>

        <div className="divide-y divide-slate-100 dark:divide-slate-800/80">
          {recentTransactions.length === 0 ? (
            <p className="text-xs text-slate-400 italic py-6 text-center">
              {locale === 'vi-VN' ? 'Chưa có giao dịch nào gần đây' : 'No recent transactions'}
            </p>
          ) : (
            recentTransactions.map((tx) => {
              const isIncome = tx.type === 'income' || (tx.amount && tx.amount > 0);
              return (
                <div key={tx.id} className="py-3.5 flex items-center justify-between gap-4 group">
                  <div className="flex items-center gap-3 min-w-0">
                    <div
                      className={cn(
                        'p-2 rounded-2xl shrink-0 transition-transform group-hover:scale-105',
                        isIncome
                          ? 'bg-emerald-50 dark:bg-emerald-950/40 text-emerald-600 dark:text-emerald-400'
                          : 'bg-rose-50 dark:bg-rose-950/40 text-rose-600 dark:text-rose-400'
                      )}
                    >
                      {isIncome ? <ArrowDownLeft className="h-4 w-4" /> : <ArrowUpRight className="h-4 w-4" />}
                    </div>
                    <div className="min-w-0">
                      <p className="text-xs font-bold text-slate-900 dark:text-white truncate">
                        {tx.description || tx.category || (locale === 'vi-VN' ? 'Giao dịch' : 'Transaction')}
                      </p>
                      <p className="text-[11px] text-slate-400 truncate">
                        {formatDate(tx.date, locale)} {tx.walletName ? `• ${tx.walletName}` : ''}
                      </p>
                    </div>
                  </div>

                  <div className="text-right shrink-0">
                    <div
                      className={cn(
                        'text-xs font-extrabold font-mono tabular-nums',
                        isIncome ? 'text-emerald-600 dark:text-emerald-400' : 'text-slate-900 dark:text-white'
                      )}
                    >
                      {isHidden
                        ? '••••••••'
                        : `${isIncome ? '+' : '-'}${formatMoney(Math.abs(tx.amount), locale)}`}
                    </div>
                    <span className="text-[10px] text-slate-400 uppercase font-medium">
                      {tx.type}
                    </span>
                  </div>
                </div>
              );
            })
          )}
        </div>
      </section>
    </div>
  );
};
