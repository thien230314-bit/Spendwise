import React, { useState, useEffect } from 'react';
import { motion } from 'motion/react';
import {
  X,
  Tag,
  AlertCircle,
  CheckCircle2,
  Sparkles,
  Sliders,
  ArrowDownLeft,
  ArrowUpRight,
  Info,
} from 'lucide-react';
import { useI18n } from '../../../i18n/I18nContext';
import { useFinancial } from '../../../providers/FinancialProvider';
import { Transaction } from '../../../types';
import { formatMoney } from '../../../utils/money';
import { formatTransactionTime } from '../../../utils/date';
import { getLocalizedCategoryName, getLocalizedWalletName, getLocalizedErrorMessage } from '../../../utils/localizationHelpers';
import { Button } from '../../../components/ui/Button';

interface ClassifyTransactionModalProps {
  isOpen: boolean;
  onClose: () => void;
  transaction: Transaction | null;
  onSuccess?: () => void;
}

export const ClassifyTransactionModal: React.FC<ClassifyTransactionModalProps> = ({
  isOpen,
  onClose,
  transaction,
  onSuccess,
}) => {
  const { t, locale } = useI18n();
  const { categories, classifyTransaction, createRule } = useFinancial();

  const [selectedCategoryKey, setSelectedCategoryKey] = useState('');
  const [saveAsRule, setSaveAsRule] = useState(false);
  const [ruleKeyword, setRuleKeyword] = useState('');
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  // Auto-init state when modal opens or transaction changes
  useEffect(() => {
    if (transaction) {
      // Pick existing category or match sensible default based on direction/type
      setSelectedCategoryKey(transaction.category || '');
      // Extract a clean keyword suggestion from title or counterparty
      const candidateKeyword = (transaction.counterparty || transaction.title || '')
        .trim()
        .replace(/^(nhan tien|chuyen tien|thanh toan|rut tien|nap tien)\s+/i, '');
      setRuleKeyword(candidateKeyword);
      setSaveAsRule(false);
      setErrorMessage(null);
    }
  }, [transaction, isOpen]);

  // Escape listener
  useEffect(() => {
    if (!isOpen) return;
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose();
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [isOpen, onClose]);

  if (!isOpen || !transaction) return null;

  const isCredit = transaction.direction === 'credit' || transaction.amount > 0;
  const isDebit = transaction.direction === 'debit' || transaction.amount < 0;

  // Filter categories matching transaction flow (income vs expense) excluding archived
  const relevantCategories = categories.filter((c) => {
    if (c.isArchived) return false;
    if (isCredit) return c.kind === 'income';
    if (isDebit) return c.kind === 'expense';
    return true;
  });

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedCategoryKey) {
      setErrorMessage(
        locale === 'vi-VN'
          ? 'Vui lòng chọn một danh mục chi tiêu/thu nhập phù hợp.'
          : 'Please select an appropriate category.'
      );
      return;
    }

    setIsSubmitting(true);
    setErrorMessage(null);

    const res = classifyTransaction({
      transactionId: transaction.id,
      categoryKey: selectedCategoryKey,
    });

    if (!res.success) {
      setErrorMessage(
        getLocalizedErrorMessage(res.error, locale, t)
      );
      setIsSubmitting(false);
      return;
    }

    // If user opted to create an automatic rule
    if (saveAsRule && ruleKeyword.trim()) {
      const targetCategory = categories.find(
        (c) => c.key === selectedCategoryKey || c.id === selectedCategoryKey
      );
      if (targetCategory) {
        createRule({
          descriptionPattern: ruleKeyword.trim(),
          targetCategoryId: targetCategory.id,
          priority: 5,
          isActive: true,
        });
      }
    }

    setIsSubmitting(false);
    if (onSuccess) onSuccess();
    onClose();
  };

  return (
    <div
      id="classify-transaction-modal-backdrop"
      className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-xs animate-in fade-in duration-150"
      onClick={onClose}
    >
      <motion.div
        id="classify-transaction-modal"
        initial={{ opacity: 0, scale: 0.96 }}
        animate={{ opacity: 1, scale: 1 }}
        exit={{ opacity: 0, scale: 0.96 }}
        transition={{ duration: 0.15 }}
        onClick={(e) => e.stopPropagation()}
        className="w-full max-w-lg bg-white dark:bg-slate-900 rounded-3xl p-6 sm:p-7 shadow-2xl border border-slate-100 dark:border-slate-800 space-y-5"
      >
        {/* Header */}
        <div className="flex items-start justify-between gap-4">
          <div className="flex items-center gap-3">
            <div className="h-10 w-10 rounded-2xl bg-amber-500/10 text-amber-600 dark:text-amber-400 flex items-center justify-center shrink-0">
              <Tag className="h-5 w-5" />
            </div>
            <div>
              <h3 className="text-base sm:text-lg font-bold text-slate-900 dark:text-white">
                {locale === 'vi-VN' ? 'Phân loại giao dịch' : 'Classify Transaction'}
              </h3>
              <p className="text-xs text-slate-500 dark:text-slate-400">
                {locale === 'vi-VN'
                  ? 'Gán danh mục chính xác để phục vụ báo cáo và phân tích tài chính'
                  : 'Assign the exact category to enhance analytics and financial tracking'}
              </p>
            </div>
          </div>
          <button
            type="button"
            id="btn-close-classify-modal"
            onClick={onClose}
            className="p-1.5 rounded-xl text-slate-400 hover:text-slate-600 dark:hover:text-slate-200 hover:bg-slate-100 dark:hover:bg-slate-800 transition-colors"
          >
            <X className="h-5 w-5" />
          </button>
        </div>

        {/* Transaction Summary Card */}
        <div className="p-4 rounded-2xl bg-slate-50 dark:bg-slate-800/60 border border-slate-200/60 dark:border-slate-700/60 space-y-2.5">
          <div className="flex items-start justify-between gap-3">
            <div className="space-y-0.5 min-w-0">
              {transaction.needsCategoryReview ? (
                <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-md bg-amber-100 dark:bg-amber-950/60 text-amber-700 dark:text-amber-300 text-[10px] font-bold">
                  <AlertCircle className="h-3 w-3" />
                  <span>{locale === 'vi-VN' ? 'Cần phân loại' : 'Needs Review'}</span>
                </span>
              ) : (
                <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-md bg-slate-100 dark:bg-slate-800 text-slate-700 dark:text-slate-300 text-[10px] font-bold">
                  <Tag className="h-3 w-3" />
                  <span>{locale === 'vi-VN' ? 'Đổi hạng mục' : 'Change category'}</span>
                </span>
              )}
              <p className="text-sm font-bold text-slate-900 dark:text-white truncate pt-1">
                {transaction.title}
              </p>
              {transaction.description && transaction.description !== transaction.title && (
                <p className="text-xs text-slate-500 dark:text-slate-400 line-clamp-1">
                  {transaction.description}
                </p>
              )}
            </div>

            <div className="text-right shrink-0">
              <span
                className={`text-base font-extrabold tabular-nums flex items-center justify-end gap-0.5 ${
                  isCredit
                    ? 'text-emerald-600 dark:text-emerald-400'
                    : 'text-slate-900 dark:text-slate-100'
                }`}
              >
                {isCredit ? (
                  <ArrowDownLeft className="h-4 w-4" />
                ) : (
                  <ArrowUpRight className="h-4 w-4" />
                )}
                {formatMoney(Math.abs(transaction.amount), locale, {
                  showSign: true,
                  forceSign: isCredit,
                })}
              </span>
              <p className="text-[11px] text-slate-400 tabular-nums">
                {formatTransactionTime(transaction.date, locale)}
              </p>
            </div>
          </div>

          <div className="pt-2 border-t border-slate-200/40 dark:border-slate-700/40 flex flex-wrap items-center justify-between text-[11px] text-slate-500 dark:text-slate-400">
            <span>
              {transaction.walletId ? (
                <>
                  {locale === 'vi-VN' ? 'Ví:' : 'Wallet:'}{' '}
                  <strong className="text-slate-700 dark:text-slate-200">
                    {getLocalizedWalletName(transaction.walletId, transaction.walletName, locale)}
                  </strong>
                </>
              ) : (
                <>
                  {locale === 'vi-VN' ? 'Nguồn:' : 'Source:'}{' '}
                  <strong className="text-slate-700 dark:text-slate-200">
                    {transaction.counterparty || (locale === 'vi-VN' ? 'Ngân hàng / SePay' : 'Bank / SePay')}
                  </strong>
                </>
              )}
            </span>
            {transaction.referenceCode && (
              <span>
                {locale === 'vi-VN' ? 'Mã:' : 'ID:'} <code className="text-slate-600 dark:text-slate-300">{transaction.referenceCode}</code>
              </span>
            )}
          </div>
        </div>

        {/* Error Notification */}
        {errorMessage && (
          <div className="p-3 rounded-2xl bg-rose-50 dark:bg-rose-950/40 border border-rose-200 dark:border-rose-800/60 flex items-center gap-2 text-xs font-semibold text-rose-600 dark:text-rose-400">
            <AlertCircle className="h-4 w-4 shrink-0" />
            <span>{errorMessage}</span>
          </div>
        )}

        {/* Form */}
        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="space-y-1.5">
            <label className="block text-xs font-bold text-slate-700 dark:text-slate-300">
              {locale === 'vi-VN' ? 'Chọn danh mục đúng' : 'Select Category'} *
            </label>
            <div className="grid grid-cols-2 gap-2 max-h-48 overflow-y-auto pr-1">
              {relevantCategories.map((c) => {
                const isSelected = selectedCategoryKey === c.key || selectedCategoryKey === c.id;
                return (
                  <button
                    type="button"
                    key={c.id}
                    onClick={() => setSelectedCategoryKey(c.key)}
                    className={`flex items-center gap-2 p-2.5 rounded-xl border text-xs font-bold transition-all text-left cursor-pointer ${
                      isSelected
                        ? 'border-emerald-500 bg-emerald-50/80 dark:bg-emerald-950/40 text-emerald-700 dark:text-emerald-300 shadow-2xs ring-1 ring-emerald-500'
                        : 'border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800/60 text-slate-700 dark:text-slate-300 hover:border-slate-300'
                    }`}
                  >
                    <Tag className={`h-3.5 w-3.5 shrink-0 ${isSelected ? 'text-emerald-500' : 'text-slate-400'}`} />
                    <span className="truncate">
                      {c.key.startsWith('categories.') ? t(c.key) : (c.isSystem ? getLocalizedCategoryName(c.name, t, categories) : c.name)}
                    </span>
                  </button>
                );
              })}
            </div>
          </div>

          {/* Rule Automation Option */}
          <div className="p-3.5 rounded-2xl bg-slate-50 dark:bg-slate-800/40 border border-slate-200/60 dark:border-slate-700/60 space-y-2.5">
            <label className="flex items-center gap-2.5 cursor-pointer text-xs font-semibold text-slate-700 dark:text-slate-200">
              <input
                type="checkbox"
                id="checkbox-save-rule"
                checked={saveAsRule}
                onChange={(e) => setSaveAsRule(e.target.checked)}
                className="rounded text-emerald-600 focus:ring-emerald-500 h-4 w-4"
              />
              <span className="flex items-center gap-1.5">
                <Sparkles className="h-3.5 w-3.5 text-amber-500" />
                {locale === 'vi-VN'
                  ? 'Ghi nhớ quy tắc này cho các giao dịch tương tự sau này'
                  : 'Remember this rule for future similar transactions'}
              </span>
            </label>

            {saveAsRule && (
              <div className="pt-1.5 space-y-1 animate-in fade-in-50 duration-150">
                <label className="block text-[11px] font-medium text-slate-500 dark:text-slate-400">
                  {locale === 'vi-VN' ? 'Từ khóa nhận diện mô tả:' : 'Pattern keyword:'}
                </label>
                <input
                  type="text"
                  id="input-rule-keyword"
                  value={ruleKeyword}
                  onChange={(e) => setRuleKeyword(e.target.value)}
                  placeholder={locale === 'vi-VN' ? 'Ví dụ: Highlands, Grab...' : 'e.g. Highlands, Grab...'}
                  className="w-full px-3 py-1.5 text-xs rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-900 text-slate-900 dark:text-white focus:outline-none focus:ring-1 focus:ring-emerald-500"
                />
              </div>
            )}
          </div>

          {/* Actions */}
          <div className="flex items-center justify-end gap-2.5 pt-2">
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={onClose}
              className="text-xs font-semibold"
            >
              {t('common.cancel')}
            </Button>
            <Button
              type="submit"
              size="sm"
              disabled={isSubmitting || !selectedCategoryKey}
              className="text-xs bg-emerald-600 hover:bg-emerald-700 text-white font-semibold gap-1.5 shadow-2xs"
            >
              <CheckCircle2 className="h-4 w-4" />
              <span>
                {locale === 'vi-VN'
                  ? transaction.needsCategoryReview
                    ? 'Xác nhận phân loại'
                    : 'Cập nhật hạng mục'
                  : transaction.needsCategoryReview
                  ? 'Confirm Category'
                  : 'Update Category'}
              </span>
            </Button>
          </div>
        </form>
      </motion.div>
    </div>
  );
};
