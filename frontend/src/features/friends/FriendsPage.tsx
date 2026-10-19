import React, { useState, useEffect, useMemo, useRef } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import {
  Users,
  UserPlus,
  UserCheck,
  Star,
  Search,
  Send,
  X,
  Check,
  Tag,
  Clock,
  UserMinus,
  AlertCircle,
  Phone,
  Mail,
  Loader2,
  RefreshCw,
} from 'lucide-react';
import { useI18n } from '../../i18n/I18nContext';
import { useAuth } from '../../providers/AuthProvider';
import { friendService } from '../../services/friendService';
import { friendsApiService, FriendContactDto } from '../../services/friendsApiService';
import { deriveInitials } from '../../services/authMapper';
import { FriendWithMeta, FriendRequest } from '../../types/friends';
import { User } from '../../types';
import { AddFriendModal, AddFriendTarget } from './modals/AddFriendModal';
import { EditNicknameModal } from './modals/EditNicknameModal';
import { getLocalizedErrorMessage } from '../../utils/localizationHelpers';
import { ApiError } from '../../lib/apiError';
import { cn } from '../../utils/cn';
import { FriendsSkeleton } from '../../components/skeletons/FriendsSkeleton';

export const FriendsPage: React.FC = () => {
  const { t, locale } = useI18n();
  const { currentUser } = useAuth();
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();

  const activeTabParam = searchParams.get('tab') || 'friends';
  const [activeTab, setActiveTab] = useState<'friends' | 'received' | 'sent'>(
    activeTabParam === 'received' ? 'received' : activeTabParam === 'sent' ? 'sent' : 'friends'
  );

  const [friends, setFriends] = useState<FriendWithMeta[]>([]);
  const [incomingRequests, setIncomingRequests] = useState<
    Array<FriendRequest & { senderUser?: User }>
  >([]);
  const [outgoingRequests, setOutgoingRequests] = useState<
    Array<FriendRequest & { recipientUser?: User }>
  >([]);
  const [isLoading, setIsLoading] = useState(false);
  const [actionInProgress, setActionInProgress] = useState<string | null>(null);

  // Search & Filter in friends list
  const [searchQuery, setSearchQuery] = useState('');
  const [showFavoritesOnly, setShowFavoritesOnly] = useState(false);

  // Modals state
  const [isAddModalOpen, setIsAddModalOpen] = useState(false);
  const [selectedUserForAdd, setSelectedUserForAdd] = useState<AddFriendTarget | null>(null);

  const [editingFriend, setEditingFriend] = useState<FriendWithMeta | null>(null);
  const [unfriendConfirmFriend, setUnfriendConfirmFriend] = useState<FriendWithMeta | null>(null);

  // Direct quick search inside Friends page for adding new friend
  const remoteInputRef = useRef<HTMLInputElement>(null);
  const [isQuickSearchOpen, setIsQuickSearchOpen] = useState(false);
  const [newFriendIdentifier, setNewFriendIdentifier] = useState('');
  const [isSearchingContact, setIsSearchingContact] = useState(false);
  const [searchContactResult, setSearchContactResult] = useState<FriendContactDto | null>(null);
  const [searchContactError, setSearchContactError] = useState<string | null>(null);

  // Toast feedback
  const [toastMessage, setToastMessage] = useState<string | null>(null);

  const showToast = (msg: string) => {
    setToastMessage(msg);
    setTimeout(() => {
      setToastMessage(null);
    }, 3000);
  };

  const syncFromService = () => {
    setFriends(friendService.getFriends());
    setIncomingRequests(friendService.getIncomingRequests());
    setOutgoingRequests(friendService.getOutgoingRequests());
  };

  useEffect(() => {
    if (!currentUser?.id) {
      friendService.clearCache();
      setFriends([]);
      setIncomingRequests([]);
      setOutgoingRequests([]);
      return;
    }

    setIsLoading(true);
    friendService
      .refreshAll(currentUser.id)
      .catch((err) => console.warn('Failed to load friends', err))
      .finally(() => {
        setIsLoading(false);
        syncFromService();
      });

    const unsubscribe = friendService.subscribe(() => {
      syncFromService();
    });
    return () => unsubscribe();
  }, [currentUser?.id]);

  useEffect(() => {
    if (activeTabParam === 'received' && activeTab !== 'received') {
      setActiveTab('received');
    } else if (activeTabParam === 'sent' && activeTab !== 'sent') {
      setActiveTab('sent');
    } else if (activeTabParam === 'friends' && activeTab !== 'friends') {
      setActiveTab('friends');
    }
  }, [activeTabParam]);

  const handleTabChange = (tab: 'friends' | 'received' | 'sent') => {
    setActiveTab(tab);
    setSearchParams({ tab });
  };

  const handleToggleFavorite = async (friendUserId: string) => {
    if (!currentUser) return;
    setActionInProgress(friendUserId);
    try {
      const res = await friendService.toggleFavoriteAsync(currentUser.id, friendUserId);
      if (res.success) {
        showToast(
          res.isFavorite
            ? locale === 'vi-VN'
              ? 'Đã thêm vào danh sách Yêu thích.'
              : 'Added to Favorites.'
            : locale === 'vi-VN'
            ? 'Đã xóa khỏi danh sách Yêu thích.'
            : 'Removed from Favorites.'
        );
      } else {
        showToast(getLocalizedErrorMessage(res.error, locale, t));
      }
    } finally {
      setActionInProgress(null);
    }
  };

  const handleAcceptRequest = async (reqId: string, senderName?: string) => {
    if (!currentUser) return;
    setActionInProgress(reqId);
    try {
      const res = await friendService.acceptFriendRequestAsync(reqId);
      if (res.success) {
        showToast(
          locale === 'vi-VN'
            ? `Đã chấp nhận kết bạn với ${senderName || 'bạn mới'}.`
            : `Accepted friend request from ${senderName || 'friend'}.`
        );
      } else {
        showToast(getLocalizedErrorMessage(res.error, locale, t));
      }
    } finally {
      setActionInProgress(null);
    }
  };

  const handleRejectRequest = async (reqId: string) => {
    if (!currentUser) return;
    setActionInProgress(reqId);
    try {
      const res = await friendService.rejectFriendRequestAsync(reqId);
      if (res.success) {
        showToast(locale === 'vi-VN' ? 'Đã từ chối lời mời kết bạn.' : 'Rejected friend request.');
      } else {
        showToast(getLocalizedErrorMessage(res.error, locale, t));
      }
    } finally {
      setActionInProgress(null);
    }
  };

  const handleCancelOutgoing = async (reqId: string) => {
    if (!currentUser) return;
    setActionInProgress(reqId);
    try {
      const res = await friendService.cancelFriendRequestAsync(reqId);
      if (res.success) {
        showToast(locale === 'vi-VN' ? 'Đã hủy lời mời kết bạn.' : 'Cancelled friend request.');
      } else {
        showToast(getLocalizedErrorMessage(res.error, locale, t));
      }
    } finally {
      setActionInProgress(null);
    }
  };

  const handleConfirmUnfriend = async () => {
    if (!currentUser || !unfriendConfirmFriend) return;
    setActionInProgress(unfriendConfirmFriend.userId);
    try {
      const res = await friendService.unfriendAsync(unfriendConfirmFriend.userId);
      if (res.success) {
        showToast(
          locale === 'vi-VN'
            ? `Đã hủy kết bạn với ${unfriendConfirmFriend.name}.`
            : `Unfriended ${unfriendConfirmFriend.name}.`
        );
      } else {
        showToast(getLocalizedErrorMessage(res.error, locale, t));
      }
    } finally {
      setActionInProgress(null);
      setUnfriendConfirmFriend(null);
    }
  };

  const handleTransferToFriend = (friend: FriendWithMeta) => {
    if (friend.phone) {
      navigate(`/transfer?recipientPhone=${encodeURIComponent(friend.phone)}`);
    } else {
      navigate('/transfer');
    }
  };

  // Filtered friends list
  const filteredFriends = useMemo(() => {
    return friends.filter((f) => {
      if (showFavoritesOnly && !f.isFavorite) return false;
      if (!searchQuery.trim()) return true;
      const q = searchQuery.toLowerCase().trim();
      const matchName = f.name.toLowerCase().includes(q);
      const matchNick = f.nickname?.toLowerCase().includes(q);
      const matchSpendId = f.spendId?.toLowerCase().includes(q);
      const matchPhone = f.phone?.includes(q);
      return matchName || matchNick || matchSpendId || matchPhone;
    });
  }, [friends, searchQuery, showFavoritesOnly]);

  const handleSearchContactSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    const trimmed = newFriendIdentifier.trim();
    if (!trimmed) return;

    setIsSearchingContact(true);
    setSearchContactResult(null);
    setSearchContactError(null);

    try {
      const res = await friendsApiService.searchFriendContact(trimmed);
      setSearchContactResult(res);
    } catch (err: unknown) {
      const apiErr = err as ApiError;
      if (
        apiErr.code === 'contact_not_found' ||
        apiErr.title?.toLowerCase().includes('not found') ||
        apiErr.detail?.toLowerCase().includes('not found')
      ) {
        setSearchContactError(
          locale === 'vi-VN'
            ? 'Không tìm thấy người dùng phù hợp với email hoặc số điện thoại này.'
            : 'Eligible contact not found.'
        );
      } else if (apiErr.code === 'friend_self_forbidden') {
        setSearchContactError(
          locale === 'vi-VN'
            ? 'Đây là tài khoản của bạn. Không thể kết bạn với chính mình.'
            : 'You cannot add yourself as a friend.'
        );
      } else {
        setSearchContactError(
          getLocalizedErrorMessage(apiErr.detail || apiErr.title, locale, t)
        );
      }
    } finally {
      setIsSearchingContact(false);
    }
  };

  const handleFocusRemoteLookup = () => {
    if (activeTab !== 'friends') {
      handleTabChange('friends');
    }
    setTimeout(() => {
      remoteInputRef.current?.focus();
      remoteInputRef.current?.scrollIntoView({ behavior: 'smooth', block: 'center' });
    }, 50);
  };

  return (
    <div className="space-y-6 max-w-6xl mx-auto pb-12 animate-in fade-in-50 duration-200">
      {/* Toast */}
      {toastMessage && (
        <div className="fixed bottom-6 right-6 z-50 flex items-center gap-2 px-4 py-2.5 rounded-xl bg-slate-900 text-white dark:bg-white dark:text-slate-900 text-xs font-semibold shadow-xl animate-in slide-in-from-bottom-3">
          <Check className="h-4 w-4 text-emerald-400" />
          <span>{toastMessage}</span>
        </div>
      )}

      {/* Page Header */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 border-b border-slate-200 dark:border-slate-800 pb-5">
        <div>
          <div className="flex items-center gap-2.5">
            <div className="p-2 rounded-xl bg-emerald-50 dark:bg-emerald-950/80 text-emerald-600 dark:text-emerald-400">
              <Users className="h-6 w-6" />
            </div>
            <div>
              <h1 className="text-xl sm:text-2xl font-bold tracking-tight text-slate-900 dark:text-white">
                {locale === 'vi-VN' ? 'Danh bạ & Bạn bè' : 'Friends & Contacts'}
              </h1>
              <p className="text-xs sm:text-sm text-slate-500 dark:text-slate-400 mt-0.5">
                {locale === 'vi-VN'
                  ? 'Quản lý bạn bè, chuyển tiền trực tiếp và lời mời tham gia nhóm quỹ.'
                  : 'Manage friends, transfer directly, and invite friends to shared groups.'}
              </p>
            </div>
          </div>
        </div>

        {/* Quick Add Friend Button (Focuses inline lookup on desktop) */}
        <button
          type="button"
          onClick={handleFocusRemoteLookup}
          className="flex items-center gap-2 px-4 py-2.5 rounded-xl bg-emerald-600 hover:bg-emerald-700 text-white font-semibold text-xs shadow-xs transition-colors cursor-pointer self-start sm:self-center"
        >
          <UserPlus className="h-4 w-4" />
          <span>{locale === 'vi-VN' ? 'Kết bạn mới' : 'Add Friend'}</span>
        </button>
      </div>

      {/* Tabs */}
      <div className="flex items-center gap-2 border-b border-slate-200 dark:border-slate-800 pb-2">
        <button
          type="button"
          onClick={() => handleTabChange('friends')}
          className={cn(
            'flex items-center gap-2 px-3.5 py-2 text-xs sm:text-sm font-semibold rounded-lg transition-colors cursor-pointer',
            activeTab === 'friends'
              ? 'bg-emerald-50 text-emerald-700 dark:bg-emerald-950/60 dark:text-emerald-300 border border-emerald-200 dark:border-emerald-800/60'
              : 'text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-slate-200 hover:bg-slate-50 dark:hover:bg-slate-800'
          )}
        >
          <UserCheck className="h-4 w-4" />
          <span>{locale === 'vi-VN' ? 'Bạn bè' : 'Friends'}</span>
          <span className="ml-1 px-1.5 py-0.5 rounded-full bg-slate-200 dark:bg-slate-700 text-slate-700 dark:text-slate-300 text-[10px] font-mono">
            {friends.length}
          </span>
        </button>

        <button
          type="button"
          onClick={() => handleTabChange('received')}
          className={cn(
            'flex items-center gap-2 px-3.5 py-2 text-xs sm:text-sm font-semibold rounded-lg transition-colors cursor-pointer relative',
            activeTab === 'received'
              ? 'bg-emerald-50 text-emerald-700 dark:bg-emerald-950/60 dark:text-emerald-300 border border-emerald-200 dark:border-emerald-800/60'
              : 'text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-slate-200 hover:bg-slate-50 dark:hover:bg-slate-800'
          )}
        >
          <Clock className="h-4 w-4" />
          <span>{locale === 'vi-VN' ? 'Lời mời nhận được' : 'Received Requests'}</span>
          {incomingRequests.length > 0 ? (
            <span className="ml-1 px-1.5 py-0.5 rounded-full bg-rose-500 text-white text-[10px] font-bold">
              {incomingRequests.length}
            </span>
          ) : (
            <span className="ml-1 px-1.5 py-0.5 rounded-full bg-slate-200 dark:bg-slate-700 text-slate-700 dark:text-slate-300 text-[10px] font-mono">
              0
            </span>
          )}
        </button>

        <button
          type="button"
          onClick={() => handleTabChange('sent')}
          className={cn(
            'flex items-center gap-2 px-3.5 py-2 text-xs sm:text-sm font-semibold rounded-lg transition-colors cursor-pointer',
            activeTab === 'sent'
              ? 'bg-emerald-50 text-emerald-700 dark:bg-emerald-950/60 dark:text-emerald-300 border border-emerald-200 dark:border-emerald-800/60'
              : 'text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-slate-200 hover:bg-slate-50 dark:hover:bg-slate-800'
          )}
        >
          <Send className="h-4 w-4" />
          <span>{locale === 'vi-VN' ? 'Đã gửi' : 'Sent Requests'}</span>
          <span className="ml-1 px-1.5 py-0.5 rounded-full bg-slate-200 dark:bg-slate-700 text-slate-700 dark:text-slate-300 text-[10px] font-mono">
            {outgoingRequests.length}
          </span>
        </button>
      </div>

      {isLoading && (
        <div className="pt-2">
          <FriendsSkeleton />
        </div>
      )}

      {/* TAB 1: BẠN BÈ (ACCEPTED) */}
      {!isLoading && activeTab === 'friends' && (
        <div className="space-y-6">
          {/* Controls: Split Search & Discovery Area */}
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            {/* A: Local Friend Filter */}
            <div
              className={cn(
                'p-4 rounded-2xl border border-slate-200 dark:border-slate-800 bg-slate-50/60 dark:bg-slate-900/40 transition-opacity',
                friends.length === 0 && 'opacity-70'
              )}
            >
              <div className="flex items-center justify-between mb-2">
                <span className="text-[11px] font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400">
                  {locale === 'vi-VN' ? 'Lọc danh bạ hiện có' : 'Filter current friends'}
                </span>
                <div className="flex items-center gap-2">
                  <span className="text-[10px] text-slate-400 font-mono">
                    {locale === 'vi-VN' ? `${friends.length} liên hệ` : `${friends.length} contacts`}
                  </span>
                  {friends.length > 0 && (
                    <button
                      type="button"
                      onClick={() => setShowFavoritesOnly((prev) => !prev)}
                      className={cn(
                        'flex items-center gap-1 px-2 py-0.5 rounded-md text-[10px] font-semibold border transition-colors cursor-pointer',
                        showFavoritesOnly
                          ? 'bg-amber-50 dark:bg-amber-950/60 border-amber-300 dark:border-amber-700 text-amber-800 dark:text-amber-300'
                          : 'bg-white dark:bg-slate-900 border-slate-200 dark:border-slate-700 text-slate-600 dark:text-slate-300'
                      )}
                    >
                      <Star
                        className={cn(
                          'h-3 w-3',
                          showFavoritesOnly ? 'text-amber-500 fill-amber-500' : 'text-slate-400'
                        )}
                      />
                      <span>{locale === 'vi-VN' ? 'Yêu thích' : 'Favorites'}</span>
                    </button>
                  )}
                </div>
              </div>
              <div className="relative">
                <Search className="w-4 h-4 text-slate-400 absolute left-3 top-1/2 -translate-y-1/2" />
                <input
                  type="text"
                  disabled={friends.length === 0}
                  placeholder={
                    locale === 'vi-VN'
                      ? 'Tìm trong danh sách bạn bè theo tên, biệt danh...'
                      : 'Search friends by name, nickname...'
                  }
                  value={searchQuery}
                  onChange={(e) => setSearchQuery(e.target.value)}
                  className={cn(
                    'w-full pl-9 pr-8 py-2 rounded-xl border border-slate-200 dark:border-slate-700 text-xs transition-colors',
                    friends.length === 0
                      ? 'bg-white/60 dark:bg-slate-950/40 text-slate-400 cursor-not-allowed'
                      : 'bg-white dark:bg-slate-900 text-slate-900 dark:text-white placeholder:text-slate-400 focus:outline-none focus:ring-1 focus:ring-emerald-500'
                  )}
                />
                {searchQuery && (
                  <button
                    type="button"
                    onClick={() => setSearchQuery('')}
                    className="absolute right-2.5 top-1/2 -translate-y-1/2 text-slate-400 hover:text-slate-600 dark:hover:text-slate-200 cursor-pointer p-0.5"
                  >
                    <X className="h-3.5 w-3.5" />
                  </button>
                )}
              </div>
            </div>

            {/* B: Remote User Lookup (Active Discovery) */}
            <div className="p-4 rounded-2xl border border-emerald-200 dark:border-emerald-800/60 bg-emerald-50/30 dark:bg-emerald-950/20">
              <div className="flex items-center justify-between mb-2">
                <span className="text-[11px] font-bold uppercase tracking-wider text-emerald-800 dark:text-emerald-300 flex items-center gap-1.5">
                  <Search className="w-3.5 h-3.5 text-emerald-600 dark:text-emerald-400" />
                  <span>{locale === 'vi-VN' ? 'Tìm người dùng mới' : 'Find new users'}</span>
                </span>
                <span className="text-[10px] text-emerald-700 dark:text-emerald-400">
                  {locale === 'vi-VN' ? 'Email hoặc SĐT chính xác' : 'Exact email or phone'}
                </span>
              </div>
              <form onSubmit={handleSearchContactSubmit} className="flex items-center gap-2">
                <div className="relative flex-1">
                  <input
                    ref={remoteInputRef}
                    type="text"
                    placeholder={
                      locale === 'vi-VN'
                        ? 'Nhập email hoặc SĐT (0xxxxxxxxx)...'
                        : 'Enter exact email or phone...'
                    }
                    value={newFriendIdentifier}
                    onChange={(e) => {
                      setNewFriendIdentifier(e.target.value);
                      if (searchContactError) setSearchContactError(null);
                    }}
                    className="w-full px-3 py-2 rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-900 text-xs text-slate-900 dark:text-white placeholder:text-slate-400 focus:outline-none focus:ring-1 focus:ring-emerald-500"
                  />
                  {newFriendIdentifier && (
                    <button
                      type="button"
                      onClick={() => {
                        setNewFriendIdentifier('');
                        setSearchContactResult(null);
                        setSearchContactError(null);
                      }}
                      className="absolute right-2.5 top-1/2 -translate-y-1/2 text-slate-400 hover:text-slate-600 dark:hover:text-slate-200 cursor-pointer p-0.5"
                    >
                      <X className="h-3.5 w-3.5" />
                    </button>
                  )}
                </div>
                <button
                  type="submit"
                  disabled={isSearchingContact || !newFriendIdentifier.trim()}
                  className="px-4 py-2 rounded-xl bg-emerald-600 hover:bg-emerald-700 disabled:opacity-50 disabled:cursor-not-allowed text-white font-semibold text-xs shadow-xs transition flex items-center gap-1.5 cursor-pointer shrink-0"
                >
                  {isSearchingContact ? (
                    <Loader2 className="w-3.5 h-3.5 animate-spin" />
                  ) : (
                    <span>{locale === 'vi-VN' ? 'Tìm' : 'Search'}</span>
                  )}
                </button>
              </form>

              {/* Inline Remote Lookup Result Slot */}
              {isSearchingContact && (
                <div className="mt-3 p-3 rounded-xl bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 flex items-center justify-center gap-2 text-slate-500 text-xs shadow-xs">
                  <Loader2 className="h-4 w-4 animate-spin text-emerald-600" />
                  <span>{locale === 'vi-VN' ? 'Đang tra cứu danh bạ...' : 'Searching contact...'}</span>
                </div>
              )}

              {searchContactError && !isSearchingContact && (
                <div className="mt-3 p-3 rounded-xl bg-rose-50 dark:bg-rose-950/40 border border-rose-200 dark:border-rose-900/60 flex items-start gap-2 text-xs text-rose-700 dark:text-rose-300">
                  <AlertCircle className="w-4 h-4 shrink-0 mt-0.5" />
                  <span>{searchContactError}</span>
                </div>
              )}

              {searchContactResult && !isSearchingContact && (
                <div className="mt-3 p-3 rounded-xl bg-white dark:bg-slate-900 border border-emerald-300 dark:border-emerald-700/80 flex items-center justify-between gap-3 shadow-xs">
                  <div className="flex items-center gap-3 min-w-0">
                    <div className="w-10 h-10 rounded-xl bg-emerald-600 text-white font-bold text-xs flex items-center justify-center shrink-0">
                      {deriveInitials(searchContactResult.fullName)}
                    </div>
                    <div className="min-w-0">
                      <div className="font-bold text-xs sm:text-sm text-slate-900 dark:text-white truncate">
                        {searchContactResult.fullName}
                      </div>
                      <div className="text-[11px] font-mono text-slate-500 dark:text-slate-400">
                        @{searchContactResult.spendId}
                      </div>
                      {searchContactResult.phone && (
                        <div className="text-[11px] font-mono text-slate-400 dark:text-slate-500 mt-0.5">
                          {searchContactResult.phone}
                        </div>
                      )}
                    </div>
                  </div>

                  {/* Actions depending on real state */}
                  {searchContactResult.state === 'SELF' && (
                    <span className="px-3 py-1.5 rounded-xl bg-slate-200 dark:bg-slate-700 text-slate-700 dark:text-slate-300 text-xs font-semibold">
                      {locale === 'vi-VN' ? 'Tài khoản của bạn' : 'Your Account'}
                    </span>
                  )}

                  {searchContactResult.state === 'FRIENDS' && (
                    <span className="flex items-center gap-1 px-3 py-1.5 rounded-xl bg-emerald-100 dark:bg-emerald-950/60 text-emerald-700 dark:text-emerald-300 text-xs font-semibold">
                      <Check className="w-3.5 h-3.5" />
                      <span>{locale === 'vi-VN' ? 'Đã là bạn bè' : 'Friends'}</span>
                    </span>
                  )}

                  {searchContactResult.state === 'REQUEST_SENT' && (
                    <span className="flex items-center gap-1 px-3 py-1.5 rounded-xl bg-amber-100 dark:bg-amber-950/60 text-amber-700 dark:text-amber-300 text-xs font-semibold">
                      <Clock className="w-3.5 h-3.5" />
                      <span>{locale === 'vi-VN' ? 'Đã gửi lời mời' : 'Request Sent'}</span>
                    </span>
                  )}

                  {searchContactResult.state === 'REQUEST_RECEIVED' && (
                    <button
                      type="button"
                      onClick={() => handleTabChange('received')}
                      className="px-3.5 py-1.5 rounded-xl bg-emerald-600 hover:bg-emerald-700 text-white text-xs font-semibold shadow-xs transition cursor-pointer"
                    >
                      {locale === 'vi-VN' ? 'Xem lời mời' : 'View Request'}
                    </button>
                  )}

                  {searchContactResult.state === 'NONE' && (
                    <button
                      type="button"
                      onClick={() => {
                        setSelectedUserForAdd({
                          id: searchContactResult.userId,
                          name: searchContactResult.fullName,
                          contact: newFriendIdentifier.trim(),
                          spendId: searchContactResult.spendId,
                          email: null,
                          phone: searchContactResult.phone,
                          avatarInitials: deriveInitials(searchContactResult.fullName),
                        });
                        setIsAddModalOpen(true);
                      }}
                      className="flex items-center gap-1.5 px-3.5 py-2 rounded-xl bg-emerald-600 hover:bg-emerald-700 text-white text-xs font-semibold shadow-xs transition cursor-pointer shrink-0"
                    >
                      <UserPlus className="w-3.5 h-3.5" />
                      <span>{locale === 'vi-VN' ? 'Kết bạn' : 'Add Friend'}</span>
                    </button>
                  )}
                </div>
              )}
            </div>
          </div>

          {/* Friends Grid or Compact Empty State */}
          {filteredFriends.length === 0 ? (
            <div className="p-8 rounded-2xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 shadow-xs text-center max-w-xl mx-auto my-6">
              <div className="w-12 h-12 rounded-xl bg-slate-100 dark:bg-slate-800 text-slate-400 dark:text-slate-500 flex items-center justify-center mx-auto mb-3">
                <Users className="w-6 h-6" />
              </div>
              <h3 className="text-sm sm:text-base font-bold text-slate-800 dark:text-slate-200">
                {searchQuery || showFavoritesOnly
                  ? locale === 'vi-VN'
                    ? 'Không tìm thấy bạn bè phù hợp'
                    : 'No matching friends found'
                  : locale === 'vi-VN'
                  ? 'Chưa có bạn bè trong danh bạ'
                  : 'No friends yet'}
              </h3>
              <p className="text-xs text-slate-500 dark:text-slate-400 mt-1 max-w-sm mx-auto">
                {searchQuery || showFavoritesOnly
                  ? locale === 'vi-VN'
                    ? 'Thử thay đổi từ khóa tìm kiếm hoặc tắt bộ lọc yêu thích.'
                    : 'Try changing your search term or disabling the favorites filter.'
                  : locale === 'vi-VN'
                  ? 'Tìm người dùng bằng email hoặc số điện thoại để gửi lời mời kết bạn.'
                  : 'Find users by email or phone number to send friend requests.'}
              </p>
            </div>
          ) : (
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
              {filteredFriends.map((friend) => (
                <div
                  key={friend.userId}
                  className="p-4 rounded-2xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 shadow-xs hover:border-emerald-500/50 dark:hover:border-emerald-500/50 transition-all flex flex-col justify-between"
                >
                  <div>
                    {/* Card Top: Favorite Star & Nickname Badge */}
                    <div className="flex items-start justify-between gap-2">
                      <div className="flex items-center gap-3 min-w-0">
                        <div className="w-12 h-12 rounded-full flex items-center justify-center shrink-0 bg-emerald-600 text-white font-bold text-base shadow-xs">
                          {friend.avatarInitials || friend.name.slice(0, 2).toUpperCase()}
                        </div>
                        <div className="min-w-0">
                          <div className="flex items-center gap-1.5">
                            <h3 className="font-semibold text-sm text-slate-900 dark:text-white truncate">
                              {friend.nickname || friend.name}
                            </h3>
                          </div>
                          {friend.nickname && (
                            <p className="text-[11px] text-slate-500 dark:text-slate-400 truncate">
                              {locale === 'vi-VN' ? 'Tên gốc:' : 'Real name:'} {friend.name}
                            </p>
                          )}
                          {friend.spendId && (
                            <div className="text-xs text-slate-500 dark:text-slate-400 truncate flex items-center gap-1.5 mt-0.5">
                              <span className="inline-flex items-center px-1.5 py-0.5 rounded text-[10px] font-mono font-medium bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-300">
                                @{friend.spendId}
                              </span>
                            </div>
                          )}
                          {friend.phone && (
                            <div className="text-xs font-mono text-slate-500 dark:text-slate-400 flex items-center gap-1.5 mt-0.5">
                              <Phone className="w-3 h-3 shrink-0 text-slate-400" />
                              <span>{friend.phone}</span>
                            </div>
                          )}
                        </div>
                      </div>

                      {/* Favorite Button */}
                      <button
                        type="button"
                        disabled={actionInProgress === friend.userId}
                        onClick={() => handleToggleFavorite(friend.userId)}
                        title={
                          friend.isFavorite
                            ? locale === 'vi-VN'
                              ? 'Bỏ yêu thích'
                              : 'Remove favorite'
                            : locale === 'vi-VN'
                            ? 'Đánh dấu yêu thích'
                            : 'Mark favorite'
                        }
                        className="p-1.5 rounded-lg text-slate-400 hover:text-amber-500 hover:bg-slate-50 dark:hover:bg-slate-800 transition cursor-pointer shrink-0 disabled:opacity-50"
                      >
                        <Star
                          className={cn(
                            'h-4 w-4 transition-colors',
                            friend.isFavorite
                              ? 'text-amber-500 fill-amber-500'
                              : 'text-slate-300 dark:text-slate-600'
                          )}
                        />
                      </button>
                    </div>
                  </div>

                  {/* Actions Footer */}
                  <div className="mt-4 pt-3 border-t border-slate-100 dark:border-slate-800 flex items-center justify-between gap-2">
                    <div className="flex items-center gap-1.5">
                      {/* Edit Nickname Button */}
                      <button
                        type="button"
                        onClick={() => setEditingFriend(friend)}
                        title={locale === 'vi-VN' ? 'Đặt biệt danh' : 'Set nickname'}
                        className="p-2 rounded-xl text-slate-500 hover:text-slate-800 dark:hover:text-slate-200 hover:bg-slate-100 dark:hover:bg-slate-800 transition cursor-pointer"
                      >
                        <Tag className="w-3.5 h-3.5" />
                      </button>

                      {/* Unfriend Button */}
                      <button
                        type="button"
                        onClick={() => setUnfriendConfirmFriend(friend)}
                        title={locale === 'vi-VN' ? 'Hủy kết bạn' : 'Unfriend'}
                        className="p-2 rounded-xl text-slate-400 hover:text-rose-600 hover:bg-rose-50 dark:hover:bg-rose-950/40 transition cursor-pointer"
                      >
                        <UserMinus className="w-3.5 h-3.5" />
                      </button>
                    </div>

                    {/* Transfer Button */}
                    <button
                      type="button"
                      onClick={() => handleTransferToFriend(friend)}
                      className="flex items-center gap-1.5 px-3.5 py-1.5 rounded-xl bg-emerald-600 hover:bg-emerald-700 text-white font-semibold text-xs shadow-xs transition cursor-pointer"
                    >
                      <Send className="w-3 h-3" />
                      <span>{locale === 'vi-VN' ? 'Chuyển tiền' : 'Transfer'}</span>
                    </button>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      )}

      {/* TAB 2: LỜI MỜI NHẬN ĐƯỢC (INCOMING REQUESTS) */}
      {!isLoading && activeTab === 'received' && (
        <div className="space-y-4">
          {incomingRequests.length === 0 ? (
            <div className="py-16 text-center border border-dashed border-slate-200 dark:border-slate-800 rounded-2xl bg-white dark:bg-slate-900/50">
              <div className="w-12 h-12 rounded-full bg-slate-100 dark:bg-slate-800 text-slate-400 flex items-center justify-center mx-auto mb-3">
                <Clock className="w-6 h-6" />
              </div>
              <h3 className="text-sm font-semibold text-slate-800 dark:text-slate-200">
                {locale === 'vi-VN' ? 'Không có lời mời nào đang chờ' : 'No pending requests'}
              </h3>
              <p className="text-xs text-slate-500 dark:text-slate-400 max-w-sm mx-auto mt-1">
                {locale === 'vi-VN'
                  ? 'Khi có người dùng gửi lời mời kết bạn kèm tin nhắn, thông báo sẽ hiển thị tại đây.'
                  : 'Incoming friend requests with personalized messages will appear here.'}
              </p>
            </div>
          ) : (
            <div className="space-y-3">
              {incomingRequests.map((req) => {
                const sender = req.senderUser;
                const timeAgo = new Date(req.createdAt).toLocaleDateString(locale, {
                  day: 'numeric',
                  month: 'numeric',
                  year: 'numeric',
                  hour: '2-digit',
                  minute: '2-digit',
                });
                const isItemBusy = actionInProgress === req.id;

                return (
                  <div
                    key={req.id}
                    className="p-4 rounded-2xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 shadow-xs flex flex-col sm:flex-row sm:items-center justify-between gap-4"
                  >
                    <div className="flex items-start gap-3 min-w-0">
                      <div className="w-12 h-12 rounded-full flex items-center justify-center shrink-0 bg-emerald-600 text-white font-bold text-sm shadow-xs">
                        {sender?.avatarInitials || 'U'}
                      </div>
                      <div className="min-w-0">
                        <div className="flex items-center gap-2">
                          <h4 className="font-semibold text-sm text-slate-900 dark:text-white truncate">
                            {sender?.name || 'Người dùng SpendWise'}
                          </h4>
                          <span className="text-[11px] text-slate-400 font-mono">
                            {timeAgo}
                          </span>
                        </div>
                        <div className="text-xs text-slate-500 dark:text-slate-400 truncate mt-0.5">
                          {sender?.email} {sender?.phone && `• ${sender.phone}`}
                        </div>
                        {req.message && (
                          <div className="mt-2 p-2.5 rounded-xl bg-slate-50 dark:bg-slate-800/80 border border-slate-100 dark:border-slate-800 text-xs text-slate-700 dark:text-slate-300 italic">
                            "{req.message}"
                          </div>
                        )}
                      </div>
                    </div>

                    <div className="flex items-center gap-2 shrink-0 self-end sm:self-center">
                      <button
                        type="button"
                        disabled={isItemBusy}
                        onClick={() => handleRejectRequest(req.id)}
                        className="px-3.5 py-2 rounded-xl text-xs font-semibold text-rose-600 dark:text-rose-400 bg-rose-50 hover:bg-rose-100 dark:bg-rose-950/50 dark:hover:bg-rose-900/50 border border-rose-200 dark:border-rose-800/80 transition cursor-pointer disabled:opacity-50"
                      >
                        {locale === 'vi-VN' ? 'Từ chối' : 'Reject'}
                      </button>
                      <button
                        type="button"
                        disabled={isItemBusy}
                        onClick={() => handleAcceptRequest(req.id, sender?.name)}
                        className="px-4 py-2 rounded-xl text-xs font-semibold bg-emerald-600 hover:bg-emerald-700 disabled:opacity-50 text-white shadow-xs transition cursor-pointer flex items-center gap-1.5"
                      >
                        {isItemBusy && <Loader2 className="w-3.5 h-3.5 animate-spin" />}
                        <span>{locale === 'vi-VN' ? 'Chấp nhận' : 'Accept'}</span>
                      </button>
                    </div>
                  </div>
                );
              })}
            </div>
          )}
        </div>
      )}

      {/* TAB 3: ĐÃ GỬI (OUTGOING REQUESTS) */}
      {!isLoading && activeTab === 'sent' && (
        <div className="space-y-4">
          {outgoingRequests.length === 0 ? (
            <div className="py-16 text-center border border-dashed border-slate-200 dark:border-slate-800 rounded-2xl bg-white dark:bg-slate-900/50">
              <div className="w-12 h-12 rounded-full bg-slate-100 dark:bg-slate-800 text-slate-400 flex items-center justify-center mx-auto mb-3">
                <Send className="w-6 h-6" />
              </div>
              <h3 className="text-sm font-semibold text-slate-800 dark:text-slate-200">
                {locale === 'vi-VN' ? 'Chưa gửi lời mời kết bạn nào' : 'No sent requests'}
              </h3>
              <p className="text-xs text-slate-500 dark:text-slate-400 max-w-sm mx-auto mt-1 mb-4">
                {locale === 'vi-VN'
                  ? 'Tìm kiếm và gửi lời mời kết bạn đến bạn bè trên SpendWise.'
                  : 'Search and send friend requests to people on SpendWise.'}
              </p>
              <button
                type="button"
                onClick={() => {
                  setIsQuickSearchOpen(true);
                  setNewFriendIdentifier('');
                  setSearchContactResult(null);
                  setSearchContactError(null);
                }}
                className="px-4 py-2 rounded-xl bg-emerald-600 hover:bg-emerald-700 text-white font-semibold text-xs shadow-xs transition cursor-pointer"
              >
                {locale === 'vi-VN' ? 'Gửi lời mời kết bạn' : 'Send a Request'}
              </button>
            </div>
          ) : (
            <div className="space-y-3">
              {outgoingRequests.map((req) => {
                const recipient = req.recipientUser;
                const timeAgo = new Date(req.createdAt).toLocaleDateString(locale, {
                  day: 'numeric',
                  month: 'numeric',
                  year: 'numeric',
                  hour: '2-digit',
                  minute: '2-digit',
                });
                const isItemBusy = actionInProgress === req.id;

                return (
                  <div
                    key={req.id}
                    className="p-4 rounded-2xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 shadow-xs flex flex-col sm:flex-row sm:items-center justify-between gap-4"
                  >
                    <div className="flex items-start gap-3 min-w-0">
                      <div className="w-12 h-12 rounded-full flex items-center justify-center shrink-0 bg-slate-200 dark:bg-slate-700 text-slate-700 dark:text-slate-200 font-bold text-sm">
                        {recipient?.avatarInitials || 'U'}
                      </div>
                      <div className="min-w-0">
                        <div className="flex items-center gap-2">
                          <h4 className="font-semibold text-sm text-slate-900 dark:text-white truncate">
                            {recipient?.name || 'Người dùng'}
                          </h4>
                          <span className="text-[10px] px-2 py-0.5 rounded-full bg-amber-50 dark:bg-amber-950/80 text-amber-700 dark:text-amber-400 font-medium">
                            {locale === 'vi-VN' ? 'Chờ chấp nhận' : 'Pending'}
                          </span>
                        </div>
                        <div className="text-xs text-slate-500 dark:text-slate-400 truncate mt-0.5">
                          {recipient?.email} {recipient?.phone && `• ${recipient.phone}`}
                        </div>
                        {req.message && (
                          <div className="mt-2 p-2.5 rounded-xl bg-slate-50 dark:bg-slate-800/80 border border-slate-100 dark:border-slate-800 text-xs text-slate-600 dark:text-slate-300 italic">
                            "{req.message}"
                          </div>
                        )}
                        <p className="text-[11px] text-slate-400 font-mono mt-1">
                          {locale === 'vi-VN' ? 'Gửi lúc:' : 'Sent at:'} {timeAgo}
                        </p>
                      </div>
                    </div>

                    <button
                      type="button"
                      disabled={isItemBusy}
                      onClick={() => handleCancelOutgoing(req.id)}
                      className="px-3.5 py-1.5 rounded-xl text-xs font-semibold text-rose-600 dark:text-rose-400 hover:bg-rose-50 dark:hover:bg-rose-950/40 border border-rose-200 dark:border-rose-900/60 transition cursor-pointer self-end sm:self-center disabled:opacity-50 flex items-center gap-1.5"
                    >
                      {isItemBusy && <Loader2 className="w-3 h-3 animate-spin" />}
                      <span>{locale === 'vi-VN' ? 'Hủy lời mời' : 'Cancel Request'}</span>
                    </button>
                  </div>
                );
              })}
            </div>
          )}
        </div>
      )}

      {/* QUICK SEARCH & ADD FRIEND MODAL */}
      {isQuickSearchOpen && (
        <div className="fixed inset-0 z-50 flex items-start justify-center pt-24 p-4 bg-slate-900/60 backdrop-blur-xs animate-in fade-in-0">
          <div className="w-full max-w-lg rounded-2xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 shadow-2xl overflow-hidden animate-in zoom-in-95">
            {/* Header */}
            <div className="flex items-center justify-between px-5 py-3.5 border-b border-slate-100 dark:border-slate-800 bg-slate-50/50 dark:bg-slate-950/40">
              <div className="flex items-center gap-2">
                <UserPlus className="h-4 w-4 text-emerald-600 dark:text-emerald-400" />
                <h3 className="text-sm font-bold text-slate-900 dark:text-white">
                  {locale === 'vi-VN' ? 'Tìm & Kết bạn mới' : 'Find & Add Friends'}
                </h3>
              </div>
              <button
                type="button"
                onClick={() => {
                  setIsQuickSearchOpen(false);
                  setNewFriendIdentifier('');
                  setSearchContactResult(null);
                  setSearchContactError(null);
                }}
                className="text-slate-400 hover:text-slate-600 dark:hover:text-slate-200 cursor-pointer p-1"
              >
                <X className="h-4 w-4" />
              </button>
            </div>

            {/* Input & Search Form */}
            <form onSubmit={handleSearchContactSubmit} className="p-4 border-b border-slate-100 dark:border-slate-800">
              <div className="flex items-center gap-2">
                <div className="relative flex-1">
                  <Search className="absolute left-3.5 top-1/2 -translate-y-1/2 h-4 w-4 text-slate-400" />
                  <input
                    type="text"
                    autoFocus
                    placeholder={
                      locale === 'vi-VN'
                        ? 'Nhập email hoặc SĐT (0xxxxxxxxx)...'
                        : 'Enter exact email or phone...'
                    }
                    value={newFriendIdentifier}
                    onChange={(e) => {
                      setNewFriendIdentifier(e.target.value);
                      if (searchContactError) setSearchContactError(null);
                    }}
                    className="w-full pl-10 pr-4 py-2.5 rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-950 text-xs sm:text-sm text-slate-900 dark:text-white placeholder:text-slate-400 focus:outline-none focus:ring-2 focus:ring-emerald-500"
                  />
                </div>
                <button
                  type="submit"
                  disabled={isSearchingContact || !newFriendIdentifier.trim()}
                  className="px-4 py-2.5 rounded-xl bg-emerald-600 hover:bg-emerald-700 disabled:opacity-50 disabled:cursor-not-allowed text-white text-xs font-semibold shadow-xs transition flex items-center gap-1.5 cursor-pointer"
                >
                  {isSearchingContact ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <Search className="w-3.5 h-3.5" />}
                  <span>{locale === 'vi-VN' ? 'Tìm' : 'Search'}</span>
                </button>
              </div>
              <p className="text-[11px] text-slate-400 dark:text-slate-500 mt-1.5 ml-1">
                {locale === 'vi-VN'
                  ? 'Tìm chính xác qua địa chỉ email hoặc số điện thoại của người dùng trên SpendWise.'
                  : 'Search by exact email or phone number registered on SpendWise.'}
              </p>
            </form>

            {/* Search Outcome */}
            <div className="p-4 min-h-[140px] flex flex-col justify-center">
              {!newFriendIdentifier.trim() && !searchContactResult && !searchContactError && (
                <div className="text-center py-6 text-slate-400 text-xs">
                  {locale === 'vi-VN'
                    ? 'Nhập email hoặc số điện thoại rồi bấm Tìm kiếm.'
                    : 'Enter contact details and press Search.'}
                </div>
              )}

              {isSearchingContact && (
                <div className="text-center py-6 flex items-center justify-center gap-2 text-slate-500 text-xs">
                  <Loader2 className="h-4 w-4 animate-spin text-emerald-600" />
                  <span>{locale === 'vi-VN' ? 'Đang tra cứu danh bạ...' : 'Searching...'}</span>
                </div>
              )}

              {searchContactError && !isSearchingContact && (
                <div className="p-3.5 rounded-xl bg-rose-50 dark:bg-rose-950/40 border border-rose-200 dark:border-rose-900/60 flex items-start gap-2.5 text-xs text-rose-700 dark:text-rose-300">
                  <AlertCircle className="w-4 h-4 shrink-0 mt-0.5" />
                  <span>{searchContactError}</span>
                </div>
              )}

              {searchContactResult && !isSearchingContact && (
                <div className="p-3.5 rounded-xl bg-slate-50 dark:bg-slate-800/60 border border-slate-200/80 dark:border-slate-700/80 flex items-center justify-between gap-3">
                  <div className="flex items-center gap-3 min-w-0">
                    <div className="w-11 h-11 rounded-full flex items-center justify-center shrink-0 bg-emerald-600 text-white font-bold text-sm shadow-xs">
                      {deriveInitials(searchContactResult.fullName)}
                    </div>
                    <div className="min-w-0">
                      <div className="font-semibold text-sm text-slate-900 dark:text-white truncate">
                        {searchContactResult.fullName}
                      </div>
                      <div className="text-xs text-slate-500 dark:text-slate-400 truncate">
                        {searchContactResult.spendId}
                      </div>
                      {searchContactResult.phone && (
                        <div className="text-[11px] font-mono text-slate-400 dark:text-slate-500 mt-0.5">
                          {searchContactResult.phone}
                        </div>
                      )}
                    </div>
                  </div>

                  {searchContactResult.state === 'SELF' && (
                    <span className="px-3 py-1.5 rounded-xl bg-slate-200 dark:bg-slate-700 text-slate-700 dark:text-slate-300 text-xs font-semibold">
                      {locale === 'vi-VN' ? 'Tài khoản của bạn' : 'Your Account'}
                    </span>
                  )}

                  {searchContactResult.state === 'FRIENDS' && (
                    <span className="flex items-center gap-1 px-3 py-1.5 rounded-xl bg-emerald-100 dark:bg-emerald-950/60 text-emerald-700 dark:text-emerald-300 text-xs font-semibold">
                      <Check className="w-3.5 h-3.5" />
                      <span>{locale === 'vi-VN' ? 'Đã là bạn bè' : 'Friends'}</span>
                    </span>
                  )}

                  {searchContactResult.state === 'REQUEST_SENT' && (
                    <span className="flex items-center gap-1 px-3 py-1.5 rounded-xl bg-amber-100 dark:bg-amber-950/60 text-amber-700 dark:text-amber-300 text-xs font-semibold">
                      <Clock className="w-3.5 h-3.5" />
                      <span>{locale === 'vi-VN' ? 'Đã gửi lời mời' : 'Request Sent'}</span>
                    </span>
                  )}

                  {searchContactResult.state === 'REQUEST_RECEIVED' && (
                    <button
                      type="button"
                      onClick={() => {
                        setIsQuickSearchOpen(false);
                        handleTabChange('received');
                      }}
                      className="px-3 py-1.5 rounded-xl bg-emerald-600 hover:bg-emerald-700 text-white text-xs font-semibold shadow-xs transition cursor-pointer"
                    >
                      {locale === 'vi-VN' ? 'Xem lời mời' : 'View Request'}
                    </button>
                  )}

                  {searchContactResult.state === 'NONE' && (
                    <button
                      type="button"
                      onClick={() => {
                        setSelectedUserForAdd({
                          id: searchContactResult.userId,
                          name: searchContactResult.fullName,
                          contact: newFriendIdentifier.trim(),
                          spendId: searchContactResult.spendId,
                          email: null,
                          phone: searchContactResult.phone,
                          avatarInitials: deriveInitials(searchContactResult.fullName),
                        });
                        setIsAddModalOpen(true);
                        setIsQuickSearchOpen(false);
                      }}
                      className="flex items-center gap-1.5 px-4 py-2 rounded-xl bg-emerald-600 hover:bg-emerald-700 text-white text-xs font-semibold shadow-xs transition cursor-pointer shrink-0"
                    >
                      <UserPlus className="w-3.5 h-3.5" />
                      <span>{locale === 'vi-VN' ? 'Kết bạn' : 'Add Friend'}</span>
                    </button>
                  )}
                </div>
              )}
            </div>
          </div>
        </div>
      )}

      {/* ADD FRIEND MODAL WITH MESSAGE */}
      <AddFriendModal
        isOpen={isAddModalOpen}
        onClose={() => {
          setIsAddModalOpen(false);
          setSelectedUserForAdd(null);
        }}
        targetUser={selectedUserForAdd}
        onSuccess={() => {
          showToast(
            locale === 'vi-VN'
              ? 'Đã gửi lời mời kết bạn thành công!'
              : 'Friend request sent successfully!'
          );
        }}
      />

      {/* EDIT NICKNAME MODAL */}
      <EditNicknameModal
        isOpen={!!editingFriend}
        onClose={() => setEditingFriend(null)}
        friend={editingFriend}
        currentUserId={currentUser?.id || ''}
        onSuccess={() => {
          showToast(locale === 'vi-VN' ? 'Đã cập nhật biệt danh.' : 'Nickname updated.');
        }}
      />

      {/* UNFRIEND CONFIRMATION MODAL */}
      {unfriendConfirmFriend && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-xs animate-in fade-in-0">
          <div className="bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-2xl w-full max-w-sm shadow-2xl p-6 text-center space-y-4 animate-in zoom-in-95">
            <div className="w-12 h-12 rounded-full bg-rose-50 dark:bg-rose-950/60 text-rose-600 dark:text-rose-400 flex items-center justify-center mx-auto">
              <UserMinus className="w-6 h-6" />
            </div>
            <div>
              <h3 className="text-base font-bold text-slate-900 dark:text-white">
                {locale === 'vi-VN' ? 'Hủy kết bạn?' : 'Unfriend?'}
              </h3>
              <p className="text-xs text-slate-500 dark:text-slate-400 mt-1 leading-relaxed">
                {locale === 'vi-VN'
                  ? `Bạn có chắc muốn hủy kết bạn với ${unfriendConfirmFriend.name}? Hai người sẽ không còn trong danh sách bạn bè của nhau.`
                  : `Are you sure you want to unfriend ${unfriendConfirmFriend.name}?`}
              </p>
            </div>
            <div className="flex items-center justify-center gap-2.5 pt-2">
              <button
                type="button"
                disabled={Boolean(actionInProgress)}
                onClick={() => setUnfriendConfirmFriend(null)}
                className="px-4 py-2 text-xs font-semibold text-slate-600 dark:text-slate-400 hover:bg-slate-100 dark:hover:bg-slate-800 rounded-xl transition cursor-pointer disabled:opacity-50"
              >
                {locale === 'vi-VN' ? 'Hủy' : 'Cancel'}
              </button>
              <button
                type="button"
                disabled={Boolean(actionInProgress)}
                onClick={handleConfirmUnfriend}
                className="px-4 py-2 text-xs font-semibold bg-rose-600 hover:bg-rose-700 disabled:opacity-50 text-white rounded-xl shadow-xs transition cursor-pointer flex items-center gap-1.5"
              >
                {actionInProgress && <Loader2 className="w-3.5 h-3.5 animate-spin" />}
                <span>{locale === 'vi-VN' ? 'Xác nhận hủy' : 'Confirm Unfriend'}</span>
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
