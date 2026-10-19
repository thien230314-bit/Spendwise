import React, { useState, useEffect } from 'react';
import { motion } from 'motion/react';
import { X, Sliders, Tag, AlertCircle, Sparkles, Hash } from 'lucide-react';
import { useI18n } from '../../../i18n/I18nContext';
import { useFinancial } from '../../../providers/FinancialProvider';
import { TransactionRule } from '../../../types';
import { getLocalizedCategoryName, getLocalizedErrorMessage } from '../../../utils/localizationHelpers';
import { Button } from '../../../components/ui/Button';
import { Select } from '../../../components/ui/Select';
import { sanitizeIntegerInput, parseSafePositiveInteger } from '../../../utils/inputValidation';

interface CreateRuleModalProps {
  isOpen: boolean;
  onClose: () => void;
  editingRule?: TransactionRule | null;
}

export const CreateRuleModal: React.FC<CreateRuleModalProps> = ({
  isOpen,
  onClose,
  editingRule,
}) => {
  const { t, locale } = useI18n();
  const { categories, createRule, editRule } = useFinancial();

  const [descriptionPattern, setDescriptionPattern] = useState('');
  const [targetCategoryId, setTargetCategoryId] = useState('');
  const [priority, setPriority] = useState<string>('1');
  const [isActive, setIsActive] = useState(true);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  // Escape key listener
  useEffect(() => {
    if (!isOpen) return;
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape' && !isSubmitting) {
        onClose();
      }
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [isOpen, isSubmitting, onClose]);

  useEffect(() => {
    if (editingRule) {
      setDescriptionPattern(editingRule.descriptionPattern);
      setTargetCategoryId(editingRule.targetCategoryId);
      setPriority(String(editingRule.priority));
      setIsActive(editingRule.isActive);
    } else {
      setDescriptionPattern('');
      setTargetCategoryId(categories[0]?.id || '');
      setPriority('1');
      setIsActive(true);
    }
    setErrorMessage(null);
  }, [editingRule, isOpen, categories]);

  if (!isOpen) return null;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (isSubmitting) return;
    setErrorMessage(null);

    if (!descriptionPattern.trim()) {
      setErrorMessage(
        locale === 'en-US'
          ? 'Please enter a description pattern.'
          : 'Vui lòng nhập mẫu mô tả giao dịch.'
      );
      return;
    }

    if (!targetCategoryId) {
      setErrorMessage(
        locale === 'en-US'
          ? 'Please select a target category.'
          : 'Vui lòng chọn danh mục áp dụng.'
      );
      return;
    }

    const { valid: isPriorityValid, value: priorityNum } = parseSafePositiveInteger(priority);
    if (!isPriorityValid || priorityNum < 1 || priorityNum > 100) {
      setErrorMessage(
        locale === 'en-US'
          ? 'Priority must be a number between 1 and 100.'
          : 'Mức độ ưu tiên phải là số nguyên từ 1 đến 100.'
      );
      return;
    }

    setIsSubmitting(true);
    try {
      if (editingRule) {
        const res = await editRule(editingRule.id, {
          descriptionPattern: descriptionPattern.trim(),
          targetCategoryId,
          priority: priorityNum,
          isActive,
        });
        if (!res.success) {
          setErrorMessage(
            getLocalizedErrorMessage(res.error, locale, t)
          );
          return;
        }
      } else {
        const res = await createRule({
          descriptionPattern: descriptionPattern.trim(),
          targetCategoryId,
          priority: priorityNum,
          isActive,
        });
        if (!res.success) {
          setErrorMessage(
            getLocalizedErrorMessage(res.error, locale, t)
          );
          return;
        }
      }

      onClose();
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-xs">
      <motion.div
        initial={{ opacity: 0, scale: 0.96 }}
        animate={{ opacity: 1, scale: 1 }}
        exit={{ opacity: 0, scale: 0.96 }}
        className="bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-3xl w-full max-w-lg overflow-hidden shadow-2xl flex flex-col max-h-[90vh]"
      >
        {/* Header */}
        <div className="p-6 border-b border-slate-100 dark:border-slate-800 flex items-center justify-between">
          <div>
            <h2 className="text-base sm:text-lg font-bold text-slate-900 dark:text-white flex items-center gap-2">
              <Sliders className="h-5 w-5 text-emerald-500" />
              <span>{editingRule ? t('rules.editTitle') : t('rules.createBtn')}</span>
            </h2>
            <p className="text-xs text-slate-500 dark:text-slate-400 mt-0.5">
              {t('rules.subtitle')}
            </p>
          </div>
          <button
            type="button"
            onClick={onClose}
            className="p-2 text-slate-400 hover:text-slate-600 dark:hover:text-white rounded-xl hover:bg-slate-100 dark:hover:bg-slate-800 transition-colors"
          >
            <X className="h-5 w-5" />
          </button>
        </div>

        {/* Error Banner */}
        {errorMessage && (
          <div className="mx-6 mt-4 p-3 bg-rose-50 dark:bg-rose-950/30 border border-rose-200 dark:border-rose-900/50 rounded-2xl flex items-center gap-2.5 text-xs text-rose-700 dark:text-rose-300">
            <AlertCircle className="h-4 w-4 text-rose-600 shrink-0" />
            <span>{errorMessage}</span>
          </div>
        )}

        {/* Form */}
        <form
          onSubmit={handleSubmit}
          onKeyDown={(e) => {
            if (e.key === 'Enter' && (e.target as HTMLElement).tagName !== 'TEXTAREA') {
              e.preventDefault();
              handleSubmit(e);
            }
          }}
          className="p-6 overflow-y-auto space-y-4 flex-1"
        >
          {/* Description Pattern */}
          <div>
            <label className="block text-xs font-semibold text-slate-700 dark:text-slate-300 mb-1.5">
              {locale === 'en-US' ? 'Description Pattern (Contains) *' : 'Mẫu mô tả nhận diện (Chứa từ khóa) *'}
            </label>
            <input
              type="text"
              value={descriptionPattern}
              onChange={(e) => setDescriptionPattern(e.target.value)}
              placeholder={
                locale === 'en-US'
                  ? 'e.g. Grab, Shopee, Netflix, Circle K, Salary'
                  : 'Ví dụ: Grab, Shopee, Netflix, Circle K, Lương'
              }
              required
              className="w-full px-3.5 py-2.5 text-xs sm:text-sm rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 text-slate-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-emerald-500"
            />
            <p className="text-[11px] text-slate-400 mt-1">
              {locale === 'en-US'
                ? 'When a transaction description contains this keyword, the target category will be automatically applied.'
                : 'Khi nội dung giao dịch chứa từ khóa này, danh mục chỉ định sẽ được tự động gán.'}
            </p>
          </div>

          {/* Target Category */}
          <div>
            <label className="block text-xs font-semibold text-slate-700 dark:text-slate-300 mb-1.5">
              {locale === 'en-US' ? 'Target Category *' : 'Danh mục tự động gán *'}
            </label>
            <Select
              value={targetCategoryId}
              onChange={(val) => setTargetCategoryId(val)}
              icon={<Tag className="h-4 w-4 text-slate-400" />}
              accentColor="emerald"
            >
              {categories
                .filter((c) => !c.isArchived)
                .map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.key.startsWith('categories.') ? t(c.key) : (c.isSystem ? getLocalizedCategoryName(c.name, t, categories) : c.name)} ({c.kind === 'income' ? t('categories.kindIncome') : t('categories.kindExpense')})
                  </option>
                ))}
            </Select>
          </div>

          {/* Priority */}
          <div>
            <label className="block text-xs font-semibold text-slate-700 dark:text-slate-300 mb-1.5">
              {locale === 'en-US' ? 'Evaluation Priority (Higher evaluated first)' : 'Mức độ ưu tiên (Số càng cao xử lý trước)'}
            </label>
            <div className="relative">
              <input
                type="text"
                inputMode="numeric"
                value={priority}
                onChange={(e) => setPriority(sanitizeIntegerInput(e.target.value))}
                placeholder="1"
                className="w-full pl-9 pr-3.5 py-2.5 text-xs sm:text-sm rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 text-slate-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-emerald-500"
              />
              <Hash className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-slate-400 pointer-events-none" />
            </div>
          </div>

          {/* Active status */}
          <div className="pt-2">
            <label className="flex items-center gap-2.5 cursor-pointer">
              <input
                type="checkbox"
                checked={isActive}
                onChange={(e) => setIsActive(e.target.checked)}
                className="rounded border-slate-300 text-emerald-600 focus:ring-emerald-500 h-4 w-4"
              />
              <span className="text-xs font-semibold text-slate-700 dark:text-slate-300">
                {t('rules.activeStatus')}
              </span>
            </label>
          </div>

          {/* Footer Buttons */}
          <div className="flex justify-end gap-2 pt-4 border-t border-slate-100 dark:border-slate-800">
            <Button type="button" variant="outline" size="sm" disabled={isSubmitting} onClick={onClose}>
              {t('common.cancel')}
            </Button>
            <Button type="submit" size="sm" disabled={isSubmitting}>
              {isSubmitting
                ? (t('common.processing') || 'Đang xử lý...')
                : editingRule
                ? t('common.save')
                : t('rules.createBtn')}
            </Button>
          </div>
        </form>
      </motion.div>
    </div>
  );
};
