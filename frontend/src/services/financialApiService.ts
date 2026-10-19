import { apiClient } from '../lib';

// ---------------------------------------------------------------------------
// 1. Wallets DTOs & Contracts
// ---------------------------------------------------------------------------

export type WalletType = 'spending' | 'savings' | 'emergency';

export interface WalletDto {
  id: string;
  name: string;
  type: WalletType;
  currency: string;
  status: string;
  availableBalance: number;
  isDefault: boolean;
  createdAt: string;
}

export interface CreateWalletRequest {
  name: string;
  type: WalletType;
  isDefault: boolean;
}

export interface UpdateWalletRequest {
  name: string;
  type: WalletType;
  isDefault: boolean;
}

// ---------------------------------------------------------------------------
// 2. Categories DTOs & Contracts
// ---------------------------------------------------------------------------

export type BackendCategoryKind = 'Income' | 'Expense';

export interface CategoryDto {
  id: string;
  key: string;
  name: string;
  kind: BackendCategoryKind;
  icon?: string | null;
  color?: string | null;
  isSystem: boolean;
  isActive: boolean;
}

export interface CreateCategoryRequest {
  name: string;
  kind: BackendCategoryKind;
  icon?: string | null;
  color?: string | null;
}

export interface UpdateCategoryRequest {
  name: string;
  kind: BackendCategoryKind;
  icon?: string | null;
  color?: string | null;
}

export interface DeleteCategoryResponse {
  id: string;
  archived: boolean;
}

// ---------------------------------------------------------------------------
// 3. Evidence DTOs & Contracts
// ---------------------------------------------------------------------------

export interface EvidenceDto {
  id: string;
  originalFileName: string;
  contentType: string;
  length: number;
  contentHash?: string;
  createdAt: string;
}

// ---------------------------------------------------------------------------
// 4. Transactions DTOs & Contracts
// ---------------------------------------------------------------------------

export interface TransactionQueryParams {
  page?: number;
  pageSize?: number;
  walletId?: string;
  categoryId?: string;
  type?: string;
  from?: string;
  to?: string;
  minimumAmount?: number;
  maximumAmount?: number;
  search?: string;
}

