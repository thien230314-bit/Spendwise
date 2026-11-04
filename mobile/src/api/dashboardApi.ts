import { apiRequest } from './client';

export interface DashboardRecentTransactionDto {
  id: string;
  type: string;
  amount: number;
  description: string;
  occurredAt: string;
}

export interface DashboardCategoryDto {
  categoryId?: string;
  categoryName: string;
  amount: number;
}

export interface DashboardCashFlowDto {
  year: number;
  month: number;
  income: number;
  expense: number;
}

export interface DashboardAttentionDto {
  type: string;
  severity: string;
  title: string;
  relatedId: string;
  dueAt?: string;
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
  recentTransactions: DashboardRecentTransactionDto[];
  categoryDistribution: DashboardCategoryDto[];
  sixMonthCashFlow: DashboardCashFlowDto[];
  attentionItems: DashboardAttentionDto[];
}

export const dashboardApi = {
  getDashboard: () => apiRequest<DashboardDto>('/api/dashboard', { authenticated: true }),
};
