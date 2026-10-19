import { apiClient } from '../lib/apiClient';

export interface CreateSupportCaseRequest {
  category: string;
  priority: string;
  linkedTransactionId?: string | null;
  subject: string;
  description: string;
}

export interface CreateDisputeRequest {
  linkedTransactionId: string;
  reason: string;
  description: string;
}

export interface CaseDto {
  id: string;
  type: string;
  status: string;
  summary: string;
  userId: string;
  linkedTransactionId?: string | null;
  createdAt: string;
}

export interface CaseAttachmentUpload {
  fileName: string;
  contentType: string;
  content: string; // Base64 encoded string
}

export interface CaseAttachmentDto {
  id: string;
  fileName: string;
  contentType: string;
  length: number;
  hash: string;
  createdAt: string;
}

export interface CaseHistoryDto {
  id: string;
  actorId: string;
  actorName: string;
  actorRole: string;
  action: string;
  message: string;
  oldStatus?: string | null;
  newStatus?: string | null;
  createdAt: string;
  attachments: CaseAttachmentDto[];
}

export interface SupportCaseDetailDto {
  id: string;
  ticketNumber: string;
  userId: string;
  subject: string;
  description: string;
  category: string;
  priority: string;
  status: string;
  linkedTransactionId?: string | null;
  assignedAdminId?: string | null;
  createdAt: string;
  updatedAt?: string | null;
  latestReplyAt?: string | null;
  correctionGranted: boolean;
  history: CaseHistoryDto[];
}

export interface DisputeDetailDto {
  id: string;
  userId: string;
  linkedTransactionId: string;
  reference: string;
  title: string;
  amount: number;
  counterparty?: string | null;
  occurredAt: string;
  reason: string;
  description: string;
  status: string;
  resolution?: string | null;
  createdAt: string;
  updatedAt?: string | null;
  history: CaseHistoryDto[];
}

export interface CaseMessageRequest {
  message: string;
  attachments?: CaseAttachmentUpload[] | null;
}

export interface CaseStatusRequest {
  status: string;
  message: string;
  attachments?: CaseAttachmentUpload[] | null;
}

export interface CorrectionGrantRequest {
  adminPin: string;
  reason: string;
}

