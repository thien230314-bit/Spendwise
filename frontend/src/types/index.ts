export type SupportedLocale = 'vi-VN' | 'en-US';
export type ThemeMode = 'light' | 'dark' | 'system';
export type SystemRole = 'User' | 'SystemAdmin';
export type UserAccountStatus = 'active' | 'restricted' | 'locked' | 'suspended';

export interface User {
  id: string;
  name: string;
  email: string;
  role: SystemRole;
  language: SupportedLocale;
  currency: 'VND' | 'USD';
  avatarInitials: string;
  phone?: string;
  status?: UserAccountStatus;
  isLocked?: boolean;
  statusReason?: string;
  statusUpdatedAt?: string;
  mustChangePassword?: boolean;
  createdAt?: string;
  /** Backend-authoritative. Default: false. */
  engagementEmailOptIn?: boolean;
}

export interface Money {
  amount: number;
  currency: 'VND' | 'USD';
}

export interface WalletAccount {
  id: string;
  name: string;
  type: 'spending' | 'savings' | 'emergency';
  balance: number;
  currency: string;
  accountNumber: string;
  isDefault?: boolean;
  colorClass: string;
  bgGradient: string;
}

export type TransactionDirection = 'credit' | 'debit';

export type TransactionSemanticType =
  | 'income'
  | 'expense'
  | 'transfer'
  | 'group_settlement'
  | 'refund'
  | 'goal_allocation'
  | 'unknown';

export type ClassificationSource =
  | 'manual'
  | 'rule'
  | 'pattern'
  | 'system'
  | 'unknown';

export type TransactionType =
  | 'income'
  | 'expense'
  | 'transfer_out'
  | 'transfer_in'
  | 'internal_transfer'
  | 'goal_contribution'
  | 'goal_withdrawal'
  | 'group_fund_contribution'
  | 'debt_settlement'
  | 'deposit'
  | 'withdrawal'
  | 'refund'
  | 'reversal'
  | 'bank_top_up'
  | 'unknown';

export interface TransactionCorrectionSnapshot {
  amount: number;
  category: string;
  description: string;
  walletId?: string;
  walletName?: string;
  occurredAt: string;
  counterparty?: string;
}

export interface TransactionCorrection {
  id: string;
  correctionNumber: number;
  actorId: string;
  actorRole: SystemRole;
  actorName: string;
  timestamp: string;
  reason: string;
  before: TransactionCorrectionSnapshot;
  after: TransactionCorrectionSnapshot;
  addedEvidence?: AttachmentMetadata[];
}

export interface TransactionGrantRecord {
  id: string;
  adminId: string;
  adminName: string;
  supportCaseId: string;
  reason: string;
  grantedAt: string;
}

export interface Transaction {
  id: string;
  userId?: string;
  title: string;
  titleEn?: string;
  description?: string;
  descriptionEn?: string;
  category: string;
  amount: number;
  signedAmount?: number;
  type: TransactionType;
  date: string;
  rawDate: string;
  occurredAt?: string;
  createdAt?: string;
  isManualEntry?: boolean;
  evidence?: AttachmentMetadata[];
  counterparty?: string;
  counterpartyEn?: string;
  status: 'completed' | 'pending' | 'failed' | 'cancelled' | 'unknown';
  walletId?: string;
  walletName?: string;
  canEdit?: boolean;
  canDelete?: boolean;
  referenceCode?: string;
  correctionAllowance?: number;
  correctionCount?: number;
  remainingCorrections?: number;
  corrections?: TransactionCorrection[];
  grantHistory?: TransactionGrantRecord[];
  originalValues?: TransactionCorrectionSnapshot;
  // Phase 3A Classification Metadata
  direction?: TransactionDirection;
  semanticType?: TransactionSemanticType;
  classificationSource?: ClassificationSource;
  classificationConfidence?: number;
  needsCategoryReview?: boolean;
  recipientUserId?: string;
}

export interface BudgetCategoryItem {
  id: string;
  categoryKey: string;
  categoryId?: string;
  name: string;
  limit: number;
  month: string;
  iconName: string;
  color: string;
  iconBg: string;
  iconColor: string;
  rollover?: boolean;
  spent?: number;
  remaining?: number;
  percentage?: number;
  alertLevel?: string;
  alertsEnabled?: boolean;
}

export interface GoalContributionItem {
  id: string;
  amount: number;
  date: string;
  sourceWalletName: string;
  type: 'deposit' | 'withdraw';
}

