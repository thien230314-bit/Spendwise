import React, { useState } from 'react';
import { cn } from '../../../utils/cn';
import {
  PieChart,
  Target,
  Calendar,
  Plus,
  ArrowRight,
  TrendingUp,
  Clock,
  Car,
  Home,
  Laptop,
} from 'lucide-react';
import { formatMoney } from '../../../utils/money';

interface MobilePlanningScreenProps {
  theme?: 'light' | 'dark';
  onOpenSheet?: (sheetId: string) => void;
  statusMode?: 'normal' | 'skeleton' | 'loading' | 'error';
}

export const MobilePlanningScreen: React.FC<MobilePlanningScreenProps> = ({
  theme = 'light',
  onOpenSheet,
  statusMode = 'normal',
}) => {
  const isDark = theme === 'dark';
  const isSkeleton = statusMode === 'skeleton';
  const [section, setSection] = useState<'budget' | 'goal' | 'recurring'>('budget');

  return (
    <div
      className={cn(
        'flex-1 flex flex-col p-4 space-y-4 overflow-y-auto transition-colors pb-6',
        isDark ? 'bg-[#090D16]' : 'bg-[#F8FAFC]'
      )}
    >
      {/* 3 Segments: Ngân sách / Mục tiêu / Định kỳ */}
      <div
        className={cn(
          'flex p-1 rounded-xl border text-xs font-semibold shrink-0',
          isDark ? 'bg-[#101622] border-[#1E293B]' : 'bg-[#FCFBF7] border-[#E2E8F0]'
        )}
      >
        {[
          { id: 'budget', label: 'Ngân sách' },
          { id: 'goal', label: 'Mục tiêu' },
          { id: 'recurring', label: 'Định kỳ' },
        ].map((item) => {
          const isActive = section === item.id;
          return (
            <button
              key={item.id}
              type="button"
              onClick={() => setSection(item.id as any)}
              className={cn(
                'flex-1 py-1.5 rounded-lg text-center transition-all cursor-pointer',
                isActive
                  ? isDark
                    ? 'bg-[#1E293B] text-emerald-400 shadow-xs'
                    : 'bg-white text-emerald-700 shadow-xs font-bold'
                  : 'text-slate-400 hover:text-slate-600'
              )}
            >
              {item.label}
            </button>
          );
        })}
      </div>

      {/* SECTION 1: NGÂN SÁCH */}
      {section === 'budget' && (
        <div className="space-y-4 animate-in fade-in-50 duration-150">
          <div
            className={cn(
              'p-5 rounded-3xl border shadow-sm space-y-3.5 transition-all',
              isDark ? 'bg-[#101622] border-[#1E293B]' : 'bg-[#FCFBF7] border-[#E2E8F0]'
            )}
          >
            <div className="flex items-center justify-between">
              <span className="text-xs font-bold text-slate-800 dark:text-slate-200">
                Ngân sách tháng 10/2026
              </span>
              <button
                type="button"
                onClick={() => onOpenSheet?.('budget-create')}
                className="text-[11px] font-semibold text-emerald-600 dark:text-emerald-400 hover:underline cursor-pointer"
              >
                + Đặt ngân sách
              </button>
            </div>

            <div className="space-y-2">
              <div className="w-full h-3 rounded-full bg-slate-100 dark:bg-slate-800 overflow-hidden">
                <div className="w-[62%] h-full rounded-full bg-emerald-500 transition-all" />
              </div>
              <div className="flex justify-between items-center text-xs font-mono">
                <span className="text-emerald-600 dark:text-emerald-400 font-bold">
                  Đã chi: 9.300.000 ₫ (62%)
                </span>
                <span className="text-slate-400">Hạn mức: 15.000.000 ₫</span>
              </div>
            </div>
          </div>

          {/* Category Budgets */}
          <div
            className={cn(
              'p-4 rounded-3xl border shadow-sm space-y-3 transition-all',
              isDark ? 'bg-[#101622] border-[#1E293B]' : 'bg-[#FCFBF7] border-[#E2E8F0]'
            )}
          >
            <span className="text-xs font-bold text-slate-800 dark:text-slate-200 block">
              Hạn mức danh mục chi tiêu
            </span>

            <div className="space-y-3">
              {[
                { name: 'Ăn uống', spent: 4200000, limit: 6000000, pct: 70, color: 'bg-emerald-500' },
                { name: 'Mua sắm', spent: 2100000, limit: 3000000, pct: 70, color: 'bg-indigo-500' },
                { name: 'Đi lại & Xăng', spent: 850000, limit: 1500000, pct: 56, color: 'bg-amber-500' },
              ].map((c) => (
                <div key={c.name} className="space-y-1">
                  <div className="flex justify-between items-center text-xs">
                    <span className="font-semibold text-slate-700 dark:text-slate-300">{c.name}</span>
                    <span className="font-mono text-[11px] text-slate-400">
                      {formatMoney(c.spent, 'vi-VN')} / {formatMoney(c.limit, 'vi-VN')}
                    </span>
                  </div>
                  <div className="w-full h-2 rounded-full bg-slate-100 dark:bg-slate-800 overflow-hidden">
                    <div style={{ width: `${c.pct}%` }} className={cn('h-full rounded-full', c.color)} />
                  </div>
                </div>
              ))}
            </div>
          </div>
        </div>
      )}

      {/* SECTION 2: MỤC TIÊU TIẾT KIỆM */}
      {section === 'goal' && (
        <div className="space-y-4 animate-in fade-in-50 duration-150">
          <div
            className={cn(
              'p-4 rounded-3xl border shadow-sm flex items-center justify-between',
              isDark ? 'bg-[#101622] border-[#1E293B]' : 'bg-[#FCFBF7] border-[#E2E8F0]'
            )}
          >
            <div>
              <span className="text-xs font-bold text-slate-800 dark:text-slate-200 block">
                Tổng tích lũy mục tiêu
              </span>
              <span className="text-xl font-extrabold font-mono text-emerald-600 dark:text-emerald-400">
                145.200.000 ₫
              </span>
            </div>
            <button
              type="button"
              onClick={() => onOpenSheet?.('goal-create')}
              className="py-2 px-3 rounded-xl bg-emerald-600 hover:bg-emerald-700 text-white text-xs font-bold cursor-pointer"
            >
              + Tạo mục tiêu
            </button>
          </div>

          <div className="space-y-3">
            {[
              { id: '1', title: 'Mua xe VinFast VF8', icon: Car, target: 180000000, current: 85000000, pct: 47, date: '30/06/2027' },
              { id: '2', title: 'Quỹ dự phòng khẩn cấp', icon: Home, target: 50000000, current: 40000000, pct: 80, date: '31/12/2026' },
              { id: '3', title: 'MacBook Pro M3 Max', icon: Laptop, target: 45000000, current: 20200000, pct: 45, date: '15/10/2027' },
            ].map((g) => {
              const Icon = g.icon;
              return (
                <div
                  key={g.id}
                  className={cn(
                    'p-4 rounded-3xl border shadow-sm space-y-3',
                    isDark ? 'bg-[#101622] border-[#1E293B]' : 'bg-[#FCFBF7] border-[#E2E8F0]'
                  )}
                >
                  <div className="flex items-center justify-between">
                    <div className="flex items-center gap-2.5">
                      <div className="w-8 h-8 rounded-xl bg-emerald-100 dark:bg-emerald-950 text-emerald-600 dark:text-emerald-400 flex items-center justify-center">
                        <Icon className="w-4 h-4" />
                      </div>
                      <span className="text-xs font-bold text-slate-800 dark:text-slate-200">
                        {g.title}
                      </span>
                    </div>
                    <span className="text-xs font-bold font-mono text-emerald-600 dark:text-emerald-400">
                      {g.pct}%
                    </span>
                  </div>

                  <div className="w-full h-2.5 rounded-full bg-slate-100 dark:bg-slate-800 overflow-hidden">
                    <div style={{ width: `${g.pct}%` }} className="h-full rounded-full bg-emerald-500" />
                  </div>

                  <div className="flex justify-between items-center text-[11px] font-mono text-slate-400">
                    <span>{formatMoney(g.current, 'vi-VN')} / {formatMoney(g.target, 'vi-VN')}</span>
                    <span>Hạn: {g.date}</span>
                  </div>
                </div>
              );
            })}
          </div>
        </div>
      )}

      {/* SECTION 3: GIAO DỊCH ĐỊNH KỲ */}
      {section === 'recurring' && (
        <div className="space-y-4 animate-in fade-in-50 duration-150">
          <div
            className={cn(
              'p-4 rounded-3xl border shadow-sm flex items-center justify-between',
              isDark ? 'bg-[#101622] border-[#1E293B]' : 'bg-[#FCFBF7] border-[#E2E8F0]'
            )}
          >
            <span className="text-xs font-bold text-slate-800 dark:text-slate-200">
              Lịch giao dịch định kỳ
            </span>
            <button
              type="button"
              onClick={() => onOpenSheet?.('recurring-create')}
              className="text-[11px] font-semibold text-emerald-600 dark:text-emerald-400 hover:underline cursor-pointer"
            >
              + Đăng ký mới
            </button>
          </div>

          <div className="space-y-2.5">
            {[
              { id: '1', title: 'Tiền thuê nhà Vinhomes', amount: -7500000, date: 'Ngày 05 hàng tháng', status: 'Active' },
              { id: '2', title: 'Gói Netflix Premium 4K', amount: -260000, date: 'Ngày 15 hàng tháng', status: 'Active' },
              { id: '3', title: 'Lương chuyển khoản TechCorp', amount: 28500000, date: 'Ngày 28 hàng tháng', status: 'Active' },
            ].map((r) => (
              <div
                key={r.id}
                className={cn(
                  'p-3.5 rounded-2xl border flex items-center justify-between gap-3',
                  isDark ? 'bg-[#101622] border-[#1E293B]' : 'bg-[#FCFBF7] border-[#E2E8F0]'
                )}
              >
                <div className="space-y-0.5">
                  <span className="text-xs font-semibold text-slate-800 dark:text-slate-200 block">
                    {r.title}
                  </span>
                  <span className="text-[10px] text-slate-400 font-mono">
                    {r.date}
                  </span>
                </div>
                <span className="text-xs font-bold font-mono tabular-nums text-slate-800 dark:text-slate-200">
                  {formatMoney(r.amount, 'vi-VN')}
                </span>
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  );
};