export const casesApiService = {
  // --- User Support Cases ---
  async createSupportCase(data: CreateSupportCaseRequest): Promise<CaseDto> {
    return apiClient.post<CaseDto>('/cases/support', data);
  },

  async getSupportCases(): Promise<SupportCaseDetailDto[]> {
    return apiClient.get<SupportCaseDetailDto[]>('/cases/support');
  },

  async getSupportCase(id: string): Promise<SupportCaseDetailDto> {
    return apiClient.get<SupportCaseDetailDto>(`/cases/support/${id}`);
  },

  async replySupportCase(id: string, data: CaseMessageRequest): Promise<SupportCaseDetailDto> {
    return apiClient.post<SupportCaseDetailDto>(`/cases/support/${id}/messages`, data);
  },

  // --- User Disputes ---
  async createDispute(data: CreateDisputeRequest): Promise<CaseDto> {
    return apiClient.post<CaseDto>('/cases/disputes', data);
  },

  async getDisputes(): Promise<DisputeDetailDto[]> {
    return apiClient.get<DisputeDetailDto[]>('/cases/disputes');
  },

  async getDispute(id: string): Promise<DisputeDetailDto> {
    return apiClient.get<DisputeDetailDto>(`/cases/disputes/${id}`);
  },

  async replyDispute(id: string, data: CaseMessageRequest): Promise<DisputeDetailDto> {
    return apiClient.post<DisputeDetailDto>(`/cases/disputes/${id}/messages`, data);
  },

  // --- User Attachment Blob Download ---
  async getAttachmentBlob(id: string): Promise<Blob> {
    return apiClient.getBlob(`/cases/attachments/${id}`);
  },

  // --- Admin Cases V2 ---
  async getAdminSupportCases(): Promise<SupportCaseDetailDto[]> {
    return apiClient.get<SupportCaseDetailDto[]>('/admin/cases/support');
  },

  async getAdminSupportCase(id: string): Promise<SupportCaseDetailDto> {
    return apiClient.get<SupportCaseDetailDto>(`/admin/cases/support/${id}`);
  },

  async adminReplySupportCase(id: string, data: CaseMessageRequest): Promise<SupportCaseDetailDto> {
    return apiClient.post<SupportCaseDetailDto>(`/admin/cases/support/${id}/messages`, data);
  },

  async updateAdminSupportStatus(id: string, data: CaseStatusRequest): Promise<SupportCaseDetailDto> {
    return apiClient.post<SupportCaseDetailDto>(`/admin/cases/support/${id}/status`, data);
  },

  async grantAdminCorrection(id: string, data: CorrectionGrantRequest): Promise<SupportCaseDetailDto> {
    return apiClient.post<SupportCaseDetailDto>(`/admin/cases/support/${id}/correction-grant`, data);
  },

  async getAdminDisputes(): Promise<DisputeDetailDto[]> {
    return apiClient.get<DisputeDetailDto[]>('/admin/cases/disputes');
  },

  async getAdminDispute(id: string): Promise<DisputeDetailDto> {
    return apiClient.get<DisputeDetailDto>(`/admin/cases/disputes/${id}`);
  },

  async adminReplyDispute(id: string, data: CaseMessageRequest): Promise<DisputeDetailDto> {
    return apiClient.post<DisputeDetailDto>(`/admin/cases/disputes/${id}/messages`, data);
  },

  async updateAdminDisputeStatus(id: string, data: CaseStatusRequest): Promise<DisputeDetailDto> {
    return apiClient.post<DisputeDetailDto>(`/admin/cases/disputes/${id}/status`, data);
  },

  async getAdminAttachmentBlob(id: string): Promise<Blob> {
    return apiClient.getBlob(`/admin/cases/attachments/${id}`);
  },

  // --- Admin Audit Logs ---
  async getAdminAuditLogs(): Promise<AuditLogDto[]> {
    return apiClient.get<AuditLogDto[]>('/admin/audit-logs');
  },

  // --- Admin Dashboard & Reports ---
  async getAdminDashboard(from?: string, to?: string): Promise<AdminDashboardDto> {
    const params = new URLSearchParams();
    if (from) params.set('from', from);
    if (to) params.set('to', to);
    const queryString = params.toString() ? `?${params.toString()}` : '';
    return apiClient.get<AdminDashboardDto>(`/admin/dashboard${queryString}`);
  },

  // --- Admin Sensitive Financial Transactions & Ledger ---
  async searchAdminTransactions(reason: string): Promise<AdminTransactionDto[]> {
    const rawTransactions = await apiClient.post<RawAdminTransactionDto[]>('/admin/transactions/search', { reason });
    return (rawTransactions || []).map(normalizeAdminTransaction);
  },

  async searchAdminLedger(reason: string): Promise<LedgerTransactionDto[]> {
    return apiClient.post<LedgerTransactionDto[]>('/admin/ledger/search', { reason });
  },

  // --- Admin Users Management ---
  async getAdminUsers(search?: string): Promise<AdminUserDto[]> {
    const query = search ? `?search=${encodeURIComponent(search)}` : '';
    return apiClient.get<AdminUserDto[]>(`/admin/users${query}`);
  },

  async updateAdminUserStatus(id: string, data: { status: string; reason: string; adminPin: string }): Promise<AdminUserDto> {
    return apiClient.put<AdminUserDto>(`/admin/users/${id}/status`, data);
  },

  async adminResetUserPassword(id: string, data: { reason: string; adminPin: string }): Promise<AdminTemporaryPasswordResponse> {
    return apiClient.post<AdminTemporaryPasswordResponse>(`/admin/users/${id}/reset-password`, data);
  },

  async adminResetUserPin(id: string, data: { reason: string; adminPin: string }): Promise<AdminSecurityPinResetResponse> {
    return apiClient.post<AdminSecurityPinResetResponse>(`/admin/users/${id}/reset-pin`, data);
  },

  async adminRevokeUserSessions(id: string, reason: string): Promise<void> {
    return apiClient.post<void>(`/admin/users/${id}/revoke-sessions`, { reason });
  },

  // --- Admin Feature Flags ---
  async getAdminFeatureFlags(): Promise<FeatureFlagDto[]> {
    return apiClient.get<FeatureFlagDto[]>('/admin/feature-flags');
  },

  async updateAdminFeatureFlag(name: string, data: { isEnabled: boolean; reason: string }): Promise<FeatureFlagDto> {
    return apiClient.put<FeatureFlagDto>(`/admin/feature-flags/${encodeURIComponent(name)}`, data);
  },

  // --- Admin System Health ---
  async getAdminHealth(): Promise<AdminHealthDto> {
    return apiClient.get<AdminHealthDto>('/admin/health');
  },

  // --- Admin Account Recovery Cache & In-flight Deduplication ---
  _recoveryCache: null as { data: AdminAccountRecoveryDto[]; timestamp: number; key: string } | null,
  _recoveryInFlight: null as { promise: Promise<AdminAccountRecoveryDto[]>; key: string } | null,
  _recoveryCooldownUntil: 0,

  invalidateAdminAccountRecoveriesCache() {
    casesApiService._recoveryCache = null;
    casesApiService._recoveryInFlight = null;
    // NOTE: Do NOT reset _recoveryCooldownUntil! 
    // Cache invalidation marks cached data as stale, but must strictly respect Backend HTTP 429 rate-limit cooldown.
  },

  async getAdminAccountRecoveries(params?: {
    status?: string;
    search?: string;
  }): Promise<AdminAccountRecoveryDto[]> {
    const q = new URLSearchParams();
    if (params?.status && params.status !== 'all') q.set('status', params.status);
    if (params?.search) q.set('search', params.search);
    const query = q.toString() ? `?${q.toString()}` : '';
    const cacheKey = query;
    const now = Date.now();

    // 1. Check in-flight promise sharing (single-flight deduplication)
    if (this._recoveryInFlight && this._recoveryInFlight.key === cacheKey) {
      return this._recoveryInFlight.promise;
    }

    // 2. Check fresh cache (TTL: 60s)
    const RECOVERY_CACHE_TTL_MS = 60000;
    if (
      this._recoveryCache &&
      this._recoveryCache.key === cacheKey &&
      now - this._recoveryCache.timestamp < RECOVERY_CACHE_TTL_MS
    ) {
      return this._recoveryCache.data;
    }

    // 3. Respect 429 cooldown if active
    if (now < this._recoveryCooldownUntil) {
      if (this._recoveryCache && this._recoveryCache.key === cacheKey) {
        return this._recoveryCache.data;
      }
      return [];
    }

    // 4. Create single-flight in-flight promise
    const fetchPromise = apiClient
      .get<AdminAccountRecoveryDto[]>(`/admin/account-recovery${query}`)
      .then((data) => {
        const result = Array.isArray(data) ? data : [];
        this._recoveryCache = {
          data: result,
          timestamp: Date.now(),
          key: cacheKey,
        };
        return result;
      })
      .catch((error: any) => {
        const status = error?.status ?? error?.response?.status;
        if (status === 429) {
          // Parse Retry-After from ApiError or fallback to 15s cooldown
          let cooldownSeconds = 15;
          if (typeof error?.retryAfterSeconds === 'number' && error.retryAfterSeconds > 0) {
            cooldownSeconds = error.retryAfterSeconds;
          } else if (error?.retryAfterHeader) {
            const parsedSec = parseInt(error.retryAfterHeader, 10);
            if (!isNaN(parsedSec) && parsedSec > 0) {
              cooldownSeconds = parsedSec;
            } else {
              const parsedDateMs = new Date(error.retryAfterHeader).getTime();
              if (!isNaN(parsedDateMs) && parsedDateMs > Date.now()) {
                cooldownSeconds = Math.ceil((parsedDateMs - Date.now()) / 1000);
              }
            }
          }
          this._recoveryCooldownUntil = Date.now() + cooldownSeconds * 1000;

          // If stale cache exists, gracefully return it to avoid breaking UI badge
          if (this._recoveryCache && this._recoveryCache.key === cacheKey) {
            return this._recoveryCache.data;
          }
          return [];
        }
        throw error;
      })
      .finally(() => {
        if (this._recoveryInFlight?.key === cacheKey) {
          this._recoveryInFlight = null;
        }
      });

    this._recoveryInFlight = { promise: fetchPromise, key: cacheKey };
    return fetchPromise;
  },

  async getAdminAccountRecoveryById(id: string): Promise<AdminAccountRecoveryDto> {
    return apiClient.get<AdminAccountRecoveryDto>(`/admin/account-recovery/${id}`);
  },

  async adminRequestMoreInfoAccountRecovery(id: string, data: { adminPin: string; reason: string }): Promise<AdminAccountRecoveryDto> {
    const res = await apiClient.post<AdminAccountRecoveryDto>(`/admin/account-recovery/${id}/more-info`, data);
    this.invalidateAdminAccountRecoveriesCache();
    return res;
  },

  async adminRejectAccountRecovery(id: string, data: { adminPin: string; reason: string }): Promise<AdminAccountRecoveryDto> {
    const res = await apiClient.post<AdminAccountRecoveryDto>(`/admin/account-recovery/${id}/reject`, data);
    this.invalidateAdminAccountRecoveriesCache();
    return res;
  },

  async adminApproveAccountRecovery(id: string, data: { adminPin: string; reason: string; userId: string }): Promise<AdminAccountRecoveryApproveResponse> {
    const res = await apiClient.post<AdminAccountRecoveryApproveResponse>(`/admin/account-recovery/${id}/approve`, data);
    this.invalidateAdminAccountRecoveriesCache();
    return res;
  },
};

