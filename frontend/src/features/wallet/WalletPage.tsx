import React, { useState, useEffect, useMemo } from 'react';
import {
  Wallet,
  Plus,
  ArrowLeftRight,
  CreditCard,
  Building2,
  TrendingUp,
  TrendingDown,
  CheckCircle2,
  ChevronRight,
  Eye,
  EyeOff,
  X,
  AlertCircle,
  Edit3,
  Trash2,
  ShieldCheck,
  RefreshCw,
  Unlink,
  Link2,
  ArrowDownLeft,
  ArrowUpRight,
  ArrowDownRight,
  Tag,
  ExternalLink,
  Copy,
  Check,
} from 'lucide-react';
import { useI18n } from '../../i18n/I18nContext';
import { useBalanceVisibility } from '../../providers/BalanceVisibilityProvider';
import { useFinancial, WalletAccount } from '../../providers/FinancialProvider';
import { getLocalizedErrorMessage } from '../../utils/localizationHelpers';
import { useAuth } from '../../providers/AuthProvider';
import { BankConnection, BankTransaction } from '../../types';
import { TestModeBankLinkChallengeDto, WalletTopUpDto } from '../../services/bankApiService';
import { formatMoney } from '../../utils/money';
import { formatDate } from '../../utils/date';
import { Button } from '../../components/ui/Button';
import { Select } from '../../components/ui/Select';
import { cn } from '../../utils/cn';
import { getLocalizedWalletName } from '../../utils/localizationHelpers';
import { parseSafePositiveInteger, sanitizeDigits } from '../../utils/inputValidation';
import { useSecurityPinChallenge } from '../../providers/SecurityPinChallengeProvider';
import { Skeleton } from '../../components/ui/Skeleton';

const parseIsoUtcMs = (dateStr?: string | null): number => {
  if (!dateStr) return NaN;
  const normalized = /(?:Z|[+-]\d{2}:\d{2})$/i.test(dateStr) ? dateStr : `${dateStr}Z`;
  return new Date(normalized).getTime();
};

const POPULAR_BANKS = [
  { id: 'vcb', name: 'Vietcombank', code: 'VCB', color: 'bg-emerald-600' },
  { id: 'tcb', name: 'Techcombank', code: 'TCB', color: 'bg-rose-600' },
  { id: 'acb', name: 'ACB', code: 'ACB', color: 'bg-blue-600' },
  { id: 'mb', name: 'MB Bank', code: 'MB', color: 'bg-indigo-600' },
  { id: 'icb', name: 'VietinBank', code: 'CTG', color: 'bg-cyan-700' },
  { id: 'bidv', name: 'BIDV', code: 'BIDV', color: 'bg-teal-700' },
  { id: 'vpb', name: 'VPBank', code: 'VPB', color: 'bg-emerald-700' },
  { id: 'tpb', name: 'TPBank', code: 'TPB', color: 'bg-purple-600' },
];

