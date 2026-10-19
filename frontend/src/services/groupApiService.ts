import { apiClient } from '../lib';

// ---------------------------------------------------------------------------
// 1. Backend Group DTOs (aligning exactly with SpendWise.Application.Groups)
// ---------------------------------------------------------------------------

export interface CreateGroupRequest {
  name: string;
  description?: string;
  type: string;
  currency?: string;
  securityPin?: string;
}

export interface UpdateGroupRequest {
  name: string;
  description?: string;
  membersCanCreateExpenses: boolean;
}

export interface GroupMemberDto {
  userId: string;
  fullName: string;
  spendId: string;
  role: string;
  joinedAt: string;
}

export interface GroupReceivingBankDto {
  id: string;
  ownerUserId: string;
  bankConnectionId: string;
  bankBrand: string;
  maskedAccount: string;
  accountHolder: string;
}

export interface GroupDto {
  id: string;
  name: string;
  description?: string;
  type: string;
  status: string;
  membersCanCreateExpenses: boolean;
  currentUserRole: string;
  members: GroupMemberDto[];
  currency: string;
  ownerId?: string | null;
  receivingBank?: GroupReceivingBankDto | null;
}

export interface InviteGroupRequest {
  userId: string;
}

export interface GroupInviteDto {
  id: string;
  groupId: string;
  groupName: string;
  invitedUserId: string;
  status: string;
  expiresAt: string;
  token: string;
}

export interface SharedFundDto {
  id: string;
  groupId: string;
  balance: number;
  currency: string;
}

export interface CreateFundContributionPaymentRequest {
  amount: number;
  purpose?: string;
  idempotencyKey: string;
  walletId?: string;
  securityPin?: string;
}

export interface GroupContributionDto {
  id: string;
  groupId: string;
  userId: string;
  walletId: string;
  amount: number;
  purpose: string;
  ledgerTransactionId: string;
  createdAt: string;
  replayed: boolean;
}

export interface FundTransactionDto {
  id: string;
  type: string;
  amount: number;
  userId?: string | null;
  userName?: string | null;
  description: string;
  date: string;
  referenceCode: string;
  paymentId?: string | null;
  walletId?: string | null;
  ledgerTransactionId?: string | null;
}

export interface CreateFundRequestRequest {
  purpose: string;
  amountPerMember: number;
  dueDate?: string;
  recipientUserIds: string[];
}

export interface FundRequestRecipientDto {
  id: string;
  userId: string;
  userName: string;
  status: string;
  paymentId?: string | null;
  paidAt?: string | null;
}

export interface FundRequestDto {
  id: string;
  groupId: string;
  purpose: string;
  amountPerMember: number;
  dueDate?: string | null;
  recipients: FundRequestRecipientDto[];
  createdAt: string;
}

export interface PayFundRequestRequest {
  idempotencyKey: string;
  walletId?: string;
  securityPin?: string;
}

export interface CreateFundRoundRequest {
  title: string;
  description?: string;
  targetAmount: number;
}

export interface FundRoundMemberDto {
  id: string;
  userId: string;
  userName: string;
  requiredAmount: number;
  paidAmount: number;
  status: string;
}

export interface FundRoundDto {
  id: string;
  groupId: string;
  title: string;
  description?: string | null;
  targetAmount: number;
  collectedAmount: number;
  status: string;
  createdAt: string;
  completedAt?: string | null;
  members: FundRoundMemberDto[];
}

export interface PayFundRoundRequest {
  amount: number;
  idempotencyKey: string;
  walletId?: string;
  securityPin?: string;
}

export interface CreateGroupExpenseRequest {
  title: string;
  amount: number;
  categoryId?: string | null;
  category?: string | null;
  paidByType: string; // 'user' | 'fund'
  paidByUserId?: string | null;
  date?: string | null;
  description?: string | null;
  securityPin?: string | null;
  idempotencyKey?: string | null;
}