if (typeof window !== 'undefined') {
  (window as any).casesApiService = casesApiService;
  window.addEventListener('spendwise_tokens_cleared', () => {
    casesApiService.invalidateAdminAccountRecoveriesCache();
  });
}

export interface AdminAccountRecoveryReviewDto {
  id: string;
  adminId: string;
  action: string;
  note: string;
  createdAt: string;
}

export interface AdminAccountRecoveryDto {
  id: string;
  fullName: string;
  phone: string;
  description: string;
  emailHint?: string | null;
  status: 'Pending' | 'MoreInfoNeeded' | 'Approved' | 'Rejected';
  resolvedUserId?: string | null;
  reviewedByAdminId?: string | null;
  reviewedAt?: string | null;
  adminNote?: string | null;
  createdAt: string;
  updatedAt?: string | null;
  reviews: AdminAccountRecoveryReviewDto[];
}

export interface AdminAccountRecoveryApproveResponse {
  recovery: AdminAccountRecoveryDto;
  temporaryPassword: string;
}

export interface AuditLogDto {
  id: string;
  actorUserId?: string | null;
  action: string;
  targetType: string;
  targetId?: string | null;
  reason?: string | null;
  metadataJson?: string | null;
  createdAt: string;
}

export interface AdminDashboardDto {
  totalUsers: number;
  activeUsers: number;
  newUsers: number;
  transactionCount: number;
  transactionVolume: number;
  p2pCount: number;
  successRate: number;
  failedTransactions: number;
  pendingTransactions: number;
  riskCases: number;
  disputes: number;
  supportCases: number;
  aiRequests: number;
  systemHealth: string;
}

