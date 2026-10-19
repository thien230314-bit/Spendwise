import React, { createContext, useContext, useState, useEffect, useCallback, useMemo, useRef } from 'react';
import { useAuth } from './AuthProvider';
import {
  Transaction,
  DashboardSummary,
  SpendingCategory,
  AttentionItem,
  BudgetCategoryItem,
  GoalItem,
  GoalContributionItem,
  CategoryItem,
  RecurringTransaction,
  TransactionRule,
  BankConnection,
  BankTransaction,
  BankConnectionStatus,
  TransactionCorrection,
  TransactionCorrectionSnapshot,
  TransactionGrantRecord,
  AttachmentMetadata,
  TransactionDirection,
  TransactionSemanticType,
  ClassificationSource,
  RecurringFrequency,
} from '../types';
import { ApiError } from '../lib';
import { financialApiService, TransactionDto } from '../services/financialApiService';
import {
  planningApiService,
  BudgetDto,
  GoalDto,
  RecurringDto,
  TransactionRuleDto,
} from '../services/planningApiService';
import {
  mapWallet,
  mapCategory,
  mapTransaction,
  mapDashboardSummary,
  mapSpendingCategories,
  mapAttentionItems,
} from '../services/financialMapper';
import {
  mapBudget,
  mapGoal,
  mapRecurring,
  mapTransactionRule,
} from '../services/planningMapper';
import {
  isRecurringDue,
  getTodayDateString,
} from '../services/recurringService';
import { classifyCandidateTransaction } from '../services/transactionClassificationService';
import {
  AdminBankIntegrationEvent,
  BankReconciliationSummary,
  SePayIntegrationStatus,
} from '../types/admin';
import { INITIAL_CATEGORIES } from '../mocks/financialAutomationData';

const DEFAULT_SEPAY_INTEGRATION_STATUS: SePayIntegrationStatus = {
  enabled: false,
  environment: 'Sandbox',
  configured: false,
  companyConfigured: false,
  lifecycleWebhookConfigured: false,
  ipnConfigured: false,
  lastSuccessfulProviderCallAt: null,
  lastWebhookReceivedAt: null,
};

const DEFAULT_BANK_RECONCILIATION_SUMMARY: BankReconciliationSummary = {
  activeConnections: 0,
  inactiveConnections: 0,
  unlinkedConnections: 0,
  processedEvents: 0,
  duplicateDeliveries: 0,
  unmatchedEvents: 0,
  failedEvents: 0,
  importedTransactions: 0,
};

import {
  bankApiService,
  TestModeBankLinkChallengeDto,
  TestModeLinkedAccountDto,
  WalletTopUpDto,
  AdminBankConnectionDto,
  AdminBankTransactionDto,
  AdminBankIntegrationEventDto,
  BankLinkSessionDto,
  CreateBankLinkRequest,
} from '../services/bankApiService';
import {
  mapBankConnection,
  mapAdminBankConnection,
  mapBankTransaction,
  mapAdminBankTransaction,
  mapAdminBankIntegrationEvent,
  mapBankReconciliationSummary,
  mapSePayIntegrationStatus,
} from '../services/bankMapper';
import { getTransactionEffectiveDate } from '../utils/analyticsHelpers';

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

export interface AddTransactionParams {
  walletId: string;
  amount: number;
  category: string;
  description: string;
  date?: string;
  occurredAt?: string;
  counterparty?: string;
  evidence?: AttachmentMetadata[];
  securityPin?: string;
}

export interface CorrectTransactionParams {
  id: string;
  amount?: number;
  walletId?: string;
  category?: string;
  description?: string;
  occurredAt?: string;
  counterparty?: string;
  reason: string;
  additionalEvidence?: AttachmentMetadata[];
  securityPin?: string;
}

export interface CreateBudgetParams {
  name: string;
  categoryKey: string;
  categoryId?: string;
  limit: number;
  month?: string;
  rollover?: boolean;
  alertsEnabled?: boolean;
  securityPin?: string;
}

export interface CreateGoalParams {
  title: string;
  category?: string;
  categoryId?: string;
  targetAmount: number;
  initialAmount?: number;
  walletId?: string;
  deadline?: string;
  iconName?: string;
  colorHex?: string;
  securityPin?: string;
}

export interface FinancialContextType {
  isLoading: boolean;
  wallets: WalletAccount[];
  transactions: Transaction[];
  budgets: BudgetCategoryItem[];
  goals: GoalItem[];
  dashboardSummary: DashboardSummary;
  spendingCategories: SpendingCategory[];
  attentionItems: AttentionItem[];
  getWallet: (id: string) => WalletAccount | undefined;
  addIncome: (params: AddTransactionParams) => Promise<{ success: boolean; txId?: string; error?: string }>;
  addExpense: (params: AddTransactionParams) => Promise<{ success: boolean; txId?: string; error?: string }>;
  deleteTransaction: (id: string) => { success: boolean; error?: string };
  editTransaction: (
    id: string,
    updates: Partial<AddTransactionParams & { title?: string; reason?: string; additionalEvidence?: AttachmentMetadata[] }>
  ) => Promise<{ success: boolean; error?: string }>;
  correctTransaction: (params: CorrectTransactionParams) => Promise<{ success: boolean; error?: string }>;
  loadBudgetsForMonth?: (year: number, month: number) => Promise<void>;
  createBudget: (params: CreateBudgetParams) => Promise<{ success: boolean; budgetId?: string; error?: string }>;
  updateBudgetLimit: (budgetId: string, newLimit: number, securityPin?: string) => Promise<{ success: boolean; error?: string }>;
  editBudget: (budgetId: string, updates: Partial<CreateBudgetParams> & { securityPin?: string }) => Promise<{ success: boolean; error?: string }>;
  deleteBudget: (budgetId: string, securityPin?: string) => Promise<{ success: boolean; error?: string }>;
  createGoal: (params: CreateGoalParams) => Promise<{ success: boolean; goalId?: string; error?: string }>;
  editGoal: (goalId: string, updates: Partial<CreateGoalParams> & { securityPin?: string }) => Promise<{ success: boolean; error?: string }>;
  deleteGoal: (goalId: string, securityPin?: string) => Promise<{ success: boolean; error?: string }>;
  contributeToGoal: (params: { goalId: string; walletId: string; amount: number; securityPin?: string }) => Promise<{
    success: boolean;
    error?: string;
  }>;
  withdrawFromGoal: (params: { goalId: string; walletId: string; amount: number; securityPin?: string }) => Promise<{
    success: boolean;
    error?: string;
  }>;
  transferBetweenWallets: (params: {
    sourceWalletId: string;
    destWalletId: string;
    amount: number;
    description?: string;
    securityPin?: string;
  }) => Promise<{ success: boolean; error?: string; txId?: string }>;
  createWallet: (params: {
    name: string;
    type?: 'spending' | 'savings' | 'emergency';
    initialBalance?: number;
  }) => Promise<{ success: boolean; error?: string; walletId?: string }>;
  updateWallet: (
    walletId: string,
    updates: { name: string; type?: 'spending' | 'savings' | 'emergency' }
  ) => Promise<{ success: boolean; error?: string }>;
  setDefaultWallet: (walletId: string) => Promise<{ success: boolean; error?: string }>;
  closeWallet: (walletId: string) => Promise<{ success: boolean; error?: string }>;
  getTransactionDetail?: (transactionId: string) => Promise<Transaction>;
  downloadEvidenceBlob?: (evidenceId: string) => Promise<Blob>;
  refreshFinancialCore?: () => Promise<void>;
  // External Bank Integration (SePay Unified Banking)
  bankConnections: BankConnection[];
  bankTransactions: BankTransaction[];
  testModeLinkedAccounts: TestModeLinkedAccountDto[];
  walletTopUps: WalletTopUpDto[];
  startBankLink: () => Promise<{ success: boolean; challenge?: TestModeBankLinkChallengeDto; error?: string; code?: string }>;
  confirmBankLink: (challengeId: string) => Promise<{ success: boolean; connection?: BankConnection; error?: string; code?: string }>;
  unlinkBankConnection: (id: string, request?: CreateBankLinkRequest) => Promise<{ success: boolean; session?: BankLinkSessionDto; error?: string; code?: string }>;
  reactivateBankConnection: (id: string, request?: CreateBankLinkRequest) => Promise<{ success: boolean; session?: BankLinkSessionDto; error?: string; code?: string }>;
  syncBankConnection: (id: string) => Promise<{ success: boolean; newTransactionCount?: number; importedCount?: number; error?: string; code?: string }>;
  createWalletTopUp: (params: { bankConnectionId: string; walletId: string; amount: number }) => Promise<{
    success: boolean;
    topUp?: WalletTopUpDto;
    error?: string;
    code?: string;
    activeTopUp?: WalletTopUpDto;
  }>;
  confirmWalletTopUp: (topUpId: string) => Promise<{
    success: boolean;
    topUp?: WalletTopUpDto;
    error?: string;
    code?: string;
  }>;
  updateBankTransactionCategory: (bankTxId: string, categoryId: string) => Promise<{
    success: boolean;
    error?: string;
    code?: string;
  }>;
  refreshBankConnections: () => Promise<{
    bankConnections: BankConnection[];
    testModeLinkedAccounts: TestModeLinkedAccountDto[];
  }>;
  refreshWalletTopUps: () => Promise<WalletTopUpDto[]>;
  refreshAdminBanking: () => Promise<void>;
  // Admin Bank Integration & Governance
  adminBankConnections: BankConnection[];
  adminBankTransactions: BankTransaction[];
  adminBankIntegrationEvents: AdminBankIntegrationEvent[];
  bankReconciliationSummary: BankReconciliationSummary;
  sePayIntegrationStatus: SePayIntegrationStatus;
  // Automation & Category Management
  categories: CategoryItem[];
  addCategory: (category: Omit<CategoryItem, 'id'>) => Promise<{ success: boolean; categoryId?: string; error?: string }>;
  editCategory: (id: string, updates: Partial<CategoryItem>) => Promise<{ success: boolean; error?: string }>;
  deleteCategory: (id: string) => Promise<{ success: boolean; archived?: boolean; message?: string; error?: string }>;
  recurringTransactions: RecurringTransaction[];
  createRecurring: (
    params: Omit<RecurringTransaction, 'id' | 'createdAt'>
  ) => Promise<{ success: boolean; recurringId?: string; error?: string }>;
  editRecurring: (id: string, updates: Partial<RecurringTransaction>) => Promise<{ success: boolean; error?: string }>;
  deleteRecurring: (id: string) => Promise<{ success: boolean; error?: string }>;
  toggleRecurringStatus: (id: string) => Promise<{ success: boolean; newStatus?: 'active' | 'paused'; error?: string }>;
  recordRecurringOccurrence: (
    recurringId: string,
    occurrenceId: string,
    params: {
      securityPin: string;
      evidenceIds?: string[];
      evidence?: AttachmentMetadata[];
      idempotencyKey?: string;
    }
  ) => Promise<{ success: boolean; error?: string }>;
  activeBudgetAlertsCount: number;
  dueRecurringCount: number;
  advanceRecurringOccurrence: (recurringId: string, dueDate: string, frequency: RecurringFrequency) => void;
  rules: TransactionRule[];
  createRule: (
    params: Omit<TransactionRule, 'id' | 'createdAt'>
  ) => Promise<{ success: boolean; ruleId?: string; error?: string }>;
  editRule: (id: string, updates: Partial<TransactionRule>) => Promise<{ success: boolean; error?: string }>;
  deleteRule: (id: string) => Promise<{ success: boolean; error?: string }>;
  toggleRuleStatus: (id: string) => Promise<{ success: boolean; newStatus?: boolean; error?: string }>;
  // Phase 3A: Transaction Classification Foundation
  classifyTransaction: (params: { transactionId: string; categoryKey: string }) => { success: boolean; error?: string };
  importBankTransactionToHistory: (params: { bankTransactionId: string; walletId?: string }) => { success: boolean; transactionId?: string; error?: string };
  importBankTransactionToLedger?: (params: { bankTransactionId: string; walletId?: string }) => { success: boolean; transactionId?: string; error?: string };
}

