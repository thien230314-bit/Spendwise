import React, { useState } from 'react';
import { cn } from '../../../utils/cn';
import {
  ArrowLeftRight,
  Wallet,
  User,
  ShieldCheck,
  Building2,
  Lock,
  ArrowRight,
  Info,
} from 'lucide-react';
import { formatMoney } from '../../../utils/money';

interface MobileTransferScreenProps {
  theme?: 'light' | 'dark';
  onOpenSheet?: (sheetId: string) => void;
  statusMode?: 'normal' | 'loading' | 'error' | 'success';
}

export const MobileTransferScreen: React.FC<MobileTransferScreenProps> = ({
  theme = 'light',
  onOpenSheet,
  statusMode = 'normal',
}) => {
  const isDark = theme === 'dark';
  const [recipient, setRecipient] = useState('0987654321');
  const [amount, setAmount] = useState('500000');
  const [note, setNote] = useState('Chuyển tiền tiền nhà');

  return (
    <div
      className={cn(
        'flex-1 flex flex-col p-4 space-y-4 overflow-y-auto transition-colors pb-6',
        isDark ? 'bg-[#090D16]' : 'bg-[#F8FAFC]'
      )}
    >
      {/* Transfer Form Notebook Container (matching mobile transfer.tsx) */}
      <div
        className={cn(
          'p-5 rounded-3xl border shadow-sm space-y-4 transition-all',
          isDark
            ? 'bg-[#101622] border-[#1E293B]'
            : 'bg-[#FCFBF7] border-[#E2E8F0]'
        )}
      >
        {/* Step 1: Source Wallet */}
        <div className="space-y-1.5">
          <label className="text-[11px] font-bold text-slate-400 uppercase tracking-wider">
            1. Ví nguồn trích tiền
          </label>
          <div
            className={cn(
              'p-3.5 rounded-2xl border flex items-center justify-between gap-3 cursor-pointer',
              isDark
                ? 'bg-[#151B22] border-[#1E293B]'
                : 'bg-white border-[#E2E8F0]'
            )}
            onClick={() => onOpenSheet?.('wallet-switcher')}
          >
            <div className="flex items-center gap-2.5">
              <div className="w-8 h-8 rounded-xl bg-emerald-100 dark:bg-emerald-950 text-emerald-600 dark:text-emerald-400 flex items-center justify-center">
                <Wallet className="w-4 h-4" />
              </div>
              <div>
                <span className="text-xs font-bold text-slate-800 dark:text-slate-200 block">
                  Ví SpendWise Mặc định
                </span>
                <span className="text-[10px] text-slate-400 font-mono">
                  Khả dụng: 14.250.000 ₫
                </span>
              </div>
            </div>
            <span className="text-xs text-slate-400 font-semibold">Đổi ▾</span>
          </div>
        </div>

        {/* Step 2: Recipient Identifier */}
        <div className="space-y-1.5">
          <label className="text-[11px] font-bold text-slate-400 uppercase tracking-wider">
            2. Người nhận (SĐT / SpendId)
          </label>
          <div
            className={cn(
              'flex items-center gap-2.5 px-3 py-2.5 rounded-2xl border transition-colors',
              isDark
                ? 'bg-[#151B22] border-[#1E293B] text-white'
                : 'bg-white border-[#E2E8F0] text-slate-900'
            )}
          >
            <User className="w-4 h-4 text-slate-400 shrink-0" />
            <input
              type="text"
              value={recipient}
              onChange={(e) => setRecipient(e.target.value)}
              placeholder="Nhập số điện thoại hoặc SpendId"
              className="w-full bg-transparent text-xs outline-none font-mono"
            />
          </div>

          {/* Verified Recipient Card */}
          <div
            className={cn(
              'p-2.5 rounded-xl border flex items-center gap-2.5 text-xs',
              isDark
                ? 'bg-[#052E26]/50 border-emerald-900/50 text-[#34D399]'
                : 'bg-[#ECFDF5] border-emerald-200 text-[#047857]'
            )}
          >
            <ShieldCheck className="w-4 h-4 shrink-0" />
            <div>
              <span className="font-bold block">Trần Thị Bình (SpendId: SW-88412)</span>
              <span className="text-[10px] opacity-80 font-mono">Tài khoản nhận: VCB 0451****89</span>
            </div>
          </div>
        </div>

        {/* Step 3: Amount Input */}
        <div className="space-y-1.5">
          <label className="text-[11px] font-bold text-slate-400 uppercase tracking-wider">
            3. Số tiền chuyển (VND)
          </label>
          <div
            className={cn(
              'px-3 py-3 rounded-2xl border text-xl font-bold font-mono transition-colors flex items-center justify-between',
              isDark
                ? 'bg-[#151B22] border-[#1E293B] text-white'
                : 'bg-white border-[#E2E8F0] text-slate-900'
            )}
          >
            <input
              type="text"
              value={amount}
              onChange={(e) => setAmount(e.target.value)}
              placeholder="0"
              className="w-full bg-transparent outline-none tabular-nums"
            />
            <span className="text-xs text-slate-400 font-sans font-semibold">VND</span>
          </div>

          <div className="flex gap-1.5 pt-1">
            {['100000', '200000', '500000', '1000000'].map((amt) => (
              <button
                key={amt}
                type="button"
                onClick={() => setAmount(amt)}
                className={cn(
                  'flex-1 py-1 rounded-lg border text-[10px] font-mono font-semibold transition-all cursor-pointer',
                  amount === amt
                    ? 'border-emerald-500 bg-emerald-50 dark:bg-emerald-950/60 text-emerald-700 dark:text-emerald-300 font-bold'
                    : 'border-slate-200 dark:border-slate-800 text-slate-500 hover:bg-slate-100 dark:hover:bg-slate-800'
                )}
              >
                {parseInt(amt, 10).toLocaleString('vi-VN')}
              </button>
            ))}
          </div>
        </div>

        {/* Step 4: Note */}
        <div className="space-y-1.5">
          <label className="text-[11px] font-bold text-slate-400 uppercase tracking-wider">
            4. Lời nhắn (Nội dung)
          </label>
          <div
            className={cn(
              'px-3 py-2.5 rounded-2xl border transition-colors',
              isDark
                ? 'bg-[#151B22] border-[#1E293B] text-white'
                : 'bg-white border-[#E2E8F0] text-slate-900'
            )}
          >
            <textarea
              rows={2}
              value={note}
              onChange={(e) => setNote(e.target.value)}
              placeholder="Nội dung chuyển tiền (không bắt buộc)"
              className="w-full bg-transparent text-xs outline-none resize-none"
            />
          </div>
        </div>

        {/* Action Button: Trigger PIN Confirmation Sheet */}
        <button
          type="button"
          onClick={() => onOpenSheet?.('pin-challenge')}
          className="w-full py-3.5 px-4 rounded-2xl bg-emerald-600 hover:bg-emerald-700 active:scale-[0.98] text-white text-xs font-bold transition-all shadow-md flex items-center justify-center gap-2 cursor-pointer"
        >
          <Lock className="w-3.5 h-3.5" />
          <span>Tiếp tục (Nhập mã PIN xác nhận)</span>
          <ArrowRight className="w-3.5 h-3.5" />
        </button>
      </div>

      {/* Safety Notice */}
      <div className="flex items-start gap-2 text-[10px] text-slate-400 px-2">
        <Info className="w-3.5 h-3.5 shrink-0 mt-0.5 text-slate-400" />
        <span>Giao dịch chuyển tiền nội bộ SpendWise được xử lý tức thì 24/7 và miễn phí hoàn toàn.</span>
      </div>
    </div>
  );
};
