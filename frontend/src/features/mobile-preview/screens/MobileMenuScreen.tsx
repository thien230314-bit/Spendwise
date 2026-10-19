import React from 'react';
import { cn } from '../../../utils/cn';
import {
  Users,
  Folder,
  Building2,
  Bell,
  Sparkles,
  LifeBuoy,
  User,
  Settings,
  Lock,
  Smartphone,
  LogOut,
  ChevronRight,
} from 'lucide-react';

interface MobileMenuScreenProps {
  theme?: 'light' | 'dark';
  onOpenSheet?: (sheetId: string) => void;
  onNavigateScreen?: (screen: string) => void;
  onLogout?: () => void;
}

export const MobileMenuScreen: React.FC<MobileMenuScreenProps> = ({
  theme = 'light',
  onOpenSheet,
  onNavigateScreen,
  onLogout,
}) => {
  const isDark = theme === 'dark';

  return (
    <div
      className={cn(
        'flex-1 flex flex-col p-4 space-y-4 overflow-y-auto transition-colors pb-6',
        isDark ? 'bg-[#090D16]' : 'bg-[#F8FAFC]'
      )}
    >
      {/* 1. Profile Summary Card */}
      <div
        className={cn(
          'p-4 rounded-3xl border shadow-sm flex items-center gap-3.5 transition-all',
          isDark ? 'bg-[#101622] border-[#1E293B]' : 'bg-[#FCFBF7] border-[#E2E8F0]'
        )}
      >
        <div
          className={cn(
            'w-12 h-12 rounded-2xl flex items-center justify-center font-bold text-base shrink-0 shadow-xs',
            isDark ? 'bg-[#052E26] text-[#34D399]' : 'bg-[#ECFDF5] text-[#047857]'
          )}
        >
          SW
        </div>
        <div className="flex-1 min-w-0">
          <span
            className={cn(
              'text-base font-bold tracking-tight block truncate',
              isDark ? 'text-white' : 'text-slate-900'
            )}
          >
            Nguyễn Văn An
          </span>
          <span className="text-[11px] text-slate-400 font-mono">
            SpendId: SW-98234
          </span>
        </div>
      </div>

      {/* 2. Group: TÀI CHÍNH KẾT NỐI */}
      <div className="space-y-1.5">
        <span className="text-[11px] font-bold text-slate-400 uppercase tracking-wider px-2">
          TÀI CHÍNH KẾT NỐI
        </span>
        <div
          className={cn(
            'rounded-3xl border shadow-sm divide-y transition-all overflow-hidden',
            isDark
              ? 'bg-[#101622] border-[#1E293B] divide-slate-800'
              : 'bg-[#FCFBF7] border-[#E2E8F0] divide-slate-200/60'
          )}
        >
          {[
            { id: 'friends', title: 'Bạn bè & Tìm kiếm', detail: 'Tìm qua SĐT, SpendId, quản lý kết bạn', icon: Users, isScreen: true },
            { id: 'groups', title: 'Nhóm & Quỹ chi tiêu', detail: 'Quỹ chung bạn bè, gia đình, công ty', icon: Folder },
            { id: 'banks', title: 'Ngân hàng liên kết', detail: 'Quản lý tài khoản SePay Open Banking', icon: Building2 },
          ].map((item) => {
            const Icon = item.icon;
            return (
              <button
                key={item.id}
                type="button"
                onClick={() => {
                  if (item.isScreen && onNavigateScreen) {
                    onNavigateScreen(item.id);
                  } else {
                    onOpenSheet?.(item.id);
                  }
                }}
                className="w-full p-3.5 flex items-center justify-between gap-3 text-left hover:bg-slate-50/50 dark:hover:bg-slate-800/40 transition-colors cursor-pointer"
              >
                <div className="flex items-center gap-3">
                  <div className="w-8 h-8 rounded-xl bg-emerald-50 dark:bg-emerald-950/60 text-emerald-600 dark:text-emerald-400 flex items-center justify-center shrink-0">
                    <Icon className="w-4 h-4" />
                  </div>
                  <div>
                    <span className="text-xs font-bold text-slate-800 dark:text-slate-200 block">
                      {item.title}
                    </span>
                    <span className="text-[10px] text-slate-400">
                      {item.detail}
                    </span>
                  </div>
                </div>
                <ChevronRight className="w-4 h-4 text-slate-400" />
              </button>
            );
          })}
        </div>
      </div>

      {/* 3. Group: DỊCH VỤ & HỖ TRỢ */}
      <div className="space-y-1.5">
        <span className="text-[11px] font-bold text-slate-400 uppercase tracking-wider px-2">
          DỊCH VỤ & HỖ TRỢ
        </span>
        <div
          className={cn(
            'rounded-3xl border shadow-sm divide-y transition-all overflow-hidden',
            isDark
              ? 'bg-[#101622] border-[#1E293B] divide-slate-800'
              : 'bg-[#FCFBF7] border-[#E2E8F0] divide-slate-200/60'
          )}
        >
          {[
            { id: 'notifications', title: 'Thông báo', detail: 'Cảnh báo số dư và biến động tài khoản', icon: Bell },
            { id: 'support', title: 'Hỗ trợ & Khiếu nại', detail: 'Yêu cầu trợ giúp và xử lý tranh chấp', icon: LifeBuoy },
          ].map((item) => {
            const Icon = item.icon;
            return (
              <button
                key={item.id}
                type="button"
                onClick={() => {
                  if (item.isScreen && onNavigateScreen) {
                    onNavigateScreen(item.id);
                  } else {
                    onOpenSheet?.(item.id);
                  }
                }}
                className="w-full p-3.5 flex items-center justify-between gap-3 text-left hover:bg-slate-50/50 dark:hover:bg-slate-800/40 transition-colors cursor-pointer"
              >
                <div className="flex items-center gap-3">
                  <div className="w-8 h-8 rounded-xl bg-emerald-50 dark:bg-emerald-950/60 text-emerald-600 dark:text-emerald-400 flex items-center justify-center shrink-0">
                    <Icon className="w-4 h-4" />
                  </div>
                  <div>
                    <span className="text-xs font-bold text-slate-800 dark:text-slate-200 block">
                      {item.title}
                    </span>
                    <span className="text-[10px] text-slate-400">
                      {item.detail}
                    </span>
                  </div>
                </div>
                <ChevronRight className="w-4 h-4 text-slate-400" />
              </button>
            );
          })}
        </div>
      </div>

      {/* 4. Group: TÀI KHOẢN & BẢO MẬT */}
      <div className="space-y-1.5">
        <span className="text-[11px] font-bold text-slate-400 uppercase tracking-wider px-2">
          TÀI KHOẢN & BẢO MẬT
        </span>
        <div
          className={cn(
            'rounded-3xl border shadow-sm divide-y transition-all overflow-hidden',
            isDark
              ? 'bg-[#101622] border-[#1E293B] divide-slate-800'
              : 'bg-[#FCFBF7] border-[#E2E8F0] divide-slate-200/60'
          )}
        >
          {[
            { id: 'profile', title: 'Hồ sơ cá nhân', detail: 'Thông tin tài khoản SpendWise', icon: User },
            { id: 'settings', title: 'Tùy chọn giao diện & ngôn ngữ', detail: 'Light/Dark mode, Tiếng Việt / English', icon: Settings },
            { id: 'pin-setting', title: 'Mã PIN bảo mật', detail: 'Quản lý mã PIN 6 chữ số', icon: Lock },
            { id: 'sessions', title: 'Thiết bị & Phiên truy cập', detail: 'Quản lý các thiết bị đăng nhập', icon: Smartphone },
          ].map((item) => {
            const Icon = item.icon;
            return (
              <button
                key={item.id}
                type="button"
                onClick={() => onOpenSheet?.(item.id)}
                className="w-full p-3.5 flex items-center justify-between gap-3 text-left hover:bg-slate-50/50 dark:hover:bg-slate-800/40 transition-colors cursor-pointer"
              >
                <div className="flex items-center gap-3">
                  <div className="w-8 h-8 rounded-xl bg-emerald-50 dark:bg-emerald-950/60 text-emerald-600 dark:text-emerald-400 flex items-center justify-center shrink-0">
                    <Icon className="w-4 h-4" />
                  </div>
                  <div>
                    <span className="text-xs font-bold text-slate-800 dark:text-slate-200 block">
                      {item.title}
                    </span>
                    <span className="text-[10px] text-slate-400">
                      {item.detail}
                    </span>
                  </div>
                </div>
                <ChevronRight className="w-4 h-4 text-slate-400" />
              </button>
            );
          })}
        </div>
      </div>

      {/* Logout Button */}
      <button
        type="button"
        onClick={onLogout}
        className={cn(
          'w-full py-3.5 rounded-2xl border text-xs font-bold flex items-center justify-center gap-2 transition-colors cursor-pointer',
          isDark
            ? 'border-rose-900/40 bg-rose-950/20 text-rose-400 hover:bg-rose-950/40'
            : 'border-rose-200 bg-rose-50 text-rose-600 hover:bg-rose-100'
        )}
      >
        <LogOut className="w-4 h-4" />
        <span>Đăng xuất tài khoản</span>
      </button>
    </div>
  );
};
