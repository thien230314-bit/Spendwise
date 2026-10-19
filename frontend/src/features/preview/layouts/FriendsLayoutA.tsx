import React, { useState, useMemo } from 'react';
import {
  Users,
  UserPlus,
  Search,
  Star,
  ArrowUpRight,
  Send,
  MoreVertical,
  CheckCircle2,
  Clock,
  UserCheck,
  UserX,
  Phone,
  ShieldCheck,
} from 'lucide-react';
import { useI18n } from '../../../i18n/I18nContext';
import { Button } from '../../../components/ui/Button';
import { cn } from '../../../utils/cn';

/**
 * FRIENDS LAYOUT A — BALANCED WORKSPACE (Based on Stitch Screen 1a1ddce52b814760ab6c85ddf97d3f2d)
 * Bố cục:
 * - Hàng 1: Thống kê mạng lưới tài chính (Tổng bạn bè, Yêu thích, Lời mời chờ, Đang chia tiền).
 * - Cột trái (8 cols): Danh bạ bạn bè với thanh tìm kiếm, bộ lọc chip và các tác vụ tài chính (Chuyển tiền nhanh, Sửa biệt danh, Hủy bạn).
 * - Cột phải (4 cols): Khối Lời mời kết bạn (Chấp nhận/Từ chối) & Thêm bạn bè nhanh qua SĐT/STK.
 */
export interface FriendsLayoutProps {
  onOpenPopup?: (popupId: string, mode?: 'default' | 'prefill') => void;
}

