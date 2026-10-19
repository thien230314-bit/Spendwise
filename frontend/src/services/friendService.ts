import { FriendRequest, FriendWithMeta, FriendshipStatus, UserFriendPreferences } from '../types/friends';
import { User } from '../types';
import { friendsApiService, FriendDto, FriendRequestDto } from './friendsApiService';
import { deriveInitials } from './authMapper';
import { ApiError } from '../lib/apiError';

type FriendEventListener = () => void;

function mapFriendDto(dto: FriendDto): FriendWithMeta {
  return {
    userId: dto.userId,
    name: dto.fullName,
    spendId: dto.spendId,
    phone: dto.phone || '',
    avatarInitials: deriveInitials(dto.fullName),
    nickname: dto.nickname || undefined,
    isFavorite: dto.isFavorite,
    friendshipDate: dto.friendsSince,
  };
}

function mapIncomingRequest(dto: FriendRequestDto, currentUserId: string): FriendRequest & { senderUser?: User } {
  return {
    id: dto.id,
    requesterUserId: dto.userId,
    addresseeUserId: currentUserId,
    message: dto.message || '',
    status: (dto.status.toLowerCase() as any) || 'pending',
    createdAt: dto.createdAt,
    senderUser: {
      id: dto.userId,
      name: dto.fullName,
      email: '',
      phone: '',
      role: 'User',
      language: 'vi-VN',
      currency: 'VND',
      avatarInitials: deriveInitials(dto.fullName),
    },
  };
}

function mapOutgoingRequest(dto: FriendRequestDto, currentUserId: string): FriendRequest & { recipientUser?: User } {
  return {
    id: dto.id,
    requesterUserId: currentUserId,
    addresseeUserId: dto.userId,
    message: dto.message || '',
    status: (dto.status.toLowerCase() as any) || 'pending',
    createdAt: dto.createdAt,
    recipientUser: {
      id: dto.userId,
      name: dto.fullName,
      email: '',
      phone: '',
      role: 'User',
      language: 'vi-VN',
      currency: 'VND',
      avatarInitials: deriveInitials(dto.fullName),
    },
  };
}

class FriendService {
  private listeners: Set<FriendEventListener> = new Set();
  private cachedFriends: FriendWithMeta[] = [];
  private cachedIncomingRequests: Array<FriendRequest & { senderUser?: User }> = [];
  private cachedOutgoingRequests: Array<FriendRequest & { recipientUser?: User }> = [];
  private cachedUserId: string | null = null;
  private isRefreshing: boolean = false;
  private fetchSequence: number = 0;

  public subscribe(listener: FriendEventListener): () => void {
    this.listeners.add(listener);
    return () => {
      this.listeners.delete(listener);
    };
  }

  private notify() {
    this.listeners.forEach((l) => {
      try {
        l();
      } catch (err) {
        console.error('Error notifying friend listener', err);
      }
    });
  }

  public clearCache(): void {
    this.fetchSequence++;
    this.cachedFriends = [];
    this.cachedIncomingRequests = [];
    this.cachedOutgoingRequests = [];
    this.cachedUserId = null;
    this.notify();
  }

  public async refreshAll(currentUserId?: string): Promise<void> {
    const seq = ++this.fetchSequence;
    this.isRefreshing = true;
    try {
      const uid = currentUserId || this.cachedUserId;
      if (uid) {
        this.cachedUserId = uid;
      }
      const [friendsDto, incomingDto, outgoingDto] = await Promise.all([
        friendsApiService.getFriends(),
        friendsApiService.getIncomingRequests(),
        friendsApiService.getOutgoingRequests(),
      ]);

      // If an account switch or newer fetch occurred while in flight, ignore this response
      if (seq !== this.fetchSequence) {
        return;
      }

      this.cachedFriends = (friendsDto || []).map(mapFriendDto);
      const effectiveUid = this.cachedUserId || '';
      this.cachedIncomingRequests = (incomingDto || []).map((d) => mapIncomingRequest(d, effectiveUid));
      this.cachedOutgoingRequests = (outgoingDto || []).map((d) => mapOutgoingRequest(d, effectiveUid));
      this.notify();
    } catch (err) {
      console.warn('Failed to refresh friends from backend', err);
      throw err;
    } finally {
      if (seq === this.fetchSequence) {
        this.isRefreshing = false;
      }
    }
  }

  public getFriends(_userId?: string): FriendWithMeta[] {
    return this.cachedFriends;
  }

  public getIncomingRequests(_userId?: string): Array<FriendRequest & { senderUser?: User }> {
    return this.cachedIncomingRequests;
  }

  public getOutgoingRequests(_userId?: string): Array<FriendRequest & { recipientUser?: User }> {
    return this.cachedOutgoingRequests;
  }

  public isFriend(userIdA: string, userIdB: string): boolean {
    if (!userIdA || !userIdB || userIdA === userIdB) return false;
    return this.cachedFriends.some((f) => f.userId === userIdA || f.userId === userIdB);
  }

