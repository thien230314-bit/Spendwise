import React, { useState, useMemo } from 'react';
import {
  Target,
  Plus,
  ArrowDownLeft,
  ArrowUpRight,
  Clock,
  ShieldCheck,
  TrendingUp,
} from 'lucide-react';
import { useI18n } from '../../../i18n/I18nContext';
import { useFinancial } from '../../../providers/FinancialProvider';
import { formatMoney } from '../../../utils/money';
import { Button } from '../../../components/ui/Button';
import { cn } from '../../../utils/cn';

/**
 * GOALS LAYOUT B — EFFICIENT WORKSPACE
 * Bố cục:
 * - Cột trái (8 cols): Danh sách mục tiêu chi tiết dạng hàng ngang, kèm tiến độ và nút nạp/rút nhanh.
 * - Cột phải (4 cols): Bảng điều khiển tốc độ tích lũy, dự phóng hoàn thành & Khối bảo mật mã PIN khi rút vốn.
 */
export interface GoalsLayoutProps {
  onOpenPopup?: (popupId: string, mode?: 'default' | 'prefill') => void;
}

export const GoalsLayoutB: React.FC<GoalsLayoutProps> = ({ onOpenPopup }) => {
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
      {/* 1. Header Toolbar */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 pb-2 border-b border-slate-200/80 dark:border-slate-800">
        <div className="flex items-center gap-2">
          <Target className="h-5 w-5 text-cyan-500" />
          <h2 className="text-xl font-extrabold tracking-tight text-slate-900 dark:text-white">
            {locale === 'vi-VN' ? 'Mục tiêu & Tích lũy vốn' : 'Goals Dashboard'}
          </h2>
        </div>
        <div className="flex items-center gap-2">
          <span className="text-[10px] font-bold px-2.5 py-1 rounded-full bg-cyan-100 dark:bg-cyan-950/60 text-cyan-800 dark:text-cyan-300">
            Layout B (Efficient Dual-Pane)
          </span>
          <Button
            size="sm"
            variant="primary"
            onClick={() => onOpenPopup?.('goal-create')}
            className="text-xs font-semibold gap-1 cursor-pointer shadow-xs"
          >
            <Plus className="h-3.5 w-3.5" />
            <span>Thêm mục tiêu</span>
          </Button>
        </div>
      </div>

      {/* 2. Asymmetric 2-Column Grid (8 cols / 4 cols) */}
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 items-start">
        {/* Left (8 cols): Horizontal Goals List */}
        <div className="lg:col-span-8 space-y-3.5">
          {activeGoals.map((g: any) => {
            const pct = Math.round((g.currentAmount / g.targetAmount) * 100);
            return (
              <div
                key={g.id}
                className="p-5 rounded-2xl bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 shadow-xs space-y-3"
              >
                <div className="flex items-center justify-between">
                  <div>
                    <h3 className="text-sm font-bold text-slate-900 dark:text-white">{g.title}</h3>
                    <p className="text-[11px] text-slate-400 font-mono mt-0.5">Hạn chót: {g.deadline}</p>
                  </div>
                  <div className="text-right">
                    <div className="flex items-center justify-end gap-1.5">
                      <span className="text-xs font-bold font-mono text-emerald-600 dark:text-emerald-400 block">
                        {pct}%
                      </span>
                      <button
                        type="button"
                        onClick={() => onOpenPopup?.('goal-edit', 'prefill')}
                        className="text-[10px] text-slate-400 hover:text-slate-700 dark:hover:text-slate-200 font-bold px-1.5 py-0.5 rounded hover:bg-slate-100 dark:hover:bg-slate-800 transition"
                      >
                        Sửa
                      </button>
                    </div>
                    <span className="text-[11px] text-slate-400 font-mono">
                      {formatMoney(g.currentAmount, locale)} / {formatMoney(g.targetAmount, locale)}
                    </span>
                  </div>
                </div>

                <div className="w-full h-2.5 bg-slate-100 dark:bg-slate-800 rounded-full overflow-hidden">
                  <div style={{ width: `${Math.min(100, pct)}%` }} className="h-full bg-emerald-500 rounded-full" />
                </div>

                <div className="pt-2 border-t border-slate-100 dark:border-slate-800 flex items-center justify-between text-xs">
                  <button
                    type="button"
                    onClick={() => onOpenPopup?.('goal-contribute')}
                    className="flex items-center gap-1 font-semibold text-emerald-600 dark:text-emerald-400 hover:underline cursor-pointer"
                  >
                    <ArrowDownLeft className="h-3.5 w-3.5" />
                    <span>Nạp tiền</span>
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

        {/* Right (4 cols): Velocity Summary & Security */}
        <div className="lg:col-span-4 space-y-4">
          <div className="p-5 rounded-2xl bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 shadow-xs space-y-4">
            <span className="text-[11px] font-bold uppercase tracking-wider text-slate-400 block">
              Tổng quỹ tích lũy ({stats.count} mục tiêu)
            </span>
            <div className="space-y-1 font-mono">
              <span className="text-2xl font-bold text-slate-900 dark:text-white block">
                {formatMoney(stats.totalSaved, locale)}
              </span>
              <span className="text-xs text-slate-400 block">
                mục tiêu đích: {formatMoney(stats.totalTarget, locale)}
              </span>
            </div>

            <div className="w-full h-3 bg-slate-100 dark:bg-slate-800 rounded-full overflow-hidden">
              <div style={{ width: `${Math.min(100, stats.pct)}%` }} className="h-full bg-emerald-500 rounded-full" />
            </div>

            <div className="pt-3 border-t border-slate-100 dark:border-slate-800 flex justify-between text-xs font-mono">
              <span className="text-slate-500">Tỷ lệ hoàn thành:</span>
              <span className="font-bold text-emerald-600">{stats.pct}%</span>
            </div>
          </div>

          <div className="p-4 rounded-xl bg-slate-50 dark:bg-slate-800/40 border border-slate-200/70 dark:border-slate-800 text-xs space-y-1.5">
            <div className="flex items-center gap-1.5 font-bold text-slate-900 dark:text-white">
              <ShieldCheck className="h-4 w-4 text-emerald-600" />
              <span>Bảo vệ quỹ bằng mã PIN</span>
            </div>
            <p className="text-[11px] text-slate-500 dark:text-slate-400">
              Mọi thao tác rút tiền từ quỹ mục tiêu về ví đều yêu cầu xác thực PIN 6 số.
            </p>
          </div>
        </div>
      </div>
    </div>
  );
};
