import React from 'react';
import { NavLink } from 'react-router-dom';
import {
  Briefcase,
  Utensils,
  Car,
  Tv,
  ArrowDownLeft,
  ArrowUpRight,
} from 'lucide-react';
import { useI18n } from '../../../i18n/I18nContext';
import { useBalanceVisibility } from '../../../providers/BalanceVisibilityProvider';
import { formatMoney } from '../../../utils/money';
import { formatTransactionTime } from '../../../utils/date';
import { CategoryItem, Transaction } from '../../../types';
import { Skeleton } from '../../../components/ui/Skeleton';
import { getLocalizedTxTitle, getLocalizedCategoryName } from '../../../utils/localizationHelpers';
import { useFinancial } from '../../../providers/FinancialProvider';

interface RecentTransactionsListProps {
  transactions: Transaction[];
  categories?: CategoryItem[];
  isLoading?: boolean;
}

export const RecentTransactionsList: React.FC<RecentTransactionsListProps> = ({
  transactions,
  categories: propCategories,
  isLoading = false,
}) => {
  const { t, locale } = useI18n();
  const { isHidden } = useBalanceVisibility();
  const { categories: contextCategories } = useFinancial();
  const categories = propCategories || contextCategories;

  const getTransactionBadge = (tx: Transaction) => {
    if (tx.title.includes('Lương') || tx.category.includes('salary')) {
      return {
        bg: 'bg-emerald-100 text-emerald-600 dark:bg-emerald-950/60 dark:text-emerald-400',
        icon: <Briefcase className="h-4 w-4" />,
      };
    }
    if (tx.title.includes('Cơm') || tx.title.includes('Cà phê') || tx.category.includes('food')) {
      return {
        bg: 'bg-amber-100 text-amber-600 dark:bg-amber-950/60 dark:text-amber-400',
        icon: <Utensils className="h-4 w-4" />,
      };
    }
    if (tx.title.includes('Grab') || tx.category.includes('transport')) {
      return {
        bg: 'bg-blue-100 text-blue-600 dark:bg-blue-950/60 dark:text-blue-400',
        icon: <Car className="h-4 w-4" />,
      };
    }
    if (tx.type === 'transfer_in' || tx.category.includes('p2p')) {
      return {
        bg: 'bg-teal-100 text-teal-600 dark:bg-teal-950/60 dark:text-teal-400',
        icon: <ArrowDownLeft className="h-4 w-4" />,
      };
    }
    if (tx.title.includes('Netflix') || tx.category.includes('entertainment')) {
      return {
        bg: 'bg-rose-100 text-rose-600 dark:bg-rose-950/60 dark:text-rose-400',
        icon: <Tv className="h-4 w-4" />,
      };
    }
    return {
      bg: 'bg-slate-100 text-slate-600 dark:bg-slate-800 dark:text-slate-400',
      icon: <ArrowUpRight className="h-4 w-4" />,
    };
  };

  const getCategoryLabel = (categoryKey: string, defaultTitle: string) => {
    if (categoryKey.startsWith('categories.')) {
      return t(categoryKey);
    }
    return defaultTitle;
  };

  if (isLoading) {
    return (
      <div className="bg-white dark:bg-slate-900 rounded-3xl p-6 border border-slate-100 dark:border-slate-800 shadow-sm">
        <div className="flex justify-between items-center mb-6">
          <Skeleton className="h-5 w-36" />
          <Skeleton className="h-4 w-16" />
        </div>
        <div className="space-y-4">
          {[1, 2, 3, 4, 5].map((i) => (
            <div key={i} className="flex items-center justify-between py-1">
              <div className="flex items-center gap-3">
                <Skeleton className="h-10 w-10 rounded-full" />
                <div>
                  <Skeleton className="h-4 w-32 mb-1.5" />
                  <Skeleton className="h-3 w-20" />
                </div>
              </div>
              <Skeleton className="h-4 w-20" />
            </div>
          ))}
        </div>
      </div>
    );
  }

  return (
    <div className="bg-white dark:bg-[#0f1422] rounded-2xl p-6 border border-slate-200/80 dark:border-slate-800/80 shadow-xs">
      {/* Header */}
      <div className="flex items-center justify-between mb-4">
        <h3 className="text-sm sm:text-base font-bold text-slate-900 dark:text-slate-100">
          {t('dashboard.recentTransactionsTitle')}
        </h3>
        <NavLink
          to="/transactions"
          className="text-xs font-semibold text-slate-500 hover:text-slate-800 dark:text-slate-400 dark:hover:text-slate-200 transition-colors"
        >
          {t('common.viewAll')}
        </NavLink>
      </div>

      {/* Rows or Empty State */}
      {transactions.length === 0 ? (
        <div className="py-8 flex flex-col items-center justify-center text-center">
          <p className="text-sm font-medium text-slate-500 dark:text-slate-400">
            {locale === 'vi-VN' ? 'Chưa có giao dịch nào.' : 'No transactions yet.'}
          </p>
          <p className="text-xs text-slate-400 dark:text-slate-500 mt-1">
            {locale === 'vi-VN'
              ? 'Hãy thêm giao dịch đầu tiên để bắt đầu theo dõi tài chính.'
              : 'Add your first transaction to start tracking your finances.'}
          </p>
        </div>
      ) : (
        <div className="space-y-2.5">
          {transactions.map((tx) => {
            const isPositive = tx.amount > 0;
            const formattedAmount = isHidden
              ? '••••••••'
              : formatMoney(tx.amount, locale, {
                  showSign: true,
                  forceSign: true,
                });
            const badge = getTransactionBadge(tx);

            return (
              <div
                key={tx.id}
                className="flex items-center justify-between py-1.5 hover:bg-slate-50 dark:hover:bg-slate-800/50 px-2.5 -mx-2.5 rounded-xl transition-colors"
              >
                {/* Left: Icon & Description */}
                <div className="flex items-center gap-3 min-w-0">
                  <div
                    className={`w-9 h-9 rounded-lg flex items-center justify-center font-bold text-sm shrink-0 ${badge.bg}`}
                  >
                    {badge.icon}
                  </div>
                  <div className="min-w-0">
                    <p className="text-sm font-semibold text-slate-900 dark:text-slate-100 truncate leading-tight">
                      {getLocalizedTxTitle(tx, locale)}
                    </p>
                    <p className="text-xs text-slate-400 dark:text-slate-500 mt-0.5 truncate">
                      {formatTransactionTime(tx.date, locale)} • {getLocalizedCategoryName(tx.category, t, categories)}
                    </p>
                  </div>
                </div>

                {/* Right: Amount */}
                <div className="text-right shrink-0 pl-3">
                  <span
                    className={`text-sm font-bold tabular-nums font-display ${
                      isPositive
                        ? 'text-emerald-600 dark:text-emerald-400'
                        : 'text-slate-900 dark:text-slate-100'
                    }`}
                  >
                    {formattedAmount}
                  </span>
                </div>
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
};

