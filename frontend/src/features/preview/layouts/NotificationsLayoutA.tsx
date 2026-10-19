import React, { useState, useMemo } from 'react';
import {
  Bell,
  CheckCheck,
  Filter,
  Search,
  Trash2,
  SlidersHorizontal,
  ArrowDownLeft,
  ArrowUpRight,
  ShieldCheck,
  AlertTriangle,
  Clock,
  Eye,
  Settings,
} from 'lucide-react';
import { useI18n } from '../../../i18n/I18nContext';
import { Button } from '../../../components/ui/Button';
import { cn } from '../../../utils/cn';

/**
 * NOTIFICATIONS LAYOUT A — BALANCED WORKSPACE
 * Bố cục:
 * - Hàng 1: Header tổng quan & bộ nút thao tác nhanh (Đánh dấu đã đọc tất cả, Tùy chỉnh thông báo, Xóa tất cả).
 * - Cột trái (8 cols): Dòng thời gian thông báo dạng danh sách trực quan, có avatar phân loại (Biến động số dư, Ngân sách, Bảo mật, Bạn bè).
 * - Cột phải (4 cols): Bộ lọc tài khoản ví chuyên sâu & Tóm tắt số lượng thông báo theo danh mục.
 */
export interface NotificationsLayoutProps {
  onOpenPopup?: (popupId: string, mode?: 'default' | 'prefill') => void;
}

