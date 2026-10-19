import React, { useState, useEffect, useRef } from 'react';
import {
  Plus,
  Play,
  Pause,
  Edit2,
  Trash2,
  Calendar,
  AlertTriangle,
  RefreshCw,
  Wallet,
  TrendingDown,
  TrendingUp,
  Clock,
  CheckCircle2,
  AlertCircle,
} from 'lucide-react';
import { useI18n } from '../../../i18n/I18nContext';
import { useFinancial } from '../../../providers/FinancialProvider';
import { useAuth } from '../../../providers/AuthProvider';
import { RecurringTransaction } from '../../../types';
import { formatMoney } from '../../../utils/money';
import { getLocalizedCategoryName, getLocalizedWalletName, getLocalizedErrorMessage } from '../../../utils/localizationHelpers';
import { Button } from '../../../components/ui/Button';
import { CreateRecurringModal } from '../modals/CreateRecurringModal';
import { isRecurringDue } from '../../../services/recurringService';

interface RecurringTransactionsTabProps {
  focusRecurringId?: string | null;
  onRecordPayment?: (item: RecurringTransaction) => void;
}

export const RecurringTransactionsTab: React.FC<RecurringTransactionsTabProps> = ({
  focusRecurringId,
  onRecordPayment,
}) => {
  const { t, locale } = useI18n();
  const { currentUser } = useAuth();
  const { recurringTransactions, categories, toggleRecurringStatus, deleteRecurring } = useFinancial();

  const [isModalOpen, setIsModalOpen] = useState(false);
  const [editingItem, setEditingItem] = useState<RecurringTransaction | null>(null);
  const [deletingItem, setDeletingItem] = useState<RecurringTransaction | null>(null);
  const [isDeleting, setIsDeleting] = useState(false);
  const [deleteError, setDeleteError] = useState<string | null>(null);
  const [togglingId, setTogglingId] = useState<string | null>(null);
  const [toggleError, setToggleError] = useState<string | null>(null);

  const focusedRef = useRef<HTMLDivElement | null>(null);

  useEffect(() => {
    if (focusRecurringId && focusedRef.current) {
      focusedRef.current.scrollIntoView({ behavior: 'smooth', block: 'center' });
    }
  }, [focusRecurringId]);

  // Escape key listener for delete confirmation modal
  useEffect(() => {
    if (!deletingItem) return;
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape' && !isDeleting) {
        setDeletingItem(null);
        setDeleteError(null);
      }
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [deletingItem, isDeleting]);

  // Calculate summary stats
  const activeItems = recurringTransactions.filter((r) => r.status === 'active');
  const projectedMonthlyExpense = activeItems
    .filter((r) => r.type === 'expense')
    .reduce((sum, r) => {
      // Normalize to monthly approximation for overview
      let multiplier = 1;
      if (r.frequency === 'daily') multiplier = 30;
      if (r.frequency === 'weekly') multiplier = 4.3;
      if (r.frequency === 'yearly') multiplier = 1 / 12;
      return sum + r.amount * multiplier;
    }, 0);

  const projectedMonthlyIncome = activeItems
    .filter((r) => r.type === 'income')
    .reduce((sum, r) => {
      let multiplier = 1;
      if (r.frequency === 'daily') multiplier = 30;
      if (r.frequency === 'weekly') multiplier = 4.3;
      if (r.frequency === 'yearly') multiplier = 1 / 12;
      return sum + r.amount * multiplier;
    }, 0);

  const handleOpenCreate = () => {
    setEditingItem(null);
    setIsModalOpen(true);
  };

  const handleOpenEdit = (item: RecurringTransaction) => {
    setEditingItem(item);
    setIsModalOpen(true);
  };

  const handleConfirmDelete = async () => {
    if (!deletingItem || isDeleting) return;
    setIsDeleting(true);
    setDeleteError(null);
    try {
      const res = await deleteRecurring(deletingItem.id);
      if (res.success) {
        setDeletingItem(null);
      } else {
        setDeleteError(getLocalizedErrorMessage(res.error, locale, t));
      }
    } catch {
      setDeleteError(t('common.errorOccurred'));
    } finally {
      setIsDeleting(false);
    }
  };

  const handleToggleStatus = async (id: string) => {
    if (togglingId) return;
    setTogglingId(id);
    setToggleError(null);
    try {
      const res = await toggleRecurringStatus(id);
      if (!res.success) {
        setToggleError(getLocalizedErrorMessage(res.error, locale, t));
      }
    } catch {
      setToggleError(t('common.errorOccurred'));
    } finally {
      setTogglingId(null);
    }
  };

  const getFrequencyLabel = (freq: string) => {
    switch (freq) {
      case 'daily':
        return t('recurring.freqDaily');
      case 'weekly':
        return t('recurring.freqWeekly');
      case 'monthly':
        return t('recurring.freqMonthly');
      case 'yearly':
        return t('recurring.freqYearly');
      default:
        return freq;
    }
  };

  return (
    <div className="space-y-6">
      {/* Top Summary Bar */}
      <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
        <div className="p-4 bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-2xl flex items-center gap-3.5 shadow-2xs">
          <div className="h-10 w-10 rounded-xl bg-emerald-50 dark:bg-emerald-950/40 text-emerald-600 flex items-center justify-center shrink-0">
            <RefreshCw className="h-5 w-5" />
          </div>
          <div>
            <p className="text-xs text-slate-500 dark:text-slate-400 font-medium">
              {t('recurring.statusActive')}
            </p>
            <p className="text-lg font-bold text-slate-900 dark:text-white">
              {t('recurring.summaryActive', { count: activeItems.length })}
            </p>
          </div>
        </div>

        <div className="p-4 bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-2xl flex items-center gap-3.5 shadow-2xs">
          <div className="h-10 w-10 rounded-xl bg-rose-50 dark:bg-rose-950/40 text-rose-600 flex items-center justify-center shrink-0">
            <TrendingDown className="h-5 w-5" />
          </div>
          <div>
            <p className="text-xs text-slate-500 dark:text-slate-400 font-medium">
              {t('recurring.summaryTotalMonthly')}
            </p>
            <p className="text-lg font-bold text-rose-600 dark:text-rose-400">
              {formatMoney(projectedMonthlyExpense, locale)}
            </p>
          </div>
        </div>

        <div className="p-4 bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-2xl flex items-center gap-3.5 shadow-2xs">
          <div className="h-10 w-10 rounded-xl bg-teal-50 dark:bg-teal-950/40 text-teal-600 flex items-center justify-center shrink-0">
            <TrendingUp className="h-5 w-5" />
          </div>
          <div>
            <p className="text-xs text-slate-500 dark:text-slate-400 font-medium">
              {t('recurring.summaryTotalIncome')}
            </p>
            <p className="text-lg font-bold text-teal-600 dark:text-teal-400">
              {formatMoney(projectedMonthlyIncome, locale)}
            </p>
          </div>
        </div>
      </div>

      {/* Main List Section */}
      <div className="bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-3xl overflow-hidden shadow-2xs">
        <div className="p-5 sm:p-6 border-b border-slate-100 dark:border-slate-800 flex flex-wrap items-center justify-between gap-4">
          <div>
            <h3 className="text-base font-bold text-slate-900 dark:text-white flex items-center gap-2">
              <RefreshCw className="h-5 w-5 text-emerald-500" />
              <span>{t('recurring.title')}</span>
            </h3>
            <p className="text-xs text-slate-500 dark:text-slate-400 mt-0.5">
              {t('recurring.subtitle')}
            </p>
          </div>

          <Button type="button" onClick={handleOpenCreate} className="gap-2 text-xs font-semibold">
            <Plus className="h-4 w-4" />
            <span>{t('recurring.createBtn')}</span>
          </Button>
        </div>

        {toggleError && (
          <div className="p-3 mx-4 sm:mx-6 mt-4 rounded-2xl bg-rose-50 dark:bg-rose-950/40 text-rose-700 dark:text-rose-300 text-xs font-semibold flex items-center justify-between gap-2">
            <div className="flex items-center gap-2">
              <AlertCircle className="h-4 w-4 shrink-0" />
              <span>{toggleError}</span>
            </div>
            <button
              type="button"
              onClick={() => setToggleError(null)}
              className="text-slate-400 hover:text-slate-600 text-xs font-bold px-2 py-0.5"
            >
              ✕
            </button>
          </div>
        )}

        {/* List of items */}
        {recurringTransactions.length === 0 ? (
          <div className="p-12 text-center space-y-3">
            <div className="h-12 w-12 rounded-2xl bg-slate-100 dark:bg-slate-800 text-slate-400 flex items-center justify-center mx-auto">
              <RefreshCw className="h-6 w-6" />
            </div>
            <p className="text-sm font-bold text-slate-900 dark:text-white">
              {t('recurring.emptyState')}
            </p>
            <p className="text-xs text-slate-500 max-w-md mx-auto">
              {t('recurring.emptyStateDesc')}
            </p>
            <Button type="button" size="sm" onClick={handleOpenCreate} className="mt-2">
              {t('recurring.createBtn')}
            </Button>
          </div>
        ) : (
          <div className="divide-y divide-slate-100 dark:divide-slate-800">
            {recurringTransactions.map((item) => {
              const isIncome = item.type === 'income';
              const isActive = item.status === 'active';
              const isDue = isRecurringDue(item, currentUser?.id || '');
              const isFocused = focusRecurringId === item.id;
              const categoryItem = categories.find((c) => c.id === item.categoryId);
              const categoryLabel = categoryItem
                ? (categoryItem.isSystem
                    ? (categoryItem.key.startsWith('categories.') ? t(categoryItem.key) : getLocalizedCategoryName(categoryItem.name, t, categories))
                    : categoryItem.name)
                : item.categoryId;

              return (
                <div
                  key={item.id}
                  ref={isFocused ? focusedRef : undefined}
                  className={`p-4 sm:p-5 flex flex-col sm:flex-row sm:items-center justify-between gap-4 hover:bg-slate-50/50 dark:hover:bg-slate-800/40 transition-colors ${
                    isFocused ? 'ring-2 ring-emerald-500 bg-emerald-50/20 dark:bg-emerald-950/20 rounded-2xl' : ''
                  } ${!isActive ? 'opacity-70 bg-slate-50/30 dark:bg-slate-900/40' : ''}`}
                >
                  <div className="flex items-start gap-3.5 min-w-0">
                    <div
                      className={`h-10 w-10 rounded-2xl flex items-center justify-center shrink-0 mt-0.5 ${
                        isIncome
                          ? 'bg-emerald-50 dark:bg-emerald-950/40 text-emerald-600'
                          : 'bg-rose-50 dark:bg-rose-950/40 text-rose-600'
                      }`}
                    >
                      {isIncome ? <TrendingUp className="h-5 w-5" /> : <TrendingDown className="h-5 w-5" />}
                    </div>

                    <div className="min-w-0 space-y-1">
                      <div className="flex items-center gap-2 flex-wrap">
                        <h4 className="text-sm font-bold text-slate-900 dark:text-white truncate">
                          {item.title}
                        </h4>
                        <span
                          className={`text-[10px] font-semibold px-2 py-0.5 rounded-full ${
                            isActive
                              ? 'bg-emerald-50 dark:bg-emerald-950/50 text-emerald-700 dark:text-emerald-300'
                              : 'bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-400'
                          }`}
                        >
                          {isActive ? t('recurring.statusActive') : t('recurring.statusPaused')}
                        </span>
                        {isDue && (
                          <span className="text-[10px] font-bold px-2 py-0.5 rounded-full bg-rose-100 dark:bg-rose-950/60 text-rose-600 dark:text-rose-400 border border-rose-200 dark:border-rose-900/50 flex items-center gap-1">
                            <Clock className="h-3 w-3" />
                            <span>{locale === 'vi-VN' ? 'Đến hạn' : 'Due'}</span>
                          </span>
                        )}
                      </div>

                      <div className="flex items-center gap-3 text-xs text-slate-500 dark:text-slate-400 flex-wrap">
                        <span className="font-medium text-slate-700 dark:text-slate-300">
                          {categoryLabel}
                        </span>
                        <span>•</span>
                        <span className="flex items-center gap-1">
                          <Wallet className="h-3 w-3 text-slate-400" />
                          <span>
                            {item.walletName
                              ? getLocalizedWalletName(item.walletId || '', item.walletName, locale)
                              : 'Ví'}
                          </span>
                        </span>
                        <span>•</span>
                        <span className="flex items-center gap-1">
                          <Calendar className="h-3 w-3 text-slate-400" />
                          <span>
                            {getFrequencyLabel(item.frequency)} — {t('recurring.nextRun')}: {item.nextRunDate}
                          </span>
                        </span>
                      </div>
                      {item.description && (
                        <p className="text-[11px] text-slate-400 italic">{item.description}</p>
                      )}
                    </div>
                  </div>

                  {/* Right Side: Amount & Control Actions */}
                  <div className="flex items-center justify-between sm:justify-end gap-4 shrink-0 pt-2 sm:pt-0 border-t sm:border-t-0 border-slate-100 dark:border-slate-800">
                    <div className="text-left sm:text-right">
                      <p
                        className={`text-sm sm:text-base font-bold ${
                          isIncome ? 'text-emerald-600 dark:text-emerald-400' : 'text-slate-900 dark:text-white'
                        }`}
                      >
                        {isIncome ? '+' : '-'} {formatMoney(item.amount, locale)}
                      </p>
                      <p className="text-[10px] text-slate-400 font-medium">
                        /{getFrequencyLabel(item.frequency).toLowerCase()}
                      </p>
                    </div>

                    <div className="flex items-center gap-1.5 flex-wrap">
                      {isDue && onRecordPayment && (
                        <Button
                          type="button"
                          size="sm"
                          onClick={() => onRecordPayment(item)}
                          className="bg-emerald-600 hover:bg-emerald-700 text-white font-bold text-xs gap-1.5 px-3 py-1.5 shadow-xs cursor-pointer"
                        >
                          <CheckCircle2 className="h-3.5 w-3.5" />
                          <span>{locale === 'vi-VN' ? 'Ghi nhận thanh toán' : 'Record payment'}</span>
                        </Button>
                      )}

                      <button
                        type="button"
                        disabled={togglingId === item.id}
                        onClick={() => handleToggleStatus(item.id)}
                        className={`p-2 rounded-xl border transition-colors ${
                          togglingId === item.id ? 'opacity-50 cursor-not-allowed ' : ''
                        }${
                          isActive
                            ? 'border-slate-200 dark:border-slate-700 text-slate-600 hover:bg-slate-100 dark:hover:bg-slate-800'
                            : 'border-emerald-200 dark:border-emerald-800 bg-emerald-50 dark:bg-emerald-950/40 text-emerald-600 hover:bg-emerald-100'
                        }`}
                        title={isActive ? t('recurring.pause') : t('recurring.resume')}
                      >
                        {isActive ? <Pause className="h-4 w-4" /> : <Play className="h-4 w-4" />}
                      </button>

                      <button
                        type="button"
                        onClick={() => handleOpenEdit(item)}
                        className="p-2 border border-slate-200 dark:border-slate-700 text-slate-600 hover:text-slate-900 dark:text-slate-300 rounded-xl hover:bg-slate-100 dark:hover:bg-slate-800 transition-colors"
                        title={t('common.edit')}
                      >
                        <Edit2 className="h-4 w-4" />
                      </button>

                      <button
                        type="button"
                        onClick={() => {
                          setDeletingItem(item);
                          setDeleteError(null);
                        }}
                        className="p-2 border border-slate-200 dark:border-slate-700 text-slate-400 hover:text-rose-600 rounded-xl hover:bg-rose-50 dark:hover:bg-rose-950/30 transition-colors"
                        title={t('common.delete')}
                      >
                        <Trash2 className="h-4 w-4" />
                      </button>
                    </div>
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </div>

      {/* Delete Confirmation Modal with historical data preservation warning */}
      {deletingItem && (
        <div className="fixed inset-0 z-60 flex items-center justify-center p-4 bg-black/50 backdrop-blur-xs">
          <div className="bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-3xl p-6 w-full max-w-md space-y-4 shadow-xl">
            <div className="flex items-center gap-3 text-rose-600">
              <AlertTriangle className="h-6 w-6 shrink-0" />
              <h4 className="text-sm sm:text-base font-bold text-slate-900 dark:text-white">
                {t('recurring.deleteConfirmTitle')}
              </h4>
            </div>
            <p className="text-xs text-slate-600 dark:text-slate-300 leading-relaxed">
              {t('recurring.deleteConfirmDesc')}
            </p>
            {deleteError && (
              <div className="p-3 rounded-2xl bg-rose-50 dark:bg-rose-950/40 text-rose-700 dark:text-rose-300 text-xs font-semibold flex items-center gap-2">
                <AlertCircle className="h-4 w-4 shrink-0" />
                <span>{deleteError}</span>
              </div>
            )}
            <div className="flex justify-end gap-2 pt-2">
              <Button
                type="button"
                variant="outline"
                size="sm"
                disabled={isDeleting}
                onClick={() => {
                  setDeletingItem(null);
                  setDeleteError(null);
                }}
              >
                {t('common.cancel')}
              </Button>
              <Button
                type="button"
                variant="destructive"
                size="sm"
                disabled={isDeleting}
                onClick={handleConfirmDelete}
              >
                {isDeleting ? (t('common.processing') || 'Đang xóa...') : t('common.delete')}
              </Button>
            </div>
          </div>
        </div>
      )}

      {/* Create / Edit Recurring Modal */}
      <CreateRecurringModal
        isOpen={isModalOpen}
        onClose={() => setIsModalOpen(false)}
        editingItem={editingItem}
      />
    </div>
  );
};
