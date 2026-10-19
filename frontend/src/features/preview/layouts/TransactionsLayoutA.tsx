import React, { useState, useMemo } from 'react';
import {
  Search,
  Plus,
  Minus,
  Filter,
  Calendar,
  Wallet,
  Tag,
  ArrowDownLeft,
  ArrowUpRight,
  Clock,
  History,
  RefreshCw,
  Sliders,
  FileText,
  ChevronRight,
} from 'lucide-react';
import { useI18n } from '../../../i18n/I18nContext';
import { useBalanceVisibility } from '../../../providers/BalanceVisibilityProvider';
import { useFinancial } from '../../../providers/FinancialProvider';
import { formatMoney } from '../../../utils/money';
import { formatDate } from '../../../utils/date';
import { Button } from '../../../components/ui/Button';
import { cn } from '../../../utils/cn';

/**
 * TRANSACTIONS LAYOUT A — BALANCED WORKSPACE
 * Bố cục:
 * - Header 3 sub-tabs chính: Lịch sử giao dịch, Giao dịch định kỳ, Quy tắc phân loại.
 * - Toolbar tìm kiếm & bộ lọc phân tầng trải ngang thanh thoát, không chiếm nhiều chiều cao.
 * - Bảng dữ liệu giao dịch Full-Width rộng rãi, hiển thị rõ chứng từ, danh mục và ngày tháng.
 */
export interface TransactionsLayoutProps {
  onOpenPopup?: (popupId: string, mode?: 'default' | 'prefill') => void;
}

