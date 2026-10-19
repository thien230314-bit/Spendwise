import React, { useState, useMemo } from 'react';
import {
  PieChart as PieIcon,
  Plus,
  Calendar,
  AlertTriangle,
  CheckCircle2,
} from 'lucide-react';
import { useI18n } from '../../../i18n/I18nContext';
import { useFinancial } from '../../../providers/FinancialProvider';
import { formatMoney } from '../../../utils/money';
import { Button } from '../../../components/ui/Button';
import { cn } from '../../../utils/cn';

/**
 * BUDGET LAYOUT B — EFFICIENT WORKSPACE
 * Bố cục:
 * - Cột trái (7 cols): Danh sách ngân sách theo từng danh mục dạng thẻ xếp gọn, tối ưu mật độ thông tin.
 * - Cột phải (5 cols): Thước đo hạn mức tổng quan tháng này cố định kèm Cảnh báo các danh mục chi tiêu chạm ngưỡng (>80%).
 */
export interface BudgetLayoutProps {
  onOpenPopup?: (popupId: string, mode?: 'default' | 'prefill') => void;
}

export const BudgetLayoutB: React.FC<BudgetLayoutProps> = ({ onOpenPopup }) => {
  const { t, locale } = useI18n();
  const { budgets } = useFinancial();

  const [selectedMonth, setSelectedMonth] = useState('10/2026');

  const overall = useMemo(() => {
    const totalBudget = budgets.reduce((acc, b) => acc + b.amount, 0) || 15000000;
    const totalSpent = budgets.reduce((acc, b) => acc + (b.spent || 0), 0) || 6800000;
    const pct = totalBudget > 0 ? Math.round((totalSpent / totalBudget) * 100) : 0;
    return { totalBudget, totalSpent, pct, remaining: totalBudget - totalSpent };
  }, [budgets]);

  const items = budgets.length > 0 ? budgets : [
    { id: '1', categoryName: 'Ăn uống', amount: 5000000, spent: 3200000 },
    { id: '2', categoryName: 'Đi lại & Xăng xe', amount: 2000000, spent: 1850000 },
    { id: '3', categoryName: 'Mua sắm', amount: 4000000, spent: 1200000 },
    { id: '4', categoryName: 'Hóa đơn & Tiện ích', amount: 3000000, spent: 550000 },
  ];

  return (
    <div className="space-y-6">
      {/* 1. Header Toolbar */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 pb-2 border-b border-slate-200/80 dark:border-slate-800">
        <div className="flex items-center gap-3">
          <div className="flex items-center gap-2">
            <PieIcon className="h-5 w-5 text-cyan-500" />
            <h2 className="text-xl font-extrabold tracking-tight text-slate-900 dark:text-white">
              {locale === 'vi-VN' ? 'Kiểm soát ngân sách' : 'Budget Cockpit'}
            </h2>
          </div>
          <div className="flex items-center gap-1 px-2.5 py-1 rounded-lg bg-slate-100 dark:bg-slate-800 text-xs font-mono font-bold">
            <Calendar className="h-3 w-3 text-slate-400" />
            <span>T{selectedMonth}</span>
          </div>
        </div>

        <div className="flex items-center gap-2">
          <span className="text-[10px] font-bold px-2 py-1 rounded-full bg-cyan-100 dark:bg-cyan-950/60 text-cyan-800 dark:text-cyan-300">
            Layout B (Efficient Dual-Pane)
          </span>
          <Button
            size="sm"
            variant="primary"
            onClick={() => onOpenPopup?.('budget-create')}
            className="text-xs font-semibold gap-1 cursor-pointer shadow-xs"
          >
            <Plus className="h-3.5 w-3.5" />
            <span>Thêm mới</span>
          </Button>
        </div>
      </div>

      {/* 2. Asymmetric 2-Column Grid (7 cols / 5 cols) */}
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 items-start">
        {/* Left (7 cols): Category Budgets Detailed List */}
        <div className="lg:col-span-7 space-y-3">
          {items.map((b: any) => {
            const spent = b.spent || 0;
            const pct = Math.round((spent / b.amount) * 100);
            return (
              <div
                key={b.id}
                className="p-4 rounded-xl bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 shadow-xs space-y-2.5"
              >
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    <span className="text-xs font-bold text-slate-900 dark:text-white">
                      {b.categoryName}
                    </span>
                    <button
                      type="button"
                      onClick={() => onOpenPopup?.('budget-edit', 'prefill')}
                      className="text-[10px] text-slate-400 hover:text-slate-700 dark:hover:text-slate-200 font-bold px-1.5 py-0.5 rounded hover:bg-slate-100 dark:hover:bg-slate-800 transition"
                    >
                      Sửa
                    </button>
                    <button
                      type="button"
                      onClick={() => onOpenPopup?.('budget-delete')}
                      className="text-[10px] text-rose-400 hover:text-rose-600 font-bold px-1.5 py-0.5 rounded hover:bg-rose-50 dark:hover:bg-rose-950/40 transition"
                    >
                      Xóa
                    </button>
                  </div>
                  <span className={cn(
                    'text-[11px] font-bold font-mono px-2 py-0.5 rounded-full',
                    pct > 90 ? 'bg-rose-100 text-rose-700' : pct > 75 ? 'bg-amber-100 text-amber-700' : 'bg-emerald-100 text-emerald-700'
                  )}>
                    {pct}% ({formatMoney(spent, locale)} / {formatMoney(b.amount, locale)})
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
              </div>
            );
          })}
        </div>

        {/* Right (5 cols): Global Meter & Alert Warnings */}
        <div className="lg:col-span-5 space-y-4">
          <div className="p-5 rounded-2xl bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 shadow-xs space-y-4">
            <span className="text-[11px] font-bold uppercase tracking-wider text-slate-400 block">
              Tổng quan toàn bộ ngân sách
            </span>
            <div className="space-y-1 font-mono">
              <span className="text-2xl font-bold text-slate-900 dark:text-white block">
                {formatMoney(overall.totalSpent, locale)}
              </span>
              <span className="text-xs text-slate-400 block">
                trên tổng hạn mức {formatMoney(overall.totalBudget, locale)}
              </span>
            </div>

            <div className="w-full h-3 bg-slate-100 dark:bg-slate-800 rounded-full overflow-hidden">
              <div
                style={{ width: `${Math.min(100, overall.pct)}%` }}
                className="h-full bg-emerald-500 rounded-full"
              />
            </div>

            <div className="pt-3 border-t border-slate-100 dark:border-slate-800 flex justify-between text-xs font-mono">
              <span className="text-slate-500">Khả dụng còn lại:</span>
              <span className="font-bold text-emerald-600 dark:text-emerald-400">
                {formatMoney(overall.remaining, locale)}
              </span>
            </div>
          </div>

          {/* Attention Alerts */}
          <div className="p-4 rounded-xl bg-amber-50/70 dark:bg-amber-950/20 border border-amber-200/80 dark:border-amber-900/40 text-xs space-y-2">
            <div className="flex items-center gap-1.5 font-bold text-amber-900 dark:text-amber-200">
              <AlertTriangle className="h-4 w-4 text-amber-600" />
              <span>Cảnh báo chi tiêu chạm ngưỡng</span>
            </div>
            <p className="text-[11px] text-slate-600 dark:text-slate-400">
              Danh mục <strong>Đi lại & Xăng xe</strong> đã tiêu thụ 92.5% hạn mức cho phép trong tháng 10.
            </p>
          </div>
        </div>
      </div>
    </div>
  );
};
