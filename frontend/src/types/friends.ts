export type FriendRequestStatus = 'pending' | 'accepted' | 'rejected';

export interface FriendRequest {
  id: string;
  requesterUserId: string;
  addresseeUserId: string;
  message: string;
  status: FriendRequestStatus;
  createdAt: string;
  respondedAt?: string;
}

export interface UserFriendPreference {
  isFavorite?: boolean;
  nickname?: string;
}

export type UserFriendPreferences = Record<string, UserFriendPreference>;

export interface FriendWithMeta {
  userId: string;
  name: string; // Canonical User.name
  spendId?: string; // SpendWise user public tag
  email?: string;
  phone: string;
  avatarInitials: string;
  nickname?: string; // Private nickname set by current user
  isFavorite: boolean; // Private favorite set by current user
  friendshipDate?: string;
  requestId?: string;
}

export type FriendshipStatus =
  | 'SELF'
  | 'FRIENDS'
  | 'REQUEST_SENT'
  | 'REQUEST_RECEIVED'
  | 'NONE';
