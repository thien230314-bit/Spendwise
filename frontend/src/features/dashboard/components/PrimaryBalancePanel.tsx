import React from 'react';
import { useNavigate } from 'react-router-dom';
import { Eye, EyeOff, Plus, Minus, ArrowUpRight, TrendingUp } from 'lucide-react';
import { useI18n } from '../../../i18n/I18nContext';
import { useBalanceVisibility } from '../../../providers/BalanceVisibilityProvider';
import { formatMoney } from '../../../utils/money';
import { DashboardSummary } from '../../../types';
import { Skeleton } from '../../../components/ui/Skeleton';

interface PrimaryBalancePanelProps {
  summary?: DashboardSummary;
  isLoading?: boolean;
}

export const PrimaryBalancePanel: React.FC<PrimaryBalancePanelProps> = ({
  summary,
  isLoading = false,
}) => {
  const { t, locale } = useI18n();
  const { isHidden, toggleVisibility } = useBalanceVisibility();
  const navigate = useNavigate();

  if (isLoading || !summary) {
    return (
      <div className="h-[180px] rounded-3xl border border-slate-100 dark:border-slate-800 bg-white dark:bg-slate-900 p-6 shadow-sm flex flex-col justify-between">
        <div>
          <Skeleton className="h-4 w-28 mb-3" />
          <Skeleton className="h-10 w-56 mb-2" />
          <Skeleton className="h-4 w-40" />
        </div>
        <div className="flex gap-3">
          <Skeleton className="h-10 flex-1 rounded-xl" />
          <Skeleton className="h-10 flex-1 rounded-xl" />
          <Skeleton className="h-10 w-12 rounded-xl" />
        </div>
      </div>
    );
  }

  // Break formatted balance down to style value and currency symbol cleanly if not hidden
  const rawFormatted = formatMoney(summary.totalBalance, locale, { isHidden });
  let valuePart = rawFormatted;
  let symbolPart = '';

  if (!isHidden) {
    if (locale === 'vi-VN') {
      const parts = rawFormatted.split(' ');
      if (parts.length > 1) {
        symbolPart = parts.pop() || '';
        valuePart = parts.join(' ');
      }
    }
  }

  return (
    <div className="bg-white dark:bg-[#0f1422] rounded-2xl p-5 sm:p-6 border border-slate-200/80 dark:border-slate-800/80 shadow-xs flex flex-col justify-between h-full">
      <div>
        <p className="text-xs sm:text-sm font-semibold tracking-wide text-slate-500 dark:text-slate-400 flex items-center">
          <span>{locale === 'vi-VN' ? 'Số dư ví khả dụng' : 'Available Wallet Balance'}</span>
          <button
            type="button"
            onClick={toggleVisibility}
            className="ml-2 p-1 rounded-md text-slate-400 hover:text-slate-600 dark:hover:text-slate-200 hover:bg-slate-100 dark:hover:bg-slate-800/60 transition-colors focus:outline-none cursor-pointer"
            aria-label={isHidden ? t('common.showBalance') : t('common.hideBalance')}
            title={isHidden ? t('common.showBalance') : t('common.hideBalance')}
          >
            {isHidden ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
          </button>
        </p>

        <div className="mt-1 flex items-baseline">
          <h2 className="text-3xl sm:text-4xl font-extrabold tracking-tight text-slate-900 dark:text-slate-50 tabular-nums">
            {valuePart}
            {symbolPart && (
              <span className="text-xl sm:text-2xl font-normal text-slate-400 dark:text-slate-500 ml-1.5">
                {symbolPart}
              </span>
            )}
          </h2>
        </div>

        {summary.totalGoalsReserved && summary.totalGoalsReserved > 0 ? (
          <p className="text-xs text-slate-500 dark:text-slate-400 mt-1.5 font-medium">
            {locale === 'vi-VN'
              ? `Tổng tài sản theo dõi: ${formatMoney(summary.totalTrackedAssets ?? (summary.totalBalance + summary.totalGoalsReserved), locale, { isHidden })} (gồm ${formatMoney(summary.totalGoalsReserved, locale, { isHidden })} trong mục tiêu)`
              : `Total tracked assets: ${formatMoney(summary.totalTrackedAssets ?? (summary.totalBalance + summary.totalGoalsReserved), locale, { isHidden })} (incl. ${formatMoney(summary.totalGoalsReserved, locale, { isHidden })} in goals)`}
          </p>
        ) : null}

        {typeof summary.balanceChangePercentage === 'number' && (
          <p className="text-xs text-emerald-600 dark:text-emerald-400 mt-2 font-semibold flex items-center gap-1">
            <TrendingUp className="h-3.5 w-3.5 stroke-[2.5]" />
            <span>+{summary.balanceChangePercentage}% {t('dashboard.balanceGrowth')}</span>
          </p>
        )}
      </div>

      {/* Embedded High-Efficiency Action Row */}
      <div className="flex items-center gap-2.5 sm:gap-3 mt-5">
        <button
          type="button"
          onClick={() => navigate('/transactions?tab=income')}
          className="flex-1 py-2.5 px-3 bg-emerald-600 text-white rounded-xl text-sm font-semibold hover:bg-emerald-500 active:scale-[0.98] transition-all flex items-center justify-center shadow-xs shadow-emerald-950/10 border border-emerald-500/20 cursor-pointer"
        >
          <Plus className="h-4 w-4 mr-1.5 stroke-[2.5]" />
          <span>{t('quickActions.income')}</span>
        </button>

        <button
          type="button"
          onClick={() => navigate('/transactions?tab=expense')}
          className="flex-1 py-2.5 px-3 bg-slate-900 text-white dark:bg-slate-800 dark:text-slate-100 dark:hover:bg-slate-700 rounded-xl text-sm font-semibold hover:bg-slate-800 active:scale-[0.98] transition-all flex items-center justify-center shadow-xs border border-slate-700/50 cursor-pointer"
        >
          <Minus className="h-4 w-4 mr-1.5 stroke-[2.5]" />
          <span>{t('quickActions.expense')}</span>
        </button>

        <button
          type="button"
          onClick={() => navigate('/transfer')}
          className="w-11 h-10 bg-slate-100 hover:bg-slate-200/80 dark:bg-slate-800/80 dark:hover:bg-slate-700 text-slate-700 dark:text-slate-200 rounded-xl flex items-center justify-center transition-colors border border-slate-200/70 dark:border-slate-700/70 shadow-2xs shrink-0 cursor-pointer"
          title={t('quickActions.transfer')}
          aria-label={t('quickActions.transfer')}
        >
          <ArrowUpRight className="h-4 w-4" />
        </button>
      </div>
    </div>
  );
};

