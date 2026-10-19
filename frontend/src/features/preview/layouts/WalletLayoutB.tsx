import React, { useState, useMemo } from 'react';
import {
  Wallet,
  ArrowLeftRight,
  TrendingUp,
  Eye,
  EyeOff,
  Link2,
  ArrowDownLeft,
  ArrowUpRight,
  ArrowDownRight,
  Tag,
  ExternalLink,
  Unlink,
  CheckCircle2,
  Building2,
  ShieldCheck,
  RefreshCw,
  Plus,
} from 'lucide-react';
import { useI18n } from '../../../i18n/I18nContext';
import { useBalanceVisibility } from '../../../providers/BalanceVisibilityProvider';
import { useFinancial } from '../../../providers/FinancialProvider';
import { formatMoney } from '../../../utils/money';
import { formatDate } from '../../../utils/date';
import { Button } from '../../../components/ui/Button';
import { cn } from '../../../utils/cn';

/**
 * WALLET LAYOUT B — EFFICIENT WORKSPACE
 * Bố cục:
 * - Hàng 1: Unified Wallet Header: Tích hợp Số dư tổng khả dụng + Thanh tác vụ nhanh trong 1 card ngang mỏng gọn.
 * - Hàng 2: Bố cục 2 cột bất đối xứng (7 cols / 5 cols) tối ưu không gian desktop:
 *   + Cột trái (7 cols): Danh sách ví nội bộ SpendWise (Grid 2 cột) & Bảng biến động số dư tài khoản ngân hàng chi tiết.
 *   + Cột phải (5 cols): Trung tâm quản lý liên kết SePay Open Banking & Khối bảo mật Open Banking.
 */
export interface WalletLayoutBProps {
  onOpenPopup?: (popupId: string, mode?: 'default' | 'prefill') => void;
}

