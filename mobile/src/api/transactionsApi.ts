import { apiRequest } from './client';

export interface TransactionDto {
  id: string;
  walletId?: string;
  walletName?: string;
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
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  pageNumber: number;
  totalPages: number;
}

export const transactionsApi = {
  getTransactions: (params?: { page?: number; pageSize?: number; type?: string; walletId?: string }) => {
    const query = new URLSearchParams();
    if (params?.page) query.set('page', params.page.toString());
    if (params?.pageSize) query.set('pageSize', params.pageSize.toString());
    if (params?.type) query.set('type', params.type);
    if (params?.walletId) query.set('walletId', params.walletId);
    const qs = query.toString() ? `?${query.toString()}` : '';
    return apiRequest<PagedResult<TransactionDto>>(`/api/transactions${qs}`, { authenticated: true });
  },

  recordExpense: (payload: {
    walletId: string;
    amount: number;
    description: string;
    categoryId?: string;
    occurredAt?: string;
    idempotencyKey?: string;
    securityPin?: string;
  }) =>
    apiRequest<TransactionDto>('/api/transactions/expense', {
      method: 'POST',
      body: JSON.stringify(payload),
      authenticated: true,
    }),

  recordIncome: (payload: {
    walletId: string;
    amount: number;
    description: string;
    categoryId?: string;
    occurredAt?: string;
    idempotencyKey?: string;
    securityPin?: string;
  }) =>
    apiRequest<TransactionDto>('/api/transactions/income', {
      method: 'POST',
      body: JSON.stringify(payload),
      authenticated: true,
    }),
};
