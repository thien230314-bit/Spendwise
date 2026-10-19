import React, { useState, useMemo } from 'react';
import {
  Search,
  Plus,
  Filter,
  History,
  RefreshCw,
  Sliders,
  FileText,
  ArrowDownLeft,
  ArrowUpRight,
  PieChart,
} from 'lucide-react';
import { useI18n } from '../../../i18n/I18nContext';
import { useBalanceVisibility } from '../../../providers/BalanceVisibilityProvider';
import { useFinancial } from '../../../providers/FinancialProvider';
import { formatMoney } from '../../../utils/money';
import { formatDate } from '../../../utils/date';
import { Button } from '../../../components/ui/Button';
import { cn } from '../../../utils/cn';

/**
 * TRANSACTIONS LAYOUT B — EFFICIENT WORKSPACE
 * Bố cục:
 * - Cấu trúc 2 cột bất đối xứng (8 cols / 4 cols):
 *   + Cột trái (8 cols): Bảng dữ liệu giao dịch chính với mật độ thông tin tối ưu, thanh tìm kiếm inline và phân trang.
 *   + Cột phải (4 cols): Bộ lọc chuyên sâu cố định & Thống kê nhanh tổng thu / tổng chi của bộ lọc hiện tại.
 */
export interface TransactionsLayoutProps {
  onOpenPopup?: (popupId: string, mode?: 'default' | 'prefill') => void;
}