export interface GroupExpenseDto {
  id: string;
  groupId: string;
  title: string;
  amount: number;
  categoryId?: string | null;
  category: string;
  paidByType: string;
  paidByUserId?: string | null;
  date: string;
  description?: string | null;
  status: string;
}

export interface GroupActivityDto {
  id: string;
  actorUserId?: string | null;
  type: string;
  dataJson?: string | null;
  createdAt: string;
}

export interface GroupDeletionVoteRequest {
  approved: boolean;
  securityPin?: string;
}

export interface GroupDeletionVoteDto {
  id: string;
  userId: string;
  userName: string;
  status: string;
  votedAt?: string | null;
}

export interface GroupRefundDto {
  id: string;
  userId: string;
  userName: string;
  contributionBasis: number;
  refundAmount: number;
  percentage?: number | null;
  status: string;
  paymentId?: string | null;
  paymentReference?: string | null;
  completedAt?: string | null;
  destinationWalletId?: string | null;
  ledgerTransactionId?: string | null;
}

export interface GroupDeletionRequestDto {
  id: string;
  groupId: string;
  status: string;
  fundBalanceSnapshot: number;
  createdAt: string;
  readyAt?: string | null;
  completedAt?: string | null;
  votes: GroupDeletionVoteDto[];
  refunds: GroupRefundDto[];
}

export interface CreateGroupRefundPaymentRequest {
  idempotencyKey: string;
  securityPin?: string;
  walletId?: string;
}

export interface FinalizeGroupDeletionRequest {
  securityPin: string;
}

// ---------------------------------------------------------------------------
// 2. groupApiService Implementation
// ---------------------------------------------------------------------------

