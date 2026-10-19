import { apiClient } from '../lib/apiClient';

export interface FriendContactDto {
  userId: string;
  fullName: string;
  spendId: string;
  phone?: string | null;
  state: 'SELF' | 'FRIENDS' | 'REQUEST_SENT' | 'REQUEST_RECEIVED' | 'NONE';
}

export interface FriendDto {
  userId: string;
  fullName: string;
  spendId: string;
  phone?: string | null;
  nickname?: string | null;
  isFavorite: boolean;
  friendsSince: string;
}

export interface FriendRequestDto {
  id: string;
  userId: string;
  fullName: string;
  spendId: string;
  message?: string | null;
  status: string; // 'Pending' | 'Accepted' | 'Rejected' | 'Cancelled'
  createdAt: string;
}

export interface SendFriendRequestInput {
  contact: string;
  message?: string | null;
}

export interface UpdateFriendPreferenceInput {
  nickname: string | null;
  isFavorite: boolean;
}

export const friendsApiService = {
  async searchFriendContact(contact: string): Promise<FriendContactDto> {
    return apiClient.get<FriendContactDto>(`/friends/search?contact=${encodeURIComponent(contact.trim())}`);
  },

  async getFriends(): Promise<FriendDto[]> {
    return apiClient.get<FriendDto[]>('/friends');
  },

  async getIncomingRequests(): Promise<FriendRequestDto[]> {
    return apiClient.get<FriendRequestDto[]>('/friends/requests/incoming');
  },

  async getOutgoingRequests(): Promise<FriendRequestDto[]> {
    return apiClient.get<FriendRequestDto[]>('/friends/requests/outgoing');
  },

  async sendFriendRequest(data: SendFriendRequestInput): Promise<FriendRequestDto> {
    return apiClient.post<FriendRequestDto>('/friends/requests', {
      contact: data.contact.trim(),
      message: data.message?.trim() || undefined,
    });
  },

  async cancelFriendRequest(requestId: string): Promise<FriendRequestDto> {
    return apiClient.delete<FriendRequestDto>(`/friends/requests/${requestId}`);
  },

  async acceptFriendRequest(requestId: string): Promise<FriendDto> {
    return apiClient.post<FriendDto>(`/friends/requests/${requestId}/accept`, {});
  },

  async rejectFriendRequest(requestId: string): Promise<FriendRequestDto> {
    return apiClient.post<FriendRequestDto>(`/friends/requests/${requestId}/reject`, {});
  },

  async unfriend(friendUserId: string): Promise<void> {
    return apiClient.delete<void>(`/friends/${friendUserId}`);
  },

  async updatePreference(friendUserId: string, data: UpdateFriendPreferenceInput): Promise<FriendDto> {
    return apiClient.patch<FriendDto>(`/friends/${friendUserId}/preference`, data);
  },
};