  public getFriendshipStatus(currentUserId: string, targetUserId: string): FriendshipStatus {
    if (!currentUserId || !targetUserId) return 'NONE';
    if (currentUserId === targetUserId) return 'SELF';

    if (this.cachedFriends.some((f) => f.userId === targetUserId)) {
      return 'FRIENDS';
    }
    if (this.cachedOutgoingRequests.some((r) => r.addresseeUserId === targetUserId)) {
      return 'REQUEST_SENT';
    }
    if (this.cachedIncomingRequests.some((r) => r.requesterUserId === targetUserId)) {
      return 'REQUEST_RECEIVED';
    }
    return 'NONE';
  }

  public getRequestById(requestId: string): FriendRequest | undefined {
    const inc = this.cachedIncomingRequests.find((r) => r.id === requestId);
    if (inc) return inc;
    return this.cachedOutgoingRequests.find((r) => r.id === requestId);
  }

  public async sendFriendRequestAsync(
    contact: string,
    message?: string
  ): Promise<{ success: boolean; request?: FriendRequestDto; error?: string }> {
    try {
      const res = await friendsApiService.sendFriendRequest({ contact, message });
      await this.refreshAll();
      return { success: true, request: res };
    } catch (err) {
      const apiErr = err as ApiError;
      return { success: false, error: apiErr.detail || apiErr.title || 'Lỗi khi gửi lời mời kết bạn.' };
    }
  }

  public async acceptFriendRequestAsync(
    requestId: string
  ): Promise<{ success: boolean; friend?: FriendDto; error?: string }> {
    try {
      const friend = await friendsApiService.acceptFriendRequest(requestId);
      await this.refreshAll();
      return { success: true, friend };
    } catch (err) {
      const apiErr = err as ApiError;
      return { success: false, error: apiErr.detail || apiErr.title || 'Lỗi khi chấp nhận kết bạn.' };
    }
  }

  public async rejectFriendRequestAsync(
    requestId: string
  ): Promise<{ success: boolean; error?: string }> {
    try {
      await friendsApiService.rejectFriendRequest(requestId);
      await this.refreshAll();
      return { success: true };
    } catch (err) {
      const apiErr = err as ApiError;
      return { success: false, error: apiErr.detail || apiErr.title || 'Lỗi khi từ chối lời mời kết bạn.' };
    }
  }

  public async cancelFriendRequestAsync(
    requestId: string
  ): Promise<{ success: boolean; error?: string }> {
    try {
      await friendsApiService.cancelFriendRequest(requestId);
      await this.refreshAll();
      return { success: true };
    } catch (err) {
      const apiErr = err as ApiError;
      return { success: false, error: apiErr.detail || apiErr.title || 'Lỗi khi hủy lời mời kết bạn.' };
    }
  }

  public async unfriendAsync(
    friendUserId: string
  ): Promise<{ success: boolean; error?: string }> {
    try {
      await friendsApiService.unfriend(friendUserId);
      await this.refreshAll();
      return { success: true };
    } catch (err) {
      const apiErr = err as ApiError;
      return { success: false, error: apiErr.detail || apiErr.title || 'Lỗi khi hủy kết bạn.' };
    }
  }

  public async updatePreferenceAsync(
    friendUserId: string,
    data: { nickname: string | null; isFavorite: boolean }
  ): Promise<{ success: boolean; friend?: FriendDto; error?: string }> {
    try {
      const friend = await friendsApiService.updatePreference(friendUserId, data);
      await this.refreshAll();
      return { success: true, friend };
    } catch (err) {
      const apiErr = err as ApiError;
      return { success: false, error: apiErr.detail || apiErr.title || 'Lỗi khi cập nhật thông tin bạn bè.' };
    }
  }

  public async toggleFavoriteAsync(
    currentUserId: string,
    friendUserId: string
  ): Promise<{ success: boolean; isFavorite?: boolean; error?: string }> {
    const friend = this.cachedFriends.find((f) => f.userId === friendUserId);
    if (!friend) {
      return { success: false, error: 'Friend not found' };
    }
    const nextState = !friend.isFavorite;
    try {
      await friendsApiService.updatePreference(friendUserId, {
        nickname: friend.nickname || null,
        isFavorite: nextState,
      });
      await this.refreshAll(currentUserId);
      return { success: true, isFavorite: nextState };
    } catch (err) {
      const apiErr = err as ApiError;
      return {
        success: false,
        isFavorite: friend.isFavorite,
        error: apiErr.detail || apiErr.title || 'Lỗi khi cập nhật yêu thích.',
      };
    }
  }

  public getPreferences(_userId: string): UserFriendPreferences {
    const map: UserFriendPreferences = {};
    this.cachedFriends.forEach((f) => {
      map[f.userId] = { isFavorite: f.isFavorite, nickname: f.nickname };
    });
    return map;
  }

  public savePreferences(_userId: string, _prefs: UserFriendPreferences): void {
    // Backend is authoritative
  }
}

export const friendService = new FriendService();
