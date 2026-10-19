import React, { useState, useMemo, useEffect, useRef } from 'react';
import { useSearchParams, useNavigate } from 'react-router-dom';
import {
  Search,
  Plus,
  Minus,
  Filter,
  X,
  Calendar,
  Wallet,
  Tag,
  ArrowDownLeft,
  ArrowUpRight,
  Send,
  Download,
  MoreVertical,
  Edit2,
  Eye,
  CheckCircle2,
  AlertCircle,
  Clock,
  Briefcase,
  Utensils,
  Car,
  ShoppingBag,
  Tv,
  Home,
  ShieldCheck,
  TrendingUp,
  TrendingDown,
  Info,
  Copy,
  Check,
  ChevronDown,
  History,
  RefreshCw,
  Sliders,
  ShieldAlert,
  Lock,
  LifeBuoy,
  FileText,
  HelpCircle,
} from 'lucide-react';
import { ActionBadge } from '../../components/ui/ActionBadge';
import { useAuth } from '../../providers/AuthProvider';
import { useI18n } from '../../i18n/I18nContext';
import { useBalanceVisibility } from '../../providers/BalanceVisibilityProvider';
import { useFinancial, uploadEvidenceItems } from '../../providers/FinancialProvider';
import { Transaction, TransactionType, AttachmentMetadata, RecurringTransaction, RecurringFrequency } from '../../types';
import { formatMoney, formatReadableAmount, moneyToWords } from '../../utils/money';
import { formatTransactionTime, formatExactTransactionTime } from '../../utils/date';
import { Button } from '../../components/ui/Button';
import { Select } from '../../components/ui/Select';
import { cn } from '../../utils/cn';
import { sanitizeIntegerInput, parseSafePositiveInteger } from '../../utils/inputValidation';
import {
  getLocalizedTxTitle,
  getLocalizedTxDesc,
  getLocalizedTxCounterparty,
  getLocalizedWalletName,
  getLocalizedCategoryName,
  getLocalizedErrorMessage,
} from '../../utils/localizationHelpers';
import { AttachmentUploader } from '../../components/support/AttachmentUploader';
import { Skeleton } from '../../components/ui/Skeleton';
import { AttachmentList } from '../../components/support/AttachmentList';
import { CategoryManagerModal } from './modals/CategoryManagerModal';
import { RecurringTransactionsTab } from './components/RecurringTransactionsTab';
import { TransactionRulesTab } from './components/TransactionRulesTab';
import { getTransactionEffectiveDate } from '../../utils/analyticsHelpers';
import { useSecurityPinChallenge } from '../../providers/SecurityPinChallengeProvider';

const ALLOWED_EVIDENCE_MIME_TYPES = ['image/jpeg', 'image/png', 'image/webp', 'application/pdf'];