export type GoalStatus = 'active' | 'completed' | 'paused' | 'cancelled';

export interface GoalItem {
  id: string;
  title: string;
  category: string;
  categoryId?: string;
  categoryName?: string;
  targetAmount: number;
  currentAmount: number;
  initialAmount?: number;
  deadline: string;
  remainingDays?: number;
  monthlyAutoSave?: number;
  iconName?: string;
  iconBg?: string;
  iconColor?: string;
  colorHex?: string;
  status?: GoalStatus;
  history: GoalContributionItem[];
}

export interface AttentionItem {
  id: string;
  messageKey?: string;
  messageParams?: Record<string, string | number>;
  rawMessage: string;
  rawMessageEn?: string;
  actionLabel: string;
  actionLabelEn?: string;
  actionLabelKey?: string;
  actionRoute: string;
  secondaryActionLabel?: string;
  secondaryActionLabelEn?: string;
  secondaryActionLabelKey?: string;
  secondaryActionRoute?: string;
  severity: 'info' | 'warning' | 'reminder';
  iconType: 'budget' | 'payment_request' | 'subscription';
}

export interface SpendingCategory {
  id: string;
  nameKey: string;
  name: string;
  amount: number;
  percentage: number;
  color: string;
  badgeBg: string;
  icon: string;
}

export interface DashboardSummary {
  totalBalance: number;
  totalTrackedAssets?: number;
  totalWalletsBalance?: number;
  totalGoalsReserved?: number;
  incomeThisMonth: number;
  expenseThisMonth: number;
  budgetRemaining: number;
  othersOweUser: number;
  userOwesOthers: number;
  balanceChangePercentage?: number;
  cashFlowDifference: number;
  groupFundBalance?: number;
  userPendingFundObligations?: number;
}

export interface NotificationItem {
  id: string;
  userId?: string;
  accountId?: string;
  accountName?: string;
  accountType?: 'spending' | 'savings' | 'emergency' | 'security' | 'system' | 'bank' | 'friend';
  accountNumber?: string;
  title: string;
  titleKey?: string;
  message: string;
  messageKey?: string;
  detail?: string;
  detailKey?: string;
  i18nParams?: Record<string, string | number>;
  time: string;
  timestamp: number;
  isRead: boolean;
  type: 'payment' | 'budget' | 'subscription' | 'recurring' | 'group' | 'system' | 'security' | 'transfer' | 'friend_request' | 'friend_accepted' | 'friend_rejected';
  category?: 'transaction' | 'budget' | 'recurring' | 'group' | 'security' | 'system' | 'friend';
  amountChange?: number;
  balanceAfter?: number;
  actionRoute?: string;
  actionLabelKey?: string;
  relatedTransactionId?: string;
  friendRequestId?: string;
  friendRequesterId?: string;
  statusVariant?: 'default' | 'success' | 'danger';
}

export interface AccountNotificationPreference {
  accountId: string;
  accountName: string;
  notifyBalanceChanges: boolean;
  notifyBudgetAlerts: boolean;
  notifyLargeTransactions: boolean;
  largeTransactionThreshold: number;
  notifyMinBalanceAlert: boolean;
  minBalanceThreshold: number;
}

export interface NavItem {
  id: string;
  labelKey: string;
  defaultLabel: string;
  path: string;
  iconName: string;
  badge?: number | string;
}

export type CategoryKind = 'income' | 'expense';

export interface CategoryItem {
  id: string;
  key: string;
  name: string;
  nameEn?: string;
  kind: CategoryKind;
  icon: string;
  color?: string;
  isSystem?: boolean;
  isArchived?: boolean;
}

export type RecurringFrequency = 'daily' | 'weekly' | 'monthly' | 'yearly';

export interface RecurringOccurrence {
  id: string;
  scheduledFor: string;
  status: 'Due' | 'Overdue' | 'Recorded' | string;
  recordedTransactionId?: string;
  recordedAt?: string;
}

export interface RecurringTransaction {
  id: string;
  title: string;
  type: 'income' | 'expense';
  walletId: string;
  walletName?: string;
  categoryId: string;
  categoryName?: string;
  amount: number;
  frequency: RecurringFrequency;
  nextRunDate: string;
  scheduleAnchorDay?: number;
  scheduleAnchorMonth?: number;
  description?: string;
  status: 'active' | 'paused';
  isSubscription?: boolean;
  lastRunAt?: string;
  occurrences?: RecurringOccurrence[];
  createdAt?: string;
}

