import React from 'react';
import { ArrowDownRight, ArrowUpRight, PiggyBank, Users } from 'lucide-react';
import { useI18n } from '../../../i18n/I18nContext';
import { useBalanceVisibility } from '../../../providers/BalanceVisibilityProvider';
import { formatMoney } from '../../../utils/money';
import { DashboardSummary } from '../../../types';
import { Skeleton } from '../../../components/ui/Skeleton';

interface FinancialSummaryCardsProps {
  summary?: DashboardSummary;
  isLoading?: boolean;
}

export const FinancialSummaryCards: React.FC<FinancialSummaryCardsProps> = ({
  summary,
  isLoading = false,
}) => {
  const { t, locale } = useI18n();
  const { isHidden } = useBalanceVisibility();

  if (isLoading || !summary) {
    return (
      <div className="grid grid-cols-2 gap-3 sm:gap-4">
        {[1, 2, 3, 4].map((i) => (
          <div
            key={i}
            className="rounded-2xl border border-slate-100 dark:border-slate-800 bg-white dark:bg-slate-900 p-4"
          >
            <Skeleton className="h-3 w-16 mb-2" />
            <Skeleton className="h-6 w-24" />
          </div>
        ))}
      </div>
    );
  }

  const cards = [
    {
      id: 'income',
      title: t('dashboard.incomeThisMonth'),
      amount: formatMoney(summary.incomeThisMonth, locale, {
        showSign: true,
        forceSign: true,
        isHidden,
      }),
      rawAmount: summary.incomeThisMonth,
      icon: ArrowDownRight,
      amountColor: 'text-emerald-600 dark:text-emerald-400',
      iconColor: 'text-emerald-500 bg-emerald-50 dark:bg-emerald-950/40',
      accentBorder: 'border-l-[3px] border-l-emerald-500 dark:border-l-emerald-400',
    },
    {
      id: 'expense',
      title: t('dashboard.expenseThisMonth'),
      amount: isHidden
        ? '••••••••'
        : summary.expenseThisMonth === 0
        ? formatMoney(0, locale)
        : `−${formatMoney(summary.expenseThisMonth, locale).replace('+', '').replace('−', '')}`,
      rawAmount: summary.expenseThisMonth,
      icon: ArrowUpRight,
      amountColor: 'text-rose-500 dark:text-rose-400',
      iconColor: 'text-rose-500 bg-rose-50 dark:bg-rose-950/40',
      accentBorder: 'border-l-[3px] border-l-rose-500 dark:border-l-rose-400',
    },
    {
      id: 'budget',
      title: t('dashboard.budgetRemaining'),
      amount: formatMoney(summary.budgetRemaining, locale, { isHidden }),
      rawAmount: summary.budgetRemaining,
      icon: PiggyBank,
      amountColor: 'text-slate-800 dark:text-slate-200',
      iconColor: 'text-sky-500 bg-sky-50 dark:bg-sky-950/40',
      accentBorder: 'border-l-[3px] border-l-sky-500 dark:border-l-sky-400',
    },
    {
      id: 'group-fund',
      title: summary.userPendingFundObligations && summary.userPendingFundObligations > 0
        ? (locale === 'vi-VN' ? 'Cần góp quỹ' : 'Pending Fund')
        : (locale === 'vi-VN' ? 'Quỹ nhóm' : 'Group Fund'),
      amount: formatMoney(
        summary.userPendingFundObligations && summary.userPendingFundObligations > 0
          ? summary.userPendingFundObligations
          : summary.groupFundBalance || 0,
        locale,
        { isHidden }
      ),
      rawAmount: summary.userPendingFundObligations && summary.userPendingFundObligations > 0
        ? summary.userPendingFundObligations
        : summary.groupFundBalance || 0,
      icon: Users,
      amountColor: 'text-indigo-600 dark:text-indigo-400',
      iconColor: 'text-indigo-500 bg-indigo-50 dark:bg-indigo-950/40',
      accentBorder: 'border-l-[3px] border-l-indigo-500 dark:border-l-indigo-400',
    },
  ];

  return (
    <div className="grid grid-cols-2 gap-3 sm:gap-3.5 h-full content-between">
      {cards.map((card) => {
        const Icon = card.icon;

        return (
          <div
            key={card.id}
            className={`flex flex-col justify-between rounded-xl border border-slate-200/80 dark:border-slate-800/80 bg-white dark:bg-[#0f1422] p-3.5 sm:p-4 transition-all duration-150 shadow-xs hover:border-slate-300 dark:hover:border-slate-700 ${card.accentBorder}`}
          >
            <div className="flex items-center justify-between gap-1 mb-2">
              <span className="text-[11px] font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400 truncate">
                {card.title}
              </span>
              <div className={`flex h-6 w-6 items-center justify-center rounded-lg ${card.iconColor} shrink-0`}>
                <Icon className="h-3.5 w-3.5" />
              </div>
            </div>
            <div className={`text-base sm:text-lg font-bold font-display tracking-tight tabular-nums truncate ${card.amountColor}`}>
              {card.amount}
            </div>
          </div>
        );
      })}

      {/* Integrated Net Monthly Cash Flow strip */}
      <div className="col-span-2 flex items-center justify-between px-3.5 py-2 rounded-xl bg-slate-50 dark:bg-slate-900/60 border border-slate-200/70 dark:border-slate-800/80 text-xs">
        <div className="flex items-center gap-1.5 text-slate-600 dark:text-slate-300 font-semibold">
          <span className={`inline-block w-2 h-2 rounded-full ${
            summary.cashFlowDifference > 0 ? 'bg-emerald-500' : summary.cashFlowDifference < 0 ? 'bg-rose-500' : 'bg-slate-400'
          }`} />
          <span>{locale === 'vi-VN' ? 'Dòng tiền ròng tháng này:' : 'Net monthly cash flow:'}</span>
        </div>
        <div className="flex items-center gap-2">
          <span className={`font-bold font-display tabular-nums text-xs sm:text-sm ${
            summary.cashFlowDifference > 0
              ? 'text-emerald-600 dark:text-emerald-400'
              : summary.cashFlowDifference < 0
              ? 'text-rose-600 dark:text-rose-400'
              : 'text-slate-700 dark:text-slate-300'
          }`}>
            {formatMoney(summary.cashFlowDifference, locale, {
              showSign: summary.cashFlowDifference !== 0,
              forceSign: summary.cashFlowDifference !== 0,
              isHidden,
            })}
          </span>
          {summary.cashFlowDifference === 0 && !isHidden && (
            <span className="text-[11px] text-slate-500 dark:text-slate-400 font-medium">
              ({locale === 'vi-VN' ? 'Thu nhập = Chi tiêu' : 'Balanced'})
            </span>
          )}
        </div>
      </div>
    </div>
  );
};

