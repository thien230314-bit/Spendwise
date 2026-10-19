import React from 'react';
import { useNavigate } from 'react-router-dom';
import { Clock, ArrowRight, ShieldAlert } from 'lucide-react';
import { useAuth } from '../../providers/AuthProvider';
import { useI18n } from '../../i18n/I18nContext';

export const SessionExpiredModal: React.FC = () => {
  const { isSessionExpired, dismissSessionExpired } = useAuth();
  const { t } = useI18n();
  const navigate = useNavigate();

  if (!isSessionExpired) return null;

  const handleReLogin = () => {
    dismissSessionExpired();
    navigate('/login', { replace: true });
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-xs animate-in fade-in-0">
      <div className="w-full max-w-md rounded-3xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 p-6 sm:p-8 shadow-2xl space-y-5 animate-in zoom-in-95">
        <div className="flex h-12 w-12 items-center justify-center rounded-2xl bg-amber-100 dark:bg-amber-950/60 text-amber-600 dark:text-amber-400">
          <Clock className="h-6 w-6" />
        </div>

        <div className="space-y-1.5">
          <h3 className="text-lg font-bold text-slate-900 dark:text-white">
            {t('auth.sessionExpiredTitle')}
          </h3>
          <p className="text-xs sm:text-sm text-slate-500 dark:text-slate-400 leading-relaxed">
            {t('auth.sessionExpiredDesc')}
          </p>
        </div>

        <div className="p-3 rounded-2xl bg-slate-50 dark:bg-slate-800/50 border border-slate-100 dark:border-slate-800 text-xs text-slate-600 dark:text-slate-300 flex items-center gap-2">
          <ShieldAlert className="h-4 w-4 text-emerald-600 dark:text-emerald-400 shrink-0" />
          <span>{t('auth.brandHighlight3')}</span>
        </div>

        <button
          type="button"
          onClick={handleReLogin}
          className="w-full flex items-center justify-center gap-2 py-3 px-4 rounded-xl bg-emerald-600 hover:bg-emerald-700 text-white text-sm font-semibold shadow-sm transition-colors cursor-pointer"
        >
          <span>{t('auth.reLoginBtn')}</span>
          <ArrowRight className="h-4 w-4" />
        </button>
      </div>
    </div>
  );
};
