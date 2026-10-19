import React, { useState, useMemo } from 'react';
import { useI18n } from '../../../i18n/I18nContext';
import { Group } from '../../../types/groups';
import { X, Users, Sparkles, AlertCircle, CheckCircle2, Info } from 'lucide-react';
import { parseSafePositiveInteger, sanitizeDigits } from '../../../utils/inputValidation';
import { useGroup } from '../../../providers/GroupProvider';
import { getLocalizedErrorMessage } from '../../../utils/localizationHelpers';

interface CreateFundRoundModalProps {
  isOpen: boolean;
  onClose: () => void;
  group: Group;
}

export const CreateFundRoundModal: React.FC<CreateFundRoundModalProps> = ({
  isOpen,
  onClose,
  group,
}) => {
  const { formatCurrency, locale, t } = useI18n();
  const { createFundRound } = useGroup();

  const [title, setTitle] = useState('');
  const [amountStr, setAmountStr] = useState('4000000');
  const [description, setDescription] = useState('');
  const [error, setError] = useState('');

  const activeMembers = useMemo(() => {
    return group.members.filter((m) => m.status === 'active');
  }, [group.members]);

  const amountCheck = parseSafePositiveInteger(amountStr);
  const targetAmount = amountCheck.valid ? amountCheck.value : 0;

  // Exact division simulation
  const allocation = useMemo(() => {
    const count = activeMembers.length;
    if (count === 0 || targetAmount <= 0) return { members: [], base: 0, remainder: 0 };

    const base = Math.floor(targetAmount / count);
    const remainder = targetAmount - base * count;
    const hasActiveOwner = activeMembers.some((m) => m.userId === group.ownerId);

    const members = activeMembers.map((m, idx) => {
      const isOwnerMember = hasActiveOwner ? m.userId === group.ownerId : idx === 0;
      const share = isOwnerMember ? base + remainder : base;
      return {
        userId: m.userId,
        name: m.name,
        avatarInitials: m.avatarInitials,
        isOwner: isOwnerMember,
        share,
      };
    });

    return { members, base, remainder };
  }, [activeMembers, targetAmount, group.ownerId]);

  if (!isOpen) return null;

  const quickPresets = [1000000, 2000000, 4000000, 8000000];

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');

    if (!title.trim()) {
      setError(
        locale === 'vi-VN'
          ? 'Vui lòng nhập tên đợt góp quỹ (ví dụ: Chi phí đi biển)'
          : 'Please enter a fund round title (e.g. Beach Trip Expenses)'
      );
      return;
    }

    if (!amountCheck.valid || targetAmount <= 0) {
      setError(
        locale === 'vi-VN'
          ? 'Tổng số tiền cần thu phải là số nguyên dương hợp lệ (> 0 ₫)'
          : 'Target amount must be a valid positive integer (> 0 ₫)'
      );
      return;
    }

    if (activeMembers.length === 0) {
      setError(
        locale === 'vi-VN'
          ? 'Nhóm hiện chưa có thành viên hoạt động để đóng quỹ'
          : 'Group has no active members to contribute to fund'
      );
      return;
    }

    const res = await createFundRound({
      groupId: group.id,
      title: title.trim(),
      targetAmount,
      description: description.trim() || undefined,
    });

    if (!res.success) {
      setError(getLocalizedErrorMessage(res.error, locale, t));
      return;
    }

    onClose();
  };

  return (
    <div
      id="modal-create-fund-round-backdrop"
      className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-sm animate-in fade-in duration-200"
    >
      <div
        id="modal-create-fund-round-container"
        className="w-full max-w-lg bg-white dark:bg-slate-900 rounded-2xl shadow-2xl border border-slate-200 dark:border-slate-800 overflow-hidden flex flex-col max-h-[90vh]"
      >
        {/* Header */}
        <div className="flex items-center justify-between px-6 py-4 border-b border-slate-200 dark:border-slate-800 bg-emerald-50/50 dark:bg-emerald-950/20">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-xl bg-emerald-600 dark:bg-emerald-500 flex items-center justify-center text-white shadow-sm">
              <Sparkles className="h-5 w-5" />
            </div>
            <div>
              <h2 className="text-base font-bold text-slate-900 dark:text-white">
                {locale === 'vi-VN' ? 'Đợt đóng quỹ nhóm' : 'Create Fund Round'}
              </h2>
              <p className="text-xs text-slate-500 dark:text-slate-400">
                {group.name} • {activeMembers.length} {locale === 'vi-VN' ? 'thành viên tham gia' : 'active members'}
              </p>
            </div>
          </div>
          <button
            id="btn-close-create-fund-round"
            onClick={onClose}
            className="p-1.5 text-slate-400 hover:text-slate-600 dark:hover:text-slate-200 hover:bg-slate-100 dark:hover:bg-slate-800 rounded-lg transition-colors"
          >
            <X className="h-5 w-5" />
          </button>
        </div>

        {/* Content */}
        <form
          onSubmit={handleSubmit}
          onKeyDown={(e) => {
            if (e.key === 'Enter' && (e.target as HTMLElement).tagName !== 'TEXTAREA') {
              e.preventDefault();
              handleSubmit(e);
            }
          }}
          className="flex-1 overflow-y-auto p-6 space-y-5"
        >
          {error && (
            <div
              id="create-fund-round-error"
              className="p-3 bg-rose-50 dark:bg-rose-950/30 border border-rose-200 dark:border-rose-900/50 rounded-xl text-xs text-rose-700 dark:text-rose-300 flex items-center gap-2"
            >
              <AlertCircle className="h-4 w-4 shrink-0" />
              <span>{error}</span>
            </div>
          )}

          {/* Round Title */}
          <div>
            <label className="block text-xs font-semibold text-slate-700 dark:text-slate-300 uppercase tracking-wider mb-1.5">
              {locale === 'vi-VN' ? 'Tên đợt góp quỹ' : 'Round Title'}{' '}
              <span className="text-rose-500">*</span>
            </label>
            <input
              id="input-fund-round-title"
              type="text"
              value={title}
              onChange={(e) => setTitle(e.target.value)}
              placeholder={locale === 'vi-VN' ? 'Ví dụ: Chi phí đi biển, Tiền phòng T9, Tiệc liên hoan...' : 'e.g. Beach Trip, September Rent...'}
              className="w-full px-3.5 py-2.5 bg-slate-50 dark:bg-slate-800/80 border border-slate-300 dark:border-slate-700 rounded-xl text-sm font-medium text-slate-900 dark:text-white placeholder-slate-400 focus:outline-none focus:ring-2 focus:ring-emerald-500"
              autoFocus
            />
          </div>

          {/* Target Amount */}
          <div>
            <label className="block text-xs font-semibold text-slate-700 dark:text-slate-300 uppercase tracking-wider mb-1.5">
              {locale === 'vi-VN' ? 'Tổng quỹ cần thu' : 'Total Target Amount'}{' '}
              <span className="text-rose-500">*</span>
            </label>
            <div className="relative">
              <input
                id="input-fund-round-target-amount"
                type="text"
                value={amountStr}
                onChange={(e) => setAmountStr(sanitizeDigits(e.target.value))}
                placeholder="4000000"
                className="w-full pl-3.5 pr-14 py-2.5 bg-slate-50 dark:bg-slate-800/80 border border-slate-300 dark:border-slate-700 rounded-xl text-base font-bold text-slate-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-emerald-500"
              />
              <span className="absolute right-3.5 top-1/2 -translate-y-1/2 text-xs font-semibold text-slate-500 dark:text-slate-400">
                VND
              </span>
            </div>

            {/* Quick Presets */}
            <div className="flex flex-wrap gap-2 mt-2">
              {quickPresets.map((val) => (
                <button
                  key={val}
                  type="button"
                  id={`btn-preset-fund-round-${val}`}
                  onClick={() => setAmountStr(val.toString())}
                  className={`text-xs px-2.5 py-1 rounded-lg border font-medium transition-colors ${
                    targetAmount === val
                      ? 'bg-emerald-50 dark:bg-emerald-950/40 text-emerald-700 dark:text-emerald-300 border-emerald-300 dark:border-emerald-800'
                      : 'bg-slate-50 dark:bg-slate-800 text-slate-600 dark:text-slate-400 border-slate-200 dark:border-slate-700 hover:bg-slate-100'
                  }`}
                >
                  {formatCurrency(val)}
                </button>
              ))}
            </div>
          </div>

          {/* Description */}
          <div>
            <label className="block text-xs font-semibold text-slate-700 dark:text-slate-300 uppercase tracking-wider mb-1.5">
              {locale === 'vi-VN' ? 'Mô tả / Dự toán (không bắt buộc)' : 'Description / Plan (Optional)'}
            </label>
            <textarea
              id="input-fund-round-desc"
              rows={2}
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              placeholder={locale === 'vi-VN' ? 'Dự kiến chi cho xe cộ, ăn uống và khách sạn...' : 'Planned expenses for transport, food, hotel...'}
              className="w-full px-3.5 py-2 bg-slate-50 dark:bg-slate-800/80 border border-slate-300 dark:border-slate-700 rounded-xl text-xs text-slate-900 dark:text-white placeholder-slate-400 focus:outline-none focus:ring-2 focus:ring-emerald-500 resize-none"
            />
          </div>

          {/* Live Snapshot Division Table */}
          {targetAmount > 0 && activeMembers.length > 0 && (
            <div className="rounded-xl border border-slate-200 dark:border-slate-800 bg-slate-50/70 dark:bg-slate-800/40 p-3.5 space-y-2.5">
              <div className="flex items-center justify-between">
                <span className="text-xs font-semibold text-slate-700 dark:text-slate-300 flex items-center gap-1.5">
                  <Users className="h-3.5 w-3.5 text-emerald-600 dark:text-emerald-400" />
                  {locale === 'vi-VN' ? 'Phân bổ đều cho từng thành viên' : 'Equal Contribution Breakdown'}
                </span>
                <span className="text-xs font-bold text-emerald-600 dark:text-emerald-400">
                  {formatCurrency(targetAmount)}
                </span>
              </div>

              <div className="divide-y divide-slate-200/80 dark:divide-slate-700/60 max-h-48 overflow-y-auto pr-1">
                {allocation.members.map((m) => (
                  <div key={m.userId} className="flex items-center justify-between py-2 text-xs">
                    <div className="flex items-center gap-2">
                      <div className="w-6 h-6 rounded-full bg-slate-200 dark:bg-slate-700 text-slate-700 dark:text-slate-200 font-bold flex items-center justify-center text-[10px]">
                        {m.avatarInitials}
                      </div>
                      <span className="text-slate-900 dark:text-slate-100 font-medium">
                        {m.name}
                        {m.isOwner && (
                          <span className="ml-1 text-[10px] text-emerald-600 dark:text-emerald-400 font-normal">
                            ({locale === 'vi-VN' ? 'Chủ nhóm' : 'Owner'})
                          </span>
                        )}
                      </span>
                    </div>
                    <span className="font-semibold text-slate-800 dark:text-slate-200">
                      {formatCurrency(m.share)}
                    </span>
                  </div>
                ))}
              </div>

              {allocation.remainder > 0 && (
                <p className="text-[11px] text-slate-500 dark:text-slate-400 italic">
                  * {locale === 'vi-VN'
                    ? `Số tiền lẻ ${formatCurrency(allocation.remainder)} được phân bổ minh bạch vào phần của Chủ nhóm.`
                    : `Odd amount ${formatCurrency(allocation.remainder)} is transparently allocated to Owner's share.`}
                </p>
              )}
            </div>
          )}

          {/* Model Invariant Notice */}
          <div className="rounded-xl border border-blue-200 dark:border-blue-900/50 bg-blue-50/60 dark:bg-blue-950/20 p-3 flex gap-2.5 text-xs text-blue-800 dark:text-blue-300">
            <Info className="h-4 w-4 shrink-0 mt-0.5 text-blue-600 dark:text-blue-400" />
            <p className="leading-relaxed">
              {locale === 'vi-VN'
                ? 'Khi tạo đợt góp quỹ, số dư quỹ nhóm và ví cá nhân sẽ KHÔNG thay đổi. Quỹ nhóm sẽ chỉ tăng lên khi từng thành viên thực tế nộp phần của mình.'
                : 'Creating a fund round does NOT immediately alter group or wallet balances. Group fund increases only as members actually pay their shares.'}
            </p>
          </div>

          {/* Actions */}
          <div className="flex items-center justify-end gap-3 pt-2">
            <button
              type="button"
              id="btn-cancel-create-fund-round"
              onClick={onClose}
              className="px-4 py-2 text-xs font-semibold text-slate-600 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800 rounded-xl transition-colors"
            >
              {locale === 'vi-VN' ? 'Hủy' : 'Cancel'}
            </button>
            <button
              type="submit"
              id="btn-submit-create-fund-round"
              className="px-5 py-2.5 text-xs font-bold text-white bg-emerald-600 hover:bg-emerald-700 dark:bg-emerald-500 dark:hover:bg-emerald-600 rounded-xl shadow-sm transition-colors flex items-center gap-1.5"
            >
              <CheckCircle2 className="h-4 w-4" />
              {locale === 'vi-VN' ? 'Tạo đợt đóng quỹ' : 'Create Fund Round'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};
