import React, { useState, useEffect } from 'react';
import { useI18n } from '../../../i18n/I18nContext';
import { useAuth } from '../../../providers/AuthProvider';
import { Group } from '../../../types/groups';
import {
  X,
  Receipt,
  Wallet,
  UserCheck,
  Calendar,
  FileText,
  AlertCircle,
} from 'lucide-react';
import { cn } from '../../../utils/cn';
import { Select } from '../../../components/ui/Select';
import {
  sanitizeIntegerInput,
  parseSafePositiveInteger,
} from '../../../utils/inputValidation';
import { useSecurityPinChallenge } from '../../../providers/SecurityPinChallengeProvider';
import { getLocalizedErrorMessage } from '../../../utils/localizationHelpers';

interface AddSharedExpenseModalProps {
  isOpen: boolean;
  onClose: () => void;
  group: Group;
  onAddExpense: (params: {
    groupId: string;
    title: string;
    amount: number;
    categoryId: string;
    categoryName: string;
    paidByType: 'user' | 'fund';
    paidByUserId?: string;
    paidByUserName: string;
    date: string;
    description?: string;
    securityPin?: string;
  }) => Promise<{ success: boolean; error?: string }> | { success: boolean; error?: string };
}

export const AddSharedExpenseModal: React.FC<AddSharedExpenseModalProps> = ({
  isOpen,
  onClose,
  group,
  onAddExpense,
}) => {
  const { t, formatCurrency, locale } = useI18n();
  const { currentUser } = useAuth();
  const { challengePinWithValue } = useSecurityPinChallenge();
  const currentUserId = currentUser?.id;

  const activeMembers = group.members.filter((m) => m.status === 'active');

  const currentMember = activeMembers.find(
    (m) => currentUserId && m.userId === currentUserId
  );
  const defaultPayerId = currentMember ? currentMember.userId : (activeMembers[0]?.userId || '');

  const [title, setTitle] = useState('');
  const [amountStr, setAmountStr] = useState('500000');
  const [categoryId, setCategoryId] = useState('utilities');
  const [date, setDate] = useState(() => new Date().toISOString().split('T')[0]);
  const [description, setDescription] = useState('');
  const [paidByType, setPaidByType] = useState<'user' | 'fund'>(
    group.sharedFund.balance > 0 ? 'fund' : 'user'
  );
  const [paidByUserId, setPaidByUserId] = useState<string>(defaultPayerId);
  const [error, setError] = useState('');

  const categories = [
    { id: 'utilities', nameVi: 'Điện nước & Tiện ích', nameEn: 'Utilities', icon: '💡' },
    { id: 'food', nameVi: 'Ăn uống & BBQ', nameEn: 'Food & Dining', icon: '🍽️' },
    { id: 'rent', nameVi: 'Tiền nhà & Phòng', nameEn: 'Rent & Housing', icon: '🏠' },
    { id: 'transport', nameVi: 'Di chuyển & Xăng xe', nameEn: 'Transportation', icon: '🛵' },
    { id: 'shopping', nameVi: 'Mua sắm chung', nameEn: 'Common Shopping', icon: '🛒' },
    { id: 'entertainment', nameVi: 'Giải trí & Hoạt động', nameEn: 'Entertainment', icon: '🎉' },
  ];

  const parsedAmount = parseSafePositiveInteger(amountStr).value;

  useEffect(() => {
    if (isOpen) {
      setError('');
      if (!paidByUserId && defaultPayerId) {
        setPaidByUserId(defaultPayerId);
      }
    }
  }, [isOpen, defaultPayerId, paidByUserId]);

  if (!isOpen) return null;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    if (!title.trim()) {
      setError(locale === 'vi-VN' ? 'Vui lòng nhập tên khoản chi' : 'Please enter expense title');
      return;
    }

    const amountCheck = parseSafePositiveInteger(amountStr);
    if (!amountCheck.valid || amountCheck.value <= 0) {
      setError(
        locale === 'vi-VN'
          ? 'Số tiền chi tiêu phải là số nguyên dương hợp lệ (> 0 ₫)'
          : 'Amount must be a valid positive number (> 0 ₫)'
      );
      return;
    }
    const validatedAmount = amountCheck.value;

    if (paidByType === 'fund' && group.sharedFund.balance < validatedAmount) {
      setError(
        locale === 'vi-VN'
          ? `Số dư quỹ nhóm (${formatCurrency(group.sharedFund.balance)}) không đủ để thanh toán khoản chi này`
          : `Group fund balance (${formatCurrency(group.sharedFund.balance)}) is insufficient`
      );
      return;
    }

    if (paidByType === 'user' && !paidByUserId) {
      setError(locale === 'vi-VN' ? 'Vui lòng chọn người đã thanh toán' : 'Please select who paid');
      return;
    }

    const selectedCat = categories.find((c) => c.id === categoryId) || categories[0];
    const payerMember = activeMembers.find((m) => m.userId === paidByUserId);

    const pin = await challengePinWithValue({
      actionName: locale === 'vi-VN' ? 'Thêm khoản chi chung' : 'Add Shared Expense',
      amount: validatedAmount,
    });
    if (!pin) return;

    const res = await onAddExpense({
      groupId: group.id,
      title: title.trim(),
      amount: validatedAmount,
      categoryId,
      categoryName: locale === 'vi-VN' ? selectedCat.nameVi : selectedCat.nameEn,
      paidByType,
      paidByUserId: paidByType === 'user' ? paidByUserId : undefined,
      paidByUserName:
        paidByType === 'user'
          ? payerMember?.name || currentUser?.name || (locale === 'vi-VN' ? 'Thành viên' : 'Member')
          : locale === 'vi-VN'
          ? 'Quỹ chung'
          : 'Shared Fund',
      date,
      description: description.trim() || undefined,
      securityPin: pin,
    });

    if (!res.success) {
      setError(getLocalizedErrorMessage(res.error, locale, t));
    } else {
      onClose();
      resetForm();
    }
  };

  const resetForm = () => {
    setTitle('');
    setAmountStr('500000');
    setDescription('');
    setError('');
    setPaidByUserId(defaultPayerId);
    setPaidByType(group.sharedFund.balance > 0 ? 'fund' : 'user');
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-sm animate-fadeIn">
      <div className="bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-3xl w-full max-w-lg shadow-2xl overflow-hidden flex flex-col max-h-[90vh] animate-scaleUp">
        {/* Header */}
        <div className="flex items-center justify-between px-6 py-4 border-b border-slate-100 dark:border-slate-800 bg-slate-50/50 dark:bg-slate-950/40 shrink-0">
          <div className="flex items-center gap-2.5">
            <div className="p-2 rounded-2xl bg-emerald-50 dark:bg-emerald-950/80 text-emerald-600 dark:text-emerald-400">
              <Receipt className="h-5 w-5" />
            </div>
            <div>
              <h2 className="text-base sm:text-lg font-bold text-slate-900 dark:text-slate-100">
                {locale === 'vi-VN' ? 'Thêm khoản chi tiêu nhóm' : 'Add Group Expense'}
              </h2>
              <p className="text-xs text-slate-500 dark:text-slate-400">
                {group.name}
              </p>
            </div>
          </div>
          <button
            onClick={onClose}
            className="p-2 rounded-xl text-slate-400 hover:text-slate-600 dark:hover:text-slate-200 hover:bg-slate-100 dark:hover:bg-slate-800 transition-colors"
          >
            <X className="h-5 w-5" />
          </button>
        </div>

        {/* Error Banner */}
        {error && (
          <div className="mx-6 mt-4 p-3 rounded-2xl bg-rose-50 dark:bg-rose-950/50 border border-rose-200 dark:border-rose-800 text-rose-700 dark:text-rose-300 text-xs flex items-center gap-2">
            <AlertCircle className="h-4 w-4 shrink-0" />
            <span>{error}</span>
          </div>
        )}

        {/* Form Body */}
        <form onSubmit={handleSubmit} className="p-6 space-y-4 overflow-y-auto flex-1">
          {/* Title */}
          <div>
            <label className="block text-xs font-bold text-slate-700 dark:text-slate-300 mb-1.5">
              {locale === 'vi-VN' ? 'Tên khoản chi' : 'Expense Title'} <span className="text-rose-500">*</span>
            </label>
            <input
              type="text"
              id="expense-title-input"
              value={title}
              onChange={(e) => setTitle(e.target.value)}
              placeholder={locale === 'vi-VN' ? 'Ví dụ: Tiền điện tháng 9, Ăn tối liên hoan...' : 'e.g. September electricity bill...'}
              className="w-full px-4 py-2.5 rounded-xl border border-slate-200 dark:border-slate-800 bg-slate-50/50 dark:bg-slate-950 text-slate-900 dark:text-slate-100 text-sm focus:outline-none focus:ring-2 focus:ring-emerald-500"
              autoFocus
            />
          </div>

          {/* Amount */}
          <div>
            <label className="block text-xs font-bold text-slate-700 dark:text-slate-300 mb-1.5">
              {locale === 'vi-VN' ? 'Số tiền (₫)' : 'Amount (₫)'} <span className="text-rose-500">*</span>
            </label>
            <div className="relative">
              <input
                type="text"
                inputMode="numeric"
                id="expense-amount-input"
                value={amountStr}
                onChange={(e) => setAmountStr(sanitizeIntegerInput(e.target.value))}
                className="w-full px-4 py-2.5 rounded-xl border border-slate-200 dark:border-slate-800 bg-slate-50/50 dark:bg-slate-950 text-slate-900 dark:text-slate-100 text-base font-bold font-mono focus:outline-none focus:ring-2 focus:ring-emerald-500"
              />
              <span className="absolute right-4 top-2.5 text-xs text-slate-400 font-medium">
                {formatCurrency(parsedAmount)}
              </span>
            </div>
          </div>

          {/* Category */}
          <div>
            <label className="block text-xs font-bold text-slate-700 dark:text-slate-300 mb-1.5">
              {locale === 'vi-VN' ? 'Danh mục' : 'Category'}
            </label>
            <div className="grid grid-cols-2 sm:grid-cols-3 gap-2">
              {categories.map((c) => (
                <button
                  key={c.id}
                  type="button"
                  onClick={() => setCategoryId(c.id)}
                  className={cn(
                    'p-2.5 rounded-xl border text-left text-xs font-semibold flex items-center gap-2 transition-all',
                    categoryId === c.id
                      ? 'bg-emerald-50 dark:bg-emerald-950/80 border-emerald-500 text-emerald-800 dark:text-emerald-300 shadow-sm'
                      : 'border-slate-200 dark:border-slate-800 text-slate-600 dark:text-slate-400 hover:bg-slate-50 dark:hover:bg-slate-800/60'
                  )}
                >
                  <span className="text-base">{c.icon}</span>
                  <span className="truncate">{locale === 'vi-VN' ? c.nameVi : c.nameEn}</span>
                </button>
              ))}
            </div>
          </div>

          {/* Paid By Method */}
          <div>
            <label className="block text-xs font-bold text-slate-700 dark:text-slate-300 mb-1.5">
              {locale === 'vi-VN' ? 'Nguồn chi trả' : 'Paid By'} <span className="text-rose-500">*</span>
            </label>
            <div className="grid grid-cols-2 gap-2 mb-2.5">
              <button
                type="button"
                id="btn-payby-fund"
                onClick={() => setPaidByType('fund')}
                className={cn(
                  'p-3 rounded-2xl border text-left transition-all flex items-start gap-2.5',
                  paidByType === 'fund'
                    ? 'bg-emerald-50 dark:bg-emerald-950/80 border-emerald-500 text-emerald-900 dark:text-emerald-200 shadow-sm'
                    : 'border-slate-200 dark:border-slate-800 text-slate-600 dark:text-slate-400 hover:bg-slate-50 dark:hover:bg-slate-800/60'
                )}
              >
                <Wallet className="h-4 w-4 mt-0.5 text-emerald-600 dark:text-emerald-400 shrink-0" />
                <div>
                  <p className="text-xs font-bold">
                    {locale === 'vi-VN' ? 'Chi từ Quỹ chung' : 'Group Fund'}
                  </p>
                  <p className="text-[11px] text-slate-400 mt-0.5">
                    {locale === 'vi-VN' ? 'Số dư:' : 'Balance:'} {formatCurrency(group.sharedFund.balance)}
                  </p>
                </div>
              </button>

              <button
                type="button"
                id="btn-payby-user"
                onClick={() => setPaidByType('user')}
                className={cn(
                  'p-3 rounded-2xl border text-left transition-all flex items-start gap-2.5',
                  paidByType === 'user'
                    ? 'bg-emerald-50 dark:bg-emerald-950/80 border-emerald-500 text-emerald-900 dark:text-emerald-200 shadow-sm'
                    : 'border-slate-200 dark:border-slate-800 text-slate-600 dark:text-slate-400 hover:bg-slate-50 dark:hover:bg-slate-800/60'
                )}
              >
                <UserCheck className="h-4 w-4 mt-0.5 text-emerald-600 dark:text-emerald-400 shrink-0" />
                <div>
                  <p className="text-xs font-bold">
                    {locale === 'vi-VN' ? 'Thành viên thanh toán' : 'Member Paid'}
                  </p>
                  <p className="text-[11px] text-slate-400 mt-0.5">
                    {locale === 'vi-VN' ? 'Ghi nhận chi tiêu' : 'Record payer'}
                  </p>
                </div>
              </button>
            </div>

            {/* Member selector if user paid */}
            {paidByType === 'user' && (
              <div className="pt-1">
                <Select
                  id="expense-payer-select"
                  value={paidByUserId}
                  onChange={(val) => setPaidByUserId(val)}
                  options={activeMembers.map((m) => ({
                    value: m.userId,
                    label: `${m.name}${m.userId === currentUserId ? ` (${locale === 'vi-VN' ? 'Bạn' : 'You'})` : ''}`,
                  }))}
                  size="md"
                />
              </div>
            )}
          </div>

          {/* Date */}
          <div>
            <label className="block text-xs font-bold text-slate-700 dark:text-slate-300 mb-1.5 flex items-center gap-1.5">
              <Calendar className="h-3.5 w-3.5 text-slate-400" />
              <span>{locale === 'vi-VN' ? 'Ngày chi tiêu' : 'Expense Date'}</span>
            </label>
            <input
              type="date"
              id="expense-date-input"
              value={date}
              onChange={(e) => setDate(e.target.value)}
              className="w-full px-4 py-2.5 rounded-xl border border-slate-200 dark:border-slate-800 bg-slate-50/50 dark:bg-slate-950 text-slate-900 dark:text-slate-100 text-xs font-semibold focus:outline-none focus:ring-2 focus:ring-emerald-500"
            />
          </div>

          {/* Description */}
          <div>
            <label className="block text-xs font-bold text-slate-700 dark:text-slate-300 mb-1.5 flex items-center gap-1.5">
              <FileText className="h-3.5 w-3.5 text-slate-400" />
              <span>{locale === 'vi-VN' ? 'Ghi chú thêm (không bắt buộc)' : 'Notes / Description (Optional)'}</span>
            </label>
            <textarea
              id="expense-desc-input"
              rows={2}
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              placeholder={locale === 'vi-VN' ? 'Ghi chú hóa đơn hoặc địa điểm...' : 'Optional notes, receipts, place...'}
              className="w-full px-4 py-2.5 rounded-xl border border-slate-200 dark:border-slate-800 bg-slate-50/50 dark:bg-slate-950 text-slate-900 dark:text-slate-100 text-xs focus:outline-none focus:ring-2 focus:ring-emerald-500 resize-none"
            />
          </div>

          {/* Footer Actions */}
          <div className="pt-3 border-t border-slate-100 dark:border-slate-800 flex items-center justify-end gap-2.5">
            <button
              type="button"
              onClick={onClose}
              className="px-4 py-2.5 rounded-xl border border-slate-200 dark:border-slate-800 hover:bg-slate-100 dark:hover:bg-slate-800 text-slate-600 dark:text-slate-300 text-xs font-semibold transition-colors"
            >
              {locale === 'vi-VN' ? 'Hủy' : 'Cancel'}
            </button>

            <button
              type="submit"
              id="btn-submit-group-expense"
              className="px-5 py-2.5 rounded-xl bg-emerald-600 hover:bg-emerald-700 text-white text-xs font-semibold shadow-sm transition-all"
            >
              {locale === 'vi-VN' ? 'Thêm khoản chi' : 'Add Expense'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};
