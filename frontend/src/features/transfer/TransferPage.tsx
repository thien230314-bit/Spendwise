import React, { useState, useEffect, useCallback } from 'react';
import { useSearchParams, useLocation } from 'react-router-dom';
import {
  Send,
  ShieldCheck,
  CheckCircle2,
  Clock,
  Check,
  AlertCircle,
  Smartphone,
  Info,
  Landmark,
  Copy,
  X,
  Building2,
  ExternalLink,
  RefreshCw,
  Loader2,
} from 'lucide-react';
import { useI18n } from '../../i18n/I18nContext';
import { useFinancial } from '../../providers/FinancialProvider';
import { useAuth } from '../../providers/AuthProvider';
import {
  transfersApiService,
  TransferRecipientDto,
  BankDirectPaymentDto,
} from '../../services/transfersApiService';
import { formatMoney } from '../../utils/money';
import { formatDate } from '../../utils/date';
import {
  sanitizeIntegerInput,
  parseSafePositiveInteger,
  isValidVietnamPhone,
  sanitizeVietnamPhoneInput,
} from '../../utils/inputValidation';
import { Button } from '../../components/ui/Button';
import { cn } from '../../utils/cn';
import { useSecurityPinChallenge } from '../../providers/SecurityPinChallengeProvider';
import { isApiError } from '../../lib/apiError';
import { friendService } from '../../services/friendService';
import { FriendWithMeta } from '../../types/friends';
import { TransferSkeleton } from '../../components/skeletons/TransferSkeleton';

