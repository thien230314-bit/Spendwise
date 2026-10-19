import React, { useState, useEffect, useMemo } from 'react';
import { NavLink, useLocation, useNavigate } from 'react-router-dom';
import {
  LayoutDashboard,
  Users,
  WalletCards,
  ArrowLeftRight,
  BookOpen,
  ShieldAlert,
  CheckCheck,
  Headphones,
  Scale,
  Building2,
  Sliders,
  ScrollText,
  Activity,
  ToggleLeft,
  ChevronDown,
  ChevronRight,
  PanelLeftClose,
  PanelLeftOpen,
  ShieldCheck,
} from 'lucide-react';
import { useI18n } from '../../i18n/I18nContext';
import { useAuth } from '../../providers/AuthProvider';
import { useSupportDisputes } from '../../services/supportDisputeService';
import { casesApiService } from '../../services/casesApiService';
import { ActionBadge } from '../ui/ActionBadge';
import { APP_META } from '../../config/appMeta';
import { cn } from '../../utils/cn';
import { Tooltip } from '../ui/Tooltip';
import { SpendWiseLogo } from '../brand/SpendWiseLogo';

interface AdminSidebarProps {
  isCollapsed: boolean;
  onToggleCollapse: () => void;
}

interface AdminNavItem {
  id: string;
  label: string;
  path: string;
  exact?: boolean;
  icon: React.ComponentType<{ className?: string }>;
  badge?: string;
  badgeColor?: string;
}

interface AdminNavGroup {
  id: string;
  label: string;
  icon: React.ComponentType<{ className?: string }>;
  badge?: string;
  badgeColor?: string;
  children: AdminNavItem[];
}

type AdminSidebarEntry =
  | { type: 'item'; data: AdminNavItem }
  | { type: 'group'; data: AdminNavGroup };

