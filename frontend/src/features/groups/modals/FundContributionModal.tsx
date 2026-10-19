import React, { useState, useEffect, useMemo } from 'react';
import {
  X,
  Wallet,
  CheckCircle2,
  AlertCircle,
  Sparkles,
  ArrowRight,
  ShieldCheck,
} from 'lucide-react';
import { useI18n } from '../../../i18n/I18nContext';
import { useFinancial } from '../../../providers/FinancialProvider';
import { useGroup } from '../../../providers/GroupProvider';
import { useAuth } from '../../../providers/AuthProvider';
import { Group, FundContributionRequest } from '../../../types/groups';
import { parseSafePositiveInteger, sanitizeDigits, getLocalDateString } from '../../../utils/inputValidation';
import { cn } from '../../../utils/cn';
import { getLocalizedErrorMessage } from '../../../utils/localizationHelpers';
import { useSecurityPinChallenge } from '../../../providers/SecurityPinChallengeProvider';

export interface FundContributionModalProps {
  isOpen: boolean;
  onClose: () => void;
  group: Group;
  fundRequest?: FundContributionRequest | null;
  onOpenSettings?: () => void;
}

export const FundContributionModal: React.FC<FundContributionModalProps> = ({
  isOpen,
  onClose,
  group,
  fundRequest,
}) => {
  const { formatCurrency, locale, t } = useI18n();
  const { currentUser } = useAuth();
  const { wallets } = useFinancial();
  const { challengePinWithValue } = useSecurityPinChallenge();
  const { contributeToFund, payFundRequest } = useGroup();

  const isPayingRequest = Boolean(fundRequest);
  const [step, setStep] = useState<'input' | 'success'>('input');
  const [amountStr, setAmountStr] = useState(
    fundRequest ? fundRequest.amountPerMember.toString() : '500000'
  );
  const [note, setNote] = useState(
    fundRequest
      ? locale === 'vi-VN'
        ? `Đóng theo yêu cầu: ${fundRequest.purpose}`
        : `Contribute per request: ${fundRequest.purpose}`
      : locale === 'vi-VN'
      ? 'Đóng quỹ sinh hoạt nhóm'
      : 'Group fund contribution'
  );

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
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [isProcessing, setIsProcessing] = useState(false);
  const [contributedAmount, setContributedAmount] = useState<number>(0);

  // Reset state on open or change
  useEffect(() => {
    if (isOpen) {
      setStep('input');
      setIsProcessing(false);
      setContributedAmount(0);
      setSelectedWalletId(defaultWallet?.id || '');
      setAmountStr(fundRequest ? fundRequest.amountPerMember.toString() : '500000');
      setNote(
        fundRequest
          ? locale === 'vi-VN'
            ? `Đóng theo yêu cầu: ${fundRequest.purpose}`
            : `Contribute per request: ${fundRequest.purpose}`
          : locale === 'vi-VN'
          ? 'Đóng quỹ sinh hoạt nhóm'
          : 'Group fund contribution'
      );

      if (fundRequest) {
        const rec = currentUser?.id
          ? fundRequest.recipients.find((r) => r.userId === currentUser.id)
          : null;
        const isPastDue = Boolean(
          fundRequest.dueDate && fundRequest.dueDate < getLocalDateString()
        );
        if (!rec) {
          setErrorMessage(
            locale === 'vi-VN'
              ? 'Bạn không nằm trong danh sách người nhận của yêu cầu đóng quỹ này.'
              : 'You are not in the recipient list for this fund request.'
          );
        } else if (rec.status === 'Paid') {
          setErrorMessage(
            locale === 'vi-VN'
              ? 'Bạn đã hoàn tất yêu cầu đóng quỹ này.'
              : 'You have already completed this fund request.'
          );
        } else if (rec.status === 'Overdue' || (rec.status === 'Pending' && isPastDue)) {
          setErrorMessage(
            locale === 'vi-VN'
              ? 'Yêu cầu đóng quỹ này đã quá hạn.'
              : 'This fund request is overdue.'
          );
        } else if (rec.status === 'Cancelled') {
          setErrorMessage(
            locale === 'vi-VN'
              ? 'Yêu cầu đóng quỹ này đã bị hủy bỏ.'
              : 'This fund request has been cancelled.'
          );
        } else {
          setErrorMessage(null);
        }
      } else {
        setErrorMessage(null);
      }
    }
  }, [isOpen, fundRequest, defaultWallet, currentUser?.id, locale]);

  if (!isOpen) return null;

  const selectedWallet = activeWallets.find((w) => w.id === selectedWalletId) || defaultWallet;
  const amountValidation = parseSafePositiveInteger(amountStr);
  const enteredAmount = amountValidation.valid ? amountValidation.value : 0;
  const isBalanceSufficient = selectedWallet ? selectedWallet.balance >= enteredAmount : false;

  const handleAmountChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    setAmountStr(sanitizeDigits(e.target.value));
    setErrorMessage(null);
  };

  const handleQuickPreset = (preset: number) => {
    if (isPayingRequest) return;
    setAmountStr(preset.toString());
    setErrorMessage(null);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMessage(null);

    if (!selectedWallet) {
      setErrorMessage(
        locale === 'vi-VN'
          ? 'Vui lòng chọn ví thanh toán.'
          : 'Please select a payment wallet.'
      );
      return;
    }

    if (!amountValidation.valid || enteredAmount <= 0) {
      setErrorMessage(
        locale === 'vi-VN'
          ? 'Vui lòng nhập số tiền đóng quỹ hợp lệ.'
          : 'Please enter a valid contribution amount.'
      );
      return;
    }

    if (selectedWallet.balance < enteredAmount) {
      setErrorMessage(
        locale === 'vi-VN'
          ? `Số dư ví không đủ. Ví hiện có ${formatCurrency(selectedWallet.balance)}.`
          : `Insufficient wallet balance. Wallet has ${formatCurrency(selectedWallet.balance)}.`
      );
      return;
    }

    const pin = await challengePinWithValue({
      actionName: isPayingRequest
        ? locale === 'vi-VN'
          ? 'Xác nhận thanh toán yêu cầu đóng quỹ'
          : 'Confirm Fund Request Payment'
        : locale === 'vi-VN'
        ? 'Xác nhận đóng góp quỹ nhóm'
        : 'Confirm Group Fund Contribution',
      amount: enteredAmount,
    });

    if (pin === null) return; // User cancelled PIN challenge

    setIsProcessing(true);

    try {
      if (isPayingRequest && fundRequest) {
        const res = await payFundRequest({
          groupId: group.id,
          requestId: fundRequest.id,
          walletId: selectedWallet.id,
          securityPin: pin,
        });
        if (!res.success) {
          setErrorMessage(getLocalizedErrorMessage(res.error, locale, t));
          setIsProcessing(false);
          return;
        }
      } else {
        const res = await contributeToFund({
          groupId: group.id,
          amount: enteredAmount,
          purpose: note.trim() || undefined,
          walletId: selectedWallet.id,
          securityPin: pin,
        });
        if (!res.success) {
          setErrorMessage(getLocalizedErrorMessage(res.error, locale, t));
          setIsProcessing(false);
          return;
        }
      }

      setContributedAmount(enteredAmount);
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
            <div className="p-2 rounded-xl bg-emerald-50 dark:bg-emerald-950/80 text-emerald-600 dark:text-emerald-400">
              <Wallet className="h-5 w-5" />
            </div>
            <div>
              <h2 className="text-base font-bold text-slate-900 dark:text-slate-100">
                {isPayingRequest
                  ? locale === 'vi-VN'
                    ? 'Thanh toán yêu cầu đóng quỹ'
                    : 'Pay Fund Request'
                  : locale === 'vi-VN'
                  ? 'Đóng góp Quỹ nhóm'
                  : 'Group Fund Contribution'}
              </h2>
              <p className="text-xs text-slate-500 dark:text-slate-400">
                {group.name} • {locale === 'vi-VN' ? 'Quỹ SpendWise' : 'SpendWise Shared Fund'}
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
              {/* Group 1: Source & Destination */}
              <div className="p-3.5 sm:p-4 rounded-2xl bg-slate-50 dark:bg-slate-800/50 border border-slate-200/70 dark:border-slate-700/60 space-y-3">
                <span className="text-[11px] font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400 block">
                  {locale === 'vi-VN' ? '1. Nguồn trích tiền & Đích đến' : '1. Fund Source & Destination'}
                </span>

                {/* Wallet Selection */}
                <div>
                  <label className="block text-xs font-semibold text-slate-700 dark:text-slate-300 mb-1.5">
                    {locale === 'vi-VN' ? 'Từ Ví cá nhân SpendWise' : 'From Personal SpendWise Wallet'}
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
                              'w-full text-left p-3 rounded-2xl border transition-all flex items-center justify-between cursor-pointer',
                              isSelected
                                ? 'border-emerald-500 bg-emerald-50/60 dark:bg-emerald-950/40 ring-1 ring-emerald-500'
                                : 'border-slate-200 dark:border-slate-800 hover:border-slate-300 dark:hover:border-slate-700 bg-white dark:bg-slate-900'
                            )}
                          >
                            <div className="flex items-center gap-3">
                              <div
                                className={cn(
                                  'p-2 rounded-xl',
                                  isSelected
                                    ? 'bg-emerald-500 text-white'
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
                                  {locale === 'vi-VN' ? 'Số dư khả dụng:' : 'Available balance:'}{' '}
                                  <span className="font-semibold text-slate-700 dark:text-slate-300 font-mono">
                                    {formatCurrency(w.balance)}
                                  </span>
                                </p>
                              </div>
                            </div>
                            {isSelected && (
                              <CheckCircle2 className="h-4 w-4 text-emerald-600 dark:text-emerald-400" />
                            )}
                          </button>
                        );
                      })}
                    </div>
                  )}
                </div>

                {/* Destination Preview Card */}
                <div className="p-3 rounded-xl bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-700 flex items-center justify-between text-xs">
                  <div>
                    <span className="text-[10px] font-bold uppercase tracking-wider text-slate-400 block">
                      {locale === 'vi-VN' ? 'Vào Quỹ nhóm' : 'Into Group Fund'}
                    </span>
                    <p className="font-bold text-slate-900 dark:text-slate-100">{group.name}</p>
                  </div>
                  <div className="text-right">
                    <span className="text-[10px] text-slate-400 block">
                      {locale === 'vi-VN' ? 'Số dư hiện tại:' : 'Current balance:'}
                    </span>
                    <span className="font-bold font-mono text-slate-700 dark:text-slate-300">
                      {formatCurrency(group.sharedFund.balance)}
                    </span>
                  </div>
                </div>

                <p className="text-[11px] text-slate-500 dark:text-slate-400 flex items-center gap-1.5 pt-0.5">
                  <ShieldCheck className="h-3.5 w-3.5 text-slate-400 shrink-0" />
                  <span>
                    {locale === 'vi-VN'
                      ? 'Trích tiền nội bộ từ Ví cá nhân SpendWise vào Quỹ nhóm.'
                      : 'Internal transfer from personal SpendWise wallet to Group Fund.'}
                  </span>
                </p>
              </div>

              {/* Group 2: Amount & Purpose */}
              <div className="p-3.5 sm:p-4 rounded-2xl bg-slate-50 dark:bg-slate-800/50 border border-slate-200/70 dark:border-slate-700/60 space-y-3.5">
                <span className="text-[11px] font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400 block">
                  {locale === 'vi-VN' ? '2. Số tiền & Nội dung đóng góp' : '2. Amount & Purpose'}
                </span>

                {/* Amount Input */}
                <div>
                  <div className="flex items-center justify-between mb-1.5">
                    <label className="text-xs font-semibold text-slate-700 dark:text-slate-300">
                      {locale === 'vi-VN' ? 'Số tiền đóng quỹ (VND) *' : 'Contribution Amount (VND) *'}
                    </label>
                    {isPayingRequest ? (
                      <span className="text-[11px] text-emerald-600 dark:text-emerald-400 font-medium">
                        {locale === 'vi-VN' ? 'Cố định theo yêu cầu' : 'Fixed per request'}
                      </span>
                    ) : (
                      amountValidation.valid && enteredAmount > 0 && (
                        <span className="text-xs font-bold text-emerald-600 dark:text-emerald-400 tabular-nums font-mono">
                          ≈ {formatCurrency(enteredAmount)}
                        </span>
                      )
                    )}
                  </div>
                  <div className="relative">
                    <input
                      type="text"
                      inputMode="numeric"
                      readOnly={isPayingRequest}
                      value={amountStr}
                      onChange={handleAmountChange}
                      className={cn(
                        'w-full px-4 py-3 rounded-2xl border text-lg font-bold tracking-tight text-slate-900 dark:text-slate-100 bg-white dark:bg-slate-900 focus:outline-none focus:ring-2 focus:ring-emerald-500 transition-all font-mono',
                        isPayingRequest
                          ? 'bg-slate-100 dark:bg-slate-800/60 cursor-not-allowed border-slate-200 dark:border-slate-800'
                          : 'border-slate-200 dark:border-slate-800'
                      )}
                      placeholder="0"
                    />
                    <span className="absolute right-4 top-1/2 -translate-y-1/2 text-xs font-semibold text-slate-400">
                      VND
                    </span>
                  </div>

                  {!isPayingRequest && (
                    <div className="grid grid-cols-4 gap-2 mt-2">
                      {[200000, 500000, 1000000, 2000000].map((preset) => (
                        <button
                          key={preset}
                          type="button"
                          onClick={() => handleQuickPreset(preset)}
                          className="py-1.5 px-2 rounded-xl text-[11px] font-semibold bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-700 hover:bg-slate-100 dark:hover:bg-slate-800 text-slate-700 dark:text-slate-300 transition-colors text-center cursor-pointer shadow-2xs"
                        >
                          +{preset >= 1000000 ? `${preset / 1000000}tr` : `${preset / 1000}k`}
                        </button>
                      ))}
                    </div>
                  )}

                  {selectedWallet && enteredAmount > 0 && !isBalanceSufficient && (
                    <p className="mt-1.5 text-xs text-rose-600 dark:text-rose-400 flex items-center gap-1 font-medium">
                      <AlertCircle className="h-3.5 w-3.5 shrink-0" />
                      <span>
                        {locale === 'vi-VN'
                          ? 'Số dư ví không đủ để thanh toán số tiền này.'
                          : 'Wallet balance is insufficient for this amount.'}
                      </span>
                    </p>
                  )}
                </div>

                {/* Note / Purpose Input */}
                <div>
                  <label className="block text-xs font-semibold text-slate-700 dark:text-slate-300 mb-1.5">
                    {locale === 'vi-VN' ? 'Nội dung đóng quỹ' : 'Contribution Note'}
                  </label>
                  <input
                    type="text"
                    value={note}
                    onChange={(e) => setNote(e.target.value)}
                    className="w-full px-4 py-2.5 rounded-xl border border-slate-200 dark:border-slate-700 text-xs font-medium text-slate-900 dark:text-slate-100 bg-white dark:bg-slate-900 focus:outline-none focus:ring-2 focus:ring-emerald-500"
                    placeholder={
                      locale === 'vi-VN' ? 'Nhập ghi chú...' : 'Enter note...'
                    }
                  />
                </div>

                {/* Projected Group Balance (Presentation-Only Derived Value) */}
                <div className="p-3 rounded-xl bg-emerald-50/70 dark:bg-emerald-950/40 border border-emerald-500/20 flex items-center justify-between text-xs">
                  <span className="text-emerald-900 dark:text-emerald-200 font-medium">
                    {locale === 'vi-VN' ? 'Dự kiến số dư Quỹ nhóm sau khi góp:' : 'Projected Group Fund Balance:'}
                  </span>
                  <span className="font-black text-emerald-700 dark:text-emerald-300 font-mono text-sm tabular-nums">
                    {formatCurrency(group.sharedFund.balance + (amountValidation.valid && enteredAmount > 0 ? enteredAmount : 0))}
                  </span>
                </div>
              </div>

              {/* Action buttons */}
              <div className="pt-2 flex items-center justify-end gap-3">
                <button
                  type="button"
                  onClick={onClose}
                  className="px-4 py-2.5 rounded-xl text-xs font-semibold text-slate-600 dark:text-slate-400 hover:bg-slate-100 dark:hover:bg-slate-800 transition-colors cursor-pointer"
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
                    !isBalanceSufficient
                  }
                  className="px-5 py-2.5 rounded-xl text-xs font-bold text-white bg-emerald-600 hover:bg-emerald-700 disabled:opacity-50 disabled:cursor-not-allowed shadow-md shadow-emerald-600/20 transition-all flex items-center gap-2 cursor-pointer"
                >
                  <span>
                    {isProcessing
                      ? locale === 'vi-VN'
                        ? 'Đang xử lý...'
                        : 'Processing...'
                      : locale === 'vi-VN'
                      ? 'Xác nhận đóng quỹ'
                      : 'Confirm Contribution'}
                  </span>
                </button>
              </div>
            </form>
          )}

          {step === 'success' && (
            <div className="py-6 text-center space-y-4">
              <div className="w-16 h-16 rounded-full bg-emerald-100 dark:bg-emerald-950/80 text-emerald-600 dark:text-emerald-400 flex items-center justify-center mx-auto shadow-inner">
                <CheckCircle2 className="h-8 w-8" />
              </div>
              <div>
                <h3 className="text-base font-bold text-slate-900 dark:text-slate-100">
                  {locale === 'vi-VN' ? 'Đóng quỹ thành công!' : 'Contribution Successful!'}
                </h3>
                <p className="text-xs text-slate-500 dark:text-slate-400 mt-1">
                  {locale === 'vi-VN'
                    ? `Đã chuyển ${formatCurrency(contributedAmount)} từ ví vào quỹ nhóm ${group.name}.`
                    : `Transferred ${formatCurrency(contributedAmount)} to ${group.name} group fund.`}
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
                  <span className="font-bold text-emerald-600 dark:text-emerald-400">
                    +{formatCurrency(contributedAmount)}
                  </span>
                </div>
                <div className="flex justify-between">
                  <span className="text-slate-500 dark:text-slate-400">
                    {locale === 'vi-VN' ? 'Ghi chú:' : 'Note:'}
                  </span>
                  <span className="font-medium text-slate-700 dark:text-slate-300">
                    {note}
                  </span>
                </div>
              </div>

              <button
                type="button"
                onClick={onClose}
                className="w-full py-2.5 rounded-xl text-xs font-bold text-white bg-emerald-600 hover:bg-emerald-700 shadow-md shadow-emerald-600/20 transition-all"
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
