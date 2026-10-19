import {
  Group,
  GroupMember,
  GroupCategory,
  GroupRole,
  SharedFund,
  FundTransaction,
  FundContributionRequest,
  GroupFundRound,
  GroupExpense,
  GroupActivity,
  GroupDeletionRequest,
  GroupDeletionStatus,
  MemberVoteStatus,
  GroupReceivingBank,
} from '../types/groups';
import type {
  GroupDto,
  GroupMemberDto,
  SharedFundDto,
  FundTransactionDto,
  FundRequestDto,
  FundRoundDto,
  GroupExpenseDto,
  GroupActivityDto,
  GroupDeletionRequestDto,
  GroupReceivingBankDto,
} from './groupApiService';

export function deriveInitials(name?: string): string {
  if (!name || !name.trim()) return 'SW';
  const parts = name.trim().split(/\s+/);
  if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
  return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
}

export function mapGroupMember(dto: GroupMemberDto, currentUserId?: string): GroupMember {
  const role: GroupRole = dto.role === 'Owner' ? 'Owner' : 'Member';
  return {
    id: dto.userId,
    userId: dto.userId,
    name: dto.fullName,
    avatarInitials: deriveInitials(dto.fullName),
    groupRole: role,
    joinedDate: dto.joinedAt,
    isCurrentUser: Boolean(currentUserId && dto.userId === currentUserId),
    totalContributed: 0,
    totalSpent: 0,
    status: 'active',
  };
}

export function mapSharedFund(dto: SharedFundDto): SharedFund {
  return {
    id: dto.id,
    groupId: dto.groupId,
    balance: dto.balance,
    currency: (dto.currency === 'USD' ? 'USD' : 'VND'),
  };
}

export function mapFundTransaction(dto: FundTransactionDto, groupId: string): FundTransaction {
  const isContribution = dto.type.toLowerCase() === 'contribution';
  return {
    id: dto.id,
    groupId: groupId,
    type: isContribution ? 'contribution' : 'expense',
    amount: dto.amount,
    userId: dto.userId || '',
    userName: dto.userName || 'Thành viên',
    description: dto.description,
    date: dto.date,
    referenceCode: dto.referenceCode,
    sourceWalletName: undefined,
    paymentId: dto.paymentId || undefined,
  };
}

export function mapFundRequest(dto: FundRequestDto): FundContributionRequest {
  return {
    id: dto.id,
    groupId: dto.groupId,
    amountPerMember: dto.amountPerMember,
    purpose: dto.purpose,
    dueDate: dto.dueDate ? dto.dueDate.slice(0, 10) : '',
    createdDate: dto.createdAt ? dto.createdAt.slice(0, 10) : '',
    recipients: dto.recipients.map((r) => {
      let status: 'Pending' | 'Paid' | 'Overdue' | 'Cancelled' = 'Pending';
      if (r.status === 'Paid') status = 'Paid';
      else if (r.status === 'Overdue') status = 'Overdue';
      else if (r.status === 'Cancelled') status = 'Cancelled';
      return {
        userId: r.userId,
        userName: r.userName,
        status,
        paidAt: r.paidAt || undefined,
      };
    }),
  };
}

export function mapFundRound(dto: FundRoundDto): GroupFundRound {
  return {
    id: dto.id,
    groupId: dto.groupId,
    title: dto.title,
    description: dto.description || undefined,
    targetAmount: dto.targetAmount,
    collectedAmount: dto.collectedAmount,
    status: dto.status === 'Completed' ? 'Completed' : 'Active',
    createdByUserId: '',
    createdAt: dto.createdAt,
    completedAt: dto.completedAt || undefined,
    members: dto.members.map((m) => {
      let status: 'Unpaid' | 'Partial' | 'Paid' = 'Unpaid';
      if (m.status === 'Paid') status = 'Paid';
      else if (m.status === 'Partial') status = 'Partial';
      return {
        userId: m.userId,
        userName: m.userName,
        requiredAmount: m.requiredAmount,
        paidAmount: m.paidAmount,
        status,
      };
    }),
  };
}

