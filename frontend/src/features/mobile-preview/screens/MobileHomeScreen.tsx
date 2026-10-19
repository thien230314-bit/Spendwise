import React, { useState } from 'react';
import { cn } from '../../../utils/cn';
import {
  Bell,
  SlidersHorizontal,
  Search,
  Eye,
  EyeOff,
  TrendingUp,
  TrendingDown,
  ArrowRightLeft,
  Users,
  ShieldCheck,
  ArrowUp,
  ArrowDown,
  PieChart,
  PiggyBank,
  AlertTriangle,
  CalendarClock,
  Utensils,
  Plane,
  Laptop,
  ArrowRight,
  ChevronRight,
  Coffee,
  ShoppingBag,
  Briefcase,
} from 'lucide-react';
import { formatMoney } from '../../../utils/money';

interface MobileHomeScreenProps {
  theme?: 'light' | 'dark';
  onNavigateTab?: (tab: 'home' | 'wallet' | 'transfer' | 'planning' | 'menu') => void;
  onOpenSheet?: (sheetId: string) => void;
  statusMode?: 'normal' | 'skeleton' | 'loading' | 'error';
}

export const MobileHomeScreen: React.FC<MobileHomeScreenProps> = ({
  theme = 'light',
  onNavigateTab,
  onOpenSheet,
  statusMode = 'normal',
}) => {
  const isDark = theme === 'dark';
  const isSkeleton = statusMode === 'skeleton';
  const [isBalanceHidden, setIsBalanceHidden] = useState(false);
  const [searchQuery, setSearchQuery] = useState('');
  const [chartViewMode, setChartViewMode] = useState<'all' | 'income' | 'expense'>('all');
  const [donutMode, setDonutMode] = useState<'expense' | 'income'>('expense');

  // Realistic mock data matching Google Stitch V2 Design Spec
  const totalBalance = 14250000;
  const monthIncome = 42500000;
  const monthExpense = 18250000;
  const budgetRemaining = 6750000;
  const netCashFlow = monthIncome - monthExpense; // +24.250.000 ₫

  // 6-Month Historical Flow
  const monthlyBarData = [
    { month: 'T5', incH: '50%', expH: '35%' },
    { month: 'T6', incH: '60%', expH: '40%' },
    { month: 'T7', incH: '70%', expH: '50%' },
    { month: 'T8', incH: '55%', expH: '45%' },
    { month: 'T9', incH: '75%', expH: '55%' },
    { month: 'T10', incH: '80%', expH: '50%', isCurrent: true },
  ];

  // Category Breakdown
  const categorySlices = [
    { name: 'Ăn uống', pct: 42, amount: 7665000, color: '#047857' },
    { name: 'Mua sắm', pct: 28, amount: 5110000, color: '#34D399' },
    { name: 'Nhà cửa', pct: 18, amount: 3285000, color: '#6366F1' },
    { name: 'Khác', pct: 12, amount: 2190000, color: '#94A3B8' },
  ];

  return (
    <div
      id="mobile-home-screen-scroll-container"
      className={cn(
        'flex-1 flex flex-col overflow-y-auto no-scrollbar font-sans select-none pb-20 transition-colors',
        isDark ? 'bg-[#0B111E] text-slate-100' : 'bg-[#F4F7F6] text-slate-900'
      )}
    >
      {/* ========================================================
          BLOCK 01: STICKY HEADER & DEDICATED SEARCH BAR
          ======================================================== */}
      <header
        className={cn(
          'sticky top-0 z-30 px-4 pt-3 pb-3 flex flex-col gap-3 backdrop-blur-md border-b transition-colors',
          isDark
            ? 'bg-[#0B111E]/95 border-slate-800'
            : 'bg-[#F4F7F6]/95 border-slate-200/60'
        )}
      >
        {/* Header Top Row */}
        <div className="flex justify-between items-center w-full">
          <div className="flex items-center gap-3">
            <div className="relative w-11 h-11 rounded-full overflow-hidden border-2 border-emerald-600/30 shadow-xs shrink-0">
              <img
                src="https://images.unsplash.com/photo-1534528741775-53994a69daeb?auto=format&fit=crop&w=150&q=80"
                alt="Portrait of Lan Anh"
                className="w-full h-full object-cover"
              />
              <div className="absolute bottom-0 right-0 w-3 h-3 bg-emerald-500 rounded-full border-2 border-white dark:border-slate-900" />
            </div>
            <div>
              <div className="flex items-center gap-1.5">
                <span className="text-[11px] font-bold uppercase tracking-wider text-emerald-600 dark:text-emerald-400">
                  SpendWise
                </span>
                <span className="inline-block w-1 h-1 rounded-full bg-emerald-600/40" />
                <span className="text-[11px] text-slate-400 font-medium">Fintech</span>
              </div>
              <h1 className="text-base font-bold leading-tight">Xin chào, Lan Anh 👋</h1>
            </div>
          </div>

          <div className="flex items-center gap-2">
            {/* Notification Button */}
            <button
              type="button"
              onClick={() => onOpenSheet?.('notifications')}
              className={cn(
                'relative w-10 h-10 rounded-full flex items-center justify-center border shadow-xs transition-colors active:scale-95 cursor-pointer',
                isDark
                  ? 'bg-slate-800 border-slate-700 text-slate-200 hover:text-emerald-400'
                  : 'bg-white border-slate-200/80 text-slate-700 hover:text-emerald-700'
              )}
              title="Thông báo"
            >
              <Bell className="w-5 h-5" />
              <span className="absolute top-2 right-2 w-2 h-2 rounded-full bg-rose-500 ring-2 ring-white dark:ring-slate-900" />
            </button>

            {/* Utility Filter Button */}
            <button
              type="button"
              onClick={() => onOpenSheet?.('filter')}
              className={cn(
                'w-10 h-10 rounded-full flex items-center justify-center border shadow-xs transition-colors active:scale-95 cursor-pointer',
                isDark
                  ? 'bg-slate-800 border-slate-700 text-slate-200 hover:text-emerald-400'
                  : 'bg-white border-slate-200/80 text-slate-700 hover:text-emerald-700'
              )}
              title="Cài đặt nhanh"
            >
              <SlidersHorizontal className="w-4 h-4" />
            </button>
          </div>
        </div>

        {/* Dedicated Full-Width Search Bar */}
        <div className="flex items-center gap-2">
          <div
            className={cn(
              'flex-1 flex items-center gap-2.5 px-3.5 py-2.5 rounded-2xl border shadow-xs transition-colors',
              isDark
                ? 'bg-slate-900 border-slate-800 text-white focus-within:border-emerald-500'
                : 'bg-white border-slate-200/80 text-slate-900 focus-within:border-emerald-600'
            )}
          >
            <Search className="w-4 h-4 text-slate-400 shrink-0" />
            <input
              type="text"
              placeholder="Tìm kiếm bạn bè, giao dịch, ví..."
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              className="w-full bg-transparent border-0 p-0 text-xs placeholder-slate-400 focus:outline-none"
            />
          </div>

          <button
            type="button"
            onClick={() => onOpenSheet?.('filter')}
            className="w-10 h-10 rounded-2xl bg-[#064E3B] text-white flex items-center justify-center shadow-xs shrink-0 hover:bg-emerald-800 transition-colors active:scale-95 cursor-pointer"
            title="Lọc"
          >
            <SlidersHorizontal className="w-4 h-4" />
          </button>
        </div>
      </header>

      {/* Main Screen Content Body */}
      <main className="px-4 pt-2 flex flex-col gap-4">
        {/* ========================================================
            BLOCK 02: OBSIDIAN-EMERALD BALANCE CARD
            ======================================================== */}
        <section className="relative overflow-hidden rounded-2xl bg-gradient-to-br from-[#064E3B] via-[#053d2e] to-[#022c22] text-white p-4 shadow-[0_12px_28px_-6px_rgba(6,78,59,0.35)] border border-emerald-600/30">
          <div className="absolute -right-8 -top-8 w-44 h-44 bg-emerald-400/20 rounded-full blur-3xl pointer-events-none" />
          <div className="absolute -left-8 -bottom-8 w-36 h-36 bg-teal-300/10 rounded-full blur-2xl pointer-events-none" />

          <div className="relative z-10 flex flex-col gap-3">
            <div className="flex justify-between items-center">
              <div className="flex items-center gap-1.5 text-emerald-200">
                <span className="w-2 h-2 rounded-full bg-emerald-400" />
                <span className="text-[11px] font-bold tracking-wider uppercase">
                  VÍ CHÍNH SPENDWISE • TỔNG TÀI SẢN RÒNG
                </span>
              </div>
              <button
                type="button"
                onClick={() => setIsBalanceHidden(!isBalanceHidden)}
                className="w-7 h-7 rounded-full bg-white/10 hover:bg-white/20 flex items-center justify-center text-white/90 transition-colors cursor-pointer"
                title={isBalanceHidden ? 'Hiện số dư' : 'Ẩn số dư'}
              >
                {isBalanceHidden ? <EyeOff className="w-3.5 h-3.5" /> : <Eye className="w-3.5 h-3.5" />}
              </button>
            </div>

            <div>
              {isSkeleton ? (
                <div className="h-9 w-48 bg-white/20 rounded-lg animate-pulse my-1" />
              ) : (
                <div className="text-[32px] leading-tight font-extrabold tracking-tight text-white flex items-baseline gap-1 font-mono">
                  {isBalanceHidden ? '••••••••' : formatMoney(totalBalance, 'vi-VN').replace(' ₫', '')}
                  <span className="text-emerald-300 text-2xl font-bold">₫</span>
                </div>
              )}
              <div className="flex items-center gap-1.5 mt-1 text-xs text-emerald-200 font-medium">
                <TrendingUp className="w-3.5 h-3.5 text-emerald-300" />
                <span>+1.200.000 ₫ tháng này</span>
              </div>
            </div>

            <div className="pt-3 border-t border-white/15 flex justify-between items-center text-xs">
              <div className="flex items-center gap-1.5 bg-white/10 backdrop-blur-sm px-3 py-1.5 rounded-full border border-white/10">
                <span className="w-2 h-2 rounded-full bg-emerald-300 animate-pulse" />
                <span className="font-medium text-emerald-100 text-[11px]">Tỷ lệ tích lũy: 42%</span>
              </div>
              <div className="flex items-center gap-1 text-emerald-200/90 font-medium text-[11px]">
                <ShieldCheck className="w-3.5 h-3.5 text-emerald-300" />
                <span>An toàn tài chính</span>
              </div>
            </div>
          </div>
        </section>

        {/* ========================================================
            BLOCK 03: FOUR QUICK ACTIONS
            [Thu nhập, Chi tiêu, Chuyển tiền, Quỹ nhóm]
            ======================================================== */}
        <section className="grid grid-cols-4 gap-2">
          {/* Action 1: Thu nhập */}
          <button
            type="button"
            onClick={() => onOpenSheet?.('add-transaction')}
            className="flex flex-col items-center gap-1.5 group cursor-pointer"
          >
            <div className="w-13 h-13 rounded-2xl bg-emerald-50 dark:bg-emerald-950/40 border border-emerald-200/70 dark:border-emerald-800/50 text-emerald-700 dark:text-emerald-400 flex items-center justify-center shadow-xs group-hover:bg-emerald-600 group-hover:text-white transition-all active:scale-95">
              <TrendingUp className="w-6 h-6 stroke-[2.2]" />
            </div>
            <span className="text-xs font-semibold text-slate-800 dark:text-slate-200 text-center">Thu nhập</span>
          </button>

          {/* Action 2: Chi tiêu */}
          <button
            type="button"
            onClick={() => onOpenSheet?.('add-transaction')}
            className="flex flex-col items-center gap-1.5 group cursor-pointer"
          >
            <div className="w-13 h-13 rounded-2xl bg-rose-50 dark:bg-rose-950/40 border border-rose-200/70 dark:border-rose-800/50 text-rose-600 dark:text-rose-400 flex items-center justify-center shadow-xs group-hover:bg-rose-600 group-hover:text-white transition-all active:scale-95">
              <TrendingDown className="w-6 h-6 stroke-[2.2]" />
            </div>
            <span className="text-xs font-semibold text-slate-800 dark:text-slate-200 text-center">Chi tiêu</span>
          </button>

          {/* Action 3: Chuyển tiền */}
          <button
            type="button"
            onClick={() => onNavigateTab?.('transfer')}
            className="flex flex-col items-center gap-1.5 group cursor-pointer"
          >
            <div className="w-13 h-13 rounded-2xl bg-teal-50 dark:bg-teal-950/40 border border-teal-200/70 dark:border-teal-800/50 text-teal-700 dark:text-teal-400 flex items-center justify-center shadow-xs group-hover:bg-teal-700 group-hover:text-white transition-all active:scale-95">
              <ArrowRightLeft className="w-5 h-5 stroke-[2.2]" />
            </div>
            <span className="text-xs font-semibold text-slate-800 dark:text-slate-200 text-center">Chuyển tiền</span>
          </button>

          {/* Action 4: Quỹ nhóm */}
          <button
            type="button"
            onClick={() => onNavigateTab?.('menu')}
            className="flex flex-col items-center gap-1.5 group cursor-pointer"
          >
            <div className="w-13 h-13 rounded-2xl bg-indigo-50 dark:bg-indigo-950/40 border border-indigo-200/70 dark:border-indigo-800/50 text-indigo-700 dark:text-indigo-400 flex items-center justify-center shadow-xs group-hover:bg-indigo-700 group-hover:text-white transition-all active:scale-95">
              <Users className="w-5 h-5 stroke-[2.2]" />
            </div>
            <span className="text-xs font-semibold text-slate-800 dark:text-slate-200 text-center">Quỹ nhóm</span>
          </button>
        </section>

        {/* ========================================================
            BLOCK 04: COMPACT FINANCIAL OVERVIEW (2x2 GRID)
            - Thu tháng này: 42.500.000 ₫ (+18%)
            - Chi tháng này: 18.250.000 ₫ (73%)
            - Ngân sách còn lại: 6.750.000 ₫ (progress 27%)
            - Dòng tiền ròng: +24.250.000 ₫ (Dương an toàn)
            ======================================================== */}
        <section className="grid grid-cols-2 gap-3">
          {/* Card 1: Thu tháng này */}
          <div
            className={cn(
              'rounded-2xl p-3.5 shadow-xs border flex flex-col justify-between transition-colors',
              isDark ? 'bg-slate-900 border-slate-800' : 'bg-white border-slate-100'
            )}
          >
            <div className="flex items-center justify-between mb-2">
              <span className="text-xs font-medium text-slate-400">Thu tháng này</span>
              <span className="w-6 h-6 rounded-full bg-emerald-50 dark:bg-emerald-950/50 text-emerald-600 dark:text-emerald-400 flex items-center justify-center">
                <ArrowUp className="w-3.5 h-3.5 stroke-[2.5]" />
              </span>
            </div>
            <div>
              <span className="text-sm font-bold block font-mono text-slate-900 dark:text-white">
                {formatMoney(monthIncome, 'vi-VN')}
              </span>
              <span className="text-[10px] font-semibold text-emerald-700 dark:text-emerald-400 bg-emerald-50 dark:bg-emerald-950/60 px-1.5 py-0.5 rounded mt-1 inline-block">
                +18% kỳ trước
              </span>
            </div>
          </div>

          {/* Card 2: Chi tháng này */}
          <div
            className={cn(
              'rounded-2xl p-3.5 shadow-xs border flex flex-col justify-between transition-colors',
              isDark ? 'bg-slate-900 border-slate-800' : 'bg-white border-slate-100'
            )}
          >
            <div className="flex items-center justify-between mb-2">
              <span className="text-xs font-medium text-slate-400">Chi tháng này</span>
              <span className="w-6 h-6 rounded-full bg-amber-50 dark:bg-amber-950/50 text-amber-600 dark:text-amber-400 flex items-center justify-center">
                <ArrowDown className="w-3.5 h-3.5 stroke-[2.5]" />
              </span>
            </div>
            <div>
              <span className="text-sm font-bold block font-mono text-slate-900 dark:text-white">
                {formatMoney(monthExpense, 'vi-VN')}
              </span>
              <span className="text-[10px] font-semibold text-amber-700 dark:text-amber-400 bg-amber-50 dark:bg-amber-950/60 px-1.5 py-0.5 rounded mt-1 inline-block">
                73% hạn mức
              </span>
            </div>
          </div>

          {/* Card 3: Ngân sách còn lại */}
          <div
            className={cn(
              'rounded-2xl p-3.5 shadow-xs border flex flex-col justify-between transition-colors',
              isDark ? 'bg-slate-900 border-slate-800' : 'bg-white border-slate-100'
            )}
          >
            <div className="flex items-center justify-between mb-2">
              <span className="text-xs font-medium text-slate-400">Ngân sách còn lại</span>
              <span className="w-6 h-6 rounded-full bg-blue-50 dark:bg-blue-950/50 text-blue-600 dark:text-blue-400 flex items-center justify-center">
                <PieChart className="w-3.5 h-3.5" />
              </span>
            </div>
            <div>
              <span className="text-sm font-bold block font-mono text-slate-900 dark:text-white">
                {formatMoney(budgetRemaining, 'vi-VN')}
              </span>
              <div className="w-full bg-slate-100 dark:bg-slate-800 h-1.5 rounded-full mt-2 overflow-hidden">
                <div className="bg-blue-600 h-full rounded-full" style={{ width: '27%' }} />
              </div>
            </div>
          </div>

          {/* Card 4: Dòng tiền ròng */}
          <div
            className={cn(
              'rounded-2xl p-3.5 shadow-xs border flex flex-col justify-between transition-colors',
              isDark
                ? 'bg-gradient-to-br from-emerald-950/40 to-slate-900 border-emerald-800/40'
                : 'bg-gradient-to-br from-emerald-50/40 to-white border-emerald-100/80'
            )}
          >
            <div className="flex items-center justify-between mb-2">
              <span className="text-xs font-semibold text-emerald-800 dark:text-emerald-300">
                Dòng tiền ròng
              </span>
              <span className="w-6 h-6 rounded-full bg-emerald-100 dark:bg-emerald-900/60 text-emerald-800 dark:text-emerald-300 flex items-center justify-center">
                <PiggyBank className="w-3.5 h-3.5" />
              </span>
            </div>
            <div>
              <span className="text-sm font-extrabold text-emerald-700 dark:text-emerald-400 font-mono block">
                +{formatMoney(netCashFlow, 'vi-VN')}
              </span>
              <span className="text-[10px] font-semibold text-emerald-700 dark:text-emerald-400 mt-1 inline-block">
                Dương an toàn
              </span>
            </div>
          </div>
        </section>

        {/* ========================================================
            BLOCK 05: SIX-MONTH CASH FLOW CHART
            ======================================================== */}
        <section
          className={cn(
            'rounded-2xl p-4 shadow-xs border transition-colors',
            isDark ? 'bg-slate-900 border-slate-800' : 'bg-white border-slate-100'
          )}
        >
          <div className="flex justify-between items-start mb-3">
            <div>
              <h3 className="text-xs font-bold leading-tight">Biến động 6 tháng (Tháng 5 - Tháng 10)</h3>
              <p className="text-[11px] text-slate-400">Tháng 5 - Tháng 10, 2026</p>
            </div>
            <span className="bg-emerald-50 dark:bg-emerald-950/60 text-emerald-700 dark:text-emerald-300 text-[10px] font-bold px-2 py-0.5 rounded-full border border-emerald-200 dark:border-emerald-800">
              +12.4% so với kỳ trước
            </span>
          </div>

          {/* View Selector Tabs */}
          <div className="flex gap-1.5 p-1 bg-slate-100 dark:bg-slate-800/80 rounded-xl mb-3">
            <button
              type="button"
              onClick={() => setChartViewMode('all')}
              className={cn(
                'flex-1 py-1 text-[11px] font-bold rounded-lg transition-all cursor-pointer',
                chartViewMode === 'all'
                  ? 'bg-white dark:bg-slate-700 text-emerald-700 dark:text-emerald-300 shadow-2xs'
                  : 'text-slate-500 hover:text-slate-900 dark:hover:text-slate-200'
              )}
            >
              Tất cả
            </button>
            <button
              type="button"
              onClick={() => setChartViewMode('income')}
              className={cn(
                'flex-1 py-1 text-[11px] font-medium rounded-lg transition-all cursor-pointer',
                chartViewMode === 'income'
                  ? 'bg-white dark:bg-slate-700 text-emerald-700 dark:text-emerald-300 shadow-2xs font-bold'
                  : 'text-slate-500 hover:text-slate-900 dark:hover:text-slate-200'
              )}
            >
              Chỉ Thu
            </button>
            <button
              type="button"
              onClick={() => setChartViewMode('expense')}
              className={cn(
                'flex-1 py-1 text-[11px] font-medium rounded-lg transition-all cursor-pointer',
                chartViewMode === 'expense'
                  ? 'bg-white dark:bg-slate-700 text-emerald-700 dark:text-emerald-300 shadow-2xs font-bold'
                  : 'text-slate-500 hover:text-slate-900 dark:hover:text-slate-200'
              )}
            >
              Chỉ Chi
            </button>
          </div>

          {/* Summary Row Metric Bar */}
          <div className="grid grid-cols-2 gap-2 p-2.5 bg-slate-50 dark:bg-slate-800/40 rounded-xl border border-slate-100 dark:border-slate-800 mb-3 text-xs">
            <div>
              <span className="text-slate-400 block text-[10px]">Tổng Thu</span>
              <span className="font-bold text-emerald-700 dark:text-emerald-400 font-mono">
                248.500.000 ₫
              </span>
            </div>
            <div>
              <span className="text-slate-400 block text-[10px]">Tổng Chi</span>
              <span className="font-bold text-slate-800 dark:text-slate-200 font-mono">
                142.100.000 ₫
              </span>
            </div>
          </div>

          {/* Bar Chart Dual Monthly Bars */}
          <div className="h-40 flex items-end justify-between gap-2 pt-2 px-1 border-b border-slate-100 dark:border-slate-800">
            {monthlyBarData.map((bar, idx) => (
              <div key={idx} className="flex flex-col items-center gap-1.5 flex-1">
                <div className="w-full flex items-end justify-center gap-1 h-28">
                  {/* Income bar */}
                  {chartViewMode !== 'expense' && (
                    <div
                      className={cn(
                        'w-2.5 sm:w-3 rounded-t-sm transition-all',
                        bar.isCurrent ? 'bg-[#064E3B] dark:bg-emerald-500 shadow-xs' : 'bg-emerald-600/80 dark:bg-emerald-600'
                      )}
                      style={{ height: bar.incH }}
                    />
                  )}
                  {/* Expense bar */}
                  {chartViewMode !== 'income' && (
                    <div
                      className={cn(
                        'w-2.5 sm:w-3 rounded-t-sm transition-all',
                        bar.isCurrent ? 'bg-emerald-300 dark:bg-emerald-400' : 'bg-emerald-200 dark:bg-emerald-800'
                      )}
                      style={{ height: bar.expH }}
                    />
                  )}
                </div>
                <span
                  className={cn(
                    'text-[10px] font-medium',
                    bar.isCurrent ? 'font-bold text-emerald-600 dark:text-emerald-400' : 'text-slate-400'
                  )}
                >
                  {bar.month}
                </span>
              </div>
            ))}
          </div>

          {/* Chart Legend */}
          <div className="flex justify-center items-center gap-5 mt-3 text-xs text-slate-500">
            <div className="flex items-center gap-1.5">
              <span className="w-2.5 h-2.5 rounded-xs bg-[#047857]" />
              <span className="font-medium text-[11px]">Thu nhập</span>
            </div>
            <div className="flex items-center gap-1.5">
              <span className="w-2.5 h-2.5 rounded-xs bg-emerald-300 dark:bg-emerald-700" />
              <span className="font-medium text-[11px]">Chi tiêu</span>
            </div>
          </div>
        </section>

        {/* ========================================================
            BLOCK 06: CATEGORY DISTRIBUTION DONUT
            ======================================================== */}
        <section
          className={cn(
            'rounded-2xl p-4 shadow-xs border transition-colors',
            isDark ? 'bg-slate-900 border-slate-800' : 'bg-white border-slate-100'
          )}
        >
          <div className="flex justify-between items-center mb-3">
            <h3 className="text-xs font-bold leading-tight">Phân bổ chi tiêu tháng 10</h3>
            <div className="flex p-0.5 bg-slate-100 dark:bg-slate-800 rounded-lg text-[10px]">
              <button
                type="button"
                onClick={() => setDonutMode('expense')}
                className={cn(
                  'px-2.5 py-1 rounded-md transition-all cursor-pointer',
                  donutMode === 'expense'
                    ? 'bg-white dark:bg-slate-700 text-emerald-700 dark:text-emerald-300 font-bold shadow-2xs'
                    : 'text-slate-400 hover:text-slate-600'
                )}
              >
                Chi tiêu
              </button>
              <button
                type="button"
                onClick={() => setDonutMode('income')}
                className={cn(
                  'px-2.5 py-1 rounded-md transition-all cursor-pointer',
                  donutMode === 'income'
                    ? 'bg-white dark:bg-slate-700 text-emerald-700 dark:text-emerald-300 font-bold shadow-2xs'
                    : 'text-slate-400 hover:text-slate-600'
                )}
              >
                Thu nhập
              </button>
            </div>
          </div>

          <div className="flex flex-col sm:flex-row items-center gap-4 pt-1">
            {/* Donut Graphic */}
            <div className="relative w-30 h-30 flex items-center justify-center shrink-0">
              <svg className="w-full h-full -rotate-90" viewBox="0 0 100 100">
                <circle cx="50" cy="50" r="38" fill="none" stroke="#E2E8F0" strokeWidth="12" />
                {/* 42% An uong */}
                <circle
                  cx="50"
                  cy="50"
                  r="38"
                  fill="none"
                  stroke="#047857"
                  strokeWidth="12"
                  strokeDasharray="100.2 138.8"
                  strokeDashoffset="0"
                />
                {/* 28% Mua sam */}
                <circle
                  cx="50"
                  cy="50"
                  r="38"
                  fill="none"
                  stroke="#34D399"
                  strokeWidth="12"
                  strokeDasharray="66.8 172.2"
                  strokeDashoffset="-100.2"
                />
                {/* 18% Nha cua */}
                <circle
                  cx="50"
                  cy="50"
                  r="38"
                  fill="none"
                  stroke="#6366F1"
                  strokeWidth="12"
                  strokeDasharray="43 196"
                  strokeDashoffset="-167"
                />
                {/* 12% Khac */}
                <circle
                  cx="50"
                  cy="50"
                  r="38"
                  fill="none"
                  stroke="#94A3B8"
                  strokeWidth="12"
                  strokeDasharray="28.6 210.4"
                  strokeDashoffset="-210"
                />
              </svg>
              <div className="text-center absolute inset-0 flex flex-col items-center justify-center pointer-events-none">
                <span className="text-[9px] uppercase font-bold text-slate-400 block tracking-wider">
                  Tổng chi
                </span>
                <span className="text-xs font-extrabold font-mono text-slate-900 dark:text-white">
                  18.25M
                </span>
              </div>
            </div>

            {/* Category Progress Bars */}
            <div className="flex flex-col gap-2.5 w-full">
              {categorySlices.map((item, idx) => (
                <div key={idx}>
                  <div className="flex justify-between items-center text-xs">
                    <div className="flex items-center gap-2">
                      <span className="w-2.5 h-2.5 rounded-full" style={{ backgroundColor: item.color }} />
                      <span className="font-semibold text-slate-800 dark:text-slate-200 text-[11px]">
                        {item.name}
                      </span>
                      <span className="text-[10px] font-bold text-slate-400">{item.pct}%</span>
                    </div>
                    <span className="font-bold text-slate-900 dark:text-white font-mono text-[11px]">
                      {formatMoney(item.amount, 'vi-VN')}
                    </span>
                  </div>
                  <div className="w-full bg-slate-100 dark:bg-slate-800 h-1.5 rounded-full overflow-hidden mt-1">
                    <div
                      className="h-full rounded-full"
                      style={{ width: `${item.pct}%`, backgroundColor: item.color }}
                    />
                  </div>
                </div>
              ))}
            </div>
          </div>
        </section>

        {/* ========================================================
            BLOCK 07: FINANCIAL ATTENTION (CẦN CHÚ Ý)
            ======================================================== */}
        <section className="flex flex-col gap-2.5">
          <div className="flex items-center gap-1.5">
            <AlertTriangle className="w-4 h-4 text-amber-500 fill-amber-500" />
            <h3 className="text-xs font-bold leading-tight">Cần chú ý</h3>
            <span className="w-2 h-2 rounded-full bg-amber-500 animate-ping" />
          </div>

          {/* Alert 1 */}
          <div className="bg-amber-50/80 dark:bg-amber-950/30 border border-amber-200/90 dark:border-amber-800/40 rounded-2xl p-3.5 flex flex-col gap-2 shadow-2xs">
            <div className="flex items-start justify-between gap-2">
              <div className="flex items-start gap-2.5">
                <div className="w-8 h-8 rounded-full bg-amber-100 dark:bg-amber-900/50 text-amber-700 dark:text-amber-300 flex items-center justify-center shrink-0 mt-0.5">
                  <Utensils className="w-4 h-4" />
                </div>
                <div>
                  <h4 className="text-xs font-bold text-amber-900 dark:text-amber-200 leading-snug">
                    Ngân sách Ăn uống đã chạm 84% hạn mức
                  </h4>
                  <p className="text-[11px] text-amber-700 dark:text-amber-400 mt-0.5">
                    Còn lại 1.260.000 ₫ cho 12 ngày còn lại của tháng
                  </p>
                </div>
              </div>
              <span className="text-[10px] font-bold bg-amber-200 dark:bg-amber-800 text-amber-900 dark:text-amber-200 px-2 py-0.5 rounded-full shrink-0">
                84%
              </span>
            </div>
            <div className="w-full bg-amber-200/60 dark:bg-amber-900/40 h-1.5 rounded-full overflow-hidden">
              <div className="bg-amber-500 h-full rounded-full" style={{ width: '84%' }} />
            </div>
            <div className="flex justify-end pt-0.5">
              <button
                type="button"
                onClick={() => onNavigateTab?.('planning')}
                className="px-3 py-1 bg-white dark:bg-slate-800 border border-amber-300 dark:border-amber-700 text-amber-900 dark:text-amber-200 text-xs font-semibold rounded-lg hover:bg-amber-100 dark:hover:bg-slate-700 transition-colors shadow-2xs active:scale-95 cursor-pointer"
              >
                Xem chi tiết
              </button>
            </div>
          </div>

          {/* Alert 2 */}
          <div className="bg-rose-50/80 dark:bg-rose-950/30 border border-rose-200/90 dark:border-rose-800/40 rounded-2xl p-3.5 flex flex-col gap-2 shadow-2xs">
            <div className="flex items-start justify-between gap-2">
              <div className="flex items-start gap-2.5">
                <div className="w-8 h-8 rounded-full bg-rose-100 dark:bg-rose-900/50 text-rose-700 dark:text-rose-300 flex items-center justify-center shrink-0 mt-0.5">
                  <CalendarClock className="w-4 h-4" />
                </div>
                <div>
                  <h4 className="text-xs font-bold text-rose-900 dark:text-rose-200 leading-snug">
                    Quỹ Du lịch Đà Nẵng: Đến hạn đóng kỳ 3
                  </h4>
                  <p className="text-[11px] text-rose-700 dark:text-rose-400 mt-0.5">
                    Số tiền cần đóng: <span className="font-bold">1.500.000 ₫</span> (Hạn chót: 20/10)
                  </p>
                </div>
              </div>
              <span className="text-[10px] font-bold bg-rose-200 dark:bg-rose-800 text-rose-900 dark:text-rose-200 px-2 py-0.5 rounded-full shrink-0">
                Đến hạn
              </span>
            </div>
            <div className="flex justify-end pt-0.5">
              <button
                type="button"
                onClick={() => onNavigateTab?.('transfer')}
                className="px-3.5 py-1 bg-rose-600 text-white text-xs font-bold rounded-lg hover:bg-rose-700 transition-colors shadow-2xs active:scale-95 flex items-center gap-1 cursor-pointer"
              >
                <span>Đóng ngay</span>
                <ArrowRight className="w-3.5 h-3.5" />
              </button>
            </div>
          </div>
        </section>

        {/* ========================================================
            BLOCK 08: GROUP FUNDS SUMMARY (QUỸ NHÓM CỦA BẠN)
            ======================================================== */}
        <section className="flex flex-col gap-2.5">
          <div className="flex justify-between items-center">
            <h3 className="text-xs font-bold leading-tight">Quỹ nhóm của bạn</h3>
            <button
              type="button"
              onClick={() => onNavigateTab?.('menu')}
              className="text-xs font-bold text-emerald-600 dark:text-emerald-400 hover:underline flex items-center gap-0.5 cursor-pointer"
            >
              Xem tất cả quỹ nhóm →
            </button>
          </div>

          <div className="flex flex-col gap-2.5">
            {/* Fund 1 */}
            <div
              className={cn(
                'rounded-2xl p-3.5 shadow-xs border flex flex-col gap-2.5 transition-colors',
                isDark ? 'bg-slate-900 border-slate-800' : 'bg-white border-slate-100'
              )}
            >
              <div className="flex justify-between items-start">
                <div className="flex items-center gap-2.5">
                  <div className="w-9 h-9 rounded-xl bg-teal-50 dark:bg-teal-950/50 text-teal-700 dark:text-teal-400 flex items-center justify-center font-bold">
                    <Plane className="w-4 h-4" />
                  </div>
                  <div>
                    <h4 className="text-xs font-bold text-slate-900 dark:text-white">
                      Quỹ Du lịch Đà Nẵng 2026
                    </h4>
                    <span className="text-[11px] text-slate-400">5 thành viên • Chu kỳ hàng tháng</span>
                  </div>
                </div>
                <span className="text-[10px] font-bold bg-emerald-50 dark:bg-emerald-950/60 text-emerald-700 dark:text-emerald-400 px-2 py-0.5 rounded-full border border-emerald-200 dark:border-emerald-800">
                  Đã hoàn thành 2/3 kỳ
                </span>
              </div>

              <div className="flex justify-between items-center text-xs pt-1 border-t border-slate-100 dark:border-slate-800">
                <div>
                  <span className="text-[10px] text-slate-400 block">Số dư quỹ</span>
                  <span className="font-bold font-mono text-slate-900 dark:text-white">18.500.000 ₫</span>
                </div>
                <div className="text-right">
                  <span className="text-[10px] text-slate-400 block">Nghĩa vụ cá nhân</span>
                  <span className="font-bold text-emerald-600 dark:text-emerald-400 font-mono">
                    1.500.000 ₫
                  </span>
                </div>
              </div>

              <div className="flex items-center justify-between pt-1">
                <div className="flex -space-x-2 overflow-hidden">
                  <img
                    className="inline-block h-6 w-6 rounded-full ring-2 ring-white dark:ring-slate-900 object-cover"
                    src="https://images.unsplash.com/photo-1494790108377-be9c29b29330?auto=format&fit=crop&w=80&q=80"
                    alt="Member 1"
                  />
                  <img
                    className="inline-block h-6 w-6 rounded-full ring-2 ring-white dark:ring-slate-900 object-cover"
                    src="https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?auto=format&fit=crop&w=80&q=80"
                    alt="Member 2"
                  />
                  <img
                    className="inline-block h-6 w-6 rounded-full ring-2 ring-white dark:ring-slate-900 object-cover"
                    src="https://images.unsplash.com/photo-1500648767791-00dcc994a43e?auto=format&fit=crop&w=80&q=80"
                    alt="Member 3"
                  />
                  <div className="inline-flex h-6 w-6 rounded-full bg-slate-200 dark:bg-slate-700 text-slate-600 dark:text-slate-300 text-[10px] font-bold items-center justify-center ring-2 ring-white dark:ring-slate-900">
                    +2
                  </div>
                </div>
                <button
                  type="button"
                  onClick={() => onNavigateTab?.('menu')}
                  className="text-xs font-semibold text-emerald-600 dark:text-emerald-400 hover:underline flex items-center cursor-pointer"
                >
                  Chi tiết <ChevronRight className="w-3.5 h-3.5" />
                </button>
              </div>
            </div>

            {/* Fund 2 */}
            <div
              className={cn(
                'rounded-2xl p-3.5 shadow-xs border flex flex-col gap-2.5 transition-colors',
                isDark ? 'bg-slate-900 border-slate-800' : 'bg-white border-slate-100'
              )}
            >
              <div className="flex justify-between items-start">
                <div className="flex items-center gap-2.5">
                  <div className="w-9 h-9 rounded-xl bg-indigo-50 dark:bg-indigo-950/50 text-indigo-700 dark:text-indigo-400 flex items-center justify-center font-bold">
                    <Utensils className="w-4 h-4" />
                  </div>
                  <div>
                    <h4 className="text-xs font-bold text-slate-900 dark:text-white">
                      Quỹ Ăn trưa Công ty
                    </h4>
                    <span className="text-[11px] text-slate-400">8 thành viên • Đóng góp linh hoạt</span>
                  </div>
                </div>
                <span className="text-[10px] font-bold bg-indigo-50 dark:bg-indigo-950/60 text-indigo-700 dark:text-indigo-400 px-2 py-0.5 rounded-full border border-indigo-200 dark:border-indigo-800">
                  Hoạt động đều
                </span>
              </div>

              <div className="flex justify-between items-center text-xs pt-1 border-t border-slate-100 dark:border-slate-800">
                <div>
                  <span className="text-[10px] text-slate-400 block">Số dư quỹ</span>
                  <span className="font-bold font-mono text-slate-900 dark:text-white">3.200.000 ₫</span>
                </div>
                <div className="flex -space-x-2 overflow-hidden">
                  <img
                    className="inline-block h-6 w-6 rounded-full ring-2 ring-white dark:ring-slate-900 object-cover"
                    src="https://images.unsplash.com/photo-1438761681033-6461ffad8d80?auto=format&fit=crop&w=80&q=80"
                    alt="Member 1"
                  />
                  <img
                    className="inline-block h-6 w-6 rounded-full ring-2 ring-white dark:ring-slate-900 object-cover"
                    src="https://images.unsplash.com/photo-1472099645785-5658abf4ff4e?auto=format&fit=crop&w=80&q=80"
                    alt="Member 2"
                  />
                  <div className="inline-flex h-6 w-6 rounded-full bg-slate-200 dark:bg-slate-700 text-slate-600 dark:text-slate-300 text-[10px] font-bold items-center justify-center ring-2 ring-white dark:ring-slate-900">
                    +6
                  </div>
                </div>
              </div>
            </div>
          </div>
        </section>

        {/* ========================================================
            BLOCK 09: SAVINGS GOALS (MỤC TIÊU TIẾT KIỆM)
            ======================================================== */}
        <section className="flex flex-col gap-2.5">
          <div className="flex justify-between items-center">
            <h3 className="text-xs font-bold leading-tight">Mục tiêu tiết kiệm</h3>
            <button
              type="button"
              onClick={() => onNavigateTab?.('planning')}
              className="text-xs font-bold text-emerald-600 dark:text-emerald-400 hover:underline cursor-pointer"
            >
              Tạo mục tiêu mới +
            </button>
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
            {/* Goal 1 */}
            <div
              className={cn(
                'rounded-2xl p-3.5 shadow-xs border flex flex-col justify-between gap-2.5 transition-colors',
                isDark ? 'bg-slate-900 border-slate-800' : 'bg-white border-slate-100'
              )}
            >
              <div>
                <div className="flex items-center justify-between mb-1.5">
                  <div className="w-8 h-8 rounded-lg bg-emerald-50 dark:bg-emerald-950/50 text-emerald-700 dark:text-emerald-400 flex items-center justify-center">
                    <PiggyBank className="w-4 h-4" />
                  </div>
                  <span className="text-[10px] font-bold text-emerald-700 dark:text-emerald-400 bg-emerald-50 dark:bg-emerald-950/60 px-2 py-0.5 rounded-full">
                    72%
                  </span>
                </div>
                <h4 className="text-xs font-bold text-slate-900 dark:text-white">Quỹ khẩn cấp 6 tháng</h4>
                <p className="text-[11px] text-slate-400 mt-0.5 font-mono">
                  36.000.000 ₫ / 50.000.000 ₫
                </p>
              </div>
              <div>
                <div className="w-full bg-slate-100 dark:bg-slate-800 h-2 rounded-full overflow-hidden">
                  <div className="bg-[#047857] h-full rounded-full" style={{ width: '72%' }} />
                </div>
                <span className="text-[10px] text-slate-400 mt-1 block">Còn thiếu 14.000.000 ₫</span>
              </div>
            </div>

            {/* Goal 2 */}
            <div
              className={cn(
                'rounded-2xl p-3.5 shadow-xs border flex flex-col justify-between gap-2.5 transition-colors',
                isDark ? 'bg-slate-900 border-slate-800' : 'bg-white border-slate-100'
              )}
            >
              <div>
                <div className="flex items-center justify-between mb-1.5">
                  <div className="w-8 h-8 rounded-lg bg-indigo-50 dark:bg-indigo-950/50 text-indigo-600 dark:text-indigo-400 flex items-center justify-center">
                    <Laptop className="w-4 h-4" />
                  </div>
                  <span className="text-[10px] font-bold text-indigo-700 dark:text-indigo-400 bg-indigo-50 dark:bg-indigo-950/60 px-2 py-0.5 rounded-full">
                    60%
                  </span>
                </div>
                <h4 className="text-xs font-bold text-slate-900 dark:text-white">Mua Macbook Pro M3</h4>
                <p className="text-[11px] text-slate-400 mt-0.5 font-mono">
                  21.000.000 ₫ / 35.000.000 ₫
                </p>
              </div>
              <div>
                <div className="w-full bg-slate-100 dark:bg-slate-800 h-2 rounded-full overflow-hidden">
                  <div className="bg-indigo-600 h-full rounded-full" style={{ width: '60%' }} />
                </div>
                <span className="text-[10px] text-slate-400 mt-1 block">Còn thiếu 14.000.000 ₫</span>
              </div>
            </div>
          </div>
        </section>

        {/* ========================================================
            BLOCK 10: RECENT TRANSACTIONS (5 ITEMS)
            ======================================================== */}
        <section
          className={cn(
            'rounded-2xl p-4 shadow-xs border transition-colors',
            isDark ? 'bg-slate-900 border-slate-800' : 'bg-white border-slate-100'
          )}
        >
          <div className="flex justify-between items-center mb-3">
            <h3 className="text-xs font-bold leading-tight">Giao dịch gần đây</h3>
            <button
              type="button"
              onClick={() => onNavigateTab?.('wallet')}
              className="text-xs font-bold text-emerald-600 dark:text-emerald-400 hover:underline cursor-pointer"
            >
              Xem tất cả →
            </button>
          </div>

          <div className="flex flex-col divide-y divide-slate-100 dark:divide-slate-800">
            {/* Tx 1 */}
            <div className="flex items-center justify-between py-2.5 first:pt-0">
              <div className="flex items-center gap-3">
                <div className="w-10 h-10 rounded-xl bg-emerald-50 dark:bg-emerald-950/40 text-[#047857] dark:text-emerald-400 flex items-center justify-center shrink-0">
                  <Utensils className="w-4 h-4" />
                </div>
                <div>
                  <h5 className="text-xs font-bold text-slate-900 dark:text-white">Nhà hàng Maison Marou</h5>
                  <p className="text-[11px] text-slate-400">Hôm nay, 12:45</p>
                </div>
              </div>
              <div className="text-right">
                <span className="text-xs font-bold font-mono text-slate-900 dark:text-white block">
                  -350.000 ₫
                </span>
                <span className="block text-[10px] text-slate-400">Ăn uống</span>
              </div>
            </div>

            {/* Tx 2 */}
            <div className="flex items-center justify-between py-2.5">
              <div className="flex items-center gap-3">
                <div className="w-10 h-10 rounded-xl bg-emerald-100 dark:bg-emerald-900/40 text-emerald-800 dark:text-emerald-300 flex items-center justify-center shrink-0">
                  <Briefcase className="w-4 h-4" />
                </div>
                <div>
                  <h5 className="text-xs font-bold text-slate-900 dark:text-white">Nhận lương tháng 10</h5>
                  <p className="text-[11px] text-slate-400">Hôm qua, 09:00</p>
                </div>
              </div>
              <div className="text-right">
                <span className="text-xs font-bold font-mono text-emerald-600 dark:text-emerald-400 block">
                  +25.000.000 ₫
                </span>
                <span className="inline-block text-[10px] font-semibold text-emerald-700 dark:text-emerald-300 bg-emerald-50 dark:bg-emerald-950/60 px-1.5 rounded">
                  Thu nhập
                </span>
              </div>
            </div>

            {/* Tx 3 */}
            <div className="flex items-center justify-between py-2.5">
              <div className="flex items-center gap-3">
                <div className="w-10 h-10 rounded-xl bg-amber-50 dark:bg-amber-950/40 text-amber-600 dark:text-amber-400 flex items-center justify-center shrink-0">
                  <Coffee className="w-4 h-4" />
                </div>
                <div>
                  <h5 className="text-xs font-bold text-slate-900 dark:text-white">Starbucks Coffee</h5>
                  <p className="text-[11px] text-slate-400">14 Tháng 10</p>
                </div>
              </div>
              <div className="text-right">
                <span className="text-xs font-bold font-mono text-slate-900 dark:text-white block">
                  -115.000 ₫
                </span>
                <span className="block text-[10px] text-slate-400">Ăn uống</span>
              </div>
            </div>

            {/* Tx 4 */}
            <div className="flex items-center justify-between py-2.5">
              <div className="flex items-center gap-3">
                <div className="w-10 h-10 rounded-xl bg-indigo-50 dark:bg-indigo-950/40 text-indigo-700 dark:text-indigo-400 flex items-center justify-center shrink-0">
                  <Users className="w-4 h-4" />
                </div>
                <div>
                  <h5 className="text-xs font-bold text-slate-900 dark:text-white">
                    Chuyển tiền đóng Quỹ nhóm Đà Nẵng
                  </h5>
                  <p className="text-[11px] text-slate-400">13 Tháng 10</p>
                </div>
              </div>
              <div className="text-right">
                <span className="text-xs font-bold font-mono text-slate-900 dark:text-white block">
                  -1.500.000 ₫
                </span>
                <span className="block text-[10px] text-slate-400">Quỹ nhóm</span>
              </div>
            </div>

            {/* Tx 5 */}
            <div className="flex items-center justify-between py-2.5 last:pb-0">
              <div className="flex items-center gap-3">
                <div className="w-10 h-10 rounded-xl bg-rose-50 dark:bg-rose-950/40 text-rose-600 dark:text-rose-400 flex items-center justify-center shrink-0">
                  <ShoppingBag className="w-4 h-4" />
                </div>
                <div>
                  <h5 className="text-xs font-bold text-slate-900 dark:text-white">
                    Thanh toán Shopee Express
                  </h5>
                  <p className="text-[11px] text-slate-400">12 Tháng 10</p>
                </div>
              </div>
              <div className="text-right">
                <span className="text-xs font-bold font-mono text-slate-900 dark:text-white block">
                  -420.000 ₫
                </span>
                <span className="block text-[10px] text-slate-400">Mua sắm</span>
              </div>
            </div>
          </div>
        </section>
      </main>
    </div>
  );
};
