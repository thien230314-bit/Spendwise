import React, { useState } from 'react';
import { motion, AnimatePresence } from 'motion/react';
import {
  X,
  Plus,
  Edit2,
  Trash2,
  AlertCircle,
  CheckCircle,
  Tag,
  ShieldAlert,
  Briefcase,
  Utensils,
  ShoppingBag,
  Car,
  Tv,
  Home,
  GraduationCap,
  Target,
  Send,
  TrendingUp,
} from 'lucide-react';
import { useI18n } from '../../../i18n/I18nContext';
import { useFinancial } from '../../../providers/FinancialProvider';
import { CategoryItem, CategoryKind } from '../../../types';
import { getLocalizedCategoryName, getLocalizedErrorMessage } from '../../../utils/localizationHelpers';
import { Button } from '../../../components/ui/Button';

interface CategoryManagerModalProps {
  isOpen: boolean;
  onClose: () => void;
}

const AVAILABLE_ICONS = [
  { name: 'Utensils', icon: Utensils, label: 'Ăn uống' },
  { name: 'ShoppingBag', icon: ShoppingBag, label: 'Mua sắm' },
  { name: 'Car', icon: Car, label: 'Đi lại' },
  { name: 'Tv', icon: Tv, label: 'Giải trí' },
  { name: 'Home', icon: Home, label: 'Hóa đơn' },
  { name: 'GraduationCap', icon: GraduationCap, label: 'Học tập' },
  { name: 'Target', icon: Target, label: 'Tiết kiệm' },
  { name: 'Briefcase', icon: Briefcase, label: 'Lương' },
  { name: 'TrendingUp', icon: TrendingUp, label: 'Đầu tư' },
  { name: 'Send', icon: Send, label: 'Chuyển tiền' },
  { name: 'Tag', icon: Tag, label: 'Khác' },
];

const AVAILABLE_COLORS = [
  '#10B981', // Emerald
  '#0EA5E9', // Sky
  '#F59E0B', // Amber
  '#EC4899', // Pink
  '#8B5CF6', // Purple
  '#14B8A6', // Teal
  '#6366F1', // Indigo
  '#EF4444', // Red
  '#6B7280', // Gray
];

const getCategoryIconLabel = (iconName: string, locale: string): string => {
  const isEn = locale === 'en-US';
  switch (iconName) {
    case 'Utensils':
      return isEn ? 'Food' : 'Ăn uống';
    case 'ShoppingBag':
      return isEn ? 'Shopping' : 'Mua sắm';
    case 'Car':
      return isEn ? 'Transportation' : 'Đi lại';
    case 'Tv':
      return isEn ? 'Entertainment' : 'Giải trí';
    case 'Home':
      return isEn ? 'Bills' : 'Hóa đơn';
    case 'GraduationCap':
      return isEn ? 'Education' : 'Học tập';
    case 'Target':
      return isEn ? 'Savings' : 'Tiết kiệm';
    case 'Briefcase':
      return isEn ? 'Salary' : 'Lương';
    case 'TrendingUp':
      return isEn ? 'Investment' : 'Đầu tư';
    case 'Send':
      return isEn ? 'Transfer' : 'Chuyển tiền';
    case 'Tag':
    default:
      return isEn ? 'Other' : 'Khác';
  }
};

