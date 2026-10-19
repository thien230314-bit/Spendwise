import React, { useState, useEffect } from 'react';
import {
  Plus,
  Sliders,
  Edit2,
  Trash2,
  AlertTriangle,
  ArrowRight,
  ShieldCheck,
  Hash,
  AlertCircle,
} from 'lucide-react';
import { useI18n } from '../../../i18n/I18nContext';
import { useFinancial } from '../../../providers/FinancialProvider';
import { TransactionRule } from '../../../types';
import { getLocalizedCategoryName, getLocalizedErrorMessage } from '../../../utils/localizationHelpers';
import { Button } from '../../../components/ui/Button';
import { CreateRuleModal } from '../modals/CreateRuleModal';

export const TransactionRulesTab: React.FC = () => {
  const { t, locale } = useI18n();
  const { rules, categories, toggleRuleStatus, deleteRule } = useFinancial();

  const [isModalOpen, setIsModalOpen] = useState(false);
  const [editingRule, setEditingRule] = useState<TransactionRule | null>(null);
  const [deletingRule, setDeletingRule] = useState<TransactionRule | null>(null);
  const [isDeleting, setIsDeleting] = useState(false);
  const [deleteError, setDeleteError] = useState<string | null>(null);
  const [togglingId, setTogglingId] = useState<string | null>(null);
  const [toggleError, setToggleError] = useState<string | null>(null);

  // Escape key listener for delete confirmation modal
  useEffect(() => {
    if (!deletingRule) return;
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape' && !isDeleting) {
        setDeletingRule(null);
        setDeleteError(null);
      }
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [deletingRule, isDeleting]);

  const handleOpenCreate = () => {
    setEditingRule(null);
    setIsModalOpen(true);
  };

  const handleOpenEdit = (rule: TransactionRule) => {
    setEditingRule(rule);
    setIsModalOpen(true);
  };

  const handleConfirmDelete = async () => {
    if (!deletingRule || isDeleting) return;
    setIsDeleting(true);
    setDeleteError(null);
    try {
      const res = await deleteRule(deletingRule.id);
      if (res.success) {
        setDeletingRule(null);
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
      const res = await toggleRuleStatus(id);
      if (!res.success) {
        setToggleError(getLocalizedErrorMessage(res.error, locale, t));
      }
    } catch {
      setToggleError(t('common.errorOccurred'));
    } finally {
      setTogglingId(null);
    }
  };

  // Sort rules by priority descending
  const sortedRules = [...rules].sort((a, b) => (b.priority || 0) - (a.priority || 0));

  return (
    <div className="space-y-6">
      {/* Informative Banner */}
      <div className="p-4 bg-emerald-50/70 dark:bg-emerald-950/30 border border-emerald-200 dark:border-emerald-900/50 rounded-2xl flex items-start gap-3">
        <ShieldCheck className="h-5 w-5 text-emerald-600 dark:text-emerald-400 shrink-0 mt-0.5" />
        <div className="text-xs text-emerald-800 dark:text-emerald-300">
          <p className="font-semibold">{t('rules.ruleNotice')}</p>
        </div>
      </div>

      {/* Rules List Container */}
      <div className="bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-3xl overflow-hidden shadow-2xs">
        <div className="p-5 sm:p-6 border-b border-slate-100 dark:border-slate-800 flex flex-wrap items-center justify-between gap-4">
          <div>
            <h3 className="text-base font-bold text-slate-900 dark:text-white flex items-center gap-2">
              <Sliders className="h-5 w-5 text-emerald-500" />
              <span>{t('rules.title')}</span>
            </h3>
            <p className="text-xs text-slate-500 dark:text-slate-400 mt-0.5">
              {t('rules.subtitle')}
            </p>
          </div>

          <Button type="button" onClick={handleOpenCreate} className="gap-2 text-xs font-semibold">
            <Plus className="h-4 w-4" />
            <span>{t('rules.createBtn')}</span>
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

        {/* List */}
        {sortedRules.length === 0 ? (
          <div className="p-12 text-center space-y-3">
            <div className="h-12 w-12 rounded-2xl bg-slate-100 dark:bg-slate-800 text-slate-400 flex items-center justify-center mx-auto">
              <Sliders className="h-6 w-6" />
            </div>
            <p className="text-sm font-bold text-slate-900 dark:text-white">
              {t('rules.emptyState')}
            </p>
            <p className="text-xs text-slate-500 max-w-md mx-auto">
              {t('rules.emptyStateDesc')}
            </p>
            <Button type="button" size="sm" onClick={handleOpenCreate} className="mt-2">
              {t('rules.createBtn')}
            </Button>
          </div>
        ) : (
          <div className="divide-y divide-slate-100 dark:divide-slate-800">
            {sortedRules.map((rule) => {
              const matchedCategory = categories.find((c) => c.id === rule.targetCategoryId);
              const categoryLabel = matchedCategory
                ? (matchedCategory.isSystem
                    ? (matchedCategory.key.startsWith('categories.') ? t(matchedCategory.key) : getLocalizedCategoryName(matchedCategory.name, t, categories))
                    : matchedCategory.name)
                : rule.targetCategoryId;

              return (
                <div
                  key={rule.id}
                  className={`p-4 sm:p-5 flex flex-col sm:flex-row sm:items-center justify-between gap-4 hover:bg-slate-50/50 dark:hover:bg-slate-800/40 transition-colors ${
                    !rule.isActive ? 'opacity-65 bg-slate-50/20 dark:bg-slate-900/30' : ''
                  }`}
                >
                  <div className="flex items-start gap-3.5 min-w-0">
                    <div className="h-9 w-9 rounded-xl bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-300 flex items-center justify-center shrink-0 mt-0.5">
                      <Sliders className="h-4 w-4" />
                    </div>

                    <div className="min-w-0 space-y-1">
                      <div className="flex items-center gap-2 flex-wrap">
                        <span className="text-sm font-bold text-slate-900 dark:text-white truncate">
                          "{rule.descriptionPattern}"
                        </span>
                        <span
                          className={`text-[10px] font-semibold px-2 py-0.5 rounded-full ${
                            rule.isActive
                              ? 'bg-emerald-50 dark:bg-emerald-950/50 text-emerald-700 dark:text-emerald-300'
                              : 'bg-slate-100 dark:bg-slate-800 text-slate-500'
                          }`}
                        >
                          {rule.isActive ? t('rules.activeStatus') : t('rules.inactiveStatus')}
                        </span>
                        <span className="inline-flex items-center gap-0.5 text-[10px] bg-slate-100 dark:bg-slate-800 px-2 py-0.5 rounded-md text-slate-600 dark:text-slate-400 font-medium">
                          <Hash className="h-3 w-3" />
                          <span>{t('rules.priorityBadge')}: {rule.priority || 1}</span>
                        </span>
                      </div>

                      <div className="flex items-center gap-2 text-xs text-slate-500 dark:text-slate-400 flex-wrap">
                        <span className="bg-slate-100 dark:bg-slate-800/60 px-2 py-0.5 rounded-md font-mono text-[11px] text-slate-700 dark:text-slate-300">
                          {t('rules.fieldDescription')} {t('rules.opContains')} "{rule.descriptionPattern}"
                        </span>
                        <ArrowRight className="h-3 w-3 text-slate-400 shrink-0" />
                        <span className="font-semibold text-emerald-600 dark:text-emerald-400">
                          {categoryLabel}
                        </span>
                      </div>
                    </div>
                  </div>

                  {/* Actions */}
                  <div className="flex items-center justify-between sm:justify-end gap-3 shrink-0 pt-2 sm:pt-0 border-t sm:border-t-0 border-slate-100 dark:border-slate-800">
                    {/* Toggle */}
                    <button
                      type="button"
                      disabled={togglingId === rule.id}
                      onClick={() => handleToggleStatus(rule.id)}
                      className={`text-xs font-semibold px-3 py-1.5 rounded-xl border transition-colors ${
                        togglingId === rule.id ? 'opacity-50 cursor-not-allowed ' : ''
                      }${
                        rule.isActive
                          ? 'border-emerald-200 dark:border-emerald-800 text-emerald-600 bg-emerald-50 dark:bg-emerald-950/40 hover:bg-emerald-100'
                          : 'border-slate-200 dark:border-slate-700 text-slate-500 hover:bg-slate-100 dark:hover:bg-slate-800'
                      }`}
                    >
                      {rule.isActive
                        ? (locale === 'vi-VN' ? 'Tắt quy tắc' : 'Disable rule')
                        : (locale === 'vi-VN' ? 'Bật quy tắc' : 'Enable rule')}
                    </button>

                    <button
                      type="button"
                      onClick={() => handleOpenEdit(rule)}
                      className="p-2 border border-slate-200 dark:border-slate-700 text-slate-600 hover:text-slate-900 dark:text-slate-300 rounded-xl hover:bg-slate-100 dark:hover:bg-slate-800 transition-colors"
                      title={t('common.edit')}
                    >
                      <Edit2 className="h-4 w-4" />
                    </button>

                    <button
                      type="button"
                      onClick={() => {
                        setDeletingRule(rule);
                        setDeleteError(null);
                      }}
                      className="p-2 border border-slate-200 dark:border-slate-700 text-slate-400 hover:text-rose-600 rounded-xl hover:bg-rose-50 dark:hover:bg-rose-950/30 transition-colors"
                      title={t('common.delete')}
                    >
                      <Trash2 className="h-4 w-4" />
                    </button>
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </div>

      {/* Delete Modal */}
      {deletingRule && (
        <div className="fixed inset-0 z-60 flex items-center justify-center p-4 bg-black/50 backdrop-blur-xs">
          <div className="bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-3xl p-6 w-full max-w-md space-y-4 shadow-xl">
            <div className="flex items-center gap-3 text-rose-600">
              <AlertTriangle className="h-6 w-6 shrink-0" />
              <h4 className="text-sm sm:text-base font-bold text-slate-900 dark:text-white">
                {t('rules.deleteConfirmTitle')}
              </h4>
            </div>
            <p className="text-xs text-slate-600 dark:text-slate-300 leading-relaxed">
              {t('rules.deleteConfirmDesc')}
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
                  setDeletingRule(null);
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

      {/* Create / Edit Rule Modal */}
      <CreateRuleModal
        isOpen={isModalOpen}
        onClose={() => setIsModalOpen(false)}
        editingRule={editingRule}
      />
    </div>
  );
};
