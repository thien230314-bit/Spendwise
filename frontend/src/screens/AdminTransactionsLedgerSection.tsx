import React, { useState, useMemo, useEffect } from 'react';
import {
  Search,
  ArrowLeftRight,
  Scale,
  ClipboardList,
  Settings,
  Lock,
  Unlock,
  Check,
  AlertTriangle,
  ChevronLeft,
  ChevronRight,
  FileCheck2,
  RefreshCw,
  ShieldCheck,
  ShieldAlert,
  X,
  Receipt,
} from 'lucide-react';
import { formatVND, useSpendWise } from '../context/SpendWiseContext';
import {
  AdminGlobalAuditEvent,
  AdminLedgerTransactionEntry,
  AdminLedgerTxStatus,
  AdminLedgerTxType,
  AdminSyntheticWalletLedgerItem,
  AdminSyntheticWalletState,
  AdminSystemConfigParameter,
} from '../data/adminFixtures';

export type AdminLedgerSubTabId =
  | 'transactions'
  | 'reconciliation'
  | 'audit'
  | 'config';

interface AdminTransactionsLedgerSectionProps {
  transactions: AdminLedgerTransactionEntry[];
  wallets: AdminSyntheticWalletLedgerItem[];
  onUpdateWallets: React.Dispatch<
    React.SetStateAction<AdminSyntheticWalletLedgerItem[]>
  >;
  auditEvents: AdminGlobalAuditEvent[];
  onAppendAuditEvent: (event: AdminGlobalAuditEvent) => void;
  configParams: AdminSystemConfigParameter[];
  onUpdateConfigParams: React.Dispatch<
    React.SetStateAction<AdminSystemConfigParameter[]>
  >;
  activeSubTab: AdminLedgerSubTabId;
  onChangeSubTab: (tab: AdminLedgerSubTabId) => void;
}

type PendingLedgerAction =
  | {
      kind: 'toggle_wallet_freeze';
      walletId: string;
      walletCode: string;
      walletNameVi: string;
      walletNameEn: string;
      ownerName: string;
      ownerSpendId: string;
      currentState: AdminSyntheticWalletState;
      targetState: AdminSyntheticWalletState;
    }
  | {
      kind: 'run_reconciliation_batch';
      walletCount: number;
    }
  | {
      kind: 'verify_system_config';
      paramCount: number;
    };

interface LedgerActionReceiptBanner {
  referenceCode: string;
  timestamp: string;
  targetCode: string;
  titleVi: string;
  titleEn: string;
  summaryVi: string;
  summaryEn: string;
  reason: string;
  nonSettlementNoticeVi: string;
  nonSettlementNoticeEn: string;
}

const TX_PAGE_SIZE = 5;

function getTxStatusMeta(
  status: AdminLedgerTxStatus,
  tr: (vi: string, en: string) => string
): {
  label: string;
  textClass: string;
} {
  switch (status) {
    case 'settled_demo':
      return {
        label: tr('Hoàn tất sổ cái (DEMO)', 'Ledger Settled (DEMO)'),
        textClass: 'text-[var(--sw-em-d)]',
      };
    case 'held_reservation':
      return {
        label: tr('Đang giữ chỗ (Hold)', 'Reserved Hold'),
        textClass: 'text-[var(--amber)]',
      };
    case 'pending_reconcile':
      return {
        label: tr('Chờ đối soát mã', 'Pending Reconcile'),
        textClass: 'text-[var(--amber)]',
      };
    case 'reversed_demo':
      return {
        label: tr('Đã hoàn trả sổ cái', 'Reversed to Wallet'),
        textClass: 'text-[var(--ink2)]',
      };
  }
}

function getWalletStateMeta(
  state: AdminSyntheticWalletState,
  tr: (vi: string, en: string) => string
): {
  label: string;
  textClass: string;
} {
  if (state === 'active') {
    return {
      label: tr('Hoạt động', 'Active'),
      textClass: 'text-[var(--sw-em-d)]',
    };
  }
  return {
    label: tr('Đang đóng băng', 'Frozen'),
    textClass: 'text-[var(--rose)]',
  };
}

export const AdminTransactionsLedgerSection: React.FC<
  AdminTransactionsLedgerSectionProps