export function mapGroupExpense(dto: GroupExpenseDto): GroupExpense {
  return {
    id: dto.id,
    groupId: dto.groupId,
    title: dto.title,
    amount: dto.amount,
    categoryId: dto.categoryId || 'cat-general',
    categoryName: dto.category,
    paidByType: dto.paidByType.toLowerCase() === 'fund' ? 'fund' : 'user',
    paidByUserId: dto.paidByUserId || undefined,
    paidByUserName: dto.paidByType.toLowerCase() === 'fund' ? 'Quỹ chung' : 'Thành viên',
    date: dto.date,
    description: dto.description || undefined,
    status: 'settled',
  };
}

export function mapGroupActivity(dto: GroupActivityDto, groupId: string, memberMap?: Map<string, string>): GroupActivity {
  let title = 'Hoạt động nhóm';
  let desc = '';
  let type: GroupActivity['type'] = 'request';

  let dataObj: any = {};
  if (dto.dataJson) {
    try {
      dataObj = JSON.parse(dto.dataJson);
    } catch {
      // ignore
    }
  }

  const actorName = (dto.actorUserId && memberMap?.get(dto.actorUserId)) || 'Thành viên';

  const typeLower = dto.type.toLowerCase();
  if (typeLower.includes('contribution')) {
    type = 'fund_contribution';
    title = 'Đóng góp quỹ chung';
    desc = `${actorName} đã đóng góp vào quỹ chung`;
  } else if (typeLower.includes('fundexpense') || (typeLower.includes('expense') && dataObj?.paidByType === 'Fund')) {
    type = 'fund_expense';
    title = dataObj?.Title || dataObj?.title || 'Chi tiêu từ quỹ';
    desc = `${actorName} đã chi từ quỹ chung`;
  } else if (typeLower.includes('expense')) {
    type = 'expense';
    title = dataObj?.Title || dataObj?.title || 'Chi tiêu nhóm';
    desc = `${actorName} đã ghi nhận chi tiêu`;
  } else if (typeLower.includes('invited')) {
    type = 'member_invited';
    title = 'Mời thành viên';
    desc = `${actorName} đã mời thành viên mới`;
  } else if (typeLower.includes('joined')) {
    type = 'member_joined';
    title = 'Thành viên tham gia';
    desc = `${actorName} đã tham gia nhóm`;
  } else if (typeLower.includes('removed')) {
    type = 'member_removed';
    title = 'Rời khỏi nhóm';
    desc = `${actorName} đã rời khỏi nhóm`;
  } else if (typeLower.includes('roundcreated')) {
    type = 'fund_round_created';
    title = 'Tạo đợt góp quỹ';
    desc = `${actorName} đã tạo đợt góp quỹ ${dataObj?.Title || dataObj?.title || ''}`;
  } else if (typeLower.includes('roundpaid')) {
    type = 'fund_round_paid';
    title = 'Đóng quỹ theo đợt';
    desc = `${actorName} đã đóng quỹ`;
  } else if (typeLower.includes('roundcompleted')) {
    type = 'fund_round_completed';
    title = 'Hoàn thành đợt góp quỹ';
    desc = `Đợt góp quỹ đã hoàn thành`;
  }

  return {
    id: dto.id,
    groupId,
    title,
    description: desc,
    type,
    userId: dto.actorUserId || undefined,
    userName: actorName,
    timestamp: dto.createdAt,
  };
}

export function mapGroupReceivingBank(dto: GroupReceivingBankDto): GroupReceivingBank {
  return {
    ownerUserId: dto.ownerUserId,
    bankConnectionId: dto.bankConnectionId,
    bankBrandName: dto.bankBrand,
    maskedAccountNumber: dto.maskedAccount,
    accountHolderName: dto.accountHolder,
  };
}