export function createDefaultMainWallet(userId: string): WalletAccount {
  return {
    id: `w_main_${userId}`,
    name: 'Ví chính',
    balance: 0,
    currency: 'VND',
    type: 'spending',
    accountNumber: '**** ' + (userId.replace(/\D/g, '').slice(-4) || '1001'),
    isDefault: true,
    colorClass: 'text-emerald-400',
    bgGradient: 'from-slate-800 via-slate-900 to-slate-950',
  };
}

const INITIAL_WALLETS: WalletAccount[] = [];

export function normalizeCategories(rawCategories: unknown[]): CategoryItem[] {
  if (!Array.isArray(rawCategories) || rawCategories.length === 0) {
    return INITIAL_CATEGORIES;
  }

  const normalized: CategoryItem[] = [];
  const seenKeys = new Set<string>();

  for (const raw of rawCategories) {
    if (!raw || typeof raw !== 'object') continue;
    const cat = { ...(raw as CategoryItem) };

    // Migrate old other category keys / system category IDs
    if (cat.id === 'cat_inc_04') {
      cat.key = 'categories.income_other';
      cat.kind = 'income';
      if (!cat.name || cat.name === 'Khác' || cat.name === 'Others') {
        cat.name = 'Thu nhập khác';
      }
      cat.isSystem = true;
    } else if (cat.id === 'cat_exp_08') {
      cat.key = 'categories.expense_other';
      cat.kind = 'expense';
      if (!cat.name || cat.name === 'Khác' || cat.name === 'Others') {
        cat.name = 'Chi tiêu khác';
      }
      cat.isSystem = true;
    } else if (cat.key === 'categories.others') {
      if (cat.kind === 'income') {
        cat.key = 'categories.income_other';
        cat.name = 'Thu nhập khác';
      } else {
        cat.key = 'categories.expense_other';
        cat.name = 'Chi tiêu khác';
      }
    }

    // Ensure all system default categories from INITIAL_CATEGORIES have isSystem: true
    const systemMatch = INITIAL_CATEGORIES.find((sc) => sc.id === cat.id || sc.key === cat.key);
    if (systemMatch) {
      cat.isSystem = true;
      cat.key = systemMatch.key;
      cat.kind = systemMatch.kind;
      cat.icon = cat.icon || systemMatch.icon;
      cat.color = cat.color || systemMatch.color;
    }

    // Deduplicate: avoid duplicate categories.income_other or categories.expense_other or duplicate keys
    if (seenKeys.has(cat.key)) {
      if (cat.isSystem) {
        continue;
      }
      cat.key = `${cat.key}_${cat.id || Math.random().toString(36).substring(2, 6)}`;
    }

    seenKeys.add(cat.key);
    normalized.push(cat);
  }

  // Ensure system categories cat_inc_04 (categories.income_other) and cat_exp_08 (categories.expense_other) are present
  if (!normalized.some((c) => c.key === 'categories.income_other' || c.id === 'cat_inc_04')) {
    const incOther = INITIAL_CATEGORIES.find((c) => c.id === 'cat_inc_04');
    if (incOther) normalized.push(incOther);
  }
  if (!normalized.some((c) => c.key === 'categories.expense_other' || c.id === 'cat_exp_08')) {
    const expOther = INITIAL_CATEGORIES.find((c) => c.id === 'cat_exp_08');
    if (expOther) normalized.push(expOther);
  }

  return normalized;
}

export function normalizeTransaction(tx: Transaction): Transaction {
  const direction: TransactionDirection =
    tx.direction ||
    (tx.type === 'income' ||
    tx.type === 'transfer_in' ||
    tx.type === 'goal_withdrawal' ||
    tx.type === 'deposit' ||
    tx.amount > 0
      ? 'credit'
      : 'debit');

  let semanticType: TransactionSemanticType = tx.semanticType || 'unknown';
  if (!tx.semanticType) {
    if (tx.type === 'income') semanticType = 'income';
    else if (tx.type === 'expense') semanticType = 'expense';
    else if (tx.type === 'transfer_in' || tx.type === 'transfer_out') semanticType = 'transfer';
    else if (tx.type === 'goal_contribution' || tx.type === 'goal_withdrawal')
      semanticType = 'goal_allocation';
    else semanticType = 'unknown';
  }

  let category = tx.category;
  if (category === 'categories.others' || category === 'cat_inc_04' || category === 'cat_exp_08') {
    category = semanticType === 'income' || tx.type === 'income' ? 'categories.income_other' : 'categories.expense_other';
  }

  return {
    ...tx,
    direction,
    semanticType,
    category,
    classificationSource: tx.classificationSource || (tx.isManualEntry ? 'manual' : 'system'),
    classificationConfidence:
      typeof tx.classificationConfidence === 'number' ? tx.classificationConfidence : 1,
    needsCategoryReview: Boolean(tx.needsCategoryReview),
  };
}

function dataUrlToFile(dataUrl: string, fileName: string, mimeType?: string): File {
  const parts = dataUrl.split(',');
  const mime = mimeType || parts[0].match(/:(.*?);/)?.[1] || 'application/octet-stream';
  const bstr = atob(parts[1]);
  let n = bstr.length;
  const u8arr = new Uint8Array(n);
  while (n--) {
    u8arr[n] = bstr.charCodeAt(n);
  }
  return new File([u8arr], fileName, { type: mime });
}

export async function uploadEvidenceItems(items?: AttachmentMetadata[]): Promise<string[]> {
  if (!items || items.length === 0) return [];
  const ids: string[] = [];
  for (const item of items) {
    const isGuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(item.id);
    if (isGuid && !item.dataUrl) {
      ids.push(item.id);
      continue;
    }
    if (item.dataUrl) {
      const file = dataUrlToFile(item.dataUrl, item.name, item.type);
      const res = await financialApiService.uploadEvidence(file);
      ids.push(res.id);
    }
  }
  return ids;
}

function resolveCategoryGuid(catKeyOrId: string, currentCategories: CategoryItem[]): string | null {
  if (/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(catKeyOrId)) {
    return catKeyOrId;
  }
  const matched = currentCategories.find(
    (c) => c.key === catKeyOrId || c.id === catKeyOrId || c.name.toLowerCase() === catKeyOrId.toLowerCase()
  );
  if (matched && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(matched.id)) {
    return matched.id;
  }
  return null;
}

const FinancialContext = createContext<FinancialContextType | undefined>(undefined);

