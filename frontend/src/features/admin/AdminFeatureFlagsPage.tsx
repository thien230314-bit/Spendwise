import React, { useState, useEffect, useCallback } from 'react';
import {
  ToggleLeft,
  ToggleRight,
  RefreshCw,
  AlertTriangle,
  CheckCircle2,
  Sliders,
  X,
  Info,
} from 'lucide-react';
import { useI18n } from '../../i18n/I18nContext';
import { casesApiService, FeatureFlagDto } from '../../services/casesApiService';
import { EmptyState } from '../../components/ui/EmptyState';

export const AdminFeatureFlagsPage: React.FC = () => {
  const { t, locale } = useI18n();
  const isVi = locale === 'vi-VN';

  const [flags, setFlags] = useState<FeatureFlagDto[]>([]);
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [toastMessage, setToastMessage] = useState<string | null>(null);

  // Toggle Modal State
  const [targetFlag, setTargetFlag] = useState<FeatureFlagDto | null>(null);
  const [targetState, setTargetState] = useState<boolean>(false);
  const [reason, setReason] = useState('');
  const [actionError, setActionError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const showToast = (msg: string) => {
    setToastMessage(msg);
    setTimeout(() => setToastMessage(null), 3500);
  };

  const fetchFlags = useCallback(async () => {
    setIsLoading(true);
    setLoadError(null);
    try {
      const data = await casesApiService.getAdminFeatureFlags();
      setFlags(data);
    } catch (err: any) {
      setLoadError(
        err?.message || (isVi ? 'Không thể tải danh sách cờ tính năng từ máy chủ.' : 'Failed to load feature flags from server.')
      );
    } finally {
      setIsLoading(false);
    }
  }, [isVi]);

  useEffect(() => {
    fetchFlags();
  }, [fetchFlags]);

  const openToggleModal = (flag: FeatureFlagDto) => {
    setTargetFlag(flag);
    setTargetState(!flag.isEnabled);
    setReason('');
    setActionError(null);
    setIsSubmitting(false);
  };

  const closeToggleModal = () => {
    setTargetFlag(null);
    setReason('');
    setActionError(null);
    setIsSubmitting(false);
  };

  const handleConfirmToggle = async () => {
    if (!targetFlag) return;
    const trimmedReason = reason.trim();
    if (!trimmedReason) {
      setActionError(
        isVi ? 'Vui lòng nhập lý do điều chỉnh cờ tính năng.' : 'Please enter a reason for modifying this feature flag.'
      );
      return;
    }

    setIsSubmitting(true);
    setActionError(null);
    try {
      const updated = await casesApiService.updateAdminFeatureFlag(targetFlag.name, {
        isEnabled: targetState,
        reason: trimmedReason,
      });

      setFlags((prev) => prev.map((f) => (f.name === updated.name ? updated : f)));
      showToast(
        isVi
          ? `Đã ${targetState ? 'bật' : 'tắt'} tính năng "${targetFlag.name}".`
          : `Feature "${targetFlag.name}" ${targetState ? 'enabled' : 'disabled'}.`
      );
      closeToggleModal();
    } catch (err: any) {
      setActionError(
        err?.message || (isVi ? 'Lỗi khi cập nhật cờ tính năng.' : 'Failed to update feature flag.')
      );
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div id="admin-feature-flags-page" className="space-y-6">
      {/* Toast Notification */}
      {toastMessage && (
        <div className="fixed bottom-6 right-6 z-50 flex items-center gap-2 px-4 py-3 rounded-xl bg-slate-900 text-white shadow-xl border border-emerald-500/50 text-xs font-semibold animate-in fade-in slide-in-from-bottom-2">
          <CheckCircle2 className="w-4 h-4 text-emerald-400" />
          {toastMessage}
        </div>
      )}

      {/* Header */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <h2 className="text-xl font-bold text-slate-900 dark:text-white tracking-tight">
            {t('adminFeatureFlags.title')}
          </h2>
          <p className="text-xs text-slate-500 dark:text-slate-400 mt-1 max-w-2xl leading-relaxed">
            {isVi
              ? 'Bật hoặc tắt các tính năng hệ thống có thẩm quyền từ máy chủ.'
              : 'Enable or disable authoritative system feature flags directly on the backend.'}
          </p>
        </div>

        <button
          type="button"
          onClick={fetchFlags}
          disabled={isLoading}
          className="inline-flex items-center gap-2 px-3 py-2 rounded-xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 text-xs font-semibold text-slate-700 dark:text-slate-300 hover:bg-slate-50 dark:hover:bg-slate-800 transition disabled:opacity-50"
        >
          <RefreshCw className={`w-3.5 h-3.5 ${isLoading ? 'animate-spin text-emerald-500' : ''}`} />
          <span>{isVi ? 'Làm mới' : 'Refresh'}</span>
        </button>
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
            onClick={fetchFlags}
            className="px-3 py-1 bg-rose-100 dark:bg-rose-900/50 hover:bg-rose-200 text-rose-800 dark:text-rose-200 rounded-lg font-medium transition"
          >
            {isVi ? 'Thử lại' : 'Retry'}
          </button>
        </div>
      )}

      {/* Flags List */}
      {isLoading && flags.length === 0 ? (
        <div className="p-12 text-center text-slate-400">
          <div className="inline-flex items-center gap-2 text-xs">
            <RefreshCw className="w-4 h-4 animate-spin text-emerald-500" />
            <span>{isVi ? 'Đang tải danh sách cờ tính năng...' : 'Loading feature flags...'}</span>
          </div>
        </div>
      ) : flags.length === 0 ? (
        <EmptyState
          icon={Sliders}
          title={isVi ? 'Không có cờ tính năng nào' : 'No feature flags available'}
          description={
            isVi
              ? 'Hiện tại hệ thống chưa đăng ký cờ tính năng động nào từ backend.'
              : 'No dynamic feature flags are currently configured on the backend.'
          }
        />
      ) : (
        <div className="rounded-xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 divide-y divide-slate-100 dark:divide-slate-800 overflow-hidden shadow-xs">
          {flags.map((flag) => (
            <div
              key={flag.name}
              className="p-4 sm:p-5 flex flex-col sm:flex-row sm:items-center justify-between gap-4 hover:bg-slate-50/50 dark:hover:bg-slate-800/30 transition"
            >
              <div className="space-y-1 max-w-xl">
                <div className="flex items-center gap-2.5">
                  <span className="text-sm font-bold text-slate-900 dark:text-white font-mono">
                    {flag.name}
                  </span>
                  <span
                    className={`inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-semibold ${
                      flag.isEnabled
                        ? 'bg-emerald-50 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-400 border border-emerald-200/60 dark:border-emerald-800/40'
                        : 'bg-slate-100 text-slate-600 dark:bg-slate-800 dark:text-slate-400 border border-slate-200 dark:border-slate-700'
                    }`}
                  >
                    {flag.isEnabled
                      ? (isVi ? 'Đang bật' : 'Enabled')
                      : (isVi ? 'Đang tắt' : 'Disabled')}
                  </span>
                </div>
                <p className="text-xs text-slate-500 dark:text-slate-400 leading-relaxed">
                  {flag.description || (isVi ? 'Không có mô tả chi tiết.' : 'No description provided.')}
                </p>
              </div>

              <div>
                <button
                  type="button"
                  onClick={() => openToggleModal(flag)}
                  className={`inline-flex items-center gap-2 px-3.5 py-2 rounded-xl text-xs font-semibold transition cursor-pointer border ${
                    flag.isEnabled
                      ? 'bg-rose-50 text-rose-700 dark:bg-rose-950/40 dark:text-rose-300 border-rose-200 dark:border-rose-800/50 hover:bg-rose-100'
                      : 'bg-emerald-50 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-300 border-emerald-200 dark:border-emerald-800/50 hover:bg-emerald-100'
                  }`}
                >
                  {flag.isEnabled ? (
                    <>
                      <ToggleRight className="w-4 h-4" />
                      <span>{isVi ? 'Tắt tính năng' : 'Disable'}</span>
                    </>
                  ) : (
                    <>
                      <ToggleLeft className="w-4 h-4" />
                      <span>{isVi ? 'Bật tính năng' : 'Enable'}</span>
                    </>
                  )}
                </button>
              </div>
            </div>
          ))}
        </div>
      )}

      {/* Info note */}
      <div className="p-3.5 rounded-xl bg-slate-50 dark:bg-slate-800/40 border border-slate-200 dark:border-slate-800 text-xs text-slate-600 dark:text-slate-400 flex items-center gap-2.5">
        <Info className="w-4 h-4 shrink-0 text-slate-400" />
        <span>
          {isVi
            ? 'Thay đổi trạng thái cờ tính năng sẽ lập tức áp dụng cho toàn hệ thống và ghi vào nhật ký kiểm toán quản trị.'
            : 'Modifying feature flags takes effect system-wide immediately and is permanently logged in the admin audit trail.'}
        </span>
      </div>

      {/* Toggle Confirmation Modal */}
      {targetFlag && (
        <div className="fixed inset-0 z-60 flex items-center justify-center p-4 bg-black/60 backdrop-blur-xs">
          <div className="w-full max-w-md rounded-2xl bg-white dark:bg-[#151D2F] border border-slate-200 dark:border-slate-800 p-6 shadow-2xl space-y-4 animate-in fade-in-0 zoom-in-95">
            <div className="flex items-center justify-between border-b border-slate-100 dark:border-slate-800 pb-3">
              <div className="flex items-center gap-2.5">
                <div className="w-9 h-9 rounded-xl bg-slate-100 dark:bg-slate-800 flex items-center justify-center text-slate-700 dark:text-slate-300">
                  <Sliders className="w-5 h-5" />
                </div>
                <div>
                  <h3 className="text-sm font-bold text-slate-900 dark:text-white">
                    {targetState
                      ? (isVi ? 'Bật cờ tính năng' : 'Enable Feature Flag')
                      : (isVi ? 'Tắt cờ tính năng' : 'Disable Feature Flag')}
                  </h3>
                  <p className="text-[11px] text-slate-500 dark:text-slate-400 font-mono">
                    {targetFlag.name}
                  </p>
                </div>
              </div>
              <button
                type="button"
                onClick={closeToggleModal}
                className="p-1 rounded-lg text-slate-400 hover:text-slate-600 dark:hover:text-slate-200"
              >
                <X className="w-5 h-5" />
              </button>
            </div>

            <p className="text-xs text-slate-600 dark:text-slate-300 leading-relaxed">
              {isVi
                ? `Bạn đang chuẩn bị ${targetState ? 'kích hoạt' : 'vô hiệu hóa'} cờ tính năng "${targetFlag.name}". Thao tác này yêu cầu nhập lý do ghi audit.`
                : `You are about to ${targetState ? 'enable' : 'disable'} feature flag "${targetFlag.name}". This action requires an audit justification reason.`}
            </p>

            {/* Mandatory Reason */}
            <div className="space-y-1.5">
              <label className="block text-xs font-semibold text-slate-700 dark:text-slate-300">
                {isVi ? 'Lý do thay đổi' : 'Modification Reason'}{' '}
                <span className="text-rose-500">*</span>
              </label>
              <textarea
                value={reason}
                onChange={(e) => {
                  setReason(e.target.value);
                  if (actionError) setActionError(null);
                }}
                rows={2}
                placeholder={isVi ? 'Nhập lý do thay đổi cờ tính năng (bắt buộc)...' : 'Enter justification reason...'}
                className="w-full p-2.5 text-xs rounded-xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-900 text-slate-900 dark:text-slate-100 placeholder:text-slate-400 focus:outline-hidden focus:ring-1 focus:ring-emerald-500"
              />
            </div>

            {/* Error message */}
            {actionError && (
              <div className="p-2.5 rounded-xl bg-rose-50 dark:bg-rose-950/40 border border-rose-200 dark:border-rose-900/50 text-rose-600 dark:text-rose-400 text-xs flex items-center gap-2">
                <AlertTriangle className="w-4 h-4 shrink-0" />
                <span>{actionError}</span>
              </div>
            )}

            {/* Footer Buttons */}
            <div className="flex items-center gap-2 pt-2">
              <button
                type="button"
                onClick={closeToggleModal}
                disabled={isSubmitting}
                className="flex-1 py-2.5 px-3 rounded-xl border border-slate-200 dark:border-slate-800 text-xs font-semibold text-slate-700 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800 transition"
              >
                {t('common.cancel')}
              </button>
              <button
                type="button"
                onClick={handleConfirmToggle}
                disabled={isSubmitting || !reason.trim()}
                className={`flex-1 py-2.5 px-3 rounded-xl text-xs font-semibold text-white shadow-sm transition disabled:opacity-50 disabled:cursor-not-allowed ${
                  targetState
                    ? 'bg-emerald-600 hover:bg-emerald-700'
                    : 'bg-rose-600 hover:bg-rose-700'
                }`}
              >
                {isSubmitting
                  ? (isVi ? 'Đang xử lý...' : 'Processing...')
                  : (isVi ? 'Xác nhận' : 'Confirm')}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

export default AdminFeatureFlagsPage;
