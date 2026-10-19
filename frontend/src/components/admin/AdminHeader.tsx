import React, { useState, useEffect } from 'react';
import { useLocation } from 'react-router-dom';
import {
  Bell,
  Search,
  Sun,
  Moon,
  Laptop,
  Globe,
  Shield,
  ShieldCheck,
  Server,
} from 'lucide-react';
import { useI18n } from '../../i18n/I18nContext';
import { useTheme } from '../../providers/ThemeProvider';
import { useAuth } from '../../providers/AuthProvider';
import { useNotifications } from '../../providers/NotificationProvider';
import { UserDropdown } from '../layout/UserDropdown';
import { NotificationsDropdown } from '../layout/NotificationsDropdown';
import { Tooltip } from '../ui/Tooltip';
import { cn } from '../../utils/cn';

interface AdminHeaderProps {
  pageTitle?: string;
  pageSubtitle?: string;
}

export const AdminHeader: React.FC<AdminHeaderProps> = ({ pageTitle, pageSubtitle }) => {
  const { locale, setLocale, t } = useI18n();
  const { theme, setTheme } = useTheme();
  const { currentUser } = useAuth();
  const { unreadCount } = useNotifications();
  const location = useLocation();

  const [isNotifOpen, setIsNotifOpen] = useState(false);
  const [isUserMenuOpen, setIsUserMenuOpen] = useState(false);

  const getAdminRouteTitle = () => {
    if (pageTitle) return pageTitle;
    const path = location.pathname;
    if (path === '/admin' || path === '/admin/overview') return t('adminOverview.title');
    if (path.startsWith('/admin/users')) return t('adminUsers.title');
    if (path.startsWith('/admin/transactions')) return t('adminTransactions.title');
    if (path.startsWith('/admin/ledger')) return t('adminLedger.title');
    if (path.startsWith('/admin/risk')) return t('adminRisk.title');
    if (path.startsWith('/admin/disputes')) return t('adminDisputes.title');
    if (path.startsWith('/admin/support')) return t('adminSupport.title');
    if (path.startsWith('/admin/audit')) return t('adminAudit.title');
    if (path.startsWith('/admin/banking') || path.startsWith('/admin/sandbox')) return t('adminBanking.title');
    if (path.startsWith('/admin/reconciliation')) return t('adminReconciliation.title');
    if (path.startsWith('/admin/health')) return t('adminHealth.title');
    if (path.startsWith('/admin/configuration')) return t('adminConfig.title');
    if (path.startsWith('/admin/feature-flags')) return t('adminFeatureFlags.title');
    return t('adminNav.consoleTitle');
  };

  const getAdminRouteSubtitle = () => {
    if (pageSubtitle) return pageSubtitle;
    const path = location.pathname;
    if (path === '/admin' || path === '/admin/overview') return t('adminOverview.subtitle');
    if (path.startsWith('/admin/users')) return t('adminUsers.subtitle');
    if (path.startsWith('/admin/transactions')) return t('adminTransactions.subtitle');
    if (path.startsWith('/admin/ledger')) return t('adminLedger.subtitle');
    if (path.startsWith('/admin/risk')) return t('adminRisk.subtitle');
    if (path.startsWith('/admin/disputes')) return t('adminDisputes.subtitle');
    if (path.startsWith('/admin/support')) return t('adminSupport.subtitle');
    if (path.startsWith('/admin/audit')) return t('adminAudit.subtitle');
    if (path.startsWith('/admin/banking') || path.startsWith('/admin/sandbox')) return t('adminBanking.subtitle');
    if (path.startsWith('/admin/reconciliation')) return t('adminReconciliation.subtitle');
    if (path.startsWith('/admin/health')) return t('adminHealth.subtitle');
    if (path.startsWith('/admin/configuration')) return t('adminConfig.subtitle');
    if (path.startsWith('/admin/feature-flags')) return t('adminFeatureFlags.subtitle');
    return t('adminOverview.subtitle');
  };

  return (
    <header
      id="admin-header"
      className="sticky top-0 z-20 flex h-16 w-full items-center justify-between border-b border-slate-200 dark:border-slate-800 bg-white/95 dark:bg-[#0E1726]/95 backdrop-blur-md px-6 sm:px-8"
    >
      {/* Left: Page Title & Admin Context */}
      <div className="flex flex-col min-w-0">
        <div className="flex items-center gap-2">
          <h1 className="text-base sm:text-lg font-bold text-slate-900 dark:text-white tracking-tight leading-tight truncate">
            {getAdminRouteTitle()}
          </h1>
          <span className="hidden sm:inline-flex items-center gap-1 text-[10px] font-bold px-2 py-0.5 rounded-full bg-slate-900 dark:bg-emerald-600 text-white shadow-xs">
            <Shield className="w-2.5 h-2.5" />
            ADMIN
          </span>
        </div>
        <p className="text-xs text-slate-500 dark:text-slate-400 truncate hidden sm:block">
          {getAdminRouteSubtitle()}
        </p>
      </div>

      {/* Right: Lang, Theme, Admin Profile */}
      <div className="flex items-center gap-3 sm:gap-4">
        {/* Language Switcher */}
        <Tooltip
          content={locale === 'vi-VN' ? 'Chuyển sang tiếng Anh' : 'Switch to Vietnamese'}
          side="bottom"
        >
          <button
            id="admin-btn-lang"
            onClick={() => setLocale(locale === 'vi-VN' ? 'en-US' : 'vi-VN')}
            className="flex h-9 items-center gap-1.5 rounded-xl border border-slate-200 dark:border-slate-800 bg-slate-50 dark:bg-slate-900/60 px-2.5 text-xs font-semibold text-slate-700 dark:text-slate-200 hover:bg-slate-100 dark:hover:bg-slate-800 transition"
          >
            <Globe className="h-3.5 w-3.5 text-slate-400" />
            <span>{locale === 'vi-VN' ? 'VIE' : 'ENG'}</span>
          </button>
        </Tooltip>

        {/* Theme Mode Selector (Light, Dark, System) - 3-State Icon Buttons */}
        <div id="admin-theme-selector" className="flex items-center p-0.5 rounded-lg bg-slate-100 dark:bg-slate-800 border border-slate-200/60 dark:border-slate-700/60">
          <Tooltip content={t('common.light') || 'Light'} side="bottom">
            <button
              type="button"
              id="admin-btn-theme-light"
              onClick={() => setTheme('light')}
              className={cn(
                'flex h-7 w-7 items-center justify-center rounded-md transition-all cursor-pointer',
                theme === 'light'
                  ? 'bg-white dark:bg-slate-900 text-amber-500 shadow-xs font-bold'
                  : 'text-slate-400 hover:text-slate-700 dark:hover:text-slate-200'
              )}
              aria-label={locale === 'vi-VN' ? 'Chế độ sáng' : 'Light mode'}
            >
              <Sun className="h-3.5 w-3.5" />
            </button>
          </Tooltip>

          <Tooltip content={t('common.dark') || 'Dark'} side="bottom">
            <button
              type="button"
              id="admin-btn-theme-dark"
              onClick={() => setTheme('dark')}
              className={cn(
                'flex h-7 w-7 items-center justify-center rounded-md transition-all cursor-pointer',
                theme === 'dark'
                  ? 'bg-white dark:bg-slate-900 text-sky-400 shadow-xs font-bold'
                  : 'text-slate-400 hover:text-slate-700 dark:hover:text-slate-200'
              )}
              aria-label={locale === 'vi-VN' ? 'Chế độ tối' : 'Dark mode'}
            >
              <Moon className="h-3.5 w-3.5" />
            </button>
          </Tooltip>

          <Tooltip content={t('common.system') || 'System'} side="bottom">
            <button
              type="button"
              id="admin-btn-theme-system"
              onClick={() => setTheme('system')}
              className={cn(
                'flex h-7 w-7 items-center justify-center rounded-md transition-all cursor-pointer',
                theme === 'system'
                  ? 'bg-white dark:bg-slate-900 text-emerald-500 shadow-xs font-bold'
                  : 'text-slate-400 hover:text-slate-700 dark:hover:text-slate-200'
              )}
              aria-label={locale === 'vi-VN' ? 'Theo hệ thống' : 'System mode'}
            >
              <Laptop className="h-3.5 w-3.5" />
            </button>
          </Tooltip>
        </div>

        {/* Notifications */}
        <div className="relative">
          <Tooltip content={t('common.notifications')} side="bottom">
            <button
              id="admin-btn-notif"
              onClick={() => setIsNotifOpen((prev) => !prev)}
              className="relative flex h-9 w-9 items-center justify-center rounded-xl border border-slate-200 dark:border-slate-800 bg-slate-50 dark:bg-slate-900/60 text-slate-600 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800 transition"
            >
              <Bell className="h-4 w-4" />
              {unreadCount > 0 && (
                <span className="absolute -top-1 -right-1 flex h-4 w-4 items-center justify-center rounded-full bg-rose-500 text-[9px] font-bold text-white shadow-xs">
                  {unreadCount > 99 ? '99+' : unreadCount}
                </span>
              )}
            </button>
          </Tooltip>
          <NotificationsDropdown isOpen={isNotifOpen} onClose={() => setIsNotifOpen(false)} />
        </div>

        {/* User / Admin Dropdown */}
        <div className="relative">
          <button
            id="admin-btn-user"
            onClick={() => setIsUserMenuOpen((prev) => !prev)}
            className="flex items-center gap-2.5 rounded-xl border border-slate-200 dark:border-slate-800 p-1 pr-3 hover:bg-slate-50 dark:hover:bg-slate-800/60 transition"
          >
            <div className="flex h-7 w-7 items-center justify-center rounded-lg bg-slate-900 dark:bg-emerald-600 text-white font-bold text-xs shadow-xs">
              {currentUser?.avatarInitials || 'AD'}
            </div>
            <div className="flex flex-col text-left">
              <span className="text-xs font-semibold text-slate-800 dark:text-slate-200 leading-none">
                {currentUser?.name || 'Admin'}
              </span>
              <span className="text-[10px] text-slate-400 mt-0.5 leading-none truncate max-w-[120px]">
                {currentUser?.email || currentUser?.phone || 'admin'}
              </span>
            </div>
          </button>

          <UserDropdown isOpen={isUserMenuOpen} onClose={() => setIsUserMenuOpen(false)} />
        </div>
      </div>
    </header>
  );
};
