import { apiClient } from '../lib/apiClient';
import { TransactionDto } from './financialApiService';

// ==========================================
// DTOs matching SpendWise.Application.Planning
// ==========================================

export interface BudgetDto {
  id: string;
  categoryId: string;
  categoryName: string;
  year: number;
  month: number;
  limitAmount: number;
  spentAmount: number;
  remainingAmount: number;
  percentage: number;
  alertLevel: string; // 'Normal' | 'Warning' | 'Exceeded'
  alertsEnabled: boolean;
  rollover: boolean;
}

export interface CreateBudgetRequest {
  categoryId: string;
  year: number;
  month: number;
  limitAmount: number;
  alertsEnabled?: boolean;
  rollover?: boolean;
  securityPin?: string | null;
}

export interface UpdateBudgetRequest {
  limitAmount: number;
  alertsEnabled?: boolean;
  rollover?: boolean;
  securityPin?: string | null;
}

export interface DeleteBudgetRequest {
  securityPin: string;
}

export interface GoalMovementDto {
  id: string;
  amount: number;
  date: string;
  walletId: string;
  walletName?: string | null;
  type: string; // 'Contribution' | 'Withdrawal'
}

export interface GoalDto {
  id: string;
  name: string;
  targetAmount: number;
  currentAllocatedAmount: number;
  targetDate?: string | null;
  status: string;
  percentage: number;
  categoryId?: string | null;
  categoryName?: string | null;
  iconName?: string | null;
  colorHex?: string | null;
  movements: GoalMovementDto[];
}

export interface CreateGoalRequest {
  name: string;
  targetAmount: number;
  targetDate?: string | null;
  categoryId?: string | null;
  iconName?: string | null;
  colorHex?: string | null;
  initialAmount?: number;
  walletId?: string | null;
  idempotencyKey?: string | null;
  securityPin?: string | null;
}

export interface UpdateGoalRequest {
  name: string;
  targetAmount: number;
  targetDate?: string | null;
  categoryId?: string | null;
  iconName?: string | null;
  colorHex?: string | null;
  securityPin?: string | null;
}

export interface DeleteGoalRequest {
  securityPin: string;
}

export interface GoalMovementRequest {
  walletId: string;
  amount: number;
  idempotencyKey: string;
  securityPin?: string | null;
}

export interface RecurringOccurrenceDto {
  id: string;
  scheduledFor: string;
  status: string; // 'Scheduled' | 'Due' | 'Overdue' | 'Recorded' | 'Skipped'
  recordedTransactionId?: string | null;
  recordedAt?: string | null;
}

export interface RecurringDto {
  id: string;
  walletId: string;
  categoryId: string;
  kind: string; // 'Income' | 'Expense'
  amount: number;
  title: string;
  description: string;
  frequency: string; // 'Daily' | 'Weekly' | 'Monthly' | 'Yearly'
  nextRunAt: string;
  status: string; // 'Active' | 'Paused'
  isSubscription: boolean;
  lastRunAt?: string | null;
  anchorDay: number;
  anchorMonth: number;
  occurrences?: RecurringOccurrenceDto[] | null;
}

export interface CreateRecurringRequest {
  walletId: string;
  categoryId: string;
  kind: string;
  amount: number;
  description: string;
  frequency: string;
  nextRunAt: string;
  isSubscription?: boolean;
  title?: string | null;
}

export interface UpdateRecurringRequest {
  walletId: string;
  categoryId: string;
  kind: string;
  amount: number;
  description: string;
  frequency: string;
  nextRunAt: string;
  isSubscription?: boolean;
  title?: string | null;
}

export interface RecordRecurringOccurrenceRequest {
  securityPin: string;
  evidenceIds: string[];
  idempotencyKey: string;
}

export interface TransactionRuleDto {
  id: string;
  descriptionPattern: string;
  targetCategoryId: string;
  targetCategoryName: string;
  priority: number;
  isActive: boolean;
}

export interface CreateTransactionRuleRequest {
  descriptionPattern: string;
  targetCategoryId: string;
  priority?: number;
}

export interface UpdateTransactionRuleRequest {
  descriptionPattern: string;
  targetCategoryId: string;
  priority?: number;
  isActive?: boolean;
}

// ==========================================
// Planning API Service
// ==========================================