export const WalletLayoutB: React.FC<WalletLayoutBProps> = ({ onOpenPopup }) => {
  const { t, locale } = useI18n();
  const { isHidden, toggleVisibility } = useBalanceVisibility();
  const { wallets, bankConnections, bankTransactions, isLoading } = useFinancial();

  const [bankTxFilter, setBankTxFilter] = useState<'ALL' | 'Credit' | 'Debit'>('ALL');

  const totalBalance = useMemo(() => {
    return wallets.reduce((sum, w) => sum + w.balance, 0);
  }, [wallets]);

  const filteredBankTransactions = useMemo(() => {
    return bankTransactions.filter((tx) => {
      if (bankTxFilter === 'ALL') return true;
      return tx.direction === bankTxFilter;
    });
  }, [bankTransactions, bankTxFilter]);

  return (
    <div className="space-y-6">
      {/* 1. Unified Wallet Control Header */}
      <div className="p-6 rounded-2xl bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 shadow-xs flex flex-col md:flex-row md:items-center justify-between gap-5">
        <div>
          <div className="flex items-center gap-2">
            <span className="text-xs font-bold uppercase tracking-wider text-slate-400">
              {locale === 'vi-VN' ? 'Tổng số dư khả dụng' : 'Total Balance'}
            </span>
            <button
              type="button"
              onClick={toggleVisibility}
              className="p-1 rounded-md text-slate-400 hover:text-slate-600 dark:hover:text-slate-200 hover:bg-slate-100 dark:hover:bg-slate-800 transition cursor-pointer"
            >
              {isHidden ? <EyeOff className="h-3.5 w-3.5" /> : <Eye className="h-3.5 w-3.5" />}
            </button>
            <span className="text-[10px] font-bold px-2 py-0.5 rounded-full bg-cyan-100 dark:bg-cyan-950/60 text-cyan-800 dark:text-cyan-300">
              Layout B (Efficient)
            </span>
          </div>
          <div className="mt-1 flex items-baseline font-mono">
            <h2 className="text-3xl font-extrabold tracking-tight text-slate-900 dark:text-white tabular-nums">
              {isHidden ? '••••••••' : formatMoney(totalBalance, locale)}
            </h2>
          </div>
        </div>

        {/* Action Toolbar */}
        <div className="flex flex-wrap items-center gap-2.5">
          <Button
            size="sm"
            variant="primary"
            onClick={() => onOpenPopup?.('wallet-topup')}
            className="text-xs font-semibold gap-1.5 cursor-pointer"
          >
            <ArrowDownLeft className="h-3.5 w-3.5" />
            <span>{locale === 'vi-VN' ? 'Nạp tiền ví' : 'Top Up'}</span>
          </Button>

          <Button
            size="sm"
            variant="outline"
            onClick={() => onOpenPopup?.('wallet-transfer')}
            className="text-xs font-semibold gap-1.5 text-slate-700 dark:text-slate-300 border-slate-200 dark:border-slate-700 hover:bg-slate-50 dark:hover:bg-slate-800 cursor-pointer"
          >
            <ArrowLeftRight className="h-3.5 w-3.5" />
            <span>{t('wallet.transferBetween')}</span>
          </Button>

          <Button
            size="sm"
            variant="outline"
            onClick={() => onOpenPopup?.('wallet-link-challenge')}
            className="text-xs font-semibold gap-1.5 text-emerald-700 dark:text-emerald-300 border-emerald-200 dark:border-emerald-800/60 hover:bg-emerald-50 dark:hover:bg-emerald-950/40 cursor-pointer"
          >
            <Link2 className="h-3.5 w-3.5" />
            <span>{t('wallet.linkBankBtn')}</span>
          </Button>

          <Button
            size="sm"
            variant="outline"
            onClick={() => onOpenPopup?.('wallet-create')}
            className="text-xs font-semibold gap-1.5 text-slate-700 dark:text-slate-300 border-slate-200 dark:border-slate-700 hover:bg-slate-50 dark:hover:bg-slate-800 cursor-pointer"
          >
            <Plus className="h-3.5 w-3.5" />
            <span>+ Thêm ví</span>
          </Button>
        </div>
      </div>

      {/* 2. Asymmetric Two-Column Grid (7 cols / 5 cols) */}
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 items-start">
        {/* Left (7 cols): Wallets List + Bank Transactions Ledger */}
        <div className="lg:col-span-7 space-y-6">
          {/* Wallets 2-column cards */}
          <div className="bg-white dark:bg-slate-900 rounded-2xl p-5 border border-slate-200/80 dark:border-slate-800 shadow-xs space-y-4">
            <div className="flex items-center justify-between pb-3 border-b border-slate-100 dark:border-slate-800">
              <h3 className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400 flex items-center gap-2">
                <Wallet className="h-3.5 w-3.5 text-emerald-500" />
                <span>Ví nội bộ ({wallets.length})</span>
              </h3>
              <button className="text-xs text-emerald-600 dark:text-emerald-400 font-bold hover:underline">
                + Thêm ví
              </button>
            </div>

            <div className="grid grid-cols-1 sm:grid-cols-2 gap-3.5">
              {wallets.map((wallet) => (
                <div
                  key={wallet.id}
                  className={cn(
                    'p-4 rounded-xl border flex flex-col justify-between min-h-36 transition-all',
                    wallet.isDefault
                      ? 'border-emerald-500/80 bg-emerald-50/20 dark:bg-emerald-950/10'
                      : 'border-slate-200/80 dark:border-slate-800 bg-slate-50/50 dark:bg-slate-800/30'
                  )}
                >
                  <div className="flex items-start justify-between gap-1">
                    <div>
                      <span className="text-xs font-bold text-slate-900 dark:text-white block truncate">
                        {wallet.name}
                      </span>
                      <span className="text-[10px] text-slate-400 font-mono">
                        {wallet.type === 'spending' ? 'Ví chi tiêu' : 'Ví tiết kiệm'}
                      </span>
                    </div>
                    {wallet.isDefault && (
                      <span className="text-[9px] font-black uppercase px-1.5 py-0.5 rounded bg-emerald-100 text-emerald-800 dark:bg-emerald-950 dark:text-emerald-300">
                        Default
                      </span>
                    )}
                  </div>

                  <div className="pt-2 border-t border-slate-200/60 dark:border-slate-800/60 flex items-end justify-between">
                    <div>
                      <span className="text-[9px] text-slate-400 block uppercase">Khả dụng</span>
                      <span className="text-base font-bold font-mono text-slate-900 dark:text-white tabular-nums">
                        {isHidden ? '••••••••' : formatMoney(wallet.balance, locale)}
                      </span>
                    </div>
                    <button className="text-[11px] text-slate-500 hover:text-slate-900 font-medium">Sửa</button>
                  </div>
                </div>
              ))}
            </div>
          </div>

          {/* Bank Transactions Ledger */}
          <div className="bg-white dark:bg-slate-900 rounded-2xl border border-slate-200/80 dark:border-slate-800 shadow-xs overflow-hidden">
            <div className="p-4 border-b border-slate-100 dark:border-slate-800 flex items-center justify-between">
              <h3 className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400 flex items-center gap-1.5">
                <span>Biến động số dư ngân hàng</span>
              </h3>

              <div className="flex items-center gap-1 bg-slate-100 dark:bg-slate-800 p-0.5 rounded-lg text-[11px] font-bold">
                <button
                  type="button"
                  onClick={() => setBankTxFilter('ALL')}
                  className={cn('px-2 py-0.5 rounded-md cursor-pointer', bankTxFilter === 'ALL' ? 'bg-white dark:bg-slate-900 shadow-xs' : 'text-slate-500')}
                >
                  Tất cả
                </button>
                <button
                  type="button"
                  onClick={() => setBankTxFilter('Credit')}
                  className={cn('px-2 py-0.5 rounded-md cursor-pointer', bankTxFilter === 'Credit' ? 'bg-emerald-600 text-white' : 'text-slate-500')}
                >
                  Thu
                </button>
                <button
                  type="button"
                  onClick={() => setBankTxFilter('Debit')}
                  className={cn('px-2 py-0.5 rounded-md cursor-pointer', bankTxFilter === 'Debit' ? 'bg-rose-600 text-white' : 'text-slate-500')}
                >
                  Chi
                </button>
              </div>
            </div>

            <div className="divide-y divide-slate-100 dark:divide-slate-800">
              {filteredBankTransactions.length === 0 ? (
                <p className="text-xs text-slate-400 py-8 text-center">Chưa có giao dịch phù hợp.</p>
              ) : (
                filteredBankTransactions.slice(0, 5).map((tx) => (
                  <div key={tx.id} className="p-3.5 flex items-center justify-between gap-2 hover:bg-slate-50/50 dark:hover:bg-slate-800/20 transition">
                    <div className="flex items-center gap-2.5 min-w-0">
                      <div className={cn(
                        'h-8 w-8 rounded-lg flex items-center justify-center shrink-0 font-bold',
                        tx.direction === 'Credit' ? 'bg-emerald-100 text-emerald-600 dark:bg-emerald-950/60 dark:text-emerald-400' : 'bg-slate-100 text-slate-600 dark:bg-slate-800 dark:text-slate-400'
                      )}>
                        {tx.direction === 'Credit' ? <ArrowDownLeft className="h-3.5 w-3.5" /> : <ArrowUpRight className="h-3.5 w-3.5" />}
                      </div>
                      <div className="min-w-0">
                        <p className="text-xs font-bold text-slate-900 dark:text-white truncate">
                          {tx.bankBrandName} • {tx.content}
                        </p>
                        <p className="text-[10px] text-slate-400 font-mono">
                          {formatDate(tx.transactionDate, locale)}
                        </p>
                      </div>
                    </div>

                    <div className="text-right font-mono shrink-0">
                      <span className={cn(
                        'text-xs font-bold tabular-nums block',
                        tx.direction === 'Credit' ? 'text-emerald-600 dark:text-emerald-400' : 'text-slate-900 dark:text-white'
                      )}>
                        {tx.direction === 'Credit' ? '+' : '−'}
                        {formatMoney(Math.abs(tx.amount), locale, { isHidden })}
                      </span>
                    </div>
                  </div>
                ))
              )}
            </div>
          </div>
        </div>

        {/* Right (5 cols): SePay Banking Hub & Security Card */}
        <div className="lg:col-span-5 space-y-6">
          <div className="bg-white dark:bg-slate-900 rounded-2xl p-5 border border-slate-200/80 dark:border-slate-800 shadow-xs space-y-4">
            <div className="flex items-center justify-between pb-3 border-b border-slate-100 dark:border-slate-800">
              <h3 className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400 flex items-center gap-2">
                <Building2 className="h-3.5 w-3.5 text-blue-500" />
                <span>SePay Open Banking Hub</span>
              </h3>
              <button className="text-xs text-blue-600 dark:text-blue-400 font-bold hover:underline">
                + Kết nối
              </button>
            </div>

            <div className="space-y-3">
              {bankConnections.map((conn) => (
                <div
                  key={conn.id}
                  className="p-4 rounded-xl border border-slate-200/70 dark:border-slate-800 bg-slate-50/50 dark:bg-slate-800/20 space-y-2.5"
                >
                  <div className="flex items-center justify-between">
                    <div className="flex items-center gap-2.5">
                      <div className="h-8 w-8 rounded-lg bg-blue-100 dark:bg-blue-950/60 text-blue-600 dark:text-blue-400 flex items-center justify-center font-bold text-[10px]">
                        {conn.bankBrandName.substring(0, 3)}
                      </div>
                      <div>
                        <span className="text-xs font-bold text-slate-900 dark:text-white block">{conn.bankBrandName}</span>
                        <span className="text-[10px] text-slate-400 font-mono">{conn.maskedAccountNumber}</span>
                      </div>
                    </div>
                    <span className="text-[9px] font-bold px-2 py-0.5 rounded-full bg-emerald-100 text-emerald-800 dark:bg-emerald-950 dark:text-emerald-300">
                      {conn.status}
                    </span>
                  </div>

                  <div className="pt-2 border-t border-slate-200/60 dark:border-slate-800 flex items-center justify-between text-[11px]">
                    <span className="text-slate-400 truncate">{conn.accountHolderName}</span>
                    <button className="text-emerald-600 dark:text-emerald-400 font-semibold hover:underline">
                      Nạp tiền
                    </button>
                  </div>
                </div>
              ))}
            </div>
          </div>

          {/* Security Assurance Card */}
          <div className="p-4 rounded-2xl bg-slate-50 dark:bg-slate-800/40 border border-slate-200/70 dark:border-slate-800 space-y-2 text-xs">
            <div className="flex items-center gap-2 font-bold text-slate-900 dark:text-white">
              <ShieldCheck className="h-4 w-4 text-emerald-600" />
              <span>Bảo mật Open Banking SePay</span>
            </div>
            <p className="text-[11px] text-slate-500 dark:text-slate-400 leading-relaxed">
              Mọi giao dịch nạp tiền và liên kết tài khoản ngân hàng đều được bảo mật qua chuẩn Open Banking API và đối soát thời gian thực.
            </p>
          </div>
        </div>
      </div>
    </div>
  );
};
