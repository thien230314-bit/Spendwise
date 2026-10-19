import React, { useState, useMemo } from 'react';
import {
  Target,
  Plus,
  ArrowDownLeft,
  ArrowUpRight,
  TrendingUp,
  Calendar,
  Clock,
  Car,
  Home,
  Laptop,
  Plane,
  ShieldCheck,
} from 'lucide-react';
import { useI18n } from '../../../i18n/I18nContext';
import { useFinancial } from '../../../providers/FinancialProvider';
import { formatMoney } from '../../../utils/money';
import { Button } from '../../../components/ui/Button';
import { cn } from '../../../utils/cn';

/**
 * GOALS LAYOUT A — BALANCED WORKSPACE
 * Bố cục:
 * - Hàng 1: Thống kê tổng quan tích lũy (Tổng đã gom / Tổng đích, % hoàn thành, + Nút tạo mục tiêu).
 * - Hàng 2: Danh sách thẻ mục tiêu tích lũy dàn hàng Grid 3 cột cân đối, thanh tiến độ % trực quan và các nút Nạp/Rút/Sửa tích hợp.
 */
export interface GoalsLayoutProps {
  onOpenPopup?: (popupId: string, mode?: 'default' | 'prefill') => void;
}

export const GoalsLayoutA: React.FC<GoalsLayoutProps> = ({ onOpenPopup }) => {
  const { t, locale } = useI18n();
  const { goals } = useFinancial();

  const activeGoals = useMemo(() => {
    return goals.length > 0
      ? goals
      : [
          { id: '1', title: 'Mua xe VinFast VF8', targetAmount: 180000000, currentAmount: 85000000, deadline: '2027-06-30' },
          { id: '2', title: 'Quỹ dự phòng khẩn cấp', targetAmount: 50000000, currentAmount: 40000000, deadline: '2026-12-31' },
          { id: '3', title: 'Du lịch Tokyo mùa thu', targetAmount: 40000000, currentAmount: 20200000, deadline: '2027-10-15' },
        ];
  }, [goals]);

  const stats = useMemo(() => {
    const totalTarget = activeGoals.reduce((acc, g) => acc + g.targetAmount, 0);
    const totalSaved = activeGoals.reduce((acc, g) => acc + g.currentAmount, 0);
    const pct = totalTarget > 0 ? Math.round((totalSaved / totalTarget) * 100) : 0;
    return { totalTarget, totalSaved, pct, count: activeGoals.length };
  }, [activeGoals]);

  return (
    <div className="space-y-6">
      {/* 1. Header & Savings Metrics Overview Card */}
      <div className="p-6 rounded-3xl bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 shadow-xs flex flex-col md:flex-row md:items-center justify-between gap-6">
        <div>
          <div className="flex items-center gap-2 mb-1">
            <Target className="h-5 w-5 text-emerald-500" />
            <h2 className="text-xl font-extrabold tracking-tight text-slate-900 dark:text-white">
              {locale === 'vi-VN' ? 'Mục tiêu tích lũy tài chính' : 'Savings Goals'}
            </h2>
            <span className="text-[10px] font-bold px-2 py-0.5 rounded-full bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-300">
              Layout A (Balanced Grid)
            </span>
          </div>
          <div className="flex items-baseline gap-2 font-mono mt-2">
            <span className="text-2xl sm:text-3xl font-extrabold text-slate-900 dark:text-white tabular-nums">
              {formatMoney(stats.totalSaved, locale)}
            </span>
            <span className="text-xs text-slate-400">/ {formatMoney(stats.totalTarget, locale)} ({stats.pct}%)</span>
          </div>
        </div>

        <div className="flex items-center gap-3">
          <Button
            size="sm"
            variant="primary"
            onClick={() => onOpenPopup?.('goal-create')}
            className="text-xs font-semibold gap-1.5 cursor-pointer shadow-xs"
          >
            <Plus className="h-3.5 w-3.5" />
            <span>+ Tạo mục tiêu mới</span>
          </Button>
        </div>
      </div>

      {/* 2. Active Goals 3-Column Grid */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-5">
        {activeGoals.map((g: any) => {
          const pct = Math.round((g.currentAmount / g.targetAmount) * 100);
          return (
            <div
              key={g.id}
              className="p-5 rounded-2xl bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 shadow-xs flex flex-col justify-between min-h-60 space-y-4"
            >
              <div>
                <div className="flex items-start justify-between gap-2 mb-2">
                  <span className="text-sm font-bold text-slate-900 dark:text-white truncate">
                    {g.title}
                  </span>
                  <div className="flex items-center gap-1.5 shrink-0">
                    <span className="text-xs font-bold font-mono px-2 py-0.5 rounded-full bg-emerald-100 text-emerald-800 dark:bg-emerald-950 dark:text-emerald-300">
                      {pct}%
                    </span>
                    <button
                      type="button"
                      onClick={() => onOpenPopup?.('goal-edit', 'prefill')}
                      className="text-[10px] text-slate-400 hover:text-slate-700 dark:hover:text-slate-200 font-bold px-1.5 py-0.5 rounded hover:bg-slate-100 dark:hover:bg-slate-800 transition"
                      title="Chỉnh sửa mục tiêu"
                    >
                      Sửa
                    </button>
                  </div>
                </div>
                <div className="flex items-center gap-1.5 text-[11px] text-slate-400 font-mono">
                  <Clock className="h-3 w-3" />
                  <span>Hạn chót: {g.deadline}</span>
                </div>
              </div>

              {/* Progress bar */}
              <div className="space-y-1.5">
                <div className="w-full h-2.5 bg-slate-100 dark:bg-slate-800 rounded-full overflow-hidden">
                  <div
                    style={{ width: `${Math.min(100, pct)}%` }}
                    className="h-full bg-emerald-500 rounded-full transition-all"
                  />
                </div>
                <div className="flex justify-between items-center text-xs font-mono">
                  <span className="text-slate-800 dark:text-slate-200 font-bold">{formatMoney(g.currentAmount, locale)}</span>
                  <span className="text-slate-400">{formatMoney(g.targetAmount, locale)}</span>
                </div>
              </div>

              {/* Quick Actions */}
              <div className="pt-3 border-t border-slate-100 dark:border-slate-800 flex items-center justify-between gap-2 text-xs">
                <button
                  type="button"
                  onClick={() => onOpenPopup?.('goal-contribute')}
                  className="flex items-center gap-1 font-semibold text-emerald-600 dark:text-emerald-400 hover:underline cursor-pointer"
                >
                  <ArrowDownLeft className="h-3.5 w-3.5" />
                  <span>Nạp thêm</span>
                </button>
                <button
                  type="button"
                  onClick={() => onOpenPopup?.('goal-withdraw')}
                  className="flex items-center gap-1 font-semibold text-slate-500 hover:text-slate-900 dark:hover:text-white cursor-pointer"
                >
                  <ArrowUpRight className="h-3.5 w-3.5" />
                  <span>Rút về ví</span>
                </button>
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
};