export const planningApiService = {
  // --- Budgets ---
  getBudgets(year: number, month: number): Promise<BudgetDto[]> {
    return apiClient.get<BudgetDto[]>(`/budgets?year=${year}&month=${month}`);
  },

  getBudget(id: string): Promise<BudgetDto> {
    return apiClient.get<BudgetDto>(`/budgets/${id}`);
  },

  createBudget(data: CreateBudgetRequest): Promise<BudgetDto> {
    return apiClient.post<BudgetDto>('/budgets', data);
  },

  updateBudget(id: string, data: UpdateBudgetRequest): Promise<BudgetDto> {
    return apiClient.put<BudgetDto>(`/budgets/${id}`, data);
  },

  deleteBudget(id: string, request: DeleteBudgetRequest): Promise<void> {
    return apiClient.deleteWithBody<void>(`/budgets/${id}`, request);
  },

  // --- Goals ---
  getGoals(): Promise<GoalDto[]> {
    return apiClient.get<GoalDto[]>('/goals');
  },

  getGoal(id: string): Promise<GoalDto> {
    return apiClient.get<GoalDto>(`/goals/${id}`);
  },

  createGoal(data: CreateGoalRequest): Promise<GoalDto> {
    return apiClient.post<GoalDto>('/goals', data);
  },

  updateGoal(id: string, data: UpdateGoalRequest): Promise<GoalDto> {
    return apiClient.put<GoalDto>(`/goals/${id}`, data);
  },

  deleteGoal(id: string, request: DeleteGoalRequest): Promise<void> {
    return apiClient.deleteWithBody<void>(`/goals/${id}`, request);
  },

  contributeGoal(id: string, data: GoalMovementRequest): Promise<GoalDto> {
    return apiClient.post<GoalDto>(`/goals/${id}/contributions`, data);
  },

  withdrawGoal(id: string, data: GoalMovementRequest): Promise<GoalDto> {
    return apiClient.post<GoalDto>(`/goals/${id}/withdrawals`, data);
  },

  // --- Recurring Transactions ---
  getRecurringTransactions(): Promise<RecurringDto[]> {
    return apiClient.get<RecurringDto[]>('/recurring-transactions');
  },

  getRecurringTransaction(id: string): Promise<RecurringDto> {
    return apiClient.get<RecurringDto>(`/recurring-transactions/${id}`);
  },

  createRecurring(data: CreateRecurringRequest): Promise<RecurringDto> {
    return apiClient.post<RecurringDto>('/recurring-transactions', data);
  },

  updateRecurring(id: string, data: UpdateRecurringRequest): Promise<RecurringDto> {
    return apiClient.put<RecurringDto>(`/recurring-transactions/${id}`, data);
  },

  deleteRecurring(id: string): Promise<void> {
    return apiClient.delete<void>(`/recurring-transactions/${id}`);
  },

  pauseRecurring(id: string): Promise<RecurringDto> {
    return apiClient.post<RecurringDto>(`/recurring-transactions/${id}/pause`);
  },

  resumeRecurring(id: string): Promise<RecurringDto> {
    return apiClient.post<RecurringDto>(`/recurring-transactions/${id}/resume`);
  },

  recordRecurringOccurrence(
    id: string,
    occurrenceId: string,
    request: RecordRecurringOccurrenceRequest
  ): Promise<TransactionDto> {
    return apiClient.post<TransactionDto>(
      `/recurring-transactions/${id}/occurrences/${occurrenceId}/record`,
      request
    );
  },

  // --- Transaction Rules ---
  getTransactionRules(): Promise<TransactionRuleDto[]> {
    return apiClient.get<TransactionRuleDto[]>('/transaction-rules');
  },

  getTransactionRule(id: string): Promise<TransactionRuleDto> {
    return apiClient.get<TransactionRuleDto>(`/transaction-rules/${id}`);
  },

  createTransactionRule(data: CreateTransactionRuleRequest): Promise<TransactionRuleDto> {
    return apiClient.post<TransactionRuleDto>('/transaction-rules', data);
  },

  updateTransactionRule(id: string, data: UpdateTransactionRuleRequest): Promise<TransactionRuleDto> {
    return apiClient.put<TransactionRuleDto>(`/transaction-rules/${id}`, data);
  },

  deleteTransactionRule(id: string): Promise<void> {
    return apiClient.delete<void>(`/transaction-rules/${id}`);
  },
};
