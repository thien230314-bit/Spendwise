import {
  WalletAccount,
  CategoryItem,
  Transaction,
  TransactionType,
  TransactionCorrection,
  TransactionCorrectionSnapshot,
  TransactionGrantRecord,
  AttachmentMetadata,
  DashboardSummary,
  SpendingCategory,
  AttentionItem,
  SystemRole,
} from '../types';
import {
  WalletDto,
  CategoryDto,
  EvidenceDto,
  TransactionDto,
  TransactionCorrectionDto,
  CorrectionGrantDto,
  DashboardDto,
  CategoryDistributionDto,
  AttentionItemDto,
  WalletType,
} from './financialApiService';
import { formatDate } from '../utils/date';

// ---------------------------------------------------------------------------
// 1. Wallets
// ---------------------------------------------------------------------------

export function getWalletVisuals(type: WalletType): { colorClass: string; bgGradient: string } {
  switch (type) {
    case 'savings':
      return {
        colorClass: 'text-blue-400',
        bgGradient: 'from-blue-900 via-slate-900 to-slate-950',
      };
    case 'emergency':
      return {
        colorClass: 'text-amber-400',
        bgGradient: 'from-amber-900 via-slate-900 to-slate-950',
      };
    case 'spending':
    default:
      return {
        colorClass: 'text-emerald-400',
        bgGradient: 'from-slate-800 via-slate-900 to-slate-950',
      };
  }
}

export function mapWallet(dto: WalletDto): WalletAccount {
  const visuals = getWalletVisuals(dto.type);
  return {
    id: dto.id,
    name: dto.name,
    type: dto.type,
    balance: dto.availableBalance,
    currency: dto.currency || 'VND',
    accountNumber: '', // Backend WalletDto has no account number
    isDefault: dto.isDefault,
    colorClass: visuals.colorClass,
    bgGradient: visuals.bgGradient,
  };
}

// ---------------------------------------------------------------------------
// 2. Categories
// ---------------------------------------------------------------------------

export function mapCategory(dto: CategoryDto): CategoryItem {
  return {
    id: dto.id,
    key: dto.key,
    name: dto.name,
    kind: dto.kind === 'Income' ? 'income' : 'expense',
    icon: dto.icon || (dto.kind === 'Income' ? 'Wallet' : 'ShoppingBag'),
    color: dto.color || (dto.kind === 'Income' ? '#10B981' : '#EF4444'),
    isSystem: dto.isSystem,
    isArchived: !dto.isActive,
  };
}

// ---------------------------------------------------------------------------
// 3. Evidence
// ---------------------------------------------------------------------------

export function mapEvidence(dto: EvidenceDto): AttachmentMetadata {
  return {
    id: dto.id,
    name: dto.originalFileName,
    size: dto.length,
    type: dto.contentType,
    uploadedAt: dto.createdAt,
    dataUrl: undefined,
  };
}

// ---------------------------------------------------------------------------
// 4. Transactions
// ---------------------------------------------------------------------------

export function mapTransactionType(backendType: string | number): TransactionType {
  const val = String(backendType);
  switch (val) {
    case '0':
    case 'Income':
      return 'income';
    case '1':
    case 'Expense':
      return 'expense';
    case '2':
    case 'TransferOut':
      return 'transfer_out';
    case '3':
    case 'TransferIn':
      return 'transfer_in';
    case '4':
    case 'InternalTransfer':
      return 'internal_transfer';
    case '5':
    case 'GoalContribution':
      return 'goal_contribution';
    case '6':
    case 'GoalWithdrawal':
      return 'goal_withdrawal';
    case '7':
    case 'GroupFundContribution':
      return 'group_fund_contribution';
    case '8':
    case 'DebtSettlement':
      return 'debt_settlement';
    case '9':
    case 'Deposit':
      return 'deposit';
    case '10':
    case 'Withdrawal':
      return 'withdrawal';
    case '11':
    case 'Refund':
      return 'refund';
    case '12':
    case 'Reversal':
      return 'reversal';
    case '13':
    case 'BankTopUp':
      return 'bank_top_up';
    default:
      return 'unknown';
  }
}

export function mapTransactionStatus(backendStatus: string): 'completed' | 'pending' | 'failed' | 'cancelled' | 'unknown' {
  const s = (backendStatus || '').toLowerCase();
  if (s === 'completed') return 'completed';
  if (s === 'pending') return 'pending';
  if (s === 'failed') return 'failed';
  if (s === 'cancelled' || s === 'canceled') return 'cancelled';
  return 'unknown';
}

export function mapTransactionCorrection(dto: TransactionCorrectionDto): TransactionCorrection {
  const beforeSnapshot: TransactionCorrectionSnapshot = {
    amount: typeof dto.before?.amount === 'number' ? dto.before.amount : 0,
    category: String(dto.before?.category || dto.before?.categoryId || ''),
    description: String(dto.before?.description || ''),
    walletId: dto.before?.walletId ? String(dto.before.walletId) : undefined,
    walletName: dto.before?.walletName ? String(dto.before.walletName) : undefined,
    occurredAt: String(dto.before?.occurredAt || dto.createdAt),
    counterparty: dto.before?.counterparty ? String(dto.before.counterparty) : undefined,
  };

  const afterSnapshot: TransactionCorrectionSnapshot = {
    amount: typeof dto.after?.amount === 'number' ? dto.after.amount : 0,
    category: String(dto.after?.category || dto.after?.categoryId || ''),
    description: String(dto.after?.description || ''),
    walletId: dto.after?.walletId ? String(dto.after.walletId) : undefined,
    walletName: dto.after?.walletName ? String(dto.after.walletName) : undefined,
    occurredAt: String(dto.after?.occurredAt || dto.createdAt),
    counterparty: dto.after?.counterparty ? String(dto.after.counterparty) : undefined,
  };

  return {
    id: dto.id,
    correctionNumber: dto.correctionNumber,
    actorId: dto.actorUserId,
    actorRole: (dto.actorRole as SystemRole) || 'User',
    actorName: dto.actorName,
    timestamp: dto.createdAt,
    reason: dto.reason,
    before: beforeSnapshot,
    after: afterSnapshot,
    addedEvidence: dto.evidence ? dto.evidence.map(mapEvidence) : undefined,
  };
}

