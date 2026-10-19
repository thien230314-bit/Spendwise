import React from 'react';
import { Calendar as CalendarIcon } from 'lucide-react';
import { useI18n } from '../../../i18n/I18nContext';
import { useAuth } from '../../../providers/AuthProvider';
import { formatDate, getTimeOfDay } from '../../../utils/date';

export const GreetingHeader: React.FC = () => {
  const { t, locale } = useI18n();
  const { currentUser } = useAuth();
  const timeOfDay = getTimeOfDay();

  const getGreeting = () => {
    // Take short name for conversational feel (e.g. Minh Anh or Admin)
    const rawName = currentUser?.name || 'User';
    const parts = rawName.split(' ');
    const displayName = parts.length > 2 ? parts.slice(-2).join(' ') : rawName;

    if (timeOfDay === 'morning') {
      return t('dashboard.greetingMorning', { name: displayName });
    }
    if (timeOfDay === 'afternoon') {
      return t('dashboard.greetingAfternoon', { name: displayName });
    }
    return t('dashboard.greetingEvening', { name: displayName });
  };

  const currentDateFormatted = formatDate(new Date(), locale);

  return (
    <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-2 pb-1">
      <div>
        <h2 className="text-xl sm:text-2xl font-extrabold tracking-tight font-display text-slate-900 dark:text-slate-50">
          {getGreeting()}
        </h2>
        <p className="text-xs sm:text-sm text-slate-500 dark:text-slate-400 mt-0.5 font-normal">
          {t('dashboard.greetingSubtitle')}
        </p>
      </div>

      <div className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-white dark:bg-[#0f1422] border border-slate-200/70 dark:border-slate-800/80 text-xs font-semibold text-slate-700 dark:text-slate-300 shadow-[0_1px_2px_rgba(0,0,0,0.03)] self-start sm:self-auto">
        <CalendarIcon className="h-3.5 w-3.5 text-slate-400 dark:text-slate-500" />
        <span>{currentDateFormatted}</span>
      </div>
    </div>
  );
};
