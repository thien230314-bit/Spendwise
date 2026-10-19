import React, { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { useI18n } from '../../../i18n/I18nContext';
import { useAuth } from '../../../providers/AuthProvider';
import { GroupMember } from '../../../types/groups';
import { friendService } from '../../../services/friendService';
import { FriendWithMeta } from '../../../types/friends';
import { getLocalizedErrorMessage } from '../../../utils/localizationHelpers';
import {
  X,
  UserPlus,
  Search,
  CheckCircle,
  Check,
  ExternalLink,
  UserCheck,
  Loader2,
} from 'lucide-react';

interface InviteMemberModalProps {
  isOpen: boolean;
  onClose: () => void;
  onInvite: (identifier: string) => Promise<{ success: boolean; member?: GroupMember; error?: string }>;
  existingMembers?: GroupMember[];
}

export const InviteMemberModal: React.FC<InviteMemberModalProps> = ({
  isOpen,
  onClose,
  onInvite,
  existingMembers = [],
}) => {
  const { locale, t } = useI18n();
  const { currentUser } = useAuth();
  const navigate = useNavigate();

  const [searchQuery, setSearchQuery] = useState('');
  const [friendsList, setFriendsList] = useState<FriendWithMeta[]>([]);
  const [isLoadingFriends, setIsLoadingFriends] = useState(false);
  const [invitingId, setInvitingId] = useState<string | null>(null);
  const [invitedMember, setInvitedMember] = useState<GroupMember | null>(null);
  const [error, setError] = useState('');
  const [successMsg, setSuccessMsg] = useState('');
  const [justInvitedIds, setJustInvitedIds] = useState<string[]>([]);

  // Load accepted friends for current user with cache refresh
  useEffect(() => {
    let isMounted = true;
    if (currentUser?.id && isOpen) {
      setIsLoadingFriends(true);
      friendService
        .refreshAll(currentUser.id)
        .then(() => {
          if (!isMounted) return;
          const friends = friendService.getFriends(currentUser.id);
          setFriendsList(friends);
        })
        .catch((err) => {
          console.warn('Failed to refresh friends list:', err);
          if (isMounted) {
            const friends = friendService.getFriends(currentUser.id);
            setFriendsList(friends);
          }
        })
        .finally(() => {
          if (isMounted) setIsLoadingFriends(false);
        });
    }
    return () => {
      isMounted = false;
    };
  }, [currentUser?.id, isOpen]);

  if (!isOpen) return null;

  // Filter friends based on search query (name, nickname, spendId, phone)
  const filteredFriends = friendsList.filter((f) => {
    if (!searchQuery.trim()) return true;
    const q = searchQuery.toLowerCase().trim();
    const nameMatch = f.name.toLowerCase().includes(q);
    const nicknameMatch = f.nickname ? f.nickname.toLowerCase().includes(q) : false;
    const spendIdMatch = f.spendId ? f.spendId.toLowerCase().includes(q) : false;
    const phoneMatch = f.phone ? f.phone.toLowerCase().includes(q) : false;
    return nameMatch || nicknameMatch || spendIdMatch || phoneMatch;
  });

  // Check if a friend is already member of this group
  const isAlreadyInGroup = (friend: FriendWithMeta) => {
    return existingMembers.some((m) => {
      if (m.userId && friend.userId && m.userId === friend.userId) return true;
      if (m.email && friend.email && m.email.toLowerCase() === friend.email.toLowerCase()) return true;
      if (m.phone && friend.phone && m.phone.trim() === friend.phone.trim()) return true;
      return false;
    });
  };

  const handleInviteFriend = async (friend: FriendWithMeta) => {
    if (invitingId) return;
    setError('');
    setSuccessMsg('');
    setInvitingId(friend.userId);

    try {
      // Preference: pass email, fallback to phone, fallback to ID
      const identifier = friend.email || friend.phone || friend.userId;
      const res = await onInvite(identifier);

      if (!res.success) {
        setError(getLocalizedErrorMessage(res.error, locale, t));
      } else {
        setJustInvitedIds((prev) => [...prev, friend.userId]);
        setInvitedMember(res.member || null);
        setSuccessMsg(
          locale === 'vi-VN'
            ? `Đã gửi lời mời đến ${friend.nickname || friend.name}`
            : `Sent invitation to ${friend.nickname || friend.name}`
        );
      }
    } catch {
      setError(locale === 'vi-VN' ? 'Đã có lỗi xảy ra.' : 'An error occurred.');
    } finally {
      setInvitingId(null);
    }
  };

  const handleClose = () => {
    setSearchQuery('');
    setInvitedMember(null);
    setError('');
    setSuccessMsg('');
    setJustInvitedIds([]);
    setInvitingId(null);
    onClose();
  };

  const handleNavigateToFriends = () => {
    handleClose();
    navigate('/friends');
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-xs animate-fadeIn">
      <div className="bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-2xl w-full max-w-lg shadow-2xl overflow-hidden animate-scaleUp">
        {/* Header */}
        <div className="flex items-center justify-between px-6 py-4 border-b border-slate-100 dark:border-slate-800 bg-slate-50/50 dark:bg-slate-950/40">
          <div className="flex items-center gap-2.5">
            <div className="p-2 rounded-xl bg-purple-50 dark:bg-purple-950/80 text-purple-600 dark:text-purple-400">
              <UserPlus className="h-5 w-5" />
            </div>
            <div>
              <h2 className="text-base font-bold text-slate-900 dark:text-slate-100">
                {locale === 'vi-VN' ? 'Mời bạn bè vào nhóm' : 'Invite Friends to Group'}
              </h2>
              <p className="text-xs text-slate-500 dark:text-slate-400">
                {locale === 'vi-VN'
                  ? 'Chỉ có thể mời người dùng đã là bạn bè'
                  : 'Only accepted friends can be invited'}
              </p>
            </div>
          </div>
          <button
            type="button"
            onClick={handleClose}
            className="p-1.5 rounded-lg text-slate-400 hover:text-slate-600 dark:hover:text-slate-200 hover:bg-slate-100 dark:hover:bg-slate-800 transition-colors cursor-pointer"
          >
            <X className="h-5 w-5" />
          </button>
        </div>

        {/* Content */}
        <div className="p-6 space-y-4">
          {error && (
            <div className="p-3 text-xs text-rose-700 dark:text-rose-400 bg-rose-50 dark:bg-rose-950/50 border border-rose-200 dark:border-rose-800 rounded-xl">
              {error}
            </div>
          )}

          {successMsg && (
            <div className="p-3 text-xs text-emerald-700 dark:text-emerald-400 bg-emerald-50 dark:bg-emerald-950/50 border border-emerald-200 dark:border-emerald-800 rounded-xl flex items-center gap-2">
              <CheckCircle className="w-4 h-4 text-emerald-500 shrink-0" />
              <span>{successMsg}</span>
            </div>
          )}

          {/* Search Bar within Accepted Friends */}
          {friendsList.length > 0 && (
            <div className="relative">
              <Search className="absolute left-3.5 top-3 h-4 w-4 text-slate-400" />
              <input
                type="text"
                placeholder={
                  locale === 'vi-VN'
                    ? 'Tìm trong danh sách bạn bè (tên, email, số điện thoại)...'
                    : 'Search friends by name, email, or phone...'
                }
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
                className="w-full pl-10 pr-4 py-2.5 rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-950 text-slate-900 dark:text-slate-100 text-sm focus:outline-none focus:ring-2 focus:ring-purple-500"
              />
            </div>
          )}

          {/* Friends List or Empty States */}
          {isLoadingFriends ? (
            <div className="py-8 flex flex-col items-center justify-center text-slate-400 text-xs">
              <Loader2 className="w-6 h-6 text-purple-600 animate-spin mb-2" />
              <span>{locale === 'vi-VN' ? 'Đang tải danh sách bạn bè...' : 'Loading friends...'}</span>
            </div>
          ) : friendsList.length === 0 ? (
            /* Clean Empty State: No friends yet */
            <div className="py-8 px-4 flex flex-col items-center text-center">
              <div className="w-12 h-12 rounded-full bg-purple-50 dark:bg-purple-950/60 text-purple-600 dark:text-purple-400 flex items-center justify-center mb-3">
                <UserCheck className="w-6 h-6" />
              </div>
              <h3 className="text-sm font-bold text-slate-800 dark:text-slate-200">
                {locale === 'vi-VN' ? 'Chưa có bạn bè để mời vào nhóm' : 'No friends yet to invite'}
              </h3>
              <p className="text-xs text-slate-500 dark:text-slate-400 mt-1 max-w-xs leading-relaxed">
                {locale === 'vi-VN'
                  ? 'Bạn chỉ có thể mời những người dùng đã kết bạn với bạn. Hãy kết bạn trước khi mời vào nhóm.'
                  : 'You can only invite users who are already your friends. Add friends first to invite them.'}
              </p>
              <button
                type="button"
                onClick={handleNavigateToFriends}
                className="mt-4 inline-flex items-center gap-1.5 px-4 py-2 rounded-xl bg-purple-600 hover:bg-purple-700 text-white text-xs font-semibold shadow-xs transition cursor-pointer"
              >
                <UserPlus className="w-3.5 h-3.5" />
                <span>{locale === 'vi-VN' ? 'Tìm & kết bạn ngay' : 'Find & Add Friends'}</span>
              </button>
            </div>
          ) : filteredFriends.length === 0 ? (
            /* No search matches */
            <div className="py-8 text-center text-slate-400 dark:text-slate-500 text-xs">
              {locale === 'vi-VN'
                ? 'Không tìm thấy bạn bè nào khớp với từ khóa tìm kiếm.'
                : 'No friends matched your search criteria.'}
            </div>
          ) : (
            /* Scrollable list of accepted friends */
            <div className="max-h-[300px] overflow-y-auto space-y-2 pr-1 divide-y divide-slate-100 dark:divide-slate-800">
              {filteredFriends.map((friend) => {
                const alreadyInGroup = isAlreadyInGroup(friend);
                const isJustInvited = justInvitedIds.includes(friend.userId);
                const displayName = friend.nickname || friend.name;

                return (
                  <div
                    key={friend.userId}
                    className="pt-2 first:pt-0 flex items-center justify-between gap-3 py-2"
                  >
                    <div className="flex items-center gap-3 min-w-0">
                      <div className="w-10 h-10 rounded-full flex items-center justify-center bg-purple-600 text-white font-bold text-sm shrink-0 shadow-xs">
                        {friend.avatarInitials || 'U'}
                      </div>
                      <div className="min-w-0">
                        <div className="flex items-center gap-1.5">
                          <span className="font-semibold text-xs sm:text-sm text-slate-900 dark:text-white truncate">
                            {displayName}
                          </span>
                          {friend.nickname && (
                            <span className="text-[11px] text-slate-400 dark:text-slate-500 truncate">
                              ({friend.name})
                            </span>
                          )}
                        </div>
                        <div className="text-[11px] text-slate-500 dark:text-slate-400 truncate flex items-center gap-1.5">
                          {friend.spendId && (
                            <span className="font-mono text-slate-600 dark:text-slate-400">@{friend.spendId}</span>
                          )}
                          {friend.spendId && friend.phone && <span>•</span>}
                          {friend.phone && (
                            <span className="font-mono">{friend.phone}</span>
                          )}
                        </div>
                      </div>
                    </div>

                    {/* Action Button / Badge */}
                    <div className="shrink-0">
                      {alreadyInGroup ? (
                        <span className="inline-flex items-center gap-1 px-2.5 py-1 rounded-lg bg-slate-100 dark:bg-slate-800 text-slate-500 dark:text-slate-400 text-xs font-medium">
                          <Check className="w-3.5 h-3.5 text-slate-400" />
                          <span>{locale === 'vi-VN' ? 'Đã tham gia' : 'In Group'}</span>
                        </span>
                      ) : isJustInvited ? (
                        <span className="inline-flex items-center gap-1 px-2.5 py-1 rounded-lg bg-emerald-50 dark:bg-emerald-950/60 text-emerald-700 dark:text-emerald-400 text-xs font-semibold border border-emerald-200 dark:border-emerald-800/60">
                          <CheckCircle className="w-3.5 h-3.5" />
                          <span>{locale === 'vi-VN' ? 'Đã mời' : 'Invited'}</span>
                        </span>
                      ) : (
                        <button
                          type="button"
                          disabled={invitingId === friend.userId}
                          onClick={() => handleInviteFriend(friend)}
                          className="flex items-center gap-1 px-3 py-1.5 rounded-xl bg-purple-600 hover:bg-purple-700 disabled:opacity-50 disabled:cursor-not-allowed text-white text-xs font-semibold shadow-xs transition-colors cursor-pointer"
                        >
                          {invitingId === friend.userId ? (
                            <Loader2 className="w-3.5 h-3.5 animate-spin" />
                          ) : (
                            <UserPlus className="w-3.5 h-3.5" />
                          )}
                          <span>{locale === 'vi-VN' ? 'Mời' : 'Invite'}</span>
                        </button>
                      )}
                    </div>
                  </div>
                );
              })}
            </div>
          )}

          {/* Footer controls */}
          <div className="flex items-center justify-between pt-4 border-t border-slate-100 dark:border-slate-800">
            <button
              type="button"
              onClick={handleNavigateToFriends}
              className="text-xs font-medium text-purple-600 hover:text-purple-700 dark:text-purple-400 hover:underline flex items-center gap-1 cursor-pointer"
            >
              <span>{locale === 'vi-VN' ? 'Quản lý danh sách bạn bè' : 'Manage Friends List'}</span>
              <ExternalLink className="w-3 h-3" />
            </button>

            <button
              type="button"
              onClick={handleClose}
              className="px-4 py-2 rounded-xl bg-slate-100 dark:bg-slate-800 text-slate-700 dark:text-slate-300 hover:bg-slate-200 dark:hover:bg-slate-700 text-xs font-medium transition cursor-pointer"
            >
              {locale === 'vi-VN' ? 'Đóng' : 'Close'}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};