export const TransactionsPage: React.FC = () => {
  const { currentUser } = useAuth();
  const { t, locale } = useI18n();
  const { isHidden } = useBalanceVisibility();
  const { challengePin, challengePinWithValue } = useSecurityPinChallenge();
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const {
    isLoading,
    transactions,
    wallets,
    recurringTransactions,
    rules,
    categories,
    addIncome,
    addExpense,
    editTransaction,
    correctTransaction,
    dueRecurringCount,
    recordRecurringOccurrence,
    getTransactionDetail,
    downloadEvidenceBlob,
  } = useFinancial();

  // Pending recurring payment occurrence being recorded
  const [pendingRecurringOccurrence, setPendingRecurringOccurrence] = useState<{
    recurringId: string;
    occurrenceId: string;
    attemptIdempotencyKey: string;
    recurringItem: RecurringTransaction;
    uploadedEvidenceIds?: string[];
  } | null>(null);
  const [isRecordingRecurring, setIsRecordingRecurring] = useState(false);
  const isRecordingRecurringRef = useRef(false);

  // Primary sub-navigation tab: History vs Recurring vs Rules
  const [activeMainTab, setActiveMainTab] = useState<'history' | 'recurring' | 'rules'>('history');
  const [isCategoryModalOpen, setIsCategoryModalOpen] = useState(false);

  // Search & Filter state
  const [searchTerm, setSearchTerm] = useState('');
  const [typeFilter, setTypeFilter] = useState<'all' | 'income' | 'expense' | 'transfer_out' | 'transfer_in' | 'bank_top_up'>('all');
  const [timeFilter, setTimeFilter] = useState<'all' | 'today' | 'week' | 'month'>('all');
  const [categoryFilter, setCategoryFilter] = useState('all');
  const [walletFilter, setWalletFilter] = useState('all');
  const [amountFilter, setAmountFilter] = useState<'all' | 'under100k' | '100kTo1m' | 'over1m'>('all');

  // Handle URL search params (e.g., ?tab=history | recurring | rules | income | expense | transfer)
  useEffect(() => {
    const tab = searchParams.get('tab');
    const view = searchParams.get('view');

    if (tab === 'recurring' || view === 'recurring') {
      setActiveMainTab('recurring');
    } else if (tab === 'rules' || view === 'rules') {
      setActiveMainTab('rules');
    } else if (tab === 'categories' || view === 'categories') {
      setIsCategoryModalOpen(true);
    } else if (tab === 'history' || view === 'history') {
      setActiveMainTab('history');
    } else if (tab === 'income') {
      setActiveMainTab('history');
      setTypeFilter('income');
    } else if (tab === 'expense') {
      setActiveMainTab('history');
      setTypeFilter('expense');
    } else if (tab === 'transfer_out') {
      setActiveMainTab('history');
      setTypeFilter('transfer_out');
    } else if (tab === 'transfer_in') {
      setActiveMainTab('history');
      setTypeFilter('transfer_in');
    } else if (tab === 'transfer') {
      setActiveMainTab('history');
      setTypeFilter('transfer_out');
    } else if (tab === 'bank_top_up' || tab === 'top_up') {
      setActiveMainTab('history');
      setTypeFilter('bank_top_up');
    } else if (tab === 'all') {
      setActiveMainTab('history');
      setTypeFilter('all');
    }

    const filter = searchParams.get('filter');
    if (filter === 'bank_top_up' || filter === 'top_up') {
      setActiveMainTab('history');
      setTypeFilter('bank_top_up');
    } else if (filter === 'income') {
      setActiveMainTab('history');
      setTypeFilter('income');
    } else if (filter === 'expense') {
      setActiveMainTab('history');
      setTypeFilter('expense');
    }

    const wallet = searchParams.get('wallet');
    if (wallet) setWalletFilter(wallet);

    const category = searchParams.get('category');
    if (category) setCategoryFilter(category);

    const txId = searchParams.get('txId') || searchParams.get('id');
    if (txId) {
      const match = transactions.find((tx) => tx.id === txId || tx.referenceCode === txId);
      if (match) setSelectedTx(match);
    }
  }, [searchParams, transactions]);

  const handleTabChange = (newTab: 'history' | 'recurring' | 'rules') => {
    setActiveMainTab(newTab);
    setSearchParams((prev) => {
      const next = new URLSearchParams(prev);
      next.set('tab', newTab);
      next.delete('view');
      return next;
    });
  };

  // Pagination state
  const PAGE_SIZE = 10;
  const [visibleCount, setVisibleCount] = useState(PAGE_SIZE);
  const [copiedCode, setCopiedCode] = useState(false);
  const [copiedId, setCopiedId] = useState<string | null>(null);

  const handleCopyId = (id: string) => {
    navigator.clipboard.writeText(id).then(() => {
      setCopiedId(id);
      setTimeout(() => setCopiedId(null), 1500);
    }).catch(() => {});
  };

  // Modal / Drawer states
  const [selectedTx, setSelectedTx] = useState<Transaction | null>(null);
  const [isAddIncomeOpen, setIsAddIncomeOpen] = useState(false);
  const [isAddExpenseOpen, setIsAddExpenseOpen] = useState(false);
  const [editingTx, setEditingTx] = useState<Transaction | null>(null);
  const [correctingTx, setCorrectingTx] = useState<Transaction | null>(null);
  const [toastMessage, setToastMessage] = useState<string | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  // Synchronize selectedTx when transactions update (e.g. after a correction or admin grant)
  useEffect(() => {
    if (selectedTx) {
      const fresh = transactions.find((t) => t.id === selectedTx.id);
      if (fresh) setSelectedTx((prev) => (prev ? { ...prev, ...fresh } : fresh));
    }
  }, [transactions]);

  // Load detailed transaction from backend if available
  useEffect(() => {
    if (!selectedTx?.id || !getTransactionDetail) return;
    let isMounted = true;
    getTransactionDetail(selectedTx.id)
      .then((detailed) => {
        if (isMounted && detailed) {
          setSelectedTx((prev) => (prev?.id === detailed.id ? { ...prev, ...detailed } : prev));
        }
      })
      .catch(() => {});
    return () => {
      isMounted = false;
    };
  }, [selectedTx?.id, getTransactionDetail]);

  // Read-only evidence for currently selected transaction
  const selectedTxEvidence = useMemo(() => {
    if (!selectedTx) return [];
    if (selectedTx.evidence && selectedTx.evidence.length > 0) return selectedTx.evidence;
    return [];
  }, [selectedTx]);

  // Helper for local datetime-local ISO format YYYY-MM-DDTHH:mm
  const getLocalDateTimeString = (dateInput?: Date | string) => {
    const d = dateInput ? new Date(dateInput) : new Date();
    if (isNaN(d.getTime())) return '';
    const pad = (n: number) => String(n).padStart(2, '0');
    const year = d.getFullYear();
    const month = pad(d.getMonth() + 1);
    const day = pad(d.getDate());
    const hours = pad(d.getHours());
    const minutes = pad(d.getMinutes());
    return `${year}-${month}-${day}T${hours}:${minutes}`;
  };

  // Escape key handler for modals
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        if (isRecordingRecurring) return;
        setSelectedTx(null);
        setIsAddIncomeOpen(false);
        setIsAddExpenseOpen(false);
        setPendingRecurringOccurrence(null);
        setErrorMessage(null);
        setEditingTx(null);
        setCorrectingTx(null);
        setIsCategoryModalOpen(false);
      }
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [isRecordingRecurring]);

  // Default wallet finder
  const defaultWalletId = wallets.find((w) => w.isDefault)?.id || wallets[0]?.id || '';

  // Form states for Income / Expense
  const [formAmount, setFormAmount] = useState('');
  const [formCategory, setFormCategory] = useState('categories.salary');
  const [formWalletId, setFormWalletId] = useState(defaultWalletId);
  const [formDate, setFormDate] = useState(new Date().toISOString().split('T')[0]);
  const [formDateTime, setFormDateTime] = useState(getLocalDateTimeString());
  const [formEvidence, setFormEvidence] = useState<AttachmentMetadata[]>([]);
  const [formDesc, setFormDesc] = useState('');
  const [formCounterparty, setFormCounterparty] = useState('');

  // Form states for Controlled Correction
  const [corrAmount, setCorrAmount] = useState('');
  const [corrCategory, setCorrCategory] = useState('');
  const [corrWalletId, setCorrWalletId] = useState('');
  const [corrDateTime, setCorrDateTime] = useState('');
  const [corrDesc, setCorrDesc] = useState('');
  const [corrCounterparty, setCorrCounterparty] = useState('');
  const [corrReason, setCorrReason] = useState('');
  const [corrAdditionalEvidence, setCorrAdditionalEvidence] = useState<AttachmentMetadata[]>([]);
  const [corrErrorMessage, setCorrErrorMessage] = useState<string | null>(null);

  const showToast = (msg: string) => {
    setToastMessage(msg);
    setTimeout(() => setToastMessage(null), 3000);
  };

  // Reset pagination when any filter changes
  useEffect(() => {
    setVisibleCount(PAGE_SIZE);
  }, [searchTerm, typeFilter, timeFilter, categoryFilter, walletFilter, amountFilter]);

  const handleCopyTxCode = (code: string) => {
    navigator.clipboard?.writeText(code);
    setCopiedCode(true);
    setTimeout(() => setCopiedCode(false), 2000);
    showToast(locale === 'vi-VN' ? 'Đã sao chép mã giao dịch' : 'Transaction code copied');
  };

  // Top summary stats derived strictly for current calendar month
  const summaryStats = useMemo(() => {
    const now = new Date();
    const currentMonth = now.getMonth();
    const currentYear = now.getFullYear();

    let income = 0;
    let expense = 0;

    transactions.forEach((tx) => {
      if (tx.status === 'completed' && Boolean(currentUser?.id) && tx.userId === currentUser?.id) {
        const txDate = getTransactionEffectiveDate(tx);
        if (txDate && txDate.getMonth() === currentMonth && txDate.getFullYear() === currentYear) {
          const isExcluded =
            tx.semanticType === 'transfer' ||
            tx.semanticType === 'group_settlement' ||
            tx.semanticType === 'refund' ||
            tx.semanticType === 'goal_allocation' ||
            tx.semanticType === 'unknown';

          if (!isExcluded) {
            const isIncome =
              tx.semanticType === 'income' ||
              (!tx.semanticType && tx.type === 'income');
            const isExpense =
              tx.semanticType === 'expense' ||
              (!tx.semanticType && tx.type === 'expense');

            if (isIncome) {
              income += Math.abs(tx.amount);
            } else if (isExpense) {
              expense += Math.abs(tx.amount);
            }
          }
        }
      }
    });

    return {
      income,
      expense,
      difference: income - expense,
      totalCount: transactions.length,
    };
  }, [transactions, currentUser?.id]);

  // Active filters count
  const isFiltered =
    searchTerm !== '' ||
    typeFilter !== 'all' ||
    timeFilter !== 'all' ||
    categoryFilter !== 'all' ||
    walletFilter !== 'all' ||
    amountFilter !== 'all';

  const resetFilters = () => {
    setSearchTerm('');
    setTypeFilter('all');
    setTimeFilter('all');
    setCategoryFilter('all');
    setWalletFilter('all');
    setAmountFilter('all');
  };

  // Filtered transactions
  const filteredTransactions = useMemo(() => {
    return transactions.filter((tx) => {
      // Type tab
      if (typeFilter === 'income' && tx.type !== 'income') return false;
      if (typeFilter === 'expense' && tx.type !== 'expense') return false;
      if (typeFilter === 'transfer_out' && tx.type !== 'transfer_out') return false;
      if (typeFilter === 'transfer_in' && tx.type !== 'transfer_in') return false;
      if (typeFilter === 'bank_top_up' && tx.type !== 'bank_top_up') return false;

      // Search term
      if (searchTerm) {
        const query = searchTerm.toLowerCase();
        const matchTitle =
          tx.title.toLowerCase().includes(query) ||
          tx.titleEn?.toLowerCase().includes(query);
        const matchDesc =
          tx.description?.toLowerCase().includes(query) ||
          tx.descriptionEn?.toLowerCase().includes(query);
        const matchCounterparty =
          tx.counterparty?.toLowerCase().includes(query) ||
          tx.counterpartyEn?.toLowerCase().includes(query);
        const matchCode = tx.referenceCode?.toLowerCase().includes(query);
        if (!matchTitle && !matchDesc && !matchCounterparty && !matchCode) return false;
      }

      // Category filter
      if (categoryFilter !== 'all' && tx.category !== categoryFilter) {
        return false;
      }

      // Wallet filter
      if (walletFilter !== 'all' && tx.walletId !== walletFilter) {
        return false;
      }

      // Amount filter
      const absAmount = Math.abs(tx.amount);
      if (amountFilter === 'under100k' && absAmount >= 100000) return false;
      if (amountFilter === '100kTo1m' && (absAmount < 100000 || absAmount > 1000000)) return false;
      if (amountFilter === 'over1m' && absAmount <= 1000000) return false;

      // Time filter
      if (timeFilter !== 'all') {
        const txDate = new Date(tx.date);
        const now = new Date();
        if (timeFilter === 'today') {
          const isToday = txDate.toDateString() === now.toDateString();
          if (!isToday) return false;
        } else if (timeFilter === 'week') {
          const weekAgo = new Date(now.getTime() - 7 * 24 * 60 * 60 * 1000);
          if (txDate < weekAgo) return false;
        } else if (timeFilter === 'month') {
          if (txDate.getMonth() !== now.getMonth() || txDate.getFullYear() !== now.getFullYear()) {
            return false;
          }
        }
      }

      return true;
    });
  }, [transactions, typeFilter, searchTerm, categoryFilter, walletFilter, amountFilter, timeFilter]);

  // Icon resolver
  const getCategoryIcon = (category: string, type: TransactionType) => {
    if (type === 'unknown') {
      return <HelpCircle className="h-4 w-4 text-slate-400" />;
    }
    if (type === 'transfer_in' || type === 'goal_withdrawal' || type === 'bank_top_up' || type === 'deposit') {
      return <ArrowDownLeft className="h-4 w-4 text-emerald-600 dark:text-emerald-400" />;
    }
    if (type === 'transfer_out' || type === 'goal_contribution' || type === 'withdrawal' || type === 'group_fund_contribution') {
      return <Send className="h-4 w-4 text-teal-600 dark:text-teal-400" />;
    }
    if (type === 'refund' || type === 'reversal') {
      return <RefreshCw className="h-4 w-4 text-amber-600 dark:text-amber-400" />;
    }
    if (type === 'internal_transfer') {
      return <RefreshCw className="h-4 w-4 text-teal-600 dark:text-teal-400" />;
    }
    if (category.includes('salary') || type === 'income') {
      return <Briefcase className="h-4 w-4 text-emerald-600 dark:text-emerald-400" />;
    }
    if (category.includes('food')) {
      return <Utensils className="h-4 w-4 text-amber-600 dark:text-amber-400" />;
    }
    if (category.includes('transport')) {
      return <Car className="h-4 w-4 text-sky-600 dark:text-sky-400" />;
    }
    if (category.includes('shopping')) {
      return <ShoppingBag className="h-4 w-4 text-emerald-600 dark:text-emerald-400" />;
    }
    if (category.includes('entertainment')) {
      return <Tv className="h-4 w-4 text-indigo-600 dark:text-indigo-400" />;
    }
    if (category.includes('utilities')) {
      return <Home className="h-4 w-4 text-teal-600 dark:text-teal-400" />;
    }
    return <ArrowUpRight className="h-4 w-4 text-slate-500 dark:text-slate-400" />;
  };

  const getTransactionTypeLabel = (type: TransactionType) => {
    switch (type) {
      case 'income':
        return t('transactions.typeIncome');
      case 'expense':
        return t('transactions.typeExpense');
      case 'transfer_out':
        return t('transactions.typeTransferOut');
      case 'transfer_in':
        return t('transactions.typeTransferIn');
      case 'bank_top_up':
        return locale === 'vi-VN' ? 'Nạp tiền qua SePay' : 'SePay Bank Top-up';
      case 'goal_contribution':
        return t('transactions.typeGoalDeposit');
      case 'goal_withdrawal':
        return t('transactions.typeGoalWithdraw');
      case 'internal_transfer':
        return locale === 'vi-VN' ? 'Chuyển tiền nội bộ' : 'Internal Transfer';
      case 'group_fund_contribution':
        return locale === 'vi-VN' ? 'Đóng quỹ nhóm' : 'Group Fund Contribution';
      case 'debt_settlement':
        return locale === 'vi-VN' ? 'Thanh toán nợ' : 'Debt Settlement';
      case 'deposit':
        return locale === 'vi-VN' ? 'Nạp tiền' : 'Deposit';
      case 'withdrawal':
        return locale === 'vi-VN' ? 'Rút tiền' : 'Withdrawal';
      case 'refund':
        return locale === 'vi-VN' ? 'Hoàn tiền' : 'Refund';
      case 'reversal':
        return locale === 'vi-VN' ? 'Hoàn đảo giao dịch' : 'Reversal';
      default:
        return locale === 'vi-VN' ? 'Không xác định' : 'Unknown';
    }
  };

  const getCategoryName = (cat: string) => {
    return getLocalizedCategoryName(cat, t, categories);
  };

  // Selected wallet for form validation
  const currentFormWallet = wallets.find((w) => w.id === formWalletId) || wallets[0];
  const { valid: isFormAmountValid, value: parsedFormAmount } = parseSafePositiveInteger(formAmount);
  const isExpenseInsufficient =
    isAddExpenseOpen && currentFormWallet && isFormAmountValid && parsedFormAmount > currentFormWallet.balance;

  // Close handlers that properly clear pending recurring occurrence state
  const handleCloseAddIncome = () => {
    if (isRecordingRecurring) return;
    setIsAddIncomeOpen(false);
    setPendingRecurringOccurrence(null);
    setFormEvidence([]);
    setErrorMessage(null);
  };

  const handleCloseAddExpense = () => {
    if (isRecordingRecurring) return;
    setIsAddExpenseOpen(false);
    setPendingRecurringOccurrence(null);
    setFormEvidence([]);
    setErrorMessage(null);
  };

  // Handlers for Add Income
  const handleOpenAddIncome = () => {
    setPendingRecurringOccurrence(null);
    setFormAmount('');
    const incomeCats = categories.filter((c) => c.kind === 'income' && !c.isArchived);
    setFormCategory(incomeCats[0]?.key || '');
    setFormWalletId(defaultWalletId);
    setFormDate(new Date().toISOString().split('T')[0]);
    setFormDateTime(getLocalDateTimeString());
    setFormEvidence([]);
    setFormDesc('');
    setFormCounterparty('');
    setErrorMessage(null);
    setIsAddIncomeOpen(true);
  };

  const handleSaveIncome = async (e: React.FormEvent) => {
    e.preventDefault();
    if (pendingRecurringOccurrence) {
      if (isRecordingRecurringRef.current) return;
      if (!formEvidence || formEvidence.length === 0) {
        setErrorMessage(
          locale === 'vi-VN'
            ? 'Chứng từ giao dịch (hình ảnh hoặc hóa đơn PDF) là bắt buộc khi ghi nhận thanh toán định kỳ.'
            : 'Transaction evidence (image or PDF receipt) is required for recurring payment.'
        );
        return;
      }
      if (formEvidence.length > 3) {
        setErrorMessage(
          locale === 'vi-VN'
            ? 'Chỉ được tải lên tối đa 3 tệp chứng từ.'
            : 'You can upload at most 3 evidence documents.'
        );
        return;
      }
      const hasInvalidMime = formEvidence.some((ev) => !ALLOWED_EVIDENCE_MIME_TYPES.includes(ev.type));
      if (hasInvalidMime) {
        setErrorMessage(
          locale === 'vi-VN'
            ? 'Định dạng tệp không hợp lệ. Chỉ chấp nhận JPG, PNG, WEBP hoặc PDF.'
            : 'Invalid file format. Only JPG, PNG, WEBP, or PDF are accepted.'
        );
        return;
      }

      const scheduledAmount = pendingRecurringOccurrence.recurringItem.amount;
      const pin = await challengePinWithValue({
        actionName: locale === 'vi-VN' ? 'Ghi nhận thanh toán định kỳ' : 'Record Recurring Payment',
        amount: scheduledAmount,
      });
      if (!pin) return;

      if (isRecordingRecurringRef.current) return;
      isRecordingRecurringRef.current = true;
      setIsRecordingRecurring(true);
      try {
        let evidenceIds = pendingRecurringOccurrence.uploadedEvidenceIds;
        if (!evidenceIds || evidenceIds.length === 0) {
          evidenceIds = await uploadEvidenceItems(formEvidence);
          setPendingRecurringOccurrence((prev) => (prev ? { ...prev, uploadedEvidenceIds: evidenceIds } : null));
        }

        const res = await recordRecurringOccurrence(
          pendingRecurringOccurrence.recurringId,
          pendingRecurringOccurrence.occurrenceId,
          {
            securityPin: pin,
            evidenceIds,
            idempotencyKey: pendingRecurringOccurrence.attemptIdempotencyKey,
          }
        );

        if (res.success) {
          setPendingRecurringOccurrence(null);
          setIsAddIncomeOpen(false);
          setFormEvidence([]);
          setErrorMessage(null);
          showToast(
            locale === 'vi-VN'
              ? `Đã ghi nhận thanh toán định kỳ +${formatMoney(scheduledAmount, locale)} vào ví!`
              : `Recurring payment +${formatMoney(scheduledAmount, locale)} was recorded successfully.`
          );
        } else {
          setErrorMessage(
            getLocalizedErrorMessage(res.error, locale, t, 'common.errorOccurred')
          );
        }
      } catch (err) {
        setErrorMessage(
          getLocalizedErrorMessage(err instanceof Error ? err.message : String(err), locale, t, 'common.errorOccurred')
        );
      } finally {
        isRecordingRecurringRef.current = false;
        setIsRecordingRecurring(false);
      }
      return;
    }

    if (!isFormAmountValid || parsedFormAmount <= 0) {
      setErrorMessage(locale === 'vi-VN' ? 'Vui lòng nhập số tiền hợp lệ (> 0 ₫)' : 'Please enter a valid amount (> 0 ₫)');
      return;
    }
    if (!formCategory) {
      setErrorMessage(locale === 'en-US' ? 'Please select a category.' : 'Vui lòng chọn danh mục thu nhập.');
      return;
    }

    if (!formEvidence || formEvidence.length === 0) {
      setErrorMessage(
        locale === 'vi-VN'
          ? 'Chứng từ giao dịch (hình ảnh hoặc hóa đơn PDF) là bắt buộc khi ghi nhận thu thủ công.'
          : 'Transaction evidence (image or PDF receipt) is required for manual entry.'
      );
      return;
    }

    const occurredIso = formDateTime ? new Date(formDateTime).toISOString() : new Date().toISOString();

    const pin = await challengePinWithValue({
      actionName: locale === 'vi-VN' ? 'Ghi nhận thu nhập' : 'Record Income',
      amount: parsedFormAmount,
    });
    if (!pin) return;

    const res = await addIncome({
      amount: parsedFormAmount,
      category: formCategory,
      walletId: formWalletId,
      description: formDesc || (locale === 'vi-VN' ? 'Khoản thu nhập' : 'Income'),
      occurredAt: occurredIso,
      date: occurredIso,
      counterparty: formCounterparty,
      evidence: formEvidence,
      securityPin: pin,
    });

    if (res.success) {
      setIsAddIncomeOpen(false);
      setFormEvidence([]);
      showToast(
        locale === 'vi-VN'
          ? `Đã ghi nhận khoản thu +${formatMoney(parsedFormAmount, locale)} vào ví!`
          : `Income +${formatMoney(parsedFormAmount, locale)} was recorded successfully.`
      );
    } else {
      setErrorMessage(
        getLocalizedErrorMessage(res.error, locale, t, 'common.errorOccurred')
      );
    }
  };

  // Pre-fill and open Add Expense/Income for Recurring Due Payment
  const handleRecordRecurringPayment = (item: RecurringTransaction) => {
    const targetOccurrence = item.occurrences?.find((o) => o.status === 'Due' || o.status === 'Overdue');

    if (!targetOccurrence || !targetOccurrence.id || targetOccurrence.status === 'Recorded') {
      showToast(
        locale === 'vi-VN'
          ? 'Không tìm thấy kỳ thanh toán hợp lệ (Đến hạn hoặc Quá hạn) để ghi nhận.'
          : 'No valid Due or Overdue recurring occurrence found to process.'
      );
      return;
    }

    setPendingRecurringOccurrence({
      recurringId: item.id,
      occurrenceId: targetOccurrence.id,
      attemptIdempotencyKey: crypto.randomUUID(),
      recurringItem: item,
      uploadedEvidenceIds: undefined,
    });

    setFormAmount(String(item.amount));
    const matchedCat = categories.find((c) => c.id === item.categoryId || c.key === item.categoryId);
    setFormCategory(matchedCat ? matchedCat.key : (item.categoryId || ''));
    setFormWalletId(item.walletId || defaultWalletId);
    const schedDate = targetOccurrence.scheduledFor ? targetOccurrence.scheduledFor.split('T')[0] : (item.nextRunDate || new Date().toISOString().split('T')[0]);
    setFormDate(schedDate);
    setFormDateTime(getLocalDateTimeString(targetOccurrence.scheduledFor || item.nextRunDate || new Date()));
    setFormDesc(item.title + (item.description ? ` (${item.description})` : ''));
    setFormCounterparty('');
    setFormEvidence([]);
    setErrorMessage(null);

    if (item.type === 'income') {
      setIsAddIncomeOpen(true);
    } else {
      setIsAddExpenseOpen(true);
    }
  };

  const handleOpenAddExpense = () => {
    setPendingRecurringOccurrence(null);
    setFormAmount('');
    setFormCategory('');
    setFormWalletId(defaultWalletId);
    setFormDate(new Date().toISOString().split('T')[0]);
    setFormDateTime(getLocalDateTimeString());
    setFormDesc('');
    setFormCounterparty('');
    setFormEvidence([]);
    setErrorMessage(null);
    setIsAddExpenseOpen(true);
  };

  const handleSaveExpense = async (e: React.FormEvent) => {
    e.preventDefault();
    if (pendingRecurringOccurrence) {
      if (isRecordingRecurringRef.current) return;
      if (!formEvidence || formEvidence.length === 0) {
        setErrorMessage(
          locale === 'vi-VN'
            ? 'Chứng từ giao dịch (hình ảnh hoặc hóa đơn PDF) là bắt buộc khi ghi nhận thanh toán định kỳ.'
            : 'Transaction evidence (image or PDF receipt) is required for recurring payment.'
        );
        return;
      }
      if (formEvidence.length > 3) {
        setErrorMessage(
          locale === 'vi-VN'
            ? 'Chỉ được tải lên tối đa 3 tệp chứng từ.'
            : 'You can upload at most 3 evidence documents.'
        );
        return;
      }
      const hasInvalidMime = formEvidence.some((ev) => !ALLOWED_EVIDENCE_MIME_TYPES.includes(ev.type));
      if (hasInvalidMime) {
        setErrorMessage(
          locale === 'vi-VN'
            ? 'Định dạng tệp không hợp lệ. Chỉ chấp nhận JPG, PNG, WEBP hoặc PDF.'
            : 'Invalid file format. Only JPG, PNG, WEBP, or PDF are accepted.'
        );
        return;
      }

      const scheduledAmount = pendingRecurringOccurrence.recurringItem.amount;
      const pin = await challengePinWithValue({
        actionName: locale === 'vi-VN' ? 'Ghi nhận thanh toán định kỳ' : 'Record Recurring Payment',
        amount: scheduledAmount,
      });
      if (!pin) return;

      if (isRecordingRecurringRef.current) return;
      isRecordingRecurringRef.current = true;
      setIsRecordingRecurring(true);
      try {
        let evidenceIds = pendingRecurringOccurrence.uploadedEvidenceIds;
        if (!evidenceIds || evidenceIds.length === 0) {
          evidenceIds = await uploadEvidenceItems(formEvidence);
          setPendingRecurringOccurrence((prev) => (prev ? { ...prev, uploadedEvidenceIds: evidenceIds } : null));
        }

        const res = await recordRecurringOccurrence(
          pendingRecurringOccurrence.recurringId,
          pendingRecurringOccurrence.occurrenceId,
          {
            securityPin: pin,
            evidenceIds,
            idempotencyKey: pendingRecurringOccurrence.attemptIdempotencyKey,
          }
        );

        if (res.success) {
          setPendingRecurringOccurrence(null);
          setIsAddExpenseOpen(false);
          setFormEvidence([]);
          setErrorMessage(null);
          showToast(
            locale === 'vi-VN'
              ? `Đã ghi nhận thanh toán định kỳ -${formatMoney(scheduledAmount, locale)} từ ví!`
              : `Recurring payment -${formatMoney(scheduledAmount, locale)} was recorded successfully.`
          );
        } else {
          setErrorMessage(
            getLocalizedErrorMessage(res.error, locale, t, 'common.errorOccurred')
          );
        }
      } catch (err) {
        setErrorMessage(
          getLocalizedErrorMessage(err instanceof Error ? err.message : String(err), locale, t, 'common.errorOccurred')
        );
      } finally {
        isRecordingRecurringRef.current = false;
        setIsRecordingRecurring(false);
      }
      return;
    }

    const { valid, value: parsedFormAmount } = parseSafePositiveInteger(formAmount);
    if (!valid || parsedFormAmount <= 0) {
      setErrorMessage(locale === 'vi-VN' ? 'Vui lòng nhập số tiền hợp lệ (> 0 ₫)' : 'Please enter a valid amount (> 0 ₫)');
      return;
    }
    if (!formCategory) {
      setErrorMessage(locale === 'en-US' ? 'Please select a category.' : 'Vui lòng chọn danh mục.');
      return;
    }

    // Evidence is strictly required
    if (!formEvidence || formEvidence.length === 0) {
      setErrorMessage(
        locale === 'vi-VN'
          ? 'Chứng từ giao dịch (hình ảnh hoặc hóa đơn PDF) là bắt buộc khi ghi nhận chi thủ công.'
          : 'Transaction evidence (image or PDF receipt) is required for manual entry.'
      );
      return;
    }

    const occurredIso = formDateTime ? new Date(formDateTime).toISOString() : new Date().toISOString();

    const pin = await challengePinWithValue({
      actionName: locale === 'vi-VN' ? 'Ghi nhận chi tiêu' : 'Record Expense',
      amount: parsedFormAmount,
    });
    if (!pin) return;

    const res = await addExpense({
      amount: parsedFormAmount,
      category: formCategory,
      walletId: formWalletId,
      description: formDesc || (locale === 'vi-VN' ? 'Khoản chi tiêu' : 'Expense'),
      occurredAt: occurredIso,
      date: occurredIso,
      counterparty: formCounterparty,
      evidence: formEvidence,
      securityPin: pin,
    });

    if (res.success) {
      setIsAddExpenseOpen(false);
      setFormEvidence([]);
      showToast(
        locale === 'vi-VN'
          ? `Đã ghi nhận khoản chi -${formatMoney(parsedFormAmount, locale)} từ ví!`
          : `Expense -${formatMoney(parsedFormAmount, locale)} was recorded successfully.`
      );
    } else {
      setErrorMessage(
        getLocalizedErrorMessage(res.error, locale, t, 'common.errorOccurred')
      );
    }
  };

  // Handlers for Controlled Correction
  const handleOpenCorrection = (tx: Transaction) => {
    if (!tx.isManualEntry || (tx.type !== 'income' && tx.type !== 'expense')) {
      showToast(t('transactions.cannotEditTransfer'));
      return;
    }
    const allowance = typeof tx.correctionAllowance === 'number' ? tx.correctionAllowance : 1;
    const count = typeof tx.correctionCount === 'number' ? tx.correctionCount : (tx.corrections?.length || 0);
    const remaining = Math.max(0, allowance - count);

    if (remaining <= 0) {
      navigate(`/support?tab=cases&category=transaction_edit&tx=${tx.id}`);
      return;
    }

    setCorrectingTx(tx);
    setCorrAmount(Math.abs(tx.amount).toString());
    setCorrCategory(tx.category);
    setCorrWalletId(tx.walletId || defaultWalletId);
    setCorrDesc(tx.description || tx.title);
    setCorrCounterparty(tx.counterparty || '');
    setCorrDateTime(getLocalDateTimeString(tx.occurredAt || tx.date));
    setCorrReason('');
    setCorrAdditionalEvidence([]);
    setCorrErrorMessage(null);
  };

  const handleCloseCorrection = () => {
    setCorrectingTx(null);
    setCorrAdditionalEvidence([]);
    setCorrErrorMessage(null);
  };

  const handleSaveCorrection = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!correctingTx) return;

    const { valid, value: parsedAmount } = parseSafePositiveInteger(corrAmount);
    if (!valid || parsedAmount <= 0) {
      setCorrErrorMessage(
        locale === 'vi-VN' ? 'Vui lòng nhập số tiền hợp lệ (> 0 ₫)' : 'Please enter a valid amount (> 0 ₫)'
      );
      return;
    }
    if (!corrCategory) {
      setCorrErrorMessage(locale === 'en-US' ? 'Please select a category.' : 'Vui lòng chọn danh mục.');
      return;
    }
    if (!corrReason || !corrReason.trim()) {
      setCorrErrorMessage(
        locale === 'vi-VN' ? 'Lý do điều chỉnh giao dịch là bắt buộc.' : 'Correction reason is required.'
      );
      return;
    }

    const pin = await challengePinWithValue({
      actionName: locale === 'vi-VN' ? 'Điều chỉnh giao dịch' : 'Correct Transaction',
      amount: parsedAmount,
    });
    if (!pin) return;

    const occurredIso = corrDateTime ? new Date(corrDateTime).toISOString() : undefined;
    const res = await correctTransaction({
      id: correctingTx.id,
      amount: parsedAmount,
      walletId: corrWalletId,
      category: corrCategory,
      description: corrDesc || (correctingTx.type === 'income' ? (locale === 'vi-VN' ? 'Khoản thu nhập' : 'Income') : (locale === 'vi-VN' ? 'Khoản chi tiêu' : 'Expense')),
      counterparty: corrCounterparty,
      occurredAt: occurredIso,
      reason: corrReason.trim(),
      additionalEvidence: corrAdditionalEvidence,
      securityPin: pin,
    });

    if (res.success) {
      handleCloseCorrection();
      showToast(locale === 'vi-VN' ? 'Đã điều chỉnh giao dịch thành công!' : 'Transaction corrected successfully!');
    } else {
      setCorrErrorMessage(
        getLocalizedErrorMessage(res.error, locale, t, 'common.errorOccurred')
      );
    }
  };


  // Handlers for Edit
  const handleOpenEdit = (tx: Transaction) => {
    if (tx.canEdit === false || tx.type === 'transfer_out' || tx.type === 'transfer_in') {
      showToast(t('transactions.cannotEditTransfer'));
      return;
    }
    setEditingTx(tx);
    setFormAmount(Math.abs(tx.amount).toString());
    setFormCategory(tx.category);
    setFormWalletId(tx.walletId || defaultWalletId);
    setFormDesc(tx.description || tx.title);
    setFormCounterparty(tx.counterparty || '');
    setFormDate(tx.rawDate || tx.date.split('T')[0]);
    setErrorMessage(null);
  };

  const handleSaveEdit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!editingTx) return;

    const pin = await challengePinWithValue({
      actionName: locale === 'vi-VN' ? 'Cập nhật giao dịch' : 'Update Transaction',
      amount: Math.abs(editingTx.amount),
    });
    if (!pin) return;

    const res = await editTransaction(editingTx.id, {
      category: formCategory,
      description: formDesc,
      reason: locale === 'vi-VN' ? 'Cập nhật thông tin giao dịch' : 'Updated transaction details',
      securityPin: pin,
    });

    if (res.success) {
      setEditingTx(null);
      if (selectedTx?.id === editingTx.id) {
        setSelectedTx((prev) =>
          prev
            ? {
                ...prev,
                description: formDesc,
                title: formDesc || prev.title,
                category: formCategory,
              }
            : null
        );
      }
      showToast(
        locale === 'vi-VN'
          ? 'Đã cập nhật thông tin giao dịch thành công!'
          : 'Transaction updated successfully!'
      );
    } else {
      setErrorMessage(
        getLocalizedErrorMessage(res.error, locale, t, 'common.errorOccurred')
      );
    }
  };

  return (
    <div className="space-y-6 max-w-7xl mx-auto animate-in fade-in-50 duration-200">
      {/* Toast */}
      {toastMessage && (
        <div className="fixed top-6 right-6 z-50 flex items-center gap-2 px-4 py-3 rounded-2xl bg-slate-900 text-white shadow-xl border border-emerald-500/30 text-xs font-semibold animate-in slide-in-from-top-3">
          <CheckCircle2 className="h-4 w-4 text-emerald-400" />
          <span>{toastMessage}</span>
        </div>
      )}

      {/* Header & Primary Actions */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <h2 className="text-xl sm:text-2xl font-bold tracking-tight text-slate-900 dark:text-white">
            {t('transactions.pageTitle')}
          </h2>
          <p className="text-xs sm:text-sm text-slate-500 dark:text-slate-400 mt-0.5">
            {t('transactions.pageSubtitle')}
          </p>
        </div>

        <div className="flex flex-wrap items-center gap-2.5">
          <Button
            size="sm"
            variant="outline"
            onClick={() => setIsCategoryModalOpen(true)}
            className="gap-1.5 text-slate-700 dark:text-slate-200 border-slate-200 dark:border-slate-700 hover:bg-slate-100 dark:hover:bg-slate-800 font-semibold cursor-pointer shadow-2xs"
          >
            <Tag className="h-4 w-4 text-emerald-500" />
            <span>{t('transactions.manageCategories')}</span>
          </Button>

          <Button
            size="sm"
            onClick={handleOpenAddIncome}
            className="gap-1.5 bg-emerald-600 hover:bg-emerald-700 text-white font-semibold cursor-pointer shadow-2xs"
          >
            <Plus className="h-4 w-4" />
            <span>{t('transactions.incomeAction')}</span>
          </Button>

          <Button
            size="sm"
            variant="outline"
            onClick={handleOpenAddExpense}
            className="gap-1.5 text-rose-600 dark:text-rose-400 border-rose-200 dark:border-rose-900/50 hover:bg-rose-50 dark:hover:bg-rose-950/30 font-semibold cursor-pointer shadow-2xs"
          >
            <Minus className="h-4 w-4" />
            <span>{t('transactions.expenseAction')}</span>
          </Button>
        </div>
      </div>

      {/* Primary Sub-navigation Tabs: History | Recurring | Rules */}
      <div className="flex items-center gap-2 p-1.5 bg-slate-100 dark:bg-slate-800/80 rounded-2xl w-fit">
        <button
          type="button"
          onClick={() => handleTabChange('history')}
          className={cn(
            'flex items-center gap-2 px-4 py-2 text-xs sm:text-sm font-bold rounded-xl transition-all cursor-pointer',
            activeMainTab === 'history'
              ? 'bg-white dark:bg-slate-900 text-slate-900 dark:text-white shadow-xs'
              : 'text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white'
          )}
        >
          <History className="h-4 w-4" />
          <span>{t('transactions.historyTab')}</span>
          <span className="text-[10px] px-1.5 py-0.5 rounded-full bg-slate-100 dark:bg-slate-800 text-slate-500">
            {transactions.length}
          </span>
        </button>

        <button
          type="button"
          onClick={() => handleTabChange('recurring')}
          className={cn(
            'flex items-center gap-2 px-4 py-2 text-xs sm:text-sm font-bold rounded-xl transition-all cursor-pointer',
            activeMainTab === 'recurring'
              ? 'bg-white dark:bg-slate-900 text-slate-900 dark:text-white shadow-xs'
              : 'text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white'
          )}
        >
          <RefreshCw className="h-4 w-4" />
          <span>{t('transactions.recurringTab')}</span>
          <ActionBadge count={dueRecurringCount} />
          <span className="text-[10px] px-1.5 py-0.5 rounded-full bg-slate-100 dark:bg-slate-800 text-slate-500">
            {recurringTransactions.length}
          </span>
        </button>

        <button
          type="button"
          onClick={() => handleTabChange('rules')}
          className={cn(
            'flex items-center gap-2 px-4 py-2 text-xs sm:text-sm font-bold rounded-xl transition-all cursor-pointer',
            activeMainTab === 'rules'
              ? 'bg-white dark:bg-slate-900 text-slate-900 dark:text-white shadow-xs'
              : 'text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white'
          )}
        >
          <Sliders className="h-4 w-4" />
          <span>{t('transactions.rulesTab')}</span>
          <span className="text-[10px] px-1.5 py-0.5 rounded-full bg-slate-100 dark:bg-slate-800 text-slate-500">
            {rules.length}
          </span>
        </button>
      </div>

      {/* Conditionally Render Recurring or Rules Tab */}
      {activeMainTab === 'recurring' && (
        <RecurringTransactionsTab
          focusRecurringId={searchParams.get('focus')}
          onRecordPayment={handleRecordRecurringPayment}
        />
      )}
      {activeMainTab === 'rules' && <TransactionRulesTab />}

      {/* HISTORY VIEW (Default) */}
      {activeMainTab === 'history' && (
        <div className="space-y-6">
      {/* Top Summary Cards */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        {/* Thu nhập tháng này */}
        <div className="p-5 rounded-3xl bg-white dark:bg-slate-900 border border-slate-100 dark:border-slate-800 shadow-sm flex flex-col justify-between space-y-2">
          <div className="flex items-center justify-between">
            <span className="text-xs font-semibold text-slate-500 dark:text-slate-400">
              {t('transactions.thisMonthIncome')}
            </span>
            <div className="h-8 w-8 rounded-xl bg-emerald-100 dark:bg-emerald-950/60 text-emerald-600 dark:text-emerald-400 flex items-center justify-center">
              <TrendingUp className="h-4 w-4" />
            </div>
          </div>
          <div>
            <p className="text-2xl font-bold text-slate-900 dark:text-white tabular-nums">
              {isHidden ? '••••••••' : formatMoney(summaryStats.income, locale)}
            </p>
            <span className="text-xs text-emerald-600 dark:text-emerald-400 font-medium">
              {locale === 'vi-VN' ? 'Đã ghi nhận toàn bộ dòng tiền vào' : 'All recorded cash inflows'}
            </span>
          </div>
        </div>

        {/* Chi tiêu tháng này */}
        <div className="p-5 rounded-3xl bg-white dark:bg-slate-900 border border-slate-100 dark:border-slate-800 shadow-sm flex flex-col justify-between space-y-2">
          <div className="flex items-center justify-between">
            <span className="text-xs font-semibold text-slate-500 dark:text-slate-400">
              {t('transactions.thisMonthExpense')}
            </span>
            <div className="h-8 w-8 rounded-xl bg-rose-100 dark:bg-rose-950/60 text-rose-600 dark:text-rose-400 flex items-center justify-center">
              <TrendingDown className="h-4 w-4" />
            </div>
          </div>
          <div>
            <p className="text-2xl font-bold text-slate-900 dark:text-white tabular-nums">
              {isHidden ? '••••••••' : formatMoney(summaryStats.expense, locale)}
            </p>
            <span className="text-xs text-slate-600 dark:text-slate-400 font-medium">
              {locale === 'vi-VN' ? 'Bao gồm chi tiêu & chuyển tiền' : 'Includes spending & transfers'}
            </span>
          </div>
        </div>

        {/* Chênh lệch */}
        <div className="p-5 rounded-3xl bg-white dark:bg-slate-900 border border-slate-100 dark:border-slate-800 shadow-sm flex flex-col justify-between space-y-2">
          <div className="flex items-center justify-between">
            <span className="text-xs font-semibold text-slate-500 dark:text-slate-400">
              {t('transactions.difference')}
            </span>
            <div className="h-8 w-8 rounded-xl bg-teal-100 dark:bg-teal-950/60 text-teal-600 dark:text-teal-400 flex items-center justify-center">
              <ShieldCheck className="h-4 w-4" />
            </div>
          </div>
          <div>
            <p className={cn('text-2xl font-bold tabular-nums', summaryStats.difference >= 0 ? 'text-emerald-600 dark:text-emerald-400' : 'text-rose-600 dark:text-rose-400')}>
              {isHidden ? '••••••••' : `${summaryStats.difference >= 0 ? '+' : ''}${formatMoney(summaryStats.difference, locale)}`}
            </p>
            <span className="text-xs text-slate-600 dark:text-slate-400 font-medium">
              {locale === 'vi-VN' ? 'Thặng dư dòng tiền tháng' : 'Net monthly surplus'}
            </span>
          </div>
        </div>

        {/* Tổng giao dịch */}
        <div className="p-5 rounded-3xl bg-white dark:bg-slate-900 border border-slate-100 dark:border-slate-800 shadow-sm flex flex-col justify-between space-y-2">
          <div className="flex items-center justify-between">
            <span className="text-xs font-semibold text-slate-500 dark:text-slate-400">
              {t('transactions.totalCount')}
            </span>
            <div className="h-8 w-8 rounded-xl bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-300 flex items-center justify-center">
              <Clock className="h-4 w-4" />
            </div>
          </div>
          <div>
            <p className="text-2xl font-bold text-slate-900 dark:text-white tabular-nums">
              {summaryStats.totalCount}
            </p>
            <span className="text-xs text-slate-600 dark:text-slate-400 font-medium">
              {locale === 'vi-VN' ? 'Giao dịch đã đồng bộ trong hệ thống' : 'Synchronized transactions'}
            </span>
          </div>
        </div>
      </div>

      {/* Practical Filter & Search Toolbar */}
      <div className="bg-white dark:bg-slate-900 rounded-3xl border border-slate-100 dark:border-slate-800 p-4 space-y-3.5 shadow-sm">
        {/* Search input and Tab filters */}
        <div className="flex flex-col lg:flex-row lg:items-center justify-between gap-3">
          {/* Tabs */}
          <div className="flex flex-wrap items-center gap-1.5 bg-slate-100 dark:bg-slate-800/80 p-1 rounded-2xl">
            <button
              type="button"
              onClick={() => setTypeFilter('all')}
              className={cn(
                'px-3.5 py-1.5 text-xs font-bold rounded-xl transition-all cursor-pointer',
                typeFilter === 'all'
                  ? 'bg-white dark:bg-slate-900 text-slate-900 dark:text-white shadow-2xs'
                  : 'text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white'
              )}
            >
              {t('transactions.all')}
            </button>
            <button
              type="button"
              onClick={() => setTypeFilter('income')}
              className={cn(
                'px-3.5 py-1.5 text-xs font-bold rounded-xl transition-all cursor-pointer',
                typeFilter === 'income'
                  ? 'bg-white dark:bg-slate-900 text-emerald-600 dark:text-emerald-400 shadow-2xs'
                  : 'text-slate-600 dark:text-slate-400 hover:text-emerald-600'
              )}
            >
              {t('transactions.incomeTab')}
            </button>
            <button
              type="button"
              onClick={() => setTypeFilter('expense')}
              className={cn(
                'px-3.5 py-1.5 text-xs font-bold rounded-xl transition-all cursor-pointer',
                typeFilter === 'expense'
                  ? 'bg-white dark:bg-slate-900 text-rose-600 dark:text-rose-400 shadow-2xs'
                  : 'text-slate-600 dark:text-slate-400 hover:text-rose-600'
              )}
            >
              {t('transactions.expenseTab')}
            </button>
            <button
              type="button"
              onClick={() => setTypeFilter('transfer_out')}
              className={cn(
                'px-3.5 py-1.5 text-xs font-bold rounded-xl transition-all cursor-pointer',
                typeFilter === 'transfer_out'
                  ? 'bg-white dark:bg-slate-900 text-teal-600 dark:text-teal-400 shadow-2xs'
                  : 'text-slate-600 dark:text-slate-400 hover:text-teal-600'
              )}
            >
              {t('transactions.transferTab')}
            </button>
            <button
              type="button"
              onClick={() => setTypeFilter('transfer_in')}
              className={cn(
                'px-3.5 py-1.5 text-xs font-bold rounded-xl transition-all cursor-pointer',
                typeFilter === 'transfer_in'
                  ? 'bg-white dark:bg-slate-900 text-emerald-600 dark:text-emerald-400 shadow-2xs'
                  : 'text-slate-600 dark:text-slate-400 hover:text-emerald-600'
              )}
            >
              {t('transactions.receiveTab')}
            </button>
            <button
              type="button"
              id="btn-filter-bank-topup"
              onClick={() => setTypeFilter('bank_top_up')}
              className={cn(
                'px-3.5 py-1.5 text-xs font-bold rounded-xl transition-all cursor-pointer flex items-center gap-1.5',
                typeFilter === 'bank_top_up'
                  ? 'bg-white dark:bg-slate-900 text-emerald-600 dark:text-emerald-400 shadow-2xs'
                  : 'text-slate-600 dark:text-slate-400 hover:text-emerald-600'
              )}
            >
              <span>{locale === 'vi-VN' ? 'Nạp tiền (SePay)' : 'Top-up (SePay)'}</span>
            </button>
          </div>

          {/* Search bar */}
          <div className="relative flex-1 max-w-md">
            <Search className="absolute left-3.5 top-1/2 -translate-y-1/2 h-4 w-4 text-slate-400" />
            <input
              type="text"
              placeholder={t('transactions.searchPlaceholder')}
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
              className="w-full pl-10 pr-4 py-2 text-xs rounded-2xl border border-slate-200 dark:border-slate-700 bg-slate-50/60 dark:bg-slate-800/60 text-slate-900 dark:text-white placeholder:text-slate-500 dark:placeholder:text-slate-400 focus:outline-none focus:ring-1 focus:ring-emerald-500"
            />
            {searchTerm && (
              <button
                type="button"
                onClick={() => setSearchTerm('')}
                className="absolute right-3 top-1/2 -translate-y-1/2 text-slate-400 hover:text-slate-600"
              >
                <X className="h-3.5 w-3.5" />
              </button>
            )}
          </div>
        </div>

        {/* Dropdown Filters row */}
        <div className="flex flex-wrap items-center justify-between gap-3 pt-2 border-t border-slate-100 dark:border-slate-800 text-xs">
          <div className="flex flex-wrap items-center gap-2.5">
            {/* Time filter */}
            <Select
              value={timeFilter}
              onChange={(val) => setTimeFilter(val as any)}
              size="sm"
              variant="filter"
              icon={<Calendar className="h-3.5 w-3.5 text-slate-400" />}
            >
              <option value="all">{t('transactions.allTimes')}</option>
              <option value="today">{t('transactions.today')}</option>
              <option value="week">{t('transactions.thisWeek')}</option>
              <option value="month">{t('transactions.thisMonth')}</option>
            </Select>

            {/* Category filter */}
            <Select
              value={categoryFilter}
              onChange={(val) => setCategoryFilter(val)}
              size="sm"
              variant="filter"
              icon={<Tag className="h-3.5 w-3.5 text-slate-400" />}
            >
              <option value="all">{t('transactions.allCategories')}</option>
              {categories.map((c) => (
                <option key={c.id} value={c.key}>
                  {c.key.startsWith('categories.') ? t(c.key) : (c.isSystem ? getLocalizedCategoryName(c.name, t, categories) : c.name)}
                </option>
              ))}
            </Select>

            {/* Wallet filter */}
            <Select
              value={walletFilter}
              onChange={(val) => setWalletFilter(val)}
              size="sm"
              variant="filter"
              icon={<Wallet className="h-3.5 w-3.5 text-slate-400" />}
            >
              <option value="all">{t('transactions.allWallets')}</option>
              {wallets.map((w) => (
                <option key={w.id} value={w.id}>
                  {getLocalizedWalletName(w.id, w.name, locale)}
                </option>
              ))}
            </Select>

            {/* Amount filter */}
            <Select
              value={amountFilter}
              onChange={(val) => setAmountFilter(val as any)}
              size="sm"
              variant="filter"
            >
              <option value="all">{t('transactions.allAmounts')}</option>
              <option value="under100k">{t('transactions.under100k')}</option>
              <option value="100kTo1m">{t('transactions.from100kTo1m')}</option>
              <option value="over1m">{t('transactions.over1m')}</option>
            </Select>
          </div>

          {/* Reset button */}
          {isFiltered && (
            <button
              type="button"
              onClick={resetFilters}
              className="flex items-center gap-1 text-xs font-bold text-rose-600 dark:text-rose-400 hover:underline cursor-pointer"
            >
              <X className="h-3.5 w-3.5" />
              <span>{t('transactions.clearFilters')}</span>
            </button>
          )}
        </div>
      </div>

      {/* Transaction History Desktop List / Table Hybrid */}
      <div className="bg-white dark:bg-slate-900 rounded-3xl border border-slate-100 dark:border-slate-800 shadow-sm overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-left text-xs">
            <thead className="bg-slate-100/70 dark:bg-slate-800/60 text-slate-700 dark:text-slate-300 uppercase tracking-wider font-semibold border-b border-slate-200/80 dark:border-slate-800 text-xs">
              <tr>
                <th className="py-3.5 px-5">{t('transactions.colTx')}</th>
                <th className="py-3.5 px-4">{t('transactions.colCategory')}</th>
                <th className="py-3.5 px-4">{t('transactions.colWallet')}</th>
                <th className="py-3.5 px-4">{t('transactions.colDate')}</th>
                <th className="py-3.5 px-5 text-right">{t('transactions.colAmount')}</th>
                <th className="py-3.5 px-4 text-center">{t('transactions.colStatus')}</th>
                <th className="py-3.5 px-4 text-center">{t('transactions.colActions')}</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100 dark:divide-slate-800/60">
              {isLoading && filteredTransactions.length === 0 ? (
                Array.from({ length: 6 }).map((_, idx) => (
                  <tr key={`tx-skel-${idx}`} className="animate-pulse">
                    <td className="py-4 px-5">
                      <div className="flex items-center gap-3">
                        <Skeleton className="h-9 w-9 rounded-2xl shrink-0" />
                        <div className="space-y-1.5 flex-1">
                          <Skeleton className="h-4 w-32" />
                          <Skeleton className="h-3 w-20" />
                        </div>
                      </div>
                    </td>
                    <td className="py-4 px-4">
                      <Skeleton className="h-5 w-24 rounded-full" />
                    </td>
                    <td className="py-4 px-4">
                      <Skeleton className="h-4 w-24" />
                    </td>
                    <td className="py-4 px-4">
                      <Skeleton className="h-4 w-20" />
                    </td>
                    <td className="py-4 px-5 text-right">
                      <Skeleton className="h-4 w-24 ml-auto" />
                    </td>
                    <td className="py-4 px-4 text-center">
                      <Skeleton className="h-5 w-16 mx-auto rounded-full" />
                    </td>
                    <td className="py-4 px-4 text-center">
                      <Skeleton className="h-7 w-12 mx-auto rounded-lg" />
                    </td>
                  </tr>
                ))
              ) : filteredTransactions.length === 0 ? (
                <tr>
                  <td colSpan={7} className="py-16 text-center text-slate-500 dark:text-slate-400">
                    <div className="max-w-xs mx-auto space-y-3">
                      <div className="h-12 w-12 rounded-2xl bg-slate-100 dark:bg-slate-800 flex items-center justify-center mx-auto text-slate-400">
                        <Search className="h-6 w-6" />
                      </div>
                      <div className="space-y-1">
                        <p className="text-sm font-bold text-slate-700 dark:text-slate-200">
                          {isFiltered ? t('transactions.noFilteredResults') : t('common.noTransactions')}
                        </p>
                        <p className="text-xs text-slate-600 dark:text-slate-400 leading-relaxed">
                          {isFiltered
                            ? t('transactions.noFilteredResultsDesc')
                            : locale === 'vi-VN'
                            ? 'Bắt đầu ghi nhận khoản thu hoặc chi tiêu đầu tiên của bạn.'
                            : 'Start by recording your first income or expense.'}
                        </p>
                      </div>
                      {isFiltered ? (
                        <Button
                          size="sm"
                          variant="outline"
                          onClick={resetFilters}
                          className="mt-2 text-xs font-semibold gap-1.5"
                        >
                          <X className="h-3.5 w-3.5" />
                          <span>{t('transactions.clearFilters')}</span>
                        </Button>
                      ) : (
                        <div className="flex items-center justify-center gap-2 pt-1">
                          <Button
                            size="sm"
                            onClick={handleOpenAddIncome}
                            className="text-xs bg-emerald-600 hover:bg-emerald-700 text-white font-semibold gap-1"
                          >
                            <Plus className="h-3.5 w-3.5" />
                            <span>{t('transactions.incomeAction')}</span>
                          </Button>
                          <Button
                            size="sm"
                            variant="outline"
                            onClick={handleOpenAddExpense}
                            className="text-xs font-semibold gap-1"
                          >
                            <Minus className="h-3.5 w-3.5" />
                            <span>{t('transactions.expenseAction')}</span>
                          </Button>
                        </div>
                      )}
                    </div>
                  </td>
                </tr>
              ) : (
                filteredTransactions.slice(0, visibleCount).map((tx) => {
                  const isPositive = tx.amount > 0;
                  const isTransfer = tx.type === 'transfer_out' || tx.type === 'transfer_in';
                  const formattedAmount = isHidden
                    ? '••••••••'
                    : formatMoney(tx.amount, locale, {
                        showSign: true,
                        forceSign: true,
                      });

                  return (
                    <tr
                      key={tx.id}
                      onClick={() => setSelectedTx(tx)}
                      className="hover:bg-slate-50/60 dark:hover:bg-slate-800/40 cursor-pointer transition-colors group"
                    >
                      {/* Giao dịch */}
                      <td className="py-3.5 px-5">
                        <div className="flex items-center gap-3">
                          <div className="h-9 w-9 rounded-2xl bg-slate-100 dark:bg-slate-800 flex items-center justify-center shrink-0">
                            {getCategoryIcon(tx.category, tx.type)}
                          </div>
                          <div>
                            <div className="flex items-center gap-2">
                              <p className="font-bold text-slate-900 dark:text-white group-hover:text-emerald-600 transition-colors">
                                {getLocalizedTxTitle(tx, locale)}
                              </p>
                              {tx.type === 'bank_top_up' && (
                                <span className="px-1.5 py-0.2 rounded text-[10px] font-bold bg-teal-100 dark:bg-teal-950/60 text-teal-700 dark:text-teal-300">
                                  {locale === 'vi-VN' ? 'Nạp tiền qua SePay' : 'SePay Top-up'}
                                </span>
                              )}
                            </div>
                            {(tx.description || tx.descriptionEn) && (
                              <p className="text-[11px] text-slate-400 line-clamp-1">
                                {getLocalizedTxDesc(tx, locale)}
                              </p>
                            )}
                          </div>
                        </div>
                      </td>

                      {/* Danh mục */}
                      <td className="py-3.5 px-4">
                        <span className="px-2.5 py-1 rounded-full bg-slate-100 dark:bg-slate-800 text-slate-700 dark:text-slate-300 text-[11px] font-semibold">
                          {getCategoryName(tx.category)}
                        </span>
                      </td>

                      {/* Ví */}
                      <td className="py-3.5 px-4 text-slate-600 dark:text-slate-300 font-medium">
                        {getLocalizedWalletName(tx.walletId, tx.walletName, locale)}
                      </td>

                      {/* Ngày */}
                      <td className="py-3.5 px-4 text-slate-500 dark:text-slate-400 tabular-nums">
                        {formatTransactionTime(tx.date, locale)}
                      </td>

                      {/* Số tiền */}
                      <td className="py-3.5 px-5 text-right font-bold tabular-nums">
                        <span
                          className={
                            isPositive
                              ? 'text-emerald-600 dark:text-emerald-400 text-sm'
                              : 'text-slate-900 dark:text-slate-100 text-sm'
                          }
                        >
                          {formattedAmount}
                        </span>
                      </td>

                      {/* Trạng thái */}
                      <td className="py-3.5 px-4 text-center">
                        {tx.status === 'completed' ? (
                          <span className="px-2.5 py-0.5 rounded-full text-[10px] font-bold bg-emerald-100 dark:bg-emerald-950/60 text-emerald-700 dark:text-emerald-300">
                            {t('transactions.statusCompleted')}
                          </span>
                        ) : tx.status === 'pending' ? (
                          <span className="px-2.5 py-0.5 rounded-full text-[10px] font-bold bg-amber-100 dark:bg-amber-950/60 text-amber-700 dark:text-amber-300">
                            {locale === 'vi-VN' ? 'Đang xử lý' : 'Pending'}
                          </span>
                        ) : tx.status === 'cancelled' || tx.status === 'failed' ? (
                          <span className="px-2.5 py-0.5 rounded-full text-[10px] font-bold bg-rose-100 dark:bg-rose-950/60 text-rose-700 dark:text-rose-300">
                            {locale === 'vi-VN' ? 'Đã hủy' : 'Cancelled'}
                          </span>
                        ) : (
                          <span className="px-2.5 py-0.5 rounded-full text-[10px] font-bold bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-400">
                            {locale === 'vi-VN' ? 'Không xác định' : 'Unknown'}
                          </span>
                        )}
                      </td>

                      {/* Action Menu */}
                      <td
                        className="py-3.5 px-4 text-center"
                        onClick={(e) => e.stopPropagation()}
                      >
                        <div className="flex items-center justify-center gap-1">
                          <button
                            type="button"
                            onClick={() => setSelectedTx(tx)}
                            title={t('common.viewDetails')}
                            className="p-1.5 rounded-lg text-slate-400 hover:text-slate-700 dark:hover:text-slate-200 hover:bg-slate-100 dark:hover:bg-slate-800 transition-colors"
                          >
                            <Eye className="h-4 w-4" />
                          </button>
                          {(() => {
                            const isManual = tx.isManualEntry && (tx.type === 'income' || tx.type === 'expense');
                            if (isManual) {
                              const allowance = typeof tx.correctionAllowance === 'number' ? tx.correctionAllowance : 1;
                              const count = typeof tx.correctionCount === 'number' ? tx.correctionCount : (tx.corrections?.length || 0);
                              const remaining = allowance - count;

                              if (remaining <= 0) {
                                return (
                                  <button
                                    type="button"
                                    onClick={() => navigate(`/support?tab=cases&category=transaction_edit&tx=${tx.id}`)}
                                    title={locale === 'vi-VN' ? 'Yêu cầu thêm lượt sửa' : 'Request extra correction'}
                                    className="p-1.5 rounded-lg text-amber-600 hover:text-amber-700 hover:bg-amber-50 dark:hover:bg-amber-950/30 transition-colors cursor-pointer"
                                  >
                                    <LifeBuoy className="h-4 w-4" />
                                  </button>
                                );
                              }

                              return (
                                <button
                                  type="button"
                                  onClick={() => handleOpenCorrection(tx)}
                                  title={locale === 'vi-VN' ? `Chỉnh sửa · còn ${remaining} lượt` : `Edit · ${remaining} left`}
                                  className="p-1.5 rounded-lg text-slate-400 hover:text-emerald-600 hover:bg-emerald-50 dark:hover:bg-emerald-950/30 transition-colors cursor-pointer"
                                >
                                  <Edit2 className="h-3.5 w-3.5" />
                                </button>
                              );
                            }

                            // Non-manual / system transactions: keep existing restrictions unchanged
                            if (tx.canEdit !== false && !isTransfer) {
                              return (
                                <button
                                  type="button"
                                  onClick={() => handleOpenEdit(tx)}
                                  title={t('common.edit')}
                                  className="p-1.5 rounded-lg text-slate-400 hover:text-emerald-600 hover:bg-emerald-50 dark:hover:bg-emerald-950/30 transition-colors cursor-pointer"
                                >
                                  <Edit2 className="h-3.5 w-3.5" />
                                </button>
                              );
                            }

                            return null;
                          })()}
                        </div>
                      </td>
                    </tr>
                  );
                })
              )}
            </tbody>
          </table>
        </div>

        {/* Load More & Pagination Bar */}
        {filteredTransactions.length > 0 && (
          <div className="p-4 border-t border-slate-100 dark:border-slate-800 bg-slate-50/40 dark:bg-slate-800/30 flex flex-col sm:flex-row items-center justify-between gap-3 text-xs">
            <span className="text-slate-400 font-medium">
              {t('transactions.showingCount', {
                count: Math.min(visibleCount, filteredTransactions.length),
                total: filteredTransactions.length,
              })}
            </span>

            {visibleCount < filteredTransactions.length ? (
              <Button
                size="sm"
                variant="outline"
                onClick={() => setVisibleCount((prev) => prev + PAGE_SIZE)}
                className="gap-1.5 font-bold shadow-2xs cursor-pointer hover:bg-white dark:hover:bg-slate-800"
              >
                <span>
                  {t('transactions.loadMore')} (
                  {filteredTransactions.length - visibleCount}{' '}
                  {locale === 'vi-VN' ? 'còn lại' : 'remaining'}
                  )
                </span>
                <ChevronDown className="h-3.5 w-3.5 text-slate-400" />
              </Button>
            ) : filteredTransactions.length > PAGE_SIZE ? (
              <span className="text-[11px] font-semibold text-slate-400 flex items-center gap-1">
                <CheckCircle2 className="h-3.5 w-3.5 text-emerald-500" />
                <span>{t('transactions.noMoreTransactions')}</span>
              </span>
            ) : null}
          </div>
        )}
      </div>
      </div>
      )}

      {/* 1. TRANSACTION DETAILS DRAWER / MODAL */}
      {selectedTx && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-xs animate-in fade-in duration-150">
          <div className="w-full max-w-xl max-h-[90vh] overflow-y-auto bg-white dark:bg-slate-900 rounded-3xl p-6 sm:p-7 shadow-2xl border border-slate-100 dark:border-slate-800 space-y-5 animate-in zoom-in-95">
            <div className="flex items-center justify-between">
              <h3 className="text-base font-bold text-slate-900 dark:text-white">
                {t('transactions.detailsTitle')}
              </h3>
              <button
                type="button"
                onClick={() => setSelectedTx(null)}
                className="p-1.5 rounded-xl text-slate-400 hover:bg-slate-100 dark:hover:bg-slate-800 cursor-pointer"
              >
                <X className="h-4 w-4" />
              </button>
            </div>

            {/* Header with Amount */}
            <div className="text-center py-4 bg-slate-50 dark:bg-slate-800/40 rounded-2xl border border-slate-100 dark:border-slate-800 space-y-1">
              <span className="text-xs font-medium text-slate-400">
                {getLocalizedTxTitle(selectedTx, locale)}
              </span>
              <p
                className={cn(
                  'text-2xl font-bold tabular-nums',
                  selectedTx.amount > 0 ? 'text-emerald-600 dark:text-emerald-400' : 'text-slate-900 dark:text-white'
                )}
              >
                {isHidden
                  ? '••••••••'
                  : formatMoney(selectedTx.amount, locale, {
                      showSign: true,
                      forceSign: true,
                    })}
              </p>
              {selectedTx.status === 'completed' ? (
                <span className="inline-block px-2.5 py-0.5 rounded-full text-[10px] font-bold bg-emerald-100 text-emerald-700 dark:bg-emerald-950 dark:text-emerald-300 mt-1">
                  {t('transactions.statusCompleted')}
                </span>
              ) : selectedTx.status === 'pending' ? (
                <span className="inline-block px-2.5 py-0.5 rounded-full text-[10px] font-bold bg-amber-100 text-amber-700 dark:bg-amber-950 dark:text-amber-300 mt-1">
                  {locale === 'vi-VN' ? 'Đang xử lý' : 'Pending'}
                </span>
              ) : selectedTx.status === 'cancelled' || selectedTx.status === 'failed' ? (
                <span className="inline-block px-2.5 py-0.5 rounded-full text-[10px] font-bold bg-rose-100 text-rose-700 dark:bg-rose-950 dark:text-rose-300 mt-1">
                  {locale === 'vi-VN' ? 'Đã hủy' : 'Cancelled'}
                </span>
              ) : (
                <span className="inline-block px-2.5 py-0.5 rounded-full text-[10px] font-bold bg-slate-100 text-slate-600 dark:bg-slate-800 dark:text-slate-400 mt-1">
                  {locale === 'vi-VN' ? 'Không xác định' : 'Unknown'}
                </span>
              )}
            </div>

            {/* Notice for transactions that cannot be edited or are system generated */}
            {!selectedTx.isManualEntry && (selectedTx.canEdit === false || selectedTx.type === 'transfer_out' || selectedTx.type === 'transfer_in' || selectedTx.type === 'goal_contribution' || selectedTx.type === 'goal_withdrawal') && (
              <div className="p-3 rounded-2xl bg-slate-100 dark:bg-slate-800/80 border border-slate-200 dark:border-slate-700/60 text-xs text-slate-600 dark:text-slate-400 flex items-start gap-2">
                <Info className="h-4 w-4 shrink-0 text-slate-500 mt-0.5" />
                <span>
                  {selectedTx.type === 'goal_contribution' || selectedTx.type === 'goal_withdrawal'
                    ? (locale === 'vi-VN' ? 'Giao dịch mục tiêu tích lũy được đồng bộ tự động và không thể chỉnh sửa thủ công.' : 'Goal allocation transactions are synced automatically and cannot be manually edited.')
                    : (locale === 'vi-VN' ? 'Giao dịch hệ thống / đối tác được bảo vệ tính toàn vẹn và không thể chỉnh sửa trực tiếp.' : 'System/partner transactions are protected for audit integrity and cannot be edited directly.')}
                </span>
              </div>
            )}

            {/* Policy Info for Manual Entries */}
            {selectedTx.isManualEntry && (selectedTx.type === 'income' || selectedTx.type === 'expense') && (
              <div className="p-3 rounded-2xl bg-slate-50 dark:bg-slate-800/50 border border-slate-200 dark:border-slate-700 text-xs text-slate-600 dark:text-slate-400 flex items-start gap-2">
                <ShieldCheck className="h-4 w-4 shrink-0 text-emerald-500 mt-0.5" />
                <div className="space-y-0.5">
                  <span className="font-semibold text-slate-800 dark:text-slate-200">
                    {locale === 'vi-VN' ? 'Chính sách chỉnh sửa an toàn' : 'Controlled Correction Policy'}
                  </span>
                  <p className="text-[11px] leading-relaxed">
                    {locale === 'vi-VN'
                      ? 'Mọi thay đổi thông tin giao dịch sẽ được ghi nhận vào lịch sử điều chỉnh kèm lý do và chứng từ bổ sung.'
                      : 'All changes are audited and recorded in the immutable correction history with reasons and optional evidence.'}
                  </p>
                </div>
              </div>
            )}

            {/* Info Grid */}
            <div className="space-y-2 text-xs">
              <div className="flex justify-between py-1.5 border-b border-slate-100 dark:border-slate-800">
                <span className="text-slate-400">{t('transactions.date')}:</span>
                <span className="font-semibold text-slate-800 dark:text-slate-200">
                  {formatTransactionTime(selectedTx.date || selectedTx.createdAt || new Date().toISOString(), locale)}
                </span>
              </div>

              <div className="flex justify-between py-1.5 border-b border-slate-100 dark:border-slate-800">
                <span className="text-slate-400">{locale === 'vi-VN' ? 'Thời gian phát sinh' : 'Occurred At'}:</span>
                <span className="font-semibold text-slate-800 dark:text-slate-200">
                  {formatExactTransactionTime(selectedTx.occurredAt || selectedTx.date || selectedTx.createdAt || new Date().toISOString(), locale)}
                </span>
              </div>

              <div className="flex justify-between py-1.5 border-b border-slate-100 dark:border-slate-800">
                <span className="text-slate-400">{t('transactions.id')}:</span>
                <div className="flex items-center gap-1">
                  <span className="font-mono text-slate-800 dark:text-slate-200">{selectedTx.id}</span>
                  <button
                    type="button"
                    onClick={() => handleCopyId(selectedTx.id)}
                    title={locale === 'vi-VN' ? 'Sao chép mã giao dịch' : 'Copy transaction ID'}
                    className="p-1 rounded-md text-slate-400 hover:text-slate-600 hover:bg-slate-100 dark:hover:bg-slate-800 cursor-pointer"
                  >
                    {copiedId === selectedTx.id ? <Check className="h-3.5 w-3.5 text-emerald-500" /> : <Copy className="h-3.5 w-3.5" />}
                  </button>
                </div>
              </div>

              <div className="flex justify-between py-1.5 border-b border-slate-100 dark:border-slate-800">
                <span className="text-slate-400">{t('transactions.type')}:</span>
                <span className="font-semibold text-slate-800 dark:text-slate-200">
                  {getTransactionTypeLabel(selectedTx.type)}
                </span>
              </div>

              <div className="flex justify-between py-1.5 border-b border-slate-100 dark:border-slate-800">
                <span className="text-slate-400">{t('transactions.category')}:</span>
                <span className="font-semibold text-slate-800 dark:text-slate-200">
                  {getCategoryName(selectedTx.category)}
                </span>
              </div>

              <div className="flex justify-between py-1.5 border-b border-slate-100 dark:border-slate-800">
                <span className="text-slate-400">{t('transactions.sourceFund')}:</span>
                <span className="font-semibold text-slate-800 dark:text-slate-200">
                  {getLocalizedWalletName(selectedTx.walletId, selectedTx.walletName, locale)}
                </span>
              </div>

              {(selectedTx.counterparty || selectedTx.counterpartyEn) && (
                <div className="flex justify-between py-1.5 border-b border-slate-100 dark:border-slate-800">
                  <span className="text-slate-400">{t('transactions.recipient')}:</span>
                  <span className="font-semibold text-slate-800 dark:text-slate-200">
                    {getLocalizedTxCounterparty(selectedTx, locale)}
                  </span>
                </div>
              )}

              {/* Exact Timestamps: Occurred vs Created */}
              <div className="flex justify-between py-1.5 border-b border-slate-100 dark:border-slate-800">
                <span className="text-slate-400">
                  {locale === 'vi-VN' ? 'Thời gian giao dịch:' : 'Transaction time:'}
                </span>
                <span className="font-semibold text-slate-800 dark:text-slate-200">
                  {formatExactTransactionTime(selectedTx.occurredAt || selectedTx.date, locale)}
                </span>
              </div>

              <div className="flex justify-between py-1.5 border-b border-slate-100 dark:border-slate-800">
                <span className="text-slate-400">
                  {locale === 'vi-VN' ? 'Ghi nhận lúc:' : 'Recorded at:'}
                </span>
                <span className="font-medium text-slate-600 dark:text-slate-400 font-mono">
                  {formatExactTransactionTime(selectedTx.createdAt || selectedTx.date, locale)}
                </span>
              </div>

              {/* Classification Metadata */}
              <div className="flex justify-between py-1.5 border-b border-slate-100 dark:border-slate-800">
                <span className="text-slate-400">
                  {locale === 'vi-VN' ? 'Phân loại / Nguồn:' : 'Classification / Source:'}
                </span>
                <span className="font-mono text-slate-700 dark:text-slate-300 text-[11px]">
                  {selectedTx.semanticType || 'unknown'} · {selectedTx.classificationSource || 'unknown'}
                  {typeof selectedTx.classificationConfidence === 'number' && (
                    <span className="text-slate-400 ml-1">
                      ({Math.round(selectedTx.classificationConfidence * 100)}%)
                    </span>
                  )}
                </span>
              </div>

              {(selectedTx.description || selectedTx.descriptionEn) && (
                <div className="py-1.5">
                  <span className="text-slate-400 block mb-1">{t('transactions.description')}:</span>
                  <p className="p-2.5 rounded-xl bg-slate-50 dark:bg-slate-800 text-slate-700 dark:text-slate-300">
                    {getLocalizedTxDesc(selectedTx, locale)}
                  </p>
                </div>
              )}
            </div>

            {/* Read-Only Transaction Evidence */}
            {selectedTxEvidence && selectedTxEvidence.length > 0 && (
              <div className="pt-2 border-t border-slate-100 dark:border-slate-800">
                <AttachmentList
                  attachments={selectedTxEvidence}
                  title={locale === 'vi-VN' ? 'Chứng từ giao dịch' : 'Transaction Evidence'}
                  loadAttachmentBlob={(att) => downloadEvidenceBlob ? downloadEvidenceBlob(att.id) : Promise.reject(new Error('No blob loader'))}
                />
              </div>
            )}

            {/* Immutable Correction History with Diff Inspection */}
            {selectedTx.corrections && selectedTx.corrections.length > 0 && (
              <div className="space-y-3 pt-3 border-t border-slate-100 dark:border-slate-800">
                <div className="text-[11px] font-bold text-slate-500 uppercase tracking-wider flex items-center gap-1.5">
                  <History className="w-3.5 h-3.5 text-slate-400" />
                  <span>
                    {locale === 'vi-VN'
                      ? `Lịch sử điều chỉnh (${selectedTx.corrections.length})`
                      : `Correction History (${selectedTx.corrections.length})`}
                  </span>
                </div>
                <div className="space-y-2.5">
                  {selectedTx.corrections.map((corr, idx) => (
                    <div
                      key={corr.id || idx}
                      className="p-3.5 rounded-2xl bg-slate-50 dark:bg-slate-800/50 border border-slate-200/70 dark:border-slate-700/70 space-y-2 text-xs"
                    >
                      <div className="flex items-center justify-between text-[11px]">
                        <span className="font-bold text-slate-800 dark:text-slate-200">
                          {corr.actorName || (locale === 'vi-VN' ? 'Chủ sở hữu' : 'Owner')}
                        </span>
                        <span className="font-mono text-slate-400">
                          {formatExactTransactionTime(corr.timestamp, locale)}
                        </span>
                      </div>
                      <p className="text-slate-700 dark:text-slate-300 italic">
                        {locale === 'vi-VN' ? 'Lý do:' : 'Reason:'} "{corr.reason}"
                      </p>

                      {/* Field Diffs */}
                      <div className="space-y-1.5 text-[11px] text-slate-600 dark:text-slate-400 bg-white/70 dark:bg-slate-900/50 p-2.5 rounded-xl border border-slate-200/60 dark:border-slate-800">
                        {corr.before.amount !== corr.after.amount && (
                          <div className="flex items-center justify-between">
                            <span>{locale === 'vi-VN' ? 'Số tiền:' : 'Amount:'}</span>
                            <span>
                              <span className="line-through text-rose-500 mr-1.5">{formatMoney(corr.before.amount, locale)}</span>
                              <span className="text-emerald-600 dark:text-emerald-400 font-bold">{formatMoney(corr.after.amount, locale)}</span>
                            </span>
                          </div>
                        )}
                        {corr.before.category !== corr.after.category && (
                          <div className="flex items-center justify-between">
                            <span>{locale === 'vi-VN' ? 'Danh mục:' : 'Category:'}</span>
                            <span>
                              <span className="line-through text-slate-400 mr-1.5">{getCategoryName(corr.before.category)}</span>
                              <span className="font-semibold text-slate-800 dark:text-slate-200">{getCategoryName(corr.after.category)}</span>
                            </span>
                          </div>
                        )}
                        {corr.before.walletName !== corr.after.walletName && (
                          <div className="flex items-center justify-between">
                            <span>{locale === 'vi-VN' ? 'Ví:' : 'Wallet:'}</span>
                            <span>
                              <span className="line-through text-slate-400 mr-1.5">{corr.before.walletName}</span>
                              <span className="font-semibold text-slate-800 dark:text-slate-200">{corr.after.walletName}</span>
                            </span>
                          </div>
                        )}
                        {corr.before.description !== corr.after.description && (
                          <div className="flex items-center justify-between">
                            <span>{locale === 'vi-VN' ? 'Mô tả:' : 'Description:'}</span>
                            <span>
                              <span className="line-through text-slate-400 mr-1.5">{corr.before.description}</span>
                              <span className="font-semibold text-slate-800 dark:text-slate-200">{corr.after.description}</span>
                            </span>
                          </div>
                        )}
                        {corr.before.occurredAt !== corr.after.occurredAt && (
                          <div className="flex items-center justify-between">
                            <span>{locale === 'vi-VN' ? 'Thời gian:' : 'Time:'}</span>
                            <span>
                              <span className="line-through text-slate-400 mr-1.5">{formatExactTransactionTime(corr.before.occurredAt, locale)}</span>
                              <span className="font-semibold text-slate-800 dark:text-slate-200">{formatExactTransactionTime(corr.after.occurredAt, locale)}</span>
                            </span>
                          </div>
                        )}
                        {corr.before.counterparty !== corr.after.counterparty && (
                          <div className="flex items-center justify-between">
                            <span>{locale === 'vi-VN' ? 'Đối tác:' : 'Counterparty:'}</span>
                            <span>
                              <span className="line-through text-slate-400 mr-1.5">{corr.before.counterparty || (locale === 'vi-VN' ? '(trống)' : '(empty)')}</span>
                              <span className="font-semibold text-slate-800 dark:text-slate-200">{corr.after.counterparty || (locale === 'vi-VN' ? '(trống)' : '(empty)')}</span>
                            </span>
                          </div>
                        )}
                      </div>

                      {/* Additional Evidence */}
                      {corr.addedEvidence && corr.addedEvidence.length > 0 && (
                        <div className="pt-1">
                          <AttachmentList
                            attachments={corr.addedEvidence}
                            title={locale === 'vi-VN' ? 'Chứng từ bổ sung khi điều chỉnh' : 'Added Correction Evidence'}
                            loadAttachmentBlob={(att) => downloadEvidenceBlob ? downloadEvidenceBlob(att.id) : Promise.reject(new Error('No blob loader'))}
                          />
                        </div>
                      )}
                    </div>
                  ))}
                </div>
              </div>
            )}

            {/* Admin Grant History */}
            {selectedTx.grantHistory && selectedTx.grantHistory.length > 0 && (
              <div className="space-y-2 pt-3 border-t border-slate-100 dark:border-slate-800">
                <div className="text-[11px] font-bold text-slate-500 uppercase tracking-wider flex items-center gap-1.5">
                  <ShieldCheck className="w-3.5 h-3.5 text-blue-500" />
                  <span>
                    {locale === 'vi-VN'
                      ? `Lịch sử cấp lượt từ Quản trị viên (${selectedTx.grantHistory.length})`
                      : `Administrator Correction Grants (${selectedTx.grantHistory.length})`}
                  </span>
                </div>
                <div className="space-y-2">
                  {selectedTx.grantHistory.map((gh, idx) => (
                    <div
                      key={gh.id || idx}
                      className="p-3 rounded-2xl bg-blue-50/60 dark:bg-blue-950/25 border border-blue-100 dark:border-blue-900/40 space-y-1 text-xs text-blue-950 dark:text-blue-200"
                    >
                      <div className="flex items-center justify-between text-[11px]">
                        <span className="font-bold">
                          {gh.adminName || (locale === 'vi-VN' ? 'Quản trị viên' : 'Administrator')}
                        </span>
                        <span className="font-mono text-slate-400">
                          {formatExactTransactionTime(gh.grantedAt, locale)}
                        </span>
                      </div>
                      <p className="text-slate-700 dark:text-slate-300">
                        {locale === 'vi-VN' ? 'Lý do:' : 'Reason:'} "{gh.reason}"
                      </p>
                      {gh.supportCaseId && (
                        <p className="text-[10px] text-slate-400 font-mono">
                          {locale === 'vi-VN' ? 'Mã yêu cầu hỗ trợ:' : 'Support request ID:'} #{gh.supportCaseId}
                        </p>
                      )}
                    </div>
                  ))}
                </div>
              </div>
            )}

            {/* Modal Actions */}
            <div className="flex flex-wrap items-center justify-between gap-2.5 pt-3 border-t border-slate-100 dark:border-slate-800">
              <button
                type="button"
                onClick={() => {
                  const tx = selectedTx;
                  setSelectedTx(null);
                  navigate(`/disputes?tx=${tx.id}`);
                }}
                className="flex items-center gap-1.5 text-xs text-rose-600 dark:text-rose-400 hover:text-rose-700 font-semibold hover:underline cursor-pointer"
              >
                <ShieldAlert className="h-3.5 w-3.5" />
                <span>{locale === 'vi-VN' ? 'Khiếu nại giao dịch' : 'Dispute transaction'}</span>
              </button>

              <div className="flex items-center gap-2">
                {(() => {
                  const isManual = selectedTx.isManualEntry && (selectedTx.type === 'income' || selectedTx.type === 'expense');
                  if (isManual) {
                    const allowance = typeof selectedTx.correctionAllowance === 'number' ? selectedTx.correctionAllowance : 1;
                    const count = typeof selectedTx.correctionCount === 'number' ? selectedTx.correctionCount : (selectedTx.corrections?.length || 0);
                    const remaining = allowance - count;

                    if (remaining <= 0) {
                      return (
                        <Button
                          size="sm"
                          variant="outline"
                          onClick={() => {
                            const txId = selectedTx.id;
                            setSelectedTx(null);
                            navigate(`/support?tab=cases&category=transaction_edit&tx=${txId}`);
                          }}
                          className="gap-1.5 text-amber-700 dark:text-amber-300 border-amber-300 dark:border-amber-700 hover:bg-amber-50 dark:hover:bg-amber-950/30 cursor-pointer"
                        >
                          <LifeBuoy className="h-3.5 w-3.5" />
                          <span>{locale === 'vi-VN' ? 'Yêu cầu thêm lượt sửa' : 'Request extra correction'}</span>
                        </Button>
                      );
                    }

                    return (
                      <Button
                        size="sm"
                        variant="outline"
                        onClick={() => {
                          const tx = selectedTx;
                          setSelectedTx(null);
                          handleOpenCorrection(tx);
                        }}
                        className="gap-1.5 text-emerald-700 dark:text-emerald-300 border-emerald-300 dark:border-emerald-700 hover:bg-emerald-50 dark:hover:bg-emerald-950/30 cursor-pointer font-bold"
                      >
                        <Edit2 className="h-3.5 w-3.5" />
                        <span>
                          {locale === 'vi-VN' ? `Chỉnh sửa · còn ${remaining} lượt` : `Edit · ${remaining} left`}
                        </span>
                      </Button>
                    );
                  }

                  // Non-manual / system transactions: keep existing restrictions unchanged
                  if (selectedTx.canEdit !== false && selectedTx.type !== 'transfer_out' && selectedTx.type !== 'transfer_in') {
                    return (
                      <Button
                        size="sm"
                        variant="outline"
                        onClick={() => {
                          const tx = selectedTx;
                          setSelectedTx(null);
                          handleOpenEdit(tx);
                        }}
                        className="gap-1.5"
                      >
                        <Edit2 className="h-3.5 w-3.5" />
                        <span>{t('transactions.edit')}</span>
                      </Button>
                    );
                  }

                  return null;
                })()}

                <Button size="sm" onClick={() => setSelectedTx(null)}>
                  {t('common.close')}
                </Button>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* 2. ADD INCOME MODAL */}
      {isAddIncomeOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-xs animate-in fade-in duration-150">
          <form
            onSubmit={handleSaveIncome}
            onKeyDown={(e) => {
              if (e.key === 'Enter' && (e.target as HTMLElement).tagName !== 'TEXTAREA') {
                e.preventDefault();
                handleSaveIncome(e);
              }
            }}
            className="w-full max-w-lg max-h-[90vh] overflow-y-auto bg-white dark:bg-slate-900 rounded-3xl p-6 sm:p-8 shadow-2xl border border-slate-100 dark:border-slate-800 space-y-5 animate-in zoom-in-95"
          >
            <div className="flex items-center justify-between">
              <div className="flex items-center gap-2.5">
                <div className="h-9 w-9 rounded-2xl bg-emerald-100 dark:bg-emerald-950 text-emerald-600 dark:text-emerald-400 flex items-center justify-center">
                  <Plus className="h-5 w-5" />
                </div>
                <h3 className="text-base sm:text-lg font-bold text-slate-900 dark:text-white">
                  {t('transactions.addIncomeTitle')}
                </h3>
              </div>
              <button
                type="button"
                disabled={isRecordingRecurring}
                onClick={handleCloseAddIncome}
                className="p-1.5 rounded-xl text-slate-400 hover:bg-slate-100 dark:hover:bg-slate-800 disabled:opacity-50 disabled:cursor-not-allowed"
              >
                <X className="h-4 w-4" />
              </button>
            </div>

            {pendingRecurringOccurrence && (
              <div className="p-3.5 rounded-2xl bg-emerald-50/80 dark:bg-emerald-950/40 border border-emerald-200/80 dark:border-emerald-800/60 flex items-start gap-3">
                <RefreshCw className="h-4 w-4 text-emerald-600 dark:text-emerald-400 mt-0.5 shrink-0" />
                <div className="text-xs space-y-0.5">
                  <div className="font-bold text-emerald-800 dark:text-emerald-300">
                    {locale === 'vi-VN' ? 'Ghi nhận kỳ thanh toán định kỳ' : 'Recording recurring occurrence'}
                  </div>
                  <div className="text-slate-600 dark:text-slate-400">
                    {pendingRecurringOccurrence.recurringItem.title} • {locale === 'vi-VN' ? 'Số tiền, danh mục, ví và ngày theo lịch định kỳ đã được cố định (chỉ đọc).' : 'Scheduled amount, category, wallet, and date are fixed (read-only).'}
                  </div>
                </div>
              </div>
            )}

            {errorMessage && (
              <div className="p-3 rounded-2xl bg-rose-50 text-rose-700 text-xs font-semibold flex items-center gap-2">
                <AlertCircle className="h-4 w-4 shrink-0" />
                <span>{errorMessage}</span>
              </div>
            )}

            <div className="space-y-4 text-xs">
              {/* Số tiền */}
              <div className="space-y-1.5">
                <label className="font-bold text-slate-700 dark:text-slate-300">
                  {t('transactions.amount')} *
                </label>
                <div className="relative">
                  <input
                    type="text"
                    inputMode="numeric"
                    placeholder={locale === 'vi-VN' ? 'Ví dụ: 5000000' : 'e.g. 5000000'}
                    value={formAmount}
                    disabled={Boolean(pendingRecurringOccurrence) || isRecordingRecurring}
                    readOnly={Boolean(pendingRecurringOccurrence)}
                    onChange={(e) => {
                      setFormAmount(sanitizeIntegerInput(e.target.value));
                      setErrorMessage(null);
                    }}
                    required
                    className={cn(
                      "w-full px-4 py-2.5 text-sm rounded-2xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-slate-900 dark:text-white font-bold placeholder:font-normal focus:outline-none focus:ring-2 focus:ring-emerald-500",
                      pendingRecurringOccurrence && "bg-slate-100 dark:bg-slate-800/60 text-slate-500 dark:text-slate-400 cursor-not-allowed border-slate-200/80"
                    )}
                  />
                  <span className="absolute right-4 top-1/2 -translate-y-1/2 text-xs font-bold text-slate-400">
                    VND
                  </span>
                </div>
                {/* Live readable money preview */}
                {isFormAmountValid && parsedFormAmount > 0 && Number.isSafeInteger(parsedFormAmount) && (
                  <div
                    id="income-amount-preview"
                    className="px-3 py-1.5 rounded-xl bg-emerald-50/70 dark:bg-emerald-950/30 border border-emerald-100 dark:border-emerald-900/40 text-left transition-all"
                  >
                    <div className="text-xs font-bold text-emerald-700 dark:text-emerald-300">
                      {formatReadableAmount(parsedFormAmount, locale)}
                    </div>
                    <div className="text-[11px] font-medium text-emerald-600/80 dark:text-emerald-400/80 leading-tight mt-0.5">
                      {moneyToWords(parsedFormAmount, locale)}
                    </div>
                  </div>
                )}
              </div>

              {/* Nguồn thu / Danh mục */}
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <div className="space-y-1.5">
                  <label className="font-bold text-slate-700 dark:text-slate-300">
                    {t('transactions.incomeSource')}
                  </label>
                  <Select
                    value={formCategory}
                    onChange={(val) => setFormCategory(val)}
                    disabled={Boolean(pendingRecurringOccurrence) || isRecordingRecurring}
                    accentColor="emerald"
                  >
                    {categories
                      .filter((c) => c.kind === 'income' && !c.isArchived)
                      .map((c) => (
                        <option key={c.id} value={c.key}>
                          {c.key.startsWith('categories.') ? t(c.key) : (c.isSystem ? getLocalizedCategoryName(c.name, t, categories) : c.name)}
                        </option>
                      ))}
                  </Select>
                </div>

                {/* Ví nhận tiền */}
                <div className="space-y-1.5">
                  <label className="font-bold text-slate-700 dark:text-slate-300">
                    {t('transactions.receivingWallet')}
                  </label>
                  <Select
                    value={formWalletId}
                    onChange={(val) => setFormWalletId(val)}
                    disabled={Boolean(pendingRecurringOccurrence) || isRecordingRecurring}
                    accentColor="emerald"
                    icon={<Wallet className="h-4 w-4 text-emerald-500" />}
                  >
                    {wallets.map((w) => (
                      <option key={w.id} value={w.id}>
                        {getLocalizedWalletName(w.id, w.name, locale)} ({formatMoney(w.balance, locale)})
                      </option>
                    ))}
                  </Select>
                </div>
              </div>

              {/* Ngày giờ & Đối tác */}
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <div className="space-y-1.5">
                  <label className="font-bold text-slate-700 dark:text-slate-300">
                    {t('transactions.date')} & {locale === 'vi-VN' ? 'Giờ giao dịch' : 'Time'} *
                  </label>
                  <input
                    type="datetime-local"
                    value={formDateTime}
                    disabled={Boolean(pendingRecurringOccurrence) || isRecordingRecurring}
                    readOnly={Boolean(pendingRecurringOccurrence)}
                    onChange={(e) => setFormDateTime(e.target.value)}
                    required
                    className={cn(
                      "w-full px-3.5 py-2 rounded-2xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-slate-900 dark:text-white focus:outline-none",
                      pendingRecurringOccurrence && "bg-slate-100 dark:bg-slate-800/60 text-slate-500 dark:text-slate-400 cursor-not-allowed border-slate-200/80"
                    )}
                  />
                </div>

                <div className="space-y-1.5">
                  <label className="font-bold text-slate-700 dark:text-slate-300">
                    {locale === 'vi-VN' ? 'Người gửi / Công ty (Tùy chọn)' : 'Sender / Company (Optional)'}
                  </label>
                  <input
                    type="text"
                    placeholder={locale === 'vi-VN' ? 'Ví dụ: Công ty Cổ phần ABC' : 'e.g. ABC Corporation'}
                    value={formCounterparty}
                    disabled={isRecordingRecurring}
                    onChange={(e) => setFormCounterparty(e.target.value)}
                    className="w-full px-3.5 py-2 rounded-2xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-slate-900 dark:text-white focus:outline-none"
                  />
                </div>
              </div>

              {/* Mô tả */}
              <div className="space-y-1.5">
                <label className="font-bold text-slate-700 dark:text-slate-300">
                  {t('transactions.description')}
                </label>
                <input
                  type="text"
                  placeholder={locale === 'vi-VN' ? 'Ví dụ: Lương tháng 8' : 'e.g. August Salary'}
                  value={formDesc}
                  disabled={isRecordingRecurring}
                  onChange={(e) => setFormDesc(e.target.value)}
                  className="w-full px-3.5 py-2.5 rounded-2xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-slate-900 dark:text-white focus:outline-none"
                />
              </div>

              {/* Chứng từ giao dịch (Bắt buộc) */}
              <div className="space-y-1.5 pt-1">
                <div className="flex items-center justify-between">
                  <label className="font-bold text-slate-700 dark:text-slate-300 flex items-center gap-1">
                    <FileText className="h-3.5 w-3.5 text-emerald-500" />
                    <span>{locale === 'vi-VN' ? 'Chứng từ giao dịch (Ảnh / Biên lai PDF) *' : 'Transaction Evidence *'}</span>
                  </label>
                  <span className="text-[11px] text-slate-400">
                    {locale === 'vi-VN' ? 'Tối đa 3 tệp, 2 MB/tệp' : 'Max 3 files, 2 MB each'}
                  </span>
                </div>
                <AttachmentUploader
                  attachments={formEvidence}
                  onChange={(files) => {
                    setFormEvidence(files);
                    if (files.length > 0) setErrorMessage(null);
                  }}
                  maxFiles={3}
                  maxSizeMb={2}
                />
              </div>
            </div>

            <div className="flex items-center justify-end gap-3 pt-3 border-t border-slate-100 dark:border-slate-800">
              <Button
                type="button"
                variant="outline"
                disabled={isRecordingRecurring}
                onClick={handleCloseAddIncome}
              >
                {t('transactions.cancelBtn')}
              </Button>
              <Button
                type="submit"
                disabled={isRecordingRecurring}
                className="bg-emerald-600 hover:bg-emerald-700 text-white font-bold disabled:opacity-50 disabled:cursor-not-allowed"
              >
                {isRecordingRecurring
                  ? (locale === 'vi-VN' ? 'Đang ghi nhận...' : 'Recording...')
                  : t('transactions.saveIncomeBtn')}
              </Button>
            </div>
          </form>
        </div>
      )}

      {/* 3. ADD EXPENSE MODAL */}
      {isAddExpenseOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-xs animate-in fade-in duration-150">
          <form
            onSubmit={handleSaveExpense}
            onKeyDown={(e) => {
              if (e.key === 'Enter' && (e.target as HTMLElement).tagName !== 'TEXTAREA') {
                e.preventDefault();
                handleSaveExpense(e);
              }
            }}
            className="w-full max-w-lg max-h-[90vh] overflow-y-auto bg-white dark:bg-slate-900 rounded-3xl p-6 sm:p-8 shadow-2xl border border-slate-100 dark:border-slate-800 space-y-5 animate-in zoom-in-95"
          >
            <div className="flex items-center justify-between">
              <div className="flex items-center gap-2.5">
                <div className="h-9 w-9 rounded-2xl bg-rose-100 dark:bg-rose-950 text-rose-600 dark:text-rose-400 flex items-center justify-center">
                  <Minus className="h-5 w-5" />
                </div>
                <h3 className="text-base sm:text-lg font-bold text-slate-900 dark:text-white">
                  {t('transactions.addExpenseTitle')}
                </h3>
              </div>
              <button
                type="button"
                disabled={isRecordingRecurring}
                onClick={handleCloseAddExpense}
                className="p-1.5 rounded-xl text-slate-400 hover:bg-slate-100 dark:hover:bg-slate-800 disabled:opacity-50 disabled:cursor-not-allowed"
              >
                <X className="h-4 w-4" />
              </button>
            </div>

            {pendingRecurringOccurrence && (
              <div className="p-3.5 rounded-2xl bg-rose-50/80 dark:bg-rose-950/40 border border-rose-200/80 dark:border-rose-800/60 flex items-start gap-3">
                <RefreshCw className="h-4 w-4 text-rose-600 dark:text-rose-400 mt-0.5 shrink-0" />
                <div className="text-xs space-y-0.5">
                  <div className="font-bold text-rose-800 dark:text-rose-300">
                    {locale === 'vi-VN' ? 'Ghi nhận kỳ chi tiêu định kỳ' : 'Recording recurring occurrence'}
                  </div>
                  <div className="text-slate-600 dark:text-slate-400">
                    {pendingRecurringOccurrence.recurringItem.title} • {locale === 'vi-VN' ? 'Số tiền, danh mục, ví và ngày theo lịch định kỳ đã được cố định (chỉ đọc).' : 'Scheduled amount, category, wallet, and date are fixed (read-only).'}
                  </div>
                </div>
              </div>
            )}

            {/* Inline Insufficient Warning */}
            {isExpenseInsufficient && (
              <div className="p-3 rounded-2xl bg-rose-50 dark:bg-rose-950/40 border border-rose-200 dark:border-rose-900 text-rose-700 dark:text-rose-300 text-xs font-semibold flex items-center gap-2">
                <AlertCircle className="h-4 w-4 shrink-0" />
                <span>{t('transactions.insufficientBalance')}</span>
              </div>
            )}

            {errorMessage && !isExpenseInsufficient && (
              <div className="p-3 rounded-2xl bg-rose-50 text-rose-700 text-xs font-semibold flex items-center gap-2">
                <AlertCircle className="h-4 w-4 shrink-0" />
                <span>{errorMessage}</span>
              </div>
            )}

            <div className="space-y-4 text-xs">
              {/* Số tiền */}
              <div className="space-y-1.5">
                <label className="font-bold text-slate-700 dark:text-slate-300">
                  {t('transactions.amount')} *
                </label>
                <div className="relative">
                  <input
                    type="text"
                    inputMode="numeric"
                    placeholder={locale === 'vi-VN' ? 'Ví dụ: 65000' : 'e.g. 65000'}
                    value={formAmount}
                    disabled={Boolean(pendingRecurringOccurrence) || isRecordingRecurring}
                    readOnly={Boolean(pendingRecurringOccurrence)}
                    onChange={(e) => {
                      setFormAmount(sanitizeIntegerInput(e.target.value));
                      setErrorMessage(null);
                    }}
                    required
                    className={cn(
                      "w-full px-4 py-2.5 text-sm rounded-2xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-slate-900 dark:text-white font-bold placeholder:font-normal focus:outline-none focus:ring-2 focus:ring-rose-500",
                      pendingRecurringOccurrence && "bg-slate-100 dark:bg-slate-800/60 text-slate-500 dark:text-slate-400 cursor-not-allowed border-slate-200/80"
                    )}
                  />
                  <span className="absolute right-4 top-1/2 -translate-y-1/2 text-xs font-bold text-slate-400">
                    VND
                  </span>
                </div>
                {/* Live readable money preview */}
                {isFormAmountValid && parsedFormAmount > 0 && Number.isSafeInteger(parsedFormAmount) && (
                  <div
                    id="expense-amount-preview"
                    className="px-3 py-1.5 rounded-xl bg-rose-50/70 dark:bg-rose-950/30 border border-rose-100 dark:border-rose-900/40 text-left transition-all"
                  >
                    <div className="text-xs font-bold text-rose-700 dark:text-rose-300">
                      {formatReadableAmount(parsedFormAmount, locale)}
                    </div>
                    <div className="text-[11px] font-medium text-rose-600/80 dark:text-rose-400/80 leading-tight mt-0.5">
                      {moneyToWords(parsedFormAmount, locale)}
                    </div>
                  </div>
                )}
              </div>

              {/* Danh mục & Ví thanh toán */}
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <div className="space-y-1.5">
                  <label className="font-bold text-slate-700 dark:text-slate-300">
                    {t('transactions.category')}
                  </label>
                  <Select
                    value={formCategory}
                    onChange={(val) => setFormCategory(val)}
                    disabled={Boolean(pendingRecurringOccurrence) || isRecordingRecurring}
                    accentColor="rose"
                  >
                    {categories
                      .filter((c) => c.kind === 'expense' && !c.isArchived)
                      .map((c) => (
                        <option key={c.id} value={c.key}>
                          {c.key.startsWith('categories.') ? t(c.key) : (c.isSystem ? getLocalizedCategoryName(c.name, t, categories) : c.name)}
                        </option>
                      ))}
                  </Select>
                </div>

                <div className="space-y-1.5">
                  <label className="font-bold text-slate-700 dark:text-slate-300">
                    {t('transactions.payingWallet')}
                  </label>
                  <Select
                    value={formWalletId}
                    onChange={(val) => setFormWalletId(val)}
                    disabled={Boolean(pendingRecurringOccurrence) || isRecordingRecurring}
                    accentColor="rose"
                    icon={<Wallet className="h-4 w-4 text-rose-500" />}
                  >
                    {wallets.map((w) => (
                      <option key={w.id} value={w.id}>
                        {getLocalizedWalletName(w.id, w.name, locale)} ({formatMoney(w.balance, locale)})
                      </option>
                    ))}
                  </Select>
                </div>
              </div>

              {/* Ngày giờ & Nơi chi tiêu */}
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <div className="space-y-1.5">
                  <label className="font-bold text-slate-700 dark:text-slate-300">
                    {t('transactions.date')} & {locale === 'vi-VN' ? 'Giờ giao dịch' : 'Time'} *
                  </label>
                  <input
                    type="datetime-local"
                    value={formDateTime}
                    disabled={Boolean(pendingRecurringOccurrence) || isRecordingRecurring}
                    readOnly={Boolean(pendingRecurringOccurrence)}
                    onChange={(e) => setFormDateTime(e.target.value)}
                    required
                    className={cn(
                      "w-full px-3.5 py-2 rounded-2xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-slate-900 dark:text-white focus:outline-none",
                      pendingRecurringOccurrence && "bg-slate-100 dark:bg-slate-800/60 text-slate-500 dark:text-slate-400 cursor-not-allowed border-slate-200/80"
                    )}
                  />
                </div>

                <div className="space-y-1.5">
                  <label className="font-bold text-slate-700 dark:text-slate-300">
                    {locale === 'vi-VN' ? 'Địa điểm / Đối tác (Tùy chọn)' : 'Merchant / Place (Optional)'}
                  </label>
                  <input
                    type="text"
                    placeholder={locale === 'vi-VN' ? 'Ví dụ: Quán Cơm Tấm Sài Gòn' : 'e.g. Starbucks Coffee'}
                    value={formCounterparty}
                    disabled={isRecordingRecurring}
                    onChange={(e) => setFormCounterparty(e.target.value)}
                    className="w-full px-3.5 py-2 rounded-2xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-slate-900 dark:text-white focus:outline-none"
                  />
                </div>
              </div>

              {/* Mô tả */}
              <div className="space-y-1.5">
                <label className="font-bold text-slate-700 dark:text-slate-300">
                  {t('transactions.description')}
                </label>
                <input
                  type="text"
                  placeholder={locale === 'vi-VN' ? 'Ví dụ: Cơm trưa' : 'e.g. Lunch'}
                  value={formDesc}
                  disabled={isRecordingRecurring}
                  onChange={(e) => setFormDesc(e.target.value)}
                  className="w-full px-3.5 py-2.5 rounded-2xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-slate-900 dark:text-white focus:outline-none"
                />
              </div>

              {/* Chứng từ giao dịch (Bắt buộc) */}
              <div className="space-y-1.5 pt-1">
                <div className="flex items-center justify-between">
                  <label className="font-bold text-slate-700 dark:text-slate-300 flex items-center gap-1">
                    <FileText className="h-3.5 w-3.5 text-rose-500" />
                    <span>{locale === 'vi-VN' ? 'Chứng từ chi tiêu (Hóa đơn / Biên lai PDF) *' : 'Expense Evidence *'}</span>
                  </label>
                  <span className="text-[11px] text-slate-400">
                    {locale === 'vi-VN' ? 'Tối đa 3 tệp, 2 MB/tệp' : 'Max 3 files, 2 MB each'}
                  </span>
                </div>
                <AttachmentUploader
                  attachments={formEvidence}
                  onChange={(files) => {
                    setFormEvidence(files);
                    if (files.length > 0) setErrorMessage(null);
                  }}
                  maxFiles={3}
                  maxSizeMb={2}
                />
              </div>
            </div>

            <div className="flex items-center justify-end gap-3 pt-3 border-t border-slate-100 dark:border-slate-800">
              <Button
                type="button"
                variant="outline"
                disabled={isRecordingRecurring}
                onClick={handleCloseAddExpense}
              >
                {t('transactions.cancelBtn')}
              </Button>
              <Button
                type="submit"
                disabled={isRecordingRecurring || isExpenseInsufficient || parsedFormAmount <= 0}
                className="bg-rose-600 hover:bg-rose-700 text-white font-bold disabled:opacity-50 disabled:cursor-not-allowed"
              >
                {isRecordingRecurring
                  ? (locale === 'vi-VN' ? 'Đang ghi nhận...' : 'Recording...')
                  : t('transactions.saveExpenseBtn')}
              </Button>
            </div>
          </form>
        </div>
      )}

      {/* 4. CONTROLLED CORRECTION MODAL (FOR MANUAL INCOME / EXPENSE) */}
      {correctingTx && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-xs animate-in fade-in duration-150">
          <form
            onSubmit={handleSaveCorrection}
            onKeyDown={(e) => {
              if (e.key === 'Enter' && (e.target as HTMLElement).tagName !== 'TEXTAREA') {
                e.preventDefault();
                handleSaveCorrection(e);
              }
            }}
            className="w-full max-w-lg max-h-[90vh] overflow-y-auto bg-white dark:bg-slate-900 rounded-3xl p-6 sm:p-8 shadow-2xl border border-slate-100 dark:border-slate-800 space-y-5 animate-in zoom-in-95"
          >
            <div className="flex items-center justify-between">
              <div className="flex items-center gap-2.5">
                <div className="h-9 w-9 rounded-2xl bg-amber-100 dark:bg-amber-950 text-amber-600 dark:text-amber-400 flex items-center justify-center">
                  <Edit2 className="h-4 w-4" />
                </div>
                <div>
                  <h3 className="text-base sm:text-lg font-bold text-slate-900 dark:text-white">
                    {locale === 'vi-VN' ? 'Điều chỉnh giao dịch' : 'Correct Transaction'}
                  </h3>
                  <p className="text-[11px] text-slate-400">
                    {correctingTx.type === 'income'
                      ? (locale === 'vi-VN' ? 'Khoản thu nhập thủ công' : 'Manual income')
                      : (locale === 'vi-VN' ? 'Khoản chi tiêu thủ công' : 'Manual expense')}
                  </p>
                </div>
              </div>
              <button
                type="button"
                onClick={handleCloseCorrection}
                className="p-1.5 rounded-xl text-slate-400 hover:bg-slate-100 dark:hover:bg-slate-800"
              >
                <X className="h-4 w-4" />
              </button>
            </div>

            {/* Allowance Badge */}
            {(() => {
              const allowance = typeof correctingTx.correctionAllowance === 'number' ? correctingTx.correctionAllowance : 1;
              const count = typeof correctingTx.correctionCount === 'number' ? correctingTx.correctionCount : (correctingTx.corrections?.length || 0);
              const remaining = Math.max(0, allowance - count);
              return (
                <div className="p-3 rounded-2xl bg-amber-50 dark:bg-amber-950/40 border border-amber-200 dark:border-amber-900/60 flex items-center justify-between text-xs text-amber-900 dark:text-amber-200">
                  <span className="font-semibold">
                    {locale === 'vi-VN' ? 'Lượt sửa còn lại cho giao dịch này:' : 'Remaining corrections:'}
                  </span>
                  <span className="px-2.5 py-0.5 rounded-full font-bold bg-amber-200/70 dark:bg-amber-900 text-amber-950 dark:text-amber-100 text-xs">
                    {remaining} / {allowance} {locale === 'vi-VN' ? 'lượt' : 'left'}
                  </span>
                </div>
              );
            })()}

            {corrErrorMessage && (
              <div className="p-3 rounded-2xl bg-rose-50 text-rose-700 text-xs font-semibold flex items-center gap-2">
                <AlertCircle className="h-4 w-4 shrink-0" />
                <span>{corrErrorMessage}</span>
              </div>
            )}

            <div className="space-y-4 text-xs">
              {/* Số tiền mới */}
              <div className="space-y-1.5">
                <label className="font-bold text-slate-700 dark:text-slate-300">
                  {t('transactions.amount')} *
                </label>
                <div className="relative">
                  <input
                    type="text"
                    inputMode="numeric"
                    placeholder={locale === 'vi-VN' ? 'Nhập số tiền điều chỉnh' : 'Enter corrected amount'}
                    value={corrAmount}
                    onChange={(e) => {
                      setCorrAmount(sanitizeIntegerInput(e.target.value));
                      setCorrErrorMessage(null);
                    }}
                    required
                    className="w-full px-4 py-2.5 text-sm rounded-2xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-slate-900 dark:text-white font-bold placeholder:font-normal focus:outline-none focus:ring-2 focus:ring-amber-500"
                  />
                  <span className="absolute right-4 top-1/2 -translate-y-1/2 text-xs font-bold text-slate-400">
                    VND
                  </span>
                </div>
                {/* Readable money preview */}
                {(() => {
                  const { valid, value } = parseSafePositiveInteger(corrAmount);
                  if (valid && value > 0) {
                    return (
                      <div className="px-3 py-1.5 rounded-xl bg-amber-50/70 dark:bg-amber-950/30 border border-amber-100 dark:border-amber-900/40 text-left">
                        <div className="text-xs font-bold text-amber-800 dark:text-amber-300">
                          {formatReadableAmount(value, locale)}
                        </div>
                        <div className="text-[11px] font-medium text-amber-700/80 dark:text-amber-400/80 leading-tight mt-0.5">
                          {moneyToWords(value, locale)}
                        </div>
                      </div>
                    );
                  }
                  return null;
                })()}
              </div>

              {/* Danh mục & Ví */}
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <div className="space-y-1.5">
                  <label className="font-bold text-slate-700 dark:text-slate-300">
                    {t('transactions.category')} *
                  </label>
                  <Select
                    value={corrCategory}
                    onChange={(val) => setCorrCategory(val)}
                    accentColor="amber"
                  >
                    {categories
                      .filter((c) => !c.isArchived && (correctingTx.type === 'income' ? c.kind === 'income' : c.kind === 'expense'))
                      .map((c) => (
                        <option key={c.id} value={c.key}>
                          {c.key.startsWith('categories.') ? t(c.key) : (c.isSystem ? getLocalizedCategoryName(c.name, t, categories) : c.name)}
                        </option>
                      ))}
                  </Select>
                </div>

                <div className="space-y-1.5">
                  <label className="font-bold text-slate-700 dark:text-slate-300">
                    {correctingTx.type === 'income' ? t('transactions.receivingWallet') : t('transactions.payingWallet')} *
                  </label>
                  <Select
                    value={corrWalletId}
                    onChange={(val) => setCorrWalletId(val)}
                    accentColor="amber"
                    icon={<Wallet className="h-4 w-4 text-amber-500" />}
                  >
                    {wallets.map((w) => (
                      <option key={w.id} value={w.id}>
                        {getLocalizedWalletName(w.id, w.name, locale)} ({formatMoney(w.balance, locale)})
                      </option>
                    ))}
                  </Select>
                </div>
              </div>

              {/* Ngày giờ & Đối tác */}
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <div className="space-y-1.5">
                  <label className="font-bold text-slate-700 dark:text-slate-300">
                    {t('transactions.date')} & {locale === 'vi-VN' ? 'Giờ giao dịch' : 'Time'} *
                  </label>
                  <input
                    type="datetime-local"
                    value={corrDateTime}
                    onChange={(e) => setCorrDateTime(e.target.value)}
                    required
                    className="w-full px-3.5 py-2 rounded-2xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-slate-900 dark:text-white focus:outline-none"
                  />
                </div>

                <div className="space-y-1.5">
                  <label className="font-bold text-slate-700 dark:text-slate-300">
                    {correctingTx.type === 'income'
                      ? (locale === 'vi-VN' ? 'Người gửi / Công ty' : 'Sender / Company')
                      : (locale === 'vi-VN' ? 'Địa điểm / Đối tác' : 'Merchant / Place')}
                  </label>
                  <input
                    type="text"
                    value={corrCounterparty}
                    onChange={(e) => setCorrCounterparty(e.target.value)}
                    className="w-full px-3.5 py-2 rounded-2xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-slate-900 dark:text-white focus:outline-none"
                  />
                </div>
              </div>

              {/* Mô tả */}
              <div className="space-y-1.5">
                <label className="font-bold text-slate-700 dark:text-slate-300">
                  {t('transactions.description')}
                </label>
                <input
                  type="text"
                  value={corrDesc}
                  onChange={(e) => setCorrDesc(e.target.value)}
                  className="w-full px-3.5 py-2.5 rounded-2xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-slate-900 dark:text-white focus:outline-none"
                />
              </div>

              {/* Lý do điều chỉnh (Bắt buộc) */}
              <div className="space-y-1.5">
                <div className="flex items-center justify-between">
                  <label className="font-bold text-slate-700 dark:text-slate-300">
                    {locale === 'vi-VN' ? 'Lý do điều chỉnh giao dịch *' : 'Correction Reason *'}
                  </label>
                  <span className="text-[11px] text-rose-500 font-semibold">
                    {locale === 'vi-VN' ? 'Bắt buộc lưu lịch sử' : 'Required'}
                  </span>
                </div>
                <textarea
                  rows={2}
                  required
                  placeholder={
                    locale === 'vi-VN'
                      ? 'Nêu rõ lý do điều chỉnh số tiền, danh mục hoặc ví...'
                      : 'State the reason for this correction...'
                  }
                  value={corrReason}
                  onChange={(e) => {
                    setCorrReason(e.target.value);
                    setCorrErrorMessage(null);
                  }}
                  className="w-full px-3.5 py-2.5 rounded-2xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-slate-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-amber-500 resize-none text-xs"
                />
              </div>

              {/* Bổ sung chứng từ mới (Tùy chọn) */}
              <div className="space-y-1.5 pt-1">
                <div className="flex items-center justify-between">
                  <label className="font-bold text-slate-700 dark:text-slate-300 flex items-center gap-1">
                    <FileText className="h-3.5 w-3.5 text-amber-500" />
                    <span>{locale === 'vi-VN' ? 'Bổ sung chứng từ mới (Tùy chọn)' : 'Additional Evidence (Optional)'}</span>
                  </label>
                  <span className="text-[11px] text-slate-400">
                    {locale === 'vi-VN' ? 'Tối đa 3 tệp, 2 MB/tệp' : 'Max 3 files, 2 MB each'}
                  </span>
                </div>
                <AttachmentUploader
                  attachments={corrAdditionalEvidence}
                  onChange={setCorrAdditionalEvidence}
                  maxFiles={3}
                  maxSizeMb={2}
                />
              </div>
            </div>

            <div className="flex items-center justify-end gap-3 pt-3 border-t border-slate-100 dark:border-slate-800">
              <Button
                type="button"
                variant="outline"
                onClick={handleCloseCorrection}
              >
                {t('transactions.cancelBtn')}
              </Button>
              <Button type="submit" className="bg-amber-600 hover:bg-amber-700 text-white font-bold">
                {locale === 'vi-VN' ? 'Lưu điều chỉnh' : 'Save Correction'}
              </Button>
            </div>
          </form>
        </div>
      )}

      {/* 5. EDIT TRANSACTION FALLBACK MODAL (FOR OTHER SYSTEM EDITABLE TRANSACTIONS) */}
      {editingTx && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-xs animate-in fade-in duration-150">
          <form
            onSubmit={handleSaveEdit}
            onKeyDown={(e) => {
              if (e.key === 'Enter' && (e.target as HTMLElement).tagName !== 'TEXTAREA') {
                e.preventDefault();
                handleSaveEdit(e);
              }
            }}
            className="w-full max-w-lg bg-white dark:bg-slate-900 rounded-3xl p-6 sm:p-8 shadow-2xl border border-slate-100 dark:border-slate-800 space-y-5 animate-in zoom-in-95"
          >
            <div className="flex items-center justify-between">
              <div className="flex items-center gap-2.5">
                <div className="h-9 w-9 rounded-2xl bg-slate-100 dark:bg-slate-800 text-slate-800 dark:text-slate-200 flex items-center justify-center">
                  <Edit2 className="h-4 w-4" />
                </div>
                <h3 className="text-base sm:text-lg font-bold text-slate-900 dark:text-white">
                  {editingTx.type === 'income'
                    ? t('transactions.editIncomeTitle')
                    : t('transactions.editExpenseTitle')}
                </h3>
              </div>
              <button
                type="button"
                onClick={() => setEditingTx(null)}
                className="p-1.5 rounded-xl text-slate-400 hover:bg-slate-100 dark:hover:bg-slate-800"
              >
                <X className="h-4 w-4" />
              </button>
            </div>

            {errorMessage && (
              <div className="p-3 rounded-2xl bg-rose-50 text-rose-700 text-xs font-semibold flex items-center gap-2">
                <AlertCircle className="h-4 w-4 shrink-0" />
                <span>{errorMessage}</span>
              </div>
            )}

            <div className="space-y-4 text-xs">
              {/* Số tiền (Bất biến / Immutable) */}
              <div className="space-y-1.5">
                <div className="flex items-center justify-between">
                  <label className="font-bold text-slate-700 dark:text-slate-300">
                    {t('transactions.amount')}
                  </label>
                  <span className="inline-flex items-center gap-1 text-[11px] font-medium text-slate-400 dark:text-slate-500">
                    <Lock className="h-3 w-3" />
                    {locale === 'vi-VN' ? 'Bất biến' : 'Read-only'}
                  </span>
                </div>
                <div className="relative">
                  <input
                    type="text"
                    disabled
                    readOnly
                    value={formatMoney(Math.abs(editingTx.amount), locale)}
                    className="w-full px-4 py-2.5 text-sm rounded-2xl border border-slate-200 dark:border-slate-800 bg-slate-100 dark:bg-slate-800/60 text-slate-500 dark:text-slate-400 font-bold cursor-not-allowed"
                  />
                  <Lock className="absolute right-3.5 top-1/2 -translate-y-1/2 h-4 w-4 text-slate-400" />
                </div>
                <p className="text-[11px] text-slate-500 dark:text-slate-400 font-normal">
                  {t('transactions.amountImmutableHint')}
                </p>
              </div>

              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <div className="space-y-1.5">
                  <label className="font-bold text-slate-700 dark:text-slate-300">
                    {t('transactions.category')}
                  </label>
                  <Select
                    value={formCategory}
                    onChange={(val) => setFormCategory(val)}
                    accentColor={editingTx.type === 'income' ? 'emerald' : 'rose'}
                  >
                    {categories
                      .filter((c) => !c.isArchived && (editingTx.type === 'income' ? c.kind === 'income' : c.kind === 'expense'))
                      .map((c) => (
                        <option key={c.id} value={c.key}>
                          {c.key.startsWith('categories.') ? t(c.key) : (c.isSystem ? getLocalizedCategoryName(c.name, t, categories) : c.name)}
                        </option>
                      ))}
                  </Select>
                </div>

                {/* Ví (Bất biến / Immutable) */}
                <div className="space-y-1.5">
                  <div className="flex items-center justify-between">
                    <label className="font-bold text-slate-700 dark:text-slate-300">
                      {editingTx.type === 'income' ? t('transactions.receivingWallet') : t('transactions.payingWallet')}
                    </label>
                    <span className="inline-flex items-center gap-1 text-[11px] font-medium text-slate-400 dark:text-slate-500">
                      <Lock className="h-3 w-3" />
                      {locale === 'vi-VN' ? 'Cố định' : 'Locked'}
                    </span>
                  </div>
                  <div className="relative">
                    <input
                      type="text"
                      disabled
                      readOnly
                      value={getLocalizedWalletName(
                        editingTx.walletId || defaultWalletId,
                        wallets.find((w) => w.id === (editingTx.walletId || defaultWalletId))?.name || editingTx.walletName || 'Ví chính',
                        locale
                      )}
                      className="w-full px-3.5 py-2.5 rounded-2xl border border-slate-200 dark:border-slate-800 bg-slate-100 dark:bg-slate-800/60 text-slate-500 dark:text-slate-400 font-semibold cursor-not-allowed"
                    />
                    <Lock className="absolute right-3.5 top-1/2 -translate-y-1/2 h-4 w-4 text-slate-400" />
                  </div>
                  <p className="text-[11px] text-slate-500 dark:text-slate-400 font-normal">
                    {t('transactions.walletImmutableHint')}
                  </p>
                </div>
              </div>

              <div className="space-y-1.5">
                <label className="font-bold text-slate-700 dark:text-slate-300">
                  {t('transactions.description')}
                </label>
                <input
                  type="text"
                  value={formDesc}
                  onChange={(e) => setFormDesc(e.target.value)}
                  className="w-full px-3.5 py-2.5 rounded-2xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-slate-900 dark:text-white"
                />
              </div>
            </div>

            <div className="flex items-center justify-end gap-3 pt-3 border-t border-slate-100 dark:border-slate-800">
              <Button
                type="button"
                variant="outline"
                onClick={() => setEditingTx(null)}
              >
                {t('transactions.cancelBtn')}
              </Button>
              <Button type="submit" className="font-bold">
                {t('transactions.saveChangesBtn')}
              </Button>
            </div>
          </form>
        </div>
      )}
      {/* Category Manager Modal */}
      <CategoryManagerModal
        isOpen={isCategoryModalOpen}
        onClose={() => setIsCategoryModalOpen(false)}
      />
    </div>
  );
};
