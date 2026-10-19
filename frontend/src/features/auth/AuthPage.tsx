import React, { useState, useEffect, useRef, useMemo } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import {
  Mail,
  Lock,
  Eye,
  EyeOff,
  Phone,
  CheckCircle2,
  AlertCircle,
  Shield,
  Loader2,
  Moon,
  Sun,
  Globe,
  Laptop,
  X,
  Check,
  ArrowLeft,
  KeyRound,
} from 'lucide-react';
import { Spinner } from '../../components/ui/Spinner';
import { SpendWiseSLoading } from '../../components/ui/SpendWiseSLoading';
import { useAuth } from '../../providers/AuthProvider';
import { useI18n } from '../../i18n/I18nContext';
import { useTheme } from '../../providers/ThemeProvider';
import { SpendWiseLogo } from '../../components/brand/SpendWiseLogo';
import { securityPinService, isValidSecurityPin } from '../../services/securityPinService';
import { SixDigitPinInput } from '../../components/security/SixDigitPinInput';
import { authApiService } from '../../services/authApiService';
import { cn } from '../../utils/cn';
import { resolveAdminReturnPath } from '../../app/router/adminWebRouting';
import { ZxcvbnFactory } from '@zxcvbn-ts/core';
import { dictionary as commonDictionary, adjacencyGraphs } from '@zxcvbn-ts/language-common';
import { dictionary as englishDictionary, translations as englishTranslations } from '@zxcvbn-ts/language-en';

// Advisory password estimate; required Backend password policy is evaluated separately.
const passwordEstimator = new ZxcvbnFactory({
  dictionary: { ...commonDictionary, ...englishDictionary },
  graphs: adjacencyGraphs,
  translations: englishTranslations,
});

interface AuthPageProps {
  initialMode?: 'login' | 'register';
}

