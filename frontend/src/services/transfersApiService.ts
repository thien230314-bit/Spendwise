import { apiClient } from '../lib/apiClient';

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface TransferRecipientDto {
  userId?: string | null;
  fullName: string;
  bankBrand: string;
  accountHolder: string;
  maskedAccount: string;
  phone?: string | null;
}

export interface CreateTransferRequest {
  recipientUserId: string;
  amount: number;
  note: string;
  idempotencyKey: string;
}

export type BankDirectPaymentStatus =
  | 'Pending'
  | 'AwaitingReconciliation'
  | 'Completed'
  | 'Failed'
  | 'Cancelled';

export interface BankDirectPaymentDto {
  id: string;
  senderUserId: string;
  recipientUserId?: string | null;
  recipientDisplayName: string;
  recipientPhone?: string | null;
  recipientBankConnectionId: string;
  bankBrand: string;
  accountHolder: string;
  maskedAccount: string;
  amount: number;
  currency: string;
  note: string;
  paymentReference: string;
  contextType?: string;
  groupId?: string;
  status: BankDirectPaymentStatus | string;
  createdAt: string;
  senderConfirmedAt?: string;
  completedAt?: string;
  matchedBankTransactionId?: string;
}

export interface ConfirmTransferRequest {
  securityPin: string;
}

export const transfersApiService = {
  /**
   * GET /api/transfers/recipients?phone={phone}
   */
  async getRecipientByPhone(phone: string): Promise<TransferRecipientDto> {
    return apiClient.get<TransferRecipientDto>(`/transfers/recipients?phone=${encodeURIComponent(phone)}`);
  },

  /**
   * POST /api/transfers
   */
  async createTransfer(data: CreateTransferRequest): Promise<BankDirectPaymentDto> {
    return apiClient.post<BankDirectPaymentDto>('/transfers', data);
  },

  /**
   * GET /api/transfers?page=1&pageSize=20
   */
  async getTransfers(params?: { page?: number; pageSize?: number }): Promise<PagedResult<BankDirectPaymentDto>> {
    const page = params?.page ?? 1;
    const pageSize = params?.pageSize ?? 20;
    return apiClient.get<PagedResult<BankDirectPaymentDto>>(`/transfers?page=${page}&pageSize=${pageSize}`);
  },

  /**
   * GET /api/transfers/{transferId}
   */
  async getTransferById(transferId: string): Promise<BankDirectPaymentDto> {
    return apiClient.get<BankDirectPaymentDto>(`/transfers/${transferId}`);
  },

  /**
   * POST /api/transfers/{paymentId}/confirm
   */
  async confirmTransfer(paymentId: string, data: ConfirmTransferRequest): Promise<BankDirectPaymentDto> {
    return apiClient.post<BankDirectPaymentDto>(`/transfers/${paymentId}/confirm`, data);
  },
};
