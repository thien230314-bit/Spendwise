import React from 'react';
import { ArrowRightLeft, TrendingUp } from 'lucide-react';
import { useI18n } from '../../../i18n/I18nContext';
import { useBalanceVisibility } from '../../../providers/BalanceVisibilityProvider';
import { formatMoney } from '../../../utils/money';
import { DashboardSummary } from '../../../types';

interface CashFlowBarProps {
  summary?: DashboardSummary;
}

export const CashFlowBar: React.FC<CashFlowBarProps> = ({ summary }) => {
  const { t, locale } = useI18n();
  const { isHidden } = useBalanceVisibility();

  if (!summary) return null;

  const income = formatMoney(summary.incomeThisMonth, locale, { isHidden });
  const expense = formatMoney(summary.expenseThisMonth, locale, { isHidden });
  const difference = formatMoney(summary.cashFlowDifference, locale, {
    showSign: true,
    forceSign: true,
    isHidden,
  });

  return (
    <div className="flex flex-wrap items-center justify-between gap-3 px-5 py-3 rounded-xl border border-slate-200/70 dark:border-slate-800/80 bg-white dark:bg-[#0f1422] text-xs shadow-[0_1px_3px_rgba(0,0,0,0.02),0_4px_12px_-2px_rgba(0,0,0,0.02)] dark:shadow-[0_2px_12px_-2px_rgba(0,0,0,0.4)]">
      <div className="flex items-center gap-2 text-slate-500 dark:text-slate-400 font-medium">
        <ArrowRightLeft className="h-3.5 w-3.5 text-slate-400" />
        <span className="font-semibold text-slate-600 dark:text-slate-300">{t('dashboard.cashFlowTitle')}:</span>
      </div>

      <div className="flex items-center flex-wrap gap-4 sm:gap-6 text-xs">
        <div className="flex items-center gap-1.5">
          <span className="text-slate-400 dark:text-slate-500 font-medium">{t('dashboard.income')}:</span>
          <span className="text-slate-800 dark:text-slate-200 font-bold tabular-nums">{income}</span>
        </div>

        <div className="flex items-center gap-1.5">
          <span className="text-slate-400 dark:text-slate-500 font-medium">{t('dashboard.expense')}:</span>
          <span className="text-slate-800 dark:text-slate-200 font-bold tabular-nums">{expense}</span>
        </div>

        <div className="flex items-center gap-1.5 pl-2.5 border-l border-slate-200 dark:border-slate-800">
          <span className="text-slate-400 dark:text-slate-500 font-medium">{t('dashboard.netDifference')}:</span>
          <span className="text-emerald-600 dark:text-emerald-400 font-bold tabular-nums flex items-center gap-0.5">
            <TrendingUp className="h-3 w-3 stroke-[2.5]" />
            {difference}
          </span>
        </div>
      </div>
    </div>
  );
};

