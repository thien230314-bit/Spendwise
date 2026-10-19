import React, { useState, useMemo, useEffect } from 'react';
import { useSearchParams } from 'react-router-dom';
import {
  Target,
  Plus,
  ArrowDownLeft,
  ArrowUpRight,
  TrendingUp,
  Calendar,
  CheckCircle2,
  X,
  Laptop,
  Plane,
  Heart,
  Car,
  Home,
  Shield,
  Clock,
  Sparkles,
  Info,
  Layers,
  ArrowRight,
  Wallet,
  AlertCircle,
  Edit3,
  Trash2,
  AlertTriangle,
} from 'lucide-react';
import { useI18n } from '../../i18n/I18nContext';
import { useBalanceVisibility } from '../../providers/BalanceVisibilityProvider';
import { useFinancial } from '../../providers/FinancialProvider';
import { GoalItem } from '../../types';
import { formatMoney } from '../../utils/money';
import { formatDate } from '../../utils/date';
import { Button } from '../../components/ui/Button';
import { Select } from '../../components/ui/Select';
import {
  sanitizeIntegerInput,
  parseSafePositiveInteger,
  isValidDateString,
  isDateTodayOrFuture,
  getLocalDateString,
  getFutureDateString,
} from '../../utils/inputValidation';
import { cn } from '../../utils/cn';
import { getLocalizedGoalCategory, getLocalizedErrorMessage } from '../../utils/localizationHelpers';
import { useSecurityPinChallenge } from '../../providers/SecurityPinChallengeProvider';
import { GoalsSkeleton } from '../../components/skeletons/GoalsSkeleton';

