import React, { useState, useEffect } from 'react';
import { useI18n } from '../../../i18n/I18nContext';
import { useAuth } from '../../../providers/AuthProvider';
import { friendService } from '../../../services/friendService';
import { getLocalizedErrorMessage } from '../../../utils/localizationHelpers';
import { X, UserPlus, Send, CheckCircle2, AlertCircle } from 'lucide-react';

export interface AddFriendTarget {
  id?: string;
  name: string;
  contact?: string;
  spendId?: string | null;
  email?: string | null;
  phone?: string | null;
  avatarInitials?: string;
}

interface AddFriendModalProps {
  isOpen: boolean;
  onClose: () => void;
  targetUser: AddFriendTarget | null;
  onSuccess?: () => void;
}

export const AddFriendModal: React.FC<AddFriendModalProps> = ({
  isOpen,
  onClose,
  targetUser,
  onSuccess,
}) => {
  const { t, locale } = useI18n();
  const { currentUser } = useAuth();

  const [message, setMessage] = useState('');
  const [error, setError] = useState('');
  const [isSuccess, setIsSuccess] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const defaultMessage =
    locale === 'vi-VN'
      ? 'Chào bạn, hãy kết bạn với nhau cùng tạo nhóm chi tiêu nào.'
      : "Hi! Let's connect and create shared spending groups together.";

  useEffect(() => {
    if (isOpen) {
      setMessage(defaultMessage);
      setError('');
      setIsSuccess(false);
      setIsSubmitting(false);
    }
  }, [isOpen, targetUser, defaultMessage]);

  if (!isOpen || !targetUser) return null;

  const charCount = message.length;
  const maxChars = 300;

  const handleSend = async () => {
    if (!currentUser) {
      setError(locale === 'vi-VN' ? 'Vui lòng đăng nhập.' : 'Please log in.');
      return;
    }

    const trimmed = message.trim();
    if (!trimmed) {
      setError(
        locale === 'vi-VN'
          ? 'Vui lòng nhập lời nhắn kết bạn (bắt buộc).'
          : 'Please enter a message (required).'
      );
      return;
    }

    if (trimmed.length > maxChars) {
      setError(
        locale === 'vi-VN'
          ? `Lời nhắn không được vượt quá ${maxChars} ký tự.`
          : `Message must not exceed ${maxChars} characters.`
      );
      return;
    }

    const contactToUse = targetUser.contact || targetUser.phone || targetUser.email || '';
    if (!contactToUse) {
      setError(locale === 'vi-VN' ? 'Thiếu thông tin liên hệ (email hoặc số điện thoại).' : 'Contact info (email or phone) is required.');
      return;
    }

    setIsSubmitting(true);
    setError('');

    const res = await friendService.sendFriendRequestAsync(contactToUse, trimmed);

    setIsSubmitting(false);

    if (!res.success) {
      setError(getLocalizedErrorMessage(res.error, locale, t));
    } else {
      setIsSuccess(true);
      if (onSuccess) onSuccess();
      setTimeout(() => {
        onClose();
      }, 1400);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-xs animate-in fade-in-0">
      <div className="bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-2xl w-full max-w-md shadow-2xl overflow-hidden animate-in zoom-in-95">
        {/* Header */}
        <div className="flex items-center justify-between px-6 py-4 border-b border-slate-100 dark:border-slate-800 bg-slate-50/50 dark:bg-slate-950/40">
          <div className="flex items-center gap-2.5">
            <div className="p-2 rounded-xl bg-emerald-50 dark:bg-emerald-950/80 text-emerald-600 dark:text-emerald-400">
              <UserPlus className="h-5 w-5" />
            </div>
            <div>
              <h2 className="text-base font-bold text-slate-900 dark:text-slate-100">
                {locale === 'vi-VN' ? 'Gửi lời mời kết bạn' : 'Send Friend Request'}
              </h2>
              <p className="text-xs text-slate-500 dark:text-slate-400">
                {locale === 'vi-VN' ? 'Kết nối bạn bè trên SpendWise' : 'Connect with friends on SpendWise'}
              </p>
            </div>
          </div>
          <button
            type="button"
            disabled={isSubmitting}
            onClick={onClose}
            className="p-1.5 rounded-lg text-slate-400 hover:text-slate-600 dark:hover:text-slate-200 hover:bg-slate-100 dark:hover:bg-slate-800 transition-colors cursor-pointer disabled:opacity-50 disabled:cursor-not-allowed"
          >
            <X className="h-5 w-5" />
          </button>
        </div>

        {/* Content */}
        <div className="p-6 space-y-4">
          {isSuccess ? (
            <div className="text-center py-6 space-y-3">
              <div className="h-12 w-12 rounded-full bg-emerald-100 dark:bg-emerald-950/60 text-emerald-600 dark:text-emerald-400 mx-auto flex items-center justify-center">
                <CheckCircle2 className="h-6 w-6" />
              </div>
              <h3 className="text-base font-bold text-slate-900 dark:text-white">
                {locale === 'vi-VN' ? 'Đã gửi lời mời kết bạn!' : 'Friend Request Sent!'}
              </h3>
              <p className="text-xs text-slate-500 dark:text-slate-400">
                {locale === 'vi-VN'
                  ? `Lời mời kèm tin nhắn của bạn đã được gửi đến ${targetUser.name}.`
                  : `Your request and message have been sent to ${targetUser.name}.`}
              </p>
            </div>
          ) : (
            <>
              {/* Target User Card */}
              <div className="flex items-center gap-3 p-3.5 rounded-xl bg-slate-50 dark:bg-slate-800/60 border border-slate-200/80 dark:border-slate-700/80">
                <div className="w-11 h-11 rounded-full flex items-center justify-center shrink-0 bg-emerald-600 text-white font-bold text-sm shadow-xs">
                  {targetUser.avatarInitials || targetUser.name.slice(0, 2).toUpperCase()}
                </div>
                <div className="min-w-0 flex-1">
                  <div className="font-semibold text-sm text-slate-900 dark:text-white truncate">
                    {targetUser.name}
                  </div>
                  {targetUser.spendId && (
                    <div className="text-xs text-slate-500 dark:text-slate-400 font-mono">
                      @{targetUser.spendId}
                    </div>
                  )}
                  {targetUser.email && (
                    <div className="text-xs text-slate-500 dark:text-slate-400 truncate">
                      {targetUser.email}
                    </div>
                  )}
                  {!targetUser.spendId && !targetUser.email && targetUser.contact && (
                    <div className="text-xs text-slate-500 dark:text-slate-400 truncate">
                      {targetUser.contact}
                    </div>
                  )}
                  {targetUser.phone && (
                    <div className="text-[11px] font-mono text-slate-400 dark:text-slate-500 mt-0.5">
                      {targetUser.phone}
                    </div>
                  )}
                </div>
              </div>

              {/* Error message */}
              {error && (
                <div className="flex items-start gap-2 p-3 text-xs text-rose-700 dark:text-rose-400 bg-rose-50 dark:bg-rose-950/50 border border-rose-200 dark:border-rose-800 rounded-xl">
                  <AlertCircle className="h-4 w-4 shrink-0 mt-0.5" />
                  <span>{error}</span>
                </div>
              )}

              {/* Mandatory message */}
              <div>
                <div className="flex items-center justify-between mb-1.5">
                  <label className="text-xs font-semibold text-slate-700 dark:text-slate-300">
                    {locale === 'vi-VN' ? 'Lời nhắn kết bạn' : 'Friend Request Message'}
                    <span className="text-rose-500 ml-1">*</span>
                  </label>
                  <span
                    className={`text-[11px] font-mono ${
                      charCount > maxChars ? 'text-rose-600 font-bold' : 'text-slate-400'
                    }`}
                  >
                    {charCount}/{maxChars}
                  </span>
                </div>
                <textarea
                  rows={3}
                  value={message}
                  maxLength={maxChars}
                  disabled={isSubmitting}
                  onChange={(e) => {
                    setMessage(e.target.value);
                    if (error) setError('');
                  }}
                  placeholder={defaultMessage}
                  className="w-full px-3 py-2 text-xs rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-950 text-slate-900 dark:text-white placeholder:text-slate-400 focus:outline-none focus:ring-2 focus:ring-emerald-500 resize-none disabled:opacity-60"
                />
              </div>

              {/* Actions */}
              <div className="flex items-center justify-end gap-2.5 pt-2">
                <button
                  type="button"
                  disabled={isSubmitting}
                  onClick={onClose}
                  className="px-4 py-2 text-xs font-medium text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white transition-colors cursor-pointer disabled:opacity-50"
                >
                  {locale === 'vi-VN' ? 'Hủy' : 'Cancel'}
                </button>
                <button
                  type="button"
                  onClick={handleSend}
                  disabled={isSubmitting || !message.trim() || charCount > maxChars}
                  className="flex items-center gap-1.5 px-5 py-2.5 rounded-xl bg-emerald-600 hover:bg-emerald-700 disabled:opacity-50 disabled:cursor-not-allowed text-white text-xs font-semibold shadow-xs transition-all cursor-pointer"
                >
                  <Send className="h-3.5 w-3.5" />
                  <span>
                    {isSubmitting
                      ? (locale === 'vi-VN' ? 'Đang gửi...' : 'Sending...')
                      : (locale === 'vi-VN' ? 'Gửi lời mời' : 'Send Request')}
                  </span>
                </button>
              </div>
            </>
          )}
        </div>
      </div>
    </div>
  );
};
