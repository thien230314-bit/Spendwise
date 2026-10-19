import React, { useState } from 'react';
import { cn } from '../../../utils/cn';
import {
  Search,
  UserPlus,
  Users,
  Check,
  X,
  Clock,
  ShieldCheck,
  UserMinus,
  Sparkles,
  Phone,
  Mail,
  Loader2,
} from 'lucide-react';

interface MobileFriendsScreenProps {
  theme?: 'light' | 'dark';
  onOpenSheet?: (sheetId: string) => void;
  statusMode?: 'normal' | 'skeleton' | 'loading' | 'error';
}

export const MobileFriendsScreen: React.FC<MobileFriendsScreenProps> = ({
  theme = 'light',
  onOpenSheet,
  statusMode = 'normal',
}) => {
  const isDark = theme === 'dark';
  const isSkeleton = statusMode === 'skeleton';
  const [activeTab, setActiveTab] = useState<'friends' | 'incoming' | 'outgoing'>('friends');
  const [searchContact, setSearchContact] = useState('');
  const [searchLoading, setSearchLoading] = useState(false);
  const [searchResult, setSearchResult] = useState<{
    fullName: string;
    spendId: string;
    phone: string;
    state: 'NONE' | 'FRIENDS' | 'REQUEST_SENT';
  } | null>(null);

  const handleSearch = () => {
    if (!searchContact.trim()) return;
    setSearchLoading(true);
    setSearchResult(null);
    setTimeout(() => {
      setSearchLoading(false);
      setSearchResult({
        fullName: 'Lê Hoàng Nam',
        spendId: 'SW-55129',
        phone: '0912345678',
        state: 'NONE',
      });
    }, 400);
  };

  return (
    <div
      className={cn(
        'flex-1 flex flex-col p-4 space-y-4 overflow-y-auto transition-colors pb-6',
        isDark ? 'bg-[#090D16]' : 'bg-[#F8FAFC]'
      )}
    >
      {/* 1. Real Friends Search Bar */}
      <div
        className={cn(
          'p-4 rounded-3xl border shadow-sm space-y-3 transition-all',
          isDark ? 'bg-[#101622] border-[#1E293B]' : 'bg-[#FCFBF7] border-[#E2E8F0]'
        )}
      >
        <span className="text-xs font-bold text-slate-800 dark:text-slate-200 block">
          Tìm kiếm & Kết bạn mới
        </span>

        <div className="flex gap-2">
          <div
            className={cn(
              'flex-1 flex items-center gap-2 px-3 py-2.5 rounded-2xl border text-xs',
              isDark
                ? 'bg-[#151B22] border-[#1E293B] text-white'
                : 'bg-white border-[#E2E8F0] text-slate-900'
            )}
          >
            <Search className="w-4 h-4 text-slate-400 shrink-0" />
            <input
              type="text"
              value={searchContact}
              onChange={(e) => setSearchContact(e.target.value)}
              onKeyDown={(e) => e.key === 'Enter' && handleSearch()}
              placeholder="Nhập SĐT hoặc SpendId (VD: SW-12345)"
              className="w-full bg-transparent outline-none text-xs"
            />
          </div>

          <button
            type="button"
            onClick={handleSearch}
            disabled={searchLoading || !searchContact.trim()}
            className={cn(
              'px-4 rounded-2xl bg-emerald-600 hover:bg-emerald-700 text-white text-xs font-bold transition-all cursor-pointer flex items-center justify-center',
              (searchLoading || !searchContact.trim()) && 'opacity-60 cursor-not-allowed'
            )}
          >
            {searchLoading ? <Loader2 className="w-4 h-4 animate-spin" /> : <span>Tìm</span>}
          </button>
        </div>

        {/* Found Result Preview Card */}
        {searchResult && (
          <div
            className={cn(
              'p-3.5 rounded-2xl border flex items-center justify-between gap-3 animate-in fade-in',
              isDark ? 'bg-[#151B22] border-[#1E293B]' : 'bg-white border-[#E2E8F0]'
            )}
          >
            <div className="flex items-center gap-2.5">
              <div className="w-9 h-9 rounded-xl bg-emerald-100 dark:bg-emerald-950 text-emerald-600 dark:text-emerald-400 font-bold flex items-center justify-center text-xs">
                LH
              </div>
              <div>
                <span className="text-xs font-bold text-slate-800 dark:text-slate-200 block">
                  {searchResult.fullName}
                </span>
                <span className="text-[10px] text-slate-400 font-mono">
                  {searchResult.spendId} • {searchResult.phone}
                </span>
              </div>
            </div>

            {searchResult.state === 'REQUEST_SENT' ? (
              <span className="text-[11px] text-slate-400 font-semibold">Đã gửi lời mời</span>
            ) : (
              <button
                type="button"
                onClick={() => setSearchResult({ ...searchResult, state: 'REQUEST_SENT' })}
                className="py-1.5 px-3 rounded-xl bg-emerald-600 hover:bg-emerald-700 text-white text-xs font-bold transition-all cursor-pointer"
              >
                + Kết bạn
              </button>
            )}
          </div>
        )}
      </div>

      {/* 2. Tabs: Bạn bè / Lời mời / Đã gửi */}
      <div
        className={cn(
          'flex p-1 rounded-xl border text-xs font-semibold shrink-0',
          isDark ? 'bg-[#101622] border-[#1E293B]' : 'bg-[#FCFBF7] border-[#E2E8F0]'
        )}
      >
        {[
          { id: 'friends', label: 'Bạn bè (4)' },
          { id: 'incoming', label: 'Lời mời (2)' },
          { id: 'outgoing', label: 'Đã gửi (1)' },
        ].map((tab) => {
          const isActive = activeTab === tab.id;
          return (
            <button
              key={tab.id}
              type="button"
              onClick={() => setActiveTab(tab.id as any)}
              className={cn(
                'flex-1 py-1.5 rounded-lg text-center transition-all cursor-pointer',
                isActive
                  ? isDark
                    ? 'bg-[#1E293B] text-emerald-400 shadow-xs font-bold'
                    : 'bg-white text-emerald-700 shadow-xs font-bold'
                  : 'text-slate-400 hover:text-slate-600'
              )}
            >
              {tab.label}
            </button>
          );
        })}
      </div>

      {/* 3. List Content based on activeTab */}
      <div
        className={cn(
          'p-4 rounded-3xl border shadow-sm space-y-3 transition-all',
          isDark ? 'bg-[#101622] border-[#1E293B]' : 'bg-[#FCFBF7] border-[#E2E8F0]'
        )}
      >
        {activeTab === 'friends' && (
          <div className="divide-y divide-slate-100 dark:divide-slate-800/80">
            {[
              { id: '1', name: 'Trần Thị Bình', spendId: 'SW-88412', phone: '0987654321', isFav: true },
              { id: '2', name: 'Vũ Quốc Huy', spendId: 'SW-34190', phone: '0901234567', isFav: false },
              { id: '3', name: 'Đặng Mai Lan', spendId: 'SW-77215', phone: '0938889999', isFav: true },
              { id: '4', name: 'Phạm Minh Tuấn', spendId: 'SW-11234', phone: '0977665544', isFav: false },
            ].map((f) => (
              <div key={f.id} className="py-2.5 flex items-center justify-between gap-3">
                <div className="flex items-center gap-2.5">
                  <div className="w-8 h-8 rounded-full bg-slate-100 dark:bg-slate-800 text-slate-700 dark:text-slate-300 font-bold flex items-center justify-center text-xs">
                    {f.name.slice(0, 2).toUpperCase()}
                  </div>
                  <div>
                    <span className="text-xs font-bold text-slate-800 dark:text-slate-200 block">
                      {f.name}
                    </span>
                    <span className="text-[10px] text-slate-400 font-mono">
                      {f.spendId}
                    </span>
                  </div>
                </div>

                <div className="flex items-center gap-2">
                  <button
                    type="button"
                    onClick={() => onOpenSheet?.('transfer')}
                    className="py-1 px-2.5 rounded-lg border border-emerald-500/30 text-emerald-600 dark:text-emerald-400 hover:bg-emerald-50 dark:hover:bg-emerald-950/40 text-[11px] font-semibold cursor-pointer"
                  >
                    Chuyển tiền
                  </button>
                  <button
                    type="button"
                    className="p-1 text-slate-400 hover:text-rose-500 cursor-pointer"
                    title="Hủy kết bạn"
                  >
                    <UserMinus className="w-3.5 h-3.5" />
                  </button>
                </div>
              </div>
            ))}
          </div>
        )}

        {activeTab === 'incoming' && (
          <div className="divide-y divide-slate-100 dark:divide-slate-800/80">
            {[
              { id: 'req-1', name: 'Ngô Thanh Tùng', spendId: 'SW-66231', msg: 'Chào An, mình là Tùng ở phòng Kế toán.' },
              { id: 'req-2', name: 'Hoàng Kim Yến', spendId: 'SW-90234', msg: 'Kết bạn với mình nhé!' },
            ].map((req) => (
              <div key={req.id} className="py-2.5 flex items-center justify-between gap-3">
                <div>
                  <span className="text-xs font-bold text-slate-800 dark:text-slate-200 block">
                    {req.name}
                  </span>
                  <span className="text-[10px] text-slate-400 font-mono block">
                    {req.spendId}
                  </span>
                  <p className="text-[11px] text-slate-500 dark:text-slate-400 mt-0.5">{req.msg}</p>
                </div>

                <div className="flex items-center gap-1.5 shrink-0">
                  <button
                    type="button"
                    className="py-1 px-2.5 rounded-lg bg-emerald-600 hover:bg-emerald-700 text-white text-[11px] font-bold cursor-pointer"
                  >
                    Đồng ý
                  </button>
                  <button
                    type="button"
                    className="py-1 px-2 rounded-lg border border-slate-200 dark:border-slate-800 text-slate-400 hover:text-slate-600 text-[11px] font-semibold cursor-pointer"
                  >
                    Từ chối
                  </button>
                </div>
              </div>
            ))}
          </div>
        )}

        {activeTab === 'outgoing' && (
          <div className="divide-y divide-slate-100 dark:divide-slate-800/80">
            {[
              { id: 'out-1', name: 'Bùi Gia Huy', spendId: 'SW-12845', time: '1 ngày trước' },
            ].map((req) => (
              <div key={req.id} className="py-2.5 flex items-center justify-between gap-3">
                <div>
                  <span className="text-xs font-bold text-slate-800 dark:text-slate-200 block">
                    {req.name}
                  </span>
                  <span className="text-[10px] text-slate-400 font-mono">
                    {req.spendId} • {req.time}
                  </span>
                </div>
                <button
                  type="button"
                  className="py-1 px-2.5 rounded-lg border border-rose-200 dark:border-rose-900/50 text-rose-500 text-[11px] font-semibold cursor-pointer"
                >
                  Thu hồi
                </button>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
};
