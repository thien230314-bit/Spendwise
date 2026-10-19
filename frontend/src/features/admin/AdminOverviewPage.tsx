import React, { useState, useEffect, useCallback } from 'react';
import { NavLink } from 'react-router-dom';
import {
  Users,
  Building2,
  Headphones,
  ShieldCheck,
  CheckCircle2,
  AlertTriangle,
  ArrowUpRight,
  RefreshCw,
  AlertCircle,
  Activity,
  CreditCard,
} from 'lucide-react';
import { useI18n } from '../../i18n/I18nContext';
import { useAuth } from '../../providers/AuthProvider';
import { casesApiService, AdminDashboardDto } from '../../services/casesApiService';
import { Button } from '../../components/ui/Button';
import { ApiError } from '../../lib/apiError';
import { AdminOverviewSkeleton } from '../../components/skeletons/AdminOverviewSkeleton';

export const AdminOverviewPage: React.FC = () => {
  const { t, locale } = useI18n();
  const isVi = locale === 'vi-VN';
  const { currentUser } = useAuth();

  const [dashboard, setDashboard] = useState<AdminDashboardDto | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);

  const fetchDashboardData = useCallback(async () => {
    if (!currentUser || currentUser.role !== 'SystemAdmin') {
      setDashboard(null);
      setIsLoading(false);
      return;
    }

    setIsLoading(true);
    setLoadError(null);

    try {
      const data = await casesApiService.getAdminDashboard();
      setDashboard(data);
    } catch (err) {
      console.error('Failed to load admin dashboard metrics:', err);
      const apiErr = err as ApiError;
      setLoadError(
        apiErr?.detail ||
        apiErr?.title ||
        (isVi ? 'Không thể tải chỉ số bảng điều khiển từ máy chủ.' : 'Failed to load admin dashboard metrics.')
      );
      setDashboard(null);
    } finally {
      setIsLoading(false);
    }
  }, [currentUser, isVi]);

  useEffect(() => {
    fetchDashboardData();
  }, [fetchDashboardData]);

  const formatCurrency = (val: number) => {
    return new Intl.NumberFormat(locale === 'vi-VN' ? 'vi-VN' : 'en-US', {
      style: 'currency',
      currency: 'VND',
      maximumFractionDigits: 0,
    }).format(val);
  };

  const openTicketsCount = dashboard?.supportCases ?? 0;
  const openDisputesCount = dashboard?.disputes ?? 0;
  const pendingRiskCount = dashboard?.riskCases ?? 0;
  const pendingTxCount = dashboard?.pendingTransactions ?? 0;
  const failedTxCount = dashboard?.failedTransactions ?? 0;

  const needsAttentionItems = [
    ...(openTicketsCount > 0
      ? [
          {
            id: 'support',
            title: isVi
              ? `${openTicketsCount} yêu cầu hỗ trợ đang chờ xử lý`
              : `${openTicketsCount} support case(s) pending`,
            link: '/admin/support',
            icon: Headphones,
            color: 'text-purple-600 dark:text-purple-400',
            bgColor: 'bg-purple-50 dark:bg-purple-950/30 border-purple-200 dark:border-purple-900/40',
          },
        ]
      : []),
    ...(openDisputesCount > 0
      ? [
          {
            id: 'disputes',
            title: isVi
              ? `${openDisputesCount} hồ sơ khiếu nại giao dịch đang mở`
              : `${openDisputesCount} dispute(s) open`,
            link: '/admin/disputes',
            icon: AlertTriangle,
            color: 'text-amber-600 dark:text-amber-400',
            bgColor: 'bg-amber-50 dark:bg-amber-950/30 border-amber-200 dark:border-amber-900/40',
          },
        ]
      : []),
    ...(pendingRiskCount > 0
      ? [
          {
            id: 'risk',
            title: isVi
              ? `${pendingRiskCount} cảnh báo rủi ro giao dịch cần đối soát`
              : `${pendingRiskCount} risk case(s) pending review`,
            link: '/admin/risk',
            icon: ShieldCheck,
            color: 'text-rose-600 dark:text-rose-400',
            bgColor: 'bg-rose-50 dark:bg-rose-950/30 border-rose-200 dark:border-rose-900/40',
          },
        ]
      : []),
    ...(failedTxCount > 0
      ? [
          {
            id: 'failed_tx',
            title: isVi
              ? `${failedTxCount} giao dịch tài chính bị thất bại`
              : `${failedTxCount} failed transaction(s)`,
            link: '/admin/transactions',
            icon: CreditCard,
            color: 'text-rose-600 dark:text-rose-400',
            bgColor: 'bg-rose-50 dark:bg-rose-950/30 border-rose-200 dark:border-rose-900/40',
          },
        ]
      : []),
  ];

  if (isLoading && !dashboard) {
    return <AdminOverviewSkeleton />;
  }

  return (
    <div id="admin-overview-page" className="space-y-6">
      {/* Header */}
      <div className="flex flex-col sm:flex-row sm:items-start justify-between gap-4">
        <div>
          <h2 className="text-xl font-bold text-slate-900 dark:text-white tracking-tight">
            {t('adminOverview.title')}
          </h2>
          <p className="text-xs text-slate-500 dark:text-slate-400 mt-1 max-w-2xl leading-relaxed">
            {isVi
              ? 'Tổng quan thời gian thực về người dùng, số dư giao dịch, tỷ lệ thành công và các vụ việc cần xử lý từ backend.'
              : 'Real-time overview of users, transaction volume, success rates, and active operational cases from the backend.'}
          </p>
        </div>

        <div className="flex items-center gap-2">
          <Button
            size="sm"
            variant="outline"
            onClick={fetchDashboardData}
            disabled={isLoading}
            className="text-xs font-semibold gap-1.5 cursor-pointer"
          >
            <RefreshCw className={`w-3.5 h-3.5 ${isLoading ? 'animate-spin' : ''}`} />
            <span>{isVi ? 'Làm mới' : 'Refresh'}</span>
          </Button>
          <NavLink
            to="/admin/users"
            className="inline-flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium rounded-xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 text-slate-700 dark:text-slate-300 hover:bg-slate-50 dark:hover:bg-slate-800 transition"
          >
            <Users className="w-3.5 h-3.5 text-slate-400" />
            <span>{isVi ? 'Người dùng' : 'Users'} {dashboard ? `(${dashboard.totalUsers})` : ''}</span>
          </NavLink>
        </div>
      </div>

      {loadError && (
        <div className="p-4 rounded-2xl bg-rose-50/70 dark:bg-rose-950/30 border border-rose-200 dark:border-rose-900/50 flex flex-col sm:flex-row items-start sm:items-center justify-between gap-3 text-xs">
          <div className="flex items-center gap-2.5 text-rose-800 dark:text-rose-300 font-medium">
            <AlertCircle className="w-4 h-4 text-rose-600 shrink-0" />
            <span>{loadError}</span>
          </div>
          <Button
            size="sm"
            variant="outline"
            onClick={fetchDashboardData}
            className="text-xs font-semibold gap-1 border-rose-300 dark:border-rose-800 text-rose-800 dark:text-rose-300 hover:bg-rose-100/50 cursor-pointer shrink-0"
          >
            <RefreshCw className="w-3 h-3" />
            <span>{isVi ? 'Thử lại' : 'Retry'}</span>
          </Button>
        </div>
      )}

      {/* What Needs Attention Section */}
      <div className="p-4 rounded-xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 space-y-3">
        <div className="flex items-center justify-between">
          <h3 className="text-xs font-semibold uppercase tracking-wider text-slate-500 dark:text-slate-400">
            {isVi ? 'Hạng mục cần chú ý' : 'Items Requiring Attention'}
          </h3>
          <span className="text-xs text-slate-400">
            {isLoading
              ? (isVi ? 'Đang tải...' : 'Loading...')
              : loadError
              ? (isVi ? 'Không có dữ liệu' : 'Unavailable')
              : !dashboard
              ? '—'
              : needsAttentionItems.length > 0
              ? `${needsAttentionItems.length} ${isVi ? 'vấn đề cần xử lý' : 'pending item(s)'}`
              : (isVi ? 'Đang ổn định' : 'All clear')}
          </span>
        </div>

        {isLoading ? (
          <div className="py-4 flex items-center justify-center gap-2 text-xs text-slate-500">
            <RefreshCw className="w-3.5 h-3.5 animate-spin text-emerald-600" />
            <span>{isVi ? 'Đang cập nhật số liệu giám sát...' : 'Loading attention items...'}</span>
          </div>
        ) : loadError ? (
          <div className="flex items-center gap-2 p-3 rounded-lg bg-rose-50/60 dark:bg-rose-950/20 border border-rose-200/50 dark:border-rose-900/30 text-rose-800 dark:text-rose-400 text-xs">
            <AlertCircle className="w-4 h-4 text-rose-600 shrink-0" />
            <span>{isVi ? 'Không thể tải danh sách hạng mục cần chú ý do lỗi kết nối.' : 'Unable to load attention items due to connection error.'}</span>
          </div>
        ) : !dashboard ? (
          <div className="py-4 text-center text-xs text-slate-400">
            {isVi ? 'Chưa có dữ liệu bảng điều khiển.' : 'No dashboard data available.'}
          </div>
        ) : needsAttentionItems.length === 0 ? (
          <div className="flex items-center gap-2 p-3 rounded-lg bg-emerald-50/60 dark:bg-emerald-950/20 border border-emerald-200/50 dark:border-emerald-900/30 text-emerald-800 dark:text-emerald-400 text-xs">
            <CheckCircle2 className="w-4 h-4 text-emerald-600 dark:text-emerald-400 shrink-0" />
            <span>
              {isVi
                ? 'Không có cảnh báo tồn đọng. Tất cả yêu cầu hỗ trợ, khiếu nại và giao dịch ngân hàng đang được kiểm soát an toàn.'
                : 'No pending alerts. Support cases, disputes, and risk items are fully managed.'}
            </span>
          </div>
        ) : (
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-2.5">
            {needsAttentionItems.map((item) => {
              const Icon = item.icon;
              return (
                <NavLink
                  key={item.id}
                  to={item.link}
                  className={`flex items-center justify-between p-3 rounded-lg border transition hover:opacity-90 ${item.bgColor}`}
                >
                  <div className="flex items-center gap-2.5 min-w-0">
                    <Icon className={`w-4 h-4 shrink-0 ${item.color}`} />
                    <span className="text-xs font-medium text-slate-900 dark:text-white truncate">
                      {item.title}
                    </span>
                  </div>
                  <ArrowUpRight className="w-3.5 h-3.5 text-slate-400 shrink-0 ml-2" />
                </NavLink>
              );
            })}
          </div>
        )}
      </div>

      {/* Real Backend Summary Strip */}
      <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
        {/* Total Users */}
        <div className="p-3.5 rounded-xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900">
          <span className="text-xs text-slate-500 dark:text-slate-400 block">
            {t('adminOverview.totalUsers')}
          </span>
          <span className="text-lg font-bold text-slate-900 dark:text-white mt-0.5 block">
            {isLoading ? '—' : (dashboard?.totalUsers?.toLocaleString() ?? '—')}
          </span>
          <span className="text-[11px] text-emerald-600 dark:text-emerald-400 block mt-0.5">
            {isLoading ? '...' : dashboard ? `${dashboard.activeUsers ?? 0} ${isVi ? 'đang hoạt động' : 'active'}` : '—'}
          </span>
        </div>

        {/* Transactions Count & Volume */}
        <div className="p-3.5 rounded-xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900">
          <span className="text-xs text-slate-500 dark:text-slate-400 block">
            {isVi ? 'Tổng giao dịch (30 ngày)' : 'Transactions (30d)'}
          </span>
          <span className="text-lg font-bold text-slate-900 dark:text-white mt-0.5 block">
            {isLoading ? '—' : (dashboard?.transactionCount?.toLocaleString() ?? '—')}
          </span>
          <span className="text-[11px] text-slate-400 block mt-0.5 truncate font-mono">
            {isLoading ? '...' : dashboard ? formatCurrency(dashboard.transactionVolume ?? 0) : '—'}
          </span>
        </div>

        {/* Success Rate */}
        <div className="p-3.5 rounded-xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900">
          <span className="text-xs text-slate-500 dark:text-slate-400 block">
            {isVi ? 'Tỷ lệ thành công' : 'Success Rate'}
          </span>
          <span className="text-lg font-bold text-slate-900 dark:text-white mt-0.5 block">
            {isLoading ? '—' : dashboard ? `${dashboard.successRate}%` : '—'}
          </span>
          <span className="text-[11px] text-slate-400 block mt-0.5">
            {isLoading ? '...' : dashboard ? `${dashboard.pendingTransactions ?? 0} ${isVi ? 'đang xử lý' : 'pending'}` : '—'}
          </span>
        </div>

        {/* Support & Disputes */}
        <div className="p-3.5 rounded-xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900">
          <span className="text-xs text-slate-500 dark:text-slate-400 block">
            {isVi ? 'Vé hỗ trợ & Khiếu nại' : 'Support & Disputes'}
          </span>
          <span className="text-lg font-bold text-slate-900 dark:text-white mt-0.5 block">
            {isLoading ? '—' : openTicketsCount + openDisputesCount}
          </span>
          <span className="text-[11px] text-slate-400 block mt-0.5">
            {isLoading ? '...' : `${openTicketsCount} ${isVi ? 'hỗ trợ' : 'support'}, ${openDisputesCount} ${isVi ? 'khiếu nại' : 'disputes'}`}
          </span>
        </div>
      </div>

      {/* Main Content: Key Operational Areas and Quick Navigation */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Sensitive Financial Access Section */}
        <div className="lg:col-span-2 rounded-xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 p-5 space-y-4 shadow-xs">
          <div className="flex items-center justify-between pb-3 border-b border-slate-100 dark:border-slate-800">
            <div>
              <h3 className="text-sm font-semibold text-slate-900 dark:text-white">
                {isVi ? 'Quản lý & Giám sát tài chính hệ thống' : 'System Financial Oversight'}
              </h3>
              <p className="text-xs text-slate-500 dark:text-slate-400 mt-0.5">
                {isVi
                  ? 'Tra cứu và đối soát sổ cái theo cơ chế xác thực lý do nhạy cảm (Sensitive Access Audit).'
                  : 'Search and inspect transactions & ledger under sensitive access audit compliance.'}
              </p>
            </div>
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 pt-1">
            {/* Quick Card 1: Transactions Search */}
            <div className="p-4 rounded-xl bg-slate-50 dark:bg-slate-800/40 border border-slate-200/60 dark:border-slate-700/60 space-y-3 flex flex-col justify-between">
              <div className="space-y-1.5">
                <div className="flex items-center gap-2 text-slate-900 dark:text-white font-semibold text-xs">
                  <CreditCard className="w-4 h-4 text-emerald-600 dark:text-emerald-400 shrink-0" />
                  <span>{isVi ? 'Tra cứu giao dịch nhạy cảm' : 'Sensitive Transactions Search'}</span>
                </div>
                <p className="text-[11px] text-slate-500 dark:text-slate-400 leading-relaxed">
                  {isVi
                    ? 'Tra cứu 200 giao dịch tài chính hệ thống gần nhất với lý do truy cập hợp lệ để đối soát sự cố.'
                    : 'Inspect latest system transactions with audit justification for reconciliation and incident review.'}
                </p>
              </div>
              <NavLink
                to="/admin/transactions"
                className="inline-flex items-center justify-between px-3 py-2 rounded-lg bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-700 text-xs font-semibold text-slate-800 dark:text-slate-200 hover:border-emerald-500 transition"
              >
                <span>{isVi ? 'Mở màn hình giao dịch' : 'Open Transactions'}</span>
                <ArrowUpRight className="w-3.5 h-3.5 text-slate-400" />
              </NavLink>
            </div>

            {/* Quick Card 2: Ledger Search */}
            <div className="p-4 rounded-xl bg-slate-50 dark:bg-slate-800/40 border border-slate-200/60 dark:border-slate-700/60 space-y-3 flex flex-col justify-between">
              <div className="space-y-1.5">
                <div className="flex items-center gap-2 text-slate-900 dark:text-white font-semibold text-xs">
                  <Building2 className="w-4 h-4 text-blue-600 dark:text-blue-400 shrink-0" />
                  <span>{isVi ? 'Sổ cái bút toán kép (Ledger)' : 'Double-Entry Ledger'}</span>
                </div>
                <p className="text-[11px] text-slate-500 dark:text-slate-400 leading-relaxed">
                  {isVi
                    ? 'Kiểm toán tính cân bằng (Debit = Credit) và trạng thái bút toán bất biến của toàn bộ tài khoản SpendWise.'
                    : 'Audit ledger balance (Debit = Credit) and immutable postings across all SpendWise accounts.'}
                </p>
              </div>
              <NavLink
                to="/admin/ledger"
                className="inline-flex items-center justify-between px-3 py-2 rounded-lg bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-700 text-xs font-semibold text-slate-800 dark:text-slate-200 hover:border-blue-500 transition"
              >
                <span>{isVi ? 'Mở sổ cái tài chính' : 'Open Ledger'}</span>
                <ArrowUpRight className="w-3.5 h-3.5 text-slate-400" />
              </NavLink>
            </div>
          </div>
        </div>

        {/* System Health Box */}
        <div className="p-5 rounded-xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 space-y-4 shadow-xs">
          <div className="flex items-center justify-between">
            <h3 className="text-sm font-semibold text-slate-900 dark:text-white flex items-center gap-2">
              <Activity className="w-4 h-4 text-emerald-600 dark:text-emerald-400" />
              <span>{isVi ? 'Trạng thái máy chủ & Vận hành' : 'System & Service Health'}</span>
            </h3>
            <NavLink
              to="/admin/health"
              className="text-xs font-medium text-emerald-600 dark:text-emerald-400 hover:underline flex items-center gap-0.5"
            >
              <span>{isVi ? 'Chi tiết' : 'Details'}</span>
              <ArrowUpRight className="w-3.5 h-3.5" />
            </NavLink>
          </div>

          <div className="space-y-3 text-xs">
            {/* Database / Core Connection */}
            <div className="p-3 rounded-xl bg-slate-50 dark:bg-slate-800/40 border border-slate-100 dark:border-slate-800 space-y-1">
              <div className="flex items-center justify-between">
                <span className="font-medium text-slate-800 dark:text-slate-200">
                  {isVi ? 'Cơ sở dữ liệu (SQL Server)' : 'Database Connection'}
                </span>
                <span className={`text-[11px] font-semibold px-2 py-0.5 rounded-full ${
                  isLoading || !dashboard
                    ? 'bg-slate-100 text-slate-600 dark:bg-slate-800 dark:text-slate-400'
                    : dashboard?.systemHealth === 'Healthy'
                    ? 'bg-emerald-50 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-400'
                    : 'bg-rose-50 text-rose-700 dark:bg-rose-950/40 dark:text-rose-400'
                }`}>
                  {isLoading
                    ? '...'
                    : !dashboard
                    ? (isVi ? 'Chưa kết nối' : 'Not Connected')
                    : dashboard?.systemHealth === 'Healthy'
                    ? (isVi ? 'Bình thường (Healthy)' : 'Healthy')
                    : (isVi ? 'Lỗi kết nối' : 'Unhealthy')}
                </span>
              </div>
              <p className="text-[11px] text-slate-500 dark:text-slate-400">
                {isVi ? 'Xác thực qua IDatabaseHealthChecker backend.' : 'Verified via backend health check.'}
              </p>
            </div>

            {/* AI Assistant Requests Count */}
            <div className="p-3 rounded-xl bg-slate-50 dark:bg-slate-800/40 border border-slate-100 dark:border-slate-800 space-y-1">
              <div className="flex items-center justify-between">
                <span className="font-medium text-slate-800 dark:text-slate-200">
                  {isVi ? 'Yêu cầu trợ lý AI (30 ngày)' : 'AI Requests (30d)'}
                </span>
                <span className="font-mono text-xs font-bold text-slate-900 dark:text-white">
                  {isLoading ? '—' : (dashboard?.aiRequests ?? 0)}
                </span>
              </div>
              <p className="text-[11px] text-slate-500 dark:text-slate-400">
                {isVi ? 'Số tin nhắn người dùng tương tác trợ lý tài chính.' : 'User queries processed by financial AI.'}
              </p>
            </div>

            {/* Audit Logs Link */}
            <div className="pt-2 border-t border-slate-100 dark:border-slate-800 flex items-center justify-between text-xs">
              <span className="text-slate-500 dark:text-slate-400">
                {isVi ? 'Nhật ký kiểm toán quản trị' : 'Admin Audit Logs'}
              </span>
              <NavLink
                to="/admin/audit"
                className="font-medium text-emerald-600 dark:text-emerald-400 hover:underline inline-flex items-center gap-1"
              >
                <span>{isVi ? 'Xem nhật ký' : 'View logs'}</span>
                <ArrowUpRight className="w-3.5 h-3.5" />
              </NavLink>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};

export default AdminOverviewPage;
