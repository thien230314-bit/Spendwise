import React, { useState, useEffect, useRef } from 'react';
import {
  ShieldCheck,
  KeyRound,
  Lock,
  Mail,
  Eye,
  EyeOff,
  ArrowLeft,
  ArrowRight,
  Check,
  AlertTriangle,
  Sparkles,
  Smartphone,
  User,
  Globe,
  Sun,
  Moon,
  LogOut,
  RotateCcw,
  ChevronDown,
  X,
  RefreshCw,
  Clock,
} from 'lucide-react';
import { useSpendWise } from '../context/SpendWiseContext';
import { useAuth } from '../providers/AuthProvider';
import { AuthLayout } from '../components/AuthLayout';
import { FormField, glassInputClass } from '../components/FormField';
import {
  TeddyBearHandle,
  calculateLookFromInput,
} from '../components/TeddyBearAvatar';

export interface AdminOperatorAccount {
  operatorId: string;
  fullName: string;
  email: string;
  roleVi: string;
  roleEn: string;
  unitVi: string;
  unitEn: string;
  securityPin: string;
}

export const DEFAULT_ADMIN_ACCOUNT: AdminOperatorAccount = {
  operatorId: 'OP-08',
  fullName: 'Trần Minh Quân',
  email: 'admin@spendwise.vn',
  roleVi: 'Quản trị viên Vận hành Cấp cao',
  roleEn: 'Senior Operations Administrator',
  unitVi: 'Khối Vận hành & Kiểm soát Nội bộ',
  unitEn: 'Core Operations & Risk Control',
  securityPin: '246810',
};

export const DEFAULT_ADMIN_PASSWORD = 'Admin@2026!';
export const DEMO_VALID_RECOVERY_OTP = '849201';
export const DEMO_EXPIRED_RECOVERY_OTP = '111999';

export const ADMIN_DEMO_FIRST_OTP_VERIFIED_STORAGE_KEY =
  'sw_admin_demo_first_otp_verified_v1';

export const FIRST_LOGIN_OTP_TTL_SECONDS = 120;

export function generateRandom8DigitOtp(previousCode?: string): string {
  for (let attempt = 0; attempt < 16; attempt++) {
    let num = 0;
    if (typeof window !== 'undefined' && window.crypto?.getRandomValues) {
      const buf = new Uint32Array(1);
      window.crypto.getRandomValues(buf);
      num = 10000000 + (buf[0] % 90000000);
    } else {
      num = Math.floor(10000000 + Math.random() * 90000000);
    }
    const code = String(num);
    if (code !== previousCode && code !== '00000000') {
      return code;
    }
  }
  return '84729163';
}

export function hasAdminDemoCompletedFirstOtp(email: string): boolean {
  try {
    if (typeof localStorage === 'undefined') return false;
    const stored = localStorage.getItem(ADMIN_DEMO_FIRST_OTP_VERIFIED_STORAGE_KEY);
    return stored === email.trim().toLowerCase();
  } catch {
    return false;
  }
}

export function markAdminDemoFirstOtpVerified(email: string): void {
  try {
    if (typeof localStorage === 'undefined') return;
    localStorage.setItem(
      ADMIN_DEMO_FIRST_OTP_VERIFIED_STORAGE_KEY,
      email.trim().toLowerCase()
    );
  } catch {
    // ignore storage errors
  }
}

export function clearAdminDemoFirstOtpVerified(): void {
  try {
    if (typeof localStorage === 'undefined') return;
    localStorage.removeItem(ADMIN_DEMO_FIRST_OTP_VERIFIED_STORAGE_KEY);
  } catch {
    // ignore storage errors
  }
}

type AuthGateMode =
  | 'login'
  | 'first_login_otp'
  | 'pin_challenge'
  | 'forgot_request'
  | 'forgot_reset'
  | 'forgot_success';

interface AdminAuthGateProps {
  adminAccount: AdminOperatorAccount;
  currentAdminPassword: string;
  onUpdateAdminPassword: (nextPassword: string) => void;
  onAuthenticated: () => void;
  onExitToMobileApp: () => void;
}

