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
  Tag,
  Filter,
} from 'lucide-react';
import { useI18n } from '../../../i18n/I18nContext';
import { Button } from '../../../components/ui/Button';
import { cn } from '../../../utils/cn';

/**
 * FRIENDS LAYOUT B — EFFICIENT HIGH-DENSITY 3-COLUMN SPLIT (Based on Stitch Screen 43af37155a364a0aaaf110ed89f51c82)
 * Bố cục:
 * - Cột 1 (3 cols): Điều hướng phân nhóm & Bộ lọc danh bạ (Tất cả, Yêu thích, Nhóm chi tiêu, Bộ lọc KYC).
 * - Cột 2 (6 cols): Bảng danh bạ thu gọn mật độ cao, tích hợp thao tác chuyển tiền & chia tiền trực diện.
 * - Cột 3 (3 cols): Trung tâm tiếp nhận lời mời kết nối & Hộp tra cứu nhanh SePay/Bank ID.
 */
export interface FriendsLayoutProps {
  onOpenPopup?: (popupId: string, mode?: 'default' | 'prefill') => void;
}

export const FriendsLayoutB: React.FC<FriendsLayoutProps> = ({ onOpenPopup }) => {
  const { t, locale } = useI18n();

  const [activeGroupFilter, setActiveGroupFilter] = useState('all');
  const [searchQuery, setSearchQuery] = useState('');

  const friendsList = useMemo(() => [
    { id: '1', name: 'Nguyễn Văn An', phone: '0912***678', bank: 'Vietcombank • 9821', tag: 'Nhóm Du Lịch', isFav: true, splitAmt: 250000 },
    { id: '2', name: 'Trần Mai Lan', phone: '0901***888', bank: 'MoMo • 0901', tag: 'Gia đình', isFav: true, splitAmt: 0 },
    { id: '3', name: 'Hoàng Tuấn Bách', phone: '0988***123', bank: 'Techcombank • 4410', tag: 'Đồng nghiệp', isFav: false, splitAmt: 120000 },
    { id: '4', name: 'Lê Hương Giang', phone: '0934***555', bank: 'MB Bank • 8832', tag: 'Nhóm Cà phê', isFav: false, splitAmt: 0 },
    { id: '5', name: 'Phạm Quốc Dũng', phone: '0915***345', bank: 'ACB • 1209', tag: 'Đồng nghiệp', isFav: true, splitAmt: 350000 },
  ], []);

  const filteredFriends = useMemo(() => {
    return friendsList.filter((f) => {
      if (activeGroupFilter === 'fav' && !f.isFav) return false;
      if (activeGroupFilter === 'split' && f.splitAmt === 0) return false;
      if (searchQuery.trim() && !f.name.toLowerCase().includes(searchQuery.toLowerCase())) return false;
      return true;
    });
  }, [friendsList, activeGroupFilter, searchQuery]);

  return (
    <div className="space-y-6">
      {/* 1. Header Toolbar */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 pb-2 border-b border-slate-200/80 dark:border-slate-800">
        <div className="flex items-center gap-2">
          <Users className="h-5 w-5 text-cyan-500" />
          <h2 className="text-xl font-extrabold tracking-tight text-slate-900 dark:text-white">
            {locale === 'vi-VN' ? 'Danh bạ tài chính liên kết' : 'Financial Directory'}
          </h2>
        </div>
        <div className="flex items-center gap-2">
          <span className="text-[10px] font-bold px-2.5 py-1 rounded-full bg-cyan-100 dark:bg-cyan-950/60 text-cyan-800 dark:text-cyan-300">
            Layout B (High-Density 3-Column Split)
          </span>
          <Button
            size="sm"
            variant="primary"
            onClick={() => onOpenPopup?.('friend-add')}
            className="text-xs font-semibold gap-1 cursor-pointer shadow-xs"
          >
            <UserPlus className="h-3.5 w-3.5" />
            <span>Thêm bạn mới</span>
          </Button>
        </div>
      </div>

      {/* 2. 3-Column Split Grid (3 cols / 6 cols / 3 cols) */}
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-5 items-start">
        {/* Column 1 (3 cols): Categories & Group Tree Navigation */}
        <div className="lg:col-span-3 space-y-4">
          <div className="p-4 rounded-2xl bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 shadow-xs space-y-2 text-xs">
            <h3 className="text-[11px] font-bold uppercase tracking-wider text-slate-400 px-2">Phân loại liên hệ</h3>
            <button
              type="button"
              onClick={() => setActiveGroupFilter('all')}
              className={cn('w-full text-left px-3 py-2 rounded-xl font-medium transition flex items-center justify-between', activeGroupFilter === 'all' ? 'bg-slate-900 text-white dark:bg-white dark:text-slate-900 font-bold' : 'text-slate-600 dark:text-slate-400 hover:bg-slate-50 dark:hover:bg-slate-800')}
            >
              <span>Tất cả bạn bè</span>
              <span className="font-mono text-[11px]">48</span>
            </button>
            <button
              type="button"
              onClick={() => setActiveGroupFilter('fav')}
              className={cn('w-full text-left px-3 py-2 rounded-xl font-medium transition flex items-center justify-between', activeGroupFilter === 'fav' ? 'bg-amber-500 text-white font-bold' : 'text-slate-600 dark:text-slate-400 hover:bg-slate-50 dark:hover:bg-slate-800')}
            >
              <span>Được yêu thích ⭐</span>
              <span className="font-mono text-[11px]">8</span>
            </button>
            <button
              type="button"
              onClick={() => setActiveGroupFilter('split')}
              className={cn('w-full text-left px-3 py-2 rounded-xl font-medium transition flex items-center justify-between', activeGroupFilter === 'split' ? 'bg-emerald-600 text-white font-bold' : 'text-slate-600 dark:text-slate-400 hover:bg-slate-50 dark:hover:bg-slate-800')}
            >
              <span>Đang chia tiền</span>
              <span className="font-mono text-[11px]">3</span>
            </button>
          </div>

          <div className="p-4 rounded-2xl bg-slate-50 dark:bg-slate-800/40 border border-slate-200/70 dark:border-slate-800 text-xs space-y-2">
            <div className="flex items-center gap-1.5 font-bold text-slate-900 dark:text-white">
              <ShieldCheck className="h-4 w-4 text-emerald-600" />
              <span>Đồng bộ VietQR & SePay</span>
            </div>
            <p className="text-[11px] text-slate-500 dark:text-slate-400 leading-relaxed">
              Tự động khớp lệnh chuyển tiền & cập nhật dư nợ bạn bè theo thời gian thực.
            </p>
          </div>
        </div>

        {/* Column 2 (6 cols): Compact Table Directory */}
        <div className="lg:col-span-6 space-y-3">
          <div className="p-3 bg-white dark:bg-slate-900 rounded-2xl border border-slate-200/80 dark:border-slate-800 shadow-xs flex items-center gap-2">
            <Search className="h-4 w-4 text-slate-400 shrink-0 ml-1" />
            <input
              type="text"
              placeholder="Lọc nhanh danh bạ bạn bè..."
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              className="w-full bg-transparent text-xs outline-none"
            />
          </div>

          <div className="bg-white dark:bg-slate-900 rounded-2xl border border-slate-200/80 dark:border-slate-800 shadow-xs overflow-hidden">
            <div className="divide-y divide-slate-100 dark:divide-slate-800 text-xs">
              {filteredFriends.map((f) => (
                <div key={f.id} className="p-3.5 flex items-center justify-between gap-3 hover:bg-slate-50/60 dark:hover:bg-slate-800/30 transition">
                  <div className="flex items-center gap-3 min-w-0">
                    <div className="h-8 w-8 rounded-full bg-slate-100 dark:bg-slate-800 text-slate-700 dark:text-slate-300 flex items-center justify-center font-bold text-xs shrink-0">
                      {f.name.substring(0, 2).toUpperCase()}
                    </div>
                    <div className="min-w-0">
                      <div className="flex items-center gap-1.5">
                        <span className="font-bold text-slate-900 dark:text-white truncate">{f.name}</span>
                        {f.isFav && <Star className="h-2.5 w-2.5 fill-amber-400 text-amber-400 shrink-0" />}
                        <span className="text-[9px] px-1.5 py-0.2 rounded bg-slate-100 dark:bg-slate-800 text-slate-500">{f.tag}</span>
                      </div>
                      <p className="text-[10px] text-slate-400 font-mono mt-0.5">{f.phone} • {f.bank}</p>
                    </div>
                  </div>

                  <div className="flex items-center gap-2 shrink-0">
                    {f.splitAmt > 0 && (
                      <span className="text-[11px] font-mono font-bold text-emerald-600 dark:text-emerald-400">
                        +{f.splitAmt.toLocaleString('vi-VN')} ₫
                      </span>
                    )}
                    <button
                      type="button"
                      onClick={() => onOpenPopup?.('transfer-review-modal')}
                      className="p-1.5 rounded-lg bg-emerald-50 dark:bg-emerald-950/60 text-emerald-700 dark:text-emerald-300 font-semibold text-[11px] hover:bg-emerald-100"
                      title="Chuyển tiền nhanh"
                    >
                      <ArrowUpRight className="h-3.5 w-3.5" />
                    </button>
                    <button
                      type="button"
                      onClick={() => onOpenPopup?.('friend-edit-nickname', 'prefill')}
                      className="p-1.5 rounded-lg text-slate-400 hover:text-slate-700 dark:hover:text-slate-200"
                      title="Sửa biệt danh"
                    >
                      Sửa
                    </button>
                  </div>
                </div>
              ))}
            </div>
          </div>
        </div>

        {/* Column 3 (3 cols): Connection Requests & Quick Add */}
        <div className="lg:col-span-3 space-y-4">
          <div className="p-4 rounded-2xl bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 shadow-xs space-y-3 text-xs">
            <h3 className="font-bold uppercase tracking-wider text-slate-700 dark:text-slate-300 flex items-center justify-between">
              <span>Lời mời mới (2)</span>
              <span className="h-2 w-2 rounded-full bg-emerald-500" />
            </h3>

            <div className="space-y-2">
              <div className="p-2.5 rounded-xl bg-slate-50 dark:bg-slate-800/50 space-y-2 border border-slate-100 dark:border-slate-800">
                <div>
                  <p className="font-bold text-slate-900 dark:text-white">Vũ Thùy Linh</p>
                  <p className="text-[10px] text-slate-400">0977***654</p>
                </div>
                <div className="flex gap-1.5 pt-1">
                  <button
                    type="button"
                    onClick={() => onOpenPopup?.('friend-accept')}
                    className="flex-1 py-1 rounded bg-emerald-600 text-white font-bold text-[10px]"
                  >
                    Đồng ý
                  </button>
                  <button
                    type="button"
                    onClick={() => onOpenPopup?.('friend-reject')}
                    className="flex-1 py-1 rounded border border-slate-200 text-slate-500 font-semibold text-[10px]"
                  >
                    Bỏ qua
                  </button>
                </div>
              </div>
            </div>
          </div>

          <div className="p-4 rounded-2xl bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 shadow-xs space-y-2.5 text-xs">
            <h3 className="font-bold uppercase tracking-wider text-slate-700 dark:text-slate-300">
              Kết nối bạn bè nhanh
            </h3>
            <button
              type="button"
              onClick={() => onOpenPopup?.('friend-add')}
              className="w-full py-2 rounded-xl bg-slate-900 dark:bg-white text-white dark:text-slate-900 font-bold text-xs"
            >
              + Nhập mã / Quét QR
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};
