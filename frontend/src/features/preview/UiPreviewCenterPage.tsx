import React, { Component, ErrorInfo, useState, useEffect } from 'react';
import { NavLink, useSearchParams } from 'react-router-dom';
import {
  Sparkles,
  Play,
  RotateCcw,
  Sun,
  Moon,
  Layers,
  Compass,
  ArrowRight,
  ShieldCheck,
  Eye,
  CheckCircle2,
  AlertCircle,
  HelpCircle,
  Pause,
  Wifi,
  WifiOff,
  Clock,
  XCircle,
  Check,
  Radio,
  UserCheck,
  ExternalLink,
  User,
  Shield,
} from 'lucide-react';
import { SpendWiseSplash } from '../../components/brand/SpendWiseSplash';
import { SpendWiseSLoading, SpendWiseLoadingStatus } from '../../components/ui/SpendWiseSLoading';
import { GlobalNetworkBanner } from '../../components/layout/GlobalNetworkBanner';
import { networkStatusService } from '../../services/networkStatusService';
import { OnboardingPreviewModal } from '../../components/onboarding/OnboardingPreviewModal';
import { TransferSkeleton } from '../../components/skeletons/TransferSkeleton';
import { BudgetSkeleton } from '../../components/skeletons/BudgetSkeleton';
import { GoalsSkeleton } from '../../components/skeletons/GoalsSkeleton';
import { FriendsSkeleton } from '../../components/skeletons/FriendsSkeleton';
import { GroupsSkeleton } from '../../components/skeletons/GroupsSkeleton';
import { NotificationsSkeleton } from '../../components/skeletons/NotificationsSkeleton';
import { SupportDisputesSkeleton } from '../../components/skeletons/SupportDisputesSkeleton';
import { AdminOverviewSkeleton } from '../../components/skeletons/AdminOverviewSkeleton';
import { RecurringTransactionsSkeleton } from '../../components/skeletons/RecurringTransactionsSkeleton';
import { ProfileSettingsSkeleton } from '../../components/skeletons/ProfileSettingsSkeleton';
import { Spinner } from '../../components/ui/Spinner';
import { Button } from '../../components/ui/Button';
import { useTheme } from '../../providers/ThemeProvider';
import { useI18n } from '../../i18n/I18nContext';
import { useAuth } from '../../providers/AuthProvider';
import { cn } from '../../utils/cn';

// Existing production pages for real layout comparison
import { TransferPage } from '../transfer/TransferPage';
import { BudgetPage } from '../budget/BudgetPage';
import { GoalsPage } from '../goals/GoalsPage';
import { FriendsPage } from '../friends/FriendsPage';
import { GroupsPage } from '../groups/GroupsPage';
import { NotificationsPage } from '../notifications/NotificationsPage';
import { SupportDisputesPage } from '../support/SupportDisputesPage';
import { SettingsPage } from '../settings/SettingsPage';
import { SettingsRedesignPreview } from '../settings/SettingsRedesignPreview';
import { FinancialRedesignBatch2Preview } from './FinancialRedesignBatch2Preview';
import { SpendWiseMobilePreviewCenter } from '../mobile-preview/SpendWiseMobilePreviewCenter';
import { DashboardPage } from '../dashboard/DashboardPage';
import { WalletPage } from '../wallet/WalletPage';
import { TransactionsPage } from '../transactions/TransactionsPage';
import { RecurringTransactionsTab } from '../transactions/components/RecurringTransactionsTab';

// Admin Feature Pages
import { AdminOverviewPage } from '../admin/AdminOverviewPage';
import { AdminUsersPage } from '../admin/AdminUsersPage';
import { AdminTransactionsPage } from '../admin/AdminTransactionsPage';
import { AdminLedgerPage } from '../admin/AdminLedgerPage';
import { AdminRiskPage } from '../admin/AdminRiskPage';
import { AdminDisputesPage } from '../admin/AdminDisputesPage';
import { AdminSupportPage } from '../admin/AdminSupportPage';
import { AdminAuditPage } from '../admin/AdminAuditPage';
import { AdminBankingPage } from '../admin/AdminBankingPage';
import { AdminReconciliationPage } from '../admin/AdminReconciliationPage';
import { AdminHealthPage } from '../admin/AdminHealthPage';
import { AdminConfigurationPage } from '../admin/AdminConfigurationPage';
import { AdminFeatureFlagsPage } from '../admin/AdminFeatureFlagsPage';

interface PreviewErrorBoundaryProps {
  children?: React.ReactNode;
  pageName: string;
  route: string;
}

interface PreviewErrorBoundaryState {
  hasError: boolean;
  error: Error | null;
}

class PreviewErrorBoundary extends Component<PreviewErrorBoundaryProps, PreviewErrorBoundaryState> {
  constructor(props: PreviewErrorBoundaryProps) {
    super(props);
    this.state = { hasError: false, error: null };
  }

  static getDerivedStateFromError(error: Error): PreviewErrorBoundaryState {
    return { hasError: true, error };
  }

  componentDidCatch(error: Error, errorInfo: ErrorInfo) {
    console.error('PreviewErrorBoundary caught an error in preview component:', error, errorInfo);
  }

  componentDidUpdate(prevProps: PreviewErrorBoundaryProps) {
    if (prevProps.route !== this.props.route && this.state.hasError) {
      this.setState({ hasError: false, error: null });
    }
  }

  render() {
    if (this.state.hasError) {
      return (
        <div className="p-6 rounded-2xl bg-amber-50/70 dark:bg-amber-950/20 border border-amber-200 dark:border-amber-800/60 text-slate-900 dark:text-slate-100 space-y-4">
          <div className="flex items-start gap-3">
            <div className="h-9 w-9 rounded-xl bg-amber-100 dark:bg-amber-900/40 text-amber-600 dark:text-amber-400 flex items-center justify-center shrink-0">
              <AlertCircle className="h-5 w-5" />
            </div>
            <div>
              <h4 className="text-sm font-bold text-slate-900 dark:text-white">
                Chế độ xem trước: Trang {this.props.pageName} cần phiên dữ liệu người dùng thực tế
              </h4>
              <p className="text-xs text-slate-600 dark:text-slate-300 mt-1 leading-relaxed">
                Component này truy cập dữ liệu ngữ cảnh (ví dụ: ví tài khoản, hồ sơ cá nhân) hiện chưa có trong phiên khách. Để xem giao diện đầy đủ với dữ liệu cá nhân của bạn, vui lòng mở trực tiếp route chính thức hoặc đăng nhập.
              </p>
            </div>
          </div>

          <div className="flex flex-wrap items-center gap-3 pt-2">
            <NavLink
              to={this.props.route}
              className="inline-flex items-center gap-1.5 px-3.5 py-1.5 rounded-xl bg-emerald-600 hover:bg-emerald-700 text-white text-xs font-bold transition shadow-xs"
            >
              <span>Mở route chính thức ({this.props.route})</span>
              <ExternalLink className="h-3.5 w-3.5" />
            </NavLink>
            <button
              type="button"
              onClick={() => this.setState({ hasError: false, error: null })}
              className="px-3.5 py-1.5 rounded-xl border border-slate-300 dark:border-slate-700 bg-white dark:bg-slate-800 text-xs font-bold text-slate-700 dark:text-slate-200 hover:bg-slate-50 dark:hover:bg-slate-700 cursor-pointer transition"
            >
              Thử render lại
            </button>
          </div>

          {this.state.error?.message && (
            <div className="p-2.5 rounded-lg bg-white/60 dark:bg-black/30 text-[11px] font-mono text-slate-500 dark:text-slate-400 break-all border border-slate-200/50 dark:border-slate-800/50">
              Thông tin chi tiết lỗi: {this.state.error.message}
            </div>
          )}
        </div>
      );
    }
    return this.props.children;
  }
}

