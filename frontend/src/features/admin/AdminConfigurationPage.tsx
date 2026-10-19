import React, { useState, useEffect, useCallback } from 'react';
import {
  Sliders,
  Construction,
  ShieldCheck,
  Megaphone,
  AlertTriangle,
  Send,
  XCircle,
  Clock,
  CheckCircle2,
  Server,
  Info,
  X,
  RefreshCw,
} from 'lucide-react';
import { useI18n } from '../../i18n/I18nContext';
import { useAuth } from '../../providers/AuthProvider';
import { useSecurityPinChallenge } from '../../providers/SecurityPinChallengeProvider';
import {
  systemOperationsService,
  SystemMaintenanceState,
  EmergencyAnnouncement,
} from '../../services/systemOperationsService';

export const AdminConfigurationPage: React.FC = () => {
  const { t, locale } = useI18n();
  const { currentUser } = useAuth();
  const { challengePinWithValue } = useSecurityPinChallenge();
  const isVi = locale === 'vi-VN';

  // System Maintenance State
  const [maintenanceState, setMaintenanceState] = useState<SystemMaintenanceState>(() => {
    return systemOperationsService.getMaintenanceState();
  });

  // Emergency Announcements State
  const [activeAnnouncement, setActiveAnnouncement] = useState<EmergencyAnnouncement | null>(() => {
    return systemOperationsService.getActiveAnnouncement();
  });

  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [pageError, setPageError] = useState<string | null>(null);

  // Form State for New Announcement
  const [newTitle, setNewTitle] = useState('');
  const [newDescription, setNewDescription] = useState('');
  const [newReason, setNewReason] = useState('');
  const [formError, setFormError] = useState<string | null>(null);
  const [actionSuccessMsg, setActionSuccessMsg] = useState<string | null>(null);
  const [isSubmittingAnnouncement, setIsSubmittingAnnouncement] = useState(false);

  // Maintenance Toggle Confirmation Dialog State
  const [maintenanceConfirmOpen, setMaintenanceConfirmOpen] = useState(false);
  const [maintenanceReason, setMaintenanceReason] = useState('');
  const [maintenanceDialogError, setMaintenanceDialogError] = useState<string | null>(null);
  const [isSubmittingMaintenance, setIsSubmittingMaintenance] = useState(false);

  // Publish Confirmation Dialog State
  const [publishConfirmOpen, setPublishConfirmOpen] = useState(false);
  const [publishDialogError, setPublishDialogError] = useState<string | null>(null);

  // Deactivate Confirmation Dialog State
  const [deactivateConfirmId, setDeactivateConfirmId] = useState<string | null>(null);
  const [deactivateReason, setDeactivateReason] = useState('');
  const [deactivateDialogError, setDeactivateDialogError] = useState<string | null>(null);
  const [isSubmittingDeactivate, setIsSubmittingDeactivate] = useState(false);

  const fetchInitialData = useCallback(async () => {
    setIsLoading(true);
    setPageError(null);
    try {
      const res = await systemOperationsService.refreshState();
      setMaintenanceState(res.maintenance);
      setActiveAnnouncement(res.activeAnnouncement);
    } catch (err: any) {
      setPageError(
        err?.message ||
          (isVi
            ? 'Không thể tải trạng thái vận hành từ máy chủ.'
            : 'Failed to load system operations state from server.')
      );
    } finally {
      setIsLoading(false);
    }
  }, [isVi]);

  useEffect(() => {
    fetchInitialData();

    const unsubMaintenance = systemOperationsService.subscribeMaintenance((newState) => {
      setMaintenanceState(newState);
    });

    const unsubAnnouncements = systemOperationsService.subscribeAnnouncements(() => {
      setActiveAnnouncement(systemOperationsService.getActiveAnnouncement());
    });

    return () => {
      unsubMaintenance();
      unsubAnnouncements();
    };
  }, [fetchInitialData]);

  const showFeedback = (msg: string) => {
    setActionSuccessMsg(msg);
    setTimeout(() => {
      setActionSuccessMsg(null);
    }, 4500);
  };

  // ---------------------------------------------------------------------------
  // TOGGLE MAINTENANCE
  // ---------------------------------------------------------------------------
  const handleInitiateToggleMaintenance = () => {
    setMaintenanceReason('');
    setMaintenanceDialogError(null);
    setMaintenanceConfirmOpen(true);
  };

  const handleConfirmToggleMaintenance = async () => {
    if (!currentUser || isSubmittingMaintenance) return;
    const trimmedReason = maintenanceReason.trim();
    if (!trimmedReason) {
      setMaintenanceDialogError(
        isVi
          ? 'Vui lòng nhập lý do thay đổi chế độ bảo trì.'
          : 'Please provide a reason for changing maintenance mode.'
      );
      return;
    }

    const targetState = !maintenanceState.enabled;

    // Challenge Admin for their actual Security PIN
    const enteredPin = await challengePinWithValue({
      actionName: targetState
        ? (isVi ? 'Kích hoạt bảo trì hệ thống' : 'Enable System Maintenance')
        : (isVi ? 'Tắt chế độ bảo trì hệ thống' : 'Disable System Maintenance'),
      title: targetState
        ? (isVi ? 'Bật chế độ bảo trì hệ thống' : 'Enable Maintenance Mode')
        : (isVi ? 'Tắt chế độ bảo trì hệ thống' : 'Disable Maintenance Mode'),
      description: targetState
        ? (isVi
            ? 'Nhập mã bảo mật Quản trị viên 6 chữ số để xác nhận bật chế độ bảo trì. Người dùng thông thường sẽ tạm dừng truy cập.'
            : 'Enter 6-digit Admin Security PIN. Normal users will be temporarily restricted with a maintenance notice.')
        : (isVi
            ? 'Nhập mã bảo mật Quản trị viên 6 chữ số để mở lại toàn bộ quyền truy cập cho người dùng thông thường.'
            : 'Enter 6-digit Admin Security PIN to restore normal user access.'),
    });

    if (!enteredPin) {
      // User cancelled PIN modal
      return;
    }

    setIsSubmittingMaintenance(true);
    setMaintenanceDialogError(null);

    try {
      const updatedState = await systemOperationsService.changeMaintenance({
        isEnabled: targetState,
        securityPin: enteredPin,
        reason: trimmedReason,
      });

      setMaintenanceState(updatedState);
      setMaintenanceConfirmOpen(false);
      setMaintenanceReason('');
      showFeedback(
        targetState
          ? (isVi
              ? 'Đã bật chế độ bảo trì hệ thống thành công.'
              : 'System maintenance enabled successfully.')
          : (isVi
              ? 'Đã tắt chế độ bảo trì hệ thống. Người dùng có thể truy cập bình thường.'
              : 'System maintenance disabled. Normal access restored.')
      );
    } catch (err: any) {
      setMaintenanceDialogError(
        err?.message ||
          (isVi
            ? 'Không thể cập nhật chế độ bảo trì. Vui lòng kiểm tra lại mã PIN và thử lại.'
            : 'Failed to update maintenance mode. Please verify PIN and try again.')
      );
    } finally {
      setIsSubmittingMaintenance(false);
    }
  };

  // ---------------------------------------------------------------------------
  // PUBLISH EMERGENCY ANNOUNCEMENT
  // ---------------------------------------------------------------------------
  const handleInitiatePublishAnnouncement = (e: React.FormEvent) => {
    e.preventDefault();
    if (!currentUser) return;

    const trimmedTitle = newTitle.trim();
    const trimmedDesc = newDescription.trim();
    const trimmedReason = newReason.trim();

    if (!trimmedTitle || !trimmedDesc || !trimmedReason) {
      setFormError(
        isVi
          ? 'Vui lòng nhập đầy đủ tiêu đề, nội dung và lý do phát hành thông báo khẩn cấp.'
          : 'Please provide title, details, and reason for publishing the announcement.'
      );
      return;
    }

    setFormError(null);
    setPublishDialogError(null);
    setPublishConfirmOpen(true);
  };

  const handleConfirmPublishAnnouncement = async () => {
    if (!currentUser || isSubmittingAnnouncement) return;

    const trimmedTitle = newTitle.trim();
    const trimmedDesc = newDescription.trim();
    const trimmedReason = newReason.trim();

    const enteredPin = await challengePinWithValue({
      actionName: isVi ? 'Gửi thông báo khẩn' : 'Publish Emergency Announcement',
      title: isVi ? 'Xác thực gửi thông báo toàn hệ thống' : 'Confirm System-wide Announcement',
      description: isVi
        ? 'Nhập mã bảo mật Quản trị viên 6 chữ số để hiển thị thông báo khẩn tới toàn bộ người dùng.'
        : 'Enter 6-digit Admin Security PIN to display this emergency announcement to all users.',
    });

    if (!enteredPin) {
      return;
    }

    setIsSubmittingAnnouncement(true);
    setPublishDialogError(null);

    try {
      const created = await systemOperationsService.publishAnnouncement({
        title: trimmedTitle,
        description: trimmedDesc,
        securityPin: enteredPin,
        reason: trimmedReason,
      });

      setActiveAnnouncement(created);
      setPublishConfirmOpen(false);
      setNewTitle('');
      setNewDescription('');
      setNewReason('');
      showFeedback(
        isVi
          ? 'Đã gửi thông báo khẩn thành công tới toàn hệ thống.'
          : 'Emergency announcement published successfully to all users.'
      );
    } catch (err: any) {
      setPublishDialogError(
        err?.message ||
          (isVi
            ? 'Không thể phát hành thông báo khẩn. Vui lòng kiểm tra lại mã PIN và dữ liệu nhập.'
            : 'Failed to publish announcement. Please check PIN and input fields.')
      );
    } finally {
      setIsSubmittingAnnouncement(false);
    }
  };

  // ---------------------------------------------------------------------------
  // DEACTIVATE ACTIVE ANNOUNCEMENT
  // ---------------------------------------------------------------------------
  const handleInitiateDeactivateAnnouncement = (id: string) => {
    setDeactivateConfirmId(id);
    setDeactivateReason('');
    setDeactivateDialogError(null);
  };

  const handleConfirmDeactivateAnnouncement = async () => {
    if (!currentUser || !deactivateConfirmId || isSubmittingDeactivate) return;
    const trimmedReason = deactivateReason.trim();
    if (!trimmedReason) {
      setDeactivateDialogError(
        isVi
          ? 'Vui lòng nhập lý do gỡ thông báo khẩn.'
          : 'Please provide a reason for deactivating the announcement.'
      );
      return;
    }

    const targetId = deactivateConfirmId;

    const enteredPin = await challengePinWithValue({
      actionName: isVi ? 'Gỡ thông báo khẩn' : 'Deactivate Announcement',
      title: isVi ? 'Kết thúc thông báo khẩn' : 'End Emergency Announcement',
      description: isVi
        ? 'Nhập mã bảo mật Quản trị viên 6 chữ số để dừng hiển thị thông báo này.'
        : 'Enter 6-digit Admin Security PIN to deactivate this emergency announcement.',
    });

    if (!enteredPin) {
      return;
    }

    setIsSubmittingDeactivate(true);
    setDeactivateDialogError(null);

    try {
      await systemOperationsService.deactivateAnnouncement(targetId, {
        securityPin: enteredPin,
        reason: trimmedReason,
      });

      setActiveAnnouncement(null);
      setDeactivateConfirmId(null);
      setDeactivateReason('');
      showFeedback(
        isVi
          ? 'Đã gỡ thông báo khẩn thành công.'
          : 'Emergency announcement deactivated successfully.'
      );
    } catch (err: any) {
      setDeactivateDialogError(
        err?.message ||
          (isVi
            ? 'Không thể gỡ thông báo khẩn. Vui lòng kiểm tra lại mã PIN.'
            : 'Failed to deactivate announcement. Please verify your PIN.')
      );
    } finally {
      setIsSubmittingDeactivate(false);
    }
  };

  const formattedMaintenanceDate = maintenanceState.changedAt
    ? new Date(maintenanceState.changedAt).toLocaleString(isVi ? 'vi-VN' : 'en-US', {
        dateStyle: 'medium',
        timeStyle: 'short',
      })
    : null;

  return (
    <div id="admin-configuration-page" className="space-y-8 max-w-5xl">
      {/* Top Page Header */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <div className="flex items-center gap-2 text-slate-500 dark:text-slate-400 text-xs font-semibold uppercase tracking-wider">
            <Sliders className="h-4 w-4 text-emerald-600 dark:text-emerald-400" />
            <span>{isVi ? 'Cấu hình hệ thống' : 'System Configuration'}</span>
          </div>
          <h2 className="text-xl sm:text-2xl font-bold text-slate-900 dark:text-white tracking-tight mt-1">
            {t('adminConfig.title')}
          </h2>
          <p className="text-xs sm:text-sm text-slate-500 dark:text-slate-400 mt-1 leading-relaxed">
            {isVi
              ? 'Cấu hình chế độ bảo trì hệ thống và thông báo khẩn tới người dùng. Mọi thao tác đều yêu cầu lý do ghi audit và xác thực mã PIN Quản trị viên.'
              : 'Configure system maintenance mode and emergency announcements. All actions require audit justification and Admin Security PIN verification.'}
          </p>
        </div>

        <button
          type="button"
          onClick={fetchInitialData}
          disabled={isLoading}
          className="inline-flex items-center gap-2 px-3 py-2 rounded-xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 text-xs font-semibold text-slate-700 dark:text-slate-300 hover:bg-slate-50 dark:hover:bg-slate-800 transition disabled:opacity-50 shrink-0 self-start sm:self-center"
        >
          <RefreshCw className={`w-3.5 h-3.5 ${isLoading ? 'animate-spin text-emerald-500' : ''}`} />
          <span>{isVi ? 'Làm mới' : 'Refresh'}</span>
        </button>
      </div>

      {/* Page Error Banner */}
      {pageError && (
        <div className="p-4 rounded-xl bg-rose-50 dark:bg-rose-950/30 border border-rose-200 dark:border-rose-900/40 text-xs text-rose-700 dark:text-rose-300 flex items-center justify-between">
          <div className="flex items-center gap-2.5">
            <AlertTriangle className="w-4 h-4 shrink-0 text-rose-500" />
            <span>{pageError}</span>
          </div>
          <button
            type="button"
            onClick={fetchInitialData}
            className="px-3 py-1 bg-rose-100 dark:bg-rose-900/50 hover:bg-rose-200 text-rose-800 dark:text-rose-200 rounded-lg font-medium transition"
          >
            {isVi ? 'Thử lại' : 'Retry'}
          </button>
        </div>
      )}

      {/* Success / Feedback notification */}
      {actionSuccessMsg && (
        <div className="p-3.5 rounded-xl bg-emerald-50 dark:bg-emerald-950/40 border border-emerald-200 dark:border-emerald-800 text-emerald-800 dark:text-emerald-300 text-xs font-medium flex items-center gap-2.5 animate-in fade-in slide-in-from-top-2">
          <CheckCircle2 className="h-4 w-4 text-emerald-600 dark:text-emerald-400 shrink-0" />
          <span>{actionSuccessMsg}</span>
        </div>
      )}

      {/* ========================================================================= */}
      {/* SECTION 1: SYSTEM MAINTENANCE MODE                                       */}
      {/* ========================================================================= */}
      <div className="bg-white dark:bg-[#151D2F] rounded-2xl border border-slate-200 dark:border-slate-800 p-6 shadow-xs space-y-6">
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 border-b border-slate-100 dark:border-slate-800 pb-5">
          <div className="space-y-1">
            <div className="flex items-center gap-2.5">
              <div
                className={`p-2 rounded-xl ${
                  maintenanceState.enabled
                    ? 'bg-amber-100 dark:bg-amber-950/50 text-amber-600 dark:text-amber-400'
                    : 'bg-emerald-100 dark:bg-emerald-950/50 text-emerald-600 dark:text-emerald-400'
                }`}
              >
                {maintenanceState.enabled ? (
                  <Construction className="h-5 w-5" />
                ) : (
                  <ShieldCheck className="h-5 w-5" />
                )}
              </div>
              <div>
                <h3 className="text-base font-bold text-slate-900 dark:text-white">
                  {isVi ? 'Bảo trì hệ thống' : 'System Maintenance'}
                </h3>
                <p className="text-xs text-slate-500 dark:text-slate-400">
                  {isVi
                    ? 'Tạm thời giới hạn người dùng truy cập khi cần cập nhật hệ thống'
                    : 'Temporarily limit user access during system updates'}
                </p>
              </div>
            </div>
          </div>

          {/* Current Status Pill & Switch */}
          <div className="flex items-center gap-3">
            <div
              className={`inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-xs font-semibold ${
                maintenanceState.enabled
                  ? 'bg-amber-100 dark:bg-amber-950/70 text-amber-800 dark:text-amber-300 border border-amber-300 dark:border-amber-800'
                  : 'bg-emerald-100 dark:bg-emerald-950/70 text-emerald-800 dark:text-emerald-300 border border-emerald-300 dark:border-emerald-800'
              }`}
            >
              <span
                className={`h-2 w-2 rounded-full ${
                  maintenanceState.enabled ? 'bg-amber-500 animate-ping' : 'bg-emerald-500'
                }`}
              />
              <span>
                {maintenanceState.enabled
                  ? isVi
                    ? 'Đang bật bảo trì'
                    : 'Maintenance Active'
                  : isVi
                  ? 'Hoạt động bình thường'
                  : 'Normal Operations'}
              </span>
            </div>

            {/* Switch Control */}
            <div className="flex items-center gap-3">
              <span className="text-xs font-semibold text-slate-600 dark:text-slate-300">
                {maintenanceState.enabled
                  ? (isVi ? 'BẬT' : 'ON')
                  : (isVi ? 'TẮT' : 'OFF')}
              </span>
              <button
                type="button"
                role="switch"
                aria-checked={maintenanceState.enabled}
                id="switch-system-maintenance"
                onClick={handleInitiateToggleMaintenance}
                className={`relative inline-flex h-7 w-14 shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors duration-200 ease-in-out focus:outline-hidden focus:ring-2 focus:ring-amber-500 focus:ring-offset-2 ${
                  maintenanceState.enabled ? 'bg-amber-600' : 'bg-slate-300 dark:bg-slate-700'
                }`}
              >
                <span className="sr-only">Toggle maintenance mode</span>
                <span
                  className={`pointer-events-none inline-block h-6 w-6 transform rounded-full bg-white shadow-md ring-0 transition duration-200 ease-in-out flex items-center justify-center text-[10px] font-bold ${
                    maintenanceState.enabled
                      ? 'translate-x-7 text-amber-700'
                      : 'translate-x-0 text-slate-600'
                  }`}
                >
                  {maintenanceState.enabled ? 'ON' : 'OFF'}
                </span>
              </button>
            </div>
          </div>
        </div>

        {/* Informative Guidance */}
        <div className="grid grid-cols-1 md:grid-cols-3 gap-4 text-xs text-slate-600 dark:text-slate-400">
          <div className="p-3.5 rounded-xl bg-slate-50 dark:bg-slate-900/50 border border-slate-100 dark:border-slate-800/80 space-y-1">
            <span className="font-semibold text-slate-800 dark:text-slate-200 block">
              {isVi ? 'Quyền truy cập người dùng:' : 'Normal User Access:'}
            </span>
            <p className="text-[11px] leading-relaxed">
              {maintenanceState.enabled
                ? isVi
                  ? 'Người dùng thông thường bị chặn và hiển thị màn hình thông báo bảo trì.'
                  : 'Normal users are blocked with an informative maintenance screen.'
                : isVi
                ? 'Tất cả người dùng truy cập bình thường và thực hiện giao dịch.'
                : 'All users can access dashboards and perform financial transactions.'}
            </p>
          </div>

          <div className="p-3.5 rounded-xl bg-slate-50 dark:bg-slate-900/50 border border-slate-100 dark:border-slate-800/80 space-y-1">
            <span className="font-semibold text-slate-800 dark:text-slate-200 block">
              {isVi ? 'Quyền quản trị viên:' : 'Administrator Access:'}
            </span>
            <p className="text-[11px] leading-relaxed">
              {isVi
                ? 'Quản trị viên luôn duy trì quyền truy cập đầy đủ vào bảng điều khiển quản trị.'
                : 'System Administrators retain 100% unrestricted access to all admin tools.'}
            </p>
          </div>

          <div className="p-3.5 rounded-xl bg-slate-50 dark:bg-slate-900/50 border border-slate-100 dark:border-slate-800/80 space-y-1">
            <span className="font-semibold text-slate-800 dark:text-slate-200 block">
              {isVi ? 'Nhật ký thay đổi:' : 'Last Modified:'}
            </span>
            <p className="text-[11px] font-mono text-slate-500 dark:text-slate-400">
              {formattedMaintenanceDate ? `${formattedMaintenanceDate}` : isVi ? 'Chưa có nhật ký' : 'Never'}
            </p>
          </div>
        </div>
      </div>

      {/* ========================================================================= */}
      {/* SECTION 2: EMERGENCY SYSTEM ANNOUNCEMENTS                                */}
      {/* ========================================================================= */}
      <div className="bg-white dark:bg-[#151D2F] rounded-2xl border border-slate-200 dark:border-slate-800 p-6 shadow-xs space-y-6">
        <div className="flex items-center justify-between border-b border-slate-100 dark:border-slate-800 pb-4">
          <div className="flex items-center gap-2.5">
            <div className="p-2 rounded-xl bg-rose-100 dark:bg-rose-950/50 text-rose-600 dark:text-rose-400">
              <Megaphone className="h-5 w-5" />
            </div>
            <div>
              <h3 className="text-base font-bold text-slate-900 dark:text-white">
                {isVi ? 'Thông báo khẩn' : 'Emergency Announcements'}
              </h3>
              <p className="text-xs text-slate-500 dark:text-slate-400">
                {isVi
                  ? 'Hiển thị biểu ngữ thông báo khẩn cấp có thẩm quyền trên màn hình của người dùng'
                  : 'Display an authoritative urgent announcement banner across user screens'}
              </p>
            </div>
          </div>
        </div>

        {/* Active Announcement Card */}
        {activeAnnouncement ? (
          <div className="p-4 rounded-xl bg-rose-50 dark:bg-rose-950/40 border border-rose-200 dark:border-rose-900/50 space-y-3">
            <div className="flex items-start justify-between gap-3">
              <div className="space-y-1">
                <div className="flex items-center gap-2">
                  <span className="text-[10px] font-bold uppercase tracking-wider px-2 py-0.5 rounded-sm bg-rose-600 text-white">
                    {isVi ? 'Đang hiển thị' : 'Active Announcement'}
                  </span>
                  <h4 className="text-sm font-bold text-slate-900 dark:text-white">
                    {activeAnnouncement.title}
                  </h4>
                </div>
                <p className="text-xs text-slate-700 dark:text-slate-300 leading-relaxed">
                  {activeAnnouncement.description}
                </p>
                <div className="flex items-center gap-2 text-[11px] text-slate-500 dark:text-slate-400 pt-1">
                  <Clock className="h-3 w-3" />
                  <span>
                    {new Date(activeAnnouncement.createdAt).toLocaleString(isVi ? 'vi-VN' : 'en-US')}
                  </span>
                </div>
              </div>

              <button
                type="button"
                id="btn-deactivate-announcement"
                onClick={() => handleInitiateDeactivateAnnouncement(activeAnnouncement.id)}
                className="px-3 py-1.5 rounded-lg border border-rose-300 dark:border-rose-800 hover:bg-rose-100 dark:hover:bg-rose-900/50 text-rose-700 dark:text-rose-300 text-xs font-semibold transition-colors shrink-0 flex items-center gap-1.5 cursor-pointer"
              >
                <XCircle className="h-3.5 w-3.5" />
                <span>{isVi ? 'Gỡ thông báo' : 'End Announcement'}</span>
              </button>
            </div>
          </div>
        ) : (
          <div className="p-3.5 rounded-xl bg-slate-50 dark:bg-slate-900/40 border border-slate-100 dark:border-slate-800 text-xs text-slate-500 dark:text-slate-400 flex items-center gap-2">
            <Info className="h-4 w-4 shrink-0 text-slate-400" />
            <span>
              {isVi
                ? 'Hiện tại không có thông báo khẩn cấp nào đang hoạt động trên máy chủ.'
                : 'No emergency announcement is currently active on the server.'}
            </span>
          </div>
        )}

        {/* Publish Announcement Form */}
        <form onSubmit={handleInitiatePublishAnnouncement} className="space-y-4 pt-2">
          <h4 className="text-xs font-bold uppercase tracking-wider text-slate-700 dark:text-slate-300">
            {isVi ? 'Tạo thông báo khẩn mới' : 'Create New Emergency Announcement'}
          </h4>

          {formError && (
            <div className="p-2.5 rounded-xl bg-rose-50 dark:bg-rose-950/40 border border-rose-200 dark:border-rose-900/50 text-rose-600 dark:text-rose-400 text-xs flex items-center gap-2">
              <AlertTriangle className="h-4 w-4 shrink-0" />
              <span>{formError}</span>
            </div>
          )}

          <div className="space-y-1.5">
            <label className="block text-xs font-semibold text-slate-700 dark:text-slate-300">
              {isVi ? 'Tiêu đề thông báo' : 'Announcement Title'} <span className="text-rose-500">*</span>
            </label>
            <input
              type="text"
              id="input-announcement-title"
              value={newTitle}
              onChange={(e) => {
                setNewTitle(e.target.value);
                if (formError) setFormError(null);
              }}
              placeholder={
                isVi
                  ? 'Ví dụ: Bảo trì cổng thanh toán ngân hàng đối tác'
                  : 'E.g., Partner Banking Gateway Temporary Maintenance'
              }
              className="w-full p-2.5 text-xs rounded-xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-900 text-slate-900 dark:text-slate-100 placeholder:text-slate-400 focus:outline-hidden focus:ring-1 focus:ring-emerald-500"
            />
          </div>

          <div className="space-y-1.5">
            <label className="block text-xs font-semibold text-slate-700 dark:text-slate-300">
              {isVi ? 'Nội dung chi tiết' : 'Announcement Details'} <span className="text-rose-500">*</span>
            </label>
            <textarea
              id="input-announcement-desc"
              rows={3}
              value={newDescription}
              onChange={(e) => {
                setNewDescription(e.target.value);
                if (formError) setFormError(null);
              }}
              placeholder={
                isVi
                  ? 'Nhập nội dung ngắn gọn, rõ ràng để người dùng nắm bắt thông tin...'
                  : 'Provide clear, concise guidance for affected users...'
              }
              className="w-full p-2.5 text-xs rounded-xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-900 text-slate-900 dark:text-slate-100 placeholder:text-slate-400 focus:outline-hidden focus:ring-1 focus:ring-emerald-500"
            />
          </div>

          <div className="space-y-1.5">
            <label className="block text-xs font-semibold text-slate-700 dark:text-slate-300">
              {isVi ? 'Lý do phát hành thông báo (ghi nhật ký kiểm toán)' : 'Audit Reason'} <span className="text-rose-500">*</span>
            </label>
            <input
              type="text"
              id="input-announcement-reason"
              value={newReason}
              onChange={(e) => {
                setNewReason(e.target.value);
                if (formError) setFormError(null);
              }}
              placeholder={
                isVi
                  ? 'Nhập lý do nghiệp vụ phát hành thông báo này...'
                  : 'Enter operational reason for broadcasting this announcement...'
              }
              className="w-full p-2.5 text-xs rounded-xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-900 text-slate-900 dark:text-slate-100 placeholder:text-slate-400 focus:outline-hidden focus:ring-1 focus:ring-emerald-500"
            />
          </div>

          <div className="flex items-center justify-between pt-1">
            <span className="text-[11px] text-slate-500 dark:text-slate-400">
              {isVi
                ? 'Yêu cầu xác nhận mã bảo mật 6 số của Quản trị viên trước khi gửi thông báo.'
                : 'Requires 6-digit Admin Security PIN verification before broadcast.'}
            </span>

            <button
              type="submit"
              id="btn-submit-emergency-announcement"
              disabled={!newTitle.trim() || !newDescription.trim() || !newReason.trim()}
              className="px-4 py-2.5 rounded-xl bg-rose-600 hover:bg-rose-700 text-white font-semibold text-xs shadow-xs transition-colors flex items-center gap-2 disabled:opacity-50 disabled:cursor-not-allowed cursor-pointer"
            >
              <Send className="h-3.5 w-3.5" />
              <span>{isVi ? 'Gửi thông báo toàn hệ thống' : 'Broadcast Announcement'}</span>
            </button>
          </div>
        </form>

        {/* Announcement History notice */}
        <div className="p-3.5 rounded-xl bg-slate-50 dark:bg-slate-900/40 border border-slate-100 dark:border-slate-800 text-[11px] text-slate-500 dark:text-slate-400 flex items-center gap-2">
          <Info className="h-4 w-4 shrink-0 text-slate-400" />
          <span>
            {isVi
              ? 'Hệ thống lưu trữ một thông báo hoạt động tại một thời điểm theo tiêu chuẩn backend. Lịch sử chi tiết được lưu trong Nhật ký kiểm toán quản trị.'
              : 'The backend serves the single active emergency announcement. Archived announcements and modifications are recorded in Admin Audit Logs.'}
          </span>
        </div>
      </div>

      {/* ========================================================================= */}
      {/* SECTION 3: SYSTEM ADMINISTRATION & OPERATIONAL PARAMETERS                 */}
      {/* ========================================================================= */}
      <div className="p-4 rounded-2xl bg-slate-50 dark:bg-[#111A2E] border border-slate-200 dark:border-slate-800/80 text-xs text-slate-600 dark:text-slate-400 space-y-2">
        <div className="flex items-center gap-2 font-semibold text-slate-700 dark:text-slate-300">
          <Server className="h-4 w-4 text-slate-500" />
          <span>{isVi ? 'Lưu ý quản trị vận hành' : 'Operations Guidance'}</span>
        </div>
        <p className="text-[11px] leading-relaxed">
          {isVi
            ? 'Mọi thay đổi đối với chế độ bảo trì và thông báo khẩn đều được xử lý trực tiếp tại máy chủ có thẩm quyền và áp dụng đồng bộ qua các trình duyệt người dùng.'
            : 'All modifications to system maintenance mode and emergency announcements execute authoritative server-side commands and synchronize across client browsers.'}
        </p>
      </div>

      {/* ========================================================================= */}
      {/* EXPLICIT CONFIRMATION DIALOG 1: MAINTENANCE TOGGLE                        */}
      {/* ========================================================================= */}
      {maintenanceConfirmOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-xs">
          <div className="bg-white dark:bg-[#151D2F] w-full max-w-md rounded-2xl border border-slate-200 dark:border-slate-800 shadow-2xl p-6 space-y-5 animate-in fade-in zoom-in-95 duration-150">
            <div className="flex items-start justify-between">
              <div className="flex items-center gap-3">
                <div
                  className={`p-2.5 rounded-xl ${
                    maintenanceState.enabled
                      ? 'bg-emerald-100 dark:bg-emerald-950/60 text-emerald-600 dark:text-emerald-400'
                      : 'bg-amber-100 dark:bg-amber-950/60 text-amber-600 dark:text-amber-400'
                  }`}
                >
                  {maintenanceState.enabled ? (
                    <ShieldCheck className="h-6 w-6" />
                  ) : (
                    <Construction className="h-6 w-6" />
                  )}
                </div>
                <div>
                  <h3 className="text-base font-bold text-slate-900 dark:text-white">
                    {maintenanceState.enabled
                      ? isVi
                        ? 'Xác nhận tắt bảo trì hệ thống?'
                        : 'Confirm Disable Maintenance Mode?'
                      : isVi
                      ? 'Xác nhận bật bảo trì hệ thống?'
                      : 'Confirm Enable Maintenance Mode?'}
                  </h3>
                  <p className="text-xs text-slate-500 dark:text-slate-400">
                    {isVi ? 'Yêu cầu lý do và xác nhận mã PIN' : 'Requires audit reason & PIN confirmation'}
                  </p>
                </div>
              </div>
              <button
                type="button"
                onClick={() => setMaintenanceConfirmOpen(false)}
                disabled={isSubmittingMaintenance}
                className="text-slate-400 hover:text-slate-600 p-1 cursor-pointer"
              >
                <X className="h-5 w-5" />
              </button>
            </div>

            <div
              className={`p-3.5 rounded-xl text-xs space-y-2 ${
                maintenanceState.enabled
                  ? 'bg-emerald-50 dark:bg-emerald-950/40 border border-emerald-200 dark:border-emerald-900/50 text-emerald-800 dark:text-emerald-300'
                  : 'bg-amber-50 dark:bg-amber-950/40 border border-amber-200 dark:border-amber-900/50 text-amber-800 dark:text-amber-300'
              }`}
            >
              <div className="flex items-center gap-2 font-semibold">
                <AlertTriangle className="h-4 w-4 shrink-0" />
                <span>
                  {maintenanceState.enabled
                    ? isVi
                      ? 'Khôi phục truy cập cho người dùng'
                      : 'Restoring access for normal users'
                    : isVi
                    ? 'Tạm ngắt truy cập người dùng thông thường'
                    : 'Restricting normal user access'}
                </span>
              </div>
              <p className="text-[11px] leading-relaxed">
                {maintenanceState.enabled
                  ? isVi
                    ? 'Hệ thống sẽ mở lại toàn bộ chức năng quản lý tài chính và giao dịch cho tất cả người dùng thông thường.'
                    : 'All financial features will become accessible to normal users immediately.'
                  : isVi
                  ? 'Toàn bộ người dùng thông thường sẽ bị tạm khóa truy cập và hiển thị màn hình thông báo bảo trì. Quản trị viên vẫn giữ quyền đăng nhập.'
                  : 'Normal users will be blocked with a maintenance screen. Admins maintain full access.'}
              </p>
            </div>

            {/* Mandatory Reason */}
            <div className="space-y-1.5 text-xs">
              <label className="block font-semibold text-slate-700 dark:text-slate-300">
                {isVi ? 'Lý do thay đổi trạng thái' : 'Reason for status change'}{' '}
                <span className="text-rose-500">*</span>
              </label>
              <textarea
                rows={2}
                value={maintenanceReason}
                onChange={(e) => {
                  setMaintenanceReason(e.target.value);
                  if (maintenanceDialogError) setMaintenanceDialogError(null);
                }}
                placeholder={
                  isVi
                    ? 'Nhập lý do thay đổi chế độ bảo trì (bắt buộc)...'
                    : 'Provide audit justification reason...'
                }
                className="w-full p-2.5 text-xs rounded-xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-900 text-slate-900 dark:text-slate-100 placeholder:text-slate-400 focus:outline-hidden focus:ring-1 focus:ring-amber-500"
              />
            </div>

            {maintenanceDialogError && (
              <div className="p-2.5 rounded-xl bg-rose-50 dark:bg-rose-950/40 border border-rose-200 dark:border-rose-900/50 text-rose-600 dark:text-rose-400 text-xs flex items-center gap-2">
                <AlertTriangle className="h-4 w-4 shrink-0" />
                <span>{maintenanceDialogError}</span>
              </div>
            )}

            <div className="flex items-center justify-end gap-3 pt-2">
              <button
                type="button"
                onClick={() => setMaintenanceConfirmOpen(false)}
                disabled={isSubmittingMaintenance}
                className="px-4 py-2 rounded-xl text-xs font-semibold border border-slate-200 dark:border-slate-700 hover:bg-slate-100 dark:hover:bg-slate-800 text-slate-700 dark:text-slate-300 transition-colors cursor-pointer"
              >
                {isVi ? 'Hủy bỏ' : 'Cancel'}
              </button>
              <button
                type="button"
                id="btn-confirm-maintenance-dialog"
                disabled={isSubmittingMaintenance || !maintenanceReason.trim()}
                onClick={handleConfirmToggleMaintenance}
                className={`px-4 py-2 rounded-xl text-xs font-semibold text-white shadow-xs transition-colors cursor-pointer flex items-center gap-1.5 disabled:opacity-50 disabled:cursor-not-allowed ${
                  maintenanceState.enabled
                    ? 'bg-emerald-600 hover:bg-emerald-700'
                    : 'bg-amber-600 hover:bg-amber-700'
                }`}
              >
                <span>
                  {isSubmittingMaintenance
                    ? isVi
                      ? 'Đang xử lý...'
                      : 'Processing...'
                    : isVi
                    ? 'Tiếp tục & Xác thực PIN'
                    : 'Continue & Verify PIN'}
                </span>
              </button>
            </div>
          </div>
        </div>
      )}

      {/* ========================================================================= */}
      {/* EXPLICIT CONFIRMATION DIALOG 2: PUBLISH EMERGENCY ANNOUNCEMENT            */}
      {/* ========================================================================= */}
      {publishConfirmOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-xs">
          <div className="bg-white dark:bg-[#151D2F] w-full max-w-md rounded-2xl border border-slate-200 dark:border-slate-800 shadow-2xl p-6 space-y-5 animate-in fade-in zoom-in-95 duration-150">
            <div className="flex items-start justify-between">
              <div className="flex items-center gap-3">
                <div className="p-2.5 rounded-xl bg-rose-100 dark:bg-rose-950/60 text-rose-600 dark:text-rose-400">
                  <Megaphone className="h-6 w-6" />
                </div>
                <div>
                  <h3 className="text-base font-bold text-slate-900 dark:text-white">
                    {isVi ? 'Xác nhận gửi thông báo khẩn?' : 'Confirm Broadcast Announcement?'}
                  </h3>
                  <p className="text-xs text-slate-500 dark:text-slate-400">
                    {isVi ? 'Thông báo sẽ hiển thị tới toàn thể người dùng' : 'Visible to all active users immediately'}
                  </p>
                </div>
              </div>
              <button
                type="button"
                onClick={() => setPublishConfirmOpen(false)}
                disabled={isSubmittingAnnouncement}
                className="text-slate-400 hover:text-slate-600 p-1 cursor-pointer"
              >
                <X className="h-5 w-5" />
              </button>
            </div>

            {/* Preview */}
            <div className="p-3.5 rounded-xl bg-slate-50 dark:bg-slate-900/60 border border-slate-200 dark:border-slate-800 space-y-2">
              <span className="text-[10px] font-bold uppercase tracking-wider text-rose-600 dark:text-rose-400 block">
                {isVi ? 'Xem trước nội dung:' : 'Announcement Preview:'}
              </span>
              <p className="text-xs font-bold text-slate-900 dark:text-white">
                {newTitle.trim()}
              </p>
              <p className="text-[11px] text-slate-600 dark:text-slate-300 leading-relaxed">
                {newDescription.trim()}
              </p>
              <p className="text-[10px] font-mono text-slate-400 pt-1">
                {isVi ? 'Lý do: ' : 'Reason: '}{newReason.trim()}
              </p>
            </div>

            <div className="p-3 rounded-xl bg-rose-50 dark:bg-rose-950/40 border border-rose-200 dark:border-rose-900/50 text-xs text-rose-800 dark:text-rose-300 flex items-start gap-2">
              <AlertTriangle className="h-4 w-4 shrink-0 mt-0.5" />
              <span className="text-[11px] leading-relaxed">
                {isVi
                  ? 'Thông báo khẩn này sẽ lập tức hiển thị trên màn hình của tất cả người dùng đang hoạt động. Bạn cần xác thực mã PIN Quản trị viên 6 chữ số để gửi thông báo.'
                  : 'This emergency notice will appear immediately for all active users. Admin 6-digit Security PIN is required to authorize.'}
              </span>
            </div>

            {publishDialogError && (
              <div className="p-2.5 rounded-xl bg-rose-50 dark:bg-rose-950/40 border border-rose-200 dark:border-rose-900/50 text-rose-600 dark:text-rose-400 text-xs flex items-center gap-2">
                <AlertTriangle className="h-4 w-4 shrink-0" />
                <span>{publishDialogError}</span>
              </div>
            )}

            <div className="flex items-center justify-end gap-3 pt-2">
              <button
                type="button"
                onClick={() => setPublishConfirmOpen(false)}
                disabled={isSubmittingAnnouncement}
                className="px-4 py-2 rounded-xl text-xs font-semibold border border-slate-200 dark:border-slate-700 hover:bg-slate-100 dark:hover:bg-slate-800 text-slate-700 dark:text-slate-300 transition-colors cursor-pointer"
              >
                {isVi ? 'Hủy bỏ' : 'Cancel'}
              </button>
              <button
                type="button"
                id="btn-confirm-publish-dialog"
                disabled={isSubmittingAnnouncement}
                onClick={handleConfirmPublishAnnouncement}
                className="px-4 py-2 rounded-xl text-xs font-semibold text-white bg-rose-600 hover:bg-rose-700 shadow-xs transition-colors cursor-pointer flex items-center gap-1.5 disabled:opacity-50"
              >
                <span>
                  {isSubmittingAnnouncement
                    ? isVi
                      ? 'Đang xử lý...'
                      : 'Processing...'
                    : isVi
                    ? 'Tiếp tục & Xác thực PIN'
                    : 'Continue & Verify PIN'}
                </span>
              </button>
            </div>
          </div>
        </div>
      )}

      {/* ========================================================================= */}
      {/* EXPLICIT CONFIRMATION DIALOG 3: DEACTIVATE / END ANNOUNCEMENT              */}
      {/* ========================================================================= */}
      {deactivateConfirmId && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-xs">
          <div className="bg-white dark:bg-[#151D2F] w-full max-w-md rounded-2xl border border-slate-200 dark:border-slate-800 shadow-2xl p-6 space-y-5 animate-in fade-in zoom-in-95 duration-150">
            <div className="flex items-start justify-between">
              <div className="flex items-center gap-3">
                <div className="p-2.5 rounded-xl bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-300">
                  <Clock className="h-6 w-6" />
                </div>
                <div>
                  <h3 className="text-base font-bold text-slate-900 dark:text-white">
                    {isVi ? 'Xác nhận gỡ thông báo khẩn?' : 'Confirm End Announcement?'}
                  </h3>
                  <p className="text-xs text-slate-500 dark:text-slate-400">
                    {isVi ? 'Gỡ hiển thị thông báo trên toàn hệ thống' : 'Remove announcement across the system'}
                  </p>
                </div>
              </div>
              <button
                type="button"
                onClick={() => setDeactivateConfirmId(null)}
                disabled={isSubmittingDeactivate}
                className="text-slate-400 hover:text-slate-600 p-1 cursor-pointer"
              >
                <X className="h-5 w-5" />
              </button>
            </div>

            <div className="p-3.5 rounded-xl bg-slate-50 dark:bg-slate-900/50 border border-slate-200 dark:border-slate-800 text-xs text-slate-600 dark:text-slate-300 space-y-2">
              <div className="flex items-center gap-2 font-semibold text-slate-800 dark:text-slate-200">
                <Info className="h-4 w-4 text-emerald-500" />
                <span>{isVi ? 'Gỡ thông báo an toàn' : 'Safe deactivation'}</span>
              </div>
              <p className="text-[11px] leading-relaxed">
                {isVi
                  ? 'Thông báo khẩn sẽ được gỡ khỏi màn hình của người dùng ngay lập tức và chuyển trạng thái trên máy chủ. Bạn cần nhập lý do và xác thực mã PIN Quản trị viên 6 chữ số để xác nhận.'
                  : 'The emergency announcement will be removed from user viewports immediately. You will need to provide an audit reason and enter your 6-digit Admin Security PIN to confirm.'}
              </p>
            </div>

            {/* Mandatory Reason */}
            <div className="space-y-1.5 text-xs">
              <label className="block font-semibold text-slate-700 dark:text-slate-300">
                {isVi ? 'Lý do gỡ thông báo' : 'Reason for deactivation'}{' '}
                <span className="text-rose-500">*</span>
              </label>
              <textarea
                rows={2}
                value={deactivateReason}
                onChange={(e) => {
                  setDeactivateReason(e.target.value);
                  if (deactivateDialogError) setDeactivateDialogError(null);
                }}
                placeholder={
                  isVi
                    ? 'Nhập lý do gỡ thông báo (bắt buộc)...'
                    : 'Provide audit justification reason...'
                }
                className="w-full p-2.5 text-xs rounded-xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-900 text-slate-900 dark:text-slate-100 placeholder:text-slate-400 focus:outline-hidden focus:ring-1 focus:ring-emerald-500"
              />
            </div>

            {deactivateDialogError && (
              <div className="p-2.5 rounded-xl bg-rose-50 dark:bg-rose-950/40 border border-rose-200 dark:border-rose-900/50 text-rose-600 dark:text-rose-400 text-xs flex items-center gap-2">
                <AlertTriangle className="h-4 w-4 shrink-0" />
                <span>{deactivateDialogError}</span>
              </div>
            )}

            <div className="flex items-center justify-end gap-3 pt-2">
              <button
                type="button"
                onClick={() => setDeactivateConfirmId(null)}
                disabled={isSubmittingDeactivate}
                className="px-4 py-2 rounded-xl text-xs font-semibold border border-slate-200 dark:border-slate-700 hover:bg-slate-100 dark:hover:bg-slate-800 text-slate-700 dark:text-slate-300 transition-colors cursor-pointer"
              >
                {isVi ? 'Hủy bỏ' : 'Cancel'}
              </button>
              <button
                type="button"
                id="btn-confirm-deactivate-dialog"
                disabled={isSubmittingDeactivate || !deactivateReason.trim()}
                onClick={handleConfirmDeactivateAnnouncement}
                className="px-4 py-2 rounded-xl text-xs font-semibold text-white bg-slate-900 hover:bg-slate-800 dark:bg-slate-100 dark:hover:bg-white dark:text-slate-900 shadow-xs transition-colors cursor-pointer flex items-center gap-1.5 disabled:opacity-50 disabled:cursor-not-allowed"
              >
                <span>
                  {isSubmittingDeactivate
                    ? isVi
                      ? 'Đang xử lý...'
                      : 'Processing...'
                    : isVi
                    ? 'Tiếp tục & Xác thực PIN'
                    : 'Continue & Verify PIN'}
                </span>
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

export default AdminConfigurationPage;
