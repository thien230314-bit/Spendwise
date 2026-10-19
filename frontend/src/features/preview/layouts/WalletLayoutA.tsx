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
 * WALLET LAYOUT A — BALANCED WORKSPACE
 * Bố cục:
 * - Hàng 1: Tổng số dư ví khả dụng & 3 nút hành động chính (Nạp tiền, Chuyển giữa ví, Liên kết ngân hàng).
 * - Hàng 2: Danh sách ví nội bộ SpendWise (Grid 3 cột cân xứng, phân tách rõ thẻ Mặc định).
 * - Hàng 3: Phân bổ 2 cột song song (6 cols / 6 cols):
 *   + Cột trái: Trung tâm ngân hàng liên kết SePay Open Banking.
 *   + Cột phải: Bảng biến động số dư ngân hàng kèm bộ lọc Tất cả / Thu / Chi.
 */
export interface WalletLayoutProps {
  onOpenPopup?: (popupId: string, mode?: 'default' | 'prefill') => void;
}

export const WalletLayoutA: React.FC<WalletLayoutProps> = ({ onOpenPopup }) => {
  const { t, locale } = useI18n();
  const { isHidden, toggleVisibility } = useBalanceVisibility();
  const { wallets, bankConnections, bankTransactions, isLoading } = useFinancial();

  const [bankTxFilter, setBankTxFilter] = useState<'ALL' | 'Credit' | 'Debit'>('ALL');
  const [selectedWalletId, setSelectedWalletId] = useState<string>(wallets[0]?.id || '');

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
      {/* 1. Wallet Total Balance Hero Card */}
      <div className="relative overflow-hidden rounded-3xl bg-white dark:bg-[#0F172A] text-slate-900 dark:text-white p-6 sm:p-8 shadow-sm border border-slate-100 dark:border-slate-800">
        <div className="relative z-10 flex flex-col md:flex-row md:items-center justify-between gap-6">
          <div className="space-y-2">
            <div className="flex items-center gap-2 text-xs font-semibold text-slate-500 dark:text-slate-400 uppercase tracking-wider">
              <span>{t('wallet.totalAvailable')}</span>
              <button
                type="button"
                onClick={toggleVisibility}
                className="p-1 text-slate-400 hover:text-slate-700 dark:hover:text-white transition-colors cursor-pointer"
                title={isHidden ? t('common.showBalance') : t('common.hideBalance')}
              >
                {isHidden ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
              </button>
              <span className="text-[10px] font-bold px-2 py-0.5 rounded-full bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-300">
                Layout A (Balanced)
              </span>
            </div>
            <div className="text-3xl sm:text-4xl font-bold tracking-tight text-slate-900 dark:text-white tabular-nums">
              {isHidden ? '••••••••' : formatMoney(totalBalance, locale)}
            </div>
            <p className="text-xs text-slate-500 dark:text-slate-400">
              {locale === 'vi-VN' ? 'Bảo lưu 100% chức năng ví nội bộ & cổng liên kết Open Banking.' : 'Full parity internal wallets & Open Banking hub.'}
            </p>
          </div>

          {/* Action Toolbar */}
          <div className="flex flex-wrap items-center gap-3">
            <button
              type="button"
              onClick={() => onOpenPopup?.('wallet-topup')}
              className="flex items-center gap-2 px-4 py-2.5 rounded-2xl bg-blue-600 hover:bg-blue-500 text-white text-xs font-bold shadow-xs transition-colors cursor-pointer"
            >
              <ArrowDownLeft className="h-4 w-4" />
              <span>{locale === 'vi-VN' ? 'Nạp tiền ví' : 'Top Up'}</span>
            </button>

            <button
              type="button"
              onClick={() => onOpenPopup?.('wallet-transfer')}
              className="flex items-center gap-2 px-4 py-2.5 rounded-2xl bg-slate-900 hover:bg-slate-800 text-white dark:bg-white dark:text-slate-900 dark:hover:bg-slate-100 text-xs font-bold shadow-xs transition-colors cursor-pointer"
            >
              <ArrowLeftRight className="h-4 w-4" />
              <span>{t('wallet.transferBetween')}</span>
            </button>

            <button
              type="button"
              onClick={() => onOpenPopup?.('wallet-link-challenge')}
              className="flex items-center gap-2 px-4 py-2.5 rounded-2xl bg-emerald-600 hover:bg-emerald-700 text-white text-xs font-bold transition-colors cursor-pointer"
            >
              <Link2 className="h-4 w-4" />
              <span>{t('wallet.linkBankBtn')}</span>
            </button>

            <button
              type="button"
              onClick={() => onOpenPopup?.('wallet-create')}
              className="flex items-center gap-2 px-4 py-2.5 rounded-2xl bg-slate-100 hover:bg-slate-200 dark:bg-slate-800 dark:hover:bg-slate-700 text-slate-800 dark:text-slate-200 text-xs font-bold transition-colors cursor-pointer"
            >
              <Plus className="h-4 w-4" />
              <span>+ Thêm ví mới</span>
            </button>
          </div>
        </div>

        <div className="absolute -bottom-16 -right-16 w-64 h-64 bg-emerald-500/10 rounded-full blur-3xl pointer-events-none" />
      </div>

      {/* 2. Internal SpendWise Wallets Deck (Full-Width 3-Column Grid) */}
      <div className="space-y-4">
        <div className="flex items-center justify-between">
          <h3 className="text-sm font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400 flex items-center gap-2">
            <Wallet className="h-4 w-4 text-emerald-500" />
            <span>{locale === 'vi-VN' ? 'Danh sách ví nội bộ SpendWise' : 'SpendWise Internal Wallets'}</span>
          </h3>
          <span className="text-xs text-slate-400">
            {t('wallet.accountsCount', { count: wallets.length })}
          </span>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-3 gap-5">
          {wallets.map((wallet) => (
            <div
              key={wallet.id}
              className={cn(
                'rounded-3xl p-6 bg-white dark:bg-slate-900 border transition-all flex flex-col justify-between min-h-48 shadow-xs',
                wallet.isDefault
                  ? 'border-emerald-500/80 ring-1 ring-emerald-500/20'
                  : 'border-slate-100 dark:border-slate-800'
              )}
            >
              <div>
                <div className="flex items-start justify-between gap-2 mb-2">
                  <div className="flex items-center gap-2">
                    <span className="text-xs font-bold text-slate-900 dark:text-white truncate">
                      {wallet.name}
                    </span>
                    {wallet.isDefault && (
                      <span className="text-[10px] font-black uppercase px-2 py-0.5 rounded-full bg-emerald-100 text-emerald-800 dark:bg-emerald-950 dark:text-emerald-300">
                        {locale === 'vi-VN' ? 'Mặc định' : 'Default'}
                      </span>
                    )}
                  </div>
                  <span className="text-[10px] text-slate-400 font-mono">
                    {wallet.type === 'spending' ? 'Chi tiêu' : wallet.type === 'savings' ? 'Tiết kiệm' : 'Dự phòng'}
                  </span>
                </div>
                <p className="text-[11px] text-slate-400 font-mono">{wallet.id.substring(0, 12)}</p>
              </div>

              <div className="pt-4 border-t border-slate-100 dark:border-slate-800 flex items-end justify-between">
                <div>
                  <span className="text-[10px] text-slate-400 block uppercase font-medium">Khả dụng</span>
                  <span className="text-lg font-bold font-mono text-slate-900 dark:text-white tabular-nums">
                    {isHidden ? '••••••••' : formatMoney(wallet.balance, locale)}
                  </span>
                </div>
                <div className="flex items-center gap-2">
                  <button
                    type="button"
                    onClick={() => onOpenPopup?.('wallet-edit', 'prefill')}
                    className="text-xs text-slate-500 hover:text-slate-900 dark:hover:text-white font-medium p-1 cursor-pointer"
                  >
                    Sửa
                  </button>
                  <button
                    type="button"
                    onClick={() => onOpenPopup?.('wallet-close')}
                    className="text-xs text-rose-500 hover:text-rose-700 font-medium p-1 cursor-pointer"
                  >
                    Đóng
                  </button>
                </div>
              </div>
            </div>
          ))}
        </div>
      </div>

      {/* 3. Balanced Tier (6 cols / 6 cols): SePay Banking Hub + Bank Balance Ledger */}
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 items-start">
        {/* Left Column (6 cols): SePay Open Banking Hub */}
        <div className="lg:col-span-6 space-y-4">
          <div className="flex items-center justify-between">
            <h3 className="text-sm font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400 flex items-center gap-2">
              <Building2 className="h-4 w-4 text-blue-500" />
              <span>{locale === 'vi-VN' ? 'Tài khoản ngân hàng liên kết (SePay)' : 'Linked Bank Accounts (SePay)'}</span>
            </h3>
            <span className="text-xs text-slate-400">
              {bankConnections.length} liên kết
            </span>
          </div>

          <div className="space-y-3">
            {bankConnections.length === 0 ? (
              <div className="p-8 rounded-2xl bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 text-center space-y-2">
                <Building2 className="h-8 w-8 text-slate-300 dark:text-slate-600 mx-auto" />
                <p className="text-xs font-semibold text-slate-600 dark:text-slate-300">
                  {locale === 'vi-VN' ? 'Chưa có tài khoản ngân hàng nào được liên kết.' : 'No bank accounts linked.'}
                </p>
              </div>
            ) : (
              bankConnections.map((conn) => (
                <div
                  key={conn.id}
                  className="p-5 rounded-2xl bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 shadow-xs space-y-3"
                >
                  <div className="flex items-center justify-between">
                    <div className="flex items-center gap-3">
                      <div className="h-10 w-10 rounded-xl bg-slate-100 dark:bg-slate-800 flex items-center justify-center font-bold text-xs text-slate-700 dark:text-slate-300">
                        {conn.bankBrandName.substring(0, 3).toUpperCase()}
                      </div>
                      <div>
                        <h4 className="text-sm font-bold text-slate-900 dark:text-white">
                          {conn.bankBrandName}
                        </h4>
                        <p className="text-xs text-slate-400 font-mono">
                          {conn.maskedAccountNumber || '••••••••'}
                        </p>
                      </div>
                    </div>

                    <span className={cn(
                      'text-[10px] font-bold px-2 py-0.5 rounded-full',
                      conn.status === 'Active'
                        ? 'bg-emerald-100 dark:bg-emerald-950 text-emerald-700 dark:text-emerald-300'
                        : 'bg-slate-200 dark:bg-slate-800 text-slate-600 dark:text-slate-400'
                    )}>
                      {conn.status === 'Active' ? 'Hoạt động' : conn.status}
                    </span>
                  </div>

                  <div className="pt-3 border-t border-slate-100 dark:border-slate-800 flex items-center justify-between text-xs">
                    <span className="text-[11px] text-slate-400">
                      {conn.accountHolderName}
                    </span>
                    <div className="flex items-center gap-3">
                      <button className="text-emerald-600 dark:text-emerald-400 font-semibold hover:underline">
                        Nạp tiền test
                      </button>
                      <button className="text-rose-500 font-semibold hover:underline">
                        Ngắt kết nối
                      </button>
                    </div>
                  </div>
                </div>
              ))
            )}
          </div>
        </div>

        {/* Right Column (6 cols): Bank Transactions Ledger with Filter */}
        <div className="lg:col-span-6 bg-white dark:bg-slate-900 rounded-2xl border border-slate-200/80 dark:border-slate-800 shadow-xs overflow-hidden">
          <div className="p-5 border-b border-slate-100 dark:border-slate-800 flex flex-col sm:flex-row sm:items-center justify-between gap-3">
            <div>
              <h3 className="text-sm font-bold text-slate-900 dark:text-white">
                {locale === 'vi-VN' ? 'Biến động số dư ngân hàng' : 'Bank Balance Ledger'}
              </h3>
              <p className="text-xs text-slate-400 mt-0.5">
                {locale === 'vi-VN' ? 'Dữ liệu nhận tự động từ SePay Webhook' : 'Live webhook feed from SePay'}
              </p>
            </div>

            <div className="flex items-center gap-1 bg-slate-100 dark:bg-slate-800 p-1 rounded-xl self-start sm:self-auto text-xs font-bold">
              <button
                type="button"
                onClick={() => setBankTxFilter('ALL')}
                className={cn('px-2.5 py-1 rounded-lg cursor-pointer', bankTxFilter === 'ALL' ? 'bg-white dark:bg-slate-900 shadow-xs' : 'text-slate-500')}
              >
                Tất cả
              </button>
              <button
                type="button"
                onClick={() => setBankTxFilter('Credit')}
                className={cn('px-2.5 py-1 rounded-lg cursor-pointer', bankTxFilter === 'Credit' ? 'bg-emerald-600 text-white' : 'text-slate-500')}
              >
                Tiền vào
              </button>
              <button
                type="button"
                onClick={() => setBankTxFilter('Debit')}
                className={cn('px-2.5 py-1 rounded-lg cursor-pointer', bankTxFilter === 'Debit' ? 'bg-rose-600 text-white' : 'text-slate-500')}
              >
                Tiền ra
              </button>
            </div>
          </div>

          <div className="divide-y divide-slate-100 dark:divide-slate-800">
            {filteredBankTransactions.length === 0 ? (
              <p className="text-xs text-slate-400 py-12 text-center">
                {locale === 'vi-VN' ? 'Chưa có biến động số dư nào phù hợp bộ lọc.' : 'No bank transactions found.'}
              </p>
            ) : (
              filteredBankTransactions.slice(0, 5).map((tx) => (
                <div key={tx.id} className="p-4 flex items-center justify-between gap-3 hover:bg-slate-50/50 dark:hover:bg-slate-800/30 transition">
                  <div className="flex items-center gap-3 min-w-0">
                    <div className={cn(
                      'h-9 w-9 rounded-xl flex items-center justify-center shrink-0 font-bold',
                      tx.direction === 'Credit'
                        ? 'bg-emerald-100 text-emerald-600 dark:bg-emerald-950/60 dark:text-emerald-400'
                        : 'bg-slate-100 text-slate-600 dark:bg-slate-800 dark:text-slate-400'
                    )}>
                      {tx.direction === 'Credit' ? <ArrowDownLeft className="h-4 w-4" /> : <ArrowUpRight className="h-4 w-4" />}
                    </div>
                    <div className="min-w-0">
                      <p className="text-xs font-bold text-slate-900 dark:text-white truncate">
                        {tx.bankBrandName} • {tx.content}
                      </p>
                      <p className="text-[10px] text-slate-400 font-mono">
                        {formatDate(tx.transactionDate, locale)} • {tx.referenceCode || tx.paymentCode}
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
    </div>
  );
};
