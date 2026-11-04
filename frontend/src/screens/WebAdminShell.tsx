import React, { useState, useMemo, useEffect, useCallback } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import { useAuth } from '../providers/AuthProvider';
import {
  casesApiService,
  AdminDashboardDto,
  AdminUserDto,
  SupportCaseDetailDto,
  AdminTransactionDto,
  AuditLogDto,
} from '../services/casesApiService';
import {
  systemOperationsService,
  SystemMaintenanceState,
  EmergencyAnnouncement,
} from '../services/systemOperationsService';
import {
  LayoutDashboard,
  FileBarChart2,
  ClipboardCheck,
  BellRing,
  Users,
  ArrowLeftRight,
  LifeBuoy,
  Settings,
  Megaphone,
  Cpu,
  Building2,
  ClipboardList,
  AlertTriangle,
  Menu,
  X,
  Search,
  Download,
  Check,
  ArrowUpRight,
  Sun,
  Moon,
  Globe,
  Smartphone,
  SlidersHorizontal,
  RotateCcw,
  PanelLeftClose,
  PanelLeftOpen,
} from 'lucide-react';
import { formatVND, useSpendWise } from '../context/SpendWiseContext';
import {
  ADMIN_ACTIVITY_REPORTS_FIXTURES,
  ADMIN_AI_OPERATIONS_FIXTURES,
  ADMIN_BANK_GATEWAYS_FIXTURES,
  ADMIN_BROADCAST_FIXTURES,
  ADMIN_INITIAL_GLOBAL_AUDIT_FIXTURES,
  ADMIN_LEDGER_TRANSACTIONS_FIXTURES,
  ADMIN_OPERATIONAL_TASKS_FIXTURES,
  ADMIN_OVERVIEW_WINDOWS,
  ADMIN_SUPPORT_CASES_FIXTURES,
  ADMIN_SYNTHETIC_WALLETS_FIXTURES,
  ADMIN_SYSTEM_ALERTS_FIXTURES,
  ADMIN_SYSTEM_CONFIG_FIXTURES,
  ADMIN_USER_DIRECTORY_FIXTURES,
  AdminActivityReportRow,
  AdminAiPipelineEntry,
  AdminBankGatewayEntry,
  AdminBroadcastEntry,
  AdminGlobalAuditEvent,
  AdminOperationalTask,
  AdminScreenId,
  AdminSupportCaseEntry,
  AdminSupportCaseCategory,
  AdminSupportCaseStatus,
  AdminSupportCasePriority,
  AdminSyntheticWalletLedgerItem,
  AdminSystemAlert,
  AdminSystemConfigParameter,
  AdminTimeframeId,
  AdminUserDirectoryEntry,
  AdminUserAccountStatus,
} from '../data/adminFixtures';
import { AdminUsersSection } from './AdminUsersSection';
import {
  AdminLedgerSubTabId,
  AdminTransactionsLedgerSection,
} from './AdminTransactionsLedgerSection';
import { AdminSupportSection } from './AdminSupportSection';
import {
  AdminSystemOperationsSection,
  AdminSystemSubTabId,
} from './AdminSystemOperationsSection';
import {
  AdminAccountMenu,
  AdminAuthGate,
  DEFAULT_ADMIN_ACCOUNT,
  DEFAULT_ADMIN_PASSWORD,
  clearAdminDemoFirstOtpVerified,
} from './AdminAuthGate';

export interface WebAdminShellProps {
  onExitWebAdmin?: () => void;
  onOpenMobileApp?: () => void;
  initialScreen?: AdminScreenId;
  initialSystemSubTab?: AdminSystemSubTabId;
}

type AdminNavGroupId = 'g1_monitoring' | 'g2_users' | 'g3_ledger' | 'g4_support' | 'g5_system';

type SidebarNavItemId =
  | AdminScreenId
  | 'ai_ops'
  | 'bank_gateways'
  | 'audit_log';

interface SidebarNavItem {
  id: SidebarNavItemId;
  targetScreen: AdminScreenId;
  systemSubTab?: AdminSystemSubTabId;
  labelVi: string;
  labelEn: string;
  icon: React.FC<{ className?: string }>;
  enabled: boolean;
  group: 'active' | 'planned';
  navGroup: AdminNavGroupId;
}

const NAV_GROUPS_META: Array<{
  id: AdminNavGroupId;
  testId: string;
  titleVi: string;
  titleEn: string;
}> = [
  {
    id: 'g1_monitoring',
    testId: 'sw-admin-nav-group-1',
    titleVi: 'Giám sát & vận hành',
    titleEn: 'Monitoring & Operations',
  },
  {
    id: 'g2_users',
    testId: 'sw-admin-nav-group-2',
    titleVi: 'Người dùng & bảo mật',
    titleEn: 'Users & Security',
  },
  {
    id: 'g3_ledger',
    testId: 'sw-admin-nav-group-3',
    titleVi: 'Giao dịch & sổ cái',
    titleEn: 'Transactions & Ledger',
  },
  {
    id: 'g4_support',
    testId: 'sw-admin-nav-group-4',
    titleVi: 'Hỗ trợ khách hàng',
    titleEn: 'Customer Support',
  },
  {
    id: 'g5_system',
    testId: 'sw-admin-nav-group-5',
    titleVi: 'Hệ thống & tích hợp',
    titleEn: 'System & Integrations',
  },
];

const SIDEBAR_NAV_ITEMS: SidebarNavItem[] = [
  {
    id: 'overview',
    targetScreen: 'overview',
    labelVi: 'Tổng quan',
    labelEn: 'Overview',
    icon: LayoutDashboard,
    enabled: true,
    group: 'active',
    navGroup: 'g1_monitoring',
  },
  {
    id: 'reports',
    targetScreen: 'reports',
    labelVi: 'Báo cáo',
    labelEn: 'Reports',
    icon: FileBarChart2,
    enabled: true,
    group: 'active',
    navGroup: 'g1_monitoring',
  },
  {
    id: 'tasks',
    targetScreen: 'tasks',
    labelVi: 'Việc cần xử lý',
    labelEn: 'Tasks',
    icon: ClipboardCheck,
    enabled: true,
    group: 'active',
    navGroup: 'g1_monitoring',
  },
  {
    id: 'alerts',
    targetScreen: 'alerts',
    labelVi: 'Cảnh báo',
    labelEn: 'Alerts',
    icon: BellRing,
    enabled: true,
    group: 'active',
    navGroup: 'g1_monitoring',
  },
  {
    id: 'users',
    targetScreen: 'users',
    labelVi: 'Người dùng',
    labelEn: 'Users',
    icon: Users,
    enabled: true,
    group: 'active',
    navGroup: 'g2_users',
  },
  {
    id: 'transactions',
    targetScreen: 'transactions',
    labelVi: 'Giao dịch',
    labelEn: 'Transactions',
    icon: ArrowLeftRight,
    enabled: true,
    group: 'active',
    navGroup: 'g3_ledger',
  },
  {
    id: 'support',
    targetScreen: 'support',
    labelVi: 'Hỗ trợ & khiếu nại',
    labelEn: 'Support & Complaints',
    icon: LifeBuoy,
    enabled: true,
    group: 'active',
    navGroup: 'g4_support',
  },
  {
    id: 'system',
    targetScreen: 'system',
    systemSubTab: 'broadcast',
    labelVi: 'Thông báo',
    labelEn: 'Notifications',
    icon: Megaphone,
    enabled: true,
    group: 'active',
    navGroup: 'g5_system',
  },
  {
    id: 'ai_ops',
    targetScreen: 'system',
    systemSubTab: 'ai_ops',
    labelVi: 'Giám sát AI',
    labelEn: 'AI Monitoring',
    icon: Cpu,
    enabled: true,
    group: 'active',
    navGroup: 'g5_system',
  },
  {
    id: 'bank_gateways',
    targetScreen: 'system',
    systemSubTab: 'bank_gateways',
    labelVi: 'Ngân hàng',
    labelEn: 'Bank Gateways',
    icon: Building2,
    enabled: true,
    group: 'active',
    navGroup: 'g5_system',
  },
  {
    id: 'audit_log',
    targetScreen: 'system',
    systemSubTab: 'config_audit',
    labelVi: 'Nhật ký quản trị',
    labelEn: 'Admin Log',
    icon: ClipboardList,
    enabled: true,
    group: 'active',
    navGroup: 'g5_system',
  },
];

function formatCompactMillionsVND(amount: number, lang: 'vi' | 'en'): string {
  if (!Number.isFinite(amount) || amount <= 0) return '0 ₫';
  if (amount >= 1_000_000_000) {
    const val = (amount / 1_000_000_000).toLocaleString(lang === 'en' ? 'en-US' : 'vi-VN', {
      maximumFractionDigits: 2,
    });
    return lang === 'en' ? `${val}B VND` : `${val} tỷ ₫`;
  }
  if (amount >= 1_000_000) {
    const val = (amount / 1_000_000).toLocaleString(lang === 'en' ? 'en-US' : 'vi-VN', {
      maximumFractionDigits: 1,
    });
    return lang === 'en' ? `${val}M VND` : `${val} tr ₫`;
  }
  return formatVND(amount);
}

export const ADMIN_SIDEBAR_COLLAPSED_STORAGE_KEY =
  'sw_admin_sidebar_collapsed_v2';