export const AdminAuthGate: React.FC<AdminAuthGateProps> = ({
  adminAccount,
  currentAdminPassword,
  onUpdateAdminPassword,
  onAuthenticated,
  onExitToMobileApp,
}) => {
  const { tr, language, setLanguage, resolvedTheme, setTheme } = useSpendWise();
  const { initiateLogin, completePinVerify, currentUser } = useAuth();

  const [mode, setMode] = useState<AuthGateMode>('login');
  const [isTyping, setIsTyping] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const bearRef = useRef<TeddyBearHandle | null>(null);
  const typingTimerRef = useRef<number | null>(null);
  const blurTimeoutRef = useRef<number | null>(null);
  const activeGroupRef = useRef<'text' | 'password' | 'pin' | null>(null);
  const isClickingPassToggleRef = useRef(false);

  // Step 1: Email + Password state (never prefilled automatically)
  const [emailInput, setEmailInput] = useState('');
  const [passwordInput, setPasswordInput] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [loginError, setLoginError] = useState<string | null>(null);
  const [autofillNotice, setAutofillNotice] = useState<string | null>(null);

  // Step 1B (First Login Only): Random 8-digit One-Time OTP Challenge
  const [firstOtpGeneratedCode, setFirstOtpGeneratedCode] = useState<string>('');
  const [firstOtpInput, setFirstOtpInput] = useState<string>('');
  const [firstOtpExpiresAtMs, setFirstOtpExpiresAtMs] = useState<number>(0);
  const [firstOtpRemainingSeconds, setFirstOtpRemainingSeconds] = useState<number>(0);
  const [firstOtpManuallyExpired, setFirstOtpManuallyExpired] = useState<boolean>(false);
  const [firstOtpUsed, setFirstOtpUsed] = useState<boolean>(false);
  const [firstOtpError, setFirstOtpError] = useState<string | null>(null);
  const [firstOtpErrorKind, setFirstOtpErrorKind] = useState<
    'validation' | 'wrong_code' | 'expired_code' | null
  >(null);
  const [firstOtpResendNotice, setFirstOtpResendNotice] = useState<string | null>(null);

  // Step 2: PIN Challenge state (never prefilled automatically)
  const [pinInput, setPinInput] = useState('');
  const [pinError, setPinError] = useState<string | null>(null);
  const [pinAutofillNotice, setPinAutofillNotice] = useState<string | null>(null);

  // Forgot password / Recovery state
  const [recoveryEmail, setRecoveryEmail] = useState('');
  const [recoveryOtp, setRecoveryOtp] = useState('');
  const [recoveryNewPassword, setRecoveryNewPassword] = useState('');
  const [recoveryConfirmPassword, setRecoveryConfirmPassword] = useState('');
  const [showRecoveryPassword, setShowRecoveryPassword] = useState(false);
  const [recoveryError, setRecoveryError] = useState<string | null>(null);
  const [recoveryErrorCode, setRecoveryErrorCode] = useState<
    'validation' | 'wrong_code' | 'expired_code' | null
  >(null);
  const [isOtpManuallyExpired, setIsOtpManuallyExpired] = useState(false);
  const [resendBanner, setResendBanner] = useState<string | null>(null);

  const clearBlurTimeout = () => {
    if (blurTimeoutRef.current !== null) {
      window.clearTimeout(blurTimeoutRef.current);
      blurTimeoutRef.current = null;
    }
  };

  const triggerTypingPulse = () => {
    setIsTyping(true);
    if (typingTimerRef.current !== null) {
      window.clearTimeout(typingTimerRef.current);
    }
    typingTimerRef.current = window.setTimeout(() => {
      setIsTyping(false);
    }, 240);
  };

  useEffect(() => {
    return () => {
      clearBlurTimeout();
      if (typingTimerRef.current !== null) {
        window.clearTimeout(typingTimerRef.current);
      }
    };
  }, []);

  useEffect(() => {
    clearBlurTimeout();
    activeGroupRef.current = null;
    isClickingPassToggleRef.current = false;
    bearRef.current?.reset();
  }, [mode]);

  // Live countdown timer for First-Login 8-digit OTP expiry
  useEffect(() => {
    if (mode !== 'first_login_otp' || !firstOtpExpiresAtMs) return;
    if (firstOtpManuallyExpired) {
      setFirstOtpRemainingSeconds(0);
      return;
    }

    const updateRemaining = () => {
      const rem = Math.max(0, Math.ceil((firstOtpExpiresAtMs - Date.now()) / 1000));
      setFirstOtpRemainingSeconds(rem);
    };

    updateRemaining();
    const intervalId = window.setInterval(updateRemaining, 1000);
    return () => window.clearInterval(intervalId);
  }, [mode, firstOtpExpiresAtMs, firstOtpManuallyExpired]);

  const handleTextFocus = (e: React.FocusEvent<HTMLInputElement>) => {
    clearBlurTimeout();
    activeGroupRef.current = 'text';
    bearRef.current?.setHandsUp(false);
    bearRef.current?.lookAt(calculateLookFromInput(e.currentTarget));
  };

  const handleTextLookUpdate = (el: HTMLInputElement, ensureChecking = false) => {
    if (activeGroupRef.current !== 'text') return;
    if (ensureChecking) {
      bearRef.current?.setHandsUp(false);
    }
    bearRef.current?.lookAt(calculateLookFromInput(el));
  };

  const handleFieldBlur = (group: 'text' | 'password' | 'pin') => {
    clearBlurTimeout();
    blurTimeoutRef.current = window.setTimeout(() => {
      if (isClickingPassToggleRef.current) return;
      if (activeGroupRef.current === group) {
        activeGroupRef.current = null;
        bearRef.current?.setIdle();
      }
    }, 100);
  };

  const handlePasswordFocus = () => {
    clearBlurTimeout();
    activeGroupRef.current = 'password';
    bearRef.current?.setHandsUp(!showPassword);
  };

  // Single clearly marked Admin DEMO account button: fills email & password ONLY, never submits
  const handleExplicitDemoLoginAutofill = () => {
    setEmailInput(adminAccount.email);
    setPasswordInput(currentAdminPassword);
    setLoginError(null);
    setAutofillNotice(
      tr(
        'Đã điền thông tin tài khoản Quản trị mẫu (DEMO). Nhấn "Đăng nhập" để tiếp tục.',
        'Filled DEMO Admin credentials. Click "Sign In" to continue.'
      )
    );
    triggerTypingPulse();
  };

  const issueNewFirstLoginOtp = (previousCode?: string, isResend = false) => {
    const nextCode = generateRandom8DigitOtp(previousCode);
    const expiresAt = Date.now() + FIRST_LOGIN_OTP_TTL_SECONDS * 1000;
    setFirstOtpGeneratedCode(nextCode);
    setFirstOtpInput('');
    setFirstOtpExpiresAtMs(expiresAt);
    setFirstOtpRemainingSeconds(FIRST_LOGIN_OTP_TTL_SECONDS);
    setFirstOtpManuallyExpired(false);
    setFirstOtpUsed(false);
    setFirstOtpError(null);
    setFirstOtpErrorKind(null);
    if (isResend) {
      setFirstOtpResendNotice(
        tr(
          `Đã phát hành mã OTP 8 chữ số mới dùng 1 lần (DEMO: ${nextCode}). Mã trước đó đã bị hủy hiệu lực.`,
          `Issued a new one-time 8-digit OTP (DEMO: ${nextCode}). Previous codes were invalidated.`
        )
      );
    } else {
      setFirstOtpResendNotice(null);
    }
  };

  const handleLoginSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    const trimmedEmail = emailInput.trim();

    if (!trimmedEmail || !trimmedEmail.includes('@') || !trimmedEmail.includes('.')) {
      setLoginError(
        tr(
          'Vui lòng nhập địa chỉ email quản trị hợp lệ.',
          'Please enter a valid administrator email address.'
        )
      );
      bearRef.current?.triggerFail();
      return;
    }

    if (!passwordInput) {
      setLoginError(
        tr(
          'Vui lòng nhập mật khẩu quản trị viên.',
          'Please enter your administrator password.'
        )
      );
      bearRef.current?.triggerFail();
      return;
    }

    setIsSubmitting(true);
    setLoginError(null);
    setAutofillNotice(null);

    try {
      const res = await initiateLogin(trimmedEmail, passwordInput, 'SpendWise Web Admin');
      if (res.success) {
        setLoginError(null);
        setPinInput('');
        setPinError(null);
        bearRef.current?.triggerSuccess();
        if (res.needsPin) {
          setMode('pin_challenge');
        } else {
          onAuthenticated();
        }
      } else {
        setLoginError(
          res.error ||
            tr(
              'Email hoặc mật khẩu quản trị không chính xác.',
              'Invalid administrator email or password.'
            )
        );
        bearRef.current?.triggerFail();
      }
    } catch (err: any) {
      setLoginError(
        err?.detail ||
          err?.title ||
          err?.message ||
          tr(
            'Không thể kết nối máy chủ xác thực Backend.',
            'Unable to connect to authentication server.'
          )
      );
      bearRef.current?.triggerFail();
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleFirstLoginOtpSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setFirstOtpResendNotice(null);
    const cleanedOtp = firstOtpInput.trim();

    if (!/^\d{8}$/.test(cleanedOtp)) {
      setFirstOtpErrorKind('validation');
      setFirstOtpError(
        tr(
          'Mã xác thực OTP phải gồm đúng 8 chữ số.',
          'Verification OTP code must be 8 digits.'
        )
      );
      bearRef.current?.triggerFail();
      return;
    }

    const isTimeExpired =
      firstOtpManuallyExpired ||
      firstOtpRemainingSeconds <= 0 ||
      (firstOtpExpiresAtMs > 0 && Date.now() >= firstOtpExpiresAtMs);

    if (isTimeExpired || firstOtpUsed || cleanedOtp === '00000000') {
      setFirstOtpErrorKind('expired_code');
      setFirstOtpError(
        tr(
          'Mã xác thực OTP 8 chữ số đã hết hạn hoặc đã được sử dụng. Vui lòng nhấn "Gửi lại OTP" để nhận mã mới.',
          'This 8-digit OTP code has expired or was already used. Please click "Resend OTP" to generate a new code.'
        )
      );
      bearRef.current?.triggerFail();
      return;
    }

    if (cleanedOtp !== firstOtpGeneratedCode) {
      setFirstOtpErrorKind('wrong_code');
      setFirstOtpError(
        tr(
          'Mã xác thực OTP không chính xác. Vui lòng kiểm tra lại mã 8 chữ số hiển thị trong hộp DEMO.',
          'Incorrect 8-digit OTP code. Please check the DEMO code and try again.'
        )
      );
      bearRef.current?.triggerFail();
      return;
    }

    // Valid one-time 8-digit OTP verified!
    setFirstOtpUsed(true);
    setFirstOtpError(null);
    setFirstOtpErrorKind(null);
    markAdminDemoFirstOtpVerified(adminAccount.email);
    bearRef.current?.triggerSuccess();

    setPinInput('');
    setPinError(null);
    setPinAutofillNotice(null);
    setMode('pin_challenge');
  };

  const handleResendFirstLoginOtp = () => {
    issueNewFirstLoginOtp(firstOtpGeneratedCode, true);
    triggerTypingPulse();
  };

  const handleExpireFirstLoginOtpNow = () => {
    setFirstOtpManuallyExpired(true);
    setFirstOtpRemainingSeconds(0);
    setFirstOtpExpiresAtMs(Date.now() - 1000);
    setFirstOtpResendNotice(null);
    setFirstOtpErrorKind('expired_code');
    setFirstOtpError(
      tr(
        'Mã xác thực OTP hiện tại đã hết hiệu lực (DEMO). Vui lòng nhấn "Gửi lại OTP" để nhận mã 8 chữ số mới.',
        'The current OTP code has expired (DEMO). Please click "Resend OTP" to receive a new 8-digit code.'
      )
    );
  };

  const handleExplicitDemoPinAutofill = () => {
    setPinInput(adminAccount.securityPin);
    setPinError(null);
    setPinAutofillNotice(
      tr(
        'Đã điền mã PIN bảo mật mẫu (246810). Nhấn "Xác nhận đăng nhập" để mở bảng điều khiển.',
        'Filled DEMO security PIN (246810). Click "Confirm Sign-In" to enter Web Admin.'
      )
    );
    triggerTypingPulse();
  };

  const handlePinSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    const cleanedPin = pinInput.trim();

    if (!/^\d{6}$/.test(cleanedPin)) {
      setPinError(
        tr(
          'Mã PIN bảo mật quản trị phải gồm đúng 6 chữ số.',
          'Administrator security PIN must be exactly 6 digits.'
        )
      );
      bearRef.current?.triggerFail();
      return;
    }

    setIsSubmitting(true);
    setPinError(null);

    try {
      const res = await completePinVerify(cleanedPin);
      if (res.success) {
        if (res.user?.role !== 'SystemAdmin') {
          setPinError(
            tr(
              'Tài khoản không có đặc quyền Quản trị viên hệ thống (SystemAdmin).',
              'Account does not have SystemAdmin privileges.'
            )
          );
          bearRef.current?.triggerFail();
          return;
        }
        setPinError(null);
        bearRef.current?.triggerSuccess();
        onAuthenticated();
      } else {
        setPinError(
          res.error ||
            tr(
              'Mã PIN bảo mật không chính xác. Vui lòng thử lại.',
              'Incorrect security PIN. Please try again.'
            )
        );
        bearRef.current?.triggerFail();
      }
    } catch (err: any) {
      setPinError(
        err?.detail ||
          err?.title ||
          err?.message ||
          tr('Lỗi xác thực mã PIN bảo mật.', 'Security PIN verification error.')
      );
      bearRef.current?.triggerFail();
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleOpenForgotPassword = () => {
    setRecoveryEmail(emailInput.trim());
    setRecoveryOtp('');
    setRecoveryNewPassword('');
    setRecoveryConfirmPassword('');
    setRecoveryError(null);
    setRecoveryErrorCode(null);
    setIsOtpManuallyExpired(false);
    setResendBanner(null);
    setMode('forgot_request');
  };

  const handleForgotRequestSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    const trimmed = recoveryEmail.trim();
    if (!trimmed || !trimmed.includes('@') || !trimmed.includes('.')) {
      setRecoveryErrorCode('validation');
      setRecoveryError(
        tr(
          'Vui lòng nhập đúng định dạng địa chỉ email công vụ (ví dụ: admin@spendwise.vn).',
          'Please enter a valid work email address (e.g., admin@spendwise.vn).'
        )
      );
      bearRef.current?.triggerFail();
      return;
    }

    setRecoveryError(null);
    setRecoveryErrorCode(null);
    setRecoveryOtp('');
    setIsOtpManuallyExpired(false);
    setResendBanner(null);
    bearRef.current?.triggerSuccess();
    setMode('forgot_reset');
  };

  const handleResendRecoveryOtp = () => {
    setIsOtpManuallyExpired(false);
    setRecoveryOtp('');
    setRecoveryError(null);
    setRecoveryErrorCode(null);
    setResendBanner(
      tr(
        'Đã cấp lại mã OTP khôi phục mới (DEMO: 849201). Mã cũ đã bị hủy hiệu lực.',
        'Issued a fresh DEMO recovery OTP (849201). Previous codes were invalidated.'
      )
    );
  };

  const handleForgotResetSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setResendBanner(null);

    const cleanedOtp = recoveryOtp.trim();
    if (!/^\d{6}$/.test(cleanedOtp)) {
      setRecoveryErrorCode('validation');
      setRecoveryError(
        tr(
          'Vui lòng nhập mã xác thực OTP gồm đúng 6 chữ số.',
          'Please enter a valid 6-digit OTP verification code.'
        )
      );
      bearRef.current?.triggerFail();
      return;
    }

    if (cleanedOtp === DEMO_EXPIRED_RECOVERY_OTP || isOtpManuallyExpired) {
      setRecoveryErrorCode('expired_code');
      setRecoveryError(
        tr(
          'Mã xác thực OTP đã hết hạn hiệu lực (quá 5 phút). Vui lòng nhấn "Cấp lại mã OTP mới (DEMO)" để tiếp tục.',
          'This OTP code has expired (past 5-minute window). Please click "Resend New OTP (DEMO)" to continue.'
        )
      );
      bearRef.current?.triggerFail();
      return;
    }

    if (cleanedOtp !== DEMO_VALID_RECOVERY_OTP) {
      setRecoveryErrorCode('wrong_code');
      setRecoveryError(
        tr(
          'Mã xác thực OTP không chính xác. Vui lòng kiểm tra lại mã 6 chữ số.',
          'Incorrect OTP verification code. Please check the 6-digit code and try again.'
        )
      );
      bearRef.current?.triggerFail();
      return;
    }

    if (recoveryNewPassword.length < 8) {
      setRecoveryErrorCode('validation');
      setRecoveryError(
        tr(
          'Mật khẩu quản trị mới phải có tối thiểu 8 ký tự.',
          'New administrator password must be at least 8 characters long.'
        )
      );
      bearRef.current?.triggerFail();
      return;
    }

    if (recoveryNewPassword !== recoveryConfirmPassword) {
      setRecoveryErrorCode('validation');
      setRecoveryError(
        tr(
          'Mật khẩu xác nhận không khớp với mật khẩu mới.',
          'Password confirmation does not match the new password.'
        )
      );
      bearRef.current?.triggerFail();
      return;
    }

    onUpdateAdminPassword(recoveryNewPassword);
    setRecoveryError(null);
    setRecoveryErrorCode(null);
    setPasswordInput('');
    bearRef.current?.triggerSuccess();
    setMode('forgot_success');
  };

  const handleReturnToLogin = () => {
    setMode('login');
    setLoginError(null);
    setPinError(null);
    setFirstOtpError(null);
    setFirstOtpErrorKind(null);
    setRecoveryError(null);
    setRecoveryErrorCode(null);
    setAutofillNotice(null);
  };

  const firstOtpMin = Math.floor(firstOtpRemainingSeconds / 60);
  const firstOtpSec = String(firstOtpRemainingSeconds % 60).padStart(2, '0');

  const getHeaderMeta = (): {
    title: string;
    subtitle?: string;
    bearVariant: 'login' | 'register';
  } => {
    switch (mode) {
      case 'first_login_otp':
        return {
          title: tr('Xác thực Email', 'Verify Email'),
          subtitle: tr(
            `Nhập mã OTP 8 chữ số dùng 1 lần cho lần đăng nhập đầu tiên của ${adminAccount.email}`,
            `Enter the one-time 8-digit OTP for the first sign-in of ${adminAccount.email}`
          ),
          bearVariant: 'register',
        };
      case 'pin_challenge':
        return {
          title: tr('Xác thực mã PIN', 'Verify 6-Digit PIN'),
          subtitle: tr(
            'Nhập mã PIN 6 chữ số để hoàn tất đăng nhập bảo mật',
            'Enter your 6-digit security PIN to complete sign-in'
          ),
          bearVariant: 'login',
        };
      case 'forgot_request':
      case 'forgot_reset':
      case 'forgot_success':
        return {
          title: tr('Khôi phục mật khẩu', 'Reset Password'),
          subtitle:
            mode === 'forgot_request'
              ? tr(
                  'Nhập email quản trị để nhận mã xác thực khôi phục (DEMO)',
                  'Enter your administrator email to receive a recovery code (DEMO)'
                )
              : mode === 'forgot_reset'
              ? tr(
                  'Nhập mã xác thực OTP tạm thời và mật khẩu mới',
                  'Enter the temporary OTP verification code and your new password'
                )
              : tr(
                  'Mật khẩu quản trị đã được cập nhật thành công',
                  'Your administrator password has been updated'
                ),
          bearVariant: 'login',
        };
      case 'login':
      default:
        return {
          title: tr('Đăng nhập Quản trị', 'Admin Sign In'),
          subtitle: tr(
            'Cổng Quản trị Vận hành Nội bộ SpendWise',
            'SpendWise Internal Operations Console'
          ),
          bearVariant: 'login',
        };
    }
  };

  const headerMeta = getHeaderMeta();

  return (
    <div
      data-testid="sw-admin-auth-gate"
      className="relative min-h-dvh w-full bg-[var(--bg)] text-[var(--ink)] flex flex-col justify-between px-4 py-4 sm:px-6 overflow-x-hidden"
    >
      <div aria-hidden="true" className="sw-mesh fixed inset-0 pointer-events-none" />

      {/* Top utility bar */}
      <header className="relative z-10 mx-auto flex w-full max-w-5xl items-center justify-between gap-3">
        <div className="flex items-center gap-2.5 min-w-0">
          <span
            aria-hidden="true"
            className="inline-flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-gradient-to-br from-[#10b981] via-[#059669] to-[#047857] font-display text-[1.0625rem] font-extrabold text-white shadow-xs"
          >
            S
          </span>
          <div className="min-w-0">
            <div className="font-display text-[0.9375rem] font-extrabold tracking-tight text-[var(--ink)] truncate">
              SpendWise Admin
            </div>
            <div className="text-[0.75rem] font-medium text-[var(--ink2)] truncate">
              {tr('Cổng Quản trị Vận hành', 'Operations Console')}
            </div>
          </div>
        </div>

        <div className="flex items-center gap-2 shrink-0">
          <button
            type="button"
            data-testid="sw-admin-auth-lang-toggle"
            onClick={() => setLanguage(language === 'vi' ? 'en' : 'vi')}
            className="sw-focus-ring sw-btn ghost sm !min-h-[36px] !px-2.5 !py-1 text-[0.75rem]"
          >
            <Globe aria-hidden="true" className="h-3.5 w-3.5" />
            <span>{language === 'vi' ? 'VI' : 'EN'}</span>
          </button>

          <button
            type="button"
            data-testid="sw-admin-auth-theme-toggle"
            onClick={() => setTheme(resolvedTheme === 'dark' ? 'light' : 'dark')}
            className="sw-focus-ring sw-btn ghost sm !min-h-[36px] !px-2.5 !py-1 text-[0.75rem]"
          >
            {resolvedTheme === 'dark' ? (
              <Sun aria-hidden="true" className="h-3.5 w-3.5" />
            ) : (
              <Moon aria-hidden="true" className="h-3.5 w-3.5" />
            )}
            <span className="hidden sm:inline">
              {resolvedTheme === 'dark' ? tr('Sáng', 'Light') : tr('Tối', 'Dark')}
            </span>
          </button>

          <button
            type="button"
            data-testid="sw-admin-auth-exit-btn"
            onClick={onExitToMobileApp}
            className="sw-focus-ring sw-btn ghost sm !min-h-[36px] !px-3 !py-1 text-[0.75rem]"
          >
            <Smartphone aria-hidden="true" className="h-3.5 w-3.5" />
            <span>{tr('Về ứng dụng di động', 'Back to Mobile App')}</span>
          </button>
        </div>
      </header>

      {/* Main Centered Auth Card reusing AuthLayout, FormField, glassInputClass from User AuthScreen */}
      <main className="relative z-10 mx-auto my-auto w-full max-w-[440px]">
        <AuthLayout
          title={headerMeta.title}
          subtitle={headerMeta.subtitle}
          isAnimatingS={isTyping}
          bearVariant={headerMeta.bearVariant}
          bearRef={bearRef}
        >
          {/* ===============================================================
              STEP 1: ADMIN EMAIL & PASSWORD SIGN IN (Matches User AuthScreen)
              =============================================================== */}
          {mode === 'login' && (
            <form
              onSubmit={handleLoginSubmit}
              data-testid="sw-admin-login-form"
              noValidate
              className="space-y-4"
            >
              <FormField
                htmlFor="sw-admin-login-email"
                label="Email"
                error={loginError || undefined}
              >
                <div className="relative">
                  <Mail
                    aria-hidden="true"
                    className="pointer-events-none absolute left-3.5 top-1/2 h-5 w-5 -translate-y-1/2 text-[var(--sw-text-secondary)]"
                  />
                  <input
                    id="sw-admin-login-email"
                    name="username"
                    type="email"
                    required
                    autoComplete="username"
                    data-testid="sw-admin-login-email-input"
                    value={emailInput}
                    onFocus={handleTextFocus}
                    onBlur={() => handleFieldBlur('text')}
                    onClick={(e) => handleTextLookUpdate(e.currentTarget, true)}
                    onKeyUp={(e) => handleTextLookUpdate(e.currentTarget, true)}
                    onChange={(e) => {
                      const el = e.currentTarget;
                      setEmailInput(el.value);
                      if (loginError) setLoginError(null);
                      if (autofillNotice) setAutofillNotice(null);
                      triggerTypingPulse();
                      handleTextLookUpdate(el);
                    }}
                    placeholder="admin@spendwise.vn"
                    className={glassInputClass(true, false, Boolean(loginError))}
                  />
                </div>
              </FormField>

              <FormField
                htmlFor="sw-admin-login-password"
                label={tr('Mật khẩu', 'Password')}
                rightLabel={
                  <button
                    type="button"
                    data-testid="sw-admin-forgot-password-btn"
                    onClick={handleOpenForgotPassword}
                    className="sw-focus-ring inline-flex min-h-[44px] items-center rounded px-2 py-1 text-[0.9375rem] font-semibold text-[var(--sw-action-primary)] dark:text-[#2DD4BF] hover:underline cursor-pointer"
                  >
                    {tr('Quên mật khẩu?', 'Forgot password?')}
                  </button>
                }
              >
                <div className="relative">
                  <Lock
                    aria-hidden="true"
                    className="pointer-events-none absolute left-3.5 top-1/2 h-5 w-5 -translate-y-1/2 text-[var(--sw-text-secondary)]"
                  />
                  <input
                    id="sw-admin-login-password"
                    name="password"
                    type={showPassword ? 'text' : 'password'}
                    required
                    autoComplete="current-password"
                    data-testid="sw-admin-login-password-input"
                    value={passwordInput}
                    onFocus={handlePasswordFocus}
                    onBlur={() => handleFieldBlur('password')}
                    onChange={(e) => {
                      setPasswordInput(e.target.value);
                      if (loginError) setLoginError(null);
                      if (autofillNotice) setAutofillNotice(null);
                      triggerTypingPulse();
                      if (activeGroupRef.current === 'password') {
                        bearRef.current?.setHandsUp(!showPassword);
                      }
                    }}
                    placeholder="••••••••••••"
                    className={glassInputClass(true, true, Boolean(loginError))}
                  />
                  <button
                    type="button"
                    onPointerDown={() => {
                      isClickingPassToggleRef.current = true;
                    }}
                    onFocus={() => {
                      clearBlurTimeout();
                      activeGroupRef.current = 'password';
                    }}
                    onBlur={() => handleFieldBlur('password')}
                    onClick={() => {
                      const nextShow = !showPassword;
                      setShowPassword(nextShow);
                      bearRef.current?.setHandsUp(!nextShow);
                      window.setTimeout(() => {
                        isClickingPassToggleRef.current = false;
                      }, 60);
                    }}
                    aria-label={
                      showPassword
                        ? tr('Ẩn mật khẩu', 'Hide password')
                        : tr('Hiện mật khẩu', 'Show password')
                    }
                    className="sw-focus-ring absolute right-1.5 top-1/2 -translate-y-1/2 flex h-10 w-10 items-center justify-center rounded-lg text-[var(--sw-text-secondary)] cursor-pointer"
                  >
                    {showPassword ? (
                      <EyeOff aria-hidden="true" className="h-5 w-5" />
                    ) : (
                      <Eye aria-hidden="true" className="h-5 w-5" />
                    )}
                  </button>
                </div>
              </FormField>

              {loginError && (
                <div
                  role="alert"
                  data-testid="sw-admin-login-error"
                  className="sr-only"
                >
                  {loginError}
                </div>
              )}

              {autofillNotice && (
                <div
                  role="status"
                  data-testid="sw-admin-autofill-notice"
                  className="rounded-xl border border-emerald-500/35 bg-emerald-500/10 p-3 text-[0.8125rem] font-semibold text-emerald-700 dark:text-emerald-300"
                >
                  {autofillNotice}
                </div>
              )}

              {/* PRIMARY SIGN IN BUTTON (Matches User AuthScreen primary button) */}
              <button
                type="submit"
                disabled={isSubmitting}
                data-testid="sw-admin-login-submit-btn"
                className="sw-focus-ring flex min-h-[48px] w-full items-center justify-center gap-2 rounded-xl bg-[var(--sw-action-primary)] hover:bg-[var(--sw-action-primary-hover)] py-3 px-4 text-[1.0625rem] font-semibold text-white shadow-sm active:scale-[0.99] transition-all disabled:opacity-60 cursor-pointer"
              >
                {isSubmitting ? (
                  <>
                    <RefreshCw className="h-5 w-5 animate-spin" />
                    <span>{tr('Đang xác thực...', 'Authenticating...')}</span>
                  </>
                ) : (
                  <>
                    <span>{tr('Đăng nhập', 'Sign In')}</span>
                    <ArrowRight aria-hidden="true" className="h-5 w-5" />
                  </>
                )}
              </button>
            </form>
          )}

          {/* ===============================================================
              STEP 1B: FIRST-LOGIN 8-DIGIT RANDOM ONE-TIME OTP VERIFICATION
              (Styled like User AuthScreen register_otp)
              =============================================================== */}
          {mode === 'first_login_otp' && (
            <form
              onSubmit={handleFirstLoginOtpSubmit}
              data-testid="sw-admin-first-otp-form"
              noValidate
              className="space-y-4"
            >
              {/* Explicit Demo-Only 8-Digit Random OTP Display Box */}
              <div
                data-testid="sw-admin-first-otp-demo-banner"
                className="rounded-xl border border-emerald-500/35 bg-emerald-500/10 p-3.5 space-y-2"
              >
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <span className="inline-flex items-center gap-1.5 text-[0.78125rem] font-bold text-emerald-800 dark:text-emerald-200">
                    <Sparkles aria-hidden="true" className="h-4 w-4 shrink-0 text-emerald-600 dark:text-emerald-400" />
                    <span>
                      {tr(
                        'Mã OTP 8 số dùng 1 lần (Chỉ hiển thị cho DEMO):',
                        'One-Time 8-Digit OTP (DEMO display only):'
                      )}
                    </span>
                  </span>
                  <span
                    data-testid="sw-admin-first-otp-demo-code"
                    className="rounded-lg bg-[var(--glass2)] border border-emerald-500/40 px-2.5 py-1 font-mono text-[1rem] font-extrabold tracking-widest text-[var(--sw-em-d)] tabular-nums select-all"
                  >
                    {firstOtpGeneratedCode}
                  </span>
                </div>

                <div className="flex flex-wrap items-center justify-between gap-2 text-[0.75rem] text-[var(--ink2)] pt-1 border-t border-emerald-500/20">
                  <span
                    data-testid="sw-admin-first-otp-timer"
                    className="inline-flex items-center gap-1 font-semibold tabular-nums"
                  >
                    <Clock aria-hidden="true" className="h-3.5 w-3.5 shrink-0" />
                    {firstOtpRemainingSeconds > 0 && !firstOtpManuallyExpired ? (
                      <span>
                        {tr(
                          `Còn hiệu lực: ${firstOtpMin}:${firstOtpSec}`,
                          `Expires in: ${firstOtpMin}:${firstOtpSec}`
                        )}
                      </span>
                    ) : (
                      <span className="font-bold text-[var(--rose)]">
                        {tr('Đã hết hạn hiệu lực', 'Code Expired')}
                      </span>
                    )}
                  </span>

                  <button
                    type="button"
                    data-testid="sw-admin-first-otp-expire-now-btn"
                    onClick={handleExpireFirstLoginOtpNow}
                    className="sw-focus-ring text-[0.72rem] font-bold text-[var(--amber)] hover:underline cursor-pointer"
                  >
                    {tr('Giả lập hết hạn ngay (DEMO)', 'Simulate Expiry Now (DEMO)')}
                  </button>
                </div>
              </div>

              <FormField
                htmlFor="sw-admin-first-otp-input"
                label={tr('Mã xác thực OTP', 'OTP Code')}
                hint={
                  firstOtpRemainingSeconds > 0 && !firstOtpManuallyExpired
                    ? tr(
                        `Mã 8 chữ số có hiệu lực trong ${firstOtpMin}:${firstOtpSec} • Chỉ yêu cầu ở lần đăng nhập đầu tiên`,
                        `8-digit code expires in ${firstOtpMin}:${firstOtpSec} • Required on first login only`
                      )
                    : tr(
                        'Mã OTP hiện tại đã hết hạn • Nhấn "Gửi lại OTP" để tạo mã 8 chữ số mới',
                        'Current OTP has expired • Click "Resend OTP" to generate a new 8-digit code'
                      )
                }
                error={firstOtpError || undefined}
              >
                <div className="relative">
                  <KeyRound
                    aria-hidden="true"
                    className="pointer-events-none absolute left-3.5 top-1/2 h-5 w-5 -translate-y-1/2 text-[var(--sw-text-secondary)]"
                  />
                  <input
                    id="sw-admin-first-otp-input"
                    name="otp"
                    type="text"
                    inputMode="numeric"
                    autoComplete="one-time-code"
                    maxLength={8}
                    required
                    autoFocus
                    data-testid="sw-admin-first-otp-input"
                    value={firstOtpInput}
                    onFocus={handleTextFocus}
                    onBlur={() => handleFieldBlur('text')}
                    onChange={(e) => {
                      const clean = e.currentTarget.value
                        .replace(/[^\d]/g, '')
                        .slice(0, 8);
                      setFirstOtpInput(clean);
                      if (firstOtpError) {
                        setFirstOtpError(null);
                        setFirstOtpErrorKind(null);
                      }
                      triggerTypingPulse();
                    }}
                    placeholder={firstOtpGeneratedCode || '12345678'}
                    className={`${glassInputClass(true, false, Boolean(firstOtpError))} font-mono tracking-widest font-bold`}
                  />
                </div>
              </FormField>

              {firstOtpError && (
                <div
                  role="alert"
                  data-testid="sw-admin-first-otp-error"
                  data-error-kind={firstOtpErrorKind || 'validation'}
                  className="sr-only"
                >
                  {firstOtpError}
                </div>
              )}

              {firstOtpResendNotice && !firstOtpError && (
                <div
                  role="status"
                  data-testid="sw-admin-first-otp-resend-notice"
                  className="rounded-xl border border-emerald-500/35 bg-emerald-500/10 p-3 text-[0.8125rem] font-semibold text-emerald-700 dark:text-emerald-300"
                >
                  {firstOtpResendNotice}
                </div>
              )}

              <button
                type="submit"
                data-testid="sw-admin-first-otp-submit-btn"
                className="sw-focus-ring flex min-h-[48px] w-full items-center justify-center gap-2 rounded-xl bg-[var(--sw-action-primary)] hover:bg-[var(--sw-action-primary-hover)] py-3 px-4 text-[1.0625rem] font-semibold text-white shadow-sm active:scale-[0.99] transition-all"
              >
                <span>{tr('Xác nhận OTP & Tiếp tục', 'Verify OTP & Continue')}</span>
                <ArrowRight aria-hidden="true" className="h-5 w-5" />
              </button>

              <div className="grid grid-cols-2 gap-2">
                <button
                  type="button"
                  data-testid="sw-admin-first-otp-resend-btn"
                  onClick={handleResendFirstLoginOtp}
                  className="sw-focus-ring sw-btn sw-btn-ghost w-full cursor-pointer"
                >
                  <RefreshCw aria-hidden="true" className="h-4 w-4" />
                  <span>{tr('Gửi lại OTP', 'Resend OTP')}</span>
                </button>

                <button
                  type="button"
                  data-testid="sw-admin-first-otp-back-btn"
                  onClick={handleReturnToLogin}
                  className="sw-focus-ring sw-btn sw-btn-ghost w-full cursor-pointer"
                >
                  <ArrowLeft aria-hidden="true" className="h-4 w-4" />
                  <span>{tr('Quay lại', 'Back')}</span>
                </button>
              </div>
            </form>
          )}

          {/* ===============================================================
              STEP 2: ADMIN SECURITY PIN CHALLENGE
              (Styled like User AuthScreen challenge)
              =============================================================== */}
          {mode === 'pin_challenge' && (
            <form
              onSubmit={handlePinSubmit}
              data-testid="sw-admin-pin-form"
              noValidate
              className="space-y-4"
            >
              <FormField
                htmlFor="sw-admin-pin-input"
                label={tr('Mã PIN bảo mật', 'Security PIN')}
                hint={tr(
                  'Nhập mã PIN 6 chữ số đã đăng ký với tài khoản Quản trị viên của bạn.',
                  'Enter your 6-digit registered Administrator Security PIN.'
                )}
                error={pinError || undefined}
              >
                <div className="relative">
                  <KeyRound
                    aria-hidden="true"
                    className="pointer-events-none absolute left-3.5 top-1/2 h-5 w-5 -translate-y-1/2 text-[var(--sw-text-secondary)]"
                  />
                  <input
                    id="sw-admin-pin-input"
                    name="pin"
                    type="password"
                    inputMode="numeric"
                    autoComplete="one-time-code"
                    maxLength={6}
                    required
                    autoFocus
                    data-testid="sw-admin-pin-input"
                    value={pinInput}
                    onFocus={() => {
                      clearBlurTimeout();
                      activeGroupRef.current = 'pin';
                      bearRef.current?.setHandsUp(true);
                    }}
                    onBlur={() => handleFieldBlur('pin')}
                    onChange={(e) => {
                      const digitsOnly = e.currentTarget.value
                        .replace(/[^\d]/g, '')
                        .slice(0, 6);
                      setPinInput(digitsOnly);
                      if (pinError) setPinError(null);
                      if (pinAutofillNotice) setPinAutofillNotice(null);
                      triggerTypingPulse();
                    }}
                    placeholder="••••••"
                    className={`${glassInputClass(true, false, Boolean(pinError))} font-mono tracking-widest font-bold`}
                  />
                </div>
              </FormField>

              {pinError && (
                <div
                  role="alert"
                  data-testid="sw-admin-pin-error"
                  className="sr-only"
                >
                  {pinError}
                </div>
              )}

              {pinAutofillNotice && (
                <div
                  role="status"
                  className="rounded-xl border border-emerald-500/35 bg-emerald-500/10 p-3 text-[0.8125rem] font-semibold text-emerald-700 dark:text-emerald-300"
                >
                  {pinAutofillNotice}
                </div>
              )}

              <button
                type="submit"
                disabled={isSubmitting}
                data-testid="sw-admin-pin-submit-btn"
                className="sw-focus-ring flex min-h-[48px] w-full items-center justify-center gap-2 rounded-xl bg-[var(--sw-action-primary)] hover:bg-[var(--sw-action-primary-hover)] py-3 px-4 text-[1.0625rem] font-semibold text-white shadow-sm active:scale-[0.99] transition-all disabled:opacity-60 cursor-pointer"
              >
                {isSubmitting ? (
                  <>
                    <RefreshCw className="h-5 w-5 animate-spin" />
                    <span>{tr('Đang xác thực...', 'Verifying...')}</span>
                  </>
                ) : (
                  <>
                    <span>{tr('Xác nhận đăng nhập', 'Confirm Sign-In')}</span>
                    <ArrowRight aria-hidden="true" className="h-5 w-5" />
                  </>
                )}
              </button>

              <button
                type="button"
                data-testid="sw-admin-pin-back-btn"
                onClick={handleReturnToLogin}
                className="sw-focus-ring sw-btn sw-btn-ghost w-full cursor-pointer"
              >
                <ArrowLeft aria-hidden="true" className="h-4 w-4" />
                <span>{tr('Hủy & Quay lại đăng nhập', 'Cancel & Back to Sign In')}</span>
              </button>
            </form>
          )}

          {/* ===============================================================
              STEP 3A: FORGOT PASSWORD — REQUEST BY EMAIL
              =============================================================== */}
          {mode === 'forgot_request' && (
            <form
              onSubmit={handleForgotRequestSubmit}
              data-testid="sw-admin-forgot-request-form"
              noValidate
              className="space-y-4"
            >
              <FormField
                htmlFor="sw-admin-recovery-email"
                label={tr('Email công vụ', 'Administrator Email')}
                rightLabel={
                  <button
                    type="button"
                    data-testid="sw-admin-recovery-autofill-email-btn"
                    onClick={() => {
                      setRecoveryEmail(adminAccount.email);
                      setRecoveryError(null);
                    }}
                    className="sw-focus-ring text-[0.8125rem] font-semibold text-[var(--sw-action-primary)] dark:text-[#2DD4BF] hover:underline cursor-pointer"
                  >
                    {tr('Điền email mẫu (DEMO)', 'Fill DEMO email')}
                  </button>
                }
                error={recoveryError || undefined}
              >
                <div className="relative">
                  <Mail
                    aria-hidden="true"
                    className="pointer-events-none absolute left-3.5 top-1/2 h-5 w-5 -translate-y-1/2 text-[var(--sw-text-secondary)]"
                  />
                  <input
                    id="sw-admin-recovery-email"
                    type="email"
                    data-testid="sw-admin-recovery-email-input"
                    value={recoveryEmail}
                    onFocus={handleTextFocus}
                    onBlur={() => handleFieldBlur('text')}
                    onChange={(e) => {
                      setRecoveryEmail(e.target.value);
                      if (recoveryError) setRecoveryError(null);
                      triggerTypingPulse();
                    }}
                    placeholder="admin@spendwise.vn"
                    className={glassInputClass(true, false, Boolean(recoveryError))}
                  />
                </div>
              </FormField>

              {recoveryError && (
                <div
                  role="alert"
                  data-testid="sw-admin-recovery-error"
                  className="sr-only"
                >
                  {recoveryError}
                </div>
              )}

              <button
                type="submit"
                data-testid="sw-admin-recovery-request-btn"
                className="sw-focus-ring flex min-h-[48px] w-full items-center justify-center gap-2 rounded-xl bg-[var(--sw-action-primary)] hover:bg-[var(--sw-action-primary-hover)] py-3 px-4 text-[1.0625rem] font-semibold text-white shadow-sm active:scale-[0.99] transition-all"
              >
                <span>{tr('Gửi yêu cầu khôi phục', 'Request Recovery Code')}</span>
                <ArrowRight aria-hidden="true" className="h-5 w-5" />
              </button>

              <button
                type="button"
                data-testid="sw-admin-recovery-back-to-login-btn"
                onClick={handleReturnToLogin}
                className="sw-focus-ring sw-btn sw-btn-ghost min-h-[44px] w-full cursor-pointer"
              >
                <ArrowLeft aria-hidden="true" className="h-4 w-4" />
                <span>{tr('Quay lại đăng nhập', 'Back to Sign In')}</span>
              </button>
            </form>
          )}

          {/* ===============================================================
              STEP 3B: FORGOT PASSWORD — NEUTRAL ACK + DEMO OTP & NEW PASSWORD
              =============================================================== */}
          {mode === 'forgot_reset' && (
            <form
              onSubmit={handleForgotResetSubmit}
              data-testid="sw-admin-forgot-reset-form"
              noValidate
              className="space-y-4"
            >
              {/* Neutral Request Acknowledgment (Never leaks account existence) */}
              <div
                role="status"
                data-testid="sw-admin-recovery-neutral-ack"
                className="rounded-xl border border-emerald-500/35 bg-emerald-500/10 p-3.5 text-[0.8125rem] text-[var(--ink)] leading-relaxed space-y-1"
              >
                <div className="font-extrabold text-[var(--sw-em-d)]">
                  {tr(
                    'Đã ghi nhận yêu cầu khôi phục mật khẩu',
                    'Recovery Request Acknowledged'
                  )}
                </div>
                <p className="text-[0.78125rem] text-[var(--ink2)]">
                  {tr(
                    `Nếu địa chỉ "${recoveryEmail.trim()}" thuộc danh sách quản trị viên được cấp quyền, hệ thống đã khởi tạo mã xác thực OTP tạm thời. Vì lý do bảo mật, hệ thống không xác nhận hay phủ nhận sự tồn tại của email.`,
                    `If "${recoveryEmail.trim()}" belongs to an authorized administrator, a temporary recovery OTP has been generated. For security, account existence is neither confirmed nor denied.`
                  )}
                </p>
              </div>

              {/* DEMO OTP Simulation & State Testing Controls */}
              <div
                data-testid="sw-admin-recovery-demo-box"
                className="rounded-xl border border-[var(--line)] bg-[var(--sw-surface-muted)]/75 p-3.5 space-y-2"
              >
                <div className="flex flex-wrap items-center justify-between gap-2 text-[0.75rem]">
                  <span className="font-bold text-[var(--ink)]">
                    {tr(
                      'Trình mô phỏng OTP Khôi phục (DEMO):',
                      'Recovery OTP Simulator (DEMO):'
                    )}
                  </span>
                  <span className="font-mono font-extrabold text-[var(--sw-em-d)] tabular-nums">
                    OTP: {DEMO_VALID_RECOVERY_OTP}
                  </span>
                </div>
                <div className="grid grid-cols-1 sm:grid-cols-3 gap-1.5">
                  <button
                    type="button"
                    data-testid="sw-admin-recovery-fill-valid-otp-btn"
                    onClick={() => {
                      setRecoveryOtp(DEMO_VALID_RECOVERY_OTP);
                      setIsOtpManuallyExpired(false);
                      setRecoveryError(null);
                      setRecoveryErrorCode(null);
                    }}
                    className="sw-focus-ring rounded-lg border border-[var(--line)] bg-[var(--glass2)] px-2.5 py-1.5 text-[0.72rem] font-bold text-[var(--sw-em-d)] hover:bg-[var(--sw-selected-bg)] cursor-pointer"
                  >
                    {tr('Điền mã đúng (849201)', 'Fill Valid (849201)')}
                  </button>

                  <button
                    type="button"
                    data-testid="sw-admin-recovery-fill-wrong-otp-btn"
                    onClick={() => {
                      setRecoveryOtp('000000');
                      setIsOtpManuallyExpired(false);
                      setRecoveryError(null);
                      setRecoveryErrorCode(null);
                    }}
                    className="sw-focus-ring rounded-lg border border-[var(--line)] bg-[var(--glass2)] px-2.5 py-1.5 text-[0.72rem] font-bold text-[var(--ink2)] hover:text-[var(--ink)] cursor-pointer"
                  >
                    {tr('Thử mã sai (000000)', 'Test Wrong (000000)')}
                  </button>

                  <button
                    type="button"
                    data-testid="sw-admin-recovery-fill-expired-otp-btn"
                    onClick={() => {
                      setRecoveryOtp(DEMO_EXPIRED_RECOVERY_OTP);
                      setIsOtpManuallyExpired(true);
                      setRecoveryError(null);
                      setRecoveryErrorCode(null);
                    }}
                    className="sw-focus-ring rounded-lg border border-[var(--line)] bg-[var(--glass2)] px-2.5 py-1.5 text-[0.72rem] font-bold text-[var(--amber)] hover:text-[var(--ink)] cursor-pointer"
                  >
                    {tr('Thử mã hết hạn (111999)', 'Test Expired (111999)')}
                  </button>
                </div>

                <div className="flex items-center justify-between gap-2 pt-1 border-t border-[var(--line)] text-[0.72rem]">
                  <span className="text-[var(--ink2)]">
                    {tr(
                      'Không gửi email thực · Chỉ kiểm thử nội bộ',
                      'Zero real email sent · Local simulation only'
                    )}
                  </span>
                  <button
                    type="button"
                    data-testid="sw-admin-recovery-resend-otp-btn"
                    onClick={handleResendRecoveryOtp}
                    className="sw-focus-ring inline-flex items-center gap-1 font-bold text-[var(--sw-em-d)] hover:underline cursor-pointer"
                  >
                    <RefreshCw aria-hidden="true" className="h-3 w-3" />
                    <span>
                      {tr('Cấp lại mã OTP mới (DEMO)', 'Resend New OTP (DEMO)')}
                    </span>
                  </button>
                </div>
              </div>

              {resendBanner && (
                <div
                  role="status"
                  data-testid="sw-admin-recovery-resend-banner"
                  className="rounded-xl bg-[var(--sw-selected-bg)] px-3 py-2 text-[0.78125rem] font-bold text-[var(--sw-em-d)]"
                >
                  {resendBanner}
                </div>
              )}

              <FormField
                htmlFor="sw-admin-recovery-otp"
                label={tr('Mã xác thực OTP (6 chữ số)', 'OTP Verification Code (6 digits)')}
              >
                <div className="relative">
                  <KeyRound
                    aria-hidden="true"
                    className="pointer-events-none absolute left-3.5 top-1/2 h-5 w-5 -translate-y-1/2 text-[var(--sw-text-secondary)]"
                  />
                  <input
                    id="sw-admin-recovery-otp"
                    type="text"
                    inputMode="numeric"
                    maxLength={6}
                    data-testid="sw-admin-recovery-otp-input"
                    value={recoveryOtp}
                    onChange={(e) => {
                      setRecoveryOtp(e.target.value.replace(/\D/g, '').slice(0, 6));
                      if (recoveryError) {
                        setRecoveryError(null);
                        setRecoveryErrorCode(null);
                      }
                    }}
                    placeholder="849201"
                    className={`${glassInputClass(true)} font-mono tracking-widest font-bold`}
                  />
                </div>
              </FormField>

              <FormField
                htmlFor="sw-admin-recovery-new-pass"
                label={tr('Mật khẩu mới (tối thiểu 8 ký tự)', 'New Password (min 8 chars)')}
              >
                <div className="relative">
                  <Lock
                    aria-hidden="true"
                    className="pointer-events-none absolute left-3.5 top-1/2 h-5 w-5 -translate-y-1/2 text-[var(--sw-text-secondary)]"
                  />
                  <input
                    id="sw-admin-recovery-new-pass"
                    type={showRecoveryPassword ? 'text' : 'password'}
                    data-testid="sw-admin-recovery-new-password-input"
                    value={recoveryNewPassword}
                    onChange={(e) => {
                      setRecoveryNewPassword(e.target.value);
                      if (recoveryError) {
                        setRecoveryError(null);
                        setRecoveryErrorCode(null);
                      }
                    }}
                    placeholder="••••••••••••"
                    className={glassInputClass(true, true)}
                  />
                  <button
                    type="button"
                    onClick={() => setShowRecoveryPassword((prev) => !prev)}
                    aria-label={
                      showRecoveryPassword
                        ? tr('Ẩn mật khẩu', 'Hide password')
                        : tr('Hiện mật khẩu', 'Show password')
                    }
                    className="sw-focus-ring absolute right-1.5 top-1/2 -translate-y-1/2 flex h-10 w-10 items-center justify-center rounded-lg text-[var(--sw-text-secondary)] cursor-pointer"
                  >
                    {showRecoveryPassword ? (
                      <EyeOff aria-hidden="true" className="h-5 w-5" />
                    ) : (
                      <Eye aria-hidden="true" className="h-5 w-5" />
                    )}
                  </button>
                </div>
              </FormField>

              <FormField
                htmlFor="sw-admin-recovery-confirm-pass"
                label={tr('Xác nhận mật khẩu mới', 'Confirm New Password')}
              >
                <div className="relative">
                  <Lock
                    aria-hidden="true"
                    className="pointer-events-none absolute left-3.5 top-1/2 h-5 w-5 -translate-y-1/2 text-[var(--sw-text-secondary)]"
                  />
                  <input
                    id="sw-admin-recovery-confirm-pass"
                    type={showRecoveryPassword ? 'text' : 'password'}
                    data-testid="sw-admin-recovery-confirm-password-input"
                    value={recoveryConfirmPassword}
                    onChange={(e) => {
                      setRecoveryConfirmPassword(e.target.value);
                      if (recoveryError) {
                        setRecoveryError(null);
                        setRecoveryErrorCode(null);
                      }
                    }}
                    placeholder="••••••••••••"
                    className={glassInputClass(true)}
                  />
                </div>
              </FormField>

              {recoveryError && (
                <div
                  role="alert"
                  data-testid="sw-admin-recovery-error"
                  data-error-kind={recoveryErrorCode || 'validation'}
                  className="rounded-xl bg-[var(--rose-s)] px-3.5 py-2.5 text-[0.8125rem] font-bold text-[var(--rose)] flex items-start gap-2"
                >
                  <AlertTriangle aria-hidden="true" className="h-4 w-4 shrink-0 mt-0.5" />
                  <span>{recoveryError}</span>
                </div>
              )}

              <button
                type="submit"
                data-testid="sw-admin-recovery-submit-btn"
                className="sw-focus-ring flex min-h-[48px] w-full items-center justify-center gap-2 rounded-xl bg-[var(--sw-action-primary)] hover:bg-[var(--sw-action-primary-hover)] py-3 px-4 text-[1.0625rem] font-semibold text-white shadow-sm active:scale-[0.99] transition-all"
              >
                <span>{tr('Đặt lại mật khẩu (DEMO)', 'Reset Password (DEMO)')}</span>
                <ArrowRight aria-hidden="true" className="h-5 w-5" />
              </button>

              <button
                type="button"
                data-testid="sw-admin-recovery-back-to-login-btn"
                onClick={handleReturnToLogin}
                className="sw-focus-ring sw-btn sw-btn-ghost min-h-[44px] w-full cursor-pointer"
              >
                <ArrowLeft aria-hidden="true" className="h-4 w-4" />
                <span>{tr('Quay lại đăng nhập', 'Back to Sign In')}</span>
              </button>
            </form>
          )}

          {/* ===============================================================
              STEP 3C: FORGOT PASSWORD — SUCCESS STATE
              =============================================================== */}
          {mode === 'forgot_success' && (
            <div
              data-testid="sw-admin-recovery-success"
              className="space-y-4 text-center py-2"
            >
              <div className="mx-auto flex h-12 w-12 items-center justify-center rounded-2xl bg-[var(--sw-selected-bg)] text-[var(--sw-em-d)]">
                <Check aria-hidden="true" className="h-6 w-6" />
              </div>
              <div className="space-y-1.5">
                <h2 className="font-display text-[1.125rem] font-extrabold text-[var(--ink)]">
                  {tr(
                    'Đã đặt lại mật khẩu thành công',
                    'Password Reset Complete'
                  )}
                </h2>
                <p className="text-[0.84375rem] text-[var(--ink2)] leading-relaxed">
                  {tr(
                    'Mật khẩu quản trị trong phiên DEMO đã được cập nhật. Vui lòng quay lại màn hình đăng nhập để xác thực bằng mật khẩu mới và mã PIN bảo mật.',
                    'Your in-session DEMO administrator password has been updated. Return to sign in with your new password and security PIN.'
                  )}
                </p>
              </div>
              <button
                type="button"
                data-testid="sw-admin-recovery-back-to-login-btn"
                onClick={handleReturnToLogin}
                className="sw-focus-ring flex min-h-[48px] w-full items-center justify-center gap-2 rounded-xl bg-[var(--sw-action-primary)] hover:bg-[var(--sw-action-primary-hover)] py-3 px-4 text-[1.0625rem] font-semibold text-white shadow-sm active:scale-[0.99] transition-all"
              >
                <span>{tr('Quay lại đăng nhập', 'Return to Sign In')}</span>
                <ArrowRight aria-hidden="true" className="h-5 w-5" />
              </button>
            </div>
          )}
        </AuthLayout>
      </main>

      {/* Footer Notice */}
      <footer className="relative z-10 mx-auto w-full max-w-5xl text-center text-[0.75rem] text-[var(--ink2)]">
        {tr(
          'Môi trường trình diễn SpendWise Admin DEMO · Xác thực độc lập với phiên người dùng di động · Không gửi email hoặc gọi máy chủ ngoài.',
          'SpendWise Admin DEMO · Strictly isolated from mobile user session · Zero external emails or backend calls.'
        )}
      </footer>
    </div>
  );
};

