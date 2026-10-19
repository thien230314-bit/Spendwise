import React from 'react';
import { useNavigate } from 'react-router-dom';
import { Plus, Minus, ArrowUpRight, Users } from 'lucide-react';
import { useI18n } from '../../../i18n/I18nContext';

export const QuickActions: React.FC = () => {
  const { t } = useI18n();
  const navigate = useNavigate();

  const actions = [
    {
      id: 'income',
      label: t('quickActions.income'),
      icon: Plus,
      onClick: () => navigate('/transactions?tab=income'),
      colorClass:
        'bg-emerald-50 text-emerald-700 hover:bg-emerald-100 dark:bg-emerald-950/60 dark:text-emerald-300 dark:hover:bg-emerald-900/60 border-emerald-200/80 dark:border-emerald-800/60',
      iconClass: 'text-emerald-600 dark:text-emerald-400',
    },
    {
      id: 'expense',
      label: t('quickActions.expense'),
      icon: Minus,
      onClick: () => navigate('/transactions?tab=expense'),
      colorClass:
        'bg-rose-50 text-rose-700 hover:bg-rose-100 dark:bg-rose-950/60 dark:text-rose-300 dark:hover:bg-rose-900/60 border-rose-200/80 dark:border-rose-800/60',
      iconClass: 'text-rose-600 dark:text-rose-400',
    },
    {
      id: 'transfer',
      label: t('quickActions.transfer'),
      icon: ArrowUpRight,
      onClick: () => navigate('/transfer'),
      colorClass:
        'bg-stone-50 text-stone-700 hover:bg-stone-100 dark:bg-slate-800 dark:text-slate-200 dark:hover:bg-slate-700/80 border-stone-200/80 dark:border-slate-700',
      iconClass: 'text-teal-600 dark:text-teal-400',
    },
    {
      id: 'groups',
      label: t('quickActions.groups'),
      icon: Users,
      onClick: () => navigate('/groups'),
      colorClass:
        'bg-stone-50 text-stone-700 hover:bg-stone-100 dark:bg-slate-800 dark:text-slate-200 dark:hover:bg-slate-700/80 border-stone-200/80 dark:border-slate-700',
      iconClass: 'text-indigo-600 dark:text-indigo-400',
    },
  ];

  return (
    <div className="grid grid-cols-2 sm:grid-cols-4 gap-2 sm:gap-3">
      {actions.map((action) => {
        const Icon = action.icon;
        return (
          <button
            key={action.id}
            type="button"
            onClick={action.onClick}
            className={`group flex flex-col items-center justify-center p-2.5 sm:p-3 rounded-xl border transition-all duration-150 active:scale-95 cursor-pointer shadow-2xs ${action.colorClass}`}
          >
            <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-white/80 dark:bg-slate-900/80 shadow-2xs mb-1.5 group-hover:scale-105 transition-transform">
              <Icon className={`h-4 w-4 ${action.iconClass}`} />
            </div>
            <span className="text-xs font-semibold tracking-tight truncate max-w-full">
              {action.label}
            </span>
          </button>
        );
      })}
    </div>
  );
};
