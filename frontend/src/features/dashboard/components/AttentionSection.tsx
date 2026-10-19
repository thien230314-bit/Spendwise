import React from 'react';
import { NavLink } from 'react-router-dom';
import { AlertCircle, ArrowUpRight, Calendar, ChevronRight, ShieldAlert } from 'lucide-react';
import { useI18n } from '../../../i18n/I18nContext';
import { AttentionItem } from '../../../types';
import { Skeleton } from '../../../components/ui/Skeleton';
import { getLocalizedWalletName, getLocalizedActionLabel } from '../../../utils/localizationHelpers';

interface AttentionSectionProps {
  items: AttentionItem[];
  isLoading?: boolean;
}

export const AttentionSection: React.FC<AttentionSectionProps> = ({ items, isLoading = false }) => {
  const { t, locale } = useI18n();

  if (isLoading) {
    return (
      <div className="bg-amber-50/70 dark:bg-amber-950/20 rounded-3xl border border-amber-200/60 dark:border-amber-900/40 p-5">
        <Skeleton className="h-5 w-32 mb-4" />
        <div className="space-y-3">
          <Skeleton className="h-14 w-full rounded-2xl" />
          <Skeleton className="h-14 w-full rounded-2xl" />
        </div>
      </div>
    );
  }

  if (!items || items.length === 0) return null;

  const getIcon = (type: AttentionItem['iconType']) => {
    switch (type) {
      case 'budget':
        return (
          <div className="flex h-8 w-8 items-center justify-center rounded-xl bg-amber-100 dark:bg-amber-950/60 text-amber-600 dark:text-amber-400 shrink-0">
            <AlertCircle className="h-4 w-4" />
          </div>
        );
      case 'payment_request':
        return (
          <div className="flex h-8 w-8 items-center justify-center rounded-xl bg-sky-100 dark:bg-sky-950/60 text-sky-600 dark:text-sky-400 shrink-0">
            <ArrowUpRight className="h-4 w-4" />
          </div>
        );
      case 'subscription':
        return (
          <div className="flex h-8 w-8 items-center justify-center rounded-xl bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-400 shrink-0">
            <Calendar className="h-4 w-4" />
          </div>
        );
      default:
        return (
          <div className="flex h-8 w-8 items-center justify-center rounded-xl bg-slate-100 dark:bg-slate-800 text-slate-500 shrink-0">
            <ShieldAlert className="h-4 w-4" />
          </div>
        );
    }
  };

  return (
    <div className="bg-amber-50/60 dark:bg-amber-950/20 rounded-2xl border border-amber-200/60 dark:border-amber-900/40 p-5 shadow-[0_1px_3px_rgba(0,0,0,0.02),0_4px_12px_-2px_rgba(0,0,0,0.02)]">
      <div className="flex items-center justify-between mb-3.5">
        <h3 className="text-sm font-bold text-amber-950 dark:text-amber-200 flex items-center gap-2 font-display">
          <span>{t('dashboard.attentionTitle')}</span>
          <span className="flex h-5 w-5 items-center justify-center rounded-full bg-amber-200/80 text-amber-900 dark:bg-amber-900/80 dark:text-amber-200 text-[10px] font-bold">
            {items.length}
          </span>
        </h3>
      </div>

      <div className="space-y-2.5">
        {items.map((item) => {
          const actionText = item.actionLabelKey
            ? getLocalizedActionLabel(item.actionLabelKey, t)
            : locale === 'en-US' && item.actionLabelEn
            ? item.actionLabelEn
            : getLocalizedActionLabel(item.actionLabel, t);

          const secondaryActionText = item.secondaryActionLabelKey
            ? getLocalizedActionLabel(item.secondaryActionLabelKey, t)
            : locale === 'en-US' && item.secondaryActionLabelEn
            ? item.secondaryActionLabelEn
            : item.secondaryActionLabel
            ? getLocalizedActionLabel(item.secondaryActionLabel, t)
            : null;

          let displayMessage = '';
          if (item.messageKey) {
            const params: Record<string, string | number> = { ...(item.messageParams || {}) };
            if (params.walletName && typeof params.walletName === 'string') {
              params.walletName = getLocalizedWalletName(undefined, params.walletName, locale);
            }
            if (locale === 'en-US' && params.balance && typeof params.balance === 'string' && params.balance.includes('₫')) {
              params.balance = params.balance.replace(/([\d\.,]+)\s*₫/, '₫$1');
            }
            displayMessage = t(item.messageKey, params);
          } else {
            displayMessage = locale === 'en-US' && item.rawMessageEn ? item.rawMessageEn : item.rawMessage;
          }

          return (
            <div
              key={item.id}
              className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 p-3.5 rounded-xl border border-amber-200/60 dark:border-amber-900/50 bg-white dark:bg-[#0f1422] shadow-2xs transition-all hover:border-amber-300 dark:hover:border-amber-800"
            >
              <div className="flex items-center gap-3 min-w-0">
                {getIcon(item.iconType)}
                <div className="min-w-0">
                  <p className="text-xs sm:text-sm font-medium text-slate-800 dark:text-slate-200 leading-snug">
                    {displayMessage}
                  </p>
                </div>
              </div>

              {/* Action buttons */}
              <div className="flex items-center gap-2 shrink-0 self-end sm:self-center">
                {secondaryActionText && item.secondaryActionRoute && (
                  <NavLink
                    to={item.secondaryActionRoute}
                    className="text-xs font-semibold text-slate-500 hover:text-slate-800 dark:text-slate-400 dark:hover:text-slate-200 px-2 py-1 transition-colors"
                  >
                    {secondaryActionText}
                  </NavLink>
                )}
                <NavLink
                  to={item.actionRoute}
                  className="inline-flex items-center gap-1 h-7 px-3 bg-slate-900 hover:bg-slate-800 dark:bg-slate-800 dark:hover:bg-slate-700 text-white rounded-lg text-xs font-semibold shadow-2xs transition-colors"
                >
                  <span>{actionText}</span>
                  <ChevronRight className="h-3 w-3" />
                </NavLink>
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
};

