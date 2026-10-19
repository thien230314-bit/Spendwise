import React, { useState, useMemo } from 'react';
import { useI18n } from '../../../i18n/I18nContext';
import { useBalanceVisibility } from '../../../providers/BalanceVisibilityProvider';
import { useAuth } from '../../../providers/AuthProvider';
import { Group, GroupExpense } from '../../../types/groups';
import {
  Receipt,
  Plus,
  Calendar,
} from 'lucide-react';
import { cn } from '../../../utils/cn';

interface GroupExpensesTabProps {
  group: Group;
  onOpenAddExpense: () => void;
  onSelectExpense?: (expense: GroupExpense) => void;
}

export const GroupExpensesTab: React.FC<GroupExpensesTabProps> = ({
  group,
  onOpenAddExpense,
  onSelectExpense,
}) => {
  const { t, formatCurrency, locale } = useI18n();
  const { isBalanceHidden, maskValue } = useBalanceVisibility();
  const { currentUser } = useAuth();
  const currentUserId = currentUser?.id;

  const currentMember = group.members.find(
    (m) => currentUserId && m.userId === currentUserId
  );
  const currentMemberUserId = currentMember?.userId || currentUserId;

  const [filter, setFilter] = useState<'all' | 'me' | 'fund'>('all');

  const checkIsPaidByMe = (exp: GroupExpense) => {
    if (exp.paidByType !== 'user') return false;
    return Boolean(
      (currentMemberUserId && exp.paidByUserId === currentMemberUserId) ||
      (currentUserId && exp.paidByUserId === currentUserId)
    );
  };

  const filteredExpenses = useMemo(() => {
    return group.expenses.filter((exp) => {
      if (filter === 'me') {
        return checkIsPaidByMe(exp);
      }
      if (filter === 'fund') return exp.paidByType === 'fund';
      return true;
    });
  }, [group.expenses, filter, currentMemberUserId, currentUserId]);

  return (
    <div className="space-y-6 animate-fadeIn">
      {/* Header & Controls */}
      <div className="flex items-center justify-between flex-wrap gap-3">
        <div>
          <h3 className="text-base font-bold text-slate-900 dark:text-slate-100 flex items-center gap-2">
            <Receipt className="h-5 w-5 text-emerald-600 dark:text-emerald-400" />
            <span>{locale === 'vi-VN' ? 'Danh sách chi phí nhóm' : 'Group Expenses'} ({filteredExpenses.length})</span>
          </h3>
          <p className="text-xs text-slate-500 dark:text-slate-400">
            {locale === 'vi-VN' ? 'Theo dõi các khoản chi tiêu thực tế của nhóm.' : 'Track actual group expenditures.'}
          </p>
        </div>

        <button
          onClick={onOpenAddExpense}
          className="px-4 py-2 rounded-xl bg-emerald-600 hover:bg-emerald-700 text-white text-xs font-semibold shadow-sm transition-all flex items-center gap-1.5"
        >
          <Plus className="h-4 w-4" />
          <span>{t('groups.addExpenseBtn')}</span>
        </button>
      </div>

      {/* Filter Tabs */}
      <div className="flex items-center gap-1.5 bg-slate-100 dark:bg-slate-900 p-1 rounded-xl border border-slate-200/80 dark:border-slate-800 w-fit">
        <button
          onClick={() => setFilter('all')}
          className={cn(
            'px-3 py-1.5 text-xs font-medium rounded-lg transition-all',
            filter === 'all'
              ? 'bg-white dark:bg-slate-800 text-slate-900 dark:text-slate-100 shadow-sm font-semibold'
              : 'text-slate-600 dark:text-slate-400 hover:text-slate-900'
          )}
        >
          {t('groups.filterAll')}
        </button>

        <button
          onClick={() => setFilter('me')}
          className={cn(
            'px-3 py-1.5 text-xs font-medium rounded-lg transition-all',
            filter === 'me'
              ? 'bg-white dark:bg-slate-800 text-slate-900 dark:text-slate-100 shadow-sm font-semibold'
              : 'text-slate-600 dark:text-slate-400 hover:text-slate-900'
          )}
        >
          {t('groups.filterPaidByMe')}
        </button>

        <button
          onClick={() => setFilter('fund')}
          className={cn(
            'px-3 py-1.5 text-xs font-medium rounded-lg transition-all',
            filter === 'fund'
              ? 'bg-white dark:bg-slate-800 text-slate-900 dark:text-slate-100 shadow-sm font-semibold'
              : 'text-slate-600 dark:text-slate-400 hover:text-slate-900'
          )}
        >
          {t('groups.filterPaidByFund')}
        </button>
      </div>

      {/* Expense List */}
      <div className="space-y-3">
        {filteredExpenses.length > 0 ? (
          filteredExpenses.map((exp) => {
            const isPaidByFund = exp.paidByType === 'fund';
            const isPaidByMe = checkIsPaidByMe(exp);

            return (
              <div
                key={exp.id}
                onClick={() => onSelectExpense?.(exp)}
                className="bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-2xl p-4.5 hover:border-slate-300 dark:hover:border-slate-700 transition-all flex flex-col md:flex-row md:items-center justify-between gap-4 cursor-pointer group"
              >
                {/* Left: Info */}
                <div className="space-y-1.5 flex-1 min-w-0">
                  <div className="flex items-center gap-2 flex-wrap">
                    <span className="px-2.5 py-0.5 rounded-full text-[11px] font-semibold bg-slate-100 dark:bg-slate-800 text-slate-700 dark:text-slate-300 border border-slate-200/60 dark:border-slate-700">
                      {exp.categoryName}
                    </span>

                    <span
                      className={cn(
                        'px-2 py-0.5 rounded-full text-[11px] font-semibold border',
                        isPaidByFund
                          ? 'bg-purple-50 text-purple-700 dark:bg-purple-950/70 dark:text-purple-300 border-purple-200/60'
                          : isPaidByMe
                          ? 'bg-emerald-50 text-emerald-700 dark:bg-emerald-950/70 dark:text-emerald-300 border-emerald-200/60'
                          : 'bg-slate-100 text-slate-600 dark:bg-slate-800 dark:text-slate-300 border-slate-200/60'
                      )}
                    >
                      {isPaidByFund
                        ? (locale === 'vi-VN' ? 'Chi từ Quỹ chung' : 'Paid from Fund')
                        : isPaidByMe
                        ? (locale === 'vi-VN' ? 'Bạn đã chi' : 'Paid by you')
                        : `${locale === 'vi-VN' ? 'Người chi:' : 'Paid by:'} ${exp.paidByUserName}`}
                    </span>

                    <span className="text-xs text-slate-400 flex items-center gap-1">
                      <Calendar className="h-3 w-3" />
                      <span>{exp.date}</span>
                    </span>
                  </div>

                  <h4 className="text-sm font-bold text-slate-900 dark:text-slate-100 group-hover:text-emerald-600 dark:group-hover:text-emerald-400 transition-colors">
                    {exp.title}
                  </h4>

                  {exp.description && (
                    <p className="text-xs text-slate-500 dark:text-slate-400 line-clamp-2">
                      {exp.description}
                    </p>
                  )}
                </div>

                {/* Right: Amount */}
                <div className="flex md:flex-col items-center md:items-end justify-between gap-1 shrink-0 pt-2 md:pt-0 border-t md:border-t-0 border-slate-100 dark:border-slate-800">
                  <span className="text-base font-bold text-slate-900 dark:text-slate-100 font-mono">
                    {isBalanceHidden ? maskValue(exp.amount) : formatCurrency(exp.amount)}
                  </span>
                </div>
              </div>
            );
          })
        ) : (
          <div className="p-8 text-center text-xs text-slate-400 bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-2xl">
            {locale === 'vi-VN' ? 'Không có khoản chi nào phù hợp.' : 'No matching expenses.'}
          </div>
        )}
      </div>
    </div>
  );
};
