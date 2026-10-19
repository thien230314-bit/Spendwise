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
  Layers,
} from 'lucide-react';
import { useI18n } from '../../../i18n/I18nContext';
import { Button } from '../../../components/ui/Button';
import { cn } from '../../../utils/cn';

/**
 * NOTIFICATIONS LAYOUT B — EFFICIENT HIGH-DENSITY WORKSPACE
 * Bố cục:
 * - Cột trái (4 cols): Widget tổng hợp tài khoản ví và số thông báo chưa đọc, kèm bộ lọc danh mục compact.
 * - Cột phải (8 cols): Bảng sự kiện thông báo gọn gàng, hỗ trợ đọc nhanh và đánh dấu đã đọc tức thì.
 */
export interface NotificationsLayoutProps {
  onOpenPopup?: (popupId: string, mode?: 'default' | 'prefill') => void;
}

export const NotificationsLayoutB: React.FC<NotificationsLayoutProps> = ({ onOpenPopup }) => {
  const { t, locale } = useI18n();

  const [activeAccount, setActiveAccount] = useState('all');

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

  return (
    <div className="space-y-6">
      {/* 1. Header Toolbar */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 pb-2 border-b border-slate-200/80 dark:border-slate-800">
        <div className="flex items-center gap-2">
          <Bell className="h-5 w-5 text-cyan-500" />
          <h2 className="text-xl font-extrabold tracking-tight text-slate-900 dark:text-white">
            {locale === 'vi-VN' ? 'Nhật ký biến động & Cảnh báo' : 'Notification Stream'}
          </h2>
        </div>
        <div className="flex items-center gap-2">
          <span className="text-[10px] font-bold px-2.5 py-1 rounded-full bg-cyan-100 dark:bg-cyan-950/60 text-cyan-800 dark:text-cyan-300">
            Layout B (High-Density Dual-Pane)
          </span>
          <Button
            size="sm"
            variant="outline"
            onClick={() => onOpenPopup?.('notif-account-pref')}
            className="text-xs font-semibold gap-1 cursor-pointer"
          >
            <SlidersHorizontal className="h-3.5 w-3.5" />
            <span>Tùy chỉnh</span>
          </Button>
          <Button
            size="sm"
            variant="primary"
            onClick={() => onOpenPopup?.('notif-mark-all')}
            className="text-xs font-semibold gap-1 cursor-pointer shadow-xs"
          >
            <CheckCheck className="h-3.5 w-3.5" />
            <span>Đã đọc</span>
          </Button>
        </div>
      </div>

      {/* 2. Asymmetric Dual-Pane (4 cols / 8 cols) */}
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 items-start">
        {/* Left (4 cols): Account Filter Cards */}
        <div className="lg:col-span-4 space-y-4">
          <div className="p-4 rounded-2xl bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 shadow-xs space-y-3 text-xs">
            <h3 className="font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400">
              Ví & Tài khoản theo dõi
            </h3>
            <div className="space-y-1.5">
              {[
                { id: 'all', name: 'Toàn bộ ví tài chính', unread: 2 },
                { id: 'w1', name: 'Ví Chính Vietcombank', unread: 1 },
                { id: 'w2', name: 'Ví Tiền Mặt', unread: 0 },
                { id: 'w3', name: 'Quỹ Chi Tiêu Chung', unread: 1 },
              ].map((acc) => (
                <button
                  key={acc.id}
                  type="button"
                  onClick={() => setActiveAccount(acc.id)}
                  className={cn(
                    'w-full text-left px-3 py-2 rounded-xl transition flex items-center justify-between',
                    activeAccount === acc.id
                      ? 'bg-slate-900 text-white dark:bg-white dark:text-slate-900 font-bold'
                      : 'text-slate-600 dark:text-slate-400 hover:bg-slate-50 dark:hover:bg-slate-800'
                  )}
                >
                  <span>{acc.name}</span>
                  {acc.unread > 0 && (
                    <span className="h-5 px-1.5 rounded-full bg-rose-500 text-white font-mono text-[10px] font-bold flex items-center justify-center">
                      {acc.unread}
                    </span>
                  )}
                </button>
              ))}
            </div>
          </div>
        </div>

        {/* Right (8 cols): Compact Event Stream */}
        <div className="lg:col-span-8 bg-white dark:bg-slate-900 rounded-2xl border border-slate-200/80 dark:border-slate-800 shadow-xs overflow-hidden divide-y divide-slate-100 dark:divide-slate-800">
          {notificationsList.map((item) => (
            <div
              key={item.id}
              onClick={() => onOpenPopup?.('notif-detail')}
              className={cn(
                'p-3.5 flex items-start justify-between gap-3 transition cursor-pointer hover:bg-slate-50/70 dark:hover:bg-slate-800/40 text-xs',
                !item.read ? 'bg-cyan-50/20 dark:bg-cyan-950/10' : ''
              )}
            >
              <div className="flex items-start gap-3 min-w-0">
                <div className={cn(
                  'h-8 w-8 rounded-lg flex items-center justify-center shrink-0 mt-0.5',
                  item.type === 'balance' ? 'bg-emerald-100 text-emerald-600' :
                  item.type === 'budget' ? 'bg-amber-100 text-amber-600' :
                  item.type === 'security' ? 'bg-indigo-100 text-indigo-600' : 'bg-slate-100 text-slate-600'
                )}>
                  {item.type === 'balance' ? <ArrowDownLeft className="h-3.5 w-3.5" /> :
                   item.type === 'budget' ? <AlertTriangle className="h-3.5 w-3.5" /> :
                   <Bell className="h-3.5 w-3.5" />}
                </div>
                <div className="min-w-0">
                  <p className={cn('font-bold truncate', !item.read ? 'text-slate-900 dark:text-white' : 'text-slate-600 dark:text-slate-300')}>
                    {item.title}
                  </p>
                  <p className="text-[11px] text-slate-500 dark:text-slate-400 line-clamp-1 mt-0.5">{item.desc}</p>
                </div>
              </div>

              <div className="text-right shrink-0 font-mono text-[10px] text-slate-400">
                <span>{item.time}</span>
              </div>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
};