interface AdminAccountMenuProps {
  adminAccount: AdminOperatorAccount;
  currentAdminPassword: string;
  onUpdateAdminPassword: (nextPassword: string) => void;
  onRequestResetDemoFixtures: () => void;
  onLogoutAdmin: () => void;
}

export const AdminAccountMenu: React.FC<AdminAccountMenuProps> = ({
  adminAccount,
  currentAdminPassword,
  onUpdateAdminPassword,
  onRequestResetDemoFixtures,
  onLogoutAdmin,
}) => {
  const { tr, language, setLanguage, resolvedTheme, setTheme } = useSpendWise();

  const [isOpen, setIsOpen] = useState(false);
  const [isChangePasswordModalOpen, setIsChangePasswordModalOpen] =
    useState(false);
  const [currentPassInput, setCurrentPassInput] = useState('');
  const [newPassInput, setNewPassInput] = useState('');
  const [confirmPassInput, setConfirmPassInput] = useState('');
  const [changePassError, setChangePassError] = useState<string | null>(null);
  const [changePassSuccess, setChangePassSuccess] = useState<string | null>(
    null
  );

  const menuRef = useRef<HTMLDivElement | null>(null);

  useEffect(() => {
    if (!isOpen) return;
    const handleClickOutside = (e: MouseEvent) => {
      if (menuRef.current && !menuRef.current.contains(e.target as Node)) {
        setIsOpen(false);
      }
    };
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        setIsOpen(false);
      }
    };
    document.addEventListener('mousedown', handleClickOutside);
    window.addEventListener('keydown', handleKeyDown);
    return () => {
      document.removeEventListener('mousedown', handleClickOutside);
      window.removeEventListener('keydown', handleKeyDown);
    };
  }, [isOpen]);

  const openChangePasswordModal = () => {
    setIsOpen(false);
    setCurrentPassInput('');
    setNewPassInput('');
    setConfirmPassInput('');
    setChangePassError(null);
    setChangePassSuccess(null);
    setIsChangePasswordModalOpen(true);
  };

  const handleChangePasswordSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!currentPassInput) {
      setChangePassError(
        tr(
          'Vui lòng nhập mật khẩu quản trị hiện tại.',
          'Please enter your current administrator password.'
        )
      );
      return;
    }

    if (currentPassInput !== currentAdminPassword) {
      setChangePassError(
        tr(
          'Mật khẩu hiện tại không chính xác.',
          'Current password is incorrect.'
        )
      );
      return;
    }

    if (newPassInput.length < 8) {
      setChangePassError(
        tr(
          'Mật khẩu mới phải có tối thiểu 8 ký tự.',
          'New password must be at least 8 characters.'
        )
      );
      return;
    }

    if (newPassInput === currentAdminPassword) {
      setChangePassError(
        tr(
          'Mật khẩu mới phải khác mật khẩu đang sử dụng.',
          'New password must be different from your current password.'
        )
      );
      return;
    }

    if (newPassInput !== confirmPassInput) {
      setChangePassError(
        tr(
          'Xác nhận mật khẩu mới không khớp.',
          'New password confirmation does not match.'
        )
      );
      return;
    }

    onUpdateAdminPassword(newPassInput);
    setChangePassError(null);
    setChangePassSuccess(
      tr(
        'Đã đổi mật khẩu quản trị thành công trong phiên DEMO hiện tại.',
        'Administrator password updated for the current DEMO session.'
      )
    );
    setCurrentPassInput('');
    setNewPassInput('');
    setConfirmPassInput('');
  };

  return (
    <>
      <div ref={menuRef} className="relative group/acct">
        <button
          type="button"
          data-testid="sw-admin-account-menu-btn"
          aria-expanded={isOpen}
          aria-haspopup="menu"
          aria-label={tr(
            `Tài khoản của tôi (${adminAccount.fullName})`,
            `My Account (${adminAccount.fullName})`
          )}
          title={tr(
            `Tài khoản của tôi · ${adminAccount.fullName}`,
            `My Account · ${adminAccount.fullName}`
          )}
          onClick={() => setIsOpen((prev) => !prev)}
          className="sw-focus-ring sw-btn ghost sm !h-9 !min-h-[36px] !px-2.5 !py-1 text-[0.78125rem] whitespace-nowrap cursor-pointer"
        >
          <User aria-hidden="true" className="h-4 w-4 shrink-0 text-[var(--sw-em-d)]" />
          <span className="sr-only">
            {tr('Tài khoản của tôi', 'My Account')}
          </span>
          <ChevronDown aria-hidden="true" className="h-3.5 w-3.5 shrink-0 opacity-75" />
        </button>

        {!isOpen && (
          <span
            role="tooltip"
            className="pointer-events-none invisible opacity-0 group-hover/acct:visible group-hover/acct:opacity-100 group-focus-within/acct:visible group-focus-within/acct:opacity-100 transition-opacity duration-150 absolute right-0 top-full mt-1.5 z-50 whitespace-nowrap rounded-lg border border-[var(--stroke)] bg-[var(--ink)] px-2.5 py-1 text-[0.72rem] font-bold text-[var(--bg)] shadow-lg"
          >
            {tr(
              `Tài khoản của tôi · ${adminAccount.fullName}`,
              `My Account · ${adminAccount.fullName}`
            )}
          </span>
        )}

        {isOpen && (
          <div
            role="menu"
            aria-label={tr('Tài khoản của tôi', 'My Account Menu')}
            data-testid="sw-admin-account-menu-dropdown"
            className="fixed right-2.5 top-14 w-[280px] max-w-[calc(100vw-68px)] sm:absolute sm:right-0 sm:top-auto sm:mt-2 sm:w-[300px] sm:max-w-[90vw] rounded-2xl border border-[var(--stroke)] bg-[var(--sw-sheet-bg)] p-3.5 shadow-2xl z-50 space-y-3"
          >
            {/* Operator Profile Summary */}
            <div
              data-testid="sw-admin-account-profile-summary"
              className="space-y-1 border-b border-[var(--line)] pb-3"
            >
              <div className="flex items-center justify-between gap-2">
                <span className="font-display text-[0.9375rem] font-extrabold text-[var(--ink)]">
                  {adminAccount.fullName}
                </span>
                <span className="font-mono text-[0.72rem] font-bold text-[var(--sw-em-d)] tabular-nums">
                  {adminAccount.operatorId}
                </span>
              </div>
              <div className="text-[0.78125rem] font-semibold text-[var(--ink2)]">
                {tr(adminAccount.roleVi, adminAccount.roleEn)}
              </div>
              <div className="text-[0.75rem] text-[var(--ink3)] font-mono truncate">
                {adminAccount.email}
              </div>
              <div className="text-[0.72rem] text-[var(--ink3)]">
                {tr(adminAccount.unitVi, adminAccount.unitEn)}
              </div>
            </div>

            {/* Language & Theme Controls */}
            <div className="space-y-2 border-b border-[var(--line)] pb-3">
              <div className="flex items-center justify-between gap-2 text-[0.78125rem]">
                <span className="font-bold text-[var(--ink2)] flex items-center gap-1.5">
                  <Globe aria-hidden="true" className="h-3.5 w-3.5" />
                  <span>{tr('Ngôn ngữ', 'Language')}</span>
                </span>
                <div className="inline-flex rounded-xl border border-[var(--line)] bg-[var(--sw-surface-muted)] p-0.5">
                  <button
                    type="button"
                    data-testid="sw-admin-menu-lang-vi"
                    onClick={() => setLanguage('vi')}
                    className={`px-2.5 py-1 rounded-lg text-[0.72rem] font-extrabold cursor-pointer ${
                      language === 'vi'
                        ? 'bg-[var(--sw-selected-bg)] text-[var(--sw-em-d)]'
                        : 'text-[var(--ink2)]'
                    }`}
                  >
                    Tiếng Việt
                  </button>
                  <button
                    type="button"
                    data-testid="sw-admin-menu-lang-en"
                    onClick={() => setLanguage('en')}
                    className={`px-2.5 py-1 rounded-lg text-[0.72rem] font-extrabold cursor-pointer ${
                      language === 'en'
                        ? 'bg-[var(--sw-selected-bg)] text-[var(--sw-em-d)]'
                        : 'text-[var(--ink2)]'
                    }`}
                  >
                    English
                  </button>
                </div>
              </div>

              <div className="flex items-center justify-between gap-2 text-[0.78125rem]">
                <span className="font-bold text-[var(--ink2)] flex items-center gap-1.5">
                  {resolvedTheme === 'dark' ? (
                    <Moon aria-hidden="true" className="h-3.5 w-3.5" />
                  ) : (
                    <Sun aria-hidden="true" className="h-3.5 w-3.5" />
                  )}
                  <span>{tr('Giao diện', 'Theme')}</span>
                </span>
                <div className="inline-flex rounded-xl border border-[var(--line)] bg-[var(--sw-surface-muted)] p-0.5">
                  <button
                    type="button"
                    data-testid="sw-admin-menu-theme-light"
                    onClick={() => setTheme('light')}
                    className={`px-2.5 py-1 rounded-lg text-[0.72rem] font-extrabold cursor-pointer ${
                      resolvedTheme === 'light'
                        ? 'bg-[var(--sw-selected-bg)] text-[var(--sw-em-d)]'
                        : 'text-[var(--ink2)]'
                    }`}
                  >
                    {tr('Sáng', 'Light')}
                  </button>
                  <button
                    type="button"
                    data-testid="sw-admin-menu-theme-dark"
                    onClick={() => setTheme('dark')}
                    className={`px-2.5 py-1 rounded-lg text-[0.72rem] font-extrabold cursor-pointer ${
                      resolvedTheme === 'dark'
                        ? 'bg-[var(--sw-selected-bg)] text-[var(--sw-em-d)]'
                        : 'text-[var(--ink2)]'
                    }`}
                  >
                    {tr('Tối', 'Dark')}
                  </button>
                </div>
              </div>
            </div>

            {/* Menu Actions: Change Password, Reset DEMO Data, Logout */}
            <div className="space-y-1">
              <button
                type="button"
                role="menuitem"
                data-testid="sw-admin-change-password-open-btn"
                onClick={openChangePasswordModal}
                className="sw-focus-ring flex w-full items-center gap-2.5 rounded-xl px-3 py-2 text-left text-[0.8125rem] font-bold text-[var(--ink)] hover:bg-[var(--sw-surface-muted)] cursor-pointer"
              >
                <KeyRound aria-hidden="true" className="h-4 w-4 text-[var(--ink2)]" />
                <span>{tr('Đổi mật khẩu', 'Change Password')}</span>
              </button>

              <button
                type="button"
                role="menuitem"
                data-testid="sw-admin-menu-reset-fixtures-btn"
                onClick={() => {
                  setIsOpen(false);
                  onRequestResetDemoFixtures();
                }}
                className="sw-focus-ring flex w-full items-center gap-2.5 rounded-xl px-3 py-2 text-left text-[0.8125rem] font-bold text-[var(--amber)] hover:bg-[var(--sw-surface-muted)] cursor-pointer"
              >
                <RotateCcw aria-hidden="true" className="h-4 w-4" />
                <span>
                  {tr('Khôi phục dữ liệu mẫu...', 'Reset Demo Data...')}
                </span>
              </button>

              <button
                type="button"
                role="menuitem"
                data-testid="sw-admin-logout-btn"
                onClick={() => {
                  setIsOpen(false);
                  onLogoutAdmin();
                }}
                className="sw-focus-ring flex w-full items-center gap-2.5 rounded-xl px-3 py-2 text-left text-[0.8125rem] font-extrabold text-[var(--rose)] hover:bg-[var(--rose-s)]/60 cursor-pointer"
              >
                <LogOut aria-hidden="true" className="h-4 w-4" />
                <span>{tr('Đăng xuất', 'Sign Out')}</span>
              </button>
            </div>
          </div>
        )}
      </div>

      {/* Focused Change Password Dialog */}
      {isChangePasswordModalOpen && (
        <div
          role="dialog"
          aria-modal="true"
          aria-labelledby="sw-admin-cp-modal-title"
          data-testid="sw-admin-change-password-modal"
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4 backdrop-blur-xs"
        >
          <form
            onSubmit={handleChangePasswordSubmit}
            className="sw-card w-full max-w-md max-h-[calc(100dvh-2rem)] flex flex-col overflow-hidden p-0 shadow-xl bg-[var(--sw-sheet-bg)]"
          >
            <div className="shrink-0 flex items-start justify-between gap-3 p-5 border-b border-[var(--line)]">
              <div className="space-y-0.5">
                <h2
                  id="sw-admin-cp-modal-title"
                  className="font-display text-[1.0625rem] font-extrabold text-[var(--ink)]"
                >
                  {tr('Đổi mật khẩu quản trị', 'Change Administrator Password')}
                </h2>
                <p className="text-[0.75rem] text-[var(--ink2)]">
                  {tr(
                    `Áp dụng cho tài khoản ${adminAccount.email} trong phiên DEMO`,
                    `Applies to ${adminAccount.email} in this DEMO session`
                  )}
                </p>
              </div>
              <button
                type="button"
                onClick={() => setIsChangePasswordModalOpen(false)}
                aria-label={tr('Đóng hộp thoại', 'Close dialog')}
                className="sw-focus-ring sw-ib !h-8 !w-8 !min-h-[32px] !min-w-[32px]"
              >
                <X aria-hidden="true" className="h-4 w-4" />
              </button>
            </div>

            <div className="flex-1 min-h-0 overflow-y-auto p-5 space-y-3.5 sw-sheet-body">
              <div className="space-y-1">
                <label
                  htmlFor="sw-admin-cp-current"
                  className="block text-[0.8125rem] font-bold text-[var(--ink)]"
                >
                  {tr('Mật khẩu hiện tại', 'Current Password')}
                </label>
                <input
                  id="sw-admin-cp-current"
                  type="password"
                  data-testid="sw-admin-cp-current-input"
                  value={currentPassInput}
                  onChange={(e) => {
                    setCurrentPassInput(e.target.value);
                    if (changePassError) setChangePassError(null);
                  }}
                  className="sw-in w-full text-[0.84375rem]"
                />
              </div>

              <div className="space-y-1">
                <label
                  htmlFor="sw-admin-cp-new"
                  className="block text-[0.8125rem] font-bold text-[var(--ink)]"
                >
                  {tr('Mật khẩu mới (tối thiểu 8 ký tự)', 'New Password (min 8 chars)')}
                </label>
                <input
                  id="sw-admin-cp-new"
                  type="password"
                  data-testid="sw-admin-cp-new-input"
                  value={newPassInput}
                  onChange={(e) => {
                    setNewPassInput(e.target.value);
                    if (changePassError) setChangePassError(null);
                  }}
                  className="sw-in w-full text-[0.84375rem]"
                />
              </div>

              <div className="space-y-1">
                <label
                  htmlFor="sw-admin-cp-confirm"
                  className="block text-[0.8125rem] font-bold text-[var(--ink)]"
                >
                  {tr('Xác nhận mật khẩu mới', 'Confirm New Password')}
                </label>
                <input
                  id="sw-admin-cp-confirm"
                  type="password"
                  data-testid="sw-admin-cp-confirm-input"
                  value={confirmPassInput}
                  onChange={(e) => {
                    setConfirmPassInput(e.target.value);
                    if (changePassError) setChangePassError(null);
                  }}
                  className="sw-in w-full text-[0.84375rem]"
                />
              </div>

              {changePassError && (
                <div
                  role="alert"
                  data-testid="sw-admin-cp-error"
                  className="rounded-xl bg-[var(--rose-s)] px-3 py-2 text-[0.8125rem] font-bold text-[var(--rose)]"
                >
                  {changePassError}
                </div>
              )}

              {changePassSuccess && (
                <div
                  role="status"
                  data-testid="sw-admin-cp-success"
                  className="rounded-xl bg-[var(--sw-selected-bg)] px-3 py-2 text-[0.8125rem] font-bold text-[var(--sw-em-d)]"
                >
                  {changePassSuccess}
                </div>
              )}
            </div>

            <div className="shrink-0 flex items-center justify-end gap-2 px-5 py-3.5 border-t border-[var(--line)] bg-[var(--sw-sheet-bg)]">
              <button
                type="button"
                data-testid="sw-admin-cp-close-btn"
                onClick={() => setIsChangePasswordModalOpen(false)}
                className="sw-focus-ring sw-btn ghost sm"
              >
                <span>{tr('Đóng', 'Close')}</span>
              </button>
              <button
                type="submit"
                data-testid="sw-admin-cp-submit-btn"
                className="sw-focus-ring sw-btn sw-btn-pri sm"
              >
                <Check aria-hidden="true" className="h-4 w-4" />
                <span>{tr('Lưu mật khẩu mới', 'Save Password')}</span>
              </button>
            </div>
          </form>
        </div>
      )}
    </>
  );
};
