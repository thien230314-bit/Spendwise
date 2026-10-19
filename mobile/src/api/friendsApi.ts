import { apiRequest } from './client';

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

export const friendsApi = {
  async searchFriendContact(contact: string): Promise<FriendContactDto> {
    return apiRequest<FriendContactDto>(`/friends/search?contact=${encodeURIComponent(contact.trim())}`, {
      authenticated: true,
    });
  },

  async getFriends(): Promise<FriendDto[]> {
    return apiRequest<FriendDto[]>('/friends', {
      authenticated: true,
    });
  },

  async getIncomingRequests(): Promise<FriendRequestDto[]> {
    return apiRequest<FriendRequestDto[]>('/friends/requests/incoming', {
      authenticated: true,
    });
  },

  async getOutgoingRequests(): Promise<FriendRequestDto[]> {
    return apiRequest<FriendRequestDto[]>('/friends/requests/outgoing', {
      authenticated: true,
    });
  },

  async sendFriendRequest(data: SendFriendRequestInput): Promise<FriendRequestDto> {
    return apiRequest<FriendRequestDto>('/friends/requests', {
      method: 'POST',
      body: JSON.stringify({
        contact: data.contact.trim(),
        message: data.message?.trim() || undefined,
      }),
      authenticated: true,
    });
  },

  async acceptFriendRequest(requestId: string): Promise<FriendDto> {
    return apiRequest<FriendDto>(`/friends/requests/${requestId}/accept`, {
      method: 'POST',
      body: JSON.stringify({}),
      authenticated: true,
    });
  },

  async rejectFriendRequest(requestId: string): Promise<FriendRequestDto> {
    return apiRequest<FriendRequestDto>(`/friends/requests/${requestId}/reject`, {
      method: 'POST',
      body: JSON.stringify({}),
      authenticated: true,
    });
  },

  async cancelFriendRequest(requestId: string): Promise<FriendRequestDto> {
    return apiRequest<FriendRequestDto>(`/friends/requests/${requestId}`, {
      method: 'DELETE',
      authenticated: true,
    });
  },

  async unfriend(friendUserId: string): Promise<void> {
    return apiRequest<void>(`/friends/${friendUserId}`, {
      method: 'DELETE',
      authenticated: true,
    });
  },
};
