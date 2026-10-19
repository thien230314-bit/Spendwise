import React, { useState } from 'react';
import {
  Send,
  ShieldCheck,
  CheckCircle2,
  Clock,
  Smartphone,
  Info,
  Users,
  ArrowRight,
} from 'lucide-react';
import { useI18n } from '../../../i18n/I18nContext';
import { useFinancial } from '../../../providers/FinancialProvider';
import { formatMoney } from '../../../utils/money';
import { formatDate } from '../../../utils/date';
import { Button } from '../../../components/ui/Button';
import { cn } from '../../../utils/cn';

/**
 * TRANSFER LAYOUT A — BALANCED WORKSPACE
 * Bố cục:
 * - Form chuyển tiền tuần tự (Source -> Recipient -> Amount) đặt ở trung tâm (7 cols).
 * - Cột bên phải (5 cols): Thẻ bạn bè gợi ý chuyển nhanh & Khối đảm bảo an toàn giao dịch PIN 6 số.
 * - Hàng dưới: Lịch sử các lệnh chuyển tiền gần đây (Full-width).
 */
export interface TransferLayoutProps {
  onOpenPopup?: (popupId: string, mode?: 'default' | 'prefill') => void;
}

export const TransferLayoutA: React.FC<TransferLayoutProps> = ({ onOpenPopup }) => {
  const { t, locale } = useI18n();
  const { wallets } = useFinancial();

  const sourceWallet = wallets.find((w) => w.isDefault) || wallets[0];
  const [recipientPhone, setRecipientPhone] = useState('0912345678');
  const [amount, setAmount] = useState('200000');
  const [note, setNote] = useState('Chuyển tiền ăn trưa');

  return (
    <div className="space-y-6">
      {/* 1. Header Banner */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 pb-2 border-b border-slate-200/80 dark:border-slate-800">
        <div>
          <h2 className="text-xl font-extrabold tracking-tight text-slate-900 dark:text-white flex items-center gap-2">
            <Send className="h-5 w-5 text-emerald-500" />
            <span>{locale === 'vi-VN' ? 'Chuyển tiền trong SpendWise' : 'Direct Transfer'}</span>
          </h2>
          <p className="text-xs text-slate-500 dark:text-slate-400 mt-0.5">
            {locale === 'vi-VN' ? 'Chuyển tiền tức thì qua số điện thoại, bảo mật mã PIN 6 số.' : 'Instant transfer via verified phone.'}
          </p>
        </div>
        <span className="text-[10px] font-bold px-2.5 py-1 rounded-full bg-slate-100 dark:bg-slate-800 text-slate-700 dark:text-slate-300">
          Layout A (Balanced Workspace)
        </span>
      </div>

      {/* 2. Main Transfer Form & Friends Helper Grid (7 cols / 5 cols) */}
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 items-start">
        {/* Left Column (7 cols): Main Input Form */}
        <div className="lg:col-span-7 bg-white dark:bg-slate-900 rounded-2xl p-6 border border-slate-200/80 dark:border-slate-800 shadow-xs space-y-5">
          {/* Step 1: Source Wallet */}
          <div className="space-y-1.5">
            <label className="text-xs font-bold uppercase tracking-wider text-slate-400">1. Ví nguồn trích tiền</label>
            <div className="p-3.5 rounded-xl border border-emerald-500/80 bg-emerald-50/20 dark:bg-emerald-950/20 flex items-center justify-between">
              <div>
                <span className="text-xs font-bold text-slate-900 dark:text-white block">{sourceWallet?.name || 'Ví chính'}</span>
                <span className="text-[10px] text-slate-400 font-mono">SPW-PRIMARY</span>
              </div>
              <span className="text-xs font-bold font-mono text-emerald-600 dark:text-emerald-400">
                {formatMoney(sourceWallet?.balance || 0, locale)}
              </span>
            </div>
          </div>

          {/* Step 2: Recipient Phone */}
          <div className="space-y-1.5">
            <label className="text-xs font-bold uppercase tracking-wider text-slate-400">2. Số điện thoại người nhận</label>
            <div className="relative">
              <Smartphone className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-slate-400" />
              <input
                type="text"
                value={recipientPhone}
                onChange={(e) => setRecipientPhone(e.target.value)}
                placeholder="Nhập 10 số điện thoại VN..."
                className="w-full pl-9 pr-4 py-2.5 rounded-xl bg-slate-50 dark:bg-slate-800 border border-slate-200 dark:border-slate-700 text-xs font-mono font-bold focus:outline-none focus:ring-1 focus:ring-emerald-500"
              />
            </div>
            <p className="text-[11px] text-emerald-600 dark:text-emerald-400 font-semibold flex items-center gap-1">
              <CheckCircle2 className="h-3.5 w-3.5" />
              <span>Đã tìm thấy: NGUYEN VAN A (Tài khoản đang hoạt động)</span>
            </p>
          </div>

          {/* Step 3: Amount & Note */}
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div className="space-y-1.5">
              <label className="text-xs font-bold uppercase tracking-wider text-slate-400">3. Số tiền (VND)</label>
              <input
                type="text"
                value={amount}
                onChange={(e) => setAmount(e.target.value)}
                className="w-full px-3 py-2 rounded-xl bg-slate-50 dark:bg-slate-800 border border-slate-200 dark:border-slate-700 text-sm font-mono font-bold"
              />
            </div>
            <div className="space-y-1.5">
              <label className="text-xs font-bold uppercase tracking-wider text-slate-400">Lời nhắn</label>
              <input
                type="text"
                value={note}
                onChange={(e) => setNote(e.target.value)}
                className="w-full px-3 py-2 rounded-xl bg-slate-50 dark:bg-slate-800 border border-slate-200 dark:border-slate-700 text-xs"
              />
            </div>
          </div>

          {/* Action Button */}
          <div className="pt-2">
            <Button
              size="md"
              variant="primary"
              onClick={() => onOpenPopup?.('transfer-review-modal')}
              className="w-full font-bold text-xs gap-2 cursor-pointer shadow-xs"
            >
              <ShieldCheck className="h-4 w-4" />
              <span>Tiếp tục & Xác thực mã PIN bảo mật</span>
            </Button>
          </div>
        </div>

        {/* Right Column (5 cols): Quick Friends Picker & Security Card */}
        <div className="lg:col-span-5 space-y-4">
          <div className="p-5 rounded-2xl bg-white dark:bg-slate-900 border border-slate-200/80 dark:border-slate-800 shadow-xs space-y-3">
            <h3 className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400 flex items-center gap-1.5">
              <Users className="h-3.5 w-3.5 text-indigo-500" />
              <span>Gợi ý bạn bè chuyển nhanh</span>
            </h3>

            <div className="space-y-2">
              {['Trần Thị B (0901***888)', 'Lê Văn C (0988***123)', 'Phạm Hoàng D (0934***555)'].map((friend, idx) => (
                <button
                  key={idx}
                  type="button"
                  onClick={() => setRecipientPhone('090123456' + idx)}
                  className="w-full text-left p-2.5 rounded-xl border border-slate-100 dark:border-slate-800 hover:bg-slate-50 dark:hover:bg-slate-800/40 text-xs flex items-center justify-between cursor-pointer transition"
                >
                  <span className="font-semibold text-slate-800 dark:text-slate-200">{friend}</span>
                  <ArrowRight className="h-3.5 w-3.5 text-slate-400" />
                </button>
              ))}
            </div>
          </div>

          <div className="p-4 rounded-2xl bg-emerald-50/50 dark:bg-emerald-950/20 border border-emerald-200/60 dark:border-emerald-900/40 text-xs space-y-2">
            <div className="flex items-center gap-2 font-bold text-emerald-800 dark:text-emerald-300">
              <ShieldCheck className="h-4 w-4" />
              <span>Xác thực an toàn 100%</span>
            </div>
            <p className="text-[11px] text-slate-600 dark:text-slate-400 leading-relaxed">
              Tất cả các giao dịch chuyển tiền đều yêu cầu xác thực bằng mã PIN bảo mật 6 số cá nhân và biên lai lưu trữ điện tử.
            </p>
          </div>
        </div>
      </div>
    </div>
  );
};
