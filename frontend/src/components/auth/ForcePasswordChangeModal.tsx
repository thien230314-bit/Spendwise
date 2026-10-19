import React, { useState, useId } from 'react';
import { useAuth } from '../../providers/AuthProvider';
import { useI18n } from '../../i18n/I18nContext';
import { Lock, Eye, EyeOff, Check, X, ShieldAlert } from 'lucide-react';

export const ForcePasswordChangeModal: React.FC = () => {
  const { currentUser, isAuthenticated, changePassword, refreshCurrentUser } = useAuth();
  const { locale } = useI18n();

  const currentPasswordId = useId();
  const newPasswordId = useId();
  const confirmPasswordId = useId();

  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [showCurrentPassword, setShowCurrentPassword] = useState(false);
  const [showNewPassword, setShowNewPassword] = useState(false);
  const [showConfirmPassword, setShowConfirmPassword] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  // If user does not require password change, render nothing
  if (!isAuthenticated || !currentUser || !currentUser.mustChangePassword) {
    return null;
  }

  // Password validation rules (Synced with Backend: 8-128, upper, lower, number; special is optional)
  const hasMinLength = newPassword.length >= 8 && newPassword.length <= 128;
  const hasUppercase = /[A-Z]/.test(newPassword);
  const hasLowercase = /[a-z]/.test(newPassword);
  const hasNumber = /\d/.test(newPassword);
  const hasSpecial = /[!@#$%^&*(),.?":{}|<>]/.test(newPassword);
  const passwordsMatch = newPassword.length > 0 && newPassword === confirmPassword;
  const notSameAsCurrent = currentPassword.length > 0 && newPassword !== currentPassword;

  const isFormValid =
    currentPassword.length > 0 &&
    hasMinLength &&
    hasUppercase &&
    hasLowercase &&
    hasNumber &&
    notSameAsCurrent &&
    passwordsMatch;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!isFormValid || isSubmitting) return;

    if (newPassword === currentPassword) {
      setErrorMessage(
        locale === 'vi-VN'
          ? 'Mật khẩu mới không được trùng với mật khẩu hiện tại.'
          : 'New password cannot be the same as current password.'
      );
      return;
    }

    if (!passwordsMatch) {
      setErrorMessage(
        locale === 'vi-VN' ? 'Mật khẩu xác nhận không khớp.' : 'Passwords do not match.'
      );
      return;
    }

    setIsSubmitting(true);
    setErrorMessage(null);

    try {
      await changePassword(currentPassword, newPassword);
      // Trust backend: refresh current profile to update mustChangePassword state
      await refreshCurrentUser();
    } catch (err: any) {
      setErrorMessage(
        err?.detail ||
          err?.title ||
          (locale === 'vi-VN'
            ? 'Đã xảy ra lỗi trong quá trình cập nhật mật khẩu. Vui lòng kiểm tra mật khẩu hiện tại.'
            : 'An error occurred while updating your password. Please verify current password.')
      );
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div
      id="force-password-change-overlay"
      className="fixed inset-0 z-9999 flex items-center justify-center p-4 bg-slate-950/80 backdrop-blur-md animate-in fade-in-0 duration-200"
    >
      <div className="w-full max-w-md rounded-2xl bg-white dark:bg-[#151D2F] border border-slate-200 dark:border-slate-800 p-6 shadow-2xl space-y-5 animate-in zoom-in-95 duration-200">
        {/* Header with security icon */}
        <div className="flex items-center gap-3 border-b border-slate-100 dark:border-slate-800 pb-4">
          <div className="w-11 h-11 rounded-xl bg-amber-500/10 text-amber-600 dark:text-amber-400 flex items-center justify-center shrink-0 border border-amber-500/20">
            <Lock className="w-6 h-6" />
          </div>
          <div>
            <div className="flex items-center gap-2">
              <h2 className="text-base font-bold text-slate-900 dark:text-white">
                {locale === 'vi-VN' ? 'Đổi mật khẩu bắt buộc' : 'Password Change Required'}
              </h2>
            </div>
            <p className="text-[11px] text-slate-500 dark:text-slate-400 mt-0.5">
              {currentUser.name} ({currentUser.email})
            </p>
          </div>
        </div>

        {/* Informational banner - No skip allowed */}
        <div className="p-3.5 rounded-xl bg-amber-50 dark:bg-amber-950/40 border border-amber-200 dark:border-amber-900/40 text-xs text-amber-800 dark:text-amber-300 space-y-1.5">
          <div className="flex items-center gap-2 font-bold text-xs text-amber-900 dark:text-amber-200">
            <ShieldAlert className="w-4 h-4 shrink-0 text-amber-600 dark:text-amber-400" />
            <span>
              {locale === 'vi-VN' ? 'Yêu cầu bảo mật tài khoản' : 'Account Security Requirement'}
            </span>
          </div>
          <p className="text-[11px] leading-relaxed text-amber-800/90 dark:text-amber-300/90">
            {locale === 'vi-VN'
              ? 'Tài khoản của bạn đang có yêu cầu đổi mật khẩu bắt buộc. Vì lý do an toàn, bạn phải thiết lập mật khẩu mới ngay để tiếp tục sử dụng hệ thống. Bạn không thể bỏ qua bước này.'
              : 'Your account requires an immediate password update. For your security, you must set a new password to continue. This step cannot be skipped.'}
          </p>
        </div>

        {/* Password Form */}
        <form onSubmit={handleSubmit} className="space-y-4">
          {/* Current Password Input */}
          <div className="space-y-1.5">
            <label
              htmlFor={currentPasswordId}
              className="block text-xs font-semibold text-slate-700 dark:text-slate-300"
            >
              {locale === 'vi-VN' ? 'Mật khẩu hiện tại' : 'Current Password'}{' '}
              <span className="text-rose-500">*</span>
            </label>
            <div className="relative">
              <input
                id={currentPasswordId}
                type={showCurrentPassword ? 'text' : 'password'}
                value={currentPassword}
                onChange={(e) => {
                  setCurrentPassword(e.target.value);
                  if (errorMessage) setErrorMessage(null);
                }}
                placeholder={locale === 'vi-VN' ? 'Nhập mật khẩu hiện tại...' : 'Enter current password...'}
                className="w-full h-10 px-3.5 pr-10 text-xs rounded-xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-900 text-slate-900 dark:text-white placeholder:text-slate-400 focus:outline-hidden focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500"
                required
                autoFocus
              />
              <button
                type="button"
                onClick={() => setShowCurrentPassword(!showCurrentPassword)}
                className="absolute right-3 top-1/2 -translate-y-1/2 text-slate-400 hover:text-slate-600 dark:hover:text-slate-200 cursor-pointer"
              >
                {showCurrentPassword ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
              </button>
            </div>
          </div>

          {/* New Password Input */}
          <div className="space-y-1.5">
            <label
              htmlFor={newPasswordId}
              className="block text-xs font-semibold text-slate-700 dark:text-slate-300"
            >
              {locale === 'vi-VN' ? 'Mật khẩu mới' : 'New Password'}{' '}
              <span className="text-rose-500">*</span>
            </label>
            <div className="relative">
              <input
                id={newPasswordId}
                type={showNewPassword ? 'text' : 'password'}
                value={newPassword}
                onChange={(e) => {
                  setNewPassword(e.target.value);
                  if (errorMessage) setErrorMessage(null);
                }}
                placeholder={locale === 'vi-VN' ? 'Nhập mật khẩu mới...' : 'Enter new password...'}
                className="w-full h-10 px-3.5 pr-10 text-xs rounded-xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-900 text-slate-900 dark:text-white placeholder:text-slate-400 focus:outline-hidden focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500"
                required
              />
              <button
                type="button"
                onClick={() => setShowNewPassword(!showNewPassword)}
                className="absolute right-3 top-1/2 -translate-y-1/2 text-slate-400 hover:text-slate-600 dark:hover:text-slate-200 cursor-pointer"
              >
                {showNewPassword ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
              </button>
            </div>
          </div>

          {/* Confirm Password Input */}
          <div className="space-y-1.5">
            <label
              htmlFor={confirmPasswordId}
              className="block text-xs font-semibold text-slate-700 dark:text-slate-300"
            >
              {locale === 'vi-VN' ? 'Xác nhận mật khẩu mới' : 'Confirm New Password'}{' '}
              <span className="text-rose-500">*</span>
            </label>
            <div className="relative">
              <input
                id={confirmPasswordId}
                type={showConfirmPassword ? 'text' : 'password'}
                value={confirmPassword}
                onChange={(e) => {
                  setConfirmPassword(e.target.value);
                  if (errorMessage) setErrorMessage(null);
                }}
                placeholder={
                  locale === 'vi-VN' ? 'Nhập lại mật khẩu mới...' : 'Re-enter new password...'
                }
                className="w-full h-10 px-3.5 pr-10 text-xs rounded-xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-900 text-slate-900 dark:text-white placeholder:text-slate-400 focus:outline-hidden focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500"
                required
              />
              <button
                type="button"
                onClick={() => setShowConfirmPassword(!showConfirmPassword)}
                className="absolute right-3 top-1/2 -translate-y-1/2 text-slate-400 hover:text-slate-600 dark:hover:text-slate-200 cursor-pointer"
              >
                {showConfirmPassword ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
              </button>
            </div>
          </div>

          {/* Requirement checklist */}
          <div className="p-3 rounded-xl bg-slate-50 dark:bg-slate-900/60 border border-slate-200 dark:border-slate-800 space-y-1.5 text-[11px]">
            <span className="font-semibold text-slate-600 dark:text-slate-400 block mb-1">
              {locale === 'vi-VN' ? 'Yêu cầu mật khẩu:' : 'Password requirements:'}
            </span>
            <div className="grid grid-cols-2 gap-1.5">
              <div className={`flex items-center gap-1.5 ${hasMinLength ? 'text-emerald-600 dark:text-emerald-400 font-medium' : 'text-slate-400'}`}>
                {hasMinLength ? <Check className="w-3.5 h-3.5" /> : <X className="w-3.5 h-3.5" />}
                <span>{locale === 'vi-VN' ? 'Ít nhất 8 ký tự' : 'At least 8 characters'}</span>
              </div>
              <div className={`flex items-center gap-1.5 ${hasUppercase && hasLowercase ? 'text-emerald-600 dark:text-emerald-400 font-medium' : 'text-slate-400'}`}>
                {hasUppercase && hasLowercase ? <Check className="w-3.5 h-3.5" /> : <X className="w-3.5 h-3.5" />}
                <span>{locale === 'vi-VN' ? 'Chữ hoa & thường' : 'Upper & lowercase'}</span>
              </div>
              <div className={`flex items-center gap-1.5 ${hasNumber ? 'text-emerald-600 dark:text-emerald-400 font-medium' : 'text-slate-400'}`}>
                {hasNumber ? <Check className="w-3.5 h-3.5" /> : <X className="w-3.5 h-3.5" />}
                <span>{locale === 'vi-VN' ? 'Chứa số (0-9)' : 'Contains number'}</span>
              </div>
              <div className={`flex items-center gap-1.5 ${hasSpecial ? 'text-emerald-600 dark:text-emerald-400 font-medium' : 'text-slate-400'}`}>
                {hasSpecial ? <Check className="w-3.5 h-3.5" /> : <span className="w-3.5 h-3.5 inline-block text-center text-[10px] text-slate-400">•</span>}
                <span>{locale === 'vi-VN' ? 'Ký tự đặc biệt (tùy chọn)' : 'Special char (optional)'}</span>
              </div>
              {newPassword && (
                <div className={`flex items-center gap-1.5 col-span-2 ${passwordsMatch ? 'text-emerald-600 dark:text-emerald-400 font-medium' : 'text-rose-500'}`}>
                  {passwordsMatch ? <Check className="w-3.5 h-3.5" /> : <X className="w-3.5 h-3.5" />}
                  <span>{locale === 'vi-VN' ? 'Hai mật khẩu trùng khớp' : 'Passwords match'}</span>
                </div>
              )}
            </div>
          </div>

          {/* Error Message */}
          {errorMessage && (
            <div className="p-2.5 rounded-xl bg-rose-50 dark:bg-rose-950/40 border border-rose-200 dark:border-rose-900/50 text-rose-600 dark:text-rose-400 text-xs flex items-center gap-2">
              <X className="w-4 h-4 shrink-0" />
              <span>{errorMessage}</span>
            </div>
          )}

          {/* Submit button - No cancel button */}
          <button
            type="submit"
            id="btn-force-password-change-submit"
            disabled={!isFormValid || isSubmitting}
            className="w-full py-3 px-4 rounded-xl text-xs font-bold text-white bg-emerald-600 hover:bg-emerald-500 disabled:opacity-40 disabled:hover:bg-emerald-600 disabled:cursor-not-allowed shadow-md shadow-emerald-900/20 transition-all cursor-pointer flex items-center justify-center gap-2"
          >
            {isSubmitting ? (
              <span>{locale === 'vi-VN' ? 'Đang cập nhật...' : 'Updating...'}</span>
            ) : (
              <span>
                {locale === 'vi-VN' ? 'Lưu mật khẩu & Tiếp tục' : 'Save Password & Continue'}
              </span>
            )}
          </button>
        </form>
      </div>
    </div>
  );
};
