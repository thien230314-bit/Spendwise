import React, { useState } from 'react';
import { cn } from '../../../utils/cn';
import {
  Wallet,
  Building2,
  Search,
  Plus,
  ArrowDownLeft,
  ArrowUpRight,
  ShieldCheck,
  Tag,
  CheckCircle2,
} from 'lucide-react';
import { formatMoney } from '../../../utils/money';

interface MobileWalletScreenProps {
  theme?: 'light' | 'dark';
  onOpenSheet?: (sheetId: string) => void;
  statusMode?: 'normal' | 'skeleton' | 'loading' | 'error';
}

export const MobileWalletScreen: React.FC<MobileWalletScreenProps> = ({
  theme = 'light',
  onOpenSheet,
  statusMode = 'normal',
}) => {
  const isDark = theme === 'dark';
  const isSkeleton = statusMode === 'skeleton';
  const [filter, setFilter] = useState<'all' | 'income' | 'expense'>('all');
  const [searchQuery, setSearchQuery] = useState('');

  return (
    <div
      className={cn(
        'flex-1 flex flex-col p-4 space-y-4 overflow-y-auto transition-colors pb-6',
        isDark ? 'bg-[#090D16]' : 'bg-[#F8FAFC]'
      )}
    >
      {/* 1. Internal SpendWise Wallet Card (Obsidian Emerald Theme matching Stitch V2) */}
      <div
        className={cn(
          'p-5 rounded-3xl border shadow-sm space-y-3.5 transition-all text-white',
          'bg-[#064E3B] border-[#047857]'
        )}
      >
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <Wallet className="w-4 h-4 text-emerald-300" />
            <span className="text-xs font-bold text-white">
              Ví SpendWise
            </span>
            <span className="text-[10px] px-2 py-0.5 rounded-full bg-emerald-950/60 text-emerald-300 border border-emerald-500/30 font-semibold">
              Nội bộ
            </span>
          </div>
          <button
            type="button"
            onClick={() => onOpenSheet?.('add-wallet')}
            className="text-[11px] font-semibold text-emerald-300 hover:underline cursor-pointer"
          >
            + Tạo ví mới
          </button>
        </div>

        {/* Selected Wallet Switcher */}
        <div
          className={cn(
            'p-3 rounded-2xl border flex items-center justify-between gap-3 cursor-pointer',
            'bg-black/25 border-emerald-500/30'
          )}
          onClick={() => onOpenSheet?.('wallet-switcher')}
        >
          <div>
            <span className="text-xs font-bold text-white block">
              Ví Chi tiêu Cá nhân (Mặc định)
            </span>
            <span className="text-[11px] text-emerald-200/80 font-mono">
              SW-98234-01
            </span>
          </div>
          <span className="text-xs font-semibold text-emerald-200">Đổi ví ▾</span>
        </div>

        {/* Balance */}
        <div>
          <span className="text-[11px] text-emerald-200/80 font-medium block">
            Số dư khả dụng
          </span>
          {isSkeleton ? (
            <div className="h-8 w-40 bg-emerald-900/50 rounded animate-pulse mt-1" />
          ) : (
            <span className="text-2xl font-extrabold font-mono text-white tabular-nums">
              {formatMoney(14250000, 'vi-VN')}
            </span>
          )}
        </div>
      </div>

      {/* 2. Linked Bank Account Card (SePay) */}
      <div
        className={cn(
          'p-4 rounded-3xl border shadow-sm space-y-3 transition-all',
          isDark
            ? 'bg-[#101622] border-[#1E293B]'
            : 'bg-[#FCFBF7] border-[#E2E8F0]'
        )}
      >
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <Building2 className="w-4 h-4 text-emerald-500" />
            <span className="text-xs font-bold text-slate-800 dark:text-slate-200">
              Ngân hàng liên kết
            </span>
            <span className="text-[10px] px-2 py-0.5 rounded-full bg-slate-100 dark:bg-slate-800 text-slate-500 font-medium font-mono">
              SePay
            </span>
          </div>
          <button
            type="button"
            onClick={() => onOpenSheet?.('link-bank')}
            className="text-[11px] font-semibold text-emerald-600 dark:text-emerald-400 hover:underline cursor-pointer"
          >
            + Liên kết
          </button>
        </div>

        <div
          className={cn(
            'p-3 rounded-2xl border flex items-center justify-between gap-3',
            isDark ? 'bg-[#151B22] border-[#1E293B]' : 'bg-white border-[#E2E8F0]'
          )}
        >
          <div className="flex items-center gap-2.5">
            <div className="w-8 h-8 rounded-xl bg-slate-900 text-white font-bold flex items-center justify-center text-xs">
              VCB
            </div>
            <div>
              <span className="text-xs font-bold text-slate-800 dark:text-slate-200 block">
                Vietcombank • 9832
              </span>
              <span className="text-[10px] text-slate-400 font-mono">
                Nguyen Van An
              </span>
            </div>
          </div>
          <span className="text-[10px] px-2 py-0.5 rounded-full bg-emerald-100 dark:bg-emerald-950 text-emerald-700 dark:text-emerald-400 font-semibold">
            Đang liên kết
          </span>
        </div>
      </div>

      {/* 3. Search & Filter Bar */}
      <div className="space-y-2">
        <div
          className={cn(
            'flex items-center gap-2 px-3 py-2.5 rounded-2xl border text-xs',
            isDark ? 'bg-[#101622] border-[#1E293B] text-white' : 'bg-white border-[#E2E8F0] text-slate-900'
          )}
        >
          <Search className="w-4 h-4 text-slate-400 shrink-0" />
          <input
            type="text"
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            placeholder="Tìm kiếm giao dịch, danh mục…"
            className="w-full bg-transparent outline-none text-xs"
          />
        </div>

        {/* Direction Segments */}
        <div
          className={cn(
            'flex p-1 rounded-xl border text-xs font-semibold',
            isDark ? 'bg-[#101622] border-[#1E293B]' : 'bg-[#FCFBF7] border-[#E2E8F0]'
          )}
        >
          {(['all', 'income', 'expense'] as const).map((key) => {
            const labels = { all: 'Tất cả', income: 'Thu nhập', expense: 'Chi tiêu' };
            const isActive = filter === key;
            return (
              <button
                key={key}
                type="button"
                onClick={() => setFilter(key)}
                className={cn(
                  'flex-1 py-1.5 rounded-lg text-center transition-all cursor-pointer',
                  isActive
                    ? isDark
                      ? 'bg-[#1E293B] text-emerald-400 shadow-xs'
                      : 'bg-white text-emerald-700 shadow-xs font-bold'
                    : 'text-slate-400 hover:text-slate-600'
                )}
              >
                {labels[key]}
              </button>
            );
          })}
        </div>
      </div>

      {/* 4. Transactions List */}
      <div
        className={cn(
          'p-4 rounded-3xl border shadow-sm space-y-3 transition-all',
          isDark
            ? 'bg-[#101622] border-[#1E293B]'
            : 'bg-[#FCFBF7] border-[#E2E8F0]'
        )}
      >
        <span className="text-xs font-bold text-slate-800 dark:text-slate-200 block">
          Lịch sử giao dịch ({filter === 'all' ? 'Tất cả' : filter === 'income' ? 'Thu nhập' : 'Chi tiêu'})
        </span>

        <div className="divide-y divide-slate-100 dark:divide-slate-800/80">
          {[
            { id: '1', title: 'Siêu thị WinMart', cat: 'Ăn uống', amount: -245000, type: 'expense', date: '14:20' },
            { id: '2', title: 'Lương tháng 10 TechCorp', cat: 'Tiền lương', amount: 28500000, type: 'income', date: 'Hôm qua' },
            { id: '3', title: 'Xăng xe Petrolimex', cat: 'Đi lại', amount: -110000, type: 'expense', date: '11/10' },
            { id: '4', title: 'Chuyển tiền cho Mẹ', cat: 'Gia đình', amount: -3000000, type: 'expense', date: '10/10' },
          ]
            .filter((tx) => (filter === 'all' ? true : tx.type === filter))
            .map((tx) => (
              <div key={tx.id} className="py-2.5 flex items-center justify-between gap-3">
                <div className="flex items-center gap-2.5">
                  <div
                    className={cn(
                      'w-8 h-8 rounded-full flex items-center justify-center font-bold text-xs shrink-0',
                      tx.type === 'income'
                        ? 'bg-emerald-100 text-emerald-600 dark:bg-emerald-950/60 dark:text-emerald-400'
                        : 'bg-slate-100 text-slate-600 dark:bg-slate-800 dark:text-slate-400'
                    )}
                  >
                    {tx.type === 'income' ? <ArrowDownLeft className="w-4 h-4" /> : <ArrowUpRight className="w-4 h-4" />}
                  </div>
                  <div>
                    <span className="text-xs font-semibold text-slate-800 dark:text-slate-200 block">
                      {tx.title}
                    </span>
                    <div className="flex items-center gap-1.5 text-[10px] text-slate-400 font-mono">
                      <span>{tx.cat}</span>
                      <span>•</span>
                      <span>{tx.date}</span>
                    </div>
                  </div>
                </div>

                <span
                  className={cn(
                    'text-xs font-bold font-mono tabular-nums',
                    tx.type === 'income'
                      ? 'text-emerald-600 dark:text-emerald-400'
                      : 'text-slate-800 dark:text-slate-200'
                  )}
                >
                  {tx.type === 'income' ? '+' : '−'}
                  {formatMoney(Math.abs(tx.amount), 'vi-VN')}
                </span>
              </div>
            ))}
        </div>
      </div>
    </div>
  );
};
