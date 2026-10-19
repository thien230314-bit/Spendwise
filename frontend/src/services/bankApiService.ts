import { apiClient } from '../lib';
import type { BankConnection, BankTransaction, AccountEnvironment } from '../types';
import type {
  AdminBankIntegrationEvent,
  BankReconciliationSummary,
  SePayIntegrationStatus,
} from '../types/admin';

// ---------------------------------------------------------------------------
// 1. User SePay / Bank Connection DTOs
// ---------------------------------------------------------------------------

export interface BankConnectionDto {
  id: string;
  provider: string;
  accountEnvironment: AccountEnvironment | string;
  bankBrandName: string;
  accountType?: string;
  accountHolderName?: string;
  maskedAccountNumber: string;
  status: string;
  linkedAt?: string;
  lastSyncedAt?: string;
}

export interface CreateBankLinkRequest {
  completionRedirectUri?: string;
  language?: string;
}

export interface BankLinkSessionDto {
  sessionId: string;
  hostedLinkUrl: string;
  expiresAt: string;
  purpose: string;
}

export interface TestModeBankLinkChallengeDto {
  challengeId: string;
  reference: string;
  expiresAt: string;
  instruction: string;
}

export interface TestModeLinkedAccountDto {
  bankConnectionId: string;
  bankName: string;
  maskedAccountNumber: string;
  accountType?: string;
  status: string;
  accumulatedBalance: number;
}

export interface BankSyncDto {
  importedCount: number;
  duplicateCount: number;
  lastSyncedAt: string;
}

// ---------------------------------------------------------------------------
// 2. Wallet Top-Up DTOs (SWTP)
// ---------------------------------------------------------------------------

export interface CreateWalletTopUpRequest {
  bankConnectionId: string;
  walletId: string;
  amount: number;
}

export interface WalletTopUpDto {
  id: string;
  bankConnectionId: string;
  walletId: string;
  amount: number;
  currency: string;
  maskedAccountNumber: string;
  reference: string;
  status: 'Pending' | 'Processing' | 'Completed' | 'Expired' | 'Failed' | string;
  expiresAt: string;
  completedAt?: string;
  financialTransactionId?: string;
  instruction: string;
}

// ---------------------------------------------------------------------------
// 3. Bank Transactions DTOs
// ---------------------------------------------------------------------------

export interface BankTransactionFilter {
  page?: number;
  pageSize?: number;
  bankConnectionId?: string;
  direction?: string;
  from?: string;
  to?: string;
  search?: string;
}

