import React, { useState, useEffect } from 'react';
import { useSearchParams, useNavigate } from 'react-router-dom';
import {
  User,
  Shield,
  Palette,
  Bell,
  Eye,
  EyeOff,
  CheckCircle2,
  Sun,
  Moon,
  Laptop,
  Check,
  Smartphone,
  KeyRound,
  Monitor,
  Trash2,
  Plus,
  X,
  AlertCircle,
  LifeBuoy,
  AlertTriangle,
  MessageSquare,
  ShieldAlert,
  LogOut,
  Loader2,
  ArrowRight,
  ExternalLink,
} from 'lucide-react';
import { useI18n } from '../../i18n/I18nContext';
import { useTheme } from '../../providers/ThemeProvider';
import { useBalanceVisibility } from '../../providers/BalanceVisibilityProvider';
import { useAuth } from '../../providers/AuthProvider';
import { useNotifications } from '../../providers/NotificationProvider';
import { useFinancial } from '../../providers/FinancialProvider';
import { formatMoney } from '../../utils/money';
import { formatDate } from '../../utils/date';
import { Button } from '../../components/ui/Button';
import { Select } from '../../components/ui/Select';
import { cn } from '../../utils/cn';
import { SixDigitPinInput } from '../../components/security/SixDigitPinInput';
import { securityPinService, isValidSecurityPin } from '../../services/securityPinService';
import { authApiService } from '../../services/authApiService';
import { APP_META } from '../../config/appMeta';