// Legacy wrappers may pass initialMode; Admin Web always renders Login.
export const AuthPage: React.FC<AuthPageProps> = () => {
  const {
    initiateLogin,
    completePinVerify,
    completePinSetup,
    isAuthenticated,
    currentUser,
    isLoading: authLoading,
  } = useAuth();
  const { t, locale, setLocale } = useI18n();
  const { theme, setTheme, resolvedTheme } = useTheme();
  const navigate = useNavigate();
  const location = useLocation();

  // Form input refs for reliable Enter-key navigation
  const loginIdInputRef = useRef<HTMLInputElement>(null);
  const loginPasswordInputRef = useRef<HTMLInputElement>(null);
  const pinInputRef = useRef<HTMLInputElement>(null);
  const confirmPinInputRef = useRef<HTMLInputElement>(null);
  const otpInputRef = useRef<HTMLInputElement>(null);

  // Login Form States
  const [loginId, setLoginId] = useState('');
  const [loginPassword, setLoginPassword] = useState('');
  const [rememberMe, setRememberMe] = useState(true);
  const [showLoginPassword, setShowLoginPassword] = useState(false);
  const [isLoginSubmitting, setIsLoginSubmitting] = useState(false);
  const [loginLoadingStatus, setLoginLoadingStatus] = useState<'pending' | 'success' | 'error' | 'offline' | 'timeout'>('pending');
  const [loginError, setLoginError] = useState<string | null>(null);

  // Two-factor Security PIN Login States
  const [loginStage, setLoginStage] = useState<'credentials' | 'pin_verify' | 'pin_setup'>('credentials');
  const [pinSetupStep, setPinSetupStep] = useState<1 | 2>(1);
  const [enteredPin, setEnteredPin] = useState('');
  const [confirmSetupPin, setConfirmSetupPin] = useState('');
  const [showEnteredPin, setShowEnteredPin] = useState(false);
  const [pinError, setPinError] = useState<string | null>(null);
  const [isPinSubmitting, setIsPinSubmitting] = useState(false);

  // Forgot PIN in login modal states
  const [showForgotPinModal, setShowForgotPinModal] = useState(false);
  const [forgotPinEmail, setForgotPinEmail] = useState('');
  const [forgotPinError, setForgotPinError] = useState<string | null>(null);
  const [isForgotPinSubmitting, setIsForgotPinSubmitting] = useState(false);
  const [forgotPinSubmitted, setForgotPinSubmitted] = useState(false);

  // Forgot Password / Recovery States (W02-A Contract Baseline)
  const [isRecoveryMode, setIsRecoveryMode] = useState(false);
  const [recoveryStep, setRecoveryStep] = useState<'email' | 'otp' | 'new_password' | 'success'>('email');
  const [recoveryEmail, setRecoveryEmail] = useState('');
  const [recoveryOtp, setRecoveryOtp] = useState('');
  const [recoveryNewPassword, setRecoveryNewPassword] = useState('');
  const [recoveryConfirmPassword, setRecoveryConfirmPassword] = useState('');
  const [showRecoveryNewPassword, setShowRecoveryNewPassword] = useState(false);
  const [showRecoveryConfirmPassword, setShowRecoveryConfirmPassword] = useState(false);
  const [recoveryError, setRecoveryError] = useState<string | null>(null);
  const [isRecoverySubmitting, setIsRecoverySubmitting] = useState(false);

  // Admin reset password policy: apply this version only once Codex confirms 8–128.
  // Strength is advisory; a weak score never silently overrides the Backend policy.
  const isRecoveryPasswordPolicyValid =
    recoveryNewPassword.length >= 8 &&
    recoveryNewPassword.length <= 128 &&
    /[A-Z]/.test(recoveryNewPassword) &&
    /[a-z]/.test(recoveryNewPassword) &&
    /[0-9]/.test(recoveryNewPassword);
  const isRecoveryConfirmationValid =
    isRecoveryPasswordPolicyValid &&
    recoveryConfirmPassword.length > 0 &&
    recoveryConfirmPassword === recoveryNewPassword;
  const recoveryStrengthScore = useMemo(
    () => (recoveryNewPassword ? passwordEstimator.check(recoveryNewPassword).score : null),
    [recoveryNewPassword]
  );
  const recoveryStrengthLevel = recoveryStrengthScore === null
    ? 0
    : recoveryStrengthScore <= 1 ? 1 : recoveryStrengthScore;
  const recoveryStrengthLabel = recoveryStrengthLevel === 1
    ? (locale === 'vi-VN' ? 'Yếu' : 'Weak')
    : recoveryStrengthLevel === 2
      ? (locale === 'vi-VN' ? 'Trung bình' : 'Fair')
      : recoveryStrengthLevel === 3
        ? (locale === 'vi-VN' ? 'Mạnh' : 'Strong')
        : recoveryStrengthLevel === 4
          ? (locale === 'vi-VN' ? 'Rất mạnh' : 'Very strong')
          : (locale === 'vi-VN' ? 'Chưa đánh giá' : 'Not rated');


  // Redirect if already authenticated
  useEffect(() => {
    if (isAuthenticated && !authLoading) {
      navigate(resolveAdminReturnPath(currentUser?.role, (location.state as any)?.from), { replace: true });
    }
  }, [isAuthenticated, authLoading, currentUser, navigate, location]);

  // Preserve email prefill and support ?action=forgot from reset-password redirect
  useEffect(() => {
    const params = new URLSearchParams(location.search);
    const email = params.get('email');
    if (email) {
      setLoginId(email);
      setRecoveryEmail(email);
    }
    const action = params.get('action');
    if (action === 'forgot') {
      setIsRecoveryMode(true);
      setRecoveryStep('email');
    }
  }, [location.search]);

  const handleLoginSubmit = async (
    e?: React.FormEvent | React.KeyboardEvent,
    overrideCredentials?: { loginId: string; password: string }
  ) => {
    if (e && typeof e.preventDefault === 'function') {
      e.preventDefault();
    }
    if (isLoginSubmitting) return;
    setLoginError(null);

    const targetLogin = overrideCredentials ? overrideCredentials.loginId : loginId;
    const targetPassword = overrideCredentials ? overrideCredentials.password : loginPassword;

    const cleanLogin = targetLogin.trim();
    if (!cleanLogin) {
      setLoginError(
        locale === 'vi-VN'
          ? 'Vui lòng nhập Email hoặc số điện thoại.'
          : 'Please enter your Email or phone number.'
      );
      loginIdInputRef.current?.focus();
      return;
    }

    if (!targetPassword) {
      setLoginError(
        locale === 'vi-VN' ? 'Vui lòng nhập mật khẩu.' : 'Please enter your password.'
      );
      loginPasswordInputRef.current?.focus();
      return;
    }

    setIsLoginSubmitting(true);
    setLoginLoadingStatus('pending');
    try {
      const res = await initiateLogin(cleanLogin, targetPassword, rememberMe ? 'browser' : undefined);

      if (res.success) {
        setLoginLoadingStatus('success');
        setTimeout(() => {
          setIsLoginSubmitting(false);
          setEnteredPin('');
          setConfirmSetupPin('');
          setPinError(null);
          setPinSetupStep(1);
          if (res.status === 'PIN_SETUP_REQUIRED') {
            setLoginStage('pin_setup');
          } else {
            setLoginStage('pin_verify');
          }
          setTimeout(() => {
            pinInputRef.current?.focus();
          }, 120);
        }, 500);
      } else {
        if (typeof navigator !== 'undefined' && !navigator.onLine) {
          setLoginLoadingStatus('offline');
        } else if (res.error === 'TIMEOUT' || res.error === 'network_timeout') {
          setLoginLoadingStatus('timeout');
        } else {
          setLoginLoadingStatus('error');
        }

        if (res.error === 'ACCOUNT_RESTRICTED') {
          setLoginError(t('auth.accountRestricted'));
        } else if (res.error === 'SERVER_ERROR') {
          setLoginError(t('auth.serverError'));
        } else {
          setLoginError(t('auth.invalidCredentials'));
        }

        setTimeout(() => {
          setIsLoginSubmitting(false);
        }, 900);
      }
    } catch (err: any) {
      console.error('Login error', err);
      if (typeof navigator !== 'undefined' && !navigator.onLine) {
        setLoginLoadingStatus('offline');
        setLoginError(locale === 'vi-VN' ? 'Không thể kết nối. Kiểm tra mạng rồi thử lại.' : 'No internet connection. Please check your network.');
      } else {
        setLoginLoadingStatus('error');
        setLoginError(t('auth.serverError'));
      }
      setTimeout(() => {
        setIsLoginSubmitting(false);
      }, 900);
    }
  };

  const isPinVerifyingRef = useRef(false);

  const executePinVerify = async (candidatePin: string) => {
    if (isPinSubmitting || isPinVerifyingRef.current) return;

    if (!isValidSecurityPin(candidatePin)) {
      setPinError(
        locale === 'vi-VN'
          ? 'Mã bảo mật phải bao gồm đúng 6 chữ số.'
          : 'Security PIN must be exactly 6 digits.'
      );
      return;
    }

    isPinVerifyingRef.current = true;
    setIsPinSubmitting(true);
    setPinError(null);

    try {
      const res = await completePinVerify(candidatePin);
      if (res.success) {
        navigate(resolveAdminReturnPath(res.user?.role, (location.state as any)?.from), { replace: true });
      } else {
        setPinError(
          locale === 'vi-VN'
            ? 'Mã bảo mật không chính xác. Vui lòng thử lại.'
            : 'Incorrect Security PIN. Please try again.'
        );
        setEnteredPin('');
      }
    } catch {
      setPinError(
        locale === 'vi-VN'
          ? 'Đã xảy ra lỗi khi xác thực mã bảo mật.'
          : 'An error occurred while verifying Security PIN.'
      );
      setEnteredPin('');
    } finally {
      setIsPinSubmitting(false);
      isPinVerifyingRef.current = false;
    }
  };

  const handlePinVerifySubmit = (e?: React.FormEvent | React.KeyboardEvent) => {
    if (e && typeof e.preventDefault === 'function') {
      e.preventDefault();
    }
    executePinVerify(enteredPin);
  };

  const handlePinSetupStep1Complete = (step1Pin: string) => {
    if (!isValidSecurityPin(step1Pin)) return;
    setEnteredPin(step1Pin);
    setPinError(null);
    setPinSetupStep(2);
    setConfirmSetupPin('');
  };

  const executePinSetupStep2 = async (step2Pin: string) => {
    if (isPinSubmitting) return;

    if (step2Pin !== enteredPin) {
      setPinError(
        locale === 'vi-VN'
          ? 'Xác nhận mã bảo mật không khớp. Vui lòng xác nhận lại.'
          : 'Security PIN confirmation does not match. Please try again.'
      );
      setConfirmSetupPin('');
      return;
    }

    setIsPinSubmitting(true);
    setPinError(null);

    try {
      const res = await completePinSetup(enteredPin, step2Pin);
      if (res.success) {
        navigate(resolveAdminReturnPath(res.user?.role, (location.state as any)?.from), { replace: true });
      } else {
        setPinError(
          locale === 'vi-VN'
            ? 'Không thể lưu Mã bảo mật. Vui lòng thử lại.'
            : 'Failed to save Security PIN. Please try again.'
        );
        setConfirmSetupPin('');
      }
    } catch {
      setPinError(
        locale === 'vi-VN'
          ? 'Đã xảy ra lỗi khi thiết lập mã bảo mật.'
          : 'An error occurred while setting up Security PIN.'
      );
      setConfirmSetupPin('');
    } finally {
      setIsPinSubmitting(false);
    }
  };

  const handlePinSetupSubmit = (e?: React.FormEvent | React.KeyboardEvent) => {
    if (e && typeof e.preventDefault === 'function') {
      e.preventDefault();
    }
    if (pinSetupStep === 1) {
      handlePinSetupStep1Complete(enteredPin);
    } else {
      executePinSetupStep2(confirmSetupPin);
    }
  };

  const handleCancelPinStage = () => {
    setLoginStage('credentials');
    setPinSetupStep(1);
    setEnteredPin('');
    setConfirmSetupPin('');
    setPinError(null);
    setTimeout(() => {
      loginPasswordInputRef.current?.focus();
    }, 100);
  };

  const handleOpenForgotPinModal = () => {
    setForgotPinEmail(loginId || '');
    setForgotPinError(null);
    setForgotPinSubmitted(false);
    setShowForgotPinModal(true);
  };

  const handleCloseForgotPinModal = () => {
    setShowForgotPinModal(false);
    setForgotPinSubmitted(false);
    setForgotPinError(null);
  };

  const handleForgotPinSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    const emailToUse = forgotPinEmail.trim();
    if (!emailToUse) {
      setForgotPinError(locale === 'vi-VN' ? 'Vui lòng nhập email.' : 'Please enter an email.');
      return;
    }

    setIsForgotPinSubmitting(true);
    setForgotPinError(null);

    try {
      await securityPinService.createPinResetRequest(emailToUse);
      setForgotPinSubmitted(true);
    } catch (err: any) {
      setForgotPinError(
        err?.message ||
        (locale === 'vi-VN'
          ? 'Không thể gửi yêu cầu đặt lại mã bảo mật. Vui lòng thử lại sau.'
          : 'Unable to send Security PIN reset request. Please try again later.')
      );
    } finally {
      setIsForgotPinSubmitting(false);
    }
  };

  // Recovery Handlers (W02-A Contract Baseline)
  const handleOpenRecovery = () => {
    setIsRecoveryMode(true);
    setRecoveryStep('email');
    setRecoveryEmail(loginId || '');
    setRecoveryOtp('');
    setRecoveryNewPassword('');
    setRecoveryConfirmPassword('');
    setRecoveryError(null);
  };

  const handleCancelRecovery = () => {
    setIsRecoveryMode(false);
    setRecoveryStep('email');
    setRecoveryOtp('');
    setRecoveryNewPassword('');
    setRecoveryConfirmPassword('');
    setRecoveryError(null);
  };

  const handleRecoveryEmailSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (isRecoverySubmitting) return;

    const trimmed = recoveryEmail.trim();
    if (!trimmed) {
      setRecoveryError(
        locale === 'vi-VN' ? 'Vui lòng nhập địa chỉ email.' : 'Please enter your email address.'
      );
      return;
    }
    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    if (!emailRegex.test(trimmed)) {
      setRecoveryError(
        locale === 'vi-VN' ? 'Địa chỉ email không đúng định dạng.' : 'Invalid email address format.'
      );
      return;
    }

    setIsRecoverySubmitting(true);
    setRecoveryError(null);

    try {
      await authApiService.forgotPassword({ identifier: trimmed });
      // HTTP 202 Accepted: Move to OTP step unconditionally (prevent account enumeration)
      setRecoveryStep('otp');
    } catch {
      // Even if network or request throws, if backend accepts or rejects neutrally
      // Only network errors show connection failure
      setRecoveryError(
        locale === 'vi-VN'
          ? 'Không thể kết nối đến máy chủ. Vui lòng kiểm tra mạng và thử lại.'
          : 'Could not connect to the server. Please check your connection and try again.'
      );
    } finally {
      setIsRecoverySubmitting(false);
    }
  };

  const handleRecoveryOtpContinue = (e: React.FormEvent) => {
    e.preventDefault();
    const cleanOtp = recoveryOtp.trim();
    if (cleanOtp.length !== 8 || !/^\d{8}$/.test(cleanOtp)) {
      setRecoveryError(
        locale === 'vi-VN'
          ? 'Mã OTP phải bao gồm đúng 8 chữ số.'
          : 'OTP must be exactly 8 digits.'
      );
      return;
    }
    // CONTRACT RULE: No OTP Verify API exists. Format valid only. Advance to new password.
    setRecoveryError(null);
    setRecoveryStep('new_password');
  };

  const handleRecoveryResetSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (isRecoverySubmitting) return;

    // Validate Password Policy (8-128 chars, 1 uppercase, 1 lowercase, 1 digit)
    if (recoveryNewPassword.length < 8 || recoveryNewPassword.length > 128) {
      setRecoveryError(
        locale === 'vi-VN'
          ? 'Mật khẩu mới phải có độ dài từ 8 đến 128 ký tự.'
          : 'New password must be between 8 and 128 characters.'
      );
      return;
    }
    if (!/[A-Z]/.test(recoveryNewPassword)) {
      setRecoveryError(
        locale === 'vi-VN'
          ? 'Mật khẩu mới phải chứa ít nhất một chữ cái in hoa.'
          : 'New password must contain at least one uppercase letter.'
      );
      return;
    }
    if (!/[a-z]/.test(recoveryNewPassword)) {
      setRecoveryError(
        locale === 'vi-VN'
          ? 'Mật khẩu mới phải chứa ít nhất một chữ cái thường.'
          : 'New password must contain at least one lowercase letter.'
      );
      return;
    }
    if (!/[0-9]/.test(recoveryNewPassword)) {
      setRecoveryError(
        locale === 'vi-VN'
          ? 'Mật khẩu mới phải chứa ít nhất một chữ số.'
          : 'New password must contain at least one digit.'
      );
      return;
    }
    if (recoveryNewPassword !== recoveryConfirmPassword) {
      setRecoveryError(
        locale === 'vi-VN'
          ? 'Mật khẩu xác nhận không khớp. Vui lòng kiểm tra lại.'
          : 'Password confirmation does not match. Please verify.'
      );
      return;
    }

    setIsRecoverySubmitting(true);
    setRecoveryError(null);

    try {
      await authApiService.resetPassword({
        identifier: recoveryEmail.trim(),
        otp: recoveryOtp.trim(),
        newPassword: recoveryNewPassword,
      });
      // HTTP 204: Only now show success screen! Clear secrets from state
      setRecoveryOtp('');
      setRecoveryNewPassword('');
      setRecoveryConfirmPassword('');
      setRecoveryStep('success');
    } catch (err: any) {
      const msg = err?.message || '';
      if (msg.includes('weak_password')) {
        setRecoveryError(
          locale === 'vi-VN'
            ? 'Mật khẩu chưa đáp ứng tiêu chuẩn an toàn (8-128 ký tự, chữ hoa, chữ thường, chữ số).'
            : 'Password does not meet security requirements (8-128 characters, upper, lower, digit).'
        );
      } else {
        setRecoveryError(
          locale === 'vi-VN'
            ? 'Mã OTP không chính xác hoặc đã hết hạn. Vui lòng kiểm tra lại.'
            : 'Invalid or expired OTP code. Please check and try again.'
        );
      }
    } finally {
      setIsRecoverySubmitting(false);
    }
  };

  const handleLoginKeyDown = (e: React.KeyboardEvent<HTMLElement>) => {
    if (e.key === 'Enter') {
      const target = e.target as HTMLElement;
      if (target.tagName === 'BUTTON' && (target as HTMLButtonElement).type === 'button') {
        return;
      }
      e.preventDefault();

      // If typing in loginId and password is not yet entered, focus password field
      if (target === loginIdInputRef.current && !loginPassword) {
        loginPasswordInputRef.current?.focus();
        return;
      }

      handleLoginSubmit(e);
    }
  };

  return (
    <div className="desk-tabletop-container min-h-screen w-full flex flex-col items-center justify-center p-3 sm:p-6 text-slate-800 dark:text-[#E5E7EB] antialiased font-sans relative overflow-x-hidden selection:bg-emerald-500 selection:text-white transition-colors duration-300">
      {/* Desk Lamp Warm Ambient Lighting & Desk Edge Vignette */}
      <div className="desk-lamp-glow-overlay" aria-hidden="true" />
      <div className="desk-wood-vignette" aria-hidden="true" />

      {/* Top Header Bar */}
      <div className="w-full max-w-[940px] flex items-center justify-between py-2 mb-3 z-10 px-2 sm:px-4">
        {/* Brand Logo */}
        <div className="flex items-center gap-3">
          <SpendWiseLogo size={38} variant="badge" className="shadow-md shadow-blue-950/40 shrink-0" />
          <div className="flex flex-col">
            <div className="flex items-center gap-1 leading-none">
              <span className="text-base font-bold text-stone-900 dark:text-white tracking-tight">Spend</span>
              <span className="text-base font-black text-emerald-700 dark:text-emerald-400 tracking-tight">Wise</span>
            </div>
            <span className="text-[10px] font-bold tracking-wider text-stone-600 dark:text-stone-400 uppercase mt-0.5">
              FINANCE PLATFORM
            </span>
          </div>
        </div>

        {/* Top Controls: Pill rounded with subtle border & glow */}
        <div className="flex items-center gap-2">
          {/* Language Switcher */}
          <button
            type="button"
            onClick={() => setLocale(locale === 'vi-VN' ? 'en-US' : 'vi-VN')}
            className="flex items-center gap-1.5 h-8 px-3 rounded-full bg-stone-50/90 dark:bg-[#151B22]/90 hover:bg-white dark:hover:bg-[#1C2430] border border-stone-300/80 dark:border-white/10 hover:border-emerald-500/50 text-stone-800 dark:text-stone-200 font-semibold text-xs transition-all duration-200 cursor-pointer shadow-xs hover:shadow-[0_0_12px_rgba(16,185,129,0.2)]"
            title={locale === 'vi-VN' ? 'Switch to English' : 'Chuyển sang Tiếng Việt'}
          >
            <Globe className="h-3.5 w-3.5 text-emerald-700 dark:text-emerald-400" />
            <span className="uppercase tracking-wider font-bold">
              {locale === 'vi-VN' ? 'VI' : 'EN'}
            </span>
          </button>

          {/* Theme Mode Selector (Light, Dark, System) */}
          <div id="auth-theme-selector" className="flex items-center p-0.5 rounded-full bg-stone-50/90 dark:bg-[#151B22]/90 border border-stone-300/80 dark:border-white/10 shadow-xs">
            <button
              type="button"
              id="auth-btn-theme-light"
              onClick={() => setTheme('light')}
              className={cn(
                'h-7 w-7 rounded-full flex items-center justify-center transition-all cursor-pointer',
                theme === 'light'
                  ? 'bg-white dark:bg-[#1C2430] text-amber-500 shadow-xs font-bold'
                  : 'text-stone-400 hover:text-stone-700 dark:hover:text-stone-200'
              )}
              title={locale === 'vi-VN' ? 'Chế độ sáng' : 'Light mode'}
              aria-label={locale === 'vi-VN' ? 'Chế độ sáng' : 'Light mode'}
            >
              <Sun className="h-3.5 w-3.5" />
            </button>
            <button
              type="button"
              id="auth-btn-theme-dark"
              onClick={() => setTheme('dark')}
              className={cn(
                'h-7 w-7 rounded-full flex items-center justify-center transition-all cursor-pointer',
                theme === 'dark'
                  ? 'bg-white dark:bg-[#1C2430] text-sky-400 shadow-xs font-bold'
                  : 'text-stone-400 hover:text-stone-700 dark:hover:text-stone-200'
              )}
              title={locale === 'vi-VN' ? 'Chế độ tối' : 'Dark mode'}
              aria-label={locale === 'vi-VN' ? 'Chế độ tối' : 'Dark mode'}
            >
              <Moon className="h-3.5 w-3.5" />
            </button>
            <button
              type="button"
              id="auth-btn-theme-system"
              onClick={() => setTheme('system')}
              className={cn(
                'h-7 w-7 rounded-full flex items-center justify-center transition-all cursor-pointer',
                theme === 'system'
                  ? 'bg-white dark:bg-[#1C2430] text-emerald-500 shadow-xs font-bold'
                  : 'text-stone-400 hover:text-stone-700 dark:hover:text-stone-200'
              )}
              title={locale === 'vi-VN' ? 'Theo hệ thống' : 'System mode'}
              aria-label={locale === 'vi-VN' ? 'Theo hệ thống' : 'System mode'}
            >
              <Laptop className="h-3.5 w-3.5" />
            </button>
          </div>
        </div>
      </div>

      {/* ================= MAIN ANIMATED CONTAINER (3D NOTEBOOK STAGE) ================= */}
      <div className="auth-book-wrapper">
        {/* Executive Leather Desk Blotter Mat resting on the wood table */}
        <div className="desk-leather-mat" aria-hidden="true" />

        {/* Notebook Leather Outer Cover */}
        <div className="notebook-leather-cover" aria-hidden="true">
          <div className="notebook-corner top-left" />
          <div className="notebook-corner top-right" />
          <div className="notebook-corner bottom-left" />
          <div className="notebook-corner bottom-right" />
        </div>

        {/* Notebook Pen Loop & Stylus on Right Rim */}
        <div className="notebook-pen-loop" aria-hidden="true">
          <div className="notebook-pen">
            <div className="notebook-pen-cap" />
            <div className="notebook-pen-clip" />
            <div className="notebook-pen-body" />
          </div>
        </div>

        {/* ================= DESK STATIONERY PENS (3 BÚT TO ĐẶT NGOÀI CUỐN SỔ) ================= */}
        {/* 1. Cây bút chì gỗ to (Big Hexagonal Cedar Wood Pencil) - Bên trái cuốn sổ */}
        <div
          className="desk-stationery-item desk-pen-pos-pencil hidden lg:block"
          title={locale === 'vi-VN' ? 'Bút chì gỗ tuyết tùng 2B' : 'Cedar Wood Drafting Pencil 2B'}
        >
          <div className="desk-big-pencil desk-pen-shadow">
            <div className="desk-pencil-lead" />
            <div className="desk-pencil-wood" />
            <div className="desk-pencil-shaft">
              <div className="desk-pencil-facet-left" />
              <div className="desk-pencil-facet-center" />
              <div className="desk-pencil-facet-right" />
            </div>
            <div className="desk-pencil-ferrule" />
            <div className="desk-pencil-eraser" />
          </div>
        </div>

        {/* 2. Cây bút máy to sang trọng (Big Luxury Gold Fountain Pen) - Bên phải phía trên */}
        <div
          className="desk-stationery-item desk-pen-pos-fountain hidden lg:block"
          title={locale === 'vi-VN' ? 'Bút máy cao cấp mạ vàng' : 'Luxury Gold Fountain Pen'}
        >
          <div className="desk-big-fountain-pen desk-pen-shadow">
            <div className="desk-fountain-nib" />
            <div className="desk-fountain-grip" />
            <div className="desk-fountain-band" />
            <div className="desk-fountain-barrel" />
            <div className="desk-fountain-cap">
              <div className="desk-fountain-clip" />
            </div>
          </div>
        </div>

        {/* 3. Cây bút điện tử to thông minh (Big Modern Digital Stylus / Smart Pen) - Bên phải phía dưới */}
        <div
          className="desk-stationery-item desk-pen-pos-stylus hidden lg:block"
          title={locale === 'vi-VN' ? 'Bút cảm ứng điện tử thông minh' : 'Smart Digital Stylus Pen'}
        >
          <div className="desk-big-digital-stylus desk-pen-shadow">
            <div className="desk-stylus-nib" />
            <div className="desk-stylus-cone" />
            <div className="desk-stylus-body">
              <div className="desk-stylus-flat-facet" />
            </div>
            <div className="desk-stylus-led-ring" />
            <div className="desk-stylus-cap" />
          </div>
        </div>

        <div className="auth-container">
          {/* Dây ruy-băng lụa đánh dấu trang sổ (Kẹp tại trang Đăng nhập) */}
          <div className="notebook-ribbon-bookmark" aria-hidden="true" />

          {/* Notebook Center Ring Binder (Còng khuyên lò xo kim loại mạ đồng) */}
          <div className="notebook-spine-rings" aria-hidden="true">
            {[0, 1, 2, 3, 4, 5, 6, 7].map((idx) => (
              <div key={idx} className="notebook-ring-unit" style={{ top: `${7 + idx * 11.8}%` }}>
                <div className="notebook-hole left" />
                <div className="notebook-ring-loop">
                  <div className="notebook-ring-sheen" />
                </div>
                <div className="notebook-hole right" />
              </div>
            ))}
          </div>
        
        {/* ================= 1. LOGIN FORM BOX ================= */}
        <div className="auth-form-box login">
          {isRecoveryMode ? (
            <div className="w-full space-y-4 max-w-[370px]">
              {/* Back to Login link */}
              {recoveryStep !== 'success' && (
                <button
                  type="button"
                  id="btn-recovery-back-to-login"
                  onClick={handleCancelRecovery}
                  disabled={isRecoverySubmitting}
                  className="inline-flex items-center gap-1.5 text-xs text-slate-500 hover:text-slate-800 dark:text-slate-400 dark:hover:text-slate-200 font-medium cursor-pointer transition-colors"
                >
                  <ArrowLeft className="h-3.5 w-3.5" />
                  <span>{locale === 'vi-VN' ? 'Quay lại đăng nhập' : 'Back to Login'}</span>
                </button>
              )}

              {/* Recovery Error Banner */}
              {recoveryError && (
                <div className="flex items-start gap-2.5 p-3 rounded-[10px] bg-rose-50 dark:bg-rose-950/60 border border-rose-300 dark:border-rose-500/50 text-xs text-rose-800 dark:text-rose-300 shadow-xs">
                  <AlertCircle className="h-4 w-4 text-rose-600 dark:text-rose-400 shrink-0 mt-0.5" />
                  <div className="flex-1 font-medium">{recoveryError}</div>
                </div>
              )}

              {/* SCREEN 1 — ENTER EMAIL */}
              {recoveryStep === 'email' && (
                <form onSubmit={handleRecoveryEmailSubmit} className="space-y-4">
                  <div className="text-center mb-1">
                    <div className="inline-flex items-center justify-center w-12 h-12 rounded-2xl bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 mb-2 border border-emerald-500/20">
                      <KeyRound className="w-6 h-6" />
                    </div>
                    <h1 className="text-2xl font-extrabold text-slate-900 dark:text-[#E5E7EB] tracking-tight font-display">
                      {locale === 'vi-VN' ? 'Quên mật khẩu' : 'Forgot Password'}
                    </h1>
                    <p className="text-xs text-slate-500 dark:text-[#9CA3AF] mt-1 font-normal leading-relaxed">
                      {locale === 'vi-VN'
                        ? 'Nhập địa chỉ email tài khoản Quản trị viên để nhận mã xác thực đặt lại mật khẩu.'
                        : 'Enter your Admin account email address to receive a password reset verification code.'}
                    </p>
                  </div>

                  <div className="space-y-1">
                    <label className="block text-[11px] font-semibold text-slate-700 dark:text-slate-300 uppercase tracking-wider">
                      {locale === 'vi-VN' ? 'Địa chỉ Email' : 'Email Address'}
                    </label>
                    <div className="relative group">
                      <input
                        id="input-recovery-email"
                        type="email"
                        value={recoveryEmail}
                        onChange={(e) => {
                          setRecoveryEmail(e.target.value);
                          if (recoveryError) setRecoveryError(null);
                        }}
                        placeholder="admin@example.com"
                        disabled={isRecoverySubmitting}
                        required
                        autoFocus
                        className="w-full h-10.5 pl-10 pr-4 rounded-[10px] bg-white dark:bg-[#151B22] border border-slate-200 dark:border-white/10 hover:border-slate-300 dark:hover:border-white/20 focus:border-emerald-500 focus:ring-2 focus:ring-emerald-500/20 text-sm text-slate-900 dark:text-[#E5E7EB] placeholder:text-slate-400 dark:placeholder:text-[#6B7280] focus:outline-none transition-all font-medium shadow-xs"
                      />
                      <Mail className="absolute left-3.5 top-1/2 -translate-y-1/2 h-4 w-4 text-slate-400 dark:text-[#6B7280] group-focus-within:text-emerald-600 dark:group-focus-within:text-emerald-400 transition-colors" />
                    </div>
                  </div>

                  <button
                    type="submit"
                    id="btn-recovery-email-submit"
                    disabled={isRecoverySubmitting}
                    className="w-full h-11 rounded-[10px] bg-gradient-to-r from-emerald-600 via-emerald-500 to-emerald-600 hover:brightness-110 active:scale-[0.99] text-white font-bold text-sm flex items-center justify-center gap-2 shadow-lg shadow-emerald-950/50 hover:shadow-emerald-500/25 border border-emerald-400/20 transition-all duration-200 cursor-pointer disabled:opacity-60 disabled:cursor-not-allowed"
                  >
                    <span>
                      {isRecoverySubmitting
                        ? locale === 'vi-VN' ? 'Đang xử lý...' : 'Processing...'
                        : locale === 'vi-VN' ? 'Gửi mã xác thực' : 'Send Verification Code'}
                    </span>
                  </button>
                </form>
              )}

              {/* SCREEN 2 — ENTER OTP */}
              {recoveryStep === 'otp' && (
                <form onSubmit={handleRecoveryOtpContinue} className="space-y-4">
                  <div className="text-center mb-1">
                    <div className="inline-flex items-center justify-center w-12 h-12 rounded-2xl bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 mb-2 border border-emerald-500/20">
                      <Shield className="w-6 h-6" />
                    </div>
                    <h1 className="text-2xl font-extrabold text-slate-900 dark:text-[#E5E7EB] tracking-tight font-display">
                      {locale === 'vi-VN' ? 'Nhập mã OTP' : 'Enter OTP Code'}
                    </h1>
                    {/* Neutral confirmation text (Strict Contract Baseline) */}
                    <p className="text-xs text-slate-500 dark:text-[#9CA3AF] mt-1 font-normal leading-relaxed">
                      {locale === 'vi-VN'
                        ? 'Nếu email có thể sử dụng để khôi phục tài khoản, hướng dẫn đặt lại mật khẩu sẽ được gửi đến email đó.'
                        : 'If an account matches the information provided, password reset instructions will be sent to the registered email.'}
                    </p>
                  </div>

                  {/* Masked Email Chip */}
                  <div className="p-2.5 rounded-xl bg-slate-50 dark:bg-[#151B22] border border-slate-200 dark:border-white/10 flex items-center justify-between text-xs">
                    <span className="text-slate-500 dark:text-slate-400">
                      {locale === 'vi-VN' ? 'Email nhận mã:' : 'Target Email:'}
                    </span>
                    <span className="font-semibold text-slate-800 dark:text-slate-200 font-mono">
                      {recoveryEmail.replace(/^(.)(.*)(@.*)$/, (_, a, b, c) => `${a}***${c}`)}
                    </span>
                  </div>

                  {/* 8-Digit OTP Input (Strictly Numeric, Sequential Left-to-Right, Paste Support) */}
                  <div className="space-y-2">
                    <label className="block text-[11px] font-semibold text-slate-700 dark:text-slate-300 uppercase tracking-wider text-center">
                      {locale === 'vi-VN' ? 'Mã OTP (8 chữ số)' : 'OTP Code (8 digits)'}
                    </label>

                    <div
                      onClick={() => {
                        if (otpInputRef.current) {
                          otpInputRef.current.focus();
                          const len = otpInputRef.current.value.length;
                          otpInputRef.current.setSelectionRange(len, len);
                        }
                      }}
                      className="relative flex items-center justify-center gap-1.5 sm:gap-2 cursor-pointer select-none py-1"
                    >
                      {/* Underlying hidden real input */}
                      <input
                        ref={otpInputRef}
                        id="input-recovery-otp"
                        type="text"
                        inputMode="numeric"
                        pattern="[0-9]*"
                        maxLength={8}
                        autoComplete="one-time-code"
                        value={recoveryOtp}
                        onChange={(e) => {
                          const numeric = e.target.value.replace(/\D/g, '').slice(0, 8);
                          setRecoveryOtp(numeric);
                          if (recoveryError) setRecoveryError(null);
                        }}
                        onSelect={() => {
                          if (otpInputRef.current) {
                            const len = otpInputRef.current.value.length;
                            otpInputRef.current.setSelectionRange(len, len);
                          }
                        }}
                        onClick={() => {
                          if (otpInputRef.current) {
                            const len = otpInputRef.current.value.length;
                            otpInputRef.current.setSelectionRange(len, len);
                          }
                        }}
                        onFocus={() => {
                          if (otpInputRef.current) {
                            const len = otpInputRef.current.value.length;
                            otpInputRef.current.setSelectionRange(len, len);
                          }
                        }}
                        onPaste={(e) => {
                          e.preventDefault();
                          const pasted = e.clipboardData.getData('text').replace(/\D/g, '').slice(0, 8);
                          setRecoveryOtp(pasted);
                          if (recoveryError) setRecoveryError(null);
                        }}
                        autoFocus
                        className="absolute inset-0 w-full h-full opacity-0 cursor-pointer pointer-events-auto"
                        aria-label="OTP 8 digits"
                      />

                      {/* 8 Visual Digit Slots */}
                      {Array.from({ length: 8 }).map((_, index) => {
                        const char = recoveryOtp[index] || '';
                        const isFilled = Boolean(char);
                        const isCurrentActive = index === recoveryOtp.length && recoveryOtp.length < 8;

                        return (
                          <div
                            key={index}
                            className={cn(
                              'w-8.5 h-11 sm:w-9.5 sm:h-12 rounded-lg flex items-center justify-center text-lg font-bold font-mono transition-all duration-150',
                              'border-2 bg-white dark:bg-[#151B22] shadow-2xs',
                              recoveryError
                                ? 'border-rose-400 dark:border-rose-500 bg-rose-50/40 dark:bg-rose-950/20 text-rose-600 dark:text-rose-400 ring-2 ring-rose-400/20'
                                : isCurrentActive
                                ? 'border-emerald-500 dark:border-emerald-400 ring-2 ring-emerald-500/20 dark:ring-emerald-400/20'
                                : isFilled
                                ? 'border-slate-400 dark:border-slate-600 text-slate-900 dark:text-white'
                                : 'border-slate-200 dark:border-white/10 text-slate-400 dark:text-slate-600'
                            )}
                          >
                            {isFilled ? (
                              char
                            ) : isCurrentActive ? (
                              <span className="w-0.5 h-5 bg-emerald-500 animate-pulse" />
                            ) : (
                              <span className="w-1.5 h-1.5 rounded-full bg-slate-200 dark:bg-slate-700" />
                            )}
                          </div>
                        );
                      })}
                    </div>

                    <p className="text-[11px] text-slate-400 dark:text-slate-500 text-center">
                      {locale === 'vi-VN' ? 'Nhập đúng 8 chữ số nhận từ hòm thư Gmail' : 'Enter 8 digits received via Gmail'}
                    </p>
                  </div>

                  <button
                    type="submit"
                    id="btn-recovery-otp-continue"
                    disabled={recoveryOtp.length !== 8}
                    className="w-full h-11 rounded-[10px] bg-gradient-to-r from-emerald-600 via-emerald-500 to-emerald-600 hover:brightness-110 active:scale-[0.99] text-white font-bold text-sm flex items-center justify-center gap-2 shadow-lg shadow-emerald-950/50 hover:shadow-emerald-500/25 border border-emerald-400/20 transition-all duration-200 cursor-pointer disabled:opacity-50 disabled:cursor-not-allowed"
                  >
                    <span>{locale === 'vi-VN' ? 'Tiếp tục' : 'Continue'}</span>
                  </button>
                </form>
              )}

              {/* SCREEN 3 — NEW PASSWORD */}
              {recoveryStep === 'new_password' && (
                <form onSubmit={handleRecoveryResetSubmit} className="space-y-3.5">
                  <div className="text-center mb-1">
                    <div className="inline-flex items-center justify-center w-12 h-12 rounded-2xl bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 mb-2 border border-emerald-500/20">
                      <Lock className="w-6 h-6" />
                    </div>
                    <h1 className="text-2xl font-extrabold text-slate-900 dark:text-[#E5E7EB] tracking-tight font-display">
                      {locale === 'vi-VN' ? 'Mật khẩu mới' : 'New Password'}
                    </h1>
                    <p className="text-xs text-slate-500 dark:text-[#9CA3AF] mt-0.5 font-normal">
                      {locale === 'vi-VN'
                        ? 'Thiết lập mật khẩu quản trị viên mới đáp ứng chính sách bảo mật.'
                        : 'Set a new Admin password satisfying security policy.'}
                    </p>
                  </div>

                  {/* New Password Input */}
                  <div className="space-y-1">
                    <label className="block text-[11px] font-semibold text-slate-700 dark:text-slate-300 uppercase tracking-wider">
                      {locale === 'vi-VN' ? 'Mật khẩu mới' : 'New Password'}
                    </label>
                    <div className="relative group">
                      <input
                        id="input-recovery-new-password"
                        aria-label={locale === 'vi-VN' ? 'Mật khẩu mới' : 'New password'}
                        aria-describedby="recovery-password-strength recovery-password-advice recovery-password-policy"
                        autoComplete="new-password"
                        maxLength={128}
                        type={showRecoveryNewPassword ? 'text' : 'password'}
                        value={recoveryNewPassword}
                        onChange={(e) => {
                          setRecoveryNewPassword(e.target.value);
                          if (recoveryError) setRecoveryError(null);
                        }}
                        disabled={isRecoverySubmitting}
                        placeholder="••••••••••"
                        autoFocus
                        className="w-full h-10.5 pl-10 pr-10 rounded-[10px] bg-white dark:bg-[#151B22] border border-slate-200 dark:border-white/10 hover:border-slate-300 dark:hover:border-white/20 focus:border-emerald-500 focus:ring-2 focus:ring-emerald-500/20 text-sm text-slate-900 dark:text-[#E5E7EB] placeholder:text-slate-400 focus:outline-none transition-all font-medium tracking-wide shadow-xs"
                      />
                      <Lock className="absolute left-3.5 top-1/2 -translate-y-1/2 h-4 w-4 text-slate-400 dark:text-[#6B7280] group-focus-within:text-emerald-600 dark:group-focus-within:text-emerald-400 transition-colors" />
                      <button
                        type="button"
                        onClick={() => setShowRecoveryNewPassword(!showRecoveryNewPassword)}
                        className="absolute right-3 top-1/2 -translate-y-1/2 p-1 text-slate-400 hover:text-slate-700 dark:hover:text-slate-200 cursor-pointer"
                      >
                        {showRecoveryNewPassword ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
                      </button>
                    </div>
                    {/* zxcvbn analyzes predictable patterns and common passwords (advisory only). */}
                    <div className="pt-2 space-y-1.5" id="recovery-password-strength">
                      <div className="flex items-center justify-between gap-2 text-[11px]">
                        <span className="font-semibold text-slate-600 dark:text-slate-400">
                          {locale === 'vi-VN' ? 'Độ mạnh mật khẩu' : 'Password strength'}
                        </span>
                        <span className="font-semibold text-slate-700 dark:text-slate-300" aria-live="polite">
                          {recoveryStrengthLabel}
                        </span>
                      </div>
                      <div
                        className="grid grid-cols-4 gap-1.5"
                        role="meter"
                        aria-label={locale === 'vi-VN' ? 'Mức độ mạnh của mật khẩu' : 'Password strength level'}
                        aria-valuemin={0}
                        aria-valuemax={4}
                        aria-valuenow={recoveryStrengthLevel}
                        aria-valuetext={recoveryStrengthLabel}
                      >
                        {[1, 2, 3, 4].map((segment) => (
                          <span
                            key={segment}
                            aria-hidden="true"
                            className={cn(
                              'h-1.5 rounded-full transition-colors duration-150',
                              recoveryStrengthLevel < segment
                                ? 'bg-slate-200 dark:bg-slate-700'
                                : recoveryStrengthLevel === 1
                                  ? 'bg-rose-500'
                                  : recoveryStrengthLevel === 2
                                    ? 'bg-amber-500'
                                    : recoveryStrengthLevel === 3
                                      ? 'bg-emerald-500'
                                      : 'bg-emerald-600'
                            )}
                          />
                        ))}
                      </div>
                      <p id="recovery-password-advice" className="text-[11px] leading-relaxed text-slate-500 dark:text-slate-400">
                        {locale === 'vi-VN'
                          ? 'Nên dùng từ 12 ký tự, tránh mật khẩu phổ biến, tên riêng hoặc chuỗi dễ đoán. Đây là gợi ý, không phải điều kiện bắt buộc.'
                          : 'Prefer 12+ characters and avoid common passwords, names, or predictable sequences. This is advice, not a requirement.'}
                      </p>
                    </div>
                  </div>

                  {/* Confirm Password Input */}
                  <div className="space-y-1">
                    <label className="block text-[11px] font-semibold text-slate-700 dark:text-slate-300 uppercase tracking-wider">
                      {locale === 'vi-VN' ? 'Xác nhận mật khẩu' : 'Confirm Password'}
                    </label>
                    <div className="relative group">
                      <input
                        id="input-recovery-confirm-password"
                        aria-label={locale === 'vi-VN' ? 'Xác nhận mật khẩu' : 'Confirm password'}
                        aria-invalid={recoveryConfirmPassword.length > 0 && !isRecoveryConfirmationValid}
                        aria-describedby="recovery-confirm-feedback"
                        autoComplete="new-password"
                        maxLength={128}
                        type={showRecoveryConfirmPassword ? 'text' : 'password'}
                        value={recoveryConfirmPassword}
                        onChange={(e) => {
                          setRecoveryConfirmPassword(e.target.value);
                          if (recoveryError) setRecoveryError(null);
                        }}
                        disabled={isRecoverySubmitting}
                        placeholder="••••••••••"
                        className="w-full h-10.5 pl-10 pr-10 rounded-[10px] bg-white dark:bg-[#151B22] border border-slate-200 dark:border-white/10 hover:border-slate-300 dark:hover:border-white/20 focus:border-emerald-500 focus:ring-2 focus:ring-emerald-500/20 text-sm text-slate-900 dark:text-[#E5E7EB] placeholder:text-slate-400 focus:outline-none transition-all font-medium tracking-wide shadow-xs"
                      />
                      <Lock className="absolute left-3.5 top-1/2 -translate-y-1/2 h-4 w-4 text-slate-400 dark:text-[#6B7280] group-focus-within:text-emerald-600 dark:group-focus-within:text-emerald-400 transition-colors" />
                      <button
                        type="button"
                        onClick={() => setShowRecoveryConfirmPassword(!showRecoveryConfirmPassword)}
                        className="absolute right-3 top-1/2 -translate-y-1/2 p-1 text-slate-400 hover:text-slate-700 dark:hover:text-slate-200 cursor-pointer"
                      >
                        {showRecoveryConfirmPassword ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
                      </button>
                      {isRecoveryConfirmationValid && (
                        <CheckCircle2
                          aria-label={locale === 'vi-VN' ? 'Mật khẩu xác nhận hợp lệ và trùng khớp' : 'Passwords match and meet requirements'}
                          role="img"
                          className="absolute right-10 top-1/2 -translate-y-1/2 h-4 w-4 text-emerald-600 dark:text-emerald-400"
                        />
                      )}
                    </div>
                    <p id="recovery-confirm-feedback" className="text-[11px] leading-relaxed" aria-live="polite">
                      {recoveryConfirmPassword.length > 0 && recoveryConfirmPassword !== recoveryNewPassword
                        ? <span className="text-rose-600 dark:text-rose-400">{locale === 'vi-VN' ? 'Mật khẩu xác nhận chưa khớp.' : 'Passwords do not match.'}</span>
                        : isRecoveryConfirmationValid
                          ? <span className="text-emerald-600 dark:text-emerald-400">{locale === 'vi-VN' ? 'Mật khẩu xác nhận đã khớp.' : 'Passwords match.'}</span>
                          : null}
                    </p>
                  </div>

                  {/* Password Requirements List matching Backend policy */}
                  <div id="recovery-password-policy" className="p-2.5 rounded-xl bg-slate-50 dark:bg-[#151B22] border border-slate-200 dark:border-white/10 space-y-1 text-[11px]">
                    <div className="flex items-center gap-1.5 text-slate-600 dark:text-slate-400">
                      <span className={cn('h-1.5 w-1.5 rounded-full', recoveryNewPassword.length >= 8 && recoveryNewPassword.length <= 128 ? 'bg-emerald-500' : 'bg-slate-300 dark:bg-slate-600')} />
                      <span>{locale === 'vi-VN' ? 'Độ dài từ 8 - 128 ký tự' : '8 - 128 characters length'}</span>
                    </div>
                    <div className="flex items-center gap-1.5 text-slate-600 dark:text-slate-400">
                      <span className={cn('h-1.5 w-1.5 rounded-full', /[A-Z]/.test(recoveryNewPassword) ? 'bg-emerald-500' : 'bg-slate-300 dark:bg-slate-600')} />
                      <span>{locale === 'vi-VN' ? 'Ít nhất một chữ cái viết hoa (A-Z)' : 'At least one uppercase letter (A-Z)'}</span>
                    </div>
                    <div className="flex items-center gap-1.5 text-slate-600 dark:text-slate-400">
                      <span className={cn('h-1.5 w-1.5 rounded-full', /[a-z]/.test(recoveryNewPassword) ? 'bg-emerald-500' : 'bg-slate-300 dark:bg-slate-600')} />
                      <span>{locale === 'vi-VN' ? 'Ít nhất một chữ cái thường (a-z)' : 'At least one lowercase letter (a-z)'}</span>
                    </div>
                    <div className="flex items-center gap-1.5 text-slate-600 dark:text-slate-400">
                      <span className={cn('h-1.5 w-1.5 rounded-full', /[0-9]/.test(recoveryNewPassword) ? 'bg-emerald-500' : 'bg-slate-300 dark:bg-slate-600')} />
                      <span>{locale === 'vi-VN' ? 'Ít nhất một chữ số (0-9)' : 'At least one digit (0-9)'}</span>
                    </div>
                  </div>

                  <button
                    type="submit"
                    id="btn-recovery-reset-submit"
                    disabled={isRecoverySubmitting || !isRecoveryConfirmationValid || !/^\d{8}$/.test(recoveryOtp)}
                    className="w-full h-11 rounded-[10px] bg-gradient-to-r from-emerald-600 via-emerald-500 to-emerald-600 hover:brightness-110 active:scale-[0.99] text-white font-bold text-sm flex items-center justify-center gap-2 shadow-lg shadow-emerald-950/50 hover:shadow-emerald-500/25 border border-emerald-400/20 transition-all duration-200 cursor-pointer disabled:opacity-60 disabled:cursor-not-allowed"
                  >
                    <span>
                      {isRecoverySubmitting
                        ? locale === 'vi-VN' ? 'Đang xử lý...' : 'Processing...'
                        : locale === 'vi-VN' ? 'Đổi mật khẩu' : 'Reset Password'}
                    </span>
                  </button>
                </form>
              )}

              {/* SCREEN 4 — SUCCESS (Only upon HTTP 204) */}
              {recoveryStep === 'success' && (
                <div className="text-center py-4 space-y-4">
                  <div className="inline-flex items-center justify-center w-14 h-14 rounded-2xl bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 border border-emerald-500/20">
                    <CheckCircle2 className="w-8 h-8" />
                  </div>
                  <div>
                    <h1 className="text-2xl font-extrabold text-slate-900 dark:text-[#E5E7EB] tracking-tight font-display">
                      {locale === 'vi-VN' ? 'Đặt lại mật khẩu thành công' : 'Password Reset Successful'}
                    </h1>
                    <p className="text-xs text-slate-500 dark:text-[#9CA3AF] mt-2 font-normal leading-relaxed">
                      {locale === 'vi-VN'
                        ? 'Mật khẩu của bạn đã được cập nhật thành công. Bạn có thể đăng nhập bằng mật khẩu mới.'
                        : 'Your password has been successfully updated. You can now log in with your new password.'}
                    </p>
                  </div>

                  <button
                    type="button"
                    id="btn-recovery-success-return"
                    onClick={handleCancelRecovery}
                    className="w-full h-11 rounded-[10px] bg-emerald-600 hover:bg-emerald-500 text-white font-bold text-sm transition-colors shadow-md shadow-emerald-950/30 cursor-pointer"
                  >
                    {locale === 'vi-VN' ? 'Quay lại đăng nhập' : 'Back to Login'}
                  </button>
                </div>
              )}
            </div>
          ) : loginStage === 'pin_verify' ? (
            <div className="w-full space-y-4 max-w-[370px]">
              <div className="text-center mb-1">
                <div className="inline-flex items-center justify-center w-12 h-12 rounded-2xl bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 mb-2 border border-emerald-500/20">
                  <Shield className="w-6 h-6" />
                </div>
                <h1 className="text-2xl font-extrabold text-slate-900 dark:text-[#E5E7EB] tracking-tight font-display">
                  {locale === 'vi-VN' ? 'Xác thực Mã bảo mật' : 'Security PIN Verification'}
                </h1>
                <p className="text-xs text-slate-500 dark:text-[#9CA3AF] mt-1 font-normal">
                  {locale === 'vi-VN'
                    ? 'Nhập mã bảo mật 6 số để hoàn tất phiên đăng nhập'
                    : 'Enter your 6-digit Security PIN to complete login'}
                </p>
              </div>

              {/* User Identity Chip */}
              <div className="p-2.5 rounded-xl bg-slate-50 dark:bg-[#151B22] border border-slate-200 dark:border-white/10 flex items-center justify-between">
                <div className="flex items-center gap-2 min-w-0">
                  <div className="w-7 h-7 rounded-full bg-emerald-100 dark:bg-emerald-950/60 text-emerald-700 dark:text-emerald-300 font-bold text-xs flex items-center justify-center shrink-0">
                    {(loginId || 'U').charAt(0).toUpperCase()}
                  </div>
                  <div className="min-w-0">
                    <p className="text-xs font-semibold text-slate-900 dark:text-white truncate">
                      {loginId}
                    </p>
                    <p className="text-[10px] text-slate-500 dark:text-slate-400 truncate">
                      {locale === 'vi-VN' ? 'Đang xác thực bảo mật' : 'Security verification in progress'}
                    </p>
                  </div>
                </div>
                <span className="text-[9px] font-bold px-2 py-0.5 rounded-full bg-slate-200 dark:bg-slate-800 text-slate-700 dark:text-slate-300 uppercase">
                  {locale === 'vi-VN' ? 'Bảo mật' : 'Security'}
                </span>
              </div>

              {/* Error Banner */}
              {pinError && (
                <div className="flex items-start gap-2.5 p-3 rounded-[10px] bg-rose-50 dark:bg-rose-950/60 border border-rose-300 dark:border-rose-500/50 text-xs text-rose-800 dark:text-rose-300 shadow-xs">
                  <AlertCircle className="h-4 w-4 text-rose-600 dark:text-rose-400 shrink-0 mt-0.5" />
                  <div className="flex-1 font-medium">{pinError}</div>
                </div>
              )}

              {/* Six-digit PIN Input with auto-verify on 6th digit */}
              <div className="space-y-3 py-1">
                <div className="flex items-center justify-between px-1">
                  <label className="block text-[11px] font-semibold text-slate-700 dark:text-slate-300 uppercase tracking-wider">
                    {locale === 'vi-VN' ? 'Mã bảo mật (6 số)' : 'Security PIN (6 digits)'}
                  </label>
                  <button
                    type="button"
                    id="btn-forgot-pin-login"
                    onClick={handleOpenForgotPinModal}
                    className="text-[11px] text-emerald-600 hover:text-emerald-700 dark:text-emerald-400 dark:hover:text-emerald-300 font-medium cursor-pointer"
                  >
                    {locale === 'vi-VN' ? 'Quên mã bảo mật?' : 'Forgot Security PIN?'}
                  </button>
                </div>

                <SixDigitPinInput
                  id="input-auth-pin-verify"
                  value={enteredPin}
                  onChange={(val) => {
                    setEnteredPin(val);
                    if (pinError) setPinError(null);
                  }}
                  onComplete={executePinVerify}
                  disabled={isPinSubmitting}
                  error={Boolean(pinError)}
                  showToggle
                  mask={!showEnteredPin}
                  onToggleMask={() => setShowEnteredPin(!showEnteredPin)}
                  autoFocus
                />

                <div className="text-center text-[11px] text-slate-500 dark:text-slate-400">
                  {isPinSubmitting ? (
                    <span className="inline-flex items-center gap-1.5 text-emerald-600 dark:text-emerald-400 font-medium">
                      <Loader2 className="w-3.5 h-3.5 animate-spin" />
                      {locale === 'vi-VN' ? 'Đang xác thực...' : 'Verifying...'}
                    </span>
                  ) : (
                    <span>{locale === 'vi-VN' ? 'Tự động đăng nhập khi nhập số thứ 6' : 'Signs in automatically on 6th digit'}</span>
                  )}
                </div>
              </div>
            </div>
          ) : loginStage === 'pin_setup' ? (
            <div className="w-full space-y-3 max-w-[370px]">
              <div className="text-center mb-1">
                <div className="inline-flex items-center justify-center w-12 h-12 rounded-2xl bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 mb-2 border border-emerald-500/20">
                  <Shield className="w-6 h-6" />
                </div>
                <h1 className="text-xl font-extrabold text-slate-900 dark:text-[#E5E7EB] tracking-tight font-display">
                  {pinSetupStep === 1
                    ? locale === 'vi-VN'
                      ? 'Tạo Mã bảo mật (6 số)'
                      : 'Create Security PIN (6 digits)'
                    : locale === 'vi-VN'
                    ? 'Xác nhận Mã bảo mật'
                    : 'Confirm Security PIN'}
                </h1>
                <p className="text-[11px] text-slate-500 dark:text-[#9CA3AF] mt-0.5 font-normal leading-relaxed">
                  {pinSetupStep === 1
                    ? locale === 'vi-VN'
                      ? 'Thiết lập Mã bảo mật 6 số bắt buộc để bảo vệ giao dịch tài chính.'
                      : 'Set up a mandatory 6-digit Security PIN to protect your transactions.'
                    : locale === 'vi-VN'
                    ? 'Nhập lại đúng 6 số vừa tạo để hoàn tất thiết lập.'
                    : 'Re-enter the 6 digits to complete setup.'}
                </p>
              </div>

              {/* User Identity Chip */}
              <div className="p-2 rounded-xl bg-slate-50 dark:bg-[#151B22] border border-slate-200 dark:border-white/10 flex items-center justify-between">
                <div className="min-w-0">
                  <p className="text-xs font-semibold text-slate-900 dark:text-white truncate">
                    {loginId || (locale === 'vi-VN' ? 'Tài khoản SpendWise' : 'SpendWise Account')}
                  </p>
                  {loginId && (
                    <p className="text-[10px] text-slate-500 dark:text-slate-400 truncate">
                      {loginId}
                    </p>
                  )}
                </div>
                <span className="text-[9px] font-bold px-2 py-0.5 rounded-full bg-emerald-100 dark:bg-emerald-950/60 text-emerald-700 dark:text-emerald-300 uppercase">
                  {pinSetupStep === 1
                    ? locale === 'vi-VN' ? 'Bước 1/2' : 'Step 1/2'
                    : locale === 'vi-VN' ? 'Bước 2/2' : 'Step 2/2'}
                </span>
              </div>

              {/* Error Banner */}
              {pinError && (
                <div className="flex items-start gap-2 p-2.5 rounded-[10px] bg-rose-50 dark:bg-rose-950/60 border border-rose-300 dark:border-rose-500/50 text-xs text-rose-800 dark:text-rose-300 shadow-xs">
                  <AlertCircle className="h-4 w-4 text-rose-600 dark:text-rose-400 shrink-0 mt-0.5" />
                  <div className="flex-1 font-medium">{pinError}</div>
                </div>
              )}

              {pinSetupStep === 1 ? (
                <div className="space-y-3 py-2">
                  <SixDigitPinInput
                    id="input-auth-pin-setup-step1"
                    value={enteredPin}
                    onChange={(val) => {
                      setEnteredPin(val);
                      if (pinError) setPinError(null);
                    }}
                    onComplete={handlePinSetupStep1Complete}
                    disabled={isPinSubmitting}
                    error={Boolean(pinError)}
                    showToggle
                    mask={!showEnteredPin}
                    onToggleMask={() => setShowEnteredPin(!showEnteredPin)}
                    autoFocus
                  />
                  <p className="text-[11px] text-slate-500 dark:text-slate-400 text-center">
                    {locale === 'vi-VN' ? 'Tự động chuyển bước khi nhập đủ 6 số' : 'Advances automatically on 6th digit'}
                  </p>
                </div>
              ) : (
                <div className="space-y-3 py-2">
                  <SixDigitPinInput
                    id="input-auth-pin-setup-step2"
                    value={confirmSetupPin}
                    onChange={(val) => {
                      setConfirmSetupPin(val);
                      if (pinError) setPinError(null);
                    }}
                    onComplete={executePinSetupStep2}
                    disabled={isPinSubmitting}
                    error={Boolean(pinError)}
                    showToggle
                    mask={!showEnteredPin}
                    onToggleMask={() => setShowEnteredPin(!showEnteredPin)}
                    autoFocus
                  />
                  <div className="flex items-center justify-between text-[11px] text-slate-500 dark:text-slate-400 px-1">
                    <button
                      type="button"
                      onClick={() => {
                        setPinSetupStep(1);
                        setConfirmSetupPin('');
                        setPinError(null);
                      }}
                      className="text-slate-500 hover:text-slate-700 dark:text-slate-400 underline"
                    >
                      {locale === 'vi-VN' ? '← Nhập lại mã' : '← Re-enter PIN'}
                    </button>
                    <span>
                      {isPinSubmitting
                        ? locale === 'vi-VN' ? 'Đang lưu...' : 'Saving...'
                        : locale === 'vi-VN' ? 'Tự động lưu khi nhập đủ 6 số' : 'Saves automatically on 6th digit'}
                    </span>
                  </div>
                </div>
              )}

              {/* Back button */}
              <div className="pt-2">
                <button
                  type="button"
                  id="btn-cancel-pin-setup"
                  onClick={handleCancelPinStage}
                  disabled={isPinSubmitting}
                  className="w-full py-2.5 text-xs text-slate-500 hover:text-slate-700 dark:text-slate-400 dark:hover:text-slate-200 font-medium cursor-pointer transition-colors text-center border border-slate-200 dark:border-white/10 rounded-xl hover:bg-slate-50 dark:hover:bg-slate-800"
                >
                  {locale === 'vi-VN' ? '← Quay lại đăng nhập' : '← Back to Login'}
                </button>
              </div>
            </div>
          ) : (
          <form onSubmit={handleLoginSubmit} onKeyDown={handleLoginKeyDown} className="w-full space-y-4 max-w-[370px]">
            <div className="text-center mb-2">
              <h1 className="text-2xl sm:text-3xl font-extrabold text-slate-900 dark:text-[#E5E7EB] tracking-tight font-display">
                {locale === 'vi-VN' ? 'Đăng Nhập' : 'Login'}
              </h1>
              <p className="text-xs text-slate-500 dark:text-[#9CA3AF] mt-1 font-normal">
                {locale === 'vi-VN'
                  ? 'Truy cập không gian quản lý tài chính SpendWise'
                  : 'Access your SpendWise financial workspace'}
              </p>
            </div>

            {/* Success Banner */}
            

            {/* Error Banner */}
            {loginError && (
              <div className="flex items-start gap-2.5 p-3 rounded-[10px] bg-rose-50 dark:bg-rose-950/60 border border-rose-300 dark:border-rose-500/50 text-xs text-rose-800 dark:text-rose-300 shadow-xs">
                <AlertCircle className="h-4 w-4 text-rose-600 dark:text-rose-400 shrink-0 mt-0.5" />
                <div className="flex-1 font-medium">{loginError}</div>
              </div>
            )}

            {/* Input: Email or Phone */}
            <div className="space-y-1">
              <label className="block text-[11px] font-semibold text-slate-700 dark:text-slate-300 uppercase tracking-wider">
                {locale === 'vi-VN' ? 'Email hoặc số điện thoại' : 'Email or phone number'}
              </label>
              <div className="relative group">
                <input
                  ref={loginIdInputRef}
                  id="input-admin-login-id"
                  type="text"
                  value={loginId}
                  onChange={(e) => setLoginId(e.target.value)}
                  onKeyDown={handleLoginKeyDown}
                  placeholder={locale === 'vi-VN' ? 'example@gmail.com hoặc 0xxxxxxxxx' : 'example@gmail.com or 0xxxxxxxxx'}
                  disabled={isLoginSubmitting}
                  className="w-full h-10.5 pl-10 pr-4 rounded-[10px] bg-white dark:bg-[#151B22] border border-slate-200 dark:border-white/10 hover:border-slate-300 dark:hover:border-white/20 focus:border-emerald-500 focus:ring-2 focus:ring-emerald-500/20 text-sm text-slate-900 dark:text-[#E5E7EB] placeholder:text-slate-400 dark:placeholder:text-[#6B7280] focus:outline-none transition-all font-medium shadow-xs"
                />
                <Mail className="absolute left-3.5 top-1/2 -translate-y-1/2 h-4 w-4 text-slate-400 dark:text-[#6B7280] group-focus-within:text-emerald-600 dark:group-focus-within:text-emerald-400 transition-colors" />
              </div>
            </div>

            {/* Input: Password */}
            <div className="space-y-1">
              <label className="block text-[11px] font-semibold text-slate-700 dark:text-slate-300 uppercase tracking-wider">
                {locale === 'vi-VN' ? 'Mật khẩu' : 'Password'}
              </label>
              <div className="relative group">
                <input
                  ref={loginPasswordInputRef}
                  id="input-admin-login-password"
                  type={showLoginPassword ? 'text' : 'password'}
                  value={loginPassword}
                  onChange={(e) => setLoginPassword(e.target.value)}
                  onKeyDown={handleLoginKeyDown}
                  placeholder="••••••••"
                  disabled={isLoginSubmitting}
                  className="w-full h-10.5 pl-10 pr-10 rounded-[10px] bg-white dark:bg-[#151B22] border border-slate-200 dark:border-white/10 hover:border-slate-300 dark:hover:border-white/20 focus:border-emerald-500 focus:ring-2 focus:ring-emerald-500/20 text-sm text-slate-900 dark:text-[#E5E7EB] placeholder:text-slate-400 dark:placeholder:text-[#6B7280] focus:outline-none transition-all font-medium tracking-wide shadow-xs"
                />
                <Lock className="absolute left-3.5 top-1/2 -translate-y-1/2 h-4 w-4 text-slate-400 dark:text-[#6B7280] group-focus-within:text-emerald-600 dark:group-focus-within:text-emerald-400 transition-colors" />
                <button
                  type="button"
                  onClick={() => setShowLoginPassword(!showLoginPassword)}
                  className="absolute right-3 top-1/2 -translate-y-1/2 p-1 text-slate-400 dark:text-[#6B7280] hover:text-slate-700 dark:hover:text-slate-200 cursor-pointer"
                >
                  {showLoginPassword ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
                </button>
              </div>
            </div>

            {/* Remember Me & Forgot Password Entry */}
            <div className="flex items-center justify-between text-xs pt-0.5">
              <label className="flex items-center gap-2 text-slate-600 dark:text-slate-300 cursor-pointer select-none">
                <input
                  type="checkbox"
                  checked={rememberMe}
                  onChange={(e) => setRememberMe(e.target.checked)}
                  className="h-4 w-4 rounded border-slate-300 dark:border-slate-700 bg-white dark:bg-[#151B22] text-emerald-600 focus:ring-emerald-500/30 cursor-pointer accent-emerald-600"
                />
                <span className="font-medium">{locale === 'vi-VN' ? 'Ghi nhớ phiên đăng nhập' : 'Remember my session'}</span>
              </label>

              <button
                type="button"
                id="btn-admin-forgot-password-entry"
                onClick={handleOpenRecovery}
                className="text-xs text-emerald-600 hover:text-emerald-700 dark:text-emerald-400 dark:hover:text-emerald-300 font-semibold cursor-pointer transition-colors"
              >
                {locale === 'vi-VN' ? 'Quên mật khẩu?' : 'Forgot password?'}
              </button>
            </div>

            {/* Submit Button */}
            <button
              type="submit"
              id="btn-admin-login-submit"
              disabled={isLoginSubmitting}
              className="w-full h-11 rounded-[10px] bg-gradient-to-r from-emerald-600 via-emerald-500 to-emerald-600 hover:brightness-110 active:scale-[0.99] text-white font-bold text-sm flex items-center justify-center gap-2 shadow-lg shadow-emerald-950/50 hover:shadow-emerald-500/25 border border-emerald-400/20 transition-all duration-200 cursor-pointer disabled:opacity-60 disabled:cursor-not-allowed"
            >
              <span>{isLoginSubmitting ? (locale === 'vi-VN' ? 'Đang đăng nhập...' : 'Logging in...') : (locale === 'vi-VN' ? 'Đăng Nhập' : 'Login')}</span>
            </button>
          </form>
          )}

          {/* Branded Animated S Loading Overlay for Login Submission */}
          {isLoginSubmitting && (
            <div
              className="absolute inset-0 bg-[#FCFBF7]/90 dark:bg-[#101622]/92 backdrop-blur-xs flex flex-col items-center justify-center z-30 transition-all duration-300"
              aria-live="polite"
              aria-label={locale === 'vi-VN' ? 'Đang đăng nhập vào SpendWise...' : 'Logging into SpendWise...'}
            >
              <SpendWiseSLoading
                size={110}
                status={loginLoadingStatus}
                label={
                  loginLoadingStatus === 'success'
                    ? locale === 'vi-VN'
                      ? 'Xác thực thành công!'
                      : 'Authenticated successfully!'
                    : loginLoadingStatus === 'error'
                    ? locale === 'vi-VN'
                      ? 'Đăng nhập không thành công'
                      : 'Login failed'
                    : loginLoadingStatus === 'offline'
                    ? locale === 'vi-VN'
                      ? 'Không có kết nối mạng'
                      : 'No Internet Connection'
                    : loginLoadingStatus === 'timeout'
                    ? locale === 'vi-VN'
                      ? 'Hết thời gian chờ phản hồi'
                      : 'Request Timed Out'
                    : locale === 'vi-VN'
                    ? 'Đang xác thực thông tin...'
                    : 'Authenticating account...'
                }
                sublabel={
                  loginLoadingStatus === 'success'
                    ? locale === 'vi-VN'
                      ? 'Đang chuyển đến bước tiếp theo...'
                      : 'Proceeding to next step...'
                    : loginLoadingStatus === 'error'
                    ? loginError || (locale === 'vi-VN' ? 'Vui lòng kiểm tra lại thông tin' : 'Please check your credentials')
                    : loginLoadingStatus === 'offline'
                    ? locale === 'vi-VN'
                      ? 'Kiểm tra mạng rồi thử lại.'
                      : 'Check connection and retry.'
                    : loginLoadingStatus === 'timeout'
                    ? locale === 'vi-VN'
                      ? 'Yêu cầu mất quá nhiều thời gian.'
                      : 'Request took too long.'
                    : locale === 'vi-VN'
                    ? 'Hệ thống đang chuẩn bị số liệu tài chính'
                    : 'Preparing your financial ledger'
                }
              />
            </div>
          )}
        </div>

        {/* Static branded notebook panel — no registration transition */}
        <div className="auth-toggle-box">
          {/* Existing decorative brand panel */}
          <div className="auth-toggle-panel toggle-left">
            {/* Official Brand Logo Badge */}
            <SpendWiseLogo size={46} variant="badge" className="mb-3.5 shadow-lg shadow-black/40" />

            {/* Serif Headline */}
            <h1 className="font-fraunces font-bold text-2xl sm:text-[27px] text-white leading-tight mb-3 drop-shadow-xs">
              {locale === 'vi-VN' ? (
                <>
                  Mỗi khoản chi,<br />
                  <em className="italic text-amber-300 font-fraunces font-normal">một dòng ghi rõ ràng.</em>
                </>
              ) : (
                <>
                  Every expense,<br />
                  <em className="italic text-amber-300 font-fraunces font-normal">clearly recorded.</em>
                </>
              )}
            </h1>

          </div>

        </div>

      </div>
      </div>

      {/* Footer Info (Prominent Security Plaque on Desk) */}
      <div className="mt-5 mb-1 z-10 flex items-center justify-center px-4">
        <div className="inline-flex flex-wrap items-center justify-center gap-2 sm:gap-3 px-4 sm:px-5 py-2 rounded-full text-xs font-medium
          bg-white/95 text-stone-900 border border-stone-300/90 shadow-md shadow-stone-900/10 ring-1 ring-stone-900/5 backdrop-blur-md
          dark:bg-[#12171E]/95 dark:text-stone-300 dark:border-white/10 dark:ring-white/5 dark:shadow-xl dark:shadow-black/60
          transition-all duration-200 hover:shadow-lg hover:border-emerald-500/40">
          <div className="flex items-center justify-center w-5 h-5 rounded-full bg-emerald-600 text-white shadow-xs dark:bg-emerald-950/80 dark:text-emerald-400 dark:border dark:border-emerald-500/30">
            <Shield className="h-3 w-3" />
          </div>
          <span className="font-bold text-stone-900 dark:text-white tracking-tight">
            SpendWise Finance OS
          </span>
          <span className="hidden sm:inline-block w-1 h-1 rounded-full bg-stone-400 dark:bg-stone-600" />
          <span className="text-stone-700 dark:text-stone-300 font-medium">
            {locale === 'vi-VN' ? 'Bảo mật tài chính cá nhân' : 'Personal Financial Security'}
          </span>
        </div>
      </div>

      {/* Forgot Security PIN Modal */}
      {showForgotPinModal && (
        <div
          id="modal-forgot-pin"
          className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/60 backdrop-blur-xs animate-in fade-in duration-150 overflow-y-auto"
        >
          <div className="w-full max-w-md bg-white dark:bg-[#151B22] rounded-2xl p-6 border border-slate-200 dark:border-white/10 shadow-2xl space-y-4 my-8 animate-in zoom-in-95 duration-150">
            <div className="flex items-start justify-between pb-2 border-b border-slate-100 dark:border-white/5">
              <div className="flex items-center gap-2">
                <div className="h-8 w-8 rounded-lg bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 flex items-center justify-center">
                  <Shield className="h-4 w-4" />
                </div>
                <h3 className="text-base font-bold text-slate-900 dark:text-white">
                  {locale === 'vi-VN' ? 'Khôi phục Mã bảo mật' : 'Recover Security PIN'}
                </h3>
              </div>
              <button
                type="button"
                id="btn-close-forgot-pin-modal"
                onClick={handleCloseForgotPinModal}
                className="p-1.5 rounded-lg text-slate-400 hover:text-slate-600 hover:bg-slate-100 dark:hover:bg-slate-800 transition-colors cursor-pointer"
              >
                <X className="h-4 w-4" />
              </button>
            </div>

            {forgotPinSubmitted ? (
              <div className="space-y-4 py-2">
                <div className="p-4 rounded-xl bg-emerald-50 dark:bg-emerald-950/40 border border-emerald-200 dark:border-emerald-800 text-xs text-emerald-800 dark:text-emerald-300 space-y-2">
                  <div className="flex items-center gap-2 font-bold text-sm">
                    <CheckCircle2 className="h-4 w-4 text-emerald-600 dark:text-emerald-400" />
                    <span>
                      {locale === 'vi-VN' ? 'Đã gửi hướng dẫn!' : 'Instructions Sent!'}
                    </span>
                  </div>
                  <p className="leading-relaxed">
                    {locale === 'vi-VN'
                      ? 'Liên kết đặt lại mã bảo mật đã được gửi. Mã bảo mật hiện tại vẫn có hiệu lực cho đến khi bạn thiết lập thành công mã bảo mật mới.'
                      : 'A Security PIN reset link has been sent. Your current PIN remains valid until you successfully set a new Security PIN.'}
                  </p>
                </div>

                <button
                  type="button"
                  id="btn-confirm-forgot-pin-close"
                  onClick={handleCloseForgotPinModal}
                  className="w-full mt-2 py-2.5 rounded-[10px] bg-emerald-600 hover:bg-emerald-500 text-white font-bold text-xs transition-colors cursor-pointer shadow-sm shadow-emerald-900/30"
                >
                  {locale === 'vi-VN' ? 'Đã hiểu' : 'Got it'}
                </button>
              </div>
            ) : (
              <form onSubmit={handleForgotPinSubmit} className="space-y-4">
                <p className="text-xs text-slate-600 dark:text-slate-300 leading-relaxed">
                  {locale === 'vi-VN'
                    ? 'Nhập địa chỉ email tài khoản của bạn để nhận liên kết tạo Mã bảo mật 6 số mới. Mã bảo mật hiện tại vẫn có hiệu lực cho đến khi bạn thiết lập thành công mã bảo mật mới.'
                    : 'Enter your account email to receive a secure link to create a new 6-digit Security PIN. Your current PIN remains valid until you successfully set a new Security PIN.'}
                </p>

                <div className="space-y-1.5">
                  <label
                    htmlFor="forgot-pin-email-input"
                    className="text-xs font-semibold text-slate-700 dark:text-slate-300 uppercase tracking-wider"
                  >
                    {locale === 'vi-VN' ? 'Địa chỉ email tài khoản' : 'Account email address'}
                  </label>
                  <input
                    id="forgot-pin-email-input"
                    type="email"
                    value={forgotPinEmail}
                    onChange={(e) => {
                      setForgotPinEmail(e.target.value);
                      if (forgotPinError) setForgotPinError(null);
                    }}
                    placeholder="example@gmail.com"
                    className={`w-full h-10.5 px-3.5 rounded-[10px] bg-white dark:bg-[#151B22] border text-xs text-slate-900 dark:text-white placeholder:text-slate-400 dark:placeholder:text-[#6B7280] focus:outline-none focus:ring-2 shadow-xs transition-colors ${
                      forgotPinError
                        ? 'border-rose-400 focus:border-rose-500 focus:ring-rose-500/20'
                        : 'border-slate-200 dark:border-white/10 focus:border-emerald-500 focus:ring-emerald-500/20'
                    }`}
                    required
                    autoFocus
                  />
                  {forgotPinError && (
                    <p className="text-[11px] text-rose-500 dark:text-rose-400 font-medium">
                      {forgotPinError}
                    </p>
                  )}
                </div>

                <div className="flex items-center justify-end gap-2.5 pt-2">
                  <button
                    type="button"
                    id="btn-cancel-forgot-pin"
                    onClick={handleCloseForgotPinModal}
                    className="px-4 py-2 rounded-lg text-xs font-semibold text-slate-500 hover:text-slate-900 dark:text-slate-400 dark:hover:text-white hover:bg-slate-100 dark:hover:bg-slate-800 transition-colors cursor-pointer"
                  >
                    {locale === 'vi-VN' ? 'Hủy' : 'Cancel'}
                  </button>
                  <button
                    type="submit"
                    id="btn-submit-forgot-pin"
                    disabled={isForgotPinSubmitting}
                    className="px-5 py-2 rounded-[10px] text-xs font-bold text-white bg-emerald-600 hover:bg-emerald-500 disabled:opacity-50 disabled:hover:bg-emerald-600 disabled:cursor-not-allowed shadow-sm shadow-emerald-900/30 transition-all cursor-pointer flex items-center gap-1.5"
                  >
                    {isForgotPinSubmitting ? (
                      <>
                        <Loader2 className="h-3.5 w-3.5 animate-spin" />
                        <span>{locale === 'vi-VN' ? 'Đang xử lý...' : 'Processing...'}</span>
                      </>
                    ) : (
                      <span>{locale === 'vi-VN' ? 'Gửi liên kết' : 'Send Reset Link'}</span>
                    )}
                  </button>
                </div>
              </form>
            )}
          </div>
        </div>
      )}
    </div>
  );
};
