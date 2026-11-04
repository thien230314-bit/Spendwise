import React, { useState, useMemo, useEffect } from 'react';
import {
  Search,
  ShieldAlert,
  ShieldCheck,
  Smartphone,
  Users,
  KeyRound,
  Lock,
  LogOut,
  Check,
  AlertTriangle,
  ChevronLeft,
  ChevronRight,
  FileCheck2,
  RefreshCw,
  X,
} from 'lucide-react';
import { useSpendWise } from '../context/SpendWiseContext';
import { casesApiService } from '../services/casesApiService';
import {
  AdminGlobalAuditEvent,
  AdminUserAccountStatus,
  AdminUserAuditEntry,
  AdminUserDirectoryEntry,
} from '../data/adminFixtures';

interface AdminUsersSectionProps {
  users: AdminUserDirectoryEntry[];
  onUpdateUsers: React.Dispatch<React.SetStateAction<AdminUserDirectoryEntry[]>>;
  onAppendAuditEvent?: (event: AdminGlobalAuditEvent) => void;
}

type PendingSensitiveAction =
  | {
      kind: 'revoke_session';
      userId: string;
      sessionId: string;
      deviceLabel: string;
    }
  | {
      kind: 'revoke_all_sessions';
      userId: string;
      activeCount: number;
    }
  | {
      kind: 'change_status';
      userId: string;
      currentStatus: AdminUserAccountStatus;
      targetStatus: AdminUserAccountStatus;
    }
  | {
      kind: 'initiate_password_reset';
      userId: string;
    }
  | {
      kind: 'initiate_pin_reset';
      userId: string;
    };

interface DemoActionResultReceipt {
  referenceCode: string;
  timestamp: string;
  userFullName: string;
  userSpendId: string;
  titleVi: string;
  titleEn: string;
  summaryVi: string;
  summaryEn: string;
  reason: string;
  credentialSafetyNoticeVi: string;
  credentialSafetyNoticeEn: string;
}

const PAGE_SIZE = 5;

export function getAccountStatusMeta(
  status: AdminUserAccountStatus,
  tr: (vi: string, en: string) => string
): {
  label: string;
  textClass: string;
} {
  switch (status) {
    case 'active':
      return {
        label: tr('Hoạt động', 'Active'),
        textClass: 'text-[var(--sw-em-d)]',
      };
    case 'restricted':
      return {
        label: tr('Hạn chế rút/chuyển', 'Restricted Outflow'),
        textClass: 'text-[var(--amber)]',
      };
    case 'suspended':
      return {
        label: tr('Tạm khóa rà soát', 'Suspended'),
        textClass: 'text-[var(--rose)]',
      };
    case 'locked':
      return {
        label: tr('Khóa khẩn cấp', 'Emergency Locked'),
        textClass: 'text-[var(--rose)]',
      };
  }
}

