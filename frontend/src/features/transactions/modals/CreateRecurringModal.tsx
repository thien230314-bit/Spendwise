import React, { useState, useEffect } from 'react';
import { motion } from 'motion/react';
import { X, Calendar, Wallet, Tag, AlertCircle, RefreshCw } from 'lucide-react';
import { useI18n } from '../../../i18n/I18nContext';
import { useFinancial } from '../../../providers/FinancialProvider';
import { RecurringTransaction, RecurringFrequency, CategoryKind } from '../../../types';
import { getLocalizedWalletName, getLocalizedCategoryName, getLocalizedErrorMessage } from '../../../utils/localizationHelpers';
import { Button } from '../../../components/ui/Button';
import { Select } from '../../../components/ui/Select';
import {
  sanitizeIntegerInput,
  parseSafePositiveInteger,
  isValidDateString,
  isDateTodayOrFuture,
  getLocalDateString,
} from '../../../utils/inputValidation';

interface CreateRecurringModalProps {
  isOpen: boolean;
  onClose: () => void;
  editingItem?: RecurringTransaction | null;
}

export const CreateRecurringModal: React.FC<CreateRecurringModalProps> = ({
  isOpen,
  onClose,
  editingItem,
}) => {
  const { t, locale } = useI18n();
  const { wallets, categories, createRecurring, editRecurring } = useFinancial();

  // Pick first default wallet or first available wallet
  const defaultWalletId = wallets.find((w) => w.isDefault)?.id || wallets[0]?.id || '';

  const [title, setTitle] = useState('');
  const [type, setType] = useState<CategoryKind>('expense');
  const [walletId, setWalletId] = useState(defaultWalletId);
  const [categoryId, setCategoryId] = useState('');
  const [amount, setAmount] = useState('');
  const [frequency, setFrequency] = useState<RecurringFrequency>('monthly');
  const [nextRunDate, setNextRunDate] = useState(() => {
    const next = new Date();
    next.setDate(next.getDate() + 1);
    return next.toISOString().split('T')[0];
  });
  const [description, setDescription] = useState('');
  const [status, setStatus] = useState<'active' | 'paused'>('active');
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
    if (editingItem) {
      setTitle(editingItem.title);
      setType(editingItem.type);
      setWalletId(editingItem.walletId || defaultWalletId);
      setCategoryId(editingItem.categoryId);
      setAmount(editingItem.amount ? String(editingItem.amount) : '');
      setFrequency(editingItem.frequency);
      setNextRunDate(editingItem.nextRunDate);
      setDescription(editingItem.description || '');
      setStatus(editingItem.status);
    } else {
      setTitle('');
      setType('expense');
      setWalletId(defaultWalletId);
      const defaultCat = categories.find((c) => c.kind === 'expense')?.id || '';
      setCategoryId(defaultCat);
      setAmount('');
      setFrequency('monthly');
      const next = new Date();
      next.setDate(next.getDate() + 1);
      setNextRunDate(next.toISOString().split('T')[0]);
      setDescription('');
      setStatus('active');
    }
    setErrorMessage(null);
  }, [editingItem, isOpen, defaultWalletId, categories]);

  if (!isOpen) return null;

  // Filter categories by selected kind excluding archived
  const filteredCategories = categories.filter((c) => c.kind === type && !c.isArchived);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (isSubmitting) return;
    setErrorMessage(null);

    if (!title.trim()) {
      setErrorMessage(locale === 'en-US' ? 'Please enter a title.' : 'Vui lòng nhập tên giao dịch.');
      return;
    }

    const { valid: isAmountValid, value: numAmount } = parseSafePositiveInteger(amount);
    if (!isAmountValid || numAmount <= 0) {
      setErrorMessage(locale === 'en-US' ? 'Amount must be greater than 0.' : 'Số tiền phải lớn hơn 0.');
      return;
    }

    if (!walletId) {
      setErrorMessage(locale === 'en-US' ? 'Please select a wallet.' : 'Vui lòng chọn ví.');
      return;
    }

    if (!categoryId) {
      setErrorMessage(locale === 'en-US' ? 'Please select a category.' : 'Vui lòng chọn danh mục.');
      return;
    }

    if (!nextRunDate || !isValidDateString(nextRunDate)) {
      setErrorMessage(
        locale === 'en-US' ? 'Please select a valid next run date (YYYY-MM-DD).' : 'Vui lòng chọn ngày chạy tiếp theo hợp lệ (YYYY-MM-DD).'
      );
      return;
    }

    const isNextRunDateChanged = editingItem ? editingItem.nextRunDate !== nextRunDate : true;
    if (isNextRunDateChanged && !isDateTodayOrFuture(nextRunDate)) {
      setErrorMessage(
        locale === 'en-US' ? 'Next run date cannot be in the past.' : 'Ngày chạy tiếp theo không thể ở quá khứ.'
      );
      return;
    }

    const nextParts = nextRunDate.split('-').map(Number);
    const selectedDay = nextParts[2];
    const selectedMonth = nextParts[1];

    setIsSubmitting(true);
    try {
      if (editingItem) {
        const isDateChanged = editingItem.nextRunDate !== nextRunDate;
        const res = await editRecurring(editingItem.id, {
          title: title.trim(),
          type,
          walletId,
          categoryId,
          amount: numAmount,
          frequency,
          nextRunDate,
          scheduleAnchorDay: isDateChanged ? selectedDay : (editingItem.scheduleAnchorDay ?? selectedDay),
          scheduleAnchorMonth: isDateChanged ? selectedMonth : (editingItem.scheduleAnchorMonth ?? selectedMonth),
          description: description.trim() || undefined,
          status,
        });
        if (!res.success) {
          setErrorMessage(
            getLocalizedErrorMessage(res.error, locale, t)
          );
          return;
        }
      } else {
        const res = await createRecurring({
          title: title.trim(),
          type,
          walletId,
          categoryId,
          amount: numAmount,
          frequency,
          nextRunDate,
          scheduleAnchorDay: selectedDay,
          scheduleAnchorMonth: selectedMonth,
          description: description.trim() || undefined,
          status,
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
              <RefreshCw className="h-5 w-5 text-emerald-500" />
              <span>{editingItem ? t('recurring.editTitle') : t('recurring.createBtn')}</span>
            </h2>
            <p className="text-xs text-slate-500 dark:text-slate-400 mt-0.5">
              {t('recurring.subtitle')}
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

        {/* Form Body */}
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
          {/* Income vs Expense Selection */}
          <div>
            <label className="block text-xs font-semibold text-slate-700 dark:text-slate-300 mb-1.5">
              {t('categories.categoryKind')}
            </label>
            <div className="flex gap-2">
              <button
                type="button"
                onClick={() => {
                  setType('expense');
                  const expCat = categories.find((c) => c.kind === 'expense')?.id || '';
                  setCategoryId(expCat);
                }}
                className={`flex-1 py-2 text-xs font-semibold rounded-xl border transition-all ${
                  type === 'expense'
                    ? 'bg-rose-50 border-rose-500 text-rose-700 dark:bg-rose-950/40 dark:text-rose-300 ring-2 ring-rose-500/20'
                    : 'border-slate-200 dark:border-slate-700 text-slate-600 dark:text-slate-400'
                }`}
              >
                {t('categories.kindExpense')}
              </button>
              <button
                type="button"
                onClick={() => {
                  setType('income');
                  const incCat = categories.find((c) => c.kind === 'income')?.id || '';
                  setCategoryId(incCat);
                }}
                className={`flex-1 py-2 text-xs font-semibold rounded-xl border transition-all ${
                  type === 'income'
                    ? 'bg-emerald-50 border-emerald-500 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-300 ring-2 ring-emerald-500/20'
                    : 'border-slate-200 dark:border-slate-700 text-slate-600 dark:text-slate-400'
                }`}
              >
                {t('categories.kindIncome')}
              </button>
            </div>
          </div>

          {/* Title */}
          <div>
            <label className="block text-xs font-semibold text-slate-700 dark:text-slate-300 mb-1.5">
              {locale === 'en-US' ? 'Transaction Name / Title *' : 'Tên giao dịch định kỳ *'}
            </label>
            <input
              type="text"
              value={title}
              onChange={(e) => setTitle(e.target.value)}
              placeholder={
                locale === 'en-US'
                  ? 'e.g. Rent, Netflix, Monthly salary...'
                  : 'Ví dụ: Tiền thuê nhà, Netflix, Lương tháng...'
              }
              required
              className="w-full px-3.5 py-2.5 text-xs sm:text-sm rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 text-slate-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-emerald-500"
            />
          </div>

          {/* Amount & Frequency */}
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div>
              <label className="block text-xs font-semibold text-slate-700 dark:text-slate-300 mb-1.5">
                {t('transactions.amount')} (VND) *
              </label>
              <input
                type="text"
                inputMode="numeric"
                value={amount}
                onChange={(e) => setAmount(sanitizeIntegerInput(e.target.value))}
                placeholder={locale === 'en-US' ? 'e.g. 3500000' : 'Ví dụ: 3500000'}
                required
                className="w-full px-3.5 py-2.5 text-xs sm:text-sm rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 text-slate-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-emerald-500"
              />
            </div>

            <div>
              <label className="block text-xs font-semibold text-slate-700 dark:text-slate-300 mb-1.5">
                {t('recurring.frequency')}
              </label>
              <Select
                value={frequency}
                onChange={(val) => setFrequency(val as RecurringFrequency)}
                accentColor="emerald"
              >
                <option value="daily">{t('recurring.freqDaily')}</option>
                <option value="weekly">{t('recurring.freqWeekly')}</option>
                <option value="monthly">{t('recurring.freqMonthly')}</option>
                <option value="yearly">{t('recurring.freqYearly')}</option>
              </Select>
            </div>
          </div>

          {/* Wallet & Category */}
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div>
              <label className="block text-xs font-semibold text-slate-700 dark:text-slate-300 mb-1.5">
                {t('transactions.colWallet')} *
              </label>
              <Select
                value={walletId}
                onChange={(val) => setWalletId(val)}
                icon={<Wallet className="h-4 w-4 text-slate-400" />}
                accentColor="emerald"
              >
                {wallets.map((w) => (
                  <option key={w.id} value={w.id}>
                    {getLocalizedWalletName(w.id, w.name, locale)} ({w.currency})
                  </option>
                ))}
              </Select>
            </div>

            <div>
              <label className="block text-xs font-semibold text-slate-700 dark:text-slate-300 mb-1.5">
                {t('transactions.category')}
              </label>
              <Select
                value={categoryId}
                onChange={(val) => setCategoryId(val)}
                icon={<Tag className="h-4 w-4 text-slate-400" />}
                accentColor="emerald"
              >
                {filteredCategories.map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.key.startsWith('categories.') ? t(c.key) : (c.isSystem ? getLocalizedCategoryName(c.name, t, categories) : c.name)}
                  </option>
                ))}
              </Select>
            </div>
          </div>

          {/* Next Run Date & Status */}
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div>
              <label className="block text-xs font-semibold text-slate-700 dark:text-slate-300 mb-1.5">
                {t('recurring.nextRun')}
              </label>
              <div className="relative">
                <input
                  type="date"
                  min={editingItem ? undefined : getLocalDateString()}
                  value={nextRunDate}
                  onChange={(e) => setNextRunDate(e.target.value)}
                  required
                  className="w-full pl-9 pr-3.5 py-2.5 text-xs sm:text-sm rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 text-slate-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-emerald-500"
                />
                <Calendar className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-slate-400 pointer-events-none" />
              </div>
            </div>

            <div>
              <label className="block text-xs font-semibold text-slate-700 dark:text-slate-300 mb-1.5">
                {t('transactions.colStatus')}
              </label>
              <Select
                value={status}
                onChange={(val) => setStatus(val as 'active' | 'paused')}
                accentColor="emerald"
              >
                <option value="active">{t('recurring.statusActive')}</option>
                <option value="paused">{t('recurring.statusPaused')}</option>
              </Select>
            </div>
          </div>

          {/* Description */}
          <div>
            <label className="block text-xs font-semibold text-slate-700 dark:text-slate-300 mb-1.5">
              {t('transactions.note')}
            </label>
            <input
              type="text"
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              placeholder={
                locale === 'en-US'
                  ? 'Detailed note for recurring transaction...'
                  : 'Ghi chú chi tiết cho giao dịch định kỳ...'
              }
              className="w-full px-3.5 py-2.5 text-xs sm:text-sm rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 text-slate-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-emerald-500"
            />
          </div>

          {/* Footer Buttons */}
          <div className="flex justify-end gap-2 pt-4 border-t border-slate-100 dark:border-slate-800">
            <Button type="button" variant="outline" size="sm" disabled={isSubmitting} onClick={onClose}>
              {t('common.cancel')}
            </Button>
            <Button type="submit" size="sm" disabled={isSubmitting}>
              {isSubmitting
                ? (t('common.processing') || 'Đang xử lý...')
                : editingItem
                ? t('common.save')
                : t('recurring.createBtn')}
            </Button>
          </div>
        </form>
      </motion.div>
    </div>
  );
};
