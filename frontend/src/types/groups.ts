export type GroupRole = 'Owner' | 'Member';

export type GroupCategory =
  | 'Roommates'
  | 'Trip'
  | 'Family'
  | 'Couple'
  | 'Friends'
  | 'Other';

export interface GroupMember {
  id: string;
  userId: string;
  name: string;
  avatarInitials: string;
  groupRole: GroupRole;
  joinedDate: string;
  isCurrentUser?: boolean;
  totalContributed: number;
  totalSpent: number;
  status: 'active' | 'pending_invitation';
  phone?: string;
  email?: string;
}

export interface SharedFund {
  id: string;
  groupId: string;
  balance: number;
  currency: 'VND' | 'USD';
}

export interface FundTransaction {
  id: string;
  groupId: string;
  type: 'contribution' | 'expense';
  amount: number;
  userId: string;
  userName: string;
  description: string;
  date: string;
  referenceCode: string;
  sourceWalletName?: string;
  paymentId?: string;
}

export interface FundContributionRecipient {
  userId: string;
  userName: string;
  status: 'Pending' | 'Paid' | 'Overdue' | 'Cancelled';
  paidAt?: string;
}

export interface FundContributionRequest {
  id: string;
  groupId: string;
  amountPerMember: number;
  purpose: string;
  dueDate: string;
  createdDate: string;
  recipients: FundContributionRecipient[];
}

export interface GroupExpense {
  id: string;
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
  status: 'settled' | 'unsettled';
}

export type GroupFundRoundStatus = 'Active' | 'Completed';
export type FundRoundMemberStatus = 'Unpaid' | 'Partial' | 'Paid';

export interface GroupFundRoundMember {
  userId: string;
  userName: string;
  requiredAmount: number;
  paidAmount: number;
  status: FundRoundMemberStatus;
}

export interface GroupFundRound {
  id: string;
  groupId: string;
  title: string;
  description?: string;
  targetAmount: number;
  collectedAmount: number;
  status: GroupFundRoundStatus;
  createdByUserId: string;
  createdAt: string;
  completedAt?: string;
  members: GroupFundRoundMember[];
}

export interface GroupActivity {
  id: string;
  groupId: string;
  title: string;
  description: string;
  titleKey?: string;
  descriptionKey?: string;
  i18nParams?: Record<string, string | number>;
  type:
    | 'expense'
    | 'fund_contribution'
    | 'member_invited'
    | 'member_joined'
    | 'member_removed'
    | 'request'
    | 'fund_expense'
    | 'fund_round_created'
    | 'fund_round_paid'
    | 'fund_round_completed';
  amount?: number;
  userId?: string;
  userName: string;
  timestamp: string;
}

export type GroupDeletionStatus = 'Pending' | 'Rejected' | 'Cancelled' | 'Ready';
export type MemberVoteStatus = 'Pending' | 'Approved' | 'Rejected';

export interface GroupMemberVote {
  userId: string;
  userName: string;
  status: MemberVoteStatus;
  respondedAt?: string;
}

export interface GroupDeletionMemberRefundSnapshot {
  id?: string;
  userId: string;
  userName: string;
  totalContributed: number;
  refundAmount: number;
  percentage?: number;
  settlementStatus?: 'pending' | 'payment_pending' | 'completed';
  paymentId?: string;
  paymentReference?: string;
  recipientBankConnectionId?: string;
  completedAt?: string;
  destinationWalletId?: string;
}

export interface GroupDeletionRefundPlan {
  fundBalance: number;
  totalContributionBasis: number;
  memberRefunds: GroupDeletionMemberRefundSnapshot[];
  totalRefund: number;
  remainderGivenToUserId?: string;
  calculatedAt: string;
}

export interface PendingGroupRefundItem {
  id: string;
  deletionRequestId: string;
  groupId: string;
  groupName: string;
  userId: string;
  userName: string;
  amount: number;
  status: 'pending' | 'completed';
  createdAt: string;
  txReferenceCode: string;
}

export interface GroupDeletionRequest {
  id: string;
  groupId: string;
  requestedByUserId: string;
  requestedAt: string;
  status: GroupDeletionStatus;
  memberVotes: GroupMemberVote[];
  refundPlan?: GroupDeletionRefundPlan;
}

export interface GroupReceivingBank {
  ownerUserId: string;
  bankConnectionId: string;
  bankBrandName: string;
  maskedAccountNumber: string;
  accountHolderName?: string;
}

export interface Group {
  id: string;
  name: string;
  type: GroupCategory;
  description: string;
  currency: string;
  createdDate: string;
  ownerId: string;
  currentUserRole: GroupRole;
  members: GroupMember[];
  sharedFund: SharedFund;
  fundTransactions: FundTransaction[];
  fundRequests: FundContributionRequest[];
  fundRounds?: GroupFundRound[];
  expenses: GroupExpense[];
  activities: GroupActivity[];
  deletionRequest?: GroupDeletionRequest | null;
  receivingBank?: GroupReceivingBank | null;
}
