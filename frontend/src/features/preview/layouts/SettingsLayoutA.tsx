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
import { useI18n } from '../../../i18n/I18nContext';
import { useTheme } from '../../../providers/ThemeProvider';
import { useBalanceVisibility } from '../../../providers/BalanceVisibilityProvider';
import { useAuth } from '../../../providers/AuthProvider';
import { Button } from '../../../components/ui/Button';
import { cn } from '../../../utils/cn';

/**
 * SETTINGS LAYOUT A — BALANCED WORKSPACE (Comprehensive Form Cards)
 * Bố cục:
 * - Hàng 1: Thẻ hồ sơ cá nhân với định danh KYC, email, số điện thoại và nút chỉnh sửa hồ sơ.
 * - Cột trái (7 cols): Cài đặt bảo mật (Đổi mã PIN 6 số, Đổi mật khẩu) & Tùy chọn giao diện.
 * - Cột phải (5 cols): Quyền riêng tư, quản lý thiết bị & nút đăng xuất an toàn.
 */
export interface SettingsLayoutProps {
  onOpenPopup?: (popupId: string, mode?: 'default' | 'prefill') => void;
}

export const SettingsLayoutA: React.FC<SettingsLayoutProps> = ({ onOpenPopup }) => {
  const { t, locale } = useI18n();
  const { theme, setTheme } = useTheme();
  const { isHidden, toggleVisibility } = useBalanceVisibility();
  const { currentUser } = useAuth();

  return (
    <div className="space-y-6">
      {/* 1. Profile Header Card */}
      <div className="p-6 rounded-3xl bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 shadow-xs flex flex-col md:flex-row md:items-center justify-between gap-6">
        <div className="flex items-center gap-4">
          <div className="h-16 w-16 rounded-2xl bg-emerald-600 text-white font-bold text-xl flex items-center justify-center shadow-md">
            {currentUser?.fullName?.substring(0, 2).toUpperCase() || 'SW'}
          </div>
          <div>
            <div className="flex items-center gap-2">
              <h2 className="text-lg font-extrabold text-slate-900 dark:text-white">
                {currentUser?.fullName || 'Người dùng SpendWise'}
              </h2>
              <span className="text-[10px] font-bold px-2 py-0.5 rounded-full bg-emerald-100 text-emerald-800 dark:bg-emerald-950 dark:text-emerald-300">
                Đã xác minh KYC
              </span>
            </div>
            <p className="text-xs text-slate-400 font-mono mt-0.5">{currentUser?.email || 'user@spendwise.local'}</p>
          </div>
        </div>

        <div className="flex items-center gap-2">
          <Button
            size="sm"
            variant="outline"
            onClick={() => onOpenPopup?.('settings-edit-profile')}
            className="text-xs font-semibold"
          >
            Sửa hồ sơ
          </Button>
          <Button
            size="sm"
            variant="outline"
            onClick={() => onOpenPopup?.('settings-logout-confirm')}
            className="text-xs font-semibold text-rose-600 hover:text-rose-700"
          >
            Đăng xuất
          </Button>
        </div>
      </div>

      {/* 2. Grid (7 cols / 5 cols) */}
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 items-start">
        {/* Left (7 cols): Security & Theme */}
        <div className="lg:col-span-7 space-y-4">
          <div className="p-5 rounded-2xl bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 shadow-xs space-y-4 text-xs">
            <h3 className="font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400 flex items-center gap-2">
              <Shield className="h-4 w-4 text-emerald-500" />
              <span>Bảo mật & Mã khóa</span>
            </h3>

            <div className="space-y-3">
              <div className="p-3.5 rounded-xl border border-slate-100 dark:border-slate-800 flex items-center justify-between">
                <div>
                  <span className="font-bold text-slate-900 dark:text-white block">Mã PIN bảo mật 6 số</span>
                  <span className="text-[11px] text-slate-400">Dùng để xác thực chuyển tiền và rút quỹ</span>
                </div>
                <Button
                  size="sm"
                  variant="outline"
                  onClick={() => onOpenPopup?.('settings-change-pin')}
                  className="text-xs"
                >
                  Đổi PIN
                </Button>
              </div>

              <div className="p-3.5 rounded-xl border border-slate-100 dark:border-slate-800 flex items-center justify-between">
                <div>
                  <span className="font-bold text-slate-900 dark:text-white block">Mật khẩu tài khoản</span>
                  <span className="text-[11px] text-slate-400">Đăng nhập tài khoản SpendWise</span>
                </div>
                <Button
                  size="sm"
                  variant="outline"
                  onClick={() => onOpenPopup?.('settings-change-password')}
                  className="text-xs"
                >
                  Đổi mật khẩu
                </Button>
              </div>
            </div>
          </div>
        </div>

        {/* Right (5 cols): Preferences & Privacy */}
        <div className="lg:col-span-5 space-y-4">
          <div className="p-5 rounded-2xl bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 shadow-xs space-y-3 text-xs">
            <h3 className="font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400 flex items-center gap-2">
              <Palette className="h-4 w-4 text-indigo-500" />
              <span>Giao diện & Tùy chọn</span>
            </h3>
            <div className="flex gap-2 pt-1">
              <button
                type="button"
                onClick={() => setTheme('light')}
                className={cn('flex-1 py-2 rounded-xl border text-xs font-bold transition flex items-center justify-center gap-1.5', theme === 'light' ? 'bg-slate-900 text-white dark:bg-white dark:text-slate-900' : 'border-slate-200 text-slate-600')}
              >
                <Sun className="h-3.5 w-3.5" />
                <span>Sáng</span>
              </button>
              <button
                type="button"
                onClick={() => setTheme('dark')}
                className={cn('flex-1 py-2 rounded-xl border text-xs font-bold transition flex items-center justify-center gap-1.5', theme === 'dark' ? 'bg-slate-900 text-white dark:bg-white dark:text-slate-900' : 'border-slate-200 text-slate-600')}
              >
                <Moon className="h-3.5 w-3.5" />
                <span>Tối</span>
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};