> = ({
  transactions,
  wallets,
  onUpdateWallets,
  auditEvents,
  onAppendAuditEvent,
  configParams,
  onUpdateConfigParams,
  activeSubTab,
  onChangeSubTab,
}) => {
  const { tr } = useSpendWise();

  // 1. Transactions filter & pagination state
  const [txSearchQuery, setTxSearchQuery] = useState('');
  const [txTypeFilter, setTxTypeFilter] = useState<'all' | AdminLedgerTxType>('all');
  const [txStatusFilter, setTxStatusFilter] = useState<'all' | AdminLedgerTxStatus>(
    'all'
  );
  const [txDirectionFilter, setTxDirectionFilter] = useState<
    'all' | 'credit' | 'debit'
  >('all');
  const [txWalletCodeFilter, setTxWalletCodeFilter] = useState<string>('all');
  const [txCurrentPage, setTxCurrentPage] = useState(1);
  const [simulatedTxError, setSimulatedTxError] = useState(false);
  const [selectedTxId, setSelectedTxId] = useState<string>('ltx_01');

  // 2. Reconciliation & Synthetic Wallets filter state
  const [walletSearchQuery, setWalletSearchQuery] = useState('');
  const [walletStateFilter, setWalletStateFilter] = useState<
    'all' | AdminSyntheticWalletState
  >('all');

  // 3. Audit Log filter state
  const [auditSearchQuery, setAuditSearchQuery] = useState('');
  const [auditModuleFilter, setAuditModuleFilter] = useState<
    'all' | AdminGlobalAuditEvent['module']
  >('all');

  // 4. System Config filter state
  const [configCategoryFilter, setConfigCategoryFilter] = useState<
    'all' | AdminSystemConfigParameter['category']
  >('all');

  // 5. Deliberate confirmation modal state
  const [pendingAction, setPendingAction] = useState<PendingLedgerAction | null>(
    null
  );
  const [reasonInput, setReasonInput] = useState('');
  const [isDeliberatelyConfirmed, setIsDeliberatelyConfirmed] = useState(false);
  const [confirmError, setConfirmError] = useState<string | null>(null);

  // 6. Latest explicit demo action receipt banner
  const [latestReceipt, setLatestReceipt] =
    useState<LedgerActionReceiptBanner | null>(null);

  // Filtered transactions
  const filteredTransactions = useMemo(() => {
    const q = txSearchQuery.trim().toLowerCase();
    return transactions.filter((tx) => {
      if (txTypeFilter !== 'all' && tx.type !== txTypeFilter) return false;
      if (txStatusFilter !== 'all' && tx.status !== txStatusFilter) return false;
      if (txDirectionFilter !== 'all' && tx.direction !== txDirectionFilter) {
        return false;
      }
      if (txWalletCodeFilter !== 'all' && tx.walletCode !== txWalletCodeFilter) {
        return false;
      }
      if (!q) return true;
      return (
        tx.txCode.toLowerCase().includes(q) ||
        tx.correlationId.toLowerCase().includes(q) ||
        tx.walletCode.toLowerCase().includes(q) ||
        tx.ownerName.toLowerCase().includes(q) ||
        tx.ownerSpendId.toLowerCase().includes(q) ||
        tx.counterpartyLabelVi.toLowerCase().includes(q) ||
        tx.counterpartyLabelEn.toLowerCase().includes(q) ||
        tx.debitAccountCode.toLowerCase().includes(q) ||
        tx.creditAccountCode.toLowerCase().includes(q)
      );
    });
  }, [
    transactions,
    txSearchQuery,
    txTypeFilter,
    txStatusFilter,
    txDirectionFilter,
    txWalletCodeFilter,
  ]);

  const txTotalPages = Math.max(
    1,
    Math.ceil(filteredTransactions.length / TX_PAGE_SIZE)
  );
  const txSafePage = Math.min(txCurrentPage, txTotalPages);

  useEffect(() => {
    setTxCurrentPage(1);
  }, [
    txSearchQuery,
    txTypeFilter,
    txStatusFilter,
    txDirectionFilter,
    txWalletCodeFilter,
  ]);

  const paginatedTransactions = useMemo(() => {
    const startIdx = (txSafePage - 1) * TX_PAGE_SIZE;
    return filteredTransactions.slice(startIdx, startIdx + TX_PAGE_SIZE);
  }, [filteredTransactions, txSafePage]);

  const selectedTransaction = useMemo(
    () =>
      transactions.find((t) => t.id === selectedTxId) ||
      filteredTransactions[0] ||
      transactions[0] ||
      null,
    [transactions, filteredTransactions, selectedTxId]
  );

  const selectedTxWallet = useMemo(() => {
    if (!selectedTransaction) return null;
    return (
      wallets.find((w) => w.id === selectedTransaction.walletId) ||
      wallets.find((w) => w.walletCode === selectedTransaction.walletCode) ||
      null
    );
  }, [wallets, selectedTransaction]);

  // Reconciliation KPIs
  const reconciliationMetrics = useMemo(() => {
    let totalAvailable = 0;
    let totalHeld = 0;
    let totalLedger = 0;
    let totalJournal = 0;
    let totalVariance = 0;
    let frozenCount = 0;

    for (const w of wallets) {
      totalAvailable += w.availableBalanceVnd;
      totalHeld += w.heldBalanceVnd;
      totalLedger += w.totalLedgerBalanceVnd;
      totalJournal += w.journalComputedBalanceVnd;
      totalVariance += Math.abs(w.varianceVnd);
      if (w.state === 'frozen') frozenCount += 1;
    }

    return {
      totalAvailable,
      totalHeld,
      totalLedger,
      totalJournal,
      totalVariance,
      frozenCount,
      activeCount: wallets.length - frozenCount,
    };
  }, [wallets]);

  const filteredWallets = useMemo(() => {
    const q = walletSearchQuery.trim().toLowerCase();
    return wallets.filter((w) => {
      if (walletStateFilter !== 'all' && w.state !== walletStateFilter) {
        return false;
      }
      if (!q) return true;
      return (
        w.walletCode.toLowerCase().includes(q) ||
        w.walletNameVi.toLowerCase().includes(q) ||
        w.walletNameEn.toLowerCase().includes(q) ||
        w.ownerName.toLowerCase().includes(q) ||
        w.ownerSpendId.toLowerCase().includes(q)
      );
    });
  }, [wallets, walletSearchQuery, walletStateFilter]);

  const filteredAuditEvents = useMemo(() => {
    const q = auditSearchQuery.trim().toLowerCase();
    return auditEvents.filter((ev) => {
      if (auditModuleFilter !== 'all' && ev.module !== auditModuleFilter) {
        return false;
      }
      if (!q) return true;
      return (
        ev.referenceCode.toLowerCase().includes(q) ||
        ev.actionTitleVi.toLowerCase().includes(q) ||
        ev.actionTitleEn.toLowerCase().includes(q) ||
        ev.targetEntity.toLowerCase().includes(q) ||
        ev.reason.toLowerCase().includes(q) ||
        ev.operator.toLowerCase().includes(q)
      );
    });
  }, [auditEvents, auditSearchQuery, auditModuleFilter]);

  const filteredConfigParams = useMemo(() => {
    if (configCategoryFilter === 'all') return configParams;
    return configParams.filter((c) => c.category === configCategoryFilter);
  }, [configParams, configCategoryFilter]);

  // Modal helpers
  const openActionModal = (action: PendingLedgerAction) => {
    setPendingAction(action);
    setReasonInput('');
    setIsDeliberatelyConfirmed(false);
    setConfirmError(null);
  };

  const closeActionModal = () => {
    setPendingAction(null);
    setReasonInput('');
    setIsDeliberatelyConfirmed(false);
    setConfirmError(null);
  };

  useEffect(() => {
    if (!pendingAction) return;
    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        closeActionModal();
      }
    };
    window.addEventListener('keydown', onKeyDown);
    return () => window.removeEventListener('keydown', onKeyDown);
  }, [pendingAction]);

  const presetReasons = useMemo(() => {
    if (!pendingAction) return [];
    if (pendingAction.kind === 'toggle_wallet_freeze') {
      if (pendingAction.targetState === 'frozen') {
        return [
          tr(
            'Đóng băng tạm thời chiều rút/chuyển để bảo vệ số dư theo yêu cầu rà soát',
            'Temporarily freeze wallet outflow to protect balance under security review'
          ),
          tr(
            'Phát hiện bất thường phiên đăng nhập, tạm khóa ví chờ xác minh chủ tài khoản',
            'Suspicious session detected; freezing synthetic wallet pending owner verification'
          ),
        ];
      }
      return [
        tr(
          'Đã đối chiếu chứng từ & xác minh chủ ví hợp lệ, mở khóa đóng băng ví',
          'Verified supporting documents and owner identity; unfreezing synthetic wallet'
        ),
        tr(
          'Hoàn tất xử lý hồ sơ hỗ trợ, khôi phục trạng thái hoạt động bình thường',
          'Completed support case investigation; restoring normal wallet state'
        ),
      ];
    }
    if (pendingAction.kind === 'run_reconciliation_batch') {
      return [
        tr(
          'Đối soát định kỳ ca trực: kiểm tra cân bằng tổng số dư khả dụng + giữ chỗ với sổ cái',
          'Shift reconciliation check: verify liquid + reserved balances match journal totals'
        ),
      ];
    }
    return [
      tr(
        'Xác nhận kiểm tra các tham số bất biến sổ cái và chính sách ẩn khóa bí mật đầu ca trực',
        'Verified ledger invariant rules and redacted secret hygiene for current shift'
      ),
    ];
  }, [pendingAction, tr]);

  const handleConfirmAction = (e: React.FormEvent) => {
    e.preventDefault();
    if (!pendingAction) return;

    const trimmedReason = reasonInput.trim();
    if (trimmedReason.length < 6) {
      setConfirmError(
        tr(
          'Vui lòng nhập lý do nghiệp vụ cụ thể (tối thiểu 6 ký tự).',
          'Please enter a specific operational reason (at least 6 characters).'
        )
      );
      return;
    }

    if (!isDeliberatelyConfirmed) {
      setConfirmError(
        tr(
          'Vui lòng đánh dấu xác nhận chủ đích trước khi thực thi thao tác.',
          'Please check the deliberate confirmation box before executing this action.'
        )
      );
      return;
    }

    const nowStamp = '03/11/2026 · 10:30';
    const refSuffix = String(Math.floor(1000 + Math.random() * 9000));
    const operatorName = 'Trần Hoàng Nam · Ca 1';

    if (pendingAction.kind === 'toggle_wallet_freeze') {
      const refCode = `AUD-LED-20261103-${refSuffix}`;
      const isFreezing = pendingAction.targetState === 'frozen';

      onUpdateWallets((prev) =>
        prev.map((w) =>
          w.id === pendingAction.walletId
            ? {
                ...w,
                state: pendingAction.targetState,
                lastReconciledAt: nowStamp,
                statusReasonVi: trimmedReason,
                statusReasonEn: trimmedReason,
              }
            : w
        )
      );

      const newAudit: AdminGlobalAuditEvent = {
        id: `gaud_${Date.now()}`,
        referenceCode: refCode,
        timestamp: nowStamp,
        module: 'ledger',
        moduleLabelVi: 'Sổ cái & Ví',
        moduleLabelEn: 'Ledger & Wallets',
        actionTitleVi: isFreezing
          ? `Đóng băng ví mẫu ${pendingAction.walletCode}`
          : `Mở khóa đóng băng ví mẫu ${pendingAction.walletCode}`,
        actionTitleEn: isFreezing
          ? `Froze synthetic wallet ${pendingAction.walletCode}`
          : `Unfroze synthetic wallet ${pendingAction.walletCode}`,
        targetEntity: `${pendingAction.walletCode} · ${pendingAction.ownerName} (${pendingAction.ownerSpendId})`,
        detailVi: isFreezing
          ? 'Chuyển trạng thái ví: Hoạt động → Đang đóng băng (DEMO)'
          : 'Chuyển trạng thái ví: Đang đóng băng → Hoạt động (DEMO)',
        detailEn: isFreezing
          ? 'Wallet state transition: Active → Frozen (DEMO)'
          : 'Wallet state transition: Frozen → Active (DEMO)',
        reason: trimmedReason,
        operator: operatorName,
      };

      onAppendAuditEvent(newAudit);

      setLatestReceipt({
        referenceCode: refCode,
        timestamp: nowStamp,
        targetCode: pendingAction.walletCode,
        titleVi: isFreezing
          ? `Đã đóng băng ví mẫu ${pendingAction.walletCode}`
          : `Đã mở khóa đóng băng ví mẫu ${pendingAction.walletCode}`,
        titleEn: isFreezing
          ? `Synthetic wallet ${pendingAction.walletCode} frozen`
          : `Synthetic wallet ${pendingAction.walletCode} unfrozen`,
        summaryVi: isFreezing
          ? `Ví "${pendingAction.walletNameVi}" của ${pendingAction.ownerName} đã chuyển sang trạng thái Đang đóng băng và ghi nhận vào Nhật ký Kiểm toán.`
          : `Ví "${pendingAction.walletNameVi}" của ${pendingAction.ownerName} đã khôi phục trạng thái Hoạt động và ghi nhận vào Nhật ký Kiểm toán.`,
        summaryEn: isFreezing
          ? `Wallet "${pendingAction.walletNameEn}" (${pendingAction.ownerName}) is now Frozen and logged in the Audit Trail.`
          : `Wallet "${pendingAction.walletNameEn}" (${pendingAction.ownerName}) is now Active and logged in the Audit Trail.`,
        reason: trimmedReason,
        nonSettlementNoticeVi:
          'Mô phỏng DEMO: Thao tác chỉ cập nhật trạng thái ví mẫu cục bộ, không tác động tài khoản ngân hàng thực hay hệ thống thanh toán liên ngân hàng.',
        nonSettlementNoticeEn:
          'DEMO Simulation: Mutation updates local synthetic wallet state only; no real bank account or external payment rail is affected.',
      });

      closeActionModal();
      return;
    }

    if (pendingAction.kind === 'run_reconciliation_batch') {
      const refCode = `AUD-REC-20261103-${refSuffix}`;

      onUpdateWallets((prev) =>
        prev.map((w) => ({
          ...w,
          lastReconciledAt: nowStamp,
        }))
      );

      const newAudit: AdminGlobalAuditEvent = {
        id: `gaud_${Date.now()}`,
        referenceCode: refCode,
        timestamp: nowStamp,
        module: 'ledger',
        moduleLabelVi: 'Sổ cái & Ví',
        moduleLabelEn: 'Ledger & Wallets',
        actionTitleVi: `Chạy đối soát cân bằng toàn bộ ${pendingAction.walletCount} ví mẫu`,
        actionTitleEn: `Executed balance reconciliation across ${pendingAction.walletCount} synthetic wallets`,
        targetEntity: `${pendingAction.walletCount} ví mẫu · Chênh lệch = 0 ₫`,
        detailVi:
          'Tổng số dư sổ cái (Khả dụng + Giữ chỗ) khớp 100% với tổng bút toán Nợ/Có đối ứng',
        detailEn:
          'Total ledger balance (Liquid + Held) matched 100% against double-entry journal sum',
        reason: trimmedReason,
        operator: operatorName,
      };

      onAppendAuditEvent(newAudit);

      setLatestReceipt({
        referenceCode: refCode,
        timestamp: nowStamp,
        targetCode: 'LEDGER-RECON-ALL',
        titleVi: 'Hoàn tất đối soát cân bằng Sổ cái (Chênh lệch: 0 ₫)',
        titleEn: 'Completed Ledger Balance Reconciliation (Variance: 0 VND)',
        summaryVi: `Đã kiểm tra chéo ${pendingAction.walletCount} ví mẫu: Tổng số dư sổ cái ${formatVND(
          reconciliationMetrics.totalLedger
        )} khớp 100% với tổng bút toán nhật ký.`,
        summaryEn: `Cross-checked ${pendingAction.walletCount} synthetic wallets: Total ledger balance ${formatVND(
          reconciliationMetrics.totalLedger
        )} matched 100% with journal entries.`,
        reason: trimmedReason,
        nonSettlementNoticeVi:
          'Mô phỏng DEMO: Đối soát thực hiện trên dữ liệu sổ cái giả lập, không phát sinh quyết toán ngân hàng thực.',
        nonSettlementNoticeEn:
          'DEMO Simulation: Reconciliation verified against synthetic ledger fixtures with zero external settlement.',
      });

      closeActionModal();
      return;
    }

    // verify_system_config
    const refCode = `AUD-SYS-20261103-${refSuffix}`;

    onUpdateConfigParams((prev) =>
      prev.map((c) => ({
        ...c,
        lastVerifiedAt: nowStamp,
      }))
    );

    const newAudit: AdminGlobalAuditEvent = {
      id: `gaud_${Date.now()}`,
      referenceCode: refCode,
      timestamp: nowStamp,
      module: 'system',
      moduleLabelVi: 'Cấu hình Hệ thống',
      moduleLabelEn: 'System Config',
      actionTitleVi: `Xác nhận kiểm tra ${pendingAction.paramCount} tham số cấu hình & bảo mật khóa`,
      actionTitleEn: `Verified ${pendingAction.paramCount} system configuration & secret hygiene parameters`,
      targetEntity: 'SYSTEM_CONFIG_V2 · Redacted Secrets Verified',
      detailVi:
        'Xác nhận quy tắc bất biến sổ cái kép, hạn mức rủi ro và chính sách ẩn toàn bộ khóa bí mật',
      detailEn:
        'Confirmed double-entry invariants, velocity thresholds, and strict secret redaction policy',
      reason: trimmedReason,
      operator: operatorName,
    };

    onAppendAuditEvent(newAudit);

    setLatestReceipt({
      referenceCode: refCode,
      timestamp: nowStamp,
      targetCode: 'SYSTEM-CONFIG-V2',
      titleVi: 'Đã xác nhận kiểm tra Cấu hình Hệ thống & ghi Nhật ký Kiểm toán',
      titleEn: 'System Configuration verified & recorded in Audit Log',
      summaryVi: `Đã cập nhật mốc kiểm tra (${nowStamp}) cho ${pendingAction.paramCount} tham số hệ thống. Toàn bộ khóa tích hợp được ẩn an toàn.`,
      summaryEn: `Updated verification timestamp (${nowStamp}) across ${pendingAction.paramCount} system parameters. All integration secrets remain strictly redacted.`,
      reason: trimmedReason,
      nonSettlementNoticeVi:
        'Chính sách Bảo mật: Không có khóa API, webhook secret hay thông tin xác thực nào bị lộ trên giao diện.',
      nonSettlementNoticeEn:
        'Security Policy: Zero API keys, webhook secrets, or credentials are exposed in the browser.',
    });

    closeActionModal();
  };

  const resetTxFilters = () => {
    setTxSearchQuery('');
    setTxTypeFilter('all');
    setTxStatusFilter('all');
    setTxDirectionFilter('all');
    setTxWalletCodeFilter('all');
    setSimulatedTxError(false);
  };

  return (
    <div
      data-testid="sw-admin-screen-transactions-ledger"
      className="space-y-6 min-w-0"
    >
      {/* Compact Sub-navigation & Secondary Icon Quick-Action Bar */}
      <div className="sw-card p-3 sm:p-3.5 min-w-0 max-w-full flex items-center justify-between gap-2.5">
        <div
          role="tablist"
          aria-label={tr(
            'Phân hệ Giao dịch, Đối soát, Nhật ký quản trị và Cấu hình',
            'Transactions, Reconciliation, Audit Log, and System Configuration tabs'
          )}
          data-testid="sw-admin-ledger-subtabs"
          className="flex flex-wrap items-center gap-1.5 min-w-0 flex-1 overflow-x-auto max-w-full"
        >
          <button
            type="button"
            role="tab"
            aria-selected={activeSubTab === 'transactions'}
            data-testid="sw-admin-ledger-tab-transactions"
            onClick={() => onChangeSubTab('transactions')}
            className={`sw-focus-ring flex min-h-[36px] shrink-0 items-center gap-1.5 rounded-xl px-2.5 py-1 text-left text-[0.78125rem] font-bold transition-colors cursor-pointer whitespace-nowrap ${
              activeSubTab === 'transactions'
                ? 'bg-[var(--ink)] text-[var(--bg)] shadow-xs'
                : 'bg-[var(--sw-surface-muted)] text-[var(--ink2)] hover:text-[var(--ink)]'
            }`}
          >
            <ArrowLeftRight aria-hidden="true" className="h-3.5 w-3.5 shrink-0" />
            <span>{tr('Giao dịch & Biên lai', 'Transactions & Receipts')}</span>
            <span className="text-[0.72rem] font-extrabold tabular-nums opacity-85">
              {transactions.length}
            </span>
          </button>

          <button
            type="button"
            role="tab"
            aria-selected={activeSubTab === 'reconciliation'}
            data-testid="sw-admin-ledger-tab-reconciliation"
            onClick={() => onChangeSubTab('reconciliation')}
            className={`sw-focus-ring flex min-h-[36px] shrink-0 items-center gap-1.5 rounded-xl px-2.5 py-1 text-left text-[0.78125rem] font-bold transition-colors cursor-pointer whitespace-nowrap ${
              activeSubTab === 'reconciliation'
                ? 'bg-[var(--ink)] text-[var(--bg)] shadow-xs'
                : 'bg-[var(--sw-surface-muted)] text-[var(--ink2)] hover:text-[var(--ink)]'
            }`}
          >
            <Scale aria-hidden="true" className="h-3.5 w-3.5 shrink-0" />
            <span>{tr('Đối soát Số dư & Ví mẫu', 'Reconciliation & Wallets')}</span>
            <span className="text-[0.72rem] font-extrabold tabular-nums opacity-85">
              {wallets.length}
            </span>
          </button>

          <button
            type="button"
            role="tab"
            aria-selected={activeSubTab === 'audit'}
            data-testid="sw-admin-ledger-tab-audit"
            onClick={() => onChangeSubTab('audit')}
            className={`sw-focus-ring flex min-h-[36px] shrink-0 items-center gap-1.5 rounded-xl px-2.5 py-1 text-left text-[0.78125rem] font-bold transition-colors cursor-pointer whitespace-nowrap ${
              activeSubTab === 'audit'
                ? 'bg-[var(--ink)] text-[var(--bg)] shadow-xs'
                : 'bg-[var(--sw-surface-muted)] text-[var(--ink2)] hover:text-[var(--ink)]'
            }`}
          >
            <ClipboardList aria-hidden="true" className="h-3.5 w-3.5 shrink-0" />
            <span>{tr('Nhật ký quản trị', 'Audit Log')}</span>
            <span className="text-[0.72rem] font-extrabold tabular-nums opacity-85">
              {auditEvents.length}
            </span>
          </button>

          <button
            type="button"
            role="tab"
            aria-selected={activeSubTab === 'config'}
            data-testid="sw-admin-ledger-tab-config"
            onClick={() => onChangeSubTab('config')}
            className={`sw-focus-ring flex min-h-[36px] shrink-0 items-center gap-1.5 rounded-xl px-2.5 py-1 text-left text-[0.78125rem] font-bold transition-colors cursor-pointer whitespace-nowrap ${
              activeSubTab === 'config'
                ? 'bg-[var(--ink)] text-[var(--bg)] shadow-xs'
                : 'bg-[var(--sw-surface-muted)] text-[var(--ink2)] hover:text-[var(--ink)]'
            }`}
          >
            <Settings aria-hidden="true" className="h-3.5 w-3.5 shrink-0" />
            <span>{tr('Cấu hình hệ thống', 'System Configuration')}</span>
            <span className="text-[0.72rem] font-extrabold tabular-nums opacity-85">
              {configParams.length}
            </span>
          </button>
        </div>

        <div className="relative group/txsim shrink-0">
          <button
            type="button"
            data-testid="sw-admin-tx-simulate-error-btn"
            aria-pressed={simulatedTxError}
            aria-label={
              simulatedTxError
                ? tr('Khôi phục sổ cái mẫu', 'Restore ledger feed')
                : tr('Mô phỏng lỗi tải sổ cái', 'Simulate ledger error')
            }
            title={
              simulatedTxError
                ? tr('Khôi phục sổ cái mẫu', 'Restore ledger feed')
                : tr('Mô phỏng lỗi tải sổ cái', 'Simulate ledger error')
            }
            onClick={() => setSimulatedTxError((prev) => !prev)}
            className="sw-focus-ring sw-btn ghost sm !h-9 !w-9 !min-h-[36px] !min-w-[36px] !p-0 justify-center cursor-pointer"
          >
            <AlertTriangle aria-hidden="true" className="h-4 w-4 shrink-0" />
            <span className="sr-only">
              {simulatedTxError
                ? tr('Khôi phục sổ cái mẫu', 'Restore ledger feed')
                : tr('Mô phỏng lỗi tải sổ cái', 'Simulate ledger error')}
            </span>
          </button>
          <span
            role="tooltip"
            className="pointer-events-none invisible opacity-0 group-hover/txsim:visible group-hover/txsim:opacity-100 group-focus-within/txsim:visible group-focus-within/txsim:opacity-100 transition-opacity duration-150 absolute right-0 top-full mt-1.5 z-40 whitespace-nowrap rounded-lg border border-[var(--stroke)] bg-[var(--ink)] px-2.5 py-1 text-[0.72rem] font-bold text-[var(--bg)] shadow-lg"
          >
            {simulatedTxError
              ? tr('Khôi phục sổ cái mẫu', 'Restore ledger feed')
              : tr('Mô phỏng lỗi tải sổ cái', 'Simulate ledger error')}
          </span>
        </div>
      </div>

      {/* Explicit Demo Result Receipt Banner (shown after any wallet freeze/unfreeze, reconciliation, or config verification) */}
      {latestReceipt && (
        <div
          role="status"
          aria-live="polite"
          data-testid="sw-admin-ledger-action-result"
          className="sw-card p-4 sm:p-5 border border-[var(--sw-em)] bg-[var(--sw-em-s)]/60 space-y-2"
        >
          <div className="flex items-start justify-between gap-3">
            <div className="flex items-center gap-2 text-[0.8125rem] font-extrabold text-[var(--sw-em-d)]">
              <FileCheck2 aria-hidden="true" className="h-4 w-4 shrink-0" />
              <span>
                {tr(' kết quả thực thi DEMO', 'DEMO Execution Receipt')} ·{' '}
                <span className="tabular-nums">{latestReceipt.referenceCode}</span>
              </span>
            </div>
            <button
              type="button"
              data-testid="sw-admin-dismiss-ledger-result-btn"
              onClick={() => setLatestReceipt(null)}
              aria-label={tr('Đóng thông báo kết quả', 'Dismiss result receipt')}
              className="sw-focus-ring sw-ib !h-8 !w-8 !min-h-[32px] !min-w-[32px]"
            >
              <X aria-hidden="true" className="h-4 w-4" />
            </button>
          </div>

          <div className="font-display text-[1rem] font-extrabold text-[var(--ink)]">
            {tr(latestReceipt.titleVi, latestReceipt.titleEn)}
          </div>
          <p className="text-[0.84375rem] text-[var(--ink)] leading-relaxed">
            {tr(latestReceipt.summaryVi, latestReceipt.summaryEn)}
          </p>
          <div className="text-[0.8125rem] text-[var(--ink2)]">
            <span className="font-bold">{tr('Lý do ghi nhận:', 'Recorded reason:')}</span>{' '}
            {latestReceipt.reason}
          </div>
          <div className="pt-1 text-[0.75rem] font-semibold text-[var(--sw-em-d)]">
            {tr(
              latestReceipt.nonSettlementNoticeVi,
              latestReceipt.nonSettlementNoticeEn
            )}
          </div>
        </div>
      )}

      {/* =====================================================================
          SUB-TAB 1: TRANSACTIONS & RECEIPT INSPECTOR
          ===================================================================== */}
      {activeSubTab === 'transactions' && (
        <div className="space-y-4">
          {/* Search & Filter Controls */}
          <div className="sw-card p-4 sm:p-5 space-y-3">
            <div className="grid grid-cols-1 md:grid-cols-12 gap-3">
              {/* Search input */}
              <div className="md:col-span-4 relative">
                <Search
                  aria-hidden="true"
                  className="pointer-events-none absolute left-3.5 top-1/2 -translate-y-1/2 h-4 w-4 text-[var(--ink3)]"
                />
                <input
                  type="search"
                  data-testid="sw-admin-tx-search"
                  value={txSearchQuery}
                  onChange={(e) => setTxSearchQuery(e.target.value)}
                  placeholder={tr(
                    'Tìm mã GD, mã đối soát, ví, chủ ví, tài khoản Nợ/Có...',
                    'Search TX code, correlation ID, wallet, owner, GL account...'
                  )}
                  aria-label={tr(
                    'Tìm kiếm bút toán giao dịch sổ cái',
                    'Search ledger transactions'
                  )}
                  className="sw-in w-full !pl-10 !py-2.5 text-[0.84375rem]"
                />
              </div>

              {/* Type filter */}
              <div className="md:col-span-3">
                <label htmlFor="sw-admin-tx-type-select" className="sr-only">
                  {tr('Lọc theo loại giao dịch', 'Filter by transaction type')}
                </label>
                <select
                  id="sw-admin-tx-type-select"
                  data-testid="sw-admin-tx-type-filter"
                  value={txTypeFilter}
                  onChange={(e) =>
                    setTxTypeFilter(e.target.value as 'all' | AdminLedgerTxType)
                  }
                  className="sw-in w-full !py-2.5 text-[0.84375rem]"
                >
                  <option value="all">
                    {tr('Tất cả loại giao dịch', 'All transaction types')}
                  </option>
                  <option value="bank_topup">
                    {tr('Nạp ví từ Ngân hàng', 'Bank Wallet Top-Up')}
                  </option>
                  <option value="bank_withdrawal">
                    {tr('Rút tiền về Ngân hàng', 'Bank Withdrawal')}
                  </option>
                  <option value="p2p_transfer">
                    {tr('Chuyển/Nhận P2P nội bộ', 'Internal P2P Transfer')}
                  </option>
                  <option value="fund_contribution">
                    {tr('Đóng góp Quỹ nhóm', 'Group Fund Contribution')}
                  </option>
                  <option value="fund_disbursement">
                    {tr('Giải ngân Quỹ nhóm (24h)', 'Group Fund Disbursement (24h)')}
                  </option>
                  <option value="goal_allocation">
                    {tr('Phân bổ Mục tiêu Tiết kiệm', 'Savings Goal Allocation')}
                  </option>
                </select>
              </div>

              {/* Status filter */}
              <div className="md:col-span-3">
                <label htmlFor="sw-admin-tx-status-select" className="sr-only">
                  {tr('Lọc theo trạng thái sổ cái', 'Filter by ledger status')}
                </label>
                <select
                  id="sw-admin-tx-status-select"
                  data-testid="sw-admin-tx-status-filter"
                  value={txStatusFilter}
                  onChange={(e) =>
                    setTxStatusFilter(
                      e.target.value as 'all' | AdminLedgerTxStatus
                    )
                  }
                  className="sw-in w-full !py-2.5 text-[0.84375rem]"
                >
                  <option value="all">
                    {tr('Tất cả trạng thái sổ cái', 'All ledger statuses')}
                  </option>
                  <option value="settled_demo">
                    {tr('Hoàn tất sổ cái (DEMO)', 'Ledger Settled (DEMO)')}
                  </option>
                  <option value="held_reservation">
                    {tr('Đang giữ chỗ 24h / Mục tiêu', 'Reserved / 24h Hold')}
                  </option>
                  <option value="pending_reconcile">
                    {tr('Chờ khớp mã tham chiếu', 'Pending Reference Match')}
                  </option>
                  <option value="reversed_demo">
                    {tr('Đã hoàn trả sổ cái', 'Reversed to Wallet')}
                  </option>
                </select>
              </div>

              {/* Direction filter */}
              <div className="md:col-span-2">
                <label htmlFor="sw-admin-tx-direction-select" className="sr-only">
                  {tr('Lọc theo chiều dòng tiền', 'Filter by flow direction')}
                </label>
                <select
                  id="sw-admin-tx-direction-select"
                  data-testid="sw-admin-tx-direction-filter"
                  value={txDirectionFilter}
                  onChange={(e) =>
                    setTxDirectionFilter(
                      e.target.value as 'all' | 'credit' | 'debit'
                    )
                  }
                  className="sw-in w-full !py-2.5 text-[0.84375rem]"
                >
                  <option value="all">
                    {tr('Mọi chiều (Ghi Nợ/Có)', 'All directions')}
                  </option>
                  <option value="credit">
                    {tr('Ghi Có (+ Vào ví)', 'Credit (+ Inflow)')}
                  </option>
                  <option value="debit">
                    {tr('Ghi Nợ (- Ra/Giữ chỗ)', 'Debit (- Outflow/Hold)')}
                  </option>
                </select>
              </div>
            </div>

            {txWalletCodeFilter !== 'all' && (
              <div className="flex items-center justify-between gap-2 pt-2 border-t border-[var(--line)] text-[0.8125rem]">
                <span className="text-[var(--ink2)]">
                  {tr('Đang lọc theo ví mẫu:', 'Filtering by synthetic wallet:')}{' '}
                  <strong className="font-bold text-[var(--ink)] tabular-nums">
                    {txWalletCodeFilter}
                  </strong>
                </span>
                <button
                  type="button"
                  onClick={() => setTxWalletCodeFilter('all')}
                  className="sw-focus-ring sw-btn ghost sm"
                >
                  <span>{tr('Bỏ lọc ví', 'Clear wallet filter')}</span>
                </button>
              </div>
            )}
          </div>

          {simulatedTxError ? (
            <div
              role="alert"
              data-testid="sw-admin-tx-error-state"
              className="sw-card p-6 text-center space-y-3"
            >
              <div className="mx-auto flex h-11 w-11 items-center justify-center rounded-2xl bg-[var(--rose-s)] text-[var(--rose)]">
                <AlertTriangle aria-hidden="true" className="h-5 w-5" />
              </div>
              <h3 className="font-display text-[1rem] font-extrabold text-[var(--ink)]">
                {tr(
                  'Không thể tải danh sách bút toán sổ cái (Mô phỏng lỗi)',
                  'Unable to load ledger journal entries (Simulated error)'
                )}
              </h3>
              <p className="text-[0.84375rem] text-[var(--ink2)] max-w-md mx-auto">
                {tr(
                  'Kết nối tới bộ đọc chỉ mục sổ cái mẫu đang tạm gián đoạn để kiểm thử giao diện chịu lỗi.',
                  'Connection to the synthetic ledger index reader is paused to verify error state resilience.'
                )}
              </p>
              <div>
                <button
                  type="button"
                  data-testid="sw-admin-tx-error-retry-btn"
                  onClick={() => setSimulatedTxError(false)}
                  className="sw-focus-ring sw-btn sw-btn-pri sm"
                >
                  <RefreshCw aria-hidden="true" className="h-4 w-4" />
                  <span>{tr('Thử tải lại sổ cái', 'Retry loading ledger')}</span>
                </button>
              </div>
            </div>
          ) : (
            <div className="grid grid-cols-1 xl:grid-cols-12 gap-6 items-start min-w-0">
              {/* Left 7 cols: Paginated Transactions Table */}
              <div className="xl:col-span-7 min-w-0 sw-card overflow-hidden">
                <div className="flex flex-wrap items-center justify-between gap-2 p-4 sm:px-5 border-b border-[var(--line)]">
                  <div>
                    <h3 className="font-display text-[0.9375rem] font-extrabold text-[var(--ink)]">
                      {tr(
                        'Nhật ký Bút toán Giao dịch (DEMO)',
                        'Ledger Transaction Journal (DEMO)'
                      )}
                    </h3>
                    <div className="text-[0.75rem] text-[var(--ink2)]">
                      {tr(
                        `Hiển thị ${paginatedTransactions.length} / ${filteredTransactions.length} bút toán khớp bộ lọc`,
                        `Showing ${paginatedTransactions.length} of ${filteredTransactions.length} matching entries`
                      )}
                    </div>
                  </div>

                  {(txSearchQuery ||
                    txTypeFilter !== 'all' ||
                    txStatusFilter !== 'all' ||
                    txDirectionFilter !== 'all' ||
                    txWalletCodeFilter !== 'all') && (
                    <button
                      type="button"
                      data-testid="sw-admin-tx-reset-filters-btn"
                      onClick={resetTxFilters}
                      className="sw-focus-ring sw-btn ghost sm"
                    >
                      <span>{tr('Đặt lại bộ lọc', 'Reset filters')}</span>
                    </button>
                  )}
                </div>

                {filteredTransactions.length === 0 ? (
                  <div
                    data-testid="sw-admin-tx-empty"
                    className="p-8 text-center space-y-3"
                  >
                    <div className="font-display text-[0.9375rem] font-extrabold text-[var(--ink)]">
                      {tr(
                        'Không tìm thấy bút toán giao dịch phù hợp',
                        'No matching ledger transactions found'
                      )}
                    </div>
                    <p className="text-[0.8125rem] text-[var(--ink2)] max-w-sm mx-auto">
                      {tr(
                        'Thử xóa từ khóa tìm kiếm hoặc chọn lại bộ lọc loại giao dịch / trạng thái sổ cái.',
                        'Try clearing your search query or adjusting the transaction type and status filters.'
                      )}
                    </p>
                    <button
                      type="button"
                      onClick={resetTxFilters}
                      className="sw-focus-ring sw-btn ghost sm"
                    >
                      <span>{tr('Hiển thị toàn bộ bút toán', 'Show all entries')}</span>
                    </button>
                  </div>
                ) : (
                  <>
                    <div className="w-full max-w-full overflow-x-auto">
                      <table
                        aria-label={tr(
                          'Bảng giao dịch sổ cái phân trang',
                          'Paginated ledger transactions table'
                        )}
                        className="w-full text-left border-collapse"
                      >
                        <thead>
                          <tr className="border-b border-[var(--line)] text-[0.75rem] font-bold text-[var(--ink3)]">
                            <th scope="col" className="py-3 pl-4 pr-2">
                              {tr('Mã GD & Thời gian', 'TX Code & Time')}
                            </th>
                            <th scope="col" className="py-3 px-2">
                              {tr('Loại & Ví mẫu', 'Type & Wallet')}
                            </th>
                            <th scope="col" className="py-3 px-2 text-right">
                              {tr('Giá trị (VNĐ)', 'Amount (VND)')}
                            </th>
                            <th scope="col" className="py-3 pl-2 pr-4">
                              {tr('Trạng thái sổ cái', 'Ledger Status')}
                            </th>
                          </tr>
                        </thead>
                        <tbody className="divide-y divide-[var(--line)] text-[0.8125rem]">
                          {paginatedTransactions.map((tx) => {
                            const isSelected = selectedTransaction?.id === tx.id;
                            const statusMeta = getTxStatusMeta(tx.status, tr);
                            const isCredit = tx.direction === 'credit';

                            return (
                              <tr
                                key={tx.id}
                                data-testid={`sw-admin-tx-row-${tx.txCode}`}
                                aria-selected={isSelected}
                                onClick={() => setSelectedTxId(tx.id)}
                                className={`cursor-pointer transition-colors ${
                                  isSelected
                                    ? 'bg-[var(--sw-em-s)]/75'
                                    : 'hover:bg-[var(--sw-surface-muted)]/60'
                                }`}
                              >
                                <td className="py-3.5 pl-4 pr-2 align-top">
                                  <div className="font-bold text-[var(--ink)] tabular-nums">
                                    {tx.txCode}
                                  </div>
                                  <div className="text-[0.75rem] text-[var(--ink2)] tabular-nums">
                                    {tx.createdAt}
                                  </div>
                                  <div className="text-[0.6875rem] text-[var(--ink3)] tabular-nums truncate max-w-[165px]">
                                    {tx.correlationId}
                                  </div>
                                </td>

                                <td className="py-3.5 px-2 align-top">
                                  <div className="font-bold text-[var(--ink)]">
                                    {tr(tx.typeLabelVi, tx.typeLabelEn)}
                                  </div>
                                  <div className="text-[0.75rem] text-[var(--ink2)]">
                                    <span className="font-semibold tabular-nums">
                                      {tx.walletCode}
                                    </span>{' '}
                                    · {tx.ownerName}
                                  </div>
                                  <div className="text-[0.75rem] text-[var(--ink3)] truncate max-w-[220px]">
                                    {tr(tx.counterpartyLabelVi, tx.counterpartyLabelEn)}
                                  </div>
                                </td>

                                <td className="py-3.5 px-2 align-top text-right whitespace-nowrap">
                                  <div
                                    className={`font-display font-extrabold tabular-nums ${
                                      isCredit
                                        ? 'text-[var(--sw-em-d)]'
                                        : 'text-[var(--ink)]'
                                    }`}
                                  >
                                    {isCredit ? '+' : '-'}
                                    {formatVND(tx.amountVnd)}
                                  </div>
                                  <div className="text-[0.6875rem] text-[var(--ink3)] tabular-nums">
                                    {tr('Số dư sau:', 'Post:')}{' '}
                                    {formatVND(tx.postBalanceVnd)}
                                  </div>
                                </td>

                                <td className="py-3.5 pl-2 pr-4 align-top">
                                  <div
                                    className={`font-bold ${statusMeta.textClass}`}
                                  >
                                    {statusMeta.label}
                                  </div>
                                  <div className="text-[0.6875rem] text-[var(--ink3)] tabular-nums">
                                    {tx.debitAccountCode} → {tx.creditAccountCode}
                                  </div>
                                </td>
                              </tr>
                            );
                          })}
                        </tbody>
                      </table>
                    </div>

                    {/* Pagination Bar */}
                    <div
                      data-testid="sw-admin-tx-pagination"
                      className="flex items-center justify-between gap-2 px-4 py-3 border-t border-[var(--line)] text-[0.75rem] text-[var(--ink2)]"
                    >
                      <span className="tabular-nums">
                        {tr(
                          `Trang ${txSafePage} / ${txTotalPages} (Tổng ${filteredTransactions.length} bút toán)`,
                          `Page ${txSafePage} of ${txTotalPages} (${filteredTransactions.length} total)`
                        )}
                      </span>
                      <div className="flex items-center gap-1.5">
                        <button
                          type="button"
                          data-testid="sw-admin-tx-page-prev"
                          disabled={txSafePage <= 1}
                          onClick={() =>
                            setTxCurrentPage((p) => Math.max(1, p - 1))
                          }
                          className="sw-focus-ring sw-btn ghost sm !px-2.5 disabled:opacity-40"
                        >
                          <ChevronLeft aria-hidden="true" className="h-4 w-4" />
                          <span>{tr('Trước', 'Prev')}</span>
                        </button>
                        <button
                          type="button"
                          data-testid="sw-admin-tx-page-next"
                          disabled={txSafePage >= txTotalPages}
                          onClick={() =>
                            setTxCurrentPage((p) =>
                              Math.min(txTotalPages, p + 1)
                            )
                          }
                          className="sw-focus-ring sw-btn ghost sm !px-2.5 disabled:opacity-40"
                        >
                          <span>{tr('Sau', 'Next')}</span>
                          <ChevronRight aria-hidden="true" className="h-4 w-4" />
                        </button>
                      </div>
                    </div>
                  </>
                )}
              </div>

              {/* Right 5 cols: Transaction Receipt & Double-Entry Inspector */}
              <aside
                aria-label={tr(
                  'Chi tiết biên lai và bút toán sổ cái',
                  'Transaction receipt and double-entry ledger inspector'
                )}
                data-testid="sw-admin-tx-receipt-inspector"
                className="xl:col-span-5 min-w-0 sw-card p-5 space-y-4"
              >
                {selectedTransaction ? (
                  <>
                    <div className="flex items-start justify-between gap-3 border-b border-[var(--line)] pb-3.5">
                      <div>
                        <div className="flex items-center gap-2 text-[0.75rem] font-bold text-[var(--sw-em-d)]">
                          <Receipt aria-hidden="true" className="h-4 w-4" />
                          <span>
                            {tr(
                              'Biên lai & Bút toán Sổ cái (DEMO)',
                              'Transaction Receipt & Journal Inspector (DEMO)'
                            )}
                          </span>
                        </div>
                        <h3 className="mt-1 font-display text-[1.125rem] font-extrabold text-[var(--ink)] tabular-nums">
                          {selectedTransaction.txCode}
                        </h3>
                        <div className="text-[0.75rem] text-[var(--ink2)]">
                          {tr(
                            selectedTransaction.typeLabelVi,
                            selectedTransaction.typeLabelEn
                          )}{' '}
                          · {selectedTransaction.createdAt}
                        </div>
                      </div>

                      <div className="text-right">
                        <div
                          className={`font-display text-[1.125rem] font-extrabold tabular-nums ${
                            selectedTransaction.direction === 'credit'
                              ? 'text-[var(--sw-em-d)]'
                              : 'text-[var(--ink)]'
                          }`}
                        >
                          {selectedTransaction.direction === 'credit' ? '+' : '-'}
                          {formatVND(selectedTransaction.amountVnd)}
                        </div>
                        <div
                          className={`text-[0.75rem] font-bold ${
                            getTxStatusMeta(selectedTransaction.status, tr)
                              .textClass
                          }`}
                        >
                          {getTxStatusMeta(selectedTransaction.status, tr).label}
                        </div>
                      </div>
                    </div>

                    {/* Key-Value Ledger Receipt Fields */}
                    <div className="space-y-2 text-[0.8125rem]">
                      <div className="sw-kv">
                        <span className="text-[var(--ink2)]">
                          {tr('Mã đối soát (Correlation ID)', 'Correlation ID')}
                        </span>
                        <span className="font-bold text-[var(--ink)] tabular-nums">
                          {selectedTransaction.correlationId}
                        </span>
                      </div>
                      <div className="sw-kv">
                        <span className="text-[var(--ink2)]">
                          {tr('Khóa chống trùng (Idempotency)', 'Idempotency Key')}
                        </span>
                        <span className="font-semibold text-[var(--ink)] tabular-nums">
                          {selectedTransaction.idempotencyKeyMasked}
                        </span>
                      </div>
                      <div className="sw-kv">
                        <span className="text-[var(--ink2)]">
                          {tr('Ví mẫu & Chủ sở hữu', 'Synthetic Wallet & Owner')}
                        </span>
                        <span className="font-bold text-[var(--ink)] text-right">
                          {selectedTransaction.walletCode} ·{' '}
                          {selectedTransaction.ownerName} (
                          {selectedTransaction.ownerSpendId})
                        </span>
                      </div>
                      <div className="sw-kv">
                        <span className="text-[var(--ink2)]">
                          {tr('Đối tác / Nguồn nhận', 'Counterparty')}
                        </span>
                        <span className="font-semibold text-[var(--ink)] text-right">
                          {tr(
                            selectedTransaction.counterpartyLabelVi,
                            selectedTransaction.counterpartyLabelEn
                          )}
                        </span>
                      </div>
                      <div className="sw-kv">
                        <span className="text-[var(--ink2)]">
                          {tr('Tài khoản Ghi Nợ (Debit GL)', 'Debit Account (GL)')}
                        </span>
                        <span className="font-bold text-[var(--ink)] tabular-nums">
                          {selectedTransaction.debitAccountCode}
                        </span>
                      </div>
                      <div className="sw-kv">
                        <span className="text-[var(--ink2)]">
                          {tr('Tài khoản Ghi Có (Credit GL)', 'Credit Account (GL)')}
                        </span>
                        <span className="font-bold text-[var(--ink)] tabular-nums">
                          {selectedTransaction.creditAccountCode}
                        </span>
                      </div>
                      <div className="sw-kv">
                        <span className="text-[var(--ink2)]">
                          {tr('Biến động số dư khả dụng', 'Liquid Balance Shift')}
                        </span>
                        <span className="font-bold text-[var(--ink)] tabular-nums">
                          {formatVND(selectedTransaction.preBalanceVnd)} →{' '}
                          {formatVND(selectedTransaction.postBalanceVnd)}
                        </span>
                      </div>
                      <div className="sw-kv">
                        <span className="text-[var(--ink2)]">
                          {tr('Phương thức xác thực', 'Auth Method')}
                        </span>
                        <span className="font-semibold text-[var(--ink)]">
                          {tr(
                            selectedTransaction.authMethodVi,
                            selectedTransaction.authMethodEn
                          )}
                        </span>
                      </div>
                    </div>

                    {/* Description & Reconciliation Note */}
                    <div className="space-y-2 pt-2 border-t border-[var(--line)] text-[0.8125rem]">
                      <div>
                        <div className="text-[0.75rem] font-bold text-[var(--ink2)]">
                          {tr('Diễn giải nghiệp vụ', 'Transaction Description')}
                        </div>
                        <p className="text-[var(--ink)] mt-0.5 leading-relaxed">
                          {tr(
                            selectedTransaction.descriptionVi,
                            selectedTransaction.descriptionEn
                          )}
                        </p>
                      </div>
                      <div>
                        <div className="text-[0.75rem] font-bold text-[var(--ink2)]">
                          {tr('Ghi chú đối soát sổ cái', 'Reconciliation Note')}
                        </div>
                        <p className="font-semibold text-[var(--sw-em-d)] mt-0.5 leading-relaxed">
                          {tr(
                            selectedTransaction.reconciliationNoteVi,
                            selectedTransaction.reconciliationNoteEn
                          )}
                        </p>
                      </div>
                    </div>

                    {/* Linked Synthetic Wallet Quick Control */}
                    {selectedTxWallet && (
                      <div className="pt-3 border-t border-[var(--line)] space-y-2.5">
                        <div className="flex items-center justify-between gap-2 text-[0.8125rem]">
                          <span className="font-bold text-[var(--ink2)]">
                            {tr('Trạng thái ví liên kết:', 'Linked wallet state:')}{' '}
                            <span className="text-[var(--ink)] tabular-nums">
                              {selectedTxWallet.walletCode}
                            </span>
                          </span>
                          <span
                            data-testid="sw-admin-inspector-wallet-state"
                            className={`font-extrabold ${
                              getWalletStateMeta(selectedTxWallet.state, tr)
                                .textClass
                            }`}
                          >
                            {getWalletStateMeta(selectedTxWallet.state, tr).label}
                          </span>
                        </div>

                        <div className="flex flex-wrap items-center gap-2">
                          <button
                            type="button"
                            data-testid="sw-admin-inspector-toggle-wallet-freeze-btn"
                            onClick={() =>
                              openActionModal({
                                kind: 'toggle_wallet_freeze',
                                walletId: selectedTxWallet.id,
                                walletCode: selectedTxWallet.walletCode,
                                walletNameVi: selectedTxWallet.walletNameVi,
                                walletNameEn: selectedTxWallet.walletNameEn,
                                ownerName: selectedTxWallet.ownerName,
                                ownerSpendId: selectedTxWallet.ownerSpendId,
                                currentState: selectedTxWallet.state,
                                targetState:
                                  selectedTxWallet.state === 'active'
                                    ? 'frozen'
                                    : 'active',
                              })
                            }
                            className="sw-focus-ring sw-btn ghost sm flex-1 justify-center whitespace-nowrap"
                          >
                            {selectedTxWallet.state === 'active' ? (
                              <>
                                <Lock aria-hidden="true" className="h-3.5 w-3.5" />
                                <span>
                                  {tr(
                                    'Đóng băng ví này (DEMO)',
                                    'Freeze this wallet (DEMO)'
                                  )}
                                </span>
                              </>
                            ) : (
                              <>
                                <Unlock
                                  aria-hidden="true"
                                  className="h-3.5 w-3.5"
                                />
                                <span>
                                  {tr(
                                    'Mở khóa ví này (DEMO)',
                                    'Unfreeze this wallet (DEMO)'
                                  )}
                                </span>
                              </>
                            )}
                          </button>

                          <button
                            type="button"
                            data-testid="sw-admin-inspector-jump-reconcile-btn"
                            onClick={() => onChangeSubTab('reconciliation')}
                            className="sw-focus-ring sw-btn ghost sm whitespace-nowrap"
                          >
                            <Scale aria-hidden="true" className="h-3.5 w-3.5" />
                            <span>
                              {tr('Bảng đối soát ví', 'Wallet Reconciliation')}
                            </span>
                          </button>
                        </div>
                      </div>
                    )}

                    {/* Explicit Non-Settlement Footer */}
                    <div className="pt-2 border-t border-[var(--line)] text-[0.75rem] text-[var(--ink3)] leading-relaxed">
                      {tr(
                        'Lưu ý minh bạch: Biên lai sổ cái này chỉ phục vụ kiểm thử vận hành DEMO. Không có lệnh chuyển tiền Napas 247 hay quyết toán ngân hàng thực nào được thực thi.',
                        'Transparency Notice: This ledger receipt is for DEMO operational inspection only. No real Napas 247 or external bank settlement has occurred.'
                      )}
                    </div>
                  </>
                ) : null}
              </aside>
            </div>
          )}
        </div>
      )}

      {/* =====================================================================
          SUB-TAB 2: LEDGER BALANCE RECONCILIATION & SYNTHETIC WALLET FREEZE
          ===================================================================== */}
      {activeSubTab === 'reconciliation' && (
        <div
          data-testid="sw-admin-reconciliation-view"
          className="space-y-5 min-w-0"
        >
          {/* Top Reconciliation Summary Strip */}
          <div className="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-4 gap-3.5">
            <div className="sw-card p-4 space-y-1 min-w-0">
              <div className="text-[0.75rem] font-bold text-[var(--ink2)] truncate">
                {tr(
                  'Tổng số dư Sổ cái (Khả dụng + Giữ chỗ)',
                  'Total Ledger Liability (Liquid + Held)'
                )}
              </div>
              <div
                data-testid="sw-admin-reconcile-total-ledger"
                className="font-display text-[1.25rem] sm:text-[1.3125rem] font-extrabold text-[var(--ink)] tabular-nums truncate"
              >
                {formatVND(reconciliationMetrics.totalLedger)}
              </div>
              <div className="text-[0.72rem] text-[var(--ink3)] tabular-nums truncate">
                {tr('Khả dụng:', 'Liquid:')}{' '}
                {formatVND(reconciliationMetrics.totalAvailable)} ·{' '}
                {tr('Giữ chỗ:', 'Held:')}{' '}
                {formatVND(reconciliationMetrics.totalHeld)}
              </div>
            </div>

            <div className="sw-card p-4 space-y-1 min-w-0">
              <div className="text-[0.75rem] font-bold text-[var(--ink2)] truncate">
                {tr(
                  'Tổng bút toán Nợ/Có đối ứng (Journal)',
                  'Computed Journal Entry Sum'
                )}
              </div>
              <div
                data-testid="sw-admin-reconcile-total-journal"
                className="font-display text-[1.25rem] sm:text-[1.3125rem] font-extrabold text-[var(--ink)] tabular-nums truncate"
              >
                {formatVND(reconciliationMetrics.totalJournal)}
              </div>
              <div className="text-[0.72rem] text-[var(--sw-em-d)] font-semibold truncate">
                {tr(
                  'Cân bằng tuyệt đối theo mã đối soát',
                  'Strictly balanced across correlation IDs'
                )}
              </div>
            </div>

            <div className="sw-card p-4 space-y-1 min-w-0">
              <div className="text-[0.75rem] font-bold text-[var(--ink2)] truncate">
                {tr(
                  'Chênh lệch Đối soát (Variance)',
                  'Reconciliation Variance'
                )}
              </div>
              <div
                data-testid="sw-admin-reconcile-variance"
                className="font-display text-[1.25rem] sm:text-[1.3125rem] font-extrabold text-[var(--sw-em-d)] tabular-nums truncate"
              >
                {formatVND(reconciliationMetrics.totalVariance)}
              </div>
              <div className="text-[0.72rem] text-[var(--sw-em-d)] font-bold truncate">
                {tr('Khớp 100% · Không lệch sổ cái', '100% Matched · Zero Variance')}
              </div>
            </div>

            <div className="sw-card p-4 space-y-1 min-w-0">
              <div className="text-[0.75rem] font-bold text-[var(--ink2)] truncate">
                {tr('Trạng thái Ví mẫu (Active / Frozen)', 'Synthetic Wallet States')}
              </div>
              <div className="font-display text-[1.25rem] sm:text-[1.3125rem] font-extrabold text-[var(--ink)] tabular-nums truncate">
                {reconciliationMetrics.activeCount}{' '}
                <span className="text-[0.875rem] font-bold text-[var(--sw-em-d)]">
                  {tr('mở', 'active')}
                </span>{' '}
                / {reconciliationMetrics.frozenCount}{' '}
                <span className="text-[0.875rem] font-bold text-[var(--rose)]">
                  {tr('đóng băng', 'frozen')}
                </span>
              </div>
              <div className="text-[0.72rem] text-[var(--ink3)] truncate">
                {tr(
                  'Yêu cầu xác nhận & lý do khi đóng băng/mở khóa',
                  'Requires confirmation & reason to freeze/unfreeze'
                )}
              </div>
            </div>
          </div>

          {/* Wallets Filter & Batch Reconcile Bar */}
          <div className="sw-card p-4 sm:p-5 space-y-4 min-w-0 overflow-hidden">
            <div className="flex flex-col md:flex-row md:items-center md:justify-between gap-3">
              <div className="flex flex-1 flex-col sm:flex-row items-stretch sm:items-center gap-3">
                <div className="relative flex-1">
                  <Search
                    aria-hidden="true"
                    className="pointer-events-none absolute left-3.5 top-1/2 -translate-y-1/2 h-4 w-4 text-[var(--ink3)]"
                  />
                  <input
                    type="search"
                    data-testid="sw-admin-wallet-search"
                    value={walletSearchQuery}
                    onChange={(e) => setWalletSearchQuery(e.target.value)}
                    placeholder={tr(
                      'Tìm mã ví (WAL-...), tên ví, chủ sở hữu, SpendID...',
                      'Search wallet code (WAL-...), wallet name, owner, SpendID...'
                    )}
                    aria-label={tr(
                      'Tìm kiếm ví mẫu đối soát',
                      'Search synthetic wallets'
                    )}
                    className="sw-in w-full !pl-10 !py-2.5 text-[0.84375rem]"
                  />
                </div>

                <div>
                  <label htmlFor="sw-admin-wallet-state-select" className="sr-only">
                    {tr('Lọc theo trạng thái ví', 'Filter by wallet state')}
                  </label>
                  <select
                    id="sw-admin-wallet-state-select"
                    data-testid="sw-admin-wallet-state-filter"
                    value={walletStateFilter}
                    onChange={(e) =>
                      setWalletStateFilter(
                        e.target.value as 'all' | AdminSyntheticWalletState
                      )
                    }
                    className="sw-in w-full sm:w-auto !py-2.5 text-[0.84375rem]"
                  >
                    <option value="all">
                      {tr('Tất cả trạng thái ví', 'All wallet states')}
                    </option>
                    <option value="active">
                      {tr('Đang hoạt động (Active)', 'Active wallets')}
                    </option>
                    <option value="frozen">
                      {tr('Đang đóng băng (Frozen)', 'Frozen wallets')}
                    </option>
                  </select>
                </div>
              </div>

              <button
                type="button"
                data-testid="sw-admin-reconcile-all-btn"
                onClick={() =>
                  openActionModal({
                    kind: 'run_reconciliation_batch',
                    walletCount: wallets.length,
                  })
                }
                className="sw-focus-ring sw-btn sw-btn-pri sm whitespace-nowrap justify-center"
              >
                <Scale aria-hidden="true" className="h-4 w-4 shrink-0" />
                <span>
                  {tr(
                    'Chạy đối soát cân bằng Sổ cái (DEMO)',
                    'Run Ledger Reconciliation (DEMO)'
                  )}
                </span>
              </button>
            </div>

            {/* Synthetic Wallets Table */}
            <div className="overflow-x-auto border-t border-[var(--line)] pt-2">
              <table
                data-testid="sw-admin-wallets-table"
                aria-label={tr(
                  'Bảng đối soát số dư và quản lý đóng băng ví mẫu',
                  'Synthetic wallet balance reconciliation and freeze control table'
                )}
                className="w-full text-left border-collapse"
              >
                <thead>
                  <tr className="border-b border-[var(--line)] text-[0.75rem] font-bold text-[var(--ink3)]">
                    <th scope="col" className="py-3 pl-2 pr-3">
                      {tr('Mã Ví & Chủ sở hữu', 'Wallet Code & Owner')}
                    </th>
                    <th scope="col" className="py-3 px-3 text-right">
                      {tr('Khả dụng / Giữ chỗ', 'Liquid / Held')}
                    </th>
                    <th scope="col" className="py-3 px-3 text-right">
                      {tr('Tổng Sổ cái vs Nhật ký', 'Ledger vs Journal')}
                    </th>
                    <th scope="col" className="py-3 px-3">
                      {tr('Trạng thái & Lý do hiện tại', 'State & Current Reason')}
                    </th>
                    <th scope="col" className="py-3 pl-3 pr-2 text-right">
                      {tr('Thao tác bảo vệ (DEMO)', 'Control Action (DEMO)')}
                    </th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-[var(--line)] text-[0.8125rem]">
                  {filteredWallets.map((w) => {
                    const stateMeta = getWalletStateMeta(w.state, tr);
                    const isFrozen = w.state === 'frozen';

                    return (
                      <tr
                        key={w.id}
                        data-testid={`sw-admin-wallet-row-${w.walletCode}`}
                        className="hover:bg-[var(--sw-surface-muted)]/50 transition-colors"
                      >
                        <td className="py-4 pl-2 pr-3 align-top">
                          <div className="font-bold text-[var(--ink)] tabular-nums">
                            {w.walletCode}
                          </div>
                          <div className="text-[0.8125rem] font-semibold text-[var(--ink)]">
                            {tr(w.walletNameVi, w.walletNameEn)}
                          </div>
                          <div className="text-[0.75rem] text-[var(--ink2)]">
                            {w.ownerName} ({w.ownerSpendId}) ·{' '}
                            {tr(w.walletKindVi, w.walletKindEn)}
                          </div>
                        </td>

                        <td className="py-4 px-3 align-top text-right whitespace-nowrap tabular-nums">
                          <div className="font-bold text-[var(--ink)]">
                            {formatVND(w.availableBalanceVnd)}
                          </div>
                          <div className="text-[0.75rem] text-[var(--ink2)]">
                            {tr('Giữ chỗ:', 'Held:')} {formatVND(w.heldBalanceVnd)}
                          </div>
                        </td>

                        <td className="py-4 px-3 align-top text-right whitespace-nowrap tabular-nums">
                          <div className="font-display font-extrabold text-[var(--ink)]">
                            {formatVND(w.totalLedgerBalanceVnd)}
                          </div>
                          <div className="text-[0.75rem] text-[var(--sw-em-d)] font-bold">
                            {tr('Lệch:', 'Var:')} {formatVND(w.varianceVnd)} ·{' '}
                            {tr('Khớp 100%', 'Matched')}
                          </div>
                          <div className="text-[0.6875rem] text-[var(--ink3)]">
                            {tr('Đối soát:', 'Checked:')} {w.lastReconciledAt}
                          </div>
                        </td>

                        <td className="py-4 px-3 align-top max-w-[270px]">
                          <div
                            data-testid={`sw-admin-wallet-state-${w.walletCode}`}
                            className={`font-extrabold ${stateMeta.textClass}`}
                          >
                            {stateMeta.label}
                          </div>
                          <p className="text-[0.75rem] text-[var(--ink2)] mt-0.5 leading-snug">
                            {tr(w.statusReasonVi, w.statusReasonEn)}
                          </p>
                        </td>

                        <td className="py-4 pl-3 pr-2 align-top text-right whitespace-nowrap">
                          <div className="flex flex-col sm:flex-row items-end sm:items-center justify-end gap-2">
                            <button
                              type="button"
                              data-testid={`sw-admin-wallet-view-tx-${w.walletCode}`}
                              onClick={() => {
                                setTxWalletCodeFilter(w.walletCode);
                                onChangeSubTab('transactions');
                              }}
                              className="sw-focus-ring sw-btn ghost sm"
                            >
                              <span>{tr('Xem bút toán', 'View TXs')}</span>
                            </button>

                            <button
                              type="button"
                              data-testid={`sw-admin-wallet-toggle-freeze-${w.walletCode}`}
                              onClick={() =>
                                openActionModal({
                                  kind: 'toggle_wallet_freeze',
                                  walletId: w.id,
                                  walletCode: w.walletCode,
                                  walletNameVi: w.walletNameVi,
                                  walletNameEn: w.walletNameEn,
                                  ownerName: w.ownerName,
                                  ownerSpendId: w.ownerSpendId,
                                  currentState: w.state,
                                  targetState: isFrozen ? 'active' : 'frozen',
                                })
                              }
                              className={`sw-focus-ring sw-btn sm ${
                                isFrozen ? 'sw-btn-pri' : 'ghost'
                              }`}
                            >
                              {isFrozen ? (
                                <>
                                  <Unlock
                                    aria-hidden="true"
                                    className="h-3.5 w-3.5"
                                  />
                                  <span>
                                    {tr('Mở khóa ví', 'Unfreeze Wallet')}
                                  </span>
                                </>
                              ) : (
                                <>
                                  <Lock
                                    aria-hidden="true"
                                    className="h-3.5 w-3.5"
                                  />
                                  <span>
                                    {tr('Đóng băng ví', 'Freeze Wallet')}
                                  </span>
                                </>
                              )}
                            </button>
                          </div>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          </div>
        </div>
      )}

      {/* =====================================================================
          SUB-TAB 3: AUDIT LOG VIEW (NHẬT KÝ KIỂM TOÁN HỆ THỐNG)
          ===================================================================== */}
      {activeSubTab === 'audit' && (
        <div
          data-testid="sw-admin-audit-log-view"
          className="sw-card p-4 sm:p-5 space-y-4"
        >
          <div className="flex flex-col md:flex-row md:items-center md:justify-between gap-3">
            <div>
              <h3 className="font-display text-[1rem] font-extrabold text-[var(--ink)]">
                {tr(
                  'Nhật ký Kiểm toán Vận hành & Sổ cái (DEMO)',
                  'Operational & Ledger Audit Log (DEMO)'
                )}
              </h3>
              <p className="text-[0.8125rem] text-[var(--ink2)]">
                {tr(
                  'Ghi nhận tức thời mọi thao tác đóng băng/mở khóa ví mẫu, đối soát sổ cái, xác nhận cấu hình và bảo mật người dùng trong phiên.',
                  'Records all synthetic wallet freeze/unfreeze actions, ledger reconciliations, config verifications, and user security mutations.'
                )}
              </p>
            </div>

            <div className="flex flex-col sm:flex-row items-stretch sm:items-center gap-2.5">
              <div className="relative">
                <Search
                  aria-hidden="true"
                  className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-[var(--ink3)]"
                />
                <input
                  type="search"
                  data-testid="sw-admin-audit-search"
                  value={auditSearchQuery}
                  onChange={(e) => setAuditSearchQuery(e.target.value)}
                  placeholder={tr(
                    'Tìm mã AUD-..., đối tượng, lý do...',
                    'Search AUD-... code, entity, reason...'
                  )}
                  aria-label={tr('Tìm kiếm nhật ký kiểm toán', 'Search audit log')}
                  className="sw-in !pl-9 !py-2 text-[0.8125rem]"
                />
              </div>

              <select
                data-testid="sw-admin-audit-module-filter"
                value={auditModuleFilter}
                onChange={(e) =>
                  setAuditModuleFilter(
                    e.target.value as 'all' | AdminGlobalAuditEvent['module']
                  )
                }
                aria-label={tr(
                  'Lọc theo phân hệ kiểm toán',
                  'Filter audit log by module'
                )}
                className="sw-in !py-2 text-[0.8125rem]"
              >
                <option value="all">
                  {tr('Tất cả phân hệ', 'All modules')}
                </option>
                <option value="ledger">
                  {tr('Sổ cái & Ví (Ledger)', 'Ledger & Wallets')}
                </option>
                <option value="users">
                  {tr('Bảo mật Người dùng (Users)', 'User Security')}
                </option>
                <option value="support">
                  {tr('Hỗ trợ & Hoàn tiền (Support)', 'Support & Refunds')}
                </option>
                <option value="system">
                  {tr('Cấu hình Hệ thống (System)', 'System Config')}
                </option>
              </select>
            </div>
          </div>

          <div className="divide-y divide-[var(--line)] border-t border-[var(--line)]">
            {filteredAuditEvents.length === 0 ? (
              <div className="py-8 text-center text-[0.84375rem] text-[var(--ink2)]">
                {tr(
                  'Không có bản ghi kiểm toán nào khớp bộ lọc hiện tại.',
                  'No audit log entries match the current filter.'
                )}
              </div>
            ) : (
              filteredAuditEvents.map((ev) => (
                <div
                  key={ev.id}
                  data-testid={`sw-admin-audit-item-${ev.referenceCode}`}
                  className="py-3.5 flex flex-col lg:flex-row lg:items-start lg:justify-between gap-2 text-[0.8125rem]"
                >
                  <div className="space-y-1">
                    <div className="flex flex-wrap items-center gap-2">
                      <span className="font-bold text-[var(--sw-em-d)] tabular-nums">
                        {ev.referenceCode}
                      </span>
                      <span className="text-[var(--ink3)]">·</span>
                      <span className="font-semibold text-[var(--ink2)]">
                        {tr(ev.moduleLabelVi, ev.moduleLabelEn)}
                      </span>
                      <span className="text-[var(--ink3)]">·</span>
                      <span className="text-[var(--ink2)] tabular-nums">
                        {ev.timestamp}
                      </span>
                    </div>

                    <div className="font-display text-[0.9375rem] font-extrabold text-[var(--ink)]">
                      {tr(ev.actionTitleVi, ev.actionTitleEn)}
                    </div>

                    <div className="text-[var(--ink2)]">
                      <span className="font-semibold text-[var(--ink)]">
                        {tr('Đối tượng:', 'Target:')}
                      </span>{' '}
                      {ev.targetEntity} — {tr(ev.detailVi, ev.detailEn)}
                    </div>

                    <div className="text-[var(--ink)]">
                      <span className="font-bold text-[var(--ink2)]">
                        {tr('Lý do xác nhận:', 'Confirmed reason:')}
                      </span>{' '}
                      {ev.reason}
                    </div>
                  </div>

                  <div className="text-[0.75rem] font-semibold text-[var(--ink2)] lg:text-right shrink-0">
                    {tr('Người thực hiện:', 'Operator:')} {ev.operator}
                  </div>
                </div>
              ))
            )}
          </div>
        </div>
      )}

      {/* =====================================================================
          SUB-TAB 4: SYSTEM CONFIGURATION VIEW (CẤU HÌNH HỆ THỐNG & BẤT BIẾN SỔ CÁI)
          ===================================================================== */}
      {activeSubTab === 'config' && (
        <div
          data-testid="sw-admin-system-config-view"
          className="sw-card p-4 sm:p-5 space-y-4"
        >
          <div className="flex flex-col lg:flex-row lg:items-center lg:justify-between gap-3">
            <div>
              <h3 className="font-display text-[1rem] font-extrabold text-[var(--ink)]">
                {tr(
                  'Cấu hình Bất biến Sổ cái & Chính sách Bảo mật Khóa (DEMO)',
                  'Ledger Invariants & Secret Hygiene Configuration (DEMO)'
                )}
              </h3>
              <p className="text-[0.8125rem] text-[var(--ink2)]">
                {tr(
                  'Chế độ chỉ đọc an toàn đối với khóa tích hợp ([REDACTED_SECRET]). Cho phép xác nhận kiểm tra cấu hình đầu ca trực để ghi Nhật ký Kiểm toán.',
                  'Read-safe inspection with strict secret redaction ([REDACTED_SECRET]). Operators can record a shift configuration verification in the Audit Log.'
                )}
              </p>
            </div>

            <div className="flex flex-wrap items-center gap-2.5 shrink-0">
              <select
                data-testid="sw-admin-config-category-filter"
                value={configCategoryFilter}
                onChange={(e) =>
                  setConfigCategoryFilter(
                    e.target.value as
                      | 'all'
                      | AdminSystemConfigParameter['category']
                  )
                }
                aria-label={tr(
                  'Lọc nhóm cấu hình hệ thống',
                  'Filter system configuration category'
                )}
                className="sw-in !py-2 text-[0.8125rem]"
              >
                <option value="all">
                  {tr('Tất cả nhóm cấu hình', 'All config categories')}
                </option>
                <option value="ledger_invariants">
                  {tr('Bất biến Sổ cái & Đối soát', 'Ledger Invariants')}
                </option>
                <option value="velocity_risk">
                  {tr('Hạn mức & Kiểm soát Rủi ro', 'Velocity & Risk')}
                </option>
                <option value="integrations_security">
                  {tr('Tích hợp & Bảo mật Khóa', 'Integrations & Secret Hygiene')}
                </option>
              </select>

              <button
                type="button"
                data-testid="sw-admin-verify-config-btn"
                onClick={() =>
                  openActionModal({
                    kind: 'verify_system_config',
                    paramCount: configParams.length,
                  })
                }
                className="sw-focus-ring sw-btn sw-btn-pri sm whitespace-nowrap"
              >
                <ShieldCheck aria-hidden="true" className="h-4 w-4 shrink-0" />
                <span>
                  {tr(
                    'Xác nhận kiểm tra cấu hình ca trực (DEMO)',
                    'Verify Shift Config & Log Audit (DEMO)'
                  )}
                </span>
              </button>
            </div>
          </div>

          <div className="divide-y divide-[var(--line)] border-t border-[var(--line)]">
            {filteredConfigParams.map((param) => (
              <div
                key={param.id}
                data-testid={`sw-admin-config-row-${param.keyName}`}
                className="py-4 flex flex-col lg:flex-row lg:items-start lg:justify-between gap-3"
              >
                <div className="space-y-1 max-w-2xl">
                  <div className="flex flex-wrap items-center gap-2 text-[0.75rem]">
                    <span className="font-bold text-[var(--sw-em-d)]">
                      {tr(param.categoryLabelVi, param.categoryLabelEn)}
                    </span>
                    <span className="text-[var(--ink3)]">·</span>
                    <code className="font-mono text-[0.75rem] font-semibold text-[var(--ink2)]">
                      {param.keyName}
                    </code>
                  </div>

                  <div className="font-display text-[0.9375rem] font-extrabold text-[var(--ink)]">
                    {tr(param.labelVi, param.labelEn)}
                  </div>

                  <p className="text-[0.8125rem] text-[var(--ink2)] leading-relaxed">
                    {tr(param.descriptionVi, param.descriptionEn)}
                  </p>
                </div>

                <div className="lg:text-right space-y-1 shrink-0">
                  <div
                    className={`inline-flex items-center gap-1.5 font-mono text-[0.8125rem] font-bold ${
                      param.isRedactedSecret
                        ? 'text-[var(--amber)]'
                        : 'text-[var(--ink)]'
                    }`}
                  >
                    {param.isRedactedSecret && (
                      <Lock aria-hidden="true" className="h-3.5 w-3.5 shrink-0" />
                    )}
                    <span>{param.valueDisplay}</span>
                  </div>
                  <div className="text-[0.75rem] text-[var(--ink3)] tabular-nums">
                    {tr('Kiểm tra gần nhất:', 'Last verified:')}{' '}
                    {param.lastVerifiedAt}
                  </div>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* =====================================================================
          DELIBERATE CONFIRMATION MODAL (WALLET FREEZE/UNFREEZE, RECONCILE, CONFIG)
          ===================================================================== */}
      {pendingAction && (
        <div
          role="dialog"
          aria-modal="true"
          aria-labelledby="sw-admin-ledger-confirm-title"
          data-testid="sw-admin-ledger-confirm-modal"
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4 backdrop-blur-xs"
        >
          <form
            onSubmit={handleConfirmAction}
            className="sw-card w-full max-w-lg max-h-[calc(100dvh-2rem)] flex flex-col overflow-hidden p-0 shadow-xl bg-[var(--sw-sheet-bg)]"
          >
            <div className="shrink-0 flex items-start justify-between gap-3 p-5 sm:px-6 sm:pt-5 sm:pb-3.5 border-b border-[var(--line)]">
              <div className="flex items-center gap-2.5 min-w-0">
                <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-2xl bg-[var(--amber-s)] text-[var(--amber)]">
                  <ShieldAlert aria-hidden="true" className="h-5 w-5" />
                </div>
                <div className="min-w-0">
                  <h3
                    id="sw-admin-ledger-confirm-title"
                    className="font-display text-[1.0625rem] font-extrabold text-[var(--ink)]"
                  >
                    {pendingAction.kind === 'toggle_wallet_freeze'
                      ? pendingAction.targetState === 'frozen'
                        ? tr(
                            `Xác nhận Đóng băng Ví mẫu ${pendingAction.walletCode}`,
                            `Confirm Freezing Synthetic Wallet ${pendingAction.walletCode}`
                          )
                        : tr(
                            `Xác nhận Mở khóa Ví mẫu ${pendingAction.walletCode}`,
                            `Confirm Unfreezing Synthetic Wallet ${pendingAction.walletCode}`
                          )
                      : pendingAction.kind === 'run_reconciliation_batch'
                      ? tr(
                          'Xác nhận Chạy Đối soát Cân bằng Sổ cái',
                          'Confirm Running Ledger Balance Reconciliation'
                        )
                      : tr(
                          'Xác nhận Kiểm tra Cấu hình Hệ thống Ca trực',
                          'Confirm Shift System Configuration Verification'
                        )}
                  </h3>
                  <div className="text-[0.75rem] text-[var(--ink2)]">
                    {tr(
                      'Thao tác yêu cầu lý do nghiệp vụ và ghi vào Nhật ký Kiểm toán DEMO',
                      'Requires operational reason and records a DEMO Audit Log entry'
                    )}
                  </div>
                </div>
              </div>

              <button
                type="button"
                data-testid="sw-admin-ledger-confirm-cancel-icon"
                onClick={closeActionModal}
                aria-label={tr('Đóng hộp thoại xác nhận', 'Close confirmation modal')}
                className="sw-focus-ring sw-ib !h-9 !w-9 !min-h-[36px] !min-w-[36px] shrink-0"
              >
                <X aria-hidden="true" className="h-4 w-4" />
              </button>
            </div>

            <div className="flex-1 min-h-0 overflow-y-auto overscroll-contain p-5 sm:px-6 py-4 space-y-4 sw-sheet-body">
              {/* Target summary */}
              <div className="rounded-xl border border-[var(--line)] bg-[var(--sw-surface-muted)]/60 p-3.5 text-[0.8125rem] space-y-1">
                {pendingAction.kind === 'toggle_wallet_freeze' && (
                  <>
                    <div>
                      <span className="font-bold text-[var(--ink2)]">
                        {tr('Ví mục tiêu:', 'Target wallet:')}
                      </span>{' '}
                      <span className="font-extrabold text-[var(--ink)] tabular-nums">
                        {pendingAction.walletCode}
                      </span>{' '}
                      — {tr(pendingAction.walletNameVi, pendingAction.walletNameEn)}
                    </div>
                    <div>
                      <span className="font-bold text-[var(--ink2)]">
                        {tr('Chuyển trạng thái:', 'State change:')}
                      </span>{' '}
                      <span className="font-bold text-[var(--ink)]">
                        {pendingAction.currentState === 'active'
                          ? tr(
                              'Hoạt động → Đang đóng băng (DEMO)',
                              'Active → Frozen (DEMO)'
                            )
                          : tr(
                              'Đang đóng băng → Hoạt động (DEMO)',
                              'Frozen → Active (DEMO)'
                            )}
                      </span>
                    </div>
                  </>
                )}
                {pendingAction.kind === 'run_reconciliation_batch' && (
                  <div>
                    {tr(
                      `Kiểm tra đồng bộ tổng số dư khả dụng + giữ chỗ với tổng bút toán nhật ký trên ${pendingAction.walletCount} ví mẫu.`,
                      `Verify liquid + held balance totals against computed journal entries across ${pendingAction.walletCount} synthetic wallets.`
                    )}
                  </div>
                )}
                {pendingAction.kind === 'verify_system_config' && (
                  <div>
                    {tr(
                      `Xác nhận kiểm tra ${pendingAction.paramCount} tham số bất biến sổ cái và chính sách ẩn toàn bộ khóa bí mật ([REDACTED_SECRET]).`,
                      `Verify ${pendingAction.paramCount} ledger invariant parameters and confirm strict secret redaction ([REDACTED_SECRET]).`
                    )}
                  </div>
                )}
                <div className="text-[0.75rem] text-[var(--sw-em-d)] font-semibold pt-1">
                  {tr(
                    'Cam kết DEMO: Không phát sinh lệnh quyết toán ngân hàng thực và không lộ bất kỳ khóa bí mật nào.',
                    'DEMO Guarantee: Does not trigger real bank settlement or expose any credentials/secrets.'
                  )}
                </div>
              </div>

              {/* Preset reason buttons */}
              {presetReasons.length > 0 && (
                <div className="space-y-1.5">
                  <div className="text-[0.75rem] font-bold text-[var(--ink2)]">
                    {tr('Gợi ý lý do nhanh:', 'Quick reason templates:')}
                  </div>
                  <div className="flex flex-wrap gap-1.5">
                    {presetReasons.map((preset, idx) => (
                      <button
                        key={idx}
                        type="button"
                        data-testid={`sw-admin-ledger-preset-reason-${idx}`}
                        onClick={() => {
                          setReasonInput(preset);
                          setConfirmError(null);
                        }}
                        className="sw-focus-ring rounded-lg border border-[var(--line)] bg-[var(--sw-surface-muted)] px-2.5 py-1.5 text-left text-[0.75rem] font-semibold text-[var(--ink2)] hover:text-[var(--ink)] cursor-pointer"
                      >
                        {preset}
                      </button>
                    ))}
                  </div>
                </div>
              )}

              {/* Mandatory Reason Textarea */}
              <div className="space-y-1.5">
                <label
                  htmlFor="sw-admin-ledger-reason-input"
                  className="block text-[0.8125rem] font-bold text-[var(--ink)]"
                >
                  {tr(
                    'Lý do nghiệp vụ & căn cứ đối soát (Bắt buộc) *',
                    'Operational reason & audit justification (Required) *'
                  )}
                </label>
                <textarea
                  id="sw-admin-ledger-reason-input"
                  data-testid="sw-admin-ledger-reason-input"
                  rows={3}
                  value={reasonInput}
                  onChange={(e) => {
                    setReasonInput(e.target.value);
                    if (confirmError) setConfirmError(null);
                  }}
                  placeholder={tr(
                    'Nhập mã hồ sơ hỗ trợ hoặc lý do đối soát cụ thể (tối thiểu 6 ký tự)...',
                    'Enter support case reference or reconciliation reason (min 6 chars)...'
                  )}
                  className="sw-in w-full text-[0.84375rem]"
                />
              </div>

              {/* Deliberate Confirmation Checkbox */}
              <label className="flex items-start gap-2.5 text-[0.8125rem] text-[var(--ink)] cursor-pointer select-none">
                <input
                  type="checkbox"
                  data-testid="sw-admin-ledger-confirm-checkbox"
                  checked={isDeliberatelyConfirmed}
                  onChange={(e) => {
                    setIsDeliberatelyConfirmed(e.target.checked);
                    if (confirmError) setConfirmError(null);
                  }}
                  className="mt-1 h-4 w-4 rounded border-[var(--line)] accent-[#059669] shrink-0"
                />
                <span>
                  {tr(
                    'Tôi xác nhận thực hiện thao tác DEMO này và ghi sự kiện vào Nhật ký Kiểm toán (không ảnh hưởng quyết toán ngân hàng thực).',
                    'I deliberately confirm this DEMO mutation and understand it will be logged in the Audit Trail (no real banking settlement).'
                  )}
                </span>
              </label>

              {confirmError && (
                <div
                  role="alert"
                  data-testid="sw-admin-ledger-confirm-error"
                  className="rounded-xl bg-[var(--rose-s)] px-3 py-2 text-[0.8125rem] font-bold text-[var(--rose)]"
                >
                  {confirmError}
                </div>
              )}
            </div>

            <div className="shrink-0 flex items-center justify-end gap-2 px-5 sm:px-6 py-3.5 border-t border-[var(--line)] bg-[var(--sw-sheet-bg)]">
              <button
                type="button"
                data-testid="sw-admin-ledger-confirm-cancel-btn"
                onClick={closeActionModal}
                className="sw-focus-ring sw-btn ghost sm"
              >
                <span>{tr('Hủy bỏ', 'Cancel')}</span>
              </button>
              <button
                type="submit"
                data-testid="sw-admin-ledger-confirm-submit-btn"
                className="sw-focus-ring sw-btn sw-btn-pri sm"
              >
                <Check aria-hidden="true" className="h-4 w-4" />
                <span>
                  {tr('Xác nhận & Ghi Kiểm toán', 'Confirm & Record Audit')}
                </span>
              </button>
            </div>
          </form>
        </div>
      )}
    </div>
  );
};