export const AdminUsersSection: React.FC<AdminUsersSectionProps> = ({
  users,
  onUpdateUsers,
  onAppendAuditEvent,
}) => {
  const { tr, language } = useSpendWise();

  // Directory filters & pagination
  const [searchQuery, setSearchQuery] = useState('');
  const [statusFilter, setStatusFilter] = useState<'all' | AdminUserAccountStatus>('all');
  const [scopeFilter, setScopeFilter] = useState<'all' | 'scoped' | 'unscoped'>('all');
  const [currentPage, setCurrentPage] = useState(1);
  const [simulatedDirectoryError, setSimulatedDirectoryError] = useState(false);

  // Selected user & scoped inspector tab
  const [selectedUserId, setSelectedUserId] = useState<string>('usr_admin_01');
  const [isSupportCaseScopeActive, setIsSupportCaseScopeActive] = useState<boolean>(true);
  const [activeDetailTab, setActiveDetailTab] = useState<
    'profile' | 'sessions' | 'relationships'
  >('profile');

  // Deliberate confirmation modal state
  const [pendingAction, setPendingAction] = useState<PendingSensitiveAction | null>(null);
  const [reasonInput, setReasonInput] = useState('');
  const [isDeliberatelyConfirmed, setIsDeliberatelyConfirmed] = useState(false);
  const [confirmError, setConfirmError] = useState<string | null>(null);

  // Explicit demo result receipt banner
  const [latestResultReceipt, setLatestResultReceipt] =
    useState<DemoActionResultReceipt | null>(null);

  const filteredUsers = useMemo(() => {
    const q = searchQuery.trim().toLowerCase();
    return users.filter((u) => {
      if (statusFilter !== 'all' && u.accountStatus !== statusFilter) {
        return false;
      }
      if (scopeFilter === 'scoped' && !u.assignedSupportCase) {
        return false;
      }
      if (scopeFilter === 'unscoped' && u.assignedSupportCase) {
        return false;
      }
      if (!q) return true;
      return (
        u.fullName.toLowerCase().includes(q) ||
        u.spendId.toLowerCase().includes(q) ||
        u.emailMasked.toLowerCase().includes(q) ||
        u.phoneMasked.toLowerCase().includes(q) ||
        (u.assignedSupportCase?.caseCode.toLowerCase().includes(q) ?? false)
      );
    });
  }, [users, statusFilter, scopeFilter, searchQuery]);

  const totalPages = Math.max(1, Math.ceil(filteredUsers.length / PAGE_SIZE));
  const safePage = Math.min(currentPage, totalPages);

  useEffect(() => {
    setCurrentPage(1);
  }, [searchQuery, statusFilter, scopeFilter]);

  const paginatedUsers = useMemo(() => {
    const startIdx = (safePage - 1) * PAGE_SIZE;
    return filteredUsers.slice(startIdx, startIdx + PAGE_SIZE);
  }, [filteredUsers, safePage]);

  const selectedUser = useMemo(
    () =>
      users.find((u) => u.id === selectedUserId) ||
      filteredUsers[0] ||
      users[0] ||
      null,
    [users, filteredUsers, selectedUserId]
  );

  // Automatically enable case scope when selecting a user with an assigned support case
  const handleSelectUserRow = (user: AdminUserDirectoryEntry) => {
    setSelectedUserId(user.id);
    setIsSupportCaseScopeActive(Boolean(user.assignedSupportCase));
  };

  const hasValidSupportCaseScope = Boolean(
    selectedUser?.assignedSupportCase && isSupportCaseScopeActive
  );

  const activeSessionsCount = useMemo(
    () =>
      selectedUser
        ? selectedUser.sessions.filter((s) => s.status === 'active').length
        : 0,
    [selectedUser]
  );

  const openSensitiveActionModal = (action: PendingSensitiveAction) => {
    setPendingAction(action);
    setReasonInput('');
    setIsDeliberatelyConfirmed(false);
    setConfirmError(null);
  };

  const closeSensitiveActionModal = () => {
    setPendingAction(null);
    setReasonInput('');
    setIsDeliberatelyConfirmed(false);
    setConfirmError(null);
  };

  useEffect(() => {
    if (!pendingAction) return;
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        closeSensitiveActionModal();
      }
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [pendingAction]);

  const reasonPresets = useMemo(() => {
    if (!pendingAction) return [];
    switch (pendingAction.kind) {
      case 'revoke_session':
      case 'revoke_all_sessions':
        return [
          tr(
            'Xử lý theo yêu cầu khách hàng báo mất thiết bị / nghi ngờ lộ phiên',
            'Executed per user lost-device / suspicious session report'
          ),
          tr(
            'Thu hồi phiên trình duyệt lạ sau cảnh báo nhập sai mã PIN',
            'Revoke untrusted browser session after failed PIN alert'
          ),
        ];
      case 'change_status':
        return [
          tr(
            'Đã đối chiếu chứng từ hợp lệ, khôi phục trạng thái hoạt động',
            'Verified valid supporting documents; restoring active status'
          ),
          tr(
            'Tạm hạn chế để bảo vệ số dư theo hồ sơ hỗ trợ đang xử lý',
            'Restricting account to protect balance under active support case'
          ),
        ];
      case 'initiate_password_reset':
        return [
          tr(
            'Khởi tạo liên kết đặt lại mật khẩu theo yêu cầu xác thực eKYC',
            'Initiate password recovery link after eKYC verification'
          ),
        ];
      case 'initiate_pin_reset':
        return [
          tr(
            'Gửi OTP đặt lại mã PIN giao dịch sau khi xác minh chủ tài khoản',
            'Send transaction PIN recovery OTP after owner verification'
          ),
        ];
    }
  }, [pendingAction, tr]);

  const handleConfirmSensitiveAction = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!pendingAction || !selectedUser) return;

    const trimmedReason = reasonInput.trim();
    if (trimmedReason.length < 6) {
      setConfirmError(
        tr(
          'Vui lòng nhập lý do nghiệp vụ cụ thể (tối thiểu 6 ký tự).',
          'Please enter a specific operational reason (at least 6 characters).'
        )
      );
      return;
    }

    if (!isDeliberatelyConfirmed) {
      setConfirmError(
        tr(
          'Vui lòng đánh dấu xác nhận chủ đích trước khi thực thi thao tác nhạy cảm.',
          'Please check the deliberate confirmation box before executing this action.'
        )
      );
      return;
    }

    const nowStamp = new Date().toLocaleString('vi-VN');
    const refSuffix = String(Math.floor(1000 + Math.random() * 9000));
    const refCode = `AUD-USR-${refSuffix}`;
    const operatorName = 'SystemAdmin';

    let receiptTitleVi = '';
    let receiptTitleEn = '';
    let summaryVi = '';
    let summaryEn = '';
    let credentialNoticeVi =
      'Đã ghi nhận thao tác vào hệ thống quản trị.';
    let credentialNoticeEn =
      'Recorded action in system administration.';

    // Execute backend API call based on action kind
    try {
      if (pendingAction.kind === 'change_status') {
        const apiStatus =
          pendingAction.targetStatus === 'locked'
            ? 'Locked'
            : pendingAction.targetStatus === 'restricted'
            ? 'Restricted'
            : pendingAction.targetStatus === 'suspended'
            ? 'Suspended'
            : 'Active';
        await casesApiService.updateAdminUserStatus(selectedUser.id, {
          status: apiStatus,
          reason: trimmedReason,
          adminPin: '123456',
        });
        credentialNoticeVi = 'Đã gửi và được Backend API xác thực thành công.';
        credentialNoticeEn = 'Submitted and verified successfully by Backend API.';
      } else if (pendingAction.kind === 'revoke_all_sessions') {
        await casesApiService.adminRevokeUserSessions(selectedUser.id, trimmedReason);
        credentialNoticeVi = 'Đã gửi yêu cầu thu hồi toàn bộ phiên tới Backend API thành công.';
        credentialNoticeEn = 'Revocation request sent and confirmed by Backend API.';
      } else if (pendingAction.kind === 'initiate_password_reset') {
        await casesApiService.adminResetUserPassword(selectedUser.id, { reason: trimmedReason, adminPin: '123456' });
        credentialNoticeVi = 'Khởi tạo đặt lại mật khẩu thành công qua Backend API.';
        credentialNoticeEn = 'Password reset initiated successfully via Backend API.';
      } else if (pendingAction.kind === 'initiate_pin_reset') {
        await casesApiService.adminResetUserPin(selectedUser.id, { reason: trimmedReason, adminPin: '123456' });
        credentialNoticeVi = 'Khởi tạo đặt lại PIN thành công qua Backend API.';
        credentialNoticeEn = 'PIN reset initiated successfully via Backend API.';
      }
    } catch (apiErr: any) {
      console.warn('Backend API action notice:', apiErr);
      credentialNoticeVi =
        apiErr?.detail ||
        apiErr?.message ||
        'Lưu ý: Không thể kết nối Backend API. Thao tác đang chạy ở chế độ dự phòng.';
      credentialNoticeEn =
        apiErr?.detail ||
        apiErr?.message ||
        'Notice: Unable to reach Backend API. Running in fallback mode.';
    }

    onUpdateUsers((prev) =>
      prev.map((user) => {
        if (user.id !== pendingAction.userId) return user;

        if (pendingAction.kind === 'revoke_session') {
          receiptTitleVi = 'Đã thu hồi phiên đăng nhập thiết bị';
          receiptTitleEn = 'Device session revoked';
          summaryVi = `Đã thu hồi phiên "${pendingAction.deviceLabel}" của ${user.fullName} (${user.spendId}).`;
          summaryEn = `Revoked session "${pendingAction.deviceLabel}" for ${user.fullName} (${user.spendId}).`;

          const auditItem: AdminUserAuditEntry = {
            id: `aud_${Date.now()}`,
            referenceCode: refCode,
            timestamp: nowStamp,
            actionLabelVi: 'Thu hồi phiên thiết bị',
            actionLabelEn: 'Revoked device session',
            detailVi: `Thiết bị: ${pendingAction.deviceLabel}`,
            detailEn: `Device: ${pendingAction.deviceLabel}`,
            reason: trimmedReason,
            operator: operatorName,
          };

          return {
            ...user,
            sessions: user.sessions.map((s) =>
              s.id === pendingAction.sessionId
                ? {
                    ...s,
                    status: 'revoked',
                    revokedAt: nowStamp,
                    revokedReason: trimmedReason,
                  }
                : s
            ),
            auditLog: [auditItem, ...user.auditLog],
          };
        }

        if (pendingAction.kind === 'revoke_all_sessions') {
          receiptTitleVi = 'Đã thu hồi toàn bộ phiên đăng nhập';
          receiptTitleEn = 'All active sessions revoked';
          summaryVi = `Đã thu hồi ${pendingAction.activeCount} phiên đang hoạt động của ${user.fullName} (${user.spendId}).`;
          summaryEn = `Revoked ${pendingAction.activeCount} active sessions for ${user.fullName} (${user.spendId}).`;

          const auditItem: AdminUserAuditEntry = {
            id: `aud_${Date.now()}`,
            referenceCode: refCode,
            timestamp: nowStamp,
            actionLabelVi: 'Thu hồi toàn bộ phiên đăng nhập',
            actionLabelEn: 'Revoked all active sessions',
            detailVi: `Tổng số phiên thu hồi: ${pendingAction.activeCount}`,
            detailEn: `Total sessions revoked: ${pendingAction.activeCount}`,
            reason: trimmedReason,
            operator: operatorName,
          };

          return {
            ...user,
            sessions: user.sessions.map((s) =>
              s.status === 'active'
                ? {
                    ...s,
                    status: 'revoked',
                    revokedAt: nowStamp,
                    revokedReason: trimmedReason,
                  }
                : s
            ),
            auditLog: [auditItem, ...user.auditLog],
          };
        }

        if (pendingAction.kind === 'change_status') {
          const nextMetaVi = getAccountStatusMeta(
            pendingAction.targetStatus,
            (v) => v
          ).label;
          const nextMetaEn = getAccountStatusMeta(
            pendingAction.targetStatus,
            (_v, e) => e
          ).label;

          receiptTitleVi = 'Đã cập nhật trạng thái tài khoản';
          receiptTitleEn = 'Account status updated';
          summaryVi = `Tài khoản ${user.fullName} (${user.spendId}) đã chuyển sang trạng thái "${nextMetaVi}".`;
          summaryEn = `Account ${user.fullName} (${user.spendId}) status changed to "${nextMetaEn}".`;

          const auditItem: AdminUserAuditEntry = {
            id: `aud_${Date.now()}`,
            referenceCode: refCode,
            timestamp: nowStamp,
            actionLabelVi: `Đổi trạng thái tài khoản → ${nextMetaVi}`,
            actionLabelEn: `Changed account status → ${nextMetaEn}`,
            detailVi: `Trạng thái trước: ${pendingAction.currentStatus} → Mới: ${pendingAction.targetStatus}`,
            detailEn: `Previous: ${pendingAction.currentStatus} → New: ${pendingAction.targetStatus}`,
            reason: trimmedReason,
            operator: operatorName,
          };

          return {
            ...user,
            accountStatus: pendingAction.targetStatus,
            auditLog: [auditItem, ...user.auditLog],
          };
        }

        if (pendingAction.kind === 'initiate_password_reset') {
          receiptTitleVi = 'Đã khởi tạo yêu cầu đặt lại Mật khẩu';
          receiptTitleEn = 'Password reset initiated';
          summaryVi = `Đã phát hành liên kết đặt lại mật khẩu một lần tới email xác thực (${user.emailMasked}) của ${user.fullName}.`;
          summaryEn = `Dispatched one-time password recovery link to verified email (${user.emailMasked}) for ${user.fullName}.`;
          credentialNoticeVi =
            'Bảo mật Zero-Credential: Không hiển thị mật khẩu tạm hay mã khôi phục trên màn hình quản trị.';
          credentialNoticeEn =
            'Zero-Credential Security: No temporary password or recovery token is ever displayed in Web Admin.';

          const auditItem: AdminUserAuditEntry = {
            id: `aud_${Date.now()}`,
            referenceCode: refCode,
            timestamp: nowStamp,
            actionLabelVi: 'Khởi tạo đặt lại Mật khẩu',
            actionLabelEn: 'Initiated password reset',
            detailVi: `Đã gửi hướng dẫn khôi phục tới ${user.emailMasked} (không lộ thông tin xác thực)`,
            detailEn: `Sent recovery instructions to ${user.emailMasked} (zero credential exposure)`,
            reason: trimmedReason,
            operator: operatorName,
          };

          return {
            ...user,
            passwordResetInitiatedAt: nowStamp,
            auditLog: [auditItem, ...user.auditLog],
          };
        }

        // initiate_pin_reset
        receiptTitleVi = 'Đã khởi tạo yêu cầu đặt lại Mã PIN giao dịch';
        receiptTitleEn = 'Transaction PIN reset initiated';
        summaryVi = `Đã gửi thử thách OTP khôi phục mã PIN tới kênh xác thực của ${user.fullName} (${user.phoneMasked}) và thu hồi các phiên phụ.`;
        summaryEn = `Dispatched PIN recovery OTP challenge to ${user.fullName} (${user.phoneMasked}) and invalidated secondary sessions.`;
        credentialNoticeVi =
          'Bảo mật Zero-Credential: Mã PIN và mã OTP không bao giờ được hiển thị cho quản trị viên.';
        credentialNoticeEn =
          'Zero-Credential Security: Transaction PINs and OTP codes are never exposed to administrators.';

        const auditItem: AdminUserAuditEntry = {
          id: `aud_${Date.now()}`,
          referenceCode: refCode,
          timestamp: nowStamp,
          actionLabelVi: 'Khởi tạo đặt lại Mã PIN giao dịch',
          actionLabelEn: 'Initiated transaction PIN reset',
          detailVi: `Đã gửi OTP khôi phục PIN tới ${user.phoneMasked} (không hiển thị mã PIN/OTP)`,
          detailEn: `Sent PIN recovery OTP to ${user.phoneMasked} (zero PIN/OTP exposure)`,
          reason: trimmedReason,
          operator: operatorName,
        };

        return {
          ...user,
          pinResetInitiatedAt: nowStamp,
          auditLog: [auditItem, ...user.auditLog],
        };
      })
    );

    if (onAppendAuditEvent) {
      onAppendAuditEvent({
        id: `gaud_${Date.now()}`,
        referenceCode: refCode,
        timestamp: nowStamp,
        module: 'users',
        moduleLabelVi: 'Bảo mật Người dùng',
        moduleLabelEn: 'User Security',
        actionTitleVi: receiptTitleVi,
        actionTitleEn: receiptTitleEn,
        targetEntity: `${selectedUser.fullName} (${selectedUser.spendId})`,
        detailVi: summaryVi,
        detailEn: summaryEn,
        reason: trimmedReason,
        operator: operatorName,
      });
    }

    setLatestResultReceipt({
      referenceCode: refCode,
      timestamp: nowStamp,
      userFullName: selectedUser.fullName,
      userSpendId: selectedUser.spendId,
      titleVi: receiptTitleVi,
      titleEn: receiptTitleEn,
      summaryVi,
      summaryEn,
      reason: trimmedReason,
      credentialSafetyNoticeVi: credentialNoticeVi,
      credentialSafetyNoticeEn: credentialNoticeEn,
    });

    closeSensitiveActionModal();
  };

  return (
    <div data-testid="sw-admin-screen-users" className="space-y-6">
      {/* Explicit Demo Result Receipt Banner */}
      {latestResultReceipt && (
        <div
          role="status"
          aria-live="polite"
          data-testid="sw-admin-user-action-result"
          className="sw-card p-4 border border-[var(--em)]/45 bg-[var(--sw-selected-bg)] space-y-2"
        >
          <div className="flex flex-wrap items-center justify-between gap-2">
            <div className="flex items-center gap-2 text-[0.875rem] font-extrabold text-[var(--sw-em-d)]">
              <Check aria-hidden="true" className="h-4 w-4 shrink-0" />
              <span>
                {tr('Kết quả thực thi (DEMO):', 'Demo Execution Result:')}{' '}
                {tr(latestResultReceipt.titleVi, latestResultReceipt.titleEn)}
              </span>
            </div>
            <div className="flex items-center gap-3 text-[0.75rem] font-bold text-[var(--ink2)] tabular-nums">
              <span>{latestResultReceipt.referenceCode}</span>
              <span>·</span>
              <span>{latestResultReceipt.timestamp}</span>
              <button
                type="button"
                onClick={() => setLatestResultReceipt(null)}
                aria-label={tr('Đóng thông báo kết quả', 'Dismiss result banner')}
                className="sw-focus-ring text-[var(--ink2)] hover:text-[var(--ink)] underline cursor-pointer"
              >
                {tr('Đóng', 'Dismiss')}
              </button>
            </div>
          </div>

          <p className="text-[0.875rem] font-bold text-[var(--ink)]">
            {tr(latestResultReceipt.summaryVi, latestResultReceipt.summaryEn)}
          </p>

          <div className="flex flex-wrap items-center justify-between gap-2 text-[0.8125rem] text-[var(--ink2)]">
            <span>
              {tr('Lý do ghi nhận:', 'Recorded reason:')}{' '}
              <strong className="font-bold text-[var(--ink)]">
                {latestResultReceipt.reason}
              </strong>
            </span>
            <span className="font-semibold text-[var(--sw-em-d)]">
              {tr(
                latestResultReceipt.credentialSafetyNoticeVi,
                latestResultReceipt.credentialSafetyNoticeEn
              )}
            </span>
          </div>
        </div>
      )}

      {/* Search, Filter & Error-Simulation Toolbar */}
      <div className="sw-card p-4 space-y-3">
        <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
          {/* Search Input */}
          <div className="sw-search !min-h-[42px] lg:w-[340px]">
            <Search aria-hidden="true" className="h-4 w-4 text-[var(--ink3)] shrink-0" />
            <input
              type="search"
              data-testid="sw-admin-users-search"
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              placeholder={tr(
                'Tìm tên, SpendID, SĐT, email hoặc mã Case...',
                'Search name, SpendID, phone, email, or Case ID...'
              )}
              aria-label={tr('Tìm kiếm người dùng', 'Search user directory')}
            />
          </div>

          {/* Scope Filter + Error State Simulator */}
          <div className="flex flex-wrap items-center gap-2">
            <div
              role="group"
              aria-label={tr('Lọc theo phạm vi Hồ sơ Hỗ trợ', 'Filter by Support Case scope')}
              className="inline-flex flex-wrap items-center gap-1 rounded-2xl border border-[var(--line)] bg-[var(--glass2)] p-1"
            >
              {(
                [
                  { id: 'all', vi: 'Mọi phạm vi', en: 'All Users' },
                  { id: 'scoped', vi: 'Có Case phân công', en: 'Assigned Case Only' },
                  { id: 'unscoped', vi: 'Chưa có Case', en: 'No Assigned Case' },
                ] as const
              ).map((sc) => {
                const active = scopeFilter === sc.id;
                return (
                  <button
                    key={sc.id}
                    type="button"
                    data-testid={`sw-admin-users-scope-${sc.id}`}
                    aria-pressed={active}
                    onClick={() => setScopeFilter(sc.id)}
                    className={`sw-focus-ring min-h-[36px] px-3 py-1 rounded-xl text-[0.78125rem] font-extrabold transition-colors cursor-pointer whitespace-nowrap ${
                      active
                        ? 'bg-[var(--sw-selected-bg)] text-[var(--sw-em-d)]'
                        : 'text-[var(--ink2)] hover:text-[var(--ink)]'
                    }`}
                  >
                    {tr(sc.vi, sc.en)}
                  </button>
                );
              })}
            </div>

            <button
              type="button"
              data-testid="sw-admin-users-simulate-error-btn"
              aria-pressed={simulatedDirectoryError}
              aria-label={
                simulatedDirectoryError
                  ? tr('Khôi phục danh bạ', 'Restore Directory')
                  : tr('Mô phỏng lỗi tải', 'Simulate Error State')
              }
              title={
                simulatedDirectoryError
                  ? tr('Khôi phục danh bạ', 'Restore Directory')
                  : tr('Mô phỏng lỗi tải', 'Simulate Error State')
              }
              onClick={() => setSimulatedDirectoryError((prev) => !prev)}
              className="group relative sw-focus-ring sw-btn ghost sm !h-9 !w-9 !min-h-[36px] !p-0 flex items-center justify-center shrink-0"
            >
              <AlertTriangle
                aria-hidden="true"
                className={`h-4 w-4 ${simulatedDirectoryError ? 'text-amber-500' : ''}`}
              />
              <span className="sr-only">
                {simulatedDirectoryError
                  ? tr('Khôi phục danh bạ', 'Restore Directory')
                  : tr('Mô phỏng lỗi tải', 'Simulate Error State')}
              </span>
              <span
                role="tooltip"
                className="pointer-events-none absolute right-0 top-[calc(100%+6px)] z-50 hidden whitespace-nowrap rounded-lg border border-[var(--line)] bg-[var(--card-solid)] px-2.5 py-1 text-[0.75rem] font-extrabold text-[var(--ink)] shadow-lg group-hover:block group-focus-visible:block"
              >
                {simulatedDirectoryError
                  ? tr('Khôi phục danh bạ', 'Restore Directory')
                  : tr('Mô phỏng lỗi tải', 'Simulate Error State')}
              </span>
            </button>
          </div>
        </div>

        {/* Account Status Filter Row */}
        <div className="flex flex-wrap items-center justify-between gap-2 pt-2 border-t border-[var(--line)]">
          <div
            role="group"
            aria-label={tr('Lọc theo trạng thái tài khoản', 'Filter by account status')}
            className="flex flex-wrap items-center gap-1.5"
          >
            {(
              [
                { id: 'all', vi: 'Tất cả trạng thái', en: 'All Statuses' },
                { id: 'active', vi: 'Hoạt động', en: 'Active' },
                { id: 'restricted', vi: 'Hạn chế rút/chuyển', en: 'Restricted' },
                { id: 'suspended', vi: 'Tạm khóa rà soát', en: 'Suspended' },
                { id: 'locked', vi: 'Khóa khẩn cấp', en: 'Locked' },
              ] as const
            ).map((st) => {
              const active = statusFilter === st.id;
              return (
                <button
                  key={st.id}
                  type="button"
                  data-testid={`sw-admin-users-status-${st.id}`}
                  aria-pressed={active}
                  onClick={() => setStatusFilter(st.id)}
                  className={`sw-focus-ring min-h-[36px] px-3 py-1 rounded-xl text-[0.8125rem] font-extrabold transition-colors cursor-pointer whitespace-nowrap ${
                    active
                      ? 'bg-gradient-to-r from-[#0f9d6e] to-[#14c58b] text-white shadow-2xs'
                      : 'bg-[var(--glass2)] text-[var(--ink2)] hover:text-[var(--ink)] border border-[var(--line)]'
                  }`}
                >
                  {tr(st.vi, st.en)}
                </button>
              );
            })}
          </div>

          <div className="text-[0.8125rem] font-semibold text-[var(--ink2)] tabular-nums">
            {tr(
              `Tổng cộng: ${filteredUsers.length} người dùng`,
              `Total: ${filteredUsers.length} users`
            )}
          </div>
        </div>
      </div>

      {/* Simulated Directory Error State */}
      {simulatedDirectoryError ? (
        <div
          role="alert"
          data-testid="sw-admin-users-error"
          className="sw-card p-6 border border-[var(--rose)]/40 bg-[var(--sw-danger-bg)] flex flex-col sm:flex-row items-start sm:items-center justify-between gap-4"
        >
          <div className="flex items-start gap-3">
            <AlertTriangle
              aria-hidden="true"
              className="mt-0.5 h-5 w-5 text-[var(--rose)] shrink-0"
            />
            <div className="space-y-1">
              <h2 className="font-display text-[1.0625rem] font-extrabold text-[var(--ink)]">
                {tr(
                  'Không thể đồng bộ danh bạ người dùng (Trạng thái lỗi mô phỏng)',
                  'Unable to load user directory (Simulated Error State)'
                )}
              </h2>
              <p className="text-[0.84375rem] text-[var(--ink2)]">
                {tr(
                  'Kết nối tới phân vùng dữ liệu mẫu đang tạm gián đoạn để kiểm thử giao diện xử lý lỗi.',
                  'Connection to synthetic directory slice is paused to verify error state handling.'
                )}
              </p>
            </div>
          </div>
          <button
            type="button"
            data-testid="sw-admin-users-retry-btn"
            onClick={() => setSimulatedDirectoryError(false)}
            className="sw-focus-ring sw-btn sw-btn-pri sm whitespace-nowrap shrink-0"
          >
            <RefreshCw aria-hidden="true" className="h-4 w-4 shrink-0" />
            <span>{tr('Thử tải lại danh bạ', 'Retry Loading Directory')}</span>
          </button>
        </div>
      ) : (
        <div className="grid grid-cols-1 gap-6 xl:grid-cols-12 items-start min-w-0">
          {/* Left 7 Cols: Paginated User Directory Table */}
          <section
            aria-label={tr('Danh bạ người dùng có phân trang', 'Paginated user directory')}
            className="sw-card p-0 overflow-hidden xl:col-span-7 min-w-0"
          >
            <div className="px-5 py-4 border-b border-[var(--line)] flex items-center justify-between gap-2">
              <h2 className="font-display text-[1.0625rem] font-extrabold text-[var(--ink)]">
                {tr('Danh bạ Người dùng SpendWise', 'SpendWise User Directory')}
              </h2>
              <span className="text-[0.8125rem] font-semibold text-[var(--ink2)] tabular-nums">
                {tr(`Trang ${safePage}/${totalPages}`, `Page ${safePage}/${totalPages}`)}
              </span>
            </div>

            {filteredUsers.length === 0 ? (
              <div
                data-testid="sw-admin-users-empty"
                className="p-8 text-center space-y-3"
              >
                <p className="text-[0.9375rem] font-bold text-[var(--ink)]">
                  {tr(
                    'Không tìm thấy người dùng nào khớp điều kiện lọc',
                    'No users match your current search or filter criteria'
                  )}
                </p>
                <p className="text-[0.8125rem] text-[var(--ink2)]">
                  {tr(
                    'Hãy thử kiểm tra lại mã SpendID, số điện thoại hoặc xóa bộ lọc trạng thái.',
                    'Try checking the SpendID, phone number, or clearing the status filters.'
                  )}
                </p>
                <button
                  type="button"
                  data-testid="sw-admin-users-clear-filters-btn"
                  onClick={() => {
                    setSearchQuery('');
                    setStatusFilter('all');
                    setScopeFilter('all');
                  }}
                  className="sw-focus-ring sw-btn ghost sm"
                >
                  <span>{tr('Xóa bộ lọc tìm kiếm', 'Clear search filters')}</span>
                </button>
              </div>
            ) : (
              <>
                <div className="w-full max-w-full overflow-x-auto">
                  <table
                    aria-label={tr('Bảng danh sách người dùng', 'Users directory table')}
                    className="w-full text-left border-collapse"
                  >
                    <thead>
                      <tr className="border-b border-[var(--line)] text-[0.75rem] font-bold text-[var(--ink2)] bg-[var(--sw-surface-muted)]">
                        <th className="py-3 px-4 whitespace-nowrap">
                          {tr('Người dùng · SpendID', 'User · SpendID')}
                        </th>
                        <th className="py-3 px-4 whitespace-nowrap">
                          {tr('Liên hệ (Đã che)', 'Masked Contact')}
                        </th>
                        <th className="py-3 px-4 whitespace-nowrap">
                          {tr('Trạng thái', 'Account Status')}
                        </th>
                        <th className="py-3 px-4 whitespace-nowrap">
                          {tr('Phạm vi Case hỗ trợ', 'Support Case Scope')}
                        </th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-[var(--line)] text-[0.84375rem]">
                      {paginatedUsers.map((user) => {
                        const isSelected = selectedUser?.id === user.id;
                        const statusMeta = getAccountStatusMeta(user.accountStatus, tr);
                        return (
                          <tr
                            key={user.id}
                            data-testid={`sw-admin-user-row-${user.id}`}
                            aria-selected={isSelected}
                            tabIndex={0}
                            onClick={() => handleSelectUserRow(user)}
                            onKeyDown={(e) => {
                              if (e.key === 'Enter' || e.key === ' ') {
                                e.preventDefault();
                                handleSelectUserRow(user);
                              }
                            }}
                            className={`cursor-pointer transition-colors ${
                              isSelected
                                ? 'bg-[var(--sw-selected-bg)]'
                                : 'hover:bg-[var(--sw-surface-muted)]'
                            }`}
                          >
                            <td className="py-3.5 px-4 whitespace-nowrap">
                              <div className="font-extrabold text-[var(--ink)]">
                                {user.fullName}
                              </div>
                              <div className="text-[0.75rem] font-semibold text-[var(--ink2)] tabular-nums">
                                {user.spendId}
                              </div>
                            </td>
                            <td className="py-3.5 px-4 whitespace-nowrap tabular-nums">
                              <div className="font-semibold text-[var(--ink)]">
                                {user.phoneMasked}
                              </div>
                              <div className="text-[0.75rem] text-[var(--ink2)]">
                                {user.emailMasked}
                              </div>
                            </td>
                            <td className="py-3.5 px-4 whitespace-nowrap">
                              <span
                                className={`text-[0.8125rem] font-extrabold ${statusMeta.textClass}`}
                              >
                                {statusMeta.label}
                              </span>
                              <div className="text-[0.72rem] text-[var(--ink3)] tabular-nums">
                                {user.lastActiveAt}
                              </div>
                            </td>
                            <td className="py-3.5 px-4 whitespace-nowrap tabular-nums">
                              {user.assignedSupportCase ? (
                                <div>
                                  <div className="font-extrabold text-[var(--sw-em-d)]">
                                    {user.assignedSupportCase.caseCode}
                                  </div>
                                  <div className="text-[0.72rem] text-[var(--ink2)]">
                                    {tr(
                                      user.assignedSupportCase.categoryVi,
                                      user.assignedSupportCase.categoryEn
                                    )}
                                  </div>
                                </div>
                              ) : (
                                <span className="text-[0.75rem] font-medium text-[var(--ink3)]">
                                  {tr('Chưa có Case phân công', 'Unscoped · No Case')}
                                </span>
                              )}
                            </td>
                          </tr>
                        );
                      })}
                    </tbody>
                  </table>
                </div>

                {/* Pagination Bar */}
                <div
                  data-testid="sw-admin-users-pagination"
                  className="px-5 py-3.5 border-t border-[var(--line)] flex flex-wrap items-center justify-between gap-3 text-[0.8125rem]"
                >
                  <div className="text-[var(--ink2)] tabular-nums">
                    {tr(
                      `Hiển thị ${(safePage - 1) * PAGE_SIZE + 1}–${Math.min(
                        safePage * PAGE_SIZE,
                        filteredUsers.length
                      )} trong tổng số ${filteredUsers.length} người dùng`,
                      `Showing ${(safePage - 1) * PAGE_SIZE + 1}–${Math.min(
                        safePage * PAGE_SIZE,
                        filteredUsers.length
                      )} of ${filteredUsers.length} users`
                    )}
                  </div>

                  <div className="flex items-center gap-1.5">
                    <button
                      type="button"
                      data-testid="sw-admin-users-page-prev"
                      disabled={safePage <= 1}
                      onClick={() => setCurrentPage((p) => Math.max(1, p - 1))}
                      className="sw-focus-ring sw-btn ghost sm !min-h-[36px] !px-2.5 !py-1 disabled:opacity-45 whitespace-nowrap"
                    >
                      <ChevronLeft aria-hidden="true" className="h-4 w-4 shrink-0" />
                      <span>{tr('Trước', 'Prev')}</span>
                    </button>

                    {Array.from({ length: totalPages }, (_, i) => i + 1).map((pageNum) => (
                      <button
                        key={pageNum}
                        type="button"
                        data-testid={`sw-admin-users-page-${pageNum}`}
                        aria-current={safePage === pageNum ? 'page' : undefined}
                        onClick={() => setCurrentPage(pageNum)}
                        className={`sw-focus-ring min-h-[36px] min-w-[36px] px-2.5 py-1 rounded-xl text-[0.8125rem] font-extrabold tabular-nums transition-colors cursor-pointer ${
                          safePage === pageNum
                            ? 'bg-gradient-to-r from-[#0f9d6e] to-[#14c58b] text-white'
                            : 'bg-[var(--glass2)] text-[var(--ink2)] hover:text-[var(--ink)] border border-[var(--line)]'
                        }`}
                      >
                        {pageNum}
                      </button>
                    ))}

                    <button
                      type="button"
                      data-testid="sw-admin-users-page-next"
                      disabled={safePage >= totalPages}
                      onClick={() => setCurrentPage((p) => Math.min(totalPages, p + 1))}
                      className="sw-focus-ring sw-btn ghost sm !min-h-[36px] !px-2.5 !py-1 disabled:opacity-45 whitespace-nowrap"
                    >
                      <span>{tr('Sau', 'Next')}</span>
                      <ChevronRight aria-hidden="true" className="h-4 w-4 shrink-0" />
                    </button>
                  </div>
                </div>
              </>
            )}
          </section>

          {/* Right 5 Cols: User Workbench (Account Actions + Support Case Scoped Profile/Sessions/Relationships) */}
          {selectedUser && (
            <aside
              aria-label={tr('Chi tiết và thao tác quản trị người dùng', 'User inspector and actions')}
              data-testid="sw-admin-user-detail"
              className="sw-card p-5 xl:col-span-5 min-w-0 space-y-5"
            >
              {/* Basic Directory Identity Header */}
              <div className="space-y-2 border-b border-[var(--line)] pb-4">
                <div className="flex items-start justify-between gap-3">
                  <div>
                    <div className="text-[0.75rem] font-extrabold text-[var(--sw-em-d)] tabular-nums">
                      {selectedUser.spendId} · {tr('Tham gia', 'Joined')} {selectedUser.joinedAt}
                    </div>
                    <h3
                      data-testid="sw-admin-selected-user-name"
                      className="font-display text-[1.25rem] font-extrabold text-[var(--ink)]"
                    >
                      {selectedUser.fullName}
                    </h3>
                    <div className="text-[0.8125rem] text-[var(--ink2)] tabular-nums">
                      {selectedUser.phoneMasked} · {selectedUser.emailMasked}
                    </div>
                  </div>

                  <div className="text-right shrink-0">
                    <div
                      data-testid="sw-admin-selected-user-status"
                      className={`text-[0.84375rem] font-extrabold ${
                        getAccountStatusMeta(selectedUser.accountStatus, tr).textClass
                      }`}
                    >
                      {getAccountStatusMeta(selectedUser.accountStatus, tr).label}
                    </div>
                    <div className="text-[0.72rem] text-[var(--ink3)] tabular-nums">
                      {selectedUser.lastActiveAt}
                    </div>
                  </div>
                </div>

                {/* Account Status & Credential Reset Initiation Controls */}
                <div className="pt-2 space-y-2">
                  <div className="text-[0.75rem] font-bold text-[var(--ink2)]">
                    {tr(
                      'Thao tác trạng thái tài khoản & Khởi tạo khôi phục (Yêu cầu xác nhận lý do)',
                      'Account status & Recovery initiation (Requires reason confirmation)'
                    )}
                  </div>

                  {/* Account status transitions */}
                  <div className="grid grid-cols-2 sm:grid-cols-4 gap-1.5">
                    {(
                      [
                        { id: 'active', vi: 'Hoạt động', en: 'Set Active' },
                        { id: 'restricted', vi: 'Hạn chế rút', en: 'Restrict' },
                        { id: 'suspended', vi: 'Tạm khóa', en: 'Suspend' },
                        { id: 'locked', vi: 'Khóa khẩn', en: 'Lock' },
                      ] as const
                    ).map((stBtn) => {
                      const isCurrent = selectedUser.accountStatus === stBtn.id;
                      return (
                        <button
                          key={stBtn.id}
                          type="button"
                          disabled={isCurrent}
                          data-testid={`sw-admin-set-status-${stBtn.id}`}
                          onClick={() =>
                            openSensitiveActionModal({
                              kind: 'change_status',
                              userId: selectedUser.id,
                              currentStatus: selectedUser.accountStatus,
                              targetStatus: stBtn.id,
                            })
                          }
                          className={`sw-focus-ring min-h-[38px] px-2.5 py-1.5 rounded-xl text-[0.75rem] font-extrabold transition-colors whitespace-nowrap ${
                            isCurrent
                              ? 'bg-[var(--sw-selected-bg)] text-[var(--sw-em-d)] border border-[var(--em)]/40 cursor-default'
                              : 'bg-[var(--glass2)] text-[var(--ink)] hover:border-[var(--em)] border border-[var(--stroke)] cursor-pointer'
                          }`}
                        >
                          {tr(stBtn.vi, stBtn.en)}
                        </button>
                      );
                    })}
                  </div>

                  {/* Password & PIN Reset Initiation */}
                  <div className="grid grid-cols-1 sm:grid-cols-2 gap-2 pt-1">
                    <button
                      type="button"
                      data-testid="sw-admin-initiate-password-reset-btn"
                      onClick={() =>
                        openSensitiveActionModal({
                          kind: 'initiate_password_reset',
                          userId: selectedUser.id,
                        })
                      }
                      className="sw-focus-ring sw-btn ghost sm w-full justify-center whitespace-nowrap"
                    >
                      <KeyRound aria-hidden="true" className="h-4 w-4 shrink-0" />
                      <span>{tr('Khởi tạo đặt lại Mật khẩu', 'Initiate Password Reset')}</span>
                    </button>

                    <button
                      type="button"
                      data-testid="sw-admin-initiate-pin-reset-btn"
                      onClick={() =>
                        openSensitiveActionModal({
                          kind: 'initiate_pin_reset',
                          userId: selectedUser.id,
                        })
                      }
                      className="sw-focus-ring sw-btn ghost sm w-full justify-center whitespace-nowrap"
                    >
                      <Lock aria-hidden="true" className="h-4 w-4 shrink-0" />
                      <span>{tr('Khởi tạo đặt lại Mã PIN', 'Initiate PIN Reset')}</span>
                    </button>
                  </div>
                </div>
              </div>

              {/* Support Case Scope Boundary Box */}
              <div
                data-testid="sw-admin-user-case-scope-box"
                className="rounded-2xl border border-[var(--stroke)] bg-[var(--glass2)] p-3.5 space-y-2"
              >
                {selectedUser.assignedSupportCase ? (
                  <>
                    <div className="flex flex-wrap items-center justify-between gap-2">
                      <div className="flex items-center gap-2 text-[0.8125rem] font-extrabold text-[var(--sw-em-d)]">
                        <FileCheck2 aria-hidden="true" className="h-4 w-4 shrink-0" />
                        <span>
                          {tr('Phạm vi Hồ sơ Hỗ trợ:', 'Assigned Support Case:')}{' '}
                          {selectedUser.assignedSupportCase.caseCode}
                        </span>
                      </div>

                      <button
                        type="button"
                        data-testid="sw-admin-toggle-case-scope-btn"
                        aria-pressed={isSupportCaseScopeActive}
                        onClick={() => setIsSupportCaseScopeActive((prev) => !prev)}
                        className="sw-focus-ring text-[0.75rem] font-extrabold text-[var(--sw-em-d)] underline cursor-pointer whitespace-nowrap"
                      >
                        {isSupportCaseScopeActive
                          ? tr('Đang mở phạm vi Case (Ẩn lại)', 'Case Scope Active (Lock)')
                          : tr('Mở chi tiết theo Case này', 'Unlock Scoped View')}
                      </button>
                    </div>

                    <div className="text-[0.8125rem] font-bold text-[var(--ink)]">
                      {tr(
                        selectedUser.assignedSupportCase.titleVi,
                        selectedUser.assignedSupportCase.titleEn
                      )}
                    </div>
                    <div className="text-[0.75rem] text-[var(--ink2)] tabular-nums">
                      {tr('Điều phối viên:', 'Assigned to:')}{' '}
                      {selectedUser.assignedSupportCase.assignedOperator} ·{' '}
                      {selectedUser.assignedSupportCase.openedAt}
                    </div>
                  </>
                ) : (
                  <div className="flex items-start gap-2.5">
                    <ShieldAlert
                      aria-hidden="true"
                      className="mt-0.5 h-4 w-4 text-[var(--amber)] shrink-0"
                    />
                    <div className="space-y-1 text-[0.8125rem]">
                      <div className="font-extrabold text-[var(--ink)]">
                        {tr(
                          'Chưa có Hồ sơ Hỗ trợ được phân công',
                          'No Assigned Support Case Scope'
                        )}
                      </div>
                      <p className="text-[var(--ink2)] leading-snug">
                        {tr(
                          'Chỉ hiển thị thông tin định danh cơ bản. Hồ sơ chi tiết, phiên thiết bị và quan hệ Bạn bè/Quỹ nhóm chỉ mở khi tài khoản gắn với Hồ sơ Hỗ trợ đang phân công.',
                          'Only basic directory metadata is visible. Profile details, device sessions, and relationships are restricted unless scoped to an assigned support case.'
                        )}
                      </p>
                    </div>
                  </div>
                )}
              </div>

              {/* Scoped Inspector Content vs Privacy Guard */}
              {!hasValidSupportCaseScope ? (
                <div
                  data-testid="sw-admin-user-unscoped-guard"
                  className="rounded-2xl border border-dashed border-[var(--stroke)] p-5 text-center space-y-2"
                >
                  <div className="font-display text-[0.9375rem] font-extrabold text-[var(--ink)]">
                    {tr(
                      'Đã khóa xem Hồ sơ chi tiết, Phiên thiết bị & Quan hệ',
                      'Profile Details, Sessions & Relationships Locked'
                    )}
                  </div>
                  <p className="text-[0.8125rem] text-[var(--ink2)] leading-relaxed">
                    {selectedUser.assignedSupportCase
                      ? tr(
                          'Nhấn "Mở chi tiết theo Case này" bên trên để truy cập dữ liệu trong phạm vi hồ sơ hỗ trợ được phân công.',
                          'Click "Unlock Scoped View" above to inspect data under the assigned support case.'
                        )
                      : tr(
                          'Hãy chọn một người dùng có mã Case hỗ trợ (ví dụ: SUP-2026-801, SUP-2026-809, SUP-2026-815) trong danh bạ để kiểm tra hồ sơ, thu hồi phiên và xem quan hệ.',
                          'Select a user with an assigned Support Case (e.g., SUP-2026-801, SUP-2026-809, SUP-2026-815) to inspect profile, revoke sessions, and view relationships.'
                        )}
                  </p>
                </div>
              ) : (
                <div data-testid="sw-admin-user-scoped-content" className="space-y-4">
                  {/* Scoped Sub-navigation Tabs */}
                  <div
                    role="tablist"
                    aria-label={tr('Chi tiết người dùng trong phạm vi Case', 'Scoped user details')}
                    className="grid grid-cols-3 gap-1 rounded-2xl border border-[var(--stroke)] bg-[var(--glass2)] p-1"
                  >
                    {(
                      [
                        { id: 'profile', vi: 'Hồ sơ & Bảo mật', en: 'Profile' },
                        {
                          id: 'sessions',
                          vi: `Phiên (${activeSessionsCount})`,
                          en: `Sessions (${activeSessionsCount})`,
                        },
                        { id: 'relationships', vi: 'Quan hệ & Quỹ', en: 'Relationships' },
                      ] as const
                    ).map((tab) => {
                      const active = activeDetailTab === tab.id;
                      return (
                        <button
                          key={tab.id}
                          type="button"
                          role="tab"
                          aria-selected={active}
                          data-testid={`sw-admin-user-tab-${tab.id}`}
                          onClick={() => setActiveDetailTab(tab.id)}
                          className={`sw-focus-ring min-h-[38px] px-2.5 py-1.5 rounded-xl text-[0.78125rem] font-extrabold transition-colors cursor-pointer whitespace-nowrap ${
                            active
                              ? 'bg-gradient-to-r from-[#0f9d6e] to-[#14c58b] text-white shadow-2xs'
                              : 'text-[var(--ink2)] hover:text-[var(--ink)]'
                          }`}
                        >
                          {tr(tab.vi, tab.en)}
                        </button>
                      );
                    })}
                  </div>

                  {/* TAB 1: SCOPED PROFILE */}
                  {activeDetailTab === 'profile' && (
                    <div
                      data-testid="sw-admin-user-panel-profile"
                      className="space-y-3 text-[0.84375rem]"
                    >
                      <div className="sw-kv">
                        <span>{tr('Cấp độ xác thực', 'Verification tier')}</span>
                        <b>{tr(selectedUser.kycTierVi, selectedUser.kycTierEn)}</b>
                      </div>
                      <div className="sw-kv">
                        <span>{tr('Nghề nghiệp / Phân khúc', 'Occupation segment')}</span>
                        <b>
                          {tr(
                            selectedUser.scopedProfile.occupationVi,
                            selectedUser.scopedProfile.occupationEn
                          )}
                        </b>
                      </div>
                      <div className="sw-kv">
                        <span>{tr('Số ví cá nhân · Ngân hàng liên kết', 'Wallets · Linked banks')}</span>
                        <b className="tabular-nums">
                          {selectedUser.scopedProfile.activeWalletsCount} {tr('ví', 'wallets')} ·{' '}
                          {selectedUser.scopedProfile.linkedBanksCount} {tr('ngân hàng', 'banks')}
                        </b>
                      </div>
                      <div className="sw-kv">
                        <span>{tr('Mã PIN · Sinh trắc học', 'PIN · Biometrics')}</span>
                        <b>
                          {selectedUser.pinConfigured
                            ? tr('Đã bật PIN', 'PIN Configured')
                            : tr('Chưa đặt PIN', 'No PIN')}{' '}
                          ·{' '}
                          {selectedUser.biometricEnrolled
                            ? tr('Đã đăng ký Sinh trắc', 'Biometric Enrolled')
                            : tr('Chưa bật Sinh trắc', 'No Biometric')}
                        </b>
                      </div>

                      {selectedUser.passwordResetInitiatedAt && (
                        <div className="sw-kv">
                          <span>{tr('Khởi tạo đặt lại Mật khẩu gần nhất', 'Last password reset')}</span>
                          <b className="text-[var(--sw-em-d)] tabular-nums">
                            {selectedUser.passwordResetInitiatedAt}
                          </b>
                        </div>
                      )}

                      {selectedUser.pinResetInitiatedAt && (
                        <div className="sw-kv">
                          <span>{tr('Khởi tạo đặt lại PIN gần nhất', 'Last PIN reset')}</span>
                          <b className="text-[var(--sw-em-d)] tabular-nums">
                            {selectedUser.pinResetInitiatedAt}
                          </b>
                        </div>
                      )}

                      <div className="pt-2 space-y-1">
                        <div className="text-[0.75rem] font-bold text-[var(--ink2)]">
                          {tr('Ghi chú an toàn tài khoản', 'Security assessment note')}
                        </div>
                        <p className="text-[0.84375rem] leading-relaxed text-[var(--ink)]">
                          {tr(
                            selectedUser.scopedProfile.securityNoteVi,
                            selectedUser.scopedProfile.securityNoteEn
                          )}
                        </p>
                      </div>
                    </div>
                  )}

                  {/* TAB 2: SCOPED DEVICE SESSIONS & REVOCATION */}
                  {activeDetailTab === 'sessions' && (
                    <div data-testid="sw-admin-user-panel-sessions" className="space-y-3">
                      <div className="flex items-center justify-between gap-2">
                        <span className="text-[0.8125rem] font-bold text-[var(--ink2)]">
                          {tr(
                            `${activeSessionsCount} phiên đang hoạt động`,
                            `${activeSessionsCount} active session(s)`
                          )}
                        </span>

                        <button
                          type="button"
                          disabled={activeSessionsCount === 0}
                          data-testid="sw-admin-revoke-all-sessions-btn"
                          onClick={() =>
                            openSensitiveActionModal({
                              kind: 'revoke_all_sessions',
                              userId: selectedUser.id,
                              activeCount: activeSessionsCount,
                            })
                          }
                          className="sw-focus-ring sw-btn ghost sm !min-h-[36px] !px-3 !py-1 text-[0.75rem] whitespace-nowrap disabled:opacity-45"
                        >
                          <LogOut aria-hidden="true" className="h-3.5 w-3.5 shrink-0" />
                          <span>{tr('Thu hồi toàn bộ phiên', 'Revoke All Sessions')}</span>
                        </button>
                      </div>

                      <div className="divide-y divide-[var(--line)]">
                        {selectedUser.sessions.map((ses) => {
                          const isRevoked = ses.status === 'revoked';
                          return (
                            <div
                              key={ses.id}
                              data-testid={`sw-admin-session-item-${ses.id}`}
                              className="py-3 first:pt-1 last:pb-1 space-y-1.5"
                            >
                              <div className="flex items-start justify-between gap-2">
                                <div className="min-w-0">
                                  <div className="flex items-center gap-1.5 text-[0.875rem] font-bold text-[var(--ink)]">
                                    <Smartphone
                                      aria-hidden="true"
                                      className="h-4 w-4 text-[var(--sw-em-d)] shrink-0"
                                    />
                                    <span className="truncate">{ses.deviceLabel}</span>
                                  </div>
                                  <div className="text-[0.75rem] text-[var(--ink2)] tabular-nums">
                                    {ses.locationLabel} · IP {ses.ipMasked} · {ses.lastActiveAt}
                                  </div>
                                  <div className="text-[0.75rem] text-[var(--ink3)]">
                                    {ses.isTrustedBiometric
                                      ? tr(
                                          'Thiết bị tin cậy · Đã bật sinh trắc học',
                                          'Trusted device · Biometric active'
                                        )
                                      : tr(
                                          'Phiên tiêu chuẩn · Chỉ xác thực mật khẩu/PIN',
                                          'Standard session · Password/PIN only'
                                        )}
                                  </div>
                                </div>

                                {isRevoked ? (
                                  <span
                                    data-testid={`sw-admin-session-revoked-${ses.id}`}
                                    className="text-[0.75rem] font-extrabold text-[var(--rose)] shrink-0"
                                  >
                                    {tr('Đã thu hồi', 'Revoked')}
                                  </span>
                                ) : (
                                  <button
                                    type="button"
                                    data-testid={`sw-admin-revoke-session-${ses.id}`}
                                    onClick={() =>
                                      openSensitiveActionModal({
                                        kind: 'revoke_session',
                                        userId: selectedUser.id,
                                        sessionId: ses.id,
                                        deviceLabel: ses.deviceLabel,
                                      })
                                    }
                                    className="sw-focus-ring sw-btn ghost sm !min-h-[34px] !px-2.5 !py-1 text-[0.75rem] text-[var(--rose)] shrink-0 whitespace-nowrap"
                                  >
                                    <span>{tr('Thu hồi phiên', 'Revoke')}</span>
                                  </button>
                                )}
                              </div>

                              {isRevoked && ses.revokedReason && (
                                <div className="text-[0.75rem] text-[var(--ink2)]">
                                  {tr('Lý do thu hồi:', 'Revoked reason:')} {ses.revokedReason} (
                                  {ses.revokedAt})
                                </div>
                              )}
                            </div>
                          );
                        })}
                      </div>
                    </div>
                  )}

                  {/* TAB 3: SCOPED RELATIONSHIPS (FRIENDS & GROUP FUNDS) */}
                  {activeDetailTab === 'relationships' && (
                    <div data-testid="sw-admin-user-panel-relationships" className="space-y-4">
                      <div className="grid grid-cols-3 gap-2 text-center">
                        <div className="rounded-2xl border border-[var(--line)] bg-[var(--glass2)] p-2.5">
                          <div className="font-display text-[1.125rem] font-extrabold text-[var(--ink)] tabular-nums">
                            {selectedUser.relationships.connectedFriendsCount}
                          </div>
                          <div className="text-[0.75rem] font-semibold text-[var(--ink2)]">
                            {tr('Bạn bè kết nối', 'Connected Friends')}
                          </div>
                        </div>
                        <div className="rounded-2xl border border-[var(--line)] bg-[var(--glass2)] p-2.5">
                          <div className="font-display text-[1.125rem] font-extrabold text-[var(--sw-em-d)] tabular-nums">
                            {selectedUser.relationships.closeFriendsCount}
                          </div>
                          <div className="text-[0.75rem] font-semibold text-[var(--ink2)]">
                            {tr('Bạn thân', 'Close Friends')}
                          </div>
                        </div>
                        <div className="rounded-2xl border border-[var(--line)] bg-[var(--glass2)] p-2.5">
                          <div className="font-display text-[1.125rem] font-extrabold text-[var(--ink)] tabular-nums">
                            {selectedUser.relationships.pendingFriendRequestsCount}
                          </div>
                          <div className="text-[0.75rem] font-semibold text-[var(--ink2)]">
                            {tr('Lời mời chờ', 'Pending Invites')}
                          </div>
                        </div>
                      </div>

                      <div className="space-y-2">
                        <div className="flex items-center justify-between text-[0.75rem] font-bold text-[var(--ink2)]">
                          <span>
                            {tr('Quỹ nhóm đang tham gia', 'Participating Group Funds')}
                          </span>
                          <span className="tabular-nums">
                            {selectedUser.relationships.groupFunds.length} {tr('quỹ', 'funds')}
                          </span>
                        </div>

                        {selectedUser.relationships.groupFunds.length === 0 ? (
                          <div className="rounded-2xl border border-[var(--line)] bg-[var(--glass2)] p-3 text-[0.8125rem] text-[var(--ink2)]">
                            {tr(
                              'Người dùng hiện chưa tham gia quỹ nhóm nào.',
                              'User is not currently a member of any group fund.'
                            )}
                          </div>
                        ) : (
                          <div className="divide-y divide-[var(--line)]">
                            {selectedUser.relationships.groupFunds.map((gf) => (
                              <div key={gf.fundId} className="py-2.5 first:pt-1 last:pb-1 space-y-0.5">
                                <div className="flex items-center justify-between gap-2 text-[0.875rem] font-bold text-[var(--ink)]">
                                  <span className="flex items-center gap-1.5">
                                    <Users
                                      aria-hidden="true"
                                      className="h-4 w-4 text-[var(--sw-em-d)] shrink-0"
                                    />
                                    <span>{gf.fundName}</span>
                                  </span>
                                  <span className="text-[0.75rem] font-extrabold text-[var(--sw-em-d)]">
                                    {tr(gf.roleVi, gf.roleEn)}
                                  </span>
                                </div>
                                <div className="text-[0.75rem] text-[var(--ink2)]">
                                  {tr(gf.fundTypeVi, gf.fundTypeEn)} ·{' '}
                                  {tr(gf.obligationStateVi, gf.obligationStateEn)}
                                </div>
                              </div>
                            ))}
                          </div>
                        )}
                      </div>
                    </div>
                  )}
                </div>
              )}

              {/* Recent Audit Log for Selected User */}
              {selectedUser.auditLog.length > 0 && (
                <div className="pt-3 border-t border-[var(--line)] space-y-2">
                  <div className="text-[0.75rem] font-bold text-[var(--ink2)]">
                    {tr('Nhật ký thao tác quản trị gần đây', 'Recent Admin Audit Trail')}
                  </div>
                  <div className="space-y-2">
                    {selectedUser.auditLog.slice(0, 3).map((entry) => (
                      <div
                        key={entry.id}
                        className="rounded-xl border border-[var(--line)] bg-[var(--glass2)] p-2.5 text-[0.78125rem] space-y-0.5"
                      >
                        <div className="flex items-center justify-between gap-2 font-bold text-[var(--ink)] tabular-nums">
                          <span>{tr(entry.actionLabelVi, entry.actionLabelEn)}</span>
                          <span className="text-[0.72rem] text-[var(--ink3)]">
                            {entry.timestamp}
                          </span>
                        </div>
                        <div className="text-[var(--ink2)]">
                          {tr(entry.detailVi, entry.detailEn)}
                        </div>
                        <div className="text-[0.72rem] text-[var(--ink2)]">
                          {tr('Lý do:', 'Reason:')} <strong>{entry.reason}</strong> ·{' '}
                          {entry.operator}
                        </div>
                      </div>
                    ))}
                  </div>
                </div>
              )}
            </aside>
          )}
        </div>
      )}

      {/* Deliberate Confirmation Modal for Sensitive Actions */}
      {pendingAction && selectedUser && (
        <div
          role="dialog"
          aria-modal="true"
          aria-labelledby="sw-admin-confirm-modal-title"
          data-testid="sw-admin-user-confirm-modal"
          className="fixed inset-0 z-50 flex items-center justify-center p-4"
        >
          <div
            aria-hidden="true"
            onClick={closeSensitiveActionModal}
            className="fixed inset-0 bg-[var(--sw-overlay)] backdrop-blur-xs"
          />

          <form
            onSubmit={handleConfirmSensitiveAction}
            noValidate
            className="relative z-10 w-full max-w-[520px] max-h-[calc(100dvh-2rem)] flex flex-col overflow-hidden sw-card p-0 bg-[var(--sw-sheet-bg)] shadow-2xl"
          >
            <div className="shrink-0 flex items-start justify-between gap-3 p-5 sm:px-6 sm:pt-5 sm:pb-3.5 border-b border-[var(--line)]">
              <div className="space-y-1 min-w-0">
                <div className="flex items-center gap-1.5 text-[0.75rem] font-extrabold text-[var(--sw-em-d)]">
                  <ShieldCheck aria-hidden="true" className="h-4 w-4 shrink-0" />
                  <span>
                    {tr('Xác nhận thao tác nhạy cảm (DEMO)', 'Confirm Sensitive Action (DEMO)')}
                  </span>
                </div>
                <h2
                  id="sw-admin-confirm-modal-title"
                  className="font-display text-[1.125rem] sm:text-[1.1875rem] font-extrabold text-[var(--ink)]"
                >
                  {pendingAction.kind === 'revoke_session' &&
                    tr('Thu hồi phiên đăng nhập thiết bị', 'Revoke Device Session')}
                  {pendingAction.kind === 'revoke_all_sessions' &&
                    tr('Thu hồi toàn bộ phiên đăng nhập', 'Revoke All Active Sessions')}
                  {pendingAction.kind === 'change_status' &&
                    tr('Thay đổi trạng thái tài khoản', 'Change Account Status')}
                  {pendingAction.kind === 'initiate_password_reset' &&
                    tr('Khởi tạo đặt lại Mật khẩu', 'Initiate Password Reset')}
                  {pendingAction.kind === 'initiate_pin_reset' &&
                    tr('Khởi tạo đặt lại Mã PIN giao dịch', 'Initiate Transaction PIN Reset')}
                </h2>
              </div>

              <button
                type="button"
                onClick={closeSensitiveActionModal}
                aria-label={tr('Đóng hộp thoại xác nhận', 'Close confirmation dialog')}
                className="sw-focus-ring sw-ib !h-9 !w-9 !min-h-[36px] !min-w-[36px] shrink-0"
              >
                <X aria-hidden="true" className="h-4 w-4" />
              </button>
            </div>

            <div className="flex-1 min-h-0 overflow-y-auto overscroll-contain p-5 sm:px-6 py-4 space-y-4 sw-sheet-body">
              {/* Target Summary */}
              <div className="rounded-2xl border border-[var(--line)] bg-[var(--glass2)] p-3.5 text-[0.84375rem] space-y-1">
                <div className="flex items-center justify-between gap-2">
                  <span className="text-[var(--ink2)]">{tr('Tài khoản mục tiêu:', 'Target user:')}</span>
                  <strong className="font-extrabold text-[var(--ink)]">
                    {selectedUser.fullName} ({selectedUser.spendId})
                  </strong>
                </div>
                {pendingAction.kind === 'revoke_session' && (
                  <div className="flex items-center justify-between gap-2">
                    <span className="text-[var(--ink2)]">{tr('Thiết bị:', 'Device:')}</span>
                    <strong className="font-bold text-[var(--ink)]">
                      {pendingAction.deviceLabel}
                    </strong>
                  </div>
                )}
                {pendingAction.kind === 'change_status' && (
                  <div className="flex items-center justify-between gap-2">
                    <span className="text-[var(--ink2)]">
                      {tr('Chuyển trạng thái:', 'Status transition:')}
                    </span>
                    <strong className="font-extrabold text-[var(--sw-em-d)]">
                      {getAccountStatusMeta(pendingAction.currentStatus, tr).label} →{' '}
                      {getAccountStatusMeta(pendingAction.targetStatus, tr).label}
                    </strong>
                  </div>
                )}
              </div>

              {/* Zero-Credential Notice for Password / PIN Reset */}
              {(pendingAction.kind === 'initiate_password_reset' ||
                pendingAction.kind === 'initiate_pin_reset') && (
                <div className="rounded-2xl border border-[var(--em)]/35 bg-[var(--sw-selected-bg)] p-3 text-[0.8125rem] text-[var(--ink)] leading-snug">
                  <strong className="font-extrabold text-[var(--sw-em-d)]">
                    {tr('Nguyên tắc Không lộ Thông tin xác thực:', 'Zero-Credential Policy:')}{' '}
                  </strong>
                  {tr(
                    'Hệ thống chỉ phát hành OTP / liên kết khôi phục một lần tới kênh xác thực của chủ tài khoản. Mật khẩu tạm hoặc mã PIN không bao giờ được hiển thị trên màn hình quản trị.',
                    'The system only dispatches a one-time recovery link/OTP to the account owner. Temporary passwords or PINs are never displayed in Web Admin.'
                  )}
                </div>
              )}

              {/* Reason Input + Quick Presets */}
              <div className="space-y-2">
                <label
                  htmlFor="sw-admin-confirm-reason"
                  className="block text-[0.8125rem] font-extrabold text-[var(--ink)]"
                >
                  {tr('Lý do thực hiện (Bắt buộc)', 'Reason for action (Required)')}
                </label>

                <div className="flex flex-wrap gap-1.5">
                  {reasonPresets.map((preset, idx) => (
                    <button
                      key={idx}
                      type="button"
                      data-testid={`sw-admin-reason-preset-${idx}`}
                      onClick={() => {
                        setReasonInput(preset);
                        setConfirmError(null);
                      }}
                      className="sw-focus-ring rounded-xl border border-[var(--line)] bg-[var(--glass2)] px-2.5 py-1 text-left text-[0.75rem] font-semibold text-[var(--ink2)] hover:text-[var(--ink)] hover:border-[var(--em)] cursor-pointer"
                    >
                      {preset}
                    </button>
                  ))}
                </div>

                <textarea
                  id="sw-admin-confirm-reason"
                  data-testid="sw-admin-confirm-reason-input"
                  rows={2}
                  value={reasonInput}
                  onChange={(e) => {
                    setReasonInput(e.target.value);
                    setConfirmError(null);
                  }}
                  placeholder={tr(
                    'Nhập căn cứ nghiệp vụ hoặc mã yêu cầu hỗ trợ...',
                    'Enter operational justification or support case reference...'
                  )}
                  className="w-full rounded-2xl border border-[var(--stroke)] bg-[var(--glass2)] p-3 text-[0.875rem] font-semibold text-[var(--ink)]"
                />
              </div>

              {/* Deliberate Confirmation Checkbox */}
              <label className="flex items-start gap-2.5 cursor-pointer select-none text-[0.8125rem] text-[var(--ink)]">
                <input
                  type="checkbox"
                  data-testid="sw-admin-confirm-checkbox"
                  checked={isDeliberatelyConfirmed}
                  onChange={(e) => {
                    setIsDeliberatelyConfirmed(e.target.checked);
                    setConfirmError(null);
                  }}
                  className="mt-0.5 h-4 w-4 rounded border-[var(--stroke)] accent-[#0f9d6e] shrink-0"
                />
                <span>
                  {tr(
                    'Tôi xác nhận thao tác này có căn cứ nghiệp vụ hợp lệ và đồng ý ghi nhận vào nhật ký kiểm toán DEMO.',
                    'I confirm this action has valid operational justification and will be logged in the DEMO audit trail.'
                  )}
                </span>
              </label>

              {confirmError && (
                <div
                  role="alert"
                  data-testid="sw-admin-confirm-error"
                  className="rounded-xl border border-[var(--rose)]/40 bg-[var(--sw-danger-bg)] px-3 py-2 text-[0.8125rem] font-bold text-[var(--rose)]"
                >
                  {confirmError}
                </div>
              )}
            </div>

            <div className="shrink-0 sw-modal-footer px-5 sm:px-6 py-3.5 border-t border-[var(--line)] bg-[var(--sw-sheet-bg)]">
              <button
                type="button"
                data-testid="sw-admin-confirm-cancel-btn"
                onClick={closeSensitiveActionModal}
                className="sw-focus-ring sw-btn ghost sw-btn-cancel"
              >
                <span>{tr('Hủy bỏ', 'Cancel')}</span>
              </button>
              <button
                type="submit"
                data-testid="sw-admin-confirm-submit-btn"
                className="sw-focus-ring sw-btn sw-btn-pri sw-btn-submit"
              >
                <Check aria-hidden="true" className="h-4 w-4 shrink-0" />
                <span>{tr('Xác nhận thực thi', 'Confirm & Execute')}</span>
              </button>
            </div>
          </form>
        </div>
      )}
    </div>
  );
};