export const GoalsPage: React.FC = () => {
  const { t, locale } = useI18n();
  const { isHidden } = useBalanceVisibility();
  const { challengePinWithValue } = useSecurityPinChallenge();
  const {
    isLoading,
    goals,
    wallets,
    categories,
    createGoal,
    editGoal,
    deleteGoal,
    contributeToGoal,
    withdrawFromGoal,
  } = useFinancial();
  const [searchParams] = useSearchParams();


  // Modals
  const [isCreateOpen, setIsCreateOpen] = useState(false);
  const [editingGoal, setEditingGoal] = useState<GoalItem | null>(null);
  const [deletingGoal, setDeletingGoal] = useState<GoalItem | null>(null);
  const [deleteError, setDeleteError] = useState<string | null>(null);
  const [contributingGoal, setContributingGoal] = useState<GoalItem | null>(null);
  const [withdrawingGoal, setWithdrawingGoal] = useState<GoalItem | null>(null);
  const [selectedGoalDetails, setSelectedGoalDetails] = useState<GoalItem | null>(null);
  const [toastMessage, setToastMessage] = useState<string | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  // Deep linking
  useEffect(() => {
    const action = searchParams.get('action');
    if (action === 'create') setIsCreateOpen(true);

    const goalId = searchParams.get('id') || searchParams.get('goalId');
    if (goalId) {
      const match = goals.find((g) => g.id === goalId);
      if (match) {
        if (action === 'contribute') setContributingGoal(match);
        else if (action === 'withdraw') setWithdrawingGoal(match);
        else setSelectedGoalDetails(match);
      }
    }
  }, [searchParams, goals]);

  // Form states for Create / Edit Goal
  const [newTitle, setNewTitle] = useState('');
  const [newCategoryId, setNewCategoryId] = useState('');
  const [newTargetAmount, setNewTargetAmount] = useState('');
  const [newInitialAmount, setNewInitialAmount] = useState('0');
  const [newDeadline, setNewDeadline] = useState(() => getFutureDateString(180));
  const [newIconName, setNewIconName] = useState('Target');

  // Form states for Contribute / Withdraw
  const defaultWalletId = wallets.find((w) => w.isDefault)?.id || wallets[0]?.id || '';
  const [newWalletId, setNewWalletId] = useState<string>(defaultWalletId);
  const [actionWalletId, setActionWalletId] = useState<string>(defaultWalletId);
  const [actionAmount, setActionAmount] = useState('');

  // Keep actionWalletId synced if wallets change
  useEffect(() => {
    if (!actionWalletId && wallets.length > 0) {
      setActionWalletId(wallets.find((w) => w.isDefault)?.id || wallets[0]?.id || '');
    }
  }, [wallets, actionWalletId]);

  const showToast = (msg: string) => {
    setToastMessage(msg);
    setTimeout(() => setToastMessage(null), 3000);
  };

  // Top summary stats
  const overallStats = useMemo(() => {
    let totalTarget = 0;
    let totalSaved = 0;

    goals.forEach((g) => {
      totalTarget += g.targetAmount;
      totalSaved += g.currentAmount;
    });

    const progressPercentage =
      totalTarget > 0 ? Math.min(100, Math.round((totalSaved / totalTarget) * 100)) : 0;

    return {
      activeCount: goals.length,
      totalTarget,
      totalSaved,
      progressPercentage,
    };
  }, [goals]);

  // Icon resolver
  const getGoalIcon = (iconName?: string) => {
    switch (iconName) {
      case 'Laptop':
        return <Laptop className="h-5 w-5" />;
      case 'Plane':
        return <Plane className="h-5 w-5" />;
      case 'Heart':
        return <Heart className="h-5 w-5" />;
      case 'Car':
        return <Car className="h-5 w-5" />;
      case 'Home':
        return <Home className="h-5 w-5" />;
      default:
        return <Target className="h-5 w-5" />;
    }
  };

  const iconOptions = useMemo(
    () => [
      { name: 'Target', label: t('goals.iconTarget') },
      { name: 'Laptop', label: t('goals.iconEquipment') },
      { name: 'Plane', label: t('goals.iconTravel') },
      { name: 'Heart', label: t('goals.iconHealth') },
      { name: 'Car', label: t('goals.iconVehicle') },
      { name: 'Home', label: t('goals.iconHousing') },
    ],
    [t]
  );

  // Selected wallet for contribution/withdraw calculation
  const selectedWallet = actionWalletId
    ? wallets.find((w) => w.id === actionWalletId)
    : wallets.find((w) => w.isDefault) || wallets[0];
  const { valid: isActionAmountValid, value: parsedActionAmount } = parseSafePositiveInteger(actionAmount);

  // Contribute preview calculation
  const contributeInsufficient =
    contributingGoal && selectedWallet && parsedActionAmount > selectedWallet.balance;
  const previewWalletAfterContribute = selectedWallet
    ? Math.max(0, selectedWallet.balance - parsedActionAmount)
    : 0;
  const previewGoalAfterContribute = contributingGoal
    ? contributingGoal.currentAmount + parsedActionAmount
    : 0;

  // Withdraw preview calculation
  const withdrawExcess =
    withdrawingGoal && parsedActionAmount > withdrawingGoal.currentAmount;
  const previewWalletAfterWithdraw = selectedWallet
    ? selectedWallet.balance + parsedActionAmount
    : 0;
  const previewGoalAfterWithdraw = withdrawingGoal
    ? Math.max(0, withdrawingGoal.currentAmount - parsedActionAmount)
    : 0;

  // Handlers
  const handleOpenCreate = () => {
    setNewTitle('');
    setNewCategoryId('');
    setNewTargetAmount('15000000');
    setNewInitialAmount('0');
    setNewWalletId(defaultWalletId);
    setNewDeadline(getFutureDateString(180));
    setNewIconName('Target');
    setErrorMessage(null);
    setIsCreateOpen(true);
  };

  const handleSaveCreate = async (e: React.FormEvent) => {
    e.preventDefault();
    if (isSubmitting) return;
    const { valid: isTargetValid, value: target } = parseSafePositiveInteger(newTargetAmount);
    const { valid: isInitialValid, value: initial } = newInitialAmount.trim()
      ? parseSafePositiveInteger(newInitialAmount)
      : { valid: true, value: 0 };

    if (!isTargetValid || target <= 0) {
      setErrorMessage(t('goals.errorTargetAmount'));
      return;
    }

    if (!newDeadline || !isValidDateString(newDeadline)) {
      setErrorMessage(
        locale === 'vi-VN' ? 'Vui lòng chọn ngày đến hạn hợp lệ (YYYY-MM-DD).' : 'Please select a valid deadline date.'
      );
      return;
    }

    if (!isDateTodayOrFuture(newDeadline)) {
      setErrorMessage(
        locale === 'vi-VN' ? 'Thời hạn hoàn thành mục tiêu không thể ở quá khứ.' : 'Goal deadline cannot be in the past.'
      );
      return;
    }

    if (!isInitialValid || initial < 0) {
      setErrorMessage(locale === 'vi-VN' ? 'Số tiền khởi tạo không hợp lệ.' : 'Initial amount is invalid.');
      return;
    }

    if (initial > target) {
      setErrorMessage(
        locale === 'vi-VN'
          ? 'Số tiền khởi tạo không thể lớn hơn mục tiêu.'
          : 'Initial amount cannot exceed target amount.'
      );
      return;
    }

    const targetWalletId = newWalletId || defaultWalletId;
    const selectedSourceWallet = targetWalletId ? wallets.find((w) => w.id === targetWalletId) : undefined;
    if (initial > 0) {
      if (!selectedSourceWallet) {
        setErrorMessage(
          locale === 'vi-VN' ? 'Vui lòng chọn ví nguồn hợp lệ.' : 'Please select a valid source wallet.'
        );
        return;
      }
      if (initial > selectedSourceWallet.balance) {
        setErrorMessage(
          locale === 'vi-VN'
            ? `Số dư ví "${selectedSourceWallet.name}" không đủ để phân bổ (${formatMoney(selectedSourceWallet.balance, locale)}).`
            : `Wallet "${selectedSourceWallet.name}" balance is insufficient for initial allocation.`
        );
        return;
      }
    }

    const pinValue = await challengePinWithValue({
      actionName: locale === 'vi-VN' ? 'Tạo mục tiêu tích lũy mới' : 'Create Financial Goal',
      amount: initial > 0 ? initial : target,
    });
    if (pinValue === null) return;

    setIsSubmitting(true);
    try {
      const res = await createGoal({
        title: newTitle || (locale === 'vi-VN' ? 'Mục tiêu mới' : 'New Goal'),
        categoryId: newCategoryId || undefined,
        category: categories.find((c) => c.id === newCategoryId)?.name || undefined,
        targetAmount: target,
        initialAmount: initial,
        walletId: initial > 0 ? selectedSourceWallet?.id : undefined,
        deadline: newDeadline,
        iconName: newIconName,
        colorHex: '#10B981',
        securityPin: pinValue,
      });

      if (res.success) {
        setIsCreateOpen(false);
        showToast(t('goals.toastCreated', { title: newTitle || (locale === 'vi-VN' ? 'Mục tiêu mới' : 'New Goal') }));
      } else {
        setErrorMessage(
          getLocalizedErrorMessage(res.error, locale, t, 'goals.errorCreateFailed')
        );
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleOpenEdit = (g: GoalItem) => {
    setEditingGoal(g);
    setNewTitle(g.title);
    setNewCategoryId(g.categoryId || '');
    setNewTargetAmount(g.targetAmount.toString());
    setNewDeadline(g.deadline || '2026-12-31');
    setNewIconName(g.iconName || 'Target');
    setErrorMessage(null);
  };

  const handleSaveEdit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (isSubmitting) return;
    if (!editingGoal) return;
    const { valid: isTargetValid, value: target } = parseSafePositiveInteger(newTargetAmount);

    if (!isTargetValid || target <= 0) {
      setErrorMessage(t('goals.errorTargetAmount'));
      return;
    }

    if (!newDeadline || !isValidDateString(newDeadline)) {
      setErrorMessage(
        locale === 'vi-VN' ? 'Vui lòng chọn ngày đến hạn hợp lệ (YYYY-MM-DD).' : 'Please select a valid deadline date.'
      );
      return;
    }

    if (newDeadline !== editingGoal.deadline && !isDateTodayOrFuture(newDeadline)) {
      setErrorMessage(
        locale === 'vi-VN' ? 'Thời hạn hoàn thành mục tiêu không thể ở quá khứ.' : 'Goal deadline cannot be in the past.'
      );
      return;
    }

    const pinValue = await challengePinWithValue({
      actionName: locale === 'vi-VN' ? 'Cập nhật mục tiêu tài chính' : 'Update Financial Goal',
      amount: target,
    });
    if (pinValue === null) return;

    setIsSubmitting(true);
    try {
      const res = await editGoal(editingGoal.id, {
        title: newTitle,
        categoryId: newCategoryId,
        category: categories.find((c) => c.id === newCategoryId)?.name || undefined,
        targetAmount: target,
        deadline: newDeadline,
        iconName: newIconName,
        securityPin: pinValue,
      });

      if (res.success) {
        if (selectedGoalDetails?.id === editingGoal.id) {
          setSelectedGoalDetails((prev) =>
            prev
              ? {
                  ...prev,
                  title: newTitle,
                  categoryId: newCategoryId || undefined,
                  category: categories.find((c) => c.id === newCategoryId)?.name || prev.category,
                  targetAmount: target,
                  deadline: newDeadline,
                  iconName: newIconName,
                }
              : null
          );
        }

        setEditingGoal(null);
        showToast(t('goals.editSuccess'));
      } else {
        setErrorMessage(
          getLocalizedErrorMessage(res.error, locale, t, 'common.error')
        );
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleConfirmDelete = async () => {
    if (isSubmitting) return;
    if (!deletingGoal) return;

    const pinValue = await challengePinWithValue({
      actionName: locale === 'vi-VN' ? 'Đóng / Xóa mục tiêu' : 'Close / Delete Goal',
    });
    if (pinValue === null) return;

    setIsSubmitting(true);
    try {
      const res = await deleteGoal(deletingGoal.id, pinValue);
      if (!res.success) {
        setDeleteError(
          getLocalizedErrorMessage(res.error, locale, t, 'goals.goalHasFundsOrHistoryError')
        );
        return;
      }
      if (selectedGoalDetails?.id === deletingGoal.id) {
        setSelectedGoalDetails(null);
      }
      setDeletingGoal(null);
      setDeleteError(null);
      showToast(t('goals.deleteSuccess'));
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleOpenContribute = (g: GoalItem) => {
    setContributingGoal(g);
    const fallbackId = wallets.find((w) => w.isDefault)?.id || wallets[0]?.id || '';
    setActionWalletId(fallbackId);
    setActionAmount('500000');
    setErrorMessage(null);
  };

  const handleConfirmContribute = async (e: React.FormEvent) => {
    e.preventDefault();
    if (isSubmitting) return;
    if (!contributingGoal) return;
    if (!actionWalletId || wallets.length === 0) {
      setErrorMessage(locale === 'vi-VN' ? 'Không có ví thanh toán hợp lệ.' : 'No valid wallet found.');
      return;
    }
    if (!isActionAmountValid || parsedActionAmount <= 0) {
      setErrorMessage(t('goals.errorContributeAmount'));
      return;
    }
    if (contributeInsufficient) {
      setErrorMessage(t('transactions.insufficientBalance'));
      return;
    }

    const pinValue = await challengePinWithValue({
      actionName: locale === 'vi-VN' ? 'Nạp tiền vào mục tiêu' : 'Deposit into Goal',
      amount: parsedActionAmount,
    });
    if (pinValue === null) return;

    setIsSubmitting(true);
    try {
      const res = await contributeToGoal({
        goalId: contributingGoal.id,
        walletId: actionWalletId,
        amount: parsedActionAmount,
        securityPin: pinValue,
      });

      if (res.success) {
        setContributingGoal(null);
        showToast(
          t('goals.toastContributed', {
            amount: formatMoney(parsedActionAmount, locale),
            title: contributingGoal.title,
          })
        );
      } else {
        setErrorMessage(
          getLocalizedErrorMessage(res.error, locale, t, 'common.error')
        );
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleOpenWithdraw = (g: GoalItem) => {
    setWithdrawingGoal(g);
    const fallbackId = wallets.find((w) => w.isDefault)?.id || wallets[0]?.id || '';
    setActionWalletId(fallbackId);
    setActionAmount(Math.min(500000, g.currentAmount).toString());
    setErrorMessage(null);
  };

  const handleConfirmWithdraw = async (e: React.FormEvent) => {
    e.preventDefault();
    if (isSubmitting) return;
    if (!withdrawingGoal) return;
    if (!actionWalletId || wallets.length === 0) {
      setErrorMessage(locale === 'vi-VN' ? 'Không có ví nhận tiền hợp lệ.' : 'No valid destination wallet found.');
      return;
    }
    if (!isActionAmountValid || parsedActionAmount <= 0) {
      setErrorMessage(t('goals.errorWithdrawAmount'));
      return;
    }
    if (withdrawExcess) {
      setErrorMessage(t('goals.errorWithdrawExcess'));
      return;
    }

    const pinValue = await challengePinWithValue({
      actionName: locale === 'vi-VN' ? 'Rút tiền từ mục tiêu' : 'Withdraw from Goal',
      amount: parsedActionAmount,
    });
    if (pinValue === null) return;

    setIsSubmitting(true);
    try {
      const res = await withdrawFromGoal({
        goalId: withdrawingGoal.id,
        walletId: actionWalletId,
        amount: parsedActionAmount,
        securityPin: pinValue,
      });

      if (res.success) {
        setWithdrawingGoal(null);
        showToast(
          t('goals.toastWithdrawn', {
            amount: formatMoney(parsedActionAmount, locale),
            title: withdrawingGoal.title,
          })
        );
      } else {
        setErrorMessage(
          getLocalizedErrorMessage(res.error, locale, t, 'common.error')
        );
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  if (isLoading && goals.length === 0) {
    return <GoalsSkeleton />;
  }

  return (
    <div className="space-y-6 max-w-7xl mx-auto animate-in fade-in-50 duration-200">
      {/* Toast */}
      {toastMessage && (
        <div className="fixed top-6 right-6 z-50 flex items-center gap-2 px-4 py-3 rounded-2xl bg-slate-900 text-white shadow-xl border border-emerald-500/30 text-xs font-semibold animate-in slide-in-from-top-3">
          <CheckCircle2 className="h-4 w-4 text-emerald-400" />
          <span>{toastMessage}</span>
        </div>
      )}

      {/* Header */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <h2 className="text-xl sm:text-2xl font-bold tracking-tight text-slate-900 dark:text-white">
            {t('goals.pageTitle')}
          </h2>
          <p className="text-xs sm:text-sm text-slate-500 dark:text-slate-400 mt-0.5">
            {t('goals.pageSubtitle')}
          </p>
        </div>

        <Button
          size="sm"
          onClick={handleOpenCreate}
          className="gap-1.5 bg-emerald-600 hover:bg-emerald-700 text-white font-bold cursor-pointer shadow-2xs self-start sm:self-auto"
        >
          <Plus className="h-4 w-4" />
          <span>{t('goals.createGoal')}</span>
        </Button>
      </div>

      {/* Goals Overview Summary - Progress First */}
      <div className="bg-white dark:bg-slate-900 rounded-3xl border border-slate-100 dark:border-slate-800 p-6 shadow-sm">
        <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 items-center">
          {/* Left Column: Dominant Accumulated & Financial Equation */}
          <div className="lg:col-span-7 space-y-3">
            <div>
              <span className="text-xs font-semibold text-slate-500 dark:text-slate-400">
                {locale === 'vi-VN' ? 'Tổng quan tích lũy mục tiêu' : 'Goals Savings Overview'}
              </span>
              <div className="flex items-baseline gap-3 mt-1">
                <span className="text-3xl sm:text-4xl font-extrabold text-slate-900 dark:text-white tabular-nums tracking-tight">
                  {isHidden ? '••••••••' : formatMoney(overallStats.totalSaved, locale)}
                </span>
                <span className="text-xs font-bold px-2.5 py-0.5 rounded-full bg-emerald-100 dark:bg-emerald-950/60 text-emerald-700 dark:text-emerald-300">
                  {locale === 'vi-VN'
                    ? `Đã tích lũy (${overallStats.progressPercentage}%)`
                    : `Accumulated (${overallStats.progressPercentage}%)`}
                </span>
              </div>
            </div>

            {/* Compact Financial Equation: [Tổng mục tiêu] - [Đã có] = [Còn thiếu] */}
            <div className="inline-flex flex-wrap items-center gap-2 p-2.5 rounded-2xl bg-slate-50 dark:bg-slate-800/60 border border-slate-200/60 dark:border-slate-700/50 text-xs">
              <span className="text-slate-500 dark:text-slate-400">
                {locale === 'vi-VN' ? 'Tổng mục tiêu:' : 'Total Target:'}{' '}
                <strong className="text-slate-900 dark:text-white font-bold tabular-nums">
                  {isHidden ? '••••••••' : formatMoney(overallStats.totalTarget, locale)}
                </strong>
              </span>
              <span className="text-slate-300 dark:text-slate-600 font-bold">-</span>
              <span className="text-slate-500 dark:text-slate-400">
                {locale === 'vi-VN' ? 'Đã có:' : 'Saved:'}{' '}
                <strong className="text-emerald-600 dark:text-emerald-400 font-bold tabular-nums">
                  {isHidden ? '••••••••' : formatMoney(overallStats.totalSaved, locale)}
                </strong>
              </span>
              <span className="text-slate-300 dark:text-slate-600 font-bold">=</span>
              <span className="text-slate-500 dark:text-slate-400">
                {locale === 'vi-VN' ? 'Còn thiếu:' : 'Remaining:'}{' '}
                <strong className="text-slate-800 dark:text-slate-200 font-bold tabular-nums">
                  {isHidden ? '••••••••' : formatMoney(Math.max(0, overallStats.totalTarget - overallStats.totalSaved), locale)}
                </strong>
              </span>
            </div>
          </div>

          {/* Right Column: Progress & Status */}
          <div className="lg:col-span-5 p-4 rounded-2xl bg-slate-50 dark:bg-slate-800/40 border border-slate-100 dark:border-slate-800 space-y-2.5">
            <div className="flex items-center justify-between">
              <span className="text-xs font-semibold text-slate-600 dark:text-slate-300">
                {t('goals.overallProgress')}
              </span>
              <div className="flex items-center gap-2">
                <span className="text-xs font-bold px-2.5 py-0.5 rounded-full bg-teal-50 dark:bg-teal-950/60 text-teal-700 dark:text-teal-300 border border-teal-200/60 dark:border-teal-800/60">
                  {overallStats.activeCount} {locale === 'vi-VN' ? 'mục tiêu' : 'goals'}
                </span>
                <span className="text-base font-extrabold text-slate-900 dark:text-white tabular-nums">
                  {overallStats.progressPercentage}%
                </span>
              </div>
            </div>

            <div className="h-3 w-full bg-slate-200/70 dark:bg-slate-700 rounded-full overflow-hidden flex">
              <div
                className="h-full bg-emerald-500 rounded-full transition-all duration-500"
                style={{ width: `${overallStats.progressPercentage}%` }}
              />
            </div>

            <div className="flex justify-between text-[11px] font-medium text-slate-500 dark:text-slate-400">
              <span>{formatMoney(0, locale)}</span>
              <span>
                {locale === 'vi-VN'
                  ? `Mục tiêu: ${formatMoney(overallStats.totalTarget, locale)}`
                  : `Target: ${formatMoney(overallStats.totalTarget, locale)}`}
              </span>
            </div>
          </div>
        </div>

        {/* Integrated Financial Note Line */}
        <div className="mt-5 pt-3.5 border-t border-slate-100 dark:border-slate-800/80 flex items-center gap-2 text-xs text-slate-600 dark:text-slate-300">
          <Info className="h-4 w-4 text-emerald-600 dark:text-emerald-400 shrink-0" />
          <p className="leading-relaxed">
            {locale === 'vi-VN' ? (
              <>
                <strong className="text-emerald-700 dark:text-emerald-400">Lưu ý tài chính:</strong> Khoản tiền đóng góp vào mục tiêu là sự điều chuyển nội bộ giữa các quỹ của bạn, <em className="text-slate-500 dark:text-slate-400">không tính vào chi phí sinh hoạt hàng tháng</em>.
              </>
            ) : (
              <>
                <strong className="text-emerald-700 dark:text-emerald-400">Financial notice:</strong> Goal contributions are internal fund transfers between your balances, <em className="text-slate-500 dark:text-slate-400">not counted as monthly living expenses</em>.
              </>
            )}
          </p>
        </div>
      </div>

      {/* Goal Cards Section */}
      <div className="space-y-4">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <h3 className="text-base font-bold text-slate-900 dark:text-white">
              {t('goals.goalList')}
            </h3>
            <span className="px-2 py-0.5 rounded-full text-xs font-bold bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-300">
              {goals.length}
            </span>
          </div>
        </div>

        {goals.length === 0 ? (
          <div className="p-12 rounded-3xl bg-white dark:bg-slate-900 border border-slate-100 dark:border-slate-800 text-center space-y-3">
            <div className="h-12 w-12 rounded-2xl bg-emerald-50 dark:bg-emerald-950/50 text-emerald-600 dark:text-emerald-400 flex items-center justify-center mx-auto">
              <Target className="h-6 w-6" />
            </div>
            <div className="space-y-1 max-w-sm mx-auto">
              <p className="text-sm font-bold text-slate-800 dark:text-slate-200">
                {t('goals.noGoalsYet')}
              </p>
              <p className="text-xs text-slate-400">
                {t('goals.noGoalsDesc')}
              </p>
            </div>
            <Button
              size="sm"
              onClick={handleOpenCreate}
              className="bg-emerald-600 hover:bg-emerald-700 text-white font-bold text-xs gap-1.5 cursor-pointer mt-2"
            >
              <Plus className="h-3.5 w-3.5" />
              <span>{t('goals.createGoal')}</span>
            </Button>
          </div>
        ) : (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-5">
            {goals.map((g) => {
              const percentage = Math.min(100, Math.round((g.currentAmount / g.targetAmount) * 100));
              const isCompleted = percentage >= 100;
              const remainingAmount = Math.max(0, g.targetAmount - g.currentAmount);
              const isSingleFeatured = goals.length === 1;

              return (
                <div
                  key={g.id}
                  onClick={() => setSelectedGoalDetails(g)}
                  className={cn(
                    'p-5 sm:p-6 rounded-3xl bg-white dark:bg-slate-900 border border-slate-100 dark:border-slate-800 shadow-sm hover:border-emerald-500/40 dark:hover:border-emerald-500/40 transition-all cursor-pointer group flex flex-col justify-between space-y-5',
                    isSingleFeatured ? 'col-span-1 md:col-span-2 lg:col-span-2' : ''
                  )}
                >
                  {/* Header */}
                  <div className="flex flex-col sm:flex-row sm:items-start justify-between gap-3">
                    <div className="flex items-center gap-3.5">
                      <div className="h-12 w-12 rounded-2xl bg-emerald-100 dark:bg-emerald-950 text-emerald-600 dark:text-emerald-400 flex items-center justify-center shrink-0">
                        {getGoalIcon(g.iconName)}
                      </div>
                      <div>
                        <div className="flex items-center gap-2">
                          <h4 className="text-base font-bold text-slate-900 dark:text-white group-hover:text-emerald-600 transition-colors">
                            {g.title}
                          </h4>
                          <span
                            className={cn(
                              'px-2.5 py-0.5 rounded-full text-[11px] font-bold',
                              g.status === 'paused'
                                ? 'bg-amber-100 text-amber-700 dark:bg-amber-950 dark:text-amber-300'
                                : g.status === 'cancelled'
                                ? 'bg-rose-100 text-rose-700 dark:bg-rose-950 dark:text-rose-300'
                                : isCompleted
                                ? 'bg-emerald-100 text-emerald-700 dark:bg-emerald-950 dark:text-emerald-300'
                                : 'bg-emerald-50 text-emerald-700 dark:bg-emerald-950/60 dark:text-emerald-300 border border-emerald-200/50 dark:border-emerald-800/50'
                            )}
                          >
                            {g.status === 'paused'
                              ? t('goals.statusPaused')
                              : g.status === 'cancelled'
                              ? t('goals.statusCancelled')
                              : isCompleted
                              ? t('goals.statusCompleted')
                              : `${percentage}%`}
                          </span>
                        </div>
                        <span className="text-xs text-slate-500 dark:text-slate-400 font-medium">
                          {getLocalizedGoalCategory(g.category, t, locale)}
                        </span>
                      </div>
                    </div>

                    <div className="flex items-center gap-2 self-end sm:self-auto" onClick={(e) => e.stopPropagation()}>
                      {g.deadline && (
                        <div className="flex items-center gap-1.5 px-3 py-1 rounded-xl bg-slate-50 dark:bg-slate-800/60 border border-slate-200/60 dark:border-slate-700/50 text-xs text-slate-600 dark:text-slate-300 font-medium">
                          <Calendar className="h-3.5 w-3.5 text-slate-400" />
                          <span>
                            {t('goals.targetDate')}:{' '}
                            <strong className="font-bold text-slate-800 dark:text-slate-200">
                              {formatDate(g.deadline, locale)}
                            </strong>
                          </span>
                        </div>
                      )}
                      <button
                        type="button"
                        onClick={() => handleOpenEdit(g)}
                        title={t('goals.editGoal')}
                        aria-label={t('goals.editGoal')}
                        className="p-1.5 rounded-xl text-slate-400 hover:text-emerald-600 hover:bg-slate-100 dark:hover:bg-slate-800 transition-colors cursor-pointer"
                      >
                        <Edit3 className="h-4 w-4" />
                      </button>
                      <button
                        type="button"
                        onClick={() => {
                          setDeleteError(null);
                          setDeletingGoal(g);
                        }}
                        title={t('goals.deleteGoal')}
                        aria-label={t('goals.deleteGoal')}
                        className="p-1.5 rounded-xl text-slate-400 hover:text-rose-600 hover:bg-rose-50 dark:hover:bg-rose-950/30 transition-colors cursor-pointer"
                      >
                        <Trash2 className="h-4 w-4" />
                      </button>
                    </div>
                  </div>

                  {/* 3-Metric Financial Grid */}
                  <div className="grid grid-cols-3 gap-3 p-3.5 rounded-2xl bg-slate-50 dark:bg-slate-800/40 border border-slate-100 dark:border-slate-800">
                    <div>
                      <span className="text-[11px] font-semibold text-slate-500 dark:text-slate-400 block">
                        {t('goals.totalSaved')}
                      </span>
                      <span className="text-sm sm:text-base font-extrabold text-emerald-600 dark:text-emerald-400 tabular-nums mt-0.5 block">
                        {isHidden ? '••••••••' : formatMoney(g.currentAmount, locale)}
                      </span>
                    </div>
                    <div className="border-l border-slate-200/60 dark:border-slate-700/50 pl-3">
                      <span className="text-[11px] font-semibold text-slate-500 dark:text-slate-400 block">
                        {t('goals.targetAmount')}
                      </span>
                      <span className="text-sm sm:text-base font-bold text-slate-800 dark:text-slate-200 tabular-nums mt-0.5 block">
                        {isHidden ? '••••••••' : formatMoney(g.targetAmount, locale)}
                      </span>
                    </div>
                    <div className="border-l border-slate-200/60 dark:border-slate-700/50 pl-3">
                      <span className="text-[11px] font-semibold text-slate-500 dark:text-slate-400 block">
                        {locale === 'vi-VN' ? 'Còn thiếu' : 'Remaining'}
                      </span>
                      <span className="text-sm sm:text-base font-bold text-slate-700 dark:text-slate-300 tabular-nums mt-0.5 block">
                        {isHidden ? '••••••••' : formatMoney(remainingAmount, locale)}
                      </span>
                    </div>
                  </div>

                  {/* Progress bar */}
                  <div className="space-y-1.5">
                    <div className="h-2.5 w-full bg-slate-100 dark:bg-slate-800 rounded-full overflow-hidden">
                      <div
                        className={cn(
                          'h-full rounded-full transition-all duration-300',
                          isCompleted ? 'bg-emerald-500' : 'bg-emerald-500'
                        )}
                        style={{ width: `${percentage}%` }}
                      />
                    </div>
                    <div className="flex items-center justify-between text-xs text-slate-500 dark:text-slate-400">
                      <span>
                        {locale === 'vi-VN' ? 'Tiến độ hoàn thành:' : 'Progress:'}{' '}
                        <strong className="text-slate-700 dark:text-slate-300 tabular-nums">{percentage}%</strong>
                      </span>
                    </div>
                  </div>

                  {/* Card Actions */}
                  <div
                    className="flex items-center gap-3 pt-3 border-t border-slate-100 dark:border-slate-800/80"
                    onClick={(e) => e.stopPropagation()}
                  >
                    <Button
                      size="sm"
                      onClick={() => handleOpenContribute(g)}
                      className="flex-1 bg-emerald-600 hover:bg-emerald-700 text-white font-bold text-xs gap-1.5 cursor-pointer py-2.5 shadow-2xs"
                    >
                      <Plus className="h-4 w-4" />
                      <span>{locale === 'vi-VN' ? 'Đóng góp vào mục tiêu' : t('goals.contribute')}</span>
                    </Button>

                    {g.currentAmount > 0 && (
                      <Button
                        size="sm"
                        variant="outline"
                        onClick={() => handleOpenWithdraw(g)}
                        className="text-slate-700 dark:text-slate-300 text-xs px-3.5 py-2.5 font-semibold cursor-pointer"
                      >
                        <span>{t('goals.withdraw')}</span>
                      </Button>
                    )}
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </div>

      {/* 1. CREATE GOAL MODAL */}
      {isCreateOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-xs animate-in fade-in duration-150">
          <form
            onSubmit={handleSaveCreate}
            onKeyDown={(e) => {
              if (e.key === 'Enter' && (e.target as HTMLElement).tagName !== 'TEXTAREA') {
                e.preventDefault();
                handleSaveCreate(e);
              }
            }}
            className="w-full max-w-md bg-white dark:bg-slate-900 rounded-3xl p-6 sm:p-7 shadow-2xl border border-slate-100 dark:border-slate-800 space-y-5 animate-in zoom-in-95"
          >
            <div className="flex items-center justify-between">
              <h3 className="text-base font-bold text-slate-900 dark:text-white">
                {t('goals.createModalTitle')}
              </h3>
              <button
                type="button"
                onClick={() => setIsCreateOpen(false)}
                className="p-1.5 rounded-xl text-slate-400 hover:bg-slate-100 dark:hover:bg-slate-800 cursor-pointer"
              >
                <X className="h-4 w-4" />
              </button>
            </div>

            {errorMessage && (
              <div className="p-3 rounded-2xl bg-rose-50 dark:bg-rose-950/30 text-rose-700 dark:text-rose-300 text-xs font-semibold flex items-center gap-2">
                <AlertCircle className="h-4 w-4 shrink-0" />
                <span>{errorMessage}</span>
              </div>
            )}

            <div className="space-y-4 text-xs">
              {/* Group 1: Thông tin mục tiêu */}
              <div className="p-3.5 rounded-2xl bg-slate-50 dark:bg-slate-800/50 border border-slate-200/60 dark:border-slate-700/50 space-y-3">
                <span className="text-[11px] font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400 block">
                  {locale === 'vi-VN' ? '1. Thông tin mục tiêu' : '1. Goal Information'}
                </span>

                <div className="space-y-1.5">
                  <label className="font-bold text-slate-700 dark:text-slate-300">
                    {t('goals.goalName')} *
                  </label>
                  <input
                    type="text"
                    placeholder={t('goals.goalNamePlaceholder')}
                    value={newTitle}
                    onChange={(e) => setNewTitle(e.target.value)}
                    required
                    className="w-full px-4 py-2.5 rounded-2xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-900 text-slate-900 dark:text-white font-semibold focus:outline-none"
                  />
                </div>

                <div className="space-y-1.5">
                  <label className="font-bold text-slate-700 dark:text-slate-300">
                    {t('goals.goalCategoryLabel')}
                  </label>
                  <Select
                    value={newCategoryId}
                    onChange={(val) => setNewCategoryId(val)}
                    accentColor="emerald"
                  >
                    <option value="">{locale === 'vi-VN' ? '— Không phân loại —' : '— No category —'}</option>
                    {categories.filter((c) => !c.isArchived).map((c) => (
                      <option key={c.id} value={c.id}>
                        {c.name}
                      </option>
                    ))}
                  </Select>
                </div>

                <div className="space-y-1.5">
                  <label className="font-bold text-slate-700 dark:text-slate-300">
                    {locale === 'vi-VN' ? 'Biểu tượng mục tiêu' : 'Goal icon'}
                  </label>
                  <div className="grid grid-cols-6 gap-2">
                    {[
                      { name: 'Target', label: locale === 'vi-VN' ? 'Mục tiêu' : 'Goal' },
                      { name: 'Laptop', label: locale === 'vi-VN' ? 'Thiết bị' : 'Device' },
                      { name: 'Plane', label: locale === 'vi-VN' ? 'Du lịch' : 'Travel' },
                      { name: 'Heart', label: locale === 'vi-VN' ? 'Sức khỏe' : 'Health' },
                      { name: 'Car', label: locale === 'vi-VN' ? 'Xe cộ' : 'Vehicle' },
                      { name: 'Home', label: locale === 'vi-VN' ? 'Nhà cửa' : 'Home' },
                    ].map((ic) => (
                      <button
                        key={ic.name}
                        type="button"
                        title={ic.label}
                        aria-label={ic.label}
                        onClick={() => setNewIconName(ic.name)}
                        className={cn(
                          'p-2.5 rounded-2xl border flex items-center justify-center transition-all cursor-pointer',
                          newIconName === ic.name
                            ? 'border-emerald-500 bg-emerald-50 dark:bg-emerald-950 text-emerald-600 dark:text-emerald-400 font-bold'
                            : 'border-slate-200 dark:border-slate-700 text-slate-500 hover:border-slate-300'
                        )}
                      >
                        {getGoalIcon(ic.name)}
                      </button>
                    ))}
                  </div>
                </div>
              </div>

              {/* Group 2: Kế hoạch tài chính & Thời hạn */}
              <div className="p-3.5 rounded-2xl bg-slate-50 dark:bg-slate-800/50 border border-slate-200/60 dark:border-slate-700/50 space-y-3">
                <span className="text-[11px] font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400 block">
                  {locale === 'vi-VN' ? '2. Kế hoạch tài chính & Thời hạn' : '2. Financial Plan & Timeline'}
                </span>

                <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                  <div className="space-y-1.5">
                    <div className="flex items-center justify-between">
                      <label className="font-bold text-slate-700 dark:text-slate-300">
                        {t('goals.targetAmount')} (VND) *
                      </label>
                      {(() => {
                        const parsed = parseSafePositiveInteger(newTargetAmount);
                        return parsed.valid && parsed.value > 0 ? (
                          <span className="text-xs font-bold text-emerald-600 dark:text-emerald-400 tabular-nums">
                            ≈ {formatMoney(parsed.value, locale)}
                          </span>
                        ) : null;
                      })()}
                    </div>
                    <input
                      type="text"
                      inputMode="numeric"
                      placeholder="15000000"
                      value={newTargetAmount}
                      onChange={(e) => setNewTargetAmount(sanitizeIntegerInput(e.target.value))}
                      required
                      className="w-full px-3.5 py-2.5 rounded-2xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-900 text-slate-900 dark:text-white font-bold"
                    />
                  </div>

                  <div className="space-y-1.5">
                    <label className="font-bold text-slate-700 dark:text-slate-300">
                      {t('goals.targetDate')}
                    </label>
                    <input
                      type="date"
                      min={getLocalDateString()}
                      value={newDeadline}
                      onChange={(e) => setNewDeadline(e.target.value)}
                      className="w-full px-3.5 py-2.5 rounded-2xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-900 text-slate-900 dark:text-white font-semibold"
                    />
                  </div>
                </div>

                <div className="space-y-1.5">
                  <div className="flex items-center justify-between">
                    <label className="font-bold text-slate-700 dark:text-slate-300">
                      {locale === 'vi-VN' ? 'Số tiền nạp khởi tạo ban đầu (Tùy chọn)' : 'Initial Seed Amount (Optional)'}
                    </label>
                    {(() => {
                      const parsed = parseSafePositiveInteger(newInitialAmount);
                      return parsed.valid && parsed.value > 0 ? (
                        <span className="text-xs font-bold text-emerald-600 dark:text-emerald-400 tabular-nums">
                          ≈ {formatMoney(parsed.value, locale)}
                        </span>
                      ) : null;
                    })()}
                  </div>
                  <input
                    type="text"
                    inputMode="numeric"
                    placeholder="0"
                    value={newInitialAmount}
                    onChange={(e) => setNewInitialAmount(sanitizeIntegerInput(e.target.value))}
                    className="w-full px-3.5 py-2.5 rounded-2xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-900 text-slate-900 dark:text-white font-bold"
                  />
                </div>

                {parseSafePositiveInteger(newInitialAmount).value > 0 && (
                  <div className="space-y-1.5 animate-in fade-in pt-1">
                    <label className="font-bold text-slate-700 dark:text-slate-300">
                      {locale === 'vi-VN' ? 'Trích tiền từ ví nguồn' : 'Deduct from wallet'}
                    </label>
                    <Select
                      value={newWalletId}
                      onChange={(val) => setNewWalletId(val)}
                      accentColor="emerald"
                    >
                      {wallets.map((w) => (
                        <option key={w.id} value={w.id}>
                          {w.name} ({formatMoney(w.balance, locale)})
                        </option>
                      ))}
                    </Select>
                  </div>
                )}
              </div>
            </div>

            <div className="flex items-center justify-end gap-3 pt-3 border-t border-slate-100 dark:border-slate-800">
              <Button
                type="button"
                variant="outline"
                onClick={() => setIsCreateOpen(false)}
                disabled={isSubmitting}
                className="cursor-pointer"
              >
                {t('goals.cancel')}
              </Button>
              <Button
                type="submit"
                disabled={isSubmitting}
                className="bg-emerald-600 hover:bg-emerald-700 text-white font-bold cursor-pointer disabled:opacity-50"
              >
                {isSubmitting ? (locale === 'vi-VN' ? 'Đang lưu...' : 'Saving...') : t('goals.saveGoal')}
              </Button>
            </div>
          </form>
        </div>
      )}

      {/* 2. EDIT GOAL MODAL */}
      {editingGoal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-xs animate-in fade-in duration-150">
          <form
            onSubmit={handleSaveEdit}
            onKeyDown={(e) => {
              if (e.key === 'Enter' && (e.target as HTMLElement).tagName !== 'TEXTAREA') {
                e.preventDefault();
                handleSaveEdit(e);
              }
            }}
            className="w-full max-w-md bg-white dark:bg-slate-900 rounded-3xl p-6 sm:p-7 shadow-2xl border border-slate-100 dark:border-slate-800 space-y-5 animate-in zoom-in-95"
          >
            <div className="flex items-center justify-between">
              <h3 className="text-base font-bold text-slate-900 dark:text-white">
                {t('goals.editGoal')}
              </h3>
              <button
                type="button"
                onClick={() => setEditingGoal(null)}
                className="p-1.5 rounded-xl text-slate-400 hover:bg-slate-100 dark:hover:bg-slate-800 cursor-pointer"
              >
                <X className="h-4 w-4" />
              </button>
            </div>

            {errorMessage && (
              <div className="p-3 rounded-2xl bg-rose-50 dark:bg-rose-950/30 text-rose-700 dark:text-rose-300 text-xs font-semibold flex items-center gap-2">
                <AlertCircle className="h-4 w-4 shrink-0" />
                <span>{errorMessage}</span>
              </div>
            )}

            <div className="space-y-4 text-xs">
              {/* Group 1: Thông tin mục tiêu */}
              <div className="p-3.5 rounded-2xl bg-slate-50 dark:bg-slate-800/50 border border-slate-200/60 dark:border-slate-700/50 space-y-3">
                <span className="text-[11px] font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400 block">
                  {locale === 'vi-VN' ? '1. Thông tin mục tiêu' : '1. Goal Information'}
                </span>

                <div className="space-y-1.5">
                  <label className="font-bold text-slate-700 dark:text-slate-300">
                    {t('goals.goalName')} *
                  </label>
                  <input
                    type="text"
                    value={newTitle}
                    onChange={(e) => setNewTitle(e.target.value)}
                    required
                    className="w-full px-4 py-2.5 rounded-2xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-900 text-slate-900 dark:text-white font-semibold focus:outline-none"
                  />
                </div>

                <div className="space-y-1.5">
                  <label className="font-bold text-slate-700 dark:text-slate-300">
                    {t('goals.goalCategoryLabel')}
                  </label>
                  <Select
                    value={newCategoryId}
                    onChange={(val) => setNewCategoryId(val)}
                    accentColor="emerald"
                  >
                    <option value="">{locale === 'vi-VN' ? '— Không phân loại —' : '— No category —'}</option>
                    {categories.filter((c) => !c.isArchived).map((c) => (
                      <option key={c.id} value={c.id}>
                        {c.name}
                      </option>
                    ))}
                  </Select>
                </div>

                <div className="space-y-1.5">
                  <label className="font-bold text-slate-700 dark:text-slate-300">
                    {locale === 'vi-VN' ? 'Biểu tượng mục tiêu' : 'Goal icon'}
                  </label>
                  <div className="grid grid-cols-6 gap-2">
                    {[
                      { name: 'Target', label: locale === 'vi-VN' ? 'Mục tiêu' : 'Goal' },
                      { name: 'Laptop', label: locale === 'vi-VN' ? 'Thiết bị' : 'Device' },
                      { name: 'Plane', label: locale === 'vi-VN' ? 'Du lịch' : 'Travel' },
                      { name: 'Heart', label: locale === 'vi-VN' ? 'Sức khỏe' : 'Health' },
                      { name: 'Car', label: locale === 'vi-VN' ? 'Xe cộ' : 'Vehicle' },
                      { name: 'Home', label: locale === 'vi-VN' ? 'Nhà cửa' : 'Home' },
                    ].map((ic) => (
                      <button
                        key={ic.name}
                        type="button"
                        title={ic.label}
                        aria-label={ic.label}
                        onClick={() => setNewIconName(ic.name)}
                        className={cn(
                          'p-2.5 rounded-2xl border flex items-center justify-center transition-all cursor-pointer',
                          newIconName === ic.name
                            ? 'border-emerald-500 bg-emerald-50 dark:bg-emerald-950 text-emerald-600 dark:text-emerald-400 font-bold'
                            : 'border-slate-200 dark:border-slate-700 text-slate-500 hover:border-slate-300'
                        )}
                      >
                        {getGoalIcon(ic.name)}
                      </button>
                    ))}
                  </div>
                </div>
              </div>

              {/* Group 2: Kế hoạch tài chính & Thời hạn */}
              <div className="p-3.5 rounded-2xl bg-slate-50 dark:bg-slate-800/50 border border-slate-200/60 dark:border-slate-700/50 space-y-3">
                <span className="text-[11px] font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400 block">
                  {locale === 'vi-VN' ? '2. Kế hoạch tài chính & Thời hạn' : '2. Financial Plan & Timeline'}
                </span>

                <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                  <div className="space-y-1.5">
                    <div className="flex items-center justify-between">
                      <label className="font-bold text-slate-700 dark:text-slate-300">
                        {t('goals.targetAmount')} (VND) *
                      </label>
                      {(() => {
                        const parsed = parseSafePositiveInteger(newTargetAmount);
                        return parsed.valid && parsed.value > 0 ? (
                          <span className="text-xs font-bold text-emerald-600 dark:text-emerald-400 tabular-nums">
                            ≈ {formatMoney(parsed.value, locale)}
                          </span>
                        ) : null;
                      })()}
                    </div>
                    <input
                      type="text"
                      inputMode="numeric"
                      value={newTargetAmount}
                      onChange={(e) => setNewTargetAmount(sanitizeIntegerInput(e.target.value))}
                      required
                      className="w-full px-3.5 py-2.5 rounded-2xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-900 text-slate-900 dark:text-white font-bold"
                    />
                  </div>

                  <div className="space-y-1.5">
                    <label className="font-bold text-slate-700 dark:text-slate-300">
                      {t('goals.targetDate')}
                    </label>
                    <input
                      type="date"
                      value={newDeadline}
                      onChange={(e) => setNewDeadline(e.target.value)}
                      className="w-full px-3.5 py-2.5 rounded-2xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-900 text-slate-900 dark:text-white font-semibold"
                    />
                  </div>
                </div>
              </div>
            </div>

            <div className="flex items-center justify-end gap-3 pt-3 border-t border-slate-100 dark:border-slate-800">
              <Button
                type="button"
                variant="outline"
                onClick={() => setEditingGoal(null)}
                disabled={isSubmitting}
                className="cursor-pointer"
              >
                {t('goals.cancel')}
              </Button>
              <Button
                type="submit"
                disabled={isSubmitting}
                className="bg-emerald-600 hover:bg-emerald-700 text-white font-bold cursor-pointer disabled:opacity-50"
              >
                {isSubmitting ? (locale === 'vi-VN' ? 'Đang lưu...' : 'Saving...') : t('goals.saveGoal')}
              </Button>
            </div>
          </form>
        </div>
      )}

      {/* 3. DELETE GOAL CONFIRMATION MODAL */}
      {deletingGoal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-xs animate-in fade-in duration-150">
          <div className="w-full max-w-sm bg-white dark:bg-slate-900 rounded-3xl p-6 shadow-2xl border border-slate-100 dark:border-slate-800 space-y-4 animate-in zoom-in-95">
            <div className="h-12 w-12 rounded-2xl bg-rose-100 dark:bg-rose-950 text-rose-600 dark:text-rose-400 flex items-center justify-center mx-auto">
              <Trash2 className="h-6 w-6" />
            </div>
            <div className="text-center space-y-1.5">
              <h3 className="text-base font-bold text-slate-900 dark:text-white">
                {t('goals.confirmDeleteTitle')}
              </h3>
              <p className="text-xs text-slate-400 leading-relaxed">
                {t('goals.confirmDeleteDesc', { title: deletingGoal.title })}
              </p>
            </div>

            {deleteError && (
              <div className="p-3 rounded-2xl bg-rose-50 dark:bg-rose-950/30 border border-rose-200 dark:border-rose-900/50 flex items-center gap-2 text-xs text-rose-700 dark:text-rose-300 font-semibold">
                <AlertCircle className="h-4 w-4 shrink-0" />
                <span>{deleteError}</span>
              </div>
            )}

            {deletingGoal.currentAmount > 0 && !deleteError && (
              <div className="p-3 rounded-2xl bg-amber-50 dark:bg-amber-950/30 border border-amber-200 dark:border-amber-900/50 flex items-center gap-2 text-xs text-amber-700 dark:text-amber-300 font-semibold">
                <AlertTriangle className="h-4 w-4 shrink-0" />
                <span>
                  {locale === 'vi-VN'
                    ? `Mục tiêu đang có ${formatMoney(deletingGoal.currentAmount, locale)}. Hãy rút tiền trước khi xóa nếu cần hoàn tiền vào ví.`
                    : `Goal currently holds ${formatMoney(deletingGoal.currentAmount, locale)}. Please withdraw funds before deleting to refund them to your wallet.`}
                </span>
              </div>
            )}

            <div className="flex items-center justify-center gap-3 pt-2">
              <Button
                type="button"
                variant="outline"
                onClick={() => {
                  setDeletingGoal(null);
                  setDeleteError(null);
                }}
                disabled={isSubmitting}
                className="flex-1 text-xs cursor-pointer"
              >
                {t('goals.cancel')}
              </Button>
              <Button
                type="button"
                onClick={handleConfirmDelete}
                disabled={isSubmitting}
                className="flex-1 text-xs bg-rose-600 hover:bg-rose-700 text-white font-bold cursor-pointer disabled:opacity-50"
              >
                {isSubmitting ? (locale === 'vi-VN' ? 'Đang xóa...' : 'Deleting...') : t('goals.deleteGoal')}
              </Button>
            </div>
          </div>
        </div>
      )}

      {/* 4. CONTRIBUTE MODAL */}
      {contributingGoal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-xs animate-in fade-in duration-150">
          <form
            onSubmit={handleConfirmContribute}
            onKeyDown={(e) => {
              if (e.key === 'Enter' && (e.target as HTMLElement).tagName !== 'TEXTAREA') {
                e.preventDefault();
                handleConfirmContribute(e);
              }
            }}
            className="w-full max-w-md bg-white dark:bg-slate-900 rounded-3xl p-6 sm:p-7 shadow-2xl border border-slate-100 dark:border-slate-800 space-y-5 animate-in zoom-in-95"
          >
            <div className="flex items-center justify-between">
              <div className="flex items-center gap-2.5">
                <div className="h-9 w-9 rounded-2xl bg-emerald-100 dark:bg-emerald-950 text-emerald-600 dark:text-emerald-400 flex items-center justify-center">
                  <Plus className="h-5 w-5" />
                </div>
                <div>
                  <h3 className="text-base font-bold text-slate-900 dark:text-white">
                    {t('goals.contributeModalTitle')}
                  </h3>
                  <span className="text-xs text-slate-400">{contributingGoal.title}</span>
                </div>
              </div>
              <button
                type="button"
                onClick={() => setContributingGoal(null)}
                className="p-1.5 rounded-xl text-slate-400 hover:bg-slate-100 dark:hover:bg-slate-800 cursor-pointer"
              >
                <X className="h-4 w-4" />
              </button>
            </div>

            {errorMessage && (
              <div className="p-3 rounded-2xl bg-rose-50 dark:bg-rose-950/30 text-rose-700 dark:text-rose-300 text-xs font-semibold flex items-center gap-2">
                <AlertCircle className="h-4 w-4 shrink-0" />
                <span>{errorMessage}</span>
              </div>
            )}

            {contributeInsufficient && (
              <div className="p-3 rounded-2xl bg-rose-50 dark:bg-rose-950/30 text-rose-700 dark:text-rose-300 text-xs font-semibold flex items-center gap-2">
                <AlertCircle className="h-4 w-4 shrink-0" />
                <span>{locale === 'vi-VN' ? 'Số dư ví thanh toán không đủ.' : 'Insufficient wallet balance.'}</span>
              </div>
            )}

            <div className="space-y-4 text-xs">
              <div className="space-y-1.5">
                <label className="font-bold text-slate-700 dark:text-slate-300">
                  {t('goals.sourceFund')}
                </label>
                <Select
                  value={actionWalletId}
                  onChange={(val) => setActionWalletId(val)}
                  accentColor="emerald"
                >
                  {wallets.map((w) => (
                    <option key={w.id} value={w.id}>
                      {w.name} ({formatMoney(w.balance, locale)})
                    </option>
                  ))}
                </Select>
              </div>

              <div className="space-y-1.5">
                <label className="font-bold text-slate-700 dark:text-slate-300">
                  {t('goals.amountToContribute')} (VND) *
                </label>
                <input
                  type="text"
                  inputMode="numeric"
                  value={actionAmount}
                  onChange={(e) => setActionAmount(sanitizeIntegerInput(e.target.value))}
                  required
                  className="w-full px-4 py-2.5 text-sm rounded-2xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-slate-900 dark:text-white font-bold"
                />
              </div>

              {/* Transaction Preview Section */}
              <div className="p-4 rounded-2xl bg-slate-50 dark:bg-slate-800/50 border border-slate-100 dark:border-slate-800 space-y-2 text-xs">
                <div className="flex justify-between text-slate-400">
                  <span>{t('goals.previewCurrentWallet')}:</span>
                  <span className="font-semibold text-slate-700 dark:text-slate-300">
                    {formatMoney(selectedWallet?.balance || 0, locale)}
                  </span>
                </div>
                <div className="flex justify-between text-slate-400">
                  <span>{t('goals.previewContributeAmount')}:</span>
                  <span className="font-semibold text-emerald-600">
                    +{formatMoney(parsedActionAmount, locale)}
                  </span>
                </div>
                <div className="h-px bg-slate-200 dark:bg-slate-700 my-1" />
                <div className="flex justify-between text-slate-700 dark:text-slate-200 font-bold">
                  <span>{t('goals.previewWalletAfter')}:</span>
                  <span className="tabular-nums">
                    {formatMoney(previewWalletAfterContribute, locale)}
                  </span>
                </div>
                <div className="flex justify-between text-emerald-600 font-bold">
                  <span>{t('goals.previewGoalAfter')}:</span>
                  <span className="tabular-nums">
                    {formatMoney(previewGoalAfterContribute, locale)}
                  </span>
                </div>
              </div>
            </div>

            <div className="flex items-center justify-end gap-3 pt-3 border-t border-slate-100 dark:border-slate-800">
              <Button
                type="button"
                variant="outline"
                onClick={() => setContributingGoal(null)}
                disabled={isSubmitting}
                className="cursor-pointer"
              >
                {t('goals.cancel')}
              </Button>
              <Button
                type="submit"
                disabled={contributeInsufficient || parsedActionAmount <= 0 || isSubmitting}
                className="bg-emerald-600 hover:bg-emerald-700 text-white font-bold disabled:opacity-50 cursor-pointer"
              >
                {isSubmitting ? (locale === 'vi-VN' ? 'Đang xử lý...' : 'Processing...') : t('goals.confirmContribute')}
              </Button>
            </div>
          </form>
        </div>
      )}

      {/* 5. WITHDRAW MODAL */}
      {withdrawingGoal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-xs animate-in fade-in duration-150">
          <form
            onSubmit={handleConfirmWithdraw}
            onKeyDown={(e) => {
              if (e.key === 'Enter' && (e.target as HTMLElement).tagName !== 'TEXTAREA') {
                e.preventDefault();
                handleConfirmWithdraw(e);
              }
            }}
            className="w-full max-w-md bg-white dark:bg-slate-900 rounded-3xl p-6 sm:p-7 shadow-2xl border border-slate-100 dark:border-slate-800 space-y-5 animate-in zoom-in-95"
          >
            <div className="flex items-center justify-between">
              <div className="flex items-center gap-2.5">
                <div className="h-9 w-9 rounded-2xl bg-amber-100 dark:bg-amber-950 text-amber-600 dark:text-amber-400 flex items-center justify-center">
                  <ArrowDownLeft className="h-5 w-5" />
                </div>
                <div>
                  <h3 className="text-base font-bold text-slate-900 dark:text-white">
                    {t('goals.withdrawModalTitle')}
                  </h3>
                  <span className="text-xs text-slate-400">{withdrawingGoal.title}</span>
                </div>
              </div>
              <button
                type="button"
                onClick={() => setWithdrawingGoal(null)}
                className="p-1.5 rounded-xl text-slate-400 hover:bg-slate-100 dark:hover:bg-slate-800 cursor-pointer"
              >
                <X className="h-4 w-4" />
              </button>
            </div>

            {errorMessage && (
              <div className="p-3 rounded-2xl bg-rose-50 dark:bg-rose-950/30 text-rose-700 dark:text-rose-300 text-xs font-semibold flex items-center gap-2">
                <AlertCircle className="h-4 w-4 shrink-0" />
                <span>{errorMessage}</span>
              </div>
            )}

            {withdrawExcess && (
              <div className="p-3 rounded-2xl bg-rose-50 dark:bg-rose-950/30 text-rose-700 dark:text-rose-300 text-xs font-semibold flex items-center gap-2">
                <AlertCircle className="h-4 w-4 shrink-0" />
                <span>
                  {locale === 'vi-VN'
                    ? 'Số tiền rút vượt quá số dư hiện tại của mục tiêu.'
                    : 'Withdrawal amount exceeds current goal savings.'}
                </span>
              </div>
            )}

            <div className="space-y-4 text-xs">
              <div className="space-y-1.5">
                <label className="font-bold text-slate-700 dark:text-slate-300">
                  {t('goals.destWallet')}
                </label>
                <Select
                  value={actionWalletId}
                  onChange={(val) => setActionWalletId(val)}
                  accentColor="emerald"
                >
                  {wallets.map((w) => (
                    <option key={w.id} value={w.id}>
                      {w.name} ({formatMoney(w.balance, locale)})
                    </option>
                  ))}
                </Select>
              </div>

              <div className="space-y-1.5">
                <label className="font-bold text-slate-700 dark:text-slate-300">
                  {t('goals.amountToWithdraw')} (VND) *
                </label>
                <input
                  type="text"
                  inputMode="numeric"
                  value={actionAmount}
                  onChange={(e) => setActionAmount(sanitizeIntegerInput(e.target.value))}
                  required
                  className="w-full px-4 py-2.5 text-sm rounded-2xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-slate-900 dark:text-white font-bold"
                />
              </div>

              {/* Transaction Preview Section */}
              <div className="p-4 rounded-2xl bg-slate-50 dark:bg-slate-800/50 border border-slate-100 dark:border-slate-800 space-y-2 text-xs">
                <div className="flex justify-between text-slate-400">
                  <span>{locale === 'vi-VN' ? 'Số dư mục tiêu hiện tại:' : 'Current goal balance:'}</span>
                  <span className="font-semibold text-slate-700 dark:text-slate-300">
                    {formatMoney(withdrawingGoal.currentAmount, locale)}
                  </span>
                </div>
                <div className="flex justify-between text-slate-400">
                  <span>{t('goals.previewWithdrawAmount')}:</span>
                  <span className="font-semibold text-amber-600">
                    -{formatMoney(parsedActionAmount, locale)}
                  </span>
                </div>
                <div className="h-px bg-slate-200 dark:bg-slate-700 my-1" />
                <div className="flex justify-between text-slate-700 dark:text-slate-200 font-bold">
                  <span>{t('goals.previewWalletAfter')}:</span>
                  <span className="tabular-nums">
                    {formatMoney(previewWalletAfterWithdraw, locale)}
                  </span>
                </div>
                <div className="flex justify-between text-emerald-600 font-bold">
                  <span>{t('goals.previewGoalAfter')}:</span>
                  <span className="tabular-nums">
                    {formatMoney(previewGoalAfterWithdraw, locale)}
                  </span>
                </div>
              </div>
            </div>

            <div className="flex items-center justify-end gap-3 pt-3 border-t border-slate-100 dark:border-slate-800">
              <Button
                type="button"
                variant="outline"
                onClick={() => setWithdrawingGoal(null)}
                disabled={isSubmitting}
                className="cursor-pointer"
              >
                {t('goals.cancel')}
              </Button>
              <Button
                type="submit"
                disabled={withdrawExcess || parsedActionAmount <= 0 || isSubmitting}
                className="bg-emerald-600 hover:bg-emerald-700 text-white font-bold disabled:opacity-50 cursor-pointer"
              >
                {isSubmitting ? (locale === 'vi-VN' ? 'Đang xử lý...' : 'Processing...') : t('goals.confirmWithdraw')}
              </Button>
            </div>
          </form>
        </div>
      )}

      {/* 6. GOAL DETAILS MODAL & HISTORY */}
      {selectedGoalDetails && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-xs animate-in fade-in duration-150">
          <div className="w-full max-w-lg bg-white dark:bg-slate-900 rounded-3xl p-6 sm:p-7 shadow-2xl border border-slate-100 dark:border-slate-800 space-y-5 animate-in zoom-in-95">
            <div className="flex items-center justify-between">
              <div className="flex items-center gap-2.5">
                <div className="h-10 w-10 rounded-2xl bg-emerald-100 dark:bg-emerald-950 text-emerald-600 dark:text-emerald-400 flex items-center justify-center">
                  {getGoalIcon(selectedGoalDetails.iconName)}
                </div>
                <div>
                  <h3 className="text-base font-bold text-slate-900 dark:text-white">
                    {selectedGoalDetails.title}
                  </h3>
                  <span className="text-xs text-slate-400">{getLocalizedGoalCategory(selectedGoalDetails.category, t, locale)}</span>
                </div>
              </div>
              <button
                type="button"
                onClick={() => setSelectedGoalDetails(null)}
                className="p-1.5 rounded-xl text-slate-400 hover:bg-slate-100 dark:hover:bg-slate-800 cursor-pointer"
              >
                <X className="h-4 w-4" />
              </button>
            </div>

            {/* Savings Overview */}
            <div className="grid grid-cols-2 gap-3 p-4 bg-slate-50 dark:bg-slate-800/40 rounded-2xl border border-slate-100 dark:border-slate-800 text-xs">
              <div>
                <span className="text-slate-400 block">{t('goals.currentSavings')}</span>
                <span className="text-lg font-bold text-emerald-600 dark:text-emerald-400 tabular-nums">
                  {formatMoney(selectedGoalDetails.currentAmount, locale)}
                </span>
              </div>
              <div>
                <span className="text-slate-400 block">{t('goals.remainingAmount')}</span>
                <span className="text-lg font-bold text-slate-800 dark:text-slate-200 tabular-nums">
                  {formatMoney(
                    Math.max(0, selectedGoalDetails.targetAmount - selectedGoalDetails.currentAmount),
                    locale
                  )}
                </span>
              </div>
            </div>

            {/* History Table */}
            <div className="space-y-3">
              <span className="text-xs font-bold text-slate-700 dark:text-slate-300">
                {t('goals.historyTitle')} ({selectedGoalDetails.history.length})
              </span>

              <div className="max-h-56 overflow-y-auto divide-y divide-slate-100 dark:divide-slate-800 border border-slate-100 dark:border-slate-800 rounded-2xl p-2 bg-slate-50/50 dark:bg-slate-800/30">
                {selectedGoalDetails.history.length === 0 ? (
                  <p className="py-6 text-center text-xs text-slate-400 font-medium">
                    {t('goals.noHistory')}
                  </p>
                ) : (
                  selectedGoalDetails.history.map((h) => (
                    <div key={h.id} className="py-2 px-3 flex items-center justify-between text-xs">
                      <div>
                        <p className="font-bold text-slate-800 dark:text-slate-200">
                          {h.type === 'deposit' ? t('goals.goalDeposit') : t('goals.goalWithdraw')}
                        </p>
                        <span className="text-[11px] text-slate-400">
                          {formatDate(h.date, locale)} • {h.sourceWalletName || (locale === 'vi-VN' ? 'Ví chính' : 'Main Wallet')}
                        </span>
                      </div>
                      <span
                        className={cn(
                          'font-bold tabular-nums',
                          h.type === 'deposit'
                            ? 'text-emerald-600 dark:text-emerald-400'
                            : 'text-amber-600 dark:text-amber-400'
                        )}
                      >
                        {h.type === 'deposit' ? '+' : '-'}
                        {formatMoney(h.amount, locale)}
                      </span>
                    </div>
                  ))
                )}
              </div>
            </div>

            <div className="flex items-center justify-between pt-2 border-t border-slate-100 dark:border-slate-800">
              <div className="flex items-center gap-2">
                <Button
                  size="sm"
                  variant="outline"
                  onClick={() => {
                    handleOpenEdit(selectedGoalDetails);
                  }}
                  className="text-xs font-semibold gap-1 cursor-pointer"
                >
                  <Edit3 className="h-3.5 w-3.5" />
                  <span>{t('goals.editGoal')}</span>
                </Button>
                <Button
                  size="sm"
                  variant="outline"
                  onClick={() => {
                    setDeletingGoal(selectedGoalDetails);
                  }}
                  className="text-xs text-rose-600 dark:text-rose-400 border-rose-200 dark:border-rose-900/40 hover:bg-rose-50 dark:hover:bg-rose-950/30 font-semibold gap-1 cursor-pointer"
                >
                  <Trash2 className="h-3.5 w-3.5" />
                  <span>{t('goals.deleteGoal')}</span>
                </Button>
              </div>

              <div className="flex items-center gap-2">
                <Button
                  size="sm"
                  onClick={() => {
                    const g = selectedGoalDetails;
                    setSelectedGoalDetails(null);
                    handleOpenContribute(g);
                  }}
                  className="bg-emerald-600 hover:bg-emerald-700 text-white font-bold text-xs gap-1 cursor-pointer"
                >
                  <Plus className="h-3.5 w-3.5" />
                  <span>{t('goals.contribute')}</span>
                </Button>
                <Button size="sm" variant="outline" onClick={() => setSelectedGoalDetails(null)} className="cursor-pointer">
                  {t('common.close')}
                </Button>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
