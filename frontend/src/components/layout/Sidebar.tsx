import React, { useMemo } from 'react';
import { NavLink, useLocation } from 'react-router-dom';
import {
  LayoutDashboard,
  Wallet,
  ArrowLeftRight,
  Send,
  PieChart,
  Target,
  Users,
  UserCheck,
  LifeBuoy,
  PanelLeftClose,
  PanelLeftOpen,
} from 'lucide-react';
import { useI18n } from '../../i18n/I18nContext';
import { useAuth } from '../../providers/AuthProvider';
import { useFinancial } from '../../providers/FinancialProvider';
import { useSupportDisputes } from '../../services/supportDisputeService';
import { cn } from '../../utils/cn';
import { Tooltip } from '../ui/Tooltip';
import { ActionBadge } from '../ui/ActionBadge';
import { SpendWiseLogo } from '../brand/SpendWiseLogo';
import { APP_META } from '../../config/appMeta';

interface SidebarProps {
  isCollapsed: boolean;
  onToggleCollapse: () => void;
}

export const Sidebar: React.FC<SidebarProps> = ({ isCollapsed, onToggleCollapse }) => {
  const { t, locale } = useI18n();
  const location = useLocation();
  const { currentUser } = useAuth();
  const { activeBudgetAlertsCount, dueRecurringCount } = useFinancial();

  const { supportCases, disputes } = useSupportDisputes(currentUser);

  const userActionNeedMoreInfoCount = useMemo(() => {
    if (!currentUser?.id) return 0;
    const casesCount = (supportCases || []).filter(
      (c) => c.userId === currentUser.id && c.status === 'need_more_info'
    ).length;
    const disputesCount = (disputes || []).filter(
      (d) => d.userId === currentUser.id && d.status === 'need_more_info'
    ).length;
    return casesCount + disputesCount;
  }, [currentUser?.id, supportCases, disputes]);

  const mainNavItems = [
    {
      id: 'overview',
      label: t('navigation.overview'),
      path: '/dashboard',
      icon: LayoutDashboard,
    },
    {
      id: 'wallet',
      label: t('navigation.wallet'),
      path: '/wallet',
      icon: Wallet,
    },
    {
      id: 'transactions',
      label: t('navigation.transactions'),
      path: '/transactions',
      icon: ArrowLeftRight,
      badgeCount: dueRecurringCount,
    },
    {
      id: 'transfer',
      label: t('navigation.transfer'),
      path: '/transfer',
      icon: Send,
    },
    {
      id: 'budget',
      label: t('navigation.budget'),
      path: '/budget',
      icon: PieChart,
      badgeCount: activeBudgetAlertsCount,
    },
    {
      id: 'goals',
      label: t('navigation.goals'),
      path: '/goals',
      icon: Target,
    },
    {
      id: 'groups',
      label: t('navigation.groups'),
      path: '/groups',
      icon: Users,
    },
    {
      id: 'friends',
      label: locale === 'vi-VN' ? 'Bạn bè' : 'Friends',
      path: '/friends',
      icon: UserCheck,
    },
    {
      id: 'support-disputes',
      label: t('navigation.supportDisputes'),
      path: '/support',
      icon: LifeBuoy,
      badgeCount: userActionNeedMoreInfoCount,
    },
  ];

  return (
    <aside
      className={cn(
        'relative flex flex-col bg-white dark:bg-[#0F172A] text-slate-800 dark:text-white border-r border-slate-200 dark:border-slate-800/80 transition-all duration-200 z-30 shrink-0 select-none min-h-screen',
        isCollapsed ? 'w-[72px]' : 'w-[256px]'
      )}
    >
      {/* Top Header: Logo, Brand & Top Collapse Button */}
      <div
        className={cn(
          'flex items-center h-16 px-4 border-b border-slate-200/80 dark:border-white/5',
          isCollapsed ? 'justify-center' : 'justify-between'
        )}
      >
        <NavLink to="/dashboard" className="flex items-center gap-3 group focus:outline-none min-w-0">
          {/* SpendWise Official Brand Badge */}
          <SpendWiseLogo
            size={34}
            variant="badge"
            className="group-hover:scale-105 transition-transform duration-200"
          />

          {!isCollapsed && (
            <div className="flex flex-col truncate">
              <span className="text-lg font-bold tracking-tight text-slate-900 dark:text-white leading-tight truncate">
                SpendWise
              </span>
              <span className="text-[10px] uppercase font-semibold tracking-wider text-slate-500 dark:text-slate-400">
                Finance OS
              </span>
            </div>
          )}
        </NavLink>

        {/* Top collapse button when expanded */}
        {!isCollapsed && (
          <Tooltip content={t('navigation.collapseSidebar')} side="right">
            <button
              type="button"
              onClick={onToggleCollapse}
              className="flex h-8 w-8 items-center justify-center rounded-lg text-slate-500 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white hover:bg-slate-100 dark:hover:bg-white/10 transition-colors focus:outline-none shrink-0 cursor-pointer"
              aria-label={t('navigation.collapseSidebar')}
            >
              <PanelLeftClose className="h-4 w-4" />
            </button>
          </Tooltip>
        )}
      </div>

      {/* Top expand button when collapsed */}
      {isCollapsed && (
        <div className="py-2 flex justify-center border-b border-slate-200/80 dark:border-white/5">
          <Tooltip content={t('navigation.expandSidebar')} side="right">
            <button
              type="button"
              onClick={onToggleCollapse}
              className="flex h-9 w-9 items-center justify-center rounded-xl text-slate-500 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white hover:bg-slate-100 dark:hover:bg-white/10 transition-colors focus:outline-none cursor-pointer"
              aria-label={t('navigation.expandSidebar')}
            >
              <PanelLeftOpen className="h-4 w-4" />
            </button>
          </Tooltip>
        </div>
      )}

      {/* Main Navigation List */}
      <nav className="flex-1 py-4 px-3 space-y-1 overflow-y-auto">
        {mainNavItems.map((item) => {
          const Icon = item.icon;
          const isActive =
            item.path === '/dashboard'
              ? location.pathname === '/' || location.pathname === '/dashboard'
              : item.path === '/support'
              ? location.pathname === '/support' || location.pathname === '/disputes'
              : location.pathname.startsWith(item.path);

          const navButton = (
            <NavLink
              to={item.path}
              className={cn(
                'relative group flex items-center gap-3 px-4 py-3 rounded-xl text-sm font-medium transition-colors duration-150',
                isActive
                  ? 'bg-emerald-50 dark:bg-white/10 text-emerald-700 dark:text-emerald-400 font-semibold shadow-xs'
                  : 'text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-slate-200 hover:bg-slate-100 dark:hover:bg-white/5',
                isCollapsed && 'justify-center px-0 h-10 w-10 mx-auto'
              )}
            >
              <Icon
                className={cn(
                  'h-5 w-5 shrink-0 transition-colors',
                  isActive
                    ? 'text-emerald-600 dark:text-emerald-400'
                    : 'text-slate-400 group-hover:text-slate-600 dark:text-slate-400 dark:group-hover:text-slate-200'
                )}
              />
              {!isCollapsed && (
                <span className="truncate flex-1">{item.label}</span>
              )}
              {!isCollapsed && item.badgeCount && item.badgeCount > 0 ? (
                <ActionBadge count={item.badgeCount} />
              ) : null}
              {isCollapsed && item.badgeCount && item.badgeCount > 0 ? (
                <span className="absolute top-1.5 right-1.5 w-2 h-2 rounded-full bg-rose-500 ring-2 ring-white dark:ring-[#0F172A]" />
              ) : null}
            </NavLink>
          );

          if (isCollapsed) {
            const tooltipText = item.badgeCount && item.badgeCount > 0 ? `${item.label} (${item.badgeCount})` : item.label;
            return (
              <div key={item.id} className="flex justify-center">
                <Tooltip content={tooltipText} side="right">
                  {navButton}
                </Tooltip>
              </div>
            );
          }

          return <div key={item.id}>{navButton}</div>;
        })}
      </nav>

      {/* Bottom Footer: App Version */}
      <div className="p-3 border-t border-slate-200/80 dark:border-white/5">
        {isCollapsed ? (
          <div
            className="flex justify-center py-1"
            title={`${APP_META.name} v${APP_META.version}`}
          >
            <span className="text-[10px] font-semibold text-slate-400 dark:text-slate-500">
              v{APP_META.version}
            </span>
          </div>
        ) : (
          <div className="px-3 py-2 rounded-xl bg-slate-50 dark:bg-slate-900/60 border border-slate-200/80 dark:border-slate-800 flex items-center justify-between">
            <div className="flex items-center gap-2">
              <span className="h-2 w-2 rounded-full bg-emerald-500" />
              <span className="text-[11px] font-medium text-slate-600 dark:text-slate-400">
                {APP_META.name} v{APP_META.version}
              </span>
            </div>
            <span className="text-[10px] font-medium px-1.5 py-0.5 rounded bg-slate-200/70 dark:bg-slate-800 text-slate-500 dark:text-slate-400">
              {APP_META.stage}
            </span>
          </div>
        )}
      </div>
    </aside>
  );
};
