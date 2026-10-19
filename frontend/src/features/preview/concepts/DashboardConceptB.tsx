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
  ShieldCheck,
  PieChart as PieIcon,
  BarChart3,
  Activity,
  Layers,
  ArrowUpRight,
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
 * DASHBOARD CONCEPT B: Financial Command Center
 * Triết lý thiết kế:
 * - Trung tâm chỉ huy vận hành tài chính tốc độ cao, phân chia khu vực dứt khoát.
 * - Cockpit analytical panels: Các module đo lường hiệu suất tài chính chuyên nghiệp.
 * - Bảng điều khiển tối ưu hóa mật độ thông tin với độ phân giải cao và visual weight cân bằng.
 * - Bảo toàn 100% 16 Feature IDs (DASH-001 -> DASH-013).
 */
export const DashboardConceptB: React.FC = () => {
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
    <div className="space-y-6 max-w-7xl mx-auto font-sans">
      {/* CONCEPT BADGE */}
      <div className="flex flex-wrap items-center justify-between gap-3 px-5 py-3 rounded-xl bg-slate-900 text-white dark:bg-slate-950 border border-slate-800 shadow-xs">
        <div className="flex items-center gap-2.5">
          <span className="flex h-2.5 w-2.5 rounded-full bg-cyan-400 animate-pulse" />
          <span className="text-xs font-bold uppercase tracking-widest text-cyan-400">Concept B</span>
          <span className="text-xs text-slate-400">|</span>
          <span className="text-xs font-semibold text-slate-200">Financial Command Center</span>
        </div>
        <div className="flex items-center gap-3 text-xs text-slate-400">
          <span>High-Density Cockpit</span>
          <span>•</span>
          <span className="text-cyan-300 font-medium">Bảo toàn 16/16 Feature IDs</span>
        </div>
      </div>

      {/* COMMAND CONTROL TOP STRIP: GREETING & STATUS TELEMETRY (DASH-001 & DASH-002) */}
      <header className="p-5 rounded-2xl bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 shadow-xs flex flex-col md:flex-row md:items-center justify-between gap-4">
        <div className="flex items-center gap-3">
          <div className="p-2.5 rounded-xl bg-cyan-500/10 text-cyan-600 dark:text-cyan-400 border border-cyan-500/20 shrink-0">
            <Activity className="h-5 w-5" />
          </div>
          <div>
            <div className="flex items-center gap-2">
              <h1 className="text-xl sm:text-2xl font-black text-slate-900 dark:text-white tracking-tight">
                {greetingText}
              </h1>
              <span className="px-2 py-0.5 rounded-md text-[10px] font-bold uppercase tracking-wider bg-emerald-100 text-emerald-800 dark:bg-emerald-950/60 dark:text-emerald-300 border border-emerald-300 dark:border-emerald-800">
                LIVE
              </span>
            </div>
            <p className="text-xs text-slate-500 dark:text-slate-400 mt-0.5">
              {locale === 'vi-VN'
                ? 'Trung tâm giám sát dòng tiền, hạn mức ngân sách và danh mục chi tiêu.'
                : 'Central operations console for cash flow, budgets, and allocations.'}
            </p>
          </div>
        </div>

        {/* Visibility Toggle & Timestamp (DASH-002) */}
        <div className="flex items-center gap-3">
          <div className="flex items-center gap-2 px-3 py-1.5 rounded-lg bg-slate-100 dark:bg-slate-800 text-xs font-mono text-slate-600 dark:text-slate-300 border border-slate-200/80 dark:border-slate-700">
            <Clock className="h-3.5 w-3.5 text-slate-400" />
            <span>{formatDate(new Date(), locale)}</span>
          </div>

          <button
            type="button"
            onClick={toggleVisibility}
            className="flex items-center gap-2 px-3.5 py-1.5 rounded-lg bg-slate-900 hover:bg-slate-800 text-white dark:bg-slate-800 dark:hover:bg-slate-700 text-xs font-semibold transition-colors cursor-pointer"
          >
            {isHidden ? <Eye className="h-3.5 w-3.5 text-emerald-400" /> : <EyeOff className="h-3.5 w-3.5 text-slate-400" />}
            <span>{isHidden ? (locale === 'vi-VN' ? 'Hiện số liệu' : 'Show') : (locale === 'vi-VN' ? 'Bảo mật' : 'Mask')}</span>
          </button>
        </div>
      </header>

      {/* DASH-SEC-04: ATTENTION BANNER (DASH-010) */}
      {(isLoading || attentionItems.length > 0) && (
        <section className="rounded-xl p-3.5 bg-amber-500/10 border-l-4 border-amber-500 border border-amber-500/20 text-amber-900 dark:text-amber-200 flex items-center justify-between gap-4">
          <div className="flex items-center gap-2.5 min-w-0">
            <AlertCircle className="h-4 w-4 text-amber-600 dark:text-amber-400 shrink-0" />
            <p className="text-xs font-medium truncate">
              {attentionItems.length > 0
                ? attentionItems[0].rawMessage
                : locale === 'vi-VN'
                ? 'Cảnh báo vận hành: Ngân sách hoặc mục tiêu tài chính cần được rà soát.'
                : 'Operational warning: Budget threshold or goal needs review.'}
            </p>
          </div>
          <button
            type="button"
            onClick={() => navigate('/budget')}
            className="px-3 py-1 rounded-md text-xs font-bold bg-amber-500/20 hover:bg-amber-500/30 text-amber-900 dark:text-amber-200 transition-colors shrink-0 cursor-pointer"
          >
            {locale === 'vi-VN' ? 'Xử lý ngay' : 'Resolve'}
          </button>
        </section>
      )}

      {/* COMMAND CENTER GRID: BALANCED 4-COLUMN TOP ROW */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
        {/* HERO TILE 1: TOTAL AVAILABLE BALANCE (DASH-003) */}
        <div className="p-5 rounded-2xl bg-slate-900 text-white dark:bg-slate-950 border border-slate-800 flex flex-col justify-between shadow-xs">
          <div className="space-y-1.5">
            <div className="flex items-center justify-between text-slate-400">
              <span className="text-[11px] font-bold uppercase tracking-wider">
                {locale === 'vi-VN' ? 'Số dư khả dụng' : 'Available Balance'}
              </span>
              <ShieldCheck className="h-4 w-4 text-emerald-400" />
            </div>
            <div className="text-2xl sm:text-3xl font-black font-mono tracking-tight text-white tabular-nums">
              {isHidden ? '••••••••' : formatMoney(dashboardSummary.totalBalance || 0, locale)}
            </div>
          </div>
          <div className="pt-3 mt-3 border-t border-slate-800 text-[11px] text-slate-400 flex items-center justify-between">
            <span>{locale === 'vi-VN' ? 'Trạng thái ví:' : 'Status:'}</span>
            <span className="text-emerald-400 font-semibold">{locale === 'vi-VN' ? 'Sẵn sàng' : 'Active'}</span>
          </div>
        </div>

        {/* HERO TILE 2: MONTHLY INFLOW & OUTFLOW (DASH-005 & DASH-006) */}
        <div className="p-5 rounded-2xl bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 flex flex-col justify-between shadow-xs">
          <div className="space-y-2">
            <div className="flex items-center justify-between text-slate-500 dark:text-slate-400">
              <span className="text-[11px] font-bold uppercase tracking-wider">
                {locale === 'vi-VN' ? 'Dòng thu / chi tháng' : 'Monthly Flow'}
              </span>
              <Layers className="h-4 w-4 text-slate-400" />
            </div>
            <div className="space-y-1">
              <div className="flex items-center justify-between">
                <span className="text-xs text-slate-500">{locale === 'vi-VN' ? 'Thu:' : 'In:'}</span>
                <span className="text-sm font-bold font-mono text-emerald-600 dark:text-emerald-400 tabular-nums">
                  {isHidden ? '••••••••' : formatMoney(incomeThisMonth, locale)}
                </span>
              </div>
              <div className="flex items-center justify-between">
                <span className="text-xs text-slate-500">{locale === 'vi-VN' ? 'Chi:' : 'Out:'}</span>
                <span className="text-sm font-bold font-mono text-rose-600 dark:text-rose-400 tabular-nums">
                  {isHidden ? '••••••••' : `-${formatMoney(expenseThisMonth, locale)}`}
                </span>
              </div>
            </div>
          </div>
          <div className="pt-2 mt-2 border-t border-slate-100 dark:border-slate-800 text-[11px] text-slate-400 flex items-center justify-between">
            <span>{locale === 'vi-VN' ? 'Chênh lệch ròng:' : 'Net:'}</span>
            <span className={cn('font-mono font-bold', netFlow >= 0 ? 'text-emerald-600' : 'text-rose-600')}>
              {isHidden ? '••••••••' : `${netFlow >= 0 ? '+' : ''}${formatMoney(netFlow, locale)}`}
            </span>
          </div>
        </div>

        {/* HERO TILE 3: BUDGET RUNWAY (DASH-007) */}
        <div className="p-5 rounded-2xl bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 flex flex-col justify-between shadow-xs">
          <div className="space-y-1.5">
            <div className="flex items-center justify-between text-slate-500 dark:text-slate-400">
              <span className="text-[11px] font-bold uppercase tracking-wider">
                {locale === 'vi-VN' ? 'Hạn mức ngân sách' : 'Budget Runway'}
              </span>
              <PieIcon className="h-4 w-4 text-sky-500" />
            </div>
            <div className="text-2xl font-black font-mono text-slate-900 dark:text-white tabular-nums">
              {isHidden ? '••••••••' : formatMoney(dashboardSummary.budgetRemaining || 0, locale)}
            </div>
          </div>
          <div className="pt-3 mt-3 border-t border-slate-100 dark:border-slate-800 text-[11px] text-slate-400 flex items-center justify-between">
            <span>{locale === 'vi-VN' ? 'Khả dụng còn lại' : 'Remaining'}</span>
            <span className="text-sky-600 font-semibold">{locale === 'vi-VN' ? 'Kiểm soát tốt' : 'Controlled'}</span>
          </div>
        </div>

        {/* HERO TILE 4: GROUP CAPITAL & OBLIGATIONS (DASH-008) */}
        <div className="p-5 rounded-2xl bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 flex flex-col justify-between shadow-xs">
          <div className="space-y-1.5">
            <div className="flex items-center justify-between text-slate-500 dark:text-slate-400">
              <span className="text-[11px] font-bold uppercase tracking-wider">
                {locale === 'vi-VN' ? 'Vốn quỹ nhóm' : 'Group Capital'}
              </span>
              <Users className="h-4 w-4 text-purple-500" />
            </div>
            <div className="text-2xl font-black font-mono text-slate-900 dark:text-white tabular-nums">
              {isHidden ? '••••••••' : formatMoney(dashboardSummary.groupFundBalance || 0, locale)}
            </div>
          </div>
          <div className="pt-3 mt-3 border-t border-slate-100 dark:border-slate-800 text-[11px] text-slate-400 flex items-center justify-between">
            <span>{locale === 'vi-VN' ? 'Nghĩa vụ nợ:' : 'Due:'}</span>
            <span className="text-purple-600 font-semibold">
              {(dashboardSummary.userPendingFundObligations || 0) > 0
                ? formatMoney(dashboardSummary.userPendingFundObligations || 0, locale)
                : locale === 'vi-VN' ? 'Không có' : 'None'}
            </span>
          </div>
        </div>
      </div>

      {/* COMMAND CONTROL SUB-STRIP: RAPID ACTION COCKPIT (DASH-004A-D) + NET BAR (DASH-009) */}
      <div className="p-4 rounded-2xl bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 shadow-xs flex flex-col lg:flex-row items-center justify-between gap-4">
        {/* DASH-004A-D: BUTTONS */}
        <div className="flex flex-wrap items-center gap-2 w-full lg:w-auto">
          <button
            type="button"
            onClick={() => navigate('/transactions?action=income')}
            className="flex-1 sm:flex-none flex items-center justify-center gap-2 px-4 py-2.5 rounded-xl bg-emerald-600 hover:bg-emerald-700 text-white text-xs font-bold shadow-xs transition-colors cursor-pointer"
          >
            <Plus className="h-3.5 w-3.5" />
            <span>{locale === 'vi-VN' ? '+ Thu nhập' : '+ Income'}</span>
          </button>
          <button
            type="button"
            onClick={() => navigate('/transactions?action=expense')}
            className="flex-1 sm:flex-none flex items-center justify-center gap-2 px-4 py-2.5 rounded-xl bg-rose-600 hover:bg-rose-700 text-white text-xs font-bold shadow-xs transition-colors cursor-pointer"
          >
            <Minus className="h-3.5 w-3.5" />
            <span>{locale === 'vi-VN' ? '- Chi tiêu' : '- Expense'}</span>
          </button>
          <button
            type="button"
            onClick={() => navigate('/transfer')}
            className="flex-1 sm:flex-none flex items-center justify-center gap-2 px-4 py-2.5 rounded-xl bg-sky-600 hover:bg-sky-700 text-white text-xs font-bold shadow-xs transition-colors cursor-pointer"
          >
            <Send className="h-3.5 w-3.5" />
            <span>{locale === 'vi-VN' ? 'Chuyển tiền' : 'Transfer'}</span>
          </button>
          <button
            type="button"
            onClick={() => navigate('/groups')}
            className="flex-1 sm:flex-none flex items-center justify-center gap-2 px-4 py-2.5 rounded-xl bg-purple-600 hover:bg-purple-700 text-white text-xs font-bold shadow-xs transition-colors cursor-pointer"
          >
            <Users className="h-3.5 w-3.5" />
            <span>{locale === 'vi-VN' ? 'Quỹ nhóm' : 'Group Funds'}</span>
          </button>
        </div>

        {/* DASH-009: NET BAR */}
        <div className="w-full lg:w-80 space-y-1.5 shrink-0">
          <div className="flex items-center justify-between text-xs font-semibold">
            <span className="text-slate-500">{locale === 'vi-VN' ? 'Cán cân dòng tiền' : 'Cash Flow Balance'}</span>
            <span className="font-mono text-emerald-600 dark:text-emerald-400 font-bold">{incomeRatio}% Inflow</span>
          </div>
          <div className="w-full h-2 rounded-full bg-slate-200 dark:bg-slate-800 overflow-hidden flex">
            <div style={{ width: `${incomeRatio}%` }} className="h-full bg-emerald-500" />
            <div style={{ width: `${100 - incomeRatio}%` }} className="h-full bg-rose-500" />
          </div>
        </div>
      </div>

      {/* ANALYTICS SECTION: SIDE-BY-SIDE ANALYTICAL TELEMETRY (DASH-011 & DASH-012) */}
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 items-stretch">
        {/* DASH-SEC-05: 6-MONTH COMPARISON (DASH-011) -> 7 Cols */}
        <div className="lg:col-span-7 p-5 rounded-2xl bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 shadow-xs flex flex-col justify-between">
          <div>
            <div className="flex items-center justify-between pb-3 border-b border-slate-100 dark:border-slate-800">
              <div className="flex items-center gap-2">
                <BarChart3 className="h-4 w-4 text-cyan-600 dark:text-cyan-400" />
                <h3 className="text-sm font-bold text-slate-900 dark:text-white uppercase tracking-wider">
                  {locale === 'vi-VN' ? 'Lịch sử dòng tiền 6 tháng' : '6-Month Trendline'}
                </h3>
              </div>
              <div className="flex items-center gap-1 bg-slate-100 dark:bg-slate-800 p-1 rounded-lg text-xs font-bold">
                <button
                  type="button"
                  onClick={() => setBarChartFlow('expense')}
                  className={cn(
                    'px-2.5 py-1 rounded-md transition-colors cursor-pointer',
                    barChartFlow === 'expense' ? 'bg-rose-500 text-white' : 'text-slate-500 hover:text-slate-900'
                  )}
                >
                  {locale === 'vi-VN' ? 'Chi' : 'Exp'}
                </button>
                <button
                  type="button"
                  onClick={() => setBarChartFlow('income')}
                  className={cn(
                    'px-2.5 py-1 rounded-md transition-colors cursor-pointer',
                    barChartFlow === 'income' ? 'bg-emerald-500 text-white' : 'text-slate-500 hover:text-slate-900'
                  )}
                >
                  {locale === 'vi-VN' ? 'Thu' : 'Inc'}
                </button>
              </div>
            </div>

            <div className="pt-6 pb-2">
              <div className="grid grid-cols-6 gap-2 sm:gap-4 h-44 items-end">
                {monthlyData.map((slot) => {
                  const barHeight = Math.max(10, Math.round((slot.total / maxMonthValue) * 100));
                  return (
                    <div key={`${slot.year}-${slot.month}`} className="flex flex-col items-center gap-1.5 h-full justify-end group">
                      <span className="text-[10px] font-mono font-medium text-slate-400 opacity-0 group-hover:opacity-100 transition-opacity">
                        {formatMoney(slot.total, locale).replace('₫', '').trim()}
                      </span>
                      <div className="w-full max-w-[32px] h-full flex items-end">
                        <div
                          style={{ height: `${barHeight}%` }}
                          className={cn(
                            'w-full rounded-lg transition-all duration-300',
                            barChartFlow === 'expense'
                              ? 'bg-rose-500/80 group-hover:bg-rose-600'
                              : 'bg-emerald-500/80 group-hover:bg-emerald-600'
                          )}
                        />
                      </div>
                      <span className="text-xs font-bold text-slate-500 truncate">
                        {locale === 'vi-VN' ? slot.labelVi : slot.labelEn}
                      </span>
                    </div>
                  );
                })}
              </div>
            </div>
          </div>

          <div className="pt-3 border-t border-slate-100 dark:border-slate-800 text-[11px] text-slate-400 flex items-center justify-between">
            <span>{locale === 'vi-VN' ? 'Phân tích tự động' : 'Automatic analytics'}</span>
            <span className="font-mono font-bold text-slate-700 dark:text-slate-300">
              {locale === 'vi-VN' ? `Tổng: ${formatMoney(totalSixMonths, locale)}` : `Total: ${formatMoney(totalSixMonths, locale)}`}
            </span>
          </div>
        </div>

        {/* DASH-SEC-06: CATEGORY BREAKDOWN (DASH-012) -> 5 Cols */}
        <div className="lg:col-span-5 p-5 rounded-2xl bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 shadow-xs flex flex-col justify-between">
          <div>
            <div className="flex items-center justify-between pb-3 border-b border-slate-100 dark:border-slate-800">
              <div className="flex items-center gap-2">
                <PieIcon className="h-4 w-4 text-cyan-600 dark:text-cyan-400" />
                <h3 className="text-sm font-bold text-slate-900 dark:text-white uppercase tracking-wider">
                  {locale === 'vi-VN' ? 'Phân loại chi tiêu tháng' : 'Category Telemetry'}
                </h3>
              </div>
              <div className="flex items-center gap-1 bg-slate-100 dark:bg-slate-800 p-1 rounded-lg text-xs font-bold">
                <button
                  type="button"
                  onClick={() => setDonutFlow('expense')}
                  className={cn(
                    'px-2.5 py-1 rounded-md transition-colors cursor-pointer',
                    donutFlow === 'expense' ? 'bg-rose-500 text-white' : 'text-slate-500 hover:text-slate-900'
                  )}
                >
                  {locale === 'vi-VN' ? 'Chi' : 'Exp'}
                </button>
                <button
                  type="button"
                  onClick={() => setDonutFlow('income')}
                  className={cn(
                    'px-2.5 py-1 rounded-md transition-colors cursor-pointer',
                    donutFlow === 'income' ? 'bg-emerald-500 text-white' : 'text-slate-500 hover:text-slate-900'
                  )}
                >
                  {locale === 'vi-VN' ? 'Thu' : 'Inc'}
                </button>
              </div>
            </div>

            <div className="py-5 flex flex-col sm:flex-row items-center justify-center gap-5">
              <div className="relative w-32 h-32 shrink-0 flex items-center justify-center">
                <svg className="w-full h-full -rotate-90" viewBox="0 0 160 160">
                  <circle
                    cx="80"
                    cy="80"
                    r={radius}
                    fill="transparent"
                    stroke="currentColor"
                    strokeWidth={18}
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
                      strokeWidth={hoveredSlice === segment.key ? 22 : 18}
                      strokeDasharray={segment.strokeDasharray}
                      strokeDashoffset={segment.strokeDashoffset}
                      className="transition-all duration-300 cursor-pointer"
                      onMouseEnter={() => setHoveredSlice(segment.key)}
                      onMouseLeave={() => setHoveredSlice(null)}
                    />
                  ))}
                </svg>
                <div className="absolute inset-0 flex flex-col items-center justify-center pointer-events-none text-center">
                  <span className="text-[10px] font-bold uppercase text-slate-400">
                    {locale === 'vi-VN' ? 'Tổng' : 'Total'}
                  </span>
                  <span className="text-xs font-black font-mono text-slate-900 dark:text-white">
                    {formatMoney(totalAmount, locale).replace('₫', '').trim()}
                  </span>
                </div>
              </div>

              <div className="space-y-1.5 flex-1 w-full max-h-40 overflow-y-auto pr-1">
                {categorySlices.length === 0 ? (
                  <p className="text-xs text-slate-400 italic text-center py-4">
                    {locale === 'vi-VN' ? 'Không có giao dịch' : 'No telemetry data'}
                  </p>
                ) : (
                  categorySlices.map((slice) => (
                    <div
                      key={slice.key}
                      onMouseEnter={() => setHoveredSlice(slice.key)}
                      onMouseLeave={() => setHoveredSlice(null)}
                      className={cn(
                        'flex items-center justify-between p-1.5 rounded-lg text-xs transition-colors',
                        hoveredSlice === slice.key ? 'bg-slate-100 dark:bg-slate-800' : ''
                      )}
                    >
                      <div className="flex items-center gap-2 truncate">
                        <span className="w-2 h-2 rounded-full shrink-0" style={{ backgroundColor: slice.color }} />
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

          <div className="pt-3 border-t border-slate-100 dark:border-slate-800 text-[11px] text-slate-400 flex items-center justify-between">
            <span>{locale === 'vi-VN' ? 'Tháng này' : 'Current Month'}</span>
            <span className="font-mono">{categorySlices.length} {locale === 'vi-VN' ? 'nhóm' : 'items'}</span>
          </div>
        </div>
      </div>

      {/* DASH-SEC-07: RECENT TRANSACTIONS LEDGER (DASH-013) */}
      <section className="p-5 rounded-2xl bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 shadow-xs">
        <div className="flex items-center justify-between pb-3 border-b border-slate-100 dark:border-slate-800">
          <div className="flex items-center gap-2">
            <Clock className="h-4 w-4 text-cyan-600 dark:text-cyan-400" />
            <h3 className="text-sm font-bold text-slate-900 dark:text-white uppercase tracking-wider">
              {locale === 'vi-VN' ? 'Nhật ký 5 giao dịch phát sinh gần nhất' : 'Recent Event Log (5)'}
            </h3>
          </div>
          <button
            type="button"
            onClick={() => navigate('/transactions')}
            className="text-xs font-bold text-cyan-600 dark:text-cyan-400 hover:underline flex items-center gap-1 cursor-pointer"
          >
            <span>{locale === 'vi-VN' ? 'Mở sổ cái giao dịch' : 'Open Ledger'}</span>
            <ChevronRight className="h-4 w-4" />
          </button>
        </div>

        <div className="divide-y divide-slate-100 dark:divide-slate-800">
          {recentTransactions.length === 0 ? (
            <p className="text-xs text-slate-400 italic py-6 text-center">
              {locale === 'vi-VN' ? 'Chưa có bản ghi giao dịch' : 'No transaction logs'}
            </p>
          ) : (
            recentTransactions.map((tx) => {
              const isIncome = tx.type === 'income' || (tx.amount && tx.amount > 0);
              return (
                <div key={tx.id} className="py-3 flex items-center justify-between gap-4">
                  <div className="flex items-center gap-3 min-w-0">
                    <div
                      className={cn(
                        'p-2 rounded-lg shrink-0',
                        isIncome ? 'bg-emerald-500/10 text-emerald-600' : 'bg-rose-500/10 text-rose-600'
                      )}
                    >
                      {isIncome ? <ArrowDownLeft className="h-4 w-4" /> : <ArrowUpRight className="h-4 w-4" />}
                    </div>
                    <div className="min-w-0">
                      <p className="text-xs font-bold text-slate-900 dark:text-white truncate">
                        {tx.description || tx.category || (locale === 'vi-VN' ? 'Giao dịch' : 'Transaction')}
                      </p>
                      <p className="text-[11px] text-slate-400 truncate">
                        {formatDate(tx.date, locale)} {tx.walletName ? `| ${tx.walletName}` : ''}
                      </p>
                    </div>
                  </div>

                  <div className="text-right shrink-0">
                    <div
                      className={cn(
                        'text-xs font-black font-mono tabular-nums',
                        isIncome ? 'text-emerald-600' : 'text-slate-900 dark:text-white'
                      )}
                    >
                      {isHidden
                        ? '••••••••'
                        : `${isIncome ? '+' : '-'}${formatMoney(Math.abs(tx.amount), locale)}`}
                    </div>
                    <span className="text-[10px] text-slate-400 font-mono uppercase">
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