export interface BankTransactionDto {
  id: string;
  bankConnectionId: string;
  bankBrandName: string;
  maskedAccountNumber: string;
  externalTransactionId: string;
  direction: string;
  amount: number;
  currency: string;
  transactionDate: string;
  content: string;
  referenceCode?: string;
  paymentCode?: string;
  gateway?: string;
  source: string;
  importedAt: string;
  categoryId?: string;
  categoryName?: string;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

// ---------------------------------------------------------------------------
// 4. Admin Banking DTOs
// ---------------------------------------------------------------------------

export interface AdminBankConnectionDto {
  id: string;
  userId: string;
  provider: string;
  accountEnvironment: AccountEnvironment | string;
  externalAccountId: string;
  bankBrandName: string;
  maskedAccountNumber: string;
  status: string;
  linkedAt?: string;
  lastSyncedAt?: string;
  createdAt: string;
}

export interface AdminBankTransactionFilter {
  page?: number;
  pageSize?: number;
  userId?: string;
  bankConnectionId?: string;
  provider?: string;
  direction?: string;
  from?: string;
  to?: string;
  search?: string;
}

export interface AdminBankTransactionDto {
  id: string;
  userId: string;
  userFullName: string;
  userSpendId: string;
  bankConnectionId: string;
  provider: string;
  bankBrandName: string;
  maskedAccountNumber: string;
  externalTransactionId: string;
  direction: string;
  amount: number;
  currency: string;
  transactionDate: string;
  content: string;
  referenceCode?: string;
  paymentCode?: string;
  gateway?: string;
  source: string;
  importedAt: string;
}

export interface AdminBankIntegrationEventDto {
  id: string;
  provider: string;
  externalEventId?: string;
  externalTransactionId?: string;
  eventType: string;
  externalBankAccountId?: string;
  status: string;
  failureCode?: string;
  duplicateDeliveryCount: number;
  lastDuplicateReceivedAt?: string;
  receivedAt: string;
  processedAt?: string;
}

export interface BankReconciliationDto {
  activeConnections: number;
  inactiveConnections: number;
  unlinkedConnections: number;
  processedEvents: number;
  duplicateDeliveries: number;
  unmatchedEvents: number;
  failedEvents: number;
  importedTransactions: number;
}

export interface SePayBankHubStatusDto {
  enabled: boolean;
  environment: string;
  configured: boolean;
  companyConfigured: boolean;
  lifecycleWebhookConfigured: boolean;
  ipnConfigured: boolean;
  lastSuccessfulProviderCallAt?: string;
  lastWebhookReceivedAt?: string;
}

export interface SePayTestModeAccountDto {
  providerAccountId: string;
  bankName: string;
  accountNumber: string;
  accountType: string;
  accumulatedBalance: number;
  status: string;
  maskedAccountNumber: string;
}

// ---------------------------------------------------------------------------
// Query String Helpers
// ---------------------------------------------------------------------------

function buildFilterQueryString(params?: Record<string, unknown>): string {
  if (!params) return '';
  const searchParams = new URLSearchParams();
  for (const [key, val] of Object.entries(params)) {
    if (val !== undefined && val !== null && val !== '') {
      searchParams.set(key, String(val));
    }
  }
  const qs = searchParams.toString();
  return qs ? `?${qs}` : '';
}

// ---------------------------------------------------------------------------
// Bank API Service
// ---------------------------------------------------------------------------

export const bankApiService = {
  // ── User Bank Connections ──
  async getBankConnections(): Promise<BankConnectionDto[]> {
    return apiClient.get<BankConnectionDto[]>('/bank-connections');
  },

  async getBankConnection(id: string): Promise<BankConnectionDto> {
    return apiClient.get<BankConnectionDto>(`/bank-connections/${id}`);
  },

  async unlinkBankConnection(id: string, request?: CreateBankLinkRequest): Promise<BankLinkSessionDto> {
    return apiClient.post<BankLinkSessionDto>(`/bank-connections/${id}/unlink`, request ?? {});
  },

  async reactivateBankConnection(id: string, request?: CreateBankLinkRequest): Promise<BankLinkSessionDto> {
    return apiClient.post<BankLinkSessionDto>(`/bank-connections/${id}/reactivate`, request ?? {});
  },

  async syncBankConnection(id: string): Promise<BankSyncDto> {
    return apiClient.post<BankSyncDto>(`/bank-connections/${id}/sync`, {});
  },

  // ── SePay Test Mode Self-Link (SWLINK) ──
  async startTestModeLink(): Promise<TestModeBankLinkChallengeDto> {
    return apiClient.post<TestModeBankLinkChallengeDto>('/bank-connections/test-mode/link-challenges', {});
  },

  async confirmTestModeLink(challengeId: string): Promise<BankConnectionDto> {
    return apiClient.post<BankConnectionDto>(`/bank-connections/test-mode/link-challenges/${challengeId}/confirm`, {});
  },

  async getTestModeLinkedAccounts(): Promise<TestModeLinkedAccountDto[]> {
    return apiClient.get<TestModeLinkedAccountDto[]>('/bank-connections/test-mode/linked-accounts');
  },

  // ── Wallet Top-Up (SWTP) ──
  async createTestModeTopUp(request: CreateWalletTopUpRequest): Promise<WalletTopUpDto> {
    return apiClient.post<WalletTopUpDto>('/bank-connections/test-mode/wallet-top-ups', request);
  },

  async confirmTestModeTopUp(topUpId: string): Promise<WalletTopUpDto> {
    return apiClient.post<WalletTopUpDto>(`/bank-connections/test-mode/wallet-top-ups/${topUpId}/confirm`, {});
  },

  async getTestModeWalletTopUps(): Promise<WalletTopUpDto[]> {
    return apiClient.get<WalletTopUpDto[]>('/bank-connections/test-mode/wallet-top-ups');
  },

  // ── Bank Transactions ──
  async getBankTransactions(filter?: BankTransactionFilter): Promise<PagedResult<BankTransactionDto>> {
    const qs = buildFilterQueryString(filter as Record<string, unknown>);
    return apiClient.get<PagedResult<BankTransactionDto>>(`/bank-transactions${qs}`);
  },

  async getBankTransaction(id: string): Promise<BankTransactionDto> {
    return apiClient.get<BankTransactionDto>(`/bank-transactions/${id}`);
  },

  /**
   * PATCH /api/bank-transactions/{id}/category
   */
  async updateBankTransactionCategory(id: string, categoryId: string): Promise<BankTransactionDto> {
    return apiClient.patch<BankTransactionDto>(`/bank-transactions/${id}/category`, { categoryId });
  },

  // ── Admin Banking APIs ──
  async getAdminBankConnections(params?: {
    userId?: string;
    status?: string;
    provider?: string;
    bank?: string;
  }): Promise<AdminBankConnectionDto[]> {
    const qs = buildFilterQueryString(params as Record<string, unknown>);
    return apiClient.get<AdminBankConnectionDto[]>(`/admin/bank-connections${qs}`);
  },

  async getAdminBankConnection(id: string): Promise<AdminBankConnectionDto> {
    return apiClient.get<AdminBankConnectionDto>(`/admin/bank-connections/${id}`);
  },

  async getAdminBankTransactions(filter?: AdminBankTransactionFilter): Promise<PagedResult<AdminBankTransactionDto>> {
    const qs = buildFilterQueryString(filter as Record<string, unknown>);
    return apiClient.get<PagedResult<AdminBankTransactionDto>>(`/admin/bank-transactions${qs}`);
  },

  async getAdminBankIntegrationEvents(params?: {
    status?: string;
    eventType?: string;
    externalTransactionId?: string;
    externalBankAccountId?: string;
    from?: string;
    to?: string;
  }): Promise<AdminBankIntegrationEventDto[]> {
    const qs = buildFilterQueryString(params as Record<string, unknown>);
    return apiClient.get<AdminBankIntegrationEventDto[]>(`/admin/bank-integration/events${qs}`);
  },

  async getAdminBankReconciliation(): Promise<BankReconciliationDto> {
    return apiClient.get<BankReconciliationDto>('/admin/bank-integration/reconciliation');
  },

  async getAdminSePayStatus(): Promise<SePayBankHubStatusDto> {
    return apiClient.get<SePayBankHubStatusDto>('/admin/integrations/sepay/status');
  },

  async getAdminSePayTestModeAccounts(): Promise<SePayTestModeAccountDto[]> {
    return apiClient.get<SePayTestModeAccountDto[]>('/admin/integrations/sepay/test-mode/bank-accounts');
  },
};