export const FriendsLayoutA: React.FC<FriendsLayoutProps> = ({ onOpenPopup }) => {
  const { t, locale } = useI18n();

  const [searchQuery, setSearchQuery] = useState('');
  const [filterTab, setFilterTab] = useState<'all' | 'favorites' | 'shared'>('all');
  const [requestTab, setRequestTab] = useState<'received' | 'sent'>('received');

  const friendsList = useMemo(() => [
    { id: '1', name: 'Nguyễn Văn An', phone: '0912***678', bank: 'Vietcombank • 9821', tag: 'Nhóm Du Lịch', isFav: true, splitAmt: 250000 },
    { id: '2', name: 'Trần Mai Lan', phone: '0901***888', bank: 'MoMo • 0901', tag: 'Gia đình', isFav: true, splitAmt: 0 },
    { id: '3', name: 'Hoàng Tuấn Bách', phone: '0988***123', bank: 'Techcombank • 4410', tag: 'Đồng nghiệp', isFav: false, splitAmt: 120000 },
    { id: '4', name: 'Lê Hương Giang', phone: '0934***555', bank: 'MB Bank • 8832', tag: 'Nhóm Cà phê', isFav: false, splitAmt: 0 },
  ], []);

  const filteredFriends = useMemo(() => {
    return friendsList.filter((f) => {
      if (filterTab === 'favorites' && !f.isFav) return false;
      if (filterTab === 'shared' && f.splitAmt === 0) return false;
      if (searchQuery.trim() && !f.name.toLowerCase().includes(searchQuery.toLowerCase())) return false;
      return true;
    });
  }, [friendsList, filterTab, searchQuery]);

  return (
    <div className="space-y-6">
      {/* 1. Metric Overview Header Banner */}
      <div className="p-6 rounded-3xl bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 shadow-xs flex flex-col md:flex-row md:items-center justify-between gap-6">
        <div>
          <div className="flex items-center gap-2 mb-1">
            <Users className="h-5 w-5 text-emerald-500" />
            <h2 className="text-xl font-extrabold tracking-tight text-slate-900 dark:text-white">
              {locale === 'vi-VN' ? 'Mạng lưới bạn bè & Danh bạ' : 'Friends & Financial Network'}
            </h2>
            <span className="text-[10px] font-bold px-2 py-0.5 rounded-full bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-300">
              Layout A (Balanced 2-Column)
            </span>
          </div>
          <div className="flex flex-wrap items-center gap-4 text-xs font-mono text-slate-500 mt-2">
            <span>Tổng số: <strong className="text-slate-900 dark:text-white">48</strong> bạn bè</span>
            <span>•</span>
            <span>Yêu thích: <strong className="text-amber-600">8</strong></span>
            <span>•</span>
            <span>Yêu cầu chờ: <strong className="text-indigo-600">3</strong></span>
            <span>•</span>
            <span>Đang chia tiền: <strong className="text-emerald-600">3 nhóm</strong></span>
          </div>
        </div>

        <div className="flex items-center gap-2">
          <Button
            size="sm"
            variant="primary"
            onClick={() => onOpenPopup?.('friend-add')}
            className="text-xs font-semibold gap-1.5 cursor-pointer shadow-xs"
          >
            <UserPlus className="h-3.5 w-3.5" />
            <span>+ Thêm bạn bè mới</span>
          </Button>
        </div>
      </div>

      {/* 2. Main 2-Column Grid (8 cols / 4 cols) */}
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 items-start">
        {/* Left (8 cols): Directory & Search */}
        <div className="lg:col-span-8 space-y-4">
          {/* Search & Filter Chips */}
          <div className="p-4 rounded-2xl bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 shadow-xs space-y-3">
            <div className="relative">
              <Search className="absolute left-3.5 top-1/2 -translate-y-1/2 h-4 w-4 text-slate-400" />
              <input
                type="text"
                placeholder={locale === 'vi-VN' ? 'Tìm bạn bè theo tên, số điện thoại...' : 'Search friends by name, phone...'}
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
                className="w-full pl-10 pr-4 py-2 rounded-xl bg-slate-50 dark:bg-slate-800/80 border border-slate-200 dark:border-slate-700 text-xs focus:ring-2 focus:ring-emerald-500 outline-none"
              />
            </div>

            <div className="flex items-center gap-2 pt-1">
              {[
                { id: 'all', label: `Tất cả (${friendsList.length})` },
                { id: 'favorites', label: 'Yêu thích (2)' },
                { id: 'shared', label: 'Đang chia tiền (2)' },
              ].map((tab) => (
                <button
                  key={tab.id}
                  type="button"
                  onClick={() => setFilterTab(tab.id as any)}
                  className={cn(
                    'px-3 py-1.5 rounded-lg text-xs font-medium transition cursor-pointer',
                    filterTab === tab.id
                      ? 'bg-slate-900 text-white dark:bg-white dark:text-slate-900 font-bold'
                      : 'bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-400 hover:text-slate-900'
                  )}
                >
                  {tab.label}
                </button>
              ))}
            </div>
          </div>

          {/* Friends Cards Grid */}
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            {filteredFriends.map((f) => (
              <div
                key={f.id}
                className="p-4 rounded-2xl bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 shadow-xs space-y-3 flex flex-col justify-between"
              >
                <div>
                  <div className="flex items-start justify-between gap-2">
                    <div className="flex items-center gap-2.5">
                      <div className="h-10 w-10 rounded-full bg-emerald-100 dark:bg-emerald-950 text-emerald-700 dark:text-emerald-300 flex items-center justify-center font-bold text-sm">
                        {f.name.substring(0, 2).toUpperCase()}
                      </div>
                      <div>
                        <h4 className="text-sm font-bold text-slate-900 dark:text-white flex items-center gap-1.5">
                          <span>{f.name}</span>
                          {f.isFav && <Star className="h-3 w-3 fill-amber-400 text-amber-400" />}
                        </h4>
                        <p className="text-[11px] text-slate-400 font-mono">{f.phone}</p>
                      </div>
                    </div>

                    <div className="flex items-center gap-1">
                      <button
                        type="button"
                        onClick={() => onOpenPopup?.('friend-edit-nickname', 'prefill')}
                        className="p-1 rounded text-slate-400 hover:text-slate-700 dark:hover:text-slate-200 text-xs font-bold"
                        title="Sửa biệt danh"
                      >
                        Sửa
                      </button>
                      <button
                        type="button"
                        onClick={() => onOpenPopup?.('friend-unfriend')}
                        className="p-1 rounded text-rose-400 hover:text-rose-600 text-xs font-bold"
                        title="Hủy kết bạn"
                      >
                        Hủy
                      </button>
                    </div>
                  </div>

                  <div className="mt-3 pt-2.5 border-t border-slate-100 dark:border-slate-800 text-[11px] space-y-1">
                    <div className="flex justify-between text-slate-500">
                      <span>Tài khoản liên kết:</span>
                      <span className="font-medium text-slate-700 dark:text-slate-300">{f.bank}</span>
                    </div>
                    {f.splitAmt > 0 && (
                      <div className="flex justify-between font-mono">
                        <span className="text-emerald-600 font-semibold">Đang chia tiền:</span>
                        <span className="font-bold text-emerald-600">{f.splitAmt.toLocaleString('vi-VN')} ₫</span>
                      </div>
                    )}
                  </div>
                </div>

                <div className="pt-2 flex items-center justify-between gap-2 border-t border-slate-100 dark:border-slate-800">
                  <span className="text-[10px] font-bold px-2 py-0.5 rounded-full bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-400">
                    {f.tag}
                  </span>
                  <button
                    type="button"
                    onClick={() => onOpenPopup?.('transfer-review-modal')}
                    className="text-xs font-bold text-emerald-600 hover:underline flex items-center gap-1"
                  >
                    <span>Chuyển tiền</span>
                    <ArrowUpRight className="h-3 w-3" />
                  </button>
                </div>
              </div>
            ))}
          </div>
        </div>

        {/* Right (4 cols): Connection Requests & Quick Add */}
        <div className="lg:col-span-4 space-y-4">
          {/* Inbound / Outbound Requests */}
          <div className="p-5 rounded-2xl bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 shadow-xs space-y-4 text-xs">
            <div className="flex items-center justify-between border-b border-slate-100 dark:border-slate-800 pb-2.5">
              <h3 className="font-bold uppercase tracking-wider text-slate-700 dark:text-slate-300 flex items-center gap-1.5">
                <Clock className="h-3.5 w-3.5 text-indigo-500" />
                <span>Lời mời kết nối</span>
              </h3>
              <div className="flex items-center gap-1">
                <button
                  type="button"
                  onClick={() => setRequestTab('received')}
                  className={cn('px-2 py-0.5 rounded text-[11px] font-bold', requestTab === 'received' ? 'bg-indigo-100 dark:bg-indigo-950 text-indigo-700 dark:text-indigo-300' : 'text-slate-400')}
                >
                  Đã nhận (2)
                </button>
                <button
                  type="button"
                  onClick={() => setRequestTab('sent')}
                  className={cn('px-2 py-0.5 rounded text-[11px] font-bold', requestTab === 'sent' ? 'bg-indigo-100 dark:bg-indigo-950 text-indigo-700 dark:text-indigo-300' : 'text-slate-400')}
                >
                  Đã gửi (1)
                </button>
              </div>
            </div>

            <div className="space-y-3">
              {[
                { name: 'Vũ Thùy Linh', phone: '0977***654', mutual: 'Có 3 bạn chung' },
                { name: 'Đặng Minh Trí', phone: '0981***999', mutual: 'Cùng nhóm Du Lịch' },
              ].map((req, i) => (
                <div key={i} className="p-3 rounded-xl bg-slate-50 dark:bg-slate-800/60 border border-slate-100 dark:border-slate-800 space-y-2">
                  <div className="flex items-center justify-between">
                    <div>
                      <p className="font-bold text-slate-900 dark:text-white">{req.name}</p>
                      <p className="text-[10px] text-slate-400 font-mono">{req.phone} • {req.mutual}</p>
                    </div>
                  </div>
                  <div className="flex items-center gap-2 pt-1">
                    <button
                      type="button"
                      onClick={() => onOpenPopup?.('friend-accept')}
                      className="flex-1 py-1 rounded-lg bg-emerald-600 hover:bg-emerald-700 text-white font-bold text-[11px] flex items-center justify-center gap-1 shadow-xs"
                    >
                      <UserCheck className="h-3 w-3" />
                      <span>Chấp nhận</span>
                    </button>
                    <button
                      type="button"
                      onClick={() => onOpenPopup?.('friend-reject')}
                      className="flex-1 py-1 rounded-lg border border-slate-200 dark:border-slate-700 text-slate-600 dark:text-slate-300 font-semibold text-[11px] flex items-center justify-center gap-1 hover:bg-slate-100"
                    >
                      <UserX className="h-3 w-3" />
                      <span>Từ chối</span>
                    </button>
                  </div>
                </div>
              ))}
            </div>
          </div>

          {/* Quick Add Search Panel */}
          <div className="p-5 rounded-2xl bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 shadow-xs space-y-3 text-xs">
            <h3 className="font-bold uppercase tracking-wider text-slate-700 dark:text-slate-300 flex items-center gap-1.5">
              <UserPlus className="h-3.5 w-3.5 text-emerald-500" />
              <span>Tra cứu bạn bè nhanh</span>
            </h3>
            <p className="text-[11px] text-slate-400">
              Nhập số điện thoại người dùng SpendWise để gửi lời mời kết bạn tức thời.
            </p>
            <div className="flex gap-2">
              <input
                type="text"
                placeholder="0912..."
                defaultValue="0988776655"
                className="flex-1 px-3 py-2 rounded-xl bg-slate-50 dark:bg-slate-800 border border-slate-200 dark:border-slate-700 font-mono text-xs"
              />
              <button
                type="button"
                onClick={() => onOpenPopup?.('friend-add', 'prefill')}
                className="px-3 py-2 rounded-xl bg-slate-900 dark:bg-white text-white dark:text-slate-900 font-bold text-xs"
              >
                Tìm
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};
