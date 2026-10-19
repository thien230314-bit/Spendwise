import React, { useState, useMemo } from 'react';
import { Link } from 'react-router-dom';
import { Tag, AlertCircle, PieChart as PieIcon, ArrowRight } from 'lucide-react';
import { useFinancial } from '../../../providers/FinancialProvider';
import { useAuth } from '../../../providers/AuthProvider';
import { useI18n } from '../../../i18n/I18nContext';
import { useBalanceVisibility } from '../../../providers/BalanceVisibilityProvider';
import { formatMoney } from '../../../utils/money';
import { getLocalizedCategoryName } from '../../../utils/localizationHelpers';
import { Skeleton } from '../../../components/ui/Skeleton';
import {
  getTransactionEffectiveDate,
  isUserCompletedTx,
  getAnalyticsFlow,
  getAnalyticsCategoryKey,
  EXPENSE_RED_PALETTE,
  INCOME_GREEN_PALETTE,
} from '../../../utils/analyticsHelpers';

type FlowType = 'expense' | 'income';

interface CategorySliceData {
  key: string;
  name: string;
  amount: number;
  percentage: number;
  color: string;
  needsReviewCount: number;
}

interface CurrentMonthCategoryDonutProps {
  isLoading?: boolean;
}

export const CurrentMonthCategoryDonut: React.FC<CurrentMonthCategoryDonutProps> = ({
  isLoading = false,
}) => {
  const { transactions, categories } = useFinancial();
  const { currentUser } = useAuth();
  const { t, locale } = useI18n();
  const { isHidden } = useBalanceVisibility();

  // Toggle state: default is 'expense' (Chi)
  const [activeFlow, setActiveFlow] = useState<FlowType>('expense');
  const [hoveredSlice, setHoveredSlice] = useState<string | null>(null);

  const now = new Date();
  const currentMonth = now.getMonth();
  const currentYear = now.getFullYear();
  const monthDisplayNum = currentMonth + 1;

  // Derive current month category distribution
  const { categorySlices, totalAmount, totalNeedsReview } = useMemo(() => {
    const categoryTotals: Record<string, number> = {};
    const reviewCounts: Record<string, number> = {};
    let total = 0;
    let totalReview = 0;

    transactions.forEach((tx) => {
      if (!isUserCompletedTx(tx, currentUser?.id)) return;

      const flow = getAnalyticsFlow(tx);
      if (flow !== activeFlow) return;

      const txDate = getTransactionEffectiveDate(tx);
      if (!txDate || txDate.getMonth() !== currentMonth || txDate.getFullYear() !== currentYear) return;

      const rawCat = tx.category?.trim() || '';
      const matchedCat = categories.find(
        (c) => c.id === rawCat || c.key === rawCat || c.name === rawCat
      );
      // Use stable category ID if matched, otherwise normalized key
      const catKey = matchedCat ? matchedCat.id : getAnalyticsCategoryKey(tx, activeFlow);
      const amt = Math.abs(tx.amount);

      categoryTotals[catKey] = (categoryTotals[catKey] || 0) + amt;
      total += amt;

      if (tx.needsCategoryReview) {
        reviewCounts[catKey] = (reviewCounts[catKey] || 0) + 1;
        totalReview += 1;
      }
    });

    const palette = activeFlow === 'expense' ? EXPENSE_RED_PALETTE : INCOME_GREEN_PALETTE;

    const slices: CategorySliceData[] = Object.entries(categoryTotals)
      .filter(([_, amt]) => amt > 0)
      .sort((a, b) => b[1] - a[1])
      .map(([key, amount], index) => {
        const matchingCategory = categories.find((c) => c.id === key || c.key === key || c.name === key);
        let name = '';
        if (matchingCategory) {
          if (matchingCategory.isSystem) {
            name = matchingCategory.key.startsWith('categories.')
              ? t(matchingCategory.key)
              : getLocalizedCategoryName(matchingCategory.name, t, categories);
          } else {
            // For custom category: strictly display its real User-entered name
            name = matchingCategory.name;
          }
        } else {
          name = getLocalizedCategoryName(key, t, categories);
        }

        const percentage = total > 0 ? Math.round((amount / total) * 100) : 0;
        // If custom category has a defined color, prefer that color in chart and legend
        const color = matchingCategory?.color || palette[index % palette.length];

        return {
          key,
          name,
          amount,
          percentage,
          color,
          needsReviewCount: reviewCounts[key] || 0,
        };
      });

    return {
      categorySlices: slices,
      totalAmount: total,
      totalNeedsReview: totalReview,
    };
  }, [transactions, categories, currentUser?.id, activeFlow, currentMonth, currentYear, t]);

  // SVG Donut geometry
  const radius = 64;
  const strokeWidth = 24;
  const circumference = 2 * Math.PI * radius;

  // Calculate SVG stroke segments
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

  const hoveredCategoryData = hoveredSlice
    ? categorySlices.find((s) => s.key === hoveredSlice)
    : null;

  if (isLoading) {
    return (
      <div className="bg-white dark:bg-[#0f1422] rounded-3xl border border-slate-100 dark:border-slate-800/80 p-5 sm:p-6 shadow-sm flex flex-col justify-between h-full">
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 pb-4 border-b border-slate-100 dark:border-slate-800">
          <div>
            <Skeleton className="h-5 w-44 mb-2" />
            <Skeleton className="h-3.5 w-32" />
          </div>
          <Skeleton className="h-8 w-32 rounded-2xl" />
        </div>
        <div className="flex flex-col items-center justify-center py-6">
          <Skeleton className="h-44 w-44 rounded-full" />
        </div>
        <div className="space-y-2 pt-2 border-t border-slate-100 dark:border-slate-800">
          {[1, 2, 3].map((i) => (
            <div key={i} className="flex items-center justify-between py-1">
              <div className="flex items-center gap-2">
                <Skeleton className="h-3 w-3 rounded-full" />
                <Skeleton className="h-3.5 w-24" />
              </div>
              <Skeleton className="h-3.5 w-16" />
            </div>
          ))}
        </div>
      </div>
    );
  }

  return (
    <div
      id="current-month-category-donut-card"
      className="bg-white dark:bg-[#0f1422] rounded-3xl border border-slate-100 dark:border-slate-800/80 p-5 sm:p-6 shadow-sm flex flex-col justify-between h-full"
    >
      {/* Header with Title and Chi / Thu Toggle */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 pb-4 border-b border-slate-100 dark:border-slate-800">
        <div>
          <h3 className="text-base font-bold text-slate-900 dark:text-white flex items-center gap-2">
            <PieIcon className="h-4 w-4 text-slate-500" />
            <span>
              {locale === 'vi-VN'
                ? `Phân bổ ${activeFlow === 'expense' ? 'chi tiêu' : 'thu nhập'} T${monthDisplayNum}`
                : `Current Month ${activeFlow === 'expense' ? 'Expenses' : 'Income'}`}
            </span>
          </h3>
          <p className="text-xs text-slate-400 font-medium mt-0.5">
            {locale === 'vi-VN'
              ? `Tháng ${monthDisplayNum}/${currentYear} • Phân loại thực tế`
              : `Month ${monthDisplayNum}/${currentYear} • Financial semantics`}
          </p>
        </div>

        {/* Semantic Toggle: Chi (Red) vs Thu (Green) */}
        <div className="flex items-center gap-1 bg-slate-100 dark:bg-slate-800/80 p-1 rounded-2xl self-start sm:self-auto">
          <button
            type="button"
            id="btn-donut-toggle-expense"
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
            id="btn-donut-toggle-income"
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

      {/* Main Content Area */}
      {totalAmount === 0 || categorySlices.length === 0 ? (
        /* Empty State */
        <div className="py-12 flex flex-col items-center justify-center text-center space-y-3">
          <div className="h-16 w-16 rounded-full bg-slate-100 dark:bg-slate-800 flex items-center justify-center text-slate-400">
            <PieIcon className="h-8 w-8 stroke-[1.5]" />
          </div>
          <div>
            <p className="text-sm font-semibold text-slate-700 dark:text-slate-300">
              {locale === 'vi-VN'
                ? `Chưa có giao dịch ${activeFlow === 'expense' ? 'chi tiêu' : 'thu nhập'} trong tháng ${monthDisplayNum}`
                : `No ${activeFlow === 'expense' ? 'expenses' : 'income'} recorded in month ${monthDisplayNum}`}
            </p>
            <p className="text-xs text-slate-400 mt-1 max-w-xs">
              {locale === 'vi-VN'
                ? `Giao dịch ${activeFlow === 'expense' ? 'chi tiêu' : 'thu nhập'} của bạn sẽ xuất hiện tại đây khi được đồng bộ hoặc tạo mới.`
                : `Transactions will automatically appear here once recorded or synced.`}
            </p>
          </div>
        </div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-12 gap-6 items-center py-4">
          {/* Donut Chart Visual (5 cols) */}
          <div className="md:col-span-5 flex flex-col items-center justify-center relative">
            <div className="relative w-44 h-44 sm:w-48 sm:h-48 flex items-center justify-center">
              <svg className="w-full h-full transform -rotate-90" viewBox="0 0 160 160">
                {/* Background Ring */}
                <circle
                  cx="80"
                  cy="80"
                  r={radius}
                  fill="transparent"
                  stroke="currentColor"
                  strokeWidth={strokeWidth}
                  className="text-slate-100 dark:text-slate-800"
                />

                {/* Slices */}
                {svgSegments.map((segment) => {
                  const isHovered = hoveredSlice === segment.key;
                  return (
                    <circle
                      key={segment.key}
                      cx="80"
                      cy="80"
                      r={radius}
                      fill="transparent"
                      stroke={segment.color}
                      strokeWidth={isHovered ? strokeWidth + 4 : strokeWidth}
                      strokeDasharray={segment.strokeDasharray}
                      strokeDashoffset={segment.strokeDashoffset}
                      strokeLinecap="butt"
                      className="transition-all duration-200 cursor-pointer"
                      onMouseEnter={() => setHoveredSlice(segment.key)}
                      onMouseLeave={() => setHoveredSlice(null)}
                    >
                      <title>{`${segment.name}: ${formatMoney(segment.amount, locale)} (${segment.percentage}%)`}</title>
                    </circle>
                  );
                })}
              </svg>

              {/* Center Summary Label */}
              <div className="absolute inset-0 flex flex-col items-center justify-center pointer-events-none text-center px-2">
                <span className="text-[11px] font-semibold text-slate-400 uppercase tracking-wider truncate max-w-[120px]">
                  {hoveredCategoryData
                    ? hoveredCategoryData.name
                    : (locale === 'vi-VN'
                        ? (activeFlow === 'expense' ? 'Tổng chi' : 'Tổng thu')
                        : (activeFlow === 'expense' ? 'Total spent' : 'Total earned'))}
                </span>
                <span
                  className={`text-base sm:text-lg font-black tracking-tight tabular-nums mt-0.5 ${
                    activeFlow === 'expense'
                      ? 'text-rose-600 dark:text-rose-400'
                      : 'text-emerald-600 dark:text-emerald-400'
                  }`}
                >
                  {isHidden
                    ? '••••••••'
                    : formatMoney(hoveredCategoryData ? hoveredCategoryData.amount : totalAmount, locale)}
                </span>
                <span className="text-[10px] text-slate-400 font-medium">
                  {hoveredCategoryData
                    ? `${hoveredCategoryData.percentage}%`
                    : `T${monthDisplayNum}/${currentYear}`}
                </span>
              </div>
            </div>
          </div>

          {/* Categories Legend List (7 cols) */}
          <div className="md:col-span-7 space-y-2.5 max-h-60 overflow-y-auto pr-1">
            {categorySlices.map((slice) => {
              const isHovered = hoveredSlice === slice.key;
              const isOtherCategory =
                slice.key === 'categories.expense_other' ||
                slice.key === 'categories.income_other' ||
                slice.key === 'categories.other' ||
                slice.key === 'categories.others';

              return (
                <div
                  key={slice.key}
                  onMouseEnter={() => setHoveredSlice(slice.key)}
                  onMouseLeave={() => setHoveredSlice(null)}
                  className={`p-2.5 rounded-2xl border transition-all ${
                    isHovered
                      ? 'border-slate-300 dark:border-slate-700 bg-slate-50 dark:bg-slate-800/80 shadow-2xs'
                      : 'border-slate-100 dark:border-slate-800/60 bg-transparent'
                  }`}
                >
                  <div className="flex items-center justify-between gap-2">
                    <div className="flex items-center gap-2 min-w-0">
                      <span
                        className="h-3 w-3 rounded-full shrink-0"
                        style={{ backgroundColor: slice.color }}
                      />
                      <span className="text-xs font-semibold text-slate-800 dark:text-slate-200 truncate">
                        {slice.name}
                      </span>
                      <span className="text-[11px] font-bold text-slate-400 shrink-0">
                        {slice.percentage}%
                      </span>
                    </div>

                    <span className="text-xs font-bold text-slate-900 dark:text-white tabular-nums shrink-0">
                      {isHidden ? '••••••••' : formatMoney(slice.amount, locale)}
                    </span>
                  </div>

                  {/* Unclassified / Needs Review Alert */}
                  {isOtherCategory && slice.needsReviewCount > 0 && (
                    <div className="mt-2 pt-1.5 border-t border-slate-100 dark:border-slate-800 flex items-center justify-between">
                      <div className="flex items-center gap-1.5 text-amber-600 dark:text-amber-400">
                        <AlertCircle className="h-3.5 w-3.5 shrink-0" />
                        <span className="text-[11px] font-medium">
                          {locale === 'vi-VN'
                            ? `${slice.needsReviewCount} giao dịch cần phân loại`
                            : `${slice.needsReviewCount} transactions need review`}
                        </span>
                      </div>
                      <Link
                        to={`/transactions?filter=needs_review&category=${encodeURIComponent(slice.key)}`}
                        className="inline-flex items-center gap-1 text-[11px] font-bold text-amber-600 dark:text-amber-400 hover:text-amber-700 dark:hover:text-amber-300 transition-colors"
                      >
                        <span>{locale === 'vi-VN' ? 'Phân loại ngay' : 'Classify'}</span>
                        <ArrowRight className="h-3 w-3" />
                      </Link>
                    </div>
                  )}
                </div>
              );
            })}
          </div>
        </div>
      )}

      {/* Footer Info if there are pending reviews in other categories */}
      {totalNeedsReview > 0 && (
        <div className="pt-3 mt-2 border-t border-slate-100 dark:border-slate-800/80 flex items-center justify-between">
          <span className="text-xs text-slate-500 dark:text-slate-400">
            {locale === 'vi-VN'
              ? `Có tổng cộng ${totalNeedsReview} giao dịch chưa phân loại chính xác trong tháng.`
              : `Total ${totalNeedsReview} transactions pending category review this month.`}
          </span>
          <Link
            to="/transactions?filter=needs_review"
            className="text-xs font-bold text-amber-600 dark:text-amber-400 hover:underline flex items-center gap-1 shrink-0"
          >
            <span>{locale === 'vi-VN' ? 'Xem tất cả' : 'Review all'}</span>
            <ArrowRight className="h-3 w-3" />
          </Link>
        </div>
      )}
    </div>
  );
};
