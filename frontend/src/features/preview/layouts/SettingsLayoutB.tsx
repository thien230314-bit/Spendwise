import React, { useState } from 'react';
import {
  User,
  Shield,
  Palette,
  Bell,
  Sun,
  Moon,
  Laptop,
  Check,
  KeyRound,
  ShieldAlert,
  LogOut,
  SlidersHorizontal,
} from 'lucide-react';
import { useI18n } from '../../../i18n/I18nContext';
import { useTheme } from '../../../providers/ThemeProvider';
import { useAuth } from '../../../providers/AuthProvider';
import { Button } from '../../../components/ui/Button';
import { cn } from '../../../utils/cn';

/**
 * SETTINGS LAYOUT B — EFFICIENT HIGH-DENSITY WORKSPACE
 * Bố cục:
 * - Cột trái (4 cols): Thanh menu tab dọc theo từng mảng chức năng (Tài khoản, Bảo mật, Giao diện, Phiên đăng nhập).
 * - Cột phải (8 cols): Khối cài đặt tập trung tương ứng với hành động lưu tức thì và các hộp tác vụ bảo mật.
 */
export interface SettingsLayoutProps {
  onOpenPopup?: (popupId: string, mode?: 'default' | 'prefill') => void;
}

export const SettingsLayoutB: React.FC<SettingsLayoutProps> = ({ onOpenPopup }) => {
  const { t, locale } = useI18n();
  const { theme, setTheme } = useTheme();
  const { currentUser } = useAuth();

  const [activeTab, setActiveTab] = useState<'security' | 'profile' | 'preferences'>('security');

  return (
    <div className="space-y-6">
      {/* 1. Header Toolbar */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 pb-2 border-b border-slate-200/80 dark:border-slate-800">
        <div className="flex items-center gap-2">
          <Shield className="h-5 w-5 text-cyan-500" />
          <h2 className="text-xl font-extrabold tracking-tight text-slate-900 dark:text-white">
            {locale === 'vi-VN' ? 'Cài đặt hệ thống & Bảo mật' : 'System Settings'}
          </h2>
        </div>
        <div className="flex items-center gap-2">
          <span className="text-[10px] font-bold px-2.5 py-1 rounded-full bg-cyan-100 dark:bg-cyan-950/60 text-cyan-800 dark:text-cyan-300">
            Layout B (Efficient Sidebar Navigation)
          </span>
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

      {/* 2. Side Nav & Content (4 cols / 8 cols) */}
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 items-start">
        {/* Left Nav (4 cols) */}
        <div className="lg:col-span-4 p-4 rounded-2xl bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 shadow-xs space-y-1.5 text-xs">
          <button
            type="button"
            onClick={() => setActiveTab('security')}
            className={cn('w-full text-left px-3.5 py-2.5 rounded-xl font-bold transition flex items-center gap-2.5', activeTab === 'security' ? 'bg-slate-900 text-white dark:bg-white dark:text-slate-900' : 'text-slate-600 hover:bg-slate-50')}
          >
            <Shield className="h-4 w-4" />
            <span>Bảo mật & Mã PIN</span>
          </button>
          <button
            type="button"
            onClick={() => setActiveTab('profile')}
            className={cn('w-full text-left px-3.5 py-2.5 rounded-xl font-bold transition flex items-center gap-2.5', activeTab === 'profile' ? 'bg-slate-900 text-white dark:bg-white dark:text-slate-900' : 'text-slate-600 hover:bg-slate-50')}
          >
            <User className="h-4 w-4" />
            <span>Thông tin cá nhân</span>
          </button>
          <button
            type="button"
            onClick={() => setActiveTab('preferences')}
            className={cn('w-full text-left px-3.5 py-2.5 rounded-xl font-bold transition flex items-center gap-2.5', activeTab === 'preferences' ? 'bg-slate-900 text-white dark:bg-white dark:text-slate-900' : 'text-slate-600 hover:bg-slate-50')}
          >
            <Palette className="h-4 w-4" />
            <span>Giao diện & Chủ đề</span>
          </button>
        </div>

        {/* Right Content (8 cols) */}
        <div className="lg:col-span-8 p-6 rounded-2xl bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 shadow-xs space-y-4 text-xs">
          {activeTab === 'security' && (
            <div className="space-y-4">
              <h3 className="font-bold text-sm text-slate-900 dark:text-white">Thiết lập bảo mật hai lớp & PIN</h3>
              <div className="space-y-3">
                <div className="p-3.5 rounded-xl bg-slate-50 dark:bg-slate-800 flex items-center justify-between">
                  <div>
                    <span className="font-bold block">Mã PIN giao dịch 6 số</span>
                    <span className="text-[11px] text-slate-400">Đã kích hoạt bảo vệ tài khoản</span>
                  </div>
                  <Button size="sm" variant="outline" onClick={() => onOpenPopup?.('settings-change-pin')} className="text-xs">
                    Thay đổi
                  </Button>
                </div>
                <div className="p-3.5 rounded-xl bg-slate-50 dark:bg-slate-800 flex items-center justify-between">
                  <div>
                    <span className="font-bold block">Mật khẩu đăng nhập</span>
                    <span className="text-[11px] text-slate-400">Cập nhật lần cuối: 30 ngày trước</span>
                  </div>
                  <Button size="sm" variant="outline" onClick={() => onOpenPopup?.('settings-change-password')} className="text-xs">
                    Cập nhật
                  </Button>
                </div>
              </div>
            </div>
          )}

          {activeTab === 'profile' && (
            <div className="space-y-4">
              <h3 className="font-bold text-sm text-slate-900 dark:text-white">Hồ sơ cá nhân KYC</h3>
              <div className="space-y-2">
                <p>Họ tên: <strong>{currentUser?.fullName || 'Người dùng SpendWise'}</strong></p>
                <p>Email: <strong>{currentUser?.email || 'user@spendwise.local'}</strong></p>
                <p>Số điện thoại: <strong>0912***678</strong></p>
              </div>
              <Button size="sm" variant="primary" onClick={() => onOpenPopup?.('settings-edit-profile')} className="text-xs">
                Chỉnh sửa thông tin
              </Button>
            </div>
          )}

          {activeTab === 'preferences' && (
            <div className="space-y-4">
              <h3 className="font-bold text-sm text-slate-900 dark:text-white">Chủ đề hiển thị</h3>
              <div className="flex gap-2">
                <button
                  type="button"
                  onClick={() => setTheme('light')}
                  className={cn('px-4 py-2 rounded-xl border text-xs font-bold', theme === 'light' ? 'bg-slate-900 text-white dark:bg-white dark:text-slate-900' : 'border-slate-200')}
                >
                  Giao diện Sáng
                </button>
                <button
                  type="button"
                  onClick={() => setTheme('dark')}
                  className={cn('px-4 py-2 rounded-xl border text-xs font-bold', theme === 'dark' ? 'bg-slate-900 text-white dark:bg-white dark:text-slate-900' : 'border-slate-200')}
                >
                  Giao diện Tối
                </button>
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
};