export const TransferPage: React.FC = () => {
  const { t, locale } = useI18n();
  const { currentUser } = useAuth();
  const { challengePinWithValue } = useSecurityPinChallenge();
  const { wallets, refreshFinancialCore, isLoading } = useFinancial();
  const [searchParams] = useSearchParams();
  const location = useLocation();

  const sourceWallet = wallets.find((w) => w.isDefault) || wallets[0];
  const sourceBalance = sourceWallet?.balance ?? 0;

  // Recipient phone lookup state
  const [recipientPhone, setRecipientPhone] = useState('');
  const [selectedRecipient, setSelectedRecipient] = useState<TransferRecipientDto | null>(null);
  const [lookupState, setLookupState] = useState<
    'idle' | 'searching' | 'found' | 'not_found' | 'self' | 'inactive' | 'invalid_format' | 'bank_required'
  >('idle');
  const [suggestedFriends, setSuggestedFriends] = useState<FriendWithMeta[]>([]);

  const [amount, setAmount] = useState('');
  const [note, setNote] = useState('');

  // Transfer State Machine: 'draft' -> 'review' -> 'completed'
  const [transferStatus, setTransferStatus] = useState<'draft' | 'review' | 'completed'>('draft');
  const [activePayment, setActivePayment] = useState<BankDirectPaymentDto | null>(null);

  // Transfers from backend
  const [allTransfers, setAllTransfers] = useState<BankDirectPaymentDto[]>([]);
  const [isLoadingTransfers, setIsLoadingTransfers] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [isCheckingStatus, setIsCheckingStatus] = useState(false);
  const [copiedField, setCopiedField] = useState<string | null>(null);

  // Load transfers list from backend
  const refreshTransfers = useCallback(async () => {
    if (!currentUser?.id) return;
    setIsLoadingTransfers(true);
    try {
      const res = await transfersApiService.getTransfers({ page: 1, pageSize: 50 });
      setAllTransfers(res?.items || []);
    } catch (err) {
      console.warn('Failed to load transfers from backend', err);
    } finally {
      setIsLoadingTransfers(false);
    }
  }, [currentUser?.id]);

  useEffect(() => {
    void refreshTransfers();
  }, [refreshTransfers]);

  // Load accepted friends for transfer recipient suggestions
  useEffect(() => {
    let isMounted = true;
    if (currentUser?.id) {
      friendService
        .refreshAll(currentUser.id)
        .then(() => {
          if (!isMounted) return;
          const friends = friendService.getFriends(currentUser.id);
          const sorted = [...friends].sort((a, b) => {
            if (a.isFavorite && !b.isFavorite) return -1;
            if (!a.isFavorite && b.isFavorite) return 1;
            return (a.nickname || a.name).localeCompare(b.nickname || b.name);
          });
          setSuggestedFriends(sorted);
        })
        .catch((err) => {
          console.warn('Failed to load friend suggestions:', err);
          if (isMounted) {
            const friends = friendService.getFriends(currentUser.id);
            setSuggestedFriends(friends);
          }
        });
    }
    return () => {
      isMounted = false;
    };
  }, [currentUser?.id]);

  // Lookup handler: exact 10-digit VN phone via backend
  const performPhoneLookup = async (rawPhone: string) => {
    const sanitized = sanitizeVietnamPhoneInput(rawPhone);
    setRecipientPhone(sanitized);

    if (!sanitized) {
      setSelectedRecipient(null);
      setLookupState('idle');
      return;
    }

    if (!sanitized.startsWith('0') || sanitized.length > 10) {
      setSelectedRecipient(null);
      setLookupState('invalid_format');
      return;
    }

    if (sanitized.length < 10) {
      setSelectedRecipient(null);
      setLookupState('idle');
      return;
    }

    if (!isValidVietnamPhone(sanitized)) {
      setSelectedRecipient(null);
      setLookupState('invalid_format');
      return;
    }

    // Call backend endpoint GET /api/transfers/recipients?phone={phone}
    setLookupState('searching');
    try {
      const recipient = await transfersApiService.getRecipientByPhone(sanitized);
      if (recipient.userId && recipient.userId === currentUser?.id) {
        setSelectedRecipient(null);
        setLookupState('self');
        return;
      }
      setSelectedRecipient(recipient);
      setLookupState('found');
    } catch (err) {
      setSelectedRecipient(null);
      if (isApiError(err) && err.code === 'active_bank_required') {
        setLookupState('bank_required');
      } else if (isApiError(err) && err.status === 404) {
        setLookupState('not_found');
      } else if (isApiError(err) && err.code === 'user_inactive') {
        setLookupState('inactive');
      } else {
        setLookupState('not_found');
      }
    }
  };

  // Handle URL search params for deep linking
  useEffect(() => {
    const state = location.state as { userId?: string; phone?: string; recipientPhone?: string } | undefined;
    const phoneParam =
      searchParams.get('recipientPhone') ||
      searchParams.get('phone') ||
      searchParams.get('to') ||
      searchParams.get('recipient') ||
      state?.recipientPhone ||
      state?.phone;

    if (phoneParam) {
      void performPhoneLookup(phoneParam);
    }

    const amt = searchParams.get('amount');
    if (amt) setAmount(amt);

    const n = searchParams.get('note');
    if (n) setNote(n);
  }, [searchParams, location.state]);

  // Escape listener for review modal
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape' && transferStatus === 'review') {
        setTransferStatus('draft');
      }
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [transferStatus]);

  const quickAmounts = [50000, 100000, 200000, 500000, 1000000, 2000000];
  const { valid: isAmountValid, value: draftTransferAmount } = parseSafePositiveInteger(amount);
  const isOverBalance = isAmountValid && draftTransferAmount > sourceBalance;
  const projectedBalance = Math.max(0, sourceBalance - draftTransferAmount);
  const displayAmount = amount ? new Intl.NumberFormat('vi-VN').format(Number(amount)) : '';

  const displayRecipient = selectedRecipient
    ? `${selectedRecipient.fullName} (${recipientPhone})`
    : locale === 'en-US'
    ? 'No recipient selected'
    : 'Chưa chọn người nhận';

  const handleCopyText = (text: string, fieldKey: string) => {
    navigator.clipboard.writeText(text);
    setCopiedField(fieldKey);
    setTimeout(() => setCopiedField(null), 2000);
  };

  // Open review confirmation step
  const handleOpenConfirm = (e: React.FormEvent) => {
    e.preventDefault();
    if (!currentUser?.id || !selectedRecipient || !isAmountValid || draftTransferAmount <= 0) {
      return;
    }
    setSubmitError(null);
    setTransferStatus('review');
  };

  // Execute transfer with security PIN to backend POST /transfers and POST /transfers/{id}/confirm
  const handleExecuteTransfer = async () => {
    if (!currentUser?.id || !selectedRecipient || !isAmountValid || draftTransferAmount <= 0) {
      return;
    }

    const pin = await challengePinWithValue({
      title: locale === 'vi-VN' ? 'Xác nhận chuyển tiền Bank-Direct' : 'Confirm Bank-Direct Transfer',
      amount: draftTransferAmount,
      description: `${selectedRecipient.fullName} • ${selectedRecipient.bankBrand || 'Bank'}`,
    });

    if (!pin) {
      return;
    }

    setIsSubmitting(true);
    setSubmitError(null);

    try {
      const idempotencyKey = typeof crypto !== 'undefined' && crypto.randomUUID ? crypto.randomUUID() : `${Date.now()}-${Math.random()}`;
      const defaultNote = locale === 'vi-VN' ? 'Chuyển tiền SpendWise' : 'SpendWise Transfer';

      if (!selectedRecipient.userId) {
        setSubmitError(locale === 'vi-VN' ? 'Không tìm thấy ID người nhận hợp lệ.' : 'Invalid recipient user ID.');
        return;
      }

      // 1. Create transfer instruction on backend
      const created = await transfersApiService.createTransfer({
        recipientUserId: selectedRecipient.userId,
        amount: draftTransferAmount,
        note: note.trim() || defaultNote,
        idempotencyKey,
      });

      // 2. Confirm transfer with entered Security PIN
      const confirmed = await transfersApiService.confirmTransfer(created.id, {
        securityPin: pin,
      });

      setActivePayment(confirmed);
      await refreshTransfers();

      if (confirmed.status.toLowerCase() === 'completed') {
        setTransferStatus('completed');
        await refreshFinancialCore();
      } else {
        // Stays in review/instruction mode showing the payment reference
        setTransferStatus('review');
      }
    } catch (err) {
      if (isApiError(err)) {
        setSubmitError(err.detail || err.title || 'Lỗi khi thực hiện chuyển tiền.');
      } else {
        setSubmitError(locale === 'vi-VN' ? 'Đã xảy ra lỗi không xác định.' : 'An unexpected error occurred.');
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  // Poll / Check backend payment status
  const handleCheckPaymentStatus = async () => {
    if (!activePayment?.id) return;
    setIsCheckingStatus(true);
    try {
      const updated = await transfersApiService.getTransferById(activePayment.id);
      setActivePayment(updated);
      await refreshTransfers();
      if (updated.status.toLowerCase() === 'completed') {
        setTransferStatus('completed');
        await refreshFinancialCore();
      }
    } catch (err) {
      console.warn('Failed to check transfer status', err);
    } finally {
      setIsCheckingStatus(false);
    }
  };

  const handleDone = () => {
    setTransferStatus('draft');
    setActivePayment(null);
    setAmount('');
    setNote('');
  };

  const pendingPayments = allTransfers.filter((t) => {
    const s = t.status?.toLowerCase();
    return s === 'pending' || s === 'awaitingreconciliation';
  });
  const completedTransfers = allTransfers.filter((t) => t.status?.toLowerCase() === 'completed');

  if (isLoading && wallets.length === 0) {
    return <TransferSkeleton />;
  }

  return (
    <div id="transfer-page" className="space-y-6">
      {/* Page Title & Subtitle */}
      <div>
        <h2 className="text-xl sm:text-2xl font-bold text-slate-900 dark:text-white tracking-tight">
          {t('transfer.title')}
        </h2>
        <p className="text-xs sm:text-sm text-slate-500 dark:text-slate-400 mt-1">
          {locale === 'vi-VN'
            ? 'Chuyển tiền từ ví SpendWise đến tài khoản ngân hàng đã liên kết của người nhận.'
            : 'Direct bank transfer to recipient verified bank account.'}
        </p>
      </div>

      {/* Review & Instruction Modal */}
      {transferStatus === 'review' && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-3 sm:p-4 bg-slate-950/70 backdrop-blur-xs animate-in fade-in duration-150 overflow-y-auto">
          <div className="w-full max-w-lg bg-white dark:bg-slate-900 rounded-3xl p-5 sm:p-7 border border-slate-100 dark:border-slate-800 shadow-2xl space-y-5 my-auto animate-in zoom-in-95 duration-150 max-h-[95vh] overflow-y-auto">
            {/* Modal Header */}
            <div className="flex items-center justify-between border-b border-slate-100 dark:border-slate-800 pb-3">
              <div className="flex items-center gap-2">
                <div className="h-8 w-8 rounded-full bg-emerald-100 dark:bg-emerald-950/60 text-emerald-600 dark:text-emerald-400 flex items-center justify-center font-bold">
                  <Send className="h-4 w-4" />
                </div>
                <div>
                  <h3 className="text-sm font-bold text-slate-900 dark:text-white">
                    {locale === 'vi-VN' ? 'Xác nhận thông tin chuyển tiền' : 'Confirm Transfer Details'}
                  </h3>
                  <p className="text-[10px] text-slate-400">
                    {activePayment ? (locale === 'vi-VN' ? 'Lệnh thanh toán đã tạo' : 'Payment Instruction Created') : (locale === 'vi-VN' ? 'Kiểm tra thông tin trước khi xác nhận' : 'Verify details before confirmation')}
                  </p>
                </div>
              </div>
              <button
                type="button"
                onClick={() => setTransferStatus('draft')}
                className="text-slate-400 hover:text-slate-600 dark:hover:text-slate-200 transition-colors p-1"
              >
                <X className="h-5 w-5" />
              </button>
            </div>

            {/* Error Banner */}
            {submitError && (
              <div className="p-3 rounded-xl bg-rose-50 dark:bg-rose-950/40 border border-rose-200 dark:border-rose-900 text-xs text-rose-700 dark:text-rose-400 flex items-start gap-2">
                <AlertCircle className="h-4 w-4 shrink-0 mt-0.5" />
                <span>{submitError}</span>
              </div>
            )}

            {/* Recipient & Amount Preview */}
            <div className="text-center py-2 space-y-1">
              <p className="text-xs text-slate-500 dark:text-slate-400">
                {locale === 'vi-VN' ? 'Chuyển tiền đến' : 'Transferring to'}
              </p>
              <h4 className="text-base font-bold text-slate-900 dark:text-white">
                {activePayment?.recipientDisplayName || selectedRecipient?.fullName}
              </h4>
              <p className="text-xs font-mono text-slate-500 dark:text-slate-400">
                {activePayment?.recipientPhone || recipientPhone}
              </p>
              <p className="text-2xl sm:text-3xl font-extrabold text-emerald-600 dark:text-emerald-400 tabular-nums pt-1">
                {formatMoney(activePayment?.amount || draftTransferAmount, locale)}
              </p>
            </div>

            {/* Payment Instruction Box (Truthful UI: Shows backend reference, no fabricated QR) */}
            <div className="p-4 rounded-2xl bg-slate-50 dark:bg-slate-800/60 border border-slate-200/80 dark:border-slate-700/80 space-y-3.5">
              <div className="flex items-center justify-between pb-2 border-b border-slate-200/70 dark:border-slate-700/70">
                <div className="flex items-center gap-2">
                  <div className="h-6 w-6 rounded-md bg-emerald-600 text-white flex items-center justify-center font-bold text-xs">
                    <Building2 className="h-3.5 w-3.5" />
                  </div>
                  <div>
                    <p className="text-xs font-bold text-slate-900 dark:text-white">
                      {locale === 'vi-VN' ? 'Thông tin chuyển tiền Bank-Direct' : 'Bank-Direct Payment Instruction'}
                    </p>
                  </div>
                </div>
                <span className={cn(
                  'text-[10px] font-bold px-2 py-0.5 rounded-full border',
                  activePayment?.status.toLowerCase() === 'completed'
                    ? 'bg-emerald-50 dark:bg-emerald-950/40 text-emerald-700 dark:text-emerald-400 border-emerald-300 dark:border-emerald-800'
                    : activePayment?.status.toLowerCase() === 'awaitingreconciliation'
                    ? 'bg-blue-50 dark:bg-blue-950/40 text-blue-700 dark:text-blue-400 border-blue-300 dark:border-blue-800'
                    : activePayment?.status.toLowerCase() === 'failed' || activePayment?.status.toLowerCase() === 'cancelled'
                    ? 'bg-rose-50 dark:bg-rose-950/40 text-rose-700 dark:text-rose-400 border-rose-300 dark:border-rose-800'
                    : 'bg-amber-50 dark:bg-amber-950/40 text-amber-700 dark:text-amber-400 border-amber-300 dark:border-amber-800'
                )}>
                  {activePayment?.status || (locale === 'vi-VN' ? 'Chờ xác nhận' : 'Pending')}
                </span>
              </div>

              {/* Instruction Details Table */}
              <div className="space-y-2 text-xs">
                <div className="flex items-center justify-between">
                  <span className="text-slate-500 dark:text-slate-400">
                    {locale === 'vi-VN' ? 'Ngân hàng thụ hưởng:' : 'Beneficiary Bank:'}
                  </span>
                  <span className="font-bold text-slate-900 dark:text-white flex items-center gap-1.5">
                    {activePayment?.bankBrand || selectedRecipient?.bankBrand || '—'}
                  </span>
                </div>

                <div className="flex items-center justify-between">
                  <span className="text-slate-500 dark:text-slate-400">
                    {locale === 'vi-VN' ? 'Tài khoản thụ hưởng:' : 'Beneficiary Account:'}
                  </span>
                  <span className="font-mono font-bold text-slate-900 dark:text-white">
                    {activePayment?.maskedAccount || selectedRecipient?.maskedAccount || '••••'}
                  </span>
                </div>

                <div className="flex items-center justify-between">
                  <span className="text-slate-500 dark:text-slate-400">
                    {locale === 'vi-VN' ? 'Tên chủ tài khoản:' : 'Account Holder:'}
                  </span>
                  <span className="font-medium text-slate-800 dark:text-slate-200">
                    {activePayment?.accountHolder || selectedRecipient?.accountHolder || activePayment?.recipientDisplayName || selectedRecipient?.fullName}
                  </span>
                </div>

                {activePayment?.paymentReference && (
                  <div className="flex items-center justify-between pt-2 border-t border-slate-200/60 dark:border-slate-700/60">
                    <span className="text-slate-600 dark:text-slate-300 font-semibold">
                      {locale === 'vi-VN' ? 'Mã tham chiếu thanh toán:' : 'Payment Reference:'}
                    </span>
                    <div className="flex items-center gap-2 font-mono font-bold text-emerald-700 dark:text-emerald-300 bg-emerald-50 dark:bg-emerald-950/60 px-2 py-0.5 rounded-lg border border-emerald-200 dark:border-emerald-800">
                      <span>{activePayment.paymentReference}</span>
                      <button
                        type="button"
                        onClick={() => handleCopyText(activePayment.paymentReference, 'ref')}
                        className="text-emerald-600 hover:text-emerald-800 dark:hover:text-emerald-200 transition-colors"
                        title={locale === 'vi-VN' ? 'Sao chép' : 'Copy'}
                      >
                        {copiedField === 'ref' ? <Check className="h-3.5 w-3.5 text-emerald-600" /> : <Copy className="h-3.5 w-3.5" />}
                      </button>
                    </div>
                  </div>
                )}
              </div>

              {/* Informative Note */}
              <div className="p-2.5 rounded-xl bg-blue-50/70 dark:bg-blue-950/30 border border-blue-200/80 dark:border-blue-900/40 text-[11px] text-blue-900 dark:text-blue-300 flex items-start gap-2">
                <Info className="h-4 w-4 shrink-0 text-blue-600 dark:text-blue-400 mt-0.5" />
                <p className="leading-relaxed">
                  {locale === 'vi-VN'
                    ? 'Giao dịch được quản lý an toàn qua hệ sinh thái SpendWise. Vui lòng sử dụng mã tham chiếu thanh toán chính xác để đối soát tự động.'
                    : 'Transfers are securely verified by SpendWise. Ensure the exact payment reference is used for automated reconciliation.'}
                </p>
              </div>
            </div>

            {/* Modal Actions */}
            <div className="flex items-center gap-3 pt-2">
              <Button
                type="button"
                variant="outline"
                size="lg"
                className="flex-1 text-xs"
                onClick={() => setTransferStatus('draft')}
              >
                {locale === 'vi-VN' ? 'Đóng' : 'Close'}
              </Button>

              {!activePayment ? (
                <Button
                  type="button"
                  size="lg"
                  className="flex-1 text-xs font-bold bg-emerald-600 hover:bg-emerald-700 text-white"
                  disabled={isSubmitting}
                  onClick={handleExecuteTransfer}
                >
                  {isSubmitting ? (
                    <span className="flex items-center gap-1.5">
                      <Loader2 className="h-3.5 w-3.5 animate-spin" />
                      <span>{locale === 'vi-VN' ? 'Đang gửi...' : 'Submitting...'}</span>
                    </span>
                  ) : (
                    <span>{locale === 'vi-VN' ? 'Xác nhận bằng mã PIN' : 'Confirm with PIN'}</span>
                  )}
                </Button>
              ) : (
                <Button
                  type="button"
                  size="lg"
                  className="flex-1 text-xs font-bold bg-emerald-600 hover:bg-emerald-700 text-white"
                  disabled={isCheckingStatus}
                  onClick={handleCheckPaymentStatus}
                >
                  {isCheckingStatus ? (
                    <span className="flex items-center gap-1.5">
                      <Loader2 className="h-3.5 w-3.5 animate-spin" />
                      <span>{locale === 'vi-VN' ? 'Đang kiểm tra...' : 'Checking...'}</span>
                    </span>
                  ) : (
                    <span className="flex items-center gap-1.5">
                      <RefreshCw className="h-3.5 w-3.5" />
                      <span>{locale === 'vi-VN' ? 'Kiểm tra trạng thái' : 'Check Status'}</span>
                    </span>
                  )}
                </Button>
              )}
            </div>
          </div>
        </div>
      )}

      {/* Transfer Success Modal */}
      {transferStatus === 'completed' && activePayment && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/70 backdrop-blur-xs animate-in fade-in duration-150">
          <div className="w-full max-w-md bg-white dark:bg-slate-900 rounded-3xl p-6 sm:p-8 border border-slate-100 dark:border-slate-800 shadow-2xl space-y-6 text-center animate-in zoom-in-95 duration-150">
            <div className="h-16 w-16 rounded-full bg-emerald-100 dark:bg-emerald-950/60 text-emerald-600 dark:text-emerald-400 mx-auto flex items-center justify-center shadow-xs">
              <CheckCircle2 className="h-8 w-8" />
            </div>

            <div>
              <h3 className="text-xl font-bold text-slate-900 dark:text-white">
                {locale === 'vi-VN' ? 'Chuyển Tiền Thành Công' : 'Transfer Completed'}
              </h3>
              {activePayment.paymentReference && (
                <p className="text-xs text-slate-500 dark:text-slate-400 mt-1">
                  {locale === 'vi-VN' ? 'Mã tham chiếu:' : 'Reference:'}{' '}
                  <span className="font-mono font-bold text-slate-800 dark:text-slate-200">
                    {activePayment.paymentReference}
                  </span>
                </p>
              )}
            </div>

            <div className="p-4 rounded-2xl bg-slate-50 dark:bg-slate-800/60 text-left space-y-2 text-xs border border-slate-200/60 dark:border-slate-700/60">
              <div className="flex justify-between">
                <span className="text-slate-500">{t('transfer.recipientLabel')}:</span>
                <span className="font-bold text-slate-800 dark:text-slate-200">
                  {activePayment.recipientDisplayName} ({activePayment.recipientPhone})
                </span>
              </div>
              <div className="flex justify-between">
                <span className="text-slate-500">{locale === 'vi-VN' ? 'Ngân hàng:' : 'Bank:'}</span>
                <span className="font-bold text-slate-800 dark:text-slate-200">
                  {activePayment.bankBrand} ({activePayment.maskedAccount})
                </span>
              </div>
              <div className="flex justify-between">
                <span className="text-slate-500">{t('transfer.amountLabel')}:</span>
                <span className="font-bold text-emerald-600 dark:text-emerald-400 tabular-nums">
                  {formatMoney(activePayment.amount, locale)}
                </span>
              </div>
              {activePayment.note && (
                <div className="flex justify-between">
                  <span className="text-slate-500">{t('transfer.noteLabel')}:</span>
                  <span className="text-slate-700 dark:text-slate-300">{activePayment.note}</span>
                </div>
              )}
              <div className="flex justify-between pt-1 border-t border-slate-200/60 dark:border-slate-700/60">
                <span className="text-slate-500">{locale === 'vi-VN' ? 'Trạng thái đối soát:' : 'Reconciliation:'}</span>
                <span className="font-bold text-emerald-600 dark:text-emerald-400 flex items-center gap-1">
                  <ShieldCheck className="h-3.5 w-3.5" />
                  {locale === 'vi-VN' ? 'Đã xác nhận thành công' : 'Confirmed'}
                </span>
              </div>
            </div>

            <Button size="lg" className="w-full font-bold text-xs bg-emerald-600 hover:bg-emerald-700 text-white" onClick={handleDone}>
              {locale === 'vi-VN' ? 'Hoàn tất' : 'Done'}
            </Button>
          </div>
        </div>
      )}

      {/* Source Wallet Panel */}
      {sourceWallet && (
        <div className="rounded-3xl p-5 border border-emerald-200 dark:border-emerald-900/60 bg-emerald-50/60 dark:bg-emerald-950/20 flex items-center justify-between shadow-xs">
          <div className="flex items-center gap-3.5">
            <div className="h-11 w-11 rounded-2xl bg-emerald-600 text-white flex items-center justify-center shrink-0 shadow-xs">
              <Landmark className="h-5 w-5" />
            </div>
            <div>
              <div className="flex items-center gap-2">
                <span className="text-xs font-semibold text-slate-500 dark:text-slate-400">
                  {locale === 'vi-VN' ? 'Ví nguồn' : 'Source Wallet'}
                </span>
                {sourceWallet.isDefault && (
                  <span className="text-[10px] font-bold px-2 py-0.5 rounded-full bg-white dark:bg-slate-900 text-emerald-700 dark:text-emerald-300 border border-emerald-300 dark:border-emerald-800">
                    {locale === 'vi-VN' ? 'Mặc định' : 'Default'}
                  </span>
                )}
              </div>
              <p className="text-base font-bold text-slate-900 dark:text-white mt-0.5">
                {sourceWallet.name}
              </p>
            </div>
          </div>
          <div className="text-right">
            <p className="text-xs text-slate-500 dark:text-slate-400">
              {locale === 'vi-VN' ? 'Số dư khả dụng' : 'Available Balance'}
            </p>
            <p className="text-lg sm:text-xl font-extrabold text-slate-900 dark:text-white tabular-nums font-mono">
              {formatMoney(sourceBalance, locale)}
            </p>
          </div>
        </div>
      )}

      {/* Main Grid: Form on Left, Pending & History on Right */}
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 items-start">
        {/* Left Form Area */}
        <div className="lg:col-span-7 bg-white dark:bg-slate-900 rounded-3xl p-6 sm:p-8 border border-slate-100 dark:border-slate-800 shadow-xs space-y-6">
          <form onSubmit={handleOpenConfirm} className="space-y-6">
            {/* Phone Lookup Section */}
            <div>
              <div className="flex items-center justify-between mb-2">
                <label className="text-xs font-bold text-slate-700 dark:text-slate-300 uppercase tracking-wider">
                  {locale === 'vi-VN' ? 'Số điện thoại người nhận' : 'Recipient Phone Number'}
                </label>
                <span className="text-[11px] text-slate-400">
                  {locale === 'vi-VN' ? '10 chữ số (bắt đầu 0)' : 'Exact 10 digits (starts with 0)'}
                </span>
              </div>

              <div className="relative">
                <Smartphone className="absolute left-3.5 top-1/2 -translate-y-1/2 h-4 w-4 text-slate-400" />
                <input
                  type="tel"
                  inputMode="numeric"
                  value={recipientPhone}
                  onChange={(e) => void performPhoneLookup(e.target.value)}
                  maxLength={10}
                  placeholder="0912345678"
                  className={cn(
                    'w-full pl-10 pr-14 py-3 text-xs sm:text-sm font-mono rounded-2xl border bg-slate-50/50 dark:bg-slate-800 text-slate-900 dark:text-white placeholder:text-slate-400 focus:outline-hidden focus:ring-2',
                    lookupState === 'found'
                      ? 'border-emerald-500 focus:ring-emerald-500/40'
                      : lookupState === 'bank_required'
                      ? 'border-slate-200 dark:border-slate-700 focus:ring-emerald-500/40'
                      : lookupState === 'not_found' || lookupState === 'self' || lookupState === 'inactive' || lookupState === 'invalid_format'
                      ? 'border-rose-500 focus:ring-rose-500/40'
                      : 'border-slate-200 dark:border-slate-700 focus:ring-emerald-500/40'
                  )}
                />
                {recipientPhone && (
                  <span className="absolute right-3.5 top-1/2 -translate-y-1/2 text-[10px] font-mono text-slate-400">
                    {recipientPhone.length}/10
                  </span>
                )}
              </div>

              {/* Friend Suggestions */}
              {suggestedFriends.length > 0 && (
                <div className="mt-2.5">
                  <p className="text-[11px] font-semibold text-slate-500 dark:text-slate-400 mb-1.5">
                    {locale === 'vi-VN' ? 'Gợi ý từ bạn bè:' : 'Suggested friends:'}
                  </p>
                  <div className="flex flex-wrap gap-1.5 max-h-24 overflow-y-auto">
                    {suggestedFriends.map((friend) => {
                      const sanitizedFriendPhone = sanitizeVietnamPhoneInput(friend.phone || '');
                      const hasValidPhone = Boolean(friend.phone && isValidVietnamPhone(sanitizedFriendPhone));
                      const isSelected = recipientPhone === sanitizedFriendPhone;
                      return (
                        <button
                          key={friend.userId}
                          type="button"
                          disabled={!hasValidPhone}
                          onClick={() => {
                            if (friend.phone && hasValidPhone) {
                              void performPhoneLookup(friend.phone);
                            }
                          }}
                          className={cn(
                            'inline-flex items-center gap-1.5 px-2.5 py-1 rounded-xl text-xs transition border cursor-pointer',
                            !hasValidPhone
                              ? 'opacity-40 cursor-not-allowed border-slate-200 dark:border-slate-800 text-slate-400'
                              : isSelected
                              ? 'bg-emerald-50 dark:bg-emerald-950/60 border-emerald-500 text-emerald-700 dark:text-emerald-300 font-semibold'
                              : 'bg-white dark:bg-slate-800 border-slate-200 dark:border-slate-700 hover:border-emerald-400 text-slate-700 dark:text-slate-200'
                          )}
                        >
                          <span className="w-5 h-5 rounded-full bg-purple-100 dark:bg-purple-900/60 text-purple-700 dark:text-purple-300 flex items-center justify-center text-[10px] font-bold">
                            {friend.avatarInitials || (friend.nickname || friend.name).slice(0, 1)}
                          </span>
                          <span className="truncate max-w-[120px]">{friend.nickname || friend.name}</span>
                          {friend.isFavorite && <span className="text-amber-500 text-[10px]">★</span>}
                        </button>
                      );
                    })}
                  </div>
                </div>
              )}

              {/* Status Feedback */}
              {lookupState === 'searching' && (
                <p className="text-[11px] text-slate-500 dark:text-slate-400 mt-1.5 flex items-center gap-1.5">
                  <Loader2 className="h-3.5 w-3.5 animate-spin text-emerald-600" />
                  <span>{locale === 'vi-VN' ? 'Đang kiểm tra tài khoản...' : 'Looking up recipient...'}</span>
                </p>
              )}

              {lookupState === 'idle' && recipientPhone.length > 0 && recipientPhone.length < 10 && (
                <p className="text-[11px] text-slate-500 dark:text-slate-400 mt-1.5 flex items-center gap-1.5">
                  <Info className="h-3.5 w-3.5 shrink-0 text-slate-400" />
                  <span>
                    {locale === 'vi-VN'
                      ? `Cần nhập đủ 10 chữ số hợp lệ để tra cứu (đã nhập ${recipientPhone.length}/10)`
                      : `Enter 10 digits to search (${recipientPhone.length}/10)`}
                  </span>
                </p>
              )}

              {lookupState === 'invalid_format' && (
                <p className="text-xs font-semibold text-rose-500 dark:text-rose-400 mt-1.5 flex items-center gap-1.5">
                  <AlertCircle className="h-3.5 w-3.5 shrink-0" />
                  <span>
                    {locale === 'vi-VN'
                      ? 'Số điện thoại phải gồm đúng 10 chữ số và bắt đầu bằng 0.'
                      : 'Phone number must be exactly 10 digits starting with 0.'}
                  </span>
                </p>
              )}

              {lookupState === 'self' && (
                <p className="text-xs font-semibold text-rose-500 dark:text-rose-400 mt-1.5 flex items-center gap-1.5">
                  <AlertCircle className="h-3.5 w-3.5 shrink-0" />
                  <span>{t('transfer.selfTransferError')}</span>
                </p>
              )}

              {lookupState === 'inactive' && (
                <p className="text-xs font-semibold text-rose-500 dark:text-rose-400 mt-1.5 flex items-center gap-1.5">
                  <AlertCircle className="h-3.5 w-3.5 shrink-0" />
                  <span>
                    {locale === 'vi-VN'
                      ? 'Tài khoản người nhận đang bị khóa hoặc tạm dừng giao dịch.'
                      : 'Recipient account is locked or suspended.'}
                  </span>
                </p>
              )}

              {lookupState === 'not_found' && (
                <p className="text-xs font-semibold text-rose-500 dark:text-rose-400 mt-1.5 flex items-center gap-1.5">
                  <AlertCircle className="h-3.5 w-3.5 shrink-0" />
                  <span>
                    {locale === 'vi-VN'
                      ? 'Không tìm thấy người dùng nào với số điện thoại này trong hệ thống.'
                      : 'No user found with this phone number.'}
                  </span>
                </p>
              )}

              {lookupState === 'bank_required' && (
                <div className="mt-3 p-3.5 rounded-2xl border border-amber-300 dark:border-amber-800/60 bg-amber-50/80 dark:bg-amber-950/30 flex items-start gap-3 animate-in fade-in-50">
                  <AlertCircle className="h-5 w-5 shrink-0 text-amber-600 dark:text-amber-400 mt-0.5" />
                  <div className="text-xs">
                    <div className="flex items-center gap-2 mb-1">
                      <span className="font-semibold text-slate-600 dark:text-slate-300">
                        {locale === 'vi-VN' ? '✓ Số điện thoại hợp lệ' : '✓ Valid phone number'}
                      </span>
                      <span className="text-slate-300 dark:text-slate-600">•</span>
                      <span className="font-bold text-amber-800 dark:text-amber-300">
                        {locale === 'vi-VN' ? 'Người nhận chưa đủ điều kiện' : 'Recipient not eligible'}
                      </span>
                    </div>
                    <p className="text-amber-900 dark:text-amber-200 leading-relaxed">
                      {locale === 'vi-VN'
                        ? 'Người nhận chưa có tài khoản ngân hàng đang hoạt động để nhận chuyển tiền.'
                        : 'The recipient does not have an active bank account available for transfers.'}
                    </p>
                  </div>
                </div>
              )}

              {/* Matched Recipient Card */}
              {lookupState === 'found' && selectedRecipient && (
                <div className="mt-3 space-y-2.5 animate-in fade-in-50">
                  <div className="p-3.5 rounded-2xl bg-emerald-50/80 dark:bg-emerald-950/30 border border-emerald-200 dark:border-emerald-900/50 flex items-center justify-between">
                    <div className="flex items-center gap-3">
                      <div className="h-10 w-10 rounded-full bg-emerald-600 text-white font-bold flex items-center justify-center text-xs shadow-xs shrink-0">
                        {selectedRecipient.fullName.slice(0, 2).toUpperCase()}
                      </div>
                      <div>
                        <p className="text-xs font-bold text-slate-900 dark:text-white">
                          {selectedRecipient.fullName}
                        </p>
                        <p className="text-[11px] font-mono font-medium text-emerald-700 dark:text-emerald-400">
                          {recipientPhone}
                        </p>
                      </div>
                    </div>
                    <span className="text-[10px] font-bold text-emerald-700 dark:text-emerald-400 bg-white dark:bg-slate-900 px-2.5 py-1 rounded-full border border-emerald-300 dark:border-emerald-800 flex items-center gap-1">
                      <CheckCircle2 className="w-3 h-3 text-emerald-500" />
                      {locale === 'vi-VN' ? 'Đã xác thực' : 'Verified'}
                    </span>
                  </div>

                  {selectedRecipient.bankBrand ? (
                    <div className="p-3.5 rounded-2xl bg-slate-50/90 dark:bg-slate-800/80 border border-slate-200 dark:border-slate-700 flex items-center justify-between">
                      <div className="flex items-center gap-3">
                        <div className="h-10 w-10 rounded-xl bg-blue-100 dark:bg-blue-950/60 text-blue-600 dark:text-blue-400 flex items-center justify-center shrink-0">
                          <Landmark className="h-5 w-5" />
                        </div>
                        <div>
                          <p className="text-xs font-bold text-slate-900 dark:text-white">
                            {selectedRecipient.bankBrand}
                          </p>
                          <p className="text-[11px] font-mono text-slate-600 dark:text-slate-400">
                            {selectedRecipient.maskedAccount}
                            {selectedRecipient.accountHolder ? ` • ${selectedRecipient.accountHolder}` : ''}
                          </p>
                        </div>
                      </div>
                      <span className="text-[10px] font-bold text-emerald-700 dark:text-emerald-400 bg-emerald-50 dark:bg-emerald-950/40 px-2.5 py-1 rounded-full border border-emerald-200 dark:border-emerald-800 flex items-center gap-1">
                        <Check className="w-3 h-3 text-emerald-500" />
                        {locale === 'vi-VN' ? 'Tài khoản hoạt động' : 'Active Account'}
                      </span>
                    </div>
                  ) : (
                    <div className="p-3.5 rounded-2xl bg-amber-50/90 dark:bg-amber-950/40 border border-amber-300 dark:border-amber-800/60 flex items-start gap-2.5 text-xs text-amber-900 dark:text-amber-200">
                      <AlertCircle className="h-4 w-4 shrink-0 text-amber-600 dark:text-amber-400 mt-0.5" />
                      <div>
                        <p className="font-bold">
                          {locale === 'vi-VN' ? 'Người nhận chưa liên kết tài khoản ngân hàng hoạt động' : 'Recipient has no active linked bank account'}
                        </p>
                        <p className="text-[11px] text-amber-800 dark:text-amber-300 mt-0.5 leading-relaxed">
                          {locale === 'vi-VN'
                            ? 'Người nhận chưa có tài khoản ngân hàng hoạt động để nhận tiền chuyển trực tiếp.'
                            : 'This recipient has no active bank account to receive direct transfers.'}
                        </p>
                      </div>
                    </div>
                  )}
                </div>
              )}
            </div>

            {/* Amount Field & Quick Chips */}
            <div className="p-5 rounded-2xl bg-slate-50/80 dark:bg-slate-800/40 border border-slate-200/80 dark:border-slate-700/80 space-y-3">
              <div className="flex items-center justify-between">
                <label className="block text-xs font-bold text-slate-700 dark:text-slate-300 uppercase tracking-wider">
                  {t('transfer.amount')}
                </label>
                <span className="text-[11px] text-slate-400">VND</span>
              </div>

              <div className="relative">
                <input
                  type="text"
                  inputMode="numeric"
                  value={displayAmount}
                  onChange={(e) => setAmount(sanitizeIntegerInput(e.target.value))}
                  required
                  placeholder="0"
                  className="w-full px-4 py-3.5 text-2xl sm:text-3xl font-extrabold rounded-xl border border-slate-300 dark:border-slate-600 bg-white dark:bg-slate-900 text-slate-900 dark:text-white focus:outline-hidden focus:ring-2 focus:ring-emerald-500 tabular-nums shadow-2xs"
                />
                <span className="absolute right-4 top-1/2 -translate-y-1/2 text-lg font-bold text-slate-400">
                  ₫
                </span>
              </div>

              {/* Quick Amount Chips */}
              <div className="flex flex-wrap gap-1.5 pt-1">
                {quickAmounts.map((q) => {
                  const isChipActive = draftTransferAmount === q;
                  return (
                    <button
                      key={q}
                      type="button"
                      onClick={() => setAmount(q.toString())}
                      className={cn(
                        'px-2.5 py-1 text-[11px] font-semibold rounded-lg border transition-colors cursor-pointer',
                        isChipActive
                          ? 'border-emerald-500 bg-emerald-50 dark:bg-emerald-950/60 text-emerald-700 dark:text-emerald-300 font-bold'
                          : 'border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 text-slate-600 dark:text-slate-300 hover:border-emerald-500 hover:text-emerald-600'
                      )}
                    >
                      {formatMoney(q, locale)}
                    </button>
                  );
                })}
              </div>

              {/* Post-Transfer Balance Preview */}
              <div className="flex items-center justify-between text-xs pt-2 border-t border-slate-200/70 dark:border-slate-700/70 text-slate-500 dark:text-slate-400">
                <span>{locale === 'vi-VN' ? 'Số dư sau chuyển' : 'Projected balance'}</span>
                {isOverBalance ? (
                  <span className="font-bold text-rose-500 dark:text-rose-400 flex items-center gap-1">
                    <AlertCircle className="h-3.5 w-3.5" />
                    {locale === 'vi-VN' ? 'Vượt quá số dư khả dụng' : 'Exceeds available balance'}
                  </span>
                ) : isAmountValid && draftTransferAmount > 0 ? (
                  <strong className="text-sm tabular-nums text-slate-800 dark:text-slate-100 font-mono">
                    {formatMoney(projectedBalance, locale)}
                  </strong>
                ) : (
                  <span className="text-slate-400">—</span>
                )}
              </div>
            </div>

            {/* Note Field */}
            <div>
              <label className="block text-xs font-bold text-slate-700 dark:text-slate-300 uppercase tracking-wider mb-2">
                {t('transfer.note')}
              </label>
              <input
                type="text"
                value={note}
                onChange={(e) => setNote(e.target.value)}
                maxLength={200}
                placeholder={locale === 'vi-VN' ? 'Nhập lời nhắn chuyển tiền (tùy chọn)' : 'Transfer message (optional)'}
                className="w-full px-4 py-3 text-xs sm:text-sm rounded-2xl border border-slate-200 dark:border-slate-700 bg-slate-50/50 dark:bg-slate-800 text-slate-900 dark:text-white placeholder:text-slate-400 focus:outline-hidden focus:ring-2 focus:ring-emerald-500/40"
              />
            </div>

            {/* Submit Button */}
            <Button
              type="submit"
              size="lg"
              className="w-full py-4 text-sm font-bold bg-emerald-600 hover:bg-emerald-700 text-white rounded-2xl shadow-xs"
              disabled={lookupState !== 'found' || !selectedRecipient || !isAmountValid || draftTransferAmount <= 0}
            >
              <Send className="h-4 w-4 mr-2" />
              <span>{locale === 'vi-VN' ? 'Tiếp tục chuyển tiền' : 'Continue to Transfer'}</span>
            </Button>
          </form>
        </div>

        {/* Right Column: Pending Reconciliations & History */}
        <div className="lg:col-span-5 space-y-6">
          {/* Transfer Conditions Rail */}
          <div className="bg-white dark:bg-slate-900 rounded-3xl p-6 border border-slate-100 dark:border-slate-800 shadow-xs space-y-3">
            <h3 className="text-base font-bold text-slate-800 dark:text-white">
              {locale === 'vi-VN' ? 'Điều kiện chuyển tiền' : 'Transfer Requirements'}
            </h3>
            <ol className="space-y-2.5 text-xs sm:text-sm text-slate-600 dark:text-slate-300">
              <li className="flex gap-2">
                <span className="font-bold text-emerald-600 dark:text-emerald-400 shrink-0">1.</span>
                <span>
                  {locale === 'vi-VN'
                    ? 'Người nhận phải có tài khoản ngân hàng đang hoạt động đã liên kết.'
                    : 'Recipient must have an active linked bank account.'}
                </span>
              </li>
              <li className="flex gap-2">
                <span className="font-bold text-emerald-600 dark:text-emerald-400 shrink-0">2.</span>
                <span>
                  {locale === 'vi-VN'
                    ? 'Kiểm tra lại thông tin người nhận và số tiền trước khi xác nhận.'
                    : 'Review recipient details and transfer amount before confirming.'}
                </span>
              </li>
              <li className="flex gap-2">
                <span className="font-bold text-emerald-600 dark:text-emerald-400 shrink-0">3.</span>
                <span>
                  {locale === 'vi-VN'
                    ? 'Giao dịch cần xác nhận bằng mã PIN bảo mật.'
                    : 'Transactions require confirmation with your security PIN.'}
                </span>
              </li>
            </ol>
          </div>

          {/* Pending Transfers Awaiting Reconciliation */}
          {pendingPayments.length > 0 && (
            <div className="bg-white dark:bg-slate-900 rounded-3xl p-6 border border-amber-200 dark:border-amber-900/60 shadow-xs space-y-4">
              <div className="flex items-center justify-between">
                <h3 className="text-sm font-bold text-slate-800 dark:text-white flex items-center gap-2">
                  <Clock className="h-4 w-4 text-amber-500" />
                  <span>{locale === 'vi-VN' ? 'Lệnh chờ đối soát' : 'Pending Reconciliations'}</span>
                </h3>
                <span className="text-[10px] font-bold px-2 py-0.5 rounded-full bg-amber-100 dark:bg-amber-950/60 text-amber-700 dark:text-amber-400">
                  {pendingPayments.length}
                </span>
              </div>

              <div className="divide-y divide-slate-100 dark:divide-slate-800/80">
                {pendingPayments.map((p) => (
                  <div key={p.id} className="py-3 space-y-2">
                    <div className="flex items-center justify-between">
                      <div>
                        <p className="text-xs font-bold text-slate-800 dark:text-slate-200">
                          {p.recipientDisplayName || '—'}
                        </p>
                        <p className="text-[11px] text-slate-400">
                          {p.bankBrand} • {p.maskedAccount}
                        </p>
                      </div>
                      <div className="text-right">
                        <p className="text-xs font-bold text-emerald-600 dark:text-emerald-400 tabular-nums">
                          {formatMoney(p.amount, locale)}
                        </p>
                        <span className="text-[10px] font-mono text-slate-400">
                          {p.paymentReference}
                        </span>
                      </div>
                    </div>

                    <div className="flex items-center justify-between pt-1">
                      <button
                        type="button"
                        onClick={() => {
                          setActivePayment(p);
                          setTransferStatus('review');
                        }}
                        className="text-[11px] font-semibold text-emerald-600 hover:text-emerald-700 flex items-center gap-1 cursor-pointer"
                      >
                        <ExternalLink className="h-3 w-3" />
                        <span>{locale === 'vi-VN' ? 'Xem chi tiết lệnh' : 'View Instruction'}</span>
                      </button>

                      <div className="flex items-center gap-2">
                        <span className={cn(
                          'text-[9px] font-semibold px-1.5 py-0.5 rounded-full border',
                          p.status?.toLowerCase() === 'awaitingreconciliation'
                            ? 'bg-blue-50 dark:bg-blue-950/40 text-blue-700 dark:text-blue-400 border-blue-300 dark:border-blue-800'
                            : 'bg-amber-50 dark:bg-amber-950/40 text-amber-700 dark:text-amber-400 border-amber-300 dark:border-amber-800'
                        )}>
                          {p.status}
                        </span>
                        <span className="text-[10px] font-mono text-amber-600 dark:text-amber-400">
                          {formatDate(p.createdAt, locale)}
                        </span>
                      </div>
                    </div>
                  </div>
                ))}
              </div>
            </div>
          )}

          {/* Transfer History (from backend GET /api/transfers) */}
          <div className="bg-white dark:bg-slate-900 rounded-3xl p-6 border border-slate-100 dark:border-slate-800 shadow-xs space-y-4">
            <div className="flex items-center justify-between">
              <h3 className="text-base font-bold text-slate-800 dark:text-white flex items-center gap-2">
                <Clock className="h-4 w-4 text-emerald-500" />
                <span>{locale === 'vi-VN' ? 'Lịch sử chuyển tiền' : 'Transfer History'}</span>
              </h3>
              <button
                type="button"
                onClick={refreshTransfers}
                disabled={isLoadingTransfers}
                className="text-slate-400 hover:text-slate-600 dark:hover:text-slate-200 transition-colors p-1"
                title={locale === 'vi-VN' ? 'Làm mới' : 'Refresh'}
              >
                <RefreshCw className={cn('h-3.5 w-3.5', isLoadingTransfers && 'animate-spin')} />
              </button>
            </div>

            <div className="divide-y divide-slate-100 dark:divide-slate-800/80">
              {completedTransfers.length === 0 ? (
                <div className="py-6 text-center text-xs text-slate-400">
                  {locale === 'vi-VN' ? 'Chưa có giao dịch chuyển tiền hoàn tất nào.' : 'No completed transfers.'}
                </div>
              ) : (
                completedTransfers.map((tx) => (
                  <div key={tx.id} className="py-3 flex items-center justify-between">
                    <div className="flex items-center gap-3">
                      <div className="h-9 w-9 rounded-full bg-slate-100 dark:bg-slate-800 text-slate-700 dark:text-slate-300 flex items-center justify-center font-bold text-xs">
                        {(tx.recipientDisplayName || 'U').slice(0, 1).toUpperCase()}
                      </div>
                      <div>
                        <p className="text-xs sm:text-sm font-bold text-slate-800 dark:text-slate-200 truncate">
                          {tx.recipientDisplayName || '—'}
                        </p>
                        <p className="text-[11px] text-slate-400">
                          {tx.note || (locale === 'vi-VN' ? 'Chuyển tiền' : 'Transfer')} • {formatDate(tx.completedAt || tx.createdAt, locale)}
                        </p>
                      </div>
                    </div>

                    <div className="text-right">
                      <span className="text-xs sm:text-sm font-bold text-slate-800 dark:text-slate-200 tabular-nums">
                        -{formatMoney(tx.amount, locale)}
                      </span>
                      <p className="text-[10px] font-mono text-emerald-600 dark:text-emerald-400">
                        {tx.bankBrand}
                      </p>
                    </div>
                  </div>
                ))
              )}
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};