export function mapCorrectionGrant(dto: CorrectionGrantDto): TransactionGrantRecord {
  return {
    id: dto.id,
    adminId: dto.grantedByUserId || '',
    adminName: dto.grantedByName || 'SystemAdmin',
    supportCaseId: '',
    reason: dto.reason,
    grantedAt: dto.grantedAt,
  };
}

export function mapTransaction(
  dto: TransactionDto,
  categories: CategoryItem[] = [],
  currentUserId?: string
): Transaction {
  // Resolve category key through loaded categories by ID where possible
  let resolvedCategory = 'other';
  if (dto.categoryId) {
    const matched = categories.find((c) => c.id === dto.categoryId);
    if (matched) {
      resolvedCategory = matched.key;
    } else if (dto.categoryName) {
      resolvedCategory = dto.categoryName;
    }
  } else if (dto.categoryName) {
    resolvedCategory = dto.categoryName;
  }

  const rawDate = dto.occurredAt;
  const displayDate = formatDate(rawDate);

  const evidence = dto.evidence ? dto.evidence.map(mapEvidence) : undefined;
  const corrections = dto.corrections ? dto.corrections.map(mapTransactionCorrection) : undefined;
  const grantHistory = dto.correctionGrants ? dto.correctionGrants.map(mapCorrectionGrant) : undefined;

  const resolvedSignedAmount = typeof dto.signedAmount === 'number' ? dto.signedAmount : dto.amount;

  return {
    id: dto.id,
    userId: currentUserId,
    title: dto.description || dto.categoryName || 'Transaction',
    description: dto.description,
    category: resolvedCategory,
    amount: resolvedSignedAmount,
    signedAmount: resolvedSignedAmount,
    type: mapTransactionType(dto.type),
    date: displayDate,
    rawDate: rawDate,
    occurredAt: dto.occurredAt,
    createdAt: dto.occurredAt,
    isManualEntry: dto.type === 'Income' || dto.type === 'Expense',
    evidence,
    counterparty: dto.counterpartyName,
    status: mapTransactionStatus(dto.status),
    walletId: dto.walletId,
    walletName: dto.walletName,
    canEdit: dto.canCorrect,
    canDelete: false, // finalized transactions cannot be hard-deleted
    referenceCode: dto.ledgerTransactionId || dto.id,
    correctionAllowance: dto.correctionAllowance,
    correctionCount: dto.correctionCount,
    remainingCorrections: Math.max(0, (dto.correctionAllowance || 0) - (dto.correctionCount || 0)),
    corrections,
    grantHistory,
  };
}

// ---------------------------------------------------------------------------
// 5. Dashboard
// ---------------------------------------------------------------------------

export function mapDashboardSummary(dto: DashboardDto): DashboardSummary {
  return {
    totalBalance: dto.totalBalance,
    totalWalletsBalance: dto.totalWalletsBalance,
    totalTrackedAssets: dto.totalTrackedAssets,
    totalGoalsReserved: dto.totalGoalsReserved,
    incomeThisMonth: dto.incomeThisMonth,
    expenseThisMonth: dto.expenseThisMonth,
    budgetRemaining: dto.budgetRemaining,
    cashFlowDifference: dto.cashFlowDifference,
    groupFundBalance: dto.groupFundBalance,
    userPendingFundObligations: dto.userPendingFundObligations,
    othersOweUser: dto.othersOweUser,
    userOwesOthers: dto.userOwesOthers,
  };
}

export function mapSpendingCategories(
  items: CategoryDistributionDto[] | undefined,
  categories: CategoryItem[] = []
): SpendingCategory[] {
  if (!items || !Array.isArray(items)) return [];
  const total = items.reduce((sum, item) => sum + (item.amount || 0), 0);
  return items.map((item, index) => {
    const matched = item.categoryId ? categories.find((c) => c.id === item.categoryId) : undefined;
    const percentage =
      typeof item.percentage === 'number'
        ? item.percentage
        : total > 0
        ? Math.round((item.amount / total) * 100)
        : 0;
    return {
      id: item.categoryId || `cat-dist-${index}`,
      nameKey: matched ? `category.${matched.key}` : item.categoryName,
      name: item.categoryName,
      amount: item.amount,
      percentage,
      color: matched?.color || '#10B981',
      badgeBg: 'bg-emerald-500/10',
      icon: matched?.icon || 'ShoppingBag',
    };
  });
}

export function mapAttentionItems(items: AttentionItemDto[] | undefined): AttentionItem[] {
  if (!items || !Array.isArray(items)) return [];
  return items.map((item, index) => {
    let severity: 'info' | 'warning' | 'reminder' = 'info';
    if (item.severity === 'warning') severity = 'warning';
    else if (item.severity === 'reminder') severity = 'reminder';

    let iconType: 'budget' | 'payment_request' | 'subscription' = 'budget';
    if (item.type === 'payment_request') iconType = 'payment_request';
    else if (item.type === 'subscription') iconType = 'subscription';

    return {
      id: item.id || item.relatedId || `att-${index}`,
      rawMessage: item.message || item.title,
      actionLabel: item.title,
      actionRoute: item.actionUrl || '/dashboard',
      severity,
      iconType,
    };
  });
}