export const UiPreviewCenterPage: React.FC = () => {
  const { theme, resolvedTheme, setTheme } = useTheme();
  const { locale, setLocale } = useI18n();
  const { currentUser, isAuthenticated } = useAuth();
  const [searchParams, setSearchParams] = useSearchParams();

  const handleToggleTheme = () => {
    setTheme(resolvedTheme === 'dark' ? 'light' : 'dark');
  };

  const handleToggleLocale = () => {
    setLocale(locale === 'vi-VN' ? 'en-US' : 'vi-VN');
  };

  // Active Main Preview Section:
  // mobile: SPENDWISE MOBILE USER PREVIEW (Default - Priority 1)
  // splash: Splash Intro
  // auth: S-Loading Lifecycle
  // skeletons: Page Skeletons
  // onboarding: Onboarding (4 steps)
  const initialTab = (searchParams.get('tab') as any) || 'mobile';
  const [activeTab, setActiveTabState] = useState<'mobile' | 'splash' | 'auth' | 'skeletons' | 'onboarding'>(
    ['mobile', 'splash', 'auth', 'skeletons', 'onboarding'].includes(initialTab) ? initialTab : 'mobile'
  );

  const setActiveTab = (tab: 'mobile' | 'splash' | 'auth' | 'skeletons' | 'onboarding') => {
    setActiveTabState(tab);
    setSearchParams({ tab });
  };

  // Splash Preview State
  const [splashReplayKey, setSplashReplayKey] = useState<number>(1);
  const [splashCompleted, setSplashCompleted] = useState<boolean>(false);

  // Skeletons Selector State: Grouped by User and Admin areas, default to User -> Dashboard
  const [skeletonScope, setSkeletonScope] = useState<'user' | 'admin'>('user');
  const [selectedSkeletonRoute, setSelectedSkeletonRoute] = useState<string>('dashboard');
  const [viewMode, setViewMode] = useState<'skeleton' | 'live' | 'split' | 'stacked'>('stacked');

  // Auth Loading Lifecycle & S-Loading Preview State
  const [sLoadingStatus, setSLoadingStatus] = useState<SpendWiseLoadingStatus>('pending');
  const [authScenario, setAuthScenario] = useState<'login' | 'register'>('login');
  const [authButtonState, setAuthButtonState] = useState<'normal' | 'login_loading' | 'register_loading'>('login_loading');
  const [sLoadingPaused, setSLoadingPaused] = useState<boolean>(false);
  const [sReplayKey, setSReplayKey] = useState<number>(1);
  const [activeSequence, setActiveSequence] = useState<string | null>(null);

  // Global Network Banner Preview State
  const [networkBannerMock, setNetworkBannerMock] = useState<'online' | 'offline' | 'reconnected' | null>(null);

  // Scenario copy helper for perfect Light/Dark parity
  const getScenarioCopy = (status: SpendWiseLoadingStatus, scenario: 'login' | 'register') => {
    if (scenario === 'login') {
      switch (status) {
        case 'pending':
          return {
            label: 'Đang xác thực tài khoản...',
            sublabel: 'Đang kiểm tra chứng chỉ bảo mật và đồng bộ số liệu',
          };
        case 'success':
          return {
            label: 'Đăng nhập thành công!',
            sublabel: 'Chào mừng bạn quay trở lại SpendWise',
          };
        case 'error':
          return {
            label: 'Đăng nhập không thành công',
            sublabel: 'Email hoặc mật khẩu không chính xác, vui lòng thử lại',
          };
        case 'offline':
          return {
            label: 'Không có kết nối mạng',
            sublabel: 'Vui lòng kiểm tra lại đường truyền Internet của bạn',
          };
        case 'timeout':
          return {
            label: 'Hết thời gian yêu cầu',
            sublabel: 'Máy chủ phản hồi chậm, vui lòng thử lại sau giây lát',
          };
      }
    } else {
      switch (status) {
        case 'pending':
          return {
            label: 'Đang khởi tạo tài khoản...',
            sublabel: 'Đang tạo sổ cái cá nhân và cấu hình danh mục',
          };
        case 'success':
          return {
            label: 'Khởi tạo thành công!',
            sublabel: 'Sổ cái cá nhân SpendWise của bạn đã sẵn sàng',
          };
        case 'error':
          return {
            label: 'Đăng ký không thành công',
            sublabel: 'Email này đã được sử dụng hoặc thông tin chưa hợp lệ',
          };
        case 'offline':
          return {
            label: 'Không có kết nối mạng',
            sublabel: 'Vui lòng kiểm tra lại Wi-Fi hoặc dữ liệu di động',
          };
        case 'timeout':
          return {
            label: 'Hết thời gian yêu cầu',
            sublabel: 'Máy chủ phản hồi chậm, vui lòng thử lại sau giây lát',
          };
      }
    }
  };

  // Sequence runner helper
  const runSequence = (targetStatus: SpendWiseLoadingStatus, sequenceName: string, durationMs: number = 1800) => {
    setActiveSequence(sequenceName);
    setSLoadingStatus('pending');
    setSReplayKey((k) => k + 1);

    setTimeout(() => {
      setSLoadingStatus(targetStatus);
      setTimeout(() => {
        setActiveSequence(null);
      }, targetStatus === 'success' ? 600 : 1200);
    }, durationMs);
  };

  // Comprehensive User & Admin Page Registry with honest Skeleton implementation tracking
  const SKELETON_ITEMS = [
    // ================= USER PAGES =================
    {
      id: 'dashboard',
      scope: 'user' as const,
      name: 'Dashboard (Tổng quan tài chính)',
      route: '/dashboard',
      requiresAuth: true,
      requiresRole: 'User' as const,
      hasSkeleton: true,
      component: <TransferSkeleton />,
      liveComponent: <DashboardPage />,
    },
    {
      id: 'wallet',
      scope: 'user' as const,
      name: 'Wallet (Ví & Tài khoản)',
      route: '/wallet',
      requiresAuth: true,
      requiresRole: 'User' as const,
      hasSkeleton: true,
      component: <TransferSkeleton />,
      liveComponent: <WalletPage />,
    },
    {
      id: 'transactions',
      scope: 'user' as const,
      name: 'Transactions (Lịch sử giao dịch)',
      route: '/transactions',
      requiresAuth: true,
      requiresRole: 'User' as const,
      hasSkeleton: false,
      component: null,
      liveComponent: <TransactionsPage />,
    },
    {
      id: 'transfer',
      scope: 'user' as const,
      name: 'Transfer (Chuyển tiền nhanh)',
      route: '/transfer',
      requiresAuth: true,
      requiresRole: 'User' as const,
      hasSkeleton: true,
      component: <TransferSkeleton />,
      liveComponent: <TransferPage />,
    },
    {
      id: 'budget',
      scope: 'user' as const,
      name: 'Budgets (Ngân sách chi tiêu)',
      route: '/budget',
      requiresAuth: true,
      requiresRole: 'User' as const,
      hasSkeleton: true,
      component: <BudgetSkeleton />,
      liveComponent: <BudgetPage />,
    },
    {
      id: 'goals',
      scope: 'user' as const,
      name: 'Goals (Mục tiêu tài chính)',
      route: '/goals',
      requiresAuth: true,
      requiresRole: 'User' as const,
      hasSkeleton: true,
      component: <GoalsSkeleton />,
      liveComponent: <GoalsPage />,
    },
    {
      id: 'friends',
      scope: 'user' as const,
      name: 'Friends (Bạn bè & Danh bạ)',
      route: '/friends',
      requiresAuth: true,
      requiresRole: 'User' as const,
      hasSkeleton: true,
      component: <FriendsSkeleton />,
      liveComponent: <FriendsPage />,
    },
    {
      id: 'groups',
      scope: 'user' as const,
      name: 'Groups / Funds (Nhóm quỹ chia sẻ)',
      route: '/groups',
      requiresAuth: true,
      requiresRole: 'User' as const,
      hasSkeleton: true,
      component: <GroupsSkeleton />,
      liveComponent: <GroupsPage />,
    },
    {
      id: 'notifications',
      scope: 'user' as const,
      name: 'Notifications (Trung tâm thông báo)',
      route: '/notifications',
      requiresAuth: true,
      requiresRole: 'User' as const,
      hasSkeleton: true,
      component: <NotificationsSkeleton />,
      liveComponent: <NotificationsPage />,
    },
    {
      id: 'recurring',
      scope: 'user' as const,
      name: 'Recurring (Giao dịch định kỳ)',
      route: '/transactions?tab=recurring',
      requiresAuth: true,
      requiresRole: 'User' as const,
      hasSkeleton: true,
      component: <RecurringTransactionsSkeleton />,
      liveComponent: (
        <div className="p-4 bg-white dark:bg-slate-900 rounded-2xl">
          <RecurringTransactionsTab />
        </div>
      ),
    },
    {
      id: 'support',
      scope: 'user' as const,
      name: 'Support / Disputes (Hỗ trợ & Khiếu nại)',
      route: '/support',
      requiresAuth: true,
      requiresRole: 'User' as const,
      hasSkeleton: true,
      component: <SupportDisputesSkeleton />,
      liveComponent: <SupportDisputesPage />,
    },
    {
      id: 'settings',
      scope: 'user' as const,
      name: 'Profile / Settings (Cài đặt tài khoản)',
      route: '/settings',
      requiresAuth: true,
      requiresRole: 'User' as const,
      hasSkeleton: true,
      component: <ProfileSettingsSkeleton />,
      liveComponent: <SettingsPage />,
    },

    // ================= ADMIN PAGES =================
    {
      id: 'admin_overview',
      scope: 'admin' as const,
      name: 'Overview (Tổng quan quản trị)',
      route: '/admin/overview',
      requiresAuth: true,
      requiresRole: 'SystemAdmin' as const,
      hasSkeleton: true,
      component: <AdminOverviewSkeleton />,
      liveComponent: <AdminOverviewPage />,
    },
    {
      id: 'admin_users',
      scope: 'admin' as const,
      name: 'Users (Quản lý người dùng)',
      route: '/admin/users',
      requiresAuth: true,
      requiresRole: 'SystemAdmin' as const,
      hasSkeleton: false,
      component: null,
      liveComponent: <AdminUsersPage />,
    },
    {
      id: 'admin_transactions',
      scope: 'admin' as const,
      name: 'Transactions (Giám sát giao dịch hệ thống)',
      route: '/admin/transactions',
      requiresAuth: true,
      requiresRole: 'SystemAdmin' as const,
      hasSkeleton: false,
      component: null,
      liveComponent: <AdminTransactionsPage />,
    },
    {
      id: 'admin_ledger',
      scope: 'admin' as const,
      name: 'Ledger (Sổ cái hệ thống)',
      route: '/admin/ledger',
      requiresAuth: true,
      requiresRole: 'SystemAdmin' as const,
      hasSkeleton: false,
      component: null,
      liveComponent: <AdminLedgerPage />,
    },
    {
      id: 'admin_risk',
      scope: 'admin' as const,
      name: 'Risk & Fraud (Phòng ngừa rủi ro & gian lận)',
      route: '/admin/risk',
      requiresAuth: true,
      requiresRole: 'SystemAdmin' as const,
      hasSkeleton: false,
      component: null,
      liveComponent: <AdminRiskPage />,
    },
    {
      id: 'admin_disputes',
      scope: 'admin' as const,
      name: 'Disputes (Xử lý tranh chấp & khiếu nại)',
      route: '/admin/disputes',
      requiresAuth: true,
      requiresRole: 'SystemAdmin' as const,
      hasSkeleton: false,
      component: null,
      liveComponent: <AdminDisputesPage />,
    },
    {
      id: 'admin_support',
      scope: 'admin' as const,
      name: 'Support (Quản trị hỗ trợ người dùng)',
      route: '/admin/support',
      requiresAuth: true,
      requiresRole: 'SystemAdmin' as const,
      hasSkeleton: false,
      component: null,
      liveComponent: <AdminSupportPage />,
    },
    {
      id: 'admin_audit',
      scope: 'admin' as const,
      name: 'Audit Logs (Nhật ký kiểm toán)',
      route: '/admin/audit',
      requiresAuth: true,
      requiresRole: 'SystemAdmin' as const,
      hasSkeleton: false,
      component: null,
      liveComponent: <AdminAuditPage />,
    },
    {
      id: 'admin_banking',
      scope: 'admin' as const,
      name: 'Banking (Giám sát cổng kết nối ngân hàng)',
      route: '/admin/banking',
      requiresAuth: true,
      requiresRole: 'SystemAdmin' as const,
      hasSkeleton: false,
      component: null,
      liveComponent: <AdminBankingPage />,
    },
    {
      id: 'admin_reconciliation',
      scope: 'admin' as const,
      name: 'Reconciliation (Đối soát số dư & giao dịch)',
      route: '/admin/reconciliation',
      requiresAuth: true,
      requiresRole: 'SystemAdmin' as const,
      hasSkeleton: false,
      component: null,
      liveComponent: <AdminReconciliationPage />,
    },
    {
      id: 'admin_health',
      scope: 'admin' as const,
      name: 'System Health (Sức khỏe hệ thống & dịch vụ)',
      route: '/admin/health',
      requiresAuth: true,
      requiresRole: 'SystemAdmin' as const,
      hasSkeleton: false,
      component: null,
      liveComponent: <AdminHealthPage />,
    },
    {
      id: 'admin_configuration',
      scope: 'admin' as const,
      name: 'Configuration (Cấu hình tham số hệ thống)',
      route: '/admin/configuration',
      requiresAuth: true,
      requiresRole: 'SystemAdmin' as const,
      hasSkeleton: false,
      component: null,
      liveComponent: <AdminConfigurationPage />,
    },
    {
      id: 'admin_feature_flags',
      scope: 'admin' as const,
      name: 'Feature Flags (Quản lý cờ tính năng)',
      route: '/admin/feature-flags',
      requiresAuth: true,
      requiresRole: 'SystemAdmin' as const,
      hasSkeleton: false,
      component: null,
      liveComponent: <AdminFeatureFlagsPage />,
    },
  ];

  // Filter items by active scope (User or Admin)
  const currentScopeItems = SKELETON_ITEMS.filter((item) => item.scope === skeletonScope);

  // Active selected item guaranteed to be inside current scope
  const currentSkeleton =
    currentScopeItems.find((s) => s.id === selectedSkeletonRoute) || currentScopeItems[0];

  const handleScopeChange = (newScope: 'user' | 'admin') => {
    setSkeletonScope(newScope);
    const firstItemOfScope = SKELETON_ITEMS.find((item) => item.scope === newScope);
    if (firstItemOfScope) {
      setSelectedSkeletonRoute(firstItemOfScope.id);
    }
  };

  return (
    <div className="min-h-screen bg-[#F8F9FA] dark:bg-[#0B1120] text-slate-900 dark:text-slate-100 transition-colors duration-200">
      {/* Top Dev Banner */}
      <header className="sticky top-0 z-40 bg-white/90 dark:bg-slate-900/90 backdrop-blur-md border-b border-slate-200 dark:border-slate-800 px-4 sm:px-8 py-3.5 flex flex-wrap items-center justify-between gap-4">
        <div className="flex items-center gap-3">
          <div className="h-8 w-8 rounded-xl bg-emerald-600 text-white flex items-center justify-center font-bold text-sm shadow-xs">
            <Sparkles className="h-4 w-4" />
          </div>
          <div>
            <div className="flex items-center gap-2">
              <h1 className="text-base font-bold text-slate-900 dark:text-white leading-tight">
                SpendWise UI Enhancement Preview Center
              </h1>
              <span className="px-2 py-0.5 rounded-full bg-amber-100 dark:bg-amber-950/60 text-amber-800 dark:text-amber-300 font-mono text-[10px] font-bold border border-amber-300 dark:border-amber-800">
                Xem trước
              </span>
            </div>
            <p className="text-[11px] text-slate-500 dark:text-slate-400">
              Môi trường xem trước độc lập cho Splash, S-Loading Lifecycle, Global Network Status & Skeletons
            </p>
          </div>
        </div>

        {/* Global Controls: Theme & Language */}
        <div className="flex items-center gap-2">
          <Button
            variant="outline"
            size="sm"
            onClick={handleToggleTheme}
            className="text-xs font-semibold gap-1.5 cursor-pointer"
          >
            {resolvedTheme === 'dark' ? <Sun className="h-3.5 w-3.5 text-amber-400" /> : <Moon className="h-3.5 w-3.5 text-slate-600" />}
            <span>{resolvedTheme === 'dark' ? 'Light Mode' : 'Dark Mode'}</span>
          </Button>

          <Button
            variant="outline"
            size="sm"
            onClick={handleToggleLocale}
            className="text-xs font-semibold font-mono cursor-pointer"
          >
            {locale === 'vi-VN' ? 'EN' : 'VI'}
          </Button>

          <NavLink
            to="/dashboard"
            className="text-xs font-bold text-emerald-600 dark:text-emerald-400 hover:underline px-2 py-1"
          >
            Về Dashboard →
          </NavLink>
        </div>
      </header>

      {/* Global Network Banner Sandbox Preview */}
      <GlobalNetworkBanner mockState={networkBannerMock} />

      {/* Main Container */}
      <main className="max-w-7xl mx-auto px-4 sm:px-8 py-6 space-y-6">
        {/* Navigation Tabs (Mobile Priority First) */}
        <div className="flex border-b border-slate-200 dark:border-slate-800 gap-2 pb-2 overflow-x-auto no-scrollbar">
          {[
            { id: 'mobile', label: '📱 SPENDWISE MOBILE USER PREVIEW (Chính thức)' },
            { id: 'splash', label: '1. Splash Intro & Logo Animation' },
            { id: 'auth', label: '2. S-Loading Lifecycle & Network Status' },
            { id: 'skeletons', label: '3. Mobile Page Skeletons (Bố cục riêng từng trang)' },
            { id: 'onboarding', label: '4. First-Time Onboarding (4 Bước)' },
          ].map((tab) => (
            <button
              key={tab.id}
              type="button"
              onClick={() => setActiveTab(tab.id as any)}
              className={cn(
                'px-4 py-2.5 rounded-xl text-xs sm:text-sm font-bold transition-all whitespace-nowrap cursor-pointer',
                activeTab === tab.id
                  ? 'bg-emerald-600 text-white shadow-xs'
                  : 'text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white hover:bg-slate-100 dark:hover:bg-slate-800'
              )}
            >
              {tab.label}
            </button>
          ))}
        </div>

        {/* ================= SECTION 0: SPENDWISE MOBILE USER PREVIEW ================= */}
        {activeTab === 'mobile' && (
          <div className="space-y-4 animate-in fade-in-50 duration-200">
            <SpendWiseMobilePreviewCenter />
          </div>
        )}

        {/* ================= SECTION 1: SPLASH INTRO ================= */}
        {activeTab === 'splash' && (
          <div className="space-y-4 animate-in fade-in-50 duration-200">
            <div className="p-4 rounded-2xl bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 flex flex-wrap items-center justify-between gap-3">
              <div className="space-y-0.5">
                <h3 className="text-sm font-bold text-slate-900 dark:text-white">
                  Màn hình Splash Khởi động (Cinematic 2.1s)
                </h3>
                <p className="text-xs text-slate-500 dark:text-slate-400">
                  Timeline 2.1s: Lún nhẹ (0.2s) ➔ Phóng to 1.4x ➔ Vệt sáng trắng-cyan 20° quét qua ➔ Vầng sáng #28E0F0 & vòng viền mảnh ➔ Tan biến (2.1s). Bấm trực tiếp vào logo hoặc Replay.
                </p>
              </div>

              <div className="flex items-center gap-2">
                <Button
                  size="sm"
                  onClick={() => {
                    setSplashCompleted(false);
                    setSplashReplayKey((k) => k + 1);
                  }}
                  className="gap-1.5 text-xs font-bold bg-emerald-600 hover:bg-emerald-700 text-white cursor-pointer"
                >
                  <RotateCcw className="h-3.5 w-3.5" />
                  <span>Phát lại animation (Replay)</span>
                </Button>

                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => setActiveTab('onboarding')}
                  className="gap-1.5 text-xs font-semibold cursor-pointer"
                >
                  <span>Chuyển sang Onboarding Preview →</span>
                </Button>
              </div>
            </div>

            {/* Splash Stage Viewport */}
            <div className="rounded-3xl border border-slate-200 dark:border-slate-800 overflow-hidden shadow-sm h-[480px]">
              <SpendWiseSplash
                replayKey={splashReplayKey}
                onComplete={() => setSplashCompleted(true)}
                autoDismiss={false}
              />
            </div>

            {splashCompleted && (
              <div className="p-3.5 rounded-xl bg-emerald-50 dark:bg-emerald-950/40 border border-emerald-200 dark:border-emerald-800 flex items-center justify-between text-xs text-emerald-800 dark:text-emerald-300">
                <span className="flex items-center gap-2 font-semibold">
                  <CheckCircle2 className="h-4 w-4 text-emerald-600" />
                  Animation đã hoàn thành chu kỳ và sẵn sàng điều hướng tiếp.
                </span>
                <button
                  type="button"
                  onClick={() => setActiveTab('onboarding')}
                  className="font-bold underline hover:text-emerald-900 dark:hover:text-emerald-200 cursor-pointer"
                >
                  Mở Onboarding ngay
                </button>
              </div>
            )}
          </div>
        )}

        {/* ================= SECTION 2: AUTH S-LOADING LIFECYCLE & NETWORK STATUS ================= */}
        {activeTab === 'auth' && (
          <div className="space-y-6 animate-in fade-in-50 duration-200">
            {/* Control Panel: S-Loading Lifecycle & Network Status */}
            <div className="p-5 rounded-2xl bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 space-y-4">
              <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
                <div className="space-y-1">
                  <div className="flex items-center gap-2">
                    <h3 className="text-sm font-bold text-slate-900 dark:text-white">
                      Bảng Điều Khiển Vòng Đời S-Loading & Kết Nối Mạng
                    </h3>
                    <span className="text-[10px] px-2 py-0.5 rounded-full bg-emerald-100 dark:bg-emerald-950/60 text-emerald-800 dark:text-emerald-300 font-bold border border-emerald-300 dark:border-emerald-800">
                      Lifecycle State Machine
                    </span>
                  </div>
                  <p className="text-xs text-slate-500 dark:text-slate-400">
                    Thử nghiệm chuyển tiếp trực tiếp giữa các trạng thái: Đang xử lý ➔ Thành công (✓) / Lỗi (✕) / Mất mạng / Hết thời gian.
                  </p>
                </div>

                <div className="flex flex-wrap items-center gap-2">
                  <span className="text-xs font-bold text-slate-500 uppercase tracking-wider hidden sm:inline">Kịch bản:</span>
                  <div className="flex items-center p-0.5 rounded-xl bg-slate-100 dark:bg-slate-800 border border-slate-200 dark:border-slate-700">
                    <button
                      type="button"
                      onClick={() => setAuthScenario('login')}
                      className={cn(
                        'px-3 py-1.5 rounded-lg text-xs font-bold transition-all whitespace-nowrap cursor-pointer',
                        authScenario === 'login'
                          ? 'bg-white dark:bg-slate-900 text-emerald-600 dark:text-emerald-400 shadow-xs'
                          : 'text-slate-600 dark:text-slate-400'
                      )}
                    >
                      Đăng nhập (Login)
                    </button>
                    <button
                      type="button"
                      onClick={() => setAuthScenario('register')}
                      className={cn(
                        'px-3 py-1.5 rounded-lg text-xs font-bold transition-all whitespace-nowrap cursor-pointer',
                        authScenario === 'register'
                          ? 'bg-white dark:bg-slate-900 text-emerald-600 dark:text-emerald-400 shadow-xs'
                          : 'text-slate-600 dark:text-slate-400'
                      )}
                    >
                      Đăng ký (Register)
                    </button>
                  </div>

                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => setSLoadingPaused(!sLoadingPaused)}
                    className="gap-1.5 text-xs font-semibold whitespace-nowrap cursor-pointer"
                  >
                    {sLoadingPaused ? <Play className="h-3.5 w-3.5 text-emerald-600" /> : <Pause className="h-3.5 w-3.5 text-amber-500" />}
                    <span>{sLoadingPaused ? 'Tiếp tục' : 'Tạm dừng'}</span>
                  </Button>

                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => {
                      setSReplayKey((k) => k + 1);
                      setSLoadingPaused(false);
                      setSLoadingStatus('pending');
                    }}
                    className="gap-1.5 text-xs font-semibold whitespace-nowrap cursor-pointer"
                  >
                    <RotateCcw className="h-3.5 w-3.5" />
                    <span>Đặt lại</span>
                  </Button>
                </div>
              </div>

              {/* Group 1: Trạng thái tĩnh (Direct State) */}
              <div className="p-3.5 rounded-xl bg-slate-50 dark:bg-slate-800/60 border border-slate-200/80 dark:border-slate-800 space-y-2">
                <div className="flex items-center justify-between">
                  <span className="text-[11px] font-bold uppercase tracking-wider text-slate-600 dark:text-slate-400">
                    1. Trạng thái tĩnh (Direct State)
                  </span>
                  <span className="text-[10px] text-slate-400">Chọn trạng thái để xem visual phản hồi</span>
                </div>
                <div className="flex flex-wrap gap-2">
                  {[
                    { id: 'pending', label: 'Đang xử lý (Pending)', icon: Sparkles, color: 'hover:border-emerald-500' },
                    { id: 'success', label: 'Thành công (✓)', icon: Check, color: 'hover:border-emerald-500' },
                    { id: 'error', label: 'Thất bại (✕)', icon: XCircle, color: 'hover:border-rose-500' },
                    { id: 'offline', label: 'Mất mạng (Offline)', icon: WifiOff, color: 'hover:border-amber-500' },
                    { id: 'timeout', label: 'Hết giờ (Timeout)', icon: Clock, color: 'hover:border-amber-500' },
                  ].map((s) => {
                    const Icon = s.icon;
                    const isSelected = sLoadingStatus === s.id;
                    return (
                      <button
                        key={s.id}
                        type="button"
                        onClick={() => {
                          setSLoadingStatus(s.id as SpendWiseLoadingStatus);
                          setSReplayKey((k) => k + 1);
                        }}
                        className={cn(
                          'px-3 py-1.5 rounded-xl text-xs font-bold transition-all flex items-center gap-1.5 whitespace-nowrap cursor-pointer border',
                          isSelected
                            ? 'bg-slate-900 text-white dark:bg-emerald-600 dark:text-white border-transparent shadow-xs'
                            : 'bg-white dark:bg-slate-800 text-slate-700 dark:text-slate-300 border-slate-200 dark:border-slate-700',
                          s.color
                        )}
                      >
                        <Icon className="h-3.5 w-3.5" />
                        <span>{s.label}</span>
                      </button>
                    );
                  })}
                </div>
              </div>

              {/* Group 2: Mô phỏng chuỗi chuyển tiếp (Sequence Simulation) */}
              <div className="p-3.5 rounded-xl bg-slate-50 dark:bg-slate-800/60 border border-slate-200/80 dark:border-slate-800 space-y-2">
                <div className="flex items-center justify-between">
                  <span className="text-[11px] font-bold uppercase tracking-wider text-slate-600 dark:text-slate-400">
                    2. Mô phỏng chuỗi chuyển tiếp (Sequence Transition)
                  </span>
                  <span className="text-[10px] text-slate-400">Chạy animation từ Pending ➔ Kết quả</span>
                </div>
                <div className="flex flex-wrap gap-2">
                  <Button
                    size="sm"
                    variant="outline"
                    disabled={activeSequence !== null}
                    onClick={() => runSequence('success', 'Pending → Success', 1500)}
                    className={cn(
                      'gap-1.5 text-xs font-bold whitespace-nowrap cursor-pointer bg-white dark:bg-slate-800',
                      activeSequence === 'Pending → Success' && 'border-emerald-500 bg-emerald-50 dark:bg-emerald-950/40 text-emerald-700 dark:text-emerald-300'
                    )}
                  >
                    <Check className="h-3.5 w-3.5 text-emerald-600" />
                    <span>Pending ➔ Thành công (✓)</span>
                  </Button>

                  <Button
                    size="sm"
                    variant="outline"
                    disabled={activeSequence !== null}
                    onClick={() => runSequence('error', 'Pending → Error', 1500)}
                    className={cn(
                      'gap-1.5 text-xs font-bold whitespace-nowrap cursor-pointer bg-white dark:bg-slate-800',
                      activeSequence === 'Pending → Error' && 'border-rose-500 bg-rose-50 dark:bg-rose-950/40 text-rose-700 dark:text-rose-300'
                    )}
                  >
                    <XCircle className="h-3.5 w-3.5 text-rose-600" />
                    <span>Pending ➔ Thất bại (✕)</span>
                  </Button>

                  <Button
                    size="sm"
                    variant="outline"
                    disabled={activeSequence !== null}
                    onClick={() => runSequence('offline', 'Pending → Offline', 1500)}
                    className={cn(
                      'gap-1.5 text-xs font-bold whitespace-nowrap cursor-pointer bg-white dark:bg-slate-800',
                      activeSequence === 'Pending → Offline' && 'border-amber-500 bg-amber-50 dark:bg-amber-950/40 text-amber-700 dark:text-amber-300'
                    )}
                  >
                    <WifiOff className="h-3.5 w-3.5 text-amber-600" />
                    <span>Pending ➔ Mất mạng</span>
                  </Button>

                  <Button
                    size="sm"
                    variant="outline"
                    disabled={activeSequence !== null}
                    onClick={() => runSequence('timeout', 'Pending → Timeout', 1500)}
                    className={cn(
                      'gap-1.5 text-xs font-bold whitespace-nowrap cursor-pointer bg-white dark:bg-slate-800',
                      activeSequence === 'Pending → Timeout' && 'border-amber-500 bg-amber-50 dark:bg-amber-950/40 text-amber-700 dark:text-amber-300'
                    )}
                  >
                    <Clock className="h-3.5 w-3.5 text-amber-600" />
                    <span>Pending ➔ Hết thời gian</span>
                  </Button>
                </div>
              </div>

              {/* Group 3: Global Network Status Simulation */}
              <div className="p-3.5 rounded-xl bg-slate-50 dark:bg-slate-800/60 border border-slate-200/80 dark:border-slate-800 space-y-2">
                <div className="flex items-center justify-between">
                  <span className="text-[11px] font-bold uppercase tracking-wider text-slate-600 dark:text-slate-400">
                    3. Banner Mạng Toàn Cục (Global Network Banner)
                  </span>
                  <span className="text-[10px] text-slate-400">Xuất hiện ngay dưới top header khi mất/có lại mạng</span>
                </div>
                <div className="flex flex-wrap gap-2">
                  <button
                    type="button"
                    onClick={() => setNetworkBannerMock(null)}
                    className={cn(
                      'px-3 py-1.5 rounded-xl text-xs font-bold transition-all border whitespace-nowrap cursor-pointer',
                      networkBannerMock === null
                        ? 'bg-slate-900 text-white dark:bg-slate-700 dark:text-white border-transparent'
                        : 'bg-white dark:bg-slate-800 text-slate-600 dark:text-slate-400 border-slate-200 dark:border-slate-700'
                    )}
                  >
                    Theo mạng thật (Live System)
                  </button>
                  <button
                    type="button"
                    onClick={() => setNetworkBannerMock('offline')}
                    className={cn(
                      'px-3 py-1.5 rounded-xl text-xs font-bold transition-all flex items-center gap-1.5 border whitespace-nowrap cursor-pointer',
                      networkBannerMock === 'offline'
                        ? 'bg-amber-600 text-white border-transparent shadow-xs'
                        : 'bg-white dark:bg-slate-800 text-amber-600 dark:text-amber-400 border-amber-300 dark:border-amber-800'
                    )}
                  >
                    <WifiOff className="h-3.5 w-3.5" />
                    <span>Mô phỏng Mất kết nối (Offline)</span>
                  </button>
                  <button
                    type="button"
                    onClick={() => setNetworkBannerMock('reconnected')}
                    className={cn(
                      'px-3 py-1.5 rounded-xl text-xs font-bold transition-all flex items-center gap-1.5 border whitespace-nowrap cursor-pointer',
                      networkBannerMock === 'reconnected'
                        ? 'bg-emerald-600 text-white border-transparent shadow-xs'
                        : 'bg-white dark:bg-slate-800 text-emerald-600 dark:text-emerald-400 border-emerald-300 dark:border-emerald-800'
                    )}
                  >
                    <Check className="h-3.5 w-3.5" />
                    <span>Mô phỏng Đã kết nối lại (4 giây)</span>
                  </button>
                </div>
              </div>
            </div>

            {/* Showcase Stage 1: Dual Theme Stage (Light vs Dark with synchronized scenario copy) */}
            <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
              {/* Light Theme Container (Enforced true light tokens) */}
              <div
                key={`light-stage-${sReplayKey}`}
                className="p-8 rounded-3xl bg-[#FCFBF7] border border-[#e8e2d2] shadow-sm flex flex-col items-center justify-center relative overflow-hidden min-h-[300px] text-slate-900"
              >
                <div className="absolute top-4 left-4 flex items-center gap-1.5 px-2.5 py-1 rounded-full bg-amber-100/90 border border-amber-300 text-amber-900 text-[10px] font-bold">
                  <Sun className="h-3 w-3 text-amber-600" />
                  <span>Light Paper Mode (#FCFBF7) • {authScenario === 'login' ? 'Đăng nhập' : 'Đăng ký'}</span>
                </div>

                <SpendWiseSLoading
                  size={130}
                  status={sLoadingStatus}
                  paused={sLoadingPaused}
                  label={getScenarioCopy(sLoadingStatus, authScenario).label}
                  sublabel={getScenarioCopy(sLoadingStatus, authScenario).sublabel}
                />
              </div>

              {/* Dark Theme Container */}
              <div
                key={`dark-stage-${sReplayKey}`}
                className="p-8 rounded-3xl bg-[#101622] border border-white/10 shadow-sm flex flex-col items-center justify-center relative overflow-hidden min-h-[300px] text-white"
              >
                <div className="absolute top-4 left-4 flex items-center gap-1.5 px-2.5 py-1 rounded-full bg-slate-800 border border-slate-700 text-slate-200 text-[10px] font-bold">
                  <Moon className="h-3 w-3 text-emerald-400" />
                  <span>Dark Leather Mode (#101622) • {authScenario === 'login' ? 'Đăng nhập' : 'Đăng ký'}</span>
                </div>

                <SpendWiseSLoading
                  size={130}
                  status={sLoadingStatus}
                  paused={sLoadingPaused}
                  label={getScenarioCopy(sLoadingStatus, authScenario).label}
                  sublabel={getScenarioCopy(sLoadingStatus, authScenario).sublabel}
                />
              </div>
            </div>

            {/* Showcase Stage 2: Notebook Mock Overlay (Interactive Modal Experience) */}
            <div className="p-6 rounded-3xl bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 space-y-4">
              <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-2">
                <div>
                  <h4 className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400">
                    Mô phỏng trải nghiệm thực tế trong Form Đăng nhập & Đăng ký
                  </h4>
                  <p className="text-[11px] text-slate-500 dark:text-slate-400">
                    Lớp phủ mờ nhẹ xuất hiện ngay trên form khi submit, hiển thị vòng đời chuyển tiếp đầy đủ.
                  </p>
                </div>

                <div className="flex items-center gap-2">
                  <button
                    type="button"
                    onClick={() => setAuthButtonState('normal')}
                    className={cn(
                      'px-3 py-1 rounded-lg text-xs font-bold cursor-pointer transition-all',
                      authButtonState === 'normal'
                        ? 'bg-slate-900 text-white dark:bg-white dark:text-slate-900'
                        : 'bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-400'
                    )}
                  >
                    Bình thường (Idle)
                  </button>
                  <button
                    type="button"
                    onClick={() => setAuthButtonState('login_loading')}
                    className={cn(
                      'px-3 py-1 rounded-lg text-xs font-bold cursor-pointer transition-all',
                      authButtonState === 'login_loading'
                        ? 'bg-emerald-600 text-white'
                        : 'bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-400'
                    )}
                  >
                    Đang Đăng nhập
                  </button>
                  <button
                    type="button"
                    onClick={() => setAuthButtonState('register_loading')}
                    className={cn(
                      'px-3 py-1 rounded-lg text-xs font-bold cursor-pointer transition-all',
                      authButtonState === 'register_loading'
                        ? 'bg-emerald-600 text-white'
                        : 'bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-400'
                    )}
                  >
                    Đang Đăng ký
                  </button>
                </div>
              </div>

              {/* Interactive Mock Notebook Form Card */}
              <div className="max-w-md mx-auto p-6 rounded-2xl bg-[#FCFBF7] dark:bg-[#101622] border border-slate-200 dark:border-white/10 relative overflow-hidden shadow-md">
                {/* Simulated Form Fields */}
                <div className="space-y-3 opacity-90">
                  <div className="text-center mb-3">
                    <h5 className="text-base font-extrabold text-slate-900 dark:text-white">
                      {authButtonState === 'register_loading' ? 'Đăng Ký Tài Khoản' : 'Đăng Nhập SpendWise'}
                    </h5>
                    <p className="text-[11px] text-slate-500 dark:text-slate-400">
                      Nhập thông tin xác thực để bắt đầu
                    </p>
                  </div>

                  <div>
                    <label className="text-[10px] font-bold text-slate-500 uppercase">Email / Tài khoản</label>
                    <input
                      type="text"
                      disabled
                      value="alex.nguyen@spendwise.dev"
                      className="w-full px-3 py-2 text-xs rounded-lg border border-slate-300 dark:border-slate-700 bg-white dark:bg-slate-800 text-slate-900 dark:text-white"
                    />
                  </div>

                  <div>
                    <label className="text-[10px] font-bold text-slate-500 uppercase">Mật khẩu</label>
                    <input
                      type="password"
                      disabled
                      value="••••••••••••"
                      className="w-full px-3 py-2 text-xs rounded-lg border border-slate-300 dark:border-slate-700 bg-white dark:bg-slate-800 text-slate-900 dark:text-white"
                    />
                  </div>

                  <button
                    type="button"
                    disabled={authButtonState !== 'normal'}
                    className="w-full h-11 rounded-[10px] bg-gradient-to-r from-emerald-600 via-emerald-500 to-emerald-600 text-white font-bold text-sm flex items-center justify-center gap-2 shadow-lg shadow-emerald-950/50 border border-emerald-400/20 disabled:opacity-60 cursor-pointer disabled:cursor-not-allowed mt-2"
                  >
                    <span>
                      {authButtonState === 'normal'
                        ? 'Đăng Nhập'
                        : authButtonState === 'login_loading'
                        ? 'Đang đăng nhập...'
                        : 'Đang tạo tài khoản...'}
                    </span>
                  </button>
                </div>

                {/* Simulated Live S-Loading Overlay */}
                {authButtonState !== 'normal' && (
                  <div className="absolute inset-0 bg-[#FCFBF7]/92 dark:bg-[#101622]/94 backdrop-blur-xs flex flex-col items-center justify-center z-20 animate-in fade-in-50 duration-200">
                    <SpendWiseSLoading
                      size={110}
                      status={sLoadingStatus}
                      paused={sLoadingPaused}
                      label={
                        sLoadingStatus === 'pending'
                          ? authButtonState === 'login_loading'
                            ? 'Đang xác thực thông tin...'
                            : 'Đang tạo tài khoản mới...'
                          : sLoadingStatus === 'success'
                          ? 'Thành công!'
                          : sLoadingStatus === 'error'
                          ? 'Xác thực thất bại'
                          : sLoadingStatus === 'offline'
                          ? 'Mất kết nối'
                          : 'Hết giờ'
                      }
                      sublabel={
                        sLoadingStatus === 'pending'
                          ? authButtonState === 'login_loading'
                            ? 'Hệ thống đang chuẩn bị số liệu tài chính'
                            : 'Hệ thống đang khởi tạo sổ cái tài chính'
                          : sLoadingStatus === 'success'
                          ? 'Đang chuyển tiếp vào bảng điều khiển...'
                          : sLoadingStatus === 'error'
                          ? 'Vui lòng kiểm tra lại thông tin đã nhập'
                          : sLoadingStatus === 'offline'
                          ? 'Vui lòng kiểm tra đường truyền Internet'
                          : 'Vui lòng thử lại sau'
                      }
                    />
                  </div>
                )}
              </div>
            </div>
          </div>
        )}

        {/* ================= SECTION 3: PAGE SKELETONS GALLERY ================= */}
        {activeTab === 'skeletons' && (
          <div className="space-y-6 animate-in fade-in-50 duration-200">
            {/* Top Selector Rail */}
            {/* Top Selector Rail */}
            <div className="p-4 rounded-2xl bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 flex flex-col xl:flex-row xl:items-center justify-between gap-4">
              <div className="flex flex-wrap items-center gap-3">
                {/* 1. Khu vực: User vs Admin Segmented Control */}
                <div className="flex items-center gap-1.5 shrink-0">
                  <span className="text-[11px] font-bold text-slate-500 uppercase tracking-wider hidden sm:inline">
                    Khu vực:
                  </span>
                  <div className="flex items-center p-1 rounded-xl bg-slate-100 dark:bg-slate-800 border border-slate-200 dark:border-slate-700">
                    <button
                      type="button"
                      onClick={() => handleScopeChange('user')}
                      className={cn(
                        'px-3 py-1.5 rounded-lg text-xs font-bold transition-all flex items-center gap-1.5 cursor-pointer',
                        skeletonScope === 'user'
                          ? 'bg-emerald-600 text-white shadow-xs'
                          : 'text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white'
                      )}
                    >
                      <User className="h-3.5 w-3.5" />
                      <span>User</span>
                      <span className="text-[10px] px-1.5 py-0.2 rounded-full bg-white/20">
                        {SKELETON_ITEMS.filter((i) => i.scope === 'user').length}
                      </span>
                    </button>

                    <button
                      type="button"
                      onClick={() => handleScopeChange('admin')}
                      className={cn(
                        'px-3 py-1.5 rounded-lg text-xs font-bold transition-all flex items-center gap-1.5 cursor-pointer',
                        skeletonScope === 'admin'
                          ? 'bg-purple-600 text-white shadow-xs'
                          : 'text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white'
                      )}
                    >
                      <Shield className="h-3.5 w-3.5" />
                      <span>Admin</span>
                      <span className="text-[10px] px-1.5 py-0.2 rounded-full bg-white/20">
                        {SKELETON_ITEMS.filter((i) => i.scope === 'admin').length}
                      </span>
                    </button>
                  </div>
                </div>

                {/* 2. Chọn trang: Filtered Dropdown */}
                <div className="flex items-center gap-2">
                  <label className="text-[11px] font-bold text-slate-500 shrink-0 uppercase tracking-wider hidden sm:inline">
                    Trang:
                  </label>
                  <select
                    value={currentSkeleton.id}
                    onChange={(e) => setSelectedSkeletonRoute(e.target.value)}
                    className="px-3 py-1.5 text-xs font-bold rounded-xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-slate-900 dark:text-white focus:outline-hidden max-w-[280px] sm:max-w-none truncate"
                  >
                    {currentScopeItems.map((item) => (
                      <option key={item.id} value={item.id}>
                        {item.name} {item.hasSkeleton ? '✓' : '(Chưa có Skeleton)'}
                      </option>
                    ))}
                  </select>
                </div>
              </div>

              {/* 3. Chế độ: UI thật / Skeleton / So sánh */}
              <div className="flex flex-wrap items-center gap-2 self-start xl:self-auto">
                <div className="flex items-center p-1 rounded-xl bg-slate-100 dark:bg-slate-800 border border-slate-200 dark:border-slate-700">
                  <button
                    type="button"
                    onClick={() => setViewMode('live')}
                    className={cn(
                      'px-3 py-1.5 rounded-lg text-xs font-bold transition-all whitespace-nowrap cursor-pointer',
                      viewMode === 'live'
                        ? 'bg-white dark:bg-slate-900 text-emerald-600 dark:text-emerald-400 shadow-xs'
                        : 'text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white'
                    )}
                  >
                    1. UI thật (Rendered)
                  </button>

                  <button
                    type="button"
                    onClick={() => setViewMode('skeleton')}
                    className={cn(
                      'px-3 py-1.5 rounded-lg text-xs font-bold transition-all whitespace-nowrap cursor-pointer',
                      viewMode === 'skeleton'
                        ? 'bg-white dark:bg-slate-900 text-emerald-600 dark:text-emerald-400 shadow-xs'
                        : 'text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white'
                    )}
                  >
                    2. Skeleton (Loading)
                  </button>

                  {currentSkeleton.liveComponent && (
                    <button
                      type="button"
                      onClick={() => setViewMode(viewMode === 'split' ? 'split' : 'stacked')}
                      className={cn(
                        'px-3 py-1.5 rounded-lg text-xs font-bold transition-all whitespace-nowrap cursor-pointer',
                        viewMode === 'stacked' || viewMode === 'split'
                          ? 'bg-white dark:bg-slate-900 text-emerald-600 dark:text-emerald-400 shadow-xs'
                          : 'text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white'
                      )}
                    >
                      3. So sánh đối chiếu
                    </button>
                  )}
                </div>

                {(viewMode === 'stacked' || viewMode === 'split') && currentSkeleton.liveComponent && (
                  <div className="flex items-center p-1 rounded-xl bg-slate-100 dark:bg-slate-800 border border-slate-200 dark:border-slate-700">
                    <button
                      type="button"
                      onClick={() => setViewMode('stacked')}
                      className={cn(
                        'px-2.5 py-1 rounded-lg text-[11px] font-bold transition-all whitespace-nowrap cursor-pointer',
                        viewMode === 'stacked'
                          ? 'bg-emerald-600 text-white shadow-xs'
                          : 'text-slate-600 dark:text-slate-400'
                      )}
                    >
                      Dọc (Chuẩn Desktop)
                    </button>
                    <button
                      type="button"
                      onClick={() => setViewMode('split')}
                      className={cn(
                        'px-2.5 py-1 rounded-lg text-[11px] font-bold transition-all whitespace-nowrap cursor-pointer',
                        viewMode === 'split'
                          ? 'bg-emerald-600 text-white shadow-xs'
                          : 'text-slate-600 dark:text-slate-400'
                      )}
                    >
                      Ngang (2 Cột)
                    </button>
                  </div>
                )}
              </div>
            </div>

            {/* Skeleton / Live Render Stage */}
            {(() => {
              const isEligibleToRender = !currentSkeleton.requiresAuth || isAuthenticated;
              const hasRole = !currentSkeleton.requiresRole || (currentUser?.role === currentSkeleton.requiresRole);

              // Honest Skeleton Component renderer with fallback for pages without skeleton
              const renderSkeletonContent = () => {
                if (currentSkeleton.hasSkeleton && currentSkeleton.component) {
                  return currentSkeleton.component;
                }

                return (
                  <div className="py-16 px-6 text-center space-y-3">
                    <div className="h-12 w-12 rounded-2xl bg-amber-100 dark:bg-amber-950/60 text-amber-600 dark:text-amber-400 mx-auto flex items-center justify-center">
                      <Clock className="h-6 w-6" />
                    </div>
                    <div className="space-y-1">
                      <h4 className="text-sm font-bold text-slate-900 dark:text-white">
                        Trang {currentSkeleton.name} chưa triển khai Skeleton riêng
                      </h4>
                      <p className="text-xs text-slate-500 dark:text-slate-400 max-w-md mx-auto">
                        Trang này hiện tải trực tiếp từ backend hoặc sử dụng spinner toàn trang. Khi thiết kế xong Skeleton tương ứng cho route <code className="font-mono text-[11px] px-1.5 py-0.5 rounded-sm bg-slate-100 dark:bg-slate-800">{currentSkeleton.route}</code>, component sẽ xuất hiện tại đây.
                      </p>
                    </div>
                  </div>
                );
              };

              const renderLiveContent = () => {
                if (!currentSkeleton.liveComponent) return null;

                return (
                  <div className="space-y-4">
                    {/* Honest Auth & Route Information Bar */}
                    <div className="p-3.5 rounded-2xl bg-slate-50 dark:bg-slate-900 border border-slate-200 dark:border-slate-800 flex flex-wrap items-center justify-between gap-3 text-xs">
                      <div className="flex items-center gap-2">
                        {isAuthenticated ? (
                          <span className="flex items-center gap-1.5 px-2.5 py-1 rounded-full bg-emerald-100 dark:bg-emerald-950/60 text-emerald-800 dark:text-emerald-300 font-bold">
                            <UserCheck className="h-3.5 w-3.5 text-emerald-600" />
                            <span>Đã đăng nhập ({currentUser?.name || currentUser?.email} • {currentUser?.role})</span>
                          </span>
                        ) : (
                          <span className="flex items-center gap-1.5 px-2.5 py-1 rounded-full bg-amber-100 dark:bg-amber-950/60 text-amber-800 dark:text-amber-300 font-bold border border-amber-300 dark:border-amber-800">
                            <AlertCircle className="h-3.5 w-3.5 text-amber-600" />
                            <span>Khách (Chưa đăng nhập) • {currentSkeleton.requiresRole === 'SystemAdmin' ? 'Trang Admin yêu cầu quyền SystemAdmin' : 'Hiển thị giao diện thực tế ở trạng thái mặc định'}</span>
                          </span>
                        )}
                        <span className="text-slate-500 dark:text-slate-400 font-mono text-[11px] hidden sm:inline">
                          Route: {currentSkeleton.route}
                        </span>
                        <span className="px-2 py-0.5 rounded-md text-[10px] font-bold bg-slate-200/60 dark:bg-slate-800 text-slate-700 dark:text-slate-300">
                          {currentSkeleton.scope === 'admin' ? '🛡️ Admin Area' : '👤 User Area'}
                        </span>
                      </div>

                      <NavLink
                        to={currentSkeleton.route}
                        className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-xl bg-slate-900 hover:bg-slate-800 dark:bg-emerald-600 dark:hover:bg-emerald-700 text-white font-bold transition shadow-xs cursor-pointer"
                      >
                        <span>Mở trang thật SpendWise</span>
                        <ExternalLink className="h-3.5 w-3.5" />
                      </NavLink>
                    </div>

                    {/* Actual Real Production Page Component wrapped in PreviewErrorBoundary */}
                    <div className="p-6 rounded-3xl bg-white dark:bg-[#0B1120] border border-slate-200/80 dark:border-slate-800 shadow-xs min-h-[500px] overflow-hidden">
                      <PreviewErrorBoundary
                        pageName={currentSkeleton.name}
                        route={currentSkeleton.route}
                      >
                        {currentSkeleton.liveComponent}
                      </PreviewErrorBoundary>
                    </div>
                  </div>
                );
              };

              return (
                <>
                  {viewMode === 'skeleton' && (
                    <div className="p-6 rounded-3xl bg-white dark:bg-[#0B1120] border border-slate-200/80 dark:border-slate-800 shadow-xs min-h-[500px] overflow-hidden">
                      {renderSkeletonContent()}
                    </div>
                  )}

                  {viewMode === 'live' && renderLiveContent()}

                  {viewMode === 'stacked' && currentSkeleton.liveComponent && (
                    <div className="space-y-8">
                      <div className="space-y-2">
                        <div className="flex items-center justify-between px-2">
                          <span className="text-xs font-bold uppercase tracking-wider text-emerald-600">
                            1. Bố cục Skeleton Shimmer (Full-Width Desktop)
                          </span>
                          <span className="text-[10px] text-slate-400">Loading State</span>
                        </div>
                        <div className="p-6 rounded-3xl bg-white dark:bg-[#0B1120] border border-slate-200/80 dark:border-slate-800 shadow-xs overflow-hidden">
                          {renderSkeletonContent()}
                        </div>
                      </div>

                      <div className="space-y-2">
                        <div className="flex items-center justify-between px-2">
                          <span className="text-xs font-bold uppercase tracking-wider text-slate-600 dark:text-slate-300">
                            2. Bố cục trang thực tế SpendWise (Full-Width Desktop)
                          </span>
                          <span className="text-[10px] text-slate-400">Rendered State</span>
                        </div>
                        {renderLiveContent()}
                      </div>
                    </div>
                  )}

                  {viewMode === 'split' && currentSkeleton.liveComponent && (
                    <div className="grid grid-cols-1 xl:grid-cols-2 gap-6">
                      <div className="space-y-2">
                        <div className="flex items-center justify-between px-2">
                          <span className="text-xs font-bold uppercase tracking-wider text-emerald-600">
                            1. Bố cục Skeleton Shimmer
                          </span>
                          <span className="text-[10px] text-slate-400">Loading State</span>
                        </div>
                        <div className="p-5 rounded-3xl bg-white dark:bg-[#0B1120] border border-slate-200/80 dark:border-slate-800 shadow-xs overflow-hidden">
                          {renderSkeletonContent()}
                        </div>
                      </div>

                      <div className="space-y-2">
                        <div className="flex items-center justify-between px-2">
                          <span className="text-xs font-bold uppercase tracking-wider text-slate-600 dark:text-slate-300">
                            2. Bố cục trang thực tế SpendWise
                          </span>
                          <span className="text-[10px] text-slate-400">Rendered State</span>
                        </div>
                        {renderLiveContent()}
                      </div>
                    </div>
                  )}
                </>
              );
            })()}
          </div>
        )}

        {/* ================= SECTION 4: FIRST-TIME ONBOARDING ================= */}
        {activeTab === 'onboarding' && (
          <div className="space-y-4 animate-in fade-in-50 duration-200">
            <div className="p-4 rounded-2xl bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 flex flex-wrap items-center justify-between gap-3">
              <div>
                <h3 className="text-sm font-bold text-slate-900 dark:text-white">
                  First-Time Onboarding Flow (4 Bước)
                </h3>
                <p className="text-xs text-slate-500 dark:text-slate-400">
                  Chỉ ở chế độ Preview: Không can thiệp DB, không ép buộc người dùng thật, hỗ trợ chọn tất cả 6 mục, bỏ qua và tóm tắt.
                </p>
              </div>
            </div>

            <OnboardingPreviewModal />
          </div>
        )}

        {/* ================= SECTION 5: SETTINGS REDESIGN PREVIEW (BATCH 1) ================= */}
        {activeTab === 'settings-redesign' && (
          <div className="space-y-6 animate-in fade-in-50 duration-200">
            <div className="p-4 sm:p-5 rounded-2xl bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 flex flex-col sm:flex-row sm:items-center justify-between gap-3">
              <div>
                <div className="flex items-center gap-2">
                  <h3 className="text-sm font-bold text-slate-900 dark:text-white">
                    Phương án Cải tiến Toàn diện Giao diện Cài đặt (Settings Redesign V2 — Clean Financial)
                  </h3>
                  <span className="text-[10px] font-bold px-2 py-0.5 rounded-full bg-emerald-100 dark:bg-emerald-950 text-emerald-700 dark:text-emerald-400 border border-emerald-300 dark:border-emerald-800">
                    Impeccable Design Review
                  </span>
                </div>
                <p className="text-xs text-slate-500 dark:text-slate-400 mt-0.5">
                  Bố cục tối giản chuẩn ứng dụng tài chính (Operate Mode): Loại bỏ banner đen bóng bẩy và nested cards; tổ chức các nhóm chức năng dạng danh sách hàng phân cách tinh tế (Single Central Column max-w-4xl), trung thực 100% với dữ liệu backend.
                </p>
              </div>

              <div className="flex items-center gap-2 self-start sm:self-auto shrink-0">
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => {
                    setActiveTab('skeletons');
                    setSelectedSkeletonRoute('settings');
                    setViewMode('live');
                  }}
                  className="text-xs font-semibold cursor-pointer"
                >
                  <span>So sánh với Trang cũ →</span>
                </Button>
              </div>
            </div>

            {/* Render Settings Redesign Component */}
            <SettingsRedesignPreview />
          </div>
        )}

        {/* ================= SECTION 6: CORE FINANCIAL UI REDESIGN PREVIEW (BATCH 2) ================= */}
        {activeTab === 'batch2' && (
          <div className="space-y-6 animate-in fade-in-50 duration-200">
            <FinancialRedesignBatch2Preview />
          </div>
        )}
      </main>
    </div>
  );
};
