import React, { useState, useEffect } from 'react';
import { useI18n } from '../../../i18n/I18nContext';
import { friendService } from '../../../services/friendService';
import { FriendWithMeta } from '../../../types/friends';
import { getLocalizedErrorMessage } from '../../../utils/localizationHelpers';
import { X, Tag, AlertCircle } from 'lucide-react';

interface EditNicknameModalProps {
  isOpen: boolean;
  onClose: () => void;
  friend: FriendWithMeta | null;
  currentUserId: string;
  onSuccess?: () => void;
}

export const EditNicknameModal: React.FC<EditNicknameModalProps> = ({
  isOpen,
  onClose,
  friend,
  onSuccess,
}) => {
  const { t, locale } = useI18n();
  const [nickname, setNickname] = useState('');
  const [isSaving, setIsSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (isOpen && friend) {
      setNickname(friend.nickname || '');
      setIsSaving(false);
      setError(null);
    }
  }, [isOpen, friend]);

  if (!isOpen || !friend) return null;

  const handleSave = async () => {
    setIsSaving(true);
    setError(null);
    const trimmed = nickname.trim();
    const res = await friendService.updatePreferenceAsync(friend.userId, {
      nickname: trimmed || null,
      isFavorite: friend.isFavorite,
    });
    setIsSaving(false);

    if (res.success) {
      if (onSuccess) onSuccess();
      onClose();
    } else {
      setError(getLocalizedErrorMessage(res.error, locale, t));
    }
  };

  const handleClear = async () => {
    setIsSaving(true);
    setError(null);
    const res = await friendService.updatePreferenceAsync(friend.userId, {
      nickname: null,
      isFavorite: friend.isFavorite,
    });
    setIsSaving(false);

    if (res.success) {
      if (onSuccess) onSuccess();
      onClose();
    } else {
      setError(getLocalizedErrorMessage(res.error, locale, t));
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-xs animate-in fade-in-0">
      <div className="bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-2xl w-full max-w-sm shadow-2xl overflow-hidden animate-in zoom-in-95">
        {/* Header */}
        <div className="flex items-center justify-between px-5 py-3.5 border-b border-slate-100 dark:border-slate-800 bg-slate-50/50 dark:bg-slate-950/40">
          <div className="flex items-center gap-2">
            <Tag className="h-4 w-4 text-emerald-600 dark:text-emerald-400" />
            <h3 className="text-sm font-bold text-slate-900 dark:text-white">
              {locale === 'vi-VN' ? 'Đặt biệt danh riêng' : 'Set Private Nickname'}
            </h3>
          </div>
          <button
            type="button"
            disabled={isSaving}
            onClick={onClose}
            className="text-slate-400 hover:text-slate-600 dark:hover:text-slate-200 cursor-pointer p-1 disabled:opacity-50"
          >
            <X className="h-4 w-4" />
          </button>
        </div>

        {/* Content */}
        <div className="p-5 space-y-4">
          <p className="text-xs text-slate-500 dark:text-slate-400">
            {locale === 'vi-VN'
              ? `Biệt danh này là riêng tư, chỉ một mình bạn nhìn thấy đối với người bạn "${friend.name}".`
              : `This nickname is private and only visible to you for "${friend.name}".`}
          </p>

          {error && (
            <div className="flex items-start gap-2 p-3 text-xs text-rose-700 dark:text-rose-400 bg-rose-50 dark:bg-rose-950/50 border border-rose-200 dark:border-rose-800 rounded-xl">
              <AlertCircle className="h-4 w-4 shrink-0 mt-0.5" />
              <span>{error}</span>
            </div>
          )}

          <div>
            <label className="block text-xs font-semibold text-slate-700 dark:text-slate-300 mb-1.5">
              {locale === 'vi-VN' ? 'Biệt danh' : 'Nickname'}
            </label>
            <div className="relative">
              <input
                type="text"
                autoFocus
                maxLength={50}
                disabled={isSaving}
                placeholder={locale === 'vi-VN' ? 'Ví dụ: Bình đồng nghiệp, Mẹ, Anh Huy...' : 'E.g., Mom, Dave Colleague...'}
                value={nickname}
                onChange={(e) => setNickname(e.target.value)}
                onKeyDown={(e) => {
                  if (e.key === 'Enter') {
                    e.preventDefault();
                    handleSave();
                  }
                }}
                className="w-full px-3 py-2 text-xs rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-950 text-slate-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-emerald-500 disabled:opacity-60"
              />
              <span className="absolute right-2.5 top-2 text-[10px] text-slate-400 font-mono">
                {nickname.length}/50
              </span>
            </div>
          </div>

          <div className="flex items-center justify-between gap-2 pt-2">
            {friend.nickname ? (
              <button
                type="button"
                disabled={isSaving}
                onClick={handleClear}
                className="text-xs text-rose-600 hover:text-rose-700 font-medium cursor-pointer disabled:opacity-50"
              >
                {locale === 'vi-VN' ? 'Xóa biệt danh' : 'Remove Nickname'}
              </button>
            ) : (
              <div />
            )}

            <div className="flex items-center gap-2">
              <button
                type="button"
                disabled={isSaving}
                onClick={onClose}
                className="px-3.5 py-1.5 text-xs text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white cursor-pointer disabled:opacity-50"
              >
                {locale === 'vi-VN' ? 'Hủy' : 'Cancel'}
              </button>
              <button
                type="button"
                disabled={isSaving}
                onClick={handleSave}
                className="px-4 py-1.5 text-xs font-semibold bg-emerald-600 hover:bg-emerald-700 disabled:opacity-50 disabled:cursor-not-allowed text-white rounded-xl shadow-xs transition cursor-pointer"
              >
                {isSaving
                  ? (locale === 'vi-VN' ? 'Đang lưu...' : 'Saving...')
                  : (locale === 'vi-VN' ? 'Lưu' : 'Save')}
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};
