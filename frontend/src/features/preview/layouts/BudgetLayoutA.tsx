import React, { useState, useMemo } from 'react';
import {
  PieChart as PieIcon,
  Plus,
  Calendar,
  AlertTriangle,
  CheckCircle2,
  TrendingUp,
  Layers,
} from 'lucide-react';
import { useI18n } from '../../../i18n/I18nContext';
import { useFinancial } from '../../../providers/FinancialProvider';
import { formatMoney } from '../../../utils/money';
import { Button } from '../../../components/ui/Button';
import { cn } from '../../../utils/cn';

/**
 * BUDGET LAYOUT A — BALANCED WORKSPACE
 * Bố cục:
 * - Hàng 1: Header & Bộ chọn tháng động MM/YYYY + Nút Thiết lập ngân sách.
 * - Hàng 2: Thước đo hạn mức ngân sách tổng quan tháng này (Full-width).
 * - Hàng 3: Danh sách ngân sách theo danh mục (Grid 2 cột cân đối, hiển thị rõ tiến độ % và cảnh báo).
 */
export interface BudgetLayoutProps {
  onOpenPopup?: (popupId: string, mode?: 'default' | 'prefill') => void;
}

export const BudgetLayoutA: React.FC<BudgetLayoutProps> = ({ onOpenPopup }) => {
  const { t, locale } = useI18n();
  const { budgets } = useFinancial();

  const [selectedMonth, setSelectedMonth] = useState('10/2026');

  const overall = useMemo(() => {
    const totalBudget = budgets.reduce((acc, b) => acc + b.amount, 0) || 15000000;
    const totalSpent = budgets.reduce((acc, b) => acc + (b.spent || 0), 0) || 6800000;
    const pct = totalBudget > 0 ? Math.round((totalSpent / totalBudget) * 100) : 0;
    return { totalBudget, totalSpent, pct, remaining: totalBudget - totalSpent };
  }, [budgets]);

  return (
    <div className="space-y-6">
      {/* 1. Header Toolbar */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 pb-2 border-b border-slate-200/80 dark:border-slate-800">
        <div className="flex items-center gap-3">
          <div className="flex items-center gap-2">
            <PieIcon className="h-5 w-5 text-emerald-500" />
            <h2 className="text-xl font-extrabold tracking-tight text-slate-900 dark:text-white">
              {locale === 'vi-VN' ? 'Ngân sách chi tiêu' : 'Budgets'}
            </h2>
          </div>
          <div className="flex items-center gap-1.5 px-3 py-1.5 rounded-xl bg-slate-100 dark:bg-slate-800 text-xs font-bold font-mono">
            <Calendar className="h-3.5 w-3.5 text-slate-400" />
            <span>Tháng {selectedMonth}</span>
          </div>
        </div>

        <div className="flex items-center gap-2">
          <span className="text-[10px] font-bold px-2.5 py-1 rounded-full bg-slate-100 dark:bg-slate-800 text-slate-700 dark:text-slate-300">
            Layout A (Balanced Workspace)
          </span>
          <Button
            size="sm"
            variant="primary"
            onClick={() => onOpenPopup?.('budget-create')}
            className="text-xs font-semibold gap-1.5 cursor-pointer"
          >
            <Plus className="h-3.5 w-3.5" />
            <span>+ Tạo ngân sách</span>
          </Button>
        </div>
      </div>

      {/* 2. Full-Width Overview Progress Meter Card */}
      <div className="p-6 rounded-3xl bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 shadow-xs space-y-4">
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
          <div>
            <span className="text-xs font-bold uppercase tracking-wider text-slate-400 block mb-1">
              Tiến độ chi tiêu tháng này
            </span>
            <div className="flex items-baseline gap-2 font-mono">
              <span className="text-2xl sm:text-3xl font-extrabold text-slate-900 dark:text-white tabular-nums">
                {formatMoney(overall.totalSpent, locale)}
              </span>
              <span className="text-xs text-slate-400">/ {formatMoney(overall.totalBudget, locale)}</span>
            </div>
          </div>

          <div className="text-right">
            <span className="text-xs font-bold text-slate-400 block mb-1">Còn lại</span>
            <span className="text-lg font-bold font-mono text-emerald-600 dark:text-emerald-400">
              {formatMoney(overall.remaining, locale)} ({100 - overall.pct}%)
            </span>
          </div>
        </div>

        {/* Global Progress Bar */}
        <div className="w-full h-3 bg-slate-100 dark:bg-slate-800 rounded-full overflow-hidden">
          <div
            style={{ width: `${Math.min(100, overall.pct)}%` }}
            className={cn(
              'h-full rounded-full transition-all',
              overall.pct > 90 ? 'bg-rose-500' : overall.pct > 75 ? 'bg-amber-500' : 'bg-emerald-500'
            )}
          />
        </div>
      </div>

      {/* 3. Category Budgets Grid (Balanced 2 Columns) */}
      <div className="space-y-4">
        <h3 className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400">
          Chi tiết danh mục ({budgets.length || 4})
        </h3>

        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          {(budgets.length > 0 ? budgets : [
            { id: '1', categoryName: 'Ăn uống', amount: 5000000, spent: 3200000 },
            { id: '2', categoryName: 'Đi lại & Xăng xe', amount: 2000000, spent: 1850000 },
            { id: '3', categoryName: 'Mua sắm', amount: 4000000, spent: 1200000 },
            { id: '4', categoryName: 'Hóa đơn & Tiện ích', amount: 3000000, spent: 550000 },
          ]).map((b: any) => {
            const spent = b.spent || 0;
            const pct = Math.round((spent / b.amount) * 100);
            return (
              <div
                key={b.id}
                className="p-5 rounded-2xl bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 shadow-xs space-y-3"
              >
                <div className="flex items-center justify-between">
                  <span className="text-sm font-bold text-slate-900 dark:text-white">
                    {b.categoryName}
                  </span>
                  <span className={cn(
                    'text-xs font-bold font-mono px-2 py-0.5 rounded-full',
                    pct > 90 ? 'bg-rose-100 text-rose-700' : pct > 75 ? 'bg-amber-100 text-amber-700' : 'bg-emerald-100 text-emerald-700'
                  )}>
                    {pct}%
                  </span>
                </div>

                <div className="w-full h-2 bg-slate-100 dark:bg-slate-800 rounded-full overflow-hidden">
                  <div
                    style={{ width: `${Math.min(100, pct)}%` }}
                    className={cn(
                      'h-full rounded-full',
                      pct > 90 ? 'bg-rose-500' : pct > 75 ? 'bg-amber-500' : 'bg-emerald-500'
                    )}
                  />
                </div>

                <div className="flex justify-between items-center text-xs font-mono text-slate-400 pt-1">
                  <span>Đã chi: <strong className="text-slate-800 dark:text-slate-200">{formatMoney(spent, locale)}</strong></span>
                  <span>Hạn mức: {formatMoney(b.amount, locale)}</span>
                </div>

                <div className="flex items-center justify-end gap-2 pt-2 border-t border-slate-100 dark:border-slate-800">
                  <button
                    type="button"
                    onClick={() => onOpenPopup?.('budget-detail')}
                    className="text-xs text-slate-500 hover:text-slate-700 dark:hover:text-slate-300 font-medium cursor-pointer"
                  >
                    Chi tiết
                  </button>
                  <button
                    type="button"
                    onClick={() => onOpenPopup?.('budget-edit', 'prefill')}
                    className="text-xs text-amber-600 hover:text-amber-700 font-medium cursor-pointer"
                  >
                    Sửa
                  </button>
                  <button
                    type="button"
                    onClick={() => onOpenPopup?.('budget-delete')}
                    className="text-xs text-rose-600 hover:text-rose-700 font-medium cursor-pointer"
                  >
                    Xóa
                  </button>
                </div>
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
};
