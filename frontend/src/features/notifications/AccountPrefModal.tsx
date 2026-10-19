import React, { useState } from 'react';
import { X, SlidersHorizontal, Bell, AlertTriangle, ShieldCheck, DollarSign, Wallet, Check } from 'lucide-react';
import { useI18n } from '../../i18n/I18nContext';
import { useNotifications, AccountFilterOption } from '../../providers/NotificationProvider';
import { AccountNotificationPreference } from '../../types';
import { Button } from '../../components/ui/Button';
import { cn } from '../../utils/cn';
import { sanitizeIntegerInput, parseSafeNonNegativeInteger } from '../../utils/inputValidation';
import { getLocalizedAccountDisplayName } from '../../utils/localizationHelpers';

interface AccountPrefModalProps {
  isOpen: boolean;
  onClose: () => void;
  initialAccountId?: string;
  onSavedToast: (msg: string) => void;
}

export const AccountPrefModal: React.FC<AccountPrefModalProps> = ({
  isOpen,
  onClose,
  initialAccountId = 'w-main',
  onSavedToast,
}) => {
  const { t, locale } = useI18n();
  const { accountList, accountPreferences, updateAccountPreferences } = useNotifications();

  // Filter out 'all' from customizable accounts
  const customizableAccounts = accountList.filter((a) => a.id !== 'all');

  const getAccountDisplayName = (acc?: { id: string; name: string; type?: string }) => {
    if (!acc) return '';
    return getLocalizedAccountDisplayName(acc.id, acc.name, locale, acc.type);
  };

  const [activeAccId, setActiveAccId] = useState<string>(
    initialAccountId !== 'all' ? initialAccountId : (customizableAccounts[0]?.id || 'w-main')
  );

  const currentPrefs: AccountNotificationPreference = accountPreferences[activeAccId] || {
    accountId: activeAccId,
    accountName: customizableAccounts.find((a) => a.id === activeAccId)?.name || activeAccId,
    notifyBalanceChanges: true,
    notifyBudgetAlerts: true,
    notifyLargeTransactions: true,
    largeTransactionThreshold: 1000000,
    notifyMinBalanceAlert: true,
    minBalanceThreshold: 500000,
  };

  const [formData, setFormData] = useState<AccountNotificationPreference>(currentPrefs);
  const [largeTxStr, setLargeTxStr] = useState<string>(
    String(currentPrefs.largeTransactionThreshold !== undefined ? currentPrefs.largeTransactionThreshold : 1000000)
  );
  const [minBalanceStr, setMinBalanceStr] = useState<string>(
    String(currentPrefs.minBalanceThreshold !== undefined ? currentPrefs.minBalanceThreshold : 500000)
  );
  const [error, setError] = useState('');

  // Sync when active account tab changes
  const handleSelectAccount = (id: string) => {
    setActiveAccId(id);
    const prefs = accountPreferences[id] || {
      accountId: id,
      accountName: customizableAccounts.find((a) => a.id === id)?.name || id,
      notifyBalanceChanges: true,
      notifyBudgetAlerts: true,
      notifyLargeTransactions: true,
      largeTransactionThreshold: 1000000,
      notifyMinBalanceAlert: true,
      minBalanceThreshold: 500000,
    };
    setFormData(prefs);
    setLargeTxStr(String(prefs.largeTransactionThreshold !== undefined ? prefs.largeTransactionThreshold : 1000000));
    setMinBalanceStr(String(prefs.minBalanceThreshold !== undefined ? prefs.minBalanceThreshold : 500000));
    setError('');
  };

  const [isSaving, setIsSaving] = useState(false);

  const handleSave = async () => {
    let finalLarge = formData.largeTransactionThreshold;
    let finalMin = formData.minBalanceThreshold;

    if (formData.notifyLargeTransactions) {
      const largeCheck = parseSafeNonNegativeInteger(largeTxStr);
      if (!largeCheck.valid) {
        setError(
          locale === 'vi-VN'
            ? 'Ngưỡng giao dịch lớn không hợp lệ (phải là số nguyên không âm)'
            : 'Large transaction threshold must be a non-negative integer'
        );
        return;
      }
      finalLarge = largeCheck.value;
    }

    if (formData.notifyMinBalanceAlert) {
      const minCheck = parseSafeNonNegativeInteger(minBalanceStr);
      if (!minCheck.valid) {
        setError(
          locale === 'vi-VN'
            ? 'Ngưỡng số dư tối thiểu không hợp lệ (phải là số nguyên không âm)'
            : 'Minimum balance threshold must be a non-negative integer'
        );
        return;
      }
      finalMin = minCheck.value;
    }

    const finalPrefs: AccountNotificationPreference = {
      ...formData,
      largeTransactionThreshold: finalLarge,
      minBalanceThreshold: finalMin,
    };

    setIsSaving(true);
    setError('');

    try {
      await updateAccountPreferences(activeAccId, finalPrefs);
      const currentAccItem = customizableAccounts.find((a) => a.id === activeAccId);
      const accName = getAccountDisplayName(currentAccItem) || activeAccId;
      onSavedToast(t('notifications.toastPrefsSaved', { account: accName }));
      onClose();
    } catch (err: any) {
      console.error('Failed to save notification preferences:', err);
      setError(
        err?.message ||
          (locale === 'vi-VN'
            ? 'Không thể lưu cài đặt thông báo lên máy chủ. Vui lòng thử lại.'
            : 'Failed to save notification preferences to server. Please try again.')
      );
    } finally {
      setIsSaving(false);
    }
  };

  if (!isOpen) return null;

  const currentAcc = customizableAccounts.find((a) => a.id === activeAccId);

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-stone-900/50 dark:bg-black/70 backdrop-blur-xs animate-in fade-in-0">
      <div
        className="w-full max-w-xl rounded-3xl bg-white dark:bg-slate-900 border border-stone-200 dark:border-slate-800 shadow-2xl overflow-hidden animate-in zoom-in-95 duration-150 flex flex-col max-h-[90vh]"
        onClick={(e) => e.stopPropagation()}
      >
        {/* Header */}
        <div className="flex items-center justify-between px-6 py-4 border-b border-stone-100 dark:border-slate-800 bg-stone-50/50 dark:bg-slate-900/50">
          <div className="flex items-center gap-2.5">
            <div className="h-9 w-9 rounded-xl bg-emerald-100 dark:bg-emerald-950/60 text-emerald-600 dark:text-emerald-400 flex items-center justify-center">
              <SlidersHorizontal className="h-5 w-5" />
            </div>
            <div>
              <h3 className="text-base font-bold text-stone-900 dark:text-white">
                {t('notifications.prefModalTitle')}
              </h3>
              <p className="text-xs text-stone-500 dark:text-slate-400">
                {t('notifications.prefModalSubtitle', { account: getAccountDisplayName(currentAcc) || activeAccId })}
              </p>
            </div>
          </div>
          <button
            onClick={onClose}
            className="h-8 w-8 rounded-full hover:bg-stone-200/60 dark:hover:bg-slate-800 flex items-center justify-center text-stone-400 hover:text-stone-700 dark:hover:text-slate-200 transition-colors"
          >
            <X className="h-4 w-4" />
          </button>
        </div>

        {/* Account Selector Tabs */}
        <div className="px-6 pt-4 pb-2 border-b border-stone-100 dark:border-slate-800 bg-stone-50/30 dark:bg-slate-900/30">
          <label className="text-[11px] font-bold text-stone-500 dark:text-slate-400 uppercase tracking-wider block mb-2">
            {t('notifications.filterByAccount')}
          </label>
          <div className="flex items-center gap-2 overflow-x-auto no-scrollbar pb-1">
            {customizableAccounts.map((acc) => {
              const isSelected = activeAccId === acc.id;
              return (
                <button
                  key={acc.id}
                  onClick={() => handleSelectAccount(acc.id)}
                  className={cn(
                    'flex items-center gap-1.5 px-3 py-1.5 rounded-xl text-xs font-semibold whitespace-nowrap transition-all cursor-pointer',
                    isSelected
                      ? 'bg-emerald-600 text-white shadow-2xs font-bold'
                      : 'bg-white dark:bg-slate-800 border border-stone-200 dark:border-slate-700 text-stone-600 dark:text-slate-300 hover:border-emerald-500'
                  )}
                >
                  <Wallet className="h-3.5 w-3.5" />
                  <span>{getAccountDisplayName(acc)}</span>
                </button>
              );
            })}
          </div>
        </div>

        {/* Body Content / Form */}
        <div className="p-6 space-y-5 overflow-y-auto flex-1">
          {/* Toggle 1: Balance Changes */}
          <div className="flex items-start justify-between gap-4 p-4 rounded-2xl bg-stone-50/70 dark:bg-slate-800/40 border border-stone-100 dark:border-slate-800">
            <div className="space-y-0.5">
              <div className="flex items-center gap-2">
                <Bell className="h-4 w-4 text-emerald-600 dark:text-emerald-400" />
                <h4 className="text-xs font-bold text-stone-900 dark:text-white">
                  {t('notifications.prefBalanceChangeTitle')}
                </h4>
              </div>
              <p className="text-[11px] text-stone-500 dark:text-slate-400">
                {t('notifications.prefBalanceChangeDesc')}
              </p>
            </div>
            <label className="relative inline-flex items-center cursor-pointer shrink-0 mt-0.5">
              <input
                type="checkbox"
                checked={formData.notifyBalanceChanges}
                onChange={(e) => setFormData({ ...formData, notifyBalanceChanges: e.target.checked })}
                className="sr-only peer"
              />
              <div className="w-11 h-6 bg-stone-200 peer-focus:outline-none rounded-full peer dark:bg-slate-700 peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-white after:border-stone-300 after:border after:rounded-full after:h-5 after:w-5 after:transition-all dark:border-slate-600 peer-checked:bg-emerald-600" />
            </label>
          </div>

          {/* Toggle 2: Budget Thresholds */}
          <div className="flex items-start justify-between gap-4 p-4 rounded-2xl bg-stone-50/70 dark:bg-slate-800/40 border border-stone-100 dark:border-slate-800">
            <div className="space-y-0.5">
              <div className="flex items-center gap-2">
                <AlertTriangle className="h-4 w-4 text-amber-500 dark:text-amber-400" />
                <h4 className="text-xs font-bold text-stone-900 dark:text-white">
                  {t('notifications.prefBudgetAlertTitle')}
                </h4>
              </div>
              <p className="text-[11px] text-stone-500 dark:text-slate-400">
                {t('notifications.prefBudgetAlertDesc')}
              </p>
            </div>
            <label className="relative inline-flex items-center cursor-pointer shrink-0 mt-0.5">
              <input
                type="checkbox"
                checked={formData.notifyBudgetAlerts}
                onChange={(e) => setFormData({ ...formData, notifyBudgetAlerts: e.target.checked })}
                className="sr-only peer"
              />
              <div className="w-11 h-6 bg-stone-200 peer-focus:outline-none rounded-full peer dark:bg-slate-700 peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-white after:border-stone-300 after:border after:rounded-full after:h-5 after:w-5 after:transition-all dark:border-slate-600 peer-checked:bg-emerald-600" />
            </label>
          </div>

          {/* Toggle 3: Large Transaction Alert */}
          <div className="p-4 rounded-2xl bg-stone-50/70 dark:bg-slate-800/40 border border-stone-100 dark:border-slate-800 space-y-3">
            <div className="flex items-start justify-between gap-4">
              <div className="space-y-0.5">
                <div className="flex items-center gap-2">
                  <DollarSign className="h-4 w-4 text-indigo-600 dark:text-indigo-400" />
                  <h4 className="text-xs font-bold text-stone-900 dark:text-white">
                    {t('notifications.prefLargeTxTitle')}
                  </h4>
                </div>
                <p className="text-[11px] text-stone-500 dark:text-slate-400">
                  {t('notifications.prefLargeTxDesc')}
                </p>
              </div>
              <label className="relative inline-flex items-center cursor-pointer shrink-0 mt-0.5">
                <input
                  type="checkbox"
                  checked={formData.notifyLargeTransactions}
                  onChange={(e) => setFormData({ ...formData, notifyLargeTransactions: e.target.checked })}
                  className="sr-only peer"
                />
                <div className="w-11 h-6 bg-stone-200 peer-focus:outline-none rounded-full peer dark:bg-slate-700 peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-white after:border-stone-300 after:border after:rounded-full after:h-5 after:w-5 after:transition-all dark:border-slate-600 peer-checked:bg-emerald-600" />
              </label>
            </div>

            {formData.notifyLargeTransactions && (
              <div className="pt-2 border-t border-stone-200/60 dark:border-slate-700/60 flex items-center justify-between gap-3">
                <span className="text-xs text-stone-600 dark:text-slate-300 font-medium">
                  {t('notifications.prefLargeTxThreshold')}
                </span>
                <div className="relative w-40">
                  <input
                    type="text"
                    inputMode="numeric"
                    value={largeTxStr}
                    onChange={(e) => {
                      setLargeTxStr(sanitizeIntegerInput(e.target.value));
                      if (error) setError('');
                    }}
                    className="w-full h-8 rounded-lg bg-white dark:bg-slate-900 border border-stone-200 dark:border-slate-700 text-xs px-3 text-right font-bold text-stone-900 dark:text-white focus:ring-1 focus:ring-emerald-500 outline-none"
                  />
                </div>
              </div>
            )}
          </div>

          {/* Toggle 4: Min Balance Warning */}
          <div className="p-4 rounded-2xl bg-stone-50/70 dark:bg-slate-800/40 border border-stone-100 dark:border-slate-800 space-y-3">
            <div className="flex items-start justify-between gap-4">
              <div className="space-y-0.5">
                <div className="flex items-center gap-2">
                  <ShieldCheck className="h-4 w-4 text-purple-600 dark:text-purple-400" />
                  <h4 className="text-xs font-bold text-stone-900 dark:text-white">
                    {t('notifications.prefMinBalanceTitle')}
                  </h4>
                </div>
                <p className="text-[11px] text-stone-500 dark:text-slate-400">
                  {t('notifications.prefMinBalanceDesc')}
                </p>
              </div>
              <label className="relative inline-flex items-center cursor-pointer shrink-0 mt-0.5">
                <input
                  type="checkbox"
                  checked={formData.notifyMinBalanceAlert}
                  onChange={(e) => setFormData({ ...formData, notifyMinBalanceAlert: e.target.checked })}
                  className="sr-only peer"
                />
                <div className="w-11 h-6 bg-stone-200 peer-focus:outline-none rounded-full peer dark:bg-slate-700 peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-white after:border-stone-300 after:border after:rounded-full after:h-5 after:w-5 after:transition-all dark:border-slate-600 peer-checked:bg-emerald-600" />
              </label>
            </div>

            {formData.notifyMinBalanceAlert && (
              <div className="pt-2 border-t border-stone-200/60 dark:border-slate-700/60 flex items-center justify-between gap-3">
                <span className="text-xs text-stone-600 dark:text-slate-300 font-medium">
                  {t('notifications.prefMinBalanceThreshold')}
                </span>
                <div className="relative w-40">
                  <input
                    type="text"
                    inputMode="numeric"
                    value={minBalanceStr}
                    onChange={(e) => {
                      setMinBalanceStr(sanitizeIntegerInput(e.target.value));
                      if (error) setError('');
                    }}
                    className="w-full h-8 rounded-lg bg-white dark:bg-slate-900 border border-stone-200 dark:border-slate-700 text-xs px-3 text-right font-bold text-stone-900 dark:text-white focus:ring-1 focus:ring-emerald-500 outline-none"
                  />
                </div>
              </div>
            )}
          </div>
        </div>

        {/* Error message */}
        {error && (
          <div className="px-6 py-2.5 bg-rose-50 dark:bg-rose-950/40 border-t border-rose-100 dark:border-rose-900/40 text-xs text-rose-600 dark:text-rose-400 font-medium flex items-center gap-1.5">
            <AlertTriangle className="h-4 w-4 shrink-0" />
            <span>{error}</span>
          </div>
        )}

        {/* Footer Actions */}
        <div className="flex items-center justify-end gap-3 px-6 py-4 border-t border-stone-100 dark:border-slate-800 bg-stone-50/50 dark:bg-slate-900/50">
          <Button variant="outline" size="sm" onClick={onClose} disabled={isSaving}>
            {t('notifications.cancelBtn')}
          </Button>
          <Button variant="primary" size="sm" onClick={handleSave} disabled={isSaving} className="gap-1.5">
            <Check className="h-4 w-4" />
            <span>{isSaving ? (locale === 'vi-VN' ? 'Đang lưu...' : 'Saving...') : t('notifications.savePrefsBtn')}</span>
          </Button>
        </div>
      </div>
    </div>
  );
};