export const WalletPage: React.FC = () => {
  const { t, locale } = useI18n();
  const { isHidden, toggleVisibility } = useBalanceVisibility();
  const { currentUser } = useAuth();
  const { challengePinWithValue } = useSecurityPinChallenge();
  const {
    isLoading,
    wallets,
    transactions,
    bankConnections,
    bankTransactions,
    testModeLinkedAccounts,
    walletTopUps,
    categories,
    transferBetweenWallets,
    createWallet,
    updateWallet,
    setDefaultWallet,
    closeWallet,
    startBankLink,
    confirmBankLink,
    unlinkBankConnection,
    reactivateBankConnection,
    syncBankConnection,
    createWalletTopUp,
    confirmWalletTopUp,
    updateBankTransactionCategory,
    refreshBankConnections,
    refreshWalletTopUps,
  } = useFinancial();

  const [toastMessage, setToastMessage] = useState<string | null>(null);
  const [modalError, setModalError] = useState<string | null>(null);

  // Bank Transaction Category Classification state
  const [classifyingBankTx, setClassifyingBankTx] = useState<BankTransaction | null>(null);
  const [isClassifyingBankTxSubmitting, setIsClassifyingBankTxSubmitting] = useState(false);

  // Modals state
  const [isTransferOpen, setIsTransferOpen] = useState(false);
  const [isAddWalletOpen, setIsAddWalletOpen] = useState(false);
  const [editingWallet, setEditingWallet] = useState<WalletAccount | null>(null);
  const [closingWallet, setClosingWallet] = useState<WalletAccount | null>(null);
  const [closeWalletError, setCloseWalletError] = useState<string | null>(null);

  // SWLINK Self-Link Challenge Modal state
  const [linkChallengeModalState, setLinkChallengeModalState] = useState<{
    isOpen: boolean;
    loading: boolean;
    challenge: TestModeBankLinkChallengeDto | null;
    timeLeft: number;
    confirming: boolean;
    error: string | null;
  }>({
    isOpen: false,
    loading: false,
    challenge: null,
    timeLeft: 0,
    confirming: false,
    error: null,
  });
  const [isChallengeCopied, setIsChallengeCopied] = useState(false);

  // SWTP Wallet Top-Up Modal state
  const [topUpModalState, setTopUpModalState] = useState<{
    isOpen: boolean;
    step: 'create' | 'challenge';
    bankConnectionId: string;
    walletId: string;
    amount: string;
    loading: boolean;
    topUp: WalletTopUpDto | null;
    timeLeft: number;
    confirming: boolean;
    error: string | null;
  }>({
    isOpen: false,
    step: 'create',
    bankConnectionId: '',
    walletId: '',
    amount: '50000',
    loading: false,
    topUp: null,
    timeLeft: 0,
    confirming: false,
    error: null,
  });
  const [isTopUpRefCopied, setIsTopUpRefCopied] = useState(false);

  // Bank transactions filter
  const [bankTxFilter, setBankTxFilter] = useState<'ALL' | 'Credit' | 'Debit'>('ALL');

  // Internal Wallet Transfer Form state
  const defaultWalletId = wallets.find((w) => w.isDefault)?.id || wallets[0]?.id || '';
  const secondWalletId = wallets.find((w) => w.id !== defaultWalletId)?.id || wallets[1]?.id || '';
  const [sourceWalletId, setSourceWalletId] = useState<string>(defaultWalletId);
  const [targetWalletId, setTargetWalletId] = useState<string>(secondWalletId);
  const [actionAmount, setActionAmount] = useState('500000');

  // Add / Edit Wallet state
  const [newWalletName, setNewWalletName] = useState('');
  const [newWalletType, setNewWalletType] = useState<'spending' | 'savings' | 'emergency'>('savings');

  // Accessibility: Escape key handler for open modals
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        if (isTransferOpen) setIsTransferOpen(false);
        else if (isAddWalletOpen) setIsAddWalletOpen(false);
        else if (editingWallet) setEditingWallet(null);
        else if (closingWallet) {
          setClosingWallet(null);
          setCloseWalletError(null);
        } else if (linkChallengeModalState.isOpen) {
          setLinkChallengeModalState((prev) => ({ ...prev, isOpen: false }));
        } else if (topUpModalState.isOpen) {
          setTopUpModalState((prev) => ({ ...prev, isOpen: false }));
        }
      }
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [isTransferOpen, isAddWalletOpen, editingWallet, closingWallet, linkChallengeModalState.isOpen, topUpModalState.isOpen]);

  // Keep wallet IDs in sync when wallets list changes
  useEffect(() => {
    if (!sourceWalletId && wallets.length > 0) {
      const defId = wallets.find((w) => w.isDefault)?.id || wallets[0]?.id || '';
      setSourceWalletId(defId);
    }
  }, [wallets, sourceWalletId]);

  const totalBalance = useMemo(() => {
    return wallets.reduce((sum, w) => sum + w.balance, 0);
  }, [wallets]);

  const balanceChange = useMemo(() => {
    if (!transactions || transactions.length === 0 || totalBalance === 0) {
      return null;
    }

    const now = new Date();
    const currentMonth = now.getMonth();
    const currentYear = now.getFullYear();

    let netThisMonth = 0;
    let hasCurrentMonthActivity = false;

    for (const tx of transactions) {
      if (tx.status && tx.status !== 'completed') continue;
      const dateStr = tx.rawDate || tx.date;
      if (!dateStr) continue;
      const d = new Date(dateStr);
      if (isNaN(d.getTime())) continue;

      if (d.getMonth() === currentMonth && d.getFullYear() === currentYear) {
        if (
          tx.type === 'income' ||
          tx.type === 'deposit' ||
          tx.type === 'goal_withdrawal'
        ) {
          netThisMonth += Math.abs(tx.amount);
          hasCurrentMonthActivity = true;
        } else if (
          tx.type === 'expense' ||
          tx.type === 'goal_contribution'
        ) {
          netThisMonth -= Math.abs(tx.amount);
          hasCurrentMonthActivity = true;
        }
      }
    }

    if (!hasCurrentMonthActivity || netThisMonth === 0) {
      return null;
    }

    const prevMonthEndBalance = totalBalance - netThisMonth;
    const isPositive = netThisMonth > 0;
    const sign = isPositive ? '+' : '-';

    let pctText = '';
    if (prevMonthEndBalance > 0) {
      const pct = Math.abs((netThisMonth / prevMonthEndBalance) * 100);
      pctText = `${sign}${pct.toFixed(1)}%`;
    } else {
      pctText = `${sign}100%`;
    }

    const amountFormatted = formatMoney(Math.abs(netThisMonth), locale);
    const amountText = `(${sign}${amountFormatted})`;
    const vsText = locale === 'vi-VN' ? 'so với tháng trước' : 'vs last month';

    return {
      isPositive,
      text: `${pctText} ${vsText} ${amountText}`,
    };
  }, [transactions, totalBalance, locale]);

  const filteredBankTransactions = useMemo(() => {
    if (bankTxFilter === 'ALL') return bankTransactions;
    return bankTransactions.filter((tx) => tx.direction === bankTxFilter);
  }, [bankTransactions, bankTxFilter]);

  const recentInternalActivities = useMemo(() => {
    return transactions.slice(0, 5).map((tx) => {
      let actType: 'in' | 'out' | 'internal' = 'out';
      if (
        tx.type === 'deposit' ||
        tx.type === 'income' ||
        tx.type === 'transfer_in' ||
        tx.type === 'goal_withdrawal' ||
        tx.amount > 0
      ) {
        actType = 'in';
      } else if (
        tx.type === 'transfer_out' ||
        tx.title.includes('Chuyển') ||
        tx.title.includes('Transfer') ||
        tx.type === 'goal_contribution'
      ) {
        actType = 'internal';
      }

      const displayTitle = locale === 'vi-VN' ? tx.title : tx.titleEn || tx.title;
      const targetLabel = tx.walletName || (locale === 'vi-VN' ? 'Ví SpendWise' : 'SpendWise Wallet');
      const formattedDate = formatDate(tx.date, locale);

      return {
        id: tx.id,
        title: displayTitle,
        target: targetLabel,
        amount: Math.abs(tx.amount),
        type: actType,
        date: formattedDate,
      };
    });
  }, [transactions, locale]);

  const showToast = (msg: string) => {
    setToastMessage(msg);
    setTimeout(() => setToastMessage(null), 3000);
  };

  // Handler: Open SWLINK Connect Bank flow
  const handleOpenConnectBank = async (_preselectedBankBrand?: string) => {
    setLinkChallengeModalState({
      isOpen: true,
      loading: true,
      challenge: null,
      timeLeft: 0,
      confirming: false,
      error: null,
    });

    const res = await startBankLink();
    if (res.success && res.challenge) {
      if (!res.challenge.expiresAt || isNaN(parseIsoUtcMs(res.challenge.expiresAt))) {
        setLinkChallengeModalState({
          isOpen: true,
          loading: false,
          challenge: null,
          timeLeft: 0,
          confirming: false,
          error:
            locale === 'vi-VN'
              ? 'Dữ liệu thời hạn mã liên kết từ máy chủ không hợp lệ.'
              : 'Invalid link challenge expiry from server.',
        });
        return;
      }
      const expiresAt = parseIsoUtcMs(res.challenge.expiresAt);
      const now = Date.now();
      const remainingSeconds = Math.floor((expiresAt - now) / 1000);
      const isExpired = remainingSeconds <= 0;

      setLinkChallengeModalState({
        isOpen: true,
        loading: false,
        challenge: res.challenge,
        timeLeft: Math.max(0, remainingSeconds),
        confirming: false,
        error: isExpired
          ? (locale === 'vi-VN' ? 'Mã liên kết đã hết hạn. Vui lòng tạo mã mới.' : 'Link challenge has expired. Please create a new one.')
          : null,
      });
    } else {
      setLinkChallengeModalState((prev) => ({
        ...prev,
        loading: false,
        error: res.error || (locale === 'vi-VN' ? 'Không thể khởi tạo mã liên kết ngân hàng.' : 'Cannot initialize bank link challenge.'),
      }));
    }
  };

  const handleConfirmLinkChallenge = async () => {
    if (!linkChallengeModalState.challenge) return;
    setLinkChallengeModalState((prev) => ({ ...prev, confirming: true, error: null }));
    const res = await confirmBankLink(linkChallengeModalState.challenge.challengeId);
    if (res.success) {
      setLinkChallengeModalState({
        isOpen: false,
        loading: false,
        challenge: null,
        timeLeft: 0,
        confirming: false,
        error: null,
      });
      showToast(
        locale === 'vi-VN'
          ? 'Liên kết tài khoản SePay thành công!'
          : 'Successfully linked bank account via SePay!'
      );
    } else {
      setLinkChallengeModalState((prev) => ({
        ...prev,
        confirming: false,
        error:
          res.error ||
          (locale === 'vi-VN'
            ? 'Chưa phát hiện giao dịch khớp với mã tham chiếu. Vui lòng chuyển khoản demo trên SePay rồi bấm xác nhận lại.'
            : 'Matching transaction not found yet.'),
      }));
    }
  };

  const handleCopyChallengeCode = () => {
    if (linkChallengeModalState.challenge?.reference) {
      navigator.clipboard?.writeText(linkChallengeModalState.challenge.reference);
      setIsChallengeCopied(true);
      setTimeout(() => setIsChallengeCopied(false), 2000);
    }
  };

  // Timer effect for Link Challenge
  useEffect(() => {
    if (!linkChallengeModalState.isOpen || !linkChallengeModalState.challenge || linkChallengeModalState.timeLeft <= 0) return;
    const interval = setInterval(() => {
      setLinkChallengeModalState((prev) => {
        if (prev.timeLeft <= 1) {
          clearInterval(interval);
          return {
            ...prev,
            timeLeft: 0,
            error:
              locale === 'vi-VN'
                ? 'Mã liên kết đã hết hạn. Vui lòng tạo mã mới.'
                : 'Link challenge has expired. Please create a new one.',
          };
        }
        return { ...prev, timeLeft: prev.timeLeft - 1 };
      });
    }, 1000);
    return () => clearInterval(interval);
  }, [linkChallengeModalState.isOpen, linkChallengeModalState.challenge, locale]);

  // Handler: Unlink bank connection
  const handleStartUnlinkConnection = async (conn: BankConnection) => {
    const isTestMode = conn.accountEnvironment === 'TestMode';
    const redirectUri = `${window.location.origin}/wallet`;
    const lang = locale === 'vi-VN' ? 'vi' : 'en';

    const res = await unlinkBankConnection(
      conn.id,
      !isTestMode ? { completionRedirectUri: redirectUri, language: lang } : undefined
    );

    if (res.success) {
      if (isTestMode) {
        showToast(
          locale === 'vi-VN'
            ? `Đã hủy liên kết tài khoản ${conn.bankBrandName} qua SePay.`
            : `Unlinked ${conn.bankBrandName} connection via SePay.`
        );
      } else {
        const hostedUrl = res.session?.hostedLinkUrl?.trim();
        if (hostedUrl) {
          window.location.href = hostedUrl;
        } else {
          showToast(
            locale === 'vi-VN'
              ? 'Không nhận được đường dẫn liên kết từ ngân hàng. Vui lòng thử lại.'
              : 'Failed to receive provider authorization link. Please try again.'
          );
        }
      }
    } else {
      showToast(
        getLocalizedErrorMessage(
          res.error,
          locale,
          t,
          locale === 'vi-VN'
            ? 'Không thể hủy liên kết ngân hàng. Vui lòng thử lại.'
            : 'Cannot unlink bank connection'
        )
      );
    }
  };

  // Handler: Reactivate bank connection
  const handleStartReactivateConnection = async (conn: BankConnection) => {
    const redirectUri = `${window.location.origin}/wallet`;
    const lang = locale === 'vi-VN' ? 'vi' : 'en';

    const res = await reactivateBankConnection(conn.id, {
      completionRedirectUri: redirectUri,
      language: lang,
    });

    if (res.success) {
      const hostedUrl = res.session?.hostedLinkUrl?.trim();
      if (hostedUrl) {
        window.location.href = hostedUrl;
      } else {
        showToast(
          locale === 'vi-VN'
            ? 'Không nhận được đường dẫn liên kết từ ngân hàng. Vui lòng thử lại.'
            : 'Failed to receive provider authorization link. Please try again.'
        );
      }
    } else {
      showToast(
        getLocalizedErrorMessage(
          res.error,
          locale,
          t,
          locale === 'vi-VN'
            ? 'Không thể kết nối lại ngân hàng. Vui lòng thử lại.'
            : 'Cannot reactivate bank connection'
        )
      );
    }
  };

  // Handler: Sync Bank Connection (Active only)
  const handleSyncConnection = async (connId: string, bankBrandName: string) => {
    const res = await syncBankConnection(connId);
    if (res.success) {
      showToast(
        locale === 'vi-VN'
          ? `Đã đồng bộ giao dịch mới nhất từ ${bankBrandName} qua SePay! (${res.importedCount || 0} giao dịch mới)`
          : `Synced latest transactions from ${bankBrandName} via SePay! (${res.importedCount || 0} new)`
      );
    } else {
      showToast(
        getLocalizedErrorMessage(
          res.error,
          locale,
          t,
          locale === 'vi-VN'
            ? 'Không thể đồng bộ tài khoản ngân hàng. Vui lòng thử lại.'
            : 'Unable to sync the bank connection.'
        )
      );
    }
  };

  // Handler: Open SWTP Top-Up Modal
  const handleOpenTopUpModal = async (preselectedWalletId?: string) => {
    const { testModeLinkedAccounts: freshLinked } = await refreshBankConnections();
    const availableLinked = (freshLinked || []).filter((a) => a.status === 'Active');
    if (availableLinked.length === 0) {
      showToast(
        locale === 'vi-VN'
          ? 'Chưa có tài khoản ngân hàng thử nghiệm (TestMode). Vui lòng liên kết tài khoản ngân hàng TestMode trước khi nạp tiền.'
          : 'No active TestMode bank account. Please link a TestMode bank account before topping up.'
      );
      return;
    }

    // Check if there is an active pending top-up to resume (freshly fetched)
    const freshTopUps = await refreshWalletTopUps();
    const activeTopUp = (freshTopUps || []).find(
      (t) =>
        (t.status === 'Pending' || t.status === 'Processing') &&
        (!t.expiresAt || parseIsoUtcMs(t.expiresAt) > Date.now())
    );

    if (activeTopUp) {
      const expiresAt = activeTopUp.expiresAt ? parseIsoUtcMs(activeTopUp.expiresAt) : 0;
      const remainingSeconds = expiresAt ? Math.max(0, Math.floor((expiresAt - Date.now()) / 1000)) : 0;
      setTopUpModalState({
        isOpen: true,
        step: 'challenge',
        bankConnectionId: activeTopUp.bankConnectionId,
        walletId: activeTopUp.walletId,
        amount: String(activeTopUp.amount),
        loading: false,
        topUp: activeTopUp,
        timeLeft: remainingSeconds,
        confirming: false,
        error: null,
      });
      return;
    }

    const defaultWallet =
      wallets.find((w) => w.id === preselectedWalletId) ||
      wallets.find((w) => w.isDefault) ||
      wallets[0];

    const initialBankConnId = availableLinked[0].bankConnectionId;

    setTopUpModalState({
      isOpen: true,
      step: 'create',
      bankConnectionId: initialBankConnId,
      walletId: defaultWallet?.id || '',
      amount: '50000',
      loading: false,
      topUp: null,
      timeLeft: 0,
      confirming: false,
      error: null,
    });
  };

  const handleCreateTopUpSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    const { valid, value: parsedAmount } = parseSafePositiveInteger(topUpModalState.amount);
    if (!valid || parsedAmount < 10000) {
      setTopUpModalState((prev) => ({
        ...prev,
        error: locale === 'vi-VN' ? 'Số tiền nạp tối thiểu là 10.000 ₫' : 'Minimum top-up amount is 10,000 ₫',
      }));
      return;
    }
    if (!topUpModalState.bankConnectionId) {
      setTopUpModalState((prev) => ({
        ...prev,
        error: locale === 'vi-VN' ? 'Vui lòng chọn tài khoản ngân hàng' : 'Please select bank account',
      }));
      return;
    }
    if (!topUpModalState.walletId) {
      setTopUpModalState((prev) => ({
        ...prev,
        error: locale === 'vi-VN' ? 'Vui lòng chọn ví nhận tiền' : 'Please select target wallet',
      }));
      return;
    }

    setTopUpModalState((prev) => ({ ...prev, loading: true, error: null }));
    const { testModeLinkedAccounts: freshLinked } = await refreshBankConnections();
    const matchingConn = (freshLinked || []).find(
      (a) => a.bankConnectionId === topUpModalState.bankConnectionId
    );
    if (!matchingConn || matchingConn.status !== 'Active') {
      setTopUpModalState((prev) => ({
        ...prev,
        loading: false,
        error:
          locale === 'vi-VN'
            ? 'Tài khoản ngân hàng thử nghiệm (TestMode) đã chọn không còn hoạt động hoặc không tồn tại. Vui lòng liên kết lại hoặc chọn tài khoản khác.'
            : 'Selected TestMode bank account is not active or does not exist. Please relink or select another account.',
      }));
      return;
    }

    const res = await createWalletTopUp({
      bankConnectionId: topUpModalState.bankConnectionId,
      walletId: topUpModalState.walletId,
      amount: parsedAmount,
    });

    if (res.success && res.topUp) {
      if (!res.topUp.expiresAt || isNaN(parseIsoUtcMs(res.topUp.expiresAt))) {
        setTopUpModalState((prev) => ({
          ...prev,
          loading: false,
          error:
            locale === 'vi-VN'
              ? 'Dữ liệu thời hạn nạp tiền từ máy chủ không hợp lệ.'
              : 'Invalid top-up expiry from server.',
        }));
        return;
      }
      const expiresAt = parseIsoUtcMs(res.topUp.expiresAt);
      const now = Date.now();
      const remainingSeconds = Math.floor((expiresAt - now) / 1000);
      const isExpired = remainingSeconds <= 0;

      setTopUpModalState((prev) => ({
        ...prev,
        step: 'challenge',
        loading: false,
        topUp: res.topUp!,
        timeLeft: Math.max(0, remainingSeconds),
        confirming: false,
        error: isExpired
          ? (locale === 'vi-VN' ? 'Yêu cầu nạp tiền đã hết hạn. Vui lòng tạo yêu cầu mới.' : 'Top-up request has expired. Please create a new one.')
          : null,
      }));
    } else if (res.code === 'wallet_top_up_active_exists' && res.activeTopUp) {
      const active = res.activeTopUp;
      const expiresAt = active.expiresAt ? parseIsoUtcMs(active.expiresAt) : 0;
      const remainingSeconds = expiresAt ? Math.max(0, Math.floor((expiresAt - Date.now()) / 1000)) : 0;
      setTopUpModalState((prev) => ({
        ...prev,
        step: 'challenge',
        loading: false,
        topUp: active,
        timeLeft: remainingSeconds,
        confirming: false,
        error:
          locale === 'vi-VN'
            ? 'Bạn đang có một yêu cầu nạp tiền đang chờ xử lý. Vui lòng hoàn tất hoặc xác nhận giao dịch này.'
            : 'You already have an active pending top-up. Please complete or confirm this transaction.',
      }));
    } else {
      setTopUpModalState((prev) => ({
        ...prev,
        loading: false,
        error: res.error || (locale === 'vi-VN' ? 'Không thể tạo yêu cầu nạp tiền.' : 'Failed to create top-up.'),
      }));
    }
  };

  const handleConfirmTopUp = async () => {
    if (!topUpModalState.topUp) return;
    setTopUpModalState((prev) => ({ ...prev, confirming: true, error: null }));
    const res = await confirmWalletTopUp(topUpModalState.topUp.id);

    // If API failed to reach backend or returned error without topUp payload
    if (!res.success || !res.topUp) {
      setTopUpModalState((prev) => ({
        ...prev,
        confirming: false,
        error:
          res.error ||
          (locale === 'vi-VN'
            ? 'Không thể kiểm tra giao dịch nạp tiền. Vui lòng thử lại sau.'
            : 'Unable to check top-up transaction. Please try again.'),
      }));
      return;
    }

    // Always use the returned backend WalletTopUpDto as authoritative state
    const currentTopUp = res.topUp;
    const status = currentTopUp.status;

    let remainingSeconds = 0;
    const topUpExpiresMs = parseIsoUtcMs(currentTopUp.expiresAt);
    if (!isNaN(topUpExpiresMs)) {
      remainingSeconds = Math.max(0, Math.floor((topUpExpiresMs - Date.now()) / 1000));
    }

    if (status === 'Completed') {
      setTopUpModalState({
        isOpen: false,
        step: 'create',
        bankConnectionId: '',
        walletId: '',
        amount: '50000',
        loading: false,
        topUp: null,
        timeLeft: 0,
        confirming: false,
        error: null,
      });
      showToast(
        locale === 'vi-VN'
          ? `Nạp ${formatMoney(currentTopUp.amount, locale)} vào ví thành công!`
          : `Top-up of ${formatMoney(currentTopUp.amount, locale)} successful!`
      );
    } else if (status === 'Pending' || status === 'Processing') {
      setTopUpModalState((prev) => ({
        ...prev,
        confirming: false,
        topUp: currentTopUp,
        timeLeft: remainingSeconds,
        error:
          locale === 'vi-VN'
            ? 'Giao dịch chưa được đối soát trên hệ thống SePay. Vui lòng kiểm tra lại sau ít phút hoặc thử lại.'
            : 'Transaction has not been reconciled on SePay yet. Please check again in a few moments.',
      }));
    } else if (status === 'Expired' || remainingSeconds <= 0) {
      setTopUpModalState((prev) => ({
        ...prev,
        confirming: false,
        topUp: currentTopUp,
        timeLeft: 0,
        error:
          locale === 'vi-VN'
            ? 'Yêu cầu nạp tiền đã hết hạn. Vui lòng đóng và tạo yêu cầu nạp mới.'
            : 'Top-up request has expired. Please create a new top-up request.',
      }));
    } else if (status === 'Failed') {
      setTopUpModalState((prev) => ({
        ...prev,
        confirming: false,
        topUp: currentTopUp,
        timeLeft: 0,
        error:
          res.error ||
          (locale === 'vi-VN'
            ? 'Giao dịch nạp tiền thất bại hoặc bị từ chối. Vui lòng tạo yêu cầu nạp mới.'
            : 'Top-up transaction failed. Please create a new request.'),
      }));
    } else {
      setTopUpModalState((prev) => ({
        ...prev,
        confirming: false,
        topUp: currentTopUp,
        timeLeft: remainingSeconds,
        error:
          locale === 'vi-VN'
            ? `Trạng thái giao dịch: ${status}. Vui lòng kiểm tra lại.`
            : `Top-up status: ${status}. Please verify.`,
      }));
    }
  };

  const handleCopyTopUpReference = () => {
    if (topUpModalState.topUp?.reference) {
      navigator.clipboard?.writeText(topUpModalState.topUp.reference);
      setIsTopUpRefCopied(true);
      setTimeout(() => setIsTopUpRefCopied(false), 2000);
    }
  };

  // Timer effect for Top-Up Challenge
  useEffect(() => {
    if (!topUpModalState.isOpen || topUpModalState.step !== 'challenge' || topUpModalState.timeLeft <= 0) return;
    const interval = setInterval(() => {
      setTopUpModalState((prev) => {
        if (prev.timeLeft <= 1) {
          clearInterval(interval);
          return {
            ...prev,
            timeLeft: 0,
            error: locale === 'vi-VN' ? 'Yêu cầu nạp tiền đã hết hạn. Vui lòng tạo yêu cầu mới.' : 'Top-up request has expired. Please create a new one.',
          };
        }
        return { ...prev, timeLeft: prev.timeLeft - 1 };
      });
    }, 1000);
    return () => clearInterval(interval);
  }, [topUpModalState.isOpen, topUpModalState.step, locale]);

  // Handler: Transfer between internal wallets
  const handleTransferSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    const { valid, value: parsedAmount } = parseSafePositiveInteger(actionAmount);
    if (!valid || parsedAmount <= 0) {
      setModalError(locale === 'vi-VN' ? 'Vui lòng nhập số tiền hợp lệ (> 0 ₫)' : 'Please enter a valid amount (> 0 ₫)');
      return;
    }

    const securityPin = await challengePinWithValue({
      actionName: locale === 'vi-VN' ? 'Chuyển tiền giữa các ví' : 'Transfer Between Wallets',
      amount: parsedAmount,
    });
    if (!securityPin) return;

    const res = await transferBetweenWallets({
      sourceWalletId,
      destWalletId: targetWalletId,
      amount: parsedAmount,
      securityPin,
    });
    if (res.success) {
      setIsTransferOpen(false);
      showToast(
        locale === 'vi-VN'
          ? `Đã chuyển ${formatMoney(parsedAmount, locale)} giữa các ví thành công!`
          : `Internal transfer of ${formatMoney(parsedAmount, locale)} successful!`
      );
    } else {
      setModalError(
        getLocalizedErrorMessage(
          res.error,
          locale,
          t,
          locale === 'vi-VN'
            ? 'Không thể chuyển tiền giữa các ví.'
            : 'Unable to transfer between wallets. Please try again.'
        )
      );
    }
  };

  // Handler: Add internal wallet
  const handleAddWalletSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (wallets.length >= 3) {
      setModalError(
        locale === 'vi-VN'
          ? 'Mỗi tài khoản chỉ được tạo tối đa 3 ví cá nhân (1 ví chính + 2 ví phụ).'
          : 'Maximum 3 personal wallets reached.'
      );
      return;
    }
    if (!newWalletName.trim()) {
      setModalError(locale === 'vi-VN' ? 'Vui lòng nhập tên ví' : 'Please enter wallet name');
      return;
    }
    if (newWalletType !== 'savings' && newWalletType !== 'emergency') {
      setModalError(
        locale === 'vi-VN'
          ? 'Loại ví bổ sung chỉ có thể là Tiết kiệm hoặc Dự phòng.'
          : 'Additional wallet type must be savings or emergency.'
      );
      return;
    }
    const res = await createWallet({
      name: newWalletName.trim(),
      type: newWalletType,
      initialBalance: 0,
    });
    if (res.success) {
      setIsAddWalletOpen(false);
      setNewWalletName('');
      showToast(
        locale === 'vi-VN'
          ? `Đã tạo ví "${newWalletName.trim()}" thành công!`
          : `Wallet "${newWalletName.trim()}" created successfully!`
      );
    } else {
      setModalError(
        getLocalizedErrorMessage(
          res.error,
          locale,
          t,
          locale === 'vi-VN'
            ? 'Không thể tạo ví.'
            : 'Unable to create the wallet. Please try again.'
        )
      );
    }
  };

  // Handler: Edit internal wallet
  const handleSaveEditWallet = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!editingWallet) return;
    if (!newWalletName.trim()) {
      setModalError(locale === 'vi-VN' ? 'Vui lòng nhập tên ví' : 'Please enter wallet name');
      return;
    }
    const res = await updateWallet(editingWallet.id, {
      name: newWalletName.trim(),
      type: newWalletType,
    });
    if (res.success) {
      setEditingWallet(null);
      showToast(locale === 'vi-VN' ? 'Đã cập nhật thông tin ví!' : 'Wallet updated successfully!');
    } else {
      setModalError(
        getLocalizedErrorMessage(
          res.error,
          locale,
          t,
          locale === 'vi-VN'
            ? 'Không thể cập nhật ví.'
            : 'Unable to update the wallet. Please try again.'
        )
      );
    }
  };

  // Handler: Set default wallet
  const handleSetDefault = async (walletId: string) => {
    const res = await setDefaultWallet(walletId);
    if (res.success) {
      showToast(locale === 'vi-VN' ? 'Đã đặt làm ví mặc định!' : 'Set as default wallet!');
    } else {
      showToast(
        getLocalizedErrorMessage(
          res.error,
          locale,
          t,
          locale === 'vi-VN'
            ? 'Không thể đặt ví mặc định.'
            : 'Unable to set the default wallet. Please try again.'
        )
      );
    }
  };

  // Handler: Close wallet
  const handleConfirmCloseWallet = async () => {
    if (!closingWallet) return;
    const res = await closeWallet(closingWallet.id);
    if (res.success) {
      setClosingWallet(null);
      setCloseWalletError(null);
      showToast(
        locale === 'vi-VN'
          ? `Đã đóng ví "${closingWallet.name}" thành công!`
          : `Closed wallet "${closingWallet.name}"!`
      );
    } else {
      setCloseWalletError(
        getLocalizedErrorMessage(
          res.error,
          locale,
          t,
          locale === 'vi-VN'
            ? 'Không thể đóng ví.'
            : 'Unable to close the wallet. Please try again.'
        )
      );
    }
  };

  return (
    <div id="wallet-page" className="space-y-8 animate-in fade-in-50 duration-200">
      {/* Toast Notification */}
      {toastMessage && (
        <div className="fixed top-6 right-6 z-50 flex items-center gap-2 px-4 py-3 rounded-2xl bg-slate-900 text-white shadow-xl border border-emerald-500/30 text-xs font-semibold animate-in slide-in-from-top-3">
          <CheckCircle2 className="h-4 w-4 text-emerald-400 shrink-0" />
          <span>{toastMessage}</span>
        </div>
      )}

      {/* Page Header */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <h2 className="text-xl sm:text-2xl font-bold tracking-tight text-slate-900 dark:text-white">
            {t('wallet.pageTitle')}
          </h2>
          <p className="text-xs sm:text-sm text-slate-500 dark:text-slate-400 mt-0.5">
            {t('wallet.pageSubtitle')}
          </p>
        </div>

        <div className="flex flex-wrap items-center gap-2">
          <Button
            variant="outline"
            size="sm"
            onClick={() => handleOpenTopUpModal()}
            className="gap-1.5 text-xs border-blue-500/30 text-blue-700 dark:text-blue-300 hover:bg-blue-50 dark:hover:bg-blue-950/40 cursor-pointer"
          >
            <ArrowDownLeft className="h-4 w-4 text-blue-600 dark:text-blue-400" />
            <span>{locale === 'vi-VN' ? 'Nạp tiền vào ví' : 'Top Up Wallet'}</span>
          </Button>

          <Button
            variant="outline"
            size="sm"
            onClick={() => handleOpenConnectBank()}
            className="gap-1.5 text-xs border-emerald-500/30 text-emerald-700 dark:text-emerald-300 hover:bg-emerald-50 dark:hover:bg-emerald-950/40 cursor-pointer"
          >
            <Link2 className="h-4 w-4 text-emerald-600 dark:text-emerald-400" />
            <span>{t('wallet.linkBankBtn')}</span>
          </Button>

          <Button
            variant="outline"
            size="sm"
            disabled={wallets.length >= 3}
            onClick={() => {
              if (wallets.length >= 3) {
                showToast(
                  locale === 'vi-VN'
                    ? 'Đã đạt giới hạn tối đa 3 ví cá nhân (1 ví chính + 2 ví phụ).'
                    : 'Maximum 3 personal wallets reached.'
                );
                return;
              }
              setModalError(null);
              setNewWalletName('');
              setNewWalletType('savings');
              setIsAddWalletOpen(true);
            }}
            title={
              wallets.length >= 3
                ? locale === 'vi-VN'
                  ? 'Đã đạt giới hạn tối đa 3 ví cá nhân (1 ví chính + 2 ví phụ)'
                  : 'Maximum 3 personal wallets reached'
                : undefined
            }
            className={cn(
              'gap-1.5 text-xs border-slate-200 dark:border-slate-700 hover:bg-slate-50 dark:hover:bg-slate-800',
              wallets.length >= 3 ? 'opacity-50 cursor-not-allowed' : 'cursor-pointer'
            )}
          >
            <Plus className="h-4 w-4" />
            <span>{t('wallet.addWallet')}</span>
            {wallets.length >= 3 && <span className="text-[10px] text-amber-500 font-bold ml-1">(3/3)</span>}
          </Button>
        </div>
      </div>

      {/* Wallet Total Balance Summary Card */}
      <div className="relative overflow-hidden rounded-3xl bg-white dark:bg-[#0F172A] text-slate-900 dark:text-white p-6 sm:p-8 shadow-sm border border-slate-100 dark:border-slate-800">
        <div className="relative z-10 flex flex-col md:flex-row md:items-center justify-between gap-6">
          <div className="space-y-2">
            <div className="flex items-center gap-2 text-xs font-semibold text-slate-500 dark:text-slate-400 uppercase tracking-wider">
              <span>{t('wallet.totalAvailable')}</span>
              <button
                type="button"
                onClick={toggleVisibility}
                className="p-1 text-slate-400 hover:text-slate-700 dark:hover:text-white transition-colors cursor-pointer"
                title={isHidden ? t('common.showBalance') : t('common.hideBalance')}
              >
                {isHidden ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
              </button>
            </div>
            <div className="text-3xl sm:text-4xl font-bold tracking-tight text-slate-900 dark:text-white tabular-nums">
              {isHidden ? '••••••••' : formatMoney(totalBalance, locale)}
            </div>
            {balanceChange && (
              <div
                className={`flex items-center gap-2 text-xs font-medium ${
                  balanceChange.isPositive
                    ? 'text-emerald-600 dark:text-emerald-400'
                    : 'text-rose-600 dark:text-rose-400'
                }`}
              >
                {balanceChange.isPositive ? (
                  <TrendingUp className="h-3.5 w-3.5" />
                ) : (
                  <TrendingDown className="h-3.5 w-3.5" />
                )}
                <span>{balanceChange.text}</span>
              </div>
            )}
          </div>

          {/* Action Buttons: Top Up, Internal Transfer & Link Bank */}
          <div className="flex flex-wrap items-center gap-3">
            <button
              type="button"
              onClick={() => handleOpenTopUpModal()}
              className="flex items-center gap-2 px-4 py-2.5 rounded-2xl bg-blue-600 hover:bg-blue-500 text-white text-xs font-bold shadow-xs transition-colors cursor-pointer"
            >
              <ArrowDownLeft className="h-4 w-4" />
              <span>{locale === 'vi-VN' ? 'Nạp tiền' : 'Top Up'}</span>
            </button>

            <button
              type="button"
              onClick={() => {
                setModalError(null);
                setActionAmount('500000');
                const defId = wallets.find((w) => w.isDefault)?.id || wallets[0]?.id || '';
                const targetId = wallets.find((w) => w.id !== defId)?.id || wallets[1]?.id || '';
                setSourceWalletId(defId);
                setTargetWalletId(targetId);
                setIsTransferOpen(true);
              }}
              className="flex items-center gap-2 px-4 py-2.5 rounded-2xl bg-slate-900 hover:bg-slate-800 text-white dark:bg-white dark:text-slate-900 dark:hover:bg-slate-100 text-xs font-bold shadow-xs transition-colors cursor-pointer"
            >
              <ArrowLeftRight className="h-4 w-4" />
              <span>{t('wallet.transferBetween')}</span>
            </button>

            <button
              type="button"
              onClick={() => handleOpenConnectBank()}
              className="flex items-center gap-2 px-4 py-2.5 rounded-2xl bg-emerald-600 hover:bg-emerald-700 text-white text-xs font-bold transition-colors cursor-pointer"
            >
              <Link2 className="h-4 w-4" />
              <span>{t('wallet.linkBankBtn')}</span>
            </button>
          </div>
        </div>

        {/* Background accent glow */}
        <div className="absolute -bottom-16 -right-16 w-64 h-64 bg-emerald-500/10 rounded-full blur-3xl pointer-events-none" />
      </div>

      {/* SECTION 1: Internal SpendWise Wallets */}
      <div className="space-y-4">
        <div className="flex items-center justify-between">
          <h3 className="text-sm font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400 flex items-center gap-2">
            <Wallet className="h-4 w-4 text-emerald-500" />
            <span>{locale === 'vi-VN' ? 'Ví nội bộ SpendWise' : 'SpendWise Internal Wallets'}</span>
          </h3>
          <span className="text-xs text-slate-400">
            {t('wallet.accountsCount', { count: wallets.length })}
          </span>
        </div>

        {isLoading && wallets.length === 0 ? (
          <div className="grid grid-cols-1 md:grid-cols-3 gap-5">
            {[1, 2, 3].map((i) => (
              <div
                key={`wallet-skel-${i}`}
                className="rounded-3xl p-6 bg-white dark:bg-slate-900 border border-slate-100 dark:border-slate-800 shadow-sm flex flex-col justify-between min-h-56"
              >
                <div>
                  <div className="flex items-start justify-between gap-2 mb-3">
                    <Skeleton className="h-4 w-28" />
                    <Skeleton className="h-5 w-16 rounded-full" />
                  </div>
                  <Skeleton className="h-6 w-36 mb-1" />
                  <Skeleton className="h-3 w-20" />
                </div>
                <div className="pt-4 border-t border-slate-100 dark:border-slate-800/80 flex items-end justify-between">
                  <div>
                    <Skeleton className="h-3 w-16 mb-1.5" />
                    <Skeleton className="h-6 w-28" />
                  </div>
                  <Skeleton className="h-7 w-14 rounded-lg" />
                </div>
              </div>
            ))}
          </div>
        ) : wallets.length === 0 ? (
          <div className="bg-white dark:bg-slate-900 rounded-3xl p-10 text-center border border-slate-100 dark:border-slate-800 shadow-sm space-y-4">
            <div className="h-12 w-12 rounded-2xl bg-emerald-100 dark:bg-emerald-950 text-emerald-600 dark:text-emerald-400 flex items-center justify-center mx-auto">
              <CreditCard className="h-6 w-6" />
            </div>
            <p className="text-xs text-slate-400 max-w-sm mx-auto">
              {t('wallet.noWalletsDesc')}
            </p>
            <Button
              size="sm"
              onClick={() => {
                setNewWalletName('');
                setNewWalletType('savings');
                setIsAddWalletOpen(true);
              }}
              className="bg-emerald-600 hover:bg-emerald-700 text-white font-bold gap-1.5 cursor-pointer"
            >
              <Plus className="h-4 w-4" />
              <span>{t('wallet.addWallet')}</span>
            </Button>
          </div>
        ) : (
          <div className="grid grid-cols-1 md:grid-cols-3 gap-5">
            {wallets.map((wallet) => {
              const translatedName = getLocalizedWalletName(wallet.id, wallet.name, locale);

              return (
                <div
                  key={wallet.id}
                  className={`relative rounded-3xl p-6 bg-gradient-to-br ${wallet.bgGradient} text-white shadow-sm border border-white/10 flex flex-col justify-between min-h-56 transition-transform hover:-translate-y-1`}
                >
                  <div>
                    <div className="flex items-start justify-between gap-2 mb-2">
                      <div>
                        {wallet.accountNumber && (
                          <span className="text-[11px] font-semibold uppercase tracking-wider text-slate-400">
                            {wallet.accountNumber}
                          </span>
                        )}
                        <h3 className="text-base font-bold text-white mt-0.5">{translatedName}</h3>
                      </div>
                      <div className="flex items-center gap-1.5">
                        {wallet.isDefault && (
                          <span className="px-2 py-0.5 rounded-full bg-emerald-500/20 text-emerald-300 text-[10px] font-bold border border-emerald-500/30">
                            {t('wallet.defaultBadge')}
                          </span>
                        )}
                        <button
                          type="button"
                          onClick={() => {
                            setEditingWallet(wallet);
                            setNewWalletName(wallet.name);
                            setNewWalletType(wallet.type);
                            setModalError(null);
                          }}
                          className="p-1 rounded-lg text-slate-300 hover:text-white hover:bg-white/10 transition-colors cursor-pointer"
                          title={t('wallet.editWallet')}
                        >
                          <Edit3 className="h-3.5 w-3.5" />
                        </button>
                        {!wallet.isDefault && (
                          <button
                            type="button"
                            onClick={() => {
                              setClosingWallet(wallet);
                              setCloseWalletError(null);
                            }}
                            className="p-1 rounded-lg text-slate-300 hover:text-rose-300 hover:bg-white/10 transition-colors cursor-pointer"
                            title={t('wallet.closeWallet')}
                          >
                            <Trash2 className="h-3.5 w-3.5" />
                          </button>
                        )}
                      </div>
                    </div>
                  </div>

                  <div className="pt-6">
                    <span className="text-xs text-slate-400 block mb-1">
                      {t('wallet.currentBalance')}
                    </span>
                    <span className="text-2xl font-bold tracking-tight text-white tabular-nums block">
                      {isHidden ? '••••••••' : formatMoney(wallet.balance, locale)}
                    </span>

                    {!wallet.isDefault && (
                      <button
                        type="button"
                        onClick={() => handleSetDefault(wallet.id)}
                        className="mt-4 text-[11px] font-semibold text-slate-300 hover:text-white flex items-center gap-1 transition-colors cursor-pointer"
                      >
                        <span>{t('wallet.setDefault')}</span>
                        <ChevronRight className="h-3 w-3" />
                      </button>
                    )}
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </div>

      {/* SECTION 2 & 3: BALANCED WORKSPACE (Layout A) */}
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 items-start">
        {/* Left Column (6 cols): Linked Bank Accounts (SePay Bank Hub) */}
        <div className="lg:col-span-6 bg-white dark:bg-slate-900 rounded-2xl p-6 border border-slate-100 dark:border-slate-800 shadow-sm space-y-5">
          <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
            <div>
              <div className="flex items-center gap-2.5 flex-wrap">
                <Building2 className="h-5 w-5 text-emerald-600 dark:text-emerald-400 shrink-0" />
                <h3 className="text-lg font-bold text-slate-900 dark:text-white tracking-tight">
                  {t('wallet.linkedBanksTitle')}
                </h3>
                <span className="px-2.5 py-0.5 rounded-full bg-emerald-50 dark:bg-emerald-950/60 text-emerald-700 dark:text-emerald-300 text-[11px] font-semibold border border-emerald-200/80 dark:border-emerald-800/80">
                  SePay Open Banking
                </span>
              </div>
              <p className="text-xs text-slate-500 dark:text-slate-400 mt-1 leading-normal">
                {t('wallet.linkedBanksSubtitle')}
              </p>
            </div>

            <Button
              size="sm"
              onClick={() => handleOpenConnectBank()}
              className="bg-emerald-600 hover:bg-emerald-700 text-white font-bold text-xs gap-1.5 cursor-pointer rounded-xl shadow-sm shrink-0"
            >
              <Plus className="h-3.5 w-3.5" />
              <span>{t('wallet.linkBankBtn')}</span>
            </Button>
          </div>

          {/* Soft Information Panel */}
          <div className="p-3.5 rounded-2xl bg-emerald-50/40 dark:bg-emerald-950/20 border border-emerald-200/70 dark:border-emerald-800/50 flex items-start gap-3">
            <div className="h-7 w-7 rounded-lg bg-emerald-100/70 dark:bg-emerald-900/40 text-emerald-700 dark:text-emerald-400 flex items-center justify-center shrink-0 mt-0.5">
              <ShieldCheck className="h-4 w-4" />
            </div>
            <div className="space-y-0.5 min-w-0 flex-1">
              <h4 className="text-xs font-bold text-emerald-950 dark:text-emerald-300">
                {locale === 'vi-VN' ? 'Nguyên tắc an toàn & riêng tư' : 'Security Policy'}
              </h4>
              <p className="text-[11px] text-slate-700 dark:text-slate-300 leading-relaxed font-normal">
                {t('wallet.hostedLinkModalDesc')}
              </p>
            </div>
          </div>

          {/* Bank Connection List */}
          <div className="grid grid-cols-1 gap-3.5 pt-1">
            {bankConnections.length === 0 ? (
              <div className="py-8 text-center text-xs text-slate-400">
                {locale === 'vi-VN'
                  ? 'Chưa có tài khoản ngân hàng nào được liên kết qua SePay.'
                  : 'No bank accounts linked via SePay yet.'}
              </div>
            ) : (
              bankConnections.map((conn) => {
                const isActive = conn.status === 'Active';

                return (
                  <div
                    key={conn.id}
                    className={cn(
                      'flex flex-col justify-between p-4 rounded-2xl border transition-colors',
                      isActive
                        ? 'border-emerald-500/20 bg-emerald-500/5 dark:bg-emerald-950/20'
                        : 'border-slate-200 dark:border-slate-800 bg-slate-50/50 dark:bg-slate-800/30 opacity-80'
                    )}
                  >
                    <div className="space-y-2.5">
                      <div className="flex items-center justify-between">
                        <div className="flex items-center gap-2.5">
                          <div className="h-8 w-8 rounded-xl bg-slate-900 text-white font-bold flex items-center justify-center text-xs shrink-0 shadow-xs">
                            {conn.bankBrandName.slice(0, 3).toUpperCase()}
                          </div>
                          <div>
                            <p className="text-sm font-bold text-slate-900 dark:text-white leading-tight">
                              {conn.bankBrandName}
                            </p>
                            <p className="text-[11px] font-mono text-slate-400">
                              {conn.maskedAccountNumber}
                            </p>
                          </div>
                        </div>

                        <span
                          className={cn(
                            'text-[10px] font-bold px-2 py-0.5 rounded-full',
                            conn.status === 'Active'
                              ? 'bg-emerald-100 dark:bg-emerald-950 text-emerald-700 dark:text-emerald-300'
                              : conn.status === 'Unlinked'
                              ? 'bg-slate-200 dark:bg-slate-800 text-slate-600 dark:text-slate-400'
                              : conn.status === 'Pending'
                              ? 'bg-amber-100 dark:bg-amber-950 text-amber-700 dark:text-amber-300'
                              : conn.status === 'Error'
                              ? 'bg-rose-100 dark:bg-rose-950 text-rose-700 dark:text-rose-300'
                              : 'bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-400'
                          )}
                        >
                          {conn.status === 'Active'
                            ? (locale === 'vi-VN' ? 'Hoạt động' : 'Active')
                            : conn.status === 'Pending'
                            ? (locale === 'vi-VN' ? 'Đang chờ' : 'Pending')
                            : conn.status === 'Inactive'
                            ? (locale === 'vi-VN' ? 'Không hoạt động' : 'Inactive')
                            : conn.status === 'Unlinked'
                            ? (locale === 'vi-VN' ? 'Đã ngắt liên kết' : 'Unlinked')
                            : conn.status === 'Error'
                            ? (locale === 'vi-VN' ? 'Lỗi' : 'Error')
                            : conn.status}
                        </span>
                      </div>

                      <div className="space-y-0.5 text-[11px] text-slate-500 dark:text-slate-400">
                        <p>
                          <span className="font-semibold text-slate-700 dark:text-slate-300">
                            {conn.accountHolderName || (locale === 'vi-VN' ? 'Không có thông tin' : '—')}
                          </span>
                        </p>
                        <p className="text-[10px] text-slate-400">
                          {conn.accountEnvironment === 'TestMode' ? (
                            <span className="font-semibold text-amber-600 dark:text-amber-400">
                              {locale === 'vi-VN' ? 'Môi trường: Thử nghiệm (TestMode)' : 'Environment: TestMode'}
                            </span>
                          ) : (
                            <>
                              {locale === 'vi-VN' ? 'Đồng bộ lần cuối:' : 'Last synced:'}{' '}
                              {conn.lastSyncedAt ? formatDate(conn.lastSyncedAt, locale) : '—'}
                            </>
                          )}
                        </p>
                      </div>
                    </div>

                    {/* Actions for this connection */}
                    <div className="pt-3 mt-3 border-t border-slate-200/60 dark:border-slate-800 flex items-center justify-between gap-2">
                      {conn.accountEnvironment === 'TestMode' ? (
                        conn.status === 'Active' ? (
                          <>
                            <button
                              type="button"
                              onClick={() => handleOpenTopUpModal()}
                              className="flex items-center gap-1.5 text-xs font-semibold text-emerald-600 dark:text-emerald-400 hover:text-emerald-700 transition cursor-pointer"
                            >
                              <ArrowDownRight className="h-3.5 w-3.5" />
                              <span>{locale === 'vi-VN' ? 'Nạp tiền (Test)' : 'Top-up (Test)'}</span>
                            </button>
                            <button
                              type="button"
                              onClick={() => handleStartUnlinkConnection(conn)}
                              className="flex items-center gap-1.5 text-xs font-semibold text-rose-500 hover:text-rose-600 transition cursor-pointer"
                            >
                              <Unlink className="h-3.5 w-3.5" />
                              <span>{t('wallet.unlinkBankBtn')}</span>
                            </button>
                          </>
                        ) : (
                          <div className="w-full flex items-center justify-between gap-2">
                            <span className="text-[11px] text-slate-400 dark:text-slate-500 italic">
                              {conn.status === 'Unlinked'
                                ? locale === 'vi-VN'
                                  ? 'Đã ngắt kết nối'
                                  : 'Connection ended'
                                : conn.status === 'Pending'
                                ? locale === 'vi-VN'
                                  ? 'Đang chờ liên kết'
                                  : 'Pending verification'
                                : locale === 'vi-VN'
                                ? 'Không hoạt động'
                                : 'Inactive'}
                            </span>
                            <button
                              type="button"
                              onClick={() => handleOpenConnectBank(conn.bankBrandName)}
                              className="flex items-center gap-1.5 text-xs font-semibold text-blue-600 dark:text-blue-400 hover:text-blue-700 transition cursor-pointer"
                            >
                              <ExternalLink className="h-3.5 w-3.5" />
                              <span>{locale === 'vi-VN' ? 'Liên kết lại' : 'Link again'}</span>
                            </button>
                          </div>
                        )
                      ) : conn.status === 'Active' ? (
                        <>
                          <button
                            type="button"
                            onClick={() => handleSyncConnection(conn.id, conn.bankBrandName)}
                            className="flex items-center gap-1.5 text-xs font-semibold text-emerald-600 dark:text-emerald-400 hover:text-emerald-700 transition cursor-pointer"
                          >
                            <RefreshCw className="h-3.5 w-3.5" />
                            <span>{t('wallet.syncNowBtn')}</span>
                          </button>
                          <button
                            type="button"
                            onClick={() => handleStartUnlinkConnection(conn)}
                            className="flex items-center gap-1.5 text-xs font-semibold text-rose-500 hover:text-rose-600 transition cursor-pointer"
                          >
                            <Unlink className="h-3.5 w-3.5" />
                            <span>{t('wallet.unlinkBankBtn')}</span>
                          </button>
                        </>
                      ) : conn.status === 'Inactive' ? (
                        <button
                          type="button"
                          onClick={() => handleStartReactivateConnection(conn)}
                          className="flex items-center gap-1.5 text-xs font-semibold text-emerald-600 dark:text-emerald-400 hover:text-emerald-700 transition cursor-pointer"
                        >
                          <RefreshCw className="h-3.5 w-3.5" />
                          <span>{t('wallet.reactivateBankBtn')}</span>
                        </button>
                      ) : conn.status === 'Unlinked' ? (
                        <div className="w-full flex items-center justify-between gap-2">
                          <span className="text-[11px] text-slate-400 dark:text-slate-500 italic">
                            {locale === 'vi-VN' ? 'Đã ngắt kết nối' : 'Connection ended'}
                          </span>
                          <button
                            type="button"
                            onClick={() => handleOpenConnectBank(conn.bankBrandName)}
                            className="flex items-center gap-1.5 text-xs font-semibold text-blue-600 dark:text-blue-400 hover:text-blue-700 transition cursor-pointer"
                          >
                            <ExternalLink className="h-3.5 w-3.5" />
                            <span>{locale === 'vi-VN' ? 'Liên kết lại' : 'Link again'}</span>
                          </button>
                        </div>
                      ) : conn.status === 'Pending' ? (
                        <div className="w-full flex items-center justify-between gap-2">
                          <span className="text-xs text-amber-600 dark:text-amber-400 font-medium">
                            {locale === 'vi-VN' ? 'Đang xác thực liên kết...' : 'Verifying connection...'}
                          </span>
                          {linkChallengeModalState.challenge && (
                            <button
                              type="button"
                              onClick={() =>
                                setLinkChallengeModalState((prev) => ({
                                  ...prev,
                                  isOpen: true,
                                }))
                              }
                              className="text-xs font-semibold text-amber-700 dark:text-amber-300 hover:underline cursor-pointer"
                            >
                              {locale === 'vi-VN' ? 'Xem mã xác nhận' : 'View challenge code'}
                            </button>
                          )}
                        </div>
                      ) : (
                        <div className="w-full flex items-center justify-between gap-2">
                          <span className="text-xs text-rose-500 font-medium">
                            {locale === 'vi-VN' ? 'Lỗi kết nối' : 'Connection error'}
                          </span>
                          <button
                            type="button"
                            onClick={() => handleOpenConnectBank(conn.bankBrandName)}
                            className="text-xs font-semibold text-blue-600 dark:text-blue-400 hover:underline cursor-pointer"
                          >
                            {locale === 'vi-VN' ? 'Thử lại' : 'Retry'}
                          </button>
                        </div>
                      )}
                    </div>
                  </div>
                );
              })
            )}
          </div>
        </div>

        {/* Right Column (6 cols): External Bank Transactions Feed */}
        <div className="lg:col-span-6 bg-white dark:bg-slate-900 rounded-2xl p-6 border border-slate-100 dark:border-slate-800 shadow-sm space-y-4">
          <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
            <div>
              <h3 className="text-base font-bold text-slate-900 dark:text-white flex items-center gap-2">
                <Building2 className="h-4 w-4 text-emerald-500" />
                <span>{t('wallet.externalBankTxTitle')}</span>
              </h3>
              <p className="text-[11px] text-slate-400 mt-0.5">
                {t('wallet.externalBankTxSubtitle')}
              </p>
            </div>

            {/* Direction Filter Tabs */}
            <div className="flex items-center gap-1 bg-slate-100 dark:bg-slate-800 p-1 rounded-xl shrink-0">
              <button
                type="button"
                onClick={() => setBankTxFilter('ALL')}
                className={cn(
                  'px-2.5 py-1 rounded-lg text-[11px] font-bold transition cursor-pointer',
                  bankTxFilter === 'ALL'
                    ? 'bg-white dark:bg-slate-900 text-slate-900 dark:text-white shadow-xs'
                    : 'text-slate-500 hover:text-slate-900 dark:hover:text-white'
                )}
              >
                {t('wallet.filterAll')}
              </button>
              <button
                type="button"
                onClick={() => setBankTxFilter('Credit')}
                className={cn(
                  'px-2.5 py-1 rounded-lg text-[11px] font-bold transition cursor-pointer',
                  bankTxFilter === 'Credit'
                    ? 'bg-emerald-600 text-white shadow-xs'
                    : 'text-slate-500 hover:text-slate-900 dark:hover:text-white'
                )}
              >
                {t('wallet.filterCredit')}
              </button>
              <button
                type="button"
                onClick={() => setBankTxFilter('Debit')}
                className={cn(
                  'px-2.5 py-1 rounded-lg text-[11px] font-bold transition cursor-pointer',
                  bankTxFilter === 'Debit'
                    ? 'bg-slate-900 text-white dark:bg-slate-700 shadow-xs'
                    : 'text-slate-500 hover:text-slate-900 dark:hover:text-white'
                )}
              >
                {t('wallet.filterDebit')}
              </button>
            </div>
          </div>

          {/* Transactions list */}
          <div className="divide-y divide-slate-100 dark:divide-slate-800/80">
            {filteredBankTransactions.length === 0 ? (
              <p className="text-xs text-slate-400 py-8 text-center">
                {locale === 'vi-VN'
                  ? 'Chưa có biến động số dư ngân hàng nào phù hợp bộ lọc.'
                  : 'No bank transactions match this filter.'}
              </p>
            ) : (
              filteredBankTransactions.map((tx) => (
                <div
                  key={tx.id}
                  className="flex items-center justify-between py-3 px-2 -mx-2 hover:bg-slate-50/60 dark:hover:bg-slate-800/40 rounded-xl transition-colors"
                >
                  <div className="flex items-center gap-3">
                    <div
                      className={cn(
                        'h-10 w-10 rounded-full flex items-center justify-center shrink-0 font-bold',
                        tx.direction === 'Credit'
                          ? 'bg-emerald-100 text-emerald-600 dark:bg-emerald-950/60 dark:text-emerald-400'
                          : 'bg-slate-100 text-slate-600 dark:bg-slate-800 dark:text-slate-400'
                      )}
                    >
                      {tx.direction === 'Credit' ? (
                        <ArrowDownLeft className="h-4 w-4" />
                      ) : (
                        <ArrowUpRight className="h-4 w-4" />
                      )}
                    </div>
                    <div>
                      <div className="flex items-center gap-2">
                        <p className="text-xs font-bold text-slate-800 dark:text-slate-200">
                          {tx.bankBrandName || 'Bank'} • {tx.maskedAccountNumber || '••••'}
                        </p>
                        <span className="text-[9px] px-1.5 py-0.2 rounded-md bg-teal-50 dark:bg-teal-950/40 text-teal-600 dark:text-teal-400 font-semibold font-mono">
                          {tx.source}
                        </span>
                      </div>
                      <p className="text-[11px] text-slate-500 dark:text-slate-400 mt-0.5 line-clamp-1">
                        {tx.content}
                      </p>
                      <p className="text-[10px] text-slate-400 font-mono">
                        {formatDate(tx.transactionDate, locale)} • {tx.referenceCode || tx.paymentCode || tx.externalTransactionId}
                      </p>
                      <div className="flex items-center gap-1.5 mt-1">
                        <Tag className="h-3 w-3 text-slate-400" />
                        <span
                          className={cn(
                            'text-[10px] px-2 py-0.5 rounded-full font-medium',
                            tx.categoryId
                              ? 'bg-indigo-50 dark:bg-indigo-950/50 text-indigo-600 dark:text-indigo-400 border border-indigo-200/60 dark:border-indigo-800/60'
                              : 'bg-slate-100 dark:bg-slate-800 text-slate-500 italic'
                          )}
                        >
                          {tx.categoryName || (locale === 'vi-VN' ? 'Chưa phân loại' : 'Unclassified')}
                        </span>
                        <button
                          type="button"
                          onClick={() => setClassifyingBankTx(tx)}
                          className="text-[10px] text-indigo-600 dark:text-indigo-400 hover:underline font-semibold ml-1 cursor-pointer"
                        >
                          {tx.categoryId
                            ? locale === 'vi-VN'
                              ? 'Đổi danh mục'
                              : 'Change'
                            : locale === 'vi-VN'
                            ? 'Phân loại'
                            : 'Classify'}
                        </button>
                      </div>
                    </div>
                  </div>

                  <div className="text-right shrink-0">
                    <span
                      className={cn(
                        'text-xs font-bold tabular-nums',
                        tx.direction === 'Credit'
                          ? 'text-emerald-600 dark:text-emerald-400'
                          : 'text-slate-800 dark:text-slate-200'
                      )}
                    >
                      {tx.direction === 'Credit' ? '+' : '-'}
                      {isHidden ? '••••••••' : formatMoney(tx.amount, locale)}
                    </span>
                  </div>
                </div>
              ))
            )}
          </div>
        </div>

        {/* Recent SpendWise Internal Wallet Activities (5 cols) */}
        <div className="lg:col-span-5 bg-white dark:bg-slate-900 rounded-3xl p-6 border border-slate-100 dark:border-slate-800 shadow-sm space-y-4">
          <div className="flex items-center justify-between">
            <h3 className="text-base font-bold text-slate-800 dark:text-white flex items-center gap-2">
              <CreditCard className="h-4 w-4 text-teal-500" />
              <span>{t('wallet.recentActivity')}</span>
            </h3>
            <span className="text-xs font-semibold text-slate-400">
              {t('wallet.txCount', { count: recentInternalActivities.length })}
            </span>
          </div>

          <div className="divide-y divide-slate-100 dark:divide-slate-800/80">
            {recentInternalActivities.length === 0 ? (
              <p className="text-xs text-slate-400 py-6 text-center">
                {locale === 'vi-VN' ? 'Chưa có hoạt động giao dịch nội bộ.' : 'No internal wallet activity recorded.'}
              </p>
            ) : (
              recentInternalActivities.map((act) => (
                <div
                  key={act.id}
                  className="flex items-center justify-between py-2.5 px-2 -mx-2 hover:bg-slate-50/60 dark:hover:bg-slate-800/40 rounded-xl transition-colors"
                >
                  <div className="flex items-center gap-2.5">
                    <div
                      className={cn(
                        'h-8 w-8 rounded-full flex items-center justify-center shrink-0 font-bold',
                        act.type === 'in'
                          ? 'bg-emerald-100 text-emerald-600 dark:bg-emerald-950/60 dark:text-emerald-400'
                          : act.type === 'internal'
                          ? 'bg-sky-100 text-sky-600 dark:bg-sky-950/60 dark:text-sky-400'
                          : 'bg-slate-100 text-slate-600 dark:bg-slate-800 dark:text-slate-400'
                      )}
                    >
                      {act.type === 'in' ? (
                        <ArrowDownLeft className="h-3.5 w-3.5" />
                      ) : act.type === 'internal' ? (
                        <ArrowLeftRight className="h-3.5 w-3.5" />
                      ) : (
                        <ArrowUpRight className="h-3.5 w-3.5" />
                      )}
                    </div>
                    <div>
                      <p className="text-xs font-bold text-slate-800 dark:text-slate-200">
                        {act.title}
                      </p>
                      <p className="text-[10px] text-slate-400 mt-0.5">
                        {act.target} • {act.date}
                      </p>
                    </div>
                  </div>

                  <div className="text-right shrink-0">
                    <span
                      className={cn(
                        'text-xs font-bold tabular-nums',
                        act.type === 'in'
                          ? 'text-emerald-600 dark:text-emerald-400'
                          : act.type === 'internal'
                          ? 'text-sky-600 dark:text-sky-400'
                          : 'text-slate-800 dark:text-slate-200'
                      )}
                    >
                      {act.type === 'in' ? '+' : act.type === 'out' ? '-' : '⇄ '}
                      {isHidden ? '••••••••' : formatMoney(act.amount, locale)}
                    </span>
                  </div>
                </div>
              ))
            )}
          </div>
        </div>
      </div>

      {/* ========================================================================= */}
      {/* MODAL 1: SEPAY TEST MODE BANK LINK CHALLENGE (SWLINK)                      */}
      {/* ========================================================================= */}
      {linkChallengeModalState.isOpen && (
        <div
          role="dialog"
          aria-modal="true"
          className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/60 backdrop-blur-xs animate-in fade-in duration-150"
        >
          <div className="w-full max-w-md bg-white dark:bg-slate-900 rounded-3xl p-6 sm:p-7 border border-slate-100 dark:border-slate-800 shadow-2xl space-y-5 animate-in zoom-in-95 max-h-[90vh] overflow-y-auto">
            <div className="flex items-center justify-between">
              <div>
                <div className="flex items-center gap-2">
                  <h3 className="text-base font-bold text-slate-900 dark:text-white">
                    {locale === 'vi-VN' ? 'Liên kết tài khoản SePay Test Mode' : 'Link SePay Test Mode Account'}
                  </h3>
                  <span className="px-2 py-0.5 rounded-full bg-emerald-100 dark:bg-emerald-950/60 text-emerald-700 dark:text-emerald-300 text-[10px] font-bold border border-emerald-200 dark:border-emerald-800">
                    Test Mode (SWLINK)
                  </span>
                </div>
                <p className="text-[11px] text-slate-400 mt-0.5">
                  {locale === 'vi-VN'
                    ? 'Tạo mã SWLINK và xác nhận bằng giao dịch thử trên SePay Test Mode.'
                    : 'Create an SWLINK reference and confirm it using a SePay Test Mode transaction.'}
                </p>
              </div>
              <button
                type="button"
                onClick={() => setLinkChallengeModalState((prev) => ({ ...prev, isOpen: false }))}
                className="p-1.5 rounded-xl text-slate-400 hover:bg-slate-100 dark:hover:bg-slate-800 cursor-pointer"
              >
                <X className="h-4 w-4" />
              </button>
            </div>

            {linkChallengeModalState.error && (
              <div className="p-3 rounded-2xl bg-rose-50 dark:bg-rose-950/50 border border-rose-200 dark:border-rose-900/50 text-rose-600 dark:text-rose-400 text-xs flex items-center gap-2">
                <AlertCircle className="h-4 w-4 shrink-0" />
                <span>{linkChallengeModalState.error}</span>
              </div>
            )}

            {linkChallengeModalState.loading ? (
              <div className="py-8 flex flex-col items-center justify-center gap-3 text-slate-400 text-xs">
                <RefreshCw className="h-6 w-6 animate-spin text-emerald-500" />
                <span>{locale === 'vi-VN' ? 'Đang tạo mã liên kết từ máy chủ...' : 'Creating link challenge from server...'}</span>
              </div>
            ) : linkChallengeModalState.challenge ? (
              <div className="space-y-4">
                {/* Reference Code Card */}
                <div className="p-4 rounded-2xl bg-slate-50 dark:bg-slate-800/60 border border-slate-200/80 dark:border-slate-700/60 space-y-2 text-center">
                  <span className="text-[11px] font-medium text-slate-500 dark:text-slate-400 uppercase tracking-wider block">
                    {locale === 'vi-VN' ? 'Mã tham chiếu liên kết' : 'Link Reference Code'}
                  </span>
                  <div className="flex items-center justify-center gap-2">
                    <span className="font-mono text-xl sm:text-2xl font-bold tracking-wider text-emerald-600 dark:text-emerald-400 select-all">
                      {linkChallengeModalState.challenge.reference}
                    </span>
                    <button
                      type="button"
                      onClick={handleCopyChallengeCode}
                      className="p-2 rounded-xl bg-white dark:bg-slate-700 border border-slate-200 dark:border-slate-600 hover:bg-slate-100 dark:hover:bg-slate-600 text-slate-600 dark:text-slate-200 transition cursor-pointer"
                      title={locale === 'vi-VN' ? 'Sao chép mã' : 'Copy code'}
                    >
                      {isChallengeCopied ? (
                        <Check className="h-4 w-4 text-emerald-500" />
                      ) : (
                        <Copy className="h-4 w-4" />
                      )}
                    </button>
                  </div>
                  {isChallengeCopied && (
                    <span className="text-[10px] text-emerald-600 dark:text-emerald-400 font-bold block">
                      {locale === 'vi-VN' ? 'Đã sao chép mã tham chiếu!' : 'Reference code copied!'}
                    </span>
                  )}
                  {linkChallengeModalState.timeLeft > 0 ? (
                    <span className="text-[11px] text-slate-400 block pt-1">
                      {locale === 'vi-VN' ? 'Mã có hiệu lực trong:' : 'Valid for:'}{' '}
                      <span className="font-bold text-slate-700 dark:text-slate-300 font-mono">
                        {Math.floor(linkChallengeModalState.timeLeft / 60)}:
                        {String(linkChallengeModalState.timeLeft % 60).padStart(2, '0')}
                      </span>
                    </span>
                  ) : (
                    <span className="text-[11px] text-rose-500 font-semibold block pt-1">
                      {locale === 'vi-VN' ? 'Mã liên kết đã hết hạn. Vui lòng tạo mã mới.' : 'Link challenge has expired. Please create a new code.'}
                    </span>
                  )}
                </div>

                {/* Instructions */}
                <div className="p-3.5 rounded-2xl bg-sky-50 dark:bg-sky-950/40 border border-sky-200/80 dark:border-sky-800/50 text-xs text-sky-800 dark:text-sky-300 space-y-2">
                  <div className="flex items-center gap-1.5 font-bold text-[11px]">
                    <ShieldCheck className="h-4 w-4 text-sky-500 shrink-0" />
                    <span>{locale === 'vi-VN' ? 'Hướng dẫn xác thực (SePay Test Mode)' : 'Verification Instructions'}</span>
                  </div>
                  <ol className="list-decimal list-inside space-y-1 text-[11px] leading-relaxed text-sky-700 dark:text-sky-200">
                    <li>
                      {locale === 'vi-VN'
                        ? 'Mở cổng mô phỏng SePay hoặc thực hiện chuyển khoản demo.'
                        : 'Open the SePay sandbox simulator or initiate a demo bank transfer.'}
                    </li>
                    <li>
                      {locale === 'vi-VN'
                        ? 'Điền đúng mã tham chiếu phía trên vào phần Nội dung chuyển khoản.'
                        : 'Include the exact reference code above in the transfer memo/description.'}
                    </li>
                    <li>
                      {locale === 'vi-VN'
                        ? 'Bấm nút "Xác nhận liên kết" bên dưới để hệ thống đối soát.'
                        : 'Click "Confirm Link" below to reconcile and complete connection.'}
                    </li>
                  </ol>
                </div>

                <div className="pt-2 flex items-center gap-3">
                  <button
                    type="button"
                    onClick={() => setLinkChallengeModalState((prev) => ({ ...prev, isOpen: false }))}
                    className="flex-1 py-2.5 rounded-2xl border border-slate-200 dark:border-slate-800 hover:bg-slate-50 dark:hover:bg-slate-800 text-slate-700 dark:text-slate-300 text-xs font-bold transition cursor-pointer"
                  >
                    {t('common.cancel')}
                  </button>
                  <button
                    type="button"
                    disabled={linkChallengeModalState.confirming || linkChallengeModalState.timeLeft <= 0}
                    onClick={handleConfirmLinkChallenge}
                    className="flex-1 py-2.5 rounded-2xl bg-emerald-600 hover:bg-emerald-700 text-white text-xs font-bold shadow-xs transition flex items-center justify-center gap-2 disabled:opacity-50 cursor-pointer"
                  >
                    {linkChallengeModalState.confirming ? (
                      <>
                        <RefreshCw className="h-3.5 w-3.5 animate-spin" />
                        <span>{locale === 'vi-VN' ? 'Đang đối soát...' : 'Reconciling...'}</span>
                      </>
                    ) : (
                      <span>{locale === 'vi-VN' ? 'Xác nhận liên kết' : 'Confirm Link'}</span>
                    )}
                  </button>
                </div>

                {linkChallengeModalState.timeLeft <= 0 && (
                  <button
                    type="button"
                    onClick={() => handleOpenConnectBank()}
                    className="w-full py-2.5 rounded-2xl border border-emerald-500/40 text-emerald-600 dark:text-emerald-400 hover:bg-emerald-50 dark:hover:bg-emerald-950/40 text-xs font-bold transition cursor-pointer"
                  >
                    {locale === 'vi-VN' ? 'Tạo mã liên kết mới' : 'Create new challenge'}
                  </button>
                )}
              </div>
            ) : null}
          </div>
        </div>
      )}

      {/* ========================================================================= */}
      {/* MODAL 1B: SEPAY WALLET TOP-UP MODAL (SWTP)                                */}
      {/* ========================================================================= */}
      {topUpModalState.isOpen && (
        <div
          role="dialog"
          aria-modal="true"
          className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/60 backdrop-blur-xs animate-in fade-in duration-150"
        >
          <div className="w-full max-w-md bg-white dark:bg-slate-900 rounded-3xl p-6 sm:p-7 border border-slate-100 dark:border-slate-800 shadow-2xl space-y-5 animate-in zoom-in-95 max-h-[90vh] overflow-y-auto">
            <div className="flex items-center justify-between">
              <div>
                <div className="flex items-center gap-2">
                  <h3 className="text-base font-bold text-slate-900 dark:text-white">
                    {locale === 'vi-VN' ? 'Nạp tiền vào ví SpendWise' : 'Top Up SpendWise Wallet'}
                  </h3>
                  <span className="px-2 py-0.5 rounded-full bg-blue-100 dark:bg-blue-950/60 text-blue-700 dark:text-blue-300 text-[10px] font-bold border border-blue-200 dark:border-blue-800">
                    SePay SWTP
                  </span>
                </div>
                <p className="text-[11px] text-slate-400 mt-0.5">
                  {locale === 'vi-VN'
                    ? 'Nạp tiền từ tài khoản ngân hàng đã liên kết'
                    : 'Top up from your linked bank connection'}
                </p>
              </div>
              <button
                type="button"
                onClick={() => setTopUpModalState((prev) => ({ ...prev, isOpen: false }))}
                className="p-1.5 rounded-xl text-slate-400 hover:bg-slate-100 dark:hover:bg-slate-800 cursor-pointer"
              >
                <X className="h-4 w-4" />
              </button>
            </div>

            {topUpModalState.error && (
              <div className="p-3 rounded-2xl bg-rose-50 dark:bg-rose-950/50 border border-rose-200 dark:border-rose-900/50 text-rose-600 dark:text-rose-400 text-xs flex items-center gap-2">
                <AlertCircle className="h-4 w-4 shrink-0" />
                <span>{topUpModalState.error}</span>
              </div>
            )}

            {topUpModalState.step === 'create' ? (
              <form onSubmit={handleCreateTopUpSubmit} className="space-y-4">
                {/* Bank Connection Select */}
                <div className="space-y-1.5">
                  <label className="text-xs font-semibold text-slate-700 dark:text-slate-300">
                    {locale === 'vi-VN' ? 'Tài khoản ngân hàng nguồn' : 'Source Linked Bank'}
                  </label>
                  <Select
                    value={topUpModalState.bankConnectionId}
                    onChange={(val) => setTopUpModalState((prev) => ({ ...prev, bankConnectionId: val }))}
                    options={
                      testModeLinkedAccounts
                        .filter((a) => a.status === 'Active')
                        .map((a) => ({
                          value: a.bankConnectionId,
                          label: `${a.bankName} (${a.maskedAccountNumber}) — ${locale === 'vi-VN' ? 'Khả dụng:' : 'Available:'} ${formatMoney(a.accumulatedBalance, locale)}`,
                        }))
                    }
                    className="w-full text-xs"
                  />
                </div>

                {/* Target Wallet Select */}
                <div className="space-y-1.5">
                  <label className="text-xs font-semibold text-slate-700 dark:text-slate-300">
                    {locale === 'vi-VN' ? 'Ví nhận tiền' : 'Destination Wallet'}
                  </label>
                  <Select
                    value={topUpModalState.walletId}
                    onChange={(val) => setTopUpModalState((prev) => ({ ...prev, walletId: val }))}
                    options={wallets.map((w) => ({
                      value: w.id,
                      label: `${getLocalizedWalletName(w.id, w.name, locale)} (${formatMoney(w.balance, locale)})`,
                    }))}
                    className="w-full text-xs"
                  />
                </div>

                {/* Amount input */}
                <div className="space-y-1.5">
                  <label className="text-xs font-semibold text-slate-700 dark:text-slate-300">
                    {locale === 'vi-VN' ? 'Số tiền nạp (₫)' : 'Top-Up Amount (₫)'}
                  </label>
                  <input
                    type="text"
                    inputMode="numeric"
                    required
                    value={topUpModalState.amount}
                    onChange={(e) =>
                      setTopUpModalState((prev) => ({
                        ...prev,
                        amount: sanitizeDigits(e.target.value),
                      }))
                    }
                    className="w-full px-4 py-2.5 rounded-2xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800 text-slate-900 dark:text-white text-xs font-bold tabular-nums focus:outline-none focus:ring-2 focus:ring-blue-500/30 focus:border-blue-500"
                    placeholder="50000"
                  />

                  {/* Quick amounts */}
                  <div className="flex flex-wrap items-center gap-1.5 pt-1">
                    {[50000, 100000, 200000, 500000, 1000000].map((amt) => (
                      <button
                        key={amt}
                        type="button"
                        onClick={() =>
                          setTopUpModalState((prev) => ({ ...prev, amount: String(amt) }))
                        }
                        className={cn(
                          'px-2.5 py-1 rounded-xl text-[10px] font-bold border transition cursor-pointer',
                          topUpModalState.amount === String(amt)
                            ? 'bg-blue-50 dark:bg-blue-950/60 border-blue-500 text-blue-600 dark:text-blue-400'
                            : 'border-slate-200 dark:border-slate-700 hover:bg-slate-100 dark:hover:bg-slate-800 text-slate-600 dark:text-slate-400'
                        )}
                      >
                        {formatMoney(amt, locale)}
                      </button>
                    ))}
                  </div>
                </div>

                <div className="pt-2 flex items-center gap-3">
                  <button
                    type="button"
                    onClick={() => setTopUpModalState((prev) => ({ ...prev, isOpen: false }))}
                    className="flex-1 py-2.5 rounded-2xl border border-slate-200 dark:border-slate-800 hover:bg-slate-50 dark:hover:bg-slate-800 text-slate-700 dark:text-slate-300 text-xs font-bold transition cursor-pointer"
                  >
                    {t('common.cancel')}
                  </button>
                  <button
                    type="submit"
                    disabled={topUpModalState.loading}
                    className="flex-1 py-2.5 rounded-2xl bg-blue-600 hover:bg-blue-700 text-white text-xs font-bold shadow-xs transition flex items-center justify-center gap-2 disabled:opacity-50 cursor-pointer"
                  >
                    {topUpModalState.loading ? (
                      <>
                        <RefreshCw className="h-3.5 w-3.5 animate-spin" />
                        <span>{locale === 'vi-VN' ? 'Đang khởi tạo...' : 'Initializing...'}</span>
                      </>
                    ) : (
                      <span>{locale === 'vi-VN' ? 'Tiếp tục' : 'Continue'}</span>
                    )}
                  </button>
                </div>
              </form>
            ) : topUpModalState.topUp ? (
              <div className="space-y-4">
                {/* Top Up Reference & Details */}
                <div className="p-4 rounded-2xl bg-slate-50 dark:bg-slate-800/60 border border-slate-200/80 dark:border-slate-700/60 space-y-3">
                  <div className="text-center space-y-1">
                    <span className="text-[11px] font-medium text-slate-500 dark:text-slate-400 uppercase tracking-wider block">
                      {locale === 'vi-VN' ? 'Mã tham chiếu nạp tiền' : 'Top-Up Reference Code'}
                    </span>
                    <div className="flex items-center justify-center gap-2">
                      <span className="font-mono text-xl sm:text-2xl font-bold tracking-wider text-blue-600 dark:text-blue-400 select-all">
                        {topUpModalState.topUp.reference}
                      </span>
                      <button
                        type="button"
                        onClick={handleCopyTopUpReference}
                        className="p-2 rounded-xl bg-white dark:bg-slate-700 border border-slate-200 dark:border-slate-600 hover:bg-slate-100 dark:hover:bg-slate-600 text-slate-600 dark:text-slate-200 transition cursor-pointer"
                        title={locale === 'vi-VN' ? 'Sao chép mã' : 'Copy code'}
                      >
                        {isTopUpRefCopied ? (
                          <Check className="h-4 w-4 text-emerald-500" />
                        ) : (
                          <Copy className="h-4 w-4" />
                        )}
                      </button>
                    </div>
                    {isTopUpRefCopied && (
                      <span className="text-[10px] text-emerald-600 dark:text-emerald-400 font-bold block">
                        {locale === 'vi-VN' ? 'Đã sao chép mã nạp tiền!' : 'Reference code copied!'}
                      </span>
                    )}
                  </div>

                  <div className="pt-2 border-t border-slate-200/60 dark:border-slate-700/60 space-y-1.5 text-xs">
                    <div className="flex items-center justify-between">
                      <span className="text-slate-500 dark:text-slate-400">
                        {locale === 'vi-VN' ? 'Số tiền cần chuyển:' : 'Amount to transfer:'}
                      </span>
                      <span className="font-bold text-slate-900 dark:text-white tabular-nums">
                        {formatMoney(topUpModalState.topUp.amount, locale)}
                      </span>
                    </div>
                    <div className="flex items-center justify-between">
                      <span className="text-slate-500 dark:text-slate-400">
                        {locale === 'vi-VN' ? 'Tài khoản nguồn:' : 'Source Account:'}
                      </span>
                      <span className="font-mono text-slate-700 dark:text-slate-300">
                        {topUpModalState.topUp.maskedAccountNumber}
                      </span>
                    </div>
                    <div className="flex items-center justify-between">
                      <span className="text-slate-500 dark:text-slate-400">
                        {locale === 'vi-VN' ? 'Trạng thái:' : 'Status:'}
                      </span>
                      <span
                        className={cn(
                          'font-semibold',
                          topUpModalState.topUp.status === 'Completed'
                            ? 'text-emerald-600 dark:text-emerald-400'
                            : topUpModalState.topUp.status === 'Expired' || topUpModalState.topUp.status === 'Failed'
                            ? 'text-rose-600 dark:text-rose-400'
                            : 'text-amber-600 dark:text-amber-400'
                        )}
                      >
                        {topUpModalState.topUp.status}
                      </span>
                    </div>
                  </div>

                  {topUpModalState.timeLeft > 0 ? (
                    <div className="text-center pt-1 text-[11px] text-slate-400">
                      {locale === 'vi-VN' ? 'Hết hạn sau:' : 'Expires in:'}{' '}
                      <span className="font-mono font-bold text-slate-700 dark:text-slate-300">
                        {Math.floor(topUpModalState.timeLeft / 60)}:
                        {String(topUpModalState.timeLeft % 60).padStart(2, '0')}
                      </span>
                    </div>
                  ) : (
                    <div className="text-center pt-1 text-[11px] text-rose-500 font-semibold">
                      {locale === 'vi-VN' ? 'Yêu cầu nạp tiền đã hết hạn. Vui lòng tạo yêu cầu mới.' : 'Top-up request has expired. Please create a new one.'}
                    </div>
                  )}
                </div>

                {/* Instructions */}
                <div className="p-3.5 rounded-2xl bg-amber-50 dark:bg-amber-950/40 border border-amber-200/80 dark:border-amber-800/50 text-xs text-amber-800 dark:text-amber-300 space-y-1.5">
                  <div className="flex items-center gap-1.5 font-bold text-[11px]">
                    <ShieldCheck className="h-4 w-4 text-amber-600 shrink-0" />
                    <span>{locale === 'vi-VN' ? 'Hướng dẫn nạp tiền (SePay Test Mode)' : 'Top-Up Instructions'}</span>
                  </div>
                  <p className="text-[11px] leading-relaxed text-amber-700 dark:text-amber-200">
                    {topUpModalState.topUp.instruction ||
                      (locale === 'vi-VN'
                        ? 'Thực hiện chuyển khoản demo với đúng mã tham chiếu phía trên. Sau khi chuyển xong, bấm "Xác nhận nạp tiền" để hệ thống cộng số dư vào ví.'
                        : 'Execute demo transfer with the reference code above, then click "Confirm Top-Up".')}
                  </p>
                </div>

                <div className="pt-2 flex items-center gap-3">
                  <button
                    type="button"
                    onClick={() => setTopUpModalState((prev) => ({ ...prev, isOpen: false }))}
                    className="flex-1 py-2.5 rounded-2xl border border-slate-200 dark:border-slate-800 hover:bg-slate-50 dark:hover:bg-slate-800 text-slate-700 dark:text-slate-300 text-xs font-bold transition cursor-pointer"
                  >
                    {t('common.cancel')}
                  </button>
                  <button
                    type="button"
                    disabled={
                      topUpModalState.confirming ||
                      topUpModalState.timeLeft <= 0 ||
                      topUpModalState.topUp.status === 'Expired' ||
                      topUpModalState.topUp.status === 'Failed'
                    }
                    onClick={handleConfirmTopUp}
                    className="flex-1 py-2.5 rounded-2xl bg-blue-600 hover:bg-blue-700 text-white text-xs font-bold shadow-xs transition flex items-center justify-center gap-2 disabled:opacity-50 cursor-pointer"
                  >
                    {topUpModalState.confirming ? (
                      <>
                        <RefreshCw className="h-3.5 w-3.5 animate-spin" />
                        <span>{locale === 'vi-VN' ? 'Đang kiểm tra...' : 'Checking...'}</span>
                      </>
                    ) : (
                      <span>{locale === 'vi-VN' ? 'Xác nhận nạp tiền' : 'Confirm Top-Up'}</span>
                    )}
                  </button>
                </div>

                {(topUpModalState.topUp.status === 'Expired' || topUpModalState.topUp.status === 'Failed' || topUpModalState.timeLeft <= 0) && (
                  <button
                    type="button"
                    onClick={() =>
                      setTopUpModalState((prev) => ({
                        ...prev,
                        step: 'create',
                        topUp: null,
                        error: null,
                        timeLeft: 0,
                      }))
                    }
                    className="w-full py-2.5 rounded-2xl border border-blue-500/40 text-blue-600 dark:text-blue-400 hover:bg-blue-50 dark:hover:bg-blue-950/40 text-xs font-bold transition cursor-pointer"
                  >
                    {locale === 'vi-VN' ? 'Tạo yêu cầu nạp tiền mới' : 'Create new top-up request'}
                  </button>
                )}
              </div>
            ) : null}
          </div>
        </div>
      )}

      {/* ========================================================================= */}
      {/* MODAL 2: TRANSFER BETWEEN INTERNAL WALLETS                                */}
      {/* ========================================================================= */}
      {isTransferOpen && (
        <div
          role="dialog"
          aria-modal="true"
          className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/60 backdrop-blur-xs animate-in fade-in duration-150"
        >
          <div className="w-full max-w-md bg-white dark:bg-slate-900 rounded-3xl p-6 sm:p-7 border border-slate-100 dark:border-slate-800 shadow-2xl space-y-5 animate-in zoom-in-95">
            <div className="flex items-center justify-between">
              <div>
                <h3 className="text-base font-bold text-slate-900 dark:text-white">
                  {t('wallet.transferBetween')}
                </h3>
                <p className="text-xs text-slate-400 mt-0.5">
                  {locale === 'vi-VN'
                    ? 'Điều chuyển số dư giữa các ví tài khoản SpendWise'
                    : 'Transfer balance between internal SpendWise wallets'}
                </p>
              </div>
              <button
                type="button"
                onClick={() => setIsTransferOpen(false)}
                className="p-1.5 rounded-xl text-slate-400 hover:bg-slate-100 dark:hover:bg-slate-800 cursor-pointer"
              >
                <X className="h-4 w-4" />
              </button>
            </div>

            {modalError && (
              <div className="p-3 rounded-2xl bg-rose-50 dark:bg-rose-950/50 border border-rose-200 dark:border-rose-900/50 text-rose-600 dark:text-rose-400 text-xs flex items-center gap-2">
                <AlertCircle className="h-4 w-4 shrink-0" />
                <span>{modalError}</span>
              </div>
            )}

            <form
              onSubmit={handleTransferSubmit}
              onKeyDown={(e) => {
                if (e.key === 'Enter' && (e.target as HTMLElement).tagName !== 'TEXTAREA') {
                  e.preventDefault();
                  handleTransferSubmit(e);
                }
              }}
              className="space-y-4"
            >
              <div className="space-y-1.5">
                <label className="text-xs font-semibold text-slate-700 dark:text-slate-300">
                  {locale === 'vi-VN' ? 'Từ ví chuyển đi' : 'From Wallet'}
                </label>
                <Select
                  value={sourceWalletId}
                  onChange={(val) => setSourceWalletId(val)}
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

              <div className="space-y-1.5">
                <label className="text-xs font-semibold text-slate-700 dark:text-slate-300">
                  {locale === 'vi-VN' ? 'Đến ví nhận' : 'To Wallet'}
                </label>
                <Select
                  value={targetWalletId}
                  onChange={(val) => setTargetWalletId(val)}
                  accentColor="emerald"
                  icon={<Wallet className="h-4 w-4 text-emerald-500" />}
                >
                  {wallets
                    .filter((w) => w.id !== sourceWalletId)
                    .map((w) => (
                      <option key={w.id} value={w.id}>
                        {getLocalizedWalletName(w.id, w.name, locale)} ({formatMoney(w.balance, locale)})
                      </option>
                    ))}
                </Select>
              </div>

              <div className="space-y-1.5">
                <label className="text-xs font-semibold text-slate-700 dark:text-slate-300">
                  {locale === 'vi-VN' ? 'Số tiền chuyển' : 'Amount'}
                </label>
                <div className="relative">
                  <input
                    type="text"
                    inputMode="numeric"
                    value={actionAmount !== '' ? Number(actionAmount).toLocaleString('vi-VN') : ''}
                    onChange={(e) => {
                      setActionAmount(sanitizeDigits(e.target.value));
                      if (modalError) setModalError(null);
                    }}
                    className="w-full px-4 py-2.5 rounded-2xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 text-slate-900 dark:text-white text-xs font-bold tabular-nums focus:outline-none focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500"
                    placeholder="500,000"
                  />
                  <span className="absolute right-4 top-2.5 text-xs text-slate-400 font-bold">₫</span>
                </div>
              </div>

              <div className="pt-2 flex items-center gap-3">
                <button
                  type="button"
                  onClick={() => setIsTransferOpen(false)}
                  className="flex-1 py-2.5 rounded-2xl border border-slate-200 dark:border-slate-800 hover:bg-slate-50 dark:hover:bg-slate-800 text-slate-700 dark:text-slate-300 text-xs font-bold transition cursor-pointer"
                >
                  {t('common.cancel')}
                </button>
                <button
                  type="submit"
                  className="flex-1 py-2.5 rounded-2xl bg-slate-900 hover:bg-slate-800 text-white dark:bg-white dark:text-slate-900 dark:hover:bg-slate-100 text-xs font-bold shadow-xs transition cursor-pointer"
                >
                  {t('transfer.confirmBtn')}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* ========================================================================= */}
      {/* MODAL 3: ADD INTERNAL WALLET                                              */}
      {/* ========================================================================= */}
      {isAddWalletOpen && (
        <div
          role="dialog"
          aria-modal="true"
          className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/60 backdrop-blur-xs animate-in fade-in duration-150"
        >
          <div className="w-full max-w-md bg-white dark:bg-slate-900 rounded-3xl p-6 sm:p-7 border border-slate-100 dark:border-slate-800 shadow-2xl space-y-5 animate-in zoom-in-95">
            <div className="flex items-center justify-between">
              <div>
                <h3 className="text-base font-bold text-slate-900 dark:text-white">
                  {t('wallet.addWallet')}
                </h3>
                <p className="text-xs text-slate-400 mt-0.5">
                  {locale === 'vi-VN'
                    ? 'Tạo ví mới để phân bổ và kiểm soát dòng tiền'
                    : 'Create a new internal wallet to allocate funds'}
                </p>
              </div>
              <button
                type="button"
                onClick={() => setIsAddWalletOpen(false)}
                className="p-1.5 rounded-xl text-slate-400 hover:bg-slate-100 dark:hover:bg-slate-800 cursor-pointer"
              >
                <X className="h-4 w-4" />
              </button>
            </div>

            {modalError && (
              <div className="p-3 rounded-2xl bg-rose-50 dark:bg-rose-950/50 border border-rose-200 dark:border-rose-900/50 text-rose-600 dark:text-rose-400 text-xs flex items-center gap-2">
                <AlertCircle className="h-4 w-4 shrink-0" />
                <span>{modalError}</span>
              </div>
            )}

            <form
              onSubmit={handleAddWalletSubmit}
              onKeyDown={(e) => {
                if (e.key === 'Enter' && (e.target as HTMLElement).tagName !== 'TEXTAREA') {
                  e.preventDefault();
                  handleAddWalletSubmit(e);
                }
              }}
              className="space-y-4"
            >
              <div className="space-y-1.5">
                <label className="text-xs font-semibold text-slate-700 dark:text-slate-300">
                  {t('wallet.walletName')}
                </label>
                <input
                  type="text"
                  value={newWalletName}
                  onChange={(e) => setNewWalletName(e.target.value)}
                  className="w-full px-4 py-2.5 rounded-2xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 text-slate-900 dark:text-white text-xs font-semibold focus:outline-none focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500"
                  placeholder={locale === 'vi-VN' ? 'Ví mua sắm / Ví công việc...' : 'Shopping / Work Vault...'}
                />
              </div>

              <div className="space-y-1.5">
                <label className="text-xs font-semibold text-slate-700 dark:text-slate-300">
                  {t('wallet.walletType')}
                </label>
                <Select
                  value={newWalletType}
                  onChange={(val) => setNewWalletType(val as any)}
                  accentColor="emerald"
                >
                  <option value="savings">{locale === 'vi-VN' ? 'Tiết kiệm (Savings)' : 'Savings'}</option>
                  <option value="emergency">{locale === 'vi-VN' ? 'Quỹ dự phòng (Emergency)' : 'Emergency'}</option>
                </Select>
              </div>

              <div className="pt-2 flex items-center gap-3">
                <button
                  type="button"
                  onClick={() => setIsAddWalletOpen(false)}
                  className="flex-1 py-2.5 rounded-2xl border border-slate-200 dark:border-slate-800 hover:bg-slate-50 dark:hover:bg-slate-800 text-slate-700 dark:text-slate-300 text-xs font-bold transition cursor-pointer"
                >
                  {t('common.cancel')}
                </button>
                <button
                  type="submit"
                  className="flex-1 py-2.5 rounded-2xl bg-emerald-600 hover:bg-emerald-700 text-white text-xs font-bold shadow-xs transition cursor-pointer"
                >
                  {t('wallet.addWallet')}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* ========================================================================= */}
      {/* MODAL 4: EDIT INTERNAL WALLET                                             */}
      {/* ========================================================================= */}
      {editingWallet && (
        <div
          role="dialog"
          aria-modal="true"
          className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/60 backdrop-blur-xs animate-in fade-in duration-150"
        >
          <div className="w-full max-w-md bg-white dark:bg-slate-900 rounded-3xl p-6 sm:p-7 border border-slate-100 dark:border-slate-800 shadow-2xl space-y-5 animate-in zoom-in-95">
            <div className="flex items-center justify-between">
              <div>
                <h3 className="text-base font-bold text-slate-900 dark:text-white">
                  {t('wallet.editWallet')}
                </h3>
                <p className="text-xs text-slate-400 mt-0.5">
                  {editingWallet.accountNumber}
                </p>
              </div>
              <button
                type="button"
                onClick={() => setEditingWallet(null)}
                className="p-1.5 rounded-xl text-slate-400 hover:bg-slate-100 dark:hover:bg-slate-800 cursor-pointer"
              >
                <X className="h-4 w-4" />
              </button>
            </div>

            {modalError && (
              <div className="p-3 rounded-2xl bg-rose-50 dark:bg-rose-950/50 border border-rose-200 dark:border-rose-900/50 text-rose-600 dark:text-rose-400 text-xs flex items-center gap-2">
                <AlertCircle className="h-4 w-4 shrink-0" />
                <span>{modalError}</span>
              </div>
            )}

            <form
              onSubmit={handleSaveEditWallet}
              onKeyDown={(e) => {
                if (e.key === 'Enter' && (e.target as HTMLElement).tagName !== 'TEXTAREA') {
                  e.preventDefault();
                  handleSaveEditWallet(e);
                }
              }}
              className="space-y-4"
            >
              <div className="space-y-1.5">
                <label className="text-xs font-semibold text-slate-700 dark:text-slate-300">
                  {t('wallet.walletName')}
                </label>
                <input
                  type="text"
                  value={newWalletName}
                  onChange={(e) => setNewWalletName(e.target.value)}
                  className="w-full px-4 py-2.5 rounded-2xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 text-slate-900 dark:text-white text-xs font-semibold focus:outline-none focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-500"
                />
              </div>

              <div className="space-y-1.5">
                <label className="text-xs font-semibold text-slate-700 dark:text-slate-300">
                  {t('wallet.walletType')}
                </label>
                <Select
                  value={newWalletType}
                  onChange={(val) => setNewWalletType(val as any)}
                  accentColor="emerald"
                >
                  <option value="spending">{locale === 'vi-VN' ? 'Tiêu dùng (Spending)' : 'Spending'}</option>
                  <option value="savings">{locale === 'vi-VN' ? 'Tiết kiệm (Savings)' : 'Savings'}</option>
                  <option value="emergency">{locale === 'vi-VN' ? 'Quỹ dự phòng (Emergency)' : 'Emergency'}</option>
                </Select>
              </div>

              <div className="pt-2 flex items-center gap-3">
                <button
                  type="button"
                  onClick={() => setEditingWallet(null)}
                  className="flex-1 py-2.5 rounded-2xl border border-slate-200 dark:border-slate-800 hover:bg-slate-50 dark:hover:bg-slate-800 text-slate-700 dark:text-slate-300 text-xs font-bold transition cursor-pointer"
                >
                  {t('common.cancel')}
                </button>
                <button
                  type="submit"
                  className="flex-1 py-2.5 rounded-2xl bg-emerald-600 hover:bg-emerald-700 text-white text-xs font-bold shadow-xs transition cursor-pointer"
                >
                  {t('common.save')}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* ========================================================================= */}
      {/* MODAL 5: CLOSE WALLET CONFIRMATION                                        */}
      {/* ========================================================================= */}
      {closingWallet && (
        <div
          role="dialog"
          aria-modal="true"
          className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/60 backdrop-blur-xs animate-in fade-in duration-150"
        >
          <div className="w-full max-w-md bg-white dark:bg-slate-900 rounded-3xl p-6 sm:p-7 border border-slate-100 dark:border-slate-800 shadow-2xl space-y-5 animate-in zoom-in-95">
            <div className="flex items-center gap-3">
              <div className="h-10 w-10 rounded-2xl bg-rose-100 dark:bg-rose-950 text-rose-600 dark:text-rose-400 flex items-center justify-center shrink-0">
                <Trash2 className="h-5 w-5" />
              </div>
              <div>
                <h3 className="text-base font-bold text-slate-900 dark:text-white">
                  {t('wallet.closeWalletConfirmTitle')}
                </h3>
                <p className="text-xs text-slate-400">
                  {closingWallet.name} ({closingWallet.accountNumber})
                </p>
              </div>
            </div>

            <p className="text-xs leading-relaxed text-slate-600 dark:text-slate-300">
              {t('wallet.closeWalletConfirmDesc')}
            </p>

            {closingWallet.balance > 0 && (
              <div className="p-3 rounded-2xl bg-amber-50 dark:bg-amber-950/50 border border-amber-200 dark:border-amber-900/50 text-amber-800 dark:text-amber-300 text-xs">
                {t('wallet.closeWalletHasBalanceWarning', {
                  balance: formatMoney(closingWallet.balance, locale),
                })}
              </div>
            )}

            {closeWalletError && (
              <div className="p-3 rounded-2xl bg-rose-50 dark:bg-rose-950/50 border border-rose-200 dark:border-rose-900/50 text-rose-600 dark:text-rose-400 text-xs flex items-center gap-2">
                <AlertCircle className="h-4 w-4 shrink-0" />
                <span>{closeWalletError}</span>
              </div>
            )}

            <div className="pt-2 flex items-center gap-3">
              <button
                type="button"
                onClick={() => {
                  setClosingWallet(null);
                  setCloseWalletError(null);
                }}
                className="flex-1 py-2.5 rounded-2xl border border-slate-200 dark:border-slate-800 hover:bg-slate-50 dark:hover:bg-slate-800 text-slate-700 dark:text-slate-300 text-xs font-bold transition cursor-pointer"
              >
                {t('common.cancel')}
              </button>
              <button
                type="button"
                disabled={closingWallet.balance > 0}
                onClick={handleConfirmCloseWallet}
                className="flex-1 py-2.5 rounded-2xl bg-rose-600 hover:bg-rose-700 text-white text-xs font-bold shadow-xs transition disabled:opacity-50 cursor-pointer"
              >
                {t('wallet.closeWallet')}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Bank Transaction Category Classification Modal */}
      {classifyingBankTx && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-sm animate-fadeIn">
          <div className="bg-white dark:bg-slate-900 rounded-3xl p-6 border border-slate-200 dark:border-slate-800 shadow-2xl w-full max-w-md space-y-4">
            <div className="flex items-center justify-between pb-3 border-b border-slate-100 dark:border-slate-800">
              <h3 className="text-base font-bold text-slate-900 dark:text-white flex items-center gap-2">
                <Tag className="h-4 w-4 text-indigo-500" />
                <span>{locale === 'vi-VN' ? 'Phân loại giao dịch ngân hàng' : 'Classify Bank Transaction'}</span>
              </h3>
              <button
                type="button"
                onClick={() => setClassifyingBankTx(null)}
                className="text-slate-400 hover:text-slate-600 dark:hover:text-slate-200 p-1 rounded-full cursor-pointer"
              >
                <X className="h-4 w-4" />
              </button>
            </div>

            <div className="p-3 rounded-2xl bg-slate-50 dark:bg-slate-800/60 text-xs space-y-1">
              <p className="font-semibold text-slate-800 dark:text-slate-200">{classifyingBankTx.content}</p>
              <p className="text-slate-500 font-mono">
                {formatMoney(classifyingBankTx.amount, locale)} • {formatDate(classifyingBankTx.transactionDate, locale)}
              </p>
            </div>

            <div className="space-y-2">
              <label className="text-xs font-semibold text-slate-700 dark:text-slate-300">
                {locale === 'vi-VN' ? 'Chọn danh mục phù hợp' : 'Select Category'}
              </label>
              <div className="grid grid-cols-2 gap-2 max-h-60 overflow-y-auto pr-1">
                {categories
                  .filter(
                    (c) =>
                      !c.isArchived &&
                      (classifyingBankTx.direction === 'Credit' ? c.kind === 'income' : c.kind === 'expense')
                  )
                  .map((cat) => (
                    <button
                      key={cat.id}
                      type="button"
                      disabled={isClassifyingBankTxSubmitting}
                      onClick={async () => {
                        setIsClassifyingBankTxSubmitting(true);
                        const res = await updateBankTransactionCategory(classifyingBankTx.id, cat.id);
                        setIsClassifyingBankTxSubmitting(false);
                        if (res.success) {
                          showToast(
                            locale === 'vi-VN'
                              ? `Đã phân loại sang: ${cat.name}`
                              : `Classified as: ${cat.name}`
                          );
                          setClassifyingBankTx(null);
                        } else {
                          showToast(
                            res.error ||
                              (locale === 'vi-VN' ? 'Lỗi khi cập nhật danh mục' : 'Failed to update category')
                          );
                        }
                      }}
                      className={cn(
                        'flex items-center gap-2 p-2.5 rounded-xl border text-left text-xs font-medium transition cursor-pointer',
                        classifyingBankTx.categoryId === cat.id
                          ? 'border-indigo-500 bg-indigo-50 dark:bg-indigo-950/60 text-indigo-700 dark:text-indigo-300 font-bold'
                          : 'border-slate-200 dark:border-slate-800 hover:bg-slate-50 dark:hover:bg-slate-800 text-slate-700 dark:text-slate-300'
                      )}
                    >
                      <span className="truncate">{cat.name}</span>
                    </button>
                  ))}
              </div>
            </div>

            <div className="pt-2 flex justify-end">
              <button
                type="button"
                onClick={() => setClassifyingBankTx(null)}
                className="px-4 py-2 text-xs font-semibold text-slate-600 dark:text-slate-400 hover:bg-slate-100 dark:hover:bg-slate-800 rounded-xl transition cursor-pointer"
              >
                {t('common.cancel')}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