export function mapGroupDeletionRequest(
  dto: GroupDeletionRequestDto | null,
  ownerId?: string
): GroupDeletionRequest | null {
  if (!dto) return null;

  let status: GroupDeletionStatus = 'Pending';
  if (dto.status === 'Ready') status = 'Ready';
  else if (dto.status === 'Cancelled') status = 'Cancelled';
  else if (dto.status === 'Rejected') status = 'Rejected';

  const memberVotes = (dto.votes || []).map((v) => {
    let voteStatus: MemberVoteStatus = 'Pending';
    if (v.status === 'Approved') voteStatus = 'Approved';
    else if (v.status === 'Rejected') voteStatus = 'Rejected';
    return {
      userId: v.userId,
      userName: v.userName,
      status: voteStatus,
      respondedAt: v.votedAt || undefined,
    };
  });

  const memberRefunds = (dto.refunds || []).map((r) => ({
    id: r.id,
    userId: r.userId,
    userName: r.userName,
    totalContributed: r.contributionBasis,
    refundAmount: r.refundAmount,
    percentage: r.percentage ?? undefined,
    settlementStatus: (r.status === 'Completed' ? 'completed' : 'pending') as 'completed' | 'pending',
    paymentId: r.paymentId || undefined,
    paymentReference: r.paymentReference || undefined,
    completedAt: r.completedAt || undefined,
    destinationWalletId: r.destinationWalletId || undefined,
  }));

  const hasRefundPlan = (status === 'Ready' || dto.status === 'Completed' || memberRefunds.length > 0);

  return {
    id: dto.id,
    groupId: dto.groupId,
    requestedByUserId: ownerId || (dto.votes[0]?.userId ?? ''),
    requestedAt: dto.createdAt,
    status,
    memberVotes,
    refundPlan: hasRefundPlan
      ? {
          fundBalance: dto.fundBalanceSnapshot,
          totalContributionBasis: memberRefunds.reduce((sum, r) => sum + r.totalContributed, 0),
          memberRefunds,
          totalRefund: memberRefunds.reduce((sum, r) => sum + r.refundAmount, 0),
          calculatedAt: dto.readyAt || dto.createdAt,
        }
      : undefined,
  };
}

export interface GroupDetailsData {
  sharedFund?: SharedFundDto | null;
  fundTransactions?: FundTransactionDto[];
  fundRequests?: FundRequestDto[];
  fundRounds?: FundRoundDto[];
  expenses?: GroupExpenseDto[];
  activities?: GroupActivityDto[];
  deletionRequest?: GroupDeletionRequestDto | null;
}

export function mapGroupDtoToGroup(
  dto: GroupDto,
  currentUserId?: string,
  details?: GroupDetailsData
): Group {
  const members = (dto.members || []).map((m) => mapGroupMember(m, currentUserId));
  const memberMap = new Map<string, string>();
  members.forEach((m) => memberMap.set(m.userId, m.name));

  const role: GroupRole = dto.currentUserRole === 'Owner' ? 'Owner' : 'Member';
  const ownerId = dto.ownerId || (members.find((m) => m.groupRole === 'Owner')?.userId ?? '');

  const type: GroupCategory = (
    ['Roommates', 'Trip', 'Family', 'Couple', 'Friends'].includes(dto.type)
      ? dto.type
      : 'Other'
  ) as GroupCategory;

  const sharedFund: SharedFund = details?.sharedFund
    ? mapSharedFund(details.sharedFund)
    : {
        id: `fund-${dto.id}`,
        groupId: dto.id,
        balance: 0,
        currency: (dto.currency === 'USD' ? 'USD' : 'VND'),
      };

  const fundTransactions = (details?.fundTransactions || []).map((tx) =>
    mapFundTransaction(tx, dto.id)
  );

  const fundRequests = (details?.fundRequests || []).map(mapFundRequest);
  const fundRounds = (details?.fundRounds || []).map(mapFundRound);
  const expenses = (details?.expenses || []).map(mapGroupExpense);
  const activities = (details?.activities || []).map((act) =>
    mapGroupActivity(act, dto.id, memberMap)
  );
  const deletionRequest = mapGroupDeletionRequest(
    details?.deletionRequest ?? null,
    ownerId
  );
  const receivingBank = dto.receivingBank ? mapGroupReceivingBank(dto.receivingBank) : null;

  return {
    id: dto.id,
    name: dto.name,
    type,
    description: dto.description || '',
    currency: dto.currency || 'VND',
    createdDate: members[0]?.joinedDate || new Date().toISOString(),
    ownerId,
    currentUserRole: role,
    members,
    sharedFund,
    fundTransactions,
    fundRequests,
    fundRounds,
    expenses,
    activities,
    deletionRequest,
    receivingBank,
  };
}