export const CategoryManagerModal: React.FC<CategoryManagerModalProps> = ({ isOpen, onClose }) => {
  const { t, locale } = useI18n();
  const { categories, addCategory, editCategory, deleteCategory } = useFinancial();

  const [activeTab, setActiveTab] = useState<'all' | 'expense' | 'income'>('all');
  const [isEditing, setIsEditing] = useState<boolean>(false);
  const [editingCatId, setEditingCatId] = useState<string | null>(null);

  // Form state
  const [name, setName] = useState('');
  const [kind, setKind] = useState<CategoryKind>('expense');
  const [icon, setIcon] = useState('Tag');
  const [color, setColor] = useState('#10B981');
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);

  // Confirm delete dialog state
  const [deletingCat, setDeletingCat] = useState<CategoryItem | null>(null);

  // Escape listener
  React.useEffect(() => {
    if (!isOpen) return;
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        if (deletingCat) {
          setDeletingCat(null);
        } else if (isEditing) {
          resetForm();
        } else {
          onClose();
        }
      }
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [isOpen, deletingCat, isEditing, onClose]);

  if (!isOpen) return null;

  const filteredCategories = categories.filter((c) => {
    if (activeTab === 'expense') return c.kind === 'expense';
    if (activeTab === 'income') return c.kind === 'income';
    return true;
  });

  const resetForm = () => {
    setName('');
    setKind('expense');
    setIcon('Tag');
    setColor('#10B981');
    setIsEditing(false);
    setEditingCatId(null);
    setErrorMessage(null);
  };

  const handleStartAdd = () => {
    resetForm();
    setIsEditing(true);
  };

  const handleStartEdit = (cat: CategoryItem) => {
    setEditingCatId(cat.id);
    setName(cat.name);
    setKind(cat.kind);
    setIcon(cat.icon || 'Tag');
    setColor(cat.color || '#10B981');
    setIsEditing(true);
    setErrorMessage(null);
    setSuccessMessage(null);
  };

  const handleSaveForm = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMessage(null);

    if (!name.trim()) {
      setErrorMessage(locale === 'en-US' ? 'Please enter a category name.' : 'Vui lòng nhập tên danh mục.');
      return;
    }

    if (editingCatId) {
      const res = await editCategory(editingCatId, {
        name: name.trim(),
        kind,
        icon,
        color,
      });
      if (!res.success) {
        setErrorMessage(
          getLocalizedErrorMessage(
            res.error,
            locale,
            t,
            locale === 'en-US'
              ? 'Unable to update the category. Please try again.'
              : 'Có lỗi xảy ra.'
          )
        );
        return;
      }
      setSuccessMessage(locale === 'en-US' ? 'Category updated successfully!' : 'Đã cập nhật danh mục thành công!');
    } else {
      const res = await addCategory({
        key: `custom_${Date.now()}`,
        name: name.trim(),
        kind,
        icon,
        color,
      });
      if (!res.success) {
        setErrorMessage(
          getLocalizedErrorMessage(
            res.error,
            locale,
            t,
            locale === 'en-US'
              ? 'Unable to create the category. Please try again.'
              : 'Có lỗi xảy ra.'
          )
        );
        return;
      }
      setSuccessMessage(locale === 'en-US' ? 'Category created successfully!' : 'Đã thêm danh mục mới thành công!');
    }

    resetForm();
    setTimeout(() => setSuccessMessage(null), 3000);
  };

  const handleConfirmDelete = async () => {
    if (!deletingCat) return;
    setErrorMessage(null);
    const res = await deleteCategory(deletingCat.id);
    if (!res.success) {
      setErrorMessage(
        getLocalizedErrorMessage(
          res.error,
          locale,
          t,
          locale === 'en-US'
            ? 'Unable to delete the category. It may still be in use.'
            : t('categories.inUseError')
        )
      );
      setDeletingCat(null);
      return;
    }
    if (res.archived) {
      setSuccessMessage(
        locale === 'en-US'
          ? 'Category archived to preserve historical records.'
          : 'Danh mục đã được lưu trữ để bảo toàn dữ liệu lịch sử và sẽ không hiển thị khi tạo mới.'
      );
    } else {
      setSuccessMessage(locale === 'en-US' ? 'Category deleted.' : 'Đã xóa danh mục.');
    }
    setDeletingCat(null);
    setTimeout(() => setSuccessMessage(null), 3000);
  };

  const renderIconComponent = (iconName: string, iconColor?: string) => {
    const matched = AVAILABLE_ICONS.find((i) => i.name === iconName);
    const IconComp = matched ? matched.icon : Tag;
    return <IconComp className="h-4 w-4" style={{ color: iconColor }} />;
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-xs">
      <motion.div
        initial={{ opacity: 0, scale: 0.96 }}
        animate={{ opacity: 1, scale: 1 }}
        exit={{ opacity: 0, scale: 0.96 }}
        className="bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-3xl w-full max-w-2xl overflow-hidden shadow-2xl flex flex-col max-h-[90vh]"
      >
        {/* Header */}
        <div className="p-6 border-b border-slate-100 dark:border-slate-800 flex items-center justify-between">
          <div>
            <h2 className="text-lg font-bold text-slate-900 dark:text-white flex items-center gap-2">
              <Tag className="h-5 w-5 text-emerald-500" />
              <span>{t('categories.managerTitle')}</span>
            </h2>
            <p className="text-xs text-slate-500 dark:text-slate-400 mt-1">
              {t('categories.managerSubtitle')}
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

        {/* Notification Banners */}
        <div className="px-6 pt-4 space-y-2">
          {errorMessage && (
            <div className="p-3 bg-rose-50 dark:bg-rose-950/30 border border-rose-200 dark:border-rose-900/50 rounded-2xl flex items-start gap-2.5 text-xs text-rose-700 dark:text-rose-300">
              <AlertCircle className="h-4 w-4 text-rose-600 shrink-0 mt-0.5" />
              <span>{errorMessage}</span>
            </div>
          )}
          {successMessage && (
            <div className="p-3 bg-emerald-50 dark:bg-emerald-950/30 border border-emerald-200 dark:border-emerald-900/50 rounded-2xl flex items-center gap-2.5 text-xs text-emerald-700 dark:text-emerald-300">
              <CheckCircle className="h-4 w-4 text-emerald-600 shrink-0" />
              <span>{successMessage}</span>
            </div>
          )}
        </div>

        {/* Content Body */}
        <div className="p-6 overflow-y-auto flex-1 space-y-6">
          {/* Top Bar: Tabs & New Category Button */}
          {!isEditing && (
            <div className="flex flex-wrap items-center justify-between gap-3">
              <div className="flex items-center gap-1.5 p-1 bg-slate-100 dark:bg-slate-800 rounded-xl">
                <button
                  type="button"
                  onClick={() => setActiveTab('all')}
                  className={`px-3 py-1.5 rounded-lg text-xs font-semibold transition-all ${
                    activeTab === 'all'
                      ? 'bg-white dark:bg-slate-700 text-slate-900 dark:text-white shadow-xs'
                      : 'text-slate-600 dark:text-slate-400 hover:text-slate-900'
                  }`}
                >
                  {t('transactions.all')} ({categories.length})
                </button>
                <button
                  type="button"
                  onClick={() => setActiveTab('expense')}
                  className={`px-3 py-1.5 rounded-lg text-xs font-semibold transition-all ${
                    activeTab === 'expense'
                      ? 'bg-white dark:bg-slate-700 text-slate-900 dark:text-white shadow-xs'
                      : 'text-slate-600 dark:text-slate-400 hover:text-slate-900'
                  }`}
                >
                  {t('categories.kindExpense')} ({categories.filter((c) => c.kind === 'expense').length})
                </button>
                <button
                  type="button"
                  onClick={() => setActiveTab('income')}
                  className={`px-3 py-1.5 rounded-lg text-xs font-semibold transition-all ${
                    activeTab === 'income'
                      ? 'bg-white dark:bg-slate-700 text-slate-900 dark:text-white shadow-xs'
                      : 'text-slate-600 dark:text-slate-400 hover:text-slate-900'
                  }`}
                >
                  {t('categories.kindIncome')} ({categories.filter((c) => c.kind === 'income').length})
                </button>
              </div>

              <Button
                type="button"
                size="sm"
                onClick={handleStartAdd}
                className="gap-1.5 text-xs font-semibold"
              >
                <Plus className="h-4 w-4" />
                <span>{t('categories.addCategory')}</span>
              </Button>
            </div>
          )}

          {/* Form when creating or editing */}
          <AnimatePresence mode="wait">
            {isEditing && (
              <motion.form
                initial={{ opacity: 0, y: -10 }}
                animate={{ opacity: 1, y: 0 }}
                exit={{ opacity: 0, y: -10 }}
                onSubmit={handleSaveForm}
                className="p-5 bg-slate-50 dark:bg-slate-800/50 rounded-2xl border border-slate-200 dark:border-slate-700 space-y-4"
              >
                <div className="flex items-center justify-between pb-2 border-b border-slate-200 dark:border-slate-700">
                  <h3 className="text-sm font-bold text-slate-900 dark:text-white">
                    {editingCatId ? t('categories.editCategory') : t('categories.addCategory')}
                  </h3>
                  <button
                    type="button"
                    onClick={resetForm}
                    className="text-xs text-slate-500 hover:text-slate-800 dark:hover:text-white font-medium"
                  >
                    {t('common.cancel')}
                  </button>
                </div>

                <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                  <div>
                    <label className="block text-xs font-semibold text-slate-700 dark:text-slate-300 mb-1.5">
                      {t('categories.categoryName')} *
                    </label>
                    <input
                      type="text"
                      value={name}
                      onChange={(e) => setName(e.target.value)}
                      onKeyDown={(e) => {
                        if (e.key === 'Enter') {
                          e.preventDefault();
                          handleSaveForm(e);
                        }
                      }}
                      placeholder={
                        locale === 'vi-VN'
                          ? 'Ví dụ: Cà phê sáng, Gym, Tiền điện...'
                          : 'e.g. Morning coffee, Gym, Electric bill...'
                      }
                      required
                      className="w-full px-3.5 py-2 text-xs sm:text-sm rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 text-slate-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-emerald-500"
                    />
                  </div>

                  <div>
                    <label className="block text-xs font-semibold text-slate-700 dark:text-slate-300 mb-1.5">
                      {t('categories.categoryKind')}
                    </label>
                    <div className="flex gap-2">
                      <button
                        type="button"
                        onClick={() => setKind('expense')}
                        className={`flex-1 py-2 text-xs font-semibold rounded-xl border transition-all ${
                          kind === 'expense'
                            ? 'bg-rose-50 border-rose-500 text-rose-700 dark:bg-rose-950/40 dark:text-rose-300 shadow-xs'
                            : 'border-slate-200 dark:border-slate-700 text-slate-600 dark:text-slate-400 hover:border-slate-300'
                        }`}
                      >
                        {t('categories.kindExpense')}
                      </button>
                      <button
                        type="button"
                        onClick={() => setKind('income')}
                        className={`flex-1 py-2 text-xs font-semibold rounded-xl border transition-all ${
                          kind === 'income'
                            ? 'bg-emerald-50 border-emerald-500 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-300 shadow-xs'
                            : 'border-slate-200 dark:border-slate-700 text-slate-600 dark:text-slate-400 hover:border-slate-300'
                        }`}
                      >
                        {t('categories.kindIncome')}
                      </button>
                    </div>
                    {editingCatId && (
                      <p className="text-[11px] text-amber-600 dark:text-amber-400 mt-1.5 leading-snug">
                        {locale === 'vi-VN'
                          ? 'Thay đổi loại danh mục chỉ áp dụng cho giao dịch mới. Các giao dịch đã ghi nhận không thay đổi.'
                          : 'Changing category type only applies to new transactions. Historical records will not be changed.'}
                      </p>
                    )}
                  </div>
                </div>

                {/* Icon & Color Selection */}
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 pt-2">
                  <div>
                    <label className="block text-xs font-semibold text-slate-700 dark:text-slate-300 mb-1.5">
                      {t('categories.iconLabel')}
                    </label>
                    <div className="flex flex-wrap gap-2">
                      {AVAILABLE_ICONS.map((item) => {
                        const isSel = icon === item.name;
                        const IconComponent = item.icon;
                        return (
                          <button
                            key={item.name}
                            type="button"
                            onClick={() => setIcon(item.name)}
                            className={`p-2 rounded-xl border transition-all ${
                              isSel
                                ? 'border-emerald-500 bg-emerald-50 dark:bg-emerald-950/50 text-emerald-600 ring-2 ring-emerald-500/30'
                                : 'border-slate-200 dark:border-slate-700 hover:bg-slate-100 dark:hover:bg-slate-700 text-slate-600 dark:text-slate-400'
                            }`}
                            title={getCategoryIconLabel(item.name, locale)}
                          >
                            <IconComponent className="h-4 w-4" />
                          </button>
                        );
                      })}
                    </div>
                  </div>

                  <div>
                    <label className="block text-xs font-semibold text-slate-700 dark:text-slate-300 mb-1.5">
                      {t('categories.colorLabel')}
                    </label>
                    <div className="flex flex-wrap gap-2">
                      {AVAILABLE_COLORS.map((c) => {
                        const isSel = color === c;
                        return (
                          <button
                            key={c}
                            type="button"
                            onClick={() => setColor(c)}
                            className={`h-7 w-7 rounded-full border-2 transition-transform ${
                              isSel ? 'scale-110 border-slate-900 dark:border-white shadow-sm' : 'border-transparent hover:scale-105'
                            }`}
                            style={{ backgroundColor: c }}
                          />
                        );
                      })}
                    </div>
                  </div>
                </div>

                {/* Action Buttons */}
                <div className="flex justify-end gap-2 pt-3 border-t border-slate-200 dark:border-slate-700">
                  <Button type="button" variant="outline" size="sm" onClick={resetForm}>
                    {t('common.cancel')}
                  </Button>
                  <Button type="submit" size="sm">
                    {editingCatId ? t('common.save') : t('categories.addCategory')}
                  </Button>
                </div>
              </motion.form>
            )}
          </AnimatePresence>

          {/* Categories Grid List */}
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
            {filteredCategories.map((cat) => {
              const displayName = cat.isSystem
                ? (cat.key.startsWith('categories.') ? t(cat.key) : getLocalizedCategoryName(cat.name, t))
                : cat.name;

              return (
                <div
                  key={cat.id}
                  className="p-3.5 bg-white dark:bg-slate-800/80 rounded-2xl border border-slate-200 dark:border-slate-700 flex items-center justify-between hover:border-slate-300 dark:hover:border-slate-600 transition-all shadow-2xs"
                >
                  <div className="flex items-center gap-3 min-w-0">
                    <div
                      className="h-9 w-9 rounded-xl flex items-center justify-center shrink-0"
                      style={{
                        backgroundColor: `${cat.color || '#10B981'}20`,
                      }}
                    >
                      {renderIconComponent(cat.icon || 'Tag', cat.color || '#10B981')}
                    </div>
                    <div className="min-w-0">
                      <p className="text-xs font-bold text-slate-900 dark:text-white truncate">
                        {displayName}
                      </p>
                      <div className="flex items-center gap-2 mt-0.5">
                        <span
                          className={`text-[10px] font-semibold px-1.5 py-0.5 rounded-md ${
                            cat.kind === 'income'
                              ? 'bg-emerald-50 dark:bg-emerald-950/40 text-emerald-700 dark:text-emerald-300'
                              : 'bg-amber-50 dark:bg-amber-950/40 text-amber-700 dark:text-amber-300'
                          }`}
                        >
                          {cat.kind === 'income' ? t('categories.kindIncome') : t('categories.kindExpense')}
                        </span>
                        {cat.isSystem ? (
                          <span className="text-[10px] text-slate-400 font-medium">
                            {t('categories.systemBadge')}
                          </span>
                        ) : (
                          <span className="text-[10px] text-sky-600 dark:text-sky-400 font-medium">
                            {cat.isArchived
                              ? (locale === 'vi-VN' ? 'Đã lưu trữ' : 'Archived')
                              : (locale === 'vi-VN' ? 'Tùy chỉnh' : 'Custom')}
                          </span>
                        )}
                      </div>
                    </div>
                  </div>

                  {/* Actions: only editable and deletable if NOT a system category */}
                  <div className="flex items-center gap-1 shrink-0">
                    {!cat.isSystem && (
                      <>
                        <button
                          type="button"
                          onClick={() => handleStartEdit(cat)}
                          className="p-1.5 text-slate-400 hover:text-slate-600 dark:hover:text-slate-200 rounded-lg hover:bg-slate-100 dark:hover:bg-slate-700 transition-colors"
                          title={t('common.edit')}
                        >
                          <Edit2 className="h-3.5 w-3.5" />
                        </button>
                        <button
                          type="button"
                          onClick={() => setDeletingCat(cat)}
                          className="p-1.5 text-slate-400 hover:text-rose-600 rounded-lg hover:bg-rose-50 dark:hover:bg-rose-950/30 transition-colors"
                          title={t('common.delete')}
                        >
                          <Trash2 className="h-3.5 w-3.5" />
                        </button>
                      </>
                    )}
                  </div>
                </div>
              );
            })}
          </div>
        </div>

        {/* Delete Confirmation Dialog */}
        {deletingCat && (
          <div className="fixed inset-0 z-60 flex items-center justify-center p-4 bg-black/50 backdrop-blur-xs">
            <div className="bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-2xl p-5 w-full max-w-sm space-y-4 shadow-xl">
              <div className="flex items-center gap-3 text-rose-600 dark:text-rose-400">
                <ShieldAlert className="h-6 w-6" />
                <h4 className="text-sm font-bold text-slate-900 dark:text-white">
                  {t('categories.deleteConfirmTitle')}
                </h4>
              </div>
              <p className="text-xs text-slate-600 dark:text-slate-300">
                {t('categories.deleteConfirmDesc')}
              </p>
              <div className="flex justify-end gap-2 pt-2">
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() => setDeletingCat(null)}
                >
                  {t('common.cancel')}
                </Button>
                <Button
                  type="button"
                  variant="destructive"
                  size="sm"
                  onClick={handleConfirmDelete}
                >
                  {t('common.delete')}
                </Button>
              </div>
            </div>
          </div>
        )}

        {/* Modal Footer */}
        <div className="p-4 border-t border-slate-100 dark:border-slate-800 bg-slate-50/50 dark:bg-slate-900/50 flex justify-end">
          <Button type="button" variant="outline" size="sm" onClick={onClose}>
            {t('common.close')}
          </Button>
        </div>
      </motion.div>
    </div>
  );
};
