import { apiClient } from '../lib/apiClient';

// ---------------------------------------------------------------------------
// Backend DTOs & Contracts (matching SpendWise.Application.Notifications)
// ---------------------------------------------------------------------------

export interface NotificationDto {
  id: string;
  type: string;
  title: string;
  message: string;
  dataJson?: string | null;
  isRead: boolean;
  createdAt: string;
  readAt?: string | null;
}

export interface NotificationPageDto {
  items: NotificationDto[];
  page: number;
  pageSize: number;
  total: number;
}

export interface NotificationSearchFilter {
  unreadOnly?: boolean;
  type?: string;
  search?: string;
  page?: number;
  pageSize?: number;
}

export interface NotificationPreferenceDto {
  id: string;
  accountKey: string;
  accountName: string;
  walletId?: string | null;
  notifyBalanceChanges: boolean;
  notifyBudgetAlerts: boolean;
  notifyLargeTransactions: boolean;
  largeTransactionThreshold: number;
  notifyMinBalanceAlert: boolean;
  minBalanceThreshold: number;
}

export interface NotificationPreferenceRequest {
  accountKey: string;
  accountName: string;
  walletId?: string | null;
  notifyBalanceChanges: boolean;
  notifyBudgetAlerts: boolean;
  notifyLargeTransactions: boolean;
  largeTransactionThreshold: number;
  notifyMinBalanceAlert: boolean;
  minBalanceThreshold: number;
}

// ---------------------------------------------------------------------------
// Notification API Service
// ---------------------------------------------------------------------------

export const notificationsApiService = {
  /**
   * GET /api/notifications
   * Fetch notifications list, optionally filtered by unreadOnly.
   */
  async getNotifications(unreadOnly?: boolean): Promise<NotificationDto[]> {
    const query = unreadOnly ? '?unreadOnly=true' : '';
    return apiClient.get<NotificationDto[]>(`/notifications${query}`);
  },

  /**
   * GET /api/notifications/search
   * Search / paginate notifications from backend.
   */
  async searchNotifications(filter: NotificationSearchFilter): Promise<NotificationPageDto> {
    const params = new URLSearchParams();
    if (filter.unreadOnly !== undefined) params.append('unreadOnly', String(filter.unreadOnly));
    if (filter.type) params.append('type', filter.type);
    if (filter.search) params.append('search', filter.search);
    if (filter.page) params.append('page', String(filter.page));
    if (filter.pageSize) params.append('pageSize', String(filter.pageSize));

    const qs = params.toString();
    return apiClient.get<NotificationPageDto>(`/notifications/search${qs ? `?${qs}` : ''}`);
  },

  /**
   * POST /api/notifications/{id}/read
   * Mark a single notification as read.
   */
  async markAsRead(id: string): Promise<void> {
    return apiClient.post<void>(`/notifications/${id}/read`);
  },

  /**
   * POST /api/notifications/read-all
   * Mark all user notifications as read.
   */
  async markAllAsRead(): Promise<void> {
    return apiClient.post<void>('/notifications/read-all');
  },

  /**
   * DELETE /api/notifications/{id}
   * Delete a single notification.
   */
  async deleteNotification(id: string): Promise<void> {
    return apiClient.delete<void>(`/notifications/${id}`);
  },

  /**
   * DELETE /api/notifications
   * Clear all user notifications. Returns count of deleted records.
   */
  async clearAllNotifications(): Promise<number> {
    return apiClient.delete<number>('/notifications');
  },

  /**
   * GET /api/notifications/preferences
   * Fetch notification preferences list for the current user.
   */
  async getPreferences(): Promise<NotificationPreferenceDto[]> {
    return apiClient.get<NotificationPreferenceDto[]>('/notifications/preferences');
  },

  /**
   * PUT /api/notifications/preferences
   * Save a single notification preference (either wallet-scoped or global "all").
   */
  async savePreference(request: NotificationPreferenceRequest): Promise<NotificationPreferenceDto> {
    return apiClient.put<NotificationPreferenceDto>('/notifications/preferences', request);
  },
};
