import React, { useState } from 'react';
import {
  Send,
  ShieldCheck,
  CheckCircle2,
  Clock,
  Smartphone,
  ArrowRight,
  History,
} from 'lucide-react';
import { useI18n } from '../../../i18n/I18nContext';
import { useFinancial } from '../../../providers/FinancialProvider';
import { formatMoney } from '../../../utils/money';
import { formatDate } from '../../../utils/date';
import { Button } from '../../../components/ui/Button';
import { cn } from '../../../utils/cn';

/**
 * TRANSFER LAYOUT B — EFFICIENT WORKSPACE
 * Bố cục:
 * - Cột trái (7 cols): Khối nhập liệu chuyển tiền tinh gọn với thanh chọn ví nguồn & nhập SĐT ngay cạnh nhau.
 * - Cột phải (5 cols): Sổ cái lịch sử lệnh chuyển tiền gần đây (Recent transfers) kèm biên lai tra cứu nhanh.
 */
export interface TransferLayoutProps {
  onOpenPopup?: (popupId: string, mode?: 'default' | 'prefill') => void;
}

export const TransferLayoutB: React.FC<TransferLayoutProps> = ({ onOpenPopup }) => {
  const { t, locale } = useI18n();
  const { wallets } = useFinancial();

  const sourceWallet = wallets.find((w) => w.isDefault) || wallets[0];
  const [recipientPhone, setRecipientPhone] = useState('0988776655');
  const [amount, setAmount] = useState('500000');
  const [note, setNote] = useState('Góp quỹ tháng này');

  return (
    <div className="space-y-6">
      {/* 1. Header Banner */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 pb-2 border-b border-slate-200/80 dark:border-slate-800">
        <div>
          <h2 className="text-xl font-extrabold tracking-tight text-slate-900 dark:text-white flex items-center gap-2">
            <Send className="h-5 w-5 text-cyan-500" />
            <span>{locale === 'vi-VN' ? 'Cổng chuyển tiền tức thời' : 'Transfer Terminal'}</span>
          </h2>
          <p className="text-xs text-slate-500 dark:text-slate-400 mt-0.5">
            {locale === 'vi-VN' ? 'Bố cục 2 cột tối ưu: Form chuyển tiền song song với Lịch sử lệnh.' : 'Optimized 2-column workflow.'}
          </p>
        </div>
        <span className="text-[10px] font-bold px-2.5 py-1 rounded-full bg-cyan-100 dark:bg-cyan-950/60 text-cyan-800 dark:text-cyan-300">
          Layout B (Efficient Workspace)
        </span>
      </div>

      {/* 2. Side-by-Side Dual Pane Layout (7 cols / 5 cols) */}
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 items-start">
        {/* Left Column (7 cols): Compact Transfer Execution Terminal */}
        <div className="lg:col-span-7 bg-white dark:bg-slate-900 rounded-2xl p-5 border border-slate-200/80 dark:border-slate-800 shadow-xs space-y-4">
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-3.5">
            <div className="space-y-1">
              <label className="text-[11px] font-bold uppercase tracking-wider text-slate-400">Ví nguồn</label>
              <div className="p-2.5 rounded-xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-xs flex justify-between items-center">
                <span className="font-bold">{sourceWallet?.name || 'Ví chính'}</span>
                <span className="font-mono text-emerald-600 font-bold">{formatMoney(sourceWallet?.balance || 0, locale)}</span>
              </div>
            </div>

            <div className="space-y-1">
              <label className="text-[11px] font-bold uppercase tracking-wider text-slate-400">SĐT Người nhận</label>
              <input
                type="text"
                value={recipientPhone}
                onChange={(e) => setRecipientPhone(e.target.value)}
                className="w-full px-3 py-2 rounded-xl bg-slate-50 dark:bg-slate-800 border border-slate-200 dark:border-slate-700 text-xs font-mono font-bold"
              />
            </div>
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 gap-3.5">
            <div className="space-y-1">
              <label className="text-[11px] font-bold uppercase tracking-wider text-slate-400">Số tiền (VND)</label>
              <input
                type="text"
                value={amount}
                onChange={(e) => setAmount(e.target.value)}
                className="w-full px-3 py-2 rounded-xl bg-slate-50 dark:bg-slate-800 border border-slate-200 dark:border-slate-700 text-sm font-mono font-bold"
              />
            </div>

            <div className="space-y-1">
              <label className="text-[11px] font-bold uppercase tracking-wider text-slate-400">Lời nhắn</label>
              <input
                type="text"
                value={note}
                onChange={(e) => setNote(e.target.value)}
                className="w-full px-3 py-2 rounded-xl bg-slate-50 dark:bg-slate-800 border border-slate-200 dark:border-slate-700 text-xs"
              />
            </div>
          </div>

          <div className="pt-2">
            <Button
              size="md"
              variant="primary"
              onClick={() => onOpenPopup?.('transfer-review-modal')}
              className="w-full font-bold text-xs gap-2 cursor-pointer shadow-xs"
            >
              <ShieldCheck className="h-4 w-4" />
              <span>Chuyển tiền & Xác nhận PIN 6 số</span>
            </Button>
          </div>
        </div>

        {/* Right Column (5 cols): Recent Transfer Logs */}
        <div className="lg:col-span-5 bg-white dark:bg-slate-900 rounded-2xl border border-slate-200/80 dark:border-slate-800 shadow-xs overflow-hidden">
          <div className="p-4 border-b border-slate-100 dark:border-slate-800 flex items-center justify-between">
            <h3 className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400 flex items-center gap-1.5">
              <History className="h-3.5 w-3.5 text-cyan-500" />
              <span>Lịch sử chuyển gần đây</span>
            </h3>
            <span className="text-[10px] text-slate-400">Thời gian thực</span>
          </div>

          <div className="divide-y divide-slate-100 dark:divide-slate-800 text-xs">
            {[
              { name: 'Nguyễn Văn A', phone: '0912***678', amt: 250000, time: 'Hôm nay 10:15', status: 'Hoàn thành' },
              { name: 'Trần Thị B', phone: '0901***888', amt: 120000, time: 'Hôm qua 16:30', status: 'Hoàn thành' },
              { name: 'Lê Văn C', phone: '0988***123', amt: 500000, time: '11/10/2026', status: 'Hoàn thành' },
            ].map((item, idx) => (
              <div key={idx} className="p-3.5 flex items-center justify-between gap-2 hover:bg-slate-50/50 dark:hover:bg-slate-800/20">
                <div>
                  <span className="font-bold text-slate-900 dark:text-white block">{item.name}</span>
                  <span className="text-[10px] text-slate-400 font-mono">{item.time}</span>
                </div>
                <div className="text-right font-mono">
                  <span className="font-bold text-rose-600 block">−{formatMoney(item.amt, locale)}</span>
                  <span className="text-[9px] text-emerald-600 font-semibold">{item.status}</span>
                </div>
              </div>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
};