export const WebAdminShell: React.FC<WebAdminShellProps> = ({
  onExitWebAdmin,
  onOpenMobileApp,
  initialScreen = 'overview',
  initialSystemSubTab = 'broadcast',
}) => {
  const { tr, language, setLanguage, resolvedTheme, setTheme } = useSpendWise();
  const { currentUser, logout, isAuthenticated } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();

  const isRealAdmin = isAuthenticated && currentUser?.role === 'SystemAdmin';

  // Dedicated Admin Authentication state (strictly isolated from Mobile User session)
  const [isAdminAuthenticated, setIsAdminAuthenticated] = useState<boolean>(() => isRealAdmin);
  const [adminPassword, setAdminPassword] = useState(DEFAULT_ADMIN_PASSWORD);
  const [isResetConfirmModalOpen, setIsResetConfirmModalOpen] = useState(false);

  useEffect(() => {
    if (isRealAdmin) {
      setIsAdminAuthenticated(true);
    }
  }, [isRealAdmin]);

  // Track whether viewport is narrow (< 768px) so narrow defaults to a permanent icon rail
  // and expands a labeled overlay without pushing content, while >= 768px (including AI Studio Preview pane) toggles inline
  const [isNarrowViewport, setIsNarrowViewport] = useState<boolean>(() =>
    typeof window !== 'undefined' ? window.innerWidth < 768 : false
  );

  // Persisted Wide/Preview Left Sidebar state: expanded (false) vs compact icon rail (true)
  const [isSidebarCollapsed, setIsSidebarCollapsed] = useState<boolean>(() => {
    try {
      if (typeof localStorage === 'undefined') return false;
      return localStorage.getItem(ADMIN_SIDEBAR_COLLAPSED_STORAGE_KEY) === 'true';
    } catch {
      return false;
    }
  });

  const [railTooltip, setRailTooltip] = useState<{
    label: string;
    top: number;
    left: number;
  } | null>(null);

  const [activeScreen, setActiveScreen] = useState<AdminScreenId>(initialScreen);
  const [systemSubTab, setSystemSubTab] = useState<AdminSystemSubTabId>(initialSystemSubTab);
  const [isMobileDrawerOpen, setIsMobileDrawerOpen] = useState(false);

  // Sync with initialScreen and initialSystemSubTab props
  useEffect(() => {
    if (initialScreen) {
      setActiveScreen(initialScreen);
    }
    if (initialSystemSubTab) {
      setSystemSubTab(initialSystemSubTab);
    }
  }, [initialScreen, initialSystemSubTab]);

  // Real backend live state
  const [liveDashboard, setLiveDashboard] = useState<AdminDashboardDto | null>(null);
  const [isDashboardLoading, setIsDashboardLoading] = useState(false);
  const [dashboardError, setDashboardError] = useState<string | null>(null);

  const [isLiveUsersLoading, setIsLiveUsersLoading] = useState(false);
  const [liveUsersError, setLiveUsersError] = useState<string | null>(null);

  const [isLiveTransactionsLoading, setIsLiveTransactionsLoading] = useState(false);
  const [liveTransactionsError, setLiveTransactionsError] = useState<string | null>(null);

  const [isLiveSupportLoading, setIsLiveSupportLoading] = useState(false);
  const [liveSupportError, setLiveSupportError] = useState<string | null>(null);

  useEffect(() => {
    const handleResize = () => {
      const narrow = window.innerWidth < 768;
      setIsNarrowViewport(narrow);
      if (!narrow) {
        setIsMobileDrawerOpen(false);
      }
    };
    handleResize();
    window.addEventListener('resize', handleResize);
    return () => window.removeEventListener('resize', handleResize);
  }, []);

  const handleToggleSidebarCollapsed = () => {
    setRailTooltip(null);
    if (typeof window !== 'undefined' && window.innerWidth < 768) {
      setIsMobileDrawerOpen((prev) => !prev);
      return;
    }
    setIsSidebarCollapsed((prev) => {
      const next = !prev;
      try {
        if (typeof localStorage !== 'undefined') {
          localStorage.setItem(
            ADMIN_SIDEBAR_COLLAPSED_STORAGE_KEY,
            next ? 'true' : 'false'
          );
        }
      } catch {
        // ignore storage errors
      }
      return next;
    });
  };

  const showFloatingRailTooltip = (
    el: HTMLElement,
    label: string
  ) => {
    const rect = el.getBoundingClientRect();
    setRailTooltip({
      label,
      top: Math.round(rect.top + rect.height / 2),
      left: Math.round(rect.right + 10),
    });
  };

  const hideFloatingRailTooltip = () => {
    setRailTooltip(null);
  };

  // 1. Overview state
  const [overviewTimeframe, setOverviewTimeframe] = useState<AdminTimeframeId>('30d');

  // 2. Activity Reports state (synthetic fixtures only)
  const [reportsDomainFilter, setReportsDomainFilter] = useState<
    'all' | AdminActivityReportRow['domain']
  >('all');
  const [reportsSearchQuery, setReportsSearchQuery] = useState('');
  const [selectedReportId, setSelectedReportId] = useState<string>('rpt_101');
  const [csvExportBanner, setCsvExportBanner] = useState<string | null>(null);

  // 3. Operational Tasks state (synthetic fixtures only)
  const [tasksList, setTasksList] = useState<AdminOperationalTask[]>(() =>
    ADMIN_OPERATIONAL_TASKS_FIXTURES.map((item) => ({ ...item }))
  );
  const [taskStatusFilter, setTaskStatusFilter] = useState<
    'all' | AdminOperationalTask['status']
  >('all');
  const [taskPriorityFilter, setTaskPriorityFilter] = useState<
    'all' | AdminOperationalTask['priority']
  >('all');
  const [taskSearchQuery, setTaskSearchQuery] = useState('');
  const [selectedTaskId, setSelectedTaskId] = useState<string>('tsk_01');
  const [taskNoteDraft, setTaskNoteDraft] = useState<string>('');

  // 4. Alerts state (synthetic fixtures only)
  const [alertsList, setAlertsList] = useState<AdminSystemAlert[]>(() =>
    ADMIN_SYSTEM_ALERTS_FIXTURES.map((item) => ({ ...item }))
  );
  const [alertSeverityFilter, setAlertSeverityFilter] = useState<
    'all' | AdminSystemAlert['severity']
  >('all');
  const [alertStateFilter, setAlertStateFilter] = useState<
    'all' | AdminSystemAlert['state']
  >('all');
  const [selectedAlertId, setSelectedAlertId] = useState<string>('alt_901');

  // 5. Users Directory state (ADMIN A2 synthetic fixtures only)
  const [usersList, setUsersList] = useState<AdminUserDirectoryEntry[]>(() =>
    ADMIN_USER_DIRECTORY_FIXTURES.map((u) => ({
      ...u,
      sessions: u.sessions.map((s) => ({ ...s })),
      relationships: {
        ...u.relationships,
        groupFunds: u.relationships.groupFunds.map((g) => ({ ...g })),
      },
      auditLog: u.auditLog.map((a) => ({ ...a })),
    }))
  );

  // 6. Transactions, Synthetic Wallets, Global Audit Log & System Config (ADMIN A3)
  const [ledgerSubTab, setLedgerSubTab] =
    useState<AdminLedgerSubTabId>('transactions');
  const [walletsList, setWalletsList] = useState<
    AdminSyntheticWalletLedgerItem[]
  >(() => ADMIN_SYNTHETIC_WALLETS_FIXTURES.map((w) => ({ ...w })));
  const [auditEventsList, setAuditEventsList] = useState<
    AdminGlobalAuditEvent[]
  >(() => ADMIN_INITIAL_GLOBAL_AUDIT_FIXTURES.map((ev) => ({ ...ev })));
  const [configParamsList, setConfigParamsList] = useState<
    AdminSystemConfigParameter[]
  >(() => ADMIN_SYSTEM_CONFIG_FIXTURES.map((c) => ({ ...c })));

  // 7. Support & Complaints Case Queue state (ADMIN A4 synthetic fixtures only)
  const [supportCasesList, setSupportCasesList] = useState<
    AdminSupportCaseEntry[]
  >(() =>
    ADMIN_SUPPORT_CASES_FIXTURES.map((c) => ({
      ...c,
      scopedUser: { ...c.scopedUser },
      scopedFund: c.scopedFund ? { ...c.scopedFund } : null,
      refundClaim: c.refundClaim ? { ...c.refundClaim } : null,
      messages: c.messages.map((m) => ({ ...m })),
    }))
  );

  // 8. Broadcast Authoring, AI Operations & Bank Gateway Monitoring state (ADMIN A5)
  const [broadcastsList, setBroadcastsList] = useState<AdminBroadcastEntry[]>(
    () => ADMIN_BROADCAST_FIXTURES.map((b) => ({ ...b }))
  );
  const [aiPipelinesList, setAiPipelinesList] = useState<
    AdminAiPipelineEntry[]
  >(() => ADMIN_AI_OPERATIONS_FIXTURES.map((p) => ({ ...p })));
  const [bankGatewaysList, setBankGatewaysList] = useState<
    AdminBankGatewayEntry[]
  >(() => ADMIN_BANK_GATEWAYS_FIXTURES.map((gw) => ({ ...gw })));

  const handleAppendGlobalAudit = (event: AdminGlobalAuditEvent) => {
    setAuditEventsList((prev) => [event, ...prev]);
  };

  // Fetch Live Admin Dashboard stats from backend API
  const fetchLiveDashboard = useCallback(async () => {
    if (!isAdminAuthenticated) return;
    setIsDashboardLoading(true);
    setDashboardError(null);
    try {
      const data = await casesApiService.getAdminDashboard();
      setLiveDashboard(data);
    } catch (err: any) {
      setDashboardError(
        err?.detail || err?.title || err?.message || 'Không thể tải số liệu thời gian thực từ Backend API.'
      );
      setLiveDashboard(null);
    } finally {
      setIsDashboardLoading(false);
    }
  }, [isAdminAuthenticated]);

  // Fetch real users from backend
  const fetchRealUsers = useCallback(async () => {
    if (!isAdminAuthenticated) return;
    setIsLiveUsersLoading(true);
    setLiveUsersError(null);
    try {
      const usersData = await casesApiService.getAdminUsers();
      if (Array.isArray(usersData) && usersData.length > 0) {
        const mappedUsers: AdminUserDirectoryEntry[] = usersData.map((u) => {
          const rawStatus = (u.status || 'Active').toLowerCase();
          const accountStatus: AdminUserAccountStatus =
            rawStatus === 'locked'
              ? 'locked'
              : rawStatus === 'restricted'
              ? 'restricted'
              : rawStatus === 'suspended'
              ? 'suspended'
              : 'active';

          return {
            id: u.id,
            spendId: u.spendId || `SPEND-${u.id.slice(0, 4)}`,
            fullName: u.fullName || u.email,
            emailMasked: u.email.replace(/(.{2})(.*)(@.*)/, '$1***$3'),
            phoneMasked: '098 •••• 824',
            accountStatus,
            kycTierVi: 'eKYC Cấp 2 · Đã xác minh CCCD',
            kycTierEn: 'eKYC Tier 2 · Citizen ID Verified',
            joinedAt: u.createdAt ? new Date(u.createdAt).toLocaleDateString('vi-VN') : '2026-10-28',
            lastActiveAt: 'Vừa xong',
            pinConfigured: true,
            biometricEnrolled: true,
            assignedSupportCase: null,
            scopedProfile: {
              occupationVi: 'Chuyên viên kỹ thuật',
              occupationEn: 'Technical Specialist',
              activeWalletsCount: 1,
              linkedBanksCount: 1,
              securityNoteVi: 'Hồ sơ người dùng thực từ Backend API',
              securityNoteEn: 'Real user profile from Backend API',
            },
            sessions: [],
            relationships: {
              groupFunds: [],
              connectedFriendsCount: 0,
              closeFriendsCount: 0,
              pendingFriendRequestsCount: 0,
            },
            auditLog: [
              {
                id: `log-${u.id}`,
                referenceCode: `AUD-REG-${u.id.slice(0, 6)}`,
                timestamp: u.createdAt || new Date().toISOString(),
                actionLabelVi: 'Khởi tạo tài khoản hệ thống',
                actionLabelEn: 'System account initialized',
                detailVi: 'Đăng ký tài khoản qua Backend API',
                detailEn: 'Registered user via Backend API',
                reason: 'Người dùng SpendWise',
                operator: 'System',
              },
            ],
          };
        });
        setUsersList(mappedUsers);
      } else if (Array.isArray(usersData) && usersData.length === 0) {
        setUsersList([]);
      }
    } catch (err: any) {
      setLiveUsersError(
        err?.detail || err?.title || err?.message || 'Không thể kết nối Backend để lấy danh sách người dùng.'
      );
    } finally {
      setIsLiveUsersLoading(false);
    }
  }, [isAdminAuthenticated]);

  // Fetch real support cases from backend
  const fetchRealSupportCases = useCallback(async () => {
    if (!isAdminAuthenticated) return;
    setIsLiveSupportLoading(true);
    setLiveSupportError(null);
    try {
      const casesData = await casesApiService.getAdminSupportCases();
      if (Array.isArray(casesData) && casesData.length > 0) {
        const mappedCases: AdminSupportCaseEntry[] = casesData.map((c) => {
          const rawStatus = (c.status || 'open').toLowerCase();
          const status: AdminSupportCaseStatus =
            rawStatus === 'resolved'
              ? 'resolved'
              : rawStatus === 'rejected' || rawStatus === 'closed'
              ? 'closed'
              : rawStatus === 'awaiting_user' || rawStatus === 'waiting_for_user'
              ? 'awaiting_user_info'
              : rawStatus === 'in_progress'
              ? 'in_progress'
              : 'open';

          const priority: AdminSupportCasePriority =
            c.priority === 'High' ? 'high' : c.priority === 'Low' ? 'medium' : 'critical';

          const category: AdminSupportCaseCategory =
            c.category === 'complaint_transaction'
              ? 'fund_dispute'
              : c.category === 'pin_security'
              ? 'security_pin'
              : 'bank_topup';

          return {
            id: c.id,
            caseCode: c.ticketNumber || `TCK-${c.id.slice(0, 8)}`,
            createdAt: c.createdAt ? new Date(c.createdAt).toLocaleDateString('vi-VN') : '2026-10-28',
            updatedAt: 'Hôm nay',
            status,
            priority,
            category,
            categoryLabelVi: 'Hỗ trợ nghiệp vụ & Khiếu nại',
            categoryLabelEn: 'Support & Complaints',
            subjectVi: c.subject || 'Hồ sơ hỗ trợ',
            subjectEn: c.subject || 'Support Case',
            summaryVi: c.description || c.subject || '',
            summaryEn: c.description || c.subject || '',
            assignedOperator: c.assignedAdminId ? 'Admin Vận Hành' : null,
            scopedUser: {
              userId: c.userId || 'usr-unknown',
              fullName: 'Người dùng SpendWise',
              spendId: 'SPEND',
              emailMasked: 'u***@spendwise.test',
              phoneMasked: '098 •••• 824',
              kycTierVi: 'Cấp 2',
              kycTierEn: 'Tier 2',
              walletCode: 'WAL-01',
              walletBalanceVnd: 0,
              recentDeviceSummaryVi: 'Chrome Windows',
              recentDeviceSummaryEn: 'Chrome Windows',
              securityNoteVi: 'Hồ sơ người dùng thực từ Backend API',
              securityNoteEn: 'Real user profile from Backend API',
            },
            scopedFund: null,
            refundClaim: null,
            messages: (c.history || []).map((h: any, idx: number) => ({
              id: `msg-${idx}`,
              senderRole: h.authorRole === 'Admin' ? 'operator' : 'user',
              senderName: h.authorName || 'Hệ thống',
              timestamp: h.createdAt || new Date().toISOString(),
              bodyVi: h.content || '',
              bodyEn: h.content || '',
            })),
          };
        });
        setSupportCasesList(mappedCases);
      } else if (Array.isArray(casesData) && casesData.length === 0) {
        setSupportCasesList([]);
      }
    } catch (err: any) {
      setLiveSupportError(
        err?.detail || err?.title || err?.message || 'Không thể tải danh sách hồ sơ hỗ trợ từ Backend.'
      );
    } finally {
      setIsLiveSupportLoading(false);
    }
  }, [isAdminAuthenticated]);

  useEffect(() => {
    if (activeScreen === 'overview') {
      fetchLiveDashboard();
    } else if (activeScreen === 'users') {
      fetchRealUsers();
    } else if (activeScreen === 'support') {
      fetchRealSupportCases();
    }
  }, [activeScreen, fetchLiveDashboard, fetchRealUsers, fetchRealSupportCases]);

  useEffect(() => {
    if (!isMobileDrawerOpen) return;
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        setIsMobileDrawerOpen(false);
      }
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [isMobileDrawerOpen]);

  const openPendingTaskCount = useMemo(
    () => tasksList.filter((t) => t.status !== 'completed').length,
    [tasksList]
  );

  const unacknowledgedAlertCount = useMemo(
    () => alertsList.filter((a) => a.state === 'unacknowledged').length,
    [alertsList]
  );

  const openSupportCasesCount = useMemo(
    () =>
      supportCasesList.filter(
        (c) => c.status !== 'resolved' && c.status !== 'closed'
      ).length,
    [supportCasesList]
  );

  const currentOverviewStats = useMemo(() => {
    const base = ADMIN_OVERVIEW_WINDOWS[overviewTimeframe];
    if (liveDashboard) {
      return {
        ...base,
        gmvVnd: liveDashboard.transactionVolume || base.gmvVnd,
        activeUsers: liveDashboard.activeUsers || base.activeUsers,
        openTasksCount: liveDashboard.supportCases ?? openPendingTaskCount,
      };
    }
    return {
      ...base,
      openTasksCount: openPendingTaskCount,
    };
  }, [overviewTimeframe, openPendingTaskCount, liveDashboard]);

  const maxOverviewBarValue = useMemo(
    () =>
      currentOverviewStats.dailySeries.reduce((max, item) => {
        const sum = item.personalFlowVnd + item.groupFundFlowVnd;
        return sum > max ? sum : max;
      }, 0),
    [currentOverviewStats]
  );

  const filteredReports = useMemo(() => {
    const q = reportsSearchQuery.trim().toLowerCase();
    return ADMIN_ACTIVITY_REPORTS_FIXTURES.filter((row) => {
      if (reportsDomainFilter !== 'all' && row.domain !== reportsDomainFilter) {
        return false;
      }
      if (!q) return true;
      return (
        row.code.toLowerCase().includes(q) ||
        row.titleVi.toLowerCase().includes(q) ||
        row.titleEn.toLowerCase().includes(q) ||
        row.domainLabelVi.toLowerCase().includes(q) ||
        row.domainLabelEn.toLowerCase().includes(q)
      );
    });
  }, [reportsDomainFilter, reportsSearchQuery]);

  const selectedReport = useMemo(
    () =>
      filteredReports.find((r) => r.id === selectedReportId) ||
      filteredReports[0] ||
      ADMIN_ACTIVITY_REPORTS_FIXTURES[0],
    [filteredReports, selectedReportId]
  );

  const filteredTasks = useMemo(() => {
    const q = taskSearchQuery.trim().toLowerCase();
    return tasksList.filter((task) => {
      if (taskStatusFilter !== 'all' && task.status !== taskStatusFilter) {
        return false;
      }
      if (taskPriorityFilter !== 'all' && task.priority !== taskPriorityFilter) {
        return false;
      }
      if (!q) return true;
      return (
        task.code.toLowerCase().includes(q) ||
        task.titleVi.toLowerCase().includes(q) ||
        task.titleEn.toLowerCase().includes(q) ||
        task.referenceEntity.toLowerCase().includes(q) ||
        task.assignee.toLowerCase().includes(q)
      );
    });
  }, [tasksList, taskStatusFilter, taskPriorityFilter, taskSearchQuery]);

  const selectedTask = useMemo(
    () =>
      filteredTasks.find((t) => t.id === selectedTaskId) ||
      tasksList.find((t) => t.id === selectedTaskId) ||
      filteredTasks[0] ||
      tasksList[0],
    [filteredTasks, tasksList, selectedTaskId]
  );

  const filteredAlerts = useMemo(() => {
    return alertsList.filter((alert) => {
      if (alertSeverityFilter !== 'all' && alert.severity !== alertSeverityFilter) {
        return false;
      }
      if (alertStateFilter !== 'all' && alert.state !== alertStateFilter) {
        return false;
      }
      return true;
    });
  }, [alertsList, alertSeverityFilter, alertStateFilter]);

  const selectedAlert = useMemo(
    () =>
      filteredAlerts.find((a) => a.id === selectedAlertId) ||
      alertsList.find((a) => a.id === selectedAlertId) ||
      filteredAlerts[0] ||
      alertsList[0],
    [filteredAlerts, alertsList, selectedAlertId]
  );

  const handleSelectScreen = (
    screenId:
      | 'overview'
      | 'reports'
      | 'tasks'
      | 'alerts'
      | 'users'
      | 'transactions'
      | 'support'
      | 'system',
    targetSystemTab?: AdminSystemSubTabId
  ) => {
    setActiveScreen(screenId);
    if (screenId === 'transactions') {
      setLedgerSubTab('transactions');
    } else if (screenId === 'system') {
      setSystemSubTab(targetSystemTab || 'broadcast');
    }
    setIsMobileDrawerOpen(false);

    let targetPath = `/admin/${screenId}`;
    if (screenId === 'system' && targetSystemTab) {
      if (targetSystemTab === 'ai_ops') targetPath = '/admin/health';
      else if (targetSystemTab === 'bank_gateways') targetPath = '/admin/banking';
      else if (targetSystemTab === 'config_audit') targetPath = '/admin/audit';
      else targetPath = '/admin/system';
    }
    if (location.pathname !== targetPath) {
      navigate(targetPath);
    }
  };

  const handleLogoutAdmin = async () => {
    setIsAdminAuthenticated(false);
    setIsMobileDrawerOpen(false);
    setIsResetConfirmModalOpen(false);
    setActiveScreen('overview');
    await logout();
    navigate('/login', { replace: true });
  };

  const handleExportReportsCsv = () => {
    const headers = ['Code', 'Period', 'Domain', 'Transactions', 'GrossVolumeVND', 'SuccessRatePct', 'Status'];
    const rows = filteredReports.map((r) => [
      r.code,
      r.periodLabel,
      language === 'en' ? r.domainLabelEn : r.domainLabelVi,
      String(r.txCount),
      String(r.grossVolumeVnd),
      `${r.successRatePct}%`,
      language === 'en' ? r.statusLabelEn : r.statusLabelVi,
    ]);
    const csvContent = [headers, ...rows]
      .map((line) => line.map((cell) => `"${cell.replace(/"/g, '""')}"`).join(','))
      .join('\n');

    try {
      const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `spendwise-admin-reports-${new Date().toISOString().slice(0, 10)}.csv`;
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
      URL.revokeObjectURL(url);
    } catch {
      // Fallback for restricted sandbox environments
    }

    setCsvExportBanner(
      tr(
        `Đã xuất thành công ${filteredReports.length} dòng báo cáo hoạt động (CSV).`,
        `Exported ${filteredReports.length} activity report rows to CSV.`
      )
    );
  };

  const handleUpdateTaskStatus = (
    taskId: string,
    nextStatus: AdminOperationalTask['status']
  ) => {
    setTasksList((prev) =>
      prev.map((item) => (item.id === taskId ? { ...item, status: nextStatus } : item))
    );
  };

  const handleSaveTaskOperatorNote = (e: React.FormEvent) => {
    e.preventDefault();
    const trimmed = taskNoteDraft.trim();
    if (!trimmed || !selectedTask) return;
    setTasksList((prev) =>
      prev.map((item) =>
        item.id === selectedTask.id
          ? {
              ...item,
              operatorNoteVi: trimmed,
              operatorNoteEn: trimmed,
            }
          : item
      )
    );
    setTaskNoteDraft('');
  };

  const handleUpdateAlertState = (
    alertId: string,
    nextState: AdminSystemAlert['state']
  ) => {
    setAlertsList((prev) =>
      prev.map((item) => (item.id === alertId ? { ...item, state: nextState } : item))
    );
  };

  const handleAcknowledgeAllFilteredAlerts = () => {
    const targetIds = new Set(
      filteredAlerts
        .filter((a) => a.state === 'unacknowledged')
        .map((a) => a.id)
    );
    if (targetIds.size === 0) return;
    setAlertsList((prev) =>
      prev.map((item) =>
        targetIds.has(item.id) ? { ...item, state: 'acknowledged' } : item
      )
    );
  };

  const handleResetDemoFixtures = () => {
    setTasksList(ADMIN_OPERATIONAL_TASKS_FIXTURES.map((item) => ({ ...item })));
    setAlertsList(ADMIN_SYSTEM_ALERTS_FIXTURES.map((item) => ({ ...item })));
    setUsersList(
      ADMIN_USER_DIRECTORY_FIXTURES.map((u) => ({
        ...u,
        sessions: u.sessions.map((s) => ({ ...s })),
        relationships: {
          ...u.relationships,
          groupFunds: u.relationships.groupFunds.map((g) => ({ ...g })),
        },
        auditLog: u.auditLog.map((a) => ({ ...a })),
      }))
    );
    setWalletsList(
      ADMIN_SYNTHETIC_WALLETS_FIXTURES.map((w) => ({ ...w }))
    );
    setAuditEventsList(
      ADMIN_INITIAL_GLOBAL_AUDIT_FIXTURES.map((ev) => ({ ...ev }))
    );
    setConfigParamsList(
      ADMIN_SYSTEM_CONFIG_FIXTURES.map((c) => ({ ...c }))
    );
    setSupportCasesList(
      ADMIN_SUPPORT_CASES_FIXTURES.map((c) => ({
        ...c,
        scopedUser: { ...c.scopedUser },
        scopedFund: c.scopedFund ? { ...c.scopedFund } : null,
        refundClaim: c.refundClaim ? { ...c.refundClaim } : null,
        messages: c.messages.map((m) => ({ ...m })),
      }))
    );
    setBroadcastsList(ADMIN_BROADCAST_FIXTURES.map((b) => ({ ...b })));
    setAiPipelinesList(ADMIN_AI_OPERATIONS_FIXTURES.map((p) => ({ ...p })));
    setBankGatewaysList(
      ADMIN_BANK_GATEWAYS_FIXTURES.map((gw) => ({ ...gw }))
    );
    setAdminPassword(DEFAULT_ADMIN_PASSWORD);
    clearAdminDemoFirstOtpVerified();
    setCsvExportBanner(null);
  };

  const activeScreenMeta = useMemo(() => {
    switch (activeScreen) {
      case 'overview':
        return {
          title: tr('Tổng quan', 'Overview'),
          subtitle: tr(
            'Số liệu tổng hợp dòng tiền, đối soát và hàng đợi vận hành (dữ liệu mẫu độc lập)',
            'Synthesized cashflow, settlement health, and operator queues (synthetic fixtures)'
          ),
        };
      case 'reports':
        return {
          title: tr('Báo cáo', 'Activity Reports'),
          subtitle: tr(
            'Đối soát khối lượng giao dịch theo phân hệ Ví P2P, Quỹ nhóm, Ngân hàng và Ngân sách',
            'Channel volume and settlement reconciliation across Wallets, Group Funds, Banking, and Budgets'
          ),
        };
      case 'tasks':
        return {
          title: tr('Việc cần xử lý', 'Operational Tasks'),
          subtitle: tr(
            'Hàng đợi rà soát đối soát ngân hàng, thẩm định khiếu nại quỹ nhóm và hạn mức rút tiền',
            'Back-office queue for bank reference matching, group fund dispute review, and velocity checks'
          ),
        };
      case 'alerts':
        return {
          title: tr('Cảnh báo', 'System & Risk Alerts'),
          subtitle: tr(
            'Giám sát độ trễ cổng thanh toán, bảo mật mã PIN và bộ lập lịch quỹ định kỳ',
            'Gateway latency monitoring, PIN security lockouts, and fixed-fund cycle evaluators'
          ),
        };
      case 'users':
        return {
          title: tr('Người dùng', 'Users & Account Security'),
          subtitle: tr(
            'Tra cứu danh bạ phân trang, kiểm tra hồ sơ/phiên/quan hệ theo phạm vi Case hỗ trợ và thao tác bảo mật',
            'Searchable paginated directory, support-case scoped profile/sessions/relationships, and security actions'
          ),
        };
      case 'transactions':
        return {
          title: tr('Giao dịch', 'Transactions & Ledger'),
          subtitle: tr(
            'Tra cứu bút toán kép phân trang, biên lai đối soát, đóng băng/mở khóa ví mẫu và nhật ký kiểm toán',
            'Paginated double-entry journal, receipt inspector, confirmed synthetic wallet freeze/unfreeze, and audit trail'
          ),
        };
      case 'support':
        return {
          title: tr('Hỗ trợ & khiếu nại', 'Support & Complaints'),
          subtitle: tr(
            'Hàng đợi hồ sơ hỗ trợ, nhận xử lý, chi tiết người dùng & quỹ nhóm theo phạm vi Case và phê duyệt hoàn tiền',
            'Support case queue, self-assignment, case-scoped user & group fund context, and refund workflow'
          ),
        };
      case 'system':
        if (systemSubTab === 'ai_ops') {
          return {
            title: tr('Giám sát AI', 'AI Monitoring'),
            subtitle: tr(
              'Giám sát độ trễ pipeline phân loại giao dịch, dự báo ngân sách và kiểm duyệt an toàn (DEMO)',
              'Monitor categorization latency, budget pacing pipelines, and safety guardrails (DEMO)'
            ),
          };
        }
        if (systemSubTab === 'bank_gateways') {
          return {
            title: tr('Ngân hàng', 'Bank Gateways'),
            subtitle: tr(
              'Giám sát trạng thái cổng ngân hàng liên kết, Napas 247 và điều hướng bảo trì mẫu (DEMO)',
              'Monitor linked bank gateways, Napas 247 fast transfer rails, and maintenance routing (DEMO)'
            ),
          };
        }
        if (systemSubTab === 'config_audit') {
          return {
            title: tr('Nhật ký quản trị', 'Admin Audit & Configuration'),
            subtitle: tr(
              'Nhật ký thao tác vận hành toàn phiên và tham số cấu hình ẩn khóa bí mật (DEMO)',
              'Session-wide operator audit log and redacted system configuration parameters (DEMO)'
            ),
          };
        }
        return {
          title: tr('Thông báo', 'System Notifications'),
          subtitle: tr(
            'Soạn thảo thông báo hệ thống (bản nháp, xem trước, chọn tệp người nhận) và quản lý lịch phát hành (DEMO)',
            'Author system notifications (draft, preview, target audience) and manage DEMO publication queue'
          ),
        };
    }
  }, [activeScreen, systemSubTab, tr]);

  const isEffectiveSidebarExpanded = isNarrowViewport
    ? isMobileDrawerOpen
    : !isSidebarCollapsed;

  const renderSidebarNavigationContent = (isMobileDrawer: boolean) => {
    const activeItems = SIDEBAR_NAV_ITEMS.filter((i) => i.group === 'active');
    // In-flow sidebar is an icon rail on narrow (<768px) so expanding never pushes content;
    // on >=768px it follows isSidebarCollapsed inline; inside the narrow overlay (isMobileDrawer=true) it is expanded with full labels.
    const isCollapsed = !isMobileDrawer && (isNarrowViewport || isSidebarCollapsed);
    const toggleTooltipText = isEffectiveSidebarExpanded
      ? tr('Thu gọn thanh điều hướng bên', 'Collapse sidebar navigation')
      : tr('Mở rộng thanh điều hướng bên', 'Expand sidebar navigation');

    return (
      <div
        className={`flex h-full min-h-0 flex-col justify-between ${
          isCollapsed ? 'px-2 py-2.5' : 'px-3 py-3'
        } select-none overflow-hidden`}
      >
        {/* Brand identity + Explicit Sidebar Toggle / Overlay Collapse */}
        <div
          className={`flex ${
            isCollapsed
              ? 'flex-col items-center gap-2 pb-2.5'
              : 'items-center justify-between gap-2.5 px-1 pb-2.5'
          } shrink-0 border-b border-[var(--line)]`}
        >
          <div
            className={`flex items-center ${
              isCollapsed ? 'justify-center' : 'gap-2.5 min-w-0'
            }`}
          >
            <span
              aria-hidden="true"
              className="inline-flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-gradient-to-br from-[#10b981] via-[#059669] to-[#047857] font-display text-[1rem] font-extrabold text-white shadow-xs"
            >
              S
            </span>
            {!isCollapsed && (
              <div className="min-w-0">
                <div className="font-display text-[0.96875rem] font-extrabold tracking-tight text-[var(--ink)] truncate">
                  SpendWise Admin
                </div>
                <div className="text-[0.71875rem] font-medium text-[var(--ink2)] truncate">
                  {tr('Quản trị vận hành', 'Operations Console')}
                </div>
              </div>
            )}
          </div>

          {!isMobileDrawer && (
            <button
              type="button"
              data-testid="sw-admin-sidebar-toggle"
              aria-expanded={isEffectiveSidebarExpanded}
              aria-label={toggleTooltipText}
              title={toggleTooltipText}
              onClick={handleToggleSidebarCollapsed}
              onMouseEnter={(e) =>
                showFloatingRailTooltip(e.currentTarget, toggleTooltipText)
              }
              onFocus={(e) =>
                showFloatingRailTooltip(e.currentTarget, toggleTooltipText)
              }
              onMouseLeave={hideFloatingRailTooltip}
              onBlur={hideFloatingRailTooltip}
              className="sw-focus-ring sw-ib !h-9 !w-9 !min-h-[36px] !min-w-[36px] shrink-0 cursor-pointer"
            >
              {!isEffectiveSidebarExpanded ? (
                <PanelLeftOpen aria-hidden="true" className="h-4 w-4" />
              ) : (
                <PanelLeftClose aria-hidden="true" className="h-4 w-4" />
              )}
            </button>
          )}

          {isMobileDrawer && (
            <button
              type="button"
              data-testid="sw-admin-mobile-drawer-close"
              onClick={() => setIsMobileDrawerOpen(false)}
              aria-label={tr('Thu gọn thanh điều hướng bên', 'Collapse sidebar overlay')}
              title={tr('Thu gọn thanh điều hướng bên', 'Collapse sidebar overlay')}
              className="sw-focus-ring sw-ib !h-9 !w-9 !min-h-[36px] !min-w-[36px] shrink-0 cursor-pointer"
            >
              <PanelLeftClose aria-hidden="true" className="h-4 w-4" />
            </button>
          )}
        </div>

        {/* Primary navigation organized into 5 clear groups — scrollbar hidden */}
        <nav
          aria-label={tr('Điều hướng Web Admin chính', 'Primary Web Admin Navigation')}
          onScroll={hideFloatingRailTooltip}
          className={`sw-admin-sidebar-nav no-scrollbar flex-1 min-h-0 overflow-y-auto overflow-x-hidden overscroll-contain py-2 ${
            isCollapsed ? 'space-y-1' : 'space-y-2'
          }`}
        >
          {NAV_GROUPS_META.map((grp, grpIdx) => {
            const groupItems = activeItems.filter((i) => i.navGroup === grp.id);
            if (groupItems.length === 0) return null;

            return (
              <div
                key={grp.id}
                data-testid={grp.testId}
                className={isCollapsed ? 'space-y-1' : 'space-y-1'}
              >
                {isCollapsed ? (
                  grpIdx > 0 ? (
                    <div
                      aria-hidden="true"
                      className="mx-2 my-1 border-t border-[var(--line)]"
                    />
                  ) : null
                ) : (
                  <div className="px-2.5 pt-1 pb-0.5 text-[0.6875rem] font-bold tracking-wide text-[var(--ink3)]">
                    {tr(grp.titleVi, grp.titleEn)}
                  </div>
                )}
                {groupItems.map((item) => {
                  const IconComp = item.icon;
                  const isSelected =
                    item.targetScreen === 'system'
                      ? activeScreen === 'system' &&
                        (systemSubTab === item.systemSubTab ||
                          (systemSubTab === 'all' && item.id === 'system'))
                      : activeScreen === item.targetScreen;
                  const label = tr(item.labelVi, item.labelEn);
                  const countValue =
                    item.id === 'tasks'
                      ? openPendingTaskCount
                      : item.id === 'alerts'
                      ? unacknowledgedAlertCount
                      : item.id === 'support'
                      ? openSupportCasesCount
                      : null;
                  const accessibleLabel =
                    countValue !== null && countValue > 0
                      ? `${label} (${countValue})`
                      : label;

                  if (isCollapsed) {
                    return (
                      <button
                        key={item.id}
                        type="button"
                        data-testid={`sw-admin-nav-${item.id}`}
                        aria-current={isSelected ? 'page' : undefined}
                        aria-label={accessibleLabel}
                        title={accessibleLabel}
                        onClick={() => {
                          hideFloatingRailTooltip();
                          handleSelectScreen(
                            item.targetScreen,
                            item.systemSubTab
                          );
                        }}
                        onMouseEnter={(e) =>
                          showFloatingRailTooltip(
                            e.currentTarget,
                            accessibleLabel
                          )
                        }
                        onFocus={(e) =>
                          showFloatingRailTooltip(
                            e.currentTarget,
                            accessibleLabel
                          )
                        }
                        onMouseLeave={hideFloatingRailTooltip}
                        onBlur={hideFloatingRailTooltip}
                        className={`sw-focus-ring relative mx-auto flex h-10 w-10 min-h-[40px] min-w-[40px] sm:h-[42px] sm:w-[42px] sm:min-h-[42px] sm:min-w-[42px] items-center justify-center rounded-xl transition-colors cursor-pointer ${
                          isSelected
                            ? 'bg-gradient-to-r from-[#0f9d6e] to-[#14c58b] text-white shadow-xs'
                            : 'text-[var(--ink2)] hover:bg-[var(--sw-surface-muted)] hover:text-[var(--ink)]'
                        }`}
                      >
                        <IconComp aria-hidden="true" className="h-[18px] w-[18px] shrink-0" />
                        <span className="sr-only">{label}</span>
                        {countValue !== null && countValue > 0 && (
                          <span
                            aria-hidden="true"
                            className={`absolute top-1.5 right-1.5 h-2 w-2 rounded-full ${
                              isSelected ? 'bg-white' : 'bg-[var(--sw-em-d)]'
                            }`}
                          />
                        )}
                      </button>
                    );
                  }

                  return (
                    <button
                      key={item.id}
                      type="button"
                      data-testid={`sw-admin-nav-${item.id}`}
                      aria-current={isSelected ? 'page' : undefined}
                      aria-label={accessibleLabel}
                      onClick={() =>
                        handleSelectScreen(item.targetScreen, item.systemSubTab)
                      }
                      className={`sw-focus-ring flex w-full min-h-[36px] items-center justify-between gap-2.5 rounded-xl px-3 py-1.5 text-left text-[0.84375rem] font-bold transition-colors cursor-pointer whitespace-nowrap ${
                        isSelected
                          ? 'bg-gradient-to-r from-[#0f9d6e] to-[#14c58b] text-white shadow-xs'
                          : 'text-[var(--ink2)] hover:bg-[var(--sw-surface-muted)] hover:text-[var(--ink)]'
                      }`}
                    >
                      <span className="flex items-center gap-2.5 min-w-0 truncate">
                        <IconComp aria-hidden="true" className="h-4 w-4 shrink-0" />
                        <span className="truncate">{label}</span>
                      </span>
                      {countValue !== null && countValue > 0 && (
                        <span
                          className={`text-[0.75rem] font-extrabold tabular-nums shrink-0 ${
                            isSelected ? 'text-white/95' : 'text-[var(--sw-em-d)]'
                          }`}
                        >
                          {countValue}
                        </span>
                      )}
                    </button>
                  );
                })}
              </div>
            );
          })}
        </nav>

        {/* Footer switch back to Mobile App */}
        <div className="shrink-0 pt-2 border-t border-[var(--line)]">
          {isCollapsed ? (
            <button
              type="button"
              data-testid="sw-admin-sidebar-open-mobile-app-btn"
              aria-label={tr('Ứng dụng di động', 'Mobile App')}
              title={tr('Ứng dụng di động', 'Mobile App')}
              onClick={() => {
                hideFloatingRailTooltip();
                if (onOpenMobileApp) {
                  onOpenMobileApp();
                } else {
                  navigate('/forbidden');
                }
              }}
              onMouseEnter={(e) =>
                showFloatingRailTooltip(
                  e.currentTarget,
                  tr('Ứng dụng di động', 'Mobile App')
                )
              }
              onFocus={(e) =>
                showFloatingRailTooltip(
                  e.currentTarget,
                  tr('Ứng dụng di động', 'Mobile App')
                )
              }
              onMouseLeave={hideFloatingRailTooltip}
              onBlur={hideFloatingRailTooltip}
              className="sw-focus-ring mx-auto flex h-10 w-10 min-h-[40px] min-w-[40px] sm:h-[42px] sm:w-[42px] sm:min-h-[42px] sm:min-w-[42px] items-center justify-center rounded-xl text-[var(--ink2)] hover:bg-[var(--sw-surface-muted)] hover:text-[var(--ink)] transition-colors cursor-pointer"
            >
              <Smartphone aria-hidden="true" className="h-[18px] w-[18px] shrink-0" />
              <span className="sr-only">
                {tr('Ứng dụng di động', 'Mobile App')}
              </span>
            </button>
          ) : (
            <button
              type="button"
              data-testid={
                isMobileDrawer
                  ? 'sw-admin-drawer-open-mobile-app-btn'
                  : 'sw-admin-sidebar-open-mobile-app-btn'
              }
              onClick={() => {
                setIsMobileDrawerOpen(false);
                if (onOpenMobileApp) {
                  onOpenMobileApp();
                } else {
                  navigate('/forbidden');
                }
              }}
              className="sw-focus-ring sw-btn ghost sm !min-h-[38px] !py-1.5 w-full justify-center text-[0.8125rem] whitespace-nowrap"
            >
              <Smartphone aria-hidden="true" className="h-4 w-4 shrink-0" />
              <span>{tr('Ứng dụng di động', 'Mobile App')}</span>
            </button>
          )}
        </div>
      </div>
    );
  };

  // Gate all Admin views behind Dedicated Admin Login & PIN Challenge
  if (!isAdminAuthenticated) {
    return (
      <AdminAuthGate
        adminAccount={DEFAULT_ADMIN_ACCOUNT}
        currentAdminPassword={adminPassword}
        onUpdateAdminPassword={setAdminPassword}
        onAuthenticated={() => setIsAdminAuthenticated(true)}
        onExitToMobileApp={onExitWebAdmin || (() => navigate('/login'))}
      />
    );
  }

  return (
    <div
      data-testid="sw-web-admin-shell"
      className="relative min-h-dvh w-full bg-[var(--bg)] text-[var(--ink)] flex flex-row overflow-x-hidden"
    >
      {/* Shared Mint Emerald Mesh Backdrop */}
      <div aria-hidden="true" className="sw-mesh fixed inset-0 pointer-events-none" />

      {/* Permanent Left Navigation Sidebar — Visible at ALL widths (64px-72px icon rail on <768px; 76px rail or 252px-264px expanded inline on >=768px) */}
      <aside
        aria-label={tr('Thanh bên Web Admin', 'Web Admin Sidebar')}
        data-testid="sw-admin-desktop-sidebar"
        data-sidebar-state={isEffectiveSidebarExpanded ? 'expanded' : 'collapsed'}
        className={`flex w-[64px] min-w-[64px] max-w-[64px] sm:w-[72px] sm:min-w-[72px] sm:max-w-[72px] ${
          isSidebarCollapsed
            ? 'md:w-[76px] md:min-w-[76px] md:max-w-[76px]'
            : 'md:w-[252px] md:min-w-[252px] md:max-w-[252px] lg:w-[264px] lg:min-w-[264px] lg:max-w-[264px]'
        } shrink-0 flex-col sticky top-0 h-dvh max-h-dvh overflow-hidden z-20 border-r border-[var(--stroke)] bg-[var(--glass2)] backdrop-blur-xl transition-[width] duration-200 ease-out`}
      >
        {renderSidebarNavigationContent(false)}
      </aside>

      {/* Accessible Unclipped Floating Tooltip for Rail & Toggle at all widths */}
      {railTooltip && (
        <div
          role="tooltip"
          data-testid="sw-admin-rail-tooltip"
          style={{
            top: `${railTooltip.top}px`,
            left: `${railTooltip.left}px`,
          }}
          className="fixed z-50 -translate-y-1/2 pointer-events-none whitespace-nowrap rounded-lg border border-[var(--stroke)] bg-[var(--ink)] px-2.5 py-1 text-[0.75rem] font-bold text-[var(--bg)] shadow-lg"
        >
          {railTooltip.label}
        </div>
      )}

      {/* Labeled Overlay on Narrow Viewports (< 768px) — Expands over screen without pushing content */}
      {isMobileDrawerOpen && (
        <div
          role="dialog"
          aria-modal="true"
          aria-label={tr('Menu điều hướng Web Admin', 'Web Admin Navigation Drawer')}
          data-testid="sw-admin-mobile-drawer"
          className="fixed inset-0 z-50 flex md:hidden"
        >
          <div
            aria-hidden="true"
            onClick={() => setIsMobileDrawerOpen(false)}
            className="fixed inset-0 bg-[var(--sw-overlay)] backdrop-blur-xs"
          />
          <div className="relative z-10 flex h-full max-h-dvh w-[260px] max-w-[85vw] flex-col overflow-hidden border-r border-[var(--stroke)] bg-[var(--sw-sheet-bg)] shadow-2xl">
            {renderSidebarNavigationContent(true)}
          </div>
        </div>
      )}

      {/* Main Flex Child (min-w-0 prevents any child from expanding page width) */}
      <div
        data-testid="sw-admin-main-column"
        className="relative z-10 flex-1 min-w-0 max-w-full flex flex-col overflow-x-hidden"
      >
        {/* Topbar: Wraps cleanly at narrow 320-400px widths; no hamburger replacing left rail */}
        <header className="sticky top-0 z-20 flex flex-wrap items-center justify-between gap-2 border-b border-[var(--stroke)] bg-[var(--glass2)]/90 px-2.5 py-2 sm:px-5 sm:py-2.5 lg:px-7 backdrop-blur-xl min-w-0 max-w-full">
          <div className="flex items-center gap-2 min-w-0 flex-1 basis-[120px]">
            <div className="min-w-0 flex-1">
              <h1
                data-testid="sw-admin-screen-heading"
                className="font-display text-[1rem] sm:text-[1.25rem] font-extrabold tracking-tight text-[var(--ink)] leading-snug break-words"
              >
                {activeScreenMeta.title}
              </h1>
              <p className="hidden md:block text-[0.75rem] text-[var(--ink2)] truncate">
                {activeScreenMeta.subtitle}
              </p>
            </div>
          </div>

          {/* Quick Utilities (Theme/Language/Account as compact icons with tooltips) + Text for Reset & Exit */}
          <div className="flex flex-wrap items-center justify-end gap-1 sm:gap-2 min-w-0 max-w-full">
            <div className="relative group/lang">
              <button
                type="button"
                data-testid="sw-admin-lang-toggle"
                aria-label={
                  language === 'vi'
                    ? tr('Ngôn ngữ: Tiếng Việt (Nhấn để chuyển sang Tiếng Anh)', 'Language: Vietnamese (Switch to English)')
                    : tr('Ngôn ngữ: English (Switch to Tiếng Việt)', 'Language: English (Switch to Vietnamese)')
                }
                title={
                  language === 'vi'
                    ? 'Ngôn ngữ: Tiếng Việt (VI)'
                    : 'Language: English (EN)'
                }
                onClick={() => setLanguage(language === 'vi' ? 'en' : 'vi')}
                className="sw-focus-ring sw-btn ghost sm !h-8 sm:!h-9 !min-h-[32px] sm:!min-h-[36px] !px-2 !py-1 text-[0.72rem] font-extrabold whitespace-nowrap cursor-pointer"
              >
                <Globe aria-hidden="true" className="h-3.5 w-3.5 sm:h-4 sm:w-4 shrink-0" />
                <span>{language === 'vi' ? 'VI' : 'EN'}</span>
              </button>
              <span
                role="tooltip"
                className="pointer-events-none invisible opacity-0 group-hover/lang:visible group-hover/lang:opacity-100 group-focus-within/lang:visible group-focus-within/lang:opacity-100 transition-opacity duration-150 absolute right-0 top-full mt-1.5 z-50 whitespace-nowrap rounded-lg border border-[var(--stroke)] bg-[var(--ink)] px-2.5 py-1 text-[0.72rem] font-bold text-[var(--bg)] shadow-lg"
              >
                {language === 'vi'
                  ? 'Đổi ngôn ngữ (VI → EN)'
                  : 'Switch language (EN → VI)'}
              </span>
            </div>

            <div className="relative group/theme">
              <button
                type="button"
                data-testid="sw-admin-theme-toggle"
                aria-label={
                  resolvedTheme === 'dark'
                    ? tr('Giao diện Tối (Nhấn để chuyển sang Sáng)', 'Dark theme (Switch to Light)')
                    : tr('Giao diện Sáng (Nhấn để chuyển sang Tối)', 'Light theme (Switch to Dark)')
                }
                title={
                  resolvedTheme === 'dark'
                    ? tr('Giao diện: Tối', 'Theme: Dark')
                    : tr('Giao diện: Sáng', 'Theme: Light')
                }
                onClick={() => setTheme(resolvedTheme === 'dark' ? 'light' : 'dark')}
                className="sw-focus-ring sw-btn ghost sm !h-8 !w-8 sm:!h-9 sm:!w-9 !min-h-[32px] !min-w-[32px] sm:!min-h-[36px] sm:!min-w-[36px] !p-0 justify-center cursor-pointer"
              >
                {resolvedTheme === 'dark' ? (
                  <Sun aria-hidden="true" className="h-3.5 w-3.5 sm:h-4 sm:w-4 shrink-0" />
                ) : (
                  <Moon aria-hidden="true" className="h-3.5 w-3.5 sm:h-4 sm:w-4 shrink-0" />
                )}
                <span className="sr-only">
                  {resolvedTheme === 'dark'
                    ? tr('Sáng', 'Light')
                    : tr('Tối', 'Dark')}
                </span>
              </button>
              <span
                role="tooltip"
                className="pointer-events-none invisible opacity-0 group-hover/theme:visible group-hover/theme:opacity-100 group-focus-within/theme:visible group-focus-within/theme:opacity-100 transition-opacity duration-150 absolute right-0 top-full mt-1.5 z-50 whitespace-nowrap rounded-lg border border-[var(--stroke)] bg-[var(--ink)] px-2.5 py-1 text-[0.72rem] font-bold text-[var(--bg)] shadow-lg"
              >
                {resolvedTheme === 'dark'
                  ? tr('Chuyển sang giao diện Sáng', 'Switch to Light theme')
                  : tr('Chuyển sang giao diện Tối', 'Switch to Dark theme')}
              </span>
            </div>

            {/* Destructive / State-Reset Action retains explicit text label */}
            <button
              type="button"
              data-testid="sw-admin-reset-fixtures-btn"
              onClick={() => setIsResetConfirmModalOpen(true)}
              className="sw-focus-ring sw-btn ghost sm !h-8 sm:!h-9 !min-h-[32px] sm:!min-h-[36px] !px-2 sm:!px-2.5 !py-1 text-[0.7rem] sm:text-[0.75rem] whitespace-nowrap cursor-pointer"
            >
              <RotateCcw aria-hidden="true" className="h-3.5 w-3.5 shrink-0" />
              <span className="hidden xl:inline">
                {tr('Khôi phục dữ liệu mẫu', 'Reset Demo Data')}
              </span>
              <span className="xl:hidden">{tr('Khôi phục mẫu', 'Reset Demo')}</span>
            </button>

            <AdminAccountMenu
              adminAccount={DEFAULT_ADMIN_ACCOUNT}
              currentAdminPassword={adminPassword}
              onUpdateAdminPassword={setAdminPassword}
              onRequestResetDemoFixtures={() => setIsResetConfirmModalOpen(true)}
              onLogoutAdmin={handleLogoutAdmin}
            />

            <button
              type="button"
              data-testid="sw-admin-exit-to-app-btn"
              onClick={onExitWebAdmin}
              className="sw-focus-ring sw-btn sw-btn-pri sm !h-8 sm:!h-9 !min-h-[32px] sm:!min-h-[36px] !px-2.5 sm:!px-3 !py-1 text-[0.7rem] sm:text-[0.75rem] whitespace-nowrap shrink-0 cursor-pointer"
            >
              <span>{tr('Về ứng dụng', 'Back to App')}</span>
            </button>
          </div>
        </header>

        {/* Main Workspace Container */}
        <main className="mx-auto w-full max-w-[1360px] min-w-0 flex-1 px-2.5 py-3.5 sm:px-5 sm:py-5 lg:px-7 space-y-4 sm:space-y-6">
          {/* =================================================================
              SCREEN 1: OVERVIEW (TỔNG QUAN VẬN HÀNH)
              ================================================================= */}
          {activeScreen === 'overview' && (
            <div data-testid="sw-admin-screen-overview" className="space-y-4 sm:space-y-6 min-w-0">
              {/* Timeframe Filter Bar — Wraps date and timeframe buttons cleanly */}
              <div className="flex flex-wrap items-center justify-between gap-2.5 min-w-0">
                <div className="text-[0.8125rem] sm:text-[0.875rem] font-semibold text-[var(--ink2)] tabular-nums min-w-0 break-words">
                  {tr('Chu kỳ thống kê:', 'Reporting window:')}{' '}
                  <strong className="font-extrabold text-[var(--ink)]">
                    {language === 'en'
                      ? currentOverviewStats.periodRangeEn
                      : currentOverviewStats.periodRangeVi}
                  </strong>
                </div>

                <div
                  role="group"
                  aria-label={tr('Chọn khung thời gian tổng quan', 'Select overview timeframe')}
                  className="inline-flex flex-wrap items-center rounded-2xl border border-[var(--stroke)] bg-[var(--glass2)] p-1 gap-1 min-w-0 max-w-full"
                >
                  {(['7d', '30d', 'q4_2026'] as const).map((tfId) => {
                    const tf = ADMIN_OVERVIEW_WINDOWS[tfId];
                    const selected = overviewTimeframe === tfId;
                    return (
                      <button
                        key={tfId}
                        type="button"
                        data-testid={`sw-admin-overview-tf-${tfId}`}
                        aria-pressed={selected}
                        onClick={() => setOverviewTimeframe(tfId)}
                        className={`sw-focus-ring min-h-[34px] sm:min-h-[36px] px-2.5 sm:px-3 py-1 rounded-xl text-[0.75rem] sm:text-[0.8125rem] font-extrabold transition-colors cursor-pointer whitespace-nowrap ${
                          selected
                            ? 'bg-gradient-to-r from-[#0f9d6e] to-[#14c58b] text-white shadow-2xs'
                            : 'text-[var(--ink2)] hover:text-[var(--ink)]'
                        }`}
                      >
                        {tr(tf.labelVi, tf.labelEn)}
                      </button>
                    );
                  })}
                </div>
              </div>

              {/* 4 KPI Summary Strip — Single-column at narrow widths, responsive columns on wider viewports */}
              <section
                aria-label={tr('Chỉ số vận hành cốt lõi', 'Core operational KPIs')}
                className="grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-4 min-w-0"
              >
                <div className="sw-card p-3.5 sm:p-4 space-y-1 min-w-0">
                  <div className="text-[0.75rem] font-bold text-[var(--ink2)] leading-snug break-words">
                    {tr('Tổng giá trị giao dịch (GMV)', 'Gross Transaction Volume')}
                  </div>
                  <div
                    data-testid="sw-admin-kpi-gmv"
                    className="font-display text-[1.125rem] sm:text-[1.3125rem] font-extrabold text-[var(--ink)] tabular-nums break-words"
                  >
                    {formatVND(currentOverviewStats.gmvVnd)}
                  </div>
                  <div className="text-[0.75rem] font-semibold text-[var(--sw-em-d)] tabular-nums leading-snug break-words">
                    {language === 'en'
                      ? currentOverviewStats.gmvDeltaLabelEn
                      : currentOverviewStats.gmvDeltaLabelVi}
                  </div>
                </div>

                <div className="sw-card p-3.5 sm:p-4 space-y-1 min-w-0">
                  <div className="text-[0.75rem] font-bold text-[var(--ink2)] leading-snug break-words">
                    {tr('Người dùng hoạt động', 'Active Users')}
                  </div>
                  <div
                    data-testid="sw-admin-kpi-users"
                    className="font-display text-[1.125rem] sm:text-[1.3125rem] font-extrabold text-[var(--ink)] tabular-nums break-words"
                  >
                    {currentOverviewStats.activeUsers.toLocaleString(
                      language === 'en' ? 'en-US' : 'vi-VN'
                    )}
                  </div>
                  <div className="text-[0.75rem] font-medium text-[var(--ink2)] tabular-nums leading-snug break-words">
                    {tr(
                      `${currentOverviewStats.verifiedAuthRatePct}% đã xác thực PIN & Sinh trắc`,
                      `${currentOverviewStats.verifiedAuthRatePct}% PIN & Biometric verified`
                    )}
                  </div>
                </div>

                <div className="sw-card p-3.5 sm:p-4 space-y-1 min-w-0">
                  <div className="text-[0.75rem] font-bold text-[var(--ink2)] leading-snug break-words">
                    {tr('Số dư Quỹ nhóm đang quản lý', 'Active Group Funds Balance')}
                  </div>
                  <div
                    data-testid="sw-admin-kpi-funds"
                    className="font-display text-[1.125rem] sm:text-[1.3125rem] font-extrabold text-[var(--ink)] tabular-nums break-words"
                  >
                    {formatVND(currentOverviewStats.activeGroupFundsBalanceVnd)}
                  </div>
                  <div className="text-[0.75rem] font-medium text-[var(--ink2)] tabular-nums leading-snug break-words">
                    {tr(
                      `${currentOverviewStats.activeGroupFundsCount} quỹ nhóm đang hoạt động`,
                      `${currentOverviewStats.activeGroupFundsCount} active group funds`
                    )}
                  </div>
                </div>

                <div className="sw-card p-3.5 sm:p-4 space-y-1 min-w-0">
                  <div className="text-[0.75rem] font-bold text-[var(--ink2)] leading-snug break-words">
                    {tr('Tỷ lệ đối soát khớp lệnh', 'Settlement Reconciliation Rate')}
                  </div>
                  <div
                    data-testid="sw-admin-kpi-reconciliation"
                    className="font-display text-[1.125rem] sm:text-[1.3125rem] font-extrabold text-[var(--ink)] tabular-nums break-words"
                  >
                    {currentOverviewStats.reconciliationRatePct}%
                  </div>
                  <div className="text-[0.75rem] font-medium text-[var(--ink2)] tabular-nums leading-snug break-words">
                    {tr(
                      `${openPendingTaskCount} tác vụ cần xử lý · ${unacknowledgedAlertCount} cảnh báo mới`,
                      `${openPendingTaskCount} open tasks · ${unacknowledgedAlertCount} unacknowledged alerts`
                    )}
                  </div>
                </div>
              </section>

              {/* Volume Chart + Quick Queue Snapshot */}
              <div className="grid grid-cols-1 gap-4 sm:gap-6 lg:grid-cols-12 items-stretch min-w-0">
                {/* Left 7 cols: Cashflow & Settlement Chart */}
                <section
                  aria-label={tr('Biểu đồ lưu lượng dòng tiền hệ thống', 'System cashflow volume chart')}
                  className="sw-card p-3.5 sm:p-5 lg:col-span-7 min-w-0 flex flex-col justify-between space-y-4 sm:space-y-5"
                >
                  <div className="flex flex-wrap items-baseline justify-between gap-2 min-w-0">
                    <div className="min-w-0">
                      <h2 className="font-display text-[1rem] sm:text-[1.125rem] font-extrabold text-[var(--ink)] break-words">
                        {tr(
                          'Lưu lượng thanh toán Cá nhân vs Quỹ nhóm',
                          'Personal Wallets vs Group Funds Volume'
                        )}
                      </h2>
                      <p className="text-[0.78125rem] sm:text-[0.8125rem] text-[var(--ink2)] break-words">
                        {tr(
                          'Phân tách dòng tiền P2P/Ví cá nhân và dòng tiền Góp/Giải ngân Quỹ nhóm',
                          'Breakdown of personal wallet P2P flow vs shared group fund settlements'
                        )}
                      </p>
                    </div>

                    <div className="flex flex-wrap items-center gap-3 text-[0.75rem] font-bold text-[var(--ink2)]">
                      <span className="inline-flex items-center gap-1.5">
                        <span
                          aria-hidden="true"
                          className="h-2.5 w-2.5 rounded-full bg-emerald-600 shrink-0"
                        />
                        <span>{tr('Ví cá nhân & P2P', 'Personal & P2P')}</span>
                      </span>
                      <span className="inline-flex items-center gap-1.5">
                        <span
                          aria-hidden="true"
                          className="h-2.5 w-2.5 rounded-full bg-amber-500 shrink-0"
                        />
                        <span>{tr('Quỹ nhóm', 'Group Funds')}</span>
                      </span>
                    </div>
                  </div>

                  {/* Bar Columns — Locally scrollable on narrow screens so 7 bars never clip or push page */}
                  <div className="w-full max-w-full overflow-x-auto pb-1">
                    <div
                      role="list"
                      aria-label={tr('Chi tiết lưu lượng theo kỳ', 'Volume breakdown by period')}
                      className="grid grid-cols-7 gap-2 sm:gap-2.5 items-end pt-2 min-w-[360px] sm:min-w-0"
                    >
                      {currentOverviewStats.dailySeries.map((point) => {
                        const totalPointVnd = point.personalFlowVnd + point.groupFundFlowVnd;
                        const personalPct =
                          maxOverviewBarValue > 0
                            ? Math.max(8, Math.round((point.personalFlowVnd / maxOverviewBarValue) * 100))
                            : 0;
                        const fundPct =
                          maxOverviewBarValue > 0
                            ? Math.max(6, Math.round((point.groupFundFlowVnd / maxOverviewBarValue) * 100))
                            : 0;

                        return (
                          <div
                            key={point.dateLabel}
                            role="listitem"
                            className="flex flex-col items-center min-w-0 space-y-1.5"
                          >
                            <span className="text-[0.6875rem] font-bold text-[var(--ink2)] tabular-nums truncate max-w-full">
                              {formatCompactMillionsVND(totalPointVnd, language)}
                            </span>
                            <div className="w-full h-[132px] rounded-xl bg-[var(--glass2)] border border-[var(--stroke)] p-1.5 flex items-end justify-center gap-1">
                              <div
                                title={`${tr('Ví cá nhân & P2P', 'Personal & P2P')}: ${formatVND(point.personalFlowVnd)}`}
                                className="w-1/2 rounded-md bg-gradient-to-t from-emerald-700 to-emerald-500 transition-all duration-200"
                                style={{ height: `${personalPct}%` }}
                              />
                              <div
                                title={`${tr('Quỹ nhóm', 'Group Funds')}: ${formatVND(point.groupFundFlowVnd)}`}
                                className="w-1/2 rounded-md bg-gradient-to-t from-amber-600 to-amber-400 transition-all duration-200"
                                style={{ height: `${fundPct}%` }}
                              />
                            </div>
                            <span className="text-[0.75rem] font-bold text-[var(--ink)] tabular-nums truncate">
                              {point.dateLabel}
                            </span>
                            <span className="text-[0.6875rem] text-[var(--ink3)] tabular-nums truncate">
                              {point.txCount.toLocaleString(language === 'en' ? 'en-US' : 'vi-VN')}{' '}
                              {tr('lệnh', 'tx')}
                            </span>
                          </div>
                        );
                      })}
                    </div>
                  </div>

                  <div className="flex flex-wrap items-center justify-between gap-2 pt-3 border-t border-[var(--line)] text-[0.8125rem] text-[var(--ink2)] min-w-0">
                    <span className="break-words">
                      {tr(
                        'Dữ liệu tổng hợp từ sổ cái mẫu Tháng 10–11/2026.',
                        'Aggregated from Oct–Nov 2026 synthetic ledger fixtures.'
                      )}
                    </span>
                    <button
                      type="button"
                      onClick={() => handleSelectScreen('reports')}
                      className="sw-focus-ring inline-flex items-center gap-1 font-extrabold text-[var(--sw-em-d)] hover:underline cursor-pointer text-left break-words"
                    >
                      <span>{tr('Mở Báo cáo đối soát chi tiết', 'Open Activity Reports')}</span>
                      <ArrowUpRight aria-hidden="true" className="h-4 w-4 shrink-0" />
                    </button>
                  </div>
                </section>

                {/* Right 5 cols: Priority Operational Queue & Active Alerts */}
                <section
                  aria-label={tr('Tác vụ ưu tiên và cảnh báo', 'Priority tasks and active alerts')}
                  className="sw-card p-3.5 sm:p-5 lg:col-span-5 min-w-0 flex flex-col justify-between space-y-4"
                >
                  <div className="flex flex-wrap items-baseline justify-between gap-2 min-w-0">
                    <h2 className="font-display text-[1rem] sm:text-[1.125rem] font-extrabold text-[var(--ink)] break-words">
                      {tr('Hàng đợi cần chú ý ngay', 'Immediate Attention Queue')}
                    </h2>
                    <span className="text-[0.75rem] font-bold text-[var(--ink2)] tabular-nums">
                      {tr(
                        `${openPendingTaskCount} tác vụ · ${unacknowledgedAlertCount} cảnh báo`,
                        `${openPendingTaskCount} tasks · ${unacknowledgedAlertCount} alerts`
                      )}
                    </span>
                  </div>

                  <div className="divide-y divide-[var(--line)] min-w-0">
                    {tasksList
                      .filter((t) => t.status !== 'completed')
                      .slice(0, 2)
                      .map((task) => (
                        <div key={task.id} className="py-3 first:pt-0 space-y-1 min-w-0">
                          <div className="flex flex-wrap items-center justify-between gap-1.5 text-[0.75rem] text-[var(--ink2)] tabular-nums">
                            <span className="font-bold text-[var(--sw-em-d)] break-words">
                              {task.code} · {tr(task.categoryVi, task.categoryEn)}
                            </span>
                            <span>
                              {tr('Hạn:', 'Due:')} {task.dueAt}
                            </span>
                          </div>
                          <div className="text-[0.875rem] font-bold text-[var(--ink)] break-words">
                            {tr(task.titleVi, task.titleEn)}
                          </div>
                          <div className="flex flex-wrap items-center justify-between gap-1.5 pt-0.5 text-[0.8125rem] text-[var(--ink2)] min-w-0">
                            <span className="truncate min-w-0">{task.referenceEntity}</span>
                            {typeof task.amountVnd === 'number' && (
                              <strong className="font-extrabold text-[var(--ink)] tabular-nums shrink-0">
                                {formatVND(task.amountVnd)}
                              </strong>
                            )}
                          </div>
                        </div>
                      ))}

                    {alertsList.slice(0, 2).map((alert) => (
                      <div key={alert.id} className="py-3 last:pb-0 space-y-1 min-w-0">
                        <div className="flex flex-wrap items-center justify-between gap-1.5 text-[0.75rem] text-[var(--ink2)] tabular-nums">
                          <span
                            className={`font-bold break-words ${
                              alert.severity === 'critical'
                                ? 'text-[var(--rose)]'
                                : 'text-[var(--amber)]'
                            }`}
                          >
                            {alert.code} · {tr(alert.serviceVi, alert.serviceEn)}
                          </span>
                          <span>{alert.triggeredAt}</span>
                        </div>
                        <div className="text-[0.875rem] font-bold text-[var(--ink)] break-words">
                          {tr(alert.titleVi, alert.titleEn)}
                        </div>
                        <div className="text-[0.78125rem] text-[var(--ink2)] break-words">
                          {tr(alert.affectedCountLabelVi, alert.affectedCountLabelEn)}
                        </div>
                      </div>
                    ))}
                  </div>

                  <div className="grid grid-cols-1 min-[420px]:grid-cols-2 gap-2 pt-2 border-t border-[var(--line)]">
                    <button
                      type="button"
                      onClick={() => handleSelectScreen('tasks')}
                      className="sw-focus-ring sw-btn ghost sm w-full justify-center whitespace-nowrap"
                    >
                      <span>{tr('Xử lý Tác vụ', 'Manage Tasks')}</span>
                    </button>
                    <button
                      type="button"
                      onClick={() => handleSelectScreen('alerts')}
                      className="sw-focus-ring sw-btn sw-btn-pri sm w-full justify-center whitespace-nowrap"
                    >
                      <span>{tr('Xem Cảnh báo', 'Inspect Alerts')}</span>
                    </button>
                  </div>
                </section>
              </div>
            </div>
          )}

          {/* =================================================================
              SCREEN 2: ACTIVITY REPORTS (BÁO CÁO HOẠT ĐỘNG)
              ================================================================= */}
          {activeScreen === 'reports' && (
            <div data-testid="sw-admin-screen-reports" className="space-y-6 min-w-0">
              {/* Filter & Search Bar */}
              <div className="sw-card p-3.5 sm:p-4 flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between min-w-0">
                <div
                  role="group"
                  aria-label={tr('Lọc báo cáo theo phân hệ', 'Filter reports by domain')}
                  className="flex flex-wrap items-center gap-1.5 min-w-0"
                >
                  {(
                    [
                      { id: 'all', vi: 'Tất cả phân hệ', en: 'All Domains' },
                      { id: 'wallet_p2p', vi: 'Ví & Chuyển P2P', en: 'Wallets & P2P' },
                      { id: 'group_funds', vi: 'Quỹ nhóm', en: 'Group Funds' },
                      { id: 'banking', vi: 'Ngân hàng', en: 'Banking' },
                      { id: 'budget_goals', vi: 'Ngân sách & Mục tiêu', en: 'Budgets & Goals' },
                    ] as const
                  ).map((tab) => {
                    const active = reportsDomainFilter === tab.id;
                    return (
                      <button
                        key={tab.id}
                        type="button"
                        data-testid={`sw-admin-reports-filter-${tab.id}`}
                        aria-pressed={active}
                        onClick={() => setReportsDomainFilter(tab.id)}
                        className={`sw-focus-ring min-h-[36px] px-3 py-1 rounded-xl text-[0.78125rem] sm:text-[0.8125rem] font-extrabold transition-colors cursor-pointer whitespace-nowrap ${
                          active
                            ? 'bg-gradient-to-r from-[#0f9d6e] to-[#14c58b] text-white shadow-2xs'
                            : 'bg-[var(--glass2)] text-[var(--ink2)] hover:text-[var(--ink)] border border-[var(--line)]'
                        }`}
                      >
                        {tr(tab.vi, tab.en)}
                      </button>
                    );
                  })}
                </div>

                <div className="flex flex-col sm:flex-row items-stretch sm:items-center gap-2.5 min-w-0">
                  <div className="sw-search !min-h-[40px] sm:w-[260px] min-w-0">
                    <Search aria-hidden="true" className="h-4 w-4 text-[var(--ink3)] shrink-0" />
                    <input
                      type="search"
                      data-testid="sw-admin-reports-search"
                      value={reportsSearchQuery}
                      onChange={(e) => setReportsSearchQuery(e.target.value)}
                      placeholder={tr('Tìm mã báo cáo, phân hệ...', 'Search report code, domain...')}
                      aria-label={tr('Tìm kiếm báo cáo hoạt động', 'Search activity reports')}
                    />
                  </div>

                  <button
                    type="button"
                    data-testid="sw-admin-reports-export-btn"
                    onClick={handleExportReportsCsv}
                    className="sw-focus-ring sw-btn sw-btn-pri sm !min-h-[40px] whitespace-nowrap shrink-0"
                  >
                    <Download aria-hidden="true" className="h-4 w-4 shrink-0" />
                    <span>{tr('Xuất CSV', 'Export CSV')}</span>
                  </button>
                </div>
              </div>

              {csvExportBanner && (
                <div
                  role="status"
                  data-testid="sw-admin-reports-export-banner"
                  className="rounded-2xl border border-[var(--em)]/40 bg-[var(--sw-success-bg)] px-4 py-3 flex items-center justify-between gap-3 text-[0.84375rem] font-bold text-[var(--sw-em-d)]"
                >
                  <span>{csvExportBanner}</span>
                  <button
                    type="button"
                    onClick={() => setCsvExportBanner(null)}
                    className="sw-focus-ring text-[0.75rem] underline cursor-pointer shrink-0"
                  >
                    {tr('Đóng', 'Dismiss')}
                  </button>
                </div>
              )}

              {/* Reports Table + Detail Inspector */}
              <div className="grid grid-cols-1 gap-6 xl:grid-cols-12 items-start min-w-0">
                <div className="sw-card p-0 overflow-hidden xl:col-span-8 min-w-0">
                  <div className="px-4 py-3.5 sm:px-5 sm:py-4 border-b border-[var(--line)] flex flex-wrap items-center justify-between gap-2">
                    <h2 className="font-display text-[1rem] sm:text-[1.0625rem] font-extrabold text-[var(--ink)] break-words">
                      {tr('Bảng tổng hợp đối soát theo phân hệ', 'Reconciliation Reports by Domain')}
                    </h2>
                    <span className="text-[0.8125rem] font-semibold text-[var(--ink2)] tabular-nums">
                      {filteredReports.length} {tr('báo cáo', 'reports')}
                    </span>
                  </div>

                  {filteredReports.length === 0 ? (
                    <div className="p-8 text-center space-y-2">
                      <p className="text-[0.9375rem] font-bold text-[var(--ink)]">
                        {tr('Không tìm thấy báo cáo phù hợp', 'No matching activity reports')}
                      </p>
                      <p className="text-[0.8125rem] text-[var(--ink2)]">
                        {tr(
                          'Thử xóa bộ lọc phân hệ hoặc từ khóa tìm kiếm.',
                          'Try clearing the domain filter or search query.'
                        )}
                      </p>
                    </div>
                  ) : (
                    <div className="w-full max-w-full overflow-x-auto">
                      <table className="w-full text-left border-collapse">
                        <thead>
                          <tr className="border-b border-[var(--line)] text-[0.75rem] font-bold text-[var(--ink2)] bg-[var(--sw-surface-muted)]">
                            <th className="py-3 px-4 whitespace-nowrap">{tr('Mã · Kỳ', 'Code · Period')}</th>
                            <th className="py-3 px-4">{tr('Báo cáo & Phân hệ', 'Report & Domain')}</th>
                            <th className="py-3 px-4 text-right whitespace-nowrap">{tr('Số lệnh', 'Tx Count')}</th>
                            <th className="py-3 px-4 text-right whitespace-nowrap">{tr('Tổng giá trị', 'Gross Volume')}</th>
                            <th className="py-3 px-4 text-right whitespace-nowrap">{tr('Tỷ lệ khớp', 'Match Rate')}</th>
                            <th className="py-3 px-4 whitespace-nowrap">{tr('Trạng thái', 'Status')}</th>
                          </tr>
                        </thead>
                        <tbody className="divide-y divide-[var(--line)] text-[0.84375rem]">
                          {filteredReports.map((row) => {
                            const isSelected = selectedReport?.id === row.id;
                            return (
                              <tr
                                key={row.id}
                                data-testid={`sw-admin-report-row-${row.id}`}
                                onClick={() => setSelectedReportId(row.id)}
                                className={`cursor-pointer transition-colors ${
                                  isSelected
                                    ? 'bg-[var(--sw-selected-bg)]'
                                    : 'hover:bg-[var(--sw-surface-muted)]'
                                }`}
                              >
                                <td className="py-3.5 px-4 whitespace-nowrap tabular-nums">
                                  <div className="font-extrabold text-[var(--ink)]">{row.code}</div>
                                  <div className="text-[0.75rem] text-[var(--ink2)]">{row.periodLabel}</div>
                                </td>
                                <td className="py-3.5 px-4 min-w-[200px]">
                                  <div className="font-bold text-[var(--ink)]">
                                    {tr(row.titleVi, row.titleEn)}
                                  </div>
                                  <div className="text-[0.75rem] text-[var(--ink2)]">
                                    {tr(row.domainLabelVi, row.domainLabelEn)} ·{' '}
                                    {tr(row.ownerUnitVi, row.ownerUnitEn)}
                                  </div>
                                </td>
                                <td className="py-3.5 px-4 text-right font-bold tabular-nums whitespace-nowrap">
                                  {row.txCount.toLocaleString(language === 'en' ? 'en-US' : 'vi-VN')}
                                </td>
                                <td className="py-3.5 px-4 text-right font-extrabold text-[var(--ink)] tabular-nums whitespace-nowrap">
                                  {formatVND(row.grossVolumeVnd)}
                                </td>
                                <td className="py-3.5 px-4 text-right font-bold tabular-nums whitespace-nowrap">
                                  {row.successRatePct}%
                                </td>
                                <td className="py-3.5 px-4 whitespace-nowrap">
                                  <span
                                    className={`text-[0.8125rem] font-extrabold ${
                                      row.status === 'reconciled'
                                        ? 'text-[var(--sw-em-d)]'
                                        : row.status === 'variance_review'
                                        ? 'text-[var(--amber)]'
                                        : 'text-[var(--sky)]'
                                    }`}
                                  >
                                    {tr(row.statusLabelVi, row.statusLabelEn)}
                                  </span>
                                </td>
                              </tr>
                            );
                          })}
                        </tbody>
                      </table>
                    </div>
                  )}
                </div>

                {/* Selected Report Detail Card */}
                {selectedReport && (
                  <aside
                    aria-label={tr('Chi tiết báo cáo đối soát', 'Selected report details')}
                    data-testid="sw-admin-report-inspector"
                    className="sw-card p-4 sm:p-5 xl:col-span-4 min-w-0 space-y-4"
                  >
                    <div className="space-y-1 border-b border-[var(--line)] pb-3">
                      <div className="text-[0.75rem] font-bold text-[var(--sw-em-d)] tabular-nums">
                        {selectedReport.code} · {selectedReport.periodLabel}
                      </div>
                      <h3 className="font-display text-[1.0625rem] sm:text-[1.125rem] font-extrabold text-[var(--ink)] break-words">
                        {tr(selectedReport.titleVi, selectedReport.titleEn)}
                      </h3>
                      <div className="text-[0.8125rem] text-[var(--ink2)] break-words">
                        {tr(selectedReport.domainLabelVi, selectedReport.domainLabelEn)} ·{' '}
                        {tr(selectedReport.ownerUnitVi, selectedReport.ownerUnitEn)}
                      </div>
                    </div>

                    <div className="space-y-2 text-[0.84375rem]">
                      <div className="sw-kv">
                        <span>{tr('Tổng số giao dịch', 'Total transactions')}</span>
                        <b className="tabular-nums">
                          {selectedReport.txCount.toLocaleString(
                            language === 'en' ? 'en-US' : 'vi-VN'
                          )}
                        </b>
                      </div>
                      <div className="sw-kv">
                        <span>{tr('Tổng giá trị phát sinh', 'Gross volume')}</span>
                        <b className="tabular-nums">{formatVND(selectedReport.grossVolumeVnd)}</b>
                      </div>
                      <div className="sw-kv">
                        <span>{tr('Giá trị đã quyết toán', 'Net settled volume')}</span>
                        <b className="tabular-nums">{formatVND(selectedReport.netSettledVnd)}</b>
                      </div>
                      <div className="sw-kv">
                        <span>{tr('Chênh lệch chờ rà soát', 'Variance under review')}</span>
                        <b
                          className={`tabular-nums ${
                            selectedReport.varianceVnd > 0
                              ? 'text-[var(--amber)]'
                              : 'text-[var(--sw-em-d)]'
                          }`}
                        >
                          {formatVND(selectedReport.varianceVnd)}
                        </b>
                      </div>
                    </div>

                    <div className="space-y-1 pt-2 border-t border-[var(--line)]">
                      <div className="text-[0.75rem] font-bold text-[var(--ink2)]">
                        {tr('Ghi chú kiểm toán nội bộ', 'Audit summary note')}
                      </div>
                      <p className="text-[0.84375rem] leading-relaxed text-[var(--ink)] break-words">
                        {tr(selectedReport.summaryNoteVi, selectedReport.summaryNoteEn)}
                      </p>
                    </div>
                  </aside>
                )}
              </div>
            </div>
          )}

          {/* =================================================================
              SCREEN 3: OPERATIONAL TASKS (TÁC VỤ VẬN HÀNH)
              ================================================================= */}
          {activeScreen === 'tasks' && (
            <div data-testid="sw-admin-screen-tasks" className="space-y-6 min-w-0">
              {/* Filter Bar */}
              <div className="sw-card p-3.5 sm:p-4 flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between min-w-0">
                <div className="flex flex-wrap items-center gap-2 min-w-0">
                  <div
                    role="group"
                    aria-label={tr('Lọc theo trạng thái tác vụ', 'Filter tasks by status')}
                    className="inline-flex flex-wrap items-center gap-1 rounded-2xl border border-[var(--line)] bg-[var(--glass2)] p-1 min-w-0 max-w-full"
                  >
                    {(
                      [
                        { id: 'all', vi: 'Tất cả', en: 'All' },
                        { id: 'pending', vi: 'Chờ xử lý', en: 'Pending' },
                        { id: 'in_review', vi: 'Đang kiểm tra', en: 'In Review' },
                        { id: 'completed', vi: 'Đã hoàn tất', en: 'Completed' },
                      ] as const
                    ).map((st) => {
                      const active = taskStatusFilter === st.id;
                      return (
                        <button
                          key={st.id}
                          type="button"
                          data-testid={`sw-admin-tasks-status-${st.id}`}
                          aria-pressed={active}
                          onClick={() => setTaskStatusFilter(st.id)}
                          className={`sw-focus-ring min-h-[34px] sm:min-h-[36px] px-2.5 sm:px-3 py-1 rounded-xl text-[0.75rem] sm:text-[0.8125rem] font-extrabold transition-colors cursor-pointer whitespace-nowrap ${
                            active
                              ? 'bg-gradient-to-r from-[#0f9d6e] to-[#14c58b] text-white shadow-2xs'
                              : 'text-[var(--ink2)] hover:text-[var(--ink)]'
                          }`}
                        >
                          {tr(st.vi, st.en)}
                        </button>
                      );
                    })}
                  </div>

                  <div
                    role="group"
                    aria-label={tr('Lọc theo độ ưu tiên', 'Filter tasks by priority')}
                    className="inline-flex flex-wrap items-center gap-1 rounded-2xl border border-[var(--line)] bg-[var(--glass2)] p-1 min-w-0 max-w-full"
                  >
                    <SlidersHorizontal
                      aria-hidden="true"
                      className="ml-2 h-3.5 w-3.5 text-[var(--ink3)] shrink-0"
                    />
                    {(
                      [
                        { id: 'all', vi: 'Mọi mức độ', en: 'Any Priority' },
                        { id: 'high', vi: 'Cao', en: 'High' },
                        { id: 'medium', vi: 'Trung bình', en: 'Medium' },
                        { id: 'low', vi: 'Thấp', en: 'Low' },
                      ] as const
                    ).map((pr) => {
                      const active = taskPriorityFilter === pr.id;
                      return (
                        <button
                          key={pr.id}
                          type="button"
                          data-testid={`sw-admin-tasks-priority-${pr.id}`}
                          aria-pressed={active}
                          onClick={() => setTaskPriorityFilter(pr.id)}
                          className={`sw-focus-ring min-h-[34px] sm:min-h-[36px] px-2.5 py-1 rounded-xl text-[0.75rem] sm:text-[0.78125rem] font-bold transition-colors cursor-pointer whitespace-nowrap ${
                            active
                              ? 'bg-[var(--sw-selected-bg)] text-[var(--sw-em-d)]'
                              : 'text-[var(--ink2)] hover:text-[var(--ink)]'
                          }`}
                        >
                          {tr(pr.vi, pr.en)}
                        </button>
                      );
                    })}
                  </div>
                </div>

                <div className="sw-search !min-h-[40px] lg:w-[280px] min-w-0">
                  <Search aria-hidden="true" className="h-4 w-4 text-[var(--ink3)] shrink-0" />
                  <input
                    type="search"
                    data-testid="sw-admin-tasks-search"
                    value={taskSearchQuery}
                    onChange={(e) => setTaskSearchQuery(e.target.value)}
                    placeholder={tr('Tìm mã tác vụ, người phụ trách...', 'Search task code, assignee...')}
                    aria-label={tr('Tìm kiếm tác vụ vận hành', 'Search operational tasks')}
                  />
                </div>
              </div>

              {/* Tasks Queue + Interactive Task Workbench */}
              <div className="grid grid-cols-1 gap-6 xl:grid-cols-12 items-start min-w-0">
                <div className="sw-card p-4 sm:p-5 xl:col-span-7 min-w-0 space-y-3">
                  <div className="flex flex-wrap items-center justify-between gap-2 pb-2 border-b border-[var(--line)]">
                    <h2 className="font-display text-[1rem] sm:text-[1.0625rem] font-extrabold text-[var(--ink)] break-words">
                      {tr('Danh sách tác vụ vận hành', 'Operational Task Queue')}
                    </h2>
                    <span className="text-[0.8125rem] font-semibold text-[var(--ink2)] tabular-nums">
                      {filteredTasks.length} {tr('tác vụ', 'tasks')}
                    </span>
                  </div>

                  {filteredTasks.length === 0 ? (
                    <div className="py-8 text-center space-y-2">
                      <p className="text-[0.9375rem] font-bold text-[var(--ink)]">
                        {tr('Không có tác vụ nào khớp bộ lọc', 'No operational tasks match filters')}
                      </p>
                      <button
                        type="button"
                        onClick={() => {
                          setTaskStatusFilter('all');
                          setTaskPriorityFilter('all');
                          setTaskSearchQuery('');
                        }}
                        className="sw-focus-ring sw-btn ghost sm"
                      >
                        <span>{tr('Xóa bộ lọc', 'Clear filters')}</span>
                      </button>
                    </div>
                  ) : (
                    <div className="divide-y divide-[var(--line)]">
                      {filteredTasks.map((task) => {
                        const isSelected = selectedTask?.id === task.id;
                        const statusText =
                          task.status === 'completed'
                            ? tr('Đã hoàn tất', 'Completed')
                            : task.status === 'in_review'
                            ? tr('Đang kiểm tra', 'In Review')
                            : tr('Chờ xử lý', 'Pending');
                        const priorityText =
                          task.priority === 'high'
                            ? tr('Ưu tiên cao', 'High priority')
                            : task.priority === 'medium'
                            ? tr('Trung bình', 'Medium')
                            : tr('Thấp', 'Low');

                        return (
                          <div
                            key={task.id}
                            data-testid={`sw-admin-task-item-${task.id}`}
                            onClick={() => setSelectedTaskId(task.id)}
                            className={`py-3.5 px-3 -mx-3 rounded-2xl transition-colors cursor-pointer space-y-1.5 ${
                              isSelected
                                ? 'bg-[var(--sw-selected-bg)]'
                                : 'hover:bg-[var(--sw-surface-muted)]'
                            }`}
                          >
                            <div className="flex flex-wrap items-center justify-between gap-2 text-[0.75rem] text-[var(--ink2)] tabular-nums">
                              <span>
                                <strong className="font-extrabold text-[var(--ink)]">
                                  {task.code}
                                </strong>{' '}
                                · {tr(task.categoryVi, task.categoryEn)} ·{' '}
                                <span
                                  className={
                                    task.priority === 'high'
                                      ? 'font-bold text-[var(--rose)]'
                                      : 'font-semibold text-[var(--ink2)]'
                                  }
                                >
                                  {priorityText}
                                </span>
                              </span>
                              <span
                                className={`font-extrabold ${
                                  task.status === 'completed'
                                    ? 'text-[var(--sw-em-d)]'
                                    : task.status === 'in_review'
                                    ? 'text-[var(--sky)]'
                                    : 'text-[var(--amber)]'
                                }`}
                              >
                                {statusText}
                              </span>
                            </div>

                            <div className="text-[0.9375rem] font-bold text-[var(--ink)] break-words">
                              {tr(task.titleVi, task.titleEn)}
                            </div>

                            <div className="flex flex-wrap items-center justify-between gap-2 text-[0.8125rem] text-[var(--ink2)]">
                              <span>
                                {task.assignee} · {tr('Hạn:', 'Due:')} {task.dueAt}
                              </span>
                              {typeof task.amountVnd === 'number' && (
                                <strong className="font-extrabold text-[var(--ink)] tabular-nums">
                                  {formatVND(task.amountVnd)}
                                </strong>
                              )}
                            </div>
                          </div>
                        );
                      })}
                    </div>
                  )}
                </div>

                {/* Task Workbench Panel */}
                {selectedTask && (
                  <aside
                    aria-label={tr('Chi tiết xử lý tác vụ', 'Task execution workbench')}
                    data-testid="sw-admin-task-workbench"
                    className="sw-card p-4 sm:p-5 xl:col-span-5 min-w-0 space-y-4"
                  >
                    <div className="space-y-1 border-b border-[var(--line)] pb-3">
                      <div className="text-[0.75rem] font-bold text-[var(--sw-em-d)] tabular-nums">
                        {selectedTask.code} · {tr(selectedTask.categoryVi, selectedTask.categoryEn)}
                      </div>
                      <h3 className="font-display text-[1.0625rem] sm:text-[1.125rem] font-extrabold text-[var(--ink)] break-words">
                        {tr(selectedTask.titleVi, selectedTask.titleEn)}
                      </h3>
                      <div className="text-[0.8125rem] text-[var(--ink2)] break-words">
                        {selectedTask.referenceEntity}
                      </div>
                    </div>

                    <div className="space-y-1.5 text-[0.84375rem]">
                      <div className="sw-kv">
                        <span>{tr('Người phụ trách', 'Assignee')}</span>
                        <b>{selectedTask.assignee}</b>
                      </div>
                      <div className="sw-kv">
                        <span>{tr('Thời gian tạo · Hạn xử lý', 'Created · Due')}</span>
                        <b className="tabular-nums">
                          {selectedTask.createdAt} → {selectedTask.dueAt}
                        </b>
                      </div>
                      {typeof selectedTask.amountVnd === 'number' && (
                        <div className="sw-kv">
                          <span>{tr('Giá trị liên quan', 'Related amount')}</span>
                          <b className="tabular-nums">{formatVND(selectedTask.amountVnd)}</b>
                        </div>
                      )}
                    </div>

                    <div className="space-y-1.5">
                      <div className="text-[0.75rem] font-bold text-[var(--ink2)]">
                        {tr('Mô tả nghiệp vụ', 'Operational context')}
                      </div>
                      <p className="text-[0.84375rem] leading-relaxed text-[var(--ink)] break-words">
                        {tr(selectedTask.descriptionVi, selectedTask.descriptionEn)}
                      </p>
                    </div>

                    <div className="space-y-1.5 pt-2 border-t border-[var(--line)]">
                      <div className="text-[0.75rem] font-bold text-[var(--ink2)]">
                        {tr('Ghi chú ca trực hiện tại', 'Current operator note')}
                      </div>
                      <p
                        data-testid="sw-admin-task-current-note"
                        className="text-[0.84375rem] font-semibold text-[var(--sw-em-d)] break-words"
                      >
                        {tr(selectedTask.operatorNoteVi, selectedTask.operatorNoteEn)}
                      </p>
                    </div>

                    {/* Update note form */}
                    <form onSubmit={handleSaveTaskOperatorNote} className="space-y-2 pt-1">
                      <label
                        htmlFor="sw-admin-task-note-input"
                        className="block text-[0.75rem] font-bold text-[var(--ink2)]"
                      >
                        {tr('Cập nhật ghi chú xử lý', 'Update resolution note')}
                      </label>
                      <div className="flex flex-wrap items-center gap-2">
                        <input
                          id="sw-admin-task-note-input"
                          type="text"
                          value={taskNoteDraft}
                          onChange={(e) => setTaskNoteDraft(e.target.value)}
                          placeholder={tr(
                            'Nhập biên bản đối soát hoặc mã giao dịch...',
                            'Enter audit note or settlement reference...'
                          )}
                          className="flex-1 min-w-[160px] min-h-[40px] rounded-xl border border-[var(--stroke)] bg-[var(--glass2)] px-3 py-1.5 text-[0.84375rem] font-semibold text-[var(--ink)]"
                        />
                        <button
                          type="submit"
                          data-testid="sw-admin-task-save-note-btn"
                          className="sw-focus-ring sw-btn ghost sm !min-h-[40px] whitespace-nowrap shrink-0"
                        >
                          <span>{tr('Lưu ghi chú', 'Save Note')}</span>
                        </button>
                      </div>
                    </form>

                    {/* Status transition controls */}
                    <div className="flex flex-wrap items-center gap-2 pt-3 border-t border-[var(--line)]">
                      {selectedTask.status !== 'in_review' && (
                        <button
                          type="button"
                          data-testid="sw-admin-task-mark-review-btn"
                          onClick={() => handleUpdateTaskStatus(selectedTask.id, 'in_review')}
                          className="sw-focus-ring sw-btn ghost sm flex-1 justify-center whitespace-nowrap"
                        >
                          <span>{tr('Nhận kiểm tra', 'Move to In Review')}</span>
                        </button>
                      )}
                      {selectedTask.status !== 'completed' ? (
                        <button
                          type="button"
                          data-testid="sw-admin-task-complete-btn"
                          onClick={() => handleUpdateTaskStatus(selectedTask.id, 'completed')}
                          className="sw-focus-ring sw-btn sw-btn-pri sm flex-1 justify-center whitespace-nowrap"
                        >
                          <Check aria-hidden="true" className="h-4 w-4 shrink-0" />
                          <span>{tr('Đánh dấu hoàn tất', 'Mark Completed')}</span>
                        </button>
                      ) : (
                        <button
                          type="button"
                          data-testid="sw-admin-task-reopen-btn"
                          onClick={() => handleUpdateTaskStatus(selectedTask.id, 'pending')}
                          className="sw-focus-ring sw-btn ghost sm flex-1 justify-center whitespace-nowrap"
                        >
                          <span>{tr('Mở lại tác vụ', 'Reopen Task')}</span>
                        </button>
                      )}
                    </div>
                  </aside>
                )}
              </div>
            </div>
          )}

          {/* =================================================================
              SCREEN 4: ALERTS (CẢNH BÁO HỆ THỐNG)
              ================================================================= */}
          {activeScreen === 'alerts' && (
            <div data-testid="sw-admin-screen-alerts" className="space-y-6 min-w-0">
              {/* Filter & Bulk Action Toolbar */}
              <div className="sw-card p-3.5 sm:p-4 flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between min-w-0">
                <div className="flex flex-wrap items-center gap-2 min-w-0">
                  <div
                    role="group"
                    aria-label={tr('Lọc theo mức độ cảnh báo', 'Filter alerts by severity')}
                    className="inline-flex flex-wrap items-center gap-1 rounded-2xl border border-[var(--line)] bg-[var(--glass2)] p-1 min-w-0 max-w-full"
                  >
                    {(
                      [
                        { id: 'all', vi: 'Tất cả mức độ', en: 'All Severities' },
                        { id: 'critical', vi: 'Nghiêm trọng', en: 'Critical' },
                        { id: 'warning', vi: 'Cảnh báo', en: 'Warning' },
                        { id: 'info', vi: 'Thông tin', en: 'Info' },
                      ] as const
                    ).map((sev) => {
                      const active = alertSeverityFilter === sev.id;
                      return (
                        <button
                          key={sev.id}
                          type="button"
                          data-testid={`sw-admin-alerts-sev-${sev.id}`}
                          aria-pressed={active}
                          onClick={() => setAlertSeverityFilter(sev.id)}
                          className={`sw-focus-ring min-h-[34px] sm:min-h-[36px] px-2.5 sm:px-3 py-1 rounded-xl text-[0.75rem] sm:text-[0.8125rem] font-extrabold transition-colors cursor-pointer whitespace-nowrap ${
                            active
                              ? 'bg-gradient-to-r from-[#0f9d6e] to-[#14c58b] text-white shadow-2xs'
                              : 'text-[var(--ink2)] hover:text-[var(--ink)]'
                          }`}
                        >
                          {tr(sev.vi, sev.en)}
                        </button>
                      );
                    })}
                  </div>

                  <div
                    role="group"
                    aria-label={tr('Lọc theo trạng thái xác nhận', 'Filter alerts by state')}
                    className="inline-flex flex-wrap items-center gap-1 rounded-2xl border border-[var(--line)] bg-[var(--glass2)] p-1 min-w-0 max-w-full"
                  >
                    {(
                      [
                        { id: 'all', vi: 'Mọi trạng thái', en: 'Any State' },
                        { id: 'unacknowledged', vi: 'Chưa xác nhận', en: 'Unacknowledged' },
                        { id: 'acknowledged', vi: 'Đã xác nhận', en: 'Acknowledged' },
                        { id: 'resolved', vi: 'Đã xử lý', en: 'Resolved' },
                      ] as const
                    ).map((st) => {
                      const active = alertStateFilter === st.id;
                      return (
                        <button
                          key={st.id}
                          type="button"
                          data-testid={`sw-admin-alerts-state-${st.id}`}
                          aria-pressed={active}
                          onClick={() => setAlertStateFilter(st.id)}
                          className={`sw-focus-ring min-h-[34px] sm:min-h-[36px] px-2.5 py-1 rounded-xl text-[0.75rem] sm:text-[0.78125rem] font-bold transition-colors cursor-pointer whitespace-nowrap ${
                            active
                              ? 'bg-[var(--sw-selected-bg)] text-[var(--sw-em-d)]'
                              : 'text-[var(--ink2)] hover:text-[var(--ink)]'
                          }`}
                        >
                          {tr(st.vi, st.en)}
                        </button>
                      );
                    })}
                  </div>
                </div>

                <button
                  type="button"
                  data-testid="sw-admin-alerts-ack-all-btn"
                  disabled={!filteredAlerts.some((a) => a.state === 'unacknowledged')}
                  onClick={handleAcknowledgeAllFilteredAlerts}
                  className="sw-focus-ring sw-btn sw-btn-pri sm !min-h-[40px] shrink-0"
                >
                  <Check aria-hidden="true" className="h-4 w-4 shrink-0" />
                  <span>
                    {tr('Xác nhận tất cả cảnh báo mới', 'Acknowledge Unacknowledged')}
                  </span>
                </button>
              </div>

              {/* Alerts List + Runbook Detail Panel */}
              <div className="grid grid-cols-1 gap-6 xl:grid-cols-12 items-start min-w-0">
                <div className="sw-card p-5 xl:col-span-7 min-w-0 space-y-3">
                  <div className="flex items-center justify-between gap-2 pb-2 border-b border-[var(--line)]">
                    <h2 className="font-display text-[1.0625rem] font-extrabold text-[var(--ink)]">
                      {tr('Nhật ký cảnh báo hệ thống & rủi ro', 'System & Risk Alert Feed')}
                    </h2>
                    <span className="text-[0.8125rem] font-semibold text-[var(--ink2)] tabular-nums">
                      {filteredAlerts.length} {tr('cảnh báo', 'alerts')}
                    </span>
                  </div>

                  {filteredAlerts.length === 0 ? (
                    <div className="py-8 text-center space-y-2">
                      <p className="text-[0.9375rem] font-bold text-[var(--ink)]">
                        {tr('Không có cảnh báo nào khớp bộ lọc', 'No alerts match the current filter')}
                      </p>
                      <button
                        type="button"
                        onClick={() => {
                          setAlertSeverityFilter('all');
                          setAlertStateFilter('all');
                        }}
                        className="sw-focus-ring sw-btn ghost sm"
                      >
                        <span>{tr('Hiện tất cả cảnh báo', 'Show All Alerts')}</span>
                      </button>
                    </div>
                  ) : (
                    <div className="divide-y divide-[var(--line)]">
                      {filteredAlerts.map((alert) => {
                        const isSelected = selectedAlert?.id === alert.id;
                        const sevText =
                          alert.severity === 'critical'
                            ? tr('Nghiêm trọng', 'Critical')
                            : alert.severity === 'warning'
                            ? tr('Cảnh báo', 'Warning')
                            : tr('Thông tin', 'Info');
                        const stateText =
                          alert.state === 'unacknowledged'
                            ? tr('Chưa xác nhận', 'Unacknowledged')
                            : alert.state === 'acknowledged'
                            ? tr('Đã xác nhận', 'Acknowledged')
                            : tr('Đã xử lý', 'Resolved');

                        return (
                          <div
                            key={alert.id}
                            data-testid={`sw-admin-alert-item-${alert.id}`}
                            onClick={() => setSelectedAlertId(alert.id)}
                            className={`py-3.5 px-3 -mx-3 rounded-2xl transition-colors cursor-pointer space-y-1.5 ${
                              isSelected
                                ? 'bg-[var(--sw-selected-bg)]'
                                : 'hover:bg-[var(--sw-surface-muted)]'
                            }`}
                          >
                            <div className="flex flex-wrap items-center justify-between gap-2 text-[0.75rem] tabular-nums">
                              <span>
                                <strong
                                  className={
                                    alert.severity === 'critical'
                                      ? 'font-extrabold text-[var(--rose)]'
                                      : alert.severity === 'warning'
                                      ? 'font-extrabold text-[var(--amber)]'
                                      : 'font-extrabold text-[var(--sky)]'
                                  }
                                >
                                  {alert.code} · {sevText}
                                </strong>{' '}
                                <span className="text-[var(--ink2)]">
                                  · {tr(alert.serviceVi, alert.serviceEn)}
                                </span>
                              </span>

                              <span
                                className={`font-extrabold ${
                                  alert.state === 'resolved'
                                    ? 'text-[var(--sw-em-d)]'
                                    : alert.state === 'acknowledged'
                                    ? 'text-[var(--sky)]'
                                    : 'text-[var(--rose)]'
                                }`}
                              >
                                {stateText}
                              </span>
                            </div>

                            <div className="text-[0.9375rem] font-bold text-[var(--ink)]">
                              {tr(alert.titleVi, alert.titleEn)}
                            </div>

                            <div className="flex flex-wrap items-center justify-between gap-2 text-[0.8125rem] text-[var(--ink2)] tabular-nums">
                              <span>{tr(alert.affectedCountLabelVi, alert.affectedCountLabelEn)}</span>
                              <span>{alert.triggeredAt}</span>
                            </div>
                          </div>
                        );
                      })}
                    </div>
                  )}
                </div>

                {/* Alert Runbook Inspector */}
                {selectedAlert && (
                  <aside
                    aria-label={tr('Chi tiết cảnh báo và hướng dẫn xử lý', 'Alert details and runbook')}
                    data-testid="sw-admin-alert-inspector"
                    className="sw-card p-5 xl:col-span-5 min-w-0 space-y-4"
                  >
                    <div className="space-y-1 border-b border-[var(--line)] pb-3">
                      <div className="text-[0.75rem] font-bold text-[var(--ink2)] tabular-nums">
                        {selectedAlert.code} · {selectedAlert.triggeredAt}
                      </div>
                      <h3 className="font-display text-[1.125rem] font-extrabold text-[var(--ink)]">
                        {tr(selectedAlert.titleVi, selectedAlert.titleEn)}
                      </h3>
                      <div className="text-[0.8125rem] text-[var(--ink2)]">
                        {tr(selectedAlert.serviceVi, selectedAlert.serviceEn)} ·{' '}
                        {tr(
                          selectedAlert.affectedCountLabelVi,
                          selectedAlert.affectedCountLabelEn
                        )}
                      </div>
                    </div>

                    <div className="space-y-1.5">
                      <div className="text-[0.75rem] font-bold text-[var(--ink2)]">
                        {tr('Chi tiết sự kiện', 'Incident detail')}
                      </div>
                      <p className="text-[0.84375rem] leading-relaxed text-[var(--ink)]">
                        {tr(selectedAlert.detailVi, selectedAlert.detailEn)}
                      </p>
                    </div>

                    <div className="space-y-1.5 pt-2 border-t border-[var(--line)]">
                      <div className="text-[0.75rem] font-bold text-[var(--ink2)]">
                        {tr('Hướng dẫn vận hành (Runbook)', 'Recommended operator action')}
                      </div>
                      <p className="text-[0.84375rem] font-semibold text-[var(--sw-em-d)] leading-relaxed">
                        {tr(selectedAlert.runbookHintVi, selectedAlert.runbookHintEn)}
                      </p>
                    </div>

                    <div className="flex flex-wrap items-center gap-2 pt-3 border-t border-[var(--line)]">
                      {selectedAlert.state === 'unacknowledged' && (
                        <button
                          type="button"
                          data-testid="sw-admin-alert-ack-btn"
                          onClick={() =>
                            handleUpdateAlertState(selectedAlert.id, 'acknowledged')
                          }
                          className="sw-focus-ring sw-btn ghost sm flex-1 justify-center whitespace-nowrap"
                        >
                          <span>{tr('Xác nhận đã tiếp nhận', 'Acknowledge Alert')}</span>
                        </button>
                      )}
                      {selectedAlert.state !== 'resolved' ? (
                        <button
                          type="button"
                          data-testid="sw-admin-alert-resolve-btn"
                          onClick={() => handleUpdateAlertState(selectedAlert.id, 'resolved')}
                          className="sw-focus-ring sw-btn sw-btn-pri sm flex-1 justify-center whitespace-nowrap"
                        >
                          <Check aria-hidden="true" className="h-4 w-4 shrink-0" />
                          <span>{tr('Đánh dấu đã xử lý', 'Mark Resolved')}</span>
                        </button>
                      ) : (
                        <button
                          type="button"
                          data-testid="sw-admin-alert-reopen-btn"
                          onClick={() =>
                            handleUpdateAlertState(selectedAlert.id, 'unacknowledged')
                          }
                          className="sw-focus-ring sw-btn ghost sm flex-1 justify-center whitespace-nowrap"
                        >
                          <span>{tr('Mở lại cảnh báo', 'Reopen Alert')}</span>
                        </button>
                      )}
                    </div>
                  </aside>
                )}
              </div>
            </div>
          )}

          {/* =================================================================
              SCREEN 5: USERS (ADMIN A2 — DANH BẠ & BẢO MẬT NGƯỜI DÙNG)
              ================================================================= */}
          {activeScreen === 'users' && (
            <AdminUsersSection
              users={usersList}
              onUpdateUsers={setUsersList}
              onAppendAuditEvent={handleAppendGlobalAudit}
            />
          )}

          {/* =================================================================
              SCREEN 6: TRANSACTIONS & LEDGER (ADMIN A3)
              ================================================================= */}
          {activeScreen === 'transactions' && (
            <AdminTransactionsLedgerSection
              transactions={ADMIN_LEDGER_TRANSACTIONS_FIXTURES}
              wallets={walletsList}
              onUpdateWallets={setWalletsList}
              auditEvents={auditEventsList}
              onAppendAuditEvent={handleAppendGlobalAudit}
              configParams={configParamsList}
              onUpdateConfigParams={setConfigParamsList}
              activeSubTab={ledgerSubTab}
              onChangeSubTab={(tab) => setLedgerSubTab(tab)}
            />
          )}

          {/* =================================================================
              SCREEN 7: SUPPORT & COMPLAINTS CASE QUEUE (ADMIN A4)
              ================================================================= */}
          {activeScreen === 'support' && (
            <AdminSupportSection
              cases={supportCasesList}
              onUpdateCases={setSupportCasesList}
              onAppendAuditEvent={handleAppendGlobalAudit}
            />
          )}

          {/* =================================================================
              SCREEN 8: SYSTEM, NOTIFICATIONS, AI OPS, BANK GATEWAYS & AUDIT
              ================================================================= */}
          {activeScreen === 'system' && (
            <AdminSystemOperationsSection
              broadcasts={broadcastsList}
              onUpdateBroadcasts={setBroadcastsList}
              aiPipelines={aiPipelinesList}
              onUpdateAiPipelines={setAiPipelinesList}
              bankGateways={bankGatewaysList}
              onUpdateBankGateways={setBankGatewaysList}
              configParams={configParamsList}
              onUpdateConfigParams={setConfigParamsList}
              auditEvents={auditEventsList}
              onAppendAuditEvent={handleAppendGlobalAudit}
              activeSubTab={systemSubTab}
              onChangeSubTab={setSystemSubTab}
            />
          )}
        </main>
      </div>

      {/* Confirmation Dialog for Resetting DEMO Fixtures */}
      {isResetConfirmModalOpen && (
        <div
          role="dialog"
          aria-modal="true"
          aria-labelledby="sw-admin-reset-confirm-title"
          data-testid="sw-admin-reset-confirm-modal"
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4 backdrop-blur-xs"
        >
          <div className="sw-card w-full max-w-md max-h-[calc(100dvh-2rem)] flex flex-col overflow-hidden p-0 shadow-xl bg-[var(--sw-sheet-bg)]">
            <div className="shrink-0 flex items-start justify-between gap-3 p-5 border-b border-[var(--line)]">
              <div className="flex items-center gap-2.5">
                <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-[var(--amber-s)] text-[var(--amber)]">
                  <AlertTriangle aria-hidden="true" className="h-4 w-4" />
                </div>
                <h2
                  id="sw-admin-reset-confirm-title"
                  className="font-display text-[1.0625rem] font-extrabold text-[var(--ink)]"
                >
                  {tr(
                    'Xác nhận khôi phục dữ liệu mẫu',
                    'Confirm Resetting Demo Data'
                  )}
                </h2>
              </div>
              <button
                type="button"
                onClick={() => setIsResetConfirmModalOpen(false)}
                aria-label={tr('Đóng hộp thoại', 'Close dialog')}
                className="sw-focus-ring sw-ib !h-8 !w-8 !min-h-[32px] !min-w-[32px]"
              >
                <X aria-hidden="true" className="h-4 w-4" />
              </button>
            </div>

            <div className="p-5 text-[0.84375rem] text-[var(--ink2)] leading-relaxed space-y-2">
              <p className="font-semibold text-[var(--ink)]">
                {tr(
                  'Bạn có chắc chắn muốn đặt lại toàn bộ dữ liệu vận hành mẫu (DEMO) về trạng thái ban đầu không?',
                  'Are you sure you want to reset all synthetic DEMO operational data back to initial state?'
                )}
              </p>
              <p>
                {tr(
                  'Thao tác này chỉ làm mới dữ liệu mẫu trong bảng quản trị (tác vụ, cảnh báo, người dùng mẫu, ví mẫu, khiếu nại và thông báo), hoàn toàn không ảnh hưởng tới dữ liệu người dùng trên ứng dụng di động.',
                  'This only resets Admin DEMO fixtures and does not affect the Mobile User session or data.'
                )}
              </p>
            </div>

            <div className="shrink-0 flex items-center justify-end gap-2 px-5 py-3.5 border-t border-[var(--line)] bg-[var(--sw-sheet-bg)]">
              <button
                type="button"
                data-testid="sw-admin-reset-cancel-btn"
                onClick={() => setIsResetConfirmModalOpen(false)}
                className="sw-focus-ring sw-btn ghost sm"
              >
                <span>{tr('Hủy bỏ', 'Cancel')}</span>
              </button>
              <button
                type="button"
                data-testid="sw-admin-reset-confirm-submit-btn"
                onClick={() => {
                  handleResetDemoFixtures();
                  setIsResetConfirmModalOpen(false);
                }}
                className="sw-focus-ring sw-btn sw-btn-pri sm"
              >
                <RotateCcw aria-hidden="true" className="h-3.5 w-3.5" />
                <span>
                  {tr('Xác nhận khôi phục', 'Confirm Reset')}
                </span>
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