export interface TransactionRule {
  id: string;
  descriptionPattern: string;
  targetCategoryId: string;
  targetCategoryName?: string;
  priority: number;
  isActive: boolean;
  createdAt?: string;
}

export * from './friends';

// External Bank Integration (SePay Bank Hub)
export type BankProvider = 'SePayBankHub';

export type BankConnectionStatus = 'Pending' | 'Active' | 'Inactive' | 'Unlinked' | 'Error';

export type BankTransactionDirection = 'Credit' | 'Debit';

export type BankTransactionSource = 'Webhook' | 'ManualSync';

export type AccountEnvironment = 'BankHub' | 'TestMode';

export interface BankConnection {
  id: string;
  userId: string;
  provider: BankProvider;
  bankBrandName: string;
  accountType?: string;
  accountHolderName?: string;
  maskedAccountNumber: string;
  status: BankConnectionStatus;
  linkedAt?: string;
  unlinkedAt?: string;
  lastSyncedAt?: string;
  createdAt: string;
  updatedAt?: string;
  accountEnvironment?: AccountEnvironment;
}

export interface BankTransaction {
  id: string;
  userId: string;
  bankConnectionId: string;
  provider: BankProvider;
  bankBrandName?: string;
  maskedAccountNumber?: string;
  externalTransactionId: string;
  direction: BankTransactionDirection;
  amount: number;
  currency: string;
  transactionDate: string;
  content: string;
  referenceCode?: string;
  paymentCode?: string;
  gateway?: string;
  source: BankTransactionSource;
  importedAt: string;
  categoryId?: string;
  categoryName?: string;
}



export type SupportCaseCategory =
  | 'system_error'
  | 'account'
  | 'banking_sepay'
  | 'transaction_edit'
  | 'other'
  | 'transaction'
  | 'feature_request'
  | 'general';

export type SupportCaseStatus = 'pending' | 'open' | 'in_progress' | 'need_more_info' | 'resolved' | 'closed' | 'rejected';

export interface AttachmentMetadata {
  id: string;
  name: string;
  size: number;
  type: string;
  dataUrl?: string;
  uploadedAt: string;
}

export interface SupportHistoryItem {
  id: string;
  actorId: string;
  actorName: string;
  actorRole: 'User' | 'SystemAdmin';
  action: 'create' | 'reply' | 'status_change' | 'request_info' | 'provide_info';
  message: string;
  attachments?: AttachmentMetadata[];
  timestamp: string;
  oldStatus?: string;
  newStatus?: string;
}

export interface SupportCase {
  id: string;
  ticketNumber?: string;
  userId: string;
  userName?: string;
  userEmail?: string;
  userPhone?: string;
  subject: string;
  title?: string;
  category: SupportCaseCategory;
  message: string;
  description?: string;
  linkedTransactionId?: string;
  linkedTransactionRef?: string;
  attachments?: AttachmentMetadata[];
  status: SupportCaseStatus;
  priority?: 'urgent' | 'high' | 'normal' | 'low';
  assignedAgent?: string;
  createdAt: string;
  updatedAt: string;
  latestReply?: string;
  history?: SupportHistoryItem[];
  correctionGranted?: boolean;
  correctionGrantedAt?: string;
  correctionGrantedBy?: string;
  correctionGrantReason?: string;
}

export type DisputeReason =
  | 'unrecognized_charge'
  | 'duplicate_transaction'
  | 'duplicate_deduction'
  | 'incorrect_amount'
  | 'goods_not_received'
  | 'merchant_dispute'
  | 'other';

export type DisputeStatus = 'pending' | 'submitted' | 'under_review' | 'in_review' | 'need_more_info' | 'resolved' | 'rejected';

export interface DisputeRecord {
  id: string;
  disputeCode?: string;
  userId: string;
  userName?: string;
  userEmail?: string;
  userPhone?: string;
  transactionId: string;
  transactionRef: string;
  transactionTitle?: string;
  transactionAmount: number;
  transactionCounterparty?: string;
  transactionDate: string;
  reason: DisputeReason;
  description: string;
  attachments?: AttachmentMetadata[];
  status: DisputeStatus;
  assignedAdmin?: string;
  adminNotes?: string;
  createdAt: string;
  updatedAt: string;
  resolutionNote?: string;
  history?: SupportHistoryItem[];
}