export const FinancialProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const { currentUser } = useAuth();
  const activeUserIdRef = useRef<string | null>(currentUser?.id || null);
  useEffect(() => {
    activeUserIdRef.current = currentUser?.id || null;
  }, [currentUser?.id]);

  const [wallets, setWallets] = useState<WalletAccount[]>([]);
  const [transactions, setTransactions] = useState<Transaction[]>([]);
  const [backendDashboardSummary, setBackendDashboardSummary] = useState<DashboardSummary | null>(null);
  const [backendSpendingCategories, setBackendSpendingCategories] = useState<SpendingCategory[] | null>(null);
  const [backendAttentionItems, setBackendAttentionItems] = useState<AttentionItem[] | null>(null);

  const [budgets, setBudgets] = useState<BudgetCategoryItem[]>([]);
  const [goals, setGoals] = useState<GoalItem[]>([]);
  const [categories, setCategories] = useState<CategoryItem[]>([]);
  const [recurringTransactions, setRecurringTransactions] = useState<RecurringTransaction[]>([]);
  const [rules, setRules] = useState<TransactionRule[]>([]);
  const [isLoading, setIsLoading] = useState<boolean>(true);

  // User SePay bank state (fetched authoritatively from backend, not persisted in localStorage)
  const [bankConnections, setBankConnections] = useState<BankConnection[]>([]);
  const [bankTransactions, setBankTransactions] = useState<BankTransaction[]>([]);
  const [testModeLinkedAccounts, setTestModeLinkedAccounts] = useState<TestModeLinkedAccountDto[]>([]);
  const [walletTopUps, setWalletTopUps] = useState<WalletTopUpDto[]>([]);

  // Admin global bank integration state (fetched authoritatively from backend)
  const [adminBankConnections, setAdminBankConnections] = useState<BankConnection[]>([]);
  const [adminBankTransactions, setAdminBankTransactions] = useState<BankTransaction[]>([]);
  const [adminBankIntegrationEvents, setAdminBankIntegrationEvents] = useState<AdminBankIntegrationEvent[]>([]);
  const [bankReconciliationSummary, setBankReconciliationSummary] = useState<BankReconciliationSummary>(
    DEFAULT_BANK_RECONCILIATION_SUMMARY
  );
  const [sePayIntegrationStatus, setSePayIntegrationStatus] = useState<SePayIntegrationStatus>(
    DEFAULT_SEPAY_INTEGRATION_STATUS
  );

  // Authoritative financial core refresh from locked backend
  const refreshFinancialCore = useCallback(async () => {
    if (!currentUser?.id || currentUser.role === 'SystemAdmin') {
      setIsLoading(false);
      return;
    }
    setIsLoading(true);
    try {
      const now = new Date();
      const currentYear = now.getFullYear();
      const currentMonth = now.getMonth() + 1;

      const [catsDto, walletsDto, dashDto, budgetDtos, goalDtos, recurringDtos, ruleDtos] =
        await Promise.all([
          financialApiService.getCategories({ includeArchived: true }),
          financialApiService.getWallets(),
          financialApiService.getDashboard(),
          planningApiService.getBudgets(currentYear, currentMonth).catch((err) => {
            console.error('Failed to fetch budgets', err);
            return [] as BudgetDto[];
          }),
          planningApiService.getGoals().catch((err) => {
            console.error('Failed to fetch goals', err);
            return [] as GoalDto[];
          }),
          planningApiService.getRecurringTransactions().catch((err) => {
            console.error('Failed to fetch recurring transactions', err);
            return [] as RecurringDto[];
          }),
          planningApiService.getTransactionRules().catch((err) => {
            console.error('Failed to fetch transaction rules', err);
            return [] as TransactionRuleDto[];
          }),
        ]);

      const mappedCats = catsDto.map(mapCategory);
      const mappedWallets = walletsDto.map(mapWallet);

      setCategories(mappedCats);
      setWallets(mappedWallets);
      setBackendDashboardSummary(mapDashboardSummary(dashDto));
      setBackendSpendingCategories(mapSpendingCategories(dashDto.categoryDistribution, mappedCats));
      setBackendAttentionItems(mapAttentionItems(dashDto.attentionItems));

      setBudgets(budgetDtos.map((b) => mapBudget(b, mappedCats)));
      setGoals(goalDtos.map(mapGoal));
      setRecurringTransactions(recurringDtos.map((r) => mapRecurring(r, mappedWallets, mappedCats)));
      setRules(ruleDtos.map(mapTransactionRule));

      // Fetch all transaction pages up to totalCount
      let page = 1;
      const pageSize = 100;
      const allTxDtos: TransactionDto[] = [];
      while (true) {
        const pagedRes = await financialApiService.getTransactions({ page, pageSize });
        if (!pagedRes.items || pagedRes.items.length === 0) break;
        allTxDtos.push(...pagedRes.items);
        if (allTxDtos.length >= pagedRes.totalCount || pagedRes.items.length < pageSize) break;
        page++;
      }
      setTransactions(allTxDtos.map((t) => mapTransaction(t, mappedCats, currentUser?.id)));

      // User SePay bank connections & transactions from locked backend
      const reqUserId = currentUser.id;
      const [connsRes, txsRes, linkedRes, topUpsRes] = await Promise.allSettled([
        bankApiService.getBankConnections(),
        bankApiService.getBankTransactions({ pageSize: 100 }),
        bankApiService.getTestModeLinkedAccounts(),
        bankApiService.getTestModeWalletTopUps(),
      ]);

      if (activeUserIdRef.current !== reqUserId) return;

      if (connsRes.status === 'fulfilled') {
        setBankConnections(connsRes.value.map((c) => mapBankConnection(c, reqUserId)));
      } else {
        console.error('Failed to load bank connections', connsRes.reason);
      }

      if (txsRes.status === 'fulfilled') {
        setBankTransactions(txsRes.value.items.map((t) => mapBankTransaction(t, reqUserId)));
      } else {
        console.error('Failed to load bank transactions', txsRes.reason);
      }

      if (linkedRes.status === 'fulfilled') {
        setTestModeLinkedAccounts(linkedRes.value);
      } else {
        console.error('Failed to load test mode linked accounts', linkedRes.reason);
      }

      if (topUpsRes.status === 'fulfilled') {
        setWalletTopUps(topUpsRes.value);
      } else {
        console.error('Failed to load wallet top ups', topUpsRes.reason);
      }
    } catch (err) {
      console.error('Failed to refresh financial core state from backend', err);
    } finally {
      setIsLoading(false);
    }
  }, [currentUser?.id, currentUser?.role]);

  // Authoritative admin banking refresh
  const refreshAdminBanking = useCallback(async () => {
    if (!currentUser?.id || currentUser.role !== 'SystemAdmin') return;
    const currentUserId = currentUser.id;
    try {
      const [conns, txsRes, events, reconciliation, status] = await Promise.all([
        bankApiService.getAdminBankConnections().catch(() => [] as AdminBankConnectionDto[]),
        bankApiService.getAdminBankTransactions({ pageSize: 100 }).catch(() => ({
          items: [] as AdminBankTransactionDto[],
          page: 1,
          pageSize: 100,
          totalCount: 0,
        })),
        bankApiService.getAdminBankIntegrationEvents().catch(() => [] as AdminBankIntegrationEventDto[]),
        bankApiService.getAdminBankReconciliation().catch(() => null),
        bankApiService.getAdminSePayStatus().catch(() => null),
      ]);

      if (activeUserIdRef.current !== currentUserId) return;

      setAdminBankConnections(conns.map(mapAdminBankConnection));
      setAdminBankTransactions(txsRes.items.map(mapAdminBankTransaction));
      setAdminBankIntegrationEvents(events.map(mapAdminBankIntegrationEvent));
      if (reconciliation) {
        setBankReconciliationSummary(mapBankReconciliationSummary(reconciliation));
      }
      if (status) {
        setSePayIntegrationStatus(mapSePayIntegrationStatus(status));
      }
    } catch (err) {
      console.error('Failed to refresh admin banking', err);
    }
  }, [currentUser?.id, currentUser?.role]);

  // Load budgets for specific month
  const loadBudgetsForMonth = useCallback(
    async (year: number, month: number) => {
      if (!currentUser?.id || currentUser.role === 'SystemAdmin') return;
      try {
        const dtos = await planningApiService.getBudgets(year, month);
        const mapped = dtos.map((d) => mapBudget(d, categories));
        setBudgets((prev) => {
          const monthStr = `${String(month).padStart(2, '0')}/${year}`;
          const otherMonths = prev.filter((b) => b.month !== monthStr);
          return [...otherMonths, ...mapped];
        });
      } catch (err) {
        console.error('Failed to load budgets for month', err);
      }
    },
    [currentUser?.id, currentUser?.role, categories]
  );

  // Hydrate authoritative financial core or admin banking on user switch
  useEffect(() => {
    if (!currentUser?.id) {
      setWallets([]);
      setTransactions([]);
      setCategories([]);
      setBudgets([]);
      setGoals([]);
      setRecurringTransactions([]);
      setRules([]);
      setBackendDashboardSummary(null);
      setBackendSpendingCategories([]);
      setBackendAttentionItems([]);
      setBankConnections([]);
      setBankTransactions([]);
      setTestModeLinkedAccounts([]);
      setWalletTopUps([]);
      setAdminBankConnections([]);
      setAdminBankTransactions([]);
      setAdminBankIntegrationEvents([]);
      setIsLoading(false);
      return;
    }
    if (currentUser.role === 'SystemAdmin') {
      setWallets([]);
      setTransactions([]);
      setCategories([]);
      setBudgets([]);
      setGoals([]);
      setRecurringTransactions([]);
      setRules([]);
      setBackendDashboardSummary(null);
      setBackendSpendingCategories([]);
      setBackendAttentionItems([]);
      setBankConnections([]);
      setBankTransactions([]);
      setTestModeLinkedAccounts([]);
      setWalletTopUps([]);
      refreshAdminBanking();
      return;
    }
    setBankConnections([]);
    setBankTransactions([]);
    setTestModeLinkedAccounts([]);
    setWalletTopUps([]);
    setAdminBankConnections([]);
    setAdminBankTransactions([]);
    setAdminBankIntegrationEvents([]);
    refreshFinancialCore();
  }, [currentUser?.id, currentUser?.role, refreshFinancialCore, refreshAdminBanking]);

  // Live listener for correction grants dispatched in current window session
  useEffect(() => {
    if (!currentUser?.id) return;
    const handleCorrectionGranted = (e: any) => {
      const { userId, grant } = e?.detail || {};
      if (userId && currentUser.id === userId && grant) {
        refreshFinancialCore();
      }
    };

    window.addEventListener('spendwise_correction_granted', handleCorrectionGranted);
    return () => {
      window.removeEventListener('spendwise_correction_granted', handleCorrectionGranted);
    };
  }, [currentUser?.id, refreshFinancialCore]);

  // Dynamic calculations from shared state
  const totalWalletsBalance = useMemo(() => {
    return wallets.reduce((sum, w) => sum + w.balance, 0);
  }, [wallets]);

  // Goals are internal allocation, not expense/income
  // Total tracked assets = sum Wallet balances + sum active/completed Goal currentAmount
  const totalGoalsReserved = useMemo(() => {
    return goals
      .filter((g) => g.status === 'active' || g.status === 'completed' || !g.status)
      .reduce((sum, g) => sum + (g.currentAmount || 0), 0);
  }, [goals]);

  const totalTrackedAssets = useMemo(() => {
    return totalWalletsBalance + totalGoalsReserved;
  }, [totalWalletsBalance, totalGoalsReserved]);

  // Derive income/expense strictly for this month and year
  const monthlyStats = useMemo(() => {
    const now = new Date();
    const currentMonth = now.getMonth();
    const currentYear = now.getFullYear();

    let income = 0;
    let expense = 0;

    transactions.forEach((tx) => {
      if (tx.status === 'completed' && Boolean(currentUser?.id) && tx.userId === currentUser?.id) {
        const txDate = getTransactionEffectiveDate(tx);
        if (txDate && txDate.getMonth() === currentMonth && txDate.getFullYear() === currentYear) {
          // Semantic classification check:
          // transfer, group_settlement, refund, goal_allocation, unknown are NOT normal personal income/expense
          const isExcluded =
            tx.semanticType === 'transfer' ||
            tx.semanticType === 'group_settlement' ||
            tx.semanticType === 'refund' ||
            tx.semanticType === 'goal_allocation' ||
            tx.semanticType === 'unknown';

          if (!isExcluded) {
            const isIncome =
              tx.semanticType === 'income' || (!tx.semanticType && tx.type === 'income');
            const isExpense =
              tx.semanticType === 'expense' || (!tx.semanticType && tx.type === 'expense');

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
    };
  }, [transactions, currentUser?.id]);

  // Derive spending categories dynamically from current month's completed expenses (fallback if backend not available)
  const derivedSpendingCategories: SpendingCategory[] = useMemo(() => {
    const now = new Date();
    const currentMonth = now.getMonth();
    const currentYear = now.getFullYear();

    const categoryMap: Record<string, number> = {};
    let totalExpense = 0;

    transactions.forEach((tx) => {
      if (tx.status === 'completed' && Boolean(currentUser?.id) && tx.userId === currentUser?.id) {
        const isExcluded =
          tx.semanticType === 'transfer' ||
          tx.semanticType === 'group_settlement' ||
          tx.semanticType === 'refund' ||
          tx.semanticType === 'goal_allocation' ||
          tx.semanticType === 'unknown';
        const isExpense =
          !isExcluded &&
          (tx.semanticType === 'expense' || (!tx.semanticType && tx.type === 'expense'));

        if (isExpense) {
          const txDate = getTransactionEffectiveDate(tx);
          if (txDate && txDate.getMonth() === currentMonth && txDate.getFullYear() === currentYear) {
            const cat = tx.category || 'categories.expense_other';
            const amt = Math.abs(tx.amount);
            categoryMap[cat] = (categoryMap[cat] || 0) + amt;
            totalExpense += amt;
          }
        }
      }
    });

    if (totalExpense === 0) {
      return [];
    }

    const styleMap: Record<string, { color: string; badgeBg: string; icon: string }> = {
      'categories.food': { color: '#F59E0B', badgeBg: 'bg-amber-500/10', icon: 'Utensils' },
      'categories.shopping': { color: '#EC4899', badgeBg: 'bg-pink-500/10', icon: 'ShoppingBag' },
      'categories.housing': { color: '#3B82F6', badgeBg: 'bg-blue-500/10', icon: 'Home' },
      'categories.transportation': { color: '#10B981', badgeBg: 'bg-emerald-500/10', icon: 'Car' },
      'categories.entertainment': { color: '#8B5CF6', badgeBg: 'bg-purple-500/10', icon: 'Gamepad2' },
      'categories.utilities': { color: '#06B6D4', badgeBg: 'bg-cyan-500/10', icon: 'Zap' },
      'categories.health': { color: '#EF4444', badgeBg: 'bg-rose-500/10', icon: 'HeartPulse' },
      'categories.education': { color: '#6366F1', badgeBg: 'bg-indigo-500/10', icon: 'GraduationCap' },
      'categories.other': { color: '#64748B', badgeBg: 'bg-slate-500/10', icon: 'Layers' },
      'categories.expense_other': { color: '#64748B', badgeBg: 'bg-slate-500/10', icon: 'Tag' },
      'categories.others': { color: '#64748B', badgeBg: 'bg-slate-500/10', icon: 'Tag' },
    };

    return Object.entries(categoryMap)
      .map(([catKey, amount], index) => {
        const matchingCategory = categories.find((c) => c.key === catKey || c.id === catKey);
        const nameKey = matchingCategory?.key || catKey;
        const name = matchingCategory?.name || catKey.replace('categories.', '');
        const style = styleMap[catKey] || {
          color: '#64748B',
          badgeBg: 'bg-slate-500/10',
          icon: 'Layers',
        };
        const percentage = Math.round((amount / totalExpense) * 100);

        return {
          id: `sp-cat-${index}-${catKey}`,
          nameKey,
          name,
          amount,
          percentage,
          color: style.color,
          badgeBg: style.badgeBg,
          icon: style.icon,
        };
      })
      .sort((a, b) => b.amount - a.amount);
  }, [transactions, categories, currentUser?.id]);

  const spendingCategories: SpendingCategory[] = useMemo(() => {
    if (backendSpendingCategories && backendSpendingCategories.length > 0) {
      return backendSpendingCategories;
    }
    return derivedSpendingCategories;
  }, [backendSpendingCategories, derivedSpendingCategories]);

  // Derive attention items dynamically from actual user state
  const attentionItems: AttentionItem[] = useMemo(() => {
    const items: AttentionItem[] = [];

    // 1. Wallets warning if wallet has 0 or negative balance
    wallets.forEach((w) => {
      if (w.balance <= 0) {
        items.push({
          id: `att-wallet-${w.id}`,
          messageKey: 'dashboard.walletZeroBalanceAlert',
          messageParams: {
            walletName: w.name,
            balance: `${w.balance.toLocaleString('vi-VN')} ₫`,
          },
          rawMessage: `Ví "${w.name}" hiện có số dư ${w.balance.toLocaleString('vi-VN')} ₫.`,
          rawMessageEn: `Wallet "${w.name === 'Ví chính' ? 'Main Wallet' : w.name}" currently has a balance of ₫${w.balance.toLocaleString('en-US')}.`,
          actionLabelKey: 'dashboard.topUp',
          actionLabel: 'Nạp tiền',
          actionLabelEn: 'Top up',
          actionRoute: '/wallet',
          severity: 'reminder',
          iconType: 'budget',
        });
      }
    });

    // 2. Budgets near or exceeded
    const now = new Date();
    const currentMonthNum = now.getMonth() + 1;
    const currentYear = now.getFullYear();
    const currentMonthFormatted = `${String(currentMonthNum).padStart(2, '0')}/${currentYear}`;

    budgets
      .filter((b) => b.month === currentMonthFormatted)
      .forEach((b) => {
        const spent = transactions
          .filter((tx) => {
            if (!currentUser?.id || tx.userId !== currentUser.id) return false;
            if (tx.status !== 'completed') return false;
            if (tx.category !== b.categoryKey) return false;

            // Budget spending must count ONLY real Expense semantics: semanticType === 'expense'
            let isExpense = false;
            if (tx.semanticType) {
              isExpense = tx.semanticType === 'expense';
            } else {
              isExpense = tx.type === 'expense';
            }
            if (!isExpense) return false;

            // Transaction date priority: occurredAt -> date -> rawDate. Skip invalid dates.
            const txDate = getTransactionEffectiveDate(tx);
            if (!txDate) return false;

            return (
              txDate.getMonth() + 1 === currentMonthNum &&
              txDate.getFullYear() === currentYear
            );
          })
          .reduce((sum, tx) => sum + Math.abs(tx.amount), 0);

        if (b.limit > 0 && spent >= b.limit) {
          const pct = Math.round((spent / b.limit) * 100);
          items.push({
            id: `att-budget-over-${b.id}`,
            messageKey: 'dashboard.budgetExceededAlert',
            messageParams: {
              budgetName: b.name,
              percentage: pct,
            },
            rawMessage: `Ngân sách "${b.name}" đã vượt hạn mức (${pct}%).`,
            rawMessageEn: `Budget "${b.name}" has exceeded limit (${pct}%).`,
            actionLabelKey: 'common.viewBudget',
            actionLabel: 'Xem ngân sách',
            actionLabelEn: 'View budget',
            actionRoute: '/budget',
            severity: 'warning',
            iconType: 'budget',
          });
        } else if (b.limit > 0 && spent >= b.limit * 0.9) {
          const pct = Math.round((spent / b.limit) * 100);
          items.push({
            id: `att-budget-near-${b.id}`,
            messageKey: 'dashboard.budgetNearLimitAlert',
            messageParams: {
              budgetName: b.name,
              percentage: pct,
            },
            rawMessage: `Ngân sách "${b.name}" đã sử dụng ${pct}% hạn mức.`,
            rawMessageEn: `Budget "${b.name}" has used ${pct}% of limit.`,
            actionLabelKey: 'common.viewDetails',
            actionLabel: 'Xem chi tiết',
            actionLabelEn: 'View details',
            actionRoute: '/budget',
            severity: 'reminder',
            iconType: 'budget',
          });
        }
      });

    const backendItems = backendAttentionItems || [];
    return [...backendItems, ...items];
  }, [backendAttentionItems, wallets, budgets, transactions, currentUser?.id]);

  // Derive budget remaining
  const totalBudgetRemaining = useMemo(() => {
    const now = new Date();
    const currentMonthNum = now.getMonth() + 1;
    const currentYear = now.getFullYear();
    const currentMonthFormatted = `${String(currentMonthNum).padStart(2, '0')}/${currentYear}`;

    let totalLimit = 0;
    let totalSpent = 0;

    budgets
      .filter((b) => b.month === currentMonthFormatted)
      .forEach((b) => {
        totalLimit += b.limit;
        const spentForCat = transactions
          .filter((tx) => {
            if (!currentUser?.id || tx.userId !== currentUser.id) return false;
            if (tx.status !== 'completed') return false;
            if (tx.category !== b.categoryKey) return false;

            let isExpense = false;
            if (tx.semanticType) {
              isExpense = tx.semanticType === 'expense';
            } else {
              isExpense = tx.type === 'expense';
            }
            if (!isExpense) return false;

            const txDate = getTransactionEffectiveDate(tx);
            if (!txDate) return false;

            return (
              txDate.getMonth() + 1 === currentMonthNum &&
              txDate.getFullYear() === currentYear
            );
          })
          .reduce((sum, tx) => sum + Math.abs(tx.amount), 0);
        totalSpent += spentForCat;
      });

    return Math.max(0, totalLimit - totalSpent);
  }, [budgets, transactions, currentUser?.id]);

  const dashboardSummary: DashboardSummary = useMemo(() => {
    if (backendDashboardSummary) {
      return backendDashboardSummary;
    }
    return {
      totalBalance: totalWalletsBalance,
      totalTrackedAssets,
      totalWalletsBalance,
      totalGoalsReserved,
      incomeThisMonth: monthlyStats.income,
      expenseThisMonth: monthlyStats.expense,
      budgetRemaining: totalBudgetRemaining,
      othersOweUser: 0,
      userOwesOthers: 0,
      balanceChangePercentage: undefined,
      cashFlowDifference: monthlyStats.difference,
    };
  }, [backendDashboardSummary, totalWalletsBalance, totalTrackedAssets, totalGoalsReserved, monthlyStats, totalBudgetRemaining]);

  const getWallet = useCallback(
    (id: string) => {
      return wallets.find((w) => w.id === id);
    },
    [wallets]
  );

  // 1. ADD INCOME FLOW
  const addIncome = useCallback(
    async (params: AddTransactionParams) => {
      const { walletId, amount, category, description, date, occurredAt, counterparty, evidence, securityPin } = params;
      if (amount <= 0) {
        return { success: false, error: 'Số tiền phải lớn hơn 0' };
      }

      // Evidence is strictly required for manual income
      if (!evidence || !Array.isArray(evidence) || evidence.length === 0) {
        return {
          success: false,
          error: 'Chứng từ giao dịch (hình ảnh hoặc hóa đơn PDF) là bắt buộc khi tạo khoản thu thủ công.',
        };
      }

      const targetWallet = walletId ? wallets.find((w) => w.id === walletId) : (wallets.find((w) => w.isDefault) || wallets[0]);
      if (!targetWallet) {
        return { success: false, error: 'Ví thanh toán không tồn tại hoặc không hợp lệ.' };
      }

      try {
        const evidenceIds = await uploadEvidenceItems(evidence);
        const categoryId = resolveCategoryGuid(category, categories);
        if (!categoryId) {
          return { success: false, error: 'Danh mục không hợp lệ hoặc không tồn tại.' };
        }
        const idempotencyKey = crypto.randomUUID();
        const nowIso = new Date().toISOString();
        const isoOccurredAt = occurredAt ? new Date(occurredAt).toISOString() : (date ? new Date(date).toISOString() : nowIso);

        const res = await financialApiService.createIncome({
          walletId: targetWallet.id,
          categoryId,
          amount: Math.abs(amount),
          description: description || 'Khoản thu nhập',
          occurredAt: isoOccurredAt,
          idempotencyKey,
          securityPin,
          evidenceIds: evidenceIds.length > 0 ? evidenceIds : undefined,
          counterparty,
        });
        await refreshFinancialCore();
        return { success: true, txId: res.id };
      } catch (err) {
        const apiErr = err as ApiError;
        return {
          success: false,
          error: apiErr.detail || apiErr.title || 'Lỗi khi tạo giao dịch thu nhập.',
        };
      }
    },
    [wallets, categories, refreshFinancialCore]
  );

  // 2. ADD EXPENSE FLOW
  const addExpense = useCallback(
    async (params: AddTransactionParams) => {
      const { walletId, amount, category, description, date, occurredAt, counterparty, evidence, securityPin } = params;
      if (amount <= 0) {
        return { success: false, error: 'Số tiền phải lớn hơn 0' };
      }

      // Evidence is strictly required for manual expense
      if (!evidence || !Array.isArray(evidence) || evidence.length === 0) {
        return {
          success: false,
          error: 'Chứng từ giao dịch (hình ảnh hoặc hóa đơn PDF) là bắt buộc khi tạo khoản chi thủ công.',
        };
      }

      const targetWallet = walletId ? wallets.find((w) => w.id === walletId) : (wallets.find((w) => w.isDefault) || wallets[0]);
      if (!targetWallet) {
        return { success: false, error: 'Ví thanh toán không tồn tại hoặc không hợp lệ.' };
      }
      if (amount > targetWallet.balance) {
        return { success: false, error: 'Số dư không đủ để thực hiện giao dịch.' };
      }

      try {
        const evidenceIds = await uploadEvidenceItems(evidence);
        const categoryId = resolveCategoryGuid(category, categories);
        if (!categoryId) {
          return { success: false, error: 'Danh mục không hợp lệ hoặc không tồn tại.' };
        }
        const idempotencyKey = crypto.randomUUID();
        const nowIso = new Date().toISOString();
        const isoOccurredAt = occurredAt ? new Date(occurredAt).toISOString() : (date ? new Date(date).toISOString() : nowIso);

        const res = await financialApiService.createExpense({
          walletId: targetWallet.id,
          categoryId,
          amount: Math.abs(amount),
          description: description || 'Khoản chi tiêu',
          occurredAt: isoOccurredAt,
          idempotencyKey,
          securityPin,
          evidenceIds: evidenceIds.length > 0 ? evidenceIds : undefined,
          counterparty,
        });
        await refreshFinancialCore();
        return { success: true, txId: res.id };
      } catch (err) {
        const apiErr = err as ApiError;
        return {
          success: false,
          error: apiErr.detail || apiErr.title || 'Lỗi khi tạo giao dịch chi tiêu.',
        };
      }
    },
    [wallets, categories, refreshFinancialCore]
  );

  // 3. DELETE TRANSACTION FLOW (Hard Delete is Strictly Prohibited for Finalized Transactions)
  const deleteTransaction = useCallback(
    (_id: string) => {
      return {
        success: false,
        error: 'IMMUTABLE_TRANSACTION',
      };
    },
    []
  );

  // 4. CORRECT TRANSACTION FLOW (Controlled, Audit-trailed manual corrections)
  const correctTransaction = useCallback(
    async (params: CorrectTransactionParams) => {
      const { id, amount, walletId, category, description, occurredAt, counterparty, reason, additionalEvidence, securityPin } = params;

      if (!currentUser?.id) {
        return {
          success: false,
          error: 'Vui lòng đăng nhập để thực hiện điều chỉnh giao dịch.',
        };
      }

      if (currentUser.role === 'SystemAdmin') {
        return {
          success: false,
          error: 'Quản trị viên chỉ có quyền kiểm tra và cấp lượt sửa, không thể trực tiếp sửa sổ cái tài chính.',
        };
      }

      if (!reason || !reason.trim()) {
        return {
          success: false,
          error: 'Lý do điều chỉnh là bắt buộc.',
        };
      }

      try {
        const additionalEvidenceIds = await uploadEvidenceItems(additionalEvidence);
        let categoryId: string | undefined = undefined;
        if (category) {
          const resolved = resolveCategoryGuid(category, categories);
          if (!resolved) {
            return { success: false, error: 'Danh mục điều chỉnh không hợp lệ hoặc không tồn tại.' };
          }
          categoryId = resolved;
        }
        const targetWalletId = walletId ? (wallets.find((w) => w.id === walletId)?.id) : undefined;
        const isoOccurredAt = occurredAt ? new Date(occurredAt).toISOString() : undefined;

        await financialApiService.correctTransaction(id, {
          categoryId,
          description: description || '',
          securityPin,
          evidenceIds: additionalEvidenceIds.length > 0 ? additionalEvidenceIds : undefined,
          reason: reason.trim(),
          occurredAt: isoOccurredAt,
          walletId: targetWalletId,
          amount: amount ? Math.abs(amount) : undefined,
          counterparty,
        });
        await refreshFinancialCore();
        return { success: true };
      } catch (err) {
        const apiErr = err as ApiError;
        return {
          success: false,
          error: apiErr.detail || apiErr.title || 'Lỗi khi điều chỉnh giao dịch.',
        };
      }
    },
    [currentUser?.id, currentUser?.role, wallets, categories, refreshFinancialCore]
  );

  // 5. EDIT TRANSACTION WRAPPER (backward compatible)
  const editTransaction = useCallback(
    async (
      id: string,
      updates: Partial<AddTransactionParams & { title?: string; reason?: string; additionalEvidence?: AttachmentMetadata[] }>
    ) => {
      if (updates.reason) {
        return correctTransaction({
          id,
          amount: updates.amount,
          walletId: updates.walletId,
          category: updates.category,
          description: updates.description || updates.title,
          occurredAt: updates.occurredAt || updates.date,
          counterparty: updates.counterparty,
          reason: updates.reason,
          additionalEvidence: updates.additionalEvidence,
          securityPin: updates.securityPin,
        });
      }

      return {
        success: false,
        error: 'Điều chỉnh giao dịch thủ công yêu cầu cung cấp lý do điều chỉnh để ghi nhận vào lịch sử kiểm toán.',
      };
    },
    [correctTransaction]
  );

  // 5C. GET TRANSACTION DETAIL (Live fetch with complete evidence, corrections, grants)
  const getTransactionDetail = useCallback(
    async (transactionId: string): Promise<Transaction> => {
      const dto = await financialApiService.getTransaction(transactionId);
      return mapTransaction(dto, categories, currentUser?.id);
    },
    [categories, currentUser?.id]
  );

  // 5D. DOWNLOAD EVIDENCE BLOB
  const downloadEvidenceBlob = useCallback(
    async (evidenceId: string): Promise<Blob> => {
      return financialApiService.getEvidenceBlob(evidenceId);
    },
    []
  );

  // 6. BUDGETS MANAGEMENT
  const createBudget = useCallback(
    async (params: CreateBudgetParams) => {
      try {
        const categoryId = params.categoryId || resolveCategoryGuid(params.categoryKey, categories);
        if (!categoryId) {
          return { success: false, error: 'Danh mục không hợp lệ hoặc không tồn tại.' };
        }
        let y = new Date().getFullYear();
        let m = new Date().getMonth() + 1;
        if (params.month) {
          if (params.month.includes('/')) {
            const parts = params.month.split('/').map(Number);
            m = parts[0];
            y = parts[1];
          } else if (params.month.includes('-')) {
            const parts = params.month.split('-').map(Number);
            y = parts[0];
            m = parts[1];
          }
        }
        const res = await planningApiService.createBudget({
          categoryId,
          year: y,
          month: m,
          limitAmount: params.limit,
          alertsEnabled: params.alertsEnabled ?? true,
          rollover: params.rollover ?? false,
          securityPin: params.securityPin || null,
        });
        await loadBudgetsForMonth(y, m);
        return { success: true, budgetId: res.id };
      } catch (err) {
        const apiErr = err as ApiError;
        return {
          success: false,
          error: apiErr.detail || apiErr.title || 'Lỗi khi tạo ngân sách.',
        };
      }
    },
    [categories, loadBudgetsForMonth]
  );

  const updateBudgetLimit = useCallback(
    async (budgetId: string, newLimit: number, securityPin?: string) => {
      try {
        const target = budgets.find((b) => b.id === budgetId);
        const res = await planningApiService.updateBudget(budgetId, {
          limitAmount: Math.max(100000, newLimit),
          alertsEnabled: target?.alertsEnabled ?? true,
          rollover: target?.rollover ?? false,
          securityPin: securityPin || null,
        });
        setBudgets((prev) => prev.map((b) => (b.id === budgetId ? mapBudget(res, categories) : b)));
        return { success: true };
      } catch (err) {
        const apiErr = err as ApiError;
        return {
          success: false,
          error: apiErr.detail || apiErr.title || 'Lỗi khi cập nhật hạn mức ngân sách.',
        };
      }
    },
    [budgets, categories]
  );

  const editBudget = useCallback(
    async (budgetId: string, updates: Partial<CreateBudgetParams> & { securityPin?: string }) => {
      try {
        const target = budgets.find((b) => b.id === budgetId);
        const limitAmount = updates.limit !== undefined ? Math.max(100000, updates.limit) : (target?.limit || 100000);
        const alertsEnabled = updates.alertsEnabled !== undefined ? updates.alertsEnabled : (target?.alertsEnabled ?? true);
        const rollover = updates.rollover !== undefined ? updates.rollover : (target?.rollover ?? false);
        const res = await planningApiService.updateBudget(budgetId, {
          limitAmount,
          alertsEnabled,
          rollover,
          securityPin: updates.securityPin || null,
        });
        setBudgets((prev) => prev.map((b) => (b.id === budgetId ? mapBudget(res, categories) : b)));
        return { success: true };
      } catch (err) {
        const apiErr = err as ApiError;
        return {
          success: false,
          error: apiErr.detail || apiErr.title || 'Lỗi khi cập nhật ngân sách.',
        };
      }
    },
    [budgets, categories]
  );

  const deleteBudget = useCallback(
    async (budgetId: string, securityPin?: string) => {
      try {
        await planningApiService.deleteBudget(budgetId, {
          securityPin: securityPin || '',
        });
        setBudgets((prev) => prev.filter((b) => b.id !== budgetId));
        return { success: true };
      } catch (err) {
        const apiErr = err as ApiError;
        return {
          success: false,
          error: apiErr.detail || apiErr.title || 'Lỗi khi xóa ngân sách.',
        };
      }
    },
    []
  );

  // 7. GOALS MANAGEMENT & CONTRIBUTIONS
  const createGoal = useCallback(
    async (params: CreateGoalParams) => {
      const {
        title,
        category,
        categoryId,
        targetAmount,
        initialAmount = 0,
        walletId,
        deadline,
        iconName = 'Target',
        colorHex = '#10B981',
        securityPin,
      } = params;

      if (!targetAmount || targetAmount <= 0) {
        return { success: false, error: 'Số tiền mục tiêu không hợp lệ.' };
      }

      if (initialAmount < 0) {
        return { success: false, error: 'Số tiền khởi tạo không thể âm.' };
      }

      if (initialAmount > targetAmount) {
        return { success: false, error: 'Số tiền khởi tạo không thể vượt quá số tiền mục tiêu.' };
      }

      let resolvedCatId: string | null = categoryId || null;
      if (!resolvedCatId && category) {
        resolvedCatId = resolveCategoryGuid(category, categories);
      }

      try {
        const res = await planningApiService.createGoal({
          name: title,
          targetAmount,
          targetDate: deadline ? new Date(deadline).toISOString() : null,
          categoryId: resolvedCatId,
          iconName,
          colorHex,
          initialAmount: initialAmount > 0 ? initialAmount : undefined,
          walletId: initialAmount > 0 ? (walletId || null) : null,
          idempotencyKey: crypto.randomUUID(),
          securityPin: securityPin || null,
        });

        if (initialAmount > 0) {
          await refreshFinancialCore();
        } else {
          const updatedGoals = await planningApiService.getGoals();
          setGoals(updatedGoals.map(mapGoal));
        }
        return { success: true, goalId: res.id };
      } catch (err) {
        const apiErr = err as ApiError;
        return {
          success: false,
          error: apiErr.detail || apiErr.title || 'Lỗi khi tạo mục tiêu.',
        };
      }
    },
    [categories, refreshFinancialCore]
  );

  const editGoal = useCallback(
    async (goalId: string, updates: Partial<CreateGoalParams> & { securityPin?: string }) => {
      const target = goals.find((g) => g.id === goalId);
      if (!target) return { success: false, error: 'Không tìm thấy mục tiêu.' };

      let finalCatId: string | null;
      if (updates.categoryId !== undefined) {
        finalCatId = updates.categoryId || null;
      } else if (updates.category !== undefined) {
        finalCatId = resolveCategoryGuid(updates.category, categories);
      } else {
        finalCatId = target.categoryId || null;
      }

      try {
        const res = await planningApiService.updateGoal(goalId, {
          name: updates.title || target.title,
          targetAmount: updates.targetAmount !== undefined ? updates.targetAmount : target.targetAmount,
          targetDate: updates.deadline ? new Date(updates.deadline).toISOString() : (target.deadline ? new Date(target.deadline).toISOString() : null),
          categoryId: finalCatId,
          iconName: updates.iconName || target.iconName || null,
          colorHex: updates.colorHex || target.colorHex || null,
          securityPin: updates.securityPin || null,
        });
        setGoals((prev) => prev.map((g) => (g.id === goalId ? mapGoal(res) : g)));
        return { success: true };
      } catch (err) {
        const apiErr = err as ApiError;
        return {
          success: false,
          error: apiErr.detail || apiErr.title || 'Lỗi khi cập nhật mục tiêu.',
        };
      }
    },
    [goals, categories]
  );

  const deleteGoal = useCallback(
    async (goalId: string, securityPin?: string) => {
      try {
        await planningApiService.deleteGoal(goalId, {
          securityPin: securityPin || '',
        });
        setGoals((prev) => prev.filter((g) => g.id !== goalId));
        return { success: true };
      } catch (err) {
        const apiErr = err as ApiError;
        return {
          success: false,
          error: apiErr.detail || apiErr.title || 'Lỗi khi xóa mục tiêu.',
        };
      }
    },
    []
  );

  // Contribute to Goal (Handled entirely by backend ledger movement)
  const contributeToGoal = useCallback(
    async (params: { goalId: string; walletId: string; amount: number; securityPin?: string }) => {
      try {
        await planningApiService.contributeGoal(params.goalId, {
          walletId: params.walletId,
          amount: params.amount,
          idempotencyKey: crypto.randomUUID(),
          securityPin: params.securityPin || null,
        });
        await refreshFinancialCore();
        return { success: true };
      } catch (err) {
        const apiErr = err as ApiError;
        return {
          success: false,
          error: apiErr.detail || apiErr.title || 'Lỗi khi tích lũy mục tiêu.',
        };
      }
    },
    [refreshFinancialCore]
  );

  // Withdraw from Goal (Handled entirely by backend ledger movement)
  const withdrawFromGoal = useCallback(
    async (params: { goalId: string; walletId: string; amount: number; securityPin?: string }) => {
      try {
        await planningApiService.withdrawGoal(params.goalId, {
          walletId: params.walletId,
          amount: params.amount,
          idempotencyKey: crypto.randomUUID(),
          securityPin: params.securityPin || null,
        });
        await refreshFinancialCore();
        return { success: true };
      } catch (err) {
        const apiErr = err as ApiError;
        return {
          success: false,
          error: apiErr.detail || apiErr.title || 'Lỗi khi rút tiền mục tiêu.',
        };
      }
    },
    [refreshFinancialCore]
  );

  // 10. WALLET OPERATIONS (Internal Transfer, Create Wallet, Update Wallet, Set Default, Close Wallet)
  const transferBetweenWallets = useCallback(
    async (params: {
      sourceWalletId: string;
      destWalletId: string;
      amount: number;
      description?: string;
      securityPin?: string;
    }) => {
      const { sourceWalletId, destWalletId, amount, description, securityPin } = params;
      const source = wallets.find((w) => w.id === sourceWalletId);
      const dest = wallets.find((w) => w.id === destWalletId);

      if (!source || !dest) return { success: false, error: 'Ví không tồn tại.' };
      if (source.id === dest.id) return { success: false, error: 'Ví nguồn và ví đích không được trùng nhau.' };
      if (amount <= 0) return { success: false, error: 'Số tiền chuyển phải lớn hơn 0.' };
      if (amount > source.balance) return { success: false, error: 'Số dư ví nguồn không đủ.' };

      try {
        const res = await financialApiService.transferInternal({
          sourceWalletId,
          destinationWalletId: destWalletId,
          amount,
          description: description || `Chuyển tiền từ "${source.name}" sang "${dest.name}"`,
          idempotencyKey: crypto.randomUUID(),
          securityPin,
        });
        await refreshFinancialCore();
        return { success: true, txId: res.id };
      } catch (err) {
        const apiErr = err as ApiError;
        return {
          success: false,
          error: apiErr.detail || apiErr.title || 'Lỗi khi chuyển tiền nội bộ.',
        };
      }
    },
    [wallets, refreshFinancialCore]
  );

  const createWallet = useCallback(
    async (params: { name: string; type?: 'spending' | 'savings' | 'emergency'; initialBalance?: number }) => {
      const { name, type = 'spending' } = params;
      if (!name.trim()) return { success: false, error: 'Vui lòng nhập tên ví.' };

      if (wallets.length >= 3) {
        return {
          success: false,
          error: 'Mỗi tài khoản chỉ được tạo tối đa 3 ví cá nhân (1 ví chính + 2 ví phụ).',
        };
      }

      try {
        const res = await financialApiService.createWallet({
          name: name.trim(),
          type,
          isDefault: wallets.length === 0,
        });
        await refreshFinancialCore();
        return { success: true, walletId: res.id };
      } catch (err) {
        const apiErr = err as ApiError;
        return {
          success: false,
          error: apiErr.detail || apiErr.title || 'Lỗi khi tạo ví.',
        };
      }
    },
    [wallets.length, refreshFinancialCore]
  );

  const updateWallet = useCallback(
    async (walletId: string, updates: { name: string; type?: 'spending' | 'savings' | 'emergency' }) => {
      const current = wallets.find((w) => w.id === walletId);
      if (!current) return { success: false, error: 'Không tìm thấy ví.' };

      try {
        await financialApiService.updateWallet(walletId, {
          name: updates.name.trim() || current.name,
          type: updates.type || current.type,
          isDefault: current.isDefault,
        });
        await refreshFinancialCore();
        return { success: true };
      } catch (err) {
        const apiErr = err as ApiError;
        return {
          success: false,
          error: apiErr.detail || apiErr.title || 'Lỗi khi cập nhật ví.',
        };
      }
    },
    [wallets, refreshFinancialCore]
  );

  const setDefaultWallet = useCallback(
    async (walletId: string) => {
      const current = wallets.find((w) => w.id === walletId);
      if (!current) return { success: false, error: 'Không tìm thấy ví.' };

      try {
        await financialApiService.updateWallet(walletId, {
          name: current.name,
          type: current.type,
          isDefault: true,
        });
        await refreshFinancialCore();
        return { success: true };
      } catch (err) {
        const apiErr = err as ApiError;
        return {
          success: false,
          error: apiErr.detail || apiErr.title || 'Lỗi khi đặt ví mặc định.',
        };
      }
    },
    [wallets, refreshFinancialCore]
  );

  const closeWallet = useCallback(
    async (walletId: string) => {
      const current = wallets.find((w) => w.id === walletId);
      if (!current) return { success: false, error: 'Không tìm thấy ví.' };

      try {
        await financialApiService.deleteWallet(walletId);
        await refreshFinancialCore();
        return { success: true };
      } catch (err) {
        const apiErr = err as ApiError;
        return {
          success: false,
          error: apiErr.detail || apiErr.title || 'Lỗi khi đóng ví.',
        };
      }
    },
    [wallets, refreshFinancialCore]
  );

  // External Bank Integration Handlers (SePay Unified Banking)
  const refreshBankConnections = useCallback(async (): Promise<{
    bankConnections: BankConnection[];
    testModeLinkedAccounts: TestModeLinkedAccountDto[];
  }> => {
    if (!currentUser?.id) return { bankConnections: [], testModeLinkedAccounts: [] };
    const reqUserId = currentUser.id;
    try {
      const conns = await bankApiService.getBankConnections();
      if (activeUserIdRef.current !== reqUserId) return { bankConnections: [], testModeLinkedAccounts: [] };
      const mappedConns = conns.map((c) => mapBankConnection(c, reqUserId));
      setBankConnections(mappedConns);

      let linked: TestModeLinkedAccountDto[] = [];
      try {
        linked = await bankApiService.getTestModeLinkedAccounts();
        if (activeUserIdRef.current === reqUserId) {
          setTestModeLinkedAccounts(linked);
        }
      } catch (linkedErr) {
        console.error('Failed to refresh test mode linked accounts', linkedErr);
      }
      return { bankConnections: mappedConns, testModeLinkedAccounts: linked };
    } catch (err) {
      console.error('Failed to refresh bank connections', err);
      return { bankConnections: [], testModeLinkedAccounts: [] };
    }
  }, [currentUser?.id]);

  const refreshWalletTopUps = useCallback(async (): Promise<WalletTopUpDto[]> => {
    if (!currentUser?.id) return [];
    const reqUserId = currentUser.id;
    try {
      const topUps = await bankApiService.getTestModeWalletTopUps();
      if (activeUserIdRef.current !== reqUserId) return [];
      setWalletTopUps(topUps);
      return topUps;
    } catch (err) {
      console.error('Failed to refresh wallet top ups', err);
      return [];
    }
  }, [currentUser?.id]);

  const startBankLink = useCallback(async () => {
    try {
      const challenge = await bankApiService.startTestModeLink();
      return { success: true, challenge };
    } catch (err) {
      const apiErr = err as ApiError;
      return {
        success: false,
        code: apiErr.code,
        error: apiErr.detail || apiErr.title || 'Không thể khởi tạo mã liên kết ngân hàng.',
      };
    }
  }, []);

  const confirmBankLink = useCallback(
    async (challengeId: string) => {
      if (!currentUser?.id) return { success: false, error: 'User not authenticated' };
      try {
        const connDto = await bankApiService.confirmTestModeLink(challengeId);
        const conn = mapBankConnection(connDto, currentUser.id);
        await refreshBankConnections();
        return { success: true, connection: conn };
      } catch (err) {
        const apiErr = err as ApiError;
        return {
          success: false,
          code: apiErr.code,
          error:
            apiErr.detail ||
            apiErr.title ||
            'Chưa phát hiện giao dịch khớp với mã tham chiếu. Vui lòng chuyển khoản demo trên SePay rồi bấm xác nhận lại.',
        };
      }
    },
    [currentUser?.id, refreshBankConnections]
  );

  const unlinkBankConnection = useCallback(
    async (id: string, request?: CreateBankLinkRequest) => {
      try {
        const session = await bankApiService.unlinkBankConnection(id, request);
        await refreshBankConnections();
        return { success: true, session };
      } catch (err) {
        const apiErr = err as ApiError;
        return {
          success: false,
          code: apiErr.code,
          error: apiErr.detail || apiErr.title || 'Lỗi khi hủy liên kết ngân hàng.',
        };
      }
    },
    [refreshBankConnections]
  );

  const reactivateBankConnection = useCallback(
    async (id: string, request?: CreateBankLinkRequest) => {
      try {
        const session = await bankApiService.reactivateBankConnection(id, request);
        await refreshBankConnections();
        return { success: true, session };
      } catch (err) {
        const apiErr = err as ApiError;
        return {
          success: false,
          code: apiErr.code,
          error: apiErr.detail || apiErr.title || 'Lỗi khi kết nối lại ngân hàng.',
        };
      }
    },
    [refreshBankConnections]
  );

  const syncBankConnection = useCallback(
    async (id: string) => {
      try {
        const res = await bankApiService.syncBankConnection(id);
        await refreshBankConnections();
        if (currentUser?.id) {
          const txs = await bankApiService.getBankTransactions({ pageSize: 100 });
          setBankTransactions(txs.items.map((t) => mapBankTransaction(t, currentUser.id)));
        }
        return {
          success: true,
          newTransactionCount: res.importedCount,
          importedCount: res.importedCount,
        };
      } catch (err) {
        const apiErr = err as ApiError;
        return {
          success: false,
          code: apiErr.code,
          error: apiErr.detail || apiErr.title || 'Lỗi khi đồng bộ giao dịch ngân hàng.',
        };
      }
    },
    [currentUser?.id, refreshBankConnections]
  );

  const createWalletTopUp = useCallback(
    async (params: { bankConnectionId: string; walletId: string; amount: number }) => {
      try {
        const res = await bankApiService.createTestModeTopUp(params);
        await refreshWalletTopUps();
        return { success: true, topUp: res };
      } catch (err) {
        const apiErr = err as ApiError;
        let activeTopUp: WalletTopUpDto | undefined;
        if (apiErr.code === 'wallet_top_up_active_exists') {
          try {
            const topUps = await bankApiService.getTestModeWalletTopUps();
            setWalletTopUps(topUps);
            activeTopUp = topUps.find(
              (t) => t.status === 'Pending' || t.status === 'Processing'
            );
          } catch (e) {
            console.error('Failed to fetch active top ups upon conflict', e);
          }
        }
        return {
          success: false,
          code: apiErr.code,
          activeTopUp,
          error: apiErr.detail || apiErr.title || 'Lỗi khi tạo yêu cầu nạp tiền.',
        };
      }
    },
    [refreshWalletTopUps]
  );

  const confirmWalletTopUp = useCallback(
    async (topUpId: string) => {
      try {
        const res = await bankApiService.confirmTestModeTopUp(topUpId);
        if (res.status === 'Completed') {
          // Backend ledger credited wallet balance! Refresh wallets & transactions
          await refreshFinancialCore();
        }
        await refreshWalletTopUps();
        return { success: true, topUp: res };
      } catch (err) {
        const apiErr = err as ApiError;
        return {
          success: false,
          code: apiErr.code,
          error:
            apiErr.detail ||
            apiErr.title ||
            'Giao dịch chuyển tiền chưa được xác nhận trên hệ thống SePay. Vui lòng kiểm tra lại sau ít phút.',
        };
      }
    },
    [refreshFinancialCore, refreshWalletTopUps]
  );

  const updateBankTransactionCategory = useCallback(
    async (bankTxId: string, categoryId: string) => {
      try {
        const updatedDto = await bankApiService.updateBankTransactionCategory(bankTxId, categoryId);
        if (currentUser?.id) {
          const updatedTx = mapBankTransaction(updatedDto, currentUser.id);
          setBankTransactions((prev) => prev.map((t) => (t.id === bankTxId ? updatedTx : t)));
        }
        return { success: true };
      } catch (err) {
        const apiErr = err as ApiError;
        return {
          success: false,
          code: apiErr.code,
          error: apiErr.detail || apiErr.title || 'Lỗi khi cập nhật danh mục giao dịch ngân hàng.',
        };
      }
    },
    [currentUser?.id]
  );

  // Category Management Handlers
  const addCategory = useCallback(
    async (params: Omit<CategoryItem, 'id'>) => {
      if (!params.name.trim()) {
        return { success: false, error: 'Vui lòng nhập tên danh mục.' };
      }
      try {
        const res = await financialApiService.createCategory({
          name: params.name.trim(),
          kind: params.kind === 'income' ? 'Income' : 'Expense',
          icon: params.icon || null,
          color: params.color || null,
        });
        await refreshFinancialCore();
        return { success: true, categoryId: res.id };
      } catch (err) {
        const apiErr = err as ApiError;
        return {
          success: false,
          error: apiErr.detail || apiErr.title || 'Lỗi khi tạo danh mục.',
        };
      }
    },
    [refreshFinancialCore]
  );

  const editCategory = useCallback(
    async (id: string, updates: Partial<CategoryItem>) => {
      const catToEdit = categories.find((c) => c.id === id);
      if (!catToEdit) {
        return { success: false, error: 'Không tìm thấy danh mục.' };
      }
      if (catToEdit.isSystem) {
        return { success: false, error: 'Không thể chỉnh sửa danh mục hệ thống.' };
      }
      if (updates.name !== undefined && !updates.name.trim()) {
        return { success: false, error: 'Tên danh mục không được để trống.' };
      }

      try {
        const newKind = updates.kind || catToEdit.kind;
        await financialApiService.updateCategory(id, {
          name: updates.name ? updates.name.trim() : catToEdit.name,
          kind: newKind === 'income' ? 'Income' : 'Expense',
          icon: updates.icon !== undefined ? updates.icon : catToEdit.icon,
          color: updates.color !== undefined ? updates.color : catToEdit.color,
        });
        await refreshFinancialCore();
        return { success: true };
      } catch (err) {
        const apiErr = err as ApiError;
        return {
          success: false,
          error: apiErr.detail || apiErr.title || 'Lỗi khi cập nhật danh mục.',
        };
      }
    },
    [categories, refreshFinancialCore]
  );

  const deleteCategory = useCallback(
    async (id: string) => {
      const catToDelete = categories.find((c) => c.id === id);
      if (!catToDelete) {
        return { success: false, error: 'Không tìm thấy danh mục.' };
      }
      if (catToDelete.isSystem) {
        return { success: false, error: 'Không thể xóa danh mục hệ thống.' };
      }

      try {
        const res = await financialApiService.deleteCategory(id);
        await refreshFinancialCore();
        if (res.archived) {
          return {
            success: true,
            archived: true,
            message: 'Danh mục đã được lưu trữ để bảo toàn dữ liệu lịch sử và sẽ không hiển thị khi tạo giao dịch mới.',
          };
        }
        return { success: true, archived: false };
      } catch (err) {
        const apiErr = err as ApiError;
        return {
          success: false,
          error: apiErr.detail || apiErr.title || 'Lỗi khi xóa danh mục.',
        };
      }
    },
    [categories, refreshFinancialCore]
  );

  // Recurring Transactions Handlers
  const createRecurring = useCallback(
    async (params: Omit<RecurringTransaction, 'id' | 'createdAt'>) => {
      if (!params.title?.trim()) {
        return { success: false, error: 'Vui lòng nhập tên giao dịch định kỳ.' };
      }
      if (params.amount <= 0) {
        return { success: false, error: 'Số tiền định kỳ phải lớn hơn 0.' };
      }

      const resolvedCatId =
        params.categoryId ||
        resolveCategoryGuid(params.categoryName || '', categories) ||
        categories[0]?.id;
      if (!resolvedCatId) {
        return { success: false, error: 'Vui lòng chọn danh mục hợp lệ.' };
      }

      const targetWallet = wallets.find((w) => w.id === params.walletId) || wallets[0];
      if (!targetWallet) {
        return { success: false, error: 'Vui lòng chọn ví hợp lệ.' };
      }

      const freqFormatted = params.frequency.charAt(0).toUpperCase() + params.frequency.slice(1);
      const kind = params.type === 'income' ? 'Income' : 'Expense';
      const nextRunIso = params.nextRunDate ? new Date(params.nextRunDate).toISOString() : new Date().toISOString();

      try {
        const res = await planningApiService.createRecurring({
          walletId: targetWallet.id,
          categoryId: resolvedCatId,
          kind,
          amount: params.amount,
          description: params.description || params.title.trim(),
          frequency: freqFormatted,
          nextRunAt: nextRunIso,
          isSubscription: params.isSubscription ?? false,
          title: params.title.trim(),
        });

        if (params.status === 'paused') {
          await planningApiService.pauseRecurring(res.id);
        }

        const list = await planningApiService.getRecurringTransactions();
        setRecurringTransactions(list.map((r) => mapRecurring(r, wallets, categories)));
        return { success: true, recurringId: res.id };
      } catch (err) {
        const apiErr = err as ApiError;
        return {
          success: false,
          error: apiErr.detail || apiErr.title || 'Lỗi khi tạo giao dịch định kỳ.',
        };
      }
    },
    [wallets, categories]
  );

  const editRecurring = useCallback(
    async (id: string, updates: Partial<RecurringTransaction>) => {
      const target = recurringTransactions.find((r) => r.id === id);
      if (!target) return { success: false, error: 'Không tìm thấy giao dịch định kỳ.' };

      const targetWalletId = updates.walletId || target.walletId || wallets[0]?.id;
      const resolvedCatId =
        updates.categoryId ||
        (updates.categoryName ? resolveCategoryGuid(updates.categoryName, categories) : target.categoryId) ||
        categories[0]?.id;
      const kind = (updates.type || target.type) === 'income' ? 'Income' : 'Expense';
      const freq = updates.frequency || target.frequency || 'monthly';
      const freqFormatted = freq.charAt(0).toUpperCase() + freq.slice(1);
      const nextDateStr = updates.nextRunDate || target.nextRunDate;
      const nextRunIso = nextDateStr ? new Date(nextDateStr).toISOString() : new Date().toISOString();

      try {
        await planningApiService.updateRecurring(id, {
          walletId: targetWalletId,
          categoryId: resolvedCatId,
          kind,
          amount: updates.amount !== undefined ? updates.amount : target.amount,
          description: updates.description !== undefined ? updates.description : target.description,
          frequency: freqFormatted,
          nextRunAt: nextRunIso,
          isSubscription: updates.isSubscription !== undefined ? updates.isSubscription : target.isSubscription,
          title: updates.title !== undefined ? updates.title : target.title,
        });

        if (updates.status && updates.status !== target.status) {
          if (updates.status === 'paused') {
            await planningApiService.pauseRecurring(id);
          } else if (updates.status === 'active') {
            await planningApiService.resumeRecurring(id);
          }
        }

        const list = await planningApiService.getRecurringTransactions();
        setRecurringTransactions(list.map((r) => mapRecurring(r, wallets, categories)));
        return { success: true };
      } catch (err) {
        const apiErr = err as ApiError;
        return {
          success: false,
          error: apiErr.detail || apiErr.title || 'Lỗi khi cập nhật giao dịch định kỳ.',
        };
      }
    },
    [recurringTransactions, wallets, categories]
  );

  const deleteRecurring = useCallback(
    async (id: string) => {
      try {
        await planningApiService.deleteRecurring(id);
        setRecurringTransactions((prev) => prev.filter((rec) => rec.id !== id));
        return { success: true };
      } catch (err) {
        const apiErr = err as ApiError;
        return {
          success: false,
          error: apiErr.detail || apiErr.title || 'Lỗi khi xóa giao dịch định kỳ.',
        };
      }
    },
    []
  );

  const toggleRecurringStatus = useCallback(
    async (id: string) => {
      const target = recurringTransactions.find((r) => r.id === id);
      if (!target) return { success: false, error: 'Không tìm thấy giao dịch định kỳ.' };

      try {
        if (target.status === 'active') {
          const res = await planningApiService.pauseRecurring(id);
          setRecurringTransactions((prev) =>
            prev.map((r) => (r.id === id ? mapRecurring(res, wallets, categories) : r))
          );
          return { success: true, newStatus: 'paused' };
        } else {
          const res = await planningApiService.resumeRecurring(id);
          setRecurringTransactions((prev) =>
            prev.map((r) => (r.id === id ? mapRecurring(res, wallets, categories) : r))
          );
          return { success: true, newStatus: 'active' };
        }
      } catch (err) {
        const apiErr = err as ApiError;
        return {
          success: false,
          error: apiErr.detail || apiErr.title || 'Lỗi khi thay đổi trạng thái giao dịch định kỳ.',
        };
      }
    },
    [recurringTransactions, wallets, categories]
  );

  const recordRecurringOccurrence = useCallback(
    async (
      recurringId: string,
      occurrenceId: string,
      params: {
        securityPin: string;
        evidenceIds?: string[];
        evidence?: AttachmentMetadata[];
        idempotencyKey?: string;
      }
    ) => {
      try {
        let finalEvidenceIds = params.evidenceIds ? [...params.evidenceIds] : [];
        if (finalEvidenceIds.length === 0 && params.evidence && params.evidence.length > 0) {
          const uploadedIds = await uploadEvidenceItems(params.evidence);
          finalEvidenceIds = [...uploadedIds];
        }

        await planningApiService.recordRecurringOccurrence(recurringId, occurrenceId, {
          securityPin: params.securityPin,
          evidenceIds: finalEvidenceIds,
          idempotencyKey: params.idempotencyKey || crypto.randomUUID(),
        });
        await refreshFinancialCore();
        return { success: true };
      } catch (err) {
        const apiErr = err as ApiError;
        return {
          success: false,
          error: apiErr.detail || apiErr.title || 'Lỗi khi ghi nhận thanh toán định kỳ.',
        };
      }
    },
    [refreshFinancialCore]
  );

  // Active budget warnings count for CURRENT calendar month (>= 90% spent or warning/exceeded)
  const activeBudgetAlertsCount = useMemo(() => {
    if (!currentUser?.id) return 0;
    return budgets.filter((b) => b.alertLevel === 'Warning' || b.alertLevel === 'Exceeded').length;
  }, [budgets, currentUser?.id]);

  // Due recurring occurrences count - backend authoritative
  const dueRecurringCount = useMemo(() => {
    if (!currentUser?.id) return 0;
    let count = 0;
    for (const r of recurringTransactions) {
      if (r.status !== 'active') continue;
      if (r.occurrences && r.occurrences.length > 0) {
        count += r.occurrences.filter((o) => o.status === 'Due' || o.status === 'Overdue').length;
      } else {
        const todayStr = getTodayDateString();
        if (isRecurringDue(r, currentUser.id, todayStr)) {
          count++;
        }
      }
    }
    return count;
  }, [recurringTransactions, currentUser?.id]);

  const advanceRecurringOccurrence = useCallback(
    (_recurringId: string, _dueDate: string, _frequency: RecurringFrequency) => {
      // Deprecated: Occurrence advancement is handled backend-authoritatively via recordRecurringOccurrence.
    },
    []
  );

  // Transaction Rules Handlers
  const createRule = useCallback(
    async (params: Omit<TransactionRule, 'id' | 'createdAt'>) => {
      if (!params.descriptionPattern.trim()) {
        return { success: false, error: 'Vui lòng nhập mẫu mô tả (pattern).' };
      }
      if (!params.targetCategoryId) {
        return { success: false, error: 'Vui lòng chọn danh mục đích.' };
      }
      try {
        const res = await planningApiService.createTransactionRule({
          descriptionPattern: params.descriptionPattern.trim(),
          targetCategoryId: params.targetCategoryId,
          priority: Number(params.priority) || 10,
        });
        const newMapped = mapTransactionRule(res);
        setRules((prev) => [...prev, newMapped].sort((a, b) => b.priority - a.priority));
        return { success: true, ruleId: res.id };
      } catch (err) {
        const apiErr = err as ApiError;
        return {
          success: false,
          error: apiErr.detail || apiErr.title || 'Lỗi khi tạo quy tắc.',
        };
      }
    },
    []
  );

  const editRule = useCallback(
    async (id: string, updates: Partial<TransactionRule>) => {
      const target = rules.find((r) => r.id === id);
      if (!target) return { success: false, error: 'Không tìm thấy quy tắc.' };
      try {
        const res = await planningApiService.updateTransactionRule(id, {
          descriptionPattern: updates.descriptionPattern !== undefined ? updates.descriptionPattern.trim() : target.descriptionPattern,
          targetCategoryId: updates.targetCategoryId || target.targetCategoryId,
          priority: updates.priority !== undefined ? Number(updates.priority) || 10 : target.priority,
          isActive: updates.isActive !== undefined ? updates.isActive : target.isActive,
        });
        const updated = mapTransactionRule(res);
        setRules((prev) => prev.map((r) => (r.id === id ? updated : r)).sort((a, b) => b.priority - a.priority));
        return { success: true };
      } catch (err) {
        const apiErr = err as ApiError;
        return {
          success: false,
          error: apiErr.detail || apiErr.title || 'Lỗi khi cập nhật quy tắc.',
        };
      }
    },
    [rules]
  );

  const deleteRule = useCallback(
    async (id: string) => {
      try {
        await planningApiService.deleteTransactionRule(id);
        setRules((prev) => prev.filter((rule) => rule.id !== id));
        return { success: true };
      } catch (err) {
        const apiErr = err as ApiError;
        return {
          success: false,
          error: apiErr.detail || apiErr.title || 'Lỗi khi xóa quy tắc.',
        };
      }
    },
    []
  );

  const toggleRuleStatus = useCallback(
    async (id: string) => {
      const target = rules.find((r) => r.id === id);
      if (!target) return { success: false, error: 'Không tìm thấy quy tắc.' };
      try {
        const res = await planningApiService.updateTransactionRule(id, {
          descriptionPattern: target.descriptionPattern,
          targetCategoryId: target.targetCategoryId,
          priority: target.priority,
          isActive: !target.isActive,
        });
        setRules((prev) => prev.map((r) => (r.id === id ? mapTransactionRule(res) : r)));
        return { success: true, newStatus: res.isActive };
      } catch (err) {
        const apiErr = err as ApiError;
        return {
          success: false,
          error: apiErr.detail || apiErr.title || 'Lỗi khi thay đổi trạng thái quy tắc.',
        };
      }
    },
    [rules]
  );

  // Phase 3A: Transaction Classification Foundation (Unintegrated slice in Core B)
  const classifyTransaction = useCallback(
    (_params: { transactionId: string; categoryKey: string }) => {
      return {
        success: false,
        error: 'Chức năng phân loại giao dịch ngân hàng đang được phát triển.',
      };
    },
    []
  );

  const importBankTransactionToHistory = useCallback(
    (_params: { bankTransactionId: string; walletId?: string }) => {
      return {
        success: false,
        error: 'Chức năng nhập giao dịch ngân hàng vào sổ cái đang chờ đồng bộ từ máy chủ.',
      };
    },
    []
  );

  return (
    <FinancialContext.Provider
      value={{
        isLoading,
        wallets,
        transactions,
        budgets,
        goals,
        dashboardSummary,
        spendingCategories,
        attentionItems,
        categories,
        addCategory,
        editCategory,
        deleteCategory,
        recurringTransactions,
        createRecurring,
        editRecurring,
        deleteRecurring,
        toggleRecurringStatus,
        activeBudgetAlertsCount,
        dueRecurringCount,
        advanceRecurringOccurrence,
        rules,
        createRule,
        editRule,
        deleteRule,
        toggleRuleStatus,
        getWallet,
        addIncome,
        addExpense,
        deleteTransaction,
        editTransaction,
        correctTransaction,
        loadBudgetsForMonth,
        createBudget,
        updateBudgetLimit,
        editBudget,
        deleteBudget,
        createGoal,
        editGoal,
        deleteGoal,
        contributeToGoal,
        withdrawFromGoal,
        recordRecurringOccurrence,
        transferBetweenWallets,
        createWallet,
        updateWallet,
        setDefaultWallet,
        closeWallet,
        getTransactionDetail,
        downloadEvidenceBlob,
        refreshFinancialCore,
        bankConnections,
        bankTransactions,
        testModeLinkedAccounts,
        walletTopUps,
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
        refreshAdminBanking,
        adminBankConnections,
        adminBankTransactions,
        adminBankIntegrationEvents,
        bankReconciliationSummary,
        sePayIntegrationStatus,
        classifyTransaction,
        importBankTransactionToHistory,
        importBankTransactionToLedger: importBankTransactionToHistory,
      }}
    >
      {children}
    </FinancialContext.Provider>
  );
};

export const useFinancial = (): FinancialContextType => {
  const context = useContext(FinancialContext);
  if (!context) {
    throw new Error('useFinancial must be used within a FinancialProvider');
  }
  return context;
};
