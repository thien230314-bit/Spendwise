import { apiRequest } from './client';

export interface TransferDto {
  id: string;
  senderUserId: string;
  senderName: string;
  recipientUserId: string;
  recipientName: string;
  sourceWalletId: string;
  destinationWalletId: string;
  amount: number;
  currency: string;
  description: string;
  status: string;
  ledgerTransactionId?: string;
  completedAt?: string;
  replayed?: boolean;
}

export const transfersApi = {
  transferInternal: (payload: {
    sourceWalletId: string;
    destinationWalletId: string;
    amount: number;
    description: string;
    idempotencyKey: string;
    securityPin: string;
  }) =>
    apiRequest<TransferDto>('/api/transfers/internal', {
      method: 'POST',
      body: JSON.stringify(payload),
      authenticated: true,
    }),

  transferP2P: (payload: {
    sourceWalletId: string;
    recipientUserId: string;
    amount: number;
    description: string;
    idempotencyKey: string;
    securityPin: string;
  }) =>
    apiRequest<TransferDto>('/api/transfers/p2p', {
      method: 'POST',
      body: JSON.stringify(payload),
      authenticated: true,
    }),
};