export const TransactionsLayoutA: React.FC<TransactionsLayoutProps> = ({ onOpenPopup }) => {
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

  return (
    <div className="space-y-6">
      {/* 1. Header & Primary Tab Navigation */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 pb-2 border-b border-slate-200/80 dark:border-slate-800">
        <div className="flex items-center gap-2">
          <button
            type="button"
            onClick={() => setActiveMainTab('history')}
            className={cn(
              'px-4 py-2 rounded-xl text-xs font-bold transition flex items-center gap-2 cursor-pointer',
              activeMainTab === 'history'
                ? 'bg-slate-900 text-white dark:bg-white dark:text-slate-900 shadow-xs'
                : 'text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white'
            )}
          >
            <History className="h-4 w-4" />
            <span>Lịch sử giao dịch ({transactions.length})</span>
          </button>

          <button
            type="button"
            onClick={() => {
              setActiveMainTab('recurring');
              onOpenPopup?.('tx-recurring-create');
            }}
            className={cn(
              'px-4 py-2 rounded-xl text-xs font-bold transition flex items-center gap-2 cursor-pointer',
              activeMainTab === 'recurring'
                ? 'bg-slate-900 text-white dark:bg-white dark:text-slate-900 shadow-xs'
                : 'text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white'
            )}
          >
            <RefreshCw className="h-4 w-4" />
            <span>Giao dịch định kỳ</span>
          </button>

          <button
            type="button"
            onClick={() => {
              setActiveMainTab('rules');
              onOpenPopup?.('tx-rule-create');
            }}
            className={cn(
              'px-4 py-2 rounded-xl text-xs font-bold transition flex items-center gap-2 cursor-pointer',
              activeMainTab === 'rules'
                ? 'bg-slate-900 text-white dark:bg-white dark:text-slate-900 shadow-xs'
                : 'text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white'
            )}
          >
            <Sliders className="h-4 w-4" />
            <span>Quy tắc tự động</span>
          </button>
        </div>

        <div className="flex items-center gap-2 flex-wrap">
          <Button
            size="sm"
            variant="outline"
            onClick={() => onOpenPopup?.('tx-category-mgr')}
            className="text-xs font-semibold gap-1.5 cursor-pointer"
          >
            <span>Quản lý danh mục</span>
          </Button>

          <Button
            size="sm"
            variant="primary"
            onClick={() => onOpenPopup?.('tx-add-expense')}
            className="text-xs font-semibold gap-1.5 bg-rose-600 hover:bg-rose-700 text-white cursor-pointer"
          >
            <Plus className="h-3.5 w-3.5" />
            <span>+ Chi tiêu</span>
          </Button>

          <Button
            size="sm"
            variant="primary"
            onClick={() => onOpenPopup?.('tx-add-income')}
            className="text-xs font-semibold gap-1.5 bg-emerald-600 hover:bg-emerald-700 text-white cursor-pointer"
          >
            <Plus className="h-3.5 w-3.5" />
            <span>+ Thu nhập</span>
          </Button>
        </div>
      </div>

      {/* 2. Search & Filter Horizontal Toolbar */}
      <div className="p-4 rounded-2xl bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 shadow-xs flex flex-col md:flex-row items-center justify-between gap-4">
        <div className="relative w-full md:w-80">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-slate-400" />
          <input
            type="text"
            placeholder="Tìm kiếm theo tiêu đề giao dịch..."
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            className="w-full pl-9 pr-4 py-2 rounded-xl bg-slate-50 dark:bg-slate-800 border border-slate-200 dark:border-slate-700 text-xs focus:outline-none focus:ring-1 focus:ring-emerald-500"
          />
        </div>

        <div className="flex items-center gap-2 w-full md:w-auto overflow-x-auto text-xs font-semibold">
          <button
            type="button"
            onClick={() => setTypeFilter('all')}
            className={cn(
              'px-3 py-1.5 rounded-lg transition',
              typeFilter === 'all' ? 'bg-slate-900 text-white dark:bg-slate-700' : 'text-slate-500 hover:bg-slate-100 dark:hover:bg-slate-800'
            )}
          >
            Tất cả
          </button>
          <button
            type="button"
            onClick={() => setTypeFilter('income')}
            className={cn(
              'px-3 py-1.5 rounded-lg transition',
              typeFilter === 'income' ? 'bg-emerald-600 text-white' : 'text-slate-500 hover:bg-slate-100 dark:hover:bg-slate-800'
            )}
          >
            Thu nhập
          </button>
          <button
            type="button"
            onClick={() => setTypeFilter('expense')}
            className={cn(
              'px-3 py-1.5 rounded-lg transition',
              typeFilter === 'expense' ? 'bg-rose-600 text-white' : 'text-slate-500 hover:bg-slate-100 dark:hover:bg-slate-800'
            )}
          >
            Chi tiêu
          </button>
        </div>
      </div>

      {/* 3. Full-Width Transactions Ledger Table */}
      <div className="bg-white dark:bg-slate-900 rounded-2xl border border-slate-200/80 dark:border-slate-800 shadow-xs overflow-hidden">
        <div className="px-5 py-3.5 border-b border-slate-100 dark:border-slate-800 flex items-center justify-between text-xs text-slate-400 font-semibold uppercase">
          <span>Thông tin giao dịch</span>
          <span>Số tiền & Ví</span>
        </div>

        <div className="divide-y divide-slate-100 dark:divide-slate-800">
          {filteredTransactions.length === 0 ? (
            <div className="py-16 text-center text-xs text-slate-400 space-y-2">
              <FileText className="h-8 w-8 mx-auto text-slate-300 dark:text-slate-600" />
              <p>Không tìm thấy giao dịch nào phù hợp.</p>
            </div>
          ) : (
            filteredTransactions.slice(0, 10).map((tx) => (
              <div
                key={tx.id}
                onClick={() => onOpenPopup?.('tx-detail-drawer')}
                className="p-4 sm:p-5 flex items-center justify-between gap-4 hover:bg-slate-50/50 dark:hover:bg-slate-800/30 transition-colors cursor-pointer group"
              >
                <div className="flex items-center gap-3.5 min-w-0">
                  <div
                    className={cn(
                      'h-10 w-10 rounded-xl flex items-center justify-center shrink-0 font-bold',
                      tx.type === 'income'
                        ? 'bg-emerald-100 text-emerald-600 dark:bg-emerald-950/60 dark:text-emerald-400'
                        : 'bg-slate-100 text-slate-600 dark:bg-slate-800 dark:text-slate-400'
                    )}
                  >
                    {tx.type === 'income' ? <ArrowDownLeft className="h-4 w-4" /> : <ArrowUpRight className="h-4 w-4" />}
                  </div>
                  <div className="min-w-0">
                    <p className="text-sm font-bold text-slate-900 dark:text-white truncate">
                      {tx.title}
                    </p>
                    <div className="flex items-center gap-2 text-xs text-slate-400 mt-0.5">
                      <span className="font-mono">{formatDate(tx.date, locale)}</span>
                      <span>•</span>
                      <span className="px-2 py-0.5 rounded-full bg-slate-100 dark:bg-slate-800 text-[10px] font-medium text-slate-600 dark:text-slate-300">
                        {tx.category || 'Chung'}
                      </span>
                    </div>
                  </div>
                </div>

                <div className="text-right font-mono shrink-0">
                  <span
                    className={cn(
                      'text-sm sm:text-base font-bold tabular-nums block',
                      tx.type === 'income' ? 'text-emerald-600 dark:text-emerald-400' : 'text-slate-900 dark:text-white'
                    )}
                  >
                    {tx.type === 'income' ? '+' : '−'}
                    {formatMoney(Math.abs(tx.amount), locale, { isHidden })}
                  </span>
                  <span className="text-[11px] text-slate-400 font-sans block">
                    Ví chính
                  </span>
                </div>
              </div>
            ))
          )}
        </div>
      </div>
    </div>
  );
};
