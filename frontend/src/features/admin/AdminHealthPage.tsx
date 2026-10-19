import React, { useState, useEffect, useCallback } from 'react';
import {
  Server,
  Database,
  Building2,
  Bell,
  Cpu,
  Radio,
  Clock,
  RefreshCw,
  AlertTriangle,
  CheckCircle2,
  XCircle,
  HelpCircle,
  Info,
} from 'lucide-react';
import { useI18n } from '../../i18n/I18nContext';
import { casesApiService, AdminHealthDto } from '../../services/casesApiService';

export const AdminHealthPage: React.FC = () => {
  const { locale } = useI18n();
  const isVi = locale === 'vi-VN';

  const [healthData, setHealthData] = useState<AdminHealthDto | null>(null);
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [lastChecked, setLastChecked] = useState<Date | null>(null);

  const fetchHealth = useCallback(async () => {
    setIsLoading(true);
    setLoadError(null);
    try {
      const data = await casesApiService.getAdminHealth();
      setHealthData(data);
      setLastChecked(new Date());
    } catch (err: any) {
      setLoadError(
        err?.message || (isVi ? 'Không thể kiểm tra trạng thái máy chủ.' : 'Failed to query server health.')
      );
    } finally {
      setIsLoading(false);
    }
  }, [isVi]);

  useEffect(() => {
    fetchHealth();
  }, [fetchHealth]);

  const getStatusBadge = (status?: string | boolean | null) => {
    if (status === true || status === 'Healthy' || status === 'Enabled' || status === 'Configured' || status === 'Registered') {
      return {
        type: 'success' as const,
        label: isVi
          ? status === 'Healthy'
            ? 'Khỏe mạnh'
            : status === 'Enabled'
            ? 'Đang bật'
            : status === 'Configured'
            ? 'Đã cấu hình'
            : status === 'Registered'
            ? 'Đã đăng ký'
            : 'Hoạt động'
          : String(status),
      };
    }
    if (status === false || status === 'Unhealthy' || status === 'Disabled') {
      return {
        type: 'danger' as const,
        label: isVi
          ? status === 'Unhealthy'
            ? 'Không khả dụng / Lỗi'
            : 'Đã tắt'
          : String(status),
      };
    }
    if (status === 'Degraded' || status === 'Warning') {
      return {
        type: 'warning' as const,
        label: isVi ? 'Cảnh báo' : 'Degraded',
      };
    }
    return {
      type: 'neutral' as const,
      label: status ? String(status) : isVi ? 'Không xác định' : 'Unknown',
    };
  };

  return (
    <div id="admin-health-page" className="space-y-6">
      {/* Header */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <h2 className="text-xl font-bold text-slate-900 dark:text-white tracking-tight">
            {isVi ? 'Trạng thái hệ thống' : 'System Status'}
          </h2>
          <p className="text-xs text-slate-500 dark:text-slate-400 mt-1 max-w-2xl leading-relaxed">
            {isVi
              ? 'Kiểm tra trạng thái thời gian thực của máy chủ, cơ sở dữ liệu và các tích hợp nền tảng.'
              : 'Real-time health telemetry across backend API, database, and background integrations.'}
          </p>
        </div>

        <div className="flex items-center gap-3">
          {lastChecked && (
            <span className="text-[11px] text-slate-400 font-mono">
              {isVi ? 'Cập nhật lúc: ' : 'Checked: '}
              {lastChecked.toLocaleTimeString(locale)}
            </span>
          )}
          <button
            type="button"
            onClick={fetchHealth}
            disabled={isLoading}
            className="inline-flex items-center gap-2 px-3 py-2 rounded-xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 text-xs font-semibold text-slate-700 dark:text-slate-300 hover:bg-slate-50 dark:hover:bg-slate-800 transition disabled:opacity-50"
          >
            <RefreshCw className={`w-3.5 h-3.5 ${isLoading ? 'animate-spin text-emerald-500' : ''}`} />
            <span>{isVi ? 'Kiểm tra ngay' : 'Refresh'}</span>
          </button>
        </div>
      </div>

      {/* Error state */}
      {loadError && (
        <div className="p-4 rounded-xl bg-rose-50 dark:bg-rose-950/30 border border-rose-200 dark:border-rose-900/40 text-xs text-rose-700 dark:text-rose-300 flex items-center justify-between">
          <div className="flex items-center gap-2.5">
            <AlertTriangle className="w-4 h-4 shrink-0 text-rose-500" />
            <span>{loadError}</span>
          </div>
          <button
            type="button"
            onClick={fetchHealth}
            className="px-3 py-1 bg-rose-100 dark:bg-rose-900/50 hover:bg-rose-200 text-rose-800 dark:text-rose-200 rounded-lg font-medium transition"
          >
            {isVi ? 'Thử lại' : 'Retry'}
          </button>
        </div>
      )}

      {/* Loading state */}
      {isLoading && !healthData && (
        <div className="p-12 text-center text-slate-400">
          <div className="inline-flex items-center gap-2 text-xs">
            <RefreshCw className="w-4 h-4 animate-spin text-emerald-500" />
            <span>{isVi ? 'Đang truy vấn tình trạng hệ thống...' : 'Querying system telemetry...'}</span>
          </div>
        </div>
      )}

      {/* Health Cards Grid */}
      {healthData && (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          {/* 1. Backend Core API */}
          {(() => {
            const badge = getStatusBadge(healthData.api);
            return (
              <div className="p-4 rounded-xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 shadow-xs space-y-3">
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-2.5">
                    <div className="p-2 rounded-lg bg-emerald-50 dark:bg-emerald-950/50 text-emerald-600 dark:text-emerald-400">
                      <Server className="w-4 h-4" />
                    </div>
                    <span className="text-xs font-bold text-slate-900 dark:text-white">
                      {isVi ? 'Máy chủ API chính' : 'Core Backend API'}
                    </span>
                  </div>
                  <span
                    className={`inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-xs font-semibold ${
                      badge.type === 'success'
                        ? 'bg-emerald-50 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-400 border border-emerald-200/60 dark:border-emerald-800/40'
                        : badge.type === 'danger'
                        ? 'bg-rose-50 text-rose-700 dark:bg-rose-950/40 dark:text-rose-400 border border-rose-200/60 dark:border-rose-800/40'
                        : 'bg-slate-100 text-slate-600 dark:bg-slate-800 dark:text-slate-400 border border-slate-200 dark:border-slate-700'
                    }`}
                  >
                    <span
                      className={`w-1.5 h-1.5 rounded-full ${
                        badge.type === 'success' ? 'bg-emerald-500' : badge.type === 'danger' ? 'bg-rose-500' : 'bg-slate-400'
                      }`}
                    />
                    {badge.label}
                  </span>
                </div>
                <p className="text-[11px] text-slate-500 dark:text-slate-400">
                  {isVi
                    ? 'Dịch vụ xử lý nghiệp vụ SpendWise và định tuyến API REST.'
                    : 'Main REST gateway & application logic handlers.'}
                </p>
              </div>
            );
          })()}

          {/* 2. SQL Server Database */}
          {(() => {
            const badge = getStatusBadge(healthData.sqlServer);
            return (
              <div className="p-4 rounded-xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 shadow-xs space-y-3">
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-2.5">
                    <div className="p-2 rounded-lg bg-blue-50 dark:bg-blue-950/50 text-blue-600 dark:text-blue-400">
                      <Database className="w-4 h-4" />
                    </div>
                    <span className="text-xs font-bold text-slate-900 dark:text-white">
                      {isVi ? 'Cơ sở dữ liệu SQL Server' : 'SQL Server Database'}
                    </span>
                  </div>
                  <span
                    className={`inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-xs font-semibold ${
                      badge.type === 'success'
                        ? 'bg-emerald-50 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-400 border border-emerald-200/60 dark:border-emerald-800/40'
                        : badge.type === 'danger'
                        ? 'bg-rose-50 text-rose-700 dark:bg-rose-950/40 dark:text-rose-400 border border-rose-200/60 dark:border-rose-800/40'
                        : 'bg-slate-100 text-slate-600 dark:bg-slate-800 dark:text-slate-400 border border-slate-200 dark:border-slate-700'
                    }`}
                  >
                    <span
                      className={`w-1.5 h-1.5 rounded-full ${
                        badge.type === 'success' ? 'bg-emerald-500' : badge.type === 'danger' ? 'bg-rose-500' : 'bg-slate-400'
                      }`}
                    />
                    {badge.label}
                  </span>
                </div>
                <p className="text-[11px] text-slate-500 dark:text-slate-400">
                  {isVi
                    ? 'Lưu trữ giao dịch, sổ cái kép kế toán và thông tin bảo mật.'
                    : 'Primary ACID datastore for ledger records & user data.'}
                </p>
              </div>
            );
          })()}

          {/* 3. AI Provider */}
          {(() => {
            const badge = getStatusBadge(healthData.aiProvider);
            return (
              <div className="p-4 rounded-xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 shadow-xs space-y-3">
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-2.5">
                    <div className="p-2 rounded-lg bg-indigo-50 dark:bg-indigo-950/50 text-indigo-600 dark:text-indigo-400">
                      <Cpu className="w-4 h-4" />
                    </div>
                    <span className="text-xs font-bold text-slate-900 dark:text-white">
                      {isVi ? 'Dịch vụ AI' : 'AI Provider'}
                    </span>
                  </div>
                  <span
                    className={`inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-xs font-semibold ${
                      badge.type === 'success'
                        ? 'bg-emerald-50 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-400 border border-emerald-200/60 dark:border-emerald-800/40'
                        : 'bg-slate-100 text-slate-600 dark:bg-slate-800 dark:text-slate-400 border border-slate-200 dark:border-slate-700'
                    }`}
                  >
                    <span
                      className={`w-1.5 h-1.5 rounded-full ${
                        badge.type === 'success' ? 'bg-emerald-500' : 'bg-slate-400'
                      }`}
                    />
                    {badge.label}
                  </span>
                </div>
                <p className="text-[11px] text-slate-500 dark:text-slate-400">
                  {isVi
                    ? 'Nhận diện hóa đơn OCR, phân loại chi tiêu và trợ lý tài chính thông minh.'
                    : 'Receipt OCR, smart categorization & financial assistant.'}
                </p>
              </div>
            );
          })()}

          {/* 4. SePay Gateway */}
          {(() => {
            const sepay = healthData.sePay;
            const isConfigured = sepay?.configured ?? false;
            return (
              <div className="p-4 rounded-xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 shadow-xs space-y-3">
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-2.5">
                    <div className="p-2 rounded-lg bg-amber-50 dark:bg-amber-950/50 text-amber-600 dark:text-amber-400">
                      <Building2 className="w-4 h-4" />
                    </div>
                    <span className="text-xs font-bold text-slate-900 dark:text-white">
                      {isVi ? 'Cổng SePay' : 'SePay Gateway'}
                    </span>
                  </div>
                  <span
                    className={`inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-xs font-semibold ${
                      isConfigured
                        ? 'bg-emerald-50 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-400 border border-emerald-200/60 dark:border-emerald-800/40'
                        : 'bg-amber-50 text-amber-700 dark:bg-amber-950/40 dark:text-amber-400 border border-amber-200/60 dark:border-amber-800/40'
                    }`}
                  >
                    <span
                      className={`w-1.5 h-1.5 rounded-full ${
                        isConfigured ? 'bg-emerald-500' : 'bg-amber-500'
                      }`}
                    />
                    {isConfigured
                      ? isVi
                        ? `Đã cấu hình (${sepay?.environment || 'sandbox'})`
                        : `Configured (${sepay?.environment || 'sandbox'})`
                      : isVi
                      ? 'Chưa cấu hình'
                      : 'Not Configured'}
                  </span>
                </div>
                <p className="text-[11px] text-slate-500 dark:text-slate-400">
                  {isVi
                    ? 'Xử lý nạp ví, webhook đối soát giao dịch ngân hàng tự động.'
                    : 'Webhook reconciliation and automated deposit processing.'}
                </p>
              </div>
            );
          })()}

          {/* 5. SignalR Realtime */}
          {(() => {
            const badge = getStatusBadge(healthData.signalR);
            return (
              <div className="p-4 rounded-xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 shadow-xs space-y-3">
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-2.5">
                    <div className="p-2 rounded-lg bg-teal-50 dark:bg-teal-950/50 text-teal-600 dark:text-teal-400">
                      <Radio className="w-4 h-4" />
                    </div>
                    <span className="text-xs font-bold text-slate-900 dark:text-white">
                      {isVi ? 'Thời gian thực (SignalR)' : 'Realtime (SignalR)'}
                    </span>
                  </div>
                  <span
                    className={`inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-xs font-semibold ${
                      badge.type === 'success'
                        ? 'bg-emerald-50 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-400 border border-emerald-200/60 dark:border-emerald-800/40'
                        : 'bg-slate-100 text-slate-600 dark:bg-slate-800 dark:text-slate-400 border border-slate-200 dark:border-slate-700'
                    }`}
                  >
                    <span
                      className={`w-1.5 h-1.5 rounded-full ${
                        badge.type === 'success' ? 'bg-emerald-500' : 'bg-slate-400'
                      }`}
                    />
                    {badge.label}
                  </span>
                </div>
                <p className="text-[11px] text-slate-500 dark:text-slate-400">
                  {isVi
                    ? 'Kênh kết nối WebSocket đẩy thông báo và cập nhật số dư tức thời.'
                    : 'Realtime WebSocket push notifications & instant transaction events.'}
                </p>
              </div>
            );
          })()}

          {/* 6. Notifications Outbox */}
          {(() => {
            const outbox = healthData.notificationsOutbox;
            const badge = getStatusBadge(outbox?.status);
            return (
              <div className="p-4 rounded-xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 shadow-xs space-y-3">
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-2.5">
                    <div className="p-2 rounded-lg bg-purple-50 dark:bg-purple-950/50 text-purple-600 dark:text-purple-400">
                      <Bell className="w-4 h-4" />
                    </div>
                    <span className="text-xs font-bold text-slate-900 dark:text-white">
                      {isVi ? 'Hàng đợi thông báo (Outbox)' : 'Notifications Outbox'}
                    </span>
                  </div>
                  <span
                    className={`inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-xs font-semibold ${
                      badge.type === 'success'
                        ? 'bg-emerald-50 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-400 border border-emerald-200/60 dark:border-emerald-800/40'
                        : 'bg-slate-100 text-slate-600 dark:bg-slate-800 dark:text-slate-400 border border-slate-200 dark:border-slate-700'
                    }`}
                  >
                    <span
                      className={`w-1.5 h-1.5 rounded-full ${
                        badge.type === 'success' ? 'bg-emerald-500' : 'bg-slate-400'
                      }`}
                    />
                    {badge.label}
                  </span>
                </div>
                <div className="flex items-center gap-3 text-[11px] text-slate-500 dark:text-slate-400">
                  <span>
                    {isVi ? 'Đang chờ: ' : 'Pending: '}
                    <strong className="font-mono text-slate-700 dark:text-slate-200">{outbox?.pending ?? 0}</strong>
                  </span>
                  <span>•</span>
                  <span>
                    {isVi ? 'Thất bại: ' : 'Failed: '}
                    <strong className={`font-mono ${(outbox?.failed ?? 0) > 0 ? 'text-rose-500 font-bold' : 'text-slate-700 dark:text-slate-200'}`}>
                      {outbox?.failed ?? 0}
                    </strong>
                  </span>
                </div>
              </div>
            );
          })()}

          {/* 7. Background & Recurring Processors */}
          {(() => {
            const badge = getStatusBadge(healthData.backgroundProcessing || healthData.recurringProcessor);
            return (
              <div className="p-4 rounded-xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 shadow-xs space-y-3">
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-2.5">
                    <div className="p-2 rounded-lg bg-orange-50 dark:bg-orange-950/50 text-orange-600 dark:text-orange-400">
                      <Clock className="w-4 h-4" />
                    </div>
                    <span className="text-xs font-bold text-slate-900 dark:text-white">
                      {isVi ? 'Tác vụ nền & Định kỳ' : 'Background & Recurring'}
                    </span>
                  </div>
                  <span
                    className={`inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-xs font-semibold ${
                      badge.type === 'success'
                        ? 'bg-emerald-50 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-400 border border-emerald-200/60 dark:border-emerald-800/40'
                        : 'bg-slate-100 text-slate-600 dark:bg-slate-800 dark:text-slate-400 border border-slate-200 dark:border-slate-700'
                    }`}
                  >
                    <span
                      className={`w-1.5 h-1.5 rounded-full ${
                        badge.type === 'success' ? 'bg-emerald-500' : 'bg-slate-400'
                      }`}
                    />
                    {badge.label}
                  </span>
                </div>
                <p className="text-[11px] text-slate-500 dark:text-slate-400">
                  {isVi
                    ? 'Xử lý các giao dịch định kỳ, tính toán hạn mức ngân sách và tự động đối soát.'
                    : 'Recurring budget recalculations and automated background routines.'}
                </p>
              </div>
            );
          })()}
        </div>
      )}

      {/* Audit Note */}
      <div className="p-3.5 rounded-xl bg-slate-50 dark:bg-slate-800/40 border border-slate-200 dark:border-slate-800 text-xs text-slate-600 dark:text-slate-400 flex items-center gap-2.5">
        <Info className="w-4 h-4 shrink-0 text-slate-400" />
        <span>
          {isVi
            ? 'Trạng thái trên được truy vấn trực tiếp từ GET /api/admin/health của máy chủ backend SpendWise.'
            : 'All health indicators reflect direct live telemetry returned by backend GET /api/admin/health.'}
        </span>
      </div>
    </div>
  );
};

export default AdminHealthPage;