export const NotificationsLayoutA: React.FC<NotificationsLayoutProps> = ({ onOpenPopup }) => {
  const { t, locale } = useI18n();

  const [selectedCat, setSelectedCat] = useState<'all' | 'balance' | 'security' | 'friends'>('all');
  const [searchQuery, setSearchQuery] = useState('');

  const notificationsList = useMemo(() => [
    {
      id: '1',
      title: 'Biến động số dư: +2.500.000 ₫',
      desc: 'Nhận tiền từ Nguyễn Văn An qua ngân hàng Vietcombank (Nội dung: Trả tiền ăn tối).',
      time: '10 phút trước',
      type: 'balance',
      read: false,
      wallet: 'Ví Chính',
    },
    {
      id: '2',
      title: 'Cảnh báo ngân sách chạm 85%',
      desc: 'Hạn mức chi tiêu danh mục Ăn uống tháng 10/2026 đã đạt ngưỡng 4.250.000 / 5.000.000 ₫.',
      time: '2 giờ trước',
      type: 'budget',
      read: false,
      wallet: 'Tất cả',
    },
    {
      id: '3',
      title: 'Đăng nhập thiết bị mới được phê duyệt',
      desc: 'Tài khoản vừa đăng nhập từ Chrome Windows tại TP. Hồ Chí Minh.',
      time: 'Hôm qua 15:30',
      type: 'security',
      read: true,
      wallet: 'Bảo mật',
    },
    {
      id: '4',
      title: 'Lời mời kết bạn mới',
      desc: 'Vũ Thùy Linh vừa gửi cho bạn một lời mời kết nối tài chính.',
      time: 'Hôm qua 09:12',
      type: 'friends',
      read: true,
      wallet: 'Bạn bè',
    },
  ], []);

  const filtered = useMemo(() => {
    return notificationsList.filter((n) => {
      if (selectedCat !== 'all' && n.type !== selectedCat) return false;
      if (searchQuery.trim() && !n.title.toLowerCase().includes(searchQuery.toLowerCase())) return false;
      return true;
    });
  }, [notificationsList, selectedCat, searchQuery]);

  return (
    <div className="space-y-6">
      {/* 1. Header Toolbar */}
      <div className="p-6 rounded-3xl bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 shadow-xs flex flex-col md:flex-row md:items-center justify-between gap-6">
        <div>
          <div className="flex items-center gap-2 mb-1">
            <Bell className="h-5 w-5 text-emerald-500" />
            <h2 className="text-xl font-extrabold tracking-tight text-slate-900 dark:text-white">
              {locale === 'vi-VN' ? 'Trung tâm thông báo' : 'Notifications Hub'}
            </h2>
            <span className="text-[10px] font-bold px-2 py-0.5 rounded-full bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-300">
              Layout A (Balanced 2-Column)
            </span>
          </div>
          <p className="text-xs text-slate-500 dark:text-slate-400">
            {locale === 'vi-VN' ? 'Theo dõi biến động số dư, cảnh báo chi tiêu và cập nhật bảo mật.' : 'Monitor balance changes and alerts.'}
          </p>
        </div>

        <div className="flex items-center gap-2">
          <Button
            size="sm"
            variant="outline"
            onClick={() => onOpenPopup?.('notif-account-pref')}
            className="text-xs font-semibold gap-1.5 cursor-pointer"
          >
            <SlidersHorizontal className="h-3.5 w-3.5" />
            <span>Tùy chỉnh thông báo</span>
          </Button>
          <Button
            size="sm"
            variant="primary"
            onClick={() => onOpenPopup?.('notif-mark-all')}
            className="text-xs font-semibold gap-1.5 cursor-pointer shadow-xs"
          >
            <CheckCheck className="h-3.5 w-3.5" />
            <span>Đọc tất cả</span>
          </Button>
        </div>
      </div>

      {/* 2. Main 2-Column Grid (8 cols / 4 cols) */}
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 items-start">
        {/* Left (8 cols): Notification Feed */}
        <div className="lg:col-span-8 space-y-3">
          <div className="p-3 bg-white dark:bg-slate-900 rounded-2xl border border-slate-200/80 dark:border-slate-800 shadow-xs flex items-center gap-2">
            <Search className="h-4 w-4 text-slate-400 shrink-0 ml-1" />
            <input
              type="text"
              placeholder="Tìm kiếm nội dung thông báo..."
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              className="w-full bg-transparent text-xs outline-none"
            />
          </div>

          <div className="bg-white dark:bg-slate-900 rounded-2xl border border-slate-200/80 dark:border-slate-800 shadow-xs overflow-hidden divide-y divide-slate-100 dark:divide-slate-800">
            {filtered.map((item) => (
              <div
                key={item.id}
                onClick={() => onOpenPopup?.('notif-detail')}
                className={cn(
                  'p-4 flex items-start gap-3.5 transition cursor-pointer hover:bg-slate-50/70 dark:hover:bg-slate-800/40',
                  !item.read ? 'bg-emerald-50/20 dark:bg-emerald-950/10' : ''
                )}
              >
                <div className={cn(
                  'h-9 w-9 rounded-xl flex items-center justify-center shrink-0 mt-0.5',
                  item.type === 'balance' ? 'bg-emerald-100 text-emerald-600 dark:bg-emerald-950/60 dark:text-emerald-400' :
                  item.type === 'budget' ? 'bg-amber-100 text-amber-600 dark:bg-amber-950/60 dark:text-amber-400' :
                  item.type === 'security' ? 'bg-indigo-100 text-indigo-600 dark:bg-indigo-950/60 dark:text-indigo-400' :
                  'bg-slate-100 text-slate-600 dark:bg-slate-800 dark:text-slate-400'
                )}>
                  {item.type === 'balance' ? <ArrowDownLeft className="h-4 w-4" /> :
                   item.type === 'budget' ? <AlertTriangle className="h-4 w-4" /> :
                   item.type === 'security' ? <ShieldCheck className="h-4 w-4" /> :
                   <Bell className="h-4 w-4" />}
                </div>

                <div className="flex-1 min-w-0">
                  <div className="flex items-center justify-between gap-2">
                    <h4 className={cn('text-xs font-bold truncate', !item.read ? 'text-slate-900 dark:text-white' : 'text-slate-600 dark:text-slate-300')}>
                      {item.title}
                    </h4>
                    <span className="text-[10px] text-slate-400 font-mono shrink-0">{item.time}</span>
                  </div>
                  <p className="text-[11px] text-slate-500 dark:text-slate-400 mt-1 line-clamp-2 leading-relaxed">
                    {item.desc}
                  </p>
                  <div className="flex items-center gap-2 mt-2">
                    <span className="text-[9px] font-bold px-2 py-0.5 rounded-full bg-slate-100 dark:bg-slate-800 text-slate-500">
                      {item.wallet}
                    </span>
                    {!item.read && (
                      <span className="text-[9px] font-bold px-2 py-0.5 rounded-full bg-emerald-100 text-emerald-700 dark:bg-emerald-950 dark:text-emerald-300">
                        Mới
                      </span>
                    )}
                  </div>
                </div>
              </div>
            ))}
          </div>
        </div>

        {/* Right (4 cols): Category Filter & Threshold Settings */}
        <div className="lg:col-span-4 space-y-4">
          <div className="p-5 rounded-2xl bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 shadow-xs space-y-3 text-xs">
            <h3 className="font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400 flex items-center gap-1.5">
              <Filter className="h-3.5 w-3.5 text-emerald-500" />
              <span>Lọc theo chuyên mục</span>
            </h3>

            <div className="space-y-1.5">
              {[
                { id: 'all', label: 'Tất cả thông báo', count: 4 },
                { id: 'balance', label: 'Biến động số dư', count: 1 },
                { id: 'budget', label: 'Cảnh báo ngân sách', count: 1 },
                { id: 'security', label: 'Bảo mật & Đăng nhập', count: 1 },
                { id: 'friends', label: 'Lời mời bạn bè', count: 1 },
              ].map((c) => (
                <button
                  key={c.id}
                  type="button"
                  onClick={() => setSelectedCat(c.id as any)}
                  className={cn(
                    'w-full text-left px-3 py-2 rounded-xl font-medium transition flex items-center justify-between',
                    selectedCat === c.id
                      ? 'bg-slate-900 text-white dark:bg-white dark:text-slate-900 font-bold'
                      : 'text-slate-600 dark:text-slate-400 hover:bg-slate-50 dark:hover:bg-slate-800'
                  )}
                >
                  <span>{c.label}</span>
                  <span className="font-mono text-[10px]">{c.count}</span>
                </button>
              ))}
            </div>
          </div>

          <div className="p-4 rounded-2xl bg-emerald-50/50 dark:bg-emerald-950/20 border border-emerald-200/60 dark:border-emerald-900/40 text-xs space-y-2">
            <div className="flex items-center gap-1.5 font-bold text-emerald-900 dark:text-emerald-200">
              <SlidersHorizontal className="h-4 w-4 text-emerald-600" />
              <span>Hạn mức thông báo chủ động</span>
            </div>
            <p className="text-[11px] text-slate-600 dark:text-slate-400 leading-relaxed">
              Bạn có thể điều chỉnh ngưỡng giao dịch lớn (&gt; 1.000.000 ₫) và cảnh báo số dư tối thiểu.
            </p>
            <button
              type="button"
              onClick={() => onOpenPopup?.('notif-account-pref')}
              className="text-xs font-bold text-emerald-600 dark:text-emerald-400 hover:underline pt-1 inline-block"
            >
              Thiết lập ngưỡng ngay →
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};
