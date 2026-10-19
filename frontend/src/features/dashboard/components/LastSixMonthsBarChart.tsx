import React, { useState, useMemo } from 'react';
import { BarChart3, Calendar } from 'lucide-react';
import { useFinancial } from '../../../providers/FinancialProvider';
import { useAuth } from '../../../providers/AuthProvider';
import { useI18n } from '../../../i18n/I18nContext';
import { useBalanceVisibility } from '../../../providers/BalanceVisibilityProvider';
import { formatMoney } from '../../../utils/money';
import { Skeleton } from '../../../components/ui/Skeleton';
import {
  getTransactionEffectiveDate,
  isUserCompletedTx,
  getAnalyticsFlow,
  getLast6CalendarMonths,
  MonthSlot,
} from '../../../utils/analyticsHelpers';

type FlowType = 'expense' | 'income';

interface MonthBarData extends MonthSlot {
  total: number;
  count: number;
}

interface LastSixMonthsBarChartProps {
  isLoading?: boolean;
}

export const LastSixMonthsBarChart: React.FC<LastSixMonthsBarChartProps> = ({
  isLoading = false,
}) => {
  const { transactions } = useFinancial();
  const { currentUser } = useAuth();
  const { t, locale } = useI18n();
  const { isHidden } = useBalanceVisibility();

  // Toggle state: default is 'expense' (Chi)
  const [activeFlow, setActiveFlow] = useState<FlowType>('expense');
  const [hoveredIndex, setHoveredIndex] = useState<number | null>(null);

  // Compute exactly 6 calendar months ending with the current month
  const monthlyData: MonthBarData[] = useMemo(() => {
    const slots = getLast6CalendarMonths(new Date());

    return slots.map((slot) => {
      let total = 0;
      let count = 0;

      transactions.forEach((tx) => {
        if (!isUserCompletedTx(tx, currentUser?.id)) return;

        const flow = getAnalyticsFlow(tx);
        if (flow !== activeFlow) return;

        const txDate = getTransactionEffectiveDate(tx);
        if (!txDate) return;
        if (txDate.getMonth() === slot.month && txDate.getFullYear() === slot.year) {
          total += Math.abs(tx.amount);
          count += 1;
        }
      });

      return {
        ...slot,
        total,
        count,
      };
    });
  }, [transactions, currentUser?.id, activeFlow]);

  // Overall metrics for the 6 months
  const totalSixMonths = useMemo(() => {
    return monthlyData.reduce((acc, curr) => acc + curr.total, 0);
  }, [monthlyData]);

  const maxMonthValue = useMemo(() => {
    const max = Math.max(...monthlyData.map((m) => m.total));
    return max > 0 ? max : 1000000;
  }, [monthlyData]);

  const averageMonthly = useMemo(() => {
    return Math.round(totalSixMonths / 6);
  }, [totalSixMonths]);

  if (isLoading) {
    return (
      <div className="bg-white dark:bg-[#0f1422] rounded-3xl border border-slate-100 dark:border-slate-800/80 p-5 sm:p-6 shadow-sm flex flex-col justify-between h-full">
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 pb-4 border-b border-slate-100 dark:border-slate-800">
          <div>
            <Skeleton className="h-5 w-48 mb-2" />
            <Skeleton className="h-3.5 w-36" />
          </div>
          <Skeleton className="h-8 w-32 rounded-2xl" />
        </div>
        <div className="grid grid-cols-2 gap-3 py-4">
          <Skeleton className="h-16 rounded-2xl" />
          <Skeleton className="h-16 rounded-2xl" />
        </div>
        <div className="h-44 flex items-end justify-between gap-3 pt-4 px-2">
          {[40, 65, 30, 85, 55, 75].map((pct, idx) => (
            <div key={idx} className="flex-1 flex flex-col items-center gap-2 h-full justify-end">
              <Skeleton className="w-full max-w-[38px] rounded-t-xl" style={{ height: `${pct}%` }} />
              <Skeleton className="h-3 w-8" />
            </div>
          ))}
        </div>
        <div className="pt-3 border-t border-slate-100 dark:border-slate-800 flex items-center justify-between">
          <Skeleton className="h-3 w-40" />
          <Skeleton className="h-3 w-24" />
        </div>
      </div>
    );
  }

  return (
    <div
      id="last-six-months-barchart-card"
      className="bg-white dark:bg-[#0f1422] rounded-3xl border border-slate-100 dark:border-slate-800/80 p-5 sm:p-6 shadow-sm flex flex-col justify-between h-full"
    >
      {/* Header */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 pb-4 border-b border-slate-100 dark:border-slate-800">
        <div>
          <h3 className="text-base font-bold text-slate-900 dark:text-white flex items-center gap-2">
            <BarChart3 className="h-4 w-4 text-slate-500" />
            <span>
              {locale === 'vi-VN' ? 'Thu / Chi 6 tháng gần nhất' : 'Last 6 Months Trend'}
            </span>
          </h3>
          <p className="text-xs text-slate-400 font-medium mt-0.5 flex items-center gap-1.5">
            <Calendar className="h-3 w-3" />
            <span>
              {(locale === 'vi-VN' ? monthlyData[0]?.labelVi : monthlyData[0]?.labelEn)} - {(locale === 'vi-VN' ? monthlyData[5]?.labelVi : monthlyData[5]?.labelEn)} (
              {locale === 'vi-VN' ? 'Hiển thị đơn chuỗi' : 'Single series'}
              )
            </span>
          </p>
        </div>

        {/* Semantic Toggle: Chi (Red) vs Thu (Green) */}
        <div className="flex items-center gap-1 bg-slate-100 dark:bg-slate-800/80 p-1 rounded-2xl self-start sm:self-auto">
          <button
            type="button"
            id="btn-barchart-toggle-expense"
            onClick={() => setActiveFlow('expense')}
            className={`px-3.5 py-1.5 text-xs font-bold rounded-xl transition-all cursor-pointer ${
              activeFlow === 'expense'
                ? 'bg-rose-500 text-white shadow-xs'
                : 'text-slate-600 dark:text-slate-400 hover:text-rose-500'
            }`}
          >
            {locale === 'vi-VN' ? 'Chi' : 'Expenses'}
          </button>
          <button
            type="button"
            id="btn-barchart-toggle-income"
            onClick={() => setActiveFlow('income')}
            className={`px-3.5 py-1.5 text-xs font-bold rounded-xl transition-all cursor-pointer ${
              activeFlow === 'income'
                ? 'bg-emerald-500 text-white shadow-xs'
                : 'text-slate-600 dark:text-slate-400 hover:text-emerald-500'
            }`}
          >
            {locale === 'vi-VN' ? 'Thu' : 'Income'}
          </button>
        </div>
      </div>

      {/* Summary Row */}
      <div className="grid grid-cols-2 gap-3 py-3 px-3.5 my-2 rounded-2xl bg-slate-50 dark:bg-slate-800/40 border border-slate-100 dark:border-slate-800">
        <div>
          <span className="text-[11px] font-semibold text-slate-400 block">
            {locale === 'vi-VN'
              ? `Tổng ${activeFlow === 'expense' ? 'chi' : 'thu'} 6 tháng`
              : `6-Month Total ${activeFlow === 'expense' ? 'Expenses' : 'Income'}`}
          </span>
          <span
            className={`text-sm sm:text-base font-extrabold tabular-nums ${
              activeFlow === 'expense'
                ? 'text-rose-600 dark:text-rose-400'
                : 'text-emerald-600 dark:text-emerald-400'
            }`}
          >
            {isHidden ? '••••••••' : formatMoney(totalSixMonths, locale)}
          </span>
        </div>

        <div>
          <span className="text-[11px] font-semibold text-slate-400 block">
            {locale === 'vi-VN' ? 'Trung bình tháng' : 'Monthly Average'}
          </span>
          <span className="text-sm sm:text-base font-bold text-slate-800 dark:text-slate-200 tabular-nums">
            {isHidden ? '••••••••' : formatMoney(averageMonthly, locale)}
          </span>
        </div>
      </div>

      {/* Chart Canvas */}
      <div className="py-2">
        <div className="relative h-52 w-full flex items-end justify-between gap-2 sm:gap-4 pt-8 pb-6 px-1 sm:px-2">
          {/* Subtle horizontal grid lines */}
          <div className="absolute inset-x-0 top-8 bottom-6 flex flex-col justify-between pointer-events-none opacity-40">
            <div className="border-b border-dashed border-slate-200 dark:border-slate-700 w-full" />
            <div className="border-b border-dashed border-slate-200 dark:border-slate-700 w-full" />
            <div className="border-b border-slate-200 dark:border-slate-700 w-full" />
          </div>

          {/* 6 Monthly Columns */}
          {monthlyData.map((item, index) => {
            const isHovered = hoveredIndex === index;
            const barHeightPct =
              item.total > 0
                ? Math.min(100, Math.max(6, Math.round((item.total / maxMonthValue) * 100)))
                : 0;

            return (
              <div
                key={`${item.year}-${item.month}`}
                className="relative flex-1 h-full flex flex-col items-center justify-end group cursor-pointer"
                onMouseEnter={() => setHoveredIndex(index)}
                onMouseLeave={() => setHoveredIndex(null)}
              >
                {/* Floating Tooltip / Value */}
                <div
                  className={`absolute -top-6 transition-all duration-200 pointer-events-none z-10 whitespace-nowrap px-2 py-0.5 rounded-md text-[10px] font-bold shadow-xs ${
                    isHovered
                      ? 'opacity-100 -translate-y-1 bg-slate-900 text-white dark:bg-white dark:text-slate-900 scale-105'
                      : 'opacity-0 sm:opacity-75 bg-transparent text-slate-500 dark:text-slate-400'
                  }`}
                >
                  {isHidden ? '••••••••' : formatMoney(item.total, locale)}
                </div>

                {/* Track Background */}
                <div className="w-full max-w-[38px] h-full flex items-end bg-slate-100/70 dark:bg-slate-800/40 rounded-t-xl overflow-hidden p-0.5">
                  {/* Active Bar */}
                  <div
                    className={`w-full rounded-t-lg transition-all duration-300 ease-out ${
                      activeFlow === 'expense'
                        ? isHovered
                          ? 'bg-rose-600'
                          : 'bg-rose-500'
                        : isHovered
                        ? 'bg-emerald-600'
                        : 'bg-emerald-500'
                    }`}
                    style={{
                      height: `${barHeightPct}%`,
                      minHeight: item.total > 0 ? '6px' : '0px',
                    }}
                  />
                </div>

                {/* Month Label */}
                <div className="absolute -bottom-5 flex flex-col items-center">
                  <span
                    className={`text-xs tabular-nums transition-colors ${
                      item.isCurrentMonth
                        ? 'font-black text-slate-900 dark:text-white'
                        : 'font-semibold text-slate-500 dark:text-slate-400'
                    }`}
                  >
                    {locale === 'vi-VN' ? item.labelVi : item.labelEn}
                  </span>
                </div>
              </div>
            );
          })}
        </div>
      </div>

      {/* Footer Info */}
      <div className="pt-2 border-t border-slate-100 dark:border-slate-800 flex items-center justify-between text-xs text-slate-400">
        <span>
          {locale === 'vi-VN'
            ? `Cột tô màu thể hiện giá trị ${activeFlow === 'expense' ? 'chi tiêu' : 'thu nhập'} thực tế.`
            : `Bars reflect actual ${activeFlow === 'expense' ? 'expenses' : 'income'} by transaction semantic.`}
        </span>
        <span className="font-semibold text-slate-600 dark:text-slate-300">
          {locale === 'vi-VN'
            ? `Tháng hiện tại: ${monthlyData[5]?.labelVi}`
            : `Current: ${monthlyData[5]?.labelEn}`}
        </span>
      </div>
    </div>
  );
};
