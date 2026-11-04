import { apiRequest } from './client';

export interface WalletDto {
  id: string;
  name: string;
  type: string;
  balance?: number;
  availableBalance: number;
  currency: string;
  isDefault: boolean;
  status: string;
}

export interface CreateWalletRequest {
  name: string;
  type: string;
  isDefault?: boolean;
}

export const walletsApi = {
  getWallets: () => apiRequest<WalletDto[]>('/api/wallets', { authenticated: true }),

  getWallet: (walletId: string) =>
    apiRequest<WalletDto>(`/api/wallets/${walletId}`, { authenticated: true }),

  createWallet: (request: CreateWalletRequest) =>
    apiRequest<WalletDto>('/api/wallets', {
      method: 'POST',
      body: JSON.stringify(request),
      authenticated: true,
    }),

  closeWallet: (walletId: string) =>
    apiRequest<void>(`/api/wallets/${walletId}`, {
      method: 'DELETE',
      authenticated: true,
    }),
};
