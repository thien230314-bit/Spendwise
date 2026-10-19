import React, { useState } from 'react';
import { useI18n } from '../../../i18n/I18nContext';
import { Group } from '../../../types/groups';
import { X, Send, AlertCircle } from 'lucide-react';
import { cn } from '../../../utils/cn';
import { getLocalizedErrorMessage } from '../../../utils/localizationHelpers';
import {
  parseSafePositiveInteger,
  sanitizeDigits,
  isValidDateString,
  isDateTodayOrFuture,
  getFutureDateString,
  getLocalDateString,
} from '../../../utils/inputValidation';

interface FundRequestModalProps {
  isOpen: boolean;
  onClose: () => void;
  group: Group;
  onRequest: (params: {
    groupId: string;
    amountPerMember: number;
    purpose: string;
    dueDate: string;
    recipientUserIds: string[];
  }) => { success: boolean; error?: string };
}

export const FundRequestModal: React.FC<FundRequestModalProps> = ({
  isOpen,
  onClose,
  group,
  onRequest,
}) => {
  const { t, formatCurrency, locale } = useI18n();

  // Eligible recipients: active members only, excluding the Group Owner
  const eligibleMembers = group.members.filter(
    (m) => m.status === 'active' && m.userId !== group.ownerId
  );

  const [amountStr, setAmountStr] = useState('500000');
  const [purpose, setPurpose] = useState(
    locale === 'vi-VN' ? 'Đóng tiền điện & nước tháng 8' : 'August utilities fund request'
  );
  const [dueDate, setDueDate] = useState(() => getFutureDateString(7));
  const [selectedUserIds, setSelectedUserIds] = useState<string[]>(() =>
    eligibleMembers.map((m) => m.userId)
  );
  const [error, setError] = useState('');

  if (!isOpen) return null;

  const amountCheck = parseSafePositiveInteger(amountStr);
  const parsedAmount = amountCheck.valid ? amountCheck.value : 0;

  const toggleUser = (userId: string) => {
    if (selectedUserIds.includes(userId)) {
      if (selectedUserIds.length === 1) return; // Must keep at least one
      setSelectedUserIds((prev) => prev.filter((id) => id !== userId));
    } else {
      setSelectedUserIds((prev) => [...prev, userId]);
    }
  };

  const handleSelectAll = () => {
    if (selectedUserIds.length === eligibleMembers.length) {
      setSelectedUserIds(eligibleMembers[0] ? [eligibleMembers[0].userId] : []);
    } else {
      setSelectedUserIds(eligibleMembers.map((m) => m.userId));
    }
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (eligibleMembers.length === 0) {
      setError(
        locale === 'vi-VN'
          ? 'Nhóm chưa có thành viên hoạt động khác Chủ nhóm để gửi yêu cầu.'
          : 'No active members other than the Group Owner are available for this request.'
      );
      return;
    }
    if (!amountCheck.valid || amountCheck.value <= 0) {
      setError(
        locale === 'vi-VN'
          ? 'Số tiền yêu cầu phải là số nguyên dương hợp lệ (> 0 ₫)'
          : 'Amount must be a valid positive integer (> 0 ₫)'
      );
      return;
    }
    if (!purpose.trim()) {
      setError(locale === 'vi-VN' ? 'Vui lòng nhập mục đích thu quỹ' : 'Please enter purpose');
      return;
    }
    if (!dueDate || !isValidDateString(dueDate)) {
      setError(locale === 'vi-VN' ? 'Vui lòng chọn ngày đến hạn hợp lệ (YYYY-MM-DD)' : 'Please select a valid due date');
      return;
    }
    if (!isDateTodayOrFuture(dueDate)) {
      setError(locale === 'vi-VN' ? 'Hạn nộp quỹ không được ở quá khứ' : 'Due date cannot be in the past');
      return;
    }
    if (selectedUserIds.length === 0) {
      setError(locale === 'vi-VN' ? 'Vui lòng chọn ít nhất 1 thành viên' : 'Select at least 1 member');
      return;
    }

    const res = onRequest({
      groupId: group.id,
      amountPerMember: amountCheck.value,
      purpose: purpose.trim(),
      dueDate,
      recipientUserIds: selectedUserIds,
    });

    if (!res.success) {
      setError(getLocalizedErrorMessage(res.error, locale, t));
    } else {
      onClose();
      setError('');
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-sm animate-fadeIn">
      <div className="bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-2xl w-full max-w-lg shadow-2xl overflow-hidden animate-scaleUp">
        {/* Header */}
        <div className="flex items-center justify-between px-6 py-4 border-b border-slate-100 dark:border-slate-800 bg-slate-50/50 dark:bg-slate-950/40">
          <div className="flex items-center gap-2.5">
            <div className="p-2 rounded-xl bg-amber-50 dark:bg-amber-950/80 text-amber-600 dark:text-amber-400">
              <Send className="h-5 w-5" />
            </div>
            <div>
              <h2 className="text-lg font-bold text-slate-900 dark:text-slate-100">
                {t('groups.requestFundBtn')}
              </h2>
              <p className="text-xs text-slate-500 dark:text-slate-400">
                {group.name} • {locale === 'vi-VN' ? 'Gửi yêu cầu đóng quỹ đến các thành viên' : 'Send fund contribution request'}
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

        {/* Form */}
        <form
          onSubmit={handleSubmit}
          onKeyDown={(e) => {
            if (e.key === 'Enter' && (e.target as HTMLElement).tagName !== 'TEXTAREA') {
              e.preventDefault();
              handleSubmit(e);
            }
          }}
          className="p-6 space-y-4"
        >
          {error && (
            <div className="flex items-center gap-2 p-3 text-xs text-rose-700 dark:text-rose-400 bg-rose-50 dark:bg-rose-950/50 border border-rose-200 dark:border-rose-800 rounded-xl">
              <AlertCircle className="h-4 w-4 shrink-0" />
              <span>{error}</span>
            </div>
          )}

          {/* Amount per member */}
          <div>
            <label className="block text-xs font-semibold text-slate-700 dark:text-slate-300 mb-1.5">
              {t('groups.requestFundPerMember')} <span className="text-rose-500">*</span>
            </label>
            <div className="relative">
              <input
                type="text"
                inputMode="numeric"
                value={amountStr ? (amountCheck.valid ? amountCheck.value.toLocaleString('vi-VN') : amountStr) : ''}
                onChange={(e) => {
                  const num = sanitizeDigits(e.target.value);
                  setAmountStr(num);
                  if (error) setError('');
                }}
                placeholder="0"
                className="w-full px-3.5 py-2.5 rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-950 text-slate-900 dark:text-slate-100 text-base font-bold focus:outline-none focus:ring-2 focus:ring-amber-500 text-right pr-12"
              />
              <span className="absolute right-4 top-3 text-xs font-bold text-slate-400">₫</span>
            </div>
          </div>

          {/* Purpose */}
          <div>
            <label className="block text-xs font-semibold text-slate-700 dark:text-slate-300 mb-1.5">
              {t('groups.fundPurpose')} <span className="text-rose-500">*</span>
            </label>
            <input
              type="text"
              value={purpose}
              onChange={(e) => {
                setPurpose(e.target.value);
                if (error) setError('');
              }}
              placeholder={locale === 'vi-VN' ? 'VD: Đóng tiền điện tháng 8, Mua đồ gia dụng...' : 'e.g. Electricity bill...'}
              className="w-full px-3.5 py-2.5 rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-950 text-slate-900 dark:text-slate-100 text-sm focus:outline-none focus:ring-2 focus:ring-amber-500"
            />
          </div>

          {/* Due date */}
          <div>
            <label className="block text-xs font-semibold text-slate-700 dark:text-slate-300 mb-1.5">
              {t('groups.dueDate')}
            </label>
            <input
              type="date"
              min={getLocalDateString()}
              value={dueDate}
              onChange={(e) => {
                setDueDate(e.target.value);
                if (error) setError('');
              }}
              className="w-full px-3.5 py-2 rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-950 text-slate-900 dark:text-slate-100 text-xs focus:outline-none focus:ring-2 focus:ring-amber-500"
            />
          </div>

          {/* Recipient Member Selection */}
          <div>
            {eligibleMembers.length === 0 ? (
              <div className="p-4 rounded-xl bg-amber-50 dark:bg-amber-950/40 border border-amber-200 dark:border-amber-800/60 text-xs text-amber-800 dark:text-amber-300 flex items-start gap-2.5">
                <AlertCircle className="h-4 w-4 shrink-0 mt-0.5" />
                <span>
                  {locale === 'vi-VN'
                    ? 'Nhóm chưa có thành viên hoạt động khác Chủ nhóm để gửi yêu cầu.'
                    : 'No active members other than the Group Owner are available for this request.'}
                </span>
              </div>
            ) : (
              <>
                <div className="flex items-center justify-between mb-1.5">
                  <label className="text-xs font-semibold text-slate-700 dark:text-slate-300">
                    {t('groups.selectRecipients')} ({selectedUserIds.length}/{eligibleMembers.length})
                  </label>
                  <button
                    type="button"
                    onClick={handleSelectAll}
                    className="text-xs text-amber-600 dark:text-amber-400 hover:underline"
                  >
                    {selectedUserIds.length === eligibleMembers.length
                      ? (locale === 'vi-VN' ? 'Bỏ chọn tất cả' : 'Deselect all')
                      : (locale === 'vi-VN' ? 'Chọn tất cả' : 'Select all')}
                  </button>
                </div>

                <div className="space-y-1.5 max-h-40 overflow-y-auto pr-1">
                  {eligibleMembers.map((member) => {
                    const isSelected = selectedUserIds.includes(member.userId);
                    return (
                      <div
                        key={member.id}
                        onClick={() => toggleUser(member.userId)}
                        className={cn(
                          'flex items-center justify-between p-2.5 rounded-xl border text-xs cursor-pointer transition-all',
                          isSelected
                            ? 'bg-amber-50/70 dark:bg-amber-950/40 border-amber-300 dark:border-amber-800 text-slate-900 dark:text-slate-100'
                            : 'border-slate-200 dark:border-slate-800 text-slate-500 dark:text-slate-400 bg-white dark:bg-slate-950'
                        )}
                      >
                        <div className="flex items-center gap-2.5">
                          <input
                            type="checkbox"
                            checked={isSelected}
                            onChange={() => {}}
                            className="rounded text-amber-600 focus:ring-amber-500 h-4 w-4"
                          />
                          <span className="font-semibold">{member.name}</span>
                          {(member.email || member.phone) && (
                            <span className="text-[11px] text-slate-400 font-mono">
                              {member.email || member.phone}
                            </span>
                          )}
                        </div>
                        <span className="font-bold text-amber-700 dark:text-amber-300 font-mono">
                          {formatCurrency(parsedAmount)}
                        </span>
                      </div>
                    );
                  })}
                </div>
              </>
            )}
          </div>

          {/* Expected Total */}
          <div className="p-3 bg-slate-50 dark:bg-slate-950 rounded-xl border border-slate-200 dark:border-slate-800 flex items-center justify-between text-xs">
            <span className="text-slate-500 dark:text-slate-400">
              {locale === 'vi-VN' ? 'Dự kiến thu về quỹ:' : 'Expected Fund Total:'}
            </span>
            <span className="text-base font-bold text-emerald-600 dark:text-emerald-400">
              {formatCurrency(parsedAmount * selectedUserIds.length)}
            </span>
          </div>

          {/* Footer */}
          <div className="flex items-center justify-end gap-3 pt-2">
            <button
              type="button"
              onClick={onClose}
              className="px-4 py-2 text-xs font-medium text-slate-600 dark:text-slate-400 hover:text-slate-900 transition-colors"
            >
              {locale === 'vi-VN' ? 'Hủy' : 'Cancel'}
            </button>
            <button
              type="submit"
              disabled={eligibleMembers.length === 0 || selectedUserIds.length === 0}
              className={cn(
                "px-5 py-2.5 rounded-xl text-xs font-semibold shadow-sm transition-all flex items-center gap-1.5",
                eligibleMembers.length === 0 || selectedUserIds.length === 0
                  ? "bg-slate-200 dark:bg-slate-800 text-slate-400 dark:text-slate-600 cursor-not-allowed"
                  : "bg-amber-600 hover:bg-amber-700 text-white"
              )}
            >
              <Send className="h-4 w-4" />
              <span>{locale === 'vi-VN' ? 'Gửi yêu cầu' : 'Send Request'}</span>
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};
