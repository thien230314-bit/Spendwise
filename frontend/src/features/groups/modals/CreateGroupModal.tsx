import React, { useState } from 'react';
import { useI18n } from '../../../i18n/I18nContext';
import { GroupCategory } from '../../../types/groups';
import { X, Users, Sparkles, AlertCircle } from 'lucide-react';
import { cn } from '../../../utils/cn';
import { useSecurityPinChallenge } from '../../../providers/SecurityPinChallengeProvider';

interface CreateGroupModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSubmit: (params: { name: string; type: GroupCategory; description: string; currency?: string; securityPin?: string }) => void;
}

export const CreateGroupModal: React.FC<CreateGroupModalProps> = ({ isOpen, onClose, onSubmit }) => {
  const { t, locale } = useI18n();
  const { challengePinWithValue } = useSecurityPinChallenge();
  const [name, setName] = useState('');
  const [type, setType] = useState<GroupCategory>('Roommates');
  const [description, setDescription] = useState('');
  const [error, setError] = useState('');

  if (!isOpen) return null;

  const categories: { id: GroupCategory; labelVi: string; labelEn: string; icon: string }[] = [
    { id: 'Roommates', labelVi: 'Phòng trọ', labelEn: 'Roommates', icon: '🏠' },
    { id: 'Trip', labelVi: 'Du lịch', labelEn: 'Trip', icon: '✈️' },
    { id: 'Family', labelVi: 'Gia đình', labelEn: 'Family', icon: '👨‍👩‍👧‍👦' },
    { id: 'Couple', labelVi: 'Couple', labelEn: 'Couple', icon: '💑' },
    { id: 'Friends', labelVi: 'Nhóm bạn', labelEn: 'Friends', icon: '🎉' },
    { id: 'Other', labelVi: 'Khác', labelEn: 'Other', icon: '📁' },
  ];

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!name.trim()) {
      setError(locale === 'vi-VN' ? 'Vui lòng nhập tên nhóm' : 'Please enter group name');
      return;
    }

    const pin = await challengePinWithValue({
      actionName: locale === 'vi-VN' ? 'Tạo nhóm chi tiêu mới' : 'Create Shared Group',
    });
    if (!pin) return;

    onSubmit({
      name: name.trim(),
      type,
      description: description.trim(),
      currency: 'VND',
      securityPin: pin,
    });

    onClose();
    setName('');
    setDescription('');
    setError('');
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-sm animate-fadeIn">
      <div className="bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-2xl w-full max-w-lg shadow-2xl overflow-hidden animate-scaleUp">
        {/* Modal Header */}
        <div className="flex items-center justify-between px-6 py-4 border-b border-slate-100 dark:border-slate-800 bg-slate-50/50 dark:bg-slate-950/40">
          <div className="flex items-center gap-2.5">
            <div className="p-2 rounded-xl bg-emerald-50 dark:bg-emerald-950/80 text-emerald-600 dark:text-emerald-400">
              <Users className="h-5 w-5" />
            </div>
            <div>
              <h2 className="text-lg font-bold text-slate-900 dark:text-slate-100">
                {t('groups.createModalTitle')}
              </h2>
              <p className="text-xs text-slate-500 dark:text-slate-400">
                {locale === 'vi-VN' ? 'Bạn sẽ là Trưởng nhóm của nhóm mới này' : 'You will be the Owner of this new group'}
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

        {/* Form Body */}
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

          {/* Group Name */}
          <div>
            <label className="block text-xs font-semibold text-slate-700 dark:text-slate-300 mb-1.5">
              {t('groups.groupNameLabel')} <span className="text-rose-500">*</span>
            </label>
            <input
              type="text"
              required
              placeholder={locale === 'vi-VN' ? 'VD: Phòng trọ 202, Du lịch Phú Quốc, Quỹ nhóm bạn...' : 'e.g. Room 202, Trip to Beach...'}
              value={name}
              onChange={(e) => {
                setName(e.target.value);
                if (error) setError('');
              }}
              className="w-full px-3.5 py-2.5 rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-950 text-slate-900 dark:text-slate-100 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
            />
          </div>

          {/* Category Select */}
          <div>
            <label className="block text-xs font-semibold text-slate-700 dark:text-slate-300 mb-1.5">
              {t('groups.groupTypeLabel')}
            </label>
            <div className="grid grid-cols-3 gap-2">
              {categories.map((cat) => (
                <button
                  type="button"
                  key={cat.id}
                  onClick={() => setType(cat.id)}
                  className={cn(
                    'flex items-center gap-2 p-2.5 rounded-xl text-xs font-medium border transition-all text-left',
                    type === cat.id
                      ? 'bg-emerald-50 dark:bg-emerald-950/70 border-emerald-500 text-emerald-700 dark:text-emerald-300 shadow-sm'
                      : 'border-slate-200 dark:border-slate-800 hover:border-slate-300 dark:hover:border-slate-700 text-slate-700 dark:text-slate-300 bg-white dark:bg-slate-950'
                  )}
                >
                  <span className="text-base">{cat.icon}</span>
                  <span className="truncate">{locale === 'vi-VN' ? cat.labelVi : cat.labelEn}</span>
                </button>
              ))}
            </div>
          </div>

          {/* Description */}
          <div>
            <label className="block text-xs font-semibold text-slate-700 dark:text-slate-300 mb-1.5">
              {t('groups.descriptionLabel')}
            </label>
            <textarea
              rows={2}
              placeholder={locale === 'vi-VN' ? 'Mô tả mục đích hoạt động và kế hoạch quỹ nhóm...' : 'Describe group purpose and fund plans...'}
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              className="w-full px-3.5 py-2 rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-950 text-slate-900 dark:text-slate-100 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500 resize-none"
            />
          </div>

          {/* Currency */}
          <div className="flex items-center justify-between p-3 bg-slate-50 dark:bg-slate-950/60 rounded-xl border border-slate-100 dark:border-slate-800 text-xs text-slate-600 dark:text-slate-400">
            <span>{t('groups.currencyLabel')}</span>
            <span className="font-bold text-slate-900 dark:text-slate-200">VND (₫)</span>
          </div>

          {/* Footer Actions */}
          <div className="flex items-center justify-end gap-3 pt-2">
            <button
              type="button"
              onClick={onClose}
              className="px-4 py-2 text-xs font-medium text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-slate-200 transition-colors"
            >
              {locale === 'vi-VN' ? 'Hủy' : 'Cancel'}
            </button>
            <button
              type="submit"
              className="px-5 py-2.5 rounded-xl bg-emerald-600 hover:bg-emerald-700 text-white text-xs font-semibold shadow-sm shadow-emerald-600/30 hover:shadow transition-all"
            >
              {t('groups.saveGroupBtn')}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};