export interface PagedTransactionsDto {
  items: TransactionDto[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface TransactionCorrectionDto {
  id: string;
  correctionNumber: number;
  reason: string;
  actorUserId: string;
  actorRole: string;
  actorName: string;
  createdAt: string;
  evidence?: EvidenceDto[];
  before?: Record<string, unknown>;
  after?: Record<string, unknown>;
}

export interface CorrectionGrantDto {
  id: string;
  grantNumber?: number;
  additionalCount: number;
  reason: string;
  grantedByUserId?: string;
  grantedByName?: string;
  grantedAt: string;
}

export interface TransactionDto {
  id: string;
  walletId: string;
  walletName: string;
  categoryId?: string;
  categoryName?: string;
  type: string;
  status: string;
  amount: number;
  signedAmount: number;
  currency: string;
  description: string;
  counterpartyName?: string;
  occurredAt: string;
  ledgerTransactionId?: string;
  relatedBusinessId?: string;
  correctionAllowance: number;
  correctionCount: number;
  canCorrect: boolean;
  corrections?: TransactionCorrectionDto[];
  evidence?: EvidenceDto[];
  correctionGrants?: CorrectionGrantDto[];
}

export interface CreateManualTransactionRequest {
  walletId: string;
  categoryId: string; // backend category GUID
  amount: number;
  description: string;
  occurredAt?: string;
  idempotencyKey?: string;
  securityPin?: string;
  evidenceIds?: string[];
  counterparty?: string;
}

export interface CorrectTransactionRequest {
  categoryId?: string; // backend category GUID
  description: string;
  securityPin?: string;
  evidenceIds?: string[];
  reason: string; // required
  occurredAt?: string;
  walletId?: string;
  amount?: number;
  counterparty?: string;
}

// ---------------------------------------------------------------------------
// 5. Internal Transfer DTOs & Contracts
// ---------------------------------------------------------------------------

export interface CreateInternalTransferRequest {
  sourceWalletId: string;
  destinationWalletId: string;
  amount: number;
  description: string;
  idempotencyKey: string;
  securityPin?: string;
}

export interface TransferDto {
  id: string;
  sourceWalletId: string;
  destinationWalletId: string;
  amount: number;
  fee?: number;
  description: string;
  status: string;
  createdAt: string;
}

// ---------------------------------------------------------------------------
// 6. Dashboard DTOs & Contracts
// ---------------------------------------------------------------------------

export interface CategoryDistributionDto {
  categoryId?: string;
  categoryName: string;
  amount: number;
  percentage?: number;
  transactionCount?: number;
}

export interface SixMonthCashFlowDto {
  year?: number;
  month?: string | number;
  income: number;
  expense: number;
}

export interface AttentionItemDto {
  id?: string;
  relatedId?: string;
  type: string;
  title: string;
  message?: string;
  severity: string;
  actionUrl?: string;
  dueAt?: string;
}

export interface DashboardRecentTransactionDto {
  id: string;
  type: string;
  amount: number;
  description: string;
  occurredAt: string;
}

export interface DashboardDto {
  totalBalance: number;
  totalWalletsBalance: number;
  totalTrackedAssets: number;
  totalGoalsReserved: number;
  incomeThisMonth: number;
  expenseThisMonth: number;
  budgetRemaining: number;
  cashFlowDifference: number;
  groupFundBalance: number;
  userPendingFundObligations: number;
  othersOweUser: number;
  userOwesOthers: number;
  recentTransactions?: (TransactionDto | DashboardRecentTransactionDto)[];
  categoryDistribution?: CategoryDistributionDto[];
  sixMonthCashFlow?: SixMonthCashFlowDto[];
  attentionItems?: AttentionItemDto[];
}

// ---------------------------------------------------------------------------
// Query string helper functions
// ---------------------------------------------------------------------------

function buildCategoryQueryString(params?: { kind?: 'Income' | 'Expense'; includeArchived?: boolean }): string {
  if (!params) return '';
  const searchParams = new URLSearchParams();
  if (params.kind) searchParams.set('kind', params.kind);
  if (params.includeArchived !== undefined) searchParams.set('includeArchived', String(params.includeArchived));
  const qs = searchParams.toString();
  return qs ? `?${qs}` : '';
}

function buildTransactionQueryString(params?: TransactionQueryParams): string {
  if (!params) return '';
  const searchParams = new URLSearchParams();
  if (params.page !== undefined) searchParams.set('page', String(params.page));
  if (params.pageSize !== undefined) searchParams.set('pageSize', String(params.pageSize));
  if (params.walletId) searchParams.set('walletId', params.walletId);
  if (params.categoryId) searchParams.set('categoryId', params.categoryId);
  if (params.type) searchParams.set('type', params.type);
  if (params.from) searchParams.set('from', params.from);
  if (params.to) searchParams.set('to', params.to);
  if (params.minimumAmount !== undefined) searchParams.set('minimumAmount', String(params.minimumAmount));
  if (params.maximumAmount !== undefined) searchParams.set('maximumAmount', String(params.maximumAmount));
  if (params.search) searchParams.set('search', params.search);
  const qs = searchParams.toString();
  return qs ? `?${qs}` : '';
}

// ---------------------------------------------------------------------------
// Service implementation
// ---------------------------------------------------------------------------

export const financialApiService = {
  // Wallets
  async getWallets(): Promise<WalletDto[]> {
    return apiClient.get<WalletDto[]>('/wallets');
  },

  async getWallet(walletId: string): Promise<WalletDto> {
    return apiClient.get<WalletDto>(`/wallets/${walletId}`);
  },

  async createWallet(data: CreateWalletRequest): Promise<WalletDto> {
    return apiClient.post<WalletDto>('/wallets', data);
  },

  async updateWallet(walletId: string, data: UpdateWalletRequest): Promise<WalletDto> {
    return apiClient.put<WalletDto>(`/wallets/${walletId}`, data);
  },

  async deleteWallet(walletId: string): Promise<void> {
    return apiClient.delete<void>(`/wallets/${walletId}`);
  },

  // Categories
  async getCategories(params?: { kind?: 'Income' | 'Expense'; includeArchived?: boolean }): Promise<CategoryDto[]> {
    const qs = buildCategoryQueryString(params);
    return apiClient.get<CategoryDto[]>(`/categories${qs}`);
  },

  async getCategory(categoryId: string): Promise<CategoryDto> {
    return apiClient.get<CategoryDto>(`/categories/${categoryId}`);
  },

  async createCategory(data: CreateCategoryRequest): Promise<CategoryDto> {
    return apiClient.post<CategoryDto>('/categories', data);
  },

  async updateCategory(categoryId: string, data: UpdateCategoryRequest): Promise<CategoryDto> {
    return apiClient.put<CategoryDto>(`/categories/${categoryId}`, data);
  },

  async deleteCategory(categoryId: string): Promise<DeleteCategoryResponse> {
    return apiClient.delete<DeleteCategoryResponse>(`/categories/${categoryId}`);
  },

  // Evidence
  async uploadEvidence(file: File): Promise<EvidenceDto> {
    const form = new FormData();
    form.append('file', file);
    return apiClient.upload<EvidenceDto>('/evidence', form);
  },

  async getEvidenceBlob(evidenceId: string): Promise<Blob> {
    return apiClient.getBlob(`/evidence/${evidenceId}`);
  },

  // Transactions
  async getTransactions(params?: TransactionQueryParams): Promise<PagedTransactionsDto> {
    const qs = buildTransactionQueryString(params);
    return apiClient.get<PagedTransactionsDto>(`/transactions${qs}`);
  },

  async getTransaction(transactionId: string): Promise<TransactionDto> {
    return apiClient.get<TransactionDto>(`/transactions/${transactionId}`);
  },

  async createIncome(data: CreateManualTransactionRequest): Promise<TransactionDto> {
    return apiClient.post<TransactionDto>('/transactions/income', data);
  },

  async createExpense(data: CreateManualTransactionRequest): Promise<TransactionDto> {
    return apiClient.post<TransactionDto>('/transactions/expense', data);
  },

  async correctTransaction(transactionId: string, data: CorrectTransactionRequest): Promise<TransactionDto> {
    return apiClient.patch<TransactionDto>(`/transactions/${transactionId}`, data);
  },

  // Internal transfer
  async transferInternal(data: CreateInternalTransferRequest): Promise<TransferDto> {
    return apiClient.post<TransferDto>('/transfers/internal', data);
  },

  // Dashboard
  async getDashboard(): Promise<DashboardDto> {
    return apiClient.get<DashboardDto>('/dashboard');
  },
};
