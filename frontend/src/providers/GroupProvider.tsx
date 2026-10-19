import React, { createContext, useContext, useState, useCallback, useMemo, useRef, useEffect } from 'react';
import {
  Group,
  GroupMember,
  GroupCategory,
  GroupFundRound,
  GroupDeletionRequest,
  GroupDeletionRefundPlan,
} from '../types/groups';
import { useAuth } from './AuthProvider';
import { useFinancial } from './FinancialProvider';
import { groupApiService } from '../services/groupApiService';
import {
  mapGroupDtoToGroup,
  GroupDetailsData,
} from '../services/groupMapper';
import { isApiError } from '../lib';
import { friendService } from '../services/friendService';
import { friendsApiService } from '../services/friendsApiService';

export interface CreateGroupParams {
  name: string;
  type: GroupCategory;
  description: string;
  currency?: string;
  securityPin?: string;
}

export interface CreateSharedExpenseParams {
  groupId: string;
  title: string;
  amount: number;
  categoryId: string;
  categoryName: string;
  paidByType: 'user' | 'fund';
  paidByUserId?: string;
  paidByUserName: string;
  date: string;
  description?: string;
  securityPin?: string;
}

export interface GroupContextType {
  groups: Group[];
  selectedGroupId: string | null;
  selectedGroup: Group | null;
  isLoading: boolean;
  error: string | null;
  setSelectedGroupId: (id: string | null) => void;
  getGroup: (id: string) => Group | undefined;
  refreshGroups: () => Promise<void>;
  refreshSelectedGroup: (groupId?: string) => Promise<void>;
  createGroup: (params: CreateGroupParams) => Promise<{ success: boolean; group?: Group; error?: string }>;
  updateGroupInfo: (
    groupId: string,
    updates: { name?: string; description?: string; type?: GroupCategory; membersCanCreateExpenses?: boolean }
  ) => Promise<{ success: boolean; error?: string }>;
  closeGroup: (groupId: string) => Promise<{ success: boolean; error?: string }>;
  transferOwnership: (groupId: string, newOwnerUserId: string) => Promise<{ success: boolean; error?: string }>;
  inviteMember: (groupId: string, identifier: string) => Promise<{ success: boolean; member?: GroupMember; error?: string }>;
  removeMember: (groupId: string, memberId: string) => Promise<{ success: boolean; error?: string }>;
  contributeToFund: (params: {
    groupId: string;
    amount: number;
    purpose?: string;
    walletId?: string;
    securityPin?: string;
    idempotencyKey?: string;
  }) => Promise<{ success: boolean; error?: string }>;
  createFundRound: (params: {
    groupId: string;
    title: string;
    targetAmount: number;
    description?: string;
  }) => Promise<{ success: boolean; round?: GroupFundRound; error?: string }>;
  payFundRound: (params: {
    groupId: string;
    roundId: string;
    amount: number;
    walletId?: string;
    securityPin?: string;
    idempotencyKey?: string;
  }) => Promise<{ success: boolean; error?: string }>;
  createFundRequest: (params: {
    groupId: string;
    amountPerMember: number;
    purpose: string;
    dueDate: string;
    recipientUserIds: string[];
  }) => Promise<{ success: boolean; error?: string }>;
  payFundRequest: (params: {
    groupId: string;
    requestId: string;
    walletId?: string;
    securityPin?: string;
    idempotencyKey?: string;
  }) => Promise<{ success: boolean; error?: string }>;
  createSharedExpense: (params: CreateSharedExpenseParams) => Promise<{ success: boolean; error?: string; expenseId?: string }>;
  requestGroupDeletion: (groupId: string) => Promise<{ success: boolean; error?: string; request?: GroupDeletionRequest }>;
  cancelGroupDeletion: (groupId: string) => Promise<{ success: boolean; error?: string }>;
  voteGroupDeletion: (groupId: string, decision: 'Approve' | 'Reject', securityPin?: string) => Promise<{ success: boolean; error?: string }>;
  executeRefund: (params: {
    groupId: string;
    requestId: string;
    refundId: string;
    walletId?: string;
    securityPin?: string;
    idempotencyKey?: string;
  }) => Promise<{ success: boolean; error?: string }>;
  finalizeGroupDeletionAndRefund: (groupId: string, securityPin?: string) => Promise<{
    success: boolean;
    error?: string;
    refundPlan?: GroupDeletionRefundPlan;
  }>;
}