interface RawAdminTransactionDto {
  id: string;
  type: string | number;
  status: string | number;
  amount: number;
  currency: string;
  ledgerTransactionId: string;
  relatedBusinessId?: string | null;
  createdAt: string;
}

const FINANCIAL_TRANSACTION_TYPES: Record<number, string> = {
  0: 'Income',
  1: 'Expense',
  2: 'TransferOut',
  3: 'TransferIn',
  4: 'InternalTransfer',
  5: 'GoalContribution',
  6: 'GoalWithdrawal',
  7: 'GroupFundContribution',
  8: 'DebtSettlement',
  9: 'Deposit',
  10: 'Withdrawal',
  11: 'Refund',
  12: 'Reversal',
  13: 'BankTopUp',
};

const FINANCIAL_TRANSACTION_STATUSES: Record<number, string> = {
  0: 'Pending',
  1: 'Processing',
  2: 'Completed',
  3: 'Failed',
  4: 'Cancelled',
  5: 'Reversed',
};

function normalizeAdminTransaction(raw: RawAdminTransactionDto): AdminTransactionDto {
  let normalizedType = String(raw.type ?? '');
  if (typeof raw.type === 'number' && FINANCIAL_TRANSACTION_TYPES[raw.type]) {
    normalizedType = FINANCIAL_TRANSACTION_TYPES[raw.type];
  }

  let normalizedStatus = String(raw.status ?? '');
  if (typeof raw.status === 'number' && FINANCIAL_TRANSACTION_STATUSES[raw.status]) {
    normalizedStatus = FINANCIAL_TRANSACTION_STATUSES[raw.status];
  }

  return {
    ...raw,
    type: normalizedType,
    status: normalizedStatus,
  };
}

export interface AdminTransactionDto {
  id: string;
  type: string;
  status: string;
  amount: number;
  currency: string;
  ledgerTransactionId: string;
  relatedBusinessId?: string | null;
  createdAt: string;
}

export interface LedgerTransactionDto {
  id: string;
  reference: string;
  businessType: string;
  businessId: string;
  status: string;
  debitTotal: number;
  creditTotal: number;
  balanced: boolean;
  createdAt: string;
}

export interface AdminUserDto {
  id: string;
  fullName: string;
  email: string;
  spendId: string;
  status: string;
  systemRole: string;
  mustChangePassword: boolean;
  hasSecurityPin: boolean;
  createdAt: string;
  lastLoginAt?: string | null;
}

export interface AdminTemporaryPasswordResponse {
  userId: string;
  temporaryPassword: string;
  mustChangePassword: boolean;
}

export interface AdminSecurityPinResetResponse {
  userId: string;
  resetRequired: boolean;
}

export interface FeatureFlagDto {
  name: string;
  isEnabled: boolean;
  description: string;
}

export interface AdminHealthDto {
  api?: string;
  sqlServer?: string;
  aiProvider?: string;
  signalR?: string;
  sePay?: {
    enabled: boolean;
    configured: boolean;
    environment: string;
  };
  notificationsOutbox?: {
    status: string;
    pending: number;
    failed: number;
  };
  recurringProcessor?: string;
  backgroundProcessing?: string;
  [key: string]: any;
}