export const groupApiService = {
  // Groups Core
  async getGroups(): Promise<GroupDto[]> {
    return apiClient.get<GroupDto[]>('/groups');
  },

  async getGroup(groupId: string): Promise<GroupDto> {
    return apiClient.get<GroupDto>(`/groups/${groupId}`);
  },

  async createGroup(data: CreateGroupRequest): Promise<GroupDto> {
    return apiClient.post<GroupDto>('/groups', data);
  },

  async updateGroup(groupId: string, data: UpdateGroupRequest): Promise<GroupDto> {
    return apiClient.put<GroupDto>(`/groups/${groupId}`, data);
  },

  // Invites & Membership
  async inviteMember(groupId: string, data: InviteGroupRequest): Promise<GroupInviteDto> {
    return apiClient.post<GroupInviteDto>(`/groups/${groupId}/invites`, data);
  },

  async getInvitations(): Promise<GroupInviteDto[]> {
    return apiClient.get<GroupInviteDto[]>('/groups/invites');
  },

  async acceptInvite(inviteId: string, token: string): Promise<GroupDto> {
    return apiClient.post<GroupDto>(`/groups/invites/${inviteId}/accept?token=${encodeURIComponent(token)}`, {});
  },

  async rejectInvite(inviteId: string): Promise<GroupInviteDto> {
    return apiClient.post<GroupInviteDto>(`/groups/invites/${inviteId}/reject`, {});
  },

  async removeMember(groupId: string, memberUserId: string): Promise<void> {
    return apiClient.delete<void>(`/groups/${groupId}/members/${memberUserId}`);
  },

  async transferOwnership(groupId: string, memberUserId: string): Promise<void> {
    return apiClient.post<void>(`/groups/${groupId}/ownership/${memberUserId}`, {});
  },

  // Group Shared Fund
  async getSharedFund(groupId: string): Promise<SharedFundDto> {
    return apiClient.get<SharedFundDto>(`/groups/${groupId}/fund`);
  },

  async getFundTransactions(groupId: string): Promise<FundTransactionDto[]> {
    return apiClient.get<FundTransactionDto[]>(`/groups/${groupId}/fund/transactions`);
  },

  async contributeToFund(
    groupId: string,
    data: CreateFundContributionPaymentRequest
  ): Promise<GroupContributionDto> {
    return apiClient.post<GroupContributionDto>(`/groups/${groupId}/fund/contributions`, data);
  },

  // Fund Requests
  async getFundRequests(groupId: string): Promise<FundRequestDto[]> {
    return apiClient.get<FundRequestDto[]>(`/groups/${groupId}/fund-requests`);
  },

  async createFundRequest(groupId: string, data: CreateFundRequestRequest): Promise<FundRequestDto> {
    return apiClient.post<FundRequestDto>(`/groups/${groupId}/fund-requests`, data);
  },

  async payFundRequest(
    groupId: string,
    requestId: string,
    data: PayFundRequestRequest
  ): Promise<GroupContributionDto> {
    return apiClient.post<GroupContributionDto>(`/groups/${groupId}/fund-requests/${requestId}/pay`, data);
  },

  // Fund Rounds
  async getFundRounds(groupId: string): Promise<FundRoundDto[]> {
    return apiClient.get<FundRoundDto[]>(`/groups/${groupId}/fund-rounds`);
  },

  async createFundRound(groupId: string, data: CreateFundRoundRequest): Promise<FundRoundDto> {
    return apiClient.post<FundRoundDto>(`/groups/${groupId}/fund-rounds`, data);
  },

  async payFundRound(
    groupId: string,
    roundId: string,
    data: PayFundRoundRequest
  ): Promise<GroupContributionDto> {
    return apiClient.post<GroupContributionDto>(`/groups/${groupId}/fund-rounds/${roundId}/pay`, data);
  },

  // Shared Expenses
  async getGroupExpenses(groupId: string): Promise<GroupExpenseDto[]> {
    return apiClient.get<GroupExpenseDto[]>(`/groups/${groupId}/expenses`);
  },

  async createGroupExpense(groupId: string, data: CreateGroupExpenseRequest): Promise<GroupExpenseDto> {
    return apiClient.post<GroupExpenseDto>(`/groups/${groupId}/expenses`, data);
  },

  // Activities
  async getGroupActivities(groupId: string): Promise<GroupActivityDto[]> {
    return apiClient.get<GroupActivityDto[]>(`/groups/${groupId}/activities`);
  },

  // Deletion, Voting & Dissolution Refunds
  async getDeletionRequest(groupId: string): Promise<GroupDeletionRequestDto | null> {
    return apiClient.get<GroupDeletionRequestDto | null>(`/groups/${groupId}/deletion-request`);
  },

  async requestDeletion(groupId: string): Promise<GroupDeletionRequestDto> {
    return apiClient.post<GroupDeletionRequestDto>(`/groups/${groupId}/deletion-requests`, {});
  },

  async voteDeletion(
    groupId: string,
    requestId: string,
    data: GroupDeletionVoteRequest
  ): Promise<GroupDeletionRequestDto> {
    return apiClient.post<GroupDeletionRequestDto>(`/groups/${groupId}/deletion-requests/${requestId}/vote`, data);
  },

  async cancelDeletion(groupId: string, requestId: string): Promise<GroupDeletionRequestDto> {
    return apiClient.post<GroupDeletionRequestDto>(`/groups/${groupId}/deletion-requests/${requestId}/cancel`, {});
  },

  async createRefundPayment(
    groupId: string,
    requestId: string,
    refundId: string,
    data: CreateGroupRefundPaymentRequest
  ): Promise<GroupRefundDto> {
    return apiClient.post<GroupRefundDto>(
      `/groups/${groupId}/deletion-requests/${requestId}/refunds/${refundId}/payment`,
      data
    );
  },

  async finalizeDeletion(
    groupId: string,
    requestId: string,
    data: FinalizeGroupDeletionRequest
  ): Promise<GroupDeletionRequestDto> {
    return apiClient.post<GroupDeletionRequestDto>(
      `/groups/${groupId}/deletion-requests/${requestId}/finalize`,
      data
    );
  },
};
