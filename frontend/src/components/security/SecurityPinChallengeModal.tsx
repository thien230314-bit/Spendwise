import React, { useState, useEffect } from 'react';
import { Shield, X, AlertCircle, Loader2 } from 'lucide-react';
import { useAuth } from '../../providers/AuthProvider';
import { useI18n } from '../../i18n/I18nContext';
import { securityPinService, isValidSecurityPin } from '../../services/securityPinService';
import { SixDigitPinInput } from './SixDigitPinInput';

export interface SecurityPinChallengeProps {
  isOpen: boolean;
  title?: string;
  description?: string;
  amount?: number;
  onSuccess: (enteredPin?: string) => void;
  onCancel: () => void;
}

export const SecurityPinChallengeModal: React.FC<SecurityPinChallengeProps> = ({
  isOpen,
  title,
  description,
  amount,
  onSuccess,
  onCancel,
}) => {
  const { currentUser } = useAuth();
  const { locale } = useI18n();
  const isVi = locale === 'vi-VN';

  const [pin, setPin] = useState('');
  const [mask, setMask] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [isVerifying, setIsVerifying] = useState(false);
  const [isPinSet, setIsPinSet] = useState<boolean | null>(null);
  const [isServiceUnavailable, setIsServiceUnavailable] = useState(false);

  // Recovery flow states
  const [isRecoveryOpen, setIsRecoveryOpen] = useState(false);
  const [recoveryEmail, setRecoveryEmail] = useState('');
  const [recoverySuccess, setRecoverySuccess] = useState(false);

  useEffect(() => {
    if (isOpen) {
      setPin('');
      setError(null);
      setIsVerifying(false);
      setIsRecoveryOpen(false);
      setRecoverySuccess(false);
      setIsServiceUnavailable(false);
      setIsPinSet(null);

      if (currentUser?.id) {
        if (currentUser.email) {
          setRecoveryEmail(currentUser.email);
        }
        securityPinService
          .getStatus()
          .then((res) => {
            setIsPinSet(res.isSet);
          })
          .catch(() => {
            setIsPinSet(null);
            setIsServiceUnavailable(true);
          });
      }
    }
  }, [isOpen, currentUser]);

  if (!isOpen || !currentUser) {
    return null;
  }

  const executeVerify = async (candidatePin: string) => {
    if (isVerifying) return;

    if (!isValidSecurityPin(candidatePin)) {
      setError(
        isVi
          ? 'Mã bảo mật phải bao gồm đúng 6 chữ số.'
          : 'Security PIN must be exactly 6 digits.'
      );
      return;
    }

    setIsVerifying(true);
    setError(null);

    try {
      const isValid = await securityPinService.verifyPin(candidatePin);
      if (isValid) {
        setIsVerifying(false);
        onSuccess(candidatePin);
      } else {
        setIsVerifying(false);
        setError(
          isVi
            ? 'Mã bảo mật không chính xác. Vui lòng thử lại.'
            : 'Incorrect Security PIN. Please try again.'
        );
        setPin('');
      }
    } catch (err: any) {
      setIsVerifying(false);
      setError(
        err?.detail ||
          err?.title ||
          (isVi
            ? 'Máy chủ không khả dụng hoặc lỗi kết nối. Vui lòng thử lại sau.'
            : 'Service unavailable or network connection error. Please try again later.')
      );
      setPin('');
    }
  };

  const handleSendRecovery = async (e: React.FormEvent) => {
    e.preventDefault();
    const emailToUse = recoveryEmail.trim() || currentUser?.email || '';
    if (!emailToUse) {
      setError(isVi ? 'Vui lòng nhập email.' : 'Please enter an email address.');
      return;
    }

    try {
      await securityPinService.createPinResetRequest(emailToUse);
      // Neutral response — always show success banner
      setRecoverySuccess(true);
      setError(null);
    } catch {
      setRecoverySuccess(true);
      setError(null);
    }
  };

  return (
    <div
      id="modal-security-pin-challenge"
      className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/70 backdrop-blur-xs animate-in fade-in duration-150 overflow-y-auto"
    >
      <div className="w-full max-w-md bg-white dark:bg-[#111827] rounded-3xl p-6 sm:p-7 border border-slate-200 dark:border-slate-800 shadow-2xl space-y-5 my-8 animate-in zoom-in-95 duration-150">
        {/* Header */}
        <div className="flex items-start justify-between pb-2 border-b border-slate-100 dark:border-slate-800">
          <div className="flex items-center gap-2.5">
            <div className="h-10 w-10 rounded-2xl bg-emerald-50 dark:bg-emerald-950/50 border border-emerald-200 dark:border-emerald-800 text-emerald-600 dark:text-emerald-400 flex items-center justify-center">
              <Shield className="h-5 w-5" />
            </div>
            <div>
              <span className="text-[10px] font-bold uppercase tracking-wider text-emerald-600 dark:text-emerald-400">
                {isVi ? 'Bảo mật giao dịch' : 'Security Verification'}
              </span>
              <h3 className="text-base font-bold text-slate-900 dark:text-white">
                {isRecoveryOpen
                  ? isVi
                    ? 'Khôi phục Mã bảo mật'
                    : 'Recover Security PIN'
                  : title || (isVi ? 'Xác thực Mã bảo mật (6 số)' : 'Security PIN Verification')}
              </h3>
            </div>
          </div>
          <button
            type="button"
            id="btn-cancel-pin-challenge-x"
            onClick={onCancel}
            className="p-1.5 rounded-xl text-slate-400 hover:text-slate-600 hover:bg-slate-100 dark:hover:bg-slate-800 transition-colors cursor-pointer"
          >
            <X className="h-5 w-5" />
          </button>
        </div>

        {/* RECOVERY SUB-FLOW */}
        {isRecoveryOpen ? (
          <div className="space-y-4">
            <p className="text-xs text-slate-600 dark:text-slate-400 leading-relaxed">
              {isVi
                ? 'Gửi yêu cầu thiết lập lại mã bảo mật. Mã bảo mật hiện tại vẫn có hiệu lực cho đến khi bạn thiết lập thành công mã bảo mật mới qua liên kết an toàn.'
                : 'Request a Security PIN reset. Your current PIN remains valid until you successfully set a new Security PIN via a secure link.'}
            </p>

            {error && (
              <div className="p-3 rounded-xl bg-rose-50 dark:bg-rose-950/40 border border-rose-200 dark:border-rose-900/60 text-xs text-rose-700 dark:text-rose-300 flex items-start gap-2">
                <AlertCircle className="h-4 w-4 shrink-0 mt-0.5" />
                <span>{error}</span>
              </div>
            )}

            {recoverySuccess ? (
              <div className="p-4 rounded-2xl bg-emerald-50 dark:bg-emerald-950/40 border border-emerald-200 dark:border-emerald-800 text-xs text-emerald-800 dark:text-emerald-300 space-y-2">
                <p className="font-semibold">
                  {isVi
                    ? 'Đã gửi liên kết đặt lại Mã bảo mật thành công!'
                    : 'Security PIN reset link sent successfully!'}
                </p>
                <p>
                  {isVi
                    ? 'Nếu thông tin khớp với tài khoản, liên kết đặt lại mã bảo mật đã được gửi đến email của bạn.'
                    : 'If the information matches an account, a Security PIN reset link has been sent to your email.'}
                </p>
                <div className="pt-2">
                  <button
                    type="button"
                    onClick={onCancel}
                    className="w-full py-2 px-3 rounded-xl bg-emerald-600 hover:bg-emerald-700 text-white font-medium text-xs transition-colors cursor-pointer"
                  >
                    {isVi ? 'Đóng và hủy thao tác' : 'Close and Cancel'}
                  </button>
                </div>
              </div>
            ) : (
              <form onSubmit={handleSendRecovery} className="space-y-3">
                <div>
                  <label className="block text-xs font-semibold text-slate-700 dark:text-slate-300 mb-1">
                    Email
                  </label>
                  <input
                    type="email"
                    value={recoveryEmail}
                    onChange={(e) => setRecoveryEmail(e.target.value)}
                    placeholder="user@example.com"
                    className="w-full px-3 py-2 rounded-xl border border-slate-300 dark:border-slate-700 bg-white dark:bg-slate-900 text-xs text-slate-900 dark:text-white focus:ring-2 focus:ring-emerald-500 focus:outline-hidden"
                    required
                  />
                </div>

                <div className="flex items-center gap-2 pt-1">
                  <button
                    type="button"
                    onClick={() => setIsRecoveryOpen(false)}
                    className="flex-1 py-2.5 px-3 rounded-xl border border-slate-200 dark:border-slate-700 text-xs font-semibold text-slate-600 dark:text-slate-400 hover:bg-slate-50 dark:hover:bg-slate-800 transition-colors cursor-pointer"
                  >
                    {isVi ? 'Quay lại' : 'Back'}
                  </button>
                  <button
                    type="submit"
                    className="flex-1 py-2.5 px-3 rounded-xl bg-emerald-600 hover:bg-emerald-700 text-white font-semibold text-xs transition-colors cursor-pointer"
                  >
                    {isVi ? 'Gửi liên kết' : 'Send Link'}
                  </button>
                </div>
              </form>
            )}
          </div>
        ) : isServiceUnavailable ? (
          /* BACKEND UNAVAILABLE */
          <div className="space-y-4">
            <div className="p-3.5 rounded-xl bg-rose-50 dark:bg-rose-950/40 border border-rose-200 dark:border-rose-900/60 text-xs text-rose-700 dark:text-rose-300 flex items-start gap-2.5">
              <AlertCircle className="h-5 w-5 shrink-0 text-rose-600 dark:text-rose-400 mt-0.5" />
              <div>
                <p className="font-bold">
                  {isVi ? 'Máy chủ không khả dụng' : 'Service Unavailable'}
                </p>
                <p className="mt-1 leading-relaxed text-[11px]">
                  {isVi
                    ? 'Không thể kết nối đến máy chủ xác thực mã bảo mật. Vui lòng kiểm tra kết nối và thử lại.'
                    : 'Unable to connect to the security PIN service. Please check your connection and try again.'}
                </p>
              </div>
            </div>

            <div className="space-y-2 pt-2">
              <button
                type="button"
                onClick={() => {
                  setIsServiceUnavailable(false);
                  securityPinService
                    .getStatus()
                    .then((res) => setIsPinSet(res.isSet))
                    .catch(() => setIsServiceUnavailable(true));
                }}
                className="w-full py-2.5 px-4 rounded-xl bg-emerald-600 hover:bg-emerald-700 text-white font-semibold text-xs transition-colors cursor-pointer"
              >
                {isVi ? 'Thử lại' : 'Retry'}
              </button>
              <button
                type="button"
                onClick={onCancel}
                className="w-full py-2.5 px-4 rounded-xl border border-slate-200 dark:border-slate-700 text-slate-700 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800 text-xs font-semibold transition-colors cursor-pointer"
              >
                {isVi ? 'Hủy bỏ' : 'Cancel'}
              </button>
            </div>
          </div>
        ) : isPinSet === false ? (
          /* PIN NOT SET - TRUTHFUL RECOVERY PATH */
          <div className="space-y-4">
            <div className="p-3.5 rounded-xl bg-amber-50 dark:bg-amber-950/40 border border-amber-200 dark:border-amber-800 text-xs text-amber-800 dark:text-amber-300 flex items-start gap-2.5">
              <AlertCircle className="h-5 w-5 shrink-0 text-amber-600 dark:text-amber-400 mt-0.5" />
              <div>
                <p className="font-bold">
                  {isVi ? 'Chưa thiết lập Mã bảo mật' : 'Security PIN Not Configured'}
                </p>
                <p className="mt-1 leading-relaxed text-[11px]">
                  {isVi
                    ? 'Tài khoản của bạn chưa hoàn tất thiết lập Mã bảo mật 6 số. Hãy sử dụng chức năng khôi phục hoặc đăng nhập lại để thiết lập mã bảo mật.'
                    : 'Your account does not have a 6-digit Security PIN configured. Please use PIN recovery or log in again to set up your PIN.'}
                </p>
              </div>
            </div>

            <div className="space-y-2 pt-2">
              <button
                type="button"
                onClick={() => setIsRecoveryOpen(true)}
                className="w-full py-2.5 px-4 rounded-xl bg-emerald-600 hover:bg-emerald-700 text-white font-semibold text-xs transition-colors cursor-pointer"
              >
                {isVi ? 'Thiết lập qua liên kết email' : 'Set up via Email Link'}
              </button>
              <button
                type="button"
                onClick={onCancel}
                className="w-full py-2.5 px-4 rounded-xl border border-slate-200 dark:border-slate-700 text-slate-700 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800 text-xs font-semibold transition-colors cursor-pointer"
              >
                {isVi ? 'Hủy bỏ' : 'Cancel'}
              </button>
            </div>
          </div>
        ) : (
          /* NORMAL PIN VERIFY FLOW */
          <div className="space-y-4">
            <p className="text-xs text-slate-600 dark:text-slate-400 leading-relaxed text-center">
              {description ||
                (isVi
                  ? 'Nhập Mã bảo mật 6 số để xác nhận và thực hiện giao dịch.'
                  : 'Enter your 6-digit Security PIN to authorize this transaction.')}
            </p>

            {amount !== undefined && amount > 0 && (
              <div className="p-3 rounded-2xl bg-slate-50 dark:bg-slate-800/60 border border-slate-200/80 dark:border-slate-700 text-center">
                <span className="text-[11px] text-slate-500 dark:text-slate-400">
                  {isVi ? 'Số tiền giao dịch:' : 'Transaction Amount:'}
                </span>
                <div className="text-lg font-bold text-slate-900 dark:text-white tabular-nums">
                  {amount.toLocaleString('vi-VN')} ₫
                </div>
              </div>
            )}

            {error && (
              <div
                id="pin-challenge-error"
                className="p-3 rounded-xl bg-rose-50 dark:bg-rose-950/40 border border-rose-200 dark:border-rose-900/60 text-xs text-rose-700 dark:text-rose-300 flex items-start gap-2"
              >
                <AlertCircle className="h-4 w-4 shrink-0 mt-0.5" />
                <span>{error}</span>
              </div>
            )}

            {/* Six-Digit Pin Slot Input with natural Bank/Wallet UX */}
            <div className="py-2 space-y-3">
              <SixDigitPinInput
                id="input-challenge-pin"
                value={pin}
                onChange={(val) => {
                  setPin(val);
                  if (error) setError(null);
                }}
                onComplete={executeVerify}
                mask={mask}
                showToggle
                onToggleMask={() => setMask(!mask)}
                disabled={isVerifying}
                error={Boolean(error)}
                autoFocus
              />
              <div className="flex items-center justify-between text-[11px] text-slate-500 dark:text-slate-400 px-1">
                <span>{isVi ? 'Tự động xác thực khi nhập số thứ 6' : 'Verifies automatically on 6th digit'}</span>
                <button
                  type="button"
                  onClick={() => setIsRecoveryOpen(true)}
                  className="text-emerald-600 dark:text-emerald-400 font-medium hover:underline cursor-pointer"
                >
                  {isVi ? 'Quên mã bảo mật?' : 'Forgot PIN?'}
                </button>
              </div>
            </div>

            {isVerifying && (
              <div className="flex items-center justify-center gap-2 py-1 text-xs text-emerald-600 dark:text-emerald-400 font-medium">
                <Loader2 className="h-4 w-4 animate-spin" />
                <span>{isVi ? 'Đang xác thực...' : 'Verifying...'}</span>
              </div>
            )}

            {/* Action Buttons: Cancel is always available */}
            <div className="pt-2">
              <button
                type="button"
                id="btn-cancel-pin-challenge"
                onClick={onCancel}
                className="w-full py-2.5 px-4 rounded-xl border border-slate-200 dark:border-slate-700 text-slate-700 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800 text-xs font-semibold transition-colors cursor-pointer"
              >
                {isVi ? 'Hủy bỏ' : 'Cancel'}
              </button>
            </div>
          </div>
        )}
      </div>
    </div>
  );
};