export const SettingsPage: React.FC = () => {
  const { t, locale, setLocale } = useI18n();
  const { theme, setTheme } = useTheme();
  const { isHidden, toggleVisibility } = useBalanceVisibility();
  const { currentUser, logout, refreshCurrentUser, updateCurrentUser } = useAuth();
  const { accountPreferences, updateAccountPreferences } = useNotifications();
  const { wallets, transactions } = useFinancial();
  const [searchParams, setSearchParams] = useSearchParams();
  const navigate = useNavigate();

  const [toastMessage, setToastMessage] = useState<string | null>(null);
  const [isUpdatingEngagementOptIn, setIsUpdatingEngagementOptIn] = useState(false);

  // Tab navigation from query params (supports ?tab=profile as alias for account, ?tab=help as alias for support)
  const tabParam = searchParams.get('tab');
  const normalizedTab =
    tabParam === 'profile' ? 'account' : tabParam === 'help' ? 'support' : tabParam;
  const activeTab =
    normalizedTab &&
    ['account', 'security', 'preferences', 'notifications', 'support'].includes(normalizedTab)
      ? normalizedTab
      : 'all';

  // Redirect legacy /settings?tab=support or /settings?tab=help directly to /support or /disputes
  useEffect(() => {
    if (tabParam === 'support' || tabParam === 'help') {
      const subtab = searchParams.get('subtab');
      const tx = searchParams.get('tx');
      if (subtab === 'disputes' || tx) {
        navigate(tx ? `/disputes?tx=${tx}` : '/disputes', { replace: true });
      } else {
        navigate('/support', { replace: true });
      }
    }
  }, [tabParam, searchParams, navigate]);

  const handleTabChange = (tabKey: string) => {
    if (tabKey === 'support') {
      navigate('/support');
      return;
    }
    if (tabKey === 'all') {
      searchParams.delete('tab');
      setSearchParams(searchParams);
    } else {
      setSearchParams({ tab: tabKey });
    }
  };

  const showToast = (msg: string) => {
    setToastMessage(msg);
    setTimeout(() => setToastMessage(null), 3000);
  };

  // Change Password Modal & Form State
  const [isChangePasswordOpen, setIsChangePasswordOpen] = useState(false);
  const [isSubmittingPassword, setIsSubmittingPassword] = useState(false);
  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [passwordError, setPasswordError] = useState<string | null>(null);

  // Sign Out All Sessions Modal & Submitting State
  const [isSignOutAllOpen, setIsSignOutAllOpen] = useState(false);
  const [isSigningOutAll, setIsSigningOutAll] = useState(false);

  // Password validation checks (SpendWise backend policy: 8..128 chars, 1 uppercase, 1 lowercase, 1 digit)
  const hasMinLength = newPassword.length >= 8;
  const hasUppercase = /[A-Z]/.test(newPassword);
  const hasLowercase = /[a-z]/.test(newPassword);
  const hasDigit = /[0-9]/.test(newPassword);
  const isPasswordValid = hasMinLength && hasUppercase && hasLowercase && hasDigit;

  // Accessibility: Escape key handler for Settings modals
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        if (isChangePasswordOpen && !isSubmittingPassword) {
          setIsChangePasswordOpen(false);
          setPasswordError(null);
        } else if (isSignOutAllOpen && !isSigningOutAll) {
          setIsSignOutAllOpen(false);
        }
      }
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [isChangePasswordOpen, isSubmittingPassword, isSignOutAllOpen, isSigningOutAll]);

  // Default wallet notification preferences (Single source of truth)
  const defaultWallet = wallets.find((w) => w.isDefault) || wallets[0];
  const targetWalletId = defaultWallet?.id;

  const mainPrefs = (targetWalletId ? accountPreferences[targetWalletId] : undefined) || {
    accountId: targetWalletId || '',
    accountName: defaultWallet?.name || (locale === 'vi-VN' ? 'Ví chính' : 'Main Wallet'),
    notifyBalanceChanges: true,
    notifyBudgetAlerts: true,
    notifyLargeTransactions: true,
    largeTransactionThreshold: 1000000,
    notifyMinBalanceAlert: true,
    minBalanceThreshold: 500000,
  };

  const handleTogglePref = async (
    field: 'notifyBalanceChanges' | 'notifyBudgetAlerts' | 'notifyLargeTransactions' | 'notifyMinBalanceAlert',
    val: boolean
  ) => {
    const key = targetWalletId || 'all';
    try {
      await updateAccountPreferences(key, { [field]: val });
      showToast(locale === 'vi-VN' ? 'Đã cập nhật tùy chọn thông báo' : 'Notification preferences updated');
    } catch (err: any) {
      console.error('Failed to update notification preference:', err);
      showToast(
        err?.message ||
          (locale === 'vi-VN'
            ? 'Không thể cập nhật tùy chọn thông báo.'
            : 'Failed to update notification preferences.')
      );
    }
  };

  // Password submission handler with 8..128 char policy validation
  const handleChangePasswordSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!currentPassword) {
      setPasswordError(locale === 'vi-VN' ? 'Vui lòng nhập mật khẩu hiện tại.' : 'Please enter your current password.');
      return;
    }
    if (!hasMinLength) {
      setPasswordError(locale === 'vi-VN' ? 'Mật khẩu mới phải có ít nhất 8 ký tự.' : 'Password must be at least 8 characters.');
      return;
    }
    if (!hasUppercase) {
      setPasswordError(locale === 'vi-VN' ? 'Mật khẩu mới phải chứa ít nhất 1 chữ in hoa.' : 'Password must contain at least 1 uppercase letter.');
      return;
    }
    if (!hasLowercase) {
      setPasswordError(locale === 'vi-VN' ? 'Mật khẩu mới phải chứa ít nhất 1 chữ in thường.' : 'Password must contain at least 1 lowercase letter.');
      return;
    }
    if (!hasDigit) {
      setPasswordError(locale === 'vi-VN' ? 'Mật khẩu mới phải chứa ít nhất 1 chữ số.' : 'Password must contain at least 1 digit.');
      return;
    }
    if (newPassword !== confirmPassword) {
      setPasswordError(t('security.passwordMismatch'));
      return;
    }

    setIsSubmittingPassword(true);
    try {
      await authApiService.changePassword({ currentPassword, newPassword });
      setPasswordError(null);
      setCurrentPassword('');
      setNewPassword('');
      setConfirmPassword('');
      setIsChangePasswordOpen(false);
      showToast(t('security.passwordUpdatedToast'));
      await refreshCurrentUser();
    } catch (err: any) {
      setPasswordError(
        err?.detail ||
          err?.title ||
          (locale === 'vi-VN'
            ? 'Đổi mật khẩu thất bại. Vui lòng kiểm tra mật khẩu hiện tại.'
            : 'Failed to change password. Please check your current password.')
      );
    } finally {
      setIsSubmittingPassword(false);
    }
  };

  // Sign out all sessions connected to real backend
  const handleSignOutAllSessions = async () => {
    setIsSigningOutAll(true);
    try {
      await authApiService.revokeAllSessions();
      setIsSignOutAllOpen(false);
      showToast(t('security.signOutAllSuccess'));
      await logout();
    } catch (err: any) {
      showToast(
        err?.detail ||
          err?.title ||
          (locale === 'vi-VN' ? 'Đăng xuất thất bại.' : 'Sign out failed.')
      );
    } finally {
      setIsSigningOutAll(false);
    }
  };

  // Security PIN State
  const [hasPin, setHasPin] = useState<boolean | null>(null);
  const [isChangePinOpen, setIsChangePinOpen] = useState(false);
  const [pinModalStage, setPinModalStage] = useState<'old_pin' | 'new_pin' | 'confirm_pin'>('old_pin');
  const [oldPin, setOldPin] = useState('');
  const [newPin, setNewPin] = useState('');
  const [confirmNewPin, setConfirmNewPin] = useState('');
  const [showPin, setShowPin] = useState(false);
  const [changePinError, setChangePinError] = useState<string | null>(null);
  const [isSavingPin, setIsSavingPin] = useState(false);

  useEffect(() => {
    if (currentUser?.id) {
      securityPinService.getStatus().then((res) => {
        setHasPin(res.isSet);
      }).catch(() => {
        setHasPin(null);
      });
    }
  }, [currentUser]);

  const handleOldPinComplete = async (enteredOldPin: string) => {
    if (!currentUser?.id || isSavingPin) return;
    setIsSavingPin(true);
    setChangePinError(null);
    try {
      const isOldValid = await securityPinService.verifyPin(enteredOldPin);
      if (isOldValid) {
        setOldPin(enteredOldPin);
        setPinModalStage('new_pin');
        setNewPin('');
        setConfirmNewPin('');
      } else {
        setChangePinError(
          locale === 'vi-VN'
            ? 'Mã bảo mật hiện tại không chính xác.'
            : 'Incorrect current Security PIN.'
        );
        setOldPin('');
      }
    } catch (err: any) {
      setChangePinError(
        err?.detail ||
          err?.title ||
          (locale === 'vi-VN'
            ? 'Máy chủ không khả dụng hoặc lỗi kết nối. Vui lòng thử lại sau.'
            : 'Service unavailable or network connection error. Please try again later.')
      );
      setOldPin('');
    } finally {
      setIsSavingPin(false);
    }
  };

  const handleNewPinComplete = (enteredNewPin: string) => {
    if (!isValidSecurityPin(enteredNewPin)) return;
    if (hasPin && enteredNewPin === oldPin) {
      setChangePinError(
        locale === 'vi-VN'
          ? 'Mã bảo mật mới không được trùng mã hiện tại.'
          : 'New Security PIN cannot be the same as current PIN.'
      );
      return;
    }
    setNewPin(enteredNewPin);
    setChangePinError(null);
    setPinModalStage('confirm_pin');
    setConfirmNewPin('');
  };

  const handleConfirmPinComplete = async (enteredConfirmPin: string) => {
    if (!currentUser?.id || isSavingPin) return;

    if (enteredConfirmPin !== newPin) {
      setChangePinError(
        locale === 'vi-VN'
          ? 'Mã bảo mật xác nhận không khớp. Vui lòng nhập lại.'
          : 'Security PIN confirmation does not match. Please try again.'
      );
      setConfirmNewPin('');
      return;
    }

    setIsSavingPin(true);
    setChangePinError(null);

    try {
      const res = await securityPinService.changePin(oldPin, newPin, enteredConfirmPin);
      if (res.success) {
        setHasPin(true);
        setIsChangePinOpen(false);
        setOldPin('');
        setNewPin('');
        setConfirmNewPin('');
        showToast(
          locale === 'vi-VN'
            ? 'Đã cập nhật Mã bảo mật 6 số thành công.'
            : '6-digit Security PIN updated successfully.'
        );
      } else {
        setChangePinError(
          res.error === 'INVALID_PIN'
            ? (locale === 'vi-VN' ? 'Mã bảo mật không hợp lệ hoặc không khớp.' : 'Invalid PIN or PINs do not match.')
            : (locale === 'vi-VN' ? 'Không thể lưu Mã bảo mật. Vui lòng thử lại.' : 'Failed to update Security PIN. Please try again.')
        );
        setConfirmNewPin('');
      }
    } catch {
      setChangePinError(
        locale === 'vi-VN'
          ? 'Đã xảy ra lỗi khi lưu mã bảo mật.'
          : 'Error saving Security PIN.'
      );
      setConfirmNewPin('');
    } finally {
      setIsSavingPin(false);
    }
  };

  const handleForgotPinFromSettings = async () => {
    if (!currentUser?.email) return;
    try {
      await securityPinService.createPinResetRequest(currentUser.email);
      showToast(
        locale === 'vi-VN'
          ? 'Đã gửi liên kết khôi phục Mã bảo mật đến email của bạn.'
          : 'Security PIN recovery link sent to your email.'
      );
    } catch {
      showToast(
        locale === 'vi-VN'
          ? 'Không thể tạo yêu cầu khôi phục mã bảo mật.'
          : 'Could not create PIN reset request.'
      );
    }
  };

  return (
    <div className="max-w-4xl mx-auto space-y-6 pb-12 animate-in fade-in-50 duration-200">
      {/* Toast */}
      {toastMessage && (
        <div className="fixed top-6 right-6 z-50 flex items-center gap-2 px-4 py-2.5 rounded-xl bg-slate-900 text-white shadow-lg border border-emerald-500/30 text-xs font-medium animate-in slide-in-from-top-2">
          <CheckCircle2 className="h-4 w-4 text-emerald-400 shrink-0" />
          <span>{toastMessage}</span>
        </div>
      )}

      {/* 1. Account Summary Bar (Clean, Minimal, No Dark Gradient Hero) */}
      <div className="bg-white dark:bg-slate-900 rounded-2xl p-5 sm:p-6 border border-slate-200/80 dark:border-slate-800 shadow-xs flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div className="flex items-center gap-4">
          <div className="h-14 w-14 rounded-2xl bg-emerald-600 dark:bg-emerald-500 text-white font-bold text-xl flex items-center justify-center shrink-0 shadow-xs">
            {currentUser?.avatarInitials || 'U'}
          </div>
          <div className="space-y-0.5">
            <div className="flex flex-wrap items-center gap-2">
              <h2 className="text-base sm:text-lg font-bold text-slate-900 dark:text-white uppercase tracking-tight">
                {currentUser?.name || 'SpendWise User'}
              </h2>
              {currentUser?.role === 'SystemAdmin' && (
                <span className="text-[10px] font-bold px-2 py-0.5 rounded-md bg-amber-100 dark:bg-amber-950 text-amber-800 dark:text-amber-300 border border-amber-200 dark:border-amber-800">
                  ADMIN
                </span>
              )}
            </div>
            <p className="text-xs text-slate-500 dark:text-slate-400 font-mono">
              {[currentUser?.email, currentUser?.phone].filter(Boolean).join(' • ') ||
                (locale === 'vi-VN' ? 'Chưa cập nhật thông tin' : 'No contact info')}
            </p>
          </div>
        </div>

        <div className="flex items-center gap-3 self-start sm:self-auto pt-2 sm:pt-0 border-t sm:border-t-0 border-slate-100 dark:border-slate-800">
          <div className="text-left sm:text-right">
            <span className="text-[10px] uppercase font-bold tracking-wider text-slate-400 block font-sans">
              {locale === 'vi-VN' ? 'Mã ví chính' : 'Primary Wallet'}
            </span>
            <span className="text-xs font-bold font-mono text-slate-800 dark:text-slate-200">
              {(() => {
                const defaultW = wallets.find((w) => w.isDefault) || wallets[0];
                if (defaultW?.accountNumber?.includes('****')) {
                  return `SW-${defaultW.accountNumber.replace('**** ', '')}-VN`;
                }
                if (defaultW?.id) {
                  return `SW-${defaultW.id.replace('w-', '').toUpperCase()}-VN`;
                }
                return locale === 'vi-VN' ? 'Chưa tạo ví' : 'No wallet created';
              })()}
            </span>
          </div>
        </div>
      </div>

      {/* 2. Compact Filter Navigation Bar */}
      <div className="flex items-center gap-1.5 p-1 bg-slate-100/80 dark:bg-slate-900 rounded-xl border border-slate-200/60 dark:border-slate-800 w-fit max-w-full overflow-x-auto text-xs font-semibold no-scrollbar">
        {[
          { id: 'all', label: locale === 'vi-VN' ? 'Tất cả cài đặt' : 'All' },
          { id: 'preferences', label: locale === 'vi-VN' ? 'Giao diện' : 'Appearance' },
          { id: 'notifications', label: locale === 'vi-VN' ? 'Thông báo' : 'Notifications' },
          { id: 'support', label: locale === 'vi-VN' ? 'Hỗ trợ' : 'Support' },
          { id: 'account', label: locale === 'vi-VN' ? 'Tài khoản' : 'Account' },
          { id: 'security', label: locale === 'vi-VN' ? 'Bảo mật' : 'Security' },
        ].map((tab) => {
          const isSelected = activeTab === tab.id;
          return (
            <button
              key={tab.id}
              type="button"
              onClick={() => handleTabChange(tab.id)}
              className={cn(
                'px-3.5 py-1.5 rounded-lg transition-all whitespace-nowrap cursor-pointer',
                isSelected
                  ? 'bg-white dark:bg-slate-800 text-slate-900 dark:text-white shadow-xs font-bold'
                  : 'text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white'
              )}
            >
              {tab.label}
            </button>
          );
        })}
      </div>

      {/* 3. Settings Sections Container (Single Central Column, Grouped Cards with Clean Row Dividers) */}
      <div className="space-y-6">

        {/* SECTION: Appearance & Experience */}
        {(activeTab === 'all' || activeTab === 'preferences') && (
          <div className="bg-white dark:bg-slate-900 rounded-2xl border border-slate-200/80 dark:border-slate-800 shadow-xs overflow-hidden">
            <div className="px-5 py-4 border-b border-slate-100 dark:border-slate-800 bg-slate-50/50 dark:bg-slate-900/50">
              <h3 className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400 flex items-center gap-2">
                <Palette className="h-4 w-4 text-slate-400" />
                <span>{locale === 'vi-VN' ? 'Giao diện & Tùy chọn' : 'Appearance & Preferences'}</span>
              </h3>
            </div>

            <div className="divide-y divide-slate-100 dark:divide-slate-800">
              {/* Language Row */}
              <div className="p-4 sm:p-5 flex flex-col sm:flex-row sm:items-center justify-between gap-4">
                <div className="space-y-0.5">
                  <span className="text-xs sm:text-sm font-bold text-slate-900 dark:text-white">
                    {t('settings.languageLabel')}
                  </span>
                  <p className="text-xs text-slate-400">
                    {locale === 'vi-VN' ? 'Chọn ngôn ngữ giao diện của hệ thống.' : 'Select system display language.'}
                  </p>
                </div>
                <div className="flex items-center gap-2 self-start sm:self-auto">
                  <button
                    type="button"
                    onClick={() => {
                      setLocale('vi-VN');
                      showToast('Đã đổi ngôn ngữ sang Tiếng Việt');
                    }}
                    className={cn(
                      'px-3.5 py-1.5 rounded-lg text-xs font-semibold border transition-all cursor-pointer',
                      locale === 'vi-VN'
                        ? 'border-emerald-500 bg-emerald-50/60 dark:bg-emerald-950/40 text-emerald-700 dark:text-emerald-300 ring-1 ring-emerald-500'
                        : 'border-slate-200 dark:border-slate-800 text-slate-600 dark:text-slate-400 hover:bg-slate-50 dark:hover:bg-slate-800'
                    )}
                  >
                    🇻🇳 Tiếng Việt
                  </button>
                  <button
                    type="button"
                    onClick={() => {
                      setLocale('en-US');
                      showToast('Switched language to English');
                    }}
                    className={cn(
                      'px-3.5 py-1.5 rounded-lg text-xs font-semibold border transition-all cursor-pointer',
                      locale === 'en-US'
                        ? 'border-emerald-500 bg-emerald-50/60 dark:bg-emerald-950/40 text-emerald-700 dark:text-emerald-300 ring-1 ring-emerald-500'
                        : 'border-slate-200 dark:border-slate-800 text-slate-600 dark:text-slate-400 hover:bg-slate-50 dark:hover:bg-slate-800'
                    )}
                  >
                    🇺🇸 English
                  </button>
                </div>
              </div>

              {/* Theme Row */}
              <div className="p-4 sm:p-5 flex flex-col sm:flex-row sm:items-center justify-between gap-4">
                <div className="space-y-0.5">
                  <span className="text-xs sm:text-sm font-bold text-slate-900 dark:text-white">
                    {t('settings.themeLabel')}
                  </span>
                  <p className="text-xs text-slate-400">
                    {locale === 'vi-VN' ? 'Tùy chỉnh giao diện Sáng, Tối hoặc theo Hệ thống.' : 'Choose light, dark, or system default.'}
                  </p>
                </div>
                <div className="flex items-center gap-1.5 self-start sm:self-auto p-1 bg-slate-100 dark:bg-slate-800/60 rounded-xl border border-slate-200/60 dark:border-slate-700/60">
                  {[
                    { id: 'light', label: locale === 'vi-VN' ? 'Sáng' : 'Light', icon: Sun },
                    { id: 'dark', label: locale === 'vi-VN' ? 'Tối' : 'Dark', icon: Moon },
                    { id: 'system', label: locale === 'vi-VN' ? 'Hệ thống' : 'System', icon: Laptop },
                  ].map((mode) => {
                    const Icon = mode.icon;
                    const isCurrent = theme === mode.id;
                    return (
                      <button
                        key={mode.id}
                        type="button"
                        onClick={() => setTheme(mode.id as any)}
                        className={cn(
                          'px-3 py-1.5 rounded-lg text-xs font-semibold transition-all flex items-center gap-1.5 cursor-pointer',
                          isCurrent
                            ? 'bg-white dark:bg-slate-900 text-emerald-600 dark:text-emerald-400 shadow-xs font-bold'
                            : 'text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white'
                        )}
                      >
                        <Icon className="h-3.5 w-3.5 shrink-0" />
                        <span>{mode.label}</span>
                      </button>
                    );
                  })}
                </div>
              </div>

              {/* Balance Visibility Row */}
              <div className="p-4 sm:p-5 flex flex-col sm:flex-row sm:items-center justify-between gap-4">
                <div className="space-y-0.5">
                  <span className="text-xs sm:text-sm font-bold text-slate-900 dark:text-white">
                    {t('settings.hideBalanceByDefault')}
                  </span>
                  <p className="text-xs text-slate-400">
                    {t('settings.hideBalanceDesc')}
                  </p>
                </div>
                <button
                  type="button"
                  onClick={toggleVisibility}
                  className={cn(
                    'p-2 rounded-xl border transition-colors cursor-pointer self-start sm:self-auto',
                    isHidden
                      ? 'bg-slate-900 text-white border-slate-900 dark:bg-slate-800'
                      : 'bg-slate-100 text-slate-700 border-slate-200 dark:bg-slate-800 dark:text-slate-300'
                  )}
                  aria-label={isHidden ? 'Show balance' : 'Hide balance'}
                >
                  {isHidden ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
                </button>
              </div>
            </div>
          </div>
        )}

        {/* SECTION: Notifications */}
        {(activeTab === 'all' || activeTab === 'notifications') && (
          <div className="bg-white dark:bg-slate-900 rounded-2xl border border-slate-200/80 dark:border-slate-800 shadow-xs overflow-hidden">
            <div className="px-5 py-4 border-b border-slate-100 dark:border-slate-800 bg-slate-50/50 dark:bg-slate-900/50 flex items-center justify-between">
              <h3 className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400 flex items-center gap-2">
                <Bell className="h-4 w-4 text-slate-400" />
                <span>{t('settings.notificationSettings')}</span>
              </h3>
              <span className="text-[11px] text-emerald-600 dark:text-emerald-400 font-semibold">
                {locale === 'vi-VN' ? 'Đồng bộ hệ thống' : 'Synced'}
              </span>
            </div>

            <div className="divide-y divide-slate-100 dark:divide-slate-800">
              <label className="p-4 sm:p-5 flex items-center justify-between gap-4 cursor-pointer hover:bg-slate-50/50 dark:hover:bg-slate-800/30 transition-colors">
                <div className="space-y-0.5 pr-4">
                  <span className="text-xs sm:text-sm font-semibold text-slate-900 dark:text-white block">
                    {t('settings.notifyBalance')}
                  </span>
                  <span className="text-xs text-slate-400 block leading-relaxed">
                    {t('settings.notifyBalanceDesc')}
                  </span>
                </div>
                <input
                  type="checkbox"
                  checked={mainPrefs.notifyBalanceChanges}
                  onChange={(e) => handleTogglePref('notifyBalanceChanges', e.target.checked)}
                  className="h-4 w-4 rounded text-emerald-600 focus:ring-emerald-500 shrink-0 cursor-pointer"
                />
              </label>

              <label className="p-4 sm:p-5 flex items-center justify-between gap-4 cursor-pointer hover:bg-slate-50/50 dark:hover:bg-slate-800/30 transition-colors">
                <div className="space-y-0.5 pr-4">
                  <span className="text-xs sm:text-sm font-semibold text-slate-900 dark:text-white block">
                    {t('settings.notifyBudget')}
                  </span>
                  <span className="text-xs text-slate-400 block leading-relaxed">
                    {t('settings.notifyBudgetDesc')}
                  </span>
                </div>
                <input
                  type="checkbox"
                  checked={mainPrefs.notifyBudgetAlerts}
                  onChange={(e) => handleTogglePref('notifyBudgetAlerts', e.target.checked)}
                  className="h-4 w-4 rounded text-emerald-600 focus:ring-emerald-500 shrink-0 cursor-pointer"
                />
              </label>

              <label className="p-4 sm:p-5 flex items-center justify-between gap-4 cursor-pointer hover:bg-slate-50/50 dark:hover:bg-slate-800/30 transition-colors">
                <div className="space-y-0.5 pr-4">
                  <span className="text-xs sm:text-sm font-semibold text-slate-900 dark:text-white block">
                    {locale === 'vi-VN' ? 'Cảnh báo giao dịch lớn' : 'Large Transaction Alerts'}
                  </span>
                  <span className="text-xs text-slate-400 block leading-relaxed">
                    {locale === 'vi-VN' ? 'Thông báo khi phát sinh giao dịch trên 1.000.000đ' : 'Notify when transactions exceed threshold'}
                  </span>
                </div>
                <input
                  type="checkbox"
                  checked={mainPrefs.notifyLargeTransactions}
                  onChange={(e) => handleTogglePref('notifyLargeTransactions', e.target.checked)}
                  className="h-4 w-4 rounded text-emerald-600 focus:ring-emerald-500 shrink-0 cursor-pointer"
                />
              </label>

              <label className="p-4 sm:p-5 flex items-center justify-between gap-4 cursor-pointer hover:bg-slate-50/50 dark:hover:bg-slate-800/30 transition-colors">
                <div className="space-y-0.5 pr-4">
                  <span className="text-xs sm:text-sm font-semibold text-slate-900 dark:text-white block">
                    {locale === 'vi-VN' ? 'Cảnh báo số dư tối thiểu' : 'Minimum Balance Alert'}
                  </span>
                  <span className="text-xs text-slate-400 block leading-relaxed">
                    {locale === 'vi-VN' ? 'Nhắc nhở khi số dư ví xuống dưới mức an toàn' : 'Remind when wallet balance falls below safe threshold'}
                  </span>
                </div>
                <input
                  type="checkbox"
                  checked={mainPrefs.notifyMinBalanceAlert}
                  onChange={(e) => handleTogglePref('notifyMinBalanceAlert', e.target.checked)}
                  className="h-4 w-4 rounded text-emerald-600 focus:ring-emerald-500 shrink-0 cursor-pointer"
                />
              </label>

              {/* Engagement Email Opt-In (backend-authoritative) */}
              <label className="p-4 sm:p-5 flex items-center justify-between gap-4 cursor-pointer hover:bg-slate-50/50 dark:hover:bg-slate-800/30 transition-colors">
                <div className="space-y-0.5 pr-4">
                  <span className="text-xs sm:text-sm font-semibold text-slate-900 dark:text-white block">
                    {locale === 'vi-VN' ? 'Email cập nhật sản phẩm' : 'Product update emails'}
                  </span>
                  <span className="text-xs text-slate-400 block leading-relaxed">
                    {locale === 'vi-VN'
                      ? 'Nhận thông báo về tính năng mới và cập nhật từ SpendWise.'
                      : 'Receive news about new features and updates from SpendWise.'}
                  </span>
                </div>
                <input
                  type="checkbox"
                  disabled={isUpdatingEngagementOptIn}
                  checked={currentUser?.engagementEmailOptIn ?? false}
                  onChange={async (e) => {
                    if (isUpdatingEngagementOptIn) return;
                    const targetVal = e.target.checked;
                    setIsUpdatingEngagementOptIn(true);
                    try {
                      const updated = await authApiService.updatePreferences({ engagementEmailOptIn: targetVal });
                      if (typeof updated.engagementEmailOptIn === 'boolean') {
                        updateCurrentUser({ engagementEmailOptIn: updated.engagementEmailOptIn });
                        showToast(locale === 'vi-VN' ? 'Đã cập nhật tùy chọn email' : 'Email preference updated');
                      } else {
                        throw new Error('CONTRACT_MISMATCH');
                      }
                    } catch {
                      showToast(locale === 'vi-VN' ? 'Không thể cập nhật tùy chọn.' : 'Failed to update preference.');
                    } finally {
                      setIsUpdatingEngagementOptIn(false);
                    }
                  }}
                  className="h-4 w-4 rounded text-emerald-600 focus:ring-emerald-500 shrink-0 disabled:opacity-50 cursor-pointer"
                />
              </label>
            </div>
          </div>
        )}

        {/* SECTION: Help & Support */}
        {(activeTab === 'all' || activeTab === 'support') && (
          <div className="bg-white dark:bg-slate-900 rounded-2xl border border-slate-200/80 dark:border-slate-800 shadow-xs overflow-hidden">
            <div className="px-5 py-4 border-b border-slate-100 dark:border-slate-800 bg-slate-50/50 dark:bg-slate-900/50">
              <h3 className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400 flex items-center gap-2">
                <LifeBuoy className="h-4 w-4 text-slate-400" />
                <span>{locale === 'vi-VN' ? 'Trợ giúp & Khiếu nại' : 'Help & Disputes'}</span>
              </h3>
            </div>

            <div className="divide-y divide-slate-100 dark:divide-slate-800">
              {/* Support Requests */}
              <div
                onClick={() => navigate('/support')}
                className="p-4 sm:p-5 flex items-center justify-between gap-4 hover:bg-slate-50/60 dark:hover:bg-slate-800/40 transition-colors cursor-pointer group"
              >
                <div className="space-y-0.5 pr-4">
                  <div className="flex items-center gap-1.5">
                    <MessageSquare className="h-4 w-4 text-slate-400 group-hover:text-emerald-500 transition-colors shrink-0" />
                    <span className="text-xs sm:text-sm font-semibold text-slate-900 dark:text-white group-hover:text-emerald-600 dark:group-hover:text-emerald-400 transition-colors">
                      {locale === 'vi-VN' ? 'Gửi yêu cầu Hỗ trợ' : 'Support Requests'}
                    </span>
                  </div>
                  <p className="text-xs text-slate-400">
                    {locale === 'vi-VN' ? 'Gửi và theo dõi ticket hỗ trợ kỹ thuật, tài khoản.' : 'Submit and track technical & account tickets.'}
                  </p>
                </div>
                <ArrowRight className="h-4 w-4 text-slate-400 group-hover:text-emerald-500 group-hover:translate-x-0.5 transition-all shrink-0" />
              </div>

              {/* Transaction Disputes */}
              <div
                onClick={() => navigate('/disputes')}
                className="p-4 sm:p-5 flex items-center justify-between gap-4 hover:bg-slate-50/60 dark:hover:bg-slate-800/40 transition-colors cursor-pointer group"
              >
                <div className="space-y-0.5 pr-4">
                  <div className="flex items-center gap-1.5">
                    <ShieldAlert className="h-4 w-4 text-rose-500 shrink-0" />
                    <span className="text-xs sm:text-sm font-semibold text-slate-900 dark:text-white group-hover:text-rose-600 dark:group-hover:text-rose-400 transition-colors">
                      {locale === 'vi-VN' ? 'Khiếu nại & Tra soát giao dịch' : 'Transaction Disputes'}
                    </span>
                  </div>
                  <p className="text-xs text-slate-400">
                    {locale === 'vi-VN' ? 'Tra soát giao dịch nghi vấn, sai lệch hoặc trùng lặp.' : 'Report unrecognized charges, discrepancies or duplicates.'}
                  </p>
                </div>
                <ArrowRight className="h-4 w-4 text-slate-400 group-hover:text-rose-500 group-hover:translate-x-0.5 transition-all shrink-0" />
              </div>
            </div>
          </div>
        )}

        {/* SECTION: Account & Profile Details */}
        {(activeTab === 'all' || activeTab === 'account') && (
          <div className="bg-white dark:bg-slate-900 rounded-2xl border border-slate-200/80 dark:border-slate-800 shadow-xs overflow-hidden">
            <div className="px-5 py-4 border-b border-slate-100 dark:border-slate-800 bg-slate-50/50 dark:bg-slate-900/50">
              <h3 className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400 flex items-center gap-2">
                <User className="h-4 w-4 text-slate-400" />
                <span>{locale === 'vi-VN' ? 'Thông tin tài khoản' : 'Account Details'}</span>
              </h3>
            </div>

            <div className="divide-y divide-slate-100 dark:divide-slate-800">
              {/* Email Row */}
              <div className="p-4 sm:p-5 flex flex-col sm:flex-row sm:items-center justify-between gap-2">
                <div>
                  <span className="text-xs text-slate-400 block">{locale === 'vi-VN' ? 'Email đăng nhập' : 'Login Email'}</span>
                  <span className="text-xs sm:text-sm font-semibold text-slate-900 dark:text-white font-mono">
                    {currentUser?.email || (locale === 'vi-VN' ? 'Chưa thiết lập' : 'Not set')}
                  </span>
                </div>
                <span className="text-[11px] font-semibold text-emerald-600 dark:text-emerald-400 self-start sm:self-auto flex items-center gap-1">
                  <Check className="h-3 w-3" />
                  <span>{locale === 'vi-VN' ? 'Đã xác thực' : 'Verified'}</span>
                </span>
              </div>

              {/* Phone Row */}
              <div className="p-4 sm:p-5 flex flex-col sm:flex-row sm:items-center justify-between gap-2">
                <div>
                  <span className="text-xs text-slate-400 block">{locale === 'vi-VN' ? 'Số điện thoại' : 'Phone Number'}</span>
                  <span className="text-xs sm:text-sm font-semibold text-slate-900 dark:text-white font-mono">
                    {currentUser?.phone || (locale === 'vi-VN' ? 'Chưa thiết lập' : 'Not set')}
                  </span>
                </div>
              </div>

              {/* Wallet ID Row */}
              <div className="p-4 sm:p-5 flex flex-col sm:flex-row sm:items-center justify-between gap-2">
                <div>
                  <span className="text-xs text-slate-400 block">{locale === 'vi-VN' ? 'Mã định danh ví' : 'Wallet Identifier'}</span>
                  <span className="text-xs sm:text-sm font-semibold font-mono text-slate-900 dark:text-white">
                    {(() => {
                      const defaultW = wallets.find((w) => w.isDefault) || wallets[0];
                      if (defaultW?.accountNumber?.includes('****')) {
                        return `SW-${defaultW.accountNumber.replace('**** ', '')}-VN`;
                      }
                      if (defaultW?.id) {
                        return `SW-${defaultW.id.replace('w-', '').toUpperCase()}-VN`;
                      }
                      return locale === 'vi-VN' ? 'Chưa tạo ví' : 'No wallet created';
                    })()}
                  </span>
                </div>
                <span className="text-[11px] text-slate-400 self-start sm:self-auto">
                  {locale === 'vi-VN' ? 'Ví hoạt động' : 'Active Wallet'}
                </span>
              </div>
            </div>
          </div>
        )}

        {/* SECTION: Security & Authentication (FINAL SECTION) */}
        {(activeTab === 'all' || activeTab === 'security') && (
          <div className="bg-white dark:bg-slate-900 rounded-2xl border border-slate-200/80 dark:border-slate-800 shadow-xs overflow-hidden">
            <div className="px-5 py-4 border-b border-slate-100 dark:border-slate-800 bg-slate-50/50 dark:bg-slate-900/50">
              <h3 className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400 flex items-center gap-2">
                <Shield className="h-4 w-4 text-slate-400" />
                <span>{locale === 'vi-VN' ? 'Tài khoản & Bảo mật' : 'Account & Security'}</span>
              </h3>
            </div>

            <div className="divide-y divide-slate-100 dark:divide-slate-800">
              {/* Row 1: Security PIN */}
              <div className="p-4 sm:p-5 flex flex-col sm:flex-row sm:items-center justify-between gap-4">
                <div className="space-y-0.5">
                  <div className="flex items-center gap-2">
                    <span className="text-xs sm:text-sm font-bold text-slate-900 dark:text-white">
                      {locale === 'vi-VN' ? 'Mã bảo mật 6 số' : '6-digit Security PIN'}
                    </span>
                    <span
                      className={cn(
                        'text-[10px] font-bold px-2 py-0.5 rounded-full uppercase tracking-wider',
                        hasPin === true
                          ? 'bg-emerald-100 dark:bg-emerald-950/60 text-emerald-700 dark:text-emerald-400 border border-emerald-200 dark:border-emerald-800'
                          : hasPin === false
                          ? 'bg-amber-100 dark:bg-amber-950/60 text-amber-700 dark:text-amber-400 border border-amber-200 dark:border-amber-800'
                          : 'bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-400 border border-slate-200 dark:border-slate-700'
                      )}
                    >
                      {hasPin === true
                        ? locale === 'vi-VN'
                          ? 'Đã thiết lập'
                          : 'Set'
                        : hasPin === false
                        ? locale === 'vi-VN'
                          ? 'Chưa thiết lập'
                          : 'Not set'
                        : locale === 'vi-VN'
                        ? 'Chưa xác định'
                        : 'Unknown'}
                    </span>
                  </div>
                  <p className="text-xs text-slate-400 max-w-xl">
                    {locale === 'vi-VN'
                      ? 'Dùng để xác thực chuyển tiền, mở quỹ chung và điều chỉnh hạn mức.'
                      : 'Used to authorize transfers, shared funds and limit changes.'}
                  </p>
                </div>
                <div className="flex items-center gap-3 self-start sm:self-auto shrink-0">
                  {hasPin === true && (
                    <button
                      type="button"
                      id="btn-settings-forgot-pin"
                      onClick={handleForgotPinFromSettings}
                      className="text-xs font-semibold text-slate-500 hover:text-emerald-600 dark:text-slate-400 dark:hover:text-emerald-400 hover:underline cursor-pointer"
                    >
                      {locale === 'vi-VN' ? 'Quên mã PIN?' : 'Forgot PIN?'}
                    </button>
                  )}
                  <Button
                    size="sm"
                    id="btn-settings-change-pin"
                    variant="outline"
                    onClick={() => {
                      setOldPin('');
                      setNewPin('');
                      setConfirmNewPin('');
                      setChangePinError(null);
                      setPinModalStage(hasPin === true ? 'old_pin' : 'new_pin');
                      setIsChangePinOpen(true);
                    }}
                    className="text-xs font-semibold text-emerald-600 dark:text-emerald-400 hover:text-emerald-700 border-slate-200 dark:border-slate-700 cursor-pointer"
                  >
                    {hasPin === true
                      ? locale === 'vi-VN'
                        ? 'Đổi mã bảo mật'
                        : 'Change Security PIN'
                      : locale === 'vi-VN'
                      ? 'Thiết lập mã bảo mật'
                      : 'Set up Security PIN'}
                  </Button>
                </div>
              </div>

              {/* Row 2: Password */}
              <div className="p-4 sm:p-5 flex flex-col sm:flex-row sm:items-center justify-between gap-4">
                <div className="space-y-0.5">
                  <span className="text-xs sm:text-sm font-bold text-slate-900 dark:text-white">
                    {t('security.changePassword')}
                  </span>
                  <p className="text-xs text-slate-400 max-w-xl">
                    {t('security.changePasswordDesc')}
                  </p>
                </div>
                <Button
                  size="sm"
                  variant="outline"
                  onClick={() => setIsChangePasswordOpen(true)}
                  className="text-xs font-semibold text-slate-700 dark:text-slate-300 border-slate-200 dark:border-slate-700 self-start sm:self-auto shrink-0 cursor-pointer"
                >
                  {t('security.changePassword')}
                </Button>
              </div>

              {/* Row 3: Revoke All Sessions */}
              <div className="p-4 sm:p-5 flex flex-col sm:flex-row sm:items-center justify-between gap-4">
                <div className="space-y-0.5">
                  <span className="text-xs sm:text-sm font-bold text-slate-800 dark:text-slate-200">
                    {locale === 'vi-VN' ? 'Đăng xuất tất cả thiết bị' : 'Sign out all sessions'}
                  </span>
                  <p className="text-xs text-slate-400 max-w-xl">
                    {locale === 'vi-VN'
                      ? 'Thu hồi token và đăng xuất tài khoản khỏi tất cả các thiết bị và phiên làm việc khác.'
                      : 'Revoke tokens and sign out your account from all other active devices and sessions.'}
                  </p>
                </div>
                <Button
                  size="sm"
                  variant="outline"
                  onClick={() => setIsSignOutAllOpen(true)}
                  className="text-xs font-semibold text-slate-700 dark:text-slate-300 border-slate-200 dark:border-slate-700 hover:bg-slate-50 dark:hover:bg-slate-800 self-start sm:self-auto shrink-0 cursor-pointer"
                >
                  {locale === 'vi-VN' ? 'Đăng xuất tất cả' : 'Sign out all'}
                </Button>
              </div>

              {/* Row 4: Logout Current Session (FINAL ACTION AT THE VERY END) */}
              <div className="p-4 sm:p-5 flex flex-col sm:flex-row sm:items-center justify-between gap-4 bg-slate-50/30 dark:bg-slate-900/40">
                <div className="space-y-0.5">
                  <span className="text-xs sm:text-sm font-bold text-rose-600 dark:text-rose-400 flex items-center gap-1.5">
                    <LogOut className="h-4 w-4" />
                    <span>{locale === 'vi-VN' ? 'Đăng xuất tài khoản' : 'Sign Out'}</span>
                  </span>
                  <p className="text-xs text-slate-400 max-w-xl">
                    {locale === 'vi-VN'
                      ? 'Kết thúc phiên làm việc hiện tại trên thiết bị này.'
                      : 'End current session on this device.'}
                  </p>
                </div>
                <Button
                  size="sm"
                  variant="outline"
                  id="btn-settings-logout"
                  onClick={async () => {
                    await logout();
                    navigate('/login');
                  }}
                  className="text-xs font-bold text-rose-600 dark:text-rose-400 border-rose-200 dark:border-rose-900/40 hover:bg-rose-50 dark:hover:bg-rose-950/30 self-start sm:self-auto shrink-0 cursor-pointer"
                >
                  <LogOut className="h-3.5 w-3.5 mr-1" />
                  <span>{locale === 'vi-VN' ? 'Đăng xuất' : 'Sign Out'}</span>
                </Button>
              </div>
            </div>
          </div>
        )}

      </div>

      {/* 4. Minimal System Footer Note */}
      <div className="pt-4 text-center text-xs text-slate-400 dark:text-slate-500">
        {APP_META.name} • v{APP_META.version}
      </div>

      {/* 1. CHANGE PASSWORD MODAL */}
      {isChangePasswordOpen && (
        <div
          role="dialog"
          aria-modal="true"
          className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/60 backdrop-blur-xs animate-in fade-in duration-150"
        >
          <form
            onSubmit={handleChangePasswordSubmit}
            onKeyDown={(e) => {
              if (e.key === 'Enter' && (e.target as HTMLElement).tagName !== 'TEXTAREA') {
                e.preventDefault();
                handleChangePasswordSubmit(e);
              }
            }}
            className="w-full max-w-md bg-white dark:bg-slate-900 rounded-3xl p-6 sm:p-7 border border-slate-100 dark:border-slate-800 shadow-2xl space-y-5 animate-in zoom-in-95"
          >
            <div className="flex items-center justify-between">
              <div className="flex items-center gap-2">
                <KeyRound className="h-5 w-5 text-emerald-500" />
                <h3 className="text-base font-bold text-slate-900 dark:text-white">
                  {t('security.changePassword')}
                </h3>
              </div>
              <button
                type="button"
                aria-label={locale === 'vi-VN' ? 'Đóng' : 'Close'}
                disabled={isSubmittingPassword}
                onClick={() => {
                  if (!isSubmittingPassword) {
                    setIsChangePasswordOpen(false);
                    setPasswordError(null);
                  }
                }}
                className="p-1.5 rounded-xl text-slate-400 hover:bg-slate-100 dark:hover:bg-slate-800 cursor-pointer disabled:opacity-50"
              >
                <X className="h-4 w-4" />
              </button>
            </div>

            {passwordError && (
              <div className="p-3 rounded-2xl bg-rose-50 dark:bg-rose-950/30 text-rose-700 dark:text-rose-300 text-xs font-semibold flex items-center gap-2">
                <AlertCircle className="h-4 w-4 shrink-0" />
                <span>{passwordError}</span>
              </div>
            )}

            <div className="space-y-4 text-xs">
              <div className="space-y-1.5">
                <label className="font-bold text-slate-700 dark:text-slate-300">
                  {t('security.currentPassword')} *
                </label>
                <input
                  type="password"
                  placeholder="••••••••"
                  value={currentPassword}
                  onChange={(e) => setCurrentPassword(e.target.value)}
                  required
                  disabled={isSubmittingPassword}
                  className="w-full px-3.5 py-2.5 rounded-2xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-slate-900 dark:text-white font-medium disabled:opacity-50"
                />
              </div>

              <div className="space-y-1.5">
                <label className="font-bold text-slate-700 dark:text-slate-300">
                  {t('security.newPassword')} *
                </label>
                <input
                  type="password"
                  placeholder="••••••••"
                  value={newPassword}
                  onChange={(e) => setNewPassword(e.target.value)}
                  required
                  minLength={8}
                  disabled={isSubmittingPassword}
                  className="w-full px-3.5 py-2.5 rounded-2xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-slate-900 dark:text-white font-medium disabled:opacity-50"
                />
                
                {/* Password Strength / Policy Indicator */}
                <div className="pt-1 grid grid-cols-2 gap-1.5 text-[10px]">
                  <div className={cn('flex items-center gap-1', hasMinLength ? 'text-emerald-600 dark:text-emerald-400 font-semibold' : 'text-slate-400')}>
                    <div className={cn('h-1.5 w-1.5 rounded-full', hasMinLength ? 'bg-emerald-500' : 'bg-slate-300 dark:bg-slate-700')} />
                    <span>{locale === 'vi-VN' ? 'Ít nhất 8 ký tự' : 'At least 8 characters'}</span>
                  </div>
                  <div className={cn('flex items-center gap-1', hasUppercase ? 'text-emerald-600 dark:text-emerald-400 font-semibold' : 'text-slate-400')}>
                    <div className={cn('h-1.5 w-1.5 rounded-full', hasUppercase ? 'bg-emerald-500' : 'bg-slate-300 dark:bg-slate-700')} />
                    <span>{locale === 'vi-VN' ? '1 chữ in hoa (A-Z)' : '1 uppercase (A-Z)'}</span>
                  </div>
                  <div className={cn('flex items-center gap-1', hasLowercase ? 'text-emerald-600 dark:text-emerald-400 font-semibold' : 'text-slate-400')}>
                    <div className={cn('h-1.5 w-1.5 rounded-full', hasLowercase ? 'bg-emerald-500' : 'bg-slate-300 dark:bg-slate-700')} />
                    <span>{locale === 'vi-VN' ? '1 chữ thường (a-z)' : '1 lowercase (a-z)'}</span>
                  </div>
                  <div className={cn('flex items-center gap-1', hasDigit ? 'text-emerald-600 dark:text-emerald-400 font-semibold' : 'text-slate-400')}>
                    <div className={cn('h-1.5 w-1.5 rounded-full', hasDigit ? 'bg-emerald-500' : 'bg-slate-300 dark:bg-slate-700')} />
                    <span>{locale === 'vi-VN' ? '1 chữ số (0-9)' : '1 digit (0-9)'}</span>
                  </div>
                </div>
              </div>

              <div className="space-y-1.5">
                <label className="font-bold text-slate-700 dark:text-slate-300">
                  {t('security.confirmNewPassword')} *
                </label>
                <input
                  type="password"
                  placeholder="••••••••"
                  value={confirmPassword}
                  onChange={(e) => setConfirmPassword(e.target.value)}
                  required
                  disabled={isSubmittingPassword}
                  className="w-full px-3.5 py-2.5 rounded-2xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-slate-900 dark:text-white font-medium disabled:opacity-50"
                />
              </div>
            </div>

            <div className="flex items-center justify-end gap-3 pt-3 border-t border-slate-100 dark:border-slate-800">
              <Button
                type="button"
                variant="outline"
                disabled={isSubmittingPassword}
                onClick={() => {
                  setIsChangePasswordOpen(false);
                  setPasswordError(null);
                }}
                className="cursor-pointer"
              >
                {t('common.cancel')}
              </Button>
              <Button
                type="submit"
                disabled={isSubmittingPassword}
                className="bg-emerald-600 hover:bg-emerald-700 text-white font-bold cursor-pointer flex items-center gap-2"
              >
                {isSubmittingPassword ? (
                  <>
                    <Loader2 className="h-4 w-4 animate-spin" />
                    <span>{locale === 'vi-VN' ? 'Đang lưu...' : 'Saving...'}</span>
                  </>
                ) : (
                  <span>{t('common.save')}</span>
                )}
              </Button>
            </div>
          </form>
        </div>
      )}

      {/* 2. SIGN OUT ALL SESSIONS CONFIRMATION MODAL */}
      {isSignOutAllOpen && (
        <div
          role="dialog"
          aria-modal="true"
          className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/60 backdrop-blur-xs animate-in fade-in duration-150"
        >
          <div className="w-full max-w-md bg-white dark:bg-slate-900 rounded-3xl p-6 sm:p-7 border border-slate-100 dark:border-slate-800 shadow-2xl space-y-5 animate-in zoom-in-95">
            <div className="flex items-center justify-between">
              <div className="flex items-center gap-2 text-rose-600 dark:text-rose-400">
                <ShieldAlert className="h-5 w-5" />
                <h3 className="text-base font-bold text-slate-900 dark:text-white">
                  {locale === 'vi-VN' ? 'Đăng xuất tất cả phiên' : 'Sign out all sessions'}
                </h3>
              </div>
              <button
                type="button"
                aria-label={locale === 'vi-VN' ? 'Đóng' : 'Close'}
                disabled={isSigningOutAll}
                onClick={() => setIsSignOutAllOpen(false)}
                className="p-1.5 rounded-xl text-slate-400 hover:bg-slate-100 dark:hover:bg-slate-800 cursor-pointer disabled:opacity-50"
              >
                <X className="h-4 w-4" />
              </button>
            </div>

            <div className="space-y-2 text-xs">
              <div className="p-3 rounded-2xl bg-amber-50 dark:bg-amber-950/30 border border-amber-200 dark:border-amber-900/40 text-[11px] text-amber-800 dark:text-amber-300 leading-relaxed">
                {locale === 'vi-VN'
                  ? 'Thao tác này sẽ thu hồi tất cả phiên đăng nhập và mã làm mới của bạn trên tất cả thiết bị khác. Bạn sẽ cần đăng nhập lại.'
                  : 'This action will revoke all your active sessions and refresh tokens across all devices. You will need to log in again.'}
              </div>
              <p className="text-slate-600 dark:text-slate-300 leading-relaxed">
                {locale === 'vi-VN'
                  ? 'Bạn có chắc chắn muốn đăng xuất khỏi tất cả thiết bị không?'
                  : 'Are you sure you want to sign out of all active sessions and devices?'}
              </p>
            </div>

            <div className="flex items-center justify-end gap-3 pt-3 border-t border-slate-100 dark:border-slate-800">
              <Button
                type="button"
                variant="outline"
                disabled={isSigningOutAll}
                onClick={() => setIsSignOutAllOpen(false)}
                className="cursor-pointer"
              >
                {t('common.cancel')}
              </Button>
              <Button
                type="button"
                disabled={isSigningOutAll}
                onClick={handleSignOutAllSessions}
                className="bg-rose-600 hover:bg-rose-700 text-white font-bold cursor-pointer flex items-center gap-2"
              >
                {isSigningOutAll ? (
                  <>
                    <Loader2 className="h-4 w-4 animate-spin" />
                    <span>{locale === 'vi-VN' ? 'Đang đăng xuất...' : 'Signing out...'}</span>
                  </>
                ) : (
                  <span>{locale === 'vi-VN' ? 'Xác nhận đăng xuất' : 'Confirm Sign Out'}</span>
                )}
              </Button>
            </div>
          </div>
        </div>
      )}

      {/* Change Security PIN Modal */}
      {isChangePinOpen && (
        <div
          id="modal-change-security-pin"
          className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/60 backdrop-blur-xs animate-in fade-in-0 duration-150 overflow-y-auto"
        >
          <div className="w-full max-w-md my-auto bg-white dark:bg-slate-900 rounded-3xl p-6 sm:p-7 border border-slate-200 dark:border-slate-800 shadow-2xl space-y-5 animate-in zoom-in-95 duration-150">
            {/* Header */}
            <div className="flex items-center justify-between pb-3 border-b border-slate-100 dark:border-slate-800">
              <div className="flex items-center gap-2.5">
                <div className="h-9 w-9 rounded-2xl bg-emerald-50 dark:bg-emerald-950/50 border border-emerald-200 dark:border-emerald-800 text-emerald-600 dark:text-emerald-400 flex items-center justify-center">
                  <ShieldAlert className="h-4 w-4" />
                </div>
                <div>
                  <h3 className="text-base font-bold text-slate-900 dark:text-white">
                    {hasPin === true
                      ? locale === 'vi-VN'
                        ? 'Đổi mã bảo mật 6 số'
                        : 'Change 6-digit Security PIN'
                      : locale === 'vi-VN'
                      ? 'Thiết lập mã bảo mật 6 số'
                      : 'Set up 6-digit Security PIN'}
                  </h3>
                  <p className="text-xs text-slate-400">
                    {locale === 'vi-VN'
                      ? 'Mã bảo mật gồm đúng 6 chữ số'
                      : 'Security PIN must be exactly 6 digits'}
                  </p>
                </div>
              </div>
              <button
                type="button"
                id="btn-close-change-pin"
                onClick={() => {
                  setIsChangePinOpen(false);
                  setChangePinError(null);
                }}
                className="p-1.5 rounded-xl text-slate-400 hover:text-slate-600 hover:bg-slate-100 dark:hover:bg-slate-800 transition-colors cursor-pointer"
              >
                <X className="h-5 w-5" />
              </button>
            </div>

            <div className="space-y-4">
              {changePinError && (
                <div className="p-3 rounded-2xl bg-rose-50 dark:bg-rose-950/40 border border-rose-200 dark:border-rose-900/60 text-xs text-rose-700 dark:text-rose-300 flex items-start gap-2">
                  <AlertCircle className="h-4 w-4 shrink-0 mt-0.5" />
                  <span>{changePinError}</span>
                </div>
              )}

              {/* Stage Stepper Indicator */}
              <div className="flex items-center justify-center gap-2 pb-1">
                {hasPin === true && (
                  <span
                    className={`h-1.5 rounded-full transition-all duration-200 ${
                      pinModalStage === 'old_pin' ? 'w-8 bg-emerald-500' : 'w-2 bg-emerald-500/40'
                    }`}
                  />
                )}
                <span
                  className={`h-1.5 rounded-full transition-all duration-200 ${
                    pinModalStage === 'new_pin' ? 'w-8 bg-emerald-500' : 'w-2 bg-slate-200 dark:bg-slate-700'
                  }`}
                />
                <span
                  className={`h-1.5 rounded-full transition-all duration-200 ${
                    pinModalStage === 'confirm_pin' ? 'w-8 bg-emerald-500' : 'w-2 bg-slate-200 dark:bg-slate-700'
                  }`}
                />
              </div>

              {/* Stage 1: Old PIN verification if enrolled */}
              {pinModalStage === 'old_pin' && (
                <div className="space-y-3 py-1">
                  <div className="flex items-center justify-between px-1">
                    <label className="text-xs font-bold text-slate-700 dark:text-slate-300">
                      {locale === 'vi-VN' ? 'Mã bảo mật hiện tại' : 'Current Security PIN'}
                    </label>
                    <button
                      type="button"
                      onClick={() => {
                        setIsChangePinOpen(false);
                        handleForgotPinFromSettings();
                      }}
                      className="text-[11px] font-medium text-emerald-600 dark:text-emerald-400 hover:underline cursor-pointer"
                    >
                      {locale === 'vi-VN' ? 'Quên mã cũ?' : 'Forgot current PIN?'}
                    </button>
                  </div>
                  <SixDigitPinInput
                    id="input-change-pin-old"
                    value={oldPin}
                    onChange={(val) => {
                      setOldPin(val);
                      if (changePinError) setChangePinError(null);
                    }}
                    onComplete={handleOldPinComplete}
                    disabled={isSavingPin}
                    error={Boolean(changePinError)}
                    showToggle
                    mask={!showPin}
                    onToggleMask={() => setShowPin(!showPin)}
                    autoFocus
                  />
                  <p className="text-[11px] text-slate-400 text-center">
                    {isSavingPin ? (
                      <span className="inline-flex items-center gap-1.5 text-emerald-600 font-medium">
                        <Loader2 className="w-3.5 h-3.5 animate-spin" />
                        {locale === 'vi-VN' ? 'Đang xác thực...' : 'Verifying...'}
                      </span>
                    ) : (
                      <span>{locale === 'vi-VN' ? 'Tự động kiểm tra khi nhập đủ 6 số' : 'Verifies automatically on 6th digit'}</span>
                    )}
                  </p>
                </div>
              )}

              {/* Stage 2: Enter New PIN */}
              {pinModalStage === 'new_pin' && (
                <div className="space-y-3 py-1">
                  <label className="block text-xs font-bold text-slate-700 dark:text-slate-300 text-center">
                    {locale === 'vi-VN' ? 'Nhập mã bảo mật mới (6 số)' : 'Enter New Security PIN (6 digits)'}
                  </label>
                  <SixDigitPinInput
                    id="input-change-pin-new"
                    value={newPin}
                    onChange={(val) => {
                      setNewPin(val);
                      if (changePinError) setChangePinError(null);
                    }}
                    onComplete={handleNewPinComplete}
                    disabled={isSavingPin}
                    error={Boolean(changePinError)}
                    showToggle
                    mask={!showPin}
                    onToggleMask={() => setShowPin(!showPin)}
                    autoFocus
                  />
                  <p className="text-[11px] text-slate-400 text-center">
                    {locale === 'vi-VN' ? 'Tự động chuyển bước khi nhập đủ 6 số' : 'Advances automatically on 6th digit'}
                  </p>
                </div>
              )}

              {/* Stage 3: Confirm New PIN */}
              {pinModalStage === 'confirm_pin' && (
                <div className="space-y-3 py-1">
                  <label className="block text-xs font-bold text-slate-700 dark:text-slate-300 text-center">
                    {locale === 'vi-VN' ? 'Xác nhận mã bảo mật mới' : 'Confirm New Security PIN'}
                  </label>
                  <SixDigitPinInput
                    id="input-change-pin-confirm"
                    value={confirmNewPin}
                    onChange={(val) => {
                      setConfirmNewPin(val);
                      if (changePinError) setChangePinError(null);
                    }}
                    onComplete={handleConfirmPinComplete}
                    disabled={isSavingPin}
                    error={Boolean(changePinError)}
                    showToggle
                    mask={!showPin}
                    onToggleMask={() => setShowPin(!showPin)}
                    autoFocus
                  />
                  <p className="text-[11px] text-slate-400 text-center">
                    {isSavingPin ? (
                      <span className="inline-flex items-center gap-1.5 text-emerald-600 font-medium">
                        <Loader2 className="w-3.5 h-3.5 animate-spin" />
                        {locale === 'vi-VN' ? 'Đang lưu mã bảo mật...' : 'Saving Security PIN...'}
                      </span>
                    ) : (
                      <span>{locale === 'vi-VN' ? 'Tự động lưu khi nhập đủ 6 số xác nhận' : 'Saves automatically on 6th digit'}</span>
                    )}
                  </p>
                  <div className="flex justify-center pt-1">
                    <button
                      type="button"
                      onClick={() => {
                        setPinModalStage('new_pin');
                        setConfirmNewPin('');
                        setChangePinError(null);
                      }}
                      disabled={isSavingPin}
                      className="text-xs text-emerald-600 dark:text-emerald-400 hover:underline cursor-pointer font-medium"
                    >
                      {locale === 'vi-VN' ? 'Nhập lại mã mới' : 'Change new PIN'}
                    </button>
                  </div>
                </div>
              )}

              <div className="flex items-center justify-end gap-3 pt-3 border-t border-slate-100 dark:border-slate-800">
                <Button
                  type="button"
                  variant="outline"
                  disabled={isSavingPin}
                  onClick={() => {
                    setIsChangePinOpen(false);
                    setChangePinError(null);
                  }}
                  className="cursor-pointer"
                >
                  {t('common.cancel')}
                </Button>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
