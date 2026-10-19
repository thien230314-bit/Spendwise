import React, { useState } from 'react';
import {
  X,
  Shield,
  Layers,
  CheckCircle2,
  AlertCircle,
  ExternalLink,
  Lock,
  ArrowRight,
  Eye,
  SlidersHorizontal,
} from 'lucide-react';
import { useI18n } from '../../../i18n/I18nContext';
import { cn } from '../../../utils/cn';
import { PopupItem, POPUP_INVENTORY_BY_PAGE } from './popupInventory';

interface PopupInspectorDrawerProps {
  pageKey: string;
  isOpen: boolean;
  onClose: () => void;
  onSelectPopup: (popupId: string, stateMode: 'default' | 'prefill' | 'error') => void;
  selectedPopupId: string | null;
}

export const PopupInspectorDrawer: React.FC<PopupInspectorDrawerProps> = ({
  pageKey,
  isOpen,
  onClose,
  onSelectPopup,
  selectedPopupId,
}) => {
  const { locale } = useI18n();
  const [filterCategory, setFilterCategory] = useState<string>('ALL');

  if (!isOpen) return null;

  const popups: PopupItem[] = POPUP_INVENTORY_BY_PAGE[pageKey] || [];

  const categories = ['ALL', 'Create', 'Edit', 'Delete', 'Confirm', 'PIN', 'Details'];

  const filteredPopups = popups.filter((p) => {
    if (filterCategory === 'ALL') return true;
    return p.category === filterCategory;
  });

  const getCategoryBadgeClass = (cat: string) => {
    switch (cat) {
      case 'Create':
        return 'bg-emerald-50 text-emerald-700 dark:bg-emerald-950/80 dark:text-emerald-300 border-emerald-200 dark:border-emerald-800';
      case 'Edit':
        return 'bg-amber-50 text-amber-700 dark:bg-amber-950/80 dark:text-amber-300 border-amber-200 dark:border-amber-800';
      case 'Delete':
        return 'bg-rose-50 text-rose-700 dark:bg-rose-950/80 dark:text-rose-300 border-rose-200 dark:border-rose-800';
      case 'PIN':
      case 'Confirm':
        return 'bg-indigo-50 text-indigo-700 dark:bg-indigo-950/80 dark:text-indigo-300 border-indigo-200 dark:border-indigo-800';
      case 'Details':
      default:
        return 'bg-slate-50 text-slate-700 dark:bg-slate-800 dark:text-slate-300 border-slate-200 dark:border-slate-700';
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex justify-end bg-slate-950/60 backdrop-blur-xs animate-in fade-in duration-150">
      <div className="w-full max-w-md md:max-w-lg bg-white dark:bg-slate-900 border-l border-slate-200 dark:border-slate-800 h-full flex flex-col shadow-2xl animate-in slide-in-from-right duration-200">
        {/* Drawer Header */}
        <div className="p-5 border-b border-slate-200 dark:border-slate-800 flex items-center justify-between shrink-0">
          <div>
            <div className="flex items-center gap-2">
              <span className="px-2 py-0.5 rounded text-[10px] font-black uppercase tracking-wider bg-emerald-100 text-emerald-800 dark:bg-emerald-950 dark:text-emerald-300 border border-emerald-300 dark:border-emerald-800">
                INSPECTOR
              </span>
              <h3 className="text-base font-bold text-slate-900 dark:text-white">
                {locale === 'vi-VN' ? 'Danh mục Popup UI Inspector' : 'Popup UI Inspector Catalog'}
              </h3>
            </div>
            <p className="text-xs text-slate-500 dark:text-slate-400 mt-1">
              {locale === 'vi-VN'
                ? `Khám phá & kiểm tra ${popups.length} popup thực tế của phân hệ này`
                : `Inspect ${popups.length} verified production popups for this module`}
            </p>
          </div>

          <button
            onClick={onClose}
            className="p-2 rounded-xl text-slate-400 hover:text-slate-700 dark:hover:text-slate-200 hover:bg-slate-100 dark:hover:bg-slate-800 transition cursor-pointer"
          >
            <X className="h-5 w-5" />
          </button>
        </div>

        {/* Category Filters */}
        <div className="px-5 py-3 border-b border-slate-100 dark:border-slate-800/80 bg-slate-50/50 dark:bg-slate-900/50 shrink-0">
          <div className="flex items-center gap-1.5 overflow-x-auto no-scrollbar">
            {categories.map((cat) => (
              <button
                key={cat}
                onClick={() => setFilterCategory(cat)}
                className={cn(
                  'px-2.5 py-1 rounded-lg text-xs font-semibold whitespace-nowrap transition-all cursor-pointer',
                  filterCategory === cat
                    ? 'bg-emerald-600 text-white shadow-2xs'
                    : 'text-slate-600 dark:text-slate-400 hover:bg-slate-100 dark:hover:bg-slate-800'
                )}
              >
                {cat}
              </button>
            ))}
          </div>
        </div>

        {/* Popup List */}
        <div className="flex-1 overflow-y-auto p-5 space-y-3.5">
          {filteredPopups.map((popup) => {
            const isSelected = selectedPopupId === popup.id;

            return (
              <div
                key={popup.id}
                className={cn(
                  'rounded-2xl p-4 border transition-all space-y-3 bg-white dark:bg-slate-900',
                  isSelected
                    ? 'border-emerald-500 dark:border-emerald-500 shadow-md ring-2 ring-emerald-500/20'
                    : 'border-slate-200 dark:border-slate-800 hover:border-slate-300 dark:hover:border-slate-700'
                )}
              >
                <div className="flex items-start justify-between gap-3">
                  <div className="space-y-1 min-w-0">
                    <div className="flex items-center gap-2 flex-wrap">
                      <span
                        className={cn(
                          'px-2 py-0.5 rounded-full text-[10px] font-bold border',
                          getCategoryBadgeClass(popup.category)
                        )}
                      >
                        {popup.category}
                      </span>
                      <span className="text-[10px] font-mono text-slate-400 truncate max-w-[200px]">
                        {popup.componentName}
                      </span>
                    </div>

                    <h4 className="text-sm font-bold text-slate-900 dark:text-white leading-snug">
                      {locale === 'vi-VN' ? popup.nameVi : popup.nameEn}
                    </h4>
                    <p className="text-xs text-slate-500 dark:text-slate-400 leading-relaxed">
                      {locale === 'vi-VN' ? popup.descriptionVi : popup.descriptionEn}
                    </p>
                  </div>
                </div>

                <div className="pt-2 border-t border-slate-100 dark:border-slate-800 flex items-center justify-between gap-2">
                  <span className="text-[10px] font-bold text-emerald-600 dark:text-emerald-400 flex items-center gap-1">
                    <CheckCircle2 className="h-3 w-3" />
                    <span>{popup.status}</span>
                  </span>

                  <div className="flex items-center gap-1.5">
                    {/* View Standard State */}
                    <button
                      onClick={() => {
                        onSelectPopup(popup.id, 'default');
                        onClose();
                      }}
                      className="px-3 py-1.5 rounded-xl bg-emerald-600 hover:bg-emerald-700 text-white text-xs font-semibold shadow-2xs transition cursor-pointer flex items-center gap-1"
                    >
                      <Eye className="h-3.5 w-3.5" />
                      <span>{locale === 'vi-VN' ? 'Xem giao diện' : 'Open UI'}</span>
                    </button>

                    {/* Quick Prefilled Mode for Edit popups */}
                    {popup.hasEditPrefill && (
                      <button
                        onClick={() => {
                          onSelectPopup(popup.id, 'prefill');
                          onClose();
                        }}
                        className="px-2.5 py-1.5 rounded-xl bg-amber-500 hover:bg-amber-600 text-white text-xs font-semibold shadow-2xs transition cursor-pointer"
                        title={locale === 'vi-VN' ? 'Mở kèm dữ liệu mẫu đã điền' : 'Open with prefilled sample data'}
                      >
                        Prefilled
                      </button>
                    )}
                  </div>
                </div>
              </div>
            );
          })}
        </div>

        {/* Drawer Footer Notice */}
        <div className="p-4 border-t border-slate-200 dark:border-slate-800 bg-slate-50 dark:bg-slate-900/80 text-[11px] text-slate-500 dark:text-slate-400 shrink-0 flex items-center gap-2">
          <Shield className="h-4 w-4 text-emerald-600 dark:text-emerald-400 shrink-0" />
          <span>
            {locale === 'vi-VN'
              ? 'DEV Safe Isolated Mode: Mọi thao tác đều không gọi API hay thay đổi database thật.'
              : 'DEV Safe Isolated Mode: No mutations or real API calls will be executed.'}
          </span>
        </div>
      </div>
    </div>
  );
};
