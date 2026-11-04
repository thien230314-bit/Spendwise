import { apiRequest } from './client';

export interface V1FundDto {
  id: string;
  name: string;
  kind: 'Fixed' | 'Free';
  targetVnd: number | null;
  contributionDurationHours: number | null;
  trustCommitmentAccepted?: boolean;
  status: string;
  currentCycle?: number;
  maxCycles?: number;
  balanceVnd?: number;
  availableSpendableVnd?: number;
  heldAmountVnd?: number;
  myContributionVnd?: number;
  myRole?: 'owner' | 'member';
  canSpend?: boolean;
  effectiveCanSpend?: boolean;
  membersCount?: number;
}

export interface V1MemberDto {
  userId: string;
  fullName: string;
  avatarUrl?: string;
  seat: number;
  isOwner: boolean;
  canSpend: boolean;
  effectiveCanSpend: boolean;
  individualContributionCompleted: boolean;
  groupFundingCompleted: boolean;
  canManageSpendingPermission: boolean;
  joinedAt: string;
  equityVnd?: number;
  contributedVnd?: number;
}

export interface V1FundRequestDto {
  id: string;
  fundId: string;
  requestedByUserId: string;
  recipientUserId: string;
  recipientName?: string;
  kind: 'Expense' | 'Withdrawal';
  amountVnd: number;
  purpose: string;
  state: 'AwaitingOwnerReview' | 'ApprovedUnused' | 'Completed' | 'Rejected' | 'Cancelled' | 'Expired';
  reviewedByUserId?: string;
  reviewedAt?: string;
  executionAvailability?: string;
  createdAt: string;
}

export interface V1FinanceDto {
  fundId: string;
  revision: number;
  targetVnd?: number;
  balanceVnd: number;
  contributionsEnabled: boolean;
  spendingState: string;
  obligations: {
    userId: string;
    seat: number;
    requiredVnd: number;
    paidVnd: number;
    remainingVnd: number;
    lockedExcessVnd: number;
  }[];
}

export interface CreateFixedFundPayload {
  name: string;
  kind: 'Fixed';
  targetVnd: number;
  contributionDurationHours: number; // 1 to 720
  trustCommitmentAccepted: boolean;
  commitmentVersion: string;
  securityPin: string;
}

export interface CreateFreeFundPayload {
  name: string;
  kind: 'Free';
  targetVnd: null;
  securityPin: string;
}

export const fundsApi = {
  getFunds: () => apiRequest<V1FundDto[]>('/api/v1/funds', { authenticated: true }),

  getFund: (id: string) => apiRequest<V1FundDto>(`/api/v1/funds/${id}`, { authenticated: true }),

  createFund: (payload: CreateFixedFundPayload | CreateFreeFundPayload) =>
    apiRequest<V1FundDto>('/api/v1/funds', {
      method: 'POST',
      body: JSON.stringify(payload),
      authenticated: true,
    }),

  getFinance: (id: string) =>
    apiRequest<V1FinanceDto>(`/api/v1/funds/${id}/finance`, { authenticated: true }),

  getMembers: (id: string) =>
    apiRequest<V1MemberDto[]>(`/api/v1/funds/${id}/members`, { authenticated: true }),

  confirmObligation: (id: string, targetVnd: number, securityPin: string) =>
    apiRequest<V1FinanceDto>(`/api/v1/funds/${id}/obligations/confirm`, {
      method: 'POST',
      body: JSON.stringify({ targetVnd, securityPin }),
      authenticated: true,
    }),

  contribute: (
    id: string,
    payload: {
      walletId: string;
      amountVnd: number;
      idempotencyKey: string;
      securityPin: string;
      obligationRevision?: number;
      purpose?: string;
    }
  ) =>
    apiRequest<{ id: string; amountVnd: number }>(`/api/v1/funds/${id}/contributions`, {
      method: 'POST',
      body: JSON.stringify(payload),
      authenticated: true,
    }),

  getRequests: (id: string) =>
    apiRequest<V1FundRequestDto[]>(`/api/v1/funds/${id}/requests`, { authenticated: true }),

  createRequest: (
    id: string,
    payload: {
      kind: 'Expense' | 'Withdrawal';
      amountVnd: number;
      recipientUserId: string;
      purpose: string;
      idempotencyKey: string;
      securityPin: string;
    }
  ) =>
    apiRequest<V1FundRequestDto>(`/api/v1/funds/${id}/requests`, {
      method: 'POST',
      body: JSON.stringify(payload),
      authenticated: true,
    }),

  reviewRequest: (
    requestId: string,
    payload: {
      approved: boolean;
      securityPin: string;
      reviewReason?: string;
    }
  ) =>
    apiRequest<V1FundRequestDto>(`/api/v1/funds/requests/${requestId}/review`, {
      method: 'POST',
      body: JSON.stringify(payload),
      authenticated: true,
    }),

  cancelRequest: (requestId: string) =>
    apiRequest<void>(`/api/v1/funds/requests/${requestId}/cancel`, {
      method: 'POST',
      authenticated: true,
    }),

  closeActivity: (id: string, securityPin: string) =>
    apiRequest<void>(`/api/v1/funds/${id}/activity/close`, {
      method: 'POST',
      body: JSON.stringify({ securityPin }),
      authenticated: true,
    }),

  leaveFund: (id: string, securityPin: string, idempotencyKey: string) =>
    apiRequest<void>(`/api/v1/funds/${id}/leave`, {
      method: 'POST',
      body: JSON.stringify({ securityPin, idempotencyKey, destinationWalletId: null }),
      authenticated: true,
    }),

  dissolveFund: (id: string, securityPin: string, idempotencyKey: string) =>
    apiRequest<void>(`/api/v1/funds/${id}/dissolve`, {
      method: 'POST',
      body: JSON.stringify({ securityPin, idempotencyKey }),
      authenticated: true,
    }),
};
