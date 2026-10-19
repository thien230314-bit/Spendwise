import React, { useState, useMemo, useEffect } from 'react';
import { useI18n } from '../../../i18n/I18nContext';
import { useFinancial } from '../../../providers/FinancialProvider';
import { useAuth } from '../../../providers/AuthProvider';
import { useGroup } from '../../../providers/GroupProvider';
import { Group, GroupFundRound } from '../../../types/groups';
import {
  X,
  Wallet,
  CheckCircle2,
  AlertCircle,
  ArrowRight,
  ShieldCheck,
} from 'lucide-react';
import { parseSafePositiveInteger, sanitizeDigits } from '../../../utils/inputValidation';
import { cn } from '../../../utils/cn';
import { getLocalizedErrorMessage } from '../../../utils/localizationHelpers';
import { useSecurityPinChallenge } from '../../../providers/SecurityPinChallengeProvider';

interface PayFundRoundModalProps {
  isOpen: boolean;
  onClose: () => void;
  group: Group;
  round: GroupFundRound;
}

export const PayFundRoundModal: React.FC<PayFundRoundModalProps> = ({
  isOpen,
  onClose,
  group,
  round,
}) => {
  const { formatCurrency, locale, t } = useI18n();
  const { currentUser } = useAuth();
  const { wallets } = useFinancial();
  const { challengePinWithValue } = useSecurityPinChallenge();
  const { payFundRound } = useGroup();

  const currentMember = useMemo(() => {
    return round.members.find((m) => m.userId === currentUser?.id);
  }, [round.members, currentUser?.id]);

  const remaining = useMemo(() => {
    if (!currentMember) return 0;
    return Math.max(0, currentMember.requiredAmount - currentMember.paidAmount);
  }, [currentMember]);

  const [step, setStep] = useState<'input' | 'success'>('input');
  const [amountStr, setAmountStr] = useState(remaining > 0 ? remaining.toString() : '0');
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [isProcessing, setIsProcessing] = useState(false);
  const [paidAmount, setPaidAmount] = useState<number>(0);

  // Eligible personal wallets
  const activeWallets = useMemo(() => {
    return wallets || [];
  }, [wallets]);

  const defaultWallet = useMemo(() => {
    return (
      activeWallets.find((w) => w.type === 'spending' || (w as any).type === 'main') ||
      activeWallets[0]
    );
  }, [activeWallets]);

  const [selectedWalletId, setSelectedWalletId] = useState<string>('');

  // Sync state on open
  useEffect(() => {
    if (isOpen) {
      setStep('input');
      setIsProcessing(false);
      setPaidAmount(0);
      setSelectedWalletId(defaultWallet?.id || '');
      setAmountStr(remaining > 0 ? remaining.toString() : '0');

      if (currentMember && currentMember.status === 'Paid') {
        setErrorMessage(
          locale === 'vi-VN'
            ? 'Bạn đã hoàn tất phần đóng quỹ của mình trong đợt này.'
            : 'You have completed your fund contribution for this round.'
        );
      } else {
        setErrorMessage(null);
      }
    }
  }, [isOpen, remaining, currentMember, defaultWallet, locale]);

  if (!isOpen) return null;

  const selectedWallet = activeWallets.find((w) => w.id === selectedWalletId) || defaultWallet;
  const amountValidation = parseSafePositiveInteger(amountStr);
  const enteredAmount = amountValidation.valid ? amountValidation.value : 0;
  const isBalanceSufficient = selectedWallet ? selectedWallet.balance >= enteredAmount : false;
  const isExceedingRemaining = enteredAmount > remaining;

  const handleAmountChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    setAmountStr(sanitizeDigits(e.target.value));
    setErrorMessage(null);
  };

  const handleFillRemaining = () => {
    setAmountStr(remaining.toString());
    setErrorMessage(null);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMessage(null);

    if (!selectedWallet) {
      setErrorMessage(
        locale === 'vi-VN' ? 'Vui lòng chọn ví thanh toán.' : 'Please select a payment wallet.'
      );
      return;
    }

    if (!amountValidation.valid || enteredAmount <= 0) {
      setErrorMessage(
        locale === 'vi-VN'
          ? 'Vui lòng nhập số tiền hợp lệ.'
          : 'Please enter a valid contribution amount.'
      );
      return;
    }

    if (isExceedingRemaining) {
      setErrorMessage(
        locale === 'vi-VN'
          ? `Số tiền đóng không được vượt quá số tiền còn thiếu (${formatCurrency(remaining)}).`
          : `Amount cannot exceed remaining balance (${formatCurrency(remaining)}).`
      );
      return;
    }

    if (!isBalanceSufficient) {
      setErrorMessage(
        locale === 'vi-VN'
          ? `Số dư ví không đủ. Ví hiện có ${formatCurrency(selectedWallet.balance)}.`
          : `Insufficient wallet balance. Wallet has ${formatCurrency(selectedWallet.balance)}.`
      );
      return;
    }

    const pin = await challengePinWithValue({
      actionName:
        locale === 'vi-VN'
          ? `Đóng quỹ theo đợt — ${round.title}`
          : `Pay Fund Round — ${round.title}`,
      amount: enteredAmount,
    });

    if (pin === null) return;

    setIsProcessing(true);

    try {
      const res = await payFundRound({
        groupId: group.id,
        roundId: round.id,
        amount: enteredAmount,
        walletId: selectedWallet.id,
        securityPin: pin,
      });

      if (!res.success) {
        setErrorMessage(getLocalizedErrorMessage(res.error, locale, t));
        setIsProcessing(false);
        return;
      }

      setPaidAmount(enteredAmount);
      setStep('success');
    } catch (err: any) {
      setErrorMessage(
        getLocalizedErrorMessage(err?.message, locale, t, 'common.errorOccurred')
      );
    } finally {
      setIsProcessing(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-sm animate-fadeIn">
      <div className="bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-3xl w-full max-w-lg shadow-2xl overflow-hidden flex flex-col max-h-[90vh] animate-scaleUp">
        {/* Header */}
        <div className="flex items-center justify-between px-6 py-4 border-b border-slate-100 dark:border-slate-800 bg-slate-50/50 dark:bg-slate-950/40 shrink-0">
          <div className="flex items-center gap-2.5">
            <div className="p-2 rounded-xl bg-purple-50 dark:bg-purple-950/80 text-purple-600 dark:text-purple-400">
              <Wallet className="h-5 w-5" />
            </div>
            <div>
              <h2 className="text-base font-bold text-slate-900 dark:text-slate-100">
                {locale === 'vi-VN' ? 'Đóng quỹ theo đợt' : 'Pay Fund Round'}
              </h2>
              <p className="text-xs text-slate-500 dark:text-slate-400">
                {round.title} • {group.name}
              </p>
            </div>
          </div>
          <button
            onClick={onClose}
            className="p-1.5 rounded-lg text-slate-400 hover:text-slate-600 dark:hover:text-slate-200 hover:bg-slate-100 dark:hover:bg-slate-800 transition-colors"
          >
            <X className="h-5 w-5" />
          </button>
        </div>

        {/* Content */}
        <div className="p-6 overflow-y-auto space-y-5">
          {errorMessage && (
            <div className="flex items-center gap-2.5 p-3.5 text-xs text-rose-700 dark:text-rose-400 bg-rose-50 dark:bg-rose-950/50 border border-rose-200 dark:border-rose-800 rounded-2xl">
              <AlertCircle className="h-4 w-4 shrink-0" />
              <span>{errorMessage}</span>
            </div>
          )}

          {step === 'input' && (
            <form onSubmit={handleSubmit} className="space-y-4">
              {/* Obligation summary */}
              <div className="p-4 rounded-2xl bg-slate-50 dark:bg-slate-950/60 border border-slate-200 dark:border-slate-800 grid grid-cols-2 gap-3 text-xs">
                <div>
                  <span className="text-slate-500 dark:text-slate-400">
                    {locale === 'vi-VN' ? 'Phần cần đóng:' : 'Required Amount:'}
                  </span>
                  <p className="font-semibold text-slate-800 dark:text-slate-200 mt-0.5">
                    {formatCurrency(currentMember?.requiredAmount || 0)}
                  </p>
                </div>
                <div>
                  <span className="text-slate-500 dark:text-slate-400">
                    {locale === 'vi-VN' ? 'Còn thiếu:' : 'Remaining:'}
                  </span>
                  <p className="font-bold text-purple-600 dark:text-purple-400 mt-0.5">
                    {formatCurrency(remaining)}
                  </p>
                </div>
              </div>

              {/* Wallet Selection */}
              <div>
                <label className="block text-xs font-semibold text-slate-700 dark:text-slate-300 mb-1.5">
                  {locale === 'vi-VN' ? 'Trừ tiền từ Ví cá nhân' : 'Pay from Personal Wallet'}
                </label>
                {activeWallets.length === 0 ? (
                  <div className="p-3 bg-amber-50 dark:bg-amber-950/40 border border-amber-200 dark:border-amber-800 rounded-xl text-xs text-amber-800 dark:text-amber-200">
                    {locale === 'vi-VN'
                      ? 'Bạn chưa có ví cá nhân khả dụng.'
                      : 'No available personal wallet found.'}
                  </div>
                ) : (
                  <div className="space-y-2">
                    {activeWallets.map((w) => {
                      const isSelected = (selectedWallet?.id || selectedWalletId) === w.id;
                      return (
                        <button
                          key={w.id}
                          type="button"
                          onClick={() => setSelectedWalletId(w.id)}
                          className={cn(
                            'w-full text-left p-3 rounded-2xl border transition-all flex items-center justify-between',
                            isSelected
                              ? 'border-purple-500 bg-purple-50/60 dark:bg-purple-950/40 ring-1 ring-purple-500'
                              : 'border-slate-200 dark:border-slate-800 hover:border-slate-300 dark:hover:border-slate-700 bg-slate-50/50 dark:bg-slate-900/50'
                          )}
                        >
                          <div className="flex items-center gap-3">
                            <div
                              className={cn(
                                'p-2 rounded-xl',
                                isSelected
                                  ? 'bg-purple-500 text-white'
                                  : 'bg-slate-200 dark:bg-slate-800 text-slate-600 dark:text-slate-400'
                              )}
                            >
                              <Wallet className="h-4 w-4" />
                            </div>
                            <div>
                              <p className="text-xs font-bold text-slate-900 dark:text-slate-100">
                                {w.name}
                              </p>
                              <p className="text-[11px] text-slate-500 dark:text-slate-400">
                                {locale === 'vi-VN' ? 'Số dư:' : 'Balance:'}{' '}
                                <span className="font-semibold text-slate-700 dark:text-slate-300">
                                  {formatCurrency(w.balance)}
                                </span>
                              </p>
                            </div>
                          </div>
                          {isSelected && (
                            <CheckCircle2 className="h-4 w-4 text-purple-600 dark:text-purple-400" />
                          )}
                        </button>
                      );
                    })}
                  </div>
                )}
              </div>

              {/* Amount Input */}
              <div>
                <div className="flex items-center justify-between mb-1.5">
                  <label className="text-xs font-semibold text-slate-700 dark:text-slate-300">
                    {locale === 'vi-VN' ? 'Số tiền đóng (VND)' : 'Payment Amount (VND)'}
                  </label>
                  <button
                    type="button"
                    onClick={handleFillRemaining}
                    className="text-[11px] text-purple-600 dark:text-purple-400 hover:underline font-semibold"
                  >
                    {locale === 'vi-VN' ? 'Đóng toàn bộ còn thiếu' : 'Pay Full Remaining'}
                  </button>
                </div>
                <div className="relative">
                  <input
                    type="text"
                    inputMode="numeric"
                    value={amountStr}
                    onChange={handleAmountChange}
                    className="w-full px-4 py-3 rounded-2xl border border-slate-200 dark:border-slate-800 text-lg font-bold tracking-tight text-slate-900 dark:text-slate-100 bg-white dark:bg-slate-950 focus:outline-none focus:ring-2 focus:ring-purple-500 transition-all"
                    placeholder="0"
                  />
                  <span className="absolute right-4 top-1/2 -translate-y-1/2 text-xs font-semibold text-slate-400">
                    VND
                  </span>
                </div>

                {isExceedingRemaining && (
                  <p className="mt-1.5 text-xs text-rose-600 dark:text-rose-400 flex items-center gap-1 font-medium">
                    <AlertCircle className="h-3.5 w-3.5 shrink-0" />
                    <span>
                      {locale === 'vi-VN'
                        ? 'Số tiền vượt quá phần còn thiếu.'
                        : 'Amount exceeds remaining balance.'}
                    </span>
                  </p>
                )}

                {selectedWallet && enteredAmount > 0 && !isBalanceSufficient && !isExceedingRemaining && (
                  <p className="mt-1.5 text-xs text-rose-600 dark:text-rose-400 flex items-center gap-1 font-medium">
                    <AlertCircle className="h-3.5 w-3.5 shrink-0" />
                    <span>
                      {locale === 'vi-VN'
                        ? 'Số dư ví không đủ để thanh toán.'
                        : 'Insufficient wallet balance.'}
                    </span>
                  </p>
                )}
              </div>

              {/* Security card */}
              <div className="p-3.5 rounded-2xl bg-slate-50 dark:bg-slate-950/60 border border-slate-200 dark:border-slate-800 text-[11px] text-slate-600 dark:text-slate-400 space-y-1">
                <div className="flex items-center gap-1.5 font-semibold text-slate-700 dark:text-slate-300">
                  <ShieldCheck className="h-4 w-4 text-purple-600 dark:text-purple-400" />
                  <span>{locale === 'vi-VN' ? 'Xác thực PIN' : 'PIN Verification'}</span>
                </div>
                <p>
                  {locale === 'vi-VN'
                    ? 'Giao dịch trích từ ví SpendWise vào quỹ chung nhóm theo đợt. Yêu cầu nhập mã PIN bảo mật.'
                    : 'Transaction is debited from your SpendWise wallet into the fund round. Requires Security PIN.'}
                </p>
              </div>

              {/* Actions */}
              <div className="pt-2 flex items-center justify-end gap-3">
                <button
                  type="button"
                  onClick={onClose}
                  className="px-4 py-2.5 rounded-xl text-xs font-semibold text-slate-600 dark:text-slate-400 hover:bg-slate-100 dark:hover:bg-slate-800 transition-colors"
                >
                  {locale === 'vi-VN' ? 'Hủy' : 'Cancel'}
                </button>
                <button
                  type="submit"
                  disabled={
                    isProcessing ||
                    !selectedWallet ||
                    !amountValidation.valid ||
                    enteredAmount <= 0 ||
                    !isBalanceSufficient ||
                    isExceedingRemaining
                  }
                  className="px-5 py-2.5 rounded-xl text-xs font-bold text-white bg-purple-600 hover:bg-purple-700 disabled:opacity-50 disabled:cursor-not-allowed shadow-md shadow-purple-600/20 transition-all flex items-center gap-2"
                >
                  <span>
                    {isProcessing
                      ? locale === 'vi-VN'
                        ? 'Đang xử lý...'
                        : 'Processing...'
                      : locale === 'vi-VN'
                      ? 'Xác nhận đóng quỹ'
                      : 'Confirm Payment'}
                  </span>
                  <ArrowRight className="h-4 w-4" />
                </button>
              </div>
            </form>
          )}

          {step === 'success' && (
            <div className="py-6 text-center space-y-4">
              <div className="w-16 h-16 rounded-full bg-purple-100 dark:bg-purple-950/80 text-purple-600 dark:text-purple-400 flex items-center justify-center mx-auto shadow-inner">
                <CheckCircle2 className="h-8 w-8" />
              </div>
              <div>
                <h3 className="text-base font-bold text-slate-900 dark:text-slate-100">
                  {locale === 'vi-VN' ? 'Đóng quỹ đợt thành công!' : 'Round Payment Successful!'}
                </h3>
                <p className="text-xs text-slate-500 dark:text-slate-400 mt-1">
                  {locale === 'vi-VN'
                    ? `Đã thanh toán ${formatCurrency(paidAmount)} cho đợt "${round.title}".`
                    : `Paid ${formatCurrency(paidAmount)} for round "${round.title}".`}
                </p>
              </div>

              <div className="p-4 bg-slate-50 dark:bg-slate-950/50 border border-slate-200 dark:border-slate-800 rounded-2xl text-left space-y-2 text-xs">
                <div className="flex justify-between">
                  <span className="text-slate-500 dark:text-slate-400">
                    {locale === 'vi-VN' ? 'Ví nguồn:' : 'Source Wallet:'}
                  </span>
                  <span className="font-semibold text-slate-800 dark:text-slate-200">
                    {selectedWallet?.name}
                  </span>
                </div>
                <div className="flex justify-between">
                  <span className="text-slate-500 dark:text-slate-400">
                    {locale === 'vi-VN' ? 'Số tiền:' : 'Amount:'}
                  </span>
                  <span className="font-bold text-purple-600 dark:text-purple-400">
                    +{formatCurrency(paidAmount)}
                  </span>
                </div>
              </div>

              <button
                type="button"
                onClick={onClose}
                className="w-full py-2.5 rounded-xl text-xs font-bold text-white bg-purple-600 hover:bg-purple-700 shadow-md shadow-purple-600/20 transition-all"
              >
                {locale === 'vi-VN' ? 'Hoàn tất' : 'Done'}
              </button>
            </div>
          )}
        </div>
      </div>
    </div>
  );
};