const GroupContext = createContext<GroupContextType | undefined>(undefined);

function extractApiErrorMessage(err: unknown, fallback: string): string {
  if (isApiError(err)) {
    return err.detail || err.title || fallback;
  }
  if (err instanceof Error) {
    return err.message;
  }
  return fallback;
}

export const GroupProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const { currentUser, isAuthenticated } = useAuth();
  const { refreshFinancialCore } = useFinancial();

  const [allGroups, setAllGroups] = useState<Group[]>([]);
  const [selectedGroupId, setSelectedGroupId] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);

  const activeUserIdRef = useRef<string | null>(null);

  // Helper to fetch details for a specific group
  const fetchGroupDetailsData = useCallback(async (groupId: string): Promise<GroupDetailsData> => {
    try {
      const [
        sharedFund,
        fundTransactions,
        fundRequests,
        fundRounds,
        expenses,
        activities,
        deletionRequest,
      ] = await Promise.allSettled([
        groupApiService.getSharedFund(groupId),
        groupApiService.getFundTransactions(groupId),
        groupApiService.getFundRequests(groupId),
        groupApiService.getFundRounds(groupId),
        groupApiService.getGroupExpenses(groupId),
        groupApiService.getGroupActivities(groupId),
        groupApiService.getDeletionRequest(groupId),
      ]);

      return {
        sharedFund: sharedFund.status === 'fulfilled' ? sharedFund.value : null,
        fundTransactions: fundTransactions.status === 'fulfilled' ? fundTransactions.value : [],
        fundRequests: fundRequests.status === 'fulfilled' ? fundRequests.value : [],
        fundRounds: fundRounds.status === 'fulfilled' ? fundRounds.value : [],
        expenses: expenses.status === 'fulfilled' ? expenses.value : [],
        activities: activities.status === 'fulfilled' ? activities.value : [],
        deletionRequest: deletionRequest.status === 'fulfilled' ? deletionRequest.value : null,
      };
    } catch (e) {
      console.warn(`Failed to fetch details for group ${groupId}:`, e);
      return {};
    }
  }, []);

  // Fetch all groups from backend
  const refreshGroups = useCallback(async () => {
    if (!isAuthenticated || !currentUser?.id || currentUser.role === 'SystemAdmin') {
      setAllGroups([]);
      setIsLoading(false);
      return;
    }
    setIsLoading(true);
    setError(null);
    try {
      const groupDtos = await groupApiService.getGroups();
      const currentUserId = currentUser.id;

      // Map basic group dtos to groups, fetching details for each (or active group)
      const mappedGroups = await Promise.all(
        groupDtos.map(async (dto) => {
          const details = await fetchGroupDetailsData(dto.id);
          return mapGroupDtoToGroup(dto, currentUserId, details);
        })
      );

      setAllGroups(mappedGroups);
    } catch (err) {
      const msg = extractApiErrorMessage(err, 'Failed to fetch groups');
      setError(msg);
    } finally {
      setIsLoading(false);
    }
  }, [isAuthenticated, currentUser?.id, currentUser?.role, fetchGroupDetailsData]);

  // Refresh single group details
  const refreshSelectedGroup = useCallback(
    async (groupId?: string) => {
      const targetId = groupId || selectedGroupId;
      if (!targetId || !currentUser?.id || currentUser.role === 'SystemAdmin') return;

      try {
        const [dto, details] = await Promise.all([
          groupApiService.getGroup(targetId),
          fetchGroupDetailsData(targetId),
        ]);
        const mapped = mapGroupDtoToGroup(dto, currentUser.id, details);

        setAllGroups((prev) => {
          const exists = prev.some((g) => g.id === targetId);
          if (exists) {
            return prev.map((g) => (g.id === targetId ? mapped : g));
          }
          return [...prev, mapped];
        });
      } catch (err) {
        console.warn(`Failed to refresh group ${targetId}:`, err);
      }
    },
    [selectedGroupId, currentUser?.id, currentUser?.role, fetchGroupDetailsData]
  );

  // Sync groups on user change / login
  useEffect(() => {
    const currentUserId = currentUser?.id ?? null;
    const isSystemAdmin = currentUser?.role === 'SystemAdmin';
    if (activeUserIdRef.current !== currentUserId) {
      activeUserIdRef.current = currentUserId;
      setSelectedGroupId(null);
      if (isAuthenticated && currentUserId && !isSystemAdmin) {
        refreshGroups();
      } else {
        setAllGroups([]);
      }
    }
  }, [currentUser?.id, currentUser?.role, isAuthenticated, refreshGroups]);

  const selectedGroup = useMemo(() => {
    if (!selectedGroupId) return null;
    return allGroups.find((g) => g.id === selectedGroupId) || null;
  }, [allGroups, selectedGroupId]);

  const getGroup = useCallback(
    (id: string) => {
      return allGroups.find((g) => g.id === id);
    },
    [allGroups]
  );

  // 1. CREATE GROUP
  const createGroup = useCallback(
    async (params: CreateGroupParams) => {
      try {
        const createdDto = await groupApiService.createGroup({
          name: params.name,
          description: params.description,
          type: params.type,
          currency: params.currency || 'VND',
          securityPin: params.securityPin,
        });
        const details = await fetchGroupDetailsData(createdDto.id);
        const mapped = mapGroupDtoToGroup(createdDto, currentUser?.id, details);

        setAllGroups((prev) => [mapped, ...prev]);
        setSelectedGroupId(mapped.id);
        return { success: true, group: mapped };
      } catch (err) {
        return { success: false, error: extractApiErrorMessage(err, 'Failed to create group') };
      }
    },
    [currentUser?.id, fetchGroupDetailsData]
  );

  // 2. UPDATE GROUP INFO
  const updateGroupInfo = useCallback(
    async (
      groupId: string,
      updates: { name?: string; description?: string; type?: GroupCategory; membersCanCreateExpenses?: boolean }
    ) => {
      try {
        const current = allGroups.find((g) => g.id === groupId);
        const name = updates.name !== undefined ? updates.name : (current?.name || '');
        const description = updates.description !== undefined ? updates.description : current?.description;
        const membersCanCreateExpenses = updates.membersCanCreateExpenses !== undefined ? updates.membersCanCreateExpenses : true;

        await groupApiService.updateGroup(groupId, {
          name,
          description,
          membersCanCreateExpenses,
        });
        await refreshSelectedGroup(groupId);
        return { success: true };
      } catch (err) {
        return { success: false, error: extractApiErrorMessage(err, 'Failed to update group') };
      }
    },
    [allGroups, refreshSelectedGroup]
  );

  // 3. TRANSFER OWNERSHIP
  const transferOwnership = useCallback(
    async (groupId: string, newOwnerUserId: string) => {
      try {
        await groupApiService.transferOwnership(groupId, newOwnerUserId);
        await refreshSelectedGroup(groupId);
        return { success: true };
      } catch (err) {
        return { success: false, error: extractApiErrorMessage(err, 'Failed to transfer ownership') };
      }
    },
    [refreshSelectedGroup]
  );

  // 5. INVITE MEMBER
  const inviteMember = useCallback(
    async (groupId: string, identifier: string) => {
      try {
        const trimmed = identifier.trim();
        let targetUserId: string | null = null;

        // Check if identifier is already a valid GUID
        const guidRegex = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
        if (guidRegex.test(trimmed)) {
          targetUserId = trimmed;
        } else {
          // Check friends list first
          if (currentUser?.id) {
            const friends = friendService.getFriends(currentUser.id);
            const foundFriend = friends.find(
              (f) =>
                f.userId === trimmed ||
                (f.email && f.email.toLowerCase() === trimmed.toLowerCase()) ||
                (f.phone && f.phone.trim() === trimmed) ||
                (f.spendId && f.spendId.toLowerCase() === trimmed.toLowerCase())
            );
            if (foundFriend?.userId) {
              targetUserId = foundFriend.userId;
            }
          }

          // Fallback to backend user/contact search API
          if (!targetUserId) {
            try {
              const contactDto = await friendsApiService.searchFriendContact(trimmed);
              if (contactDto?.userId) {
                targetUserId = contactDto.userId;
              }
            } catch {
              // search returned not found or error
            }
          }
        }

        if (!targetUserId) {
          return {
            success: false,
            error: 'Không tìm thấy người dùng phù hợp hoặc tài khoản không có trong danh sách bạn bè.',
          };
        }

        await groupApiService.inviteMember(groupId, { userId: targetUserId });
        await refreshSelectedGroup(groupId);
        const updated = allGroups.find((g) => g.id === groupId);
        const member = updated?.members.find((m) => m.userId === targetUserId);
        return { success: true, member };
      } catch (err) {
        return { success: false, error: extractApiErrorMessage(err, 'Failed to invite member') };
      }
    },
    [currentUser?.id, refreshSelectedGroup, allGroups]
  );

  // 6. REMOVE MEMBER
  const removeMember = useCallback(
    async (groupId: string, memberId: string) => {
      try {
        await groupApiService.removeMember(groupId, memberId);
        await refreshSelectedGroup(groupId);
        return { success: true };
      } catch (err) {
        return { success: false, error: extractApiErrorMessage(err, 'Failed to remove member') };
      }
    },
    [refreshSelectedGroup]
  );

  // 7. CONTRIBUTE TO FUND (Wallet-funded only)
  const contributeToFund = useCallback(
    async (params: {
      groupId: string;
      amount: number;
      purpose?: string;
      walletId?: string;
      securityPin?: string;
      idempotencyKey?: string;
    }) => {
      try {
        const idempotencyKey =
          params.idempotencyKey || `grp-contrib-${Date.now()}-${Math.random().toString(36).slice(2, 9)}`;
        await groupApiService.contributeToFund(params.groupId, {
          amount: params.amount,
          purpose: params.purpose,
          walletId: params.walletId,
          securityPin: params.securityPin,
          idempotencyKey,
        });

        // Backend ledger committed: refetch authoritative balances
        await refreshSelectedGroup(params.groupId);
        if (refreshFinancialCore) {
          await refreshFinancialCore();
        }
        return { success: true };
      } catch (err) {
        return { success: false, error: extractApiErrorMessage(err, 'Failed to contribute to fund') };
      }
    },
    [refreshSelectedGroup, refreshFinancialCore]
  );

  // 8. CREATE FUND ROUND
  const createFundRound = useCallback(
    async (params: {
      groupId: string;
      title: string;
      targetAmount: number;
      description?: string;
    }) => {
      try {
        await groupApiService.createFundRound(params.groupId, {
          title: params.title,
          description: params.description,
          targetAmount: params.targetAmount,
        });
        await refreshSelectedGroup(params.groupId);
        const updated = allGroups.find((g) => g.id === params.groupId);
        const round = updated?.fundRounds?.[0];
        return { success: true, round };
      } catch (err) {
        return { success: false, error: extractApiErrorMessage(err, 'Failed to create fund round') };
      }
    },
    [refreshSelectedGroup, allGroups]
  );

  // 9. PAY FUND ROUND (Wallet-funded only)
  const payFundRound = useCallback(
    async (params: {
      groupId: string;
      roundId: string;
      amount: number;
      walletId?: string;
      securityPin?: string;
      idempotencyKey?: string;
    }) => {
      try {
        const idempotencyKey =
          params.idempotencyKey || `grp-roundpay-${Date.now()}-${Math.random().toString(36).slice(2, 9)}`;
        await groupApiService.payFundRound(params.groupId, params.roundId, {
          amount: params.amount,
          walletId: params.walletId,
          securityPin: params.securityPin,
          idempotencyKey,
        });

        await refreshSelectedGroup(params.groupId);
        if (refreshFinancialCore) {
          await refreshFinancialCore();
        }
        return { success: true };
      } catch (err) {
        return { success: false, error: extractApiErrorMessage(err, 'Failed to pay fund round') };
      }
    },
    [refreshSelectedGroup, refreshFinancialCore]
  );

  // 10. CREATE FUND REQUEST
  const createFundRequest = useCallback(
    async (params: {
      groupId: string;
      amountPerMember: number;
      purpose: string;
      dueDate: string;
      recipientUserIds: string[];
    }) => {
      try {
        await groupApiService.createFundRequest(params.groupId, {
          purpose: params.purpose,
          amountPerMember: params.amountPerMember,
          dueDate: params.dueDate ? new Date(params.dueDate).toISOString() : undefined,
          recipientUserIds: params.recipientUserIds,
        });
        await refreshSelectedGroup(params.groupId);
        return { success: true };
      } catch (err) {
        return { success: false, error: extractApiErrorMessage(err, 'Failed to create fund request') };
      }
    },
    [refreshSelectedGroup]
  );

  // 11. PAY FUND REQUEST (Wallet-funded only)
  const payFundRequest = useCallback(
    async (params: {
      groupId: string;
      requestId: string;
      walletId?: string;
      securityPin?: string;
      idempotencyKey?: string;
    }) => {
      try {
        const idempotencyKey =
          params.idempotencyKey || `grp-reqpay-${Date.now()}-${Math.random().toString(36).slice(2, 9)}`;
        await groupApiService.payFundRequest(params.groupId, params.requestId, {
          walletId: params.walletId,
          securityPin: params.securityPin,
          idempotencyKey,
        });

        await refreshSelectedGroup(params.groupId);
        if (refreshFinancialCore) {
          await refreshFinancialCore();
        }
        return { success: true };
      } catch (err) {
        return { success: false, error: extractApiErrorMessage(err, 'Failed to pay fund request') };
      }
    },
    [refreshSelectedGroup, refreshFinancialCore]
  );

  // 12. CREATE SHARED EXPENSE
  const createSharedExpense = useCallback(
    async (params: CreateSharedExpenseParams) => {
      try {
        const idempotencyKey = `grp-expense-${Date.now()}-${Math.random().toString(36).slice(2, 9)}`;
        const isGuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
        const categoryId =
          params.categoryId && isGuid.test(params.categoryId) ? params.categoryId : null;

        const dto = await groupApiService.createGroupExpense(params.groupId, {
          title: params.title,
          amount: params.amount,
          categoryId,
          category: params.categoryName,
          paidByType: params.paidByType,
          paidByUserId: params.paidByUserId,
          date: params.date ? new Date(params.date).toISOString() : new Date().toISOString(),
          description: params.description,
          securityPin: params.securityPin,
          idempotencyKey,
        });

        await refreshSelectedGroup(params.groupId);
        if (params.paidByType === 'fund' && refreshFinancialCore) {
          await refreshFinancialCore();
        }
        return { success: true, expenseId: dto.id };
      } catch (err) {
        return { success: false, error: extractApiErrorMessage(err, 'Failed to create expense') };
      }
    },
    [refreshSelectedGroup, refreshFinancialCore]
  );

  // 13. REQUEST GROUP DELETION (Owner only)
  const requestGroupDeletion = useCallback(
    async (groupId: string) => {
      try {
        await groupApiService.requestDeletion(groupId);
        await refreshSelectedGroup(groupId);
        const updated = allGroups.find((g) => g.id === groupId);
        return { success: true, request: updated?.deletionRequest || undefined };
      } catch (err) {
        return { success: false, error: extractApiErrorMessage(err, 'Failed to request group deletion') };
      }
    },
    [refreshSelectedGroup, allGroups]
  );

  // 14. CANCEL GROUP DELETION (Owner only)
  const cancelGroupDeletion = useCallback(
    async (groupId: string) => {
      try {
        const current = allGroups.find((g) => g.id === groupId);
        const reqId = current?.deletionRequest?.id;
        if (!reqId) {
          return { success: false, error: 'No active deletion request' };
        }
        await groupApiService.cancelDeletion(groupId, reqId);
        await refreshSelectedGroup(groupId);
        return { success: true };
      } catch (err) {
        return { success: false, error: extractApiErrorMessage(err, 'Failed to cancel deletion') };
      }
    },
    [allGroups, refreshSelectedGroup]
  );

  // 15. VOTE ON GROUP DELETION
  const voteGroupDeletion = useCallback(
    async (groupId: string, decision: 'Approve' | 'Reject', securityPin?: string) => {
      try {
        const current = allGroups.find((g) => g.id === groupId);
        const reqId = current?.deletionRequest?.id;
        if (!reqId) {
          return { success: false, error: 'No active deletion request' };
        }
        await groupApiService.voteDeletion(groupId, reqId, {
          approved: decision === 'Approve',
          securityPin,
        });
        await refreshSelectedGroup(groupId);
        return { success: true };
      } catch (err) {
        return { success: false, error: extractApiErrorMessage(err, 'Failed to vote on deletion') };
      }
    },
    [allGroups, refreshSelectedGroup]
  );

  // 16. EXECUTE REFUND (Dissolution refund to personal wallet)
  const executeRefund = useCallback(
    async (params: {
      groupId: string;
      requestId: string;
      refundId: string;
      walletId?: string;
      securityPin?: string;
      idempotencyKey?: string;
    }) => {
      try {
        const idempotencyKey =
          params.idempotencyKey || `grp-refund-${Date.now()}-${Math.random().toString(36).slice(2, 9)}`;
        await groupApiService.createRefundPayment(params.groupId, params.requestId, params.refundId, {
          walletId: params.walletId,
          securityPin: params.securityPin,
          idempotencyKey,
        });

        await refreshSelectedGroup(params.groupId);
        if (refreshFinancialCore) {
          await refreshFinancialCore();
        }
        return { success: true };
      } catch (err) {
        return { success: false, error: extractApiErrorMessage(err, 'Failed to process refund') };
      }
    },
    [refreshSelectedGroup, refreshFinancialCore]
  );

  // 17. FINALIZE GROUP DELETION (Owner PIN required)
  const finalizeGroupDeletionAndRefund = useCallback(
    async (groupId: string, securityPin?: string) => {
      try {
        const current = allGroups.find((g) => g.id === groupId);
        const reqId = current?.deletionRequest?.id;
        if (!reqId) {
          return { success: false, error: 'No active deletion request found' };
        }
        await groupApiService.finalizeDeletion(groupId, reqId, {
          securityPin: securityPin || '',
        });
        await refreshGroups();
        return { success: true };
      } catch (err) {
        return { success: false, error: extractApiErrorMessage(err, 'Failed to finalize deletion') };
      }
    },
    [allGroups, refreshGroups]
  );

  // 18. CLOSE GROUP (initiates deletion request)
  const closeGroup = useCallback(
    async (groupId: string) => {
      return requestGroupDeletion(groupId);
    },
    [requestGroupDeletion]
  );

  return (
    <GroupContext.Provider
      value={{
        groups: allGroups,
        selectedGroupId,
        selectedGroup,
        isLoading,
        error,
        setSelectedGroupId,
        getGroup,
        refreshGroups,
        refreshSelectedGroup,
        createGroup,
        updateGroupInfo,
        closeGroup,
        transferOwnership,
        inviteMember,
        removeMember,
        contributeToFund,
        createFundRound,
        payFundRound,
        createFundRequest,
        payFundRequest,
        createSharedExpense,
        requestGroupDeletion,
        cancelGroupDeletion,
        voteGroupDeletion,
        executeRefund,
        finalizeGroupDeletionAndRefund,
      }}
    >
      {children}
    </GroupContext.Provider>
  );
};

export const useGroup = (): GroupContextType => {
  const context = useContext(GroupContext);
  if (!context) {
    throw new Error('useGroup must be used within a GroupProvider');
  }
  return context;
};