export const AdminSidebar: React.FC<AdminSidebarProps> = ({
  isCollapsed,
  onToggleCollapse,
}) => {
  const { t } = useI18n();
  const location = useLocation();
  const navigate = useNavigate();
  const { currentUser } = useAuth();
  const { supportCases, disputes } = useSupportDisputes(currentUser);

  const [adminBadgeTick, setAdminBadgeTick] = useState(0);
  const [backendRecoveryPendingCount, setBackendRecoveryPendingCount] = useState(0);

  useEffect(() => {
    const handleUpdate = () => {
      casesApiService.invalidateAdminAccountRecoveriesCache();
      setAdminBadgeTick((prev) => prev + 1);
    };
    window.addEventListener('spendwise_recovery_requests_updated', handleUpdate);
    window.addEventListener('storage', handleUpdate);
    return () => {
      window.removeEventListener('spendwise_recovery_requests_updated', handleUpdate);
      window.removeEventListener('storage', handleUpdate);
    };
  }, []);

  useEffect(() => {
    const isSysAdmin = currentUser?.role === 'SystemAdmin' || (currentUser as any)?.systemRole === 'SystemAdmin';
    if (isSysAdmin) {
      casesApiService
        .getAdminAccountRecoveries({ status: 'Pending' })
        .then((res) => {
          setBackendRecoveryPendingCount(Array.isArray(res) ? res.length : 0);
        })
        .catch(() => {});
    }
  }, [currentUser?.role, (currentUser as any)?.systemRole, adminBadgeTick]);

  const { supportChildCount, disputeChildCount, parentSupportDisputesCount } = useMemo(() => {
    const supportCount = (supportCases || []).filter(
      (c) => c.status === 'pending' || c.status === 'open' || c.status === 'in_progress'
    ).length;

    const recoveryCount = backendRecoveryPendingCount;

    const disputeCount = (disputes || []).filter(
      (d) =>
        d.status === 'pending' ||
        d.status === 'submitted' ||
        d.status === 'under_review' ||
        d.status === 'in_review'
    ).length;

    const supChild = supportCount + recoveryCount;
    const dispChild = disputeCount;
    const parentCount = supChild + dispChild;

    return {
      supportChildCount: supChild,
      disputeChildCount: dispChild,
      parentSupportDisputesCount: parentCount,
    };
  }, [supportCases, disputes, adminBadgeTick]);

  const navEntries: AdminSidebarEntry[] = [
    {
      type: 'item',
      data: {
        id: 'overview',
        label: t('adminNav.overview'),
        path: '/admin',
        exact: true,
        icon: LayoutDashboard,
      },
    },
    {
      type: 'item',
      data: {
        id: 'users',
        label: t('adminNav.users'),
        path: '/admin/users',
        icon: Users,
      },
    },
    {
      type: 'group',
      data: {
        id: 'financial-ops',
        label: t('adminNav.financialOps'),
        icon: WalletCards,
        children: [
          {
            id: 'transactions',
            label: t('adminNav.transactions'),
            path: '/admin/transactions',
            icon: ArrowLeftRight,
          },
          {
            id: 'ledger',
            label: t('adminNav.ledger'),
            path: '/admin/ledger',
            icon: BookOpen,
          },
          {
            id: 'risk',
            label: t('adminNav.risk'),
            path: '/admin/risk',
            icon: ShieldAlert,
          },
          {
            id: 'reconciliation',
            label: t('adminNav.reconciliation'),
            path: '/admin/reconciliation',
            icon: CheckCheck,
          },
        ],
      },
    },
    {
      type: 'group',
      data: {
        id: 'support-disputes',
        label: t('adminNav.supportAndDisputes'),
        icon: Headphones,
        badge: parentSupportDisputesCount > 0 ? String(parentSupportDisputesCount) : undefined,
        children: [
          {
            id: 'support',
            label: t('adminNav.supportRequests'),
            path: '/admin/support',
            icon: Headphones,
            badge: supportChildCount > 0 ? String(supportChildCount) : undefined,
          },
          {
            id: 'disputes',
            label: t('adminNav.disputes'),
            path: '/admin/disputes',
            icon: Scale,
            badge: disputeChildCount > 0 ? String(disputeChildCount) : undefined,
          },
        ],
      },
    },
    {
      type: 'item',
      data: {
        id: 'banking',
        label: t('adminNav.banking'),
        path: '/admin/banking',
        icon: Building2,
      },
    },
    {
      type: 'group',
      data: {
        id: 'system-admin',
        label: t('adminNav.systemAdmin'),
        icon: Sliders,
        children: [
          {
            id: 'audit',
            label: t('adminNav.audit'),
            path: '/admin/audit',
            icon: ScrollText,
          },
          {
            id: 'health',
            label: t('adminNav.health'),
            path: '/admin/health',
            icon: Activity,
          },
          {
            id: 'configuration',
            label: t('adminNav.configuration'),
            path: '/admin/configuration',
            icon: Sliders,
          },
          {
            id: 'feature-flags',
            label: t('adminNav.featureFlags'),
            path: '/admin/feature-flags',
            icon: ToggleLeft,
          },
        ],
      },
    },
  ];

  // Initialize expanded state: expand group if current route is inside it
  const [expandedGroups, setExpandedGroups] = useState<Record<string, boolean>>(() => {
    const initial: Record<string, boolean> = {};
    navEntries.forEach((entry) => {
      if (entry.type === 'group') {
        const isChildActive = entry.data.children.some(
          (child) =>
            location.pathname === child.path ||
            location.pathname.startsWith(`${child.path}/`)
        );
        initial[entry.data.id] = isChildActive;
      }
    });
    return initial;
  });

  // Ensure active route's parent automatically remains expanded
  useEffect(() => {
    navEntries.forEach((entry) => {
      if (entry.type === 'group') {
        const isChildActive = entry.data.children.some(
          (child) =>
            location.pathname === child.path ||
            location.pathname.startsWith(`${child.path}/`)
        );
        if (isChildActive) {
          setExpandedGroups((prev) => ({
            ...prev,
            [entry.data.id]: true,
          }));
        }
      }
    });
  }, [location.pathname]);

  const toggleGroup = (groupId: string) => {
    setExpandedGroups((prev) => ({
      ...prev,
      [groupId]: !prev[groupId],
    }));
  };

  return (
    <aside
      id="admin-sidebar"
      className={cn(
        'sticky top-0 h-screen z-30 flex flex-col border-r border-slate-200 dark:border-slate-800 bg-white dark:bg-[#0E1726] transition-all duration-200 ease-in-out shrink-0 select-none',
        isCollapsed ? 'w-[72px]' : 'w-64'
      )}
    >
      {/* Brand Header */}
      <div className="flex h-16 items-center px-4 border-b border-slate-200 dark:border-slate-800 justify-between">
        <div className="flex items-center gap-3 min-w-0">
          <div className="relative shrink-0">
            <SpendWiseLogo size={36} variant="badge" />
            <span className="absolute -bottom-1 -right-1 flex h-4 w-4 items-center justify-center rounded-full bg-emerald-600 text-white ring-2 ring-white dark:ring-[#0E1726] shadow-xs">
              <ShieldCheck className="h-2.5 w-2.5" />
            </span>
          </div>
          {!isCollapsed && (
            <div className="flex flex-col min-w-0">
              <span className="text-sm font-bold text-slate-900 dark:text-white tracking-tight leading-none truncate">
                SpendWise
              </span>
              <div className="flex items-center gap-1.5 mt-1">
                <span className="text-[10px] font-extrabold uppercase tracking-widest text-emerald-600 dark:text-emerald-400">
                  {t('adminNav.consoleSubtitle')}
                </span>
              </div>
            </div>
          )}
        </div>

        {/* Collapse button */}
        {!isCollapsed && (
          <button
            id="admin-sidebar-collapse-btn"
            onClick={onToggleCollapse}
            className="flex h-8 w-8 items-center justify-center rounded-lg text-slate-400 hover:text-slate-600 dark:hover:text-slate-200 hover:bg-slate-100 dark:hover:bg-slate-800/80 transition-colors"
            title={t('navigation.collapseSidebar')}
          >
            <PanelLeftClose className="h-4 w-4" />
          </button>
        )}
      </div>

      {/* Main Admin Navigation */}
      <div className="flex-1 overflow-y-auto px-3 py-4 space-y-1.5 scrollbar-thin scrollbar-thumb-slate-300 dark:scrollbar-thumb-slate-700">
        {!isCollapsed && (
          <div className="px-3 pb-1 text-[10px] font-bold uppercase tracking-wider text-slate-400 dark:text-slate-500">
            {t('adminNav.consoleTitle')}
          </div>
        )}

        {navEntries.map((entry) => {
          // 1. Standalone navigation item
          if (entry.type === 'item') {
            const item = entry.data;
            const Icon = item.icon;
            const isActive = item.exact
              ? location.pathname === item.path || (item.path === '/admin' && location.pathname === '/admin/overview')
              : location.pathname === item.path ||
                location.pathname.startsWith(`${item.path}/`) ||
                (item.path === '/admin/banking' &&
                  (location.pathname === '/admin/sandbox' ||
                    location.pathname.startsWith('/admin/sandbox/')));

            const content = (
              <NavLink
                to={item.path}
                end={Boolean(item.exact)}
                id={`admin-nav-${item.id}`}
                className={cn(
                  'group flex items-center gap-3 rounded-xl px-3 py-2.5 text-xs font-semibold transition-all duration-150 relative',
                  isActive
                    ? 'bg-emerald-600 text-white shadow-sm shadow-emerald-600/20 dark:bg-emerald-600 dark:text-white'
                    : 'text-slate-600 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800/60 hover:text-slate-900 dark:hover:text-white',
                  isCollapsed && 'justify-center px-0'
                )}
              >
                <Icon
                  className={cn(
                    'h-4 w-4 shrink-0 transition-transform duration-150 group-hover:scale-110',
                    isActive
                      ? 'text-white'
                      : 'text-slate-400 dark:text-slate-400 group-hover:text-slate-700 dark:group-hover:text-slate-200'
                  )}
                />

                {!isCollapsed && (
                  <>
                    <span className="truncate flex-1">{item.label}</span>
                    {item.badge && Number(item.badge) > 0 && (
                      <ActionBadge count={Number(item.badge)} />
                    )}
                  </>
                )}
              </NavLink>
            );

            if (isCollapsed) {
              return (
                <Tooltip key={item.id} content={item.label} side="right">
                  {content}
                </Tooltip>
              );
            }

            return <div key={item.id}>{content}</div>;
          }

          // 2. Expandable Accordion Group
          const group = entry.data;
          const GroupIcon = group.icon;
          const isExpanded = !!expandedGroups[group.id];
          const isGroupActive = group.children.some(
            (child) =>
              location.pathname === child.path ||
              location.pathname.startsWith(`${child.path}/`)
          );

          // Collapsed sidebar view for group
          if (isCollapsed) {
            const activeChild = group.children.find(
              (child) =>
                location.pathname === child.path ||
                location.pathname.startsWith(`${child.path}/`)
            );
            const targetPath = activeChild ? activeChild.path : group.children[0].path;

            return (
              <Tooltip
                key={group.id}
                content={
                  <div>
                    <div className="font-bold text-xs flex items-center justify-between gap-2">
                      <span>{group.label}</span>
                      {group.badge && Number(group.badge) > 0 && (
                        <ActionBadge count={Number(group.badge)} />
                      )}
                    </div>
                    <div className="text-[11px] text-slate-300 mt-1 space-y-1">
                      {group.children.map((c) => (
                        <div
                          key={c.id}
                          className={cn(
                            'flex items-center justify-between gap-2',
                            (location.pathname === c.path ||
                              location.pathname.startsWith(`${c.path}/`)) &&
                              'text-emerald-400 font-semibold'
                          )}
                        >
                          <span>• {c.label}</span>
                          {c.badge && Number(c.badge) > 0 && (
                            <ActionBadge count={Number(c.badge)} />
                          )}
                        </div>
                      ))}
                    </div>
                  </div>
                }
                side="right"
              >
                <button
                  id={`admin-nav-group-collapsed-${group.id}`}
                  onClick={() => navigate(targetPath)}
                  className={cn(
                    'group relative flex h-10 w-full items-center justify-center rounded-xl text-xs font-semibold transition-all duration-150',
                    isGroupActive
                      ? 'bg-emerald-50 dark:bg-emerald-950/40 text-emerald-600 dark:text-emerald-400 ring-1 ring-emerald-500/30'
                      : 'text-slate-600 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800/60 hover:text-slate-900 dark:hover:text-white'
                  )}
                >
                  <GroupIcon
                    className={cn(
                      'h-4 w-4 shrink-0 transition-transform duration-150 group-hover:scale-110',
                      isGroupActive
                        ? 'text-emerald-600 dark:text-emerald-400'
                        : 'text-slate-400 dark:text-slate-400 group-hover:text-slate-700 dark:group-hover:text-slate-200'
                    )}
                  />
                  {group.badge && Number(group.badge) > 0 && (
                    <span
                      className="absolute top-1.5 right-1.5 h-2 w-2 rounded-full ring-2 ring-white dark:ring-[#0E1726] bg-rose-500"
                    />
                  )}
                </button>
              </Tooltip>
            );
          }

          // Expanded sidebar view for group
          return (
            <div key={group.id} className="space-y-1">
              {/* Group Accordion Header */}
              <button
                id={`admin-nav-group-header-${group.id}`}
                onClick={() => toggleGroup(group.id)}
                className={cn(
                  'group flex w-full items-center justify-between gap-2.5 rounded-xl px-3 py-2 text-xs font-semibold transition-all duration-150',
                  isGroupActive
                    ? 'text-slate-900 dark:text-white bg-slate-100/70 dark:bg-slate-800/40 font-bold'
                    : 'text-slate-600 dark:text-slate-300 hover:bg-slate-100/80 dark:hover:bg-slate-800/60 hover:text-slate-900 dark:hover:text-white'
                )}
                aria-expanded={isExpanded}
              >
                <div className="flex items-center gap-3 min-w-0 flex-1">
                  <GroupIcon
                    className={cn(
                      'h-4 w-4 shrink-0 transition-colors',
                      isGroupActive
                        ? 'text-emerald-600 dark:text-emerald-400'
                        : 'text-slate-400 dark:text-slate-400 group-hover:text-slate-700 dark:group-hover:text-slate-200'
                    )}
                  />
                  <span className="truncate text-left">{group.label}</span>
                </div>

                <div className="flex items-center gap-1.5 shrink-0">
                  {group.badge && Number(group.badge) > 0 && (
                    <ActionBadge count={Number(group.badge)} />
                  )}
                  {isExpanded ? (
                    <ChevronDown className="h-3.5 w-3.5 text-slate-400 group-hover:text-slate-600 dark:group-hover:text-slate-300 transition-colors" />
                  ) : (
                    <ChevronRight className="h-3.5 w-3.5 text-slate-400 group-hover:text-slate-600 dark:group-hover:text-slate-300 transition-colors" />
                  )}
                </div>
              </button>

              {/* Group Children (Accordion Body) */}
              {isExpanded && (
                <div
                  id={`admin-nav-group-children-${group.id}`}
                  className="ml-3 pl-3 border-l border-slate-200 dark:border-slate-800 space-y-0.5 my-1"
                >
                  {group.children.map((child) => {
                    const ChildIcon = child.icon;
                    const isChildActive = child.exact
                      ? location.pathname === child.path
                      : location.pathname === child.path ||
                        location.pathname.startsWith(`${child.path}/`);

                    return (
                      <NavLink
                        key={child.id}
                        to={child.path}
                        end={Boolean(child.exact)}
                        id={`admin-nav-child-${child.id}`}
                        className={cn(
                          'group flex items-center gap-2.5 rounded-lg px-2.5 py-2 text-xs transition-all duration-150 relative',
                          isChildActive
                            ? 'bg-emerald-600 text-white font-semibold shadow-xs shadow-emerald-600/20 dark:bg-emerald-600 dark:text-white'
                            : 'text-slate-600 dark:text-slate-400 hover:bg-slate-100 dark:hover:bg-slate-800/60 hover:text-slate-900 dark:hover:text-white font-medium'
                        )}
                      >
                        <ChildIcon
                          className={cn(
                            'h-3.5 w-3.5 shrink-0 transition-transform duration-150 group-hover:scale-110',
                            isChildActive
                              ? 'text-white'
                              : 'text-slate-400 dark:text-slate-500 group-hover:text-slate-700 dark:group-hover:text-slate-200'
                          )}
                        />
                        <span className="truncate flex-1">{child.label}</span>
                        {child.badge && Number(child.badge) > 0 && (
                          <ActionBadge count={Number(child.badge)} />
                        )}
                      </NavLink>
                    );
                  })}
                </div>
              )}
            </div>
          );
        })}
      </div>

      {/* Footer / Expand Button for Collapsed State */}
      <div className="p-3 border-t border-slate-200 dark:border-slate-800">
        {isCollapsed ? (
          <button
            id="admin-sidebar-expand-btn"
            onClick={onToggleCollapse}
            className="flex h-10 w-full items-center justify-center rounded-xl text-slate-400 hover:text-slate-600 dark:hover:text-slate-200 hover:bg-slate-100 dark:hover:bg-slate-800 transition-colors"
            title={t('navigation.expandSidebar')}
          >
            <PanelLeftOpen className="h-5 w-5" />
          </button>
        ) : (
          <div className="px-3 py-2 rounded-xl bg-slate-50 dark:bg-slate-900/60 border border-slate-200/80 dark:border-slate-800 flex items-center justify-between">
            <div className="flex items-center gap-2">
              <span className="h-2 w-2 rounded-full bg-emerald-500 animate-pulse" />
              <span className="text-[11px] font-medium text-slate-600 dark:text-slate-400">
                {APP_META.name} v{APP_META.version}
              </span>
            </div>
            <span className="text-[10px] font-bold px-1.5 py-0.5 rounded bg-emerald-100 dark:bg-emerald-950/60 text-emerald-700 dark:text-emerald-400">
              {t('adminNav.badge')}
            </span>
          </div>
        )}
      </div>
    </aside>
  );
};

