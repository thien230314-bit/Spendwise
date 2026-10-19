import React, { useState } from 'react';
import {
  User,
  Shield,
  Palette,
  Bell,
  Eye,
  EyeOff,
  Sun,
  Moon,
  Laptop,
  Check,
  KeyRound,
  ShieldAlert,
  LogOut,
  LifeBuoy,
  MessageSquare,
  ArrowRight,
  ExternalLink,
  CheckCircle2,
  Lock,
} from 'lucide-react';
import { useI18n } from '../../i18n/I18nContext';
import { useTheme } from '../../providers/ThemeProvider';
import { useBalanceVisibility } from '../../providers/BalanceVisibilityProvider';
import { useAuth } from '../../providers/AuthProvider';
import { useNotifications } from '../../providers/NotificationProvider';
import { useFinancial } from '../../providers/FinancialProvider';
import { Button } from '../../components/ui/Button';
import { cn } from '../../utils/cn';
import { APP_META } from '../../config/appMeta';

export const SettingsRedesignPreview: React.FC = () => {
  const { t, locale, setLocale } = useI18n();
  const { theme, setTheme } = useTheme();
  const { isHidden, toggleVisibility } = useBalanceVisibility();
  const { currentUser } = useAuth();
  const { accountPreferences } = useNotifications();
  const { wallets } = useFinancial();

  // Active filter tab
  const [activeTab, setActiveTab] = useState<'all' | 'account' | 'security' | 'preferences' | 'notifications' | 'support'>('all');

  // Preview local notification states (Mirrors real notification settings)
  const [notifyBalance, setNotifyBalance] = useState(true);
  const [notifyBudget, setNotifyBudget] = useState(true);
  const [notifyLargeTx, setNotifyLargeTx] = useState(true);
  const [notifyMinBalance, setNotifyMinBalance] = useState(true);
  const [notifyEmailNews, setNotifyEmailNews] = useState(currentUser?.engagementEmailOptIn ?? false);

  // Local feedback toast
  const [previewToast, setPreviewToast] = useState<string | null>(null);
  const triggerToast = (msg: string) => {
    setPreviewToast(msg);
    setTimeout(() => setPreviewToast(null), 2500);
  };

  // Default wallet identifier logic (exact match with production SettingsPage)
  const defaultWallet = wallets.find((w) => w.isDefault) || wallets[0];
  const walletIdentifier = (() => {
    if (defaultWallet?.accountNumber?.includes('****')) {
      return `SW-${defaultWallet.accountNumber.replace('**** ', '')}-VN`;
    }
    if (defaultWallet?.id) {
      return `SW-${defaultWallet.id.replace('w-', '').toUpperCase()}-VN`;
    }
    return locale === 'vi-VN' ? 'Chưa tạo ví' : 'No wallet created';
  })();

  const filterTabs = [
    { id: 'all', label: locale === 'vi-VN' ? 'Tất cả cài đặt' : 'All' },
    { id: 'account', label: locale === 'vi-VN' ? 'Tài khoản' : 'Account' },
    { id: 'security', label: locale === 'vi-VN' ? 'Bảo mật' : 'Security' },
    { id: 'preferences', label: locale === 'vi-VN' ? 'Giao diện' : 'Appearance' },
    { id: 'notifications', label: locale === 'vi-VN' ? 'Thông báo' : 'Notifications' },
    { id: 'support', label: locale === 'vi-VN' ? 'Hỗ trợ' : 'Support' },
  ];

  return (
    <div className="max-w-4xl mx-auto space-y-6 pb-12 animate-in fade-in-50 duration-200">
      {/* Toast Notification */}
      {previewToast && (
        <div className="fixed top-6 right-6 z-50 flex items-center gap-2 px-4 py-2.5 rounded-xl bg-slate-900 text-white shadow-lg border border-emerald-500/30 text-xs font-medium animate-in slide-in-from-top-2">
          <CheckCircle2 className="h-4 w-4 text-emerald-400 shrink-0" />
          <span>{previewToast}</span>
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
              {walletIdentifier}
            </span>
          </div>
        </div>
      </div>

      {/* 2. Compact Filter Navigation Bar */}
      <div className="flex items-center gap-1.5 p-1 bg-slate-100/80 dark:bg-slate-900 rounded-xl border border-slate-200/60 dark:border-slate-800 w-fit max-w-full overflow-x-auto text-xs font-semibold no-scrollbar">
        {filterTabs.map((tab) => {
          const isSelected = activeTab === tab.id;
          return (
            <button
              key={tab.id}
              type="button"
              onClick={() => setActiveTab(tab.id as any)}
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
                    {walletIdentifier}
                  </span>
                </div>
                <span className="text-[11px] text-slate-400 self-start sm:self-auto">
                  {locale === 'vi-VN' ? 'Ví hoạt động' : 'Active Wallet'}
                </span>
              </div>
            </div>
          </div>
        )}

        {/* SECTION: Security & Authentication */}
        {(activeTab === 'all' || activeTab === 'security') && (
          <div className="bg-white dark:bg-slate-900 rounded-2xl border border-slate-200/80 dark:border-slate-800 shadow-xs overflow-hidden">
            <div className="px-5 py-4 border-b border-slate-100 dark:border-slate-800 bg-slate-50/50 dark:bg-slate-900/50">
              <h3 className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400 flex items-center gap-2">
                <Shield className="h-4 w-4 text-slate-400" />
                <span>{locale === 'vi-VN' ? 'Bảo mật & Xác thực' : 'Security & Authentication'}</span>
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
                    <span className="text-[10px] font-bold px-2 py-0.5 rounded-full bg-emerald-50 dark:bg-emerald-950/60 text-emerald-700 dark:text-emerald-400 border border-emerald-200 dark:border-emerald-800">
                      {locale === 'vi-VN' ? 'Đã kích hoạt' : 'Active'}
                    </span>
                  </div>
                  <p className="text-xs text-slate-400 max-w-xl">
                    {locale === 'vi-VN'
                      ? 'Dùng để xác thực chuyển tiền, mở quỹ chung và điều chỉnh hạn mức.'
                      : 'Used to authorize transfers, shared funds and limit changes.'}
                  </p>
                </div>
                <div className="flex items-center gap-3 self-start sm:self-auto shrink-0">
                  <button
                    type="button"
                    onClick={() => triggerToast(locale === 'vi-VN' ? 'Xem trước: Mở hướng dẫn Quên mã PIN' : 'Preview: Forgot PIN guide')}
                    className="text-xs font-semibold text-slate-500 hover:text-emerald-600 dark:text-slate-400 dark:hover:text-emerald-400 hover:underline cursor-pointer"
                  >
                    {locale === 'vi-VN' ? 'Quên mã PIN?' : 'Forgot PIN?'}
                  </button>
                  <Button
                    size="sm"
                    variant="outline"
                    onClick={() => triggerToast(locale === 'vi-VN' ? 'Xem trước: Mở hộp thoại Đổi mã PIN' : 'Preview: Open Change PIN')}
                    className="text-xs font-semibold text-emerald-600 dark:text-emerald-400 hover:text-emerald-700 border-slate-200 dark:border-slate-700"
                  >
                    {locale === 'vi-VN' ? 'Đổi mã PIN' : 'Change PIN'}
                  </Button>
                </div>
              </div>

              {/* Row 2: Password */}
              <div className="p-4 sm:p-5 flex flex-col sm:flex-row sm:items-center justify-between gap-4">
                <div className="space-y-0.5">
                  <span className="text-xs sm:text-sm font-bold text-slate-900 dark:text-white">
                    {locale === 'vi-VN' ? 'Mật khẩu đăng nhập' : 'Login Password'}
                  </span>
                  <p className="text-xs text-slate-400 max-w-xl">
                    {locale === 'vi-VN'
                      ? 'Tối thiểu 8 ký tự, bao gồm chữ hoa, chữ thường và chữ số.'
                      : 'Minimum 8 characters with uppercase, lowercase and numbers.'}
                  </p>
                </div>
                <Button
                  size="sm"
                  variant="outline"
                  onClick={() => triggerToast(locale === 'vi-VN' ? 'Xem trước: Mở hộp thoại Đổi mật khẩu' : 'Preview: Open Password Dialog')}
                  className="text-xs font-semibold text-slate-700 dark:text-slate-300 border-slate-200 dark:border-slate-700 self-start sm:self-auto shrink-0"
                >
                  {locale === 'vi-VN' ? 'Đổi mật khẩu' : 'Change Password'}
                </Button>
              </div>

              {/* Row 3: Revoke All Sessions */}
              <div className="p-4 sm:p-5 flex flex-col sm:flex-row sm:items-center justify-between gap-4">
                <div className="space-y-0.5">
                  <span className="text-xs sm:text-sm font-bold text-rose-600 dark:text-rose-400">
                    {locale === 'vi-VN' ? 'Đăng xuất tất cả thiết bị' : 'Sign out all sessions'}
                  </span>
                  <p className="text-xs text-slate-400 max-w-xl">
                    {locale === 'vi-VN'
                      ? 'Hủy tất cả phiên làm việc khác trên điện thoại, máy tính và trình duyệt.'
                      : 'Revoke active sessions across all other devices and browsers.'}
                  </p>
                </div>
                <Button
                  size="sm"
                  variant="outline"
                  onClick={() => triggerToast(locale === 'vi-VN' ? 'Xem trước: Xác nhận đăng xuất tất cả' : 'Preview: Confirm sign out all')}
                  className="text-xs font-semibold text-rose-600 dark:text-rose-400 border-rose-200 dark:border-rose-900/40 hover:bg-rose-50 dark:hover:bg-rose-950/30 self-start sm:self-auto shrink-0"
                >
                  {locale === 'vi-VN' ? 'Đăng xuất tất cả' : 'Sign out all'}
                </Button>
              </div>
            </div>
          </div>
        )}

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
                    {locale === 'vi-VN' ? 'Ngôn ngữ hiển thị' : 'Display Language'}
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
                      triggerToast('Đã đổi ngôn ngữ sang Tiếng Việt');
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
                      triggerToast('Switched language to English');
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
                    {locale === 'vi-VN' ? 'Chủ đề màu sắc' : 'Color Theme'}
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
                        onClick={() => {
                          setTheme(mode.id as any);
                          triggerToast(locale === 'vi-VN' ? `Đã chuyển chủ đề: ${mode.label}` : `Theme: ${mode.label}`);
                        }}
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
                    {locale === 'vi-VN' ? 'Ẩn số dư mặc định' : 'Hide balance by default'}
                  </span>
                  <p className="text-xs text-slate-400">
                    {locale === 'vi-VN' ? 'Che số tiền trên các thẻ ví khi xem ở nơi đông người.' : 'Mask wallet balance amounts in public spaces.'}
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
            <div className="px-5 py-4 border-b border-slate-100 dark:border-slate-800 bg-slate-50/50 dark:bg-slate-900/50">
              <h3 className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400 flex items-center gap-2">
                <Bell className="h-4 w-4 text-slate-400" />
                <span>{locale === 'vi-VN' ? 'Cài đặt thông báo' : 'Notification Settings'}</span>
              </h3>
            </div>

            <div className="divide-y divide-slate-100 dark:divide-slate-800">
              {[
                {
                  id: 'balance',
                  title: locale === 'vi-VN' ? 'Biến động số dư tức thời' : 'Balance Changes',
                  desc: locale === 'vi-VN' ? 'Thông báo khi ví nhận tiền, chuyển tiền hoặc thanh toán giao dịch.' : 'Instant notification on credits, debits and transfers.',
                  checked: notifyBalance,
                  set: setNotifyBalance,
                },
                {
                  id: 'budget',
                  title: locale === 'vi-VN' ? 'Cảnh báo hạn mức ngân sách' : 'Budget Limit Alerts',
                  desc: locale === 'vi-VN' ? 'Nhắc nhở khi danh mục chi tiêu chạm ngưỡng 80% và 100% hạn mức.' : 'Alerts when expense categories reach 80% and 100%.',
                  checked: notifyBudget,
                  set: setNotifyBudget,
                },
                {
                  id: 'largeTx',
                  title: locale === 'vi-VN' ? 'Giao dịch giá trị lớn' : 'Large Transactions',
                  desc: locale === 'vi-VN' ? 'Cảnh báo đối với các giao dịch có giá trị trên 1.000.000₫.' : 'Notify on single transactions exceeding 1,000,000₫.',
                  checked: notifyLargeTx,
                  set: setNotifyLargeTx,
                },
                {
                  id: 'minBalance',
                  title: locale === 'vi-VN' ? 'Cảnh báo số dư tối thiểu' : 'Minimum Balance Alert',
                  desc: locale === 'vi-VN' ? 'Nhắc nhở khi số dư ví xuống dưới mức an toàn dự phòng.' : 'Remind when wallet balance drops below safety reserve.',
                  checked: notifyMinBalance,
                  set: setNotifyMinBalance,
                },
              ].map((item) => (
                <label key={item.id} className="p-4 sm:p-5 flex items-center justify-between gap-4 cursor-pointer hover:bg-slate-50/50 dark:hover:bg-slate-800/30 transition-colors">
                  <div className="space-y-0.5 pr-4">
                    <span className="text-xs sm:text-sm font-semibold text-slate-900 dark:text-white block">
                      {item.title}
                    </span>
                    <span className="text-xs text-slate-400 block leading-relaxed">
                      {item.desc}
                    </span>
                  </div>
                  <input
                    type="checkbox"
                    checked={item.checked}
                    onChange={(e) => {
                      item.set(e.target.checked);
                      triggerToast(locale === 'vi-VN' ? 'Đã lưu tùy chọn thông báo' : 'Notification preference updated');
                    }}
                    className="h-4 w-4 rounded text-emerald-600 focus:ring-emerald-500 shrink-0 cursor-pointer"
                  />
                </label>
              ))}

              {/* Product Update Email (Matches backend engagement email preference) */}
              <label className="p-4 sm:p-5 flex items-center justify-between gap-4 cursor-pointer hover:bg-slate-50/50 dark:hover:bg-slate-800/30 transition-colors">
                <div className="space-y-0.5 pr-4">
                  <span className="text-xs sm:text-sm font-semibold text-slate-900 dark:text-white block">
                    {locale === 'vi-VN' ? 'Email cập nhật sản phẩm' : 'Product Update Emails'}
                  </span>
                  <span className="text-xs text-slate-400 block leading-relaxed">
                    {locale === 'vi-VN'
                      ? 'Nhận thông tin định kỳ về các tính năng mới và cải tiến bảo mật từ SpendWise.'
                      : 'Receive periodic announcements about new capabilities and security.'}
                  </span>
                </div>
                <input
                  type="checkbox"
                  checked={notifyEmailNews}
                  onChange={(e) => {
                    setNotifyEmailNews(e.target.checked);
                    triggerToast(locale === 'vi-VN' ? 'Đã lưu tùy chọn email' : 'Email preference updated');
                  }}
                  className="h-4 w-4 rounded text-emerald-600 focus:ring-emerald-500 shrink-0 cursor-pointer"
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
              {/* Row 1: Support Portal */}
              <div
                onClick={() => triggerToast(locale === 'vi-VN' ? 'Xem trước: Điều hướng tới /support' : 'Preview: Navigate to /support')}
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
                    {locale === 'vi-VN' ? 'Tạo và theo dõi các ticket giải đáp thắc mắc tài khoản hoặc lỗi tính năng.' : 'Create and monitor support tickets for account questions.'}
                  </p>
                </div>
                <ArrowRight className="h-4 w-4 text-slate-400 group-hover:text-emerald-500 group-hover:translate-x-0.5 transition-all shrink-0" />
              </div>

              {/* Row 2: Transaction Disputes */}
              <div
                onClick={() => triggerToast(locale === 'vi-VN' ? 'Xem trước: Điều hướng tới /disputes' : 'Preview: Navigate to /disputes')}
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
                    {locale === 'vi-VN' ? 'Tra soát giao dịch bị trừ tiền sai, trùng lặp hoặc nghi vấn gian lận.' : 'Report incorrect charges, duplicate payments or suspicious entries.'}
                  </p>
                </div>
                <ArrowRight className="h-4 w-4 text-slate-400 group-hover:text-rose-500 group-hover:translate-x-0.5 transition-all shrink-0" />
              </div>
            </div>
          </div>
        )}

      </div>

      {/* 4. Minimal System Footer Note */}
      <div className="pt-4 text-center text-xs text-slate-400 dark:text-slate-500">
        {APP_META.name} • {APP_META.version}
      </div>
    </div>
  );
};
