import React, { useState, useEffect } from 'react';
import { useSearchParams, useNavigate, Link } from 'react-router-dom';
import {
  ShieldCheck,
  CheckCircle2,
  AlertCircle,
  Clock,
  ShieldX,
  ArrowRight,
  ArrowLeft,
  Loader2,
  Moon,
  Sun,
  Globe,
  Laptop,
} from 'lucide-react';
import { cn } from '../../utils/cn';
import { SpendWiseLogo } from '../../components/brand/SpendWiseLogo';
import { SixDigitPinInput } from '../../components/security/SixDigitPinInput';
import { securityPinService, PinResetTokenStatus, isValidSecurityPin } from '../../services/securityPinService';
import { useI18n } from '../../i18n/I18nContext';
import { useTheme } from '../../providers/ThemeProvider';
import { User } from '../../types';

export const ResetPinPage: React.FC = () => {
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const { locale, setLocale } = useI18n();
  const { theme, setTheme, resolvedTheme } = useTheme();

  // In-memory token storage (Token Security: scrubbed from address bar immediately)
  const [token, setToken] = useState<string>(() => searchParams.get('token') || '');

  const [status, setStatus] = useState<PinResetTokenStatus>('validating');

  // Two-step PIN setup state
  const [pinStep, setPinStep] = useState<1 | 2>(1);
  const [newPin, setNewPin] = useState('');
  const [confirmPin, setConfirmPin] = useState('');
  const [showPin, setShowPin] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);

  const isVi = locale === 'vi-VN';

  // Token Security: Read token into state and scrub query parameter from URL
  useEffect(() => {
    const urlToken = searchParams.get('token');
    if (urlToken) {
      setToken(urlToken);
      // Clean query parameter from address bar without reloading
      const cleanUrl = window.location.pathname;
      window.history.replaceState({}, document.title, cleanUrl);
    }
  }, [searchParams]);

  // Validate token on mount or token change
  useEffect(() => {
    if (!token) {
      // Do not overwrite to invalid if the reset has already completed successfully
      setStatus((prev) => (prev === 'reset-success' ? 'reset-success' : 'invalid'));
      return;
    }

    let isMounted = true;
    securityPinService.validatePinResetToken(token).then((validation) => {
      if (isMounted) {
        setStatus(validation.status);
      }
    });

    return () => {
      isMounted = false;
    };
  }, [token]);

  const handleStep1Complete = (pin: string) => {
    if (!isValidSecurityPin(pin)) return;
    setNewPin(pin);
    setSubmitError(null);
    setPinStep(2);
    setConfirmPin('');
  };

  const handleStep2Complete = async (confirmedValue: string) => {
    if (isSubmitting || !token) return;

    if (confirmedValue !== newPin) {
      setSubmitError(
        isVi
          ? 'Mã bảo mật xác nhận không khớp. Vui lòng thử lại.'
          : 'Confirmation PIN does not match. Please try again.'
      );
      setConfirmPin('');
      return;
    }

    setIsSubmitting(true);
    setSubmitError(null);

    try {
      const result = await securityPinService.completePinReset(token, confirmedValue);
      if (result.success) {
        setStatus('reset-success');
        // Clear sensitive PINs and token immediately upon success
        setToken('');
        setNewPin('');
        setConfirmPin('');
      } else {
        if (result.error === 'EXPIRED_TOKEN') {
          setStatus('expired');
        } else if (result.error === 'ALREADY_USED') {
          setStatus('already-used');
        } else if (result.error === 'INVALID_PIN') {
          setSubmitError(
            isVi
              ? 'Mã bảo mật phải bao gồm đúng 6 chữ số.'
              : 'Security PIN must be exactly 6 numeric digits.'
          );
          setConfirmPin('');
        } else {
          setSubmitError(
            isVi
              ? 'Không thể hoàn tất đặt lại mã bảo mật. Vui lòng thử lại.'
              : 'Failed to complete PIN reset. Please try again.'
          );
          setConfirmPin('');
        }
      }
    } catch {
      setSubmitError(
        isVi
          ? 'Đã xảy ra lỗi không mong muốn. Vui lòng thử lại sau.'
          : 'An unexpected error occurred. Please try again later.'
      );
      setConfirmPin('');
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="min-h-screen flex flex-col bg-slate-50 dark:bg-[#0b0f17] text-slate-800 dark:text-slate-100 transition-colors duration-200">
      {/* Top Navigation Bar */}
      <header className="w-full border-b border-slate-200/80 dark:border-slate-800/80 bg-white/70 dark:bg-[#0f141c]/70 backdrop-blur-md sticky top-0 z-20">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 h-16 flex items-center justify-between">
          <div className="flex items-center gap-3">
            <SpendWiseLogo />
          </div>

          <div className="flex items-center gap-2">
            {/* Language Switcher */}
            <button
              type="button"
              id="btn-switch-lang"
              onClick={() => setLocale(isVi ? 'en-US' : 'vi-VN')}
              className="px-2.5 py-1.5 rounded-lg border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 text-xs font-medium flex items-center gap-1.5 hover:bg-slate-50 dark:hover:bg-slate-700/50 transition-colors cursor-pointer"
            >
              <Globe className="h-3.5 w-3.5 text-slate-500" />
              <span>{isVi ? 'Tiếng Việt' : 'English'}</span>
            </button>

            {/* Theme Mode Selector (Light, Dark, System) */}
            <div id="reset-pin-theme-selector" className="flex items-center p-0.5 rounded-lg border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 shadow-xs">
              <button
                type="button"
                id="btn-pin-theme-light"
                onClick={() => setTheme('light')}
                className={cn(
                  'flex h-6 w-6 items-center justify-center rounded transition-all cursor-pointer',
                  theme === 'light'
                    ? 'bg-slate-100 dark:bg-slate-700 text-amber-500 shadow-xs font-bold'
                    : 'text-slate-400 hover:text-slate-700 dark:hover:text-slate-200'
                )}
                title={isVi ? 'Chế độ sáng' : 'Light mode'}
                aria-label={isVi ? 'Chế độ sáng' : 'Light mode'}
              >
                <Sun className="h-3 w-3" />
              </button>
              <button
                type="button"
                id="btn-pin-theme-dark"
                onClick={() => setTheme('dark')}
                className={cn(
                  'flex h-6 w-6 items-center justify-center rounded transition-all cursor-pointer',
                  theme === 'dark'
                    ? 'bg-slate-100 dark:bg-slate-700 text-sky-400 shadow-xs font-bold'
                    : 'text-slate-400 hover:text-slate-700 dark:hover:text-slate-200'
                )}
                title={isVi ? 'Chế độ tối' : 'Dark mode'}
                aria-label={isVi ? 'Chế độ tối' : 'Dark mode'}
              >
                <Moon className="h-3 w-3" />
              </button>
              <button
                type="button"
                id="btn-pin-theme-system"
                onClick={() => setTheme('system')}
                className={cn(
                  'flex h-6 w-6 items-center justify-center rounded transition-all cursor-pointer',
                  theme === 'system'
                    ? 'bg-slate-100 dark:bg-slate-700 text-emerald-500 shadow-xs font-bold'
                    : 'text-slate-400 hover:text-slate-700 dark:hover:text-slate-200'
                )}
                title={isVi ? 'Theo hệ thống' : 'System mode'}
                aria-label={isVi ? 'Theo hệ thống' : 'System mode'}
              >
                <Laptop className="h-3 w-3" />
              </button>
            </div>
          </div>
        </div>
      </header>

      {/* Main Content Area */}
      <main className="flex-1 flex items-center justify-center p-4 sm:p-6 md:p-8">
        <div className="w-full max-w-md">
          {/* Card Container */}
          <div className="bg-white dark:bg-[#111827] rounded-3xl border border-slate-200/80 dark:border-slate-800 shadow-xl overflow-hidden p-6 sm:p-8">
            {/* STATE 1: VALIDATING */}
            {status === 'validating' && (
              <div id="state-validating" className="py-12 flex flex-col items-center text-center space-y-4">
                <Loader2 className="h-10 w-10 text-emerald-600 animate-spin" />
                <div className="space-y-1">
                  <h2 className="text-lg font-bold text-slate-900 dark:text-white">
                    {isVi ? 'Đang xác thực liên kết...' : 'Validating reset link...'}
                  </h2>
                  <p className="text-xs text-slate-500 dark:text-slate-400">
                    {isVi ? 'Vui lòng chờ trong giây lát' : 'Please hold on for a moment'}
                  </p>
                </div>
              </div>
            )}

            {/* STATE 2: EXPIRED TOKEN */}
            {status === 'expired' && (
              <div id="state-expired" className="py-6 flex flex-col items-center text-center space-y-4">
                <div className="h-14 w-14 rounded-2xl bg-amber-50 dark:bg-amber-950/40 border border-amber-200 dark:border-amber-800/60 flex items-center justify-center text-amber-600 dark:text-amber-400">
                  <Clock className="h-7 w-7" />
                </div>
                <div className="space-y-1.5">
                  <h2 className="text-xl font-bold text-slate-900 dark:text-white">
                    {isVi ? 'Liên kết đã hết hạn' : 'Link Expired'}
                  </h2>
                  <p className="text-xs text-slate-600 dark:text-slate-400 max-w-xs leading-relaxed">
                    {isVi
                      ? 'Liên kết đặt lại mã bảo mật này đã quá thời hạn 15 phút. Vui lòng yêu cầu một liên kết mới.'
                      : 'This PIN reset link has expired after 15 minutes. Please request a new one.'}
                  </p>
                </div>
                <div className="pt-3 w-full">
                  <Link
                    to="/login"
                    className="w-full inline-flex items-center justify-center gap-2 py-3 px-4 rounded-xl bg-emerald-600 hover:bg-emerald-700 text-white font-semibold text-sm shadow-md transition-all active:scale-98"
                  >
                    <span>{isVi ? 'Quay lại Đăng nhập' : 'Back to Sign In'}</span>
                    <ArrowRight className="h-4 w-4" />
                  </Link>
                </div>
              </div>
            )}

            {/* STATE 3: ALREADY USED */}
            {status === 'already-used' && (
              <div id="state-already-used" className="py-6 flex flex-col items-center text-center space-y-4">
                <div className="h-14 w-14 rounded-2xl bg-slate-100 dark:bg-slate-800 border border-slate-200 dark:border-slate-700 flex items-center justify-center text-slate-500 dark:text-slate-400">
                  <ShieldCheck className="h-7 w-7" />
                </div>
                <div className="space-y-1.5">
                  <h2 className="text-xl font-bold text-slate-900 dark:text-white">
                    {isVi ? 'Liên kết đã được sử dụng' : 'Link Already Used'}
                  </h2>
                  <p className="text-xs text-slate-600 dark:text-slate-400 max-w-xs leading-relaxed">
                    {isVi
                      ? 'Liên kết này chỉ có thể sử dụng một lần và đã hoàn tất việc đặt lại mã bảo mật.'
                      : 'This one-time reset link has already been used.'}
                  </p>
                </div>
                <div className="pt-3 w-full">
                  <Link
                    to="/login"
                    className="w-full inline-flex items-center justify-center gap-2 py-3 px-4 rounded-xl bg-emerald-600 hover:bg-emerald-700 text-white font-semibold text-sm shadow-md transition-all active:scale-98"
                  >
                    <span>{isVi ? 'Đăng nhập ngay' : 'Sign In Now'}</span>
                    <ArrowRight className="h-4 w-4" />
                  </Link>
                </div>
              </div>
            )}

            {/* STATE 4: INVALID TOKEN */}
            {status === 'invalid' && (
              <div id="state-invalid" className="py-6 flex flex-col items-center text-center space-y-4">
                <div className="h-14 w-14 rounded-2xl bg-rose-50 dark:bg-rose-950/40 border border-rose-200 dark:border-rose-800/60 flex items-center justify-center text-rose-600 dark:text-rose-400">
                  <ShieldX className="h-7 w-7" />
                </div>
                <div className="space-y-1.5">
                  <h2 className="text-xl font-bold text-slate-900 dark:text-white">
                    {isVi ? 'Liên kết không hợp lệ' : 'Invalid Reset Link'}
                  </h2>
                  <p className="text-xs text-slate-600 dark:text-slate-400 max-w-xs leading-relaxed">
                    {isVi
                      ? 'Mã bảo mật trong liên kết không tồn tại hoặc đã bị vô hiệu hóa bởi hệ thống.'
                      : 'The security reset token is invalid or has been invalidated.'}
                  </p>
                </div>
                <div className="pt-3 w-full">
                  <Link
                    to="/login"
                    className="w-full inline-flex items-center justify-center gap-2 py-3 px-4 rounded-xl bg-slate-900 hover:bg-slate-800 text-white font-semibold text-sm shadow-md transition-all active:scale-98"
                  >
                    <span>{isVi ? 'Về trang Đăng nhập' : 'Go to Sign In'}</span>
                    <ArrowRight className="h-4 w-4" />
                  </Link>
                </div>
              </div>
            )}

            {/* STATE 4.5: NETWORK ERROR */}
            {status === 'network-error' && (
              <div id="state-network-error" className="py-6 flex flex-col items-center text-center space-y-4">
                <div className="h-14 w-14 rounded-2xl bg-amber-50 dark:bg-amber-950/40 border border-amber-200 dark:border-amber-800/60 flex items-center justify-center text-amber-600 dark:text-amber-400">
                  <AlertCircle className="h-7 w-7" />
                </div>
                <div className="space-y-1.5">
                  <h2 className="text-xl font-bold text-slate-900 dark:text-white">
                    {isVi ? 'Lỗi kết nối máy chủ' : 'Server Connection Error'}
                  </h2>
                  <p className="text-xs text-slate-600 dark:text-slate-400 max-w-xs leading-relaxed">
                    {isVi
                      ? 'Không thể kiểm tra liên kết đặt lại mã bảo mật. Vui lòng kiểm tra mạng và thử lại.'
                      : 'Could not verify the PIN reset link. Please check your network and try again.'}
                  </p>
                </div>
                <div className="pt-3 w-full flex flex-col gap-2">
                  <button
                    type="button"
                    onClick={() => {
                      if (token) {
                        setStatus('validating');
                        securityPinService.validatePinResetToken(token).then((res) => setStatus(res.status));
                      }
                    }}
                    className="w-full inline-flex items-center justify-center gap-2 py-3 px-4 rounded-xl bg-emerald-600 hover:bg-emerald-700 text-white font-semibold text-sm shadow-md transition-all active:scale-98 cursor-pointer"
                  >
                    <span>{isVi ? 'Thử lại' : 'Retry'}</span>
                  </button>
                  <Link
                    to="/login"
                    className="w-full inline-flex items-center justify-center py-2 text-xs text-slate-500 hover:text-slate-800 dark:hover:text-slate-200 transition-colors"
                  >
                    <span>{isVi ? 'Quay lại Đăng nhập' : 'Back to Login'}</span>
                  </Link>
                </div>
              </div>
            )}

            {/* STATE 5: RESET SUCCESS */}
            {status === 'reset-success' && (
              <div id="state-reset-success" className="py-6 flex flex-col items-center text-center space-y-5 animate-in fade-in-50 duration-200">
                <div className="h-14 w-14 rounded-2xl bg-emerald-50 dark:bg-emerald-950/40 border border-emerald-200 dark:border-emerald-800/60 flex items-center justify-center text-emerald-600 dark:text-emerald-400">
                  <CheckCircle2 className="h-7 w-7" />
                </div>
                <div className="space-y-2">
                  <h2 className="text-xl font-bold text-slate-900 dark:text-white">
                    {isVi ? 'Đặt lại mã bảo mật thành công!' : 'Security PIN Reset Successfully!'}
                  </h2>
                  <p className="text-xs text-slate-600 dark:text-slate-400 max-w-xs leading-relaxed">
                    {isVi
                      ? 'Mã bảo mật 6 số mới cho tài khoản của bạn đã được kích hoạt và mã hóa an toàn.'
                      : 'The new 6-digit Security PIN for your account is now active and securely encrypted.'}
                  </p>
                </div>
                <div className="pt-2 w-full">
                  <Link
                    id="btn-login-after-reset-pin"
                    to="/login"
                    className="w-full inline-flex items-center justify-center gap-2 py-3 px-4 rounded-xl bg-emerald-600 hover:bg-emerald-700 text-white font-semibold text-sm shadow-md transition-all active:scale-98"
                  >
                    <span>{isVi ? 'Đăng nhập ngay' : 'Sign In Now'}</span>
                    <ArrowRight className="h-4 w-4" />
                  </Link>
                </div>
              </div>
            )}

            {/* STATE 6: FORM (TOKEN VALID) */}
            {status === 'valid' && (
              <div id="state-form" className="space-y-6 animate-in fade-in-50 duration-150">
                {/* Header */}
                <div className="space-y-2 text-center">
                  <div className="inline-flex h-12 w-12 items-center justify-center rounded-2xl bg-emerald-50 dark:bg-emerald-950/50 border border-emerald-200 dark:border-emerald-800 text-emerald-600 dark:text-emerald-400 mb-1">
                    <ShieldCheck className="h-6 w-6" />
                  </div>
                  <h1 className="text-xl font-bold text-slate-900 dark:text-white">
                    {pinStep === 1
                      ? isVi
                        ? 'Nhập Mã bảo mật mới (6 số)'
                        : 'Enter New Security PIN (6 digits)'
                      : isVi
                      ? 'Xác nhận Mã bảo mật mới'
                      : 'Confirm New Security PIN'}
                  </h1>
                  <p className="text-xs text-slate-500 dark:text-slate-400">
                    {isVi ? 'Thiết lập Mã bảo mật 6 chữ số mới cho tài khoản của bạn' : 'Set up a new 6-digit Security PIN for your account'}
                  </p>
                </div>

                {/* Stepper indicator */}
                <div className="flex items-center justify-center gap-2">
                  <span
                    className={`h-1.5 rounded-full transition-all duration-200 ${
                      pinStep === 1 ? 'w-8 bg-emerald-500' : 'w-2 bg-emerald-500/40'
                    }`}
                  />
                  <span
                    className={`h-1.5 rounded-full transition-all duration-200 ${
                      pinStep === 2 ? 'w-8 bg-emerald-500' : 'w-2 bg-slate-200 dark:bg-slate-700'
                    }`}
                  />
                </div>

                {submitError && (
                  <div className="p-3 rounded-xl bg-rose-50 dark:bg-rose-950/40 border border-rose-200 dark:border-rose-900/60 text-xs text-rose-700 dark:text-rose-300 flex items-start gap-2">
                    <AlertCircle className="h-4 w-4 shrink-0 mt-0.5" />
                    <span>{submitError}</span>
                  </div>
                )}

                {pinStep === 1 ? (
                  /* Step 1: Enter New PIN */
                  <div className="space-y-4 py-2">
                    <SixDigitPinInput
                      id="input-reset-pin-step1"
                      value={newPin}
                      onChange={(val) => {
                        setNewPin(val);
                        if (submitError) setSubmitError(null);
                      }}
                      onComplete={handleStep1Complete}
                      disabled={isSubmitting}
                      error={Boolean(submitError)}
                      showToggle
                      mask={!showPin}
                      onToggleMask={() => setShowPin(!showPin)}
                      autoFocus
                    />
                    <p className="text-[11px] text-slate-500 dark:text-slate-400 text-center">
                      {isVi ? 'Tự động chuyển bước khi nhập đủ 6 số' : 'Advances automatically on 6th digit'}
                    </p>
                  </div>
                ) : (
                  /* Step 2: Confirm New PIN */
                  <div className="space-y-4 py-2">
                    <SixDigitPinInput
                      id="input-reset-pin-step2"
                      value={confirmPin}
                      onChange={(val) => {
                        setConfirmPin(val);
                        if (submitError) setSubmitError(null);
                      }}
                      onComplete={handleStep2Complete}
                      disabled={isSubmitting}
                      error={Boolean(submitError)}
                      showToggle
                      mask={!showPin}
                      onToggleMask={() => setShowPin(!showPin)}
                      autoFocus
                    />

                    <div className="text-center text-[11px]">
                      {isSubmitting ? (
                        <span className="inline-flex items-center gap-1.5 text-emerald-600 dark:text-emerald-400 font-medium">
                          <Loader2 className="w-3.5 h-3.5 animate-spin" />
                          {isVi ? 'Đang lưu mã bảo mật...' : 'Saving Security PIN...'}
                        </span>
                      ) : (
                        <span className="text-slate-500 dark:text-slate-400">
                          {isVi ? 'Tự động kích hoạt khi nhập đủ 6 số xác nhận' : 'Activates automatically on 6th digit'}
                        </span>
                      )}
                    </div>

                    <div className="flex justify-center pt-1">
                      <button
                        type="button"
                        onClick={() => {
                          setPinStep(1);
                          setConfirmPin('');
                          setSubmitError(null);
                        }}
                        disabled={isSubmitting}
                        className="inline-flex items-center gap-1 text-xs text-emerald-600 dark:text-emerald-400 hover:underline cursor-pointer font-medium"
                      >
                        <ArrowLeft className="w-3 h-3" />
                        <span>{isVi ? 'Nhập lại mã mới' : 'Change new PIN'}</span>
                      </button>
                    </div>
                  </div>
                )}

                <div className="pt-2 text-center border-t border-slate-100 dark:border-slate-800">
                  <Link
                    to="/login"
                    className="text-xs text-slate-500 hover:text-slate-800 dark:hover:text-slate-200 transition-colors"
                  >
                    {isVi ? 'Hủy và quay lại Đăng nhập' : 'Cancel and return to Sign In'}
                  </Link>
                </div>
              </div>
            )}
          </div>
        </div>
      </main>
    </div>
  );
};
