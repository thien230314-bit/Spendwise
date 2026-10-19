import React, { useState, useEffect } from 'react';
import {
  ShieldCheck,
  RefreshCw,
  LogOut,
  Construction,
  Megaphone,
  X,
  Bell,
  Globe,
  Sun,
  Moon,
  Laptop,
} from 'lucide-react';
import { useI18n } from '../../i18n/I18nContext';
import { useTheme } from '../../providers/ThemeProvider';
import { useAuth } from '../../providers/AuthProvider';
import {
  systemOperationsService,
  SystemMaintenanceState,
  EmergencyAnnouncement,
} from '../../services/systemOperationsService';
import { ActionBadge } from '../ui/ActionBadge';
import { APP_META } from '../../config/appMeta';
import { cn } from '../../utils/cn';

interface MaintenanceScreenProps {
  maintenanceState?: SystemMaintenanceState;
}

export const MaintenanceScreen: React.FC<MaintenanceScreenProps> = ({ maintenanceState }) => {
  const { locale, setLocale, t } = useI18n();
  const { theme, setTheme } = useTheme();
  const { logout } = useAuth();
  const isVi = locale === 'vi-VN';

  const [activeAnnouncements, setActiveAnnouncements] = useState<EmergencyAnnouncement[]>(() => {
    return systemOperationsService.getActiveAnnouncements();
  });

  const [isAnnouncementModalOpen, setIsAnnouncementModalOpen] = useState(false);

  useEffect(() => {
    const update = () => {
      setActiveAnnouncements(systemOperationsService.getActiveAnnouncements());
    };
    update();
    const unsubscribe = systemOperationsService.subscribeAnnouncements(update);
    return () => unsubscribe();
  }, []);

  // Keyboard escape handler for announcement modal
  useEffect(() => {
    if (!isAnnouncementModalOpen) return;
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        setIsAnnouncementModalOpen(false);
      }
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [isAnnouncementModalOpen]);

  const [isChecking, setIsChecking] = useState(false);

  const handleRefresh = async () => {
    setIsChecking(true);
    try {
      const fresh = await systemOperationsService.refreshState();
      if (!fresh.maintenance.enabled) {
        // Maintenance has ended! The AppShell subscriber will naturally re-render
        return;
      }
    } catch {
      // Fallback
    } finally {
      setIsChecking(false);
    }
  };

  const formattedTime = maintenanceState?.changedAt
    ? new Date(maintenanceState.changedAt).toLocaleString(isVi ? 'vi-VN' : 'en-US', {
        hour: '2-digit',
        minute: '2-digit',
        day: '2-digit',
        month: '2-digit',
        year: 'numeric',
      })
    : null;

  return (
    <div
      id="system-maintenance-screen"
      className="min-h-screen w-full flex items-center justify-center p-4 sm:p-6 bg-slate-50 dark:bg-[#0B1120] text-slate-900 dark:text-slate-100 transition-colors"
    >
      <div className="w-full max-w-lg bg-white dark:bg-[#151D2F] rounded-2xl border border-slate-200 dark:border-slate-800 shadow-xl p-6 sm:p-8 space-y-6 text-center">
        {/* Brand header & Controls */}
        <div className="flex items-center justify-between pb-3 border-b border-slate-100 dark:border-slate-800/80">
          <div className="flex items-center gap-2">
            <div className="h-7 w-7 rounded-lg bg-emerald-600 text-white font-bold flex items-center justify-center text-xs shadow-xs">
              S
            </div>
            <span className="text-sm font-bold tracking-tight text-slate-900 dark:text-white">
              {APP_META.name}
            </span>
          </div>

          {/* Maintenance Screen Language & Theme Controls */}
          <div className="flex items-center gap-2">
            {/* Language Toggle */}
            <button
              type="button"
              id="btn-maintenance-lang"
              onClick={() => setLocale(locale === 'vi-VN' ? 'en-US' : 'vi-VN')}
              className="flex h-7 items-center gap-1 rounded-lg border border-slate-200 dark:border-slate-700 bg-slate-100 dark:bg-slate-800 px-2 text-[11px] font-semibold text-slate-700 dark:text-slate-200 hover:bg-slate-200 dark:hover:bg-slate-700 transition cursor-pointer"
              title={isVi ? 'Chuyển sang tiếng Anh' : 'Switch to Vietnamese'}
              aria-label={isVi ? 'Chuyển ngôn ngữ' : 'Switch language'}
            >
              <Globe className="h-3 w-3 text-slate-400" />
              <span>{isVi ? 'VIE' : 'ENG'}</span>
            </button>

            {/* 3-state Theme Selector */}
            <div
              id="maintenance-theme-selector"
              className="flex items-center p-0.5 rounded-lg bg-slate-100 dark:bg-slate-800 border border-slate-200/60 dark:border-slate-700/60"
            >
              <button
                type="button"
                id="btn-maintenance-theme-light"
                onClick={() => setTheme('light')}
                className={cn(
                  'flex h-6 w-6 items-center justify-center rounded transition-all cursor-pointer',
                  theme === 'light'
                    ? 'bg-white dark:bg-slate-900 text-amber-500 shadow-xs font-bold'
                    : 'text-slate-400 hover:text-slate-700 dark:hover:text-slate-200'
                )}
                title={t('common.light') || 'Light'}
                aria-label={isVi ? 'Chế độ sáng' : 'Light mode'}
              >
                <Sun className="h-3 w-3" />
              </button>

              <button
                type="button"
                id="btn-maintenance-theme-dark"
                onClick={() => setTheme('dark')}
                className={cn(
                  'flex h-6 w-6 items-center justify-center rounded transition-all cursor-pointer',
                  theme === 'dark'
                    ? 'bg-white dark:bg-slate-900 text-sky-400 shadow-xs font-bold'
                    : 'text-slate-400 hover:text-slate-700 dark:hover:text-slate-200'
                )}
                title={t('common.dark') || 'Dark'}
                aria-label={isVi ? 'Chế độ tối' : 'Dark mode'}
              >
                <Moon className="h-3 w-3" />
              </button>

              <button
                type="button"
                id="btn-maintenance-theme-system"
                onClick={() => setTheme('system')}
                className={cn(
                  'flex h-6 w-6 items-center justify-center rounded transition-all cursor-pointer',
                  theme === 'system'
                    ? 'bg-white dark:bg-slate-900 text-emerald-500 shadow-xs font-bold'
                    : 'text-slate-400 hover:text-slate-700 dark:hover:text-slate-200'
                )}
                title={t('common.system') || 'System'}
                aria-label={isVi ? 'Theo hệ thống' : 'System mode'}
              >
                <Laptop className="h-3 w-3" />
              </button>
            </div>
          </div>
        </div>

        {/* Maintenance Icon */}
        <div className="inline-flex h-16 w-16 items-center justify-center rounded-2xl bg-amber-50 dark:bg-amber-950/40 border border-amber-200 dark:border-amber-800/60 text-amber-600 dark:text-amber-400 mx-auto shadow-xs">
          <Construction className="h-8 w-8 animate-pulse" />
        </div>

        {/* Title & Description */}
        <div className="space-y-2.5">
          <div className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-xs font-semibold bg-amber-100 dark:bg-amber-950/70 text-amber-800 dark:text-amber-300 border border-amber-200/80 dark:border-amber-800">
            <span className="h-2 w-2 rounded-full bg-amber-500 animate-ping" />
            <span>{t('maintenance.modeActive')}</span>
          </div>
          <h1 className="text-xl sm:text-2xl font-bold tracking-tight text-slate-900 dark:text-white">
            {t('maintenance.title')}
          </h1>
          <p className="text-xs sm:text-sm text-slate-600 dark:text-slate-400 leading-relaxed max-w-md mx-auto">
            {t('maintenance.description')}
          </p>
        </div>

        {/* Security & Data Guarantee Box */}
        <div className="p-3.5 rounded-xl bg-slate-50 dark:bg-slate-900/60 border border-slate-200 dark:border-slate-800/80 text-left space-y-2 text-xs text-slate-600 dark:text-slate-400">
          <div className="flex items-center gap-2 text-emerald-600 dark:text-emerald-400 font-semibold">
            <ShieldCheck className="h-4 w-4 shrink-0" />
            <span>{t('maintenance.securityGuarantee')}</span>
          </div>
          {formattedTime && (
            <p className="text-[11px] text-slate-500 dark:text-slate-500 font-mono">
              {t('maintenance.lastUpdated')} {formattedTime}
            </p>
          )}
        </div>

        {/* System Announcements Action Button */}
        <div>
          <button
            type="button"
            id="btn-maintenance-announcements"
            onClick={() => setIsAnnouncementModalOpen(true)}
            className="w-full flex items-center justify-center gap-2.5 px-4 py-2.5 rounded-xl border border-amber-200/80 dark:border-amber-800/60 bg-amber-50/70 dark:bg-amber-950/30 hover:bg-amber-100/70 dark:hover:bg-amber-950/50 text-amber-900 dark:text-amber-200 font-semibold text-xs shadow-xs transition-colors cursor-pointer"
          >
            <Megaphone className="h-4 w-4 text-amber-600 dark:text-amber-400 shrink-0" />
            <span>{t('common.systemAnnouncements')}</span>
            {activeAnnouncements.length > 0 && (
              <ActionBadge count={activeAnnouncements.length} />
            )}
          </button>
        </div>

        {/* Secondary Actions: Refresh & Logout */}
        <div className="flex flex-col sm:flex-row items-center justify-center gap-3 pt-1">
          <button
            type="button"
            id="btn-maintenance-refresh"
            onClick={handleRefresh}
            disabled={isChecking}
            className="w-full sm:w-auto flex-1 flex items-center justify-center gap-2 px-5 py-2.5 rounded-xl bg-emerald-600 hover:bg-emerald-700 text-white font-semibold text-xs shadow-xs transition-colors cursor-pointer disabled:opacity-50"
          >
            <RefreshCw className={`h-3.5 w-3.5 ${isChecking ? 'animate-spin' : ''}`} />
            <span>{isChecking ? (isVi ? 'Đang kiểm tra...' : 'Checking...') : t('maintenance.checkStatus')}</span>
          </button>
          <button
            type="button"
            id="btn-maintenance-logout"
            onClick={() => logout()}
            className="w-full sm:w-auto flex items-center justify-center gap-2 px-4 py-2.5 rounded-xl border border-slate-200 dark:border-slate-700 hover:bg-slate-100 dark:hover:bg-slate-800 text-slate-700 dark:text-slate-300 font-medium text-xs transition-colors cursor-pointer"
          >
            <LogOut className="h-3.5 w-3.5" />
            <span>{t('common.logout')}</span>
          </button>
        </div>

        {/* Centralized App Version Footer */}
        <div className="pt-2 border-t border-slate-100 dark:border-slate-800/80">
          <span className="text-[11px] font-medium text-slate-400 dark:text-slate-500">
            {APP_META.name} v{APP_META.version}
          </span>
        </div>
      </div>

      {/* System Announcements Modal */}
      {isAnnouncementModalOpen && (
        <div
          role="dialog"
          aria-modal="true"
          aria-labelledby="modal-announcement-title"
          className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/60 backdrop-blur-xs animate-in fade-in duration-150"
          onClick={(e) => {
            if (e.target === e.currentTarget) setIsAnnouncementModalOpen(false);
          }}
        >
          <div className="w-full max-w-lg bg-white dark:bg-[#151D2F] rounded-2xl border border-slate-200 dark:border-slate-800 shadow-2xl p-5 sm:p-6 space-y-4 max-h-[85vh] flex flex-col text-left">
            {/* Modal Header */}
            <div className="flex items-center justify-between border-b border-slate-100 dark:border-slate-800/80 pb-3">
              <div className="flex items-center gap-2.5">
                <div className="p-2 rounded-xl bg-amber-50 dark:bg-amber-950/50 text-amber-600 dark:text-amber-400 border border-amber-200/60 dark:border-amber-800/60">
                  <Bell className="h-4 w-4" />
                </div>
                <div>
                  <h3
                    id="modal-announcement-title"
                    className="text-sm font-bold uppercase tracking-wider text-slate-900 dark:text-white"
                  >
                    {t('common.systemAnnouncements')}
                  </h3>
                  <p className="text-[11px] text-slate-500 dark:text-slate-400">
                    {activeAnnouncements.length > 0
                      ? t('maintenance.activeCount', { count: activeAnnouncements.length })
                      : t('maintenance.adminUpdatesSubtitle')}
                  </p>
                </div>
              </div>
              <button
                type="button"
                id="btn-close-announcement-icon"
                onClick={() => setIsAnnouncementModalOpen(false)}
                className="p-1.5 rounded-lg text-slate-400 hover:text-slate-700 dark:hover:text-slate-200 hover:bg-slate-100 dark:hover:bg-slate-800 transition-colors cursor-pointer"
                aria-label={t('common.close')}
              >
                <X className="h-4 w-4" />
              </button>
            </div>

            {/* Modal Body: Active Announcements List */}
            <div className="overflow-y-auto space-y-3 flex-1 pr-1">
              {activeAnnouncements.length === 0 ? (
                <div className="p-6 text-center space-y-2 rounded-xl bg-slate-50 dark:bg-slate-900/50 border border-slate-100 dark:border-slate-800">
                  <Megaphone className="h-8 w-8 text-slate-400 dark:text-slate-500 mx-auto stroke-[1.5]" />
                  <p className="text-xs font-medium text-slate-600 dark:text-slate-300">
                    {t('common.noActiveAnnouncements')}
                  </p>
                </div>
              ) : (
                activeAnnouncements.map((announcement) => {
                  const publishedTime = announcement.createdAt
                    ? new Date(announcement.createdAt).toLocaleString(
                        isVi ? 'vi-VN' : 'en-US',
                        {
                          hour: '2-digit',
                          minute: '2-digit',
                          day: '2-digit',
                          month: '2-digit',
                          year: 'numeric',
                        }
                      )
                    : null;

                  return (
                    <div
                      key={announcement.id}
                      className="p-4 rounded-xl bg-amber-50/50 dark:bg-amber-950/20 border border-amber-200/70 dark:border-amber-800/50 space-y-2.5"
                    >
                      <div className="flex items-start justify-between gap-2">
                        {/* Admin-authored title preserved as written */}
                        <h4 className="text-sm font-bold text-slate-900 dark:text-white leading-snug">
                          {announcement.title}
                        </h4>
                        <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[10px] font-semibold bg-amber-100 text-amber-800 dark:bg-amber-950/60 dark:text-amber-300 border border-amber-200 dark:border-amber-800 shrink-0">
                          <span className="h-1.5 w-1.5 rounded-full bg-amber-500 animate-pulse" />
                          <span>{t('maintenance.active')}</span>
                        </span>
                      </div>

                      {/* Admin-authored description preserved as written */}
                      <p className="text-xs text-slate-700 dark:text-slate-300 leading-relaxed whitespace-pre-wrap">
                        {announcement.description}
                      </p>

                      {publishedTime && (
                        <div className="pt-1 border-t border-amber-200/50 dark:border-amber-900/40 text-[11px] text-slate-500 dark:text-slate-400 flex items-center justify-between">
                          <span>
                            {t('common.publishedAt')}: {publishedTime}
                          </span>
                          {announcement.createdBy && (
                            <span className="text-[10px] text-slate-400">
                              {announcement.createdBy}
                            </span>
                          )}
                        </div>
                      )}
                    </div>
                  );
                })
              )}
            </div>

            {/* Modal Footer */}
            <div className="pt-2 border-t border-slate-100 dark:border-slate-800/80 flex justify-end">
              <button
                type="button"
                id="btn-close-announcement-modal"
                onClick={() => setIsAnnouncementModalOpen(false)}
                className="px-4 py-2 rounded-xl bg-slate-100 hover:bg-slate-200 dark:bg-slate-800 dark:hover:bg-slate-700 text-slate-700 dark:text-slate-200 text-xs font-semibold transition-colors cursor-pointer"
              >
                {t('common.close')}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
