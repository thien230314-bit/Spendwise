import React, { useState, useEffect, useCallback } from 'react';
import {
  Users,
  Search,
  ShieldCheck,
  Lock,
  Slash,
  Eye,
  X,
  CheckCircle2,
  AlertTriangle,
  Info,
  KeyRound,
  Mail,
  Shield,
  RefreshCw,
  Copy,
  LogOut,
} from 'lucide-react';
import { useI18n } from '../../i18n/I18nContext';
import { useAuth } from '../../providers/AuthProvider';
import { Select } from '../../components/ui/Select';
import {
  casesApiService,
  AdminUserDto,
  AdminTemporaryPasswordResponse,
} from '../../services/casesApiService';
import { isValidSecurityPin } from '../../services/securityPinService';

export const AdminUsersPage: React.FC = () => {
  const { t, locale } = useI18n();
  const isVi = locale === 'vi-VN';
  const { currentUser } = useAuth();

  const [users, setUsers] = useState<AdminUserDto[]>([]);
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [loadError, setLoadError] = useState<string | null>(null);

  const [searchQuery, setSearchQuery] = useState('');
  const [statusFilter, setStatusFilter] = useState<'all' | 'Active' | 'Restricted' | 'Suspended' | 'Locked'>('all');
  const [selectedUser, setSelectedUser] = useState<AdminUserDto | null>(null);
  const [toastMessage, setToastMessage] = useState<string | null>(null);

  const showToast = (msg: string) => {
    setToastMessage(msg);
    setTimeout(() => setToastMessage(null), 3500);
  };

  // Fetch users from backend
  const fetchUsers = useCallback(async (query?: string) => {
    setIsLoading(true);
    setLoadError(null);
    try {
      const data = await casesApiService.getAdminUsers(query);
      setUsers(data);
      setSelectedUser((prev) => (prev ? data.find((u) => u.id === prev.id) || null : null));
    } catch (err: any) {
      setLoadError(
        err?.message || (isVi ? 'Không thể tải danh sách người dùng từ máy chủ.' : 'Failed to load user list from server.')
      );
    } finally {
      setIsLoading(false);
    }
  }, [isVi]);

  useEffect(() => {
    fetchUsers();
  }, [fetchUsers]);

  // Status Change Modal State
  const [statusModalUser, setStatusModalUser] = useState<AdminUserDto | null>(null);
  const [statusModalTarget, setStatusModalTarget] = useState<'Active' | 'Restricted' | 'Suspended' | 'Locked'>('Active');
  const [statusModalReason, setStatusModalReason] = useState('');
  const [statusModalPin, setStatusModalPin] = useState('');
  const [statusModalError, setStatusModalError] = useState<string | null>(null);
  const [statusModalIsSubmitting, setStatusModalIsSubmitting] = useState(false);

  const openStatusChangeModal = (user: AdminUserDto) => {
    if (user.id === currentUser?.id) {
      showToast(
        isVi
          ? 'Bạn không thể tự thay đổi trạng thái tài khoản của chính mình.'
          : 'You cannot alter the status of your own account.'
      );
      return;
    }
    if (user.systemRole === 'SystemAdmin') {
      showToast(
        isVi
          ? 'Không thể thay đổi trạng thái của Quản trị viên hệ thống khác.'
          : 'Cannot modify the status of another System Administrator.'
      );
      return;
    }
    setStatusModalUser(user);
    const validTarget = (['Active', 'Restricted', 'Suspended', 'Locked'].includes(user.status)
      ? user.status
      : 'Active') as 'Active' | 'Restricted' | 'Suspended' | 'Locked';
    setStatusModalTarget(validTarget);
    setStatusModalReason('');
    setStatusModalPin('');
    setStatusModalError(null);
    setStatusModalIsSubmitting(false);
  };

  const closeStatusModal = () => {
    setStatusModalUser(null);
    setStatusModalReason('');
    setStatusModalPin('');
    setStatusModalError(null);
    setStatusModalIsSubmitting(false);
  };

  const executeStatusChange = async () => {
    if (!statusModalUser) return;
    const trimmedReason = statusModalReason.trim();
    if (!trimmedReason) {
      setStatusModalError(
        isVi ? 'Vui lòng nhập lý do thay đổi trạng thái.' : 'Please enter a reason for status change.'
      );
      return;
    }
    if (!isValidSecurityPin(statusModalPin)) {
      setStatusModalError(
        isVi
          ? 'Mã bảo mật Quản trị viên phải bao gồm đúng 6 chữ số.'
          : 'Admin Security PIN must be exactly 6 digits.'
      );
      return;
    }

    setStatusModalIsSubmitting(true);
    setStatusModalError(null);
    try {
      const updatedUser = await casesApiService.updateAdminUserStatus(statusModalUser.id, {
        status: statusModalTarget,
        reason: trimmedReason,
        adminPin: statusModalPin,
      });

      setUsers((prev) => prev.map((u) => (u.id === updatedUser.id ? updatedUser : u)));
      if (selectedUser?.id === updatedUser.id) {
        setSelectedUser(updatedUser);
      }

      showToast(
        isVi
          ? `Đã cập nhật trạng thái người dùng sang "${statusModalTarget}".`
          : `User status updated to "${statusModalTarget}".`
      );
      closeStatusModal();
    } catch (err: any) {
      setStatusModalError(
        err?.message || (isVi ? 'Lỗi cập nhật trạng thái người dùng.' : 'Failed to update user status.')
      );
    } finally {
      setStatusModalIsSubmitting(false);
    }
  };

  // Password Reset Modal State
  const [passwordResetModalUser, setPasswordResetModalUser] = useState<AdminUserDto | null>(null);
  const [passwordResetReason, setPasswordResetReason] = useState('');
  const [passwordResetPin, setPasswordResetPin] = useState('');
  const [passwordResetError, setPasswordResetError] = useState<string | null>(null);
  const [passwordResetIsSubmitting, setPasswordResetIsSubmitting] = useState(false);

  // Temporary Password Issued Display Modal State
  const [issuedTempPassword, setIssuedTempPassword] = useState<{
    user: AdminUserDto;
    result: AdminTemporaryPasswordResponse;
  } | null>(null);
  const [hasCopiedPassword, setHasCopiedPassword] = useState(false);

  const openPasswordResetModal = (user: AdminUserDto) => {
    if (user.id === currentUser?.id) {
      showToast(
        isVi
          ? 'Bạn không thể tự đặt lại mật khẩu của chính mình từ trang Quản trị.'
          : 'You cannot reset your own password from User Management.'
      );
      return;
    }
    if (user.systemRole === 'SystemAdmin') {
      showToast(
        isVi
          ? 'Không thể đặt lại thông tin xác thực của Quản trị viên hệ thống khác.'
          : 'Cannot reset credentials of another System Administrator.'
      );
      return;
    }
    setPasswordResetModalUser(user);
    setPasswordResetReason('');
    setPasswordResetPin('');
    setPasswordResetError(null);
    setPasswordResetIsSubmitting(false);
  };

  const closePasswordResetModal = () => {
    setPasswordResetModalUser(null);
    setPasswordResetReason('');
    setPasswordResetPin('');
    setPasswordResetError(null);
    setPasswordResetIsSubmitting(false);
  };

  const executePasswordReset = async () => {
    if (!passwordResetModalUser) return;
    const trimmedReason = passwordResetReason.trim();
    if (!trimmedReason) {
      setPasswordResetError(
        isVi ? 'Vui lòng nhập lý do đặt lại mật khẩu.' : 'Please enter a reason for password reset.'
      );
      return;
    }
    if (!isValidSecurityPin(passwordResetPin)) {
      setPasswordResetError(
        isVi
          ? 'Mã bảo mật Quản trị viên phải bao gồm đúng 6 chữ số.'
          : 'Admin Security PIN must be exactly 6 digits.'
      );
      return;
    }

    setPasswordResetIsSubmitting(true);
    setPasswordResetError(null);
    try {
      const result = await casesApiService.adminResetUserPassword(passwordResetModalUser.id, {
        reason: trimmedReason,
        adminPin: passwordResetPin,
      });

      // Update local state to show mustChangePassword = true
      setUsers((prev) =>
        prev.map((u) => (u.id === passwordResetModalUser.id ? { ...u, mustChangePassword: true } : u))
      );
      if (selectedUser?.id === passwordResetModalUser.id) {
        setSelectedUser((prev) => (prev ? { ...prev, mustChangePassword: true } : null));
      }

      const targetUser = passwordResetModalUser;
      closePasswordResetModal();

      // Show issued temporary password modal directly to Admin
      setIssuedTempPassword({
        user: targetUser,
        result,
      });
      setHasCopiedPassword(false);
    } catch (err: any) {
      setPasswordResetError(
        err?.message || (isVi ? 'Không thể đặt lại mật khẩu người dùng.' : 'Failed to reset user password.')
      );
    } finally {
      setPasswordResetIsSubmitting(false);
    }
  };

  // PIN Reset Modal State
  const [pinResetModalUser, setPinResetModalUser] = useState<AdminUserDto | null>(null);
  const [pinResetReason, setPinResetReason] = useState('');
  const [pinResetPin, setPinResetPin] = useState('');
  const [pinResetError, setPinResetError] = useState<string | null>(null);
  const [pinResetIsSubmitting, setPinResetIsSubmitting] = useState(false);

  const openPinResetModal = (user: AdminUserDto) => {
    if (user.id === currentUser?.id) {
      showToast(
        isVi
          ? 'Bạn không thể tự đặt lại mã bảo mật của chính mình.'
          : 'You cannot reset your own Security PIN.'
      );
      return;
    }
    if (user.systemRole === 'SystemAdmin') {
      showToast(
        isVi
          ? 'Không thể đặt lại mã bảo mật của Quản trị viên hệ thống khác.'
          : 'Cannot reset Security PIN of another System Administrator.'
      );
      return;
    }
    setPinResetModalUser(user);
    setPinResetReason('');
    setPinResetPin('');
    setPinResetError(null);
    setPinResetIsSubmitting(false);
  };

  const closePinResetModal = () => {
    setPinResetModalUser(null);
    setPinResetReason('');
    setPinResetPin('');
    setPinResetError(null);
    setPinResetIsSubmitting(false);
  };

  const executePinReset = async () => {
    if (!pinResetModalUser) return;
    const trimmedReason = pinResetReason.trim();
    if (!trimmedReason) {
      setPinResetError(
        isVi ? 'Vui lòng nhập lý do đặt lại mã bảo mật.' : 'Please enter a reason for PIN reset.'
      );
      return;
    }
    if (!isValidSecurityPin(pinResetPin)) {
      setPinResetError(
        isVi
          ? 'Mã bảo mật Quản trị viên phải bao gồm đúng 6 chữ số.'
          : 'Admin Security PIN must be exactly 6 digits.'
      );
      return;
    }

    setPinResetIsSubmitting(true);
    setPinResetError(null);
    try {
      await casesApiService.adminResetUserPin(pinResetModalUser.id, {
        reason: trimmedReason,
        adminPin: pinResetPin,
      });

      // Update local state to reflect hasSecurityPin = false
      setUsers((prev) =>
        prev.map((u) => (u.id === pinResetModalUser.id ? { ...u, hasSecurityPin: false } : u))
      );
      if (selectedUser?.id === pinResetModalUser.id) {
        setSelectedUser((prev) => (prev ? { ...prev, hasSecurityPin: false } : null));
      }

      showToast(
        isVi
          ? `Đã đặt lại mã bảo mật cho ${pinResetModalUser.fullName}. Người dùng sẽ phải cài mã mới khi đăng nhập.`
          : `Security PIN reset for ${pinResetModalUser.fullName}. User must enroll a new PIN on next login.`
      );
      closePinResetModal();
    } catch (err: any) {
      setPinResetError(
        err?.message || (isVi ? 'Không thể đặt lại mã bảo mật người dùng.' : 'Failed to reset Security PIN.')
      );
    } finally {
      setPinResetIsSubmitting(false);
    }
  };

  // Revoke Sessions Modal State
  const [revokeModalUser, setRevokeModalUser] = useState<AdminUserDto | null>(null);
  const [revokeReason, setRevokeReason] = useState('');
  const [revokeError, setRevokeError] = useState<string | null>(null);
  const [revokeIsSubmitting, setRevokeIsSubmitting] = useState(false);

  const openRevokeModal = (user: AdminUserDto) => {
    setRevokeModalUser(user);
    setRevokeReason('');
    setRevokeError(null);
    setRevokeIsSubmitting(false);
  };

  const closeRevokeModal = () => {
    setRevokeModalUser(null);
    setRevokeReason('');
    setRevokeError(null);
    setRevokeIsSubmitting(false);
  };

  const executeRevokeSessions = async () => {
    if (!revokeModalUser) return;
    const trimmedReason = revokeReason.trim();
    if (!trimmedReason) {
      setRevokeError(
        isVi ? 'Vui lòng nhập lý do thu hồi phiên đăng nhập.' : 'Please enter a reason for revoking sessions.'
      );
      return;
    }

    setRevokeIsSubmitting(true);
    setRevokeError(null);
    try {
      await casesApiService.adminRevokeUserSessions(revokeModalUser.id, trimmedReason);
      showToast(
        isVi
          ? `Đã thu hồi tất cả phiên đăng nhập của ${revokeModalUser.fullName}.`
          : `All active sessions for ${revokeModalUser.fullName} have been revoked.`
      );
      closeRevokeModal();
    } catch (err: any) {
      setRevokeError(
        err?.message || (isVi ? 'Không thể thu hồi phiên đăng nhập.' : 'Failed to revoke sessions.')
      );
    } finally {
      setRevokeIsSubmitting(false);
    }
  };

  // Client-side search & filtering
  const filteredUsers = users.filter((u) => {
    const q = searchQuery.toLowerCase().trim();
    const matchesSearch =
      !q ||
      u.fullName.toLowerCase().includes(q) ||
      u.email.toLowerCase().includes(q) ||
      (u.spendId && u.spendId.toLowerCase().includes(q)) ||
      u.id.toLowerCase().includes(q);

    const matchesStatus = statusFilter === 'all' || u.status === statusFilter;
    return matchesSearch && matchesStatus;
  });

  const getAvatarInitials = (name: string) => {
    if (!name) return 'U';
    const parts = name.trim().split(/\s+/);
    if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
    return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
  };

  return (
    <div id="admin-users-page" className="space-y-6">
      {/* Toast Notification */}
      {toastMessage && (
        <div className="fixed bottom-6 right-6 z-50 flex items-center gap-2 px-4 py-3 rounded-xl bg-slate-900 text-white shadow-xl border border-emerald-500/50 text-xs font-semibold animate-in fade-in slide-in-from-bottom-2">
          <CheckCircle2 className="w-4 h-4 text-emerald-400" />
          {toastMessage}
        </div>
      )}

      {/* Header & Controls */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <h2 className="text-xl font-bold text-slate-900 dark:text-white tracking-tight">
            {t('adminUsers.title')}
          </h2>
          <p className="text-xs text-slate-500 dark:text-slate-400">
            {t('adminUsers.subtitle')}
          </p>
        </div>

        {/* Search & Filter Bar */}
        <div className="flex flex-wrap items-center gap-3">
          <div className="relative w-64">
            <input
              type="text"
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              placeholder={t('adminUsers.searchPlaceholder')}
              className="w-full h-9 pl-9 pr-3 text-xs rounded-xl bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 text-slate-800 dark:text-slate-200 placeholder:text-slate-400 focus:outline-hidden focus:ring-1 focus:ring-emerald-500"
            />
            <Search className="w-3.5 h-3.5 absolute left-3 top-1/2 -translate-y-1/2 text-slate-400" />
          </div>

          <Select
            value={statusFilter}
            onChange={(val) => setStatusFilter(val as any)}
            size="sm"
            variant="compact"
            className="w-44"
          >
            <option value="all">{t('adminUsers.filterAll')}</option>
            <option value="Active">{isVi ? 'Hoạt động' : 'Active'}</option>
            <option value="Restricted">{isVi ? 'Hạn chế' : 'Restricted'}</option>
            <option value="Suspended">{isVi ? 'Đình chỉ' : 'Suspended'}</option>
            <option value="Locked">{isVi ? 'Đã khóa' : 'Locked'}</option>
          </Select>

          <button
            type="button"
            onClick={() => fetchUsers(searchQuery)}
            disabled={isLoading}
            title={isVi ? 'Làm mới' : 'Refresh'}
            className="h-9 px-3 rounded-xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 text-slate-600 dark:text-slate-300 hover:bg-slate-50 dark:hover:bg-slate-800 flex items-center justify-center transition disabled:opacity-50"
          >
            <RefreshCw className={`w-3.5 h-3.5 ${isLoading ? 'animate-spin' : ''}`} />
          </button>
        </div>
      </div>

      {/* Privacy Notice Banner */}
      <div className="p-3.5 rounded-xl bg-blue-50/70 dark:bg-blue-950/30 border border-blue-200 dark:border-blue-900/40 text-xs text-blue-800 dark:text-blue-300 flex items-center gap-2.5">
        <ShieldCheck className="w-4 h-4 shrink-0 text-blue-600 dark:text-blue-400" />
        <span>{t('adminUsers.privacyNotice')}</span>
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
            onClick={() => fetchUsers(searchQuery)}
            className="px-3 py-1 bg-rose-100 dark:bg-rose-900/50 hover:bg-rose-200 text-rose-800 dark:text-rose-200 rounded-lg font-medium transition"
          >
            {isVi ? 'Thử lại' : 'Retry'}
          </button>
        </div>
      )}

      {/* Users Table */}
      <div className="rounded-xl bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 shadow-xs overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-left text-xs">
            <thead className="bg-slate-50/80 dark:bg-slate-800/60 border-b border-slate-200 dark:border-slate-800 text-slate-700 dark:text-slate-200 font-semibold text-xs">
              <tr>
                <th className="px-5 py-3.5">{t('adminUsers.thUser')}</th>
                <th className="px-5 py-3.5">{t('adminUsers.thContact')}</th>
                <th className="px-5 py-3.5">{t('adminUsers.thRole')}</th>
                <th className="px-5 py-3.5">{t('adminUsers.thStatus')}</th>
                <th className="px-5 py-3.5 text-right">{t('adminUsers.thActions')}</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100 dark:divide-slate-800 font-medium">
              {isLoading && users.length === 0 ? (
                <tr>
                  <td colSpan={5} className="py-12 text-center text-slate-400">
                    <div className="inline-flex items-center gap-2">
                      <RefreshCw className="w-4 h-4 animate-spin text-emerald-500" />
                      <span>{isVi ? 'Đang tải danh sách người dùng...' : 'Loading users...'}</span>
                    </div>
                  </td>
                </tr>
              ) : filteredUsers.length === 0 ? (
                <tr>
                  <td colSpan={5} className="py-8 text-center text-slate-400">
                    {isVi ? 'Không tìm thấy người dùng nào phù hợp.' : 'No users match the search criteria.'}
                  </td>
                </tr>
              ) : (
                filteredUsers.map((user) => {
                  const status = user.status;
                  const isPeerAdmin = user.systemRole === 'SystemAdmin' && user.id !== currentUser?.id;
                  const isSelf = user.id === currentUser?.id;

                  return (
                    <tr
                      key={user.id}
                      onClick={() => setSelectedUser(user)}
                      className="hover:bg-slate-50 dark:hover:bg-slate-800/40 cursor-pointer transition-colors"
                    >
                      <td className="px-5 py-3.5">
                        <div className="flex items-center gap-3">
                          <div className="w-8 h-8 rounded-full bg-emerald-600 text-white font-bold flex items-center justify-center text-xs shrink-0">
                            {getAvatarInitials(user.fullName)}
                          </div>
                          <div>
                            <div className="font-semibold text-slate-900 dark:text-white flex items-center gap-1.5">
                              <span>{user.fullName}</span>
                              {isSelf && (
                                <span className="text-[10px] px-1.5 py-0.2 rounded bg-blue-100 text-blue-700 dark:bg-blue-950 dark:text-blue-300">
                                  {isVi ? 'Bạn' : 'You'}
                                </span>
                              )}
                            </div>
                            <div className="text-[11px] text-slate-400 font-mono">
                              ID: {user.id.slice(0, 8)}...{user.id.slice(-4)}
                            </div>
                          </div>
                        </div>
                      </td>
                      <td className="px-5 py-3.5">
                        <div className="text-slate-700 dark:text-slate-300">{user.email}</div>
                        <div className="text-[11px] text-slate-400 font-mono">
                          SpendID: {user.spendId || '—'}
                        </div>
                      </td>
                      <td className="px-5 py-3.5">
                        <span
                          className={`inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium ${
                            user.systemRole === 'SystemAdmin'
                              ? 'bg-slate-900 text-white dark:bg-emerald-600'
                              : 'bg-slate-100 text-slate-700 dark:bg-slate-800 dark:text-slate-300'
                          }`}
                        >
                          {user.systemRole === 'SystemAdmin'
                            ? (isVi ? 'Quản trị hệ thống' : 'System Administrator')
                            : (isVi ? 'Người dùng' : 'User')}
                        </span>
                      </td>
                      <td className="px-5 py-3.5">
                        <span
                          className={`inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-xs font-medium ${
                            status === 'Active'
                              ? 'bg-emerald-50 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-400 border border-emerald-200/60 dark:border-emerald-800/40'
                              : status === 'Restricted'
                              ? 'bg-amber-50 text-amber-700 dark:bg-amber-950/40 dark:text-amber-400 border border-amber-200/60 dark:border-amber-800/40'
                              : status === 'Suspended'
                              ? 'bg-orange-50 text-orange-700 dark:bg-orange-950/40 dark:text-orange-400 border border-orange-200/60 dark:border-orange-800/40'
                              : 'bg-rose-50 text-rose-700 dark:bg-rose-950/40 dark:text-rose-400 border border-rose-200/60 dark:border-rose-800/40'
                          }`}
                        >
                          {status === 'Active' && <ShieldCheck className="w-3 h-3" />}
                          {status === 'Restricted' && <AlertTriangle className="w-3 h-3" />}
                          {status === 'Suspended' && <Slash className="w-3 h-3" />}
                          {status === 'Locked' && <Lock className="w-3 h-3" />}
                          {status === 'Active'
                            ? (isVi ? 'Hoạt động' : 'Active')
                            : status === 'Restricted'
                            ? (isVi ? 'Hạn chế' : 'Restricted')
                            : status === 'Suspended'
                            ? (isVi ? 'Đình chỉ' : 'Suspended')
                            : (isVi ? 'Đã khóa' : 'Locked')}
                        </span>
                      </td>
                      <td className="px-5 py-3.5 text-right" onClick={(e) => e.stopPropagation()}>
                        <button
                          type="button"
                          onClick={() => setSelectedUser(user)}
                          className="inline-flex items-center gap-1 px-3 py-1.5 rounded-lg border border-slate-200 dark:border-slate-700 hover:bg-slate-100 dark:hover:bg-slate-800 text-slate-700 dark:text-slate-200 text-xs font-medium transition"
                        >
                          <Eye className="w-3.5 h-3.5" />
                          {t('adminUsers.viewDetails')}
                        </button>
                      </td>
                    </tr>
                  );
                })
              )}
            </tbody>
          </table>
        </div>
      </div>

      {/* User Details & Governance Modal */}
      {selectedUser && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/50 backdrop-blur-xs">
          <div className="w-full max-w-lg rounded-2xl bg-white dark:bg-[#151D2F] border border-slate-200 dark:border-slate-800 shadow-2xl overflow-hidden animate-in fade-in-0 zoom-in-95">
            {/* Modal Header */}
            <div className="p-5 border-b border-slate-100 dark:border-slate-800 flex items-center justify-between">
              <div className="flex items-center gap-3">
                <div className="w-10 h-10 rounded-full bg-emerald-600 text-white font-bold flex items-center justify-center text-sm">
                  {getAvatarInitials(selectedUser.fullName)}
                </div>
                <div>
                  <h3 className="text-sm font-bold text-slate-900 dark:text-white flex items-center gap-2">
                    <span>{selectedUser.fullName}</span>
                    {selectedUser.id === currentUser?.id && (
                      <span className="text-[10px] px-1.5 py-0.5 rounded bg-blue-100 text-blue-700 dark:bg-blue-950 dark:text-blue-300">
                        {isVi ? 'Tài khoản của bạn' : 'Your Account'}
                      </span>
                    )}
                  </h3>
                  <p className="text-xs text-slate-400 font-mono">ID: {selectedUser.id}</p>
                </div>
              </div>
              <button
                type="button"
                onClick={() => setSelectedUser(null)}
                className="p-1 rounded-lg text-slate-400 hover:text-slate-600 dark:hover:text-slate-200"
              >
                <X className="w-5 h-5" />
              </button>
            </div>

            {/* Modal Body */}
            <div className="p-5 space-y-5 text-xs overflow-y-auto max-h-[80vh]">
              {/* SECTION 1: Thông tin tài khoản */}
              <div className="space-y-3">
                <div className="flex items-center gap-2 pb-1 border-b border-slate-100 dark:border-slate-800">
                  <Info className="w-3.5 h-3.5 text-slate-400" />
                  <span className="text-[10px] font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400">
                    {isVi ? '1. Thông tin tài khoản' : '1. Account Information'}
                  </span>
                </div>

                <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                  <div className="p-3 rounded-xl bg-slate-50 dark:bg-slate-900/60 border border-slate-100 dark:border-slate-800/80">
                    <span className="text-slate-400 block text-[10px] uppercase font-semibold">
                      {isVi ? 'Họ và tên' : 'Full Name'}
                    </span>
                    <span className="font-medium text-slate-800 dark:text-slate-200">
                      {selectedUser.fullName}
                    </span>
                  </div>
                  <div className="p-3 rounded-xl bg-slate-50 dark:bg-slate-900/60 border border-slate-100 dark:border-slate-800/80">
                    <span className="text-slate-400 block text-[10px] uppercase font-semibold">
                      Email
                    </span>
                    <span className="font-medium text-slate-800 dark:text-slate-200 truncate block">
                      {selectedUser.email}
                    </span>
                  </div>
                  <div className="p-3 rounded-xl bg-slate-50 dark:bg-slate-900/60 border border-slate-100 dark:border-slate-800/80">
                    <span className="text-slate-400 block text-[10px] uppercase font-semibold">
                      SpendID
                    </span>
                    <span className="font-medium font-mono text-slate-800 dark:text-slate-200">
                      {selectedUser.spendId || '—'}
                    </span>
                  </div>
                  <div className="p-3 rounded-xl bg-slate-50 dark:bg-slate-900/60 border border-slate-100 dark:border-slate-800/80">
                    <span className="text-slate-400 block text-[10px] uppercase font-semibold">
                      {isVi ? 'Vai trò hệ thống' : 'System Role'}
                    </span>
                    <span className="font-medium text-slate-800 dark:text-slate-200">
                      {selectedUser.systemRole === 'SystemAdmin'
                        ? (isVi ? 'Quản trị hệ thống' : 'System Administrator')
                        : (isVi ? 'Người dùng' : 'User')}
                    </span>
                  </div>
                </div>

                {/* Dates */}
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-3 text-[11px] text-slate-500 dark:text-slate-400">
                  <div>
                    {isVi ? 'Ngày tạo: ' : 'Created: '}
                    <span className="font-mono text-slate-700 dark:text-slate-300">
                      {selectedUser.createdAt ? new Date(selectedUser.createdAt).toLocaleString(locale) : '—'}
                    </span>
                  </div>
                  <div>
                    {isVi ? 'Đăng nhập cuối: ' : 'Last Login: '}
                    <span className="font-mono text-slate-700 dark:text-slate-300">
                      {selectedUser.lastLoginAt ? new Date(selectedUser.lastLoginAt).toLocaleString(locale) : (isVi ? 'Chưa đăng nhập' : 'Never')}
                    </span>
                  </div>
                </div>

                {/* Privacy Notice */}
                <div className="p-3 rounded-xl bg-amber-50 dark:bg-amber-950/30 border border-amber-200 dark:border-amber-900/40 text-[11px] text-amber-800 dark:text-amber-300">
                  🔒 <strong>{isVi ? 'Bảo vệ riêng tư:' : 'Privacy Protection:'}</strong>{' '}
                  {isVi
                    ? 'Số dư ví cá nhân, ngân sách và mục tiêu tiết kiệm được mã hóa và không hiển thị cho Quản trị viên hệ thống.'
                    : 'Private wallet balances, budgets, and goal targets are encrypted and unavailable to system administrators.'}
                </div>
              </div>

              {/* SECTION 2: Trạng thái tài khoản */}
              <div className="space-y-3 pt-2 border-t border-slate-100 dark:border-slate-800">
                <div className="flex items-center justify-between pb-1 border-b border-slate-100 dark:border-slate-800">
                  <div className="flex items-center gap-2">
                    <ShieldCheck className="w-3.5 h-3.5 text-slate-400" />
                    <span className="text-[10px] font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400">
                      {isVi ? '2. Trạng thái tài khoản' : '2. Account Status'}
                    </span>
                  </div>
                  <span
                    className={`inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-xs font-semibold ${
                      selectedUser.status === 'Active'
                        ? 'bg-emerald-50 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-400 border border-emerald-200/60 dark:border-emerald-800/40'
                        : selectedUser.status === 'Restricted'
                        ? 'bg-amber-50 text-amber-700 dark:bg-amber-950/40 dark:text-amber-400 border border-amber-200/60 dark:border-amber-800/40'
                        : selectedUser.status === 'Suspended'
                        ? 'bg-orange-50 text-orange-700 dark:bg-orange-950/40 dark:text-orange-400 border border-orange-200/60 dark:border-orange-800/40'
                        : 'bg-rose-50 text-rose-700 dark:bg-rose-950/40 dark:text-rose-400 border border-rose-200/60 dark:border-rose-800/40'
                    }`}
                  >
                    {selectedUser.status === 'Active' && <ShieldCheck className="w-3 h-3" />}
                    {selectedUser.status === 'Restricted' && <AlertTriangle className="w-3 h-3" />}
                    {selectedUser.status === 'Suspended' && <Slash className="w-3 h-3" />}
                    {selectedUser.status === 'Locked' && <Lock className="w-3 h-3" />}
                    {selectedUser.status === 'Active'
                      ? (isVi ? 'Hoạt động' : 'Active')
                      : selectedUser.status === 'Restricted'
                      ? (isVi ? 'Hạn chế' : 'Restricted')
                      : selectedUser.status === 'Suspended'
                      ? (isVi ? 'Đình chỉ' : 'Suspended')
                      : (isVi ? 'Đã khóa' : 'Locked')}
                  </span>
                </div>

                {/* Self Protection or Peer SystemAdmin Protection Notice */}
                {selectedUser.id === currentUser?.id ? (
                  <div className="p-3 rounded-xl bg-slate-100 dark:bg-slate-800/80 border border-slate-200 dark:border-slate-700 text-xs text-slate-600 dark:text-slate-300 flex items-center gap-2">
                    <Info className="w-4 h-4 text-slate-400 shrink-0" />
                    <span>
                      {isVi
                        ? 'Bạn đang đăng nhập bằng tài khoản này. Không thể tự khóa hoặc thay đổi trạng thái của chính mình.'
                        : 'You are currently logged in with this account. You cannot alter status or lock your own account.'}
                    </span>
                  </div>
                ) : selectedUser.systemRole === 'SystemAdmin' ? (
                  <div className="p-3 rounded-xl bg-slate-100 dark:bg-slate-800/80 border border-slate-200 dark:border-slate-700 text-xs text-slate-600 dark:text-slate-300 flex items-center gap-2">
                    <Shield className="w-4 h-4 text-emerald-500 shrink-0" />
                    <span>
                      {isVi
                        ? 'Tài khoản Quản trị hệ thống ngang hàng được bảo vệ. Không thể thay đổi trạng thái hoặc đặt lại thông tin xác thực.'
                        : 'Peer SystemAdmin account is protected. Status modifications and credential resets are forbidden.'}
                    </span>
                  </div>
                ) : (
                  <div>
                    <button
                      type="button"
                      id="btn-admin-change-status"
                      data-testid="btn-admin-change-status"
                      onClick={() => openStatusChangeModal(selectedUser)}
                      className="w-full inline-flex items-center justify-center gap-2 px-4 py-2.5 rounded-xl border border-slate-200 dark:border-slate-700 hover:bg-slate-100 dark:hover:bg-slate-800 text-slate-800 dark:text-slate-200 font-semibold transition text-xs cursor-pointer shadow-xs"
                    >
                      <Shield className="w-3.5 h-3.5 text-slate-500" />
                      <span>{isVi ? 'Thay đổi trạng thái' : 'Change Status'}</span>
                    </button>
                  </div>
                )}
              </div>

              {/* SECTION 3: Bảo mật & truy cập */}
              <div className="space-y-3 pt-2 border-t border-slate-100 dark:border-slate-800">
                <div className="flex items-center justify-between pb-1 border-b border-slate-100 dark:border-slate-800">
                  <div className="flex items-center gap-2">
                    <KeyRound className="w-3.5 h-3.5 text-slate-400" />
                    <span className="text-[10px] font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400">
                      {isVi ? '3. Bảo mật & truy cập' : '3. Security & Access'}
                    </span>
                  </div>
                  <span className="text-[11px] font-medium text-slate-500 dark:text-slate-400">
                    {isVi ? 'Mã bảo mật: ' : 'PIN Status: '}
                    {selectedUser.hasSecurityPin ? (
                      <span className="text-emerald-600 dark:text-emerald-400 font-semibold">
                        {isVi ? 'Đã thiết lập' : 'Enrolled'}
                      </span>
                    ) : (
                      <span className="text-amber-600 dark:text-amber-400 font-semibold">
                        {isVi ? 'Chưa thiết lập' : 'Not Enrolled'}
                      </span>
                    )}
                  </span>
                </div>

                {selectedUser.mustChangePassword && (
                  <div className="p-2.5 rounded-xl bg-amber-50 dark:bg-amber-950/30 border border-amber-200 dark:border-amber-900/40 text-[11px] text-amber-800 dark:text-amber-300 flex items-center gap-2">
                    <AlertTriangle className="w-3.5 h-3.5 text-amber-600 shrink-0" />
                    <span>{isVi ? 'Yêu cầu đổi mật khẩu khi đăng nhập tiếp theo đang được kích hoạt.' : 'Password change is enforced on next login.'}</span>
                  </div>
                )}

                <p className="text-[11px] text-slate-500 dark:text-slate-400 leading-relaxed">
                  {isVi
                    ? 'Mọi thao tác can thiệp tài khoản nhạy cảm yêu cầu xác thực bằng mã bảo mật Quản trị viên và được ghi nhận đầy đủ vào Nhật ký kiểm toán hệ thống.'
                    : 'All sensitive administrative interventions require Admin Security PIN authorization and are recorded in the System Audit Log.'}
                </p>

                <div className="grid grid-cols-1 sm:grid-cols-3 gap-2 pt-1">
                  {/* Send Password Reset */}
                  <button
                    type="button"
                    id="btn-admin-send-password-reset"
                    disabled={selectedUser.id === currentUser?.id || selectedUser.systemRole === 'SystemAdmin'}
                    onClick={() => openPasswordResetModal(selectedUser)}
                    className="inline-flex items-center justify-center gap-1.5 px-3 py-2.5 rounded-xl border border-indigo-200 dark:border-indigo-900/50 bg-indigo-50/50 dark:bg-indigo-950/30 text-indigo-700 dark:text-indigo-300 hover:bg-indigo-100 dark:hover:bg-indigo-950/60 font-semibold transition text-xs cursor-pointer disabled:opacity-40 disabled:cursor-not-allowed"
                  >
                    <Mail className="w-3.5 h-3.5" />
                    {isVi ? 'Đặt lại mật khẩu' : 'Reset Password'}
                  </button>

                  {/* Reset Security PIN */}
                  <button
                    type="button"
                    id="btn-admin-reset-pin"
                    disabled={selectedUser.id === currentUser?.id || selectedUser.systemRole === 'SystemAdmin'}
                    onClick={() => openPinResetModal(selectedUser)}
                    className="inline-flex items-center justify-center gap-1.5 px-3 py-2.5 rounded-xl border border-rose-200 dark:border-rose-900/50 bg-rose-50/50 dark:bg-rose-950/30 text-rose-700 dark:text-rose-300 hover:bg-rose-100 dark:hover:bg-rose-950/60 font-semibold transition text-xs cursor-pointer disabled:opacity-40 disabled:cursor-not-allowed"
                  >
                    <KeyRound className="w-3.5 h-3.5" />
                    {isVi ? 'Đặt lại mã PIN' : 'Reset PIN'}
                  </button>

                  {/* Revoke Active Sessions */}
                  <button
                    type="button"
                    id="btn-admin-revoke-sessions"
                    disabled={selectedUser.id === currentUser?.id}
                    onClick={() => openRevokeModal(selectedUser)}
                    className="inline-flex items-center justify-center gap-1.5 px-3 py-2.5 rounded-xl border border-slate-200 dark:border-slate-800 bg-slate-50 dark:bg-slate-900 text-slate-700 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800 font-semibold transition text-xs cursor-pointer disabled:opacity-40 disabled:cursor-not-allowed"
                  >
                    <LogOut className="w-3.5 h-3.5" />
                    {isVi ? 'Thu hồi phiên' : 'Revoke Sessions'}
                  </button>
                </div>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Modal 1: Status Change Modal */}
      {statusModalUser && (
        <div className="fixed inset-0 z-60 flex items-center justify-center p-4 bg-black/60 backdrop-blur-xs">
          <div className="w-full max-w-md rounded-2xl bg-white dark:bg-[#151D2F] border border-slate-200 dark:border-slate-800 p-6 shadow-2xl space-y-4 animate-in fade-in-0 zoom-in-95">
            <div className="flex items-center justify-between border-b border-slate-100 dark:border-slate-800 pb-3">
              <div className="flex items-center gap-2.5">
                <div className="w-9 h-9 rounded-xl bg-slate-100 dark:bg-slate-800 flex items-center justify-center text-slate-700 dark:text-slate-300">
                  <Shield className="w-5 h-5" />
                </div>
                <div>
                  <h3 className="text-sm font-bold text-slate-900 dark:text-white">
                    {isVi ? 'Thay đổi trạng thái tài khoản' : 'Change Account Status'}
                  </h3>
                  <p className="text-[11px] text-slate-500 dark:text-slate-400">
                    {statusModalUser.fullName} ({statusModalUser.email})
                  </p>
                </div>
              </div>
              <button
                type="button"
                onClick={closeStatusModal}
                className="p-1 rounded-lg text-slate-400 hover:text-slate-600 dark:hover:text-slate-200"
              >
                <X className="w-5 h-5" />
              </button>
            </div>

            {/* Target Status Options */}
            <div className="space-y-1.5">
              <label className="block text-xs font-semibold text-slate-700 dark:text-slate-300">
                {isVi ? 'Chọn trạng thái mới' : 'Select New Status'}
              </label>
              <div className="grid grid-cols-2 gap-2">
                {/* Active */}
                <button
                  type="button"
                  onClick={() => setStatusModalTarget('Active')}
                  className={`p-2.5 rounded-xl border text-left transition flex items-center gap-2 ${
                    statusModalTarget === 'Active'
                      ? 'border-emerald-500 bg-emerald-50/50 dark:bg-emerald-950/30 text-emerald-900 dark:text-emerald-200'
                      : 'border-slate-200 dark:border-slate-800 hover:bg-slate-50 dark:hover:bg-slate-900 text-slate-700 dark:text-slate-300'
                  }`}
                >
                  <ShieldCheck className="w-4 h-4 text-emerald-600 shrink-0" />
                  <div className="text-xs font-semibold">{isVi ? 'Hoạt động' : 'Active'}</div>
                </button>

                {/* Restricted */}
                <button
                  type="button"
                  onClick={() => setStatusModalTarget('Restricted')}
                  className={`p-2.5 rounded-xl border text-left transition flex items-center gap-2 ${
                    statusModalTarget === 'Restricted'
                      ? 'border-amber-500 bg-amber-50/50 dark:bg-amber-950/30 text-amber-900 dark:text-amber-200'
                      : 'border-slate-200 dark:border-slate-800 hover:bg-slate-50 dark:hover:bg-slate-900 text-slate-700 dark:text-slate-300'
                  }`}
                >
                  <AlertTriangle className="w-4 h-4 text-amber-500 shrink-0" />
                  <div className="text-xs font-semibold">{isVi ? 'Hạn chế' : 'Restricted'}</div>
                </button>

                {/* Suspended */}
                <button
                  type="button"
                  onClick={() => setStatusModalTarget('Suspended')}
                  className={`p-2.5 rounded-xl border text-left transition flex items-center gap-2 ${
                    statusModalTarget === 'Suspended'
                      ? 'border-orange-500 bg-orange-50/50 dark:bg-orange-950/30 text-orange-900 dark:text-orange-200'
                      : 'border-slate-200 dark:border-slate-800 hover:bg-slate-50 dark:hover:bg-slate-900 text-slate-700 dark:text-slate-300'
                  }`}
                >
                  <Slash className="w-4 h-4 text-orange-500 shrink-0" />
                  <div className="text-xs font-semibold">{isVi ? 'Đình chỉ' : 'Suspended'}</div>
                </button>

                {/* Locked */}
                <button
                  type="button"
                  onClick={() => setStatusModalTarget('Locked')}
                  className={`p-2.5 rounded-xl border text-left transition flex items-center gap-2 ${
                    statusModalTarget === 'Locked'
                      ? 'border-rose-500 bg-rose-50/60 dark:bg-rose-950/40 text-rose-900 dark:text-rose-200'
                      : 'border-slate-200 dark:border-slate-800 hover:bg-slate-50 dark:hover:bg-slate-900 text-slate-700 dark:text-slate-300'
                  }`}
                >
                  <Lock className="w-4 h-4 text-rose-600 shrink-0" />
                  <div className="text-xs font-semibold">{isVi ? 'Khóa tài khoản' : 'Locked'}</div>
                </button>
              </div>
            </div>

            {/* Mandatory Reason */}
            <div className="space-y-1.5">
              <label className="block text-xs font-semibold text-slate-700 dark:text-slate-300">
                {isVi ? 'Lý do thực hiện' : 'Action Reason'}{' '}
                <span className="text-rose-500">*</span>
              </label>
              <textarea
                value={statusModalReason}
                onChange={(e) => {
                  setStatusModalReason(e.target.value);
                  if (statusModalError) setStatusModalError(null);
                }}
                rows={2}
                placeholder={isVi ? 'Nhập lý do thay đổi trạng thái (bắt buộc)...' : 'Provide justification reason...'}
                className="w-full p-2.5 text-xs rounded-xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-900 text-slate-900 dark:text-slate-100 placeholder:text-slate-400 focus:outline-hidden focus:ring-1 focus:ring-emerald-500"
              />
            </div>

            {/* Mandatory Admin 6-Digit Security PIN */}
            <div className="space-y-1.5">
              <div className="flex items-center justify-between">
                <label className="block text-xs font-semibold text-slate-700 dark:text-slate-300">
                  {isVi ? 'Mã bảo mật Quản trị viên' : 'Admin Security PIN'}{' '}
                  <span className="text-rose-500">*</span>
                </label>
                <span className="text-[10px] text-slate-400 font-mono">
                  {isVi ? '6 chữ số' : '6 digits'}
                </span>
              </div>
              <input
                type="password"
                inputMode="numeric"
                maxLength={6}
                value={statusModalPin}
                onChange={(e) => {
                  const sanitized = e.target.value.replace(/\D/g, '').slice(0, 6);
                  setStatusModalPin(sanitized);
                  if (statusModalError) setStatusModalError(null);
                }}
                placeholder="••••••"
                className="w-full p-2.5 text-xs font-mono tracking-widest rounded-xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-900 text-slate-900 dark:text-slate-100 placeholder:text-slate-400 focus:outline-hidden focus:ring-1 focus:ring-emerald-500"
              />
            </div>

            {/* Error message */}
            {statusModalError && (
              <div className="p-2.5 rounded-xl bg-rose-50 dark:bg-rose-950/40 border border-rose-200 dark:border-rose-900/50 text-rose-600 dark:text-rose-400 text-xs flex items-center gap-2">
                <AlertTriangle className="w-4 h-4 shrink-0" />
                <span>{statusModalError}</span>
              </div>
            )}

            {/* Footer Buttons */}
            <div className="flex items-center gap-2 pt-2">
              <button
                type="button"
                onClick={closeStatusModal}
                disabled={statusModalIsSubmitting}
                className="flex-1 py-2.5 px-3 rounded-xl border border-slate-200 dark:border-slate-800 text-xs font-semibold text-slate-700 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800 transition"
              >
                {t('common.cancel')}
              </button>
              <button
                type="button"
                onClick={executeStatusChange}
                disabled={statusModalIsSubmitting || !statusModalReason.trim() || statusModalPin.length !== 6}
                className={`flex-1 py-2.5 px-3 rounded-xl text-xs font-semibold text-white shadow-sm transition disabled:opacity-50 disabled:cursor-not-allowed ${
                  statusModalTarget === 'Locked'
                    ? 'bg-rose-600 hover:bg-rose-700'
                    : 'bg-emerald-600 hover:bg-emerald-700'
                }`}
              >
                {statusModalIsSubmitting
                  ? (isVi ? 'Đang xử lý...' : 'Processing...')
                  : (isVi ? 'Xác nhận cập nhật' : 'Confirm Update')}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Modal 2: Admin Password Reset Modal */}
      {passwordResetModalUser && (
        <div className="fixed inset-0 z-60 flex items-center justify-center p-4 bg-black/60 backdrop-blur-xs">
          <div className="w-full max-w-md rounded-2xl bg-white dark:bg-[#151D2F] border border-slate-200 dark:border-slate-800 p-6 shadow-2xl space-y-4 animate-in fade-in-0 zoom-in-95">
            <div className="flex items-center justify-between border-b border-slate-100 dark:border-slate-800 pb-3">
              <div className="flex items-center gap-2.5">
                <div className="w-9 h-9 rounded-xl bg-amber-50 dark:bg-amber-950/50 text-amber-600 dark:text-amber-400 flex items-center justify-center">
                  <KeyRound className="w-5 h-5" />
                </div>
                <div>
                  <h3 className="text-sm font-bold text-slate-900 dark:text-white">
                    {isVi ? 'Đặt lại mật khẩu người dùng' : 'Reset User Password'}
                  </h3>
                  <p className="text-[11px] text-slate-500 dark:text-slate-400">
                    {passwordResetModalUser.fullName} ({passwordResetModalUser.email})
                  </p>
                </div>
              </div>
              <button
                type="button"
                onClick={closePasswordResetModal}
                className="p-1 rounded-lg text-slate-400 hover:text-slate-600 dark:hover:text-slate-200 cursor-pointer"
              >
                <X className="w-5 h-5" />
              </button>
            </div>

            {/* Backend-authoritative notice */}
            <div className="p-3 rounded-xl bg-amber-50 dark:bg-amber-950/40 border border-amber-200/80 dark:border-amber-900/40 text-xs text-amber-900 dark:text-amber-200 space-y-1">
              <span className="font-bold block">
                {isVi ? 'Quy trình đặt lại mật khẩu an toàn:' : 'Secure Password Reset Protocol:'}
              </span>
              <ul className="list-disc pl-4 text-[11px] space-y-0.5 text-amber-800/90 dark:text-amber-300/90">
                <li>
                  {isVi
                    ? 'Máy chủ sẽ sinh ngẫu nhiên một mật khẩu tạm thời bảo mật.'
                    : 'The server will randomly generate a secure temporary password.'}
                </li>
                <li>
                  {isVi
                    ? 'Toàn bộ phiên làm việc của người dùng sẽ bị chấm dứt ngay lập tức.'
                    : 'All active sessions of the user will be terminated immediately.'}
                </li>
                <li>
                  {isVi
                    ? 'Người dùng bắt buộc phải đổi mật khẩu mới trong lần đăng nhập tiếp theo.'
                    : 'User will be forced to change their password on their next login.'}
                </li>
              </ul>
            </div>

            {/* Mandatory Reason */}
            <div className="space-y-1.5">
              <label className="block text-xs font-semibold text-slate-700 dark:text-slate-300">
                {isVi ? 'Lý do thực hiện' : 'Action Reason'}{' '}
                <span className="text-rose-500">*</span>
              </label>
              <textarea
                value={passwordResetReason}
                onChange={(e) => {
                  setPasswordResetReason(e.target.value);
                  if (passwordResetError) setPasswordResetError(null);
                }}
                rows={2}
                placeholder={isVi ? 'Nhập lý do đặt lại mật khẩu (bắt buộc)...' : 'Provide reason for resetting password...'}
                className="w-full p-2.5 text-xs rounded-xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-900 text-slate-900 dark:text-slate-100 placeholder:text-slate-400 focus:outline-hidden focus:ring-1 focus:ring-amber-500"
              />
            </div>

            {/* Mandatory Admin 6-Digit Security PIN */}
            <div className="space-y-1.5">
              <div className="flex items-center justify-between">
                <label className="block text-xs font-semibold text-slate-700 dark:text-slate-300">
                  {isVi ? 'Mã bảo mật Quản trị viên' : 'Admin Security PIN'}{' '}
                  <span className="text-rose-500">*</span>
                </label>
                <span className="text-[10px] text-slate-400 font-mono">
                  {isVi ? '6 chữ số' : '6 digits'}
                </span>
              </div>
              <input
                type="password"
                inputMode="numeric"
                maxLength={6}
                value={passwordResetPin}
                onChange={(e) => {
                  const sanitized = e.target.value.replace(/\D/g, '').slice(0, 6);
                  setPasswordResetPin(sanitized);
                  if (passwordResetError) setPasswordResetError(null);
                }}
                placeholder="••••••"
                className="w-full p-2.5 text-xs font-mono tracking-widest rounded-xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-900 text-slate-900 dark:text-slate-100 placeholder:text-slate-400 focus:outline-hidden focus:ring-1 focus:ring-amber-500"
              />
            </div>

            {/* Error message */}
            {passwordResetError && (
              <div className="p-2.5 rounded-xl bg-rose-50 dark:bg-rose-950/40 border border-rose-200 dark:border-rose-900/50 text-rose-600 dark:text-rose-400 text-xs flex items-center gap-2">
                <AlertTriangle className="w-4 h-4 shrink-0" />
                <span>{passwordResetError}</span>
              </div>
            )}

            {/* Footer Buttons */}
            <div className="flex items-center gap-2 pt-2">
              <button
                type="button"
                onClick={closePasswordResetModal}
                disabled={passwordResetIsSubmitting}
                className="flex-1 py-2.5 px-3 rounded-xl border border-slate-200 dark:border-slate-800 text-xs font-semibold text-slate-700 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800 transition cursor-pointer"
              >
                {t('common.cancel')}
              </button>
              <button
                type="button"
                id="btn-admin-confirm-password-reset"
                onClick={executePasswordReset}
                disabled={passwordResetIsSubmitting || !passwordResetReason.trim() || passwordResetPin.length !== 6}
                className="flex-1 py-2.5 px-3 rounded-xl text-xs font-semibold text-white bg-amber-600 hover:bg-amber-500 shadow-sm transition disabled:opacity-50 disabled:cursor-not-allowed cursor-pointer"
              >
                {passwordResetIsSubmitting
                  ? (isVi ? 'Đang xử lý...' : 'Processing...')
                  : (isVi ? 'Tạo mật khẩu tạm' : 'Generate Temp Password')}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Modal 2B: Temporary Password Issued Dialog */}
      {issuedTempPassword && (
        <div className="fixed inset-0 z-70 flex items-center justify-center p-4 bg-black/70 backdrop-blur-xs">
          <div className="w-full max-w-md rounded-2xl bg-white dark:bg-[#151D2F] border border-slate-200 dark:border-slate-800 p-6 shadow-2xl space-y-4 animate-in fade-in-0 zoom-in-95">
            <div className="flex items-center gap-3">
              <div className="w-10 h-10 rounded-xl bg-emerald-50 dark:bg-emerald-950/50 text-emerald-600 dark:text-emerald-400 flex items-center justify-center shrink-0">
                <CheckCircle2 className="w-6 h-6" />
              </div>
              <div>
                <h3 className="text-sm font-bold text-slate-900 dark:text-white">
                  {isVi ? 'Mật khẩu tạm thời đã tạo' : 'Temporary Password Generated'}
                </h3>
                <p className="text-[11px] text-slate-500 dark:text-slate-400">
                  {issuedTempPassword.user.fullName} ({issuedTempPassword.user.email})
                </p>
              </div>
            </div>

            <div className="p-4 rounded-xl bg-slate-50 dark:bg-slate-900 border border-slate-200 dark:border-slate-800 space-y-2">
              <span className="text-[11px] font-semibold text-slate-500 uppercase tracking-wider block">
                {isVi ? 'Mật khẩu tạm thời được cấp:' : 'Issued Temporary Password:'}
              </span>
              <div className="flex items-center justify-between gap-2 p-2.5 bg-white dark:bg-slate-800 rounded-lg border border-slate-200 dark:border-slate-700">
                <span className="font-mono text-base font-bold text-slate-900 dark:text-white select-all">
                  {issuedTempPassword.result.temporaryPassword}
                </span>
                <button
                  type="button"
                  onClick={() => {
                    navigator.clipboard.writeText(issuedTempPassword.result.temporaryPassword);
                    setHasCopiedPassword(true);
                    setTimeout(() => setHasCopiedPassword(false), 2000);
                  }}
                  className="px-2.5 py-1 text-xs font-semibold rounded bg-emerald-50 text-emerald-700 dark:bg-emerald-950 dark:text-emerald-300 hover:bg-emerald-100 flex items-center gap-1 transition"
                >
                  <Copy className="w-3.5 h-3.5" />
                  {hasCopiedPassword ? (isVi ? 'Đã chép!' : 'Copied!') : (isVi ? 'Sao chép' : 'Copy')}
                </button>
              </div>
            </div>

            <div className="p-3 rounded-xl bg-blue-50 dark:bg-blue-950/40 border border-blue-200 dark:border-blue-900/40 text-[11px] text-blue-800 dark:text-blue-300">
              ⚠️ <strong>{isVi ? 'Lưu ý bảo mật quan trọng:' : 'Important Security Notice:'}</strong>{' '}
              {isVi
                ? 'Mật khẩu tạm thời này chỉ hiển thị một lần duy nhất tại đây và không được lưu trữ. Hãy chuyển an toàn cho người dùng và nhắc nhở họ đổi mật khẩu ngay.'
                : 'This temporary password is only displayed once and will not be stored. Transmit it securely to the user and remind them to change it on login.'}
            </div>

            <button
              type="button"
              onClick={() => setIssuedTempPassword(null)}
              className="w-full py-2.5 px-4 rounded-xl text-xs font-semibold text-white bg-slate-900 hover:bg-slate-800 dark:bg-emerald-600 dark:hover:bg-emerald-500 transition shadow-sm"
            >
              {isVi ? 'Tôi đã lưu và hiểu thông tin' : 'I have safely noted this password'}
            </button>
          </div>
        </div>
      )}

      {/* Modal 3: Admin Security PIN Reset Modal */}
      {pinResetModalUser && (
        <div className="fixed inset-0 z-60 flex items-center justify-center p-4 bg-black/60 backdrop-blur-xs">
          <div className="w-full max-w-md rounded-2xl bg-white dark:bg-[#151D2F] border border-slate-200 dark:border-slate-800 p-6 shadow-2xl space-y-4 animate-in fade-in-0 zoom-in-95">
            <div className="flex items-center justify-between border-b border-slate-100 dark:border-slate-800 pb-3">
              <div className="flex items-center gap-2.5">
                <div className="w-9 h-9 rounded-xl bg-rose-50 dark:bg-rose-950/50 text-rose-600 dark:text-rose-400 flex items-center justify-center">
                  <KeyRound className="w-5 h-5" />
                </div>
                <div>
                  <h3 className="text-sm font-bold text-slate-900 dark:text-white">
                    {isVi ? 'Đặt lại mã bảo mật người dùng' : 'Reset User Security PIN'}
                  </h3>
                  <p className="text-[11px] text-slate-500 dark:text-slate-400">
                    {pinResetModalUser.fullName} ({pinResetModalUser.email})
                  </p>
                </div>
              </div>
              <button
                type="button"
                onClick={closePinResetModal}
                className="p-1 rounded-lg text-slate-400 hover:text-slate-600 dark:hover:text-slate-200"
              >
                <X className="w-5 h-5" />
              </button>
            </div>

            <p className="text-xs text-slate-600 dark:text-slate-300 leading-relaxed">
              {isVi
                ? `Mã bảo mật hiện tại của ${pinResetModalUser.fullName} sẽ bị vô hiệu hóa ngay trên máy chủ. Người dùng sẽ bắt buộc phải thiết lập mã mới khi đăng nhập.`
                : `The current Security PIN for ${pinResetModalUser.fullName} will be invalidated on the server. The user will be required to enroll a new PIN on next login.`}
            </p>

            {/* Mandatory Reason */}
            <div className="space-y-1.5">
              <label className="block text-xs font-semibold text-slate-700 dark:text-slate-300">
                {isVi ? 'Lý do thực hiện' : 'Action Reason'}{' '}
                <span className="text-rose-500">*</span>
              </label>
              <textarea
                value={pinResetReason}
                onChange={(e) => {
                  setPinResetReason(e.target.value);
                  if (pinResetError) setPinResetError(null);
                }}
                rows={2}
                placeholder={isVi ? 'Nhập lý do đặt lại mã bảo mật (bắt buộc)...' : 'Provide reason for PIN reset...'}
                className="w-full p-2.5 text-xs rounded-xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-900 text-slate-900 dark:text-slate-100 placeholder:text-slate-400 focus:outline-hidden focus:ring-1 focus:ring-emerald-500"
              />
            </div>

            {/* Mandatory Admin 6-Digit Security PIN */}
            <div className="space-y-1.5">
              <div className="flex items-center justify-between">
                <label className="block text-xs font-semibold text-slate-700 dark:text-slate-300">
                  {isVi ? 'Mã bảo mật Quản trị viên' : 'Admin Security PIN'}{' '}
                  <span className="text-rose-500">*</span>
                </label>
                <span className="text-[10px] text-slate-400 font-mono">
                  {isVi ? '6 chữ số' : '6 digits'}
                </span>
              </div>
              <input
                type="password"
                inputMode="numeric"
                maxLength={6}
                value={pinResetPin}
                onChange={(e) => {
                  const sanitized = e.target.value.replace(/\D/g, '').slice(0, 6);
                  setPinResetPin(sanitized);
                  if (pinResetError) setPinResetError(null);
                }}
                placeholder="••••••"
                className="w-full p-2.5 text-xs font-mono tracking-widest rounded-xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-900 text-slate-900 dark:text-slate-100 placeholder:text-slate-400 focus:outline-hidden focus:ring-1 focus:ring-emerald-500"
              />
            </div>

            {/* Error message */}
            {pinResetError && (
              <div className="p-2.5 rounded-xl bg-rose-50 dark:bg-rose-950/40 border border-rose-200 dark:border-rose-900/50 text-rose-600 dark:text-rose-400 text-xs flex items-center gap-2">
                <AlertTriangle className="w-4 h-4 shrink-0" />
                <span>{pinResetError}</span>
              </div>
            )}

            {/* Footer Buttons */}
            <div className="flex items-center gap-2 pt-2">
              <button
                type="button"
                onClick={closePinResetModal}
                disabled={pinResetIsSubmitting}
                className="flex-1 py-2.5 px-3 rounded-xl border border-slate-200 dark:border-slate-800 text-xs font-semibold text-slate-700 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800 transition"
              >
                {t('common.cancel')}
              </button>
              <button
                type="button"
                onClick={executePinReset}
                disabled={pinResetIsSubmitting || !pinResetReason.trim() || pinResetPin.length !== 6}
                className="flex-1 py-2.5 px-3 rounded-xl text-xs font-semibold text-white bg-rose-600 hover:bg-rose-700 shadow-sm transition disabled:opacity-50 disabled:cursor-not-allowed"
              >
                {pinResetIsSubmitting
                  ? (isVi ? 'Đang xử lý...' : 'Processing...')
                  : (isVi ? 'Xác nhận đặt lại PIN' : 'Confirm Reset PIN')}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Modal 4: Revoke User Sessions Modal */}
      {revokeModalUser && (
        <div className="fixed inset-0 z-60 flex items-center justify-center p-4 bg-black/60 backdrop-blur-xs">
          <div className="w-full max-w-md rounded-2xl bg-white dark:bg-[#151D2F] border border-slate-200 dark:border-slate-800 p-6 shadow-2xl space-y-4 animate-in fade-in-0 zoom-in-95">
            <div className="flex items-center justify-between border-b border-slate-100 dark:border-slate-800 pb-3">
              <div className="flex items-center gap-2.5">
                <div className="w-9 h-9 rounded-xl bg-slate-100 dark:bg-slate-800 text-slate-700 dark:text-slate-300 flex items-center justify-center">
                  <LogOut className="w-5 h-5" />
                </div>
                <div>
                  <h3 className="text-sm font-bold text-slate-900 dark:text-white">
                    {isVi ? 'Thu hồi toàn bộ phiên đăng nhập' : 'Revoke All User Sessions'}
                  </h3>
                  <p className="text-[11px] text-slate-500 dark:text-slate-400">
                    {revokeModalUser.fullName} ({revokeModalUser.email})
                  </p>
                </div>
              </div>
              <button
                type="button"
                onClick={closeRevokeModal}
                className="p-1 rounded-lg text-slate-400 hover:text-slate-600 dark:hover:text-slate-200"
              >
                <X className="w-5 h-5" />
              </button>
            </div>

            <p className="text-xs text-slate-600 dark:text-slate-300 leading-relaxed">
              {isVi
                ? `Tất cả refresh tokens và phiên làm việc đang hoạt động của ${revokeModalUser.fullName} sẽ bị hủy ngay lập tức. Người dùng sẽ bị đăng xuất khỏi tất cả các thiết bị.`
                : `All active refresh tokens and sessions for ${revokeModalUser.fullName} will be terminated immediately. The user will be logged out of all devices.`}
            </p>

            {/* Mandatory Reason */}
            <div className="space-y-1.5">
              <label className="block text-xs font-semibold text-slate-700 dark:text-slate-300">
                {isVi ? 'Lý do thực hiện' : 'Action Reason'}{' '}
                <span className="text-rose-500">*</span>
              </label>
              <textarea
                value={revokeReason}
                onChange={(e) => {
                  setRevokeReason(e.target.value);
                  if (revokeError) setRevokeError(null);
                }}
                rows={2}
                placeholder={isVi ? 'Nhập lý do thu hồi phiên đăng nhập (bắt buộc)...' : 'Provide reason for revoking sessions...'}
                className="w-full p-2.5 text-xs rounded-xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-900 text-slate-900 dark:text-slate-100 placeholder:text-slate-400 focus:outline-hidden focus:ring-1 focus:ring-emerald-500"
              />
            </div>

            {/* Error message */}
            {revokeError && (
              <div className="p-2.5 rounded-xl bg-rose-50 dark:bg-rose-950/40 border border-rose-200 dark:border-rose-900/50 text-rose-600 dark:text-rose-400 text-xs flex items-center gap-2">
                <AlertTriangle className="w-4 h-4 shrink-0" />
                <span>{revokeError}</span>
              </div>
            )}

            {/* Footer Buttons */}
            <div className="flex items-center gap-2 pt-2">
              <button
                type="button"
                onClick={closeRevokeModal}
                disabled={revokeIsSubmitting}
                className="flex-1 py-2.5 px-3 rounded-xl border border-slate-200 dark:border-slate-800 text-xs font-semibold text-slate-700 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800 transition"
              >
                {t('common.cancel')}
              </button>
              <button
                type="button"
                onClick={executeRevokeSessions}
                disabled={revokeIsSubmitting || !revokeReason.trim()}
                className="flex-1 py-2.5 px-3 rounded-xl text-xs font-semibold text-white bg-slate-900 hover:bg-slate-800 dark:bg-emerald-600 dark:hover:bg-emerald-700 shadow-sm transition disabled:opacity-50 disabled:cursor-not-allowed"
              >
                {revokeIsSubmitting
                  ? (isVi ? 'Đang xử lý...' : 'Processing...')
                  : (isVi ? 'Xác nhận thu hồi' : 'Confirm Revoke')}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

export default AdminUsersPage;