export const TransactionsLayoutB: React.FC<TransactionsLayoutProps> = ({ onOpenPopup }) => {
  const { t, locale } = useI18n();
  const { isHidden } = useBalanceVisibility();
  const { transactions, categories, wallets } = useFinancial();

  const [activeMainTab, setActiveMainTab] = useState<'history' | 'recurring' | 'rules'>('history');
  const [searchTerm, setSearchTerm] = useState('');
  const [typeFilter, setTypeFilter] = useState<'all' | 'income' | 'expense'>('all');

  const filteredTransactions = useMemo(() => {
    return transactions.filter((tx) => {
      if (typeFilter !== 'all' && tx.type !== typeFilter) return false;
      if (searchTerm.trim() && !tx.title.toLowerCase().includes(searchTerm.toLowerCase())) return false;
      return true;
    });
  }, [transactions, typeFilter, searchTerm]);

  const summary = useMemo(() => {
    let income = 0;
    let expense = 0;
    filteredTransactions.forEach((tx) => {
      if (tx.type === 'income') income += Math.abs(tx.amount);
      else expense += Math.abs(tx.amount);
    });
    return { income, expense, count: filteredTransactions.length };
  }, [filteredTransactions]);

  return (
    <div className="space-y-6">
      {/* 1. Header Sub-Tabs */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 pb-2 border-b border-slate-200/80 dark:border-slate-800">
        <div className="flex items-center gap-2">
          <button
            type="button"
            onClick={() => setActiveMainTab('history')}
            className={cn(
              'px-3.5 py-1.5 rounded-lg text-xs font-bold transition flex items-center gap-2 cursor-pointer',
              activeMainTab === 'history' ? 'bg-slate-900 text-white dark:bg-white dark:text-slate-900' : 'text-slate-500'
            )}
          >
            <History className="h-3.5 w-3.5" />
            <span>Lịch sử ({transactions.length})</span>
          </button>
          <button
            type="button"
            onClick={() => {
              setActiveMainTab('recurring');
              onOpenPopup?.('tx-recurring-create');
            }}
            className={cn(
              'px-3.5 py-1.5 rounded-lg text-xs font-bold transition flex items-center gap-2 cursor-pointer',
              activeMainTab === 'recurring' ? 'bg-slate-900 text-white dark:bg-white dark:text-slate-900' : 'text-slate-500'
            )}
          >
            <RefreshCw className="h-3.5 w-3.5" />
            <span>Định kỳ</span>
          </button>
          <button
            type="button"
            onClick={() => {
              setActiveMainTab('rules');
              onOpenPopup?.('tx-rule-create');
            }}
            className={cn(
              'px-3.5 py-1.5 rounded-lg text-xs font-bold transition flex items-center gap-2 cursor-pointer',
              activeMainTab === 'rules' ? 'bg-slate-900 text-white dark:bg-white dark:text-slate-900' : 'text-slate-500'
            )}
          >
            <Sliders className="h-3.5 w-3.5" />
            <span>Quy tắc</span>
          </button>
        </div>

        <div className="flex items-center gap-2">
          <span className="text-[10px] font-bold px-2 py-1 rounded-full bg-cyan-100 dark:bg-cyan-950/60 text-cyan-800 dark:text-cyan-300">
            Layout B (Efficient 2-Column)
          </span>
          <Button
            size="sm"
            variant="primary"
            onClick={() => onOpenPopup?.('tx-add-expense')}
            className="text-xs font-semibold gap-1 cursor-pointer shadow-xs"
          >
            <Plus className="h-3.5 w-3.5" />
            <span>+ Chi tiêu</span>
          </Button>
          <Button
            size="sm"
            variant="outline"
            onClick={() => onOpenPopup?.('tx-add-income')}
            className="text-xs font-semibold gap-1 cursor-pointer text-emerald-600 hover:text-emerald-700"
          >
            <Plus className="h-3.5 w-3.5" />
            <span>+ Thu nhập</span>
          </Button>
        </div>
      </div>

      {/* 2. Main Two-Column Layout (8 cols / 4 cols) */}
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 items-start">
        {/* Left Column (8 cols): Search + Ledger Table */}
        <div className="lg:col-span-8 space-y-4">
          <div className="relative">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-slate-400" />
            <input
              type="text"
              placeholder="Tìm kiếm giao dịch..."
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
              className="w-full pl-9 pr-4 py-2 rounded-xl bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 text-xs focus:outline-none focus:ring-1 focus:ring-emerald-500"
            />
          </div>

          <div className="bg-white dark:bg-slate-900 rounded-2xl border border-slate-200/80 dark:border-slate-800 shadow-xs overflow-hidden">
            <div className="divide-y divide-slate-100 dark:divide-slate-800">
              {filteredTransactions.length === 0 ? (
                <div className="py-12 text-center text-xs text-slate-400">Không tìm thấy giao dịch nào.</div>
              ) : (
                filteredTransactions.slice(0, 10).map((tx) => (
                  <div
                    key={tx.id}
                    onClick={() => onOpenPopup?.('tx-detail-drawer')}
                    className="p-3.5 flex items-center justify-between gap-3 hover:bg-slate-50/50 dark:hover:bg-slate-800/20 transition cursor-pointer"
                  >
                    <div className="flex items-center gap-3 min-w-0">
                      <div className={cn(
                        'h-8 w-8 rounded-lg flex items-center justify-center shrink-0 font-bold',
                        tx.type === 'income' ? 'bg-emerald-100 text-emerald-600 dark:bg-emerald-950/60 dark:text-emerald-400' : 'bg-slate-100 text-slate-600 dark:bg-slate-800 dark:text-slate-400'
                      )}>
                        {tx.type === 'income' ? <ArrowDownLeft className="h-3.5 w-3.5" /> : <ArrowUpRight className="h-3.5 w-3.5" />}
                      </div>
                      <div className="min-w-0">
                        <p className="text-xs font-bold text-slate-900 dark:text-white truncate">{tx.title}</p>
                        <p className="text-[10px] text-slate-400 font-mono">{formatDate(tx.date, locale)} • {tx.category || 'Chung'}</p>
                      </div>
                    </div>
                    <div className="text-right font-mono shrink-0">
                      <span className={cn(
                        'text-xs font-bold tabular-nums block',
                        tx.type === 'income' ? 'text-emerald-600 dark:text-emerald-400' : 'text-slate-900 dark:text-white'
                      )}>
                        {tx.type === 'income' ? '+' : '−'}
                        {formatMoney(Math.abs(tx.amount), locale, { isHidden })}
                      </span>
                    </div>
                  </div>
                ))
              )}
            </div>
          </div>
        </div>

        {/* Right Column (4 cols): Filter Deck & Ledger Summary Strip */}
        <div className="lg:col-span-4 space-y-4">
          <div className="p-5 rounded-2xl bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 shadow-xs space-y-4 text-xs">
            <h3 className="font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400 flex items-center gap-2">
              <Filter className="h-3.5 w-3.5 text-emerald-500" />
              <span>Bộ lọc loại giao dịch</span>
            </h3>

            <div className="space-y-1.5">
              <button
                type="button"
                onClick={() => setTypeFilter('all')}
                className={cn('w-full text-left px-3 py-2 rounded-lg font-medium transition flex items-center justify-between', typeFilter === 'all' ? 'bg-slate-100 dark:bg-slate-800 text-slate-900 dark:text-white font-bold' : 'text-slate-500')}
              >
                <span>Tất cả</span>
                <span className="font-mono text-[10px]">{transactions.length}</span>
              </button>
              <button
                type="button"
                onClick={() => setTypeFilter('income')}
                className={cn('w-full text-left px-3 py-2 rounded-lg font-medium transition flex items-center justify-between', typeFilter === 'income' ? 'bg-emerald-50 dark:bg-emerald-950/40 text-emerald-700 dark:text-emerald-300 font-bold' : 'text-slate-500')}
              >
                <span>Chỉ Thu nhập</span>
                <span className="font-mono text-[10px] text-emerald-600">+</span>
              </button>
              <button
                type="button"
                onClick={() => setTypeFilter('expense')}
                className={cn('w-full text-left px-3 py-2 rounded-lg font-medium transition flex items-center justify-between', typeFilter === 'expense' ? 'bg-rose-50 dark:bg-rose-950/40 text-rose-700 dark:text-rose-300 font-bold' : 'text-slate-500')}
              >
                <span>Chỉ Chi tiêu</span>
                <span className="font-mono text-[10px] text-rose-600">−</span>
              </button>
            </div>
          </div>

          {/* Quick Analytics Summary Box */}
          <div className="p-5 rounded-2xl bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 shadow-xs space-y-3 text-xs">
            <h3 className="font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400">
              Tổng kết lọc ({summary.count})
            </h3>
            <div className="space-y-2 pt-2 border-t border-slate-100 dark:border-slate-800 font-mono">
              <div className="flex justify-between items-center text-emerald-600 dark:text-emerald-400">
                <span>Tổng thu:</span>
                <span className="font-bold">+{formatMoney(summary.income, locale, { isHidden })}</span>
              </div>
              <div className="flex justify-between items-center text-rose-600 dark:text-rose-400">
                <span>Tổng chi:</span>
                <span className="font-bold">−{formatMoney(summary.expense, locale, { isHidden })}</span>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};
